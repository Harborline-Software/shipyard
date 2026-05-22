# ADR 0094 Step 2+ — scoping research

**Authored by:** ONR (V13 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-22T19-05Z` item V13 #2)
**Authored at:** 2026-05-22T19-40Z

---

## TL;DR

shipyard#100 (MERGED) implements ADR 0094 Steps 1-5 (in-memory). The **next
step** is `EventLogBackedAuditEventReader` — production implementation that
layers over the kernel `IEventLog` substrate. ONR estimates **~3-4 days**
Engineer time including projection store design + tests.

After Step 6 ships, **Step 7+ is post-MVP**:
- CountAsync surface (for total-count UI)
- IssuedBy filter (security-review surface)
- Free-text payload search (compliance-search surface)
- Cross-tenant super-admin query
- ADR 0094 amendments TBD per each forward-watch maturity

---

## 1. Current substrate state

### 1.1 Shipped (shipyard#100 MERGED 2026-05-21)

Per `shipyard/packages/kernel-audit/`:
- `IAuditEventReader` interface (in `IAuditEventReader.cs`)
- `InMemoryAuditEventReader` (production-quality in-memory; suitable for tests + dev)
- `AuditEventReaderQuery`, `AuditEventPage`, `AuditEventCursor` records
- DI extension `AddSunfishKernelAuditReaderInMemory()`
- 22 tests passing (8 + 8 new + FW1/FW2)

### 1.2 Punted to follow-on (per shipyard#100 §"Forward-watch items")

> "EventLogBackedAuditEventReader punted to follow-on PR: EventLog-backed
> cursor/pagination over IEventLog.ReplayAsync is substantial substrate work
> outside this PR's scope."

(Note: IEventLog uses `ReadAfterAsync` + `ReadRangeAsync`, not `ReplayAsync` —
likely an Engineer-side naming nit.)

---

## 2. Step 6 — `EventLogBackedAuditEventReader`

### 2.1 Core complexity

The challenge: IEventLog is **sequence-ordered append-only**, but
IAuditEventReader requires **reverse-chronological (OccurredAt DESC, AuditId DESC)
tenant-scoped paginated retrieval**.

Three implementation strategies:

#### Strategy A — Scan-and-filter (naive)

```csharp
public async Task<AuditEventPage> ListAsync(TenantId tenantId, AuditEventReaderQuery query, CancellationToken ct)
{
    var allEntries = new List<LogEntry>();
    await foreach (var entry in _eventLog.ReadAfterAsync(0, ct))
        if (entry.Event.Kind == "audit") allEntries.Add(entry);

    var records = allEntries
        .Select(e => DeserializeAuditRecord(e.Event))
        .Where(r => r.TenantId.Equals(tenantId))
        .Where(r => MatchQuery(r, query))
        .OrderByDescending(r => r.OccurredAt)
        .ThenByDescending(r => r.AuditId)
        .Take(query.PageSize)
        .ToList();

    return new AuditEventPage(records, /* cursor logic */);
}
```

**Cost:** O(N_total) per ListAsync call (every audit event in log; scanned for
every page request).
**Verdict:** ACCEPTABLE for MVP (small audit volume); UNACCEPTABLE at production
scale (millions of events; 7-year retention).

#### Strategy B — Projection store with tenant + time secondary index

Maintain a separate projection store (e.g., another IEventLog with different
sequence semantics, OR a SQLite-backed snapshot, OR an in-memory list):

```csharp
public class EventLogBackedAuditEventReader : IAuditEventReader
{
    private readonly IEventLog _eventLog;
    private readonly AuditProjectionStore _projection;  // <-- NEW substrate
    // ...

    public async Task<AuditEventPage> ListAsync(...)
    {
        // Query projection store (already tenant-indexed + OccurredAt-indexed)
        return await _projection.QueryAsync(tenantId, query, ct);
    }
}
```

**Cost:** O(log N) read; O(1) write (projection update on append).
**Verdict:** Right shape for production. Requires NEW substrate
(`AuditProjectionStore` interface + InMemory impl + future EF Core / SQLite impl).

#### Strategy C — Snapshot-based (per IEventLog snapshot mechanism)

Use IEventLog's built-in snapshot mechanism (per IEventLog.WriteSnapshotAsync /
ReadLatestSnapshotAsync) to maintain a tenant-keyed materialized view.

```csharp
public async Task<AuditEventPage> ListAsync(...)
{
    var snapshot = await _eventLog.ReadLatestSnapshotAsync(
        aggregateId: $"audit-tenant-{tenantId}",
        epochId: "current",
        schemaVersion: "1.0", ct);
    // Deserialize snapshot + return paginated subset
}
```

**Cost:** snapshot-write cost on every audit append (or batched); read O(N) over
snapshot.
**Verdict:** Aligns with IEventLog paper §8 semantics; less new substrate to
introduce. Trade-off: snapshot maintenance overhead.

#### ONR recommendation: Strategy B

Rationale:
- Most flexible (can swap projection backends — InMemory → EF Core → external)
- Aligns with cohort-3 reports cluster's projection pattern (cartridge-backed
  read models per PAO #116)
- Doesn't compete with IEventLog snapshot semantics for unrelated use cases
- Easier to test in isolation

### 2.2 Step 6 LOC + dependencies

| Component | Estimated LOC | Notes |
|---|---|---|
| `AuditProjectionStore.cs` interface | ~50 | NEW substrate primitive |
| `InMemoryAuditProjectionStore.cs` | ~120 | Reference impl |
| `EventLogBackedAuditEventReader.cs` | ~180 | Main impl |
| Update DI extension (`AddSunfishKernelAuditReader`) | ~30 | EventLog-backed variant |
| `AuditAppendedEventHandler.cs` (projection updater) | ~80 | Subscribes to IEventLog appends; updates projection |
| Tests: projection + reader integration | ~250 | 12-15 new tests |
| **Total** | **~710** | |

**Effort estimate:** ~3-4 days Engineer time (substrate authoring + tests + DI
+ cross-cluster integration).

### 2.3 Dependencies

- **IEventLog** (already exists in `packages/kernel-event-bus/`)
- **IAuditTrail** (already exists; write-side substrate per ADR 0049)
- **IOperationSigner** (cursor signing at Bridge layer per V11 #2; substrate
  remains agnostic)

### 2.4 Tests required

| # | Test | Closes |
|---|---|---|
| 1 | `GetByIdAsync_FromEventLogProjection_ReturnsRecord` | baseline |
| 2 | `GetByIdAsync_CrossTenantFromEventLog_ReturnsNull_EmitsTBV` | uniform-404 + emission |
| 3 | `ListAsync_ProjectionStoreReverseChronOrder_TenantScoped` | ordering |
| 4 | `ListAsync_EventLogReplaysOnStartup_RebuildsProjection` | recovery |
| 5 | `ListAsync_NewAuditAppendUpdatesProjection_LiveData` | append-driven update |
| 6 | `ListAsync_CursorWalksAcrossProjectionWindow` | pagination |
| 7 | `ListAsync_FilterByEventType_MatchesExactType` | filter |
| 8 | `ListAsync_DateRangeFilter` | filter |
| 9 | `ListAsync_CorrelationIdFilter` | filter |
| 10 | `StreamAsync_FromProjection_StreamsAllMatching` | streaming |
| 11 | `StreamAsync_Lazy_DoesNotMaterializePages` | lazy invariant |
| 12 | `ProjectionStore_StartupBackfill_FromExistingLog` | recovery |
| 13 | `ProjectionStore_ConcurrentAppend_NoLostUpdates` | concurrency |
| 14 | `AuditAppendedEventHandler_OnlyHandlesAuditEvents` | event filter |
| 15 | `EventLogBackedAuditEventReader_TenantBoundaryViolation_EmitsViaWriteSideIAuditTrail` | recursion safety |

---

## 3. Step 7+ (post-MVP forward-watches)

### 3.1 CountAsync surface

Per ADR 0094 §"Pagination posture": "When/if total-count surfaces emerge, a
future ADR amendment adds a `CountAsync` method to the reader."

**Trigger:** UI surfaces wanting "Showing N of M" / "Total: M" (e.g., audit-trail
viewer summary header per cohort-4 retro).

**Effort:** ~1 day (single method + tests + projection-store extension).

### 3.2 IssuedBy filter (security-review surface)

Per ADR 0094 §"Filter API design": "deferred to a future security-review surface
ADR."

**Trigger:** Security review use case (e.g., "all actions by user X across all
tenants for support escalation").

**Effort:** ~2-3 days (new ADR + amendment + reader extension + projection
re-indexing if needed).

### 3.3 Multi-event-type OR

Per ADR 0094: "caller composes multiple `ListAsync` calls or uses the future
security-review surface."

**Trigger:** UI surfaces showing combined Security + Financial events (e.g.,
audit timeline merge).

**Effort:** ~1-2 days (query DTO extension + projection-store extension).

### 3.4 Free-text payload search

Per ADR 0094: "explicitly NOT in scope; payload is opaque dictionary per
AuditPayload. A future compliance-search ADR may add structured payload-index
support."

**Trigger:** Compliance team requirement for searching within audit payloads.

**Effort:** ~5-7 days (new ADR + payload structured-indexing substrate +
projection-store rebuild + tests). Significant scope; post-MVP-1.0.

### 3.5 Cross-tenant audit query (super-admin)

Per ADR 0094: "deferred to a future super-admin surface ADR (C2 in V2 #6)."

**Trigger:** Multi-tenant federation work activation; super-admin support /
forensics use cases.

**Effort:** ~3-5 days (new ADR + reader surface + auth-policy + projection
multi-tenant-aware).

---

## 4. Step ordering recommendation

Per V13 #2 finding + V7 #3 MVP critical-path + V9 #1 cohort-4 sequence:

1. **Step 6 — EventLogBackedAuditEventReader** (this V13 #2 scope; ~3-4 days)
   - Engineer fires when cohort-4 Bridge endpoint family is ready to ship
2. **Step 7 — CountAsync** (post-MVP-1.0; UI-driven trigger)
3. **Step 8 — IssuedBy filter** (post-MVP-1.0; security-review trigger)
4. **Step 9 — Multi-event-type OR** (post-MVP-1.0; UI-driven trigger)
5. **Step 10 — Cross-tenant super-admin** (post-multi-tenant-federation)
6. **Step 11+ — Free-text payload search** (compliance-driven; far future)

---

## 5. Cohort-4 sequencing impact

Per V9 #1 cohort-4 critical path + V13 #4 finding:

```
Step 1-5 (shipyard#100; in-memory) [MERGED 2026-05-21]
       │
       ▼
signal-bridge audit-events Bridge endpoint family (cohort-4 PR 0; uses InMemoryAuditEventReader)
       │
       ├─→ cohort-4 demo path (cohort-4 ships with in-memory backing)
       │
       └─→ Step 6 EventLogBackedAuditEventReader (production swap; ~3-4 days)
                                                  (CAN ship in parallel; not gating cohort-4 demo)
```

**Key insight:** Step 6 is **not gating** cohort-4 demo. The in-memory
implementation is acceptable for demo + first paying tenant. Step 6 fires
when production scale demands (10k+ audit events; >1 tenant).

---

## 6. Risk + mitigation

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Step 6 substrate complexity overruns 4-day estimate | MEDIUM | MEDIUM (delays production swap) | Engineer files question early if AuditProjectionStore design hits scope blockers |
| AuditProjectionStore design conflicts with cohort-3 cartridge projection pattern | LOW | MEDIUM | ONR cross-references PAO #116 cartridge spec before Step 6 authoring |
| EventLog replay-on-startup performance degrades at scale | MEDIUM | LOW (acceptable cold-start) | Snapshot strategy per Strategy C; future amendment |
| Step 6 + projection backfill conflicts with concurrent audit appends | LOW | LOW (eventual-consistency window) | Test 13 covers concurrent append; documented in PR description |
| Step 6 cursor signing layer choice diverges from V11 #2 recommendation | LOW | LOW (refactor cost ~30 min) | ONR forward-watches at Step 6 PR Ready-flip |

---

## 7. Pattern conformance forward-watches

When Step 6 ships, pattern emergence:

- **pattern-tenant-scoped-list-with-bridge-aggregation** (V13 #1 candidate) —
  Step 6 would be 5th-instance shipping; 2nd-instance-confirmed at minimum
- **pattern-canonical-audit-payload-shape** (V10 #2 + V11 #1 candidate) —
  Step 6 continues 5-field canonical
- **pattern-eventlog-projection-store** (NEW candidate; emerges at Step 6) —
  AuditProjectionStore is first instance; cohort-3 cartridge-projection may be
  2nd if it follows same shape

---

## 8. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **Step 6 trigger** — fires when?
   - Option A: Immediately post-cohort-4 demo (Engineer's call)
   - Option B: When first production tenant signs up (post-MVP)
   - Option C: When audit volume exceeds 10k records (data-driven)
   ONR recommends Option A (parallel with cohort-4 demo polish; reduces risk
   of post-launch substrate scrambling).

2. **AuditProjectionStore design** — Strategy A (scan-and-filter; naive) vs
   Strategy B (projection store; recommended) vs Strategy C (snapshot-based)?
   .NET-architect council ratification at Step 6 PR Ready-flip.

3. **Step 7+ post-MVP triggers** — explicitly schedule (e.g., 30 days post-MVP)
   OR fire on demand (UI signal / security-review request)?

4. **pattern-eventlog-projection-store** — register as candidate now (forward-
   watch), OR wait until 2nd-instance emerges (cohort-3 cartridge projection
   maturity)?

---

## 9. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T19-05Z` item V13 #2
2. shipyard#100 (kernel-audit Step 1-5; MERGED 2026-05-21)
3. `shipyard/packages/kernel-audit/IAuditEventReader.cs`
4. `shipyard/packages/kernel-event-bus/IEventLog.cs` — read-side methods
   `ReadAfterAsync` + `ReadRangeAsync` + snapshot ops
5. `shipyard/packages/kernel-audit/EventLogBackedAuditTrail.cs` — write-side precedent
6. ADR 0094 (Accepted 2026-05-21)
7. ADR 0049 (audit substrate write-side; IAuditTrail)
8. ADR 0092 §A3 + §A6 (uniform-404 + canonical payload)
9. V9 #1 cohort-4 FED PR-by-PR specs (shipyard#119)
10. V10 #1 Engineer substrate ladder (shipyard#121) — Step 6 in PR #5 forward-watch
11. V11 #2 ADR 0094 Step 1 consultation (shipyard#127) — Step 2+ forward-watch
12. V12 #3 Engineer V3 #1 supplement (shipyard#131) — supersedence note
13. V13 #4 Engineer V3 #1 progress tracking (shipyard#133) — shipyard#100 already-shipped finding

---

## 10. What ONR does next

V13 #2 scoping complete. Proceeds to V13 #3 (Cohort-4 critical-path timing
analysis; ~1-2h).

— ONR, 2026-05-22T19:40Z
