using Sunfish.Blocks.Assets.Domain;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Assets.Services;

/// <summary>
/// Domain repository for <see cref="Asset"/>. First-slice surface: get, list (by
/// tenant / category), upsert, soft-delete, plus a warranty-expiry query.
/// Tenant-scoping is mandatory on every call — the repository never returns
/// assets from other tenants, and rejects the system / default
/// <see cref="TenantId"/> (a real tenant is required for every operation).
/// </summary>
public interface IAssetRepository
{
    /// <summary>Returns the asset with the given id, or <c>null</c> if not found in the tenant's scope.</summary>
    Task<Asset?> GetByIdAsync(TenantId tenant, AssetId id, CancellationToken cancellationToken = default);

    /// <summary>Lists all assets owned by the tenant. Disposed records excluded by default.</summary>
    Task<IReadOnlyList<Asset>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>Lists all assets in the given category, scoped to the tenant. Disposed excluded by default.</summary>
    Task<IReadOnlyList<Asset>> ListByCategoryAsync(TenantId tenant, AssetCategory category, bool includeDisposed = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists assets whose warranty expires on or before <paramref name="asOf"/>,
    /// scoped to the tenant. The basis for the
    /// <c>assets.warrantyReminders.enabled</c> first-slice (reminder scheduling
    /// itself is a follow-up). Disposed records and assets without a warranty are
    /// excluded.
    /// </summary>
    Task<IReadOnlyList<Asset>> ListWarrantiesExpiringByAsync(TenantId tenant, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates the asset.</summary>
    Task UpsertAsync(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes the asset by stamping <see cref="Asset.DisposedAt"/> +
    /// <see cref="Asset.DisposalReason"/> and moving its
    /// <see cref="Asset.LifecycleState"/> to <see cref="LifecycleState.Disposed"/>.
    /// ALSO appends an <see cref="AssetLifecycleEvent"/> of type
    /// <see cref="AssetLifecycleEventType.Disposed"/> via the registered
    /// <see cref="IAssetLifecycleEventStore"/>. No-op if the asset is unknown to
    /// the tenant.
    /// </summary>
    Task SoftDeleteAsync(TenantId tenant, AssetId id, string reason, DateTimeOffset disposedAt, string recordedBy, CancellationToken cancellationToken = default);
}
