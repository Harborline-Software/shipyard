# States Matrix — Per-Page Empty / Loading / Success / Error Variants

Consolidates the state matrix for all 3 cohort-2 pages in one table for FED's quick reference.

## LeaseDetailPage (PR 1)

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| Loading | `usePayments` `isPending` | 3-row table skeleton | none (passive) |
| Empty | `data.items.length === 0` (legit or cross-tenant) | "No payments recorded yet for this lease." | `[Record the first payment]` → RentCollectionPage |
| Success | `data.items.length > 0` | Table rows | `[+ Record a new payment]` below table |
| Error | `isError` true | Red surface w/ message | `[Retry]` re-fetches |
| Cross-tenant | (server returns empty) | Same as Empty | (no leak) |

## AccountingPage (PR 2)

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| Loading | either `useAccountingSummary` or `useAccountingOutstanding` `isPending` | Tiles show skeleton numbers; table shows 3-row skeleton | none |
| Empty | `summary.outstanding === 0` AND `outstanding.length === 0` | Tiles show $0; table: "No outstanding invoices." | none (this is a positive state) |
| Partial empty | summary non-zero; outstanding empty | Tiles render normally; table: "No outstanding invoices currently." | none |
| Success | both queries succeed with data | Tiles render; table rows | (none on page — read-only) |
| Error | either query `isError` | Red surface above tiles | `[Retry]` re-fetches both |
| Cross-tenant | (server-scoped to caller) | N/A — no cross-tenant scope here | N/A |

## RentCollectionPage (PR 3)

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| Form (initial) | page mount, `useLeases` succeeded | Empty form, lease dropdown populated | `[Record payment]` submit, `[Cancel]` back |
| Leases loading | `useLeases` `isPending` | Lease select disabled w/ "Loading…" placeholder | none |
| Leases error | `useLeases` `isError` | Red surface above form | `[Retry]` re-fetch leases |
| Submitting | `mutation.isPending` | Submit button: "Recording…" (disabled + spinner) | none (button disabled) |
| Success (confirmation) | `mutation.isSuccess` | Green confirmation surface with paymentId + audit-trail note | `[Record another]` reset form, `[View lease history]` → `/leases/{id}#payments` |
| E1 — Token-fetch failure | `TokenFetchError` thrown | Red surface "Couldn't reach the payment service" | `[Try again]` re-runs full submit (token + POST) |
| E2 — Token rejection | `TokenRejectionError` thrown (POST 400 w/ CSRF reason) | Red surface "Session expired" | `[Reload page]` full reload |
| E3 — Lease not found | `LeaseNotFoundError` thrown (POST 400 w/ generic body) | Red surface "We couldn't find that lease" | `[Choose another lease]` refocus lease select |
| E4 — Generic 5xx / network | `GenericServerError` thrown | Red surface "Something went wrong" | `[Try again]` re-runs full submit |
| Cross-tenant | (folded into E3 — generic copy) | E3 surface | E3 affordance |

### RentCollectionPage state-preservation contract

| State transition | Form values preserved? |
|---|---|
| Form → Submitting → Success | (form values not relevant; confirmation view replaces) |
| Form → Submitting → E1 / E4 → retry | ✅ YES (lease/amount/date/method persist) |
| Form → Submitting → E2 → reload | ❌ NO (full page reload; user expects fresh start) |
| Form → Submitting → E3 → choose another lease | ✅ YES (lease cleared; amount/date/method persist) |

## Universal a11y annotations

- All loading skeletons: `aria-busy="true"` + visually-hidden "Loading…" announcement
- All error surfaces: `role="alert"` + Heroicon `ExclamationTriangleIcon` (red-600) — color is NOT the sole signal
- All success surfaces: `role="status"` + Heroicon `CheckCircleIcon` (green-600)
- All forms: required indicators are `aria-required="true"` on the input PLUS `*` in label text

— PAO, 2026-05-19
