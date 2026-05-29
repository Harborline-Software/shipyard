---
id: 101
title: Asset-Management Bundle Substrate
status: Proposed
date: 2026-05-29
proposed-date: 2026-05-29
author: Engineer (Workstream C1)
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - asset-lifecycle-tracking
  - depreciation
  - warranty-management
  - multi-tenant-isolation
  - block-naming-generalization
  - bundle-activation
  - cross-block-fk-integrity

enables:
  - c1-1-asset-substrate-build
  - c1-2-asset-bridge-endpoints
  - c1-3-asset-cockpit-react-pages
  - c1-4-asset-management-bundle-activation
  - asset-management-bundle-active

composes:
  - 7    # Bundle Manifest Schema (asset-management.bundle.json conforms; featureDefaults are the feature-key contract this ADR satisfies)
  - 8    # Foundation.MultiTenancy (every asset entity is IMustHaveTenant; persistence adapters default-reject TenantId.System)
  - 11   # Bundle Versioning + Upgrade Policy (the Draft->Active activation gate in C1.4)
  - 49   # Audit-Trail Substrate (asset lifecycle events are append-only; Sunfish.Kernel.Audit is the eventual emission target, in-memory first-slice)
  - 53   # Work Order Domain Model (WorkOrder.Equipment FK is the existing asset<->maintenance link; "Asset" was renamed Equipment per UPF Rule 4)
  - 91   # ITenantContext Divergence Resolution (consumption picks the Authorization sum-interface facade, NOT the MultiTenancy narrowed variant; signal-bridge#34 trap)
  - 98   # Block-Naming Generalization (the substrate-tier precedent for promoting/renaming a property-scoped block into a domain-general one; THIS ADR is a fresh instance of that decision)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-defining ADR; ADR text carries MANDATORY dual-council before Accept. The C1.2 Bridge endpoint + C1.3 cockpit PAIR additionally carries pattern-009 sec-eng SPOT-CHECK.
---

# ADR 0101 — Asset-Management Bundle Substrate

**Status:** Proposed — Rev 1. P3 future-roadmap (post-MVP). Substrate-defining: this ADR pins the
domain model the asset-management reference bundle's cockpit + Bridge endpoints will build on, and
decides **where that domain lives** (greenfield `blocks-assets` vs. promotion of the existing
`blocks-property-equipment` Equipment domain). Dual-council MANDATORY (security-engineering +
.NET-architect) on this ADR text BEFORE Accept, per the substrate-tier Halt cadence established by
ADR 0095/0096/0097/0098/0099/0100. Do NOT self-accept.

**Date:** 2026-05-29

**Resolves:** The asset-management bundle (`asset-management.bundle.json`, `status: Draft`,
`maturity: Scaffold`) declares `featureDefaults` for `assets.lifecycle.tracking.enabled`,
`assets.depreciation.autoCalculate`, and `assets.warrantyReminders.enabled` — but there is **no
asset domain substrate that satisfies those feature keys.** The post-MVP WBS register frames
Workstream C1.1 as a simple "gap-fill" against a `blocks-assets` package it labels *partial*. That
framing is wrong (see §"Context — the stub-vs-partial finding"); `blocks-assets` is a stub, and the
real physical-asset domain already exists in a *property-scoped* block. This ADR resolves the
resulting design fork before any C1 build begins.

**Predecessor WBS:** `shipyard/icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md` (ONR;
shipyard#181 Workstream C, units C1.1–C1.4). **Its C1.1 "partial / gap-fill" framing is corrected
by this ADR.**

---

## A0 cited-symbol audit

Per the ADR 0093/0096/0097/0099/0100 cited-symbol audit discipline. Classifications:
**Existing & verified** (on `shipyard/main` at authoring — origin/main @ a0cc757, path-checked);
**Introduced by this ADR** (ships in a C1.1+ build PR after Accept).

| Symbol / Path | Classification | Verified |
|---|---|---|
| `Sunfish.Blocks.Assets.Models.AssetRecord` (`Id, Name, Path, SizeBytes, LastModifiedUtc`) | **Existing — STUB.** A UI-only file-catalog DTO. NOT `IMustHaveTenant`, no lifecycle, no depreciation, no warranty, no repository, no DI. The package is a read-display catalog surface only. | yes — `packages/blocks-assets/Models/AssetRecord.cs` (10 lines) |
| `Sunfish.Blocks.Assets.AssetCatalogBlock` (`.razor`; `Items`, `ShowFileManager`) | **Existing — STUB.** Composes `SunfishDataGrid` + `SunfishFileManager`; read-display only. Note: param named `Items` not `Assets` to dodge `ComponentBase.Assets` (.NET 10) collision. | yes — `packages/blocks-assets/AssetCatalogBlock.razor` |
| `Sunfish.Blocks.PropertyEquipment.Models.Equipment` (`Id, TenantId, Property (required PropertyId), Class, DisplayName, Make, Model, SerialNumber, InstalledAt, AcquisitionCost, AcquisitionReceiptRef, ExpectedUsefulLifeYears, Warranty, VehicleData, CreatedAt, DisposedAt, DisposalReason`) | **Existing — the REAL asset domain.** `IMustHaveTenant`. Has acquisition cost basis + useful-life (depreciation inputs), `WarrantyMetadata`, soft-delete disposition. Hard-coupled to `Property` via a **required** FK — no orphan equipment. | yes — `packages/blocks-property-equipment/Models/Equipment.cs` |
| `Sunfish.Blocks.PropertyEquipment.Models.EquipmentLifecycleEvent` (`EventId, Equipment, Property, TenantId, EventType, OccurredAt, RecordedBy, Notes, Metadata`) | **Existing.** Append-only lifecycle history. Carries a `PropertyId` snapshot. | yes — `packages/blocks-property-equipment/Models/EquipmentLifecycleEvent.cs` |
| `Sunfish.Blocks.PropertyEquipment.Models.EquipmentLifecycleEventType` (enum: `Installed, Serviced, Inspected, WarrantyClaimed, Replaced, Disposed, PhotoAdded, NotesUpdated, MileageRecorded`) | **Existing.** Closed set; lifecycle transition discriminator. | yes — `packages/blocks-property-equipment/Models/EquipmentLifecycleEventType.cs` |
| `Sunfish.Blocks.PropertyEquipment.Models.WarrantyMetadata` (`StartsAt, ExpiresAt, Provider, PolicyNumber, CoverageNotes`) | **Existing.** Embedded value object on `Equipment`. Satisfies the bones of `assets.warrantyReminders`. | yes — `packages/blocks-property-equipment/Models/WarrantyMetadata.cs` |
| `Sunfish.Blocks.PropertyEquipment.Models.EquipmentClass` (enum: `WaterHeater, HVAC, Appliance, Roof, Vehicle, Plumbing, Electrical, Other`) | **Existing.** Property-flavored class enum. Asset-mgmt personas (fleet, manufacturing, IT hardware) need a wider/registry-backed class set — see Decision D2. | yes — `packages/blocks-property-equipment/Models/EquipmentClass.cs` |
| `Sunfish.Blocks.PropertyEquipment.Services.IEquipmentRepository` (`GetByIdAsync, ListByPropertyAsync, ListByTenantAsync, ListByClassAsync, UpsertAsync, SoftDeleteAsync` — all tenant-scoped) | **Existing.** Tenant-scoping mandatory on every call; never returns cross-tenant rows. Reference repository contract for the asset substrate. | yes — `packages/blocks-property-equipment/Services/IEquipmentRepository.cs` |
| `Sunfish.Blocks.Maintenance.Models.WorkOrder.Equipment` (`EquipmentId?`) | **Existing.** The asset<->maintenance link. xmldoc: "FK to the physical equipment this work targets … renamed from 'Asset' per UPF Rule 4." The maintenance domain already treats Equipment as the asset. | yes — `packages/blocks-maintenance/Models/WorkOrder.cs:78` |
| `Sunfish.Foundation.MultiTenancy.IMustHaveTenant` + `Sunfish.Foundation.Authorization.ITenantContext` (sum-interface facade) | **Existing.** Every asset entity MUST implement `IMustHaveTenant`; the Bridge/load path consumes the Authorization **facade** (NOT `MultiTenancy.ITenantContext`) per ADR 0091 R2 (signal-bridge#34 CS0104 trap). | yes — `packages/foundation-authorization/`, `packages/foundation-multitenancy/` |
| `Sunfish.Foundation.Assets.*` (Entities / Versions / Hierarchy / Audit / Common) | **Existing — UNRELATED namespace; NAME-COLLISION HAZARD.** This `Assets` is the local-first **data-substrate** (entity store, version store, CRDT hierarchy, hash-chained audit). It is NOT physical-asset management. Any new physical-asset code MUST NOT live under `Sunfish.Foundation.Assets.*`; `Sunfish.Blocks.Assets` is the only "asset" surface that means *physical assets*. | yes — `packages/foundation/Assets/**` |
| `Sunfish.Foundation.Integrations.Payments.Money` | **Existing.** The typed money value object (ADR 0051). `Equipment.AcquisitionCost` is still raw `decimal?` with a `TODO: Money` migration note; the asset substrate should adopt `Money` from the start. | yes — referenced by `WorkOrder.EstimatedCost` |
| `DepreciationSchedule`, `DepreciationMethod`, `WarrantyTerm`, `AssetCategory`, `IAssetRepository`, `IAssetLifecycleEventStore` | **Introduced by this ADR** (ship in C1.1 after Accept). New asset domain types. | n/a — proposed |
| `GET/POST/PUT /api/v1/assets`, `/api/v1/assets/{id}`, `/api/v1/assets/{id}/lifecycle` | **Introduced by this ADR** (ship in C1.2 after Accept). | n/a — proposed |

---

## Context

### The stub-vs-partial finding (empirical, resolved before designing)

The post-MVP WBS register (Workstream C, C1.1) describes `blocks-assets` as **"partial"** and frames
C1.1 as filling "missing asset-lifecycle + warranty entities." **This is incorrect.** An empirical
survey of `shipyard/packages/blocks-assets/` (origin/main @ a0cc757) found the package is a **STUB**:

- **3 source files, ~37 lines of C# total** (excluding tests + localization shim):
  - `Models/AssetRecord.cs` — a **read-display file-catalog DTO** with `Id`, `Name`, `Path`,
    `SizeBytes`, `LastModifiedUtc`. It is **not** `IMustHaveTenant`. It has no lifecycle, no
    depreciation, no warranty, no acquisition cost, no disposition.
  - `AssetCatalogBlock.razor` — a UI surface composing `SunfishDataGrid` + `SunfishFileManager`;
    "read-display only — upload, transform, tag, version are all deferred" (per its own comment).
  - `Localization/SharedResource.cs` — i18n shim.
- **No domain services, no repository, no persistence, no DI registration.** The package's own
  README states: "UI-only catalog block — no domain-services or persistence."

So `blocks-assets` is a **file/photo browser**, not an asset-management domain. There are **zero**
asset-lifecycle, depreciation, or warranty entities to "gap-fill." This is why an **ADR (not a
WBS gap-fill ticket) is the correct vehicle**: the substrate must be designed greenfield, and the
package's *name* (`blocks-assets`) already means something else (a file catalog).

### The real asset domain already exists — but it is property-scoped

The physical-asset domain the asset-management bundle needs **already exists** — in
`blocks-property-equipment`. Its `Equipment` record carries everything the bundle's `featureDefaults`
imply:

| Bundle feature key | Existing `Equipment` support |
|---|---|
| `assets.lifecycle.tracking.enabled` | `EquipmentLifecycleEvent` (append-only) + `EquipmentLifecycleEventType` (9-state enum) + `InstalledAt` / `DisposedAt` / `DisposalReason` |
| `assets.depreciation.autoCalculate` | `AcquisitionCost` (cost basis) + `ExpectedUsefulLifeYears` (useful life) — the *inputs* to a depreciation calc, but no `DepreciationSchedule` / `DepreciationMethod` entity yet |
| `assets.warrantyReminders.enabled` | `WarrantyMetadata` (`StartsAt`/`ExpiresAt`/`Provider`/`PolicyNumber`) embedded on `Equipment` — the data, but no reminder-scheduling service |

And it is **already wired to maintenance**: `WorkOrder.Equipment` is an `EquipmentId?` FK whose
xmldoc records that the field was "renamed from 'Asset' per UPF Rule 4." The maintenance domain
already treats `Equipment` *as* the asset.

**The catch:** `Equipment.Property` is a **required, non-nullable `PropertyId` FK** — "there are no
orphan equipment." That is correct for property management, but the asset-management bundle's
personas are **fleet, manufacturing equipment, facility assets, IT hardware** (per the manifest's
`seedWorkspaces` + `description`). A fleet vehicle or a CNC machine does **not** belong to a
`Property`. So the existing domain cannot be reused as-is for asset-management.

### Two name-collision hazards (both confirmed)

1. **`Sunfish.Foundation.Assets.*`** is the local-first **data substrate** (entity store, version
   store, CRDT hierarchy, hash-chained audit) — NOT physical-asset management. Any new
   physical-asset domain code MUST NOT land under that namespace.
2. **`ComponentBase.Assets`** (.NET 10) collides with a Blazor parameter named `Assets` — which is
   why `AssetCatalogBlock` already uses `Items`. Cockpit components must avoid the bare `Assets`
   parameter name.

---

## Decision

**Build a greenfield, property-agnostic asset domain in `blocks-assets`, by generalizing the proven
shape of the existing `blocks-property-equipment` domain — NOT by adding a parallel asset model from
scratch, and NOT by overloading the property-scoped `Equipment` record.**

Concretely:

**D1 — Greenfield asset domain in `blocks-assets`, the stub repurposed.** The asset-management
domain lives in `Sunfish.Blocks.Assets` (the package that already *means* "assets"). The existing
file-catalog `AssetRecord` + `AssetCatalogBlock` are retained as-is (they are a UI catalog surface,
orthogonal to the domain) but the package gains a real domain alongside them. The new domain mirrors
the `blocks-property-equipment` conventions: strongly-typed opaque IDs (`AssetId` as a
`readonly record struct` with `NewId()` + JSON converter, per `EquipmentId`), `IMustHaveTenant`
records, append-only lifecycle events, an in-memory-first repository with mandatory tenant scoping.

**D2 — The asset domain model (tier-1 domain-blocks).** Per the three-tier slotting vocabulary, the
asset lifecycle/depreciation/warranty entities are **tier-1 `domain-block`** types (concrete DI,
never vendor-swapped). The first-slice domain:

- **`Asset` : `IMustHaveTenant`** — `Id` (`AssetId`), `TenantId`, `Category` (`AssetCategory`),
  `DisplayName`, `Make`/`Model`/`SerialNumber`, `AcquiredAt`, `AcquisitionCost` (`Money`, NOT raw
  `decimal` — adopt ADR 0051 from the start, learning from the `Equipment` `TODO: Money` debt),
  `AcquisitionReceiptRef`, `ExpectedUsefulLifeYears`, `LifecycleState`, `Warranty`
  (`WarrantyTerm?`), `Location` (free-text — NOT a `PropertyId`; see D3), `Notes`,
  `PrimaryPhotoBlobRef`, `CreatedAt`, `DisposedAt`, `DisposalReason`. Soft-delete via `DisposedAt`.
- **`AssetCategory`** — a registry-backed category (fleet-vehicle, manufacturing-equipment,
  facility-asset, IT-hardware, …) replacing the property-flavored fixed `EquipmentClass` enum.
  First-slice may ship a closed enum + a `TODO` to back it with the schema registry (mirrors the
  `EquipmentClass` OQ-A2 deferral); a registry-backed category is the cleaner long-term path.
- **`AssetLifecycleEvent` : `IMustHaveTenant`** — append-only, `EventId`, `Asset` (`AssetId`),
  `TenantId`, `EventType`, `OccurredAt`, `RecordedBy`, `Notes`, `Metadata`. Mirrors
  `EquipmentLifecycleEvent`. `AssetLifecycleEventType`: `Acquired, Deployed, Serviced, Inspected,
  WarrantyClaimed, Depreciated, Transferred, Replaced, Disposed, PhotoAdded, NotesUpdated`.
- **`LifecycleState`** — the asset's current lifecycle status (e.g. `Draft, Active, InMaintenance,
  Retired, Disposed`); transitions are recorded as `AssetLifecycleEvent`s.
- **`DepreciationSchedule` + `DepreciationMethod`** — `DepreciationMethod` enum (`StraightLine,
  DecliningBalance, UnitsOfProduction, None`); `DepreciationSchedule` value object
  (`Method, SalvageValue (Money), StartDate, UsefulLifeYears, accumulated/periodic computed`).
  Satisfies `assets.depreciation.autoCalculate` (defaults to `false` per the manifest — the schedule
  exists but auto-calc is opt-in). Pure computation; no external provider.
- **`WarrantyTerm`** — value object mirroring `WarrantyMetadata`
  (`StartsAt, ExpiresAt, Provider, PolicyNumber, CoverageNotes`); the basis for the
  `assets.warrantyReminders` reminder service (reminder *scheduling* itself is a follow-up slice — a
  query "warranties expiring within N days" suffices for first-slice).
- **`IAssetRepository` + `IAssetLifecycleEventStore`** — tenant-scoped on every call, mirroring
  `IEquipmentRepository` (`GetByIdAsync, ListByTenantAsync, ListByCategoryAsync, UpsertAsync,
  SoftDeleteAsync`). In-memory first-slice; persistence adapter follows.

**D3 — Asset is property-agnostic; relationship to property-equipment is a non-required link, not
inheritance.** `Asset` has a free-text `Location`, NOT a required `PropertyId` (a fleet vehicle has
no property). The asset-management bundle and the property bundle are siblings: an operator who
*also* manages properties can link an `Asset` to a `Property`/`Equipment` via an **optional**
correlation reference, but the asset domain does not depend on `blocks-properties` or
`blocks-property-equipment`. **`blocks-assets` MUST NOT take a project/package reference on
`blocks-property-equipment`** (avoid a property-coupling on a property-agnostic block).

**D4 — Maintenance link: extend `WorkOrder`, do not fork it.** Maintenance work today targets
`WorkOrder.Equipment` (`EquipmentId?`). For asset-management, a work order may target an `Asset`
that is not property `Equipment`. Resolution: add an **optional** `WorkOrder.Asset` (`AssetId?`)
alongside the existing `Equipment` FK (both nullable; at most one set in practice), OR introduce a
unifying `MaintainableRef`. **This is an OPEN QUESTION for .NET-architect council** (OQ-2) — it
touches the shipped `blocks-maintenance.WorkOrder` contract and must not be decided unilaterally.

**D5 — Bundle activation is the last step, gated on cockpit + endpoints.** `asset-management.bundle.json`
flips `status: Draft` -> `Active` (and `maturity: Scaffold` -> a higher tier) only in C1.4, after the
substrate (C1.1), Bridge endpoints (C1.2), and cockpit (C1.3) land. C1.4 also reconciles the
manifest's aspirational module keys (`sunfish.blocks.contacts`, `…communications`, etc.) to the
shipped block names per ADR 0098.

### The C1.1 -> C1.4 build ladder (implementation plan)

This ADR is the **C1-ADR**; it ratifies the contract the four build units inherit. Build order is
strictly sequential (each gates the next):

| Unit | Scope | Repo/layer | Gate (PASS/FAIL) | Council |
|---|---|---|---|---|
| **C1.1** | **Substrate (greenfield).** Build the D2 asset domain in `blocks-assets`: `Asset`, `AssetCategory`, `AssetId`, `AssetLifecycleEvent(+Type)`, `LifecycleState`, `DepreciationSchedule(+Method)`, `WarrantyTerm`, `IAssetRepository`, `IAssetLifecycleEventStore`, in-memory impls, DI extension. All `IMustHaveTenant`; tenant-scoped repos; `Money` for cost basis. | shipyard `blocks-assets` | PASS = domain + in-memory repos + DI compile & test-green; every entity `IMustHaveTenant`; repos reject `TenantId.System`; no dependency on `blocks-property-equipment`. | test-eng (substrate); **.NET-architect (this ADR)** |
| **C1.2** | **Asset Bridge endpoints.** `GET/POST/PUT /api/v1/assets`, `/assets/{id}`, `POST /assets/{id}/lifecycle` (lifecycle transition). Tenant scope resolved from the **Authorization facade `ITenantContext`** (ADR 0091 R2; NOT the MultiTenancy variant — signal-bridge#34 trap). | signal-bridge | PASS = endpoints reject requests without a resolved tenant; load path filters by `ITenantContext.TenantId`; no cross-tenant read possible. | **security-engineering (tenant-scope on load/endpoint path) — MANDATORY** |
| **C1.3** | **Asset cockpit React pages.** Asset list, asset detail, lifecycle timeline, warranty-expiry view, maintenance-link panel. 2–3 PRs (list+detail, lifecycle, warranty). Avoid the bare `Assets` component param (.NET 10 `ComponentBase.Assets` collision — use `Items`/`AssetItems`). | sunfish web | PASS = pages bind only to the C1.2 endpoints; no direct DB/tenant access in the client; pattern-009 PAIR with C1.2. | **pattern-009 PAIR** (security-engineering SPOT-CHECK on the Bridge+frontend pair) |
| **C1.4** | **Bundle activation.** `asset-management.bundle.json` `Draft`->`Active`; reconcile aspirational module keys to shipped block names (ADR 0098); bump `maturity`. | shipyard `foundation-catalog` | PASS = manifest validates against the ADR 0007 schema; every `requiredModules` key maps to a shipped package; no Draft-only feature key left unsatisfied. | none |

---

## Consequences

**Positive:**

- The asset-management bundle gets a domain that actually satisfies its `featureDefaults`, designed
  greenfield instead of bolted onto a property-scoped block.
- Reuses the *proven shape* of `blocks-property-equipment` (tenant-keying, lifecycle events, warranty
  value object, soft-delete, strongly-typed IDs) without inheriting its property coupling — low
  design risk, high consistency.
- Adopts `Money` (ADR 0051) for cost basis from day one, avoiding the `decimal? -> Money` migration
  debt that `Equipment.AcquisitionCost` still carries.
- Keeps `blocks-assets` dependency-light (no `blocks-property-equipment` / `blocks-properties` ref),
  so asset-management deploys cleanly without the property cluster.

**Negative / costs:**

- **Two physical-asset domains coexist** (`Equipment` for property-scoped, `Asset` for general).
  This is a deliberate near-term duplication. A future ADR could unify them (promote `Asset` to a
  domain-general core that `Equipment` specializes) — but that is a larger refactor touching the
  shipped property + maintenance clusters and is explicitly **out of scope** here. Flagged as a
  revisit condition.
- The `WorkOrder.Asset` vs. `WorkOrder.Equipment` link (D4) modifies a shipped maintenance contract;
  resolution deferred to council (OQ-2).
- The `blocks-assets` package now carries both a file-catalog surface and an asset domain — a mild
  cohesion smell. Acceptable: the catalog is a thin UI block; the domain is the substantive content.

**Rollback / revisit:**

- Rollback = git revert of the C1.1 substrate PR (no migration runs until a persistence adapter +
  bundle activation land; the in-memory first-slice is side-effect-free).
- **Revisit if:** (a) CIC rules `Asset`/`Equipment` should be unified into one core before C1
  build; (b) the property-equipment cluster needs the same property-agnostic generalization (then a
  shared `Asset` core ADR supersedes the per-block duplication); (c) the depreciation feature needs
  a tax-jurisdiction provider (would promote depreciation method selection to a tier-2
  category-provider).
- **Kill trigger:** if dual-council returns RED on the property-agnostic-domain decision, do not
  build C1.1; re-author at Rev 2.

---

## Council review (MANDATORY before Accept)

Per the substrate-tier Halt cadence (ADR 0095/0096/0097/0098/0099/0100), this ADR carries
**dual-council MANDATORY** before flipping to Accepted:

- **security-engineering** — tenant-scope correctness on the asset load path + the C1.2 Bridge
  endpoints (`/api/v1/assets`): every read/write MUST be tenant-filtered via the Authorization
  facade `ITenantContext` (ADR 0091 R2; the `MultiTenancy.ITenantContext` narrowed-variant trap from
  signal-bridge#34 is the specific miss to guard against), and the repos MUST reject
  `TenantId.System`/default. The C1.2+C1.3 pattern-009 PAIR additionally carries the standing
  sec-eng SPOT-CHECK on PR-open.
- **.NET-architect** — the substrate design: the greenfield-vs-promotion decision (D1/D3), the
  domain model shape (D2), and the `WorkOrder` link resolution (OQ-2 / D4).

Both councils write a `council-verdict-*` beacon. RED on either blocks Accept; dual-AMBER folds into
a Rev 2 per the ADR 0069 / Halt-9 precedent.

---

## Open questions for Admiral / CIC ruling

- **OQ-1 (design, for .NET-architect + CIC):** Greenfield `Asset` domain in `blocks-assets` (this
  ADR's D1) **vs.** promoting `blocks-property-equipment`'s `Equipment` into a property-agnostic
  `Asset` core that `Equipment` then specializes. This ADR recommends greenfield-now + unify-later
  (lower blast radius on the shipped property + maintenance clusters). CIC may prefer paying the
  unification cost up front to avoid two coexisting asset domains. **This is the single
  highest-leverage decision in the ADR.**
- **OQ-2 (contract, for .NET-architect):** The maintenance link (D4). Add an optional
  `WorkOrder.Asset` (`AssetId?`) alongside `WorkOrder.Equipment`, or introduce a unifying
  `MaintainableRef`? Touches the shipped `blocks-maintenance.WorkOrder` contract.
- **OQ-3 (scope):** `AssetCategory` — ship a closed enum first-slice (fast) or back it with the
  schema registry immediately (cleaner, per the fleet "prefer cleanest long-term option" directive)?
  Leaning registry-backed given P3 quality-over-speed posture.

---

## References

- `packages/foundation-catalog/Manifests/Bundles/asset-management.bundle.json` — the bundle this ADR serves.
- `packages/blocks-assets/` — the stub being repurposed (`AssetRecord`, `AssetCatalogBlock`).
- `packages/blocks-property-equipment/` — the reference domain (`Equipment`, lifecycle events, warranty).
- `packages/blocks-maintenance/Models/WorkOrder.cs` — the existing asset<->maintenance FK (`Equipment`).
- `icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md` — Workstream C1 (the "partial" framing this ADR corrects).
- ADR 0007 (Bundle Manifest Schema), 0011 (Bundle Versioning), 0049 (Audit-Trail Substrate), 0051 (Money), 0053 (Work Order Domain Model), 0091 (ITenantContext Divergence), 0098 (Block-Naming Generalization).
