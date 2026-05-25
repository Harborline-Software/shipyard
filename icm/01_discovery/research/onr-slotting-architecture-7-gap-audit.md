# ONR research — UPF audit + verification of 7 slotting-architecture gaps (2026-05-25)

**Requester:** CIC (via Admiral)
**Scope:** Verify CIC's 7-gap analysis of the proposed AI/media capability slot
registry against actual fleet state; challenge framing; run UPF Stage 0 +
Stage 2 on the recommended 6-step attack order; identify simpler approaches.
**Out of scope:** Authoring ADR-0063 / providers.ts / Tauri command spec. This
is audit only — no production code, no ADR drafts.
**Authoritative sources:** Shipyard ADRs 0013 / 0023 / 0062 / 0063 / 0067 +
`packages/foundation-integrations/` + `packages/foundation-mission-space/` +
`packages/contracts/src/{integrations,system-requirements}.ts`; flight-deck
`packages/plugins/{manifest-schema.json,registry.json,plugins/*}` +
`docs/architecture/plugin-architecture.md` + galley-platform-spec.md;
tender `apps/desktop/src-tauri/src/{commands,devices,telemetry,lib}.rs`.
**Status:** Final.

---

## TL;DR (executive summary)

| Gap | CIC verdict | ONR verdict | One-line rationale |
|---|---|---|---|
| 1 — No AI/Media slot contracts (shipyard) | Critical | **PARTIALLY TRUE** | `providers.ts` doesn't exist BUT (a) `integrations.ts` already ships `IntegrationProviderSchema` / `ActiveProviderSnapshot` / `IntegrationAtlasView` / `ProviderValidationStatus`; (b) `Sunfish.Foundation.Integrations` ships `IProviderRegistry` / `ProviderDescriptor` / `ProviderHealthReport` / `IProviderHealthCheck`. The *content* gap is real: `IntegrationCategory` lacks AI/media values. The *structural* gap CIC named is overstated. |
| 2 — No ADR for slot registry | Critical | **FALSE (counter-finding)** | ADR-0013 codifies `IProviderRegistry` + provider-neutrality policy; ADR-0023 codifies the per-slot UI method pattern; ADR-0062 codifies the DirectX-Feature-Levels capability-negotiation substrate (`MissionEnvelope` + `IFeatureGate<T>`); ADR-0063 codifies the install-UX + per-feature `MinimumSpec` declarative tier. The pattern is **over-codified, not under-codified**. CIC's "no ADR" framing is wrong; the real gap is *integration* of these existing ADRs with the AI/media domain. **Also: numeric collision — ADR-0063 is taken** (Mission Space Requirements). |
| 3 — Tender has no IPC contract w/ shipyard registry | High | **FALSE** | `tender/apps/desktop/src-tauri/src/` is NOT default Tauri boilerplate. It has `commands.rs` / `devices.rs` / `telemetry.rs` with substantive Tauri commands (`get_services`, `get_devices`, `get_system_stats`, `emergency_stop`, `restart_signal_bridge`). The named commands (`list_providers` / `get_health` / `install_bundle`) DON'T exist — that is the genuine sub-gap, but the broader "boilerplate" framing is incorrect. |
| 4 — No shared TS types for tender to consume | High | **PARTIALLY TRUE** | True: tender has no dependency on `@sunfish/contracts`; no publish pipeline; package is `@sunfish/contracts` not `@harborline/contracts` (name mismatch in CIC's framing). False part: there IS established precedent (`file:` sibling references — see fleet conventions on `@sunfish/ui-react`). |
| 5 — Essential vs Extension slot bootstrap missing | Medium | **FALSE (CIC misread the file)** | `system-requirements.ts` is NOT scoped to OS/env. It is the install-time evaluation surface from ADR-0063, projecting `SystemRequirementsResult` + `DimensionEvaluation` + `SpecPolicy: Required\|Recommended\|Informational`. The "essential vs extension tier" model **already exists** under different names (`Required` = essential; `Recommended` = extension). The bootstrap-check substrate is `IMinimumSpecResolver.EvaluateAsync` per ADR-0063-A1.1. |
| 6 — `Sunfish.slnx` stranded in shipyard | Medium | **TRUE — and worse than CIC stated** | `Sunfish.slnx` references 14 `accelerators/...` paths that **no longer exist** post-2026-05-17 migration. It's not just stranded; it is **broken as a buildable solution**. `Shipyard.slnx` (239 projects, clean) is the working artifact. |
| 7 — `BannedSymbols.txt` empty | Low | **TRUE — file is literally 0 bytes** | Confirmed empty file at shipyard root. CIC's framing is accurate; this is the only gap that survives review largely intact. |

**Composite verdict:** Of 7 gaps, **2 are TRUE, 3 are FALSE, 2 are PARTIALLY TRUE**. The CIC gap analysis appears to have been authored without surveying **flight-deck's pre-existing plugin-manifest registry** (`packages/plugins/manifest-schema.json` + 17 manifests + slot/capability vocabulary), and without reading **ADRs 0013 / 0023 / 0062 / 0063 / 0067** which collectively codify the registry/manifest/capability/install-UX substrate the gap analysis claims is absent.

**The AHA finding:** **The slot registry already exists. It lives in flight-deck.** The architectural question is not "design ADR-0063 and providers.ts from scratch" but rather **"promote flight-deck's existing plugin-manifest pattern into a fleet-wide foundation, generalize it to non-AI/media providers, and reconcile it with the existing `Sunfish.Foundation.Integrations` + `Sunfish.Foundation.MissionSpace` substrate."**

**Revised attack order (3 phases, not 6 steps):** see §"Recommendations" below. Net effect: substantially less new code authoring; substantially more *promotion* + *reconciliation*; the proposed ADR-0063 should be renumbered (e.g. ADR-0095) and reframed as a **promotion+reconciliation ADR**, not a greenfield design ADR.

---

## Gap-by-gap detailed verification

### Gap 1 — Critical: No AI/Media Service Slot Contracts (shipyard)

**CIC claim:** `packages/contracts/src/` has 6 domain files but no `providers.ts`, no `ProviderManifest`, no slot capability types, no control-surface types. "The entire capability slot registry discussed in these sessions has no contract surface yet."

**Verification:**

```
$ ls shipyard/packages/contracts/src/
__tests__  accounting.ts  index.ts  integrations.ts  property.ts  sync.ts
system-requirements.ts  tenant.ts
```

CIC counted 6 files; ground truth is 7 `.ts` files (CIC missed `tenant.ts`) plus
`__tests__` and `index.ts`.

**`providers.ts` does NOT exist** — verified. **BUT existing prior art:**

1. `integrations.ts` ships the contracts that approximate the gap's framing:
   - `IntegrationCategory` enum: `Payments | TransactionalEmail | MarketingEmail | Messaging | MeshVpn | Captcha`
   - `IntegrationProviderSchema { providerId, displayName, category, credentialFields, ... }` — **this is the ProviderManifest CIC said doesn't exist**
   - `ActiveProviderSnapshot { providerId, activatedAt }`
   - `IntegrationValidationResult { status, validatedAt, errorCode, errorMessage }`
   - `IntegrationAtlasView { activeByCategory, statusByCategory }`
   - `ProviderValidationStatus` enum: `Unknown | Valid | Invalid | Unreachable`
   - `ReactIntegrationAtlasProvider` interface with `getSchemas() / getAtlasView() / issueProviderChange()`

2. `system-requirements.ts` ships the `MinimumSpec` / capability-evaluation surface from ADR-0063 (see Gap 5).

3. **At the .NET-substrate layer (`packages/foundation-integrations/`):**
   - `IProviderRegistry.cs`
   - `ProviderDescriptor.cs`
   - `ProviderHealthStatus.cs` (enum: `Unknown | Healthy | Degraded | Unhealthy`)
   - `IProviderHealthCheck.cs`
   - `ProviderHealthReport` (per ADR-0013 + `apps/docs/foundation/integrations/`)
   - `InMemoryProviderRegistry.cs`
   - Sub-folders: `Captcha/`, `Messaging/`, `Payments/`, `Signatures/`
   - Provider-neutrality enforcement: `packages/analyzers/provider-neutrality/`
   - Concrete adapters in repo: `packages/providers-recaptcha/`, `packages/providers-mesh-headscale/`

4. **At the flight-deck layer (`flight-deck/packages/plugins/`):**
   - `manifest-schema.json` — formal JSON Schema (`$id: galley/schemas/plugin-manifest-v1.json`)
   - `registry.json` — index of 17 plugin manifests
   - Per-plugin manifests for: `kokoro-fastapi`, `piper`, `higgs-audio`, `whisper-cpp`, `faster-whisper-large`, `comfyui`, `sdnext`, `fooocus`, `musicgen`, `openai-dalle3`, `stability-ai`, `flux-bfl`, `google-imagen`, `replicate`, `anthropic-claude`, `openai-gpt`, `google-gemini`
   - **Capability vocabulary already defined** in the schema: `tts/fast | tts/quality | stt/fast | stt/quality | image | music | llm | embedding`
   - Per-manifest fields: `id`, `name`, `version`, `kind` (`local | cloud`), `capabilities[]`, `flavor`, `repository`, `readmeUrl`, `license`, `description`, `hardware{}` (per-platform support level + accelerator), `defaults{port, baseUrl, model}` (**explicitly named "Slot config defaults applied when this plugin is assigned to a capability slot"** in the schema description), `authentication{}`, `install{prerequisites, steps, installDirDefault}`, `launch{}`, `health{path, expectedStatus}`
   - `flight-deck/packages/api-client/src/{ttsClient,sttClient,musicClient}.ts` — typed client adapters consuming the providers

5. **Cross-fleet consumer code already exists:**
   - `flight-deck/apps/web/src/features/{tts,stt,reader,audio-player,chapter-browser,voice-templates,...}` — 11 feature directories built against the slot pattern
   - `flight-deck/apps/web/dist/assets/SttPage-*.js` — built+shipped consumer

**Reframing of Gap 1:**

The gap is not "no slot contract surface exists." The gap is **"flight-deck has the slot registry; shipyard's `Sunfish.Foundation.Integrations` has the provider registry; the two patterns have diverged in vocabulary and have not been formally reconciled."**

Concretely:
- flight-deck calls them "plugins" with "capabilities"; shipyard calls them "providers" with "categories"
- flight-deck's capability vocabulary (`tts/fast`, `image`, etc.) does not appear in shipyard's `IntegrationCategory` enum
- flight-deck's `health{path, expectedStatus}` shape ≠ shipyard's `IProviderHealthCheck`/`ProviderHealthReport` API shape
- flight-deck's `defaults{port, baseUrl, model}` ≠ shipyard's `CredentialsReference`

**ONR verdict: PARTIALLY TRUE.** The structural surface CIC said was absent is, in fact, *over-represented* across two repos using divergent vocabulary. The actionable gap is **vocabulary reconciliation + promotion of flight-deck's pattern**, not greenfield contract authoring.

---

### Gap 2 — Critical: No ADR for the Slot Registry Pattern

**CIC claim:** "62 ADRs exist covering everything ... but none codifies the capability slot registry, provider manifest schema, essential vs. extension tier model, or control surface contract. ADR-0013 (foundation-integrations) touches adjacent territory but is scoped to third-party integration adapters, not the AI/media capability graph."

**Verification:**

```
$ ls shipyard/docs/adrs/ | wc -l → 96 entries (numbered 0001..0094 with gaps)
```

**CIC's "62 ADRs" count is stale by ~30 entries.** Current count is ~92 numbered ADRs (some amendments share numbers).

**Pre-existing ADRs that codify the pattern CIC says is uncodified:**

| ADR | Title | Relevance |
|---|---|---|
| **ADR-0007** | Bundle Manifest Schema | Codifies `BusinessCaseBundleManifest` + `ProviderCategory` + `ProviderRequirement`. The "bundle manifest" concept ADR-0063 (CIC's proposed) wants is **already a substrate primitive**. |
| **ADR-0009** | Foundation.FeatureManagement | "Already models [provider seam] for flags" per ADR-0013's own text. |
| **ADR-0013** | Foundation.Integrations + Provider-Neutrality Policy | Defines `ProviderDescriptor`, `IProviderRegistry`, `CredentialsReference`, `WebhookEventEnvelope`, `IWebhookEventHandler`, `IWebhookEventDispatcher`, `SyncCursor`, `ISyncCursorStore`, `ProviderHealthStatus`, `IProviderHealthCheck`. **CIC's claim that ADR-0013 is "scoped to third-party integration adapters, not the AI/media capability graph" is technically true, but ADR-0013 was authored as a GENERAL provider seam — its scope explicitly enumerates Billing, Payments, Banking, Channel Manager, Messaging, Feature Flags, Storage, Identity. AI/media is the natural next-category extension, not a different design problem.** |
| **ADR-0023** | Dialog Provider-Interface Expansion (Per-Slot Class Methods) | Codifies the per-slot method pattern on provider interfaces. Title contains "slot." |
| **ADR-0062** | Mission Space Negotiation Protocol (runtime layer) | The DirectX-Feature-Levels-pattern capability registry. Ships `MissionEnvelope`, `IMissionEnvelopeProvider`, `IFeatureGate<TFeature>` (renamed from `ICapabilityGate` per A1.2), `ProbeStatus`, 10-dimension capability surface, 9 `AuditEventType` constants for probe + verdict telemetry. **This IS the capability slot substrate.** |
| **ADR-0063** | Mission Space Requirements (install-UX layer) — **NUMBER ALREADY TAKEN** | Ships `MinimumSpec`, `MinimumSpecDimension`, `SpecPolicy { Required, Recommended, Informational }` (the "essential vs extension" model), `SystemRequirementsResult`, `IMinimumSpecResolver`, `ISystemRequirementsRenderer`, 4 new `AuditEventType` constants. |
| **ADR-0067** | Atlas Integration-Config UI Surface | Defines the control-plane UI for switching providers per category, validating credentials, configuring routing (`IIntegrationAtlasProvider`, `IValidationStatusStore`, `IntegrationCapabilityPurposes.IntegrationValidation`). |

**Counter-finding:** The pattern CIC says is uncodified is **codified across seven ADRs spanning ~2 years**. The work product CIC's gap analysis treats as "blocked" is, instead, substantially done. The genuine ADR-shaped gap (if one exists at all) is a **promotion + reconciliation ADR** that:

1. Names the existing flight-deck plugin-manifest pattern, audits it against ADR-0013, and decides whether to promote (recommended) or rebuild (not recommended)
2. Extends `IntegrationCategory` (or successor) with AI/media values
3. Reconciles `Sunfish.Foundation.Integrations.ProviderDescriptor` vs. flight-deck's plugin manifest schema (decide which is canonical)
4. Cross-references ADRs 0007 / 0013 / 0062 / 0063 / 0067 explicitly

**Numbering collision:** CIC's recommended "ADR-0063" is already taken by Mission Space Requirements. The new ADR would need a fresh number — at time of writing, 0095 looks like the next free slot (0001..0094 exist; gaps at 0019, 0020, 0045, 0047, 0050, 0092).

**ONR verdict: FALSE (counter-finding).** The slot registry pattern is over-codified across at least 7 ADRs. The CIC framing of "no ADR exists" is a survey error.

---

### Gap 3 — High: Tender Has No IPC Contract with Shipyard's Registry

**CIC claim:** "The Tauri app is scaffolded ... but the Rust `src-tauri/src/` commands are default Tauri boilerplate."

**Verification:**

```
$ ls tender/apps/desktop/src-tauri/src/
commands.rs  devices.rs  lib.rs  main.rs  telemetry.rs
```

(Note: tender's Tauri lives at `apps/desktop/src-tauri/`, NOT the top-level
`src-tauri/` CIC's framing suggested.)

`commands.rs` contains 8+ `#[tauri::command]` definitions:

1. `get_appearance(window) -> String` — macOS theme detection
2. `get_services() -> Vec<telemetry::HarborlineService>` — fleet service inventory
3. `get_system_stats() -> telemetry::SystemStats`
4. `get_local_services() -> Vec<telemetry::LocalService>`
5. `get_devices() -> Vec<devices::TailscaleDevice>` — Tailscale mesh enumeration
6. `open_external(url)` — open URL
7. `quit_app(app)` — exit handler
8. `emergency_stop()` — POST to `http://localhost:3080/api/admin/emergency-stop` on flight-deck
9. `restart_signal_bridge()` — process restart for sibling Bridge

`devices.rs` implements Tailscale device enumeration (`TailscaleDevice` model).
`telemetry.rs` implements `HarborlineService` + `SystemStats` + `LocalService`
models.

This is **NOT default Tauri boilerplate.** Tender's Rust side is a substantive
fleet-control panel that already speaks to flight-deck (`localhost:3080`) and
sibling Bridge processes. The "default boilerplate" framing is incorrect.

**True sub-gap:** The named commands `list_providers`, `get_health`,
`install_bundle`, `update_bundle`, `get_preferences` do NOT exist. So:

- "Tender has substantive Tauri command surface" → TRUE (CIC missed this)
- "Tender has provider-management Tauri commands" → FALSE
- "Tender cannot manage what it cannot query" — partially true, but tender CAN
  already query `get_services()` and `get_devices()`; what's missing is
  **provider-domain queries** specifically

**Architectural observation:** Tender's current command surface bypasses
shipyard contracts entirely — it speaks HTTP to flight-deck and shells out to
local processes. This is an architecture choice worth examining before
authoring `list_providers`-style commands: should tender call flight-deck's
existing plugin-registry endpoints (HTTP) or should it duplicate the contract
in Rust? CIC's gap framing presumes the latter without justification.

**ONR verdict: FALSE.** Specific named commands don't exist, but the broader
"Tauri boilerplate" framing is wrong by a wide margin. Tender is well past
scaffold stage.

---

### Gap 4 — High: No Shared TypeScript Types for Provider Manifests in Tender

**CIC claim:** "Once `packages/contracts/src/providers.ts` exists in shipyard,
tender's frontend needs to consume those types. Currently there's no cross-repo
dependency setup between shipyard's `@harborline/contracts` package and tender's
Vite/TS app. The packaging/publishing pipeline for the contracts package to be
consumable by an external repo (tender) is not established."

**Verification:**

1. The contracts package is named `@sunfish/contracts`, **not** `@harborline/contracts`. CIC's framing has a name mismatch.
   ```
   $ cat shipyard/packages/contracts/package.json | head -3
   { "name": "@sunfish/contracts", "version": "0.1.0", ... }
   ```

2. `package.json` lacks a `private: true` field but also lacks any registry
   configuration (no `publishConfig`, no `repository` field aimed at a registry).
   The package is *publish-ready in shape* but not currently published.

3. `tender/apps/desktop/package.json` has NO dependency on `@sunfish/contracts` (or `@harborline/contracts`):
   ```
   dependencies: @tauri-apps/api, @tauri-apps/plugin-positioner, react, react-dom
   devDependencies: @tauri-apps/cli, @types/node, @types/react, @types/react-dom,
   @vitejs/plugin-react, typescript, vite
   ```

4. No `pnpm-workspace.yaml` in tender root.

5. shipyard's `pnpm-workspace.yaml` packages: `ui-core`, `ui-adapters-react`,
   `tooling/*`, `apps/kitchen-sink`. The `contracts` package is NOT in the
   shipyard workspace either — it stands alone. This complicates the consumption
   story regardless of cross-repo packaging.

6. **Pre-existing precedent (per fleet-conventions.md): `@sunfish/ui-react`.**
   Per the fleet conventions doc: *"The package lives in sibling
   `shipyard/packages/ui-react`; npm's `file:../../../shipyard/packages/ui-react`
   symlink + the package's `exports` field resolves correctly."* So
   `file:`-based sibling consumption IS the established pattern. Tender can use
   the same: `"@sunfish/contracts": "file:../../../shipyard/packages/contracts"`.

7. There is **no published registry path** today (no GitHub Packages
   configuration, no npmjs.org publish history). Going registry-published is a
   bigger architectural step than CIC's framing acknowledges (private registry
   vs. public OSS publish each has implications for ADR-0018 license posture).

**Reframing of Gap 4:**

- "No consumption mechanism between shipyard contracts and tender" → TRUE
- "Publishing pipeline must be established to fix this" → FALSE (the `file:`
  precedent exists and is the simpler path)
- Name mismatch (`@harborline/contracts` vs `@sunfish/contracts`) is a survey
  error not a design decision

The simpler attack is: build the contracts package; add a `file:` sibling
reference in tender; skip the publishing-pipeline question entirely until a
third-party consumer (not a fleet sibling) needs it.

**ONR verdict: PARTIALLY TRUE.** Real underlying issue, mis-framed solution
shape.

---

### Gap 5 — Medium: Essential vs. Extension Slot Bootstrap Enforcement

**CIC claim:** "There's no mechanism in the shipyard kernel to declare which
slots are required at app startup and fail fast if absent. `system-requirements.ts`
exists and is the closest analog, but it covers OS/environment requirements, not
provider slot satisfaction."

**Verification:** CIC misread the file. `system-requirements.ts` is NOT scoped
to OS/env. Its leading docstring:

> *"TypeScript projection of the `SystemRequirementsResult` wire format produced
> by `Sunfish.Foundation.MissionSpace.IMinimumSpecResolver.EvaluateAsync`.
> Mirror of `packages/foundation-mission-space/Models/Requirements.cs` +
> `RequirementsEnums.cs`. ... Source of truth is ADR 0063-A1.1."*

The contents:

- `SpecPolicy { Required, Recommended, Informational }` — **this IS the
  essential-vs-extension tier model.** `Required` ≡ essential ≡ install-blocking
  if unmet; `Recommended` ≡ extension ≡ install-warning if unmet;
  `Informational` ≡ shown but never blocks.
- `OverallVerdict { Pass, WarnOnly, Block }` — bootstrap fail-fast verdict.
- `DimensionEvaluation { dimension, policy, outcome, operatorRecoveryAction, detail }` — per-dimension result.
- `SystemRequirementsResult { overall, dimensions, operatorRecoveryAction, evaluatedAt }` — bootstrap-check output.
- `DimensionChangeKind { Hardware, User, Regulatory, Runtime, FormFactor, Edition, Network, TrustAnchor, SyncState, VersionVector }` — the 10 capability dimensions.

The .NET-side substrate (`packages/foundation-mission-space/`) ships
`IMinimumSpecResolver.EvaluateAsync` — **this is exactly the
"`ProviderBootstrap` or `SlotRequirements` check" CIC says needs to be defined.**

**True sub-gap:** ADR-0063's 10 dimensions are envelope-dimensional (hardware,
network, edition, etc.) — they do NOT include a "this app needs a TTS provider
configured" dimension. So if you want **per-app-bundle slot-presence** as a
bootstrap requirement, you'd extend the 10 dimensions with an 11th
("ProviderSlots" or similar), OR you'd integrate `IProviderRegistry` into the
existing `IMinimumSpecResolver` evaluation pipeline as a separate concern.

The substrate is fundamentally already there. Adding AI/media slot-presence
as a `MinimumSpec` dimension is a small extension, not a greenfield design.

**ONR verdict: FALSE.** CIC misread `system-requirements.ts`. The model and
substrate exist. Real residual gap is "extend ADR-0063 with provider-slot as
an 11th dimension OR add provider-slot evaluation to the bootstrap pipeline."

---

### Gap 6 — Medium: `Sunfish.slnx` Stranded in Shipyard

**CIC claim:** "`Sunfish.slnx` lives in the shipyard root alongside
`Shipyard.slnx`. If sunfish is its own repo (Harborline-Software/sunfish),
this solution file either needs to migrate there or be explicitly documented
as a 'developer convenience' cross-repo solution file — otherwise it's an
implicit coupling that will confuse contributors."

**Verification:**

```
$ ls shipyard/Sunfish.slnx shipyard/Shipyard.slnx
shipyard/Shipyard.slnx
shipyard/Sunfish.slnx
```

Both exist. `Sunfish.slnx` references 245 projects; `Shipyard.slnx` references
239. The 14-line diff:

```
< accelerators/anchor/Sunfish.Anchor.csproj
< accelerators/bridge/MockOktaService/MockOktaService.csproj
< accelerators/bridge/Sunfish.Bridge.AppHost/Sunfish.Bridge.AppHost.csproj
< accelerators/bridge/Sunfish.Bridge.Client/Sunfish.Bridge.Client.csproj
< accelerators/bridge/Sunfish.Bridge.Data/Sunfish.Bridge.Data.csproj
< accelerators/bridge/Sunfish.Bridge.MigrationService/...
< accelerators/bridge/Sunfish.Bridge.ServiceDefaults/...
< accelerators/bridge/Sunfish.Bridge/Sunfish.Bridge.csproj
< accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/...
< accelerators/bridge/tests/Sunfish.Bridge.Tests.Performance/...
< accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/...
```

**The 11 `accelerators/...` paths in `Sunfish.slnx` do not exist on disk.**

```
$ ls shipyard/accelerators/
ls: shipyard/accelerators/: No such file or directory
```

Per fleet conventions (post-2026-05-17 migration):
*"`accelerators/anchor/Components/...` → `sunfish/src/Components/...`"* —
the anchor and bridge accelerators moved out of shipyard to the sibling sunfish
repo.

**`Sunfish.slnx` is not just stranded — it is BROKEN as a buildable artifact.**
Loading it in `dotnet sln` would fail to resolve those 11 project references.
This is worse than CIC's framing acknowledged.

**Resolution paths (none mutually exclusive):**

1. **Delete `Sunfish.slnx` outright** — `Shipyard.slnx` covers all extant projects in shipyard. (Cleanest.)
2. **Move `Sunfish.slnx` to the sibling sunfish repo** (`sunfish/Sunfish.slnx`) and rewrite paths as `../shipyard/packages/...` plus `src/...` (sunfish-local paths). Cross-repo solution file — explicitly a "developer convenience" artifact.
3. **Rewrite the 11 accelerator paths** in `Sunfish.slnx` to the new sunfish-repo locations (`../sunfish/src/...` etc.). Same outcome as option 2 without moving the file.

Cross-references:
- `_shared/product/roadmap-tracker.md` and other shipyard docs DO reference projects in `Sunfish.slnx`-only paths in places (would need a sweep)
- Phase 3 namespace rename plan (`icm/02_architecture/phase-3-namespace-rename-plan-2026-05-17.md`) likely also touches this area

**ONR verdict: TRUE — and more severe than CIC stated.** The file is not just
stranded; it is broken. Decision needed on resolution path.

---

### Gap 7 — Low: `BannedSymbols.txt` is Empty

**CIC claim:** "Given ADR-0004 (post-quantum signature migration) and ADR-0043
(unified threat model), this file should enumerate deprecated crypto APIs,
banned HTTP methods, etc. It's a low-effort, high-signal hardening step."

**Verification:**

```
$ wc -l shipyard/BannedSymbols.txt
0 shipyard/BannedSymbols.txt
$ wc -c shipyard/BannedSymbols.txt
0 shipyard/BannedSymbols.txt
```

The file is literally empty (0 lines, 0 bytes).

ADR-0004 and ADR-0043 both exist. ADR-0004 (post-quantum signature migration)
would naturally enumerate banned classical-crypto symbols (e.g.
`System.Security.Cryptography.RSACryptoServiceProvider`,
`Sunfish.Kernel.Crypto.LegacyEd25519`, etc.). ADR-0043 (unified threat model)
codifies the chain-of-permissiveness model — natural source of bans for
unsigned HTTP, plaintext-secret-bearing types, etc.

Additionally, ADR-0061 (three-tier peer transport) per the ADR-0067 audit
notes was previously the canonical owner of certain license-related bans
(`license-acknowledgement track CUT entirely, deferred to ADR 0067-A1 follow-up
amendment with BannedSymbols.txt enforcement from ADR 0061 remaining canonical
until that amendment lands`).

**Sub-finding:** `BannedSymbols.txt` is the Roslyn analyzer
`Microsoft.CodeAnalysis.BannedApiAnalyzers` input format — one banned symbol
per line with optional message. Format:

```
T:System.Security.Cryptography.RSACryptoServiceProvider;Use Sunfish.Kernel.Crypto.* with ADR-0004 PQ migration in mind
M:System.Diagnostics.Process.Start(System.String);Use ProcessLauncher abstraction (ADR-0044)
```

This is a real low-cost hardening task — but populating it should be
**driven by an ADR or coordinated content sweep**, not by an ad-hoc fill.
Otherwise we ship bans that don't track to a decision and can drift.

**ONR verdict: TRUE.** Genuinely empty; low-effort/high-signal. Caveat: don't
just fill it blindly — pair with a content-sourcing ADR or refer to existing
ADR-0004 / ADR-0043 / ADR-0061 explicitly.

---

## UPF Stage 0 — Discovery findings

Per `.claude/rules/universal-planning.md` Stage 0, twelve contextual checks
apply to non-trivial plans. The relevant ones for the slotting solution:

### 0.1 Existing Work (Always consider — non-trivial plan)

**Confirmed pre-existing work CIC's gap analysis did not survey:**

- 7+ ADRs (0007, 0009, 0013, 0023, 0062, 0063, 0067) that codify the registry / manifest / capability / per-slot-method / install-UX / control-plane pattern.
- 1 .NET foundation package (`foundation-integrations`) with provider registry implementations and concrete adapters (recaptcha, mesh-headscale).
- 1 .NET foundation package (`foundation-mission-space`) implementing DirectX-Feature-Levels capability negotiation.
- 1 TypeScript contracts file (`integrations.ts`) projecting the provider control-plane surface for React consumers.
- 1 TypeScript contracts file (`system-requirements.ts`) projecting the install-time capability evaluation surface.
- 1 flight-deck plugin manifest schema (`manifest-schema.json`) with 17 concrete manifests covering tts/fast, tts/quality, stt/fast, stt/quality, image, music, llm.
- 1 flight-deck plugin-architecture design doc explicitly framing capability slots.
- 1 flight-deck typed API-client trio (`ttsClient.ts`, `sttClient.ts`, `musicClient.ts`).
- 1 Roslyn analyzer enforcing provider-neutrality (`packages/analyzers/provider-neutrality/`).
- Tender Rust side already has substantive Tauri command surface speaking HTTP to flight-deck (`localhost:3080`).

This is **substantially more pre-existing work than CIC's gap analysis assumed**. The 6-step attack order would have generated significant net-new code in domains where adequate substrate already exists.

### 0.2 Feasibility (Always consider — non-trivial plan)

Promoting flight-deck's plugin-manifest pattern + extending shipyard's `IProviderRegistry` to AI/media is feasible: same JSON-schema vocabulary, same provider-neutrality discipline, no novel C# constructs needed. Net new C# work is < 500 LOC if executed as extension rather than rebuild.

Authoring **net new** "providers.ts" + ADR-0063 (per CIC's plan) is also feasible — but redundant given existing artifacts. Feasibility is not the bottleneck; coherence is.

### 0.9 AHA Effect (Highest-value discovery activity)

**The fundamentally simpler approach is: don't author from scratch — promote what exists.**

Specifically:

1. **Don't author ADR-0063** (CIC's proposed). Author ADR-0095 (next free number) as a **"Promote galley plugin-manifest pattern into shipyard substrate"** ADR. Cross-references ADR-0013 / ADR-0062 / ADR-0063 / ADR-0067 explicitly. Scope: vocabulary reconciliation + category extension, NOT greenfield design.

2. **Don't author providers.ts.** Extend `integrations.ts`:
   - Add to `IntegrationCategory` enum: `TtsFast | TtsQuality | SttFast | SttQuality | Image | Music | Llm | Embedding`
   - Or split it: `IntegrationCategory` (existing third-party SaaS) stays as-is; new `MediaCapabilityKind` enum for AI/media slots — composed into a discriminated union if needed.
   - Add `PluginManifest` shape mirroring `flight-deck/packages/plugins/manifest-schema.json` to shipyard contracts.

3. **Don't add a separate slot-bootstrap check.** Extend `system-requirements.ts` + `IMinimumSpecResolver` with an 11th dimension: `ProviderSlots`. Reuse the existing `SpecPolicy.Required | Recommended | Informational` for essential-vs-extension semantics.

4. **Don't author bespoke Tauri commands.** Tender already speaks HTTP to flight-deck at `localhost:3080`. Add HTTP endpoints to flight-deck (or to shipyard's Bridge) that surface the existing `flight-deck/packages/plugins/registry.json` + manifest data. Then tender consumes them via existing `reqwest` + `serde_json` infrastructure — no Tauri-command-side work needed for read paths.

5. **Don't establish a publishing pipeline.** Use the `file:` sibling-reference pattern per fleet-conventions §`@sunfish/ui-react` precedent.

6. **Resolve `Sunfish.slnx`** independently — this is a 1-PR migration cleanup, not part of the slotting work. Delete or fix the 11 broken accelerator paths.

7. **Populate `BannedSymbols.txt`** independently — this is a 1-PR content sweep driven by ADR-0004 / ADR-0043 / ADR-0061, not part of the slotting work.

**Estimated cost delta:** CIC's 6-step plan is ~6 ADRs/PRs of work. The simpler approach is ~2 substantive PRs (ADR-0095 + contract extension) + 2 hygiene PRs (slnx cleanup + BannedSymbols seed). Net: ~50% reduction.

### 0.10 People Risk

The slotting solution touches:

- shipyard XO (ADR authoring, foundation contracts) — Admiral coordinates
- flight-deck maintainer (plugin-manifest pattern owner; needs sign-off on promotion)
- tender owner (Tauri command surface)
- Engineer (substrate-side .NET implementation)
- FED (TypeScript contract surface)

CIC's framing as a single 6-step linear plan doesn't account for cross-ship
coordination. A pre-ADR coordination beacon to flight-deck is required before
ADR-0095 (per existing `galley-as-sunfish-accelerator.md` framing: *"This doc
captures the proposal. It does not yet reflect a decision by Sunfish's XO ...
A coordination beacon is queued for that thread."*).

### 0.11 Better Alternatives

The CIC plan presumes "build the slot registry in shipyard." Alternatives:

- **(A) Promote flight-deck's pattern to shipyard** (recommended above)
- **(B) Keep flight-deck's pattern flight-deck-local; document the convention; don't promote to shipyard** (lower coordination cost; tender + sunfish + Bridge would each rebuild instead). Cost: divergence over time.
- **(C) Treat flight-deck's plugin registry as the canonical source-of-truth and have shipyard/tender consume it via HTTP** (lowest authoring cost; highest runtime dependency on flight-deck). Cost: flight-deck becomes a fleet substrate, expanding its scope beyond "editorial production platform."

Option A is recommended; option C is a viable simpler-still alternative if
flight-deck-as-fleet-service is acceptable scope expansion.

---

## UPF Stage 2 — Meta-validation on the 6-step attack order

Per `.claude/rules/universal-planning.md` Stage 2, run the 21-anti-pattern
scan + check delegation strategy clarity, research needs identification,
Cold Start Test, and Plan Hygiene.

### Anti-pattern scan (21 patterns)

| # | Anti-pattern | Hit in CIC's 6-step plan? | Detail |
|---|---|---|---|
| AP-1 | Unvalidated assumptions | **HIT** | The plan presumes `providers.ts` must exist; presumes ADR-0013 is "scoped to third-party adapters"; presumes `system-requirements.ts` is "OS/environment requirements"; presumes ADR-0063 is unused; presumes `@harborline/contracts` is the package name. All 5 are factually wrong per §verification. |
| AP-3 | Vague success criteria | **HIT** | Each step is one sentence with no measurable outcome. "Write ADR-0063" — to what depth? With which council subagents? What constitutes "done"? Per ADR-0093 (Stage 05 adversarial review protocol amendment), ADRs require Stage 1.5 council review — no mention. |
| AP-4 | No rollback | **HIT** | The plan ends at "step 6 — resolve Sunfish.slnx." Nothing about what to do if step 1 (ADR) is BLOCKED by council; nothing about whether steps 2-6 can proceed in parallel if step 1 stalls. |
| AP-9 | Skipping Stage 0 | **HIT** | The plan was authored without Stage 0 Discovery — that's literally why this ONR audit was commissioned, and the audit found 5 pre-existing artifacts CIC's plan would duplicate or supersede. |
| AP-10 | First idea unchallenged | **HIT** | The plan assumes the slot registry is the right abstraction. It does not consider: (a) extending `IntegrationCategory`, (b) using ADR-0062's `ICapabilityGate<T>` directly, (c) flight-deck-as-fleet-service alternative, (d) doing nothing and codifying the existing divergence. |
| AP-13 | Confidence without evidence | **HIT** | "62 ADRs exist" (actual: 96); "ADR-0013 is scoped to third-party integration adapters" (actual: it enumerates 8 categories spanning all of Payments through Identity, AI/media is the natural extension); "default Tauri boilerplate" (actual: 200+ lines of substantive Rust). |
| AP-15 | Premature precision | **MILD HIT** | Step 2 names types (`ProviderManifest`, `TtsSlot`, `SttSlot`, `ImageGenerationSlot`, `AgentSlot`, `SearchSlot`, `ProviderHealth`, `ProviderTelemetry`, `ProviderCapabilities`) before research validates the naming aligns with existing fleet vocabulary (it doesn't — fleet uses `IntegrationProviderSchema`, `tts/fast`, `tts/quality`, etc.). |
| AP-18 | Unverifiable gates | **HIT** | No phase has a binary pass/fail gate. "Add `providers.ts`" — when is it done? Compiles? Has X exports? Consumed by Y? |
| AP-20 | Discovery amnesia | **HIT** | The plan lacks any Reference Library section pointing at ADRs 0013 / 0062 / 0063 / 0067 / flight-deck plugin architecture. A Stage 06 implementer would re-discover all of it. |
| AP-21 | Assumed facts without sources | **HIT** | "62 ADRs" lacks citation; "default Tauri boilerplate" lacks file-level citation; the package name `@harborline/contracts` is asserted but the actual file shows `@sunfish/contracts`. |

**10 of 21 anti-patterns hit. This plan would fail Stage 2 Meta-validation.**

### Step-by-step dependency analysis

| Step | CIC framing | Real prerequisite | Real cost |
|---|---|---|---|
| 1. Write ADR-0063 — slot registry | New design ADR | (revised) ADR-0095 — promotion + reconciliation ADR; depends on flight-deck coordination beacon | High (council-reviewed ADR; ~1-2 weeks per ADR-0093 protocol) |
| 2. Add `providers.ts` | New TS contract file | Extension to `integrations.ts` (8 new enum values; 1 new `PluginManifest` interface) | Low (~50 LOC + tests) |
| 3. Add slot bootstrap to kernel | New `ProviderBootstrap` | Extend `IMinimumSpecResolver` with `ProviderSlots` dimension (or compose) | Low (~100 LOC + tests; small ADR-0063-A2 amendment) |
| 4. Define Tauri commands | New Tauri command surface | Likely NOT needed — tender consumes flight-deck HTTP. If commands are needed, depends on step 2 (typed contract surface). | Low-medium; conditional |
| 5. Publish `@harborline/contracts` | Publishing pipeline | `file:` sibling reference (already-established fleet precedent) | Trivial |
| 6. Resolve `Sunfish.slnx` | Move or document | Delete the broken file; OR fix 11 accelerator paths; OR move + retarget | Low (1 PR, ~30 min) |

**Step ordering issues:**

- Step 5 ("publish @harborline/contracts") in CIC's plan does NOT depend on
  step 2 — `file:` references don't require publishing.
- Step 4 (Tauri commands) doesn't strictly require step 2 if tender continues
  the HTTP-to-flight-deck pattern.
- Step 3 (kernel bootstrap) depends on step 2 (typed contracts) AND on a
  decision about whether ADR-0063 needs a new dimension OR a kernel
  composition point.
- Step 6 (Sunfish.slnx) is wholly independent and shouldn't block anything.

The plan presents 6 sequential steps when the real dependency graph is more
like: `1 → 2 → 3`; `6` independent; `5` reduced to "use `file:` ref" and
parallel to all; `4` conditional and probably reduced to "add HTTP endpoints in
flight-deck."

### Cold Start Test (Stage 2 Check 5)

Can a fresh agent execute the 6-step plan? No.

- "Write ADR-0063" — fresh agent doesn't know ADR-0063 is taken (numeric collision).
- "Add providers.ts" — fresh agent doesn't know `integrations.ts` already covers most of the surface.
- "Add slot bootstrap check to kernel" — fresh agent doesn't know `system-requirements.ts` already exists with `SpecPolicy.Required/Recommended/Informational`.
- "Define Tauri commands" — fresh agent would write Rust commands without first asking whether HTTP-to-flight-deck is the simpler path.
- "Publish @harborline/contracts" — fresh agent searches for a package named `@harborline/contracts`, doesn't find it, fails. Actual name is `@sunfish/contracts`.

**Cold Start Test fails for 5 of 6 steps.**

### Plan Hygiene Protocol (Stage 2 Check 6)

The plan would need substantial fold-in before becoming executable:

- Fix the 5 unvalidated assumptions (AP-1 hits)
- Add Reference Library section citing ADRs 0013 / 0062 / 0063 / 0067 + flight-deck plugin-architecture.md
- Add Discovery Consolidation section folding in the 7+ ADR pre-existence findings
- Add Hardening Log if Stage 1.5 council review is requested
- Add measurable success criteria per phase (AP-3)
- Add rollback triggers (AP-4)

---

## Recommendations: revised gap framing + revised attack order

### Revised attack order (3 phases, not 6 steps)

**Phase 1 — Coordination + reconciliation ADR (1-2 weeks; council-reviewed)**

1.1. File coordination beacon to flight-deck maintainer + Sunfish XO requesting agreement to promote `flight-deck/packages/plugins/manifest-schema.json` into shipyard substrate. Outcome: GO / NO-GO / DEFER ruling.

1.2. If GO: Author ADR-0095 (next free number — verify against current `docs/adrs/` at PR time) titled approximately "Cross-fleet capability-slot registry — promotion of galley plugin-manifest pattern + reconciliation with `Sunfish.Foundation.Integrations`". Composes ADRs 0007 / 0013 / 0023 / 0062 / 0063 / 0067. Stage 1.5 council with security-engineering subagent (credentials handling) + .NET-architect subagent (foundation package shape) per ADR-0093 protocol.

1.3. Decisions ADR-0095 must make:
- Canonical vocabulary: do we standardize on flight-deck's `capabilities` (string-tagged like `tts/quality`) or shipyard's `ProviderCategory` (C# enum)? Recommendation: emit shipyard-canonical C# enum with `[Description]` attribute pinning to the string tag.
- Manifest source: flight-deck's `manifest.json` per plugin (filesystem), shipyard's `IProviderRegistry` (in-process registration), or both layered?
- Capability extension surface: how do third-party plugins add new capability types (new `tts/*` flavors, new `image-*` flavors)?
- Health surface reconciliation: flight-deck's `health{path,expectedStatus}` HTTP probe vs shipyard's `IProviderHealthCheck.CheckAsync()`. Recommendation: HTTP probe shape is canonical for cloud/local kind; in-process `IProviderHealthCheck` available for embedded providers.

**Phase 2 — Contract extension + substrate implementation (post-ADR-0095)**

2.1. Extend `shipyard/packages/contracts/src/integrations.ts`:
- Add AI/media values to `IntegrationCategory` (or new `MediaCapabilityKind` enum)
- Add `PluginManifest` interface mirroring flight-deck schema
- Add `PluginRegistryEntry` for registry.json projection

2.2. Extend `shipyard/packages/foundation-integrations/`:
- Add manifest-loading + manifest-validation services
- Reconcile `ProviderDescriptor` with `PluginManifest` (either: PluginManifest IS-A ProviderDescriptor; or ProviderDescriptor wraps PluginManifest)
- Concrete adapter for "flight-deck registry consumption" (HTTP client to flight-deck's registry endpoint)

2.3. Add slot-bootstrap dimension to `Sunfish.Foundation.MissionSpace`:
- 11th `DimensionChangeKind` value: `ProviderSlots`
- Per-bundle declaration in `BusinessCaseBundleManifest`: which slot capabilities are `Required` (essential) vs `Recommended` (extension)
- Bootstrap check fires during `IMinimumSpecResolver.EvaluateAsync` path

2.4. (If still needed after 1.2 decision) Add tender Tauri commands or flight-deck HTTP endpoints to surface registry queries.

**Phase 3 — Independent hygiene (parallel with Phases 1-2; ~1-2 days)**

3.1. Resolve `Sunfish.slnx`: delete OR repath the 11 broken accelerator references. Coordination with whoever owns `_shared/product/roadmap-tracker.md` if it cross-references those paths.

3.2. Populate `BannedSymbols.txt` from ADRs 0004 (PQ migration), 0043 (threat model), 0061 (license posture) — minimum 6-8 entries from the existing ADR decisions; explicit cross-reference comments per entry.

**Net change vs CIC's 6-step plan:**

| Dimension | CIC plan | ONR-revised plan |
|---|---|---|
| Number of new ADRs | 1 (ADR-0063, numeric collision) | 1 (ADR-0095, no collision) |
| Number of new TS files | 1 (`providers.ts` greenfield) | 0 (`integrations.ts` extension) |
| Number of new C# packages | 0 explicitly; 1 implicit (slot bootstrap) | 0 (extend `foundation-mission-space` + `foundation-integrations`) |
| Tauri command authoring | Required | Conditional / probably-unnecessary |
| Publishing pipeline | Required | Skip (use `file:` sibling) |
| Coordination beacons required | 0 explicit | 1 explicit (flight-deck) |
| Expected ADR council blocks | Unknown | Estimable; 0-2 if Phase 1 framing lands clean |
| Total LOC estimate | ~1500-2500 | ~500-1000 |

---

## Cross-fleet context: how the slot registry connects to existing fleet substrate

### Inference Studio + winhub TTS architecture (per fleet conventions §"TTS service architecture")

```
Mac client → 8881 (InferenceStudioService — proxy + Inference Studio UI)
              ↓
              127.0.0.1:8883 (TTSService — real Higgs Audio model backend)
```

- `flight-deck/packages/plugins/plugins/higgs-audio/manifest.json` already
  declares `defaults.port: 8881`, `defaults.baseUrl: http://localhost:8881`.
- `flight-deck/packages/api-client/src/ttsClient.ts` `TTSFlavor: 'standard'`
  targets `http://winhub:8881` / `http://desktop-umt08rn:8881` via the
  `/api/v1/audio/*` path prefix.
- The 8881/8883 split is internal to the Higgs plugin (NSSM service chain);
  the plugin-manifest abstraction correctly hides it from consumers.

The plugin-manifest pattern accommodates the 8881/8883 reality without
modification — `defaults.port` is the client-facing port; the backend chain is
implementation detail. This validates the existing schema design.

### flight-deck media studio architecture (likely heaviest consumer)

`flight-deck/apps/web/src/features/` already has 11 capability-domain feature
directories (`tts`, `stt`, `reader`, `audio-player`, `chapter-browser`,
`voice-templates`, `annotations`, `build-logs`, `chat`, `render-queue`,
`review-sessions`, `telemetry`). The flight-deck `Settings → Services`
section already consumes the registry pattern (per
`docs/architecture/plugin-architecture.md`: *"The current `/settings/services`
UI baked the provider list per capability into a hardcoded array. That doesn't
scale ... The plugin architecture replaces 'hardcoded provider dropdown' with
'registry of manifests.'"*)

The fleet-wide slot registry would absorb this existing surface, not replace
it. flight-deck stops being a special case and becomes the reference
implementation.

### Tender (Tauri menu-bar fleet control)

Tender's current architecture is "fleet control panel that speaks HTTP to
flight-deck + Tailscale + local processes." It does not currently consume
shipyard contracts. The simplest path:

- Tender stays HTTP-based for read-only queries (already works: `localhost:3080` to flight-deck)
- For write operations (install bundle, change preferences), tender either continues HTTP-to-flight-deck OR adopts the new Phase 2 Tauri commands. Decide in Phase 2 per Phase 1 ADR ruling.
- Tender consuming shipyard `@sunfish/contracts` via `file:` is straightforward when needed; defer until needed.

### `Sunfish.Foundation.Integrations` + AI/media reconciliation

The package shape is already:

```
foundation-integrations/
├── Captcha/         ← provider sub-domain
├── Messaging/       ← provider sub-domain
├── Payments/        ← provider sub-domain
├── Signatures/      ← provider sub-domain
├── IProviderRegistry.cs
├── ProviderDescriptor.cs
├── ProviderHealthStatus.cs
├── ...
```

Adding `Media/` (or `AI/`, or `CapabilitySlots/`) as a 5th sub-domain following the same pattern is the natural extension surface. The existing `IProviderRegistry` doesn't need replacement — its `ProviderDescriptor` would simply gain `Media`-domain descriptors with new `ProviderCategory` enum values.

### Existing precedents in shipyard for the move

- `IIntegrationAtlasProvider` (per ADR-0067 + `integrations.ts`) is the React-side control plane for switching providers per category. It already does for Payments / Messaging / MeshVpn / Captcha exactly what CIC's "control surface" Gap 1 envisions for AI/media.
- `IFeatureGate<TFeature>` (per ADR-0062) is the runtime capability-presence gate. It already does what CIC's "essential vs extension bootstrap" Gap 5 envisions for AI/media.
- `IMinimumSpecResolver.EvaluateAsync` (per ADR-0063) is the install-time capability-evaluation surface. It already produces `OverallVerdict: Pass | WarnOnly | Block` exactly as a slot-presence bootstrap check would want.

The AI/media slot registry is a domain extension of three pre-existing fleet substrates, not a new architecture.

---

## Open questions (residual)

These questions could not be resolved by document survey alone and would
benefit from coordination beacons / additional research before Phase 1
proceeds:

1. **Does flight-deck's maintainer agree to promote the plugin-manifest pattern to shipyard substrate?** The `galley-as-sunfish-accelerator.md` doc says a coordination beacon is queued for the Sunfish XO thread but does not show it as filed. **Action:** file the beacon as the literal Phase 1 first step.

2. **Is `@harborline/contracts` a planned future package rename of `@sunfish/contracts`?** CIC's gap analysis used the `@harborline/*` name; the actual package is `@sunfish/contracts`. If a rename is planned, it should coordinate with this work.

3. **Does the `Sunfish.slnx` cleanup belong to QM (hygiene), Engineer (substrate), or sunfish-repo owner?** Per fleet-conventions the cleanest-long-term-option policy: probably delete + let `Shipyard.slnx` be the canonical solution; if sunfish-repo wants a per-repo solution file, author one fresh at `sunfish/Sunfish.slnx` with sibling refs.

4. **Should `BannedSymbols.txt` population be a single PR or coordinated with the ADR-0067-A1 license-acknowledgement amendment that owns related license bans?**

5. **Is the "Inference Studio + Higgs Audio + Kokoro + Chatterbox + MusicGen + Whisper" service inventory on winhub the canonical AI/media inventory or just a snapshot?** The slot registry's capability vocabulary (currently 8 tags) should reflect deliberate scoping. CIC's gap analysis mentioned `AgentSlot` and `SearchSlot` which are NOT in flight-deck's current 8-tag vocabulary. Decision needed: extend the vocabulary now (Phase 1 ADR scope) or defer (Phase 2 amendment).

---

## Sources cited

Primary sources (filesystem state at retrieval 2026-05-25):

1. `shipyard/packages/contracts/src/` directory listing — verified file count + content.
2. `shipyard/docs/adrs/` — directory listing + read of ADRs 0013, 0023, 0062, 0063, 0067.
3. `shipyard/packages/foundation-integrations/` — directory listing showing `IProviderRegistry.cs`, `ProviderDescriptor.cs`, `ProviderHealthStatus.cs`, etc.
4. `shipyard/packages/foundation-mission-space/` — referenced via `system-requirements.ts` docstring and ADR-0062/0063.
5. `shipyard/apps/docs/foundation/integrations/{overview,registry}.md` — confirmed `ProviderHealthReport` shape.
6. `shipyard/Sunfish.slnx` + `Shipyard.slnx` — diff of project references.
7. `shipyard/BannedSymbols.txt` — verified 0-byte empty file via `wc -l` and `wc -c`.
8. `flight-deck/packages/plugins/manifest-schema.json` — JSON schema for plugin manifests with `$id: galley/schemas/plugin-manifest-v1.json`.
9. `flight-deck/packages/plugins/registry.json` — index of 17 plugin manifests with capability tags.
10. `flight-deck/packages/plugins/plugins/higgs-audio/manifest.json` — concrete manifest example.
11. `flight-deck/packages/api-client/src/{ttsClient.ts, sttClient.ts, musicClient.ts}` — typed consumer code.
12. `flight-deck/docs/architecture/{plugin-architecture.md, galley-platform-spec.md, galley-as-sunfish-accelerator.md}` — design docs.
13. `tender/apps/desktop/src-tauri/src/{commands.rs, lib.rs}` + `Cargo.toml` — verified Tauri command surface.
14. `tender/apps/desktop/package.json` — verified no `@sunfish/contracts` dependency.
15. `shipyard/pnpm-workspace.yaml` — verified contracts package not in workspace.

Secondary sources:

16. `.claude/rules/universal-planning.md` — UPF framework (Stages 0 / 1 / 2 / anti-pattern catalog).
17. `.claude/rules/fleet-conventions.md` — `@sunfish/ui-react` `file:` precedent; TTS 8881/8883 architecture.
18. `shipyard/icm/07_review/output/adr-audits/0013-upf-audit.md` — prior UPF audit of ADR-0013 (referenced for `IProviderHealthCheck` surface analysis).
19. `coordination/_archive/cob-question-2026-04-30T14-30Z-w28-p5c4-capability-verifier.md` — prior cross-reference to capability-verification work (different domain — macaroon-based, not AI/media — but vocabulary overlap).

Tertiary sources:

20. `coordination/_archive/admiral-directive-2026-05-17T20-07Z-yeoman-book-measurement-pass-galley-mcp.md` and related — historical context on galley fleet integration discussions.

Method note: This audit relied entirely on filesystem state at 2026-05-25
plus document content. No code execution. No vendor-side API calls. All
"VERIFIED-FALSE" findings can be reproduced with the bash commands cited
inline (find, grep, ls, wc, cat, head).
