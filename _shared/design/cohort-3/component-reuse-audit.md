# Component Reuse Audit — Cohort-3

This audit catalogs which existing components cohort-3 pages reuse, which new components ship in cohort-3 PR 1, and which gaps warrant promotion to `@sunfish/ui-react` after the cohort lands.

Cohort-3 is the **first cohort to ship its own dedicated shared-infrastructure PR (PR 1)**. Cohort-1 and cohort-2 inlined shared patterns per page (StatusPill classes, error surfaces, table primitives). Cohort-3 promotes 5 components to `apps/web/src/components/` because all 4 report pages consume them identically — the duplication cost would be prohibitive.

## Existing components reused

### From `@sunfish/ui-react` v0.2

| Component | Used by | Usage notes |
|---|---|---|
| `<Card>`, `<CardHeader>`, `<CardContent>`, `<CardTitle>`, `<CardFooter>` | All 4 pages | Shadcn-style card primitive; reused as-is for PortfolioSummary tiles, PropertyHeader cards (where applicable), filter bar wrapper |
| `<Badge>` | All 4 pages | Used where a generic chip is needed without the canonical StatusPill variants (e.g., "Expiring" badge on RentRoll, "Provisional" annotation in CSV filenames is text-only) |
| `<CurrencyAmount>` | All 4 pages | Locale-aware number formatting with sign + parentheses for negatives. Reused everywhere a money amount appears. |
| `<AgingBucketPill>` (NEW in v0.2 per FED queue #5) | ArAging + RentRoll | Renders an aging bucket value as a colored pill using the canonical `aging-bucket-pill` token. Cohort-3 is the **first consumer**; cohort-2 inlined the equivalent on AccountingPage. |

### From `@/components/` (sunfish app-local; existing)

| Component | Used by | Usage notes |
|---|---|---|
| `AuthRoleGate` | None in cohort-3 | Reports are read-only; no role-gated mutations. (Cohort-2 RentCollectionPage was the last cohort to need this.) |
| `Heroicons` (`ExclamationTriangleIcon`, `CheckCircleIcon`, `ChevronRightIcon`, `ChevronDownIcon`, `ArrowDownTrayIcon`, `ArrowPathIcon`, `XMarkIcon`) | All 4 pages | Standard icon set per cohort-1 baseline |

### Tailwind utility-class patterns (no component primitive)

These remain inline conventions, repeated across pages. The canonical compositions get a name in [`tokens.md`](./tokens.md) but no component wrapper:

| Pattern | Pages | Token canonical name |
|---|---|---|
| Skeleton row | All 4 (loading state) | `bg-gray-100 animate-pulse h-8 my-1 rounded` (no token name yet — defer until 3rd cohort needs it) |
| Sticky thead | TrialBalance + ArAging | `sticky top-0 z-10` on `<thead>` (no token name) |
| Tabular numeric cell | All 4 (any numeric column) | `text-right tabular-nums` (no token name) |
| Section heading | ArAging (between Customer/Property sections), P&L (Revenue/Expense within accordion) | `text-lg font-semibold text-gray-900 mt-6 mb-3` |

These compose existing Tailwind utilities; promoting them to components would over-abstract. Convention-by-imitation is the right level here.

## New shared components — ship in cohort-3 PR 1

Cohort-3 PR 1 is the **shared infrastructure PR**. It introduces these 5 new components, plus 2 component primitives promoted from cohort-2's "promote-candidate" list:

### 1. `<ProvisionalityBanner>` — pattern-011 visible surface

**Location:** `apps/web/src/components/ProvisionalityBanner.tsx`

**Used by:** All 4 cohort-3 report pages

**Props:**

```typescript
interface ProvisionalityBannerProps {
  isProvisional: boolean
  warnings: string[]
  className?: string
}
```

When `isProvisional === false` the component renders `null` (caller doesn't need conditional rendering at the call site). The collapse state is internally managed (the banner manages its own disclosure state since it's per-result and resets on caller unmount).

**Canonical visual:** see [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md). Token: `provisional-surface` from [`tokens.md`](./tokens.md).

### 2. `<ExportCsvButton>` — pattern-013 visible surface

**Location:** `apps/web/src/components/ExportCsvButton.tsx`

**Used by:** All 4 cohort-3 report pages

**Props:**

```typescript
interface ExportCsvButtonProps {
  enabled: boolean
  onExport: () => Promise<void>
  filename: string
}
```

Component handles its own loading state (`Exporting…` text + spinner + `aria-busy`) and failure toast (auto-dismiss after 3s). Caller provides the `onExport` function (page-specific, calls the page's `exportXxxCsv` API function) and the deterministic `filename`.

**Canonical visual + interaction:** see [`csv-export-pattern.md`](./csv-export-pattern.md).

### 3. `<ReportFilterBar>` — pattern-012 visible surface

**Location:** `apps/web/src/components/ReportFilterBar.tsx`

**Used by:** All 4 cohort-3 report pages

**Props:**

```typescript
interface ReportFilterBarProps {
  children: React.ReactNode      // page-specific filter controls
  onRun: () => void
  canRun: boolean                // computed by parent from filter validity
  isRunning: boolean
  exportButton?: React.ReactNode // typically <ExportCsvButton>; positioned at the right
}
```

Provides the filter-bar chrome: the wrapper layout, the `[Run report]` primary button on the right, the optional `[Export CSV]` slot, the responsive wrap behavior, the form-submit-on-Enter binding.

Caller passes filter controls as `children` (ChartSelector + page-specific date/toggle/multiselect inputs); component owns the layout + the Run button + the export slot.

### 4. `<ChartSelector>` — pattern-012 ChartId acquisition surface

**Location:** `apps/web/src/components/ChartSelector.tsx`

**Used by:** All 4 cohort-3 report pages

**Props:**

```typescript
interface ChartSelectorProps {
  value: ChartId | null
  onChange: (chartId: ChartId | null) => void
  required?: boolean   // default true; if false, ChartId can stay null
}
```

Behavior canonical to INDEX Q2:
- 0 charts: render disabled with "Set up a chart of accounts" link
- 1 chart: auto-select (call `onChange` on mount); render as label
- N charts: dropdown; no default selection

Fetches the chart list internally via `useCharts()` hook (which hits the Bridge chart-list endpoint — exact path TBD per pending halt condition in INDEX).

### 5. `<RunButton>` — pattern-012 primary action surface

**Location:** `apps/web/src/components/RunButton.tsx`

**Used by:** `<ReportFilterBar>` internally; never called directly by report pages

**Props:**

```typescript
interface RunButtonProps {
  onClick: () => void
  enabled: boolean
  isRunning: boolean
}
```

Renders the canonical Run-button text + visual transitions per [`run-on-demand-pattern.md`](./run-on-demand-pattern.md) (`Run report` / `Running…` / disabled state / aria-busy).

Reasoning for breaking this out of `<ReportFilterBar>` despite being internal: it's the load-bearing UI element of pattern-012; extracting it makes future pattern-012 surfaces (a dashboard tile, a quick-action menu) reuse the same button without dragging the whole filter bar along.

### 6. `<StatusPill>` — promoted from cohort-2 candidate

**Location:** `@sunfish/ui-react` (NOT `apps/web/src/components/`)

**Used by:** TrialBalance (gl-account-chip + balanceState), RentRoll (occupancy-badge), ArAging (aging-bucket-pill where used inline)

**Props:**

```typescript
interface StatusPillProps {
  kind: 'glAccountType' | 'occupancyStatus' | 'agingBucket' | 'balanceState' | 'workOrderStatus'
  value: string
  tooltip?: string
  outlined?: boolean
}
```

Variant-driven coloring per canonical compositions in [`tokens.md`](./tokens.md). The `workOrderStatus` variant exists for cohort-1 retrofit (MaintenancePage currently inlines `STATUS_COLORS`); cleanup happens as a low-priority follow-up after cohort-3 ships.

The `outlined` prop supports the canonical `OccupancyStatus.OffMarket` variant (and any future "intentional absence" states).

This component was the most-promoted candidate from cohort-2 tokens.md. Cohort-3 introducing 3 new variants (gl-account-chip, occupancy-badge, balanceState — plus the new aging-bucket variant which extends what cohort-2 inlined) crosses the "three instances" threshold; promotion is ratified.

### 7. `<ErrorSurface>` — promoted from cohort-2 candidate

**Location:** `apps/web/src/components/ErrorSurface.tsx`

**Used by:** All 4 cohort-3 report pages (ERROR state) + retrofit candidate for cohort-1 + cohort-2 error states (post-cohort-3 cleanup)

**Props:**

```typescript
interface ErrorSurfaceProps {
  variant?: 'retryable' | 'reload' | 'redirect'  // default 'retryable'
  title: string
  body: string
  onRetry?: () => void
  onReload?: () => void
  redirectTo?: { label: string; to: string }
}
```

Canonical compositions:
- `variant="retryable"` — Title + body + `[Try again]` button calling `onRetry`
- `variant="reload"` — Title + body + `[Reload page]` button (calls `window.location.reload()` internally if `onReload` not provided)
- `variant="redirect"` — Title + body + named-link redirect (e.g., `[Choose another lease]`)

Cohort-3 only uses `variant="retryable"` (all 4 page error states); the other variants exist for cohort-2 retrofit later.

## New page-local components

Components used by exactly one page; not promoted in cohort-3:

| Component | Page | Why not shared |
|---|---|---|
| `TrialBalanceTable` | TrialBalancePage | Table semantics tightly coupled to TrialBalanceRow shape; reuse value low until a second balance-style table emerges |
| `BalanceBadge` | TrialBalancePage | Single-purpose conditional pill (balanced/out-of-balance); used in exactly one place. Could compose `<StatusPill kind="balanceState">` directly without a wrapper; the wrapper exists only to encode the "format the out-of-balance delta with `<CurrencyAmount>`" logic |
| `AgingTable` | ArAgingPage | Shaped to ArAgingRow + canonical header tints; reuse not warranted until a second age-bucketed table appears |
| `TopDelinquentList` | ArAgingPage | Single-page list with a specific top-N affordance |
| `ArAgingTotalsBar` | ArAgingPage | Specific to 5-bucket aging visualization; not generalizable |
| `PortfolioSummaryTiles` (P&L variant) | ProfitAndLossByPropertyPage | 3-tile pattern; semantically similar to RentRoll's PortfolioSummaryBar but visually distinct (3 vs 5 tiles; different label set). Defer convergence until a third "portfolio summary" surface appears |
| `PropertyAccordion` + `PropertyAccordionList` | ProfitAndLossByPropertyPage | Accordion mechanics could promote to shared but cohort-3 RentRoll uses property *blocks* (non-collapsible), not accordions; defer until a second accordion consumer appears |
| `PortfolioSummaryBar` (RentRoll variant) | RentRollPage | See note above for P&L variant |
| `PropertyBlock` + `PropertyHeader` (RentRoll variant) | RentRollPage | Specific to rent roll visual idiom; not generalized |
| `UnitTable` | RentRollPage | Tightly coupled to RentRollUnitRow shape |

## Components NOT changed in cohort-3

Components touched by cohort-1 and cohort-2 that cohort-3 deliberately leaves alone:

- `MaintenancePage` work-order table — cohort-1 baseline; cohort-3 does NOT retrofit it to `<DataTable>` (see deferred promotion below)
- `LeasesPage` table — cohort-1 baseline
- `RentCollectionPage` form — cohort-2 PR 3; cohort-3 does not touch financial write paths
- `AccountingPage` — cohort-2 PR 2; cohort-3 does not touch
- `LeaseDetailPage` — cohort-2 PR 1; cohort-3 does not touch

## Deferred promotion candidates (cohort-4+ cleanup)

These patterns are now ratified for promotion but deferred to post-cohort-3 cleanup work to keep cohort-3 PR cluster scope bounded:

### `<DataTable>` primitive (still deferred)

The cohort-2 tokens.md flagged this. Cohort-3 now has 4 more table instances (TrialBalanceTable + 2× AgingTable + UnitTable), bringing total instances to 8 across cohort-1/2/3. Pattern is mature.

**Cohort-3 surfaces additional requirements** not in cohort-2 instances:
- Sticky thead (TrialBalance)
- Sub-row grouping (RentRoll units within PropertyBlock)
- Column tinting (ArAging header tints)
- Numeric formatting + zero-as-em-dash conventions
- Horizontal scroll on narrow viewports

The eventual `<DataTable>` API needs to subsume these. Recommend Yeoman scopes the API in a post-cohort-3 design pass — this is a multi-week design exercise on its own (proper column-API design, virtualization plan, sort/filter contracts).

### `<ConfirmationSurface>` primitive

Cohort-2 flagged. Cohort-3 has zero write paths (reports are read-only), so this primitive remains a 1-instance pattern (cohort-2 RentCollectionPage only). Defer until a second write surface emerges.

### `<SkeletonRow>` / `<SkeletonAccordion>` / `<SkeletonTile>` primitives

Cohort-3 has loading-state skeletons in all 4 pages + P&L accordion skeleton + skeleton tiles in P&L summary. Pattern is repetitive but the inline `bg-gray-100 animate-pulse` is so light-weight that a primitive may be over-abstraction. Defer + watch — if cohort-4 also has skeletons (likely) and they all look identical, promote.

### `<PortfolioSummary>` (generic)

P&L 3-tile + RentRoll 5-tile are visually convergent. A generic `<PortfolioSummary tiles={...}>` API would consolidate. Defer to cohort-4 when a third instance might emerge (AP Aging summary, Balance Sheet summary).

## Cohort-3 PR 1 scope (what FED implements)

For FED's quick reference, here is what PR 1 must ship before PRs 2-5 can land:

1. `apps/web/src/api/reports.ts` — wire types + 4 fetch functions + 4 CSV export functions
2. `apps/web/src/hooks/useReports.ts` — 4 React Query hooks + `useCharts` for `<ChartSelector>`
3. `apps/web/src/components/ProvisionalityBanner.tsx`
4. `apps/web/src/components/ExportCsvButton.tsx`
5. `apps/web/src/components/ReportFilterBar.tsx`
6. `apps/web/src/components/ChartSelector.tsx`
7. `apps/web/src/components/RunButton.tsx`
8. `apps/web/src/components/ErrorSurface.tsx`
9. `@sunfish/ui-react` `<StatusPill>` extraction (with all 5 variants including `workOrderStatus` for cohort-1 retrofit later)
10. Nav update: introduce `Reports` group header per INDEX Q1; populate with the 4 cohort-3 routes (NOT AP Aging per INDEX Q5)

Plus the engineering hand-offs that have to land before PR 1 can wire types:
- Engineer contract-frozen beacon for `ChartId` JSON serialization
- Engineer contract-frozen beacon for chart-list endpoint
- Engineer contract-frozen beacon for CSV export endpoint convention (Accept-header vs `/export` route)
- Engineer PR 0 (separate): Bridge `/api/v1/reports/{kind}` endpoints with mock implementations FED can develop against

— PAO, 2026-05-22
