using System.Collections.Concurrent;
using Sunfish.Blocks.Assets.Domain;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Assets.Services;

/// <summary>
/// Thread-safe in-memory <see cref="IAssetLifecycleEventStore"/> for tests,
/// demos, and kitchen-sink scenarios. Persistence-backed implementations live
/// behind the same interface in their respective hosts.
/// </summary>
/// <remarks>
/// Every operation rejects the system / default <see cref="TenantId"/>
/// (<see cref="TenantId.IsSystemSentinel"/>) — a real tenant is required, per
/// the C1.1 PASS gate (ADR 0101).
/// </remarks>
public sealed class InMemoryAssetLifecycleEventStore : IAssetLifecycleEventStore
{
    private readonly ConcurrentDictionary<(TenantId Tenant, AssetId Asset), List<AssetLifecycleEvent>> _byAsset = new();
    private readonly object _appendLock = new();

    /// <inheritdoc />
    public Task AppendAsync(AssetLifecycleEvent ev, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ev);
        TenantGuard.Require(ev.TenantId);

        lock (_appendLock)
        {
            var list = _byAsset.GetOrAdd((ev.TenantId, ev.Asset), _ => new List<AssetLifecycleEvent>());
            list.Add(ev);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AssetLifecycleEvent>> GetForAssetAsync(TenantId tenant, AssetId asset, CancellationToken cancellationToken = default)
    {
        TenantGuard.Require(tenant);

        if (!_byAsset.TryGetValue((tenant, asset), out var list))
        {
            return Task.FromResult<IReadOnlyList<AssetLifecycleEvent>>(Array.Empty<AssetLifecycleEvent>());
        }

        lock (_appendLock)
        {
            IReadOnlyList<AssetLifecycleEvent> snapshot = list.OrderBy(e => e.OccurredAt).ToList();
            return Task.FromResult(snapshot);
        }
    }
}
