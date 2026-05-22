# ProfitAndLossByPropertyPage — Full Rewrite

**Page:** `sunfish/apps/web/src/pages/ProfitAndLossByPropertyPage.tsx`
**PR:** W#77 PR 3
**Cartridge:** `ProfitAndLossByProperty` (`shipyard/packages/blocks-reports/`)
**Endpoint:** `POST /api/v1/reports/profit-loss`
**Patterns:** `@standing-pattern: pattern-009` + `@candidate-pattern: pattern-011, pattern-012, pattern-013`
**Replaces:** `PLReport.tsx` — retired in this PR. No deprecation shim. The `/reports/profit-loss` route is rebound to the new component file; the old file is deleted in the same PR.

## Scope

PLReport.tsx is replaced wholesale, not incrementally. The flat income/expense line list becomes a per-property collapsible accordion view; the ERPNext direct GL fetch becomes a cartridge-backed `POST /api/v1/reports/profit-loss` request returning a typed `ReportRunResult<PnlResult>` envelope; the 3-option period enum (Month / Quarter / Year-to-date) becomes a free-form date-range picker; and the auto-on-mount fetch becomes a run-on-demand pattern with first-class provisionality. The cohort-2 3-tile summary visual is preserved (revenue / expenses / net income) but now reads from `result.totals` rather than per-property aggregation.

## What changes from PLReport.tsx

| Aspect | Old (PLReport.tsx) | New (ProfitAndLossByPropertyPage.tsx) |
|---|---|---|
| Data source | ERPNext direct: `GET /api/method/sunfish.api.get_profit_loss` | Cartridge-backed: `POST /api/v1/reports/profit-loss` returning `ReportRunResult<PnlResult>` |
| Layout | Flat list — one Income table + one Expense table covering all properties together | Per-property accordion list; each property has its own collapsible Revenue / Expense sections |
| Property filter | Single-property dropdown OR "All properties" | `PropertyMultiSelect` — empty = all; N selected = N filtered |
| Period control | 3-option enum: `month` / `quarter` / `year` (year-to-date) | Free-form `DateRangePicker` — optional Period Start + Period End; empty = chart history |
| Auto-run | Yes — fetches on every keystroke / dropdown change | No — IDLE → READY_TO_RUN → user clicks Run (per [`run-on-demand-pattern.md`](./run-on-demand-pattern.md)) |
| Provisionality | Not surfaced (old API didn't expose it) | First-class — amber banner on `isProvisional === true` (per [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md)) |
| Zero-balance accounts | Always included | `IncludeZeroBalance` toggle, default OFF |
| Summary tiles | 3-tile (Income / Expenses / Net) — kept | 3-tile (Revenue / Expenses / Net Income) — visual preserved, copy harmonized to canonical financial vocabulary, source switched to `result.totals` |
| Export CSV | Page-header button, ERPNext re-fetch | Canonical `<ExportCsvButton>` in filter bar (per [`csv-export-pattern.md`](./csv-export-pattern.md)) |
| Loading state | Plain "Loading…" text | 3-row skeleton accordion |
| Error state | Plain red text line | Canonical error surface card + Retry button |
| Empty state | (Implicit — empty tables) | Explicit "No activity in this period and chart." copy |

The migration is **not field-compatible** with the old file. There is no shared helper to extract; the rewrite is cleaner authored from scratch against the new wire types.

## Component hierarchy

```
ProfitAndLossByPropertyPage
  ReportFilterBar
    ChartSelector            (required)
    DateRangePicker          (optional; periodStart + periodEnd as a pair)
    PropertyMultiSelect      (optional; empty = "All properties")
    ToggleFilter             ("Include zero-balance accounts"; default OFF)
    RunButton
    ExportCsvButton          (disabled until result present)
  ProvisionalityBanner       (visible only when result.isProvisional === true)
  [IDLE state]               → empty content area; nothing rendered below banner slot
  [LOADING state]            → SkeletonAccordion ×3
  [EMPTY state]              → "No activity in this period and chart." (centered, gray-500)
  [ERROR state]              → ErrorSurface card + Retry
  [SUCCESS state]
    PortfolioSummaryTiles
      Tile (Revenue)         | text-green-700
      Tile (Expenses)        | text-red-700
      Tile (Net Income)      | conditional color: green / red / gray
    PropertyAccordionList
      PropertyAccordion ×N
        AccordionHeader      → PropertyName | Revenue | Expenses | Net Income | chevron
        AccordionBody (when expanded)
          RevenueSection     → "Revenue" heading (green accent) + account lines
          ExpenseSection     → "Expenses" heading (red accent) + account lines
```

The new components introduced in PR 1 (`<ChartSelector>`, `<DateRangePicker>`, `<PropertyMultiSelect>`, `<ProvisionalityBanner>`, `<ExportCsvButton>`, `<RunButton>`, `<ReportFilterBar>`) are shared across all 4 cohort-3 pages. `<PortfolioSummaryTiles>` and `<PropertyAccordion>` are **page-local** — they don't appear elsewhere in cohort-3 (RentRoll uses property blocks with unit tables, not collapsible accordions).

## Wireframe specs

### Idle / ready-to-run state

```
+--------------------------------------------------------------------+
|  Profit & Loss by Property                                         |
|  Run on demand against any chart of accounts                       |
+--------------------------------------------------------------------+

+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]    Period: [2026-01-01] → [2026-05-22]|
| Properties: [All properties ▾]                                     |
| [☐ Include zero-balance accounts]                                  |
|                                                                    |
|                                          [Run report]  [Export CSV]|
+--------------------------------------------------------------------+

(content area empty; no skeleton — IDLE is the page's "waiting for you" rest state)
```

### Loading state — skeleton accordion

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]    Period: [2026-01-01] → [2026-05-22]|
| Properties: [All properties ▾]                                     |
| [☐ Include zero-balance accounts]                                  |
|                                                                    |
|                                       [Running…]   [Export CSV] ⤓ |
+--------------------------------------------------------------------+

+--------------------------------------------------------------------+
|  ▓▓▓▓▓▓▓▓▓▓▓        ▓▓▓▓▓▓     ▓▓▓▓▓▓     ▓▓▓▓▓▓        ▒          |
+--------------------------------------------------------------------+
|  ▓▓▓▓▓▓▓▓▓▓▓        ▓▓▓▓▓▓     ▓▓▓▓▓▓     ▓▓▓▓▓▓        ▒          |
+--------------------------------------------------------------------+
|  ▓▓▓▓▓▓▓▓▓▓▓        ▓▓▓▓▓▓     ▓▓▓▓▓▓     ▓▓▓▓▓▓        ▒          |
+--------------------------------------------------------------------+
```

Three skeleton accordion rows (`bg-gray-100 animate-pulse h-14`). Summary tiles are NOT shown during loading — the tile row also gets 3 skeleton tiles to reserve layout. No spinner overlay; the skeleton is the loading signal.

### Success state — full layout (multi-property, one expanded)

```
+--------------------------------------------------------------------+
|  Profit & Loss by Property                                         |
|  Run on demand against any chart of accounts                       |
+--------------------------------------------------------------------+

+--------------------------------------------------------------------+
| ⚠ This report covers an open accounting period and may change as   |
|   transactions are posted.                          [Show details ▾]|
+--------------------------------------------------------------------+

+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]    Period: [2026-01-01] → [2026-05-22]|
| Properties: [All properties ▾]                                     |
| [☐ Include zero-balance accounts]                                  |
|                                                                    |
|                                          [Run report]  [Export CSV]|
+--------------------------------------------------------------------+

+----------------------+ +----------------------+ +----------------------+
| REVENUE              | | EXPENSES             | | NET INCOME           |
| $124,500.00          | | $78,200.00           | | $46,300.00           |
| Jan 1 – May 22, 2026 | | Jan 1 – May 22, 2026 | | Jan 1 – May 22, 2026 |
+----------------------+ +----------------------+ +----------------------+

+--------------------------------------------------------------------+
| ▸  150 Lexington Ct          $48,200   $30,100   $18,100           |
+--------------------------------------------------------------------+
| ▾  220 Madison Pl            $52,300   $34,400   $17,900           |
+========================================================------------+   <-- expanded
|                                                                    |
|    ┃ Revenue                                                       |
|      4100  Rent Revenue                              $42,000.00    |
|      4200  Late Fee Income                            $1,800.00    |
|      4300  Other Property Income                      $8,500.00    |
|                                                                    |
|    ┃ Expenses                                                      |
|      5100  Maintenance & Repairs                     $12,400.00    |
|      5200  Utilities                                  $4,200.00    |
|      5300  Property Management                        $9,600.00    |
|      5400  Insurance                                  $3,200.00    |
|      5500  Property Tax                               $5,000.00    |
|                                                                    |
+--------------------------------------------------------------------+
| ▸  88 Riverside Dr           $24,000   $13,700   $10,300           |
+--------------------------------------------------------------------+
| ▸  Unassigned                  $0      $0           $0    (italic) |
+--------------------------------------------------------------------+
```

The expanded property has its header background changed to `bg-gray-50` and a `border-b-2 border-blue-500` bottom border — the blue stripe visually anchors the expanded body. Revenue and Expense section headings get a vertical accent bar (`border-l-2 border-green-500` / `border-l-2 border-red-500`) to color-code the section without coloring the heading text itself.

### PropertyAccordion — collapsed state

```
+--------------------------------------------------------------------+
| ▸  150 Lexington Ct          $48,200    $30,100    $18,100         |
+--------------------------------------------------------------------+
   ↑                              ↑          ↑          ↑
   chevron right                Revenue   Expenses   Net Income
   text-gray-400                                     (green if + , red if –)
```

Header row: `flex items-center justify-between px-4 py-3 border-b border-gray-200 hover:bg-gray-50 cursor-pointer`. Property name on the left (`text-base font-medium text-gray-900`). Three right-aligned numeric columns: Revenue (`text-green-700`), Expenses (`text-red-700`), Net Income (green / red / gray per sign). Chevron right (`ChevronRightIcon w-4 h-4 text-gray-400`) on the far left, just before the property name.

The whole header row is the click target (single accordion-toggle button); not just the chevron.

### PropertyAccordion — expanded state

```
+--------------------------------------------------------------------+
| ▾  220 Madison Pl            $52,300   $34,400   $17,900           |
+========================================================------------+
|                                                                    |
|    ┃ Revenue                                                       |
|      4100  Rent Revenue                              $42,000.00    |
|      4200  Late Fee Income                            $1,800.00    |
|      4300  Other Property Income                      $8,500.00    |
|                                                                    |
|    ┃ Expenses                                                      |
|      5100  Maintenance & Repairs                     $12,400.00    |
|      5200  Utilities                                  $4,200.00    |
|      5300  Property Management                        $9,600.00    |
|      5400  Insurance                                  $3,200.00    |
|      5500  Property Tax                               $5,000.00    |
|                                                                    |
+--------------------------------------------------------------------+
```

Header background: `bg-gray-50`. Bottom border: `border-b-2 border-blue-500` (the load-bearing visual anchor — the blue stripe says "the section below belongs to this row"). Chevron rotates from right to down (`ChevronDownIcon w-4 h-4 text-gray-600` — also darkens one shade vs the collapsed gray-400).

Body container: `bg-white px-4 py-3 border-b border-gray-200`. Revenue and Expense sections each get a `pl-6` indent on their content lines (the `┃` glyph in the wireframe represents `border-l-2 border-{green|red}-500 pl-2` on the section heading). Account lines: `flex items-center justify-between text-sm py-1` with `Code  Name` on the left and `<CurrencyAmount>` on the right.

### Empty state

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]    Period: [2024-01-01] → [2024-12-31]|
| Properties: [88 Riverside Dr ▾]                                    |
|                                                                    |
|                                          [Run report]  [Export CSV]|
+--------------------------------------------------------------------+

+--------------------------------------------------------------------+
|                                                                    |
|                                                                    |
|              No activity in this period and chart.                 |
|                                                                    |
|              Adjust the filters above and run again.               |
|                                                                    |
|                                                                    |
+--------------------------------------------------------------------+
```

Centered (`text-center text-gray-500 py-12`). Two-line copy: a stated fact ("No activity…") + a recovery hint ("Adjust the filters above and run again."). No empty illustration; no CTA button (the user already has the controls visible above).

### Error state

```
+--------------------------------------------------------------------+
| ⚠ Couldn't run profit & loss report                                |
|                                                                    |
| The report service didn't respond. Try again in a moment.          |
|                                                                    |
| [Retry]                                                            |
+--------------------------------------------------------------------+
```

`border border-red-200 bg-red-50 text-red-700 rounded-lg p-4`. Retry button re-issues the same `submittedParams` mutation. The provisionality banner is hidden during ERROR (the error surface owns the screen, per pattern-011).

### PortfolioSummaryTiles — positive Net Income

```
+----------------------+ +----------------------+ +----------------------+
| REVENUE              | | EXPENSES             | | NET INCOME           |
| $124,500.00          | | $78,200.00           | | $46,300.00           |
| Jan 1 – May 22, 2026 | | Jan 1 – May 22, 2026 | | Jan 1 – May 22, 2026 |
+----------------------+ +----------------------+ +----------------------+
   text-green-700           text-red-700            text-green-700
```

Layout: `grid grid-cols-1 sm:grid-cols-3 gap-4`. Tile: `rounded-lg border border-gray-200 bg-white px-4 py-3`. Label: `text-xs uppercase tracking-wide text-gray-500`. Value: `text-2xl font-semibold tabular-nums`. Subtitle (period range): `text-xs text-gray-500 mt-1`.

### PortfolioSummaryTiles — negative Net Income

```
+----------------------+ +----------------------+ +----------------------+
| REVENUE              | | EXPENSES             | | NET INCOME           |
| $32,400.00           | | $48,900.00           | | ($16,500.00)         |
| Jan 1 – May 22, 2026 | | Jan 1 – May 22, 2026 | | Jan 1 – May 22, 2026 |
+----------------------+ +----------------------+ +----------------------+
   text-green-700           text-red-700            text-red-700
```

Net Income tile switches to `text-red-700` and the value displays as `($16,500.00)` with parentheses (accounting-convention for negatives). `<CurrencyAmount>` from `@sunfish/ui-react` handles the sign-to-parentheses transformation built-in. Revenue and Expenses remain their canonical green / red even when one is much larger than the other; the Net Income tile carries the loss signal.

## State machine summary

See [`run-on-demand-pattern.md`](./run-on-demand-pattern.md) for the canonical machine. This page implements it with `PnlByPropertyParams` as the param shape. The required parameter is `chartId`; everything else (periodStart, periodEnd, propertyIds, includeZeroBalanceAccounts) is optional. The Run button is enabled the moment `chartId` is set on the form; changing any other filter resets `submittedParams` to `null` (clears result, returns to IDLE).

## Provisionality banner placement

See [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md) for the canonical UX. P&L is the **common case** for this page: the most recent month is almost always open, so most runs covering the current period return `isProvisional: true`. The banner sits below the page H1 + subtitle and above the filter bar, matching the canonical position.

Two specific provisionality patterns to expect on this page:

- A `periodEnd` in the current open month → `isProvisional: true` with warning naming the open fiscal period.
- An empty `periodEnd` (chart-history default) reaching into the current month → same.

Users should expect the banner to be visible most of the time on this page. The hidden case is historical analysis: `periodStart` + `periodEnd` both in fully-closed months returns `isProvisional: false`.

## CSV export

See [`csv-export-pattern.md`](./csv-export-pattern.md) for the canonical button visual + endpoint contract.

**Filename:** `pnl-by-property-{periodStart}-to-{periodEnd}.csv`. Example: `pnl-by-property-2026-01-01-to-2026-05-22.csv`.

**Empty-period handling:** if the user runs with an empty `DateRangePicker` (defaulted to chart history), FED reads `result.periodStart` and `result.periodEnd` from the cartridge response — the cartridge always populates these with the actual range used, so the filename always has concrete dates.

**Provisional suffix:** when `isProvisional === true`, append `-provisional` before `.csv`: `pnl-by-property-2026-01-01-to-2026-05-22-provisional.csv`.

## Resolved PAO design direction answers

The FED spec (line 495–500) requested decisions on four items. Each is resolved here.

### A. PropertyAccordion visual design — chevron + expanded border

| Element | Collapsed | Expanded |
|---|---|---|
| Chevron icon | `ChevronRightIcon w-4 h-4 text-gray-400` | `ChevronDownIcon w-4 h-4 text-gray-600` |
| Header background | (default white) | `bg-gray-50` |
| Header bottom border | `border-b border-gray-200` | `border-b-2 border-blue-500` |
| Header hover | `hover:bg-gray-50` | (no hover state; already expanded) |
| Cursor | `cursor-pointer` on the full header row | `cursor-pointer` (clicking again collapses) |
| Body indent | n/a | Revenue / Expense section headings use `pl-6` indent on the content lines |

The chevron + the blue bottom border are **the two compounding signals** that the row is expanded. Color alone isn't the signal (per a11y guideline below); the chevron direction conveys state to users who don't perceive the blue stripe.

The blue stripe is `border-b-2` (heavier than the default 1px gray) so it reads as "this row owns the section below" rather than "ordinary divider." Don't use a left-border-anchor on the body — the blue header bottom-border is the correct anchor visual.

### B. PortfolioSummaryTiles responsive — keep 3-tile at all breakpoints down to `sm:`

**Decision:** `grid grid-cols-1 sm:grid-cols-3 gap-4`. Three tiles across at `sm:` (≥640px) and wider; single column stacked at narrower viewports.

Reasoning: three tiles at ~360px (small phone portrait) gives each tile ~107px of width minus padding — the dollar value at `text-2xl` doesn't fit cleanly. Stacking to 1-column on phone is the cleaner read and trades nothing — phone is a glance medium for P&L, not a working surface. Surface Pro portrait (912px wide) is well above `sm:` so the common case is unchanged from 3-across; Surface Pro landscape (1366px) trivially fits 3-across.

Don't go to `md:grid-cols-3` (≥768px) — that gates 3-across behind tablet landscape and pushes 7"-tablet portrait into single-column unnecessarily. `sm:` is the right breakpoint.

### C. "Unassigned" property row treatment — DE-EMPHASIZE

**Decision:** render the row in `text-gray-500 italic`. Sort it LAST in the accordion list (always at the bottom, regardless of where alphabetical order would place it). Keep the chevron functional — the user can still expand to see what's in the "Unassigned" bucket.

Reasoning: "Unassigned" is an **accounting tail** — entries that didn't get a property assignment, often because of a data-entry oversight or because they're genuinely portfolio-level (e.g., entity-level insurance not allocated to a specific building). It is not a real property and shouldn't compete visually with named properties for the user's attention.

Three things this answer rules out, deliberately:

- **Don't hide it.** The user needs to know unassigned exists and how much money is in it; surfacing the bucket prompts the right "should I reassign these?" question.
- **Don't elevate it.** It is the bookkeeping leftover, not a peer entity. Visual emphasis would invite users to mistake it for a property.
- **Don't sort it alphabetically.** "U" lands in the middle of a multi-property list; bookend-bottom placement signals "this is the tail" structurally.

When `byProperty` contains no "Unassigned" entry (the cartridge is responsible for emitting it only when there are actually unassigned lines), the row is simply absent — no placeholder.

### D. Net Income tile — copy + color rule

**Copy:** `Net Income`.

Rejected alternative: `Net Profit/Loss`. Reasoning: "Net Profit/Loss" is bookkeeper-ese — the slash signals "one of these depending on sign" but reads as compound jargon to non-accountant users. "Net Income" is the standard financial-report label that already covers both signs (a negative net income is a loss; the term doesn't need both words). It matches the canonical line-item label in standard income-statement presentations and matches the cartridge's own field name `netIncome`.

**Color rule (computed at render time from the tile value):**

| Sign | Color token |
|---|---|
| Positive (value > 0) | `text-green-700` |
| Negative (value < 0) | `text-red-700` |
| Exactly zero (value === 0) | `text-gray-700` |

**Display format:** `<CurrencyAmount>` from `@sunfish/ui-react` handles the sign-to-parentheses transformation. Positive: `$1,234.56`. Negative: `($1,234.56)`. Zero: `$0.00`. The primitive's `aria-label` provides the spoken form ("negative one thousand two hundred thirty four dollars and fifty six cents") for screen readers.

**Subtitle:** below the value, render the period range in `text-xs text-gray-500` — example: `Jan 1 – May 22, 2026`. The subtitle is on all three tiles (Revenue, Expenses, Net Income) so they read as a triplet; it provides the temporal anchor without re-stating the chart name (the chart is already in the filter bar above).

## PortfolioSummaryTiles design summary

| Tile | Source | Value color | Subtitle |
|---|---|---|---|
| Revenue | `result.totals.totalRevenue` | `text-green-700` | period range |
| Expenses | `result.totals.totalExpenses` | `text-red-700` | period range |
| Net Income | `result.totals.netIncome` | conditional (Q-D above) | period range |

All three tiles use `<Card>` from `@sunfish/ui-react` (or the equivalent token combination `rounded-lg border border-gray-200 bg-white px-4 py-3` if the Card primitive isn't preferred for this lightweight surface — FED picks). Value: `text-2xl font-semibold tabular-nums`. Label: `text-xs uppercase tracking-wide text-gray-500`.

## PropertyAccordion expanded design summary

Revenue section heading: `Revenue` rendered as `text-sm font-medium text-gray-700` with a green-500 left-border accent (`border-l-2 border-green-500 pl-2`). Expenses section heading: same shape with red-500.

Account lines within each section: `flex items-center justify-between text-sm py-1`. Left side: `<span class="text-gray-600 tabular-nums mr-3">{accountCode}</span><span class="text-gray-900">{accountName}</span>`. Right side: `<CurrencyAmount value={line.amount}>` with `text-green-700 tabular-nums` (revenue) or `text-red-700 tabular-nums` (expense).

Don't render section subtotals inside the expanded body — the totals already live in the collapsed-header summary columns; restating them inside is redundant. The body is for the per-account breakdown only.

## Auto-expand behavior

When `result.byProperty.length === 1`, the page auto-expands that single property on success. Reasoning: with one property there is nothing to choose between; the user clearly wants to see the lines. Auto-expanding saves a click and matches the form-defaulting principle from the cohort-2 baseline.

For multi-property results (`byProperty.length >= 2`), all accordions are collapsed by default. The user opens the properties they care about. This keeps the page scannable on the common case (multi-property portfolio) where the user is comparing top-line numbers across properties before drilling into any one.

The auto-expand fires once on the initial render of a new `SUCCESS` state. If the user then collapses the single property, re-running with a new filter that still returns a single property will re-auto-expand (the auto-expand is keyed off "this is a fresh result," not "this user has never collapsed this property").

## Token usage

See [`tokens.md`](./tokens.md) for the canonical inventory. This page introduces no new canonical tokens beyond what `pattern-011` adds (`provisional-surface`). All other tokens are existing:

- `<CurrencyAmount>` color tokens (`text-green-700` / `text-red-700` / `text-gray-700`)
- Card / tile shell (`rounded-lg border border-gray-200 bg-white px-4 py-3`)
- Accordion header tokens (collapsed: `hover:bg-gray-50 cursor-pointer border-b border-gray-200`; expanded: `bg-gray-50 border-b-2 border-blue-500`)
- Chevron icons (`ChevronRightIcon` / `ChevronDownIcon` from Heroicons outline)
- Section accent bars (`border-l-2 border-green-500 pl-2` / `border-l-2 border-red-500 pl-2`)
- De-emphasized "Unassigned" row (`text-gray-500 italic`)

## Component reuse

From `@sunfish/ui-react` (v0.2+, shipyard#48):

- `<Card>` — tile shells, error surface card
- `<CurrencyAmount>` — every money value, sign-to-parentheses + a11y label built-in

From `apps/web/src/components/` (most authored in cohort-3 PR 1, shared across all 4 report pages):

- `<ReportFilterBar>` — the filter wrapper (PR 1)
- `<ChartSelector>` — required-filter dropdown (PR 1)
- `<DateRangePicker>` — period start + period end pair (PR 1)
- `<PropertyMultiSelect>` — optional property filter (PR 1)
- `<RunButton>` — primary action (PR 1)
- `<ExportCsvButton>` — secondary action (PR 1)
- `<ProvisionalityBanner>` — amber banner surface (PR 1)
- `<ErrorSurface variant="retryable">` — error card + Retry (PR 1, per [`tokens.md`](./tokens.md) gap-candidates section)

Page-local components (this PR only):

- `<PortfolioSummaryTiles>` — 3-tile derived from `result.totals`
- `<PropertyAccordion>` — collapsible per-property row + body
- `<PropertyAccordionList>` — orchestrates the accordion array, applies "Unassigned at end" sort

`<PropertyAccordion>` is intentionally kept page-local rather than promoted to shared. RentRollPage uses **property blocks with unit tables**, not collapsible accordions — the visual vocabulary diverges. If a future cohort adds a second collapsible-property report, that's the right time to extract a shared `<PropertyAccordion>`.

## Accessibility

**Accordion semantics:**

- Header row uses `<button aria-expanded={open} aria-controls={bodyId} class="...full header row tokens...">` — the whole header is the button (semantic + visual click target).
- Body uses `<div id={bodyId} role="region" aria-label={`${propertyName} revenue and expenses`} hidden={!open}>`.
- The `hidden` attribute is what controls visibility (not `display: none` in CSS) — `hidden` is announced correctly by screen readers and gets the right semantics out of the box.

**Keyboard:**

- `Tab` lands on each accordion header in DOM order.
- `Space` and `Enter` both toggle (native `<button>` behavior).
- After expansion, `Tab` moves focus into the body; the body contains no interactive elements in cohort-3 (just text and currency), so `Tab` proceeds to the next accordion header.

**Color is not the sole signal:**

- Revenue / Expense section accent bars are paired with the textual section headings ("Revenue", "Expenses") and the sign of the amounts — users who don't perceive the green / red accent still read the section name and the dollar sign.
- Net Income tile color is paired with the parenthesis-wrapping convention (`($1,234.56)` for negative) — color isn't the only loss signal.

**Currency a11y:**

- `<CurrencyAmount>` from `@sunfish/ui-react` provides built-in `aria-label` with the full spoken form: negative values read as "negative one thousand two hundred thirty four dollars and fifty six cents" rather than "left-paren dollar one thousand…"
- Tabular-nums (`tabular-nums`) is purely visual (column alignment); it doesn't affect screen-reader output.

**De-emphasized "Unassigned" row:**

- Italic + gray-500 is a visual treatment; the row is fully interactive (same `aria-expanded` semantics, same Tab order — last). Screen readers announce the property name "Unassigned" identically to named properties.

## States summary table

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

## Pattern alignment

- `@standing-pattern: pattern-009` (Bridge endpoint + frontend rebind pair) — formal post-cohort-1; applies to this PR as a write-coupled-to-read substrate change (cartridge replaces ERPNext direct fetch). The `POST /api/v1/reports/profit-loss` endpoint is the new bridge surface; the page is its frontend rebind.
- `@candidate-pattern: pattern-011-provisional-report-surface` — first instance, via `<ProvisionalityBanner>` consumed in this page. P&L will be the most-frequently-provisional report (the most recent month is usually open).
- `@candidate-pattern: pattern-012-run-on-demand-report` — first instance, via the IDLE → READY_TO_RUN → LOADING → SUCCESS / ERROR machine.
- `@candidate-pattern: pattern-013-csv-export-affordance` — first instance, via `<ExportCsvButton>` adjacent to Run in the filter bar.

All three candidates carry forward into PRs 2, 4, 5 (RentRoll, TrialBalance, ArAging) — they ratify together if cohort-4 picks them up consistently.

— PAO, 2026-05-22
