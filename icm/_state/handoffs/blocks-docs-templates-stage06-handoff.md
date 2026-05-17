# Hand-off — `blocks-docs-templates` (ContractTemplate + Field + Clause + RenderJob + ContractInstance)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` — **gated on `blocks-docs-core` PRs 1+2 merged AND `blocks-leases` extant on main (consumer-side smoke only — no source modifications)**
**Workstream:** Phase 3 follow-on — Path II clean-room `blocks-docs-*` cluster, unit #3 (after `blocks-docs-core` and the implicit `blocks-docs` attachment substrate)
**Spec source:** [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) §3.3 (all sub-sections: `ContractTemplate`, `ContractTemplateField`, `ContractTemplateClause`, `TemplateRenderJob`, `ContractInstance`), §4 (ER diagram — Contract/Template/Instance branch), §5.2 (`renderContractFromTemplate` pseudocode), §7.2 (cross-cluster contract to `blocks-work-*`), §7.6 (renderer delegation to `blocks-reports-*`), §8 (FOSS source citations), §9 Q6 (template versioning model)
**ADR:** [ADR 0088 — Anchor as All-In-One Local-First Runtime](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §2 (MIT output), §3 (clean-room discipline)
**CRDT conventions:** [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §1 (ULIDs), §2 (tombstones), §3 (version vectors), §5 (stable string codes), §6 (posted-then-immutable for `ContractInstance` once `SignedAt` is set)
**Event bus:** [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) §1 (envelope), §2 (naming), §3.4 (`Docs.*` catalog — this hand-off adds `Docs.ContractRendered` + `Docs.TemplateRenderJobFailed`)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~12–16h sunfish-PM (6 PRs; ~70–85 tests + docs + attribution + ERPNext template importer + ledger flip)
**PR count:** 6 PRs
**Standing patterns applied:** `pattern-001` (PR 1 — cluster scaffold + repository + DI), `pattern-005` (PR 6 — `Add<Block>()` umbrella), `pattern-006` (PR 6 — `apps/docs/blocks/<cluster>/overview.md`)
**Pre-merge council:**
- **PRs 1, 3, 4, 5, 6:** NOT required (substrate scope; mirrors the `blocks-docs-core` substrate pattern). Standard COB self-audit applies.
- **PR 2 (template rendering engine): SECURITY-ENGINEERING SUBAGENT MANDATORY.** Template rendering is a structural injection-attack surface — user-supplied template body interpolated against user-supplied variables with optional clause composition. The selected engine, sandbox boundary, and variable-escaping discipline must be reviewed before merge. Light architect spot-check on engine choice recommended.
**Attribution required:** Apache OFBiz `accounting/contract` patterns (Apache 2.0 — ContractTemplate + ContractTemplateClause shape inspires the parametric-template surface); DocAssemble (MIT — interview-style variable + field model + render-job semantics). Carry `NOTICE.md` entry. Do **NOT** cite the AGPL/GPL signing/template sources (Documenso, OpenSign, Razuna) — they are STUDY-ONLY per Stage 02 §2 license-posture table.
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs-(core|templates|signing|wiki|dam)$|^blocks-leases$"
```
Expected at this hand-off's start: `blocks-docs-core/` exists with PRs 1+2 of the docs-core hand-off merged; `blocks-leases/` exists on main; nothing matching `blocks-docs-templates/`.

---

## Context

### Phase 3 cluster position

Per Stage 02 schema design §1 and the `blocks-docs-core` hand-off's "follow-on packages" list, the document cluster decomposes into:

```
blocks-docs                 (attachment substrate; ships StorageRef + IBlobStore)      ✓ shipped
  ▼
blocks-docs-core            (Document base + version + folder + permission + retention) ✓ shipped (PRs 1+2)
  ▼
blocks-docs-templates       ← THIS HAND-OFF (ContractTemplate + Field + Clause + RenderJob + ContractInstance)
  ├─► blocks-docs-signing   (SigningWorkflow consumes ContractInstance — follow-on)
  ├─► blocks-docs-wiki      (parallel; no dependency on templates)
  └─► blocks-docs-dam       (parallel; no dependency on templates)
```

`blocks-docs-templates` is the **parametric contract-generation engine** of the cluster. It produces `ContractInstance` rows (specialized `Document` outputs of type `contract-instance`) from `ContractTemplate` + supplied variables + optional clause composition. Once a `ContractInstance` is rendered, it is a first-class `Document` and inherits the full storage / versioning / permission / retention surface from `blocks-docs-core`.

### Why `-templates` ships now

1. **`blocks-docs-core` is shipped.** The Document base entity + StorageRef plumbing + version pointers exist. ContractInstance is a `Document` of type `contract-instance` per §3.1.1's `DocumentType` enum (`'contract-instance'` is already in the enum). The substrate is ready.
2. **`blocks-leases` consumer is on main.** Lease document generation is the priority MVP consumer (per the property-business demo). Today, lease docs are hand-assembled or generated outside the system; this hand-off makes the lease-template → rendered-lease PDF flow first-class.
3. **`blocks-docs-signing` cannot ship without ContractInstance.** SigningWorkflow's `templateId: ID | null` (Stage 02 §3.5.1) wants to reference a `ContractTemplate`, and the workflow's "the document being signed" pointer needs a real `Document.id` that ContractInstance produces. Templates land first so signing can pin its `templateId` FK against a real type.
4. **ERPNext migration coverage.** Wave/Rentler customers carry hand-authored Word/HTML lease templates today. The Pass-N importer for ERPNext templates lands in PR 5 of this hand-off; without it, template migration is manual.

### What this hand-off ships

Per `blocks-docs-schema-design.md` §3.3 (verbatim entity list):

| Entity | Shape source | Notes |
|---|---|---|
| `ContractTemplate` | §3.3.1 | Template metadata + body source + category + signer-role declaration; `Document` of type `contract-template`. |
| `ContractTemplateField` | §3.3.2 | Variable declarations (name, label, fieldKind, validation regex, reference-entity for FK-typed fields). |
| `ContractTemplateClause` | §3.3.3 | Reusable + conditionally-includable clauses; `conditionExpression` is a simple side-effect-free predicate over field values. |
| `TemplateRenderJob` | §3.3.4 | Async render-job orchestrator row; statuses `queued / rendering / complete / failed`; retry counter; output document FK; pinned `templateVersionId`. |
| `ContractInstance` | §3.3.5 | Specialized Document of type `contract-instance`; pins template version; references render job; tracks party FKs + signing workflow FK + status (`rendered / sent / partially-signed / fully-signed / voided`). |

**Plus:**
- The **render engine** (PR 2) — single canonical template engine choice (XO recommendation: **Razor** — see §Critical-design-decision below).
- `ITemplateRenderingService` (PR 2) — the engine wrapper; sandboxed; produces a body in the requested output format (markdown / HTML / PDF / docx).
- `IContractInstanceService` (PR 3) — lifecycle: render → send → record signing-completion → void.
- `IRenderJobOrchestrator` (PR 4) — async job submission, retry, status-tracking, output-document creation.
- `IErpnextContractTemplateImporter` (PR 5) — migrates ERPNext "Contract" + "Email Template" + Frappe "Print Format" data into `ContractTemplate` + `ContractTemplateField`.
- DI umbrella `AddBlocksDocsTemplates()` (PR 6) + `apps/docs/blocks/docs-templates/overview.md` (PR 6) + ledger flip (PR 6).
- Cross-cluster events: `Docs.ContractRendered` (new — emitted by PR 3 on first successful render of a ContractInstance), `Docs.TemplateRenderJobFailed` (new — emitted by PR 4 after retry-exhaustion).

### What this hand-off does NOT ship

- **No e-signature surface.** `SigningWorkflow` / `SigningStep` / `SigningParty` / `Signature` / `SigningAuditLog` are §3.5 scope and ship in the separate **`blocks-docs-signing`** hand-off. Signing references (`ContractInstance.signingWorkflowId`) are typed as `string?` (untyped FK) until the signing cluster lands, then relocate to a strong-typed `SigningWorkflowId`.
- **No PDF rendering primitives.** Per §7.6, PDF generation delegates to `blocks-reports-*` (React-PDF / equivalent). This hand-off ships an `IPdfRenderer` *interface* (in `Services/`) plus a `NoOpPdfRenderer` stub for v1; the real implementation lands when `blocks-reports-pdf` is wired. Template rendering produces **markdown or HTML body strings**; PDF is a deferred orchestration step.
- **No KBA/IDV identity proofing.** Out of scope — signing cluster + identity cluster collaborate later.
- **No collaborative editing for templates.** ContractTemplate body is last-writer-wins per draft version (Loro text-CRDT path not opted-in). If collaborative template authoring is needed later, opt-in is a follow-on workstream.
- **No Razor compiler hardening / AOT-compile path.** PR 2 ships the engine running in interpreted mode under a deny-list sandbox; AOT-compile hardening is a follow-on if perf demands it.
- **No `blocks-work.Contract` writes from this hand-off.** Per §7.2 the operational `Contract` row in `blocks-work-*` is owned by that cluster. This hand-off emits `Docs.ContractRendered`; the future `blocks-work-*` Contract creation handler subscribes and links its `Contract.contractInstanceId` FK to the new `ContractInstance.id`.

### CRDT classification

Per Path II CRDT conventions §1, every entity in this hand-off is classified explicitly:

| Entity | Class | Rationale |
|---|---|---|
| `ContractTemplate` (Draft) | AP-class | Templates are authored collaboratively over time; last-writer-wins on scalars; field/clause sub-lists are OR-Sets. |
| `ContractTemplate` (Active) | CP-class transition | Flipping `isActive: true` is a coordinated act (governance equivalent of "publishing" a Document — same gating semantics as `Document.status = published`). |
| `ContractTemplateField` | AP-class | Field declarations are tied to the template's lifecycle; OR-Set semantics for adds/removes; LWW for label/help-text edits. |
| `ContractTemplateClause` | AP-class | Same as field. |
| `TemplateRenderJob` | CP-class | Job rows are append-only; status transitions are coordinated (`queued → rendering → complete | failed`); no concurrent mutation. |
| `ContractInstance` (rendered, unsigned) | AP-class on metadata; CP-class on lifecycle | Pre-signing metadata edits (notes, expirationDate) AP-merge; status transition is coordinated. |
| `ContractInstance` (fully-signed) | **CP-class, posted-then-immutable** | Once `status ∈ {fully-signed, voided}` the row is immutable (per CRDT conventions §6). |

### Critical design decision — template rendering engine

**Recommendation: Razor (specifically `Microsoft.AspNetCore.Razor.Language` + `RazorTemplateEngine`).**

**Rationale:**

1. **Zero new ecosystem deps.** Razor ships with .NET; no new package family, no new license surface, no new attack surface beyond what the rest of Sunfish already accepts.
2. **C# native.** Field/clause expressions can be expressed in C# without an FFI marshalling layer; the team already reads C#.
3. **Sandboxable.** Razor's `RazorEngine` (the in-process variant) can be restricted via:
   - A **deny-listed BaseImports** set (block `System.IO`, `System.Net`, `System.Reflection`, `System.Diagnostics.Process`, `System.Threading.Thread`, etc.).
   - Compilation against a curated `MetadataReference` set (only `mscorlib` + `System.Runtime` + Sunfish-blessed `blocks-docs-templates` model types).
   - Execution in a child `AssemblyLoadContext` (isolation; collectible AssemblyLoadContext per render — bounded memory).
   - A `CancellationToken` + execution-time-budget guard (default 5s; configurable per render job).
   - Disallow `@inject`, `@inherits`, `@functions` blocks at the lexer level via a custom `RazorProjectEngineBuilder` configuration.
4. **Mature tooling.** Visual Studio + dotnet CLI understand `.cshtml`; template authors can preview locally.
5. **Aligns with Anchor's existing stack.** Anchor uses Blazor (which is Razor underneath) — same mental model.

**Alternatives considered and rejected:**

| Engine | License | Why not |
|---|---|---|
| **Liquid (DotLiquid / Fluid)** | Apache 2.0 | Adds a dependency + parsing surface; tags + filters are less expressive than Razor for clause conditions; introduces a separate template language for the team to learn. Acceptable fallback if XO/CO determines Razor's sandbox surface is unacceptable. |
| **Handlebars.Net** | MIT | Logic-less by design; clause conditions become awkward (requires registering helpers; helpers are a privileged code surface). Less expressive for the §3.3.3 `conditionExpression` use case. |
| **Scriban** | BSD-2 | Sandbox-friendly language; small footprint; less mainstream — team would have to learn it; weaker tooling story. |
| **Custom Mustache-style** | n/a | Re-inventing parser is wasted work and an injection surface we'd own forever. Strictly worse. |

**Decision to escalate to XO/CO:** if the security-engineering subagent on PR 2 (mandatory — see §Pre-merge council) raises blocking concerns about the Razor sandbox surface, halt and file `cob-question-*` proposing Fluent (Apache 2.0 Liquid implementation) as the fallback. **XO recommendation stands as Razor pending council disposition.**

### Cluster-internal dependencies summary

```
blocks-docs                                  (StorageRef + IBlobStore — substrate)
  ▼
blocks-docs-core                             (Document + DocumentVersion + IDocumentCommandService)
  ▼
blocks-docs-templates                        (THIS HAND-OFF)
  │
  ├─► blocks-docs-signing                    (consumes ContractInstance + signingWorkflowId FK — follow-on)
  ├─► blocks-work-*                          (consumes Docs.ContractRendered event — follow-on)
  ├─► blocks-leases                          (downstream consumer — calls IContractInstanceService.RenderAsync for lease docs; no API changes here — late-binding)
  └─► blocks-reports-* (PDF renderer)        (provides IPdfRenderer real impl — follow-on; stub here)
```

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify `blocks-docs-core` is shipped (PRs 1 + 2 merged).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/Document.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Services/IDocumentCommandService.cs
   ```
   Expected: directory + files exist. If absent, **STOP** and file `cob-question-2026-05-XXTHH-MMZ-docs-templates-blocked-on-docs-core.md` — the docs-core hand-off must land first.

2. **Verify `blocks-docs` (attachment substrate) is shipped and exposes `StorageRef`.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/ 2>&1
   grep -rln "StorageRef\|IBlobStore" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/ 2>/dev/null | head -5
   ```
   Expected: directory + types exist. If `StorageRef` is missing, follow the same fallback as `blocks-docs-core` (use `string? StorageRef` as a URI placeholder per docs-core hand-off Halt §2). Note the deferral in the README.

3. **Verify `blocks-leases` is on main (consumer-side smoke target).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-leases/
   ```
   Expected: directory exists. (Confirmed per the audit list at the top.) This hand-off **does not modify `blocks-leases`** — it only provides the surface the future `blocks-leases-template-driven-doc-generation` hand-off will consume. Smoke is verifying the consumer is buildable + the API surface this hand-off ships compiles when invoked from a future caller.

4. **Verify `PartyId` cross-cluster type availability.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/Models/PartyId.cs 2>&1
   grep -rln "PartyId" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/ 2>/dev/null | head -5
   ```
   Expected: `blocks-people-foundation.PartyId` exists (used by `blocks-docs-core` per its hand-off csproj). This hand-off inherits the same dep through the docs-core project reference; no new direct dep.

5. **Verify Razor packages are accepted at the solution level.**
   ```bash
   grep -rln "Microsoft.AspNetCore.Razor.Language\|Microsoft.CodeAnalysis.CSharp" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ 2>/dev/null | head -5
   ```
   Expected: Razor.Language is likely already pulled in by `accelerators/anchor` (Blazor). If absent at the package level, PR 2 adds `Microsoft.AspNetCore.Razor.Language` + `Microsoft.CodeAnalysis.CSharp` (both Apache-2.0; both Microsoft-published; both pre-cleared per Sunfish supply-chain policy). If the solution rejects the dependency at NuGet restore (e.g., security policy disallows direct Roslyn pull), **halt and file `cob-question-*`** before PR 2 proceeds — engine choice may need to escalate to Liquid.

6. **Confirm package name availability.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs-templates$|^blocks-templates$|^blocks-contract"
   ```
   Expected: empty.

7. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` or higher. Hand-off is `ready-to-build` regardless of formal flip (CO directive operative).

8. **Confirm no parallel-session PRs touch the target area.**
   ```bash
   gh pr list --state open --search "blocks-docs-templates in:title,body"
   gh pr list --state open --search "ContractTemplate OR ContractInstance OR TemplateRenderJob in:title,body"
   gh pr list --state open --search "blocks-docs-core in:title,body"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open touching docs-core source files, file `cob-question-*`.

9. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

10. **Read the Stage 02 design source sections.** Skim `blocks-docs-schema-design.md` §3.3 (all sub-sections), §4 (Contract branch of the ER diagram), §5.2 (`renderContractFromTemplate` pseudocode), §7.2 (cross-cluster contract to `blocks-work-*`), §7.6 (PDF renderer delegation), §9 Q6 (template versioning model — Q6 recommendation `(a)` adopted: ContractTemplate IS a Document, so `templateVersionId == Document.currentVersionId`). Skim `crdt-friendly-schema-conventions.md` §1, §2, §3, §5, §6. Skim `cross-cluster-event-bus-design.md` §3.4 (Docs.* catalog — this hand-off adds two entries).

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility:

- **PR 1:** Scaffold + ContractTemplate / ContractTemplateField / ContractTemplateClause / ContractInstance / TemplateRenderJob entities + IDs + status enums + repositories + initial DI (substrate per `pattern-001`)
- **PR 2:** Template rendering engine — `ITemplateRenderingService` + Razor implementation + sandbox + variable validation + clause-expression evaluator + tests (**security council mandatory**)
- **PR 3:** `IContractInstanceService` — lifecycle service: render → record-send → record-signing → void; emits `Docs.ContractRendered`
- **PR 4:** `IRenderJobOrchestrator` — async render-job submission + retry + status tracking + output-document creation via `blocks-docs-core.IDocumentCommandService.CreateAsync`; emits `Docs.TemplateRenderJobFailed`
- **PR 5:** ERPNext importer — `IErpnextContractTemplateImporter` (consumes ERPNext "Contract", "Email Template", Frappe "Print Format" sources; idempotent upsert)
- **PR 6:** DI umbrella `AddBlocksDocsTemplates()` (`pattern-005`) + `apps/docs/blocks/docs-templates/overview.md` (`pattern-006`) + NOTICE.md + README.md + ledger flip

**Sequencing:**

- PR 1 is the substrate; everything else depends on it.
- PR 2 is sequential after PR 1; carries the architectural decision + security review.
- PR 3 sequences after PR 2 (uses `ITemplateRenderingService`).
- PR 4 sequences after PR 3 (creates output Documents via the lifecycle service; the orchestrator's `submit → render → complete` chain uses PR 3's surface).
- PR 5 can parallelize with PR 4 once PRs 1 + 2 are in (importer only needs entity types + the rendering engine for validation tests).
- PR 6 sequences last (ledger flip + docs page).

---

### PR 1 — Scaffold + entities + IDs + status enums + repositories + DI

**Estimated effort:** ~2–3h
**Scope:** new package `blocks-docs-templates`; 5 entity record types; 5 strong-typed IDs; 3 supporting enums; 5 repository contracts + 5 in-memory implementations; initial DI extension `AddBlocksDocsTemplates()`
**Standing pattern:** `pattern-001` (cluster scaffold)
**Commit subject:** `feat(blocks-docs-templates): PR 1 — scaffold + ContractTemplate + ContractInstance + RenderJob entities + repositories + DI`
**Council:** SKIP (matches `pattern-001`)
**Branch:** `cob/blocks-docs-templates-scaffold`

#### Package skeleton

```
packages/blocks-docs-templates/
├── Sunfish.Blocks.DocsTemplates.csproj
├── NOTICE.md                                  (OFBiz + DocAssemble attribution)
├── README.md                                  (citations + DI usage)
├── Models/
│   ├── ContractTemplate.cs
│   ├── ContractTemplateId.cs
│   ├── ContractTemplateField.cs
│   ├── ContractTemplateFieldId.cs
│   ├── ContractTemplateClause.cs
│   ├── ContractTemplateClauseId.cs
│   ├── TemplateRenderJob.cs
│   ├── TemplateRenderJobId.cs
│   ├── ContractInstance.cs
│   ├── ContractInstanceId.cs
│   ├── ContractTemplateCategory.cs            (enum: lease / employment / vendor / nda / service / custom)
│   ├── ContractTemplateBodyFormat.cs          (enum: markdown / html / docx / pdfForm)
│   ├── ContractTemplateFieldKind.cs           (enum: text / number / date / currency / enum / boolean / multiLine / reference)
│   ├── TemplateRenderJobStatus.cs             (enum: queued / rendering / complete / failed)
│   ├── ContractInstanceStatus.cs              (enum: rendered / sent / partiallySigned / fullySigned / voided)
│   ├── RenderOutputFormat.cs                  (enum: pdf / docx / html / markdown)
│   └── BlocksDocsTemplatesOptions.cs          (RenderTimeoutSeconds, MaxConcurrentRenders, MaxRenderJobRetries, DefaultPdfRendererKind, etc.)
├── Services/
│   ├── IContractTemplateRepository.cs
│   ├── InMemoryContractTemplateRepository.cs
│   ├── IContractTemplateFieldRepository.cs
│   ├── InMemoryContractTemplateFieldRepository.cs
│   ├── IContractTemplateClauseRepository.cs
│   ├── InMemoryContractTemplateClauseRepository.cs
│   ├── ITemplateRenderJobRepository.cs
│   ├── InMemoryTemplateRenderJobRepository.cs
│   ├── IContractInstanceRepository.cs
│   └── InMemoryContractInstanceRepository.cs
├── DependencyInjection/
│   └── BlocksDocsTemplatesServiceCollectionExtensions.cs
└── tests/
    └── Sunfish.Blocks.DocsTemplates.Tests/
        ├── Sunfish.Blocks.DocsTemplates.Tests.csproj
        ├── ContractTemplateRecordTests.cs
        ├── ContractTemplateFieldRecordTests.cs
        ├── ContractTemplateClauseRecordTests.cs
        ├── TemplateRenderJobRecordTests.cs
        ├── ContractInstanceRecordTests.cs
        ├── ContractInstanceStatusTransitionTests.cs
        ├── ContractTemplateRepositoryTests.cs
        ├── ContractInstanceRepositoryTests.cs
        └── TemplateRenderJobRepositoryTests.cs
```

#### csproj dependencies

`Sunfish.Blocks.DocsTemplates.csproj` references:

```
foundation/Sunfish.Foundation.csproj                              (ULID + Instant + common contracts)
foundation-events/Sunfish.Foundation.Events.csproj                (ISunfishDomainEvent envelope)
blocks-docs/Sunfish.Blocks.Docs.csproj                            (StorageRef + IBlobStore)
blocks-docs-core/Sunfish.Blocks.DocsCore.csproj                   (Document + DocumentVersion + IDocumentCommandService)
blocks-people-foundation/Sunfish.Blocks.People.Foundation.csproj  (PartyId — for party1Id / party2Id / submittedBy / etc.)
```

**No** dependency on `blocks-financial-*`, `blocks-work-*`, `blocks-docs-signing`, `blocks-docs-wiki`, `blocks-docs-dam`, `blocks-reports-*` — those clusters consume **this** package; consumer flow is downward.

#### Entity-shape spec (no code — types only; PR author binds to C# records per the docs-core pattern)

**`ContractTemplate`** (per §3.3.1):
- `Id: ContractTemplateId` (ULID strong-typed; new)
- `TenantId: string` (ULID/tenant scope; analyzer-enforced per cluster convention)
- `DocumentId: DocumentId` (FK to `blocks-docs-core.Document`; the template's body lives in DocumentVersion per Q6 recommendation `(a)`)
- `Name: string` (1..200)
- `Category: ContractTemplateCategory`
- `BodyFormat: ContractTemplateBodyFormat`
- `DefaultVariables: IReadOnlyDictionary<string, string>` (serialized as JSON in storage; in-memory dict for v1; values are pre-stringified because field-kind coercion happens at render-time)
- `RequiredSignerRoles: IReadOnlyList<string>` (e.g., `["landlord", "tenant", "witness"]`)
- `DefaultSigningWorkflowId: string?` (untyped FK until `blocks-docs-signing` ships; see §What-this-hand-off-does-NOT-ship)
- `IsActive: bool` (defaults `false`; flipping to `true` is the publishing act; CP-class transition per CRDT classification table)
- Standard audit envelope: `CreatedAtUtc, CreatedByPartyId, UpdatedAtUtc, UpdatedByPartyId, Version, RevisionVector` (per CRDT conventions §3)
- Soft-delete tombstone: `ArchivedAt: Instant?`, `ArchivedByPartyId: PartyId?`, `ArchivedReason: string?` (per CRDT conventions §2)

**Validation invariants (enforced at repository-write-time, mirrored in test PR 1):**
- `Name` is 1..200 chars after trim.
- `DefaultVariables` keys match `/^[a-zA-Z_][a-zA-Z0-9_]{0,63}$/` (legal identifier-style; required for safe template interpolation).
- `RequiredSignerRoles` entries are non-empty + non-duplicate after lowercase normalization.
- An active template (`IsActive == true`) cannot be soft-deleted in the same upsert (must be flipped to `IsActive = false` first).

**`ContractTemplateField`** (per §3.3.2):
- `Id: ContractTemplateFieldId`
- `TenantId: string`
- `TemplateId: ContractTemplateId` (FK)
- `Name: string` (matches `{{variable}}` key in body; matches the identifier regex above)
- `Label: string` (UI label; 1..200)
- `FieldKind: ContractTemplateFieldKind` (text / number / date / currency / enum / boolean / multiLine / reference)
- `Required: bool`
- `DefaultValueJson: string?` (JSON-serialized scalar; deserialized per FieldKind at render-time)
- `EnumOptions: IReadOnlyList<string>?` (required when `FieldKind == enum`)
- `ReferenceEntity: string?` (required when `FieldKind == reference`; values like `"Property"`, `"Employee"`, `"Party"` — resolved by a `IReferenceResolver` registered by the consumer cluster; v1 ships an in-memory stub that always returns "found" — see PR 2)
- `ValidationRegex: string?` (compiled with timeout — guard against ReDoS; see PR 2 security notes)
- `HelpText: string?`
- `SortOrder: int`
- Standard audit envelope + soft-delete tombstone.

**Validation invariants:**
- `Name` unique within `(tenantId, templateId)`.
- `FieldKind == enum` ⟹ `EnumOptions` non-empty.
- `FieldKind == reference` ⟹ `ReferenceEntity` non-empty + matches the registered set (validated at upsert time against `IReferenceEntityRegistry` — stub in PR 1; real list grows as consumer clusters register).
- `ValidationRegex`, if provided, must compile under a 250ms timeout (PR 2 hardens this; PR 1's tests only assert non-malformed input passes through).

**`ContractTemplateClause`** (per §3.3.3):
- `Id: ContractTemplateClauseId`
- `TenantId: string`
- `TemplateId: ContractTemplateId` (FK)
- `Name: string` (1..200; unique within template)
- `Body: string` (markdown clause text with same `{{variable}}` syntax as the parent template)
- `ConditionExpression: string?` (a side-effect-free predicate over field values; v1 supports a strict subset — see PR 2 §Condition-expression-grammar)
- `SortOrder: int`
- `IsOptional: bool` (when `false`, the clause is always included if `ConditionExpression` is null or evaluates true; when `true`, inclusion is also gated on the caller's `optionalClauseIds` list per §5.2 pseudocode)
- Standard audit envelope + soft-delete tombstone.

**Validation invariants:**
- `Name` unique within `(tenantId, templateId)`.
- `ConditionExpression`, if provided, must parse cleanly under the v1 grammar (PR 2 ships the parser; PR 1 records the string but does not parse).
- `Body` cannot reference `{{variable}}` keys that are not declared on the parent template's `ContractTemplateField` set (validated at upsert time by joining against the field repository — soft-validation: log a warning, do not throw; the render-time validator in PR 2 is authoritative).

**`TemplateRenderJob`** (per §3.3.4):
- `Id: TemplateRenderJobId`
- `TenantId: string`
- `TemplateId: ContractTemplateId`
- `TemplateVersionId: DocumentVersionId` (pinned at job submission per §3.3.4 — the rendered output is bound to a specific version)
- `Variables: IReadOnlyDictionary<string, string>` (resolved values; same serialization rule as `DefaultVariables`)
- `ResolvedClauseIds: IReadOnlyList<ContractTemplateClauseId>` (which optional clauses applied)
- `OutputFormat: RenderOutputFormat`
- `Status: TemplateRenderJobStatus`
- `OutputDocumentId: DocumentId?` (FK to the rendered output `Document`; null while not complete)
- `RetryCount: int` (0..MaxRenderJobRetries)
- `ErrorMessage: string?` (when `Status == failed`)
- `SubmittedByPartyId: PartyId`
- `SubmittedAtUtc: Instant`
- `StartedAtUtc: Instant?` (set when transition queued → rendering)
- `CompletedAtUtc: Instant?` (set when transition rendering → complete | failed)
- Standard audit envelope (no soft-delete; jobs are append-only history)

**Validation invariants:**
- Status transitions follow: `queued → rendering`, `rendering → (complete | failed)`, `failed → queued` (retry — only when `RetryCount < MaxRenderJobRetries`), all other transitions rejected.
- `OutputDocumentId` non-null ⟺ `Status == complete`.
- `ErrorMessage` non-null ⟺ `Status == failed`.
- `StartedAtUtc ≤ CompletedAtUtc` when both are set.

**`ContractInstance`** (per §3.3.5):
- `Id: ContractInstanceId`
- `TenantId: string`
- `TemplateId: ContractTemplateId`
- `TemplateVersionId: DocumentVersionId` (which template version was rendered; pinned for tamper detection)
- `RenderJobId: TemplateRenderJobId`
- `OutputDocumentId: DocumentId` (FK Document; non-null — `ContractInstance` only exists after a successful render)
- `WorkContractId: string?` (untyped FK to `blocks-work.Contract` until that cluster wires through — see §7.2; relocates to `WorkContractId` strong-typed when available)
- `Party1Id: PartyId?` (e.g., landlord)
- `Party2Id: PartyId?` (e.g., tenant)
- `SigningWorkflowId: string?` (untyped FK until `blocks-docs-signing` ships)
- `Status: ContractInstanceStatus` (rendered / sent / partiallySigned / fullySigned / voided)
- `EffectiveDate: LocalDate?`
- `ExpirationDate: LocalDate?`
- `SignedAt: Instant?` (set when `Status` first transitions into `fullySigned`; **posted-then-immutable trigger** per CRDT conventions §6)
- `VoidReason: string?` (set when `Status == voided`)
- Standard audit envelope + soft-delete tombstone

**Validation invariants:**
- `Status` advances monotonically except `voided` which is terminal from any prior state.
- `SignedAt` non-null ⟹ `Status == fullySigned`.
- Once `Status == fullySigned`, the following fields are immutable: `TemplateId, TemplateVersionId, RenderJobId, OutputDocumentId, Party1Id, Party2Id, SigningWorkflowId, EffectiveDate, ExpirationDate, SignedAt`. The `Status` may still transition to `voided` (voiding a signed contract is allowed; see §Halt-conditions §3 for the audit-trail requirement).
- `VoidReason` non-null ⟺ `Status == voided`.

#### Strong-typed IDs

Pattern follows `blocks-docs-core` exactly (ULID string-backed; struct with explicit conversion; `NewId()` factory; `Empty` sentinel). Five new ID types: `ContractTemplateId`, `ContractTemplateFieldId`, `ContractTemplateClauseId`, `TemplateRenderJobId`, `ContractInstanceId`.

#### Status-transition validation helpers

Two static helper classes (each with `IsAllowed(from, to) → bool` + `EnsureAllowed(from, to)` throwing variant):

- `TemplateRenderJobStatusTransitions` — encodes the queued → rendering → complete | failed | (retry → queued) graph.
- `ContractInstanceStatusTransitions` — encodes the rendered → sent → partiallySigned → fullySigned graph + voided-from-any-state terminal.

#### Repository contracts

All 5 repositories follow the docs-core pattern (`IXxxRepository` interface in `Services/` + `InMemoryXxxRepository` implementation; `ConcurrentDictionary<XxxId, Xxx>` storage; tenant-scoping enforced via `where x.TenantId == tenantId` filter on every read). Operations:

- `IContractTemplateRepository`: `GetByIdAsync`, `GetByExternalRefAsync(source, externalRefId)` (idempotency hook for the ERPNext importer in PR 5), `QueryActiveAsync(tenantId, category?)`, `UpsertAsync`, `TombstoneAsync`.
- `IContractTemplateFieldRepository`: `GetByIdAsync`, `QueryByTemplateAsync(templateId)`, `UpsertAsync`, `TombstoneAsync`.
- `IContractTemplateClauseRepository`: `GetByIdAsync`, `QueryByTemplateAsync(templateId)`, `UpsertAsync`, `TombstoneAsync`.
- `ITemplateRenderJobRepository`: `GetByIdAsync`, `QueryByStatusAsync(status, limit)` (for the orchestrator's pull-loop in PR 4), `QueryByTemplateAsync(templateId, fromUtc, toUtc)`, `UpsertAsync` (no tombstone — jobs are append-only).
- `IContractInstanceRepository`: `GetByIdAsync`, `GetByOutputDocumentIdAsync(documentId)` (reverse lookup), `GetByRenderJobIdAsync(jobId)`, `QueryByTemplateAsync(templateId)`, `QueryByPartyAsync(partyId)`, `QueryByStatusAsync(status)`, `UpsertAsync`, `TombstoneAsync`.

#### DI extension (PR 1 minimal scope)

`BlocksDocsTemplatesServiceCollectionExtensions.AddBlocksDocsTemplates(this IServiceCollection services, Action<BlocksDocsTemplatesOptions>? configure = null)` — registers all 5 in-memory repositories as Scoped (matches `blocks-docs-core` lifetime pattern + the `Use Scoped for In-Memory* lifetimes` chore from bug-AddInMemory-Scoped). Binds `BlocksDocsTemplatesOptions`. **Subsequent PRs extend this method** (PR 2 adds `ITemplateRenderingService` + sandbox config; PR 3 adds `IContractInstanceService`; PR 4 adds `IRenderJobOrchestrator`; PR 5 adds `IErpnextContractTemplateImporter`).

#### Tests (PR 1) — ~18–22 tests

- `ContractTemplateRecordTests`: field-preservation + name-trim invariant + variable-key regex enforcement + active+archived rejection.
- `ContractTemplateFieldRecordTests`: uniqueness of `Name` within `(tenantId, templateId)`; enum requires `EnumOptions`; reference requires `ReferenceEntity`.
- `ContractTemplateClauseRecordTests`: uniqueness; soft-validation (undefined `{{variable}}` logs warning, does not throw).
- `TemplateRenderJobRecordTests`: status-transition graph (allowed + disallowed); retry-cap enforcement; ErrorMessage ⟺ failed invariant.
- `ContractInstanceRecordTests`: status-transition graph; SignedAt ⟺ fullySigned invariant; posted-then-immutable enforcement (attempting to mutate Party1Id on a `fullySigned` instance rejects).
- `ContractInstanceStatusTransitionTests`: 6×6 transition matrix.
- `ContractTemplateRepositoryTests`: round-trip + tenant isolation + ExternalRef lookup + ActiveAsync filter (`IsActive == true`).
- `ContractInstanceRepositoryTests`: round-trip + party query + reverse-lookup by `OutputDocumentId`.
- `TemplateRenderJobRepositoryTests`: round-trip + status-query + append-only behavior (tombstone surface absent).

#### Verification

- `dotnet build` succeeds for the new package + the test project compiles against the docs-core + blocks-docs + blocks-people-foundation references.
- `dotnet test packages/blocks-docs-templates/tests/` passes all PR 1 tests.
- `grep -r "Sunfish.Blocks.DocsTemplates" packages/blocks-docs-templates/` returns hits in every `.cs` file (namespace sanity).

#### Do NOT in this PR

- Do NOT implement the rendering engine. PR 2 ships it.
- Do NOT post `Docs.ContractRendered` events. PR 3 ships event emission.
- Do NOT touch `blocks-docs-core/`, `blocks-leases/`, or any consumer cluster.
- Do NOT register `IRenderJobOrchestrator` — PR 4 ships it.
- Do NOT seed any production templates.

---

### PR 2 — Template rendering engine (`ITemplateRenderingService` + Razor implementation + sandbox + variable validation + clause evaluator) — **SECURITY COUNCIL MANDATORY**

**Estimated effort:** ~3–4h
**Scope:** the canonical render engine; variable typing/coercion; clause-condition evaluator; sandbox; resource budget enforcement; one Razor implementation behind the interface
**Standing pattern:** NONE (novel surface; security council mandatory)
**Commit subject:** `feat(blocks-docs-templates): PR 2 — ITemplateRenderingService Razor engine with sandbox per Stage 02 §5.2`
**Depends on:** PR 1 merged
**Council:** **MANDATORY security-engineering subagent spot-review** (Opus + xhigh, per `feedback_council_reviews_use_best_model_xhigh`). Optional `.NET architect` spot-check on engine choice. Run BEFORE auto-merge per `feedback_council_before_automerge`. PR opens as `--draft` per `feedback_pr_automerge_before_amendment_landed`.
**Branch:** `cob/blocks-docs-templates-render-engine`

#### Engine choice (recap — see §Critical-design-decision above)

**Razor.** Specifically `Microsoft.AspNetCore.Razor.Language` for parsing + `Microsoft.CodeAnalysis.CSharp` for compilation + `System.Runtime.Loader.AssemblyLoadContext` for isolation. Add the two NuGet refs to the package csproj in PR 2.

If the security council on this PR returns BLOCKING findings on the Razor sandbox surface, **halt and file `cob-question-2026-05-XXTHH-MMZ-docs-templates-engine-fallback.md`** proposing Fluent (Apache 2.0 Liquid implementation) as the fallback engine. Do not attempt mitigations beyond what the council recommends without XO/CO sign-off.

#### `ITemplateRenderingService` contract

The service exposes a single render entrypoint per template + variables + (optional) selected clause IDs. Signature shape (no impl code — interface description):

- **Input:** `RenderTemplateRequest` carrying `templateId`, the `templateBody` (already loaded by the caller from `Document.currentVersion.body`), the field list (already loaded by the caller from the field repository), the clause list (already loaded), `variables: IReadOnlyDictionary<string, string>` (the rendered-from values, stringified), `optionalClauseIds: IReadOnlySet<ContractTemplateClauseId>`, `outputFormat: RenderOutputFormat`, optional `cancellationToken`.
- **Output:** `RenderTemplateResult` carrying `error: RenderTemplateError` enum (`None / VariableValidationFailed / ClauseExpressionFailed / RenderTimeout / SandboxViolation / OutputFormatUnsupported / EngineInternalError`), `detail: string?` (human-readable), `body: string?` (the rendered text — markdown or HTML or null when the requested output is binary), `binary: byte[]?` (for docx/pdf paths; null in v1 for those formats — the orchestrator routes the rendered markdown/html to an `IPdfRenderer` separately in PR 4), `resolvedClauseIds: IReadOnlyList<ContractTemplateClauseId>`, `metrics: RenderMetrics` (duration, peak memory bucket, sandbox-violation-count).

The service does **NOT** persist anything. Persistence is the orchestrator's job (PR 4).

#### Variable-validation algorithm (per §5.2 step 1)

For each `ContractTemplateField`:
1. If `Required` and the variable key is absent or empty → `VariableValidationFailed` with the missing field name.
2. Coerce the string value per `FieldKind`:
   - `text` / `multiLine` — pass-through; max 10 KB after trim (configurable via `BlocksDocsTemplatesOptions.MaxFieldTextLength`).
   - `number` — `decimal.TryParse` under `CultureInfo.InvariantCulture`; reject on fail.
   - `date` — `LocalDate.TryParseExact` under ISO 8601 (`yyyy-MM-dd`); reject on fail.
   - `currency` — same as `number` (rounded display is a renderer concern, not a validation concern).
   - `enum` — must match a value in `EnumOptions` (case-sensitive).
   - `boolean` — accept `true / false / yes / no / 1 / 0` case-insensitively; otherwise reject.
   - `reference` — call `IReferenceResolver.ResolveAsync(referenceEntity, value, tenantId)` (stub in PR 2; concrete consumer-side resolvers register later); reject on `null` return.
3. If `ValidationRegex` is non-null, compile it with `RegexOptions.NonBacktracking | RegexOptions.CultureInvariant` and a 250ms timeout (`Regex.MatchTimeout`); reject on no match.

Accumulate all errors before returning (multi-error feedback, not first-fail-throw — gives the UI a single round-trip).

#### Clause-condition grammar (v1 — strict subset)

Per §3.3.3 a clause may carry a `ConditionExpression` like `"leaseKind === 'commercial'"`. v1 supports a strict subset of JavaScript-style expressions (parsed via Roslyn's `CSharpScript` is **NOT** used — too privileged a surface; we ship a hand-rolled recursive-descent parser instead). The grammar:

```
expr        := orExpr
orExpr      := andExpr ('||' andExpr)*
andExpr     := notExpr ('&&' notExpr)*
notExpr     := ('!' notExpr) | cmpExpr
cmpExpr     := atom (('===' | '!==' | '<' | '<=' | '>' | '>=') atom)?
atom        := identifier | string | number | boolean | '(' expr ')'
identifier  := /[a-zA-Z_][a-zA-Z0-9_]{0,63}/
string      := '\'' [^']* '\''       (single quote only; no escape; no interpolation)
number      := /-?[0-9]+(\.[0-9]+)?/
boolean     := 'true' | 'false'
```

Identifiers resolve against the validated-variable dictionary. Type-strictness: `===` and `!==` require operands of identical kind (string vs number) — otherwise the whole expression evaluates `false` (consistent with `RenderTemplateError.None` plus a soft warning in `detail`, **not** a render-failure — clauses that misparse simply skip).

**Why hand-rolled instead of an expression library:**
- Surface is tiny; parser is ~150 lines.
- No reflection; no `eval` family; no script-engine bootstrap; no GC pressure.
- Easy to fuzz (PR 2 ships a 30-input regression battery — see §Tests).

If a clause `ConditionExpression` fails to parse at render time, the clause is **excluded** and a soft warning is added to `metrics.warnings` (the warning includes the template + clause names). Does **NOT** fail the render. (Hard-fail behavior is reserved for required-but-missing fields — those break the contract.)

#### Sandbox boundary

The Razor execution sandbox is the security council's primary review surface. The non-negotiable boundary:

1. **Curated `MetadataReference` set.** The Razor compiler is given only:
   - `System.Runtime.dll`
   - `System.Linq.dll`
   - `System.Collections.dll`
   - `Sunfish.Blocks.DocsTemplates.Models.*` (read-only DTOs for fields/clauses — passed to the template as a strongly-typed model)
   - Explicitly **excluded**: `System.IO`, `System.Net`, `System.Net.Http`, `System.Reflection.Emit`, `System.Diagnostics.Process`, `System.Threading.Thread`, `System.Security.Cryptography`, anything Anchor-side (no `Sunfish.Foundation.*` types reachable from template code; only the explicit model DTO).
2. **Deny-listed Razor directives.** `@inject`, `@inherits`, `@functions`, `@code`, `@using` (all `@using` declarations rejected — the curated reference set is final), `@implements`, `@page`, `@layout` are all rejected at the lexer level (custom `RazorProjectEngineBuilder` configuration; reject + emit `SandboxViolation` with the offending directive name).
3. **Collectible `AssemblyLoadContext` per render.** Compile + execute in a fresh `AssemblyLoadContext`; mark `IsCollectible = true`; after render, drop the reference (lets GC reclaim the rendered assembly; bounds memory growth on repeated renders).
4. **Execution budget.** Wrap the render in a `CancellationTokenSource` (default 5s; overridable via `BlocksDocsTemplatesOptions.RenderTimeoutSeconds`; per-job override via `TemplateRenderJob` carries the budget). Timeout fires `RenderTimeout` error.
5. **Memory budget (soft).** Track `GC.GetTotalAllocatedBytes(precise: false)` before/after; if delta > `BlocksDocsTemplatesOptions.SoftMemoryBudgetBytes` (default 50 MB), record in `metrics.warnings` (do not fail; informational).
6. **No I/O reachable.** With `System.IO` excluded from the reference set, file reads/writes are compile-errors. Defense in depth: a `RestrictedHostObjectMissing` exception fires if any reflection-based escape attempts to load `System.IO` at runtime (caught + mapped to `SandboxViolation`).
7. **No network.** Same as above; `System.Net` excluded.
8. **No reflection emit.** Excluded.
9. **Template-output sanitization (HTML target).** When `outputFormat == html`, all `{{variable}}` interpolation HTML-encodes by default. A `@RawText(variableName)` helper exists for trusted-content passes (e.g., when the field is itself rendered HTML from a prior pass); the helper is documented + reviewed by security council as the **only** XSS-escape valve.

#### `IReferenceResolver` (sub-contract)

Stub interface for reference-typed field resolution:

- `Task<ReferenceResolution> ResolveAsync(string referenceEntity, string idValue, string tenantId, CancellationToken ct = default)` returning `ReferenceResolution` (record with `bool Resolved`, `string? DisplayName`, `string? Detail`).

PR 2 ships `AlwaysFoundReferenceResolver` as the v1 in-memory implementation (returns `Resolved: true, DisplayName: idValue, Detail: null` for any reference). This is registered as a transient default; consumer clusters override later (e.g., `blocks-property.PropertyReferenceResolver` will replace the stub when a real registration happens). Document this clearly: the stub explicitly does NOT enforce existence; production deployments wire concrete resolvers in DI **before** templates with `FieldKind == reference` are rendered.

#### DI registration (extends PR 1's `AddBlocksDocsTemplates`)

- `services.AddSingleton<ITemplateRenderingService, RazorTemplateRenderingService>()` (singleton because the compiler/host is process-wide; thread-safety enforced internally via per-render `AssemblyLoadContext`).
- `services.AddSingleton<IReferenceResolver, AlwaysFoundReferenceResolver>()` (with the disclaimer above).
- `BlocksDocsTemplatesOptions` extended with: `RenderTimeoutSeconds: int = 5`, `SoftMemoryBudgetBytes: long = 50_000_000`, `MaxFieldTextLength: int = 10_240`, `AllowRazorRawText: bool = true` (set `false` in high-security deployments to disable the `@RawText` escape valve).

#### Tests (PR 2) — ~22–28 tests

`tests/RazorTemplateRenderingServiceTests.cs`:
- `Render_SimpleSubstitution_ReplacesVariable` (`{{tenantName}}` → `"Acero Properties LLC"`).
- `Render_MissingRequiredField_ReturnsVariableValidationFailed`.
- `Render_TypeMismatchOnNumberField_ReturnsVariableValidationFailed`.
- `Render_DateFormatRejected_ReturnsVariableValidationFailed` (e.g. `"05/15/2026"` against ISO-only validator).
- `Render_EnumFieldOutsideOptions_ReturnsVariableValidationFailed`.
- `Render_ReferenceField_CallsResolverAndAcceptsResolved`.
- `Render_ReferenceField_UnresolvedRejects`.
- `Render_RegexValidation_RejectsNonMatch`.
- `Render_RegexValidation_TimesOutOnReDoSAttempt` (regex pathological input; expect timeout-mapped error).
- `Render_OptionalClauseIncluded_WhenIdInOptionalList_AndConditionTrue`.
- `Render_OptionalClauseExcluded_WhenIdNotInList`.
- `Render_RequiredClauseIncluded_WhenConditionTrue`.
- `Render_RequiredClauseExcluded_WhenConditionFalse`.
- `Render_HtmlOutput_EscapesByDefault` (`<script>` in a variable renders as `&lt;script&gt;`).
- `Render_HtmlOutput_RawTextHelperBypassesEscape_WhenEnabled`.
- `Render_HtmlOutput_RawTextHelperRejected_WhenDisabled` (option flag honored).
- `Render_DenyListedDirective_RejectsAtLexer` (`@inject Foo Bar` → `SandboxViolation`).
- `Render_DenyListedReference_RejectsAtCompile` (template tries to use `System.IO.File.ReadAllText` → compile error → `SandboxViolation`).
- `Render_LongRunningTemplate_TimesOut` (template loop within budget delta → `RenderTimeout`).
- `Render_LargeOutput_SoftMemoryWarning_DoesNotFail` (>50MB output → result is fine, warning present).
- `Render_AssemblyLoadContextCollectible` (50 successive renders do not leak monotonically — assert allocated bytes growth bounded; this is a regression test for AssemblyLoadContext lifecycle).

`tests/ClauseExpressionParserTests.cs`:
- `Parse_SimpleEquality_True`.
- `Parse_SimpleInequality_False`.
- `Parse_LogicalAnd_BothTrue`.
- `Parse_LogicalOr_ShortCircuits`.
- `Parse_NotNot_DoubleNegation`.
- `Parse_ParenthesizedNesting`.
- `Parse_StringComparison_CaseSensitive`.
- `Parse_NumericComparison_LtGt`.
- `Parse_TypeMismatch_EvaluatesFalseWithWarning`.
- `Parse_MalformedExpression_RaisesParserError_AndClauseSkipped`.
- `Parse_FuzzInputBattery_NeverThrowsUnhandled` (30 hand-curated malformed inputs; no `Exception` may escape; parser must always return either `Result.OK(bool)` or `Result.Error(msg)`).

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests pass unchanged.
- New tests pass.
- A sample template (`tests/Fixtures/SampleLeaseTemplate.cshtml`) renders against a 12-variable input set in < 200ms locally.
- Security council subagent dispatched; findings addressed before merge. Findings logged in the PR description under `## Council Findings`.

#### Do NOT in this PR

- Do NOT introduce a SQLite-backed render cache (would be a perf win; out of scope for v1).
- Do NOT introduce AOT compilation paths (out of scope).
- Do NOT introduce the `IPdfRenderer` real implementation — PR 4 stubs it; the real impl lands in `blocks-reports-pdf`.
- Do NOT register concrete reference resolvers for `Property` / `Employee` / `Party` — those land in the consumer clusters.
- Do NOT enable `@code` blocks or any privileged Razor directive even "just for tests" — security council will reject; tests must use template-only authoring.

---

### PR 3 — `IContractInstanceService` (lifecycle: render → send → record-signing → void) + `Docs.ContractRendered` event emission

**Estimated effort:** ~2–3h
**Scope:** thin lifecycle service over `ITemplateRenderingService` (PR 2) + `IDocumentCommandService` (docs-core); emits `Docs.ContractRendered` on first render; status-transition validation
**Standing pattern:** NONE (novel cross-cluster wiring + event emission; council not mandatory)
**Commit subject:** `feat(blocks-docs-templates): PR 3 — IContractInstanceService lifecycle + Docs.ContractRendered emission per Stage 02 §5.2`
**Depends on:** PR 2 merged
**Council:** SKIP (mirrors `pattern-003` light spot-check shape — event-emission verification is implicit via the test that asserts envelope; no full pattern entry yet).
**Branch:** `cob/blocks-docs-templates-instance-service`

#### Service contract — `IContractInstanceService`

- `Task<RenderContractResult> RenderAsync(RenderContractRequest request, CancellationToken ct = default)` — the canonical synchronous-path render (orchestration around `ITemplateRenderingService.RenderAsync`):
  - Loads the `ContractTemplate` + its current `DocumentVersion` (the template body) + field list + clause list via the repositories.
  - Calls `ITemplateRenderingService.RenderAsync` per PR 2.
  - On success, creates a `TemplateRenderJob` row (status `complete`) + creates a new output `Document` (type `contract-instance`) + initial `DocumentVersion` via `IDocumentCommandService.CreateAsync` (from `blocks-docs-core`) + creates a `ContractInstance` row (status `rendered`) linking template, version, job, output document, party FKs.
  - Emits `Docs.ContractRendered` envelope.
  - Returns `RenderContractResult` (record: `ContractInstance? Instance, RenderContractError Error, string? Detail`).
- `Task<RecordSentResult> RecordSentAsync(ContractInstanceId id, CancellationToken ct = default)` — flips `rendered → sent`; idempotent on already-sent.
- `Task<RecordSigningResult> RecordSigningAsync(ContractInstanceId id, RecordSigningRequest request, CancellationToken ct = default)` — flips `sent → partiallySigned | fullySigned` based on `request.AllPartiesSigned`; if `fullySigned`, sets `SignedAt` (immutability trigger per CRDT conventions §6).
- `Task<VoidContractResult> VoidAsync(ContractInstanceId id, string reason, PartyId actor, CancellationToken ct = default)` — voids from any non-voided state; writes `VoidReason`; emits no new event (voiding is observed via the next read; signing cluster's `voided` audit handles that side).

#### `RenderContractError` enum

- `None`
- `TemplateNotFound`
- `TemplateNotActive` (template `IsActive == false`)
- `TemplateVersionMissing` (no `currentVersionId` on the Document)
- `FieldValidationFailed` (forwarded from PR 2; carries the field-level detail in `Detail`)
- `ClauseExpressionFailed`
- `RenderTimeout`
- `SandboxViolation`
- `OutputDocumentCreationFailed` (wraps `IDocumentCommandService.CreateAsync` errors)
- `RepositoryWriteFailed`

#### `Docs.ContractRendered` event payload

Per `cross-cluster-event-bus-design.md` §3.4 (new entry):

| Field | Type | Source |
|---|---|---|
| `contractInstanceId` | `ContractInstanceId` | this PR |
| `templateId` | `ContractTemplateId` | this PR |
| `templateVersionId` | `DocumentVersionId` | this PR |
| `outputDocumentId` | `DocumentId` | this PR |
| `party1Id` | `PartyId?` | this PR |
| `party2Id` | `PartyId?` | this PR |
| `renderedAtUtc` | `Instant` | clock |
| `tenantId` | `string` | envelope |

**Idempotency key:** `contract-rendered:{contractInstanceId}`.

**Consumers** (per §7.2): `blocks-work-*` (creates a `Contract` row linking `Contract.contractInstanceId` FK; flips `Contract.status` from `draftingTemplate` to `pendingSignature`), `blocks-docs-signing` (auto-creates a `SigningWorkflow` when the template's `DefaultSigningWorkflowId` is set).

#### Event publisher stub

The package ships a local `IContractInstanceEventPublisher` interface + `InMemoryContractInstanceEventPublisher` (logs to `ILogger<>` + an in-memory queue for test inspection) until the canonical event-bus dispatcher's package home is decided. Pattern matches the `IInvoiceEventPublisher` stub from `blocks-financial-ar`. When the foundation event-bus package lands, the local interface relocates + the stub deletes.

#### DI registration (extends PR 2's `AddBlocksDocsTemplates`)

- `services.AddSingleton<IContractInstanceService, ContractInstanceService>()`.
- `services.AddSingleton<IContractInstanceEventPublisher, InMemoryContractInstanceEventPublisher>()`.

#### Tests (PR 3) — ~14–18 tests

`tests/ContractInstanceServiceTests.cs`:
- `Render_HappyPath_CreatesInstance_AndOutputDocument_AndEmitsEvent`.
- `Render_TemplateNotFound_ReturnsError`.
- `Render_TemplateNotActive_ReturnsError`.
- `Render_FieldValidationFailed_PropagatesDetail`.
- `Render_RenderTimeout_PropagatesError_AndNoInstanceCreated`.
- `Render_SandboxViolation_PropagatesError_AndNoInstanceCreated`.
- `Render_TwoTenantsCanRenderSameTemplate_NoCrosstalk` (tenant-isolation regression).
- `Render_IdempotencyKey_StableAcrossRenders_OfSameInputs` (same template + same variables hash → same idempotency key; multiple emissions deduplicated by downstream consumer; the publisher emits each time but the key collides).
- `RecordSent_FromRendered_FlipsStatus`.
- `RecordSent_AlreadySent_IsIdempotent`.
- `RecordSigning_PartialThenFull_SetsSignedAt_OnlyOnFull`.
- `RecordSigning_FullySigned_ImmutabilityEnforced_PostSign` (subsequent mutation attempts on `Party1Id` reject).
- `Void_FromAnyNonVoidedState_Succeeds_AndWritesReason`.
- `Void_AlreadyVoided_IsIdempotent`.
- `ContractRenderedEvent_EnvelopeFields_AllPopulated`.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass unchanged.
- New tests pass.
- A consumer-side smoke test: `AddBlocksDocsCore() + AddBlocksDocsTemplates()` in a test host, render the fixture lease template → assert one `ContractInstance` exists + one `Document` of type `contract-instance` exists + one `Docs.ContractRendered` event captured in the in-memory publisher queue.

#### Do NOT in this PR

- Do NOT implement the async retry-orchestrated path — that's PR 4.
- Do NOT route to `IPdfRenderer` — PR 4 wires the orchestrator + PDF path.
- Do NOT create `SigningWorkflow` rows — `blocks-docs-signing` owns that surface.
- Do NOT modify any `blocks-work-*` types — the consumer-side handler lands when `blocks-work-*` ships its subscriber.

---

### PR 4 — `IRenderJobOrchestrator` + retry + PDF stub + `Docs.TemplateRenderJobFailed`

**Estimated effort:** ~2–3h
**Scope:** async render-job submission queue; background pull-loop; retry-with-backoff; outputs PDF (via stub `IPdfRenderer`) or markdown/html directly per request; emits `Docs.TemplateRenderJobFailed` on retry exhaustion
**Standing pattern:** NONE (novel orchestration surface; council not mandatory but architect spot-check recommended on the background-worker lifecycle)
**Commit subject:** `feat(blocks-docs-templates): PR 4 — IRenderJobOrchestrator async render with retry + IPdfRenderer stub`
**Depends on:** PR 3 merged
**Council:** SKIP (architect spot-check optional — orchestrator follows the standard `IHostedService` pattern from `kernel-runtime`).
**Branch:** `cob/blocks-docs-templates-orchestrator`

#### Service contracts

- `IRenderJobOrchestrator`:
  - `Task<SubmitRenderJobResult> SubmitAsync(SubmitRenderJobRequest request, CancellationToken ct = default)` — creates a `TemplateRenderJob` (status `queued`), enqueues for background processing; returns the job id immediately.
  - `Task<TemplateRenderJob?> GetStatusAsync(TemplateRenderJobId jobId, CancellationToken ct = default)` — caller poll surface.
  - `Task<TemplateRenderJob?> CancelAsync(TemplateRenderJobId jobId, CancellationToken ct = default)` — best-effort cancel (only `queued` jobs cancel cleanly; `rendering` jobs are signaled but may complete).
- `IPdfRenderer` (sub-contract — real impl in `blocks-reports-pdf`):
  - `Task<RenderPdfResult> RenderPdfAsync(RenderPdfRequest request, CancellationToken ct = default)` — takes markdown or HTML + style options; returns bytes.
- `NoOpPdfRenderer` (stub):
  - Returns `RenderPdfResult` with `Error: PdfRendererUnavailable, Detail: "Configured renderer is the v1 no-op stub. Wire blocks-reports-pdf for real PDF rendering."` and the request's input body verbatim as `Bytes = utf8(request.Body)` (so the orchestrator can still create the output document — it will be a stub PDF placeholder for testing).

#### Background worker

`TemplateRenderJobWorker : BackgroundService` (registered via `IHostedService`):

1. Loops with a small (default 250ms) delay.
2. Pulls `queued` jobs from `ITemplateRenderJobRepository.QueryByStatusAsync(queued, limit: MaxConcurrentRenders)`.
3. Per job: transitions to `rendering` (CAS via repository `UpsertAsync` with version-vector check — if another worker beat us, skip), invokes `IContractInstanceService.RenderAsync` (synchronous path), routes through `IPdfRenderer` if `OutputFormat == pdf`, creates the output Document via `IDocumentCommandService.CreateAsync`, transitions the job to `complete` (with `OutputDocumentId`) or `failed` (with `ErrorMessage`).
4. On `failed`, increments `RetryCount`; if `< MaxRenderJobRetries`, transitions back to `queued` with an exponential-backoff `NextAttemptAtUtc` (1s / 4s / 16s); if `>= MaxRenderJobRetries`, emits `Docs.TemplateRenderJobFailed` and leaves the job in `failed` terminally.
5. Respects `IHostApplicationLifetime.ApplicationStopping` for clean shutdown (in-flight jobs finish or are marked back to `queued`).

#### `Docs.TemplateRenderJobFailed` event payload

| Field | Type |
|---|---|
| `jobId` | `TemplateRenderJobId` |
| `templateId` | `ContractTemplateId` |
| `templateVersionId` | `DocumentVersionId` |
| `lastErrorKind` | `string` (the `RenderTemplateError` or `RenderContractError` enum name) |
| `lastErrorDetail` | `string?` |
| `retryCount` | `int` |
| `failedAtUtc` | `Instant` |
| `tenantId` | `string` (envelope) |

**Idempotency key:** `template-render-failed:{jobId}` (terminal failure — never re-emitted).

**Consumers:** internal alert / dashboard surface (`apps/docs/blocks/docs-templates/overview.md` documents the recommended notification wiring).

#### DI registration (extends PR 3's `AddBlocksDocsTemplates`)

- `services.AddSingleton<IRenderJobOrchestrator, RenderJobOrchestrator>()`.
- `services.AddSingleton<IPdfRenderer, NoOpPdfRenderer>()` (uses `TryAddSingleton` so consumers in `blocks-reports-pdf` can `services.Replace<IPdfRenderer, ReactPdfRenderer>()` later).
- `services.AddHostedService<TemplateRenderJobWorker>()`.
- `BlocksDocsTemplatesOptions` extended with: `MaxConcurrentRenders: int = 4`, `MaxRenderJobRetries: int = 3`, `WorkerLoopDelayMs: int = 250`, `RetryBackoffSecondsBase: int = 1`, `RetryBackoffMultiplier: int = 4`.

#### Tests (PR 4) — ~14–18 tests

`tests/RenderJobOrchestratorTests.cs`:
- `Submit_CreatesQueuedJob_AndReturnsId`.
- `Submit_StatusPollableViaGetStatusAsync`.
- `Worker_PullsQueuedJob_FlipsToRendering_ThenComplete`.
- `Worker_OnRenderFailure_IncrementsRetryCount_AndReQueues`.
- `Worker_OnRetryExhausted_TransitionsToFailedTerminal_AndEmitsEvent`.
- `Worker_CancelOnQueued_TransitionsToFailed_WithCancelReason`.
- `Worker_CancelOnRendering_IsBestEffort_RaceAcceptable`.
- `Worker_ConcurrentJobs_RespectMaxConcurrentRenders`.
- `Worker_ApplicationStopping_DrainsInFlight_OrMarksBackToQueued`.
- `Submit_PdfOutput_RoutesThroughNoOpPdfRenderer_ResultsInDocumentWithBodyBytes`.
- `Submit_HtmlOutput_DoesNotInvokePdfRenderer_OutputDocumentBodyIsHtml`.
- `Submit_MarkdownOutput_DoesNotInvokePdfRenderer_OutputDocumentBodyIsMarkdown`.
- `Worker_RetryBackoff_FollowsExponentialSchedule` (1s / 4s / 16s).
- `Worker_TenantIsolation_DoesNotCrosstalk` (two tenants' jobs interleave safely).
- `TemplateRenderJobFailedEvent_EnvelopeFields_AllPopulated`.

#### Verification

- `dotnet build` succeeds.
- All prior PR tests pass.
- New tests pass.
- A consumer-side smoke test: submit 10 render jobs against 2 templates, all complete within 5s; orchestrator status query shows 10 `complete` jobs + 0 `failed`.

#### Do NOT in this PR

- Do NOT implement the real PDF renderer — that's `blocks-reports-pdf` scope.
- Do NOT introduce SQLite persistence for the job queue — in-memory is the v1.
- Do NOT add scheduler/cron features (e.g. "render on a recurring schedule"). Out of scope.
- Do NOT introduce distributed locking — Anchor is single-node per ADR 0088; the in-process worker is sufficient.

---

### PR 5 — ERPNext template importer (`IErpnextContractTemplateImporter` + `ErpnextContractTemplateSource` + idempotent upsert)

**Estimated effort:** ~1.5–2h
**Scope:** Pass-N migration importer; consumes ERPNext "Contract" doctype + "Email Template" + Frappe "Print Format" source records; produces `ContractTemplate` + initial `ContractTemplateField` set inferred from `{{...}}` placeholders in the source body; idempotent on `(source, externalRefId)` lookup
**Standing pattern:** `pattern-002` once it reaches ratification; council SKIP for now
**Commit subject:** `feat(blocks-docs-templates): PR 5 — IErpnextContractTemplateImporter — ERPNext Contract + Print Format upsert`
**Depends on:** PR 1 + PR 2 merged (does NOT depend on PR 3 / PR 4 — importer only writes template entities, not render jobs)
**Council:** SKIP (pattern shape mirrors `pattern-002`).
**Branch:** `cob/blocks-docs-templates-erpnext-importer`

#### `IErpnextContractTemplateImporter` contract

Single entrypoint:

`Task<ImportOutcome<ContractTemplate>> UpsertFromErpnextAsync(ErpnextContractTemplateSource source, string tenantId, CancellationToken cancellationToken = default)`

`ErpnextContractTemplateSource` shape (record fields, no code):

| Field | Type | Mapped from ERPNext field |
|---|---|---|
| `Name` | `string` | `Contract.name` (or `Email Template.name` / `Print Format.name`) — stable id |
| `Modified` | `string` | ERPNext `modified` ISO timestamp — version-key for change detection |
| `Title` | `string` | ERPNext `contract_name` / `subject` / `name` |
| `SourceKind` | `string` | enum: `"contract"` / `"email_template"` / `"print_format"` |
| `Category` | `string?` | ERPNext `party_type` mapped: `Customer → custom`, `Supplier → vendor`, `Lead → custom`, else `custom` |
| `BodyFormat` | `string` | `"html"` for email templates + print formats; `"markdown"` for contracts (ERPNext contracts are stored as HTML but downconverted on import) |
| `Body` | `string` | the actual template body (already with `{{variable}}` placeholders in Frappe's jinja-ish syntax) |
| `DocStatus` | `int` | ERPNext doc-status (0/1/2); only `1` (Submitted) imports as `IsActive: true` |

#### Import algorithm

1. **Idempotency check:** `templateRepo.GetByExternalRefAsync("erpnext", source.Name)` — if exists and `existing.Modified == source.Modified`, return `Skipped`. If exists with newer modified, continue with update path.
2. **Body conversion:** if `source.BodyFormat == "html"` and the consumer requested markdown, run a minimal HTML-to-markdown conversion (use a small whitelist of HTML tags: `p`, `br`, `strong`, `em`, `h1..h6`, `ul`, `ol`, `li`, `a`); strip everything else. For v1, **default to keeping the source's format verbatim** (markdown stays markdown; HTML stays HTML); the consumer can request downconversion via an option in a follow-on.
3. **Variable inference:** regex `\\{\\{\\s*([a-zA-Z_][a-zA-Z0-9_]*)\\s*\\}\\}` against the body; extract unique identifiers; for each, create a `ContractTemplateField` with `FieldKind: text`, `Required: false`, no validation. The consumer can edit these post-import to set proper field kinds.
4. **Clause inference:** for v1, **no clauses inferred**. ERPNext contracts don't have a clause-level decomposition that maps cleanly. Document this limitation; clause modeling is post-import editorial.
5. **Document creation:** call `IDocumentCommandService.CreateAsync` (from `blocks-docs-core`) to create the `Document` (type `contract-template`) + initial `DocumentVersion` carrying the body.
6. **ContractTemplate upsert:** `templateRepo.UpsertAsync` with the new/updated row. Set `ExternalRef = source.Name`, `IsActive = (source.DocStatus == 1)`.
7. **Field upsert:** for each inferred variable, `fieldRepo.UpsertAsync`. Soft-merge against existing fields (if a field with the same `Name` already exists for the template, keep its existing `FieldKind` / `Required` / `ValidationRegex` — only update `SortOrder` to match the latest body's order).
8. Return `ImportOutcome<ContractTemplate>.Inserted(template)` or `.Updated(template)` or `.Skipped`.

#### Stub `IDocumentCommandService` dependency

If the consumer host doesn't register `blocks-docs-core`, the importer fails-fast at DI resolution. Document the prerequisite clearly in the package README + the importer's XML doc.

#### Tests (PR 5) — ~10–12 tests

`tests/ErpnextContractTemplateImporterTests.cs`:
- `Upsert_NewContractSubmitted_InsertsAsActiveTemplate`.
- `Upsert_NewContractDraft_InsertsAsInactiveTemplate`.
- `Upsert_DuplicateSameModified_ReturnsSkipped`.
- `Upsert_DuplicateNewerModified_ReturnsUpdated`.
- `Upsert_VariableInference_ExtractsUniqueIdentifiers`.
- `Upsert_VariableInference_HandlesWhitespaceInBraces` (`{{ foo }}` and `{{foo}}` both extract `foo`).
- `Upsert_VariableInference_IgnoresMalformedPlaceholders` (`{{ foo bar }}` → no field).
- `Upsert_FieldUpsert_PreservesExistingFieldKind_OnReimport` (user manually changed `someDate` from `text` to `date`; reimport keeps `date`).
- `Upsert_PrintFormatSource_ImportsAsHtmlTemplate`.
- `Upsert_EmailTemplateSource_ImportsAsHtmlTemplate`.
- `Upsert_UnknownSourceKind_ReturnsSkipped_WithDetail`.
- `Upsert_TenantIsolation_NoCrosstalk`.

#### Verification

- `dotnet build` succeeds.
- All prior PR tests pass.
- New tests pass.
- A consumer-side smoke test: feed a 5-template ERPNext export (2 contracts, 2 print formats, 1 email template) → 5 templates land + their fields are inferred + a re-run is idempotent.

#### Do NOT in this PR

- Do NOT introduce a UI for post-import field-kind editing — that's apps-layer.
- Do NOT couple to `blocks-financial-tax` or any other cluster for variable validation — variables are pure strings at this layer.
- Do NOT auto-render any imported template — render is the consumer's job.
- Do NOT introduce the importer orchestrator (multi-pass driver) — that's `tooling-anchor-import` scope.

---

### PR 6 — DI umbrella + apps/docs page + NOTICE.md + README.md + ledger flip

**Estimated effort:** ~1h
**Scope:** finalize `AddBlocksDocsTemplates()` umbrella (composed of all PR 1–5 registrations); add `apps/docs/blocks/docs-templates/overview.md`; finalize `NOTICE.md` + `README.md`; flip the ledger row for this workstream
**Standing patterns:** `pattern-005` (umbrella DI) + `pattern-006` (apps/docs page) + `pattern-007` (ledger flip)
**Commit subject:** `chore(blocks-docs-templates): PR 6 — DI umbrella + apps/docs overview + ledger flip`
**Depends on:** PRs 1–5 merged
**Council:** SKIP (all three patterns SKIP per the catalog).
**Branch:** `cob/blocks-docs-templates-umbrella-and-docs`

#### DI umbrella consolidation

The cumulative `AddBlocksDocsTemplates(this IServiceCollection services, Action<BlocksDocsTemplatesOptions>? configure = null)` extension after PRs 1–5 registers:

- 5 in-memory repositories (Scoped) — from PR 1
- `ITemplateRenderingService → RazorTemplateRenderingService` (Singleton) — from PR 2
- `IReferenceResolver → AlwaysFoundReferenceResolver` (Transient — registered via `TryAddTransient` so consumer clusters can override) — from PR 2
- `IContractInstanceService → ContractInstanceService` (Singleton) — from PR 3
- `IContractInstanceEventPublisher → InMemoryContractInstanceEventPublisher` (Singleton) — from PR 3
- `IRenderJobOrchestrator → RenderJobOrchestrator` (Singleton) — from PR 4
- `IPdfRenderer → NoOpPdfRenderer` (Singleton via `TryAddSingleton` — consumer override surface) — from PR 4
- `IHostedService → TemplateRenderJobWorker` (Singleton) — from PR 4
- `IErpnextContractTemplateImporter → ErpnextContractTemplateImporter` (Singleton) — from PR 5
- `BlocksDocsTemplatesOptions` binding (carries every option flag added in PRs 1–5)

PR 6 reviews the cumulative shape, factors any leaked one-off registrations into a single coherent block, ensures every `TryAdd*` vs `Add*` choice is intentional, adds 3–5 tests under `tests/BlocksDocsTemplatesServiceCollectionExtensionsTests.cs` validating registration order / lifetimes / non-overlap.

#### `apps/docs/blocks/docs-templates/overview.md` (new file — `pattern-006`)

Per Stage 02 §1 documentation cluster + the `apps/docs/blocks/<cluster>/overview.md` convention from `blocks-financial-ar` PR 6. Structure (~150–200 lines markdown):

- **Header:** package name + Stage 02 cite + ADR 0088 cite.
- **Overview:** 3 paragraphs — what the cluster does, what it doesn't, how it fits into the docs-cluster decomposition.
- **Public surface:** table of public types (entities + services + events).
- **Variable + clause model:** explains the variable interview model + the v1 clause-condition grammar (the grammar from PR 2 is the canonical reference; the docs page summarizes).
- **Render pipeline:** diagram in mermaid — `ContractTemplate + variables → ITemplateRenderingService → Document(contract-instance) + ContractInstance → Docs.ContractRendered`.
- **Engine choice:** documents Razor + the sandbox boundary (high-level summary; council findings linked from PR 2).
- **ERPNext migration:** documents the importer + the field-inference behavior + the manual post-import editing step.
- **Quickstart:** ~15 lines of registration + render example (descriptive — not executable).
- **Related:**
  - `blocks-docs-core` (predecessor — Document substrate)
  - `blocks-docs-signing` (follow-on — consumes ContractInstance)
  - `blocks-docs-wiki` (parallel cluster sibling)
  - `blocks-docs-dam` (parallel cluster sibling)
  - `blocks-work-*` (cross-cluster consumer — Contract row linked via `WorkContractId`)
  - `blocks-reports-pdf` (follow-on — provides real `IPdfRenderer`)
  - `blocks-leases` (downstream consumer — lease document generation)

#### `NOTICE.md` (new file — Apache 2.0 attribution required)

```markdown
# NOTICE — Sunfish.Blocks.DocsTemplates

This package's parametric contract-template + field-interview model
(ContractTemplate, ContractTemplateField, ContractTemplateClause, the
variable-validation + clause-condition surfaces) derives in shape from:

- **Apache OFBiz** `accounting/contract` entities (Apache 2.0)
  <https://ofbiz.apache.org/> — header/clause decomposition; party-role
  declaration; clause-level metadata.

- **DocAssemble** (MIT)
  <https://docassemble.org/> — interview-style variable + field model;
  render-job + outputDocument semantics. (MIT is permissive — included
  here for transparency though not required by license.)

Both sources were studied and their entity shapes inspired the design.
The Sunfish implementation is original code, distributed under the MIT
License. The OFBiz entity-shape pattern is reproduced with attribution
per Apache 2.0 §4(c) of the OFBiz License.

OFBiz version studied: v18.12.x (as of 2026-05-16).
DocAssemble version studied: v1.x (as of 2026-05-16).
```

#### `README.md` (new file — sketch)

3–5 paragraphs:
- What the package is (1 paragraph).
- What it depends on (`blocks-docs-core` + `blocks-docs` + `blocks-people-foundation`; Razor packages from Microsoft).
- How to wire it (`AddBlocksDocsTemplates()` in DI; sample options block).
- Where the v1 stubs are (NoOpPdfRenderer, AlwaysFoundReferenceResolver, InMemoryContractInstanceEventPublisher) and how to replace them in production.
- Where to read more (link to apps/docs page + ADR 0088 + Stage 02 §3.3).

#### Ledger flip (`pattern-007`)

Edit the workstream source file (NOT the rendered ledger — per `feedback_never_add_workstream_rows_directly_to_ledger`): `icm/_state/workstreams/W##-blocks-docs-templates.md` (new file if not yet authored — XO drops it alongside this hand-off if missing). Flip `State: ready-to-build` → `State: built`. Re-run `tools/icm/render-ledger.py` to refresh `active-workstreams.md`.

#### Tests (PR 6)

`tests/BlocksDocsTemplatesServiceCollectionExtensionsTests.cs`:
- `AddBlocksDocsTemplates_RegistersAllRepositories_AsScoped`.
- `AddBlocksDocsTemplates_RegistersRenderingService_AsSingleton`.
- `AddBlocksDocsTemplates_RegistersPdfRendererStub_OverrideableViaTryAdd`.
- `AddBlocksDocsTemplates_RegistersHostedWorker`.
- `AddBlocksDocsTemplates_OptionsConfigurationHonored`.

#### Verification

- `dotnet build` succeeds across the package + every downstream consumer.
- All PR 1–5 tests pass.
- New PR 6 tests pass.
- `apps/docs` site builds (smoke: the new markdown file renders).
- Ledger row for `blocks-docs-templates` shows `built` after `render-ledger.py` re-runs.

#### Do NOT in this PR

- Do NOT introduce new functional surface — this PR is composition + docs + ledger only.
- Do NOT modify any other cluster's docs page.

---

## Cross-cluster integration

### Inputs (this hand-off consumes)

| Source cluster | Surface | Usage |
|---|---|---|
| `blocks-docs-core` | `Document`, `DocumentVersion`, `DocumentId`, `DocumentVersionId`, `IDocumentCommandService.CreateAsync`, `IDocumentRepository.GetByIdAsync` | Template bodies live in DocumentVersion; rendered ContractInstance outputs are Documents of type `contract-instance`. |
| `blocks-docs` | `StorageRef`, `IBlobStore` (indirectly via docs-core) | Output Documents carry storage refs when binary (PDF). |
| `blocks-people-foundation` | `PartyId` | All actor + counterparty FKs typed as `PartyId`. |
| `foundation-events` | `ISunfishDomainEvent`, `SunfishDomainEvent<TPayload>` envelope | Event emission shape. |

### Outputs (this hand-off produces)

| Output | Consumed by |
|---|---|
| `ContractTemplate` + fields + clauses | All template editors + the future `blocks-leases-template-driven-doc-generation` hand-off |
| `ContractInstance` (Document of type `contract-instance`) | `blocks-docs-signing` (signing workflow target); `blocks-work-*` (Contract row link) |
| `Docs.ContractRendered` event | `blocks-work-*` (creates Contract row + flips status to `pendingSignature`); `blocks-docs-signing` (auto-creates SigningWorkflow when template carries `DefaultSigningWorkflowId`) |
| `Docs.TemplateRenderJobFailed` event | Internal alert / dashboard surface |
| `IPdfRenderer` interface | `blocks-reports-pdf` (provides real implementation later) |

### Late-binding boundaries (untyped FKs until consumer cluster ships)

| Field | Owner cluster (when it ships) | Current type |
|---|---|---|
| `ContractTemplate.DefaultSigningWorkflowId` | `blocks-docs-signing` | `string?` |
| `ContractInstance.SigningWorkflowId` | `blocks-docs-signing` | `string?` |
| `ContractInstance.WorkContractId` | `blocks-work-*` | `string?` |

When each consumer cluster ships its strong-typed ID, a follow-on PR migrates the field type via a single `using` directive update + property type change. No public-API breakage expected (string-castable everywhere v1 ships).

### Reference-resolver registration boundary

`IReferenceResolver` is the cross-cluster seam for `FieldKind == reference` field validation. v1 ships `AlwaysFoundReferenceResolver` (permissive stub). Consumer clusters register concrete resolvers:

- `blocks-property-*` → `PropertyReferenceResolver` (handles `referenceEntity = "Property"`)
- `blocks-people-*` (when shipped) → `EmployeeReferenceResolver`, `PartyReferenceResolver`
- `blocks-leases` → `LeaseReferenceResolver`

Registration order: consumer-side concrete resolver must precede `AddBlocksDocsTemplates()` in DI for `TryAddTransient<IReferenceResolver, …>` to honor the consumer's registration. Document this in the apps/docs page.

---

## Pre-merge council requirements

### PR 2 (Template rendering engine — Razor + sandbox) — **MANDATORY**

**Security-engineering subagent — REQUIRED** (Opus + xhigh, per `feedback_council_reviews_use_best_model_xhigh`).

**Council brief:**

> Sunfish blocks-docs-templates PR 2 introduces a server-side template
> rendering engine (Razor) that executes user-supplied template body
> against user-supplied variables. The body is C#-compiled and run in
> a sandboxed AssemblyLoadContext with a deny-listed reference set
> (System.IO, System.Net, System.Reflection.Emit, etc. excluded; only
> System.Runtime + System.Linq + System.Collections + the Sunfish model
> DTO type are reachable). Razor directives `@inject`, `@inherits`,
> `@functions`, `@code`, `@using`, `@implements`, `@page`, `@layout`
> are rejected at the lexer. A 5s execution-time budget enforces via
> CancellationToken. A separate hand-rolled clause-condition parser
> (~150 lines) evaluates simple boolean expressions over the variable
> dictionary; no reflection / no script-engine; the parser must always
> return a deterministic result (no unhandled exceptions).
>
> **Review focus:**
> 1. Is the deny-listed reference set sufficient? Are there reachable
>    types in `System.Runtime` that allow file/network/process escape?
> 2. Is the deny-listed-directive set complete? Are there Razor
>    directives or `@(...)` constructs that could load arbitrary types
>    or invoke arbitrary methods despite the curated reference set?
> 3. Does the AssemblyLoadContext lifecycle actually collect on
>    repeated renders (memory-bound regression risk)?
> 4. Is the regex validation (ReDoS) mitigation sufficient
>    (`MatchTimeout = 250ms` + `RegexOptions.NonBacktracking`)?
> 5. Is the HTML-output XSS-escape behavior correct (default-escape;
>    explicit `@RawText` helper as the only bypass; option flag to
>    disable the bypass entirely)?
> 6. Does the clause-condition parser have any path to unhandled
>    exceptions on malformed input (the parser is supposed to always
>    return Result.OK/Error)?
>
> Council should return BLOCKING, MAJOR, MINOR, or APPROVED. BLOCKING
> findings halt the PR + escalate to XO. MAJOR findings require fix
> before merge. MINOR are addressed before close or filed as follow-up
> intakes.

**Council protocol:**
- Run BEFORE auto-merge per `feedback_council_before_automerge`.
- PR opens as `--draft` per `feedback_pr_automerge_before_amendment_landed`.
- Wait for council disposition before flipping to ready.
- XO spot-check council findings per `feedback_council_can_miss_spot_check_negative_existence` (verify the council actually read the sandbox configuration code; verify negative claims like "no @inject reachable" by reading the lexer config).

**`.NET architect` spot-check (optional, recommended):**
> Confirm the Razor engine choice + the AssemblyLoadContext per-render
> lifecycle is sound; confirm `BackgroundService` shutdown semantics
> in PR 4 are correct; confirm `TryAddSingleton` vs `AddSingleton`
> choices are intentional.

### PR 1, 3, 4, 5, 6 — Council SKIP

PR 1 matches `pattern-001`. PR 3 matches the implicit "cluster service over substrate" pattern (no formal pattern entry yet; similar to `pattern-003` for posting services — light spot-check would be nice but not required at this hand-off). PR 4 introduces a hosted worker; the pattern shape is standard `IHostedService` + `IBackgroundService`. PR 5 matches `pattern-002` (importer). PR 6 matches `pattern-005` + `pattern-006` + `pattern-007`.

Standard COB self-audit per `feedback_council_can_miss_spot_check_negative_existence` applies to all PRs (XO will spot-check three-direction-miss surface: negative existence, positive existence, structural citation).

---

## Idempotency-key catalog

Per `cross-cluster-event-bus-design.md` §1, every cross-cluster event carries an idempotency key. This hand-off's events:

| Event | Idempotency key |
|---|---|
| `Docs.ContractRendered` | `contract-rendered:{contractInstanceId}` |
| `Docs.TemplateRenderJobFailed` | `template-render-failed:{jobId}` |

Repository-write idempotency:
| Operation | Idempotency key |
|---|---|
| `IContractTemplateRepository.UpsertAsync` (importer path) | `(externalRef.source, externalRef.id) = ("erpnext", source.Name)` |
| `ITemplateRenderJobRepository.UpsertAsync` (transition path) | `(jobId, fromStatus, toStatus)` via version-vector CAS |
| `IContractInstanceRepository.UpsertAsync` (status transition path) | `(contractInstanceId, fromStatus, toStatus)` via version-vector CAS |
| `IContractInstanceService.RenderAsync` (caller-supplied) | optional `request.IdempotencyKey: string?` — when provided, the service short-circuits on a duplicate-key hit within the last 24h (in-memory cache; non-persistent v1) and returns the prior `ContractInstance` without re-rendering |

---

## Dependencies + sequence

### Hard predecessors (must be on main)

1. `blocks-docs-core` PRs 1 + 2 merged — provides `Document`, `DocumentVersion`, `DocumentId`, `DocumentVersionId`, `IDocumentCommandService.CreateAsync`, `IDocumentRepository`.
2. `blocks-docs` substrate merged — provides `StorageRef` + `IBlobStore`. (If absent, fall back to `string? StorageRef` per docs-core hand-off Halt §2.)
3. `blocks-people-foundation` merged — provides `PartyId`.

### Soft predecessors (recommended on main; safely deferrable)

1. `foundation-events` canonical event-bus dispatcher — if missing, ship local `IContractInstanceEventPublisher` stub per the `blocks-financial-ar` pattern.
2. `blocks-reports-pdf` — if missing (and it is at this hand-off's authoring), ship `NoOpPdfRenderer` stub per PR 4.

### Successors (this hand-off unblocks)

1. `blocks-docs-signing` — consumes `ContractInstance` + `signingWorkflowId` reverse-FK + `Docs.ContractRendered` event for auto-workflow creation.
2. `blocks-work-*` — consumes `Docs.ContractRendered` to create `Contract` rows.
3. `blocks-leases-template-driven-doc-generation` (future hand-off) — consumes `IContractInstanceService.RenderAsync` for lease document generation; registers `LeaseReferenceResolver`.
4. `blocks-reports-pdf` — implements real `IPdfRenderer`.
5. `tooling-anchor-import` orchestrator — consumes `IErpnextContractTemplateImporter` in its multi-pass migration driver.

### PR sequence within this hand-off

```
PR 1 (scaffold)
  ▼
PR 2 (rendering engine) — SECURITY COUNCIL MANDATORY; PR opens --draft
  ▼
PR 3 (IContractInstanceService) ──────────┐
  ▼                                       │
PR 4 (orchestrator + worker + PDF stub)   │
                                          │
PR 5 (ERPNext importer)  ◄── parallel after PR 2 ──┘
  ▼
PR 6 (DI umbrella + apps/docs + ledger flip)
```

PR 5 parallelizes with PR 3 + PR 4 once PR 2 lands. PR 6 sequences last.

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `accounting/contract` entity shape (Apache 2.0) — `ContractTemplate` + `ContractTemplateClause` header/clause decomposition + `requiredSignerRoles` declaration. Attribution via `NOTICE.md` + per Stage 02 §8.
- **DocAssemble** (MIT) — variable interview model + field declarations + render-job lifecycle. Attribution via `NOTICE.md` (MIT does not require attribution but we include it for transparency).
- **Microsoft.AspNetCore.Razor.Language** (Apache 2.0) — engine dependency, no source borrowed.
- **Microsoft.CodeAnalysis.CSharp** (Apache 2.0) — engine dependency, no source borrowed.

### Clean-room only (copyleft — STUDY ONLY per Stage 02 §2)

- **Documenso** (GPLv3 + Enterprise) — modern e-signing surface; **not consulted for this hand-off** (signing scope is `blocks-docs-signing`).
- **OpenSign** (AGPLv3) — DocuSign alternative; not consulted.
- **Razuna** (GPLv3) — DAM workflow; not consulted (different cluster scope).
- **Wiki.js / HedgeDoc** (AGPLv3) — not consulted (different cluster scope).

**Discipline check before merging any PR in this hand-off:**

1. No copyleft source code was opened in any editor session that produced this hand-off's PRs (Documenso / OpenSign / Wiki.js / HedgeDoc / Razuna).
2. No identifier names from any GPL/AGPL source appear in the new code (spot-check by grep before merge).
3. The clean-room schema in `blocks-docs-schema-design.md` §3.3 is the source of truth; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + entities + repos) | ~20 | record fields; status transitions; repository round-trip; tenant isolation; soft-delete |
| PR 2 (rendering engine + sandbox) | ~25 | variable validation; clause condition parser; sandbox enforcement; HTML escape; ReDoS; AssemblyLoadContext collect |
| PR 3 (IContractInstanceService) | ~16 | render happy + every failure path; status transitions; event emission; tenant isolation |
| PR 4 (orchestrator + worker) | ~16 | submit + pull + retry + backoff; cancellation; concurrency; PDF stub routing; event emission |
| PR 5 (ERPNext importer) | ~11 | idempotency; variable inference; soft-merge fields; source-kind dispatch; tenant isolation |
| PR 6 (DI umbrella + tests) | ~5 | registration order + lifetime correctness |
| **Total** | **~93 new tests** | |

### Cluster-level acceptance (PASS gate at end of PR 6)

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.DocsTemplates` package + every downstream consumer (including a smoke-host that ties `blocks-docs-core` + `blocks-docs-templates` + a stub `blocks-leases` consumer together).

**A2.** `dotnet test packages/blocks-docs-templates/tests/` passes all ~93 new tests.

**A3.** **End-to-end render round-trip.**
- Seed a `ContractTemplate` via `IContractTemplateRepository.UpsertAsync` (lease template; 8 fields; 2 conditional clauses).
- Create the template body in `blocks-docs-core` (publish via `IDocumentCommandService.PublishAsync`).
- Call `IContractInstanceService.RenderAsync(templateId, variables, optionalClauseIds: [], outputFormat: markdown)`.
- Assert: `RenderContractResult.Error == None`; one `ContractInstance` row exists with status `rendered`; one new `Document` row exists of type `contract-instance`; one `TemplateRenderJob` row exists with status `complete`; one `Docs.ContractRendered` event captured.

**A4.** **Async render round-trip via orchestrator.**
- `IRenderJobOrchestrator.SubmitAsync(request)`.
- Poll `GetStatusAsync` until status `complete` (within 2s with the default worker loop).
- Assert: same surface invariants as A3 + `TemplateRenderJob.RetryCount == 0` + `TemplateRenderJob.CompletedAtUtc` populated.

**A5.** **Retry-exhaustion round-trip.**
- Submit a render request that deterministically fails (e.g., a template body that triggers a `SandboxViolation`).
- Assert: after `MaxRenderJobRetries + 1` worker passes, status is `failed` terminally + `Docs.TemplateRenderJobFailed` event emitted exactly once.

**A6.** **Sandbox boundary.**
- Submit a template body that attempts `@System.IO.File.ReadAllText("/tmp/secret")`.
- Assert: render returns `SandboxViolation`; no file read attempted (verified by absence of any file-system probe in the test logs); job transitions to `failed` after retry exhaustion.

**A7.** **HTML escape.**
- Render an HTML template where one variable value is `<script>alert(1)</script>`.
- Assert: rendered output contains `&lt;script&gt;alert(1)&lt;/script&gt;` (escaped); does NOT contain the raw `<script>` tag.

**A8.** **Idempotency on re-render with same key.**
- Render twice with the same `IdempotencyKey`; assert the second call returns the prior `ContractInstance` without re-invoking the engine (verify by checking the engine's invocation count metric is 1 not 2).

**A9.** **ERPNext importer round-trip.**
- Construct `ErpnextContractTemplateSource` (1 contract template with 4 `{{...}}` placeholders).
- Pre-seed `blocks-docs-core` services.
- Call `IErpnextContractTemplateImporter.UpsertFromErpnextAsync`.
- Assert: `ImportOutcome.Inserted` with a non-null `ContractTemplate`; 4 `ContractTemplateField` rows created; re-run returns `Skipped`.

**A10.** **Tenant isolation.**
- Seed two tenants (`tenantA`, `tenantB`); each authors a `ContractTemplate` with the same `Name`; render each.
- Assert: no crosstalk; each tenant's `ContractInstance` query returns only its own; event envelopes carry the correct `tenantId`.

**A11.** **Consumer-side smoke (blocks-leases compile).**
- `dotnet build packages/blocks-leases/` passes after `blocks-docs-templates` lands (no API changes expected — late-binding via DI; the future lease-template hand-off wires the consumer surface).

**A12.** **Concurrency under orchestrator.**
- Submit 20 render jobs concurrently (across 3 templates).
- Assert: all 20 complete within 10s; no jobs lost or duplicated; per-job idempotency key collisions correctly deduplicated at the event publisher.

### Rendering invariants (assertions that must hold across all renders)

- **Determinism:** rendering the same template version + same variables + same clause-id set + same output format = byte-identical output. (Asserts the engine introduces no nondeterministic ordering; clauses sort by `SortOrder`; whitespace normalization is deterministic.)
- **Variable scoping:** template body may reference only declared `ContractTemplateField.Name` values; undeclared identifiers in the body produce an empty-string (with a soft warning) — NEVER a render-failure or a leak from the host scope.
- **Tenant containment:** no cross-tenant `IReferenceResolver` lookup can succeed (the resolver receives `tenantId` and must enforce; the stub `AlwaysFoundReferenceResolver` is exempt for v1 — documented).
- **No clock-skew assumptions:** all timestamps in events / jobs / instances use UTC `Instant`; tests do not depend on local-time conversion.
- **No I/O:** rendering performs zero file or network I/O (verified by a test that wraps the render call in an `AppDomain` probe — or, more practically, an `IDisposable` probe that asserts no `FileStream` / `HttpClient` was constructed during the render).

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### 1. Razor engine sandbox surface deemed unacceptable by security council (PR 2 — CRITICAL)

If the security-engineering subagent on PR 2 returns BLOCKING findings on the Razor sandbox boundary that cannot be mitigated within the current hand-off scope, **halt and file `cob-question-2026-05-XXTHH-MMZ-docs-templates-engine-fallback.md`** proposing **Fluent (Apache 2.0, Liquid implementation)** as the fallback engine.

**Fallback engine: Fluent.**
- License: Apache 2.0 — clean, no surprises.
- Surface: Liquid template language; logic-less by default; expressions limited to filters + simple comparisons.
- Sandbox: weaker than Razor's compiled path but stronger than free-form C# — the parser is the sandbox.
- Trade-off: less expressive than Razor for clause conditions (we keep our hand-rolled clause-condition parser; just swap the body engine).

XO authors the fallback hand-off addendum on receipt of the beacon.

### 2. `blocks-docs-core` PR 2 not merged (PR 1 — CRITICAL)

**Pre-build checklist step 1** catches this. If `IDocumentCommandService.CreateAsync` doesn't exist, **STOP** — the docs-core hand-off must land first. File `cob-question-*` requesting docs-core sequence-up.

### 3. Voiding a `fullySigned` ContractInstance — audit-trail requirement (PR 3)

Voiding a contract that was already fully-signed is a sensitive operation (it preserves the signed audit but flips the operational status). The current shape allows it via `IContractInstanceService.VoidAsync`. **If during PR 3 review the security-engineering subagent (or COB self-audit) determines an additional audit-log entry should be written via `blocks-docs-signing.SigningAuditLog`** (which doesn't exist yet in this hand-off's deliverables) — halt and file `cob-question-*` proposing one of:

- (a) Defer void-after-sign to `blocks-docs-signing` (the signing cluster owns the post-sign audit trail).
- (b) Ship a local `IContractInstanceAuditLog` interface + in-memory implementation here, with a TODO comment for relocation to `blocks-docs-signing` when that cluster lands.

XO recommendation: **(a) defer**. The current hand-off's `VoidAsync` should reject voiding a `fullySigned` instance with `VoidError.RequiresSigningClusterAudit`; the signing cluster's hand-off then implements the post-sign void path.

### 4. `IPdfRenderer` real implementation precedes this hand-off (PR 4)

If `blocks-reports-pdf` ships **before** this hand-off lands (unlikely per the workstream queue), wire the real `IPdfRenderer` instead of the `NoOpPdfRenderer` stub. No halt; the choice is `services.AddSingleton<IPdfRenderer, ReactPdfRenderer>()` vs `services.AddSingleton<IPdfRenderer, NoOpPdfRenderer>()`. Update the apps/docs page accordingly.

### 5. ERPNext source format ambiguity (PR 5)

The ERPNext "Contract" doctype has multiple body fields (`contract_terms`, `terms_template`, `terms`). If during PR 5 testing the importer cannot reliably select which field is the canonical body, file `cob-question-*` requesting an ERPNext-source-fixture sample from CO's actual data. v1 default: prefer `contract_terms` → `terms_template` → `terms` in that order; log a warning if multiple are non-empty.

### 6. AssemblyLoadContext memory leak in production runs (PR 2)

If post-merge monitoring detects monotonic memory growth in `accelerators/anchor` (or any production host) on repeated renders, file an immediate post-merge incident note. The regression test in PR 2 asserts collectability under unit-test conditions; production may exhibit different GC pressure. Possible mitigations (require XO decision): pool AssemblyLoadContexts (reuse compiled assemblies across compatible renders) vs raise the SoftMemoryBudgetBytes vs fall back to Liquid.

### 7. `IReferenceResolver` ambiguity — multiple registrations (PR 2 / PR 6)

If multiple consumer clusters register concrete `IReferenceResolver` implementations, the DI behavior (last-registered-wins for `TryAddTransient`) may produce unexpected behavior — only one resolver is invoked per render. v1 limitation: a single resolver handles all reference entities; the resolver dispatches internally on `referenceEntity` string. Document this clearly in PR 6's apps/docs page. If consumer clusters need per-entity routing, file a follow-on intake for `IReferenceResolverRouter` (out of scope here).

### 8. Loro CRDT collaborative template editing demand (any PR)

Per Stage 02 §9 Q1 + the docs-core hand-off's deferral, collaborative editing on `ContractTemplate.Body` (text-CRDT path) is deferred to a follow-on intake. **Do not enable** the AP-class text-CRDT path in this hand-off even if the user requests it mid-flight. If demand is high, file a follow-on intake — do not retrofit in this hand-off.

### 9. Event publisher package-home decision (PR 3 / PR 4)

Per `cross-cluster-event-bus-design.md` §10 Q1 the canonical event-bus dispatcher's package home is TBD. This hand-off ships local `IContractInstanceEventPublisher` + record types per the `IInvoiceEventPublisher` precedent from `blocks-financial-ar`. When the foundation event-bus package lands, the local types relocate. **No halt** at this hand-off — the local stub is a deliberate parallel-cluster convention.

### 10. Razor + AOT compatibility (PR 2)

If `accelerators/anchor` ever switches to a NativeAOT publish path, the Razor compilation surface used here may not work (NativeAOT cannot dynamically compile C# at runtime). Today (per current Anchor build) JIT is the deployment mode; no halt needed. If Anchor moves to AOT in the future, this cluster needs an alternative path (Liquid is AOT-friendly; Razor compiled-templates at build-time is also viable). File a follow-on intake at that point. **No halt at this hand-off.**

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–6 merged to main** (per the sequencing diagram above).
2. **Security council on PR 2 — APPROVED** (no outstanding BLOCKING; MAJOR findings addressed in-PR; MINOR resolved or filed as follow-ups).
3. **End-to-end render round-trip:** acceptance tests A3 + A4 pass.
4. **Retry exhaustion works:** acceptance test A5 passes; `Docs.TemplateRenderJobFailed` emitted exactly once.
5. **Sandbox boundary holds:** acceptance test A6 passes.
6. **HTML escape correct:** acceptance test A7 passes.
7. **Idempotency works:** acceptance test A8 passes.
8. **ERPNext importer round-trip:** acceptance test A9 passes (insert + idempotent re-insert = Skipped).
9. **Tenant isolation:** acceptance test A10 passes.
10. **Consumer-side smoke:** `blocks-leases` builds unchanged (A11).
11. **Concurrency:** acceptance test A12 passes.
12. **Tests pass:** ~93 new tests across the package.
13. **`apps/docs/blocks/docs-templates/overview.md` published** (ships in PR 6).
14. **`NOTICE.md` + `README.md` shipped** at the package root.
15. **`active-workstreams.md`** row for blocks-docs-templates updated with `built` status + the 6 PR numbers (via the source W*.md file, not the ledger directly).
16. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-blocks-docs-templates-built.md`** beacon dropped.

When the PASS gate is met, the next hand-offs can proceed:

- `blocks-docs-signing-stage06-handoff.md` — SigningWorkflow / SigningStep / SigningParty / Signature / SigningAuditLog (consumes `ContractInstance` + `Docs.ContractRendered`).
- `blocks-docs-wiki-stage06-handoff.md` — WikiSpace / WikiBook / WikiPage / Policy / Procedure / PolicyAcknowledgment (parallel cluster sibling; no dep on templates).
- `blocks-docs-dam-stage06-handoff.md` — MarketingAsset / AssetTag / AssetCollection / BrandKit (parallel cluster sibling; no dep on templates).
- `blocks-reports-pdf-stage06-handoff.md` — real `IPdfRenderer` implementation (consumes the `IPdfRenderer` interface this hand-off ships).
- `blocks-leases-template-driven-doc-generation-stage06-handoff.md` — wires `IContractInstanceService.RenderAsync` into the lease-doc workflow.

---

## Docs

**`apps/docs/blocks/docs-templates/overview.md`** — cluster docs page (ships in PR 6). Cite ADR 0088 §1; cite Stage 02 schema design §3.3 + §5.2 + §7.2 + §7.6; cite the cross-cluster event bus design §3.4; cite CRDT conventions §1 + §6; cite the Razor sandbox boundary (with link to PR 2's council findings).

Structure (sketch):

```markdown
# blocks-docs-templates

Parametric contract-template + render-job + ContractInstance surface for
the Sunfish Anchor native document domain.

## Overview

This package is the canonical contract-template + rendered-instance
surface of the `blocks-docs-*` cluster per ADR 0088 §1 and Stage 02
§3.3. It provides:

- `ContractTemplate` — parametric template; lives as a Document of type
  `contract-template`; carries field declarations + clauses + signer
  roles.
- `ContractTemplateField` — variable interview model (text / number /
  date / currency / enum / boolean / multiLine / reference).
- `ContractTemplateClause` — reusable + conditionally-includable clauses.
- `TemplateRenderJob` — async render-job lifecycle (queued / rendering /
  complete / failed) with retry-with-backoff.
- `ContractInstance` — rendered output; status (rendered / sent /
  partiallySigned / fullySigned / voided); pins template version for
  tamper detection; posted-then-immutable once fully-signed.
- `ITemplateRenderingService` — the Razor-based render engine with
  sandbox.
- `IContractInstanceService` — lifecycle service (render → send →
  signing → void).
- `IRenderJobOrchestrator` — async submission + background-worker.
- `IErpnextContractTemplateImporter` — ERPNext migration.

## Engine choice

We use **Razor** (Microsoft.AspNetCore.Razor.Language +
Microsoft.CodeAnalysis.CSharp) as the canonical template engine. The
engine runs in a sandboxed `AssemblyLoadContext` with a deny-listed
reference set (System.IO / System.Net / System.Reflection.Emit excluded)
and deny-listed Razor directives (@inject / @inherits / @functions /
@code / @using / @implements / @page / @layout). See PR 2's security
council findings for the full sandbox audit.

## Quickstart

(~15 lines: minimal example registering DI + rendering a lease template
+ recording signing.)

## Variable + clause model

(Brief grammar reference for the clause-condition parser + the
variable-coercion table per FieldKind.)

## ERPNext migration

(Brief description of the importer + the field-inference behavior + the
post-import editorial step.)

## Cross-cluster integration

| Direction | Cluster | Surface |
|---|---|---|
| consumes | blocks-docs-core | Document + DocumentVersion + IDocumentCommandService |
| consumes | blocks-docs | StorageRef + IBlobStore (via docs-core) |
| consumes | blocks-people-foundation | PartyId |
| produces | blocks-docs-signing | ContractInstance + signingWorkflowId reverse-FK |
| produces | blocks-work-* | Docs.ContractRendered event + WorkContractId FK |
| produces | blocks-reports-pdf | IPdfRenderer interface |
| produces | blocks-leases | IContractInstanceService.RenderAsync surface |

## Related

- `blocks-docs-core` (predecessor; Document substrate)
- `blocks-docs` (storage substrate)
- `blocks-docs-signing` (follow-on; consumes ContractInstance)
- `blocks-docs-wiki` (parallel cluster sibling)
- `blocks-docs-dam` (parallel cluster sibling)
- `blocks-work-*` (cross-cluster consumer)
- `blocks-reports-pdf` (follow-on; real PDF renderer)
- `blocks-leases` (downstream consumer)
- ADR 0088 — Anchor as All-In-One Local-First Runtime
- Stage 02 schema design — `blocks-docs-schema-design.md`
```

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `packages/blocks-docs-core/Models/Document.cs` (consumed) ✓ (pending docs-core hand-off close)
- `packages/blocks-docs-core/Models/DocumentVersion.cs` ✓
- `packages/blocks-docs-core/Services/IDocumentCommandService.cs` ✓
- `packages/blocks-docs/` (consumed for StorageRef) ✓ (pending if shipped)
- `packages/blocks-people-foundation/Models/PartyId.cs` ✓
- `packages/blocks-leases/` (downstream-consumer smoke target) ✓
- ADR 0088 §2, §3 ✓
- `icm/02_architecture/blocks-docs-schema-design.md` §3.3, §4, §5.2, §7.2, §7.6, §8, §9 Q6 ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §5, §6 ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.4 (entries added by this hand-off), §10 Q1 ✓
- `_shared/engineering/standing-approved-patterns.md` — pattern-001, pattern-005, pattern-006, pattern-007 (and pattern-002 once ratified for the importer) ✓

**Introduced by this hand-off** (ship across PRs 1–6):

- New package: `packages/blocks-docs-templates/`
- New entity types: `ContractTemplate`, `ContractTemplateField`, `ContractTemplateClause`, `TemplateRenderJob`, `ContractInstance`.
- New strong-typed IDs: `ContractTemplateId`, `ContractTemplateFieldId`, `ContractTemplateClauseId`, `TemplateRenderJobId`, `ContractInstanceId`.
- New enums: `ContractTemplateCategory`, `ContractTemplateBodyFormat`, `ContractTemplateFieldKind`, `TemplateRenderJobStatus`, `ContractInstanceStatus`, `RenderOutputFormat`, `RenderTemplateError`, `RenderContractError`, `VoidError`, `PdfRendererError`, `SubmitRenderJobError`.
- New transition helpers: `TemplateRenderJobStatusTransitions`, `ContractInstanceStatusTransitions`.
- New options: `BlocksDocsTemplatesOptions` (RenderTimeoutSeconds, SoftMemoryBudgetBytes, MaxFieldTextLength, AllowRazorRawText, MaxConcurrentRenders, MaxRenderJobRetries, WorkerLoopDelayMs, RetryBackoffSecondsBase, RetryBackoffMultiplier).
- New services: `IContractTemplateRepository` + `InMemoryContractTemplateRepository`, `IContractTemplateFieldRepository` + impl, `IContractTemplateClauseRepository` + impl, `ITemplateRenderJobRepository` + impl, `IContractInstanceRepository` + impl, `ITemplateRenderingService` + `RazorTemplateRenderingService`, `IContractInstanceService` + `ContractInstanceService`, `IRenderJobOrchestrator` + `RenderJobOrchestrator`, `TemplateRenderJobWorker` (BackgroundService), `IPdfRenderer` + `NoOpPdfRenderer` (stub), `IContractInstanceEventPublisher` + `InMemoryContractInstanceEventPublisher` (stub), `IReferenceResolver` + `AlwaysFoundReferenceResolver` (stub), `IErpnextContractTemplateImporter` + `ErpnextContractTemplateImporter`.
- New event records: `ContractRenderedEvent`, `TemplateRenderJobFailedEvent`.
- New import types: `ErpnextContractTemplateSource`, `ImportOutcome<ContractTemplate>` (reuses pattern from financial cluster).
- New result records: `RenderContractRequest`, `RenderContractResult`, `RenderTemplateRequest`, `RenderTemplateResult`, `RenderMetrics`, `TaxRateBreakdownLine` — no wait, that one's financial; not here.
- New helper: `ClauseExpressionParser` (the hand-rolled recursive-descent parser).
- Docs: `apps/docs/blocks/docs-templates/overview.md`.
- Attribution: `packages/blocks-docs-templates/NOTICE.md` + `README.md`.

**Cross-cluster event bus additions** (new entries to `_shared/engineering/cross-cluster-event-bus-design.md` §3.4):

- `Docs.ContractRendered` (producer: blocks-docs-templates; consumers: work, signing).
- `Docs.TemplateRenderJobFailed` (producer: blocks-docs-templates; consumers: internal alert).

These two entries are added to the engineering doc as part of PR 3 (for ContractRendered) and PR 4 (for TemplateRenderJobFailed) — small doc edits accompanying the code that emits them.

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check negative existence too (verify `IPdfRenderer` is genuinely absent before shipping the local stub; verify no other cluster already exports a `IReferenceResolver` with conflicting semantics).

---

## Cohort discipline

This hand-off is the **third cluster implementation hand-off under ADR 0088 Path II for `blocks-docs-*`** (after the implicit `blocks-docs` substrate and `blocks-docs-core`). The COB self-audit pattern applied to those hand-offs applies here verbatim:

- **`AddBlocksDocsTemplates()` naming for the DI umbrella** — matches the cluster convention (`Add<ClusterCamelCase>()`).
- **`apps/docs/blocks/<cluster>/overview.md` page convention** — applied in PR 6.
- **`NOTICE.md` + `README.md` at the package root** referencing Stage 02 design + ADR 0088 — ship in PR 6.
- **`ConcurrentDictionary` for any cache** — applied in all 5 in-memory repositories + the render-job worker's in-flight set.
- **Strong-typed Id records** (ULID-backed) — applied for all 5 new ID types.
- **Stub interfaces for cross-cluster contracts not yet shipped** — applied for `IPdfRenderer`, `IReferenceResolver`, `IContractInstanceEventPublisher`. Each ships locally; relocates when the canonical home lands; DI swap with no public-surface change.
- **Two-overload constructor pattern** (audit-disabled / audit-enabled both-or-neither) — NOT required in this hand-off (no audit interaction beyond event emission, which is the standard publisher pattern). If audit is later wired (e.g., a `IContractInstanceAuditLog` for void-after-sign per Halt §3), retrofit per the W#34/W#35 substrate-only pattern.
- **In-memory repository lifetime: Scoped, not Singleton** — per bug-AddInMemory-Scoped: all `InMemoryXxxRepository` registrations use `Scoped` lifetime so per-request state isolation works correctly in `accelerators/anchor` hosting.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-docs-templates-{slug}.md` in `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for blocks-docs-templates.
- `ScheduleWakeup 1800s`.

If COB completes PR 6 + the PASS gate is met:

- Update the workstream source W*.md file (NOT the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Run `tools/icm/render-ledger.py` to refresh `active-workstreams.md`.
- Drop `cob-status-2026-05-XXTHH-MMZ-docs-templates-built.md` to inbox.
- Continue with the next hand-off in the Phase 3 docs-cluster sequence (likely `blocks-docs-signing` — XO drops next).

---

## Open questions (for XO/CO disposition, non-blocking on the hand-off start)

1. **Engine choice — Razor vs Liquid.** XO recommends Razor (rationale in §Critical-design-decision). If security council on PR 2 finds Razor's sandbox surface unacceptable, fallback is Fluent (Apache 2.0 Liquid). **Recommended position: ship with Razor; revisit only if council blocks.**

2. **Void-after-sign audit trail home.** Currently `IContractInstanceService.VoidAsync` can void any non-voided state, including `fullySigned`. XO recommends deferring void-after-sign to `blocks-docs-signing` (which owns the post-sign audit log). This hand-off's `VoidAsync` should reject voiding a `fullySigned` instance with `VoidError.RequiresSigningClusterAudit`. **Recommended position: defer per Halt §3 option (a).**

3. **Collaborative template editing (Loro text-CRDT).** Deferred per Stage 02 §9 Q1 + the docs-core hand-off's stance. **Recommended position: defer to a future intake; do not enable in this hand-off.**

4. **PDF renderer wiring.** `blocks-reports-pdf` is the canonical owner. This hand-off ships `NoOpPdfRenderer` as the v1 stub. **Recommended position: ship the stub; wire the real renderer in a follow-on chore PR after `blocks-reports-pdf` lands.**

5. **Reference-entity routing.** v1 supports a single `IReferenceResolver` per host; the resolver dispatches internally on `referenceEntity` string. **Recommended position: ship single-resolver v1; if multiple consumer clusters need per-entity routing, file a follow-on intake for `IReferenceResolverRouter`.**

6. **`AssemblyLoadContext` pooling.** v1 creates a fresh ALC per render. If production exhibits memory pressure, pool ALCs across compatible renders. **Recommended position: ship per-render-fresh v1; pool only if measured pressure surfaces.**

7. **Field-kind extensibility.** `ContractTemplateFieldKind` enum has 8 v1 values. If consumer clusters need new kinds (`signature-placeholder`, `attachment-upload`, `address-block`), file a follow-on intake. **Recommended position: v1 enum is final; new kinds are an api-change pipeline.**

---

## Cross-references

- Spec source: `icm/02_architecture/blocks-docs-schema-design.md` §3.3 (all sub-sections), §4, §5.2, §7.2, §7.6, §8, §9 Q6.
- CRDT conventions: `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §5, §6.
- Path II CRDT classification: `icm/02_architecture/path-ii-crdt-schema-conventions.md` §1.
- Event bus: `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.4, §10 Q1.
- Path II event bus: `icm/02_architecture/path-ii-cross-cluster-event-bus.md` §1, §2, §6.
- Standing-approved patterns: `_shared/engineering/standing-approved-patterns.md` — pattern-001 (PR 1), pattern-002 (PR 5 once ratified), pattern-005 (PR 6), pattern-006 (PR 6), pattern-007 (PR 6).
- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- Predecessor hand-off: `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md` (the 3-PR docs-core build that ships the Document substrate this hand-off consumes).
- Sibling hand-offs (Phase 3 cluster context — likely concurrent or follow-on):
  - `blocks-docs-signing-stage06-handoff.md` (SigningWorkflow / SigningStep / SigningParty / Signature — consumes ContractInstance + signingWorkflowId reverse-FK)
  - `blocks-docs-wiki-stage06-handoff.md` (WikiSpace / WikiBook / WikiPage / Policy / Procedure — parallel cluster sibling)
  - `blocks-docs-dam-stage06-handoff.md` (MarketingAsset / AssetTag / AssetCollection / BrandKit — parallel cluster sibling)
- Cohort precedent hand-offs (substrate-only shape):
  - `blocks-docs-core-stage06-handoff.md` (direct precedent — same Path II clean-room cluster)
  - `blocks-financial-ar-stage06-handoff.md` (precedent for the 6-PR shape + ERPNext importer integration + event publisher stub pattern)
  - `blocks-financial-tax-stage06-handoff.md` (precedent for security-engineering spot-review on a fiscal-correctness-critical PR — analogous to PR 2's render-engine review here)

---

**End of hand-off.**
