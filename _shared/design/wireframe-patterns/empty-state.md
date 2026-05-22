# Empty State Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-1 LeasesPage / MaintenancePage; cohort-2 LeaseDetailPage payments; cohort-3 all 4 report pages
**Pattern doc:** none (universal convention)

## Canonical shape (informational)

```
+--------------------------------------------------------+
| No accounts found for this chart and period.           |
|                                                        |
| Adjust the chart selection or date range and try again.|
+--------------------------------------------------------+
```

## Canonical shape (positive — "all done")

```
+--------------------------------------------------------+
| No outstanding receivables.                            |
|                                                        |
| All customers are current as of 2026-05-22.            |
+--------------------------------------------------------+
```

## Canonical shape (with CTA)

```
+--------------------------------------------------------+
| No payments recorded yet for this lease.               |
|                                                        |
| ┌──────────────────────────┐                          |
| │ Record the first payment │                          |
| └──────────────────────────┘                          |
+--------------------------------------------------------+
```

## Load-bearing elements

- **Container** — `text-sm text-gray-500 p-6` (centered or left-aligned per page convention)
- **Title** — short; names the empty condition (`text-base font-medium text-gray-700`)
- **Body** — optional 1-2 sentences; tells the user what to do or why this is OK
- **CTA** — optional; primary-button-styled action if there's a natural next step

## Empty state variants by semantic

The empty state shape varies based on whether the empty result is informational, positive, or actionable:

| Variant | Visual signal | Copy register | Examples |
|---|---|---|---|
| Informational | Neutral gray | Neutral; suggests filter adjustment | "No accounts found for this chart and period" (TrialBalance), "No activity in this period and chart" (P&L), "No units found" (RentRoll) |
| Positive | Neutral gray with celebratory sub-copy | Friendly; affirms the empty state IS the desired state | "No outstanding receivables. All customers are current as of {asOfDate}." (ArAging) |
| Actionable | Neutral gray + CTA button | Suggests next action | "No payments recorded yet for this lease. [Record the first payment]" (LeaseDetailPage payments empty) |

## Per-page empty copy convention (cohort-3 cohort)

| Page | Empty copy |
|---|---|
| TrialBalance | "No accounts found for this chart and period." |
| ArAging (positive empty) | "No outstanding receivables." + sub-copy "All customers are current as of {asOfDate}." |
| P&L | "No activity in this period and chart." |
| RentRoll | "No units found for this chart and date." |
| LeaseDetailPage payments | "No payments recorded yet for this lease." + CTA "[Record the first payment]" |

Per-cartridge semantic; copy lives in the per-page direction docs.

## Position on page

- **Replaces the data region** of the page (not full-page) — header/filter-bar stays visible above
- Centered horizontally + ~6rem vertical padding for breathing room
- Not full-page-height — keeps the empty state from feeling like an error

## When to use

- Successful query that returned zero results (positive empty when zero is the desired state; informational otherwise)
- Newly-created entities with no children yet (actionable variant with CTA)
- After filter changes that exclude all matching records (informational; hint at filter adjustment)

## When NOT to use

- Errors (use error-surface — red, not gray)
- Loading states (use skeleton pattern; not yet in this library)
- Cross-tenant rejection (per diagnostic-non-leak invariant, folds into the standard empty state — same copy as a legitimate empty)

## Cross-references

- Component: NOT yet a shared primitive; inline convention
- Token: empty container → `text-sm text-gray-500 p-6` + `text-base font-medium text-gray-700` for title
- Composes with: table-tfoot (replaces it when zero rows); confirmation-surface (sometimes invites "no further action; return to list")
- Cross-tenant invariant: `cohort-2/cross-tenant-rejection-ux.md`
