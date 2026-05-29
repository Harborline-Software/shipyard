# ONR research — UPF on slotting-architecture in general (2026-05-25)

**Requester:** CIC (via Admiral)
**Scope:** Apply Universal Planning Framework to the question of whether
"slotting architecture" — as a general pattern — is the right substrate for
coordinating background-service capabilities across the fleet's first two
use cases: property management (sunfish) and media publishing (flight-deck).
Compare six candidate architectural patterns; recommend per-use-case.
**Out of scope:** Authoring the new ADR; authoring production code; making
the GO / NO-GO ratification call. This deliverable feeds a future ADR + CIC
ratification.
**Authoritative sources:** flight-deck `packages/plugins/manifest-schema.json`
+ 17 plugin manifests + `docs/architecture/plugin-architecture.md`; shipyard
`packages/foundation-integrations/` + `packages/foundation-mission-space/`
+ `packages/contracts/src/{integrations,system-requirements}.ts`; ADRs 0007
/ 0013 / 0023 / 0062 / 0063 / 0067; sunfish `src/Sunfish.Anchor.csproj` +
`src/Services/`; prior 7-gap audit (shipyard PR #146 / merged).
**Status:** Final draft for CIC review.

**Verbatim CIC framing (preserve in record):**

> Conduct the upf on the idea of slotting-architecture in general to support
> the first use cases of property management (sunfish) and media publishing
> (flight-deck). Slotting standardizes required feature providers but allows
> them to be exchanged or swapped. Is there a better way. If I want a single
> app to provide a certain set of capabilities what is the best way to
> coordinate the needed background services that can be setup/exchanged with
> the needed ones to provide desired features. Is everything a plugin, are
> needed slots registered that are resolved by a cluster?

---

## 1. Executive summary — direct answers to CIC's four questions

### Q1. Is there a better way [than slotting]?

**Yes — "slotting" as a single monolithic pattern is the wrong shape.** The
fleet does not need ONE coordination mechanism for ALL capabilities. It needs
**three layered mechanisms, each matched to the swap frequency and
ownership shape of the capability tier they coordinate.** Calling all three
"slotting" loses the distinctions that matter; calling none of them
"slotting" loses the working flight-deck pattern. The right answer is a
**bifurcated architecture** ("layered" is more accurate but "bifurcated"
makes the point):

1. **Concrete cluster-DI** for domain capabilities (tenant, property, lease,
   invoice in sunfish; manuscript, render-queue, voice-templates in
   flight-deck) — never swapped in production; .NET `IServiceCollection`
   extension methods per cluster. No registry layer needed.
2. **Provider registry pattern (`IProviderRegistry` + `ProviderDescriptor`)**
   for infrastructure capabilities that are vendor-swappable but tightly
   bounded (payments, messaging, blob storage, captcha, auth, mesh VPN).
   Shipyard already has this for sunfish; ADR 0013 codifies it. Five
   categories live; AI/media is a sixth-category extension.
3. **Plugin-manifest pattern (`manifest-schema.json` + per-plugin manifest
   files)** for inference capabilities where the swap surface is large,
   third-party-extensible, and hardware-dimensional (TTS, STT, image, music,
   LLM, embedding). Flight-deck has this; sunfish does not need it.

"Slotting" as CIC defined it ("standardizes required feature providers but
allows them to be exchanged") is a clean fit ONLY for tier 2 and tier 3.
For tier 1 (domain capabilities) it is overkill and harmful — it adds
indirection where none is wanted.

### Q2. What is the best way to coordinate the needed background services that can be setup/exchanged with the needed ones to provide desired features?

**The best way is a three-tier composition pattern**, declared at the app
level via `BusinessCaseBundleManifest` (ADR 0007 substrate already in
place):

```
App bundle declares
  requiredDomainBlocks      → tier 1 → resolved via DI ServiceCollectionExtensions
  providerRequirements      → tier 2 → resolved via IProviderRegistry + category routing
  capabilitySlots           → tier 3 → resolved via plugin-manifest registry (per-host)
```

Concretely: a sunfish app bundle requires `[blocks-properties,
blocks-leases, blocks-invoices, blocks-payments]` (tier 1, all concrete),
and declares `providerRequirements: [Payments, TransactionalEmail,
Storage, Captcha, IdentityProvider]` (tier 2 — vendor pluggable). It
declares NO tier-3 capability slots because property management has no AI
inference surface in MVP scope. A flight-deck app bundle requires
`[blocks-manuscript, blocks-render-queue, blocks-voice-templates]` (tier
1), declares `providerRequirements: [Storage, IdentityProvider]` (tier 2 —
minimal), and declares `capabilitySlots: [tts/fast, tts/quality, stt/fast,
stt/quality, image, music, llm, embedding]` (tier 3 — the actual swap
surface).

At runtime: tier 1 is resolved by .NET DI; tier 2 by
`IProviderRegistry.GetByCategory()`; tier 3 by the plugin registry (today
filesystem + JSON manifests; tomorrow a fleet substrate package).

**Coordination is NOT a single shape. The right answer is "yes, registered
slots; AND a plugin model; AND pure DI — composed by tier."**

### Q3. Is everything a plugin?

**No — and trying to make everything a plugin actively harms sunfish.**

- "Is the tenant entity a plugin?" — No; it's the load-bearing concrete
  domain primitive that every other sunfish capability composes against.
  Making it pluggable would mean every consumer module needs to handle the
  case where tenant is "not registered" or "swapped for an alternative
  implementation." That handler code has no use case. It's negative-value
  indirection.
- "Is the payment-gateway provider a plugin?" — Functionally yes, but the
  fleet already calls these "provider adapters" with `ProviderDescriptor`
  registered in `IProviderRegistry`. The vocabulary differs from
  flight-deck's "plugin manifest" but the substrate concept is the same:
  swappable backend per categorical role. Don't unify the vocabulary
  unless the substrate genuinely unifies.
- "Is the TTS engine a plugin?" — Yes, full plugin-manifest, with per-host
  install/launch/health probe lifecycle. Flight-deck got this right.

The distinguishing question is: **does the consumer code need to handle
the "swapped" case?** If yes → plugin/provider pattern. If no → just a
concrete dependency. About 75% of sunfish capabilities are NO; about 15% are
YES via the existing provider-registry; the other ~10% (auth, storage)
straddle.

For flight-deck the split inverts: most editorial-production capabilities
ARE swappable (different TTS quality tiers, different LLM vendors,
different image generators), so plugin-everywhere is closer to right —
but even there, `blocks-manuscript` / `blocks-render-queue` are concrete
domain blocks, not plugins.

### Q4. Are needed slots registered that are resolved by a cluster?

**Yes, but with a critical clarification: "cluster" should mean *bundle*
(ADR 0007 `BusinessCaseBundleManifest`), not a registry-cluster like a
Kubernetes term might imply.** The resolution mechanism is:

1. **App bundle declares its capability needs** (via `BusinessCaseBundleManifest`).
2. **A resolver (`IBundleResolver` — does not yet exist; sketched in
   recommendation §6.1)** walks the manifest, resolves tier 1 via DI
   extension methods, tier 2 via `IProviderRegistry.GetByCategory`, and
   tier 3 via the plugin registry's `getInstalledPlugins(capability)`.
3. **A startup bootstrap check (extension to `IMinimumSpecResolver` per ADR
   0063)** runs after resolution and fails fast if any `Required`-policy
   slot has no provider registered. `Recommended` and `Informational`
   policies generate warnings and continue.
4. **Per-tier resolution mechanisms stay distinct.** Don't unify them
   under a single "slot resolver" — each tier has different ergonomics,
   different lifecycle, different swap cadence. A unified resolver would
   bury the distinctions that make each tier appropriate.

The "cluster" in CIC's framing maps to the **bundle** primitive — the
declarative unit at which capabilities are required and resolution
happens. ADR 0007 already names this; ADR 0013 already defines the
provider-registry resolution path; flight-deck already implements the
plugin-registry resolution path. The remaining work is the resolver glue +
the bootstrap check.

---

## 2. Existing-work survey — current fleet pattern inventory

Per UPF Stage 0 Check 0.1: treat existing surfaces as DATA, not obstacles.

### 2.1 flight-deck plugin-manifest pattern (working today)

Substrate at `flight-deck/packages/plugins/`:

- **`manifest-schema.json`** — JSON Schema for plugin manifests. Capability
  vocabulary is closed enumeration: `tts/fast | tts/quality | stt/fast |
  stt/quality | image | music | llm | embedding` (8 capabilities). Each
  plugin declares which capabilities it fills (1..N).
- **`registry.json`** — index of 17 plugin manifests.
- **Per-plugin manifests** under `plugins/<id>/manifest.json` declare:
  `kind` (`local` | `cloud`), `capabilities[]`, `flavor` (API-shape hint),
  `repository`, `license`, `hardware{}` (per-platform support level +
  accelerator), `defaults{port, baseUrl, model}` ("Slot config defaults
  applied when this plugin is assigned to a capability slot"),
  `authentication{}`, `install{prerequisites, steps, installDirDefault}`,
  `launch{}`, `health{path, expectedStatus}`, `endpoint{}`, `probe{}`.
- **`plugin-architecture.md`** explicitly names the model: *"The plugin
  architecture replaces 'hardcoded provider dropdown' with 'registry of
  manifests.'"*

Resolution mechanism:
- `slotAssignments: Partial<Record<CapabilityId, string>>` — slot →
  pluginId mapping persisted in `useApiConfig` state.
- When user picks Kokoro-FastAPI for `tts/fast`:
  `slotAssignments['tts/fast'] = 'kokoro-fastapi'`; slot config absorbs
  the plugin's defaults (port, baseUrl, flavor) — overwritable.
- Typed API clients (`ttsClient.ts`, `sttClient.ts`, `musicClient.ts`)
  consume the slot assignments. The client doesn't know which plugin is
  installed; it knows the configured `baseUrl` and `flavor`.

**Verdict:** This is a working plugin model with capability slots,
manifest-driven defaults, per-host hardware tiering, and graceful slot
emptiness (slot can be `null` until reassigned). It is **not generic-DI**.
It is **not service-locator**. It is **not capability-token**. It is a
specific point on the design space optimized for *third-party-extensible
inference capabilities with per-host install/launch/health lifecycle*.

### 2.2 shipyard provider-registry pattern (working today)

Substrate at `shipyard/packages/foundation-integrations/` (ADR 0013 codified
2026-04-19, ~6 months ago):

- **`IProviderRegistry`** — registers and enumerates
  `ProviderDescriptor`s. Lookup by key OR by category.
- **`ProviderDescriptor`** — reverse-DNS-style `Key`, `ProviderCategory`,
  `Name`, `Version`, `Description`, `Capabilities` (free-form string list),
  `SupportedRegions`.
- **`ProviderCategory`** (enum at `foundation-catalog/Bundles/`):
  `Billing | Payments | BankingFeed | FeatureFlags | ChannelManager |
  Messaging | Storage | IdentityProvider | Other`. **No AI/media values.**
- **`CredentialsReference`** — opaque vault-path / keychain-key reference;
  never inline secrets.
- **`IProviderHealthCheck` / `ProviderHealthStatus`** — adapters report
  health; Bridge surfaces in admin.
- **`WebhookEventEnvelope` / `IWebhookEventDispatcher`** — normalized
  inbound event pipeline.
- **`ISyncCursorStore` / `SyncCursor`** — resumable inbound sync.
- **Sub-domain folders:** `Captcha/`, `Messaging/`, `Payments/`, `Signatures/`.
- **Concrete adapter packages:** `providers-recaptcha`,
  `providers-mesh-headscale` (live); `providers-stripe`, `providers-square`,
  `providers-postmark`, `providers-sendgrid`, `providers-twilio`, etc.
  (queued per ADR 0051 / 0052).

Resolution mechanism:
- Host's `Program.cs` calls `services.AddSunfishFoundation()` +
  per-provider `services.AddStripePayments()` /
  `services.AddPostmarkMessaging()` / etc.
- Each `Add*` registers a `ProviderDescriptor` with the registry and the
  concrete adapter type with the DI container.
- Consumer modules (`blocks-billing`, `blocks-messaging`, etc.) inject the
  abstract interface (`IPaymentGateway`, `IMessagingGateway`); DI hands
  back the registered adapter.
- **Provider-neutrality discipline:** Roslyn analyzer
  (`packages/analyzers/provider-neutrality/`) enforces that domain modules
  never `using Stripe;` etc. Adapters translate; domain stays clean.

**Verdict:** This is a working provider-registry model with category
routing, secrets-by-reference, webhook normalization, sync-cursor support,
and Roslyn-enforced neutrality. It is **not a plugin manifest** (no
install/launch/health lifecycle; adapters are .NET packages loaded at
process start, not subprocesses managed by a manifest-driven runner). It
is a specific point optimized for *vendor-swappable bounded-category SaaS
integrations*.

### 2.3 shipyard mission-space capability negotiation (working today)

Substrate at `shipyard/packages/foundation-mission-space/` (ADRs 0062 +
0063 codified 2026-04-30, ~5 weeks ago):

- **ADR 0062** ships `MissionEnvelope` (current capability profile across
  10 dimensions), `IMissionEnvelopeProvider` (central coordinator),
  `IFeatureGate<TFeature>` (per-feature gate consumer), `ProbeStatus`,
  9 `AuditEventType` constants for probe + verdict telemetry. The 10
  dimensions: `Hardware | User | Regulatory | Runtime | FormFactor |
  Edition | Network | TrustAnchor | SyncState | VersionVector`.
  **Inspired by DirectX Feature Levels.**
- **ADR 0063** ships `MinimumSpec` (per-feature/bundle declaration of
  required capability levels across the 10 dimensions),
  `MinimumSpecDimension`, `SpecPolicy { Required, Recommended,
  Informational }`, `SystemRequirementsResult`, `IMinimumSpecResolver`,
  `ISystemRequirementsRenderer`. Steam-style System Requirements page.

Resolution mechanism:
- `IMinimumSpecResolver.EvaluateAsync(bundle)` walks the bundle's
  `MinimumSpec`, queries `IMissionEnvelopeProvider` for current envelope,
  produces `SystemRequirementsResult { overall: Pass | WarnOnly | Block,
  dimensions: [...] }`.
- Anchor's `AnchorMauiSystemRequirementsRenderer` renders the result
  pre-install (full page) and post-install (toast / banner per ADR 0036).
- Re-evaluation fires on hardware change, edition change, network change,
  etc. (per dimension probe registration).

**Verdict:** This is a working capability-negotiation model based on
DirectX Feature Levels. It is **adjacent** to slot bootstrap but does
*not* currently know about provider-slot satisfaction — its 10 dimensions
are envelope-dimensional (CPU, RAM, OS, network, edition, etc.), not
provider-presence-dimensional. **An 11th dimension or a separate
composition point is needed for "this app needs a TTS provider configured"
bootstrap-check semantics.** This is the gap the prior 7-gap audit
flagged (Gap 5).

### 2.4 shipyard contracts surface (TypeScript)

`shipyard/packages/contracts/src/integrations.ts`:
- `IntegrationCategory` (6 values; mirrors `ProviderCategory` C# enum
  partially: `Payments | TransactionalEmail | MarketingEmail | Messaging |
  MeshVpn | Captcha`).
- `IntegrationProviderSchema { providerId, displayName, category,
  credentialFields, helpText, documentationUrl }` — this is the
  `ProviderManifest` shape from the consumer side.
- `ActiveProviderSnapshot { providerId, activatedAt }`.
- `IntegrationValidationResult { status, validatedAt, errorCode, errorMessage }`.
- `IntegrationAtlasView { activeByCategory, statusByCategory,
  emailRouting }`.
- `ReactIntegrationAtlasProvider` interface with `getSchemas()`,
  `getAtlasView()`, `issueProviderChange()`,
  `issueSensitiveCredential()`, `validateProvider()`.

**Verdict:** This is the React-side control plane for switching/validating
providers (Atlas Integration-Config UI per ADR 0067). It plugs into tier 2
of the proposed three-tier model.

### 2.5 sunfish capabilities (current use case)

Sunfish (`sunfish/src/Sunfish.Anchor.csproj`) consumes from shipyard:
- **Domain blocks (concrete, no swap):** `blocks-crew-comms`,
  `blocks-properties`, `blocks-property-equipment`, `blocks-maintenance`,
  `blocks-leases`, `blocks-inspections`, `blocks-integrations`,
  `blocks-ships-office`, `blocks-engine-room`, `blocks-quarterdeck`,
  `blocks-sick-bay`.
- **Foundation packages (concrete, no swap):**
  `foundation-identity-atlas`, `foundation-localfirst`,
  `foundation-recovery`, `foundation-mission-space`, `foundation-catalog`.
- **Kernel packages (concrete, no swap):** `kernel-crdt`, `kernel-runtime`,
  `kernel-security`, `kernel-sync`.
- **UI adapters (concrete, no swap):** `ui-adapters-blazor`,
  `ui-adapters-blazor/Providers/Bootstrap`.
- **Provider adapters (swap-eligible):** `providers-mesh-headscale`,
  `providers-recaptcha`.

Capability inventory (from CIC's brief + repo survey):
| Capability | Tier | Notes |
|---|---|---|
| Tenant entity | 1 (concrete) | Load-bearing; every block composes against it |
| Property entity | 1 (concrete) | Core domain primitive |
| Lease entity | 1 (concrete) | Core domain primitive |
| Invoice / payments tracking | 1 (concrete) | Domain logic; uses tier-2 Payments adapter |
| Audit trail | 1 (concrete) | `kernel-audit`; never swapped |
| Payments gateway | 2 (provider) | Stripe / Square / Adyen swap-eligible |
| Transactional email | 2 (provider) | Postmark / SendGrid / SES |
| Blob storage | 2 (provider) | S3 / Azure Blob / filesystem |
| Identity provider | 2 (provider) | Per-tenant OIDC / SAML / local |
| Captcha | 2 (provider) | reCAPTCHA / Turnstile / none |
| Mesh VPN | 2 (provider) | Headscale / Tailscale / Netbird |
| Report rendering | 1 (concrete) | Domain logic |

**Estimated split:** ~80% tier 1 (concrete), ~20% tier 2 (provider),
**0% tier 3 (plugin)** for MVP scope. Tier 3 might emerge later if sunfish
adds AI-assisted maintenance triage / OCR / lease-document summarization,
but that's post-MVP.

### 2.6 flight-deck capabilities (second use case)

Capability inventory (from `flight-deck/apps/web/src/features/` + plugins):
| Capability | Tier | Notes |
|---|---|---|
| Manuscript / chapter editing | 1 (concrete) | Domain block |
| Render queue | 1 (concrete) | Domain block |
| Voice templates | 1 (concrete) | Domain block |
| Annotations / review-sessions | 1 (concrete) | Domain block |
| Telemetry | 1 (concrete) | Foundation |
| Blob storage (audio assets) | 2 (provider) | S3 / B2 / filesystem |
| Identity provider | 2 (provider) | If hosted; minimal if local-first |
| `tts/fast` (low-latency TTS) | 3 (plugin) | Kokoro / Piper / edge-tts |
| `tts/quality` (high-fidelity TTS) | 3 (plugin) | Higgs / ElevenLabs / OpenAI |
| `stt/fast` (quick STT) | 3 (plugin) | whisper.cpp / Faster-Whisper-small |
| `stt/quality` (high-fidelity STT) | 3 (plugin) | Faster-Whisper-large / WhisperX |
| `image` (image generation) | 3 (plugin) | ComfyUI / SDNext / DALL·E / FLUX / Imagen |
| `music` (music generation) | 3 (plugin) | MusicGen / Higgs music |
| `llm` (text gen) | 3 (plugin) | Claude / GPT / Gemini |
| `embedding` | 3 (plugin) | Reserved (v2 per current plugin-architecture.md) |

**Estimated split:** ~40% tier 1 (concrete), ~10% tier 2 (provider — minimal
because local-first editorial doesn't need much vendor infrastructure),
~50% tier 3 (plugin). This is the inversion from sunfish.

### 2.7 ADRs in play

| ADR | Status | Substrate it codifies |
|---|---|---|
| 0007 | Accepted (2026-04-19) | `BusinessCaseBundleManifest`, `ProviderRequirement`, `ProviderCategory` — bundle is the declarative unit. **This is the "cluster" CIC's Q4 references.** |
| 0009 | Accepted | `Sunfish.Foundation.FeatureManagement` — feature-flag provider seam (model for provider-neutrality) |
| 0013 | Accepted (2026-04-19) | `IProviderRegistry`, `ProviderDescriptor`, `CredentialsReference`, `IProviderHealthCheck`, webhook + sync substrate. Provider-neutrality policy. |
| 0023 | Accepted (2026-04-22) | Per-slot class methods on dialog provider interfaces — "slot" in the UI-styling sense |
| 0062 | Accepted (2026-04-30, post-A1) | `MissionEnvelope`, `IFeatureGate<T>` — DirectX-Feature-Levels capability negotiation |
| 0063 | Proposed (2026-04-30) | `MinimumSpec`, `SpecPolicy`, `IMinimumSpecResolver` — install-UX layer; **per-bundle requirement declaration** |
| 0067 | Accepted (2026-05-05) | `IIntegrationAtlasProvider`, control-plane UI for tier-2 provider switching |
| 0091 | (in-flight) | ITenantContext consumption qualification — orthogonal but informs tier-1 |

**Composite observation:** **The fleet already has the substrate primitives
for all three tiers of the proposed architecture.** What is missing is:
(a) the architectural framing that names the three tiers as distinct, (b)
the resolver glue (`IBundleResolver` or equivalent), (c) the AI/media
vocabulary extension to tier 2 OR the promotion of flight-deck's tier-3
pattern into shipyard substrate, (d) the slot-presence bootstrap dimension
on `IMinimumSpecResolver`.

---

## 3. Six candidate patterns compared

Six architectural patterns evaluated against ten dimensions. Cell values:
**A** = strong fit, **B** = workable, **C** = weak, **D** = anti-pattern
for this dimension.

### 3.1 Pattern definitions

(a) **Slot registry (CIC's framing)** — A kernel-defined set of typed
capability slots (`TtsSlot`, `StorageSlot`, `AuthSlot`, ...). Providers
register against typed slots via DI manifests. Kernel asserts essential
slots at startup; non-essential slots are extension points.

(b) **Plugin model ("everything is a plugin")** — Every capability is a
plugin with a manifest declaring tags + interface + dependencies. Runtime
loads plugins matching the app's declared capability needs. Flight-deck's
existing pattern, generalized to all capabilities.

(c) **Cluster resolution** — Capability needs declared at the app/bundle
level. Clusters of substrate packages register their capabilities. Runtime
resolves the cluster graph to bind app ↔ cluster ↔ implementation. Shape
closest to ADR 0007's `BusinessCaseBundleManifest` + `requiredModules`.

(d) **Pure DI** — Just .NET DI + interface contracts. Per-cluster
extension methods (`AddSunfishProperty()`, `AddFlightDeckMedia()`). No
separate registry layer. Adapter selection at host startup via
`Program.cs` composition root.

(e) **Capability-based authorization model** — Analogous to Genode /
Fuchsia / Pony / E language: each provider holds typed capability tokens;
consumers request capabilities and the substrate routes by token-bearer
relationship. Strong sandboxing; high ergonomic cost.

(f) **Service bus / event-driven** — Capabilities published as events on
a bus; consumers subscribe; no compile-time binding; pure runtime
contract enforcement. ROS / Erlang-OTP-supervisor style.

### 3.2 Feature matrix (ten dimensions, six patterns)

| Dimension | (a) Slot registry | (b) Plugin everywhere | (c) Cluster resolution | (d) Pure DI | (e) Capability-token | (f) Service bus |
|---|---|---|---|---|---|---|
| **D1 Ergonomics (writing 50th capability)** | B — slot must be predeclared | A — drop a manifest + adapter | A — drop a block + register | A — `AddX()` extension | C — token plumbing per call | B — pub/sub boilerplate |
| **D2 Type safety (compile-time errors)** | A — typed `ISlot<T>` | C — manifest is JSON; runtime errors | B — DI checks at startup | A — full compile-time | A — typed capability handles | D — bus payloads are dynamic |
| **D3 Runtime swapability (no rebuild)** | A — reassign slot | A — swap manifest assignment | C — requires bundle reload | C — requires host restart | A — token transfer | A — subscribe new handler |
| **D4 Testability** | A — mock per slot | B — test harness loads test plugins | A — swap blocks per test | A — DI scope per test | A — token-isolated tests | C — bus state global; harder to isolate |
| **D5 Discovery (what providers exist?)** | A — registry enumeration | A — registry enumeration | A — bundle manifest | C — only by inspecting `Program.cs` | B — token-graph traversal | C — bus contents undocumented |
| **D6 Multi-app coordination (sunfish + flight-deck)** | B — both need same slot vocab | C — divergent manifest dialects | A — each app declares its bundle | A — each app composes its DI | A — each app issues its tokens | B — bus is one shared substrate |
| **D7 Substrate decoupling (no cross-app leak)** | C — slot definitions are shared | B — manifest schema shared but local plugins | A — clusters are autonomous | A — DI graphs are autonomous | A — token boundaries are explicit | C — bus is a shared global |
| **D8 IDE tooling (autocomplete, refactor)** | A — typed slot defs | C — manifest changes don't ripple to IDE | B — interfaces refactor cleanly | A — full IDE support | B — capability types refactor cleanly | D — bus payloads don't refactor |
| **D9 Observability (debugging unknown failure)** | B — slot registry diff | B — manifest validity check | A — bundle manifest diff | A — DI graph inspection | C — token-graph hard to introspect | C — bus-flight-recorder needed |
| **D10 Kernel-bootstrapping correctness (fail fast at startup)** | A — assert essential slots | B — manifest validation at install | A — bundle resolver checks at startup | B — runtime resolution errors per request | A — token absence is compile-time-ish | D — bus consumers come up async; bootstrap race conditions |

### 3.3 Per-pattern strengths and weaknesses (compressed)

(a) **Slot registry** — Strong fit for *closed enumeration of typed,
swappable infrastructure roles* (TTS slot, storage slot, payments slot).
Weak when the slot vocabulary needs to grow without a kernel change. CIC's
intuition that "slots are predeclared" is correct AND limiting.

(b) **Plugin everywhere** — Strong fit for *third-party-extensible
runtime-discoverable capabilities*. Weak for domain logic (no use case for
swapping the tenant entity at runtime). Strong fit for AI inference
(flight-deck's existing reality). Type-safety lost at the manifest
boundary.

(c) **Cluster resolution** — Strong fit for *declarative app composition
where the cluster IS the unit of release*. ADR 0007's
`BusinessCaseBundleManifest` is this pattern. Cluster boundaries are
release boundaries: a `blocks-properties` cluster ships with its own DI
extension, tests, docs. Weak at runtime swap (cluster reload is heavy).

(d) **Pure DI** — Strong fit for *concrete domain composition with stable
swap surface known at host-startup time*. Strong testability. Weak when
the swap needs to happen at runtime per-user-action (e.g., "change my TTS
provider in Settings").

(e) **Capability-token (Pony, Genode, Fuchsia, E language)** — Strong
*formal* properties: object-capability security, no ambient authority,
explicit delegation. Weak ergonomics — every capability use needs explicit
token passing. **Probably wrong for sunfish + flight-deck UNLESS we have a
hard security requirement that justifies the cost.** Could be revisited
for kernel-tier surfaces (signed event emission, audit trail token
restriction) but not for the slot/plugin coordination question.

(f) **Service bus / event-driven** — Strong fit for *async, decoupled,
multi-producer/multi-consumer event flows*. The fleet's `WebhookEventEnvelope`
+ `IWebhookEventDispatcher` (ADR 0013) is exactly this for one specific
domain (provider-initiated webhooks). Generalizing it to all capability
coordination would lose type safety and observability without compensating
gain. **The right call is to keep service-bus for inbound provider events
and not extend it to general slot coordination.**

### 3.4 Dimension-by-dimension synthesis

- **D1 (ergonomics) + D2 (type safety):** (b) trades type safety for
  drop-in extensibility; (a) and (d) keep type safety with closed
  vocabulary. For inference plugins, the trade is worth it (manifests are
  human-readable, easy to author). For domain blocks, it's not.
- **D3 (runtime swap):** (a), (b), (e), (f) support runtime swap; (c),
  (d) require restart. Sunfish's tier-2 providers swap at runtime (Atlas
  UI lets admin change Stripe → Adyen); flight-deck's tier-3 plugins swap
  at runtime (Settings → Plugins lets user pick a different TTS engine).
- **D6 (multi-app):** (c) and (d) and (e) all decouple cleanly; (a)
  requires shared slot vocabulary across apps. CIC's framing assumes
  shared vocabulary but didn't establish that's wanted.
- **D10 (bootstrap):** Critical for sunfish (a tenant management app
  cannot boot without a payments adapter when invoicing is enabled). The
  bundle-manifest + `IMinimumSpecResolver` substrate already supports
  this; needs the 11th dimension for slot presence.

### 3.5 The verdict (compressed)

**No single pattern is right.** The fleet has accidentally already
implemented a layered architecture: (c) bundle-as-cluster for app
composition; (d) pure DI within a cluster for domain blocks; tier-2 of (a)
with provider-registry vocabulary; tier-3 of (b) for inference plugins.
The work is to *recognize this layered architecture, name the layers,
codify the resolver glue*, NOT to pick a single winner.

---

## 4. AHA findings — surprises, reframings, bifurcation candidates

Per UPF Stage 0 Check 0.9. These are the discoveries that change the
shape of the answer.

### 4.1 AHA #1 — "Slotting" conflates three distinct problems

The word "slot" appears in:
- ADR 0023 (UI dialog provider per-slot class methods) — UI-styling slots
- flight-deck plugin-architecture.md (`slotAssignments: capability →
  pluginId`) — runtime inference assignment
- CIC's framing ("slotting architecture standardizes required feature
  providers") — generic coordination metaphor

These are three different patterns with the same word. Reusing "slot" as
a fleet-wide architectural term will create exactly the
vocabulary-divergence problem the prior 7-gap audit (Gap 1) flagged
between flight-deck "plugins/capabilities" and shipyard
"providers/categories."

**Recommendation:** Don't make "slotting" a fleet-wide term. Use **"tier"**
for the three-level architecture (tier-1 / tier-2 / tier-3). Reserve "slot"
for UI structural divisions (ADR 0023) and for flight-deck's
`capability → plugin` runtime mapping (existing usage).

### 4.2 AHA #2 — Sunfish doesn't need slotting at all (in MVP)

CIC's framing presumed "support the first use cases of property management
(sunfish) and media publishing (flight-deck)" wants the same architecture.
The capability inventories show:

- **Sunfish MVP:** 0 tier-3 capabilities. The "swappable infrastructure
  providers" sunfish needs (payments, email, storage, captcha, mesh,
  identity) are tier-2 — already addressed by `IProviderRegistry`. There
  is no use case in MVP for plugin-manifest semantics.
- **Flight-deck:** Most-of-app tier-3 capabilities. The plugin pattern is
  load-bearing for the editorial workflow.

**The bifurcated recommendation isn't a compromise — it's the actual
shape.** Sunfish needs tier-1 + tier-2. Flight-deck needs tier-1 + tier-2
+ tier-3. The shared substrate is tier-1 + tier-2; tier-3 is
flight-deck-specific UNTIL a future sunfish use case (AI lease-document
summarization, OCR triage) materializes.

This is a critical reframing of CIC's question: "best way to coordinate"
isn't ONE way. Sunfish gets the simpler two-tier path; flight-deck adds
the third tier when needed. The fleet's substrate supports both.

### 4.3 AHA #3 — The bundle IS the cluster

CIC's Q4 asks: "Are needed slots registered that are resolved by a
cluster?" The natural interpretation imports the Kubernetes / distributed-
systems sense of "cluster" — a runtime grouping of services that resolve
shared state.

But the fleet already has a more useful primitive: the
**`BusinessCaseBundleManifest`** (ADR 0007). A bundle is:
- A declarative unit composed of `requiredModules` (tier 1)
- With `providerRequirements` (tier 2)
- And (per ADR 0063 amendment) `requirements: MinimumSpec` for capability
  prerequisites

Adding `capabilitySlots: CapabilitySlotRequirement[]` (tier 3) to the
bundle manifest gives us the missing piece. **The bundle IS the cluster
in CIC's framing.** It's already declarative; it's already resolvable;
it's already audited (ADR 0067's audit emission for provider changes);
it's already user-facing (System Requirements page renders per-bundle).

This is a major simplification. We don't need a new "cluster" concept —
we extend `BusinessCaseBundleManifest`.

### 4.4 AHA #4 — Flight-deck has the same `manifest-schema.json` pattern that NPM and Cargo and Maven and Helm all have

The flight-deck plugin model is a vendor-installable-package pattern,
which is well-trodden. Comparable patterns:
- NPM `package.json` + npmjs registry → dependency resolution + install scripts
- Cargo `Cargo.toml` + crates.io → ditto for Rust
- Helm `Chart.yaml` + chart repos → ditto for Kubernetes deployments
- VSCode `package.json` + `contributes.*` → extension contribution points
- Eclipse OSGi `MANIFEST.MF` + service registry → component registration
- MEF (Microsoft Managed Extensibility Framework) `[Export]/[Import]` →
  attribute-driven plugin composition

What's specific to flight-deck (and what the fleet should preserve when
promoting to shipyard substrate):
1. **Capability tags as the discovery vocabulary** (`tts/fast`, `image`,
   `llm`) — closed enumeration, kernel-controlled. NPM and Cargo don't
   have this; VSCode and Eclipse OSGi do (contribution points).
2. **Per-platform hardware grading** — flight-deck's `hardware.macos-arm64
   = {support: "good", accel: "mps"}` is unusual. Most ecosystems leave
   hardware match to the user. This is right for fleet local-first
   inference posture.
3. **Lifecycle (install / launch / health)** — flight-deck plugins are
   subprocesses. NPM packages are imported libraries. The right comparison
   is Helm charts (Kubernetes deployments) or VSCode extensions
   (process-isolated UI extensions).

**Recommendation:** When promoting flight-deck's pattern, name what's
preserved vs. what's borrowed from the industry analogs. The capability-tag
vocabulary is the unique-to-fleet decision worth codifying carefully.

### 4.5 AHA #5 — There's a "tier 1.5" hiding in plain sight

The tier model as initially drawn:
- Tier 1: concrete domain blocks (no swap)
- Tier 2: provider-registry (vendor swap; bounded category)
- Tier 3: plugin-manifest (third-party-extensible; install lifecycle)

But there's a fourth category that doesn't quite fit:
- Tier 1.5: **single-implementation-per-host infrastructure with
  process-managed lifecycle** — like `kernel-sync` (gossip daemon),
  `kernel-runtime` (node host), `foundation-localfirst` (encrypted store).
  These are concrete (one implementation), but their lifecycle is managed
  (start/stop/restart) and their state is durable.

These are currently bundled with tier 1 (concrete domain) but they have
plugin-like *lifecycle* without plugin-like *swap*. They don't need a
manifest; they DO need a hosted-service registry (`IHostedService` per
.NET conventions, which is already in place).

**Recommendation:** Don't introduce tier 1.5 explicitly. Document that the
existing `IHostedService` pattern handles "managed-lifecycle concrete
infrastructure" and that this is orthogonal to the three-tier capability
model. Avoids confusion when contributors ask "where does
`kernel-sync` fit?"

### 4.6 AHA #6 — The "control plane" problem is already solved by ADR 0067

CIC's framing mentioned "control surface contract" for managing
providers. ADR 0067 (Atlas Integration-Config UI Surface) already
defines `IIntegrationAtlasProvider` with `getSchemas()`,
`issueProviderChange()`, `validateProvider()`, etc. This is the control
plane for tier-2 providers. **Extending it to tier-3 plugins** is the
natural play — same React-side interface, just emitting `PluginManifest`
instead of `IntegrationProviderSchema`.

Tier 1 doesn't need a control plane (it's not swappable). Tier 2 has it
(ADR 0067). Tier 3 needs to extend it (probably as
`PluginAtlasProvider` or similar — naming TBD in ADR).

### 4.7 AHA #7 — There IS a "single best way" question hiding underneath

CIC asked "what's the best way to coordinate background services?" The
honest answer is "it depends on the tier, and the fleet already has the
substrate" — which is correct but unsatisfying.

The implicit question CIC may be asking is: **"As an app author, when I
say 'I need a TTS capability', what's the user/dev experience flow?"**

The answer to that question IS a single concrete flow:
1. App declares `capabilitySlots: ['tts/quality']` in bundle manifest
2. At install / first-run, System Requirements page shows: "TTS quality
   provider: NONE INSTALLED — go to Settings → Plugins"
3. User picks a plugin, plugin's manifest specifies install steps,
   Tauri/CLI executes them
4. Plugin running; health probe green; slot assignment binds
5. Bundle manifest's `Required` policy now satisfied; app boots clean

Same flow whether it's tier 2 (payments adapter) or tier 3 (TTS plugin).
The user/dev experience is unified at the bundle layer; the implementation
mechanism (DI extension method vs. JSON manifest + subprocess install)
differs underneath.

**Recommendation:** When authoring the ADR, lead with the user-flow
narrative, not the mechanism. The mechanism is the implementation
detail; the bundle-declared-capability is the user contract.

---

## 5. Use-case specific recommendation

### 5.1 Sunfish (property management) — two-tier architecture

Use tiers 1 + 2. No tier 3 in MVP scope.

**Concrete recommendation:**
- Keep all domain blocks (`blocks-properties`, `blocks-leases`,
  `blocks-invoices`, etc.) as tier-1 concrete DI registrations.
  `services.AddSunfishProperty()`, etc.
- Use `IProviderRegistry` (already in place) for tier-2 vendor adapters:
  Stripe / Square for Payments; Postmark / SendGrid for TransactionalEmail;
  S3 / Azure for Storage; reCAPTCHA / Turnstile for Captcha; per-tenant
  OIDC / SAML for IdentityProvider; Headscale / Tailscale for MeshVpn.
- Declare provider requirements via `BusinessCaseBundleManifest`:

  ```jsonc
  {
    "key": "sunfish.bundles.property-management",
    "version": "0.1.0",
    "requiredModules": ["blocks-properties", "blocks-leases", "blocks-invoices", ...],
    "providerRequirements": [
      { "category": "Payments", "required": true, "purpose": "Tenant rent collection" },
      { "category": "TransactionalEmail", "required": true, "purpose": "Invoice delivery" },
      { "category": "Storage", "required": true, "purpose": "Document upload" },
      { "category": "Captcha", "required": false, "purpose": "Signup abuse" },
      { "category": "IdentityProvider", "required": false, "purpose": "Tenant SSO" }
    ],
    "requirements": { /* MinimumSpec for hardware/OS — ADR 0063 substrate */ }
  }
  ```

- **No plugin manifest layer needed.** Sunfish ships with concrete .NET
  adapter packages (`providers-stripe`, `providers-postmark`, etc.) that
  the host's `Program.cs` registers. No subprocess install lifecycle.
- **Bootstrap check:** Extend `IMinimumSpecResolver.EvaluateAsync` to
  also check `BusinessCaseBundleManifest.providerRequirements` —
  `Required = true` and no provider registered → `OverallVerdict.Block`.

**This is essentially what sunfish already has.** The "new" work is:
(a) the bootstrap check tying provider-registry presence to mission-space
verdict, (b) extending `IntegrationCategory` if any new tier-2 categories
emerge (FraudScoring? CreditCheck?), (c) implementing the remaining tier-2
adapter packages (Stripe / Postmark / S3) per their respective ADRs.

### 5.2 Flight-deck (media publishing) — three-tier architecture

Use tiers 1 + 2 + 3.

**Concrete recommendation:**
- Tier 1 — domain blocks: `blocks-manuscript`, `blocks-render-queue`,
  `blocks-voice-templates`, `blocks-annotations`, `blocks-review-sessions`,
  `blocks-telemetry`.
- Tier 2 — provider adapters: minimal. `Storage` (S3 / B2 / filesystem)
  for audio asset hosting. `IdentityProvider` if/when flight-deck adds
  multi-user hosted mode.
- Tier 3 — plugin manifests: existing pattern at
  `flight-deck/packages/plugins/manifest-schema.json`. Capability
  vocabulary: `tts/fast | tts/quality | stt/fast | stt/quality | image |
  music | llm | embedding` (plus future ones if/when added).
- App bundle manifest:

  ```jsonc
  {
    "key": "flight-deck.bundles.editorial-production",
    "version": "0.1.0",
    "requiredModules": ["blocks-manuscript", "blocks-render-queue", ...],
    "providerRequirements": [
      { "category": "Storage", "required": true, "purpose": "Audio asset persistence" }
    ],
    "capabilitySlots": [
      { "capability": "tts/quality", "policy": "Required", "purpose": "Audiobook generation" },
      { "capability": "tts/fast", "policy": "Recommended", "purpose": "Draft-tier voice review" },
      { "capability": "stt/quality", "policy": "Recommended", "purpose": "Transcription QC" },
      { "capability": "llm", "policy": "Required", "purpose": "Editorial agent assist" },
      { "capability": "image", "policy": "Informational", "purpose": "Cover art generation" },
      { "capability": "music", "policy": "Informational", "purpose": "Background scoring" }
    ],
    "requirements": { /* MinimumSpec for GPU presence per dimension */ }
  }
  ```

- **Bootstrap check:** Same `IMinimumSpecResolver.EvaluateAsync`,
  extended to also check `capabilitySlots` — `Required` and no plugin
  installed/assigned → `OverallVerdict.Block`; `Recommended` and unmet
  → `WarnOnly`; `Informational` → render in System Requirements but never
  block.

### 5.3 Same or different?

**Same architecture; different tier subset used.** Both apps:
- Declare needs via `BusinessCaseBundleManifest` (ADR 0007)
- Resolve tier 1 via DI extension methods (per-cluster `Add*` extensions)
- Resolve tier 2 via `IProviderRegistry.GetByCategory()` (ADR 0013)
- Use `IMinimumSpecResolver` for startup checks (ADR 0063)
- Use `IIntegrationAtlasProvider` for tier-2 control-plane UI (ADR 0067)

Flight-deck additionally:
- Resolves tier 3 via plugin-manifest registry (existing flight-deck
  pattern, promoted to shipyard substrate)
- Uses an extended control-plane UI (`PluginAtlasProvider`) for tier-3
  plugin management

**The architecture is shared.** Each app uses the tiers it needs.

---

## 6. Stage-1 sketch — recommendation + halt conditions

If CIC adopts the three-tier architecture, the work plan looks like this.
Per UPF Stage 1 conventions (binary gates, measurable success, FAILED
conditions).

### 6.1 Phase plan (3 phases)

**Phase 1 — Coordination + reconciliation ADR (1-2 weeks; council-reviewed)**

- *1.1* — File coordination beacon to flight-deck maintainer: "Promote
  `packages/plugins/manifest-schema.json` into shipyard substrate as the
  fleet's tier-3 capability-slot pattern. Confirm naming, ownership,
  evolution policy." Outcome: GO / NO-GO / DEFER.
  - **Gate:** Coordination beacon filed + acknowledged. **Failed if:**
    no acknowledgement within 5 business days → escalate to CIC.
- *1.2* — Author ADR-0095 (next free ADR number; verify at PR time):
  "Three-tier capability architecture — bundle-driven composition of
  concrete blocks, registry providers, and plugin manifests." Composes
  ADRs 0007 / 0013 / 0023 / 0062 / 0063 / 0067. Council posture:
  security-engineering + .NET-architect + WCAG.
  - **Gate:** ADR merged via Stage 1.5 council process (per ADR 0093
    protocol). **Failed if:** council BLOCK-tier finding not resolvable
    within 2 fix-pass cycles → escalate to CIC.
- *1.3* — ADR-0095 must make these decisions explicitly:
  - **D1** Canonical tier names. Recommendation: `domain-block` (tier 1),
    `category-provider` (tier 2), `capability-plugin` (tier 3). Avoids
    the overloaded "slot" word.
  - **D2** Vocabulary reconciliation: keep flight-deck's capability tags
    (`tts/quality`) as the canonical tier-3 vocabulary; keep shipyard's
    `ProviderCategory` C# enum as the canonical tier-2 vocabulary. Don't
    unify — they serve different swap surfaces.
  - **D3** Bundle-manifest extension: add `capabilitySlots:
    CapabilitySlotRequirement[]` field to
    `BusinessCaseBundleManifest`. Amendment to ADR 0007.
  - **D4** Bootstrap-check composition: extend `IMinimumSpecResolver`
    with an 11th dimension `ProviderSlotsSatisfied` OR introduce
    `IProviderSlotResolver` composed into the same evaluation pipeline.
    Recommendation: 11th dimension (less indirection).
  - **D5** Control-plane UI: extend `IIntegrationAtlasProvider`'s
    contract to handle tier-3 plugins (probably via a sibling interface
    `IPluginAtlasProvider`). Amendment to ADR 0067.

**Phase 2 — Substrate implementation (post-ADR-0095)**

- *2.1* — Extend `BusinessCaseBundleManifest` (ADR 0007 amendment):
  `capabilitySlots: CapabilitySlotRequirement[]` with `{ capability,
  policy, purpose }`.
- *2.2* — Extend `shipyard/packages/foundation-mission-space/`:
  - 11th `MinimumSpecDimension` value: `ProviderSlotsSatisfied`
  - Evaluation logic that walks bundle's tier-2 `providerRequirements`
    + tier-3 `capabilitySlots`, calls `IProviderRegistry.GetByCategory()`
    + plugin-registry's `getInstalledFor(capability)`, aggregates into
    dimension verdict.
- *2.3* — Promote flight-deck's plugin substrate into shipyard:
  - New package `shipyard/packages/foundation-plugins/` mirroring
    flight-deck's `packages/plugins/` JSON-schema + registry pattern.
    .NET types: `PluginManifest` record + `IPluginRegistry` + the
    `IPluginInstaller` lifecycle interface.
  - flight-deck's existing JSON manifests stay where they are (or move
    under shipyard's new package — decision in ADR-0095).
  - C# adapter that loads JSON manifests into `PluginManifest` records.
- *2.4* — Extend `shipyard/packages/contracts/src/integrations.ts`:
  - Add `PluginManifest` + `PluginRegistryEntry` + `CapabilitySlot` TS
    types mirroring the C# / JSON-schema shape.
  - Extend `IntegrationAtlasView` to include `capabilitySlots` alongside
    `activeByCategory`.
- *2.5* — Extend `IIntegrationAtlasProvider` (ADR 0067 amendment) with
  `getPluginManifests()`, `assignPluginToSlot(capability, pluginId)`,
  `installPlugin(pluginId)`, `uninstallPlugin(pluginId)`. Or split into
  sibling interface `IPluginAtlasProvider` — TBD in ADR-0095.

**Phase 3 — App-side adoption (parallel post-Phase 2)**

- *3.1* — Sunfish: author `sunfish.bundles.property-management`
  manifest with tier-1 modules + tier-2 provider requirements. Wire into
  Anchor's pre-install System Requirements page (already exists per
  AnchorMauiSystemRequirementsRenderer).
- *3.2* — Flight-deck: author `flight-deck.bundles.editorial-production`
  manifest with tier-1 modules + tier-2 + tier-3 capability slots.
  Migrate current `useApiConfig.slotAssignments` to read from the bundle
  manifest at install time.
- *3.3* — Tender (Tauri menu-bar fleet control): consume the bundle
  manifests via HTTP from flight-deck's localhost endpoint OR via direct
  `file:` reference to shipyard contracts. Phase-2 ADR-0095 decides which.

### 6.2 Reference library (for future Stage 06 implementer)

- ADRs 0007 / 0013 / 0023 / 0062 / 0063 / 0067 — substrate codifications
- Prior 7-gap audit: `icm/01_discovery/research/onr-slotting-architecture-7-gap-audit.md` (shipyard PR #146 merged)
- flight-deck `packages/plugins/manifest-schema.json` — canonical
  plugin-manifest format
- flight-deck `docs/architecture/plugin-architecture.md` — design rationale
- shipyard `packages/foundation-integrations/README.md` — provider-registry
  rationale
- shipyard `packages/foundation-mission-space/` — mission-envelope substrate

### 6.3 Anti-pattern guards (per UPF Stage 2 21-anti-pattern scan)

- **AP-9 (skipping Stage 0):** This deliverable IS Stage 0 for the future
  ADR. The ADR author must NOT re-skip Stage 0; should cite this
  deliverable + the prior 7-gap audit in its Discovery section.
- **AP-10 (first idea unchallenged):** The six patterns in §3 are the
  challenge. ADR-0095 must address why each was considered and rejected
  for the wrong tier, accepted for the right tier.
- **AP-13 (confidence without evidence):** Every capability count + every
  ADR cross-reference + every dimension verdict cited in §2-§3 is
  filesystem-verifiable. Don't reassert without re-verification (ADR
  numbering, package locations, etc. drift over time).
- **AP-15 (premature precision):** Don't name `PluginManifest.someField`
  shape in ADR-0095 — name the *categories* of fields (lifecycle, auth,
  capabilities, hardware) and let Phase 2 implementation refine.
- **AP-21 (assumed facts without sources):** Web-search citations in §3
  prior-art are intentionally light (Genode, Fuchsia, OSGi, MEF) — they
  inform the design space, they do NOT constitute primary sources for
  fleet substrate decisions. Don't over-cite secondary literature.

### 6.4 Halt conditions

- **Halt-A:** If flight-deck maintainer does NOT agree to promote the
  plugin pattern → fall back to "two-tier for sunfish; flight-deck stays
  local; document the convergence path." Don't force premature promotion.
- **Halt-B:** If Phase 2 implementation discovers `IMinimumSpecResolver`
  can't cleanly support an 11th dimension → fall back to a separate
  `IProviderSlotResolver` composed into the same evaluation pipeline.
- **Halt-C:** If sunfish's MVP scope expands to include AI-assisted
  features (OCR triage, lease summarization, fraud scoring) → re-evaluate
  whether tier 3 is needed earlier than expected.
- **Halt-D:** If ADR-0095 council Stage 1.5 produces BLOCK-tier findings
  that 2 fix-pass cycles can't resolve → escalate to CIC.

---

## 7. UPF Stage 1.5 — autonomous hardening (6 adversarial perspectives)

### 7.1 Outside Observer (6-months-from-now contributor)

Onboarding question: "Where does a new capability get wired in?"

- **Recommendation strength:** Strong if ADR-0095 includes a one-page
  decision tree (where does my capability fit? tier 1, 2, or 3?). The
  three-tier shape is learnable; the gotchas are: don't make tier-1
  capabilities pluggable; don't make tier-3 capabilities pure-DI; tier-2
  vocabulary is closed (extend the enum, don't invent a category).
- **Recommendation weakness:** If the ADR doesn't include the decision
  tree, a new contributor will reach for either the
  `IProviderRegistry` (because it's the obvious pattern) OR the plugin
  manifest (because it's the visible pattern), without understanding why
  each is appropriate for its specific tier. **Required ADR section:
  "Where does X go? — decision tree."**

### 7.2 Pessimistic Risk Assessor

What goes wrong?
- **Risk-1:** Tier 2 ↔ tier 3 boundary blur. Suppose flight-deck adds
  `embedding` as a capability AND a SaaS embedding provider (Pinecone /
  Weaviate / Vespa) exists. Is that tier 2 or tier 3? Likely tier 3
  (because its lifecycle is plugin-shaped: install local Pinecone OR
  configure cloud Pinecone), but the boundary is fuzzy. **Mitigation:**
  ADR-0095 must give explicit rules for tier boundary, ideally with
  worked examples.
- **Risk-2:** Bootstrap-check explosion. Phase 2's `ProviderSlotsSatisfied`
  dimension might fire for every slot at every startup, slowing boot.
  **Mitigation:** Probe-cost class (ADR 0062 substrate) — slot-presence
  is a cheap probe (registry lookup); validation status is an expensive
  probe (HTTP roundtrip). Use the existing cost-class machinery.
- **Risk-3:** Manifest-vs-DI divergence. Tier 2 (DI) and tier 3
  (manifest) have different update cadences: DI changes need rebuilds;
  manifests can be hot-loaded. A mixed bundle could have tier-2 + tier-3
  requirements satisfied but inconsistent. **Mitigation:** Bundle version
  bump triggers full re-evaluation; document this in ADR-0095.

### 7.3 Pedantic Lawyer (compliance / licensing / data-residency)

- **License-leak risk:** Tier 3 plugins can be GPL (ComfyUI is GPL-3.0).
  Flight-deck's existing pattern handles this by process-boundary
  isolation (galley loads ComfyUI as subprocess; license boundary holds).
  **ADR-0095 must codify the process-boundary rule** for tier-3 plugins
  with copyleft licenses, alongside the existing `BannedSymbols.txt`
  pattern (ADR 0061 + ADR 0067-A1 license-acknowledgement deferred
  amendment).
- **Data-residency risk:** Tier-2 providers (especially Payments) have
  regional restrictions. `ProviderDescriptor.SupportedRegions` already
  exists. Tier-3 plugins (cloud LLMs) have similar issues (data may flow
  through US-only endpoints). **ADR-0095 must require
  `PluginManifest.dataResidency` field** when `kind: cloud`. Already
  partially covered by `pricing` + `terms` + `endpoint.baseUrl` regional
  hints; should be made explicit.
- **MFA / authentication-chain leak:** Tier-2 + tier-3 both need
  credential storage. Flight-deck's manifest already specifies OAuth /
  PKCE / MFA semantics (good). Shipyard's `CredentialsReference` is
  vault-reference (good). Reconciling them: each tier owns its own
  credential surface. **Don't force unification across tiers** —
  different lifecycle, different storage shape (vault vs. keychain).

### 7.4 Skeptical Implementer (writing the 5th, 50th, 500th capability)

- **5th capability:** Easy. Three tiers, clear placement. Probably tier
  1 or tier 2.
- **50th capability:** Manageable but two friction points emerge:
  (a) the `IntegrationCategory` enum grows long (tier 2);
  (b) capability-tag vocabulary creep (tier 3) — should `tts/instant`
  be a new tag separate from `tts/fast`? **Mitigation:** ADR-0095
  defines the **vocabulary-evolution policy** (who can add a new
  category? who can add a new capability tag? what's the review process?).
- **500th capability:** Tier 1 explosion (block sprawl) is the real risk.
  ADR-0095 doesn't address this — that's a tier-1 cluster-design
  question. Out of scope here; reference ADR-0091 ITenantContext
  qualification and the cluster-cohesion principles in the existing
  Sunfish substrate.

### 7.5 The Manager (multi-team contribution at scale)

- **Question:** Can teams contribute capabilities without bottlenecking on
  ADR review?
- **Answer per tier:**
  - Tier 1 — adding a new block requires a Stage 02 architecture review
    (current Sunfish convention). Acceptable.
  - Tier 2 — adding a new provider adapter (e.g., `providers-adyen` if
    Stripe + Square aren't enough) does NOT require ADR — `IProviderRegistry`
    is generic. ADR only required when adding a new `ProviderCategory`
    enum value. Acceptable scaling.
  - Tier 3 — adding a new plugin (e.g., a new TTS engine) does NOT
    require ADR — plugin manifests are data, not code. PR against the
    plugin registry suffices. Excellent scaling. **This is flight-deck's
    win, preserved.**
- **Multi-team friction point:** Capability-tag vocabulary evolution
  (tier 3). If team A wants `tts/streaming` and team B wants
  `tts/realtime`, there's a coordination need. ADR-0095 must specify
  the vocabulary curator (probably "fleet substrate maintainer" or
  similar; CIC's call).

### 7.6 Devil's Advocate (am I just rebranding what we have?)

- **Honest answer:** Mostly yes. The three tiers already exist:
  - Tier 1 — domain blocks, present and working
  - Tier 2 — `IProviderRegistry`, present and working
  - Tier 3 — flight-deck plugin registry, present and working
- **What's new:**
  - The architectural framing that names them as tiers
  - The `BusinessCaseBundleManifest` extension with `capabilitySlots`
  - The bootstrap-check unification via `IMinimumSpecResolver`
  - The control-plane UI unification via `IIntegrationAtlasProvider`
    extensions
  - The promotion of flight-deck's tier-3 pattern into shipyard substrate

- **Honest critique:** A skeptical CIC could say "this is renaming."
  The counter-argument: **the renaming is the work**. Without the
  three-tier framing, the next architectural decision will fall into the
  same vocabulary-divergence trap the prior 7-gap audit flagged. With
  the framing, future capability decisions are mechanical placements
  into the existing tier.

  If CIC's reading is "this just makes explicit what we already do" —
  **that's correct, and that's the value**. Architecture is mostly
  making implicit patterns explicit so they can be defended, taught,
  and extended.

---

## 8. UPF Stage 2 — meta-validation

Per UPF Stage 2 (7 checks + 21 anti-pattern scan).

### 8.1 Delegation strategy clarity

This deliverable identifies a Phase 1 ADR (Admiral / XO authoring with
council), Phase 2 substrate implementation (Engineer / FED), Phase 3 app
adoption (sunfish + flight-deck). Delegation boundaries are explicit. ✓

### 8.2 Research needs identification

Pre-flight: coordination beacon to flight-deck maintainer. ✓
In-flight: vocabulary-evolution policy authorship. ✓
Post-flight: per-Phase-2 PR Stage 06 hand-offs. ✓

### 8.3 Review gate placement

- Phase 1: Stage 1.5 council review (security-engineering + .NET-architect
  + WCAG) per ADR 0093 protocol. ✓
- Phase 2: Per-PR Stage 06 review (substrate changes); per-amendment
  ADR-0007 / ADR-0067 council. ✓
- Phase 3: App-side PR review (lighter; no new ADR needed). ✓

### 8.4 Anti-pattern scan (21 patterns)

| # | Anti-pattern | Hit? | Mitigation |
|---|---|---|---|
| AP-1 | Unvalidated assumptions | NO | Every assumption (capability counts, ADR references, package locations) is filesystem-verified per §2 |
| AP-2 | Vague phases | NO | 3 phases with named deliverables + gates per §6.1 |
| AP-3 | Vague success criteria | NO | Binary gates per phase; FAILED conditions per §6.4 |
| AP-4 | No rollback | NO | Halt-A through Halt-D per §6.4 |
| AP-5 | Plan ending at deploy | NO | Phase 3 explicitly app-side adoption; Phase 1 deliverable is ADR, not deploy |
| AP-6 | Missing Resume Protocol | LOW | This is research; resume = re-read this doc + prior 7-gap audit |
| AP-7 | Delegation without contracts | NO | Phase delegation explicit per §6 |
| AP-8 | Blind delegation trust | NO | Each phase has acceptance gate |
| AP-9 | Skipping Stage 0 | NO | This deliverable IS Stage 0 |
| AP-10 | First idea unchallenged | NO | Six patterns compared §3 |
| AP-11 | Zombie project (no kill) | NO | Halt-A through Halt-D |
| AP-12 | Timeline fantasy | LOW | "1-2 weeks" for Phase 1; coding-domain phase sizing is scope-based |
| AP-13 | Confidence without evidence | NO | Every cite has source per §10 |
| AP-14 | Wrong detail distribution | NO | Heavy on Stage 0 (right for research); light on Stage 1 specifics (deferred to ADR) |
| AP-15 | Premature precision | NO | Tier names recommended but explicitly TBD in ADR-0095 D1; field shapes deliberately not enumerated |
| AP-16 | Hallucinated effort | LOW | "~1-2 weeks Phase 1" caveats with council timing |
| AP-17 | Delegation without context | NO | Phase 1's first step is the coordination beacon (context transfer) |
| AP-18 | Unverifiable gates | NO | Each phase gate is binary observable |
| AP-19 | Missing tool fallback | N/A | Research deliverable; no execution path |
| AP-20 | Discovery amnesia | NO | Reference library §6.2 + cross-link to prior 7-gap audit |
| AP-21 | Assumed facts without sources | NO | §10 explicitly tiers primary/secondary/tertiary |

**0 hits / 21 anti-patterns.** This is the kind of plan the prior 7-gap
audit produced under UPF discipline, applied to its own forward
recommendation.

### 8.5 Cold Start Test

Can a fresh agent author ADR-0095 from this deliverable? **Yes, mostly.**
What would still trip a fresh agent:
- Numbering — verify ADR-0095 is still free at PR time (ADRs 0001..0094
  may drift). Always verify.
- Vocabulary decisions D1-D5 in §6.1.3 are recommendations, not
  ratifications. Fresh agent should propose, NOT presume.
- Phase 2 (D4) recommendation for 11th dimension might be wrong if the
  mission-space substrate has changed since this deliverable. Re-verify.

### 8.6 Plan Hygiene

Reference library §6.2 + halt conditions §6.4 + per-phase gates §6.1 +
anti-pattern guards §6.3 are all in place. ✓

### 8.7 Discovery Consolidation

This deliverable consolidates: the prior 7-gap audit, the flight-deck
plugin survey, the shipyard provider-registry + mission-space survey,
six candidate patterns from industry prior art, capability inventories
from sunfish + flight-deck, six adversarial-perspective hardenings, the
UPF 21-anti-pattern scan. **Discovery is consolidated.**

---

## 9. Residual questions for CIC

These are questions only CIC can answer (architectural intent /
prioritization / scope):

1. **Tier vocabulary preference.** Recommendation in §6.1.3 D1 is
   `domain-block` (tier 1) / `category-provider` (tier 2) /
   `capability-plugin` (tier 3). Does CIC prefer different names? Does
   CIC want to keep "slot" as a fleet term despite the AHA #1 collision
   warning?

2. **Promotion strategy for flight-deck's tier-3 substrate.** Should the
   plugin-manifest JSON-schema move to `shipyard/packages/foundation-plugins/`
   (full promotion) OR stay at `flight-deck/packages/plugins/` with a
   reference adapter in shipyard (lighter promotion)? Lighter is faster;
   full promotion is the cleanest long-term shape per CIC's "prefer
   cleanest long-term option" directive (memory 2026-05-21).

3. **Bootstrap-check composition.** §6.1.3 D4 recommends extending
   `IMinimumSpecResolver` with an 11th dimension `ProviderSlotsSatisfied`
   (simplest), but a separate `IProviderSlotResolver` composed into the
   same pipeline is also viable. Either works; CIC's call on indirection
   tolerance.

4. **Sunfish AI/media scope.** Does CIC want sunfish to remain
   "no tier 3 in MVP" (recommendation here), OR is there an MVP use case
   for AI-assisted lease summarization / OCR triage / fraud scoring that
   would make tier 3 needed earlier? If yes, Phase 1 ADR should scope
   sunfish's tier-3 needs explicitly.

5. **Capability vocabulary curator.** §7.5 flagged this. Who can add a
   new `ProviderCategory` enum value (tier 2)? Who can add a new
   capability tag like `tts/streaming` (tier 3)? Recommendation: tier-2
   adds require ADR amendment (because they ripple to provider-neutrality
   discipline + Atlas UI); tier-3 adds require a fleet-substrate
   maintainer review + capability-tag spec doc update (lighter). Confirm.

6. **Tender integration depth.** §6.1 Phase 3 mentions tender consumption
   options. Does CIC want tender to consume bundle manifests directly
   (deep integration) OR continue speaking HTTP to flight-deck
   (loose integration, current state)? Bundle-manifest direct consumption
   is the cleanest but requires `@sunfish/contracts` `file:` reference
   to be set up (per prior fleet-conventions precedent).

---

## 10. Sources cited

### Primary (filesystem state at 2026-05-25)

1. `flight-deck/packages/plugins/manifest-schema.json` — JSON schema
   verified; 17 plugin manifests under `plugins/<id>/manifest.json`.
2. `flight-deck/packages/plugins/registry.json` — registry index.
3. `flight-deck/docs/architecture/plugin-architecture.md` — design
   rationale; explicitly references capability slots.
4. `flight-deck/docs/architecture/galley-as-sunfish-accelerator.md` —
   tech-stack reconciliation; coordination beacon queued.
5. `flight-deck/packages/plugins/plugins/higgs-audio/manifest.json` —
   concrete manifest example with `defaults.port: 8881`.
6. `flight-deck/apps/web/src/features/` — 11 capability-domain feature
   directories.
7. `shipyard/packages/foundation-integrations/{IProviderRegistry,
   ProviderDescriptor,ProviderHealthStatus,CredentialsReference}.cs` —
   tier-2 substrate.
8. `shipyard/packages/foundation-integrations/README.md` — ADR 0013
   substrate description.
9. `shipyard/packages/foundation-catalog/Bundles/{ProviderCategory,
   ProviderRequirement}.cs` — ProviderCategory enum + ProviderRequirement
   record.
10. `shipyard/packages/contracts/src/integrations.ts` — TypeScript
    projection of tier-2 control-plane.
11. `shipyard/packages/contracts/src/system-requirements.ts` — TypeScript
    projection of ADR-0063 install-UX layer.
12. `shipyard/docs/adrs/0007-bundle-manifest-schema.md` —
    `BusinessCaseBundleManifest` schema.
13. `shipyard/docs/adrs/0013-foundation-integrations.md` — provider-
    neutrality policy + tier-2 substrate.
14. `shipyard/docs/adrs/0023-dialog-provider-slot-methods.md` — per-slot
    UI styling (distinct meaning of "slot").
15. `shipyard/docs/adrs/0062-mission-space-negotiation-protocol.md` —
    DirectX-Feature-Levels capability negotiation.
16. `shipyard/docs/adrs/0063-mission-space-requirements.md` — install-UX
    layer with `MinimumSpec` + `SpecPolicy`.
17. `shipyard/docs/adrs/0067-atlas-integration-config-surface.md` —
    `IIntegrationAtlasProvider` control plane.
18. `sunfish/src/Sunfish.Anchor.csproj` — sunfish capability inventory
    via `ProjectReference` entries.
19. `sunfish/src/Services/AnchorIntegrationAtlasContext.cs` — ADR-0067
    consumer in sunfish.
20. **Prior 7-gap audit:**
    `shipyard/icm/01_discovery/research/onr-slotting-architecture-7-gap-audit.md`
    (shipyard PR #146, merged 2026-05-25). All AHA / counter-finding
    framing carries forward.

### Secondary (industry / standards literature, retrieved 2026-05-25)

21. Genode capability-based security documentation — https://genode.org/documentation/genode-foundations/20.05/architecture/Capability-based_security.html
22. Genode Wikipedia — https://en.wikipedia.org/wiki/Genode
23. Fuchsia secure principles — https://fuchsia.dev/fuchsia-src/concepts/principles/secure
24. "Understanding Fuchsia Security" arXiv 2108.04183 — https://arxiv.org/pdf/2108.04183
25. Capability-based security Wikipedia — https://en.wikipedia.org/wiki/Capability-based_security
26. OSGi Architecture — https://www.osgi.org/resources/architecture/
27. OSGi 132 Repository Service — https://docs.osgi.org/specification/osgi.cmpn/8.1.0/service.repository.html
28. VSCode Contribution Points — https://code.visualstudio.com/api/references/contribution-points
29. VSCode Extension API — https://code.visualstudio.com/api
30. VSCode Common Capabilities — https://code.visualstudio.com/api/extension-capabilities/common-capabilities
31. .NET MEF (Managed Extensibility Framework) — https://visualstudiomagazine.com/articles/2014/11/01/how-to-refactor.aspx
32. MEF + plugin architecture — https://www.focisolutions.com/2017/11/dynamic-plugin-loading-using-mef/
33. Service Locator pattern — https://en.wikipedia.org/wiki/Service_locator_pattern
34. Service Locator anti-pattern (DevIQ) — https://deviq.com/antipatterns/service-locator/
35. Plugin architecture pattern (.NET) — https://blog.nashtechglobal.com/plugin-architecture-pattern-overview-net/

### Tertiary (fleet conventions / prior auto-memory)

36. `.claude/rules/universal-planning.md` — UPF framework spec.
37. `.claude/rules/fleet-conventions.md` — `@sunfish/ui-react` `file:`
    precedent; TTS 8881/8883 architecture; commit conventions.
38. `~/.claude/projects/.../memory/feedback_prefer_cleanest_long_term_option.md` —
    "ALWAYS pick the cleanest long-term option" (CIC directive 2026-05-21).
39. `~/.claude/projects/.../memory/project_post_migration_layout.md` —
    `accelerators/` removal post-2026-05-17.

**Method note:** This audit relied on filesystem state at 2026-05-25 +
prior 7-gap audit + 3 WebSearch queries for industry prior art context.
No code execution. No vendor-side API calls. Industry-literature citations
inform the design space but do NOT constitute primary sources for fleet
decisions — the primary sources are filesystem + ADR + prior audit.
