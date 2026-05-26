using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Foundation.Integrations.DependencyInjection;

/// <summary>
/// DI helpers for the ADR 0096 Tier-2 vendor-provider substrate: register a
/// mock concrete (compile-time marker-constrained), conditionally swap in a
/// real adapter when its env-var is present, and install the production guard.
/// </summary>
public static class VendorProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TConcrete"/> (the mock) as the
    /// implementation of <typeparamref name="TContract"/>. The
    /// <c>where TConcrete : class, TContract, IMockVendorProvider</c> constraint
    /// makes "the mock carries the marker" a COMPILE error if violated, not a
    /// runtime no-op (ADR 0096, sec-eng #4). When <paramref name="descriptor"/>
    /// is supplied it is registered into DI for admin-surface enumeration
    /// (host/W79 drains DI-collected <see cref="ProviderDescriptor"/>s into
    /// <see cref="IProviderRegistry"/> at composition time).
    /// </summary>
    public static IServiceCollection AddSunfishVendorProvider<TContract, TConcrete>(
        this IServiceCollection services,
        ProviderDescriptor? descriptor = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TContract : class
        where TConcrete : class, TContract, IMockVendorProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(new ServiceDescriptor(typeof(TContract), typeof(TConcrete), lifetime));
        if (descriptor is not null)
        {
            services.AddSingleton(descriptor);
        }
        return services;
    }

    /// <summary>
    /// Conditionally replaces the registered <typeparamref name="TContract"/>
    /// with the real adapter <typeparamref name="TConcrete"/> when
    /// <paramref name="envVarKey"/> is present (non-whitespace) in the
    /// environment. Records <c>(TContract → envVarKey)</c> in the
    /// <see cref="IMockVendorEnvVarRegistry"/> UNCONDITIONALLY (source of truth
    /// for the production guard). The real adapter MUST NOT carry
    /// <see cref="IMockVendorProvider"/> (no marker constraint here).
    /// </summary>
    /// <remarks>
    /// Reads the env-var directly via <see cref="Environment.GetEnvironmentVariable(string)"/>
    /// (not <c>IConfiguration</c>) because <c>AddX</c> runs before the provider is
    /// built. Empty-string is treated as absent (prevents <c>KEY=""</c> from
    /// satisfying the present-check). The swap preserves the prior descriptor's
    /// lifetime (Option α — a Singleton mock is replaced by a Singleton real
    /// adapter).
    /// </remarks>
    public static IServiceCollection UseVendorProviderIfConfigured<TContract, TConcrete>(
        this IServiceCollection services,
        string envVarKey)
        where TContract : class
        where TConcrete : class, TContract
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(envVarKey);

        GetOrAddRegistry(services).Register(typeof(TContract), envVarKey);

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVarKey)))
        {
            var prior = services.LastOrDefault(d => d.ServiceType == typeof(TContract));
            var lifetime = prior?.Lifetime ?? ServiceLifetime.Singleton;
            services.Replace(new ServiceDescriptor(typeof(TContract), typeof(TConcrete), lifetime));
        }

        return services;
    }

    /// <summary>
    /// Installs <see cref="MockProviderProductionGuardAssertion"/> with a SNAPSHOT
    /// of the current registration tree. MUST be called LAST in the vendor-provider
    /// composition (after all <see cref="AddSunfishVendorProvider{TContract,TConcrete}"/>
    /// / <see cref="UseVendorProviderIfConfigured{TContract,TConcrete}"/> calls) so
    /// the snapshot reflects the final descriptor set.
    /// </summary>
    /// <remarks>
    /// Ordering: this guard's <c>StartAsync</c> runs AFTER ADR 0095's
    /// <c>BootstrapAndTenantMutualExclusionAssertion</c> (registration order; the
    /// W79 composition root registers the bootstrap assertion first). Both are
    /// independent invariants on disjoint composition-root properties.
    /// </remarks>
    public static IServiceCollection AddMockProviderProductionGuard(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        IReadOnlyList<ServiceDescriptor> snapshot = services.ToList();
        var registry = GetOrAddRegistry(services);
        // Plain AddSingleton (not TryAddEnumerable): the guard is registered via a
        // factory (TryAddEnumerable rejects factory descriptors), and this finalizer
        // is called exactly once at composition-root build time.
        services.AddSingleton<IHostedService>(
            _ => new MockProviderProductionGuardAssertion(snapshot, registry));
        return services;
    }

    // Shared singleton INSTANCE so composition-root-time writes + StartAsync-time
    // reads see the same registry.
    private static MockVendorEnvVarRegistry GetOrAddRegistry(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))?
            .ImplementationInstance as MockVendorEnvVarRegistry;
        if (existing is not null) return existing;

        var registry = new MockVendorEnvVarRegistry();
        services.AddSingleton<IMockVendorEnvVarRegistry>(registry);
        return registry;
    }
}
