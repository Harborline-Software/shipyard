# RentRollPage — Design Direction

**Page:** `sunfish/apps/web/src/pages/RentRollPage.tsx`
**PR:** W#77 PR 2
**Cartridge:** `RentRoll`
**Endpoint:** `POST /api/v1/reports/rent-roll`
**Pattern:** `@standing-pattern: pattern-009` (Bridge endpoint + frontend rebind pair) + `@candidate-pattern: pattern-015` (provisional report surface), `pattern-016` (run-on-demand report), `pattern-017` (CSV export affordance)
**Replaces:** `sunfish/apps/web/src/pages/RentRoll.tsx` — retired, no deprecation shim, route rebound to the new page

## Scope

`RentRoll.tsx` today is a flat 9-column ERPNext-direct table with a 3-state status badge and no portfolio summary. Cohort-3 replaces it with a property-blocked view: a single `<PortfolioSummaryBar>` at the top, then one `<PropertyBlock>` per property (a divider-bar `<PropertyHeader>` followed by a 7-column `<UnitTable>`), backed by the `RentRoll` cartridge through `POST /api/v1/reports/rent-roll`. The new page introduces a richer 4-state `OccupancyStatus` badge alongside a separate 6-bucket `DelinquencyBucket` cell, an `ExpiringSoon` flag, and full participation in the three new cohort-3 cross-cutting patterns (provisionality banner, run-on-demand, CSV export).

## What changes from `RentRoll.tsx`

| Concern | `RentRoll.tsx` (current) | `RentRollPage.tsx` (cohort-3) |
|---|---|---|
| Data source | `getRentRoll` against ERPNext direct | `POST /api/v1/reports/rent-roll` cartridge via `useRentRoll(submittedParams)` |
| Auto-run | Yes — fires on mount via `useQuery` | **No** — run-on-demand per pattern-016; IDLE until user clicks Run |
| Layout | Flat single table, one row per unit | Property-blocked — `<PortfolioSummaryBar>` + `<PropertyBlock>` × N (header + table per property) |
| Columns | 9 (Property / Unit / Tenant / Start / End / Rent / Last Pmt / Balance / Status) | 7 (Unit / Tenant / Lease End / Monthly Rent / Open Balance / Delinquency / Status) — 4 columns hidden or repositioned per Known Gaps below |
| Status enum | 3 states (`Current` / `Overdue` / `Vacant`) inline `<StatusBadge>` | 4 `OccupancyStatus` values (`Occupied` / `NoticeGiven` / `Vacant` / `OffMarket`) via `<StatusPill kind="occupancyStatus" …>` |
| Delinquency | Single `Overdue` boolean folded into Status | **Separate** `<StatusPill kind="agingBucket" …>` cell with 6 buckets (`Current` / `Days0To30` / `Days31To60` / `Days61To90` / `Days90Plus` / `NoBalance`) |
| Portfolio summary | None | `<PortfolioSummaryBar>` — 5 tiles (Occupancy Rate / Properties / Units / Monthly Rent / Open Balance) |
| Provisionality | Not surfaced | `<ProvisionalityBanner>` per pattern-015 (AR data crosses open periods routinely) |
| CSV export | Not available | `<ExportCsvButton>` per pattern-017; filename `rent-roll-{asOfDate}.csv` |
| Balance field | `balanceDue` from ERPNext | `openBalance` from cartridge (renamed; same concept, cartridge-canonical) |
| Sort order | Status (`Overdue` → `Current` → `Vacant`) then property | Property name ascending; within each property, unit label ascending (no client-side cross-property sort) |
| Loading state | Single line of `Loading…` text | 8-row skeleton (`SkeletonRows ×8`) |
| Error state | Inline red paragraph | `<ErrorSurface variant="retryable">` per cohort-3 PR 1 conventions |

## Component hierarchy

Lifted from FED spec lines 532–553 and lightly refined to make the pattern-015/012/013 surfaces explicit and to name the per-property block container.

```
RentRollPage
  PageHeader
    h1: "Rent Roll"
    subtitle: "Run on demand against any chart of accounts"
  ReportFilterBar (pattern-016 layout)
    ChartSelector            — required; pattern-016 §"Filter-by-filter conventions"
    AsOfDatePicker           — optional; defaults to today
    ExpiringWindowDaysInput  — number, min=1 max=730, default 90; label "Flag leases expiring within N days"
    IncludeVacantToggle      — checkbox, default ON
    RunButton                — primary; aria-busy when LOADING
    ExportCsvButton          — secondary; disabled until SUCCESS
  ProvisionalityBanner       — visible only on SUCCESS && isProvisional (pattern-015)
  ResultPanel (state-driven)
    [IDLE / READY_TO_RUN]    → empty slot with helper copy (or nothing visible above filter bar)
    [LOADING]                → SkeletonRows × 8 within a single placeholder table card
    [EMPTY]                  → "No units found for this chart and date." surface
    [ERROR]                  → <ErrorSurface variant="retryable"> + Retry
    [SUCCESS]
      PortfolioSummaryBar    — 5-tile responsive grid (see Q-D)
      PropertyBlock × N
        PropertyHeader       — divider-bar variant (see Q-A): property name LEFT + summary stats RIGHT
        UnitTable            — overflow-x-auto; 7 columns; per-row OccupancyStatus + DelinquencyBucket pills
```

No `AllPropertiesFooterRow` is rendered in cohort-3 — the `<PortfolioSummaryBar>` at the top already surfaces every portfolio total the user would seek; a footer-row duplicate just creates two places for the same number to drift. (FED spec line 552 names it as a hierarchy slot; PAO ratifies dropping it for this cohort.)

## Wireframe specs

### Idle / ready-to-run state

```
+------------------------------------------------------------------------------+
|  Rent Roll                                                                   |
|  Run on demand against any chart of accounts                                 |
+------------------------------------------------------------------------------+

+------------------------------------------------------------------------------+
| Chart: [Operating accounts ▾]   As of: [2026-05-22]                          |
| Flag leases expiring within: [90] days       [x] Include vacant              |
|                                                       [Run report]  [Export] |
+------------------------------------------------------------------------------+

   (no banner, no content; the page below the filter bar is blank in IDLE)
```

### Loading state

```
+------------------------------------------------------------------------------+
|  Rent Roll                                                                   |
+------------------------------------------------------------------------------+
| Chart: [Operating accounts ▾]   As of: [2026-05-22]                          |
| Flag leases expiring within: [90] days       [x] Include vacant              |
|                                                  [Running… ⟳]  [Export]      |
+------------------------------------------------------------------------------+

  +--------------------------------------------------------------------------+
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  | ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ |
  +--------------------------------------------------------------------------+
```

Eight skeleton rows (`bg-gray-100 animate-pulse h-8 my-1 rounded`) in a single placeholder table card. No `PortfolioSummaryBar` placeholder — keep the top skeleton compact.

### Success state — full layout (two properties, mixed occupancy + delinquency)

```
+------------------------------------------------------------------------------+
|  Rent Roll                                                                   |
|  Run on demand against any chart of accounts                                 |
+------------------------------------------------------------------------------+
| Chart: [Operating accounts ▾]   As of: [2026-05-22]                          |
| Flag leases expiring within: [90] days       [x] Include vacant              |
|                                                       [Run report]  [Export] |
+------------------------------------------------------------------------------+

+------------------------------------------------------------------------------+
| ⚠ This report covers an open accounting period and may change as            |
|   transactions are posted.                              [Show details ▾]     |
+------------------------------------------------------------------------------+

+------------------------------------------------------------------------------+
| OCCUPANCY     PROPERTIES   UNITS   MONTHLY RENT     OPEN BALANCE             |
|     83%            3        14       $21,800            $1,540               |
+------------------------------------------------------------------------------+

+------------------------------------------------------------------------------+
| 150 Lexington Ct                  12 units | 10 occupied (83%) | $15,200/mo  |
|                                                              | $340 open    |
+------------------------------------------------------------------------------+
| UNIT     TENANT          LEASE END    MONTHLY  OPEN BAL.  DELINQUENCY  STATUS|
|----------|---------------|------------|--------|----------|-------------|----|
| 1A       Maria Santos    2027-03-15      $1,400      $0   —             Occu|
| 1B       James Harlow    2026-06-30 *    $1,300    $340   [0–30]        Occu|
| 2A       Devon Park      2026-07-15 *    $1,400      $0   [Current]     Occu|
| 2B       (vacant)        —                  $0       $0   —             Vacn|
|            EndOfTerm                                                         |
| 3A       Ana Reyes       2027-01-01      $1,500     $50   [Current]     Occu|
| 3B       Liam O'Brien    2026-09-30      $1,500   $1,200  [90+]         Occu|
| …                                                                            |
+------------------------------------------------------------------------------+

+------------------------------------------------------------------------------+
| 22 Cedar Ave                       2 units | 0 occupied (0%)  | $0/mo        |
|                                                              | $0 open      |
+------------------------------------------------------------------------------+
| UNIT     TENANT          LEASE END    MONTHLY  OPEN BAL.  DELINQUENCY  STATUS|
|----------|---------------|------------|--------|----------|-------------|----|
| 1        (vacant)        —                  $0       $0   —             Vacn|
|            Turnover                                                          |
| 2        (n/a)           —                  $0       $0   —             OffM|
+------------------------------------------------------------------------------+
```

Notes on the layout:
- `*` next to a `LEASE END` date is the `[Expiring]` badge shorthand in ASCII; in DOM it's the canonical `bg-amber-100 text-amber-800` pill from the cohort-2 LeasesPage expiry-warning token.
- `Occu` / `Vacn` / `OffM` in the rightmost column are the ASCII shorthand for the canonical `<StatusPill kind="occupancyStatus" value=…>` chip; the inline `EndOfTerm` / `Turnover` sub-label below `(vacant)` is `text-xs text-gray-500` per Q-B below.
- `[0–30]` / `[Current]` / `[90+]` are the canonical aging-bucket pills per Q-C; `—` (rendered as `text-gray-400`) is the `NoBalance` cell.
- Tenant column for vacant rows renders `(vacant)` as `text-gray-400` (NOT an empty cell — empty cells confuse the scanner; "(vacant)" signals intent).
- Property 22 Cedar Ave is shown to exercise the "all-vacant property" sub-case: PropertyHeader summary line reads `0 occupied (0%) | $0/mo | $0 open` and the table still renders (because `includeVacant === true`).

### PropertyHeader in isolation (the divider-bar variant; Q-A answer)

```
+------------------------------------------------------------------------------+
| 150 Lexington Ct                  12 units | 10 occupied (83%) | $15,200/mo  |
|                                                              | $340 open    |
+------------------------------------------------------------------------------+
```

Tokens (composed from existing palette stops; see `tokens.md`):

```
container:           flex items-center justify-between
                     bg-gray-50 border-b border-gray-200 px-4 py-3
property name:       text-base font-semibold text-gray-900   (left)
summary stats line:  text-sm text-gray-600                   (right; pipe-separated)
```

The summary stats string is composed by the page from `RentRollPropertyBlock.summary`:

> `{totalUnits} units | {occupiedUnits} occupied ({occupancyRate}%) | {monthlyRentTotal}/mo | {openBalanceTotal} open`

Formatting:
- `occupancyRate` is the cartridge-emitted number; render rounded to nearest whole percent (`Math.round(rate * 100)`).
- `monthlyRentTotal` and `openBalanceTotal` go through `<CurrencyAmount>` (USD; `Intl.NumberFormat`).
- When `totalUnits === 0` for a property block (shouldn't happen — cartridge filters empty blocks out by default — but defend), render only the property name; suppress the stats line.

### UnitTable showing every `OccupancyStatus` × `DelinquencyBucket` variant

```
+------------------------------------------------------------------------------+
| Demo property — exercises every badge variant                                |
+------------------------------------------------------------------------------+
| UNIT  TENANT         LEASE END    MONTHLY  OPEN BAL.  DELINQUENCY    STATUS  |
|-------|---------------|------------|--------|----------|---------------|------|
| A1    Tenant Alpha   2027-01-31     $1,000      $0    —              Occupied|
| A2    Tenant Bravo   2027-01-31     $1,000      $5    [Current]      Occupied|
| A3    Tenant Char.   2027-01-31     $1,000     $50    [0–30]         Occupied|
| A4    Tenant Delta   2027-01-31     $1,000    $400    [31–60]        Occupied|
| A5    Tenant Echo    2027-01-31     $1,000    $850    [61–90]        Occupied|
| A6    Tenant Foxtrot 2027-01-31     $1,000  $1,800    [90+]          Occupied|
| B1    Tenant Golf    2026-07-01 *   $1,000      $0    —              NoticeGiven (EndOfTerm) |
| C1    (vacant)       —                  $0      $0    —              Vacant       |
|         Turnover                                                              |
| D1    (n/a)          —                  $0      $0    —              OffMarket   |
+------------------------------------------------------------------------------+
```

Pill rendering rules from this:
- **OccupancyStatus** column uses `<StatusPill kind="occupancyStatus" value={status} tooltip={vacancyReason ?? undefined}>`. `NoticeGiven` shows the `vacancyReason` via `title=` attribute (tooltip on hover); `Vacant` shows the `vacancyReason` inline below the badge as `text-xs text-gray-500` (no tooltip).
- **DelinquencyBucket** column uses `<StatusPill kind="agingBucket" value={bucket}>`. `NoBalance` renders as `—` (`text-gray-400`) with **no pill** — absence is the absence of overdue. `Current` renders as a small `bg-gray-100 text-gray-700` pill labeled `Current` to signal "checked + zero" (distinct from `NoBalance`, which means the row has nothing to age in the first place).

### PortfolioSummaryBar in isolation (Q-D answer)

```
+------------------------------------------------------------------------------+
| OCCUPANCY    PROPERTIES   UNITS    MONTHLY RENT     OPEN BALANCE             |
|     83%           3        14        $21,800            $1,540               |
| (occupancy   (properties (units      (across all      (across all            |
|  rate)        covered)    surveyed)   units)            properties)          |
+------------------------------------------------------------------------------+
```

Layout: `grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-5 gap-3` (per Q-D). Five tiles in canonical order:

| Position | Label | Value source | Value style |
|---|---|---|---|
| 1 | OCCUPANCY (anchor tile) | `portfolio.occupancyRate` → `Math.round(× 100)%` | `text-3xl font-semibold text-gray-900 tabular-nums` |
| 2 | PROPERTIES | `portfolio.propertiesCovered` | `text-2xl font-semibold text-gray-900 tabular-nums` |
| 3 | UNITS | `portfolio.totalUnits` | `text-2xl font-semibold text-gray-900 tabular-nums` |
| 4 | MONTHLY RENT | `portfolio.monthlyRentTotal` | `text-2xl font-semibold text-gray-900 tabular-nums` |
| 5 | OPEN BALANCE | `portfolio.openBalanceTotal` | `text-2xl font-semibold text-gray-900 tabular-nums` |

The Occupancy tile is the visual anchor — sized one font step larger (`text-3xl` vs `text-2xl`) to communicate that it's the primary portfolio KPI. The other four are equal-weight.

Each tile follows the cohort-2 AccountingPage summary-tile pattern:

```
container:  rounded-lg border border-gray-200 bg-white px-3 py-2
label:      text-xs uppercase tracking-wide text-gray-500
value:      tabular-nums (font-size per table above)
sub-label:  text-xs text-gray-500   (optional; the parenthesized helper text)
```

The sub-label is mandatory on the first user-encounter cohort; FED can suppress it after onboarding cohort if it dilutes density. PAO recommends keeping it for cohort-3 — five tiles benefit from disambiguation while users learn the page.

### Empty state

```
+------------------------------------------------------------------------------+
|                                                                              |
|                    No units found for this chart and date.                   |
|                                                                              |
|                Try widening the as-of date or selecting another chart.       |
|                                                                              |
+------------------------------------------------------------------------------+
```

Centered helper copy in a `border border-gray-200 rounded-lg bg-white px-6 py-12 text-center text-sm text-gray-500` container. No CTA button (this is a report, not a write surface; the only meaningful action is changing filters and re-running, which the filter bar already supports).

### Error state

```
+------------------------------------------------------------------------------+
|  ⚠ Couldn't load the rent roll                                              |
|                                                                              |
|  The report service didn't respond. Try again in a moment.                   |
|                                                                              |
|  ┌─────────┐                                                                 |
|  │ Retry   │                                                                 |
|  └─────────┘                                                                 |
+------------------------------------------------------------------------------+
```

Standard `<ErrorSurface variant="retryable">` from PR 1 — red surface (`border-red-200 bg-red-50 text-red-700 rounded-lg p-4`), exclamation icon, title + body copy, Retry button that re-fires the mutation with the same `submittedParams`. The Retry button is scoped to this surface and is NOT the same identity as the filter bar's Run button (per pattern-016 §"Run button copy + visual states").

## State machine summary

Identical to pattern-016's canonical state machine:

```
IDLE → READY_TO_RUN → LOADING → { SUCCESS | ERROR }
                              ↑           ↓
                              └── filter change resets to IDLE ──┘
```

See `run-on-demand-pattern.md` for the canonical machine; this page does not add any page-specific states. The `includeVacant` toggle is part of `formParams` like every other filter and triggers the IDLE reset on change (per pattern-016 invariant 3).

## Provisionality banner placement

Per `provisionality-banner-pattern.md`: below the page subtitle, above the filter bar, full content-width. Rent Roll **routinely** crosses open periods because the cartridge inspects open-period AR data to compute `openBalance` and `delinquencyBucket` per unit. Treat the banner as common-case — likely visible on most production runs until the user picks an as-of date that lands inside a closed period.

The banner copy + collapse interaction is governed entirely by `provisionality-banner-pattern.md`; this page does not customize.

## CSV export

Per `csv-export-pattern.md`. The `<ExportCsvButton>` sits adjacent to the Run button in the filter bar (right-aligned pair). Filename templates:

| Condition | Filename |
|---|---|
| `isProvisional === false` | `rent-roll-{asOfDate}.csv` |
| `isProvisional === true` | `rent-roll-{asOfDate}-provisional.csv` |

`{asOfDate}` is the `asOfDate` from `submittedParams` if explicit; otherwise the date the cartridge defaulted to (extracted from `result.asOf`). Example: `rent-roll-2026-05-22.csv`.

## Resolved PAO design direction answers

FED asked five questions (spec lines 598–604). Five definite answers below.

### A. PropertyHeader visual

**Pick the DIVIDER-BAR.** Not a card header, not an icon, not a property emblem.

Reasoning:
- Properties are organizational containers in this page, not first-class entities — the unit rows under each property are the load-bearing data.
- A full card header (`Card` primitive with elevation + padding) competes visually with the unit data for primary attention; the user is here to scan units, not admire property cards.
- An icon adds visual noise without aiding scanning — property names are unique within the portfolio and unambiguously identify themselves textually.
- The divider-bar pattern is the same vocabulary as the cohort-2 AccountingPage section dividers; users learn it once.

Visual spec:

```
container:           flex items-center justify-between
                     bg-gray-50 border-b border-gray-200 px-4 py-3
property name:       text-base font-semibold text-gray-900   (left)
summary stats:       text-sm text-gray-600                   (right; pipe-separated)
```

The header reads as a **section header** for the unit table below — which matches the actual reading order (user sees "this is property X" then scans the units beneath). The unit table sits directly underneath with no gap.

### B. OccupancyStatus badge palette

**Apply [Q7 from INDEX](./INDEX.md#q7--occupancystatus-badge-palette-rentroll) verbatim.** The canonical palette:

| Status | Background | Text | Border | Notes |
|---|---|---|---|---|
| Occupied | `bg-green-100` | `text-green-700` | — | matches cohort-1 work-order `Completed` |
| NoticeGiven | `bg-amber-100` | `text-amber-800` | — | action-needed state; tooltip exposes `vacancyReason` on hover via `title=` |
| Vacant | `bg-gray-100` | `text-gray-700` | — | inline `vacancyReason` sub-label below the badge as `text-xs text-gray-500` (no tooltip) |
| OffMarket | `bg-gray-100` | `text-gray-600` | `border border-gray-300` | outlined variant — "intentionally not for lease" |

`VacancyReason` surfacing differs by status:
- **NoticeGiven** → `title={vacancyReason}` on the badge element; tooltip on hover. Reason: `NoticeGiven` rows have a tenant and full row data; the row is already information-dense, and a tooltip lets the reason stay hidden in the default scan.
- **Vacant** → inline sub-label `<div className="text-xs text-gray-500">{vacancyReason}</div>` below the badge. Reason: vacant rows have mostly empty cells (no tenant, no rent, no balance); using the visual real estate to surface why the unit is vacant is productive.
- **OffMarket** → no `vacancyReason` rendered. The `OffMarket` status itself carries the meaning; the cartridge will populate `vacancyReason: OffMarket` redundantly, which is information-free.
- **Occupied** → no `vacancyReason` rendered (it's `undefined` by contract on Occupied rows).

The full canonical name in `tokens.md`: `occupancy-badge` (×4 variants). Use `<StatusPill kind="occupancyStatus" value={status} tooltip={vacancyReason}>` from `@sunfish/ui-react` — `tooltip` prop is consumed only when status is `NoticeGiven`.

### C. DelinquencyBucket cell treatment

**Pick BADGE — use the canonical `<AgingBucketPill>` from `@sunfish/ui-react` (PR 1) / `<StatusPill kind="agingBucket">`.** Not colored text, not an icon.

Reasoning:
- Badge is what users learned in cohort-2 `AccountingPage` (outstanding `<DaysDuePill>`) and what `<AgingBucketPill>` will surface in cohort-3's `ArAgingPage` `<TopDelinquentList>`. Visual consistency across the financial surface area matters more than per-page optimization.
- Colored text inside a row of textual data competes with the other text cells and blurs scanning; a discrete pill creates the contrast that overdue rows deserve.
- The aging-bucket palette is already a canonical token family (`aging-bucket-pill` × 6, per `tokens.md`); reusing it here propagates the investment.

Variant treatment:

| Bucket | Pill | Rationale |
|---|---|---|
| `NoBalance` | **`—` (`text-gray-400`)** — no pill | Absence is the absence of overdue. A pill here would imply "checked + zero" — `Current` already means that. |
| `Current` | small `bg-gray-100 text-gray-700` pill labeled `Current` | Signals "checked + zero." Differentiates from `NoBalance` (no checking happened / no aging applies). |
| `Days0To30` | canonical `bg-amber-50 text-amber-900` pill labeled `0–30` | Light warning. |
| `Days31To60` | canonical `bg-amber-100 text-amber-900` pill labeled `31–60` | Warning. |
| `Days61To90` | canonical `bg-orange-100 text-orange-900` pill labeled `61–90` | Stronger warning. |
| `Days90Plus` | canonical `bg-red-100 text-red-900` pill labeled `90+` | Severe. |

(The exact palette stops align with the `aging-bucket-pill` × 6 canonical in `tokens.md` and with the `ArAgingTable` header tints. `0–30` uses `bg-amber-50` to keep the cell-level pill one shade lighter than `31–60`'s `bg-amber-100`, which mirrors the cohort-3 convention that cell pills are lighter than column-header tints.)

Pill labels use en-dash characters (`–`) not hyphens, to match accounting-convention range notation (consistent with `ArAgingTable` headers).

### D. PortfolioSummaryBar tile count + layout

**Pick FIVE TILES** in canonical order: Occupancy Rate | Properties | Units | Monthly Rent | Open Balance.

**Layout:** `grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-5 gap-3` — five tiles fit cleanly at `lg:` (Surface Pro landscape and above); reflow to four-per-row at `sm:` (Surface Pro portrait); two-per-row at the bare phone width.

Reasoning:
- Five stats are the load-bearing portfolio numbers. Dropping to four would force a choice between "Properties" (operator scale) and "Units" (asset scale) which both matter; dropping to three would suppress either Monthly Rent or Open Balance, which are the two dollar values that anchor "is this portfolio healthy."
- Six tiles would either crowd at `lg:` or trigger a fifth-vs-sixth-row wrap that breaks visual rhythm.
- The Occupancy Rate tile is the **visual anchor**: sized one font step larger (`text-3xl` vs the others' `text-2xl`) to communicate primary-KPI status. This mirrors a convention from financial dashboards where one anchor metric leads a row of supporting metrics.
- Tile pattern reuses the cohort-2 AccountingPage summary-tile component verbatim; no new tile primitive needed.

Position: below the filter bar (and below the provisionality banner when present), above the first `<PropertyBlock>`. Full content-width.

The bar is **always visible in SUCCESS state** — even when `portfolio.totalUnits === 0` (the cartridge can return zero-unit portfolios when filters exclude everything). In that case the tiles all show their `0` / `0%` / `$0` values; the user sees that the report ran and returned nothing, distinct from the EMPTY state where the cartridge returned an empty `properties[]`.

### E. Column visibility at `<md:`

**Apply [Q3 from INDEX](./INDEX.md#q3--rentroll-responsive-hide-delinquency-column-at-md) verbatim — do NOT hide Delinquency.**

The `<UnitTable>` container uses `overflow-x-auto`, letting the user scroll horizontally on narrow screens. The `<PropertyHeader>` stays full-width above the scrolling table. The `<PortfolioSummaryBar>` reflows per Q-D.

Reasoning (from Q3):
- Hiding `Delinquency` on small screens hides the cohort-3 differentiation from the legacy page — the delinquency-bucket cell is a deliberate addition.
- Surface Pro landscape (1366×912) accommodates all 7 columns without horizontal scroll; only portrait or narrower viewports need scroll.
- Horizontal scroll on data tables is an accepted pattern for dense reports; users on narrow viewports are sophisticated enough to scroll within a section.

The `overflow-x-auto` wrapper sits **on each `<UnitTable>` individually**, not on the page. This means the `<PortfolioSummaryBar>` and `<PropertyHeader>` rows remain anchored at viewport width; only the table contents inside each property block can scroll. This is the right scope — the user's eye stays oriented to the property header while they scan units.

## UnitTable column design

Seven columns. Order is read-priority: identity first, then tenant, then lease state, then financial state, then occupancy state. (Read-priority order intentionally puts the financial state to the right of the tenant/lease state because the user scans for who/where before how-much.)

| # | Column | Source | Alignment | Cell rendering |
|---|---|---|---|---|
| 1 | Unit | `unitLabel` | left | `font-mono text-sm text-gray-900` — monospaced for label uniformity |
| 2 | Tenant | `tenantName` | left | `text-sm text-gray-900` when present; `text-gray-400` "(vacant)" when null |
| 3 | Lease End | `leaseEnd` (ISO `YYYY-MM-DD`) | left | `text-sm text-gray-600`; if `expiringSoon === true`, append `<span className="ml-2 inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800">Expiring</span>`; render `—` when `leaseEnd` is null |
| 4 | Monthly Rent | `monthlyRent` | right | `<CurrencyAmount>` USD; `tabular-nums`; render `$0` as `$0` (NOT `—`) — monthly rent of zero is meaningful (vacant unit on the market) |
| 5 | Open Balance | `openBalance` | right | `<CurrencyAmount>` USD; `tabular-nums`; render `0` as `—` (`text-gray-400`) — zero balance is the visual baseline |
| 6 | Delinquency | `delinquencyBucket` | left | per Q-C: `<StatusPill kind="agingBucket">` or `—` for `NoBalance` |
| 7 | Status | `status`, `vacancyReason` | left | per Q-B: `<StatusPill kind="occupancyStatus">` with conditional tooltip + sub-label |

Table-level styling (per `tokens.md` `RentRollPage` section):

```
container:           overflow-x-auto rounded-lg border border-gray-200 bg-white
table:               min-w-full divide-y divide-gray-200 text-sm
thead:               bg-gray-50
th:                  px-3 py-2 text-xs font-medium uppercase tracking-wide
                     text-gray-500 text-left   (text-right for Rent + Open Balance)
tbody:               divide-y divide-gray-100
tr (hover):          hover:bg-gray-50
td:                  px-3 py-2 (alignment per column rules above)
```

No row click-through to a unit detail page in cohort-3 (no such page exists). No row-level actions.

Row sort within a property: `unitLabel` ascending (alphanumeric natural sort if `@sunfish/ui-react` exposes a helper; otherwise plain JS string compare — the cartridge already pre-sorts).

## PropertyBlock structure

A `<PropertyBlock>` is the unit of repetition. Each block is:

1. `<PropertyHeader>` — divider-bar from Q-A
2. `<UnitTable>` — `overflow-x-auto` container around the 7-column table

Render rules:

- **Sort properties** by `propertyName` ascending (case-insensitive). Until property-management ships, `propertyName === propertyKey`, so this is effectively a sort by key.
- **Skip empty property blocks** when `units.length === 0` AND `includeVacant === false`. Render them when `includeVacant === true` AND `units.length === 0` (the "all-vacant property" case — show the property exists with zero units in this filter). When the cartridge emits an empty `properties[]` overall, the page falls through to the EMPTY state.
- **No separator between blocks** — the `border-b border-gray-200` on the `<PropertyHeader>` and the rounded card edge of the `<UnitTable>` provide enough visual separation. Add `mt-6` margin between blocks.
- **No collapsible blocks** — the per-property header is informational, not interactive. Users scrolling the rent roll want to see all data in one scan, not collapse-and-expand. (Contrast with `ProfitAndLossByPropertyPage`, which does use accordions because the line-item content is dense per property.)

## Hidden / deferred cohort-3 fields

Document these explicitly so FED doesn't render them and reviewers don't ask why they're missing:

| Field | Wire value in cohort-3 | Rendering | Comment in code |
|---|---|---|---|
| `lastPaymentDate` | always `null` (cartridge unimplemented) | **column not rendered** | `// TODO(cohort-4 or later): surface lastPaymentDate column when cartridge populates it.` |
| `prepaidBalance` | always `0` (cartridge unimplemented) | **not surfaced anywhere** | `// TODO(cohort-4 or later): surface prepaidBalance once cartridge supports it (likely separate cell adjacent to openBalance).` |
| `projectedNextMonthRent` | always `=== monthlyRent` (cartridge unimplemented) | **no separate column** | `// TODO(cohort-4 or later): surface projectedNextMonthRent column once cartridge computes it independently (e.g., for in-progress rent increases).` |
| `propertyName` | `=== propertyKey` (property-management cluster unshipped) | render `propertyKey` AS the header property name (no transformation) | no TODO — this is a cartridge-side dependency on property-management, not a per-call gap |

Why these are deferred not removed: the wire contract carries the fields, so the TypeScript types include them. The page just doesn't read them in cohort-3. A future cohort can add the column / cell / sub-tile without a type bump.

## Token usage

Reference `tokens.md` for the canonical names. RentRollPage consumes:

| Canonical token | Where |
|---|---|
| `provisional-surface` (×1) | `<ProvisionalityBanner>` |
| `occupancy-badge` × 4 variants | Status column in `<UnitTable>` |
| `aging-bucket-pill` × 6 variants | Delinquency column in `<UnitTable>` |
| Expiring-soon badge (existing, matches cohort-2 LeasesPage expiry warning) | Lease End column when `expiringSoon === true` |
| Summary tile pattern (existing, cohort-2 AccountingPage) | `<PortfolioSummaryBar>` × 5 tiles |
| Divider-bar pattern (existing, cohort-2 section dividers) | `<PropertyHeader>` |
| Table primitives (existing) | `<UnitTable>` |
| Skeleton row (existing) | LOADING state |
| Error surface (existing) | ERROR state |

No new color tokens introduced by this page.

## Component reuse

From `@sunfish/ui-react` (PR 1 promotes `<StatusPill>`):

- `<StatusPill kind="occupancyStatus" value={status} tooltip={vacancyReason}>` — Status column
- `<StatusPill kind="agingBucket" value={bucket}>` — Delinquency column (also exposed as `<AgingBucketPill value={bucket}>` convenience alias)
- `<CurrencyAmount amount={n}>` — Monthly Rent + Open Balance cells + PortfolioSummaryBar dollar tiles
- `<Card>` — Tile container in `<PortfolioSummaryBar>`

From `apps/web/src/components/` (PR 1 shared infra):

- `<ProvisionalityBanner result={query.data}>` — handles the `isProvisional` + `warnings[]` surface
- `<ExportCsvButton enabled={…} onExport={…} filename={…}>` — CSV affordance
- `<ReportFilterBar>` — wraps `<ChartSelector>` + `<AsOfDatePicker>` + `<RunButton>` + the page-specific child filters (`ExpiringWindowDaysInput`, `IncludeVacantToggle`)
- `<ChartSelector value={chartId} onChange={…}>` — required filter
- `<RunButton state={…}>` — primary action

No NEW page-local components beyond PR 1's shared set. The `<PropertyHeader>` and `<PortfolioSummaryBar>` are simple enough that FED inlines them within `RentRollPage.tsx` rather than promoting them to `apps/web/src/components/`. (If a future cohort needs the same divider-bar pattern, promote then; not before.)

## Accessibility

- Each `<UnitTable>` uses semantic `<table>` + `<thead>` + `<tbody>` markup with `scope="col"` on every `<th>`.
- The `<table>` element carries `aria-label={`Rent roll for ${propertyName}`}` so screen readers announce property context when they enter the table — critical for the multi-property page structure where one user-perceived "page" contains multiple tables.
- Status + Delinquency badges convey meaning through text inside the pill (`Occupied`, `0–30`, etc.) — color is supplementary, not load-bearing. Users who don't perceive color still get the meaning.
- The `<PropertyHeader>` stats line uses `aria-label` to surface the spoken form: `aria-label="12 units, 10 occupied, 83 percent, $15,200 per month, $340 open balance"`. The pipe-separated visual form is for sighted scanning; the aria-label provides the linearized natural-language form.
- The `Expiring` badge in the Lease End column has `aria-label="Expiring within {expiringWindowDays} days"` so screen readers convey the threshold context.
- The `IncludeVacant` checkbox label is explicit: `<label>Include vacant units</label>` (not `<label>Include vacant</label>`) — disambiguates that it's units, not properties.
- The `<ProvisionalityBanner>` accessibility comes from the pattern doc verbatim (`role="status"` + `aria-live="polite"`).
- The `<RunButton>` and `<ExportCsvButton>` accessibility comes from the pattern docs verbatim.
- Tab order through the filter bar: ChartSelector → AsOfDatePicker → ExpiringWindowDaysInput → IncludeVacantToggle → RunButton → ExportCsvButton. Submit on Enter (from any filter input) fires Run when enabled.

## Responsive behavior

- **Surface Pro landscape (1366×912):** all 7 `<UnitTable>` columns fit; no horizontal scroll. `<PortfolioSummaryBar>` shows 5 tiles per row.
- **Surface Pro portrait (912×1366):** `<UnitTable>` scrolls horizontally within each property block (per Q-E); `<PropertyHeader>` stays full-width above. `<PortfolioSummaryBar>` reflows to 4 tiles per row at `sm:` breakpoint.
- **Phone (< 640px):** `<UnitTable>` scrolls horizontally; `<PortfolioSummaryBar>` reflows to 2-per-row at the base grid; filter bar action buttons drop to a third row.
- **Wide desktop (≥ 1280px):** 5 tiles inline at `lg:`; full table fits with comfortable spacing.

The `<PropertyHeader>` `flex justify-between` may need to wrap on the narrowest viewports — when the summary stats string can't fit on the same line as the property name, it drops to a second line below the name (default flex-wrap behavior). This is acceptable; the bar grows from one line to two, no truncation.

## States summary

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

## Pattern alignment

- `@standing-pattern: pattern-009` — Bridge endpoint + frontend rebind pair. `POST /api/v1/reports/rent-roll` is the new Bridge endpoint; `RentRollPage.tsx` is the frontend rebind. Triggers the standard SPOT-CHECK dispatch SLA per fleet conventions.
- `@candidate-pattern: pattern-015` — provisional report surface. `<ProvisionalityBanner>` consumption is the first-instance signature.
- `@candidate-pattern: pattern-016` — run-on-demand report. IDLE → READY_TO_RUN → LOADING → SUCCESS state machine is the first-instance signature.
- `@candidate-pattern: pattern-017` — CSV export affordance. `<ExportCsvButton>` consumption is the first-instance signature.

All three candidates ratify together on the next cohort that consumes them consistently (most likely cohort-4 AP Aging).

— PAO, 2026-05-22
