using Sunfish.Blocks.Assets.Domain;
using Sunfish.Blocks.Assets.Services;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests.Services;

public sealed class AssetWarrantyExpiryTests
{
    private static WarrantyTerm Warranty(DateTimeOffset expiresAt)
        => new()
        {
            StartsAt = expiresAt.AddYears(-1),
            ExpiresAt = expiresAt,
            Provider = "Manufacturer",
        };

    [Fact]
    public async Task ListWarrantiesExpiringBy_ReturnsOnlyExpiringWithinWindow_ScopedToTenant()
    {
        var events = new InMemoryAssetLifecycleEventStore();
        var repo = new InMemoryAssetRepository(events);

        var asOf = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

        var expiringSoon = AssetTestData.NewAsset(
            AssetTestData.TenantA, warranty: Warranty(asOf.AddDays(-1)));
        var expiringLater = AssetTestData.NewAsset(
            AssetTestData.TenantA, warranty: Warranty(asOf.AddDays(30)));
        var noWarranty = AssetTestData.NewAsset(AssetTestData.TenantA, warranty: null);
        var otherTenant = AssetTestData.NewAsset(
            AssetTestData.TenantB, warranty: Warranty(asOf.AddDays(-5)));

        await repo.UpsertAsync(expiringSoon);
        await repo.UpsertAsync(expiringLater);
        await repo.UpsertAsync(noWarranty);
        await repo.UpsertAsync(otherTenant);

        var result = await repo.ListWarrantiesExpiringByAsync(AssetTestData.TenantA, asOf);

        Assert.Single(result);
        Assert.Equal(expiringSoon.Id, result[0].Id);
    }

    [Fact]
    public async Task ListWarrantiesExpiringBy_ExcludesDisposedAssets()
    {
        var events = new InMemoryAssetLifecycleEventStore();
        var repo = new InMemoryAssetRepository(events);

        var asOf = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var asset = AssetTestData.NewAsset(AssetTestData.TenantA, warranty: Warranty(asOf.AddDays(-1)));
        await repo.UpsertAsync(asset);
        await repo.SoftDeleteAsync(AssetTestData.TenantA, asset.Id, "disposed", asOf.AddDays(-2), "operator-1");

        var result = await repo.ListWarrantiesExpiringByAsync(AssetTestData.TenantA, asOf);

        Assert.Empty(result);
    }
}
