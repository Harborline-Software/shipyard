# Project-Management PILOT — Stage-05 implementation-ready hand-off

**Workstream:** C2 (Project-Management) — the first vertical reference-bundle cockpit
activation past the Property-Management MVP.
**Authored by:** ONR
**Authored at:** 2026-05-30T00:00Z
**Requester:** CIC (via Admiral dispatch) — full-parallel-push week; PM picked as the
first bundle to activate. v1 SKU = "PM + whichever cockpits are cockpit-complete by
launch," so this pilot is **launch-relevant**, not merely post-MVP.
**Status:** AMENDED — 4-council Stage-05 fold applied 2026-05-30; see changelog below.
**Builds on:** the ONR PM-pilot detailed cut in the post-MVP feature WBS
(`icm/01_discovery/research/post-mvp-feature-wbs-2026-05-30.md`, §2). That document is
the Stage-05 *precursor*; THIS document is the Stage-05 hand-off itself — directly
buildable by Engineer (Bridge) + FED (cockpit) with no further design discovery.
**Confidence:** HIGH on substrate-readiness + wire contracts (all verified on disk
against shipped `blocks-work-projects` + the live Bridge endpoint-family + cockpit-route
templates). **MEDIUM on effort sizings** — these are ONR PR-author-effort bands, NOT
Engineer-validated; Engineer SHALL confirm before a committed WBS.

---

## Stage-05 council fold (2026-05-30)

Four Stage-05 councils returned on the 0540Z parallel dispatch — ALL AMBER, ZERO RED.
Per ADR 0093 Rev 4, AMBER-with-amendments clears to Stage-06 once amendments are folded;
no re-attest round-trip. Folded per `admiral-ruling-2026-05-30T0605Z-pm-pilot-stage05-
4council-fold-and-go.md`. Summary of amendments by council:

- **sec-eng** (A1–A4): project-first tenant gate before every budget read (A1 — HIGH);
  UserId→PartyId resolution pin + write-DTO negative-match discipline (A2 — HIGH);
  permission-string names for rate-setting + approver gates (A3 — MED); IProjectReadModel
  XML-doc sourcing correction + GetMilestonesAsync empty-vs-404 distinction (A4 — LOW).
- **net-arch** (F2 + citation fixes): endpoint #6 declared a SCOPED SUBSTRATE TOUCH —
  `ITimeEntryService.GetByProjectAsync` required (not a Bridge-only assembly); actuals
  citation corrected to `IProjectActualReader`; exception type name pin.
- **test-eng** (F1–F11): §2.4 discriminator rows (self-approval/rejection, create
  validation); §2.5 write-endpoint response shapes; expanded §10 test enumeration (list
  isolation, self-rejection, antiforgery, role-gated render, named panel assertions,
  explicit #5/#6 scaffold dependency).
- **frontend-arch** (Finding 1–2 + admiral-directive-amendment-0600Z): vi.mock at the
  hook boundary replaces MSW (MSW not installed — one paradigm fleet-wide); Gantt is a
  thin adapter, all three rendering refinements (color/edges/percent) GA-deferred; route
  insertion before `:propertyId` catch-all confirmed.

Headline updated: "cockpit-only + ONE confirmed scoped substrate method (#6
`GetByProjectAsync`) + ONE principal-resolution pin (substrate-or-wiring TBD at PR 2)."
The cockpit-only thesis survives for 13 of 14 endpoints.

---

## 0. Scope-of-investigation memo

**In scope:** a Stage-05 implementation hand-off for the Project-Management cockpit
pilot — the COCKPIT-ONLY activation of the already-shipped `blocks-work-projects`
domain. Three build surfaces:
1. The **`/api/v1/projects` Bridge endpoint family** (signal-bridge, C#) — ~14 endpoints
   over the shipped `blocks-work-projects` read-models + services, tenant-scoped via the
   Authorization sum-interface `ITenantContext`.
2. The **`/cockpit/projects` React surface** (sunfish web, TSX) — list + detail + tabbed
   panels (Gantt / budget / milestones / time), reusing the existing Gantt MVP component.
3. The **manifest Draft→Active flip + module-key reconcile** for the project-management
   bundle (shipyard foundation-catalog).

**Out of scope (explicitly):**
- **ANY new `blocks-work-projects` substrate beyond the two scoped touches confirmed by
  net-arch + sec-eng councils.** The confirmed touches are: (a) `ITimeEntryService.
  GetByProjectAsync(TenantId, ProjectId, CancellationToken)` for endpoint #6 (net-arch
  F2 ruling — see §2.2 + D7); (b) the UserId→PartyId principal-resolution binding (sec-eng
  A2 — may be wiring-only if ADR 0099's auth token already carries a party-id claim;
  Engineer confirms at PR 2). If a build step appears to need any OTHER new service/entity/
  event, STOP and escalate — that is a hand-off defect, not a Stage-06 task.
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
vi.mock-mocked cockpit shell IN PARALLEL the moment Admiral dispatches the Stage-05 councils
+ they return GREEN/AMBER-with-amendments — with every wire contract, authorization seam,
pattern-009 pair, RFC 7807 discriminator, and PR boundary pinned here.

---

## 1. Substrate inventory — what is shipped (verified on disk)

`blocks-work-projects` (93 `.cs`) ships a COMPLETE project domain. The PM cockpit binds to
these contracts; it authors NONE of them except the two confirmed scoped touches (§2.2, D7).

| Capability | Contract (on disk) | Cockpit binding |
|---|---|---|
| Project lifecycle | `Project` (8-state) + `ProjectStatusMachine` (Draft→Planned→InProgress→{OnHold,Blocked,Completed}→Closed/Cancelled) | status actions on detail header |
| Project write | `IProjectService.CreateAsync` / `TransitionStatusAsync` / `AddMilestoneAsync` / `AchieveMilestoneAsync` | create form + status + milestone writes |
| Project read | `IProjectReadModel.GetByIdAsync` / `GetSummaryAsync` / `GetMilestonesAsync`; `ProjectSummary(Id,Code,Name,Status,Kind)` | list + detail |
| Timeline / Gantt | `IProjectTimelineReadModel.GetTimelineAsync` → `ProjectTimeline` + `ProjectTimelineMilestone` (planned/actual span, `PercentComplete`, ordered bars, `PredecessorMilestoneId` edges) — shipyard#192, wire-ready | Gantt panel |
| Budget | `IProjectBudgetRepository.GetCurrentAsync` / `GetRevisionsAsync` / `GetLinesAsync` / `InsertRevisionAsync` (revision-aware; `OverlappingBudgetRevisionException`) | budget panel |
| Actuals | `IProjectActualReader.GetTotalsAsync` / `GetByProjectAsync` (projected from `JournalEntryPosted`; tenant+project-scoped) — **NOTE: NOT `IProjectActualProjector`** (that is a replay/rebuild façade, not a query surface) | budget-vs-actual rollup |
| Time write | `ITimeEntryService.OpenAsync` / `StopAsync` / `SubmitAsync` (hourly rate captured at stop) | time panel |
| Time read | `ITimeEntryService.GetByProjectAsync(TenantId, ProjectId, CancellationToken)` — **SCOPED SUBSTRATE TOUCH** (net-arch F2 ruling — see §2.2 + D7) | time panel list |
| Time approval | `ITimeApprovalService.ApproveAsync` / `RejectAsync` (split authority; `RejectedByPartyId` distinct from `ApprovedByPartyId`) | approval queue |

**Revised verdict: cockpit-only + ONE confirmed scoped substrate method** (`ITimeEntryService.
GetByProjectAsync`) **+ ONE principal-resolution pin** (substrate-or-wiring TBD at PR 2 —
see §2.2, §4, §5). 13 of 14 endpoints assemble at the Bridge over shipped contracts with no
new substrate. Endpoint #6 requires a project-scoped read method on `ITimeEntryService`
(net-arch F2 ruling). The two authorization seams (designated-authority §4; time-rate/approval
§5) are spelled out in the substrate XML docs.

---

## 2. The `/api/v1/projects` Bridge endpoint family (Engineer)

**Pattern (from the live template `UnitsEndpointsExtensions.cs`):** a new
`ProjectsEndpointsExtensions.MapProjectsEndpoints` static class registering route groups via
`app.MapGroup("/api/v1/projects").RequireAuthorization(AuthenticatedTenantPolicy.PolicyName)`,
with each handler tenant-scoped INSIDE the handler via the **Authorization sum-interface
`ITenantContext`** (NOT the MultiTenancy narrowed variant — fleet convention
`[[itenantcontext_consumption_qualification]]`; ADR 0091 Step 3 has not yet narrowed the
facade, so consumption sites take the Authorization sum-interface).

**Tenant-gate ordering — CRITICAL (sec-eng A1):** for the budget read path (endpoints #5,
#11-read), the Bridge MUST:
1. Resolve the project via tenant-scoped `IProjectReadModel.GetByIdAsync(tenantContext.TenantId,
   projectId)` (which tenant-gates — returns null on mismatch).
2. Map null → 404 BEFORE calling any budget read.
3. Only then call `IProjectBudgetRepository.Get*` keyed by the now-validated `projectId`.

`IProjectBudgetRepository` takes NO `TenantId` parameter — it filters by `ProjectId`/
`ProjectBudgetId` only. The budget contract provides NO second line of defence. The project-first
gate is the sole cross-tenant protection for the budget read path. **Do not skip it.**

The same project-first gate pattern applies to `GetMilestonesAsync` (#4): gate the parent via
`GetByIdAsync` first; if the parent is absent or cross-tenant → 404. In-tenant childless project
→ 200 `[]` (empty list, not 404).

Every read passes `tenantContext.TenantId` into the `TenantId` parameter of the backing contract
where one exists; every write does the same AND resolves the acting principal (see §4, §5).

### 2.1 — Endpoint enumeration

| # | Method + route | Handler | Backing contract | R/W | Auth note |
|---|---|---|---|---|---|
| 1 | `GET /api/v1/projects` | `HandleListProjectsAsync` | `InMemoryProjectRepository.ListByTenant(TenantId)` (public) — Bridge-layer assembly; see §2.2 | R | tenant-scope only |
| 2 | `GET /api/v1/projects/{id}` | `HandleGetProjectDetailAsync` | `IProjectReadModel.GetByIdAsync` | R | tenant-scope only |
| 3 | `GET /api/v1/projects/{id}/timeline` | `HandleGetProjectTimelineAsync` | `IProjectTimelineReadModel.GetTimelineAsync` | R | tenant-scope only |
| 4 | `GET /api/v1/projects/{id}/milestones` | `HandleListMilestonesAsync` | `IProjectReadModel.GetMilestonesAsync` (project-first gate required — see §2 preamble) | R | tenant-scope only |
| 5 | `GET /api/v1/projects/{id}/budget` | `HandleGetBudgetAsync` | project-first gate → `IProjectBudgetRepository.GetCurrentAsync` + `GetLinesAsync` + `IProjectActualReader.GetTotalsAsync`/`GetByProjectAsync` (rollup) | R | **project-first gate MANDATORY** — see §2 preamble |
| 6 | `GET /api/v1/projects/{id}/time` | `HandleListTimeEntriesAsync` | `ITimeEntryService.GetByProjectAsync(TenantId, ProjectId, CancellationToken)` — **scoped substrate touch (net-arch F2)** | R | tenant-scope only |
| 7 | `POST /api/v1/projects` | `HandleCreateProjectAsync` | `IProjectService.CreateAsync` | **W** | tenant-scope; `ownerPartyId` from body (create only — different from session-derived auth principal) |
| 8 | `POST /api/v1/projects/{id}/transition` | `HandleTransitionStatusAsync` | `IProjectService.TransitionStatusAsync` | **W** | **designated-authority seam — see §4** |
| 9 | `POST /api/v1/projects/{id}/milestones` | `HandleAddMilestoneAsync` | `IProjectService.AddMilestoneAsync` | **W** | tenant-scope |
| 10 | `POST /api/v1/projects/{id}/milestones/{mid}/achieve` | `HandleAchieveMilestoneAsync` | `IProjectService.AchieveMilestoneAsync` (scope milestone by tenantId AND projectId — see §10) | **W** | tenant-scope |
| 11 | `POST /api/v1/projects/{id}/budget` | `HandleInsertBudgetRevisionAsync` | project-first gate → `IProjectBudgetRepository.InsertRevisionAsync` | **W** | tenant-scope; `OverlappingBudgetRevisionException`→409 |
| 12 | `POST /api/v1/projects/{id}/time` | `HandleTimeLifecycleAsync` (open/stop/submit by action discriminator) | `ITimeEntryService.{Open,Stop,Submit}Async` | **W** | **time-rate-authority seam on stop — see §5** |
| 13 | `POST /api/v1/projects/{id}/time/{teid}/approve` | `HandleApproveTimeAsync` | `ITimeApprovalService.ApproveAsync` | **W** | approver-role gate (distinct from worker); self-approval guard required |
| 14 | `POST /api/v1/projects/{id}/time/{teid}/reject` | `HandleRejectTimeAsync` | `ITimeApprovalService.RejectAsync` | **W** | approver-role gate; self-rejection guard required (same party-id equality check as approve) |

> **"~11 endpoints" reconciliation:** the WBS precursor named ~11; the full enumeration here
> is **14** once the milestone-list read (#4), the time approve/reject split (#13/#14 —
> required because `ITimeApprovalService` is a SEPARATE contract from `ITimeEntryService`),
> and the time-lifecycle action discriminator (#12) are made explicit. This is NOT scope
> creep — it is the honest endpoint count for the shipped contract surface. Engineer MAY
> collapse #13/#14 into #12's action discriminator at authoring discretion (one POST with an
> `action` field) — flagged as a permitted seam, not mandated. If collapsed, the family is 12.

### 2.2 — List-reader assembly and the two confirmed substrate touches

**Endpoint #1 (list projects):** `IProjectReadModel` has `GetSummaryAsync(tenantId, id)` (single).
There is NO `ListAsync(tenantId)`. **Resolution:** `InMemoryProjectRepository.ListByTenant(TenantId)`
is PUBLIC (`Services/InMemoryProjectRepository.cs:37`) — the Bridge handler injects the concrete
repository directly and calls `ListByTenant(tenantContext.TenantId)`, projecting to `ProjectSummary`.
This is a Bridge-layer assembly over the existing repository. **DO NOT add a `ListAsync` to
`IProjectReadModel`** without escalating — that would be substrate work outside the cockpit-only
scope. (Note: injecting a concrete `InMemoryProjectRepository` departs from the established
service-interface idiom; this is a permitted pragmatic exception — net-arch F3 flag, not a gate.)

**Endpoint #6 (list time entries) — SCOPED SUBSTRATE TOUCH (net-arch F2 ruling):**
`ITimeEntryService` exposes only single-entry read + write methods. The in-memory repository's
`ListByTenant` is `internal` **by deliberate design** (XML-doc explicit: "internal so the public
surface matches the future Postgres impl which won't expose an unbounded tenant scan"); it is NOT
visible to the Bridge (`InternalsVisibleTo` covers only `.Tests`, not `Sunfish.Bridge`). The
Bridge CANNOT assemble the time-entry list via the existing public surface.

**Required addition (confirmed by net-arch council + Admiral ruling):**
`Task<IReadOnlyList<TimeEntry>> GetByProjectAsync(TenantId tenantId, ProjectId projectId,
CancellationToken ct)` on `ITimeEntryService` + its in-memory implementation. This is a
project-scoped read (not an unbounded tenant scan → consistent with the documented future-Postgres
constraint). Do NOT flip `ListByTenant` to public — that contradicts the substrate author's stated
intent. This re-enters review as D7 predicted — but it is ONE project-scoped method, not a new
read-model contract.

> **Adversarial-brief hook (D7):** both list-reader gaps are now resolved. Endpoint #1 is
> Bridge-layer assembly (no new substrate). Endpoint #6 is the ONE confirmed scoped substrate
> addition. Any further substrate additions are out of cockpit-only scope — escalate.

**Principal-resolution touch (sec-eng A2 — may be wiring-only):**
The write paths require `Guid PartyId`, but `ICurrentUser` exposes only `string UserId` + `Roles`.
`IPartyReadModel` maps party→displayName, NOT user→party — it does not close the gap. **Engineer
confirms at PR 2:** does `blocks-people-foundation` expose a session-principal→PartyId reader, OR
does ADR 0099's auth token already carry a party-id claim? If ADR 0099's principal already carries
it, this is a pure Bridge wiring pin (no new substrate). If neither exists, this becomes a second
scoped substrate addition. Either way it MUST be resolved — no builder may reach for a body-supplied
`actingPartyId` as a workaround (re-opens D1 bypass — see §4).

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

> **Engineer responsibility:** when authoring the DTOs, reconcile the budget + time DTOs
> (endpoints #5/#6) against `ProjectBudget`/`ProjectBudgetLine`/`TimeEntry` on disk by the SAME
> positive + negative-match method; this hand-off pins the two highest-traffic shapes (summary +
> timeline). The reconciliation table is produced at PR 1; FED's contract scaffold for the budget
> and time panels (PR 4) is blocked on it — see §9 cascade.

### 2.4 — Error-response shape (S05-2) — RFC 7807 discriminator pin

Per ADR 0093 Rev 4 Amendment J: pin the RFC 7807 discriminator field name + enumerate the
400-class discriminators the frontend must distinguish. The Bridge convention is the `title` field
as the discriminator (W#79 precedent).

| Failure | HTTP | `title` discriminator | Source exception |
|---|---|---|---|
| Caller is not the project owner (transition) | **403** | `not-project-owner` | `NotProjectOwnerException` |
| Budget revision date overlaps prior | **409** | `overlapping-budget-revision` | `OverlappingBudgetRevisionException` |
| Duplicate budget category / empty lines | **400** | `invalid-budget-lines` | `ArgumentException` (from `InsertRevisionAsync`) |
| Illegal status transition (state machine) | **409** | `illegal-status-transition` | `InvalidProjectStatusTransitionException` (thrown from `Project.TransitionStatus` via `ProjectStatusMachine.cs` — catch this exact type in the Bridge handler) |
| Project / milestone / time-entry not found (or cross-tenant) | **404** | `project-not-found` (etc.) | null return / H5 gate |
| Time-rate set by non-authorized role | **403** | `rate-authority-denied` | Bridge-enforced (see §5a) |
| Worker approves or rejects own time entry | **403** | `self-approval-denied` | Bridge-enforced party-id equality check (see §5b) — applies symmetrically to both #13 (approve) and #14 (reject) |
| Invalid project create payload (missing required field / duplicate code) | **400** | `invalid-project-payload` | `ArgumentException` from `IProjectService.CreateAsync` (duplicate code may be 409 — confirm at PR 2 against `CreateAsync` throw behavior; the discriminator MUST be pinned before PR 2 build) |

> **Cross-tenant returns 404, not 403:** the H5 gate returns null on tenant mismatch. Verified
> at the repository impl (`InMemoryProjectRepository.GetById:22-26`) for the project read path;
> XML-doc-stated for timeline (`IProjectTimelineReadModel.cs:31-37`) and time-entry
> (`ITimeEntryService.cs:70`). The runtime behavior is correct for `IProjectReadModel` even
> though the XML-doc does not repeat the H5 note for `GetByIdAsync`/`GetSummaryAsync`. The Bridge
> maps null→404 uniformly so a cross-tenant probe is indistinguishable from a genuinely-absent
> resource (no tenant-existence oracle). This is a sec-eng-relevant invariant — enumerate it in
> the cross-tenant probe tests (§10).

> **GetMilestonesAsync empty-list vs cross-tenant-parent:** an in-tenant project with no
> milestones returns **200 `[]`** (empty list). A cross-tenant or absent parent (gated via
> `IProjectReadModel.GetByIdAsync` first) returns **404**. Gate the parent before calling
> `GetMilestonesAsync`; the method itself returns a non-nullable list (not null).

### 2.5 — Write-endpoint response shapes (test-eng F8)

Per test-eng F8: without pinned response shapes, contract tests pass vacuously. Enumerated here
with the same positive/negative-match discipline as the read DTOs.

| # | Endpoint | HTTP success | Body | Negative body constraint |
|---|---|---|---|---|
| 7 | `POST /api/v1/projects` (create) | **201 Created** | `{ id: string }` (the new `ProjectId` GUID) + `Location` header | MUST NOT include `ownerPartyId` or budget fields |
| 8 | `POST .../transition` | **200 OK** | `{ id: string, status: ProjectStatus }` (updated status) | MUST NOT include `actingPartyId` |
| 9 | `POST .../milestones` (add) | **201 Created** | `{ id: string }` (new milestone `MilestoneId` GUID) | MUST NOT include party fields |
| 10 | `POST .../milestones/{mid}/achieve` | **200 OK** | `{ id: string, status: string }` (achieved milestone id + status) | MUST NOT include party fields |
| 11 | `POST .../budget` (insert revision) | **201 Created** | `{ id: string }` (new `ProjectBudgetId` GUID) | MUST NOT include line amounts computed server-side |
| 12 | `POST .../time` (open/stop/submit) | **200 OK** | `{ id: string, status: TimeEntryStatus }` (entry id + new status) | MUST NOT include `hourlyRate` set by body on stop — rate is handler-verified then forwarded; body carries it as input only |
| 13 | `POST .../approve` | **200 OK** | `{ id: string, approvedByPartyId: string }` (entry id + server-resolved approver) | MUST NOT include body-supplied approver id |
| 14 | `POST .../reject` | **200 OK** | `{ id: string, rejectedByPartyId: string }` (entry id + server-resolved rejecter) | MUST NOT include body-supplied rejecter id |

> These shapes are Engineer's call to confirm at PR 1/PR 2 against the actual domain record
> fields; the TypeScript interfaces in `api/projects.ts` must match and the vi.mock fixtures
> must be shaped to them. FED's panel contract tests MUST assert the response shape, not just
> that the call was made.

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
| (tab) Gantt | `ProjectGanttPanel` | **reuse `MaintenanceWorkOrderTimeline.tsx`** via thin adapter (see §3.2) | #3 | within detail |
| (tab) Budget | `ProjectBudgetPanel` | current revision + lines + budget-vs-actual rollup; new revision | #5, #11 | within detail |
| (tab) Milestones | `ProjectMilestonesPanel` | milestone list + add/achieve; predecessor edges | #4, #9, #10 | within detail |
| (tab) Time | `ProjectTimePanel` | time-entry list + open/stop/submit; approval queue (role-gated) | #6, #12, #13, #14 | within detail |

**Route insertion order (frontend-arch confirmed — advisory → confirmed):**
Insert BEFORE the `:propertyId` catch-all in `App.tsx` (currently ~line 183). React Router v7
will otherwise match `projects` as a `propertyId` and render `PropertyDetailView` instead of
`ProjectListView`:

```tsx
<Route path="projects" element={<ProjectListView />} />
<Route path="projects/:projectId" element={<ProjectDetailView />} />
{/* ← insert above this line: */}
<Route path=":propertyId" element={<PropertyDetailView />} />
```

**CockpitLayout nav:** add a "Projects" nav link to `CockpitLayout.tsx` (alongside Properties /
Work Orders / Vendors / Dashboard). That file is a separate non-shared surface — NOT an App.tsx
single-writer-lane conflict.

### 3.2 — Frontend artifacts to author

- `apps/web/src/api/projects.ts` — typed API client (the DTOs from §2.3 + §2.5; NEW file, sibling
  to `leases.ts`/`maintenance.ts`; follow the module-level doc comment convention from `leases.ts`
  with workstream reference + layer note + fetch convention summary; `credentials: 'include'`).
- `apps/web/src/hooks/useProjects.ts` — TanStack-Query hooks (list/detail/timeline/budget/time +
  the write mutations).
- `apps/web/src/cockpit/projects/ProjectListView.tsx` + `ProjectDetailView.tsx` + the four panels.
- Route registration in `App.tsx` (two `<Route>` lines under `/cockpit`, before the `:propertyId`
  catch-all — see §3.1 insertion order above).
- The Gantt panel adapts `MaintenanceWorkOrderTimeline.tsx`'s row/bar model to
  `ProjectTimelineDto.milestones[]` via a **thin adapter function**. Mapping:
  `plannedDate → scheduledDate`, `actualDate → completedDate`, `id → workOrderId`,
  `status` passthrough, `appointmentDate → null`. Do NOT fork the component.
  **Pilot rendering scope (frontend-arch + admiral-directive-amendment-0600Z):**
  - **Status→color mapping: deferred.** Fallback gray bars are acceptable for the pilot
    (milestone-status strings will produce the `BAR_COLORS` fallback `bg-gray-100`). Color
    mapping is a GA follow-on.
  - **Predecessor-edge rendering: deferred.** Component renders flat independent bars; the
    `predecessorMilestoneId` field is preserved in the DTO but no edge visualization is built.
  - **Percent-complete fill: deferred.** Component has no percent-fill bar rendering.

**Mock strategy (frontend-arch F2 + admiral-directive-amendment-0600Z):**

**Use `vi.mock` at the hook boundary — NOT MSW.** MSW is not installed in `sunfish/apps/web`
(not in `package.json`, no `src/mocks/` directory, no `mockServiceWorker.js`, no MSW server
setup in `test-setup.ts`). The established pattern in the project is `vi.mock('@/hooks/use*')`
(see `src/pages/MaintenancePage.test.tsx` and the rest). One mocking paradigm fleet-wide is the
cleaner long-term call.

**Implementation:**
- Mock the `useProjects` TanStack-Query hooks via `vi.mock('@/hooks/useProjects')` in the
  view/panel tests, returning **DTO-shaped fixtures** matching the §2.3 + §2.5 contracts.
- Do NOT `npm install msw`, do NOT create `src/mocks/`, do NOT add `mockServiceWorker.js`.
- The mock→real swap story: unset the hook mock; the typed `api/projects.ts` client already
  points at the real Bridge URL with `credentials: 'include'`. No cockpit code changes.
- The contract-test scaffold IS the typed `api/projects.ts` client + hook-mock fixtures shaped
  to the frozen PR-1 DTOs — type-level contract enforcement. test-eng's write-endpoint response
  shapes (§2.5) make the TS interfaces precise enough that the fixtures ARE the contract.

**Engineer override:** Engineer holds technical-corps authority and may elect MSW (Option A) if
the HTTP-wire-contract value is judged worth standing up the new infra as a PR-3 precursor. Until
Engineer directs otherwise, build to `vi.mock`.

---

## 4. Authorization seam 1 — the designated-authority transition (sec-eng load-bearing)

This is the FIRST of the two seams the security council MUST inspect.

**The contract (verbatim from `IProjectService.cs` XML doc):** "Authorization is the caller's
responsibility. Status transitions enforce Pattern A (designated authority — the `actingPartyId`
must equal `Project.OwnerPartyId`); callers MUST verify the caller's session principal matches
before invoking `TransitionStatusAsync`." The service does NOT consult `IUserContext`; on a party
mismatch it throws `NotProjectOwnerException`.

**What this means for the Bridge handler (endpoint #8):**
1. Resolve the authenticated session principal's `PartyId` from the auth context — NOT from the
   request body. See §2.2 (principal-resolution pin) for the resolution source: confirm whether
   `blocks-people-foundation` exposes a session-principal→PartyId reader OR whether ADR 0099's
   auth token carries a party-id claim. **Engineer confirms at PR 2.**
2. Load the project; read `Project.OwnerPartyId`.
3. The Bridge handler MUST pass the SESSION principal's `PartyId` as `actingPartyId` — NEVER a
   body-supplied `actingPartyId`. A body-supplied acting-party is the primary auth-bypass vector.
4. `NotProjectOwnerException` → **403** `not-project-owner` (§2.4).

**Why the Bridge, not the service, is the gate:** the service is a pure domain contract that takes
`actingPartyId` as a parameter; it cannot see the HTTP session. The Bridge is the only layer that
knows "who is calling." If the Bridge forwards a body-supplied acting-party, ANY authenticated
tenant user can transition ANY project they can name by claiming to be the owner. **The session-
principal binding is the entire security control.**

**Write-DTO negative-match (sec-eng A2 — load-bearing):** the transition / approve / reject DTOs
carry **NO** body field named `actingPartyId`, `approverPartyId`, `rejecterPartyId`, or `partyId`.
These values are server-derived from the resolved principal ONLY. The §2.3 negative-match table
already pins the read DTO shape; this explicit statement covers the write-DTO side. A contract test
MUST assert the absence of these fields.

---

## 5. Authorization seam 2 — the time-rate authority split (sec-eng load-bearing)

The SECOND seam. Two sub-controls:

**5a — Rate-setting on stop (endpoint #12, stop action):** the `ITimeEntryService.StopAsync` XML doc:
"captures hourly rate at stop-time. Callers MUST gate rate-setting authority to a role distinct from
the worker — this service does not consult `IUserContext`." The Bridge MUST verify the session
principal holds a rate-setting role BEFORE forwarding `hourlyRate`. A worker setting their own
billable rate is the failure mode (financial integrity). Rate-authority-denied → **403**
`rate-authority-denied`.

**Permission string for rate-setting gate (sec-eng A3):** the Bridge passes `"time:rate:set"` (or
the pinned role value `"RateSetter"`) to `IAuthorizationContext.HasPermission(string)` for this
check — OR gates via `ICurrentUser.Roles` containing `"RateSetter"`. **Engineer pins the exact
value at PR 2** by inspecting the `IAuthorizationContext` setup and the role/permission scheme
in use. The discriminator name `"time:rate:set"` is a recommended placeholder; the actual string
MUST be explicit in the PR 2 implementation spec before build.

**5b — Approval authority split (endpoints #13/#14):** `ITimeApprovalService` is a SEPARATE contract
from `ITimeEntryService` precisely so "the write + approve authorities can be split at the host's
composition root." The Bridge MUST gate approve/reject to an approver role distinct from the worker
who submitted the entry. The contract stores `RejectedByPartyId` separately from `ApprovedByPartyId`
so the read-side can distinguish — the Bridge must pass the session principal's `PartyId` as the
approver/rejecter, NEVER a body value (same vector as §4).

**Permission string for approve/reject gate (sec-eng A3):** the Bridge passes `"time:approve"` (or
`"TimeApprover"`) to `IAuthorizationContext.HasPermission`. **Engineer pins the exact value at PR 2**
by the same method as §5a. The `"time:approve"` placeholder is a recommended form; must be explicit
before build.

**Self-approval prohibition (applies symmetrically to #13 AND #14):** the Bridge MUST reject an
approve/reject where the approver party-id equals the submitting worker's party-id. Guard: load the
time entry tenant-scoped via `ITimeEntryService.GetByIdAsync`; compare the resolved session `PartyId`
(from §4 / §2.2 principal resolution — ONE resolved value, not two separate resolutions) against
`TimeEntry.WorkerPartyId`. If equal → **403** `self-approval-denied` (§2.4). Apply the SAME guard on
both #13 (approve) and #14 (reject).

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
- **Mitigation:** §4 + §5 pin session-principal binding as the entire control; transition/approve/
  reject DTOs MUST NOT contain `actingPartyId`/`approverPartyId`/`rejecterPartyId`/`partyId` fields
  (write-DTO negative-match in §4 — sec-eng A2); sec-eng verifies.

### Decision D2 — Cross-tenant resource access via guessed IDs
- **Decision summary:** every read/write passes `tenantContext.TenantId` into the contract's `TenantId`
  parameter where one exists. For the budget read path, the Bridge uses the project-first tenant gate
  (§2 preamble) because `IProjectBudgetRepository` has no `TenantId` parameter (sec-eng A1).
- **Worst-case interpretation:** a handler omits the tenant parameter or maps mismatch→403.
- **Failure mode:** cross-tenant data read (omitted tenant scope) OR a tenant-existence oracle
  (403-vs-404 leak telling an attacker a project id exists in another tenant).
- **Mitigation:** §2 mandates `tenantContext.TenantId` on every contract call that accepts it; the
  project-first tenant gate (§2 preamble) covers the budget path; §2.4 pins null→404 uniformly (no
  oracle); cross-tenant probe tests required per endpoint (§10 test enumeration).

### Decision D3 — Worker self-approval / self-rating
- **Decision summary:** the Bridge rejects approve/reject where approver == submitting worker, and
  gates rate-setting to a role distinct from the worker.
- **Worst-case interpretation:** the self-approval guard is omitted because the contract "looks like it
  handles authority."
- **Failure mode:** financial integrity — a worker approves their own (possibly inflated) time + sets
  their own billable rate; bogus actuals flow to the GL via `JournalEntryPosted` projection.
- **Mitigation:** §5a + §5b make the Bridge the explicit gate; self-approval prohibition is stated;
  §2.4 pins `self-approval-denied`→403; sec-eng verifies the guard exists on BOTH approve (#13) and
  reject (#14) paths. Use ONE resolved `PartyId` (from §4 / §2.2) for both sides of the equality
  check — do not resolve separately.

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
- **Mitigation:** the state machine rejects illegal transitions via `InvalidProjectStatusTransitionException`
  (`ProjectStatusMachine.cs:40`, thrown from `Project.TransitionStatus:149-150`) — the Bridge catch-block
  targets this exact type; §2.4 pins `illegal-status-transition`→409; concurrency last-writer semantics
  flagged for Stage-06 (the in-memory repo is single-writer; the real persistence layer's optimistic-
  concurrency is a Stage-06 forward-watch, not a Stage-05 blocker).

### Decision D6 — Frontend rebind-to-nothing (pair-merge ordering)
- **Decision summary:** the frontend cockpit PRs must NOT merge ahead of their Bridge read half.
- **Worst-case interpretation:** FED's vi.mock-mocked cockpit merges before the Bridge endpoints exist.
- **Failure mode:** a mid-deploy window where `/cockpit/projects` calls `/api/v1/projects` and gets
  404 — a broken launch surface.
- **Mitigation:** §9 pair-merge cascade pins PR 1 (Bridge read) as the contract-freeze gate; FED builds
  against vi.mock but the real-binding PRs merge AFTER PR 1; the router single-writer-lane discipline
  serializes the merges.

### Decision D7 — List-reader substrate creep (net-arch F2 ruling applied)
- **Decision summary:** endpoint #1 assembles at the Bridge over `InMemoryProjectRepository.ListByTenant`
  (public — no new substrate). Endpoint #6 requires `ITimeEntryService.GetByProjectAsync(TenantId,
  ProjectId, CancellationToken)` — a ONE-method scoped substrate addition (net-arch council confirmed;
  Admiral ruling 0605Z binding). This is the ONE confirmed substrate touch beyond Bridge-assembly.
- **Worst-case interpretation:** the build adds further list methods to `IProjectReadModel` or flips
  `InMemoryTimeEntryRepository.ListByTenant` to public (contradicts documented future-Postgres intent).
- **Failure mode:** scope leak — substrate additions that bypass the dual-council ADR conversation.
- **Mitigation:** §2.2 mandates Bridge-layer assembly for #1; adds `GetByProjectAsync` for #6 only;
  any further `IProjectReadModel` or `ITimeEntryService` additions re-enter dual-council review. The
  `ListByTenant` internal seal is intentional — do not break it.

### Decision D8 — Designated-authority lockout (operational, not security)
- **Decision summary:** ONLY `Project.OwnerPartyId` can transition status (Pattern A).
- **Worst-case interpretation:** the owner leaves / is deactivated; no one can transition the project.
- **Failure mode:** operational lockout — a project stuck because its sole designated authority is gone.
- **Mitigation:** OUT of pilot scope (owner-reassignment is a follow-on); flagged here so the product
  surface knows the cockpit needs an owner-reassignment path before GA. NOT a Stage-06 blocker; an
  enterprise-edition follow-on. Recorded so it is not re-discovered.

---

## 8. FED-vs-Engineer split + vi.mock-first guidance

| Unit | Owner | Layer | Council |
|---|---|---|---|
| Bridge `/api/v1/projects` read endpoints (#1-#6) | **Engineer** | signal-bridge (C#) | sec-eng (tenant-scope) |
| Bridge `/api/v1/projects` write endpoints (#7-#14) | **Engineer** | signal-bridge (C#) | **sec-eng MANDATORY** (both seams) |
| React cockpit list + detail shell + status actions | **FED** | sunfish web (TSX) | **pattern-009 sec-eng** (NEW routes) |
| Gantt + budget + milestones panels | **FED** | sunfish web | frontend-arch (Gantt reuse — thin adapter, gray bars OK) |
| Time panel + approval queue | **FED** | sunfish web | sec-eng note (role-gated UI) |
| Manifest Draft→Active flip + module-key reconcile | **Engineer** (ONR-spec'd) | shipyard foundation-catalog | none |

**vi.mock-first (so FED builds AHEAD of the real Bridge, then swaps):**
- FED authors `apps/web/src/api/projects.ts` to the §2.3 + §2.5 DTO contracts.
- FED mocks the `useProjects` TanStack-Query hooks via `vi.mock('@/hooks/useProjects')` in each
  view/panel test file, returning DTO-shaped fixtures for all 14 endpoints — these ARE the contract-
  test scaffold (S05-3, §10). This is the established project pattern (see `MaintenancePage.test.tsx`).
- FED builds + tests the entire cockpit against vi.mock with NO running Bridge.
- The swap to the real Bridge: unset the hook mock; the typed `api/projects.ts` fetcher already points
  at the real Bridge URL with `credentials: 'include'` — NO cockpit code change.
- PR 1 is the contract-freeze gate (§9): it freezes the read DTOs FED mocks against. FED's §2.5
  write-endpoint shapes are frozen at PR 2 for the time/approval panels.

---

## 9. PR decomposition + pair-merge cascade (S05-5)

The precursor proposed a 6-PR cut. Refined to **6 PRs** below; one seam flagged as collapsible.

| PR | Title (indicative, conventional-commit) | Owner | Size | Council | Dep |
|---|---|---|---|---|---|
| **PR 1** ⭐ | `feat(bridge): projects read endpoints (list/detail/timeline/milestones/budget/time)` | Engineer | M | sec-eng (tenant-scope) | C2.1 ✓ (#192) |
| **PR 2** | `feat(bridge): projects write endpoints (create/transition/milestones/budget/time/approval)` | Engineer | M | **sec-eng MANDATORY** (D1/D3 seams) | PR 1 |
| **PR 3** | `feat(web): projects cockpit — list + detail shell + status actions` | FED | M | **pattern-009 sec-eng** (NEW routes) | PR 1 (mock-swap) |
| **PR 4** | `feat(web): project detail panels — Gantt (reuse) + milestones + budget-vs-actual` | FED | M | frontend-arch (2nd-instance pattern — light) | PR 3; **budget/time DTO reconciliation from PR 1 required for contract tests — see §9 cascade note** |
| **PR 5** | `feat(web): project time panel + approval queue` | FED | S-M | sec-eng note (role-gated UI) | PR 3 |
| **PR 6** | `chore(catalog): project-management bundle Draft->Active + module-key reconcile` | Engineer | S | none | PR 3-5 |

> **Collapsible seam flag:** PR 4 and PR 5 are split by surface (read panels vs. write+approval), NOT
> by a hard dependency — both depend only on PR 3's detail shell. Engineer/FED MAY merge them into one
> PR if the frontend lane is quiet. The split exists to keep the time-approval (sec-eng-relevant)
> surface reviewable distinctly. NOT an artificial seam — it isolates the authority-gated UI — but a
> permitted merge if velocity favors it.

### ⭐ Contract-freeze PR = **PR 1**

PR 1 (Bridge read endpoints) is the contract-freeze that unblocks FED's mock-swap. It freezes the
`/api/v1/projects` read DTOs (§2.3). FED's vi.mock fixtures (§8) are authored against these DTOs;
once PR 1 lands, the real-binding cockpit PRs (PR 3+) swap vi.mock→real with no code change. **PR 1
must land before PR 3 merges** (D6 rebind-to-nothing mitigation). PR 2 (writes) can land in parallel
with FED's PR 3 build (FED mocks the write endpoints too), but PR 2 SHOULD precede PR 5 (the
write/approval panel's real bind).

**PR 1 also produces the budget/time DTO reconciliation table** (§2.3 Engineer responsibility):
FED's PR 4 budget and time panel contract tests are **blocked** on this table — the vi.mock fixtures
for budget (#5) and time (#6) panels MUST be shaped to the actual `ProjectBudget`/`ProjectBudgetLine`/
`TimeEntry` fields. FED MAY stub the budget/time panel mocks at PR 1 authoring time but the contract
test shape assertions MUST be updated before PR 4 opens. **Explicit PR1→PR4 dependency.**

### Pair-merge cascade

```
PR 1 (Bridge read, contract-freeze) ──┬─→ PR 3 (cockpit shell, mock→real swap)
  + budget/time DTO reconciliation    │      ├─→ PR 4 (read panels — blocked on budget/time DTO)
  (PR4 contract tests blocked here)   │      └─→ PR 5 (time/approval panel)  ──→ PR 6 (manifest flip)
PR 2 (Bridge write) ──────────────────┘
```

Order invariant: PR 1 before PR 3; PR 4 budget/time contract tests finalized after PR 1 DTO table;
PR 2 before PR 5's real bind; PR 6 last (flips the bundle live only after the cockpit exists).
Serialize router-touching PRs (PR 3) through the single-writer lane.

---

## 10. Contract-test scaffolding (S05-3) + test enumeration (test-eng)

Per ADR 0093 Rev 4 Amendment M: name the contract tests per endpoint family so the FED mock layer
IS the contract scaffold the swap relies on. Mock strategy is vi.mock at the hook boundary (§3.2, §8).

**Contract tests — `/api/v1/projects` family** (per-panel `*.test.tsx` files):
- One vi.mock fixture per endpoint (14, or 12 if #13/#14 collapse) returning DTO-shaped data matching
  the `projects.ts` TypeScript interfaces (§2.3 + §2.5). The fixture IS the contract; if the real
  Bridge diverges from the TypeScript type, the type-check catches it pre-swap.
- Contract test asserts each handler's response shape matches the `projects.ts` interface.

**Test enumeration the test-eng council will check (acceptance criteria as test cases):**

**Cross-tenant + list-isolation (#1–#6):**
- **List isolation test (#1):** a tenant-B authenticated caller receives `GET /api/v1/projects`;
  the response body MUST contain zero items seeded for tenant-A, even when tenant-A has projects
  with known IDs. Assert response is 200 with an empty (or tenant-B-only) array. This is a
  **data-isolation** test, NOT an id-based 404 probe — they are distinct test cases.
- **Cross-tenant id-based 404 (#2–#6):** a tenant-B caller requesting a tenant-A `{id}` gets 404
  (not 403, not data) — D2 mitigation. Distinct per endpoint for endpoints #2–#6.

**Designated-authority tests (#8):**
- Non-owner transition → 403 `not-project-owner`; owner → success — D1 mitigation.

**Self-approval and self-rejection tests (#13, #14):**
- Self-approval test (#13): worker approving own time entry → **403** `self-approval-denied`;
  approver party-id == submitting worker party-id — D3 mitigation.
- Self-rejection test (#14): worker rejecting own time entry → **403** `self-approval-denied`;
  same equality check, same discriminator — symmetric guard; distinct test case.

**Reject positive test (#14):**
- An authorized approver (party-id != submitting worker's party-id) rejects a submitted time entry →
  **200 OK**; response confirms rejection; `rejectedByPartyId` is the approver's party-id (server-
  derived), NOT a body-supplied value.

**Create-project validation (#7):**
- POST with missing required field (e.g. no Name) → **400** `invalid-project-payload`.
- POST with duplicate Code on the same tenant → **409** or **400** (confirm at PR 2 against
  `CreateAsync` throw behavior — the discriminator MUST be pinned before PR 2 build, not deferred).

**Wrong-project milestone probe (#10):**
- `POST .../projects/{projectA-id}/milestones/{milestone-of-projectB-id}/achieve` → **404**
  `milestone-not-found`. Milestone exists in the tenant but belongs to a different project; the
  handler MUST scope the milestone lookup by BOTH tenantId AND projectId. Distinct from cross-tenant
  and from milestone-not-found.

**Rate-authority test (#12 stop):**
- Worker setting own billable rate → **403** `rate-authority-denied` — D3 mitigation.

**Budget-overlap test (#11):**
- Revision with overlapping `EffectiveFrom` → **409** `overlapping-budget-revision` — D4 mitigation.

**Illegal-transition test (#8):**
- Terminal-state → active transition (e.g. Closed → InProgress) → **409** `illegal-status-transition`
  — D5 mitigation.

**Antiforgery enforcement (#7–#14 — do NOT defer):**
All 8 POST endpoints MUST opt into antiforgery (`await antiforgery.ValidateRequestAsync(httpContext)` —
Bridge CSRF is per-handler-manual per fleet cerebrum `[2026-05-29]`). Pin at minimum ONE antiforgery-
rejection test per mutating handler (or an arch test asserting every PM mutating endpoint calls
`ValidateRequestAsync`). A request with a missing or invalid antiforgery token → **400** (or 403 per
the Bridge convention). "Confirm at PR 2" without naming a test case is NOT an enumeration — the
antiforgery check must be explicitly tested.

**Budget/time DTO contract tests (#5, #6 — scaffold blocked on PR 1):**
FED's vi.mock fixtures for the budget (#5) and time (#6) panels are provisional until Engineer
produces the §2.3 DTO reconciliation table at PR 1. The MSW handler stubs MAY be wired at PR 1
authoring time but the contract test shape assertions MUST be updated to match PR 1's actual DTOs
before FED's PR 4 opens. **Explicit PR1→PR4 dependency tracked in the pair-merge cascade (§9).**

**Frontend — Time panel role-gated render (PR 5):**
- Render `ProjectTimePanel` with vi.mock returning time entries and the current user in the "worker"
  role (not approver). Assert: approve button is absent/disabled for all entries.
- Render again with the current user in the "approver" role. Assert: approve button is present and
  actionable. Security-relevant — frontend that renders approval buttons for all roles defeats the
  role-gate intent even if the Bridge rejects the call.

**Frontend — Budget panel named render assertions (PR 4):**
- Current revision lines render with correct category names + amounts.
- Budget-vs-actual rollup row is present and displays the actuals figure.
- "Add revision" affordance is present.
- Negative: if actuals data is absent from the vi.mock fixture, rollup renders as zero or a
  placeholder (not a crash).

**Frontend — Milestones panel named render assertions (PR 4):**
- Milestone rows render in the correct order.
- Predecessor edges (`predecessorMilestoneId` non-null) are preserved in DTO (even if the thin Gantt
  adapter defers edge visualization to GA follow-on).
- Achieved milestones are distinguished from pending milestones.

This is >5 acceptance-criteria test cases + a substrate-touching Bridge family + cross-cluster (FED +
Engineer) → **test-eng-council triggers** (§11).

---

## 11. Council posture for Stage-05 adversarial review (ADR 0093 Rev 4 §"Council dispatch")

**Status: ALL FOUR COUNCILS RETURNED — AMBER, 0 RED. Stage-06 GO per ADR 0093 Rev 4 once
amendments folded (this document is the fold).**

All applicable councils dispatched IN PARALLEL on Admiral's consumption of this hand-off's status
beacon; SLA 30 minutes. Verdicts are independent + non-blocking on each other.

| Council | Effort | Triggered? | Verdict | What it inspected |
|---|---|---|---|---|
| **sec-eng-council** | Opus 4.7 + xhigh | **YES** | **AMBER (0 RED)** — A1-A4 folded above | The two authorization seams (D1 designated-authority §4; D3 time-rate/approval §5); cross-tenant 404-not-403 invariant (D2 §2.4); session-principal binding; wire-contract negative-matches (§2.3); pattern-009 frontend half (NEW routes §3.1). |
| **.NET-architect-council** | Opus 4.7 + xhigh | **YES** | **AMBER (0 RED)** — F2 + citation fixes folded above | Endpoint-family composition over shipped contracts; Bridge-layer list-assembly decision (D7 §2.2); RFC 7807 mapping (§2.4); `ITenantContext` Authorization-sum-interface consumption; DTO design over domain records. |
| **frontend-architect-council** | (per dispatch) | **YES** | **AMBER (0 RED)** — all three folds applied above | Cockpit route-mount into single-writer `/cockpit` lane; Gantt-component REUSE (thin adapter scope); wire-contract reconciliation (S05-1 §2.3); mock strategy. |
| **test-eng-council** | Sonnet 4.6 + medium | **YES** | **AMBER (0 RED)** — F1-F11 folded above | Enumeration completeness of §10 (cross-tenant probes; authorization seam negatives; antiforgery; response shapes; role-gated render). |

**pattern-009 relationship:** the two NEW frontend routes (`/cockpit/projects`,
`/cockpit/projects/:projectId`) are a pattern-009 pair with the Bridge `/api/v1/projects` family →
**sec-eng SPOT-CHECK MANDATORY on PR-open** for the cockpit PRs (PR 3) per the fleet SPOT-CHECK SLA,
IN ADDITION to this Stage-05 dispatch. Per `[[pattern009_scope]]`, the pattern triggers on the NEW
routes, not on adding cases to an existing dispatcher — these routes are net-new → triggers.

**Adjudication path (dual-AMBER):** sec-eng is authoritative on whether a threat scenario exists +
what it covers; test-eng is authoritative on whether the enumeration is complete vs. the §10
acceptance criteria. A hand-off passing sec-eng's intent gate may still fail test-eng's enumeration
gate; both must reach GREEN/AMBER-with-amendments for build.

---

## 12. Commit-message pre-flight (S05-4) — for the Stage-06 builders

Per ADR 0093 Rev 4 Amendment K + fleet commitlint traps. Before EACH PR commit:
- Subject: `type(scope): subject` — `feat(bridge): ...` / `feat(web): ...` / `chore(catalog): ...`.
- **NO `<word>#<digit>` in the commit BODY** (e.g. `shipyard#192`, `C2.1`) — wagoid parses it as a
  footer token → `footer must have leading blank line`. Pre-flight:
  `git log -1 --format=%B | grep -E '[A-Za-z]#[0-9]'` (must be empty).
- **NO `@word:` body lines** (`@standing-pattern:`, etc.) — same trap. Cross-refs (pattern-009 claims)
  go in the PR DESCRIPTION, never the commit body.
- Body lines ≤100 chars; do NOT use `--no-verify`.

---

## 13. Open decisions for Admiral / CIC (before build dispatch)

1. **Endpoint count 12 vs 14 (Engineer call):** collapse the time approve/reject (#13/#14) into #12's
   action discriminator, or keep them as distinct routes? Affects the pattern-009 route count. ONR
   recommends distinct routes (cleaner sec-eng review of the authority-gated surface) but defers to
   Engineer. NOT a blocker — a build-authoring choice to confirm.
2. **Principal-resolution source (Engineer confirm at PR 2):** confirm whether `blocks-people-
   foundation` exposes a session-principal→PartyId reader OR ADR 0099's auth token already carries
   a party-id claim. If neither, this becomes a second scoped substrate addition (sec-eng A2). If ADR
   0099's principal already carries it, it is a pure Bridge wiring pin. Must be explicit before PR 2
   build — not a Stage-05 blocker, but a PR 2 pre-flight confirm (§2.2).
3. **PR 4/PR 5 collapse:** permit Engineer/FED to merge the two FED panel PRs if the frontend lane is
   quiet (§9 collapsible seam)? ONR's default is the split (isolates the authority-gated time panel);
   Admiral may pre-authorize the collapse.
4. **Owner-reassignment follow-on (D8):** the designated-authority lockout (owner leaves → project
   stuck) is OUT of pilot scope but needs an owner-reassignment path before GA. Confirm this is a
   tracked follow-on, not a pilot gap.
5. **Permission string values (Engineer confirm at PR 2):** the `"time:rate:set"` and `"time:approve"`
   placeholder strings in §5a/§5b must be replaced with the exact strings the `IAuthorizationContext`
   setup uses. Confirm before PR 2 build.
6. **Engineer option on vi.mock vs MSW (technical-corps call):** FED is directed to vi.mock (§3.2, §8)
   per the frontend-arch fold. Engineer holds technical-corps authority and may elect MSW as a PR-3
   precursor if the HTTP-wire-contract value is judged worth the infra stand-up cost.

None of (1)-(6) blocks dispatching PR 1 + the FED vi.mock cockpit shell in parallel. They are
confirmations, not gates — the build sequence headline is **PR 1 (contract-freeze, Engineer) + FED
vi.mock cockpit shell, in parallel, the moment the Stage-05 councils return GREEN/AMBER-with-
amendments-folded**.

---

## Sources cited

1. `shipyard/packages/blocks-work-projects/Services/{IProjectService,IProjectReadModel,IProjectTimelineReadModel,IProjectBudgetRepository,ITimeEntryService,ITimeApprovalService}.cs` + `Models/ProjectStatus.cs` (93 .cs) [PRIMARY/disk] (retrieved 2026-05-30)
2. `signal-bridge/Sunfish.Bridge/Units/UnitsEndpointsExtensions.cs` + `Authorization/AuthenticatedTenantPolicy` + `Cockpit/*Endpoint.cs` — the live endpoint-family + tenant-scope template [PRIMARY/disk] (retrieved 2026-05-30)
3. `sunfish/apps/web/src/App.tsx` (`/cockpit` nested-route layout) + `api/{leases,maintenance,properties}.ts` + `components/MaintenanceWorkOrderTimeline.tsx` (Gantt MVP) + `package.json` + `test-setup.ts` + `vitest.config.ts` + `src/pages/MaintenancePage.test.tsx` [PRIMARY/disk] (retrieved 2026-05-30 by frontend-arch council)
4. `shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md` — Adversarial Brief template + council trigger matrix + S05-1..S05-5 amendments [PRIMARY] (retrieved 2026-05-30)
5. `shipyard/icm/01_discovery/research/post-mvp-feature-wbs-2026-05-30.md` §2 — the PM-pilot precursor cut [PRIMARY/ONR] (retrieved 2026-05-30)
6. `shipyard/icm/05_implementation-plan/{wse-tenant-comms-hand-off,cohort-w79-hand-off,q6-tender-deep-integration-stage-05}.md` — canonical Stage-05 hand-off template [PRIMARY/ONR] (retrieved 2026-05-30)
7. C2.1 timeline read-model merge (shipyard#192) + `IProjectTimelineReadModel` wire-ready JSON [PRIMARY/merged-reality] (retrieved 2026-05-30)
8. Fleet conventions — `.claude/rules/fleet-conventions.md` (ITenantContext Authorization sum-interface; pattern-009 SPOT-CHECK SLA; commitlint footer traps; router single-writer lane) [PRIMARY] (retrieved 2026-05-30)
9. `council-verdict-sec-eng-2026-05-30-pm-pilot-stage05.md` — sec-eng AMBER verdict, amendments A1-A4 [PRIMARY/council] (2026-05-30T1742Z)
10. `council-verdict-net-arch-2026-05-30-pm-pilot-stage05.md` — .NET-arch AMBER verdict, F2 + citation fixes [PRIMARY/council] (2026-05-30T0523Z)
11. `council-verdict-test-eng-2026-05-30-pm-pilot-stage05.md` — test-eng AMBER verdict, F1-F11 [PRIMARY/council] (2026-05-30T0521Z)
12. `council-verdict-frontend-arch-2026-05-30-pm-pilot-stage05.md` — frontend-arch AMBER verdict, Findings 1-3 [PRIMARY/council] (2026-05-30T0420Z)
13. `admiral-ruling-2026-05-30T0605Z-pm-pilot-stage05-4council-fold-and-go.md` — consolidated fold disposition [PRIMARY/admiral] (2026-05-30T0605Z)
14. `admiral-directive-amendment-2026-05-30T0600Z-fed-pm-cockpit-frontend-arch-folds.md` — frontend vi.mock ruling + Gantt scope + route order [PRIMARY/admiral] (2026-05-30T0600Z)

— ONR, 2026-05-30 (amended 2026-05-30 per 4-council Stage-05 fold)
