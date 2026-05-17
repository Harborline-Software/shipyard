# Hand-off — `blocks-docs-signing` SigningWorkflow + signing audit trail (Phase 2 document cluster — security-critical)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` — **gated on `blocks-docs-core` (Document substrate) + `blocks-docs-templates` (contract templates originate signed docs) + `blocks-people-foundation` (PartyId for signers) + `kernel-signatures` (already shipped; W#21 — crypto + revocation log)** — see Pre-build checklist
**Workstream:** W#73 — blocks-docs-signing (Phase 2 document cluster, e-signature workflow)
**Spec source:** [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) **§3.5 (read in full)** — `SigningWorkflow` + `SigningStep` + `SigningParty` + `SignatureRequest` + `Signature` + `SigningAuditLog`; plus §4 (relationships diagram), §5.3 (signing workflow pseudocode — prepare → finalize → audit), §6.5 (tamper-evidence at signing time), §7.3 (`kernel-security` + `kernel-signatures` contract), §9 (open questions Q3 + Q7)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md); [ADR 0054 electronic signature capture + document binding](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md); [ADR 0068 tenant security policy](../../../docs/adrs/0068-tenant-security-policy.md) (security-touching cluster)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~14–18h dev (7 PRs; ~55–70 tests + docs + attribution + tamper-chain verification battery)
**PR count:** 7 PRs
**Pre-merge council:**
  - **PR 1 (substrate scaffold + entities):** `.NET architect` **MANDATORY** — substrate that all signing flows depend on; entity invariants need architect sign-off before downstream services build on them.
  - **PR 2 (`ISigningWorkflowService` — start/advance/cancel/expire):** `security-engineering` **MANDATORY** — workflow state machine is the auth-context boundary; mis-modeled transitions allow signers to bypass sequence; council must verify the state machine before merge.
  - **PR 3 (`ISignatureService` — capture via kernel-signatures + audit chain):** `security-engineering` **MANDATORY** + `.NET architect` recommended — this is the **crypto-adjacent + hash-chain integrity + tamper-detection** PR; misuse of `kernel-signatures` envelope, broken hash-chain, or weak content-hash binding produces **legally-disputable signatures**. Halt and council before merge.
  - **PR 4 (parallel signing support + step assignment):** `security-engineering` spot-check — parallel orchestration introduces race windows on `SigningWorkflow.Status` transitions.
  - **PR 5 (`ExpirationHandler` background sweep):** standard self-audit; security spot-check recommended (auth tokens + magic-link expiry).
  - **PR 6 (ERPNext importer for legacy signed docs):** standard self-audit; .NET architect optional.
  - **PR 7 (DI umbrella + apps/docs + ledger flip):** standard self-audit.
  - **Council protocol:** per `feedback_pr_automerge_before_amendment_landed.md`, every council-mandatory PR opens as `--draft`; auto-merge is enabled ONLY after all amendments are pushed and all council subagents return APPROVED. Per `feedback_council_before_automerge.md`, wait for ALL agents (security-engineering + architect when both are required) before flipping to ready.
**Attribution required:**
  - **None for source-paste** — this cluster is clean-room from Documenso (GPLv3 + Enterprise) + OpenSign (AGPLv3) surface observation only; no code paste, no comment paste, no schema paste. Per `blocks-docs-schema-design.md` §8 discipline note: Documenso + OpenSign are NOT cited in source comments since no code is borrowed.
  - **kernel-signatures** is an internal Sunfish dependency (W#21 Phase 1 ship); no third-party attribution needed.
  - **RFC 3161 (timestamping) + RFC 5652 (CMS/PKCS#7) + RFC 7515 (JWS)** are IETF public standards — referenced only via delegation to `kernel-signatures`; this package never paraphrases or implements RFC text directly.

---

## Context

### Phase 2 document cluster position

Per ADR 0088 §1 + the sibling `blocks-docs-core-stage06-handoff.md`, the Phase 2 document-cluster decomposition is:

```
blocks-docs               (attachment substrate; IBlobStore; StorageRef)              ✓ shipped (6 PRs)
blocks-docs-core          (Document + DocumentVersion + Folder + Permissions + Retention)  ✓ shipped (3 PRs)
blocks-docs-wiki          (WikiSpace + WikiBook + WikiPage + Policy/Procedure + ack)  (sibling hand-off)
blocks-docs-templates     (ContractTemplate + fields + clauses + render + Instance)   (predecessor — must merge first)
blocks-docs-dam           (MarketingAsset + AssetTag + Collection + BrandKit)         (parallel — no signing dep)
blocks-docs-signing       ← THIS HAND-OFF
```

`blocks-docs-signing` is the **e-signature lifecycle** unit and the gate that unblocks:

- `blocks-property-leases` retrofit — `LeaseExecuted` event consumption depends on `SigningWorkflowCompleted` emission from this cluster (per `cross-cluster-event-bus-design.md` §3.4 `Docs.SigningWorkflowCompleted` → consumed by `work` + `Property.LeaseExecuted` per §3.6)
- `blocks-work-contracts` retrofit — `Contract.status = pending-signature → active` flip consumes `Docs.ContractFullySigned` (event from this cluster)
- `blocks-people-onboarding` — policy + employment-contract execution paths consume `Docs.DocumentSigned`
- The 4-LLC / Phase 2 leasing-pipeline acceptance demo (rent roll → tenant signs lease → executed lease) — needs an end-to-end signed-lease path

It is **not** the critical-path predecessor to `blocks-docs-dam` (DAM has no signing surface) or `blocks-docs-wiki` (wiki + policy acknowledgment uses `PolicyAcknowledgment.signatureId` as an optional FK that resolves at ack-time; this hand-off ships the FK target shape).

### What this hand-off ships

Per `blocks-docs-schema-design.md` §3.5 (read in full):

1. **`SigningWorkflow`** record entity — the orchestration root; `documentId` + `documentVersionId` (pinned at workflow creation for anti-tamper); `workflowKind ∈ {sequential, parallel}` (`hybrid` deferred per §9 Q7); `status` state machine; `expiresAt`; `finalSignedDocumentId`.
2. **`SigningStep`** record entity — per-field placement on the document; `stepKind ∈ {signature, initial, date, text-fill, checkbox, approval-only}`; PDF positioning fields; `assignedPartyId`; `completedValue`.
3. **`SigningParty`** record entity — `partyKind ∈ {employee, tenant, external}`; `partyOrder` (sequential); `roleLabel`; `status` (pending → invited → viewed → signed | declined); `authMethod` (`magic-link` | `sso` | `sms-otp` | `in-person` | `kba` stub per §9 Q3).
4. **`SignatureRequest`** record entity — outbound delivery bridge (`channel ∈ {email, sms, in-app, printed}`; `magicLinkTokenHash` (hashed, never raw); `magicLinkExpiresAt`; `reminderCount`).
5. **`Signature`** record entity — persisted signature artifact with `kernelSignatureId` FK to `kernel-signatures.SignatureEventId`; `documentVersionAtSign` + `contentHashAtSign` (tamper-detection); `signatureKind ∈ {drawn, typed, cryptographic, wet-imported}`; **no crypto material stored here — all in `kernel-signatures`** (discipline rule §3.5).
6. **`SigningAuditLog`** record entity — **append-only, hash-chained** per workflow; one row per workflow event; `prevEntryHashChain` builds tamper-evident chain; `contentHashAtEvent` proves document state at event time.
7. **`ISigningWorkflowService`** — `Start` / `Dispatch` / `AdvanceParty` / `Decline` / `Cancel` / `Expire` operations; sequential and parallel orchestration; idempotent on duplicate dispatch.
8. **`ISignatureService`** — `Capture` (delegates to `kernel-signatures.ISignatureCapture`); `VerifyChain` (audit log integrity); `VerifyTamperFree` (compares stored `contentHashAtSign` vs current document hash).
9. **`ISigningExpirationHandler`** — background sweep that transitions `sent | in-progress` workflows past `expiresAt` to `expired` + emits `SigningWorkflow.Expired` audit + `Docs.SigningWorkflowExpired` event.
10. **`IErpnextSigningWorkflowImporter`** — Pass-N integration with the ERPNext migration importer; imports legacy signed-document records (one terminal-state `SigningWorkflow` per imported signed PDF; no live workflow re-creation).
11. **Cross-cluster events** — `Docs.SigningWorkflowCompleted`, `Docs.DocumentSigned`, `Docs.ContractFullySigned`, plus locally `Docs.SigningWorkflowVoided`, `Docs.SigningWorkflowExpired`, `Docs.SigningPartyDeclined` per `cross-cluster-event-bus-design.md` §3.4.
12. **DI umbrella** — `AddBlocksDocsSigning(...)` registering all services + repositories Scoped per `pattern-005`.
13. **`apps/docs/blocks/docs-signing/overview.md`** — public-surface documentation per `pattern-006`.

### What this hand-off does NOT ship

- **NO DocuSign / HelloSign / Adobe Sign external-provider integration in v1.** That is a **separate compat-adapter workstream** (working name: `compat-docusign`, `compat-hellosign`) — treat each external e-signing vendor as its own ICM intake under the `compat-*` family. Rationale: each vendor's API surface + webhook flow + rate-limit handling is substantial; bundling into v1 multiplies surface area and security review burden. The `blocks-docs-signing` v1 ships a complete self-hosted signing path. External-provider adapters write into `SigningWorkflow` via the same `ISigningWorkflowService` contract — they replace the in-app capture leg only.
- **NO native PencilKit / iOS CryptoKit handwritten capture.** Per `kernel-signatures` README, native iOS capture is W#23 territory (iOS Field-Capture App). This hand-off consumes the kernel surface; it does not extend the kernel.
- **NO KBA (knowledge-based-authentication) IDV vendor wiring.** Per §9 Q3: `SigningParty.authMethod = 'kba'` lands as a **stub schema value** with a no-op verifier + TODO. Real KBA integration is post-MVP.
- **NO PDF merge / signed-output rendering pipeline.** `SigningWorkflow.finalSignedDocumentId` is populated by a delegation to `blocks-reports-*` PDF renderer (already shipped) — the merge call goes through `IFinalSignedDocumentMerger` interface; in v1 the implementation is a **passthrough stub** that creates a new `Document` of type `signed-pdf` whose body is the original PDF + an appended signature certificate page (1 page, text-only). True multi-signature embedding-into-PDF-form-fields is a follow-on (likely a `blocks-reports-signing-render` package).
- **NO biometric replay / forensic pen-stroke visualization.** `PenStrokeBlobRef` from `kernel-signatures` is referenced via `Signature.kernelSignatureId` (which transitively reaches the pen stroke); rendering the stroke for forensic review is a UI concern (`apps/anchor` route — out of scope).
- **NO `hybrid` workflow kind.** Per §9 Q7 — recommend dropping `'hybrid'` from v1; ship `sequential` + `parallel` only. The enum surface excludes `hybrid` until a real need emerges.
- **NO SQLite-side persistence.** Mirror the financial-AR / docs-core in-memory pattern; SQLite persistence is out of scope for v1 (the in-memory implementations ship; SQLite is a follow-on `blocks-docs-signing-persistence` workstream).
- **NO PolicyAcknowledgment workflow.** That ships with `blocks-docs-wiki`. This hand-off ships the `Signature` type that `PolicyAcknowledgment.signatureId` references; the consumer side stays in wiki.

### Security posture (binding)

`blocks-docs-signing` is the **most security-sensitive of the `blocks-docs-*` sub-packages**. Failures here produce legally-disputable signatures and contractual nullification risk. The following are non-negotiable:

1. **No crypto in this package.** Every cryptographic operation (key handling, signature byte production, certificate validation, RFC 3161 timestamp request, CMS/PKCS#7 envelope construction) routes through `kernel-signatures` + `kernel-security`. This package only stores foreign keys + display-grade metadata. Any PR that introduces a `using System.Security.Cryptography` import is a code-review red flag — kill the PR.
2. **Content-hash binding is the contract.** Every `SigningWorkflow` pins `documentVersionId` + a `contentHashAtSign` is captured per `Signature`. At verify time (`VerifyTamperFree`), the current hash MUST equal the stored hash; mismatch is a tamper event (audit + signal). The hash function is whatever `kernel-security.computeContentHash(bytes)` returns — this package never calls SHA-256 directly.
3. **Magic-link tokens never persist raw.** `SignatureRequest.magicLinkTokenHash` stores `sha256(rawToken)`; the raw token leaves the system EXACTLY ONCE — at notification dispatch — and is never logged. Per ADR 0068 §GC.1 secret-handling: raw tokens MUST NOT appear in any log line, exception message, or audit row.
4. **Audit log is append-only + hash-chained.** Per §3.5.6 — every `SigningAuditLog.prevEntryHashChain` is `sha256(serialize(prev_row))`; a missing/altered link is a chain break. `ISignatureService.VerifyChain(workflowId)` is the canonical verifier; called at every audit-relevant boundary (signer dispatch, view, sign, decline, completion).
5. **Tenant scope on every query.** Per `foundation-multitenancy` + `feedback_council_can_miss_spot_check_negative_existence.md` — every repository query takes `tenantId` either as an explicit parameter or via injected `ITenantContext`. No analyzer-bypass; no tenant-less query overload. Council MUST spot-check the negative case (no cross-tenant leak via misordered overloads).
6. **Decline + void are irreversible.** Once a `SigningParty.Status = declined` is set, the workflow transitions terminally to `declined`; no resurrection (per §3.5.1 state machine + Pattern B terminal-wins). Same for `voided`.
7. **Per-replica audit ordering is causal-stable.** Two replicas may emit audit events concurrently; the hash-chain is per-workflow + per-replica with a deterministic merge rule documented in `crdt-friendly-schema-conventions.md` §6 (posted-then-immutable) — audit rows are immutable post-write and the chain is rebuilt at merge time using CRDT-Lamport ordering. PR 3 must include the merge-rule test (two replicas each append 3 audit rows; merged ordering is deterministic + chain-verifiable).
8. **No exception-message data leakage.** Validation errors include error CODE + field NAME only, never raw signature bytes, magic-link tokens, content hashes (since those are tracking-token candidates), or signer email addresses (in messages bound for clients other than the signer).

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `SigningWorkflowId`, `SigningStepId`, `SigningPartyId`, `SignatureRequestId`, `SignatureId`, `SigningAuditLogId` — strongly typed; ULID storage |
| §1 / §8 Monotonic numbers — per-replica-suffix scheme | NOT applied — signing workflows are not user-visible numbered; `SigningWorkflow.Id` is opaque ULID; no "workflow number" required. If a future "Envelope ID" surface is wanted, follow the AR per-replica-suffix pattern |
| §2 Soft-delete tombstones | `SigningWorkflow.voidedAt` + `voidReason` per §3.5.1; **no hard delete** of any signing entity (legal retention requirement — audit must survive); workflow archival uses `Status = voided` with `voidReason` populated |
| §3 version + revisionVector | `SigningWorkflow.Version` int + `RevisionVector` Dictionary<string,long> — Loro-managed; application reads only. `SigningParty.Version` similarly carries the per-party revision vector for status transitions |
| §4 Append-only sub-collections | `SigningAuditLog` is strict append-only; `SigningStep` lines on the workflow are append-only after `status != Draft`; `Signature` rows are append-only (one per stepCompletion) |
| §5 Stable string codes | `SigningWorkflowStatus`, `SigningPartyStatus`, `SigningStepKind`, `SignatureKind`, `SigningAuditEvent` enums all surface as string codes over the wire (`"sent"`, `"signed"`, etc.); persistent storage as text |
| §6 Posted-then-immutable | Once `SigningWorkflow.status != Draft`, the workflow header is immutable; status transitions allowed but field mutations are not. Once a `Signature` is persisted, it is immutable (revocation lives in `kernel-signatures.ISignatureRevocationLog`, NOT here). Audit rows are append-only and immutable post-write |
| §7 State-machine-under-CRDT pattern B — terminal wins | Per §7 cluster table: `SigningWorkflow` uses **Pattern B (terminal-wins)**: `voided` / `expired` / `declined` / `completed` > `in-progress` > `sent` > `draft`. Implemented in `SigningWorkflowStatusResolver` registered with `kernel-crdt`. **Designated-authority IS used** for the `completed` transition: only the replica holding the `finalSignedDocumentId` (the one that ran the merge) may set `completed`. Other replicas wait for the completion-bearing op to arrive; concurrent terminal-state divergence (one replica says `voided`, another says `completed`) resolves to `voided` per terminal-wins. **Test acceptance:** simulate 3-replica race; document the resolver's output |
| §10 Two-tier validation | Tier-1 write-time on every entity persist (status transition allowed; sensitivity → permission check; etc.); Tier-2 post-merge reconciler verifies audit-chain integrity, kernel-signature FK existence, and step-completion vs party-signed consistency. Tier-2 reconciler emits `SigningWorkflow.AuditChainBroken` event if any post-merge violation surfaces |

The combination ensures: (a) two offline replicas can each prepare independent workflows and converge cleanly; (b) party status transitions resolve terminally; (c) audit chain integrity is post-merge-verifiable; (d) crypto material is never duplicated across replicas (it lives in `kernel-signatures` whose own sync semantics are independent of this package).

### Open question Q10 (financial design) — Loro append-only constraint

Per the sibling financial-AR hand-off, Q10 remains **open** at this hand-off's cutoff. This hand-off DOES touch Loro-relevant append-only constraints (the audit log is the strictest example). The PRs do not write Loro op-log integration directly; they implement immutability + append-only at the service layer via Tier-1 validation. If a Loro question arises during PR 3 (audit chain) or PR 4 (parallel state-machine convergence), file a `cob-question-*` beacon.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify `blocks-docs-core` is built (substrate).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/Document.cs 2>&1
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-core/Models/DocumentVersion.cs 2>&1
   gh pr list --state merged --search "blocks-docs-core in:title,body" --limit 10
   ```
   Expected: package exists; `Document` + `DocumentVersion` types are present. If not, **STOP** — the substrate hand-off must complete first. Drop a `cob-question-*` beacon.

2. **Verify `blocks-docs-templates` is built (predecessor).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-templates/ 2>&1
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-docs-templates/Models/ContractInstance.cs 2>&1
   gh pr list --state merged --search "blocks-docs-templates in:title,body" --limit 10
   ```
   Expected: package exists; `ContractInstance` is present. If absent, **proceed anyway** — `SigningWorkflow.templateId` is nullable (`null` for non-template-driven signings, which is the v1 majority case); the FK can be `string?` (opaque) until the templates package lands. Note the deferral in the PR description.

3. **Verify `kernel-signatures` is shipped (W#21 — should be on main).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-signatures/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-signatures/Models/SignatureEvent.cs
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-signatures/Services/ISignatureCapture.cs
   grep "public readonly record struct SignatureEventId" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-signatures/Models/Identifiers.cs
   ```
   Expected: all three present. The `SignatureEventId` is `record struct (Guid Value)` per `Identifiers.cs` line 4. The `ISignatureCapture.CaptureAsync(SignatureCaptureRequest, ct)` returns `Task<SignatureEvent>`. The `SignatureCaptureRequest` requires `TenantId Tenant`, `ActorId Signer`, `ConsentRecordId Consent`, `ContentHash DocumentHash`, `IReadOnlyList<TaxonomyClassification> Scope`, `SignatureEnvelope Envelope`, `CaptureQuality Quality`. **PR 3 wires this verbatim** — do NOT reshape the kernel surface. If any of the above is absent, **STOP and file `cob-question-*`**.

4. **Verify `blocks-people-foundation` is on main (PartyId source).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/ 2>&1
   grep "readonly record struct PartyId" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/Models/*.cs 2>&1
   ```
   Expected: package exists; `PartyId` type present. If absent, ship a local `SignerPartyId` strong-id type in this package (mirroring `blocks-leases/Models/PartyId.cs` pattern), and define `SigningParty.principalId` as `SignerPartyId?`. When `blocks-people-foundation` lands, the contract relocates; one `using` directive update.

5. **Verify `foundation-events` + cross-cluster event bus.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/ 2>&1
   grep -rln "ICrossClusterEventBus\|IEventPublisher" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/ 2>/dev/null | head -3
   ```
   Expected: foundation-events exists. If the cross-cluster event bus package isn't decided yet (per AR hand-off §10 Q7), ship local event types in `blocks-docs-signing/Events/` + a local `ISigningEventPublisher` interface with `InMemorySigningEventPublisher`. When the canonical bus lands, the contract relocates.

6. **Verify `foundation-multitenancy` + `ITenantContext`.**
   ```bash
   grep -rln "ITenantContext\|TenantId" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-multitenancy/ 2>/dev/null | head -5
   ```
   Expected: present. Tenant scoping is non-negotiable for signing entities.

7. **Verify `kernel-audit` is on main (for SignatureCaptured + related audit emission).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-audit/
   grep "SignatureCaptured\|SignatureRevoked" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-audit/AuditEventType.cs
   ```
   Expected: `kernel-audit` present; `AuditEventType` carries `SignatureCaptured` + `SignatureRevoked` + `SignatureValidityProjected` + `ConsentRecorded` constants per `kernel-signatures/README.md`. `blocks-docs-signing` does NOT emit those (`kernel-signatures` owns them); the `SigningAuditLog` in this package is a **separate audit trail** scoped to workflow lifecycle, NOT crypto events.

8. **Confirm ADR 0054 + ADR 0068 + ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0054-electronic-signature-capture-and-document-binding.md
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0068-tenant-security-policy.md
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: ADR 0054 = Accepted (W#21 shipped against it). ADR 0068 = Proposed (Tenant Security Policy; this hand-off respects §GC.1 secret-handling — see Security posture §3 above). ADR 0088 = Proposed (CO ratified 2026-05-16). Hand-off is `ready-to-build` regardless — CO directive operative.

9. **Confirm no parallel-session PRs touch `blocks-docs-*` or `kernel-signatures`.**
   ```bash
   gh pr list --state open --search "blocks-docs in:title,body"
   gh pr list --state open --search "kernel-signatures in:title,body"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*`.

10. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

11. **Read the Stage 02 design source sections.** `blocks-docs-schema-design.md` §3.5 in full (all six sub-entities + tamper-evidence narrative); §4 (relationships diagram — focus on the signing-cluster subgraph); §5.3 (signing pseudocode — `startSigningWorkflow` + `dispatchWorkflow` + `signByParty` + `audit` + `verifyAuditChain` is the canonical reference); §6.5 (tamper-evidence semantics); §7.3 (kernel-security + kernel-signatures contract); §9 Q3 + Q7 (KBA stub + hybrid drop). Read `kernel-signatures/README.md` end-to-end. Read `ADR 0054` §A1 (canonicalization) + §A2 (algorithm agility) + §A7 (taxonomy scope). Read `crdt-friendly-schema-conventions.md` §6 + §7.

12. **Run the package-name availability audit.** Per `feedback_audit_existing_blocks_before_handoff.md`:
    ```bash
    ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-docs-signing|^blocks-signing"
    ```
    Expected: empty. If `blocks-docs-signing/` or `blocks-signing/` already exists, **STOP and file `cob-question-*`** — name collision requires XO ruling.

---

## Per-PR deliverables

This hand-off splits into **7 PRs** by responsibility + security boundary. The split deliberately concentrates security-critical surface in PRs 2 + 3 so council reviews are bounded.

- PR 1: Package scaffold + entity types (`SigningWorkflow` + `SigningStep` + `SigningParty` + `SignatureRequest` + `SignatureLocal` placeholder + `SigningAuditLog`) + status enums + transition validation helper + repository contracts + DI seed (`.NET architect` council MANDATORY)
- PR 2: `ISigningWorkflowService` — `Start` / `Dispatch` / `AdvanceParty` / `Decline` / `Cancel` / `Expire` (state machine surface; **security-engineering council MANDATORY**)
- PR 3: `ISignatureService` — capture via `kernel-signatures.ISignatureCapture` + audit-chain construction + tamper verification + emission of `Docs.DocumentSigned` events (**security-engineering council MANDATORY**)
- PR 4: Parallel-signing support — step assignment per party; concurrent party-state advancement; convergence test battery (security-engineering spot-check)
- PR 5: `ISigningExpirationHandler` background sweep + magic-link token expiry handling
- PR 6: `IErpnextSigningWorkflowImporter` — legacy signed-doc migration; one terminal-state workflow per imported PDF
- PR 7: DI umbrella `AddBlocksDocsSigning()` + `apps/docs/blocks/docs-signing/overview.md` + ledger flip W#73 → built

PRs 1 + 2 + 3 are sequential. PR 4 can parallelize with PR 3 once PR 2 is in. PRs 5 + 6 + 7 sequence last (each depends on PRs 1–4). Total ~14–18h.

---

### PR 1 — Package scaffold + 6 entity types + status enums + transition helper + repository contracts + DI seed

**Estimated effort:** ~3–4h
**Scope:** new package `blocks-docs-signing`; entity records; status enums; status-transition validation helper; repository contracts + in-memory implementations; DI seed; **NO services beyond repositories** (PR 2 ships workflow service; PR 3 ships signature service)
**Commit subject:** `feat(blocks-docs-signing): scaffold signing package with workflow + step + party + signature + audit entities per Stage 02 §3.5`
**Branch:** `cob/blocks-docs-signing-scaffold`
**Pre-merge council:** **.NET architect MANDATORY** — substrate that PRs 2 + 3 + 4 all build on; entity invariants need architect sign-off before downstream services lock the shape. Council scope:
  - Entity field types + nullability decisions (esp. `documentVersionId` pin-at-create vs pin-at-dispatch)
  - Status enum value-sets + transition tables (sequence + terminal-state coverage)
  - Repository contract shape (consistency with `blocks-docs-core.IDocumentRepository` + financial-cluster patterns)
  - Tenant-scoping signature on every repository method (`tenantId` either explicit param or implicit `ITenantContext`)
  - `SigningAuditLog.prevEntryHashChain` field declaration + serialization-stability guarantee (the hash must be stable across machine architectures + .NET versions; council must confirm the serialization choice — `System.Text.Json` with `JsonSerializerOptions { WriteIndented = false }` + a stable property order — is sound)

#### Package skeleton

```
packages/blocks-docs-signing/
├── README.md
├── NOTICE.md                                            (no third-party attribution; clean-room; references ADR 0054 + 0088)
├── Sunfish.Blocks.DocsSigning.csproj
├── Models/
│   ├── SigningWorkflowId.cs
│   ├── SigningStepId.cs
│   ├── SigningPartyId.cs
│   ├── SignatureRequestId.cs
│   ├── LocalSignatureId.cs                              (distinct from kernel-signatures.SignatureEventId)
│   ├── SigningAuditLogId.cs
│   ├── SigningWorkflow.cs
│   ├── SigningStep.cs
│   ├── SigningParty.cs
│   ├── SignatureRequest.cs
│   ├── LocalSignature.cs                                (the package-local Signature; FK to kernel via KernelSignatureEventId)
│   ├── SigningAuditLog.cs
│   ├── SigningWorkflowStatus.cs                         (enum)
│   ├── SigningPartyStatus.cs                            (enum)
│   ├── SigningStepKind.cs                               (enum)
│   ├── SignatureKind.cs                                 (enum)
│   ├── SignatureChannel.cs                              (enum)
│   ├── PartyAuthMethod.cs                               (enum)
│   ├── SignatureRequestDeliveryStatus.cs                (enum)
│   ├── SigningAuditEvent.cs                             (enum)
│   └── BlocksDocsSigningOptions.cs                      (WorkflowDefaultExpiryDays + MagicLinkExpiryDays + MaxReminderCount)
├── Services/
│   ├── ISigningWorkflowRepository.cs
│   ├── InMemorySigningWorkflowRepository.cs
│   ├── ISigningStepRepository.cs
│   ├── InMemorySigningStepRepository.cs
│   ├── ISigningPartyRepository.cs
│   ├── InMemorySigningPartyRepository.cs
│   ├── ISignatureRequestRepository.cs
│   ├── InMemorySignatureRequestRepository.cs
│   ├── ILocalSignatureRepository.cs
│   ├── InMemoryLocalSignatureRepository.cs
│   ├── ISigningAuditLogRepository.cs
│   └── InMemorySigningAuditLogRepository.cs
├── StateMachine/
│   ├── SigningWorkflowStatusTransitions.cs              (allowed-set table + EnsureAllowed helper)
│   └── SigningPartyStatusTransitions.cs                 (same shape for party-side)
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs                   (PR 1: AddSunfishDocsSigningSeed — registers repos only)
└── tests/
    └── Sunfish.Blocks.DocsSigning.Tests/
        ├── Sunfish.Blocks.DocsSigning.Tests.csproj
        ├── SigningWorkflowRecordTests.cs
        ├── SigningStepRecordTests.cs
        ├── SigningPartyRecordTests.cs
        ├── SignatureRequestRecordTests.cs
        ├── LocalSignatureRecordTests.cs
        ├── SigningAuditLogRecordTests.cs
        ├── SigningWorkflowStatusTransitionTests.cs
        ├── SigningPartyStatusTransitionTests.cs
        └── RepositoryRoundTripTests.cs
```

#### csproj dependencies

```xml
<ItemGroup>
  <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  <ProjectReference Include="..\foundation-events\Sunfish.Foundation.Events.csproj" />
  <ProjectReference Include="..\foundation-multitenancy\Sunfish.Foundation.Multitenancy.csproj" />
  <ProjectReference Include="..\blocks-docs-core\Sunfish.Blocks.DocsCore.csproj" />
  <ProjectReference Include="..\blocks-people-foundation\Sunfish.Blocks.People.Foundation.csproj" />
  <ProjectReference Include="..\kernel-signatures\Sunfish.Kernel.Signatures.csproj" />
  <ProjectReference Include="..\kernel-audit\Sunfish.Kernel.Audit.csproj" />
</ItemGroup>
```

If any of `blocks-people-foundation`, `kernel-audit`, or `foundation-multitenancy` are absent from main per the Pre-build checklist, drop the project reference and stub the dependent type locally (note in the PR body).

#### Strongly-typed identifiers

Mirror the financial-AR pattern (positional record struct with `Value` property and `ToString()` override). Each id wraps a ULID string.

```
public readonly record struct SigningWorkflowId(string Value) {
    public static SigningWorkflowId New() => new(Ulid.NewUlid().ToString());
    public override string ToString() => Value;
}
```
(Similar for `SigningStepId`, `SigningPartyId`, `SignatureRequestId`, `LocalSignatureId`, `SigningAuditLogId`.)

#### Enums (PR 1 ships all 8)

**`SigningWorkflowStatus`** per §3.5.1:
```
Draft, Sent, InProgress, Completed, Declined, Expired, Voided
```
Per §9 Q7: `Hybrid` is intentionally **omitted** from the enum to prevent silent v1 mis-use. If a future intake reintroduces it, add at that point.

**`SigningPartyStatus`** per §3.5.3:
```
Pending, Invited, Viewed, Signed, Declined
```

**`SigningStepKind`** per §3.5.2:
```
Signature, Initial, Date, TextFill, Checkbox, ApprovalOnly
```

**`SignatureKind`** per §3.5.5:
```
Drawn, Typed, Cryptographic, WetImported
```

**`SignatureChannel`** per §3.5.4:
```
Email, Sms, InApp, Printed
```

**`PartyAuthMethod`** per §3.5.3:
```
MagicLink, Sso, SmsOtp, InPerson, Kba
```
`Kba` is the stub value per §9 Q3; PR 2 wires a `NoOpKbaVerifier` that returns `KbaVerificationResult.NotConfigured` until a real IDV vendor is integrated.

**`SignatureRequestDeliveryStatus`** per §3.5.4:
```
Queued, Sent, Delivered, Failed, Bounced
```

**`SigningAuditEvent`** per §3.5.6 (closed enum — adding new values requires schema-epoch coordination per `crdt-friendly-schema-conventions.md` §5):
```
WorkflowCreated, WorkflowSent, WorkflowCompleted, WorkflowVoided, WorkflowExpired,
PartyInvited, PartyViewed, PartySigned, PartyDeclined, PartyReminded,
DocumentDownloaded, DocumentTamperedDetected,
AuthMagicLinkUsed, AuthFailed
```

#### Entity records (positional `sealed record`)

For each of the six entities below, fields follow the Stage 02 §3.5 spec verbatim. The schema-design field names map to .NET PascalCase. Audit fields (`CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy`, `Version`, `RevisionVector`, `TenantId`) are on every entity per CRDT conventions §3 + foundation-multitenancy convention; not re-listed below to keep this hand-off scannable.

**`SigningWorkflow`** (Stage 02 §3.5.1):
- `Id` (SigningWorkflowId)
- `TenantId`
- `DocumentId` (Sunfish.Blocks.DocsCore.DocumentId)
- `DocumentVersionId` (Sunfish.Blocks.DocsCore.DocumentVersionId) — **pinned at workflow creation; anti-tamper**
- `WorkflowKind` (string enum `"sequential"` | `"parallel"`)
- `Status` (SigningWorkflowStatus)
- `InitiatedBy` (PartyId)
- `InitiatedAtUtc` (Instant)
- `ExpiresAtUtc` (Instant?)
- `CompletedAtUtc` (Instant?)
- `VoidedAtUtc` (Instant?)
- `VoidReason` (string?)
- `FinalSignedDocumentId` (DocumentId?)
- `TemplateId` (string? — opaque until `blocks-docs-templates` lands; then strongly-typed)

Invariants enforced at construction time:
1. If `Status == Completed`, `CompletedAtUtc` MUST be non-null AND `FinalSignedDocumentId` MUST be non-null.
2. If `Status == Voided`, `VoidedAtUtc` MUST be non-null AND `VoidReason` MUST be non-null + non-empty.
3. If `Status == Expired`, `ExpiresAtUtc` MUST be non-null AND `ExpiresAtUtc < now` at the time the status flipped.
4. `WorkflowKind` MUST be `"sequential"` or `"parallel"` (constructor throws on `"hybrid"` or any other value).

**`SigningStep`** (Stage 02 §3.5.2):
- `Id`, `TenantId`, `WorkflowId`
- `StepOrder` (int — 1..n; monotonic per workflow)
- `StepKind` (SigningStepKind)
- `PageNumber` (int? — for PDF placement; 1-indexed)
- `PositionX`, `PositionY` (decimal? — 0..1 normalized)
- `WidthFraction`, `HeightFraction` (decimal? — 0..1 normalized)
- `Required` (bool — `true` means workflow cannot complete without this step's completion)
- `AssignedPartyId` (SigningPartyId)
- `CompletedAtUtc` (Instant?)
- `CompletedValue` (string? — for text/initial; for signature kind, holds `LocalSignatureId.Value`)

Invariants:
1. `StepOrder >= 1`.
2. If any positional field is set, **all four** must be set (atomic placement).
3. If `StepKind == ApprovalOnly`, `Required == true` always.

**`SigningParty`** (Stage 02 §3.5.3):
- `Id`, `TenantId`, `WorkflowId`
- `PartyOrder` (int — for sequential workflows; first signer = 1)
- `RoleLabel` (string — 1..100)
- `PartyKind` (string enum `"employee"` | `"tenant"` | `"external"`)
- `PrincipalId` (PartyId? — non-null when `PartyKind == employee | tenant`)
- `ExternalName` (string? — required when `PartyKind == external`)
- `ExternalEmail` (string? — required when `PartyKind == external`)
- `Status` (SigningPartyStatus)
- `InvitedAtUtc`, `ViewedAtUtc`, `SignedAtUtc`, `DeclinedAtUtc` (Instant?)
- `DeclineReason` (string?)
- `AuthMethod` (PartyAuthMethod)

Invariants:
1. Exactly one of `PrincipalId` OR `(ExternalName + ExternalEmail)` is set per `PartyKind`.
2. State-transition timestamps: when `Status == X`, `XAtUtc` is non-null.
3. `RoleLabel` is non-empty.
4. `ExternalEmail` when present passes basic email regex (full RFC 5322 not required; basic shape sufficient).

**`SignatureRequest`** (Stage 02 §3.5.4):
- `Id`, `TenantId`, `WorkflowId`, `PartyId`
- `Channel` (SignatureChannel)
- `Recipient` (string — email or phone; depends on `Channel`)
- `SentAtUtc` (Instant?)
- `DeliveryStatus` (SignatureRequestDeliveryStatus)
- `MagicLinkTokenHash` (string? — **sha256 of raw token; NEVER raw token**)
- `MagicLinkExpiresAtUtc` (Instant?)
- `ReminderCount` (int — default 0)
- `LastReminderAtUtc` (Instant?)

Invariants:
1. `MagicLinkTokenHash` length MUST equal 64 chars (sha256 hex) OR be null. Constructor enforces.
2. `ReminderCount >= 0` AND `<= BlocksDocsSigningOptions.MaxReminderCount` (default 5).
3. `Recipient` non-empty.
4. When `Channel == Email`, `Recipient` passes basic email regex.

**`LocalSignature`** (Stage 02 §3.5.5):

Named `LocalSignature` (not `Signature`) to avoid collision with `kernel-signatures.SignatureEvent` semantic naming and to make the FK relationship explicit at the type level.

- `Id` (LocalSignatureId)
- `TenantId`, `WorkflowId`, `StepId`, `PartyId`
- `SignatureKind` (SignatureKind)
- `ImageRef` (string? — opaque blob reference for drawn/typed signature rendering; routes to `blocks-docs.IBlobStore` at consume time)
- `KernelSignatureEventId` (Guid? — FK to `kernel-signatures.SignatureEventId.Value`; non-null when `SignatureKind == Cryptographic`)
- `SignedAtUtc` (Instant)
- `SignedFromIp` (string?)
- `SignedUserAgent` (string?)
- `SignedGeolocation` (string? — when consented; format `"lat,lon"`)
- `DocumentVersionAtSignId` (DocumentVersionId — pinned at sign time)
- `ContentHashAtSign` (string — sha256 hex of the document at sign time; computed via `kernel-security.computeContentHash`)

Invariants:
1. When `SignatureKind == Cryptographic`, `KernelSignatureEventId` MUST be non-null.
2. When `SignatureKind ∈ {Drawn, Typed}`, `ImageRef` MUST be non-null.
3. When `SignatureKind == WetImported`, both `ImageRef` AND `KernelSignatureEventId` MAY be null (it's an attestation-only record for legacy migration).
4. `ContentHashAtSign` is 64-char sha256 hex.

**Discipline rule (binding for all PRs):** This entity **never** stores crypto material — no signature bytes, no certificates, no private keys, no CMS envelopes. All crypto material lives in `kernel-signatures.SignatureEvent` (which itself ferries an opaque `SignatureEnvelope`). `LocalSignature` only stores foreign keys + display-grade metadata. Any field addition that holds crypto bytes is a PR-block.

**`SigningAuditLog`** (Stage 02 §3.5.6) — **append-only, hash-chained**:
- `Id` (SigningAuditLogId)
- `TenantId`, `WorkflowId`
- `PartyId` (SigningPartyId? — null when event is workflow-level not party-level)
- `EventKind` (SigningAuditEvent)
- `OccurredAtUtc` (Instant)
- `ActorId` (PartyId? — employee acting on behalf; e.g. dispatching a workflow on a tenant's behalf)
- `IpAddress` (string?)
- `UserAgent` (string?)
- `Payload` (Dictionary<string, string> — event-specific; **string-only values to keep hash deterministic across .NET versions**)
- `ContentHashAtEvent` (string — sha256 hex of the document at event time; tamper-detection)
- `PrevEntryHashChain` (string — sha256 hex of the serialized prev row; `"0" * 64` for the first row in a workflow's chain)

Invariants:
1. Once persisted, **no field is mutable** (the repository's `UpdateAsync` overload throws `NotSupportedException`). Even soft-delete is forbidden; append-only is strict.
2. `Payload` values are strings; if a caller wants to record structured data, it serializes-to-JSON first. (Council scope: confirm string-only is sufficient for forensic replay.)
3. `PrevEntryHashChain` length == 64.
4. `ContentHashAtEvent` length == 64.

#### Repository contracts

Each repository has the standard methods: `GetByIdAsync`, `QueryAsync` (filtered), `UpsertAsync` (where mutability is allowed — workflows + parties + steps yes; signatures + audit-log no), and a tenant-scoped filter on every read.

**`ISigningWorkflowRepository`** read methods of note:
- `GetByDocumentIdAsync(DocumentId, CancellationToken)` — returns the active (`Sent | InProgress`) workflow for the document, or null.
- `QueryByStatusAsync(SigningWorkflowStatus, CancellationToken)` — used by the expiration sweep (PR 5).
- `QueryExpiredCandidatesAsync(Instant asOf, CancellationToken)` — returns workflows where `Status ∈ {Sent, InProgress}` AND `ExpiresAtUtc != null` AND `ExpiresAtUtc < asOf`.

**`ISigningAuditLogRepository`** **APPEND-ONLY**:
- `AppendAsync(SigningAuditLog entry, CancellationToken)` — the only mutation; throws if `WorkflowId` doesn't match the latest entry's chain.
- `GetLastAsync(SigningWorkflowId, CancellationToken)` — returns the most-recent entry (for `PrevEntryHashChain` construction).
- `GetAllForWorkflowAsync(SigningWorkflowId, CancellationToken)` — ordered by `OccurredAtUtc` ascending (for chain verification).
- **No `UpdateAsync`, no `DeleteAsync`** — the interface deliberately omits them. The InMemory impl throws on attempts.

#### Status-transition validation helpers

**`SigningWorkflowStatusTransitions`** (static class):
```
Allowed = {
    Draft         -> { Sent },
    Sent          -> { InProgress, Completed, Declined, Expired, Voided },
    InProgress    -> { Completed, Declined, Expired, Voided },
    Completed     -> {},   // terminal
    Declined      -> {},   // terminal
    Expired       -> {},   // terminal
    Voided        -> {},   // terminal
}
```

Helpers: `IsAllowed(from, to)`, `EnsureAllowed(from, to)` (throws `InvalidOperationException` with descriptive message), `IsTerminal(SigningWorkflowStatus)`.

**`SigningPartyStatusTransitions`**:
```
Allowed = {
    Pending  -> { Invited },
    Invited  -> { Viewed, Signed, Declined },
    Viewed   -> { Signed, Declined },
    Signed   -> {},        // terminal per-party
    Declined -> {},        // terminal per-party
}
```

#### DI seed

**`DependencyInjection/ServiceCollectionExtensions.cs`** (PR 1 ships the seed; PRs 2–7 extend):

```
public static class ServiceCollectionExtensions {
    public static IServiceCollection AddSunfishDocsSigningSeed(
        this IServiceCollection services,
        Action<BlocksDocsSigningOptions>? configure = null)
    {
        var options = new BlocksDocsSigningOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddScoped<ISigningWorkflowRepository, InMemorySigningWorkflowRepository>();
        services.AddScoped<ISigningStepRepository, InMemorySigningStepRepository>();
        services.AddScoped<ISigningPartyRepository, InMemorySigningPartyRepository>();
        services.AddScoped<ISignatureRequestRepository, InMemorySignatureRequestRepository>();
        services.AddScoped<ILocalSignatureRepository, InMemoryLocalSignatureRepository>();
        services.AddScoped<ISigningAuditLogRepository, InMemorySigningAuditLogRepository>();

        return services;
    }
}
```

Per the W#1 PR #688 + #692 ruling (`feedback_council_can_miss_spot_check_negative_existence.md`): **all InMemory repos register Scoped, NOT Singleton**, to avoid cross-test bleed. PR 1 council reviewer MUST verify this. PR 7's umbrella method calls `AddSunfishDocsSigningSeed` first.

#### Tests (PR 1, ~14 tests)

**`SigningWorkflowRecordTests.cs`**:
- `Construction_PreservesAllFields`
- `Status_Completed_RequiresCompletedAtAndFinalDocId` (throws if missing)
- `Status_Voided_RequiresVoidedAtAndReason` (throws if missing)
- `WorkflowKind_Hybrid_IsRejected` (throws — v1 drops hybrid)
- `DocumentVersionId_IsPinnedAtCreation` (constructor stores it; no reassignment via `with` expression on `Status != Draft`)

**`SigningWorkflowStatusTransitionTests.cs`**:
- `DraftToSent_IsAllowed`
- `DraftToCompleted_IsNotAllowed` (must go through Sent)
- `SentToCompleted_IsAllowed`
- `CompletedToAnything_IsNotAllowed` (terminal)
- `VoidedToAnything_IsNotAllowed` (terminal)
- `EnsureAllowed_ThrowsDescriptiveOnInvalidTransition`

**`SigningPartyStatusTransitionTests.cs`**:
- `PendingToInvited_IsAllowed`
- `PendingToSigned_IsNotAllowed` (must invite first)
- `InvitedToViewed_IsAllowed`
- `InvitedToSigned_IsAllowed` (sign-without-view path: signer didn't render the document UI but tapped sign — allowed)
- `SignedToAnything_IsNotAllowed` (terminal per-party)

**`SignatureRequestRecordTests.cs`**:
- `MagicLinkTokenHash_MustBe64Chars` (throws on wrong length)
- `ReminderCount_RejectsNegative`
- `ReminderCount_RejectsAboveMax` (default max 5)

**`LocalSignatureRecordTests.cs`**:
- `Cryptographic_RequiresKernelSignatureEventId` (throws if null)
- `Drawn_RequiresImageRef` (throws if null)
- `ContentHashAtSign_MustBe64Chars`

**`SigningAuditLogRecordTests.cs`**:
- `PrevEntryHashChain_MustBe64Chars`
- `Append_FirstRowUsesZeroHash` (`"0" * 64`)
- `Repository_UpdateAsync_Throws` (append-only enforcement)
- `Repository_DeleteAsync_Throws`

**`RepositoryRoundTripTests.cs`**:
- `Workflow_UpsertAndGetById_RoundTrips`
- `Workflow_TenantIsolation_OneTenantCannotSeeAnotherTenantsWorkflow`
- `AuditLog_AppendAndGetAll_PreservesInsertOrder`

Total new tests this PR: ~14.

#### Verification

- `dotnet build` succeeds for the new package + adds it to the solution.
- `dotnet test packages/blocks-docs-signing/tests/` passes all ~14 tests.
- `grep -r "Sunfish.Blocks.DocsSigning" packages/blocks-docs-signing/` returns hits in every `.cs` file (namespace sanity check).
- `grep -r "System.Security.Cryptography" packages/blocks-docs-signing/` returns **empty** (the security discipline check; if any hit appears, that's a PR-block).
- `grep -r "kernel-signatures" packages/blocks-docs-signing/Sunfish.Blocks.DocsSigning.csproj` returns the project reference (the dep is wired).

#### Do NOT in this PR

- Do NOT implement `ISigningWorkflowService`. That's PR 2.
- Do NOT implement `ISignatureService`. That's PR 3.
- Do NOT call `kernel-signatures.ISignatureCapture.CaptureAsync` from anywhere — no PR 1 service surface should invoke crypto.
- Do NOT introduce a `Signature` type with a global name; use `LocalSignature` to disambiguate from `kernel-signatures.SignatureEvent`.
- Do NOT add SQLite persistence. In-memory only per Phase 2 substrate posture.
- Do NOT add `Hybrid` to the workflow kind enum.
- Do NOT enable workflow construction with `Status != Draft` via the public constructor — initial state is always Draft; `Sent` transitions happen via the workflow service in PR 2.

---

### PR 2 — `ISigningWorkflowService` — Start / Dispatch / AdvanceParty / Decline / Cancel / Expire (state machine surface)

**Estimated effort:** ~3h
**Scope:** workflow lifecycle service; sequential + parallel orchestration; audit-log row emission on every state transition; magic-link token generation (hashed-only at rest); idempotent dispatch
**Commit subject:** `feat(blocks-docs-signing): ISigningWorkflowService — state machine + sequential/parallel dispatch + audit-on-transition`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-docs-signing-workflow-service`
**Pre-merge council:** **security-engineering MANDATORY**. Council scope:
  - State-machine completeness — no state-graph holes that allow a signer to bypass sequence
  - Sequential dispatch — only the next-in-order party is invited; out-of-order invitation attempts are rejected at the service layer (not just at the entity layer)
  - Magic-link token generation — uses a cryptographically-secure random source (`System.Security.Cryptography.RandomNumberGenerator` — note: this is the ONE allowed crypto import, scoped to token generation only; council must verify no other crypto surface sneaks in)
  - Magic-link token at-rest — only the `sha256(rawToken)` persists; the raw token leaves the service in **exactly one** notification dispatch call and never enters any log/exception/audit-payload
  - Audit-log emission on every transition — no state change without an audit row
  - Decline + void path — irreversible terminal-state transition; council confirms there is no "unvoid" or "undecline" path even via repository-level fields
  - Tenant scoping — every service method takes a `tenantId` argument either via `ITenantContext` or explicit parameter; no overload bypasses the check
  - **Negative-existence spot-check** (per `feedback_council_can_miss_spot_check_negative_existence.md`): grep the entire `ISigningWorkflowService` impl for `IsDraft` checks before mutation; grep for `WorkflowId` round-trip checks in audit-write paths; council reviewer reads actual code, not just descriptions

#### New service contract

**`Services/ISigningWorkflowService.cs`**:

```
public interface ISigningWorkflowService {
    Task<StartResult> StartAsync(StartCommand cmd, CancellationToken ct = default);
    Task<DispatchResult> DispatchAsync(SigningWorkflowId workflowId, CancellationToken ct = default);
    Task<AdvancePartyResult> AdvancePartyAsync(AdvancePartyCommand cmd, CancellationToken ct = default);
    Task<DeclineResult> DeclineAsync(DeclineCommand cmd, CancellationToken ct = default);
    Task<CancelResult> CancelAsync(SigningWorkflowId workflowId, PartyId by, string reason, CancellationToken ct = default);
    Task<ExpireResult> ExpireAsync(SigningWorkflowId workflowId, CancellationToken ct = default);
}

public sealed record StartCommand(
    DocumentId DocumentId,
    DocumentVersionId DocumentVersionId,
    string WorkflowKind,                                 // "sequential" | "parallel"
    IReadOnlyList<SigningPartySpec> Parties,
    IReadOnlyList<SigningStepSpec> Steps,
    PartyId InitiatedBy,
    Instant? ExpiresAtUtc,
    string? TemplateId);

public sealed record SigningPartySpec(
    int PartyOrder, string RoleLabel, string PartyKind,
    PartyId? PrincipalId, string? ExternalName, string? ExternalEmail,
    PartyAuthMethod AuthMethod);

public sealed record SigningStepSpec(
    int StepOrder, SigningStepKind StepKind,
    int? PageNumber, decimal? PositionX, decimal? PositionY,
    decimal? WidthFraction, decimal? HeightFraction,
    bool Required, int AssignedPartyOrder);    // resolves to PartyId after parties persist

public sealed record StartResult(SigningWorkflow? Workflow, StartError Error, string? Detail);
public enum StartError {
    None, UnknownDocument, DocumentNotPublished, NoParties, NoSteps,
    InvalidWorkflowKind, InvalidPartySpec, InvalidStepSpec,
    AssignedPartyOrderUnresolved, DocumentLockedByOtherWorkflow,
}

public sealed record DispatchResult(SigningWorkflow? Workflow, DispatchError Error, string? Detail);
public enum DispatchError {
    None, UnknownWorkflow, NotInDraft, NoPartiesToNotify,
    NotificationDispatchFailed,
}

public sealed record AdvancePartyCommand(
    SigningWorkflowId WorkflowId,
    SigningPartyId PartyId,
    SigningPartyStatus ToStatus,
    PartyId? ActorId,                                    // optional — employee acting on behalf
    string? IpAddress,
    string? UserAgent);

public sealed record AdvancePartyResult(SigningParty? Party, SigningWorkflow? Workflow, AdvancePartyError Error, string? Detail);
public enum AdvancePartyError {
    None, UnknownWorkflow, UnknownParty, InvalidPartyTransition,
    PartyOutOfOrder,                  // sequential workflows only — party tried to sign before predecessor completed
    WorkflowTerminal,
}

public sealed record DeclineCommand(SigningWorkflowId WorkflowId, SigningPartyId PartyId, string Reason, PartyId? ActorId);
public sealed record DeclineResult(SigningWorkflow? Workflow, DeclineError Error, string? Detail);
public enum DeclineError { None, UnknownWorkflow, UnknownParty, AlreadyTerminal }

public sealed record CancelResult(SigningWorkflow? Workflow, CancelError Error, string? Detail);
public enum CancelError { None, UnknownWorkflow, AlreadyTerminal, EmptyReason }

public sealed record ExpireResult(SigningWorkflow? Workflow, ExpireError Error, string? Detail);
public enum ExpireError { None, UnknownWorkflow, NotEligible, NoExpiry }
```

#### `StartAsync` algorithm

```
start(cmd):
  // Phase 1 — preconditions
  doc = await docsCore.GetDocumentByIdAsync(cmd.DocumentId)
  if doc == null: return Err(UnknownDocument)
  if doc.Status != Published: return Err(DocumentNotPublished)
  if cmd.Parties.Count < 1: return Err(NoParties)
  if cmd.Steps.Count < 1: return Err(NoSteps)
  if cmd.WorkflowKind not in {"sequential", "parallel"}: return Err(InvalidWorkflowKind)

  // Phase 2 — validate party + step specs
  for p in cmd.Parties: validatePartySpec(p) -> Err(InvalidPartySpec) on failure
  for s in cmd.Steps:   validateStepSpec(s) -> Err(InvalidStepSpec)

  // Phase 3 — document lock check
  existing = await workflowRepo.GetByDocumentIdAsync(cmd.DocumentId)
  if existing != null AND !existing.Status.IsTerminal():
    return Err(DocumentLockedByOtherWorkflow, detail: existing.Id)

  // Phase 4 — persist workflow + parties + steps (one transaction)
  workflow = new SigningWorkflow(
    Id: SigningWorkflowId.New(),
    TenantId: tenant.Id,
    DocumentId: cmd.DocumentId,
    DocumentVersionId: cmd.DocumentVersionId,
    WorkflowKind: cmd.WorkflowKind,
    Status: Draft,
    InitiatedBy: cmd.InitiatedBy,
    InitiatedAtUtc: now,
    ExpiresAtUtc: cmd.ExpiresAtUtc ?? (now + options.WorkflowDefaultExpiryDays),
    TemplateId: cmd.TemplateId,
    Version: 1)

  await workflowRepo.UpsertAsync(workflow)

  partyIdByOrder = new Dictionary<int, SigningPartyId>()
  for spec in cmd.Parties:
    party = new SigningParty(
      Id: SigningPartyId.New(), TenantId: tenant.Id, WorkflowId: workflow.Id,
      PartyOrder: spec.PartyOrder, RoleLabel: spec.RoleLabel, PartyKind: spec.PartyKind,
      PrincipalId: spec.PrincipalId, ExternalName: spec.ExternalName, ExternalEmail: spec.ExternalEmail,
      Status: Pending, AuthMethod: spec.AuthMethod, Version: 1)
    await partyRepo.UpsertAsync(party)
    partyIdByOrder[spec.PartyOrder] = party.Id

  for spec in cmd.Steps:
    if !partyIdByOrder.TryGetValue(spec.AssignedPartyOrder, out var partyId):
      return Err(AssignedPartyOrderUnresolved, detail: $"step {spec.StepOrder} -> party order {spec.AssignedPartyOrder}")
    step = new SigningStep(
      Id: SigningStepId.New(), TenantId: tenant.Id, WorkflowId: workflow.Id,
      StepOrder: spec.StepOrder, StepKind: spec.StepKind,
      PageNumber: spec.PageNumber, PositionX: spec.PositionX, PositionY: spec.PositionY,
      WidthFraction: spec.WidthFraction, HeightFraction: spec.HeightFraction,
      Required: spec.Required, AssignedPartyId: partyId)
    await stepRepo.UpsertAsync(step)

  // Phase 5 — audit-log row (WorkflowCreated)
  await audit.AppendAsync(workflow.Id, SigningAuditEvent.WorkflowCreated, partyId: null,
    payload: { "partyCount": cmd.Parties.Count.ToString(), "stepCount": cmd.Steps.Count.ToString(),
               "kind": cmd.WorkflowKind })

  return Ok(workflow)
```

#### `DispatchAsync` algorithm

```
dispatch(workflowId):
  workflow = await workflowRepo.GetByIdAsync(workflowId)
  if workflow == null: return Err(UnknownWorkflow)
  if workflow.Status != Draft: return Err(NotInDraft)

  parties = await partyRepo.GetByWorkflowAsync(workflowId)
  if parties.Count < 1: return Err(NoPartiesToNotify)

  partiesToInvite = workflow.WorkflowKind == "sequential"
    ? [parties.OrderBy(p => p.PartyOrder).First()]
    : parties

  for party in partiesToInvite:
    rawToken = secureRandomToken(bytes: 32)             // System.Security.Cryptography.RandomNumberGenerator (URL-safe base64)
    tokenHash = sha256Hex(rawToken)                     // delegate to kernel-security.computeContentHash
    request = new SignatureRequest(
      Id: SignatureRequestId.New(), TenantId: tenant.Id, WorkflowId: workflowId, PartyId: party.Id,
      Channel: party.AuthMethod.ToChannel(),            // MagicLink -> Email; SmsOtp -> Sms; etc.
      Recipient: party.ExternalEmail ?? lookupEmail(party.PrincipalId),
      MagicLinkTokenHash: tokenHash,
      MagicLinkExpiresAtUtc: now + options.MagicLinkExpiryDays,
      ReminderCount: 0,
      DeliveryStatus: Queued)
    await requestRepo.UpsertAsync(request)

    // Dispatch notification — raw token leaves the service ONCE here
    try:
      await notifications.SendSignerInviteAsync(request.Id, rawToken)
    catch (Exception ex):
      // do NOT include rawToken in any log/exception payload
      return Err(NotificationDispatchFailed, detail: ex.GetType().Name)
    finally:
      rawToken = null     // explicit; help GC; also signal intent to reviewer

    // Update party + request post-dispatch
    party = party with { Status: Invited, InvitedAtUtc: now, Version: party.Version + 1 }
    await partyRepo.UpsertAsync(party)
    request = request with { SentAtUtc: now, DeliveryStatus: Sent }
    await requestRepo.UpsertAsync(request)
    await audit.AppendAsync(workflowId, SigningAuditEvent.PartyInvited, partyId: party.Id,
      payload: { "channel": party.AuthMethod.ToChannel().ToString() })

  // Workflow status flip
  workflow = workflow with { Status: Sent, Version: workflow.Version + 1 }
  await workflowRepo.UpsertAsync(workflow)
  await audit.AppendAsync(workflowId, SigningAuditEvent.WorkflowSent, partyId: null, payload: {})

  // Emit cross-cluster event
  await events.PublishAsync(new SigningWorkflowDispatchedEvent(
    WorkflowId: workflowId, DocumentId: workflow.DocumentId, PartyCount: partiesToInvite.Count))

  return Ok(workflow)
```

#### `AdvancePartyAsync` algorithm

```
advance(cmd):
  workflow = await workflowRepo.GetByIdAsync(cmd.WorkflowId)
  if workflow == null: return Err(UnknownWorkflow)
  if workflow.Status.IsTerminal(): return Err(WorkflowTerminal)
  party = await partyRepo.GetByIdAsync(cmd.PartyId)
  if party == null or party.WorkflowId != cmd.WorkflowId: return Err(UnknownParty)

  // State-machine check
  if !SigningPartyStatusTransitions.IsAllowed(party.Status, cmd.ToStatus):
    return Err(InvalidPartyTransition, detail: $"{party.Status} -> {cmd.ToStatus}")

  // Sequential-order check (only for sign transition)
  if cmd.ToStatus == SigningPartyStatus.Signed AND workflow.WorkflowKind == "sequential":
    predecessor = parties.OrderBy(p => p.PartyOrder).TakeWhile(p => p.PartyOrder < party.PartyOrder).LastOrDefault()
    if predecessor != null AND predecessor.Status != Signed:
      return Err(PartyOutOfOrder, detail: $"predecessor party {predecessor.Id} status={predecessor.Status}")

  // Update party
  party = party with {
    Status: cmd.ToStatus, Version: party.Version + 1,
    ViewedAtUtc: cmd.ToStatus == Viewed ? now : party.ViewedAtUtc,
    SignedAtUtc: cmd.ToStatus == Signed ? now : party.SignedAtUtc,
  }
  await partyRepo.UpsertAsync(party)
  await audit.AppendAsync(cmd.WorkflowId, partyEventOf(cmd.ToStatus), partyId: party.Id,
    payload: { }, actorId: cmd.ActorId, ipAddress: cmd.IpAddress, userAgent: cmd.UserAgent)

  // Workflow-level transitions
  if cmd.ToStatus == Signed:
    if workflow.WorkflowKind == "sequential":
      next = parties.OrderBy(p => p.PartyOrder).FirstOrDefault(p => p.PartyOrder > party.PartyOrder)
      if next != null:
        await dispatchToParty(next.Id)                  // private helper: same as DispatchAsync but single-party
    if allPartiesSigned(parties):
      // Trigger completion via PR 3's ISignatureService.CompleteWorkflowAsync (which runs the merge + emits)
      await signatureService.CompleteWorkflowAsync(cmd.WorkflowId)
    else:
      workflow = workflow with { Status: InProgress, Version: workflow.Version + 1 }
      await workflowRepo.UpsertAsync(workflow)

  return Ok(party, workflow)
```

#### `DeclineAsync` algorithm

```
decline(cmd):
  workflow = await workflowRepo.GetByIdAsync(cmd.WorkflowId)
  if workflow == null: return Err(UnknownWorkflow)
  if workflow.Status.IsTerminal(): return Err(AlreadyTerminal)
  party = await partyRepo.GetByIdAsync(cmd.PartyId)
  if party == null: return Err(UnknownParty)

  party = party with { Status: Declined, DeclinedAtUtc: now, DeclineReason: cmd.Reason }
  await partyRepo.UpsertAsync(party)
  await audit.AppendAsync(cmd.WorkflowId, SigningAuditEvent.PartyDeclined, partyId: party.Id,
    payload: { "reason": cmd.Reason }, actorId: cmd.ActorId)

  // One decline = terminal workflow decline (per security posture §6)
  workflow = workflow with { Status: Declined, Version: workflow.Version + 1 }
  await workflowRepo.UpsertAsync(workflow)
  await audit.AppendAsync(cmd.WorkflowId, SigningAuditEvent.WorkflowVoided, partyId: null,
    payload: { "cause": "party-declined", "partyId": party.Id.Value })
  await events.PublishAsync(new SigningPartyDeclinedEvent(cmd.WorkflowId, party.Id, cmd.Reason))
  return Ok(workflow)
```

#### `CancelAsync` algorithm

Sets `Status = Voided`, populates `VoidedAtUtc + VoidReason`; emits `WorkflowVoided` audit + `Docs.SigningWorkflowVoided` event. Idempotent if already in terminal state (returns `AlreadyTerminal`).

#### `ExpireAsync` algorithm

Returns `NotEligible` unless `Status ∈ {Sent, InProgress}` AND `ExpiresAtUtc != null` AND `ExpiresAtUtc < now`. Otherwise: `Status = Expired`; audit + event. Called by PR 5's background sweep.

#### Helper service — `IAuditChainAppender` (private)

To keep the workflow service from re-implementing chain construction, ship a small helper:

```
internal interface IAuditChainAppender {
    Task AppendAsync(SigningWorkflowId workflowId, SigningAuditEvent eventKind,
      SigningPartyId? partyId, IReadOnlyDictionary<string, string> payload,
      PartyId? actorId = null, string? ipAddress = null, string? userAgent = null,
      CancellationToken ct = default);
}
```

The impl reads the latest audit row, computes `prevHash = sha256Hex(canonicalize(prevRow))`, computes `contentHashNow = await kernelSecurity.ComputeContentHashAsync(documentBody)`, constructs the new row, and appends. Canonicalization is `System.Text.Json.JsonSerializer.Serialize(row, options)` with `JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }` and a property-order check via a test. Council scope: confirm canonicalization is stable across .NET versions.

#### Magic-link token generation discipline

```
internal static class MagicLinkTokens {
    public static string Generate() {
        // 32 bytes -> 43 chars base64url; cryptographically secure
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url.Encode(bytes);
    }

    public static string Hash(string rawToken) {
        // delegate to kernel-security via injected hasher; the static helper is a thin wrapper
        // that callers should NOT use except in service-internal code paths
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
    }
}
```

**Council note:** the SHA256 call here is the ONLY direct crypto use in this package. It is bounded to the token-hashing helper. Confirm during review that this is acceptable scope or refactor to delegate to `kernel-security.computeContentHash`. **Preference:** delegate to kernel-security to keep this package crypto-free; if kernel-security doesn't expose a string-input variant, file `cob-question-*` for an XO ruling on whether to (a) add a string-input overload to kernel-security or (b) accept the bounded SHA256 use here.

#### DI extension (extends PR 1's seed)

```
public static IServiceCollection AddSunfishDocsSigningWorkflows(this IServiceCollection services) {
    services.AddScoped<ISigningWorkflowService, SigningWorkflowService>();
    services.AddScoped<IAuditChainAppender, AuditChainAppender>();
    services.AddScoped<ISigningEventPublisher, InMemorySigningEventPublisher>();
    return services;
}
```

PR 7's umbrella calls both `AddSunfishDocsSigningSeed()` + `AddSunfishDocsSigningWorkflows()` + (PR 3's services).

#### Tests (PR 2, ~16 tests)

**`SigningWorkflowServiceTests.cs`** (Start path):
- `Start_RejectsUnknownDocument`
- `Start_RejectsUnpublishedDocument`
- `Start_RejectsEmptyParties`
- `Start_RejectsHybridKind`
- `Start_RejectsAlreadyActiveWorkflow_OnSameDoc`
- `Start_HappyPath_PersistsWorkflowPartiesSteps_AndAuditsWorkflowCreated`
- `Start_StepAssignedPartyOrder_ResolvesToPartyId`

**`SigningWorkflowDispatchTests.cs`**:
- `Dispatch_Sequential_InvitesOnlyFirstParty`
- `Dispatch_Parallel_InvitesAllParties`
- `Dispatch_RejectsNonDraft`
- `Dispatch_PersistsTokenHashOnly_NeverRawToken` (verify the persisted `SignatureRequest.MagicLinkTokenHash` is 64-char sha256 hex; verify the raw token is not in any logged output)
- `Dispatch_AuditsPartyInvitedThenWorkflowSent`
- `Dispatch_NotificationFailure_ReturnsErrorAndLeavesWorkflowInDraft`

**`AdvancePartyTests.cs`**:
- `Advance_InvitedToSigned_HappyPath_AuditsPartySigned`
- `Advance_Sequential_OutOfOrder_Rejects` (party 2 cannot sign before party 1)
- `Advance_LastSignerSigning_TriggersCompleteWorkflow` (stub `ISignatureService` verifies the call)
- `Advance_Parallel_AllPartiesSignConcurrently_Converge` (uses 2-replica simulation)

**`DeclineCancelExpireTests.cs`**:
- `Decline_TerminatesWorkflow_AsDeclined_OnFirstPartyDecline`
- `Cancel_RequiresReason_RejectsEmpty`
- `Cancel_OnTerminal_ReturnsAlreadyTerminal`
- `Expire_OnlyEligibleIfPastExpiresAt`

**`AuditChainAppenderTests.cs`**:
- `Append_FirstRow_UsesZeroHash`
- `Append_SecondRow_HashesFirstRow`
- `Append_PayloadStringValuesAreCanonicalized` (deterministic serialization across runs)

Total new tests this PR: ~16.

#### Verification

- All PR 1 tests still pass.
- New tests pass.
- `grep -r "System.Security.Cryptography" packages/blocks-docs-signing/` returns hits ONLY in `MagicLinkTokens.cs` (the bounded scope per the council note). Anything else is a PR-block.
- `grep -rE "rawToken|raw_token" packages/blocks-docs-signing/` returns hits ONLY in `SigningWorkflowService.cs` (inside the dispatch path) — verify no log/exception emission of the variable.

#### Do NOT in this PR

- Do NOT implement signature capture. That's PR 3.
- Do NOT implement the merge → `finalSignedDocumentId`. That's PR 3.
- Do NOT introduce a "resend invitation" path. Reminders are PR 5.
- Do NOT introduce a non-Draft → Draft "edit" transition.
- Do NOT add an `Unvoid` or `Undecline` operation.

---

### PR 3 — `ISignatureService` — capture via kernel-signatures + audit chain construction + tamper verification + `Docs.DocumentSigned` events

**Estimated effort:** ~3–4h
**Scope:** signature capture via `kernel-signatures.ISignatureCapture.CaptureAsync`; `LocalSignature` persistence with `KernelSignatureEventId` FK; `ContentHashAtSign` pinning via `kernel-security.computeContentHash`; tamper-detection (`VerifyTamperFree`); audit-chain verification (`VerifyChain`); workflow completion (`CompleteWorkflowAsync`); cross-cluster event emission
**Commit subject:** `feat(blocks-docs-signing): ISignatureService — capture via kernel-signatures + audit chain + tamper verification`
**Depends on:** PR 2 merged
**Branch:** `cob/blocks-docs-signing-signature-service`
**Pre-merge council:** **security-engineering MANDATORY + .NET architect recommended**. This is the **crypto-adjacent + hash-chain integrity + tamper-detection** PR. Council scope:
  - Verify `kernel-signatures.ISignatureCapture.CaptureAsync` call site — every required field (`Tenant`, `Signer`, `Consent`, `DocumentHash`, `Scope`, `Envelope`, `Quality`) is populated correctly; no defaults that quietly weaken
  - `SignatureCaptureRequest.Scope` (taxonomy classifications per ADR 0054 §A7) — confirm the right `Sunfish.Signature.Scopes@1.0.0` node is selected per `LocalSignature.SignatureKind` (e.g. `lease-execution` for a lease document; `policy-acknowledgment` for a policy)
  - `ContentHashAtSign` MUST be computed from the **current document bytes** read from `blocks-docs-core.IDocumentRepository` → `blocks-docs.IBlobStore`, NOT from any cached hash on the Document row
  - Tamper-detection — `VerifyTamperFree` returns the right error when current hash != stored hash; the audit row `DocumentTamperedDetected` is appended at the failure point
  - Audit-chain verification — `VerifyChain` reads ALL audit rows for the workflow, recomputes the chain, and reports the first mismatch position
  - `Docs.DocumentSigned` event emission — fires once per `LocalSignature` persist; idempotent under retry
  - `Docs.ContractFullySigned` event emission — fires once when workflow completes if `templateId != null` AND the template was a contract template
  - **Negative-existence spot-check**: grep for `kernel-signatures.ISignatureRevocationLog` — this package does NOT write to the revocation log directly; revocation is a `kernel-signatures` concern triggered by external admin action (post-MVP)
  - No `using System.Security.Cryptography` imports in this PR (token hashing already isolated in PR 2)

#### New service contract

**`Services/ISignatureService.cs`**:

```
public interface ISignatureService {
    Task<CaptureResult> CaptureAsync(CaptureCommand cmd, CancellationToken ct = default);
    Task<CompleteWorkflowResult> CompleteWorkflowAsync(SigningWorkflowId workflowId, CancellationToken ct = default);
    Task<VerifyChainResult> VerifyChainAsync(SigningWorkflowId workflowId, CancellationToken ct = default);
    Task<VerifyTamperResult> VerifyTamperFreeAsync(LocalSignatureId signatureId, CancellationToken ct = default);
}

public sealed record CaptureCommand(
    SigningWorkflowId WorkflowId,
    SigningStepId StepId,
    SigningPartyId PartyId,
    SignatureKind Kind,
    string? ImageRef,                                    // for Drawn/Typed
    ConsentRecordId ConsentId,                           // UETA/E-SIGN consent — per ADR 0054
    SignatureEnvelope? Envelope,                         // for Cryptographic — opaque per ADR 0054 §A2
    CaptureQuality Quality,                              // per kernel-signatures.Models.CaptureQuality
    PenStrokeBlobRef? PenStroke,
    Geolocation? Location,
    DeviceAttestation? Attestation,
    string? IpAddress,
    string? UserAgent,
    string? Geolocation);

public sealed record CaptureResult(
    LocalSignature? Signature,
    SignatureEventId? KernelSignatureEventId,
    CaptureError Error,
    string? Detail);

public enum CaptureError {
    None, UnknownWorkflow, UnknownStep, UnknownParty,
    WorkflowNotInFlight,            // not in {Sent, InProgress}
    PartyNotInviteOrViewed,         // party must be Invited or Viewed to sign
    StepAlreadyCompleted,
    StepKindMismatch,               // e.g. trying to capture a signature on a Date step
    CryptographicMissingEnvelope,
    DrawnTypedMissingImageRef,
    KernelCaptureFailed,            // wraps any kernel-signatures error
    ContentHashUnavailable,         // document body could not be loaded
    DocumentTamperedDetected,       // pre-capture tamper check failed
}

public sealed record CompleteWorkflowResult(SigningWorkflow? Workflow, DocumentId? FinalDocumentId, CompleteError Error, string? Detail);
public enum CompleteError { None, UnknownWorkflow, NotAllSigned, AlreadyTerminal, MergeFailed }

public sealed record VerifyChainResult(bool Valid, int FirstBrokenAtIndex, string? Detail);
public sealed record VerifyTamperResult(bool TamperFree, string? StoredHash, string? CurrentHash, string? Detail);
```

#### `CaptureAsync` algorithm — per §5.3 + §6.5

```
capture(cmd):
  // Phase 1 — preconditions
  workflow = await workflowRepo.GetByIdAsync(cmd.WorkflowId)
  if workflow == null: return Err(UnknownWorkflow)
  if workflow.Status not in {Sent, InProgress}: return Err(WorkflowNotInFlight)

  step = await stepRepo.GetByIdAsync(cmd.StepId)
  if step == null or step.WorkflowId != cmd.WorkflowId: return Err(UnknownStep)
  if step.CompletedAtUtc != null: return Err(StepAlreadyCompleted)

  party = await partyRepo.GetByIdAsync(cmd.PartyId)
  if party == null or party.WorkflowId != cmd.WorkflowId: return Err(UnknownParty)
  if party.Status not in {Invited, Viewed}: return Err(PartyNotInviteOrViewed)

  // Phase 2 — step kind / capture kind consistency
  if step.StepKind == SigningStepKind.Signature OR step.StepKind == SigningStepKind.Initial:
    if cmd.Kind == SignatureKind.Cryptographic AND cmd.Envelope == null: return Err(CryptographicMissingEnvelope)
    if cmd.Kind in {Drawn, Typed} AND string.IsNullOrEmpty(cmd.ImageRef): return Err(DrawnTypedMissingImageRef)
  else:
    return Err(StepKindMismatch, detail: $"step kind {step.StepKind} does not accept signature capture")

  // Phase 3 — pin content hash + tamper check
  bytes = await docsCore.LoadDocumentBytesAsync(workflow.DocumentVersionId)
  if bytes == null: return Err(ContentHashUnavailable)
  contentHashNow = await kernelSecurity.ComputeContentHashAsync(bytes)

  // Compare against the pinned hash recorded at WorkflowCreated audit row
  pinnedAudit = (await auditRepo.GetAllForWorkflowAsync(workflow.Id)).First()
  pinnedHash = pinnedAudit.ContentHashAtEvent
  if pinnedHash != contentHashNow:
    await audit.AppendAsync(workflow.Id, SigningAuditEvent.DocumentTamperedDetected, partyId: cmd.PartyId,
      payload: { "pinnedHash": pinnedHash[0..16], "currentHash": contentHashNow[0..16] })
    return Err(DocumentTamperedDetected)

  // Phase 4 — delegate to kernel-signatures for crypto path
  kernelSignatureEventId = null
  if cmd.Kind == SignatureKind.Cryptographic:
    var kernelRequest = new SignatureCaptureRequest {
      Tenant = tenant.Id,
      Signer = party.PrincipalId?.ToActorId() ?? actorIdFromExternal(party),
      Consent = cmd.ConsentId,
      DocumentHash = new ContentHash(contentHashNow),
      Scope = resolveScopeTaxonomy(workflow, step),     // helper: maps workflow context to Sunfish.Signature.Scopes@1.0.0 nodes
      Envelope = cmd.Envelope!,
      Quality = cmd.Quality,
      PenStroke = cmd.PenStroke,
      Location = cmd.Location,
      Attestation = cmd.Attestation,
    }
    try:
      var kernelEvent = await kernelSignatureCapture.CaptureAsync(kernelRequest, ct)
      kernelSignatureEventId = kernelEvent.Id
    catch (Exception ex):
      // kernel-signatures errors do not leak details about envelope or signer beyond a category code
      return Err(KernelCaptureFailed, detail: ex.GetType().Name)

  // Phase 5 — persist LocalSignature (always; cryptographic + non-cryptographic both)
  signature = new LocalSignature(
    Id: LocalSignatureId.New(), TenantId: tenant.Id,
    WorkflowId: cmd.WorkflowId, StepId: cmd.StepId, PartyId: cmd.PartyId,
    SignatureKind: cmd.Kind,
    ImageRef: cmd.ImageRef,
    KernelSignatureEventId: kernelSignatureEventId?.Value,
    SignedAtUtc: now,
    SignedFromIp: cmd.IpAddress, SignedUserAgent: cmd.UserAgent, SignedGeolocation: cmd.Geolocation,
    DocumentVersionAtSignId: workflow.DocumentVersionId,
    ContentHashAtSign: contentHashNow)
  await signatureRepo.AppendAsync(signature)            // append-only

  // Phase 6 — update step + audit
  step = step with { CompletedAtUtc: now, CompletedValue: signature.Id.Value }
  await stepRepo.UpsertAsync(step)
  await audit.AppendAsync(cmd.WorkflowId, SigningAuditEvent.PartySigned, partyId: cmd.PartyId,
    payload: { "stepId": step.Id.Value, "kind": cmd.Kind.ToString(),
               "kernelSig": kernelSignatureEventId?.Value.ToString() ?? "" },
    actorId: null, ipAddress: cmd.IpAddress, userAgent: cmd.UserAgent)

  // Phase 7 — emit Docs.DocumentSigned event
  await events.PublishAsync(new DocumentSignedEvent(
    DocumentId: workflow.DocumentId, ContractId: workflow.TemplateId,
    SignerPartyId: party.PrincipalId ?? PartyId.External(party.ExternalEmail!),
    SignedAt: signature.SignedAtUtc))

  return Ok(signature, kernelSignatureEventId)
```

#### `CompleteWorkflowAsync` algorithm

```
complete(workflowId):
  workflow = await workflowRepo.GetByIdAsync(workflowId)
  if workflow == null: return Err(UnknownWorkflow)
  if workflow.Status.IsTerminal(): return Err(AlreadyTerminal)
  parties = await partyRepo.GetByWorkflowAsync(workflowId)
  if !parties.All(p => p.Status == Signed): return Err(NotAllSigned)

  // Run the final-doc merge (passthrough stub in v1)
  finalDocResult = await mergeService.MergeFinalSignedDocumentAsync(workflowId)
  if !finalDocResult.IsSuccess: return Err(MergeFailed, detail: finalDocResult.Detail)

  workflow = workflow with {
    Status: Completed, CompletedAtUtc: now,
    FinalSignedDocumentId: finalDocResult.DocumentId,
    Version: workflow.Version + 1,
  }
  await workflowRepo.UpsertAsync(workflow)
  await audit.AppendAsync(workflowId, SigningAuditEvent.WorkflowCompleted, partyId: null,
    payload: { "finalDocId": finalDocResult.DocumentId.Value })

  // Cross-cluster events
  await events.PublishAsync(new SigningWorkflowCompletedEvent(workflowId, finalDocResult.DocumentId))
  if workflow.TemplateId != null:
    await events.PublishAsync(new ContractFullySignedEvent(
      ContractInstanceId: workflow.TemplateId,
      FinalSignedDocumentId: finalDocResult.DocumentId,
      FullySignedAt: workflow.CompletedAtUtc.Value))

  return Ok(workflow, finalDocResult.DocumentId)
```

#### `VerifyChainAsync` algorithm

```
verifyChain(workflowId):
  entries = await auditRepo.GetAllForWorkflowAsync(workflowId)    // ordered by OccurredAtUtc
  prev = new string('0', 64)
  for i, entry in enumerate(entries):
    if entry.PrevEntryHashChain != prev:
      return new VerifyChainResult(Valid: false, FirstBrokenAtIndex: i,
        Detail: $"expected prev={prev[0..16]}; got prev={entry.PrevEntryHashChain[0..16]}")
    prev = sha256Hex(canonicalize(entry))
  return new VerifyChainResult(Valid: true, FirstBrokenAtIndex: -1, Detail: null)
```

#### `VerifyTamperFreeAsync` algorithm

```
verifyTamperFree(signatureId):
  sig = await signatureRepo.GetByIdAsync(signatureId)
  if sig == null: return new VerifyTamperResult(TamperFree: false, ..., Detail: "unknown signature")
  bytes = await docsCore.LoadDocumentVersionBytesAsync(sig.DocumentVersionAtSignId)
  currentHash = await kernelSecurity.ComputeContentHashAsync(bytes)
  return new VerifyTamperResult(
    TamperFree: sig.ContentHashAtSign == currentHash,
    StoredHash: sig.ContentHashAtSign,
    CurrentHash: currentHash,
    Detail: null)
```

#### Final-doc merge — passthrough stub

**`Services/IFinalSignedDocumentMerger.cs`** + **`PassthroughFinalSignedDocumentMerger.cs`**:

```
public interface IFinalSignedDocumentMerger {
    Task<MergeResult> MergeFinalSignedDocumentAsync(SigningWorkflowId workflowId, CancellationToken ct = default);
}
public sealed record MergeResult(bool IsSuccess, DocumentId? DocumentId, string? Detail);
```

The passthrough impl: creates a new `Document` of `documentType = signed-pdf`; body is `original-doc-bytes + appended-cert-page-text`; persists via `IDocumentCommandService.CreateAsync` (from `blocks-docs-core`); returns the new `DocumentId`. The cert page is plain-text listing every `LocalSignature` for the workflow (party label, signed-at, kernel signature event id if present, ip, geolocation). True PDF-form-field embedding is a follow-on.

#### Cross-cluster event records

Local stubs (relocate when canonical event-bus package lands), in `Events/`:

- `SigningWorkflowDispatchedEvent(SigningWorkflowId, DocumentId, int PartyCount)`
- `SigningWorkflowCompletedEvent(SigningWorkflowId, DocumentId FinalDocumentId)` → per `cross-cluster-event-bus-design.md` §3.4 idempotency-key `signing-completed:{workflowId}`
- `SigningWorkflowVoidedEvent(SigningWorkflowId, string Reason)`
- `SigningWorkflowExpiredEvent(SigningWorkflowId)`
- `SigningPartyDeclinedEvent(SigningWorkflowId, SigningPartyId, string Reason)`
- `DocumentSignedEvent(DocumentId, string? ContractId, PartyId SignerPartyId, Instant SignedAt)` → idempotency-key `document-signed:{documentId}:{signerPartyId}`
- `ContractFullySignedEvent(string ContractInstanceId, DocumentId FinalSignedDocumentId, Instant FullySignedAt)` → idempotency-key `contract-fully-signed:{contractInstanceId}`

#### DI extension (extends PR 2's)

```
public static IServiceCollection AddSunfishDocsSigningCapture(this IServiceCollection services) {
    services.AddScoped<ISignatureService, SignatureService>();
    services.AddScoped<IFinalSignedDocumentMerger, PassthroughFinalSignedDocumentMerger>();
    return services;
}
```

The `kernel-signatures.ISignatureCapture` is registered by `kernel-signatures.AddSunfishKernelSignatures()` (already on main; verify via Pre-build checklist §3); the umbrella in PR 7 calls that first.

#### Tests (PR 3, ~18 tests)

**`SignatureServiceCaptureTests.cs`**:
- `Capture_RejectsUnknownWorkflow`
- `Capture_RejectsWorkflowNotInFlight` (e.g. Draft)
- `Capture_RejectsStepAlreadyCompleted`
- `Capture_RejectsStepKindMismatch` (Date step + Signature kind → rejection)
- `Capture_RejectsCryptographicWithoutEnvelope`
- `Capture_RejectsDrawnWithoutImageRef`
- `Capture_HappyPath_Drawn_PersistsLocalSignature_AuditsPartySigned`
- `Capture_HappyPath_Cryptographic_CallsKernelSignaturesAndStoresEventId`
- `Capture_DetectsTamperedDocument` (mutate the document body between Start and Capture; verify error + audit row)
- `Capture_PinsContentHashAtSign_NotAtDispatch` (capture-time hash, not stored hash)
- `Capture_EmitsDocumentSignedEvent`

**`CompleteWorkflowTests.cs`**:
- `Complete_RejectsIfNotAllSigned`
- `Complete_HappyPath_RunsMerge_AndEmitsCompletedEvent`
- `Complete_WithTemplateId_EmitsContractFullySigned`
- `Complete_WithoutTemplateId_DoesNotEmitContractFullySigned`

**`VerifyChainTests.cs`**:
- `VerifyChain_OnIntactChain_ReturnsValid`
- `VerifyChain_OnTamperedRow_ReturnsFirstBrokenIndex`

**`VerifyTamperFreeTests.cs`**:
- `VerifyTamperFree_OnUnchangedDocument_ReturnsTrue`
- `VerifyTamperFree_OnMutatedDocument_ReturnsFalseWithBothHashes`

Total new tests this PR: ~18.

#### Verification

- All PR 1 + PR 2 tests still pass.
- New tests pass.
- `grep -r "System.Security.Cryptography" packages/blocks-docs-signing/Services/` returns ONLY the PR 2 token-hashing hits — no new crypto imports.
- Integration test exercises full path: Start (PR 2) → Dispatch (PR 2) → Advance(Viewed) (PR 2) → Capture (PR 3) → Complete (PR 3); verify all audit rows are chained correctly via `VerifyChainAsync`.
- Test that uses a real `kernel-signatures.InMemorySignatureCapture` (not a mock) verifies a cryptographic capture round-trips end-to-end and `LocalSignature.KernelSignatureEventId` matches the kernel's returned `SignatureEvent.Id.Value`.

#### Do NOT in this PR

- Do NOT implement parallel-signing race-resolution beyond the basic state-machine; full convergence battery is PR 4.
- Do NOT introduce a PDF rendering pipeline. Passthrough stub is canonical for v1.
- Do NOT implement signature revocation. Revocation is `kernel-signatures.ISignatureRevocationLog` territory; this package never writes to it.
- Do NOT introduce a `BiometricReplay` API or a `RenderPenStroke` API. The pen-stroke ref is opaque; rendering is UI-layer concern.
- Do NOT add any `using System.Security.Cryptography` imports in `Services/` beyond what PR 2 introduced (and ideally relocate that to kernel-security per the PR 2 council note).
- Do NOT log the document body, signature bytes, kernel signature event id, magic-link token (raw OR hash), or signer geolocation. Tests should assert these absences in any logged output.

---

### PR 4 — Parallel-signing support + concurrent party-state advancement + convergence test battery

**Estimated effort:** ~2h
**Scope:** flesh out parallel-workflow orchestration; concurrent party `AdvanceParty` calls converge to a deterministic final state; multi-replica audit-chain merge battery; pattern-B terminal-wins resolver test
**Commit subject:** `feat(blocks-docs-signing): parallel-signing convergence + multi-replica audit-chain merge`
**Depends on:** PR 3 merged
**Branch:** `cob/blocks-docs-signing-parallel`
**Pre-merge council:** **security-engineering spot-check** — parallel orchestration introduces race windows; council confirms the convergence rule is sound. Council scope:
  - Two parties signing concurrently (simulated 2-thread) — both succeed, both audit rows appended, final workflow state = `InProgress` (not `Completed`) until both parties' rows are visible
  - Last-party-signed transition: ONLY ONE replica wins the `CompleteWorkflowAsync` race; the other receives an `AlreadyTerminal` error and does not append a duplicate completion event
  - Audit-chain merge: two replicas each append 3 audit rows concurrently; merged chain is reproducible and `VerifyChainAsync` passes
  - Decline-during-parallel: if party A signs and party B declines concurrently, terminal-wins rule resolves to `Declined` (per security posture §6); the signed signature row stays (audit only)

#### New surface (small)

**`Services/IConcurrentDispatchCoordinator.cs`** (private — internal to the package):

```
internal interface IConcurrentDispatchCoordinator {
    Task<bool> TryClaimCompletionAsync(SigningWorkflowId workflowId, CancellationToken ct = default);
}

internal sealed class InMemoryConcurrentDispatchCoordinator : IConcurrentDispatchCoordinator {
    private readonly ConcurrentDictionary<SigningWorkflowId, bool> _claimed = new();
    public Task<bool> TryClaimCompletionAsync(SigningWorkflowId id, CancellationToken ct)
        => Task.FromResult(_claimed.TryAdd(id, true));
}
```

The coordinator is consulted inside `SignatureService.CompleteWorkflowAsync` (PR 3 change — small surgical edit): only the first caller to claim wins; others return `AlreadyTerminal`. The in-memory impl is local-only; in a multi-replica deployment the claim is implemented atomically via Loro op-log conditional-write (out of scope for this PR; doc the seam in the merger interface).

#### Multi-replica simulation harness

**`tests/MultiReplicaSimulationHarness.cs`** (test-only helper):

Spins up two `SigningWorkflowService` + `SignatureService` instances sharing nothing but a Loro-CRDT-style op buffer (simulated as an in-memory `List<SimOp>`). Each replica appends ops; the harness exposes a `Merge()` operation that applies ops in causal order to both replicas and rebuilds the audit chain per the pattern-B terminal-wins resolver.

The harness is the canonical fixture for PR 4 tests; it is NOT a production substrate — it lives in `tests/` only.

#### Tests (PR 4, ~10 tests)

**`ParallelSigningConvergenceTests.cs`**:
- `Parallel_TwoPartiesSignConcurrently_BothLocalSignaturesPersist_AndOneCompletionEventEmits`
- `Parallel_OnePartySignsOneDeclines_WorkflowResolvesToDeclined` (terminal-wins)
- `Parallel_BothPartiesAttemptComplete_OnlyOneWins` (`AlreadyTerminal` for the loser)
- `Parallel_FivePartiesConcurrent_AllSign_FinalStateCompleted` (stress test)

**`AuditChainMergeTests.cs`** (uses the harness):
- `MultiReplicaMerge_TwoReplicasEachAppendThreeRows_MergedChainVerifies`
- `MultiReplicaMerge_DocumentTamperedOnOneReplica_TamperEventVisibleAfterMerge`

**`PatternBTerminalWinsResolverTests.cs`**:
- `Resolver_VoidedBeatsCompleted_OnConcurrentTerminal`
- `Resolver_DeclinedBeatsCompleted_OnConcurrentTerminal`
- `Resolver_ExpiredBeatsInProgress_OnSweepRace`
- `Resolver_SentBeatsDraft_OnDispatchRace`

Total new tests this PR: ~10.

#### Verification

- All PR 1–3 tests still pass.
- New tests pass.
- The multi-replica harness can run with `--repeat 100` (a test parameter) and all assertions hold across 100 iterations (no flakes from race timing).
- `dotnet test --filter "FullyQualifiedName~Parallel|FullyQualifiedName~MultiReplica" --runsettings test.runsettings` passes with the harness.

#### Do NOT in this PR

- Do NOT introduce production multi-replica infrastructure. The harness is `tests/`-only.
- Do NOT modify `kernel-signatures` to expose new types. Its surface is fixed at W#21 Phase 1.

---

### PR 5 — `ISigningExpirationHandler` background sweep + magic-link token expiry handling

**Estimated effort:** ~1.5h
**Scope:** background-sweep service that transitions overdue workflows to `Expired` + audit + event emission; magic-link expiry detection on `SignatureRequest`; reminder send-rate limiting (≤ `MaxReminderCount` per request)
**Commit subject:** `feat(blocks-docs-signing): ISigningExpirationHandler background sweep + reminder cap`
**Depends on:** PR 4 merged
**Branch:** `cob/blocks-docs-signing-expiration`
**Pre-merge council:** standard self-audit; **security spot-check recommended** on the magic-link expiry path (an expired magic link must reject sign attempts at the workflow-service layer, not just at the UI layer).

#### New service

**`Services/ISigningExpirationHandler.cs`**:

```
public interface ISigningExpirationHandler {
    Task<ExpirationSweepResult> SweepAsync(Instant asOf, CancellationToken ct = default);
    Task<ReminderSweepResult> SendDueRemindersAsync(Instant asOf, ReminderCadence cadence, CancellationToken ct = default);
}

public sealed record ExpirationSweepResult(int ExpiredWorkflowCount, int FailedCount, IReadOnlyList<string> FailureDetails);
public sealed record ReminderSweepResult(int RemindersSent, int CappedCount);
public sealed record ReminderCadence(int InitialDelayDays, int IntervalDays);   // e.g. (3, 3) = first reminder 3 days post-dispatch, then every 3 days
```

#### Sweep algorithm

```
sweep(asOf):
  candidates = await workflowRepo.QueryExpiredCandidatesAsync(asOf)
  expiredCount, failureCount, failures = 0, 0, []
  for workflow in candidates:
    result = await workflowService.ExpireAsync(workflow.Id)
    if result.Error == None: expiredCount++
    else: failureCount++; failures.Add($"{workflow.Id.Value}: {result.Error}")
  return new ExpirationSweepResult(expiredCount, failureCount, failures)
```

#### Reminder algorithm

```
sendDueReminders(asOf, cadence):
  candidates = await requestRepo.QueryDueRemindersAsync(asOf, cadence)
  sent, capped = 0, 0
  for request in candidates:
    party = await partyRepo.GetByIdAsync(request.PartyId)
    if party.Status not in {Invited, Viewed}: continue          // already signed/declined → skip
    if request.ReminderCount >= options.MaxReminderCount: capped++; continue
    if request.MagicLinkExpiresAtUtc < asOf:
      // expired magic link — issue a fresh token + new request
      newToken = MagicLinkTokens.Generate()
      newHash = MagicLinkTokens.Hash(newToken)
      newRequest = request with {
        MagicLinkTokenHash: newHash,
        MagicLinkExpiresAtUtc: asOf + options.MagicLinkExpiryDays,
        ReminderCount: request.ReminderCount + 1,
        LastReminderAtUtc: asOf,
      }
      await requestRepo.UpsertAsync(newRequest)
      await notifications.SendReminderWithFreshTokenAsync(newRequest.Id, newToken)
    else:
      request = request with { ReminderCount: request.ReminderCount + 1, LastReminderAtUtc: asOf }
      await requestRepo.UpsertAsync(request)
      await notifications.SendReminderAsync(request.Id)
    sent++
    await audit.AppendAsync(request.WorkflowId, SigningAuditEvent.PartyReminded, partyId: party.Id,
      payload: { "reminderNumber": request.ReminderCount.ToString() })
  return new ReminderSweepResult(sent, capped)
```

#### Magic-link expiry enforcement

Extend `ISignatureService.CaptureAsync` (PR 3 micro-edit): at Phase 1, lookup the `SignatureRequest` for `(workflowId, partyId)`; if `MagicLinkExpiresAtUtc != null AND MagicLinkExpiresAtUtc < now`, return a new `CaptureError.MagicLinkExpired`. Audit a `AuthFailed` row with `payload: { "cause": "magic-link-expired" }`. This is a **security-spot-check item** — confirm the check fires at the service boundary, not just the UI boundary.

#### DI extension

```
public static IServiceCollection AddSunfishDocsSigningExpiration(this IServiceCollection services) {
    services.AddScoped<ISigningExpirationHandler, SigningExpirationHandler>();
    return services;
}
```

The background scheduling (cron-style invocation of `SweepAsync` + `SendDueRemindersAsync`) is the **host's responsibility** — `apps/anchor` will wire a `BackgroundService` that calls these on a configurable cadence (default: daily at 02:00 local time). This hand-off ships the service surface only; host wiring is a follow-on (and is documented in PR 7's apps/docs page).

#### Tests (PR 5, ~7 tests)

**`SigningExpirationHandlerTests.cs`**:
- `Sweep_ExpiresOnlyEligibleWorkflows` (eligible = Sent | InProgress AND ExpiresAt < now)
- `Sweep_DoesNotExpireTerminalWorkflows`
- `Sweep_AggregatesFailuresAndContinues`
- `SendReminders_SkipsCappedRequests` (at MaxReminderCount → capped++ not sent++)
- `SendReminders_RefreshesExpiredMagicLink_WithNewToken_AndAuditsReminded`
- `SendReminders_SkipsAlreadySignedOrDeclinedParties`

**`MagicLinkExpiryServiceLayerTests.cs`**:
- `Capture_RejectsExpiredMagicLink_WithMagicLinkExpiredError_AndAuditsAuthFailed`

Total new tests this PR: ~7.

#### Verification

- All PR 1–4 tests still pass.
- New tests pass.
- `grep -r "rawToken\|raw_token" packages/blocks-docs-signing/` — verify the reminder path keeps raw tokens out of logs (one assertion is a regex over the package's log output during a reminder send).

#### Do NOT in this PR

- Do NOT introduce a BackgroundService host inside the package. Host wiring belongs in `apps/anchor`.
- Do NOT introduce email/SMS provider integration. `INotificationDispatcher` is the seam (already a stub from PR 2); production wiring is a separate workstream.
- Do NOT lower MaxReminderCount under user pressure — the cap exists per ADR 0068 §GC.1 (anti-spam discipline).

---

### PR 6 — `IErpnextSigningWorkflowImporter` — legacy signed-document migration

**Estimated effort:** ~2h
**Scope:** import path for legacy ERPNext signed documents; one terminal-state `SigningWorkflow` per imported PDF; idempotency via `externalRef` field on the workflow (or a side-mapping table); audit row populated from imported metadata (signer, signed-at, ip if available)
**Commit subject:** `feat(blocks-docs-signing): IErpnextSigningWorkflowImporter — legacy signed-document migration (terminal-state shadow)`
**Depends on:** PR 5 merged
**Branch:** `cob/blocks-docs-signing-erpnext-importer`
**Pre-merge council:** standard self-audit. .NET architect optional review of the importer's idempotency-key choice.

#### New service

**`Migration/IErpnextSigningWorkflowImporter.cs`**:

```
public interface IErpnextSigningWorkflowImporter {
    Task<ImportSigningResult> ImportAsync(ErpnextSignedDocumentRecord record, CancellationToken ct = default);
}

public sealed record ErpnextSignedDocumentRecord(
    string Source,                                       // e.g. "erpnext-prod"
    string ErpnextDocId,                                 // the source document id
    DocumentId TargetDocumentId,                         // the canonical Sunfish Document already imported via blocks-docs-core importer
    DocumentVersionId TargetDocumentVersionId,
    IReadOnlyList<LegacySigningPartyRecord> Parties,
    string? TemplateRef,
    Instant SignedCompletionAtUtc,
    string? ContentHashAtImport);                        // sha256 of the doc bytes at import time; pinned for verifyTamperFree later

public sealed record LegacySigningPartyRecord(
    string RoleLabel, string SignerName, string? SignerEmail, string? SignerPartyId,
    Instant SignedAtUtc, string? IpAddress);

public sealed record ImportSigningResult(SigningWorkflowId? WorkflowId, ImportSigningError Error, string? Detail);
public enum ImportSigningError {
    None, UnknownTargetDocument, AlreadyImported, NoParties, PersistFailed,
}
```

#### Import algorithm

```
import(record):
  // Idempotency check
  existing = await workflowRepo.QueryByExternalRefAsync(record.Source, record.ErpnextDocId)
  if existing != null: return Ok(existing.Id, AlreadyImported)

  doc = await docsCore.GetDocumentByIdAsync(record.TargetDocumentId)
  if doc == null: return Err(UnknownTargetDocument)
  if record.Parties.Count < 1: return Err(NoParties)

  // Build a terminal Completed workflow
  workflowId = SigningWorkflowId.New()
  workflow = new SigningWorkflow(
    Id: workflowId, TenantId: tenant.Id,
    DocumentId: record.TargetDocumentId, DocumentVersionId: record.TargetDocumentVersionId,
    WorkflowKind: "parallel",                            // all imported records treated as if signed in parallel (order info lost)
    Status: Completed,
    InitiatedBy: tenant.SystemPartyId,                   // synthetic "system" actor
    InitiatedAtUtc: record.SignedCompletionAtUtc.AddSeconds(-1),
    CompletedAtUtc: record.SignedCompletionAtUtc,
    FinalSignedDocumentId: record.TargetDocumentId,      // for imports, target = final (no separate merge)
    TemplateId: record.TemplateRef,
    Version: 1)
  await workflowRepo.UpsertAsync(workflow)
  // (externalRef table or extra column captures (record.Source, record.ErpnextDocId) -> workflow.Id)

  for partyRecord in record.Parties:
    party = new SigningParty(
      Id: SigningPartyId.New(), TenantId: tenant.Id, WorkflowId: workflowId,
      PartyOrder: 1, RoleLabel: partyRecord.RoleLabel,
      PartyKind: partyRecord.SignerPartyId != null ? "tenant" : "external",
      PrincipalId: partyRecord.SignerPartyId?.ToPartyId(),
      ExternalName: partyRecord.SignerPartyId == null ? partyRecord.SignerName : null,
      ExternalEmail: partyRecord.SignerEmail,
      Status: Signed, InvitedAtUtc: partyRecord.SignedAtUtc, SignedAtUtc: partyRecord.SignedAtUtc,
      AuthMethod: PartyAuthMethod.InPerson)              // imported records assumed in-person
    await partyRepo.UpsertAsync(party)

    // Wet-imported signature record
    sig = new LocalSignature(
      Id: LocalSignatureId.New(), TenantId: tenant.Id,
      WorkflowId: workflowId, StepId: SigningStepId.Synthetic(),   // synthetic placeholder
      PartyId: party.Id,
      SignatureKind: SignatureKind.WetImported,
      ImageRef: null, KernelSignatureEventId: null,
      SignedAtUtc: partyRecord.SignedAtUtc, SignedFromIp: partyRecord.IpAddress,
      DocumentVersionAtSignId: record.TargetDocumentVersionId,
      ContentHashAtSign: record.ContentHashAtImport ?? "0000...0000")  // synthetic zero hash if not provided
    await signatureRepo.AppendAsync(sig)

  // Audit row (synthetic chain — single completion entry)
  await audit.AppendAsync(workflowId, SigningAuditEvent.WorkflowCompleted, partyId: null,
    payload: { "source": record.Source, "erpnextDocId": record.ErpnextDocId, "importedAt": now.ToString("O") })

  return Ok(workflowId)
```

#### DI extension

```
public static IServiceCollection AddSunfishDocsSigningMigration(this IServiceCollection services) {
    services.AddScoped<IErpnextSigningWorkflowImporter, ErpnextSigningWorkflowImporter>();
    return services;
}
```

#### Tests (PR 6, ~6 tests)

- `Import_PersistsTerminalWorkflowWithCompletedStatus`
- `Import_IdempotentOnDuplicateErpnextDocId` (second call returns `AlreadyImported`)
- `Import_PartiesPersistedAsSignedTerminal`
- `Import_RejectsMissingTargetDocument`
- `Import_RejectsEmptyParties`
- `Import_PopulatesAuditRowWithSourceMetadata`

Total new tests this PR: ~6.

#### Verification

- All PR 1–5 tests pass.
- New tests pass.
- Integration test: import 3 ERPNext records pointing at 3 distinct documents → 3 workflows persist, each terminal `Completed`, each with one `LocalSignature` per party, idempotent on re-run.

#### Do NOT in this PR

- Do NOT call `kernel-signatures.ISignatureCapture` from the importer. Legacy records are wet-signed; no crypto path applies.
- Do NOT populate `ContentHashAtImport` if the ERPNext source doesn't have it — store zero-hash and document the limitation in the importer's XML doc.
- Do NOT attempt to backfill audit-chain integrity for imported records — chain starts at the import event row.

---

### PR 7 — DI umbrella `AddBlocksDocsSigning()` + `apps/docs/blocks/docs-signing/overview.md` + ledger flip W#73 → built

**Estimated effort:** ~1h
**Scope:** umbrella DI extension wiring all five sub-`AddXxx` methods; documentation page per pattern-006; ledger flip
**Commit subject:** `chore(blocks-docs-signing): DI umbrella + apps/docs overview + ledger flip W#73 built`
**Depends on:** PR 6 merged
**Branch:** `cob/blocks-docs-signing-umbrella-docs`
**Pre-merge council:** standard self-audit.

#### Umbrella DI extension

```
public static IServiceCollection AddBlocksDocsSigning(
    this IServiceCollection services,
    Action<BlocksDocsSigningOptions>? configure = null)
{
    services.AddSunfishDocsSigningSeed(configure);
    services.AddSunfishDocsSigningWorkflows();
    services.AddSunfishDocsSigningCapture();
    services.AddSunfishDocsSigningExpiration();
    services.AddSunfishDocsSigningMigration();
    return services;
}
```

Per pattern-005, this is the only public entry point app developers call. Consumers expect:

```csharp
services.AddSunfishKernelSignatures();        // prerequisite (verify at runtime via assembly probe)
services.AddBlocksDocsCore();                 // prerequisite
services.AddBlocksDocsSigning(opts => {
    opts.WorkflowDefaultExpiryDays = 30;
    opts.MagicLinkExpiryDays = 7;
    opts.MaxReminderCount = 5;
});
```

#### apps/docs page per pattern-006

`apps/docs/blocks/docs-signing/overview.md` — sections:

1. **Overview** — what the package does; what it doesn't (no external e-sign providers; no native crypto; no PDF merge); link to ADR 0054 + 0088.
2. **Entities** — table of the 6 entities + brief description; link back to Stage 02 §3.5.
3. **DI registration** — copy-paste-ready DI snippet.
4. **`ISigningWorkflowService` API surface** — every public method with parameter shape + return shape; do NOT inline state-machine diagrams (link to Stage 02 §3.5.1 for the diagram).
5. **`ISignatureService` API surface** — same shape.
6. **Audit chain semantics** — short explanation of hash-chain + tamper-detection; link to `VerifyChainAsync` API.
7. **Security posture** — bulleted list from the hand-off §"Security posture (binding)"; pointer to ADR 0068 §GC.1.
8. **Cross-cluster events emitted** — table of event types + idempotency keys.
9. **Host wiring** — sample `BackgroundService` snippet for invoking `SweepAsync` daily.
10. **Limitations + roadmap** — no external providers (compat-adapter family is separate); no PDF embedding (follow-on); hybrid kind dropped per §9 Q7; KBA stub per §9 Q3.
11. **Related packages** — `kernel-signatures`, `kernel-security`, `kernel-audit`, `blocks-docs-core`, `blocks-docs-templates`, `blocks-people-foundation`.

Add `apps/docs/blocks/docs-signing/toc.yml` entry per pattern-010.

#### Ledger flip

Per `feedback_never_add_workstream_rows_directly_to_ledger.md`: create `icm/_state/workstreams/W73-blocks-docs-signing.md` source file first (if not already present); ensure `Status: built` line; run `tools/icm/render-ledger.py` to regenerate `active-workstreams.md`. The PR body lists all 7 PR numbers + total test count (~71 across PRs 1–6).

#### Tests (PR 7, ~3 tests)

- `AddBlocksDocsSigning_RegistersAllServices` (DI sanity check — resolve each interface)
- `AddBlocksDocsSigning_OptionsBindCorrectly` (configurable via `configure` action)
- `AddBlocksDocsSigning_DoubleRegistration_DoesNotDuplicateOrThrow` (idempotent registration)

#### Verification

- `dotnet build` succeeds.
- All ~71 tests pass.
- `apps/docs` doc renders cleanly.
- The ledger ascii rendering shows W#73 = built.

#### Do NOT in this PR

- Do NOT edit `active-workstreams.md` directly. Source file + render only.
- Do NOT add architecture commentary to the apps/docs page beyond the security posture summary — that page is consumer-facing, not design-rationale-facing.

---

## CRDT-friendly schema conventions applied

Per `_shared/engineering/crdt-friendly-schema-conventions.md` (binding):

### 1. Posted-then-immutable SigningWorkflow

Once `SigningWorkflow.Status != Draft`, the workflow header is immutable. Status transitions (per §3.5.1 state machine) are allowed; field mutations (DocumentId, WorkflowKind, etc.) are not. Enforced at the repository layer: `UpsertAsync` checks the existing row's `Status` and rejects any field-diff beyond status + completion-related fields.

### 2. Draft-stage mutability

In `Status = Draft`, the workflow + parties + steps are freely editable (the workflow author may revise the structure before Dispatch). After `Dispatch` (transition to `Sent`), the structure is frozen.

### 3. Append-only LocalSignature + SigningAuditLog

Neither entity supports updates or deletes. `ILocalSignatureRepository.AppendAsync` + `ISigningAuditLogRepository.AppendAsync` are the only mutation paths; both reject any caller attempting to update an existing row.

### 4. Hash-chain integrity post-merge

The audit chain rebuilds deterministically across replicas via the harness in PR 4. Convergence rule: per-workflow chain, ordered by `OccurredAtUtc` then by tiebreaker `(replicaId, sequenceWithinReplica)`. Each row's `PrevEntryHashChain` references the post-merge prior row. PR 3's `VerifyChainAsync` is the canonical verifier; called by the Tier-2 post-merge reconciler.

### 5. State-machine-aware merge — Pattern B (terminal-wins)

`SigningWorkflowStatusResolver` (registered with kernel-crdt) resolves divergent workflow status. Terminal precedence: `Voided > Declined > Expired > Completed > InProgress > Sent > Draft`. Designated-authority IS used for the `Completed` transition (only the replica that ran the merge may set `Completed`; others observe via op-log).

### 6. Tier-2 post-merge validation

The post-merge reconciler verifies:
1. Audit-chain integrity (every workflow's `VerifyChainAsync` returns Valid).
2. Kernel-signature FK existence (every `LocalSignature.KernelSignatureEventId` resolves in `kernel-signatures`).
3. Step-completion vs party-signed consistency (every signed party has exactly one `LocalSignature` per assigned signature/initial step).

Reconciler emits `SigningWorkflow.AuditChainBroken` event on violation. PR 4 ships the reconciler stub; production wiring is a follow-on.

---

## Event-bus catalog applied

Per `_shared/engineering/cross-cluster-event-bus-design.md` §3.4 (Docs events):

### Emitted (producer: `docs`)

| Event | Consumers | Payload | Idempotency key |
|---|---|---|---|
| `Docs.SigningWorkflowDispatched` | (internal) | `{ workflowId, documentId, partyCount }` | `signing-dispatched:{workflowId}` |
| `Docs.SigningWorkflowCompleted` | work | `{ workflowId, finalDocumentId }` | `signing-completed:{workflowId}` |
| `Docs.SigningWorkflowVoided` | (internal) | `{ workflowId, reason }` | `signing-voided:{workflowId}` |
| `Docs.SigningWorkflowExpired` | (internal) | `{ workflowId }` | `signing-expired:{workflowId}` |
| `Docs.SigningPartyDeclined` | (internal) | `{ workflowId, partyId, reason }` | `signing-declined:{workflowId}:{partyId}` |
| `Docs.DocumentSigned` | work, financial | `{ documentId, contractId?, signerPartyId, signedAt }` | `document-signed:{documentId}:{signerPartyId}` |
| `Docs.ContractFullySigned` | work, financial | `{ contractInstanceId, finalSignedDocumentId, fullySignedAt }` | `contract-fully-signed:{contractInstanceId}` |

### Consumed

This package consumes NO cross-cluster events in v1. Future intake (probably with `compat-docusign`): consume external-provider webhook events (DocuSign Connect, HelloSign webhook) and translate into local `AdvancePartyAsync` calls.

### Schema versioning

All event records carry `SchemaVersion = 1`. Per `cross-cluster-event-bus-design.md` §8 (within an event type), adding optional fields is non-breaking; removing or retyping fields requires a schema-epoch bump + parallel emission for one release window.

### Envelope construction

Per `cross-cluster-event-bus-design.md` §1.1 — every event ships in a `CrossClusterEventEnvelope { eventId, eventType, schemaVersion, occurredAtUtc, tenantId, sourceReplicaId, idempotencyKey, payload }`. Local stub in PR 3 wraps the raw record; canonical envelope wiring lands when the bus package ships.

---

## Idempotency-key catalog

Every cross-cluster operation in this hand-off has a deterministic idempotency key for safe at-least-once delivery:

| Operation | Idempotency key | Used by |
|---|---|---|
| `SigningWorkflow.Dispatch` | `signing-dispatched:{workflowId}` | event consumers; duplicate dispatch attempts return existing state |
| `SigningWorkflow.Complete` | `signing-completed:{workflowId}` | concurrent dispatch coordinator; only first claim wins |
| `LocalSignature.Append` | `local-signature:{workflowId}:{stepId}:{partyId}` | append-only repo rejects duplicates with this composite key |
| `SignatureRequest.Send` | `signature-request:{workflowId}:{partyId}:{attempt}` | reminder service uses `attempt` = `ReminderCount` for uniqueness |
| `Erpnext.ImportSigning` | `(source, erpnextDocId)` | importer checks existing workflows; second call returns `AlreadyImported` |
| `SigningAuditLog.Append` | `audit:{workflowId}:{sequenceWithinChain}` | append-only repo + chain coordinator |

---

## License posture

### Borrowed-with-attribution (permissive)

None. This cluster is clean-room; no permissive sources were consulted for entity shapes. Stage 02 §3.5 was authored from textbook fundamentals + ADR 0054 + public-documented behavior of Documenso + OpenSign (study-only, no source consulted).

### Clean-room only (copyleft)

- **Documenso** (GPLv3 + Enterprise) — surface observation only; no source paste; no comment paste; no schema paste. Sunfish staff designing this cluster MUST work in a separate worktree if reading Documenso source for any reason, per ADR 0088 §3.2.
- **OpenSign** (AGPLv3) — same discipline as Documenso.

### IETF normative references

- **RFC 3161** (Time-Stamp Protocol) — referenced via `kernel-signatures` only.
- **RFC 5652** (Cryptographic Message Syntax / PKCS#7) — referenced via `kernel-signatures` only.
- **RFC 7515** (JSON Web Signature) — referenced via `kernel-signatures` only.

This package does NOT paraphrase or implement RFC text directly.

### Sunfish output

All package contents are MIT-licensed per ADR 0088 §2. `NOTICE.md` references ADR 0054 + 0088 + 0068 but carries no third-party attribution (clean-room).

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Test count | Critical assertions |
|---|---|---|
| PR 1 (scaffold) | ~14 | Entity invariants; status-transition tables; repo round-trip; tenant isolation; append-only audit + signature repos |
| PR 2 (workflow service) | ~16 | State-machine completeness; sequential vs parallel dispatch; magic-link token-hash-only-at-rest; audit-on-every-transition; decline = terminal |
| PR 3 (signature service) | ~18 | kernel-signatures call wiring; ContentHashAtSign pinning; tamper-detection; audit-chain construction; Docs.DocumentSigned emission; passthrough merge |
| PR 4 (parallel) | ~10 | Concurrent party signing converges; terminal-wins resolver; multi-replica audit-chain merge; completion-claim race |
| PR 5 (expiration) | ~7 | Sweep + reminder cap; magic-link expiry rejection at service layer; auth-failed audit on expired link |
| PR 6 (importer) | ~6 | Terminal-state workflow shadow; idempotency by erpnextDocId; per-party Signed terminal |
| PR 7 (umbrella + docs) | ~3 | DI registration sanity; options binding |
| **Total** | **~74** | |

### Cluster-level acceptance (PASS gate at end of PR 7)

**Invariant battery (non-negotiable):**

1. **Signature non-repudiation:** every `LocalSignature` with `SignatureKind == Cryptographic` resolves to a `kernel-signatures.SignatureEvent` with a non-empty `Envelope` AND a valid `Consent` AND a matching `DocumentHash`.
2. **State-machine completeness:** every status in `SigningWorkflowStatus` is reachable from `Draft` via valid transitions; every terminal state is unreachable from any other terminal state.
3. **Tamper-evidence:** for any persisted `LocalSignature`, calling `VerifyTamperFreeAsync` returns `TamperFree = true` when the document is unchanged; returns `TamperFree = false` with both hashes when the document body is mutated post-sign.
4. **Audit-chain integrity:** `VerifyChainAsync` on any workflow with N >= 1 audit rows returns `Valid = true`; if any row is mutated (test seam), returns `Valid = false` with the correct first-broken index.
5. **Magic-link discipline:** a recorded packet-capture (test-only logging fixture) of all notification-dispatch calls contains zero raw-token strings AND every `SignatureRequest.MagicLinkTokenHash` is 64-char sha256 hex.
6. **Tenant isolation:** repeating every PR 1 + 2 + 3 + 5 + 6 test under a `Tenant B` context against `Tenant A`-owned workflows produces `UnknownXxx` errors on every read AND every write.
7. **Parallel convergence:** running PR 4's multi-replica harness 100x produces deterministic final state + verifiable audit chain on every run.
8. **Sequential ordering:** for a sequential workflow, party 2 cannot sign before party 1; the `PartyOutOfOrder` error fires + an audit row is appended.
9. **Decline = terminal:** one party decline transitions the workflow to terminal `Declined`; no resurrection path exists at the service or repository layer.
10. **Import idempotency:** running the ERPNext importer twice on the same `(source, erpnextDocId)` produces exactly one workflow.

**Performance acceptance:**

- 50-workflow expiration sweep completes in < 250ms on a developer laptop (CI tolerance 500ms).
- `VerifyChainAsync` on a 100-row audit chain completes in < 100ms.
- 10-party parallel signing (PR 4 stress test) completes (all 10 captures + completion) in < 1s.

### End-to-end demo wiring (PASS gate addendum)

The 4-LLC Phase 2 leasing-pipeline acceptance demo (per `_shared/product/local-node-architecture-paper.md` §13) requires an end-to-end signed-lease path. PR 7 must include a smoke-test (in `tests/E2E/`) that:

1. Creates a `Document` of type `contract-instance` (using `blocks-docs-core.IDocumentCommandService`).
2. Calls `ISigningWorkflowService.StartAsync` with 2 parties (landlord + tenant) + 2 steps (one signature per party) + `workflowKind = "sequential"`.
3. Calls `DispatchAsync` → verifies first party invited.
4. Calls `AdvancePartyAsync(party1, Viewed)` then `ISignatureService.CaptureAsync(party1, drawn)`.
5. Verifies second party invited via the sequential-dispatch helper.
6. Calls `AdvancePartyAsync(party2, Viewed)` then `CaptureAsync(party2, drawn)`.
7. Asserts the workflow status is `Completed`, `FinalSignedDocumentId` is set, `VerifyChainAsync` returns Valid, and `Docs.SigningWorkflowCompleted` + `Docs.ContractFullySigned` (since templateId is set) events fired exactly once each.

---

## Halt conditions (cob-question-* beacons)

Stop and file a `cob-question-*` beacon under `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/` for any of the following:

### 1. `kernel-signatures` shape changed since W#21 Phase 1 ship

If `ISignatureCapture.CaptureAsync(SignatureCaptureRequest, ct)` no longer returns `Task<SignatureEvent>`, or `SignatureCaptureRequest` requires different fields than documented in `kernel-signatures/Services/ISignatureCapture.cs`, **STOP**. The PR 3 hand-off is keyed to W#21 Phase 1 surface. File `cob-question-*` for an XO ruling on whether to (a) adapt this hand-off or (b) defer and intake a `kernel-signatures` surface stabilization.

### 2. `kernel-security.computeContentHash` not exposed

If `kernel-security` does not expose a `ComputeContentHashAsync(ReadOnlyMemory<byte>, CancellationToken) -> Task<string>` method (or the equivalent string-input variant), the PR 2 magic-link token-hashing helper falls back to direct `System.Security.Cryptography.SHA256.HashData(...)`. Council scope expands to confirm this is acceptable. If council rejects: file `cob-question-*` for an XO ruling on whether to (a) add the method to `kernel-security` (separate workstream) or (b) accept the bounded direct-SHA256 use in `MagicLinkTokens.cs`.

### 3. `blocks-people-foundation` not on main (PR 1)

If `blocks-people-foundation` is absent at PR 1 author time, the `PartyId` strong-id type relocates locally as `SignerPartyId` and `SigningParty.PrincipalId` becomes `SignerPartyId?`. Note this in PR 1 description; relocate the type when `blocks-people-foundation` lands (single `using` directive update).

### 4. `blocks-docs-templates` not on main (PR 7)

If `blocks-docs-templates` is absent at PR 7 author time, `SigningWorkflow.TemplateId` stays `string?` (opaque); `Docs.ContractFullySigned` event emission is gated on `TemplateId != null` (works regardless of typed-templateId). Update the apps/docs page note to reflect the deferral.

### 5. Cross-cluster event bus package home (PRs 3, 5)

If the canonical event-bus package isn't decided yet, ship local event types + `ISigningEventPublisher` interface + `InMemorySigningEventPublisher` impl. Note the deferral in PR 3 description. When the bus package lands, the types + publisher relocate; consumer-side `using` directive update.

### 6. Loro op-log append-only constraint surfaces (any PR)

If during PR 1 (entity invariants), PR 3 (audit-chain construction), or PR 4 (parallel convergence) you hit a Loro op-log question (e.g. "can the audit log be a Loro list with mutation-rejecting validation?"), STOP and file `cob-question-*`. Do not attempt to wire Loro op-log integration in this hand-off; in-memory implementations are the v1 ceiling.

### 7. Documenso / OpenSign source reading temptation (any PR)

If a council reviewer or contributor proposes reading Documenso or OpenSign source to "verify our state machine is complete" or "see how they handle X", STOP. Per ADR 0088 §3.2 + the License posture section above, reading copyleft source is forbidden in this worktree. Acceptable: read their public docs + UX walkthroughs + RFC references. Unacceptable: reading their source. File `cob-question-*` if the temptation is strong enough to require explicit XO ruling.

### 8. PDF merge / final-doc rendering expectations expand (PR 3)

If a council reviewer or downstream consumer demands true multi-signature PDF embedding (signature image placed at the step's positional coordinates within the source PDF) in v1, this is **out of scope**. The passthrough merger is the canonical v1 implementation. File `cob-question-*` for XO ruling if pressure persists; rendering is a follow-on (`blocks-reports-signing-render` workstream).

### 9. External provider compat-adapter scoping (any PR)

If a consumer asks "where is the DocuSign/HelloSign/Adobe-Sign integration?", the answer is: **a separate compat-adapter workstream**. This hand-off ships the canonical `ISigningWorkflowService` contract; external providers replace the in-app capture leg by implementing a parallel `IExternalProviderCaptureAdapter` and routing webhook events into `AdvancePartyAsync`. Filing intakes for `compat-docusign`, `compat-hellosign`, `compat-adobe-sign` is XO's responsibility AFTER this hand-off lands. **Open question to surface in the build report:** does CO want a feature-gating mechanism (e.g. `SigningProvider.Native` vs `SigningProvider.DocuSign` enum on `SigningWorkflow`) reserved in v1 schema for forward-compat? Recommend YES — reserve a nullable `ExternalProviderKey` field on `SigningWorkflow` (opaque string; null = native) to avoid a schema-epoch bump when compat-adapters land.

### 10. KBA / IDV vendor integration pressure (PR 2)

If a contributor proposes wiring a real KBA vendor (LexisNexis, Experian, IDology) in v1, that's a separate workstream per §9 Q3. v1 ships the `Kba` stub + `NoOpKbaVerifier` returning `KbaVerificationResult.NotConfigured`. File `cob-question-*` if the demand is for v1-blocking.

### 11. PolicyAcknowledgment.signatureId FK consumer conflict (PR 7)

`blocks-docs-wiki`'s `PolicyAcknowledgment.signatureId` references `Signature.id`. This hand-off ships the type as `LocalSignature` (renamed to avoid collision with `kernel-signatures.SignatureEvent`). If `blocks-docs-wiki` was already authored against the name `Signature`, file `cob-question-*` for an XO ruling on the canonical name. Recommend: `LocalSignatureId` is the binding name; wiki updates its FK type.

---

## PASS gate (end-state for declaring this hand-off `built`)

All of the following must be true for COB to flip `W#73` to `built` in `active-workstreams.md`:

1. **All 7 PRs merged** to `main` with green CI.
2. **~74 tests passing** (per the per-PR minima table above).
3. **All 10 cluster-level invariants** (test plan §"Cluster-level acceptance") asserted by automated tests.
4. **End-to-end demo smoke-test** (test plan §"End-to-end demo wiring") passes in `tests/E2E/`.
5. **Council approvals on file** for:
   - PR 1 (.NET architect APPROVED)
   - PR 2 (security-engineering APPROVED — state machine + magic-link + tenant scope)
   - PR 3 (security-engineering APPROVED — kernel-signatures wiring + content-hash binding + audit chain + tamper-detection; .NET architect APPROVED recommended)
   - PR 4 (security-engineering APPROVED — concurrent-state convergence + terminal-wins resolver)
6. **No `System.Security.Cryptography` imports** outside `MagicLinkTokens.cs` (or, per Halt §2, no direct crypto at all if `kernel-security.computeContentHash` accepts the helper).
7. **No raw magic-link tokens** in any logged output, exception message, audit-log payload, or persisted row across the full test battery.
8. **`apps/docs/blocks/docs-signing/overview.md`** published with `toc.yml` entry.
9. **`active-workstreams.md`** rendered showing `W#73 = built` (source file updated; renderer run; ledger checked in).
10. **Build report** posted via `cob-status-*` beacon to `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/` summarizing: PR count, test count, council approvals, open follow-ons (PDF rendering; external compat-adapters; KBA real vendor; SQLite persistence).

---

## Docs

The canonical `apps/docs` page for this package is `apps/docs/blocks/docs-signing/overview.md` (PR 7).

Beyond the in-package doc, three doc artifacts may be relevant for downstream readers:

1. **Stage 02 design source:** `icm/02_architecture/blocks-docs-schema-design.md` §3.5 — the schema-of-record; this hand-off implements that spec.
2. **ADR 0054** — electronic signature capture + document binding — the design rationale for the `kernel-signatures` substrate this package consumes.
3. **`cross-cluster-event-bus-design.md` §3.4** — the canonical `Docs.*` event catalog.

PR 7's overview page links all three.

---

## Overview (one-page summary for the PR descriptions)

`blocks-docs-signing` is the Phase 2 e-signature lifecycle package. It ships 6 entities (`SigningWorkflow`, `SigningStep`, `SigningParty`, `SignatureRequest`, `LocalSignature`, `SigningAuditLog`), 3 services (`ISigningWorkflowService`, `ISignatureService`, `ISigningExpirationHandler`), one migration importer, and a DI umbrella, totaling ~74 tests across 7 PRs.

The package is **security-critical** and clean-room from Documenso + OpenSign surface observation. All crypto delegates to `kernel-signatures` (W#21); all hashing delegates to `kernel-security` (where possible — see Halt §2). The audit log is append-only + hash-chained. Tamper-detection is built-in via content-hash binding at sign time.

What ships in v1: self-hosted signing path (sequential + parallel workflows); magic-link delivery (email/SMS); legacy ERPNext signed-doc importer; full audit chain. What doesn't: DocuSign/HelloSign/Adobe-Sign external integration (separate `compat-*` workstream family); native iOS PencilKit (W#23 territory); KBA real vendor (post-MVP); true PDF embedding (follow-on `blocks-reports-signing-render`); SQLite persistence (follow-on); the `hybrid` workflow kind (dropped per §9 Q7).

Pre-merge councils: .NET architect MANDATORY on PR 1; security-engineering MANDATORY on PRs 2 + 3; security-engineering spot-check on PR 4. Council protocol per `feedback_pr_automerge_before_amendment_landed.md` — every council-mandatory PR opens as `--draft`; auto-merge enables only after all amendments push and all council subagents return APPROVED.

---

## Naming

- Package name: `blocks-docs-signing` (matches Stage 02 §1; no collision per Pre-build §12).
- Namespace root: `Sunfish.Blocks.DocsSigning`.
- Csproj: `Sunfish.Blocks.DocsSigning.csproj`.
- Workstream ID: `W#73`.
- DI umbrella: `AddBlocksDocsSigning(...)`.

Naming follows `feedback_naming_discipline_check_before_propose.md`; the `blocks-docs-*` cluster prefix matches the sibling `blocks-docs-core` + `blocks-docs-templates` + `blocks-docs-wiki` + `blocks-docs-dam` packages.

---

## Quickstart (for the consumer; lifted into the apps/docs page)

```csharp
// Program.cs / DI setup
services.AddSunfishKernelSignatures();             // prerequisite (W#21 ship)
services.AddBlocksDocsCore();                      // prerequisite
services.AddBlocksDocsSigning(opts => {
    opts.WorkflowDefaultExpiryDays = 30;
    opts.MagicLinkExpiryDays = 7;
    opts.MaxReminderCount = 5;
});

// Usage
var workflowService = sp.GetRequiredService<ISigningWorkflowService>();
var signatureService = sp.GetRequiredService<ISignatureService>();

// Start
var start = await workflowService.StartAsync(new StartCommand(
    DocumentId: leaseDocId, DocumentVersionId: leaseVersionId,
    WorkflowKind: "sequential",
    Parties: new[] {
        new SigningPartySpec(1, "Landlord", "employee", landlordPartyId, null, null, PartyAuthMethod.MagicLink),
        new SigningPartySpec(2, "Tenant",   "tenant",   tenantPartyId,   null, null, PartyAuthMethod.MagicLink),
    },
    Steps: new[] {
        new SigningStepSpec(1, SigningStepKind.Signature, PageNumber: 5, 0.1m, 0.8m, 0.3m, 0.05m, true, 1),
        new SigningStepSpec(2, SigningStepKind.Signature, PageNumber: 5, 0.6m, 0.8m, 0.3m, 0.05m, true, 2),
    },
    InitiatedBy: currentPartyId, ExpiresAtUtc: null, TemplateId: null));

// Dispatch — invites first party (sequential)
await workflowService.DispatchAsync(start.Workflow!.Id);

// Later — first party signs
await workflowService.AdvancePartyAsync(new AdvancePartyCommand(
    start.Workflow.Id, partyId1, SigningPartyStatus.Viewed, null, "10.0.0.1", "Mozilla/..."));
await signatureService.CaptureAsync(new CaptureCommand(
    start.Workflow.Id, stepId1, partyId1, SignatureKind.Drawn, "blob:drawn-1", consentId,
    null, captureQuality, null, null, null, "10.0.0.1", "Mozilla/...", null));

// (sequential — party 2 auto-invited; same shape for party 2)

// On completion, Docs.SigningWorkflowCompleted + Docs.ContractFullySigned events fire
```

---

## Algorithms

The canonical algorithm references are inline in each PR section above. Cross-referenced to Stage 02 §5.3 (signing workflow pseudocode) + §6.5 (tamper-evidence) + §3.5.1 (state machine) + ADR 0054 §A1/A2/A7 (canonicalization + algorithm agility + scope taxonomy).

---

## Related

- **Predecessor:** `blocks-docs-core` (Document substrate); `blocks-docs-templates` (contract templates — predecessor for `ContractFullySigned` event emission).
- **Substrate:** `kernel-signatures` (W#21); `kernel-security` (content hashing); `kernel-audit` (cross-cluster audit constants — referenced for sibling integration; this package emits its own audit log scoped to workflow events, not crypto events).
- **Consumers:** `blocks-property-leases` (LeaseExecuted gated on SigningWorkflowCompleted); `blocks-work-contracts` (Contract.status flip on ContractFullySigned); `blocks-people-onboarding` (employment-contract execution paths); `blocks-docs-wiki` (PolicyAcknowledgment.signatureId references LocalSignature.Id).
- **Future workstreams** (NOT in this hand-off):
  - `blocks-reports-signing-render` — PDF embedding of signatures at step positions
  - `compat-docusign`, `compat-hellosign`, `compat-adobe-sign` — external e-signing provider compat adapters (each a separate intake; consume webhooks; route into `AdvancePartyAsync`)
  - `blocks-docs-signing-persistence` — SQLite-backed repositories
  - `blocks-docs-signing-kba` — real KBA / IDV vendor integration (post-MVP)
  - `blocks-docs-signing-recovery` — admin tools for chain repair on detected tamper events

---

## Cited-symbol verification

Per `feedback_council_can_miss_spot_check_negative_existence.md` — every symbol cited in this hand-off was verified at author time:

- `kernel-signatures.ISignatureCapture.CaptureAsync(SignatureCaptureRequest, CancellationToken) → Task<SignatureEvent>` — confirmed at `packages/kernel-signatures/Services/ISignatureCapture.cs` lines 11–23.
- `SignatureCaptureRequest` required fields (`Tenant`, `Signer`, `Consent`, `DocumentHash`, `Scope`, `Envelope`, `Quality`) — confirmed at `packages/kernel-signatures/Services/ISignatureCapture.cs` lines 26–57.
- `SignatureEventId` shape (`readonly record struct (Guid Value)`) — confirmed at `packages/kernel-signatures/Models/Identifiers.cs` line 4.
- `SignatureEvent.Id` (`SignatureEventId`) — confirmed at `packages/kernel-signatures/Models/SignatureEvent.cs` line 37.
- `kernel-audit.AuditEventType.SignatureCaptured` (and siblings) — confirmed per `packages/kernel-signatures/README.md` §"Audit emission".
- `Sunfish.Foundation.Crypto.SignatureEnvelope` — referenced in `SignatureCaptureRequest.Envelope` per ISignatureCapture.cs line 44 (algorithm-agility envelope per ADR 0054 §A2).
- `Sunfish.Foundation.Taxonomy.Models.TaxonomyClassification` — referenced in `SignatureCaptureRequest.Scope` per ISignatureCapture.cs line 41 (per ADR 0054 §A7 + W#31).
- `blocks-docs-core.Document` + `DocumentVersion` + `IDocumentRepository` + `IDocumentCommandService` — confirmed via `icm/_state/handoffs/blocks-docs-core-stage06-handoff.md` lines 86–250.
- `cross-cluster-event-bus-design.md §3.4 Docs events catalog` — confirmed at `_shared/engineering/cross-cluster-event-bus-design.md` lines 278–290.

If any of the above is found to be stale at PR-author time, file `cob-question-*` per Halt §1.

---

## Cohort discipline

This hand-off is the third in the `blocks-docs-*` cluster cohort (after `blocks-docs-core` + `blocks-docs-templates`; parallel with `blocks-docs-wiki` + `blocks-docs-dam`). Cohort discipline:

- **One package boundary per cohort PR.** No accidental edits to sibling packages.
- **Sibling-package dep additions require sibling hand-off coordination.** If PR 3 needs a new type from `blocks-docs-core`, file `cob-question-*` first.
- **Cross-cluster events emitted by this package may NOT be consumed by sibling packages in the same cohort PR.** Consumers land in separate workstreams.

---

## Beacon protocol

- **Build complete:** `cob-status-2026-MM-DDTHH-MMZ-w73-built.md` to `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`. Body: PR count + test count + council approvals + open follow-ons (the 5 future workstreams listed in §Related).
- **Halt:** `cob-question-2026-MM-DDTHH-MMZ-w73-<topic>.md` per the 11 Halt conditions above.
- **Idle (after PR 7 merges):** if no follow-on work in queue, `cob-idle-*` per `feedback_use_inbox_not_status_reports.md` + `ScheduleWakeup 1800s`.

---

## Cross-references

- Stage 02 design source: [`icm/02_architecture/blocks-docs-schema-design.md`](../../02_architecture/blocks-docs-schema-design.md) (§3.5 in full; §4 diagram; §5.3 pseudocode; §6.5 tamper-evidence; §7.3 kernel-signatures contract; §9 Q3 + Q7)
- ADR 0088 Path II: [`docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md)
- ADR 0054 electronic-signature-capture: [`docs/adrs/0054-electronic-signature-capture-and-document-binding.md`](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md)
- ADR 0068 tenant security policy: [`docs/adrs/0068-tenant-security-policy.md`](../../../docs/adrs/0068-tenant-security-policy.md)
- kernel-signatures package: [`packages/kernel-signatures/`](../../../packages/kernel-signatures/) (W#21 Phase 1 ship)
- Sibling hand-off: [`icm/_state/handoffs/blocks-docs-core-stage06-handoff.md`](blocks-docs-core-stage06-handoff.md)
- Sibling hand-off: [`icm/_state/handoffs/blocks-docs-stage06-handoff.md`](blocks-docs-stage06-handoff.md) (attachment substrate; security-engineering MANDATORY pattern reference)
- CRDT conventions: [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md)
- Cross-cluster event bus design: [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) (§3.4 Docs events)
- Standing approved patterns: [`_shared/engineering/standing-approved-patterns.md`](../../../_shared/engineering/standing-approved-patterns.md) (pattern-001, pattern-003, pattern-005, pattern-006)
- Council-before-merge protocol: feedback notes `council_before_automerge` + `pr_automerge_before_amendment_landed` + `council_can_miss_spot_check_negative_existence` (auto-loaded via project memory)

**End of hand-off for `blocks-docs-signing` (W#73).**
