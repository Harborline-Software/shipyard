# AccountingPage Design Direction

**Page:** `sunfish/apps/web/src/pages/AccountingPage.tsx`
**PR:** W#75 PR 2 (RB-7)
**Endpoints:** `GET /api/v1/financial/accounting/summary` + `GET /api/v1/financial/accounting/outstanding`
**Pattern:** `@standing-pattern: pattern-009`

## Scope

AccountingPage shows the operator's AR (accounts receivable) position: aggregated totals via the `/summary` endpoint and per-invoice outstanding rows via the `/outstanding` endpoint. Both are tenant-scoped server-side (no client-supplied tenant param).

This page existed before cohort-1 (it already consumes ERPNext via `getAccountingSummary()` + `getAccountingOutstanding()`); PR 2 is a **mechanical client-import rebind**. The DTO field shapes are preserved per handoff §3.17 — *"Do not invent new field names — preserve the React-side type shape to keep the rebind mechanical."*

## What changes

| Change | Where | Why |
|---|---|---|
| Import swap: `@/api/erpnext` → `@/api/financial` | top of file | endpoint family rebind |
| TanStack query keys preserved | hooks | mechanical rebind |
| Cross-tenant case now returns zero balances (not error) | display | per `GetSummary_CrossTenant_ZeroBalances` test |

The **visual layout does not change** for this page beyond what's already shipped. The design direction documents the current shape so cohort-3 / future redesigns have a baseline reference and Yeoman can render the wireframe formally.

## UX flow

```
AccountingPage mount
    ├─> useAccountingSummary()   -> aggregated totals
    └─> useAccountingOutstanding() -> outstanding invoice rows

Both queries run in parallel. Page renders summary cards above the outstanding table.
```

## Wireframe spec

```
┌─────────────────────────────────────────────────────────┐
│  Accounting                                             │  <-- h1 text-2xl
│  Summary of your receivables                            │  <-- p text-sm text-gray-500
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Summary tiles (4-up grid)                              │
│  ─────────────────────────────────────────────────────  │
│  ┌────────────────┐ ┌────────────────┐ ┌────────────┐   │
│  │ Invoiced       │ │ Received       │ │ Outstanding│   │  <-- Card components
│  │  $48,500       │ │  $42,250       │ │  $6,250    │   │     CardHeader (sm label)
│  │  this period   │ │  this period   │ │  >30 days: │   │     CardContent (large num)
│  │                │ │                │ │  $1,200    │   │     CardFooter (sub-detail)
│  └────────────────┘ └────────────────┘ └────────────┘   │
│  ┌────────────────┐                                     │
│  │ Aging 60+      │                                     │
│  │  $450          │                                     │
│  │  2 invoices    │                                     │
│  └────────────────┘                                     │
│                                                         │
│  Outstanding invoices                                   │  <-- h2 text-lg
│  ─────────────────────────────────────────────────────  │
│  ┌─────────────────────────────────────────────────┐    │
│  │ INVOICE      │ LEASE      │ AMOUNT  │ DAYS DUE │    │  <-- table
│  │ ──────────── │ ────────── │ ─────── │ ──────── │    │
│  │ inv_2K9X..   │ Maria S.   │ $1,250  │ 32 days  │    │  <-- "days due" pill if >30
│  │ inv_3F4D..   │ John W.    │ $1,200  │ 15 days  │    │
│  │ ...                                            │    │
│  └─────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

Layout decisions:

- **4-up summary tile grid** at top using `grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4`.
- Each tile is a Shadcn `<Card>` with: small label (gray-500), large amount (text-2xl font-semibold gray-900), sub-detail copy (gray-500 text-xs).
- **Outstanding invoice table** below tiles — same table vocabulary as MaintenancePage / LeaseDetailPage.
- **Days-due pill** on rows where age > 30 days: yellow pill (`bg-yellow-100 text-yellow-700`) for 30-60; orange (`bg-orange-100 text-orange-700`) for 60-90; red (`bg-red-100 text-red-700`) for 90+.

## States

| State | Trigger | UX |
|---|---|---|
| Loading | both `isPending` true | All 4 tiles show skeleton numbers; table shows 3-row skeleton |
| Empty (zero AR) | `summary.outstanding === 0` AND `outstanding.length === 0` | Tiles show $0; table shows "No outstanding invoices." (positive copy — this is a good state) |
| Partial empty | summary non-zero, outstanding empty | Tiles render; table shows "No outstanding invoices currently." |
| Error (either endpoint) | `isError` on either query | Red surface above tiles + table area; Retry button retries both |
| Cross-tenant | (handled server-side as zero / empty) | Same as Empty — no leak |

The cross-tenant case is **silent** at the API layer (zero balances + empty outstanding). The user sees the same UX as an actual zero-AR state. No information leak about other tenants.

## Component reuse

From `@/components/ui/`:
- `<Card>` family — already used; reuse for summary tiles.

No new components.

## Design tokens

All existing. New token suggestion for **days-due pill** color scale:

| Days due | Token (existing) | Visual |
|---|---|---|
| 0-30 | (no pill) | plain text |
| 31-60 | `bg-yellow-100 text-yellow-700` | yellow |
| 61-90 | `bg-orange-100 text-orange-700` | orange |
| 90+ | `bg-red-100 text-red-700` | red |

These are existing Tailwind classes — no new token additions to the design system. The semantic mapping (days-due bucket → color) lives in this page's local logic.

**However:** consider whether a `<StatusPill>` primitive should be promoted to `@sunfish/ui-react` after cohort-2 ships. Both MaintenancePage (work-order status) and AccountingPage (aging buckets) repeat the same pattern. See [`component-reuse-audit.md`](./component-reuse-audit.md).

## Accessibility

- Days-due pill uses both color AND text content (not color-alone).
- Summary tile labels have `aria-describedby` linking to the sub-detail copy.
- Table follows the same a11y conventions as LeaseDetailPage payments table.

## Pattern alignment

`@standing-pattern: pattern-009` — two GETs, both tenant-scoped, mechanical rebind.

— PAO, 2026-05-19
