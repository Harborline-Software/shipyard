# Cohort-3 PR cluster — consolidated cross-cutting spec

**Authored by:** ONR (V10 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-22T16-20Z` item V10 #3)
**Authored at:** 2026-05-22T17-05Z
**Workstream:** W#77 cohort-3 blocks-reports cluster (4 cartridge-backed reports)

---

## Purpose

Cohort-3 (reports cluster) has three primary inputs that need consolidation:

1. **PAO design direction** (shipyard#116; 11 deliverables / ~3,150 lines) — Track C
2. **FED pre-flight survey** (FED V26 #2; NOT YET FILED) — implementation feasibility
3. **Engineer Bridge endpoint dependency** (NOT YET OPEN) — `/api/v1/reports/{kind}` endpoints

ONR ties these together, identifies contract-frozen-beacon gaps, and surfaces
sequencing decisions to Admiral.

---

## 1. Inputs state (2026-05-22T17:05Z)

### 1.1 PAO #116 (Track C design direction) — OPEN

State per `gh pr view 116`:
- 11 deliverables in `shipyard/_shared/design/cohort-3/`
- 4 page docs (TrialBalance, ArAging, ProfitAndLossByProperty, RentRoll)
- 7 cross-cutting docs (INDEX, tokens, 3 patterns, component-reuse-audit, states-matrix)
- 3 new candidate patterns proposed (with naming collision per V7 #6 + V9 #3 Admiral
  ruling — must renumber to 015/016/017):
  - `pattern-011-provisional-report-surface` → **pattern-015** (collides with existing pattern-011 event publisher)
  - `pattern-012-run-on-demand-report` → **pattern-016** (collides with pattern-012 financial-write-path candidate per V8 #5/V10 #1)
  - `pattern-013-csv-export-affordance` → **pattern-017** (collides with pattern-013 cartridge-read-via-POST candidate per V8 #5/V10 #1)
- Renumber amendment pending PAO push (per `pao-status-2026-05-22T13-05Z-resumption-ack.md` per Admiral V9 Q1 ruling)

### 1.2 FED V26 #2 pre-flight survey — NOT FILED

FED's pre-flight typically:
- Reads PAO design direction
- Identifies implementation-blocking contract gaps
- Asks Engineer for contract-frozen beacons
- Surfaces UX seams that need Stage-05 resolution

ONR cannot consolidate until FED files. Per Admiral V10 directive, FED V26 #2
fires when triggered.

### 1.3 Engineer Bridge endpoint dependency — NOT OPEN

Per PAO #116 description, Engineer PR 0 must produce:
- `/api/v1/reports/{kind}` Bridge endpoints (4 kinds: RentRoll, ProfitAndLossByProperty, TrialBalance, ArAgingSummary)
- ChartId JSON serialization contract
- Chart-list endpoint
- CSV export endpoint (Accept-header vs `/export` route TBD)

No Engineer PR currently OPEN for this; not in V10 #1 Engineer ladder scope
(V10 #1 covers ADR 0091 Steps 3+4, ADR 0094 Step 1, signal-bridge audit-events,
foundation-idempotency — NOT reports cluster Bridge endpoints).

---

## 2. Contract-frozen-beacon gaps (cross-input dependency graph)

### 2.1 ChartId JSON serialization

**Question:** how does `ChartId` (presumably a typed identifier in shipyard
substrate) serialize over the wire?

**PAO direction:** uses chart-id as opaque string in URL paths and request bodies.
**FED need:** TypeScript type definition + JSON shape (string vs object with discriminator vs nested).
**Engineer status:** needs contract-frozen beacon.

**Recommendation:** Engineer files `engineer-contract-frozen-2026-MM-DD-chart-id-serialization.md` with:
- C# type definition
- JSON serialization format (string literal vs object)
- TypeScript .d.ts equivalent
- Test fixture showing wire shape

### 2.2 Chart-list endpoint shape

**Question:** what does `GET /api/v1/reports/charts` (or similar list endpoint) return?

**PAO direction:** ChartSelector component (per pattern-011/015 spec) lists
available charts per report kind; needs list endpoint response shape.
**FED need:** TypeScript type for chart list response.
**Engineer status:** needs contract-frozen beacon.

**Recommendation:** Engineer files `engineer-contract-frozen-2026-MM-DD-chart-list-endpoint.md` with:
- Endpoint path (`GET /api/v1/reports/{kind}/charts`?)
- Response DTO shape (id, title, description, ordering?)
- Pagination posture (likely no — chart list is small)
- Test fixture showing wire shape

### 2.3 CSV export endpoint convention

**Question:** does CSV export use Accept-header (`Accept: text/csv` on the same endpoint) OR a separate `/export` route?

**PAO direction:** "Export CSV" button per pattern-013/017 spec; doesn't pin convention.
**FED need:** Implementation pattern — fetch logic differs significantly between Accept-header negotiation vs URL-routed export.
**Engineer status:** needs contract-frozen beacon.

**Recommendation:** Engineer chooses + files `engineer-contract-frozen-2026-MM-DD-csv-export-convention.md`. ONR opinion: separate route (`GET /api/v1/reports/{kind}/export?format=csv`) is cleaner — supports future format additions (xlsx, pdf) without Accept-header overload.

**Forward-watch:** This decision precedents future export endpoints (audit-trail CSV per cohort-4 hand-off §4.5; financial reports; etc.). Pin pattern-013/017 finalization to this decision.

### 2.4 W#77 CIC pre-auth ratification

**Question:** has W#77 received CIC pre-authorization for cohort-3 (per
fleet-conventions §pre-auth requirements)?

**Current state:** unknown — Admiral's W#77 workstream tracker not surveyed by ONR.

**Recommendation:** Admiral confirms W#77 pre-auth status. If absent, file directive
or surface for CIC ratification before Engineer PR 0 opens.

### 2.5 Engineer PR 0 — `/api/v1/reports/{kind}` Bridge endpoints

**Question:** what's the endpoint shape for the 4 report kinds?

**Cohort-3 hand-off (shipyard#51 MERGED):** likely has Stage-05 spec; ONR
references but doesn't re-derive here.

**Recommendation:** Engineer authors PR 0 per cohort-3 hand-off; ONR forward-watches
contract-frozen beacon at PR-open time.

---

## 3. Sequencing chain (consolidated)

```
W#77 CIC pre-auth ratification           [§2.4; Admiral / CIC]
       │
       ▼
Engineer contract-frozen beacons:        [§2.1, §2.2, §2.3; Engineer]
  - ChartId serialization
  - Chart-list endpoint shape
  - CSV export convention
       │
       ├─→ FED V26 #2 pre-flight survey [§1.2; FED]
       │       │
       │       ▼
       │   FED PR 1 (cohort-3 PR 1)     [shared infrastructure;
       │                                  5 new shared + 2 promotions]
       │
       └─→ Engineer PR 0                 [§2.5; Bridge /reports/{kind}]
                │
                ▼
            FED PRs 2-5                  [4 pages: RentRoll, P&L, TrialBalance, ArAging]
                │
                ▼
            Pattern-015/016/017 ratification trigger
            (cohort-4 must pick up all 3 patterns consistently)
       │
       ▼
PAO #116 renumber amendment              [pending PAO push per V9 #3 Admiral ruling]
       │
       ▼
PAO #116 MERGE                            [unblocks Pattern-015/016/017 candidate registration]
       │
       ▼
QM V5 #1 catalog cleanup                  [pattern-014 promotion + pattern-013 add + pattern-004 cleanup]
       │
       ▼
ONR V9 #3 pattern catalog snapshot        [fires per Admiral V9 ruling triggers]
```

---

## 4. Cross-cutting risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| PAO #116 renumber amendment doesn't ship → cohort-3 patterns collide with existing 011/012/013 | MEDIUM | HIGH (catalog drift cascades) | PAO heartbeat verifies push; Admiral surfaces if PAO blocked |
| Engineer contract-frozen beacons unauthored → FED can't proceed | HIGH (current state) | MEDIUM (blocks 4-page rebind cluster) | Admiral routes Engineer Q on contract-frozen scope |
| CSV export convention decision precedents future endpoints; chosen poorly causes future-PR debt | LOW | MEDIUM | ONR recommendation: separate `/export?format=csv` route; .NET-architect ratification |
| Pattern-015/016/017 collide with V8 #5/V10 #1 cohort-4 patterns (e.g., V10 #1 forward-watches `pattern-tenant-id-signed-opaque-cursor` as next candidate; might get #015 if added before cohort-3) | LOW | LOW (re-renumber cheap) | Admiral pins next-available number explicitly when new candidates emerge |
| W#77 CIC pre-auth missing → Engineer PR 0 stalls | LOW | HIGH (cohort-3 blocked) | Admiral confirms pre-auth status; surfaces to CIC if absent |
| FED V26 #2 pre-flight finds blocking UX seam → cohort-3 hand-off needs amendment | LOW | MEDIUM (~3-5 days iteration) | Stage-05 protocol per ADR 0093 catches at design time |
| FED splits PRs differently than PAO PR 1-5 plan → diff inflates / shrinks | MEDIUM | LOW | Pre-flight survey reconciles; not blocking |

---

## 5. Pattern numbering reconciliation

Per V7 #6 + V9 #3 Admiral rulings + V10 #1 forward-watches:

**Currently allocated formal patterns** (per V7 #6 snapshot):
- pattern-001 to pattern-006 (cohort-1 + cohort-2 era)
- pattern-009 (Bridge endpoint + frontend rebind)
- pattern-009-tenant-keying-retrofit (formalized 2026-05-22 shipyard#103)

**Currently allocated candidate patterns:**
- pattern-010 (formerly financial-write-path; renumbered to pattern-012 per V3 #2)
- pattern-011 (event publisher; existing)
- pattern-012 (financial-write-path; candidate; V8 #5 + V10 #1 #5 forward-watch)
- pattern-013 (cartridge-read-via-POST; candidate; V5 #2 #88 → superseded by #103)
- pattern-014 (referenced but unverified per V7 #6 drift; QM V5 #1 to verify)

**Cohort-3 + V10 forward-watches need allocations:**
- **pattern-015** (provisional report surface) — PAO #116 cohort-3 pattern-011 renumbered
- **pattern-016** (run-on-demand report) — PAO #116 cohort-3 pattern-012 renumbered
- **pattern-017** (csv export affordance) — PAO #116 cohort-3 pattern-013 renumbered
- **pattern-tenant-id-signed-opaque-cursor** (V10 #1 #3 emergence) — likely pattern-018
- **pattern-uniform-404-cross-tenant** (V10 #1 #3 + #4 emergence) — likely pattern-019
- **pattern-defense-in-depth-tenant-assert-client-side** (V9 #1 emergence) — likely pattern-020
- **pattern-severity-event-prefix-coloring** (V9 #1 emergence) — likely pattern-021
- **pattern-canonical-audit-payload-shape** (V10 #2 emergence) — likely pattern-022

**Recommendation:** Admiral pins pattern numbering when each emergence reaches
2nd-instance threshold. ONR forward-watches via V8 #6 retrospective scaffold.

---

## 6. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **PAO #116 renumber amendment urgency** — block PAO from other work until pushed?
   ONR observation: pattern-015/016/017 cannot be ratified until cohort-3 cluster
   ships; renumber affects PR description only.
2. **Engineer contract-frozen beacon scope** — Engineer authors 3 beacons (ChartId,
   chart-list, CSV export)? Or fold into Engineer PR 0 PR description?
   ONR recommends: 3 standalone beacons (cleaner reviewability + reusability for
   future contract questions).
3. **CSV export convention** — separate `/export?format=csv` route (ONR recommended)
   vs Accept-header? Requires .NET-architect ratification.
4. **W#77 CIC pre-auth status** — confirm. If absent, Admiral surfaces.
5. **Engineer queue order** — V10 #1 Engineer ladder (5 PRs) currently spec'd.
   Where does cohort-3 PR 0 (`/api/v1/reports/{kind}`) fit? Before? After? Parallel?
   ONR observation: cohort-3 PR 0 doesn't depend on V10 #1 PRs; can run parallel.
6. **Pattern numbering for emerging candidates** — pin pattern-018+ allocations
   now, OR defer until 2nd-instance threshold?

---

## 7. What FED V26 #2 pre-flight should cover (recommendations)

When FED files V26 #2 pre-flight, ONR recommends covering:

- TypeScript type imports from PAO design direction (e.g., `ChartId`, `ReportRunResult`, etc.)
- TanStack hook architecture per pattern-016 (run-on-demand semantics — no auto-fetch)
- Filter state management (per pattern-016 — filter change resets result)
- ProvisionalityBanner component reuse across 4 pages
- CSV export download UX (browser-side blob vs server-side response)
- Pattern-015/016/017 component canonical signatures (per PAO design direction)
- Cohort-3 PR 1 shared-infrastructure scope (5 new + 2 promotions per PAO)
- A11y posture for ProvisionalityBanner (live-region announcement?)
- States-matrix consumption from PAO docs

---

## 8. Cohort-3 PR cluster ladder (forecast)

When all inputs land, expected cohort-3 PR ladder:

| PR | Owner | Scope | Estimated LOC | Gate |
|---|---|---|---|---|
| Engineer PR 0 | Engineer | `/api/v1/reports/{kind}` 4 endpoints + ChartId + chart-list + CSV export | ~1,000-1,500 | W#77 pre-auth |
| FED PR 1 | FED | Shared infrastructure (5 new + 2 promoted components) | ~600-900 | PAO #116 merged |
| FED PR 2 | FED | RentRollPage rewrite | ~500-700 | Engineer PR 0 |
| FED PR 3 | FED | ProfitAndLossByPropertyPage rewrite | ~600-800 | Engineer PR 0 |
| FED PR 4 | FED | TrialBalancePage new | ~500-700 | Engineer PR 0 |
| FED PR 5 | FED | ArAgingPage new | ~500-700 | Engineer PR 0 |

**Cumulative cohort-3 PR cluster:** 6 PRs, ~3,700-5,300 LOC, ~3-4 week timeline parallel.

---

## 9. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T16-20Z` item V10 #3
2. PAO shipyard#116 (cohort-3 Track C design direction; OPEN; 3,150 lines / 11 deliverables)
3. `admiral-ruling-2026-05-22T15-25Z` — PAO #116 renumber amendment ruling
4. `pao-status-2026-05-22T13-05Z-resumption-ack.md` — PAO ack
5. `admiral-directive-2026-05-20T02-15Z-pao-cohort-3-design-direction-track-c.md` — original PAO directive
6. V7 #6 pattern catalog snapshot (shipyard#108) — pattern numbering baseline
7. V9 #3 admiral ruling on pattern catalog snapshot triggers
8. V10 #1 Engineer substrate ladder PR-by-PR specs (shipyard#121)
9. V10 #2 audit-payload canonicalization research (shipyard#122)
10. cohort-3 Stage-06 hand-off (shipyard `icm/_state/handoffs/anchor-react-rebind-cohort-3-stage06-handoff.md`)
11. fleet-conventions §SPOT-CHECK dispatch SLA + §pre-auth requirements

---

## 10. What ONR does next

V10 #3 deliverable complete. V10 batch state:
- V10 #1 PRIMARY done (shipyard#121; Engineer ladder)
- V10 #2 done (shipyard#122; audit-payload canonical)
- V10 #3 done (this doc)
- V10 forward-watch: V9 #2 + V9 #3 fire when conditionals trigger

ONR files V10 complete idle beacon. 3 of 3 V10 active items shipped; 2
forward-watch items remain conditional. Awaits V11 dispatch.

— ONR, 2026-05-22T17:05Z
