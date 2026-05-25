# ERPNext Deletion Strategy — Cohort-4 Design Direction

**Workstream:** cohort-4 ERPNext-shim retirement (consumer-side of W#74 cluster)
**Owner:** Engineer execution; PAO design direction (this doc); FED + Yeoman audit support
**Authored:** 2026-05-25
**Status:** PRE-SCOPE direction. Engineer surface-inventory pending; PAO direction is
sequence + design constraints + retirement-order recommendation.

## What this document is

Cohort-4 is the cohort that **retires the ERPNext-backed legacy endpoints** in
`apps/web/src/api/erpnext.ts`. Cohort-1 through cohort-3 rebound the frontend onto the
Bridge family (`/api/v1/properties`, `/api/v1/leases`, `/api/v1/cockpit/work-orders`,
`/api/v1/reports/{kind}`); the ERPNext shim file still exists and still has consumers,
even though the deprecation banners landed in cohort-1 and the W#74 retirement window
was named "Cohort 4 (RB-12)."

This document inventories what's left, recommends a retirement order, names the design
constraints PAO carries, and flags risks to Engineer before the deletion PRs open.

## Inventory — endpoints in `apps/web/src/api/erpnext.ts`

Read the file at the cohort-4 worktree base. The exports + their current consumers:

| Export | Replacement | Consumers (per source-tree grep) | Retire? |
|---|---|---|---|
| `Property` (type) | `Property` in `@/api/properties` | (re-export ambiguity; type used only by erpnext.ts internals) | Yes — type definition moves with the function deletion |
| `getProperties()` | `getProperties()` in `@/api/properties` | None in production code; `import.meta.env.DEV` warns on call | Yes — DEAD; no live consumers in `apps/web/src/` |
| `Lease` (type) | `LeaseSummary` in `@/api/leases` | None in production code; only test fixtures | Yes — type moves with deletion |
| `Payment` (type) | TBD (cohort-2 RB-8 left this deferred — see Risk A below) | `apps/web/src/hooks/useLeases.ts` via `getPayments` import | NO — Payment type still load-bearing for `useLeases.usePayments()` until RB-8 rebinds it |
| `RecordPaymentInput` (type) | TBD (same as above) | `RentCollectionPage.tsx` via `recordPayment` | NO — same reason |
| `getLeases()` | `getLeases()` in `@/api/leases` | None in production code; deprecated warning | Yes — DEAD |
| `getLease(name)` | `getLease(name)` in `@/api/leases` | None in production code; deprecated warning | Yes — DEAD |
| `getPayments()` | NOT YET REBOUND | `apps/web/src/hooks/useLeases.ts` (`usePayments`) | NO — load-bearing; defer until cohort-4 PR Pn (see below) |
| `recordPayment(payload)` | NOT YET REBOUND | `apps/web/src/pages/RentCollectionPage.tsx` | NO — load-bearing; defer until cohort-4 PR Pn (see below) |
| `AccountingSummary` (type) | NOT YET REBOUND | `apps/web/src/pages/AccountingPage.tsx` via `getAccountingSummary` | NO — load-bearing |
| `OutstandingInvoice` (type) | NOT YET REBOUND | `apps/web/src/pages/AccountingPage.tsx` via `getAccountingOutstanding` | NO — load-bearing |
| `getAccountingSummary()` | NOT YET REBOUND | `apps/web/src/pages/AccountingPage.tsx` | NO — load-bearing |
| `getAccountingOutstanding()` | NOT YET REBOUND | `apps/web/src/pages/AccountingPage.tsx` | NO — load-bearing |
| `MaintenanceTicket` (type) | `WorkOrderSummary` in `@/api/maintenance` | None in production code; type used only by deprecated functions | Yes — type moves with deletion |
| `CreateMaintenanceInput` (type) | Bridge equivalent in `@/api/maintenance` | None in production code | Yes — DEAD |
| `UpdateMaintenanceInput` (type) | Bridge equivalent in `@/api/maintenance` | None in production code | Yes — DEAD |
| `getMaintenanceTickets()` | `getWorkOrders()` in `@/api/maintenance` | None in production code; deprecated | Yes — DEAD |
| `createMaintenanceTicket(payload)` | `createWorkOrder()` in `@/api/maintenance` | None in production code; deprecated | Yes — DEAD |
| `updateMaintenanceTicket(name, payload)` | DEFERRED (cohort-1 hand-off note: "status-update is deferred to a follow-up PR") | None in production code | Yes — DEAD (status-update was never rebound; the call site that needed it has been replaced or fell off) |
| `RentRollRow` (type) | `RentRoll` cartridge result type in `@/api/reports` | `apps/web/src/pages/RentRoll.tsx` (LEGACY page) — replaced by `RentRollPage.tsx` in cohort-3 PR 2 | NO if `RentRoll.tsx` still mounted; YES if cohort-3 PR 2 retired it |
| `PLLineItem`, `PLSummary` (types) | `ProfitAndLossByProperty` cartridge result types | `apps/web/src/pages/PLReport.tsx` (LEGACY page) — replaced by `ProfitAndLossByPropertyPage.tsx` in cohort-3 PR 3 | NO if `PLReport.tsx` still mounted; YES if cohort-3 PR 3 retired it |
| `getRentRoll()` | Bridge `/api/v1/reports/rent-roll` (cohort-3 PR 2 cartridge) | `apps/web/src/pages/RentRoll.tsx` (LEGACY) | Conditional — see PR sequencing |
| `getProfitLoss(...)` | Bridge `/api/v1/reports/profit-loss` (cohort-3 PR 3 cartridge) | `apps/web/src/pages/PLReport.tsx` (LEGACY) | Conditional — see PR sequencing |
| `exportProfitLoss(...)` | `<ExportCsvButton>` on `ProfitAndLossByPropertyPage` (pattern-017) | `apps/web/src/pages/PLReport.tsx` (LEGACY) | Conditional — see PR sequencing |
| `apiFetch<T>` (internal) | (internal helper; not exported) | All deprecated functions | Goes when the file goes |

## Risk surface

### Risk A — Payment rebind never landed (cohort-2 RB-8 deferred)

The `getPayments()` + `recordPayment()` pair was scheduled for cohort-2 RB-8. The
deprecation banner says `Cohort 2 RB-8`, but per the cohort-2 retrospective + the
current state of `apps/web/src/hooks/useLeases.ts`, **the rebind never happened.** The
hook still imports `getPayments` from `@/api/erpnext` and the comment line `Keep
usePayments() calling /api/v1/erpnext/payments — Cohort 2 RB-8 rebinds it.` is still
live.

**Cohort-4 cannot delete `erpnext.ts` until the payment-rebind is shipped.** This is
NEW substrate scope for Engineer, not just a deletion. Cohort-4 absorbs it (the
deletion is the goal; the rebind is the prerequisite).

Recommended substrate scope addition: a `getPayments()` + `recordPayment()` Bridge
endpoint pair under `/api/v1/leases/{leaseId}/payments` (collection GET + POST). Per
pattern-009 (standing), this is a Bridge endpoint + frontend rebind pair. PAO direction:
**this rebind is its own PR within cohort-4, NOT bundled into the deletion PR.** Two
reasons: (1) the rebind has its own substrate dependency that the deletion does not,
and (2) sequencing the rebind first means the deletion PR carries no risk of dangling
imports.

### Risk B — Accounting page never rebound (cohort-2 oversight)

`AccountingPage.tsx` still calls `getAccountingSummary()` + `getAccountingOutstanding()`
from `@/api/erpnext`. These endpoints are NOT deprecated in the source file (no JSDoc
deprecation banner) — they were apparently treated as "fine to leave on ERPNext until
the architecture matures." But cohort-4's goal is **complete ERPNext deletion**, which
means these endpoints either get a Bridge rebind OR the `AccountingPage.tsx` gets
deleted entirely (replaced by the cohort-3 reports cluster — TrialBalancePage +
ArAgingPage may cover the operator's accounting needs without a separate
AccountingPage).

**PAO direction: investigate AccountingPage's role before deciding rebind-or-delete.**
If `AccountingPage.tsx` is still actively mounted in the SPA router AND its
"AccountingSummary" tiles + "OutstandingInvoice" table provide unique UX that
TrialBalancePage / ArAgingPage do not cover, then the rebind is needed (new Bridge
endpoints + new cartridge OR a derived view from existing cartridges). If
`AccountingPage.tsx` is duplicative with the cohort-3 reports cluster, deleting it
entirely (route + page + hook + test) is cleaner.

Recommended FED + PAO audit before cohort-4 deletion-PR opens: read
`AccountingPage.tsx`, the route registration in `apps/web/src/App.tsx` (or wherever
routes live), and the nav-entry that surfaces it. Decide rebind-or-delete. **Cohort-4
plan: bundle this decision into the cohort-4 INDEX as Q1, surface to CIC for
ratification before the deletion PR opens.**

### Risk C — RentRoll.tsx + PLReport.tsx legacy pages may still be mounted

Cohort-3 PR 2 (`RentRollPage.tsx`) and cohort-3 PR 3
(`ProfitAndLossByPropertyPage.tsx`) shipped as **new** pages. Whether the legacy
`RentRoll.tsx` + `PLReport.tsx` files are still mounted on routes
(`/rent-roll` / `/p-l-report`) depends on the cohort-3 PR scope — did the PRs replace
the legacy routes, or add new routes alongside the legacy?

**Cohort-4 deletion must verify route-by-route that the legacy pages are unreferenced
before deleting the legacy `getRentRoll()` / `getProfitLoss()` / `exportProfitLoss()`
exports.** This is FED audit work, not new substrate — but it's load-bearing for
deletion-PR safety. If the legacy routes are still mounted (intentionally, for fallback
or for user-preference flag), the legacy `erpnext.ts` exports stay live until cohort-5
or later.

**PAO direction: cohort-4 deletion-PR includes route audit in its acceptance
checklist.** If legacy routes are still mounted, **do not delete the legacy ERPNext
exports**; instead, file a follow-up directive to retire the legacy routes first
(probably a separate cohort-4 PR or a cohort-5 item).

### Risk D — `Payment` type import-chain through tests and fixtures

Several test files (`MaintenancePage.test.tsx`, `LeaseDetailPage.test.tsx`,
`RentRoll.test.tsx`, `PLReport.test.tsx`, `LeasesPage.test.tsx`,
`PropertiesPage.test.tsx`) import from `@/api/erpnext` per the grep evidence. Most are
likely importing **types** (not functions), but the mock-ERPNext fixture pattern may
also depend on the file's existence.

**PAO direction: cohort-4 deletion-PR audits test imports.** Each test file that
imports from `@/api/erpnext` either:
1. Migrates the import to the rebound module (`@/api/properties`, `@/api/leases`,
   `@/api/maintenance`, `@/api/reports`), OR
2. If the test asserts on ERPNext-shape data that no longer exists post-deletion, the
   test gets updated to assert on the cartridge-shape data.

This is mechanical test-migration scope, NOT design-direction — FED owns it per the
deletion PR. PAO flags it here so the PR scope is named correctly upfront.

### Risk E — `apps/web/src/api/properties.ts` references erpnext.ts in a comment

The grep evidence shows `properties.ts:3:// in src/api/erpnext.ts. Per hand-off §3.2:`
— a documentation comment in `properties.ts` that references the legacy file. The
comment is HARMLESS (it's just historical context), but cohort-4 deletion-PR should
clean it up so post-deletion the comment doesn't point at a non-existent file.

This is a trivial line-update in the deletion PR. Flagged here for PR scope.

## Recommended retirement order

Cohort-4 deletion is NOT a single PR. PAO direction sequences it as **4 PRs minimum,
plus 1 conditional**:

### PR Pn (Cohort-4 P1) — Payment rebind

**Scope:**
- Add `getPayments(leaseId)` + `recordPayment(leaseId, payload)` to `@/api/payments`
  (new module) calling new Bridge endpoints `GET /api/v1/leases/{leaseId}/payments` +
  `POST /api/v1/leases/{leaseId}/payments`.
- Migrate `useLeases.ts` import from `@/api/erpnext` to `@/api/payments`.
- Migrate `RentCollectionPage.tsx` import from `@/api/erpnext` to `@/api/payments`.
- Add deprecation banners to `getPayments()` + `recordPayment()` in `erpnext.ts` for
  the brief window before deletion (helps any straggler imports surface).

**Pre-requisite:** Engineer ships the Bridge endpoint pair as its own substrate PR
(parallel to the FED rebind PR). pattern-009 standing applies — 2 PRs paired (Bridge
+ FED).

**PAO direction:** the new `<PaymentList>` + `<RecordPaymentForm>` components inherit
the cohort-2 visual language from existing payment-related surfaces; no NEW design
direction needed. If the rebind introduces a payment-status pill (Pending /
Completed), it consumes the cohort-1 `<StatusPill>` palette — Pending = amber,
Completed = green.

### PR Pn+1 (Cohort-4 P2) — Accounting page rebind OR retirement

**Scope (decided after CIC ratification on Q1 from cohort-4 INDEX):**

**If rebind:** Add `getAccountingSummary()` + `getAccountingOutstanding()` to a new
`@/api/accounting` module calling new Bridge endpoints. New cartridge OR new derived
view. PAO design direction needed if the page surfaces a new visual register
(currently the page exists and renders summary tiles + outstanding-invoice table —
the existing visual register transfers).

**If retirement:** Delete `apps/web/src/pages/AccountingPage.tsx`, the route, the nav
entry, and the test file. PAO design direction: confirm in cohort-4 INDEX which
sister pages absorb the discoverable surface (likely TrialBalancePage for summary;
ArAgingPage for outstanding-invoice — both are cohort-3-shipped).

### PR Pn+2 (Cohort-4 P3) — Legacy route audit + retirement (conditional)

**Scope (conditional on Risk C audit outcome):**

If legacy `RentRoll.tsx` + `PLReport.tsx` are still mounted:
- Remove route registrations from `App.tsx` (or equivalent)
- Remove nav entries pointing at legacy routes
- Delete legacy page files + test files
- Migrate any preserved test assertions to the cohort-3 page test files
- PAO design direction: NONE (mechanical retirement, no new UX)

If legacy routes are already retired (cohort-3 took them out):
- Skip this PR entirely; legacy-page files may already be deleted

### PR Pn+3 (Cohort-4 P4) — `erpnext.ts` deletion

**Scope:**
- Delete `apps/web/src/api/erpnext.ts` entirely
- Audit all `@/api/erpnext` imports across `apps/web/src/`; each one either:
  - Already migrated (cohort-1/2/3 work) — verify
  - Migrated by cohort-4 P1/P2/P3 — verify
  - Test fixture using ERPNext-shape data — migrate to cartridge-shape (per Risk D)
- Clean up the `properties.ts` historical comment (per Risk E)
- Update `.wolf/anatomy.md` (sunfish repo) — remove the `erpnext.ts` entry
- Update any documentation that references ERPNext as a live dependency (per
  PRE-SCOPE Layer 2 "Pre-scoped design questions for ERPNext deletion" Q3 —
  documentation surface)

**Acceptance:**
- `grep -rln "from '@/api/erpnext'" apps/web/src/` returns ZERO matches
- `grep -rln "/api/v1/erpnext" apps/web/src/` returns ZERO matches (no remaining
  raw-URL-string references either)
- Full Vite build green
- Full test suite green
- No new console warnings on dev-build mount
- PR description claims `@deprecation-finale` candidate pattern (NEW pattern for
  cohort-4 — see Patterns section below)

### PR Pn+4 (Cohort-4 P5) — Configuration + environment cleanup

**Scope (per PRE-SCOPE Layer 2 Q2):**
- Audit `apps/web/.env.example` (or equivalent) for ERPNext-named environment
  variables — remove
- Audit Bridge service configuration (`signal-bridge/src/...config...` paths) for
  ERPNext upstream-URL configuration — remove if the Bridge's ERPNext-passthrough
  routes are also being deleted (which they should be — see Risk F below)
- Audit any admin-panel settings that reference ERPNext — remove
- Audit `package.json` for ERPNext-related dependencies (unlikely — the shim is
  fetch-only — but worth a grep)

This PR is small and mostly removes config that should never have been load-bearing.
**Bundle with P4 if scope is trivial; separate PR if Bridge-side cleanup is
non-trivial.**

### Risk F (added during inventory) — Bridge-side ERPNext routes still exist

The frontend imports show `/api/v1/erpnext/properties`, `/api/v1/erpnext/leases`,
`/api/v1/erpnext/maintenance`, etc. — these are Bridge routes that proxy to ERPNext.
After cohort-4 deletes the FED consumer, those Bridge routes should ALSO be retired —
otherwise the Bridge carries dead routes that present a security/audit surface for no
runtime benefit.

**PAO direction: Bridge-side ERPNext route retirement is part of cohort-4 scope, but
owned by Engineer at the signal-bridge repo, not by FED in sunfish.** A parallel
cohort-4 Engineer PR in signal-bridge deletes the `/api/v1/erpnext/*` route family
(per pattern-009 backward — Bridge deletion paired with FED-import deletion).

**Sequencing:** Bridge-side route retirement MUST land AFTER all FED rebinds complete
(otherwise mid-deployment a request to a deleted Bridge route 404s). The order:
1. Cohort-4 P1 (payment rebind FED + Bridge endpoints)
2. Cohort-4 P2 (accounting rebind OR retirement)
3. Cohort-4 P3 (legacy route audit)
4. Cohort-4 P4 (FED `erpnext.ts` deletion)
5. Cohort-4 P5 (config cleanup; bundle with P4 if trivial)
6. Cohort-4 P6 (signal-bridge ERPNext route deletion) — LAST; Bridge can't drop
   routes until all consumers are off them

## Design constraints PAO carries

PAO direction across all cohort-4 deletion PRs:

1. **No user-visible regressions.** Every page that currently works must continue to
   work end-to-end after the deletion. The bar: a user runs the app, exercises every
   tab in the nav, and notices nothing changed. (Possible exception: if Q1 ratifies
   AccountingPage retirement, the nav loses one entry — that IS user-visible, but
   it's intentional UX change, not regression.)

2. **No deprecation messaging surfaces in UI.** ERPNext was an internal dependency,
   never user-facing. Users will not see "ERPNext support has been retired" notices —
   there's nothing for them to migrate. (Per PRE-SCOPE Layer 2 Q1.)

3. **Test coverage maintained.** Every test currently asserting on ERPNext-shape data
   migrates to cartridge-shape OR is replaced by a parallel cartridge-shape test.
   Net coverage delta: ≥0%; tests don't drop from the suite.

4. **Console clean.** No new dev-mode warnings post-deletion. The deprecation
   `console.warn` calls in `erpnext.ts` GO AWAY with the file; nothing replaces them.

5. **Documentation parity.** Any architecture document (in `_shared/architecture/`,
   in `.wolf/anatomy.md`, in README files) that names ERPNext as part of the live
   data plane gets updated to reflect the post-cohort-4 reality. The book
   (`the-inverted-stack/`) references ERPNext as a historical antecedent —
   coordinate with PAO book-side on whether the book mentions need updating (likely
   none, since the book uses Sunfish as reference; ERPNext appears in the inverted-
   stack thesis as the "prior architecture being replaced," which remains
   historically true regardless of whether Sunfish still proxies to it).

6. **No new design tokens.** Deletion work surfaces no new visual primitives. (The
   one possible exception: if the cohort-4 P1 payment rebind introduces a payment-
   status pill, that consumes the cohort-1 `<StatusPill>` palette unchanged.)

## Cross-cohort impact

| Workstream | Impact |
|---|---|
| Cohort-5+ design direction | Cohort-4 establishes that "frontend completely off ERPNext" is the substrate baseline; cohort-5 designs can assume no fallback proxy exists. Forward-watch: cohort-5+ may surface use cases that previously fell back to ERPNext — those become net-new scope, not migration. |
| Book continuity | The book treats ERPNext as the prior-state; the post-cohort-4 reality matches the book's narrative claim. No book updates needed. |
| pattern-009 ratification cycle | Each PR pair (Bridge + FED) in cohort-4 instantiates pattern-009 — by cohort-4 end, pattern-009 has well over the 5-instance threshold for cross-cohort entrenchment. The retirement-pattern (call it `@candidate-pattern: pattern-018-shim-retirement-finale`) is a NEW candidate; see Patterns section. |
| QM / audit | The deletion sweep creates a clean audit baseline — post-cohort-4, no agent can claim "I'm just adding to the legacy ERPNext shim because that's where the code currently lives." The shim doesn't exist. |
| Operator-side workflow | Zero user-facing change (the operator already exercises the cohort-1/2/3 surfaces). Bridge-side latency may improve marginally (one fewer hop through ERPNext for any rarely-exercised legacy path). No metric-tracking needed. |

## Patterns

### Candidate pattern-018 — Shim retirement finale

Cohort-4 introduces a **shim-retirement-finale** pattern: the cohort that retires a
deprecated module after preceding cohorts rebound consumers off it. The pattern is
distinct from pattern-009 (which is the rebind itself) because the finale operates on
the AFTER state — the shim's exports are dead because the rebind has shipped, and the
finale is a coordinated audit + deletion + config-cleanup sweep, NOT a new substrate
build.

**Pattern shape:**
- 4-6 PR sequence: rebind any lagging consumers, audit-mount-and-routes, delete shim
  file, clean config, retire Bridge-side proxy routes
- PR description claims `@candidate-pattern: pattern-018`
- Cross-repo coordination: FED + Engineer + (optionally) PAO for retirement-or-rebind
  judgment calls
- Acceptance criterion: zero grep matches on the deprecated module path AND zero
  user-facing regressions AND zero new console warnings

**Ratification trigger:** the SECOND time the fleet retires a deprecated module via
the same sweep pattern. (Cohort-4 is first instance; whichever cohort retires the
NEXT shim will be the ratification trigger.)

### Standing patterns exercised

- pattern-009 (Bridge endpoint + frontend rebind pair) — exercised by every PR in
  cohort-4 P1/P2/P6 sequences

## Halt conditions

| Halt | Owner | Resolution |
|---|---|---|
| Q1 (cohort-4 INDEX) — AccountingPage rebind-or-retire | CIC | Ratify before P2 PR opens |
| Q2 (cohort-4 INDEX) — Legacy RentRoll.tsx + PLReport.tsx still mounted? | FED audit | Investigate App.tsx routes; report in cohort-4 PR 0 (status beacon, not a code PR) |
| Q3 (cohort-4 INDEX) — Bridge-side ERPNext route family scope | Engineer at signal-bridge | Author parallel Engineer scope in signal-bridge cohort-4 PR sequence |
| pattern-009 SPOT-CHECK on each cohort-4 PR pair | Admiral | Dispatch security-engineering council per fleet SLA |
| Payment-bind cartridge scope (if cohort-4 P1 needs new substrate) | Engineer (W#74 v2) | Spec the payment-bind cartridge ahead of FED P1 work |

## Acceptance — cohort-4 as a whole

The cohort is COMPLETE when:

1. `apps/web/src/api/erpnext.ts` does not exist on `main`
2. No `from '@/api/erpnext'` imports anywhere in `apps/web/src/`
3. No `/api/v1/erpnext/*` routes exist in signal-bridge
4. No ERPNext-named environment variables in `apps/web/.env.example` or Bridge config
5. AccountingPage either rebound to Bridge OR deleted (per Q1)
6. Legacy RentRoll.tsx + PLReport.tsx either deleted OR confirmed unmounted (per Q2)
7. `.wolf/anatomy.md` (sunfish) reflects post-deletion file tree
8. Full test suite green
9. Full Vite build green
10. No new dev-mode console warnings
11. Cohort-4 INDEX updated with "RATIFIED" status + retrospective notes

— PAO, 2026-05-25
