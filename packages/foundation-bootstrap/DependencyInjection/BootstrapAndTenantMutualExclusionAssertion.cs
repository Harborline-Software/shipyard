using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Foundation.Bootstrap.DependencyInjection;

/// <summary>
/// Startup assertion for ADR 0095 (SECONDARY gate). Mirrors the ADR 0091 R2
/// amendment A1 <c>TenantContextScopeAssertion</c> shape. Verifies two things
/// at host start, failing closed before the first request:
/// <list type="number">
///   <item><b>Registration-presence.</b> <see cref="IBootstrapContext"/> is
///     registered scoped with a non-null concrete that implements it
///     (<c>AddSunfishBootstrapContext&lt;TConcrete&gt;()</c> was called and did
///     not bind a null factory).</item>
///   <item><b>Composition-root opt-in.</b> A post-tenant context family is also
///     wired (<c>AddSunfishTenantContext&lt;TConcrete&gt;()</c> was called) — the
///     SaaS posture requires both the pre-tenant and post-tenant surfaces.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// This assertion is REGISTRATION-PRESENCE, NOT resolution-validation (ADR 0095
/// Rev 2 / .NET-arch A3): it resolves the bindings to confirm they produce a
/// non-null instance but does NOT read any member, because the production
/// bootstrap concrete depends on <c>IHttpContextAccessor</c> whose
/// <c>HttpContext</c> is null at <see cref="IHostedService.StartAsync"/> time.
/// Per-request mutual exclusion (no scope injects both pre- and post-tenant
/// contexts) is the Step 3 <c>BootstrapAndTenantMutualExclusionAnalyzer</c>'s
/// job — the root container deliberately holds both bindings, so a startup
/// scope check cannot prove the per-scope invariant.
/// </para>
/// <para>
/// The post-tenant context is detected by reflected type name
/// (<c>Sunfish.Foundation.Authorization.ITenantContext</c>) so that
/// <c>foundation-bootstrap</c> stays structurally disjoint from
/// <c>foundation-authorization</c> (ADR 0095 §"Substrate / layering notes" —
/// neither package references the other).
/// </para>
/// </remarks>
public sealed class BootstrapAndTenantMutualExclusionAssertion : IHostedService
{
    // Assembly-qualified name of the post-tenant facade. Reflected (not a
    // compile reference) to preserve pre/post-tenant package disjointness.
    private const string PostTenantContextTypeName =
        "Sunfish.Foundation.Authorization.ITenantContext, Sunfish.Foundation.Authorization";

    private readonly IServiceScopeFactory _scopeFactory;

    public BootstrapAndTenantMutualExclusionAssertion(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // (1) Registration-presence: IBootstrapContext binds to a non-null concrete.
        var bootstrap = scope.ServiceProvider.GetService<IBootstrapContext>();
        if (bootstrap is null)
        {
            throw new InvalidOperationException(
                "ADR 0095: IBootstrapContext is not registered with a non-null concrete. "
                + "Call AddSunfishBootstrapContext<TConcrete>() in the composition root and "
                + "ensure the registration does not bind a null factory.");
        }

        // (2) Composition-root opt-in: the post-tenant context family is also wired.
        var postTenantType = Type.GetType(PostTenantContextTypeName, throwOnError: false);
        var postTenant = postTenantType is null ? null : scope.ServiceProvider.GetService(postTenantType);
        if (postTenant is null)
        {
            throw new InvalidOperationException(
                "ADR 0095: a post-tenant context family is not registered alongside the bootstrap "
                + "context. The SaaS composition root MUST also call AddSunfishTenantContext<TConcrete>() "
                + "(Sunfish.Foundation.Authorization). A bootstrap-only host mode is a forward-watch "
                + "(W79 hand-off) configuration flag and is not supported in this step.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
