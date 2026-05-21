# W#60 P4 PR 2 — Journal Entries POST endpoint package design (2026-05-21)

**Authored by:** ONR (V3 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-21T12-45Z` item #2)
**Authored at:** 2026-05-21T12-46Z
**Status:** draft (sec-eng + .NET-architect SPOT-CHECK at Engineer's PR opening; CIC ratification on pattern-012 ratification cohort)

---

## Scope

Stage-03 package-design output for the JournalEntry POST endpoint that ships in W#60 P4 PR 2 (Accountant Bridge role per W#60 Phase 4 hand-off). This endpoint is the **3rd-instance anchor for `pattern-012-financial-write-path`** ratification (per V2 #4 research recommendation; pattern-010 → pattern-012 renumber landed in same PR per V3 #2).

Single Bridge POST endpoint + audit event constant + FED form integration. Pre-research per V3 #2 directive — Engineer implementation PR consumes this design.

---

## 1. Endpoint contract

### 1.1 Route + auth

```
POST /api/v1/financial/journal-entries
Authorization: AccountantPolicy (per W#60 P4 hand-off PR 2 — financial_role claim required)
Tenant scope: server-derived from ITenantContext (per ADR 0091 R2 §"Decision drivers")
```

### 1.2 Wire format — request

```http
POST /api/v1/financial/journal-entries
Content-Type: application/json
Authorization: Bearer <oidc-token>
Idempotency-Key: <caller-supplied UUID>

{
  "antiforgery_token": "<server-issued token from /api/v1/antiforgery/refresh>",
  "posting_date": "2026-05-21",
  "memo": "Q1 utility expense reclassification",
  "chart_code": "PRIMARY",
  "lines": [
    {
      "account_code": "5100",
      "amount": 250.00,
      "direction": "Debit"
    },
    {
      "account_code": "1100",
      "amount": 250.00,
      "direction": "Credit"
    }
  ]
}
```

### 1.3 Wire format — response (success)

```http
HTTP/1.1 201 Created
Location: /api/v1/financial/journal-entries/{id}

{
  "id": "01H9K2X3M5N6P7Q8R9S0T1U2V3",  // ULID
  "posted_at": "2026-05-21T12:50:23Z",
  "version": 1
}
```

### 1.4 Wire format — response (idempotent duplicate)

```http
HTTP/1.1 200 OK   // NOT 201 — caller's request was deduped

{
  "id": "01H9K2X3M5N6P7Q8R9S0T1U2V3",  // same id as original
  "posted_at": "2026-05-21T12:50:23Z",
  "version": 1,
  "_idempotency_replay": true   // hint that this was a dedup hit
}
```

### 1.5 Wire format — response (error cases)

| Status | Reason | Body shape |
|---|---|---|
| 400 | Missing / invalid antiforgery_token | `{ "error": "csrf_invalid" }` |
| 400 | Imbalanced (sum(debits) != sum(credits)) | `{ "error": "imbalanced", "debits": 250.00, "credits": 0.00 }` |
| 400 | Invalid account_code (not in tenant's chart) | `{ "error": "account_not_found", "account_code": "9999" }` |
| 400 | Empty lines OR < 2 lines | `{ "error": "minimum_two_lines" }` |
| 403 | AccountantPolicy denied (caller not accountant_role) | `{ "error": "forbidden" }` |
| 409 | Idempotency-Key collision (same key + tenant + different body) | `{ "error": "idempotency_conflict", "details": "key+tenant exists with different body" }` |
| 422 | Posting date in closed period | `{ "error": "closed_period", "period_end": "..." }` |
| 500 | Persistence failure | `{ "error": "internal" }` |

---

## 2. Sec-eng Q1/Q2/Q3 answers (per pattern-012 ratification)

### 2.1 Q1 — CSRF: INLINED (ONR recommendation)

Antiforgery token inlined in request body as `antiforgery_token` field (NOT as separate header).

**Rationale:**
- Single POST round-trip; no two-step token-fetch-then-submit
- Token validated by Bridge handler before any other field is read (fail-fast on CSRF)
- Less likely to leak in logs vs a header (Bridge can mask `antiforgery_token` field at log time; header masking requires per-request header transformer)

**Deviation from cohort-2 PR 3 precedent:** cohort-2 PR 3 RentCollection POST uses SEPARATED header form (`RequestVerificationToken: <token>`). Pattern-012 ratification adopts INLINED for forward consistency. CIC ratifies the form change at pattern-012 promotion.

### 2.2 Q2 — Idempotency-Key: MANDATORY

`Idempotency-Key: <UUID>` header REQUIRED on every POST. Server-side hash:

```
dedup_key = SHA-256(idempotency_key || ":" || tenant_id || ":" || request_body_normalized_sha256)
```

Hash stored with 24h TTL in `Sunfish.Bridge.Idempotency.IIdempotencyStore`:
- First request: store dedup_key → response; return 201
- Within 24h with same dedup_key (idempotency-key + tenant + body) → return SAME response with `_idempotency_replay: true`; HTTP 200 instead of 201
- Within 24h with same `(idempotency-key, tenant_id)` but different body → return 409 idempotency_conflict

**`IIdempotencyStore` substrate:** new primitive; lives in `signal-bridge/Sunfish.Bridge/Idempotency/` (NEW package boundary). InMemory + SQL implementations.

### 2.3 Q3 — 409 Conflict: RELOAD (ONR recommendation)

When server returns 409:
- Caused by: idempotency-key collision OR chart-version-mismatch OR posting-period-closed-mid-edit
- Frontend behavior: fetch latest server state for the relevant entities; show diff dialog to user; user reviews + decides
- User submits with **fresh** Idempotency-Key (NOT the same one) — this is the canonical pattern

**vs RETRY:** automatic retry on 409 risks double-write if the conflict was transient (e.g., concurrent period close). RELOAD forces user intent confirmation.

---

## 3. Handler implementation pseudo-code

```csharp
namespace Sunfish.Bridge.Financial;

public static class JournalEntriesEndpoints
{
    public static IEndpointRouteBuilder MapJournalEntriesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/financial/journal-entries", HandleRecordJournalEntryAsync)
           .RequireAuthorization(AccountantPolicy.PolicyName)
           .WithName("RecordJournalEntry");
        return app;
    }

    internal static async Task<Results<
        Created<RecordJournalEntryResponse>,
        Ok<RecordJournalEntryResponse>,           // idempotent duplicate
        BadRequest<ProblemDetails>,
        ForbidHttpResult,
        Conflict<ProblemDetails>,
        UnprocessableEntityHttpResult>>
      HandleRecordJournalEntryAsync(
          [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
          RecordJournalEntryRequest request,
          ITenantContext tenantContext,
          IAntiforgery antiforgery,
          HttpContext http,
          IIdempotencyStore idempotency,
          IChartCatalogService charts,
          IJournalEntryService journalEntries,
          IAuditTrail audit,
          IOperationSigner signer,
          BridgeAuditEmitter emitter,
          CancellationToken ct)
    {
        // 1. CSRF (inlined token; fail-fast)
        if (string.IsNullOrEmpty(request.AntiforgeryToken))
            return TypedResults.BadRequest(new ProblemDetails { Detail = "csrf_invalid" });
        await antiforgery.ValidateRequestAsync(http);  // throws on invalid

        // 2. Idempotency check (first-pass)
        var tenantId = new TenantId(tenantContext.TenantId);
        var bodyNormalizedHash = ComputeBodyHash(request);
        var dedupKey = ComputeDedupKey(idempotencyKey, tenantId, bodyNormalizedHash);
        var existing = await idempotency.TryGetAsync(dedupKey, ct);
        if (existing is not null)
        {
            // Idempotent replay
            return TypedResults.Ok(new RecordJournalEntryResponse(
                Id: existing.ResponseId,
                PostedAt: existing.PostedAt,
                Version: existing.Version,
                IdempotencyReplay: true));
        }

        // Check for (key, tenant) collision with different body
        var keyCollision = await idempotency.TryGetByKeyAsync(idempotencyKey, tenantId, ct);
        if (keyCollision is not null && keyCollision.BodyHash != bodyNormalizedHash)
        {
            return TypedResults.Conflict(new ProblemDetails { Detail = "idempotency_conflict" });
        }

        // 3. Validate (DR/CR balance + minimum 2 lines + chart_code resolution + account_code lookup)
        if (request.Lines.Count < 2)
            return TypedResults.BadRequest(new ProblemDetails { Detail = "minimum_two_lines" });

        var debits = request.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount);
        var credits = request.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount);
        if (debits != credits)
            return TypedResults.BadRequest(new ProblemDetails { Detail = "imbalanced", Extensions = { ["debits"] = debits, ["credits"] = credits } });

        var chartId = await charts.ResolveChartAsync(tenantId, request.ChartCode, ct);
        if (chartId is null)
            return TypedResults.BadRequest(new ProblemDetails { Detail = "chart_not_found" });

        // Account-code validation per line
        foreach (var line in request.Lines)
        {
            var accountExists = await journalEntries.AccountExistsInChartAsync(chartId.Value, line.AccountCode, ct);
            if (!accountExists)
                return TypedResults.BadRequest(new ProblemDetails { Detail = "account_not_found", Extensions = { ["account_code"] = line.AccountCode } });
        }

        // 4. Persist
        var entry = await journalEntries.PostAsync(
            tenantId,
            chartId.Value,
            request.PostingDate,
            request.Memo,
            request.Lines,
            ct);

        // 5. Idempotency store update
        await idempotency.SetAsync(dedupKey, idempotencyKey, tenantId, bodyNormalizedHash, new IdempotencyEntry(entry.Id, entry.PostedAt, entry.Version), ttl: TimeSpan.FromHours(24), ct);

        // 6. Audit emission (JournalEntryPosted; new event type)
        await EmitJournalEntryPostedAuditAsync(entry, tenantContext, audit, signer, ct);

        // 7. Return 201 with Location header
        return TypedResults.Created($"/api/v1/financial/journal-entries/{entry.Id.Value}",
            new RecordJournalEntryResponse(entry.Id.Value, entry.PostedAt, entry.Version, IdempotencyReplay: false));
    }
}
```

---

## 4. New `AuditEventType` constant

`shipyard/packages/kernel-audit/AuditEventType.cs` adds:

```csharp
public const string JournalEntryPosted = "Financial.JournalEntryPosted";
```

Audit payload includes:
- `entry_id`
- `posting_date`
- `memo` (first 200 chars)
- `chart_id`
- `line_count`
- `total_debits`
- `total_credits`
- `idempotency_key` (caller-supplied)
- `posted_at`

---

## 5. New `IIdempotencyStore` primitive

`shipyard/packages/foundation-idempotency/IIdempotencyStore.cs` (new package):

```csharp
namespace Sunfish.Foundation.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> TryGetAsync(string dedupKey, CancellationToken ct);
    Task<IdempotencyEntryWithKey?> TryGetByKeyAsync(string idempotencyKey, TenantId tenant, CancellationToken ct);
    Task SetAsync(string dedupKey, string idempotencyKey, TenantId tenant, string bodyHash, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct);
    Task ExpireAsync(string dedupKey, CancellationToken ct);
}

public sealed record IdempotencyEntry(string ResponseId, DateTimeOffset PostedAt, int Version);
public sealed record IdempotencyEntryWithKey(string DedupKey, string BodyHash, IdempotencyEntry Entry);
```

InMemory + SQL implementations. ~3-4h Engineer (new substrate primitive).

---

## 6. Engineer PR scope

| Component | Estimate | Notes |
|---|---|---|
| `IIdempotencyStore` substrate (InMemory + tests) | 2-3h | New primitive; tests |
| `JournalEntriesEndpoints.cs` Bridge handler | 2-3h | Per pseudo-code |
| `AccountantPolicy` (if not yet shipped per W#60 P4 PR 1) | 30 min | Mirror cohort-1 / cohort-2 policy precedent |
| `AuditEventType.JournalEntryPosted` constant | 15 min | Mechanical |
| Integration tests | 2-3h | CSRF + Idempotency-Key + DR/CR balance + cross-tenant + audit emission |
| FED form integration (W#60 P4 PR 2 frontend) | 3-4h | Form UI + state machine for 409 reload |

**Total:** ~10-14h Engineer (substrate + Bridge + FED).

---

## 7. Pattern-012 ratification

PR commit subject:
```
feat(blocks-financial-ledger,bridge): pattern-012 journal-entry POST (W#60 P4 PR 2; 3rd instance ratification)
```

PR description includes:
```
@candidate-pattern: pattern-012-financial-write-path (3rd instance — ratification candidate)
```

After clean shipping + sec-eng + .NET-architect SPOT-CHECKs GREEN, pattern-012 ratifies to formal via Admiral catalog-promotion PR.

---

## 8. Open questions (already routed via V3 #6 V2 council question routing strategy)

- Pattern naming conflict resolution (renumber to 012; landed in this PR) — V3 #6 dispatched
- Q1 CSRF inlined-vs-separated — V3 #6 dispatched (Top 5)
- Q2 Idempotency-Key uniqueness scope — informational; ONR recommendation applied
- Q3 RELOAD vs RETRY — informational; ONR recommendation applied

---

## 9. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #2
2. V2 #4 research (`shipyard#72`) — pattern-010 3rd-instance design rationale + Candidate A scoring
3. W#60 P4 hand-off PR 2 — Accountant journal-entry surface + AccountantPolicy + audit emission
4. ADR 0091 R2 + ADR 0092 (substrate context)
5. Cohort-2 PR 3 RentCollection POST — 1st pattern-010/012 instance precedent
6. `shipyard/_shared/engineering/standing-approved-patterns.md` (renumbered in same PR)

---

— ONR, 2026-05-21T12:46Z
