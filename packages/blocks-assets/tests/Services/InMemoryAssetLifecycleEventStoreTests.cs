using Sunfish.Blocks.Assets.Domain;
using Sunfish.Blocks.Assets.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests.Services;

public sealed class InMemoryAssetLifecycleEventStoreTests
{
    private static AssetLifecycleEvent Event(
        TenantId tenant,
        AssetId asset,
        AssetLifecycleEventType type,
        DateTimeOffset occurredAt)
        => new()
        {
            EventId = Guid.NewGuid(),
            Asset = asset,
            TenantId = tenant,
            EventType = type,
            OccurredAt = occurredAt,
            RecordedBy = "operator-1",
        };

    [Fact]
    public async Task Append_IsOrderedOldestFirst_OnRead()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var asset = AssetId.NewId();
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Append out of chronological order.
        await store.AppendAsync(Event(AssetTestData.TenantA, asset, AssetLifecycleEventType.Serviced, t0.AddDays(2)));
        await store.AppendAsync(Event(AssetTestData.TenantA, asset, AssetLifecycleEventType.Acquired, t0));
        await store.AppendAsync(Event(AssetTestData.TenantA, asset, AssetLifecycleEventType.Deployed, t0.AddDays(1)));

        var history = await store.GetForAssetAsync(AssetTestData.TenantA, asset);

        Assert.Equal(3, history.Count);
        Assert.Equal(AssetLifecycleEventType.Acquired, history[0].EventType);
        Assert.Equal(AssetLifecycleEventType.Deployed, history[1].EventType);
        Assert.Equal(AssetLifecycleEventType.Serviced, history[2].EventType);
    }

    [Fact]
    public async Task Append_IsAppendOnly_PriorEventsRetained()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var asset = AssetId.NewId();
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await store.AppendAsync(Event(AssetTestData.TenantA, asset, AssetLifecycleEventType.Acquired, t0));
        var afterFirst = await store.GetForAssetAsync(AssetTestData.TenantA, asset);
        await store.AppendAsync(Event(AssetTestData.TenantA, asset, AssetLifecycleEventType.Inspected, t0.AddDays(5)));
        var afterSecond = await store.GetForAssetAsync(AssetTestData.TenantA, asset);

        Assert.Single(afterFirst);
        Assert.Equal(2, afterSecond.Count);
        // The first event is unchanged — nothing mutated or removed.
        Assert.Equal(AssetLifecycleEventType.Acquired, afterSecond[0].EventType);
    }

    [Fact]
    public async Task GetForAsset_DoesNotLeakAcrossTenants()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var asset = AssetId.NewId();
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await store.AppendAsync(Event(AssetTestData.TenantA, asset, AssetLifecycleEventType.Acquired, t0));

        var forB = await store.GetForAssetAsync(AssetTestData.TenantB, asset);

        Assert.Empty(forB);
    }

    [Theory]
    [MemberData(nameof(SystemSentinels))]
    public async Task RejectsSystemTenant(TenantId sentinel)
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var asset = AssetId.NewId();

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.AppendAsync(Event(sentinel, asset, AssetLifecycleEventType.Acquired, DateTimeOffset.UtcNow)));
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.GetForAssetAsync(sentinel, asset));
    }

    public static IEnumerable<object[]> SystemSentinels()
    {
        yield return new object[] { TenantId.System };
        yield return new object[] { default(TenantId) };
    }
}
