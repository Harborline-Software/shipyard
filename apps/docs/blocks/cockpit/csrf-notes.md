# Cockpit CSRF / Antiforgery Notes

**Added:** W#74 PR 3 (2026-05-18)

## Why CSRF protection is needed

Cockpit POST endpoints use cookie-based authentication (`credentials: 'include'`). Cookie auth +
JSON POST creates a live CSRF surface — a malicious page can silently trigger state-changing
requests using the victim's session cookie.

ASP.NET Core's `app.UseAntiforgery()` auto-validates antiforgery on form-binding endpoints only.
JSON POSTs require **explicit validation**.

## Wiring contract

### Bridge side

1. `Program.cs` configures `AddAntiforgery(opts => opts.HeaderName = "X-XSRF-TOKEN")`.
2. `GET /api/v1/cockpit/antiforgery-token` calls `IAntiforgery.GetAndStoreTokens()`, sets the
   antiforgery cookie, and returns `{ token: string, headerName: "X-XSRF-TOKEN" }`.
3. Each cockpit write handler injects `IAntiforgery` + `HttpContext` and calls
   `await antiforgery.ValidateRequestAsync(httpContext)`. Missing or invalid token → 400.

### Frontend side

Before any cockpit POST/PUT/DELETE:

```ts
// 1. Fetch token (sets the antiforgery cookie on the Bridge domain)
const { token } = await fetch('/api/v1/cockpit/antiforgery-token', { credentials: 'include' })
  .then(r => r.json())

// 2. Include the token as a header on the write request
await fetch('/api/v1/cockpit/work-orders', {
  method: 'POST',
  credentials: 'include',
  headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
  body: JSON.stringify(payload),
})
```

The `useMaintenance.ts` hook's `useCreateWorkOrder()` mutation handles this automatically.
Any future cockpit write hook should follow the same pattern.

## Extending to new write endpoints

Add `IAntiforgery antiforgery, HttpContext httpContext` parameters to the handler and call
`await antiforgery.ValidateRequestAsync(httpContext)` before processing. The frontend hook
must call `getCsrfToken()` (from `@/api/maintenance.ts`) before posting.
