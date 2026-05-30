using Microsoft.CodeAnalysis;

namespace Sunfish.Foundation.Bootstrap.Analyzers;

/// <summary>
/// Diagnostic descriptors for the ADR 0095 Step 3 analyzer.
/// </summary>
internal static class Diagnostics
{
    public const string MutualExclusionViolationId = "SUNFISH_BOOTSTRAP001";
    public const string AllowAnonymousMissingId = "SUNFISH_BOOTSTRAP002";

    public static readonly DiagnosticDescriptor MutualExclusionViolation = new(
        id: MutualExclusionViolationId,
        title: "Constructor injects both IBootstrapContext and a post-tenant context",
        messageFormat: "Type '{0}' has a constructor that injects both IBootstrapContext and a post-tenant context interface; the bootstrap and post-tenant scopes are mutually exclusive (ADR 0095)",
        category: "SunfishBootstrap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Per ADR 0095 Step 3 (Layer 2 enforcement; PRIMARY gate): no constructor may inject both Sunfish.Foundation.Bootstrap.IBootstrapContext and any post-tenant context interface (Sunfish.Foundation.Authorization.ITenantContext facade, Sunfish.Foundation.MultiTenancy.ITenantContext, Sunfish.Foundation.Authorization.ICurrentUser, Sunfish.Foundation.Authorization.IAuthorizationContext, Sunfish.Bridge.Middleware.IBrowserTenantContext). Mixing the two interfaces in a single scope reopens the confused-deputy seam ADR 0091 R2 amendment A1 closed. The pre-tenant and post-tenant DI scopes are structurally disjoint; the bootstrap → post-tenant transition uses a child IServiceScope per ADR 0095 §Handler Lifecycle. Severity = Error per ADR 0095 §Step 3 implementation checklist (BridgeAuditEmissionAnalyzer precedent on shipyard#71).",
        helpLinkUri: "https://github.com/ctwoodwa/shipyard/blob/main/docs/adrs/0095-bootstrap-context.md");

    public static readonly DiagnosticDescriptor AllowAnonymousMissing = new(
        id: AllowAnonymousMissingId,
        title: "Bootstrap endpoint registration is missing .AllowAnonymous()",
        messageFormat: "Endpoint registered inside MapBootstrapEndpoints via '{0}' does not declare .AllowAnonymous() in the fluent chain; bootstrap endpoints MUST be explicit about pre-auth status (ADR 0095 Rev 2 / sec-eng Gap D)",
        category: "SunfishBootstrap",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Per ADR 0095 Rev 2 / sec-eng Gap D: every handler registered inside a MapBootstrapEndpoints method body MUST declare .AllowAnonymous() at registration time — by explicit method call, not by omission of a RequireAuthorization directive. Omitting it risks the inverse failure mode: a future maintainer registers an auth-required handler via MapBootstrapEndpoints and inadvertently ships it as pre-auth-by-omission. The convention is symmetric with the bootstrap-vs-tenant mutual-exclusion analyzer rule — explicit at registration is the rule; omission is an analyzer failure. Severity = Error per ADR 0095 §Step 3 implementation checklist.",
        helpLinkUri: "https://github.com/ctwoodwa/shipyard/blob/main/docs/adrs/0095-bootstrap-context.md");
}
