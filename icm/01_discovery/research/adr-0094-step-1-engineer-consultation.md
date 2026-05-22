# ADR 0094 Step 1 — Engineer consultation

**Authored by:** ONR (V11 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-22T17-15Z` item V11 #2)
**Authored at:** 2026-05-22T17-55Z
**Target audience:** Engineer V3 #1 IAuditEventReader package authoring

---

## 1. Purpose

Engineer V3 #1 is authoring `shipyard/packages/kernel-audit/` IAuditEventReader
substrate per ADR 0094 (Accepted). This consultation:

1. Cross-checks ADR 0094 Accepted text against ONR's V9 #1 + V10 #1 specs
2. Surfaces spec divergences NOT captured in cohort-4 hand-off + V10 #1
3. Identifies forward-watch trigger conditions for follow-on PRs
4. Provides Engineer pre-flight verification checklist

---

## 2. ADR 0094 Step 1 contract — canonical

Per ADR 0094 §"Contract surface (Step 1)" (Accepted at shipyard#104 line 261):

```csharp
namespace Sunfish.Kernel.Audit;

public interface IAuditEventReader
{
    Task<AuditRecord?> GetByIdAsync(
        TenantId tenantId,
        Guid auditId,
        CancellationToken ct = default);

    Task<AuditEventPage> ListAsync(
        TenantId tenantId,
        AuditEventQuery query,
        CancellationToken ct = default);

    IAsyncEnumerable<AuditRecord> StreamAsync(
        TenantId tenantId,
        AuditEventQuery query,
        CancellationToken ct = default);
}
```

### 2.1 Key invariants (ADR 0094 ratified)

1. **TenantId as FIRST positional parameter** — explicit, not DI-injected through
   `ITenantContext` resolution at substrate boundary. Caller (Bridge handler)
   sources the value: `var tenantId = new TenantId(tenantContext.TenantId);`
2. **Uniform-empty cross-tenant** — `GetByIdAsync` returns null; `ListAsync`
   returns empty page; `StreamAsync` returns empty enumerable. NO discriminating
   error.
3. **Audit emission on `GetByIdAsync` cross-tenant** — emits
   `AuditEventType.TenantBoundaryViolation` via the write-side `IAuditTrail`
   (not self-emit; avoids recursion). 5-field canonical payload per V10 #2.
4. **`ListAsync` + `StreamAsync` do NOT emit per-result** — filter at query
   boundary; ADR 0092 §A6 carves out list-time per-row emission.
5. **Cursor opaque + signed** — cursor carries tenant-id signature; Bridge layer
   signs + verifies (cohort-4 hand-off Decision 2 + Decision 5 mitigations).
6. **Page size 1..200; default 50.**

---

## 3. Spec divergence: V10 #1 vs ADR 0094 (ONR self-correction)

### 3.1 Divergence — TenantId parameter shape

**V10 #1 Engineer ladder spec (shipyard#121 §4.2) authored signature:**
```csharp
public interface IAuditEventReader
{
    Task<AuditEventListResult> GetByTenantAsync(
        AuditEventFilters filters,
        AuditEventCursor? cursor,
        int pageSize,
        CancellationToken cancellationToken);
    // ... no explicit TenantId param; assumed DI-injected via ITenantContext
}
```

**ADR 0094 canonical:**
```csharp
Task<AuditEventPage> ListAsync(
    TenantId tenantId,           // EXPLICIT FIRST PARAMETER
    AuditEventQuery query,
    CancellationToken ct = default);
```

**ONR self-correction:** V10 #1 spec is **incorrect**. ADR 0094 mandates explicit
`TenantId` as first positional parameter (consistent with ADR 0092's
"EXPLICIT-first-positional-parameter" norm for tenant-keyed substrate).

**Engineer guidance:** **follow ADR 0094, not V10 #1**. V10 #1 needs amendment;
ONR will file a V12+ correction PR to shipyard#121 once V11 batch completes.

### 3.2 Divergence — Method naming

V10 #1 used `GetByTenantAsync` + `GetByIdAsync`; ADR 0094 uses `ListAsync` +
`GetByIdAsync` + `StreamAsync`.

**Engineer guidance:** **use ADR 0094 names** (`ListAsync` not `GetByTenantAsync`;
also add `StreamAsync` per Step 1 contract).

### 3.3 Divergence — Filter shape

V10 #1 used `AuditEventFilters` record with `Severity` field; ADR 0094 uses
`AuditEventQuery` with EventType + From + To + CorrelationId + PageSize +
Cursor.

**Note:** ADR 0094 does NOT include a `Severity` filter; V9 #1's A2 severity
filter is a Bridge-layer concern (handler can prefix-match on EventType server-side
without needing a separate substrate parameter). V10 #1 conflated UI-level
"severity" with substrate-level filter; should be split.

**Engineer guidance:**
- Substrate `AuditEventQuery`: matches ADR 0094 (no Severity field)
- Bridge handler: maps optional `?severity=Security` query-string param to
  `EventType` prefix-match (e.g., `EventType` = "Security.%" wildcard, OR
  multiple parallel `ListAsync` calls — Bridge handler implementation choice)
- This isolates substrate from UI-evolution

### 3.4 Convergence — Cursor opacity + signing

V10 #1 and ADR 0094 both specify opaque base64-encoded cursor signed by
`IOperationSigner` with embedded tenant-id signature. Convergence confirmed.

### 3.5 Convergence — Cross-tenant uniform-404 + audit emission

V10 #1 and ADR 0094 both specify uniform-null on cross-tenant + emission on
`GetByIdAsync` cross-tenant. V10 #1 used `IAuditTrail.AppendAsync`; ADR 0094
specifies write-side `IAuditTrail` (no self-emit). Convergence confirmed.

---

## 4. Engineer V3 #1 pre-flight checklist

Before opening the kernel-audit package PR, verify:

- [ ] Package path: `shipyard/packages/kernel-audit/` (per V10 #1 §4 location)
- [ ] Project file: `Sunfish.Kernel.Audit.csproj` (per ADR 0094 ref impl naming)
- [ ] Namespace: `Sunfish.Kernel.Audit`
- [ ] DI lifetime: Scoped (per-request injection; tenant context implicitly bound)
- [ ] Implementation: `InMemoryAuditEventReader` (in-memory; for tests + dev)
  + future `EventLogBackedAuditEventReader` (production; follows ADR 0049
  `EventLogBackedAuditTrail` pattern)
- [ ] AuditEventQuery type matches ADR 0094 §"Filter API design" exactly
  (EventType, From, To, CorrelationId, PageSize, Cursor)
- [ ] Cursor: opaque `string`; encoding scheme = base64(URL-safe) per ADR 0094
- [ ] Cursor TTL: NOT specified in ADR 0094 (post-MVP forward-watch)
- [ ] No `Severity` field on AuditEventQuery (Bridge maps separately per §3.3)
- [ ] Test coverage: 12+ tests per V10 #1 §4.5 + ADR 0094 §"Implementation checklist"

---

## 5. Spec ambiguities (Engineer raises if encountered)

### 5.1 AuditEventPage shape

ADR 0094 references `AuditEventPage` but doesn't pin the shape. ONR's read:

```csharp
public sealed record AuditEventPage(
    IReadOnlyList<AuditRecord> Records,
    string? NextCursor);
```

Or potentially:
```csharp
public sealed record AuditEventPage(
    IReadOnlyList<AuditRecord> Records,
    string? NextCursor,
    int RemainingEstimate = -1);  // -1 indicates "unknown"
```

**Engineer guidance:** start with the 2-field shape; add `RemainingEstimate`
only if a UI need surfaces. Document the choice in PR description.

### 5.2 StreamAsync semantics

ADR 0094 names `StreamAsync` but doesn't specify whether streaming is:
- Eager (server pre-fetches all matching records)
- Lazy (server fetches in batches; `IAsyncEnumerable` yields per-batch)

**Engineer guidance:** lazy streaming (`IAsyncEnumerable<AuditRecord>` that
hydrates batches under-the-hood). Aligns with ADR 0094 §"Performance posture
(lazy vs eager hydration)" recommendation.

### 5.3 ITenantContext dependency

ADR 0094 says callers pass `TenantId` explicitly; substrate doesn't inject
`ITenantContext`. But the audit-emission on cross-tenant probe needs
`correlation_id` (per V10 #2 + V11 #1 canonical 5-field):
- correlation_id comes from `Activity.Current?.Id ?? Guid.NewGuid().ToString("N")`
- NOT from `ITenantContext` directly

**Engineer guidance:** correlation_id sourced from `Activity.Current` (not DI);
no `ITenantContext` injection needed in substrate. Bridge layer holds the
context; substrate is context-agnostic.

### 5.4 Operation signer for cursor signing

ADR 0094 mentions cursor signing but doesn't explicitly enumerate DI for
`IOperationSigner` at substrate vs Bridge layer.

**Engineer guidance per V10 #1 + V11 #1:**
- **Substrate signing** (V10 #1 spec): substrate signs cursor on emission;
  decoded on next `ListAsync` call. Singleton-DI for IOperationSigner.
- **Bridge signing** (alternative): Bridge signs/verifies cursor; substrate
  passes opaque blob through.

Per ADR 0094 §"Recursion safety" the cleaner pattern is **Bridge-layer
signing** (substrate stays simpler). ONR forward-watches the Engineer's
implementation choice; document in PR description.

---

## 6. Forward-watch triggers for follow-on PRs

### 6.1 EventLogBackedAuditEventReader (Step 2)

Step 2 of ADR 0094 covers the production implementation backed by `IEventLog`
substrate. Fires when:
- Step 1 in-memory implementation MERGED
- Bridge endpoint (Engineer cohort-4 PR 0) ships with E2E test against in-memory
- Demo data scale outgrows in-memory (50-unit demo per V7 #3)

**Estimated effort:** ~1-2 days; ~300-500 LOC

### 6.2 CSV export endpoint (cohort-4 hand-off §4.5)

Deferred to Engineer PR 1 per cohort-4 hand-off. Fires when:
- Engineer PR 0 (audit-events Bridge endpoint family) MERGED
- ONR forward-watches per V9 #1 §5 sequencing

**Engineer guidance:** CSV export uses `StreamAsync` for memory-efficient
large-result streaming. Per cohort-4 hand-off Decision 6, cap at 10M rows
(or 7-year retention window).

### 6.3 Cross-tenant audit query (super-admin)

ADR 0094 explicitly defers to "future super-admin surface ADR (C2 in V2 #6)".
Fires when:
- Multi-tenant federation work activates (post-MVP)
- Super-admin role needed for support / forensics use cases

**Engineer guidance:** NOT in Step 1 scope. Future ADR amendment will add a
`super-admin-audit-reader` interface or extend `IAuditEventReader` with a
new method.

### 6.4 Total-count surface (CountAsync)

ADR 0094 §"Pagination posture" defers `CountAsync` to a future amendment when
UI needs total-count.

**Engineer guidance:** NOT in Step 1 scope. UI uses cursor pagination without
total count.

### 6.5 IssuedBy filter (security-review surface)

ADR 0094 defers to future ADR.

**Engineer guidance:** NOT in Step 1; callers wanting principal-filter today
fall back to `IAuditTrail.QueryAsync`.

### 6.6 Free-text payload search

ADR 0094 explicitly out-of-scope; future compliance-search ADR.

**Engineer guidance:** NOT in Step 1.

---

## 7. Pattern conformance for Engineer V3 #1 PR

When opening the kernel-audit package PR, Engineer cites:

```markdown
## Pattern claims (PR description; not commit body per fleet-commitlint policy)

@standing-pattern: pattern-009-tenant-keying-retrofit (formal) — TenantId as
EXPLICIT first parameter; uniform-empty cross-tenant; audit-emission on
GetByIdAsync cross-tenant probe.

@candidate-pattern: pattern-canonical-audit-payload-shape (V10 #2 + V11 #1
emergent candidate; 2nd-instance via this PR's 5-field emission contract;
substrate-level enforcement).
```

---

## 8. SPOT-CHECK dispatch matrix (per V10 #1 §4.7 + ADR 0094)

| Council | Mandatory? | Rationale |
|---|---|---|
| **sec-eng-council** | **MANDATORY** | Cross-tenant invariants (Check 1) + audit emission (Check 3) + canonical 5-field payload conformance (per V11 #1) |
| **.NET-architect** | **MANDATORY** | Substrate primitive design + AuditEventQuery shape ratification + interface API stability |
| test-eng-council | RECOMMENDED | New substrate primitive; baseline test coverage |
| frontend-architect | DEFER | No frontend surface |

ONR forward-watches both verdicts against ADR 0094 + V10 #1 + V11 #1.

---

## 9. Engineer V3 #1 PR description acceptance criteria template

```markdown
## Implements ADR 0094 Step 1

- [x] `IAuditEventReader` interface at `Sunfish.Kernel.Audit` namespace
- [x] `GetByIdAsync` with explicit TenantId first parameter; returns null on
  cross-tenant; emits TenantBoundaryViolation via IAuditTrail
- [x] `ListAsync` with explicit TenantId first parameter; cursor-paginated; uniform-empty cross-tenant
- [x] `StreamAsync` with explicit TenantId first parameter; lazy IAsyncEnumerable; uniform-empty cross-tenant
- [x] `AuditEventQuery`: EventType, From, To, CorrelationId, PageSize (1..200; default 50), Cursor
- [x] `AuditEventPage` 2-field shape (Records, NextCursor)
- [x] `InMemoryAuditEventReader` reference impl
- [x] DI registration: Scoped lifetime in `AddSunfishKernelAudit()` extension
- [x] 12+ tests covering uniform-empty + audit emission + cursor + canonical 5-field payload
- [x] Cursor signing via IOperationSigner (Bridge-layer OR substrate-layer; document choice)

## V11 #1 + V10 #2 alignment

- [x] TenantBoundaryViolation 5-field canonical payload (no `observed_tenant`
  non-canonical field; aligns to V10 #2 audit-payload sweep + V11 #1
  pattern-012a/012b framing)

## V11 #2 (this consultation) — guidance applied

- [x] TenantId as EXPLICIT first parameter (NOT V10 #1 §4.2 shape; corrected per ADR 0094)
- [x] No `Severity` field on AuditEventQuery (Bridge maps separately)
- [x] AuditEventPage 2-field shape (no RemainingEstimate; deferred)
- [x] Lazy IAsyncEnumerable for StreamAsync
- [x] correlation_id from Activity.Current?.Id; NOT from ITenantContext
```

---

## 10. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **V10 #1 amendment** — ONR files correction PR to shipyard#121 amending §4
   spec to match ADR 0094? Or supersede via V11 #2 consultation reference?
   ONR recommends: V11 #2 supersedes V10 #1 §4 explicitly; both PRs cite each
   other.
2. **Cursor signing layer** — substrate (V10 #1) vs Bridge (V11 #2 recommendation)?
   .NET-architect council ratification at Engineer V3 #1 SPOT-CHECK.
3. **Severity filter UX** — Bridge-layer EventType prefix-match (V11 #2
   recommendation) vs new substrate parameter? ONR recommends Bridge-layer.
4. **EventLogBackedAuditEventReader trigger** — fires automatically post-Step 1
   merge, OR Admiral routes explicitly? ONR recommends automatic per ADR 0094
   Step 2 sequencing.

---

## 11. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T17-15Z` item V11 #2
2. `shipyard/docs/adrs/0094-i-audit-event-reader.md` (Accepted 2026-05-21)
3. V10 #1 Engineer substrate ladder PR-by-PR specs (shipyard#121) §4 IAuditEventReader spec — to be amended per §3 divergences
4. V10 #2 audit-payload canonicalization research (shipyard#122) — 5-field canonical
5. V11 #1 pattern-012 canonical framing research (shipyard#124) — pattern-canonical-audit-payload-shape candidate
6. V9 #1 cohort-4 FED PR-by-PR detail specs (shipyard#119) §3.2 useAuditEvents hook + A2 severity filter
7. cohort-4 Stage-06 hand-off (shipyard `icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md`)
8. ADR 0091 R2 (ITenantContext) + ADR 0092 R2 (substrate tenant-keyed) + ADR 0046 (IOperationSigner)
9. ADR 0049 (IAuditTrail) — write-side counterpart

---

## 12. What ONR does next

V11 #2 consultation complete. Proceeds to V11 #4 (onboarding-ladder 10
decisions resolution scaffold; ~2-3h).

— ONR, 2026-05-22T17:55Z
