# ONR research — ERPNext-conversion + backlog register (reconciled) (2026-05-29)

**Requester:** CIC (via Admiral dispatch)
**Scope:** Authoritative, reconciled enumeration of ALL remaining unfinished backlog
+ ERPNext-conversion items. In scope: every ERPNext module/domain → Sunfish block →
bundle conversion-parity row (DONE/IN-FLIGHT/REMAINING), plus the non-conversion launch
backlog. Out of scope: writing production code, authoring ADRs, prioritization rulings
(those are Admiral/CIC). Method: cross-reference the canonical planning docs against
MERGED reality (git/PRs/shipped-packages-on-disk/live-routes), never trusting any single
doc's pending/shipped markers. Success = a 3-state register + the doc-drift findings.
**Status:** final
**Confidence:** HIGH on what's built (verified on disk + merged PRs). MEDIUM on a few
effort sizings (carried forward from prior ONR surveys, not re-validated with Engineer).

---

## TL;DR

1. **The roadmap-tracker (2026-04-19) is profoundly stale and the single biggest source
   of confusion.** It shows ZERO `blocks-*` packages shipped (only Foundation packages +
   5 "Draft" bundles) and an aspirational P1–P6 with 20+ unbuilt module names. **Reality:
   38 `blocks-*` packages exist on disk + ~104 packages total in `Shipyard.slnx`.** The
   tracker pre-dates ALL cohort work and essentially every P-phase listed as "blocked" is
   in fact substrate-shipped. Treat it as a historical artifact, not a status doc.

2. **ERPNext-conversion parity is ~90% DONE at the substrate + UI layer.** The property-
   management vertical (properties, leases, rent/billing, work-orders/maintenance,
   inspections, listings, accounting, reports, audit-trail, tenant onboarding, auth/login)
   is shipped end-to-end (React → Bridge → blocks). Cohorts 1–5 + W#79 onboarding + the
   ADR-0099 auth chain all MERGED, most of it **today (2026-05-29)**.

3. **TWO genuine ERPNext-conversion gaps remain.** (a) The **ERPNext SHIM has NOT been
   deleted** — `sunfish/apps/web/src/api/erpnext.ts` is still live on main with 5 consumers,
   and `signal-bridge` still ships the entire `ERPNextProxy.cs` + `/api/v1/erpnext/*` route
   family. The "cohort-4 ERPNext-deletion strategy" (2026-05-25, 6-PR plan) was authored but
   NEVER executed — the cohort-4 that actually shipped was the audit-trail viewer. (b) The
   **full 6-pass data-migration importer is NOT built**; only two thin slices exist
   (`IErpnextJournalEntryImporter` in blocks-financial-ledger + `ErpnextSalesInvoiceImporter`
   in blocks-financial-ar). The importer is correctly demand-gated (no customer signal), not
   a launch blocker.

4. **4 of 5 reference bundles are Draft-manifest-only.** Property-Management is feature-
   complete in code but its manifest still says `"status": "Draft"` and still lists module
   keys (`sunfish.blocks.invoicing`, `.crm`, `.contacts`, etc.) that don't match the shipped
   package names. Asset-Management / Project-Management / Facility-Ops / Acquisition-
   Underwriting are manifest-Draft with NO dedicated UI bundle built — substrate exists for
   some (`blocks-assets`, `blocks-work-projects`, `blocks-businesscases`) but no cockpit.

5. **The real remaining MVP-launch work is hardening + 2 substrate gates, not features.**
   Launch substrate (ADR 0097 password-hashing + ADR 0099 session/login) MERGED today.
   What's left: rate-limiter forwarded-headers (#269), 5 OPEN FED launch-polish DRAFTs
   (correctly parked), packaging (#257 Tauri MSI), CIC-gated physical actions deferred to
   2026-06-09 (#235 Apple cert, #78 Surface Pro), and the WS-E tenant-portal/magic-link
   differentiator (partial — vendor adapter shipped, portal not).

**Counts:** Conversion-parity — **DONE 17, IN-FLIGHT 1, REMAINING 6** (across module rows).
Non-conversion backlog — **launch-hardening 5, CIC-gated 3, low-pri 6, parked-post-MVP 7.**

---

## SECTION 1 — ERPNext-conversion parity register

ERPNext is the legacy "prior architecture being replaced." Conversion has TWO meanings,
both covered here:
- **(A) Functional parity** — does Sunfish have a native block + UI replacing the ERPNext
  capability? (the bulk of the work; ~done)
- **(B) ERPNext-layer retirement** — has the ERPNext shim/proxy been DELETED, and is the
  one-time data-importer built? (the two genuine gaps)

State key: **DONE** = block(s) shipped + on `main` + (where user-facing) live route.
**IN-FLIGHT** = code authored/PR open/partial. **REMAINING** = not started or planned-only.

### 1A — Functional parity by ERPNext module/domain

| ERPNext module/domain | Sunfish block(s) (on disk) | Bundle | UI route(s) on main | State |
|---|---|---|---|---|
| Accounting / GL / Chart of Accounts / Journal | `blocks-financial-ledger` (72 .cs), `blocks-reports` (52) | Property-Mgmt | `/accounting`, `/reports/trial-balance` | **DONE** |
| Accounts Receivable / Sales Invoices | `blocks-financial-ar` (34 .cs) | Property-Mgmt | `/reports/ar-aging`, rent invoicing | **DONE** |
| Accounts Payable / Purchase Invoices (Bills) | `blocks-financial-ap` (31 .cs) | Property-Mgmt | (substrate done; `ApAgingService` exists) | **DONE** (substrate) — see AP-aging report tail in §2 |
| Payments / Payment Entry / reconciliation-of-payments | `blocks-financial-payments` (23 .cs) | Property-Mgmt | `/rent` (record payment) | **DONE** |
| Fiscal periods / period-close | `blocks-financial-periods` | Property-Mgmt | accounting close | **DONE** |
| Tax (sales-tax templates, jurisdictions) | `blocks-financial-tax` + `blocks-financial-tax-bridge`, `blocks-reports-tax` | Property-Mgmt | `/reports` (tax) | **DONE** |
| Recurring billing / subscriptions (rent schedules) | `blocks-recurring-billing` (25 .cs, renamed from blocks-rent-collection per ADR 0098 Step 2), `blocks-subscriptions` (25) | Property-Mgmt | `/rent` | **DONE** |
| CRM / Contacts / Customers / Suppliers (Parties) | `blocks-people-foundation` (36 .cs — Party/PartyRole/Customer/Vendor) | Property-Mgmt | `/vendors`, `/vendors/:id` | **DONE** (note: NO separate `blocks-crm`/`blocks-contacts` — capability lives in people-foundation; see drift #4) |
| Communications / messaging / Comments | `blocks-messaging` (7 .cs), `blocks-crew-comms` (25), `foundation-channels` | Property-Mgmt + crew-comms differentiator | `/comms` | **DONE** (page live); outbound vendor adapter + tenant portal = partial, see §1B + §2 P3 |
| Documents / DMS / attachments | `blocks-docs` (40 .cs) | Property-Mgmt | (substrate; not a named MVP route) | **DONE** (substrate) |
| Projects / Tasks / Milestones / Risk | `blocks-work-projects` (85 .cs), `blocks-tasks` (6), `blocks-work-orders` (53) | Property-Mgmt / Project-Mgmt | `/work-orders`, `/work-orders/:id` | **DONE** (work-orders live; project-mgmt cockpit = REMAINING bundle, §1C) |
| Maintenance / tickets | `blocks-maintenance` | Property-Mgmt | `/maintenance` | **DONE** |
| Property / Units / Vacancy (custom DocTypes) | `blocks-properties` (20 .cs), `blocks-public-listings` (35) | Property-Mgmt | `/properties`, `/:propertyId`, `/units`, `/vacancies` | **DONE** — cohort-5 (sunfish#82 + signal-bridge#54) MERGED 2026-05-29 |
| Leases (custom DocType) | `blocks-leases` (29 .cs), `blocks-property-leasing-pipeline` | Property-Mgmt | `/leases`, `/leases/:name` | **DONE** |
| Inspections | `blocks-inspections` (35 .cs) | Property-Mgmt | (iOS field app + web) | **DONE** |
| Reporting (Rent Roll, P&L, Trial Balance, AR Aging) | `blocks-reports` (52 .cs) | Property-Mgmt | `/reports/rent-roll`, `/profit-loss`, `/profit-and-loss-by-property`, `/trial-balance`, `/ar-aging` | **DONE** (cohort-3) — AP-aging report page = REMAINING tail, §2 |
| Audit trail / GL Entry-equivalent | `kernel-audit`, `blocks` audit emission + ADR 0094 IAuditEventReader | Property-Mgmt | `/audit-trail`, `/audit-trail/:id` | **DONE** (cohort-4) |
| Tenant onboarding / signup / verify-email | W#79 onboarding (signal-bridge + sunfish) | Property-Mgmt | `/auth/signup`, `/auth/verify-email`, `/auth/resend-verification` | **DONE** (W#79 closed 2026-05-28) |

### 1B — ERPNext-layer retirement (the genuine gaps)

| Item | Evidence on main | State |
|---|---|---|
| **ERPNext frontend shim deletion** (`apps/web/src/api/erpnext.ts`) | **STILL LIVE** on main with 5 production consumers: `useLeases.ts`, `RentCollectionPage.tsx`, `AccountingPage.tsx`, `PLReport.tsx` (legacy), `RentRoll.tsx` (legacy) + desktop hooks. The cohort-4 ERPNext-deletion-strategy doc (6-PR plan P1–P6) was authored 2026-05-25 but NEVER executed. | **REMAINING** |
| **ERPNext Bridge proxy deletion** (`signal-bridge/.../Proxy/ERPNextProxy.cs` + `/api/v1/erpnext/*` routes in `Program.cs`) | **STILL LIVE** — full proxy family present (`ERPNextProxy.cs`, `IERPNextClient.cs`, `ERPNextHttpClient.cs`, `ERPNextOptions.cs`). Dead routes once FED rebinds complete; present audit/security surface for no runtime benefit. | **REMAINING** |
| **Payment + Accounting Bridge rebind** (prerequisite to shim deletion) | `getPayments`/`recordPayment` + `getAccountingSummary`/`getAccountingOutstanding` still call `/api/v1/erpnext/*`; cohort-2 RB-8 rebind never landed. Legacy `RentRoll.tsx`/`PLReport.tsx` may still be mounted (route audit needed). | **REMAINING** (the load-bearing half of shim retirement) |
| **One-time ERPNext→Sunfish data importer** (6-pass, per importer-spec) | Only 2 thin slices exist: `IErpnextJournalEntryImporter` (ledger) + `ErpnextSalesInvoiceImporter` (AR). Full 6-pass importer (chart/parties/opening/transactional/reconcile/verify) + CLI (`anchor import erpnext`) + wizard NOT built. Correctly demand-gated (no customer signal); ONR scored it 5/12 for cohort candidacy. | **REMAINING** (demand-driven; NOT launch-blocking) |
| **Forward-watch importer canonicalization** (TenantId positional) | shipyard#101 MERGED; worktree `fix-erpnext-importer-tenant-positional` is the merged branch (not active in-flight work — it's a closed chore). | **DONE** |

### 1C — Reference bundles (the 5 in the roadmap §Bundle catalog)

All 5 manifests still carry `"status": "Draft"` (stale — see drift #3).

| Bundle | Manifest | Substrate built? | Cockpit/UI bundle built? | State |
|---|---|---|---|---|
| **Property Management** | Draft | YES (all required blocks) | YES (cohorts 1–5 React app) | **DONE** (MVP feature-complete; manifest marker is drift) |
| **Asset Management** | Draft | Partial (`blocks-assets`, `blocks-property-equipment`) | NO | **REMAINING** |
| **Project Management** | Draft | Partial (`blocks-work-projects` 85 .cs, `blocks-tasks`) | NO (work-orders surface only) | **REMAINING** |
| **Facility Operations** | Draft | Partial (`blocks-engine-room`, `blocks-scheduling`, `blocks-maintenance`) | NO | **REMAINING** |
| **Acquisition / Underwriting** | Draft | Partial (`blocks-businesscases` 18 .cs, Q6 tender bundle manifest) | Tender-side only (not Sunfish cockpit) | **REMAINING** |

### 1D — Conversion roll-up

- **DONE:** all 16 functional-parity domain rows in §1A + property-management bundle +
  the merged importer canonicalization = **17 conversion units DONE.**
- **IN-FLIGHT:** **1** — the WS-E tenant-comms differentiator is partial (Postmark vendor
  adapter MERGED today as `providers-postmark`/shipyard#176; tenant magic-link portal NOT
  built). Counted as in-flight because half shipped this session.
- **REMAINING:** **6** — (1) ERPNext frontend shim deletion, (2) ERPNext Bridge proxy
  deletion, (3) payment/accounting Bridge rebind, (4) full 6-pass data importer
  (demand-gated), (5–8 collapse to) the 4 non-PM reference bundles' UI cockpits
  (Asset / Project / Facility / Acquisition) — counted as a single "remaining bundle build"
  category here but enumerated individually in §1C.

---

## SECTION 2 — Non-conversion backlog register

### 2A — Launch-hardening (real, ready, MVP-relevant)

| # | Item | Repo | State | Notes |
|---|---|---|---|---|
| #269 | `UseForwardedHeaders` config for ALL rate-limiters | signal-bridge | **REMAINING** (open task) | sec-eng #56 A2 advisory; pre-launch. Small config PR. |
| #257 | `#30` anchor-tauri MSI packaging/hardening | sunfish (Tauri) | **REMAINING** (queued for po-win subagent) | FED-flagged as Windows-side, not frontend. |
| sunfish#28/#48/#47 | e2e (Playwright) + a11y (axe/jest-axe) | sunfish | **DONE** (#28/#48 MERGED; #47 a11y likely folded) — verify | Most launch-polish OPEN PRs the 2026-05-29 research listed are NOW MERGED. |
| sunfish#46/#45/#51 | perf (vendor chunks / React Compiler / bundle visualizer) | sunfish | **DONE/partial** (#85 vendor chunks MERGED) | Confirm remaining perf DRAFTs. |
| — | Live auth smoke-test (post ADR-0099 chain) | sunfish + signal-bridge | **REMAINING** | #86/#55/#56 login chain MERGED today; needs an end-to-end live smoke against real Bridge. |

(Note: the 2026-05-29 MVP-priority research listed ~7 OPEN launch-polish PRs to sweep; as of
this reconcile the sunfish OPEN set is down to 5 DRAFTs, all of which are the parked
post-MVP polish in §2D — the launch-hardening PRs largely MERGED. This is doc-drift #5.)

### 2B — CIC-gated, deferred to 2026-06-09 (do NOT pester before then)

| # | Item | State | Gate |
|---|---|---|---|
| #235 | Apple Developer ID Application cert (macOS signing) | **REMAINING (gated)** | CIC physical action; deferred per `[[cic-physical-gates-deferred-2026-06-09]]` |
| #78 | Surface Pro W#60 P3 offline-shell acceptance test | **REMAINING (gated)** | CIC physical action; deferred |
| W#60 P4 | Collaboration track (accountant peer + CPA read-only + tenant portal + bank CSV) | **REMAINING (gated)** | Gated on P3 PASS; tenant-portal slice overlaps WS-E §1D in-flight |

### 2C — Low-priority / close-out (real but not launch-blocking)

| Item | State | Notes |
|---|---|---|
| AP Aging report page + `ApAgingSummaryCartridge` | **REMAINING** | Mechanical mirror of shipped AR-aging; `ApAgingService` substrate exists; ~5-7h. Closes reports narrative. |
| Phase 3 namespace rename (#44) | **REMAINING (parked)** | Plan drafted; awaiting quiescence. |
| A2 corrigendum / pattern-catalog hygiene | **REMAINING (low)** | Carried in ONR pattern-catalog drift audits. |
| C6 promotion / standing-pattern ratifications | **REMAINING (low)** | Pattern lifecycle bookkeeping. |
| verify-email / magic-link / login establishers (residual) | **PARTIAL** | Core login MERGED (ADR 0099); magic-link tenant-portal establisher = WS-E gap. |
| Bridge.Data PM-leakage entity moves (ADR 0015 / 8-of-9 entities) | **REMAINING (low)** | bridge-data-audit (2026-04-19) move plan M1–M5 + ADR 0015 never landed; pre-1.0, internal-only, low urgency. |

### 2D — Parked post-MVP (correctly NOT MVP — leave parked)

| Item | State |
|---|---|
| FED DRAFT sunfish#31 (dark mode) | **PARKED** |
| FED DRAFT sunfish#32 (i18n react-i18next) | **PARKED** (only OPEN non-draft-ish; still DRAFT) |
| FED DRAFT sunfish#35 (WCAG 2.2 AA audit) | **PARKED** |
| FED DRAFT sunfish#37 (keyboard-shortcut system) | **PARKED** |
| FED DRAFT sunfish#38 (OpenFeature feature-flags) | **PARKED** |
| ADR 0098 Steps 3–7 (block renames: work-orders→work-items etc.) | **PARKED** — DEFERRED post-MVP per CIC 2026-05-29 (Step 2 rent-collection→recurring-billing DID land, shipyard#172) |
| ERPNext incremental-sync (Scope B, bidirectional) | **PARKED** — separate ~3-6mo workstream if customer demands |

---

## Open questions (for Admiral/CIC, not resolvable by reconcile alone)

1. **Is ERPNext shim/proxy deletion an MVP-launch item or post-MVP?** It is NOT functionally
   needed (the app works end-to-end without deleting it), but it leaves a live `/api/v1/erpnext/*`
   proxy surface in the Bridge that has audit/security implications at launch. Recommend
   Admiral decide: launch-hardening (do the cohort-4 deletion plan now) vs. post-MVP cleanup.
2. **Should the 4 non-PM bundles (Asset/Project/Facility/Acquisition) ship for MVP, or is
   MVP property-management-only?** The MVP done-condition is "CIC's property-management
   business runs on the app" — which is met. The other 4 bundles look post-MVP.
3. **Data-importer activation:** confirm it stays demand-gated (no build until a customer
   with ERPNext history signs up), per the cohort-candidate scoping.

---

## Doc-drift findings (canonical docs contradicting merged reality — refresh candidates)

**TOP 3 (highest-impact):**

1. **`shipyard/_shared/product/roadmap-tracker.md` (2026-04-19) is comprehensively stale.**
   It shows 0 `blocks-*` shipped (reality: 38), lists ADRs only through 0015-pending
   (reality: ADRs through 0099 Accepted), and frames P1–P6 + 20+ modules as "blocked/pending"
   (reality: PM vertical shipped end-to-end). **This is the doc CIC/Admiral are most likely
   to mis-read as current.** Recommend a full refresh or a prominent "SUPERSEDED — see
   active-workstreams + this register" banner.

2. **The cohort-4 ERPNext-deletion strategy was authored but never executed; "cohort-4"
   is double-defined.** `shipyard/_shared/design/cohort-4/02-erpnext-deletion-strategy.md`
   (2026-05-25) defines cohort-4 as "ERPNext-shim retirement (6 PRs P1–P6)." But the
   cohort-4 that actually shipped was the **audit-trail viewer** (sunfish#58/#59/#71 +
   ADR 0094). The shim (`erpnext.ts`) + Bridge proxy (`ERPNextProxy.cs`) are STILL LIVE on
   main. Either the deletion work is genuinely outstanding (likely) or the doc needs a
   "superseded / cohort renumber" note. This is both a doc-drift AND a real REMAINING
   conversion item (§1B).

3. **All 5 bundle manifests say `"status": "Draft"` and the property-management manifest
   lists module keys that don't match shipped packages.** PM is feature-complete in code
   but its manifest still requires `sunfish.blocks.invoicing`, `.billing`, `.crm`,
   `.contacts`, `.communications`, `.reporting`, `.procurement`, `.reconciliation`,
   `.diligence`, `.searchworkspace`, `.reservations`, `.vendors` — **none of which exist
   as packages on disk under those names.** The capabilities shipped under property-vertical
   names (financial-ar/ap/ledger/payments, recurring-billing, messaging/crew-comms, reports,
   people-foundation, work-projects). The manifest module-key vocabulary is aspirational and
   never reconciled to the shipped block names. Recommend: (a) flip PM manifest to
   "Active/Released" and (b) reconcile its module keys to the real package names (or document
   the key→package mapping).

**Additional drift (lower-impact, noted for completeness):**

4. **`MASTER-PLAN.md` + `active-workstreams.md` both last-updated 2026-05-19** (the
   active-workstreams "Current state" header literally still reads "last updated 2026-05-06
   — W#59 Crew Comms"). They pre-date cohorts 3/4/5 + ADRs 0095–0099 + the entire auth chain.
   Stale but less load-bearing than the roadmap-tracker.

5. **The 2026-05-29 MVP-priority research's "P4 launch-polish OPEN PRs to sweep" list is
   already partly stale** — most of those PRs (#28/#48/#85 etc.) MERGED in the same session.
   Only 5 DRAFTs remain OPEN (the §2D parked set). Minor; the research was correct at
   authoring time.

6. **The importer-spec (2026-05-18) references package names that never shipped** —
   `blocks-financial-chart`, `blocks-people-*` (plural cluster), `blocks-financial-budget`,
   `anchor import erpnext` CLI. The financial-chart capability folded into
   `blocks-financial-ledger`; people into `blocks-people-foundation` (singular). Spec is a
   valid design doc but its package-name table needs reconciliation before any importer build.

---

## Sources cited

1. `shipyard/_shared/product/roadmap-tracker.md` — 2026-04-19 [PRIMARY; STALE] (retrieved 2026-05-29)
2. `shipyard/icm/_state/MASTER-PLAN.md` + `active-workstreams.md` — last-updated 2026-05-19 [PRIMARY; stale] (retrieved 2026-05-29)
3. `shipyard/icm/01_discovery/research/erpnext-migration-cohort-candidate.md` — 2026-05-21 [PRIMARY/ONR] (retrieved 2026-05-29)
4. `shipyard/_shared/design/cohort-4/02-erpnext-deletion-strategy.md` — 2026-05-25 [PRIMARY/PAO] (retrieved 2026-05-29)
5. `shipyard/_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` — 2026-05-18 [PRIMARY/XO] (retrieved 2026-05-29)
6. `shipyard/_shared/engineering/bridge-data-audit.md` — 2026-04-19 [PRIMARY] (retrieved 2026-05-29)
7. `coordination/inbox/research-mvp-feature-priority-2026-05-29T0205Z.md` — 2026-05-29 [PRIMARY/Admiral-subagent; freshest] (retrieved 2026-05-29)
8. `ls shipyard/packages/` — 38 blocks-* on disk; ~104 packages total [PRIMARY/merged-reality] (retrieved 2026-05-29)
9. `shipyard/Shipyard.slnx` (+ legacy `Sunfish.slnx` co-present) [PRIMARY] (retrieved 2026-05-29)
10. `gh pr list` merged/open across shipyard + sunfish + signal-bridge [PRIMARY/merged-reality] (retrieved 2026-05-29):
    shipyard#172/#176/#177/#178/#179 (ADR 0098 Step 2, Postmark, ADR 0099, password-hashing);
    sunfish#82/#84/#85/#86 (cohort-5 + auth login); signal-bridge#54/#55/#56 (unit/vacancy + session + login)
11. `sunfish/apps/web/src/App.tsx` live route grep + `grep @/api/erpnext` consumers [PRIMARY] (retrieved 2026-05-29)
12. `signal-bridge/.../Proxy/ERPNext*.cs` + `Program.cs` route family [PRIMARY] (retrieved 2026-05-29)
13. `shipyard/packages/foundation-catalog/Manifests/Bundles/*.bundle.json` — all 5 "Draft" + PM module-key list [PRIMARY] (retrieved 2026-05-29)
14. `shipyard/icm/_state/handoffs/` — 130+ Stage-06 hand-offs (the conversion units) [PRIMARY] (retrieved 2026-05-29)

— ONR, 2026-05-29
