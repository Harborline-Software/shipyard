# LeaseDetailPage — Payments Section Design Direction

**Page:** `sunfish/apps/web/src/pages/LeaseDetailPage.tsx` (payments section only)
**PR:** W#75 PR 1 (RB-8)
**Endpoint:** `GET /api/v1/financial/payments?leaseId=`
**Pattern:** `@standing-pattern: pattern-009`

## Scope

LeaseDetailPage is an existing page with a payments section that currently shows a placeholder banner ("Payment history will appear after the next migration step" — cohort-1 H5 deferral). PR 1 wires the section to the new financial-cluster endpoint.

This is a **read-only mechanical rebind** — no CSRF, no audit-emission UX, no new patterns. The design direction is short because the page mostly just needs the placeholder replaced with a payment list.

## What changes

| Change | Where | Why |
|---|---|---|
| Remove H5 placeholder banner | LeaseDetailPage.tsx payments section | endpoint now exists |
| Add `usePayments(lease.leaseId)` hook call | LeaseDetailPage payments section | wire to new endpoint |
| Field mapping: `payment.name` → `payment.paymentId`, `payment.posting_date` → `payment.receivedAt`, etc. | display layer | DTO contract mirror |
| Empty/loading/error states wired explicitly | new code | replaces placeholder |

## UX flow

```
LeaseDetailPage mount
    └─> useLease(leaseId)  -> lease data
    └─> usePayments(leaseId) -> payment list

Payment list states:
    isPending  -> Loading row placeholder (3-row skeleton)
    isError    -> Error surface w/ Retry
    success    -> [] → empty state w/ helpful copy
                  [...] → table rows
```

## Wireframe spec — payments section

```
┌─────────────────────────────────────────────────────────┐
│  [other LeaseDetailPage sections above — header,        │
│   tenant info, lease terms, etc.]                       │
│                                                         │
│  ─────────────────────────────────────────────────────  │
│                                                         │
│  Payment history                                        │  <-- h2 text-lg font-semibold
│  ─────────────────────────────────────────────────────  │
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │  <-- table card, rounded-lg
│  │  ID         │ DATE       │ AMOUNT    │ METHOD   │    │     border-gray-200
│  │  ─────────  │ ────────── │ ───────── │ ──────── │    │
│  │  pay_8XK3.. │ 2026-05-01 │ $1,250.00 │ ACH      │    │
│  │  pay_7HJ2.. │ 2026-04-01 │ $1,250.00 │ Check    │    │
│  │  pay_6BD9.. │ 2026-03-01 │ $1,250.00 │ ACH      │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  ┌──────────────────────────┐                          │
│  │ + Record a new payment   │                          │  <-- link/button to
│  └──────────────────────────┘                          │     RentCollectionPage with
│                                                         │     ?lease=<leaseId> prefilled
└─────────────────────────────────────────────────────────┘
```

Layout decisions:

- **Table-style list** at full-section width — same vocabulary as MaintenancePage work-order table (cohort-1 baseline).
- **ID column** shows last 8 chars (matches MaintenancePage `wo.workOrderId.slice(-8)` convention).
- **Date column** ISO format `YYYY-MM-DD`.
- **Amount column** locale-formatted with `$` prefix (assume USD; see Q1 in INDEX).
- **Method column** plain text (no pill — payment method isn't a workflow-state).
- **"+ Record a new payment" affordance** below the table, linking to RentCollectionPage with `?lease={leaseId}` query string (already supported by current RentCollectionPage line 16).

## Empty state

```
┌─────────────────────────────────────────────────────────┐
│  Payment history                                        │
│  ─────────────────────────────────────────────────────  │
│                                                         │
│  No payments recorded yet for this lease.               │
│                                                         │
│  ┌──────────────────────────┐                          │
│  │ Record the first payment │                          │
│  └──────────────────────────┘                          │
└─────────────────────────────────────────────────────────┘
```

When a lease exists but has no payments yet — common for newly-created leases. Copy is encouraging-not-warning. CTA is the same as the "+ Record a new payment" button but with adjusted phrasing.

## Loading state

3-row skeleton with grey placeholder bars (`bg-gray-100 animate-pulse h-8`). Matches MaintenancePage's `Loading work orders…` text-only approach is acceptable too — Yeoman picks whichever fits the table-row visual better. Recommend skeleton rows for visual continuity (the user sees the table will be there).

## Error state

```
┌─────────────────────────────────────────────────────────┐
│  ⚠ Couldn't load payment history                        │
│                                                         │
│  We couldn't fetch this lease's payment history.        │
│  Try again in a moment.                                 │
│                                                         │
│  ┌─────────┐                                            │
│  │ Retry   │                                            │
│  └─────────┘                                            │
└─────────────────────────────────────────────────────────┘
```

Standard error surface (red-200/50). The cross-tenant-rejection case (caller asks for payments on a lease in tenant B) returns **empty array** per handoff §3.14 test `ListPayments_CrossTenant_LeaseFromOtherTenant_ReturnsEmpty` — not 403/404. So this page never sees a "tenant mismatch" error; the cross-tenant guard is silent at the API layer (empty result is indistinguishable from "lease has no payments").

That's the right design — leaks zero information about other tenants' leases.

## Component reuse

- Table primitives: standard `<table>` with Tailwind classes (matches MaintenancePage).
- No new components.

## Design tokens

All existing — no new tokens for this page.

## States summary

| State | Trigger | UX |
|---|---|---|
| Loading | `isPending` true | 3-row skeleton |
| Empty | `data?.items.length === 0` | "No payments recorded yet" + CTA |
| Success | `data?.items.length > 0` | Table rows + "+ Record" button |
| Error | `isError` true | Red surface + Retry button |
| Cross-tenant | (handled server-side as empty array) | Same as Empty state — no leak |

## Accessibility

- Table uses `<thead>` / `<tbody>` semantic markup with `scope="col"` on headers.
- Loading skeletons have `aria-busy="true"` + visually-hidden "Loading payments" announcement.
- Empty state CTA has descriptive label, not just "Click here".

## Pattern alignment

`@standing-pattern: pattern-009` — formal post-cohort-1. Simple GET-with-filter rebind; no new pattern claims.

— PAO, 2026-05-19
