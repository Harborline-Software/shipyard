# Table + tfoot Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-1 MaintenancePage table; cohort-2 LeaseDetailPage payment table; cohort-3 TrialBalanceTable + AgingTable + UnitTable
**Promotion candidate:** `<DataTable>` (deferred to cohort-4+ per cohort-3 `tokens.md`)

## Canonical shape

```
+-------------------------------------------------------------+
|  CODE     │  ACCOUNT NAME           │ TYPE    │ DEBIT │ ... |
|  ───────  │  ─────────────────────  │ ───────│ ──────│ ─── |
|  1100     │  Cash                   │ Asset  │ 12,500│     |
|  1200     │  Accounts Receivable    │ Asset  │  3,200│     |
|  2100     │  Accounts Payable       │ Liab.  │       │ ... |
|  ─────────────────────────────────────────────────────────  |
|  TOTAL                                       │ 47,800│ ... |
+-------------------------------------------------------------+
|  Balanced  ✓                                                |
+-------------------------------------------------------------+
```

## Load-bearing elements

- **Outer table container** — `rounded-lg border border-gray-200 overflow-hidden`
- **Header row** — `bg-gray-50 text-xs font-medium uppercase tracking-wide text-gray-500 px-3 py-2 text-left`
- **Body cells (text)** — `px-3 py-2 text-sm text-gray-900`
- **Body cells (numeric)** — `px-3 py-2 text-sm text-gray-900 text-right tabular-nums`
- **Type column** — inline pill (uses `<StatusPill>` with appropriate kind)
- **Zero values in numeric columns** — display as `—` (`text-gray-400`); not `$0.00`
- **`<tfoot>` total row** — bold, `border-t` separator, sums the numeric columns
- **Status badge below tfoot** — when applicable (e.g., BalanceBadge); separate row OR floats below the table

## Sticky thead variant

For long tables (50+ rows, e.g., TrialBalance):

```
+-------------------------------------------------------------+
|  CODE     │  ACCOUNT NAME           │ TYPE    │ DEBIT │ ... |  ← sticky
|  ───────  │  ─────────────────────  │ ───────│ ──────│ ─── |
|  1100     │  Cash                   │ Asset  │ ...   │     |
|  ... (200 rows; scrollable region)                          |
+-------------------------------------------------------------+
```

Apply `sticky top-0 z-10` to `<thead>`. Container needs `max-height` + `overflow-y-auto`. The tfoot is NOT sticky — it stays at the bottom of the data, scrolled with it.

## Tinted headers variant (AgingTable)

When the header semantics carry color meaning (e.g., aging buckets):

```
+-------------------------------------------------------------+
|  NAME           │ Current │ 0-30 │ 31-60* │ 61-90* │ 90+*   |
|  ─────────────  │ ─────── │ ──── │ ────── │ ────── │ ─────  |
                       *amber  *orange  *red header backgrounds
```

Apply `bg-amber-50 text-amber-900` (31-60), `bg-orange-50 text-orange-900` (61-90), `bg-red-50 text-red-900` (90+) to the respective `<th>` elements. The tints are one shade LIGHTER than the corresponding `<AgingBucketPill>` chip backgrounds so cells + headers visually pair without competing.

## Property/section-grouped variant

When wrapped inside a property-block, the table loses its outer card border (the divider-bar IS the container):

```
+--------------------------------------------------------+
| 150 Lexington Ct                                       |  ← property-block header
| 12 units | 10 occupied (83%) | ...                     |
+--------------------------------------------------------+
|  UNIT  │  TENANT  │  ...                               |  ← table-tfoot starts here
|  101   │  Maria   │  ...                               |     (no outer rounded border)
|  102   │  James   │  ...                               |
+--------------------------------------------------------+
```

## When to use

- Any list of records with consistent column shape
- When totals/footers matter (otherwise use the no-tfoot variant — same shape minus the bottom row)
- When tabular alignment is important (financial data; numeric comparison)

## When NOT to use

- Heterogeneous data (use cards or accordions)
- Single-record detail views (use definition list pattern, not currently in this library)
- Very short data (<5 rows) where a card pattern works better

## Accessibility

- Use semantic `<thead>` / `<tbody>` / `<tfoot>` with `scope="col"` on headers
- Numeric cells: `tabular-nums` for both visual + screen-reader decimal alignment
- Status pills inside cells: pill text is the primary signal; color is supplemental
- Sortable columns (cohort-4+): use `aria-sort="ascending|descending|none"` on `<th>`

## Cross-references

- Component: NOT yet a shared `<DataTable>` primitive; promotion deferred
- Token: outer container → `rounded-lg border border-gray-200 overflow-hidden`
- Token: header cell → `bg-gray-50 text-xs font-medium uppercase tracking-wide text-gray-500`
- Composes with: status-pill pattern (for inline badge cells); empty-state pattern (when no rows)
