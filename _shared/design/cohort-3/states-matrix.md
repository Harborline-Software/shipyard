# States Matrix — Per-Page Variants

Consolidates the state matrix for all 4 cohort-3 pages in one table for FED's quick reference.

The canonical state machine — IDLE → READY_TO_RUN → LOADING → SUCCESS / ERROR with filter-change-resets-result — is defined once in [`run-on-demand-pattern.md`](./run-on-demand-pattern.md). All 4 cohort-3 pages implement it identically; this doc records the per-page UX detail for each state.

## TrialBalancePage (PR 4)

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| IDLE | Mount; or any filter changes after a prior result | Hero copy: "Select a chart and date, then click Run report." Filter bar visible. Run disabled. Export CSV disabled. | User completes required filters → READY_TO_RUN |
| READY_TO_RUN | Required filters valid (Chart selected; AsOf or Period set) | Same hero. Run button enabled. Export CSV disabled. | User clicks Run → LOADING |
| LOADING | Run clicked; mutation in flight | Filter bar dimmed; Run button shows `Running…` + spinner + `aria-busy="true"`. Skeleton rows ×5 in result region. | (none; await server) |
| SUCCESS (`isProvisional === false`) | Mutation success; `result.rows.length > 0`; isProvisional false | Filter bar normal. Table renders. BalanceBadge below tfoot. Export CSV enabled. No provisionality banner. | User can Export CSV; or change filters → IDLE; or re-run for fresh data |
| SUCCESS + provisional (`isProvisional === true`) | Mutation success; rows > 0; isProvisional true | Same as SUCCESS plus amber ProvisionalityBanner above filter bar. Export CSV filename gets `-provisional` suffix. | Same affordances as SUCCESS |
| EMPTY | Mutation success; `result.rows.length === 0` | "No accounts found for this chart and period." + remediation hint. No table, no BalanceBadge. Export CSV disabled. | User changes filters → IDLE → READY_TO_RUN → re-run |
| ERROR | Mutation failure (network / 5xx / malformed payload) | `<ErrorSurface variant="retryable">` with Retry button. Filter bar normal. Export CSV disabled. No table. | User clicks Retry → LOADING (same submitted params); or changes filters → IDLE |

## ArAgingPage (PR 5)

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| IDLE | Mount; or any filter change after SUCCESS | Filter bar shown; no result area | Run button disabled until `chartId` set |
| READY_TO_RUN | `chartId` non-empty; no submitted params yet | Filter bar shown; no result area | Run button enabled |
| LOADING | Mutation in flight | TotalsBar skeleton + 2 × table-row skeletons + (suppressed) TopDelinquentList | Run button → `Running…` + `aria-busy="true"`; Export disabled |
| SUCCESS | Result returned; `!isProvisional` | TotalsBar + 2 tables (By Customer / By Property) + (optional) TopDelinquentList | Run re-enabled for fresh fire; Export enabled |
| SUCCESS + provisional | Result returned; `isProvisional === true` | ProvisionalityBanner above filter bar + standard SUCCESS layout | Same as SUCCESS |
| EMPTY (positive) | `result.totals.totalOpen === 0` | "No outstanding receivables." panel + sub-copy with asOfDate | No CTA; user can re-run with different params |
| ERROR | Mutation rejected | `<ErrorSurface variant="retryable">` red surface | Retry button (re-fires same submitted params) |

**EMPTY semantic note:** The `EMPTY` state is triggered by the **aggregate total being zero** (`totals.totalOpen === 0`), NOT by `byCustomer.length === 0`. The cartridge may return customer rows with all-zero balances; the empty signal that matters for AR aging is "no open money anywhere."

## ProfitAndLossByPropertyPage (PR 3)

| State | Trigger | Visible surface |
|---|---|---|
| IDLE | No `submittedParams` set; user is filling the form | Filter bar visible; content area empty (no skeleton); Run button enabled when `chartId` is set |
| READY_TO_RUN | All required form params valid; user hasn't clicked Run yet | Same as IDLE — the page doesn't distinguish READY_TO_RUN from IDLE visually; the Run button being enabled is the only signal |
| LOADING | `submittedParams` set; query in flight | SkeletonAccordion ×3 + skeleton tile row; Run button shows "Running…" with spinner; Export CSV disabled |
| SUCCESS (multi-property, all collapsed) | Query resolved; `result.byProperty.length >= 2` | PortfolioSummaryTiles ×3 + PropertyAccordionList with all accordions collapsed |
| SUCCESS (single-property, auto-expanded) | Query resolved; `result.byProperty.length === 1` | PortfolioSummaryTiles ×3 + the single PropertyAccordion auto-expanded on initial render |
| SUCCESS + provisional | Any SUCCESS with `result.isProvisional === true` | All of the above + `<ProvisionalityBanner>` visible (collapsed) below page H1, above filter bar |
| EMPTY | Query resolved; `result.byProperty.length === 0` | PortfolioSummaryTiles hidden; "No activity in this period and chart." centered copy |
| ERROR | Query failed | ErrorSurface card + Retry; PortfolioSummaryTiles hidden; PropertyAccordionList hidden; ProvisionalityBanner hidden |

**Auto-expand note:** The single-property auto-expand is a deliberate variant. When the result has exactly one property, the accordion's collapsed-by-default behavior would force a redundant click. Auto-expanding is the helpful default.

## RentRollPage (PR 2)

| State | Trigger | UX |
|---|---|---|
| IDLE | No `submittedParams` (or filters changed since last successful run) | Filter bar visible; helper area below filter bar is blank; no banner, no summary, no tables |
| READY_TO_RUN | All required filters valid; no `submittedParams` yet | Filter bar visible; `Run report` enabled; same blank below |
| LOADING | `submittedParams` set; query in flight | Filter bar shows `Running… ⟳`; `<SkeletonRows ×8>` placeholder table card |
| SUCCESS | `submittedParams` set; query succeeded; result has properties | `<PortfolioSummaryBar>` + `<PropertyBlock>` × N |
| SUCCESS + provisional | Same as SUCCESS + `result.isProvisional === true` | Same as SUCCESS plus `<ProvisionalityBanner>` above the summary bar |
| EMPTY | Query succeeded; `result.properties.length === 0` | "No units found for this chart and date." surface; no summary bar |
| SUCCESS + all-vacant | `includeVacant === true`; `result.properties[*].units` non-empty but all `OccupancyStatus !== 'Occupied'` | Normal SUCCESS rendering; the data IS the message (occupancy tile reads `0%`, status column shows non-occupied pills everywhere) |
| ERROR | Query failed | `<ErrorSurface variant="retryable">` + Retry button |

**SUCCESS + all-vacant note:** RentRoll is the only cohort-3 page where SUCCESS has a meaningful variant beyond "with provisionality" — a 0%-occupied portfolio is a worth-noting case but uses standard SUCCESS rendering because the visual itself communicates the situation (every status pill non-green; tile shows 0%).

## Cross-page common state transitions

The state machine itself is identical across all 4 pages (defined in [`run-on-demand-pattern.md`](./run-on-demand-pattern.md)):

```
IDLE ─[required params set]→ READY_TO_RUN ─[Run clicked]→ LOADING ─[success]→ SUCCESS
                                                                  └[failure]→ ERROR
                                                                  
SUCCESS / ERROR / READY_TO_RUN ─[any filter changes]→ IDLE
```

What differs per page:
- **Which params are required** (all 4 require `chartId`; ArAging additionally requires nothing else; TrialBalance requires `asOfDate` OR `fiscalPeriodId`; P&L requires nothing else but `periodEnd` defaults to today; RentRoll requires nothing else)
- **What "EMPTY" means** semantically (TrialBalance: zero accounts; ArAging: zero outstanding receivables in aggregate; P&L: no activity; RentRoll: no units)
- **Variant SUCCESS states** (P&L distinguishes multi-vs-single property; RentRoll documents the all-vacant variant)

The state-machine MACHINERY (the React Query `submittedParams` pattern with `enabled` gating, the filter-change-resets-result invariant) is identical and shared via PR 1's `<ReportFilterBar>`.

## Universal a11y annotations

These apply to all 4 cohort-3 pages without exception:

- **All loading skeletons:** `aria-busy="true"` + visually-hidden "Loading report" announcement on the result region
- **All error surfaces:** `role="alert"` + `ExclamationTriangleIcon` (red-600) — color is NOT the sole signal
- **All Run buttons:** `aria-busy={isRunning}` + visible text change ("Run report" → "Running…") so SR and sighted users both perceive the state
- **All ProvisionalityBanners:** `role="status"` + `aria-live="polite"` (informational, not action-required; announced on first appearance)
- **All Export CSV buttons:** `aria-busy={isExporting}` during export; failure toast is `role="alert"` + `aria-live="assertive"` (user just initiated the action; needs immediate feedback)
- **All filter forms:** semantic `<form>` markup; Enter submits Run when valid (keyboard-only path)
- **All required inputs:** `aria-required="true"` + `*` in label text
- **All numeric cells:** `tabular-nums` for visual + screen-reader decimal alignment

## EMPTY state copy convention

A pattern emerges across the 4 pages — empty states are **specific to the cartridge's semantic**, not generic "no data":

| Page | Empty copy |
|---|---|
| TrialBalance | "No accounts found for this chart and period." |
| ArAging | "No outstanding receivables." + sub-copy "All customers are current as of {asOfDate}." |
| P&L | "No activity in this period and chart." |
| RentRoll | "No units found for this chart and date." |

The ArAging case is the only **positive** empty state in cohort-3 (a zero-AR situation is *good*; no warning visual treatment is warranted; the sub-copy explicitly celebrates the result). The other three are neutral — "you ran a report and the result was zero rows; this might be expected or might be a filter mistake." None of the four are *error* empties; the ERROR state is its own row, and the cartridge always returns 200 OK with an empty result rather than 404 / error semantics.

## SUCCESS + provisional is always a SUCCESS subspecies

Across all 4 pages, the SUCCESS + provisional state is **not a separate branch** of the state machine. It's a SUCCESS state with one additional visible element (`<ProvisionalityBanner>`). The `isProvisional` flag from the cartridge envelope doesn't change which surfaces render; it only adds the banner above the existing surfaces. All other affordances (Export CSV enabled, filter changes reset to IDLE, etc.) behave identically.

This is deliberate — pattern-011 (provisionality banner) is a **layer on top of** the SUCCESS state, not a replacement for it. The user's mental model is "this is a successful report with a caveat," not "this is some new third kind of result."

## What this matrix does NOT cover

- **Stale-while-revalidate states** — cohort-3 explicitly does NOT have a "previous result shown while new run loads" mode. The IDLE state always clears the result on filter change. See [`run-on-demand-pattern.md`](./run-on-demand-pattern.md) "Result reset semantics."
- **Background-refresh states** — reports are not auto-refreshing; there is no "stale" / "fresh" / "refreshing" tri-state. The only freshness signal is `runAtUtc` in the envelope, which the page may surface as a small "Last run at HH:MM" label below the filter bar (FED's call per page; not load-bearing).
- **Cross-tenant rejection** — out of scope for cohort-3. Reports are scoped server-side to the caller's tenant (per pattern-009 / cohort-2 baseline); the empty-vs-rejected distinction collapses to "empty" by design (no diagnostic leak about other tenants' data).
- **Concurrent run states** — if the user clicks Run twice in rapid succession, the second click is a no-op while the first is in flight (Run button is disabled during LOADING). No queueing, no cancellation, no "latest wins" race handling needed.

— PAO, 2026-05-22
