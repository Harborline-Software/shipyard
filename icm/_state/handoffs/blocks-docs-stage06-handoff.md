# Hand-off — `blocks-docs` Attachment + DocumentRef + IBlobStore wiring (Phase 3 substrate, cross-cluster attachment surface)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, blocks-docs cluster (Phase 3 substrate — attachment surface)
**Spec source:** [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) §3.1 (Document core; storage-ref dimensions), §6 (Storage model), §7 (cross-cluster contracts)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; ratified by CO 2026-05-16)
**Pipeline:** `sunfish-feature-change`
**Pipeline phase:** Phase 3 (first cluster of phase; predecessor of `blocks-docs-wiki`, `blocks-docs-templates`, `blocks-docs-dam`, `blocks-docs-signing`, and `blocks-reports`)
**Estimated effort:** ~10–13h sunfish-PM (5 feature PRs + 1 docs/DI extension PR + ~55–65 tests + docs + attribution + cross-cluster catalog updates)
**PR count:** 6 PRs
**Pre-merge council:** **MANDATORY** on PR 3 (IBlobStore wiring + tenant-scoped quotas + MIME/size policy → security-engineering required; multi-tenant filesystem boundaries are a defense-in-depth surface). Optional but recommended on PR 4 (cross-cluster DocumentRef contract surface — architect review for the foreign-key contract shape). Standard COB self-audit on PRs 1, 2, 5, 6.
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs"
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/IBlobStore.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/ 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/ 2>&1
```
Expected at this hand-off's start: nothing matching `blocks-docs/` exists (audit confirmed 2026-05-17 — package name available); `packages/foundation/Blobs/IBlobStore.cs` exists with the canonical primitive interface; `blocks-people-foundation` exists (predecessor — provides `IPartyReadModel` + tenant scoping); `foundation-events` exists (predecessor — provides `DomainEventEnvelope<TPayload>`).

---

## Context

### Phase 3 critical-path position

Per ADR 0088 §1 + the Path II 7-cluster decomposition, Phase 3 opens the `blocks-docs-*` family and `blocks-reports-*`. The full Stage 02 design (`blocks-docs-schema-design.md`) spans five candidate sub-packages — `blocks-docs-core`, `blocks-docs-wiki`, `blocks-docs-templates`, `blocks-docs-dam`, `blocks-docs-signing` — each of which will receive its own Stage 06 hand-off in turn.

**This hand-off ships the attachment substrate only:** the minimum surface that every other cluster (AR invoices, AP bills, leases, inspections, work orders) needs *now* in order to attach files to records, with the heavyweight sub-packages (wiki, templates, DAM, signing) deferred. The substrate is package `blocks-docs` (singular — the foundational floor); the sub-packages add specializations on top of it via the canonical `Attachment` + `DocumentRef` primitives.

```
PHASE 3 cluster sequence (Stage 06 hand-off order):

blocks-docs            (THIS hand-off — Attachment substrate; IBlobStore wiring)
  │
  ├──▶ blocks-docs-wiki         (deferred — full WikiSpace/Book/Page + Policy + Procedure)
  ├──▶ blocks-docs-templates    (deferred — ContractTemplate + render-job + instance)
  ├──▶ blocks-docs-dam          (deferred — MarketingAsset + tag taxonomy + BrandKit)
  ├──▶ blocks-docs-signing      (deferred — SigningWorkflow + Signature + audit chain)
  │
  └──▶ blocks-reports           (parallel — consumes DocumentRef for PDF artifacts)
```

`blocks-docs` (this hand-off) is the gate that unblocks:

- **`blocks-financial-ar` invoice attachments** — Invoice rows reference attached files (rendered PDFs, supplier-uploaded photos) via DocumentRef.
- **`blocks-financial-ap` bill attachments** — Bill rows reference vendor-uploaded invoice PDFs + receipt photos via DocumentRef.
- **`blocks-property-leases` lease document attachments** — Lease rows reference signed contract PDFs via DocumentRef.
- **`blocks-property-inspections` inspection-photo attachments** — Inspection deficiencies reference photo evidence via DocumentRef (mobile capture path).
- **`blocks-work-orders` work-order-photo attachments** — Completion proof + before/after photos via DocumentRef.
- **`blocks-docs-*` sub-packages** — Every sub-package consumes `Attachment` as the binary-storage primitive (e.g., `WikiPage.coverImage`, `MarketingAsset.storageRef`, `SigningWorkflow.finalSignedDocument`, `ContractTemplate.body` when binary).
- **`blocks-reports` artifact storage** — Report runs produce PDF artifacts that are attached + referenced via DocumentRef.

It is **not** the predecessor of `blocks-docs-wiki` *content-edit* features (wiki page CRUD + revisions + collab editing); those land in the `blocks-docs-wiki` Stage 06 hand-off.

### What this hand-off ships

Per `blocks-docs-schema-design.md` §3.1 (the `Document` base scaffold) + §6 (storage model) + §7 (cross-cluster contracts):

1. **`Attachment`** record entity — the universal binary-attachment primitive. ULID id; content-hash (sha256 via `kernel-security.computeContentHash` — for v1, the local sha256 implementation in this package); MIME type; byte size; original filename (preserved verbatim from upload); optional thumbnail-ref; storage-ref discriminated union; sensitivity classification; soft-delete tombstone; replacement chain pointer (for "Replace" operations).
2. **`DocumentRef`** cross-cluster link entity — the foreign-key surface every other cluster uses to attach files. Source cluster + source entity id + attachment-id + role label ("invoice-pdf" | "lease-contract" | "inspection-photo" | "work-order-after-photo" | ...).
3. **`StorageRef`** discriminated union (per Stage 02 §6.1) — `inline-sqlite-blob` | `fs-content-addressed` | `external-uri`. Tier rules per §6.2: ≤1MB inline, 1–100MB filesystem CAS, >100MB external (opt-in).
4. **`IAttachmentService`** — upload / get / detach / replace; content-hash deduplication on upload; idempotent on the `(tenantId, contentHash)` tuple (a re-upload of byte-identical content returns the existing Attachment id rather than creating a new one).
5. **`IDocumentRefService`** — create / get / list-for-source-entity / detach; idempotent on the `(tenantId, sourceCluster, sourceEntityId, attachmentId, role)` tuple.
6. **`IBlobStore` wiring** — register the existing `Sunfish.Foundation.Blobs.IBlobStore` + `FileSystemBlobStore` from `packages/foundation/Blobs/` as the production binary persister, wrapped with tenant-scoping + MIME/size policy enforcement. Tenant boundary is *defense-in-depth* at the service layer; the underlying CAS is tenant-agnostic at the bytes level (sha256 is the address), but the service layer rejects any `GetAsync(attachmentId)` whose Attachment row's `tenantId` does not match the active tenant scope.
7. **`MimeTypeAndSizePolicy`** — per-tenant configurable whitelist + size cap. Default whitelist + 100MB per-attachment cap; tenant-scoped quotas (cumulative `sizeBytes` for a tenant); deny-by-default. Server-side MIME sniffing (NOT trust-the-filename) per defense-in-depth.
8. **Append-only event log** — `AttachmentUploaded` / `AttachmentDetached` / `AttachmentReplaced` / `DocumentRefCreated` / `DocumentRefDetached` per `cross-cluster-event-bus-design.md` §3.4 (Docs cluster events — extended in PR 4 + PR 5 with the substrate-level entries).
9. **DI extension `AddBlocksDocs(...)`** + `apps/docs/blocks-docs/overview.md` documentation page.
10. **Idempotency-key catalog additions** to `_shared/engineering/cross-cluster-event-bus-design.md` §3.4 — new entries for the substrate-level events (the editorial change ships in PR 5 alongside the DI extension).

### What this hand-off does NOT ship

- **Wiki content** (WikiSpace, WikiBook, WikiPage, WikiChapter, Policy, Procedure, PolicyVersion, PolicyEffectiveDate, PolicyAcknowledgment) — `blocks-docs-wiki` follow-on hand-off.
- **Contract templates** (ContractTemplate, ContractTemplateField, ContractTemplateClause, TemplateRenderJob, ContractInstance) — `blocks-docs-templates` follow-on hand-off.
- **Marketing DAM** (MarketingAsset, AssetTag, AssetCollection, AssetUsage, BrandKit, BrandKitElement) — `blocks-docs-dam` follow-on hand-off.
- **Signing workflow** (SigningWorkflow, SigningStep, SigningParty, SignatureRequest, Signature, SigningAuditLog) — `blocks-docs-signing` follow-on hand-off; all crypto delegates to `kernel-security` + `kernel-signatures` per Stage 02 §3.5 + §7.3.
- **The full `Document` base entity** with versioning + revision history + folder hierarchy + permissions + retention policy — these belong with the wiki/policy/contract sub-packages where their semantics matter. The substrate ships `Attachment` (binary-only; no version history; no folders) as the floor; wiki/policy pages have their *own* version semantics via `DocumentVersion` and only borrow `Attachment` for embedded binaries (page covers, embedded images).
- **OCR / thumbnail generation** — out of scope per Stage 02 §9 Q4 (defer thumbnail generation to first-display + cache; OCR is not in any Phase 3 hand-off). `Attachment.thumbnailRef` is *declared* as a slot but always `null` in v1.
- **Encryption-at-rest of bytes** — the storage layer (`IBlobStore`) writes bytes plaintext to the local CAS. Per `packages/foundation/Blobs/IBlobStore.cs` remarks: encryption is the *caller's* responsibility (kernel-security envelope keys). Encryption is wired in a follow-on hand-off when crypto-shred retention is needed (Stage 02 §6.4 + §9 Q2). v1 acceptable behavior: plaintext blobs; tenant scoping enforced via service-layer access control; the local CAS is treated as a single-tenant trust boundary (per ADR 0088 §1 — Anchor is single-tenant per install at Light tier).
- **Loro CRDT for attachment-metadata sync** — out of scope for v1 substrate (per ADR 0088 §1 Light tier is single-node SQLite). Attachment + DocumentRef rows ship CP-class only (per CRDT conventions §5 — attachments are append-only). Loro op-log integration lands when Standard tier (multi-node) becomes a target.
- **ERPNext file importer** — DEFERRED. ERPNext's `File` doctype is heavyweight (carries permission rows + per-file attachment associations); the canonical Sunfish migration path for binary files is "extract files from ERPNext export tarball; upload via `IAttachmentService.UploadAsync(...)`; create `DocumentRef` rows pointing at the resulting attachments from the migrating record (Invoice, Bill, Lease, etc.)". This logic lives in `tooling-anchor-import` (the 6-pass orchestrator) not in this package — see §What's NOT and Halt #5 below. The substrate ships no `IErpnext*Importer`.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md` + `icm/02_architecture/path-ii-crdt-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `AttachmentId`, `DocumentRefId` — strongly-typed; ULID storage |
| §2 Soft-delete tombstones | `Attachment.deletedAt` / `deletedBy` / `deletedReason` on the record; hard-delete only allowed when no `DocumentRef` references the attachment AND `RefCount == 0`. Tombstone preserves the row for sync convergence; orphan GC is a follow-on `tooling-anchor-maintenance` workstream. |
| §3 version + revisionVector | `Attachment` is **immutable post-upload** (per §6 posted-then-immutable equivalent) — no version field needed. Replacement is a NEW Attachment with `replacesAttachmentId` pointer; the old attachment is marked `Superseded` + tombstoned after a grace window |
| §4 Append-only sub-collections | `DocumentRef[]` per `(sourceCluster, sourceEntityId)` is append-only at the API level (detach is logical; the row stays for audit) |
| §5 Stable string codes | `StorageRefKind` enum surfaces as a string code (`"inline-sqlite-blob"`, `"fs-content-addressed"`, `"external-uri"`) — never an integer |
| §6 Posted-then-immutable | Once `Attachment` is created (post-upload), the row's substantive fields (contentHash, sizeBytes, mimeType, storageRef, originalFilename, tenantId) are **immutable**. Allowed mutations: `Status` (Active → Superseded → Tombstoned); `replacedByAttachmentId` (set once); audit-trail fields (UpdatedAtUtc, UpdatedBy). Replacement requires uploading a new Attachment; replace ≠ in-place edit |
| §7 State-machine-under-CRDT pattern A — straight monotonic | Attachment lifecycle: `Active → Superseded → Tombstoned`. No branches; no concurrent transitions; CP-class. Same Pattern A applied to DocumentRef: `Active → Detached` |
| §10 Two-tier validation | Tier-1 write-time on every Attachment / DocumentRef persist (MIME / size / quota / tenant scope all enforced at the service layer before persisting); Tier-2 post-merge reconciler verifies `Attachment.contentHash == sha256(IBlobStore.GetAsync(storageRef))` (verifies CAS integrity on a scheduled basis; ships as a stub `IPostMergeReconciler` registration in PR 6) |

The combination ensures: (a) two offline replicas can each upload the same byte-identical file and converge to a single canonical Attachment via the content-hash dedup; (b) different replicas attaching the same Attachment to the same source entity from different roles produces two DocumentRef rows (different roles → not duplicates); (c) attachment deletion is logical and convergent (tombstone wins over un-tombstone — Pattern A monotonic).

### Sync class summary (per Path II §5)

| Entity | Sync class | Reason |
|---|---|---|
| `Attachment` | **CP** | Authoritative storage reference; immutable post-upload; tombstone is monotonic |
| `DocumentRef` | **CP** | Cross-cluster foreign-key contract; audit-relevant; detach is monotonic |
| `IAttachmentService` operations | CP (event-log path) | Upload + detach + replace go through the coordinator |
| `IDocumentRefService` operations | CP (event-log path) | Create + detach go through the coordinator |
| Storage bytes (CAS bodies in `IBlobStore`) | **Out-of-CRDT** | Content-addressed; bytes themselves are immutable by definition; sync via the §6.3 blob-fetch RPC pattern (sync layer concern, not this package) |

Path II `crdt-schema-conventions.md` §5 lists `blocks-docs` entries as primarily AP (for WikiPage.markdownBody, MarketingAsset.tags, DocumentTag, etc.) — **those AP entries belong to the wiki/dam sub-packages, not to this substrate.** The substrate's entities are all CP.

### Open question Q4 (Stage 02) — thumbnail generation deferred

Per `blocks-docs-schema-design.md` §9 Q4, Stage 02 recommends lazy first-display thumbnail generation. **This hand-off does NOT generate thumbnails at all** — `Attachment.thumbnailRef` is always `null` in v1. The wiki / DAM sub-packages will introduce the thumbnail pipeline in their own hand-offs.

### Open question Q2 (Stage 02) — crypto-shred deferred

Per `blocks-docs-schema-design.md` §9 Q2, Stage 02 recommends Phase 2 ships tenant-granularity crypto-shred and defers per-blob crypto-shred. **This hand-off ships neither.** v1 acceptable behavior: tombstone on delete; eventual physical GC of unreferenced CAS bytes is a follow-on `tooling-anchor-maintenance` workstream. When the wiki/policy/contract sub-packages introduce `RetentionPolicy`, they can opt in.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify `foundation/Blobs/IBlobStore.cs` exists.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/IBlobStore.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/FileSystemBlobStore.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/Cid.cs
   ```
   Expected: all three exist (verified 2026-05-17). The `IBlobStore.PutAsync(ReadOnlyMemory<byte>) → ValueTask<Cid>` + `GetAsync(Cid) → ValueTask<ReadOnlyMemory<byte>?>` + `PinAsync` / `UnpinAsync` surface is the canonical foundation primitive — DO NOT redefine it in `blocks-docs`. Consume it via DI.

2. **Verify `blocks-people-foundation` is built.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/
   grep -rln "IPartyReadModel" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/ 2>/dev/null | head -3
   ```
   Expected: package exists; `IPartyReadModel` (or equivalent local-stub) shape is present. The substrate uses `PartyId` for `Attachment.uploadedBy` / `DocumentRef.createdBy`; if `blocks-people-foundation` exports `PartyId`, consume it. If not, ship a local placeholder per the AR hand-off pattern (Halt #2).

3. **Verify `foundation-events` is built (for `DomainEventEnvelope<TPayload>`).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/
   grep -rln "DomainEventEnvelope" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/ 2>/dev/null | head -3
   ```
   Expected: package exists; `DomainEventEnvelope<TPayload>` (or the v1 envelope shape) is present. If absent, ship a local stub `IDocsEventPublisher` mirror per the AR hand-off pattern (PR 4 + PR 5).

4. **Verify no `blocks-docs*` package collision.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs"
   ```
   Expected: empty (verified 2026-05-17). Note: `apps/docs/` is the documentation site — NOT a package; no conflict.

5. **Confirm ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status-flip is housekeeping). Hand-off is `ready-to-build` regardless — CO directive operative.

6. **Confirm consumer set for the cross-cluster integration table** (so PR 4's `DocumentRefService` API + PR 5's DI extension don't surprise any consumer).
   ```bash
   grep -rln "IAttachmentService\|IDocumentRefService\|StorageRef\|AttachmentId" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ /Users/christopherwood/Projects/Harborline-Software/Sunfish/apps/ /Users/christopherwood/Projects/Harborline-Software/Sunfish/accelerators/
   ```
   Expected: empty (no consumer should exist yet — this is the first attachment substrate). If any package already declares an `IAttachmentService` shape, file `cob-question-*` for a council review of the contract boundary.

7. **Confirm no parallel-session PRs touch `foundation/Blobs/` or any `blocks-docs*` path.**
   ```bash
   gh pr list --state open --search "blocks-docs in:title,body"
   gh pr list --state open --search "foundation/Blobs in:files"
   gh pr list --state open --search "IBlobStore in:files"
   ```
   Expected: empty. If anything else is open touching `foundation/Blobs/`, file `cob-question-*` (because PR 3 will register IBlobStore-derived components via DI and any parallel modification of the primitive may conflict).

8. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

9. **Read the Stage 02 design source sections.** Skim `blocks-docs-schema-design.md` §3.1 (Document core; specifically the `storageRef` + `contentHash` + `mimeType` + `sizeBytes` slots — the substrate carves these out of `Document` into a focused `Attachment` entity), §6 (full storage model), §7.3 + §7.4 (cross-cluster contracts — kernel-security delegation and storage-sync semantics). Read `crdt-friendly-schema-conventions.md` §2 (tombstones), §6 (posted-then-immutable), §7 (state-machine Pattern A). Read `cross-cluster-event-bus-design.md` §3.4 (Docs.* events catalog — this hand-off **adds** substrate-level entries to it). Read the AR hand-off (`blocks-financial-ar-stage06-handoff.md`) §Halt for the substrate hand-off pattern.

10. **Read `packages/foundation/Blobs/IBlobStore.cs`** for the exact production primitive contract — the wiring in PR 3 will respect that surface.

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility:

- PR 1: Package scaffold + `Attachment` + `StorageRef` discriminated union + `AttachmentStatus` state machine (substrate)
- PR 2: `IAttachmentService` (upload / get / detach / replace + content-hash dedup) + InMemory implementation
- PR 3: **IBlobStore wiring** + tenant-scoping + `MimeTypeAndSizePolicy` + tenant quotas — **security-engineering council MANDATORY**
- PR 4: `DocumentRef` entity + `IDocumentRefService` cross-cluster API + idempotency-key catalog additions
- PR 5: DI extension `AddBlocksDocs(...)` + `apps/docs/blocks-docs/overview.md` + cross-cluster event-bus catalog editorial
- PR 6: Tier-2 reconciler stub (`IPostMergeReconciler` for CAS-integrity-verify) + cluster-acceptance integration tests

PRs 1 + 2 are sequential. PR 3 depends on PRs 1 + 2 (it wires the service against the persister). PR 4 depends on PR 1 only (it can parallelize with PR 2/3 if COB has bandwidth; XO recommendation: sequence PR 4 after PR 3 for simplicity). PRs 5 + 6 sequence last and depend on all prior PRs.

---

### PR 1 — Package scaffold + `Attachment` + `StorageRef` + `AttachmentStatus`

**Estimated effort:** ~2–3h
**Scope:** new package `blocks-docs`; core records; storage-ref discriminated union; status enum; status-transition validation helper; NO service layer (PR 2); NO IBlobStore wiring (PR 3); NO DocumentRef (PR 4)
**Commit subject:** `feat(blocks-docs): scaffold attachment substrate with Attachment + StorageRef + AttachmentStatus per Stage 02 §3.1 + §6`
**Branch:** `cob/blocks-docs-scaffold`

#### Package skeleton

```
packages/blocks-docs/
├── README.md
├── NOTICE.md                                       (Apache OFBiz + Mayan EDMS attribution; permissive only)
├── Sunfish.Blocks.Docs.csproj
├── Models/
│   ├── AttachmentId.cs
│   ├── Attachment.cs
│   ├── AttachmentStatus.cs
│   ├── StorageRef.cs                               (discriminated union)
│   ├── StorageRefKind.cs                           (string-code enum)
│   ├── Sensitivity.cs                              (public | internal | confidential | restricted)
│   └── (DocumentRef ships in PR 4)
├── Validation/
│   └── AttachmentStatusTransitions.cs
├── Services/
│   └── IAttachmentRepository.cs                    (read+write boundary; impl in PR 2)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs              (stub; extends in PR 2/3/4/5)
└── tests/
    ├── Sunfish.Blocks.Docs.Tests.csproj
    ├── AttachmentRecordTests.cs
    ├── StorageRefDiscriminatedUnionTests.cs
    └── AttachmentStatusTransitionTests.cs
```

#### New types

**`Models/AttachmentId.cs`** — ULID strongly-typed id, mirrors the `Sunfish.Blocks.FinancialLedger.JournalEntryId` + `Sunfish.Blocks.FinancialAr.InvoiceId` pattern.

```csharp
public readonly record struct AttachmentId
{
    public Ulid Value { get; }
    public AttachmentId(Ulid value) { Value = value; }
    public static AttachmentId New() => new(Ulid.NewUlid());
    public override string ToString() => Value.ToString();
}
```

**`Models/AttachmentStatus.cs`** per CRDT conventions §7 Pattern A (straight monotonic):

```csharp
public enum AttachmentStatus
{
    Active,         // post-upload; default
    Superseded,     // replaced by a newer attachment (replacement chain)
    Tombstoned,     // logically deleted; row preserved for sync convergence
}
```

A `static class AttachmentStatusExtensions` provides:

```csharp
public static bool IsAccessible(this AttachmentStatus s)
    => s == AttachmentStatus.Active;

public static bool IsTerminal(this AttachmentStatus s)
    => s == AttachmentStatus.Tombstoned;
```

**`Models/StorageRefKind.cs`** per Stage 02 §6.1 (stable string codes per CRDT conventions §5):

```csharp
public enum StorageRefKind
{
    InlineSqliteBlob,           // wire/JSON: "inline-sqlite-blob"
    FsContentAddressed,         // wire/JSON: "fs-content-addressed"
    ExternalUri,                // wire/JSON: "external-uri"
}

public static class StorageRefKindExtensions
{
    public static string ToWireString(this StorageRefKind k) => k switch
    {
        StorageRefKind.InlineSqliteBlob   => "inline-sqlite-blob",
        StorageRefKind.FsContentAddressed => "fs-content-addressed",
        StorageRefKind.ExternalUri        => "external-uri",
        _ => throw new ArgumentOutOfRangeException(nameof(k)),
    };

    public static StorageRefKind ParseWireString(string s) => s switch
    {
        "inline-sqlite-blob"   => StorageRefKind.InlineSqliteBlob,
        "fs-content-addressed" => StorageRefKind.FsContentAddressed,
        "external-uri"         => StorageRefKind.ExternalUri,
        _ => throw new ArgumentException($"Unknown StorageRefKind wire string: {s}", nameof(s)),
    };
}
```

**`Models/StorageRef.cs`** — the discriminated union per Stage 02 §6.1.

C# implementation uses a sealed-abstract base + sealed-record subtypes (the canonical pattern for closed-set discriminated unions; matches `Sunfish.Foundation.Wayfinder.WayfinderRoute` precedent if present, otherwise establish a new pattern):

```csharp
public abstract record StorageRef
{
    public abstract StorageRefKind Kind { get; }
    public abstract string ContentHash { get; }       // sha256, hex-lowercase
    public abstract long? SizeBytes { get; }          // null for external-uri when unknown

    private StorageRef() { }   // closed inheritance; only this file may declare subtypes

    public sealed record InlineSqliteBlob(string ContentHash, long SizeBytes) : StorageRef
    {
        public override StorageRefKind Kind => StorageRefKind.InlineSqliteBlob;
        long? StorageRef.SizeBytes => this.SizeBytes;
    }

    public sealed record FsContentAddressed(string ContentHash, long SizeBytes, string RelPath) : StorageRef
    {
        public override StorageRefKind Kind => StorageRefKind.FsContentAddressed;
        long? StorageRef.SizeBytes => this.SizeBytes;
    }

    public sealed record ExternalUri(string Uri, string MimeType, string ContentHash, long? SizeBytes) : StorageRef
    {
        public override StorageRefKind Kind => StorageRefKind.ExternalUri;
    }
}
```

(If the inherited-property syntax `long? StorageRef.SizeBytes` does not compile cleanly with positional record syntax, fall back to a manual override property — the `IS-A` discriminator + `ContentHash` + `SizeBytes` surface is what matters.)

**Tier-rule helper** per Stage 02 §6.2:

```csharp
public static class StorageTierPolicy
{
    public const long InlineThresholdBytes = 1L * 1024L * 1024L;       // 1 MB default
    public const long ExternalThresholdBytes = 100L * 1024L * 1024L;   // 100 MB default

    public static StorageRefKind RecommendKind(long sizeBytes) => sizeBytes switch
    {
        <= InlineThresholdBytes => StorageRefKind.InlineSqliteBlob,
        <= ExternalThresholdBytes => StorageRefKind.FsContentAddressed,
        _ => StorageRefKind.ExternalUri,
    };
}
```

(The actual threshold values are tunable via `BlocksDocsOptions.InlineBlobMaxBytes` — wired in PR 3. Defaults match Stage 02 §6.2.)

**`Models/Sensitivity.cs`** per Stage 02 §3.1.1:

```csharp
public enum Sensitivity
{
    Public,
    Internal,
    Confidential,
    Restricted,
}
```

**`Models/Attachment.cs`** — the substrate primitive. Carves the `storageRef` + `contentHash` + `mimeType` + `sizeBytes` slots out of the Stage 02 §3.1.1 `Document` entity into a focused, immutable attachment record:

```csharp
public sealed record Attachment
{
    public AttachmentId Id { get; init; }

    // Tenant scoping (per foundation-multitenancy convention; analyzer-enforced):
    public string TenantId { get; init; } = string.Empty;

    // Content identity:
    public StorageRef StorageRef { get; init; } = default!;
    public string ContentHash { get; init; } = string.Empty;   // sha256, hex-lowercase (also on StorageRef)
    public string MimeType { get; init; } = string.Empty;      // server-sniffed; never trusted from upload
    public long SizeBytes { get; init; }
    public string OriginalFilename { get; init; } = string.Empty;  // preserved verbatim (path-sanitized)

    // Optional thumbnail (always null in v1):
    public StorageRef? ThumbnailRef { get; init; }

    // Classification:
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Internal;  // default; tenant config may override

    // Status + replacement chain:
    public AttachmentStatus Status { get; init; } = AttachmentStatus.Active;
    public AttachmentId? ReplacesAttachmentId { get; init; }     // points to predecessor in a Replace operation
    public AttachmentId? ReplacedByAttachmentId { get; init; }   // back-pointer; set on the older when replaced

    // Tombstone (per CRDT conventions §2):
    public Instant? DeletedAtUtc { get; init; }
    public string? DeletedBy { get; init; }                      // PartyId.Value (string); avoid hard FK while people-foundation contract stabilizes
    public string? DeletedReason { get; init; }

    // Audit:
    public Instant CreatedAtUtc { get; init; }
    public string? CreatedBy { get; init; }                      // PartyId.Value
    public Instant UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
}
```

**Notes on tenant + party FKs:** Per `party-model-convention.md` §4 (read-only consumption from `blocks-people-foundation`), the substrate stores `PartyId.Value` as a string rather than the strong type — this avoids a hard compile-time dependency on `blocks-people-foundation` if the people package's contract is still stabilizing. When `IPartyReadModel` lands, the substrate's `CreatedBy` field type CAN be upgraded to `PartyId?` in a follow-on PR (additive; non-breaking for the wire shape since `PartyId` materializes to its string value).

#### `IAttachmentRepository` (write boundary; impl in PR 2)

```csharp
public interface IAttachmentRepository
{
    Task<Attachment?> GetByIdAsync(string tenantId, AttachmentId id, CancellationToken ct = default);

    Task<Attachment?> GetByContentHashAsync(string tenantId, string contentHash, CancellationToken ct = default);

    Task<IReadOnlyList<Attachment>> QueryActiveAsync(
        string tenantId,
        Sensitivity? minSensitivity = null,
        CancellationToken ct = default);

    Task UpsertAsync(Attachment attachment, CancellationToken ct = default);

    Task TombstoneAsync(string tenantId, AttachmentId id, string by, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Returns the cumulative byte size of all Active attachments for the tenant.
    /// Used by IMimeTypeAndSizePolicy for tenant-scoped quota enforcement.
    /// </summary>
    Task<long> GetTenantTotalSizeBytesAsync(string tenantId, CancellationToken ct = default);
}
```

#### Status-transition validation helper

```csharp
public static class AttachmentStatusTransitions
{
    private static readonly IReadOnlyDictionary<AttachmentStatus, IReadOnlySet<AttachmentStatus>> _allowed
        = new Dictionary<AttachmentStatus, IReadOnlySet<AttachmentStatus>>
        {
            [AttachmentStatus.Active]     = new HashSet<AttachmentStatus>
            {
                AttachmentStatus.Superseded,
                AttachmentStatus.Tombstoned,
            },
            [AttachmentStatus.Superseded] = new HashSet<AttachmentStatus>
            {
                AttachmentStatus.Tombstoned,
            },
            [AttachmentStatus.Tombstoned] = new HashSet<AttachmentStatus>(),  // terminal
        };

    public static bool IsAllowed(AttachmentStatus from, AttachmentStatus to)
        => _allowed.TryGetValue(from, out var set) && set.Contains(to);

    public static void EnsureAllowed(AttachmentStatus from, AttachmentStatus to)
    {
        if (!IsAllowed(from, to))
            throw new InvalidOperationException(
                $"Invalid AttachmentStatus transition: {from} → {to}. " +
                $"Allowed targets from {from}: {string.Join(", ", _allowed[from])}");
    }
}
```

#### DI extension (stub)

**`DependencyInjection/ServiceCollectionExtensions.cs`** (extended in PRs 2/3/4/5; PR 1 ships the empty shell):

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksDocs(this IServiceCollection services)
    {
        // PR 1: no registrations yet (Attachment + StorageRef are pure records).
        // PR 2 adds IAttachmentRepository + IAttachmentService.
        // PR 3 adds IBlobStore wiring + MimeTypeAndSizePolicy.
        // PR 4 adds IDocumentRefService.
        // PR 5 adds IDocsEventPublisher (or wires foundation-events).
        // PR 6 adds IPostMergeReconciler stub.
        return services;
    }
}
```

#### Tests (PR 1)

`tests/AttachmentRecordTests.cs`:

- `Construction_PreservesAllFields`.
- `ImmutableAfterConstruction_RecordEquality_Holds`.
- `OriginalFilename_PreservedVerbatim_NoPathTraversalSanitization` (sanitization is the SERVICE's job in PR 2; the record itself is dumb).
- `Sensitivity_DefaultIsInternal`.
- `Status_DefaultIsActive`.
- `ReplacesAttachmentId_NullByDefault`.
- `ReplacedByAttachmentId_NullByDefault`.
- `Tombstone_DeletedAtUtc_NullByDefault`.

`tests/StorageRefDiscriminatedUnionTests.cs`:

- `InlineSqliteBlob_ConstructionAndKind`.
- `FsContentAddressed_ConstructionAndKind`.
- `ExternalUri_ConstructionAndKind`.
- `Kind_RoundtripsToWireString` (each kind).
- `Kind_ParsesFromWireString_AcceptsValidThreeKinds`.
- `Kind_ParseWireString_ThrowsOnUnknown`.
- `StorageTierPolicy_RecommendsInline_When1MbOrLess`.
- `StorageTierPolicy_RecommendsFsCas_When1MbTo100Mb`.
- `StorageTierPolicy_RecommendsExternal_WhenOver100Mb`.
- `StorageTierPolicy_Boundary_ExactlyInlineThreshold_StaysInline`.
- `StorageTierPolicy_Boundary_ExactlyExternalThreshold_StaysFsCas`.

`tests/AttachmentStatusTransitionTests.cs`:

- `ActiveToSuperseded_IsAllowed`.
- `ActiveToTombstoned_IsAllowed`.
- `SupersededToTombstoned_IsAllowed`.
- `SupersededToActive_IsNotAllowed` (Pattern A is monotonic; no un-supersede).
- `TombstonedToAnything_IsNotAllowed` (terminal).
- `EnsureAllowed_ThrowsWithDescriptiveMessage`.

Total new tests this PR: ~25.

#### Verification

- `dotnet build` succeeds for the new package + adds it to the solution.
- `dotnet test packages/blocks-docs/tests/` passes all ~25 tests.
- `grep -r "Sunfish.Blocks.Docs" packages/blocks-docs/` returns hits in every `.cs` file (sanity check on namespace).
- `grep -r "namespace Sunfish.Blocks.Docs;" packages/blocks-docs/` matches every source file (correct namespace declaration).

#### Do NOT in this PR

- Do NOT introduce `IAttachmentService` — PR 2 ships it.
- Do NOT wire `IBlobStore` — PR 3 ships it.
- Do NOT introduce `DocumentRef` — PR 4 ships it.
- Do NOT introduce MIME validation logic — PR 3 ships `IMimeTypeAndSizePolicy`.
- Do NOT compute sha256 anywhere. The record receives `ContentHash` as a field; computation happens in the service layer (PR 2 + PR 3).

---

### PR 2 — `IAttachmentService` + content-hash deduplication

**Estimated effort:** ~2h
**Scope:** upload / get / detach / replace; content-hash dedup on `(tenantId, contentHash)`; idempotent re-upload returns existing AttachmentId; replace creates new Attachment + sets pointers
**Commit subject:** `feat(blocks-docs): IAttachmentService with content-hash dedup + replacement chain per Stage 02 §3.1 + CRDT conventions §6`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-docs-attachment-service`

#### New types

**`Services/IAttachmentService.cs`**:

```csharp
public interface IAttachmentService
{
    /// <summary>
    /// Uploads bytes as a new Attachment. Idempotent on (tenantId, contentHash):
    /// if an Active Attachment with the same content already exists for the tenant,
    /// returns its id WITHOUT creating a new row.
    ///
    /// The service computes sha256 over the bytes; the policy layer (PR 3) validates
    /// MIME + size + tenant quota BEFORE persisting.
    /// </summary>
    Task<UploadResult> UploadAsync(
        string tenantId,
        ReadOnlyMemory<byte> bytes,
        string originalFilename,
        Sensitivity sensitivity,
        string uploadedBy,
        CancellationToken ct = default);

    Task<Attachment?> GetAsync(string tenantId, AttachmentId id, CancellationToken ct = default);

    /// <summary>
    /// Returns the bytes of the attachment. Fails (returns null Result + StorageMiss)
    /// if the underlying CAS body is unavailable locally — caller may need to wait
    /// for sync (Standard tier) or accept the gap (Light tier with corrupted CAS).
    /// </summary>
    Task<GetBytesResult> GetBytesAsync(string tenantId, AttachmentId id, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes the Attachment (tombstone). Does NOT delete bytes from the CAS;
    /// physical GC of unreferenced bytes is a follow-on tooling-anchor-maintenance
    /// workstream. Fails if any active DocumentRef points at this Attachment
    /// (caller must detach refs first — enforces referential integrity for cross-cluster
    /// foreign keys).
    /// </summary>
    Task<DetachResult> DetachAsync(
        string tenantId,
        AttachmentId id,
        string by,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Replaces an Attachment with newly-uploaded bytes. Creates a new Attachment row
    /// (same dedup rules), sets ReplacesAttachmentId on the new row to point at the
    /// old, sets ReplacedByAttachmentId on the old row to point at the new, and
    /// transitions the old row's status to Superseded. DocumentRef rows pointing
    /// at the old attachment are NOT automatically updated — the caller (the cluster
    /// that owns the DocumentRef) decides whether to detach + re-attach or keep
    /// the Superseded reference for audit trail.
    /// </summary>
    Task<ReplaceResult> ReplaceAsync(
        string tenantId,
        AttachmentId existingId,
        ReadOnlyMemory<byte> newBytes,
        string newOriginalFilename,
        string by,
        CancellationToken ct = default);
}

public sealed record UploadResult(
    Attachment? Attachment,
    bool WasDeduplicated,         // true when the upload matched an existing Active row by (tenantId, contentHash)
    UploadError Error,
    string? Detail);

public enum UploadError
{
    None,
    EmptyBytes,
    PolicyRejectedMime,                  // surfaced from IMimeTypeAndSizePolicy in PR 3
    PolicyRejectedSize,                  // single-attachment cap exceeded
    PolicyRejectedTenantQuota,           // cumulative tenant quota exceeded
    PolicyRejectedFilename,              // path-traversal or other invalid filename
    StoragePutFailed,                    // IBlobStore.PutAsync threw or returned an error
    UnknownTenant,                       // tenant-scope check failed
}

public sealed record GetBytesResult(
    ReadOnlyMemory<byte>? Bytes,
    GetBytesError Error,
    string? Detail);

public enum GetBytesError
{
    None,
    UnknownAttachment,
    StorageMiss,                         // CAS doesn't have the bytes locally
    Tombstoned,                          // attachment is logically deleted
    TenantScopeViolation,                // attachment's tenantId != active tenant
}

public sealed record DetachResult(
    Attachment? TombstonedAttachment,
    DetachError Error,
    string? Detail);

public enum DetachError
{
    None,
    UnknownAttachment,
    AlreadyTombstoned,
    HasActiveDocumentRefs,               // caller must detach refs first
    TenantScopeViolation,
}

public sealed record ReplaceResult(
    Attachment? NewAttachment,
    Attachment? SupersededAttachment,
    ReplaceError Error,
    string? Detail);

public enum ReplaceError
{
    None,
    UnknownAttachment,
    AlreadySuperseded,
    AlreadyTombstoned,
    UploadFailed,                        // wraps UploadError
    TenantScopeViolation,
}
```

#### `UploadAsync` algorithm

```text
upload(tenantId, bytes, originalFilename, sensitivity, uploadedBy):
  // Phase 1 — preconditions
  if bytes.Length == 0: return Err(EmptyBytes)
  sanitizedFilename = SanitizeFilename(originalFilename)
    // strip any path components; reject control chars; cap at 255 bytes UTF-8
  if sanitizedFilename == null: return Err(PolicyRejectedFilename)

  // Phase 2 — content hash + MIME sniffing
  contentHash = sha256(bytes).ToHexLowercase()
  sniffedMime = MimeSniffer.SniffMimeType(bytes)
    // server-side magic-byte detection — never trust filename extension
    // wrap a small allow-list-first sniffer (e.g., based on file-magic table); fall back to "application/octet-stream"

  // Phase 3 — dedup check (idempotency on (tenantId, contentHash))
  existing = await repo.GetByContentHashAsync(tenantId, contentHash)
  if existing != null && existing.Status == Active:
    return Ok(existing, WasDeduplicated: true)
    // PR 5 also emits AttachmentDeduplicated event for observability

  // Phase 4 — policy validation (PR 3 wires IMimeTypeAndSizePolicy; PR 2 calls through to a NoOp stub)
  policyResult = await policy.ValidateAsync(tenantId, sniffedMime, bytes.Length)
  if policyResult.Rejected:
    return Err(policyResult.ErrorKind, policyResult.Detail)

  // Phase 5 — choose storage tier + persist bytes (PR 3 wires IBlobStore; PR 2 calls through to an in-memory store)
  tier = StorageTierPolicy.RecommendKind(bytes.Length)
  storageRef = await persister.PutAsync(tier, tenantId, contentHash, bytes)
  if storageRef == null: return Err(StoragePutFailed)

  // Phase 6 — persist Attachment row
  att = new Attachment {
    Id = AttachmentId.New(),
    TenantId = tenantId,
    StorageRef = storageRef,
    ContentHash = contentHash,
    MimeType = sniffedMime,
    SizeBytes = bytes.Length,
    OriginalFilename = sanitizedFilename,
    Sensitivity = sensitivity,
    Status = AttachmentStatus.Active,
    CreatedAtUtc = systemClock.GetCurrentInstant(),
    CreatedBy = uploadedBy,
    UpdatedAtUtc = systemClock.GetCurrentInstant(),
    UpdatedBy = uploadedBy,
  }
  await repo.UpsertAsync(att)

  // Phase 7 — emit event (PR 5 wires foundation-events; PR 2 emits to a local stub)
  await events.PublishAsync(new AttachmentUploadedEvent(att.Id, tenantId, contentHash, sniffedMime, bytes.Length))

  return Ok(att, WasDeduplicated: false)
```

#### `GetBytesAsync` algorithm

```text
getBytes(tenantId, attachmentId):
  att = await repo.GetByIdAsync(tenantId, attachmentId)
  if att == null: return Err(UnknownAttachment)
  if att.TenantId != tenantId: return Err(TenantScopeViolation)
    // defense-in-depth — repo also filters by tenant, but double-check at service
  if att.Status == Tombstoned: return Err(Tombstoned)
  bytes = await persister.GetAsync(att.StorageRef)
  if bytes == null: return Err(StorageMiss)
  return Ok(bytes)
```

#### `DetachAsync` algorithm

```text
detach(tenantId, attachmentId, by, reason):
  att = await repo.GetByIdAsync(tenantId, attachmentId)
  if att == null: return Err(UnknownAttachment)
  if att.TenantId != tenantId: return Err(TenantScopeViolation)
  if att.Status == Tombstoned: return Err(AlreadyTombstoned)

  // referential-integrity check — caller must detach refs first
  activeRefs = await docRefRepo.QueryActiveForAttachmentAsync(tenantId, attachmentId)
  if activeRefs.Count > 0: return Err(HasActiveDocumentRefs)

  AttachmentStatusTransitions.EnsureAllowed(att.Status, AttachmentStatus.Tombstoned)
  tombstoned = att with {
    Status = AttachmentStatus.Tombstoned,
    DeletedAtUtc = systemClock.GetCurrentInstant(),
    DeletedBy = by,
    DeletedReason = reason,
    UpdatedAtUtc = systemClock.GetCurrentInstant(),
    UpdatedBy = by,
  }
  await repo.UpsertAsync(tombstoned)
  await events.PublishAsync(new AttachmentDetachedEvent(tombstoned.Id, tenantId, reason))
  return Ok(tombstoned)
```

(Note: PR 2 ships a stub `docRefRepo` that always returns empty for the integrity check, until PR 4 lands the real `IDocumentRefRepository`. Mark with a `// TODO: replace stub when PR 4 lands` comment.)

#### `ReplaceAsync` algorithm

```text
replace(tenantId, existingId, newBytes, newOriginalFilename, by):
  existing = await repo.GetByIdAsync(tenantId, existingId)
  if existing == null: return Err(UnknownAttachment)
  if existing.TenantId != tenantId: return Err(TenantScopeViolation)
  if existing.Status == Superseded: return Err(AlreadySuperseded)
  if existing.Status == Tombstoned: return Err(AlreadyTombstoned)

  // Upload the new bytes (may dedup with another existing attachment)
  uploadResult = await UploadAsync(tenantId, newBytes, newOriginalFilename, existing.Sensitivity, by)
  if uploadResult.Error != UploadError.None:
    return Err(UploadFailed, uploadResult.Detail)
  newAtt = uploadResult.Attachment

  // Set replacement pointers (transactionally)
  AttachmentStatusTransitions.EnsureAllowed(existing.Status, AttachmentStatus.Superseded)
  newAtt = newAtt with {
    ReplacesAttachmentId = existing.Id,
    UpdatedAtUtc = systemClock.GetCurrentInstant(),
    UpdatedBy = by,
  }
  supersededExisting = existing with {
    Status = AttachmentStatus.Superseded,
    ReplacedByAttachmentId = newAtt.Id,
    UpdatedAtUtc = systemClock.GetCurrentInstant(),
    UpdatedBy = by,
  }
  await repo.UpsertAsync(newAtt)
  await repo.UpsertAsync(supersededExisting)

  await events.PublishAsync(new AttachmentReplacedEvent(
    NewAttachmentId: newAtt.Id, OldAttachmentId: supersededExisting.Id, TenantId: tenantId, By: by))
  return Ok(newAtt, supersededExisting)
```

#### In-memory repository (full impl)

**`Services/InMemoryAttachmentRepository.cs`**:

```csharp
public sealed class InMemoryAttachmentRepository : IAttachmentRepository
{
    // (tenantId, attachmentId) -> Attachment:
    private readonly ConcurrentDictionary<(string, AttachmentId), Attachment> _byId = new();

    // (tenantId, contentHash) -> AttachmentId of the Active attachment (for dedup):
    private readonly ConcurrentDictionary<(string, string), AttachmentId> _byContentHashActive = new();

    public Task<Attachment?> GetByIdAsync(string tenantId, AttachmentId id, CancellationToken ct = default)
    {
        _byId.TryGetValue((tenantId, id), out var att);
        return Task.FromResult(att);
    }

    public Task<Attachment?> GetByContentHashAsync(string tenantId, string contentHash, CancellationToken ct = default)
    {
        if (!_byContentHashActive.TryGetValue((tenantId, contentHash), out var id))
            return Task.FromResult<Attachment?>(null);
        return GetByIdAsync(tenantId, id, ct);
    }

    public Task<IReadOnlyList<Attachment>> QueryActiveAsync(
        string tenantId, Sensitivity? minSensitivity = null, CancellationToken ct = default)
    {
        var rows = _byId
            .Where(kv => kv.Key.Item1 == tenantId && kv.Value.Status == AttachmentStatus.Active)
            .Where(kv => minSensitivity is null || kv.Value.Sensitivity >= minSensitivity)
            .Select(kv => kv.Value)
            .ToList();
        return Task.FromResult<IReadOnlyList<Attachment>>(rows);
    }

    public Task UpsertAsync(Attachment attachment, CancellationToken ct = default)
    {
        _byId[(attachment.TenantId, attachment.Id)] = attachment;
        var contentKey = (attachment.TenantId, attachment.ContentHash);
        if (attachment.Status == AttachmentStatus.Active)
            _byContentHashActive[contentKey] = attachment.Id;
        else
            // remove only if THIS attachment owns the dedup key (don't bump a different active by same hash):
            _byContentHashActive.TryRemove(new KeyValuePair<(string, string), AttachmentId>(contentKey, attachment.Id));
        return Task.CompletedTask;
    }

    public Task TombstoneAsync(string tenantId, AttachmentId id, string by, string? reason, CancellationToken ct = default)
    {
        if (!_byId.TryGetValue((tenantId, id), out var existing))
            return Task.CompletedTask;
        var tombstoned = existing with
        {
            Status = AttachmentStatus.Tombstoned,
            DeletedAtUtc = SystemClock.Instance.GetCurrentInstant(),
            DeletedBy = by,
            DeletedReason = reason,
            UpdatedAtUtc = SystemClock.Instance.GetCurrentInstant(),
            UpdatedBy = by,
        };
        return UpsertAsync(tombstoned, ct);
    }

    public Task<long> GetTenantTotalSizeBytesAsync(string tenantId, CancellationToken ct = default)
    {
        var total = _byId
            .Where(kv => kv.Key.Item1 == tenantId && kv.Value.Status == AttachmentStatus.Active)
            .Sum(kv => kv.Value.SizeBytes);
        return Task.FromResult(total);
    }
}
```

#### Stub services (in this PR; promoted in PR 3 + PR 5)

**`Services/IAttachmentBytePersister.cs`** — internal interface that PR 2 introduces as a thin wrapper over `IBlobStore`. In PR 2 the impl is a pure in-memory persister; in PR 3 it's replaced by `BlobStoreAttachmentBytePersister` that wraps the production `IBlobStore` + tenant-scoping:

```csharp
internal interface IAttachmentBytePersister
{
    Task<StorageRef?> PutAsync(StorageRefKind tier, string tenantId, string contentHash, ReadOnlyMemory<byte> bytes, CancellationToken ct = default);
    Task<ReadOnlyMemory<byte>?> GetAsync(StorageRef storageRef, CancellationToken ct = default);
}

internal sealed class InMemoryAttachmentBytePersister : IAttachmentBytePersister
{
    private readonly ConcurrentDictionary<string, byte[]> _byContentHash = new();
    // ... straightforward; tier is ignored in v1 in-memory (everything is "inline")
}
```

**`Services/IMimeTypeAndSizePolicy.cs`** (stub):

```csharp
public interface IMimeTypeAndSizePolicy
{
    Task<PolicyResult> ValidateAsync(string tenantId, string sniffedMime, long sizeBytes, CancellationToken ct = default);
}

public sealed record PolicyResult(
    bool Rejected,
    UploadError ErrorKind,           // None when not rejected
    string? Detail);

internal sealed class PermissivePolicyStub : IMimeTypeAndSizePolicy
{
    public Task<PolicyResult> ValidateAsync(string _, string __, long ___, CancellationToken ____)
        => Task.FromResult(new PolicyResult(Rejected: false, ErrorKind: UploadError.None, Detail: null));
}
```

(Promoted to a real implementation in PR 3.)

**`Services/IDocsEventPublisher.cs`** + `InMemoryDocsEventPublisher` (mirrors the AR `IInvoiceEventPublisher` pattern); promoted in PR 5 to wrap `DomainEventEnvelope<TPayload>` from `foundation-events`.

#### Event records introduced

```csharp
public sealed record AttachmentUploadedEvent(
    AttachmentId AttachmentId,
    string TenantId,
    string ContentHash,
    string MimeType,
    long SizeBytes);

public sealed record AttachmentDetachedEvent(
    AttachmentId AttachmentId,
    string TenantId,
    string? Reason);

public sealed record AttachmentReplacedEvent(
    AttachmentId NewAttachmentId,
    AttachmentId OldAttachmentId,
    string TenantId,
    string By);
```

#### DI registration

Extend `ServiceCollectionExtensions.AddBlocksDocs(...)`:

```csharp
public static IServiceCollection AddBlocksDocs(this IServiceCollection services)
{
    services.AddSingleton<IAttachmentRepository, InMemoryAttachmentRepository>();
    services.AddSingleton<IAttachmentBytePersister, InMemoryAttachmentBytePersister>();
    services.AddSingleton<IMimeTypeAndSizePolicy, PermissivePolicyStub>();   // overridden in PR 3
    services.AddSingleton<IDocsEventPublisher, InMemoryDocsEventPublisher>();
    services.AddSingleton<IAttachmentService, AttachmentService>();
    return services;
}
```

#### Tests (PR 2)

`tests/AttachmentServiceUploadTests.cs`:

- `Upload_NewBytes_InsertsAttachmentAndReturnsId`.
- `Upload_DuplicateBytes_ReturnsExistingId_WasDeduplicatedTrue`.
- `Upload_DuplicateBytesAcrossTenants_DoesNotDedup` (tenant scoping in the dedup key).
- `Upload_EmptyBytes_ReturnsEmptyBytesError`.
- `Upload_ComputesSha256ContentHash`.
- `Upload_SniffsMimeFromBytes_NotFromFilename` (upload a PNG with `.txt` filename → MimeType reflects PNG).
- `Upload_SanitizesPathTraversal_InOriginalFilename` (e.g., `../etc/passwd` is rejected or sanitized to `passwd`).
- `Upload_ChoosesInlineTier_When1MbOrLess`.
- `Upload_EmitsAttachmentUploadedEvent`.
- `Upload_PreservesOriginalFilenameAfterSanitization`.
- `Upload_AssignsCreatedAtUtcAndCreatedBy`.

`tests/AttachmentServiceGetBytesTests.cs`:

- `GetBytes_RoundTripsBytesById`.
- `GetBytes_UnknownAttachment_ReturnsUnknownAttachmentError`.
- `GetBytes_TombstonedAttachment_ReturnsTombstonedError`.
- `GetBytes_TenantScopeViolation_ReturnsError` (attempting cross-tenant read).

`tests/AttachmentServiceDetachTests.cs`:

- `Detach_HappyPath_TombstonesAttachment`.
- `Detach_AlreadyTombstoned_ReturnsAlreadyTombstonedError`.
- `Detach_UnknownAttachment_ReturnsUnknownAttachmentError`.
- `Detach_EmitsAttachmentDetachedEvent`.
- `Detach_PreservesBytes_DoesNotCallBlobStoreUnpinOrDelete` (logical delete only; physical GC is out of scope).

`tests/AttachmentServiceReplaceTests.cs`:

- `Replace_HappyPath_NewAttachmentAndSupersededPointers`.
- `Replace_SupersededOriginalStatus_IsSuperseded`.
- `Replace_NewAttachmentHasReplacesAttachmentIdPointer`.
- `Replace_OriginalAttachmentHasReplacedByAttachmentIdPointer`.
- `Replace_AlreadySuperseded_ReturnsAlreadySupersededError`.
- `Replace_EmitsAttachmentReplacedEvent`.
- `Replace_NewBytesIdenticalToOld_StillReplaces_NotDeduplicated` (replacement intent is explicit; no dedup short-circuit).

`tests/AttachmentRepositoryTests.cs`:

- `UpsertAndGetById_RoundTrips`.
- `GetByContentHash_ReturnsActiveAttachment`.
- `GetByContentHash_DoesNotReturnSupersededOrTombstoned`.
- `Tombstone_RemovesDedupKeyOwnership_AllowsNewActiveSameHash` (when re-uploaded with new id).
- `GetTenantTotalSizeBytes_SumsActiveOnly_NotSupersededOrTombstoned`.

Total new tests this PR: ~28.

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests still pass.
- New tests pass.
- A regression: two `IAttachmentService` calls with byte-identical content for the same tenant produce one Attachment row.
- A regression: same byte-identical content uploaded under two distinct tenants produces two distinct Attachment rows.

#### Do NOT in this PR

- Do NOT wire the real `IBlobStore` from `foundation/Blobs/`. PR 3 ships the production persister.
- Do NOT introduce per-tenant quotas. PR 3 ships the policy with quotas.
- Do NOT introduce `DocumentRef` or reference its repository as a non-stub. PR 4 lands the real `IDocumentRefRepository`; PR 2's `DetachAsync` calls the stub.
- Do NOT introduce thumbnail generation. v1 always leaves `ThumbnailRef = null`.
- Do NOT introduce encryption. The bytes go to the persister plaintext; encryption is a follow-on workstream.

---

### PR 3 — IBlobStore wiring + MIME/size policy + tenant quotas — **security-engineering council MANDATORY**

**Estimated effort:** ~2–3h
**Scope:** wire `Sunfish.Foundation.Blobs.IBlobStore` + `FileSystemBlobStore` as the production persister; implement `IMimeTypeAndSizePolicy` with default whitelist + per-attachment cap + per-tenant cumulative quota; introduce server-side MIME sniffing
**Commit subject:** `feat(blocks-docs): wire foundation IBlobStore + MIME/size policy + tenant quotas (defense-in-depth)`
**Depends on:** PR 2 merged
**Pre-merge council:** **MANDATORY** — security-engineering. Council review must focus on:
  - Tenant scoping at the service layer (the underlying CAS is tenant-agnostic; the boundary is at `IAttachmentService` and the `Attachment.tenantId` check)
  - MIME policy as defense-in-depth (server-side sniffing, never trust filename extension)
  - Size cap enforcement (per-attachment AND cumulative tenant)
  - Filename sanitization (path traversal prevention)
  - The CAS directory structure (no leaks across tenants in the directory tree on disk)
  - No log entries containing attachment bytes or full content-hash values that could become tracking tokens

**Branch:** `cob/blocks-docs-blobstore-wiring`

#### Production byte persister

**`Services/BlobStoreAttachmentBytePersister.cs`** — replaces the InMemory stub from PR 2:

```csharp
internal sealed class BlobStoreAttachmentBytePersister : IAttachmentBytePersister
{
    private readonly IBlobStore _foundationBlobStore;          // injected from foundation/Blobs/
    private readonly BlocksDocsOptions _options;
    private readonly ITenantCasDirectoryResolver _dirResolver; // resolves the per-tenant subdirectory for fs-CAS

    public BlobStoreAttachmentBytePersister(
        IBlobStore foundationBlobStore,
        BlocksDocsOptions options,
        ITenantCasDirectoryResolver dirResolver)
    {
        _foundationBlobStore = foundationBlobStore;
        _options = options;
        _dirResolver = dirResolver;
    }

    public async Task<StorageRef?> PutAsync(
        StorageRefKind tier, string tenantId, string contentHash,
        ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
    {
        switch (tier)
        {
            case StorageRefKind.InlineSqliteBlob:
                // V1 in-memory: store under the foundation IBlobStore but mark the StorageRef as "inline"
                // so the metadata layer remembers the policy decision.
                // (When the SQLite-persistence package lands, this branch writes to a dedicated SQLite blob column.)
                var cid = await _foundationBlobStore.PutAsync(bytes, ct);
                return new StorageRef.InlineSqliteBlob(ContentHash: contentHash, SizeBytes: bytes.Length);

            case StorageRefKind.FsContentAddressed:
                // The foundation FileSystemBlobStore handles the actual filesystem write via content-addressed
                // paths. We MUST NOT bypass it — the foundation primitive owns hash addressing + directory layout.
                cid = await _foundationBlobStore.PutAsync(bytes, ct);
                // Compute the relative path the foundation store would use, for the StorageRef's relPath:
                var relPath = _dirResolver.ComputeRelPath(tenantId, contentHash);
                return new StorageRef.FsContentAddressed(
                    ContentHash: contentHash, SizeBytes: bytes.Length, RelPath: relPath);

            case StorageRefKind.ExternalUri:
                // v1: not supported by the production persister. Caller may construct an ExternalUri StorageRef
                // directly via a follow-on API for opt-in external storage; v1 substrate path returns null.
                return null;

            default:
                throw new ArgumentOutOfRangeException(nameof(tier));
        }
    }

    public async Task<ReadOnlyMemory<byte>?> GetAsync(StorageRef storageRef, CancellationToken ct = default)
    {
        // Resolve via content hash through the foundation primitive (which handles its own caching + filesystem
        // path resolution). We must NOT path-traverse manually — the foundation primitive owns the directory layout.
        if (string.IsNullOrEmpty(storageRef.ContentHash)) return null;
        var cid = ParseCidFromContentHash(storageRef.ContentHash);
        return await _foundationBlobStore.GetAsync(cid, ct);
    }

    private static Cid ParseCidFromContentHash(string contentHash) => /* mirror Cid.Parse */ ...;
}
```

**Tenant boundary discipline** (per the §Pre-merge council scope):

- The foundation `IBlobStore` is **content-addressed** — its primary key is `Cid`, not `(tenantId, Cid)`. This is correct at the bytes level (sha256 is the address; deduplication across tenants for identical bytes is mathematically inevitable AND desirable for storage efficiency). However: **the access-control boundary is enforced at the `IAttachmentService` layer.** Every `GetAsync(AttachmentId)` reads the `Attachment` row first; the row carries `tenantId`; the service refuses to surface the bytes if the active tenant scope doesn't match. **Council review must confirm this layered approach is sound** — if not, an alternative is to maintain a per-tenant separate `IBlobStore` instance (more storage cost; rejects cross-tenant dedup).
- **XO recommendation:** ship with the layered model (single IBlobStore; tenant boundary at service layer); document the trade-off explicitly; flag for revisit if Phase 4 (multi-tenant managed hosting) introduces stronger isolation requirements.

#### MIME + size policy implementation

**`Services/MimeTypeAndSizePolicy.cs`** — replaces the `PermissivePolicyStub` from PR 2:

```csharp
public sealed class MimeTypeAndSizePolicy : IMimeTypeAndSizePolicy
{
    private readonly BlocksDocsOptions _options;
    private readonly IAttachmentRepository _repo;

    public MimeTypeAndSizePolicy(BlocksDocsOptions options, IAttachmentRepository repo)
    {
        _options = options;
        _repo = repo;
    }

    public async Task<PolicyResult> ValidateAsync(
        string tenantId, string sniffedMime, long sizeBytes, CancellationToken ct = default)
    {
        // 1. MIME whitelist (per-tenant config; default whitelist if no override):
        var allowedMimes = _options.GetAllowedMimeTypes(tenantId);
        if (!allowedMimes.Contains(sniffedMime, StringComparer.OrdinalIgnoreCase))
            return Reject(UploadError.PolicyRejectedMime, $"MIME type '{sniffedMime}' is not in tenant whitelist");

        // 2. Single-attachment size cap (default 100 MB; per-tenant overridable):
        var maxAttachmentBytes = _options.GetMaxAttachmentBytes(tenantId);
        if (sizeBytes > maxAttachmentBytes)
            return Reject(UploadError.PolicyRejectedSize,
                $"Attachment size {sizeBytes} exceeds per-attachment cap {maxAttachmentBytes}");

        // 3. Tenant cumulative quota (default unlimited; per-tenant overridable):
        var tenantQuotaBytes = _options.GetTenantQuotaBytes(tenantId);
        if (tenantQuotaBytes is long quota)
        {
            var currentTotal = await _repo.GetTenantTotalSizeBytesAsync(tenantId, ct);
            if (currentTotal + sizeBytes > quota)
                return Reject(UploadError.PolicyRejectedTenantQuota,
                    $"Tenant '{tenantId}' quota exceeded: {currentTotal} + {sizeBytes} > {quota}");
        }

        return new PolicyResult(Rejected: false, ErrorKind: UploadError.None, Detail: null);
    }

    private static PolicyResult Reject(UploadError kind, string detail)
        => new(Rejected: true, ErrorKind: kind, Detail: detail);
}
```

#### Default MIME whitelist

The default whitelist (used when a tenant has no override) targets the common attachment use cases identified in the Stage 02 §1 cluster table:

```csharp
public static class DefaultMimeWhitelist
{
    public static readonly IReadOnlySet<string> Defaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Documents:
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "text/plain",
        "text/markdown",
        "text/csv",
        "application/json",
        // Images (inspection photos, marketing DAM v1 floor):
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic",
        "image/svg+xml",
        // Compressed (uncommon but expected for migration imports):
        "application/zip",
    };
}
```

Explicitly **NOT** in the default whitelist (defense-in-depth):

- `application/x-msdownload`, `application/x-executable`, `application/x-sh`, `application/octet-stream` (no executables; the `application/octet-stream` exclusion forces the sniffer to return a recognized MIME — fallback to octet-stream signals an unknown file type, which the policy rejects).
- `text/html`, `application/javascript`, `text/javascript` (no live web content; XSS risk if rendered in-app).
- `application/x-shockwave-flash` (deprecated; no Flash).

The tenant may override (`BlocksDocsOptions.SetTenantAllowedMimeTypes(tenantId, customSet)`) but the default deny-by-default posture means any tenant that does nothing is safe.

#### Filename sanitization

**`Services/FilenameSanitizer.cs`**:

```csharp
public static class FilenameSanitizer
{
    private static readonly char[] DirSeparators = { '/', '\\', ':' };
    private static readonly char[] ControlChars  = Enumerable.Range(0, 32).Select(i => (char)i).ToArray();

    public static string? Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1. Strip any path components — keep only the leaf name:
        var leaf = raw;
        var lastSep = raw.LastIndexOfAny(DirSeparators);
        if (lastSep >= 0) leaf = raw.Substring(lastSep + 1);
        if (string.IsNullOrEmpty(leaf)) return null;

        // 2. Reject if any control char present:
        if (leaf.IndexOfAny(ControlChars) >= 0) return null;

        // 3. Reject "." / ".." / reserved Windows device names:
        if (leaf is "." or "..") return null;
        var withoutExt = Path.GetFileNameWithoutExtension(leaf).ToUpperInvariant();
        if (ReservedWindowsNames.Contains(withoutExt)) return null;

        // 4. Cap at 255 bytes UTF-8 (truncate from the leaf, preserving extension when possible):
        var bytes = Encoding.UTF8.GetByteCount(leaf);
        if (bytes > 255)
        {
            // truncate ... (preserve extension if possible)
            leaf = TruncatePreservingExtension(leaf, maxBytes: 255);
        }

        return leaf;
    }

    private static readonly IReadOnlySet<string> ReservedWindowsNames = new HashSet<string>
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static string TruncatePreservingExtension(string s, int maxBytes) => /* impl */ ...;
}
```

#### MIME sniffer

**`Services/MimeSniffer.cs`** — a small magic-byte detector covering the default-whitelist set. v1 implementation: a hand-rolled detector for ~15 common types; fall back to `application/octet-stream` (which the policy rejects). A future hand-off may wire `Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider` + extension cross-check, or a richer library (e.g., HeyRed.Mime), if needed. For v1, the hand-rolled detector is sufficient (and ships with no new package dependencies).

```csharp
public static class MimeSniffer
{
    public static string SniffMimeType(ReadOnlyMemory<byte> bytes)
    {
        var span = bytes.Span;
        if (span.Length < 4) return "application/octet-stream";

        // PDF: 25 50 44 46 (%PDF):
        if (span[0] == 0x25 && span[1] == 0x50 && span[2] == 0x44 && span[3] == 0x46) return "application/pdf";

        // PNG: 89 50 4E 47:
        if (span[0] == 0x89 && span[1] == 0x50 && span[2] == 0x4E && span[3] == 0x47) return "image/png";

        // JPEG: FF D8 FF:
        if (span[0] == 0xFF && span[1] == 0xD8 && span[2] == 0xFF) return "image/jpeg";

        // WebP: 52 49 46 46 ... 57 45 42 50:
        if (span.Length > 12
            && span[0] == 0x52 && span[1] == 0x49 && span[2] == 0x46 && span[3] == 0x46
            && span[8] == 0x57 && span[9] == 0x45 && span[10] == 0x42 && span[11] == 0x50)
            return "image/webp";

        // ZIP / DOCX / XLSX: 50 4B 03 04 (PK..):
        if (span[0] == 0x50 && span[1] == 0x4B && span[2] == 0x03 && span[3] == 0x04)
        {
            // Heuristic: scan first ~512 bytes for "word/" or "xl/" markers; if found, return office MIME.
            // Otherwise return application/zip.
            // Simplest v1 impl: return application/zip; refine in follow-on.
            return "application/zip";
        }

        // Plain-text heuristic — if all bytes are printable ASCII or common UTF-8, return text/plain:
        if (IsLikelyText(span)) return "text/plain";

        return "application/octet-stream";
    }

    private static bool IsLikelyText(ReadOnlySpan<byte> span) => /* simple printable-ratio check */ ...;
}
```

(Council review may request a more thorough sniffer or a different library; XO recommendation: v1 hand-rolled covers the default whitelist; richer detection is a follow-on hardening pass.)

#### `BlocksDocsOptions`

```csharp
public sealed class BlocksDocsOptions
{
    public long InlineBlobMaxBytes { get; set; } = StorageTierPolicy.InlineThresholdBytes;          // 1 MB default
    public long ExternalBlobThresholdBytes { get; set; } = StorageTierPolicy.ExternalThresholdBytes; // 100 MB default

    // Default MIME whitelist applies when a tenant has no override:
    private IReadOnlySet<string> _defaultMimeWhitelist = DefaultMimeWhitelist.Defaults;
    private readonly ConcurrentDictionary<string, IReadOnlySet<string>> _tenantMimeWhitelists = new();

    // Default per-attachment cap (100 MB) and per-tenant overrides:
    private long _defaultMaxAttachmentBytes = 100L * 1024L * 1024L;
    private readonly ConcurrentDictionary<string, long> _tenantMaxAttachmentBytes = new();

    // Default tenant quota: null = unlimited; per-tenant overrides:
    private long? _defaultTenantQuotaBytes = null;
    private readonly ConcurrentDictionary<string, long> _tenantQuotaBytes = new();

    // CAS root directory (filesystem CAS tier):
    public string CasRootDirectory { get; set; } = string.Empty;   // host-app configures at startup

    public void SetDefaultAllowedMimeTypes(IReadOnlySet<string> mimes) => _defaultMimeWhitelist = mimes;
    public void SetTenantAllowedMimeTypes(string tenantId, IReadOnlySet<string> mimes)
        => _tenantMimeWhitelists[tenantId] = mimes;
    public IReadOnlySet<string> GetAllowedMimeTypes(string tenantId)
        => _tenantMimeWhitelists.TryGetValue(tenantId, out var s) ? s : _defaultMimeWhitelist;

    public void SetDefaultMaxAttachmentBytes(long bytes) => _defaultMaxAttachmentBytes = bytes;
    public void SetTenantMaxAttachmentBytes(string tenantId, long bytes) => _tenantMaxAttachmentBytes[tenantId] = bytes;
    public long GetMaxAttachmentBytes(string tenantId)
        => _tenantMaxAttachmentBytes.TryGetValue(tenantId, out var b) ? b : _defaultMaxAttachmentBytes;

    public void SetDefaultTenantQuotaBytes(long? bytes) => _defaultTenantQuotaBytes = bytes;
    public void SetTenantQuotaBytes(string tenantId, long bytes) => _tenantQuotaBytes[tenantId] = bytes;
    public long? GetTenantQuotaBytes(string tenantId)
        => _tenantQuotaBytes.TryGetValue(tenantId, out var b) ? b : _defaultTenantQuotaBytes;
}
```

#### DI registration updates

```csharp
public static IServiceCollection AddBlocksDocs(
    this IServiceCollection services,
    Action<BlocksDocsOptions>? configure = null)
{
    var options = new BlocksDocsOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);

    services.AddSingleton<IAttachmentRepository, InMemoryAttachmentRepository>();
    services.AddSingleton<ITenantCasDirectoryResolver, TenantCasDirectoryResolver>();
    services.AddSingleton<IAttachmentBytePersister, BlobStoreAttachmentBytePersister>();
    services.AddSingleton<IMimeTypeAndSizePolicy, MimeTypeAndSizePolicy>();
    services.AddSingleton<IDocsEventPublisher, InMemoryDocsEventPublisher>();
    services.AddSingleton<IAttachmentService, AttachmentService>();
    return services;
}
```

The consumer's host MUST register an `IBlobStore` implementation (e.g., `FileSystemBlobStore` from `packages/foundation/Blobs/`) BEFORE `AddBlocksDocs(...)`. A startup-check at the first `AddBlocksDocs` resolution throws a helpful exception if `IBlobStore` is unregistered.

#### Tests (PR 3)

`tests/MimeTypeAndSizePolicyTests.cs`:

- `ValidatesMime_AcceptsPdf_RejectsExecutable`.
- `ValidatesMime_AcceptsImageJpeg_RejectsHtml`.
- `ValidatesMime_RejectsApplicationOctetStream_DefaultPolicy` (unknown file type rejected).
- `ValidatesSize_AcceptsBelowCap`.
- `ValidatesSize_RejectsAtAndAboveCap`.
- `ValidatesQuota_AcceptsBelowQuota`.
- `ValidatesQuota_RejectsCumulativeExceedingQuota`.
- `ValidatesQuota_NoCap_DefaultUnlimited_AlwaysAccepts`.
- `TenantOverride_AllowsDifferentMimes_PerTenant`.
- `TenantOverride_DifferentSizeCap_PerTenant`.
- `TenantOverride_DifferentQuota_PerTenant`.

`tests/MimeSnifferTests.cs`:

- `Sniff_Pdf_ReturnsApplicationPdf`.
- `Sniff_Png_ReturnsImagePng`.
- `Sniff_Jpeg_ReturnsImageJpeg`.
- `Sniff_WebP_ReturnsImageWebP`.
- `Sniff_Zip_ReturnsApplicationZip`.
- `Sniff_Text_ReturnsTextPlain`.
- `Sniff_UnknownBinary_ReturnsApplicationOctetStream`.
- `Sniff_EmptyOrTooShort_ReturnsApplicationOctetStream`.
- `Sniff_DoesNotTrustFilenameExtension` (PNG bytes uploaded as `.txt` → image/png).

`tests/FilenameSanitizerTests.cs`:

- `Sanitize_StripsPathTraversal_DotDotSlash` (`../../etc/passwd` → `passwd`).
- `Sanitize_RejectsControlChars`.
- `Sanitize_RejectsReservedWindowsNames` (CON, PRN, AUX, NUL, COM1..COM9, LPT1..LPT9).
- `Sanitize_PreservesUnicodeFilenames`.
- `Sanitize_TruncatesAt255Bytes_PreservesExtension`.
- `Sanitize_RejectsEmptyOrWhitespace`.
- `Sanitize_RejectsDotAndDoubleDot`.

`tests/BlobStoreAttachmentBytePersisterTests.cs`:

- `Put_InlineTier_WritesToFoundationBlobStore`.
- `Put_FsCasTier_WritesToFoundationBlobStore_ReturnsFsCasRef`.
- `Put_ExternalTier_ReturnsNull_NotSupportedInV1`.
- `Get_RoundTripsBytes_ViaFoundationBlobStore`.
- `Get_UnknownContentHash_ReturnsNull`.

`tests/AttachmentServiceTenantScopingTests.cs`:

- `Upload_DifferentTenants_SameBytes_BothPersisted_SeparateAttachments`.
- `GetBytes_CrossTenant_ReturnsTenantScopeViolation`.
- `Detach_CrossTenant_ReturnsTenantScopeViolation`.
- `Replace_CrossTenant_ReturnsTenantScopeViolation`.

`tests/AttachmentServicePolicyIntegrationTests.cs`:

- `Upload_RejectedByMimePolicy_DoesNotPersistAttachment`.
- `Upload_RejectedBySizePolicy_DoesNotPersistAttachment`.
- `Upload_RejectedByTenantQuota_DoesNotPersistAttachment`.
- `Upload_PolicyAcceptsAfterQuotaIncrease`.

Total new tests this PR: ~33.

#### Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests pass.
- New tests pass.
- A council security-review session is run on this PR (per §Pre-merge council). Council surface: read all of PR 3's new files; verify the tenant boundary; verify no log entries leak attachment bytes; verify the default whitelist is appropriate for the cluster's use cases.
- Spot-check: the `FileSystemBlobStore` write directory after a few uploads — confirm the on-disk layout matches what the foundation primitive produces (no tenant-id paths leak in directory names; the foundation primitive owns the layout; this package does NOT write outside that layout).

#### Do NOT in this PR

- Do NOT introduce encryption-at-rest. The bytes go to the foundation primitive plaintext.
- Do NOT introduce a tenant-quota *enforcement* on `GetBytes` (quotas are upload-time only). Reading existing bytes never fails on quota.
- Do NOT introduce a `DELETE` operation on the foundation `IBlobStore` (the foundation primitive has `UnpinAsync` not `Delete`; physical GC is a follow-on workstream).
- Do NOT redefine `IBlobStore` in this package. The foundation primitive is consumed as-is.
- Do NOT trust the upload's `Content-Type` header or the filename extension. Server-side sniff only.

---

### PR 4 — `DocumentRef` + `IDocumentRefService` cross-cluster API

**Estimated effort:** ~2h
**Scope:** introduce `DocumentRef` entity + `IDocumentRefService` (create / get / list-for-source-entity / detach); idempotency-key catalog entries; integration with `IAttachmentService.DetachAsync` (the PR 2 stub becomes real)
**Commit subject:** `feat(blocks-docs): DocumentRef cross-cluster surface + IDocumentRefService per Stage 02 §7`
**Depends on:** PR 3 merged (or parallel with PR 3 — XO recommendation: sequence after for council-review simplicity)
**Branch:** `cob/blocks-docs-document-ref`

#### New types

**`Models/DocumentRefId.cs`** — ULID strongly-typed id.

**`Models/DocumentRef.cs`** — cross-cluster link:

```csharp
public sealed record DocumentRef
{
    public DocumentRefId Id { get; init; }
    public string TenantId { get; init; } = string.Empty;

    // Source identity — opaque from this package's POV; the consuming cluster owns its meaning:
    public string SourceCluster { get; init; } = string.Empty;        // e.g., "blocks-financial-ar"
    public string SourceEntityId { get; init; } = string.Empty;       // e.g., InvoiceId.Value.ToString()
    public string Role { get; init; } = string.Empty;                 // e.g., "invoice-pdf", "inspection-photo"

    // Target attachment:
    public AttachmentId AttachmentId { get; init; }

    // Status (per CRDT Pattern A — monotonic):
    public DocumentRefStatus Status { get; init; } = DocumentRefStatus.Active;
    public Instant? DetachedAtUtc { get; init; }
    public string? DetachedBy { get; init; }
    public string? DetachReason { get; init; }

    // Audit:
    public Instant CreatedAtUtc { get; init; }
    public string? CreatedBy { get; init; }
    public Instant UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
}

public enum DocumentRefStatus
{
    Active,
    Detached,
}
```

**Conventions for `SourceCluster` + `SourceEntityId` + `Role`:**

- `SourceCluster` is the **package name** of the consuming cluster (e.g., `"blocks-financial-ar"`, `"blocks-property-leases"`, `"blocks-property-inspections"`). Never an abbreviation; never a UI label.
- `SourceEntityId` is the **strong id's `Value.ToString()`** (e.g., `InvoiceId.Value.ToString()` → a ULID string). Caller MUST normalize representations (e.g., uppercase ULIDs); this package does not parse the id.
- `Role` is a **kebab-case slug** describing the semantic relationship. Reserved roles per Stage 02 §7 cross-cluster contracts:
  - `invoice-pdf` — AR-issued invoice rendered to PDF
  - `bill-attachment` — AP-received bill image / PDF / supporting doc
  - `lease-contract` — Lease signed-contract PDF
  - `lease-supporting-doc` — Lease ID / proof-of-income / pet-record / etc.
  - `inspection-photo` — Inspection-deficiency photo evidence
  - `work-order-before-photo` / `work-order-after-photo` — Work-order proof
  - `work-order-receipt` — Vendor-supplied receipt for completed work
  - `signing-final-document` — `blocks-docs-signing` final signed PDF (future)
  - `wiki-page-cover` / `wiki-page-embed` — `blocks-docs-wiki` embedded binaries (future)
  - `marketing-asset` — `blocks-docs-dam` primary asset (future; the DAM may bypass DocumentRef and own its own attachment association — TBD per the DAM hand-off)

(The role-vocabulary table is not exhaustively closed at this hand-off; consuming clusters may introduce new role values via convention. PR 4 documents the registered set in `apps/docs/blocks-docs/overview.md` and reserves the role-namespace by package.)

#### `IDocumentRefService` interface

```csharp
public interface IDocumentRefService
{
    /// <summary>
    /// Creates a DocumentRef linking a source entity to an Attachment.
    /// Idempotent on (tenantId, sourceCluster, sourceEntityId, attachmentId, role):
    /// a re-create with identical fields returns the existing DocumentRef id.
    /// Verifies the Attachment exists + is Active and tenant-scoped.
    /// </summary>
    Task<CreateRefResult> CreateAsync(
        string tenantId,
        string sourceCluster,
        string sourceEntityId,
        string role,
        AttachmentId attachmentId,
        string createdBy,
        CancellationToken ct = default);

    Task<DocumentRef?> GetAsync(string tenantId, DocumentRefId id, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRef>> ListForSourceEntityAsync(
        string tenantId,
        string sourceCluster,
        string sourceEntityId,
        string? role = null,
        bool includeDetached = false,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRef>> ListForAttachmentAsync(
        string tenantId,
        AttachmentId attachmentId,
        bool includeDetached = false,
        CancellationToken ct = default);

    Task<DetachRefResult> DetachAsync(
        string tenantId,
        DocumentRefId id,
        string by,
        string? reason,
        CancellationToken ct = default);
}

public sealed record CreateRefResult(
    DocumentRef? DocumentRef,
    bool WasIdempotent,                    // true when an existing equivalent ref was returned
    CreateRefError Error,
    string? Detail);

public enum CreateRefError
{
    None,
    UnknownAttachment,
    AttachmentNotActive,                   // attachment is Superseded or Tombstoned
    TenantScopeViolation,                  // attachment.tenantId != ref.tenantId
    InvalidSourceCluster,                  // empty or malformed
    InvalidSourceEntityId,
    InvalidRole,
}

public sealed record DetachRefResult(
    DocumentRef? DetachedRef,
    DetachRefError Error,
    string? Detail);

public enum DetachRefError
{
    None,
    UnknownDocumentRef,
    AlreadyDetached,
    TenantScopeViolation,
}
```

#### Repository

**`Services/IDocumentRefRepository.cs`**:

```csharp
public interface IDocumentRefRepository
{
    Task<DocumentRef?> GetByIdAsync(string tenantId, DocumentRefId id, CancellationToken ct = default);

    Task<DocumentRef?> FindEquivalentActiveAsync(
        string tenantId, string sourceCluster, string sourceEntityId,
        string role, AttachmentId attachmentId, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRef>> QueryForSourceEntityAsync(
        string tenantId, string sourceCluster, string sourceEntityId,
        string? role, bool includeDetached, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRef>> QueryForAttachmentAsync(
        string tenantId, AttachmentId attachmentId, bool includeDetached, CancellationToken ct = default);

    Task<IReadOnlyList<DocumentRef>> QueryActiveForAttachmentAsync(
        string tenantId, AttachmentId attachmentId, CancellationToken ct = default);
        // ^ this is the one called by IAttachmentService.DetachAsync's referential-integrity check

    Task UpsertAsync(DocumentRef documentRef, CancellationToken ct = default);
}

public sealed class InMemoryDocumentRefRepository : IDocumentRefRepository { /* mirrors InMemoryAttachmentRepository */ }
```

#### Wiring update — `IAttachmentService.DetachAsync` referential integrity

PR 2 stubbed the `docRefRepo` call with always-empty-list. PR 4 replaces that wire with the real `IDocumentRefRepository.QueryActiveForAttachmentAsync(...)`. A new test in PR 4 asserts:

- `AttachmentService.DetachAsync_BlocksWhenActiveDocumentRefExists_ReturnsHasActiveDocumentRefsError`.
- `AttachmentService.DetachAsync_AllowsAfterAllRefsAreDetached`.

#### Idempotency-key catalog additions

The following entries are added to `_shared/engineering/cross-cluster-event-bus-design.md` §3.4 (`Docs.*` events) — the actual file edit ships in PR 5 alongside the DI extension; PR 4 ships the *intent* and the event-record code:

| Event | Consumers | Payload sketch | Idempotency key |
|---|---|---|---|
| `Docs.AttachmentUploaded` | (none external — internal observability) | `{ attachmentId, tenantId, contentHash, mimeType, sizeBytes }` | `attachment-uploaded:{attachmentId}` |
| `Docs.AttachmentDetached` | (none external — internal) | `{ attachmentId, tenantId, reason? }` | `attachment-detached:{attachmentId}` |
| `Docs.AttachmentReplaced` | (none external — internal) | `{ newAttachmentId, oldAttachmentId, tenantId, by }` | `attachment-replaced:{newAttachmentId}:{oldAttachmentId}` |
| `Docs.DocumentRefCreated` | source-cluster (financial-ar, financial-ap, property-leases, property-inspections, work-orders) | `{ documentRefId, tenantId, sourceCluster, sourceEntityId, role, attachmentId }` | `documentref-created:{sourceCluster}:{sourceEntityId}:{role}:{attachmentId}` |
| `Docs.DocumentRefDetached` | source-cluster | `{ documentRefId, tenantId, sourceCluster, sourceEntityId, role, attachmentId, reason? }` | `documentref-detached:{documentRefId}` |

(Format follows `cross-cluster-event-bus-design.md` §3.4 conventions: kebab-case-prefix : entityId [: occurredAtTicks if collision risk].)

#### New event records

```csharp
public sealed record DocumentRefCreatedEvent(
    DocumentRefId DocumentRefId,
    string TenantId,
    string SourceCluster,
    string SourceEntityId,
    string Role,
    AttachmentId AttachmentId);

public sealed record DocumentRefDetachedEvent(
    DocumentRefId DocumentRefId,
    string TenantId,
    string SourceCluster,
    string SourceEntityId,
    string Role,
    AttachmentId AttachmentId,
    string? Reason);
```

#### DI registration

Extend `ServiceCollectionExtensions.AddBlocksDocs(...)`:

```csharp
services.AddSingleton<IDocumentRefRepository, InMemoryDocumentRefRepository>();
services.AddSingleton<IDocumentRefService, DocumentRefService>();
```

And remove the PR 2 stub `docRefRepo` from `AttachmentService` — it now resolves `IDocumentRefRepository` directly.

#### Tests (PR 4)

`tests/DocumentRefRecordTests.cs`:

- `Construction_PreservesAllFields`.
- `Status_DefaultIsActive`.
- `DetachedAtUtc_NullByDefault`.

`tests/DocumentRefServiceCreateTests.cs`:

- `Create_HappyPath_LinksAttachmentToSource`.
- `Create_Idempotent_OnSameTuple_ReturnsExistingId_WasIdempotentTrue`.
- `Create_DifferentRole_SameAttachment_SameSource_CreatesSecondRef` (two refs allowed — different roles).
- `Create_UnknownAttachment_ReturnsUnknownAttachmentError`.
- `Create_SupersededAttachment_ReturnsAttachmentNotActiveError`.
- `Create_TombstonedAttachment_ReturnsAttachmentNotActiveError`.
- `Create_CrossTenantAttachment_ReturnsTenantScopeViolation`.
- `Create_EmptySourceCluster_ReturnsInvalidSourceClusterError`.
- `Create_EmptySourceEntityId_ReturnsInvalidSourceEntityIdError`.
- `Create_EmptyRole_ReturnsInvalidRoleError`.
- `Create_EmitsDocumentRefCreatedEvent`.

`tests/DocumentRefServiceListTests.cs`:

- `ListForSourceEntity_ReturnsAllRefs_AcrossRoles`.
- `ListForSourceEntity_FiltersBySpecificRole`.
- `ListForSourceEntity_ExcludesDetachedByDefault`.
- `ListForSourceEntity_IncludesDetached_WhenIncludeDetachedTrue`.
- `ListForAttachment_ReturnsAllRefsToAttachment_AcrossSourceClusters`.
- `ListForAttachment_ExcludesDetachedByDefault`.

`tests/DocumentRefServiceDetachTests.cs`:

- `Detach_HappyPath_SetsStatusToDetached_AndDetachedAtUtc`.
- `Detach_AlreadyDetached_ReturnsAlreadyDetachedError`.
- `Detach_UnknownDocumentRef_ReturnsUnknownDocumentRefError`.
- `Detach_CrossTenant_ReturnsTenantScopeViolation`.
- `Detach_EmitsDocumentRefDetachedEvent`.

`tests/AttachmentServiceReferentialIntegrityTests.cs`:

- `Attachment_Detach_FailsWhenActiveDocumentRefExists_ReturnsHasActiveDocumentRefsError`.
- `Attachment_Detach_SucceedsAfterAllRefsDetached`.
- `Attachment_Detach_AllowedWhenAllRefsDetachedEvenIfManyRefsExist`.

Total new tests this PR: ~24.

#### Verification

- `dotnet build` succeeds.
- All PR 1-3 tests pass.
- New tests pass.
- Integration smoke: upload an Attachment → create 3 DocumentRefs from 3 distinct source clusters → list each by source-entity returns 1 each → detach all 3 → IAttachmentService.DetachAsync succeeds → status round-trips to Tombstoned.

#### Do NOT in this PR

- Do NOT introduce cluster-specific DocumentRef helper methods (e.g., `CreateForInvoice`, `CreateForLease`). The consumers call `CreateAsync(tenantId, "blocks-financial-ar", invoiceId.Value, "invoice-pdf", attId, createdBy)` directly — that's the contract.
- Do NOT introduce automatic-detach-on-attachment-replace. The `IAttachmentService.ReplaceAsync` operation deliberately leaves DocumentRefs alone (caller decides whether to detach + re-attach to the new Attachment, or preserve the Superseded reference for audit).
- Do NOT validate that `SourceEntityId` is a real entity in the source cluster — that's the source cluster's responsibility. This package treats source-entity-id as an opaque string.

---

### PR 5 — DI extension `AddBlocksDocs(...)` + docs + cross-cluster event-bus catalog editorial

**Estimated effort:** ~1–2h
**Scope:** finalize `AddBlocksDocs(...)` overloads + startup checks; ship `apps/docs/blocks-docs/overview.md`; ship `packages/blocks-docs/README.md`; edit `_shared/engineering/cross-cluster-event-bus-design.md` §3.4 to add the substrate events; wire `IDocsEventPublisher` to `foundation-events.DomainEventEnvelope<TPayload>` when available (otherwise leave the stub + flag)
**Commit subject:** `feat(blocks-docs): AddBlocksDocs DI extension + docs page + cross-cluster event-bus editorial`
**Depends on:** PR 4 merged
**Branch:** `cob/blocks-docs-di-and-docs`

#### `AddBlocksDocs` final shape

Mirrors the AR + AP DI extension pattern:

```csharp
public static IServiceCollection AddBlocksDocs(
    this IServiceCollection services,
    Action<BlocksDocsOptions>? configure = null)
{
    var options = new BlocksDocsOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);

    // Repositories (in-memory v1; SQLite-backed when blocks-docs-persistence ships):
    services.AddSingleton<IAttachmentRepository, InMemoryAttachmentRepository>();
    services.AddSingleton<IDocumentRefRepository, InMemoryDocumentRefRepository>();

    // Byte persister (wraps the foundation IBlobStore the consumer registered separately):
    services.AddSingleton<ITenantCasDirectoryResolver, TenantCasDirectoryResolver>();
    services.AddSingleton<IAttachmentBytePersister, BlobStoreAttachmentBytePersister>();

    // Policy + sniffer + sanitizer:
    services.AddSingleton<IMimeTypeAndSizePolicy, MimeTypeAndSizePolicy>();

    // Event publisher (stub or foundation-events bridge):
    services.AddSingleton<IDocsEventPublisher, InMemoryDocsEventPublisher>();
    // ↑ Note: if foundation-events is wired in this host, the consumer may override this registration
    //   with a foundation-events bridge. The follow-on hand-off ships the bridge directly.

    // Services:
    services.AddSingleton<IAttachmentService, AttachmentService>();
    services.AddSingleton<IDocumentRefService, DocumentRefService>();

    return services;
}
```

#### Startup check

A small hosted-service or DI sentinel validates at first resolution:

- `IBlobStore` is registered (from `foundation/Blobs/` or another provider).
- `BlocksDocsOptions.CasRootDirectory` is set (if any tenant policy will use the FsCas tier).

If either is missing, throw a helpful exception naming the missing registration + the canonical fix.

#### Docs page — `apps/docs/blocks-docs/overview.md`

```markdown
# blocks-docs

Cross-cluster attachment substrate for the Sunfish Anchor native document
domain (ADR 0088 §1). Provides the binary-storage primitive consumed by AR
invoices, AP bills, leases, inspections, work-orders, and the
`blocks-docs-*` sub-packages.

## Overview

This package is the canonical Attachment + DocumentRef surface of the
`blocks-docs-*` cluster per ADR 0088 §1. It provides:

- `Attachment` — universal binary-attachment primitive; content-addressed; immutable post-upload.
- `StorageRef` — discriminated union: inline SQLite blob, filesystem CAS, or external URI.
- `DocumentRef` — cross-cluster foreign-key surface; (sourceCluster, sourceEntityId, role) tuple.
- `IAttachmentService` — upload / get / detach / replace; content-hash dedup; tenant-scoped.
- `IDocumentRefService` — create / list / detach; cross-cluster contract.
- `IMimeTypeAndSizePolicy` — defense-in-depth MIME + size + quota policy per tenant.
- `MimeSniffer` — server-side magic-byte detection (never trust filename extension).
- `FilenameSanitizer` — path-traversal + reserved-name protection.

## What's NOT in v1

- Wiki, contract-template, DAM, signing sub-packages (separate hand-offs).
- Thumbnail generation (lazy-on-display in a future hand-off).
- Encryption-at-rest (kernel-security envelope keys in a follow-on hand-off).
- Loro CRDT sync (single-node SQLite at Light tier).
- ERPNext file importer (lives in `tooling-anchor-import` orchestrator).

## Storage tiers

| Size | Tier | StorageRefKind |
|---|---|---|
| ≤ 1 MB | inline SQLite blob | InlineSqliteBlob |
| 1 MB < size ≤ 100 MB | filesystem content-addressed | FsContentAddressed |
| > 100 MB | external (opt-in; out-of-band) | ExternalUri |

(Thresholds are tunable via `BlocksDocsOptions`.)

## Default MIME whitelist

Documents: `application/pdf`, `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `application/vnd.ms-excel`, `text/plain`, `text/markdown`, `text/csv`, `application/json`.

Images: `image/jpeg`, `image/png`, `image/webp`, `image/heic`, `image/svg+xml`.

Compressed: `application/zip`.

**Explicitly excluded** (defense-in-depth): executables, HTML/JavaScript, Flash, `application/octet-stream`.

Tenants may override per `BlocksDocsOptions.SetTenantAllowedMimeTypes(...)`.

## Tenant boundary

The substrate is tenant-scoped at the service layer. The foundation
`IBlobStore` is content-addressed (sha256) and tenant-agnostic at the bytes
level — identical bytes uploaded under two tenants share one CAS body but
produce two distinct Attachment rows (different tenantId). The
access-control boundary is enforced at `IAttachmentService` /
`IDocumentRefService`: every Get / Detach / Replace verifies the active
tenant scope matches the row's tenantId.

## Cross-cluster usage

Every cluster that wants to attach files to its records consumes this
substrate via:

```csharp
// 1. Upload the bytes:
var upload = await _attachmentSvc.UploadAsync(
    tenantId, bytes, originalFilename, sensitivity, uploadedBy);

// 2. Link the resulting Attachment to your record:
var refResult = await _documentRefSvc.CreateAsync(
    tenantId,
    sourceCluster: "blocks-financial-ar",
    sourceEntityId: invoice.Id.Value.ToString(),
    role: "invoice-pdf",
    attachmentId: upload.Attachment.Id,
    createdBy: userId);

// 3. Later: list attachments for the invoice:
var refs = await _documentRefSvc.ListForSourceEntityAsync(
    tenantId, "blocks-financial-ar", invoice.Id.Value.ToString());

// 4. To download an attachment:
var bytes = await _attachmentSvc.GetBytesAsync(tenantId, refs[0].AttachmentId);
```

## Registration

```csharp
services
    .AddSingleton<IBlobStore, FileSystemBlobStore>()    // from foundation/Blobs/
    .AddBlocksDocs(opts =>
    {
        opts.CasRootDirectory = "/var/lib/anchor/blobs";
        // tenant-specific overrides:
        opts.SetTenantMaxAttachmentBytes(tenantId: "demo", bytes: 50L * 1024 * 1024);
        opts.SetTenantQuotaBytes(tenantId: "demo", bytes: 5L * 1024 * 1024 * 1024);  // 5 GB
    });
```

## Reserved DocumentRef roles

| Role | Source cluster | Meaning |
|---|---|---|
| `invoice-pdf` | `blocks-financial-ar` | Issued invoice rendered PDF |
| `bill-attachment` | `blocks-financial-ap` | Vendor bill image / PDF |
| `lease-contract` | `blocks-property-leases` | Signed lease contract PDF |
| `lease-supporting-doc` | `blocks-property-leases` | Tenant ID / proof-of-income / etc. |
| `inspection-photo` | `blocks-property-inspections` | Inspection deficiency photo |
| `work-order-before-photo` | `blocks-work-orders` | Pre-work proof photo |
| `work-order-after-photo` | `blocks-work-orders` | Post-work proof photo |
| `work-order-receipt` | `blocks-work-orders` | Vendor receipt |
| `signing-final-document` | `blocks-docs-signing` (future) | Final signed PDF |
| `wiki-page-cover` | `blocks-docs-wiki` (future) | Wiki page cover image |
| `wiki-page-embed` | `blocks-docs-wiki` (future) | Embedded image in wiki body |
| `marketing-asset` | `blocks-docs-dam` (future) | DAM primary asset |

Consuming clusters MAY introduce new roles via convention; reserve the
role-namespace by package (e.g., `inspection-*` for `blocks-property-inspections`).

## Algorithms

- Content-hash deduplication on upload → CRDT conventions §6
- State-machine Pattern A (Active → Superseded → Tombstoned) → CRDT conventions §7
- Server-side MIME sniffing (defense-in-depth) → see `MimeSniffer`
- Filename sanitization (path-traversal + reserved-names) → see `FilenameSanitizer`

## Related

- `packages/foundation/Blobs/IBlobStore.cs` — primitive consumed by this substrate
- `packages/foundation-events/` — `DomainEventEnvelope<TPayload>`
- `packages/blocks-people-foundation/` — `IPartyReadModel` (consumed for createdBy / uploadedBy resolution)
- `packages/blocks-financial-ar/` — first consumer (`invoice-pdf` attachments)
- `packages/blocks-financial-ap/` — second consumer (`bill-attachment`)
- Future siblings: `blocks-docs-wiki`, `blocks-docs-templates`, `blocks-docs-dam`, `blocks-docs-signing`
```

#### Editorial — `_shared/engineering/cross-cluster-event-bus-design.md`

Edit §3.4 to add the substrate-level entries from PR 4's table above (`Docs.AttachmentUploaded`, `Docs.AttachmentDetached`, `Docs.AttachmentReplaced`, `Docs.DocumentRefCreated`, `Docs.DocumentRefDetached`).

The existing §3.4 entries (Policy events, Wiki events, Signing events) belong to the FUTURE sub-packages (`blocks-docs-wiki`, `blocks-docs-signing`) and stay as authored. PR 5's edit is purely additive — insert the 5 new rows at the top of §3.4 with a header sub-heading `### Substrate-level (this hand-off)`; the existing rows go under a sub-heading `### Sub-package (future)`.

#### Tests (PR 5)

`tests/AddBlocksDocsDiTests.cs`:

- `AddBlocksDocs_RegistersAllExpectedServices` (IAttachmentService, IDocumentRefService, IAttachmentRepository, IDocumentRefRepository, IMimeTypeAndSizePolicy, IAttachmentBytePersister, IDocsEventPublisher).
- `AddBlocksDocs_AppliesOptionsConfigure_PerTenantOverrides`.
- `AddBlocksDocs_DefaultBlocksDocsOptions_AreSafe` (default whitelist applied; default 100MB cap; default no-quota).
- `AddBlocksDocs_MissingIBlobStore_StartupCheckThrowsHelpfulError`.

Total new tests this PR: ~4 (small DI-validation set; the bulk of the testing was PR 1-4).

#### Verification

- `dotnet build` succeeds.
- All PR 1-4 tests pass.
- New tests pass.
- `apps/docs/blocks-docs/overview.md` renders cleanly (sanity-spot in any Markdown previewer).
- `_shared/engineering/cross-cluster-event-bus-design.md` edit reviewed for editorial consistency.

#### Do NOT in this PR

- Do NOT add new functional code paths. PR 5 is a wiring + docs PR.
- Do NOT rewrite the existing Stage 02 design or the cross-cluster event-bus design beyond the additive editorial. Existing sections stay verbatim.

---

### PR 6 — Tier-2 post-merge reconciler stub + cluster acceptance tests

**Estimated effort:** ~1–2h
**Scope:** ship a stub `ICasIntegrityReconciler` that verifies `Attachment.contentHash == sha256(IBlobStore.GetAsync(storageRef))` on a scheduled basis; ship integration tests for the full upload → ref → list → detach round-trip; ship the cluster-level PASS-gate acceptance tests
**Commit subject:** `test(blocks-docs): Tier-2 CAS-integrity reconciler stub + cluster acceptance integration tests`
**Depends on:** PR 5 merged
**Branch:** `cob/blocks-docs-reconciler-and-acceptance`

#### Reconciler stub

**`Services/ICasIntegrityReconciler.cs`**:

```csharp
public interface ICasIntegrityReconciler
{
    /// <summary>
    /// Verifies that every Active Attachment's stored bytes still match its
    /// declared ContentHash. Returns a report of any divergences.
    /// </summary>
    Task<CasIntegrityReport> RunAsync(string tenantId, CancellationToken ct = default);
}

public sealed record CasIntegrityReport(
    string TenantId,
    int AttachmentsScanned,
    int Divergences,
    IReadOnlyList<CasIntegrityFinding> Findings);

public sealed record CasIntegrityFinding(
    AttachmentId AttachmentId,
    string ExpectedContentHash,
    string? ObservedContentHash,    // null when bytes not retrievable
    CasIntegrityFindingKind Kind);

public enum CasIntegrityFindingKind
{
    BytesMissing,           // GetAsync returned null
    ContentHashMismatch,    // sha256(bytes) != attachment.contentHash
}

internal sealed class CasIntegrityReconciler : ICasIntegrityReconciler
{
    private readonly IAttachmentRepository _repo;
    private readonly IAttachmentBytePersister _persister;
    // straightforward impl
}
```

**v1 scope:** the reconciler is callable on-demand (e.g., via a maintenance CLI command — out of scope for this hand-off). Scheduled invocation (background hosted-service) is a follow-on. The reconciler ships registered + tested but unwired to any scheduler.

#### Cluster acceptance integration tests

**`tests/BlocksDocsClusterAcceptanceTests.cs`** — these are the integration tests that verify the cluster PASS gate:

- **A1.** Upload → DocumentRef create → ListForSourceEntity → GetBytes round-trip.
- **A2.** Upload dedup: same bytes / same tenant → one Attachment row; same bytes / different tenants → two Attachment rows.
- **A3.** Replace round-trip: original Active → call Replace → original is Superseded with ReplacedByAttachmentId; new is Active with ReplacesAttachmentId.
- **A4.** Detach blocked by active refs → detach refs → Attachment.Detach succeeds → status Tombstoned; GetBytes returns Tombstoned error.
- **A5.** Tenant scoping: cross-tenant attempts to GetBytes / Detach / Replace / Create-ref all return TenantScopeViolation.
- **A6.** MIME policy: PDF upload OK; HTML upload rejected.
- **A7.** Size policy: 1 KB upload OK; 200 MB upload rejected (with default 100 MB cap).
- **A8.** Quota policy: tenant with 1 MB quota; first 800 KB upload OK; second 800 KB upload rejected (cumulative).
- **A9.** Filename sanitization: `../etc/passwd` upload — Attachment.OriginalFilename is `passwd` not `../etc/passwd`.
- **A10.** CAS integrity reconciler: upload 5 attachments → run reconciler → 0 divergences. Manually corrupt one (write different bytes via a back-door test helper) → run reconciler → 1 divergence reported.
- **A11.** DocumentRef catalog: same (source-cluster, source-entity, role, attachment) tuple creates one ref then is idempotent.
- **A12.** Replacement chain: upload A → replace with B → replace with C → walk the chain back via `ReplacesAttachmentId` pointers; assert C → B → A → null.

Total new tests this PR: ~12 integration tests (in addition to the per-PR unit tests already in PRs 1-5).

#### Verification

- `dotnet build` succeeds.
- All PR 1-5 tests pass.
- New tests + acceptance suite pass.
- `dotnet test packages/blocks-docs/tests/` totals ~125 tests across the cluster.

#### Do NOT in this PR

- Do NOT wire the reconciler to a hosted-service schedule. Manual / CLI invocation is the v1 surface.
- Do NOT introduce GC of orphaned CAS bytes. Physical GC is a follow-on `tooling-anchor-maintenance` workstream.
- Do NOT introduce a UI for the reconciler report. v1 produces a `CasIntegrityReport` value object; UI presentation lives elsewhere.

---

## CRDT-friendly schema conventions applied

This hand-off applies the cluster's CRDT-friendly conventions verbatim. Cross-referenced summary:

### 1. Attachment is posted-then-immutable

Per `crdt-friendly-schema-conventions.md` §6: once an Attachment is uploaded, the row's substantive fields (`tenantId`, `contentHash`, `mimeType`, `sizeBytes`, `storageRef`, `originalFilename`) are **immutable**. Allowed mutations:

- Status transitions per `AttachmentStatusTransitions` (Active → Superseded; Active → Tombstoned; Superseded → Tombstoned).
- `ReplacedByAttachmentId` (set once on replace).
- `Sensitivity` (allowed under exceptional admin re-classification — to be enforced at the service layer in a future hardening pass; v1 leaves Sensitivity nominally writable but no API surface mutates it).
- Tombstone fields (`DeletedAtUtc`, `DeletedBy`, `DeletedReason`).
- Audit fields (`UpdatedAtUtc`, `UpdatedBy`).

The repository's `UpsertAsync` is the enforcement point at Tier-1.

### 2. Replacement is a NEW Attachment

Replacing an Attachment never mutates the existing row's bytes-related fields. Instead: upload a new Attachment (with its own content-hash dedup); link the two via `ReplacesAttachmentId` / `ReplacedByAttachmentId`; transition the predecessor to Superseded. The chain is walkable for audit + UI history.

### 3. DocumentRef is monotonic (Pattern A)

Per `crdt-friendly-schema-conventions.md` §7: DocumentRef lifecycle is Active → Detached (no branches; no concurrent transitions). Two replicas detaching the same ref concurrently both produce a Detached row that converges (Pattern A — terminal state is monotonic). No conflict resolver needed beyond the repository's last-write-wins-on-Detached behavior.

### 4. ExternalRef / Source-cluster idempotency keys

DocumentRef carries `(sourceCluster, sourceEntityId, attachmentId, role)` — the idempotency tuple for cross-cluster invocations. The catalog's idempotency-key format `documentref-created:{sourceCluster}:{sourceEntityId}:{role}:{attachmentId}` is exactly this tuple plus event-name prefix, per the cross-cluster event-bus convention.

### 5. Tombstones (CRDT conventions §2)

Both Attachment and DocumentRef carry tombstone fields (`DeletedAtUtc`, `DeletedBy`, `DeletedReason`); tombstone wins over un-tombstone in any future merge. v1 single-node SQLite makes this academic; the discipline is set up for Standard / Enterprise tier sync.

### 6. Sync class: all CP (substrate is coordination-required)

Per Path II `crdt-schema-conventions.md` §5: the substrate's Attachment + DocumentRef are CP-class (event-log path; no Loro op-log). The wiki/dam sub-packages MAY introduce AP fields (e.g., `WikiPage.markdownBody` for collaborative text editing) — those land in the respective sub-package hand-offs; the substrate has none.

### 7. Tier-2 reconciliation — CAS integrity

Per `crdt-friendly-schema-conventions.md` §10 (two-tier validation), Tier-2 post-merge reconciliation for this cluster is the CAS-integrity verification: scheduled re-verification that `sha256(IBlobStore.GetAsync(storageRef)) == Attachment.contentHash`. PR 6 ships this as a stub `ICasIntegrityReconciler`; scheduled invocation is a follow-on workstream.

---

## Event-bus catalog applied

Per `cross-cluster-event-bus-design.md` §3.4, this hand-off emits and consumes:

### Emitted (producer: `docs`)

| Event | Consumer clusters | Payload | Idempotency key |
|---|---|---|---|
| `Docs.AttachmentUploaded` | (none external — internal observability) | `{ attachmentId, tenantId, contentHash, mimeType, sizeBytes }` | `attachment-uploaded:{attachmentId}` |
| `Docs.AttachmentDetached` | (none external — internal) | `{ attachmentId, tenantId, reason? }` | `attachment-detached:{attachmentId}` |
| `Docs.AttachmentReplaced` | (none external — internal) | `{ newAttachmentId, oldAttachmentId, tenantId, by }` | `attachment-replaced:{newAttachmentId}:{oldAttachmentId}` |
| `Docs.DocumentRefCreated` | source-cluster + reports | `{ documentRefId, tenantId, sourceCluster, sourceEntityId, role, attachmentId }` | `documentref-created:{sourceCluster}:{sourceEntityId}:{role}:{attachmentId}` |
| `Docs.DocumentRefDetached` | source-cluster + reports | `{ documentRefId, tenantId, sourceCluster, sourceEntityId, role, attachmentId, reason? }` | `documentref-detached:{documentRefId}` |

### Consumed

Currently none. The substrate doesn't subscribe to any cross-cluster event — it's a pure provider surface.

### Schema versioning

All event payloads ship at `schemaVersion: "1.0.0"`. Future additive fields → minor bump; renames or breaking changes → new event type per §2 deprecation rules. Renames forbidden.

### Envelope construction

Each emitted event is wrapped in the canonical `DomainEventEnvelope<TPayload>` per `cross-cluster-event-bus-design.md` §1. The local `IDocsEventPublisher` stub ships a minimal envelope; the full envelope is populated when `foundation-events.DomainEventEnvelope<TPayload>` is wired in PR 5 (or, if `foundation-events` isn't ready, in a follow-on bridging PR).

---

## Cross-cluster contracts

Per `blocks-docs-schema-design.md` §7, the substrate touches the following clusters. Each row enumerates the **type of contact** + **direction** + **the contract surface this hand-off ships**.

| Cluster | Direction | Surface |
|---|---|---|
| `foundation/Blobs` | Consume (downward) | `IBlobStore.PutAsync` / `GetAsync` / `PinAsync` / `UnpinAsync` — bytes persister |
| `foundation-events` | Consume (downward) | `DomainEventEnvelope<TPayload>` for event emission; if absent, local stub |
| `blocks-people-foundation` | Consume (downward) | `IPartyReadModel` for `uploadedBy` / `createdBy` resolution; v1 stores `PartyId.Value` as `string?` |
| `foundation-multitenancy` | Consume (downward) | `TenantId` scoping convention — all entities carry `tenantId`; service layer enforces |
| `blocks-financial-ar` | Consumer (upward — they call us) | Calls `IAttachmentService.UploadAsync(...)` for invoice-pdf attachments; calls `IDocumentRefService.CreateAsync(sourceCluster="blocks-financial-ar", role="invoice-pdf", ...)` |
| `blocks-financial-ap` | Consumer (upward) | Calls `IAttachmentService.UploadAsync(...)` for bill-attachment files; `role="bill-attachment"` |
| `blocks-property-leases` | Consumer (upward) | `role ∈ {"lease-contract", "lease-supporting-doc"}` |
| `blocks-property-inspections` | Consumer (upward) | `role="inspection-photo"` |
| `blocks-work-orders` | Consumer (upward) | `role ∈ {"work-order-before-photo", "work-order-after-photo", "work-order-receipt"}` |
| `blocks-docs-wiki` (future) | Consumer (upward, future) | `role ∈ {"wiki-page-cover", "wiki-page-embed"}` |
| `blocks-docs-templates` (future) | Consumer (upward, future) | `role ∈ {"contract-template-body", "contract-template-supporting-doc"}` |
| `blocks-docs-dam` (future) | Consumer (upward, future) | `role="marketing-asset"`; DAM may also bypass DocumentRef and own its own attachment association (TBD in the DAM hand-off) |
| `blocks-docs-signing` (future) | Consumer (upward, future) | `role="signing-final-document"` — the final signed PDF |
| `blocks-reports` (parallel) | Consumer (upward, parallel) | `role="report-artifact"` for stored report PDFs |
| `kernel-security` (future) | Consume (downward, future) | When encryption-at-rest lands, kernel-security provides envelope keys; the substrate is the call-site |
| `kernel-signatures` (future) | Not touched by substrate | All crypto delegation happens in `blocks-docs-signing` (future sub-package), not here |

---

## License posture

### Borrowed-with-attribution (permissive)

- **Apache OFBiz** `content/Content + DataResource + ContentAssoc` entities (Apache 2.0). The `Attachment` + `DocumentRef` field shapes (content-addressed body decomposition; cross-entity link with role) derive from OFBiz's `content`-module pattern per `blocks-docs-schema-design.md` §3.1.1.

- **Mayan EDMS** `DocumentVersion + retention + tag` patterns (Apache 2.0). Although the substrate intentionally OMITS retention + version history (those land with the wiki sub-package), the Attachment-as-immutable-record discipline derives from Mayan's "version is immutable; replacement is a new version with pointer to predecessor" pattern.

**Attribution requirements:**

1. The package's `Sunfish.Blocks.Docs.csproj` carries `<PropertyGroup><NOTICEFile>NOTICE.md</NOTICEFile></PropertyGroup>`.
2. **`packages/blocks-docs/NOTICE.md`** (new file in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.Docs

This package's entity shapes (Attachment + DocumentRef; content-addressed
storage decomposition; cross-entity link with role; immutable-record-with-
replacement-chain discipline) derive from Apache OFBiz's `content` module
(<https://ofbiz.apache.org/>, Apache 2.0 license) and Mayan EDMS's
DocumentVersion model (<https://www.mayan-edms.com/>, Apache 2.0 license).

OFBiz version studied: v18.12.x (as of 2026-05-16).
Mayan EDMS version studied: 4.x (as of 2026-05-16).

The Sunfish implementation is original code, distributed under the
MIT License. The OFBiz + Mayan EDMS entity-shape patterns are reproduced
with attribution per Apache 2.0 §4(c).
```

3. Source-header comments on `Attachment.cs`, `DocumentRef.cs`, `StorageRef.cs`, `IAttachmentService.cs` reference OFBiz + Mayan EDMS in a one-line comment.

### Clean-room only (copyleft)

Per `blocks-docs-schema-design.md` §2 + §8, these sources were studied for understanding only and contribute NO code to the substrate:

- **Wiki.js, HedgeDoc** (AGPLv3) — Wiki UX patterns; relevant only to the *future* `blocks-docs-wiki` sub-package, not this substrate. No code or schema borrowed.
- **Documenso, OpenSign** (GPLv3 / AGPLv3) — Signing UX patterns; relevant only to the *future* `blocks-docs-signing` sub-package, not this substrate.
- **Razuna** (GPLv3) — DAM UX patterns; relevant only to the *future* `blocks-docs-dam` sub-package.
- **ResourceSpace, DocAssemble** (BSD-3 / MIT — permissive) — Studied for the substrate's storage decomposition, but the actual shape derives from the permissive OFBiz + Mayan precedents above; ResourceSpace's `media_resource` + DocAssemble's `Template` are clean-room references only at the substrate level.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code. (Spot-check by grep before merge.)
3. The clean-room schema in `blocks-docs-schema-design.md` §3.1 + §6 + §7 is the source of truth; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + records + state machine) | ~25 | record fields; StorageRef discriminated union; storage-tier policy; status transitions |
| PR 2 (IAttachmentService) | ~28 | upload happy + every failure; dedup; get; detach (stub-blocked); replace; repository round-trip |
| PR 3 (IBlobStore wiring + MIME/size policy + tenant quotas) | ~33 | MIME policy (whitelist + reject); MIME sniffer (every default-whitelist type); filename sanitizer; tenant scoping; quota enforcement |
| PR 4 (DocumentRef + IDocumentRefService) | ~24 | create + idempotency + every failure; list (by source / by attachment / with-filter); detach; referential-integrity blocks Attachment.Detach |
| PR 5 (DI extension + docs + editorial) | ~4 | DI validation; options application; startup check |
| PR 6 (reconciler stub + acceptance integration) | ~12 | cluster-acceptance round-trip; dedup; replace chain; tenant scope; MIME/size/quota; sanitization; CAS integrity divergence |
| **Total** | **~126 new** | |

### Cluster-level acceptance (PASS gate at end of PR 6)

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.Docs` package + every downstream consumer (initially none — this is the first consumer-facing substrate of its kind).

**A2.** `dotnet test packages/blocks-docs/tests/` passes all ~126 new tests.

**A3.** Upload → DocumentRef → Get → Detach round-trip:
- Register `FileSystemBlobStore` (`packages/foundation/Blobs/FileSystemBlobStore.cs`) + `AddBlocksDocs(opts => { opts.CasRootDirectory = tmpDir; })`.
- Upload a 64KB PDF (`MimeType=application/pdf`, sensitivity=Confidential, uploadedBy="party:alice").
- Assert: `UploadResult.Error == None`, `Attachment.Status == Active`, `Attachment.MimeType == "application/pdf"`, `Attachment.SizeBytes == 65536`.
- Create DocumentRef: sourceCluster=`blocks-financial-ar`, sourceEntityId=`01J...`, role=`invoice-pdf`.
- Assert: `CreateRefResult.Error == None`, ref's `AttachmentId` matches.
- List for source entity → 1 ref. List for attachment → 1 ref.
- `GetBytesAsync` → bytes round-trip byte-identical.
- `IAttachmentService.DetachAsync` → returns `HasActiveDocumentRefs` error.
- `IDocumentRefService.DetachAsync` → succeeds; ref status → Detached.
- `IAttachmentService.DetachAsync` → succeeds; attachment status → Tombstoned.
- `GetBytesAsync` → returns `Tombstoned` error.

**A4.** Content-hash dedup:
- Upload bytes X for tenant=`acme`. Note attachment id `A`.
- Upload bytes X (identical) again for tenant=`acme`. Note attachment id `B`.
- Assert: `A == B`; second result has `WasDeduplicated == true`.
- Upload bytes X for tenant=`bravo`. Note attachment id `C`.
- Assert: `C != A`; second result has `WasDeduplicated == false`.

**A5.** MIME policy:
- Attempt to upload bytes whose sniffed MIME is `application/x-msdownload` (executable-shaped magic). Assert `UploadError.PolicyRejectedMime`.
- Attempt to upload bytes whose sniffed MIME is `text/html`. Assert `UploadError.PolicyRejectedMime`.
- Upload bytes whose sniffed MIME is `application/pdf`. Assert success.

**A6.** Size policy:
- Configure `BlocksDocsOptions.SetTenantMaxAttachmentBytes("demo", 1024 * 1024)` (1 MB cap).
- Upload 2 MB bytes for tenant=`demo`. Assert `UploadError.PolicyRejectedSize`.
- Upload 500 KB bytes for tenant=`demo`. Assert success.

**A7.** Tenant quota:
- Configure `BlocksDocsOptions.SetTenantQuotaBytes("demo", 1 * 1024 * 1024)` (1 MB total).
- Upload 800 KB bytes. Assert success (200 KB headroom).
- Upload 400 KB different bytes. Assert `UploadError.PolicyRejectedTenantQuota`.

**A8.** Filename sanitization:
- Upload bytes with originalFilename=`../../etc/passwd`.
- Assert: success; `Attachment.OriginalFilename == "passwd"` (path stripped).

**A9.** Replacement chain:
- Upload A, replace with B, replace with C.
- Walk via `ReplacesAttachmentId`: C.replaces=B, B.replaces=A, A.replaces=null.
- Walk via `ReplacedByAttachmentId`: A.replacedBy=B, B.replacedBy=C, C.replacedBy=null.
- Statuses: A=Superseded, B=Superseded, C=Active.

**A10.** Tenant scope violation:
- Upload Attachment under tenant=`acme`. Get attachmentId.
- Attempt `GetBytesAsync(tenantId="bravo", id=attachmentId)`. Assert `GetBytesError.TenantScopeViolation`.
- Attempt `DetachAsync(tenantId="bravo", id=attachmentId)`. Assert `DetachError.TenantScopeViolation`.

**A11.** DI registration smoke:
- Build minimal `IServiceCollection` → `IServiceProvider` using foundation `IBlobStore` registration + `AddBlocksDocs()`.
- Assert: `IAttachmentService`, `IAttachmentRepository`, `IDocumentRefService`, `IDocumentRefRepository`, `IMimeTypeAndSizePolicy`, `IAttachmentBytePersister`, `IDocsEventPublisher`, `ICasIntegrityReconciler`, `BlocksDocsOptions` all resolve without error.

**A12.** CAS integrity reconciler:
- Upload 5 attachments.
- Run reconciler → 0 divergences.
- Use a test-only back-door to corrupt one CAS body's bytes in place.
- Run reconciler → 1 divergence reported, with the corrupted Attachment's id + observed-vs-expected hash.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### 1. `IBlobStore` foundation primitive shape drift (PR 3)

If `packages/foundation/Blobs/IBlobStore.cs` has changed since this hand-off was authored (added required interface members; renamed `PutAsync` / `GetAsync`; changed `Cid` semantics; etc.), the PR 3 wiring may not compile.

**Mitigation (no halt):** if the new shape is additive (new methods with default interface implementations), the wiring still works — just extend `BlobStoreAttachmentBytePersister` to call any new required members.

**Halt condition:** if `IBlobStore.PutAsync(ReadOnlyMemory<byte>) → ValueTask<Cid>` or `GetAsync(Cid) → ValueTask<ReadOnlyMemory<byte>?>` has been renamed / removed / replaced, **STOP** + file `cob-question-*` requesting either (a) the foundation primitive be restored, or (b) the substrate's wiring be re-spec'd against the new shape. Do NOT silently adapt — the contract is load-bearing.

### 2. `IPartyReadModel` cross-cluster contract (any PR using `createdBy` / `uploadedBy`)

If `blocks-people-foundation` doesn't expose a stable `IPartyReadModel` interface yet:

**Mitigation:** Ship `Attachment.CreatedBy` / `DocumentRef.CreatedBy` as `string?` (storing `PartyId.Value` raw). When `IPartyReadModel` lands, a follow-on PR upgrades to the strong type (additive; non-breaking for the wire shape).

**No halt** — the substrate doesn't *resolve* parties (it just records their id strings); the resolution happens in the consuming cluster's UI layer. If COB discovers that `blocks-people-foundation` is in active design-in-flight AND a hand-off authoring `IPartyReadModel` is queued, **defer the strong-type upgrade** to that follow-on; ship strings now.

### 3. `foundation-events.DomainEventEnvelope<TPayload>` not yet shipped (PR 5)

Per `cross-cluster-event-bus-design.md` §10 Q1: the canonical event-bus envelope's package home was TBD at the AR/AP/ledger hand-off authoring time.

**Mitigation:** Ship a local stub `IDocsEventPublisher` + local event records per the AR pattern. When the foundation event-bus package lands and exports `DomainEventEnvelope<TPayload>`, a follow-on hand-off relocates the records + bridges the publisher.

**No halt.** Mark each event-record file with a `// TODO: relocate to foundation-events.Events when foundation event-bus ships` comment.

### 4. MIME sniffer / filename sanitizer adequacy (PR 3 — council surface)

The PR 3 hand-rolled MIME sniffer covers ~15 default-whitelist types. If the security-engineering council deems this insufficient and requires:

- A more comprehensive library (e.g., `HeyRed.Mime`, `Mime-Detective`, a port of file(1)), AND/OR
- A stricter filename sanitizer (e.g., enforcing only `[a-zA-Z0-9._-]` characters), AND/OR
- Additional defense-in-depth measures (e.g., scanning bytes for embedded scripts in zip/office files),

then file `cob-question-2026-05-XXTHH-MMZ-blocks-docs-mime-sniffer-hardening.md` with the council finding + the recommended uplift scope. Implement the hardening as a follow-on PR (gated on council approval); the substrate ships v1 with the hand-rolled sniffer + the current sanitizer.

**XO recommendation:** v1 hand-rolled is acceptable for the Phase 3 demo cohort (Light tier, single-node Anchor). Hardening is a Phase 4 (multi-tenant managed hosting) requirement.

### 5. ERPNext file-import scope question (after PR 6)

The hand-off explicitly DEFERS the ERPNext file importer to the `tooling-anchor-import` orchestrator. If COB discovers that the orchestrator hand-off is queued AND its author expects `blocks-docs` to expose a `IErpnext*FileImporter` interface, **defer to the orchestrator's design** — that hand-off can introduce a thin wrapper around `IAttachmentService.UploadAsync` + `IDocumentRefService.CreateAsync` without modifying the substrate.

**No halt.** Document the design boundary in `apps/docs/blocks-docs/overview.md` (the "What's NOT in v1" section explicitly says importer lives elsewhere).

### 6. `Cid` type relationship to `contentHash` string (PR 3)

The foundation `IBlobStore` keys bytes by `Cid` (a strongly-typed content-id wrapper); the substrate's `Attachment.contentHash` is a `string` (sha256 hex-lowercase). PR 3's `BlobStoreAttachmentBytePersister` must translate between the two:

- On Put: foundation primitive returns a `Cid`; substrate persists the `string` form on the `Attachment.contentHash` and `StorageRef.contentHash` fields.
- On Get: substrate parses the `Attachment.contentHash` back into a `Cid` (via `Cid.Parse` or equivalent) to call `IBlobStore.GetAsync(cid)`.

If `Cid` does not expose a `Parse` / round-trippable string representation matching sha256-hex-lowercase (e.g., if it's a CIDv1 multibase format), the round-trip may fail. **Council review of PR 3 must confirm the `Cid ↔ string` translation is correct**; if not, file `cob-question-*` for a `Cid` extension method or a `kernel-security.computeContentHash` clarification.

**XO recommendation:** Use whatever round-trippable form `Cid.ToString()` produces as the `Attachment.contentHash` value. The substrate is opaque on the format; consumers don't introspect the string. The dedup key `(tenantId, contentHash)` works regardless of the string's internal encoding as long as identical bytes produce identical strings.

### 7. Loro append-only constraint surfaces (any PR)

Per the AR + AP hand-off halt patterns, Stage 02 Open Question Q10 on Loro append-only constraints remains open at this hand-off's cutoff. **Skip Loro integration entirely in this hand-off** (v1 substrate is single-node SQLite-equivalent in-memory; CP-class only). File `cob-question-*` only if compilation fails due to a Loro op-mapping question — it shouldn't.

### 8. Stage 02 sub-package decomposition (after PR 6)

Stage 02 design `blocks-docs-schema-design.md` §1 identifies five candidate packages — `blocks-docs-core`, `blocks-docs-wiki`, `blocks-docs-templates`, `blocks-docs-dam`, `blocks-docs-signing`. This hand-off ships `blocks-docs` (the substrate floor), not the full `blocks-docs-core`. The substrate is a *subset* of `blocks-docs-core` — it covers Attachment + DocumentRef + storage but NOT the Document base entity + folder + permission + retention.

**If COB asks "is `blocks-docs` the same as `blocks-docs-core`?"** Answer: NO. `blocks-docs` is the substrate floor; `blocks-docs-core` (when its own hand-off lands) will add the Document base entity with version history + folders + permissions + retention policies. The substrate may eventually be folded INTO `blocks-docs-core` OR kept as a thinner foundation (XO decision at the time of `blocks-docs-core` Stage 02 design ratification).

**No halt.** Document the relationship in `apps/docs/blocks-docs/overview.md`.

### 9. CAS bytes physical GC out of scope (PR 6)

The reconciler in PR 6 reports CAS-integrity divergences but does NOT physically delete orphaned CAS bytes (Attachments that have been Tombstoned for >grace-window with no remaining DocumentRefs).

**If COB asks about CAS cleanup:** the substrate ships logical-only delete (tombstone). Physical GC is the `tooling-anchor-maintenance` workstream. Do not introduce a physical-delete code path here.

**No halt.** Document.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–6 merged to main** (sequentially per the dependency chain: PRs 1+2+3+4+5+6 in order; PR 4 may parallelize with PR 3 IF council approval lands on PR 3 first; XO recommendation: keep strictly sequential).
2. **Upload → DocumentRef → Get → Detach round-trip:** acceptance test A3 passes.
3. **Content-hash dedup works:** acceptance test A4 passes.
4. **MIME / size / quota policy enforced:** acceptance tests A5 + A6 + A7 pass.
5. **Filename sanitization:** acceptance test A8 passes.
6. **Replacement chain:** acceptance test A9 passes.
7. **Tenant scope violations rejected:** acceptance test A10 passes.
8. **DI registration:** acceptance test A11 passes.
9. **CAS integrity reconciler reports divergence:** acceptance test A12 passes.
10. **Tests pass:** ~126 new tests across the package.
11. **Security-engineering council REVIEWED + APPROVED on PR 3** (mandatory; document the approving agents + their substantive findings/agreements in the PR description).
12. **`apps/docs/blocks-docs/overview.md` published** (ships in PR 5).
13. **`_shared/engineering/cross-cluster-event-bus-design.md` §3.4 editorial landed** (ships in PR 5; additive only).
14. **`active-workstreams.md`** row for the cluster updated with `built` status + the 6 PR numbers (via the source W*.md file per `feedback_never_add_workstream_rows_directly_to_ledger`).
15. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-blocks-docs-built.md`** beacon dropped.

When the PASS gate is met, the next Phase 3 hand-offs can proceed:

- `blocks-docs-wiki-stage06-handoff.md` (WikiSpace / WikiBook / WikiPage / WikiChapter / Policy / Procedure / PolicyVersion / PolicyEffectiveDate / PolicyAcknowledgment — full wiki + policy surface; consumes Attachment for cover images + embedded media).
- `blocks-docs-templates-stage06-handoff.md` (ContractTemplate / ContractTemplateField / ContractTemplateClause / TemplateRenderJob / ContractInstance — consumes Attachment for template binary bodies + rendered outputs).
- `blocks-docs-dam-stage06-handoff.md` (MarketingAsset / AssetTag / AssetCollection / AssetUsage / BrandKit / BrandKitElement — consumes Attachment as the binary store).
- `blocks-docs-signing-stage06-handoff.md` (SigningWorkflow / SigningStep / SigningParty / SignatureRequest / Signature / SigningAuditLog — consumes Attachment + DocumentRef for the final signed PDF; delegates all crypto to `kernel-security` + `kernel-signatures`).
- `blocks-reports-stage06-handoff.md` (Report / ReportTemplate / ReportRun / ReportArtifact — consumes Attachment + DocumentRef for stored report PDFs; consumes `blocks-financial-ar.IArAgingService` + `blocks-financial-ap.IApAgingService` for read sources).
- `blocks-financial-ar-attachment-integration-stage06-handoff.md` (small follow-on: AR cluster calls `IDocumentRefService.CreateAsync` when an invoice is rendered; ships a `role="invoice-pdf"` integration).
- `blocks-property-leases-attachment-integration-stage06-handoff.md` (analogous for leases).

---

## Docs

**`apps/docs/blocks-docs/overview.md`** — cluster docs page (ships in PR 5). Content sketched in §PR 5 above.

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `packages/foundation/Blobs/IBlobStore.cs` ✓ (the canonical primitive consumed by this substrate)
- `packages/foundation/Blobs/FileSystemBlobStore.cs` ✓ (the default production impl)
- `packages/foundation/Blobs/Cid.cs` ✓ (the content-id type)
- `packages/blocks-people-foundation/` ✓ (predecessor — assumed shipped; verify pre-build checklist step 2)
- `packages/foundation-events/` ✓ (predecessor — assumed shipped; verify pre-build checklist step 3)
- ADR 0088 §1 (Path II + 7-cluster decomposition) ✓
- `icm/02_architecture/blocks-docs-schema-design.md` §1, §2, §3.1, §6, §7, §8, §9 ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §5, §6, §7, §10 ✓
- `icm/02_architecture/path-ii-crdt-schema-conventions.md` §1 (CP / AP classes), §5 (blocks-docs entries) ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.4 ✓
- `icm/02_architecture/path-ii-cross-cluster-event-bus.md` §6 (Docs events catalog) ✓
- `_shared/engineering/party-model-convention.md` §3, §4 (read-only consumption from blocks-people) ✓
- Sibling hand-off precedents: `blocks-financial-ar-stage06-handoff.md`, `blocks-financial-ap-stage06-handoff.md`, `blocks-people-foundation-stage06-handoff.md` ✓

**Introduced by this hand-off** (ship across PRs 1–6):

- New package: `packages/blocks-docs/`
- New types: `AttachmentId`, `Attachment`, `AttachmentStatus`, `AttachmentStatusTransitions`, `StorageRef` (+ `InlineSqliteBlob`, `FsContentAddressed`, `ExternalUri` subtypes), `StorageRefKind`, `StorageTierPolicy`, `Sensitivity`, `DocumentRefId`, `DocumentRef`, `DocumentRefStatus`, `BlocksDocsOptions`, `PolicyResult`, `CasIntegrityReport`, `CasIntegrityFinding`, `CasIntegrityFindingKind`, `UploadResult`, `UploadError`, `GetBytesResult`, `GetBytesError`, `DetachResult`, `DetachError`, `ReplaceResult`, `ReplaceError`, `CreateRefResult`, `CreateRefError`, `DetachRefResult`, `DetachRefError`
- New events: `AttachmentUploadedEvent`, `AttachmentDetachedEvent`, `AttachmentReplacedEvent`, `DocumentRefCreatedEvent`, `DocumentRefDetachedEvent`
- New services: `IAttachmentRepository` + `InMemoryAttachmentRepository`, `IDocumentRefRepository` + `InMemoryDocumentRefRepository`, `IAttachmentBytePersister` (internal) + `InMemoryAttachmentBytePersister` (PR 2 stub) + `BlobStoreAttachmentBytePersister` (PR 3 production), `IMimeTypeAndSizePolicy` + `PermissivePolicyStub` (PR 2) + `MimeTypeAndSizePolicy` (PR 3 production), `IDocsEventPublisher` + `InMemoryDocsEventPublisher`, `IAttachmentService` + `AttachmentService`, `IDocumentRefService` + `DocumentRefService`, `ICasIntegrityReconciler` + `CasIntegrityReconciler`, `ITenantCasDirectoryResolver` + `TenantCasDirectoryResolver`
- New utilities: `MimeSniffer`, `FilenameSanitizer`, `DefaultMimeWhitelist`
- Docs: `apps/docs/blocks-docs/overview.md`
- Attribution: `packages/blocks-docs/NOTICE.md`
- Editorial: `_shared/engineering/cross-cluster-event-bus-design.md` §3.4 (additive; 5 new event-row entries)

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check negative existence too (verify the absence of `IAttachmentService` in any consuming cluster before assuming no consumer-side breakage).

---

## Cohort discipline

This hand-off is **the first cluster hand-off of Phase 3 under ADR 0088 Path II** (after Phase 1 financial cluster + Phase 2 people + work clusters). The COB self-audit pattern applied to Phase 1/2 hand-offs (AR, AP, ledger, people-foundation, work-projects, work-orders) applies here verbatim:

- **`AddBlocksDocs()` naming for the DI extension** — matches the cluster convention (Pascal-cased package suffix after `AddBlocks`).
- **`apps/docs/{cluster}/overview.md` page convention** — applied in PR 5.
- **`README.md` at the package root** referencing Stage 02 design + ADR 0088 — ship in PR 1.
- **`ConcurrentDictionary` dedup for any in-memory cache** — applied in `InMemoryAttachmentRepository`, `InMemoryDocumentRefRepository`, `InMemoryAttachmentBytePersister`.
- **Strong-typed Id records** (ULID-backed) — applied for `AttachmentId`, `DocumentRefId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — applied for `IDocsEventPublisher` (relocates when `foundation-events.DomainEventEnvelope<TPayload>` is consumable). The substrate consumes `IBlobStore` directly (no stub needed — the primitive is shipped).
- **Security-engineering council MANDATORY on the IBlobStore-wiring + policy PR** — unprecedented for the Phase 1/2 cluster hand-offs (which were substrate-only with no filesystem-touching wiring); FIRST application here. Council surface: tenant boundary, defense-in-depth policy, no log leaks. Discharge before PR 3 merge.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-blocks-docs-{slug}.md` in
  `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row.
- `ScheduleWakeup 1800s`.

If COB completes PR 6 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-blocks-docs-built.md` to inbox.
- Continue with the next hand-off in the Phase 3 critical path (likely `blocks-reports` or one of the `blocks-docs-*` sub-packages — whichever XO has dropped next).

---

## Cross-references

- Spec source: `icm/02_architecture/blocks-docs-schema-design.md` §1, §2, §3.1, §6, §7, §8, §9 (Open Questions Q2 + Q4 explicitly deferred).
- Path II conventions: `icm/02_architecture/path-ii-crdt-schema-conventions.md` §1 (CP/AP), §5 (blocks-docs entries); `icm/02_architecture/path-ii-cross-cluster-event-bus.md` §6 (Docs events catalog).
- CRDT conventions: `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §5, §6, §7, §10.
- Party convention: `_shared/engineering/party-model-convention.md` §3 (read-only roles), §4 (cross-cluster contracts).
- Event bus: `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.4, §10.
- Foundation primitives: `packages/foundation/Blobs/IBlobStore.cs`, `FileSystemBlobStore.cs`, `Cid.cs`.
- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` §2 (MIT discipline) + §3 (clean-room methodology).
- Sibling hand-offs (Phase 3 cluster context — likely concurrent or follow-on):
  - `blocks-reports-stage06-handoff.md` (consumes Attachment + DocumentRef for stored report PDFs)
  - `blocks-docs-wiki-stage06-handoff.md` (future — full WikiSpace/Book/Page + Policy)
  - `blocks-docs-templates-stage06-handoff.md` (future — ContractTemplate)
  - `blocks-docs-dam-stage06-handoff.md` (future — MarketingAsset)
  - `blocks-docs-signing-stage06-handoff.md` (future — SigningWorkflow)
- Cohort precedent hand-offs (Phase 1/2 substrate shape):
  - `blocks-financial-ar-stage06-handoff.md` (most-recent peer; 6-PR shape; substrate cluster pattern)
  - `blocks-financial-ap-stage06-handoff.md` (next-most-recent peer; mirror of -ar)
  - `blocks-people-foundation-stage06-handoff.md` (substrate-only cluster; thin shape)

---

**End of hand-off.**
