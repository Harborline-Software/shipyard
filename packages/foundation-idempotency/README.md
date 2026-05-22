# Sunfish.Foundation.Idempotency

Idempotency-key primitive for write-path Bridge endpoints — substrate for
the `pattern-012-financial-write-path` standing pattern.

## What this package provides

- **`IIdempotencyKeyStore`** — TTL-scoped key + body-hash store
  abstraction. Returns cached response on `(idempotency-key, tenant,
  body-hash)` match; returns `null` on no-match; surfaces "same key
  different body" via the separate `TryGetByKeyAsync` lookup so the
  caller can render HTTP 409.
- **`IdempotencyEntry`** — minimal cached-response record (response-id +
  posted-at + version).
- **`IdempotencyEntryWithKey`** — variant carrying the originally
  observed body hash, used for collision detection.
- **`InMemoryIdempotencyKeyStore`** — in-process impl backed by two
  `ConcurrentDictionary`s; suitable for dev hosts, signal-bridge
  AppHost, and test fixtures. Single-replica.
- **`AddSunfishIdempotency()` / `AddSunfishIdempotencyInMemory()`** —
  DI extensions mirroring `AddSunfishKernelAudit*` shape.

## Canonical usage from a Bridge handler

```csharp
var bodyHash = ComputeRequestBodyHash(request);
var dedupKey = $"SHA256({idempotencyKey}:{tenantId.Value}:{bodyHash})";

var existing = await idempotency.TryGetAsync(dedupKey, ct);
if (existing is not null)
{
    return TypedResults.Ok(new RecordJournalEntryResponse(
        Id:               existing.ResponseId,
        PostedAt:         existing.PostedAt.ToString("O"),
        Version:          existing.Version,
        IdempotencyReplay: true));
}

var keyCollision = await idempotency.TryGetByKeyAsync(idempotencyKey, tenantId, ct);
if (keyCollision is not null && keyCollision.BodyHash != bodyHash)
{
    return TypedResults.Conflict(new ProblemDetails { Detail = "idempotency_conflict" });
}

// ...execute the write...

await idempotency.SetAsync(
    dedupKey:       dedupKey,
    idempotencyKey: idempotencyKey,
    tenant:         tenantId,
    bodyHash:       bodyHash,
    entry:          new IdempotencyEntry(entry.Id.Value, postedAt, version),
    ttl:            TimeSpan.FromHours(24),   // pattern-012 canonical TTL
    ct:             ct);
```

## TTL

The pattern-012 canonical TTL is **24 hours**. Callers supply the TTL
explicitly to `SetAsync` so non-financial domains may tune the window if
their ratification permits — this package does NOT enforce a TTL ceiling
at the API surface.

## Tenant scoping

Keys are partitioned by tenant. Cross-tenant probe with the same
idempotency-key returns `null` — a tenant cannot observe another
tenant's cached response, even by guessing the key.

## Multi-replica future

The in-memory impl is single-replica by construction. Multi-replica
production hosts that need cross-process key visibility should wire a
SQLite- or Redis-backed `IIdempotencyKeyStore` (forward-watch scope; not
in pattern-012 ratification minimum).
