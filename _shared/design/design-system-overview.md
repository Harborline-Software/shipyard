# Design System Overview

**Status:** Authored 2026-05-22 (PAO; v2-batch #7).
**Scope:** Consolidating index for the fleet's design system — framework docs, cohort-specific design direction, component inventory, pattern library, design tokens, and accessibility standards.

**Use this document as:** the single entry point for anyone (FED, Engineer, council reviewer, new contributor) trying to find what already exists before authoring new design surface.

---

## Reading order

If you're new to the design system, read in this order:

1. **`design-language.md`** — the language and conventions
2. **`component-principles.md`** — how to think about components
3. **`tokens-guidelines.md`** — token system + composition rules
4. **`accessibility.md`** — A11y baseline
5. **`internationalization.md`** — i18n baseline
6. **This document** — the consolidated index of what's been shipped
7. The latest cohort's INDEX.md (currently `cohort-3/INDEX.md`) — the most recent design direction
8. `cohort-3/component-reuse-audit.md` — the canonical record of what shipped vs what's promoted to shared

---

## Framework docs (file → scope)

| File | Scope |
|---|---|
| `design-language.md` | Visual language conventions: voice, register, layout density, typography, color semantics, motion guidance. Updated rarely; load-bearing for every cohort. |
| `component-principles.md` | How components should compose: prop API conventions, controlled-vs-uncontrolled patterns, error-handling responsibilities, accessibility-as-default expectations. |
| `tokens-guidelines.md` | Token system rules: when to add a new canonical token; when to compose existing tokens; naming conventions. |
| `accessibility.md` | Baseline standards: WCAG AA target; semantic HTML; ARIA usage rules; keyboard navigation; color-not-as-sole-signal; per-component a11y checklists. |
| `internationalization.md` | i18n baseline: date/number formatting; pluralization; right-to-left support; bidi text handling; locale-specific number conventions. |

---

## Cohort design direction (chronological)

Each cohort's design direction lives in `cohort-N/` and represents the design contract FED implemented against. Cohort dirs are append-only (we keep the historical record; we do not retroactively edit shipped cohort docs).

### Cohort-1 (no formal Track C)

Cohort-1 shipped 4 simple read-mostly pages (`MaintenancePage`, `LeasesPage`, `PropertiesPage`, etc.) without formal Track C design direction. Patterns established inline:

- `pattern-009-bridge-endpoint-frontend-rebind-pair` — formal post-cohort-1; applies to all subsequent cohorts
- `STATUS_COLORS` convention for work-order status pills (later promoted to `<StatusPill kind="workOrderStatus">` in cohort-3 PR 1)
- Table primitive shape (rounded-lg border-gray-200 wrapper; gray-50 header; hover-on-row) — inline; flagged as `<DataTable>` promotion candidate; not yet promoted as of 2026-05-22

### Cohort-2 — Financial cluster (`cohort-2/`)

**Workstream:** W#75 + W#76
**Track C ship date:** 2026-05-19 (PR shipyard#37)
**Cluster close:** 2026-05-19T06:00Z per `coordination/inbox/admiral-status-2026-05-19T06-00Z-cohort2-w75-w76-sweep-complete.md`

Shipped 3 pages × 5 deliverables:

- Per-page docs: `01-lease-detail-page-payments.md`, `02-accounting-page.md`, `03-rent-collection-page.md`
- Cross-cutting: `INDEX.md`, `tokens.md`, `component-reuse-audit.md`, `states-matrix.md`, `csrf-ux-pattern.md`, `cross-tenant-rejection-ux.md`

**Patterns introduced:**
- `pattern-010-financial-write-path` (candidate) — first instance: RentCollectionPage (cohort-2 PR 3). Visible signature: `csrf-ux-pattern.md` + audit-emission acknowledgment copy
- `pattern-014-bridge-cross-tenant-audit-emission` — ratification trigger met during cohort-2 (4 instances)

**See also:** `coordination/inbox/pao-status-2026-05-22T17-45Z-cohort-2-design-retrospective.md` (full retrospective).

### Cohort-3 — Reports cluster (`cohort-3/`)

**Workstream:** W#77
**Track C ship date:** 2026-05-22 (PR shipyard#116)
**Cluster close:** Pending (FED PR cluster execution in flight; halt conditions documented in INDEX)

Shipped 4 pages × 11 deliverables (largest design-direction artifact set to date):

- Per-page docs: `01-trial-balance-page.md`, `02-ar-aging-page.md`, `03-profit-loss-by-property-page.md`, `04-rent-roll-page.md`
- Cross-cutting: `INDEX.md`, `tokens.md`, `component-reuse-audit.md`, `states-matrix.md`, `provisionality-banner-pattern.md`, `run-on-demand-pattern.md`, `csv-export-pattern.md`

**Patterns introduced (all candidates):**
- `pattern-015-provisional-report-surface` (candidate) — first instance: `<ProvisionalityBanner>` shared across 4 cohort-3 pages
- `pattern-016-run-on-demand-report` (candidate) — first instance: IDLE → READY_TO_RUN state machine shared across 4 pages
- `pattern-017-csv-export-affordance` (candidate) — first instance: `<ExportCsvButton>` shared across 4 pages

**Dedicated shared-infrastructure PR introduced this cohort** (cohort-3 PR 1): 5 new shared components + 2 promotions from cohort-2 candidates. See `cohort-3/component-reuse-audit.md`.

### Cohort-4 — AP Aging + ERPNext deletion (`cohort-4/`)

**Status:** PRE-SCOPE only (PR shipyard#139). Track C will be authored when Engineer's AP Aging cartridge contract-frozen beacon lands.

Pre-scoped scope layers documented in `cohort-4/PRE-SCOPE.md`:
1. AP Aging cartridge page (mirror of cohort-3 AR Aging)
2. ERPNext deprecation finale
3. Backfill of cohort-3 PR 1 primitives into older pages
4. Speculative additional cartridges

**Pattern carry-forward:** cohort-4 likely ratifies cohort-3's three candidates (pattern-015/016/017) on AP Aging as the second instance.

---

## Component inventory

The canonical record is `cohort-3/component-reuse-audit.md`. This section summarizes the cross-cohort view.

### In `@sunfish/ui-react` (cross-product reuse)

| Component | Origin cohort | Notes |
|---|---|---|
| `<Card>`, `<CardHeader>`, `<CardContent>`, `<CardTitle>`, `<CardFooter>` | Cohort-1 | Shadcn-style; reused throughout |
| `<Badge>` | Cohort-1 | Generic chip primitive |
| `<CurrencyAmount>` | Cohort-1 + cohort-2 inline → promoted in v0.2 via FED queue #5 | Locale-aware formatting; sign + parentheses for negatives |
| `<AgingBucketPill>` | Cohort-2 inline → promoted in v0.2 via FED queue #5 | Replaces cohort-2 days-due pill inline classes |
| `<WorkOrderStatusBadge>` | Cohort-1 + promoted in v0.2 via FED queue #5 | Replaces cohort-1 `STATUS_COLORS` inline pattern |
| `<StatusPill kind=...>` | Cohort-1/2 inline → promoted in cohort-3 PR 1 | Variant-driven; supports gl-account-chip, occupancy-badge, aging-bucket-pill, balance-state, work-order-status |

### In `apps/web/src/components/` (sunfish-app-local; cohort-3+ canonical)

| Component | Origin cohort | Notes |
|---|---|---|
| `<AuthRoleGate>` | Cohort-1 | Wraps role-gated mutations (e.g., RentCollectionPage form) |
| `<ProvisionalityBanner>` | Cohort-3 PR 1 | Amber banner + collapsible warnings; pattern-015 visible signature |
| `<ExportCsvButton>` | Cohort-3 PR 1 | Pattern-017 visible signature |
| `<ReportFilterBar>` | Cohort-3 PR 1 | Filter-bar chrome; pattern-016 visible surface |
| `<ChartSelector>` | Cohort-3 PR 1 | ChartId acquisition; auto-select when 1 chart; dropdown when N |
| `<RunButton>` | Cohort-3 PR 1 | Pattern-016 primary action |
| `<ErrorSurface variant=...>` | Cohort-2 inline → promoted in cohort-3 PR 1 | retryable / reload / redirect variants |

### Page-local components (not promoted; rationale documented per cohort)

| Component | Origin cohort | Rationale for non-promotion |
|---|---|---|
| `TrialBalanceTable` | Cohort-3 PR 4 | Table semantics tightly coupled to TrialBalanceRow shape |
| `BalanceBadge` | Cohort-3 PR 4 | Single-purpose conditional pill; composes `<StatusPill>` |
| `AgingTable` | Cohort-3 PR 5 | Shaped to ArAgingRow + canonical header tints |
| `TopDelinquentList` | Cohort-3 PR 5 | Single-page list affordance |
| `ArAgingTotalsBar` | Cohort-3 PR 5 | 5-bucket aging visualization specific |
| `PortfolioSummaryTiles` (P&L variant) | Cohort-3 PR 3 | 3-tile pattern; differs from RentRoll's 5-tile pattern |
| `PortfolioSummaryBar` (RentRoll variant) | Cohort-3 PR 2 | 5-tile pattern; differs from P&L |
| `PropertyAccordion` + `PropertyAccordionList` | Cohort-3 PR 3 | Accordion mechanics; defer until 2nd accordion consumer |
| `PropertyBlock` + `PropertyHeader` (RentRoll variant) | Cohort-3 PR 2 | Rent-roll-specific visual idiom |
| `UnitTable` | Cohort-3 PR 2 | RentRollUnitRow-shape specific |

### Promotion candidates (deferred)

| Candidate | Source cohort | Status |
|---|---|---|
| `<DataTable>` primitive | Cohort-1 + 2 + 3 (≥8 instances) | Defer to post-cohort-3 cleanup; multi-week API design exercise |
| `<ConfirmationSurface>` primitive | Cohort-2 (1 instance only) | Defer until 2nd write surface exists |
| `<SkeletonRow>` / `<SkeletonAccordion>` / `<SkeletonTile>` | Cohort-3 (4 instances) | Defer + watch; may be over-abstraction at current density |
| `<PortfolioSummary>` (generic) | Cohort-3 P&L 3-tile + RentRoll 5-tile | Defer until 3rd instance (likely cohort-4 AP Aging or Cash Flow) |

---

## Pattern library

### Standing (ratified) patterns

| Pattern | First instance | Ratified | Visible signature doc |
|---|---|---|---|
| `pattern-009-bridge-endpoint-frontend-rebind-pair` | Cohort-1 | Post-cohort-1 | (none; established as universal cohort pattern) |
| `pattern-014-bridge-cross-tenant-audit-emission` | Cohort-2 (4-instance trigger) | Post-cohort-2 | `cohort-2/cross-tenant-rejection-ux.md` (relevant; not the pattern doc per se) |

### Candidate patterns (awaiting ratification trigger)

| Pattern | First instance | Visible signature doc | Ratification trigger |
|---|---|---|---|
| `pattern-010-financial-write-path` | Cohort-2 RentCollectionPage | `cohort-2/csrf-ux-pattern.md` | Second financial write-path PR |
| `pattern-015-provisional-report-surface` | Cohort-3 (4 pages) | `cohort-3/provisionality-banner-pattern.md` | Second cohort using `IsProvisional` semantics (likely cohort-4 AP Aging) |
| `pattern-016-run-on-demand-report` | Cohort-3 (4 pages) | `cohort-3/run-on-demand-pattern.md` | Second user-triggered report (likely cohort-4 AP Aging) |
| `pattern-017-csv-export-affordance` | Cohort-3 (4 pages) | `cohort-3/csv-export-pattern.md` | Second non-report CSV surface (likely cohort-4 AP Aging) |

### Cross-cutting universal conventions (not formally patterned)

These are conventions that recur across all cohorts but haven't been promoted to formal pattern status:

- **Loading skeleton convention** — `bg-gray-100 animate-pulse h-{N} my-1 rounded` for any list/table loading state
- **Sticky thead** — `sticky top-0 z-10` on `<thead>` for long-list tables (e.g., TrialBalance)
- **Tabular numeric cell** — `text-right tabular-nums` for any numeric column
- **Section heading** — `text-lg font-semibold text-gray-900 mt-6 mb-3` between sub-sections
- **Filter form Enter-to-submit** — semantic `<form>` with Enter triggering Run when valid
- **`aria-required` + `*` in label** — for required inputs

---

## Design token canonical list

Per cohort-3 `tokens.md`. This is the cumulative canonical token roster across cohorts.

### Surface tokens (color + spacing + typography combinations)

| Token | Composition | Origin |
|---|---|---|
| `provisional-surface` | `border border-amber-300 bg-amber-50 text-amber-900 rounded-md px-4 py-3` | Cohort-3 |
| `error-surface` (retryable) | `border border-red-200 bg-red-50 text-red-700 rounded-lg p-4` | Cohort-2 inline → cohort-3 named |
| `success-surface` (confirmation) | `border border-green-200 bg-green-50 text-green-800 rounded-lg p-6` | Cohort-2 RentCollectionPage |
| `card-default` | `border border-gray-200 bg-white rounded-lg` (via `<Card>`) | Cohort-1 |
| `summary-tile` | `rounded-lg border border-gray-200 bg-white px-3 py-2` | Cohort-2 AccountingPage |

### Semantic-color families (variant-driven)

| Family | Variants | Origin | Promotion |
|---|---|---|---|
| `gl-account-chip` | 5 (Asset / Liability / Equity / Revenue / Expense) | Cohort-3 | Via `<StatusPill kind="glAccountType">` |
| `occupancy-badge` | 4 (Occupied / NoticeGiven / Vacant / OffMarket) | Cohort-3 | Via `<StatusPill kind="occupancyStatus">` |
| `aging-bucket-pill` | 6 (NoBalance / Current / Days0To30 / Days31To60 / Days61To90 / Days90Plus) | Cohort-2 inline → cohort-3 canonical | Via `<StatusPill kind="agingBucket">` or `<AgingBucketPill>` |
| `aging-bucket-header-tint` | 3 (31-60 amber / 61-90 orange / 90+ red) | Cohort-3 | Inline on `<AgingTable>` thead |
| `balance-state` | 2 (Balanced green / OutOfBalance red) | Cohort-3 | Via `<StatusPill kind="balanceState">` |
| `work-order-status` | 8 (Draft / Sent / Accepted / Scheduled / InProgress / Completed / OnHold / Cancelled) | Cohort-1 `STATUS_COLORS` → cohort-3 canonical | Via `<StatusPill kind="workOrderStatus">` |
| `expiring-soon-badge` | 1 (amber) | Cohort-2 LeasesPage; reused cohort-3 RentRoll | Inline |

### Pill base (shared across all pill variants)

`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium`

Originally inline (cohort-1 MaintenancePage); promoted to `<StatusPill>` base in cohort-3 PR 1.

### Outlined variant suffix

`border border-gray-300` (additive) for "intentional absence" states (e.g., `occupancy-badge` `OffMarket`).

### No new Tailwind palette stops

The design system has never added a new Tailwind palette color. All cohort additions compose existing palette stops via canonical names. This is a deliberate constraint — the design vocabulary is finite by design.

---

## Accessibility baseline (per `accessibility.md`)

Brief restatement; canonical is `accessibility.md`. Cross-cohort enforcement points:

- **WCAG AA contrast** for all text-on-background; verified via design-language tokens (no new palette stops means no new contrast risks)
- **Semantic HTML** preferred over ARIA where possible (`<button>` not `<div role="button">`)
- **Color is never the sole signal** — every status indicator carries text + icon in addition to color
- **All loading states** use `aria-busy="true"` + visually-hidden announcement
- **All error surfaces** use `role="alert"` + icon (not color alone)
- **All success surfaces** use `role="status"` + icon
- **All required form inputs** use `aria-required="true"` + `*` in label
- **All tables** use semantic `<thead>` + `<tbody>` + `scope="col"`
- **All numeric cells** use `tabular-nums` for visual + screen-reader decimal alignment
- **All form-submit-on-Enter** behaviors are explicit via semantic `<form>` + `<button type="submit">`

Per-component a11y annotations are documented in each cohort's per-page direction docs (e.g., `cohort-3/01-trial-balance-page.md` section 12).

---

## Authoring + execution pattern (PAO + subagent fan-out)

Per cohort-3 actual experience (wall-clock improvement from 10h cohort-2 → 75 min cohort-3):

### Three-phase Track C authoring

1. **Phase A — Foundation** (main PAO session, ~30 min): INDEX.md + tokens.md + pattern-document(s) for any new candidate patterns. Pins the contracts before page docs.
2. **Phase B — Page docs** (PAO subagent fan-out in parallel, ~30 min wall): 1 subagent per page; each writes the full per-page direction doc against the Phase A contracts.
3. **Phase C — Cross-cutting** (main PAO session, ~15 min): component-reuse-audit.md + states-matrix.md authored from Phase B returns + INDEX.md finalized with cross-refs.

This pattern is the canonical PAO Track C workflow. Cohort-4 should inherit it directly.

### When to consult the council

Per cohort-2 retrospective (`coordination/inbox/pao-status-2026-05-22T17-45Z-cohort-2-design-retrospective.md`):

- Track C does NOT require a council review pre-FED-implementation (cohort-3 shipped clean without one)
- Council review IS useful post-execution for stress-testing the design surface against potentially-missed concerns (e.g., the vol-2 5-critic council review surfaced book-prose concerns the PAO sessions had not stress-tested)
- For cohort-4: if the AP Aging design diverges substantially from AR Aging (e.g., the money-direction signal question becomes load-bearing), a 2-3-critic council pass before FED execution may add value. If AP Aging is a straight cartridge-mirror, no council needed.

---

## Cross-cohort architectural commitments

These are the implicit-but-load-bearing commitments the design system makes. Surfacing them so future cohorts don't break them inadvertently:

1. **Bridge endpoints are the boundary.** No page calls a non-Bridge backend directly (ERPNext-passthrough being deprecated in cohort-4; otherwise this is universal).
2. **Audit emission is non-optional for write paths.** Per `pattern-014` (cross-tenant audit emission) — every write surfaces "an audit-trail entry has been emitted" in the confirmation view.
3. **Provisionality is surfaced, not hidden.** Per `pattern-015` (candidate; cohort-3) — when the cartridge returns `isProvisional`, the UI shows the banner. The banner is not dismissible.
4. **Run is explicit.** Per `pattern-016` (candidate; cohort-3) — reports do not auto-fetch on mount. The user clicks Run.
5. **Filter change resets result.** Per `pattern-016` — any parameter change clears the result; no stale data with new labels.
6. **CSV export is universally available on result-bearing pages.** Per `pattern-017` (candidate; cohort-3).
7. **No diagnostic leak in error states.** Per `cohort-2/cross-tenant-rejection-ux.md` (sec-eng-council derived) — cross-tenant rejection returns empty rather than 403/404 so other tenants' existence is not leaked.

---

## Outstanding work

### Component promotion backlog

Per cohort-3 `component-reuse-audit.md` + above:

- `<DataTable>` primitive — 8 instances; ratify post-cohort-3
- `<ConfirmationSurface>` primitive — 1 instance; defer
- `<SkeletonRow>` family — 4 instances; defer + watch
- `<PortfolioSummary>` generic — 2 instances; ratify on 3rd

### Token authoring backlog

Per cohort-3 `tokens.md`:

- Loading skeleton — no canonical name (defer until 3rd cohort needs it)
- Sticky thead — no canonical name (defer)
- Tabular numeric cell — no canonical name (defer)

### Backfill backlog

Per cohort-4 PRE-SCOPE.md:

- Older cohort-1/cohort-2 pages should consume `<StatusPill>` + `<ErrorSurface>` from cohort-3 PR 1 instead of inline classes. QM/FED housekeeping; not Track C.

### Council retrospectives

- Cohort-1 + cohort-2 + cohort-3 retrospectives all useful (cohort-2 done; the others would benefit from same treatment)

---

## When to update this document

This document is the design-system index. It updates when:

1. A new cohort ships Track C (add a section under "Cohort design direction")
2. A new component is added to the canonical inventory (add to "Component inventory")
3. A new pattern is added (add to "Pattern library")
4. A new canonical token is added (add to "Design token canonical list")
5. A new framework-level doc is added to `_shared/design/` (add to "Framework docs")

This document does NOT need to update when:

- A cohort-local doc is amended (those amendments are scoped to the cohort's INDEX; cross-references here remain valid)
- An open promotion backlog item closes (the backlog table updates; the rest of the doc is unaffected)

---

— PAO, 2026-05-22
