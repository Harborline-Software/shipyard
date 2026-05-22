# Filter Bar Pattern

**Pattern type:** Structural (page-level)
**First instance:** cohort-3 `<ReportFilterBar>` (all 4 cohort-3 report pages)
**Pattern doc:** `cohort-3/run-on-demand-pattern.md` (pattern-016 candidate)

## Canonical shape

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]  As of: [2026-05-22]  Period: [— ▾]  |
| [☐ Include zero-balance accounts]  [☐ Include inactive accounts]   |
|                                                                    |
|                                          [Run report]  [Export CSV]|
+--------------------------------------------------------------------+
```

## Load-bearing elements

- **Required filters first** (left side; Chart selector, primary date/period)
- **Optional toggles + multi-selects** on a second wrap row when needed
- **Action buttons right-aligned** — `[Run report]` (primary `bg-blue-600`) + `[Export CSV]` (secondary `border-gray-300`)
- **Wrap behavior:** at narrow widths, action buttons drop to their own row, full-width on `<sm:`
- **Form-submit-on-Enter:** semantic `<form>` markup with Enter submitting Run when valid

## State variants

### IDLE (required missing)

```
+--------------------------------------------------------------------+
| Chart: [Select chart ▾]  As of: [2026-05-22]                       |
|                                                                    |
|                                          [Run report]  [Export CSV]|
|                                          (disabled)    (disabled)  |
+--------------------------------------------------------------------+
```

### READY_TO_RUN (required filters set)

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]  As of: [2026-05-22]                 |
|                                                                    |
|                                          [Run report]  [Export CSV]|
|                                          (enabled)     (disabled)  |
+--------------------------------------------------------------------+
```

### LOADING (run in flight)

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]  As of: [2026-05-22]      (dimmed)   |
|                                                                    |
|                                          [Running… ⟳]  [Export CSV]|
|                                          (disabled)    (disabled)  |
+--------------------------------------------------------------------+
```

### SUCCESS (post-run; ready for next action)

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]  As of: [2026-05-22]                 |
|                                                                    |
|                                          [Run report]  [Export CSV]|
|                                          (enabled)     (enabled)   |
+--------------------------------------------------------------------+
```

## When to use

- Any page where the user must explicitly trigger an operation (pattern-016 reports; future scheduled-runs; any non-trivial data fetch)
- Any page with multiple parameters that change the result

## When NOT to use

- Simple list pages without parameter selection (use a single search-input pattern)
- Mutation forms (use the cohort-2 RentCollectionPage form pattern)
- Read-on-mount pages (no Run button needed)

## Cross-references

- Pattern doc: `cohort-3/run-on-demand-pattern.md`
- Component: `<ReportFilterBar>` in `apps/web/src/components/`
- Token: secondary button → `border border-gray-300 text-gray-700 hover:bg-gray-50 text-sm font-medium rounded-md px-4 py-2`
- Token: primary button → `bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 text-white text-sm font-medium rounded-md px-4 py-2`
