# RentCollectionPage — Cohort-2 PR 3 Design Direction

**Page:** `sunfish/apps/web/src/pages/RentCollectionPage.tsx`
**PR:** W#75 PR 3 (RB-9)
**Endpoint:** `POST /api/v1/financial/payments`
**Pattern:** `@standing-pattern: pattern-009` + `@candidate-pattern: pattern-010-financial-write-path`

This is the **most design-critical** page in cohort-2. It's the first instance of pattern-010 (Bridge POST + CSRF + tenant-derived audit emission + cross-tenant rejection without diagnostic leak); the wireframe MUST make the pattern visible enough that the second financial write-path PR can pattern-match against it.

## Scope

User-facing operation: a property manager / owner records an inbound rent payment from a tenant against a specific lease. The result is a `Payment` entity created on the operator's tenant, an audit-trail event (`PaymentRecorded`), and a confirmation view linking back to the lease's payment history.

This is a **write-path** with three security-critical surfaces:

1. **CSRF antiforgery** — token round-trip before POST
2. **Cross-tenant lease rejection** — server-side check that `leaseId` belongs to the caller's tenant; generic 400 if not (no information leak about tenant B's existence)
3. **Audit-trail emission acknowledgment** — successful submit must produce a visible audit-trail signal in the confirmation UX (pattern-010 visible signature)

## What changes from current (cohort-1 → cohort-2)

The current `RentCollectionPage.tsx` (192 lines) has approximately the right *shape* — single-column form, Shadcn Card layout, confirmation view on success. The cohort-2 rebind is **structural, not visual** for the form proper. The four substantive changes:

| Change | Where | Why |
|---|---|---|
| `recordPayment` import: `@/api/erpnext` → `@/api/financial` | line 5 | Endpoint family rebind |
| CSRF token round-trip added before POST | inside `recordPayment` (api layer) | Pattern-010 invariant |
| DTO mapping: `Lease/Amount/Date/PaymentMethod` → `leaseId/amount/paidAt/currency/direction` (latter two programmatic) | mutation submit | DTO contract per handoff §3.24 |
| Confirmation copy: "Verify in ERPNext admin" → audit-trail visibility text | line 52-53 | ERPNext deprecation + pattern-010 acknowledgment surface |

Plus the **error-state matrix** expands from generic `mutation.error.message` to four explicit states (per handoff §3.25):

- **E1:** token-fetch failure (CSRF token endpoint unreachable / 5xx)
- **E2:** token-rejection on submit (token expired or invalid → suggest reload)
- **E3:** lease-not-found (server-side cross-tenant rejection — generic message, no tenant-B information)
- **E4:** generic 5xx / network error (server reachable but submit failed for other reasons)

## UX flow

```
                       ┌─────────────────────────────┐
                       │  Page mount                 │
                       │  - useLeases() loads        │
                       │    tenant's leases          │
                       └──────────────┬──────────────┘
                                      │
                                      ▼
                       ┌─────────────────────────────┐
                       │  Form: select lease,        │
                       │  enter amount, date,        │
                       │  method                     │
                       └──────────────┬──────────────┘
                                      │
                                      ▼ Submit
                       ┌─────────────────────────────┐
                       │  1. GET csrf token          │
                       │     /api/v1/financial/      │
                       │       antiforgery-token     │
                       └──────────────┬──────────────┘
                                      │
                       ┌──────────────┴──────────────┐
                       │ token OK             token fail
                       ▼                             ▼
       ┌─────────────────────────────┐     ┌─────────────────────────┐
       │ 2. POST /api/v1/financial/  │     │  E1: Token-fetch error  │
       │    payments                 │     │  "Couldn't reach the    │
       │    headers:                 │     │   service. Try again."  │
       │      X-XSRF-TOKEN: <token>  │     │  [Retry] button         │
       │    body: { leaseId, amount, │     └─────────────────────────┘
       │      currency: 'USD',       │
       │      direction: 'Inbound',  │
       │      paidAt, externalRef }  │
       └──────────────┬──────────────┘
                      │
       ┌──────────────┴──────────────┐ ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
       │     2xx                     │ │ 400: bad token  │ │ 400: lease      │ │ 5xx / network   │
       ▼                             ▼ ▼     E2          ▼ ▼     not found   ▼ ▼     E4          ▼
                                         "Session expired."   "We couldn't find  "Something went
                                         "Reload and try"     that lease. Pick   wrong. Try again
                                         [Reload page] btn    a different one."  in a moment."
                                                              [pick different]   [Retry]
       ┌─────────────────────────────┐
       │  Confirmation view:         │
       │  - "Payment recorded"       │
       │  - amount + tenant name +   │
       │    paymentId               │
       │  - "Audit-trail entry       │
       │    emitted." copy          │
       │  - [Record another] [View  │
       │    lease history] actions  │
       └─────────────────────────────┘
```

The CSRF round-trip is **invisible to the user** on the happy path — token fetch + POST happens inside the mutation function. The user sees: click submit → loading spinner → confirmation. The token-fetch step is only surfaced when it fails (E1) or rejects (E2).

## Wireframe spec — happy path form

ASCII wireframe (Yeoman to render formally):

```
┌─────────────────────────────────────────────────────────┐
│  Record Rent Payment                                    │  <-- h1 text-2xl font-bold
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐    │  <-- Card component
│  │  Payment details                                │    │     CardHeader / CardTitle
│  ├─────────────────────────────────────────────────┤    │
│  │  Lease *                                        │    │
│  │  [ Select a lease...                       ▼ ]  │    │  <-- select, populated from
│  │                                                 │    │     useLeases() filtered to
│  │                                                 │    │     status==='Active'
│  │  Amount ($) *                                   │    │
│  │  [ 0.00                                      ]  │    │  <-- number input, min 0.01
│  │  Monthly rent: $1,250                           │    │  <-- helper text once lease
│  │                                                 │    │     selected
│  │                                                 │    │
│  │  Payment date *                                 │    │
│  │  [ 2026-05-19                              📅 ] │    │  <-- date input, default today
│  │                                                 │    │
│  │  Payment method                                 │    │
│  │  [ ACH                                       ▼ ] │    │  <-- select: ACH/Check/Cash/Card
│  │                                                 │    │
│  │  ┌────────────────┐  ┌────────┐                │    │
│  │  │ Record payment │  │ Cancel │                │    │  <-- primary + secondary buttons
│  │  └────────────────┘  └────────┘                │    │     pattern-009 visual baseline
│  └─────────────────────────────────────────────────┘    │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

Layout decisions:

- **Single column** form, max-width `max-w-md` (matches current page).
- **Card wrapper** via Shadcn `<Card>` / `<CardHeader>` / `<CardContent>` / `<CardTitle>` (matches current page).
- **Field stack** with consistent `space-y-4` between fields.
- **Required indicator:** asterisk after label text (`Lease *`) per accessibility baseline.
- **Helper text** below amount field showing monthly rent on lease select (already in current page; keep).
- **Primary/secondary button row** at form bottom: `Record payment` (blue-600) + `Cancel` (border-gray-300).

## Wireframe spec — confirmation view

```
┌─────────────────────────────────────────────────────────┐
│  ┌─────────────────────────────────────────────────┐    │  <-- border-green-200 bg-green-50
│  │  ✓ Payment recorded                             │    │     rounded-lg p-6
│  │                                                 │    │     icon: green checkmark
│  │  $1,250.00 recorded for Maria Santos            │    │
│  │  (ref: pay_01HZX7K3...)                         │    │  <-- paymentId from response
│  │                                                 │    │     truncated for display
│  │                                                 │    │
│  │  ────────────────────────────────────           │    │
│  │  An audit-trail entry has been emitted.         │    │  <-- pattern-010 VISIBLE
│  │  View the lease's payment history to confirm.   │    │     audit-trail signature
│  │                                                 │    │
│  │  ┌──────────────────┐  ┌─────────────────────┐ │    │
│  │  │ Record another   │  │ View lease history  │ │    │
│  │  └──────────────────┘  └─────────────────────┘ │    │
│  └─────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

Key changes from current:
- "Verify in ERPNext admin that the ledger entry is correct" → "An audit-trail entry has been emitted. View the lease's payment history to confirm." (pattern-010 signature)
- "View lease" link → "View lease history" — more specific; goes to the lease's payment section anchor (`/leases/{leaseId}#payments`)
- Visual checkmark icon at top-left of the title (Heroicon `CheckCircleIcon` or equivalent) — reinforces success state

## Error-state wireframes

### E1 — Token-fetch failure (network / 5xx on token endpoint)

```
┌─────────────────────────────────────────────────────────┐
│  ⚠ Couldn't reach the payment service                   │  <-- border-red-200 bg-red-50
│                                                         │     rounded-lg p-4
│  The connection to the payment service failed before    │
│  we could record your payment. Your form is still       │
│  saved — try again in a moment.                         │
│                                                         │
│  ┌─────────┐                                            │
│  │ Try again │                                          │  <-- re-runs the full submit
│  └─────────┘                                            │     (token fetch + POST)
└─────────────────────────────────────────────────────────┘
```

Critical UX point: form state is **preserved** across retry — the lease, amount, date, method stay populated. Only the network call is retried.

### E2 — Token rejection on submit (token expired or invalid)

```
┌─────────────────────────────────────────────────────────┐
│  ⚠ Session expired                                       │
│                                                         │
│  Your session expired before this payment could be      │
│  recorded. Reload the page to start fresh.              │
│                                                         │
│  ┌──────────────┐                                       │
│  │ Reload page  │                                       │  <-- full page reload
│  └──────────────┘                                       │
└─────────────────────────────────────────────────────────┘
```

Reload required (not retry) because the CSRF token endpoint may need a fresh session cookie too. Don't try to be clever with silent retry — explicit "reload" sets the user's expectation correctly.

### E3 — Lease-not-found (cross-tenant rejection — DIAGNOSTIC-NON-LEAK)

```
┌─────────────────────────────────────────────────────────┐
│  ⚠ We couldn't find that lease                          │
│                                                         │
│  The lease isn't available for payment recording.       │
│  Please pick a different lease.                         │
│                                                         │
│  ┌──────────────────────┐                               │
│  │ Choose another lease │                               │  <-- refocuses the lease select
│  └──────────────────────┘                               │
└─────────────────────────────────────────────────────────┘
```

**CRITICAL — DIAGNOSTIC-NON-LEAK INVARIANT (per W#68 PR 3 sec-eng verdict Item 8):**

- The error MUST NOT say "Payment from another tenant" — that confirms a tenant-B lease exists with that ID.
- The error MUST NOT say "Lease ABC-123 belongs to a different organization."
- The error MUST NOT distinguish between "lease ID doesn't exist anywhere" and "lease ID exists but belongs to tenant B."
- The generic "We couldn't find that lease" works for both legitimate missing-lease cases (e.g., the lease was deleted) AND cross-tenant attempted-leak cases.

This is the **single most important security-UX decision in cohort-2**. The sec-eng SPOT-CHECK will verify this on PR 3.

### E4 — Generic 5xx or network failure

```
┌─────────────────────────────────────────────────────────┐
│  ⚠ Something went wrong                                  │
│                                                         │
│  Your payment couldn't be recorded right now. Please    │
│  try again in a moment. If this keeps happening, check  │
│  with your administrator.                               │
│                                                         │
│  ┌───────────┐                                          │
│  │ Try again │                                          │
│  └───────────┘                                          │
└─────────────────────────────────────────────────────────┘
```

Generic enough to cover server-down, DB-down, persistence-failure, etc. without revealing implementation details. Form state preserved (same as E1).

## Component reuse

From existing `@sunfish/ui-react`:
- `<Card>`, `<CardHeader>`, `<CardContent>`, `<CardTitle>` — already used; reuse as-is.

From `@/components/ui/`:
- No new primitives required.

From `@/components/`:
- `AuthRoleGate` — **NEW USAGE** on this page. Current page has no role gate, but rent collection should be gated to `['owner', 'manager']` per cohort-1 MaintenancePage precedent. Recommend wrapping the form in `<AuthRoleGate allow={['owner', 'manager']}>`.

No new components needed. All primitives already exist.

## Design tokens

See [`tokens.md`](./tokens.md) for the full inventory. RentCollectionPage uses:

- **Form fields:** `border-gray-300`, `focus:ring-blue-500`, `text-sm` (existing tokens)
- **Primary button:** `bg-blue-600 hover:bg-blue-700 text-white` (existing)
- **Secondary button:** `border-gray-300 text-gray-700 hover:bg-gray-50` (existing)
- **Success surface:** `border-green-200 bg-green-50 text-green-800` (existing; MaintenancePage uses for confirmation)
- **Error surface:** `border-red-200 bg-red-50 text-red-700` (existing)
- **Status pill (none on this page directly, but lease select shows status implicitly via filter):** N/A

**No new tokens required** for this page.

## Accessibility notes

- All form fields have `<label htmlFor>` association (already done in current page; preserve).
- Required indicators (`*`) are visible but also surfaced via `aria-required="true"` on the input.
- Error states use color (red) + icon + text — not color-alone (WCAG 1.4.1).
- Loading state on submit button uses both text change (`Recording…`) + `aria-busy="true"`.
- Confirmation success surface has `role="status"` for screen-reader announcement.

## Pattern-010 visible signature checklist

For this page to ratify pattern-010 (after the second financial write-path PR), the design direction must make these elements visible:

- [x] **CSRF token placement** — header `X-XSRF-TOKEN` (invisible at user layer; visible in API client code + error states E1+E2)
- [x] **Audit-emission acknowledgment** — confirmation copy explicitly references "audit-trail entry has been emitted"
- [x] **Cross-tenant rejection without diagnostic leak** — E3 generic copy enforces the invariant
- [x] **Pattern-009 baseline reuse** — same form/card/button vocabulary as cohort-1 MaintenancePage

Pattern-010 ratifies on the second financial write-path PR; until then, this page carries `@candidate-pattern: pattern-010-financial-write-path` claim in the PR body.

## Test plan (frontend-design layer)

Tests Yeoman / FED should ensure exist on `RentCollectionPage.test.tsx` (handoff §3.26 already covers backend tests):

- Token-fetch failure → renders E1
- Token-rejection on submit (mock 400 on POST with `X-XSRF-TOKEN` rejection error code) → renders E2 with reload button
- Lease-not-found (mock 400 on POST with generic "lease not found" body) → renders E3, refocuses lease select
- 500 on POST → renders E4 with retry button
- Successful submit → confirmation view shows paymentId + audit-trail copy + "View lease history" link
- Form state preservation across E1/E4 retry (lease/amount/date/method persist)
- AuthRoleGate denies tenant-member roles

## Open question to Engineer (forward-watch, non-blocking)

The `Lease` entity may or may not have a `currency` field. If not (default-USD-everywhere fleet today), the design direction stands as-is (currency hardcoded to USD). If multi-currency lands later, the design direction will need a small revision (currency display on confirmation view; possible currency mismatch UX).

Verify with Engineer during PR 3 implementation.

— PAO, 2026-05-19
