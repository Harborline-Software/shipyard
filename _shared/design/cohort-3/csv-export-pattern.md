# CSV Export Pattern — pattern-017 Candidate

This document captures the canonical CSV export UX for cohort-3's 4 reports. It is the **visible signature** of pattern-017-csv-export-affordance.

## The pattern (single sentence)

> An `Export CSV` button sits adjacent to the Run button in the filter bar, disabled until a successful report result is loaded; when clicked, it re-runs the same parameters with an `Accept: text/csv` header (or a `/export` route variant — pending Engineer contract), triggers a blob download with a deterministic filename, and surfaces only failure UI inline as a 3-second toast.

## Endpoint contract

**Pending halt condition** ([Pending halt conditions](./INDEX.md#pending-halt-conditions-engineer-side)): Engineer's contract-frozen beacon will specify one of:

- **Option A — Accept header:** `POST /api/v1/reports/{kind}` with `Accept: text/csv` and same JSON body returns CSV bytes
- **Option B — separate route:** `POST /api/v1/reports/{kind}/export` with same JSON body returns CSV bytes

The PAO direction is **identical for both options**. The visible UX never references the endpoint shape; FED swaps the fetch implementation when the contract lands.

## Filename convention

Filenames are deterministic and self-describing:

| Report | Filename template | Example |
|---|---|---|
| TrialBalance | `trial-balance-{asOfDate}.csv` | `trial-balance-2026-05-22.csv` |
| ArAgingSummary | `ar-aging-summary-{asOfDate}.csv` | `ar-aging-summary-2026-05-22.csv` |
| ProfitAndLossByProperty | `pnl-by-property-{periodStart}-to-{periodEnd}.csv` | `pnl-by-property-2026-01-01-to-2026-05-22.csv` |
| RentRoll | `rent-roll-{asOfDate}.csv` | `rent-roll-2026-05-22.csv` |

**Rules:**

- Date format: `YYYY-MM-DD` everywhere (no localization in filenames; consistent for sort order)
- If `asOfDate` is absent (cartridge defaulted to today), use today's date as if explicit
- For P&L, if `periodStart` is absent (cartridge defaulted to chart history), use the chart's earliest transaction date — FED retrieves from `result.periodStart` (which the cartridge always populates with the actual range used)
- Hyphens, not underscores, as separators (matches URL slug convention and macOS Finder display)
- All lowercase

### Provisional-result suffix

When `isProvisional === true`, append `-provisional` before `.csv`:

| Report | Filename example |
|---|---|
| TrialBalance | `trial-balance-2026-05-22-provisional.csv` |
| ProfitAndLossByProperty | `pnl-by-property-2026-01-01-to-2026-05-22-provisional.csv` |

This makes the file self-describing — a user emailing the CSV around knows it's provisional from the filename alone, without reading the contents. The CSV body itself does NOT need to embed provisionality metadata; the filename carries it.

## Button visual + state machine

| Report state | Button enabled? | Button text | Notes |
|---|---|---|---|
| IDLE (no result) | no | `Export CSV` | greyed-out; tooltip "Run the report first" |
| READY_TO_RUN | no | `Export CSV` | greyed-out |
| LOADING (run in flight) | no | `Export CSV` | greyed-out |
| SUCCESS, !exporting | yes | `Export CSV` | active |
| SUCCESS, exporting | no | `Exporting…` | spinner; `aria-busy="true"` |
| ERROR | no | `Export CSV` | greyed-out |

The Export CSV button is **always present in the DOM** (no hide/show — the slot exists at all states so the layout doesn't shift). Disabled state uses `disabled:opacity-50 disabled:cursor-not-allowed` per cohort-2 baseline.

## Visual tokens

| Element | Token | Source |
|---|---|---|
| Button container | `border border-gray-300 text-gray-700 hover:bg-gray-50 text-sm font-medium rounded-md px-4 py-2 inline-flex items-center gap-2 disabled:opacity-50` | existing (cohort-2 secondary button) |
| Download icon | `ArrowDownTrayIcon w-4 h-4` (Heroicon outline) | existing |
| Spinner (exporting) | `ArrowPathIcon w-4 h-4 animate-spin` | existing |

## Position on page

Adjacent to the Run button, right-aligned in the filter bar (or below it on wrapped layouts). Same row, same alignment, same visual hierarchy — they form a `[Run] [Export CSV]` action pair.

The pairing communicates: "Run the report (primary action); export the result you just ran (secondary action)." The button order (Run left, Export right) follows reading order — you Run first, then Export.

## Interaction details

### Happy path

1. User clicks `Export CSV`
2. Button transitions to `Exporting…` with spinner (`aria-busy="true"`)
3. Frontend issues the export request (`POST` with body matching last-run params + Accept-header OR separate route per Engineer contract)
4. Server returns CSV bytes; browser triggers blob download
5. Button returns to `Export CSV` (active state)
6. Default toast: NONE on success — the file appearing in Downloads is the success signal

**Why no success toast?** Browsers already surface "file downloaded" notifications natively. A redundant in-page toast would be noise. The button returning to active state is the in-page acknowledgment.

### Failure path

If the export request fails (network error, 5xx, malformed CSV):

1. Button returns to `Export CSV` (active state)
2. Inline toast appears above the filter bar:

```
+--------------------------------------------------------------------+
| ⚠ Couldn't export the report. Try again or contact support if    × |
|   this keeps happening.                                            |
+--------------------------------------------------------------------+
```

3. Toast auto-dismisses after **3 seconds** OR when the user clicks the `×` dismiss button
4. The toast does NOT block further interaction — the user can click Export CSV again immediately

**Toast tokens:**

| Element | Token |
|---|---|
| Container | `border border-red-200 bg-red-50 text-red-700 rounded-md px-4 py-2 flex items-center justify-between` |
| Icon | `ExclamationTriangleIcon w-5 h-5 text-red-600` |
| Dismiss | `XMarkIcon w-4 h-4 text-red-600 hover:text-red-800 cursor-pointer` |

**Why a toast and not a full error surface?** Because the report result is still valid and visible — the user can interact with the on-screen data while addressing the failed export. A full error surface would block the report content; toast is the right scope.

### Cross-page consistency

All 4 cohort-3 pages implement the same `<ExportCsvButton>` component. The button's only props are:

```typescript
<ExportCsvButton
  enabled={querystatus === 'success'}
  onExport={async () => { await exportCsv(submittedParams, filename) }}
  filename="trial-balance-2026-05-22.csv"
/>
```

`onExport` is page-specific (calls the page's `exportXxxCsv` function); the button itself is identical visually across pages.

## Accessibility

- Button uses `<button type="button">` (NOT inside the form; clicking it does NOT submit the form)
- Disabled state: `disabled` attribute set; `aria-disabled="true"` for screen reader clarity
- Loading state: `aria-busy="true"` + button text changes ("Export CSV" → "Exporting…"); the text change is announced by screen readers
- Failure toast: `role="alert"` + `aria-live="assertive"` (the user just initiated an action; they need immediate feedback if it failed)
- Dismiss button: `aria-label="Dismiss"` + same color/text as the alert (not relying on red as sole signal — the icon plus text provide the meaning)

## CSV content conventions

Out of scope for PAO direction (cartridge implementations control body); for FED awareness only:

- UTF-8 encoding with BOM (Excel macOS compatibility)
- RFC 4180 quoting (fields with commas, newlines, or quotes get wrapped in `"`, internal `"` doubled)
- ISO-8601 dates (`YYYY-MM-DD` / `YYYY-MM-DDTHH:mm:ssZ`)
- Numeric values: plain numbers, no currency symbol, no thousands separator, decimal point only (`1234.56` not `$1,234.56`)
- Empty cells are empty, not `"—"` or `"N/A"`

These are cartridge-side responsibilities documented in `shipyard/packages/blocks-reports/README.md`; FED does not transform server output.

## What this pattern does NOT cover

- **Other export formats** (PDF, XLSX, JSON) — pattern-017 is CSV-only. Future formats would each get their own canonical pattern (or the button could become a split-button with format choice; PAO designs that when the use case arrives).
- **Bulk export** ("export all 4 reports for this period") — out of scope. Cohort-3 ships per-report exports only.
- **Scheduled export** ("email me this CSV nightly") — forward feature; not in cohort-3 scope.
- **Per-row / per-column export selection** — the export is always the full result. Filtering belongs in the Run-on-demand filter bar (re-run with narrower params, then export).

## Ratification timeline

- **First instance:** cohort-3 PR 1 ships `<ExportCsvButton>` + cohort-3 pages PR 2–5 each consume it
- **Ratification trigger:** next non-report CSV export surface ships clean carrying `@candidate-pattern: pattern-017` claim
- **Likely second instance:** future Lease list export, Tenant directory export, or Maintenance work-order export (any list-surface gets natural CSV export ask)

## Cross-references

- Pattern-011 (provisionality banner) — provisional-result filename suffix is defined here, but the banner UX is separate (in [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md))
- Pattern-012 (run-on-demand) — Export CSV button gating tied to the SUCCESS state defined there
- Pattern-009 (Bridge endpoint + frontend rebind pair) — the export endpoint is *another* Bridge endpoint pair (one for run, one for export, with the same contract shape); pattern-009 applies to both

— PAO, 2026-05-22
