using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Foundation.Bootstrap.DependencyInjection;

/// <summary>
/// DI helper for the ADR 0095 bootstrap-context wiring. Mirrors the ADR 0091
/// R2 amendment A1 DI-helper-plus-assertion shape
/// (<c>AddSunfishTenantContext&lt;TConcrete&gt;</c>): a single scoped concrete
/// backs the interface, and a startup <see cref="IHostedService"/> assertion
/// fails the host fast on a mis-wired composition root.
/// </summary>
public static class BootstrapContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TConcrete"/> as the scoped implementation
    /// of <see cref="IBootstrapContext"/>, and installs the startup
    /// <see cref="BootstrapAndTenantMutualExclusionAssertion"/>
    /// <see cref="IHostedService"/> that verifies registration-presence of
    /// <see cref="IBootstrapContext"/> plus composition-root opt-in (a
    /// post-tenant context family is also wired). Per-request mutual exclusion
    /// is enforced by the Step 3 analyzer, NOT by the assertion.
    /// </summary>
    public static IServiceCollection AddSunfishBootstrapContext<TConcrete>(
        this IServiceCollection services)
        where TConcrete : class, IBootstrapContext
    {
        // One scoped backing registration; the interface binding aliases it
        // (GetRequiredService<TConcrete>) rather than constructing a second one.
        services.AddScoped<TConcrete>();
        services.AddScoped<IBootstrapContext>(sp => sp.GetRequiredService<TConcrete>());

        // TryAddEnumerable — host may call this more than once; the assertion
        // is registered exactly once and surfaces any mis-wiring at startup.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IHostedService, BootstrapAndTenantMutualExclusionAssertion>());

        return services;
    }
}
