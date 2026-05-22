# TrialBalancePage — Design Direction

**Page:** `sunfish/apps/web/src/pages/TrialBalancePage.tsx`
**PR:** W#77 PR 4
**Cartridge:** `TrialBalance` (`shipyard/packages/blocks-reports/.../TrialBalanceCartridge.cs`)
**Endpoint:** `POST /api/v1/reports/trial-balance`
**Patterns:** `@standing-pattern: pattern-009` + `@candidate-pattern: pattern-015, pattern-016, pattern-017`

## Scope

TrialBalancePage is a **new** page (no prior file replaces it; no migration shim) presenting a classic accounting trial balance — every GL account in a chart with its debit and credit balance as of a date or fiscal period, plus a totals row and a balance-state diagnostic. It is the canonical introduction of the cartridge-backed report surface: the page does not auto-fetch on mount, the user explicitly clicks Run after selecting required parameters, and the result envelope's `IsProvisional` flag drives an amber banner when the chosen period is open. As the first of the four cohort-3 pages, it is also the canonical exercise of `pattern-016` (run-on-demand), `pattern-015` (provisionality banner), and `pattern-017` (CSV export) — every visible UX choice here ratifies for the other three.

## Component hierarchy

Lightly refined from FED spec lines 297–318. Composition is shared-component-first; the page itself is a thin orchestrator.

```
TrialBalancePage
  PageHeader
    h1: "Trial Balance"
    subtitle: "Account balances as of a date, with debit and credit totals"
  ProvisionalityBanner (when result.isProvisional)            // pattern-015
  ReportFilterBar                                             // pattern-016
    ChartSelector              (required; Q2-canonical behavior)
    PeriodModeToggle           ([Date] | [Fiscal period] tabs; mutually exclusive)
      ↳ AsOfDatePicker         (mode = Date; default = today)
      ↳ FiscalPeriodSelector   (mode = Fiscal period)
    ToggleFilter               (IncludeZeroBalanceAccounts; default OFF)
    ToggleFilter               (IncludeInactiveAccounts;    default OFF)
    RunButton                  (aria-busy on LOADING; "Run report" / "Running…")
    ExportCsvButton            (disabled until SUCCESS)      // pattern-017
  [state-dependent content]
    IDLE              → empty hero: "Select a chart and date, then click Run report."
    READY_TO_RUN      → same hero copy; Run button now active
    LOADING           → SkeletonRows ×5
    EMPTY             → "No accounts found for this chart and period."
    ERROR             → ErrorSurface (variant=retryable) + Retry
    SUCCESS
      TrialBalanceTable
        thead: Account Code | Account Name | Type | Debit | Credit
        tbody: TrialBalanceRow ×N
        tfoot: TOTAL | sum(Debit) | sum(Credit)
      BalanceBadge                // "Balanced" or "Out of balance by $X"
```

The page is a thin composition over `ReportFilterBar`, `ProvisionalityBanner`, `TrialBalanceTable`, and `BalanceBadge` — all but the table are shared across cohort-3.

## Wireframe specs

### IDLE state (mount, no Chart selected yet)

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                             │
│  Account balances as of a date, with debit and credit totals               │
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│  Chart: [— select a chart —      ▾]   [Date] [Fiscal period]               │
│  As of: [2026-05-22            ▾]                                          │
│  ☐ Include zero-balance accounts     ☐ Include inactive accounts           │
│                                                                            │
│                                            [ Run report ]   [Export CSV ↓] │
│                                            (disabled)       (disabled)     │
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│                  Select a chart and date, then click Run report.           │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

ChartSelector is the only required filter without a default. The `As of` picker pre-fills today (operator local date) per `pattern-016` filter-bar conventions. Both toggles default OFF (conservative — fewer rows, faster scan; matches `pattern-016` boolean-toggle convention).

### READY_TO_RUN state (Chart selected; Run enabled)

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                             │
│  Account balances as of a date, with debit and credit totals               │
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│  Chart: [Operating accounts    ▾]   [Date] [Fiscal period]                 │
│  As of: [2026-05-22            ▾]                                          │
│  ☐ Include zero-balance accounts     ☐ Include inactive accounts           │
│                                                                            │
│                                            [ Run report ]   [Export CSV ↓] │
│                                            (enabled)        (disabled)     │
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│                  Select a chart and date, then click Run report.           │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

Hero copy is unchanged from IDLE — the only state change is Run-button enablement. Export CSV stays disabled until SUCCESS (gating per `pattern-017`).

### LOADING state (Run clicked; mutation in flight)

```
┌────────────────────────────────────────────────────────────────────────────┐
│  [filter bar dimmed, Run button shows "Running…" + spinner, aria-busy]     │
│  ───────────────────────────────────────────────────────────────────────── │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ ████████████  ████████████████████████  ████  ██████████  ██████████ │  │
│  │ ████████████  ████████████████████████  ████  ██████████  ██████████ │  │
│  │ ████████████  ████████████████████████  ████  ██████████  ██████████ │  │
│  │ ████████████  ████████████████████████  ████  ██████████  ██████████ │  │
│  │ ████████████  ████████████████████████  ████  ██████████  ██████████ │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────┘
```

Five skeleton rows (`bg-gray-100 animate-pulse h-8 rounded`), column widths matching the eventual table columns. Container has `aria-busy="true"` + visually-hidden "Loading trial balance" announcement.

### SUCCESS state with provisional result + balanced result

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Trial Balance                                                             │
│  Account balances as of a date, with debit and credit totals               │
│  ───────────────────────────────────────────────────────────────────────── │
│  ⚠ This report covers an open accounting period and may change as          │
│    transactions are posted.                                  [Show details ▾]│
│  ───────────────────────────────────────────────────────────────────────── │
│  Chart: [Operating accounts ▾]  As of: [2026-05-22 ▾]  ☐ Zero ☐ Inactive   │
│                                              [ Run report ]  [Export CSV ↓]│
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ ACCOUNT CODE │ ACCOUNT NAME            │ TYPE      │  DEBIT │ CREDIT │  │  <-- sticky thead
│  ├──────────────────────────────────────────────────────────────────────┤  │
│  │ 1000         │ Cash — Operating        │ [Asset]   │ 24,150 │   —    │  │
│  │ 1100         │ Accounts Receivable     │ [Asset]   │  3,420 │   —    │  │
│  │ 1500         │ Prepaid Insurance       │ [Asset]   │    600 │   —    │  │
│  │ 2000         │ Accounts Payable        │ [Liab.]   │    —   │  1,150 │  │
│  │ 2100         │ Tenant Security Deposits│ [Liab.]   │    —   │  6,400 │  │
│  │ 3000         │ Owner's Equity          │ [Equity]  │    —   │ 18,000 │  │
│  │ 4000         │ Rent Revenue            │ [Revenue] │    —   │  4,800 │  │
│  │ 5100         │ Repairs & Maintenance   │ [Expense] │  1,180 │   —    │  │
│  │ 5200         │ Property Tax            │ [Expense] │  1,000 │   —    │  │
│  ├──────────────────────────────────────────────────────────────────────┤  │
│  │ TOTAL                                              │ 30,350 │ 30,350 │  │  <-- tfoot bold
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│                                              [ ✓ Balanced ]                │  <-- BalanceBadge
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

Provisionality banner sits above the filter bar (per `pattern-015` fixed position). Zero values render as `—` (Q-D resolved below). Type column uses the gl-account-chip canonical token (Q6 from INDEX). BalanceBadge sits directly below the tfoot total row, right-aligned to match the numeric columns.

### SUCCESS state — out-of-balance variant

```
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │ TOTAL                                              │ 30,350 │ 28,000 │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│                                  [ ⚠ Out of balance by $2,350.00 ]         │  <-- red pill
│                                                                            │
```

When `result.isBalanced === false`, BalanceBadge renders red with the rounded delta. Copy: `Out of balance by ${formattedAbsDelta}` — sign is implied (the badge means "you are off"); we do not say "Debits exceed credits by …" because the underlying interpretation depends on accounting convention and PAO has no domain stance to take on whose-side-is-short.

### EMPTY state

```
┌────────────────────────────────────────────────────────────────────────────┐
│  [filter bar above, normal state]                                          │
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│                   No accounts found for this chart and period.             │
│                                                                            │
│   Try widening the period, or toggle on "Include zero-balance accounts"    │
│   to see accounts that exist but have no activity in the range.            │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

Empty copy is helpful (suggests two concrete remediations) but not pushy — no CTA button. The user already has the toggles in front of them.

### ERROR state

```
┌────────────────────────────────────────────────────────────────────────────┐
│  [filter bar above]                                                        │
│  ───────────────────────────────────────────────────────────────────────── │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  ⚠ Couldn't load trial balance                                       │  │
│  │                                                                      │  │
│  │  The report service didn't respond. Try again in a moment.           │  │
│  │                                                                      │  │
│  │  ┌─────────┐                                                         │  │
│  │  │ Retry   │                                                         │  │
│  │  └─────────┘                                                         │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────┘
```

Standard `<ErrorSurface variant="retryable">` token (red-200 border / red-50 bg / red-700 text). Retry is scoped to the surface; it re-runs the last submitted params. The page's main Run button remains in its normal state — Retry inside the surface is the canonical retry, not a Run-button color swap (per `pattern-016` Run-button copy convention).

## State machine summary

This page uses the canonical state machine defined in [`run-on-demand-pattern.md`](./run-on-demand-pattern.md) without modification:

> `IDLE → READY_TO_RUN → LOADING → SUCCESS | ERROR`, with any filter change resetting back to IDLE (form params vs submitted params separation per the pattern doc's implementation note).

No page-specific deviations. The page implements the pattern's "submitted params" / "form params" separation literally — `submittedParams` is the React Query enable gate; `formParams` is the editable form state.

## Provisionality banner placement

This page renders `<ProvisionalityBanner>` per [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md) without modification:

- Below page `<h1>` + subtitle paragraph
- Above the filter bar
- Visible only when `result.isProvisional === true` (SUCCESS state)
- Copy verbatim per Q4 in INDEX: *"⚠ This report covers an open accounting period and may change as transactions are posted."*
- Warnings list expands inline behind "Show details ▾"

No page-specific deviations.

## CSV export

This page uses `<ExportCsvButton>` per [`csv-export-pattern.md`](./csv-export-pattern.md):

- Placement: in `<ReportFilterBar>` adjacent to Run, right-aligned (the canonical `[Run] [Export CSV]` action pair)
- Disabled until SUCCESS; greyed out at all other states
- Filename: `trial-balance-{asOfDate}.csv` (e.g., `trial-balance-2026-05-22.csv`)
- Provisional suffix: when `result.isProvisional === true`, append `-provisional` before `.csv` → `trial-balance-2026-05-22-provisional.csv`
- On failure: 3-second inline toast (`role="alert"`) above the filter bar; report result remains visible and interactive

The `{asOfDate}` token resolves to whichever date the cartridge actually used: when the user submitted with mode = Date, the picker value; when mode = Fiscal period, the cartridge's effective as-of from `result.asOf`. Either way the filename is deterministic and self-describing.

## Resolved PAO design direction answers

The FED spec asks four questions for this page (lines 339–344). All four are answered decisively below.

### A. GLAccountType color mapping

Resolved by Q6 in [INDEX.md](./INDEX.md#q6--glaccounttype-color-map). Applied verbatim here for FED's eye, with the canonical `gl-account-chip` token name from [`tokens.md`](./tokens.md):

| Type | Background | Text | StatusPill variant |
|---|---|---|---|
| Asset | `bg-blue-100` | `text-blue-700` | `<StatusPill kind="glAccountType" value="Asset" />` |
| Liability | `bg-purple-100` | `text-purple-700` | `<StatusPill kind="glAccountType" value="Liability" />` |
| Equity | `bg-slate-100` | `text-slate-700` | `<StatusPill kind="glAccountType" value="Equity" />` |
| Revenue | `bg-green-100` | `text-green-700` | `<StatusPill kind="glAccountType" value="Revenue" />` |
| Expense | `bg-amber-100` | `text-amber-800` | `<StatusPill kind="glAccountType" value="Expense" />` |

Reasoning recap from Q6: assets-vs-liabilities reads cool-vs-warm in the accounting tradition (blue cash-positive; purple obligations); equity sits neutral on slate (residual); revenue inherits green for "money in"; expense uses amber so red stays reserved for **error and delinquency** semantics across the system. Reserving red is load-bearing — TrialBalance's BalanceBadge red state, ArAgingPage's 90+ tint, and every retryable error surface all need red exclusivity.

Display copy in the chip is the enum value verbatim: `Asset`, `Liability`, `Equity`, `Revenue`, `Expense`. No abbreviation. The cohort-1 `STATUS_COLORS` convention is the precedent for this enum-to-color mapping shape; cohort-3 promotes it from an inline pattern to the `<StatusPill kind="glAccountType">` variant on the shared primitive.

### B. BalanceBadge — copy + color + position

The badge is a pill rendered directly below `<TrialBalanceTable>`'s tfoot, right-aligned (so it sits visually under the numeric Debit/Credit columns it diagnoses). It uses the `<StatusPill kind="balanceState" value="…">` variant from [`tokens.md`](./tokens.md).

| State | Pill background | Text color | Copy | Icon |
|---|---|---|---|---|
| Balanced (`result.isBalanced === true`) | `bg-green-100` | `text-green-700` | `Balanced` | `CheckIcon w-4 h-4` (Heroicon outline) |
| Out of balance (`result.isBalanced === false`) | `bg-red-100` | `text-red-700` | `Out of balance by $1,234.56` | `ExclamationTriangleIcon w-4 h-4` |

**Copy rationale:**

- `Balanced` (single word) — declarative, no editorializing. The user already knows what a trial balance is; we don't need to say "Debits equal credits."
- `Out of balance by ${formattedDelta}` — names the diagnostic in business-user language. `${formattedDelta}` renders via the existing `<CurrencyAmount>` primitive (`$1,234.56` with locale-appropriate separators, two decimal places, absolute value — sign is implicit in "out of balance").

**Position rationale:**

Directly below tfoot, right-aligned to the numeric columns. The badge is the **answer** to the question the table just asked (do the totals balance?), so it sits where the totals are. Placing it in the page header would separate the diagnosis from the data; placing it inside the tfoot would clutter the totals row. The space directly under the table, aligned to the right, is the natural eye-path destination.

**Color is supplemental, not the signal.** The text "Balanced" or "Out of balance by …" carries the meaning; the icon (check vs warning triangle) carries the meaning; green/red are reinforcement only. Users with red-green color vision differences still get the full diagnosis.

### C. ProvisionalityBanner copy

Resolved by Q4 in [INDEX.md](./INDEX.md#q4--provisionalitybanner-copy) and codified in [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md). Applied verbatim:

> ⚠ This report covers an open accounting period and may change as transactions are posted. **[Show details ▾]**

Expanded warnings list opens to display `result.warnings[]` strings verbatim, bulleted. PAO does not rewrite warnings — they are cartridge-authored and may name specific entities. The frontend treats them as opaque strings.

### D. Zero-value display — `—` vs `$0.00`

**Resolution: `—` (em dash, gray)** when the column value is exactly 0. Applies to both Debit and Credit cells.

Reasoning: T-account convention is to show only the side an account has a balance on. A row like

```
Cash — Operating  | [Asset] |  24,150  |   —
Accounts Payable  | [Liab.] |    —     |  1,150
```

reads correctly the way an accountant expects to read it: one balance per account, on the appropriate side. Writing `$0.00` on the zero side would imply "we know this is precisely zero, not unset" — a precision claim the absence of activity does not earn. The em dash carries the meaning *"this account has no balance on this side"* without the false precision.

Token: `text-gray-400` for the dash (existing token; same as cohort-2 vacant-tenant-name treatment). Not bold, not red, not amber — just absent.

**Edge cases:**

- An account with non-zero balance on both sides (rare; happens for accounts that genuinely accrue on both sides intra-period): show both numbers. The dash convention applies only to *exactly* zero.
- The TOTAL row in tfoot: always shows both values numerically (never `—`), even if one side sums to zero (out-of-balance state). The diagnostic value of seeing `30,350 | 0` exceeds the visual tidiness of the dash convention.

## TrialBalanceTable column design

Five columns, fixed order, no user-controlled visibility in cohort-3 (column toggles are a candidate for the `<DataTable>` primitive promotion noted in [`tokens.md`](./tokens.md)).

| # | Header | Width | Alignment | Font | Notes |
|---|---|---|---|---|---|
| 1 | `ACCOUNT CODE` | `w-[110px]` | left | `font-mono text-sm` | Monospaced because account codes are aligned numeric/alpha identifiers; mono renders them as data-identifiers, not prose. |
| 2 | `ACCOUNT NAME` | `flex-1` | left | regular | Wraps on narrow viewports. |
| 3 | `TYPE` | `w-[110px]` | left | regular | `<StatusPill kind="glAccountType">` chip — fixed width prevents reflow on type-name length differences. |
| 4 | `DEBIT` | `w-[130px]` | right | `tabular-nums` | Right-aligned for decimal alignment; `tabular-nums` ensures column position is stable across rows. |
| 5 | `CREDIT` | `w-[130px]` | right | `tabular-nums` | Same as Debit. |

**Table container:**

- Wrapped in `rounded-lg border border-gray-200 overflow-hidden` — matches MaintenancePage / AccountingPage table card.
- `overflow-y-auto` on the body region with `max-height: calc(100vh - 320px)` (approximate; FED tunes the offset to match the actual filter-bar height) — allows vertical scroll while keeping filter + page header always visible.
- No pagination in cohort-3. Virtual scrolling is a `<DataTable>` Phase-2 concern.

**Sticky thead:**

- `<thead>` carries `position: sticky; top: 0; z-10` so column headers stay visible while the user scrolls a 50-200-row body.
- Header background is `bg-gray-50` (matches cohort-2 token); sticky-on-scroll the header stays opaque against the scrolling rows.

**tfoot (TOTAL row):**

- `border-t-2 border-gray-300` (heavier rule than body row separators) — visually a "summary line."
- TOTAL label spans columns 1-3 (`<td colspan="3" class="font-semibold">TOTAL</td>`).
- Debit and Credit totals: `font-semibold tabular-nums` — same column alignment as body rows.

**Row hover:** `hover:bg-gray-50` (matches MaintenancePage). No row-click action in cohort-3; rows are read-only.

**No row-sort in cohort-3.** Cartridge returns rows in account-code order (numeric/lexicographic per chart authorship). Sorting is a Phase-2 affordance, deferred to the `<DataTable>` primitive.

## Filter design

The filter bar is `<ReportFilterBar>` (shared component shipped in PR 1). Its children are the page-specific filters; the layout, label styles, and Run/Export pairing are pattern-fixed.

**Layout (left-to-right, wrapping on narrow viewports):**

```
ROW 1:  Chart: [______ ▾]    [Date] [Fiscal period]  ←┐
        As of: [YYYY-MM-DD]                            │ mode toggle controls
                                                       │ which input shows
ROW 2:  ☐ Include zero-balance accounts                ┘
        ☐ Include inactive accounts

ROW 3:                          [ Run report ]  [Export CSV ↓]
```

On `<sm:` (Surface Pro portrait), Row 3 stretches the action buttons full-width to maintain a clean tap target.

**Mutual-exclusive Period vs As-of via tab-style toggle.** This is the spec-line-337 decision: two pill tabs `[Date] [Fiscal period]` sit adjacent to the active-mode input. Selecting `Date` shows the `<AsOfDatePicker>` and clears any fiscal-period value; selecting `Fiscal period` shows the `<FiscalPeriodSelector>` and clears the as-of value. Only one mode's value is sent in the request body.

**Default mode: `Date`.** This is the more discoverable interpretation — every user has an intuition about "as of a date"; fewer users have an intuition about "fiscal period ID." The toggle exists for accountants who think in periods; the default matches the broader audience.

**Both boolean toggles default OFF.** `IncludeZeroBalanceAccounts` defaults false because the dominant use case is "show me where the money is," not "show me every account that exists." `IncludeInactiveAccounts` defaults false because inactive accounts have been retired by an explicit user action; showing them by default partially undoes that intent. Both toggles surface in the same row, in the order spec lines 64–65 list them.

## Token usage

This page composes existing Tailwind classes through six canonical compositions defined in [`tokens.md`](./tokens.md):

| Token (canonical) | Where used |
|---|---|
| `provisional-surface` | `<ProvisionalityBanner>` container |
| `gl-account-chip` (Asset / Liability / Equity / Revenue / Expense variants) | `<StatusPill kind="glAccountType">` in the Type column |
| `balanceState` (Balanced / OutOfBalance variants) | `<BalanceBadge>` below tfoot |
| `pill-base` (`<StatusPill>` foundation) | All three chip surfaces above |
| `data-table` (rounded-lg border-gray-200 wrapper + gray-50 header + sticky thead) | `<TrialBalanceTable>` container |
| `error-surface-retryable` (red-200/50/700 + Retry button) | ERROR state |

No new Tailwind palette stops are introduced. No new color tokens are introduced beyond `tokens.md`. The page is a pure composition of canonical names.

## Component reuse

**From `@sunfish/ui-react` (after PR 1 promotes the primitives):**

- `<StatusPill kind="…" value="…" />` — the new shared primitive (cohort-2 candidate ratified for cohort-3); used for Type column (`glAccountType` variant) and BalanceBadge (`balanceState` variant)
- `<CurrencyAmount value={n} />` — existing primitive; used for Debit / Credit cells, TOTAL tfoot values, and the BalanceBadge "Out of balance by $X" text
- `<Card>` — existing primitive; the page is wrapped in a `<Card>`-style container, optional

**From `apps/web/src/components/` (cohort-3 PR 1 ships these):**

- `<ProvisionalityBanner>` — pattern-015 surface; consumed as-is
- `<ExportCsvButton>` — pattern-017 surface; consumed as-is
- `<ReportFilterBar>` — pattern-016 surface; the page passes its filter children
- `<ChartSelector>` — pattern-016 shared filter; the page passes the current selection up via callback
- `<RunButton>` — pattern-016 surface; consumed via `<ReportFilterBar>`
- `<ErrorSurface variant="retryable">` — shared error surface; promoted from cohort-2 candidate per [`tokens.md`](./tokens.md)

**Page-specific (new in PR 4):**

- `<TrialBalanceTable>` — the table itself, page-local; not promoted to `@sunfish/ui-react` until the `<DataTable>` primitive is designed in a post-cohort-3 pass
- `<BalanceBadge>` — thin wrapper over `<StatusPill kind="balanceState">`; page-local because the "out of balance by $X" composition is unique to trial balance. If a future report needs a similar diagnostic, promote then.

**No additional components beyond what cohort-3 PR 1 already ships.** The page is intentionally thin — its job is composition, not new primitive authoring.

## Accessibility

- **Sticky thead:** `<thead>` carries `role="rowgroup"` (implicit on the element); each header `<th>` carries `scope="col"`. Sticky positioning does not alter the AT tree; screen readers reach the headers normally.
- **Skeleton rows:** wrapping container has `aria-busy="true"`; a visually-hidden `<span class="sr-only">Loading trial balance</span>` announces the LOADING state to AT.
- **Run button:** `<button type="submit">` inside the `<form>` so Enter on a form input triggers Run when enabled. State transitions: `"Run report"` (READY_TO_RUN) → `"Running…"` + `aria-busy="true"` (LOADING) → `"Run report"` (SUCCESS / ERROR). The text change is screen-reader-announced.
- **BalanceBadge:** color is **not** the sole signal. The text (`Balanced` / `Out of balance by $X`) plus the icon (check / warning triangle) carry the meaning. Color is reinforcement only. The badge container has `role="status"` so AT announces the diagnosis when it appears.
- **Tabular numerics:** the Debit / Credit columns use `tabular-nums` so decimal points align across rows. This benefits sighted users (faster column scanning) and is neutral for AT — the values are read as numbers either way.
- **Zero-value dash:** the `—` glyph has `aria-label="No balance"` on its containing cell so AT users don't hear the literal em-dash character read out (which screen readers handle inconsistently). The "no balance" announcement is the semantic intent.
- **Provisionality banner:** `role="status"` + `aria-live="polite"` per [`provisionality-banner-pattern.md`](./provisionality-banner-pattern.md). Banner first announcement happens when SUCCESS lands with `isProvisional === true`.
- **Filter labels:** every input has a visible `<label>` linked via `htmlFor`. Required filters (Chart) carry `aria-required="true"`.
- **Period mode toggle:** the two tabs use `role="tablist"` / `role="tab"` / `aria-selected="true|false"` so AT users perceive the mutual-exclusion correctly.

## States summary

| State | Trigger | UX | Action affordance |
|---|---|---|---|
| IDLE | Mount; or any filter changes after a prior result | Hero copy: "Select a chart and date, then click Run report." Filter bar visible. Run disabled. Export CSV disabled. | User completes required filters → READY_TO_RUN |
| READY_TO_RUN | Required filters valid (Chart selected; AsOf or Period set) | Same hero. Run button enabled. Export CSV disabled. | User clicks Run → LOADING |
| LOADING | Run clicked; mutation in flight | Filter bar dimmed; Run button shows `Running…` + spinner + `aria-busy="true"`. Skeleton rows ×5 in result region. | (none; await server) |
| SUCCESS (`isProvisional === false`) | Mutation success; result.rows.length > 0; isProvisional false | Filter bar normal. Table renders. BalanceBadge below tfoot. Export CSV enabled. No provisionality banner. | User can Export CSV; or change filters → IDLE; or re-run for fresh data |
| SUCCESS + provisional (`isProvisional === true`) | Mutation success; result.rows.length > 0; isProvisional true | Same as SUCCESS plus amber ProvisionalityBanner above filter bar. Export CSV filename gets `-provisional` suffix. | Same affordances as SUCCESS |
| EMPTY | Mutation success; result.rows.length === 0 | "No accounts found for this chart and period." + remediation hint. No table, no BalanceBadge. Export CSV disabled. | User changes filters → IDLE → READY_TO_RUN → re-run |
| ERROR | Mutation failure (network / 5xx / malformed payload) | `<ErrorSurface variant="retryable">` with `Retry` button. Filter bar normal. Export CSV disabled. No table. | User clicks Retry → LOADING (with same submitted params); or changes filters → IDLE |

Seven canonical states — IDLE / READY_TO_RUN / LOADING / SUCCESS / SUCCESS+provisional / EMPTY / ERROR. The state machine is a literal application of [`run-on-demand-pattern.md`](./run-on-demand-pattern.md)'s canonical model.

## Pattern alignment

- **`@standing-pattern: pattern-009`** (Bridge endpoint + frontend rebind pair) — `POST /api/v1/reports/trial-balance` is the new Bridge endpoint; this page is the frontend rebind. Standard security-engineering SPOT-CHECK applies on PR-open. The rebind layers the run-on-demand UX on top, but the pattern-009 baseline still applies (route exists; frontend consumes it; security review confirms the contract).

- **`@candidate-pattern: pattern-015`** (provisional report surface) — first instance of `<ProvisionalityBanner>` consumption. The page renders the banner only when `result.isProvisional === true`; placement, copy, and disclosure interaction are fixed by the pattern doc. Ratification happens when a second cohort using `IsProvisional` semantics ships (likely cohort-4 AP Aging).

- **`@candidate-pattern: pattern-016`** (run-on-demand report) — first instance of the IDLE → READY_TO_RUN → LOADING → SUCCESS state machine. The page implements the pattern's submitted-params vs form-params separation literally — no optimistic state, no auto-fetch on mount, filter changes reset the result. Ratification happens with the next user-triggered report.

- **`@candidate-pattern: pattern-017`** (CSV export affordance) — first instance of `<ExportCsvButton>` consumption. Filename `trial-balance-{asOfDate}.csv` (+ `-provisional` suffix when applicable) per the pattern doc. Ratification happens with the next non-report CSV export surface (e.g., a future Lease list export).

All three candidate-patterns share this single cohort-of-instance — they ratify together if cohort-4 (or a later cohort) picks them up consistently. TrialBalancePage is the most canonical of the four cohort-3 pages because every visible surface here exercises every pattern at once: a clean run-on-demand machine, a provisional banner on open-period runs, a CSV export of the result, all wrapped over a single-table report read.

— PAO, 2026-05-22
