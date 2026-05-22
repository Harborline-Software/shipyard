# Cohort-3 Design Direction — Reports Cluster

**Workstream:** W#77 (blocks-reports cluster — 4 cartridge-backed reports)
**Status:** Track C design direction (PAO authored; Yeoman wireframes)
**Authored:** 2026-05-22
**PAO:** chris (via PAO session 2026-05-22)

This directory holds the design-direction artifacts FED will consume before opening cohort-3 PRs 1–5. It is the **second** cohort design-direction artifact set (cohort-2 set the template; cohort-1 shipped without formal Track C).

Where cohort-2 was about **read-mostly financial reads + one write-path (`pattern-010` candidate)**, cohort-3 is about a **new surface category** — user-triggered, parameterized reports with a provisionality concept that didn't exist before. Three new cross-page patterns get canonized here as a result:

- **`run-on-demand-pattern.md`** — user selects params → clicks Run; report does NOT auto-fetch on mount
- **`provisionality-banner-pattern.md`** — amber banner + collapsible warnings when `ReportRunResult.IsProvisional = true`
- **`csv-export-pattern.md`** — `[Export CSV]` button surface, filename convention, disabled-until-result behavior

## Scope

Four Sunfish React pages get added or rewritten against the new `/api/v1/reports/{kind}` Bridge family:

| PR | Page | Action | Cartridge | Direction doc |
|---|---|---|---|---|
| 2 | `RentRollPage.tsx` | Rewrite of `RentRoll.tsx` | `RentRoll` | [`04-rent-roll-page.md`](./04-rent-roll-page.md) |
| 3 | `ProfitAndLossByPropertyPage.tsx` | Rewrite of `PLReport.tsx` | `ProfitAndLossByProperty` | [`03-profit-loss-by-property-page.md`](./03-profit-loss-by-property-page.md) |
| 4 | `TrialBalancePage.tsx` | New | `TrialBalance` | [`01-trial-balance-page.md`](./01-trial-balance-page.md) |
| 5 | `ArAgingPage.tsx` | New | `ArAgingSummary` | [`02-ar-aging-page.md`](./02-ar-aging-page.md) |

PR 1 is the **shared-infrastructure PR** — see cross-cutting docs below. PRs 2–5 each depend on PR 1 landing on main but can open simultaneously as DRAFTs once it does.

AP Aging is deferred to cohort-4 (cartridge not yet shipped). See Q5 for route-reservation decision.

Plus the cross-cutting deliverables:

- [`tokens.md`](./tokens.md) — design tokens used + new cohort-3 additions (`GLAccountType` palette, `OccupancyStatus` badges, aging-bucket header tints made canonical)
- [`component-reuse-audit.md`](./component-reuse-audit.md) — `@sunfish/ui-react` v0.2 reuse + new shared components needed (`<ProvisionalityBanner>`, `<ExportCsvButton>`, `<ReportFilterBar>`, `<ChartSelector>`, `<AgingBucketPill>`)
- [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md) — canonical UX for `IsProvisional` reports (pattern-015 candidate)
- [`run-on-demand-pattern.md`](./run-on-demand-pattern.md) — canonical UX for user-triggered runs (pattern-016 candidate)
- [`csv-export-pattern.md`](./csv-export-pattern.md) — canonical CSV export UX (pattern-017 candidate)
- [`states-matrix.md`](./states-matrix.md) — empty/loading/success/error variants per page (extended for the IDLE / READY_TO_RUN report state)

## Reference inputs

1. **FED cohort-3 collaboration spec** — `coordination/inbox/fed-status-2026-05-19T2400Z-cohort-3-design-collaboration.md` (711 lines; source-of-truth for component hierarchy, wire types, state machine, "PAO design direction needed" requests)
2. **W#77 blocks-reports substrate** — `shipyard/packages/blocks-reports/` (cartridge runner + 5 Phase 1 cartridges; `ReportRunResult<TResult>` envelope contract)
3. **Cohort-2 baseline** — `shipyard/_shared/design/cohort-2/` (structural template; pattern-010 candidate informs pattern-015/012/013 shape)
4. **`@sunfish/ui-react` v0.2** — just shipped via FED queue item #5 (shipyard#48); primitive set available (`<Card>`, `<Badge>`, `<CurrencyAmount>`, `<AgingBucketPill>`)
5. **Cohort-1 visual baseline** — `sunfish/apps/web/src/pages/MaintenancePage.tsx` (`STATUS_COLORS` convention for badge palettes)
6. **Framework design docs** — `shipyard/_shared/design/{design-language,tokens-guidelines,component-principles,accessibility,internationalization}.md`

## Standing-pattern alignment

- **pattern-009** (Bridge endpoint + frontend rebind pair) — formal post-cohort-1; applies to all 5 cohort-3 PRs.
- **pattern-015-provisional-report-surface** — CANDIDATE; first instance is the `<ProvisionalityBanner>` shared across all 4 cohort-3 pages. Ratifies on second cohort using `IsProvisional` semantics (likely cohort-4 AP Aging or a forward report).
- **pattern-016-run-on-demand-report** — CANDIDATE; first instance is the IDLE → READY_TO_RUN state machine shared across all 4 cohort-3 pages. Ratifies on next user-triggered report (cohort-4 AP Aging).
- **pattern-017-csv-export-affordance** — CANDIDATE; first instance is the `<ExportCsvButton>` shared across all 4 cohort-3 pages. Ratifies on next non-report CSV surface (e.g., a future Lease export, Tenant export).

All three candidates share a single cohort-of-instance — they ratify together if cohort-4 picks them up consistently.

## Surfaced design questions

The following questions came up during Phase A; surfaced to CIC for ratification or to Engineer/FED for clarification before PRs land.

### Q1 — Nav grouping for `/reports/*`

FED notes (spec line 636–638) that cohort-3 adds two new routes to nav (Trial Balance, AR Aging) on top of the existing two (P&L, Rent Roll). All four share the `/reports/` URL prefix.

**Recommendation:** introduce a **`Reports` group header** in the side nav with the 4 children indented below it. This matches the URL surface and helps with the cohort-4 expansion (AP Aging makes 5; subsequent reports keep growing).

**Alternative considered + rejected:** flat nav items at the top level. Rejected because 4+ flat report items dilute the nav and lose the cluster identity; users won't scan past 3 unrelated items at the top.

The actual nav component lives in `apps/web/src/components/SideNav.tsx` (or equivalent). FED implements the group during PR 1 (shared infrastructure scope); PAO ratifies copy here.

**Copy:** group header reads `Reports` (plain noun; no "Financial reports" qualifier — keeps the group expandable to non-financial reports later if needed).

### Q2 — `ChartSelector` behavior when one vs N charts

FED notes (spec line 644) that all 4 pages require `ChartId`. Users will typically have one chart; the multi-chart case is rare but contractually possible.

**Recommendation:** PAO-canonical behavior:
- **0 charts available:** disable Run button; show inline copy "Set up a chart of accounts before running reports" with link to `/settings/chart-of-accounts` (route reserved; not in cohort-3 scope).
- **1 chart available:** auto-select on mount; selector renders as a non-interactive label ("Chart: Operating accounts"); Run button enabled when other required filters are met.
- **N charts available (N > 1):** selector renders as a dropdown; **no default selection** — user must pick; Run button disabled until selection made.

The "auto-select when 1" path is the dominant user experience; the dropdown path supports the multi-property accountant use case without making the common case clunky.

This is design direction, not a halt — confirmed in [`run-on-demand-pattern.md`](./run-on-demand-pattern.md).

### Q3 — RentRoll responsive: hide `Delinquency` column at `<md:`?

FED notes (spec line 589, 604) that `RentRollUnitRow` has 7 columns and `Delinquency` is a candidate to hide on narrow viewports.

**Recommendation:** **do NOT hide `Delinquency` at `<md:`**. Instead:
- Use `overflow-x-auto` on the table container; let users scroll horizontally on narrow screens (Surface Pro portrait is the narrowest target).
- Hiding `Delinquency` hides the cohort-3 differentiation from the legacy RentRoll page (the new delinquency-bucket cell is a deliberate addition); hiding it on small screens makes the cohort-3 upgrade invisible to whatever percentage of users hit it portrait-first.
- Surface Pro landscape (1366×912) accommodates all 7 columns without horizontal scroll; only portrait or narrower viewports need scroll.

Confirmed in [`04-rent-roll-page.md`](./04-rent-roll-page.md).

### Q4 — `ProvisionalityBanner` copy

FED's spec line 261–263 proposes:

> ⚠ This report covers an open accounting period and may change when the period is closed. [Show details ▾]

**Recommendation:** approved with one adjustment — replace `may change` with `may change as transactions are posted`:

> ⚠ This report covers an open accounting period and may change as transactions are posted. [Show details ▾]

The adjustment names the actual mechanism (transactions get posted; the report updates) instead of the abstract "period close" event. Users without an accounting background still understand "transactions" — the original copy required them to know that period-close is what changes the numbers.

Confirmed in [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md).

### Q5 — AP Aging route reservation

AP Aging is deferred to cohort-4. FED suggests reserving the route with a TODO comment.

**Recommendation:** **do NOT reserve** the route in cohort-3. Reasoning:
- A 404 from `/reports/ap-aging` is preferable to a partially-implemented placeholder page that promises a report and delivers nothing. Cohort-1's H5 placeholder banner on LeaseDetailPage payments was an explicitly named regression we corrected in cohort-2; same lesson applies.
- The "Reports" nav group from Q1 should NOT include AP Aging until the cartridge ships. Adding a stub nav item to an unshipped feature is the same anti-pattern.
- When cohort-4 ships the AP Aging cartridge, the route + nav item + page get added in one PR — clean single signal.

The cohort-4 follow-up issue should be filed by FED when this PR lands. PAO will revisit nav grouping ratification when AP Aging arrives.

Confirmed in nav grouping section of [`02-ar-aging-page.md`](./02-ar-aging-page.md) (since AR Aging is the closest sibling).

### Q6 — `GLAccountType` color map

FED requests a palette for the 5 GL account types (Asset / Liability / Equity / Revenue / Expense) for the `TrialBalanceTable` Type column badges.

**Recommendation:**

| Type | Background | Text |
|---|---|---|
| Asset | `bg-blue-100` | `text-blue-700` |
| Liability | `bg-purple-100` | `text-purple-700` |
| Equity | `bg-slate-100` | `text-slate-700` |
| Revenue | `bg-green-100` | `text-green-700` |
| Expense | `bg-amber-100` | `text-amber-800` |

Reasoning: assets-vs-liabilities reads as cool-vs-warm in the accounting tradition (blue = cash-positive; purple = obligations). Equity gets slate (neutral; it's the residual). Revenue inherits the standard green for "money in"; expense uses amber rather than red to keep red exclusive to **error states and overdue/delinquency** — semantic-color reservation matters in a reports surface.

Confirmed in [`tokens.md`](./tokens.md) and [`01-trial-balance-page.md`](./01-trial-balance-page.md).

### Q7 — `OccupancyStatus` badge palette (RentRoll)

FED requests palette for 4 status values; proposed amber for `NoticeGiven`.

**Recommendation:**

| Status | Background | Text | Notes |
|---|---|---|---|
| Occupied | `bg-green-100` | `text-green-700` | matches cohort-1 work-order Completed |
| NoticeGiven | `bg-amber-100` | `text-amber-800` | tooltip exposes VacancyReason on hover |
| Vacant | `bg-gray-100` | `text-gray-700` | inline VacancyReason when present (no tooltip needed) |
| OffMarket | `bg-gray-100` `border border-gray-300` | `text-gray-600` | outlined variant; signals "intentionally not for lease" |

Reasoning: NoticeGiven is the action-needed state (lease ending soon; need to re-lease or accept vacancy) — amber is correct. Vacant and OffMarket are both gray because they're both "no tenant," differentiated only by intent; the outlined OffMarket variant is the lightest possible visual differentiation that still says "this is different from Vacant." Green Occupied matches the cohort-1 vocabulary for positive states.

Confirmed in [`04-rent-roll-page.md`](./04-rent-roll-page.md) and [`tokens.md`](./tokens.md).

## Pending halt conditions (Engineer-side)

These are the FED-surfaced unknowns blocking final PR contracts. None block PAO Track C authoring; PAO direction is contract-agnostic where it can be (the visible UX is the same regardless of how the API serializes ChartId or how CSV export endpoints are namespaced).

| Unknown | Blocks | Resolution path |
|---|---|---|
| `ChartId` wire format in JSON | PR 1 types | Engineer contract-frozen beacon |
| Chart list endpoint shape | PR 1 `<ChartSelector>` | Engineer contract-frozen beacon |
| CSV export endpoint convention (Accept-header vs `/export` route) | PR 1 `<ExportCsvButton>` | Engineer contract-frozen beacon |
| W#77 CIC pre-auth | All PRs DRAFT → Ready flip | Admiral routes to CIC |
| Bridge `/api/v1/reports/{kind}` endpoints authored | PRs 2–5 mock → real swap | Engineer PR 0 (separate from PR 1) |

## Sequencing

1. PAO authors direction docs (this directory) — **THIS PR**
2. Yeoman renders ASCII wireframes per direction (parallel; under PAO supervision) — folded into the direction docs themselves, not a separate output
3. PAO files `pao-status-*-cohort-3-track-c-complete.md` referencing the artifacts
4. FED reads + proceeds to PR 1 execution when W#77 pre-auth ratifies and Engineer's bridge endpoints (PR 0) land

## Acceptance

Per directive `admiral-directive-2026-05-20T02-15Z-pao-cohort-3-design-direction-track-c.md`:

1. Four pages × per-page direction doc + 7 cross-cutting deliverables (`INDEX`, `tokens`, `component-reuse-audit`, `provisionality-banner-pattern`, `run-on-demand-pattern`, `csv-export-pattern`, `states-matrix`) committed to `shipyard/_shared/design/cohort-3/`
2. PAO files `pao-status-*-cohort-3-track-c-complete.md` referencing the artifacts
3. FED can read the artifacts standalone and proceed to PR 1 execution without further PAO clarification

— PAO, 2026-05-22
