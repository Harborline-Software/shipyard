# Cohort-4 Design Tokens — Supplement to Cohort-3

This file is a **supplement** to `shipyard/_shared/design/cohort-3/tokens.md`. Cohort-4
inherits cohort-3 tokens unchanged and adds ONE new canonical composition.

## What's new vs cohort-3

Cohort-4 introduces ONE new named token composition:

- **`page-header-money-direction-pill`** — small status pill at the page-header
  level distinguishing AR (Receivables) from AP (Payables) at a glance. Used on
  ApAgingPage as primary delta from ArAgingPage; backfilled to ArAgingPage for
  visual parity.

No other new tokens. AP Aging deliberately reuses cohort-3's `aging-bucket-header-tint`
family unchanged, because the severity signal (amber/orange/red intensifying with
overdue age) carries identical meaning on both pages — the page-header pill carries
money-direction; the bucket tints carry severity; the two register cleanly without
competing for the same retinal slot.

## Token: page-header-money-direction-pill

### Composition (canonical)

```
inline-flex items-center
rounded-full
bg-gray-100
px-2.5 py-0.5
text-xs font-medium uppercase tracking-wide
text-gray-700
```

### Variants

| Variant | Label text | Page |
|---|---|---|
| `Payables` | "Payables" | ApAgingPage (NEW in cohort-4) |
| `Receivables` | "Receivables" | ArAgingPage (BACKFILL in cohort-4) |

### Use rules

1. **Placement:** top-right of the page-header block, vertically centered with the H1.
2. **Aria semantics:** decorative (`aria-hidden="true"`) when the page H1 already
   names "AP Aging" or "AR Aging" — the pill is a redundant visual signal, not the
   primary semantic carrier. (`document.title` carries the canonical page identity for
   screen readers.)
3. **Page H1 + subtitle continue to disambiguate** — the pill is NOT a substitute for
   clear page titling. If the H1 reads "Reports" or some other generic heading, the
   pill cannot carry the AP-vs-AR distinction alone.
4. **No other pages adopt the pill** unless they ALSO carry a money-direction concept
   that benefits from page-orientation cuing. Random sprinkling of the pill on
   non-direction pages would dilute the signal. (Possible future use: cash-flow
   statement, balance-sheet — both pages where a "Direction: Inflow/Outflow" or
   "Direction: Assets/Liabilities" pill would orient the user at the top of the
   page.)
5. **Color is neutral gray on both variants** — DO NOT vary background color between
   Payables and Receivables. The neutral palette keeps the pill from competing with
   the aging-bucket severity colors. The label text alone differentiates.

### Why no color-coded variant

The instinct on first read is: "AR should be red, AP should be orange — money direction
deserves a color signal." This was considered and rejected during cohort-4 authoring.
The rejection reason:

- AR Aging already uses RED in its column headers (`90+` bucket =
  `bg-red-50 text-red-900`) and in its TopDelinquent 90+ amount cells
  (`text-red-700`). If the page-header pill is ALSO red, the page becomes a
  red-on-red read where the user can't tell whether red is signalling
  money-direction or severity.
- AP Aging would have the same problem one step worse: an orange page-header pill
  PLUS orange in the 61–90 bucket header (`bg-orange-50`) creates visual collision.
- Neutral gray on the pill keeps the color-coded slots EXCLUSIVELY for the
  aging-bucket severity system. The pill carries semantic-text-only differentiation.

The decision: **money direction is signaled by LABEL TEXT, not by COLOR.** The
neutral-gray pill reads as a quiet identifier, not as a warning signal — which is
correct, because money direction is not a warning concept.

## Token: cohort-3 inheritance — applied to AP Aging

For completeness, the cohort-3 tokens cohort-4 ApAgingPage uses unchanged:

| Token | Source | Use on AP Aging |
|---|---|---|
| `provisional-surface` | cohort-3 | `<ProvisionalityBanner>` when isProvisional |
| `aging-bucket-header-tint` (Current / 0–30 neutral) | cohort-3 | AgingTable thead Current + 0–30 columns |
| `aging-bucket-header-tint` (31–60 amber) | cohort-3 | AgingTable thead 31–60 column |
| `aging-bucket-header-tint` (61–90 orange) | cohort-3 | AgingTable thead 61–90 column |
| `aging-bucket-header-tint` (90+ red) | cohort-3 | AgingTable thead 90+ column |
| Tile composition (cohort-2 → cohort-3 standard) | cohort-2 | ApAgingTotalsBar tiles |
| Border-l-4 border-blue-500 (Total tile separator) | cohort-3 | ApAgingTotalsBar Total tile |
| Section heading composition | cohort-3 | `<h2>By Vendor>` / `<h2>By Expense Category>` / `<h2>Top N outstanding-balance vendors>` |
| ErrorSurface composition (retryable variant) | cohort-3 | Cohort-4 P1/P2/P4 PRs' error states |
| Rank-chip composition | cohort-3 | TopOutstandingList rank `(N)` |
| `text-red-700 font-medium tabular-nums` | cohort-3 | TopOutstandingList 90+ amount |
| ExportCsvButton composition | cohort-3 | ApAgingPage CSV export |
| RunButton composition | cohort-3 | ApAgingPage Run |
| ReportFilterBar composition | cohort-3 | ApAgingPage filter bar |
| ChartSelector composition | cohort-3 | ApAgingPage chart filter |

No tokens are inherited and renamed. No tokens are inherited and re-tuned. Cohort-4 is
a **near-total token-reuse cohort** from cohort-3.

## Forward-watch — tokens to consider for cohort-5+

- **`<DataTable>` family** — cohort-3 introduced AgingTable + TrialBalanceTable +
  RentRollPage UnitTable; cohort-4 adds AP AgingTable. Four instances of tabular
  numeric data tables across two cohorts. Strong signal for `<DataTable>` primitive
  promotion in cohort-5 (or earlier if QM/FED prioritizes housekeeping).
- **`<TopNList>` family** — cohort-3 TopDelinquentList + cohort-4 TopOutstandingList.
  Two instances; structurally identical. Promotion to `<TopNList>` primitive in
  cohort-5 OR as part of the `<DataTable>` work (depending on whether TopN is
  modeled as a data-table or as its own thing).
- **Money-direction pill — secondary uses?** If cash-flow statement, balance sheet,
  or other money-direction-bearing reports ship in cohort-5+, they get the same
  `page-header-money-direction-pill` token (potentially with new variants like
  `Inflow` / `Outflow` / `Assets` / `Liabilities`). Forward-watch flagged here.
- **Payment-status pill (if cohort-4 P1 Payment rebind needs one)** — Pending /
  Completed / Failed states. Likely consumes the cohort-1 `<StatusPill>` palette
  (Pending = amber, Completed = green, Failed = red — matches the work-order status
  semantics). No new token; reuses existing palette via `<StatusPill>` with new
  variants.

— PAO, 2026-05-25
