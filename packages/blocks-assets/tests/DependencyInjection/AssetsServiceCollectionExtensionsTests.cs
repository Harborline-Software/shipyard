using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Assets.DependencyInjection;
using Sunfish.Blocks.Assets.Services;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests.DependencyInjection;

public sealed class AssetsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInMemoryAssets_RegistersRepositoryAndEventStore()
    {
        var services = new ServiceCollection();

        services.AddInMemoryAssets();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemoryAssetLifecycleEventStore>(provider.GetRequiredService<IAssetLifecycleEventStore>());
        Assert.IsType<InMemoryAssetRepository>(provider.GetRequiredService<IAssetRepository>());
    }

    [Fact]
    public void AddInMemoryAssets_RepositoryAndEventStoreShareInstanceWiring()
    {
        var services = new ServiceCollection();
        services.AddInMemoryAssets();
        using var provider = services.BuildServiceProvider();

        // Resolving twice yields the same singleton instances.
        var repo1 = provider.GetRequiredService<IAssetRepository>();
        var repo2 = provider.GetRequiredService<IAssetRepository>();
        var store1 = provider.GetRequiredService<IAssetLifecycleEventStore>();
        var store2 = provider.GetRequiredService<IAssetLifecycleEventStore>();

        Assert.Same(repo1, repo2);
        Assert.Same(store1, store2);
    }
}
