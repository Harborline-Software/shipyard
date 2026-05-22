# W#60 P4 PR 2 — Journal Entries POST implementation spec (2026-05-21)

**Authored by:** ONR (V5 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #4)
**Authored at:** 2026-05-21T14-42Z
**Status:** draft (implementation-level spec; sec-eng + .NET-architect council review at Engineer PR opening; pattern-012 3rd-instance ratification candidate)

---

## Scope

V3 #2 (shipyard#77) shipped the package-design for `POST /api/v1/financial/journal-entries` — endpoint contract + Q1/Q2/Q3 answers + handler pseudo-code. V5 #4 deepens to implementation-level spec:

- Exact handler method signature (Engineer copy-paste ready)
- Request/Response DTOs (matching FED TypeScript expectations)
- Substrate consumption pattern (ITenantContext + IChartCatalogService + IJournalStore + IIdempotencyKeyStore)
- Validation rules (DR/CR balance; account-id-in-chart)
- Error code mapping (E1-E6)
- Test scaffolding (audit + cross-tenant + idempotency + balance)

Companion to V3 #2 package-design + V2 #4 pattern-012 3rd-instance design.

---

## 1. Handler method signature

```csharp
// signal-bridge/Sunfish.Bridge/Financial/JournalEntriesEndpoints.cs

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
        Created<RecordJournalEntryResponse>,            // 201 first success
        Ok<RecordJournalEntryResponse>,                  // 200 idempotent replay
        BadRequest<ProblemDetails>,                      // 400 validation
        ForbidHttpResult,                                 // 403 role-based
        Conflict<ProblemDetails>,                        // 409 idempotency key collision
        UnprocessableEntityHttpResult>>                  // 422 closed period
      HandleRecordJournalEntryAsync(
          [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
          RecordJournalEntryRequest request,
          ITenantContext tenantContext,
          IAntiforgery antiforgery,
          HttpContext http,
          IIdempotencyKeyStore idempotency,
          IChartCatalogService charts,
          IJournalStore journals,
          IAuditTrail audit,
          IOperationSigner signer,
          BridgeAuditEmitter emitter,
          CancellationToken ct);
}
```

---

## 2. Request DTO (`RecordJournalEntryRequest`)

```csharp
// Lives in signal-bridge/Sunfish.Bridge/Financial/Models/RecordJournalEntryRequest.cs

namespace Sunfish.Bridge.Financial.Models;

public sealed record RecordJournalEntryRequest(
    string AntiforgeryToken,                              // inlined CSRF (Q1)
    string PostingDate,                                   // ISO date "2026-05-21"
    string Memo,                                          // max 200 chars; persisted
    string ChartCode,                                     // e.g., "PRIMARY"; resolved via IChartCatalogService
    IReadOnlyList<JournalEntryLineRequest> Lines);

public sealed record JournalEntryLineRequest(
    string AccountCode,                                   // e.g., "5100"
    decimal Amount,                                       // > 0
    string Direction);                                    // "Debit" | "Credit"
```

TypeScript equivalents (FED-side; if any draft exists in `sunfish/apps/web/src/api/financial.ts`):

```typescript
export interface RecordJournalEntryRequest {
  antiforgery_token: string;
  posting_date: string;            // ISO date
  memo: string;
  chart_code: string;
  lines: JournalEntryLineRequest[];
}

export interface JournalEntryLineRequest {
  account_code: string;
  amount: number;
  direction: 'Debit' | 'Credit';
}
```

---

## 3. Response DTO (`RecordJournalEntryResponse`)

```csharp
public sealed record RecordJournalEntryResponse(
    string Id,                                            // ULID
    string PostedAt,                                      // ISO timestamp
    int Version,                                          // optimistic concurrency version
    bool IdempotencyReplay = false);                      // true on 200 (duplicate); false on 201 (new)
```

TypeScript:

```typescript
export interface RecordJournalEntryResponse {
  id: string;                       // ULID
  posted_at: string;                // ISO timestamp
  version: number;
  _idempotency_replay?: boolean;    // hint flag
}
```

---

## 4. Substrate consumption pattern

### 4.1 ITenantContext

Server-side tenant resolution; never accept tenant_id from caller. Per ADR 0091 R2 §"Decision drivers."

```csharp
var tenantId = new TenantId(tenantContext.TenantId);
```

### 4.2 IChartCatalogService

```csharp
var chartId = await charts.ResolveChartAsync(tenantId, request.ChartCode, ct);
if (chartId is null)
{
    return TypedResults.BadRequest(new ProblemDetails {
        Detail = "chart_not_found",
        Extensions = { ["chart_code"] = request.ChartCode }
    });
}
```

Per V2 #5 multi-chart-per-tenant research + V3 #2 package-design.

### 4.3 IJournalStore

```csharp
var entry = await journals.PostAsync(
    tenantId: tenantId,                                  // tenant-keyed per cohort-2 PR 0d
    chartId: chartId.Value,
    postingDate: parsedPostingDate,
    memo: request.Memo,
    lines: request.Lines.Select(MapToServiceLine).ToArray(),
    ct);
```

### 4.4 IIdempotencyKeyStore (NEW primitive per V3 #2)

```csharp
// Lives in shipyard/packages/foundation-idempotency/IIdempotencyKeyStore.cs (NEW package)

public interface IIdempotencyKeyStore
{
    Task<IdempotencyEntry?> TryGetAsync(string dedupKey, CancellationToken ct);
    Task<IdempotencyEntryWithKey?> TryGetByKeyAsync(
        string idempotencyKey,
        TenantId tenant,
        CancellationToken ct);
    Task SetAsync(
        string dedupKey,
        string idempotencyKey,
        TenantId tenant,
        string bodyHash,
        IdempotencyEntry entry,
        TimeSpan ttl,
        CancellationToken ct);
}

public sealed record IdempotencyEntry(
    string ResponseId,
    DateTimeOffset PostedAt,
    int Version);
```

Consumption:

```csharp
var bodyHash = ComputeRequestBodyHash(request);
var dedupKey = $"SHA256({idempotencyKey}:{tenantId.Value}:{bodyHash})";

var existing = await idempotency.TryGetAsync(dedupKey, ct);
if (existing is not null)
{
    return TypedResults.Ok(new RecordJournalEntryResponse(
        Id: existing.ResponseId,
        PostedAt: existing.PostedAt.ToString("O"),
        Version: existing.Version,
        IdempotencyReplay: true));
}

var keyCollision = await idempotency.TryGetByKeyAsync(idempotencyKey, tenantId, ct);
if (keyCollision is not null && keyCollision.BodyHash != bodyHash)
{
    return TypedResults.Conflict(new ProblemDetails { Detail = "idempotency_conflict" });
}
```

### 4.5 IAntiforgery (CSRF)

```csharp
if (string.IsNullOrEmpty(request.AntiforgeryToken))
{
    return TypedResults.BadRequest(new ProblemDetails { Detail = "csrf_invalid" });
}

await antiforgery.ValidateRequestAsync(http);    // throws on invalid header / cookie binding
```

CSRF inlined per Q1 ONR recommendation (V3 #2); token in request body.

### 4.6 AuditTrail emission

```csharp
var occurredAt = DateTimeOffset.UtcNow;
var payload = new AuditPayload(new Dictionary<string, object?>
{
    ["entry_id"]       = entry.Id.Value,
    ["chart_id"]       = chartId.Value.Value,
    ["posting_date"]   = parsedPostingDate.ToString("O"),
    ["memo"]           = TruncateForAudit(request.Memo, maxChars: 200),
    ["line_count"]     = request.Lines.Count,
    ["total_debits"]   = totalDebits,
    ["total_credits"]  = totalCredits,
    ["idempotency_key"] = idempotencyKey,
});
var signed = await signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct);
await audit.AppendAsync(new AuditRecord(
    AuditId:             Guid.NewGuid(),
    TenantId:            tenantId,
    EventType:           AuditEventType.JournalEntryPosted,       // NEW constant
    OccurredAt:          occurredAt,
    Payload:             signed,
    AttestingSignatures: ImmutableArray<AttestingSignature>.Empty), ct);
```

---

## 5. Validation rules

### 5.1 Minimum 2 lines

```csharp
if (request.Lines.Count < 2)
{
    return TypedResults.BadRequest(new ProblemDetails { Detail = "minimum_two_lines" });
}
```

### 5.2 DR/CR balance (canonical financial invariant)

```csharp
var totalDebits  = request.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount);
var totalCredits = request.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount);

// Use System.Decimal precision; exact comparison
if (totalDebits != totalCredits)
{
    return TypedResults.BadRequest(new ProblemDetails
    {
        Detail = "imbalanced",
        Extensions = {
            ["debits"]  = totalDebits,
            ["credits"] = totalCredits,
            ["difference"] = Math.Abs(totalDebits - totalCredits)
        }
    });
}
```

### 5.3 Account-id-in-chart validation

```csharp
foreach (var line in request.Lines)
{
    var accountExists = await journals.AccountExistsInChartAsync(
        chartId.Value, line.AccountCode, ct);
    if (!accountExists)
    {
        return TypedResults.BadRequest(new ProblemDetails {
            Detail = "account_not_found",
            Extensions = { ["account_code"] = line.AccountCode }
        });
    }
}
```

### 5.4 Posting date in open period

```csharp
var periodStatus = await journals.GetPeriodStatusAsync(
    chartId.Value, parsedPostingDate, ct);
if (periodStatus == PeriodStatus.Closed)
{
    return TypedResults.UnprocessableEntity();
}
```

### 5.5 Direction enum

Direction is "Debit" or "Credit"; rejected otherwise via early-validation on the DTO:

```csharp
if (request.Lines.Any(l => l.Direction != "Debit" && l.Direction != "Credit"))
{
    return TypedResults.BadRequest(new ProblemDetails { Detail = "invalid_direction" });
}
```

---

## 6. Error code mapping (E1-E6)

| Code | HTTP | Detail | Trigger |
|---|---|---|---|
| **E1** csrf_invalid | 400 | `csrf_invalid` | Antiforgery token missing OR validation throws |
| **E2** chart_not_found | 400 | `chart_not_found` | ChartCode doesn't resolve to a tenant chart |
| **E3** account_not_found | 400 | `account_not_found` | Line.AccountCode not in chart |
| **E4** server_error | 500 | `internal` | Persistence failure; downstream service error |
| **E5** idempotency_conflict | 409 | `idempotency_conflict` | Same Idempotency-Key + tenant + different body |
| **E6** imbalanced | 400 | `imbalanced` | Sum(debits) != Sum(credits) |

Plus:
- **E7** forbidden — 403; not in AccountantPolicy role
- **E8** minimum_two_lines — 400; Lines.Count < 2
- **E9** closed_period — 422; posting_date in closed period
- **E10** invalid_direction — 400; Direction not Debit/Credit

---

## 7. Test scaffolding

### 7.1 Required tests (Engineer + sec-eng SPOT-CHECK acceptance)

```csharp
public sealed class JournalEntriesEndpointsTests : IClassFixture<JournalEntriesIntegrationFixture>
{
    // Happy path
    [Fact] public Task RecordJournalEntry_ValidRequest_Returns201Created() { /* */ }

    // Idempotency (pattern-012 Q2)
    [Fact] public Task RecordJournalEntry_DuplicateKey_SameBody_Returns200Idempotent() { /* */ }
    [Fact] public Task RecordJournalEntry_DuplicateKey_DifferentBody_Returns409Conflict() { /* */ }

    // Cross-tenant (per V4 #4 audit + Adversarial Brief Decision 1)
    [Fact] public Task RecordJournalEntry_TenantIdInBody_Returns400_EmitsTBV() { /* */ }
    [Fact] public Task RecordJournalEntry_ChartFromOtherTenant_Returns400() { /* */ }

    // CSRF (pattern-012 Q1)
    [Fact] public Task RecordJournalEntry_MissingAntiforgery_Returns400() { /* */ }
    [Fact] public Task RecordJournalEntry_InvalidAntiforgery_Returns400() { /* */ }

    // Validation
    [Fact] public Task RecordJournalEntry_ImbalancedLines_Returns400() { /* */ }
    [Fact] public Task RecordJournalEntry_UnknownAccountCode_Returns400() { /* */ }
    [Fact] public Task RecordJournalEntry_OnlyOneLine_Returns400() { /* */ }
    [Fact] public Task RecordJournalEntry_InvalidDirection_Returns400() { /* */ }
    [Fact] public Task RecordJournalEntry_ClosedPeriod_Returns422() { /* */ }

    // Audit emission
    [Fact] public Task RecordJournalEntry_SuccessfulPost_EmitsJournalEntryPostedAudit() { /* */ }
    [Fact] public Task RecordJournalEntry_AuditPayload_Includes5FieldCanonicalShape() { /* */ }

    // Role-based
    [Fact] public Task RecordJournalEntry_NonAccountantRole_Returns403() { /* */ }
}
```

**Total: 14 integration tests** at PR acceptance.

### 7.2 Cross-tenant fixture pattern

```csharp
public sealed class JournalEntriesIntegrationFixture : IAsyncLifetime
{
    public TenantId TenantA = new("tenant-a-known");
    public TenantId TenantB = new("tenant-b-known");
    public ChartOfAccountsId ChartA = new("chart-A");
    public ChartOfAccountsId ChartB = new("chart-B");
    public ChartOfAccountsCode ChartCodeA = new("PRIMARY");
    public ChartOfAccountsCode ChartCodeB = new("PRIMARY");

    public async Task InitializeAsync()
    {
        // Seed two tenants with distinct charts
        // Both call their chart "PRIMARY" (tenant-scoped uniqueness per V2 #5 multi-chart Q2)
    }

    public async Task<TResponse> CallAsTenantAsync<TResponse>(
        TenantId tenant,
        string chartCode,
        RecordJournalEntryRequest body)
    {
        // Set ITenantContext to the given tenant
        // POST /api/v1/financial/journal-entries with antiforgery token + body
        // Return parsed response
    }
}
```

### 7.3 Idempotency test pattern

```csharp
[Fact]
public async Task RecordJournalEntry_DuplicateKey_SameBody_Returns200Idempotent()
{
    var key = $"test-{Guid.NewGuid()}";
    var body = ValidJournalEntryBody();

    var result1 = await CallAsTenantAsync<RecordJournalEntryResponse>(
        TenantA, ChartCodeA.Value, body, idempotencyKey: key);
    var result2 = await CallAsTenantAsync<RecordJournalEntryResponse>(
        TenantA, ChartCodeA.Value, body, idempotencyKey: key);

    Assert.Equal(201, result1.StatusCode);
    Assert.Equal(200, result2.StatusCode);                            // idempotent replay
    Assert.Equal(result1.Body.Id, result2.Body.Id);                    // same response
    Assert.True(result2.Body.IdempotencyReplay);                       // hint flag set
}
```

---

## 8. Sec-eng SPOT-CHECK acceptance criteria

Sec-eng SPOT-CHECK on Engineer PR opening MANDATORY (per pattern-012 ratification candidate + write-path + audit emission + CSRF):

1. ✅ Tenant_id rejection (Adversarial Brief Decision 7 + V4 #6 cross-cutting concern)
2. ✅ Idempotency-Key uniqueness scope = (key, tenant_id) (V3 #2 Q2)
3. ✅ Audit emission canonical 5-field payload + tenant-scoped TenantId on AuditRecord
4. ✅ CSRF antiforgery validated BEFORE any other validation (fail-fast)
5. ✅ Cross-tenant chart probe rejection (chart belongs to different tenant → 400)
6. ✅ DR/CR balance check uses exact decimal (no float; no epsilon-based comparison)
7. ✅ Antiforgery token is in request body (inlined per Q1) not separate header — verify
8. ✅ Account-code validation rejects cross-tenant account in tenant's chart
9. ✅ Period status check rejects closed-period postings

---

## 9. Pattern-012 ratification

After clean shipping + sec-eng + .NET-architect SPOT-CHECKs GREEN, pattern-012-financial-write-path ratifies to formal via Admiral catalog-promotion PR.

This is the 3rd-instance ratification trigger per V2 #4 (1st instance: cohort-2 PR 3 RentCollection POST; 2nd instance: TBD; this would be the 3rd).

PR commit subject:
```
feat(signal-bridge,blocks-financial-ledger): pattern-012 journal-entry POST (W#60 P4 PR 2; 3rd instance ratification candidate)
```

PR description includes:
```
@candidate-pattern: pattern-012-financial-write-path (3rd instance ratification candidate)
@deviation-from-spec: CSRF inlined (vs cohort-2 PR 3 separated header form; pattern-012 ratification adopts INLINED per V3 #2 Q1 ONR recommendation; CIC ratifies form change at promotion)
```

---

## 10. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **IIdempotencyKeyStore package boundary** — `foundation-idempotency` NEW package (V3 #2 recommendation) vs extension of `kernel-audit` OR `foundation-persistence`? ONR recommends NEW dedicated package.
2. **MapToServiceLine adapter** — internal-class anonymous lambda vs explicit `LineMapper.ToService(...)` static helper? Engineer's call.

### For security-engineering council

1. **CSRF inlined vs separated form deviation** — V3 #2 ONR recommended INLINED; cohort-2 PR 3 used SEPARATED. Pattern-012 ratifies at this PR — sec-eng confirms which form is canonical.
2. **Audit payload memo truncation length** — 200 chars (ONR recommended) vs 100 chars vs 500 chars? Memo is user-supplied; PII risk.
3. **`AuditEventType.JournalEntryPosted` constant** — exists or new addition (NEW recommended; mirror `WorkOrderCreated`)?

### For CIC

1. **Pattern-012 form ratification** — INLINED vs SEPARATED at this PR's promotion.
2. **Period close-status semantics** — UnprocessableEntity (422; ONR recommended) vs BadRequest (400)?

---

## 11. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #4
2. V3 #2 W#60 P4 PR 2 package-design (shipyard#77) — endpoint contract + Q1/Q2/Q3 answers
3. V2 #4 pattern-010 → pattern-012 renumber + 3rd-instance design (shipyard#72)
4. V5 #2 pattern catalog hygiene PR (shipyard#88) — pattern-012 candidate entry
5. V5 #5 ADR 0091 Steps 5+6 (shipyard#91) — substrate cleanup context
6. V4 #4 pattern catalog audit (shipyard#85) — Adversarial Brief cross-reference
7. ADR 0091 R2 (Accepted) — server-side tenant derivation
8. ADR 0092 (Accepted) — substrate tenant-keyed repository contract
9. Cohort-2 PR 0d IJournalStore tenant-keyed (shipyard#64)
10. Cohort-2 PR 3 RentCollection POST — 1st pattern-010/012 instance precedent

---

## 12. What ONR does next

V5 #4 deliverable complete. Files `onr-status-*-v5-item-4-w60-p4-pr-2-impl-spec-complete.md`. Proceeds to V5 #1 (cohort-5 + cohort-6 surveys; PRIMARY 1-day work; 2 distinct PRs).

— ONR, 2026-05-21T14:42Z
