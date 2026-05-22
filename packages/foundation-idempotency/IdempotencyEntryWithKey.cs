namespace Sunfish.Foundation.Idempotency;

/// <summary>
/// Variant of <see cref="IdempotencyEntry"/> that also carries the originally
/// observed request body hash. Returned by
/// <see cref="IIdempotencyKeyStore.TryGetByKeyAsync"/> so callers can detect
/// key-with-different-body collisions (HTTP 409 surface).
/// </summary>
/// <param name="ResponseId">As <see cref="IdempotencyEntry.ResponseId"/>.</param>
/// <param name="PostedAt">As <see cref="IdempotencyEntry.PostedAt"/>.</param>
/// <param name="Version">As <see cref="IdempotencyEntry.Version"/>.</param>
/// <param name="BodyHash">
/// SHA-256 hex digest of the canonical request body that produced the
/// cached response. Compared by callers to detect "same key, different body"
/// — which is a client bug and returns HTTP 409 instead of the cached
/// response (no silent body swap).
/// </param>
public sealed record IdempotencyEntryWithKey(
    string ResponseId,
    DateTimeOffset PostedAt,
    int Version,
    string BodyHash);
