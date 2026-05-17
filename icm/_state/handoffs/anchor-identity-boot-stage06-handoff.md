# Hand-off — `anchor-identity-boot` — Anchor first-launch identity bootstrap + recovery contact registration (Path II clean-room substrate)

**From:** XO (research session)
**To:** dev OR dev-win session (work crosses C# Bridge service-layer + React frontend + optional Tauri Rust glue; see Pivot Note §"Owner")
**Created:** 2026-05-17 (MAUI shell target) — **pivoted 2026-05-17T14-30Z to Tauri/React shell target** (see Pivot Note below the frontmatter)
**Status:** `ready-to-build` (PR 1 only — see Gate conditions below; gate §G3 now BINDING, no longer informational, because the canonical shell is Tauri)
**Workstream:** Path II Anchor substrate, identity slice (companion to W#60 P3 SQLite + W#60 P4 Tauri Stronghold + W#67 social recovery delivery)
**Spec sources:**
- [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) — Anchor as the all-in-one local-first runtime (Proposed, ratified by CO 2026-05-16)
- [ADR 0046-A6 — Social Recovery Seed-Delivery Protocol](../../../docs/adrs/0046-a6-social-recovery-seed-delivery-protocol.md) (Accepted 2026-05-16)
- [W#67 hand-off — G6-A social recovery seed-delivery substrate](./w67-g6a-social-recovery-seed-delivery-protocol-stage06-handoff.md) (composes; see Gate conditions §G2)
- [W#60 P4 hand-off — Collaboration + Stronghold](./w60-collaboration-phase4-stage06-handoff.md) (PR 1 — Tauri Stronghold; see Gate conditions §G3)
- [`anchor-recovery-host-integration` hand-off](./anchor-recovery-host-integration-stage06-handoff.md) (sibling — wires `IRecoveryCoordinator` into the Anchor MAUI host; this hand-off composes on top by adding the first-launch surface)
- [`blocks-people-foundation` hand-off](./blocks-people-foundation-stage06-handoff.md) (PartyId + IPartyReadModel substrate; recovery contacts compose against `Party` identities)
- [`party-model-convention.md`](../../../_shared/engineering/party-model-convention.md) §2 (Party shape), §4 (cross-cluster references)
- [`standing-approved-patterns.md`](../../../_shared/engineering/standing-approved-patterns.md) — pattern-001 (scaffold), pattern-005 (DI extension), pattern-006 (docs page)
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~16–22h sunfish-PM (4–5 PRs + ~40–50 tests + onboarding docs + attribution)
**PR count:** 4–5 PRs (was 3 under MAUI; pivot adds Bridge-endpoint + Tauri-shell-glue splits — see pivot note below)
**Pre-merge council:** **MANDATORY on PR 1** — security-engineering + .NET architect. Identity-boot is the highest-risk surface in the Anchor product (root-seed generation + keystore persistence + onboarding-state durability). PR 2 (recovery contact registration) follows the standard W#67 / W#46 amendment pattern: substrate scope, COB self-audit, no council required UNLESS the trustee-online-during-setup constraint cannot be honored (see Halt §H6). PR 3 (DI + docs + ledger flip) is mechanical; no council.

---

## Hand-off pivot note (2026-05-17T14-30Z)

**CO ratified the Tauri-first pivot.** Anchor's primary desktop surface is now **Tauri shell + React frontend (`apps/anchor-tauri/`)**, NOT MAUI/Blazor. This hand-off was originally authored against the MAUI shell (`accelerators/anchor/`) on 2026-05-17 (earlier the same day, pre-ratification). It has been patched in place to retarget the Tauri/React surface.

**What changed in this revision:**

- **Shell target.** All Razor (`*.razor`) page deliverables in PRs 1 + 2 + 3 are replaced with React TSX component equivalents under `apps/anchor-tauri/src/pages/IdentityBoot/`. The first-launch routing guard (formerly a `FirstLaunchGuard.razor` embedded in `Home.razor`) becomes a React-router-dom (v7) guard wrapper in `apps/anchor-tauri/src/app.tsx`.
- **Secret-storage primitive.** `KeystoreRootSeedProvider` (MAUI / Windows-DPAPI / Keychain / libsecret indirection) is replaced by **Tauri Stronghold** (`tauri-plugin-stronghold`) via the existing `apps/anchor-tauri/src/services/credentialStore.ts` surface (already on main per W#60 P4 PR 1; see Gate §G3 below). The C# kernel-security surfaces (`IRootSeedProvider`, `ITeamSubkeyDerivation`, `IRecoveryCoordinator`, `IX25519KeyAgreement`, `IAuditTrail`, `IPartyWriteService`) all stay server-side in Bridge; Bridge exposes endpoints; React calls them.
- **Service split.** `AnchorIdentityBootstrapService` + `AnchorRecoveryContactService` stay C# but relocate from `accelerators/anchor/Services/IdentityBoot/` to **`accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/`** (Bridge feature module). They are invoked over HTTP by the React frontend via new Bridge endpoint files (`IdentityBootEndpoints.cs` + `RecoveryContactEndpoints.cs`) following the existing endpoint pattern at `accelerators/bridge/Sunfish.Bridge/Listings/ListingsEndpoints.cs` + `Field/FieldEndpoints.cs`. The React side calls these via `fetch()` (the existing anchor-tauri pattern; cf. `apps/anchor-tauri/src/api/erpnext.ts`).
- **Onboarding-state store.** `PreferencesAnchorOnboardingStateStore` (MAUI `Preferences`-backed) becomes **`SqliteAnchorOnboardingStateStore`** writing to the Tauri-shell SQLite database (W#60 P3 PR 2; on main as of 2026-05-14, PR #836). The schema migration adds a single `anchor_onboarding_state` table; this is additive and uses the existing migrations infrastructure (`apps/anchor-tauri/src-tauri/migrations/`). The state itself stays non-PII (step name + timestamp + team id).
- **PR count.** Grows from 3 → **4–5** to accommodate the split between Bridge endpoint authoring (C#) + React component authoring (TSX) + Tauri shell glue (Rust commands where needed for Stronghold + SQLite invocation):
  - **PR 1 (C#):** `AnchorIdentityBootstrapService` + `SqliteAnchorOnboardingStateStore` + `IdentityBootEndpoints.cs` + ~16–20 xUnit tests. **Council MANDATORY** (unchanged).
  - **PR 2 (React):** `FirstLaunchWelcomePage.tsx` + `IdentityBootstrapPage.tsx` + router guard + ~10–12 Vitest tests + `services/identityBoot.ts` (typed Bridge client wrapper). No new Stronghold work (PR 1 already wired the auth-token Stronghold path; this PR consumes it).
  - **PR 3 (C#):** `AnchorRecoveryContactService` + `RecoveryContactEndpoints.cs` + Party linkage + ~12–14 xUnit tests. Council not required unless Halt §H6.
  - **PR 4 (React):** `RecoveryContactRegistrationPage.tsx` + `RecoveryContactReviewPage.tsx` + `RecoveryContactCard.tsx` + ~10–12 Vitest tests.
  - **PR 5 (mech):** Bridge DI extension `AddAnchorIdentityBoot()` + nav-entry wiring + `apps/docs/anchor/onboarding-flow.md` + ledger flip.
- **Owner.** `To:` changes from `sunfish-PM session (COB)` to **`dev or dev-win`** — the work crosses C# Bridge service-layer authoring (dev-friendly + COB-compatible) + React frontend authoring (dev) + Tauri shell glue (dev-win-friendly when Stronghold/SQLite Rust commands need extension). Coordinate via coordination inbox if a single owner is preferred; otherwise dev-win takes PRs 1+3 (Bridge C# + any Rust glue) and dev takes PRs 2+4 (React) with PR 5 going to whoever closes last.
- **Composition with W#67 stays unchanged.** The recovery substrate (`IRecoveryCoordinator.DesignateTrusteeAsync` + `SetupTrusteeAsync` + `IX25519KeyAgreement.Box` + `TrusteeEncryptedSeed`) is shell-agnostic. The Bridge feature module composes against the same surfaces. Gate §G2 is unchanged.
- **What did NOT change:** ADR 0068 §GC.1 attestation requirement; council requirements on PR 1; halt conditions H1–H7 (a new H8 is added for Stronghold-availability edge case — see §Halt-conditions); idempotency-key catalog (operations are unchanged; only persistence backing differs); license posture (Stronghold is Apache 2.0 / MIT dual; already promoted from "future" to "current" in the License table); W#67 substrate composition; the `IRootSeedProvider` Bridge-side contract (only the *backing* implementation changes from `KeystoreRootSeedProvider` MAUI-platform-keystore indirection to **`StrongholdRootSeedProvider`** which reads/writes via the Tauri shell's Stronghold over a thin local IPC); and the cross-cluster boundary discipline.
- **What did change in scope (additive, not breaking):** The C# `IRootSeedProvider` implementation registered in Bridge becomes `StrongholdRootSeedProvider`, which depends on a new Tauri-IPC-bridge contract (a thin REST or IPC channel from the C# Bridge process to the Tauri shell process — exact mechanism TBD; see §"Open questions" Q4 below). For the common case where Bridge runs *inside* the Tauri shell as a colocated child process (per ADR 0086 §"Process model"), the IPC is in-process. For the eventual standalone-Bridge case (Hosted tier per ADR 0088 §4), Stronghold is not the right primitive and a different `IRootSeedProvider` impl is registered. This hand-off ships ONLY the colocated-Bridge case (Light tier per ADR 0088 §4); standalone-Bridge is out of scope.

**Sources for the pivot:**

- CO ratification 2026-05-17T14-30Z (verbal, then logged via coordination inbox routing); pending written ratification beacon at `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/co-ratification-2026-05-17T14-30Z-anchor-tauri-first.md` (file the beacon if absent; do NOT proceed past PR 1 PASS gate without it on file).
- [ADR 0086](../../../docs/adrs/0086-anchor-tauri-react-product-surface.md) — Tauri/React product surface (now the canonical Anchor shell; supersedes the MAUI-first language in the original ADR 0088 Path II text — file an ADR 0088 amendment if not already in flight).
- [ADR 0088](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) — Path II Anchor runtime (Tauri-first under the pivot; check for an amendment commit ratifying this).
- W#60 P4 hand-off ([`w60-collaboration-phase4-stage06-handoff.md`](./w60-collaboration-phase4-stage06-handoff.md)) PR 1 — Tauri Stronghold (on main; verified via `apps/anchor-tauri/src-tauri/Cargo.toml` containing `tauri-plugin-stronghold = "2"` + `apps/anchor-tauri/src/services/credentialStore.ts` exposing `getToken` / `setToken` / `clearToken`).
- W#60 P3 PR 2 — SQLite offline cache (on main 2026-05-14, PR #836; provides the `apps/anchor-tauri/src-tauri/` SQLite infrastructure used by the new `SqliteAnchorOnboardingStateStore`).
- Existing React patterns at `apps/anchor-tauri/src/app.tsx` (BrowserRouter v7 from `react-router-dom` — NOT TanStack Router; the original user task brief mentioned TanStack but the codebase reality is react-router-dom v7) and `apps/anchor-tauri/src/api/erpnext.ts` (Bridge call convention).
- Existing Bridge endpoint patterns at `accelerators/bridge/Sunfish.Bridge/Listings/ListingsEndpoints.cs` + `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` (the user task brief mentioned `Cockpit/CockpitEndpoints.cs` but that file does not exist on main; the canonical pattern is `Features/*/`*Endpoints.cs` per `Sunfish.Bridge.csproj` layout).

**Inline edits below.** All MAUI / Blazor / Razor references throughout the body have been retargeted to Tauri / React / TSX. Where a MAUI-specific note (e.g. `Preferences`, `MauiProgram.cs`, `Components/Pages/`) appears, it has been replaced with the Tauri/React equivalent. The deliverable file paths, DI registration sites, council scopes, idempotency rules, and PASS gates have all been re-pointed at `apps/anchor-tauri/` (frontend) + `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/` (backend) — see per-PR sections for the canonical paths under the pivot.

**If the pivot exposes a structural blocker** (e.g. Stronghold cannot be reached from a colocated Bridge process for the IPC pattern this hand-off assumes), STOP at the boundary, drop `cob-question-anchor-identity-boot-tauri-pivot-{slug}.md` to coordination inbox, and await XO + dev-win + security-engineering reconciliation. The pivot is non-trivial; do not paper over an unworkable IPC pattern with brittle hacks.

**Audit before build (Tauri-pivot revision):**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^(foundation-recovery|kernel-security|kernel-signatures|blocks-people-foundation)"
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src/ 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src-tauri/Cargo.toml 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src/services/credentialStore.ts 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Features/ 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/IRootSeedRestorer.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/IX25519SubkeyDerivation.cs 2>&1
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-recovery/TrusteeEncryptedSeed.cs 2>&1
grep -E "tauri-plugin-stronghold|sqlx" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src-tauri/Cargo.toml
```
Expected at this hand-off's start:
- `packages/foundation-recovery/` exists (W#15 + W#32 shipped)
- `packages/kernel-security/Keys/IRootSeedProvider.cs` exists (Wave 6.3.F shipped); the registered impl in Bridge under this pivot is `StrongholdRootSeedProvider` (NEW; this hand-off adds it). The MAUI-platform-keystore `KeystoreRootSeedProvider` may still exist as an artifact of the pre-pivot shell; it is no longer registered in Bridge DI under the Tauri-first shell. Do NOT delete `KeystoreRootSeedProvider.cs` in this hand-off — leave it as a reference impl pending an ADR 0088 amendment that formally retires the MAUI surface; rely on DI registration to determine which impl ships in production.
- `packages/kernel-signatures/` exists (W#1 + W#65 session signer shipped)
- `apps/anchor-tauri/` exists on main (W#60 P3 PR 1 shipped 2026-05-14)
- `apps/anchor-tauri/src-tauri/Cargo.toml` contains `tauri-plugin-stronghold = "2"` + `sqlx ... features = ["sqlite", ...]` (W#60 P4 PR 1 + W#60 P3 PR 2 shipped)
- `apps/anchor-tauri/src/services/credentialStore.ts` exists with `getToken` / `setToken` / `clearToken` exports (W#60 P4 PR 1 shipped)
- `accelerators/bridge/Sunfish.Bridge/Features/` exists; the Bridge endpoints convention is one folder per feature with `<Feature>Endpoints.cs` static method `Map<Feature>Endpoints(this WebApplication app)`; reference impls: `Listings/ListingsEndpoints.cs`, `Field/FieldEndpoints.cs`
- `packages/blocks-people-foundation/` may or may not exist yet (see Gate §G4)
- `IRootSeedRestorer` + `IX25519SubkeyDerivation` + `TrusteeEncryptedSeed` may NOT exist yet — these are W#67 deliverables and gate PR 3 of THIS hand-off (was PR 2 pre-pivot; see Gate §G2)

---

## Gate conditions (binding — verify each before opening the corresponding PR)

### §G1 — `anchor-recovery-host-integration` shipped (gates PR 1)

PR 1 of this hand-off (first-launch identity + onboarding-state machine + initial Bridge endpoints) composes on `IRecoveryCoordinator` DI registration in Bridge's `Program.cs` (formerly `MauiProgram.cs` pre-pivot). The sibling hand-off `anchor-recovery-host-integration-stage06-handoff.md` registers (now in Bridge `Program.cs`, NOT MAUI):

```csharp
services.AddSingleton<IRecoveryStateStore, InMemoryRecoveryStateStore>();
services.AddSingleton<IRecoveryClock, SystemRecoveryClock>();
services.AddSingleton<IDisputerValidator, FixedDisputerValidator>();
services.AddSingleton<IRecoveryCoordinator, RecoveryCoordinator>();
```

This hand-off depends on those registrations existing in Bridge. **Verify before PR 1:**
```bash
grep -n "IRecoveryCoordinator\|IRecoveryStateStore" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Program.cs
```
Expected: at least one line each, registered as `AddSingleton`. If absent in Bridge but present in `accelerators/anchor/MauiProgram.cs`, the sibling hand-off has not yet been pivoted to the Tauri-first shell — STOP, drop `cob-question-anchor-identity-boot-pr1-gate-g1-sibling-not-pivoted-{slug}.md` flagging that the sibling needs to retarget Bridge before this hand-off can ship its PR 1. If absent in both, drop `cob-question-anchor-identity-boot-pr1-gate-g1-{slug}.md`.

PR 4 (recovery contact registration UI; was PR 2 pre-pivot) creates React TSX components under `apps/anchor-tauri/src/pages/IdentityBoot/Recovery/`. The sibling's pre-pivot Blazor recovery pages at `accelerators/anchor/Components/Pages/Recovery/` are NOT referenced by this hand-off post-pivot. If a Tauri-side equivalent of the sibling's recovery pages exists at `apps/anchor-tauri/src/pages/Recovery/`, this hand-off composes against it (PR 4 may embed `<TrusteeSetupPage />` from the sibling per §"Page-composition note" below). If only the MAUI sibling pages exist on main, file `cob-question-*` to coordinate the sibling's pivot ahead of (or in parallel with) PR 4.

### §G2 — W#67 social recovery delivery substrate shipped (gates PR 3 backend + PR 4 React)

**Note on PR numbering (post-pivot):** PR 3 (was PR 2 pre-pivot) is the Bridge-side recovery contact backend; PR 4 (new under pivot) is the React-side recovery contact UX. Both depend on the W#67 substrate.

PR 3 of this hand-off (recovery contact registration backend + service composition) requires the following W#67 deliverables to exist on `main`:

- `packages/kernel-security/Keys/IX25519SubkeyDerivation.cs` + `HkdfX25519SubkeyDerivation.cs` (W#67 PR 1)
- `packages/kernel-security/Keys/IRootSeedRestorer.cs` (W#67 PR 1) — read-only reference (PR 3 of THIS hand-off does NOT inject `IRootSeedRestorer`; that interface is reserved for `AnchorRecoveryCompletionHandler` per ADR 0046-A6 §A6.8 §"DI scoping" note)
- `packages/foundation-recovery/TrusteeEncryptedSeed.cs` (W#67 PR 3)
- `packages/foundation-recovery/RecoveryCompletionResult.cs` (W#67 PR 3)
- `packages/foundation-recovery/IRecoveryCoordinator.cs` widened with `SetupTrusteeAsync` AND `DesignateTrusteeAsync` re-shaped to accept `trusteeDHPublicKey` per W#67 PR 5 MAJOR-2 binding fix
- `packages/foundation-recovery/TrusteeDesignation.cs` extended with `DHPublicKey` field (W#67 PR 5)

**Verify before PR 3:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/IX25519SubkeyDerivation.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/IRootSeedRestorer.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-recovery/TrusteeEncryptedSeed.cs
grep -n "SetupTrusteeAsync\|DHPublicKey" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-recovery/IRecoveryCoordinator.cs
grep -n "DHPublicKey" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-recovery/TrusteeDesignation.cs
```
Each command must produce output. Any missing → STOP PR 3 (PRs 1 + 2 can proceed independently); drop `cob-question-anchor-identity-boot-pr3-gate-g2-{slug}.md` naming exactly which symbol is missing.

### §G3 — W#60 P4 Tauri Stronghold + W#60 P3 SQLite cache on main (BINDING under pivot; gates PR 1)

**Status under the Tauri-first pivot (2026-05-17T14-30Z):** this gate is now **BINDING**, not informational. The canonical Anchor shell is Tauri/React. The secret-storage primitive is `tauri-plugin-stronghold`. The on-device persistence is the SQLite database wired by W#60 P3 PR 2.

**Verify before PR 1:**
```bash
grep -n "tauri-plugin-stronghold" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src-tauri/Cargo.toml
grep -n "sqlx.*sqlite" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src-tauri/Cargo.toml
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src/services/credentialStore.ts
grep -E "^export (async )?function (setToken|getToken|clearToken)" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src/services/credentialStore.ts
```
All four commands must produce output. Expected state per main as of 2026-05-17:
- `tauri-plugin-stronghold = "2"` is in Cargo.toml (W#60 P4 PR 1 shipped — verified pre-authoring)
- `sqlx ... features = [..."sqlite"...]` is in Cargo.toml (W#60 P3 PR 2 shipped 2026-05-14 per PR #836)
- `credentialStore.ts` exposes `setToken` / `getToken` / `clearToken` plus a `resetForTesting()` test hook (verified pre-authoring)

If ANY of the four checks fails:
- `tauri-plugin-stronghold` missing → W#60 P4 PR 1 has rolled back or is on a feature branch; STOP, drop `cob-question-anchor-identity-boot-pr1-gate-g3-stronghold-missing-{slug}.md` and await dev-win's resolution. Do NOT proceed with the auth-token flow against a non-Stronghold backing — falling back to plain-text `localStorage` or `appsettings` is a security regression.
- `sqlite` feature missing from sqlx → W#60 P3 PR 2 has rolled back or moved targets; STOP, drop `cob-question-anchor-identity-boot-pr1-gate-g3-sqlite-missing-{slug}.md`. The `SqliteAnchorOnboardingStateStore` (PR 1 deliverable) requires this infrastructure.
- `credentialStore.ts` missing → PR 1's auth-token-fetch step has no API to call; STOP, drop `cob-question-*`.
- `credentialStore.ts` lacks one of the three exports → the file changed shape since hand-off authoring; verify the new API, update PR 1's deliverable spec to match (commit message annotates the deviation), or file `cob-question-*` if the change is non-trivial.

**Why this matters under the pivot:** `StrongholdRootSeedProvider` (the new `IRootSeedProvider` impl this hand-off introduces in PR 1) READS / WRITES the 32-byte root seed via an extension of the credentialStore Stronghold vault. The vault is opened ONCE per app session per the credentialStore singleton pattern (cached `Stronghold` + `Client` handles). The root seed gets a dedicated `Client` namespace within the same snapshot — `anchor-rootseed` — to keep the auth token and the root seed cryptographically isolated within a single Stronghold snapshot. This requires Stronghold's multi-client snapshot feature to be available (it is, per `tauri-plugin-stronghold` v2); if it weren't, we'd need a second snapshot file, which is fine but requires an extra IPC round-trip.

**If COB notices that the Tauri shell has been deprecated mid-build** (a CO directive landing in `coordination/inbox/` reverting to MAUI), STOP, drop `cob-question-anchor-identity-boot-shell-divergence-{slug}.md`, and await XO reroute. Per project memory `project_w60_p4_ownership_dev_win.md` + the 2026-05-17T14-30Z ratification, the Tauri-first direction is firm; an unannounced revert would itself be the surprise condition.

**Halt §H8** below covers the Stronghold-availability edge case in more depth (e.g. Stronghold init returns an error in a fresh-install smoke test).

### §G4 — `blocks-people-foundation` status (informational, not blocking)

PR 3's (post-pivot; was PR 2 pre-pivot) recovery contact registration creates one `Party` row per recovery contact (kind = "person") so that the contact's display name + email + phone are stored once and referenced by id from `TrusteeDesignation.NodeId` ↔ `RecoveryContact.PartyId` linkage.

- **If `blocks-people-foundation` has shipped:** PR 3 takes a hard dependency on `Sunfish.Blocks.People.Foundation.IPartyWriteService.CreateAsync(...)` + `IPartyReadModel.GetByIdAsync(...)`. The recovery-contact local record stores `PartyId` (strong-typed) + `TrusteeNodeId` (the Foundation.Recovery-canonical id of the trustee's pairing identity).
- **If `blocks-people-foundation` has NOT shipped:** PR 3 ships a LOCAL stub `IPartyWriteService` + `IPartyReadModel` + `PartyId` strong-id (identical pattern to `blocks-financial-ar`'s PR 6 stub per the AR hand-off lines 1593–1617 + the `blocks-people-foundation` hand-off context §"Why this is the right slice now"). When the foundation lands, a separate retrofit hand-off relocates the stub via one-line `using` directive swap. The recovery-contact local record stays in this hand-off's package regardless of where the stub lives — only the `using` import changes.

**Verify before PR 3:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/ 2>&1
```
- Output = directory contents → use the canonical import.
- Output = "No such file or directory" → ship the local stub per `blocks-financial-ar` precedent.

Either path is `ready-to-build` for PR 3 once §G2 clears.

### §G5 — `kernel-signatures` session signer + identity-key surfaces

PR 1 references `IRootSeedProvider.GetRootSeedAsync()` (the existing first-launch identity-bootstrap surface) and `ITeamSubkeyDerivation.DeriveTeamSubkey(root, teamId)` (the existing per-team Ed25519 subkey derivation). Both exist on `main` per Wave 6.3.F (`packages/kernel-security/Keys/`). No new key derivation interfaces are introduced by this hand-off; we are wiring the *first-launch UX* over the existing surfaces.

**Verify before PR 1:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/IRootSeedProvider.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/KeystoreRootSeedProvider.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-security/Keys/ITeamSubkeyDerivation.cs
```
Each must exist. Any missing → STOP, drop `cob-question-anchor-identity-boot-pr1-gate-g5-{slug}.md`. (This is extremely unlikely — these interfaces have been on main since Wave 6.3.F; the check is mechanical defensive verification.)

---

## Context

### What this hand-off ships

The Anchor first-launch identity-bootstrap surface — the UX + state machine + DI plumbing that exposes `IRootSeedProvider` to the user on first launch, walks them through the recovery-contact-registration step, and persists the resulting onboarding-state so subsequent launches skip the wizard. The substrate primitives (`IRootSeedProvider`, `ITeamSubkeyDerivation`, `IRecoveryCoordinator`, `IX25519SubkeyDerivation`, `TrusteeEncryptedSeed`) all already exist (or are landing via gates §G1 + §G2). This hand-off does **not** add new cryptographic primitives or modify the recovery-protocol shape; it ships the user-facing flow.

Concretely (post-pivot file paths):

1. **`AnchorIdentityBootstrapService`** (`accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/`) — Bridge feature module; orchestrates the first-launch sequence: detect first-launch state → invoke `IRootSeedProvider.GetRootSeedAsync()` (which generates + persists the 32-byte seed via `StrongholdRootSeedProvider`) → emit `IdentityProvisioned` audit event → mark onboarding-step state. Invoked by the React frontend over HTTP via `IdentityBootEndpoints`.
2. **`StrongholdRootSeedProvider`** (`accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/StrongholdRootSeedProvider.cs`) — NEW `IRootSeedProvider` impl for the Tauri-first shell. Reads/writes the 32-byte seed via a local IPC channel to the Tauri shell's Stronghold vault under a dedicated `anchor-rootseed` client namespace. Replaces `KeystoreRootSeedProvider` in Bridge DI registration. The MAUI-platform-keystore impl stays in the codebase as reference until ADR 0088 amendment formally retires it.
3. **`SqliteAnchorOnboardingStateStore`** + `AnchorOnboardingState` — durable persistence in the Tauri shell's SQLite database (W#60 P3 PR 2 infrastructure). The state itself stays non-PII (step name + timestamp + team id). A schema migration adds an `anchor_onboarding_state` table; lives under `apps/anchor-tauri/src-tauri/migrations/` AND `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/Migrations/` depending on which side owns the DB writes (under the colocated-Bridge case per ADR 0086 §"Process model", the Tauri shell owns the SQLite handle; Bridge writes through a thin command exposed over IPC — see §"IPC pattern" note in PR 1 below).
4. **`IdentityProvisionedEvent`** + `RecoveryContactRegisteredEvent` — typed cross-cluster events emitted via the kernel-audit `IAuditTrail` (W#67 precedent: `AuditEventType.RecoveryRekey`). Unchanged from pre-pivot.
5. **`IdentityBootEndpoints.cs`** (Bridge; `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/`) — minimal-API endpoints: `POST /api/v1/identity-boot/ensure-bootstrapped`, `GET /api/v1/identity-boot/state`, `GET /api/v1/identity-boot/is-first-launch`. Follow the `Listings/ListingsEndpoints.cs` pattern (static `MapIdentityBootEndpoints(this WebApplication app)` registered from Bridge `Program.cs`).
6. **`FirstLaunchWelcomePage.tsx`** (`apps/anchor-tauri/src/pages/IdentityBoot/`) — React TSX component; the "this is a fresh install; we'll set up your identity now" entry point. Routes to `/identity/bootstrap` (`IdentityBootstrapPage.tsx`).
7. **`IdentityBootstrapPage.tsx`** (`apps/anchor-tauri/src/pages/IdentityBoot/`) — React TSX component; invokes the Bridge endpoint via the `services/identityBoot.ts` typed client; shows progress; on success routes to `/identity/recovery-contacts`.
8. **`RecoveryContactRegistrationPage.tsx`** (PR 4; `apps/anchor-tauri/src/pages/IdentityBoot/`) — collects 3 recovery contacts (the W#67 trustees); the Bridge feature-side `AnchorRecoveryContactService` composes `IRecoveryCoordinator.DesignateTrusteeAsync(...)` + `SetupTrusteeAsync(...)` per ADR 0046-A6 §A6.5. **Trustees must be online during setup** (ADR 0046-A6 §A6.5 §"Trustee-online requirement for setup"); the page surfaces this constraint clearly.
9. **`RecoveryContactReviewPage.tsx`** (PR 4; `apps/anchor-tauri/src/pages/IdentityBoot/`) — list-current-contacts + add/remove + acknowledgement-of-threat-model surface (per ADR 0046-A6 §A6.1 §"Threat model expansions" — per-install blast radius + no revocation must be explicit).
10. **`RecoveryContactCard.tsx`** (PR 4) — shared React component used by the Review page.
11. **`services/identityBoot.ts`** (`apps/anchor-tauri/src/services/`) — typed Bridge client for the identity-boot endpoints; mirrors `services/credentialStore.ts` style; uses `fetch` with the existing auth-token-on-cookie convention per `apps/anchor-tauri/src/api/erpnext.ts`.
12. **First-launch routing guard** — a React-router-dom (v7) `<Navigate>` redirector wrapped around route definitions in `apps/anchor-tauri/src/app.tsx`. Implemented as a `<FirstLaunchGuard>` component (`apps/anchor-tauri/src/components/FirstLaunchGuard.tsx`) that checks `GET /api/v1/identity-boot/is-first-launch` on mount and renders `<Navigate to="/identity/welcome" replace />` if true, else renders `<Outlet />`. Placed at the top of the route tree (wrapping the existing app shell routes).
13. **DI extension** — `AddAnchorIdentityBoot()` (`accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/ServiceCollectionExtensions.cs`) registers the bootstrap service + state store + the recovery-contact-registration service + endpoint mapping. Composes with the existing `AddAnchorRecovery()` registrations from `anchor-recovery-host-integration` (also relocated to Bridge under the pivot).
14. **Onboarding-flow user-guide page** (PR 5) at `apps/docs/anchor/onboarding-flow.md` covering: what happens on first launch, where the seed lives (Stronghold; not platform-keystore-indirect any more), how to register recovery contacts, the threat-model expansions per ADR 0046-A6 §A6.1, what happens if the user loses the device, what happens if a recovery contact's keystore is compromised.

### What this hand-off does NOT ship

- **Multi-device pairing.** Adding a SECOND device to the same identity (so two Anchor installs share the same root seed and CRDT replica) is **explicitly out of scope**. That work is owned by a future `blocks-localfirst-sync` cluster (cluster decomposition TBD; not in any Phase 1/2/3 of ADR 0088 Appendix B). The existing `Sunfish.Anchor.Services.Pairing.IPairingService` + `HmacPairingService` cover *team-membership pairing* (one device joins another device's team), which is a fundamentally different operation from *device-identity pairing* (same-identity multi-device). See §"Open questions" Q1 below for the scope-boundary rationale and the design hooks this hand-off leaves in place for the future cluster.
- **Enterprise SSO / federated identity.** Not in scope. The Anchor identity is a per-install root seed (paper §11.2–§11.3); enterprise SSO would be a Bridge surface (paper §20.7 Zone C / Hosted tier per ADR 0088 §4), not an Anchor-shell surface. Future ADR if/when a corporate-deployment scenario surfaces.
- **Recovery contact revocation.** Per ADR 0046-A6 §A6.1 §"No revocation": de-designating a trustee does NOT revoke their encrypted seed copy. PR 2 surfaces "Remove Contact" only as a cosmetic-list operation that emits an audit event; the page text explicitly states that the removed contact retains a usable copy until the root seed is rotated. **Root-seed rotation is not a Phase 2 primitive** (ADR 0046-A6 §A6.1 §"No revocation" + ADR 0046-A6 §"Alternatives rejected" — Phase 3 of A6). Do not invent a rotation flow here.
- **Recovery initiation from this hand-off.** The `InitiateRecoveryPage.razor` + `RecoveryStatusPage.razor` + `ApproveRecoveryPage.razor` ship via the sibling `anchor-recovery-host-integration` hand-off (Phase 1, 5 Razor pages). This hand-off only ships the *registration* surface (set up contacts during first-launch onboarding); it does not duplicate the *initiation* surface.
- **`IRecoveryContactService`** as a NEW interface. The user task brief uses this label colloquially. The actual composition surface is `IRecoveryCoordinator.DesignateTrusteeAsync(...)` + `IRecoveryCoordinator.SetupTrusteeAsync(...)` (both extant or landing via §G2). No new "contact service" abstraction is introduced; this hand-off composes against the existing coordinator. The local `AnchorRecoveryContactService` helper (PR 2) is an *Anchor-shell-internal* coordinator-wrapper that adds the `Party` linkage; it is not a public foundation surface.
- **First-launch hardware attestation.** Out of scope. The platform-attestation surface (`packages/kernel-security/Attestation/`) is wired by other workstreams; this hand-off neither requires nor exposes it.
- **Network onboarding (joining an existing team via QR).** Out of scope. That flow is the EXISTING `Onboarding.razor` + `QrOnboardingService` surface (`accelerators/anchor/Components/Pages/Onboarding.razor` + `accelerators/anchor/Services/QrOnboardingService.cs`). This hand-off's first-launch path is the *founder* path (this device is its own founder, generating a fresh team); the `Onboarding.razor` path is the *joiner* path (this device is joining an existing team). PR 1's `FirstLaunchWelcomePage.razor` surfaces BOTH choices (`Set up a new identity` → this hand-off's flow; `Join an existing team` → the existing `Onboarding.razor`).
- **Founder-bundle generation for downstream joiners.** The `QrOnboardingService.GenerateFounderBundleAsync(...)` surface already exists; this hand-off does not duplicate it. PR 1's bootstrap service emits the seed but does NOT generate a founder bundle — that's a separate, user-initiated action invoked later from a "Manage Identity" page (a P2 follow-on; not in scope for this hand-off).

### Why this hand-off, why now

1. **`IRootSeedProvider` is wired but never user-surfaced.** Per the XML doc on `IRootSeedProvider.cs` lines 7–13, the first-launch seed is generated implicitly on first `GetRootSeedAsync()` call. Today, that first call happens silently inside `AnchorBootstrapHostedService` during app start. The user has no visibility into the fact that their identity has just been provisioned, no opportunity to register recovery contacts before it lands, and no record of *when* the seed was generated. PR 1's bootstrap service surfaces this; PR 2 closes the loop by routing to recovery-contact registration before the wizard exits.
2. **W#67 just shipped seed delivery.** Per project memory `project_w65_w66_w67_g6_closed.md`, W#67 closed G6-A on 2026-05-16 with 6 PRs (#875–#903). The recovery substrate is real but has no first-launch UX entry point — users must currently navigate to `/recovery/trustee-setup` (via the sibling `anchor-recovery-host-integration` Razor pages) on their own initiative. This hand-off makes recovery-contact registration **part of the onboarding wizard**, so the safety net exists *before* the user starts entering business data. Without this, every fresh Anchor install ships in an unsafe state where the user must remember to set up recovery later (and most won't).
3. **ADR 0088 Path II committed to local-first.** Per ADR 0088 §"Decision" + §"Positive" §"Local-first vision intact" — "one install gives the user everything; no Docker prompt, no ERPNext URL configuration, no leaky abstraction." A first-launch flow that doesn't surface identity provisioning leaves the user with no model of where their data lives or what protects it. PR 3's user-guide page closes the explanation loop.
4. **Tauri-MAUI parallel hardening.** PR 1's bootstrap service is shell-agnostic (`IIdentityBootstrapService` interface + `AnchorIdentityBootstrapService` MAUI implementation). When the Tauri shell ports this flow, only the page-rendering layer changes; the service contract stays. This minimizes the future port cost.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `RecoveryContactId` (PR 2) — ULID, strongly typed; storage as text |
| §2 Soft-delete tombstones | `RecoveryContact.deletedAt` / `deletedBy` — "Remove contact" sets tombstone, never hard-deletes (preserves audit trail of who was a recovery contact at any point) |
| §3 version + revisionVector | `AnchorOnboardingState` + `RecoveryContact` carry `Version: long` and (where multi-replica capable, ie. once same-identity multi-device lands) `RevisionVector` |
| §5 Stable string codes | `AnchorOnboardingStep` is a stable-string-coded enum (`"welcome"`, `"identity-bootstrapped"`, `"recovery-contacts-registered"`, `"complete"`) per §5 deprecation discipline; never rename |
| §6 Posted-then-immutable | `IdentityProvisionedEvent` + `RecoveryContactRegisteredEvent` are append-only audit records; once written they are never UPDATEd (W#67 precedent: `AuditEventType.RecoveryRekey`) |
| §10 Two-tier validation | Tier-1 write-time: contact email must validate RFC 5322 (PR 2); trustee DH public key must be 32 bytes; contact display name must be non-empty. Tier-2 post-merge: out of scope for this hand-off (`AnchorOnboardingState` is single-replica per install for now — same-identity multi-device pairing is the cross-replica trigger and is out of scope per §"What this hand-off does NOT ship") |
| §14 Per-tenant isolation | `AnchorOnboardingState` is per-team-id (uses the existing `ITeamContextFactory.GetOrCreateAsync(teamId, ...)` boundary); recovery contacts inherit the same boundary. A single install with multiple teams (per W#42 multi-team workspace) has independent onboarding state per team |

### Cross-cluster boundary (binding; revised for pivot)

This hand-off spans **two repository surfaces** under the pivot:
- `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/` — Bridge feature module (C#; ASP.NET Core minimal-API). Owns the service layer + endpoint surface.
- `apps/anchor-tauri/src/pages/IdentityBoot/` + `apps/anchor-tauri/src/services/identityBoot.ts` + `apps/anchor-tauri/src/components/FirstLaunchGuard.tsx` — Tauri shell React frontend. Owns the UX layer.

Neither lives under `packages/*` (the kernel + foundation packages are framework-agnostic; this hand-off's shell-specific). The boundary rules:

- **The Bridge feature module OWNS the server-side bootstrap + recovery-contact-registration wiring.** No `packages/*` may take a dependency on `Sunfish.Bridge.Features.IdentityBoot.*`.
- **The Tauri React frontend OWNS the UX surface.** No other React frontend (e.g. a hypothetical Bridge admin UI under `accelerators/bridge/Sunfish.Bridge/wwwroot/`) may take a dependency on `@/pages/IdentityBoot/*` — those components are Anchor-shell specific because their copy frames the user as a Light-tier single-device founder per ADR 0088 §"Tiered runtime model" §Light.
- **Reads** (Bridge service layer) from `IRootSeedProvider` (kernel-security; via the new `StrongholdRootSeedProvider` impl), `IRecoveryCoordinator` (foundation-recovery), `IX25519SubkeyDerivation` (kernel-security; W#67 PR 1), `IPartyReadModel` (blocks-people-foundation OR local stub per §G4). Reads (React frontend) from Bridge endpoints only — never directly from `tauri-plugin-stronghold` for the root seed (the seed never leaves the Bridge/Stronghold trust boundary; only fingerprints + state cross into the React layer).
- **Writes** to `IRecoveryCoordinator` (via `DesignateTrusteeAsync` + `SetupTrusteeAsync`), `IAuditTrail` (W#67 `AuditEventType.RecoveryRekey` precedent — this hand-off adds `IdentityProvisioned` + `RecoveryContactRegistered`), `IPartyWriteService` (blocks-people-foundation OR local stub). All writes happen in the Bridge service layer; the React frontend POSTs through endpoints.
- **Never writes** to `IRootSeedProvider` (read-only by contract per `IRootSeedProvider.cs` line 31); never invokes `IRootSeedRestorer` (reserved for `AnchorRecoveryCompletionHandler` per ADR 0046-A6 §A6.8 §"DI scoping"); never bypasses `IRecoveryCoordinator` to write to `RecoveryCoordinatorState` directly.
- **Never exposes raw seed bytes over HTTP/IPC.** The Bridge↔Tauri IPC channel transmits ONLY the auth token (for credentialStore) and (in PR 1's StrongholdRootSeedProvider) the seed bytes ONLY in the in-process colocated-Bridge case where the IPC is a same-process function call. In the hypothetical out-of-process Bridge case, the seed bytes never leave Stronghold — instead, Bridge requests "perform operation X with the seed" and Stronghold returns the result. PR 1's implementation MUST be auditable for this invariant; security-engineering council line item.

---

## Pre-build checklist (dev / dev-win executes before opening PR 1)

1. **Verify gates §G1 + §G3 + §G5 cleared** (see Gate conditions above; note §G3 is now BINDING under the Tauri pivot). Each command must produce expected output. Any miss → drop `cob-question-*` (or `dev-question-*` / `dev-win-question-*`) beacon and STOP.

2. **Verify the Tauri shell is the canonical Anchor surface, not MAUI.** Per the §"Hand-off pivot note" at the top of this hand-off, this hand-off targets `apps/anchor-tauri/`. Confirm:
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src-tauri/Cargo.toml
   ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/src/app.tsx
   grep -n "react-router-dom" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/package.json
   ```
   Expected: all three commands succeed; `react-router-dom` is a dependency in `apps/anchor-tauri/package.json` (currently v7 per main). If the Tauri shell has been removed from main (extremely unlikely under the 2026-05-17T14-30Z ratification), STOP — the shell-divergence question in §G3 applies.

3. **Confirm no in-flight PRs touch `apps/anchor-tauri/src/pages/IdentityBoot/` or `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/` or `apps/anchor-tauri/src/components/FirstLaunchGuard.tsx`.**
   ```bash
   gh pr list --state open --search "anchor-identity-boot in:title,body"
   gh pr list --state open --search "FirstLaunchWelcomePage in:title,body,files"
   gh pr list --state open --search "IdentityBootstrapPage in:title,body,files"
   gh pr list --state open --search "Features/IdentityBoot in:title,body,files"
   ```
   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*` / `dev-question-*`.

4. **Confirm no in-flight PRs touch the pre-pivot MAUI surface** `accelerators/anchor/Components/Pages/` or `accelerators/anchor/Services/` (a parallel session attempting the pre-pivot version of this hand-off would be a coordination failure):
   ```bash
   gh pr list --state open --search "accelerators/anchor/Services/IdentityBoot in:title,body"
   gh pr list --state open --search "FirstLaunchWelcomePage.razor in:title,body"
   ```
   Expected: empty. If non-empty, STOP, drop `*-question-*` to surface the conflict to XO.

4. **Confirm sibling `anchor-recovery-host-integration` is built** (per Gate §G1). If still in-flight, this hand-off's PR 1 may rebase-conflict against it; coordinate the sequence so the sibling lands first.

5. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

6. **Skim ADR 0046-A6 §A6.1 §"Threat model expansions"** (lines 50–58 of the ADR). The recovery-contact-registration UX MUST surface these threat-model facts to the user (per PR 2 §"Threat-model acknowledgement" deliverable). Do not paraphrase; the page-copy quotes the ADR.

7. **Skim ADR 0088 §"Decision" + §"Tiered runtime model" §"Light"** (the canonical local-first product profile). This hand-off ships the Light-tier onboarding; the page-copy frames the wizard for the Light-tier user (single-device, no Bridge, local-first).

8. **Read** `_shared/engineering/standing-approved-patterns.md` §"pattern-001 — Cluster scaffold + Repository + DI" + §"pattern-005 — DI extension `Add<Block>()` umbrella" + §"pattern-006 — `apps/docs/blocks/<cluster>/overview.md` authoring". This hand-off applies all three; PR commit subjects must include `@standing-pattern: pattern-001` / `pattern-005` / `pattern-006` per the ratification metadata convention.

---

## Per-PR deliverables (post-pivot 5-PR split)

This hand-off splits into **5 PRs** by responsibility (up from 3 pre-pivot — the Bridge-endpoint + React-component split for each functional half + a consolidated DI/docs/ledger PR):

- **PR 1 (C#; Bridge):** `StrongholdRootSeedProvider` + `AnchorIdentityBootstrapService` + `SqliteAnchorOnboardingStateStore` + `IdentityBootEndpoints.cs` + identity-provisioning audit + first-launch detection endpoint. **MANDATORY pre-merge council** — security-engineering + .NET architect.
- **PR 2 (TSX; Tauri React frontend):** `FirstLaunchWelcomePage.tsx` + `IdentityBootstrapPage.tsx` + `FirstLaunchGuard.tsx` + `services/identityBoot.ts` typed Bridge client + Vitest tests. No new Stronghold work (PR 1's `StrongholdRootSeedProvider` is the substrate; this PR consumes the Bridge endpoints).
- **PR 3 (C#; Bridge):** `AnchorRecoveryContactService` (Bridge-feature helper, NOT a foundation interface) + `RecoveryContactEndpoints.cs` + `Party` linkage (per §G4) + `RecoveryContactRegistered` audit event. Council not required unless trustee-online constraint forces a scope shift (Halt §H6).
- **PR 4 (TSX; Tauri React frontend):** `RecoveryContactRegistrationPage.tsx` + `RecoveryContactReviewPage.tsx` + `RecoveryContactCard.tsx` shared component + Vitest tests. Composes Bridge endpoints from PR 3.
- **PR 5 (mechanical):** DI extension umbrella (`AddAnchorIdentityBoot()` in Bridge) + nav-entry wiring in `apps/anchor-tauri/src/app.tsx` + `apps/docs/anchor/onboarding-flow.md` user-guide + ledger flip + sibling-DI cross-wire verification.

Build order: PR 1 → PR 2 → PR 3 → PR 4 → PR 5. PR 1 + PR 3 can be authored in parallel branches by dev-win (Bridge C# side) while PR 2 + PR 4 are authored by dev (React side), but each TSX PR depends on its corresponding C# endpoint PR being merged first (so the React side can call real endpoints). PR 5 lands last.

**Owner allocation suggestion:** dev-win owns PRs 1 + 3 (Bridge C# + any Rust glue for the Stronghold IPC channel); dev owns PRs 2 + 4 (React TSX); PR 5 goes to whoever closes the second TSX PR (typically dev) since it ledger-flips both halves.

---

### PR 1 (C#; Bridge) — `StrongholdRootSeedProvider` + `AnchorIdentityBootstrapService` + first-launch detection + identity-provisioning endpoints

**Estimated effort:** ~6–8h (Bridge feature + Stronghold IPC contract + SQLite schema)
**Scope:** new `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/` directory; `StrongholdRootSeedProvider` impl of `IRootSeedProvider` (replaces `KeystoreRootSeedProvider` in Bridge DI under the pivot); bootstrap service; first-launch detector; SQLite-backed onboarding-state store; identity-provisioning audit event; `IdentityBootEndpoints.cs` exposing three minimal-API routes; DI registrations (inline; PR 5 extracts the umbrella extension). No Razor; no TSX (UX ships in PR 2).
**Commit subject:** `feat(anchor-identity-boot): Bridge identity-boot feature + StrongholdRootSeedProvider + SqliteOnboardingStateStore @standing-pattern: pattern-001`
**Branch:** `dev-win/anchor-identity-boot-pr1-bridge-feature` (or `cob/...` if COB owns; see Owner allocation in §"Per-PR deliverables").
**Council:** **MANDATORY** — security-engineering (key-generation + Stronghold-IPC contract + seed-handling invariants + audit-emission surface) + .NET architect (DI lifetime, endpoint-mapping pattern, IPC-channel design for the Bridge↔Stronghold path, async patterns). File `*-council-request-anchor-identity-boot-pr1-{slug}.md` to coordination inbox BEFORE opening the PR draft so XO can dispatch the councils in parallel with implementation.

#### IPC pattern (binding for PR 1)

`StrongholdRootSeedProvider` (running in the Bridge C# process) needs to read/write the 32-byte root seed in Stronghold (which runs in the Tauri Rust process). Two cases per ADR 0086 §"Process model":

1. **Colocated-Bridge case (Light tier; this hand-off's target):** Bridge runs inside the Tauri shell as a colocated child process (or, in the simplest configuration, as a .NET host invoked via Tauri's sidecar mechanism). The IPC channel is a Tauri command invoked from a thin Rust wrapper in `apps/anchor-tauri/src-tauri/src/identity_boot/mod.rs`. The Rust side opens the Stronghold vault under the `anchor-rootseed` client namespace and exposes two commands: `seed_get_or_generate` (returns a 32-byte seed; generates one if absent) and `seed_exists` (returns bool without exposing the seed). Bridge calls these via a thin `IStrongholdIpcChannel` interface; PR 1 ships an `HttpStrongholdIpcChannel` impl that talks to a local-only Tauri command bridge (`http://127.0.0.1:<port>/__stronghold/...`) — this is the colocated-Bridge IPC; a future replacement with a Unix domain socket / named pipe is possible but out of scope.

2. **Standalone-Bridge case (Hosted tier; out of scope):** Bridge runs on a different host; Stronghold doesn't exist. A different `IRootSeedProvider` impl is registered (the existing `KeystoreRootSeedProvider` or a HSM-backed alternative). DO NOT address this in PR 1; the DI registration in `Program.cs` should be conditional on a config flag (`BridgeMode.Colocated` vs `BridgeMode.Standalone`) so this hand-off doesn't break standalone-Bridge deployments. Verify `BridgeMode.cs` in `accelerators/bridge/Sunfish.Bridge/` already exposes the right enum/config; if not, file `cob-question-*`.

**Critical security invariant:** in the colocated-Bridge case, the seed bytes traverse the `127.0.0.1` HTTP channel between Bridge and the Tauri Rust process. This is fine for v1 (both processes are on the same machine, owned by the same user), but the channel MUST:
- Bind to `127.0.0.1` only (never 0.0.0.0 / external interfaces).
- Use a per-app-launch random port + bearer token (the Tauri shell generates both on app start and passes them to Bridge via env var or startup config).
- Reject any request without the bearer token.
- Log seed-related operations to the audit trail (which already routes through `IAuditTrail`).

Security-engineering council line item: verify these invariants are implemented + tested. PR 1 ships tests for token rejection + 127.0.0.1-binding.

#### Files to create

```
accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/
├── IAnchorIdentityBootstrapService.cs          (interface)
├── AnchorIdentityBootstrapService.cs           (Bridge implementation)
├── AnchorOnboardingStep.cs                     (string-coded enum per CRDT §5)
├── AnchorOnboardingState.cs                    (state record)
├── IAnchorOnboardingStateStore.cs              (storage abstraction)
├── SqliteAnchorOnboardingStateStore.cs         (sqlx via existing Tauri SQLite handle; v1)
├── AnchorOnboardingStateException.cs           (typed error for invalid transitions)
├── IIdentityProvisionedEventEmitter.cs         (audit-event emission interface)
├── IdentityProvisionedEventEmitter.cs          (composes IAuditTrail)
├── AnchorIdentityBootEvents.cs                 (typed payload records)
├── IStrongholdIpcChannel.cs                    (IPC abstraction; testable)
├── HttpStrongholdIpcChannel.cs                 (127.0.0.1 + bearer-token impl)
├── StrongholdRootSeedProvider.cs               (IRootSeedProvider impl via IStrongholdIpcChannel)
├── IdentityBootEndpoints.cs                    (3 minimal-API routes; pattern-001 + 005)
└── Migrations/
    └── 0001_create_anchor_onboarding_state.sql (additive table; per-team isolation by (team_id) PK)

apps/anchor-tauri/src-tauri/src/identity_boot/
├── mod.rs                                       (Tauri command wrappers — seed_get_or_generate, seed_exists)
└── stronghold_ipc.rs                            (the localhost HTTP bridge for IStrongholdIpcChannel)

accelerators/bridge/Sunfish.Bridge.Tests/Features/IdentityBoot/
├── AnchorIdentityBootstrapServiceTests.cs      (8–10 tests)
├── SqliteAnchorOnboardingStateStoreTests.cs    (5–7 tests; uses in-memory sqlite for tests)
├── AnchorOnboardingStepTransitionTests.cs      (4–5 tests)
├── IdentityProvisionedEventEmitterTests.cs     (3–4 tests)
├── HttpStrongholdIpcChannelTests.cs            (5–6 tests; bearer-token rejection, 127.0.0.1-only, seed-roundtrip-via-mock-server)
└── IdentityBootEndpointsTests.cs               (4–5 tests; WebApplicationFactory-style integration)
```

#### `AnchorOnboardingStep` — stable-coded step enum

Per CRDT-friendly conventions §5, the step values are **stable string codes**, never renamed. Marshal as `string` over any wire / persistence boundary; the enum sugar is for in-memory ergonomics only.

```
welcome                       — fresh install; wizard not yet started
identity-bootstrapped         — IRootSeedProvider has materialized the 32-byte seed; audit emitted
recovery-contacts-registered  — at least one IRecoveryCoordinator.SetupTrusteeAsync has succeeded (PR 2 gate)
complete                      — all steps done; wizard never shown again unless onboarding-state is cleared
```

Provide `AnchorOnboardingStepExtensions.IsTerminal(this AnchorOnboardingStep s)` returning `true` for `complete`. Provide `IsValidTransition(from, to)` enforcing the linear order (`welcome → identity-bootstrapped → recovery-contacts-registered → complete`); back-transitions are forbidden (no "wizard re-run" in v1; that's a follow-on if a user requests it).

#### `AnchorOnboardingState` record

```
record AnchorOnboardingState
    Step                        AnchorOnboardingStep   (string-coded)
    TeamId                      TeamId                 (the Kernel.Runtime.Teams.TeamId)
    IdentityProvisionedAt       DateTimeOffset?        (null until step == identity-bootstrapped)
    RecoveryContactsRegisteredAt DateTimeOffset?       (null until step == recovery-contacts-registered)
    CompletedAt                 DateTimeOffset?        (null until step == complete)
    Version                     long                    (monotonic per-replica counter; CRDT §3)
```

Soft-delete is not applicable (state is per-install singleton per team; a new state replaces the old via UPDATE, but in v1 we treat each state-change as an additive write to Preferences with the Version bumped). When same-identity multi-device pairing lands, this record will need `RevisionVector` + a tombstone field; v1 is single-replica per install per team.

#### `IAnchorOnboardingStateStore` — abstraction

```
interface IAnchorOnboardingStateStore
    ValueTask<AnchorOnboardingState?> GetAsync(TeamId teamId, CancellationToken ct)
    Task SaveAsync(AnchorOnboardingState state, CancellationToken ct)
    Task<bool> IsFirstLaunchAsync(TeamId teamId, CancellationToken ct)   (returns true iff GetAsync returns null)
```

#### `SqliteAnchorOnboardingStateStore` — v1 SQLite-backed implementation (post-pivot)

Stores `AnchorOnboardingState` rows in the `anchor_onboarding_state` table (created by `Migrations/0001_create_anchor_onboarding_state.sql`). Schema:

```sql
CREATE TABLE IF NOT EXISTS anchor_onboarding_state (
    team_id TEXT NOT NULL PRIMARY KEY,
    step TEXT NOT NULL,                          -- stable string code per CRDT §5
    identity_provisioned_at TEXT,                -- ISO 8601 UTC; nullable
    recovery_contacts_registered_at TEXT,        -- ISO 8601 UTC; nullable
    completed_at TEXT,                           -- ISO 8601 UTC; nullable
    version INTEGER NOT NULL DEFAULT 0,          -- monotonic per-replica counter
    created_at TEXT NOT NULL,                    -- ISO 8601 UTC
    updated_at TEXT NOT NULL                     -- ISO 8601 UTC
);
```

Bridge accesses the SQLite database through the existing connection-management abstraction (the connection is owned by the Tauri shell's sqlx pool per W#60 P3 PR 2, but Bridge — when colocated — receives a connection-string or handle via startup config). Verify: `apps/anchor-tauri/src-tauri/src/db.rs` (or equivalent) exposes the SQLite path; Bridge consumes it via `Configuration["Storage:SqlitePath"]`.

Step value is persisted as the stable string code (`"welcome"` / `"identity-bootstrapped"` / etc.); never as an integer ordinal.

Provide a defensive `SaveAsync` that validates `state.Step` transitions against the previous saved state via `AnchorOnboardingStepExtensions.IsValidTransition(from, to)`; throws `AnchorOnboardingStateException` on invalid transition (this is the Tier-1 validation rail). Also asserts `state.Version > existing.Version` on update; rejects stale writes (Tier-1 crash-safety).

**Critical:** the primary key is `team_id`, so a multi-team install (per W#42 multi-team workspace) has independent onboarding state per team. The active team is obtained via `IActiveTeamAccessor.GetActiveAsync(ct)` (existing surface) and passed in by the bootstrap service.

**Migration discipline:** the new table is additive; if the schema needs to evolve (e.g. a `tier` field per §"Open questions" Q2), use a new migration file `0002_*.sql` rather than ALTER-ing the existing migration. Migrations are run on Tauri shell startup (existing W#60 P3 PR 2 infrastructure); Bridge does not run migrations itself.

#### `IAnchorIdentityBootstrapService` — orchestrator interface

```
interface IAnchorIdentityBootstrapService
    Task<AnchorOnboardingState> EnsureBootstrappedAsync(CancellationToken ct)
        // Idempotent. If state.Step is welcome, advances through identity-bootstrapped (by
        // invoking IRootSeedProvider.GetRootSeedAsync). Returns the final state.
        // Does NOT advance past identity-bootstrapped; recovery-contact registration is PR 2's job.
    Task<bool> IsFirstLaunchAsync(CancellationToken ct)
        // True iff the active team has no AnchorOnboardingState row OR state.Step == welcome.
```

#### `AnchorIdentityBootstrapService` — MAUI implementation

Dependencies (constructor-injected):
- `IRootSeedProvider` (kernel-security; existing) — for `GetRootSeedAsync()` which generates+persists the seed on first call
- `IActiveTeamAccessor` (kernel-runtime; existing) — for the current `TeamId`
- `IAnchorOnboardingStateStore` (new; this PR)
- `IIdentityProvisionedEventEmitter` (new; this PR)
- `IRecoveryClock` (foundation-recovery; existing — reuse the W#15 clock for timestamps so they share semantics with `RecoveryEvent.AttestedAt`)
- `ILogger<AnchorIdentityBootstrapService>`

`EnsureBootstrappedAsync(ct)` algorithm:

1. Load state via `_stateStore.GetAsync(_activeTeam.GetActiveAsync().TeamId, ct)`. If null, initialize a new state at `Step == welcome`.
2. If `state.Step != welcome`, return state (idempotent; already done).
3. Invoke `var rootSeed = await _rootSeedProvider.GetRootSeedAsync(ct);` — this materializes the seed if it doesn't yet exist (the Stronghold-IPC first-launch path per `StrongholdRootSeedProvider`; was `KeystoreRootSeedProvider` pre-pivot).
4. Compute the install's public identity fingerprint for audit logging: `var publicKeyFingerprint = Convert.ToHexString(SHA256.HashData(rootSeed.Span))[..8];` (≤8-char fingerprint per W#67 PR 5 MAJOR-2 convention). **Do NOT log the seed itself; never log raw key material.**
5. Emit `IdentityProvisioned` audit event via `_eventEmitter.EmitAsync(new IdentityProvisionedPayload(...))` carrying `TeamId`, `PublicKeyFingerprint`, `ProvisionedAt = _clock.UtcNow()`. Payload is structured (no raw seed bytes, no free-form strings); see `AnchorIdentityBootEvents.cs` schema below.
6. Update state: `Step = identity-bootstrapped`, `IdentityProvisionedAt = _clock.UtcNow()`, `Version += 1`.
7. `await _stateStore.SaveAsync(state, ct)`.
8. Return state.

**Crash-safety:** if step 5 emits but step 7 fails (or vice versa), the system enters a non-progressed state. On next `EnsureBootstrappedAsync`, we re-enter at step 1 with no state change observed. The `IRootSeedProvider` call is idempotent (returns the cached seed); the audit-emit is idempotent at the audit-trail layer (`IAuditTrail` is append-only and de-duplicates by event id — verify in PR review). The fix is **PR-1-test-required**: see test cases below.

#### `AnchorIdentityBootEvents.cs` — typed payload records

```
record IdentityProvisionedPayload
    TeamId               TeamId
    PublicKeyFingerprint string             (≤8 hex chars; SHA-256 prefix of root-seed public key)
    ProvisionedAt        DateTimeOffset
    InstallId            string             (the persisted Preferences-stored install id; never the seed)
```

```
record RecoveryContactRegisteredPayload   (used in PR 2; defined here so the file ships in PR 1)
    TeamId               TeamId
    TrusteeNodeId        string             (the IRecoveryCoordinator-canonical trustee node id)
    PartyId              string             (or null if Party stub not yet linked)
    TrusteeKeyFingerprint string            (≤8 hex chars; SHA-256 prefix of trustee Ed25519 public key)
    RegisteredAt         DateTimeOffset
```

#### Audit event type addition

`packages/kernel-audit/AuditEventType.cs` (or wherever the audit-event-type registry lives — locate via `grep -rln "AuditEventType.RecoveryRekey" packages/`) gains two new constants:

```
IdentityProvisioned
RecoveryContactRegistered     (used in PR 2)
```

The W#67 hand-off precedent (`AuditEventType.RecoveryRekey` + `RecoveryRekeyPayload`) is the canonical pattern: enum-constant + typed payload record in `packages/kernel-audit/Payloads/`. Follow it exactly.

**Cross-package change discipline:** this audit-type addition is the only change in `packages/kernel-audit/` from this hand-off. Use commit subject `feat(kernel-audit): add IdentityProvisioned + RecoveryContactRegistered event types per anchor-identity-boot hand-off` for the kernel-audit-only commit; ship it as part of PR 1's branch (single PR; multiple commits acceptable per existing project commit-cadence). If the audit-type-registry has a constraint about same-PR-cross-package changes, file `cob-question-*`.

#### Razor page deliverables — RETARGETED to React TSX in PR 2 under the pivot

The pre-pivot version of this hand-off shipped `FirstLaunchWelcomePage.razor` + `IdentityBootstrapPage.razor` here in PR 1. Under the Tauri-first pivot, these UX surfaces are **TSX components in `apps/anchor-tauri/src/pages/IdentityBoot/`**, shipped in **PR 2** (not PR 1). PR 1 ships ONLY the Bridge endpoints they consume.

See PR 2 below for the React TSX deliverable specs (`FirstLaunchWelcomePage.tsx`, `IdentityBootstrapPage.tsx`, `services/identityBoot.ts`, `FirstLaunchGuard.tsx`). The UX framing (text, CTA order, A11y patterns) is preserved verbatim under the pivot; only the rendering technology changes.

**PR 1 endpoint surface (the contract the React side will call in PR 2):**

| Method + Route | Purpose | Request shape | Response shape |
|---|---|---|---|
| `POST /api/v1/identity-boot/ensure-bootstrapped` | Idempotent: advances onboarding from `welcome` → `identity-bootstrapped` if not already there. Triggers seed generation + audit emission. | (none; team derived from session) | `{ "step": "identity-bootstrapped", "identityProvisionedAt": "2026-05-17T14:35:00Z", "publicKeyFingerprint": "a1b2c3d4" }` |
| `GET /api/v1/identity-boot/state` | Returns the current onboarding state for the active team. | (none) | `{ "step": "welcome" \| "identity-bootstrapped" \| ..., "identityProvisionedAt": "...", "recoveryContactsRegisteredAt": "...", "completedAt": "..." }` |
| `GET /api/v1/identity-boot/is-first-launch` | True iff state is null OR step is `welcome`. Used by the React router-guard. | (none) | `{ "isFirstLaunch": true \| false }` |

All endpoints require an authenticated session (the existing Bridge auth middleware applies; team is derived from `IActiveTeamAccessor.GetActiveAsync`). Endpoints emit audit events via `IAuditTrail.AppendAsync` per the spec; the React side does NOT emit audit events directly.

**Critical UX note (preserved for PR 2 reference):** the entire bootstrap should take <200ms in the happy path (Stronghold-IPC round-trip + RNG draw + sqlite write). If `POST /ensure-bootstrapped` takes >2s, log a warning (`STRONGHOLD_SLOW`) — the Stronghold vault open is probably the culprit.

#### DI registrations (inline; PR 5 extracts)

In `accelerators/bridge/Sunfish.Bridge/Program.cs`, after the existing recovery-coordinator registrations and gated on the colocated-Bridge mode:

```csharp
if (builder.Configuration.GetValue<BridgeMode>("BridgeMode") == BridgeMode.Colocated)
{
    builder.Services.AddSingleton<IStrongholdIpcChannel, HttpStrongholdIpcChannel>();
    builder.Services.AddSingleton<IRootSeedProvider, StrongholdRootSeedProvider>();
}
// else: existing KeystoreRootSeedProvider / HSM-backed impl stays registered

builder.Services.AddSingleton<IAnchorOnboardingStateStore, SqliteAnchorOnboardingStateStore>();
builder.Services.AddSingleton<IIdentityProvisionedEventEmitter, IdentityProvisionedEventEmitter>();
builder.Services.AddSingleton<IAnchorIdentityBootstrapService, AnchorIdentityBootstrapService>();
```

Plus the endpoint mapping (after the existing `MapListingsEndpoints()` call):

```csharp
app.MapIdentityBootEndpoints();
```

PR 5 replaces these inline registrations with a single `builder.Services.AddAnchorIdentityBoot();` + `app.MapAnchorIdentityBoot();` umbrella. PR 1 leaves them inline so the cross-PR diff stays cohesive.

**Verify**: the existing `BridgeMode` enum exists at `accelerators/bridge/Sunfish.Bridge/BridgeMode.cs`. If it does not yet expose a `Colocated` value, file `cob-question-*` — adding the enum value is in scope here, but the spec needs XO confirmation that "Colocated" is the right naming.

#### Test plan (~16–20 tests for PR 1)

**`AnchorIdentityBootstrapServiceTests.cs`** (~8–10 tests):

- `EnsureBootstrappedAsync_FreshInstall_AdvancesToIdentityBootstrapped`
- `EnsureBootstrappedAsync_AlreadyBootstrapped_ReturnsExistingStateUnchanged`
- `EnsureBootstrappedAsync_PersistsStateBeforeReturning` (calls service; asserts state-store.SaveAsync was invoked)
- `EnsureBootstrappedAsync_EmitsIdentityProvisionedAuditBeforeStateSave` (order: emit → save; verifies via Moq InSequence so the test catches a regression that reverses the order — Tier-1 invariant: audit lands before state advance)
- `EnsureBootstrappedAsync_OnAuditEmitFailure_DoesNotSaveState` (state remains at welcome on next call; allows retry)
- `EnsureBootstrappedAsync_OnStateSaveFailure_StillEmittedAudit` (verifies that the audit event was emitted even though state save failed; next call re-attempts the full sequence including a fresh audit emit — this is acceptable because the audit-trail layer is append-only and the consumer can deduplicate at query time; document this behavior in the service XML doc)
- `IsFirstLaunchAsync_ReturnsTrue_WhenNoState`
- `IsFirstLaunchAsync_ReturnsTrue_WhenStateIsWelcomeStep`
- `IsFirstLaunchAsync_ReturnsFalse_AfterBootstrap`
- `EnsureBootstrappedAsync_LogsPublicKeyFingerprint_NeverRawSeed` (verifies `_logger` captures contain 8-hex-char fingerprint, never any 32-byte sequence; security-engineering council line item)

**`SqliteAnchorOnboardingStateStoreTests.cs`** (~5–7 tests; uses in-memory sqlite via `Microsoft.Data.Sqlite` for tests):

- `GetAsync_ReturnsNull_WhenNoEntry`
- `SaveAsync_RoundtripsState`
- `SaveAsync_RejectsInvalidStepTransition` (welcome → complete throws `AnchorOnboardingStateException`)
- `SaveAsync_AllowsLinearAdvance` (welcome → identity-bootstrapped → recovery-contacts-registered → complete)
- `SaveAsync_PerTeamIsolation` (two TeamIds; each has independent state; updating one does not affect the other)
- `SaveAsync_BumpsVersionMonotonically` (each save increments Version; not asserting CRDT vector — that's a follow-on when multi-replica enters scope)
- `SaveAsync_PersistsStepAsStableStringCode` (Step round-trips as `"identity-bootstrapped"` not as int ordinal — verifies CRDT §5 stable-code persistence in SQLite TEXT column)
- `SaveAsync_RejectsStaleVersion` (Tier-1 crash-safety: writing with Version <= existing throws)

**`HttpStrongholdIpcChannelTests.cs`** (~5–6 tests; new under pivot):

- `GetSeedAsync_HappyPath_RoundTripsViaMockTauriServer`
- `GetSeedAsync_RejectsWithoutBearerToken` (returns 401)
- `GetSeedAsync_RejectsConnectionFromNon127001` (mock server bound to 0.0.0.0 fails the binding-check predicate)
- `GetSeedAsync_HandlesStrongholdNotInitialized` (Tauri-side returns a typed error; channel surfaces it as `StrongholdNotInitializedException`)
- `SeedExistsAsync_DoesNotReturnSeedBytes` (parses the JSON response; asserts no `seed` field present even on success)

**`IdentityBootEndpointsTests.cs`** (~4–5 tests; new under pivot; integration-level using `WebApplicationFactory`):

- `PostEnsureBootstrapped_FreshState_Returns200_WithIdentityBootstrappedStep`
- `PostEnsureBootstrapped_AlreadyBootstrapped_Returns200_Unchanged` (idempotency)
- `GetState_NoState_Returns200_WithWelcomeStep`
- `GetIsFirstLaunch_AfterBootstrap_ReturnsFalse`
- `PostEnsureBootstrapped_RequiresAuthenticatedSession` (401 without auth)

**`AnchorOnboardingStepTransitionTests.cs`** (~4–5 tests):

- `IsValidTransition_WelcomeToIdentityBootstrapped_True`
- `IsValidTransition_IdentityBootstrappedToRecoveryContactsRegistered_True`
- `IsValidTransition_RecoveryContactsRegisteredToComplete_True`
- `IsValidTransition_BackTransitions_False` (each pair tested in a Theory)
- `IsValidTransition_SkipForward_False` (welcome → complete is invalid; welcome → recovery-contacts-registered is invalid)

**`IdentityProvisionedEventEmitterTests.cs`** (~3–4 tests):

- `EmitAsync_AppendsToAuditTrail` (verifies `_auditTrail.AppendAsync` invoked once with `IdentityProvisioned` event type)
- `EmitAsync_PayloadHasPublicKeyFingerprint_NotRawKey` (security-engineering council line item)
- `EmitAsync_HonorsCancellationToken` (cancellation propagates)

#### Standing-pattern annotation

The PR commit body includes per `standing-approved-patterns.md` §"How a PR matches a pattern":

```
@standing-pattern: pattern-001
```

For the audit-event-type addition commit (the cross-package one), no standing pattern applies (it's a one-line enum addition); no annotation needed.

#### Halt conditions for PR 1

See §Halt-conditions H1, H2, H3, H4 below.

#### PASS gate for PR 1

- All ~25–30 tests green (the post-pivot count is higher than pre-pivot's 16–20 because of the new HTTP/IPC + endpoint integration tests).
- Security-engineering council ratifies (no Blocking; ≤2 Major resolved before merge). Council MUST explicitly approve: the Stronghold-IPC bearer-token + 127.0.0.1-binding implementation; the "seed bytes never returned by `seed_exists`" invariant; the audit-emit-before-state-save ordering.
- .NET architect council ratifies (DI lifetimes correct; endpoint-mapping pattern matches existing Bridge convention; IPC-channel abstraction is testable).
- **Manual smoke test** on Surface Pro (CO's primary acceptance platform) AND on macOS dev box. Procedure: wipe `~/.local/share/anchor-tauri/anchor.stronghold` (or platform equivalent) + the `anchor_onboarding_state` SQLite row; launch Anchor Tauri; confirm Bridge boots; `curl http://localhost:<bridgePort>/api/v1/identity-boot/is-first-launch` returns `{"isFirstLaunch": true}`; `curl -X POST .../ensure-bootstrapped` returns `200` with `step: "identity-bootstrapped"`; second call to same endpoint returns the same state unchanged (idempotency); third-call `GET .../state` confirms `identityProvisionedAt` is set; restart Anchor → `is-first-launch` returns `false`.
- No raw seed bytes appear in any log output (grep the smoke-test log + Stronghold-IPC mock-server log for any hex sequence >16 chars; expect zero matches outside the public-key-fingerprint pattern).
- Stronghold-IPC channel bearer-token rejection verified (manual `curl` without the token returns 401).

---

### PR 2 (TSX; Tauri React frontend) — First-launch welcome + bootstrap UX

**Estimated effort:** ~3–4h
**Scope:** new `apps/anchor-tauri/src/pages/IdentityBoot/` directory; React TSX components `FirstLaunchWelcomePage.tsx` + `IdentityBootstrapPage.tsx`; first-launch router guard `apps/anchor-tauri/src/components/FirstLaunchGuard.tsx`; typed Bridge client `apps/anchor-tauri/src/services/identityBoot.ts`; Vitest unit tests. Route wiring added to `apps/anchor-tauri/src/app.tsx` (`/identity/welcome` + `/identity/bootstrap` route definitions + `<FirstLaunchGuard>` wrapper around the dashboard routes).
**Commit subject:** `feat(anchor-identity-boot): React first-launch welcome + bootstrap pages + router guard`
**Branch:** `dev/anchor-identity-boot-pr2-react-welcome-bootstrap`
**Council:** Not required (UX-composition scope; no key handling on the React side).

#### Files to create

```
apps/anchor-tauri/src/pages/IdentityBoot/
├── FirstLaunchWelcomePage.tsx              (entry; routes to /identity/bootstrap)
├── FirstLaunchWelcomePage.test.tsx
├── IdentityBootstrapPage.tsx               (calls services/identityBoot.ensureBootstrapped; spinner; nav)
└── IdentityBootstrapPage.test.tsx

apps/anchor-tauri/src/components/
├── FirstLaunchGuard.tsx                    (wraps Outlet; redirects to /identity/welcome if first-launch)
└── FirstLaunchGuard.test.tsx

apps/anchor-tauri/src/services/
├── identityBoot.ts                         (typed fetch wrapper for Bridge endpoints; getState, isFirstLaunch, ensureBootstrapped)
└── identityBoot.test.ts
```

Edits (not new files) to `apps/anchor-tauri/src/app.tsx`:
- Import the new pages + guard.
- Add `/identity/welcome` + `/identity/bootstrap` route definitions outside the guard (so the guard's redirect target is itself reachable).
- Wrap the existing app-shell route tree in `<FirstLaunchGuard>` so dashboard / properties / leases / accounting / etc. all redirect to the wizard on first launch.

#### `services/identityBoot.ts` — typed Bridge client

```typescript
// Mirrors apps/anchor-tauri/src/api/erpnext.ts style. Uses fetch with credentials: 'include'
// so the Bridge auth cookie is sent on every call. Endpoints defined by PR 1.

export type AnchorOnboardingStep =
  | 'welcome'
  | 'identity-bootstrapped'
  | 'recovery-contacts-registered'
  | 'complete'

export interface AnchorOnboardingState {
  step: AnchorOnboardingStep
  identityProvisionedAt: string | null
  recoveryContactsRegisteredAt: string | null
  completedAt: string | null
}

export async function getState(): Promise<AnchorOnboardingState> {
  const r = await fetch('/api/v1/identity-boot/state', { credentials: 'include' })
  if (!r.ok) throw new Error(`identity-boot state failed: ${r.status}`)
  return await r.json()
}

export async function isFirstLaunch(): Promise<boolean> {
  const r = await fetch('/api/v1/identity-boot/is-first-launch', { credentials: 'include' })
  if (!r.ok) throw new Error(`is-first-launch failed: ${r.status}`)
  const { isFirstLaunch } = await r.json()
  return isFirstLaunch
}

export async function ensureBootstrapped(): Promise<AnchorOnboardingState> {
  const r = await fetch('/api/v1/identity-boot/ensure-bootstrapped', {
    method: 'POST',
    credentials: 'include',
  })
  if (!r.ok) throw new Error(`ensure-bootstrapped failed: ${r.status}`)
  return await r.json()
}
```

#### `FirstLaunchWelcomePage.tsx`

Route: `/identity/welcome`

Shows two choices (preserved verbatim from the pre-pivot Razor spec):

1. **"Set up a new identity"** — primary CTA. On click, `navigate('/identity/bootstrap')` via `useNavigate()` from react-router-dom.
2. **"Join an existing team"** — secondary CTA. On click, `navigate('/onboarding')` (existing joiner path; not in scope for this hand-off).

Text framing (per ADR 0088 §"Tiered runtime model" §Light):

> Welcome to Sunfish Anchor.
> This is your private local-first workspace. Your data lives on this device.
> Choose how you want to get started.

Footer link or inline expander: "What's the difference?" — defers to the user-guide page (PR 5).

**A11y:** primary CTA has `aria-describedby` pointing to the explanation paragraph. Keyboard-navigable; Tab order = `[primary, secondary, help-link]`. Use the existing `@sunfish/ui-react` button primitives for consistency.

**No first-launch-guard on this page itself** — it's the entry/redirect target. The guard is mounted higher in the route tree.

#### `IdentityBootstrapPage.tsx`

Route: `/identity/bootstrap`

On mount (`useEffect` with empty deps array; equivalent to `OnInitializedAsync`):

1. Call `await ensureBootstrapped()`.
2. On success: `navigate('/identity/recovery-contacts', { replace: true })` (PR 4's route; before PR 4 ships, fall back to `navigate('/', { replace: true })` with a one-time toast "Identity ready — recovery setup coming in next update").
3. On failure: render an error state with the fingerprint or `N/A` reference; retry button calls `ensureBootstrapped()` again.

Renders a `<Spinner aria-busy="true" aria-live="polite">Setting up your identity…</Spinner>` during the call. No user interaction in the happy path.

**A11y:** `aria-busy="true"` on container while loading; `aria-live="polite"` region; error state uses `role="alert"`.

#### `FirstLaunchGuard.tsx`

```tsx
import { useEffect, useState } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { isFirstLaunch } from '@/services/identityBoot'

export function FirstLaunchGuard() {
  const [decision, setDecision] = useState<'loading' | 'pass' | 'redirect'>('loading')

  useEffect(() => {
    let cancelled = false
    isFirstLaunch()
      .then((isFresh) => {
        if (!cancelled) setDecision(isFresh ? 'redirect' : 'pass')
      })
      .catch(() => {
        // Fail-open: if the check itself errors, render the app shell so the user
        // isn't stranded. The wizard will still be reachable via the nav entry.
        if (!cancelled) setDecision('pass')
      })
    return () => { cancelled = true }
  }, [])

  if (decision === 'loading') return null  // brief flash; <100ms typical
  if (decision === 'redirect') return <Navigate to="/identity/welcome" replace />
  return <Outlet />
}
```

The `loading` state intentionally renders nothing for the brief gap (<100ms on Surface Pro per benchmarks); a placeholder spinner is unnecessary and would be visually noisy on every navigation. **Fail-open** policy is deliberate: if the Bridge endpoint is unreachable, the user enters the app and the wizard remains accessible via the nav entry (PR 5 wires that nav entry).

#### Test plan (~10–12 Vitest tests for PR 2)

- `FirstLaunchWelcomePage.test.tsx`: renders both CTAs; primary navigates to `/identity/bootstrap`; secondary navigates to `/onboarding`; A11y roles present.
- `IdentityBootstrapPage.test.tsx`: spinner rendered during call; calls `ensureBootstrapped` on mount; navigates to `/identity/recovery-contacts` on success; renders error state on failure; retry button re-invokes `ensureBootstrapped`.
- `FirstLaunchGuard.test.tsx`: renders `<Outlet />` when `isFirstLaunch` returns false; renders `<Navigate>` to `/identity/welcome` when true; fails-open (renders Outlet) when the call rejects.
- `services/identityBoot.test.ts`: each fetch wrapper handles 200, 401, 500; parses the JSON correctly.

#### PASS gate for PR 2

- All ~10–12 tests green.
- Manual smoke (paired with PR 1 PASS gate): from a fresh-wiped Anchor Tauri install, launch the app → wizard fires → identity provisioned → navigates to a placeholder (since PR 4 hasn't shipped yet, this lands at `/` with a toast; OR if PR 4 has shipped, lands at `/identity/recovery-contacts`).
- Next launch: guard fires → `isFirstLaunch` returns false → renders the app shell as normal (no wizard redirect).

---

### PR 3 (C#; Bridge) — Recovery contact registration backend + W#67 substrate composition

**Estimated effort:** ~5–7h (assuming §G2 cleared — the W#67 substrate is on `main`)
**Scope:** new `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/Recovery/` directory; `AnchorRecoveryContactService` (Bridge-feature wrapper, not a foundation interface); `RecoveryContactEndpoints.cs` exposing the React-side surface; `Party` linkage per §G4; `RecoveryContactRegistered` + `RecoveryContactRemoved` audit event emission.
**Commit subject:** `feat(anchor-identity-boot): recovery contact registration backend + W#67 substrate composition @standing-pattern: pattern-001`
**Branch:** `dev-win/anchor-identity-boot-pr3-recovery-contacts-backend`
**Council:** Not required by default (substrate-composition scope). Trigger council ONLY if Halt §H6 fires (trustee-online constraint blocked) — that scope shift would invalidate ADR 0046-A6 §A6.5's UX-acceptance argument.

#### Gate verification

Confirm §G2 cleared (all W#67 deliverables on `main`). Run:
```bash
grep -n "SetupTrusteeAsync\|TrusteeEncryptedSeed\|TrusteeDHPublicKey" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-recovery/*.cs
```
Each symbol must appear. Any missing → STOP PR 2; PR 1 lands without PR 2 and the wizard's "identity-bootstrapped → recovery-contacts-registered" transition is gated until W#67 catches up.

#### Files to create

```
accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/Recovery/
├── IAnchorRecoveryContactService.cs            (Bridge-feature-internal interface; not a foundation API)
├── AnchorRecoveryContactService.cs             (composes IRecoveryCoordinator + IPartyWriteService)
├── AnchorRecoveryContact.cs                    (record: Bridge-side projection of trustee + party linkage)
├── AnchorRecoveryContactId.cs                  (ULID strong-id)
├── IAnchorRecoveryContactRepository.cs         (sqlite-backed in v1)
├── SqliteAnchorRecoveryContactRepository.cs    (uses the Tauri-shell SQLite handle per the IPC pattern)
├── AnchorRecoveryContactRegistrationException.cs
├── RecoveryContactRegisteredEventEmitter.cs    (uses IAuditTrail; parallel to IdentityProvisionedEventEmitter from PR 1)
├── RecoveryContactEndpoints.cs                 (minimal-API: POST /register, GET /list, POST /remove, POST /advance-onboarding)
└── Migrations/
    └── 0002_create_anchor_recovery_contacts.sql

accelerators/bridge/Sunfish.Bridge.Tests/Features/IdentityBoot/Recovery/
├── AnchorRecoveryContactServiceTests.cs        (~10–12 tests)
├── SqliteAnchorRecoveryContactRepositoryTests.cs (~4–5 tests; in-memory sqlite)
├── RecoveryContactRegisteredEventEmitterTests.cs (~3 tests)
└── RecoveryContactEndpointsTests.cs            (~4–5 tests; WebApplicationFactory)
```

PR 3 deliverables are backend-only. The React TSX pages that consume `RecoveryContactEndpoints` (`RecoveryContactRegistrationPage.tsx` + `RecoveryContactReviewPage.tsx` + `RecoveryContactCard.tsx`) ship in **PR 4** (see below).

#### `AnchorRecoveryContact` record

```
record AnchorRecoveryContact
    Id                       AnchorRecoveryContactId   (ULID)
    TeamId                   TeamId
    TrusteeNodeId            string                    (canonical TrusteeDesignation.NodeId)
    PartyId                  PartyId                   (link to blocks-people-foundation OR local stub)
    DisplayName              string                    (cached from Party for offline UX; canonical truth lives in Party)
    TrusteeEdPublicKey       byte[]                    (32 bytes; from W#67 TrusteeDesignation.PublicKey)
    TrusteeDHPublicKey       byte[]                    (32 bytes; from W#67 TrusteeDesignation.DHPublicKey)
    RegisteredAt             DateTimeOffset
    Version                  long                      (CRDT §3)
    DeletedAt                DateTimeOffset?           (soft-delete tombstone per CRDT §2)
    DeletedBy                string?
    DeletedReason            string?
```

Note: this record duplicates some trustee fields (PublicKey, DHPublicKey) that also live in `TrusteeDesignation` via `IRecoveryCoordinator`'s state. The Anchor-side copy is a **read-cache** for offline UX (so the contact list renders without an `IRecoveryCoordinator` round-trip). Canonical truth remains in the coordinator's state store. The duplication is bounded — contacts are append-only post-registration; if a trustee key rotates (Phase 3 of A6, deferred), the Anchor cache invalidates on the next coordinator query.

#### `IAnchorRecoveryContactService` interface

```
interface IAnchorRecoveryContactService
    Task<AnchorRecoveryContact> RegisterAsync(
        string displayName,
        string email,
        string? phoneE164,
        byte[] trusteeEdPublicKey,
        byte[] trusteeDHPublicKey,
        string trusteeNodeId,
        CancellationToken ct)
    Task<IReadOnlyList<AnchorRecoveryContact>> ListAsync(CancellationToken ct)
        // Returns non-deleted contacts for the active team.
    Task RemoveAsync(AnchorRecoveryContactId id, string reason, CancellationToken ct)
        // Soft-delete: sets DeletedAt + emits audit event. Does NOT revoke
        // the trustee's encrypted seed copy (per ADR 0046-A6 §A6.1 §"No revocation");
        // the UX surface must surface this fact to the user before they confirm
        // (the Review page enforces this via a confirmation dialog).
    Task AdvanceOnboardingOnceMinimumContactsRegisteredAsync(CancellationToken ct)
        // When >= 1 successful registration exists for the team, advances
        // AnchorOnboardingState from identity-bootstrapped to recovery-contacts-registered.
        // Idempotent.
```

Implementation responsibilities (`AnchorRecoveryContactService.RegisterAsync`):

1. Validate inputs Tier-1 (email per RFC 5322; phone per E.164 if provided; display name non-empty; both key fields exactly 32 bytes; trustee node id non-empty).
2. Create the `Party` row (or look up if PartyId already exists for the contact's email; party-dedup is a follow-on but we MUST avoid duplicate-Party-per-trustee in the simple happy path — `IPartyReadModel.FindByExactEmailAsync(email)` returns existing if present).
3. Invoke `IRecoveryCoordinator.DesignateTrusteeAsync(trusteeNodeId, trusteeEdPublicKey, trusteeDHPublicKey, ct)` per W#67 PR 5 widened signature (this is the MAJOR-2 binding fix that ensures the DH key cross-check ratchets later during `SubmitAttestationAsync`).
4. Invoke `IRecoveryCoordinator.SetupTrusteeAsync(trusteeNodeId, encryptedSeed, ct)` per ADR 0046-A6 §A6.5 algorithm:
   a. Retrieve root seed via `IRootSeedProvider.GetRootSeedAsync(ct)`.
   b. Compute `_x25519KeyAgreement.Box(rootSeed, trusteeDHPublicKey, ownerEphPriv)` — see `kernel-security/Crypto/IX25519KeyAgreement.cs` (existing; W#67 PR 1).
   c. Construct `TrusteeEncryptedSeed(trusteeNodeId, ownerEphX25519Pub, ciphertext, nonce)`.
   d. Pass to `SetupTrusteeAsync`.
5. Persist the local `AnchorRecoveryContact` via `_repository.SaveAsync(...)`.
6. Emit `RecoveryContactRegistered` audit event via `_eventEmitter.EmitAsync(payload)` with `TrusteeKeyFingerprint = Convert.ToHexString(SHA256.HashData(trusteeEdPublicKey))[..8]`.
7. Return the `AnchorRecoveryContact`.

**Critical:** the root seed is held in memory ONLY during step 4 (encryption). Once `Box` returns the ciphertext, the plaintext seed reference is released. The `ReadOnlyMemory<byte>` from `GetRootSeedAsync` is a view into the keystore-managed buffer; we do not copy it. The `Box` call consumes the view by-value (NSec's `Key.Import` does an internal copy; the original view is not retained after `Box` returns).

**Trustee-online constraint** (per ADR 0046-A6 §A6.5 §"Trustee-online requirement for setup"): the encryption requires the trustee's DH public key, which must be obtained out-of-band (via the trustee's identity bundle per ADR 0046-A6 §A6.6). For v1, the UI collects the trustee's DH key as part of a **pasted identity-bundle string** (the trustee runs the Anchor "Generate identity bundle" flow on their device, pastes the result here). This is the UX from `TrusteeSetupPage.razor` per the W#67 PR 5 spec — we do NOT duplicate that page; we COMPOSE it. See PR 2 §"Page-composition note" below.

#### Razor page deliverables — RETARGETED to React TSX in PR 4 under the pivot

The pre-pivot version of this hand-off shipped `RecoveryContactRegistrationPage.razor` + `RecoveryContactReviewPage.razor` here in PR 2 (now PR 3). Under the Tauri-first pivot, these UX surfaces are **React TSX components in `apps/anchor-tauri/src/pages/IdentityBoot/`**, shipped in **PR 4** (see below).

The original Razor page spec is preserved below for reference; PR 4 implements the same UX semantics in TSX (using `@sunfish/ui-react` primitives, react-hook-form for the form fields, and fetch via `services/recoveryContacts.ts` typed Bridge client).

##### Original Razor spec (preserved for PR 4 reference)

Route: `/identity/recovery-contacts`
Inject: `IAnchorRecoveryContactService`, `IAnchorOnboardingStateStore`, `IActiveTeamAccessor`, `NavigationManager`, `IStringLocalizer<SharedResource>`.

Page contains:

1. **Header** — "Set up your recovery contacts."
2. **Explanation paragraph** — local-first framing: "If you lose this device, your recovery contacts can help you regain access to your data. You need at least one; we strongly recommend three for a safer recovery quorum."
3. **Threat-model acknowledgement** (mandatory; per ADR 0046-A6 §A6.1 §"Threat model expansions"):
   > **Important:** Anyone you choose as a recovery contact will hold an encrypted copy of the key that protects ALL your data. If their device is compromised, an attacker could use their copy to access your data, and you will not have a way to revoke their copy without setting up a new identity. Choose people whose devices you trust as much as your own.
   This text MUST appear verbatim (do not paraphrase). User must check a "I understand" checkbox before the "Add contact" CTA enables.
4. **Add contact form** — fields: Display name (required), Email (required), Phone (optional, E.164 format), Identity bundle (required; multi-line textarea — paste the trustee's identity-bundle string per W#67 / ADR 0046-A6 §A6.6).
5. **Identity-bundle parsing** — the Anchor-side parser is `Sunfish.Anchor.Services.Pairing.QrOnboardingService.DecodePayloadAsync` (existing surface; extract trustee Ed25519 + X25519 keys + nodeId from the bundle). Use it directly; do NOT duplicate the parser.
6. **Submit handler** — calls `IAnchorRecoveryContactService.RegisterAsync(...)`. On success: append the contact to the page's local list; clear the form; show a one-time toast "Contact registered. Setup more, or continue to finish."
7. **Continue CTA** — disabled until ≥1 contact registered. On click: `_service.AdvanceOnboardingOnceMinimumContactsRegisteredAsync(ct)` → navigate to `/`.

**Page-composition note:** if the W#67 PR 5 `TrusteeSetupPage.razor` exists, this hand-off's `RecoveryContactRegistrationPage.razor` should NOT duplicate it. Instead, this page should be the **wizard-flow wrapper** that hosts the W#67 page's form (via Razor `<TrusteeSetupPage @rendermode="..." />` component embedding) AND adds the wizard-specific Continue CTA + onboarding-state advancement. If `TrusteeSetupPage.razor` was NOT yet built (W#67 PR 5 still in flight at the time of this PR 2), build the form inline here per the spec above; when W#67 PR 5 ships, refactor to embed. **Decision principle:** prefer inline-now + refactor-later over coupling this PR's land-date to W#67 PR 5's land-date.

**A11y:** form fields have `aria-required` + visible labels; the threat-model checkbox has `aria-describedby` pointing to the threat-model paragraph; the Continue CTA has `aria-disabled` toggling based on contact-count; the contact list uses `role="list"` + `role="listitem"`.

#### `RecoveryContactReviewPage.razor`

Route: `/identity/recovery-contacts/review`
Inject: `IAnchorRecoveryContactService`, `NavigationManager`, `IStringLocalizer<SharedResource>`.

Renders the current `AnchorRecoveryContact` list via `RecoveryContactCard.razor` (shared component). Each card shows: display name, email, registered timestamp, key fingerprint (8 hex chars), Remove button.

Remove button click → confirmation dialog (modal) reiterating the no-revocation truth:

> Removing this contact only hides them from this list. They still hold an encrypted copy of your recovery key and could still help recover your data if you initiated a recovery. To truly revoke a contact, you would need to set up a new identity entirely.
> Do you want to hide this contact from your list?

On confirm: `_service.RemoveAsync(id, reason: "user_removed_from_review_page", ct)`. The contact is soft-deleted; the audit event `RecoveryContactRemoved` is emitted (add this audit-event type alongside `RecoveryContactRegistered`).

This page is accessible from the main Anchor navigation as "Recovery setup" (PR 3 wires the nav entry). It is NOT part of the first-launch wizard; it's a manage-existing surface.

#### DI registrations (inline; PR 5 extracts into the umbrella)

In `accelerators/bridge/Sunfish.Bridge/Program.cs`, after PR 1's inline registrations:

```csharp
builder.Services.AddSingleton<IAnchorRecoveryContactRepository, SqliteAnchorRecoveryContactRepository>();
builder.Services.AddSingleton<IAnchorRecoveryContactService, AnchorRecoveryContactService>();
builder.Services.AddSingleton<IRecoveryContactRegisteredEventEmitter, RecoveryContactRegisteredEventEmitter>();
```

Plus the endpoint mapping:

```csharp
app.MapRecoveryContactEndpoints();
```

#### Test plan (~17–20 tests for PR 2)

**`AnchorRecoveryContactServiceTests.cs`** (~10–12 tests):

- `RegisterAsync_HappyPath_ReturnsContactWithUlidId`
- `RegisterAsync_InvokesDesignateThenSetup_InOrder` (Moq InSequence)
- `RegisterAsync_EmitsRecoveryContactRegisteredAudit_AfterPersist`
- `RegisterAsync_ValidatesEmail_RFC5322` (invalid email throws `AnchorRecoveryContactRegistrationException`)
- `RegisterAsync_ValidatesPhone_E164WhenProvided`
- `RegisterAsync_RejectsTrusteeEdKeyOfWrongLength` (must be 32 bytes)
- `RegisterAsync_RejectsTrusteeDHKeyOfWrongLength` (must be 32 bytes)
- `RegisterAsync_LinksToExistingPartyByEmail_WhenPartyFound`
- `RegisterAsync_CreatesNewParty_WhenNoExistingByEmail`
- `RegisterAsync_NeverLogsRawTrusteeKey` (log capture grep: no 32-byte hex sequence)
- `ListAsync_ExcludesSoftDeletedContacts`
- `RemoveAsync_SetsTombstone_DoesNotHardDelete`
- `AdvanceOnboardingOnceMinimumContactsRegisteredAsync_Idempotent`
- `AdvanceOnboardingOnceMinimumContactsRegisteredAsync_RequiresAtLeastOneContact`

**`InMemoryAnchorRecoveryContactRepositoryTests.cs`** (~4–5 tests):

- `SaveAsync_PersistsAndGetReturns`
- `ListAsync_FiltersByActiveTeam` (per-team isolation per CRDT §14)
- `ListAsync_ExcludesTombstoned`
- `SaveAsync_RejectsContactWithDifferentTeamId_ThanActive` (defense-in-depth)
- `SaveAsync_AppendsAuditEvent_OnRemoval` (verifies the soft-delete path lands an audit event downstream)

**`RecoveryContactRegisteredEventEmitterTests.cs`** (~3 tests):

- `EmitAsync_AppendsRecoveryContactRegistered_ToAuditTrail`
- `EmitAsync_PayloadHasFingerprint_NotRawKey`
- `EmitAsync_CancellationTokenPropagates`

#### Halt conditions for PR 2

See §Halt-conditions H5, H6, H7 below.

#### PASS gate for PR 2

- All 17–20 tests green.
- Manual smoke test: complete PR 1's flow → land on `RecoveryContactRegistrationPage.razor` → register one contact (using a hand-crafted identity bundle from a second Anchor install OR a test fixture bundle) → click Continue → confirm navigation to home + onboarding state advanced to `recovery-contacts-registered`. Subsequent launch goes directly to home (no wizard).
- `RecoveryContactReviewPage.razor` renders correctly; Remove flow surfaces the no-revocation confirmation.

---

### PR 4 (TSX; Tauri React frontend) — Recovery contact registration + review UX

**Estimated effort:** ~4–5h
**Scope:** new React TSX pages `RecoveryContactRegistrationPage.tsx` + `RecoveryContactReviewPage.tsx` + shared `RecoveryContactCard.tsx`; typed Bridge client `apps/anchor-tauri/src/services/recoveryContacts.ts`; Vitest tests; route wiring in `apps/anchor-tauri/src/app.tsx`. Composes PR 3's Bridge endpoints.
**Commit subject:** `feat(anchor-identity-boot): React recovery contact registration + review pages`
**Branch:** `dev/anchor-identity-boot-pr4-react-recovery-contacts`
**Council:** Not required (UX-composition scope; no key handling on the React side).

#### Files to create

```
apps/anchor-tauri/src/pages/IdentityBoot/
├── RecoveryContactRegistrationPage.tsx        (route /identity/recovery-contacts)
├── RecoveryContactRegistrationPage.test.tsx
├── RecoveryContactReviewPage.tsx              (route /identity/recovery-contacts/review)
└── RecoveryContactReviewPage.test.tsx

apps/anchor-tauri/src/components/
├── RecoveryContactCard.tsx                    (shared; used by Review page)
└── RecoveryContactCard.test.tsx

apps/anchor-tauri/src/services/
├── recoveryContacts.ts                        (typed fetch wrapper: register, list, remove, advanceOnboarding)
└── recoveryContacts.test.ts
```

Edits to `apps/anchor-tauri/src/app.tsx`: register the two new routes inside the app-shell route tree (wrapped by `<FirstLaunchGuard>` from PR 2).

#### UX semantics (preserved from the original Razor spec; implemented in TSX)

The TSX components implement the same semantics as the original Razor spec preserved above in PR 3:

- **Header**, **explanation paragraph**, **threat-model acknowledgement** (verbatim ADR 0046-A6 §A6.1 quote with mandatory "I understand" checkbox gating the "Add contact" CTA), **add contact form** (display name, email, phone, identity-bundle paste), **identity-bundle parser** (composed from the existing Tauri-side identity-bundle decoder if available; else inline parsing logic), **submit handler** (calls `recoveryContacts.register(...)`), **Continue CTA** (gated on ≥1 contact registered).
- **Page-composition note:** if the sibling W#67 `TrusteeSetupPage` exists in React form (`apps/anchor-tauri/src/pages/Recovery/TrusteeSetupPage.tsx`), embed it; otherwise build the form inline (per the pre-pivot decision principle: prefer inline-now + refactor-later).
- **A11y:** preserve all aria-* attributes from the original Razor spec; use `@sunfish/ui-react` button/form primitives so styling is consistent across the app.

#### `services/recoveryContacts.ts` — typed Bridge client

Mirrors `services/identityBoot.ts` style. Exports `register({ displayName, email, phone, identityBundle })`, `list()`, `remove(id, reason)`, `advanceOnboarding()`. Each function `fetch`es a single endpoint from PR 3's `RecoveryContactEndpoints` and parses the JSON response.

#### Test plan (~10–12 Vitest tests for PR 4)

- `RecoveryContactRegistrationPage.test.tsx`: renders threat-model paragraph; submit button disabled until checkbox checked; form validates email format; submit calls `recoveryContacts.register`; on success appends to list + clears form; Continue CTA disabled until ≥1 contact registered; Continue navigates to `/`.
- `RecoveryContactReviewPage.test.tsx`: renders list of contacts; Remove button opens confirmation dialog with no-revocation copy; confirm calls `recoveryContacts.remove`; list re-renders without the removed contact.
- `RecoveryContactCard.test.tsx`: renders display name, email, fingerprint, registered-at; Remove button click invokes onRemove callback.
- `services/recoveryContacts.test.ts`: each wrapper handles 200, 400, 401, 500.

#### PASS gate for PR 4

- All ~10–12 tests green.
- Manual smoke: complete PR 2's flow → land on `RecoveryContactRegistrationPage.tsx` → register one contact (using a hand-crafted identity bundle from a second Anchor install OR a test fixture bundle) → click Continue → confirm navigation to home + onboarding state advanced to `recovery-contacts-registered`. Subsequent launch goes directly to home (no wizard).
- `RecoveryContactReviewPage.tsx` renders correctly; Remove flow surfaces the no-revocation confirmation copy verbatim from ADR 0046-A6.

---

### PR 5 — DI umbrella + onboarding-flow user-guide + nav wiring + ledger flip

**Estimated effort:** ~2h
**Scope:** extract the PRs 1 + 3 inline Bridge DI registrations into `AddAnchorIdentityBoot()` + `MapAnchorIdentityBoot()` extension methods; wire the "Recovery setup" navigation entry in `apps/anchor-tauri/src/app.tsx` (or wherever the nav lives — likely `apps/anchor-tauri/src/components/AppShell.tsx` if one exists, or directly in the `<nav>` block of `app.tsx`); author `apps/docs/anchor/onboarding-flow.md`; flip ledger.
**Commit subject:** `feat(anchor-identity-boot): DI extension + nav entry + docs page @standing-pattern: pattern-005,pattern-006`
**Branch:** `dev/anchor-identity-boot-pr5-di-docs-ledger`
**Council:** Not required (mechanical packaging).

#### Files to create

```
accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/
└── ServiceCollectionExtensions.cs              (AddAnchorIdentityBoot() + MapAnchorIdentityBoot() umbrella)

apps/docs/anchor/
└── onboarding-flow.md                          (user-guide section)
```

#### `ServiceCollectionExtensions.cs`

```csharp
public static class AnchorIdentityBootExtensions
{
    public static IServiceCollection AddAnchorIdentityBoot(this IServiceCollection services, IConfiguration config)
    {
        // PR 1 surfaces (gated on colocated-Bridge mode)
        if (config.GetValue<BridgeMode>("BridgeMode") == BridgeMode.Colocated)
        {
            services.AddSingleton<IStrongholdIpcChannel, HttpStrongholdIpcChannel>();
            services.AddSingleton<IRootSeedProvider, StrongholdRootSeedProvider>();
        }
        services.AddSingleton<IAnchorOnboardingStateStore, SqliteAnchorOnboardingStateStore>();
        services.AddSingleton<IIdentityProvisionedEventEmitter, IdentityProvisionedEventEmitter>();
        services.AddSingleton<IAnchorIdentityBootstrapService, AnchorIdentityBootstrapService>();

        // PR 3 surfaces
        services.AddSingleton<IAnchorRecoveryContactRepository, SqliteAnchorRecoveryContactRepository>();
        services.AddSingleton<IAnchorRecoveryContactService, AnchorRecoveryContactService>();
        services.AddSingleton<IRecoveryContactRegisteredEventEmitter, RecoveryContactRegisteredEventEmitter>();

        return services;
    }

    public static WebApplication MapAnchorIdentityBoot(this WebApplication app)
    {
        app.MapIdentityBootEndpoints();
        app.MapRecoveryContactEndpoints();
        return app;
    }
}
```

In `accelerators/bridge/Sunfish.Bridge/Program.cs`, delete the inline registrations from PRs 1+3 and replace with `builder.Services.AddAnchorIdentityBoot(builder.Configuration);` + `app.MapAnchorIdentityBoot();`. Position: AFTER `AddSunfishKernelSecurity` (so the default `IRootSeedProvider` registration order is right) and AFTER `AddSunfishFoundationRecovery` (so `IRecoveryCoordinator` is registered first; sibling hand-off `anchor-recovery-host-integration` provides this — confirm it has been pivoted to Bridge per §G1).

The placement assertion is mechanical but bug-prone: write a `BridgeProgramRegistrationOrderTest` that constructs the service collection in the same order as `Program.cs` does and asserts no exception when resolving `IAnchorIdentityBootstrapService` from the built provider.

#### First-launch guard — DONE in PR 2 under the pivot

Under the Tauri-first pivot, the first-launch routing guard is the React `FirstLaunchGuard.tsx` component shipped in PR 2 (see PR 2 spec above). PR 5 verifies its wiring in `apps/anchor-tauri/src/app.tsx` is correct and adds a `BridgeProgramRegistrationOrderTest` plus a React-side integration test that confirms the guard correctly redirects on first launch and lets the user through on subsequent launches.

For the existing joiner path (the equivalent of the pre-pivot `Onboarding.razor` — exists as a React component in `apps/anchor-tauri/src/` if W#60 P3 PR 1 included it; otherwise as a Bridge endpoint contract): the joiner is registering a device that's joining an existing identity; the first-launch wizard is for founders only. Confirm that the joiner's success path posts `POST /api/v1/identity-boot/ensure-bootstrapped` (or equivalent) to advance the onboarding state past `welcome`, so the guard doesn't re-fire on next launch. If the existing joiner implementation does NOT advance the onboarding state, PR 5 wires that bridge. If it can't be extended cleanly, file `*-question-*` and ship a follow-on hand-off to integrate.

#### Navigation entry — React-side

In the Tauri React frontend's nav surface (likely `apps/anchor-tauri/src/app.tsx`'s `<nav>` block, or `apps/anchor-tauri/src/components/AppShell.tsx` if it exists — verify before editing), add a "Recovery setup" entry pointing at `/identity/recovery-contacts/review`. Position: after the existing nav items (e.g. after "Properties" or "Crew Comms" — match the existing visual hierarchy). Use a Lucide `ShieldCheck` icon (lucide-react is already in the package.json deps) for consistency with the existing nav iconography.

#### `apps/docs/anchor/onboarding-flow.md`

Sections (~1500 words total; this is the canonical user-guide for what the wizard does):

1. **What happens on first launch** — the wizard, the three steps, the state persistence.
2. **Where your identity lives** — high-level: 32-byte root seed in your platform keystore (DPAPI on Windows, Keychain on macOS, libsecret on Linux). Reference paper §11.2–§11.3.
3. **What recovery contacts are and how they help** — local-first framing; ≥1 minimum, 3 recommended; the W#67 attestation flow at a high level.
4. **The threat-model truths you accepted** — quotes ADR 0046-A6 §A6.1 §"Threat model expansions" verbatim: per-install blast radius, no revocation, grace-period scope.
5. **How to add or remove contacts later** — points at `/identity/recovery-contacts/review`.
6. **What happens if you lose your device** — points at the recovery initiation flow (the sibling hand-off's `InitiateRecoveryPage.razor`).
7. **What happens if a recovery contact is compromised** — per ADR 0046-A6 §A6.1 §"No revocation": you cannot revoke; your only mitigation is to start over with a new identity. This is honest framing per the ADR.
8. **Light vs Standard vs Hosted tiers** — points at ADR 0088 §4 and explains where the user fits.
9. **Future work** — a brief note that:
   - The MAUI shell will be replaced by Tauri in a future Anchor release (per ADR 0086 + ADR 0088 §"Tiered runtime model"); the onboarding-flow ports cleanly because the service contract `IAnchorIdentityBootstrapService` is shell-agnostic.
   - Same-identity multi-device pairing is a future feature (no ETA at this writing).
   - Root-seed rotation is a future feature (Phase 3 of ADR 0046-A6).

#### Ledger flip

If a workstream row exists for this hand-off in `icm/_state/active-workstreams.md`, flip its status to `built`. **Per the user task brief: DO NOT modify `active-workstreams.md` directly** — the rendering tooling silently drops direct edits (per `feedback_never_add_workstream_rows_directly_to_ledger.md`). Instead, update the source row file in `icm/_state/workstreams/<workstream>.md` and run `render-ledger.py`. If no workstream row exists yet for `anchor-identity-boot`, file `cob-question-*` asking XO to create the source row before PR 3 flips it.

#### Test plan (~5 tests for PR 5)

- `AddAnchorIdentityBoot_RegistersAllExpectedServices_InColocatedMode` (ServiceProvider resolution test)
- `AddAnchorIdentityBoot_DoesNotRegisterStrongholdProvider_InStandaloneMode` (gate verification)
- `AddAnchorIdentityBoot_CanResolve_IAnchorIdentityBootstrapService_FromBuiltProvider`
- `MapAnchorIdentityBoot_RegistersBothEndpointGroups` (asserts the two map calls fire)
- `BridgeProgramRegistrationOrderTest` (per above)

#### PASS gate for PR 5

- All tests green.
- Manual smoke: from a fresh Tauri install, navigate to `/` → guard fires → land on wizard. After completing wizard, navigate to `/` → no redirect.
- Navigation entry "Recovery setup" renders in the React nav and routes to `/identity/recovery-contacts/review`.
- `apps/docs/anchor/onboarding-flow.md` renders correctly in DocFX (`apps/docs` build).
- Ledger row updated and `render-ledger.py` regeneration committed (per the user task brief: DO NOT modify `active-workstreams.md` directly; edit the source workstream file and run the renderer).

---

## Cross-cluster integration

This hand-off composes (consumer) and emits (producer):

### Consumes (read-only or invocation)

| Surface | Owner | Used by |
|---|---|---|
| `IRootSeedProvider.GetRootSeedAsync(ct)` | `packages/kernel-security/Keys/` | PR 1 (`AnchorIdentityBootstrapService`) — triggers first-launch seed generation + persistence |
| `StrongholdRootSeedProvider` (NEW; this hand-off PR 1) | `accelerators/bridge/Sunfish.Bridge/Features/IdentityBoot/` | NEW `IRootSeedProvider` impl registered in Bridge DI under the colocated mode; reads/writes the seed via Stronghold over the IPC channel |
| `tauri-plugin-stronghold` (via `credentialStore.ts`) | `apps/anchor-tauri/src/services/` | PR 1 (`HttpStrongholdIpcChannel` consumes the JS-side Stronghold wrapper indirectly via the IPC bridge in `src-tauri/src/identity_boot/`) |
| `KeystoreRootSeedProvider` (pre-pivot implementation) | `packages/kernel-security/Keys/` | Retained as standalone-Bridge fallback; PR 1 ensures DI registers `StrongholdRootSeedProvider` in colocated mode and leaves `KeystoreRootSeedProvider` for standalone mode |
| `ITeamSubkeyDerivation` | `packages/kernel-security/Keys/` | Not directly used in PR 1/2/3; the team subkeys are derived on-demand by downstream consumers (sync, signing); this hand-off's identity-bootstrap surface only generates the root seed, not the team subkeys. Documented in onboarding-flow.md §2. |
| `IActiveTeamAccessor.GetActiveAsync(ct)` | `packages/kernel-runtime/Teams/` | PR 1 (`AnchorIdentityBootstrapService`) — to scope `AnchorOnboardingState` per team |
| `IRecoveryClock.UtcNow()` | `packages/foundation-recovery/` | PR 1 + PR 2 — for timestamps that share semantics with `RecoveryEvent.AttestedAt` |
| `IRecoveryCoordinator.DesignateTrusteeAsync(nodeId, edPub, dhPub, ct)` | `packages/foundation-recovery/` (widened by W#67 PR 5) | PR 2 (`AnchorRecoveryContactService.RegisterAsync`) step 3 |
| `IRecoveryCoordinator.SetupTrusteeAsync(nodeId, encryptedSeed, ct)` | `packages/foundation-recovery/` (added by W#67 PR 3) | PR 2 (`AnchorRecoveryContactService.RegisterAsync`) step 4 |
| `IX25519KeyAgreement.Box(plaintext, recipientPub, ephPriv)` | `packages/kernel-security/Crypto/` (existing) | PR 2 — encryption of seed copy per ADR 0046-A6 §A6.5 |
| `IPartyReadModel.FindByExactEmailAsync(email)` | `packages/blocks-people-foundation/` OR local stub per §G4 | PR 2 — dedup-by-email before creating new Party |
| `IPartyWriteService.CreateAsync(party)` | `packages/blocks-people-foundation/` OR local stub | PR 2 — Party row creation per contact |
| `IAuditTrail.AppendAsync(record)` | `packages/kernel-audit/` | PR 1 + PR 2 — emit identity-provisioning + recovery-contact-registration events |
| Identity-bundle decoder (Tauri React-side or Bridge-side equivalent of `QrOnboardingService.DecodePayloadAsync`) | TBD location — verify on main; if only `accelerators/anchor/Services/Pairing/QrOnboardingService.cs` (MAUI) exists, file `cob-question-*` because PR 3 + PR 4 need a Tauri-shell-compatible decoder. The pre-pivot MAUI decoder cannot be invoked from Bridge directly. | PR 3 (Bridge backend) — server-side decode of pasted trustee identity bundles; PR 4 — optional client-side decode for inline validation feedback |

### Emits (audit-trail events)

| Event type | Payload | Emitted by | Consumer |
|---|---|---|---|
| `AuditEventType.IdentityProvisioned` | `IdentityProvisionedPayload` (TeamId, PublicKeyFingerprint, ProvisionedAt, InstallId) | PR 1 `AnchorIdentityBootstrapService.EnsureBootstrappedAsync` step 5 | Future Anchor security-dashboard surface; downstream Bridge analytics (when Bridge tier added) |
| `AuditEventType.RecoveryContactRegistered` | `RecoveryContactRegisteredPayload` (TeamId, TrusteeNodeId, PartyId?, TrusteeKeyFingerprint, RegisteredAt) | PR 2 `AnchorRecoveryContactService.RegisterAsync` step 6 | Future Anchor security-dashboard; recovery-audit-trail tooling |
| `AuditEventType.RecoveryContactRemoved` | (new payload — define alongside the registered payload) | PR 2 `AnchorRecoveryContactService.RemoveAsync` | Same as above |

No `cross-cluster-event-bus` events (the `IdentityBoot.*` namespace per `cross-cluster-event-bus-design.md` §3.N) are emitted in v1 — the audit-trail is the canonical event surface, and the audit-trail is multi-cluster-readable by design. If a future cross-cluster need surfaces (e.g. `blocks-people-foundation` wanting to subscribe to `IdentityProvisioned` to seed a Party row), file a follow-on hand-off to add the cross-cluster event publisher.

### Composes (UX surface composition with sibling hand-offs)

| Sibling | Composition point |
|---|---|
| `anchor-recovery-host-integration` (W#67 sibling) | Sibling registers `IRecoveryCoordinator` + the 5 recovery Razor pages (`TrusteeSetupPage`, `InitiateRecoveryPage`, `ApproveRecoveryPage`, `RecoveryStatusPage`, `PaperKey*`). This hand-off's PR 2 invokes `IRecoveryCoordinator` for setup and, where the trustee-setup-page already exists, embeds it inside the wizard wrapper. The two hand-offs MUST land in sequence: sibling first (gates §G1), then this hand-off. |
| W#67 G6-A (social recovery delivery substrate) | Provides `IX25519SubkeyDerivation`, `TrusteeEncryptedSeed`, the widened `IRecoveryCoordinator.SetupTrusteeAsync` + `DesignateTrusteeAsync(.., dhPub, ..)`, the `TrusteeDesignation.DHPublicKey` field, and the canonical-bytes signing-domain expansion. This hand-off's PR 2 composes on all of these; gate §G2 enforces. |
| `blocks-people-foundation` | Provides `Party` identity for recovery contacts. PR 2's local stub pattern (per §G4) preserves the future-relocate hook. |
| W#60 P3 PR 2 (Tauri SQLite cache) | Provides the SQLite infrastructure that `SqliteAnchorOnboardingStateStore` + `SqliteAnchorRecoveryContactRepository` write to. Gate §G3 enforces. |
| W#60 P4 PR 1 (Tauri Stronghold + credentialStore) | Provides `tauri-plugin-stronghold` + the `credentialStore.ts` API surface that PR 1's `StrongholdRootSeedProvider` extends to manage the root seed under a dedicated `anchor-rootseed` client namespace. Gate §G3 enforces. **Now a primary composition partner under the pivot** (was "NOT" pre-pivot). |

---

## Pre-merge council requirements

### PR 1 — MANDATORY councils

**security-engineering council** — focus areas:
1. `AnchorIdentityBootstrapService.EnsureBootstrappedAsync` order-of-operations: confirm audit-emit lands BEFORE state-advance (so a crashed bootstrap leaves the audit trail honest about the seed already existing in Stronghold).
2. `StrongholdRootSeedProvider` + `HttpStrongholdIpcChannel` integration (NEW under pivot): confirm we never log the seed; confirm the public-key-fingerprint pattern is consistent (8 hex chars from SHA-256 prefix; no shorter; no longer; no base64 — hex only); confirm the 127.0.0.1 binding + per-app-launch random port + bearer-token implementation; confirm `seed_exists` never returns the seed bytes; confirm the Stronghold-IPC channel logs do not capture seed material.
3. `SqliteAnchorOnboardingStateStore` data-at-rest posture: the SQLite database lives in the Tauri app-data directory (`appDataDir()`); on Windows/macOS this is user-readable but not user-writable by other users; on Linux respects `XDG_DATA_HOME` permissions. The `AnchorOnboardingState` is non-sensitive (step name + timestamp + team id — no PII, no keys). Confirm this assessment.
4. `IdentityProvisionedPayload.InstallId`: clarify what this is (it's the install id from the existing Tauri shell startup config, propagated to Bridge; a GUID, not the seed). Confirm this is the right level of detail for audit purposes — too granular (a per-install fingerprint that could correlate across teams) vs too vague.
5. Stronghold-IPC bearer-token bootstrap: confirm the token is generated per-app-launch by the Tauri shell and passed to Bridge via env var or startup config (NOT stored on disk between launches); confirm the channel rejects requests with mismatched bearer tokens (return 401, not 200-with-empty-body).
6. ADR 0068 §GC.1 attestation per project memory `feedback_council_reviews_use_best_model_xhigh.md`: identity-bootstrap is a Tenant Security Policy §GC.1 surface (key generation + secret persistence). Confirm the W#37 Tenant Security Policy gating: ADR 0068 currently `Proposed`. Per project memory `project_workstream_37_tenant_security_policy.md`, "§GC.1 counsel required; verify status in ADR file before building." If ADR 0068 is still `Proposed` at PR 1's open-time, the council MUST flag this as a hold-pending-W#37 dependency — possible outcomes: (a) PR 1 ships with a `// TODO: re-audit after ADR 0068 Accepted` marker on the bootstrap service; (b) PR 1 holds until ADR 0068 flips Accepted. XO recommends path (a) because the bootstrap surface is observable + auditable + the seed-generation logic itself is independent of tenant-security-policy semantics; the policy applies to *who can read* the seed post-generation, not to the generation event itself. Council can override.
7. Pivot-specific: confirm the Stronghold IPC mechanism choice (option (a) — 127.0.0.1 + bearer-token — per Open Question Q4) is acceptable for v1; flag if option (b) UDS / named pipe should be tracked as P1 follow-on.

**.NET architect council** — focus areas:
1. DI lifetimes: `IAnchorIdentityBootstrapService` as Singleton (Bridge is multi-request per-process, but the active team is scoped via `IActiveTeamAccessor` which is request-scoped; confirm the bootstrap service can be Singleton or whether it should be Scoped per request).
2. Endpoint-mapping pattern: `MapIdentityBootEndpoints()` follows the existing `MapListingsEndpoints()` convention (static extension on `WebApplication`); confirm placement in `Program.cs` is correct relative to auth middleware + recovery-coordinator registration.
3. Stronghold IPC channel design: `IStrongholdIpcChannel` abstracts the HTTP-on-127.0.0.1 implementation so a future UDS / named-pipe swap is non-breaking. Confirm the abstraction surface is right (verb count + payload shape + error-handling contract).
4. BridgeMode gating: the conditional `if (BridgeMode == Colocated)` DI registration is correct (StrongholdRootSeedProvider only registers in colocated mode; standalone-Bridge keeps the existing impl). Confirm the BridgeMode enum already exposes `Colocated` (verify on main) and that fallback to `KeystoreRootSeedProvider` in standalone mode does not break.
5. Cross-package commit: the PR adds enum constants to `packages/kernel-audit/AuditEventType.cs`. Confirm this is acceptable as a same-PR-cross-package change vs requiring a separate PR.

### PR 2 (TSX; React welcome+bootstrap) — Council not required

UX-composition scope; no key handling on the React side. Standard self-audit by dev applies.

### PR 3 (C#; Bridge recovery backend) — Council not required by default

Standard self-audit applies. **Exception:** if Halt §H6 fires (trustee-online constraint blocked), trigger security-engineering council before proceeding with any scope shift. The trustee-online constraint is ADR 0046-A6 §A6.5's UX contract; changing it requires ADR amendment + council ratification.

### PR 4 (TSX; React recovery pages) — Council not required

UX-composition scope; no key handling on the React side. Standard self-audit by dev applies. Same exception as PR 3 applies if Halt §H6 fires.

### PR 5 — Council not required

Mechanical packaging + docs + ledger flip. Standard self-audit applies.

---

## Idempotency-key catalog

Identity-boot operations involve persistence + event emission + external mutation (W#67 coordinator). Idempotency rules:

| Operation | Idempotency basis | Behavior on duplicate invocation |
|---|---|---|
| `AnchorIdentityBootstrapService.EnsureBootstrappedAsync` | Onboarding-state step value | If `state.Step != welcome`, returns existing state unchanged; does NOT re-invoke `IRootSeedProvider`, does NOT re-emit audit event. `IRootSeedProvider.GetRootSeedAsync` is itself idempotent (returns the cached seed after first call). |
| `AnchorRecoveryContactService.RegisterAsync` | Trustee node id + team id composite | Duplicate registration (same node id, same team) — second call detects the existing `AnchorRecoveryContact` and returns it unchanged WITHOUT re-invoking `DesignateTrusteeAsync` or `SetupTrusteeAsync`. The `IRecoveryCoordinator` itself enforces idempotency on `DesignateTrusteeAsync`; we layer the same shape at the Anchor service layer to avoid double-audit-emission. |
| `AnchorRecoveryContactService.RemoveAsync` | Contact id + tombstone state | If already tombstoned (DeletedAt != null), no-op; does NOT re-emit `RecoveryContactRemoved` audit. |
| `AdvanceOnboardingOnceMinimumContactsRegisteredAsync` | State step value | If state.Step is already `recovery-contacts-registered` or later, no-op. |
| `IdentityProvisionedEventEmitter.EmitAsync` | Event id (ULID, generated per emit-call) | If the underlying `IAuditTrail.AppendAsync` enforces event-id-uniqueness, duplicate calls produce duplicate events with distinct ids. The de-dup discipline must live in the caller (`AnchorIdentityBootstrapService`); the emitter itself is a thin wrapper. |
| `RecoveryContactRegisteredEventEmitter.EmitAsync` | Same as above | Same as above. |
| `SqliteAnchorOnboardingStateStore.SaveAsync` | (TeamId, latest-Version) | The store accepts any save with Version > current. If Version <= current, throws `AnchorOnboardingStateException` (stale write). This is the Tier-1 invariant for crash-safety — see PR 1 § "Crash-safety" note. |

---

## Dependencies + sequence

Build order (post-pivot 5-PR sequence):

```
W#67 G6-A (all 6 PRs)                            ← shipped 2026-05-16 (project memory project_w65_w66_w67_g6_closed.md)
W#60 P3 PR 1 (Tauri shell)                       ← shipped 2026-05-14 (PR #837)
W#60 P3 PR 2 (Tauri SQLite cache)                ← shipped 2026-05-14 (PR #836; gates §G3 SQLite check)
W#60 P4 PR 1 (Tauri Stronghold + credentialStore) ← on main (gates §G3 Stronghold check)
anchor-recovery-host-integration (pivoted to Bridge) ← gates §G1; sibling hand-off (may need its own pivot patch)
blocks-people-foundation (or local stub)         ← gates §G4 (PR 3 only); flexible
this hand-off PR 1 (C#; Bridge backend)          ← independent; can ship as soon as §G1 + §G3 + §G5 cleared
this hand-off PR 2 (TSX; React welcome+bootstrap) ← gates on PR 1 merged
this hand-off PR 3 (C#; Bridge recovery backend) ← gates §G2; can ship in parallel with PR 2
this hand-off PR 4 (TSX; React recovery pages)   ← gates on PR 3 merged
this hand-off PR 5 (DI umbrella + docs + ledger) ← gates on PRs 1–4 all merged
```

PRs 1 + 3 (Bridge C#) can be authored in parallel branches by dev-win. PRs 2 + 4 (React TSX) can be authored in parallel branches by dev as soon as their respective C# endpoint PR is merged (so the TSX side can fetch real endpoints). Merge order: PR 1 → PR 2 → PR 3 → PR 4 → PR 5. PR 1 + PR 3 are functionally independent so the Bridge halves can be merged in either order if PR 1 lands first per the §G1 dependency chain; the React halves follow.

If §G2 is blocked indefinitely (W#67 unships or is rolled back), PRs 1 + 2 can still ship as a standalone — the wizard would terminate at `identity-bootstrapped` and `IdentityBootstrapPage.tsx` would navigate to home with a banner "Recovery setup coming soon." This is acceptable interim state; PRs 3 + 4 + 5 would queue.

If §G3 (Stronghold or SQLite) is blocked, NO PR can ship under the pivot — see Halt §H8 for fallback options.

---

## License posture

This hand-off ships entirely under **MIT** (Sunfish repo's standard license per ADR 0088 §2 "License posture: Sunfish output is MIT"). No new third-party dependencies are introduced.

Existing third-party dependencies indirectly consumed:

| Dependency | License | Usage |
|---|---|---|
| **Tauri v2** (`tauri`, `tauri-build`) | MIT / Apache 2.0 dual | Anchor shell (W#60 P3 PR 1) |
| **`tauri-plugin-stronghold`** | Apache 2.0 / MIT dual | Auth-token storage (W#60 P4 PR 1) + root-seed storage (this hand-off PR 1; `StrongholdRootSeedProvider`) |
| **`@tauri-apps/plugin-stronghold`** (TS bindings) | Apache 2.0 / MIT dual | JS-side `credentialStore.ts` wrapper (existing) |
| **`keyring-rs`** (via W#60 P4 PR 1) | MIT | OS-keychain backing for Stronghold master-key derivation (existing) |
| **`sqlx`** (with `sqlite` feature) | MIT / Apache 2.0 dual | `SqliteAnchorOnboardingStateStore` + `SqliteAnchorRecoveryContactRepository` (this hand-off; SQLite infrastructure from W#60 P3 PR 2) |
| **`Microsoft.Data.Sqlite`** | MIT | In-memory test fixtures for the SQLite stores |
| **NSec.Cryptography** | MIT | `IX25519KeyAgreement` (existing; consumed indirectly via W#67 substrate) |
| **NSec.Cryptography → libsodium** | ISC | Same |
| **React** (`react`, `react-dom`) | MIT | React frontend (existing; W#60 P2 + P3) |
| **`react-router-dom`** v7 | MIT | Router + `<Navigate>` + `<Outlet>` (existing) |
| **`@sunfish/ui-react`** | MIT (in-repo) | Button/form primitives for the wizard pages |
| **`lucide-react`** | ISC | `ShieldCheck` icon for the nav entry (existing dep) |
| **`@tanstack/react-query`** | MIT | Optional caching layer for state polling (existing dep) |
| **System.Text.Json** | MIT | `AnchorOnboardingState` JSON marshaling on Bridge endpoints |

The clean-room discipline (ADR 0088 §3) applies but is not exercised in this hand-off — there is no GPL/AGPL source being studied. The recovery-contact pattern is standard custodial-key-management UX, well-documented in textbook cryptography (Schneier, Stallings) and in non-copyleft sources (Signal, Threema design docs). No copyleft reading required.

Per pattern-006 + the AR hand-off precedent: add an `apps/docs/anchor/onboarding-flow.md` entry to `apps/docs/toc.yml` (or equivalent) so DocFX picks up the new page. No `NOTICE.md` addition required (no third-party borrowings).

---

## Test plan summary + invariants

Aggregate across 3 PRs: ~35–40 unit tests + manual smoke gates.

### Invariants under test (binding)

1. **Identity generation is deterministic-from-seed.** Given the same root seed (read from `IRootSeedProvider`), every derived key (team subkey, SQLCipher key, X25519 trustee-DH key) is bit-identical across invocations. PR 1's tests do not directly verify this (it's a `IRootSeedProvider` invariant established by Wave 6.3.F), but PR 2's `RegisterAsync_HappyPath_ReturnsContactWithUlidId` test indirectly exercises it (`IX25519KeyAgreement.Box` is called against a derived ephemeral keypair; the same root + trustee DH key should always yield a decryptable envelope).

2. **Recovery contacts persist across restart.** PR 1's `SqliteAnchorOnboardingStateStoreTests.SaveAsync_RoundtripsState` + an integration test `RecoveryContacts_PersistAcrossProcessRestart` (constructs the SQLite store against an on-disk file twice; verifies the saved contact appears in the second instance). Under the pivot, the SQLite-backed repository is the v1 default (was in-memory pre-pivot), so the persistence test is unit-level, not just integration-level.

3. **First-launch detection is reliable.** PR 1's `IsFirstLaunchAsync_ReturnsTrue_WhenNoState` + `_WhenStateIsWelcomeStep` + `_ReturnsFalse_AfterBootstrap` — the three states. Plus PR 3's `FirstLaunchGuardTests_RedirectsWhenIsFirstLaunchTrue` — the navigation outcome. Plus the manual smoke test that wipes Preferences and confirms the wizard fires.

4. **Audit events are append-only and never carry raw secrets.** Every emit-test asserts that the payload contains only fingerprints (8 hex chars) and structured metadata; no test asserts a raw key or seed is present in any payload. Log-capture grep tests reinforce this.

5. **Onboarding-state transitions are linear.** `AnchorOnboardingStepTransitionTests` enforces this; the `SqliteAnchorOnboardingStateStore.SaveAsync` enforces it at the write boundary.

6. **Per-team isolation is enforced at the repository boundary.** `SqliteAnchorOnboardingStateStoreTests.SaveAsync_PerTeamIsolation` + `SqliteAnchorRecoveryContactRepositoryTests.ListAsync_FiltersByActiveTeam`.

7. **No revocation, surfaced honestly.** PR 2's `RecoveryContactReviewPage.razor` remove-confirmation dialog text MUST quote the no-revocation truth per ADR 0046-A6 §A6.1; the smoke-test gate includes a manual verification of the dialog text against the ADR (a copy-paste check, not an automated test — flag for COB to take a screenshot during PASS gate).

### Automated test counts by PR (post-pivot)

| PR | Test files | Approx test count |
|---|---|---|
| PR 1 (C#; Bridge) | 6 (`AnchorIdentityBootstrapServiceTests`, `SqliteAnchorOnboardingStateStoreTests`, `AnchorOnboardingStepTransitionTests`, `IdentityProvisionedEventEmitterTests`, `HttpStrongholdIpcChannelTests`, `IdentityBootEndpointsTests`) | 25–30 |
| PR 2 (TSX; React) | 4 (`FirstLaunchWelcomePage.test`, `IdentityBootstrapPage.test`, `FirstLaunchGuard.test`, `services/identityBoot.test`) | 10–12 |
| PR 3 (C#; Bridge) | 4 (`AnchorRecoveryContactServiceTests`, `SqliteAnchorRecoveryContactRepositoryTests`, `RecoveryContactRegisteredEventEmitterTests`, `RecoveryContactEndpointsTests`) | 17–22 |
| PR 4 (TSX; React) | 4 (`RecoveryContactRegistrationPage.test`, `RecoveryContactReviewPage.test`, `RecoveryContactCard.test`, `services/recoveryContacts.test`) | 10–12 |
| PR 5 (mech) | 1 (`BridgeProgramRegistrationOrderTest` + `AddAnchorIdentityBoot` tests co-located) | 5 |
| **Total** | **19 test files** | **67–81** |

### Manual smoke-test gates

Each PR has a manual smoke gate (see PR PASS-gate sections above). Aggregate manual coverage:

- Fresh install on macOS → wizard fires → identity provisioned → recovery contact registered → wizard exits → next launch lands on home (no wizard).
- Fresh install on Windows → same, with DPAPI as the keystore backing.
- Existing install (post-wizard) → navigate to `/identity/recovery-contacts/review` → list renders → Remove flow surfaces no-revocation dialog → confirm → contact soft-deleted; list re-renders without the removed contact.
- Joiner path (existing `/onboarding`) → still works, does not trigger the wizard guard (per the PR 3 §"Navigation entry" note about `QrOnboardingService` advancing the state).

---

## Halt conditions

Halt = stop work + drop a `cob-question-*.md` beacon to `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/` + flag in ledger row. Do not proceed past the halt point until XO responds.

### H1 — `IRootSeedProvider` is missing or returns zero-bytes

If a smoke-test of `IRootSeedProvider.GetRootSeedAsync()` (via the colocated Bridge endpoint OR an integration test against the freshly-registered `StrongholdRootSeedProvider`) returns a 32-byte zero buffer (the pre-Wave 6.3.F stub behavior per `IRootSeedProvider.cs` XML doc lines 14–22), STOP PR 1. This means either the Wave 6.3.F refactor regressed OR the `StrongholdRootSeedProvider` impl has a latent stub. The `IRootSeedProvider` XML doc explicitly identifies the zero-seed regression as a critical bug ("trivially breaking per-install isolation"). File `*-question-anchor-identity-boot-h1-rootseed-stub-regression-{slug}.md`. (Under the pivot, the regression most likely surfaces in the IPC channel returning a zero-padded response on Stronghold init failure — verify the channel parses the error path correctly before declaring the root cause.)

### H2 — `AnchorOnboardingState` schema would need to change

If, during PR 1 implementation, COB discovers that `AnchorOnboardingState` needs additional fields beyond the spec (e.g., a `Tier` field to distinguish Light/Standard/Hosted per ADR 0088 §4), STOP. The schema addition is a Stage 03 design decision, not a Stage 06 implementation choice. File `cob-question-anchor-identity-boot-h2-state-schema-extension-{slug}.md` proposing the field + use case.

### H3 — `apps/anchor-tauri/src/app.tsx` already has a competing redirect / first-launch guard

If `apps/anchor-tauri/src/app.tsx` already contains a top-level navigation guard or redirect logic (e.g. from a parallel sibling hand-off), STOP PR 2's guard wiring. Two guards in the same route tree risks navigation loops. File `*-question-*` for XO to reconcile. (XO expects `app.tsx` does NOT currently have a first-launch guard; verify before writing the guard. Existing redirects to `/onboarding` for joiners are fine — those are a different path and don't conflict with `<FirstLaunchGuard>`.)

### H4 — `AuditEventType` enum is locked or non-extensible

If `packages/kernel-audit/AuditEventType.cs` carries a "do not edit; generated" header, STOP PR 1's audit-type addition. The W#67 hand-off precedent (`AuditEventType.RecoveryRekey`) suggests the enum is hand-edited, but verify before adding two new constants. File `cob-question-*`.

### H5 — `blocks-people-foundation` ships mid-flight after this hand-off's PR 3 opens

If the owner opens PR 3 (backend) using the local-stub Party path (per §G4 path b) and then `blocks-people-foundation` lands on `main` before PR 3 merges, STOP. The PR will need to switch to the canonical Party imports; if the merge timeline is tight, rebase + retrofit may be cleaner than abandoning the stub. File `*-question-*` for XO to advise on rebase-vs-abandon. (Likely outcome: rebase to use the canonical import; the diff is one `using` per file + delete the stub files.)

### H6 — Trustee-online constraint cannot be honored

If, during PR 4 React UX implementation (or earlier, during PR 3 backend if the backend reveals it), the only way to register a contact is to manually paste a hand-crafted identity bundle (because there's no second Anchor install available, or the equivalent Tauri-side bundle-generation surface is broken), the trustee-online constraint per ADR 0046-A6 §A6.5 §"Trustee-online requirement for setup" is structurally unmet. The wizard would force users into a position where they can't complete onboarding because they don't have a second device with another Anchor install. STOP. This is a Stage 02 design issue, not a Stage 06 implementation issue. File `*-question-anchor-identity-boot-h6-trustee-online-unworkable-{slug}.md` with proposed alternatives (e.g., allow paper-key registration as the recovery contact instead of trustees, deferring the W#67 social-recovery flow to a manage-existing path). XO + security-engineering council convene.

### H7 — `IAnchorRecoveryContactService.RegisterAsync` race with concurrent registration

If two parallel `RegisterAsync` invocations against the same `IAnchorRecoveryContactService` (e.g., user double-clicks the submit button on `RecoveryContactRegistrationPage.tsx`) produce two identical `DesignateTrusteeAsync` calls to `IRecoveryCoordinator`, AND the coordinator rejects the second call (idempotency check at the coordinator layer), STOP PR 3 implementation and add a service-layer mutex per (TeamId, trusteeNodeId) tuple. This is a thin fix (a `SemaphoreSlim` per key), but if the coordinator's idempotency semantics are unclear, file `cob-question-*` for clarification before adding the mutex. (Under the pivot, double-submit can also be prevented at the React layer by disabling the submit button on first click — but the backend mutex is the defensive belt-and-braces.)

### H8 — Tauri Stronghold unavailable or non-functional at PR 1 build time (NEW under pivot)

If, during PR 1 implementation, the Stronghold-IPC channel cannot be established because:

(a) `tauri-plugin-stronghold` has been removed from `apps/anchor-tauri/src-tauri/Cargo.toml` since this hand-off was authored (a regression of W#60 P4 PR 1), OR
(b) `apps/anchor-tauri/src/services/credentialStore.ts` is missing or non-functional (e.g., `getHandles()` throws on every call due to an underlying Stronghold init bug), OR
(c) the colocated-Bridge IPC pattern (127.0.0.1 + bearer-token Tauri command) is structurally unworkable (e.g., Tauri v2 has removed the local-HTTP-command surface this hand-off assumes, OR the bearer-token mechanism cannot be implemented securely), OR
(d) Stronghold's multi-client-namespace feature (used to keep the auth token and root seed cryptographically isolated within a single snapshot) is unavailable or broken in the installed `tauri-plugin-stronghold` version,

STOP PR 1. Drop `cob-question-anchor-identity-boot-h8-stronghold-{slug}.md` (or `dev-win-question-*`) to coordination inbox with:

- Which sub-condition (a/b/c/d) was triggered.
- Minimal repro (commands run + observed output).
- Affected files + line numbers.
- Proposed fallback options:
  - **Fallback A:** ship `KeystoreRootSeedProvider` registration in colocated mode too (degrades security on macOS where Keychain prompts every read; not acceptable on Surface Pro as primary target).
  - **Fallback B:** delay PR 1 until W#60 P4's missing/broken Stronghold piece is fixed by dev-win.
  - **Fallback C:** ship a transitional `FileBasedRootSeedProvider` writing to a permission-locked file under `appDataDir()` (acceptable as a smoke-test-only path; NOT acceptable for production).

XO + security-engineering + dev-win convene. Default recommendation: Fallback B (wait for dev-win) unless the timeline is unacceptable; never ship Fallback A or C without security-engineering sign-off.

---

## Pre-merge PASS gate (aggregate across all 5 PRs)

For the workstream to flip `built` per PR 5:

1. **All 67–81 tests green** (post-pivot count; up from the pre-pivot 38–45).
2. **Security-engineering council ratified PR 1** with no Blocking findings and all Major findings resolved before merge. Explicit sign-off on the Stronghold-IPC bearer-token + 127.0.0.1-binding contract.
3. **.NET architect council ratified PR 1** with no Blocking findings. Explicit sign-off on the colocated-vs-standalone Bridge DI gating logic.
4. **Manual smoke tests passed on Surface Pro** (CO's primary acceptance platform under the pivot — verified by CO directly OR by dev-win in a CO-witnessed session). macOS secondary; Linux deferred. Document platform + procedure in PR 5's merge commit.
5. **Onboarding-flow user-guide page renders in DocFX** (`apps/docs` build green; new page appears in the rendered TOC).
6. **No raw seed bytes appear in any log capture** during smoke tests (grep gate per PR 1 PASS criteria; also covers the Stronghold-IPC channel logs).
7. **First-launch detection is reliable across at least 3 wipe-and-reinstall cycles** (manual; documented in PR 5 merge commit).
8. **Recovery-contact registration round-trips successfully** with at least one trustee identity bundle generated from a second Anchor Tauri install (documented in PR 5 merge commit; if a second install is unavailable, document the fixture-bundle-test substitute and flag for follow-on smoke test).
9. **`apps/docs/anchor/onboarding-flow.md` quotes ADR 0046-A6 §A6.1 §"Threat model expansions" verbatim** (no paraphrase; copy-paste check during PR 5 review).
10. **Ledger row flipped via source-file edit + `render-ledger.py` regen** (NOT direct edit of `active-workstreams.md`).
11. **Stronghold-IPC bearer-token rejection verified by manual `curl`** (PR 1 PASS gate); documented in PR 5 merge commit.

---

## Open questions (for XO to resolve before / during build, not blocking PR 1)

### Q1 — Multi-device pairing scope boundary

The user task brief asks specifically about the boundary between "first-launch identity bootstrap" (this hand-off) and "multi-device pairing" (out of scope, deferred to a future `blocks-localfirst-sync` cluster).

Current scope-boundary rationale:

- **First-launch identity bootstrap** = the device generates a fresh 32-byte root seed and persists it locally. There is ONE seed, ONE install. No cross-device coordination.
- **Multi-device pairing** = two devices share the SAME root seed (or at minimum, the SAME CRDT replica set, via per-device subkeys derived from a shared root). This requires a cryptographic-handshake protocol (likely a QR-mediated X25519 + AEAD exchange of the root seed, gated by a user-confirmed pairing code) and a Loro-CRDT-replica-id allocation handshake so the two devices don't double-write to the same vector slot.
- **What this hand-off leaves in place for the future cluster:**
  - `IRootSeedProvider` is the seed read-surface; multi-device pairing would need a `IRootSeedReceiver` (parallel to W#67's `IRootSeedRestorer` but with different semantics — receiver vs restorer — TBD by the future hand-off).
  - `AnchorOnboardingStep` enum is string-coded per CRDT §5; the future hand-off can add a new step (e.g. `device-paired-to-existing-identity`) without renaming any existing step.
  - The first-launch path explicitly routes through `FirstLaunchWelcomePage.razor` with TWO choices (set up new identity / join existing team); the existing `Onboarding.razor` joiner path handles team-membership join, not same-identity pairing. The future hand-off would add a THIRD choice ("pair this device with another of your existing devices") or refactor the welcome page to surface it.

**Recommendation:** confirm with XO whether the multi-device-pairing future hand-off should land in `blocks-localfirst-sync` (the user task brief's suggestion) OR in `accelerators/anchor/Services/IdentityBoot/MultiDevice/` (Anchor-shell-specific). The protocol logic is shell-agnostic (it's a cryptographic + CRDT handshake), so a `packages/*` placement seems correct; the UX is Anchor-specific.

### Q2 — Onboarding-state Tier field for Light/Standard/Hosted disambiguation

ADR 0088 §4 defines three tiers (Light / Standard / Hosted). The current `AnchorOnboardingState` doesn't carry a tier indicator — implicitly, the Anchor MAUI shell always runs Light tier. When the Standard tier (Anchor + bundled Bridge instance) ships, the onboarding wizard will need different steps (e.g. "Configure your bundled Bridge instance"). Should the schema extension land now (per H2) or in the Standard-tier hand-off?

**Recommendation:** defer. Add the field in the Standard-tier hand-off, with a default of `Tier.Light` for back-compat. The PreferencesAnchorOnboardingStateStore's JSON serialization handles additive fields cleanly.

### Q3 — `IRecoveryContactService` as a foundation interface vs Bridge-feature helper

The user task brief uses the term `IRecoveryContactService`. This hand-off ships a Bridge-feature-internal `IAnchorRecoveryContactService` that wraps `IRecoveryCoordinator` + adds `Party` linkage + audit-emission. Should the wrapper be promoted to a foundation interface (`packages/foundation-recovery/IRecoveryContactService.cs`) so other shells / future surfaces can compose against the same abstraction?

**Recommendation:** defer the promotion. The wrapper is currently thin and Bridge-feature-specific (it knows about `IAnchorOnboardingStateStore` + the endpoint-routing semantics). If a second shell (a hypothetical Bridge admin UI, or a future mobile shell) needs an analogous wrapper, that's the trigger to extract the common surface to foundation. Per the project memory `feedback_council_can_miss_spot_check_negative_existence.md`, we verified no such interface exists today; introducing it speculatively would be premature abstraction.

### Q4 — Stronghold-IPC mechanism (NEW under pivot)

The pivot introduces a new architectural surface: how does C# Bridge talk to Rust Tauri's Stronghold? PR 1 assumes a 127.0.0.1 + bearer-token Tauri command bridge under the colocated-Bridge case. Alternatives considered:

- **(a) Local HTTP on 127.0.0.1 + bearer-token (chosen for PR 1).** Pros: simple to implement on both sides; standard HTTP client / server idioms; easy to test with `WebApplicationFactory` + a Rust mock. Cons: ephemeral port allocation; bearer-token bootstrapping requires Tauri-startup-config plumbing; could fail to bind on restrictive Windows hosts.
- **(b) Unix domain socket / named pipe.** Pros: cannot be reached from off-machine even by mistake; permission-locked to the user; no port allocation. Cons: cross-platform inconsistency (named pipes on Windows vs UDS on macOS/Linux); .NET + Rust client libraries are less standardized.
- **(c) Tauri's invoke() API directly from a WASM-compiled Bridge subset.** Pros: zero IPC; same-process. Cons: requires Bridge to run inside the Tauri webview, which is structurally incompatible with the current Bridge ASP.NET Core architecture.
- **(d) gRPC over local socket.** Pros: typed contracts; modern. Cons: heavier dependency footprint; new tooling.

**Recommendation:** ship (a) for PR 1, with the `IStrongholdIpcChannel` abstraction designed to swap to (b) in a future hardening pass without breaking consumers. Document this trade-off in PR 1's commit body + the onboarding-flow.md `§"Future work"` section. Security-engineering council line item: confirm (a)'s 127.0.0.1 + bearer-token + per-app-launch-random surface is acceptable for v1; flag if a future hardening to (b) should be tracked as a P1 follow-on.

If the security-engineering council ratifies (a) only with caveats (e.g. "must move to UDS within 30 days"), file a follow-on hand-off `anchor-identity-boot-stronghold-ipc-hardening-stage06-handoff.md` to track the upgrade.

---

## Resume protocol

If COB pauses mid-build and a future session needs to resume:

1. **Read this hand-off file first** — it is the canonical spec.
2. **Check ledger status** in `icm/_state/active-workstreams.md` (or the source file under `icm/_state/workstreams/`) for the current PR-level status.
3. **Run the pre-build checklist** + the per-PR gates (§G1–§G5) to confirm the state hasn't drifted since pause.
4. **Check the coordination inbox** for any XO responses to prior `cob-question-*` beacons (`ls /Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`).
5. **Resume at the next un-merged PR.** If PR 1 is merged and PR 2 is partially drafted, continue PR 2 from the partial-draft branch.
6. **Re-run all tests for the in-flight PR** — Preferences / keystore state may have drifted; tests should remain green.

---

## References

- ADR 0088 — `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` — Path II ratification + cluster grouping + tier model + clean-room discipline
- ADR 0046-A6 — `docs/adrs/0046-a6-social-recovery-seed-delivery-protocol.md` — recovery substrate (W#67) + threat model + signing-domain expansion
- ADR 0046 — `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` — Phase 1 recovery scheme (W#15 + W#32)
- ADR 0068 — Tenant Security Policy (Status: Proposed; §GC.1 governs key-generation surfaces — verify status before PR 1 council)
- ADR 0086 — `docs/adrs/0086-anchor-tauri-react-product-surface.md` — **canonical Anchor shell under the 2026-05-17T14-30Z pivot; this hand-off targets it directly** (was "future / out of scope" pre-pivot)
- ADR 0032 — `docs/adrs/0032-multi-team-anchor-workspace-switching.md` — multi-team workspace (per-team onboarding-state isolation)
- W#67 hand-off — `icm/_state/handoffs/w67-g6a-social-recovery-seed-delivery-protocol-stage06-handoff.md` — recovery delivery substrate (composes via §G2)
- Sibling hand-off — `icm/_state/handoffs/anchor-recovery-host-integration-stage06-handoff.md` — recovery host integration (composes via §G1)
- `blocks-people-foundation` hand-off — `icm/_state/handoffs/blocks-people-foundation-stage06-handoff.md` — Party substrate (composes via §G4)
- W#60 P4 hand-off — `icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md` — Tauri Stronghold (**primary composition partner under the pivot per §G3; PR 1 provides the credentialStore.ts API surface this hand-off extends**)
- W#60 P3 PR 2 — Tauri SQLite cache (PR #836; merged 2026-05-14) — SQLite infrastructure for `SqliteAnchorOnboardingStateStore` + `SqliteAnchorRecoveryContactRepository`
- `_shared/engineering/standing-approved-patterns.md` — pattern-001 + pattern-005 + pattern-006 applied
- `_shared/engineering/crdt-friendly-schema-conventions.md` — §1, §2, §3, §5, §6, §10, §14 applied
- `_shared/engineering/party-model-convention.md` — §2, §4 applied (cross-cluster reference to Party)
- `packages/foundation-recovery/IRecoveryCoordinator.cs` — coordinator interface (widened by W#67)
- `packages/foundation-recovery/TrusteeDesignation.cs` — designation record (widened by W#67)
- `packages/foundation-recovery/TrusteeEncryptedSeed.cs` — encrypted seed record (added by W#67)
- `packages/kernel-security/Keys/IRootSeedProvider.cs` — read-only seed surface (existing)
- `packages/kernel-security/Keys/KeystoreRootSeedProvider.cs` — keystore-backed impl (existing)
- `packages/kernel-security/Keys/ITeamSubkeyDerivation.cs` — team subkey derivation (existing)
- `packages/kernel-security/Keys/IX25519SubkeyDerivation.cs` — X25519 derivation (added by W#67)
- `packages/kernel-security/Crypto/IX25519KeyAgreement.cs` — Box/OpenBox primitives (existing)
- `packages/kernel-audit/AuditEventType.cs` — event-type registry (this hand-off adds 3 constants)
- `accelerators/anchor/Services/AnchorBootstrapHostedService.cs` — existing team-materialization hosted service (pre-pivot MAUI artifact; the Bridge-side equivalent likely exists under `accelerators/bridge/Sunfish.Bridge/` and should be located by COB / dev-win during PR 1 implementation; if absent, file `*-question-*`)
- `apps/anchor-tauri/src/app.tsx` — existing React app shell (uses `react-router-dom` v7; this hand-off extends its route tree)
- `apps/anchor-tauri/src/services/credentialStore.ts` — existing Stronghold-backed auth-token wrapper (W#60 P4 PR 1); PR 1's `StrongholdRootSeedProvider` extends the underlying Stronghold vault with a new `anchor-rootseed` client namespace
- `accelerators/bridge/Sunfish.Bridge/Listings/ListingsEndpoints.cs` — canonical Bridge endpoint pattern (this hand-off's `IdentityBootEndpoints.cs` + `RecoveryContactEndpoints.cs` follow the same shape)
- `accelerators/bridge/Sunfish.Bridge/BridgeMode.cs` — existing Bridge mode enum (verify it exposes `Colocated`; add if absent per PR 1 spec)
- Project memory `project_w65_w66_w67_g6_closed.md` — confirms W#67 G6-A closed 2026-05-16
- Project memory `project_w60_p4_ownership_dev_win.md` — confirms W#60 P4 ownership context (dev-win); informs §G3
- Project memory `feedback_council_reviews_use_best_model_xhigh.md` — council dispatch rules for PR 1
- Project memory `feedback_council_can_miss_spot_check_negative_existence.md` — informs Q3 (no speculative interface promotion)
- Project memory `feedback_never_add_workstream_rows_directly_to_ledger.md` — informs PR 3 ledger flip discipline
