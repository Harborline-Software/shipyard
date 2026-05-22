# Tile Grid Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-2 AccountingPage summary tiles; cohort-3 PortfolioSummaryTiles (P&L 3-tile) + PortfolioSummaryBar (RentRoll 5-tile) + ArAgingTotalsBar (6-tile)
**Promotion candidate:** `<PortfolioSummary>` (deferred per cohort-3 `component-reuse-audit.md`)

## Canonical shape (3-tile, P&L variant)

```
+-------------+  +-------------+  +-------------+
| REVENUE     |  | EXPENSES    |  | NET INCOME  |
| $42,500     |  | $12,500     |  | $30,000     |
| (green)     |  | (red)       |  | (green/red) |
+-------------+  +-------------+  +-------------+
```

## Canonical shape (5-tile, RentRoll variant)

```
+------------+ +------------+ +------------+ +------------+ +------------+
| OCCUPANCY  | | PROPERTIES | | UNITS      | | MONTHLY    | | OPEN       |
|            | |            | |            | | RENT       | | BALANCE    |
| 87%        | | 3          | | 23         | | $42,200    | | $1,840     |
+------------+ +------------+ +------------+ +------------+ +------------+
```

## Canonical shape (6-tile, ArAging TotalsBar variant)

```
+--------+ +--------+ +--------+ +--------+ +--------+ +--------+
| CURRENT| | 0-30 d | | 31-60 d| | 61-90 d| | 90+ d  |▎| TOTAL  |
|        | |        | |  (amber|  (orange|   (red    |   (blue
|        | |        | |  ▎tint)|   ▎tint)|   ▎tint)  |   ▎left
| $12k   | | $3,400 | | $1,200 | | $500   | | $2,400 |▎| $19.5k |
+--------+ +--------+ +--------+ +--------+ +--------+ +--------+
```

The Total tile is differentiated by a left border (`border-l-4 border-blue-500`) — the bucket tiles are grouped; the Total stands apart.

## Load-bearing elements

- **Each tile** — `rounded-lg border border-gray-200 bg-white px-3 py-2` (or px-4 py-3 for larger variant)
- **Label** — `text-xs uppercase tracking-wide text-gray-500`
- **Value** — `text-2xl font-semibold tabular-nums` (or `text-lg` for compact; `text-3xl` for primary KPI)
- **Color-coded values** when applicable:
  - Revenue / positive Net Income: `text-green-700`
  - Expenses / negative Net Income / overdue balances: `text-red-700`
  - Neutral (currency totals; counts): `text-gray-900`
- **Sub-detail** below the value (optional): `text-xs text-gray-500`

## Responsive behavior

| Tile count | sm: breakpoint | md+: breakpoint |
|---|---|---|
| 3 tiles | `grid-cols-1 sm:grid-cols-3` | unchanged |
| 4 tiles | `grid-cols-2 sm:grid-cols-4` | unchanged |
| 5 tiles | `grid-cols-2 sm:grid-cols-4 lg:grid-cols-5` | unchanged |
| 6 tiles | `grid-cols-2 sm:grid-cols-3 lg:grid-cols-6` | unchanged |

At narrowest viewport: 1-col (3-tile) or 2-col (4+). Tiles never wrap mid-row at md+.

## Primary KPI emphasis

When one tile is more important than the others (e.g., RentRoll Occupancy Rate as the primary metric):

```
+-------------+  +------------+ +------------+ +------------+ +------------+
| OCCUPANCY   |  | PROPERTIES | | UNITS      | | MONTHLY    | | OPEN       |
|             |  |            | |            | | RENT       | | BALANCE    |
| 87%         |  | 3          | | 23         | | $42,200    | | $1,840     |
| (text-3xl)  |  | (text-2xl) | | (text-2xl) | | (text-2xl) | | (text-2xl) |
+-------------+  +------------+ +------------+ +------------+ +------------+
```

The primary KPI uses `text-3xl` while the secondary KPIs use `text-2xl`. Subtle hierarchy.

## When to use

- Top-of-page summary of portfolio/aggregate metrics
- Anywhere "at-a-glance" numeric overview is wanted
- Filter-bar-adjacent metrics (e.g., 5-tile portfolio bar above the per-property blocks)

## When NOT to use

- Single metric (use inline display)
- More than 6 tiles (consider a small table or sub-categorization)
- Mixed metric types where comparison doesn't apply (group similar metrics; separate dissimilar)

## Cross-references

- Component: page-local; `<PortfolioSummary>` promotion candidate (3rd-instance trigger; cohort-4 AP Aging or Cash Flow may qualify)
- Token: tile → `rounded-lg border border-gray-200 bg-white px-3 py-2`
- Composes with: report-page-composition (typically above the data section); accordion-list (sits above accordion rows in P&L)
