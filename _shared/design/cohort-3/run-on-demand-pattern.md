# Run-on-Demand Pattern — pattern-016 Candidate

This document captures the canonical UX for user-triggered report runs across cohort-3's 4 pages. It is the **visible signature** of pattern-016-run-on-demand-report.

## The pattern (single sentence)

> A report page does NOT auto-fetch on mount; it presents a filter bar with required and optional parameters, waits for the user to click an explicit Run button (enabled only when required parameters are valid), and resets the result to IDLE whenever any parameter changes — making the cost of running the report (server-side cartridge execution, network, render) the user's explicit decision rather than an implicit consequence of navigating to the page.

## Why this pattern exists (substrate context)

Cartridge-backed reports are **expensive** compared to typical CRUD GETs:

- TrialBalance traverses every account in the chart (50–200 rows typical)
- ArAgingSummary aggregates invoices across all customers + properties as-of-date
- ProfitAndLossByProperty joins ledger entries × property × period range
- RentRoll joins units × leases × tenants × delinquency × as-of-date

A single Run can take 1–8 seconds on real data, generates measurable database load, and produces a `snapshotMarker` audit entry. Auto-running on every page mount + every filter keystroke would:

- Hit the database 5–10× per user session for the same data
- Generate spurious snapshot markers that pollute the audit trail
- Make typing into a date picker generate 8 partial-result renders

The cost is high enough that the user clicking Run is the correct boundary — it makes intent explicit and bounded.

## State machine (canonical)

```
IDLE (no params or partial; no result)
  │  user selects required params
  ▼
READY_TO_RUN (required params all valid; no result yet)
  │  user clicks Run
  ▼
LOADING (mutation in flight)
  │  ┌─ success ─▶ SUCCESS (result rendered; CSV export enabled)
  └──┤
     └─ failure ─▶ ERROR (error surface; Retry button)
  │
  │  user changes ANY filter (from SUCCESS, READY_TO_RUN, or ERROR)
  ▼
IDLE (result cleared; Run button re-enabled if params still valid → READY_TO_RUN)
```

**Key invariants:**

1. **No auto-run.** The page never fires the report mutation on its own. Mount only triggers the chart-list fetch (for `<ChartSelector>` population) and any lookup-data prefetches.
2. **No optimistic state.** The result panel stays IDLE until SUCCESS — no skeleton-with-old-data, no "previous result while loading new one."
3. **Filter change resets result.** Any change to any parameter clears the result and returns to IDLE. This prevents users from seeing stale data labeled with new parameters.
4. **Run button gating is local.** Whether Run is enabled is purely a function of "are required params valid?" — no server check, no debounce.

## Filter bar canonical layout

```
+--------------------------------------------------------------------+
| Chart: [Operating accounts ▾]  As of: [2026-05-22]  Period: [— ▾]  |
| [☐ Include zero-balance accounts]  [☐ Include inactive accounts]   |
|                                                                    |
|                                          [Run report]  [Export CSV]|
+--------------------------------------------------------------------+
```

**Layout rules:**

- Required filters come first (Chart, then primary date/period selector)
- Optional toggles + multi-select filters follow on a second wrap row when needed
- Action buttons (Run + Export CSV) align right
- Wrap behavior: at narrow widths, action buttons drop to their own row, full-width on `<sm:` viewports
- `Run` button is `bg-blue-600` primary; `Export CSV` is `border-gray-300` secondary
- `Run` button text changes during loading: `Run report` → `Running…` (with `aria-busy="true"` + spinner)

## Filter-by-filter conventions

### Chart selector

See [Q2 in INDEX.md](./INDEX.md#q2--chartselector-behavior-when-one-vs-n-charts) for the canonical behavior:

- **0 charts:** Run disabled; inline "Set up a chart of accounts" link
- **1 chart:** auto-select; render as a label, not a control
- **N charts:** dropdown; no default selection; Run disabled until chosen

### Date pickers

- **As-of date:** defaults to **today** (operator's local date), formatted as `YYYY-MM-DD` per project convention. Always optional at the API level (cartridges default to today), but the form pre-fills today's date so the value is visible.
- **Period start / Period end:** optional; both empty defaults to "all history for this chart." If only one of the pair is filled, the other auto-fills to chart-history-start or today as appropriate (FED implements the auto-fill on blur).
- **Fiscal period selector:** optional; mutually exclusive with the as-of-date picker. Use a tab-style toggle to switch between modes: `[Date]  [Fiscal period]`. Picking one disables the other; the active mode's value is sent to the cartridge.

### Multi-select filters (property IDs, customer IDs)

- **Empty selection = "all"** — communicated in the placeholder copy: `All properties` (not `Select properties`).
- Use a dropdown with checkboxes for selection; show selected count in the closed state: `3 properties selected`.
- Above the dropdown control, an inline `[Clear]` link appears when any value is selected.

### Boolean toggles

- **Default state:** the most conservative option (e.g., `IncludeZeroBalanceAccounts` defaults OFF — show fewer rows; `IncludeInactiveAccounts` defaults OFF — show fewer rows).
- Render as Tailwind `<input type="checkbox">` with adjacent label.

### Numeric inputs (`expiringWindowDays`, `topDelinquentN`)

- Use `<input type="number">` with `min` + `max` set per FED spec
- Default values pre-filled per spec (`expiringWindowDays=90`, `topDelinquentN=10`)
- 0 is a valid value where it has UX meaning (e.g., `topDelinquentN=0` suppresses the top-delinquent list — make this discoverable via helper text: "Set to 0 to hide this section")

## Result reset semantics

When the user changes ANY parameter after a SUCCESS, the page does the following in one render frame:

1. Result panel transitions to IDLE (cleared; no skeleton)
2. Provisionality banner disappears
3. Export CSV button disables
4. Run button re-enables (assuming required params still valid)

The user sees the filter change take effect AND the result clear simultaneously. There is **no animation, no "are you sure," no confirmation modal** — the cost of an accidental clear is one Run click; the benefit (no stale data shown with new params) is correctness.

**Implementation note for FED:** the React Query `enabled` flag for the report query should be `null` whenever any param has changed since the last successful run. Use a "submitted params" state separate from "form params" — only the submitted params become the query key; the form params get committed on Run click.

```typescript
const [formParams, setFormParams] = useState<TrialBalanceParams | null>(null)
const [submittedParams, setSubmittedParams] = useState<TrialBalanceParams | null>(null)

const query = useTrialBalance(submittedParams)  // null = disabled

const onRun = () => {
  if (formParams && isValid(formParams)) {
    setSubmittedParams(formParams)
  }
}

const onFormChange = (next: TrialBalanceParams) => {
  setFormParams(next)
  setSubmittedParams(null)  // clears result; returns to IDLE
}
```

This implementation pattern is **load-bearing** for the state machine invariants. FED should not optimize it away into a single state object.

## Run button copy + visual states

| State | Button text | Visual | Disabled? | aria-busy |
|---|---|---|---|---|
| IDLE — required missing | `Run report` | `bg-blue-600 opacity-50` | yes | no |
| READY_TO_RUN | `Run report` | `bg-blue-600` | no | no |
| LOADING | `Running…` | `bg-blue-600` + spinner | yes | yes |
| SUCCESS | `Run report` | `bg-blue-600` (re-enabled for a fresh run) | no | no |
| ERROR | `Run report` | `bg-blue-600` (Retry inside error surface is the canonical retry path) | no | no |

The Run button stays the same identity across states; only its enabled state, text, and busy indicator change. Don't replace it with a "Retry" button in the ERROR state — Retry lives inside the error surface, scoped to that surface. Run button always means "run with current form values."

## Export CSV button gating

```
Report state         | Export CSV button
---------------------|-------------------------
IDLE                 | disabled (no result to export)
READY_TO_RUN         | disabled
LOADING              | disabled
SUCCESS              | enabled
ERROR                | disabled (Retry first)
```

See [`csv-export-pattern.md`](./csv-export-pattern.md) for export UX detail.

## Accessibility

- The filter bar uses semantic `<form>` markup; submit on Enter triggers Run (when Run is enabled). This is the keyboard-only path — users don't need to mouse to the Run button.
- All required inputs use `aria-required="true"` and `*` in label.
- Date pickers expose accessible labels; the As-of date defaults to today in `YYYY-MM-DD` format with `aria-label` reading the formatted date naturally ("As of May 22, 2026").
- The Run button is the form's `<button type="submit">`; Export CSV is a separate `<button type="button">`.
- Page-level keyboard shortcut: `R` (when no input is focused) submits Run. This is a power-user affordance; not required for cohort-3 MVP but PAO-recommends — FED's call.

## What this pattern does NOT cover

- **Auto-refresh / polling reports** — pattern-016 is explicitly opt-in-only. If a future dashboard tile needs auto-refresh of a report, it uses a different surface (the tile lives in a dashboard pattern, not a report-page pattern).
- **Saved parameter presets** — the form does not persist params across visits or expose a "saved filters" feature in cohort-3. A future cohort could add this without invalidating pattern-016; persistence is orthogonal to the run-on-demand discipline.
- **Server-side scheduling** — "run this report nightly and email me the CSV" is a forward feature, not in cohort-3. The pattern applies to the interactive page surface only.
- **Optimistic snapshot replay** — pattern-016 does NOT support showing the previous result while a new run is in flight. Some patterns (real-time dashboards) do; reports do not.

## Ratification timeline

- **First instance:** cohort-3 PRs 2–5 each implement the IDLE → READY_TO_RUN → LOADING → SUCCESS state machine consistently
- **Ratification trigger:** second user-triggered report ships clean carrying `@candidate-pattern: pattern-016` claim
- **Likely second instance:** cohort-4 AP Aging (same state machine; identical filter bar pattern)

## Cross-references

- Pattern-011 (provisionality banner) — appears only in SUCCESS state; complements pattern-016 but ratifies independently
- Pattern-013 (CSV export) — Export CSV button gating is defined here; full export UX in [`csv-export-pattern.md`](./csv-export-pattern.md)
- Pattern-009 (Bridge endpoint + frontend rebind pair) — cohort-3 PRs all carry this baseline pattern; run-on-demand is the report-specific *layer* above the read-rebind

— PAO, 2026-05-22
