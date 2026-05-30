# ONR research — Dynamic-forms keystone substrate scoping (ADR 0055 input) (2026-05-30)

**Requester:** CIC (via Admiral dispatch, 2026-05-29) — ADR-INPUT scoping for Admiral to author ADR 0055.
**Author:** ONR
**Scope:** Reconstruct the intent + current status of the dynamic-forms substrate (the shared
required-module spine of all reference bundles + the ERPNext DocType/Customize-Form/Web-Form parity
capability), inventory what is already BUILT vs. STUB vs. NET-NEW on disk, map ERPNext-parity scope,
and recommend the ADR 0055 (re-)scope + phasing with a minimal-v1-vs-full-parity cut. **In scope:**
status reconstruction, reuse inventory, ERPNext-capability mapping, scope/phase recommendation,
council-posture + risk flagging. **Out of scope:** writing production code, authoring or ratifying
ADR 0055 (Admiral owns the ADR), prioritization rulings (CIC/Admiral), validating effort sizings
against Engineer.
**Status:** final
**Confidence:** HIGH on what exists on disk (every cited package inspected — `kernel-schema-registry`,
`foundation/Assets`, `foundation-taxonomy`, `blocks-workflow`, `blocks-forms`, `foundation-ships-office`
stub, all 5 bundle manifests). HIGH on the ADR-0055 status contradiction (read the ADR file directly).
MEDIUM on phase sizings (carried from the existing ADR's own estimates + the C-bundle readiness audit;
not re-validated with Engineer). The single most consequential finding **materially corrects the
dispatch framing**: ADR 0055 is NOT missing — it exists as a comprehensive Accepted/Proposed-ambiguous
ADR from 2026-04-29, and its two largest substrate components are **already built in the kernel/foundation
tier under different namespaces** than the ADR's original sketch.

---

## TL;DR

1. **ADR 0055 already exists and is substantial — but its status is internally contradictory and its
   namespace plan is stale.** `docs/adrs/0055-dynamic-forms-substrate.md` (2026-04-29, CTO research
   session) is a full Option-A decision with an 8-phase / ~16-week implementation checklist. **But its
   frontmatter says `status: Accepted` while its body header + sign-off both say `Status: Proposed`,
   awaiting CEO sign-off after a council-review dispatch that the sign-off says had not yet happened.**
   Admiral's job is NOT to author 0055 fresh — it is to **reconcile + re-scope** a 2026-04-29 ADR
   against ~5 weeks of subsequent substrate that shipped underneath it.

2. **The two biggest components ADR 0055 calls "new" are already built — in the kernel + foundation
   tiers, under different namespaces.** (a) The **Schema Registry** (0055's `Sunfish.Foundation.Schema`)
   ships TODAY as `Sunfish.Kernel.SchemaRegistry` — content-addressed JSON-Schema-2020-12 registration +
   validation via `JsonSchema.Net 9.2.0`, plus **Epochs, Lenses, Upcasters, and a Migration path** (the
   exact paper §7.2/§7.3/§7.4 schema-evolution machinery 0055 deferred to v2/v3). (b) The **JSONB Entity
   Store** (0055's `IEntityInstanceStore`) ships TODAY as `Sunfish.Foundation.Assets` — `IEntityStore` +
   `IVersionStore` (SHA-256 hash chain) + `IAuditLog` + `IHierarchyService` (temporal split/merge/reparent
   tree). The `Taxonomy` substrate 0055 referenced as "future" is **Accepted** (ADR 0056) and shipped
   (`foundation-taxonomy`). A **rule-engine** substrate exists (`foundation-rule-engine-event-bridge`).
   **The substrate's foundations are ~60% present; the gap is the FORM ENGINE + AUTHORING UX + the
   canonical-type relocation, not the storage/schema/sync core.**

3. **What is genuinely a STUB is narrow and single-consumer.** `foundation-ships-office/Services/FormSchema.cs`
   + `IFormSchemaStore.cs` is a 6-field minimal descriptor with a `NoopFormSchemaStore` default, consumed
   by exactly ONE surface — `blocks-ships-office`'s `DynamicTemplate` document kind. The `xo-ruling-T02-43Z`
   that created it explicitly chose the local-stub pattern "pending canonical substrate when a forcing
   function surfaces." **CIC's keystone elevation IS that forcing function.** The relocation blast radius
   is tiny (5 files, all in `blocks-ships-office` + tests).

4. **"Forms" is the spine of ALL FIVE bundles, not four.** `sunfish.blocks.forms` is in `requiredModules`
   of property-management, asset-management, project-management, facility-operations, AND
   acquisition-underwriting. The dispatch said "four non-PM bundles"; on disk it is universal. But the
   WBS + readiness audit both mark `blocks.forms → COMPLETE` because the **presentation block** exists —
   masking that the engine behind it is the Noop stub. That mask is exactly the gap CIC is naming.

5. **Recommended cut: a "minimal-v1 keystone" that is far smaller than the 16-week ADR.** Because the
   schema registry + entity store + taxonomy + rule-bridge already exist, minimal-v1 is essentially
   (a) promote the `FormSchema`/`IFormSchemaStore` stub to a canonical `Sunfish.Foundation.Forms`
   package that **composes the existing kernel registry + entity store** (not a greenfield schema/storage
   build), (b) a thin **FormDefinition overlay** (sections + field-overlay + permissions-ref) on top of a
   registered JSON Schema, (c) a **read/render + validate/save** form-engine slice that `blocks-forms`'
   presentation layer consumes, and (d) the stub-relocation sweep. **Full DocType parity** (admin authoring
   UX, Web Forms / public surfaces, naming-series, child-tables, the three-tier rules authoring) is a
   **Phase 2+ roadmap**, NOT the keystone. The keystone unblocks bundle *authoring surfaces*; it does not
   require shipping a no-code studio.

6. **Council posture: dual-council MANDATORY (security-engineering + .NET-architect), matching the
   ADR-0095..0101 substrate-tier cadence.** User-defined forms storing arbitrary fields is the
   highest-PII-risk substrate in the platform (arbitrary tenant-authored field shapes + section-based
   permissions + per-tenant schema isolation). Re-scoping 0055 is itself substrate-defining and gets the
   same Halt cadence ADR 0101 just went through (2026-05-29).

---

## Method

Read-only, on-disk + on-doc. I (1) grepped the fleet for every `0055` / `Sunfish.Foundation.Forms` /
`FormSchema` / `IFormSchemaStore` / `DynamicTemplate` / `dynamic form` reference; (2) read ADR 0055 in
full + its frontmatter + the ADR INDEX/STATUS/GRAPH rows; (3) inspected the actual on-disk surface of
every package ADR 0055 names as "new" (`kernel-schema-registry`, `foundation/Assets`, `foundation-taxonomy`,
`foundation-rule-engine-event-bridge`, `blocks-workflow`, `blocks-forms`); (4) read the
`foundation-ships-office` stub + `xo-ruling-T02-43Z` that authored it + its consumers; (5) read all 5
bundle manifests' `requiredModules`; (6) cross-referenced the two freshest ONR surveys
(`c-bundle-substrate-readiness-audit-2026-05-29.md`, `erpnext-conversion-and-backlog-register-2026-05-29.md`)
and the `post-mvp-wbs-2026-05-29.md`. Every "exists on disk" claim was verified against
`origin/main @ 87b7266` (this worktree's base).

---

## Finding 1 — ADR 0055 status reconstruction: it exists, but is contradictory + stale

**ADR 0055 is NOT missing.** `shipyard/docs/adrs/0055-dynamic-forms-substrate.md` is a 597-line
substrate ADR authored 2026-04-29 in a "CTO research session," synthesizing four prior research/UPF
artifacts (PR #230/#231/#234/#235). It adopts **Option A** (schema-registry-driven dynamic substrate),
sketches the full contract surface (`SchemaDefinition`, `ISchemaRegistry`, `SunfishOverlay`, `FormSection`,
`SectionAccess`, `RuleDefinition`, `IFormEngine`, `EntityInstance`, `IEntityInstanceStore`), and lays out
an **8-phase / ~16-week** (12-13-week-with-scope-cut) implementation checklist.

### The status contradiction (load-bearing — Admiral must resolve this first)

| Location in the ADR | Status claim |
|---|---|
| Frontmatter `status:` (line 6) | **`Accepted`** |
| Body header (line 26) | **`**Status:** Proposed`** |
| Sign-off (line 596) | **"Status: Proposed. Awaiting CEO sign-off after council-review subagent dispatch (CTO will dispatch as next step before flipping Status: Accepted…)"** |

The `STATUS.md` and `INDEX.md` ADR-portfolio rows list 0055 without a resolved disposition. There is
**no `council-verdict-*` beacon for ADR 0055** anywhere in `coordination/_archive/` or `coordination/inbox/`
— the council-review dispatch the sign-off promised **appears never to have run**. So the true state is:
**Proposed, never council-attested, never CEO-signed; the frontmatter `Accepted` is almost certainly a
template/scaffolding artifact, not a real acceptance.** This matters because every downstream artifact
that says "per ADR 0055 acceptance" (e.g., `xo-ruling-T02-43Z`, the `cob-status` "W#55 Phase 5 (ADR 0055
Accepted)" line) was relying on an acceptance that did not actually happen with council attestation.

**Recommendation:** treat ADR 0055 as **Proposed, requiring a re-scope + dual-council attestation before
Accept.** Do NOT trust the frontmatter `Accepted`. The cleanest path is a **Rev 2 re-scope** of the
existing 0055 (the ADR-0101 precedent: same-day Rev 1 → council fold → Rev 2 → re-attest), NOT a fresh ADR.

### Why the ADR is stale (the substrate moved underneath it)

ADR 0055 was written 2026-04-29. Between then and now (2026-05-29), the platform shipped the kernel
schema registry, the foundation entity store, the taxonomy substrate (ADR 0056 Accepted), a rule-engine
bridge, and the `blocks-workflow` state-machine primitive. **The ADR's "Affected packages → new" table
is now wrong for at least 3 of its 5 named-new packages** (see Finding 2). Admiral's re-scope should
**rebase the ADR onto the shipped substrate** rather than re-deciding storage/schema/sync.

### Consumers already coded against the future substrate (the relocation TODOs)

Exactly one stub surface carries `TODO-RELOCATE-WHEN-CANONICAL … ADR 0055` markers:

- `foundation-ships-office/Services/FormSchema.cs` — `FormSchema(Id, TenantId, Name, FormSchemaStatus{Draft,
  Published,Archived}, UpdatedAt, LastModifiedBy)` + `FormSchemaId`. Three relocation TODOs.
- `foundation-ships-office/Services/IFormSchemaStore.cs` — 2-method interface (`GetByIdAsync`,
  `ListByTenantAsync`) + `NoopFormSchemaStore` no-op default. Two relocation TODOs.

**Consumers of the stub (grep, verified):** `blocks-ships-office/ShipsOfficeServiceCollectionExtensions.cs`,
`ShipsOfficeDataProvider.cs`, `ShipsOfficeDocumentKind.cs` (the `DynamicTemplate` kind), + tests. **One
consuming surface, all inside `blocks-ships-office`.** The `xo-ruling-T02-43Z` explicitly kept the stub
minimal — "consumers should not reach for additional fields (revision history, JSON-schema blob,
field-level metadata) until the canonical type ships" — and named the forcing functions that would trigger
canonicalization (a second consumer, the W#55 ledger-flip, or "CO explicitly directs"). **CIC's keystone
elevation is "CO explicitly directs."**

---

## Finding 2 — Reuse inventory: ~60% of ADR 0055's substrate is already built (under other namespaces)

This is the most decision-relevant finding. ADR 0055's "Affected packages" table lists 5 NEW foundation
packages. On disk, **3 of the 5 already exist** (under kernel/foundation namespaces the 2026-04-29 ADR
didn't anticipate), the 4th (taxonomy) is a separate Accepted ADR + shipped package, and only the form
engine + compounds are genuinely net-new.

| ADR 0055 "new" package | On-disk reality (origin/main @ 87b7266) | Verdict |
|---|---|---|
| `Sunfish.Foundation.Schema` (schema registry) | **EXISTS as `Sunfish.Kernel.SchemaRegistry`** (`packages/kernel-schema-registry/`). `ISchemaRegistry` = `RegisterAsync` (content-addressed, idempotent, `JsonSchema.Net 9.2.0`, draft 2020-12) + `GetAsync` + `ValidateAsync` (returns `SchemaValidationResult` with RFC-6901 JSON-pointer errors) + `ListAsync(tagFilter)` + **`Lenses` (paper §7.3) + `Upcasters` (§7.2) + `Epochs` (§7.4)** + a reserved `PlanMigrationAsync`/`MigrateAsync` migration path. `InMemorySchemaRegistry` reference impl. README explicitly maps to "ADR 0055 (dynamic-forms substrate; schema-registry consumer)." | **BUILT.** 0055 should *consume*, not re-author. Bonus: the schema-evolution machinery 0055 deferred to v2/v3 (lenses/upcasters/epochs) is **already present** at the kernel tier. |
| `Sunfish.Foundation.Forms` (form engine + JSONB persistence orchestration) | **DOES NOT EXIST** (grep: no `namespace Sunfish.Foundation.Forms` anywhere). This is the genuine net-new core. | **NET-NEW.** The keystone's real deliverable. |
| `Sunfish.Foundation.RuleEngine` (three-tier rules) | **PARTIAL — `foundation-rule-engine-event-bridge` exists** (`BusinessRuleEventSubscriber` + DI). This is a rule-event bridge, not the full three-tier evaluator 0055 specced (JSON-Schema if/then/else + JsonLogic + Power Fx). The evaluator tiers are net-new; the event-integration seam exists. | **PARTIAL.** Tier-1 (JSON-Schema if/then/else) is free via the kernel registry's draft-2020-12 validation; Tier-2 (JsonLogic) is net-new; Tier-3 (Power Fx) was already deferred to v2 by 0055 itself. |
| `Sunfish.Foundation.Compounds` (20 primitives) | **DOES NOT EXIST** as a named package (no `*compound*`). Some primitives exist piecemeal (`Money` per ADR 0051; `Address`/`Email`/`Phone` in `blocks-people-foundation`; `InternationalizedText` in foundation I18n). | **NET-NEW (but partially scavengeable).** Not all 20 needed for minimal-v1. |
| `Sunfish.Foundation.Taxonomy` (Coding/CodeableConcept refs) | **EXISTS as `foundation-taxonomy`; ADR 0056 = Accepted.** 0055 referenced it as "future ADR"; it shipped. | **BUILT.** 0055 should reference the Accepted ADR 0056. |
| JSONB Entity Store (`IEntityInstanceStore`, `foundation-assets-postgres` extension) | **EXISTS as `Sunfish.Foundation.Assets`** (`packages/foundation/Assets/`). `IEntityStore` (create/get/update JSON-document entities, each carrying a `SchemaId`), `IVersionStore` (SHA-256 hash chain, Automerge-style change model), `IAuditLog` (hash-chained per entity), `IHierarchyService` + `HierarchyOperations` (temporal split/merge/reparent tree with `asOf` queries + closure table). In-memory today; "Postgres backend deferred." Entity bodies store CID blob references. | **BUILT (in-memory tier).** This IS 0055's "JSONB Entity Store + tree-hierarchy" — entity-as-JSON-document + version log + audit chain + temporal tree. The Postgres/JSONB *persistence adapter* is the deferred half (consistent with 0055 Phase 3). |
| State machines (forms drive workflow transitions) | **EXISTS as `blocks-workflow`** (`WorkflowDefinition`/`WorkflowBuilder`/`StateMachine<TState,TEvent>`/`IWorkflowRuntime` + `InMemoryWorkflowRuntime`). | **BUILT.** The forms→workflow seam is a wiring concern, not new substrate. |

### What this means for the ADR re-scope

The 2026-04-29 ADR framed the substrate as **"three substrates in flight simultaneously — schema registry
+ form engine + rules engine, each with its own complexity"** and a 12-16-week MVP. **On disk, the schema
registry + the entity store + taxonomy + workflow + a rule-event bridge are already shipped.** The
remaining net-new is: **the FORM ENGINE (render/validate/save over a registered schema + overlay), the
overlay/section/permissions model, the Tier-2 JsonLogic evaluator, the compounds the bundles actually need,
the Postgres persistence adapter, and the authoring UX.** That is a materially smaller keystone than the
ADR's own 16 weeks — and most of it is *wiring existing substrate together behind a form-engine facade*,
not building storage/schema/sync from scratch.

---

## Finding 3 — "Forms" is the universal bundle spine (all five, not four)

`sunfish.blocks.forms` appears in the `requiredModules` array of **every** bundle manifest:
property-management, asset-management, project-management, facility-operations, acquisition-underwriting
(verified in all 5 `packages/foundation-catalog/Manifests/Bundles/*.bundle.json`). The dispatch framing
("shared required-module spine of all four non-PM reference bundles") undercounts — **property-management
needs it too.**

**The masking problem.** Both the `post-mvp-wbs-2026-05-29.md` (line 402: `blocks.forms → blocks-forms YES`)
and the `c-bundle-substrate-readiness-audit-2026-05-29.md` (Finding 1: `blocks.forms → COMPLETE, thin but
sufficient`) classify forms as DONE — because the **presentation block** (`blocks-forms`: `FormBlock.razor`,
opinionated Razor wiring over `SunfishForm` + `SunfishValidation`) exists. But that block renders
**statically-typed** model forms; it has **no dynamic-schema path**. The dynamic engine behind it is the
`NoopFormSchemaStore` returning empty. So every bundle "requires forms," every bundle audit says "forms
COMPLETE," and yet **no bundle can author a user-defined type today.** CIC's keystone elevation names
exactly this gap: forms-the-presentation-block is done; forms-the-dynamic-substrate is the Noop stub.

---

## Finding 4 — ERPNext DocType parity: which capabilities the Sunfish substrate should OWN

ERPNext's "DocType / Customize Form / Web Form" family is the parity target. Mapping its capabilities to
the Sunfish substrate (and to what already exists):

| ERPNext capability | What it is | Sunfish substrate owner | Status / where it lands |
|---|---|---|---|
| **DocType** (user-defined record type) | Define a new record type as metadata: fieldname, fieldtype, label, options, reqd, etc. | `Sunfish.Foundation.Forms` schema-def (overlay) **over** `kernel-schema-registry` JSON Schema | **Minimal-v1 core.** Registry exists; overlay + def is net-new. |
| **Field types** (Data, Int, Float, Currency, Date, Select, Link, Table, Check, Text Editor, Attach, …) | The primitive palette | `Sunfish.Foundation.Compounds` (the 20-primitive catalog) + JSON-Schema base types | **Phased.** Minimal-v1 ships the subset the bundles need (string/number/date/select/bool/money/reference/attachment); full 20-primitive palette is Phase 2. |
| **Customize Form** (add/modify fields on an EXISTING DocType) | Tenant overlays on a base type | ADR 0005 Layer 3 `TenantTemplateOverlay` (RFC-7396 merge-patch) + the registry's `parents`/lineage | **Phase 2** (overlay + 3-way-merge UX is its own scope; ADR 0005 already specced the model). Minimal-v1 = admin-defined NEW types only (matches 0055's "admin-defined types in v1"). |
| **Web Form** (public-facing form → DocType) | Anonymous/external submission surface | Form engine render + a public Bridge endpoint + section-permission scoping | **Phase 2+.** Public/external surface is security-heavy (anti-CSRF, rate-limit, captcha per ADR 0096 Turnstile) — explicitly NOT minimal-v1. |
| **Validation rules** (reqd, depends_on, mandatory_depends_on, read_only_depends_on) | Cross-field conditional logic | Three-tier rules engine (Tier-1 JSON-Schema if/then/else free via registry; Tier-2 JsonLogic) | **Tier-1 in minimal-v1** (visibility/required/readonly via JSON-Schema — registry already validates 2020-12). Tier-2 cross-field validation Phase 2. Tier-3 (Power Fx computed) was already v2 in 0055. |
| **Layout** (sections, column breaks, tabs) | Form visual grouping | `FormSection` in the overlay (`SunfishOverlay.Sections`) | **Minimal-v1** (sections are also the permission unit — see permissions). Tabs/column-breaks are presentation polish, Phase 2. |
| **Naming series** (auto-ID like `INV-.YYYY.-.####`) | Auto-numbered record identity | `AutoNumber` compound primitive (in 0055's 20-primitive list) | **Phase 2.** Bundle authoring surfaces don't block on it; invoice numbering already has a fleet convention (`INV-YYYY-MM-DD-{REPLICA}-{NNNN}` per the fixture memory). |
| **Permissions** (role-based, field-level, "if owner") | Who sees/edits what | Section-based permissions (`SectionAccess` ReadRoles/WriteRoles) enforced via ADR 0032 macaroons | **Minimal-v1** (section-based is 0055's Approach F; field-level is the escape hatch, Phase 2). This is the load-bearing security half — vendor/tenant magic-link section scoping. |
| **Child tables** (grid-of-rows sub-DocType) | One-to-many embedded collections | `Reference` meta-concept (cardinality + parent-child) + the entity store's `IHierarchyService` tree | **Phase 2** for full grid-authoring; minimal-v1 can represent child collections as JSON arrays in the entity body (no dedicated grid editor). |

**Parity scope recommendation:** the Sunfish substrate should OWN **DocType (admin-defined types),
field-type palette, layout-sections, section-permissions, and Tier-1 validation** as the minimal-v1
keystone. **Customize-Form overlays, Web-Forms, naming-series, child-table grid authoring, and Tier-2/3
rules** are explicitly the Phase-2+ DocType-parity roadmap. Minimal-v1 is "an admin can define a new type
and the bundles can render/validate/save instances of it"; full parity is "ERPNext Form Builder."

---

## Finding 5 — The canonical type: what `Sunfish.Foundation.Forms.FormSchema` should become

The stub today is 6 fields. The `xo-ruling-T02-43Z` deliberately withheld the deferred fields. Here is the
recommended canonical shape, **rebased onto the shipped registry + entity store** (so it composes rather
than re-invents). This is a *recommendation for the ADR to ratify*, not a ratified contract.

### Recommended canonical `FormSchema` / `FormDefinition`

The cleanest framing (consistent with the shipped `kernel-schema-registry`): **separate the validation
core from the form overlay.** The validation core is a registered JSON Schema (CID-addressed, versioned,
lens/upcaster/epoch-aware) — it already exists in the registry. The *form* layer is a thin overlay keyed
to a registered schema id:

```text
FormDefinition (the canonical type — relocates the stub's FormSchema)
  Id            : FormDefinitionId          (UUIDv7, relocate FormSchemaId)
  TenantId      : TenantId                  (multi-tenant isolation — IMustHaveTenant)
  Name          : string                    (kept from stub)
  SchemaId      : SchemaId                  (→ kernel-schema-registry; the JSON-Schema blob lives THERE,
                                             not inlined — resolves the deferred "JSON-schema blob" field)
  SchemaVersion : SemanticVersion           (version-pinned; epoch-aware via the registry)
  Status        : FormDefinitionStatus      (keep Draft/Published/Archived; ALIGN with the registry's
                                             SchemaStatus{Draft,Published,Deprecated,Withdrawn} — see note)
  Overlay       : FormOverlay               (sections + field overlays + rules + i18n title/desc)
  UpdatedAt     : DateTimeOffset            (kept from stub)
  LastModifiedBy: ActorId                   (kept from stub)
```

**On the deferred fields the `xo-ruling` withheld:**
- **JSON-schema blob** → do NOT inline it on `FormSchema`. Store the schema in the **registry** (CID-addressed)
  and reference it by `SchemaId`. This is cleaner than 0055's original `JsonSchema { get; init; }` inline
  field and avoids duplicating the registry's job.
- **Field-level metadata** → lives in `FormOverlay.Fields` (per-field UI hints, i18n, PII-sensitivity tag,
  control-type override). The validation/type metadata lives in the JSON Schema in the registry.
- **Revision history / versioning** → the registry already provides this (`SchemaVersion`, lenses, upcasters,
  epochs, content-addressing). `FormDefinition` versioning rides on `SchemaId`+`SchemaVersion`; the entity
  *instances* version independently via `Sunfish.Foundation.Assets.IVersionStore` (SHA-256 chain). **Do NOT
  build a third versioning mechanism** on `FormSchema`.

### On the `FormSchemaStatus{Draft,Published,Archived}` lifecycle

The stub's 3-state lifecycle is *narrower* than the registry's 4-state `SchemaStatus{Draft, Published,
Deprecated, Withdrawn}`. **Recommendation:** align the form-definition lifecycle to the registry's, OR map
explicitly (`Archived` ≈ `Withdrawn`; add `Deprecated` for "superseded but still readable"). The
`blocks-ships-office` `DynamicTemplate` kind maps `FormSchemaStatus` → its `DocumentStatus`; that mapping
must be preserved across the relocation (a guard for the sweep PR). The ADR should pin whether the form
lifecycle is the *same* enum as schema status or a *projection* of it.

---

## Finding 6 — Reuse vs. net-new, by package

| Surface | Relationship to the canonical substrate | Net-new vs. wiring |
|---|---|---|
| `blocks-forms` (presentation) | **KEEP as consumer.** Its `FormBlock.razor` is the static-typed render path; the dynamic path is a NEW component (`DynamicFormBlock` or a `FormBlock` mode) that takes a `FormDefinition` + `EntityInstance` and renders via the form engine. The block's "Future scope: Dynamic-forms substrate consumer (ADR 0055 Phase 1)" note is the seam. | **Wiring + 1 new component.** |
| `blocks-workflow` (state machines) | **COMPOSE.** Forms drive workflow transitions: a form `SaveAsync` can raise an event the `IWorkflowRuntime` consumes (e.g., a published inspection form advances a `WorkOrderStatus`). This is the `foundation-rule-engine-event-bridge` seam. | **Wiring.** No new substrate. |
| `foundation-ships-office` (current stub home) | **RELOCATE.** Move `FormSchema`/`FormSchemaId`/`IFormSchemaStore`/`NoopFormSchemaStore` → a new `Sunfish.Foundation.Forms` package; `blocks-ships-office` re-references the canonical. Sweep the `TODO-RELOCATE-WHEN-CANONICAL` markers. | **Mechanical relocation** (5-file blast radius). |
| `kernel-schema-registry` | **COMPOSE (primary dependency).** The form engine registers/looks-up/validates against it. Already ships everything the schema half needs. | **Wiring (the core reuse win).** |
| `foundation/Assets` (entity store) | **COMPOSE.** `EntityInstance` ≈ `IEntityStore`'s entity (JSON body + `SchemaId` + version log + audit + hierarchy). The form engine's `SaveAsync` writes through `IEntityStore`; `RenderAsync` reads from it. The Postgres/JSONB persistence adapter is the deferred half (matches 0055 Phase 3). | **Wiring + a Postgres adapter (deferred).** |
| `foundation-taxonomy` (ADR 0056 Accepted) | **REFERENCE.** `Coding`/`CodeableConcept` field types resolve against it. 0055 referenced it as "future"; it shipped. | **Wiring.** |
| `foundation-rule-engine-event-bridge` | **COMPOSE / EXTEND.** The event-bridge seam exists; the Tier-2 JsonLogic evaluator is net-new code that plugs into it. Tier-1 (JSON-Schema if/then/else) is free via the registry's validation. | **Partial reuse + Tier-2 net-new.** |
| `foundation-multitenancy` / `foundation-authorization` (`IMustHaveTenant`, `ITenantContext`) | **CONSUME.** Every `FormDefinition` + `EntityInstance` is `IMustHaveTenant`; section-permissions resolve via the Authorization-facade `ITenantContext` (ADR 0091 R2 — the `MultiTenancy` narrowed-variant CS0104 trap is the specific miss to avoid). | **Consume (per fleet convention).** |
| `Sunfish.Foundation.Forms` (form engine) | **NET-NEW.** `IFormEngine` (`RenderAsync`/`ValidateAsync`/`SaveAsync`), `FormDefinition`, `FormOverlay`, `FormSection`, `SectionAccess`, `IFormDefinitionStore` (the canonical `IFormSchemaStore`). | **The genuine net-new core.** |
| `Sunfish.Foundation.Compounds` (primitives) | **NET-NEW (subset for v1).** Only the field types the bundles need for minimal-v1. | **Net-new, scoped.** |

**Net-new code (minimal-v1):** `Sunfish.Foundation.Forms` (engine + def + overlay + store), a minimal
compounds subset, a Tier-2 JsonLogic evaluator, a Postgres entity-store adapter (or defer to in-memory for
v1), and `blocks-forms`' dynamic render component. **Everything else is wiring existing substrate.**

---

## Finding 7 — Recommended ADR 0055 scope + phasing (minimal-v1 vs. full parity)

### The cut

**MINIMAL-V1 KEYSTONE (unblocks bundle authoring surfaces with least surface area):**

1. **`Sunfish.Foundation.Forms` package** — relocate the `foundation-ships-office` stub here as the
   canonical `FormDefinition` + `IFormDefinitionStore`; sweep the `TODO-RELOCATE` markers; preserve the
   `blocks-ships-office` `DynamicTemplate` mapping.
2. **`FormOverlay` model** — `Sections` (with `SectionAccess` ReadRoles/WriteRoles) + per-field `FieldOverlay`
   (control hint, i18n label, PII tag) + `RuleDefinition[]` (Tier-1 only in v1). Keyed to a registered
   `SchemaId` (validation core lives in `kernel-schema-registry`).
3. **`IFormEngine` slice** — `RenderAsync` (schema + overlay + instance → form view, section-filtered by
   macaroon scope) + `ValidateAsync` (registry validation + Tier-1 rules) + `SaveAsync` (validate → write
   through `IEntityStore`). In-memory entity store for v1 (Postgres adapter deferred).
4. **Minimal compounds subset** — only the field types the bundles need (string/number/date/select/bool/
   `Money`/`Reference`/`Attachment`/`InternationalizedText`); reuse `Money` (ADR 0051), people-foundation
   primitives, taxonomy refs (ADR 0056).
5. **`blocks-forms` dynamic render component** — a new component (or `FormBlock` mode) consuming a
   `FormDefinition`; the presentation block stays the consumer it already is.
6. **Section-based permissions** (ADR 0032 macaroons, Approach F) — the security-load-bearing half; enables
   vendor/tenant section-scoped magic-link forms.

**Explicitly DEFERRED to PHASE 2+ (full DocType parity — NOT the keystone):**
- **Admin authoring UX** (schema editor / field-list builder / section-role editor / visual rule builder)
  — 0055's Phase 6, ~3 weeks. The bundles can be authored with **code/JSON-defined schemas** in v1; the
  no-code studio is the differentiator, not the unblock.
- **Customize-Form overlays** (tenant field-add on existing types; RFC-7396 merge-patch per ADR 0005 Layer 3)
  + 3-way-merge upgrade UX.
- **Web Forms / public-facing surfaces** (anonymous submission; security-heavy — anti-CSRF + rate-limit +
  Turnstile per ADR 0096).
- **Naming-series** (`AutoNumber` compound).
- **Child-table grid authoring** (full grid editor; v1 represents collections as JSON arrays).
- **Tier-2 JsonLogic cross-field validation** + **Tier-3 Power Fx computed fields** (Tier-3 already v2 in 0055).
- **Postgres/JSONB persistence adapter + index strategy** (v1 = in-memory; matches 0055 Phase 3 deferral).
- **Full 20-primitive compound catalog** (v1 ships the bundle-needed subset).
- **Migration of shipped cluster typed-records to JSONB** (0055 Phase 7 — this is a SEPARATE, large,
  risky migration; do NOT couple it to the keystone unblock. The keystone serves NEW admin-defined types;
  retiring `Property`/`Equipment`/`Inspection` typed-records is a follow-on ADR).

### Recommended phase cut (rebased onto shipped substrate)

| Phase | Scope | Reuses (already built) | Net-new | Council |
|---|---|---|---|---|
| **P1 — Canonical relocation + def/overlay** | `Sunfish.Foundation.Forms` package; `FormDefinition` + `FormOverlay` + `FormSection` + `SectionAccess`; relocate stub; sweep TODOs | kernel-schema-registry (validation core), foundation-multitenancy | the package + def + overlay types | **dual** |
| **P2 — Form engine slice** | `IFormEngine` render/validate/save over registry + in-memory entity store; Tier-1 rules; section-permission filtering via macaroons | kernel-schema-registry, foundation/Assets `IEntityStore`, ADR 0032 macaroons, foundation-taxonomy | engine impl + Tier-1 eval wiring | **dual** |
| **P3 — Compounds subset + blocks-forms dynamic render** | bundle-needed field types; `blocks-forms` dynamic component | Money (0051), people-foundation, taxonomy, blocks-forms presentation | compounds subset + 1 Razor component | sec-eng (PII tag handling) |
| **P4+ — full DocType parity (roadmap)** | authoring UX, Web Forms, Customize-Form overlays, Tier-2/3 rules, Postgres adapter, naming-series, child-table grids, typed-record migration | (per-item) | (per-item; each its own unit) | per-item; Web-Forms + migration = **dual** |

**Rough sizing:** P1–P3 (the keystone) is materially smaller than the existing ADR's 16-week estimate —
plausibly a handful of substantive PRs rather than a multi-month build — **because the schema registry +
entity store + taxonomy + workflow + macaroons are all already shipped.** I flag this as MEDIUM confidence
(not Engineer-validated); the point is directional: **the keystone is a facade + overlay + engine-slice
over existing substrate, not a from-scratch storage/schema/sync build.**

---

## Finding 8 — Council posture + risks

### Council posture (recommended)

**Dual-council MANDATORY (security-engineering + .NET-architect)** on the ADR 0055 re-scope text BEFORE
Accept, matching the substrate-tier Halt cadence established by ADR 0095/0096/0097/0098/0099/0100/0101.
ADR 0101 (asset-management substrate) went through exactly this on 2026-05-29 (Rev 1 → dual-council fold
→ Rev 2 → re-attest). ADR 0055's re-scope is **at least** as substrate-defining and **more** PII-sensitive.

- **security-engineering** — arbitrary-field PII (user-defined forms store tenant-authored arbitrary field
  shapes); section-based permission enforcement (macaroon scope intersection must be airtight — a section
  mis-scope leaks a vendor's data to a tenant or vice versa); per-tenant schema isolation (one tenant's
  schema must never validate/render against another's data); the eventual Web-Forms public surface
  (anti-CSRF + rate-limit + captcha) — flag as Phase-2 security-heavy and OUT of the keystone.
- **.NET-architect** — the consume-vs-rebuild decision (composing `kernel-schema-registry` +
  `foundation/Assets` vs. 0055's original from-scratch sketch); the `FormDefinition`-vs-registry-`Schema`
  boundary (don't duplicate versioning/storage); the lifecycle-enum alignment (`FormSchemaStatus` vs.
  registry `SchemaStatus`); the in-memory-first / Postgres-deferred phase boundary.

### Key risks (for the ADR to address)

1. **Arbitrary-field PII (highest).** User-defined forms can store arbitrary tenant-authored fields,
   including SSNs, bank info, health data. Mitigation the ADR should pin: a **per-field PII-sensitivity tag**
   in `FieldOverlay`; PII-tagged fields persist tenant-key-encrypted via Foundation.Recovery (0055's commitment
   #10 / Recovery PR #223); audit emission on PII-field access. The keystone must NOT ship a form-storage path
   that bypasses PII tagging.
2. **Tenant isolation.** Every `FormDefinition` + `EntityInstance` is `IMustHaveTenant`; the entity store +
   registry queries MUST be tenant-filtered; the repos MUST reject `TenantId.System`/default. Use the
   **Authorization-facade `ITenantContext`** (ADR 0091 R2 — NOT the MultiTenancy narrowed variant; the
   signal-bridge#34 CS0104 trap). Cross-tenant schema/instance bleed is the catastrophic failure mode.
3. **Schema migration / versioning.** The registry already has lenses/upcasters/epochs (paper §7.2-7.4) —
   the ADR should DECIDE whether v1 uses them (additive-only changes, per 0055's original v1 cut) or defers.
   The risk is a published schema change breaking form rendering across synced devices; the registry's
   `Draft/Published/Deprecated/Withdrawn` status gate + epoch coordinator mitigate, but the form layer must
   respect them.
4. **EAV-vs-JSON storage / query + index implications.** 0055 chose JSONB-document storage (avoiding EAV).
   The entity store already does entity-as-JSON-body. The risk is query ergonomics + indexing at scale
   (0055's own negative consequence). For minimal-v1 (in-memory) this is deferred; the Postgres-adapter phase
   must define the JSONB index + JsonPath query strategy. **Do not let the keystone ship a query path that
   only works in-memory and then falls over on Postgres** — the ADR should at least sketch the index strategy.
5. **Status-contradiction / acceptance-provenance risk.** The existing ADR's `Accepted` frontmatter is
   untrustworthy (Finding 1). Downstream docs cited a non-existent acceptance. The re-scope must explicitly
   reset the disposition to Proposed and re-attest, or the platform carries a phantom-Accepted substrate ADR.
6. **Scope-creep into a no-code studio.** The single biggest delivery risk is conflating "the keystone that
   unblocks bundle authoring surfaces" with "ship ERPNext's Form Builder." The minimal-v1 cut (admin-defined
   types via code/JSON; render/validate/save; section-permissions) is the unblock. The authoring UX is the
   differentiator and belongs in Phase 2+.

---

## Open questions (for Admiral, who authors ADR 0055 Rev 2)

1. **Acceptance provenance.** Confirm the ADR 0055 frontmatter `Accepted` is a scaffolding artifact and the
   true state is Proposed-never-attested. If any downstream work assumed real acceptance, flag it. (ONR could
   not find a `council-verdict-0055-*` beacon; recommend Admiral confirm none exists.)
2. **Re-scope vs. fresh ADR.** ONR recommends a **Rev 2 re-scope** of the existing 0055 (ADR-0101 precedent),
   not a fresh ADR — preserves the GRAPH/INDEX edges (0057 consumes 0055; 0055 composes 0001/0005/0007/0028/
   0032/0046/0049). Admiral's call.
3. **`FormDefinition` vs. registry `Schema` boundary.** Does the canonical form-def *reference* a registered
   `SchemaId` (ONR's recommendation — don't duplicate the registry) or *embed* a JSON-Schema blob (0055's
   original sketch)? This is the central architecture decision the .NET-architect council should rule on.
4. **Lifecycle-enum alignment.** Same enum as registry `SchemaStatus{Draft,Published,Deprecated,Withdrawn}`,
   or a 3-state projection (`Draft/Published/Archived`) mapped to it? Affects the `blocks-ships-office`
   `DynamicTemplate`→`DocumentStatus` mapping (sweep-PR guard).
5. **In-memory-v1 vs. Postgres-in-keystone.** Is the minimal-v1 keystone allowed to ship on the in-memory
   `IEntityStore` (fastest unblock), with the Postgres/JSONB adapter as a fast-follow, or must the keystone
   include persistence? (0055 Phase 3 implies persistence; the entity store's README says Postgres is deferred.)
6. **Does minimal-v1 need ANY authoring UX, or are code/JSON-defined schemas acceptable for the bundle
   authoring surfaces?** ONR's recommendation: code/JSON schemas for v1 (the unblock), authoring studio Phase 2.
   CIC may want a minimal field-list editor in v1 if non-developers must author the first bundle types.
7. **Typed-record migration coupling.** Confirm the shipped-cluster typed-record→JSONB migration (0055 Phase 7
   — Property/Equipment/Inspection) is DECOUPLED from the keystone (ONR strongly recommends: keystone serves
   NEW admin-defined types; typed-record retirement is a separate follow-on ADR with its own migration risk).
8. **Compounds subset for v1.** Which exact field types do the 5 bundles' authoring surfaces need? (ONR can
   produce a precise field-type inventory from the bundles' featureDefaults + the readiness audit if Admiral
   wants the P3 compounds scope pinned before the ADR.)

---

## Sources cited

1. `shipyard/docs/adrs/0055-dynamic-forms-substrate.md` — the existing ADR; frontmatter `status: Accepted` /
   body+sign-off `Status: Proposed` contradiction; Option-A decision + 8-phase / 16-week checklist + contract
   sketch [PRIMARY / on-disk] (retrieved 2026-05-30)
2. `shipyard/docs/adrs/{STATUS,INDEX,GRAPH}.md` — 0055 portfolio rows; GRAPH edges (0055 composes 0001/0005/
   0007/0028/0032/0046/0049; 0057 consumes 0055) [PRIMARY / on-disk] (2026-05-30)
3. `shipyard/packages/kernel-schema-registry/{ISchemaRegistry.cs,README.md}` + dir listing (Epochs/Lenses/
   Upcasters/Migration/Compaction) — the SHIPPED schema registry: `RegisterAsync`/`GetAsync`/`ValidateAsync`/
   `ListAsync` + lenses/upcasters/epochs; `JsonSchema.Net`; README maps to "ADR 0055 schema-registry consumer"
   [PRIMARY / on-disk] (2026-05-30)
4. `shipyard/packages/foundation/Assets/README.md` — the SHIPPED entity store: `IEntityStore`/`IVersionStore`
   (SHA-256 chain)/`IAuditLog`/`IHierarchyService` + temporal split/merge/reparent; in-memory, Postgres deferred
   [PRIMARY / on-disk] (2026-05-30)
5. `shipyard/packages/foundation-ships-office/Services/{FormSchema.cs,IFormSchemaStore.cs}` — the STUB:
   6-field `FormSchema` + `FormSchemaStatus{Draft,Published,Archived}` + 2-method `IFormSchemaStore` +
   `NoopFormSchemaStore`; 5 `TODO-RELOCATE-WHEN-CANONICAL … ADR 0055` markers [PRIMARY / on-disk] (2026-05-30)
6. `coordination/_archive/xo-ruling-2026-05-17T02-43Z-cob-w55-p5-local-formschemastore-stub.md` — the ruling
   that authored the stub; named forcing functions for canonicalization ("CO explicitly directs") [PRIMARY /
   beacon] (2026-05-30)
7. `shipyard/packages/blocks-forms/{README.md,FormBlock.razor}` — the PRESENTATION block (static-typed Razor
   over `SunfishForm`); "Future scope: Dynamic-forms substrate consumer (ADR 0055 Phase 1)" [PRIMARY / on-disk]
   (2026-05-30)
8. `shipyard/packages/foundation-catalog/Manifests/Bundles/*.bundle.json` (all 5) — `sunfish.blocks.forms`
   in `requiredModules` of ALL FIVE bundles (incl. property-management) [PRIMARY / on-disk] (2026-05-30)
9. `shipyard/docs/adrs/0005-type-customization-model.md` (Accepted) — 4-layer typed-vs-dynamic model;
   Layer-3 metadata templates + `TenantTemplateOverlay` (RFC-7396) = the Customize-Form-overlay substrate;
   `JsonSchema.Net` pinned [PRIMARY / on-disk] (2026-05-30)
10. `shipyard/docs/adrs/0101-asset-management-bundle-substrate.md` (Accepted Rev 2, 2026-05-29) — the fresh
    substrate-ADR + dual-council (sec-eng + .NET-arch) cadence precedent; references `kernel-schema-registry`
    as the registry-backing for `AssetCategory` [PRIMARY / on-disk] (2026-05-30)
11. `shipyard/docs/adrs/0056-foundation-taxonomy-substrate.md` (status: Accepted) + `packages/foundation-taxonomy/`
    — the Taxonomy substrate 0055 referenced as "future"; now shipped [PRIMARY / on-disk] (2026-05-30)
12. `shipyard/packages/{blocks-workflow/README.md,foundation-rule-engine-event-bridge/}` — state-machine
    primitive + rule-event bridge (the forms→workflow + rules seams) [PRIMARY / on-disk] (2026-05-30)
13. `shipyard/icm/01_discovery/research/c-bundle-substrate-readiness-audit-2026-05-29.md` (ONR) — `blocks.forms
    → COMPLETE (thin but sufficient)`; the four-non-PM-bundle readiness frame [PRIMARY / ONR] (2026-05-30)
14. `shipyard/icm/01_discovery/research/erpnext-conversion-and-backlog-register-2026-05-29.md` (ONR) — the 4
    non-PM bundles are manifest-Draft, no cockpit; ERPNext-parity ~90% at PM vertical [PRIMARY / ONR] (2026-05-30)
15. `shipyard/icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md` (ONR) — Workstream C (4 non-PM bundles);
    line 402 `blocks.forms → blocks-forms YES` (the masking) [PRIMARY / ONR] (2026-05-30)
16. ERPNext DocType / Customize Form / Web Form capability model — `frappeframework.com` DocType docs
    (field types, naming series, permissions, child tables, depends_on conditionals) [SECONDARY / external,
    from prior knowledge — Admiral should spot-verify specific field-type names against current ERPNext docs
    if the ADR pins an exact palette] (general knowledge; not freshly retrieved 2026-05-30)

— ONR, 2026-05-30
