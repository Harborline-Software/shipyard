namespace Sunfish.Foundation.Idempotency;

/// <summary>
/// Cached response payload for an idempotent replay. Stored under a SHA-256
/// dedupe key derived from <c>(Idempotency-Key, TenantId, body-hash)</c>;
/// retrieved on subsequent requests with the same triple so the Bridge can
/// return the original response without re-executing the write path.
/// </summary>
/// <param name="ResponseId">
/// The opaque identifier returned to the caller on first success (e.g. a
/// ULID journal-entry id). Replayed verbatim on the 200 idempotent-replay
/// surface.
/// </param>
/// <param name="PostedAt">UTC timestamp the original write committed.</param>
/// <param name="Version">
/// Optimistic-concurrency version observed at commit time. Replayed on the
/// 200 surface so the caller can pin a later mutation against the same
/// version they originally observed.
/// </param>
public sealed record IdempotencyEntry(
    string ResponseId,
    DateTimeOffset PostedAt,
    int Version);
