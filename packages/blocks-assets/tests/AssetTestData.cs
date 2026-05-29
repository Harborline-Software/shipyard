using Sunfish.Blocks.Assets.Domain;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.Assets.Tests;

/// <summary>Shared test-data builders for the asset domain.</summary>
internal static class AssetTestData
{
    public static readonly TenantId TenantA = new("tenant-a");
    public static readonly TenantId TenantB = new("tenant-b");

    public static Asset NewAsset(
        TenantId tenant,
        AssetId? id = null,
        AssetCategory category = AssetCategory.FleetVehicle,
        LifecycleState state = LifecycleState.Active,
        WarrantyTerm? warranty = null,
        DateTimeOffset? createdAt = null)
        => new()
        {
            Id = id ?? AssetId.NewId(),
            TenantId = tenant,
            Category = category,
            DisplayName = "Test asset",
            LifecycleState = state,
            AcquisitionCost = Money.Usd(10_000m),
            Warranty = warranty,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
}
