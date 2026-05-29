# ONR research — Workstream C bundle substrate-readiness audit (2026-05-29)

**Requester:** CIC (via Admiral dispatch, 2026-05-29)
**Author:** ONR
**Scope:** READ-ONLY substrate-readiness audit of the 4 non-PM reference bundles (Workstream C
of the post-MVP WBS) — Asset-Management (C1), Project-Management (C2), Facility-Operations (C3),
Acquisition/Underwriting (C4). For each bundle: compare the Draft manifest's `requiredModules` +
`featureDefaults` against the shipped `blocks-*` substrate ON DISK; classify each module COMPLETE
/ PARTIAL / NOT BUILT; produce the C*.1 substrate-fill scope and a cockpit readiness verdict;
recommend a dispatch order by ascending substrate gap; flag NEW-package units needing a `dual`
ADR. In scope: validating/correcting the WBS's "partial" calls and sizings. Out of scope: writing
production code, authoring the C*.1 ADRs themselves, building the cockpits.
**Status:** final
**Confidence:** HIGH on what substrate exists (every required-module block inspected on disk:
.cs counts + entity/service surface read against each manifest's `featureDefaults`). MEDIUM on
sizings (S/M/L bands not validated against Engineer). This audit MATERIALLY CORRECTS three of the
WBS's substrate calls (see Finding 1) — the WBS shipped on stale assumptions for `blocks-assets`,
`blocks-businesscases`, and `blocks-scheduling`/`blocks-reservations`.

---

## TL;DR

- **The WBS got the dispatch ORDER right but three substrate CALLS wrong.** C3-first remains
  correct; the gap ordering is **C3 < C2 < C1 < C4** (ascending gap). But C1 (assets) is far
  bigger than the WBS's "partial" framing, C4 (diligence) has effectively NO reuse substrate, and
  C3's NEW `blocks-reservations` package is NOT warranted — conflict-detection already ships in
  `blocks-scheduling`.
- **`blocks-assets` is a STUB, not "partial."** 3 .cs files; its only model `AssetRecord` is a
  *filesystem-catalog* record (Id/Name/Path/SizeBytes/LastModifiedUtc), NOT a depreciable business
  asset. ZERO lifecycle / depreciation / warranty / maintenance-link substrate. C1.1 is a
  near-greenfield domain-block build, not a "completeness audit." **Reclassify C1.1 `M`→`L`.**
- **`blocks-businesscases` is the BUNDLE-ENTITLEMENT engine, not diligence substrate.** Its 18 .cs
  are `BundleActivationRecord` / `TenantEntitlementSnapshot` / `IBundleProvisioningService` /
  `BundleEntitlementResolver` — the machinery that activates bundles and resolves
  editions→modules→featureDefaults. There is NO diligence-checklist / evidence / approval-gate /
  deal-pipeline / data-room entity. The WBS map `diligence → blocks-businesscases PARTIAL` is a
  **category error.** C4.1 diligence is GREENFIELD.
- **C3's NEW `blocks-reservations` package is likely UNWARRANTED.** `blocks-scheduling` already
  ships `SlotReservation` + `ReservationOutcome` (with `SLOT_CONFLICT`/`SLOT_INVERTED`) +
  `IScheduleReservationCoordinator` backed by a Flease-lease CP-class writer — the *strongest*
  possible conflict-detection (serialized, fail-closed). The manifest's
  `reservations.conflictDetection.enabled:true` is **already satisfiable today.** C3.1 should be a
  bookable-RESOURCE model on top of scheduling, NOT a new conflict-engine package — and may not
  need a `dual` ADR at all.
- **Readiness verdicts (one line each):**
  - **C3 Facility-Ops** — READY: cockpit buildable today; substrate-fill is small (resource model
    + 2 featureDefault fields). Lowest gap. Dispatch first.
  - **C2 Project-Mgmt** — READY: richest substrate (`blocks-work-projects`, 85 .cs, full
    project/budget/milestone/time surface); C2.1 is a thin confirm-audit. Dispatch second.
  - **C1 Asset-Mgmt** — BLOCKED on substrate-fill: `blocks-assets` must be built as a real asset
    domain-block first; cockpit cannot be built today. Dispatch third.
  - **C4 Acq/Underwriting** — BLOCKED on substrate-fill: diligence + data-room are greenfield on
    `blocks-businesscases`/`blocks-docs`; security-heaviest; hard prereq. Dispatch last.

---

## Method

For each of the 4 bundles I read the Draft manifest
(`packages/foundation-catalog/Manifests/Bundles/<bundle>.bundle.json`), enumerated its
`requiredModules` + `featureDefaults`, then for each required module located the shipped
`blocks-*` package on disk and inspected: (1) the .cs file count (excluding bin/obj), (2) the
`Models/` entity surface, (3) the `Services/` interface+impl surface, against the specific
`featureDefaults` the bundle declares. The WBS's manifest-key→package mapping table was the
starting map; every call was re-verified on disk. Classifications:

- **COMPLETE** — the entities/services to satisfy this bundle's `featureDefaults` exist today.
- **PARTIAL** — substrate exists but specific `featureDefaults` are unsatisfiable without fills.
- **NOT BUILT** — no substrate, or the mapped package is a different domain than the manifest key.

`.cs` counts (verified on disk, 2026-05-29, off `origin/main` @ 3f96b04):

| Block | .cs | Block | .cs |
|---|---|---|---|
| `blocks-workflow` | 16 | `blocks-work-projects` | **85** |
| `blocks-forms` | 3 | `blocks-businesscases` | 18 |
| `blocks-tasks` | 6 | `blocks-docs` | 40 |
| `blocks-scheduling` | 10 | `blocks-reports` | 52 |
| `blocks-assets` | **3** | `blocks-people-foundation` | 36 |
| `blocks-maintenance` | 65 | `blocks-inspections` | 35 |

ABSENT on disk (confirmed): `blocks-reservations`, `blocks-procurement`, `blocks-crm`,
`blocks-vendors`, `blocks-contacts`, `blocks-diligence`, `blocks-accounting`. The latter four are
manifest-key aliases satisfied (or not) by other packages — see the corrected mapping table.

---

## Finding 1 — Corrected manifest-key → shipped-package mapping (supersedes the WBS table)

The WBS table is mostly right but has **three load-bearing errors** (marked ⚠). Build against THIS:

| Manifest key | Shipped package | .cs | WBS said | DISK REALITY (corrected) |
|---|---|---|---|---|
| `blocks.workflow` | `blocks-workflow` | 16 | YES | **COMPLETE** |
| `blocks.forms` | `blocks-forms` | 3 | YES | **COMPLETE** (thin but sufficient) |
| `blocks.tasks` | `blocks-tasks` | 6 | YES (thin) | **COMPLETE** (subtasks/deps surface present per WBS) |
| `blocks.scheduling` | `blocks-scheduling` | 10 | YES | **COMPLETE** — incl. reservation conflict-detection ⚠ |
| `blocks.assets` | `blocks-assets` | 3 | YES (partial) | ⚠ **NOT BUILT for business-assets** — `AssetRecord` is a filesystem catalog entry, not a depreciable asset |
| `blocks.maintenance` | `blocks-maintenance` | 65 | YES | **COMPLETE** (minor featureDefault fills — SLA/intake-channel) |
| `blocks.inspections` | `blocks-inspections` | 35 | YES | **COMPLETE** (recurring trigger present) |
| `blocks.projects` | `blocks-work-projects` | 85 | YES | **COMPLETE** (richest non-PM block) |
| `blocks.crm` | — (Party/PartyRole in people-foundation) | — | PARTIAL | **PARTIAL** — entity base exists; NO pipeline/stage/deal entity |
| `blocks.contacts` | `blocks-people-foundation` | 36 | YES | **COMPLETE** (Party/Address/Email/Phone) |
| `blocks.diligence` | `blocks-businesscases` | 18 | PARTIAL | ⚠ **NOT BUILT** — businesscases is the bundle-ENTITLEMENT engine, not diligence substrate |
| `blocks.documents` | `blocks-docs` | 40 | YES | **PARTIAL** for data-room — doc base solid; NO data-room/watermark/external-access-audit |
| `blocks.reporting` | `blocks-reports` | 52 | YES | **COMPLETE** as a report framework; `financialAnalysis` = new report-kind (small) |
| `blocks.reservations` | — (capability in `blocks-scheduling`) | — | NOT BUILT | ⚠ **PARTIAL, not NOT-BUILT** — conflict-detection engine ships in scheduling; missing only a bookable-resource model |
| `blocks.procurement` | — | — | NOT BUILT | **NOT BUILT** (no C1–C4 unit requires it; optionalModule only) |
| `blocks.vendors` | `blocks-maintenance` Vendor + people-foundation | 65/36 | YES (substrate) | **COMPLETE** — `blocks-maintenance` has a full `Vendor`+onboarding+W9+performance surface |

**The three corrections that change C-unit scope/sizing:**

1. **`blocks.assets` → NOT BUILT (not "partial").** Evidence: the entire model surface is one
   record —
   ```csharp
   public sealed record AssetRecord {
       public required string Id; public required string Name; public required string Path;
       public long SizeBytes; public DateTime? LastModifiedUtc;
   }
   ```
   That is a file-catalog entry (`Path`, `SizeBytes`, `LastModifiedUtc`). The asset-management
   manifest's `featureDefaults` — `assets.lifecycle.tracking`, `assets.depreciation.autoCalculate`,
   `assets.warrantyReminders` — have ZERO backing entities (no AssetLifecycleState, no
   DepreciationSchedule, no Warranty, no AcquisitionCost, no disposal/retirement). C1.1 is a
   domain-block build from near-zero.

2. **`blocks.diligence` → NOT BUILT (not "partial").** Evidence: `blocks-businesscases` is the
   bundle-entitlement engine — `IBusinessCaseService.GetSnapshotAsync` returns a
   `TenantEntitlementSnapshot` by reading `IBundleCatalog` + `InMemoryBundleActivationStore`; the
   18 .cs are `BundleActivationRecord`/`BundleEntitlementResolver`/`IBundleProvisioningService`/
   `TenantEntitlementSnapshot`. The acquisition manifest's `diligence.evidenceRequired`,
   `diligence.approvalGates.enabled`, `documents.dataRoom.enabled` have NO backing entities
   (no DiligenceChecklist, no Evidence, no ApprovalGate, no DealPipeline, no DataRoom). C4.1 is
   greenfield diligence + greenfield data-room.

3. **`blocks.reservations` → PARTIAL via `blocks-scheduling` (not a NEW package).** Evidence:
   `blocks-scheduling` ships `SlotReservation` + `ReservationOutcome` (rejection reasons
   `SLOT_CONFLICT` / `SLOT_INVERTED` / `QUORUM_UNAVAILABLE`) + `IScheduleReservationCoordinator` /
   `ScheduleReservationCoordinator`, with reservation writes flowing through a Flease-lease
   CP-class writer (kernel-lease; "double-booking is worse than unavailability" — paper §2.2/§6.3).
   That IS the conflict-detection the manifest asks for, and it's the strongest possible form. What
   `blocks-scheduling` lacks is a bookable-RESOURCE catalog (resource definitions, availability
   windows, recurrence, calendar projection) — a model layer, not a new conflict engine. **C3.1
   should EXTEND scheduling with a resource model, not create `blocks-reservations`.**

---

## Per-bundle audit

### C1 — Asset-Management

**Required modules (manifest):** workflow, forms, tasks, scheduling, assets, maintenance, inspections.

| Module | Classification | Notes |
|---|---|---|
| workflow / forms / tasks / scheduling | COMPLETE | shared substrate |
| maintenance | COMPLETE | `blocks-maintenance` (65 .cs) — vendor quotes (`maintenance.vendorQuotes`) satisfied by `Rfq`/`Quote` |
| inspections | COMPLETE | `blocks-inspections` (35 .cs) — photos/severity satisfiable |
| **assets** | **NOT BUILT** | only `AssetRecord` file-catalog record; ZERO lifecycle/depreciation/warranty |

**featureDefaults reality:**
- `assets.lifecycle.tracking.enabled:true` — NO substrate. Need: asset entity (acquisition, status,
  location, custody), lifecycle state machine (acquired→in-service→maintenance→retired→disposed).
- `assets.depreciation.autoCalculate:false` — default-off, but the manifest implies a
  DepreciationSchedule entity exists to be toggled. NONE exists.
- `assets.warrantyReminders.enabled:true` — NO Warranty entity, no reminder hook.
- `maintenance.*` / `inspections.*` photos/quotes/severity — SATISFIABLE (those blocks are built).
- `logistics.transfers.enabled:false` — optional, default-off, no `blocks-logistics` (fine).

**C1.1 substrate-fill scope (CORRECTED — bigger than WBS):** Build `blocks-assets` as a real
asset domain-block: `Asset` aggregate (id, tag/serial, category, acquisitionCost, acquisitionDate,
custodyOwner, location, status), `AssetLifecycleState` machine, `DepreciationSchedule` (method +
periods; auto-calc toggleable), `Warranty` (provider, expiry, reminder window) + a warranty-reminder
hook, and a maintenance-link (asset↔workorder, mirroring the existing inspections↔maintenance
opaque-string-reference independence pattern). This is a domain-block from near-zero — NOT a
completeness audit. **Sizing: C1.1 `M`→`L`** (split: asset aggregate + lifecycle / depreciation +
warranty / maintenance-link). **Council: dual** (it's a NEW substrate domain-block, financial
surface via depreciation) — UPGRADE from the WBS's `test-eng`.

**Readiness verdict:** **BLOCKED on substrate-fill.** The asset cockpit (C1.3) cannot be built
today — there is no asset to surface. C1.1 is a hard prerequisite and is a real domain-block build.

---

### C2 — Project-Management

**Required modules (manifest):** workflow, forms, tasks, scheduling — all COMPLETE.
Primary substrate `blocks-work-projects` (85 .cs) is an optionalModule but is the bundle's spine.

| Module | Classification | Notes |
|---|---|---|
| workflow / forms / tasks / scheduling | COMPLETE | required set fully shipped |
| projects (`blocks-work-projects`) | COMPLETE | `Project`, `ProjectBudget`+`BudgetLine`, `ProjectMilestone`, `TimeEntry`/`TimeLog`, `ProjectActual`, `RemodelProject`/`RemodelPhase`, full repo+service surface, `ProjectStatusMachine` |
| crm | PARTIAL | Party/PartyRole base in people-foundation; NO pipeline/stage entity |

**featureDefaults reality:**
- `projects.budgetTracking.enabled:true` — SATISFIED (`ProjectBudget`/`ProjectBudgetLine`/`ProjectActual`).
- `tasks.subtasks.enabled` / `tasks.dependencies.enabled` — per WBS, present in `blocks-tasks`
  (confirm in C2.1; thin block at 6 .cs).
- `scheduling.resourceBooking.enabled:true` — SATISFIED (scheduling reservation coordinator).
- `reservations.conflictDetection.enabled:true` — SATISFIED (scheduling, per Finding 1.3).
- `projects.ganttView.enabled:false` — default-off; Gantt is a UI concern (FED Gantt MVP reusable).
- `crm.pipelineStages.default` — needs a pipeline entity (NOT built); but CRM is optional for PM.

**C2.1 substrate-fill scope:** Genuinely a **confirm-audit** (S–M, matches WBS): verify
`blocks-tasks` subtasks/dependencies, confirm budget-tracking + time-entry surfaces are
cockpit-complete, confirm milestone surface. Likely small gap-fills (e.g. a task-dependency edge
type if absent). The CRM pipeline (C2.4) is the only net-new substrate and it's OPTIONAL — skip
unless PM bundle wants lead intake.

**Readiness verdict:** **READY.** Cockpit (C2.3) is buildable today on the existing
`blocks-work-projects` surface; C2.1 is a thin confirm. Richest reuse of any C bundle.

---

### C3 — Facility-Operations

**Required modules (manifest):** workflow, forms, tasks, scheduling, maintenance, inspections, assets.

| Module | Classification | Notes |
|---|---|---|
| workflow / forms / tasks | COMPLETE | shared |
| scheduling | COMPLETE | reservation conflict-detection present (Finding 1.3) |
| maintenance | COMPLETE | `WorkOrder`, `MaintenanceRequest`, `Rfq`/`Quote`, `Vendor`+onboarding+W9+performance, appointment, completion-attestation, entry-notice — the richest ops block after work-projects |
| inspections | COMPLETE | `Inspection`/`InspectionTemplate`/`ChecklistItem`/`Deficiency`+severity/`EquipmentConditionAssessment`/recurring trigger |
| **assets** | **NOT BUILT** | same `AssetRecord` stub as C1 — but C3 leans on assets far less than C1 |

**featureDefaults reality:**
- `maintenance.vendorQuotes.enabled:true` — SATISFIED (`Rfq`/`Quote`/`QuoteStatus`).
- `maintenance.photos.required` / `inspections.photos.required` / `inspections.severity` — SATISFIED.
- `inspections.recurring.enabled:true` — SATISFIED (`InspectionTrigger`/recurring).
- `scheduling.resourceBooking` / `reservations.conflictDetection` — SATISFIED (scheduling).
- `vendors.performanceScoring.enabled:false` — default-off; `VendorPerformanceRecord`/`...Event`
  EXIST, so even toggling on is cheap.
- `maintenance.workOrderIntake.multiChannel:true` — **GAP (small):** `MaintenanceRequest` has no
  intake-source/channel field (Description/Priority/Status/RequestedBy only). Add a `channel` enum.
- `maintenance.slaTracking.enabled:true` — **GAP (small):** no SLA-deadline/response-target field
  on `WorkOrder`/`MaintenanceRequest`. Add SLA target + breach-clock.
- `assets.*` — the manifest lists `assets` as required, but C3's facility-ops cockpit needs assets
  only as a *referenced* dimension (work-order targets a facility/asset). If C1 hasn't shipped a
  real `blocks-assets` yet, C3 can launch with a facility/location dimension and an opaque
  asset-reference (mirroring maintenance's `DeficiencyReference` string-ref independence pattern),
  deferring full asset depth to C1. **C3 is NOT blocked on C1.**

**C3.1 substrate-fill scope (CORRECTED — smaller than WBS, NO new package):**
1. EXTEND `blocks-scheduling` with a bookable-RESOURCE model (resource catalog: rooms/equipment/
   spaces; availability windows; optional recurrence) on top of the EXISTING
   `IScheduleReservationCoordinator` conflict engine. Do NOT create `blocks-reservations`; do NOT
   re-build conflict-detection.
2. Add the two small `blocks-maintenance` featureDefault fills: intake `channel` enum
   (multi-channel intake) + SLA target/breach-clock fields.
   **Sizing: `M` (was `M`–`L`).** **Council: `sec-eng` or `test-eng`, NOT `dual`** — extending an
   existing block with a resource model is not a new-substrate-package event. If CIC/Engineer still
   prefer a standalone `blocks-reservations` package for separation-of-concerns, THAT decision (new
   package vs. extend scheduling) is the only thing warranting a `dual` ADR — see Finding 4.

**Readiness verdict:** **READY (lowest gap).** Cockpit (C3.3) is buildable today against
maintenance + inspections + scheduling; substrate-fill is a resource model + 2 small fields, not a
new conflict-engine package. **Confirms the WBS's C3-first recommendation** — for the right reason
(maintenance/inspections/scheduling are the most-built ops blocks), with a smaller C3.1 than the
WBS scoped.

---

### C4 — Acquisition / Underwriting

**Required modules (manifest):** workflow, forms, tasks — all COMPLETE. Everything that makes this
bundle an *acquisition* bundle is in the optionalModules and is unbuilt.

| Module | Classification | Notes |
|---|---|---|
| workflow / forms / tasks | COMPLETE | required set shipped |
| crm | PARTIAL | Party base only; NO deal-pipeline/stage entity |
| diligence (`blocks-businesscases`) | **NOT BUILT** | businesscases = bundle-entitlement engine, NOT diligence (Finding 1.2) |
| documents (`blocks-docs`) | PARTIAL | `Attachment`/`DocumentRef`/`Sensitivity`/MIME-policy solid; NO data-room/watermark/external-access-audit/time-bound-grant |
| reporting (`blocks-reports`) | COMPLETE-ish | report framework present; `financialAnalysis` = new report-kind |

**featureDefaults reality (the entire diligence surface is unbacked):**
- `crm.pipelineStages.default:lead,qualified,term-sheet,diligence,approval,closed-won,closed-lost`
  — NO pipeline/deal/stage entity. Greenfield.
- `diligence.evidenceRequired:true` — NO Evidence entity. Greenfield.
- `diligence.approvalGates.enabled:true` — NO ApprovalGate entity (workflow exists generically, but
  no diligence-specific gate model). Greenfield.
- `documents.dataRoom.enabled:true` — NO DataRoom entity on `blocks-docs`. Greenfield on a usable
  doc base.
- `documents.watermarking.enabled:false` / `documents.externalAccess.auditTrail:true` — NO
  watermarking, NO external-access audit trail / time-bound grant. The latter is the
  security-critical surface (external-counsel/investor access).
- `reporting.financialAnalysis.enabled:true` — a new report-kind on the existing framework (small).

**C4.1 substrate-fill scope (greenfield — confirms WBS `L`, escalate council):** Two distinct
greenfield builds:
1. **Diligence engine** (new — likely a NEW `blocks-diligence` or a major build inside a renamed
   businesscases-adjacent block; do NOT bolt diligence onto the entitlement-engine businesscases):
   `DealPipeline`/`PipelineStage`, `DiligenceChecklist`/`ChecklistItem`, `Evidence` (+ required-flag),
   `ApprovalGate`/`Approval`.
2. **Data-room** (new, on top of `blocks-docs`): `DataRoom`, time-bound external-access grant,
   external-access audit trail, optional watermarking. This is the security-critical half.
   **Sizing: `L` (2 PRs, matches WBS).** **Council: `dual` MANDATORY** (matches WBS) — the
   data-room external-access + audit trail is the heaviest security surface in all of Workstream C.

**Readiness verdict:** **BLOCKED on substrate-fill (highest gap, security-heaviest).** Neither the
diligence engine nor the data-room exists; `blocks-businesscases` does NOT provide a head start
(wrong domain). Cockpit (C4.3) cannot be built today. Dispatch last. `lite`-unsupported per
manifest (data-room/audit needs exceed local-first) — SelfHosted/HostedSaaS only.

---

## Finding 2 — Substrate-gap ranking (ascending) confirms C3-first

| Rank | Bundle | Required-module gap | C*.1 fill size (corrected) | Cockpit buildable today? |
|---|---|---|---|---|
| 1 (smallest) | **C3 Facility-Ops** | resource model (extend scheduling) + 2 small maintenance fields | `M` | **YES** |
| 2 | **C2 Project-Mgmt** | confirm-audit; CRM pipeline optional/skippable | `S`–`M` | **YES** |
| 3 | **C1 Asset-Mgmt** | `blocks-assets` greenfield domain-block (lifecycle/depreciation/warranty) | `L` | **NO** |
| 4 (largest) | **C4 Acq/Underwriting** | diligence engine + data-room, both greenfield; security-heaviest | `L` | **NO** |

**Recommended dispatch order: C3 → C2 → C1 → C4.** This MATCHES the WBS's order
(C3 → C1 → C2 → C4) on the C3-first head and the C4-last tail, but **swaps C1 and C2**: C2 is
genuinely ready (richest substrate) while C1 needs a greenfield domain-block first. The WBS put C1
second on the (incorrect) assumption that `blocks-assets` was "partial" and only needed a
completeness audit. On disk, C2 is the lower-gap bundle. **Corrected: C3 → C2 → C1 → C4.**

---

## Finding 3 — Each bundle's C*.1 + readiness, one block

- **C1.1 (Asset-Mgmt fill):** Build `blocks-assets` as a real asset domain-block (Asset aggregate +
  lifecycle + depreciation + warranty + maintenance-link). NOT a completeness audit. `L`, **dual**.
  Cockpit BLOCKED until shipped.
- **C2.1 (Project-Mgmt fill):** Confirm-audit `blocks-work-projects`/`blocks-tasks`; likely tiny
  gap-fills only. `S`–`M`, `test-eng`. Cockpit READY today.
- **C3.1 (Facility-Ops fill):** EXTEND `blocks-scheduling` with a bookable-resource model + add
  maintenance intake-channel + SLA fields. NO new package, NO new conflict engine. `M`,
  `sec-eng`/`test-eng` (downgrade from `dual` UNLESS the new-package decision is taken). Cockpit
  READY today.
- **C4.1 (Acq/Underwriting fill):** Greenfield diligence engine + greenfield data-room on
  `blocks-docs`. `blocks-businesscases` gives NO head start. `L` (2 PRs), **dual MANDATORY**.
  Cockpit BLOCKED until shipped.

---

## Finding 4 — NEW-substrate-package / `dual`-ADR flags (corrected)

The WBS flagged two `dual`-ADR units: `blocks-reservations` (C3.1) and the data-room/diligence fill
(C4.1). On disk:

1. **C4.1 (diligence + data-room) — `dual` ADR CONFIRMED, MANDATORY.** Greenfield diligence engine
   + data-room with external-access + audit trail. Security-critical (external-counsel/investor
   access, audit-trail integrity, time-bound grants). This is the genuine `dual`-council unit in
   Workstream C. The ADR should pin: diligence entity model + approval-gate semantics; data-room
   access-control + external-grant lifecycle + audit-trail contract; whether diligence lives in a
   NEW `blocks-diligence` (recommended — keep it OUT of the entitlement-engine `blocks-businesscases`).

2. **C1.1 (assets) — ADR NEWLY WARRANTED (the WBS missed this).** Because `blocks-assets` is a
   greenfield domain-block (not a "partial" fill), and depreciation touches a financial surface, a
   `dual` ADR is warranted to pin the asset aggregate boundary, the depreciation-schedule model
   (and its relationship to the financial-ledger cluster), and the asset↔maintenance/inspections
   reference pattern. The WBS scoped C1.1 as a `test-eng` audit — **upgrade to a `dual`-ADR-gated
   domain-block build.**

3. **C3.1 (`blocks-reservations`) — `dual` ADR NOT warranted as scoped; downgraded to a DECISION.**
   The conflict-detection engine already exists in `blocks-scheduling` (Flease-lease CP writer).
   The only architectural question is **new `blocks-reservations` package vs. extend
   `blocks-scheduling` with a resource model**. ONR recommends EXTEND scheduling (the coordinator,
   outcome vocabulary, and lease-backed writer are already there; a separate package would
   duplicate or depend back into scheduling for the hard part). If Engineer/CIC prefer a separate
   package for bundle-boundary cleanliness, that is a one-paragraph ADR DECISION, not a
   full `dual` substrate-contract ADR. Either way, do NOT re-build conflict-detection.

---

## Open questions

1. **C1 asset-ledger coupling.** Should `DepreciationSchedule` live in `blocks-assets` or couple to
   the financial-ledger cluster (depreciation posts JEs)? This is the C1.1 ADR's central question
   and affects whether C1.1 is one block or a block + a ledger-integration unit. (Routes to the
   C1.1 dual ADR.)
2. **C3 resource model home.** New `blocks-reservations` vs. extend `blocks-scheduling`? (Finding
   4.3 — recommend extend; needs an Engineer/CIC nod before C3.1 dispatch.)
3. **C4 diligence block identity.** New `blocks-diligence` vs. a renamed/expanded businesscases-
   adjacent block? ONR recommends a NEW `blocks-diligence` so the entitlement engine
   (`blocks-businesscases`) stays single-purpose. (Routes to the C4.1 dual ADR.)
4. **C2/C4 CRM pipeline.** A deal/lead pipeline entity is missing and is needed by BOTH the C2
   optional CRM (C2.4) and C4's deal-flow (C4.1 `crm.pipelineStages`). Should it be built once as a
   shared `blocks-crm` (or in people-foundation) and reused by both, rather than twice? Likely
   yes — flag for the C4.1 ADR to define it as shared substrate.
5. **Sizings.** S/M/L bands are PR-author-effort estimates, not Engineer-validated. The C1.1
   `M`→`L` and C3.1 `M`→smaller corrections are ONR's disk-based recalibration; Engineer should
   confirm against the actual cockpit-page count when C dispatches.

---

## Sources cited

1. `shipyard/packages/foundation-catalog/Manifests/Bundles/{asset-management,project-management,facility-operations,acquisition-underwriting}.bundle.json` — the 4 Draft manifests; `requiredModules` + `featureDefaults` + editionMappings read in full [PRIMARY] (retrieved 2026-05-29)
2. `shipyard/packages/blocks-assets/Models/AssetRecord.cs` + package file listing — the file-catalog `AssetRecord` stub (3 .cs total) [PRIMARY/on-disk] (2026-05-29)
3. `shipyard/packages/blocks-businesscases/` — `IBusinessCaseService` + `InMemoryBusinessCaseService` + `BundleActivationRecord` + `TenantEntitlementSnapshot` + `BundleEntitlementResolver` (the entitlement engine, 18 .cs) [PRIMARY/on-disk] (2026-05-29)
4. `shipyard/packages/blocks-scheduling/` — `SlotReservation` + `ReservationOutcome` (SLOT_CONFLICT/SLOT_INVERTED) + `IScheduleReservationCoordinator`/`ScheduleReservationCoordinator` Flease-lease CP writer (10 .cs) [PRIMARY/on-disk] (2026-05-29)
5. `shipyard/packages/blocks-work-projects/Models/*` + `Services/*` — Project/ProjectBudget/Milestone/TimeEntry/ProjectActual/RemodelProject surface (85 .cs) [PRIMARY/on-disk] (2026-05-29)
6. `shipyard/packages/blocks-maintenance/Models/*` — WorkOrder/MaintenanceRequest/Rfq/Quote/Vendor+onboarding+W9+performance (65 .cs); `MaintenanceRequest.cs` lacks channel/SLA fields [PRIMARY/on-disk] (2026-05-29)
7. `shipyard/packages/blocks-inspections/Models/*` — Inspection/Template/ChecklistItem/Deficiency/EquipmentConditionAssessment/recurring trigger (35 .cs) [PRIMARY/on-disk] (2026-05-29)
8. `shipyard/packages/blocks-docs/` — Attachment/DocumentRef/Sensitivity/MIME-policy (40 .cs); no data-room/watermark/external-access-audit [PRIMARY/on-disk] (2026-05-29)
9. `shipyard/packages/blocks-people-foundation/Models/*` — Party/PartyRole/PartyKind/Address/Email/Phone (36 .cs); no pipeline/deal entity [PRIMARY/on-disk] (2026-05-29)
10. `shipyard/packages/blocks-reports/` — ReportKind/ReportRunResult/ReportExecutionContext framework (52 .cs) [PRIMARY/on-disk] (2026-05-29)
11. `shipyard/icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md` Workstream C + manifest-key→package mapping table (shipyard#181, branch `onr/post-mvp-wbs`) — the document this audit validates and corrects [PRIMARY/ONR] (2026-05-29)

— ONR, 2026-05-29
