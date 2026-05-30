---
id: 55
title: Dynamic Forms Substrate
status: Proposed
date: 2026-04-29
revised: 2026-05-30
revision: 3
tier: foundation
concern:
  - persistence
  - configuration
  - dev-experience
composes:
  - 1
  - 5
  - 7
  - 28
  - 32
  - 46
  - 49
  - 56
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0055 — Dynamic Forms Substrate

**Status:** Proposed (Rev 3 — scope settled 2026-05-30 by CIC ruling; awaiting dual-council attestation)
**Date:** 2026-04-29 (Rev 1) · **Revised:** 2026-05-30 (Rev 3)
**Resolves:** Synthesizes a multi-turn architectural conversation 2026-04-29 (CEO directive establishing dynamic forms as the load-bearing MVP feature) into a coherent substrate ADR. Composes 4 prior research artifacts: [`oss-primitive-types-research-2026-04-29.md`](../../icm/01_discovery/output/oss-primitive-types-research-2026-04-29.md) (PR #231), [`dynamic-forms-authorization-permissions-upf-2026-04-29.md`](../../icm/00_intake/output/dynamic-forms-authorization-permissions-upf-2026-04-29.md) (PR #230), [`contact-use-enum-upf-2026-04-29.md`](../../icm/00_intake/output/contact-use-enum-upf-2026-04-29.md) + [`taxonomy-management-substrate-intake-2026-04-29.md`](../../icm/00_intake/output/taxonomy-management-substrate-intake-2026-04-29.md) (PR #234), [`cross-field-rules-engine-upf-2026-04-29.md`](../../icm/00_intake/output/cross-field-rules-engine-upf-2026-04-29.md) (PR #235). Rev 2 rebases onto the [dynamic-forms substrate scoping study](../../icm/01_discovery/research/dynamic-forms-substrate-scoping-2026-05-30.md) (ONR, shipyard#208).

---

## Revision history

| Rev | Date | Status | Summary |
|---|---|---|---|
| 1 | 2026-04-29 | Proposed (never attested) | Original Option-A decision; 8-phase / ~16-week from-scratch build. **Status was internally inconsistent** — frontmatter said `Accepted`, but the body header and sign-off both said `Proposed, awaiting CEO sign-off after a council-review dispatch the sign-off recorded as not-yet-run.** No `council-verdict-0055-*` beacon exists anywhere in `coordination/` — the council review the sign-off promised never ran. The frontmatter `Accepted` was a scaffolding/template artifact, not a real acceptance. |
| 2 | 2026-05-30 | **Proposed** | **Reconciliation + re-scope.** (1) Status reset to a single consistent **Proposed** across frontmatter, body, and sign-off. (2) **Namespace/reuse plan rebased** onto ~5 weeks of substrate that shipped underneath Rev 1: the schema registry is the shipped **`Sunfish.Kernel.SchemaRegistry`** (NOT Rev 1's `Sunfish.Foundation.Schema`); the JSONB entity store is the shipped **`Sunfish.Foundation.Assets`** (NOT a net-new `IEntityInstanceStore`); the Taxonomy substrate is the **Accepted ADR 0056** (`foundation-taxonomy`). ~60% of Rev 1's "new" substrate is already built. (3) **First implementation wave re-scoped** to a minimal-v1 keystone — a facade/overlay/engine-slice OVER the existing substrate — relocating the `foundation-ships-office` `FormSchema` stub to canonical `Sunfish.Foundation.Forms`. (4) Full ERPNext DocType parity (authoring UX, Web Forms, Customize-Form, naming series, child tables, Tier-2/3 rules, Postgres adapter, typed-record migration) **deferred to Phase 2+** with an explicit boundary. (5) **Council posture: dual-council MANDATORY** (security-engineering + dotnet-architect), matching the ADR 0095–0101 substrate-tier cadence. |
| 3 | 2026-05-30 | **Proposed** | **Scope settled by CIC ruling 2026-05-30T1322Z** (`admiral-ruling-2026-05-30T1322Z-adr-0055-oq5-postgres-in-keystone-oq6-code-json.md`). (1) **OQ-5 — CIC OVERRODE the Rev 2 in-memory recommendation:** the keystone now ships a **Postgres-backed `IEntityStore` at its definition-of-done** — durable at the keystone, not a fast-follow. Rationale: the keystone is the required-module spine of all five reference bundles; CIC wants it durable at DoD rather than carrying a persistence migration as a separate later workstream. (2) **OQ-6 — CIC AFFIRMED Rev 2:** code/JSON-defined schemas for v1; no authoring UX in v1 (Phase-2). (3) Consequent scope move: the **Postgres/JSONB persistence adapter** and the **JSONB index strategy** (ONR Finding-8 risk #4) move OUT of the Phase-2+ deferred list and INTO the v1 keystone boundary; the dual-council reviews the index strategy as part of the keystone. Acceptance-provenance + tenant-isolation now land on real Postgres storage, not the in-memory shim. (4) **OQ-3 stays the council-ratifiable recommendation** (reference a registry `SchemaId`, do not inline the blob). Scope is now settled for the dual-council; status remains **Proposed** pending dual-green. |

> **Reconciliation note (load-bearing).** Rev 2 is what goes to council. Rev 1's `Accepted` frontmatter must not be trusted by any downstream artifact: the council dispatch its sign-off promised never ran, and no acceptance beacon exists. Any prior work that cited "per ADR 0055 acceptance" (e.g., `xo-ruling-T02-43Z`, the `cob-status` "W#55 Phase 5 (ADR 0055 Accepted)" line) was relying on an acceptance that did not happen with council attestation. The disposition is now, unambiguously, **Proposed pending dual-council attestation.**

---

## Context

Sunfish was conceived as a no-code platform for collaborative software, but the property-operations cluster (workstreams #16–#30, drafted 2026-04-28) treated Property + Equipment + Inspection + Lease as fixed C# records — a substrate-amnesia anti-pattern that ignored Sunfish's actual platform substrate (ADR 0001 Schema Registry, ADR 0005 Type Customization, `blocks-forms`, `blocks-businesscases`).

CEO redirected 2026-04-29: **dynamic forms is load-bearing.** Specifically:
- Properties + Equipment + Address + Inspection are not hard-coded entities; they are *type definitions* registered in the schema registry
- Asset structure is a tree node hierarchy (type-tree + instance-tree)
- Properties/value-objects compose: Address can be USAddress | EUAddress | MXAddress (variant), each with custom fields, optionally with LatLng
- Admin-defined types in v1; JSONB storage required from day 1
- Form authorization needs subform/section-level permissions (vendor sees vendor's section; not whole form)
- Cross-field rules need their own substrate (conditional / validation / computed)
- Cross-device sync of both schemas and entity instances is mandatory

**Rev 2 forcing function (CIC, 2026-05-29):** CIC elevated dynamic-forms to the **highest-leverage post-MVP substrate**, because `sunfish.blocks.forms` is a `requiredModules` entry of **all five** reference bundles — property-management, asset-management, project-management, facility-operations, AND acquisition-underwriting (verified on disk; the Rev 1 framing of "four non-PM bundles" undercounts). Every bundle declares forms required; every bundle readiness audit marks forms COMPLETE — because the **presentation block** (`blocks-forms`) exists. But that block renders only **statically-typed** model forms; the dynamic engine behind it is the `NoopFormSchemaStore`, which returns empty. So no bundle can author a user-defined type today. CIC's elevation names exactly this gap. It is also the explicit "CO directs canonicalization" forcing function that `xo-ruling-T02-43Z` named when it created the local stub.

This ADR specifies the substrate. Rev 2 reframes it from "the largest single architectural commit of the cluster work" (Rev 1) to **a minimal-v1 keystone that wires existing substrate together behind a form-engine facade** — because the schema registry, entity store, taxonomy, workflow, and macaroons it depends on have all shipped since Rev 1.

---

## Decision drivers

- **CEO directive 2026-04-29:** dynamic forms is load-bearing; admin-defined types in v1; JSONB required; section-based permissions; type tree + instance tree; cross-field rules separate substrate
- **CIC directive 2026-05-29:** dynamic-forms is the highest-leverage post-MVP substrate; it is the shared required-module spine of ALL FIVE reference bundles; the keystone is what unblocks bundle authoring surfaces
- **Substrate already shipped (the Rev 2 pivot):** the schema registry (`Sunfish.Kernel.SchemaRegistry`), the JSONB entity store (`Sunfish.Foundation.Assets`), the taxonomy substrate (ADR 0056 Accepted; `foundation-taxonomy`), the workflow primitive (`blocks-workflow`), the rule-event bridge (`foundation-rule-engine-event-bridge`), and macaroons (ADR 0032) are all present on `origin/main`. The keystone composes them; it does not re-author storage/schema/sync.
- **Multi-platform requirement:** rules + forms must run on Web (Photino+Blazor or browser), .NET Anchor desktop, future iOS/Swift — eliminates code-as-rule patterns
- **Local-first + sync:** schemas and entity instances must CRDT-sync across devices per paper §6; schema versioning per paper §7 epoch model (the registry already ships Epochs/Lenses/Upcasters)
- **Admin authoring (Phase 2+):** non-developer authors compose types + rules + permissions; UX must be approachable — but v1 accepts code/JSON-defined schemas; the no-code studio is the differentiator, not the unblock
- **Provider-neutrality:** ADR 0013 enforcement gate active; substrate must not couple to vendor SDKs
- **Highest-PII-risk substrate:** user-defined forms store arbitrary tenant-authored field shapes + section-based permissions + per-tenant schema isolation — security is non-optional, hence dual-council MANDATORY

---

## On-disk reuse inventory (Rev 2 — verified 2026-05-30 @ origin/main 87b7266)

Rev 1's "Affected packages → new" table listed 5 net-new foundation packages. On disk, 3 of the 5 already exist (under kernel/foundation namespaces Rev 1 didn't anticipate), the 4th (taxonomy) is a separate Accepted ADR + shipped package, and only the form engine + compounds are genuinely net-new. Each row below was verified by reading the on-disk surface.

| Rev 1 "new" package | On-disk reality (origin/main 87b7266) | Verdict |
|---|---|---|
| `Sunfish.Foundation.Schema` (schema registry) | **SHIPPED as `Sunfish.Kernel.SchemaRegistry`** (`packages/kernel-schema-registry/`; types namespaced `Sunfish.Kernel.Schema`). `ISchemaRegistry`: `RegisterAsync` (content-addressed CID, idempotent, `JsonSchema.Net`, draft 2020-12) + `GetAsync` + `ValidateAsync` (`SchemaValidationResult` with RFC-6901 pointer errors) + `ListAsync(tagFilter)` + `PlanMigrationAsync`/`MigrateAsync`. Dirs present: `Epochs/ Lenses/ Upcasters/ Migration/ Compaction/`. README maps to "ADR 0055 (dynamic-forms substrate; schema-registry consumer)." | **BUILT.** 0055 *consumes*, not re-authors. Bonus: the schema-evolution machinery Rev 1 deferred to v2/v3 (lenses/upcasters/epochs) is already present at the kernel tier. |
| `Sunfish.Foundation.Forms` (form engine) | **DOES NOT EXIST** (grep: no `namespace Sunfish.Foundation.Forms` anywhere). | **NET-NEW.** The keystone's genuine deliverable. |
| `Sunfish.Foundation.RuleEngine` (three-tier rules) | **PARTIAL — `foundation-rule-engine-event-bridge` exists** (rule-event integration seam). The Tier-2 (JsonLogic) evaluator is net-new; Tier-3 (Power Fx) was already deferred to v2 by Rev 1. Tier-1 (JSON-Schema if/then/else) comes **free** via the kernel registry's draft-2020-12 validation. | **PARTIAL.** |
| `Sunfish.Foundation.Compounds` (20 primitives) | **DOES NOT EXIST** as a named package. Some primitives exist piecemeal (`Money` per ADR 0051; `Address`/`Email`/`Phone` in `blocks-people-foundation`; `InternationalizedText` in foundation I18n). | **NET-NEW (partially scavengeable; v1 needs only a subset).** |
| `Sunfish.Foundation.Taxonomy` | **SHIPPED as `foundation-taxonomy`; ADR 0056 = Accepted.** Rev 1 referenced it as "future ADR." | **BUILT.** Reference ADR 0056. |
| JSONB Entity Store (`IEntityInstanceStore`) | **SHIPPED as `Sunfish.Foundation.Assets`** (`packages/foundation/Assets/`). `IEntityStore` (`CreateAsync(SchemaId, JsonDocument body, …)` idempotent mint / `GetAsync` with as-of `VersionSelector` / `UpdateAsync` append-version / `DeleteAsync` tombstone / `QueryAsync` stream) + `IVersionStore` (SHA-256 hash chain) + `IAuditLog` (hash-chained) + `IHierarchyService` (temporal split/merge/reparent tree). Each entity carries a `SchemaId`. In-memory as-shipped today; Postgres backend not yet built. | **BUILT (contract + in-memory tier).** This IS Rev 1's "JSONB Entity Store + tree-hierarchy." The Postgres/JSONB persistence adapter is the as-yet-unbuilt half. **Rev 3 (CIC OQ-5):** that adapter is now IN the v1 keystone definition-of-done (Rev 2 had deferred it to Phase 2 / Rev 1 Phase 3) — the keystone ships the durable backend, not just the in-memory tier. |
| State machines (forms drive workflow) | **SHIPPED as `blocks-workflow`** (`WorkflowDefinition`/`StateMachine<TState,TEvent>`/`IWorkflowRuntime`). | **BUILT.** The forms→workflow seam is wiring, not new substrate. |

### Correction to ONR's framing (trust-but-verify)

ONR's scoping study (shipyard#208) is accurate on every reuse claim above except one nuance the ADR must not propagate: **the shipped registry's `Schema` record does NOT carry a `SchemaStatus{Draft,Published,Deprecated,Withdrawn}` field.** The shipped `Sunfish.Kernel.Schema.Schema` record is *content-addressed and immutable* — `(SchemaId, JsonSchemaText, ParentSchemas, Migrations, Tags, ContentAddress, BlobThreshold?)`. There is no lifecycle enum on it; a registered schema is a frozen, CID-identified document. Consequently:

- The `FormDefinition` lifecycle (`Draft / Published / Archived`) **lives on the FormDefinition overlay layer**, NOT delegated to a registry status field that does not exist. Rev 1's recommendation to "align the form-definition lifecycle to the registry's `SchemaStatus`" rests on an enum that isn't there. **Decision pinned (Rev 2):** keep the stub's `Draft / Published / Archived` on `FormDefinition`; do not try to mirror a registry status. Publishing a form pins a specific immutable `SchemaId` (a content address); "evolving" the schema means registering a new CID and re-pointing the form-definition — the registry's immutability gives free versioning, but the *publication lifecycle* is the form layer's concern.
- Entity tenancy isolation is a consume-side concern: `IEntityStore.CreateAsync` does not take a `TenantId` parameter (entity identity is `(Scheme, Authority, Nonce, Issuer)`); tenant scoping is enforced at the form-engine boundary via the Authorization-facade `ITenantContext` (ADR 0091 R2 — NOT the MultiTenancy narrowed variant; the signal-bridge#34 CS0104 trap), not by an over-specified store-level `TenantId` arg.

These corrections are flagged here so the dual-council reviews the *as-shipped* contract, not Rev 1's sketch.

### The stub (what canonicalizes)

Exactly one stub surface carries `TODO-RELOCATE-WHEN-CANONICAL … ADR 0055` markers (5 source-level markers: 3 in `FormSchema.cs`, 2 in `IFormSchemaStore.cs`):

- `foundation-ships-office/Services/FormSchema.cs` — `FormSchema(Id, TenantId, Name, FormSchemaStatus{Draft,Published,Archived}, UpdatedAt, LastModifiedBy)` + `FormSchemaId` (UUIDv7).
- `foundation-ships-office/Services/IFormSchemaStore.cs` — 2-method interface (`GetByIdAsync`, `ListByTenantAsync`) + `NoopFormSchemaStore` no-op default.

**Consumers of the stub (grep-verified):** `blocks-ships-office` (`ShipsOfficeServiceCollectionExtensions.cs`, `ShipsOfficeDataProvider.cs`, `ShipsOfficeDocumentKind.cs` — the `DynamicTemplate` kind) + tests. **One consuming surface, all inside `blocks-ships-office`.** The relocation blast radius is small (5 files + tests). The `FormSchemaStatus → DocumentStatus` mapping in `ShipsOfficeDocumentKind.cs` is a **guard for the sweep PR** — it must be preserved across the relocation.

---

## Considered options

Rev 1's option analysis stands (the schema-registry-driven dynamic substrate is the right architecture). Rev 2 adds an axis Rev 1 could not have considered: **build-vs-consume**, because the substrate Rev 1 would have built has since shipped.

### Option A — Schema-registry-driven dynamic substrate (chosen; rebased in Rev 2)

Build a foundation-tier substrate that defines types as data (JSON Schema 2020-12 + Sunfish overlay), stores instances as JSONB documents, renders forms automatically, evaluates cross-field rules, and enforces section-based permissions via macaroons.

**Rev 2 refinement:** *consume* the shipped registry + entity store + taxonomy + workflow + macaroons rather than re-authoring them. The net-new is the form engine + overlay + a Tier-2 evaluator + a compounds subset + a dynamic render component. Existing typed records (Property, Equipment, Inspection) remain developer-ergonomic projections; their migration to JSONB is **decoupled** to a follow-on ADR (see "What this ADR does NOT do").

- **Pro:** Aligns with the paper's no-code-platform vision
- **Pro:** Admin-defined types in v1 unblocked
- **Pro (Rev 2):** Composes *already-shipped* substrate — materially smaller than Rev 1's 16 weeks
- **Con:** The JSONB-vs-typed query ergonomics trade-off persists — and is now an in-scope v1 concern (Rev 3 — CIC OQ-5 moved the Postgres adapter + JSONB index strategy into the keystone DoD; the dual-council reviews it)

### Option B — Keep fixed schemas; add dynamic forms as v2

Rejected (Rev 1). CEO directive precludes; CIC's keystone elevation re-confirms.

### Option C — Hybrid: typed records + parallel JSONB path

Rejected (Rev 1). Split-brain UX; defers the migration cost without saving total work.

### Option D — Full event-sourced substrate

Rejected (Rev 1). Reserved as v3+. (Note: the shipped `IVersionStore` already provides an append-only SHA-256 hash chain per entity, so a degree of event-fidelity is present without re-architecting reads around events.)

**Verdict:** Adopt Option A, rebased onto shipped substrate per Rev 2.

---

## Decision

**Adopt Option A — a foundation-tier dynamic-forms substrate that composes the shipped kernel/foundation primitives, delivered as a minimal-v1 keystone with full DocType parity deferred to Phase 2+.**

### Canonical namespace home

**`Sunfish.Foundation.Forms`** (new `packages/foundation-forms/` package) is the canonical home for the form engine, definition, overlay, and store. The `foundation-ships-office` stub relocates here; `blocks-ships-office` re-references the canonical types; the `TODO-RELOCATE-WHEN-CANONICAL` markers are swept.

### Substrate components (Rev 2 — composition view)

```
┌──────────────────────────────────────────────────────────────────────┐
│                  Dynamic Forms Substrate (Sunfish.Foundation.Forms)    │
│                                                                        │
│   NET-NEW (the keystone):                                             │
│   ┌────────────────────┐   ┌──────────────────────────────────────┐  │
│   │ FormDefinition +   │   │ IFormEngine                          │  │
│   │ FormOverlay        │   │  RenderAsync / ValidateAsync /       │  │
│   │ (sections, field-  │   │  SaveAsync (resolve schema+overlay → │  │
│   │  overlays, rules,  │   │  render/validate/save; section-      │  │
│   │  SchemaId ref)     │   │  filtered by macaroon scope)         │  │
│   └─────────┬──────────┘   └──────────┬───────────────────────────┘  │
│             │   composes (does NOT re-author):                       │
│   ┌─────────▼───────────────────────────────────────────────────┐    │
│   │  SHIPPED substrate                                          │    │
│   │  • Sunfish.Kernel.SchemaRegistry  (validation core; CID;     │    │
│   │    JSON-Schema 2020-12; Epochs/Lenses/Upcasters/Migration)   │    │
│   │  • Sunfish.Foundation.Assets  (IEntityStore JSONB body +     │    │
│   │    IVersionStore SHA-256 chain + IAuditLog + IHierarchyService)│   │
│   │  • foundation-taxonomy (ADR 0056) — Coding/CodeableConcept   │    │
│   │  • blocks-workflow — forms→workflow transitions (wiring)     │    │
│   │  • foundation-rule-engine-event-bridge — rule-event seam     │    │
│   │  • ADR 0032 macaroons — section-scope enforcement            │    │
│   │  • ADR 0091 R2 ITenantContext (Authorization facade) —       │    │
│   │    per-tenant isolation                                      │    │
│   └─────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
```

### Initial contract surface (Rev 2 — recommendation for council to ratify)

The cleanest framing, consistent with the shipped registry: **separate the validation core from the form overlay.** The validation core is a registered JSON Schema (CID-addressed, immutable, lens/upcaster/epoch-aware) — it already exists in `Sunfish.Kernel.SchemaRegistry`. The *form* layer is a thin overlay that **references** a registered schema by `SchemaId`. Do NOT inline the JSON-Schema blob on the form definition.

```csharp
namespace Sunfish.Foundation.Forms;

// The canonical type — relocates the foundation-ships-office stub's FormSchema.
public sealed record FormDefinition
{
    public required FormDefinitionId Id { get; init; }          // UUIDv7 (relocate FormSchemaId)
    public required TenantId TenantId { get; init; }            // multi-tenant isolation (IMustHaveTenant)
    public required string Name { get; init; }                  // kept from stub
    public required SchemaId SchemaId { get; init; }            // → kernel registry; the JSON-Schema blob
                                                                //   lives THERE (CID), not inlined here
    public required FormDefinitionStatus Status { get; init; }  // Draft/Published/Archived — form-LAYER lifecycle
                                                                //   (the registry record has NO status field)
    public required FormOverlay Overlay { get; init; }          // sections + field overlays + rules + i18n
    public required DateTimeOffset UpdatedAt { get; init; }     // kept from stub
    public required ActorId LastModifiedBy { get; init; }       // kept from stub
}

public enum FormDefinitionStatus { Draft, Published, Archived } // preserve the stub's enum + DocumentStatus mapping

public sealed record FormOverlay
{
    public required IReadOnlyList<FormSection> Sections { get; init; }            // Approach F permission unit
    public required IReadOnlyDictionary<string, FieldOverlay> Fields { get; init; } // control hint, i18n, PII tag
    public required IReadOnlyList<RuleDefinition> Rules { get; init; }            // Tier-1 only in v1
    public InternationalizedText? Title { get; init; }
    public InternationalizedText? Description { get; init; }
}

public sealed record FormSection
{
    public required string Id { get; init; }
    public required InternationalizedText Title { get; init; }
    public required IReadOnlyList<string> Fields { get; init; }
    public required SectionAccess Access { get; init; }
}

public sealed record SectionAccess
{
    public required IReadOnlyList<string> ReadRoles { get; init; }
    public required IReadOnlyList<string> WriteRoles { get; init; }
    public string? ReadConditionExpression { get; init; }   // Tier-1 conditional (JSON-Schema if/then/else)
}

public sealed record FieldOverlay
{
    public ControlHint? Control { get; init; }              // date-picker, address-card, map-pin, …
    public InternationalizedText? Label { get; init; }
    public PiiSensitivity Pii { get; init; }                // per-field PII tag (risk 1 mitigation)
}

public interface IFormEngine
{
    // schema + overlay + instance → section-filtered view (filtered by macaroon scope)
    Task<FormView> RenderAsync(FormDefinitionId form, EntityId? instance, CapabilityToken token, CancellationToken ct);
    // registry validation (free Tier-1 if/then/else) + overlay Tier-1 rules
    Task<ValidationResult> ValidateAsync(FormDefinitionId form, JsonDocument candidate, CapabilityToken token, CancellationToken ct);
    // validate → write through Sunfish.Foundation.Assets.IEntityStore (carries the SchemaId)
    Task<EntityId> SaveAsync(FormDefinitionId form, JsonDocument candidate, CapabilityToken token, CancellationToken ct);
}

// The canonical IFormSchemaStore — relocates the stub's interface + NoopFormSchemaStore default.
public interface IFormDefinitionStore
{
    Task<FormDefinition?> GetByIdAsync(FormDefinitionId id, CancellationToken ct = default);
    Task<IReadOnlyList<FormDefinition>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
}
```

**On the fields `xo-ruling-T02-43Z` withheld:**
- **JSON-schema blob** → NOT inlined on `FormDefinition`. Stored in the registry (CID-addressed) and referenced by `SchemaId`. Cleaner than Rev 1's inline `JsonSchema { get; init; }` and avoids duplicating the registry's job.
- **Field-level metadata** → lives in `FormOverlay.Fields` (UI hints, i18n, PII-sensitivity tag). Validation/type metadata lives in the JSON Schema in the registry.
- **Revision history / versioning** → the registry provides schema versioning (CID + Migrations + lenses/upcasters/epochs); entity *instances* version independently via `Sunfish.Foundation.Assets.IVersionStore` (SHA-256 chain). **Do NOT build a third versioning mechanism** on `FormDefinition`. The form-definition pins a `SchemaId` (immutable CID); "evolving" = register a new CID + re-point.

### Key architectural commitments (Rev 2)

1. **Consume, don't rebuild.** The form engine composes `Sunfish.Kernel.SchemaRegistry` (validation core) + `Sunfish.Foundation.Assets.IEntityStore` (JSONB instance store) + `foundation-taxonomy` (ADR 0056) + ADR 0032 macaroons. It does not re-author schema/storage/sync.
2. **Reference, don't inline.** `FormDefinition` stores a `SchemaId` reference (ADR 0005 Layer-3 RFC-7396 merge-patch overlay semantics over a registry-referenced schema). The schema blob is the registry's responsibility.
3. **Form-layer lifecycle.** `FormDefinitionStatus { Draft, Published, Archived }` lives on `FormDefinition` (the registry record is immutable/content-addressed and has no status field). Preserve the `blocks-ships-office` `DynamicTemplate → DocumentStatus` mapping across the relocation.
4. **Section-based permissions (Approach F).** Each section declares ReadRoles + WriteRoles; the form engine intersects sections with the actor's macaroon scope (ADR 0032). Field-level annotations as escape hatch (Phase 2). Vendor/tenant magic-link section scoping works naturally — the token grants section access. **This is the security-load-bearing half.**
5. **Tier-1 rules free; Tier-2/3 phased.** Tier 1 (JSON-Schema if/then/else for visibility/required/readonly) comes via the registry's 2020-12 validation. Tier 2 (JsonLogic cross-field validation) is Phase 2 net-new on the `foundation-rule-engine-event-bridge` seam. Tier 3 (Power Fx computed) was already v2 in Rev 1.
6. **Postgres-backed `IEntityStore` at the keystone DoD (Rev 3 — CIC ruling 2026-05-30T1322Z, OQ-5).** The keystone ships durable: a Postgres/JSONB persistence adapter for `Sunfish.Foundation.Assets.IEntityStore` is part of the v1 definition-of-done, NOT a fast-follow. CIC OVERRODE the Rev 2 in-memory recommendation because the keystone is the required-module spine of all five reference bundles and must be durable at DoD rather than carry a persistence migration as a later workstream. The v1 query path runs on real Postgres; the JSONB index + JsonPath strategy is therefore an in-scope v1 concern the dual-council reviews (see risk 4). An in-memory `IEntityStore` remains available as a test/dev double, but it is not the v1 production target.
7. **Per-tenant isolation via the Authorization facade.** Every `FormDefinition` + entity instance is `IMustHaveTenant`; tenant scoping resolves via the **Authorization-facade `ITenantContext`** (ADR 0091 R2 — NOT the MultiTenancy narrowed variant; the signal-bridge#34 CS0104 trap is the specific miss to avoid). Cross-tenant schema/instance bleed is the catastrophic failure mode.
8. **PII tagging is mandatory at the storage boundary.** PII-tagged fields (`FieldOverlay.Pii`) persist tenant-key-encrypted via Foundation.Recovery (ADR 0046 / PR #223); audit emission on PII-field access. The keystone must NOT ship a form-storage path that bypasses PII tagging.
9. **Provider-neutrality preserved.** Form engine + overlay + rule evaluator are vendor-SDK-free per ADR 0013.
10. **Forms→workflow is wiring.** A form `SaveAsync` can raise an event the `blocks-workflow` `IWorkflowRuntime` consumes (via the `foundation-rule-engine-event-bridge` seam). No new substrate.

---

## Scope + phasing (Rev 2 — minimal-v1 keystone vs. full DocType parity)

### MINIMAL-V1 KEYSTONE (the deliverable that unblocks bundle authoring surfaces)

1. **`Sunfish.Foundation.Forms` package** — relocate the `foundation-ships-office` stub here as canonical `FormDefinition` + `IFormDefinitionStore` + `FormDefinitionId`; sweep the 5 `TODO-RELOCATE` markers; preserve the `blocks-ships-office` `DynamicTemplate → DocumentStatus` mapping.
2. **`FormOverlay` model** — `Sections` (with `SectionAccess` ReadRoles/WriteRoles) + per-field `FieldOverlay` (control hint, i18n label, PII tag) + `RuleDefinition[]` (Tier-1 only). Keyed to a registered `SchemaId` (ADR 0005 Layer-3 RFC-7396 merge-patch over the registry-referenced schema; store the **reference**, not the blob).
3. **`IFormEngine` thin slice** — `RenderAsync` (schema + overlay + instance → section-filtered view via macaroon scope) + `ValidateAsync` (registry validation + Tier-1 rules — free via the registry's 2020-12 if/then/else) + `SaveAsync` (validate → write through `IEntityStore`). Writes through the **Postgres-backed `IEntityStore`** (item 4) — durable at the keystone, per CIC ruling 2026-05-30T1322Z (OQ-5).
4. **Postgres/JSONB persistence adapter for `IEntityStore` + JSONB index strategy (Rev 3 — in keystone DoD).** A Postgres/JSONB backend for `Sunfish.Foundation.Assets.IEntityStore` (entity-as-JSON-body, version chain, audit log, hierarchy), with the JSONB index + JsonPath query strategy (ONR Finding-8 risk #4) defined as part of v1. CIC ruled (OQ-5) the keystone ships durable, not on the in-memory shim. The dual-council reviews this index strategy (see risk 4). The in-memory store remains a test/dev double.
5. **Minimal compounds subset** — only the field types the bundles need: string / number / date / select / bool / `Money` (ADR 0051) / `Reference` / `Attachment` / `InternationalizedText` / taxonomy `Coding` (ADR 0056). Reuse existing primitives; do not ship the full 20-primitive catalog.
6. **`blocks-forms` dynamic render component** — a new component (or a `FormBlock` dynamic mode) consuming a `FormDefinition`; the presentation block stays the consumer it already is.
7. **Section-based permissions** (ADR 0032 macaroons, Approach F) — the security-load-bearing half; enables vendor/tenant section-scoped magic-link forms.

### EXPLICITLY DEFERRED to PHASE 2+ (full ERPNext DocType parity — NOT the keystone)

The v1/Phase-2 boundary is crisp: **v1 = "an admin can define a new type (via code/JSON schema) and the bundles can render/validate/save instances of it durably on Postgres, with section-scoped permissions." Phase 2+ = "ERPNext Form Builder."**

- **Admin authoring UX** — schema editor / field-list builder / section-role editor / visual rule builder. v1 accepts code/JSON-defined schemas; the no-code studio is the differentiator, not the unblock. (CIC affirmed this — OQ-6, ruling 2026-05-30T1322Z.)
- **Customize-Form overlays** — tenant field-add on existing types (ADR 0005 Layer-3 RFC-7396 merge-patch) + 3-way-merge upgrade UX. (v1 = admin-defined NEW types only.)
- **Web Forms / public-facing surfaces** — anonymous/external submission; security-heavy (anti-CSRF + rate-limit + Turnstile per ADR 0096). Explicitly OUT of the keystone.
- **Naming series** (`AutoNumber` compound).
- **Child-table grid authoring** — full grid editor; v1 represents child collections as JSON arrays in the entity body.
- **Tier-2 JsonLogic cross-field validation** + **Tier-3 Power Fx computed fields** (Tier-3 was already v2 in Rev 1).
- **Full 20-primitive compound catalog** (v1 = bundle-needed subset).

> **Moved INTO the keystone by CIC ruling 2026-05-30T1322Z (OQ-5):** the **Postgres/JSONB persistence adapter for `IEntityStore`** and its **JSONB index / JsonPath query strategy** were Phase-2-deferred in Rev 2; Rev 3 relocates both into the v1 keystone definition-of-done. See "Minimal-V1 keystone" item 4 and risk 4.
- **Migration of shipped-cluster typed-records to JSONB** (Property / Equipment / Inspection — Rev 1 Phase 7). **Decoupled** from the keystone: the keystone serves NEW admin-defined types; retiring typed-records is a separate follow-on ADR with its own large migration risk. Do NOT couple them.

### Recommended phase cut (rebased onto shipped substrate)

| Phase | Scope | Reuses (shipped) | Net-new | Council |
|---|---|---|---|---|
| **P1 — Canonical relocation + def/overlay** | `Sunfish.Foundation.Forms` package; `FormDefinition` + `FormOverlay` + `FormSection` + `SectionAccess`; relocate stub; sweep TODOs; preserve `DynamicTemplate → DocumentStatus` mapping | kernel-schema-registry (validation core), foundation-multitenancy / ADR 0091 R2 facade | the package + def + overlay types | **dual** |
| **P2 — Form engine slice + Postgres-backed store** | `IFormEngine` render/validate/save over registry + **Postgres-backed `IEntityStore`** (Rev 3 — CIC OQ-5); JSONB index/JsonPath strategy; Tier-1 rules; section-permission filtering via macaroons; PII-tag enforcement at the storage boundary | kernel-schema-registry, `Sunfish.Foundation.Assets.IEntityStore`, ADR 0032 macaroons, ADR 0046 Recovery, foundation-taxonomy | engine impl + Tier-1 wiring + Postgres/JSONB adapter + index strategy | **dual** |
| **P3 — Compounds subset + blocks-forms dynamic render** | bundle-needed field types; `blocks-forms` dynamic component | Money (0051), people-foundation primitives, taxonomy (0056), `blocks-forms` presentation | compounds subset + 1 Razor component | security-engineering (PII-tag handling) |
| **P4+ — full DocType parity (roadmap)** | authoring UX, Web Forms, Customize-Form overlays, Tier-2/3 rules, naming-series, child-table grids, typed-record migration | (per-item) | (per-item; each its own unit) | per-item; Web-Forms + typed-record migration = **dual** |

**Sizing (MEDIUM confidence; NOT Engineer-validated).** P1–P3 (the keystone) is materially smaller than Rev 1's 16-week estimate — plausibly a handful of substantive PRs — **because the schema registry, entity store contract, taxonomy, workflow, and macaroons are all already shipped.** The point is directional: the keystone is a facade + overlay + engine-slice over existing substrate, not a from-scratch storage/schema/sync build. **Rev 3 adds genuinely net-new work to the keystone:** the Postgres/JSONB persistence adapter + index strategy for `IEntityStore` (CIC OQ-5), which Rev 2 had deferred. That increases the keystone's P2 sizing relative to Rev 2's in-memory cut, but the entity-store *contract* (CRUD/version/audit/hierarchy) is already defined — the net-new is the durable backend behind it, not a new contract. Final sizing is Engineer's to confirm at Stage 05.

---

## Consequences

### Positive

- **Aligns with platform vision.** The no-code-platform vision becomes load-bearing, day 1.
- **Admin-defined types in v1.** Bundle authors can define new types (custom equipment classes, custom property attributes) without code changes.
- **Unblocks all five bundles.** Forms-the-dynamic-substrate is the one missing piece behind the universal `requiredModules` entry.
- **Smaller than Rev 1.** Composing shipped substrate cuts the keystone from ~16 weeks to a handful of PRs.
- **Cross-platform parity natural.** Schema + rules are data; same engine on Mac / Windows / future iOS.
- **Audit-trail uniform.** Schema + entity changes emit to the ADR 0049 substrate (via `IAuditLog` hash chain).
- **Permissions structurally aligned.** Section-based (Approach F) composes ADR 0032 macaroons natively.
- **Provider-neutrality preserved.** No vendor SDK leakage; ADR 0013 satisfied.

### Negative

- **JSONB query ergonomics weaker than typed.** Strongly-typed LINQ queries lose; the keystone must define JSONB indexes + JsonPath query strategy as part of v1 (Rev 3 — CIC OQ-5 moved the Postgres adapter into the keystone DoD, so this is an in-scope v1 concern the dual-council reviews, no longer deferred).
- **Authoring UX is significant scope** — but explicitly Phase 2+, not v1; the risk is conflating the keystone with the studio (see risk 6).
- **Tier-2/3 rules deferred** — cross-field validation (JsonLogic) and computed fields (Power Fx) wait for Phase 2; Tier-1 (if/then/else) covers visibility/required/readonly in v1.
- **Typed-record migration deferred** — Property/Equipment/Inspection remain typed projections until a separate follow-on ADR; the keystone serves only NEW admin-defined types in v1.

### Trust impact / Security & privacy

- **Arbitrary-field PII is the highest risk.** Mitigated by per-field PII tagging + tenant-key encryption (ADR 0046) + audit on PII-field access; the keystone must not ship a storage path bypassing tagging.
- **Per-tenant schema isolation.** One tenant's schema must never validate/render against another's data; resolved at the form-engine boundary via the Authorization-facade `ITenantContext`. **Rev 3 (CIC OQ-5):** tenant isolation and acceptance-provenance now land on real Postgres/JSONB storage, not the in-memory shim — the tenant-filtering and PII-encryption guarantees must hold against the durable adapter the keystone ships, which the dual-council reviews directly rather than against an in-memory placeholder.
- **Section-based permissions are macaroon-enforced** — cryptographically verifiable per ADR 0032; no policy-engine round-trip.
- **The registry's content-addressing gives publication immutability** — a published form pins an immutable CID; a "schema change" is a new CID, so a buggy edit cannot retroactively mutate a published form's validation.

---

## Risks (Rev 2 — folded from ONR Finding 8; to be addressed by the dual-council)

1. **Arbitrary-field PII (highest).** User-defined forms can store arbitrary tenant-authored fields (SSNs, bank info, health data). **Mitigation the ADR pins:** a per-field PII-sensitivity tag in `FieldOverlay`; PII-tagged fields persist tenant-key-encrypted via Foundation.Recovery (ADR 0046 / PR #223); audit emission on PII-field access. The keystone must NOT ship a form-storage path that bypasses PII tagging.
2. **Tenant isolation (catastrophic failure mode).** Every `FormDefinition` + entity instance is `IMustHaveTenant`; the entity-store + registry queries MUST be tenant-filtered at the form-engine boundary; repos MUST reject `TenantId.System`/default. Use the **Authorization-facade `ITenantContext`** (ADR 0091 R2 — NOT the MultiTenancy narrowed variant; the signal-bridge#34 CS0104 trap). Cross-tenant schema/instance bleed is the catastrophic failure.
3. **Schema migration / versioning.** The registry already ships lenses/upcasters/epochs (paper §7.2–7.4). The ADR DECIDES: v1 uses additive-only schema changes (Rev 1's v1 cut); breaking changes / epoch coordination are Phase 2. The risk is a published schema change breaking form rendering across synced devices — the registry's content-addressing (immutable published CIDs) + the form layer's `Published` gate mitigate, but the form layer MUST respect them (it cannot silently re-point a published form to a new CID without an explicit publish).
4. **EAV-vs-JSON storage / query + index implications (Rev 3 — IN-SCOPE v1 concern; CIC OQ-5).** Rev 1 chose JSONB-document storage (avoiding EAV); the entity store already does entity-as-JSON-body. The risk is query ergonomics + indexing at scale. **No longer deferred:** CIC ruling 2026-05-30T1322Z (OQ-5) moved the Postgres/JSONB adapter into the keystone definition-of-done, so the **JSONB index + JsonPath query strategy is a v1 keystone concern the dual-council MUST review** as part of the keystone — not punted to a later adapter phase. The dotnet-architect council reviews the index strategy, the entity-store-contract-over-JSONB query plan, and whether the v1 read path scales on real Postgres (target geometry: 6 LLCs × ~500 entities). There is no in-memory-only escape hatch for v1: the production target is Postgres from the keystone DoD.
5. **Status-contradiction / acceptance-provenance risk.** Rev 1's `Accepted` frontmatter was untrustworthy (no council verdict ever ran). Rev 2 resets the disposition to **Proposed** and requires re-attestation, or the platform carries a phantom-Accepted substrate ADR. **Resolved in Rev 2** by this reconciliation; the dual-council attestation closes it.
6. **Scope-creep into a no-code studio.** The single biggest delivery risk is conflating "the keystone that unblocks bundle authoring surfaces" with "ship ERPNext's Form Builder." The minimal-v1 cut (admin-defined types via code/JSON; render/validate/save; section-permissions) is the unblock. The authoring UX is the differentiator and belongs in Phase 2+. The crisp v1/Phase-2 boundary in this ADR is the mitigation.

---

## Council posture

**Dual-council MANDATORY — security-engineering + dotnet-architect — on the ADR 0055 Rev 2 text BEFORE Accept**, matching the substrate-tier Halt cadence established by ADR 0095 / 0096 / 0097 / 0098 / 0099 / 0100 / 0101. ADR 0101 (asset-management substrate) went through exactly this on 2026-05-29 (Rev 1 → dual-council fold → Rev 2 → re-attest). The dynamic-forms re-scope is at least as substrate-defining and more PII-sensitive (arbitrary tenant-authored field shapes + section permissions + per-tenant isolation).

- **security-engineering** — arbitrary-field PII (risk 1); section-based permission enforcement (macaroon scope intersection must be airtight — a section mis-scope leaks a vendor's data to a tenant or vice versa); per-tenant schema isolation (risk 2); confirm the eventual Web-Forms public surface is Phase-2 security-heavy and OUT of the keystone.
- **dotnet-architect** — the consume-vs-rebuild decision (composing `Sunfish.Kernel.SchemaRegistry` + `Sunfish.Foundation.Assets` vs. Rev 1's from-scratch sketch); the `FormDefinition`-references-`SchemaId` boundary (don't duplicate versioning/storage); the **form-layer lifecycle decision** (the registry `Schema` record has NO status field — `FormDefinitionStatus` lives on the form, not delegated to a non-existent registry status); the **Postgres/JSONB persistence adapter + JSONB index/JsonPath query strategy now IN the keystone DoD** (Rev 3 — CIC OQ-5; review the durable read/write path, index plan, and scale geometry — this is no longer a deferred phase boundary); the `DynamicTemplate → DocumentStatus` mapping preservation across the relocation.

> **Orchestration note.** This ADR's dual-council is dispatched by the main Admiral session on the reconciliation status beacon (`admiral-subagent-status-…-adr-0055-rev2-reconcile`). Several open questions below need a CIC/Admiral decision before (or as input to) the council run.

---

## Open questions (for CIC/Admiral to resolve before/at the dual-council)

Carried from ONR Finding "Open questions"; pruned to the ones that genuinely gate the council. The ones Rev 2 already decided are marked **[RESOLVED in Rev 2]**.

| ID | Question | Rev 2 disposition |
|---|---|---|
| OQ-1 | Acceptance provenance — confirm the Rev 1 `Accepted` frontmatter was a scaffolding artifact. | **[RESOLVED in Rev 2]** No `council-verdict-0055-*` beacon exists anywhere in `coordination/`. Status reset to Proposed. |
| OQ-2 | Re-scope vs. fresh ADR. | **[RESOLVED in Rev 2]** Rev 2 re-scope (ADR-0101 precedent) — preserves the GRAPH/INDEX edges (0055 composes 0001/0005/0007/0028/0032/0046/0049/0056; 0007/0028/0049/0001/0005/0046 consume-edges intact). |
| OQ-3 | `FormDefinition` vs. registry `Schema` boundary — reference a `SchemaId` or embed a JSON-Schema blob? | **Rev 2 recommends REFERENCE** (don't duplicate the registry). **Needs dotnet-architect ratification** — this is the central architecture decision. |
| OQ-4 | Lifecycle-enum alignment — `FormDefinitionStatus{Draft,Published,Archived}` on the form, or mirror a registry status? | **[RESOLVED in Rev 2]** The shipped registry `Schema` record has NO status field (it is content-addressed/immutable). `FormDefinitionStatus{Draft,Published,Archived}` lives on the form layer; preserve the `DynamicTemplate → DocumentStatus` mapping. (Corrects ONR's "align to registry SchemaStatus" — that enum does not exist on the record.) |
| OQ-5 | In-memory-v1 vs. Postgres-in-keystone. | **[RESOLVED by CIC ruling 2026-05-30T1322Z]** CIC **OVERRODE the Rev 2 in-memory recommendation**: the keystone ships a **Postgres-backed `IEntityStore` at its definition-of-done** — durable at the keystone, not a fast-follow. The Postgres/JSONB adapter + JSONB index strategy are now IN the keystone (see item 4, risk 4). Source: `admiral-ruling-2026-05-30T1322Z-adr-0055-oq5-postgres-in-keystone-oq6-code-json.md`. |
| OQ-6 | Does minimal-v1 need ANY authoring UX, or are code/JSON-defined schemas acceptable for the bundle authoring surfaces? | **[RESOLVED by CIC ruling 2026-05-30T1322Z]** CIC **AFFIRMED** the Rev 2 recommendation: **code/JSON-defined schemas for v1; no authoring UX in v1** (field-list editor / studio is Phase 2). Source: `admiral-ruling-2026-05-30T1322Z-adr-0055-oq5-postgres-in-keystone-oq6-code-json.md`. |
| OQ-7 | Typed-record migration coupling. | **[RESOLVED in Rev 2]** DECOUPLED. The keystone serves NEW admin-defined types; Property/Equipment/Inspection typed-record retirement is a separate follow-on ADR with its own migration risk. |
| OQ-8 | Compounds subset for v1 — which exact field types do the 5 bundles' authoring surfaces need? | **Open (low-gate).** Rev 2 names a working subset (string/number/date/select/bool/Money/Reference/Attachment/InternationalizedText/Coding); ONR can produce a precise field-type inventory from the bundles' featureDefaults if Admiral wants P3 compounds pinned before the council. Does NOT gate the dual-council on P1/P2. |

**Gating summary:** OQ-3 (reference-vs-embed) is now the one remaining open gate and belongs to the dotnet-architect council. OQ-5 (persistence-in-keystone) and OQ-6 (authoring-UX-in-v1) were the two scope gates; **both are RESOLVED by CIC ruling 2026-05-30T1322Z** (OQ-5 = Postgres-in-keystone, overriding the Rev 2 in-memory recommendation; OQ-6 = code/JSON v1, affirming Rev 2) — the council now reviews a settled scope. OQ-8 is a P3 detail that can resolve post-council.

---

## Compatibility plan

### Existing callers / consumers

The only consumer coded against the future substrate is `blocks-ships-office` (the `DynamicTemplate` document kind), via the `foundation-ships-office` stub. The relocation:

| Surface | Migration strategy |
|---|---|
| `foundation-ships-office/Services/{FormSchema,IFormSchemaStore}.cs` | **RELOCATE** to `Sunfish.Foundation.Forms` as `FormDefinition` / `IFormDefinitionStore` / `FormDefinitionId` / `NoopFormDefinitionStore`. Sweep the 5 `TODO-RELOCATE` markers. |
| `blocks-ships-office` (`ShipsOfficeServiceCollectionExtensions.cs`, `ShipsOfficeDataProvider.cs`, `ShipsOfficeDocumentKind.cs`) | Re-reference the canonical types; preserve the `FormSchemaStatus → DocumentStatus` mapping (now `FormDefinitionStatus → DocumentStatus`). **Guard for the sweep PR.** |
| `blocks-forms` (presentation) | KEEP as consumer; add a dynamic render component (or `FormBlock` mode) taking a `FormDefinition`. |
| Shipped typed-record clusters (Property / Equipment / Inspection) | **NOT migrated by the keystone.** Deferred to a separate follow-on ADR (OQ-7). |

### Affected packages (Rev 2)

| Package | Change |
|---|---|
| `Sunfish.Foundation.Forms` (**new — `packages/foundation-forms/`**) | Primary keystone deliverable: form engine + `FormDefinition` + `FormOverlay` + `IFormDefinitionStore`; relocates the stub |
| `foundation-ships-office` (existing) | Modified — stub relocated out; re-references canonical |
| `blocks-ships-office` (existing) | Modified — re-references canonical; preserve `DocumentStatus` mapping |
| `blocks-forms` (existing) | Modified — adds dynamic render component |
| `Sunfish.Kernel.SchemaRegistry` (shipped) | **Consumed** (validation core) — no change |
| `Sunfish.Foundation.Assets` (shipped) | **Consumed + extended in v1** (JSONB entity store contract) — the keystone adds a **Postgres/JSONB persistence adapter + index strategy** for `IEntityStore` (Rev 3 — CIC OQ-5; was a later phase in Rev 2) |
| `foundation-taxonomy` (ADR 0056, shipped) | **Referenced** (Coding/CodeableConcept field types) — no change |
| `foundation-rule-engine-event-bridge` (shipped) | **Consumed/extended** at Phase 2 (Tier-2 evaluator) — no change in keystone |
| Compounds (subset; partly new) | New subset package or additions for the bundle-needed field types (P3) |

### ADR relationships (unchanged edges)

- **Composes:** ADR 0001 (Schema Registry Governance), 0005 (Type Customization — Layer-3 overlay), 0007 (Bundle Manifest), 0028 (CRDT), 0032 (Macaroons), 0046 (Recovery — PII encryption), 0049 (Audit), **0056 (Taxonomy — now Accepted; Rev 1 referenced as "future")**.
- **Consumed by:** ADR 0057 (leasing pipeline) per the GRAPH.

---

## Revisit triggers

- Schema authoring UX (Phase 2) rejected by CIC on usability grounds — consolidate or simplify
- JSONB query performance regressions at typical scale (6 LLCs × ~500 entities) — pivot to typed projections + read-replica (now a v1-keystone concern per CIC OQ-5, not a later phase)
- Tier-2/3 rule demand exceeds tolerance before the Phase-2 timeline
- Schema epoch handling becomes urgent (breaking schema changes accumulate; lenses/upcasters need to ship from the registry into the form layer)
- Cross-tenant schema sharing crosses a customer-pull threshold — marketplace v2 work
- Form rendering p95 > 200ms — rule-evaluator caching; schema pre-compilation
- Compliance regime requires audit-trail not currently captured (e.g., FCRA full-record-of-edits)
- iOS Swift rule-interpreter parity gap

---

## Pre-acceptance audit (Rev 2 self-check)

- [x] **AHA pass.** Four options considered (Rev 1); Rev 2 adds the build-vs-consume axis and chooses consume — the AHA insight is that ~60% of the substrate already shipped.
- [x] **Status reconciled.** Frontmatter, body header, and sign-off all consistently **Proposed**; revision history explains the Rev 1 contradiction.
- [x] **Reuse inventory verified on disk** (origin/main 87b7266) — every "shipped" claim read; ONR's one over-claim (registry `SchemaStatus` field) corrected.
- [x] **Scope cut explicit.** Minimal-v1 keystone vs. Phase-2+ DocType parity boundary is crisp and enumerated.
- [x] **Council posture set.** Dual-council MANDATORY (security-engineering + dotnet-architect); rationale tied to the ADR 0095–0101 cadence + PII sensitivity.
- [x] **Risks folded.** ONR's six risks are the risk section; mitigations pinned.
- [x] **Open questions pruned to gates.** OQ-3 is the one remaining council gate; **OQ-5/6 RESOLVED by CIC ruling 2026-05-30T1322Z (Rev 3)** — OQ-5 Postgres-in-keystone (overriding Rev 2 in-memory), OQ-6 code/JSON v1 (affirming Rev 2); OQ-1/2/4/7 resolved in Rev 2.
- [x] **Sources cited.** ONR scoping study (shipyard#208) + the on-disk packages.

---

## Sign-off

CTO (research session) — 2026-04-29 (Rev 1)
Admiral (reconciliation subagent) — 2026-05-30 (Rev 2)
Admiral (Rev 3 fold subagent) — 2026-05-30 (Rev 3 — CIC ruling 2026-05-30T1322Z folded: OQ-5 Postgres-in-keystone, OQ-6 code/JSON v1)

**Status: Proposed (Rev 3).** Re-scoped onto shipped substrate (Rev 2) with scope now settled by CIC ruling 2026-05-30T1322Z (Rev 3 — Postgres-backed `IEntityStore` at the keystone DoD; code/JSON-defined schemas for v1). Awaiting **dual-council attestation** (security-engineering + dotnet-architect) on the settled scope. The main Admiral session orchestrates the dual-council on the reconciliation status beacon. Do NOT treat this ADR as Accepted until the dual-council verdicts land and the disposition is explicitly flipped — the Rev 1 phantom-acceptance is the precise failure mode Rev 2 closes.
