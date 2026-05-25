# Cohort-4 Design Direction — AP Aging + ERPNext Deletion Finale

**Workstream:** cohort-4 (post-cohort-3; the cohort that retires ERPNext + ships AP Aging)
**Status:** Track C design direction — PRE-SCOPE (substrate not yet shipped). Documents
in this directory can be read standalone; FED + Engineer + Yeoman consume them when
substrate begins shipping.
**Authored:** 2026-05-25 (PAO; v2-batch 3 pre-scope deliverable)
**Successor:** When AP Aging substrate ships, this INDEX gets a "RATIFIED" entry per
artifact + cross-references to the PR cluster.

## Contents

| Doc | Scope |
|---|---|
| [`PRE-SCOPE.md`](./PRE-SCOPE.md) | Original cohort-4 pre-scope (authored 2026-05-22). Forecast of cohort surface; superseded in detail by the per-page docs below but retained as the cohort's chartered scope |
| [`01-ap-aging-page.md`](./01-ap-aging-page.md) | ApAgingPage design direction — mirror of cohort-3 ArAgingPage with AP-specific deltas (vendor/category grouping, "outstanding" not "delinquent" copy register, page-header money-direction pill) |
| [`02-erpnext-deletion-strategy.md`](./02-erpnext-deletion-strategy.md) | ERPNext shim retirement strategy — inventory, retirement order, risk surface, PR sequence (4-6 PRs in cohort-4), cross-cohort impact, candidate pattern-018 |
| [`tokens.md`](./tokens.md) | Cohort-4 token supplement — page-header money-direction pill (NEW), other tokens reused from cohort-3 unchanged |

## Scope summary

Cohort-4 has two parallel deliverables:

1. **AP Aging page** — surfaces accounts-payable aging via cartridge `ApAgingSummary`
   (substrate W#72 v2; not yet shipped). Mirror of cohort-3 ArAgingPage with content +
   copy deltas. Acts as the second-instance ratification trigger for cohort-3
   candidate patterns 015 / 016 / 017 (provisional / run-on-demand / CSV-export). 1 PR.

2. **ERPNext shim retirement** — deletes `apps/web/src/api/erpnext.ts`, rebinds the
   remaining live consumers (Payment + Accounting), audits legacy pages, cleans config,
   retires Bridge-side proxy routes. 4-6 PRs across sunfish + signal-bridge.

The two deliverables are **independent** — neither blocks the other. AP Aging can ship
whenever substrate lands; ERPNext deletion proceeds on its own sequence. Cohort-4 is
complete when both deliverables ratify.

## Standing pattern alignment

- **pattern-009** (Bridge endpoint + frontend rebind pair, formal post-cohort-1) —
  exercised by every PR in cohort-4 (AP Aging + Payment rebind + Accounting rebind +
  Bridge-route retirement). Mandatory SPOT-CHECK from security-engineering council per
  fleet SLA on each PR open.

## Patterns ratifying in cohort-4

Three candidates from cohort-3 ratify on their second instance:

- **`pattern-015-provisional-report-surface`** — first instance: cohort-3 ArAgingPage
  + 3 other report pages (4 instances within one cohort). Second cohort: cohort-4
  ApAgingPage. RATIFIES as standing pattern when AP Aging ships.
- **`pattern-016-run-on-demand-report`** — same pattern of ratification. RATIFIES on
  AP Aging.
- **`pattern-017-csv-export-affordance`** — same pattern of ratification. RATIFIES on
  AP Aging.

After cohort-4 ships, the three ratifications get folded into
`shipyard/_shared/standing-approved-patterns.md` per fleet convention.

## Candidate patterns introduced

- **`pattern-018-shim-retirement-finale`** — the multi-PR sweep that retires a
  deprecated module after preceding cohorts rebind consumers off it. First instance:
  cohort-4 ERPNext deletion. Ratification trigger: the SECOND time the fleet retires
  a deprecated module via the same sweep pattern.

## Surfaced design questions (CIC ratification needed before PRs open)

### Q1 — AccountingPage rebind-or-retirement

`apps/web/src/pages/AccountingPage.tsx` still calls `getAccountingSummary()` +
`getAccountingOutstanding()` from `@/api/erpnext`. Cohort-4 cannot delete the shim
until this page either:
- (A) Is rebound to a new Bridge endpoint family, OR
- (B) Is retired entirely (route + page + nav entry removed)

**PAO recommendation (deferred to CIC):** investigate the page's role first. If
TrialBalancePage + ArAgingPage (cohort-3-shipped) cover the operator's accounting
discoverable surface, **retire AccountingPage** (Option B) — cleaner architecture, one
less page to maintain, no new substrate cost. If AccountingPage surfaces unique UX
(quick-glance summary tiles + outstanding-invoice quick triage) that the report
cluster doesn't, **rebind it** (Option A) — preserves operator workflow.

**Decision authority:** CIC. **Resolution path:** PAO authors a 1-page audit of
AccountingPage's role vs cohort-3 report cluster; CIC ratifies the call before the P2
PR opens.

### Q2 — Legacy RentRoll.tsx + PLReport.tsx still mounted?

Cohort-3 PR 2 + PR 3 shipped `RentRollPage.tsx` + `ProfitAndLossByPropertyPage.tsx`
as **new** pages. Whether the cohort-3 PRs also retired the legacy routes is
unverified. If legacy routes are still mounted, the legacy ERPNext exports
(`getRentRoll()`, `getProfitLoss()`, `exportProfitLoss()`) stay live until cohort-5+;
if not, cohort-4 deletion proceeds clean.

**Decision authority:** FED audit. **Resolution path:** FED reads
`apps/web/src/App.tsx` (or equivalent) + nav config, reports route-mount status in a
cohort-4 status beacon BEFORE the P4 deletion PR opens. If still mounted, a parallel
cohort-4 PR retires the legacy routes first (probably a separate P3 PR).

### Q3 — Bridge-side ERPNext route family scope

Cohort-4 retires the `/api/v1/erpnext/*` route family on the Bridge side AFTER all
FED consumers are off them. Sequencing matters — Bridge can't drop routes until all
deployment surfaces (production + staging + dev) are on the rebind. The retirement
PR sequence end-state.

**Decision authority:** Engineer at signal-bridge. **Resolution path:** Engineer
spec a parallel cohort-4 PR sequence in signal-bridge that mirrors the FED sequence
+ retires Bridge proxy routes last.

### Q4 — Payment-rebind cartridge scope

Cohort-4 P1 introduces a `getPayments()` + `recordPayment()` rebind that needs Bridge
endpoints to land. The substrate scope is new (not just a rebind of an existing
Bridge endpoint). Is the payment-bind a new cartridge in
`shipyard/packages/blocks-reports/` (treating Payment as a Report) OR a new
non-cartridge endpoint family in signal-bridge?

**PAO recommendation:** Payment is NOT a Report (it's a transactional write-path,
not a read-only run-on-demand surface). The pattern-016 run-on-demand model does
NOT fit. PAO recommends a NON-cartridge Bridge endpoint family,
`/api/v1/leases/{leaseId}/payments` (GET collection + POST), under a new
`signal-bridge/src/.../payments-controller.cs` (or equivalent), backed by whatever
substrate is appropriate for write-path payment recording (likely the existing
cohort-2 financial-write-path cluster — `pattern-012` family).

**Decision authority:** Engineer. **Resolution path:** Engineer authors substrate
scope; PAO available for design direction on any visual surface the rebind
introduces (e.g., payment-status pill).

### Q5 — Money-direction signal on AR Aging (backfill scope)

The AP Aging page design direction introduces a page-header pill (`⟪ Payables ⟫` /
`⟪ Receivables ⟫`) for money-direction orientation. AP Aging ships with the pill;
AR Aging must be backfilled with the parallel `Receivables` pill so the visual
discipline reads consistently across both pages.

**PAO direction:** backfill AR Aging in the AP Aging PR itself (single-line JSX
addition + token reference); OR file as a 1-line follow-up PR if scope hygiene
prefers. PAO opinion: **bundle**. The backfill is 1 line of JSX + 1 token reference
— smaller than a separate-PR overhead. The PR description claims pattern-009
twice (one for AP page, one for AR backfill — both consume the same Bridge endpoint
they already do, the backfill is purely cosmetic).

**Decision authority:** PAO + FED. **Resolution path:** noted in AP Aging PR
description; backfill JSX inline.

### Q6 — Top N list — graduation to shared `<TopNList>` primitive?

AP Aging's TopOutstandingList is structurally identical to AR Aging's TopDelinquent
List (rank chip + name + secondary stat + total). Two cohorts using the same
component shape is the typical promotion trigger.

**PAO recommendation:** **DO NOT promote in cohort-4.** Both lists are tiny (≤40
LOC each); the duplication is not painful enough to justify a separate PR for
primitive extraction. Defer to cohort-5 OR to the cohort that introduces the
canonical `<DataTable>` primitive (which would absorb the Top N list along with the
underlying AgingTable). Decision is **explicit deferral**, not "we forgot."

**Decision authority:** PAO. **Resolution path:** documented here as a forward-watch
item; no action needed in cohort-4.

## Pending halt conditions (substrate-side)

These are the Engineer-side dependencies cohort-4 needs before the FED PRs can
finalize:

| Dependency | Owner | Status |
|---|---|---|
| AP Aging cartridge ships in `Sunfish.Blocks.Reports.Cartridges.ApAgingSummary` | Engineer (W#72 substrate v2) | Pending |
| Bridge endpoint `POST /api/v1/reports/ap-aging` | Engineer | Pending; depends on cartridge |
| Wire types in `apps/web/src/api/reports.ts` (`ApAgingSummary` shape) | FED | Pending; depends on cartridge contract |
| Payment-bind Bridge endpoints (cohort-4 P1) | Engineer | Pending; needs Q4 ratification |
| Accounting rebind Bridge endpoints (cohort-4 P2, if Q1 ratifies rebind) | Engineer | Pending; needs Q1 ratification |
| AccountingPage role audit (cohort-4 Q1) | PAO + FED + CIC | Pending |
| Legacy route mount status (cohort-4 Q2) | FED | Pending; trivial grep work |
| signal-bridge ERPNext route retirement scope (cohort-4 Q3) | Engineer | Pending; depends on FED rebind sequencing |

PAO is NOT blocked on these — pre-scoping work continues. When the dependencies
resolve, Track C authoring advances per per-PR design-direction docs (already drafted
for AP Aging; ERPNext deletion strategy is more inventory + sequencing than visual
direction).

## Sequencing — full cohort-4 schedule

```
T0 (today, 2026-05-25):
  - This INDEX + AP Aging direction + ERPNext deletion strategy + tokens.md committed
  - Q1 + Q2 + Q3 + Q4 routed to ratifiers via separate beacons

T0+1d-7d (depending on ratifier latency):
  - Q1 + Q2 + Q3 + Q4 ratifications return
  - FED files Q2 audit status beacon

T0+? (when W#72 v2 substrate ships):
  - Engineer ships AP Aging cartridge → AP Aging PR opens
  - In parallel: Engineer ships payment-bind Bridge endpoints (cohort-4 P1
    Bridge-side) → FED P1 PR opens (Payment rebind on sunfish)
  - In parallel: if Q1 ratified Option A: Engineer ships Accounting rebind Bridge
    endpoints → FED P2 PR opens (Accounting rebind)
  - In parallel: if Q1 ratified Option B: FED P2 PR opens (Accounting retirement)
  - Each PR triggers SPOT-CHECK dispatch per pattern-009 SLA

T0+? (after P1 + P2 + any P3 legacy-route retirement land):
  - FED P4 PR opens — `erpnext.ts` deletion
  - Engineer parallel signal-bridge PR opens — `/api/v1/erpnext/*` route retirement

T0+? (after P4 lands):
  - Cohort-4 P5 config cleanup PR (small; may bundle with P4)
  - Cohort-4 RATIFIED — INDEX updated with retrospective notes
  - candidate-pattern-018 enters the standing-pattern watch list
```

## Cross-cohort coordination concerns

| Concern | Status |
|---|---|
| Cohort-3 PR 1 primitives (`<StatusPill>`, `<ErrorSurface>`) backfill to cohort-1/2 pages | PRE-SCOPE Layer 3. Documented as forward-watch; QM/FED housekeeping NOT in cohort-4 scope. Cohort-5 or as-needed. |
| Cohort-4 may unlock `<DataTable>` primitive scope for cohort-5 | The AgingTable in AP + AR Aging is the third instance of "tabular numeric data table" shape (after TrialBalanceTable + RentRollPage UnitTable). Promotion to `<DataTable>` primitive is a cohort-5 candidate; forward-watch flagged. |
| Audiobook + book repo updates re: ERPNext historical references | None needed. The book treats ERPNext as the prior-state architecture being replaced; the post-cohort-4 reality matches the book's claim. |
| QM daemon — post-cohort-4 audit baseline | Once `erpnext.ts` is deleted, QM daemon flags any agent attempting to add new code at that path as "trying to revive a retired shim" — surface to Admiral for ruling. |

## Reference inputs

- **Cohort-3 design direction** — `shipyard/_shared/design/cohort-3/` (structural
  template for AP Aging; pattern-015/016/017 candidate docs)
- **Cohort-4 PRE-SCOPE** — `shipyard/_shared/design/cohort-4/PRE-SCOPE.md` (authored
  2026-05-22; cohort surface forecast)
- **Sunfish frontend source** — `sunfish/apps/web/src/api/erpnext.ts` (deletion
  target; inventoried in `02-erpnext-deletion-strategy.md`)
- **Sunfish consumers** — `sunfish/apps/web/src/{hooks,pages,api}/` (grep evidence
  for current ERPNext imports)
- **Signal-Bridge ERPNext routes** — `signal-bridge/src/.../erpnext-controller.cs`
  (or equivalent; Engineer audits)
- **W#72 substrate v2** — `shipyard/packages/blocks-reports/` (AP Aging cartridge
  pending)
- **Standing patterns** — `shipyard/_shared/standing-approved-patterns.md`
  (pattern-009 baseline; cohort-4 ratifies 015 + 016 + 017)

— PAO, 2026-05-25
