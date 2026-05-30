# Project-Management PILOT — Stage-05 implementation-ready hand-off

**Workstream:** C2 (Project-Management) — the first vertical reference-bundle cockpit
activation past the Property-Management MVP.
**Authored by:** ONR
**Authored at:** 2026-05-30T00:00Z
**Requester:** CIC (via Admiral dispatch) — full-parallel-push week; PM picked as the
first bundle to activate. v1 SKU = "PM + whichever cockpits are cockpit-complete by
launch," so this pilot is **launch-relevant**, not merely post-MVP.
**Status:** draft — pending Admiral consumption + Stage-05 adversarial-review dispatch
per ADR 0093 Rev 4.
**Builds on:** the ONR PM-pilot detailed cut in the post-MVP feature WBS
(`icm/01_discovery/research/post-mvp-feature-wbs-2026-05-30.md`, §2). That document is
the Stage-05 *precursor*; THIS document is the Stage-05 hand-off itself — directly
buildable by Engineer (Bridge) + FED (cockpit) with no further design discovery.
**Confidence:** HIGH on substrate-readiness + wire contracts (all verified on disk
against shipped `blocks-work-projects` + the live Bridge endpoint-family + cockpit-route
templates). **MEDIUM on effort sizings** — these are ONR PR-author-effort bands, NOT
Engineer-validated; Engineer SHALL confirm before a committed WBS.

---

## 0. Scope-of-investigation memo

**In scope:** a Stage-05 implementation hand-off for the Project-Management cockpit
pilot — the COCKPIT-ONLY activation of the already-shipped `blocks-work-projects`
domain. Three build surfaces:
1. The **`/api/v1/projects` Bridge endpoint family** (signal-bridge, C#) — ~11 endpoints
   over the shipped `blocks-work-projects` read-models + services, tenant-scoped via the
   Authorization sum-interface `ITenantContext`.
2. The **`/cockpit/projects` React surface** (sunfish web, TSX) — list + detail + tabbed
   panels (Gantt / budget / milestones / time), reusing the existing Gantt MVP component.
3. The **manifest Draft→Active flip + module-key reconcile** for the project-management
   bundle (shipyard foundation-catalog).

**Out of scope (explicitly):**
- **ANY new `blocks-work-projects` substrate.** The PM pilot is COCKPIT-ONLY — ZERO new
  domain. The 93-`.cs` block is implementation-ready; C2.1's timeline read-model already
  merged (shipyard#192). If a build step appears to need a new service/entity/event,
  STOP and escalate — that is a hand-off defect, not a Stage-06 task.
- **The dynamic-forms keystone engine.** The PM cockpit uses FIXED, TYPED forms (the C#
  project domain), NOT the user-defined dynamic-forms engine. No dependency on ADR 0055
  / `Sunfish.Foundation.Forms` / `blocks-forms` engine. See §6.
- **CRM / lead-pipeline (C2.4)** — needs the net-new `blocks-crm` shared block; out of
  the pilot critical path; enterprise-edition only.
- **Remodel-project sub-surface** (`RemodelProject`/`RemodelPhase`) — defer to enterprise;
  not on the pilot cockpit.
- **The ERPNext data importer** (Workstream A) — separate live program; `IErpnextProjectImporter`
  is orthogonal to the cockpit.
- **Cross-entity completion gating** (e.g. "all WorkOrders closed before Completed") — the
  `IProjectService` XML doc defers this to a follow-on; the pilot ships the state-machine +
  designated-authority gates ONLY.

**Authoritative sources consulted (all disk-verified, origin/main @ `87b7266`):**
`blocks-work-projects` service contracts (`IProjectService`, `IProjectReadModel`,
`IProjectTimelineReadModel`, `IProjectBudgetRepository`, `ITimeEntryService`,
`ITimeApprovalService`, `ProjectStatusMachine`); the live Bridge endpoint-family template
(`UnitsEndpointsExtensions.cs` + `AuthenticatedTenantPolicy`); the live cockpit route table
(`sunfish/apps/web/src/App.tsx` — `/cockpit` nested layout) + the Gantt MVP
(`MaintenanceWorkOrderTimeline.tsx`); ADR 0093 Rev 4 (Stage-05 adversarial-review protocol
+ S05-1..S05-5 amendments); the W#79 + WS-E Stage-05 hand-offs (canonical template); the
post-MVP feature WBS §2 (the precursor cut).

**What success looks like:** Engineer opens PR 1 (Bridge read endpoints) and FED opens its
MSW-mocked cockpit shell IN PARALLEL the moment Admiral dispatches the Stage-05 councils +
they return GREEN/AMBER-with-amendments — with every wire contract, authorization seam,
pattern-009 pair, RFC 7807 discriminator, and PR boundary pinned here.

---

## 1. Substrate inventory — what is shipped (verified on disk)

`blocks-work-projects` (93 `.cs`) ships a COMPLETE project domain. The PM cockpit binds to
these contracts; it authors NONE of them.

| Capability | Contract (on disk) | Cockpit binding |
|---|---|---|
| Project lifecycle | `Project` (8-state) + `ProjectStatusMachine` (Draft→Planned→InProgress→{OnHold,Blocked,Completed}→Closed/Cancelled) | status actions on detail header |
| Project write | `IProjectService.CreateAsync` / `TransitionStatusAsync` / `AddMilestoneAsync` / `AchieveMilestoneAsync` | create form + status + milestone writes |
| Project read | `IProjectReadModel.GetByIdAsync` / `GetSummaryAsync` / `GetMilestonesAsync`; `ProjectSummary(Id,Code,Name,Status,Kind)` | list + detail |
| Timeline / Gantt | `IProjectTimelineReadModel.GetTimelineAsync` → `ProjectTimeline` + `ProjectTimelineMilestone` (planned/actual span, `PercentComplete`, ordered bars, `PredecessorMilestoneId` edges) — shipyard#192, wire-ready | Gantt panel |
| Budget | `IProjectBudgetRepository.GetCurrentAsync` / `GetRevisionsAsync` / `GetLinesAsync` / `InsertRevisionAsync` (revision-aware; `OverlappingBudgetRevisionException`) | budget panel |
| Actuals | `IProjectActualProjector` / `IProjectActualRepository` (projected from `JournalEntryPosted`) | budget-vs-actual rollup |
| Time write | `ITimeEntryService.OpenAsync` / `StopAsync` / `SubmitAsync` (hourly rate captured at stop) | time panel |
| Time approval | `ITimeApprovalService.ApproveAsync` / `RejectAsync` (split authority; `RejectedByPartyId` distinct from `ApprovedByPartyId`) | approval queue |

**Verdict: the cockpit needs ZERO new substrate for the lite/standard editions.** Every
panel has a backing read/write contract on disk. The two authorization seams the security
council must inspect (designated-authority transition; time-rate authority split) are
already spelled out IN THE SUBSTRATE XML DOCS — see §4 and §5.

---

## 2. The `/api/v1/projects` Bridge endpoint family (Engineer)

**Pattern (from the live template `UnitsEndpointsExtensions.cs`):** a new
`ProjectsEndpointsExtensions.MapProjectsEndpoints` static class registering route groups via
`app.MapGroup("/api/v1/projects").RequireAuthorization(AuthenticatedTenantPolicy.PolicyName)`,
with each handler tenant-scoped INSIDE the handler via the **Authorization sum-interface
`ITenantContext`** (NOT the MultiTenancy narrowed variant — fleet convention
`[[itenantcontext_consumption_qualification]]`; ADR 0091 Step 3 has not yet narrowed the
facade, so consumption sites take the Authorization sum-interface). Every read passes
`tenantContext.TenantId` into the `TenantId` parameter of the backing contract; every write
does the same AND resolves the acting principal (see §4).

### 2.1 — Endpoint enumeration

| # | Method + route | Handler | Backing contract | R/W | Auth note |
|---|---|---|---|---|---|
| 1 | `GET /api/v1/projects` | `HandleListProjectsAsync` | `IProjectReadModel.GetSummaryAsync` (list reader — see §2.2) | R | tenant-scope only |
| 2 | `GET /api/v1/projects/{id}` | `HandleGetProjectDetailAsync` | `IProjectReadModel.GetByIdAsync` | R | tenant-scope only |
| 3 | `GET /api/v1/projects/{id}/timeline` | `HandleGetProjectTimelineAsync` | `IProjectTimelineReadModel.GetTimelineAsync` | R | tenant-scope only |
| 4 | `GET /api/v1/projects/{id}/milestones` | `HandleListMilestonesAsync` | `IProjectReadModel.GetMilestonesAsync` | R | tenant-scope only |
| 5 | `GET /api/v1/projects/{id}/budget` | `HandleGetBudgetAsync` | `IProjectBudgetRepository.GetCurrentAsync` + `GetLinesAsync` (+ actuals for rollup) | R | tenant-scope only |
| 6 | `GET /api/v1/projects/{id}/time` | `HandleListTimeEntriesAsync` | time-entry list reader (see §2.2) | R | tenant-scope only |
| 7 | `POST /api/v1/projects` | `HandleCreateProjectAsync` | `IProjectService.CreateAsync` | **W** | tenant-scope; `ownerPartyId` from body |
| 8 | `POST /api/v1/projects/{id}/transition` | `HandleTransitionStatusAsync` | `IProjectService.TransitionStatusAsync` | **W** | **designated-authority seam — see §4** |
| 9 | `POST /api/v1/projects/{id}/milestones` | `HandleAddMilestoneAsync` | `IProjectService.AddMilestoneAsync` | **W** | tenant-scope |
| 10 | `POST /api/v1/projects/{id}/milestones/{mid}/achieve` | `HandleAchieveMilestoneAsync` | `IProjectService.AchieveMilestoneAsync` | **W** | tenant-scope |
| 11 | `POST /api/v1/projects/{id}/budget` | `HandleInsertBudgetRevisionAsync` | `IProjectBudgetRepository.InsertRevisionAsync` | **W** | tenant-scope; `OverlappingBudgetRevisionException`→409 |
| 12 | `POST /api/v1/projects/{id}/time` | `HandleTimeLifecycleAsync` (open/stop/submit by action discriminator) | `ITimeEntryService.{Open,Stop,Submit}Async` | **W** | **time-rate-authority seam on stop — see §5** |
| 13 | `POST /api/v1/projects/{id}/time/{teid}/approve` | `HandleApproveTimeAsync` | `ITimeApprovalService.ApproveAsync` | **W** | approver-role gate (distinct from worker) |
| 14 | `POST /api/v1/projects/{id}/time/{teid}/reject` | `HandleRejectTimeAsync` | `ITimeApprovalService.RejectAsync` | **W** | approver-role gate |

> **"~11 endpoints" reconciliation:** the WBS precursor named ~11; the full enumeration here
> is **14** once the milestone-list read (#4), the time approve/reject split (#13/#14 —
> required because `ITimeApprovalService` is a SEPARATE contract from `ITimeEntryService`),
> and the time-lifecycle action discriminator (#12) are made explicit. This is NOT scope
> creep — it is the honest endpoint count for the shipped contract surface. Engineer MAY
> collapse #13/#14 into #12's action discriminator at authoring discretion (one POST with an
> `action` field) — flagged as a permitted seam, not mandated. If collapsed, the family is 12.

### 2.2 — The two list-reader gaps (the only Bridge-side "fill")

Two list endpoints have NO shipped list contract — the read-models expose `GetById`/`GetSummary`
(single) + `GetMilestones`, but NOT a tenant-wide "list all projects" or "list time entries for a
project":

- **Endpoint #1 (list projects):** `IProjectReadModel` has `GetSummaryAsync(tenantId, id)` (single).
  There is NO `ListAsync(tenantId)`. **Resolution:** the Bridge handler reads via the
  `InMemoryProjectRepository` tenant-scoped enumeration that backs `GetSummaryAsync`, projecting
  to `ProjectSummary`. This is a **Bridge-layer list assembly over the existing repository**, NOT a
  new `blocks-work-projects` contract. **DO NOT add a `ListAsync` to `IProjectReadModel`** without
  escalating — that would be substrate work outside the cockpit-only scope. If the repository's
  tenant-enumeration is not Bridge-accessible, that IS a substrate gap → escalate to Engineer for a
  scoped read-model addition (the ONLY sanctioned substrate touch, and only if forced).
- **Endpoint #6 (list time entries):** `ITimeEntryService` exposes `GetByIdAsync` (single). Same
  resolution — Bridge-layer list assembly over the time-entry repository, tenant-scoped, filtered
  by `projectId`.

> **Adversarial-brief hook (D7):** if either list-reader gap forces a substrate addition, that
> addition is a NEW substrate contract and re-enters the dual-council ADR conversation. The
> hand-off's position is that Bridge-layer assembly over the existing tenant-scoped repositories is
> sufficient; Engineer confirms at PR 1.

### 2.3 — Wire-contract reconciliation (S05-1) — `/api/v1/projects` family

Per ADR 0093 Rev 4 Amendment I: enumerate BOTH positive matches (server DTO field → frontend
interface field) AND **negative matches** (fields the frontend MUST NOT declare). The negative-match
rows force the hand-off to pin fields the frontend must not fabricate (the cohort-4 cycle-0 RED
`tenant_id`/`payload`/`signatures` trap).

**`ProjectSummary` → `ProjectSummaryDto` (list — endpoint #1):**

| Server source | Frontend interface field | Source of truth | Status |
|---|---|---|---|
| `ProjectSummary.Id` (`ProjectId`) | `id: string` (GUID) | `IProjectReadModel.cs` | MATCH |
| `ProjectSummary.Code` (`string`) | `code: string` | `IProjectReadModel.cs` | MATCH |
| `ProjectSummary.Name` (`string`) | `name: string` | `IProjectReadModel.cs` | MATCH |
| `ProjectSummary.Status` (`ProjectStatus`) | `status: ProjectStatus` (string-enum) | `ProjectStatus.cs` | MATCH |
| `ProjectSummary.Kind` (`ProjectKind`) | `kind: ProjectKind` (string-enum) | `ProjectStatus.cs` siblings | MATCH |
| (summary does NOT carry `ownerPartyId`) | frontend list MUST NOT declare `ownerPartyId` | n/a | NEGATIVE-MATCH |
| (summary does NOT carry budget/actuals) | frontend list MUST NOT declare `budget*` | n/a | NEGATIVE-MATCH |

**`ProjectTimeline` → `ProjectTimelineDto` (timeline — endpoint #3):**

| Server source | Frontend interface field | Source of truth | Status |
|---|---|---|---|
| `ProjectTimeline.ProjectId` | `projectId: string` | `IProjectTimelineReadModel.cs` | MATCH |
| `ProjectTimeline.Code` / `.Name` / `.Status` | `code` / `name` / `status` | same | MATCH |
| `ProjectTimeline.PlannedStart/End` (`DateOnly?`) | `plannedStart?: string` (ISO date) | same | MATCH |
| `ProjectTimeline.ActualStart/End` (`DateOnly?`) | `actualStart?: string` | same | MATCH |
| `ProjectTimeline.PercentComplete` (`decimal?`) | `percentComplete?: number` | same | MATCH |
| `ProjectTimelineMilestone.{Id,Code,Name,Kind,Status,PlannedDate,ActualDate,PredecessorMilestoneId}` | `milestones[]: {...}` matching | same | MATCH |
| (timeline does NOT carry budget lines) | frontend timeline MUST NOT declare `lines` | n/a | NEGATIVE-MATCH |
| (timeline does NOT carry time entries) | frontend timeline MUST NOT declare `timeEntries` | n/a | NEGATIVE-MATCH |

> **DateOnly serialization pin:** `DateOnly` serializes to ISO `yyyy-MM-dd` (no time component).
> The frontend interface MUST type these as `string` (ISO date), NOT `Date` and NOT a datetime.
> Mirror the established cohort-5/leases convention.

> **Engineer responsibility:** when authoring the DTOs, reconcile the budget + time DTOs (endpoints
> #5/#6) against `ProjectBudget`/`ProjectBudgetLine`/`TimeEntry` on disk by the SAME positive +
> negative-match method; this hand-off pins the two highest-traffic shapes (summary + timeline). The
> sec-eng + frontend-architect Stage-05 review checks these tables against the cited contract files
> directly.

### 2.4 — Error-response shape (S05-2) — RFC 7807 discriminator pin

Per ADR 0093 Rev 4 Amendment J: pin the RFC 7807 discriminator field name + enumerate the 400-class
discriminators the frontend must distinguish. The Bridge convention is the `title` field as the
discriminator (W#79 precedent).

| Failure | HTTP | `title` discriminator | Source exception |
|---|---|---|---|
| Caller is not the project owner (transition) | **403** | `not-project-owner` | `NotProjectOwnerException` |
| Budget revision date overlaps prior | **409** | `overlapping-budget-revision` | `OverlappingBudgetRevisionException` |
| Duplicate budget category / empty lines | **400** | `invalid-budget-lines` | `ArgumentException` (from `InsertRevisionAsync`) |
| Illegal status transition (state machine) | **409** | `illegal-status-transition` | `ProjectStatusMachine` reject |
| Project / milestone / time-entry not found (or cross-tenant) | **404** | `project-not-found` (etc.) | null return / H5 gate |
| Time-rate set by non-authorized role | **403** | `rate-authority-denied` | Bridge-enforced (see §5) |

> **Cross-tenant returns 404, not 403:** the H5 gate returns null on tenant mismatch (per the
> contract XML docs — "Returns null on tenant mismatch (H5)"). The Bridge maps null→404 uniformly so
> a cross-tenant probe is indistinguishable from a genuinely-absent resource (no tenant-existence
> oracle). This is a sec-eng-relevant invariant — enumerate it in the cross-tenant probe tests.

---

## 3. The `/cockpit/projects` React surface (FED)

**Pattern (from the live `App.tsx` cockpit layout):** projects slot into the existing `/cockpit`
nested-route layout EXACTLY as work-orders/vendors do — NOT a new top-level `/projects`. The router
(`App.tsx`) is a **reserved single-writer lane** (`[[rebase-sweep-shared-file-risk]]` — broke main
2026-05-28); serialize the cockpit PRs through it, do not bulk-arm auto-merge on router-touching PRs.

### 3.1 — Routes + pages

| Route | Page component | Surfaces | Backing endpoint | pattern-009? |
|---|---|---|---|---|
| `/cockpit/projects` | `ProjectListView` | summary list; filter by status/kind; "new project" | #1, #7 | **NEW route → YES** |
| `/cockpit/projects/:projectId` | `ProjectDetailView` | header (status-machine actions, owner, dates, %-complete) + tabs | #2, #8 | **NEW route → YES** |
| (tab) Gantt | `ProjectGanttPanel` | **reuse `MaintenanceWorkOrderTimeline.tsx`** (Gantt MVP) | #3 | within detail |
| (tab) Budget | `ProjectBudgetPanel` | current revision + lines + budget-vs-actual rollup; new revision | #5, #11 | within detail |
| (tab) Milestones | `ProjectMilestonesPanel` | milestone list + add/achieve; predecessor edges | #4, #9, #10 | within detail |
| (tab) Time | `ProjectTimePanel` | time-entry list + open/stop/submit; approval queue (role-gated) | #6, #12, #13, #14 | within detail |

### 3.2 — Frontend artifacts to author

- `apps/web/src/api/projects.ts` — typed API client (the DTOs from §2.3; NEW file, sibling to
  `leases.ts`/`maintenance.ts`).
- `apps/web/src/hooks/useProjects.ts` — TanStack-Query hooks (list/detail/timeline/budget/time +
  the write mutations).
- `apps/web/src/cockpit/projects/ProjectListView.tsx` + `ProjectDetailView.tsx` + the four panels.
- Route registration in `App.tsx` (two `<Route>` lines under `/cockpit`, mirroring work-orders).
- The Gantt panel **adapts** `MaintenanceWorkOrderTimeline.tsx`'s row/bar model to
  `ProjectTimelineDto.milestones[]` — it does NOT author a new Gantt engine. If the existing
  component's prop shape does not accept the timeline milestone rows directly, prefer a thin adapter
  over forking the component (`[[prefer_cleanest_long_term_option]]`).

---

## 4. Authorization seam 1 — the designated-authority transition (sec-eng load-bearing)

This is the FIRST of the two seams the security council MUST inspect.

**The contract (verbatim from `IProjectService.cs` XML doc):** "Authorization is the caller's
responsibility. Status transitions enforce Pattern A (designated authority — the `actingPartyId`
must equal `Project.OwnerPartyId`); callers MUST verify the caller's session principal matches before
invoking `TransitionStatusAsync`." The service does NOT consult `IUserContext`; on a party mismatch it
throws `NotProjectOwnerException`.

**What this means for the Bridge handler (endpoint #8):**
1. Resolve the authenticated session principal's party-id (from the auth context — NOT from the
   request body).
2. Load the project; read `Project.OwnerPartyId`.
3. The Bridge handler MUST pass the SESSION principal's party-id as `actingPartyId` — NEVER a
   body-supplied `actingPartyId`. A body-supplied acting-party is the primary auth-bypass vector.
4. `NotProjectOwnerException` → **403** `not-project-owner` (§2.4).

**Why the Bridge, not the service, is the gate:** the service is a pure domain contract that takes
`actingPartyId` as a parameter; it cannot see the HTTP session. The Bridge is the only layer that
knows "who is calling." If the Bridge forwards a body-supplied acting-party, ANY authenticated tenant
user can transition ANY project they can name by claiming to be the owner. **The session-principal
binding is the entire security control.**

---

## 5. Authorization seam 2 — the time-rate authority split (sec-eng load-bearing)

The SECOND seam. Two sub-controls:

**5a — Rate-setting on stop (endpoint #12, stop action):** the `ITimeEntryService.StopAsync` XML doc:
"captures hourly rate at stop-time. Callers MUST gate rate-setting authority to a role distinct from
the worker — this service does not consult `IUserContext`." The Bridge MUST verify the session
principal holds a rate-setting role BEFORE forwarding `hourlyRate`. A worker setting their own billable
rate is the failure mode (financial integrity). Rate-authority-denied → **403** `rate-authority-denied`.

**5b — Approval authority split (endpoints #13/#14):** `ITimeApprovalService` is a SEPARATE contract
from `ITimeEntryService` precisely so "the write + approve authorities can be split at the host's
composition root." The Bridge MUST gate approve/reject to an approver role distinct from the worker who
submitted the entry. The contract stores `RejectedByPartyId` separately from `ApprovedByPartyId` so the
read-side can distinguish — the Bridge must pass the session principal's party-id as the
approver/rejecter, NEVER a body value (same vector as §4).

**Self-approval prohibition:** the Bridge MUST reject an approve/reject where the approver party-id
equals the submitting worker's party-id (a worker approving their own time). The contract does not
enforce this (it is an authorization concern); the Bridge is the gate.

---

## 6. Dependency on the dynamic-forms keystone — NONE (load-bearing)

**The PM pilot is INDEPENDENT of the dynamic-forms engine.** The cockpit uses FIXED, TYPED forms
(create-project, add-milestone, budget-revision, time-entry) wired directly to the typed C#
`IProjectService` / `IProjectBudgetRepository` / `ITimeEntryService` contracts. It does NOT render
user-defined dynamic forms, and it does NOT depend on ADR 0055 / `Sunfish.Foundation.Forms` /
the `blocks-forms` engine.

The bundle manifest lists `forms` as a required module for *bundle-activation completeness* (so the
bundle can LATER expose dynamic intake forms), but the **cockpit build does not block on it.** Build
the PM pilot now with typed forms; fold dynamic-forms-backed intake later when that engine lands. **If
any build step reaches for a dynamic-forms dependency, that is a hand-off defect — escalate.**

---

## 7. Adversarial Brief (ADR 0093 Rev 4 §"Adversarial Brief")

Substrate-shaping escape hatch invoked: this hand-off introduces a NEW endpoint family (14 endpoints)
with two authorization seams + a financial-integrity surface → up to 12 bullets permitted. Eight used.

### Decision D1 — Body-supplied vs session-derived acting principal
- **Decision summary:** the Bridge derives `actingPartyId` (transition) + approver/rejecter party-id
  (time) + rate-setter identity from the AUTHENTICATED SESSION, never the request body.
- **Worst-case interpretation:** a careless handler forwards a body-supplied `actingPartyId`.
- **Failure mode:** complete auth bypass — any authenticated tenant user transitions any project /
  approves any time entry by claiming to be the owner/approver.
- **Mitigation:** §4 + §5 pin session-principal binding as the entire control; DTOs MUST NOT contain
  an `actingPartyId`/`approverPartyId` field (negative-match in §2.3 extension); sec-eng verifies.

### Decision D2 — Cross-tenant resource access via guessed IDs
- **Decision summary:** every read/write passes `tenantContext.TenantId` into the contract's `TenantId`
  parameter; the H5 gate returns null on mismatch.
- **Worst-case interpretation:** a handler omits the tenant parameter or maps mismatch→403.
- **Failure mode:** cross-tenant data read (omitted tenant scope) OR a tenant-existence oracle
  (403-vs-404 leak telling an attacker a project id exists in another tenant).
- **Mitigation:** §2 mandates `tenantContext.TenantId` on every call; §2.4 pins null→404 uniformly (no
  oracle); cross-tenant probe tests required per endpoint (test-eng enumeration).

### Decision D3 — Worker self-approval / self-rating
- **Decision summary:** the Bridge rejects approve/reject where approver == submitting worker, and
  gates rate-setting to a role distinct from the worker.
- **Worst-case interpretation:** the self-approval guard is omitted because the contract "looks like it
  handles authority."
- **Failure mode:** financial integrity — a worker approves their own (possibly inflated) time + sets
  their own billable rate; bogus actuals flow to the GL via `JournalEntryPosted` projection.
- **Mitigation:** §5a + §5b make the Bridge the explicit gate; self-approval prohibition is stated;
  sec-eng verifies the guard exists, not just the role check.

### Decision D4 — Budget-revision overlap / negative amounts
- **Decision summary:** `InsertRevisionAsync` rejects overlapping `EffectiveFrom` + duplicate/empty
  lines; the Bridge maps these to 409/400.
- **Worst-case interpretation:** a caller submits a revision with a past `EffectiveFrom` to silently
  supersede the current revision, or negative line amounts.
- **Failure mode:** budget history corruption / superseding the active revision retroactively; negative
  budget lines skew budget-vs-actual.
- **Mitigation:** the contract throws `OverlappingBudgetRevisionException` (date guard) + `ArgumentException`
  (line guard); §2.4 pins the discriminators; flagged for Stage-06 SPOT-CHECK that negative-amount
  validation is enforced (the contract guards duplicates/empty, NOT sign — confirm at build).

### Decision D5 — Status-transition race / illegal transition
- **Decision summary:** `ProjectStatusMachine` is authoritative on legal transitions; illegal→409.
- **Worst-case interpretation:** two concurrent transitions, or a client forcing an out-of-machine
  state (e.g. Cancelled→InProgress).
- **Failure mode:** an inconsistent project state, or a project resurrected from a terminal state.
- **Mitigation:** the state machine rejects illegal transitions (verified by `ProjectStatusMachineTests`);
  §2.4 pins `illegal-status-transition`→409; concurrency last-writer semantics flagged for Stage-06
  (the in-memory repo is single-writer; the real persistence layer's optimistic-concurrency is a
  Stage-06 forward-watch, not a Stage-05 blocker).

### Decision D6 — Frontend rebind-to-nothing (pair-merge ordering)
- **Decision summary:** the frontend cockpit PRs must NOT merge ahead of their Bridge read half.
- **Worst-case interpretation:** FED's MSW-mocked cockpit merges before the Bridge endpoints exist.
- **Failure mode:** a mid-deploy window where `/cockpit/projects` calls `/api/v1/projects` and gets
  404 — a broken launch surface.
- **Mitigation:** §9 pair-merge cascade pins PR 1 (Bridge read) as the contract-freeze gate; FED builds
  against MSW mocks but the real-binding PRs merge AFTER PR 1; the router single-writer-lane discipline
  serializes the merges.

### Decision D7 — List-reader substrate creep
- **Decision summary:** endpoints #1/#6 assemble lists at the Bridge layer over existing tenant-scoped
  repositories, NOT via new `blocks-work-projects` contracts.
- **Worst-case interpretation:** the build adds `ListAsync` to `IProjectReadModel`, silently making the
  "cockpit-only" pilot a substrate change.
- **Failure mode:** scope leak — a substrate addition that bypasses the dual-council ADR conversation
  the cockpit-only framing was meant to avoid.
- **Mitigation:** §2.2 mandates Bridge-layer assembly + escalation if forced; any `IProjectReadModel`
  addition re-enters dual-council review.

### Decision D8 — Designated-authority lockout (operational, not security)
- **Decision summary:** ONLY `Project.OwnerPartyId` can transition status (Pattern A).
- **Worst-case interpretation:** the owner leaves / is deactivated; no one can transition the project.
- **Failure mode:** operational lockout — a project stuck because its sole designated authority is gone.
- **Mitigation:** OUT of pilot scope (owner-reassignment is a follow-on); flagged here so the product
  surface knows the cockpit needs an owner-reassignment path before GA. NOT a Stage-06 blocker; an
  enterprise-edition follow-on. Recorded so it is not re-discovered.

---

## 8. FED-vs-Engineer split + MSW-mock-first guidance

| Unit | Owner | Layer | Council |
|---|---|---|---|
| Bridge `/api/v1/projects` read endpoints (#1-#6) | **Engineer** | signal-bridge (C#) | sec-eng (tenant-scope) |
| Bridge `/api/v1/projects` write endpoints (#7-#14) | **Engineer** | signal-bridge (C#) | **sec-eng MANDATORY** (both seams) |
| React cockpit list + detail shell + status actions | **FED** | sunfish web (TSX) | **pattern-009 sec-eng** (NEW routes) |
| Gantt + budget + milestones panels | **FED** | sunfish web | frontend-arch (Gantt reuse) |
| Time panel + approval queue | **FED** | sunfish web | sec-eng note (role-gated UI) |
| Manifest Draft→Active flip + module-key reconcile | **Engineer** (ONR-spec'd) | shipyard foundation-catalog | none |

**MSW-mock-first (so FED builds AHEAD of the real Bridge, then swaps):**
- FED authors `apps/web/src/api/projects.ts` to the §2.3 DTO contracts.
- FED stands up **MSW handlers** (`apps/web/src/mocks/handlers/projects.ts` or the established mocks
  location) returning DTO-shaped fixtures for all 14 endpoints — these ARE the contract-test scaffold
  (S05-3, §10).
- FED builds + tests the entire cockpit against MSW with NO running Bridge.
- The swap to the real Bridge is a config flip (MSW disabled in the integration env) — NO cockpit code
  change, BECAUSE the DTOs are frozen at PR 1 (the contract-freeze gate, §9). This is the load-bearing
  reason PR 1 is read-only + first: it freezes the wire contract FED mocks against.

---

## 9. PR decomposition + pair-merge cascade (S05-5)

The precursor proposed a 6-PR cut. Refined to **6 PRs** below; one seam flagged as collapsible.

| PR | Title (indicative, conventional-commit) | Owner | Size | Council | Dep |
|---|---|---|---|---|---|
| **PR 1** ⭐ | `feat(bridge): projects read endpoints (list/detail/timeline/milestones/budget/time)` | Engineer | M | sec-eng (tenant-scope) | C2.1 ✓ (#192) |
| **PR 2** | `feat(bridge): projects write endpoints (create/transition/milestones/budget/time/approval)` | Engineer | M | **sec-eng MANDATORY** (D1/D3 seams) | PR 1 |
| **PR 3** | `feat(web): projects cockpit — list + detail shell + status actions` | FED | M | **pattern-009 sec-eng** (NEW routes) | PR 1 (mock-swap) |
| **PR 4** | `feat(web): project detail panels — Gantt (reuse) + milestones + budget-vs-actual` | FED | M | frontend-arch (2nd-instance pattern — light) | PR 3 |
| **PR 5** | `feat(web): project time panel + approval queue` | FED | S-M | sec-eng note (role-gated UI) | PR 3 |
| **PR 6** | `chore(catalog): project-management bundle Draft->Active + module-key reconcile` | Engineer | S | none | PR 3-5 |

> **Collapsible seam flag:** PR 4 and PR 5 are split by surface (read panels vs. write+approval), NOT by
> a hard dependency — both depend only on PR 3's detail shell. Engineer/FED MAY merge them into one PR
> if the frontend lane is quiet. The split exists to keep the time-approval (sec-eng-relevant) surface
> reviewable distinctly. NOT an artificial seam — it isolates the authority-gated UI — but a permitted
> merge if velocity favors it.

### ⭐ Contract-freeze PR = **PR 1**

PR 1 (Bridge read endpoints) is the contract-freeze that unblocks FED's mock-swap. It freezes the
`/api/v1/projects` read DTOs (§2.3). FED's MSW mocks (§8) are authored against these DTOs; once PR 1
lands, the real-binding cockpit PRs (PR 3+) swap MSW→real with no code change. **PR 1 must land before
PR 3 merges** (D6 rebind-to-nothing mitigation). PR 2 (writes) can land in parallel with FED's PR 3
build (FED mocks the write endpoints too), but PR 2 SHOULD precede PR 5 (the write/approval panel's
real bind).

### Pair-merge cascade

```
PR 1 (Bridge read, contract-freeze) ──┬─→ PR 3 (cockpit shell, mock→real swap)
                                      │      ├─→ PR 4 (read panels)
PR 2 (Bridge write) ──────────────────┘      └─→ PR 5 (time/approval panel)  ──→ PR 6 (manifest flip)
```

Order invariant: PR 1 before PR 3; PR 2 before PR 5's real bind; PR 6 last (flips the bundle live only
after the cockpit exists). Serialize router-touching PRs (PR 3) through the single-writer lane.

---

## 10. MSW contract-test scaffolding (S05-3) + test enumeration (test-eng)

Per ADR 0093 Rev 4 Amendment M: name the MSW contract tests per endpoint family so the FED mock layer
IS the contract scaffold the swap relies on.

**MSW contract tests — `/api/v1/projects` family** (`apps/web/src/mocks/handlers/projects.ts` +
`*.contract.test.tsx`):
- One MSW handler per endpoint (14, or 12 if #13/#14 collapse) returning DTO-shaped fixtures.
- Contract test asserts each handler's response shape matches the `projects.ts` TypeScript interface
  (the swap's safety net — if the real Bridge diverges, the contract test catches it pre-swap).

**Test enumeration the test-eng council will check (acceptance criteria as test cases):**
- **Cross-tenant probe per read endpoint** (#1-#6): a tenant-B caller requesting a tenant-A project
  id gets 404 (not 403, not data) — D2 mitigation.
- **Designated-authority test** (#8): non-owner transition → 403 `not-project-owner`; owner →
  success — D1 mitigation.
- **Self-approval test** (#13): worker approving own time → rejected — D3 mitigation.
- **Rate-authority test** (#12 stop): worker setting own rate → 403 — D3 mitigation.
- **Budget-overlap test** (#11): revision with past `EffectiveFrom` → 409 — D4 mitigation.
- **Illegal-transition test** (#8): terminal-state→active transition → 409 — D5 mitigation.
- **Idempotency** for the POST endpoints if an Idempotency-Key is in play (confirm at PR 2).
- Frontend: per-panel render tests against MSW fixtures; the Gantt panel renders the reused component
  with timeline-milestone rows.

This is >5 acceptance-criteria test cases + a substrate-touching Bridge family + cross-cluster (FED +
Engineer) → **test-eng-council triggers** (§11).

---

## 11. Council posture for Stage-05 adversarial review (ADR 0093 Rev 4 §"Council dispatch")

All applicable councils dispatch IN PARALLEL on Admiral's consumption of this hand-off's status beacon;
SLA 30 minutes. Verdicts are independent + non-blocking on each other; Stage-06 build begins on
GREEN-or-AMBER-with-amendments across all dispatched councils.

| Council | Effort | Triggered? | What it inspects |
|---|---|---|---|
| **sec-eng-council** | Opus 4.7 + xhigh | **YES** | The two authorization seams (D1 designated-authority §4; D3 time-rate/approval §5); cross-tenant 404-not-403 invariant (D2 §2.4); session-principal binding (no body-supplied acting-party); the wire-contract negative-matches (§2.3); the pattern-009 frontend half (NEW routes §3.1). |
| **.NET-architect-council** | Opus 4.7 + xhigh | **YES** (new endpoint family) | The endpoint-family composition over the shipped contracts; the Bridge-layer list-assembly decision (D7 §2.2 — is it sound, or does it force substrate?); RFC 7807 mapping (§2.4); the `ITenantContext` Authorization-sum-interface consumption (not MultiTenancy variant); DTO design over the domain records. |
| **frontend-architect-council** | (per dispatch) | **YES** (pattern-009 frontend half) | The cockpit route-mount into the single-writer `/cockpit` lane; the Gantt-component REUSE (adapter vs. fork — D-ish); wire-contract reconciliation table from the frontend side (S05-1 §2.3); the MSW mock→real swap safety (§8). |
| **test-eng-council** | Sonnet 4.6 + medium | **YES** (>5 test cases + substrate-touch + cross-cluster) | Enumeration completeness of §10 (every read endpoint has a cross-tenant probe; every authorization seam has a negative test; every POST has the relevant idempotency/antiforgery test); the MSW contract-test scaffold completeness. |

**pattern-009 relationship:** the two NEW frontend routes (`/cockpit/projects`,
`/cockpit/projects/:projectId`) are a pattern-009 pair with the Bridge `/api/v1/projects` family →
**sec-eng SPOT-CHECK MANDATORY on PR-open** for the cockpit PRs (PR 3) per the fleet SPOT-CHECK SLA, IN
ADDITION to this Stage-05 dispatch (the Bridge family is substrate-touching → Stage-05 sec-eng applies
per ADR 0093 edge-case 3b). Per `[[pattern009_scope]]`, the pattern triggers on the NEW routes, not on
adding cases to an existing dispatcher — these routes are net-new → triggers.

**Adjudication path (dual-AMBER):** sec-eng is authoritative on whether a threat scenario exists +
what it covers; test-eng is authoritative on whether the enumeration is complete vs. the §10 acceptance
criteria. A hand-off passing sec-eng's intent gate may still fail test-eng's enumeration gate; both
must reach GREEN/AMBER-with-amendments for build.

---

## 12. Commit-message pre-flight (S05-4) — for the Stage-06 builders

Per ADR 0093 Rev 4 Amendment K + fleet commitlint traps. Before EACH PR commit:
- Subject: `type(scope): subject` — `feat(bridge): ...` / `feat(web): ...` / `chore(catalog): ...`.
- **NO `<word>#<digit>` in the commit BODY** (e.g. `shipyard#192`, `C2.1`) — wagoid parses it as a
  footer token → `footer must have leading blank line`. Pre-flight:
  `git log -1 --format=%B | grep -E '[A-Za-z]#[0-9]'` (must be empty).
- **NO `@word:` body lines** (`@standing-pattern:`, etc.) — same trap. Cross-refs (shipyard#207,
  pattern-009 claims) go in the PR DESCRIPTION, never the commit body.
- Body lines ≤100 chars; do NOT use `--no-verify`.

---

## 13. Open decisions for Admiral / CIC (before build dispatch)

1. **Endpoint count 12 vs 14 (Engineer call):** collapse the time approve/reject (#13/#14) into #12's
   action discriminator, or keep them as distinct routes? Affects the pattern-009 route count. ONR
   recommends distinct routes (cleaner sec-eng review of the authority-gated surface) but defers to
   Engineer. NOT a blocker — a build-authoring choice to confirm.
2. **List-reader assembly (Engineer confirm at PR 1):** confirm the Bridge can assemble the project +
   time-entry lists over the existing tenant-scoped repositories WITHOUT a new `IProjectReadModel.ListAsync`.
   If forced, that ONE substrate addition re-enters dual-council review (D7). This is the only place the
   "cockpit-only" framing could break — flagged for explicit Engineer sign-off.
3. **PR 4/PR 5 collapse:** permit Engineer/FED to merge the two FED panel PRs if the frontend lane is
   quiet (§9 collapsible seam)? ONR's default is the split (isolates the authority-gated time panel);
   Admiral may pre-authorize the collapse.
4. **Owner-reassignment follow-on (D8):** the designated-authority lockout (owner leaves → project
   stuck) is OUT of pilot scope but needs an owner-reassignment path before GA. Confirm this is a
   tracked follow-on, not a pilot gap.

None of (1)-(4) blocks dispatching PR 1 + the FED MSW build in parallel. They are confirmations, not
gates — the build sequence headline is **PR 1 (contract-freeze, Engineer) + FED MSW cockpit shell, in
parallel, the moment the Stage-05 councils return GREEN/AMBER**.

---

## Sources cited

1. `shipyard/packages/blocks-work-projects/Services/{IProjectService,IProjectReadModel,IProjectTimelineReadModel,IProjectBudgetRepository,ITimeEntryService,ITimeApprovalService}.cs` + `Models/ProjectStatus.cs` (93 .cs) [PRIMARY/disk] (retrieved 2026-05-30)
2. `signal-bridge/Sunfish.Bridge/Units/UnitsEndpointsExtensions.cs` + `Authorization/AuthenticatedTenantPolicy` + `Cockpit/*Endpoint.cs` — the live endpoint-family + tenant-scope template [PRIMARY/disk] (retrieved 2026-05-30)
3. `sunfish/apps/web/src/App.tsx` (`/cockpit` nested-route layout) + `api/{leases,maintenance,properties}.ts` + `components/MaintenanceWorkOrderTimeline.tsx` (Gantt MVP) [PRIMARY/disk] (retrieved 2026-05-30)
4. `shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md` — Adversarial Brief template + council trigger matrix + S05-1..S05-5 amendments [PRIMARY] (retrieved 2026-05-30)
5. `shipyard/icm/01_discovery/research/post-mvp-feature-wbs-2026-05-30.md` §2 — the PM-pilot precursor cut (shipyard#207) [PRIMARY/ONR] (retrieved 2026-05-30)
6. `shipyard/icm/05_implementation-plan/{wse-tenant-comms-hand-off,cohort-w79-hand-off,q6-tender-deep-integration-stage-05}.md` — canonical Stage-05 hand-off template [PRIMARY/ONR] (retrieved 2026-05-30)
7. C2.1 timeline read-model merge (shipyard#192) + `IProjectTimelineReadModel` wire-ready JSON [PRIMARY/merged-reality] (retrieved 2026-05-30)
8. Fleet conventions — `.claude/rules/fleet-conventions.md` (ITenantContext Authorization sum-interface; pattern-009 SPOT-CHECK SLA; commitlint footer traps; router single-writer lane) [PRIMARY] (retrieved 2026-05-30)

— ONR, 2026-05-30
