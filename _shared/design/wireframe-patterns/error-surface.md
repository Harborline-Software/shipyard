# Error Surface Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-2 LeaseDetailPage + RentCollectionPage error states (inline classes); cohort-3 PR 1 `<ErrorSurface>` promotion
**Pattern doc:** none (universal convention)

## Canonical shape (retryable)

```
+--------------------------------------------------------+
| ⚠ Couldn't load trial balance                          |
|                                                        |
| The report service didn't respond. Try again in a      |
| moment.                                                |
|                                                        |
| ┌─────────┐                                            |
| │ Retry   │                                            |
| └─────────┘                                            |
+--------------------------------------------------------+
```

## Canonical shape (reload variant)

```
+--------------------------------------------------------+
| ⚠ Session expired                                      |
|                                                        |
| Your session has expired. Reload the page to continue. |
|                                                        |
| ┌───────────────┐                                      |
| │ Reload page   │                                      |
| └───────────────┘                                      |
+--------------------------------------------------------+
```

## Canonical shape (redirect variant)

```
+--------------------------------------------------------+
| ⚠ Lease not found                                      |
|                                                        |
| We couldn't find the lease you were working with.      |
|                                                        |
| ┌──────────────────────────────┐                       |
| │ Choose another lease         │                       |
| └──────────────────────────────┘                       |
+--------------------------------------------------------+
```

## Load-bearing elements

- **Container** — `border border-red-200 bg-red-50 text-red-700 rounded-lg p-4` (or `p-6` for prominent variants)
- **Icon** — `ExclamationTriangleIcon w-5 h-5 text-red-600` (Heroicon outline)
- **Title** — short; names the failure (`text-base font-semibold text-red-800`)
- **Body** — 1-2 sentences; explains what happened + what to do (`text-sm text-red-700`)
- **Action button** — primary action variant-specific (Retry / Reload page / Choose another)

## Variants

The cohort-3 PR 1 `<ErrorSurface>` component supports three variants:

| Variant | Button | When to use |
|---|---|---|
| `retryable` | Retry | Transient failure; retry will likely succeed (5xx, network) |
| `reload` | Reload page | Session/auth failure; full page reload needed (401, CSRF token expired) |
| `redirect` | Choose another X | Resource not found; user needs to navigate (404, cross-tenant rejection) |

## Position on page

- **Replaces the data region** of the page (not full-page) — filter bar stays visible above
- When applicable, **error surface replaces what would have been the result content**
- For form-error states (e.g., RentCollectionPage E1-E4), error surface appears between form + submit area

## Diagnostic-non-leak invariant

Per cohort-2 `cross-tenant-rejection-ux.md`:

When the failure could leak information about other tenants (e.g., a user asks for a lease in tenant B from tenant A's session), the error message MUST NOT disclose whether the resource exists. The cross-tenant case folds into the generic "not found" message — same copy as a legitimately-missing resource.

Per the cohort-2 invariant: the API returns 200-with-empty for cross-tenant rejection (LeaseDetailPage payments specifically); other surfaces may return 404 with generic copy.

## Accessibility

- Container: `role="alert"` + `aria-live="assertive"` (user-initiated action just failed; needs immediate feedback)
- Color is NOT the sole signal — the `⚠` icon + textual title + body copy convey meaning to users who don't perceive red
- Action button: standard `<button>` semantics; keyboard-accessible

## When to use

- API failures (4xx/5xx) where the page-level UI needs to surface the failure
- Network errors
- Auth/session failures
- Cross-tenant rejection (with diagnostic-non-leak care)

## When NOT to use

- Field-level form validation errors (use inline field-error styling)
- Toast notifications (use a separate toast pattern — not in this library yet)
- Empty states (use empty-state pattern — `text-gray-500`, not red)

## Cross-references

- Component: `<ErrorSurface variant=...>` in `apps/web/src/components/`
- Token: error surface → `border border-red-200 bg-red-50 text-red-700 rounded-lg p-4`
- Cross-tenant invariant: `cohort-2/cross-tenant-rejection-ux.md`
- Composes with: filter-bar (sits above; remains visible during error)
