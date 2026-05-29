using Sunfish.Blocks.Assets.Domain;
using Sunfish.Blocks.Assets.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests.Services;

public sealed class InMemoryAssetRepositoryTests
{
    private static InMemoryAssetRepository NewRepo(out InMemoryAssetLifecycleEventStore events)
    {
        events = new InMemoryAssetLifecycleEventStore();
        return new InMemoryAssetRepository(events);
    }

    [Fact]
    public async Task UpsertThenGet_RoundTrips_WithinTenant()
    {
        var repo = NewRepo(out _);
        var asset = AssetTestData.NewAsset(AssetTestData.TenantA);

        await repo.UpsertAsync(asset);
        var fetched = await repo.GetByIdAsync(AssetTestData.TenantA, asset.Id);

        Assert.NotNull(fetched);
        Assert.Equal(asset.Id, fetched!.Id);
        Assert.Equal("Test asset", fetched.DisplayName);
    }

    [Fact]
    public async Task GetById_DoesNotLeakAcrossTenants()
    {
        var repo = NewRepo(out _);
        var asset = AssetTestData.NewAsset(AssetTestData.TenantA);
        await repo.UpsertAsync(asset);

        var crossTenant = await repo.GetByIdAsync(AssetTestData.TenantB, asset.Id);

        Assert.Null(crossTenant);
    }

    [Fact]
    public async Task ListByTenant_ReturnsOnlyOwnTenantRows()
    {
        var repo = NewRepo(out _);
        await repo.UpsertAsync(AssetTestData.NewAsset(AssetTestData.TenantA));
        await repo.UpsertAsync(AssetTestData.NewAsset(AssetTestData.TenantA));
        await repo.UpsertAsync(AssetTestData.NewAsset(AssetTestData.TenantB));

        var forA = await repo.ListByTenantAsync(AssetTestData.TenantA);

        Assert.Equal(2, forA.Count);
        Assert.All(forA, a => Assert.Equal(AssetTestData.TenantA, a.TenantId));
    }

    [Fact]
    public async Task ListByCategory_FiltersByCategoryAndTenant()
    {
        var repo = NewRepo(out _);
        await repo.UpsertAsync(AssetTestData.NewAsset(AssetTestData.TenantA, category: AssetCategory.FleetVehicle));
        await repo.UpsertAsync(AssetTestData.NewAsset(AssetTestData.TenantA, category: AssetCategory.ItHardware));
        await repo.UpsertAsync(AssetTestData.NewAsset(AssetTestData.TenantB, category: AssetCategory.FleetVehicle));

        var fleetForA = await repo.ListByCategoryAsync(AssetTestData.TenantA, AssetCategory.FleetVehicle);

        Assert.Single(fleetForA);
        Assert.Equal(AssetCategory.FleetVehicle, fleetForA[0].Category);
    }

    [Fact]
    public async Task SoftDelete_StampsDisposalAndExcludesFromDefaultListing()
    {
        var repo = NewRepo(out _);
        var asset = AssetTestData.NewAsset(AssetTestData.TenantA);
        await repo.UpsertAsync(asset);

        var disposedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await repo.SoftDeleteAsync(AssetTestData.TenantA, asset.Id, "sold at auction", disposedAt, "operator-1");

        var defaultList = await repo.ListByTenantAsync(AssetTestData.TenantA);
        var withDisposed = await repo.ListByTenantAsync(AssetTestData.TenantA, includeDisposed: true);
        var fetched = await repo.GetByIdAsync(AssetTestData.TenantA, asset.Id);

        Assert.Empty(defaultList);
        Assert.Single(withDisposed);
        Assert.Equal(disposedAt, fetched!.DisposedAt);
        Assert.Equal("sold at auction", fetched.DisposalReason);
        Assert.Equal(LifecycleState.Disposed, fetched.LifecycleState);
    }

    [Fact]
    public async Task SoftDelete_AppendsDisposedLifecycleEvent()
    {
        var repo = NewRepo(out var events);
        var asset = AssetTestData.NewAsset(AssetTestData.TenantA);
        await repo.UpsertAsync(asset);

        var disposedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await repo.SoftDeleteAsync(AssetTestData.TenantA, asset.Id, "scrapped", disposedAt, "operator-1");

        var history = await events.GetForAssetAsync(AssetTestData.TenantA, asset.Id);

        Assert.Single(history);
        Assert.Equal(AssetLifecycleEventType.Disposed, history[0].EventType);
        Assert.Equal(disposedAt, history[0].OccurredAt);
        Assert.Equal("operator-1", history[0].RecordedBy);
    }

    [Fact]
    public async Task SoftDelete_UnknownAsset_IsNoOp()
    {
        var repo = NewRepo(out var events);

        await repo.SoftDeleteAsync(AssetTestData.TenantA, AssetId.NewId(), "n/a", DateTimeOffset.UtcNow, "operator-1");

        var list = await repo.ListByTenantAsync(AssetTestData.TenantA, includeDisposed: true);
        Assert.Empty(list);
    }

    [Theory]
    [MemberData(nameof(SystemSentinels))]
    public async Task EveryOperation_RejectsSystemTenant(TenantId sentinel)
    {
        var repo = NewRepo(out _);
        var realAsset = AssetTestData.NewAsset(AssetTestData.TenantA);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.GetByIdAsync(sentinel, realAsset.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.ListByTenantAsync(sentinel));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.ListByCategoryAsync(sentinel, AssetCategory.Other));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.ListWarrantiesExpiringByAsync(sentinel, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SoftDeleteAsync(sentinel, realAsset.Id, "r", DateTimeOffset.UtcNow, "u"));
    }

    [Fact]
    public async Task Upsert_RejectsAssetWithSystemTenant()
    {
        var repo = NewRepo(out _);
        var sentinelAsset = AssetTestData.NewAsset(TenantId.System);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.UpsertAsync(sentinelAsset));
    }

    public static IEnumerable<object[]> SystemSentinels()
    {
        yield return new object[] { TenantId.System };
        yield return new object[] { default(TenantId) };
    }
}
