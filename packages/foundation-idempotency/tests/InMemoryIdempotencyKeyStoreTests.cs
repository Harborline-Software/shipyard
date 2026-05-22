using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Idempotency;
using Sunfish.Foundation.Idempotency.DependencyInjection;

namespace Sunfish.Foundation.Idempotency.Tests;

/// <summary>
/// Tests for <see cref="InMemoryIdempotencyKeyStore"/>. Covers the
/// pattern-012 canonical replay semantic + tenant scoping + TTL expiry +
/// collision detection.
/// </summary>
public sealed class InMemoryIdempotencyKeyStoreTests
{
    private static readonly TenantId TenantA = new("tenant-A");
    private static readonly TenantId TenantB = new("tenant-B");
    private const string DedupKey = "SHA256(test-key:tenant-A:body-hash-1)";
    private const string IdempotencyKey = "test-key";
    private const string BodyHashA = "body-hash-1";
    private const string BodyHashB = "body-hash-2";

    private static IdempotencyEntry SampleEntry(string responseId = "JE-001") =>
        new(ResponseId: responseId,
            PostedAt:   DateTimeOffset.UtcNow,
            Version:    1);

    // ── basic round-trip ──────────────────────────────────────────────────────

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenKeyNotSet()
    {
        var store = new InMemoryIdempotencyKeyStore();

        var result = await store.TryGetAsync(DedupKey, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenTryGetAsync_ReturnsCachedEntry()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var entry = SampleEntry();

        await store.SetAsync(
            dedupKey:       DedupKey,
            idempotencyKey: IdempotencyKey,
            tenant:         TenantA,
            bodyHash:       BodyHashA,
            entry:          entry,
            ttl:            TimeSpan.FromHours(24),
            ct:             CancellationToken.None);

        var fetched = await store.TryGetAsync(DedupKey, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(entry.ResponseId, fetched!.ResponseId);
        Assert.Equal(entry.PostedAt, fetched.PostedAt);
        Assert.Equal(entry.Version, fetched.Version);
    }

    [Fact]
    public async Task TryGetByKeyAsync_ReturnsEntryWithBodyHash_AfterSet()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var entry = SampleEntry();

        await store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, entry,
            TimeSpan.FromHours(24), CancellationToken.None);

        var fetched = await store.TryGetByKeyAsync(IdempotencyKey, TenantA, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal(BodyHashA, fetched!.BodyHash);
        Assert.Equal(entry.ResponseId, fetched.ResponseId);
    }

    // ── tenant scoping ────────────────────────────────────────────────────────

    [Fact]
    public async Task TryGetByKeyAsync_CrossTenantProbe_ReturnsNull()
    {
        // Same idempotency-key on two different tenants must not collide.
        var store = new InMemoryIdempotencyKeyStore();
        var entry = SampleEntry();

        await store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, entry,
            TimeSpan.FromHours(24), CancellationToken.None);

        var crossTenantProbe = await store.TryGetByKeyAsync(
            IdempotencyKey, TenantB, CancellationToken.None);

        Assert.Null(crossTenantProbe);
    }

    [Fact]
    public async Task SetAsync_TwoTenants_SameKey_AreIndependent()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var entryA = SampleEntry("JE-A");
        var entryB = SampleEntry("JE-B");
        const string dedupKeyB = "SHA256(test-key:tenant-B:body-hash-2)";

        await store.SetAsync(DedupKey,    IdempotencyKey, TenantA, BodyHashA, entryA,
            TimeSpan.FromHours(24), CancellationToken.None);
        await store.SetAsync(dedupKeyB,   IdempotencyKey, TenantB, BodyHashB, entryB,
            TimeSpan.FromHours(24), CancellationToken.None);

        var fetchedA = await store.TryGetByKeyAsync(IdempotencyKey, TenantA, CancellationToken.None);
        var fetchedB = await store.TryGetByKeyAsync(IdempotencyKey, TenantB, CancellationToken.None);

        Assert.NotNull(fetchedA);
        Assert.NotNull(fetchedB);
        Assert.Equal("JE-A", fetchedA!.ResponseId);
        Assert.Equal("JE-B", fetchedB!.ResponseId);
        Assert.Equal(BodyHashA, fetchedA.BodyHash);
        Assert.Equal(BodyHashB, fetchedB.BodyHash);
    }

    // ── collision detection (body-hash differs for same (key, tenant)) ────────

    [Fact]
    public async Task TryGetByKeyAsync_ReturnsLastWrittenBodyHash()
    {
        // SetAsync is last-write-wins for the (key, tenant) entry.
        // Bridge callers compare the returned BodyHash to the incoming body
        // and decide 409 if they differ.
        var store = new InMemoryIdempotencyKeyStore();

        await store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, SampleEntry(),
            TimeSpan.FromHours(24), CancellationToken.None);

        var fetched = await store.TryGetByKeyAsync(IdempotencyKey, TenantA, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.NotEqual(BodyHashB, fetched!.BodyHash);
        Assert.Equal(BodyHashA, fetched.BodyHash);
    }

    // ── TTL expiry ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryGetAsync_ExpiredEntry_ReturnsNull()
    {
        var startTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(startTime);
        var store = new InMemoryIdempotencyKeyStore(fake);

        await store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, SampleEntry(),
            TimeSpan.FromHours(24), CancellationToken.None);

        // T+23h → still cached
        fake.Now = startTime.AddHours(23);
        Assert.NotNull(await store.TryGetAsync(DedupKey, CancellationToken.None));

        // T+24h → expired (boundary)
        fake.Now = startTime.AddHours(24);
        Assert.Null(await store.TryGetAsync(DedupKey, CancellationToken.None));

        // T+48h → expired
        fake.Now = startTime.AddHours(48);
        Assert.Null(await store.TryGetAsync(DedupKey, CancellationToken.None));
    }

    [Fact]
    public async Task TryGetByKeyAsync_ExpiredEntry_ReturnsNull()
    {
        var startTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fake = new FakeTimeProvider(startTime);
        var store = new InMemoryIdempotencyKeyStore(fake);

        await store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, SampleEntry(),
            TimeSpan.FromHours(24), CancellationToken.None);

        fake.Now = startTime.AddHours(25);

        Assert.Null(await store.TryGetByKeyAsync(IdempotencyKey, TenantA, CancellationToken.None));
    }

    // ── validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_NonPositiveTtl_Throws()
    {
        var store = new InMemoryIdempotencyKeyStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, SampleEntry(),
                TimeSpan.Zero, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SetAsync(DedupKey, IdempotencyKey, TenantA, BodyHashA, SampleEntry(),
                TimeSpan.FromSeconds(-1), CancellationToken.None));
    }

    [Fact]
    public async Task TryGetAsync_NullOrEmptyKey_Throws()
    {
        var store = new InMemoryIdempotencyKeyStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TryGetAsync("", CancellationToken.None));
    }

    // ── DI extensions ─────────────────────────────────────────────────────────

    [Fact]
    public void AddSunfishIdempotencyInMemory_RegistersStore()
    {
        var sp = new ServiceCollection()
            .AddSunfishIdempotencyInMemory()
            .BuildServiceProvider();

        var store = sp.GetService<IIdempotencyKeyStore>();

        Assert.NotNull(store);
        Assert.IsType<InMemoryIdempotencyKeyStore>(store);
    }

    [Fact]
    public void AddSunfishIdempotency_DoesNotBindConcrete()
    {
        // The seam-only registration leaves IIdempotencyKeyStore unbound; the
        // caller must wire a production impl downstream.
        var sp = new ServiceCollection()
            .AddSunfishIdempotency()
            .BuildServiceProvider();

        var store = sp.GetService<IIdempotencyKeyStore>();

        Assert.Null(store);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public FakeTimeProvider(DateTimeOffset start) => Now = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
