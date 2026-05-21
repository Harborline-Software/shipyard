# ONR research — V2 council question routing strategy (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T12-45Z` item #6)
**Authored by:** ONR
**Authored at:** 2026-05-21T12-42Z
**Status:** draft (Admiral routes per recommendations)

---

## Scope of investigation

- **In scope:** triage the 42 open questions from V2 PRs (#68-#75) into LOAD-BEARING (decision changes if council answers differently — dispatch now) vs INFORMATIONAL (council insight welcomed but doesn't gate downstream decisions — defer to natural Stage-06 SPOT-CHECK consumption). Recommend Top 5 to dispatch first.
- **Out of scope:** authoring the dispatch beacons themselves (Admiral routes); council reviewer assignment.
- **Authoritative sources consulted:** V2 PRs #68 (8 Q), #69 (7 Q), #71 (8 Q), #72 (8 Q), #73 (7 Q), #74 (4 Q), #75 (7 Q) — open questions sections inline.
- **Success looks like:** Admiral has a prioritized triage list; cognitive load reduced from "42 questions across 7 PRs" to "5 load-bearing dispatches + 37 informational deferrals."

---

## TL;DR — Top 5 council dispatches to fire first

| # | PR | Audience | Question | Why load-bearing |
|---|---|---|---|---|
| **1** | #68 ADR 0091 Steps 3+4 | .NET-architect | Step 3.0 framing (canonical test migration only vs directive broader consumption sweep)? | Determines Step 3 PR scope; Engineer can't open Step 3 PR without this |
| **2** | #69 ADR 0092 Step 2.0 EFCore | sec-eng | `IgnoreQueryFilters` analyzer severity — Warning pre-Step-4b vs Error? | Determines pre-Step-4b enforcement model; affects every cluster's Step 2 PR pattern |
| **3** | #72 Pattern-010 3rd-instance | .NET-architect | Pattern naming conflict resolution (`pattern-010` docs-toc-entry already used; renumber to 011)? | Catalog hygiene; affects W#60 P4 PR 2 + cohort-4 anchor PR commit messages |
| **4** | #71 Audit-emission retrofit | .NET-architect | `BridgeAuditEmitter` helper vs per-handler-inline emission? | Determines retrofit PR shape (1 PR with helper vs 3 PRs with inline) |
| **5** | #72 Pattern-010 3rd-instance | sec-eng | Q1 CSRF — INLINED (ONR-recommended for pattern-010) vs SEPARATED (cohort-2 PR 3 precedent)? | Pattern-010 ratification form depends on this |

**Dispatch Top 5 immediately; defer remaining 37 to natural Stage-06 SPOT-CHECK consumption.**

---

## 1. Per-PR triage

### 1.1 shipyard#68 — ADR 0091 Steps 3+4 (8 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Step 3.0 framing | .NET-architect | **LOAD-BEARING #1** | Engineer can't scope Step 3 PR without this |
| Analyzer DI mixing Error vs Warning | .NET-architect | INFORMATIONAL | Engineer can ship analyzer with default Error; sec-eng SPOT-CHECK refines later |
| `[Obsolete]` Warning vs Error | .NET-architect | INFORMATIONAL | ONR recommendation (Warning) is consistent with grace-window intent; default is fine |
| Runtime propagation verification | sec-eng | INFORMATIONAL | ONR recommendation (analyzer + IHostedService) is defense-in-depth; default is fine |
| Analyzer test coverage minimum | sec-eng | INFORMATIONAL | Engineer can ship with 6 tests per ONR pseudo-code; sec-eng can request more at SPOT-CHECK |
| IBrowserTenantContext direct-use detection | sec-eng | INFORMATIONAL | Out-of-Step-4 scope per ONR; future analyzer extension |
| Step 5 facade deletion "one cohort" definition | CIC | INFORMATIONAL | Defer until Step 4 ships; CIC ratifies at Step 5 planning |

### 1.2 shipyard#69 — ADR 0092 Step 2.0 EFCore (7 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Ship WhereTenant as separate PR vs bundled | .NET-architect | INFORMATIONAL | Engineer's call; separate-PR ONR-recommended is reasonable default |
| Hybrid strategy confirm/amend | .NET-architect | INFORMATIONAL | ONR's hybrid recommendation matches ADR 0092 §"Step 2" + the `WithoutQueryFilters` enforcement |
| HasQueryFilter on ALL IMustHaveTenant entities | .NET-architect | INFORMATIONAL | ONR-recommended; cluster engineers can apply uniformly |
| `IgnoreQueryFilters` analyzer Warning vs Error | sec-eng | **LOAD-BEARING #2** | Determines pre-Step-4b enforcement; affects every cluster Step 2 PR |
| FW1 performance audit sequencing | sec-eng | INFORMATIONAL | ONR-recommended (parallel follow-on); Engineer can ship Step 2 PRs without it |
| `FromSqlRaw` false-positive handling | sec-eng | INFORMATIONAL | Engineer can ship with `warn + suppress` default; narrow-detection is future analyzer work |
| FW1 audit: separate workstream vs Engineer task | CIC | INFORMATIONAL | Defer to V3+ batches |

### 1.3 shipyard#71 — Audit-emission Bridge retrofit (8 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Helper class vs per-handler-inline | .NET-architect | **LOAD-BEARING #4** | Determines retrofit PR shape; 1 PR vs 3 PRs |
| Scoped DI lifetime | .NET-architect | INFORMATIONAL | ONR-recommended; matches IAuditTrail + IOperationSigner |
| Single PR vs three small | .NET-architect | INFORMATIONAL | Engineer's call per tracking beacon |
| `actual_tenant` redaction vs verbatim | sec-eng | INFORMATIONAL | ONR-recommended verbatim; tenant-scoped audit logs make leakage moot |
| Emission ordering BEFORE vs AFTER 404 | sec-eng | INFORMATIONAL | ONR-recommended BEFORE; standard pattern |
| `AuditEventType.TenantBoundaryViolation` constant existence | sec-eng | INFORMATIONAL | Engineer verifies at pre-flight; mechanical |
| Correlation ID source | sec-eng | INFORMATIONAL | ONR-recommended `Activity.Current?.Id`; W3C standard |
| Rate-limit on probe-flood audit | CIC | INFORMATIONAL | Future workstream; not blocking retrofit |

### 1.4 shipyard#72 — Pattern-010 3rd-instance (8 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Candidate selection (A vs B vs C) | .NET-architect | INFORMATIONAL | ONR's Candidate A (JournalEntry POST) clearly wins on scoring matrix; council can ratify or defer |
| Pattern naming conflict resolution | .NET-architect | **LOAD-BEARING #3** | Catalog hygiene; W#60 P4 PR 2 + cohort-4 anchor commit messages depend on resolved name |
| Idempotency-Key uniqueness scope | .NET-architect | INFORMATIONAL | ONR-recommended per-tenant; standard pattern |
| **Q1 CSRF inlined vs separated** | sec-eng | **LOAD-BEARING #5** | Pattern-010 ratification form; affects W#60 P4 PR 2 implementation |
| Q2 Idempotency-Key MANDATORY | sec-eng | INFORMATIONAL | ONR-recommended; industry-standard for financial writes |
| Q3 RELOAD vs RETRY on 409 | sec-eng | INFORMATIONAL | ONR-recommended RELOAD; UX-driven |
| Audit emission on idempotent duplicate | sec-eng | INFORMATIONAL | ONR-recommended YES; marginal cost; forensics value |
| Cohort-4 vs W#60 P4 PR 2 ratification timing | CIC | INFORMATIONAL | ONR-recommended converge; either order works |

### 1.5 shipyard#73 — Multi-chart-per-tenant (7 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Rename `GetChartIdAsync` to `GetDefaultChartIdAsync` | .NET-architect | INFORMATIONAL | Naming polish; Engineer applies during Phase 1 implementation |
| `ChartSummary.Code` uniqueness per-tenant vs global | .NET-architect | INFORMATIONAL | ONR-recommended per-tenant; standard tenant-isolated naming |
| `ChartContextMiddleware` placement | .NET-architect | INFORMATIONAL | Global middleware (ONR-recommended) is simpler; default fine |
| Chart-not-found 403 vs 404 | sec-eng | INFORMATIONAL | ONR-recommended 403; standard pattern; sec-eng can refine if needed |
| Audit emission on chart-not-found | sec-eng | INFORMATIONAL | ONR-recommended YES; mirrors V2 #3 retrofit |
| Cross-chart query isolation HasQueryFilter | sec-eng | INFORMATIONAL | ONR-recommended; mirrors tenant isolation pattern |
| Multi-chart shipping trigger | CIC | INFORMATIONAL | ONR-recommended demand-driven; CIC awaits customer signal |

**Verdict:** ALL informational; multi-chart is demand-driven. No load-bearing dispatch needed.

### 1.6 shipyard#74 — Cohort-4 scope survey (4 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Cohort-4 anchor C3 vs C7 vs C1 | CIC | INFORMATIONAL → addressed via V3 #1 | ONR's recommendation (C3) is the V3 #1 deliverable; CIC ratifies via PR review |
| AP Aging slip cohort-4 vs separate cohort | CIC | INFORMATIONAL | Defer until cohort-4 anchor lands |
| Pattern-010 ratification path convergence | CIC | INFORMATIONAL | ONR-recommended converge; W#60 P4 PR 2 + cohort-4 |
| PAO Track C cohort-4 design routing | Admiral | INFORMATIONAL | Operational; Admiral dispatches when anchor ratified |
| Audit search performance EXPLAIN-plan | .NET-architect | INFORMATIONAL | Engineer's V3 #1 cohort-4 hand-off acceptance criteria |
| Cohort-4 PR-count cap | Admiral | INFORMATIONAL | Determined by anchor choice (V3 #1) |

### 1.7 shipyard#75 — WS-E Phase 10+ addendum (7 questions)

| Q | Audience | Type | Reason |
|---|---|---|---|
| Delivery semantics default at-least-once vs at-most-once | .NET-architect | INFORMATIONAL | ONR-recommended at-least-once; standard business-messaging default |
| DLQ persistence same DB as audit | .NET-architect | INFORMATIONAL | ONR-recommended same DB; ADR 0049 7-year retention applies |
| OpenTelemetry vs Prometheus-native | .NET-architect | INFORMATIONAL | ONR-recommended OTel; standard observability stack |
| ExactlyOnce idempotency-key uniqueness scope | sec-eng | INFORMATIONAL | ONR-recommended per-tenant; standard |
| DLQ replay operator-only vs caller-replay | sec-eng | INFORMATIONAL | ONR-recommended operator-only; lower attack surface |
| Tracing tenant_id span attribute | sec-eng | INFORMATIONAL | ONR-recommended emit; tenant-isolated trace collectors mitigate leakage |
| Phase 10+ sequencing — sequential vs parallel | CIC | INFORMATIONAL | ONR-recommended sequential; not blocking until Phase 4-9 ship |

**Verdict:** ALL informational; Phase 10+ is forward-watch; no dispatch needed until Phase 4-9 land.

---

## 2. Routing recommendation summary

### Dispatch immediately (Top 5; load-bearing)

1. **`.NET-architect council` dispatch — Step 3.0 framing (shipyard#68 §6 Q1)**
   - Question: canonical (test migration only) vs directive (broader consumption sweep)?
   - Why now: Engineer's Step 3 PR scope depends on the answer
   - Recommended dispatch beacon: `admiral-council-dispatch-*-net-architect-adr-0091-step-3-0-framing.md`

2. **`security-engineering council` dispatch — IgnoreQueryFilters severity (shipyard#69 §6 Q1)**
   - Question: Warning pre-Step-4b vs Error?
   - Why now: affects every cluster's Step 2 PR enforcement pattern
   - Recommended dispatch beacon: `admiral-council-dispatch-*-sec-eng-adr-0092-step-2-0-ignore-query-filters-severity.md`

3. **`.NET-architect council` dispatch — Pattern naming resolution (shipyard#72 §4 Q2)**
   - Question: pattern-010-financial-write-path → pattern-011-financial-write-path renumber?
   - Why now: catalog hygiene; W#60 P4 PR 2 + cohort-4 anchor PR titles depend on it
   - Recommended dispatch beacon: `admiral-council-dispatch-*-net-architect-pattern-010-renumber.md`
   - **NOTE:** This may be Admiral-self-attestable (catalog hygiene; not architectural). Admiral's call.

4. **`.NET-architect council` dispatch — Helper vs inline (shipyard#71 §6 Q1)**
   - Question: BridgeAuditEmitter helper class vs per-handler-inline?
   - Why now: determines retrofit PR shape (1 vs 3)
   - Recommended dispatch beacon: `admiral-council-dispatch-*-net-architect-bridge-audit-emitter-helper.md`

5. **`security-engineering council` dispatch — Pattern-010 Q1 CSRF (shipyard#72 §4 Q1)**
   - Question: INLINED (ONR-recommended for pattern-010) vs SEPARATED (cohort-2 PR 3 precedent)?
   - Why now: pattern-010 ratification form; W#60 P4 PR 2 implementation depends on it
   - Recommended dispatch beacon: `admiral-council-dispatch-*-sec-eng-pattern-010-csrf-inlined-vs-separated.md`

### Defer to Stage-06 SPOT-CHECK consumption (informational; 37 questions)

All other questions follow ONR's provisional recommendations. Councils consume the PRs at natural SPOT-CHECK timing (when Engineer opens the implementation PR that consumes the research):

- Engineer Step 2.0 PR (ADR 0091) → sec-eng SPOT-CHECK pulls remaining shipyard#56 + #68 questions
- Engineer Step 2 EFCore PRs (ADR 0092 per cluster) → sec-eng SPOT-CHECK pulls remaining shipyard#69 questions
- Engineer audit retrofit PR → sec-eng SPOT-CHECK pulls remaining shipyard#71 questions
- Engineer W#60 P4 PR 2 (JournalEntry POST) → sec-eng SPOT-CHECK pulls remaining shipyard#72 questions + cohort-4 PR 0
- Engineer multi-chart Phase 1 (if activated) → sec-eng SPOT-CHECK pulls shipyard#73 questions
- FED cohort-4 PR(s) → sec-eng SPOT-CHECK pulls remaining shipyard#74 questions
- Engineer Phase 10+ PRs (if Phase 4-9 land) → sec-eng SPOT-CHECK pulls shipyard#75 questions

### CIC ratification questions (5 questions; defer to natural cohort sequencing)

- Cohort-4 anchor selection (#74) → V3 #1 PR is the dispatch (CIC reviews + ratifies)
- AP Aging timing (#74) → cohort-4 anchor's PR review
- Pattern-010 ratification convergence (#72, #74) → W#60 P4 PR 2 + cohort-4 anchor co-ratify
- Multi-chart shipping trigger (#73) → wait for first customer signal
- Step 5 facade deletion timing (#68) → defer to Step 4 ship

---

## 3. Admiral cognitive-load reduction

**Before triage:** 42 questions across 7 PRs requiring Admiral attention.

**After triage:** 5 council dispatches required immediately + 37 questions deferred to natural Stage-06 SPOT-CHECK consumption.

**Effort to dispatch Top 5:** ~30 min Admiral time (5 dispatch beacons; each 1 paragraph referencing the source PR).

**Effort saved:** ~3-4h Admiral time NOT spent processing 37 informational questions individually.

---

## 4. Sources cited

1. shipyard#68 — ADR 0091 Steps 3+4 pre-research (8 questions in §6)
2. shipyard#69 — ADR 0092 Step 2.0 EFCore (7 questions in §6)
3. shipyard#71 — Audit-emission Bridge retrofit (8 questions in §6)
4. shipyard#72 — Pattern-010 3rd-instance (8 questions in §4)
5. shipyard#73 — Multi-chart-per-tenant (7 questions in §5)
6. shipyard#74 — Cohort-4 scope survey (4 questions in §6)
7. shipyard#75 — WS-E Phase 10+ addendum (7 questions in §7)
8. `admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #6 — parent directive

---

## 5. What ONR does next

Returns to V3 queue. Per proceed-continuously discipline:

- Item #6 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v3-item-6-council-routing-strategy-complete.md`.
- Proceed to V3 #2: Pattern-010 → 011 renumber + W#60 P4 PR 2 design.

— ONR, 2026-05-21T12:42Z
