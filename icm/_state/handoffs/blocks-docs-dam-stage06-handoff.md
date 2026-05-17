# Hand-off — `blocks-docs-dam` Digital Asset Management (Phase 3 follow-on; MarketingAsset + AssetTag + AssetCollection + AssetUsage + BrandKit)

**From:** XO (research session)
**To:** sunfish-PM session (COB) — overflow-eligible to dev
**Created:** 2026-05-17
**Status:** `ready-to-build` — **gated on `blocks-docs-core` (W#69) all PRs merged AND `blocks-docs` (attachment substrate, W#71) all 6 PRs merged**
**Workstream:** W#73 — blocks-docs-dam (Phase 3 follow-on; marketing-asset lifecycle layer of the document cluster)
**Spec source:** [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) §3.4 (all sub-sections: MarketingAsset + AssetTag + AssetTagAssignment + AssetCollection + AssetCollectionMembership + AssetUsage + BrandKit + BrandKitElement) + §5.4 (DAM asset lookup pseudocode) + §6 (storage model, consumed) + §7.4 (cross-cluster sync semantics, consumed)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) §3 (cluster grouping)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~10–14h (5 PRs + ~55–65 tests + docs + attribution)
**PR count:** 5 PRs
**Pre-merge council:** NOT required (low-risk surface — marketing-asset lifecycle is read-mostly + non-financial + non-auth; tenant-isolation flows through the existing `blocks-docs-core` + `blocks-docs` substrate guarantees). Standard COB self-audit applies. Standing patterns operative: `pattern-001` (PR 1 scaffold), `pattern-005` (PR 5 DI umbrella), `pattern-006` (PR 5 docs page).
**Attribution required:** **ResourceSpace** (BSD-3) — DAM asset + collection + usage-tracking + brand-kit shapes. NOTICE entry mandatory. Per Stage 02 §8 "Discipline note": Razuna observed at surface level only (GPLv3 — clean-room; no code or schema borrowed). Bynder + Brandfolder are proprietary SaaS observed only via public product pages (no source; concepts validated against ResourceSpace).
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/Document.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/Attachment.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/StorageRef.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs-(dam|wiki|templates|signing)"
```
Expected at this hand-off's start: `blocks-docs-core/` exists with the full §3.1 substrate (Document + DocumentVersion + repositories); `blocks-docs/` exists with `Attachment` + `StorageRef`; nothing matching `blocks-docs-dam/` (audit confirms package name available). `blocks-docs-wiki/` may also exist by the time DAM is dequeued — irrelevant to this hand-off (no cross-coupling).

---

## Gate conditions

This hand-off cannot start until BOTH of the following are true:

### Gate 1 — `blocks-docs-core` (W#69) all PRs merged

`MarketingAsset.documentId` is a foreign key to `Document` (per Stage 02 §3.4.1: `documentId: ID; documentType = 'marketing-asset'`). The `Document` entity, `DocumentId` strong type, `DocumentType` enum (which must include `MarketingAsset` per the W#69 hand-off §3.4), `IDocumentRepository`, and `IDocumentCommandService` must all be available on `main`. Verify with:

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/Document.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/DocumentId.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/DocumentType.cs
grep -n "MarketingAsset" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/DocumentType.cs 2>&1
```

If `DocumentType.MarketingAsset` is missing from the enum (W#69 hand-off PR 1 §Enums lists it; verify it survived COB build), file `cob-question-2026-05-XXTHH-MMZ-w73-docs-dam-documenttype-enum.md` requesting a minor amendment to `blocks-docs-core` to add the enum value. Halt this hand-off until the amendment lands.

### Gate 2 — `blocks-docs` (attachment substrate, W#71) all 6 PRs merged

`MarketingAsset.storageRef` (required) and `MarketingAsset.thumbnailRef` (optional) consume the `StorageRef` discriminated union from `blocks-docs` (per Stage 02 §3.4.1 + §6.1). The `Attachment` entity, `StorageRef` DU, and `IAttachmentService` must all be on `main`. Verify with:

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/StorageRef.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/Attachment.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Services/IAttachmentService.cs
gh pr list --state open --search "blocks-docs in:title,body"
```

If any of those files is missing OR an open PR is touching `blocks-docs/Models/StorageRef.cs`, **STOP** — DO NOT begin PR 1 until the `blocks-docs` cluster has fully merged its 6-PR series. The `StorageRef` shape MUST be stable before DAM consumes it (a mid-merge `StorageRef` refactor would cascade into DAM rework).

**Why both gates are hard.** Unlike DAM ↔ docs-core (which is a pure type FK), DAM ↔ blocks-docs is a *behavioral* dependency: the binary lifecycle (upload, hash, content-address, tenant-quota enforcement, MIME validation) is owned by `IAttachmentService`. The DAM `IMarketingAssetService.RegisterAsync(...)` flow accepts a pre-existing `AttachmentId` (the binary has already been uploaded by an upstream caller) and links it via `StorageRef`. DAM does NOT upload bytes itself.

### Gate 3 (advisory only — not blocking) — `blocks-people-foundation` (PartyId)

`AssetCollection.ownerId`, `AssetTagAssignment.assignedBy`, `BrandKit` audit fields, and `AssetUsage.assignedBy`-style fields all want `PartyId` from `blocks-people-foundation`. The W#69 docs-core hand-off already takes a dependency on `blocks-people-foundation`. By transitive dependency through `blocks-docs-core.csproj`, `PartyId` is available — no additional gate needed. If for some reason `blocks-people-foundation` was downgraded (unlikely), file `cob-question-*`; do NOT introduce a local `PartyId` placeholder.

---

## Context

### Phase 3 document cluster position

`blocks-docs-dam` is the **fourth follow-on** layer of the document cluster per Stage 02 §3:

```
blocks-docs                  (attachment substrate — Attachment + StorageRef + IBlobStore + DocumentRef; W#71)
  └──▶ blocks-docs-core      (Document base entity + versioning + folders + permissions + retention; W#69)
        ├──▶ blocks-docs-wiki        (WikiSpace + WikiPage + Policy; W#70 — independent sibling)
        ├──▶ blocks-docs-templates   (ContractTemplate + render-job; W#72-or-future — independent sibling)
        ├──▶ blocks-docs-dam         ← THIS HAND-OFF (MarketingAsset + AssetTag + AssetCollection + AssetUsage + BrandKit)
        └──▶ blocks-docs-signing     (SigningWorkflow + Signature; future — independent sibling)
```

DAM is **horizontally independent** of `-wiki`, `-templates`, `-signing`. It can ship at any time after both gates above clear; it does not block and is not blocked by any sibling. The cluster grouping in ADR 0088 §3 is logical (shared spec; shared attribution discipline; shared NOTICE files; shared apps/docs hub) — not a build order.

### What this hand-off ships

Per `blocks-docs-schema-design.md` §3.4 (the entire sub-section) + §5.4 (lookup pseudocode):

**8 entity types** (all in `Models/`):

| Type | Description |
|---|---|
| `MarketingAsset` | The DAM record — wraps a `Document` (`documentType = MarketingAsset`); `assetKind` discriminator (`image/video/audio/pdf/document/copy-snippet/animation`); binary-first via `StorageRef`; rights metadata; a11y `altText`; technical metadata (`durationSeconds`, `widthPx`, `heightPx`) |
| `AssetTag` | Tag taxonomy — name, slug, `taxonomyKind` (`subject/campaign/season/channel/product/free-form`); hierarchical via optional `parentTagId` |
| `AssetTagAssignment` | Many-to-many join — `assetId × tagId`; tracks `confidence` (null = human-assigned) + `assignedBy` (null = ML-assigned, reserved for future AI-tag pass) |
| `AssetCollection` | Curated set — `collectionKind` (`campaign/brand-kit-member/gallery/mood-board/ad-hoc`); cover-asset pointer; owner; archive timestamp |
| `AssetCollectionMembership` | Join row — `collectionId × assetId × sortOrder × addedAt` |
| `AssetUsage` | Where the asset is being used — `consumerKind` (`campaign/wiki-page/contract/website/listing/email-template/external`); for licensing compliance + impact-of-change queries |
| `BrandKit` | Tenant brand pack — `name`, `description`, `isActive`, `effectiveFrom`, `effectiveUntil` |
| `BrandKitElement` | Brand-kit member — `elementKind` (`logo/color/font/voice-note/tagline/icon-set`); FK to `MarketingAsset` for image-backed elements (logos, icons); inline fields for color (`colorHex`) / font (`fontFamily`) / text (`textContent`); `sortOrder` + `isPrimary` |

**4 supporting enums** (in `Models/`):

| Enum | Values |
|---|---|
| `AssetKind` | `Image`, `Video`, `Audio`, `Pdf`, `Document`, `CopySnippet`, `Animation` |
| `AssetTaxonomyKind` | `Subject`, `Campaign`, `Season`, `Channel`, `Product`, `FreeForm` |
| `AssetCollectionKind` | `Campaign`, `BrandKitMember`, `Gallery`, `MoodBoard`, `AdHoc` |
| `AssetConsumerKind` | `Campaign`, `WikiPage`, `Contract`, `Website`, `Listing`, `EmailTemplate`, `External` |
| `BrandKitElementKind` | `Logo`, `Color`, `Font`, `VoiceNote`, `Tagline`, `IconSet` |
| `AssetRightsOwnership` | `Owned`, `Licensed`, `PublicDomain`, `CreativeCommons` |

**Value object** (in `Models/`):

| Type | Description |
|---|---|
| `AssetRights` | `ownership: AssetRightsOwnership`, `licenseName: string?`, `usageRestrictions: string[]` (e.g. `["no-derivative", "attribution-required"]`) |

**Strongly-typed IDs** (ULID-string pattern, same as financial / docs-core clusters):
`MarketingAssetId`, `AssetTagId`, `AssetTagAssignmentId`, `AssetCollectionId`, `AssetCollectionMembershipId`, `AssetUsageId`, `BrandKitId`, `BrandKitElementId`.

**Repository contracts + in-memory implementations** (in `Services/`):

- `IMarketingAssetRepository` + `InMemoryMarketingAssetRepository`
- `IAssetTagRepository` + `InMemoryAssetTagRepository`
- `IAssetTagAssignmentRepository` + `InMemoryAssetTagAssignmentRepository`
- `IAssetCollectionRepository` + `InMemoryAssetCollectionRepository`
- `IAssetCollectionMembershipRepository` + `InMemoryAssetCollectionMembershipRepository`
- `IAssetUsageRepository` + `InMemoryAssetUsageRepository`
- `IBrandKitRepository` + `InMemoryBrandKitRepository`
- `IBrandKitElementRepository` + `InMemoryBrandKitElementRepository`

**Domain services** (in `Services/`):

- `IMarketingAssetService` — lifecycle (register, update-metadata, set-rights, soft-delete via tombstone), search via the §5.4 algorithm, impact-of-change queries.
- `IAssetCollectionService` — collection CRUD; membership add/remove; reorder; archive.
- `IAssetUsageService` — usage tracking (`RecordUsage`, `MarkUsageInactive`, `QueryUsagesForAsset`).
- `IBrandKitService` — brand-kit CRUD; element add/remove; activate / deactivate; effective-on query (`GetActiveAsOfAsync(DateOnly)`).

**Cross-cluster event records** (in `Models/Events/`):

| Event | Producer | Consumer clusters | Payload |
|---|---|---|---|
| `Dam.MarketingAssetRegistered` | dam | reports, listings, marketing (future) | `{ assetId, documentId, tenantId, assetKind, registeredAt }` |
| `Dam.MarketingAssetMetadataUpdated` | dam | (none external; internal cache invalidation) | `{ assetId, fieldsChanged: string[] }` |
| `Dam.MarketingAssetTombstoned` | dam | reports, listings | `{ assetId, tombstoneReason, tombstonedBy, tombstonedAt }` |
| `Dam.AssetCollectionPublished` | dam | reports | `{ collectionId, assetCount }` |
| `Dam.AssetUsageRecorded` | dam | reports | `{ usageId, assetId, consumerKind, consumerId? }` |
| `Dam.BrandKitActivated` | dam | reports, marketing (future) | `{ brandKitId, effectiveFrom }` |

(Per `cross-cluster-event-bus-design.md` §3.x — DAM events live under producer namespace `dam`. Each event ships as a `record` in `packages/blocks-docs-dam/Models/Events/`. If by the time of build, the canonical `IDomainEventPublisher` from `foundation-events` is the production publisher, DI registers it; otherwise a local in-memory stub ships and relocates in a follow-on.)

**Idempotency-key contributions** (catalog additions, see §Idempotency-key catalog below):
- `marketing-asset-registered:{assetId}`
- `asset-collection-published:{collectionId}:{publishAttemptAt}`
- `asset-usage-recorded:{usageId}`
- `brand-kit-activated:{brandKitId}:{effectiveFrom}`

### What this hand-off does NOT ship (deferred / out of scope)

1. **Auto-thumbnail generation.** `MarketingAsset.thumbnailRef` is a nullable `StorageRef` — callers provide a pre-generated thumbnail or leave it null. **Per Stage 02 §9 Q4 (Open Question — XO recommendation):** *"Lazy thumbnails, no native binaries"* — defer thumbnail generation to first display + cache. This hand-off does NOT ship `IThumbnailGenerator`, ImageSharp dependency, ffmpeg-net binding, or any image-decode path. A follow-on intake will reopen the question when the UI surface (browser-side or Anchor MAUI-side) demands it.

2. **AI / ML asset tagging.** `AssetTagAssignment.confidence` and `AssetTagAssignment.assignedBy = null` exist in the schema to reserve space for an ML-tagging follow-on. This hand-off **does NOT** ship any ML model, tagging pipeline, image-recognition integration, or background tag-suggestion job. All tag assignments in v1 are human-driven (`confidence = null`, `assignedBy = <PartyId>`).

3. **Full-text search for `findAssets(query, ...)` (per §5.4).** The §5.4 pseudocode references `fts(['title', 'description', 'altText'], query)` — a full-text-search index. In v1, the in-memory implementation does substring matching (case-insensitive `Contains` on each searchable field). A follow-on intake will introduce SQLite FTS5 / external search index when needed; the `IMarketingAssetService.SearchAsync(...)` surface is identical (caller-transparent swap).

4. **Cross-cluster integration with listings / marketing-publish / social-publish.** DAM is the *producer* of `Dam.*` events; the consumer surfaces (e.g., `blocks-listings.ListingPhotoSelector` consuming `MarketingAssetRegistered` to populate the photo-picker) are NOT in scope. Those land with their respective cluster hand-offs. This hand-off only ships the event *emissions* + record types.

5. **Encryption at rest of asset bytes.** Per the `blocks-docs` substrate hand-off, the underlying `IBlobStore` writes bytes plaintext to the local CAS (kernel-security envelope encryption is wired in a follow-on). DAM inherits whatever encryption posture `blocks-docs` provides at build time. DAM does NOT introduce its own encryption layer.

6. **Brand-compliance acknowledgment workflow.** Per Stage 02 §9 Q9 (Open Question): *"Brand-kit-as-policy. BrandKit elements (logo, colors, fonts) are arguably governance artifacts that should require acknowledgment from marketing staff... Current design keeps them separate. Confirm: do we want a Brand-Compliance acknowledgment workflow now or later? Recommend later."* — Acknowledgment is `blocks-docs-wiki.PolicyAcknowledgment` scope; DAM has no acknowledgment surface in v1.

7. **CRDT-aware concurrent metadata editing.** `MarketingAsset.title` / `description` / `altText` are text fields; concurrent two-replica edits resolve via last-writer-wins (per `crdt-friendly-schema-conventions.md` §7 cluster table — `blocks-docs-dam` is *not* listed there, defaulting to LWW). A future intake may upgrade to Loro text-CRDT for descriptions if demand surfaces; not v1.

8. **Asset version history.** Unlike `Document` (which has `DocumentVersion` + `DocumentRevisionHistory` in `blocks-docs-core`), `MarketingAsset` does NOT carry its own version chain in v1. If the underlying binary changes (e.g., a re-export of the same logo at higher resolution), the recommended pattern is **register a new MarketingAsset row** + add `AssetUsage` rows pointing at both (with the older one's `isActive = false` after the cutover). The wrapping `Document` does carry `DocumentVersion` history per `blocks-docs-core` — that surface is available if the consumer wants per-version metadata, but the DAM service layer does not expose it directly. Documented as a known limitation in the apps/docs page.

9. **EFCore entity configurations.** Mirror whatever pattern `blocks-docs-core` lands on `main` (per the W#69 hand-off PR 3 §EFCore). If the docs-core PRs ship without EFCore (in-memory only — likely), this hand-off also ships without EFCore. A follow-on bundles EFCore across all `blocks-docs-*` packages at once when the SQLite persistence surface arrives.

10. **DAM-side full UI.** kitchen-sink demo / apps/docs page reference the cluster but do NOT ship a full UI; the UI lives in `accelerators/anchor-react/` (or equivalent) and is a separate front-end hand-off. The apps/docs page in PR 5 shows the C# DI registration + a 10-line code example only.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | All 8 Id types (`MarketingAssetId`, etc.) — strongly typed records over ULID strings |
| §2 Soft-delete tombstones | `MarketingAsset.deletedAt` / `deletedBy` / `deletedReason` on the row; same pattern on `AssetCollection.archivedAt` (already in the schema; treat as soft-delete tombstone semantically); `BrandKit.effectiveUntil` is the brand-kit tombstone analog (setting `effectiveUntil` to a past instant deactivates the kit without deleting it). Hard-delete is NEVER allowed on `MarketingAsset` (a tombstoned asset's `AssetUsage` history must survive for licensing audit). |
| §3 version + revisionVector | `MarketingAsset.Version` int + `RevisionVector` `IReadOnlyDictionary<string,long>?` — Loro-managed; application reads only. Same pattern on `AssetCollection`, `BrandKit`. |
| §4 Append-only sub-collections | `AssetTagAssignment[]` per asset and `AssetCollectionMembership[]` per collection are append-only; "removing" a tag or membership is a tombstone on the join row (`removedAt: Instant?`), not a hard delete. This preserves history for licensing audit + "what changed?" introspection. |
| §5 Stable string codes | All enums (`AssetKind`, `AssetTaxonomyKind`, `AssetCollectionKind`, `AssetConsumerKind`, `BrandKitElementKind`, `AssetRightsOwnership`) surface as string codes over the wire and in storage. NEVER an integer ordinal. |
| §6 Posted-then-immutable — N/A | DAM has no "posted" lifecycle; assets are mutable metadata throughout life. Tombstoning is the only terminal state. |
| §7 State-machine-under-CRDT — N/A | DAM has no state machine. (Collection `archivedAt` is a soft flag, not a state machine; concurrent archive vs. unarchive resolves by latest write per Loro version-vector, terminal-archive-wins is NOT desired here — an accidental archive should be undoable.) |
| §10 Two-tier validation | Tier-1 write-time on every Asset / Collection / BrandKit persist (e.g., `MarketingAsset.storageRef` non-null; `BrandKit.effectiveFrom <= effectiveUntil` if both set; `BrandKitElement.colorHex` is a valid 6/8-character hex if `elementKind = Color`); Tier-2 post-merge reconciler verifies `AssetUsage` references still point at existing assets (orphan-usage cleanup) — ships as a stub `IPostMergeReconciler` registration in PR 5. |

The combination ensures: (a) two offline replicas can concurrently register, tag, and collect different assets and converge cleanly; (b) tombstones preserve audit; (c) brand-kit activation is monotonic per (tenant, effectiveFrom) ordering.

### Why DAM is reasonable to ship in Phase 3

1. **Schema is shallow.** No double-entry posting, no signing crypto, no real-time collaboration — just typed metadata + foreign-keys + simple queries. 5 PRs maps cleanly onto entity-group ownership.
2. **Substrate is in place.** With `blocks-docs-core` (Document + DocumentVersion) and `blocks-docs` (StorageRef + Attachment) both shipped, the DAM layer is essentially pure projections on top.
3. **Low cross-cluster blast radius.** Consumers (listings / marketing-publish / etc.) are *future* surfaces; DAM ships as a producer-only layer. Adding consumers later is additive, not breaking.
4. **No copyleft entanglement.** ResourceSpace is BSD-3 (permissive). Razuna observed at surface only — clean-room discipline already in place from the Stage 02 design.
5. **Demo-ready.** A property-management business needs a place to store listing photos + brand assets (logos, color palette); shipping DAM early unblocks a tangible apps/kitchen-sink screen.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify Gate 1 (`blocks-docs-core`) is built.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/Document.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/DocumentId.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/DocumentType.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Services/IDocumentRepository.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Services/IDocumentCommandService.cs
   ```
   Expected: all five exist. If any is missing or the `DocumentType` enum lacks `MarketingAsset`, STOP and file `cob-question-2026-05-XXTHH-MMZ-w73-docs-dam-core-missing.md`.

2. **Verify Gate 2 (`blocks-docs`) is built.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/StorageRef.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/Attachment.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Services/IAttachmentService.cs
   ```
   Expected: all three exist. If `StorageRef.cs` is missing, STOP. If `Attachment.cs` is missing, STOP. DAM cannot proceed until `blocks-docs` has fully merged.

3. **Confirm `blocks-people-foundation` is on main.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/Models/PartyId.cs 2>&1
   ```
   Expected: exists (W#69 docs-core required it). If absent (highly unlikely if Gate 1 cleared), file `cob-question-*`.

4. **Verify no parallel-session PRs touch the `blocks-docs-*` cluster.**
   ```bash
   gh pr list --state open --search "blocks-docs in:title,body"
   gh pr list --state open --search "blocks-docs-dam in:title,body"
   gh pr list --state open --search "MarketingAsset in:files"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*` to coordinate.

5. **Read the source sections.** Skim `blocks-docs-schema-design.md` §3.4 (entirety), §5.4 (lookup algorithm), §6 (storage model — already consumed via `blocks-docs`; understand the StorageRef contract), §7.4 (sync semantics), §8 (FOSS-source citations — note ResourceSpace BSD-3 + Razuna/Bynder/Brandfolder discipline), §9 Q4 + Q9 (open questions explicitly punted here).

6. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

7. **Capture canonical NOTICE.md template** from a precedent sibling — e.g., `packages/blocks-docs-core/NOTICE.md` or `packages/blocks-financial-ar/NOTICE.md` — and adapt for ResourceSpace BSD-3 attribution per §License posture below.

8. **Confirm the `apps/docs/blocks/` directory** has a per-cluster pattern matching `packages/blocks-docs-{core,wiki}/` (sibling docs pages); your `apps/docs/blocks/docs-dam/overview.md` lands in PR 5.

9. **Check the `cross-cluster-event-bus-design.md` §3.x catalog** for any conflicts with the proposed `Dam.*` event names; if `Dam.MarketingAssetRegistered` (or any other) is already registered to a different producer, file `cob-question-*` — XO must adjudicate the catalog conflict.

10. **Verify standing-pattern eligibility for PRs 1 + 5.** Per `_shared/engineering/standing-approved-patterns.md`:
    - PR 1 — `pattern-001` (cluster scaffold + repository + DI) — matches.
    - PR 5 — `pattern-005` (DI umbrella) + `pattern-006` (apps/docs page) + `pattern-007` (ledger flip).
    Add the `@standing-pattern: pattern-001` line to PR 1's description; `@standing-pattern: pattern-005`, `@standing-pattern: pattern-006`, `@standing-pattern: pattern-007` to PR 5's description. PRs 2 / 3 / 4 do not match a standing pattern; they take the standard COB self-audit path.

---

## Per-PR deliverables

This hand-off splits into **5 PRs** by entity-group responsibility:

- PR 1: Package scaffold + `MarketingAsset` + `AssetTag` + `AssetTagAssignment` entities + repositories + DI (substrate; matches `pattern-001`)
- PR 2: `AssetCollection` + `AssetCollectionMembership` + `IAssetCollectionService`
- PR 3: `AssetUsage` + `IAssetUsageService` + impact-of-change query
- PR 4: `BrandKit` + `BrandKitElement` + `IBrandKitService`
- PR 5: `IMarketingAssetService` (search + lifecycle) + DI umbrella + apps/docs + NOTICE + ledger flip (matches `pattern-005` + `pattern-006` + `pattern-007`)

PRs 1 → 2 → 3 → 4 are sequential (each depends on the previous merging into `main` so the package compiles cleanly with the new types layered in). PR 5 lands last. **Optional parallelization:** PR 3 (`AssetUsage`) and PR 4 (`BrandKit`) are functionally independent — both depend only on PR 1 (`MarketingAsset` exists) — so PR 4 can be opened in parallel with PR 3 if COB wants to fan out. PR 5's umbrella + docs assume PRs 1–4 have all merged.

---

### PR 1 — Package scaffold + `MarketingAsset` + `AssetTag` + `AssetTagAssignment` + repositories + DI

**Estimated effort:** ~2.5–3h
**Scope:** new package `blocks-docs-dam`; core asset + tag entities + IDs + enums + value object (`AssetRights`); 3 repositories + in-memory implementations; DI extension stub
**Standing pattern:** `pattern-001`
**Commit subject:** `feat(blocks-docs-dam): scaffold DAM package with MarketingAsset + AssetTag + AssetTagAssignment per Stage 02 §3.4`
**Branch:** `cob/blocks-docs-dam-scaffold`

#### Package skeleton

```
packages/blocks-docs-dam/
├── README.md                                       (cluster overview + apps/docs link)
├── NOTICE.md                                       (ResourceSpace BSD-3 attribution)
├── Sunfish.Blocks.DocsDam.csproj
├── Models/
│   ├── MarketingAssetId.cs                         (ULID record struct)
│   ├── AssetTagId.cs
│   ├── AssetTagAssignmentId.cs
│   ├── MarketingAsset.cs
│   ├── AssetTag.cs
│   ├── AssetTagAssignment.cs
│   ├── AssetRights.cs                              (value object)
│   ├── AssetKind.cs                                (enum)
│   ├── AssetTaxonomyKind.cs                        (enum)
│   └── AssetRightsOwnership.cs                     (enum)
├── Services/
│   ├── IMarketingAssetRepository.cs
│   ├── InMemoryMarketingAssetRepository.cs
│   ├── IAssetTagRepository.cs
│   ├── InMemoryAssetTagRepository.cs
│   ├── IAssetTagAssignmentRepository.cs
│   └── InMemoryAssetTagAssignmentRepository.cs
├── DependencyInjection/
│   └── BlocksDocsDamServiceCollectionExtensions.cs (stub — extended in PRs 2-5)
└── tests/
    └── Sunfish.Blocks.DocsDam.Tests/
        ├── Sunfish.Blocks.DocsDam.Tests.csproj
        ├── MarketingAssetRecordTests.cs
        ├── AssetTagRecordTests.cs
        ├── AssetRightsTests.cs
        ├── MarketingAssetRepositoryTests.cs
        ├── AssetTagRepositoryTests.cs
        └── AssetTagAssignmentRepositoryTests.cs
```

#### csproj dependencies

```xml
<ItemGroup>
  <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  <ProjectReference Include="..\foundation-events\Sunfish.Foundation.Events.csproj" />
  <ProjectReference Include="..\blocks-docs\Sunfish.Blocks.Docs.csproj" />
  <ProjectReference Include="..\blocks-docs-core\Sunfish.Blocks.DocsCore.csproj" />
  <ProjectReference Include="..\blocks-people-foundation\Sunfish.Blocks.People.Foundation.csproj" />
</ItemGroup>
```

Verify these csproj paths against the actual on-main sibling shape; mirror the slash conventions used by `blocks-docs-wiki` (PR-1 reference).

#### Add to solution

Add the new package + test project to `Sunfish.slnx`. Mirror the sibling `blocks-docs-wiki` (or `blocks-docs-core`) entries verbatim, replacing `Wiki` / `Core` with `Dam`.

#### Types — IDs

Three strong-id record structs, ULID-string pattern. Mirror `DocumentId`'s shape from `blocks-docs-core`:

```text
MarketingAssetId(string Value)
AssetTagId(string Value)
AssetTagAssignmentId(string Value)
```

Each has a static `New()` returning a fresh ULID, parameter validation rejecting null/empty/whitespace, and `override ToString() => Value`.

#### Types — Enums

`AssetKind`:
- `Image`, `Video`, `Audio`, `Pdf`, `Document`, `CopySnippet`, `Animation`

`AssetTaxonomyKind`:
- `Subject`, `Campaign`, `Season`, `Channel`, `Product`, `FreeForm`

`AssetRightsOwnership`:
- `Owned`, `Licensed`, `PublicDomain`, `CreativeCommons`

All three carry an `[EnumMember(Value = "kebab-case")]`-style stable string serialization per CRDT conventions §5 (in C# this is typically a `[JsonConverter(typeof(JsonStringEnumConverter))]` annotation on the property + the enum value names themselves being the wire form; verify the project's standard pattern via `blocks-docs-core/Models/DocumentType.cs`).

#### Types — Value object `AssetRights`

Per Stage 02 §3.4.1:

```text
AssetRights {
  Ownership: AssetRightsOwnership (required)
  LicenseName: string? (e.g. "CC-BY-4.0", "stock-photo-vendor-XYZ", or null when Owned/PublicDomain)
  UsageRestrictions: IReadOnlyList<string> (e.g. ["no-derivative", "attribution-required", ...]; can be empty)
}
```

C# record struct. Default value: `Ownership = Owned`, `LicenseName = null`, `UsageRestrictions = empty`. Equality is structural over all three fields; `UsageRestrictions` equality is order-sensitive (use a stable sort when constructing if order-insensitive comparison is needed downstream).

**Invariants enforced in constructor:**
- If `Ownership == Licensed`, `LicenseName` SHOULD be non-null (warn-only Tier-1 validation; not a hard reject, because legacy imports may have unknown license metadata).
- `UsageRestrictions` entries are normalized to lowercase + trimmed; duplicates removed.

#### Types — `MarketingAsset`

Per Stage 02 §3.4.1:

```text
MarketingAsset {
  Id: MarketingAssetId (required)
  TenantId: TenantId (required — from foundation-multitenancy; mirrors blocks-docs-core.Document.TenantId)
  DocumentId: DocumentId (required — FK to blocks-docs-core.Document; the underlying Document carries documentType = MarketingAsset)
  AssetKind: AssetKind (required)
  Title: string (required; max 200 chars)
  Description: string? (max 4000 chars)
  StorageRef: StorageRef (required — from blocks-docs.Models.StorageRef; binary-first)
  ThumbnailRef: StorageRef? (optional — null until thumbnails are generated, see §What this does NOT ship #1)
  AltText: string? (a11y; max 500 chars)
  DurationSeconds: int? (for video/audio; null otherwise)
  WidthPx: int? (for image/video)
  HeightPx: int? (for image/video)
  Rights: AssetRights (required; default ownership = Owned)
  LicenseExpiresAt: Instant? (e.g. stock photo with finite license term)
  Attribution: string? (e.g. "Photo by Jane Doe / Unsplash"; optional)

  // CRDT envelope per crdt-friendly-schema-conventions.md §3:
  Version: long (monotonic per-replica counter; Loro-managed)
  RevisionVector: IReadOnlyDictionary<string,long>? (Loro-managed)

  // Tombstone per §2:
  DeletedAtUtc: Instant?
  DeletedBy: PartyId?
  DeletedReason: string?

  // Audit:
  CreatedAtUtc: Instant (required)
  CreatedBy: PartyId? (nullable for system-created)
  UpdatedAtUtc: Instant (required)
  UpdatedBy: PartyId? (nullable for system-updated)
}
```

C# `public sealed record MarketingAsset` with `required` properties and `init` accessors throughout. Validation in `IMarketingAssetRepository.UpsertAsync(...)`:
- `Title` non-empty, length ≤ 200.
- `Description` length ≤ 4000 (if non-null).
- `AltText` length ≤ 500 (if non-null).
- `StorageRef` non-null (Tier-1 hard reject; DAM is binary-first per §3.4.1).
- `DurationSeconds`, `WidthPx`, `HeightPx`: if non-null, must be > 0; if `AssetKind` is `Image`, `DurationSeconds` SHOULD be null (warn only); if `AssetKind` is `Audio`, `WidthPx` / `HeightPx` SHOULD be null (warn only); if `AssetKind` is `Video`, all three may be non-null.

**Invariant: tenant + document consistency.** `MarketingAsset.TenantId` MUST equal the wrapped `Document.TenantId`. Enforce this in the **service layer** (PR 5's `IMarketingAssetService.RegisterAsync(...)` does a cross-fetch + assert); repository layer is read-only on FK validity (it does not query `IDocumentRepository`).

#### Types — `AssetTag`

Per Stage 02 §3.4.2:

```text
AssetTag {
  Id: AssetTagId
  TenantId: TenantId
  Name: string (required; max 80 chars)
  Slug: string (required; URL-safe; unique within (TenantId, ParentTagId) — repository-enforced)
  TaxonomyKind: AssetTaxonomyKind (required)
  ParentTagId: AssetTagId? (hierarchical tags; null = root)
  Description: string? (max 500 chars)

  Version: long
  RevisionVector: IReadOnlyDictionary<string,long>?
  DeletedAtUtc: Instant?
  DeletedBy: PartyId?
  CreatedAtUtc: Instant
  CreatedBy: PartyId?
  UpdatedAtUtc: Instant
  UpdatedBy: PartyId?
}
```

**Invariants:**
- `Slug` matches `^[a-z0-9]+(-[a-z0-9]+)*$` (kebab-case).
- `Slug` is unique within `(TenantId, ParentTagId)` — enforce in repository on `UpsertAsync`.
- Hierarchy depth ≤ 4 (enforced in repository — call `GetByIdAsync(parentTagId)` chain, count; reject if depth ≥ 4). Documented limitation; mirrors `blocks-docs-core.DocumentFolder.depth ≤ 8` discipline (tags are shallower because tag hierarchies practically rarely exceed 3 levels).
- Self-reference forbidden (`ParentTagId == Id` rejected).
- Cycle forbidden (walking the parent chain must terminate at null without revisiting `Id`).

#### Types — `AssetTagAssignment`

Per Stage 02 §3.4.2:

```text
AssetTagAssignment {
  Id: AssetTagAssignmentId
  TenantId: TenantId
  AssetId: MarketingAssetId (required — FK)
  TagId: AssetTagId (required — FK)
  Confidence: double? (0..1; null when human-assigned; reserved for future ML-tagging)
  AssignedBy: PartyId? (null when ML-assigned; reserved for future ML-tagging)
  AssignedAtUtc: Instant (required)
  RemovedAtUtc: Instant? (tombstone for the join row; "removing a tag" sets this, does not delete)
  RemovedBy: PartyId?
}
```

**Invariants:**
- Composite uniqueness: `(AssetId, TagId)` where `RemovedAtUtc IS NULL` — at most one *active* assignment of a tag to an asset. Enforced in repository on `UpsertAsync`: if an active row exists, reject; if a removed row exists, allow re-add (but as a new row with a fresh Id).
- `Confidence` (if non-null) in `[0.0, 1.0]` inclusive.
- At least one of `Confidence` (set + AssignedBy null = ML) or `AssignedBy` (set + Confidence null = human) must be specified. Both can be non-null (human override of an ML suggestion — the human "accepted" the suggestion).

#### Repository contracts

**`IMarketingAssetRepository`**:

```text
Task<MarketingAsset?> GetByIdAsync(MarketingAssetId id, CancellationToken ct = default)
Task<MarketingAsset?> GetByDocumentIdAsync(DocumentId documentId, CancellationToken ct = default)
Task<IReadOnlyList<MarketingAsset>> QueryByTenantAsync(TenantId tenantId, bool includeTombstoned = false, CancellationToken ct = default)
Task<IReadOnlyList<MarketingAsset>> QueryByKindAsync(TenantId tenantId, AssetKind kind, CancellationToken ct = default)
Task UpsertAsync(MarketingAsset asset, CancellationToken ct = default)
Task TombstoneAsync(MarketingAssetId id, PartyId by, string reason, CancellationToken ct = default)
```

**`IAssetTagRepository`**:

```text
Task<AssetTag?> GetByIdAsync(AssetTagId id, CancellationToken ct = default)
Task<AssetTag?> GetBySlugAsync(TenantId tenantId, string slug, AssetTagId? parentTagId, CancellationToken ct = default)
Task<IReadOnlyList<AssetTag>> QueryByTenantAsync(TenantId tenantId, bool includeTombstoned = false, CancellationToken ct = default)
Task<IReadOnlyList<AssetTag>> QueryChildrenAsync(AssetTagId parentTagId, CancellationToken ct = default)
Task<IReadOnlyList<AssetTagId>> ResolveBySlugsAsync(TenantId tenantId, IReadOnlyList<string> slugs, CancellationToken ct = default)
Task UpsertAsync(AssetTag tag, CancellationToken ct = default)
Task TombstoneAsync(AssetTagId id, PartyId by, CancellationToken ct = default)
```

**`IAssetTagAssignmentRepository`**:

```text
Task<AssetTagAssignment?> GetByIdAsync(AssetTagAssignmentId id, CancellationToken ct = default)
Task<IReadOnlyList<AssetTagAssignment>> QueryByAssetAsync(MarketingAssetId assetId, bool includeRemoved = false, CancellationToken ct = default)
Task<IReadOnlyList<AssetTagAssignment>> QueryByTagAsync(AssetTagId tagId, bool includeRemoved = false, CancellationToken ct = default)
Task<IReadOnlyList<MarketingAssetId>> QueryAssetIdsWithAllTagsAsync(TenantId tenantId, IReadOnlyList<AssetTagId> tagIds, CancellationToken ct = default)  // intersection — per §5.4
Task UpsertAsync(AssetTagAssignment assignment, CancellationToken ct = default)
Task RemoveAsync(AssetTagAssignmentId id, PartyId by, CancellationToken ct = default)  // sets RemovedAtUtc, not hard delete
```

#### In-memory implementations

Mirror the precedent from `blocks-docs-core/Services/InMemory*Repository.cs` and `blocks-financial-ar/Services/InMemoryInvoiceRepository.cs`:

- `ConcurrentDictionary<Id, Entity>` as the primary store.
- Secondary `ConcurrentDictionary` indexes for lookup-by-slug / lookup-by-document-id / etc.
- All mutations re-emit the entity (records are immutable).
- `QueryByTenantAsync` LINQ-filters across the dictionary; tolerable up to ~10k entities (we are explicitly in-memory v1).
- Tenant filtering is mandatory on every query — analyzer-enforced per `foundation-multitenancy` convention (verify your reads pass the analyzer; do not bypass).

#### DI extension stub

**`DependencyInjection/BlocksDocsDamServiceCollectionExtensions.cs`** — minimal stub; PR 5 fleshes it out:

```text
public static class BlocksDocsDamServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksDocsDam(this IServiceCollection services)
    {
        services.TryAddSingleton<IMarketingAssetRepository, InMemoryMarketingAssetRepository>();
        services.TryAddSingleton<IAssetTagRepository, InMemoryAssetTagRepository>();
        services.TryAddSingleton<IAssetTagAssignmentRepository, InMemoryAssetTagAssignmentRepository>();
        return services;
    }
}
```

(PR 5 extends this to register `IAssetCollectionRepository`, `IAssetUsageRepository`, `IBrandKitRepository`, all the services, the event publisher wiring, and the options class.)

#### Tests (PR 1)

`tests/MarketingAssetRecordTests.cs` (~6 tests):
- `Construction_PreservesAllRequiredFields`.
- `Title_MaxLength200_Accepted; Length201_Rejected`.
- `Description_NullAccepted_Length4000Accepted_Length4001Rejected`.
- `AltText_NullAccepted_Length500Accepted_Length501Rejected`.
- `StorageRef_NullRejected` (DAM is binary-first).
- `Equality_IsStructural`.

`tests/AssetTagRecordTests.cs` (~5 tests):
- `Construction_PreservesAllRequiredFields`.
- `Slug_MatchesKebabCaseOnly` (positive + negative cases).
- `Slug_AllowsHyphens; RejectsUnderscoresAndSpaces`.
- `ParentTagId_NullAllowed_SelfReferenceRejected`.

`tests/AssetRightsTests.cs` (~4 tests):
- `Default_IsOwnedWithNullLicense`.
- `UsageRestrictions_DuplicatesRemoved_AndLowercased`.
- `Licensed_WithNullLicenseName_WarnOnlyDoesNotThrow`.
- `Equality_IsStructural_OverAllThreeFields`.

`tests/MarketingAssetRepositoryTests.cs` (~7 tests):
- `UpsertAndGetById_RoundTrips`.
- `GetByDocumentId_ReturnsMatchingAsset`.
- `QueryByTenant_FiltersByTenantId; ExcludesTombstonedByDefault; IncludesWhenAsked`.
- `QueryByKind_FiltersByAssetKind`.
- `Tombstone_SetsDeletedAt_DeletedBy_DeletedReason`.
- `Upsert_RejectsInvalidTitleLength`.
- `Upsert_RejectsNullStorageRef`.

`tests/AssetTagRepositoryTests.cs` (~6 tests):
- `UpsertAndGetById_RoundTrips`.
- `GetBySlug_ReturnsMatchingTagInScope`.
- `Slug_UniqueWithinTenantAndParent` (collision rejected; same slug under different parent OK).
- `ResolveBySlugs_ReturnsResolvedIdsInRequestOrder` (slugs that don't resolve are silently dropped — caller checks count).
- `QueryChildren_ReturnsImmediateChildrenOnly`.
- `DepthLimit_4_Rejected` (a 5th-level tag is rejected).

`tests/AssetTagAssignmentRepositoryTests.cs` (~5 tests):
- `UpsertAndGetById_RoundTrips`.
- `QueryByAsset_ReturnsActiveAssignmentsByDefault; IncludesRemovedWhenAsked`.
- `QueryAssetIdsWithAllTags_ReturnsIntersection` (asset A has tags [x,y]; asset B has [x]; intersection on [x,y] returns only A).
- `CompositeUniqueness_DuplicateActiveAssignmentRejected; RemovedAssignmentDoesNotBlockReAdd`.
- `Confidence_OutOfRange_Rejected; ZeroAndOneAccepted`.

**Total new tests this PR: ~33.**

#### Verification

- `dotnet build` succeeds for the new package + test project; both added to `Sunfish.slnx`.
- `dotnet test packages/blocks-docs-dam/tests/` passes all ~33 tests.
- `grep -rln "Sunfish.Blocks.DocsDam" packages/blocks-docs-dam/` returns hits in every `.cs` file (namespace sanity).
- `grep -rln "Sunfish.Blocks.DocsCore" packages/blocks-docs-dam/` returns hits (cross-package using of `DocumentId`).
- `grep -rln "Sunfish.Blocks.Docs\b" packages/blocks-docs-dam/` returns hits (cross-package using of `StorageRef`).
- `dotnet build` of every downstream package (none yet — DAM has no consumers in v1) still succeeds.

#### Do NOT in this PR

- Do NOT introduce `IMarketingAssetService` (PR 5 ships it). The repositories are the only public surface in PR 1.
- Do NOT introduce `AssetCollection`, `AssetUsage`, `BrandKit` (PRs 2 / 3 / 4).
- Do NOT introduce thumbnail generation, ML tagging, or full-text-search infrastructure (deferred — see §What this does NOT ship).
- Do NOT cross-call `IDocumentCommandService` from inside the repository (FK validity is service-layer's job in PR 5).
- Do NOT modify `blocks-docs-core` or `blocks-docs`. DAM is a strict downstream consumer.

---

### PR 2 — `AssetCollection` + `AssetCollectionMembership` + `IAssetCollectionService`

**Estimated effort:** ~2–2.5h
**Scope:** collection entity + membership join + service for collection CRUD / membership management / reorder / archive
**Commit subject:** `feat(blocks-docs-dam): AssetCollection + AssetCollectionMembership + IAssetCollectionService per Stage 02 §3.4.3`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-docs-dam-collections`

#### New types — IDs + enum

`AssetCollectionId`, `AssetCollectionMembershipId` — ULID record structs.

`AssetCollectionKind` enum: `Campaign`, `BrandKitMember`, `Gallery`, `MoodBoard`, `AdHoc`.

#### `AssetCollection`

Per Stage 02 §3.4.3:

```text
AssetCollection {
  Id: AssetCollectionId
  TenantId: TenantId
  Name: string (required; max 200 chars)
  Description: string? (max 2000 chars)
  CollectionKind: AssetCollectionKind (required)
  CoverAssetId: MarketingAssetId? (FK to MarketingAsset; may be null)
  OwnerId: PartyId (required)
  ArchivedAt: Instant? (soft-archive; not hard-delete)
  ArchivedBy: PartyId?

  Version: long
  RevisionVector: IReadOnlyDictionary<string,long>?
  CreatedAtUtc: Instant
  CreatedBy: PartyId?
  UpdatedAtUtc: Instant
  UpdatedBy: PartyId?
}
```

**Invariants:**
- `Name` non-empty, length ≤ 200.
- `CoverAssetId` (if non-null) MUST reference an asset in the same tenant — service-layer enforced (PR 2's `IAssetCollectionService.SetCoverAsync(...)` does the cross-fetch).
- `ArchivedAt` and `ArchivedBy` are set together (both null or both non-null).

#### `AssetCollectionMembership`

Per Stage 02 §3.4.3:

```text
AssetCollectionMembership {
  Id: AssetCollectionMembershipId
  TenantId: TenantId
  CollectionId: AssetCollectionId (FK)
  AssetId: MarketingAssetId (FK)
  SortOrder: int (>= 0; renumbered on insert/move)
  AddedAtUtc: Instant
  AddedBy: PartyId?
  RemovedAtUtc: Instant?  (tombstone; per CRDT §4 append-only sub-collection discipline)
  RemovedBy: PartyId?
}
```

**Invariants:**
- Composite uniqueness: `(CollectionId, AssetId)` where `RemovedAtUtc IS NULL` — at most one active membership of an asset in a collection.
- `SortOrder >= 0`. Order maintained by service-layer reorder operation (not necessarily contiguous after removals — sparse OK).

#### Repository contracts

**`IAssetCollectionRepository`**:

```text
Task<AssetCollection?> GetByIdAsync(AssetCollectionId id, CancellationToken ct = default)
Task<IReadOnlyList<AssetCollection>> QueryByTenantAsync(TenantId tenantId, bool includeArchived = false, CancellationToken ct = default)
Task<IReadOnlyList<AssetCollection>> QueryByKindAsync(TenantId tenantId, AssetCollectionKind kind, CancellationToken ct = default)
Task<IReadOnlyList<AssetCollection>> QueryByOwnerAsync(PartyId ownerId, CancellationToken ct = default)
Task UpsertAsync(AssetCollection collection, CancellationToken ct = default)
Task ArchiveAsync(AssetCollectionId id, PartyId by, CancellationToken ct = default)
Task UnarchiveAsync(AssetCollectionId id, PartyId by, CancellationToken ct = default)
```

**`IAssetCollectionMembershipRepository`**:

```text
Task<AssetCollectionMembership?> GetByIdAsync(AssetCollectionMembershipId id, CancellationToken ct = default)
Task<IReadOnlyList<AssetCollectionMembership>> QueryByCollectionAsync(AssetCollectionId collectionId, bool includeRemoved = false, CancellationToken ct = default)
Task<IReadOnlyList<MarketingAssetId>> QueryAssetIdsInCollectionAsync(AssetCollectionId collectionId, CancellationToken ct = default)
Task<IReadOnlyList<AssetCollectionId>> QueryCollectionIdsForAssetAsync(MarketingAssetId assetId, CancellationToken ct = default)
Task UpsertAsync(AssetCollectionMembership membership, CancellationToken ct = default)
Task RemoveAsync(AssetCollectionMembershipId id, PartyId by, CancellationToken ct = default)
```

#### Service — `IAssetCollectionService`

```text
public interface IAssetCollectionService
{
    Task<CreateCollectionResult> CreateAsync(CreateCollectionCommand cmd, CancellationToken ct = default);
    Task<UpdateMetadataResult> UpdateMetadataAsync(AssetCollectionId id, string? name, string? description, PartyId actor, CancellationToken ct = default);
    Task<SetCoverResult> SetCoverAsync(AssetCollectionId id, MarketingAssetId? coverAssetId, PartyId actor, CancellationToken ct = default);
    Task<AddMemberResult> AddAssetAsync(AssetCollectionId id, MarketingAssetId assetId, int? sortOrder, PartyId actor, CancellationToken ct = default);
    Task<RemoveMemberResult> RemoveAssetAsync(AssetCollectionId id, MarketingAssetId assetId, PartyId actor, CancellationToken ct = default);
    Task<ReorderResult> ReorderAsync(AssetCollectionId id, IReadOnlyList<MarketingAssetId> orderedAssetIds, PartyId actor, CancellationToken ct = default);
    Task<ArchiveResult> ArchiveAsync(AssetCollectionId id, PartyId actor, CancellationToken ct = default);
    Task<UnarchiveResult> UnarchiveAsync(AssetCollectionId id, PartyId actor, CancellationToken ct = default);
    Task<AssetCollection?> GetAsync(AssetCollectionId id, CancellationToken ct = default);
    Task<IReadOnlyList<MarketingAsset>> GetAssetsInCollectionAsync(AssetCollectionId id, CancellationToken ct = default);
}
```

Each result type follows the established cluster pattern: `public sealed record <Name>Result(<Entity>? Entity, <Name>Error Error, string? Detail)`. Error enums name explicit failure modes:

- `CreateCollectionError`: `None`, `EmptyName`, `NameTooLong`, `InvalidKind`, `OwnerNotFound`.
- `SetCoverError`: `None`, `CollectionNotFound`, `AssetNotFound`, `AssetWrongTenant`, `CollectionArchived`.
- `AddMemberError`: `None`, `CollectionNotFound`, `AssetNotFound`, `AssetWrongTenant`, `AssetTombstoned`, `AlreadyMember`, `CollectionArchived`.
- `RemoveMemberError`: `None`, `CollectionNotFound`, `NotAMember`, `CollectionArchived`.
- `ReorderError`: `None`, `CollectionNotFound`, `MemberSetMismatch` (the ordered list must contain exactly the current active members — no adds, no drops), `CollectionArchived`.
- `ArchiveError`: `None`, `CollectionNotFound`, `AlreadyArchived`.
- `UnarchiveError`: `None`, `CollectionNotFound`, `NotArchived`.

**Implementation notes:**
- `AddAssetAsync`: if `sortOrder` is null, default to `max(existing) + 10` (sparse ordering allows insertion between two existing items without renumbering).
- `ReorderAsync`: rewrites `SortOrder` on the provided membership IDs in increments of 10 (mirrors common drag-and-drop ordering patterns; gives subsequent insertions space without re-walking the whole collection). Tier-1 validation: the input list MUST exactly match the current active members (no adds, no removes happen here — those go through `AddAssetAsync` / `RemoveAssetAsync`).
- `SetCoverAsync` with `coverAssetId = null` clears the cover.
- Tenant-isolation: every service method validates that the input IDs all resolve to entities in the same `TenantId` as the active tenant scope (analyzer-enforced via `foundation-multitenancy`).

**Event emission (PR 2 emits, PR 5 wires the canonical publisher):**
- `Dam.AssetCollectionPublished` is emitted by `CreateAsync` after the collection persists (and again by `UnarchiveAsync` — re-activation). The `assetCount` field is computed at emission time (0 on initial create; current active membership count on unarchive).

#### Idempotency-key catalog additions

| Key | Emitted by | Notes |
|---|---|---|
| `asset-collection-created:{collectionId}` | `CreateAsync` | Deterministic per-collection — a re-create is a no-op (skipped) |
| `asset-collection-published:{collectionId}:{publishAttemptAt}` | `CreateAsync` + `UnarchiveAsync` | Per-emission timestamp; consumer dedup window is event-bus's responsibility |
| `asset-collection-archived:{collectionId}:{archivedAt}` | `ArchiveAsync` | Same |

#### DI extension update

Extend `BlocksDocsDamServiceCollectionExtensions.AddBlocksDocsDam`:

```text
services.TryAddSingleton<IAssetCollectionRepository, InMemoryAssetCollectionRepository>();
services.TryAddSingleton<IAssetCollectionMembershipRepository, InMemoryAssetCollectionMembershipRepository>();
services.TryAddSingleton<IAssetCollectionService, AssetCollectionService>();
```

#### Tests (PR 2)

`tests/AssetCollectionRecordTests.cs` (~4 tests):
- `Construction_PreservesAllFields`.
- `Name_NonEmpty_MaxLength200`.
- `ArchivedAt_AndArchivedBy_BothSetOrBothNull`.
- `Equality_IsStructural`.

`tests/AssetCollectionMembershipRecordTests.cs` (~3 tests):
- `Construction_PreservesAllFields`.
- `SortOrder_AcceptsZero_RejectsNegative`.
- `RemovedAtUtc_AndRemovedBy_BothSetOrBothNull`.

`tests/AssetCollectionRepositoryTests.cs` (~5 tests):
- `UpsertAndGetById_RoundTrips`.
- `QueryByTenant_FiltersByTenant_ExcludesArchivedByDefault`.
- `QueryByKind_FiltersByCollectionKind`.
- `QueryByOwner_ReturnsCollectionsOwnedByParty`.
- `Archive_SetsArchivedAt_FiltersFromDefaultQueries; Unarchive_Clears`.

`tests/AssetCollectionMembershipRepositoryTests.cs` (~4 tests):
- `QueryByCollection_ReturnsActiveMembersByDefault; IncludesRemovedWhenAsked`.
- `QueryAssetIdsInCollection_ReturnsActiveAssetIdsOnly`.
- `QueryCollectionIdsForAsset_ReverseLookup`.
- `Remove_SetsRemovedAtAndBy_DoesNotHardDelete`.

`tests/AssetCollectionServiceTests.cs` (~10 tests):
- `Create_HappyPath_PersistsCollection_EmitsPublishedEvent`.
- `Create_RejectsEmptyName`.
- `SetCover_RejectsCoverFromOtherTenant`.
- `SetCover_AcceptsNullToClear`.
- `AddAsset_RejectsAlreadyMember; RejectsTombstonedAsset; RejectsArchivedCollection`.
- `AddAsset_DefaultSortOrderIsMaxPlus10`.
- `RemoveAsset_TombstonesJoinRow_DoesNotHardDelete`.
- `Reorder_HappyPath_RewritesSortOrders_InTensIncrements`.
- `Reorder_RejectsMemberSetMismatch` (input list missing one active member, or has an extra).
- `Archive_AlreadyArchived_ReturnsError`.

**Total new tests this PR: ~26.**

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests still pass.
- All ~26 new tests pass.

#### Do NOT in this PR

- Do NOT introduce `AssetUsage` (PR 3) or `BrandKit` (PR 4).
- Do NOT implement the §5.4 search algorithm (PR 5).
- Do NOT introduce CRDT-aware reorder conflict resolution (out of scope; reorder is last-writer-wins per Loro version-vector).
- Do NOT hard-delete any join rows. Removal is tombstone only.

---

### PR 3 — `AssetUsage` + `IAssetUsageService` + impact-of-change query

**Estimated effort:** ~1.5–2h
**Scope:** usage-tracking entity + service for recording / deactivating / querying usages; supports §5.4 `assetImpactOfChange` algorithm; pure projection over reads (no double-entry)
**Commit subject:** `feat(blocks-docs-dam): AssetUsage + IAssetUsageService — track-and-impact-of-change per Stage 02 §3.4.4 + §5.4`
**Depends on:** PR 2 merged (can parallelize with PR 4 — both depend only on PR 1)
**Branch:** `cob/blocks-docs-dam-usage`

#### New types — IDs + enum

`AssetUsageId` — ULID record struct.

`AssetConsumerKind` enum: `Campaign`, `WikiPage`, `Contract`, `Website`, `Listing`, `EmailTemplate`, `External`.

#### `AssetUsage`

Per Stage 02 §3.4.4:

```text
AssetUsage {
  Id: AssetUsageId
  TenantId: TenantId
  AssetId: MarketingAssetId (required — FK)
  ConsumerKind: AssetConsumerKind (required)
  ConsumerId: string? (null when ConsumerKind == External; otherwise the consumer's ID as a string — opaque to DAM)
  ConsumerLabel: string (required; human-readable; e.g. "Spring 2026 newsletter"; max 200 chars)
  FirstUsedAtUtc: Instant (required)
  LastUsedAtUtc: Instant (required; >= FirstUsedAt)
  IsActive: bool (required; set false when usage ends — e.g. campaign concludes, listing taken down)
  RecordedBy: PartyId? (the actor who created the usage row; null when system-recorded)
  Version: long
  RevisionVector: IReadOnlyDictionary<string,long>?
  CreatedAtUtc: Instant
  UpdatedAtUtc: Instant
}
```

**Invariants:**
- `ConsumerLabel` non-empty.
- `FirstUsedAtUtc <= LastUsedAtUtc`.
- If `ConsumerKind == External`, `ConsumerId` MUST be null (no internal ID for external consumer); otherwise `ConsumerId` SHOULD be non-null (warn-only — external integrations may legitimately leave it null while still pointing at a consumer of known kind).
- Idempotent recording: `(AssetId, ConsumerKind, ConsumerId, ConsumerLabel)` is the natural idempotency key for `RecordUsageAsync` — duplicate calls bump `LastUsedAtUtc` rather than creating a new row.

#### Repository — `IAssetUsageRepository`

```text
Task<AssetUsage?> GetByIdAsync(AssetUsageId id, CancellationToken ct = default)
Task<AssetUsage?> FindByNaturalKeyAsync(MarketingAssetId assetId, AssetConsumerKind consumerKind, string? consumerId, string consumerLabel, CancellationToken ct = default)
Task<IReadOnlyList<AssetUsage>> QueryByAssetAsync(MarketingAssetId assetId, bool activeOnly = true, CancellationToken ct = default)
Task<IReadOnlyList<AssetUsage>> QueryByConsumerAsync(AssetConsumerKind consumerKind, string? consumerId, CancellationToken ct = default)
Task<IReadOnlyList<MarketingAssetId>> QueryAssetIdsUsedAfterAsync(TenantId tenantId, Instant cutoff, CancellationToken ct = default)
Task UpsertAsync(AssetUsage usage, CancellationToken ct = default)
```

The `QueryAssetIdsUsedAfterAsync` query is the read-model that backs the §5.4 `findAssets(... usedInLast: ...)` filter.

#### Service — `IAssetUsageService`

```text
public interface IAssetUsageService
{
    Task<RecordUsageResult> RecordUsageAsync(RecordUsageCommand cmd, CancellationToken ct = default);
    Task<MarkUsageInactiveResult> MarkUsageInactiveAsync(AssetUsageId id, PartyId actor, CancellationToken ct = default);
    Task<MarkUsageInactiveResult> MarkUsageInactiveByConsumerAsync(AssetConsumerKind consumerKind, string consumerId, PartyId actor, CancellationToken ct = default);
    Task<IReadOnlyList<AssetUsage>> QueryUsagesForAssetAsync(MarketingAssetId assetId, bool activeOnly = true, CancellationToken ct = default);
    Task<AssetImpactReport> GetImpactOfChangeAsync(MarketingAssetId assetId, CancellationToken ct = default);
}

public sealed record RecordUsageCommand(
    MarketingAssetId AssetId,
    AssetConsumerKind ConsumerKind,
    string? ConsumerId,
    string ConsumerLabel,
    PartyId? RecordedBy);

public sealed record AssetImpactReport(
    MarketingAssetId AssetId,
    int ActiveUsageCount,
    int TotalUsageCount,
    IReadOnlyList<AssetImpactEntry> Entries);

public sealed record AssetImpactEntry(
    AssetConsumerKind ConsumerKind,
    string? ConsumerId,
    string ConsumerLabel,
    Instant LastUsedAtUtc,
    bool IsActive);
```

`AssetImpactReport` materializes the §5.4 pseudocode result:

```text
assetImpactOfChange(assetId):
  usages = repo.findActiveUsages(assetId)
  return usages.map(u => ({ consumer: u.consumerKind, consumerLabel, lastUsedAt }))
```

We extend slightly to include both active and inactive in the report (with a flag) because the property-manager use case "is this logo safe to retire?" wants both counts: active is the "this is currently in use" answer, total is the historical footprint.

**Error enums:**
- `RecordUsageError`: `None`, `AssetNotFound`, `AssetTombstoned`, `EmptyConsumerLabel`, `InvalidConsumerKind`.
- `MarkUsageInactiveError`: `None`, `UsageNotFound`, `AlreadyInactive`.

**Implementation notes:**
- `RecordUsageAsync` is idempotent via `FindByNaturalKeyAsync` — if a matching row exists, update `LastUsedAtUtc` to `now` + reactivate (`IsActive = true`); else insert. This naturally handles the "campaign keeps referencing this asset" case without unbounded row growth.
- `MarkUsageInactiveByConsumerAsync` deactivates ALL usages for a given (`consumerKind`, `consumerId`) tuple — the "campaign just ended" sweep operation.

**Event emission:**
- `Dam.AssetUsageRecorded` is emitted on first-insert path of `RecordUsageAsync` (NOT on the `LastUsedAt` bump — that's noise). Consumers (reports) get a single notification per *new* usage instance, not per touch.

#### Idempotency-key catalog additions

| Key | Emitted by | Notes |
|---|---|---|
| `asset-usage-recorded:{usageId}` | `RecordUsageAsync` (insert path only) | Per-usage; downstream dedup window matches the event bus's standard |
| `asset-usage-deactivated:{usageId}` | `MarkUsageInactiveAsync` | (Optional event — only if XO consensus emerges that consumers care; otherwise omit) |

#### DI extension update

Extend `BlocksDocsDamServiceCollectionExtensions.AddBlocksDocsDam`:

```text
services.TryAddSingleton<IAssetUsageRepository, InMemoryAssetUsageRepository>();
services.TryAddSingleton<IAssetUsageService, AssetUsageService>();
```

#### Tests (PR 3)

`tests/AssetUsageRecordTests.cs` (~4 tests):
- `Construction_PreservesAllFields`.
- `LastUsedAtUtc_GreaterThanOrEqualToFirstUsedAtUtc_Enforced`.
- `ConsumerLabel_NonEmpty_MaxLength200`.
- `External_ConsumerKind_RequiresNullConsumerId_WarnOnly`.

`tests/AssetUsageRepositoryTests.cs` (~5 tests):
- `UpsertAndGetById_RoundTrips`.
- `FindByNaturalKey_ReturnsExistingRowForSameTuple`.
- `QueryByAsset_ActiveOnly_DefaultTrue; FalseIncludesInactive`.
- `QueryByConsumer_FiltersByKindAndConsumerId`.
- `QueryAssetIdsUsedAfter_FiltersByLastUsedAtCutoff`.

`tests/AssetUsageServiceTests.cs` (~9 tests):
- `RecordUsage_HappyPath_InsertsAndEmitsEvent`.
- `RecordUsage_DuplicateNaturalKey_BumpsLastUsedAt_NoNewEvent`.
- `RecordUsage_RejectsTombstonedAsset`.
- `RecordUsage_RejectsEmptyConsumerLabel`.
- `MarkUsageInactive_HappyPath_SetsIsActiveFalse`.
- `MarkUsageInactive_AlreadyInactive_ReturnsError`.
- `MarkUsageInactiveByConsumer_DeactivatesAllUsagesForTuple`.
- `GetImpactOfChange_ReturnsActiveAndTotalCounts_WithEntries`.
- `GetImpactOfChange_NoUsages_ReturnsEmptyReport`.

**Total new tests this PR: ~18.**

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- All ~18 new tests pass.

#### Do NOT in this PR

- Do NOT introduce a usage-count materialized view or balance-cache table (per the v1 / Phase 1 §6.2 discipline applied to financial — `QueryAssetIdsUsedAfter` is direct query). If the in-memory implementation becomes slow on >10k usage rows, a follow-on intake can add caching; not v1.
- Do NOT couple `IAssetUsageService` to specific consumer types (no `IListingPhotoUsageRecorder` or similar). Consumers register their own usages via the generic `RecordUsageAsync(consumerKind, consumerId, consumerLabel)` surface.
- Do NOT auto-emit `Dam.AssetUsageRecorded` on every `LastUsedAt` bump — only on insert.

---

### PR 4 — `BrandKit` + `BrandKitElement` + `IBrandKitService`

**Estimated effort:** ~2–2.5h
**Scope:** brand-kit entity + member elements (logo / color / font / voice-note / tagline / icon-set); service for kit CRUD, element CRUD, activation
**Commit subject:** `feat(blocks-docs-dam): BrandKit + BrandKitElement + IBrandKitService per Stage 02 §3.4.5`
**Depends on:** PR 1 merged (functionally independent of PR 2 + PR 3; can parallelize with PR 3)
**Branch:** `cob/blocks-docs-dam-brand-kit`

#### New types — IDs + enum

`BrandKitId`, `BrandKitElementId` — ULID record structs.

`BrandKitElementKind` enum: `Logo`, `Color`, `Font`, `VoiceNote`, `Tagline`, `IconSet`.

#### `BrandKit`

Per Stage 02 §3.4.5:

```text
BrandKit {
  Id: BrandKitId
  TenantId: TenantId
  Name: string (required; max 200 chars; e.g. "Sunfish Properties 2026")
  Description: string? (max 2000 chars)
  IsActive: bool (required; default false; activation handled via IBrandKitService.ActivateAsync)
  EffectiveFrom: Instant (required)
  EffectiveUntil: Instant? (null = no end date; non-null = deactivation timestamp)
  Version: long
  RevisionVector: IReadOnlyDictionary<string,long>?
  CreatedAtUtc: Instant
  CreatedBy: PartyId?
  UpdatedAtUtc: Instant
  UpdatedBy: PartyId?
}
```

**Invariants:**
- `Name` non-empty, length ≤ 200.
- `EffectiveFrom <= EffectiveUntil` (if both set).
- `IsActive` is a *derived projection* logically — but stored materialized for query efficiency. The service-layer keeps `IsActive` consistent with `EffectiveFrom <= now < (EffectiveUntil ?? +∞)`; concurrent activate/deactivate writes resolve via Loro version-vector LWW.
- Multiple active brand-kits per tenant are allowed in v1 (the consumer picks one — e.g. "the active kit for property X is the one with the most-recent EffectiveFrom"). Future intake may enforce single-active; not v1.

#### `BrandKitElement`

Per Stage 02 §3.4.5:

```text
BrandKitElement {
  Id: BrandKitElementId
  TenantId: TenantId
  BrandKitId: BrandKitId (required — FK)
  ElementKind: BrandKitElementKind (required)
  Name: string (required; max 200 chars; e.g. "Primary Logo", "Brand Blue", "Heading Font")
  AssetId: MarketingAssetId? (FK to MarketingAsset — required if ElementKind ∈ {Logo, IconSet}; nullable for Color/Font/VoiceNote/Tagline)
  ColorHex: string? (required if ElementKind == Color; 6 or 8 hex chars without "#")
  ColorName: string? (optional readable name; e.g. "Brand Blue")
  FontFamily: string? (required if ElementKind == Font; e.g. "Inter", "Source Serif Pro")
  FontWeights: IReadOnlyList<int>? (optional weight set; e.g. [400, 600, 700])
  TextContent: string? (required if ElementKind ∈ {VoiceNote, Tagline})
  SortOrder: int (>= 0)
  IsPrimary: bool (default false; only one element per ElementKind can be IsPrimary = true within a BrandKit — service-layer enforced)

  CreatedAtUtc: Instant
  UpdatedAtUtc: Instant
}
```

**Element-kind invariants (Tier-1 validation in `BrandKitElement` constructor + repository upsert):**
- `Logo` / `IconSet` → `AssetId` MUST be non-null; `ColorHex` / `FontFamily` / `TextContent` MUST be null.
- `Color` → `ColorHex` MUST be non-null, matches `^[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$`; `AssetId` / `FontFamily` / `TextContent` MUST be null.
- `Font` → `FontFamily` MUST be non-null; `AssetId` MAY be non-null (font file blob); `ColorHex` / `TextContent` MUST be null.
- `VoiceNote` / `Tagline` → `TextContent` MUST be non-null; all others MUST be null.
- `IsPrimary = true` invariant — at most one `IsPrimary` element per `(BrandKitId, ElementKind)` — enforced in `IBrandKitService.AddElementAsync` / `UpdateElementAsync` (repository checks).

#### Repository contracts

**`IBrandKitRepository`**:

```text
Task<BrandKit?> GetByIdAsync(BrandKitId id, CancellationToken ct = default)
Task<IReadOnlyList<BrandKit>> QueryByTenantAsync(TenantId tenantId, CancellationToken ct = default)
Task<IReadOnlyList<BrandKit>> QueryActiveAsOfAsync(TenantId tenantId, Instant asOf, CancellationToken ct = default)
Task UpsertAsync(BrandKit kit, CancellationToken ct = default)
```

**`IBrandKitElementRepository`**:

```text
Task<BrandKitElement?> GetByIdAsync(BrandKitElementId id, CancellationToken ct = default)
Task<IReadOnlyList<BrandKitElement>> QueryByKitAsync(BrandKitId kitId, CancellationToken ct = default)
Task<IReadOnlyList<BrandKitElement>> QueryByKitAndKindAsync(BrandKitId kitId, BrandKitElementKind kind, CancellationToken ct = default)
Task<BrandKitElement?> QueryPrimaryAsync(BrandKitId kitId, BrandKitElementKind kind, CancellationToken ct = default)
Task UpsertAsync(BrandKitElement element, CancellationToken ct = default)
Task RemoveAsync(BrandKitElementId id, CancellationToken ct = default)
```

#### Service — `IBrandKitService`

```text
public interface IBrandKitService
{
    Task<CreateBrandKitResult> CreateAsync(string name, string? description, Instant effectiveFrom, PartyId actor, CancellationToken ct = default);
    Task<UpdateBrandKitResult> UpdateMetadataAsync(BrandKitId id, string? name, string? description, PartyId actor, CancellationToken ct = default);
    Task<ActivateBrandKitResult> ActivateAsync(BrandKitId id, PartyId actor, CancellationToken ct = default);
    Task<DeactivateBrandKitResult> DeactivateAsync(BrandKitId id, Instant? effectiveUntil, PartyId actor, CancellationToken ct = default);
    Task<BrandKit?> GetAsync(BrandKitId id, CancellationToken ct = default);
    Task<IReadOnlyList<BrandKit>> GetActiveAsOfAsync(Instant asOf, CancellationToken ct = default);

    Task<AddElementResult> AddElementAsync(BrandKitId kitId, AddElementCommand cmd, PartyId actor, CancellationToken ct = default);
    Task<UpdateElementResult> UpdateElementAsync(BrandKitElementId elementId, UpdateElementCommand cmd, PartyId actor, CancellationToken ct = default);
    Task<RemoveElementResult> RemoveElementAsync(BrandKitElementId elementId, PartyId actor, CancellationToken ct = default);
    Task<SetPrimaryResult> SetPrimaryAsync(BrandKitElementId elementId, PartyId actor, CancellationToken ct = default);
    Task<IReadOnlyList<BrandKitElement>> GetElementsAsync(BrandKitId kitId, CancellationToken ct = default);
}
```

**Error enums:**
- `CreateBrandKitError`: `None`, `EmptyName`, `NameTooLong`, `InvalidEffectiveFrom`.
- `ActivateBrandKitError`: `None`, `KitNotFound`, `AlreadyActive`.
- `DeactivateBrandKitError`: `None`, `KitNotFound`, `AlreadyInactive`, `EffectiveUntilBeforeEffectiveFrom`.
- `AddElementError`: `None`, `KitNotFound`, `KitDeactivated`, `InvalidShapeForKind`, `AssetNotFound`, `AssetWrongTenant`.
- `SetPrimaryError`: `None`, `ElementNotFound`, `ConflictingPrimaryDemoted` (informational — when setting a new primary, the prior primary in the same `(kitId, kind)` is demoted; the result still returns `None` but `Detail` describes the demotion).

**Implementation notes:**
- `ActivateAsync`: sets `IsActive = true`; if `EffectiveFrom > now`, the activation is *scheduled* (the kit becomes effective at `EffectiveFrom`); if `EffectiveFrom <= now`, immediate. `GetActiveAsOfAsync(now)` is the source-of-truth query — `IsActive` is a denormalized hint.
- `DeactivateAsync`: sets `EffectiveUntil = (effectiveUntil ?? now)`; if `effectiveUntil` is in the past, the deactivation backdates (used for migration / "this kit was retired Q3").
- `SetPrimaryAsync`: atomically demotes the existing primary in the same `(kitId, kind)` (if any) and promotes the target. Reports the demotion in `Detail`.

**Event emission:**
- `Dam.BrandKitActivated` emitted on `ActivateAsync` happy path with `{ brandKitId, effectiveFrom }` payload.

#### Idempotency-key catalog additions

| Key | Emitted by | Notes |
|---|---|---|
| `brand-kit-created:{brandKitId}` | `CreateAsync` | Per-kit |
| `brand-kit-activated:{brandKitId}:{effectiveFrom}` | `ActivateAsync` | Per-effective-from instant; idempotent re-activation does not double-emit |
| `brand-kit-deactivated:{brandKitId}:{effectiveUntil}` | `DeactivateAsync` | Optional event; ship only if a downstream consumer needs it |

#### DI extension update

Extend `BlocksDocsDamServiceCollectionExtensions.AddBlocksDocsDam`:

```text
services.TryAddSingleton<IBrandKitRepository, InMemoryBrandKitRepository>();
services.TryAddSingleton<IBrandKitElementRepository, InMemoryBrandKitElementRepository>();
services.TryAddSingleton<IBrandKitService, BrandKitService>();
```

#### Tests (PR 4)

`tests/BrandKitRecordTests.cs` (~4 tests):
- `Construction_PreservesAllFields`.
- `EffectiveFrom_BeforeEffectiveUntil_Enforced`.
- `Name_NonEmpty_MaxLength200`.
- `Equality_IsStructural`.

`tests/BrandKitElementRecordTests.cs` (~7 tests; one per element-kind shape):
- `Logo_RequiresAssetId; RejectsColorHexAndFontFamily`.
- `IconSet_RequiresAssetId`.
- `Color_RequiresValidHex; AcceptsRRGGBBAndRRGGBBAA; Rejects7Chars`.
- `Font_RequiresFontFamily; AllowsOptionalAssetForFontFile`.
- `VoiceNote_RequiresTextContent`.
- `Tagline_RequiresTextContent`.
- `ColorName_OptionalEvenIfColor`.

`tests/BrandKitRepositoryTests.cs` (~4 tests):
- `UpsertAndGetById_RoundTrips`.
- `QueryByTenant_FiltersByTenant`.
- `QueryActiveAsOf_RespectsEffectiveFromAndUntil`.
- `Active_RequiresIsActiveTrue_AND_NowInRange`.

`tests/BrandKitElementRepositoryTests.cs` (~4 tests):
- `QueryByKit_ReturnsElementsForKit`.
- `QueryByKitAndKind_FiltersByKind`.
- `QueryPrimary_ReturnsTheIsPrimaryElement_OrNullIfNone`.
- `PrimaryUniqueness_TwoIsPrimaryForSameKindRejectedAtUpsert`.

`tests/BrandKitServiceTests.cs` (~12 tests):
- `Create_HappyPath_PersistsKit`.
- `Create_RejectsEmptyName; RejectsNameTooLong`.
- `Activate_HappyPath_EmitsActivatedEvent`.
- `Activate_AlreadyActive_ReturnsError`.
- `Deactivate_HappyPath_SetsEffectiveUntil`.
- `Deactivate_EffectiveUntilBeforeFrom_ReturnsError`.
- `AddElement_LogoRequiresAssetId_RejectsMissing`.
- `AddElement_ColorAcceptsValidHex_RejectsInvalid`.
- `AddElement_RejectsAssetFromOtherTenant`.
- `SetPrimary_DemotesExistingPrimary_PromotesTarget`.
- `GetActiveAsOf_ReturnsKitWhereEffectiveFromLeNowLtEffectiveUntil`.
- `RemoveElement_HardDeletesJoinRow` (brand-kit-element rows are operational metadata, not audit-bearing — hard-delete is acceptable; the parent kit's audit trail captures the change).

**Total new tests this PR: ~31.**

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 + PR 3 tests pass.
- All ~31 new tests pass.

#### Do NOT in this PR

- Do NOT enforce single-active-brand-kit-per-tenant. Multiple active kits is intentional in v1 (consumer chooses).
- Do NOT introduce a brand-compliance acknowledgment workflow (per §What this does NOT ship #6).
- Do NOT auto-pull thumbnails for logo / icon-set elements. Thumbnails come from the wrapped `MarketingAsset.thumbnailRef` (which may be null — see §What this does NOT ship #1).

---

### PR 5 — `IMarketingAssetService` (search + lifecycle) + DI umbrella + apps/docs + NOTICE + ledger flip

**Estimated effort:** ~2–3h
**Scope:** the main asset-lifecycle service (register / update / tag-manage / search per §5.4 / impact-of-change via `IAssetUsageService`); DI umbrella; apps/docs page; NOTICE.md ResourceSpace BSD-3 attribution; ledger flip
**Standing patterns:** `pattern-005` (DI umbrella) + `pattern-006` (apps/docs new page) + `pattern-007` (ledger flip)
**Commit subject:** `feat(blocks-docs-dam): IMarketingAssetService + DI umbrella + apps/docs page + ResourceSpace attribution; ledger flip W#73 to built`
**Depends on:** PRs 1–4 merged
**Branch:** `cob/blocks-docs-dam-service-and-ledger`

#### `IMarketingAssetService`

```text
public interface IMarketingAssetService
{
    // Lifecycle
    Task<RegisterAssetResult> RegisterAsync(RegisterAssetCommand cmd, CancellationToken ct = default);
    Task<UpdateMetadataResult> UpdateMetadataAsync(MarketingAssetId id, UpdateAssetMetadataCommand cmd, PartyId actor, CancellationToken ct = default);
    Task<SetRightsResult> SetRightsAsync(MarketingAssetId id, AssetRights rights, Instant? licenseExpiresAt, PartyId actor, CancellationToken ct = default);
    Task<SetThumbnailResult> SetThumbnailAsync(MarketingAssetId id, StorageRef? thumbnailRef, PartyId actor, CancellationToken ct = default);
    Task<TombstoneAssetResult> TombstoneAsync(MarketingAssetId id, string reason, PartyId actor, CancellationToken ct = default);

    // Tags
    Task<AddTagResult> AddTagAsync(MarketingAssetId assetId, AssetTagId tagId, PartyId actor, CancellationToken ct = default);
    Task<RemoveTagResult> RemoveTagAsync(MarketingAssetId assetId, AssetTagId tagId, PartyId actor, CancellationToken ct = default);

    // Reads
    Task<MarketingAsset?> GetAsync(MarketingAssetId id, CancellationToken ct = default);
    Task<AssetSearchResult> SearchAsync(AssetSearchQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<MarketingAsset>> GetByCollectionAsync(AssetCollectionId collectionId, CancellationToken ct = default);
    Task<AssetImpactReport> GetImpactOfChangeAsync(MarketingAssetId assetId, CancellationToken ct = default);
}

public sealed record RegisterAssetCommand(
    DocumentId DocumentId,
    AssetKind AssetKind,
    string Title,
    string? Description,
    StorageRef StorageRef,
    StorageRef? ThumbnailRef,
    string? AltText,
    int? DurationSeconds,
    int? WidthPx,
    int? HeightPx,
    AssetRights Rights,
    Instant? LicenseExpiresAt,
    string? Attribution,
    PartyId? RegisteredBy);

public sealed record UpdateAssetMetadataCommand(
    string? Title,                   // null = no change; else replaces
    string? Description,             // null = no change; pass "" to clear
    string? AltText,                 // null = no change; pass "" to clear
    int? DurationSeconds,
    int? WidthPx,
    int? HeightPx,
    string? Attribution);

public sealed record AssetSearchQuery(
    TenantId TenantId,
    string? FullTextQuery,           // matches title / description / altText (substring v1; FTS5 follow-on)
    IReadOnlyList<string>? TagSlugs, // intersection match (per §5.4 — "all tags")
    AssetCollectionId? CollectionId, // filter to members of this collection
    AssetKind? AssetKind,
    Duration? UsedInLast,            // e.g. NodaTime.Duration.FromDays(30) — uses IAssetUsageRepository.QueryAssetIdsUsedAfterAsync
    int? Limit,                      // default 100 per §5.4
    int? Offset);                    // for pagination; default 0

public sealed record AssetSearchResult(
    IReadOnlyList<MarketingAsset> Assets,
    int TotalCount,                  // pre-pagination total
    bool HasMore);
```

**Error enums:**
- `RegisterAssetError`: `None`, `DocumentNotFound`, `DocumentWrongType` (must be `DocumentType.MarketingAsset`), `DocumentWrongTenant`, `EmptyTitle`, `TitleTooLong`, `NullStorageRef`, `InvalidDimensionsForKind`.
- `UpdateMetadataError`: `None`, `AssetNotFound`, `AssetTombstoned`, `InvalidShape`.
- `SetRightsError`: `None`, `AssetNotFound`, `AssetTombstoned`, `LicenseExpiresAtInPast` (warn-only; this hand-off accepts past expiry for migration purposes — flag in `Detail`).
- `SetThumbnailError`: `None`, `AssetNotFound`, `AssetTombstoned`.
- `TombstoneAssetError`: `None`, `AssetNotFound`, `AlreadyTombstoned`.
- `AddTagError`: `None`, `AssetNotFound`, `AssetTombstoned`, `TagNotFound`, `TagWrongTenant`, `AlreadyAssigned`.
- `RemoveTagError`: `None`, `AssetNotFound`, `TagNotAssigned`.

**`SearchAsync` algorithm — per Stage 02 §5.4** (in-memory v1 — substring matching; FTS5 follow-on):

```text
search(query):
  candidates = await marketingAssetRepo.QueryByTenant(query.TenantId)
                                       .Where(a => !a.Tombstoned)
  if query.FullTextQuery is not null:
    candidates = candidates.Where(a =>
      contains(a.Title, query.FullTextQuery)
      OR contains(a.Description, query.FullTextQuery)
      OR contains(a.AltText, query.FullTextQuery))   // all case-insensitive
  if query.TagSlugs is not empty:
    tagIds = await tagRepo.ResolveBySlugs(query.TenantId, query.TagSlugs)
    if tagIds.Count != query.TagSlugs.Count:
      // some slugs didn't resolve; treat as empty-result (intersection requires ALL)
      return AssetSearchResult(empty, 0, false)
    assetIdsWithAllTags = await tagAssignmentRepo.QueryAssetIdsWithAllTags(query.TenantId, tagIds)
    candidates = candidates.Where(a => assetIdsWithAllTags.Contains(a.Id))
  if query.CollectionId is not null:
    assetIdsInCollection = await collectionMembershipRepo.QueryAssetIdsInCollection(query.CollectionId)
    candidates = candidates.Where(a => assetIdsInCollection.Contains(a.Id))
  if query.AssetKind is not null:
    candidates = candidates.Where(a => a.AssetKind == query.AssetKind)
  if query.UsedInLast is not null:
    cutoff = systemClock.GetCurrentInstant() - query.UsedInLast
    assetIdsUsedAfter = await usageRepo.QueryAssetIdsUsedAfter(query.TenantId, cutoff)
    candidates = candidates.Where(a => assetIdsUsedAfter.Contains(a.Id))

  ordered = candidates.OrderByDescending(a => a.UpdatedAtUtc).ToList()
  total = ordered.Count
  offset = query.Offset ?? 0
  limit = query.Limit ?? 100
  page = ordered.Skip(offset).Take(limit).ToList()
  hasMore = total > offset + page.Count
  return AssetSearchResult(page, total, hasMore)
```

The ordering (`UpdatedAtUtc DESC`) gives "recently changed first" — a sensible v1 default. A future intake may add relevance ranking or user-configurable sort.

**`RegisterAsync` algorithm:**

```text
register(cmd):
  // Phase 1 — preconditions
  doc = await documentRepo.GetById(cmd.DocumentId)
  if doc == null: return Err(DocumentNotFound)
  if doc.DocumentType != DocumentType.MarketingAsset: return Err(DocumentWrongType)
  if doc.TenantId != currentTenantId: return Err(DocumentWrongTenant)
  if cmd.Title is empty: return Err(EmptyTitle)
  if cmd.Title.Length > 200: return Err(TitleTooLong)
  if cmd.StorageRef is null: return Err(NullStorageRef)
  validateDimensions(cmd.AssetKind, cmd.DurationSeconds, cmd.WidthPx, cmd.HeightPx)  // warn-only for image-with-DurationSeconds etc.

  // Phase 2 — idempotency check (DocumentId is the natural key for register)
  existing = await assetRepo.GetByDocumentId(cmd.DocumentId)
  if existing is not null:
    if existing.Tombstoned: return Err(DocumentNotFound)   // refuse re-registration of a tombstoned record's document
    return Ok(existing)                                     // idempotent: same document → same asset

  // Phase 3 — persist
  asset = new MarketingAsset {
    Id = MarketingAssetId.New(),
    TenantId = currentTenantId,
    DocumentId = cmd.DocumentId,
    AssetKind = cmd.AssetKind,
    Title = cmd.Title,
    Description = cmd.Description,
    StorageRef = cmd.StorageRef,
    ThumbnailRef = cmd.ThumbnailRef,
    AltText = cmd.AltText,
    DurationSeconds = cmd.DurationSeconds,
    WidthPx = cmd.WidthPx,
    HeightPx = cmd.HeightPx,
    Rights = cmd.Rights,
    LicenseExpiresAt = cmd.LicenseExpiresAt,
    Attribution = cmd.Attribution,
    Version = 1,
    CreatedAtUtc = now,
    CreatedBy = cmd.RegisteredBy,
    UpdatedAtUtc = now,
    UpdatedBy = cmd.RegisteredBy,
  }
  await assetRepo.Upsert(asset)
  await events.Publish(new MarketingAssetRegisteredEvent(asset.Id, asset.DocumentId, asset.TenantId, asset.AssetKind, now))
  return Ok(asset)
```

**`TombstoneAsync` algorithm:**

```text
tombstone(id, reason, actor):
  asset = await assetRepo.GetById(id)
  if asset == null: return Err(AssetNotFound)
  if asset.DeletedAtUtc is not null: return Err(AlreadyTombstoned)

  tombstoned = asset with {
    DeletedAtUtc = now,
    DeletedBy = actor,
    DeletedReason = reason,
    UpdatedAtUtc = now,
    UpdatedBy = actor,
    Version = asset.Version + 1,
  }
  await assetRepo.Upsert(tombstoned)
  await events.Publish(new MarketingAssetTombstonedEvent(tombstoned.Id, reason, actor, now))
  return Ok(tombstoned)
```

Tombstoning does NOT cascade into `AssetTagAssignment`, `AssetCollectionMembership`, `AssetUsage`, `BrandKitElement` — those rows remain queryable for audit (with the asset showing as tombstoned in joined queries). A future intake can introduce a cleanup sweep if needed; v1 preserves all references for licensing audit.

#### DI umbrella (final shape — extends PRs 1–4 stubs)

**`BlocksDocsDamServiceCollectionExtensions.AddBlocksDocsDam(...)`** (final):

```text
public static IServiceCollection AddBlocksDocsDam(
    this IServiceCollection services,
    Action<BlocksDocsDamOptions>? configure = null)
{
    var options = new BlocksDocsDamOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);

    // Repositories
    services.TryAddSingleton<IMarketingAssetRepository, InMemoryMarketingAssetRepository>();
    services.TryAddSingleton<IAssetTagRepository, InMemoryAssetTagRepository>();
    services.TryAddSingleton<IAssetTagAssignmentRepository, InMemoryAssetTagAssignmentRepository>();
    services.TryAddSingleton<IAssetCollectionRepository, InMemoryAssetCollectionRepository>();
    services.TryAddSingleton<IAssetCollectionMembershipRepository, InMemoryAssetCollectionMembershipRepository>();
    services.TryAddSingleton<IAssetUsageRepository, InMemoryAssetUsageRepository>();
    services.TryAddSingleton<IBrandKitRepository, InMemoryBrandKitRepository>();
    services.TryAddSingleton<IBrandKitElementRepository, InMemoryBrandKitElementRepository>();

    // Services
    services.TryAddSingleton<IMarketingAssetService, MarketingAssetService>();
    services.TryAddSingleton<IAssetCollectionService, AssetCollectionService>();
    services.TryAddSingleton<IAssetUsageService, AssetUsageService>();
    services.TryAddSingleton<IBrandKitService, BrandKitService>();

    // Event publisher (if foundation-events' IDomainEventPublisher is present, it's used; else local stub)
    services.TryAddSingleton<IDamEventPublisher>(sp =>
    {
        var canonical = sp.GetService<IDomainEventPublisher>();
        return canonical is not null
            ? new DamEventPublisherAdapter(canonical)
            : new InMemoryDamEventPublisher();
    });

    // Tier-2 post-merge reconciler (stub; verifies AssetUsage references point at existing assets)
    services.AddSingleton<IPostMergeReconciler, AssetUsageOrphanReconciler>();

    return services;
}

public sealed class BlocksDocsDamOptions
{
    public int DefaultSearchLimit { get; init; } = 100;
    public int MaxTagDepth { get; init; } = 4;
    public bool WarnOnLicenseExpiresAtInPast { get; init; } = true;
}
```

#### apps/docs page

**`apps/docs/blocks/docs-dam/overview.md`** (matches `pattern-006`):

Sketch structure (~80–120 lines):

```markdown
# blocks-docs-dam

Digital Asset Management for marketing assets, collections, and brand kits — the
Phase 3 follow-on layer of the `blocks-docs-*` cluster.

## Overview

This package wraps `Document` (from `blocks-docs-core`) + `Attachment` /
`StorageRef` (from `blocks-docs`) with marketing-asset-specific metadata, tags,
collections, usage tracking, and brand-kit management.

It provides:

- `MarketingAsset` — typed wrapper over a `Document` with assetKind discriminator,
  rights metadata, a11y altText, technical metadata (duration / dimensions).
- `AssetTag` + `AssetTagAssignment` — hierarchical tag taxonomy; many-to-many assignments.
- `AssetCollection` + `AssetCollectionMembership` — curated sets (campaigns, mood boards, ad-hoc galleries).
- `AssetUsage` — track where an asset is referenced (for licensing compliance + impact-of-change queries).
- `BrandKit` + `BrandKitElement` — tenant brand pack (logos, colors, fonts, taglines, voice notes).

## Public surface

- `IMarketingAssetService` — registration / update / tag / search / tombstone / impact.
- `IAssetCollectionService` — collection CRUD + membership + reorder + archive.
- `IAssetUsageService` — record / deactivate / query usage; impact-of-change report.
- `IBrandKitService` — kit + element CRUD + activate / deactivate + primary-management.

## Gate

This package depends on:
- `blocks-docs-core` (Document base entity, DocumentType.MarketingAsset)
- `blocks-docs` (StorageRef discriminated union, Attachment)
- `blocks-people-foundation` (PartyId)

## What it doesn't do (v1)

- No auto-thumbnail generation (deferred — lazy thumbnail follow-on).
- No AI / ML asset tagging (schema reserves space; no model).
- No full-text-search index (in-memory substring matching v1; FTS5 follow-on).
- No brand-compliance acknowledgment workflow (per Stage 02 §9 Q9 — deferred).
- No asset version history (re-register as new MarketingAsset; underlying Document still versions).

## Quickstart

```csharp
// In Program.cs:
services.AddSunfishDocsCore();
services.AddBlocksDocs();
services.AddBlocksDocsDam();

// In a feature handler:
var registerResult = await _assetService.RegisterAsync(new RegisterAssetCommand(
    DocumentId: docId,
    AssetKind: AssetKind.Image,
    Title: "Brand Hero Image — Spring 2026",
    Description: "Lobby photo, taken at golden hour",
    StorageRef: storageRef,
    ThumbnailRef: null,
    AltText: "Sunlit lobby with brass fixtures",
    DurationSeconds: null,
    WidthPx: 1920,
    HeightPx: 1080,
    Rights: new AssetRights(AssetRightsOwnership.Owned, null, []),
    LicenseExpiresAt: null,
    Attribution: null,
    RegisteredBy: actorId));

// Search:
var searchResult = await _assetService.SearchAsync(new AssetSearchQuery(
    TenantId: tenantId,
    FullTextQuery: "lobby",
    TagSlugs: new[] { "spring-2026", "hero" },
    CollectionId: null,
    AssetKind: AssetKind.Image,
    UsedInLast: Duration.FromDays(90),
    Limit: 50,
    Offset: 0));
```

## Algorithms

- Asset search → `blocks-docs-schema-design.md` §5.4
- Impact-of-change report → §5.4

## Related

- `blocks-docs-core` (predecessor; Document base)
- `blocks-docs` (predecessor; StorageRef + Attachment)
- Future consumers: `blocks-listings` (listing-photo selector), `blocks-marketing-publish` (campaign asset attachment), `blocks-social-publish` (social-post asset selection)
```

Add a toc entry to `apps/docs/blocks/toc.yml` if that's the project convention (mirror sibling `blocks-docs-core` / `blocks-docs-wiki` entries).

#### NOTICE.md (ResourceSpace BSD-3 attribution)

**`packages/blocks-docs-dam/NOTICE.md`** — required by Stage 02 §8 + ADR 0088 §3.4 attribution discipline:

```markdown
# NOTICE — Sunfish.Blocks.DocsDam

This package's entity shapes for digital-asset management (MarketingAsset +
AssetTag + AssetTagAssignment + AssetCollection + AssetCollectionMembership +
AssetUsage + BrandKit + BrandKitElement; collection + usage-tracking + brand-kit
grouping concepts) derive from ResourceSpace's open-source DAM model
(<https://www.resourcespace.com/>, BSD 3-Clause license).

ResourceSpace version studied: 10.x branch (as of 2026-05).

The Sunfish implementation is original code, distributed under the MIT License.
The ResourceSpace entity-shape pattern is reproduced with attribution per the
ResourceSpace BSD-3 license terms.

## Clean-room study (no code or schema borrowed)

The following projects were studied for product-shape understanding only via
public documentation, demos, and surface observation:

- Razuna (GPLv3) — DAM workflow observation only.
- Bynder (proprietary SaaS) — brand-kit and asset-collection UX validation.
- Brandfolder (proprietary SaaS) — brand-kit UX validation.

No code, schema, or identifier names from these projects appear in this package.
```

Also add a one-line source-header comment on `MarketingAsset.cs`, `AssetCollection.cs`, `AssetUsage.cs`, `BrandKit.cs` referencing ResourceSpace, per the project's standard attribution-comment convention.

#### Ledger flip

Update `icm/_state/workstreams/W73-blocks-docs-dam.md` (the source W*.md file — per `feedback_never_add_workstream_rows_directly_to_ledger`) with `State: built` + the 5 PR numbers. Then run the render-ledger.py to regenerate `active-workstreams.md`. Standard PR body with test count.

If `W73-blocks-docs-dam.md` does not yet exist as a source file (the active-workstreams row may have been written directly during the planning phase), STOP and file `cob-question-*` requesting XO to author the source file first — DO NOT edit the rendered ledger directly.

#### Tests (PR 5)

`tests/MarketingAssetServiceTests.cs` (~14 tests):
- `Register_HappyPath_PersistsAndEmitsRegisteredEvent`.
- `Register_RejectsDocumentNotFound`.
- `Register_RejectsDocumentWrongType` (DocumentType.Generic, etc.).
- `Register_RejectsDocumentWrongTenant`.
- `Register_RejectsEmptyTitle; RejectsTitleTooLong`.
- `Register_RejectsNullStorageRef`.
- `Register_Idempotent_OnSameDocumentId_ReturnsExisting`.
- `Register_DimensionsForKind_WarnsOnImageWithDurationSeconds_DoesNotReject`.
- `UpdateMetadata_HappyPath_BumpsVersion_EmitsUpdatedEvent`.
- `UpdateMetadata_RejectsTombstoned`.
- `SetRights_LicenseExpiresAtInPast_WarnsOnly`.
- `SetThumbnail_NullClearsThumbnail`.
- `Tombstone_HappyPath_SetsDeletedAt_EmitsEvent`.
- `Tombstone_AlreadyTombstoned_ReturnsError`.

`tests/MarketingAssetServiceTagsTests.cs` (~5 tests):
- `AddTag_HappyPath_CreatesAssignment`.
- `AddTag_RejectsAlreadyAssigned`.
- `AddTag_RejectsTagFromOtherTenant`.
- `RemoveTag_HappyPath_TombstonesAssignment`.
- `RemoveTag_NotAssigned_ReturnsError`.

`tests/MarketingAssetServiceSearchTests.cs` (~10 tests; covers the §5.4 algorithm):
- `Search_NoFilters_ReturnsAllNonTombstonedAssetsInTenant_OrderedByUpdatedAtDesc`.
- `Search_FullTextQuery_MatchesTitle_Description_AltText_CaseInsensitive`.
- `Search_TagSlugs_IntersectionMatch_RequiresAllTags`.
- `Search_TagSlugs_UnresolvableSlug_ReturnsEmpty`.
- `Search_CollectionId_FiltersToMembers`.
- `Search_AssetKind_Filters`.
- `Search_UsedInLast_FiltersByRecentUsage`.
- `Search_CombinedFilters_All_Apply` (full-text + tags + collection + kind + recency all together).
- `Search_Pagination_OffsetAndLimit_HasMoreTrueWhenMoreRemain`.
- `Search_ExcludesTombstonedAssets`.

`tests/MarketingAssetServiceImpactTests.cs` (~3 tests):
- `GetImpactOfChange_DelegatesToUsageService_ReturnsReport`.
- `GetImpactOfChange_NoUsages_ReturnsEmptyReport`.
- `GetImpactOfChange_AssetNotFound_ReturnsEmptyReport` (defensive — does not throw).

`tests/BlocksDocsDamDIRegistrationTests.cs` (~3 tests; matches pattern-005):
- `AddBlocksDocsDam_RegistersAllRepositoriesAndServices`.
- `AddBlocksDocsDam_OptionsCallbackInvoked`.
- `AddBlocksDocsDam_IDomainEventPublisherPresent_UsesCanonicalAdapter; AbsentUsesInMemoryStub`.

**Total new tests this PR: ~35.**

#### Verification

- `dotnet build` succeeds for the package + all downstream consumers (none yet).
- `dotnet test packages/blocks-docs-dam/tests/` passes all ~35 new tests + ~108 from PRs 1-4 = ~143 total tests for the DAM cluster.
- `dotnet test` across the whole solution passes (no regressions in `blocks-docs-core`, `blocks-docs`, `blocks-docs-wiki`, or any other consumer).
- `apps/docs/blocks/docs-dam/overview.md` renders cleanly in the docs site (mirrors sibling pages' rendering).
- `NOTICE.md` present at the package root; the `Sunfish.Blocks.DocsDam.csproj` declares `<NOTICEFile>NOTICE.md</NOTICEFile>` (mirror sibling pattern).
- Source-header attribution comments present on `MarketingAsset.cs`, `AssetCollection.cs`, `AssetUsage.cs`, `BrandKit.cs`.
- Ledger entry for W#73 flipped to `built`.

#### Do NOT in this PR

- Do NOT introduce real full-text-search (FTS5 / external index) — out of scope for v1.
- Do NOT introduce thumbnail generation — deferred per §What this does NOT ship #1.
- Do NOT introduce a UI layer (Anchor MAUI / React) — that's a separate front-end hand-off.
- Do NOT change `blocks-docs-core` or `blocks-docs` to make DAM easier. If a contract needs amending, file `cob-question-*` (XO authors a docs-core amendment hand-off).
- Do NOT edit `active-workstreams.md` directly — go through `W73-blocks-docs-dam.md` per the ledger discipline.

---

## Cross-cluster integration

### Consumed (DAM is the consumer)

| Source cluster | Surface consumed | How |
|---|---|---|
| `blocks-docs-core` | `Document`, `DocumentId`, `DocumentType` enum (`MarketingAsset` value), `IDocumentRepository` | Every `MarketingAsset` wraps a `Document`. PR 5's `IMarketingAssetService.RegisterAsync` cross-fetches the document for type + tenant verification |
| `blocks-docs` | `StorageRef` discriminated union, `Attachment`, `IAttachmentService` (read-only) | `MarketingAsset.storageRef` is a `StorageRef`. DAM does NOT upload bytes itself — callers upload via `IAttachmentService` first, then pass the resulting `StorageRef` to DAM. PR 5's optional cross-reference check (verify the `StorageRef` resolves to an existing attachment in the same tenant) is in scope; warn-only Tier-1 |
| `blocks-people-foundation` | `PartyId` | Audit fields (`CreatedBy`, `UpdatedBy`, `DeletedBy`, `RegisteredBy`, etc.), `AssetCollection.OwnerId`, `AssetTagAssignment.AssignedBy` |
| `foundation-multitenancy` | `TenantId`, analyzer enforcement | Tenant-isolation on every read/write — analyzer-enforced per project convention |
| `foundation-events` | `IDomainEventPublisher`, `DomainEventEnvelope<TPayload>` | Production event emission. PR 5 wires the canonical publisher if present; otherwise local stub |
| `NodaTime` | `Instant`, `Duration`, `DateOnly` | Standard project clock surface |

### Produced (DAM is the producer)

| Event | Future consumer clusters | Payload | Notes |
|---|---|---|---|
| `Dam.MarketingAssetRegistered` | `blocks-reports-*` (asset inventory), `blocks-listings` (photo picker — future), `blocks-marketing-publish` (campaign asset attach — future) | `{ assetId, documentId, tenantId, assetKind, registeredAt }` | Emitted by `IMarketingAssetService.RegisterAsync` happy path |
| `Dam.MarketingAssetMetadataUpdated` | (none external in v1; internal cache invalidation only — not wired to consumers) | `{ assetId, fieldsChanged: string[] }` | Optional; ship if XO consensus emerges that consumers care |
| `Dam.MarketingAssetTombstoned` | `blocks-reports-*` (inventory delta), `blocks-listings` (orphan-photo detection — future) | `{ assetId, tombstoneReason, tombstonedBy, tombstonedAt }` | |
| `Dam.AssetCollectionPublished` | `blocks-reports-*` (campaign inventory) | `{ collectionId, assetCount }` | Emitted on Create + Unarchive |
| `Dam.AssetUsageRecorded` | `blocks-reports-*` (asset utilization) | `{ usageId, assetId, consumerKind, consumerId? }` | Insert-path only (NOT on LastUsedAt bumps) |
| `Dam.BrandKitActivated` | `blocks-reports-*` (brand audit), `blocks-marketing-publish` (template style refresh — future) | `{ brandKitId, effectiveFrom }` | |

All payloads ship at `schemaVersion: "1.0.0"`. Renames are forbidden; future additive fields → minor bump.

### Future-only consumers (NOT in scope; documented for forward awareness)

| Future cluster | What it would consume |
|---|---|
| `blocks-listings` | `MarketingAsset` (photo references on listings); subscribes to `Dam.MarketingAssetTombstoned` to detect orphan photos |
| `blocks-marketing-publish` (or similar; not yet a workstream) | `BrandKit` (style for campaign templates); `AssetCollection` (campaign asset bundles); subscribes to `Dam.BrandKitActivated` |
| `blocks-social-publish` (future) | `MarketingAsset` (post images); subscribes to `Dam.MarketingAssetRegistered` |
| `blocks-docs-templates` | `MarketingAsset` (logo on contract templates) |
| `blocks-docs-signing` | `MarketingAsset` (signed document watermarks — future) |

When these consumer clusters arrive, they add the necessary subscriptions in their own hand-offs; DAM ships only the producer surface in v1.

---

## Pre-merge council requirements

**None mandatory.** DAM is a low-risk surface:

- No financial correctness primitives (no money, no double-entry).
- No auth surface (tenant-isolation flows through `blocks-docs-core` + `blocks-docs` which are already-merged substrates).
- No new cryptographic primitives.
- No new secret-handling surface.
- No analyzer policy changes.
- No public-facing API.
- No new persistence migrations (in-memory v1).
- ResourceSpace BSD-3 attribution discipline is mechanical (NOTICE.md + source-comment).

Standard COB self-audit per `feedback_council_can_miss_spot_check_negative_existence` is sufficient — spot-check structural-citation negative existence (e.g., verify `IThumbnailGenerator` is genuinely absent before shipping the lazy-thumbnail "deferred" claim).

**Optional spot-check (NOT a halt — flag in PR if you want a second opinion):**

- **PR 5 search algorithm correctness against §5.4 pseudocode** — if COB has any doubt about the intersection logic in `tagAssignmentRepo.QueryAssetIdsWithAllTags(...)`, request a single-perspective Sonnet-medium spot-check.

If during build COB observes the cluster picking up an unexpected surface (e.g., a contributor PR landing under DAM that adds tenant-quota enforcement on asset storage — a security-engineering surface), HALT and request council per the standard process. The "no council" judgment is conditional on the scope outlined here.

---

## Idempotency-key catalog

Consolidated list contributed by this hand-off (per the cluster's idempotency-key convention; consumers dedup on these):

| Key | Emitted in | PR | Notes |
|---|---|---|---|
| `marketing-asset-registered:{assetId}` | `IMarketingAssetService.RegisterAsync` | PR 5 | Per-asset; idempotent re-register on same DocumentId returns existing asset (no duplicate event) |
| `marketing-asset-tombstoned:{assetId}:{tombstonedAt}` | `IMarketingAssetService.TombstoneAsync` | PR 5 | Per-event-instance; already-tombstoned re-tombstone returns error (no event) |
| `marketing-asset-metadata-updated:{assetId}:{updatedAt}` | `IMarketingAssetService.UpdateMetadataAsync` (optional event; ship only if needed by consumers) | PR 5 | Per-update; many possible per asset lifetime |
| `asset-tag-assigned:{assignmentId}` | `IMarketingAssetService.AddTagAsync` | PR 5 | Per-assignment |
| `asset-tag-removed:{assignmentId}:{removedAt}` | `IMarketingAssetService.RemoveTagAsync` | PR 5 | Per-removal |
| `asset-collection-created:{collectionId}` | `IAssetCollectionService.CreateAsync` | PR 2 | Per-collection |
| `asset-collection-published:{collectionId}:{publishAttemptAt}` | `IAssetCollectionService.CreateAsync` + `UnarchiveAsync` | PR 2 | Per-emission |
| `asset-collection-archived:{collectionId}:{archivedAt}` | `IAssetCollectionService.ArchiveAsync` | PR 2 | Per-archive |
| `asset-collection-membership-added:{membershipId}` | `IAssetCollectionService.AddAssetAsync` | PR 2 | Per-membership |
| `asset-collection-membership-removed:{membershipId}:{removedAt}` | `IAssetCollectionService.RemoveAssetAsync` | PR 2 | Per-removal |
| `asset-usage-recorded:{usageId}` | `IAssetUsageService.RecordUsageAsync` (insert path only) | PR 3 | Per-usage instance |
| `asset-usage-deactivated:{usageId}:{deactivatedAt}` | `IAssetUsageService.MarkUsageInactiveAsync` | PR 3 | Optional event |
| `brand-kit-created:{brandKitId}` | `IBrandKitService.CreateAsync` | PR 4 | Per-kit |
| `brand-kit-activated:{brandKitId}:{effectiveFrom}` | `IBrandKitService.ActivateAsync` | PR 4 | Per-effective-from instant |
| `brand-kit-deactivated:{brandKitId}:{effectiveUntil}` | `IBrandKitService.DeactivateAsync` | PR 4 | Optional event |

Consumers (reports, future listings) dedup on these keys within their event-bus window per the project's standard event-bus contract.

---

## Dependencies + sequence

```
blocks-docs                  ← MUST BE BUILT (W#71)
   └──▶ blocks-docs-core     ← MUST BE BUILT (W#69; transitively brings PartyId via blocks-people-foundation)
              │
              ▼
        blocks-docs-dam      ← THIS HAND-OFF (W#73)
              │
              ▼
        (future consumers — out of scope here)
        blocks-listings, blocks-marketing-publish, blocks-social-publish,
        blocks-docs-templates, blocks-docs-signing
```

**Within this hand-off, PR sequence:**

```
PR 1 (scaffold + MarketingAsset/Tag substrate)
   │
   ▼
PR 2 (AssetCollection)
   │
   ▼
PR 3 (AssetUsage)          ──┐
   │                          ├─▶ PR 5 (IMarketingAssetService + DI + docs + ledger)
PR 4 (BrandKit)            ──┘
   │
   ▼
(parallelizable: PR 3 || PR 4 once PR 1 is in)
```

PR 5 cannot land until PRs 1–4 are all on `main` (its DI umbrella + search algorithm cross-references every entity-group from prior PRs).

---

## License posture

### Borrowed-with-attribution (permissive)

- **ResourceSpace** `MarketingAsset` + `AssetTag` + `AssetCollection` + `AssetUsage` + `BrandKit` shapes (BSD 3-Clause). The DAM entity decomposition, the tag taxonomy hierarchy, the collection-membership join pattern, the usage-tracking concept, and the brand-kit-as-grouping concept all derive from ResourceSpace's open DAM model per `blocks-docs-schema-design.md` §3.4 + §8.

**Attribution requirements:**

1. The package's `Sunfish.Blocks.DocsDam.csproj` carries `<PropertyGroup><NOTICEFile>NOTICE.md</NOTICEFile></PropertyGroup>`.
2. `packages/blocks-docs-dam/NOTICE.md` (new file in PR 5; content sketched in §PR 5 above).
3. Source-header comments on `MarketingAsset.cs`, `AssetCollection.cs`, `AssetUsage.cs`, `BrandKit.cs` reference ResourceSpace in a one-line comment.

### Clean-room only (copyleft + proprietary)

Per Stage 02 §8 "Discipline note", these sources were studied for understanding only and contribute NO code or schema:

- **Razuna** (GPLv3) — DAM workflow observation only. No schema lifted.
- **Bynder** (proprietary SaaS) — brand-kit and asset-collection UX validation via public product pages.
- **Brandfolder** (proprietary SaaS) — brand-kit UX validation via public product pages.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code (spot-check by grep before merge — look for "razuna", "bynder", "brandfolder" in code; expected: zero).
3. The clean-room schema in `blocks-docs-schema-design.md` §3.4 is the source of truth; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + MarketingAsset / AssetTag / AssetTagAssignment + repos) | ~33 | record fields; repository round-trips; tenant isolation; validation invariants; tag slug uniqueness; tag-assignment intersection |
| PR 2 (AssetCollection + service) | ~26 | collection record + membership + service CRUD + reorder + archive lifecycle + tenant guards |
| PR 3 (AssetUsage + service) | ~18 | usage record + repository + natural-key idempotency + impact-of-change report |
| PR 4 (BrandKit + service) | ~31 | brand-kit + per-element-kind shape invariants + activation lifecycle + primary uniqueness + active-as-of query |
| PR 5 (IMarketingAssetService + DI + docs + ledger) | ~35 | register lifecycle + tag attach + §5.4 search algorithm (10 tests) + impact-of-change delegation + DI registration |
| **Total** | **~143 new tests across the cluster** | |

### Cluster-level acceptance (PASS gate at end of PR 5)

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.DocsDam` package + every existing downstream consumer (none in v1 — solution-wide build must still pass).

**A2.** `dotnet test packages/blocks-docs-dam/tests/` passes all ~143 new tests. `dotnet test` across the whole solution passes with zero regressions.

**A3.** Marketing-asset register round-trip:
- Pre-seed a `Document` with `DocumentType = MarketingAsset` via `IDocumentCommandService.CreateAsync(...)`.
- Pre-seed a `StorageRef` (inline `inline-sqlite-blob` is fine — the smallest test seam).
- Call `IMarketingAssetService.RegisterAsync(...)` with the document ID + storage ref + title.
- Assert: `RegisterAssetResult.Error == None`; the asset exists in the repository; `Dam.MarketingAssetRegistered` event was published.
- Call `RegisterAsync` again with the same `DocumentId`.
- Assert: result is the *same* asset (idempotent); no duplicate event.

**A4.** Tag intersection search:
- Register 3 assets in the same tenant.
- Create 2 tags (slug `hero`, slug `spring-2026`).
- Assign tag `hero` to assets 1 + 2; assign tag `spring-2026` to assets 1 + 3.
- `SearchAsync(query: TagSlugs = ["hero", "spring-2026"])`.
- Assert: returns asset 1 only (intersection).

**A5.** Collection lifecycle:
- Create a collection with `CollectionKind = Campaign`.
- Add 3 assets via `AddAssetAsync` with sort orders 10, 20, 30.
- `Reorder` to swap order to [asset3, asset1, asset2] → sort orders become [10, 20, 30] in that asset order.
- Archive the collection; `QueryByTenant(includeArchived = false)` excludes it; `includeArchived = true` includes it.
- Unarchive; it returns to the default query.

**A6.** Usage impact-of-change:
- Register 1 asset.
- Record 3 usages (consumerKind: Listing, Listing, EmailTemplate) with different consumerIds.
- Mark 1 of the Listing usages inactive.
- `GetImpactOfChangeAsync(assetId)` returns `ActiveUsageCount = 2`, `TotalUsageCount = 3`, `Entries.Count = 3` (all reported, with `IsActive` flag).

**A7.** Brand-kit activation:
- Create a brand-kit with `EffectiveFrom = now - 1 day`, no `EffectiveUntil`.
- Add elements: `Logo` (with AssetId), `Color` (`#1B4F72`), `Font` (FontFamily = "Inter"), `Tagline` (TextContent = "Live where you work").
- Activate.
- `GetActiveAsOfAsync(now)` returns the kit.
- Deactivate with `effectiveUntil = now + 1 hour`.
- `GetActiveAsOfAsync(now + 30 minutes)` still returns it.
- `GetActiveAsOfAsync(now + 2 hours)` does NOT return it.

**A8.** Search composability:
- Register 5 assets across 2 collections + 3 tags + 2 asset kinds.
- Record usage on 3 of them within the last 7 days.
- `SearchAsync` with full-text query + tag-intersection + collectionId + assetKind + usedInLast all set simultaneously.
- Assert: returns only the assets passing every filter; pagination works (`Limit = 2`, `Offset = 0` returns first 2; `Limit = 2`, `Offset = 2` returns next 2).

**A9.** Tenant isolation:
- In a multi-tenant test fixture (2 tenants), register assets / tags / collections / brand-kits in tenant A.
- Switch tenant context to tenant B; query for the assets / tags / collections / brand-kits.
- Assert: tenant B sees an empty set across every read API.
- Attempt to add an asset from tenant A to a collection in tenant B → `AssetWrongTenant` error.

**A10.** Source attribution discipline:
- `grep -i "razuna\|bynder\|brandfolder" packages/blocks-docs-dam/**/*.cs` → returns ZERO matches (only the NOTICE.md mentions them in attribution context).
- `packages/blocks-docs-dam/NOTICE.md` exists and references ResourceSpace BSD-3.
- Source-header comments on `MarketingAsset.cs`, `AssetCollection.cs`, `AssetUsage.cs`, `BrandKit.cs` reference ResourceSpace.

---

## Halt conditions (`cob-question-*` beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### 1. `DocumentType.MarketingAsset` enum value missing

If `blocks-docs-core` shipped without `DocumentType.MarketingAsset` in the enum, halt. The DAM cluster CANNOT proceed without the discriminator value (PR 5's `RegisterAsync` checks `doc.DocumentType == DocumentType.MarketingAsset`). File `cob-question-2026-05-XXTHH-MMZ-w73-docs-dam-documenttype-missing.md` requesting a 1-line amendment to `blocks-docs-core` (`pattern-008` docs-page-style touch — single enum-value add).

### 2. `StorageRef` shape unstable

If the `blocks-docs` `StorageRef` discriminated union has open PRs (unmerged), STOP and wait. DAM consumes `StorageRef` in `MarketingAsset.storageRef` and `MarketingAsset.thumbnailRef`; a mid-merge `StorageRef` refactor would cascade. Pre-build checklist step 2 catches this. If a `StorageRef` PR is opened *after* DAM PR 1 lands, file `cob-question-*` to coordinate (XO adjudicates whether DAM pauses or the `StorageRef` change defers).

### 3. `PartyId` placement

If `blocks-people-foundation` is unexpectedly absent (despite Gate 1 implying it's transitively present), STOP. Do NOT introduce a local `PartyId` placeholder — that would diverge from the cluster convention. File `cob-question-*`.

### 4. `IDomainEventPublisher` not yet shipped on `main`

The DI umbrella in PR 5 prefers the canonical `IDomainEventPublisher` from `foundation-events` if present; else falls back to in-memory stub. **No halt needed** — both paths are supported. If `foundation-events` is shipped but the `IDomainEventPublisher` shape has drifted from the W#69 docs-core hand-off's assumptions, file `cob-question-*` (XO updates the wiring).

### 5. ResourceSpace attribution discipline

If COB hits any uncertainty about ResourceSpace attribution scope (e.g., "is this entity name borrowed too closely?"), file `cob-question-*`. The XO ratification heuristic: ResourceSpace BSD-3 *permits* attribution + redistribution; the discipline note in Stage 02 §8 is about not borrowing GPL/AGPL code from Razuna/HedgeDoc/etc. ResourceSpace shapes are explicitly OK with attribution.

### 6. Search algorithm correctness under intersection

§5.4 specifies intersection (ALL tags must match). PR 5's tests assert this. If COB's implementation diverges (e.g., union by mistake), it will fail PR 5 test `Search_TagSlugs_IntersectionMatch_RequiresAllTags`. If COB cannot reconcile the test failure (e.g., the in-memory `QueryAssetIdsWithAllTagsAsync` implementation has a subtle bug), file `cob-question-*` with the failing test output — XO can review the algorithm + suggest a fix.

### 7. Per-element-kind shape invariants on `BrandKitElement`

If COB finds the shape invariants in PR 4 (e.g., "Logo requires AssetId, rejects ColorHex") too strict for a real-world need (e.g., a logo with associated brand color metadata), file `cob-question-*`. The intent is to keep each element type clean; an `extras` JSON bag could be added if the use case is real.

### 8. Loro CRDT integration questions

Per Stage 02 §7.4 + `blocks-docs` substrate's existing handling: CRDT integration for `MarketingAsset` / `AssetCollection` / `BrandKit` happens at the kernel-crdt layer (out of DAM's scope). DAM's `Version` + `RevisionVector` fields are *Loro-managed* (the application reads only). If COB hits a compilation question on these fields (e.g., "what is the IReadOnlyDictionary key shape?"), check the precedent in `blocks-docs-core/Models/Document.cs` first; only file `cob-question-*` if that precedent is also unclear.

### 9. apps/docs page template

If the project has changed its apps/docs page template since `blocks-docs-core` / `blocks-docs-wiki` shipped (e.g., new frontmatter schema, new toc.yml format), mirror whatever the most recent sibling shipped. If the convention is genuinely unclear, file `cob-question-*`.

### 10. Workstream source file (`W73-blocks-docs-dam.md`) missing

If `icm/_state/workstreams/W73-blocks-docs-dam.md` does not exist (the active-workstreams row may have been authored directly without a source file), do NOT edit `active-workstreams.md` directly per `feedback_never_add_workstream_rows_directly_to_ledger`. File `cob-question-*` requesting XO to author the source file first; halt PR 5's ledger-flip step until the source file lands. PRs 1–4 can still proceed.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–5 merged to main** (sequentially with PR 3 / PR 4 parallelization allowed once PR 1 is in).
2. **Asset register round-trip:** acceptance test A3 passes.
3. **Tag intersection search:** acceptance test A4 passes.
4. **Collection lifecycle:** acceptance test A5 passes.
5. **Usage impact-of-change:** acceptance test A6 passes.
6. **Brand-kit activation:** acceptance test A7 passes.
7. **Search composability:** acceptance test A8 passes.
8. **Tenant isolation:** acceptance test A9 passes.
9. **Source-attribution discipline:** acceptance test A10 passes (zero copyleft identifiers; ResourceSpace NOTICE present).
10. **Tests pass:** ~143 new tests across the package.
11. **`apps/docs/blocks/docs-dam/overview.md`** published (ships in PR 5).
12. **`NOTICE.md`** present at package root with ResourceSpace BSD-3 attribution (ships in PR 5).
13. **Source-header comments** on `MarketingAsset.cs`, `AssetCollection.cs`, `AssetUsage.cs`, `BrandKit.cs` (ship in their respective PRs).
14. **`active-workstreams.md`** row for W#73 (or whichever the canonical W# is) updated with `built` status + the 5 PR numbers — via the source `W73-*.md` file, not direct ledger edit.
15. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w73-docs-dam-built.md`** beacon dropped.

When the PASS gate is met, the next document-cluster follow-on hand-offs can proceed independently:

- `blocks-docs-templates-stage06-handoff.md` (ContractTemplate + render-job; independent sibling — can already start once Gate 1 + Gate 2 above clear; not blocked by DAM)
- `blocks-docs-signing-stage06-handoff.md` (SigningWorkflow + Signature; independent sibling — same)
- Future consumer hand-offs (`blocks-listings` photo-picker, `blocks-marketing-publish`, `blocks-social-publish`) can wire to DAM's events + read surface.

---

## Docs

**`apps/docs/blocks/docs-dam/overview.md`** — cluster docs page (ships in PR 5; sketch in §PR 5 above). Cite ADR 0088 §3 (cluster grouping); cite Stage 02 schema design §3.4 + §5.4; cite CRDT conventions §1 (ULID) + §2 (tombstones) + §5 (stable enum codes); cite ResourceSpace BSD-3 attribution.

The page is a public-surface entry point; keep it pragmatic + quickstart-heavy. The deep schema reference is the Stage 02 design doc; the apps/docs page is the developer-onboarding view.

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17 against blocks-docs-core / blocks-docs hand-offs):**

- `packages/blocks-docs-core/Models/Document.cs` — `Document` entity (Gate 1)
- `packages/blocks-docs-core/Models/DocumentId.cs` (Gate 1)
- `packages/blocks-docs-core/Models/DocumentType.cs` — `DocumentType.MarketingAsset` enum value (Gate 1; verify post-W#69 build)
- `packages/blocks-docs-core/Services/IDocumentRepository.cs` (Gate 1)
- `packages/blocks-docs-core/Services/IDocumentCommandService.cs` (Gate 1)
- `packages/blocks-docs/Models/StorageRef.cs` — `StorageRef` discriminated union (Gate 2)
- `packages/blocks-docs/Models/Attachment.cs` (Gate 2)
- `packages/blocks-docs/Services/IAttachmentService.cs` (Gate 2 — read-only consumption from DAM)
- `packages/blocks-people-foundation/Models/PartyId.cs` (Gate 3 — transitive via blocks-docs-core)
- `packages/foundation/Sunfish.Foundation.csproj` (NodaTime / TenantId)
- `packages/foundation-events/Sunfish.Foundation.Events.csproj` — `IDomainEventPublisher` + `DomainEventEnvelope` (verify present on main; fallback stub if absent)
- ADR 0088 §3 (Path II cluster grouping) ✓
- `icm/02_architecture/blocks-docs-schema-design.md` §3.4 + §5.4 + §6 + §7.4 + §8 + §9 Q4/Q9 ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §4, §5, §10 ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3 ✓
- `_shared/engineering/party-model-convention.md` ✓
- `_shared/engineering/standing-approved-patterns.md` pattern-001, pattern-005, pattern-006, pattern-007 ✓

**Introduced by this hand-off** (ship across PRs 1–5):

- New package: `packages/blocks-docs-dam/`
- New types: `MarketingAssetId`, `AssetTagId`, `AssetTagAssignmentId`, `AssetCollectionId`, `AssetCollectionMembershipId`, `AssetUsageId`, `BrandKitId`, `BrandKitElementId`, `MarketingAsset`, `AssetTag`, `AssetTagAssignment`, `AssetCollection`, `AssetCollectionMembership`, `AssetUsage`, `BrandKit`, `BrandKitElement`, `AssetRights`, `AssetKind`, `AssetTaxonomyKind`, `AssetCollectionKind`, `AssetConsumerKind`, `BrandKitElementKind`, `AssetRightsOwnership`, `BlocksDocsDamOptions`, `RegisterAssetCommand`, `UpdateAssetMetadataCommand`, `AssetSearchQuery`, `AssetSearchResult`, `AssetImpactReport`, `AssetImpactEntry`, `RecordUsageCommand`, `AddElementCommand`, `UpdateElementCommand`, plus all result records + error enums per service.
- New services: `IMarketingAssetRepository` + InMemory; `IAssetTagRepository` + InMemory; `IAssetTagAssignmentRepository` + InMemory; `IAssetCollectionRepository` + InMemory; `IAssetCollectionMembershipRepository` + InMemory; `IAssetUsageRepository` + InMemory; `IBrandKitRepository` + InMemory; `IBrandKitElementRepository` + InMemory; `IMarketingAssetService` + `MarketingAssetService`; `IAssetCollectionService` + `AssetCollectionService`; `IAssetUsageService` + `AssetUsageService`; `IBrandKitService` + `BrandKitService`; `IDamEventPublisher` + `DamEventPublisherAdapter` + `InMemoryDamEventPublisher`; `AssetUsageOrphanReconciler` (IPostMergeReconciler stub).
- New events: `MarketingAssetRegisteredEvent`, `MarketingAssetMetadataUpdatedEvent`, `MarketingAssetTombstonedEvent`, `AssetCollectionPublishedEvent`, `AssetUsageRecordedEvent`, `BrandKitActivatedEvent`.
- DI extension: `BlocksDocsDamServiceCollectionExtensions.AddBlocksDocsDam(...)`.
- Docs: `apps/docs/blocks/docs-dam/overview.md`.
- Attribution: `packages/blocks-docs-dam/NOTICE.md`.

**Self-audit reminder (per ADR 0028-A10 + `feedback_council_can_miss_spot_check_negative_existence`):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Negative existence too: verify that `IThumbnailGenerator`, ML-tagging surface, FTS5 index, brand-compliance acknowledgment, and asset version chain are genuinely absent before shipping the "deferred" claims. Use `grep -rn "IThumbnailGenerator\|IBrandComplianceAcknowledgment\|FTS5\|MarketingAssetVersion" packages/` → expected zero matches.

---

## Cohort discipline

This hand-off is the **fourth follow-on cluster** of the document cluster under ADR 0088 Path II (after `blocks-docs`, `blocks-docs-core`, and `blocks-docs-wiki`). The COB self-audit pattern from those precedent hand-offs applies verbatim:

- **`AddBlocksDocsDam()` naming for the DI extension** — matches the cluster convention.
- **`apps/docs/blocks/docs-dam/overview.md` page convention** — applied in PR 5.
- **README.md at the package root** referencing Stage 02 design + ADR 0088 — ship in PR 1.
- **`ConcurrentDictionary` dedup for any cache** — applied in every InMemory repository.
- **Strong-typed Id records** (ULID-backed) — applied for all 8 Id types.
- **Stub interfaces for cross-cluster contracts not yet shipped** — `IDamEventPublisher` ships locally with a canonical adapter + stub fallback; relocates if a canonical replacement is ever introduced (no behavior change).
- **NOTICE.md + source-header comments** — ResourceSpace BSD-3.
- **Standing-pattern eligibility tracking** — PR 1 matches `pattern-001` (cluster scaffold); PR 5 matches `pattern-005` + `pattern-006` + `pattern-007`. PRs 2 / 3 / 4 take the standard self-audit path.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w73-docs-dam-{slug}.md` in
  `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for W#73 (via the source `W73-*.md` file, NOT direct ledger edit).
- `ScheduleWakeup 1800s`.

If COB completes PR 5 + the PASS gate is met:

- Update `active-workstreams.md` via the source `W73-blocks-docs-dam.md` file (per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w73-docs-dam-built.md` to inbox.
- Continue with the next hand-off — likely an independent sibling (`blocks-docs-templates` or `blocks-docs-signing`) or a non-docs workstream from the queue.

---

## Cross-references

- Spec source: `icm/02_architecture/blocks-docs-schema-design.md` §3.4 + §5.4 + §6 + §7.4 + §8 + §9 Q4 + §9 Q9.
- CRDT conventions: `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §4, §5, §10.
- Party convention: `_shared/engineering/party-model-convention.md` §3 + §4 + §7.
- Event bus: `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.
- Standing patterns: `_shared/engineering/standing-approved-patterns.md` pattern-001, pattern-005, pattern-006, pattern-007.
- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` §3 (Path II cluster grouping).
- Predecessor hand-offs:
  - `icm/_state/handoffs/blocks-docs-stage06-handoff.md` (attachment substrate; Gate 2)
  - `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md` (Document base; Gate 1)
- Sibling hand-offs (independent — can ship in parallel; not blockers):
  - `icm/_state/handoffs/blocks-docs-wiki-stage06-handoff.md` (W#70)
  - Future: `blocks-docs-templates-stage06-handoff.md`
  - Future: `blocks-docs-signing-stage06-handoff.md`
- Cohort precedent hand-offs (substrate-only shape):
  - `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md` (DI extension + result-pattern + repository pattern reference)
  - `icm/_state/handoffs/blocks-docs-wiki-stage06-handoff.md` (sibling docs-cluster shape reference)

---

**End of hand-off.**
