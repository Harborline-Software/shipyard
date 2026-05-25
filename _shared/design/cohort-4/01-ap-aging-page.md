# ApAgingPage — Design Direction

**Page:** `sunfish/apps/web/src/pages/ApAgingPage.tsx` (new — no prior file replaces it)
**PR:** cohort-4 PR N (TBD; substrate not yet shipped)
**Cartridge:** `ApAgingSummary` (`shipyard/packages/blocks-reports/`; W#72 substrate v2 — not yet shipped)
**Endpoint:** `POST /api/v1/reports/ap-aging` (Bridge — not yet shipped)
**Patterns:** standing pattern-009 + ratification candidates pattern-015, pattern-016,
pattern-017 (cohort-3 candidates; AP Aging is the second-instance ratification trigger)

## Status

**PRE-SCOPE direction.** Substrate cartridge + Bridge endpoint not yet shipped. This
document captures PAO design direction so FED + Yeoman can read it standalone when
cohort-4 substrate arrives. Direction is **mirror-of-AR-Aging-with-deltas**; structural
shape is settled; per-cartridge wire-type specifics await contract-freeze.

## Scope

`ApAgingPage` is a new run-on-demand report page that surfaces accounts-payable aging
across a chart of accounts as of a chosen date. It renders two parallel breakdowns —
**By Vendor** and **By Expense Category** — so the user can answer "who do we owe?"
and "where is the spend concentrated?" in a single run, plus a focused **Top N
outstanding-balance vendors** list pinned below for AP triage. There is no prior
`ApAging.tsx` to migrate; this page is born aligned with the cohort-3 substrate
(`ReportRunResult<TResult>` envelope, IDLE → READY_TO_RUN state machine,
`<ProvisionalityBanner>`, `<ExportCsvButton>`).

## Component hierarchy

```
ApAgingPage
  PageHeader
    <h1>AP Aging</h1>
    <p class="subtitle">Open payables by vendor and by expense category as of a chosen date</p>
  ReportFilterBar
    ChartSelector (required)
    AsOfDatePicker (optional; defaults to today)
    TopNSelector ("Show top N outstanding vendors" — number stepper; default 10, min 0, max 100; 0 hides the section)
    RunButton
    ExportCsvButton (disabled until result present)
  ProvisionalityBanner (when result.isProvisional)
  [LOADING]   → SkeletonTotalsBar + SkeletonRows ×5 (twice — once per section)
  [EMPTY]     → "No outstanding payables." panel (positive empty)
  [ERROR]     → red surface + Retry
  [SUCCESS]
    ApAgingTotalsBar               — portfolio summary tiles (6 tiles: 5 buckets + Total)
    ApAgingSection (By Vendor)
      <h2>By Vendor</h2>
      AgingTable (rows = result.byVendor)
    ApAgingSection (By Category)
      <h2>By Expense Category</h2>
      AgingTable (rows = result.byCategory)
    TopOutstandingList (when topVendorsN > 0 AND result.topOutstanding.length > 0)
      <h2>Top {topVendorsN} outstanding-balance vendors</h2>
      ranked list (max N entries)
```

The hierarchy is **structurally identical** to AR Aging — same shared infrastructure
components from cohort-3 PR 1, same two-table layout, same Top N list, same provisional
banner placement. The deltas are entirely in the cells' content and copy register, NOT
in the structure.

## Money-direction signal (PAO ruling — resolves PRE-SCOPE Q1)

AR and AP pages must be visually distinguishable at a glance so a user mid-scan doesn't
mis-read receivables as payables (or vice versa). **PAO ruling: same aging-bucket
palette on both pages; the page-level differentiator is the H1 + subtitle copy + an
icon-less inline status pill in the page header that reads `Payables` (AP) or
`Receivables` (AR) at `text-xs uppercase tracking-wide` in a neutral gray pill.**

Reasoning: the aging-bucket palette is a **severity signal** (amber/orange/red intensify
with overdue age). Money direction is **orthogonal** to severity — overdue is bad in
both directions. Inverting the palette on AP (e.g., AP-orange / AR-red) would force the
user to learn two color systems for the same semantic information. The page-header pill
gives money direction at the page-orientation moment; once the user has oriented, the
aging buckets read identically.

The alternative considered (and rejected) was using a distinct color accent — orange for
AP, red for AR — on the page H1 underline or a left-edge accent on the totals bar. The
rejection reason: that approach **steals the aging-bucket color slot** from the table
headers, where severity already lives. Two color systems competing for the same retinal
register defeats the purpose.

Wireframe location of the page-header pill (final SUCCESS state):

```
┌──────────────────────────────────────────────────────────────────┐
│  AP Aging                                                 ⟪ AP ⟫ │
│  Open payables by vendor and by expense category                 │
└──────────────────────────────────────────────────────────────────┘
```

`⟪ AP ⟫` shorthand here for `<span class="inline-flex items-center rounded-full
bg-gray-100 px-2.5 py-0.5 text-xs font-medium uppercase tracking-wide text-gray-700">
Payables</span>`. AR Aging gets the same pill with `Receivables` text when AR Aging is
retrofitted at the same cohort window (light backfill, not a separate PR — bundled with
cohort-4 if scope permits).

## Copy register — "outstanding," not "delinquent" (PAO ruling — resolves PRE-SCOPE Q2)

AR Aging uses **"delinquent"** for its Top N list — accurate accounting language
describing customers who haven't paid. **AP Aging does NOT use "delinquent."** Reasoning:
on the AP side, the operator IS the party who owes the money. Calling the operator's own
vendors "delinquent" inverts the moral valence and reads as the operator blaming the
people they haven't paid yet.

**Canonical copy for AP Aging:**

| Surface | AR Aging | AP Aging |
|---|---|---|
| Top N section heading | "Top {N} delinquent customers" | "Top {N} outstanding-balance vendors" |
| TopNSelector label | "Show top N delinquent customers" | "Show top N outstanding vendors" |
| 90+ amount cell label | "90+" (table column header — same as AR) | "90+" (same) |
| Empty state headline | "No outstanding receivables." | "No outstanding payables." |
| Empty state sub-copy | "All customers are current as of {asOfDate}." | "All vendors are paid current as of {asOfDate}." |
| Page H1 + subtitle | "AR Aging / Open receivables by customer and by property" | "AP Aging / Open payables by vendor and by expense category" |

The copy choice "outstanding-balance vendors" (not "outstanding vendors" alone) reads as
factual: these are vendors whose balances are outstanding, not vendors whose status is
"outstanding" (which would read as a positive valence). The hyphenless variant
"outstanding balance vendors" is structurally ambiguous; **the hyphen carries
load-bearing semantic clarity** and is retained.

The TopNSelector label is shorter ("Show top N outstanding vendors") because the filter
bar UI doesn't have room for "outstanding-balance" without wrapping. The section heading
gets the full unambiguous form because it's the section title and has the room.

## By-category vs by-property secondary grouping (PAO ruling — resolves PRE-SCOPE Q3)

AR Aging's secondary grouping is **By Property** (each property is a revenue center; AR
concentration by property is operationally meaningful for collections triage). AP Aging
cannot use the same dimension because expenses don't always have a single property
attribution (e.g., bookkeeping, software, insurance — corporate-overhead expenses span
the portfolio).

**PAO ruling: AP Aging's secondary grouping is By Expense Category.** Categories are
the GL expense-account roll-up that maps to the cartridge's `ProfitAndLossByProperty`
expense-side dimensions (utilities, maintenance, property-management fees,
insurance, taxes, professional-services, etc.). The user answers "where is the spend
concentrated?" — which is the AP-side equivalent of AR's "where is collection drag?"

If a later operator workflow surfaces a strong "by property" demand on AP (e.g., a
multi-property operator who tracks property-allocated expenses tightly), AP Aging can be
extended with a **third** section (By Property) without restructuring — the cartridge
contract would add a `byProperty` field and the page would render a third
`<ApAgingSection>` below the existing two. Cohort-4 ships with two sections; cohort-5+
can extend.

The cartridge contract MUST surface `byCategory` rows with a `categoryId` +
`categoryName` shape parallel to AR Aging's `customerId` + `customerName`. This is the
substrate Engineer hand-off contract; PAO flags it for the cartridge spec.

## Aging-bucket boundaries (substrate-dependent)

AR Aging uses **0–30 / 31–60 / 61–90 / 90+** boundaries (cohort-3 baseline). The default
recommendation for AP Aging is the **same boundaries**, for two reasons:

1. Operator convention is symmetric — most property-management operators treat
   payables aging the same way they treat receivables aging (30-day terms are the
   default; overdue starts when 30 days elapse).
2. Pattern reuse — same `aging-bucket-header-tint` token compositions, same
   `<AgingBucketPill>` variants, no new tokens, no new pill variants.

The substrate-flexibility note from PRE-SCOPE remains true: if the cartridge contract
ships with different boundaries (e.g., 0-30 net / 31+ overdue if the operator's standard
payment terms are NET 30 and the cartridge wants to distinguish "due but not late" from
"overdue"), the page accommodates whatever the cartridge surfaces. The current direction
**defaults to 5 buckets** (Current / 0-30 / 31-60 / 61-90 / 90+) because that's what the
shared `<AgingTable>` and totals-bar layouts assume.

If the cartridge contract surfaces fewer or more buckets, the layout must absorb the
change without re-authoring the page. Mitigation: `<AgingTable>` accepts a `buckets`
prop (array of `{ key, label, displayOrder }`) and renders columns dynamically; the
totals-bar tile-count adjusts via CSS grid (`grid-cols-{count+1}`). PAO authoring this
as a **forward-flexible direction**; FED implementation honors the dynamic-bucket
contract.

## Wireframe specs

### 1. IDLE — page just mounted

```
┌──────────────────────────────────────────────────────────────────┐
│  AP Aging                                                 ⟪ AP ⟫ │
│  Open payables by vendor and by expense category                 │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ Chart: [— select a chart ▾]   As of: [2026-05-25]                │
│ Show top [10] outstanding vendors                                │
│                                                                  │
│                                       [Run report]  [Export CSV] │
└──────────────────────────────────────────────────────────────────┘

(no content below filter bar — page is IDLE)
```

`Run report` is disabled until a chart is selected; `Export CSV` is disabled. Page is
**silent** until the user runs it. Run-on-demand discipline from pattern-016 (same as
AR Aging).

### 2. READY_TO_RUN — chart picked, ready to fire

Identical to AR Aging — chart selected, Run button enabled, Export still disabled.

### 3. SUCCESS — full layout

```
┌──────────────────────────────────────────────────────────────────┐
│  AP Aging                                                 ⟪ AP ⟫ │
│  Open payables by vendor and by expense category                 │
└──────────────────────────────────────────────────────────────────┘

(ProvisionalityBanner here when result.isProvisional — see pattern-015 doc)

┌──────────────────────────────────────────────────────────────────┐
│ Chart: [Operating accounts ▾]   As of: [2026-05-25]              │
│ Show top [10] outstanding vendors                                │
│                                       [Run report]  [Export CSV] │
└──────────────────────────────────────────────────────────────────┘

┌────────┬────────┬────────┬────────┬────────┬┬─────────┐
│Current │ 0–30 d │31–60 d │61–90 d │ 90+ d  ││  TOTAL  │
│ $8,200 │ $4,100 │ $2,300 │ $900   │ $1,800 ││ $17,300 │
└────────┴────────┴────────┴────────┴────────┴┴─────────┘
   (six tiles in a 6-col grid on lg+, 2-col on mobile)
   (Total tile has a left border-l-4 border-blue-500 separator)

By Vendor
┌─────────────────┬────────┬───────┬───────┬───────┬───────┬────────┐
│ Vendor          │Current │ 0–30  │ 31–60 │ 61–90 │  90+  │ Total  │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ Acme HVAC       │ $2,400 │ $1,100│   —   │   —   │   —   │ $3,500 │
│ City Utilities  │ $1,800 │ $1,800│   —   │   —   │   —   │ $3,600 │
│ Northstar Mgmt  │   —    │   —   │ $1,500│   —   │   —   │ $1,500 │
│ Pacific Roof Co │ $1,000 │ $800  │   —   │ $900  │ $1,800│ $4,500 │
│ ...             │ ...    │  ...  │  ...  │  ...  │  ...  │  ...   │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ TOTAL           │$8,200  │$4,100 │$2,300 │ $900  │$1,800 │$17,300 │
└─────────────────┴────────┴───────┴───────┴───────┴───────┴────────┘

By Expense Category
┌─────────────────┬────────┬───────┬───────┬───────┬───────┬────────┐
│ Category        │Current │ 0–30  │ 31–60 │ 61–90 │  90+  │ Total  │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ Maintenance     │ $3,400 │$1,900 │   —   │ $900  │ $1,800│ $8,000 │
│ Utilities       │ $1,800 │$1,800 │   —   │   —   │   —   │ $3,600 │
│ Property Mgmt   │   —    │   —   │$1,500 │   —   │   —   │ $1,500 │
│ Insurance       │ $1,500 │   —   │   —   │   —   │   —   │ $1,500 │
│ Professional    │ $1,500 │  $400 │  $800 │   —   │   —   │ $2,700 │
├─────────────────┼────────┼───────┼───────┼───────┼───────┼────────┤
│ TOTAL           │$8,200  │$4,100 │$2,300 │ $900  │$1,800 │$17,300 │
└─────────────────┴────────┴───────┴───────┴───────┴───────┴────────┘

Top 10 outstanding-balance vendors
─────────────────────────────────────────────────────────────────
(1) Pacific Roof Co                    90+: $1,800    Total: $4,500
(2) Acme HVAC                          90+: —         Total: $3,500
(3) City Utilities                     90+: —         Total: $3,600
(4) Northstar Mgmt                     90+: —         Total: $1,500
(5) ...
```

The wireframe deliberately shows the Top N list ordered by **total outstanding
balance**, NOT by 90+ amount alone. This is a delta from AR Aging — AR's Top
Delinquent list emphasizes 90+ (because the dominant collections concern is the oldest
unpaid). AP's Top Outstanding list emphasizes **total** (because the dominant operator
concern is total cash flow obligation, not just the very-overdue tail).

Per-row visual is the SAME composition as AR Aging Top Delinquent (rank chip + name +
secondary number + total) — only the secondary-number semantics change. The rendering
component (a candidate `<TopNList>` if it graduates in cohort-4 or 5) takes a sort
field and a secondary-stat field, both of which can be configured per page.

### 4. ApAgingTotalsBar in isolation

Identical structural composition to AR Aging's TotalsBar — 6 tiles (5 buckets +
Total), grid responsive, Total tile gets `border-l-4 border-blue-500` left edge. No
new tokens. The values change; the visual doesn't.

### 5. AgingTable in isolation (header tints)

Identical to AR Aging — Current + 0–30 get `bg-gray-50 text-gray-700` neutral; 31–60
gets `bg-amber-50 text-amber-900`; 61–90 gets `bg-orange-50 text-orange-900`; 90+
gets `bg-red-50 text-red-900`; Total gets neutral gray. Header tint canonical from
cohort-3 `tokens.md` → `aging-bucket-header-tint`. **No tints invert; no palette
shifts; AP and AR are visually parallel in this register.**

### 6. TopOutstandingList in isolation

```
Top 10 outstanding-balance vendors
─────────────────────────────────────────────────────────────────
(1) Pacific Roof Co                    90+: $1,800    Total: $4,500
(2) Acme HVAC                          90+: —         Total: $3,500
(3) City Utilities                     90+: —         Total: $3,600
(4) Northstar Mgmt                     90+: —         Total: $1,500
(5) ...

Per-row composition:
  Rank chip   →  inline-flex items-center justify-center w-6 h-6
                 rounded-full bg-gray-100 text-xs font-semibold text-gray-600
  Name        →  text-sm font-medium text-gray-900
  90+ amount  →  text-red-700 font-medium tabular-nums   (when > 0)
                 text-gray-400 "—"                         (when 0)
  Total       →  text-sm font-medium text-gray-900 tabular-nums
                 (bolder than AR's Total — emphasizes the primary sort key)
  Row border  →  border-b border-gray-100
```

The only visual delta from AR's TopDelinquent: the Total cell is rendered
`font-medium text-gray-900` (bolder) instead of `text-gray-700` (lighter), because
Total IS the sort key for the AP list — the user's eye should track Total down the
list, not 90+. AR's TopDelinquent does the opposite (90+ is the sort key, so 90+ is
the bolder of the two stat columns).

### 7. EMPTY — "No outstanding payables." (positive empty)

```
┌──────────────────────────────────────────────────────────────────┐
│  AP Aging                                                 ⟪ AP ⟫ │
│  Open payables by vendor and by expense category                 │
└──────────────────────────────────────────────────────────────────┘

(filter bar — same as SUCCESS, populated with the params just run)

┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  No outstanding payables.                                        │
│  All vendors are paid current as of 2026-05-25.                  │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

Same gray-friendly visual treatment as AR's "No outstanding receivables." This is a
**positive** empty state — the operator's books are clean, every vendor is paid up. No
warning color; no CTA; the sub-copy echoes the asOfDate.

The copy "All vendors are paid current as of {asOfDate}." reads as the operator's
status (we have paid current), not the vendors' status (they are not chasing us). The
"paid current" phrasing is the AP-side mirror of AR's "are current" — but on the AP
side the operator is the actor, so the copy makes that explicit.

### 8. ERROR

Identical to AR Aging — `<ErrorSurface variant="retryable">` with copy "Couldn't run
the AP aging report" + "The report service didn't respond. Try again in a moment." +
Retry button.

## State machine summary

This page uses pattern-016 run-on-demand **as-is, with no deviations** — same IDLE →
READY_TO_RUN → LOADING → SUCCESS (or ERROR) state machine as AR Aging. Required params:
`chartId`. Optional: `asOfDate` (defaults to today), `topVendorsN` (defaults to 10).
Run button gates purely on `chartId` presence.

## Provisionality banner placement

This page uses pattern-015 provisional report surface **as-is, with no deviations** —
banner position below page header, above filter bar, full content-width.

**Note for FED and PAO planning:** AP aging crosses open accounting periods routinely
(open bills, in-flight payments, posted-but-unmatched disbursements). Expect
`isProvisional === true` to be the common case, same as AR Aging. AP Aging plus AR
Aging together create the cohort that ratifies pattern-015 — both pages exhibit the
same `IsProvisional` semantics from the cartridge envelope, both surface the banner
identically, and the cross-page consistency is what makes pattern-015 a standing
pattern by the end of cohort-4.

## CSV export

This page uses pattern-017 CSV export **as-is, with no deviations**.

Filename: `ap-aging-summary-{asOfDate}.csv` (e.g., `ap-aging-summary-2026-05-25.csv`).
Provisional suffix: `ap-aging-summary-2026-05-25-provisional.csv` when
`isProvisional === true`.

Body convention (cartridge responsibility — out of scope for PAO direction): two CSV
sections in one file, "By Vendor" then "By Expense Category," with the totals row
terminating each section; the `TopOutstanding` block is **not** included in the CSV
(it's a UI-only derived view; the data lives in the vendor rows already).

## AgingTable column design

Same column structure as AR Aging:

| Column | Alignment | Format | Notes |
|---|---|---|---|
| Name (Vendor or Category) | left | text | `text-sm text-gray-900`; `font-medium` on totals row |
| Current | right | currency | `tabular-nums`; zero → `"—"` (`text-gray-400`) |
| 0–30 | right | currency | same |
| 31–60 | right | currency | same |
| 61–90 | right | currency | same |
| 90+ | right | currency | same |
| Total | right | currency | `tabular-nums`; bold on totals row |

Header tints per cohort-3 `aging-bucket-header-tint`. Section heading above each
table (`<h2>By Vendor</h2>` / `<h2>By Expense Category</h2>`). Totals row at bottom of
each table: bold weight + `border-t-2 border-gray-300`. Row hover: `hover:bg-gray-50`.
Table container: `rounded-lg border border-gray-200 overflow-hidden`.

## TopOutstandingList visual rules

- When `result.topOutstanding.length === 0` AND `topVendorsN > 0`: the entire section
  is hidden (don't render an empty heading + empty list).
- When `topVendorsN === 0` (user explicitly suppressed via the stepper): the section
  is hidden AND the heading is hidden.
- When `topVendorsN > 0` AND `result.topOutstanding.length > 0`: render heading + list.
  If the API returns fewer rows than `topVendorsN`, the heading still reads "Top
  {topVendorsN} outstanding-balance vendors" — the heading describes the **ask**, not
  the result count. (Same logic as AR Aging's TopDelinquentList heading rule.)

## Token usage

This page consumes the same canonical tokens as AR Aging — no new cohort-4 tokens
required for AP Aging proper. (Cohort-4 may introduce a `payables-direction-accent`
token if CIC overrules the PAO ruling above; current direction is no new direction-tint
token.)

| Token | Where used |
|---|---|
| `provisional-surface` | `<ProvisionalityBanner>` (when isProvisional) |
| `aging-bucket-header-tint` (31–60 / 61–90 / 90+ variants) | `<AgingTable>` thead column tints |
| ApAgingTotalsBar tile composition (existing cohort-2 tile pattern) | TotalsBar tiles |
| `text-red-700 font-medium tabular-nums` (existing) | TopOutstandingList 90+ amount |
| Rank-chip composition (existing) | TopOutstandingList rank `(N)` chip |
| Section heading composition (existing) | `<h2>` between sections |
| Page-header pill composition (NEW for cohort-4) | `⟪ Payables ⟫` / `⟪ Receivables ⟫` |

**One new token composition for cohort-4:** the page-header status pill
(`page-header-money-direction-pill` working name). Composition: `inline-flex
items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium uppercase
tracking-wide text-gray-700`. Variants: `Payables`, `Receivables`. Promoted to
`tokens.md` cohort-4 supplement; backfilled to AR Aging in the same PR or follow-up.

## Component reuse

From `@sunfish/ui-react` (v0.2):
- `<Card>` — outer page containers if Yeoman opts for a card-wrapped layout
- `<CurrencyAmount>` — every cell in `<AgingTable>`, every tile value in
  `<ApAgingTotalsBar>`, every Total cell in `<TopOutstandingList>`
- `<AgingBucketPill>` — **not used on this page** (same reasoning as AR Aging: cell
  tinting is handled by column-header tints, not cell pills)

From `apps/web/src/components/` (cohort-3 PR 1 shared infrastructure):
- `<ProvisionalityBanner>`
- `<ExportCsvButton>`
- `<ReportFilterBar>`
- `<ChartSelector>`
- `<RunButton>`
- `<ErrorSurface variant="retryable">`

**Cohort-4 page-local components** (do not graduate to `@sunfish/ui-react`):
- `<ApAgingTotalsBar>` — thin grid composition; structurally identical to
  `<ArAgingTotalsBar>` (cohort-3); the two should be considered for promotion to a
  shared `<AgingTotalsBar>` primitive **if AP Aging ships as expected**, OR retired
  into a `<DataTable>` overlay if cohort-5+ introduces the canonical `<DataTable>`.
- `<TopOutstandingList>` — same shape as `<TopDelinquentList>`; consider promoting
  both to a shared `<TopNList>` primitive at cohort-5 PR 1 alongside the
  `<DataTable>` work. PAO direction: **do not promote in cohort-4** — second-instance
  ratification justifies, but the page-locality is not painful enough yet.

## Accessibility

Same accessibility rules as AR Aging — section heading hierarchy, decorative rank chip
with `aria-hidden="true"`, color-never-sole-signal on aging buckets (column-header text
carries the meaning), `tabular-nums` + right-aligned for tile values, `<h2>` for empty
state ("No outstanding payables."), `<ErrorSurface>` with `role="alert"` +
`aria-live="polite"`, `aria-required="true"` on Chart filter.

**One additional consideration for AP Aging specifically:** the page-header
`⟪ Payables ⟫` pill is **decorative** when both the page H1 and subtitle already
disambiguate AP from AR. The pill MAY be `aria-hidden="true"` to avoid duplicate
announcement — but the page title element itself (`document.title`) should be
"AP Aging - Sunfish" (set by the route via React Router or equivalent) so the screen
reader announces the page identity from the page-title source, not from the visual
pill.

## States summary table

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| IDLE | mount; or any filter change after SUCCESS | Filter bar shown; no result area | Run button disabled until `chartId` set |
| READY_TO_RUN | `chartId` non-empty; no submitted params yet | Filter bar shown; no result area | Run button enabled |
| LOADING | mutation in flight | TotalsBar skeleton + 2 × table-row skeletons + (suppressed) TopOutstandingList | Run button → `Running…` + `aria-busy="true"`; Export disabled |
| SUCCESS | result returned; `!isProvisional` | TotalsBar + 2 tables + (optional) TopOutstandingList | Run re-enabled for fresh fire; Export enabled |
| SUCCESS + provisional | result returned; `isProvisional === true` | ProvisionalityBanner above filter bar + standard SUCCESS layout | same as SUCCESS |
| EMPTY (positive) | `result.totals.totalOpen === 0` | "No outstanding payables." panel + sub-copy with asOfDate | no CTA; user can re-run with different params |
| ERROR | mutation rejected | `<ErrorSurface>` red surface | Retry button (re-fires same submitted params) |

`EMPTY` triggered by aggregate total being zero, NOT by `byVendor.length === 0` — same
discipline as AR Aging.

## Pattern alignment

This page exercises one standing pattern + three candidates (the same four as AR
Aging — **and ratifies all three candidates as standing patterns**, since AP Aging is
the second-instance trigger):

- **standing pattern-009** (Bridge endpoint + frontend rebind pair) — the page reads
  `POST /api/v1/reports/ap-aging` and rebinds the typed result envelope to UI. Same
  baseline as AR Aging.
- **pattern-015** (provisional report surface) — RATIFIES on AP Aging as second
  instance. Same `<ProvisionalityBanner>` from cohort-3 PR 1. AR Aging + AP Aging
  together exhibit the pattern consistently; the candidate becomes standing.
- **pattern-016** (run-on-demand report) — RATIFIES on AP Aging as second instance.
  Same IDLE → READY_TO_RUN state machine; user clicks Run explicitly.
- **pattern-017** (CSV export affordance) — RATIFIES on AP Aging as second instance.
  Same `<ExportCsvButton>` adjacent to Run, same filename convention,
  `ap-aging-summary-{asOfDate}.csv` (+ `-provisional` suffix when applicable).

All four pattern claims live in the **PR description**, not in commit bodies — fleet
commitlint convention (the `@standing-pattern:` line at start of a commit body line
trips the footer parser).

## Halt conditions (Engineer-side dependencies)

PAO authoring this design direction **does not block on substrate**. When substrate
lands, the page contract MUST surface:

| Dependency | Owner | Status |
|---|---|---|
| AP Aging cartridge ships in `Sunfish.Blocks.Reports.Cartridges.ApAgingSummary` | Engineer (W#72 substrate v2) | Pending |
| Bridge endpoint `POST /api/v1/reports/ap-aging` | Engineer | Pending; depends on cartridge |
| Wire types in `apps/web/src/api/reports.ts` (`ApAgingSummary` shape) | FED | Pending; depends on cartridge contract |
| Cartridge contract surfaces `byVendor` + `byCategory` (NOT `byProperty` in v1) | Engineer | PAO ruling above; flagged for substrate spec |
| Cartridge contract surfaces `topOutstanding` sorted by total balance DESC | Engineer | PAO ruling above; flagged for substrate spec |
| 5-bucket aging (Current / 0-30 / 31-60 / 61-90 / 90+) — same as AR | Engineer | PAO direction; flagged for substrate spec |

If the cartridge contract ships with different shape than the PAO direction above,
**PAO revisits this doc before FED begins page implementation**. Substrate-pinned
direction at that point.

## Cross-cohort backfill — AR Aging gets the same page-header pill

When AP Aging ships, AR Aging is retrofitted with the parallel `⟪ Receivables ⟫`
pill in the same PR or an immediate follow-up. Reasoning: the money-direction signal
only earns its keep if BOTH pages carry it; AP alone would read as a stylistic
flourish, but the AP+AR pair reads as a discipline. Backfill is a 1-line JSX
addition to `ArAgingPage` + a token-composition reference; trivial.

— PAO, 2026-05-25
