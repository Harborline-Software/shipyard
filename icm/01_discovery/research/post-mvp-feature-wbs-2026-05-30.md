# ONR research — Post-MVP feature WBS + Project-Management pilot detailed cut (2026-05-30)

**Requester:** CIC (via Admiral dispatch) — task #270 "Post-MVP WBS decomposition"
**Scope:** Authoritative, disk-verified WBS for the four remaining reference bundles
(asset-management, project-management, facility-operations, acquisition-underwriting) that
compose the horizontal `blocks-*` capability substrate. CIC chose "Pilot Project Management
first," so PM gets an implementation-ready cut (cockpit routes, Bridge endpoint family,
pattern-009 pairs, FED/Engineer split, PR decomposition) and the other three get catalog-level
WBS. In scope: manifest module-key → shipped-package reconcile; substrate DONE/PARTIAL/MISSING
per module; cockpit-UI gap; rough effort sizing; build sequence; net-new shared-block
enumeration; dependency graph; two CIC decisions framed. Out of scope: production code, ADR
authoring, the sequencing ruling itself (Admiral/CIC), and the ERPNext data-importer
(Workstream A — separate live program), the dynamic-forms keystone engine (separate ONR
deliverable in flight), and the WS-E tenant-comms differentiator.
**Status:** final
**Confidence:** HIGH on what is built / module-key reconcile / cockpit surface (all verified on
disk against shipped `blocks-*` and the live route table). MEDIUM on effort sizings (disk-based
ONR recalibration, NOT Engineer-validated) and on cross-bundle sequence (a recommendation for
Admiral, not a ruling).

> **Reconciliation note (load-bearing).** This deliverable was authored AFTER a same-day burst of
> Workstream-C activity that the two seed docs predate. Since the 2026-05-29 backlog register and
> the 0830Z post-MVP WBS (shipyard#181), the fleet has: (a) authored + Accepted **ADR 0101**
> (asset-mgmt substrate); (b) **MERGED C2.1** project-mgmt cockpit-readiness substrate
> (shipyard#192, 13:57Z) — so the PM substrate is now *demonstrably* cockpit-ready, not just
> "mostly built"; (c) opened **C1.1** asset-substrate (shipyard#193, DRAFT) building the greenfield
> asset domain RIGHT NOW; (d) issued an Admiral ruling (1345Z) putting **C1 + C2 both live in
> parallel**. This doc supersedes the older C-section calls where they conflict, and is consistent
> with the C-bundle substrate-readiness audit (shipyard#184, MERGED) which corrected three
> substrate facts. Where this doc and shipyard#181's Workstream-C disagree, **this doc wins** (it
> is later and disk-verified post-C2.1-merge).

---

## TL;DR

1. **PM substrate is implementation-ready TODAY — the only gap is the cockpit.** `blocks-work-projects`
   (93 .cs) ships a complete project domain: 8-state status machine with designated-authority
   transitions, milestones with predecessor edges + invoice-triggers, revision-aware budgets,
   event-projected actuals (from `JournalEntryPosted`), time-tracking (open/stop/submit + split
   approval), remodel projects/phases, and — as of shipyard#192 today — a Gantt-ready
   `IProjectTimelineReadModel`. **No new substrate is needed to start the PM cockpit.** The pilot is
   a Bridge-endpoint-family + React-cockpit build, not a domain build.

2. **The four bundles' manifest module-key vocabulary is ~80% aspirational and does NOT map 1:1 to
   shipped packages.** Of ~24 distinct module keys across the four manifests, only ~9 resolve to a
   real shipped package (workflow, tasks, scheduling, assets[stub-being-filled], maintenance,
   inspections, documents, accounting-cluster, reconciliation-cluster). The rest
   (`crm`, `contacts`, `diligence`, `reporting`, `invoicing`, `billing`, `vendors`, `procurement`,
   `reservations`, `searchworkspace`, `communications`, `logistics`, `tax-reporting`) are either
   net-new, or capability that lives under a *differently-named* package. The full reconcile table
   is in §1.0.

3. **Genuinely net-new SHARED blocks needed: `blocks-crm` (pipeline), `blocks-diligence`
   (data-room), and likely `blocks-procurement`.** `reservations` is NOT net-new — `blocks-scheduling`
   already ships `SlotReservation`/`ReservationOutcome`/`IScheduleReservationCoordinator`. `vendors`
   is NOT net-new — vendor capability lives in `blocks-people-foundation` (Party/Vendor). `contacts`/
   `reporting`/`invoicing`/`billing`/`accounting` all resolve to existing financial + people +
   reports packages. See §3b.

4. **Recommended cross-bundle build sequence: C2 (Project-Mgmt, pilot) → C3 (Facility-Ops) →
   C1 (Asset-Mgmt) → C4 (Acquisition/Underwriting).** PM first per CIC. C3 is the next-cheapest
   (richest reuse of shipped maintenance/inspections/scheduling; zero net-new shared blocks). C1 needs
   the greenfield asset domain to finish landing (in flight as shipyard#193) then a cockpit. C4 is
   last and heaviest — it needs two net-new shared blocks (`blocks-crm` + `blocks-diligence`) and
   carries the heaviest security surface (external data-room access). NOTE: the Admiral 1345Z ruling
   already put C1+C2 live in parallel; this sequence is the *cockpit-readiness* ordering, and is
   consistent with that (C1 substrate runs ahead, its cockpit lands after C2's).

5. **Two decisions for CIC, framed not resolved (§4):** (i) **ERPNext shim/proxy deletion** —
   `erpnext.ts` is live in BOTH web AND desktop apps (10+ consumers) and `ERPNextProxy.cs` +
   `/api/v1/erpnext/*` are live on the Bridge; treat the deletion as launch-hardening now, or
   post-MVP cleanup? (ii) **Do the four non-PM bundles ship in v1, or as a later release?** The MVP
   done-condition ("CIC's property-management business runs on the app") is met by PM-only; the four
   bundles are roadmap, and CIC has already greenlit building them in parallel — but "built" ≠ "in
   the v1 SKU." Frame the release-train question now so marketing/packaging is not surprised.

---

## SECTION 1 — Per-bundle WBS catalog

### 1.0 — Manifest module-key → shipped-package reconcile (the load-bearing table)

The five bundle manifests use a `sunfish.blocks.<key>` vocabulary that was designed *aspirationally*
in May and **never reconciled to the package names that actually shipped**. This table is the
canonical reconcile. State: **DONE** = shipped package exists + capability present; **PARTIAL** =
package exists but the named capability is thin / a stub being filled; **NET-NEW** = no package
provides this capability under any name.

| Manifest module key | Resolves to shipped package | State | Notes |
|---|---|---|---|
| `sunfish.blocks.workflow` | `blocks-workflow` (23 .cs) | **DONE** | Real engine: `IWorkflowRuntime`/`WorkflowDefinitionBuilder`/`WorkflowInstance`/`FrozenWorkflowDefinition`. Required by all 4. |
| `sunfish.blocks.forms` | `blocks-forms` (12 .cs) | **PARTIAL** | PRESENTATION wiring only (`FormBlockState`). The canonical **dynamic-forms ENGINE is NOT here** — it is a separate keystone scoped in a parallel ONR deliverable. Manifests require `forms`; assume the presentation layer is available, the engine is NOT. |
| `sunfish.blocks.tasks` | `blocks-tasks` (15 .cs) | **DONE** | Required by all 4. `tasks.subtasks`/`tasks.dependencies` featureDefaults map here (NOT to work-projects). |
| `sunfish.blocks.scheduling` | `blocks-scheduling` (19 .cs) | **DONE** | Ships `SlotReservation`+`ReservationOutcome`(SLOT_CONFLICT)+`IScheduleReservationCoordinator`. |
| `sunfish.blocks.assets` | `blocks-assets` (34 .cs) | **PARTIAL→DONE-soon** | Greenfield asset domain (Asset/AssetCategory/DepreciationSchedule/WarrantyTerm/LifecycleEvent) is being built RIGHT NOW in shipyard#193 (DRAFT). The pre-existing file-catalog `AssetRecord` is orthogonal and retained. Treat as DONE for sequencing once #193 merges. |
| `sunfish.blocks.maintenance` | `blocks-maintenance` (76 .cs) | **DONE** | Live `/maintenance` cockpit. Richest non-PM substrate. |
| `sunfish.blocks.inspections` | `blocks-inspections` (44 .cs) | **DONE** | Live (iOS field app + web). |
| `sunfish.blocks.projects` | `blocks-work-projects` (93 .cs) | **DONE** | The PM pilot substrate — see §1.2. Manifest key is `projects`; package is `work-projects`. |
| `sunfish.blocks.documents` | `blocks-docs` (46 .cs) | **DONE** | DMS / attachments substrate. |
| `sunfish.blocks.contacts` | `blocks-people-foundation` (36 .cs) | **DONE** | Party/PartyRole/Customer/Vendor. No separate `blocks-contacts`. |
| `sunfish.blocks.vendors` | `blocks-people-foundation` (Party/Vendor) | **DONE** | Vendor = a PartyRole. Live `/cockpit/vendors`. NOT a separate package. |
| `sunfish.blocks.communications` | `blocks-messaging` + `blocks-crew-comms` | **DONE** | Live `/comms`. (Outbound vendor adapter + tenant portal = WS-E gap, out of scope here.) |
| `sunfish.blocks.reporting` | `blocks-reports` (52 .cs) + `blocks-reports-tax` | **DONE** | Cartridge model; live `/reports/*`. |
| `sunfish.blocks.accounting` | `blocks-financial-ledger` (72 .cs) | **DONE** | GL/CoA/JE. Manifest key `accounting`; package is `financial-ledger`. |
| `sunfish.blocks.invoicing` | `blocks-financial-ar` (34 .cs) | **DONE** | Sales invoices / AR. |
| `sunfish.blocks.billing` | `blocks-recurring-billing` (25 .cs) + `blocks-subscriptions` | **DONE** | Renamed from `blocks-rent-collection` per ADR 0098 Step 2. |
| `sunfish.blocks.reconciliation` | `blocks-financial-payments` (23 .cs) | **DONE** | Payment entry / reconciliation. |
| `sunfish.blocks.tax-reporting` | `blocks-financial-tax` + `blocks-reports-tax` | **DONE** | Sales-tax templates + jurisdictions + tax reports. |
| `sunfish.blocks.reservations` | `blocks-scheduling` (SlotReservation) | **DONE (no new pkg)** | conflictDetection satisfiable today via scheduling. C3 extends with a bookable-resource model, not a new package (per shipyard#184 audit correction #3). |
| `sunfish.blocks.crm` | — | **NET-NEW** | Pipeline-stages capability (`crm.pipelineStages.default` in PM + Acq manifests). No package provides a deal/opportunity pipeline. Needed by C2-enterprise + C4. See §3b. |
| `sunfish.blocks.diligence` | — | **NET-NEW** | Due-diligence checklists + data-room + approval gates + evidence (`diligence.*`/`documents.dataRoom.*` in Acq manifest). NOT `blocks-businesscases` (that is the bundle-ENTITLEMENT engine — category error in older docs, corrected in shipyard#184). See §3b. |
| `sunfish.blocks.procurement` | — | **NET-NEW (likely)** | Purchase-requisition / PO flow. `blocks-financial-ap` covers Bills/AP, but PO-before-invoice requisition workflow is unbuilt. Enterprise-edition-only across all 4. See §3b. |
| `sunfish.blocks.searchworkspace` | — | **NET-NEW / cross-cutting** | A cross-bundle saved-search/workspace surface. Enterprise-only; defer — likely a foundation concern, not a per-bundle block. |
| `sunfish.blocks.logistics` | — | **NET-NEW** | Asset-transfer / movement (`logistics.transfers.enabled: false` default — already opt-OUT). Asset-mgmt-enterprise-only; defer (default-off). |

**Reconcile headline:** the four bundles share required-modules **workflow + forms + tasks + scheduling**
(facility-ops + asset-mgmt also require **maintenance + inspections + assets**). All the *required*
modules resolve to shipped packages (forms with the engine caveat). The net-new work is concentrated
in **optional / enterprise-edition** capability — `crm`, `diligence`, `procurement` — which is why
the lite/standard editions of all four bundles are buildable today and only the enterprise editions
need net-new shared blocks.

---

### 1.1 — C1 · Asset Management

**Manifest:** `asset-management.bundle.json` · status Draft · category Operations.
**Required modules:** workflow, forms, tasks, scheduling, **assets, maintenance, inspections**.
**Optional/enterprise adds:** contacts, communications, documents, diligence, reporting, projects,
vendors, procurement, invoicing/accounting/billing/reconciliation, tax-reporting, logistics.

| Module | Package | Substrate | Notes |
|---|---|---|---|
| assets | blocks-assets | **PARTIAL→DONE** | Greenfield domain in flight (shipyard#193, ADR 0101). Depreciation/warranty/lifecycle. |
| maintenance | blocks-maintenance | **DONE** | Reuse from PM vertical. |
| inspections | blocks-inspections | **DONE** | Reuse. |
| workflow/forms/tasks/scheduling | (shared) | **DONE** (forms=presentation) | |
| logistics | — | **NET-NEW** | default-OFF; defer. |

**Cockpit-UI gap:** NO asset cockpit exists. Needs: asset register (`/cockpit/assets`), asset detail
(lifecycle timeline + depreciation schedule + warranty + linked work-orders/inspections), warranty-
expiry watchlist. Depreciation auto-calc is opt-in (default off).
**Effort (MEDIUM confidence):** substrate finish ~in flight; Bridge `/api/v1/assets` family ~M;
cockpit ~L (register + detail + depreciation/warranty panels). **~3-4 PRs after #193 merges.**
**ADR:** ADR 0101 already Accepted — covers C1.1→C1.4 ladder. No new ADR.

### 1.2 — C2 · Project Management  *(PILOT — see §2 for the implementation-ready cut)*

**Manifest:** `project-management.bundle.json` · status Draft · category Operations.
**Required modules:** workflow, forms, tasks, scheduling.
**Optional/enterprise adds:** projects, contacts, crm, communications, documents, diligence,
reporting, searchworkspace, invoicing, billing, accounting, reconciliation, vendors, procurement,
reservations.

| Module | Package | Substrate | Notes |
|---|---|---|---|
| projects | blocks-work-projects (93 .cs) | **DONE** | Full domain incl. timeline read model (shipyard#192). See §2. |
| tasks/scheduling/workflow | (shared) | **DONE** | scheduling provides resource-booking + reservation. |
| forms | blocks-forms | **PARTIAL** | presentation only; PM pilot does NOT depend on the dynamic-forms engine (see §2 dependency note). |
| crm | — | **NET-NEW** | enterprise-only pipeline; C2 lite/standard does not need it. |
| reporting/invoicing/billing/accounting | (financial cluster) | **DONE** | enterprise-edition composition. |

**Cockpit-UI gap:** NO project cockpit. This is the pilot — full spec in §2.
**Effort (MEDIUM):** Bridge `/api/v1/projects` family ~M; cockpit ~L. **~5-6 PRs.** See §2.6.
**ADR:** none needed for lite/standard (substrate is built + ADR-backed). CRM (enterprise) needs the
shared `blocks-crm` ADR (§3b) but that is NOT on the PM-pilot critical path.

### 1.3 — C3 · Facility Operations

**Manifest:** `facility-operations.bundle.json` · status Draft · category Operations.
**Required modules:** workflow, forms, tasks, scheduling, **maintenance, inspections, assets**.
**Optional/enterprise adds:** contacts, communications, documents, diligence, reporting,
searchworkspace, vendors, procurement, reservations, invoicing/billing/accounting/reconciliation.

| Module | Package | Substrate | Notes |
|---|---|---|---|
| maintenance | blocks-maintenance (76 .cs) | **DONE** | Multi-channel intake, SLA tracking, vendor quotes — all in manifest featureDefaults, all in substrate. |
| inspections | blocks-inspections (44 .cs) | **DONE** | recurring inspections (`inspections.recurring.enabled`) supported. |
| scheduling/reservations | blocks-scheduling | **DONE** | `reservations.conflictDetection` satisfiable today; bookable-resource model is a thin extend. |
| assets | blocks-assets | **PARTIAL→DONE** | shares C1's in-flight domain. |
| vendors | blocks-people-foundation | **DONE** | vendor coordination = Party/Vendor. `vendors.performanceScoring` default-OFF. |

**Cockpit-UI gap:** NO facility cockpit, BUT the maintenance + work-orders + vendors cockpits already
exist under `/cockpit`. C3 is largely a **re-composition + a space/reservation surface**, not a from-
scratch build. Lowest net-new of the four.
**Effort (MEDIUM):** C3.1 extend scheduling with bookable-resource model ~S-M; Bridge `/api/v1/facilities`
or reuse ~M; cockpit (work-order intake board + reservation calendar + inspection schedule) ~M-L.
**~4 PRs.** Reuses the most shipped substrate of the four → **dispatch second after the PM pilot.**
**ADR:** the C3.1 reservation-model-home question is a **one-paragraph DECISION** (extend scheduling
vs new `blocks-reservations`; ONR recommends extend), NOT a dual-council ADR (shipyard#184 correction).

### 1.4 — C4 · Acquisition / Underwriting

**Manifest:** `acquisition-underwriting.bundle.json` · status Draft · category **Diligence**.
**Required modules:** workflow, forms, tasks. (Lighter required set — but heaviest optional.)
**Optional/enterprise adds:** contacts, **crm, diligence**, communications, documents, reporting,
searchworkspace, scheduling, accounting/billing/invoicing/reconciliation.
**Deployment:** SelfHosted + HostedSaaS ONLY — **lite intentionally NOT supported** (data-room +
audit-trail requirements exceed a local-first deployment, per manifest complianceNotes).

| Module | Package | Substrate | Notes |
|---|---|---|---|
| crm | — | **NET-NEW** | deal-flow pipeline (lead→term-sheet→diligence→approval→closed). |
| diligence | — | **NET-NEW** | checklists + evidence + approval gates + data-room. NOT businesscases. |
| documents/data-room | blocks-docs + NET-NEW access layer | **PARTIAL** | DMS exists; the **external-access + watermarking + access audit-trail** data-room layer is net-new and is the heaviest security surface in WS-C. |
| workflow/forms/tasks | (shared) | **DONE** | approval workflows compose on blocks-workflow. |

**Cockpit-UI gap:** NO acquisition cockpit. Needs deal pipeline board, diligence-checklist workspace,
data-room (with external time-bound invites), approval-gate UI. Highest net-new + highest security.
**Effort (LOW-MEDIUM confidence — most uncertain of the four):** two net-new shared blocks (`crm`,
`diligence`) + a data-room access layer + cockpit. **~8-12 PRs across substrate + Bridge + cockpit.**
**ADR:** **dual-council ADR MANDATORY** (data-room external-access + watermarking + access audit =
heaviest security surface in WS-C — confirmed in shipyard#184). **Dispatch LAST.**

### 1.5 — Recommended cross-bundle build sequence

```
C2 (Project-Mgmt, PILOT)  →  C3 (Facility-Ops)  →  C1 (Asset-Mgmt cockpit)  →  C4 (Acq/Underwriting)
   substrate DONE             richest reuse          substrate in flight        2 net-new blocks +
   cockpit only               ~0 net-new             (#193) then cockpit        data-room (security)
```

Rationale: **C2 first** (CIC directive — and substrate is already cockpit-ready). **C3 second**
(cheapest: reuses shipped maintenance/inspections/scheduling, zero net-new shared blocks). **C1 third**
(its greenfield asset domain is landing as shipyard#193; cockpit follows once substrate merges).
**C4 last** (two net-new shared blocks + heaviest security). This is consistent with the Admiral
1345Z ruling that put **C1 substrate + C2 both live in parallel** — C1's substrate runs ahead in its
own lane while C2's cockpit is the pilot; the *cockpit* ordering puts C2's first.

---

## SECTION 2 — Project Management PILOT (implementation-ready)

CIC chose to pilot PM first. The substrate is built and, as of shipyard#192 (MERGED 13:57Z today),
cockpit-ready. **This section is the precursor to a Stage-05 hand-off**, not the hand-off itself.

### 2.0 — Substrate inventory (verified on disk, `blocks-work-projects`, 93 .cs)

| Capability | Surface (on disk) | Cockpit-relevant |
|---|---|---|
| Project lifecycle | `Project` (8-state) + `ProjectStatusMachine` (Draft→Planned→InProgress→{OnHold,Blocked,Completed}→Closed/Cancelled) + `IProjectService` (designated-authority transitions via `OwnerPartyId`, `NotProjectOwnerException`) | list + detail + status actions |
| Project read | `IProjectReadModel` (GetById/GetSummary/GetMilestones), `ProjectSummary` | list view, cross-cluster |
| Timeline / Gantt | `IProjectTimelineReadModel` → `ProjectTimeline` + `ProjectTimelineMilestone` (planned/actual span, %-complete, ordered bars, `PredecessorMilestoneId` edges) — **shipyard#192, wire-ready JSON** | Gantt view |
| Milestones | `ProjectMilestone` (kind, weight, paymentAmount, `triggersInvoice`, predecessor edge), `AddMilestoneAsync`/`AchieveMilestoneAsync` | milestones panel |
| Budget | `ProjectBudget` (revision-aware) + `ProjectBudgetLine` + `BudgetCategory`, `IProjectBudgetRepository` (InsertRevisionAsync, GetCurrent/GetRevisions, `OverlappingBudgetRevisionException`) | budget panel |
| Actuals | `ProjectActual` + `IProjectActualProjector` (projected from `JournalEntryPosted` events via `JournalEntryPostedHandler`) | budget-vs-actual |
| Time tracking | `TimeEntry`/`TimeLog` + `ITimeEntryService` (open/stop/submit, billable, hourly-rate-at-stop) + `ITimeApprovalService` (split approve/reject authority) | time-tracking panel |
| Remodel | `RemodelProject`/`RemodelPhase`/`PhaseStatus` + `IRemodelProjectService` (phase completion + capitalization events) | remodel sub-surface (defer to enterprise) |
| Events | ProjectCreated/StatusChanged, MilestoneCreated/Achieved/InvoiceTriggered, TimeEntrySubmitted/Approved, RemodelPhaseCompleted/Capitalized | cross-cluster (already wired) |
| Importer | `IErpnextProjectImporter` (tag-based externalRef) | n/a (Workstream A) |

**Verdict: the cockpit needs ZERO new substrate for lite/standard editions.** Every panel the manifest
implies (Gantt, budget-tracking, milestones, time, task-deps) has a backing read/write surface on disk.

### 2.1 — React cockpit surface (routes + pages)

The frontend uses a `/cockpit` layout with nested routes (work-orders, vendors, property detail already
live there). Projects slot in identically:

| Route | Page | Surfaces | pattern-009? |
|---|---|---|---|
| `/cockpit/projects` | `ProjectListView` | `IProjectReadModel` summaries — filter by status/kind/priority; create | **NEW route → YES** |
| `/cockpit/projects/:projectId` | `ProjectDetailView` | header (status machine actions, owner, dates, %-complete) + tabbed panels below | **NEW route → YES** |
| (tab) Gantt | `ProjectGanttPanel` | `IProjectTimelineReadModel` → reuse `MaintenanceWorkOrderTimeline.tsx` (the Gantt MVP from sunfish#22, MERGED) | within detail |
| (tab) Budget | `ProjectBudgetPanel` | `ProjectBudget` revisions + lines + budget-vs-actual rollup | within detail |
| (tab) Milestones | `ProjectMilestonesPanel` | milestone list + add/achieve; predecessor edges | within detail |
| (tab) Time | `ProjectTimePanel` | `TimeEntry` list + open/stop/submit; approval queue (role-gated) | within detail |

**Route note:** put projects under `/cockpit/projects` (matching work-orders/vendors), NOT a top-level
`/projects` — consistency with the established cockpit layout and the single-writer frontend-lane
discipline (`app.tsx`/router is a reserved single-writer lane; broke main 2026-05-28). The Gantt panel
**reuses** the existing `MaintenanceWorkOrderTimeline` component — do not author a new Gantt engine.

### 2.2 — Bridge (signal-bridge) endpoint family

Follow the established `MapGroup("/api/v1/<family>")` + `RequireAuthorization(AuthenticatedTenantPolicy)`
+ in-handler tenant-scope-via-`ITenantContext` pattern (template: `UnitsEndpointsExtensions.cs`,
`LeasesEndpointsExtensions.cs`). New family `ProjectsEndpointsExtensions` registering `/api/v1/projects`:

| Method + route | Handler | Backing read/write | Read/Write |
|---|---|---|---|
| `GET /api/v1/projects` | ListProjects | `IProjectReadModel.GetSummary` (list) | R |
| `GET /api/v1/projects/{id}` | GetProjectDetail | `IProjectReadModel.GetById` | R |
| `GET /api/v1/projects/{id}/timeline` | GetProjectTimeline | `IProjectTimelineReadModel.GetTimelineAsync` (wire-ready JSON) | R |
| `GET /api/v1/projects/{id}/budget` | GetProjectBudget | `IProjectBudgetRepository.GetCurrent/GetRevisions` | R |
| `GET /api/v1/projects/{id}/time` | ListTimeEntries | `ITimeEntryService.GetById` + a list reader | R |
| `POST /api/v1/projects` | CreateProject | `IProjectService.CreateAsync` | **W** |
| `POST /api/v1/projects/{id}/transition` | TransitionStatus | `IProjectService.TransitionStatusAsync` (designated-authority — caller principal must == OwnerPartyId) | **W** |
| `POST /api/v1/projects/{id}/milestones` | AddMilestone | `IProjectService.AddMilestoneAsync` | **W** |
| `POST /api/v1/projects/{id}/milestones/{mid}/achieve` | AchieveMilestone | `IProjectService.AchieveMilestoneAsync` | **W** |
| `POST /api/v1/projects/{id}/budget` | InsertBudgetRevision | `IProjectBudgetRepository.InsertRevisionAsync` | **W** |
| `POST /api/v1/projects/{id}/time` (open/stop/submit) | Time lifecycle | `ITimeEntryService` | **W** |

**Authorization caveat (load-bearing for sec-eng SPOT-CHECK):** `IProjectService.TransitionStatusAsync`
enforces **Pattern A (designated authority)** — the `actingPartyId` must equal `Project.OwnerPartyId`,
and the *Bridge handler* is responsible for verifying the session principal matches `actingPartyId`
before calling the service (the service does NOT consult `IUserContext`). Likewise time-entry rate-
setting authority must be gated at the Bridge to a role distinct from the worker. These are the two
authorization seams the security council must inspect.

### 2.3 — pattern-009 (Bridge-endpoint + frontend-rebind PAIR) implications

Per fleet convention, pattern-009 is the "new Bridge endpoint + frontend rebind" PAIR, and **NEW routes
require a security-engineering SPOT-CHECK on PR-open** (per the SPOT-CHECK SLA — Admiral dispatches
sec-eng within 30 min of the DRAFT-open status beacon). For the PM pilot:

- The **two NEW frontend routes** (`/cockpit/projects`, `/cockpit/projects/:projectId`) trigger
  pattern-009 → **sec-eng SPOT-CHECK MANDATORY** on the cockpit PR(s).
- The **Bridge `/api/v1/projects` family** is the Bridge half of the pair → **sec-eng tenant-scope
  review MANDATORY** (this is the canonical pattern-009 security surface: tenant-scoping +
  designated-authority transition gating + time-rate authority split).
- Per the memory note `[[pattern009_scope]]`: pattern-009 triggers on **NEW routes**, not on adding a
  new case to an existing dispatcher. The project routes are net-new → triggers.
- Recommendation: claim the Bridge half and the frontend half as **separate PRs in the same pair** so
  the security council can review the tenant-scope seam (Bridge) and the route-mount seam (frontend)
  distinctly — the frontend PR must NOT merge ahead of its Bridge half (rebind-to-nothing risk).

### 2.4 — FED vs Engineer split

| Unit | Owner | Layer | Why |
|---|---|---|---|
| C2.2 Bridge `/api/v1/projects` family | **Engineer** | signal-bridge (C#) | endpoint handlers + tenant-scope + authorization seams; sec-eng council |
| C2.3 React cockpit (list + detail + tabs) | **FED** | sunfish web (TSX) | pages, hooks (`useProjects`), api client (`api/projects.ts`), route mount |
| Gantt panel | **FED** | sunfish web | reuse `MaintenanceWorkOrderTimeline.tsx` (already FED-owned) |
| C2.5 bundle manifest Draft→Active flip + module-key reconcile | **Engineer** (or ONR-spec'd, Engineer-executed) | shipyard foundation-catalog | see §3a |

Note: C2.1 (substrate) is already DONE (Engineer, shipyard#192). The remaining PM-pilot units are
C2.2 (Engineer/Bridge) → C2.3 (FED/cockpit) → C2.5 (manifest). C2.4 (CRM-enterprise) is OUT of the
pilot critical path (needs the net-new `blocks-crm` shared block).

### 2.5 — Dependency on the dynamic-forms keystone

**The PM pilot is INDEPENDENT of the dynamic-forms ENGINE.** The PM cockpit uses fixed, typed forms
(create-project, add-milestone, budget-revision, time-entry) wired directly to the `IProjectService` /
`IProjectBudgetRepository` / `ITimeEntryService` contracts — it does NOT render user-defined dynamic
forms. The `blocks-forms` package (presentation wiring, `FormBlockState`) is available; the canonical
dynamic-forms engine (separate ONR keystone deliverable) is NOT, and the PM pilot does not need it.
The manifest lists `forms` as a required module for *bundle-activation completeness* (so the bundle
can later expose dynamic intake forms), but the **cockpit build does not block on it.** Sequence the
PM pilot now; fold dynamic-forms-backed intake later when the engine lands.

### 2.6 — PR decomposition (the Stage-05 precursor)

| PR | Title (indicative) | Owner | Size | Council | Dep |
|---|---|---|---|---|---|
| C2.2a | `feat(bridge): /api/v1/projects read endpoints (list/detail/timeline/budget/time)` | Engineer | M | sec-eng (tenant-scope) | C2.1 ✓ |
| C2.2b | `feat(bridge): /api/v1/projects write endpoints (create/transition/milestones/budget/time)` | Engineer | M | **sec-eng MANDATORY** (designated-authority + rate-authority seams) | C2.2a |
| C2.3a | `feat(web): projects cockpit — list + detail shell + status actions` | FED | M | **pattern-009 sec-eng** (NEW routes) | C2.2a |
| C2.3b | `feat(web): project detail panels — Gantt (reuse) + milestones + budget-vs-actual` | FED | M | (2nd-instance pattern — light) | C2.3a |
| C2.3c | `feat(web): project time-tracking panel + approval queue` | FED | S-M | (role-gated UI — sec-eng note) | C2.3a |
| C2.5 | `chore(catalog): project-management bundle Draft→Active + module-key reconcile` | Engineer | S | — | C2.3 |

**Critical-path note:** C2.3a (frontend, NEW routes) must NOT merge ahead of C2.2a (its Bridge read
half) — rebind-to-nothing risk. The frontend single-writer-lane discipline (`app.tsx`/router) applies:
serialize the cockpit PRs through the frontend lane; do not bulk-arm auto-merge on PRs touching the
router (`[[rebase-sweep-shared-file-risk]]`).

---

## SECTION 3 — Cross-cutting

### 3a — Manifest Draft→Active flip + module-key reconcile

All five manifests carry `"status": "Draft"` + `"maturity": "Scaffold"`. Two distinct work items:

1. **Property-Management manifest is stale** — PM is feature-complete in code but its manifest still
   says Draft and still lists aspirational module keys (`invoicing`, `crm`, `contacts`, `reporting`,
   `reconciliation`, `diligence`, `reservations`, `vendors`) that don't match shipped package names.
   **Flip PM → Active/Released + reconcile its keys** (or add a documented key→package mapping). This
   is a quick chore (~S) and should land independent of the C-bundle work — it corrects the canonical
   catalog that other tooling reads.
2. **Per-bundle flip on cockpit completion** — each of C1-C4 flips Draft→Active as its cockpit lands.
   Bundle the flip into the bundle's final cockpit PR (C2.5 above is the PM-bundle pattern).

**Module-key reconcile (catalog-wide):** the §1.0 table IS the reconcile. Recommend committing a
`Manifests/module-key-mapping.md` (or a `resolvedPackage` field on each module entry) so the
aspirational-vs-shipped gap is documented once, not re-discovered per bundle. This is a doc/catalog
chore, not a code change — route to QM or fold into C2.5.

### 3b — Net-new SHARED blocks (referenced by manifests, not shipped)

| Candidate block | Referenced by | Capability | Rough size | ADR? | Recommendation |
|---|---|---|---|---|---|
| **`blocks-crm`** | C2 (ent), C4 (req) | Deal/opportunity pipeline, configurable stages (`crm.pipelineStages.default`), lead intake | **M-L** | YES (shared substrate) | **Build ONCE as shared substrate** — both PM-enterprise + Acquisition consume it. Don't fork two copies. Build when C4 is greenlit (C2 lite/standard doesn't need it). |
| **`blocks-diligence`** | C4 (req), asset/facility/PM (opt) | Due-diligence checklists, evidence-required gates, approval gates (`diligence.evidenceRequired`, `diligence.approvalGates`) | **L** | YES (dual-council) | **New package** — NOT `blocks-businesscases` (entitlement engine; category error corrected in shipyard#184). Heaviest of the net-new. C4-only initially. |
| **Data-room access layer** | C4 | External time-bound access + watermarking + access audit-trail (`documents.dataRoom.*`, `documents.externalAccess.auditTrail`) | **M-L** | YES (security-critical) | Extends `blocks-docs`; the external-access + audit surface is the **heaviest security work in WS-C**. Dual-council. C4-only. |
| **`blocks-procurement`** | all 4 (ent) | Purchase-requisition → PO → receipt (before the AP Bill that `blocks-financial-ap` already handles) | **M** | DECISION (new vs extend AP) | Enterprise-only across all four; default-deferred. Likely a thin new package over the AP cluster. Defer until a bundle's enterprise edition is actually scoped. |
| `blocks-reservations` | C3 (impl detail) | bookable-resource model | **S (extend, not new)** | DECISION (one-paragraph) | **DO NOT create** a new package — extend `blocks-scheduling` (already ships SlotReservation). Per shipyard#184. |
| `blocks-logistics` | C1 (ent) | asset transfers | — | — | default-OFF; defer indefinitely. |
| `blocks-searchworkspace` | all (ent) | cross-bundle saved-search | — | — | likely a foundation concern, not a per-bundle block; defer. |

**Headline:** the genuinely net-new shared-block program is **`blocks-crm` + `blocks-diligence` +
data-room layer (+ maybe `blocks-procurement`)** — and ALL of it is concentrated in C4
(Acquisition/Underwriting) plus enterprise-editions. **The lite/standard editions of C1/C2/C3 need
zero net-new shared blocks.** This is the single most important sizing fact: three of the four bundles'
shippable editions are cockpit-only builds on existing substrate.

### 3c — Dependency graph (where the dynamic-forms keystone sits)

```
        SHARED SUBSTRATE (shipped)                         NET-NEW SHARED                KEYSTONE
  workflow · tasks · scheduling · maintenance         blocks-crm ──┐              dynamic-forms ENGINE
  inspections · docs · people-foundation              blocks-diligence │           (separate ONR
  financial-cluster · reports · work-projects         data-room layer  │            deliverable)
  assets[#193] · forms(presentation)                  blocks-procurement│                 │
         │           │            │           │              │         │                 │ (optional —
         ▼           ▼            ▼           ▼              ▼         ▼                 ▼  per-bundle
   ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌─────────────────────────────┐         dynamic intake
   │ C2 PM    │ │ C3 FacOps│ │ C1 Asset │ │ C4 Acquisition/Underwriting  │         forms, NOT a
   │ (PILOT)  │ │          │ │          │ │ (needs crm + diligence +      │         cockpit blocker)
   │ cockpit  │ │ cockpit  │ │ cockpit  │ │  data-room — heaviest)        │
   └──────────┘ └──────────┘ └──────────┘ └─────────────────────────────┘
   substrate    ~0 net-new    substrate     2 net-new blocks + data-room
   DONE today   reuse-heavy    in flight     + dual-council ADR

  Unblock order:  C2  →  C3  →  C1(cockpit)  →  C4
```

**Key dependency facts:**
- **C2, C3, C1(lite/standard)** depend ONLY on shipped substrate → unblocked today (C1's asset domain
  is the only in-flight piece, shipyard#193).
- **C4** is gated on TWO net-new shared blocks + a data-room access layer + a dual-council ADR → last.
- **The dynamic-forms ENGINE gates none of the four cockpits.** It is an *optional* enrichment (user-
  defined intake forms) that each bundle can adopt later. It is NOT on any cockpit's critical path.
- **`blocks-crm` is the one cross-bundle dependency** (C2-enterprise + C4 both need it) → build it
  once, as shared substrate, when C4 is greenlit; do not fork.

---

## SECTION 4 — Two decisions framed for CIC (not resolved)

### Decision (i) — ERPNext shim/proxy deletion: launch-hardening now vs post-MVP cleanup

**Facts (verified on disk, origin/main @ 87b7266):**
- `erpnext.ts` is **live in BOTH apps**: `sunfish/apps/web/src/api/erpnext.ts` AND
  `sunfish/apps/desktop/src/api/erpnext.ts` (the desktop copy was NOT noted in the prior register —
  it is a second live consumer surface).
- Web consumers: `useLeases.ts`, `RentRoll.tsx`, `RentCollectionPage.tsx`, `PLReport.tsx`,
  `MaintenancePage.tsx`, `LeasesPage.tsx`, `LeaseDetailPage` (+ their tests). ~7 production consumers.
- `signal-bridge/Sunfish.Bridge/Proxy/ERPNextProxy.cs` is **live on main** + the `/api/v1/erpnext/*`
  route family is registered. (Workstream B in the 0830Z WBS scopes this deletion; B was tagged
  `[FUTURE — deferred]`.)

**The frame for CIC:** The app works end-to-end WITHOUT deleting the shim — it is not functionally
blocking. BUT it leaves a live `/api/v1/erpnext/*` proxy surface on the Bridge at launch, which is an
audit/security surface for no runtime benefit (a path that proxies to an external ERPNext the launch
product does not use). The choice is: **(A)** do the Workstream-B deletion now as launch-hardening
(rebind the ~7 web consumers + desktop copy to native Bridge endpoints, then delete the proxy — a
~6-PR effort, the load-bearing half is the payment/accounting rebind), or **(B)** ship v1 with the
shim live and delete post-MVP. ONR does not recommend a side — but flags that the desktop copy
**doubles the consumer surface** vs the register's estimate, so "B" carries a slightly larger residual
surface than previously framed.

### Decision (ii) — Do the four non-PM bundles ship in v1, or as a later release?

**Facts:** The MVP done-condition ("CIC's property-management business runs on the app") is **met** by
the Property-Management bundle alone. CIC has already greenlit *building* C1+C2 in parallel (Admiral
1345Z ruling) and pilot-PM-first (this task). But "built and on main" is not the same as "in the v1
product SKU / launch marketing / pricing tiers."

**The frame for CIC:** Three sub-questions, each a release-train decision:
- **Scope of v1 SKU:** does v1 ship as Property-Management-only (the four bundles land on main but are
  feature-flagged off / not marketed), or does v1's launch include whichever of C2/C3 are cockpit-
  complete by launch date?
- **Pricing/edition story:** the bundles each define lite/standard/enterprise editions — does v1 expose
  the edition tiers, or is that a 1.x concern?
- **Marketing surface:** PAO launch positioning (shipyard#195, the marketing-copy DRAFT) should know
  whether to position Sunfish as a property-management product or a multi-vertical platform at launch.

ONR's read (not a recommendation): the cleanest story is **v1 = Property-Management product; the four
bundles are a post-v1 "verticals" release train** — it lets launch marketing be crisp and lets the
bundle cockpits mature behind a flag. But this is a CIC product/positioning call, not a technical one.
Framing it now prevents a late surprise to PAO + packaging.

---

## Open questions (for Admiral / CIC)

1. **ERPNext shim deletion lane (Decision i):** launch-hardening-now or post-MVP? (Affects whether
   Workstream B promotes from `[FUTURE]` to active.) The desktop-copy discovery raises the residual-
   surface cost of deferring.
2. **v1 SKU scope (Decision ii):** is v1 PM-only, or does it include cockpit-complete C2/C3 bundles?
   PAO (shipyard#195) + packaging need this to avoid a late pivot.
3. **`blocks-crm` build trigger:** build the shared CRM substrate now (it's the one cross-bundle
   dependency, needed by C2-enterprise + C4), or defer until C4 is greenlit? Building it early
   unblocks PM-enterprise and de-risks C4; building it late avoids speculative substrate.
4. **C3 reservation-model home:** confirm the ONR recommendation (extend `blocks-scheduling`, not a new
   `blocks-reservations`) before C3 dispatch — a one-paragraph DECISION, not a dual-council ADR.
5. **Manifest reconcile artifact:** commit a `module-key-mapping.md` / `resolvedPackage` field to the
   catalog (route to QM or fold into C2.5)? The §1.0 table is the content; the question is where it
   lives canonically.

---

## Sources cited

1. `shipyard/packages/foundation-catalog/Manifests/Bundles/{project,asset,facility,acquisition,property}-management.bundle.json` — 2026-05-18 [PRIMARY] (retrieved 2026-05-29)
2. `shipyard/packages/blocks-work-projects/**` — `Project.cs`, `ProjectStatusMachine`, `IProjectService`, `IProjectReadModel`, `IProjectTimelineReadModel`, `IProjectBudgetRepository`, `ITimeEntryService`, `WorkProjectsServiceCollectionExtensions.cs` (93 .cs total) [PRIMARY/disk] (retrieved 2026-05-29)
3. `shipyard/packages/` — full `blocks-*` directory listing + per-package .cs counts (38 blocks-* on disk) [PRIMARY/disk] (retrieved 2026-05-29)
4. `signal-bridge/Sunfish.Bridge/Units/UnitsEndpointsExtensions.cs` + route-group grep across signal-bridge (`/api/v1/{units,leases,properties,reports,cockpit,audit-events,identity}`) [PRIMARY/disk] (retrieved 2026-05-29) — pattern-009 PAIR + endpoint-family template
5. `sunfish/apps/web/src/App.tsx` route table (`/cockpit` nested layout) + `api/` + `hooks/` listings + `MaintenanceWorkOrderTimeline.tsx` (Gantt MVP) [PRIMARY/disk] (retrieved 2026-05-29)
6. `coordination/inbox/onr-status-2026-05-29T1320Z-c-bundle-audit.md` (shipyard#184, MERGED) — three substrate corrections [PRIMARY/ONR] (retrieved 2026-05-29)
7. `coordination/inbox/onr-status-2026-05-29T0830Z-post-mvp-wbs-delivered.md` (shipyard#181) — the 61-unit WBS this doc reconciles against [PRIMARY/ONR] (retrieved 2026-05-29)
8. `coordination/inbox/admiral-ruling-2026-05-29T1345Z-c1-c2-bundles-start-both-now.md` — CIC C1+C2-parallel ruling [PRIMARY/Admiral] (retrieved 2026-05-29)
9. `coordination/inbox/council-verdict-test-eng-2026-05-29-shipyard-192-c2-1-project-timeline.md` + `engineer-subagent-status-...c1-1-assets-substrate-pr-opened.md` (shipyard#192 MERGED, #193 DRAFT) [PRIMARY/Engineer+Council] (retrieved 2026-05-29)
10. `shipyard/icm/01_discovery/research/erpnext-conversion-and-backlog-register-2026-05-29.md` — the reconciled backlog register (§1C bundles) [PRIMARY/ONR; seed] (retrieved 2026-05-29)
11. `coordination/inbox/research-mvp-feature-priority-2026-05-29T0205Z.md` — MVP feature-priority research [PRIMARY/Admiral-subagent; seed] (retrieved 2026-05-29)
12. `gh pr list --repo Harborline-Software/shipyard` (#192 MERGED, #184 MERGED, #191 ADR-0101 MERGED, #193 DRAFT, #195 marketing DRAFT) [PRIMARY/merged-reality] (retrieved 2026-05-29)
13. `sunfish/apps/{web,desktop}/src/api/erpnext.ts` + consumer grep + `signal-bridge/.../Proxy/ERPNextProxy.cs` [PRIMARY/disk] (retrieved 2026-05-29) — Decision (i) evidence
14. `shipyard/packages/blocks-{forms,workflow,assets,businesscases,scheduling}/**` — substrate-nature verification (forms=presentation, workflow=engine, scheduling=reservation-capable, businesscases=entitlement) [PRIMARY/disk] (retrieved 2026-05-29)

— ONR, 2026-05-30
