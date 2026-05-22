# Cohort-3 Design Tokens — Inventory

This file inventories which design tokens cohort-3 uses, which are existing vs new, and which gaps warrant promotion to `@sunfish/ui-react` after the cohort lands.

## Reference: framework design tokens

Live at `shipyard/_shared/design/tokens-guidelines.md`. Cohort-3 inherits these without modification.

## What's new vs cohort-2

Cohort-3 is the first cohort to introduce **named semantic-color families** beyond the cohort-2 days-due tints:

- **`GLAccountType` palette** — 5 account types get distinct chip colors (TrialBalancePage)
- **`OccupancyStatus` palette** — 4 occupancy states get distinct badge colors (RentRollPage)
- **Aging-bucket header tints made canonical** — cohort-2 inlined them on `<DaysDuePill>` (AccountingPage); cohort-3 promotes them to AgingTable column headers + `<AgingBucketPill>` shared component
- **Provisional surface tokens** — amber banner family for `<ProvisionalityBanner>`

None of these introduce **new Tailwind utility classes**; all compose existing palette stops. They are semantic-token *combinations* that get a canonical name, not new color definitions.

## Cohort-3 token usage by page

### TrialBalancePage (PR 4)

| Concern | Token | Status |
|---|---|---|
| Page container | `max-w-7xl mx-auto px-4 py-6` | existing |
| Page H1 | `text-2xl font-semibold text-gray-900` | existing |
| Filter bar wrapper | `flex flex-wrap items-end gap-3 border-b border-gray-200 pb-4` | existing |
| Filter label | `text-sm font-medium text-gray-700` | existing |
| Run button (primary) | `bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 text-white text-sm font-medium rounded-md px-4 py-2` | existing |
| Export CSV button | `border border-gray-300 text-gray-700 hover:bg-gray-50 text-sm rounded-md px-4 py-2 inline-flex items-center gap-2` | existing |
| Provisionality banner | `border border-amber-300 bg-amber-50 text-amber-900 rounded-md px-4 py-3` | **new (canonical)** — see [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md) |
| Balance badge — balanced | `inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-700` | existing (StatusPill pattern) |
| Balance badge — out of balance | `inline-flex items-center rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-medium text-red-700` | existing (StatusPill pattern) |
| Account-type chip — Asset | `bg-blue-100 text-blue-700` (pill base + this) | **new (canonical)** — see Q6 in INDEX |
| Account-type chip — Liability | `bg-purple-100 text-purple-700` | **new (canonical)** |
| Account-type chip — Equity | `bg-slate-100 text-slate-700` | **new (canonical)** |
| Account-type chip — Revenue | `bg-green-100 text-green-700` | **new (canonical)** |
| Account-type chip — Expense | `bg-amber-100 text-amber-800` | **new (canonical)** |
| Table container | `rounded-lg border border-gray-200 overflow-hidden` | existing |
| Table header cell | `bg-gray-50 text-xs font-medium uppercase tracking-wide text-gray-500 px-3 py-2 text-left` | existing |
| Table body cell — text | `px-3 py-2 text-sm text-gray-900` | existing |
| Table body cell — numeric | `px-3 py-2 text-sm text-gray-900 text-right tabular-nums` | existing |
| Sticky thead | `sticky top-0 z-10` (on `<thead>`) | existing |
| Skeleton row | `bg-gray-100 animate-pulse h-8 my-1 rounded` | existing |
| Error surface | `border border-red-200 bg-red-50 text-red-700 rounded-lg p-4` | existing |
| Zero-value cell | `text-gray-400` (when displaying "—") | existing |

### ArAgingPage (PR 5)

| Concern | Token | Status |
|---|---|---|
| ArAgingTotalsBar tile container | `rounded-lg border border-gray-200 bg-white px-4 py-3` | existing |
| ArAgingTotalsBar tile label | `text-xs uppercase tracking-wide text-gray-500` | existing |
| ArAgingTotalsBar tile value | `text-lg font-semibold text-gray-900 tabular-nums` | existing |
| AgingTable header — Current | `bg-gray-50 text-gray-700` | existing |
| AgingTable header — 0–30 | `bg-gray-50 text-gray-700` | existing (no-overdue) |
| AgingTable header — 31–60 | `bg-amber-50 text-amber-900` | **new (canonical aging-bucket header tint)** |
| AgingTable header — 61–90 | `bg-orange-50 text-orange-900` | **new (canonical aging-bucket header tint)** |
| AgingTable header — 90+ | `bg-red-50 text-red-900` | **new (canonical aging-bucket header tint)** |
| TopDelinquentList row | `flex items-center justify-between border-b border-gray-100 py-2` | existing |
| TopDelinquentList rank badge | `inline-flex items-center justify-center w-6 h-6 rounded-full bg-gray-100 text-xs font-semibold text-gray-600` | existing |
| TopDelinquentList 90+ amount | `text-red-700 font-medium tabular-nums` | existing |
| Section heading | `text-lg font-semibold text-gray-900 mt-6 mb-3` | existing |

The aging-bucket header tints are intentionally one shade LIGHTER than the `<AgingBucketPill>` chip backgrounds (which use `bg-amber-100` / `bg-orange-100` / `bg-red-100`). The headers tint the table column; the pills tint individual cells. Both visually pair without competing.

### ProfitAndLossByPropertyPage (PR 3)

| Concern | Token | Status |
|---|---|---|
| PortfolioSummaryTiles container | `grid grid-cols-1 sm:grid-cols-3 gap-4` | existing |
| Tile — Revenue value | `text-green-700 text-2xl font-semibold tabular-nums` | existing |
| Tile — Expenses value | `text-red-700 text-2xl font-semibold tabular-nums` | existing |
| Tile — Net Income (positive) | `text-green-700 text-2xl font-semibold tabular-nums` | existing |
| Tile — Net Income (negative) | `text-red-700 text-2xl font-semibold tabular-nums` | existing |
| PropertyAccordion header | `flex items-center justify-between border-b border-gray-200 px-4 py-3 hover:bg-gray-50 cursor-pointer` | existing |
| PropertyAccordion header — expanded | `bg-gray-50 border-b-2 border-blue-500` | existing |
| PropertyAccordion body | `bg-white px-4 py-3 border-b border-gray-200` | existing |
| Chevron — collapsed | `ChevronRightIcon w-4 h-4 text-gray-400` (Heroicon) | existing |
| Chevron — expanded | `ChevronDownIcon w-4 h-4 text-gray-600` (Heroicon) | existing |
| Account line row | `flex items-center justify-between text-sm py-1` | existing |
| Revenue line amount | `text-green-700 tabular-nums` | existing |
| Expense line amount | `text-red-700 tabular-nums` | existing |
| "Unassigned" property row | `text-gray-500 italic` (de-emphasized) | existing |

### RentRollPage (PR 2)

| Concern | Token | Status |
|---|---|---|
| PortfolioSummaryBar tile grid | `grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-5 gap-3` | existing |
| Tile (same as cohort-2 AccountingPage summary tile) | `rounded-lg border border-gray-200 bg-white px-3 py-2` | existing |
| PropertyHeader row | `flex items-center justify-between bg-gray-50 border-b border-gray-200 px-4 py-3` | existing |
| PropertyHeader property name | `text-base font-semibold text-gray-900` | existing |
| PropertyHeader stats line | `text-sm text-gray-600` | existing |
| UnitTable container | `overflow-x-auto` (allows horizontal scroll at narrow widths) | existing |
| Occupancy badge — Occupied | `bg-green-100 text-green-700` (StatusPill base) | **new (canonical)** — see Q7 in INDEX |
| Occupancy badge — NoticeGiven | `bg-amber-100 text-amber-800` (StatusPill base) | **new (canonical)** |
| Occupancy badge — Vacant | `bg-gray-100 text-gray-700` (StatusPill base) | **new (canonical)** |
| Occupancy badge — OffMarket | `bg-gray-100 border border-gray-300 text-gray-600` (StatusPill outlined variant) | **new (canonical)** |
| Expiring-soon badge | `bg-amber-100 text-amber-800` (StatusPill base) | existing (matches cohort-2 LeasesPage expiry warning) |
| Delinquency cell (`<AgingBucketPill>`) | (component-managed; uses pill base + aging palette) | **new shared component** — see [`component-reuse-audit.md`](./component-reuse-audit.md) |
| Tenant name "—" (vacant) | `text-gray-400` | existing |

## New shared tokens — promoted to canonical

These six combinations get a canonical home in the cohort-3 design docs. They compose existing Tailwind classes but get a *name* so downstream cohorts can reference them consistently.

| Canonical name | Composition | First instance |
|---|---|---|
| `provisional-surface` | `border border-amber-300 bg-amber-50 text-amber-900 rounded-md px-4 py-3` | `<ProvisionalityBanner>` |
| `gl-account-chip` (×5 variants) | StatusPill base + `bg-{family}-100 text-{family}-700` per type | `TrialBalanceTable` |
| `occupancy-badge` (×4 variants) | StatusPill base + per-status colors (one outlined variant) | `RentRollPage` UnitTable |
| `aging-bucket-header-tint` (×3 variants, 31-60 / 61-90 / 90+) | `bg-{family}-50 text-{family}-900` per bucket | `AgingTable` thead |
| `aging-bucket-pill` (×6 variants) | StatusPill base + per-bucket colors | `<AgingBucketPill>` shared component |

## Pill base — promoted from inline to shared

Cohort-2 documented the pill base (`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium`) as a repeated inline pattern. With cohort-3 introducing three new semantic-color families using this base, the time has come — the `<StatusPill>` primitive promotion candidate from cohort-2 tokens.md is ratified for cohort-3 implementation.

FED extracts `<StatusPill kind=...>` into `@sunfish/ui-react` as part of PR 1 (shared infrastructure). The variant API:

```typescript
<StatusPill kind="glAccountType" value="Asset" />
<StatusPill kind="occupancyStatus" value="NoticeGiven" tooltip={vacancyReason} />
<StatusPill kind="agingBucket" value="Days31To60" />
<StatusPill kind="balanceState" value="Balanced" />
<StatusPill kind="workOrderStatus" value={wo.status} />     // cohort-1 retrofit, future cleanup
```

## Other gap candidates for `@sunfish/ui-react` promotion (after cohort-3)

These patterns now have ≥2 cohorts of instances. Recommended for promotion in cohort-4 cleanup or as a standalone shipyard PR after cohort-3 lands:

### `<DataTable>` primitive

LeaseDetailPage payment table + AccountingPage outstanding table + MaintenancePage work-order table + cohort-3's TrialBalanceTable + AgingTable + UnitTable now all share the same shape (rounded-lg border-gray-200 wrapper, gray-50 header, hover-on-row, tabular-nums for numeric, sticky thead candidate). Six instances; pattern is now mature enough to warrant the API design exercise.

**Cohort-3 reports tables surface new requirements:** sorting (TrialBalance by account code), virtual scrolling (large account lists), column visibility toggles, sticky headers. The eventual `<DataTable>` API needs to subsume these. Recommend Yeoman scopes the API in a post-cohort-3 design pass.

### `<ErrorSurface variant="retryable">` primitive

Cohort-2 flagged this. Cohort-3 has 5 error-state surfaces (1 per page × 4 + 1 generic in PR 1). Ratify now.

**Proposed API:**

```typescript
<ErrorSurface
  variant="retryable"
  title="Couldn't load trial balance"
  body="The report service didn't respond. Try again in a moment."
  onRetry={refetch}
/>
```

FED implements during PR 1.

### `<ConfirmationSurface>` primitive

Not used in cohort-3 (no write paths). Defer to whichever cohort introduces the next write surface.

## No new design tokens (only canonical compositions)

The cohort introduces **no new Tailwind palette stops**. All "new" canonical tokens compose existing palette colors. The cohort's contribution to the design system is semantic — naming repeated combinations — not chromatic.

— PAO, 2026-05-22
