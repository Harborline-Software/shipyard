# Provisionality Banner Pattern — pattern-015 Candidate

This document captures the canonical UX for surfacing `ReportRunResult.IsProvisional = true` across cohort-3's 4 reports. It is the **visible signature** of pattern-015-provisional-report-surface.

## The pattern (single sentence)

> When a report's result envelope reports `IsProvisional = true` (because the report covers transactions in an open accounting period), the page surfaces an amber banner immediately below the page title and above the filter controls, with the warnings list collapsed by default behind a "Show details" disclosure.

## What "provisional" means (substrate context)

The `ReportRunResult<TResult>` envelope (defined in `Sunfish.Blocks.Reports.Models.ReportRunResult`) carries two fields the page must surface:

- **`IsProvisional: bool`** — true when the report covers any open fiscal period. Implies the numbers can change as more transactions get posted before period close.
- **`Warnings: string[]`** — zero or more strings explaining specific provisionality conditions. Examples:
  - `"Fiscal period 2026-05 is open; 12 transactions posted after the report run timestamp."`
  - `"Property '150 Lexington Ct' has an unposted journal entry from 2026-05-18."`
  - `"As-of date is in the future; results extrapolated from open transactions."`

Warnings always accompany `IsProvisional = true`. (Cartridge code SHOULD NOT emit warnings without setting `IsProvisional`; conversely, a `true` flag with empty warnings is acceptable but unusual.)

## Endpoint contract

Provisionality lives in the response envelope — no separate endpoint, no separate header. Every report endpoint already returns `ReportRunResult<TResult>`:

```typescript
interface ReportRunResult<TResult> {
  kind: string
  result: TResult
  runAtUtc: string
  snapshotMarker: string
  runDuration: string
  isProvisional: boolean
  warnings: string[]
}
```

`isProvisional` and `warnings` are the only fields this pattern touches. The frontend never POSTs to a "provisionality" endpoint.

## Banner copy

### Default copy (all reports)

> ⚠ This report covers an open accounting period and may change as transactions are posted. **[Show details ▾]**

This is the [Q4-approved](./INDEX.md#q4--provisionalitybanner-copy) copy. The mechanism phrasing ("may change as transactions are posted") names the actual user-visible event instead of the abstract "period close."

### Expanded details copy

When the user clicks "Show details," the warnings list expands inline:

> Why this report is provisional:
> - Fiscal period 2026-05 is open; 12 transactions posted after the report run timestamp.
> - Property "150 Lexington Ct" has an unposted journal entry from 2026-05-18.

Bulleted list of the `warnings[]` strings verbatim. **PAO does NOT rewrite warnings** — they are cartridge-authored and may name specific entities (period IDs, property names, dates) that come from data. The frontend treats them as opaque strings.

If `warnings.length === 0` but `isProvisional === true`, the disclosure shows generic fallback copy:

> Why this report is provisional:
> - This report includes transactions from an open accounting period. The exact reason is not recorded.

(This is a defensive fallback. In practice, well-behaved cartridges always emit at least one warning when `isProvisional` is true.)

## Visual design — wireframe

```
+--------------------------------------------------------------------+
|  Trial Balance                                                     |
|  Run on demand against any chart of accounts                       |
+--------------------------------------------------------------------+

+--------------------------------------------------------------------+
| ⚠ This report covers an open accounting period and may change as   |
|   transactions are posted.                          [Show details ▾]|
+--------------------------------------------------------------------+

[Filter bar: Chart | As of | Period | Run | Export CSV]

[Report content below]
```

When expanded:

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

## Visual tokens

| Element | Token | Source |
|---|---|---|
| Banner container | `border border-amber-300 bg-amber-50 text-amber-900 rounded-md px-4 py-3` | new canonical `provisional-surface` (see [`tokens.md`](./tokens.md)) |
| Warning icon | `ExclamationTriangleIcon w-5 h-5 text-amber-600` (Heroicon outline) | existing |
| Disclosure button | `text-sm font-medium text-amber-900 underline-offset-2 hover:underline` | existing |
| Disclosure chevron | `ChevronDownIcon w-4 h-4` (rotated 180° when expanded) | existing |
| Warning list | `<ul class="list-disc pl-5 mt-2 text-sm text-amber-900 space-y-1">` | existing |

The amber family (300 border / 50 background / 900 text / 600 icon) is the canonical `provisional-surface` combination. This is its first canonical use; future "this is provisional / pending / unposted" surfaces (e.g., a future "Unposted journal entries" badge on Property pages) SHOULD compose the same combination.

## Position on page

**Position is fixed by pattern:**

- Below page `<h1>` + subtitle paragraph
- Above the filter bar
- Above any tab navigation (if a future report adds tabs)
- Full content-width (matches the page max-width container; not edge-to-edge)

This positioning makes the banner the **first thing the user reads after the page title** — they see the provisionality warning before they see the parameters or the data, anchoring interpretation correctly.

**Why not below the filter bar (closer to the data)?** Because the filter bar is interactive — the banner would compete with controls for the user's first attention. Placing it above the controls keeps the controls' visual hierarchy clean.

**Why not as a toast/transient notification?** Because provisionality persists across re-runs (and across the entire viewing session for that result). Persistent state needs persistent surface; toasts are for transient events.

## State machine

```
Report state                 | Banner state
-----------------------------|------------------------------------
IDLE (no result)             | hidden (nothing to be provisional about)
LOADING                      | hidden (no result yet; don't preempt)
SUCCESS, !isProvisional      | hidden
SUCCESS, isProvisional       | visible, collapsed (user can expand)
ERROR                        | hidden (error surface owns the screen)
```

The banner appears **only on SUCCESS with `isProvisional === true`**. It does NOT appear pre-run; the user's selection of parameters can't be diagnosed as provisional until the report actually runs and the cartridge inspects the data.

Re-running the report with new parameters resets the banner to its initial collapsed state if `isProvisional` flips back to true. If `isProvisional` flips to false, the banner disappears.

## Accessibility

- Banner container: `role="status"` (NOT `role="alert"` — this is informational, not action-required)
- `aria-live="polite"` on the banner so screen readers announce it when it first appears after a Run, without interrupting any in-progress speech
- Disclosure button: `aria-expanded={true|false}` + `aria-controls="provisional-warnings-list"`
- Warning list: `id="provisional-warnings-list"`
- Color is NOT the sole signal — the `⚠` icon + textual "may change" copy communicate the meaning to users who don't perceive amber

## Interaction details

- **Initial state:** collapsed. The user sees the headline copy + disclosure button.
- **Click "Show details":** the warnings list expands inline (no modal, no popover); button text + chevron flip to "Hide details ▴".
- **Click "Hide details":** collapses back; state is **per-result**, not persisted across re-runs.
- **No "Acknowledge" or "Dismiss" affordance:** the banner is informational; the user can't dismiss it, because the underlying fact (provisional data) hasn't gone away. Hiding details is the only state change available.

This is a deliberate choice — a dismissable provisional warning would let users forget that the data they're looking at is preliminary, undermining the entire reason the banner exists.

## What this pattern does NOT cover

- **Per-cell provisionality** — if a cartridge ever needs to flag specific rows or cells as provisional (e.g., "this account's balance is provisional but the others are final"), that's a separate pattern; not in cohort-3 scope.
- **Provisional CSV exports** — the CSV export (see [`csv-export-pattern.md`](./csv-export-pattern.md)) does NOT include provisionality information inline. The exporter is responsible for naming the filename with a `-provisional` suffix when `isProvisional === true` so the file's nature is self-describing.
- **Auto-refresh of provisional reports** — the banner doesn't poll. If the user wants fresher numbers, they click Run again.

## Ratification timeline

- **First instance:** cohort-3 PR 1 ships `<ProvisionalityBanner>` + cohort-3 pages PR 2–5 each consume it
- **Ratification trigger:** second cohort using `IsProvisional` semantics ships clean carrying `@candidate-pattern: pattern-015` claim
- **Likely second instance:** cohort-4 AP Aging (which inherits the same `IsProvisional` semantics from cartridge substrate) or a forward report (Cash Flow, Balance Sheet)

## Cross-references

- Pattern-009 (Bridge endpoint + frontend rebind pair) — cohort-3 PRs all carry this; provisionality is a **layer on top of** the read-rebind, not a replacement
- Pattern-011 ratifies independently of pattern-016 (run-on-demand) and pattern-017 (CSV export). A future surface could use provisionality without being run-on-demand (e.g., a dashboard tile auto-refreshing every 5min that still shows `isProvisional`).

— PAO, 2026-05-22
