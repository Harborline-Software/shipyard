# Report Page Composition Pattern

**Pattern type:** Composition (multi-component; page-level)
**First instance:** cohort-3 (all 4 report pages: TrialBalance, ArAging, P&L by Property, RentRoll)
**Pattern docs:** `cohort-3/run-on-demand-pattern.md` (pattern-016) + `cohort-3/provisionality-banner-pattern.md` (pattern-015) + `cohort-3/csv-export-pattern.md` (pattern-017)

This is the canonical cohort-3 report-page composition — how the surface patterns fit together to form a complete report page.

## Canonical shape (SUCCESS state, with provisional result)

```
┌────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                     │  ← Page H1
│  Run on demand against any chart of accounts                       │  ← Subtitle
├────────────────────────────────────────────────────────────────────┤
│  ⚠ This report covers an open accounting period and may change as  │  ← provisionality-banner
│    transactions are posted.                          [Show details ▾]│     (pattern-015; visible when isProvisional)
├────────────────────────────────────────────────────────────────────┤
│  Chart: [Operating accounts ▾]  As of: [2026-05-22]                │  ← filter-bar
│  [☐ Include zero-balance accounts]  [☐ Include inactive accounts]  │     (pattern-016)
│                                       [Run report]   [Export CSV]  │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  +-------------------------------------------------------------+   │
│  |  CODE     │  ACCOUNT NAME           │ TYPE    │ DEBIT │ ... |   │  ← table-tfoot
│  |  ───────  │  ─────────────────────  │ ───────│ ──────│ ─── |   │     (data region)
│  |  1100     │  Cash                   │ Asset  │ 12,500│     |   │
│  |  ...                                                        |   │
│  |  ─────────────────────────────────────────────────────────  |   │
│  |  TOTAL                                       │ 47,800│ ... |   │
│  +-------------------------------------------------------------+   │
│                                                                    │
│  Balanced  ✓                                                       │  ← Status badge
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

## Canonical shape (LOADING state)

```
┌────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                     │
│  Run on demand against any chart of accounts                       │
├────────────────────────────────────────────────────────────────────┤
│  Chart: [Operating accounts ▾]  As of: [2026-05-22]  (dimmed)      │
│  [☐ Include zero-balance accounts]  [☐ Include inactive accounts]  │
│                                       [Running… ⟳]   [Export CSV]  │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒    │  ← Skeleton rows
│  ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒    │
│  ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒    │
│  ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒    │
│  ▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒    │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

## Canonical shape (ERROR state)

```
┌────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                     │
│  Run on demand against any chart of accounts                       │
├────────────────────────────────────────────────────────────────────┤
│  Chart: [Operating accounts ▾]  As of: [2026-05-22]                │
│  [☐ Include zero-balance accounts]  [☐ Include inactive accounts]  │
│                                       [Run report]   [Export CSV]  │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  +--------------------------------------------------------+        │
│  | ⚠ Couldn't load trial balance                          |        │  ← error-surface
│  |                                                        |        │     (retryable variant)
│  | The report service didn't respond. Try again in a      |        │
│  | moment.                                                |        │
│  |                                                        |        │
│  | ┌─────────┐                                            |        │
│  | │ Retry   │                                            |        │
│  | └─────────┘                                            |        │
│  +--------------------------------------------------------+        │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

## Canonical shape (EMPTY state)

```
┌────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                     │
│  Run on demand against any chart of accounts                       │
├────────────────────────────────────────────────────────────────────┤
│  Chart: [Operating accounts ▾]  As of: [2026-05-22]                │
│  [☐ Include zero-balance accounts]  [☐ Include inactive accounts]  │
│                                       [Run report]   [Export CSV]  │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  No accounts found for this chart and period.                      │  ← empty-state
│                                                                    │
│  Adjust the chart selection or date range and try again.           │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

## Composition rules

1. **Order is fixed:** H1/subtitle → provisionality banner (when visible) → filter bar → data region
2. **The filter bar stays visible** across all data-region states (LOADING / SUCCESS / EMPTY / ERROR)
3. **The provisionality banner is only visible on SUCCESS+isProvisional** — disappears on filter change (which resets to IDLE)
4. **The data region** is a slot that contains exactly one of: skeleton rows (LOADING) / table / accordion list / tile grid / property blocks (SUCCESS) / error surface (ERROR) / empty state (EMPTY)
5. **For pages with multiple result regions** (e.g., ArAging has TotalsBar + 2 tables + TopDelinquentList), the data region is the union; all sub-regions render together

## Variant compositions

The data region varies by page:

| Page | Data-region composition |
|---|---|
| TrialBalance | table-tfoot + BalanceBadge |
| ArAging | tile-grid (TotalsBar) + table-tfoot ×2 (Customer + Property) + ranked list (TopDelinquent) |
| P&L | tile-grid (PortfolioSummary) + accordion-list (PropertyAccordionList) |
| RentRoll | tile-grid (PortfolioSummaryBar) + property-block ×N (PropertyBlock with UnitTable inside) |

Each variant composes from the canonical sub-patterns; the page-level composition rule is unchanged.

## State machine canonical reference

Per `cohort-3/run-on-demand-pattern.md`:

```
IDLE (no params or partial; no result)
  │  user selects required params
  ▼
READY_TO_RUN (required params all valid; no result yet)
  │  user clicks Run
  ▼
LOADING (mutation in flight)
  │  ┌─ success ─▶ SUCCESS (result rendered; CSV export enabled)
  └──┤
     └─ failure ─▶ ERROR (error surface; Retry button)
  │
  │  user changes ANY filter (from SUCCESS, READY_TO_RUN, or ERROR)
  ▼
IDLE (result cleared; Run button re-enabled if params still valid → READY_TO_RUN)
```

## When to use

- Any cohort-3-style report page (cohort-4 AP Aging is the next expected consumer)
- Any user-triggered, parameterized data fetch where the result might be empty/error/provisional
- Any page where the patterns 015/016/017 apply

## When NOT to use

- Read-on-mount pages (no Run button; different page composition)
- Mutation-form pages (use the cohort-2 RentCollectionPage form pattern)
- Detail views (use definition-list or single-record pattern; not in this library yet)

## Cross-references

- Pattern docs: `cohort-3/run-on-demand-pattern.md` (pattern-016) + `cohort-3/provisionality-banner-pattern.md` (pattern-015) + `cohort-3/csv-export-pattern.md` (pattern-017)
- All sub-patterns: filter-bar, table-tfoot, tile-grid, provisionality-banner, error-surface, empty-state (cited above)
- Component: composes from cohort-3 PR 1 shared components (`<ProvisionalityBanner>`, `<ExportCsvButton>`, `<ReportFilterBar>`, `<ChartSelector>`, `<RunButton>`, `<ErrorSurface>`)
