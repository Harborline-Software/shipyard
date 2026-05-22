# Provisionality Banner Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-3 `<ProvisionalityBanner>` (all 4 cohort-3 report pages)
**Pattern doc:** `cohort-3/provisionality-banner-pattern.md` (pattern-015 candidate)

## Canonical shape (collapsed; default)

```
+--------------------------------------------------------------------+
| ⚠ This report covers an open accounting period and may change as   |
|   transactions are posted.                          [Show details ▾]|
+--------------------------------------------------------------------+
```

## Canonical shape (expanded)

```
+--------------------------------------------------------------------+
| ⚠ This report covers an open accounting period and may change as   |
|   transactions are posted.                          [Hide details ▴]|
|                                                                    |
| Why this report is provisional:                                    |
|   • Fiscal period 2026-05 is open; 12 transactions posted after    |
|     the report run timestamp.                                      |
|   • Property "150 Lexington Ct" has an unposted journal entry      |
|     from 2026-05-18.                                               |
+--------------------------------------------------------------------+
```

## Load-bearing elements

- **Banner container** — `border border-amber-300 bg-amber-50 text-amber-900 rounded-md px-4 py-3` (canonical token: `provisional-surface`)
- **Warning icon** — `ExclamationTriangleIcon w-5 h-5 text-amber-600` (Heroicon outline)
- **Disclosure button** — `text-sm font-medium text-amber-900 underline-offset-2 hover:underline`
- **Disclosure chevron** — `ChevronDownIcon w-4 h-4` (rotated 180° when expanded)
- **Warning list** — `<ul class="list-disc pl-5 mt-2 text-sm text-amber-900 space-y-1">`

## Position on page

**Fixed by pattern:**
- Below page `<h1>` + subtitle paragraph
- Above the filter bar
- Above any tab navigation
- Full content-width (matches page max-width; not edge-to-edge)

This positioning makes the banner the first thing the user reads after the page title — interpretation gets anchored correctly.

## Visibility rules

| Report state | Banner visibility |
|---|---|
| IDLE (no result) | hidden |
| LOADING | hidden |
| SUCCESS, !isProvisional | hidden |
| SUCCESS, isProvisional | visible, collapsed (user can expand) |
| ERROR | hidden |

When `isProvisional === false` the component renders `null` (caller doesn't need conditional rendering at the call site).

## No-dismiss policy

The banner has NO dismiss affordance. The user can collapse details but cannot make the banner go away — the underlying fact (provisional data) hasn't gone away either.

This is a deliberate constraint per `cohort-3/provisionality-banner-pattern.md`. A dismissable warning would let users forget that the data they're looking at is preliminary.

## Empty warnings fallback

If `warnings.length === 0` but `isProvisional === true`, the disclosure shows generic fallback copy:

> Why this report is provisional:
> - This report includes transactions from an open accounting period. The exact reason is not recorded.

This is defensive; well-behaved cartridges always emit at least one warning when `isProvisional` is true.

## Accessibility

- Banner: `role="status"` + `aria-live="polite"` (informational, NOT action-required)
- Disclosure: `aria-expanded={true|false}` + `aria-controls="provisional-warnings-list"`
- Warning list: `id="provisional-warnings-list"`
- Color is NOT the sole signal — the `⚠` icon + textual "may change" copy convey meaning to users who don't perceive amber

## When to use

- Any report/data surface where the result envelope carries an `isProvisional` flag
- Any surface where the underlying data can change post-display in ways the user should know about

## When NOT to use

- Toast/transient notifications (use a different pattern)
- Per-cell or per-row provisionality (would require a different design; not in cohort-3 scope)
- Action-required warnings (those use `role="alert"` not `role="status"`)

## Cross-references

- Pattern doc: `cohort-3/provisionality-banner-pattern.md` (pattern-015 candidate)
- Component: `<ProvisionalityBanner>` in `apps/web/src/components/`
- Token: `provisional-surface` (canonical, cohort-3 `tokens.md`)
- Composes with: report-page-composition (sits between H1 and filter bar)
