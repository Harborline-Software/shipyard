using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Sunfish.Foundation.Bootstrap.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="BootstrapAndTenantMutualExclusionAnalyzer"/>. Covers
/// the ADR 0095 Step 3 checklist's five test fixtures:
/// <list type="bullet">
///   <item>(a) positive — handler with only <c>IBootstrapContext</c> + <c>.AllowAnonymous()</c> → no diagnostic;</item>
///   <item>(b) negative-direct — constructor with <c>IBootstrapContext</c> +
///     <c>Sunfish.Foundation.Authorization.ITenantContext</c> → mutual-exclusion diagnostic;</item>
///   <item>(c) negative-narrowed — constructor with <c>IBootstrapContext</c> +
///     <c>Sunfish.Foundation.MultiTenancy.ITenantContext</c> narrowed variant → diagnostic;</item>
///   <item>(d) negative-browser — constructor with <c>IBootstrapContext</c> +
///     <c>IBrowserTenantContext</c> → diagnostic;</item>
///   <item>(e) inverse-failure — handler registered inside
///     <c>MapBootstrapEndpoints</c> without <c>.AllowAnonymous()</c> in the
///     fluent chain → diagnostic.</item>
/// </list>
/// </summary>
public class BootstrapAndTenantMutualExclusionAnalyzerTests
{
    private sealed class Verify : CSharpAnalyzerTest<BootstrapAndTenantMutualExclusionAnalyzer, DefaultVerifier>
    {
    }

    private static Task RunAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Verify
        {
            TestCode = source,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // Common stubs: the interfaces the analyzer matches by simple name. We
    // declare them in their canonical namespaces so QualifiedNameSyntax
    // resolution exercises the right-most-name walk.
    private const string CommonStubs = @"
namespace Sunfish.Foundation.Bootstrap
{
    public interface IBootstrapContext {}
}
namespace Sunfish.Foundation.Authorization
{
    public interface ITenantContext {}
    public interface ICurrentUser {}
    public interface IAuthorizationContext {}
}
namespace Sunfish.Foundation.MultiTenancy
{
    public interface ITenantContext {}
}
namespace Sunfish.Bridge.Middleware
{
    public interface IBrowserTenantContext {}
}
namespace Microsoft.AspNetCore.Routing
{
    public interface IEndpointRouteBuilder {}
    public interface IEndpointConventionBuilder {}
}
namespace Microsoft.AspNetCore.Builder
{
    using Microsoft.AspNetCore.Routing;
    public static class EndpointRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder MapGet(this IEndpointRouteBuilder b, string p, System.Delegate h) => null!;
        public static IEndpointConventionBuilder MapPost(this IEndpointRouteBuilder b, string p, System.Delegate h) => null!;
        public static IEndpointConventionBuilder MapPut(this IEndpointRouteBuilder b, string p, System.Delegate h) => null!;
        public static IEndpointConventionBuilder MapDelete(this IEndpointRouteBuilder b, string p, System.Delegate h) => null!;
    }
    public static class AuthorizationEndpointConventionBuilderExtensions
    {
        public static T AllowAnonymous<T>(this T builder) where T : IEndpointConventionBuilder => builder;
        public static T RequireAuthorization<T>(this T builder) where T : IEndpointConventionBuilder => builder;
    }
    public static class RoutingEndpointConventionBuilderExtensions
    {
        public static T WithName<T>(this T builder, string name) where T : IEndpointConventionBuilder => builder;
    }
}
";

    // ─── Diagnostic 1: mutual exclusion ──────────────────────────────────

    [Fact]
    public Task PositiveOnlyBootstrap_NoDiagnostic()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.Bootstrap;

class SignupHandler
{
    public SignupHandler(IBootstrapContext ctx) {}
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task PositiveOnlyPostTenant_NoDiagnostic()
    {
        const string source = CommonStubs + @"
class TenantOnlyHandler
{
    public TenantOnlyHandler(Sunfish.Foundation.Authorization.ITenantContext tenant) {}
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task NegativeDirect_FacadeITenantContext_EmitsMutualExclusion()
    {
        // (b) negative-direct: IBootstrapContext + Authorization.ITenantContext facade
        const string source = CommonStubs + @"
using Sunfish.Foundation.Bootstrap;
using Sunfish.Foundation.Authorization;

class ConfusedHandler
{
    {|#0:public ConfusedHandler(IBootstrapContext bootstrap, ITenantContext tenant) {}|}
}
";
        var expected = new DiagnosticResult(Diagnostics.MutualExclusionViolation)
            .WithLocation(0)
            .WithArguments("ConfusedHandler");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task NegativeNarrowed_MultiTenancyITenantContext_EmitsMutualExclusion()
    {
        // (c) negative-narrowed: IBootstrapContext + MultiTenancy.ITenantContext
        const string source = CommonStubs + @"
class NarrowedConfusedHandler
{
    {|#0:public NarrowedConfusedHandler(
        Sunfish.Foundation.Bootstrap.IBootstrapContext bootstrap,
        Sunfish.Foundation.MultiTenancy.ITenantContext tenant) {}|}
}
";
        var expected = new DiagnosticResult(Diagnostics.MutualExclusionViolation)
            .WithLocation(0)
            .WithArguments("NarrowedConfusedHandler");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task NegativeBrowser_IBrowserTenantContext_EmitsMutualExclusion()
    {
        // (d) negative-browser: IBootstrapContext + IBrowserTenantContext
        const string source = CommonStubs + @"
using Sunfish.Foundation.Bootstrap;
using Sunfish.Bridge.Middleware;

class BrowserConfusedHandler
{
    {|#0:public BrowserConfusedHandler(IBootstrapContext bootstrap, IBrowserTenantContext browser) {}|}
}
";
        var expected = new DiagnosticResult(Diagnostics.MutualExclusionViolation)
            .WithLocation(0)
            .WithArguments("BrowserConfusedHandler");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task NegativeICurrentUser_EmitsMutualExclusion()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.Bootstrap;
using Sunfish.Foundation.Authorization;

class CurrentUserConfusedHandler
{
    {|#0:public CurrentUserConfusedHandler(IBootstrapContext bootstrap, ICurrentUser user) {}|}
}
";
        var expected = new DiagnosticResult(Diagnostics.MutualExclusionViolation)
            .WithLocation(0)
            .WithArguments("CurrentUserConfusedHandler");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task NegativeIAuthorizationContext_EmitsMutualExclusion()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.Bootstrap;
using Sunfish.Foundation.Authorization;

class AuthzConfusedHandler
{
    {|#0:public AuthzConfusedHandler(IBootstrapContext bootstrap, IAuthorizationContext authz) {}|}
}
";
        var expected = new DiagnosticResult(Diagnostics.MutualExclusionViolation)
            .WithLocation(0)
            .WithArguments("AuthzConfusedHandler");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task NullableBootstrapWithTenant_StillEmits()
    {
        // Nullability annotation does not suppress the mutual-exclusion check.
        const string source = CommonStubs + @"
#nullable enable
using Sunfish.Foundation.Bootstrap;
using Sunfish.Foundation.Authorization;

class NullableConfusedHandler
{
    {|#0:public NullableConfusedHandler(IBootstrapContext? bootstrap, ITenantContext tenant) {}|}
}
";
        var expected = new DiagnosticResult(Diagnostics.MutualExclusionViolation)
            .WithLocation(0)
            .WithArguments("NullableConfusedHandler");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task SingleParameterCtor_NoDiagnostic()
    {
        const string source = CommonStubs + @"
using Sunfish.Foundation.Bootstrap;

class SoloHandler
{
    public SoloHandler(IBootstrapContext ctx) {}
}
";
        return RunAsync(source);
    }

    // ─── Diagnostic 2: AllowAnonymous required ───────────────────────────

    [Fact]
    public Task MapBootstrapEndpoints_WithAllowAnonymous_NoDiagnostic()
    {
        const string source = CommonStubs + @"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

static class BootstrapRoutes
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(""/api/signup"", (System.Delegate)(() => 0)).AllowAnonymous();
        endpoints.MapGet(""/api/signup/verify-email/{token}"", (System.Delegate)(() => 0))
            .AllowAnonymous()
            .WithName(""VerifyEmail"");
        return endpoints;
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task MapBootstrapEndpoints_MissingAllowAnonymous_EmitsDiagnostic()
    {
        // (e) inverse-failure: MapPost inside MapBootstrapEndpoints without AllowAnonymous
        const string source = CommonStubs + @"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

static class BootstrapRoutes
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        {|#0:endpoints.MapPost(""/api/signup"", (System.Delegate)(() => 0))|};
        return endpoints;
    }
}
";
        var expected = new DiagnosticResult(Diagnostics.AllowAnonymousMissing)
            .WithLocation(0)
            .WithArguments("MapPost");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task MapBootstrapEndpoints_WithRequireAuthorizationButNoAllowAnonymous_EmitsDiagnostic()
    {
        // RequireAuthorization is explicitly the inverse-failure case in ADR Gap D.
        const string source = CommonStubs + @"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

static class BootstrapRoutes
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        {|#0:endpoints.MapPost(""/api/signup"", (System.Delegate)(() => 0))|}.RequireAuthorization();
        return endpoints;
    }
}
";
        var expected = new DiagnosticResult(Diagnostics.AllowAnonymousMissing)
            .WithLocation(0)
            .WithArguments("MapPost");
        return RunAsync(source, expected);
    }

    [Fact]
    public Task MapBootstrapEndpoints_WithAllowAnonymousAfterWithName_NoDiagnostic()
    {
        // AllowAnonymous can appear anywhere in the fluent chain.
        const string source = CommonStubs + @"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

static class BootstrapRoutes
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(""/api/signup"", (System.Delegate)(() => 0))
            .WithName(""Signup"")
            .AllowAnonymous();
        return endpoints;
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task NonBootstrapMethod_MapWithoutAllowAnonymous_NoDiagnostic()
    {
        // The AllowAnonymous check is scoped to MapBootstrapEndpoints method names.
        const string source = CommonStubs + @"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

static class SomeOtherRoutes
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(""/api/things"", (System.Delegate)(() => 0));
        return endpoints;
    }
}
";
        return RunAsync(source);
    }

    [Fact]
    public Task MapBootstrapEndpoints_MultipleMissingAllowAnonymous_EmitsForEach()
    {
        const string source = CommonStubs + @"
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

static class BootstrapRoutes
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        {|#0:endpoints.MapPost(""/api/signup"", (System.Delegate)(() => 0))|};
        {|#1:endpoints.MapGet(""/api/signup/verify-email/{token}"", (System.Delegate)(() => 0))|};
        return endpoints;
    }
}
";
        var expected1 = new DiagnosticResult(Diagnostics.AllowAnonymousMissing)
            .WithLocation(0)
            .WithArguments("MapPost");
        var expected2 = new DiagnosticResult(Diagnostics.AllowAnonymousMissing)
            .WithLocation(1)
            .WithArguments("MapGet");
        return RunAsync(source, expected1, expected2);
    }
}
