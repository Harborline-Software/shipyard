# Confirmation Surface Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-1 MaintenancePage create-success; cohort-2 RentCollectionPage record-success
**Promotion candidate:** `<ConfirmationSurface>` (deferred — only 2 instances; need 3rd write surface to promote per cohort-3 `component-reuse-audit.md`)

## Canonical shape

```
+--------------------------------------------------------+
| ✓ Payment recorded                                     |
|                                                        |
| pay_8XK3JF2 — $1,250.00 — 2026-05-22                   |
|                                                        |
| An audit-trail entry has been emitted. View the        |
| lease's payment history to confirm.                    |
|                                                        |
| ┌─────────────────┐ ┌──────────────────────┐          |
| │ Record another  │ │ View lease history   │          |
| └─────────────────┘ └──────────────────────┘          |
+--------------------------------------------------------+
```

## Load-bearing elements

- **Container** — `border border-green-200 bg-green-50 text-green-800 rounded-lg p-6`
- **Icon** — `CheckCircleIcon w-5 h-5 text-green-600` (Heroicon outline)
- **Title** — short; names what happened (`text-base font-semibold text-green-900`)
- **Identifier line** (when applicable) — the created record's ID + key facts (`text-sm font-mono text-green-700`)
- **Body** — 1-2 sentences; for pattern-010 financial write paths, MUST include the audit-emission acknowledgment ("An audit-trail entry has been emitted. View the [contextual entity]'s history to confirm.")
- **Action row** — 2 buttons:
  - Primary (left): `[Record another]` resets the form for repeat use
  - Secondary (right): `[View entity history]` navigates to the parent entity's view

## Audit-trail acknowledgment (pattern-010 visible signature)

Per `cohort-2/csrf-ux-pattern.md`, financial write paths MUST include the audit-emission acknowledgment copy in the confirmation. This is the pattern-010 candidate's visible signature:

> An audit-trail entry has been emitted. View the [entity]'s history to confirm.

Subsequent financial write-path PRs ratifying pattern-010 SHOULD echo a parallel acknowledgment with the contextual entity (e.g., "View the lease's payment history" / "View the tenant's payment history" / etc.).

## Variants

The pattern can vary the action-row composition per use case:

| Variant | Primary action | Secondary action |
|---|---|---|
| Form-reset (most common) | Record another | View entity history |
| Navigation (cohort-1 MaintenancePage create) | View work order | Back to list |
| Settings-style (future) | Done | (no secondary) |

## Position on page

- **Replaces the form region** of the page (not full-page) — header/breadcrumbs stay visible above
- For modal/dialog write paths, the confirmation surface IS the entire modal content

## Accessibility

- Container: `role="status"` + `aria-live="polite"` (success message; not action-required but worth announcing)
- Color is NOT the sole signal — the `✓` icon + textual title + body convey meaning to users who don't perceive green
- Action buttons: standard `<button>` semantics

## When to use

- After a successful write/mutation (create / update / delete with confirmation)
- After a multi-step flow completes
- Anywhere the user took an action and the system confirmed success

## When NOT to use

- Read-only data loads (no confirmation needed)
- Failed actions (use error-surface)
- Toast notifications (use a separate toast pattern; confirmation surface is more weight than a toast)

## Cross-references

- Pattern doc: `cohort-2/csrf-ux-pattern.md` (audit-emission acknowledgment as pattern-010 visible signature)
- Component: NOT yet a shared `<ConfirmationSurface>` primitive; promotion deferred (3rd write surface needed)
- Token: success/confirmation surface → `border border-green-200 bg-green-50 text-green-800 rounded-lg p-6`
- Composes with: empty-state (if the confirmation invites "no further action; return to list")
