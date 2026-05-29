using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Session.Tests;

public sealed class InMemorySessionStoreTests
{
    private static SessionRecord NewRecord(string id, DateTimeOffset? issued = null) => new()
    {
        SessionId = id,
        UserId = "user-1",
        TenantId = new TenantId("tenant-a"),
        IssuedUtc = issued ?? DateTimeOffset.UnixEpoch,
        AbsoluteExpiryUtc = (issued ?? DateTimeOffset.UnixEpoch) + TimeSpan.FromHours(8),
        LastSeenUtc = issued ?? DateTimeOffset.UnixEpoch,
        Reason = SessionEstablishmentReason.PasswordLogin,
    };

    [Fact]
    public async Task Create_then_get_returns_the_record()
    {
        var store = new InMemorySessionStore();
        var record = NewRecord("sid-1");

        await store.CreateAsync(record, CancellationToken.None);
        var fetched = await store.GetAsync("sid-1", CancellationToken.None);

        Assert.Equal(record, fetched);
    }

    [Fact]
    public async Task Get_unknown_id_returns_null()
    {
        var store = new InMemorySessionStore();
        Assert.Null(await store.GetAsync("nope", CancellationToken.None));
    }

    [Fact]
    public async Task Get_null_or_empty_id_returns_null_without_throwing()
    {
        var store = new InMemorySessionStore();
        Assert.Null(await store.GetAsync(null!, CancellationToken.None));
        Assert.Null(await store.GetAsync(string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task Create_duplicate_id_throws()
    {
        var store = new InMemorySessionStore();
        await store.CreateAsync(NewRecord("sid-dup"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.CreateAsync(NewRecord("sid-dup"), CancellationToken.None));
    }

    [Fact]
    public async Task Touch_advances_last_seen_and_returns_touched_record()
    {
        var store = new InMemorySessionStore();
        var t0 = DateTimeOffset.UnixEpoch;
        await store.CreateAsync(NewRecord("sid-touch", t0), CancellationToken.None);

        var t1 = t0.AddMinutes(10);
        var touched = await store.TouchAsync("sid-touch", t1, CancellationToken.None);

        Assert.NotNull(touched);
        Assert.Equal(t1, touched!.LastSeenUtc);

        // The stored copy reflects the touch.
        var fetched = await store.GetAsync("sid-touch", CancellationToken.None);
        Assert.Equal(t1, fetched!.LastSeenUtc);
    }

    [Fact]
    public async Task Touch_unknown_id_returns_null()
    {
        var store = new InMemorySessionStore();
        Assert.Null(await store.TouchAsync("nope", DateTimeOffset.UnixEpoch, CancellationToken.None));
    }

    [Fact]
    public async Task Remove_revokes_so_subsequent_get_is_null()
    {
        var store = new InMemorySessionStore();
        await store.CreateAsync(NewRecord("sid-rm"), CancellationToken.None);

        Assert.True(await store.RemoveAsync("sid-rm", CancellationToken.None));
        Assert.Null(await store.GetAsync("sid-rm", CancellationToken.None));
    }

    [Fact]
    public async Task Remove_is_idempotent_for_absent_id()
    {
        var store = new InMemorySessionStore();
        Assert.False(await store.RemoveAsync("never-existed", CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_touches_do_not_lose_the_latest_slide()
    {
        var store = new InMemorySessionStore();
        var t0 = DateTimeOffset.UnixEpoch;
        await store.CreateAsync(NewRecord("sid-concurrent", t0), CancellationToken.None);

        // 200 concurrent touches at distinct timestamps; the store must converge to a valid
        // record (no torn write, no lost entry) — the compare-and-swap loop guarantees this.
        var tasks = Enumerable.Range(1, 200).Select(i =>
            store.TouchAsync("sid-concurrent", t0.AddSeconds(i), CancellationToken.None).AsTask());
        await Task.WhenAll(tasks);

        var final = await store.GetAsync("sid-concurrent", CancellationToken.None);
        Assert.NotNull(final);
        // LastSeen is one of the submitted timestamps (t0+1s .. t0+200s), never the pre-touch t0.
        Assert.InRange(final!.LastSeenUtc, t0.AddSeconds(1), t0.AddSeconds(200));
    }

    [Fact]
    public async Task Cancellation_is_observed()
    {
        var store = new InMemorySessionStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await store.GetAsync("sid", cts.Token));
    }
}
