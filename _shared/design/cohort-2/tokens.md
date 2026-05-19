# Cohort-2 Design Tokens — Inventory

This file inventories which design tokens cohort-2 uses, which are existing vs new, and which gaps warrant promotion to `@sunfish/ui-react` after the cohort lands.

## Reference: framework design tokens

Live at `shipyard/_shared/design/tokens-guidelines.md`. Cohort-2 inherits these without modification.

## Cohort-2 token usage by page

### LeaseDetailPage (PR 1)

| Concern | Token | Status |
|---|---|---|
| Section divider | `border-gray-200` | existing |
| Table border | `border-gray-200 rounded-lg` | existing |
| Table header text | `text-xs font-medium uppercase tracking-wide text-gray-500` | existing |
| Empty-state body text | `text-sm text-gray-500` | existing |
| CTA button (primary) | `bg-blue-600 hover:bg-blue-700 text-white text-sm` | existing |
| Loading skeleton | `bg-gray-100 animate-pulse` | existing |
| Error surface | `border-red-200 bg-red-50 text-red-700` | existing |

**No new tokens.**

### AccountingPage (PR 2)

| Concern | Token | Status |
|---|---|---|
| Summary tile card | `border-gray-200 bg-white rounded-lg` (via Shadcn `<Card>`) | existing |
| Summary tile label | `text-sm text-gray-500` | existing |
| Summary tile value | `text-2xl font-semibold text-gray-900` | existing |
| Summary tile sub-detail | `text-xs text-gray-500` | existing |
| Days-due pill: 31-60 | `bg-yellow-100 text-yellow-700` | existing |
| Days-due pill: 61-90 | `bg-orange-100 text-orange-700` | existing |
| Days-due pill: 90+ | `bg-red-100 text-red-700` | existing |
| Pill base | `inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium` | existing (matches MaintenancePage STATUS_COLORS convention) |
| Outstanding table | (matches LeaseDetailPage) | existing |

**No new tokens.** The days-due pill colors compose existing Tailwind classes.

### RentCollectionPage (PR 3)

| Concern | Token | Status |
|---|---|---|
| Form field border | `border-gray-300 rounded-md` | existing |
| Form field focus | `focus:ring-1 focus:ring-blue-500 focus:border-blue-500` | existing |
| Form field padding | `px-3 py-2 text-sm` | existing |
| Form label | `text-sm font-medium text-gray-700` | existing |
| Helper text | `text-xs text-gray-500` | existing |
| Primary button | `bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded` | existing |
| Secondary button | `border-gray-300 text-gray-700 hover:bg-gray-50 text-sm rounded` | existing |
| Success surface (confirmation) | `border-green-200 bg-green-50 text-green-800 rounded-lg p-6` | existing |
| Error surface (E1/E2/E3/E4) | `border-red-200 bg-red-50 text-red-700 rounded-lg p-4` | existing |
| Disabled state | `disabled:opacity-50` | existing |
| Card container | (via Shadcn `<Card>`) | existing |
| AuthRoleGate wrap | (component-level; no token) | existing |

**No new tokens.**

## Gap candidates for `@sunfish/ui-react` promotion (cohort-2 → cohort-3)

These patterns repeat across cohort-1 + cohort-2 and warrant promotion to the package after cohort-2 lands:

### `<StatusPill>` primitive

Both MaintenancePage (`STATUS_COLORS` map: Draft / Sent / Accepted / Scheduled / InProgress / Completed / OnHold / Cancelled) and AccountingPage (days-due buckets) use the same pill base + a semantic-color mapping. A `<StatusPill kind="workOrderStatus" value={status}>` or `<StatusPill kind="agingBucket" days={daysDue}>` API would consolidate the inline classes.

**Promotion criteria:** wait for a third pill consumer (likely cohort-3 reports cluster) before promoting. Current state is "two-instance pattern" — viable but not yet ratified.

### `<DataTable>` primitive

LeaseDetailPage payment table + AccountingPage outstanding table + MaintenancePage work-order table all share the same shape: rounded-lg border-gray-200 wrapper, gray-50 header, hover-on-row, mono-font ID column truncated to last 8 chars, etc. A reusable `<DataTable columns={...} rows={...}>` would consolidate.

**Promotion criteria:** worth scoping after cohort-3 reports tables ship; reports tables have different requirements (sorting, pagination) so the API contract is bigger.

### `<ErrorSurface variant="retryable">` primitive

E1/E4 share the same shape: red icon + title + body + retry button. The variants are predictable:
- `variant="retryable"` — Try again button
- `variant="reload"` — Reload page button (E2)
- `variant="redirect"` — Choose another lease (E3-style)

**Promotion criteria:** worth doing now (cohort-2) since RentCollectionPage alone has 4 error variants. Recommend Yeoman draft the component during PR 3 execution + promote in cohort-3 cleanup.

### `<ConfirmationSurface>` primitive

Confirmation views (MaintenancePage create-success, RentCollectionPage record-success) use the same shape: green-50/200 surface, semibold title, body + action row. Worth promoting alongside `<ErrorSurface>`.

## No new design tokens for cohort-2

All page implementations stay within the existing token system. The cohort introduces **no design-language changes** — purely endpoint/data rebinds with consistent visual vocabulary.

— PAO, 2026-05-19
