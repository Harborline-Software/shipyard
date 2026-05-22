using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Foundation.Idempotency.DependencyInjection;

/// <summary>
/// DI extensions for the Sunfish idempotency-key subsystem. Mirrors the
/// <c>AddSunfishKernelAudit</c> shape: caller composes one of two flavors
/// depending on whether a real impl is wired or the in-memory fake suffices.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an <see cref="IIdempotencyKeyStore"/>-shaped seam without
    /// binding a concrete impl. Callers are expected to register their own
    /// production impl (SQLite, Redis) AFTER this call. Typical usage when a
    /// host wants the seam present so optional dependency injection works
    /// for endpoints, but the concrete store is wired by the host directly.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishIdempotency(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>
    /// Register the in-memory <see cref="IIdempotencyKeyStore"/> impl as a
    /// singleton, using <see cref="TimeProvider.System"/> (or the
    /// already-registered <see cref="TimeProvider"/>) for TTL evaluation.
    /// Suitable for dev / test / single-replica hosts; multi-replica
    /// production hosts should wire a SQLite- or Redis-backed impl
    /// (forward-watch scope).
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishIdempotencyInMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdempotencyKeyStore>(sp =>
            new InMemoryIdempotencyKeyStore(sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
