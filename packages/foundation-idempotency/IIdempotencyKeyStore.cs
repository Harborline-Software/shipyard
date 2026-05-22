using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Idempotency;

/// <summary>
/// TTL-scoped key + body-hash store for idempotent write-path Bridge
/// endpoints. Implements the canonical replay semantic from
/// pattern-012-financial-write-path: a request that arrives with a
/// previously-seen (Idempotency-Key, TenantId, body-hash) triple returns the
/// cached response without re-executing the write; a request that arrives
/// with a previously-seen (Idempotency-Key, TenantId) pair but a DIFFERENT
/// body-hash returns HTTP 409 — never the cached response (no silent body
/// swap).
///
/// <para>
/// <b>Key scope is (Idempotency-Key, TenantId) — never global.</b> Two
/// different tenants may use the same Idempotency-Key for unrelated requests
/// without interference. Implementations MUST partition by tenant.
/// </para>
///
/// <para>
/// <b>TTL is mandatory.</b> The canonical default is 24 hours per the
/// pattern-012 spec; impl-specific stores MAY shorten the window for
/// resource-constrained hosts but MUST NOT extend it without a cross-cluster
/// ADR amendment. Expired entries SHOULD be evicted lazily on read; eager
/// background eviction is impl-detail.
/// </para>
///
/// <para>
/// <b>v1 contract.</b> Single-tenant in-process cache (in-memory) is
/// sufficient for ratification of the pattern-012 third-instance trigger.
/// A SQLite-backed impl ships under a follow-on hand-off when multi-replica
/// hosts need cross-process key visibility.
/// </para>
/// </summary>
public interface IIdempotencyKeyStore
{
    /// <summary>
    /// Fast-path lookup keyed by the canonical dedupe key (SHA-256 of
    /// <c>(idempotency-key, tenant-id, body-hash)</c>). Returns the cached
    /// response when the same triple has been seen within the TTL window;
    /// returns <c>null</c> otherwise.
    /// </summary>
    /// <remarks>
    /// Implementations MUST treat expired entries as <c>null</c>; eager
    /// eviction is impl-detail but the read MUST observe the TTL.
    /// </remarks>
    Task<IdempotencyEntry?> TryGetAsync(string dedupKey, CancellationToken ct);

    /// <summary>
    /// Slow-path lookup keyed by <c>(idempotency-key, tenant-id)</c>. Used to
    /// detect key-with-different-body collisions: when this method returns a
    /// non-null entry AND the caller's body hash differs from the stored
    /// <see cref="IdempotencyEntryWithKey.BodyHash"/>, the caller MUST return
    /// HTTP 409.
    /// </summary>
    /// <remarks>
    /// Implementations MUST partition by tenant — cross-tenant key visibility
    /// is forbidden (pattern-012 invariant). Expired entries MUST be treated
    /// as <c>null</c>.
    /// </remarks>
    Task<IdempotencyEntryWithKey?> TryGetByKeyAsync(
        string idempotencyKey,
        TenantId tenant,
        CancellationToken ct);

    /// <summary>
    /// Stores <paramref name="entry"/> for both the dedupe-key fast path and
    /// the (idempotency-key, tenant) slow path. Subsequent
    /// <see cref="TryGetAsync"/> calls with the same <paramref name="dedupKey"/>
    /// return <paramref name="entry"/> until <paramref name="ttl"/> elapses;
    /// <see cref="TryGetByKeyAsync"/> calls with the same
    /// <paramref name="idempotencyKey"/> + <paramref name="tenant"/> return an
    /// <see cref="IdempotencyEntryWithKey"/> carrying the same
    /// <paramref name="bodyHash"/>.
    /// </summary>
    /// <remarks>
    /// Implementations SHOULD reject <paramref name="ttl"/> values greater than
    /// 24 hours (the pattern-012 canonical default) — but this is a soft
    /// guard, not a hard validation: ratification of a longer-TTL deviation
    /// requires a cross-cluster ADR amendment.
    /// </remarks>
    Task SetAsync(
        string dedupKey,
        string idempotencyKey,
        TenantId tenant,
        string bodyHash,
        IdempotencyEntry entry,
        TimeSpan ttl,
        CancellationToken ct);
}
