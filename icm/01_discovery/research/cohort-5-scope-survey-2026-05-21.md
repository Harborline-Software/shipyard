# Cohort-5 scope survey (2026-05-21)

**Authored by:** ONR (V5 batch item #1a)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #1)
**Authored at:** 2026-05-21T14-52Z

---

## Scope

Cohort-3 reports cluster + cohort-4 audit-trail viewer are Stage-06-ready. What's cohort-5? Per V2 #6 + V5 #6 + V5 #7 candidate scoring matrices, ONR recommends cohort-5 anchor.

---

## TL;DR

1. **ONR recommends cohort-5 anchor = C1 ARR/MRR reporting wave** (highest investor-grade value; 9/12 candidate score).
2. **PR cluster shape (proposed):** Engineer prereq PR 0 (`blocks-subscriptions` accumulator extension) + 3 FED PRs (ARR Dashboard + MRR Detail + Cohort Retention Chart) + close-out PR 4 = 5 PRs.
3. **Effort estimate:** ~12-20h total (~6-8h Engineer + ~6-12h FED).
4. **Substrate gap:** `blocks-subscriptions` accumulator (cumulative MRR tracking over time-windowed periods). Not currently shipped per V2 #6 assessment.
5. **Pattern claims:** pattern-009 formal (Bridge endpoint + frontend rebind pair) + potential candidate-pattern-014 (event-accumulator-aggregation if a new pattern emerges from the accumulator design).
6. **Sequencing dependency on cohort-4:** none functional; cohort-5 independent of audit-trail viewer.
7. **MVP-demo unblock value: HIGH** — ARR/MRR is the canonical investor-meeting financial metric. Sunfish goes from "property mgmt ERP" to "investor-grade SaaS-like dashboard."

---

## 1. Cohort-5 anchor scoring (per V5 #6 candidate matrix)

| Candidate | Substrate readiness | Effort | MVP-demo value | Dependencies | Score |
|---|---|---|---|---|---|
| **C1 — ARR/MRR reporting (ONR recommended)** | Partial — `blocks-subscriptions` accumulator gap | 12-20h | **High (investor)** | None | **9/12** |
| C7 — AP Aging page (cohort-3 deferred) | `ApAgingSummaryCartridge` (~3-4h Engineer) | 5-7h | Medium (closes reports) | Engineer cartridge ship | 8/12 |
| C5 — Mobile-first UX PWA | Pure FED + PAO | 10-15h | Medium (customer-touch) | PAO Track C design | 7/12 |
| C2 — Multi-tenant admin | Production OIDC needed | 15-25h | Medium (operator-facing) | ADR for super-admin + production OIDC ADR (V1 #5 future) | 6/12 |
| C4 — ERPNext migration | Partial (`IErpnextJournalEntryImporter` shipped) | 25-40h | Low (demand-driven) | Customer signal | 5/12 |
| C6 — Real-time collaboration | Substrate missing | 25-40h | Low (polish) | New ADR | 4/12 |

---

## 2. ARR/MRR reporting wave — proposed cohort-5 anchor

### 2.1 Substrate dependencies

**Required (Engineer prereq):**
- `blocks-subscriptions` accumulator extension: time-windowed cumulative MRR tracking
- `IRecurringRevenueAccumulator` interface (new): per-tenant period-bucketed MRR aggregation
- Bridge endpoint family `GET /api/v1/financial/recurring-revenue/{summary,detail,cohort-retention}`

**Already shipped:**
- `blocks-subscriptions` package (per V2 #6 survey reference)
- W#72 reports cluster substrate (provides patterns to follow)
- `IChartCatalogService` + multi-chart-per-tenant primitives (for chart-filtered ARR if multi-chart activates per V3 #5 gate)

### 2.2 Engineer scope (~6-8h)

| Subject | Effort |
|---|---|
| Extend `blocks-subscriptions` with `IRecurringRevenueAccumulator` | 2-3h |
| MRR accumulator implementation (InMemory + tests) | 2-3h |
| Bridge endpoint family at `signal-bridge/Sunfish.Bridge/Financial/` | 2-3h |

### 2.3 FED scope (~6-12h)

| PR | Subject | Effort |
|---|---|---|
| PR 1 | `sunfish/apps/web/src/api/recurring-revenue.ts` + `ArrDashboardPage.tsx` (executive-summary tile: ARR / MRR / Growth Rate / Churn) | 2-4h |
| PR 2 | `MrrDetailPage.tsx` (month-over-month breakdown; expansion / contraction / churn / new) | 2-4h |
| PR 3 | `CohortRetentionPage.tsx` (cohort heat-map of retention curves; canonical SaaS metric) | 2-4h |

### 2.4 Total cohort-5 effort

~12-20h Engineer + FED. ~5 PRs across the cluster.

### 2.5 Pattern claims

- `@standing-pattern: pattern-009` (formal; cluster-endpoint rebind pair)
- Potential new candidate: `pattern-014-event-accumulator-aggregation` (per V2 #6 forward-watch — Bridge POST endpoint exposing time-bucketed accumulator queries; first instance: MRR accumulator)

### 2.6 Sec-eng + .NET-architect council requirements

- Engineer prereq PR 0: sec-eng SPOT-CHECK MANDATORY (new Bridge endpoint family + tenant-scoping verification per substrate retrofit pattern-009-tenant-keying-retrofit)
- FED PR 1-3: NOT required (mechanical pattern-009 mirror)

---

## 3. Sequencing within cohort-5

```
Engineer PR 0 (accumulator + Bridge endpoint) →
FED PR 1 (ArrDashboardPage) →
FED PR 2 (MrrDetailPage) →
FED PR 3 (CohortRetentionPage) →
FED PR 4 (close-out: ERPNext deprecation marks if any + smoke + docs + W#79 ledger flip)
```

Sequential within review-load smoothing; technically PR 1+2+3 can ship in parallel once Engineer PR 0 contract-frozen.

---

## 4. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `blocks-subscriptions` accumulator scope creep (more than basic MRR) | Medium | Medium (PR 0 delay) | ONR recommends MVP scope: MRR only; ARR derived as 12× MRR; expansion/churn/new-business per-cohort breakdown |
| Time-windowed aggregation performance on populated DBs | Medium | Medium (slow dashboard) | Cache + memoize via Bridge response cache (similar to W#72 reports cartridge cache) |
| Cross-chart MRR (if multi-chart per V3 #5 ratifies first) | Low | Medium (UX complexity) | Multi-chart is demand-driven; if not activated, single-chart MRR is the MVP |
| Investor demos may not need cohort retention chart (deeper SaaS metric) | Low | Low (PR 3 optional) | Stage PR 3 as optional within cohort-5; ship PR 1 + PR 2 as MVP-minimum |

---

## 5. Open questions for Admiral routing

1. **Cohort-5 anchor confirm/amend** — ONR recommends C1 ARR/MRR; alternatives: C7 AP Aging (8/12) OR C5 Mobile-first PWA (7/12)
2. **`blocks-subscriptions` accumulator scope** — MVP (MRR only; ONR recommended) vs full (MRR + expansion + churn + new-business breakdown)?
3. **`pattern-014-event-accumulator-aggregation` candidate** — propose at cohort-5 PR 0 OR defer to next emerging instance?
4. **Cohort retention chart PR 3 — within cohort-5 (ONR recommended) vs deferred to cohort-7+?** Investor demos likely don't need it for MVP.

---

## 6. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #1
2. V2 #6 cohort-4 scope survey (shipyard#74) — C1 ARR/MRR candidate framing
3. V5 #6 ERPNext migration scoping (shipyard#93) — candidate matrix scoring template
4. V5 #7 mobile-first UX (shipyard#94) — alternative candidate context
5. W#72 blocks-reports cluster — substrate pattern precedent
6. ADR 0049 (audit substrate) + ADR 0091 R2 (tenant context) — substrate consumption baselines

---

## 7. What ONR does next

V5 #1a (cohort-5 survey) deliverable complete. Files V5 #1b (cohort-6 survey) as separate PR per worktree convention.

— ONR, 2026-05-21T14:52Z
