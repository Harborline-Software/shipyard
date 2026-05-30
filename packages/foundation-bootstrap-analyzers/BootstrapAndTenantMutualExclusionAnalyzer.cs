using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sunfish.Foundation.Bootstrap.Analyzers;

/// <summary>
/// ADR 0095 Step 3 analyzer. Two diagnostics, both Error severity:
/// <list type="bullet">
///   <item><b>SUNFISH_BOOTSTRAP001</b> (mutual exclusion). Any constructor
///     that injects both <c>IBootstrapContext</c> and a post-tenant context
///     interface (<c>ITenantContext</c> facade or narrowed variant,
///     <c>IBrowserTenantContext</c>, <c>ICurrentUser</c>,
///     <c>IAuthorizationContext</c>) — flags the constructor declaration.
///     Closes the confused-deputy seam at compile time (Layer 2; PRIMARY
///     gate per ADR 0095 §Decision drivers).</item>
///   <item><b>SUNFISH_BOOTSTRAP002</b> (AllowAnonymous required). Any
///     endpoint registered inside a method named <c>MapBootstrapEndpoints</c>
///     whose fluent registration chain does NOT include
///     <c>.AllowAnonymous()</c> (Rev 2 / sec-eng Gap D — prevents
///     pre-auth-by-omission).</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Detection is purely syntactic (per ADR 0095 Rev 2 / .NET-arch A6 —
/// scope tightened from "DI-resolved dependency tree" which Roslyn cannot
/// model). The analyzer matches by parameter-type simple name; it does
/// NOT consult <see cref="SemanticModel"/> for symbol resolution, so it
/// runs cleanly against projects that haven't compiled yet. The accepted
/// 20% gap: <c>IServiceProvider.GetService&lt;T&gt;()</c> usage inside
/// method bodies and factory delegates inside
/// <c>services.AddScoped(sp =&gt; ...)</c> is not detected — same
/// trade-off as ADR 0091 R2 A2's <c>RequestContextMixingAnalyzer</c> and
/// the cohort-precedent <c>SchemaRegistrationAnalyzer</c> in
/// <c>Sunfish.Wayfinder.Analyzers</c>.
/// </para>
/// <para>
/// Diagnostic 1 fires on ANY constructor declaration in the compilation
/// that mixes the two interfaces. The ADR scope ("types registered inside
/// MapBootstrapEndpoints call-site delegate tree") is a sufficient
/// bounding condition for the security invariant; the broader scan
/// implemented here is a strict superset that captures any mixing
/// regardless of registration site, because there is no legitimate
/// use case for the mixed constructor.
/// </para>
/// <para>
/// Diagnostic 2 fires only inside methods whose declared name is
/// <c>MapBootstrapEndpoints</c>. It identifies Map* endpoint-registration
/// invocations and walks the fluent chain looking for
/// <c>.AllowAnonymous()</c>. The accepted edge: if the result of the
/// Map* call is assigned to a local and <c>AllowAnonymous</c> is
/// applied through that local in a subsequent statement, the analyzer
/// reports false positive — this is unusual; rewrite inline.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BootstrapAndTenantMutualExclusionAnalyzer : DiagnosticAnalyzer
{
    private const string BootstrapInterfaceName = "IBootstrapContext";
    private const string AllowAnonymousMethodName = "AllowAnonymous";
    private const string MapBootstrapEndpointsMethodName = "MapBootstrapEndpoints";

    /// <summary>
    /// Post-tenant interface simple-names that MUST NOT co-appear in a
    /// constructor with <c>IBootstrapContext</c>. Matched on the right-most
    /// name component, so <c>Sunfish.Foundation.Authorization.ITenantContext</c>
    /// and <c>Sunfish.Foundation.MultiTenancy.ITenantContext</c> both match
    /// <c>ITenantContext</c>.
    /// </summary>
    private static readonly HashSet<string> PostTenantInterfaceNames =
        new(StringComparer.Ordinal)
        {
            "ITenantContext",          // Authorization facade + MultiTenancy narrowed variants
            "IBrowserTenantContext",   // Bridge data-plane (5th-interface case per ADR §373)
            "ICurrentUser",            // Authorization (ADR 0091 R2 Step 1)
            "IAuthorizationContext",   // Authorization (ADR 0091 R2 Step 1)
        };

    /// <summary>
    /// HTTP-verb Map* endpoint-registration extension method names recognized
    /// inside a <c>MapBootstrapEndpoints</c> body. These are the leaf calls
    /// that actually register an endpoint, so they are the precise surface for
    /// Gap D's "explicit <c>.AllowAnonymous()</c> at registration" rule.
    /// <c>MapGroup</c> is intentionally excluded: it is a routing-tree
    /// construct, not an endpoint, and the analyzer cannot syntactically track
    /// AllowAnonymous inherited from a group down to its leaves — including it
    /// would add false positives (a group-level AllowAnonymous applied to bare
    /// leaves) without adding safety. Each leaf must carry an inline
    /// <c>.AllowAnonymous()</c>; the group-local-with-deferred-AllowAnonymous
    /// shape is the documented accepted edge (rewrite inline).
    /// </summary>
    private static readonly HashSet<string> MapEndpointMethodNames =
        new(StringComparer.Ordinal)
        {
            "MapGet",
            "MapPost",
            "MapPut",
            "MapPatch",
            "MapDelete",
            "MapMethods",
        };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            Diagnostics.MutualExclusionViolation,
            Diagnostics.AllowAnonymousMissing);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeConstructorDeclaration,
            SyntaxKind.ConstructorDeclaration);

        context.RegisterSyntaxNodeAction(
            AnalyzeMethodDeclaration,
            SyntaxKind.MethodDeclaration);
    }

    // ── Diagnostic 1: mutual-exclusion on constructor parameters ────────

    private static void AnalyzeConstructorDeclaration(SyntaxNodeAnalysisContext context)
    {
        var ctor = (ConstructorDeclarationSyntax)context.Node;
        var parameters = ctor.ParameterList.Parameters;
        if (parameters.Count < 2)
        {
            return;
        }

        bool hasBootstrap = false;
        bool hasPostTenant = false;

        foreach (var param in parameters)
        {
            var simpleName = GetTypeSimpleName(param.Type);
            if (simpleName is null)
            {
                continue;
            }

            if (simpleName == BootstrapInterfaceName)
            {
                hasBootstrap = true;
            }
            else if (PostTenantInterfaceNames.Contains(simpleName))
            {
                hasPostTenant = true;
            }

            if (hasBootstrap && hasPostTenant)
            {
                break;
            }
        }

        if (!hasBootstrap || !hasPostTenant)
        {
            return;
        }

        var containingTypeName =
            (ctor.Parent as TypeDeclarationSyntax)?.Identifier.ValueText
            ?? "(unknown type)";

        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.MutualExclusionViolation,
            ctor.GetLocation(),
            containingTypeName));
    }

    // ── Diagnostic 2: AllowAnonymous required on bootstrap endpoints ────

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.ValueText != MapBootstrapEndpointsMethodName)
        {
            return;
        }

        // Walk all invocation expressions in the method body / expression body.
        var bodyNodes = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (bodyNodes is null)
        {
            return;
        }

        foreach (var node in bodyNodes.DescendantNodes())
        {
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            if (!TryGetMapEndpointName(invocation, out var mapMethodName))
            {
                continue;
            }

            // The Map* invocation is the seed of a fluent registration chain.
            // We walk the parent chain (member-access / invocation parents)
            // looking for an .AllowAnonymous() call anywhere in the chain.
            // If absent, flag the Map* invocation site.
            if (ChainContainsAllowAnonymous(invocation))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.AllowAnonymousMissing,
                invocation.GetLocation(),
                mapMethodName));
        }
    }

    private static bool TryGetMapEndpointName(
        InvocationExpressionSyntax invocation,
        out string mapMethodName)
    {
        mapMethodName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var name = memberAccess.Name.Identifier.ValueText;
        if (!MapEndpointMethodNames.Contains(name))
        {
            return false;
        }

        mapMethodName = name;
        return true;
    }

    /// <summary>
    /// Walk the parent chain of <paramref name="mapInvocation"/> looking for
    /// a <c>MemberAccessExpression</c> whose right-hand identifier is
    /// <c>AllowAnonymous</c>. The fluent chain
    /// <c>a.Map(...).AllowAnonymous()</c> nests as
    /// <c>Invocation(MemberAccess(Invocation, AllowAnonymous))</c>, so
    /// the AllowAnonymous member-access is the parent of the Map invocation.
    /// We keep walking while still inside invocation / member-access
    /// expressions to handle chains like <c>.MapPost(...).WithName(...).AllowAnonymous()</c>.
    /// </summary>
    private static bool ChainContainsAllowAnonymous(InvocationExpressionSyntax mapInvocation)
    {
        SyntaxNode? current = mapInvocation.Parent;
        while (current is not null)
        {
            if (current is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == AllowAnonymousMethodName)
            {
                return true;
            }

            if (current is InvocationExpressionSyntax or MemberAccessExpressionSyntax)
            {
                current = current.Parent;
                continue;
            }

            return false;
        }

        return false;
    }

    // ── Shared helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Extract the right-most simple-name from a <see cref="TypeSyntax"/>.
    /// <c>Sunfish.Foundation.Authorization.ITenantContext</c> →
    /// <c>"ITenantContext"</c>; <c>IBootstrapContext?</c> →
    /// <c>"IBootstrapContext"</c>; <c>IRepository&lt;T&gt;</c> →
    /// <c>"IRepository"</c>. Returns <see langword="null"/> for shapes we
    /// don't recognize (e.g., arrays, tuples).
    /// </summary>
    private static string? GetTypeSimpleName(TypeSyntax? typeSyntax)
    {
        return typeSyntax switch
        {
            null => null,
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qualified => GetTypeSimpleName(qualified.Right),
            GenericNameSyntax generic => generic.Identifier.ValueText,
            NullableTypeSyntax nullable => GetTypeSimpleName(nullable.ElementType),
            AliasQualifiedNameSyntax aliasQualified => GetTypeSimpleName(aliasQualified.Name),
            _ => null,
        };
    }
}
