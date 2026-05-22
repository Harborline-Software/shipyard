# Cohort-4 Design Pre-Scope

**Status:** PRE-SCOPE (NOT Track C). Authored ahead of substrate work to scope the design surface so PAO direction is ready when Engineer's cartridges and Bridge endpoints land.
**Workstream candidates:** W#72 substrate v2 (AP Aging cartridge) + ERPNext deprecation finale + (TBD: cartridge family expansion candidates)
**Authored:** 2026-05-22
**PAO:** chris (per v2-batch #3 directive, post-cohort-3 Track C complete)
**Successor doc:** Full Track C at `shipyard/_shared/design/cohort-4/` will be authored when the substrate scope is finalized (Engineer-led PR cluster pre-flight survey expected before that work begins).

---

## What this document is and is not

**Is:** A pre-scope estimate of cohort-4's design surface based on (a) cohort-3 patterns that are likely to apply, (b) known substrate work in flight (AP Aging cartridge), and (c) the ERPNext deprecation finale's design implications.

**Is not:**
- A committed Track C (the full per-page direction docs will be authored after substrate scope finalizes)
- A binding contract for FED (the patterns named here are extrapolations; some may shift when the actual cartridge contracts land)
- A complete list of cohort-4's eventual scope (additional pages may be added if other cartridge candidates surface)

The point: when Engineer ships AP Aging + the ERPNext deletion PRs, PAO is not starting from zero. The design questions are pre-named; the pattern extrapolations are documented; the work to convert this into Track C is bounded.

---

## Expected scope

### Layer 1: AP Aging cartridge page (mirror of cohort-3 AR Aging)

**Cartridge:** `ApAgingSummary` (substrate W#72 v2; not yet shipped)
**Route:** `/reports/ap-aging` (currently 404 per cohort-3 INDEX Q5 decision — was deliberately not reserved)
**Component:** `ApAgingPage.tsx` (new)

AP Aging mirrors AR Aging structurally — same envelope (`ReportRunResult<TResult>`), same patterns (provisionality / run-on-demand / CSV export), same general layout (TotalsBar + by-vendor + by-category sections + Top N delinquent vendors list).

**Likely differences from AR Aging:**

| Concept | AR Aging (cohort-3) | AP Aging (cohort-4) |
|---|---|---|
| Money direction | Receivables (owed TO operator) | Payables (owed BY operator) |
| Primary grouping | By customer | By vendor |
| Secondary grouping | By property | By expense category (or by property; substrate-dependent) |
| Top-N list | Top delinquent customers | Top vendors with overdue balances |
| Color semantic | Aging buckets in amber/orange/red (overdue = bad for cash position) | Aging buckets in amber/orange/red (overdue = also bad — operator should be paying these but isn't) |
| Anti-pattern to watch | None specific | The "top delinquent vendors" framing inverts the moral valence of AR — these aren't VENDORS who owe US, these are vendors WE owe. UI register should not casually borrow AR's "delinquent" vocabulary; "outstanding" or "overdue" reads more accurately in AP context |

**Pre-scoped design questions for AP Aging Track C:**

1. **Money-direction signal.** AP and AR pages should be visually distinguishable at a glance. Proposal: AR uses red accents on the aging buckets (matches "money owed to us is at risk"); AP uses orange accents on the same buckets (matches "money we owe is also at risk but in a different way") — OR same color scheme but with a clearer page-level visual marker (heading icon; subtitle copy; small status pill in page header). PAO decides at Track C authoring.

2. **"Top delinquent vendors" — language.** As above. The phrase "delinquent" is correct accounting language but reads inverted on the AP side. Alternatives: "Top overdue vendors" / "Vendors with longest outstanding" / "Top outstanding-balance vendors." PAO decides at Track C authoring.

3. **By-category secondary grouping.** AR's "by property" grouping reflects the property-management business (each property has its own AR). AP's secondary grouping may be by expense category (rent expense, utilities, maintenance, etc.) OR by property OR by vendor type. Substrate-dependent; awaiting cartridge contract.

4. **Aging bucket boundaries.** AR uses 0-30 / 31-60 / 61-90 / 90+. AP might want different boundaries (e.g., 0-30 / 31-60 / 61-90 / 90+ same; OR 0-30 net / 31+ overdue if the operator's standard payment terms are NET 30). Substrate-dependent.

### Layer 2: ERPNext deprecation finale

**Substrate context:** Cohort-3 + earlier work moved most pages off direct-ERPNext access onto the Bridge family. Cohort-4 finishes the job. The remaining ERPNext-passthrough chapters need either (a) rebinding to a Bridge endpoint family or (b) deletion if the page is unused.

**Pre-scoped surface — to be confirmed at substrate scope-survey:**

Per cohort-1 + cohort-2 + cohort-3 work, the remaining direct-ERPNext touches are likely in:
- `apps/web/src/api/erpnext.ts` (if it still exists) — the legacy passthrough; cohort-4 deletes
- Any chapter that still calls `erpnextRequest()` or equivalent — cohort-4 rebinds or deletes
- Documentation that references ERPNext as a live dependency — cohort-4 updates to note the deprecation

**Pre-scoped design questions for ERPNext deletion:**

1. **Deprecation messaging.** Is there a user-visible "ERPNext support has been retired; please use [X]" notice anywhere? If yes, what's the timeline and migration path? Likely none (ERPNext was an internal dependency, not a user-facing integration), but worth confirming.

2. **Configuration cleanup.** Any environment variables, config files, or admin-panel settings that reference ERPNext? These need either documentation updates or removal entirely.

3. **Documentation surface.** Cohort-4 likely updates `the-inverted-stack/` book content if any chapter references ERPNext as part of the inverted-stack reference architecture (the book uses Sunfish as the reference; ERPNext is mentioned as the prior thing being replaced). This is book-side cleanup, not design-direction proper; flagged as a coordination concern.

### Layer 3: Backfill — cohort-3 PR 1 primitives consumed by older pages

**Substrate context:** Cohort-3 PR 1 shipped `<StatusPill>` and `<ErrorSurface>` as canonical primitives, promoted from cohort-1 + cohort-2 candidates. Older pages still use inline Tailwind classes that the new primitives subsume.

**Pre-scoped surface:** Backfill these older pages to consume the new primitives:

| Page | Primitive | Cohort |
|---|---|---|
| `MaintenancePage.tsx` | `<StatusPill kind="workOrderStatus">` | 1 |
| `LeaseDetailPage.tsx` payments | `<ErrorSurface variant="retryable">` | 2 |
| `AccountingPage.tsx` | `<StatusPill kind="agingBucket">` + `<ErrorSurface>` | 2 |
| `RentCollectionPage.tsx` | `<ErrorSurface variant="retryable" / "reload" / "redirect">` | 2 |
| `PropertiesPage.tsx`, `LeasesPage.tsx`, etc. | as applicable | 1 |

This is QM/FED housekeeping, NOT Track C scope — but PAO will document it in the eventual cohort-4 INDEX as a "what cohort-3 introduced that should propagate backward" coordination concern.

### Layer 4 (speculative): Additional cartridge candidates

**Substrate context unknown.** If cohort-4 brings additional cartridges beyond AP Aging (e.g., Cash Flow Statement, Balance Sheet, General Ledger Detail, Owner Statement), each gets its own Track C page-direction doc. The provisionality + run-on-demand + CSV-export patterns from cohort-3 transfer directly; only per-cartridge specifics need new authoring.

**Pre-scoped readiness:** if substrate ships 2-3 additional cartridges, Track C authoring time per page is ~1h using the cohort-3 patterns as the structural template + the PAO subagent fan-out model. So 3 additional cartridges = ~3h additional Track C authoring time.

---

## Patterns that carry forward from cohort-3

These are not under question; cohort-4 inherits them directly:

| Pattern | Cohort-3 candidate | Cohort-4 expected use |
|---|---|---|
| `pattern-009` (Bridge endpoint + frontend rebind pair) | Standing | Standing; applies to AP Aging page |
| `pattern-015-provisional-report-surface` (provisionality banner) | Candidate | Likely ratifies on AP Aging second instance |
| `pattern-016-run-on-demand-report` (no auto-fetch; explicit Run) | Candidate | Likely ratifies on AP Aging second instance |
| `pattern-017-csv-export-affordance` (Export CSV + filename convention) | Candidate | Likely ratifies on AP Aging second instance |

Cohort-4 may be the ratification trigger for all three cohort-3 candidate patterns. Worth tracking explicitly during execution.

---

## Components that carry forward from cohort-3 PR 1

These primitives, shipped in cohort-3 PR 1, are available to cohort-4 pages without re-authoring:

| Component | Cohort-3 location | Cohort-4 use |
|---|---|---|
| `<ProvisionalityBanner>` | `apps/web/src/components/` | AP Aging (when `isProvisional`) |
| `<ExportCsvButton>` | `apps/web/src/components/` | AP Aging |
| `<ReportFilterBar>` | `apps/web/src/components/` | AP Aging |
| `<ChartSelector>` | `apps/web/src/components/` | AP Aging |
| `<RunButton>` | `apps/web/src/components/` | AP Aging (via ReportFilterBar) |
| `<StatusPill>` (with new variants if needed) | `@sunfish/ui-react` | AP Aging vendor-status (if any); aging-bucket variants reused |
| `<ErrorSurface>` | `apps/web/src/components/` | AP Aging + cohort-1+2 backfill |
| `<AgingBucketPill>` | `@sunfish/ui-react` | AP Aging |
| `<CurrencyAmount>` | `@sunfish/ui-react` | AP Aging |

**Potential new cohort-4 component candidates** (if substrate justifies):

- `<VendorPill>` — if vendors get a status/category visual treatment beyond what `<StatusPill>` covers
- `<PaymentScheduleBadge>` — if AP Aging surfaces upcoming payment due dates

These are PRE-PRE-SCOPE; defer judgment until substrate ships.

---

## Tokens that carry forward from cohort-3

Per cohort-3 `tokens.md` canonical compositions:

- `provisional-surface` — reused as-is for AP provisionality
- `aging-bucket-header-tint` (×3) — reused as-is for AP aging table headers
- `aging-bucket-pill` (×6) — reused as-is for AP aging bucket pills
- `gl-account-chip` (×5) — likely not needed for AP Aging (which doesn't display GL account types directly)
- `occupancy-badge` (×4) — not needed for AP Aging (no occupancy concept)

**Potential new cohort-4 tokens** (if AP Aging design diverges from AR Aging):

- `payable-direction-accent` — if cohort-4 chooses to visually distinguish AP from AR via a color accent (orange for AP vs red for AR; or via icon; or via subtitle)

PRE-PRE-SCOPE; defer judgment until Track C authoring.

---

## Standing patterns that ratify in cohort-4 (forecast)

Cohort-3 introduced three candidate patterns whose ratification trigger is the second instance:

- **`pattern-015-provisional-report-surface`**: AP Aging is a second instance with `IsProvisional` semantics from the cartridge envelope. Ratification likely.
- **`pattern-016-run-on-demand-report`**: AP Aging will likely follow the same state machine. Ratification likely.
- **`pattern-017-csv-export-affordance`**: AP Aging will likely export CSV via the same `<ExportCsvButton>` component. Ratification likely.

If all three ratify on AP Aging, cohort-4 is the cohort that completes cohort-3's pattern-promotion arc. Worth flagging in the eventual cohort-4 INDEX for institutional record.

---

## Pre-scoped halt conditions (extrapolated)

These are the Engineer-side dependencies cohort-4 will need before Track C can finalize:

| Dependency | Owner | Status |
|---|---|---|
| AP Aging cartridge ships in `Sunfish.Blocks.Reports.Cartridges.ApAgingSummary` | Engineer (W#72 substrate v2) | Pending |
| Bridge endpoint `POST /api/v1/reports/ap-aging` | Engineer | Pending; depends on cartridge |
| Wire types in `apps/web/src/api/reports.ts` | FED | Pending; depends on cartridge contract |
| ERPNext-passthrough remaining surface inventory | Engineer or QM | Pending; needed before deletion PRs can be scoped |
| `<StatusPill>` `vendorStatus` variant API (if vendor status visualization is needed) | FED + PAO | Pending; design decision |

PAO will NOT block on these — pre-scoping work continues against cohort-3 patterns + cohort-4 forecasts. When the dependencies resolve, Track C authoring begins from the pre-scope baseline.

---

## Estimated Track C effort (when substrate lands)

Per cohort-3 actual:
- Phase A (foundation docs; INDEX + tokens + pattern docs that aren't already pinned): ~30 min in main session
- Phase B (page docs; via PAO subagent fan-out): ~30 min wall-clock for AP Aging alone; +30 min wall-clock per additional cartridge page
- Phase C (cross-page audits; component-reuse-audit + states-matrix updates): ~30 min
- Total: ~1.5h for AP-Aging-only; ~2-3h for AP Aging + 2-3 additional cartridges

The ~1.5h baseline is the cohort-4 minimum assuming the cohort-3 patterns carry forward unchanged. Each design-question that requires CIC arbitration adds 15-30 min for the arbitration round-trip.

---

## Next actions

PAO is standing by for any of:
1. Engineer's AP Aging cartridge contract-frozen beacon (triggers Track C Phase A authoring)
2. Engineer's ERPNext deletion surface inventory (triggers documentation update planning)
3. CIC decision on whether to bundle additional cartridges into cohort-4 (triggers expanded Track C scope)
4. Direction to advance other v2-batch items (#7 design-system docs; #5 marketing copy; etc.) while cohort-4 substrate is in flight

This pre-scope document is the cohort-4 design surface as PAO sees it on 2026-05-22. It will be superseded by the full Track C INDEX when substrate is ready. Until then, this document anchors what cohort-4 is expected to do and what PAO has thought about ahead of time.

— PAO, 2026-05-22
