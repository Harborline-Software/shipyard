# ArAgingPage — Design Direction

**Page:** `sunfish/apps/web/src/pages/ArAgingPage.tsx` (new — no prior file replaces it)
**PR:** W#77 PR 5
**Cartridge:** `ArAgingSummary` (`shipyard/packages/blocks-reports/`)
**Endpoint:** `POST /api/v1/reports/ar-aging`
**Patterns:** `@standing-pattern: pattern-009` + `@candidate-pattern: pattern-011, pattern-012, pattern-013`

## Scope

`ArAgingPage` is a new run-on-demand report page that surfaces accounts-receivable aging across a chart of accounts as of a chosen date. It renders two parallel breakdowns — **By Customer** and **By Property** — so the user can answer "who owes us?" and "where do we have collection drag?" in a single run, plus a focused **Top N delinquent customers** list pinned below for collections triage. There is no prior `ArAging.tsx` to migrate; this page is born aligned with the cohort-3 substrate (`ReportRunResult<TResult>` envelope, IDLE → READY_TO_RUN state machine, `<ProvisionalityBanner>`, `<ExportCsvButton>`).

## Component hierarchy

```
ArAgingPage
  PageHeader
    <h1>AR Aging</h1>
    <p class="subtitle">Open receivables by customer and by property as of a chosen date</p>
  ReportFilterBar
    ChartSelector (required)
    AsOfDatePicker (optional; defaults to today)
    TopNSelector ("Show top N delinquent" — number stepper; default 10, min 0, max 100; 0 hides the section)
    RunButton
    ExportCsvButton (disabled until result present)
  ProvisionalityBanner (when result.isProvisional)
  [LOADING]   → SkeletonTotalsBar + SkeletonRows ×5 (twice — once per section)
  [EMPTY]     → "No outstanding receivables." panel (positive empty)
  [ERROR]     → red surface + Retry
  [SUCCESS]
    ArAgingTotalsBar               — portfolio summary tiles (6 tiles: 5 buckets + Total)
    ArAgingSection (By Customer)
      <h2>By Customer</h2>
      AgingTable (rows = result.byCustomer)
    ArAgingSection (By Property)
      <h2>By Property</h2>
      AgingTable (rows = result.byProperty)
    TopDelinquentList (when topDelinquentN > 0 AND result.topDelinquent.length > 0)
      <h2>Top {topDelinquentN} delinquent customers</h2>
      ranked list (max N entries)
```

`ReportFilterBar` is the shared infrastructure component from PR 1; the page passes its page-specific filters (chart, as-of, top-N) as children plus the canonical Run + Export buttons. The two `ArAgingSection` blocks are local-only — they don't graduate to `@sunfish/ui-react`; they're a thin wrapper around `<h2>` + `<AgingTable>`.

## Wireframe specs

### 1. IDLE — page just mounted

```
┌──────────────────────────────────────────────────────────────────┐
│  AR Aging                                                         │
│  Open receivables by customer and by property as of a chosen date │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ Chart: [— select a chart ▾]   As of: [2026-05-22]                 │
│ Show top [10] delinquent customers                                │
│                                                                   │
│                                       [Run report]  [Export CSV] │
└──────────────────────────────────────────────────────────────────┘

(no content below filter bar — page is IDLE)
```

`Run report` is disabled (gray, `opacity-50`) until a chart is selected. `Export CSV` is disabled. No skeleton, no empty placeholder — the page is **silent** until the user runs it. This is the run-on-demand discipline from pattern-012.

### 2. READY_TO_RUN — chart picked, ready to fire

```
┌──────────────────────────────────────────────────────────────────┐
│ Chart: [Operating accounts ▾]   As of: [2026-05-22]               │
│ Show top [10] delinquent customers                                │
│                                                                   │
│                                       [Run report]  [Export CSV] │
│                                          ────────                 │
│                                          enabled                  │
└──────────────────────────────────────────────────────────────────┘
```

### 3. SUCCESS — full layout (the canonical visual)

```
┌──────────────────────────────────────────────────────────────────┐
│  AR Aging                                                         │
│  Open receivables by customer and by property as of a chosen date │
└──────────────────────────────────────────────────────────────────┘

(ProvisionalityBanner here when result.isProvisional — see pattern-011 doc)

┌──────────────────────────────────────────────────────────────────┐
│ Chart: [Operating accounts ▾]   As of: [2026-05-22]               │
│ Show top [10] delinquent customers                                │
│                                       [Run report]  [Export CSV] │
└──────────────────────────────────────────────────────────────────┘

┌────────┬────────┬────────┬────────┬────────┬┬─────────┐
│Current │ 0–30 d │31–60 d │61–90 d │ 90+ d  ││  TOTAL  │
│$12,000 │ $3,400 │$1,200  │ $500   │$2,400  ││ $19,500 │
└────────┴────────┴────────┴────────┴────────┴┴─────────┘
   (six tiles in a 6-col grid on lg+, 2-col on mobile)
   (Total tile has a left border-l-4 border-blue-500 separator)

By Customer
┌─────────────────┬────────┬───────┬───────┬───────┬───────┬────────┐
│ Customer        │Current │ 0–30  │ 31–60 │ 61–90 │  90+  │ Total  │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ Acme Holdings   │ $4,000 │ $1,200│   —   │   —   │   —   │ $5,200 │
│ Maria Santos    │ $1,250 │   —   │   —   │   —   │$1,200 │ $2,450 │
│ Mountain View   │ $2,500 │   —   │ $800  │   —   │   —   │ $3,300 │
│ James Harlow    │   —    │   —   │   —   │ $500  │ $800  │ $1,300 │
│ ...             │ ...    │  ...  │  ...  │  ...  │  ...  │  ...   │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ TOTAL           │$12,000 │$3,400 │$1,200 │ $500  │$2,400 │$19,500 │
└─────────────────┴────────┴───────┴───────┴───────┴───────┴────────┘

By Property
┌─────────────────┬────────┬───────┬───────┬───────┬───────┬────────┐
│ Property        │Current │ 0–30  │ 31–60 │ 61–90 │  90+  │ Total  │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ 150 Lexington Ct│ $5,500 │$2,200 │   —   │   —   │ $800  │ $8,500 │
│ Oak Park Apts   │ $3,250 │   —   │$1,200 │   —   │$1,600 │ $6,050 │
│ Riverside Lofts │ $3,250 │$1,200 │   —   │ $500  │   —   │ $4,950 │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ TOTAL           │$12,000 │$3,400 │$1,200 │ $500  │$2,400 │$19,500 │
└─────────────────┴────────┴───────┴───────┴───────┴───────┴────────┘

Top 10 delinquent customers
─────────────────────────────────────────────────────────────────
(1) Maria Santos                       90+: $1,200    Total: $2,450
(2) James Harlow                       90+: $800      Total: $1,300
(3) Northgate Realty                   90+: $400      Total: $400
(4) ...
```

### 4. ArAgingTotalsBar in isolation (central visual)

```
                                                ┃
┌─────────┬─────────┬─────────┬─────────┬─────────┃─────────┐
│ CURRENT │  0–30 d │ 31–60 d │ 61–90 d │  90+ d  ┃  TOTAL  │
│         │         │         │         │         ┃         │
│ $12,000 │  $3,400 │  $1,200 │   $500  │  $2,400 ┃ $19,500 │
└─────────┴─────────┴─────────┴─────────┴─────────┃─────────┘
                                                ┃
(left border-l-4 border-blue-500 on the Total tile sets it apart)
```

Tile shell per tile (compose existing tokens):
- Container: `rounded-lg border border-gray-200 bg-white px-3 py-2`
- Label: `text-xs uppercase tracking-wide text-gray-500`
- Value: `text-lg font-semibold text-gray-900 tabular-nums`

Total tile adds: `border-l-4 border-blue-500` (replaces the left edge of the gray border with the blue accent so the tile reads as a sum, not another bucket).

Layout responsiveness:
- `lg:` (≥1024px): `grid-cols-6` — single row, all 6 tiles inline
- `sm:–lg:` (640–1023px): `grid-cols-3` — two rows of three
- `<sm:` (<640px): `grid-cols-2` — three rows of two

The tile palette deliberately **does not** mirror the AgingTable header tints — the totals bar is a neutral summary surface; the buckets get their semantic color **only** when they appear as table column headers (which is where the user is actively comparing buckets row-by-row).

### 5. AgingTable in isolation (header tints)

```
┌─────────────────┬──────────┬──────────┬──────────┬──────────┬──────────┬────────┐
│                 │░CURRENT░░│░░0–30 d░░│▒▒31–60 d▒│▓▓61–90 d▓│██90+ d██│ TOTAL  │
│ Customer        │ gray-50  │ gray-50  │ amber-50 │orange-50 │ red-50   │        │
├─────────────────┼──────────┼──────────┼──────────┼──────────┼──────────┼────────┤
│ Acme Holdings   │  $4,000  │  $1,200  │    —     │    —     │    —     │ $5,200 │
│ Maria Santos    │  $1,250  │    —     │    —     │    —     │  $1,200  │ $2,450 │
└─────────────────┴──────────┴──────────┴──────────┴──────────┴──────────┴────────┘

Header tint canonical (from tokens.md → aging-bucket-header-tint):
  Current  → bg-gray-50  text-gray-700
  0–30 d   → bg-gray-50  text-gray-700   (no-overdue is neutral, same as Current)
  31–60 d  → bg-amber-50 text-amber-900
  61–90 d  → bg-orange-50 text-orange-900
  90+ d    → bg-red-50   text-red-900
  Total    → bg-gray-50  text-gray-700   (the Total column header is neutral)
```

### 6. TopDelinquentList in isolation

```
Top 10 delinquent customers
─────────────────────────────────────────────────────────────────
(1) Maria Santos                       90+: $1,200    Total: $2,450
(2) James Harlow                       90+: $800      Total: $1,300
(3) Northgate Realty                   90+: $400      Total: $400
(4) Hillcrest Holdings                 90+: $250      Total: $250
(5) Oakridge Property Mgmt             90+:   —       Total: $1,800
(6) Sunset Capital                     90+:   —       Total: $1,200
(7) ...

Per-row composition:
  Rank chip   →  inline-flex items-center justify-center w-6 h-6
                 rounded-full bg-gray-100 text-xs font-semibold text-gray-600
  Name        →  text-sm font-medium text-gray-900
  90+ amount  →  text-red-700 font-medium tabular-nums   (when > 0)
                 text-gray-400 "—"                         (when 0)
  Total       →  text-sm text-gray-700 tabular-nums
  Row border  →  border-b border-gray-100
```

### 7. EMPTY — "No outstanding receivables." (positive empty)

```
┌──────────────────────────────────────────────────────────────────┐
│  AR Aging                                                         │
│  Open receivables by customer and by property as of a chosen date │
└──────────────────────────────────────────────────────────────────┘

(filter bar — same as SUCCESS, populated with the params just run)

┌──────────────────────────────────────────────────────────────────┐
│                                                                   │
│  No outstanding receivables.                                      │
│  All customers are current as of 2026-05-22.                      │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

Tokens (gray-friendly, **not** warning-colored):
- Container: `rounded-lg border border-gray-200 bg-white p-8 text-center`
- Headline: `text-base font-medium text-gray-900`
- Sub-copy: `text-sm text-gray-600 mt-1`
- No icon (avoids implying alarm); no CTA (there is nothing to do).

This is the **good** empty — a portfolio with zero open AR is a win, not a problem. The visual should celebrate, not warn.

### 8. ERROR

```
┌──────────────────────────────────────────────────────────────────┐
│  ⚠ Couldn't run the AR aging report                              │
│                                                                   │
│  The report service didn't respond. Try again in a moment.       │
│                                                                   │
│  ┌─────────┐                                                      │
│  │ Retry   │                                                      │
│  └─────────┘                                                      │
└──────────────────────────────────────────────────────────────────┘
```

Standard `<ErrorSurface variant="retryable">` (the cohort-3 PR 1 primitive). Red border / red-50 background / red-700 text. `Retry` re-fires the same submitted params (does not re-read the form).

## State machine summary

This page uses [pattern-012 run-on-demand](./run-on-demand-pattern.md) **as-is, with no deviations**. The state machine:

```
IDLE → READY_TO_RUN → LOADING → SUCCESS (or ERROR)
                                  │
                                  └── any filter change → IDLE
```

Required params: `chartId` (any non-empty selection). All other params are optional with sensible defaults (`asOfDate` defaults to today; `topDelinquentN` defaults to 10). The Run button gates purely on `chartId` presence.

## Provisionality banner placement

This page uses [pattern-011 provisional report surface](./provisionality-banner-pattern.md) **as-is, with no deviations**. Position: below page header, above filter bar, full content-width.

**Note for FED and PAO planning:** AR aging crosses open accounting periods routinely — open invoices, in-flight collections, posted-but-unmatched receipts all live in the open period. Expect `isProvisional === true` to be the **common case** for this page, not the edge case. The banner UX is unchanged, but operationally this means most users will see it most of the time — which is fine; it accurately describes reality.

## CSV export

This page uses [pattern-013 CSV export](./csv-export-pattern.md) **as-is, with no deviations**.

Filename: `ar-aging-summary-{asOfDate}.csv` (e.g., `ar-aging-summary-2026-05-22.csv`).
Provisional suffix: `ar-aging-summary-2026-05-22-provisional.csv` when `isProvisional === true`.

Body convention (cartridge responsibility — out of scope for PAO direction): two CSV sections in one file, "By Customer" then "By Property," with the totals row terminating each section; the `TopDelinquent` block is **not** included in the CSV (it's a UI-only derived view; the data lives in the customer rows already).

## Resolved PAO design direction answers

These answer the four FED-asked questions from the cohort-3 spec (lines 422–427) decisively.

### A. AgingTable column header tints

**Answer:** use the canonical `aging-bucket-header-tint` variants from [`tokens.md`](./tokens.md).

| Column | Background | Text |
|---|---|---|
| `Current` | `bg-gray-50` | `text-gray-700` |
| `0–30 d` | `bg-gray-50` | `text-gray-700` |
| `31–60 d` | `bg-amber-50` | `text-amber-900` |
| `61–90 d` | `bg-orange-50` | `text-orange-900` |
| `90+ d` | `bg-red-50` | `text-red-900` |
| `Total` | `bg-gray-50` | `text-gray-700` |

Reasoning: `Current` and `0–30 d` are both "no-overdue" states — current means not yet due, 0–30 means due-but-not-late (typical 30-day net terms). They share the neutral gray header. Tints intensify with severity: amber for "noticing" (31–60), orange for "concerning" (61–90), red for "delinquent" (90+). This matches the cohort-2 `<DaysDuePill>` palette family but **one shade lighter** — table-column tints want to coexist with cell content, not compete with it (the pill chips themselves use `bg-amber-100` / `bg-orange-100` / `bg-red-100`, one shade darker).

### B. TopDelinquentList — list, not mini-table

**Answer:** render as a **ranked list**, not a mini-table.

Reasoning: at maximum 10 entries with 3 data points each (rank + 90+ amount + total open), a table is overengineered. The list reads faster because the eye scans down the customer names — name is the primary lookup key; the supporting stats are secondary. A mini-table imposes column-thinking on a value set that doesn't need it: customers aren't a grid, they're a queue.

Visual anatomy (also see Wireframe 6 above):
- **Rank chip** `(N)`: 24×24 circular `bg-gray-100` chip, `text-xs font-semibold text-gray-600`. Decorative — the name is the primary label.
- **Customer name**: `text-sm font-medium text-gray-900`, left of the row.
- **90+ amount**: `text-red-700 font-medium tabular-nums` when > 0; `text-gray-400` rendering `"—"` when 0.
- **Total open**: `text-sm text-gray-700 tabular-nums`.
- **Row border**: `border-b border-gray-100`.
- **Heading**: `Top {topDelinquentN} delinquent customers` (echoes the actual N from the user's submitted params, not the API result count).

The header echoes the **submitted** N so the user understands what they asked for; if the server returns fewer than N (because there aren't N delinquents), the list just renders fewer rows. The heading doesn't lie about scope.

### C. ArAgingTotalsBar — tile grid, not inline row

**Answer:** render as a **tile grid** of 6 tiles (5 buckets + 1 Total), not an inline row.

Reasoning: at narrow widths (Surface Pro portrait, mobile), an inline row wraps awkwardly and loses the visual parity of the buckets — half the buckets end up on one line, half on another, with the Total either floating in the middle or hanging off the end. Tiles wrap **cleanly** to 2-col on mobile and 5/6-col on desktop because each tile is a self-contained unit that retains its identity on any line. Use the canonical tile pattern from cohort-2 `AccountingPage` summary tiles (`rounded-lg border border-gray-200 bg-white px-3 py-2`).

Differentiating the Total tile: it gets a `border-l-4 border-blue-500` left edge (replaces the left side of the gray border). This breaks the visual chain of equal buckets and reads as a sum at a glance — the eye sees "5 things, then 1 different thing" without needing to read the labels.

The 6-tile grid (not 5+1 inline-row-with-divider) also makes the responsive collapse predictable: 6 → 3 → 2 columns falls along clean math; the Total tile sits in a deterministic spot at every width.

### D. Empty state copy

**Answer:** "No outstanding receivables." with sub-copy "All customers are current as of {asOfDate}."

Full structure:

```
No outstanding receivables.
All customers are current as of 2026-05-22.
```

Reasoning: this is a **positive** empty state, not a missing-data error. A portfolio with zero open AR is a win — every customer is paid up. The copy needs to communicate that directly without sounding like a warning. The sub-copy echoes the `asOfDate` so the user knows what they ran (this matters because zero AR is striking; the user will want to verify they asked the question they meant to). No CTA — there is literally nothing to do. The visual treatment is gray-friendly (gray-200 border, white background, gray-900 headline, gray-600 sub-copy), **not** warning-colored — amber or red would imply something is wrong.

## AgingTable column design

| Column | Alignment | Format | Notes |
|---|---|---|---|
| Name (Customer or Property) | left | text | `text-sm text-gray-900`; `font-medium` on totals row |
| Current | right | currency | `tabular-nums`; zero → `"—"` (`text-gray-400`) |
| 0–30 | right | currency | same |
| 31–60 | right | currency | same |
| 61–90 | right | currency | same |
| 90+ | right | currency | same |
| Total | right | currency | `tabular-nums`; bold on totals row |

- Header tints per **Q-A** above.
- Section heading above each table (`<h2>By Customer</h2>` / `<h2>By Property</h2>`): `text-lg font-semibold text-gray-900 mt-6 mb-3`.
- Totals row at bottom of each table: bold weight on Name + Total cells, `border-t-2 border-gray-300` above the row.
- Row hover: `hover:bg-gray-50` (cohort-2 baseline for read tables).
- Table container: `rounded-lg border border-gray-200 overflow-hidden`.
- No pagination, no sort UI for MVP — the cohort-3 PR 1 `<DataTable>` candidate from `tokens.md` will eventually subsume sorting and virtual scroll; AR aging is small enough (single-chart portfolios are <500 customers typically) that the unsorted full render is acceptable.

## TopDelinquentList visual

Already covered in Wireframe 6 and PAO answer B. Additional rules:

- When `result.topDelinquent.length === 0` AND `topDelinquentN > 0`: the entire section is hidden (don't render an empty heading + empty list — that's noise).
- When `topDelinquentN === 0` (user explicitly suppressed via the stepper): the section is hidden AND the heading is hidden. The user opted out; the page respects that opt-out completely.
- When `topDelinquentN > 0` AND `result.topDelinquent.length > 0`: render heading + list. If the API returns fewer rows than `topDelinquentN`, the heading still reads "Top {topDelinquentN} delinquent customers" — the heading describes the **ask**, not the result count. (Example: user asks for top 10 but only 4 customers have 90+ balances → heading reads "Top 10 delinquent customers", list shows 4 rows. The 4-vs-10 gap is itself informative — fewer than expected delinquents is good news.)

## Token usage

This page consumes the following canonical tokens (all from [`tokens.md`](./tokens.md); reference by name, not by composition):

| Token | Where used |
|---|---|
| `provisional-surface` | `<ProvisionalityBanner>` (when isProvisional) |
| `aging-bucket-header-tint` (31–60 / 61–90 / 90+ variants) | `<AgingTable>` thead column tints |
| ArAgingTotalsBar tile composition (existing cohort-2 tile pattern) | TotalsBar tiles |
| `text-red-700 font-medium tabular-nums` (existing) | TopDelinquentList 90+ amount |
| Rank-chip composition (existing) | TopDelinquentList rank `(N)` chip |
| Section heading composition (existing) | `<h2>` between sections |

Notably **not** used on this page: `aging-bucket-pill` variants. AR aging cells use **header tints + plain numeric cells** — the pill chips are reserved for surfaces where the bucket label needs to ride alongside a number (e.g., `RentRollPage` UnitTable delinquency column, where each cell is `<pill>Days31To60</pill>` with no parent column to tint). On this page, the column itself **is** the bucket; tinting the header is sufficient and the cell can stay clean.

## Component reuse

From `@sunfish/ui-react` (v0.2):
- `<Card>` — outer page containers if Yeoman opts for a card-wrapped layout (otherwise unwrapped, per cohort-2 baseline for report pages)
- `<CurrencyAmount>` — every cell in `<AgingTable>`, every tile value in `<ArAgingTotalsBar>`, every Total cell in `<TopDelinquentList>`
- `<AgingBucketPill>` — **not used on this page** (see token note above); cohort-3 PR 1 still ships it for RentRollPage and future cohorts

From `apps/web/src/components/` (cohort-3 PR 1 shared infrastructure):
- `<ProvisionalityBanner>`
- `<ExportCsvButton>`
- `<ReportFilterBar>`
- `<ChartSelector>`
- `<RunButton>`
- `<ErrorSurface variant="retryable">`

No new components introduced by this page. Specifically:
- `<ArAgingTotalsBar>` — page-local; thin grid composition; doesn't graduate
- `<AgingTable>` — page-local; thin `<table>` wrapper; will fold into the future `<DataTable>` primitive when that lands (post-cohort-3)
- `<TopDelinquentList>` — page-local; thin list composition; doesn't graduate

This keeps the cohort-3 PR 1 surface area tight: shared infrastructure is exactly the components that two-or-more pages need.

## Accessibility

- Section headings (`<h2>By Customer</h2>`, `<h2>By Property</h2>`, `<h2>Top {N} delinquent customers</h2>`) establish a clear document outline so screen readers and keyboard-skip-link users can navigate sections without traversing every table row.
- TopDelinquentList rank chip `(N)` is **decorative**: rendered with `aria-hidden="true"`; the customer name is the primary accessible label for the row.
- Color is **never** the sole signal for the aging buckets — header text (`"31–60 d"`, `"90+ d"`) carries the meaning; the tint is supplemental for sighted users scanning. Users with color-vision differences read the same data without loss.
- TotalsBar tile values use `tabular-nums` + right-aligned for visual rhythm and screen-reader parity (the order of value announcement matches the visual order).
- Empty state heading: `<h2>No outstanding receivables.</h2>` — semantic heading even for the positive state, so it appears in the document outline.
- Error state: `<ErrorSurface>` sets `role="alert"` per cohort-3 PR 1 primitive; on appearance it announces to screen-reader users without interrupting in-progress speech (the surface itself uses `aria-live="polite"`; `role="alert"` is for the headline).
- Filter bar inputs: all required inputs get `aria-required="true"` + visible `*` in label; the Run button is the form's `<button type="submit">` per pattern-012.

## States summary table

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| IDLE | mount; or any filter change after SUCCESS | Filter bar shown; no result area | Run button disabled until `chartId` set |
| READY_TO_RUN | `chartId` non-empty; no submitted params yet | Filter bar shown; no result area | Run button enabled |
| LOADING | mutation in flight | TotalsBar skeleton + 2 × table-row skeletons + (suppressed) TopDelinquentList | Run button → `Running…` + `aria-busy="true"`; Export disabled |
| SUCCESS | result returned; `!isProvisional` | TotalsBar + 2 tables + (optional) TopDelinquentList | Run re-enabled for fresh fire; Export enabled |
| SUCCESS + provisional | result returned; `isProvisional === true` | ProvisionalityBanner above filter bar + standard SUCCESS layout | same as SUCCESS |
| EMPTY (positive) | `result.totals.totalOpen === 0` | "No outstanding receivables." panel + sub-copy with asOfDate | no CTA; user can re-run with different params |
| ERROR | mutation rejected | `<ErrorSurface>` red surface | Retry button (re-fires same submitted params) |

The `EMPTY` state is triggered by the **aggregate total being zero**, not by `byCustomer.length === 0`. The cartridge may return rows with all-zero balances when `includeZeroBalanceAccounts` semantics apply elsewhere; the empty signal that matters for AR aging is "no open money anywhere," which is `totals.totalOpen === 0`.

## Pattern alignment

This page exercises one standing pattern + three candidates:

- **`@standing-pattern: pattern-009`** (Bridge endpoint + frontend rebind pair, formal post-cohort-1) — the page reads `POST /api/v1/reports/ar-aging` and rebinds the typed result envelope to UI. This is the baseline pattern; every cohort-3 PR carries it.
- **`@candidate-pattern: pattern-011`** (provisional report surface) — the page surfaces `<ProvisionalityBanner>` when `result.isProvisional === true`. AR aging is expected to be provisional in the common case (open-period transactions are the norm for receivables); this is the highest-frequency exerciser of pattern-011 in cohort-3.
- **`@candidate-pattern: pattern-012`** (run-on-demand report) — the page implements the IDLE → READY_TO_RUN → LOADING → SUCCESS state machine with no auto-fetch on mount; the user explicitly clicks Run.
- **`@candidate-pattern: pattern-013`** (CSV export affordance) — the page surfaces `<ExportCsvButton>` adjacent to Run, disabled until SUCCESS, with canonical filename `ar-aging-summary-{asOfDate}.csv` (`-provisional` suffix when applicable).

All four pattern claims live in the **PR description**, not in commit bodies — per fleet commitlint trap (the `@standing-pattern:` line at start of a commit body line trips the footer parser).

— PAO, 2026-05-22
