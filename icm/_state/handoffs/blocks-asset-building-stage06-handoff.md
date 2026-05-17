# Hand-off — `blocks-asset-building` First Layer-2 Kind Extension over the Generic Asset Substrate

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` *(see Gate Conditions below — this hand-off ships AFTER `blocks-asset-foundation` substrate ships in its own workstream; the gate is documented + the dependency is explicit)*
**Workstream:** TBD W#75 *(provisional — flag for XO registration in `active-workstreams.md` via `W75.md` source file)*
**Spec source:**
- [ADR 0094](../../../docs/adrs/0094-generic-asset-polymorphism-overlays.md) — Generic Asset Polymorphism + Domain-Specific Overlays §Layer 1 + §Layer 2 + §Per-record-class CP/AP classification + OQ catalog
- [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §1 (7-cluster grouping; `blocks-property-*` Phase 1 + the implicit `blocks-asset-*` substrate-cluster role inside it) — Proposed; CO-ratified 2026-05-16
- [`_shared/engineering/party-model-convention.md`](../../../_shared/engineering/party-model-convention.md) §3 (PartyRole registry), §4 (cross-cluster contracts — financial-AR / property / work consume AssetParty)
- [`icm/02_architecture/path-ii-crdt-schema-conventions.md`](../../02_architecture/path-ii-crdt-schema-conventions.md) §1 (ULID), §2 (soft-delete), §3 (version + revisionVector), §4 (append-only sub-collections), §5 (stable codes), §7 (state-machine resolution)
- `packages/foundation/Assets/README.md` — kernel primitives consumed by Layer 1 (`IEntityStore`, `IVersionStore`, `IAuditLog`, `IHierarchyService`)
- `packages/blocks-properties/Models/Property.cs` + `packages/blocks-property-equipment/Models/Equipment.cs` — current entity surfaces that this hand-off will eventually wrap (Phase 6 migration; NOT in scope here)
**ADR:** ADR 0094 (Proposed; status flip pending CO ratification of OQ-1 through OQ-12; this hand-off may proceed at `ready-to-build` per CO directive even with ADR `Proposed` — see Gate Conditions)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~8–10h sunfish-PM (4–5 feature PRs + ~38–45 tests + docs + attribution + ERPNext / legacy importer)
**PR count:** 5 PRs
**Pre-merge council:** NOT required (substrate-derivative scope; mirrors the W#34/W#35/W#36/W#60-P4 substrate-only pattern). Standard COB self-audit applies. **EXCEPTION:** if PR 1 introduces any field name on the canonical `Asset` row that the (already-shipped) `blocks-asset-foundation` substrate does NOT carry (i.e., this hand-off accidentally extends the substrate), **halt + council-review** before continuing. The Layer-2 contract: kind-specific data lives in the side-table; the canonical Asset row is untouched.

**Audit before build:**

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-asset-"
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-asset-foundation 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-asset-building 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-assets/Models/AssetRecord.cs 2>&1
```

Expected at this hand-off's start:

- `packages/blocks-asset-foundation/` exists (substrate-first workstream W#74 shipped first; see Gate Conditions H1).
- `packages/blocks-asset-building/` does NOT exist (greenfield package — verified 2026-05-17).
- `packages/blocks-assets/` exists (UI-only catalog; coexists, different responsibility) — DO NOT TOUCH.
- `packages/blocks-properties/` exists (legacy Property entity — Phase 6 migration target; DO NOT TOUCH in this hand-off).
- `packages/blocks-property-equipment/` exists (legacy Equipment entity — out of scope here).

**Naming-collision audit results (verified 2026-05-17):**

| Candidate name | Status |
|---|---|
| `blocks-asset-building` | GREENFIELD — no collision; ships under this hand-off. |
| `blocks-asset-foundation` (the substrate; sibling workstream) | GREENFIELD — no `blocks-asset-foundation` exists; the Substrate hand-off (W#74) authorises the name. ADR 0094 OQ-9 ratifies. |
| `blocks-assets` (UI-only catalog) | EXISTS — UI-only AssetCatalogBlock; coexists; documented in ADR 0094 §Context #4. |
| `Sunfish.Foundation.Assets` (kernel primitives) | EXISTS — foundation tier; composed by Layer 1; tier-prefix disambiguates. |
| `blocks-properties.Property` (legacy entity) | EXISTS — Phase 6 wrapper migration target; UNCHANGED in this hand-off. |

---

## Context

### Sequencing: substrate-first, layer-2 second

ADR 0094 OQ-1 establishes the substrate-first sequencing recommendation: `blocks-asset-foundation` ships in its own workstream (provisional W#74) BEFORE this hand-off (provisional W#75). This hand-off **assumes the substrate is built**. If COB picks up this hand-off and the substrate is NOT built, the Pre-build checklist catches it (step 1) and COB halts via a `cob-question-*` beacon.

**Critical sequencing recommendation (binding):** Sunfish has shipped 6+ substrate-first cluster pairs (`foundation-wayfinder` → W#43 consumers; `blocks-financial-ledger` → `blocks-financial-ar`; `blocks-people-foundation` → consumer clusters; `foundation-mission-space` → its overlays; etc.). Every pair shipped the substrate first. Bundling Layer 1 into a Layer 2 hand-off would inflate the PR count, couple substrate amendments to building-specific churn, and block parallel Layer-2 work (`-unit`, `-equipment`, `-vehicle`, `-it`, etc.) from starting until the bundled hand-off merges. **XO recommends the separate-workstream approach; CO ratification of OQ-1 closes this.**

### Cluster placement

Per ADR 0088 §1 + ADR 0094 §Layer 2: the `blocks-asset-*` cluster is *inside* the broader property-operations cluster (Phase 1 per ADR 0088). The `-asset-*` prefix marks Layer-2 kind-extensions; the `-property-*` prefix marks Layer-3 domain-composition overlays. Both naming families coexist:

- `blocks-asset-foundation` — Layer 1 substrate (kind-agnostic Asset surface)
- `blocks-asset-building` — Layer 2 kind extension (THIS hand-off)
- `blocks-asset-unit`, `blocks-asset-equipment`, `blocks-asset-vehicle`, `blocks-asset-it`, `blocks-asset-land`, `blocks-asset-intangible` — sibling Layer-2 kind extensions (separate hand-offs)
- `blocks-property-apartment`, `blocks-property-shopping-center`, `blocks-property-storage`, etc. — Layer-3 overlays (separate hand-offs)

### What this hand-off ships

Per ADR 0094 §Layer 2 (`blocks-asset-building` row):

1. **`Building`** entity — typed Layer-2 projection over a canonical `Asset` row where `Kind == "building"`. Carries kind-specific fields: `Address`, `YearBuilt`, `TotalSquareFeet`, `BuildingType` discriminator (single-family / duplex / triplex / quadplex / multi-family-5+ / commercial / mixed-use / mobile-home / industrial-flex / storage / shopping-center / retail-inline / retail-pad / office), `FoundationType`, `RoofType`, `ConstructionYear`, `LastRenovatedYear`, `ParcelNumber`, `Apn`, `ZoningCode`, `UnitCount` (projected from `blocks-asset-unit` when that ships).
2. **`BuildingId`** — strong-typed alias for `AssetId` constrained to `Kind == "building"`. Coexists with `AssetId` for compile-time safety on `Building`-specific surfaces.
3. **`BuildingType`** — stable string-code enum: `single-family` | `duplex` | `triplex` | `quadplex` | `multi-family-5+` | `commercial` | `mixed-use` | `mobile-home` | `industrial-flex` | `storage` | `shopping-center` | `retail-inline` | `retail-pad` | `office`. Per CRDT conventions §5 (stable codes; never renamed; deprecation rule applies).
4. **`BuildingKindData`** — side-table record holding the kind-specific fields; written by `IBuildingService.RegisterAsync(...)` alongside the canonical Asset row.
5. **`IBuildingRepository`** — read+write surface over the typed `Building` projection. Internally delegates to `IAssetRepository` (from the substrate) + the local `IBuildingKindDataRepository` for the side-table.
6. **`IBuildingService`** — kind-specific operations layer: `RegisterAsync` (writes canonical Asset + side-table), `GetByPropertyAddressAsync`, `ListByCityAsync`, `FilterByBuildingTypeAsync`, `RecordRenovationAsync` (writes an `AssetLifecycleEvent` of kind `renovated` + updates the side-table's `LastRenovatedYear`).
7. **Legacy + ERPNext migration importer integration** — `ILegacyPropertyToBuildingImporter` that reads `blocks-properties.Property` rows + projects them onto canonical `Asset { Kind: "building" }` + `BuildingKindData` rows. **NON-BREAKING:** the legacy `blocks-properties.Property` records are NOT mutated; the importer ships a wrapper read path that lets `IPropertyRepository.GetByIdAsync(...)` continue to return the legacy shape from the canonical surface. The full Phase 6 migration is OUT OF SCOPE here; this hand-off ships only the read-side wrapper foundation (enough for new code to adopt the generic surface while legacy code keeps working).
8. **Cross-cluster events** — emits the Layer-1 substrate's `Asset.*` events when buildings are registered/transferred/etc. (the substrate ships the event publisher; this hand-off populates the kind-data into the payload).
9. **DI extension** — `AddBlocksAssetBuilding()` registers `IBuildingRepository`, `IBuildingService`, `IBuildingKindDataRepository` (in-memory), and the legacy importer.
10. **`apps/docs/blocks-asset-building/overview.md`** — cluster docs page.

### What this hand-off does NOT ship

- **No new substrate primitives.** `Asset`, `AssetKind`, `AssetLifecycleEvent`, `AssetValuation`, `AssetCondition`, `AssetParty`, `IAssetRepository`, `IAssetLifecycleLog`, `IAssetValuationService`, `IAssetConditionService`, `IAssetHierarchyService` ALL come from `blocks-asset-foundation` (the substrate). If COB needs to add ANYTHING to the substrate during this build, **HALT** + file a `cob-question-*` beacon. The Layer-2 contract: kind-data only, no substrate extension.
- **No `blocks-asset-unit`.** Unit-within-building modeling ships as a separate Layer-2 hand-off. This hand-off's `Building.UnitCount` is initially a static integer field; once `blocks-asset-unit` lands it becomes a projection (zero behavior change to consumers).
- **No Phase 6 migration of `blocks-properties`.** The `blocks-properties.Property` entity remains its current shape. The legacy importer ships a *forward* path (legacy → canonical projection) but does NOT mutate or deprecate the legacy types in this hand-off. Phase 6 is a separate workstream.
- **No depreciation algorithm work.** `IAssetValuationService` (with strategy patterns per ADR 0094 OQ-2: StraightLine + DecliningBalance + UnitsOfProduction + NoDepreciation) ships with `blocks-asset-foundation` substrate. Buildings use it (typically StraightLine over 27.5 / 39 years for U.S. residential / commercial), but the algorithm itself is substrate.
- **No Layer-3 overlay integration.** `blocks-property-apartment`, `blocks-property-shopping-center`, etc. ship in Phase 7. This hand-off does not depend on any of them and does not reference them.
- **No CAM / auction-lien / TI-amortization logic.** All Layer-3 per ADR 0094 OQ-5.
- **No FHA / fair-housing decisioning** — orthogonal to building modeling; lives in `blocks-property-leasing-pipeline` (already shipped).
- **No multi-language `Address` model expansion.** The existing `Sunfish.Blocks.Properties.PostalAddress` is consumed as-is; international/non-postal addresses are a separate concern. Vehicle leases (lease-of-vehicle) similarly out of scope until `blocks-asset-vehicle` ships.

### Why building is the right first Layer-2

1. **The substrate's foundation kernel was designed around buildings.** `Sunfish.Foundation.Assets.README.md` worked example: "Mint Building:42 (10 floors, 120 units). Roof replaced. Correction: floors 10 → 12. Split: Building:42 → north + south." Building is the canonical asset shape in the kernel; the Layer-2 wrapping is the natural first kind to ship.
2. **Property is the existing flagship cluster surface.** `blocks-properties.Property` is referenced by 7+ downstream blocks (leases, inspections, maintenance, rent-collection, leasing-pipeline, work-orders, listings). Demonstrating the polymorphism pattern with `Building` (the Asset-typed projection that `Property` will eventually wrap to in Phase 6) validates the wrapper precedent before any of the other kind extensions ship.
3. **6-LLC commercial scope.** Apartments + mobile-home parks + light-industrial + storage + shopping-centers + retail + office + mixed-use are all *Building* sub-types. Shipping `BuildingType` discriminator in this hand-off unblocks every Layer-3 property-type overlay (Phase 7) in parallel.
4. **Lower-risk than `-vehicle` or `-equipment`.** Building has no concurrent-conflict patterns (a building is rarely modified by two replicas simultaneously — unlike vehicle telematics or equipment usage). Lower CRDT-conflict surface means simpler v1 acceptance.

### CRDT-friendly conventions applied (binding)

Per `path-ii-crdt-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `BuildingId` is a strongly-typed alias of `AssetId` (ULID-backed); `BuildingKindDataId` is a separate ULID for the side-table row |
| §2 Soft-delete tombstones | `Building` inherits the canonical `Asset.deletedAt` + `deletedBy` + `deletedReason` via the substrate; the side-table row is similarly tombstone-aware |
| §3 version + revisionVector | Inherits canonical `Asset.Version` + `Asset.RevisionVector` from the substrate; the side-table row carries its own envelope independently (allows kind-data updates without re-versioning the canonical Asset row when the substrate fields are unchanged) |
| §4 Append-only sub-collections | `BuildingRenovationRecord` rows (history of renovations) are append-only; each renovation also emits an `AssetLifecycleEvent` of kind `renovated` on the substrate log |
| §5 Stable string codes | `BuildingType` enum surfaces as stable string codes (see list in §What this hand-off ships #3); per CRDT conventions §5, codes are kebab-case, never renamed, deprecation rule applies |
| §6 Posted-then-immutable | Not strictly applicable to a building (a building isn't a "posted" transactional record). BUT: once a building is *disposed* (`AssetLifecycleEvent { kind = "disposed" }`), its kind-data side-table row is read-only; mutations are rejected at the Tier-1 validator layer |
| §7 State-machine resolution | The Asset header's lifecycle status (`active` / `disposed` / `decommissioned`) follows the substrate's resolver. No additional state machine in this hand-off. |
| §10 Two-tier validation | Tier-1 write-time: `BuildingKindData` invariants (e.g., `YearBuilt <= CurrentYear`; `TotalSquareFeet > 0`; `BuildingType` matches the canonical enum). Tier-2 post-merge: stub `IBuildingPostMergeReconciler` always returns "no issues" in v1 |
| §11 Idempotency keys | The legacy importer's `ExternalRef` field is the canonical idempotency key. `(source: "blocks-properties", externalRefId: "<legacy PropertyId>")` is the import key; idempotent re-import returns `Skipped` |

### Cross-cluster boundary (binding)

Per ADR 0094 + party-model-convention §3–§4:

- **`blocks-asset-building` OWNS the typed `Building` projection** of the canonical `Asset` row where `Kind == "building"`. Other Layer-2 packages (`-unit`, `-equipment`, `-vehicle`, etc.) do NOT reference `Building` directly; they reference `AssetId` and project on demand (a unit inside a building references the building's `AssetId` via the substrate hierarchy).
- **Layer-3 overlays consume `Building` (typed) OR `Asset` (substrate)** depending on whether they need building-specific operations. `blocks-property-apartment` will consume `Building` (it cares about `BuildingType`, `YearBuilt`, etc.); `blocks-financial-ar` will consume `Asset` (it only cares about the capex JE — building-specific fields don't matter for AR posting).
- **`blocks-properties` (legacy)** ships a *read-side wrapper* via the importer in PR 4 of this hand-off: `IPropertyRepository.GetByIdAsync` continues to return `Sunfish.Blocks.Properties.Property` records, but internally pulls from the canonical surface. **No `blocks-properties.Property` row is mutated by this hand-off.**
- **Write boundary:** the only writes to `Building` go through `IBuildingService.RegisterAsync` (canonical Asset + side-table atomic) or the legacy importer's `ImportLegacyPropertiesAsync`. Direct construction of `BuildingKindData` outside these surfaces is forbidden (analyzer rule TBD; Stage 06 ships the seam without the analyzer).

### Why this hand-off is gated on the substrate (and on ADR 0094)

This hand-off references substrate-level types (`Asset`, `AssetId`, `AssetKind`, `IAssetRepository`, `AssetLifecycleEvent`, etc.) that DO NOT EXIST on `origin/main` until `blocks-asset-foundation` ships. The Gate Conditions section enumerates the dependency. If COB picks up this hand-off and the substrate is absent, the workstream HALTS via `cob-question-*` beacon naming the missing substrate package. **The XO commitment:** the substrate hand-off (W#74) will be authored + ratified + status-flipped BEFORE this hand-off enters the priority queue.

The ADR is `Proposed` at hand-off authoring time. CO ratification of the 12 OQs may modify the surface in minor ways (e.g., OQ-9 may rename `blocks-assets-core` to `blocks-asset-foundation`; OQ-11 may consolidate `EquipmentClass` placement). **This hand-off uses the XO-recommended OQ resolutions** (see ADR 0094 OQ section); if CO overrules any OQ before this hand-off ships, XO will revise the hand-off + flip the ledger row to `held` per the standard `widening/revising mid-flight` discipline.

---

## Gate Conditions

This hand-off enters `ready-to-build` ONLY when ALL of the following are true:

### Gate G1 — `blocks-asset-foundation` substrate built

- `packages/blocks-asset-foundation/` exists on `origin/main`.
- The substrate's PASS gate met (per its own hand-off): canonical `Asset` + `AssetId` + `AssetKind` + `AssetLifecycleEvent` + `AssetValuation` + `AssetCondition` + `AssetParty` + `IAssetRepository` + `IAssetLifecycleLog` + `IAssetValuationService` + `IAssetConditionService` + `IAssetHierarchyService` all present + tested.

**If G1 NOT met:** STOP. File `cob-question-2026-05-XXTHH-MMZ-asset-building-substrate-missing.md` to `coordination/inbox/`. Halt the workstream + add a note to the W#75 row in `active-workstreams.md`. Wait for substrate hand-off to ship.

### Gate G2 — ADR 0094 status

- ADR 0094 is `Proposed` (acceptable) OR `Accepted` (preferred).
- If `Withdrawn` or `Superseded`, **STOP** and file `cob-question-*`.

CO directive can operate at `ready-to-build` even with ADR `Proposed` (per the ledger / ADR-status decoupling precedent from W#1, W#37, W#60).

### Gate G3 — No parallel-session work on `blocks-asset-*`

```bash
gh pr list --state open --search "blocks-asset in:title,body"
```

Expected: empty (or only this hand-off's own PRs). If anything else open, file `cob-question-*` before opening PR 1.

### Gate G4 — `blocks-properties` + `blocks-property-equipment` untouched on main

```bash
gh pr list --state open --search "blocks-properties in:title,body OR blocks-property-equipment in:title,body"
```

Expected: empty, OR only mechanical dependency-bump / docs-only PRs. If a substantive PR is in flight on either package, file `cob-question-*` and verify it doesn't intersect with this hand-off's Phase-6-preparation work.

### Gate G5 — Foundation kernel sanity check

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Assets/
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-assets-postgres/
```

Expected: both exist with their current shape (`IEntityStore`, `IVersionStore`, `IAuditLog`, `IHierarchyService` in `Common/`, `Entities/`, `Versions/`, `Audit/`, `Hierarchy/`). If these are mid-flight or restructuring, file `cob-question-*` before proceeding.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify Gate G1.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-asset-foundation/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-asset-foundation/Services/IAssetRepository.cs 2>&1
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-asset-foundation/Models/Asset.cs 2>&1
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-asset-foundation/Models/AssetKind.cs 2>&1
   ```
   Expected: all four exist. If any missing, halt per Gate G1.

2. **Verify Gate G2.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0094-generic-asset-polymorphism-overlays.md
   ```
   Expected: `status: Proposed` or `status: Accepted`. If anything else, halt per Gate G2.

3. **Verify Gate G3 (no parallel-session `blocks-asset-*` PRs).**
   ```bash
   gh pr list --state open --search "blocks-asset in:title,body"
   ```
   Expected: empty or only this hand-off's PRs. If unexpected, file `cob-question-*`.

4. **Verify Gate G4 (`blocks-properties` / `-property-equipment` not in flight substantively).**
   ```bash
   gh pr list --state open --search "blocks-properties in:title,body OR blocks-property-equipment in:title,body"
   ```
   Expected: empty or only mechanical PRs. Confirm none touch `Property.cs`, `PropertyId.cs`, `PropertyKind.cs`, `Equipment.cs`, `EquipmentId.cs`, `EquipmentClass.cs`.

5. **Verify Gate G5 (foundation kernel intact).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Assets/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-assets-postgres/
   ```
   Expected: both directories with their existing shape.

6. **Confirm `blocks-properties.Property` + `blocks-property-equipment.Equipment` reference surface (for the legacy importer in PR 4).**
   ```bash
   grep -rln "Sunfish.Blocks.Properties" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ /Users/christopherwood/Projects/Harborline-Software/Sunfish/apps/ /Users/christopherwood/Projects/Harborline-Software/Sunfish/accelerators/ | head -20
   grep -rln "Sunfish.Blocks.PropertyEquipment" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ /Users/christopherwood/Projects/Harborline-Software/Sunfish/apps/ /Users/christopherwood/Projects/Harborline-Software/Sunfish/accelerators/ | head -20
   ```
   Capture every consumer. PR 4's read-side wrapper must preserve every existing call signature against these consumers; the substantive Phase-6 migration is a SEPARATE hand-off.

7. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler.md`).

8. **Read the ADR sections.** Skim ADR 0094 §Layer 1, §Layer 2, §Architecture (especially "Polymorphism via discriminator, not inheritance" + "Per-record-class CP/AP classification"), §OQ-1, OQ-9, OQ-10, OQ-11. Read `path-ii-crdt-schema-conventions.md` §1, §2, §3, §4, §5, §10. Read `party-model-convention.md` §3 + §4.

9. **Confirm `blocks-asset-foundation` test suite is green on main:**
   ```bash
   dotnet test packages/blocks-asset-foundation/tests/ 2>&1 | tail -5
   ```
   If the substrate's tests fail on main, file `cob-question-*` immediately — substrate stability is required.

---

## Per-PR deliverables

This hand-off splits into **5 PRs** by responsibility:

- PR 1: Package scaffold + `Building` entity + `BuildingId` + `BuildingType` enum + extends Asset via discriminator pattern + `IBuildingRepository` + InMemory + DI registration
- PR 2: Building-specific kind-data fields (`BuildingKindData` side-table; address-refinement helpers; renovation tracking)
- PR 3: `IBuildingService` for kind-specific operations (`GetByPropertyAddress`, `ListByCity`, `FilterByBuildingType`, `RecordRenovation`)
- PR 4: Legacy `blocks-properties` read-wrapper + ERPNext importer
- PR 5: DI umbrella + `apps/docs` page + ledger flip + cluster cohort docs

PRs 1 + 2 + 3 are sequential. PR 4 can parallelize with PR 3 once PR 1 is in. PR 5 sequences last.

---

### PR 1 — Package scaffold + `Building` entity + `BuildingId` + `BuildingType` + `IBuildingRepository` + InMemory + DI

**Estimated effort:** ~2–3h
**Scope:** new package `blocks-asset-building`; core typed-projection types over the substrate; repository surface; DI extension; no kind-specific services yet (PR 3) and no legacy importer (PR 4)
**Commit subject:** `feat(blocks-asset-building): scaffold Layer-2 kind extension with Building + BuildingId + BuildingType + IBuildingRepository per ADR 0094 §Layer 2`
**Branch:** `cob/blocks-asset-building-scaffold`

#### Package skeleton

```
packages/blocks-asset-building/
├── README.md
├── NOTICE.md                                       (Apache OFBiz attribution)
├── Sunfish.Blocks.AssetBuilding.csproj
├── Models/
│   ├── BuildingId.cs                               (alias of AssetId; constrained projection)
│   ├── BuildingType.cs                             (stable string-code enum)
│   ├── Building.cs                                 (typed projection record)
│   ├── FoundationType.cs                           (stable string-code enum)
│   ├── RoofType.cs                                 (stable string-code enum)
│   └── BuildingProjection.cs                       (helper: Asset+BuildingKindData → Building)
├── Services/
│   ├── IBuildingRepository.cs
│   └── InMemoryBuildingRepository.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
└── tests/
    ├── Sunfish.Blocks.AssetBuilding.Tests.csproj
    ├── BuildingRecordTests.cs
    ├── BuildingTypeStableCodeTests.cs
    ├── BuildingProjectionTests.cs
    └── InMemoryBuildingRepositoryTests.cs
```

#### Cited substrate symbols (consumed; not re-shipped)

From `Sunfish.Blocks.AssetFoundation` (substrate; verify each before referencing):

- `Sunfish.Blocks.AssetFoundation.Models.Asset`
- `Sunfish.Blocks.AssetFoundation.Models.AssetId`
- `Sunfish.Blocks.AssetFoundation.Models.AssetKind` — string enum with `building` code
- `Sunfish.Blocks.AssetFoundation.Models.AssetLocation` (discriminated union)
- `Sunfish.Blocks.AssetFoundation.Services.IAssetRepository`
- `Sunfish.Foundation.MultiTenancy.TenantId`
- `Sunfish.Foundation.MultiTenancy.IMustHaveTenant`

#### New types

**`Models/BuildingId.cs`** — typed alias for `AssetId`, constrained semantically to `Asset.Kind == "building"`. (Aliases in C# are nominally distinct; the constraint is enforced at the repository write boundary.)

The intent: consumer code that says `BuildingId` instead of `AssetId` carries the kind-implication in the type signature without requiring runtime cost. The `BuildingId.Value` is a ULID identical to the underlying `AssetId.Value`.

**`Models/BuildingType.cs`** — stable string-code enum per ADR 0094 §Layer 2:

```text
single-family
duplex
triplex
quadplex
multi-family-5+
commercial
mixed-use
mobile-home
industrial-flex
storage
shopping-center
retail-inline
retail-pad
office
```

The string-code rule from `path-ii-crdt-schema-conventions.md` §5: codes are kebab-case, lowercase, never renamed. Adding a new code is additive; renaming requires a deprecation cycle. The enum surfaces as both a typed enum (compile-time) and a stable string (wire format).

Per ADR 0094 OQ-11 (XO recommendation): the legacy `blocks-property-equipment.EquipmentClass` enum relocates to `blocks-asset-equipment` in Phase 4; the legacy class continues as a type-alias. THIS hand-off does NOT touch `EquipmentClass`.

**`Models/FoundationType.cs` + `Models/RoofType.cs`** — stable string-code enums (kept small in v1; expand per demand):

- FoundationType: `slab` | `crawl-space` | `basement-full` | `basement-partial` | `pier-and-beam` | `pilings` | `other`
- RoofType: `composition-shingle` | `architectural-shingle` | `metal-standing-seam` | `tile-clay` | `tile-concrete` | `slate` | `flat-built-up` | `flat-modified-bitumen` | `tpo-membrane` | `epdm` | `green-roof` | `other`

**`Models/Building.cs`** — the canonical typed projection record:

The record carries the substrate-managed fields (Id, TenantId, CreatedAt, UpdatedAt, Version, deletion envelope) PLUS the building-specific fields:

- `Address` — the existing `Sunfish.Blocks.Properties.PostalAddress` (or a new `Sunfish.Blocks.AssetBuilding.Models.PostalAddress` clone if `blocks-properties` cannot be cleanly referenced — see Halt H4).
- `ParcelNumber` — string?, optional
- `Apn` — string?, optional
- `ZoningCode` — string?, optional
- `YearBuilt` — int?, optional
- `LastRenovatedYear` — int?, optional
- `TotalSquareFeet` — decimal?, optional (per CRDT conventions §10: Tier-1 validator rejects `<= 0`)
- `TotalBedrooms` — int?, optional (preserved from legacy `Property` shape for backward-compat projection)
- `TotalBathrooms` — decimal?, optional (allows `1.5`)
- `BuildingType` — required `BuildingType` enum
- `FoundationType` — optional `FoundationType` enum
- `RoofType` — optional `RoofType` enum
- `UnitCount` — int (default 1; updated by `blocks-asset-unit` when that ships)
- `ConstructionYear` — int? (synonym for `YearBuilt`; if both set the canonical is `ConstructionYear`)
- `PrimaryPhotoBlobRef` — string?, optional (preserved from legacy)
- `Notes` — string?, optional

`Building` is a **read-side projection record**; it is NOT what gets persisted directly. The persistence shape is canonical `Asset` + `BuildingKindData` (side-table; ships in PR 2). PR 1 introduces the `Building` record and the projection helper that assembles a `Building` from `Asset` + `BuildingKindData`.

**`Models/BuildingProjection.cs`** — static projection helper:

The function `BuildingProjection.From(Asset asset, BuildingKindData? kindData) → Building?` returns `null` if `asset.Kind != "building"`; otherwise constructs the typed `Building` record. The reverse direction (`Building → (Asset + BuildingKindData)`) is owned by `IBuildingService.RegisterAsync` in PR 3.

#### `IBuildingRepository` (read+write boundary; PR 1)

The repository surface, in plain prose (no code per hand-off-constraints; full signature spec at COB-implementation time):

- `GetByIdAsync(BuildingId id, CancellationToken ct)` — returns `Building?` projection
- `GetManyAsync(IEnumerable<BuildingId> ids, CancellationToken ct)` — returns `IReadOnlyDictionary<BuildingId, Building>` (omits unknown ids)
- `GetByExternalRefAsync(string source, string externalRefId, CancellationToken ct)` — idempotency-key lookup
- `QueryByTenantAsync(TenantId tenantId, BuildingType? filterByType = null, CancellationToken ct = default)` — returns `IReadOnlyList<Building>`; respects tenant scoping per `IMustHaveTenant`
- `UpsertAsync(Building building, CancellationToken ct)` — *write boundary*; writes the canonical `Asset` row + the side-table row in a single in-memory transaction (in v1)
- `TombstoneAsync(BuildingId id, TenantId tenant, string? reason, CancellationToken ct)` — tombstones the canonical Asset; emits a substrate-managed `Asset.Disposed` event

**Tier-1 validation invariants (write-time, rejected before persist):**

- `building.TenantId != default`
- `building.Address != null`
- `building.BuildingType ∈ {valid stable codes}` — reject unknown codes per CRDT conventions §5
- `building.YearBuilt is null || building.YearBuilt <= CurrentYear + 5` (allow 5-year-future planning gap for under-construction buildings)
- `building.TotalSquareFeet is null || building.TotalSquareFeet > 0`
- `building.UnitCount >= 1`

**In-memory implementation (`InMemoryBuildingRepository`):**

Backed by composition of:
- `IAssetRepository` (substrate; required DI dependency)
- An internal `ConcurrentDictionary<BuildingId, BuildingKindData>` for the side-table (preview of PR 2; the kind-data type is shipped in this PR as a stub with a single field placeholder `Reserved` that PR 2 populates)

This PR's in-memory repository SHIPS the wiring; PR 2 expands `BuildingKindData` with the substantive fields.

#### DI extension (PR 1)

`AddBlocksAssetBuilding()` registers:

- `IBuildingRepository` → `InMemoryBuildingRepository`
- A startup check (no-op in v1) that verifies `IAssetRepository` is already registered; if not, throw a helpful exception naming the missing dependency and pointing at `AddBlocksAssetFoundation()`.

#### Tests (PR 1)

`tests/BuildingRecordTests.cs`:

- `Construction_PreservesAllFields`
- `BuildingTypeStableCodes_AllRecognized` (positive cases for every documented code)
- `BuildingTypeStableCodes_RejectsUnknown` (Tier-1 validator rejects ad-hoc codes)
- `TotalSquareFeet_RejectsZeroOrNegative`
- `YearBuilt_AllowsFiveYearFutureGap_ForUnderConstruction`
- `UnitCount_DefaultsToOne`
- `Address_RequiredOnConstruction`

`tests/BuildingTypeStableCodeTests.cs`:

- `Codes_AreKebabCase_LowerCase`
- `Codes_HaveStableStringSurface` (test each code's string value matches the documented spec verbatim)

`tests/BuildingProjectionTests.cs`:

- `Project_AssetOfKindBuilding_WithKindData_ReturnsBuilding`
- `Project_AssetOfKindBuilding_WithoutKindData_ReturnsBuildingWithDefaults` (graceful degradation: missing side-table row should not blow up; degrade to a Building with `BuildingType = Other`-equivalent default)
- `Project_AssetOfKindNotBuilding_ReturnsNull`
- `Project_TombstonedAsset_ReturnsNull` (deletion envelope respected)

`tests/InMemoryBuildingRepositoryTests.cs`:

- `UpsertAndGetById_RoundTrips`
- `GetByExternalRef_ReturnsCorrectBuilding`
- `Tombstone_HidesFromDefaultQueries` (queryable with `includeDisposed: true` extension only)
- `QueryByTenant_RespectsTenantBoundary` (negative: building in tenant A is NOT visible to tenant B)
- `QueryByTenant_FilterByBuildingType_ReturnsOnlyMatching`
- `Upsert_ValidatesTenantIdNotDefault`
- `Upsert_ValidatesBuildingTypeIsKnownCode`

Total new tests this PR: ~17.

#### Verification

- `dotnet build` succeeds for the new package + adds it to the solution.
- `dotnet test packages/blocks-asset-building/tests/` passes all ~17 tests.
- `grep -r "Sunfish.Blocks.AssetBuilding" packages/blocks-asset-building/` returns hits across every `.cs` file (sanity check on namespace).
- `grep -r "Sunfish.Blocks.AssetFoundation" packages/blocks-asset-building/` confirms the substrate dependency is imported, NOT redefined.

#### Do NOT in this PR

- Do NOT introduce a new `Asset` row schema field. Substrate-extension is forbidden in Layer 2.
- Do NOT introduce a `BuildingKindData` side-table with all its fields populated. PR 2 ships that.
- Do NOT introduce `IBuildingService.RegisterAsync` or kind-specific service operations. PR 3 ships those.
- Do NOT touch `blocks-properties/`. PR 4 ships the read-side wrapper.
- Do NOT introduce a depreciation strategy. The substrate ships `IAssetValuationService`; PR 3 may demonstrate it via a test fixture but does not introduce algorithms.
- Do NOT introduce a `BuildingsByAddressIndex` cache. Direct query is acceptable for v1; the substrate may already provide indexing.

---

### PR 2 — Building-specific kind-data side-table + renovation tracking

**Estimated effort:** ~2h
**Scope:** the `BuildingKindData` side-table; the persistence wiring between canonical Asset and side-table; renovation history (`BuildingRenovationRecord` append-only)
**Commit subject:** `feat(blocks-asset-building): BuildingKindData side-table + BuildingRenovationRecord append-only history per ADR 0094 §Architecture`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-asset-building-kind-data`

#### New types

**`Models/BuildingKindData.cs`** — the canonical side-table record:

Carries the building-specific fields enumerated under PR 1's `Building` model (Address, YearBuilt, TotalSquareFeet, etc.) keyed by the canonical `AssetId`. Per CRDT conventions §3, the record carries its own `Version` + `RevisionVector` envelope; mutations bump the version independently of the canonical `Asset` row's version (allows kind-data updates without re-versioning the substrate row when the substrate-managed fields are unchanged).

**`Models/BuildingRenovationRecord.cs`** — append-only row representing one renovation event:

Fields:
- `Id: BuildingRenovationRecordId` — ULID
- `BuildingId: BuildingId`
- `RenovationYear: int` (required)
- `Scope: string` (free-text; e.g., `"Full kitchen + master bath renovation"`)
- `EstimatedCost: decimal?` (optional; for capex JE posting downstream)
- `CompletedAtUtc: Instant?` (null while in-flight; set when work completes)
- `LinkedWorkOrderRef: string?` (opaque FK to `blocks-work-*`; OQ-7-style external-ref)
- `RecordedByPartyId: PartyId` (per `party-model-convention.md` §3 — the operator who logged the renovation)
- `CreatedAtUtc: Instant`
- Soft-delete envelope (per CRDT conventions §2)

Per CRDT conventions §4, this collection is **append-only**: never UPDATE except for soft-delete tombstones. Corrections happen by appending a new record with `Scope = "Correction of #<priorRecordId>: ..."` and a tombstone on the prior.

**`Services/IBuildingKindDataRepository.cs`**: read+write surface over the side-table, called from `InMemoryBuildingRepository` (extended in this PR to compose):

- `GetByAssetIdAsync(AssetId id, CancellationToken ct) → BuildingKindData?`
- `UpsertAsync(BuildingKindData data, CancellationToken ct)` — Tier-1 validates against the substrate (the AssetId must reference an `Asset` with `Kind == "building"`)
- `TombstoneAsync(AssetId id, string? reason, CancellationToken ct)` — tombstones the side-table row; the substrate Asset row tombstone is handled separately

**`Services/IBuildingRenovationLog.cs`**: append-only log surface:

- `AppendAsync(BuildingRenovationRecord record, CancellationToken ct)` — write-once; the record's `Id` is generated server-side if not provided
- `GetByBuildingAsync(BuildingId id, CancellationToken ct) → IReadOnlyList<BuildingRenovationRecord>` — ordered by `CreatedAtUtc` ascending
- `GetSinceAsync(BuildingId id, Instant since, CancellationToken ct) → IReadOnlyList<BuildingRenovationRecord>` — incremental query

#### Tier-1 validation invariants (kind-data)

- `BuildingKindData.AssetId references a known Asset of kind "building"` (looked up via substrate `IAssetRepository`)
- `BuildingKindData.YearBuilt` if present, must be `<= CurrentYear + 5`
- `BuildingKindData.LastRenovatedYear` if present, must be `<= CurrentYear + 5` AND `>= BuildingKindData.YearBuilt`
- `BuildingKindData.TotalSquareFeet` if present, must be `> 0`
- `BuildingKindData.UnitCount >= 1`
- `BuildingKindData.BuildingType ∈ {documented stable codes}` per CRDT conventions §5

#### DI extension update

Extend `AddBlocksAssetBuilding()` to register:

- `IBuildingKindDataRepository → InMemoryBuildingKindDataRepository`
- `IBuildingRenovationLog → InMemoryBuildingRenovationLog`

The existing `InMemoryBuildingRepository` is refactored to compose these two dependencies + the substrate's `IAssetRepository`.

#### Tests (PR 2)

`tests/BuildingKindDataTests.cs`:

- `Construction_PreservesAllFields`
- `YearBuilt_BoundaryAtCurrentYearPlusFive_Accepted`
- `LastRenovated_RejectsWhenBeforeYearBuilt`
- `TotalSquareFeet_RejectsZeroOrNegative`
- `UnitCount_RejectsZero`
- `BuildingType_RejectsUnknownCode`
- `BuildingType_AcceptsAllDocumentedCodes`

`tests/BuildingKindDataRepositoryTests.cs`:

- `Upsert_LinksToCanonicalAsset_OfKindBuilding`
- `Upsert_RejectsForAssetOfWrongKind` (passing an AssetId for kind=vehicle should fail Tier-1)
- `Upsert_RejectsForUnknownAssetId`
- `GetByAssetId_ReturnsNullWhenNotPresent`
- `Tombstone_SetsDeletedAt_FiltersFromDefaultGets`

`tests/BuildingRenovationLogTests.cs`:

- `Append_AssignsIdIfNotProvided`
- `Append_PreservesProvidedId`
- `Append_PreservesAllFields`
- `GetByBuilding_OrdersAscendingByCreatedAt`
- `GetSince_ReturnsOnlyRowsAfterCutoff`
- `Append_IsImmutable_NoUpdateMethod` (verify the interface doesn't expose Update)
- `Tombstone_DoesNotAlterPriorRecords` (append-only history preserves history even when one row is soft-deleted)

`tests/InMemoryBuildingRepositoryCompositionTests.cs`:

- `Upsert_AtomicallyWritesAssetAndKindData` (one Upsert call writes both rows; partial failures roll back the side-table write)
- `Upsert_BumpsKindDataVersionIndependentlyOfAssetVersion`

Total new tests this PR: ~21.

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests still pass (no behavior regression).
- New tests pass.
- A regression scenario: register a Building → renovate it via `BuildingRenovationLog.AppendAsync` 3 times → fetch the renovation history → verify 3 rows in correct order, each with `Id`, `CreatedAtUtc`, `Scope` populated.

#### Do NOT in this PR

- Do NOT introduce a `BuildingRenovationService.PostCapexJeAsync` that posts journal entries. That cross-cluster integration belongs in `blocks-financial-ar` consumers of `Asset.*` events. The renovation log just records; downstream posts capex.
- Do NOT introduce write-side caching. PR 4 may add a `BuildingsByCity` index if the legacy importer profiling shows hot-path latency; v1 direct-query is fine.
- Do NOT make `BuildingKindData.UnitCount` a projection from `blocks-asset-unit`. The unit-from-projection wiring ships when `blocks-asset-unit` lands.

---

### PR 3 — `IBuildingService` for kind-specific operations

**Estimated effort:** ~2h
**Scope:** the write-side service surface for buildings; kind-specific operations including registration (atomic canonical Asset + side-table write), address-based query helpers, building-type filters, and renovation recording
**Commit subject:** `feat(blocks-asset-building): IBuildingService + RegisterAsync + GetByPropertyAddress + ListByCity + RecordRenovation`
**Depends on:** PR 2 merged (independent of PR 4)
**Branch:** `cob/blocks-asset-building-service`

#### New service

**`Services/IBuildingService.cs`**: kind-specific operations layer. Methods:

- `RegisterAsync(BuildingRegistration registration, CancellationToken ct) → RegisterResult` — atomic write of canonical Asset + BuildingKindData + initial AssetLifecycleEvent (kind = `acquired`); idempotent on `ExternalRef`
- `GetByPropertyAddressAsync(PostalAddress address, TenantId tenant, CancellationToken ct) → Building?` — exact-match lookup by full address; useful for ERPNext-import dedup + cross-cluster address-based dispatch
- `ListByCityAsync(string city, string? stateProvince, TenantId tenant, CancellationToken ct) → IReadOnlyList<Building>` — city-scoped portfolio query
- `FilterByBuildingTypeAsync(BuildingType type, TenantId tenant, CancellationToken ct) → IReadOnlyList<Building>` — type-scoped portfolio query
- `RecordRenovationAsync(BuildingId id, BuildingRenovationRecord record, CancellationToken ct) → RecordRenovationResult` — append to renovation log + emit `Asset.LifecycleEvent { kind: renovated }` on the substrate event-bus + optionally bump `BuildingKindData.LastRenovatedYear` if the renovation year exceeds the current value

**`Models/BuildingRegistration.cs`** — input DTO for `RegisterAsync`:

Carries the same fields as `Building` (Address, BuildingType, YearBuilt, etc.) plus:
- `RegisteredByPartyId: PartyId` (the operator)
- `RegisteredAtUtc: Instant?` (default `now()`)
- `AcquisitionCost: decimal?` (drives the substrate's `Asset.Acquired` event payload; downstream `blocks-financial-*` posts the capex JE)
- `AcquisitionDate: DateOnly?` (defaults to today; can be backdated for migration)
- `ExternalRef: string?` (idempotency key for importers)

**`Models/RegisterResult.cs`** + **`Models/RegisterError.cs`** + **`Models/RecordRenovationResult.cs`** + **`Models/RecordRenovationError.cs`** — Result-type enums per the `IInvoicePostingService` pattern from `blocks-financial-ar`.

Errors enumerated (RegisterError):
- `None`
- `DuplicateExternalRef`
- `InvalidTenantId`
- `InvalidBuildingType`
- `InvalidAddress`
- `InvalidYearBuilt`
- `AssetWriteFailed`
- `KindDataWriteFailed`
- `LifecycleEventEmitFailed`

#### `RegisterAsync` algorithm (high-level prose)

1. **Idempotency check.** If `registration.ExternalRef != null`, query substrate's `IAssetRepository.GetByExternalRefAsync(...)` first; if a Building exists, return `Skipped` with the existing Building.
2. **Tier-1 validation.** Run the full invariant set (see PR 1 + PR 2). Reject early.
3. **Allocate IDs.** Generate `AssetId.New()` for the canonical row; the `BuildingId` is the same underlying ULID.
4. **Compose canonical Asset.** Build the substrate's `Asset` record with `Kind = "building"`, `Status = "active"`, location from `registration.Address`, tenant from `registration.RegisteredByPartyId.TenantId` (or explicit `registration.TenantId`).
5. **Compose `BuildingKindData`.** Map building-specific fields from the registration into a side-table row keyed by the same AssetId.
6. **Atomic write.** Via `InMemoryBuildingRepository.UpsertAsync(building)` — composing the substrate's `IAssetRepository.UpsertAsync(asset)` + the side-table write. In v1 (in-memory), atomicity is by lock; the SQLite/Postgres backing impl (a follow-on) uses a database transaction.
7. **Append `AssetLifecycleEvent`.** Substrate emits `Asset.Acquired` event with `AssetId`, `AssetKind = "building"`, `AcquisitionCost`, `AcquisitionDate`, `AcquiredByPartyId`. Per ADR 0094 cross-cluster event-bus integration, `blocks-financial-*` consumers post capex on this event.
8. **Return.** `RegisterResult { Building = building, Error = None }`.

#### `RecordRenovationAsync` algorithm

1. Verify the Building exists (Tier-1).
2. Validate the renovation record (positive year, year ≥ Building's YearBuilt, scope non-empty).
3. Append to renovation log.
4. If `record.RenovationYear > existing BuildingKindData.LastRenovatedYear`, update the side-table's `LastRenovatedYear`.
5. Emit `Asset.LifecycleEvent { kind: "renovated", payloadJson: { renovationRecordId, year, scope, estimatedCost } }` on the substrate event-bus.
6. Return success with the new renovation record's ID.

#### DI extension update

Extend `AddBlocksAssetBuilding()` to register `IBuildingService → BuildingService`.

#### Tests (PR 3)

`tests/BuildingServiceTests.cs`:

- `Register_NewBuilding_WritesAssetAndKindDataAndLifecycleEvent` (happy path; verify all three side-effects)
- `Register_DuplicateExternalRef_ReturnsSkippedWithExisting` (idempotency)
- `Register_InvalidBuildingType_ReturnsError`
- `Register_InvalidAddress_ReturnsError`
- `Register_InvalidYearBuilt_ReturnsError` (year > now + 5; year < 1700; etc.)
- `Register_InvalidTenantId_ReturnsError`
- `Register_AssetAcquiredEventPayload_IncludesAcquisitionCost`
- `RecordRenovation_AppendsToLog_AndEmitsLifecycleEvent`
- `RecordRenovation_BumpsLastRenovatedYear_WhenLater`
- `RecordRenovation_DoesNotBumpLastRenovatedYear_WhenEarlier`
- `RecordRenovation_RejectsForUnknownBuildingId`

`tests/BuildingQueryServiceTests.cs`:

- `GetByPropertyAddress_ExactMatch_ReturnsBuilding`
- `GetByPropertyAddress_AddressDoesNotMatch_ReturnsNull`
- `GetByPropertyAddress_RespectsTenantBoundary`
- `ListByCity_ReturnsMatchingBuildings`
- `ListByCity_CaseInsensitiveOnCity`
- `ListByCity_FiltersOutTombstoned`
- `FilterByBuildingType_ReturnsMatchingType`
- `FilterByBuildingType_ExcludesOtherTypes`
- `FilterByBuildingType_RespectsTenantBoundary`

Total new tests this PR: ~20.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- New tests pass.
- An end-to-end smoke: Register a shopping-center Building → query by address → matches; record a renovation → renovation history has 1 row + LastRenovatedYear updated; query the canonical Asset → its lifecycle log has 2 events (`acquired` + `renovated`).

#### Do NOT in this PR

- Do NOT introduce a CAM allocation policy on `Building`. ADR 0094 OQ-5 binds: CAM is Layer-3 (`blocks-property-shopping-center`).
- Do NOT introduce a depreciation run trigger on Register. The substrate's `IAssetValuationService` is called on a schedule, not on registration.
- Do NOT introduce `IBuildingService.DisposeAsync`. The substrate's `IAssetRepository.TombstoneAsync` / `IAssetLifecycleLog.AppendAsync(kind: disposed)` is the canonical disposal surface. Layer 2 doesn't reimplement substrate operations.

---

### PR 4 — Legacy `blocks-properties` read-wrapper + ERPNext importer

**Estimated effort:** ~2–3h
**Scope:** non-breaking wrapper letting legacy `IPropertyRepository.GetByIdAsync` and similar surface continue to work by projecting from the canonical Asset surface; ERPNext property-record importer (`IErpnextPropertyImporter`); minimal apps/docs entry
**Commit subject:** `feat(blocks-asset-building): legacy blocks-properties read-wrapper + IErpnextPropertyImporter (non-breaking)`
**Depends on:** PR 3 merged (or parallel with PR 3 if the importer's only dependency is `IBuildingService.RegisterAsync`)
**Branch:** `cob/blocks-asset-building-legacy-wrapper`

#### Pattern overview

Mirrors the `blocks-rent-collection`-as-wrapper-over-AR-Invoice pattern from the 2026-05-16 ratification ruling Decision 3:

```
                          ┌──────────────────────────────────────┐
External consumer code    │ IPropertyRepository                  │
                          │  .GetByIdAsync(PropertyId)           │
                          │  .ListByTenantAsync(TenantId)        │  ← unchanged
                          │  .UpsertAsync(Property)              │
                          └──────────────────────────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────────┐
Wrapper layer (NEW PR 4)  │ LegacyPropertyToBuildingAdapter      │
                          │  .CanonicalToLegacy(building) →      │
                          │      Property (legacy shape)         │
                          │  .DelegateUpsert(property) →         │
                          │      IBuildingService.RegisterAsync  │
                          └──────────────────────────────────────┘
                                            │
                                            ▼
                          ┌──────────────────────────────────────┐
Canonical (PR 1-3)        │ blocks-asset-building                │
                          │   Building (canonical typed)         │
                          │   IBuildingService                   │
                          └──────────────────────────────────────┘
```

External consumers continue to call `IPropertyRepository.GetByIdAsync(...)`. The implementation constructs a Building via `IBuildingService.RegisterAsync` on upsert, or projects from the canonical Asset surface on read.

#### Non-breaking constraints (from PR 4 wrapper)

1. **`Sunfish.Blocks.Properties.Property` record signature stays identical.** All positional + init params unchanged. Adding new optional fields requires the deprecation discipline from the `blocks-rent-collection` precedent.
2. **`Sunfish.Blocks.Properties.PropertyKind` enum values stay identical.** Maps onto canonical `BuildingType`:
   - PropertyKind values map to BuildingType codes per a translation table (e.g., `Residential` → `single-family` / `duplex` / `multi-family-5+` depending on `TotalBedrooms` count; `Commercial` → `commercial`; `Mixed` → `mixed-use`; etc.). The translation MUST be deterministic; ambiguous PropertyKind→BuildingType mappings (e.g., a "Residential" Property with no unit-count) default to `single-family` (the most-common) with a warning log.
3. **`IPropertyRepository` interface stays identical.** No method-signature changes; no new methods. (New methods may be added in a future explicit api-change pipeline; not in this hand-off.)
4. **All existing tests in `packages/blocks-properties/tests/` pass unchanged.**

#### New types

**`Adapters/LegacyPropertyToBuildingAdapter.cs`** (internal class, in `Sunfish.Blocks.AssetBuilding.Adapters`):

Composition:
- `IBuildingService` (from PR 3)
- `IBuildingRepository` (from PR 1)

Methods (prose):
- `CanonicalToLegacy(Building building, BuildingKindData kindData) → Sunfish.Blocks.Properties.Models.Property` — the projection-direction: take a canonical Building, build the legacy Property record.
- `LegacyToBuildingRegistration(Sunfish.Blocks.Properties.Models.Property property) → BuildingRegistration` — the inverse: legacy Property record → registration DTO ready for `RegisterAsync`.

**`LegacyPropertyRepositoryRedirect.cs`** (optional second class, only used by the importer in this hand-off; the full `blocks-properties.IPropertyRepository` retrofit is Phase 6 — not THIS hand-off — see below).

#### Migration scope (this hand-off vs Phase 6 — DEFINED HERE)

**SCOPE THIS HAND-OFF (PR 4):** ship the ADAPTER + IMPORTER. The adapter is consumable by tests + by the importer; it is NOT yet wired into `blocks-properties.IPropertyRepository` as the primary read-side. (The full retrofit replaces the existing in-memory `InMemoryPropertyRepository` with one that delegates to the adapter — that's the Phase 6 scope.)

**OUT OF SCOPE THIS HAND-OFF:** the actual swap of `blocks-properties.InMemoryPropertyRepository` to delegate-via-adapter. That swap is Phase 6 because:
- It requires touching `blocks-properties/DependencyInjection/...` and may interact with running consumers in `accelerators/anchor/` and `accelerators/bridge/`.
- It triggers existing-test re-verification across every `blocks-properties` consumer.
- It expands the PR diff substantially.

Phase 6 is a separate workstream; XO will author its hand-off (`blocks-properties-phase6-migration-stage06-handoff.md`) after this hand-off ships.

#### `IErpnextPropertyImporter`

`Migration/IErpnextPropertyImporter.cs` — mirrors the pattern in `blocks-financial-ar.IErpnextSalesInvoiceImporter`:

- `UpsertFromErpnextAsync(ErpnextPropertySource source, TenantId targetTenant, CancellationToken ct) → ImportOutcome<Building>`

**`ErpnextPropertySource`** record fields:
- `Name` — ERPNext stable id
- `Modified` — ERPNext version key
- `PropertyName` — display name
- `Address` (composed from ERPNext's address sub-doc)
- `PropertyType` — string code per ERPNext's property doctype; mapped to `BuildingType` per the translation table
- `YearBuilt`, `TotalSquareFeet`, `TotalBedrooms`, `TotalBathrooms` — kind-data fields
- `AcquisitionCost`, `AcquisitionDate` — substrate-event payload fields
- `OwnerPartyName` — resolved via `IPartyReadModel` (per `party-model-convention.md` §4) to a `PartyId`
- `DocStatus` — ERPNext: 0=Draft, 1=Submitted, 2=Cancelled

Algorithm (prose):
1. **Idempotency check.** `IBuildingRepository.GetByExternalRefAsync(source: "erpnext", externalRefId: source.Name)`; if exists + version matches, return `Skipped`.
2. **Translate.** Build `BuildingRegistration` from the source per the translation table.
3. **Resolve owner.** Lookup `OwnerPartyName` via `IPartyReadModel`; if absent throw `UnknownPartyException` per migration-importer §10.x discipline.
4. **Register.** Call `IBuildingService.RegisterAsync(registration, ct)`.
5. **Handle DocStatus.** If `DocStatus == 2`, immediately call substrate's tombstone/dispose flow (one of the side-effects on the canonical Asset).
6. **Return `Inserted` / `Updated` / `Skipped`.**

#### DI extension update

Extend `AddBlocksAssetBuilding()` to register:

- `IErpnextPropertyImporter → ErpnextPropertyImporter`
- `LegacyPropertyToBuildingAdapter` (internal; injected into importer + future Phase-6 wrapper)

#### Tests (PR 4)

`tests/LegacyPropertyToBuildingAdapterTests.cs`:

- `CanonicalToLegacy_PreservesPropertyKindMapping` (for each BuildingType code → expected PropertyKind value)
- `CanonicalToLegacy_PreservesTotalSquareFeet_TotalBedrooms_TotalBathrooms`
- `CanonicalToLegacy_PreservesAcquisitionCost_AcquiredAt`
- `CanonicalToLegacy_PreservesAddress`
- `LegacyToBuildingRegistration_ResidentialWithBedroomCount_MapsToSingleFamily` (translation rule)
- `LegacyToBuildingRegistration_CommercialKind_MapsToCommercialBuildingType`
- `LegacyToBuildingRegistration_MixedKind_MapsToMixedUseBuildingType`
- `LegacyToBuildingRegistration_AmbiguousKind_DefaultsToSingleFamilyWithWarning` (log spy verifies warning emitted)

`tests/ErpnextPropertyImporterTests.cs`:

- `Upsert_NewProperty_RegistersBuilding`
- `Upsert_DuplicateExternalRef_ReturnsSkipped` (idempotency)
- `Upsert_VersionUpgrade_UpdatesKindData` (same External Ref, higher Modified → re-import → upsert)
- `Upsert_CancelledSource_DisposesBuilding`
- `Upsert_UnknownOwnerPartyName_ThrowsHelpfulError`
- `Upsert_MapsPropertyTypeToBuildingType` (every ERPNext PropertyType → BuildingType expectation)
- `Upsert_PreservesAcquisitionDate_EvenIfBackdated`
- `Upsert_AddressMapping_FromErpnextAddressSubDoc`

Total new tests this PR: ~16.

#### Verification

- `dotnet build` succeeds across the whole solution.
- All existing `blocks-properties` tests pass (zero regression — confirms the wrapper hasn't accidentally short-circuited the legacy types).
- All new adapter + importer tests pass.
- A consumer-host smoke test: `AddBlocksAssetFoundation()` + `AddBlocksAssetBuilding()` builds without runtime DI errors. Importing a synthetic ERPNext property → canonical Building registered; legacy projection lookup via adapter returns the equivalent `Property` shape.
- `grep -r "Sunfish.Blocks.Properties" packages/ apps/ accelerators/` — every consumer still compiles against the unchanged legacy types.

#### Do NOT in this PR

- Do NOT modify `Sunfish.Blocks.Properties.Property` record signature. Out of scope; Phase 6 territory.
- Do NOT modify `Sunfish.Blocks.Properties.IPropertyRepository` interface. Out of scope.
- Do NOT delete or `[Obsolete]`-mark any `blocks-properties` type. The legacy types remain canonical for their existing consumers until Phase 6.
- Do NOT introduce the importer orchestrator (the multi-pass driver). That lives in `tooling-anchor-import` per the AR hand-off precedent.

---

### PR 5 — DI umbrella + apps/docs page + ledger flip + cluster cohort docs

**Estimated effort:** ~1–2h
**Scope:** consolidated DI umbrella; apps/docs overview page; READMEs; cohort-discipline doc updates; ledger flip
**Commit subject:** `docs(blocks-asset-building): cluster docs + apps/docs overview + DI umbrella + cohort cohort references`
**Depends on:** PR 4 merged
**Branch:** `cob/blocks-asset-building-docs`

#### Deliverables

1. **`apps/docs/blocks-asset-building/overview.md`** — cluster docs page per the docs-page convention (mirrors `apps/docs/blocks-financial-ar/overview.md` from the AR hand-off + the AR hand-off's §Docs section).

   Structure:
   - What this ships (cluster role)
   - Quick start (DI registration + Building.RegisterAsync example — pure narrative; no exhaustive code)
   - Architecture (link to ADR 0094 + 0088)
   - Key types (Building, BuildingType, IBuildingService, ILegacyPropertyImporter)
   - Naming + relationship to `blocks-properties` (legacy) and `blocks-assets` (UI catalog)
   - Algorithms (BuildingType mapping table from PropertyKind; renovation-tracking append-only semantics)
   - Related packages (foundation/Assets kernel; blocks-asset-foundation substrate; blocks-properties legacy; blocks-financial-ar capex consumer)
   - Future Layer-3 overlays that consume this package

2. **`packages/blocks-asset-building/README.md`** — package README. Mirrors `blocks-financial-ar/README.md` (which mirrors `blocks-financial-ledger/README.md`) — concise; links to apps/docs overview; cites ADR 0094.

3. **`packages/blocks-asset-building/NOTICE.md`** — Apache OFBiz attribution per ADR 0088 §2 + ADR 0094 §References. Cites OFBiz `accounting/FixedAsset` entity model as the inspiration for the Layer-1 polymorphism (the inspiration was at the substrate level; this Layer-2 package's entity shape derives from generic accounting patterns; OFBiz is the canonical permissive reference).

4. **`active-workstreams.md` row flip.** Update the W#75 row (via the `W75.md` source file per `feedback_never_add_workstream_rows_directly_to_ledger.md`) to `built` with the 5 PR numbers cited.

5. **Drop `cob-status-2026-05-XXTHH-MMZ-w75-asset-building-built.md`** beacon to `coordination/inbox/`.

#### Tests

None additional; doc-only PR.

#### Verification

- `apps/docs` site builds (if it has a builder; otherwise verify the page exists + renders in the doc preview tool).
- All cross-references compile (no broken ADR links; ADR 0094 + 0088 + 0015 + 0008 verified present).
- `grep -r "blocks-asset-building" apps/docs/` shows the new page entry.
- `active-workstreams.md` updated (via `W75.md`) + ledger renders correctly.

---

## Cross-cluster integration

### Composes (this hand-off depends on)

- `blocks-asset-foundation` (substrate; the source of `Asset`, `AssetId`, `AssetKind`, `IAssetRepository`, `IAssetLifecycleLog`, etc.) — **HARD DEPENDENCY** (Gate G1)
- `foundation/Assets` (kernel primitives consumed by substrate; this hand-off does not consume directly but inherits via substrate)
- `foundation-multitenancy` (`TenantId`, `IMustHaveTenant`) — every Building is tenant-scoped
- `foundation-persistence` (`ISunfishEntityModule` — the Building entity contributes to the host's DbContext via this pattern per ADR 0015)
- `blocks-properties` (LEGACY — read-side only; the adapter projects FROM canonical to legacy shape; PR 4 does NOT mutate `blocks-properties` types) — DEPENDENCY DIRECTION: this package depends on `blocks-properties` for the Property record type used in the adapter's projection method

### Consumed by (future consumers; this hand-off provides the surface for)

- **`blocks-asset-unit`** (Phase 4 sibling kind-extension) — Unit's `BuildingId` FK references this hand-off's `BuildingId`. When `blocks-asset-unit` lands, `Building.UnitCount` becomes a projection from the unit-cluster rather than a static field.
- **`blocks-property-apartment` / `blocks-property-shopping-center` / `blocks-property-storage` / `blocks-property-retail` / `blocks-property-office` / `blocks-property-mobile-park` / `blocks-property-light-industrial` / `blocks-property-mixed-use`** (Phase 5 + 7 Layer-3 overlays) — each overlay composes this hand-off's `Building` + sibling kind extensions.
- **`blocks-leases`** (existing; future migration per ADR 0094 OQ-6) — currently references `PropertyId`; eventually will additionally support `AssetId` per the OQ-6 (c) recommendation.
- **`blocks-inspections`** (existing) — `EquipmentConditionAssessment` chains via `EquipmentId` today; in future Layer-2 work, Building-level condition assessments will write to the substrate's `AssetCondition` log.
- **`blocks-maintenance`** (existing) — `WorkOrder.Equipment` currently references `EquipmentId`; in future Layer-2 work, building-scoped work orders will reference `AssetId` of kind `building`.
- **`blocks-financial-ar`** (built) — consumes `Asset.Acquired` event for capex JE posting (substrate-emitted on `RegisterAsync`).
- **`blocks-financial-ledger`** (built) — depreciation runs (via substrate `IAssetValuationService`) post JEs through `IJournalPostingService`.
- **`blocks-rent-collection`** (built) — consumes Building's address + unit count for rent-schedule binding.
- **`blocks-reports-*`** (Phase 1 sibling) — building-portfolio reports consume `IBuildingRepository.QueryByTenantAsync` + filter helpers.
- **`accelerators/anchor`** (Anchor app) — Anchor UI consumes `IBuildingRepository` (or the legacy `IPropertyRepository` via wrapper) for the property-list / property-detail screens.

---

## Pre-merge council requirements

**Substrate-only / Layer-2 derivative scope: NO mandatory pre-merge council per the W#34/W#35/W#36 cohort precedent.**

**Architect spot-check on PR 1 substrate-relationship (recommended, not mandatory):**

XO recommends a single architect (Opus 4.7 + xhigh) review the PR 1 diff for the discriminator-vs-inheritance correctness, the typed-alias `BuildingId : AssetId` pattern, and the kind-data-side-table separation. The review is preventive: if PR 1 ships with a substrate-extension leak (a Building-specific field added to the canonical `Asset` row instead of the side-table), every subsequent kind extension inherits the bug. Spot-check duration: ~30min; non-blocking on PR 1 merge if council is not available.

**Security council NOT required.** This package handles:
- Tenant-scoped data (read + write); enforced via `IMustHaveTenant` per ADR 0008
- No PII directly (Building's `Address` is a property-level address, not a person's home address); PII attached to a Building flows through `AssetParty` joins which reference Party records owned by `blocks-people-foundation` (encrypted-at-rest per W#37 / ADR 0068 there)
- No cryptographic operations
- No external network surfaces (the ERPNext importer reads file-based exports; no live API calls)

Standard COB self-audit applies per ADR 0028-A10:
- Spot-check each cited symbol by reading the actual substrate file (verify `Sunfish.Blocks.AssetFoundation.Models.Asset` exists with the expected shape)
- Verify negative-existence claims (`blocks-asset-building/` is greenfield; the directory truly does not exist on `origin/main` before PR 1)
- Verify the substrate's tests pass on the COB's branch before authoring PR 1's dependencies

---

## Idempotency-key catalog

Per `path-ii-crdt-schema-conventions.md` §11:

| Event / Action | Idempotency Key | Origin |
|---|---|---|
| Building registered | `building-registered:{assetId}` | substrate `Asset.Acquired` event |
| Building tombstoned/disposed | `building-disposed:{assetId}:{detectedAtUtc}` | substrate `Asset.Disposed` event |
| Building renovated | `building-renovated:{renovationRecordId}` | this package's `BuildingRenovationLog.AppendAsync` |
| Building transferred (ownership) | `building-transferred:{assetId}:{transferEventId}` | substrate `Asset.Transferred` event |
| Building written-off (rare) | `building-writeoff:{assetId}:{writeOffEventId}` | substrate `Asset.WrittenOff` event |
| ERPNext property import | `erpnext-property:{erpnextPropertyName}` | `IErpnextPropertyImporter.UpsertFromErpnextAsync` |
| Legacy `blocks-properties.Property` ingestion | `blocks-properties:{legacyPropertyId}` | `ILegacyPropertyToBuildingImporter` |

Each idempotency key is computed by the source service before emitting the event / writing the row; the substrate's append-only logs reject duplicate keys with a soft-failure return (per `blocks-financial-ar` precedent).

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `accounting/FixedAsset` entity model (Apache 2.0). The Layer-1 polymorphism is *inspired by* OFBiz's `FixedAsset` flat record with `fixedAssetTypeId` discriminator. The Layer-2 `Building` kind extension's field shape (Address + YearBuilt + TotalSquareFeet + ParcelNumber + BuildingType) derives from OFBiz's `fixedAsset` field set for the `FACILITY` type.

**Attribution requirements:**

1. The package's `.csproj` carries a NOTICE-file declaration:
   ```xml
   <PropertyGroup><NOTICEFile>NOTICE.md</NOTICEFile></PropertyGroup>
   ```
2. **`packages/blocks-asset-building/NOTICE.md`** (ships in PR 1 OR PR 5; XO recommends PR 1 for early visibility):

   The NOTICE.md captures, in plain prose:
   - This package's entity shapes derive from Apache OFBiz's `accounting/FixedAsset` + `FixedAssetMaint` entity models (Apache 2.0).
   - OFBiz version studied: v18.12.x (as of 2026-05-17).
   - The Sunfish implementation is original code under MIT License.
   - OFBiz entity-shape pattern reproduced with attribution per Apache 2.0 §4(c).

3. Source-header comments on `Building.cs`, `BuildingType.cs`, `BuildingRenovationRecord.cs` reference OFBiz in a one-line comment each.

### Clean-room only (copyleft)

Per ADR 0088 §2–§3, these sources were studied for *understanding only* and contribute NO code:

- **Snipe-IT** (AGPL-3.0) — IT asset tracking workflow. Read-for-understanding study of "asset checkout/checkin" + "consumable tracking" + "license/seat tracking" patterns. Used to inform the Layer-1 `AssetParty.role` vocabulary (`assigned-user` role specifically derives from Snipe-IT's "checked out to" relationship). Clean-room re-derivation; no code borrowed.
- **OpenMAINT** (AGPL-3.0) — facilities-asset workflow. Read for inspection → deficiency → work-order patterns. The Building's renovation log shape is informed by OpenMAINT's "maintenance event" entity, but the Sunfish shape is a clean-room re-derivation.
- **SAP S/4HANA Asset Management** (proprietary; documentation-only) — public docs studied for enterprise asset-management vocabulary (functional location, equipment master, maintenance plan, work order). Documentation references in the package README. No SAP source code accessed.

**Discipline check before merging any PR:**

1. No copyleft source was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from Snipe-IT / OpenMAINT / GnuCash / Beancount appear in the new code. (Spot-check by grep before merge.)
3. The clean-room schema in ADR 0094 + the substrate hand-off's design is the source of truth; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + records + repo) | ~17 | record fields; stable-code enums; projection helper; in-memory repo round-trip; tenant-boundary |
| PR 2 (kind-data side-table + renovation log) | ~21 | side-table CRUD; renovation append-only; cross-row Tier-1 invariants |
| PR 3 (IBuildingService) | ~20 | register happy path + every failure path; address-query; type-filter; renovation record + emit |
| PR 4 (legacy wrapper + ERPNext importer) | ~16 | adapter projection; legacy↔canonical mapping; importer idempotency; cancel-path |
| PR 5 (docs + ledger flip) | 0 | doc-only |
| **Total** | **~74 new tests** | |

### Cluster-level acceptance (PASS gate at end of PR 5)

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.AssetBuilding` package + every existing consumer (including `Sunfish.Blocks.Properties` legacy, which must continue to build because PR 4 only ADDS the adapter; it doesn't mutate legacy types).

**A2.** `dotnet test packages/blocks-asset-building/tests/` passes all ~74 new tests; `dotnet test packages/blocks-properties/tests/` passes all existing tests unchanged (zero regression).

**A3.** **End-to-end register round-trip.**
- Seed the substrate via `AddBlocksAssetFoundation()` + this package's `AddBlocksAssetBuilding()` in DI.
- Construct a `BuildingRegistration` for a triplex at "123 Main St" (BuildingType = `triplex`; YearBuilt = 1985; TotalSquareFeet = 2400; AcquisitionCost = 285000).
- Call `IBuildingService.RegisterAsync(registration, ct)`.
- Assert: `RegisterResult.Error == None`; canonical Asset exists with `Kind == "building"`, `Status == "active"`; side-table BuildingKindData exists with the populated fields; substrate's `IAssetLifecycleLog.GetByAssetIdAsync` returns one event of kind `acquired`.

**A4.** **Renovation round-trip.**
- After A3, call `IBuildingService.RecordRenovationAsync(buildingId, renovation)` with `RenovationYear = 2024, Scope = "Kitchen + master bath", EstimatedCost = 45000`.
- Assert: renovation log returns one row; `BuildingKindData.LastRenovatedYear == 2024`; substrate lifecycle log now has 2 events.

**A5.** **Type-filter portfolio query.**
- Register 5 buildings: 2 shopping-centers + 2 office + 1 retail-inline.
- Call `IBuildingService.FilterByBuildingTypeAsync(BuildingType.ShoppingCenter, tenant, ct)`.
- Assert: returns 2 buildings; both have `BuildingType == "shopping-center"`.

**A6.** **Legacy wrapper round-trip.**
- Construct a `Sunfish.Blocks.Properties.Property` record (legacy shape) for an apartment building.
- Call `LegacyPropertyToBuildingAdapter.LegacyToBuildingRegistration(...)` then `IBuildingService.RegisterAsync(...)`.
- Reverse: call `LegacyPropertyToBuildingAdapter.CanonicalToLegacy(...)` on the resulting Building.
- Assert: the round-tripped Property has the same DisplayName, Address, PropertyKind, AcquisitionCost, YearBuilt, TotalSquareFeet, TotalBedrooms, TotalBathrooms as the original (modulo PropertyId, which is now the canonical AssetId; this is expected per the wrapper precedent).

**A7.** **ERPNext importer round-trip.**
- Construct an `ErpnextPropertySource` (1 property, DocStatus=1, PropertyType=`Commercial`).
- Pre-seed the owner Party in `InMemoryPartyReadModel`.
- Call `IErpnextPropertyImporter.UpsertFromErpnextAsync(...)`.
- Assert: `ImportOutcome.Action == Inserted`; canonical Building exists with `BuildingType == "commercial"`; ExternalRef preserved.
- Call the importer again with the SAME source.
- Assert: `ImportOutcome.Action == Skipped` (idempotency).

**A8.** **Performance acceptance (deferred to Phase 1 close-out).**
- Register 1,000 synthetic buildings; query by type returns in < 500ms locally.
- Tighter Surface Pro 7 target (< 200ms) is the Phase-1 close-out acceptance, not THIS hand-off's.

**A9.** **Tenant-boundary acceptance.**
- Register 3 buildings as tenant A, 2 as tenant B.
- Verify any `IBuildingRepository.GetByIdAsync(buildingFromB.Id, ct)` invoked with tenant-A context returns null (or throws TenantBoundaryViolation, depending on substrate's policy).

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these during the workstream, halt + drop a `cob-question-*` beacon to `coordination/inbox/`:

### H1. `blocks-asset-foundation` substrate not built yet

**Pre-build checklist step 1** catches this. If the substrate hand-off (W#74) has not shipped, **STOP** — file `cob-question-2026-05-XXTHH-MMZ-w75-substrate-missing.md` requesting substrate sequence-up. The substrate is the explicit predecessor. Do NOT bundle substrate work into this hand-off (per OQ-1 XO recommendation).

### H2. Substrate surface incompatible with hand-off assumptions

If the substrate ships with type names / namespaces / surface area different from what this hand-off assumes (e.g., the substrate's `AssetKind` is an integer enum instead of stable string codes; `IAssetRepository.GetByExternalRefAsync` doesn't exist; etc.), **STOP** and file `cob-question-2026-05-XXTHH-MMZ-w75-substrate-surface-mismatch.md`. Naming the specific symbol/expectation that fails to compile against the substrate. **XO will revise this hand-off to match the substrate's actual shape; do not push through with adapter workarounds.**

### H3. `blocks-properties.Property` API stability (PR 4 — CRITICAL)

PR 4's adapter MUST preserve every existing consumer's call signature against `IPropertyRepository` + `Sunfish.Blocks.Properties.Models.Property` + `PropertyKind`.

**Halt conditions:**

(a) Any existing test in `packages/blocks-properties/tests/` fails after PR 4 lands → **STOP IMMEDIATELY** + file `cob-question-*`. Do NOT "fix" the test to match the new behavior — that's a breaking change masquerading as a refactor.

(b) Any external consumer in `apps/`, `accelerators/`, or other `packages/blocks-*` does not compile after PR 4 lands → STOP + file `cob-question-*`.

(c) The legacy `Property.Id` ULID format differs from the canonical `Asset.Id` ULID in a way that breaks consumer logic that depends on the Id format. (XO recommendation: preserve the legacy `PropertyId`'s underlying ULID by mapping it 1:1 with the canonical `AssetId`. The wrapper writes the canonical first; the legacy ID's ULID is identical to the canonical's.)

If PR 4 cannot meet the non-breakage constraint, **council-review the breaking-change surface** before merging.

### H4. `Sunfish.Blocks.Properties.PostalAddress` reference cycle

The proposed surface uses `Sunfish.Blocks.Properties.PostalAddress` as the address shape in `BuildingKindData`. This creates a project-reference from `blocks-asset-building` to `blocks-properties` — which is the OPPOSITE direction from the ideal layering (the new canonical surface should not depend on the legacy package).

**Mitigation options:**
- (a) Clone `PostalAddress` into `Sunfish.Blocks.AssetBuilding.Models.PostalAddress`. Then `blocks-properties.PostalAddress` becomes a type-alias for the new one in Phase 6. Trade-off: short-term code duplication.
- (b) Relocate `PostalAddress` to a neutral foundation-tier package (e.g., `Sunfish.Foundation.Geography`) in a separate one-PR workstream BEFORE this hand-off starts. Cleaner; adds 1–2h to the schedule.
- (c) Keep the project-reference from `blocks-asset-building → blocks-properties` for v1; flag the inversion in the package README as a known temporary state to be resolved in Phase 6.

**XO recommendation:** **(a) — clone PostalAddress into the new package.** Minimal blast radius; Phase 6 cleanup is mechanical (type-alias). The clone has no behavior change (PostalAddress is a pure value object).

**Halt condition:** if COB attempts approach (b) without explicit XO ratification, STOP + file `cob-question-*`. (b) widens this hand-off's scope into a separate foundation-tier workstream that needs its own design.

### H5. `IPartyReadModel` placement (PR 4 — ERPNext importer)

The ERPNext importer needs to resolve `OwnerPartyName → PartyId` via `IPartyReadModel`. The contract lives in `blocks-people-foundation` (per `party-model-convention.md` §4).

**Three possible states:**
- `blocks-people-foundation` IS shipped: import the canonical `IPartyReadModel` via `using Sunfish.Blocks.People.Foundation;`.
- `blocks-people-foundation` is NOT shipped: ship a local stub `IPartyReadModel` in `Sunfish.Blocks.AssetBuilding.LocalStubs` (mirroring the `blocks-financial-ar` precedent). When the canonical home lands, the import flips and the stub deletes. Zero behavior change.

**No halt.** Just proceed with whichever state is true at PR 4 authoring time.

### H6. `BuildingType` translation table from `PropertyKind`

The PropertyKind → BuildingType mapping is not strictly 1:1 (PropertyKind has fewer values than BuildingType in v1). PR 4 ships a deterministic translation table:

| Legacy PropertyKind | BuildingType (mapped) |
|---|---|
| `Residential` (and `TotalBedrooms` not specified) | `single-family` (with `LegacyTranslationDefault` warning) |
| `Residential` (and `TotalBedrooms == 2`) | `duplex` |
| `Residential` (and `TotalBedrooms == 3`) | `triplex` |
| `Residential` (and `TotalBedrooms == 4`) | `quadplex` |
| `Residential` (and `TotalBedrooms >= 5`) | `multi-family-5+` |
| `Commercial` | `commercial` |
| `Mixed` | `mixed-use` |
| `Industrial` | `industrial-flex` |
| (any other legacy enum) | `commercial` with warning |

**Halt condition:** if COB encounters a legacy PropertyKind value not in the legacy enum's documented set, **STOP** + file `cob-question-2026-05-XXTHH-MMZ-w75-property-kind-translation.md`. Do not invent translation mappings ad-hoc.

### H7. Substrate's `AssetLocation` shape uncertain at PR 2 authoring time

If the substrate's `AssetLocation` discriminated union ships with a different concrete shape than ADR 0094 §Layer 1 anticipates (e.g., the substrate ships `AssetLocation.Coordinates` instead of `AssetLocation.Physical`), PR 2's `BuildingKindData.PhysicalLocation` projection wiring needs adjustment. **Do NOT block; emit a `cob-question-*` ONLY if compilation fails.** Otherwise adapt PR 2 to the substrate's actual shape with no surface change to consumers.

### H8. Performance regression on substrate

If integrating `IAssetRepository` (the substrate) into the InMemoryBuildingRepository introduces a noticeable performance regression on the existing in-memory `blocks-properties` repository's benchmark (e.g., `GetByIdAsync` goes from O(1) dictionary lookup to O(n) Asset scan + side-table join), file `cob-question-*` and DO NOT proceed to PR 4 until XO ratifies the performance trade-off.

**Mitigation if surfaced:** the substrate's in-memory implementation should already provide O(1) lookup; this hand-off only adds a side-table lookup. If the side-table lookup becomes a bottleneck, introduce an in-memory index in PR 2 (rebuilds on Upsert). The Phase 6 SQLite/Postgres-backed impl resolves at the database layer.

### H9. CRDT idempotency-key collision

If the idempotency-key scheme produces collisions in practice (e.g., two `building-renovated` events from concurrent renovation workflows generate the same key), this is a substrate-level bug — file `cob-question-*` immediately. The substrate's append-only log is supposed to enforce unique keys at the persistence boundary.

### H10. ADR 0094 status flip during PR sequence

If CO accepts ADR 0094 mid-PR-sequence with modifications (e.g., OQ-9 settles on `blocks-asset-foundation` and a rename is needed), file `cob-question-*` to clarify. Do NOT proceed with renames or migrations from a `Proposed` ADR's draft surface to its `Accepted` surface without XO direction.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–5 merged to main** (sequentially as documented; PR 4 may parallelize with PR 3).
2. **Building register + project round-trip:** acceptance tests A3 + A6 pass.
3. **Renovation log works:** acceptance test A4 passes.
4. **Type-filter portfolio query:** acceptance test A5 passes.
5. **ERPNext importer round-trip:** acceptance test A7 passes (insert + idempotent re-insert = Skipped).
6. **Tenant-boundary acceptance:** acceptance test A9 passes.
7. **Performance acceptance:** acceptance test A8 passes (< 500ms local; deferred to Phase 1 close-out for Surface Pro 7 target).
8. **Tests pass:** ~74 new tests across the package + zero regression in existing `blocks-properties` tests.
9. **`apps/docs/blocks-asset-building/overview.md` published** (ships in PR 5).
10. **`active-workstreams.md`** row for W#75 updated to `built` with the 5 PR numbers (via `W75.md` source file per `feedback_never_add_workstream_rows_directly_to_ledger.md`).
11. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w75-asset-building-built.md`** beacon dropped.

When the PASS gate is met, the next Layer-2 + Layer-3 hand-offs can proceed:

- `blocks-asset-unit-stage06-handoff.md` (sibling Layer-2; sequenced parallel-eligible).
- `blocks-asset-equipment-stage06-handoff.md` (Layer-2; absorbs the EquipmentClass enum per OQ-11; Phase 6 wraps the legacy `blocks-property-equipment`).
- `blocks-asset-vehicle-stage06-handoff.md` (Layer-2; absorbs `VehicleMetadata` + `TripRecord` from W#61).
- `blocks-property-apartment-stage06-handoff.md` (first Layer-3 overlay; demonstrates composition pattern).
- `blocks-properties-phase6-migration-stage06-handoff.md` (Phase 6 — the actual swap of `blocks-properties.InMemoryPropertyRepository` to delegate-via-adapter).

---

## Future workstream queue (post-PASS)

When this hand-off ships built, the following hand-offs become eligible (each is a separate workstream; XO authors per priority queue depth):

### Sibling Layer-2 kind extensions (parallel-eligible)

1. **`blocks-asset-unit`** — Unit-within-building modeling. Adds `Unit.AssetId`, `parentBuildingId`, `UnitLabel`, `Bedrooms`, `Bathrooms`, `SquareFeet`, `Floor`, `IsHandicapAccessible`, `IsSubMetered`, `Amenities[]`. Hand-off effort: ~6–8h sunfish-PM; ~4 PRs.
2. **`blocks-asset-equipment`** — Equipment modeling; absorbs the legacy `EquipmentClass` enum per ADR 0094 OQ-11. Adds `Make`, `Model`, `SerialNumber`, `LocationInProperty`, `ExpectedUsefulLifeYears`, `WarrantyMetadata`. Hand-off effort: ~8–10h sunfish-PM; ~5 PRs (parallel to this hand-off's structure).
3. **`blocks-asset-vehicle`** — Vehicle modeling; absorbs the W#61 `VehicleMetadata` + `TripRecord` types currently in `blocks-property-equipment`. Adds `Vin`, `LicensePlate`, `OdometerReading`, `FuelType`, `VehicleClass`. Hand-off effort: ~10–12h sunfish-PM; ~5–6 PRs (includes W#61 type relocation).
4. **`blocks-asset-it`** — IT asset modeling. Adds `DeviceType`, `OsVersion`, `MacAddress`, `IpAssignment`, `EncryptionStatus`, `LastSecurityPatchAt`, `LicenseSeats`. Hand-off effort: ~6–8h sunfish-PM.
5. **`blocks-asset-land`** — Land/parcel modeling. Adds `Acreage`, `Zoning`, `Improvements[]`, `EnvironmentalAssessmentStatus`, `RecordedDeedRef`. Hand-off effort: ~5–7h sunfish-PM.
6. **`blocks-asset-intangible`** — Intangible assets (trademarks / patents / goodwill / customer-lists / non-compete agreements / internal software). Adds `IntangibleClass`, `AmortizationStart`, `AmortizationLifeYears`, `RegistrationNumber`, `Jurisdiction`. Hand-off effort: ~5–7h sunfish-PM.

These six can ship in parallel once `blocks-asset-foundation` is built — each is independent at the substrate level.

### First Layer-3 overlay (sequential)

7. **`blocks-property-apartment`** — First Layer-3 demonstration. Composes `blocks-asset-building` + `blocks-asset-unit` + `blocks-asset-equipment` + `blocks-leases` + `blocks-financial-ar` + `blocks-rent-collection`. Hand-off effort: ~12–16h sunfish-PM; ~6–8 PRs. Sequenced after `-unit` + `-equipment` Layer-2 ship.

### Phase 6 migration (sequenced after the Layer-2 wave)

8. **`blocks-properties-phase6-migration`** — Swap `blocks-properties.InMemoryPropertyRepository` to delegate-via-adapter; flip the legacy package's read path to the canonical surface. Adheres to ADR 0094 §Migration Discipline rules verbatim. Effort: ~12–16h sunfish-PM; council-review required (per Discipline Rule 9). Sequenced after `blocks-asset-building` ships + 1-quarter wrapper stabilization.
9. **`blocks-property-equipment-phase6-migration`** — Same migration pattern for equipment + the W#61 vehicle types. Effort: ~10–14h sunfish-PM. Sequenced after `blocks-asset-equipment` + `blocks-asset-vehicle` ship.

### Additional Layer-3 overlays (demand-driven)

10–17. `blocks-property-mobile-park`, `blocks-property-light-industrial`, `blocks-property-storage`, `blocks-property-shopping-center`, `blocks-property-retail`, `blocks-property-office`, `blocks-property-mixed-use`. Each is a separate workstream; sequenced per CO priority + Phase 2 commercial-scope LLC demand (per memory `project_phase_2_commercial_scope.md`).

18+. Non-property Layer-3 overlays: `blocks-fleet`, `blocks-rental-pool`, `blocks-it-asset`, `blocks-construction-depot`, `blocks-restaurant-equipment`, `blocks-medical-equipment`. Each is gated on explicit CO direction or accelerator-customer demand.

---

## Verification checklist (sunfish-PM-side; pre-merge)

Before merging any PR in this hand-off, verify the following:

### PR 1 verification

- [ ] `dotnet build` succeeds for `Sunfish.Blocks.AssetBuilding` solution-wide.
- [ ] All ~17 PR 1 tests pass.
- [ ] `grep -r "Sunfish.Blocks.AssetBuilding" packages/blocks-asset-building/ | wc -l` returns ≥ 8 (sanity check on namespace coverage across files).
- [ ] No `Sunfish.Foundation.Assets` (kernel) types are re-implemented in this package — substrate composition only.
- [ ] No `Sunfish.Blocks.AssetFoundation.Models.Asset` row schema is extended; kind-data is in side-tables.
- [ ] `BuildingId` is a typed alias of `AssetId` (verify both compile + map 1:1 in roundtrip test).
- [ ] `BuildingType` codes match the documented set verbatim (string comparison; no typos in either direction).

### PR 2 verification

- [ ] All ~21 PR 2 tests pass + zero regression on PR 1 tests.
- [ ] `BuildingKindData` carries its own version envelope independent of canonical Asset.
- [ ] `BuildingRenovationRecord` is append-only at the repository layer (no `UpdateAsync` method; only `AppendAsync` + tombstone-via-soft-delete).
- [ ] In-memory atomic write: a failing side-table write rolls back the canonical Asset write.

### PR 3 verification

- [ ] All ~20 PR 3 tests pass + zero regression on PR 1–2 tests.
- [ ] `RegisterAsync` emits `Asset.Acquired` event with payload populated (verify via in-memory event publisher's recorded events).
- [ ] `RecordRenovationAsync` emits `Asset.LifecycleEvent` with `kind: "renovated"` + bumps `LastRenovatedYear` only when the renovation year exceeds the current value.
- [ ] Address-based query is case-insensitive on city + state (cohort convention).
- [ ] Filter-by-type respects tenant boundary (negative test).

### PR 4 verification

- [ ] All ~16 PR 4 tests pass + zero regression on PR 1–3 tests.
- [ ] **CRITICAL: all existing `blocks-properties/tests/` tests pass unchanged.** If ANY existing test fails, HALT per Halt H3.
- [ ] `grep -r "Sunfish.Blocks.Properties" packages/ apps/ accelerators/` returns hits identical to pre-PR-4 (no consumer code broken; no new bindings introduced).
- [ ] The legacy → canonical Id mapping is 1:1 (verify via roundtrip test in adapter tests).
- [ ] The PropertyKind → BuildingType translation table is deterministic + complete (every legacy enum value has a documented target; unknown values default to `commercial` with warning log).
- [ ] ERPNext importer idempotency: re-import returns `Skipped` not `Inserted` (per AR-precedent verification).

### PR 5 verification

- [ ] `apps/docs/blocks-asset-building/overview.md` exists + renders.
- [ ] `packages/blocks-asset-building/README.md` exists + references ADR 0094 + 0088.
- [ ] `packages/blocks-asset-building/NOTICE.md` exists + cites OFBiz attribution.
- [ ] `active-workstreams.md` row for W#75 reads `built` with the 5 PR numbers (verify via the rendered ledger; the `W75.md` source file is the underlying authority).
- [ ] `coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w75-asset-building-built.md` beacon dropped.

### Cluster-level smoke (end of PR 5)

- [ ] Acceptance tests A1–A9 all pass.
- [ ] End-to-end scenario: register a shopping-center Building → record a renovation → query the lifecycle log returns 2 events → filter-by-type returns the building.
- [ ] Legacy-wrapper smoke: a `Sunfish.Blocks.Properties.Property` (legacy) round-trips through the adapter without field loss.
- [ ] ERPNext-importer smoke: a synthetic ERPNext property → register → re-import = skipped.

---

## Docs

**`apps/docs/blocks-asset-building/overview.md`** — cluster docs page ships in PR 5.

Structure (sketch):

- # blocks-asset-building
- ## Overview — Layer-2 kind extension over the generic Asset substrate per ADR 0094
- ## Quickstart — DI registration + `IBuildingService.RegisterAsync` example
- ## Key Types
  - `Building` (typed projection)
  - `BuildingId` (typed alias of AssetId)
  - `BuildingType` (stable string-code enum)
  - `IBuildingRepository` / `IBuildingService` / `IBuildingRenovationLog`
- ## Naming
  - Relationship to `blocks-properties` (legacy; wrapper pattern in Phase 6)
  - Relationship to `blocks-assets` (UI-only catalog; coexists)
  - Relationship to `Sunfish.Foundation.Assets` (kernel primitives; substrate composes)
- ## Algorithms
  - PropertyKind → BuildingType translation table
  - Renovation append-only pattern
- ## Related Packages
  - `blocks-asset-foundation` (substrate; predecessor)
  - `blocks-asset-unit` (Phase 4 sibling)
  - `blocks-asset-equipment` (Phase 4 sibling; absorbs legacy EquipmentClass)
  - `blocks-asset-vehicle` (Phase 4 sibling; absorbs W#61 VehicleMetadata + TripRecord)
  - `blocks-property-apartment` + other Layer-3 overlays (Phase 5 + 7)
  - `blocks-properties` (legacy; non-breaking wrapper coexists; Phase 6 migration target)

Cite: ADR 0094 §Layer 2 / §Architecture / OQ-9 / OQ-11; ADR 0088 §1; CRDT conventions §5 (stable codes); party-model-convention §3 (PartyRole for `AssetParty`).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `packages/foundation/Assets/` (kernel primitives; predecessor) ✓
- `packages/foundation-assets-postgres/` (kernel Postgres impl) ✓
- `packages/blocks-assets/` (UI-only catalog; coexists; NOT TOUCHED) ✓
- `packages/blocks-properties/Models/Property.cs` (legacy entity; PR 4 read-only wrapper target) ✓
- `packages/blocks-properties/Models/PropertyId.cs` ✓
- `packages/blocks-properties/Models/PropertyKind.cs` ✓
- `packages/blocks-properties/Models/PostalAddress.cs` (used in PR 1; clone-or-reference decision per Halt H4) ✓
- `packages/blocks-property-equipment/Models/Equipment.cs` (out of scope; mentioned for context only) ✓
- ADR 0094 (this hand-off's parent) ✓
- ADR 0088 §1 + §2 + §3 ✓
- `_shared/engineering/party-model-convention.md` §3 + §4 ✓
- `icm/02_architecture/path-ii-crdt-schema-conventions.md` §1, §2, §3, §4, §5, §10, §11 ✓

**Expected on origin/main BEFORE this hand-off starts (Gate G1):**

- `packages/blocks-asset-foundation/` — Layer-1 substrate (sibling workstream W#74 ships first per OQ-1)
- `packages/blocks-asset-foundation/Models/Asset.cs`, `Models/AssetId.cs`, `Models/AssetKind.cs`, `Models/AssetLifecycleEvent.cs`, `Models/AssetValuation.cs`, `Models/AssetCondition.cs`, `Models/AssetParty.cs`, `Models/AssetLocation.cs`
- `packages/blocks-asset-foundation/Services/IAssetRepository.cs`, `Services/IAssetLifecycleLog.cs`, `Services/IAssetValuationService.cs`, `Services/IAssetConditionService.cs`, `Services/IAssetHierarchyService.cs`

**Introduced by this hand-off** (ship across PRs 1–5):

- New package: `packages/blocks-asset-building/`
- New types: `BuildingId`, `BuildingType`, `Building`, `FoundationType`, `RoofType`, `BuildingKindData`, `BuildingKindDataId`, `BuildingRenovationRecord`, `BuildingRenovationRecordId`, `BuildingRegistration`, `RegisterResult`, `RegisterError`, `RecordRenovationResult`, `RecordRenovationError`, `LegacyPropertyTranslationResult`, `ErpnextPropertySource`, `BuildingProjection` (static helper), `LegacyPropertyToBuildingAdapter`, (possibly) `Sunfish.Blocks.AssetBuilding.Models.PostalAddress` (cloned per Halt H4 mitigation A)
- New services: `IBuildingRepository` + `InMemoryBuildingRepository`, `IBuildingKindDataRepository` + `InMemoryBuildingKindDataRepository`, `IBuildingRenovationLog` + `InMemoryBuildingRenovationLog`, `IBuildingService` + `BuildingService`, `IErpnextPropertyImporter` + `ErpnextPropertyImporter`, (local stub if needed) `IPartyReadModel` + `InMemoryPartyReadModel`
- Refactored: none (PR 4 ADDS to `blocks-properties` consumers via the adapter; it does not refactor existing types)
- Docs: `apps/docs/blocks-asset-building/overview.md`
- Attribution: `packages/blocks-asset-building/NOTICE.md`
- Ledger: `W75.md` (workstream source file) → `active-workstreams.md` row flipped to `built`

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check NEGATIVE existence too (verify `packages/blocks-asset-building/` truly does not exist on origin/main before opening PR 1; verify `blocks-asset-foundation` truly exists before importing).

---

## Cohort discipline

This hand-off is the **first Layer-2 kind extension hand-off under ADR 0094** (and the first cluster hand-off in the new `blocks-asset-*` family). The COB self-audit pattern applied to W#34 / W#35 / W#36 / W#39 / W#40 substrate hand-offs + the ledger + AR + people-foundation hand-offs applies here verbatim:

- **Two-overload constructor (audit-disabled / audit-enabled both-or-neither) pattern** for any DI extension that interacts with audit. Not required in this hand-off (no audit interaction beyond substrate-emitted lifecycle events; the substrate owns the audit boundary).
- **`AddBlocksAssetBuilding()` naming for the DI extension** — matches the cluster convention.
- **`apps/docs/{cluster}/overview.md` page convention** — applied in PR 5.
- **README.md at the package root** referencing ADR 0094 + 0088 — ship in PR 1.
- **`ConcurrentDictionary` dedup for any cache** — applied in `InMemoryBuildingRepository`, `InMemoryBuildingKindDataRepository`, `InMemoryBuildingRenovationLog`.
- **Strong-typed Id records** (ULID-backed via typed-alias of AssetId) — applied for `BuildingId`, `BuildingKindDataId`, `BuildingRenovationRecordId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — applied for `IPartyReadModel` if `blocks-people-foundation` isn't yet shipped (PR 4 only); relocates when the canonical home lands; DI swap with no public surface change.
- **NO substrate extensions.** Every new symbol in this hand-off is Layer-2 surface OR side-table OR Layer-2 service. The canonical Asset row + AssetKindData substrate are untouched.

---

## CRDT-friendly schema conventions deep-dive

This hand-off applies the cluster's CRDT-friendly conventions from `path-ii-crdt-schema-conventions.md`. Cross-referenced summary specific to building-as-a-Layer-2-extension:

### 1. AP vs CP classification per entity

Per `path-ii-crdt-schema-conventions.md` §1:

| Entity in this hand-off | Class | Rationale |
|---|---|---|
| `Building` (typed projection over canonical Asset) | **AP** (via substrate) | The underlying canonical Asset is AP per ADR 0094 §Per-record-class CP/AP classification. Building inherits. Rare-conflict in practice (building rarely concurrently modified). |
| `BuildingKindData` (side-table) | **AP** | LWW with HLC clock on scalar fields. Address + YearBuilt + TotalSquareFeet are unlikely to be concurrently mutated; LWW is acceptable. |
| `BuildingRenovationRecord` | **CP** (append-only) | Audit-bearing; immutability + ordering required; mirrors `AssetLifecycleEvent` discipline. Append-only sub-collection per CRDT conventions §4. |
| `AssetParty` rows for buildings (sourced from substrate; not introduced here) | **AP** (append-only-with-tombstones) | Inherited from substrate. |

### 2. Stable string codes (binding)

Per `path-ii-crdt-schema-conventions.md` §5:

- `BuildingType` codes are kebab-case, lowercase, never renamed. The v1 set: `single-family`, `duplex`, `triplex`, `quadplex`, `multi-family-5+`, `commercial`, `mixed-use`, `mobile-home`, `industrial-flex`, `storage`, `shopping-center`, `retail-inline`, `retail-pad`, `office`.
- `FoundationType` codes: `slab`, `crawl-space`, `basement-full`, `basement-partial`, `pier-and-beam`, `pilings`, `other`.
- `RoofType` codes: `composition-shingle`, `architectural-shingle`, `metal-standing-seam`, `tile-clay`, `tile-concrete`, `slate`, `flat-built-up`, `flat-modified-bitumen`, `tpo-membrane`, `epdm`, `green-roof`, `other`.
- Adding a new code is additive; renaming an existing code requires a deprecation cycle: ADD the new code; mark the old one with a deprecation log + maintain alias for one quarter; then remove.

### 3. ExternalRef as the idempotency key

Every persisted `Building` carries an `ExternalRef` (optional; populated by importers). The `(externalRef.source, externalRef.id)` tuple is the idempotency key on the canonical Asset row. Re-import = look-up + skip-or-update. Applied verbatim from the ledger / AR / people-foundation precedent.

### 4. Soft-delete tombstones

Per `path-ii-crdt-schema-conventions.md` §2:

- `BuildingKindData` carries soft-delete envelope (`deletedAt`, `deletedBy`, `deletedReason`).
- The canonical Asset's tombstone is managed by the substrate; setting the substrate's tombstone implicitly tombstones the kind-data side-table row (via the wrapper's atomic write).
- Hard `DELETE` is forbidden at the repository layer; only the substrate's `IAssetRepository.TombstoneAsync` reaches the persistence backend, and it sets the tombstone fields rather than deleting rows.

### 5. Version + revisionVector envelope

Per `path-ii-crdt-schema-conventions.md` §3:

- The canonical Asset carries its own `Version` + `RevisionVector` (substrate-managed; Loro-aware).
- The side-table `BuildingKindData` carries its own `Version` + `RevisionVector` independently — allowing kind-data updates to bump the side-table version without bumping the canonical Asset's version.
- `BuildingRenovationRecord` is append-only; the rows themselves don't carry a version envelope (per CRDT conventions §4: append-only records are immutable; envelope is unnecessary).

### 6. Append-only sub-collections (binding)

Per `path-ii-crdt-schema-conventions.md` §4:

- `BuildingRenovationRecord[]` is an append-only sub-collection of the Building. Never UPDATE; only INSERT new rows or set the soft-delete tombstone on a prior row.
- Corrections happen by appending a new record with `Scope = "Correction of #<priorRecordId>: ..."` and (optionally) a tombstone on the prior. The original record's data is preserved for audit.

### 7. State-machine resolution — substrate-owned

Per `path-ii-crdt-schema-conventions.md` §7:

- The canonical Asset's lifecycle status (`active` / `disposed` / `decommissioned`) is the substrate's state machine; this hand-off's `Building` projection reflects it but does not extend it.
- No additional state machine on `Building` itself. `BuildingType` is a discriminator, not a state.

### 8. Two-tier validation

Per `path-ii-crdt-schema-conventions.md` §10:

- Tier-1 write-time: `IBuildingRepository.UpsertAsync` invariants (TenantId non-default; BuildingType is a known stable code; YearBuilt ≤ CurrentYear+5; TotalSquareFeet > 0 if specified; etc.).
- Tier-2 post-merge: `IBuildingPostMergeReconciler` stub registered in PR 2; v1 always returns "no issues." The reconciler is the seam for future cross-row invariants (e.g., a building's `UnitCount` should equal the count of `blocks-asset-unit` rows with `parentAssetId == buildingId`; checked post-merge when both rows arrive from concurrent replicas).

### 9. Idempotency keys for all events

Per `path-ii-crdt-schema-conventions.md` §11:

Catalog reproduced in the §Idempotency-key catalog section above. Each event carries its own idempotency key; the substrate's event-bus dedups on the key.

### 10. Per-tenant isolation

Per `path-ii-crdt-schema-conventions.md` §14:

- Every Building carries `TenantId`; enforced at the repository read+write boundary.
- A `IBuildingRepository.GetByIdAsync(id)` call from tenant context A cannot return a Building with `tenantId == B`.
- In-memory repository filters by tenant; the SQLite / Postgres backing impl (follow-on) enforces via WHERE clauses.
- Cross-tenant queries are forbidden at the repository level; admin-tier cross-tenant queries (e.g., for a service-account fetching billing across all tenants) require an explicit superuser-scoped service that bypasses the per-tenant filter — out of scope for this hand-off.

---

## Event-bus catalog applied

Per `path-ii-cross-cluster-event-bus.md`, this hand-off emits and consumes:

### Emitted (producer: `asset` via substrate)

| Event | Consumer clusters | Payload | Idempotency key |
|---|---|---|---|
| `Asset.Acquired` (from substrate on Building.Register) | financial, reports, people | `{ assetId, kind: "building", acquisitionCost, acquisitionDate, tenantId, registeredByPartyId }` | `building-registered:{assetId}` |
| `Asset.Disposed` (from substrate on Building.Dispose) | financial, reports | `{ assetId, kind: "building", disposalReason, disposedByPartyId, disposalEventId }` | `building-disposed:{assetId}:{disposalEventId}` |
| `Asset.LifecycleEvent` with `kind: "renovated"` (this package) | maintenance, reports | `{ assetId, renovationRecordId, year, scope, estimatedCost, completedAt }` | `building-renovated:{renovationRecordId}` |
| `Asset.PartyAttached` (from substrate on AssetParty.Attach) | financial, people, work | `{ assetId, partyId, roleName, attachedAt }` | `asset-party-attached:{assetId}:{partyId}:{roleName}` |

The substrate is the canonical event publisher. This hand-off populates the kind-discriminating fields in the payload (kind = `"building"`); the substrate envelopes + emits.

### Consumed

Currently none. This hand-off does not subscribe to events from other clusters. Future versions (Layer-3 overlays consuming Building events; the `blocks-financial-ar` capex consumer; the maintenance cluster's renovation tracker) subscribe to the events above.

### Schema versioning

All event payloads ship at `schemaVersion: "1.0.0"`. Future additive fields → minor bump; renames or breaking changes → new event type per cross-cluster event-bus design §2 deprecation rules. Renames are forbidden.

### Envelope construction

Each emitted event is wrapped in the substrate's canonical event envelope (per `path-ii-cross-cluster-event-bus.md` §1). This hand-off does NOT introduce a new event envelope — it populates the substrate's envelope's payload field with kind-specific data.

---

## Cohort discipline (expanded)

This hand-off is the **first Layer-2 kind extension hand-off under ADR 0094**. Several conventions are established here that future Layer-2 hand-offs (`-unit`, `-equipment`, `-vehicle`, `-it`, `-land`, `-intangible`) will inherit:

### Convention E1 — Typed Id alias

Every Layer-2 kind extension introduces a typed Id alias for the canonical `AssetId`, constrained semantically to the kind. Naming: `{Kind}Id` (e.g., `BuildingId`, `UnitId`, `EquipmentId`, `VehicleId`). The underlying ULID is identical to the canonical `AssetId.Value`.

### Convention E2 — Typed projection record

Every Layer-2 kind extension introduces a typed projection record named `{Kind}` (e.g., `Building`, `Unit`, `Equipment`, `Vehicle`). The record is a *read-side projection* over a canonical `Asset` row of the corresponding kind plus the kind-data side-table.

### Convention E3 — Side-table for kind-specific data

Every Layer-2 kind extension introduces a side-table record named `{Kind}KindData` (e.g., `BuildingKindData`, `UnitKindData`, `EquipmentKindData`). The side-table is keyed by `AssetId` and carries the kind-specific fields. Atomic write of canonical Asset + side-table happens via the kind extension's repository.

### Convention E4 — Kind-specific service

Every Layer-2 kind extension introduces a service named `I{Kind}Service` (e.g., `IBuildingService`, `IEquipmentService`). The service ships `RegisterAsync` (atomic canonical Asset + side-table write + initial lifecycle event) + kind-specific query helpers + kind-specific lifecycle operations (e.g., `RecordRenovationAsync` for buildings; `RecordServiceAsync` for equipment; `RecordTripAsync` for vehicles).

### Convention E5 — DI extension naming

Every Layer-2 kind extension's DI extension is named `AddBlocksAsset{Kind}()` (e.g., `AddBlocksAssetBuilding`, `AddBlocksAssetUnit`). The extension carries a startup check that verifies `AddBlocksAssetFoundation()` was called first.

### Convention E6 — Apps/docs cluster page

Every Layer-2 kind extension publishes `apps/docs/blocks-asset-{kind}/overview.md`.

### Convention E7 — NOTICE.md attribution

Every Layer-2 kind extension carries a NOTICE.md citing the relevant FOSS attribution per ADR 0088 §2. For buildings: OFBiz `accounting/FixedAsset` (Apache 2.0).

### Convention E8 — Substrate stability

NO Layer-2 kind extension adds to the canonical `Asset` row schema, `AssetKind` enum (except for documented new kind codes), `AssetLifecycleEvent` event kinds (except for documented new kind-specific lifecycle events), or `AssetParty` role vocabulary (except documented additions). Substrate extensions go through the substrate's own workstream + ADR amendment if needed.

### Convention E9 — Legacy wrapper precedent

For Layer-2 kind extensions that have a corresponding legacy package (`blocks-asset-building` ↔ `blocks-properties`; `blocks-asset-equipment` ↔ `blocks-property-equipment`; `blocks-asset-vehicle` partially ↔ existing W#61 `VehicleMetadata` + `TripRecord` in `blocks-property-equipment`):
- Ship a *read-side adapter* in the Layer-2 kind extension hand-off (THIS hand-off does so in PR 4).
- Defer the full Phase 6 migration (the actual swap of the legacy package's repository to delegate-via-adapter) to a separate workstream.
- Follow the Migration Discipline rules from ADR 0094 §Migration Discipline — the Wrapper-Pattern Playbook.

### Convention E10 — Test coverage targets

Every Layer-2 kind extension targets:
- ~15–20 tests in the scaffold PR (records + repo + projection)
- ~20–25 tests in the side-table + lifecycle PR
- ~20 tests in the service-surface PR
- ~12–15 tests in the legacy-wrapper + importer PR

Total: ~70–80 tests per Layer-2 kind extension. THIS hand-off targets ~74 tests at the upper end of that band.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w75-asset-building-{slug}.md` in `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for W#75 (via the `W75.md` source file).
- `ScheduleWakeup 1800s` per loop discipline.

If COB completes PR 5 + the PASS gate is met:

- Update `active-workstreams.md` (via the source `W75.md` file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w75-asset-building-built.md` to inbox.
- Continue with the next hand-off in the Phase 3-equivalent queue — likely `blocks-asset-unit` (sibling Layer-2), `blocks-asset-equipment` (sibling Layer-2 absorbing legacy EquipmentClass per OQ-11), or `blocks-asset-vehicle` (sibling absorbing W#61 artifacts) — whichever XO has dropped next.

---

## Cross-references

- ADR 0094: `docs/adrs/0094-generic-asset-polymorphism-overlays.md`.
- ADR 0088 (parent decision): `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- ADR 0015 (module-entity registration pattern): `docs/adrs/0015-module-entity-registration.md`.
- ADR 0008 (multi-tenancy): `docs/adrs/0008-foundation-multitenancy.md`.
- ADR 0046 + amendments (foundation-recovery / kernel-security): `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` + `0046-a*`.
- Substrate hand-off (sibling; predecessor): `icm/_state/handoffs/blocks-asset-foundation-stage06-handoff.md` (to be authored as workstream W#74).
- CRDT conventions: `icm/02_architecture/path-ii-crdt-schema-conventions.md`.
- Cross-cluster event bus: `icm/02_architecture/path-ii-cross-cluster-event-bus.md`.
- Party convention: `_shared/engineering/party-model-convention.md` §3 (PartyRole), §4 (cross-cluster).
- Foundation kernel: `packages/foundation/Assets/README.md`.
- Cohort precedent hand-offs:
  - `blocks-financial-ar-stage06-handoff.md` (canonical hand-off template — followed verbatim where structure is mirror-able)
  - `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (substrate-then-consumer cohort precedent)
  - `blocks-people-foundation-stage06-handoff.md` (substrate-slice-first precedent for the `-foundation` naming OQ resolution)
  - `actor-principal-resolver-stage06-handoff.md` (typed Id + substrate-projection cohort precedent)
- Legacy package context:
  - `packages/blocks-properties/Models/Property.cs` (Phase 6 migration target; PR 4 read-side wrapper foundation)
  - `packages/blocks-property-equipment/Models/Equipment.cs` (out of scope; Phase 6 target via `blocks-asset-equipment` future hand-off)
  - `packages/blocks-assets/` (UI-only catalog; coexists)
- License posture: ADR 0088 §2 (MIT output) + §3 (clean-room copyleft discipline); package-level NOTICE.md (PR 1).

---

**End of hand-off.**
