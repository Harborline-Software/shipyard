# CSRF UX Pattern — pattern-010 Candidate

This document captures the canonical CSRF round-trip UX for cohort-2 PR 3 (RentCollectionPage) and forward financial write paths. It is the **visible signature** of pattern-010-financial-write-path.

## The pattern (single sentence)

> A write-path POST is preceded by a same-origin GET that fetches an antiforgery token; the POST carries that token in the `X-XSRF-TOKEN` header; the user sees the token transaction only when it fails (token unreachable → E1) or rejects (token invalid → E2).

## Endpoint contract

Per [Q1 in INDEX.md](./INDEX.md#q1--csrf-endpoint-location-convention-engineer--onr), the recommended convention is:

- **Token endpoint:** `GET /api/v1/financial/antiforgery-token`
- **Response:** `{ "token": "<antiforgery token string>" }`
- **POST header:** `X-XSRF-TOKEN: <token>`

This mirrors cohort-1's shipped `getCsrfToken()` in `sunfish/apps/web/src/api/maintenance.ts`. ONR's handoff §3.25 snippet shows a divergent convention (`/antiforgery/token` + `RequestVerificationToken`); the canonical pattern is cohort-1's. PR 3 implementation confirms the actual endpoint location with Engineer.

## Frontend implementation

```typescript
// sunfish/apps/web/src/api/financial.ts

export async function getCsrfToken(): Promise<string> {
  const resp = await fetch('/api/v1/financial/antiforgery-token', {
    credentials: 'include',
  })
  if (!resp.ok) {
    throw new TokenFetchError(`Token fetch failed: ${resp.status}`)
  }
  const body = (await resp.json()) as { token: string }
  return body.token
}

export async function recordPayment(
  input: RecordPaymentInput,
): Promise<RecordPaymentResult> {
  const token = await getCsrfToken()  // E1 if this throws
  const resp = await fetch('/api/v1/financial/payments', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-XSRF-TOKEN': token,
    },
    body: JSON.stringify(input),
  })
  if (!resp.ok) {
    // Parse the error class — see error-discrimination section below
    throw classifyPostError(resp)
  }
  return (await resp.json()) as RecordPaymentResult
}
```

## Error discrimination

The mutation function throws typed errors that the page-level UI can dispatch on:

```typescript
class TokenFetchError extends Error { kind = 'token-fetch' as const }      // E1
class TokenRejectionError extends Error { kind = 'token-rejection' as const } // E2
class LeaseNotFoundError extends Error { kind = 'lease-not-found' as const }  // E3
class GenericServerError extends Error { kind = 'server-error' as const }     // E4

function classifyPostError(resp: Response): Error {
  // 400 with specific error code in body → discriminate
  // 401 → auth issue (different from CSRF)
  // 500/502/503 → GenericServerError
  // ...
}
```

The exact error-shape contract is FED+Engineer territory during PR 3 implementation. The design direction asserts only that **four distinct error states are reachable from the page UI** (see [`03-rent-collection-page.md`](./03-rent-collection-page.md) wireframes E1-E4).

## User-visible UX summary

| Step | What happens | User sees |
|---|---|---|
| 1 | Page mount | Form, leases dropdown |
| 2 | User fills form, clicks Submit | Button: "Recording…" with spinner |
| 3a | Token fetch succeeds (happy path) | (invisible) |
| 3b | Token fetch fails | E1 surface w/ Try again |
| 4a | POST succeeds | Confirmation view |
| 4b | POST rejected: token invalid | E2 surface w/ Reload page |
| 4c | POST rejected: lease not found | E3 surface w/ Choose another lease |
| 4d | POST rejected: 5xx | E4 surface w/ Try again |

## The audit-trail visible signature

Pattern-010 also requires the **audit-emission acknowledgment** to be visible in the confirmation view. Per [`03-rent-collection-page.md`](./03-rent-collection-page.md):

> An audit-trail entry has been emitted. View the lease's payment history to confirm.

This copy is the explicit pattern-010 signature. Subsequent financial write-path PRs ratifying pattern-010 SHOULD echo a parallel acknowledgment in their confirmation surfaces (e.g., "An audit-trail entry has been emitted. View the [contextual entity]'s history to confirm.").

## What this pattern does NOT cover

- **Non-financial write paths** — cockpit `POST /work-orders` predates this pattern (pattern-009 family). The CSRF mechanism is the same but the audit-emission acknowledgment is not yet present on MaintenancePage's confirmation. A future cohort could backport the visible-signature.
- **GET requests** — antiforgery is a write-path concern. GETs don't need tokens.
- **Multi-step write flows** — pattern-010 covers single-POST flows. Multi-step (e.g., a payment-application that touches multiple entities) may need a wrapper pattern; defer to when the use case arrives.

## Ratification timeline

- **First instance:** cohort-2 PR 3 (this design direction)
- **Ratification trigger:** second financial write-path PR ships clean carrying `@candidate-pattern: pattern-010` claim
- **Likely second instance:** future PR for payment-application creation (currently W#68 PR 3 substrate; UI is forward-watched)

— PAO, 2026-05-19
