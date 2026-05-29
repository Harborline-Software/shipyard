using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Assets.Services;

namespace Sunfish.Blocks.Assets.DependencyInjection;

/// <summary>
/// DI extension methods for registering the Sunfish asset-management domain
/// (ADR 0101 C1.1 substrate). Orthogonal to the file-catalog
/// <c>AssetCatalogBlock</c> UI surface in the same package.
/// </summary>
public static class AssetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory asset-management domain surface:
    /// <list type="bullet">
    ///   <item><see cref="IAssetLifecycleEventStore"/> → <see cref="InMemoryAssetLifecycleEventStore"/></item>
    ///   <item><see cref="IAssetRepository"/> → <see cref="InMemoryAssetRepository"/> (depends on the event store for soft-delete emission)</item>
    /// </list>
    /// Suitable for testing, prototyping, and kitchen-sink demos. Replace with a
    /// persistence-backed <see cref="IAssetRepository"/> in production hosts.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryAssets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAssetLifecycleEventStore, InMemoryAssetLifecycleEventStore>();
        services.AddSingleton<IAssetRepository, InMemoryAssetRepository>();

        return services;
    }
}
