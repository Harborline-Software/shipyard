using System.Collections.Concurrent;
using Sunfish.Blocks.Assets.Domain;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Assets.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IAssetRepository"/> for tests, demos, and
/// kitchen-sink scenarios. Persistence-backed implementations live behind the
/// same interface in their respective hosts.
/// </summary>
/// <remarks>
/// Every operation rejects the system / default <see cref="TenantId"/>
/// (<see cref="TenantId.IsSystemSentinel"/>) and scopes by tenant — the
/// repository never returns assets from another tenant, per the C1.1 PASS gate
/// (ADR 0101).
/// </remarks>
public sealed class InMemoryAssetRepository : IAssetRepository
{
    private readonly ConcurrentDictionary<(TenantId Tenant, AssetId Id), Asset> _store = new();
    private readonly IAssetLifecycleEventStore _events;

    /// <summary>Create a repository wired to the given event store for soft-delete emission.</summary>
    public InMemoryAssetRepository(IAssetLifecycleEventStore events)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public Task<Asset?> GetByIdAsync(TenantId tenant, AssetId id, CancellationToken cancellationToken = default)
    {
        TenantGuard.Require(tenant);
        _store.TryGetValue((tenant, id), out var asset);
        return Task.FromResult(asset);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListByTenantAsync(TenantId tenant, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        TenantGuard.Require(tenant);

        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant))
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Asset> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListByCategoryAsync(TenantId tenant, AssetCategory category, bool includeDisposed = false, CancellationToken cancellationToken = default)
    {
        TenantGuard.Require(tenant);

        var query = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant) && kvp.Value.Category == category)
            .Select(kvp => kvp.Value);

        if (!includeDisposed)
        {
            query = query.Where(a => a.DisposedAt is null);
        }

        IReadOnlyList<Asset> result = query.ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Asset>> ListWarrantiesExpiringByAsync(TenantId tenant, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        TenantGuard.Require(tenant);

        IReadOnlyList<Asset> result = _store
            .Where(kvp => kvp.Key.Tenant.Equals(tenant))
            .Select(kvp => kvp.Value)
            .Where(a => a.DisposedAt is null
                && a.Warranty is not null
                && a.Warranty.ExpiresAt <= asOf)
            .ToList();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task UpsertAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        TenantGuard.Require(asset.TenantId);
        _store[(asset.TenantId, asset.Id)] = asset;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(TenantId tenant, AssetId id, string reason, DateTimeOffset disposedAt, string recordedBy, CancellationToken cancellationToken = default)
    {
        TenantGuard.Require(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordedBy);

        if (_store.TryGetValue((tenant, id), out var existing))
        {
            var disposed = existing with
            {
                DisposedAt = disposedAt,
                DisposalReason = reason,
                LifecycleState = LifecycleState.Disposed,
            };
            _store[(tenant, id)] = disposed;

            await _events.AppendAsync(new AssetLifecycleEvent
            {
                EventId = Guid.NewGuid(),
                Asset = id,
                TenantId = tenant,
                EventType = AssetLifecycleEventType.Disposed,
                OccurredAt = disposedAt,
                RecordedBy = recordedBy,
                Notes = reason,
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
