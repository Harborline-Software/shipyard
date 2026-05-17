# Hand-off — `blocks-docs-retention` RetentionPolicy enforcement + LegalHold lifecycle + crypto-shred disposal (Phase 3 docs cluster follow-on)

**From:** XO (research session)
**To:** dev (galley primary; Sunfish overflow) / sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` — **gated on (a) `blocks-docs-core` all PRs merged + (b) `foundation-security-policy.IRetentionEnforcer` from W#37 P1 PR 3b.2 merged**
**Workstream:** W#71 — blocks-docs-retention (Phase 3 docs-cluster follow-on; RetentionPolicy enforcement surface)
**Spec source:** [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) §3.1.7 (RetentionPolicy entity), §6.4 (Crypto-shred + retention), §7.3 (kernel-security delegation), §9 OQ #2 (crypto-shred granularity)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md); [ADR 0068 Tenant Security Policy](../../../docs/adrs/0068-tenant-security-policy.md) §1.3, §5, §GC.1
**Predecessor hand-offs:**
- [`blocks-docs-core-stage06-handoff.md`](./blocks-docs-core-stage06-handoff.md) — Document + DocumentVersion + RetentionPolicy entity types + repository (this hand-off ENFORCES the policy whose entity ships there)
- [`blocks-docs-stage06-handoff.md`](./blocks-docs-stage06-handoff.md) — `IAttachmentService` + `IBlobStore` wiring (this hand-off ADDS the disposal/dispose API on top)
- [`w37-p1-pr3b-issuer-and-retention-enforcer-stage06-handoff.md`](./w37-p1-pr3b-issuer-and-retention-enforcer-stage06-handoff.md) — `IRetentionEnforcer` (audit-side enforcer composition pattern; this hand-off LIFTS the same pattern for documents)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~7–10h dev (4 PRs; ~30–38 tests + docs)
**PR count:** 4 PRs
**Pre-merge council:** **MANDATORY** on PR 3 (CryptoShreder + `IBlobStore.DisposeAsync` wiring → security-engineering required; HIPAA/GDPR/PCI-DSS compliance-attestation surface); **MANDATORY** on PR 2 (`IDocumentRetentionService` background sweep concurrency + cancellation → .NET architect required). Standard self-audit on PRs 1, 4.
**Attribution required:** Mayan EDMS (Apache 2.0 — RetentionPolicy lifecycle concepts; document-level retention + legal-hold patterns); foundation-security-policy `SecurityPolicyRetentionEnforcer` (composition, not borrowing). Carry NOTICE entry; no GPL/AGPL code consulted.

---

## Audit before build

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/ 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/RetentionPolicy.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/ 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-security-policy/Retention/IRetentionEnforcer.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/IBlobStore.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs-retention"
```

Expected at this hand-off's start:
- `blocks-docs-core/Models/RetentionPolicy.cs` EXISTS (per the docs-core hand-off) — carries `DisposalAction.CryptoShred` enum value but throws `NotSupportedException` when invoked (the explicit gate this hand-off resolves).
- `blocks-docs/` EXISTS with `IAttachmentService` + `IBlobStore` wiring (per the docs attachment hand-off).
- `foundation-security-policy/Retention/IRetentionEnforcer.cs` (or equivalent — see Halt §1 on exact location/name) EXISTS from W#37 P1 PR 3b.2.
- `foundation/Blobs/IBlobStore.cs` EXISTS (canonical CAS primitive).
- NOTHING matching `blocks-docs-retention/` exists yet.

---

## Context

### Phase 3 docs cluster follow-on position

Per `blocks-docs-schema-design.md` §3.1.7 + §6.4, the Phase 3 docs-cluster decomposition is:

```
blocks-docs                  (attachment substrate; IBlobStore wiring)    ✓ shipped
blocks-docs-core             (Document + DocumentVersion + RetentionPolicy entity)  ✓ shipped
blocks-docs-retention        ← THIS HAND-OFF
blocks-docs-wiki             (WikiSpace / WikiBook / WikiPage)            (follow-on)
blocks-docs-templates        (ContractTemplate / render jobs)             (follow-on)
blocks-docs-dam              (MarketingAsset / AssetCollection / BrandKit)(follow-on)
blocks-docs-signing          (SigningWorkflow / Signature / audit)        (follow-on)
```

`blocks-docs-retention` is the **first compliance-attestation surface** in the docs cluster and resolves the explicit gate the `blocks-docs-core` hand-off documented:

> **PR 3 §Halt 5 (docs-core):** `RetentionPolicy + crypto-shred`: `DisposalAction.CryptoShred` requires kernel-security envelope keys. Model the enum value but do NOT implement the crypto logic here — the shred path throws `NotSupportedException` until `blocks-docs-retention` wires the key-destruction path.

It unblocks:
- **Compliance attestation surface** — HIPAA / GDPR / PCI-DSS deployments need an enforcer that destroys keys (crypto-shred) and respects legal-hold blocks on disposal.
- `blocks-docs-wiki` policy-acknowledgment lifecycle (Policy.archivedAt + retention → disposal).
- `blocks-docs-signing` signed-PDF retention (per Mayan EDMS pattern: signed contracts get long retention floors).
- `blocks-reports-compliance-attestation` follow-on workstream (queries this cluster's disposal-log for the report surface).

### What this hand-off ships

Per `blocks-docs-schema-design.md` §3.1.7 + §6.4 + §7.3:

1. **`LegalHold`** record entity (NEW; not in Stage 02 schema verbatim — derived from §6.4 statement that *"legal-hold (RetentionPolicy.legalHold = true) BLOCKS any disposal regardless of expiry"*). LegalHold is an **append-only** CRDT-friendly entity: each placement is a row; each release is a row pointing at the placement. The aggregate state of "is this document under legal hold?" is the projection over open placements.
2. **`DocumentDisposal`** record entity (NEW; the disposal-ledger row per §6.4 *"entry written to a deletion ledger"*). Append-only; one row per disposal action; carries `disposedAt` + `disposalAction` + `disposedBy` + `cryptoShredKeyId?` + `priorContentHash` (forensic crosscheck).
3. **`IDocumentRetentionService`** — the policy-application service. Surface:
   - `ApplyPolicyAsync(documentId, retentionPolicyId, appliedBy)` — attaches a `RetentionPolicy` to a Document (records `Document.retentionPolicyId`).
   - `CheckDisposalEligibilityAsync(documentId, asOf)` — returns `DisposalEligibility` (Eligible / BlockedByLegalHold / NotYetExpired / NoPolicyAttached / AlreadyDisposed).
   - `PlaceLegalHoldAsync(documentId, reason, placedBy)` — appends a `LegalHold` row; emits `Docs.LegalHoldPlaced`.
   - `ReleaseLegalHoldAsync(legalHoldId, releasedBy)` — appends a release marker row; emits `Docs.LegalHoldReleased`.
   - `DisposeAsync(documentId, disposedBy)` — runs eligibility check; if Eligible, invokes the disposal path corresponding to `RetentionPolicy.disposalAction`; appends `DocumentDisposal` row; emits `Docs.DocumentDisposed`.
4. **`CryptoShreder`** — the disposal-action implementation for `disposalAction = CryptoShred`. Calls `IBlobStore.DisposeAsync(cid, reason)` (introduced as a NEW method on the existing `IBlobStore` per §6.4 *"per-blob symmetric key destroyed; blob bytes become inert ciphertext; eventual GC removes the file"*).
5. **`IBlobStore.DisposeAsync(Cid, DisposalReason)` extension** — additive interface method on `Sunfish.Foundation.Blobs.IBlobStore`. Three disposal-tier behaviors:
   - `inline-sqlite-blob`: column-set to NULL + deletion-ledger entry.
   - `fs-content-addressed`: per-blob key destroyed via `kernel-security.Shred({ keyId })` (calls through to the stub `IKeyShredder` interface defined here; canonical impl ships from kernel-security in a follow-on). After grace window (configurable; default 24h), the encrypted file is GC'd from disk.
   - `external-uri`: throws `NotSupportedException` — out-of-scope per §6.4 *"out-of-scope; the integration plug-in handles its own deletion"*.
6. **`IDocumentRetentionSweepJob`** — background worker (hosted service) that wakes every `RetentionSweepInterval` (default 1h, configurable), enumerates documents whose `Document.archivedAt + RetentionPolicy.retentionPeriodDays < now`, calls `CheckDisposalEligibilityAsync` + `DisposeAsync` for each Eligible result. Idempotent (re-running over already-disposed documents is a no-op). Concurrency-safe via per-document advisory locking on `Document.id`.
7. **Composition with `foundation-security-policy.IRetentionEnforcer`** — per W#37 P1 PR 3b.2, the audit-side enforcer applies jurisdiction floors (HIPAA 6-year on Identity/Security/Configuration; PCI-DSS 12-month on Financial/Security). This hand-off **does NOT redefine** that interface. Instead, `DocumentRetentionService` exposes a small `IDocumentRetentionFloorEnforcer` interface (NEW) modeled on the same `RetentionVerdict` shape — and **composes** with `foundation-security-policy` so that the *tenant-active jurisdiction preset* surfaces as a document-side floor. Concretely: when a tenant has `JurisdictionPreset = HipaaInformedDefault` active and a document carries `documentType ∈ {policy, procedure, signed-pdf, contract-instance}`, the floor enforcer applies a 6-year minimum retention floor that overrides any attached `RetentionPolicy.retentionPeriodDays < 6 years`. The pattern is parallel to the audit enforcer; the code path is independent (a different `IRetentionPolicyResolver`-equivalent reads `Document.documentType + tenantId → JurisdictionPreset`).
8. **Cross-cluster events** — `Docs.DocumentRetentionApplied`, `Docs.LegalHoldPlaced`, `Docs.LegalHoldReleased`, `Docs.DocumentDisposed` per `cross-cluster-event-bus-design.md` §3 pattern.

### What this hand-off does NOT ship

- **Compliance-attestation report rendering** — `blocks-reports-compliance-attestation` (future workstream) consumes the disposal-ledger via a query surface but renders the report itself.
- **Real kernel-security key shredding** — `IKeyShredder` ships as a stub interface here; the canonical impl ships from `kernel-security` in a follow-on hand-off. PR 3's stub destroys an in-memory key map (sufficient for v1 tests and Anchor single-tenant demo).
- **External-URI disposal** — `disposalAction = CryptoShred` on `StorageRef.kind = 'external-uri'` throws `NotSupportedException`. Per §6.4: out-of-scope; integration plug-in owns deletion.
- **Loro CRDT integration on append-only sub-collections** — `LegalHold` placements + releases are modeled as append-only rows; CRDT op-log integration is the kernel-crdt surface (deferred to Stage 02 §6.3 follow-on). v1 ships in-memory + SQLite-backed repository; Loro integration is a follow-on (per Open Q-A below).
- **Per-blob key granularity (Stage 02 §9 OQ #2 (b))** — for v1, crypto-shred operates at `RetentionPolicy` granularity (all blobs governed by the same policy share one envelope key; shredding the key shreds all blobs in the policy group). This is the **explicit Stage 02 recommendation** for Phase 2; per-row key refinement is a follow-on intake. Document the limitation in the package README.
- **EmergencyOverride** of retention floors — out-of-scope; if a deployment needs to dispose under a jurisdiction floor, that's a per-deployment legal determination requiring counsel (per ADR 0068 §GC.1) and a manual SQL operation, not a service API.
- **GDPR right-to-erasure** decisioning — per ADR 0068 §5.1, *whether a specific erasure request must be honored is a per-deployment legal determination Sunfish does not make*. This hand-off provides the disposal mechanism; the determination is operator policy.
- **MFA + cryptographic attestation of disposal** — disposal actions audit-log who/when, but a deployer wanting "two-key + MFA + multi-party-attested disposal" must compose `foundation-security-policy.IEmergencyOverrideRateLimiter` (W#37 PR 3b.3) over this hand-off's `DisposeAsync` at the consumer layer.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `LegalHoldId`, `DocumentDisposalId` — strongly typed; ULID storage |
| §2 Soft-delete via tombstones | LegalHold "release" is a NEW row pointing at the placement, NOT a column-flip on the placement row. Disposal is a NEW row in `DocumentDisposal`, NOT a column on the document. Document.archivedAt remains the existing tombstone for archive-not-dispose semantics |
| §3 version + revisionVector | `RetentionPolicy` (already shipped) reads-only here; not mutated. `DocumentDisposal` is append-only (Version: 1; never bumped) |
| §4 Append-only sub-collections | `LegalHold[]` per document is append-only (placement → release marker → re-placement → … all are NEW rows). `DocumentDisposal[]` per document is append-only (one row per disposal action; idempotent re-run produces no new row) |
| §5 Stable string codes | `DisposalEligibility` enum, `DisposalReason` enum surface as string codes (`"Eligible"`, `"BlockedByLegalHold"`, etc.) over the wire |
| §6 Posted-then-immutable | `DocumentDisposal` rows are IMMUTABLE post-write. The `cryptoShredKeyId` recorded at disposal time is the forensic record; even if the underlying key is later mistakenly resurrected, the disposal-ledger row reflects the historical truth |
| §7 State-machine-under-CRDT pattern B — terminal wins | A document is "disposed" once the first `DocumentDisposal` row exists. Concurrent disposal attempts on two replicas converge: the first DocumentDisposal row wins; the second's CryptoShreder no-ops idempotently (the `IBlobStore.DisposeAsync` is idempotent — re-disposing an already-disposed cid is a no-op returning the same `Disposed` outcome). LegalHold state is the *projection* — `IsUnderLegalHold(documentId)` = there exists ≥1 placement with no matching release row, considering CRDT-merged rows from all replicas |
| §10 Two-tier validation | Tier-1 write-time on every `DisposeAsync` invocation (eligibility re-checked at the moment of disposal, even if the sweep-job pre-checked it). Tier-2 post-merge reconciler ships as a stub registration: scans for "any document where `archivedAt + retentionPeriodDays < now - 30 days` and no DocumentDisposal row" → emits `Docs.RetentionSweepStalled` event. Full enforcement is a follow-on |

The combination ensures: (a) two offline replicas can each place independent legal holds and converge to "the union of all open holds blocks disposal"; (b) crypto-shred is forensically reconstructible — the disposal row + the destroyed-key id remain in the audit trail forever; (c) the disposal-ledger is a stable, append-only compliance-attestation surface that downstream report packages can rely on.

### Crypto-shred + retention model (binding)

The Stage 02 §6.4 model:

```
WHEN RetentionPolicy.disposalAction = 'crypto-shred' AND eligibility = Eligible:
  IF StorageRef.kind = 'inline-sqlite-blob':
    SET the inline-blob column to NULL
    APPEND DocumentDisposal row with disposalAction = CryptoShred, cryptoShredKeyId = NULL
  ELSE IF StorageRef.kind = 'fs-content-addressed':
    LOOK UP per-policy envelope key keyId from kernel-security key registry
    CALL IKeyShredder.ShredAsync(keyId)
    APPEND DocumentDisposal row with cryptoShredKeyId = keyId, priorContentHash = StorageRef.contentHash
    SCHEDULE GC of the encrypted file after RetentionSweepGracePeriod (default 24h)
  ELSE IF StorageRef.kind = 'external-uri':
    THROW NotSupportedException — operator must handle external deletion
```

**Irreversibility** (binding):
- After `IKeyShredder.ShredAsync` returns success, the key is unrecoverable. The encrypted bytes on disk are inert ciphertext.
- The 24h GC grace window allows a "panic abort" — an operator who realizes within 24h that the disposal was a mistake can manually halt the GC, but the bytes remain ciphertext that cannot be decrypted because the key is gone. The grace window is a defense-in-depth against accidental over-disposal of file allocations on disk; it does NOT preserve the data.
- The `DocumentDisposal` row is the forensic record of what was destroyed and when. It is immutable per §6.

**Legal hold blocks disposal** (binding):
- `CheckDisposalEligibilityAsync` returns `BlockedByLegalHold` whenever the projection of `LegalHold[]` for a document has any open placement (i.e., any placement row with no matching release row).
- `DisposeAsync` re-checks immediately before invoking the disposal-action path. If the eligibility changed between sweep-job enumeration and the per-document call (e.g., a legal hold was placed concurrently), `DisposeAsync` aborts with `DisposalError.BlockedByLegalHold` and the document remains intact.
- `PlaceLegalHoldAsync` is allowed on documents that are already past expiry; the hold blocks the sweep from disposing them.

### Composition with `foundation-security-policy.IRetentionEnforcer` (binding)

This is the **explicit non-redefinition discipline** for this hand-off. `foundation-security-policy.SecurityPolicyRetentionEnforcer` exists (per W#37 P1 PR 3b.2) and applies jurisdiction floors to **audit records**. The audit-side enforcer is keyed on `AuditEventClass`; this hand-off's document-side enforcer is keyed on `DocumentType`. They are **separate concerns with parallel shapes**.

What this hand-off does:
- Defines a NEW interface `Sunfish.Blocks.DocsRetention.IDocumentRetentionFloorEnforcer` modeled on the same `RetentionVerdict`-shaped return type from `foundation-security-policy` (lift the `RetentionVerdict` record by name; do NOT re-declare).
- The default impl `DocumentRetentionFloorEnforcer`:
  - Reads `ITenantSecurityPolicyLoader.GetActiveAsync(tenant)` (from `foundation-security-policy`) to obtain the active `JurisdictionPreset`.
  - Maps `Document.documentType` → applicable `AuditEventClass`-analogue (e.g., `policy` + `procedure` → Configuration-class-floor; `signed-pdf` + `contract-instance` → Identity-class-floor; `wiki-page` → no floor).
  - Returns a `RetentionVerdict` whose `MinimumHoldUntil` is the maximum of (attached `RetentionPolicy.retentionPeriodDays`-derived window, jurisdiction floor).
  - Sets `IsJurisdictionFloor: true` whenever the floor wins.
- `CheckDisposalEligibilityAsync` and `DisposeAsync` consult the floor enforcer BEFORE returning Eligible. A document under a jurisdiction floor is `NotYetExpired` even if `RetentionPolicy.retentionPeriodDays` has elapsed.

What this hand-off does NOT do:
- Does NOT modify or extend `foundation-security-policy`. (If a missing pattern is discovered — e.g., the `JurisdictionPreset → DocumentType-floor` mapping should live in `foundation-security-policy` not here — file `cob-question-*` first; do NOT speculatively add interfaces to foundation-security-policy from this hand-off.)
- Does NOT redefine `IRetentionEnforcer`. The audit-side concern stays on the audit-side interface.
- Does NOT shred audit records. Audit-side disposal is `kernel-audit`'s scheduled purge, governed by `foundation-security-policy.SecurityPolicyRetentionEnforcer`.

---

## Pre-build checklist (dev / COB executes before opening PR 1)

1. **Verify `blocks-docs-core` is built.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/RetentionPolicy.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/Document.cs
   grep -n "DisposalAction\|CryptoShred\|NotSupportedException" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/RetentionPolicy.cs 2>&1
   ```
   Expected: all four checks succeed; `RetentionPolicy.cs` carries the `CryptoShred` enum value, the `DisposalAction` enum is intact, and the gating `throw new NotSupportedException("blocks-docs-retention will wire this")` (or equivalent comment-marker) is present. If `blocks-docs-core` is not built, **STOP** — file `cob-question-*` requesting docs-core sequence-up.

2. **Verify `blocks-docs` (attachment substrate) is built.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs/Models/StorageRef.cs
   ```
   Expected: both exist (the StorageRef discriminated union must be the canonical one — this hand-off consumes it, does NOT redefine).

3. **Verify `foundation-security-policy.IRetentionEnforcer` (from W#37 P1 PR 3b.2) is built.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-security-policy/Retention/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-security-policy/Retention/SecurityPolicyRetentionEnforcer.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-security-policy/Retention/RetentionVerdict.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-security-policy/Policy/ITenantSecurityPolicyLoader.cs
   ```
   Expected: all four exist (per W#37 P1 PR 3b.2 + PR 3b.4). If `SecurityPolicyRetentionEnforcer` is absent, **STOP** — file `cob-question-*` (W#37 P1 PR 3b.2 must land first; this hand-off cannot lift the `RetentionVerdict` shape without it).

4. **Verify `foundation/Blobs/IBlobStore.cs` exists and is the canonical primitive.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/IBlobStore.cs
   grep -n "DisposeAsync\|Dispose\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation/Blobs/IBlobStore.cs 2>&1
   ```
   Expected: `IBlobStore.cs` exists; `DisposeAsync` likely **NOT** present yet (this hand-off introduces it as an additive interface method). If `DisposeAsync` is already present with a different shape, **STOP** + file `cob-question-*` describing the conflict (PR 3 of this hand-off introduces the method; a pre-existing method with a different shape requires reconciliation).

5. **Verify no parallel-session PRs touch `blocks-docs-retention` or `foundation/Blobs/`.**
   ```bash
   gh pr list --state open --search "blocks-docs-retention in:title,body"
   gh pr list --state open --search "IBlobStore in:files"
   gh pr list --state open --search "RetentionEnforcer in:title,body,files"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*`.

6. **Verify `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

7. **Confirm ADR 0068 §GC.1 awareness.**
   ```bash
   grep -n "§GC.1" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0068-tenant-security-policy.md | head -5
   ```
   This hand-off's surface (retention enforcement, legal hold, crypto-shred) is **squarely within** the §GC.1 general-counsel scope. Every NEW public type in PR 2 + PR 3 + PR 4 MUST carry an XML `<remarks>` block referencing ADR 0068 §GC.1 (same pattern as W#37 P1 PR 3b.2).

8. **Read the Stage 02 design source sections.** Skim `blocks-docs-schema-design.md` §3.1.7 (RetentionPolicy entity), §6.4 (Crypto-shred + retention), §7.3 (kernel-security delegation), §9 OQ #2 (crypto-shred granularity). Read `crdt-friendly-schema-conventions.md` §2, §4, §6, §7. Read ADR 0068 §5 (audit retention enforcement) for the pattern parallel.

9. **Read the W#37 P1 PR 3b.2 hand-off** sections on `SecurityPolicyRetentionEnforcer` + `RetentionVerdict` (lines 443–535). The composition pattern in PR 2 + PR 3 of this hand-off lifts that pattern verbatim; the document-side floor enforcer is structurally identical with `AuditEventClass` swapped for `DocumentType`.

---

## Per-PR deliverables

This hand-off splits into **4 PRs** by responsibility:

- **PR 1** — Package scaffold + `LegalHold` + `DocumentDisposal` entities + `DisposalEligibility` enum + `DisposalReason` enum + repositories + DI
- **PR 2** — `IDocumentRetentionService` + `IDocumentRetentionFloorEnforcer` (composes with foundation-security-policy) — **.NET architect council MANDATORY** on background sweep concurrency
- **PR 3** — `CryptoShreder` + `IBlobStore.DisposeAsync` extension + `IKeyShredder` stub + `IDocumentRetentionSweepJob` background worker — **security-engineering council MANDATORY**
- **PR 4** — DI umbrella `AddSunfishDocsRetention()` + apps/docs page + NOTICE.md + ledger flip

PRs 1 + 2 are sequential. PR 3 depends on PR 2 (sweep job consumes the service). PR 4 lands after PR 3 merges.

---

### PR 1 — Package scaffold + LegalHold + DocumentDisposal entities + repositories + DI

**Estimated effort:** ~2h
**Scope:** new package `blocks-docs-retention`; LegalHold + DocumentDisposal entities + supporting enums + in-memory repositories + DI extension stub. NO service layer (PR 2); NO disposal logic (PR 3); NO sweep job (PR 3).
**Commit subject:** `feat(blocks-docs-retention): PR 1 — scaffold + LegalHold + DocumentDisposal + repositories + DI`
**Branch:** `dev/blocks-docs-retention-scaffold` (or `cob/...` if cob owns)

#### Package skeleton

```
packages/blocks-docs-retention/
├── README.md
├── NOTICE.md                              (Mayan EDMS attribution; foundation-security-policy composition note)
├── Sunfish.Blocks.DocsRetention.csproj
├── Models/
│   ├── LegalHoldId.cs                     (ULID-backed strong-typed Id; pattern-001)
│   ├── LegalHold.cs                       (entity — append-only placement row)
│   ├── LegalHoldRelease.cs                (entity — append-only release marker row pointing at a placement)
│   ├── DocumentDisposalId.cs              (ULID-backed strong-typed Id)
│   ├── DocumentDisposal.cs                (entity — append-only disposal-ledger row)
│   ├── DisposalEligibility.cs             (enum — Eligible / BlockedByLegalHold / NotYetExpired / NoPolicyAttached / AlreadyDisposed)
│   ├── DisposalReason.cs                  (enum — RetentionExpired / OperatorInitiated / LegalErasureRequest)
│   ├── DisposalAction.cs                  (re-exported from blocks-docs-core for ergonomic API; OR consumed via using; pick at PR 1 review)
│   └── BlocksDocsRetentionOptions.cs      (RetentionSweepInterval / RetentionSweepGracePeriod / SweepBatchSize / SweepBatchPauseMilliseconds)
├── Services/
│   ├── ILegalHoldRepository.cs            (interface)
│   ├── InMemoryLegalHoldRepository.cs     (in-memory impl; pattern-001)
│   ├── IDocumentDisposalRepository.cs     (interface)
│   └── InMemoryDocumentDisposalRepository.cs
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs     (AddBlocksDocsRetention() — PR 1 registers repos only; PR 2/3/4 expand)
└── tests/
    └── Sunfish.Blocks.DocsRetention.Tests/
        ├── Sunfish.Blocks.DocsRetention.Tests.csproj
        └── (PR 1 tests; PR 2/3 add)
```

#### csproj dependencies

```xml
<ItemGroup>
  <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  <ProjectReference Include="..\foundation-events\Sunfish.Foundation.Events.csproj" />
  <ProjectReference Include="..\foundation-security-policy\Sunfish.Foundation.SecurityPolicy.csproj" />
  <ProjectReference Include="..\blocks-docs\Sunfish.Blocks.Docs.csproj" />
  <ProjectReference Include="..\blocks-docs-core\Sunfish.Blocks.DocsCore.csproj" />
  <ProjectReference Include="..\blocks-people-foundation\Sunfish.Blocks.People.Foundation.csproj" />
</ItemGroup>
```

Note: `blocks-people-foundation` provides `PartyId` for `placedBy` / `releasedBy` / `disposedBy` typing. `foundation-security-policy` reference is needed for the `RetentionVerdict` lift (PR 2). `blocks-docs` reference is needed for `StorageRef` (PR 3 disposal-tier branching). `blocks-docs-core` reference is needed for `DocumentId` + `RetentionPolicy` + `RetentionPolicyId`.

#### Entity shapes (PR 1 — record types only; no business logic)

`LegalHold` (append-only placement row):
```
Fields:
- id: LegalHoldId
- tenantId: TenantId (foundation-multitenancy)
- documentId: DocumentId (FK blocks-docs-core)
- reason: string (free text; max 2000 chars; not empty)
- placedAt: DateTimeOffset (set at construction; immutable)
- placedBy: PartyId (immutable)
- version: int (always 1; LegalHold is append-only — §6)
- revisionVector: Dictionary<string, long> (CRDT vector; readable; not mutated)
```

`LegalHoldRelease`:
```
Fields:
- id: LegalHoldId (release row's own id; ULID)
- legalHoldId: LegalHoldId (FK to the placement row this release targets)
- tenantId: TenantId
- releasedAt: DateTimeOffset (immutable)
- releasedBy: PartyId (immutable)
- releaseReason: string | null (free text; optional; max 2000 chars)
```

Naming consideration: `LegalHoldRelease.id` collides ambiguously with `LegalHold.id` if both use the same strong-typed `LegalHoldId`. Recommend: introduce `LegalHoldReleaseId` as a separate strong-typed Id. Validate at PR 1 review.

`DocumentDisposal`:
```
Fields:
- id: DocumentDisposalId
- tenantId: TenantId
- documentId: DocumentId
- documentVersionId: DocumentVersionId? (the latest version at disposal time; null if document had no versions yet — defensive)
- disposalAction: DisposalAction (mirror of RetentionPolicy.disposalAction at disposal time)
- disposalReason: DisposalReason (RetentionExpired | OperatorInitiated | LegalErasureRequest)
- disposedAt: DateTimeOffset (immutable)
- disposedBy: PartyId (immutable)
- cryptoShredKeyId: string | null (the kernel-security key id destroyed; null for non-crypto-shred disposal)
- priorContentHash: string | null (the StorageRef.contentHash at disposal time; forensic crosscheck)
- priorStorageRefKind: string | null (the StorageRef.kind at disposal time — 'inline-sqlite-blob' / 'fs-content-addressed' / 'external-uri')
- retentionPolicyIdAtDisposal: RetentionPolicyId (the policy whose expiry triggered this — or null for OperatorInitiated)
- version: int (always 1)
- revisionVector: Dictionary<string, long>
```

`DisposalEligibility` enum:
```
- Eligible
- BlockedByLegalHold
- NotYetExpired
- NoPolicyAttached
- AlreadyDisposed
- BlockedByJurisdictionFloor  (set when foundation-security-policy floor wins; PR 2 distinguishes from NotYetExpired for UX)
```

`DisposalReason` enum:
```
- RetentionExpired       (sweep-job invoked; policy expiry the trigger)
- OperatorInitiated      (manual disposal via service API; not auto-sweep)
- LegalErasureRequest    (operator-attested erasure-right satisfaction; ADR 0068 §5.1 caveat applies)
```

#### Repository invariants

- `ILegalHoldRepository.AddPlacementAsync(LegalHold)` — append-only; cannot mutate an existing placement.
- `ILegalHoldRepository.AddReleaseAsync(LegalHoldRelease)` — append-only; rejects if `legalHoldId` does not exist; rejects if a release row already exists for that placement (CRDT-projection: the FIRST release wins; subsequent attempts are no-ops returning the existing release row).
- `ILegalHoldRepository.GetOpenPlacementsAsync(DocumentId)` — returns placements with no matching release row.
- `ILegalHoldRepository.IsUnderLegalHoldAsync(DocumentId)` — returns true iff `GetOpenPlacementsAsync` is non-empty.
- `IDocumentDisposalRepository.AddAsync(DocumentDisposal)` — append-only; rejects if a disposal row already exists for the `documentId` (the FIRST disposal wins; subsequent calls return the existing row with an `AlreadyDisposed` sentinel — see PR 2 `DisposeAsync` for the call-site flow).
- `IDocumentDisposalRepository.GetByDocumentAsync(DocumentId)` — returns the canonical disposal row (or null).
- Tenant-isolation enforced at every repository entry point (mirror existing pattern in `blocks-docs-core/Services/InMemoryDocumentRepository.cs`).

#### Tests (PR 1) — ≥ 8 unit tests

- `LegalHold_AddPlacement_RoundTrip` — append-and-read.
- `LegalHold_AddRelease_RequiresExistingPlacement` — reject orphan release.
- `LegalHold_AddRelease_FirstWinsIdempotent` — second release attempt on same placement returns the first release row, no new write.
- `LegalHold_GetOpenPlacements_AfterRelease_ExcludesReleased` — projection correctness.
- `LegalHold_GetOpenPlacements_TwoPlacementsOneReleased_ReturnsOpenOne` — multi-placement scenario.
- `DocumentDisposal_AddFirstDisposal_RoundTrip` — happy path.
- `DocumentDisposal_AddSecondDisposalSameDocument_IdempotentReturnsExisting` — first-write-wins.
- `LegalHold_TenantIsolation` — two tenants placing holds on documents with same `Guid` material cannot see each other's rows.
- `DocumentDisposal_TenantIsolation` — same.

#### Council instructions (PR 1)

**Standard self-audit only** (substrate scope; no business logic). Spot-check ULID strong-typing on both Ids. Verify `cancellationToken` propagation through all `*Async` repository methods. Verify XML `<remarks>` blocks on `LegalHold`, `LegalHoldRelease`, `DocumentDisposal` reference ADR 0068 §GC.1.

---

### PR 2 — IDocumentRetentionService + IDocumentRetentionFloorEnforcer — .NET architect council MANDATORY

**Estimated effort:** ~3h
**Scope:** the policy-application service + the foundation-security-policy composition layer. NO disposal-action implementation (PR 3); NO background sweep (PR 3).
**Commit subject:** `feat(blocks-docs-retention): PR 2 — IDocumentRetentionService + jurisdiction-floor composition with foundation-security-policy`
**Branch:** `dev/blocks-docs-retention-service`

#### Files

```
packages/blocks-docs-retention/
├── Services/
│   ├── IDocumentRetentionService.cs                 (interface)
│   ├── DocumentRetentionService.cs                  (impl)
│   ├── IDocumentRetentionFloorEnforcer.cs           (interface — composes with foundation-security-policy)
│   ├── DocumentRetentionFloorEnforcer.cs            (default impl)
│   ├── DocumentRetentionFloorOptions.cs             (DocumentTypeFloorMap — maps DocumentType → analogue of AuditEventClass for floor lookup)
│   ├── ApplyPolicyResult.cs                         (record with IsSuccess + error enum + Detail)
│   ├── ApplyPolicyError.cs                          (enum — None / DocumentNotFound / PolicyNotFound / TenantMismatch / DocumentAlreadyDisposed)
│   ├── PlaceLegalHoldResult.cs
│   ├── PlaceLegalHoldError.cs                       (enum — None / DocumentNotFound / TenantMismatch / DocumentAlreadyDisposed / ReasonRequired)
│   ├── ReleaseLegalHoldResult.cs
│   ├── ReleaseLegalHoldError.cs                     (enum — None / HoldNotFound / TenantMismatch / AlreadyReleased)
│   ├── DisposalEligibilityResult.cs                 (record carrying eligibility verdict + reason + the binding RetentionVerdict from the floor enforcer)
│   ├── DisposeResult.cs                             (record carrying IsSuccess + DocumentDisposal? row + error)
│   └── DisposeError.cs                              (enum — None / NotEligible / BlockedByLegalHold / BlockedByJurisdictionFloor / NoPolicyAttached / DocumentNotFound / TenantMismatch / DisposalActionNotSupported)
└── Events/
    ├── DocumentRetentionAppliedEvent.cs
    ├── LegalHoldPlacedEvent.cs
    ├── LegalHoldReleasedEvent.cs
    └── DocumentDisposedEvent.cs
```

#### `IDocumentRetentionService` shape

```
namespace Sunfish.Blocks.DocsRetention;

/// <remarks>
/// See §GC.1 in ADR 0068. Document retention windows and disposal actions
/// (especially crypto-shred under HIPAA / GDPR / PCI-DSS jurisdictions)
/// are per-deployment legal determinations. This service provides the
/// enforcement mechanism; the determination of whether a specific
/// disposal is lawful is the deployer's responsibility with qualified
/// counsel. See ADR 0068 §5.1 on the right-to-erasure caveat.
/// </remarks>
public interface IDocumentRetentionService
{
    ValueTask<ApplyPolicyResult> ApplyPolicyAsync(
        DocumentId documentId,
        RetentionPolicyId retentionPolicyId,
        PartyId appliedBy,
        CancellationToken ct = default);

    ValueTask<DisposalEligibilityResult> CheckDisposalEligibilityAsync(
        DocumentId documentId,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    ValueTask<PlaceLegalHoldResult> PlaceLegalHoldAsync(
        DocumentId documentId,
        string reason,
        PartyId placedBy,
        CancellationToken ct = default);

    ValueTask<ReleaseLegalHoldResult> ReleaseLegalHoldAsync(
        LegalHoldId legalHoldId,
        string? releaseReason,
        PartyId releasedBy,
        CancellationToken ct = default);

    ValueTask<DisposeResult> DisposeAsync(
        DocumentId documentId,
        DisposalReason reason,
        PartyId disposedBy,
        CancellationToken ct = default);
}
```

`DisposeAsync` semantics:
1. Verify tenant match against ambient `ITenantContext`.
2. Re-run `CheckDisposalEligibilityAsync(documentId, now)` — must be `Eligible`. If not, return `DisposeError` corresponding to the eligibility.
3. Resolve the `RetentionPolicy` from the document; the `disposalAction` selects the implementation strategy.
4. **PR 2 ships the orchestration ONLY.** The actual disposal-action implementations (`Archive`, `SoftDelete`, `CryptoShred`) are injected via `IDisposalActionStrategy[]` — a strategy-pattern interface PR 2 introduces. PR 2's in-memory `ArchiveDisposalActionStrategy` + `SoftDeleteDisposalActionStrategy` ship working impls (they mutate `Document.archivedAt`); PR 2's `CryptoShredDisposalActionStrategy` ships throwing `NotSupportedException` (PR 3 replaces with the real impl). This split keeps PR 2's scope on the service layer + composition; PR 3 is the security-critical disposal wiring.
5. After the strategy succeeds, append the `DocumentDisposal` row via `IDocumentDisposalRepository.AddAsync`. If the repo returns `AlreadyDisposed` (the first-wins resolution), return the existing row with `DisposeResult.IsSuccess = true` (idempotent).
6. Publish `Docs.DocumentDisposed` event with idempotency key `document-disposed:{documentId}`.

#### `IDocumentRetentionFloorEnforcer` shape

```
namespace Sunfish.Blocks.DocsRetention;

/// <remarks>
/// Composes with Sunfish.Foundation.SecurityPolicy.Retention to apply
/// jurisdiction-derived retention floors (HIPAA / PCI-DSS / GDPR) at the
/// document level. See ADR 0068 §5.2 + §GC.1.
/// </remarks>
public interface IDocumentRetentionFloorEnforcer
{
    // Returns a RetentionVerdict (lifted from foundation-security-policy)
    // expressing the minimum-hold floor for this document. The verdict's
    // IsJurisdictionFloor is true when the floor wins over the document's
    // attached RetentionPolicy.retentionPeriodDays.
    ValueTask<RetentionVerdict> ResolveAsync(
        TenantId tenant,
        DocumentType documentType,
        DateTimeOffset documentCreatedAt,
        RetentionPolicy? attachedPolicy,
        CancellationToken ct = default);
}
```

The default `DocumentRetentionFloorEnforcer`:
- Reads `ITenantSecurityPolicyLoader.GetActiveAsync(tenant)` for the active `JurisdictionPreset`.
- Maps `DocumentType` → applicable floor (configurable via `DocumentRetentionFloorOptions.DocumentTypeFloorMap`):
  - `Policy` + `Procedure` + `ContractTemplate` + `ContractInstance` + `SignedPdf` → `Configuration`-analogue (HIPAA 6-year floor applies).
  - `WikiPage` + `Generic` → no floor (HIPAA, PCI-DSS, GDPR all silent at the default mapping).
  - `MarketingAsset` + `BrandKitEntry` → no floor.
- Computes `floor = ApplyJurisdictionFloor(preset, mappedClass)` — lifted directly from `SecurityPolicyRetentionEnforcer`'s §5.2 logic. The lift is by **invocation**, not duplication: this enforcer composes the audit-side enforcer's pattern but does NOT redefine the floor TimeSpan values. **If the audit-side enforcer exposes a public helper for "apply floor", call it. If it does not, this hand-off documents the floor values inline AND files a `dev-question-*` recommending the audit-side enforcer expose them.** (See Halt §3.)
- Returns a `RetentionVerdict` with `MinimumHoldUntil = max(documentCreatedAt + attachedPolicy.retentionPeriodDays, documentCreatedAt + floor)` and `IsJurisdictionFloor: true` iff the floor wins.
- When `attachedPolicy == null`, returns a verdict with `MinimumHoldUntil = documentCreatedAt + floor` (or `DateTimeOffset.MaxValue` if no floor applies).
- When `attachedPolicy.legalHold == true`, the verdict's `MinimumHoldUntil` is `DateTimeOffset.MaxValue` (legal hold blocks indefinitely; jurisdiction floor is moot).

#### `CheckDisposalEligibilityAsync` algorithm

```
1. Load Document by id; tenant-check; if missing → DisposalEligibility.NoPolicyAttached (semantic conflation: "no document, no policy"; PR 2 review can split if needed).
2. Check IDocumentDisposalRepository.GetByDocumentAsync — if non-null, return AlreadyDisposed.
3. Check ILegalHoldRepository.IsUnderLegalHoldAsync — if true, return BlockedByLegalHold.
4. If Document.retentionPolicyId == null, return NoPolicyAttached.
5. Load RetentionPolicy; if RetentionPolicy.legalHold == true (the policy itself flags hold), return BlockedByLegalHold.
6. Compute expiry: documentCreatedAt + retentionPolicy.retentionPeriodDays. If retentionPeriodDays == null (indefinite hold), return NotYetExpired.
7. If now < expiry, return NotYetExpired.
8. Invoke IDocumentRetentionFloorEnforcer.ResolveAsync(...). If verdict.MinimumHoldUntil > now AND verdict.IsJurisdictionFloor, return BlockedByJurisdictionFloor.
9. If verdict.MinimumHoldUntil > now (but not jurisdiction-floor — the attached policy itself is unexpired), return NotYetExpired.
10. Return Eligible.
```

#### Concurrency posture (background sweep — PR 3 lands the job; PR 2 defines the contracts)

The service surface MUST be safe for concurrent invocation from (a) operator manual calls and (b) the background sweep job (PR 3). Per-document advisory locking is implemented via `ConcurrentDictionary<DocumentId, SemaphoreSlim>` (or an equivalent `KeyedLock` primitive — coordinate with `.NET architect` council).

Required properties (verified in council review):
- `ApplyPolicyAsync` + `PlaceLegalHoldAsync` + `ReleaseLegalHoldAsync` + `DisposeAsync` MUST take the per-document lock for the document being mutated.
- `CheckDisposalEligibilityAsync` does NOT take a lock (read-only; the result is advisory; the locking `DisposeAsync` re-checks under lock).
- All `*Async` methods honor `CancellationToken` propagation — the lock acquisition itself is cancellation-aware (use `SemaphoreSlim.WaitAsync(ct)`).
- All `ConfigureAwait(false)` per Phase 1 wave convention.
- `TimeProvider` injected (NOT `DateTimeOffset.UtcNow` directly) — same convention as W#37 P1 PR 3b.2.

#### Tests (PR 2) — ≥ 14 unit tests

- `ApplyPolicy_Success_SetsDocumentRetentionPolicyId`
- `ApplyPolicy_DocumentNotFound_ReturnsError`
- `ApplyPolicy_DocumentAlreadyDisposed_ReturnsError`
- `CheckDisposalEligibility_NoPolicy_ReturnsNoPolicyAttached`
- `CheckDisposalEligibility_LegalHoldOpen_ReturnsBlockedByLegalHold`
- `CheckDisposalEligibility_PolicyExpiredNoFloor_ReturnsEligible`
- `CheckDisposalEligibility_PolicyExpiredHipaaFloorActiveOnPolicy_ReturnsBlockedByJurisdictionFloor`
- `CheckDisposalEligibility_AlreadyDisposed_ReturnsAlreadyDisposed`
- `CheckDisposalEligibility_LegalHoldOnPolicyTrue_ReturnsBlockedByLegalHold`
- `PlaceLegalHold_Success_AppendsRowAndEmitsEvent`
- `PlaceLegalHold_DocumentAlreadyDisposed_ReturnsError`
- `ReleaseLegalHold_Success_AppendsReleaseAndEmitsEvent`
- `ReleaseLegalHold_AlreadyReleased_IdempotentReturnsExisting`
- `Dispose_NotEligible_AbortsAndReturnsError`
- `Dispose_Eligible_InvokesArchiveStrategy_AppendsDisposalRow_EmitsEvent` (PR 2 covers Archive + SoftDelete strategies; CryptoShred coverage moves to PR 3)
- `Dispose_RaceCondition_FirstWinsIdempotent` — two concurrent DisposeAsync calls on the same document → both return success; only one DocumentDisposal row exists.
- `FloorEnforcer_HipaaPresetActive_FloorWinsOver1YearAttachedPolicy_OnPolicyType`
- `FloorEnforcer_GdprPresetActive_NoAutoFloor_AttachedPolicyWins`
- `FloorEnforcer_NoAttachedPolicy_FloorBecomesMinimumHold`
- `FloorEnforcer_LegalHoldTrueOnPolicy_VerdictIsIndefinite`

#### Council instructions (PR 2) — **.NET architect MANDATORY**

**.NET architect:**
- Verify the per-document advisory locking pattern is correct under contention (no deadlocks; no starved waiters).
- Verify `ConfigureAwait(false)` consistency.
- Verify `CancellationToken` propagation to every async hop (including the lock acquisition).
- Verify `TimeProvider` injection.
- Verify all `XxxResult` records are immutable.
- Verify the strategy-pattern dispatch (`IDisposalActionStrategy[]`) honors injection ordering — if two strategies claim the same `DisposalAction`, the registration MUST fail at DI configuration time (not at first invocation).
- Verify event emission is post-commit (after `IDocumentDisposalRepository.AddAsync` succeeds; not before).

**security-engineering (RECOMMENDED but not mandatory on PR 2):**
- Verify `BlockedByJurisdictionFloor` cannot be bypassed by passing an unexpired-but-floored policy through `ApplyPolicyAsync`.
- Verify event payloads carry no document body bytes (only IDs + metadata).
- Verify legal-hold release is not reversible by a re-place — a re-placed hold is a NEW placement with a new id, so the audit trail captures the gap.

---

### PR 3 — CryptoShreder + IBlobStore.DisposeAsync + IKeyShredder stub + IDocumentRetentionSweepJob — security-engineering council MANDATORY

**Estimated effort:** ~3h
**Scope:** the crypto-shred disposal path + the additive `IBlobStore.DisposeAsync` interface method + an in-memory `IKeyShredder` stub + the background sweep job. **THIS PR IS THE COMPLIANCE-ATTESTATION SURFACE** — security-engineering council MANDATORY.
**Commit subject:** `feat(blocks-docs-retention): PR 3 — CryptoShreder + IBlobStore.DisposeAsync + sweep job (security council)`
**Branch:** `dev/blocks-docs-retention-cryptoshred`

#### Files

```
packages/blocks-docs-retention/
├── Services/
│   ├── CryptoShredDisposalActionStrategy.cs   (replaces the PR 2 NotSupportedException stub)
│   ├── IKeyShredder.cs                        (stub interface — replaced by kernel-security in a follow-on)
│   ├── InMemoryKeyShredder.cs                 (in-memory key map; ShredAsync destroys an in-memory entry)
│   ├── BlobStoreDisposalAdapter.cs            (calls IBlobStore.DisposeAsync; handles the inline/fs/external branch)
│   ├── IDocumentRetentionSweepJob.cs          (interface — for test overrides)
│   ├── DocumentRetentionSweepJob.cs           (BackgroundService impl)
│   ├── SweepJobOptions.cs                     (cadence + batch size)
│   └── SweepJobResult.cs                      (per-iteration metrics — DocumentsEvaluated / DocumentsDisposed / DocumentsBlocked / Errors)

packages/foundation/Blobs/
├── IBlobStore.cs                              (ADDITIVE — adds DisposeAsync method)
├── DisposalReason.cs                          (NEW; foundation-level enum surfacing why a CAS entry was disposed)
└── (existing files unchanged)

packages/foundation/tests/Sunfish.Foundation.Tests/Blobs/
└── (NEW tests for IBlobStore.DisposeAsync contract — also added to FileSystemBlobStoreTests if applicable)
```

#### `IBlobStore.DisposeAsync` extension — additive contract

```
namespace Sunfish.Foundation.Blobs;

public interface IBlobStore
{
    // (existing methods unchanged: PutAsync, GetAsync, PinAsync, UnpinAsync, ExistsAsync)

    /// <remarks>
    /// Disposes a content-addressed blob. Behavior depends on the underlying
    /// store: file-system stores schedule the file for GC after a grace
    /// window; in-memory stores remove the entry immediately. This method
    /// is idempotent — disposing an already-disposed cid returns
    /// DisposalOutcome.AlreadyDisposed. The caller is responsible for any
    /// key-destruction (crypto-shred); this method handles only the
    /// bytes-side of the disposal.
    /// See blocks-docs-schema-design.md §6.4. See ADR 0068 §GC.1.
    /// </remarks>
    ValueTask<DisposalOutcome> DisposeAsync(
        Cid cid,
        DisposalReason reason,
        CancellationToken ct = default);
}

public enum DisposalOutcome
{
    Disposed,         // bytes scheduled for GC (or removed in-memory); call was the disposer
    AlreadyDisposed,  // idempotent re-call; bytes are already inert/gone
    UnsupportedKind,  // store cannot dispose (e.g., external-URI passed to a CAS-backed store)
}
```

**Existing implementations** must be updated additively in PR 3:
- `InMemoryBlobStore` (likely exists per the `blocks-docs` hand-off PR 3) → remove the bytes from the in-memory dictionary on `DisposeAsync`; return `Disposed`. On second call, return `AlreadyDisposed`.
- `FileSystemBlobStore` (canonical from `foundation/Blobs/`) → write a tombstone marker file (`<cid>.disposed.json`) carrying `{ disposedAt, reason }`, schedule actual file delete via a background pinner sweep after `RetentionSweepGracePeriod` (default 24h). On second call, return `AlreadyDisposed`.

**Council instruction (binding):** the `FileSystemBlobStore.DisposeAsync` must NOT delete the file synchronously. The 24h grace window is the defense-in-depth against accidental over-disposal. The marker file is sufficient to halt sync-time `GetAsync`; the actual file delete is a separate sweep.

#### `CryptoShredDisposalActionStrategy` shape

```
namespace Sunfish.Blocks.DocsRetention;

public sealed class CryptoShredDisposalActionStrategy : IDisposalActionStrategy
{
    public DisposalAction Action => DisposalAction.CryptoShred;

    public async ValueTask<DisposalActionOutcome> ExecuteAsync(
        DocumentId documentId,
        Document document,
        RetentionPolicy policy,
        DisposalReason reason,
        CancellationToken ct = default)
    {
        // 1. Resolve the StorageRef from Document.currentVersionId → DocumentVersion.contentStorageRef.
        // 2. Branch on StorageRef.kind:
        //    - 'inline-sqlite-blob': call _blobStoreDisposalAdapter.DisposeAsync(cid, DisposalReason.RetentionExpired)
        //      → outcome.Disposed or AlreadyDisposed; cryptoShredKeyId remains null in the returned outcome.
        //    - 'fs-content-addressed': call _keyShredder.LookupKeyAsync(retentionPolicyId) to get the per-policy keyId;
        //      call _keyShredder.ShredAsync(keyId); call _blobStoreDisposalAdapter.DisposeAsync(cid, ...);
        //      return outcome carrying the destroyed keyId.
        //    - 'external-uri': throw NotSupportedException — caller (DocumentRetentionService.DisposeAsync) catches
        //      and returns DisposeError.DisposalActionNotSupported. (See Halt §4 on whether to surface as an enum
        //      vs. propagate the exception.)
        // 3. Return DisposalActionOutcome with the metadata to populate DocumentDisposal row.
    }
}
```

`DisposalActionOutcome` record:
```
- cryptoShredKeyId: string | null
- priorContentHash: string | null
- priorStorageRefKind: string | null
- blobDisposalOutcome: DisposalOutcome
```

#### `IKeyShredder` stub

```
namespace Sunfish.Blocks.DocsRetention;

/// <remarks>
/// Stub interface. Canonical implementation ships from kernel-security in a
/// follow-on hand-off. v1 InMemoryKeyShredder destroys an in-memory key
/// entry — sufficient for Anchor single-tenant demo + tests. NOT a
/// production-grade key-destruction primitive. See ADR 0068 §GC.1.
/// </remarks>
public interface IKeyShredder
{
    // Returns the keyId associated with a retention policy. v1 stub uses
    // a deterministic derivation; production impl reads from kernel-security
    // key registry.
    ValueTask<string?> LookupKeyAsync(
        RetentionPolicyId policyId,
        CancellationToken ct = default);

    // Destroys the key. Idempotent.
    ValueTask<ShredOutcome> ShredAsync(
        string keyId,
        CancellationToken ct = default);
}

public enum ShredOutcome
{
    Destroyed,
    AlreadyDestroyed,
    KeyNotFound,
}
```

When kernel-security ships its canonical `IKeyShredder` (or equivalent), the stub deletes; DI swaps; no public surface change in this package.

#### `DocumentRetentionSweepJob` shape

```
namespace Sunfish.Blocks.DocsRetention;

public sealed class DocumentRetentionSweepJob : BackgroundService, IDocumentRetentionSweepJob
{
    private readonly IDocumentRetentionService _service;
    private readonly IDocumentRepository _documents;            // from blocks-docs-core
    private readonly IOptionsMonitor<SweepJobOptions> _options; // hot-reloadable
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DocumentRetentionSweepJob> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            try
            {
                var result = await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Retention sweep: evaluated={Evaluated} disposed={Disposed} blocked={Blocked} errors={Errors}",
                    result.DocumentsEvaluated, result.DocumentsDisposed, result.DocumentsBlocked, result.Errors);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Retention sweep iteration failed; will retry next interval");
            }

            await Task.Delay(opts.SweepInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<SweepJobResult> SweepOnceAsync(CancellationToken ct)
    {
        // 1. Enumerate documents with retentionPolicyId != null AND archivedAt + retentionPeriodDays < now
        //    (paginated by SweepBatchSize).
        // 2. For each batch:
        //    a. For each document, call IDocumentRetentionService.CheckDisposalEligibilityAsync.
        //    b. If Eligible, call IDocumentRetentionService.DisposeAsync with DisposalReason.RetentionExpired.
        //    c. Pause SweepBatchPauseMilliseconds between disposals to avoid CPU/IO storm.
        //    d. Aggregate counts into SweepJobResult.
        // 3. Return SweepJobResult.
    }
}
```

Concurrency + cancellation invariants (council-verified):
- `stoppingToken` is propagated through every async hop (including the per-document `DisposeAsync` calls).
- The job tolerates `IDocumentRetentionService.DisposeAsync` returning `BlockedByLegalHold` for documents whose eligibility changed between enumeration and disposal — counts as `DocumentsBlocked`, not an error.
- The job tolerates `OperationCanceledException` during the inner per-document loop — graceful shutdown drains the in-flight document then exits.
- The job NEVER calls `IDisposalActionStrategy.ExecuteAsync` directly — always goes through `IDocumentRetentionService.DisposeAsync` so the locking + re-check + event emission happen.

#### Tests (PR 3) — ≥ 12 unit tests + IBlobStore contract tests

- `CryptoShred_InlineBlob_DestroysBytesNoKeyShred` — StorageRef.kind = inline-sqlite-blob; DocumentDisposal.cryptoShredKeyId = null; outcome = Disposed.
- `CryptoShred_FsCas_DestroysKeyAndSchedulesBlobGC` — StorageRef.kind = fs-content-addressed; key destroyed; blob marker written.
- `CryptoShred_ExternalUri_ReturnsNotSupported` — StorageRef.kind = external-uri; DisposeError.DisposalActionNotSupported.
- `CryptoShred_AlreadyDisposedBlob_IsIdempotent` — second call returns AlreadyDisposed; no double-shred.
- `CryptoShred_KeyAlreadyDestroyed_IsIdempotent` — IKeyShredder returns AlreadyDestroyed; disposal continues; outcome = Disposed.
- `CryptoShred_KeyNotFound_FailsLoud` — IKeyShredder returns KeyNotFound; raise + log; DocumentDisposal NOT appended (the disposal action did not complete).
- `SweepJob_OneIteration_EligibleDocumentDisposed_BlockedDocumentSkipped`
- `SweepJob_OneIteration_GraceWindowRespected_NoDoubleDispose` — second iteration runs after first iteration's documents; previously-disposed are skipped via `AlreadyDisposed`.
- `SweepJob_CancellationDuringPerDocumentLoop_GracefulShutdown`
- `SweepJob_BatchPauseRespected_NoCpuStorm` (use TimeProvider + capture delays).
- `IBlobStore.DisposeAsync_FileSystemStore_WritesMarkerNotDeletesFile`
- `IBlobStore.DisposeAsync_FileSystemStore_AlreadyDisposed_Idempotent`
- `IBlobStore.DisposeAsync_InMemoryStore_RemovesEntry_Idempotent`

#### Council instructions (PR 3) — **security-engineering MANDATORY**

**security-engineering:**
- Verify `CryptoShredDisposalActionStrategy.ExecuteAsync` cannot bypass the eligibility check (it MUST be invoked only via `IDocumentRetentionService.DisposeAsync` which has the lock + re-check).
- Verify `IBlobStore.DisposeAsync` is truly idempotent — concurrent disposals on the same `Cid` MUST both return success with one returning `Disposed` and the other `AlreadyDisposed`; verify the file-system tombstone-marker write is atomic (use temp-file-rename pattern).
- Verify the 24h grace window for `FileSystemBlobStore`. The window MUST NOT be configurable below 1 hour (raise warning to council; the floor protects against operator mistakes).
- Verify `IKeyShredder.ShredAsync` MUST NOT log the key material. The interface signature accepts a `keyId` (opaque string); the actual key bytes never leave kernel-security.
- Verify `DocumentDisposal.cryptoShredKeyId` is NULL for inline-blob disposals (no key was destroyed; the bytes simply went away).
- Verify the sweep job does NOT swallow exceptions silently — `OperationCanceledException` is the only acceptable swallow; all others log + continue but increment `SweepJobResult.Errors`.
- Verify no event-payload (`Docs.DocumentDisposed`) carries the destroyed key material — only the keyId (opaque).
- Verify the `BlockedByJurisdictionFloor` path cannot be bypassed by the sweep — `DisposeAsync`'s re-check catches it.
- Verify ADR 0068 §5.1 caveat (`DisposalReason.LegalErasureRequest` is operator-attested; not auto-derived) is documented in the `DisposalReason` XML doc.
- Spot-check **negative existence** (per `feedback_council_can_miss_spot_check_negative_existence`): verify NO public API exposes a way to *resurrect* a destroyed key. `IKeyShredder` is shred-only; there is no `UnshredAsync` or `RestoreAsync`.

**.NET architect (RECOMMENDED on PR 3; MANDATORY only if security-engineering flags concurrency concerns):**
- Verify `BackgroundService.ExecuteAsync` honors graceful-shutdown (5s default Host stop timeout).
- Verify `IOptionsMonitor` change tokens trigger a behavior update without a job restart (the sweep loop reads `_options.CurrentValue` each iteration).
- Verify `Task.Delay(opts.SweepInterval, _timeProvider, stoppingToken)` is the canonical pattern (NOT `Thread.Sleep`; NOT `Task.Delay` without `_timeProvider`).

---

### PR 4 — DI umbrella + apps/docs page + NOTICE.md + ledger flip

**Estimated effort:** ~1.5h
**Scope:** wire `AddSunfishDocsRetention()` DI extension that registers all services + strategies + the sweep job; write the apps/docs page; finalize NOTICE.md; flip the ledger.
**Commit subject:** `chore(blocks-docs-retention): PR 4 — DI umbrella + apps/docs + NOTICE + ledger flip`
**Branch:** `dev/blocks-docs-retention-di-docs`

#### `AddSunfishDocsRetention()` DI extension

```
public static IServiceCollection AddSunfishDocsRetention(
    this IServiceCollection services,
    Action<BlocksDocsRetentionOptions>? configureOptions = null,
    Action<SweepJobOptions>? configureSweepJob = null)
{
    // Pattern-005: DI umbrella.
    services.AddOptions<BlocksDocsRetentionOptions>().Bind(...);
    services.AddOptions<SweepJobOptions>().Bind(...);
    if (configureOptions is not null) services.Configure(configureOptions);
    if (configureSweepJob is not null) services.Configure(configureSweepJob);

    // Repositories (PR 1)
    services.AddSingleton<ILegalHoldRepository, InMemoryLegalHoldRepository>();
    services.AddSingleton<IDocumentDisposalRepository, InMemoryDocumentDisposalRepository>();

    // Service layer (PR 2)
    services.AddSingleton<IDocumentRetentionService, DocumentRetentionService>();
    services.AddSingleton<IDocumentRetentionFloorEnforcer, DocumentRetentionFloorEnforcer>();

    // Disposal strategies (PR 2 + PR 3)
    services.AddSingleton<IDisposalActionStrategy, ArchiveDisposalActionStrategy>();
    services.AddSingleton<IDisposalActionStrategy, SoftDeleteDisposalActionStrategy>();
    services.AddSingleton<IDisposalActionStrategy, CryptoShredDisposalActionStrategy>();

    // Key shredder stub (PR 3) — kernel-security swap-target
    services.AddSingleton<IKeyShredder, InMemoryKeyShredder>();

    // Blob disposal adapter (PR 3)
    services.AddSingleton<BlobStoreDisposalAdapter>();

    // Background sweep job (PR 3)
    services.AddHostedService<DocumentRetentionSweepJob>();

    return services;
}
```

DI lifetime decisions:
- Repositories + services + strategies → **Singleton** in-memory v1. When SQLite-backed repositories land (follow-on), the registration likely flips to **Scoped** (mirroring `blocks-docs-core` pattern). Document at PR 4 review.
- `BackgroundService` registration is automatic via `AddHostedService<>` — `Singleton` by .NET convention.

#### Two-overload constructor pattern (audit-enabled both-or-neither)

Mirror the W#34 / W#35 / W#36 / W#60 substrate-only convention: if any DI extension here interacts with audit (it does — `Docs.DocumentDisposed` is an audit-relevant event), add the two-overload constructor pattern to `DocumentRetentionService` — one constructor takes `IAuditWriter`, one omits it (no-op). Verify at PR 4 review.

#### `apps/docs/blocks/docs-retention/overview.md`

```markdown
# blocks-docs-retention

Document-retention enforcement + legal-hold lifecycle + crypto-shred
disposal for the Sunfish Anchor docs cluster.

## Overview

This package enforces `RetentionPolicy` (entity defined in `blocks-docs-core`)
over `Document` records. It provides:

- `IDocumentRetentionService` — apply policy; place/release legal holds;
  check disposal eligibility; dispose.
- `IDocumentRetentionFloorEnforcer` — composes with
  `foundation-security-policy.SecurityPolicyRetentionEnforcer` to apply
  jurisdiction-derived retention floors (HIPAA / PCI-DSS / GDPR) at
  document level.
- `CryptoShredDisposalActionStrategy` — destroys the per-policy envelope
  key via `IKeyShredder` (stub; canonical impl from kernel-security in
  a follow-on); calls `IBlobStore.DisposeAsync(cid, reason)` to inert
  the encrypted bytes.
- `IDocumentRetentionSweepJob` — `BackgroundService` that periodically
  sweeps expired documents and triggers disposal.

## Compliance posture

This package's behavior surfaces are **explicitly within ADR 0068 §GC.1**:
retention windows, jurisdiction floors, crypto-shred semantics, and
legal-hold lifecycle are per-deployment legal determinations. The
package provides the **mechanism**; the determination of whether a
specific disposal action is lawful is the deployer's responsibility
with qualified counsel.

In particular:
- `DisposalReason.LegalErasureRequest` is operator-attested. Whether
  GDPR Article 17(3)(b) exempts a specific record class from
  right-to-erasure is a per-deployment determination (per ADR 0068 §5.1).
- `IDocumentRetentionFloorEnforcer` applies jurisdiction floors as
  *informed defaults*, not as legal advice. See ADR 0068 §5.2 +
  `SecurityPolicyRetentionEnforcer`.

## Quickstart

(~15 lines: register DI; attach a RetentionPolicy to a Document; place
+ release a legal hold; trigger a manual disposal.)

## Crypto-shred granularity (v1 limitation)

Per Stage 02 §9 OQ #2 recommendation (b), v1 ships with **per-policy
envelope key** granularity. All documents whose `Document.retentionPolicyId`
points to the same `RetentionPolicy` share one envelope key; shredding
that key shreds **all** governed documents. Per-blob (per-row) key
granularity is a follow-on intake (Stage 02 §9 OQ #2 (a) — refinement
of `kernel-security`).

## Legal-hold semantics

A legal hold BLOCKS any disposal regardless of retention expiry. Holds
are append-only — each placement is a row; each release is a row pointing
at the placement. A re-placed hold is a NEW placement with a NEW id; the
audit trail captures the gap between release and re-placement.

`DisposeAsync` re-checks eligibility under per-document lock immediately
before invoking the disposal-action strategy — a hold placed concurrently
with a sweep iteration is honored.

## Algorithms

- Disposal eligibility → see `blocks-docs-schema-design.md` §6.4.
- Jurisdiction-floor composition → see ADR 0068 §5.2 +
  `SecurityPolicyRetentionEnforcer`.
- Sweep cadence + grace window → see `SweepJobOptions` defaults
  (`SweepInterval = TimeSpan.FromHours(1)`, `RetentionSweepGracePeriod
  = TimeSpan.FromHours(24)`).

## Related

- `blocks-docs-core` (substrate — provides `Document`, `DocumentVersion`,
  `RetentionPolicy` entity types).
- `blocks-docs` (attachment substrate — provides `IBlobStore` + `StorageRef`).
- `foundation-security-policy` (compositional — provides
  `SecurityPolicyRetentionEnforcer` + `RetentionVerdict` + jurisdiction
  preset logic; ADR 0068).
- `kernel-security` (future — provides canonical `IKeyShredder`).
- `blocks-reports-compliance-attestation` (future — consumes the
  disposal-ledger for compliance reports).
```

#### NOTICE.md

```markdown
# NOTICE — Sunfish.Blocks.DocsRetention

This package's retention-policy lifecycle concepts (document-level
retention + legal-hold + disposal-ledger) derive from Mayan EDMS
(<https://www.mayan-edms.com/>, Apache 2.0 license).

Mayan EDMS version studied: v4.x (as of 2026-05-17).

The Sunfish implementation is original code, distributed under the MIT
License. The Mayan EDMS retention-policy pattern is reproduced with
attribution per Apache 2.0 §4(c) of the Mayan EDMS License.

This package COMPOSES with (does NOT borrow code from)
Sunfish.Foundation.SecurityPolicy.Retention.SecurityPolicyRetentionEnforcer
(W#37 P1 PR 3b.2) to apply jurisdiction-derived retention floors. See
ADR 0068 §5.2 for the floor model + §GC.1 for the general-counsel
notice that applies to all retention-floor surfaces.

No GPL/AGPL code was consulted in the authoring of this package. In
particular: ERPNext (GPLv3), OpenDocMan (GPL), LogicalDOC Community
(LGPL) were NOT consulted.
```

#### Ledger flip

Update `active-workstreams.md` W#71 row → `built`. Standard PR body with test count + PR-chain summary + composition decisions w/ foundation-security-policy.

#### Tests (PR 4) — ≥ 3 tests

- `AddSunfishDocsRetention_RegistersAllExpectedServices` — verify all interfaces resolve from the DI container.
- `AddSunfishDocsRetention_CryptoShredStrategy_RegisteredAndResolvable` — verify the strategy collection contains all three actions.
- `AddSunfishDocsRetention_HostedService_RegisteredOnce` — verify exactly one `IHostedService` registration for the sweep job (idempotent re-registration test).

#### Council instructions (PR 4)

Standard self-audit. Verify the apps/docs page is properly linked from the cluster docs index (likely `apps/docs/blocks/index.md` or equivalent). Verify NOTICE.md is referenced in the csproj `NOTICEFile` property.

---

## Cross-cluster integration

### Consumes (read-only)

- `Sunfish.Blocks.DocsCore.Models.Document` (read fields: `id`, `tenantId`, `archivedAt`, `currentVersionId`, `retentionPolicyId`, `documentType`)
- `Sunfish.Blocks.DocsCore.Models.DocumentVersion` (read fields: `id`, `contentStorageRef`)
- `Sunfish.Blocks.DocsCore.Models.RetentionPolicy` (read fields: `id`, `retentionPeriodDays`, `legalHold`, `disposalAction`, `appliesToTypes`)
- `Sunfish.Blocks.DocsCore.Services.IDocumentRepository` (read-only; PR 2 + PR 3 enumerate)
- `Sunfish.Blocks.DocsCore.Services.IRetentionPolicyRepository` (read-only)
- `Sunfish.Blocks.Docs.Models.StorageRef` (read discriminated union; branch on `kind` in PR 3)
- `Sunfish.Foundation.Blobs.IBlobStore` (PR 3: invoke `DisposeAsync`; otherwise read via existing surface)
- `Sunfish.Foundation.SecurityPolicy.Retention.RetentionVerdict` (lift record type by reference)
- `Sunfish.Foundation.SecurityPolicy.Policy.ITenantSecurityPolicyLoader` (read active `JurisdictionPreset`)
- `Sunfish.Blocks.People.Foundation.PartyId` (typing for `placedBy` / `releasedBy` / `disposedBy`)

### Writes (within this cluster)

- `LegalHold` placement rows (append-only)
- `LegalHoldRelease` rows (append-only)
- `DocumentDisposal` rows (append-only)
- `Document.retentionPolicyId` (set via `ApplyPolicyAsync` — the ONLY write into `blocks-docs-core`; uses an internal cross-cluster contract: must call `IDocumentCommandService.AttachRetentionPolicyAsync` if that surface exists on `blocks-docs-core`; otherwise a direct repository write through `IDocumentRepository.UpdateAsync` is acceptable as a v1 fallback — see Halt §5).

### Composes with (does NOT modify)

- `foundation-security-policy.SecurityPolicyRetentionEnforcer` — pattern lift; jurisdiction-floor logic.
- `kernel-security.IKeyShredder` (future) — production key-destruction primitive.

### Consumed by (this hand-off unblocks)

- `blocks-docs-core` — resolves the `CryptoShred NotSupportedException` gate explicitly documented in the docs-core hand-off §Halt 5. After PR 3 of this hand-off merges, a follow-on micro-PR can flip the docs-core `RetentionPolicy.disposalAction = CryptoShred` no-op to delegate to this cluster's `IDocumentRetentionService.DisposeAsync` when invoked.
- `blocks-docs-wiki` (follow-on) — `Policy.archivedAt` lifecycle eventually triggers `IDocumentRetentionService.DisposeAsync`.
- `blocks-docs-signing` (follow-on) — signed PDFs get long retention floors per Mayan EDMS pattern.
- `blocks-reports-compliance-attestation` (future) — queries `IDocumentDisposalRepository` for the disposal-ledger surface.

---

## Pre-merge council requirements

| PR | Council | Required? | Why |
|---|---|---|---|
| PR 1 | (none) | Self-audit only | Scaffold + append-only entities; substrate scope; mirrors blocks-docs-core PR 1 shape |
| PR 2 | **.NET architect** | **MANDATORY** | Per-document advisory locking; strategy-pattern dispatch; cancellation propagation; foundation-security-policy composition |
| PR 2 | security-engineering | Recommended | Verify `BlockedByJurisdictionFloor` cannot be bypassed; event payloads carry no body bytes |
| PR 3 | **security-engineering** | **MANDATORY** | Crypto-shred + IBlobStore.DisposeAsync wiring is the **compliance-attestation surface** (HIPAA / GDPR / PCI-DSS). Key-destruction irreversibility; grace-window enforcement; idempotency under concurrent disposal; ADR 0068 §GC.1 applies |
| PR 3 | .NET architect | Recommended | BackgroundService lifecycle; IOptionsMonitor change-token semantics; TimeProvider usage |
| PR 4 | (none) | Self-audit only | DI wiring + docs; substrate scope |

**Council-pre-merge canonical** per `feedback_pr_automerge_before_amendment_landed`: create PRs 2 + 3 as `--draft`; flip to open only after ALL council amendments are pushed. Run councils BEFORE auto-merge per `feedback_council_before_automerge`. All council subagents use Opus + xhigh per `feedback_council_reviews_use_best_model_xhigh`.

---

## Idempotency-key catalog (cross-cluster event bus)

Per `cross-cluster-event-bus-design.md` §3 pattern + kebab-case prefix discipline:

| Event | Producer | Idempotency key |
|---|---|---|
| `Docs.DocumentRetentionApplied` | this cluster | `document-retention-applied:{documentId}:{retentionPolicyId}:{appliedAtTicksUtc}` |
| `Docs.LegalHoldPlaced` | this cluster | `legal-hold-placed:{legalHoldId}` |
| `Docs.LegalHoldReleased` | this cluster | `legal-hold-released:{legalHoldReleaseId}` |
| `Docs.DocumentDisposed` | this cluster | `document-disposed:{documentId}` (the FIRST disposal wins; idempotent re-emission allowed via deduplication on this key) |
| `Docs.RetentionSweepStalled` (stub registration in PR 3; full enforcement is follow-on) | this cluster | `retention-sweep-stalled:{tenantId}:{detectedAtTicksUtc}` |

Why `document-disposed:{documentId}` does NOT include a timestamp: because `DisposeAsync` is idempotent and the FIRST disposal wins (per §7 terminal-wins pattern), the event is emitted at most once per `documentId`. Subsequent attempts return the existing row without re-emitting.

Why `legal-hold-placed:{legalHoldId}` does NOT include the documentId: because a single document can have multiple distinct placements over time, each is a distinct legalHoldId; the legalHoldId alone is the unique key.

Schema versioning: all event payloads ship at `schemaVersion: "1.0.0"`. Future additive fields → minor bump; renames or breaking changes → new event type per `cross-cluster-event-bus-design.md` §2.

---

## Dependencies + sequence

### Direct dependencies (must be built first)

1. `blocks-docs-core` — provides `Document` + `DocumentVersion` + `RetentionPolicy` + `IDocumentRepository` + `IRetentionPolicyRepository`.
2. `blocks-docs` — provides `IBlobStore` (consumed) + `StorageRef` (read).
3. `foundation-security-policy/Retention/` (W#37 P1 PR 3b.2 + PR 3b.4) — provides `RetentionVerdict` (lifted) + `ITenantSecurityPolicyLoader` (consumed).
4. `foundation/Blobs/IBlobStore.cs` — PR 3 of this hand-off extends additively with `DisposeAsync`.
5. `blocks-people-foundation` — provides `PartyId`.
6. `foundation-events` — provides `DomainEventEnvelope<TPayload>`.

### Sequence within this hand-off

```
PR 1 (entities + repos + DI scaffold)
  ↓
PR 2 (IDocumentRetentionService + composition; uses PR 1 entities; .NET architect MANDATORY)
  ↓
PR 3 (CryptoShreder + IBlobStore.DisposeAsync + sweep job; uses PR 2 surface; security MANDATORY)
  ↓
PR 4 (DI umbrella + apps/docs + NOTICE + ledger flip)
```

No PR-level parallelization within this hand-off (each PR builds on the previous one's contracts).

### What this hand-off unblocks (downstream)

1. **`blocks-docs-core` micro-PR (recommended)** — once PR 3 merges, a follow-on PR can flip the docs-core `RetentionPolicy.disposalAction = CryptoShred` from `throw NotSupportedException` to delegate to this cluster's `IDocumentRetentionService.DisposeAsync`. This is a 1-line behavior change + a test; recommend a follow-on micro-PR rather than touching docs-core in this hand-off (the dependency arrow this hand-off → docs-core is via DI, not type reference).
2. `blocks-docs-wiki` Policy/Procedure publish + retention lifecycle.
3. `blocks-docs-signing` signed-PDF retention with HIPAA-floor enforcement.
4. `blocks-reports-compliance-attestation` (new future workstream) — disposal-ledger reporting + jurisdiction-floor adherence reports.
5. Any tenant deployment that wants HIPAA / PCI-DSS / GDPR retention attestation can now configure `JurisdictionPreset` on `TenantSecurityPolicy` and have it surface for documents.

---

## License posture

### Borrowed-with-attribution (permissive)

- **Mayan EDMS** (Apache 2.0). Pattern borrowed: RetentionPolicy + legal-hold-blocks-disposal + per-document retention attachment. The `DisposalAction` enum's three values (`Archive` / `SoftDelete` / `CryptoShred`) follow the Mayan EDMS disposal-action vocabulary. NOTICE entry + source-header comment on `IDocumentRetentionService.cs` + `CryptoShredDisposalActionStrategy.cs`.

### Compositional (no code borrowed)

- **`foundation-security-policy.SecurityPolicyRetentionEnforcer`** — pattern composed (jurisdiction-floor application). The `IDocumentRetentionFloorEnforcer` interface MIRRORS the audit-side enforcer's `ResolveAsync` shape. No code copied; identical pattern applied to document-side concerns. Documented in NOTICE.md.

### Clean-room only (copyleft NOT consulted)

- **ERPNext / Frappe** (GPLv3) — document module has retention features; NOT consulted.
- **OpenDocMan** (GPL) — document retention; NOT consulted.
- **LogicalDOC Community** (LGPL) — document retention + disposal; NOT consulted.
- **Nextcloud Hub** (AGPLv3) — file retention app; NOT consulted.

**Discipline check before merging any PR:**

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL source appear in the new code (spot-check by grep before merge — particularly check the `DisposalAction` enum value names against ERPNext / OpenDocMan vocabularies; the names chosen here are derived from Mayan EDMS Apache 2.0 + the Stage 02 schema design).
3. The clean-room schema in `blocks-docs-schema-design.md` §3.1.7 + §6.4 is the source of truth; deviations require XO ratification.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (entities + repos + DI scaffold) | ~9 | append-only invariants; release-after-placement; tenant isolation; idempotent first-wins |
| PR 2 (service + composition) | ~14 | apply / check-eligibility / hold-place / hold-release / dispose; floor enforcement; race condition |
| PR 3 (crypto-shred + sweep + DisposeAsync) | ~13 (incl. IBlobStore contract) | shred per StorageRef.kind; sweep iteration; grace window; idempotency; cancellation |
| PR 4 (DI umbrella + docs) | ~3 | DI resolution; strategy registration; hosted-service registration |
| **Total** | **~39 new** | |

### Invariants (binding — verified across the suite)

These invariants MUST hold across the PR set. Council reviewers verify in PR 3:

| Invariant | Where verified |
|---|---|
| **Retention floor cannot be reduced** by an attached `RetentionPolicy.retentionPeriodDays` shorter than the jurisdiction floor | PR 2 `FloorEnforcer_HipaaPresetActive_FloorWinsOver1YearAttachedPolicy_OnPolicyType` |
| **Legal hold blocks disposal** regardless of retention expiry | PR 2 `CheckDisposalEligibility_LegalHoldOpen_ReturnsBlockedByLegalHold` + PR 3 sweep test |
| **Legal hold release is append-only** — a re-placed hold is a NEW row with a new id | PR 1 `LegalHold_AddRelease_FirstWinsIdempotent` + projection test |
| **Crypto-shred is irreversible after grace window** | PR 3 `IKeyShredder` interface has NO Unshred method (negative-existence verified by council); `IBlobStore.DisposeAsync` returns `AlreadyDisposed` on second call (positive-existence verified) |
| **Disposal-ledger row is the forensic record** — written immutably; preserves `cryptoShredKeyId` + `priorContentHash` + `priorStorageRefKind` | PR 1 `DocumentDisposal` record is immutable + PR 3 `CryptoShred_FsCas_DestroysKeyAndSchedulesBlobGC` verifies the disposal row carries the keyId |
| **First disposal wins** (CRDT terminal-wins per §7) | PR 1 `DocumentDisposal_AddSecondDisposalSameDocument_IdempotentReturnsExisting` + PR 2 `Dispose_RaceCondition_FirstWinsIdempotent` |
| **External-URI crypto-shred is not supported** — must throw or return error, NEVER silently no-op | PR 3 `CryptoShred_ExternalUri_ReturnsNotSupported` |
| **24h grace window** for FileSystemBlobStore CANNOT be configured below 1 hour | PR 3 council instruction verifies + a test asserts the options-binding rejects `< TimeSpan.FromHours(1)` |
| **`DocumentDisposed` event carries no body bytes** — only IDs + metadata | PR 2 council instruction + PR 3 event test |

### Cluster-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.DocsRetention` package + every dependency.

**A2.** `dotnet test packages/blocks-docs-retention/tests/` passes all ~39 new tests; `dotnet test packages/foundation/tests/` passes (including the new IBlobStore.DisposeAsync contract tests).

**A3.** **Apply + dispose round-trip:**
- Create a Document via `IDocumentCommandService.CreateAsync` (`blocks-docs-core`).
- Create a `RetentionPolicy` with `retentionPeriodDays = 1` and `disposalAction = Archive`.
- Call `IDocumentRetentionService.ApplyPolicyAsync(documentId, policyId)`.
- Wait 1 day (TimeProvider-fake; advance clock).
- Call `IDocumentRetentionService.CheckDisposalEligibilityAsync(documentId, asOf: now+1d)`.
- Assert: `Eligible`.
- Call `IDocumentRetentionService.DisposeAsync(documentId)`.
- Assert: `DisposeResult.IsSuccess = true`; `DocumentDisposal` row exists; `Docs.DocumentDisposed` event emitted.
- Call `DisposeAsync` again.
- Assert: `IsSuccess = true`; same `DocumentDisposal` row returned (idempotent); NO second event emitted.

**A4.** **Legal-hold blocks disposal round-trip:**
- Setup as A3 but with `retentionPeriodDays = 1`.
- Place a legal hold via `PlaceLegalHoldAsync`.
- Advance clock past expiry.
- Call `CheckDisposalEligibilityAsync` → `BlockedByLegalHold`.
- Call `DisposeAsync` → `DisposeError.BlockedByLegalHold`.
- Release the hold via `ReleaseLegalHoldAsync`.
- Call `CheckDisposalEligibilityAsync` → `Eligible`.
- Call `DisposeAsync` → success.

**A5.** **Crypto-shred + key destruction round-trip:**
- Create a Document with a `DocumentVersion` whose `contentStorageRef.kind = 'fs-content-addressed'`.
- Pre-register a key for the document's `RetentionPolicy` via `InMemoryKeyShredder`.
- Create a `RetentionPolicy` with `disposalAction = CryptoShred`, `retentionPeriodDays = 1`.
- Apply policy; advance clock; call `DisposeAsync`.
- Assert: `DocumentDisposal.cryptoShredKeyId` = the keyId; `InMemoryKeyShredder.ShredAsync` was called; `IBlobStore.DisposeAsync` returned `Disposed`; the in-memory key map no longer contains the key.

**A6.** **HIPAA jurisdiction-floor enforcement:**
- Setup as A3 but document with `documentType = Policy` and tenant with `JurisdictionPreset = HipaaInformedDefault`.
- Use a `RetentionPolicy` with `retentionPeriodDays = 365` (1 year — under HIPAA's 6-year Configuration-class floor).
- Apply policy; advance clock 2 years.
- Call `CheckDisposalEligibilityAsync` → `BlockedByJurisdictionFloor` (the floor wins; the attached 1-year policy is overridden).
- Advance clock to 7 years.
- Call `CheckDisposalEligibilityAsync` → `Eligible`.

**A7.** **Sweep job round-trip:**
- Seed 10 documents with `retentionPeriodDays = 1`, 5 of them under legal hold.
- Advance clock past expiry.
- Manually invoke `DocumentRetentionSweepJob.SweepOnceAsync(ct)`.
- Assert: `SweepJobResult.DocumentsEvaluated = 10`; `DocumentsDisposed = 5`; `DocumentsBlocked = 5`; `Errors = 0`.
- Re-invoke `SweepOnceAsync`.
- Assert: `DocumentsEvaluated = 5` (the 5 already-disposed are filtered out by the `archivedAt + retentionPeriodDays < now AND no DocumentDisposal row` predicate); `DocumentsDisposed = 0`.

**A8.** **Sweep job cancellation:**
- Seed 100 documents (large batch).
- Start the sweep job in a hosted-service test harness.
- Issue cancellation mid-iteration.
- Assert: in-flight document completes; subsequent documents skipped; no exception bubbled; the host stops within the default 5s graceful-shutdown window.

**A9.** **`IBlobStore.DisposeAsync` idempotency (foundation-level):**
- Create an `InMemoryBlobStore`; put a blob; call `DisposeAsync`.
- Assert: `Disposed`.
- Call `DisposeAsync` again.
- Assert: `AlreadyDisposed`.
- Repeat for `FileSystemBlobStore`: verify marker file written + actual file deletion deferred + second `DisposeAsync` returns `AlreadyDisposed`.

**A10.** **Composition with foundation-security-policy verified by inspection:**
- Reviewer confirms: `IDocumentRetentionFloorEnforcer.ResolveAsync` returns a `RetentionVerdict` of type `Sunfish.Foundation.SecurityPolicy.Retention.RetentionVerdict` (lift, not re-declaration).
- Reviewer confirms: `DocumentRetentionFloorEnforcer` injects `ITenantSecurityPolicyLoader` (not a local re-implementation).
- No new types named `IRetentionEnforcer`, `IAuditRetentionEnforcer`, or `SecurityPolicyRetentionEnforcer` are introduced in this package.

---

## Halt conditions (dev-question-* / cob-question-* beacons)

If the implementer hits any of these, halt the workstream + drop a `dev-question-*` (or `cob-question-*` if cob owns) beacon to `coordination/inbox/`:

### 1. `IRetentionEnforcer` actual name in foundation-security-policy

The W#37 P1 PR 3b.2 hand-off names the interface `IAuditRetentionEnforcer`. The task prompt for this hand-off refers to it as `IRetentionEnforcer`. **Likely actual name:** `IAuditRetentionEnforcer` (per the W#37 hand-off line 458). Pre-build checklist step 3 verifies. If the actual shipped name differs from both, **STOP** and file `dev-question-*` requesting the canonical name + namespace for the lift in this hand-off's `IDocumentRetentionFloorEnforcer`.

### 2. `RetentionVerdict` is internal (not public) in foundation-security-policy

If `Sunfish.Foundation.SecurityPolicy.Retention.RetentionVerdict` is declared `internal` (not `public`), the lift fails at the type-reference layer. **Mitigation:** the implementer should re-declare a local `DocumentRetentionVerdict` record in this package with the SAME shape and file a `dev-question-*` recommending foundation-security-policy widen the access modifier to `public`. Do NOT block this hand-off on the access-modifier flip (it's a separate sub-1h follow-up PR).

### 3. Audit-side jurisdiction-floor logic is private inside `SecurityPolicyRetentionEnforcer`

If `SecurityPolicyRetentionEnforcer.ApplyJurisdictionFloor` is a private method (not callable from this package), the document-side `DocumentRetentionFloorEnforcer` cannot reuse the floor TimeSpan values without duplication. **Mitigation:**
- Option A (preferred): file `dev-question-*` recommending foundation-security-policy expose a public helper `IRetentionJurisdictionFloorResolver.ResolveFloor(JurisdictionPreset preset, AuditEventClass class) → TimeSpan?`. The audit-side enforcer + this hand-off's document-side enforcer both consume it.
- Option B (acceptable v1): duplicate the floor TimeSpan values inline in `DocumentRetentionFloorEnforcer` with comments pointing at `SecurityPolicyRetentionEnforcer`'s implementation; add a comment-level TODO noting the duplication for a follow-on refactor.
- Pick Option B for v1 (unblock progress); file the Option-A `dev-question-*` immediately after PR 2 merges.

### 4. External-URI crypto-shred: enum vs. exception

`CryptoShredDisposalActionStrategy.ExecuteAsync` on `StorageRef.kind = 'external-uri'` — should it throw `NotSupportedException` (per Stage 02 §6.4 wording) or return a `DisposalActionOutcome` carrying a non-success outcome? **XO recommendation:** return a non-success outcome (cleaner exception-flow discipline). `DocumentRetentionService.DisposeAsync` translates to `DisposeError.DisposalActionNotSupported`. If implementer prefers the exception path (cleaner C# idiom for "the requested operation is fundamentally unsupported"), document the choice in PR 3 description and run the security council with the choice as a discussion point.

### 5. `Document.retentionPolicyId` write surface

`ApplyPolicyAsync` needs to set `Document.retentionPolicyId`. If `blocks-docs-core` doesn't expose `IDocumentCommandService.AttachRetentionPolicyAsync` (or equivalent), the implementer has two options:
- Option A: write directly via `IDocumentRepository.UpdateAsync(Document)` — acceptable v1 fallback; document the cross-cluster write in the PR 2 description.
- Option B: file `dev-question-*` requesting a follow-on micro-PR on `blocks-docs-core` adds `IDocumentCommandService.AttachRetentionPolicyAsync` — then re-author PR 2 to call it.
**XO recommendation:** Option A for v1 (unblocks progress; the cross-cluster write is well-bounded — only this one field). Option B as a follow-on cleanup hand-off.

### 6. `kernel-security.IKeyShredder` already exists with a different shape

If `kernel-security/IKeyShredder.cs` already exists with a shape that doesn't match this hand-off's stub, **STOP** + file `dev-question-*` to reconcile. Likely the existing kernel-security surface should be the canonical one and this package consumes it directly. **Do NOT introduce a competing interface name.** If shapes diverge but the intent is identical, prefer renaming this hand-off's stub (e.g., `IDocsKeyShredder`) and consuming the kernel-security primitive under the hood.

### 7. Per-tenant overrides of jurisdiction floors (OPEN QUESTION)

The current design has jurisdiction floors apply uniformly per tenant based on `JurisdictionPreset`. **Open question:** can an individual tenant carry per-DocumentType overrides that REDUCE the floor for specific document types (e.g., a tenant whose deployment counsel has determined that wiki-pages of type X are exempt from HIPAA Configuration-class)? ADR 0068 §1.4.2 EmergencyOverride applies to MFA/attestation policy — does an analogue exist for retention?

**XO recommendation:** v1 does NOT support per-tenant per-DocumentType floor reductions. The deployer's path for an exempt class is to file an operator-attested `DisposalReason.LegalErasureRequest` disposal (which bypasses the eligibility check IF the policy permits — see Halt §8). Document this as a known limitation. If a deployment needs configurable floor reductions, that's a follow-on intake (likely co-located with `foundation-security-policy.EmergencyOverride` extension to retention).

### 8. `DisposalReason.LegalErasureRequest` bypass semantics

`DisposalReason.LegalErasureRequest` is operator-attested per ADR 0068 §5.1. Does `DisposeAsync(reason: LegalErasureRequest)` bypass the jurisdiction-floor check? **XO recommendation:** **NO** for v1. The floor is an *informed default*; the deployer's legal determination that erasure is required for a specific record is a per-deployment manual operation (DBA-level), not a service-API path. The `LegalErasureRequest` enum value is RESERVED for future use; in v1, calling `DisposeAsync(reason: LegalErasureRequest)` on a floor-blocked document still returns `BlockedByJurisdictionFloor`. The `DisposalReason` is recorded on the `DocumentDisposal` row for documents where eligibility was already `Eligible` (without floor block) — it provides the forensic distinction between auto-sweep and operator-initiated disposals.

Document this in the package README + apps/docs page. If a deployment hits the case where `LegalErasureRequest` is needed against a floor-blocked document, that's an XO-level escalation: the deployer must (a) reduce the `TenantSecurityPolicy.JurisdictionPreset` floor (operator action; recorded in audit), (b) perform the disposal, then (c) restore the preset. The audit trail captures the gap.

### 9. SQLite-backed repositories vs. in-memory only

v1 ships in-memory repositories only (mirror of `blocks-docs-core` pattern). When the wave of SQLite + EFCore migrations for `blocks-docs-*` lands, this package's repositories follow. **No halt in this hand-off.** Note the deferral in PR 4's NOTICE / README.

### 10. Loro CRDT integration for append-only sub-collections

`LegalHold[]` placement + release rows + `DocumentDisposal[]` are natural fits for Loro op-log integration (§4 append-only conventions). v1 skips Loro entirely (per the docs-core hand-off pattern). **No halt.** Loro integration is a follow-on hand-off when `kernel-crdt` exposes the Loro op-mapping surface.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–4 merged to main** (sequentially; no parallelization within this hand-off).
2. **Apply + dispose round-trip:** acceptance test A3 passes.
3. **Legal-hold blocks disposal:** acceptance test A4 passes.
4. **Crypto-shred + key destruction:** acceptance test A5 passes.
5. **HIPAA jurisdiction-floor enforcement:** acceptance test A6 passes.
6. **Sweep job round-trip:** acceptance test A7 passes.
7. **Sweep job cancellation:** acceptance test A8 passes.
8. **`IBlobStore.DisposeAsync` idempotency:** acceptance test A9 passes.
9. **Composition with foundation-security-policy verified by inspection:** acceptance test A10 verifies (reviewer-attested).
10. **Tests pass:** ~39 new tests across the package + new IBlobStore.DisposeAsync contract tests in `foundation/tests/`.
11. **`apps/docs/blocks/docs-retention/overview.md` published** (ships in PR 4).
12. **`active-workstreams.md`** row for W#71 / blocks-docs-retention updated with `built` status + the 4 PR numbers.
13. **`coordination/inbox/{dev|cob}-status-2026-05-XXTHH-MMZ-w71-blocks-docs-retention-built.md`** beacon dropped.
14. **security-engineering council on PR 3 PASSED** (no Blocking findings open).
15. **.NET architect council on PR 2 PASSED** (no Blocking findings open).

When the PASS gate is met, the next docs-cluster hand-offs can proceed:

- `blocks-docs-core-cryptoshred-wireup-stage06-handoff.md` (micro-PR; ~30min; flips `RetentionPolicy.disposalAction = CryptoShred` from NotSupportedException to delegate).
- `blocks-docs-wiki-stage06-handoff.md` (already exists per repo — WikiSpace + WikiBook + WikiPage build can now wire retention).
- `blocks-docs-signing-stage06-handoff.md` (future — signed PDFs with long retention floors).
- `blocks-reports-compliance-attestation-stage06-handoff.md` (future workstream).

---

## Docs

**`apps/docs/blocks/docs-retention/overview.md`** — cluster docs page (ships in PR 4). Cite ADR 0088 §1; cite ADR 0068 §5 + §GC.1; cite Stage 02 schema design §3.1.7 + §6.4 + §7.3; cite W#37 P1 PR 3b.2 for composition pattern.

Structure already sketched above under PR 4.

**`packages/blocks-docs-retention/README.md`** — substrate-level README; cites Stage 02 design source + ADR 0088 + ADR 0068; reproduces ADR 0068 §GC.1 verbatim above the API surface description (mirror `foundation-security-policy/README.md` pattern per ADR 0068 §GC.1 binding).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `packages/blocks-docs-core/` (predecessor; assumed shipped per the docs-core hand-off — verify pre-build checklist step 1)
- `packages/blocks-docs-core/Models/RetentionPolicy.cs` (entity type consumed)
- `packages/blocks-docs-core/Models/Document.cs`
- `packages/blocks-docs/` (predecessor; assumed shipped per the docs hand-off — verify pre-build checklist step 2)
- `packages/blocks-docs/Models/StorageRef.cs` (discriminated union consumed)
- `packages/foundation-security-policy/Retention/` (assumed shipped per W#37 P1 PR 3b.2 — verify pre-build checklist step 3)
- `packages/foundation-security-policy/Retention/SecurityPolicyRetentionEnforcer.cs` (pattern source — composed)
- `packages/foundation-security-policy/Retention/RetentionVerdict.cs` (lifted)
- `packages/foundation-security-policy/Policy/ITenantSecurityPolicyLoader.cs` (consumed)
- `packages/foundation/Blobs/IBlobStore.cs` (additive interface extension)
- `packages/blocks-people-foundation/` (provides `PartyId` typing)
- `packages/foundation-events/` (provides `DomainEventEnvelope<TPayload>`)
- ADR 0088 §1 (Path II)
- ADR 0068 §1.3 (AuditRetentionPolicy + RetentionJurisdictionPreset), §5 (audit retention enforcement), §5.1 (right-to-erasure caveat), §5.2 (HIPAA floor logic), §GC.1 (general-counsel note)
- `icm/02_architecture/blocks-docs-schema-design.md` §3.1.7 (RetentionPolicy entity), §6.4 (Crypto-shred + retention), §7.3 (kernel-security delegation), §9 OQ #2 (crypto-shred granularity)
- `_shared/engineering/crdt-friendly-schema-conventions.md` §2, §4, §6, §7
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3
- `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md` (predecessor; §Halt 5 names the gate this hand-off resolves)
- `icm/_state/handoffs/blocks-docs-stage06-handoff.md` (predecessor; provides IBlobStore wiring this hand-off extends)
- `icm/_state/handoffs/w37-p1-pr3b-issuer-and-retention-enforcer-stage06-handoff.md` (pattern source; PR 3b.2 ships `SecurityPolicyRetentionEnforcer`)

**Introduced by this hand-off** (ship across PRs 1–4):

- New package: `packages/blocks-docs-retention/`
- New types: `LegalHoldId`, `LegalHold`, `LegalHoldReleaseId`, `LegalHoldRelease`, `DocumentDisposalId`, `DocumentDisposal`, `DisposalEligibility`, `DisposalReason`, `BlocksDocsRetentionOptions`, `ApplyPolicyResult` + `ApplyPolicyError`, `PlaceLegalHoldResult` + `PlaceLegalHoldError`, `ReleaseLegalHoldResult` + `ReleaseLegalHoldError`, `DisposalEligibilityResult`, `DisposeResult` + `DisposeError`, `DocumentRetentionFloorOptions`, `DisposalActionOutcome`, `ShredOutcome`, `SweepJobOptions`, `SweepJobResult`, event records (`DocumentRetentionAppliedEvent`, `LegalHoldPlacedEvent`, `LegalHoldReleasedEvent`, `DocumentDisposedEvent`)
- New services: `ILegalHoldRepository` + `InMemoryLegalHoldRepository`, `IDocumentDisposalRepository` + `InMemoryDocumentDisposalRepository`, `IDocumentRetentionService` + `DocumentRetentionService`, `IDocumentRetentionFloorEnforcer` + `DocumentRetentionFloorEnforcer`, `IDisposalActionStrategy` + `ArchiveDisposalActionStrategy` + `SoftDeleteDisposalActionStrategy` + `CryptoShredDisposalActionStrategy`, `IKeyShredder` + `InMemoryKeyShredder`, `BlobStoreDisposalAdapter`, `IDocumentRetentionSweepJob` + `DocumentRetentionSweepJob`
- Additive to `Sunfish.Foundation.Blobs.IBlobStore`: new method `DisposeAsync(Cid, DisposalReason, CancellationToken) → ValueTask<DisposalOutcome>`; new enum `DisposalOutcome`; new `DisposalReason` enum in `foundation/Blobs/` namespace (distinct from this package's domain-level `DisposalReason`; coordinate naming at PR 3 review)
- Docs: `apps/docs/blocks/docs-retention/overview.md`
- Attribution: `packages/blocks-docs-retention/NOTICE.md`

**Self-audit reminder** (per ADR 0028-A10): each cited symbol verified by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check NEGATIVE existence too — verify NO `UnshredAsync` / `RestoreKeyAsync` method exists on `IKeyShredder` (the irreversibility is a structural invariant, not just a documentation claim). Verify NO `IRetentionEnforcer` is redefined in this package (composition discipline).

---

## Cohort discipline

This hand-off is the **third docs-cluster hand-off under ADR 0088 Path II** (after `blocks-docs` attachment substrate and `blocks-docs-core` document substrate) and the **first compliance-attestation surface** in any cluster. The COB/dev self-audit pattern applied to W#34 / W#35 / W#36 substrate hand-offs + W#37 P1 PR 3b.2 + the ledger hand-off applies here verbatim:

- **Two-overload constructor (audit-disabled / audit-enabled both-or-neither) pattern** for the DI extension. Required here — `Docs.DocumentDisposed` is audit-relevant.
- **`AddSunfishDocsRetention()` naming for the DI extension** — matches the cluster convention.
- **`apps/docs/blocks/{cluster}/overview.md` page convention** — applied in PR 4.
- **README.md at the package root** referencing Stage 02 design + ADR 0088 + ADR 0068 — ship in PR 1.
- **`ConcurrentDictionary` dedup for any cache** — applied in in-memory repositories + `InMemoryKeyShredder`.
- **Strong-typed Id records** (ULID-backed) — applied for `LegalHoldId`, `LegalHoldReleaseId`, `DocumentDisposalId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — applied for `IKeyShredder` (kernel-security canonical impl in follow-on). Local stub; relocates when canonical home lands; DI swap with no public surface change.
- **§GC.1 XML `<remarks>` blocks** on every NEW public type in PRs 1–4 — ADR 0068 binding.

---

## Beacon protocol

If the implementer hits a halt-condition or has a design question:

- File `dev-question-2026-05-XXTHH-MMZ-w71-docs-retention-{slug}.md` (or `cob-question-*` if cob owns) in
  `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for W#71.
- `ScheduleWakeup 1800s`.

If the implementer completes PR 4 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `{dev|cob}-status-2026-05-XXTHH-MMZ-w71-blocks-docs-retention-built.md` to inbox.
- Continue with the next hand-off in the docs cluster queue (likely the micro-PR flipping `blocks-docs-core` CryptoShred NotSupportedException to delegate, then `blocks-docs-wiki` if queued).

---

## Cross-references

- Spec source: `icm/02_architecture/blocks-docs-schema-design.md` §3.1.7, §6.4, §7.3, §9 OQ #2.
- CRDT conventions: `_shared/engineering/crdt-friendly-schema-conventions.md` §2, §4, §6, §7.
- Event bus: `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3.
- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- ADR 0068: `docs/adrs/0068-tenant-security-policy.md` §1.3, §5 (5.1 + 5.2), §GC.1.
- Predecessor hand-off (docs substrate): `icm/_state/handoffs/blocks-docs-stage06-handoff.md`.
- Predecessor hand-off (document core): `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md` — §Halt 5 names the explicit gate this hand-off resolves.
- Pattern source hand-off (audit-side enforcer): `icm/_state/handoffs/w37-p1-pr3b-issuer-and-retention-enforcer-stage06-handoff.md` — PR 3b.2 shipping `SecurityPolicyRetentionEnforcer` + `RetentionVerdict` + `IRetentionPolicyResolver`.
- Template hand-off (substrate-only shape; 6-PR financial cohort): `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md`.
- Cohort precedent (DI + apps/docs convention): `icm/_state/handoffs/blocks-financial-ledger-chart-and-journal-stage06-handoff.md`, `foundation-mission-space-stage06-handoff.md`, `foundation-versioning-stage06-handoff.md`.

---

**End of hand-off.**
