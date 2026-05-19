# Cross-Tenant Rejection UX — Diagnostic-Non-Leak Invariant

This document captures the **single most security-critical design decision in cohort-2**: how the UI surfaces server-side cross-tenant rejection without leaking diagnostic information that confirms or denies the existence of resources belonging to other tenants.

## The invariant

> A cross-tenant access attempt MUST produce a user-visible error that is **indistinguishable** from a legitimate not-found / empty-state error.

This is a copy of the W#68 PR 3 sec-eng verdict Item 8 finding, applied to every cohort-2 surface:

> Error responses leak tenant-B data through diagnostic messages ("Payment '$id' not found." vs "Payment '$id' is in terminal state 'Bounced'.").

## Why this matters

A multi-tenant SaaS that distinguishes "doesn't exist anywhere" from "exists but you can't see it" is **vulnerable to id-enumeration attacks**: an attacker tries IDs in a range, observes which ones return distinct error messages, and builds up a list of valid IDs from another tenant.

The fix is at the API layer (return identical errors for both cases) AND at the UI layer (don't surface the API's reason-codes as user copy that distinguishes the cases).

## Surfaces in cohort-2 where this applies

### PR 1 — LeaseDetailPage payments

**Surface:** `GET /api/v1/financial/payments?leaseId=...`

**Cross-tenant scenario:** caller asks for payments on a lease ID that belongs to tenant B.

**Server response per handoff §3.14:** empty list (`{ items: [] }`).

**UI behavior:** identical to "this lease exists for me but has no payments yet" — show the empty state copy "No payments recorded yet for this lease." No error surface. No "this lease isn't yours" hint.

**Status:** ✅ INVARIANT HELD — the empty result is the rejection signal.

### PR 2 — AccountingPage

**Surface:** `GET /api/v1/financial/accounting/{summary,outstanding}`

**Cross-tenant scenario:** N/A. Both endpoints are tenant-scoped on the server; they return data for the **caller's** tenant only. There is no scope where the caller passes an ID that could belong to another tenant.

**Status:** ✅ N/A — no cross-tenant surface on this page.

### PR 3 — RentCollectionPage

**Surface:** `POST /api/v1/financial/payments` with `leaseId` in body.

**Cross-tenant scenario:** caller submits a `leaseId` that belongs to tenant B (id-guessing, accidental leak via another channel, etc.).

**Server response per handoff §3.22 step 3:** generic 400 with "lease not found" body. Server-side verifies `leases.GetByIdAsync(tenantContext.TenantId, leaseId, ct)` returns non-null; if null (cross-tenant or genuinely missing), return 400.

**UI behavior (E3 in [`03-rent-collection-page.md`](./03-rent-collection-page.md)):**

```
⚠ We couldn't find that lease

The lease isn't available for payment recording.
Please pick a different lease.

[ Choose another lease ]
```

**Status:** ✅ INVARIANT HELD — the copy is identical to what a legitimate "lease was deleted" or "lease ID is malformed" case would produce. No tenant-B information leak.

## Anti-patterns to avoid

These are example phrasings that **WOULD** violate the invariant. None of them should appear in cohort-2 surfaces:

| ❌ Anti-pattern | Why it leaks |
|---|---|
| "This lease belongs to a different organization." | Confirms tenant-B lease exists with that ID |
| "You don't have permission for this lease." | Implies the lease exists; you just can't see it |
| "Lease ABC-123 not visible from your account." | Echoes the ID + says it exists somewhere |
| "Cross-tenant access denied." | Names the attack vector |
| "Payment in terminal status 'Bounced'." | Confirms lease/payment exists + reveals its state |
| "Invoice #4729 belongs to tenant 'Acme Corp'." | Direct leak of tenant-B data |

## Approved phrasings

For genuinely-missing OR cross-tenant cases, all of these are acceptable (and indistinguishable from each other):

| ✅ Approved | When |
|---|---|
| "We couldn't find that lease." | Lease not found (either reason) |
| "The lease isn't available for payment recording." | Same, slightly more action-oriented |
| "No payments recorded yet for this lease." | Empty-result case (PR 1) |
| "Please pick a different lease." | CTA copy following a not-found surface |

## Sec-eng verification checklist

The sec-eng SPOT-CHECK on cohort-2 PR 3 will verify:

1. The POST handler returns generic 400 on cross-tenant `leaseId` — same response shape as malformed-leaseId case (test: `RecordPayment_CrossTenantLease_Returns400` per handoff §3.26)
2. No audit event fires on the rejected path (test: `RecordPayment_CrossTenantLease_NoAuditEvent`)
3. Server logs MAY differ (defense-in-depth signal for ops; not user-visible)
4. The page-level error copy is identical for cross-tenant and not-found cases (UI test: mock the 400 response and assert the rendered copy)

If any of these fail, PR 3 cannot ship.

## Forward note

This invariant scales to future cohorts. Cohort-3 (reports cluster) MUST apply the same discipline to per-property and per-unit report views. Cohort-4+ continues the pattern.

— PAO, 2026-05-19
