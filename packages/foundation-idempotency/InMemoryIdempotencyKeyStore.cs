using System.Collections.Concurrent;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Idempotency;

/// <summary>
/// In-process <see cref="IIdempotencyKeyStore"/> backed by a pair of
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> instances — one keyed on
/// the canonical dedupe key (SHA-256 of idempotency-key+tenant+body-hash),
/// one keyed on (idempotency-key, tenant) for collision detection. Lazy
/// eviction: expired entries are not returned from <c>TryGet*</c> calls and
/// are scrubbed from the underlying dictionaries opportunistically.
///
/// <para>
/// <b>Concurrency.</b> The two backing dictionaries are independently
/// concurrent. The interleaving "<c>SetAsync</c> commits the dedupe-key entry
/// before the (key, tenant) entry" is observable; impl tolerates this as
/// long as both writes complete before the caller awaits another
/// <c>TryGet*</c> within the same logical operation (which is the canonical
/// usage pattern from Bridge handlers). Cross-request races (two parallel
/// callers with the same triple) resolve to last-write-wins; both observe a
/// consistent post-write state.
/// </para>
///
/// <para>
/// <b>TTL.</b> Configured per <c>SetAsync</c> call; the canonical default at
/// the Bridge handler layer is 24 hours per pattern-012. This impl does NOT
/// enforce a TTL ceiling at the API surface — the ceiling lives in the
/// Bridge layer that constructs the canonical TTL.
/// </para>
///
/// <para>
/// <b>Tenant scoping.</b> Both backing dictionaries include the tenant in
/// their composite keys. Cross-tenant probe with the same idempotency-key
/// returns <c>null</c> from <see cref="TryGetByKeyAsync"/> — a tenant cannot
/// observe another tenant's cached response, even by guessing the key.
/// </para>
///
/// <para>
/// Suitable for single-replica hosts (Bridge dev hosts, signal-bridge
/// AppHost, test fixtures). Multi-replica production hosts that need
/// cross-process key visibility should swap in a SQLite- or Redis-backed
/// impl (forward-watch scope; not in pattern-012 ratification minimum).
/// </para>
/// </summary>
public sealed class InMemoryIdempotencyKeyStore : IIdempotencyKeyStore
{
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, StoredEntry> _byDedupKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Key, string TenantId), StoredEntryWithKey> _byKeyTenant = new();

    /// <summary>
    /// Creates the store using <see cref="TimeProvider.System"/> for TTL
    /// expiry. Most call sites use <see cref="InMemoryIdempotencyKeyStore(TimeProvider)"/>
    /// with a test <see cref="TimeProvider"/> instead.
    /// </summary>
    public InMemoryIdempotencyKeyStore()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates the store using the supplied <see cref="TimeProvider"/> for
    /// TTL expiry. Tests use a controllable <see cref="TimeProvider"/> so
    /// expiry can be asserted without wall-clock waits.
    /// </summary>
    public InMemoryIdempotencyKeyStore(TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc />
    public Task<IdempotencyEntry?> TryGetAsync(string dedupKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(dedupKey);
        ct.ThrowIfCancellationRequested();

        if (!_byDedupKey.TryGetValue(dedupKey, out var stored))
        {
            return Task.FromResult<IdempotencyEntry?>(null);
        }

        if (IsExpired(stored.ExpiresAt))
        {
            // Lazy eviction. The compound (key, tenant) entry MAY still be
            // present — TryGetByKeyAsync evicts symmetrically on its own read.
            _byDedupKey.TryRemove(dedupKey, out _);
            return Task.FromResult<IdempotencyEntry?>(null);
        }

        return Task.FromResult<IdempotencyEntry?>(stored.Entry);
    }

    /// <inheritdoc />
    public Task<IdempotencyEntryWithKey?> TryGetByKeyAsync(
        string idempotencyKey,
        TenantId tenant,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ct.ThrowIfCancellationRequested();

        var compositeKey = (idempotencyKey, tenant.Value);
        if (!_byKeyTenant.TryGetValue(compositeKey, out var stored))
        {
            return Task.FromResult<IdempotencyEntryWithKey?>(null);
        }

        if (IsExpired(stored.ExpiresAt))
        {
            _byKeyTenant.TryRemove(compositeKey, out _);
            return Task.FromResult<IdempotencyEntryWithKey?>(null);
        }

        return Task.FromResult<IdempotencyEntryWithKey?>(stored.Entry);
    }

    /// <inheritdoc />
    public Task SetAsync(
        string dedupKey,
        string idempotencyKey,
        TenantId tenant,
        string bodyHash,
        IdempotencyEntry entry,
        TimeSpan ttl,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(dedupKey);
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentException.ThrowIfNullOrEmpty(bodyHash);
        ArgumentNullException.ThrowIfNull(entry);
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive.");
        }
        ct.ThrowIfCancellationRequested();

        var expiresAt = _time.GetUtcNow() + ttl;

        _byDedupKey[dedupKey] = new StoredEntry(entry, expiresAt);
        _byKeyTenant[(idempotencyKey, tenant.Value)] = new StoredEntryWithKey(
            new IdempotencyEntryWithKey(entry.ResponseId, entry.PostedAt, entry.Version, bodyHash),
            expiresAt);

        return Task.CompletedTask;
    }

    private bool IsExpired(DateTimeOffset expiresAt) => _time.GetUtcNow() >= expiresAt;

    private readonly record struct StoredEntry(IdempotencyEntry Entry, DateTimeOffset ExpiresAt);
    private readonly record struct StoredEntryWithKey(IdempotencyEntryWithKey Entry, DateTimeOffset ExpiresAt);
}
