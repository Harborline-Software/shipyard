---
id: 96
title: Tier-2 Vendor-Provider Substrate
status: Proposed
date: 2026-05-25
proposed-date: 2026-05-25
author: Admiral
tier: foundation
pipeline_variant: sunfish-api-change

concern:
  - vendor-substrate
  - tier-2-discipline
  - integrations
  - security
  - production-safety

enables:
  - postmark-transactional-email
  - turnstile-captcha-verification
  - storage-vendor-substrate-future
  - identity-vendor-substrate-future
  - payments-vendor-retrofit-future

composes:
  - 13   # Foundation.Integrations (provider-neutrality precedent — parent substrate)
  - 52   # Bidirectional Messaging Substrate (sister substrate; structurally distinct from email)
  - 91   # ITenantContext Divergence Resolution (R2/A1 startup-assertion precedent)
  - 92   # Substrate-Tenant-Keyed Repository (Tier-1 domain-block discipline; this ADR is Tier-2 sibling)
  - 95   # Bootstrap Context substrate (first consumer of IEmailProvider + ICaptchaVerifier in pre-tenant signup)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-tier ADR; ADR text + each Step 1/2/3 implementation PR carries mandatory dual-council per §"Council review"

amendments: []
---

# ADR 0096 — Tier-2 Vendor-Provider Substrate

**Status:** Proposed (Revision 1; awaiting pre-merge dual-council attestation per `admiral-ruling-2026-05-25T1953Z-adr-0096-vendor-provider-substrate-8-halt-conditions.md` Halt 3 OVERRIDE)
**Date:** 2026-05-25
**Resolves:** Onboarding-ladder Decisions 4 (email vendor = Postmark) + 5 (CAPTCHA vendor = Cloudflare Turnstile) per CIC ratification `admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`. Codifies the **mock-first, vendor-swap-later** discipline as the canonical Tier-2 implementation pattern. W79 sub-cohort 1 substrate Stage-05 hand-off authoring is gated on this ADR's promotion to Accepted alongside ADR 0095.
**Predecessor research:** `shipyard/icm/01_discovery/research/onr-adr-0096-vendor-provider-substrate-scaffold.md` (1203 lines; ONR; merged via `shipyard#154` pending); `admiral-ruling-2026-05-25T1953Z-adr-0096-vendor-provider-substrate-8-halt-conditions.md` (Admiral disposed all 8 halts; 7 RATIFY-ONR + 1 OVERRIDE on Halt 3 council scope; no DEFERRED-CIC); `admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md` (CIC mock-first directive ratified 2026-05-25T18:08Z).

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-25 | Admiral | Initial draft. Folds ONR scaffold (Option B — generalized Tier-2 Vendor-Provider Substrate, not separate email/CAPTCHA ADRs) and Admiral ruling on the 8 halt conditions (7 RATIFY-ONR-RECOMMENDATION + 1 OVERRIDE on Halt 3 council scope per security-relevance rationale). Codifies `IMockVendorProvider` marker interface + `MockProviderProductionGuardAssertion` IHostedService as load-bearing production-safety substrate. New subnamespace `Sunfish.Foundation.Integrations.Email/` with `IEmailProvider` contract + `MockEmailProvider`. Retrofits existing `InMemoryCaptchaVerifier` with the marker. Adds `ProviderCategory.Captcha` + `ProviderCategory.TransactionalEmail` enum values in Step 1. New packages `packages/providers-postmark/` + `packages/providers-turnstile/` (HttpClient-only adapters per ADR 0013). Step sequencing: Step 1 substrate → (Step 2 Postmark ∥ Step 3 Turnstile) → Step 4 W79 wiring. Dual-council MANDATORY on this ADR + each implementation PR per Halt 3 OVERRIDE. Status: Proposed (awaiting dual-council). |

Promotion path: both councils self-attest GREEN via inbox status on Revision 1 → Admiral promotes ADR to `Accepted`. If a council returns AMBER, Admiral folds amendments into Revision 2 (ADR 0095 R2 precedent). **Step 1, Step 2, and Step 3 implementation PRs each carry their own mandatory dual-council SPOT-CHECK at PR-open** (per Halt 3 OVERRIDE) — these are independent council pulls on the implementation surface, not gated on ADR re-attest.

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Integrations.IMockVendorProvider` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Integrations.DependencyInjection.MockProviderProductionGuardAssertion` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Integrations.DependencyInjection.VendorProviderServiceCollectionExtensions.AddSunfishVendorProvider<TContract, TConcrete>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Integrations.DependencyInjection.VendorProviderServiceCollectionExtensions.UseVendorProviderIfConfigured<TContract, TConcrete>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Integrations.Email.IEmailProvider` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Integrations.Email.MockEmailProvider` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Catalog.Bundles.ProviderCategory.Captcha` (enum value) | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Catalog.Bundles.ProviderCategory.TransactionalEmail` (enum value) | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Providers.Postmark.PostmarkEmailProvider` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Providers.Turnstile.TurnstileCaptchaVerifier` | Introduced by this ADR | no — added in Step 3 PR |
| `Sunfish.Foundation.Integrations.Captcha.ICaptchaVerifier` | Existing | yes — `shipyard/packages/foundation-integrations/Captcha/ICaptchaVerifier.cs` (ADR 0059, W#28 Phase 3) |
| `Sunfish.Foundation.Integrations.Captcha.InMemoryCaptchaVerifier` | Existing (retrofit in Step 1) | yes — `shipyard/packages/foundation-integrations/Captcha/InMemoryCaptchaVerifier.cs` |
| `Sunfish.Foundation.Integrations.IProviderRegistry` | Existing | yes — `shipyard/packages/foundation-integrations/IProviderRegistry.cs` |
| `Sunfish.Foundation.Integrations.ProviderDescriptor` | Existing | yes — `shipyard/packages/foundation-integrations/ProviderDescriptor.cs` |
| `Sunfish.Foundation.Integrations.CredentialsReference` | Existing | yes — `shipyard/packages/foundation-integrations/CredentialsReference.cs` |
| `Sunfish.Foundation.Integrations.Messaging.IMessagingGateway` | Existing (structurally distinct — see §Decision drivers) | yes — `shipyard/packages/foundation-integrations/Messaging/IMessagingGateway.cs` (ADR 0052) |
| `Sunfish.Providers.Recaptcha.RecaptchaV3CaptchaVerifier` | Existing (shape precedent for Step 2/3) | yes — `shipyard/packages/providers-recaptcha/RecaptchaV3CaptchaVerifier.cs` |

---

## Context

Sunfish's `Foundation.Integrations` substrate (ADR 0013) codifies provider-neutrality
as a first-class platform discipline: domain code depends on vendor-neutral contracts
(`ICaptchaVerifier`, `IPaymentGateway`, `IMessagingGateway`); concrete vendor adapters
ship in separate `providers-{vendor}` packages and register through
`IProviderRegistry.Register(ProviderDescriptor, ...)`. Two onboarding-ladder decisions
landed 2026-05-25 that exercise this substrate at scale for the first time outside
the existing reCAPTCHA-only path:

- **Decision 4 (CIC ratification 2026-05-25T18:08Z):** Email transactional provider
  is **Postmark**.
- **Decision 5 (CIC ratification 2026-05-25T18:08Z):** CAPTCHA / bot-protection
  provider is **Cloudflare Turnstile**.

Both decisions arrived with a substantive architectural directive: **mock-first,
vendor-swap-later** (CIC 2026-05-25T18:08Z verbatim: *"mocked services should be set
up for now and eventually replaced with real subscriptions"*). The directive is
load-bearing across more than these two vendors — it codifies how every Tier-2
vendor surface (storage, identity, payments-retrofit, etc.) should ship: a mock
implementation lands first; the real vendor adapter ships as a separate package and
swaps into the composition root through environment-driven DI registration without
touching domain code.

ONR's scaffold (`shipyard#154`; 1203 lines) surveyed the existing
`Foundation.Integrations` surface and confirmed: `IProviderRegistry`,
`ProviderDescriptor`, `ProviderHealthStatus`, `CredentialsReference`,
`InMemoryProviderRegistry`, `ICaptchaVerifier`, `InMemoryCaptchaVerifier`,
`IMessagingGateway`, `IPaymentGateway` all exist as canonical platform contracts;
`ProviderCategory` enum carries `Billing=0`, `Payments=1`, `Messaging=5`, `Other=99`;
`Sunfish.Providers.Recaptcha` (W#28 Phase 3) establishes the canonical adapter shape
(HttpClient-no-SDK + `CredentialsReference` + `ProviderDescriptor` registration).
The substrate is real and working — what's missing is a codified pattern that
generalizes it across vendor categories with the production-safety properties the
mock-first directive demands.

A naive mock-first implementation introduces a critical foot-gun: a typo'd env var
(e.g., `POSTMRK_API_KEY` instead of `POSTMARK_API_KEY`) means the real adapter never
registers, the mock silently wins, and production signups bypass real CAPTCHA and
never send real email. The substrate **must** fail-loudly-at-startup when a
deployment is intended for production but the composition root resolved to mock
providers — analogous to ADR 0091 R2's `TenantContextRegistrationAssertion`
IHostedService that catches confused-deputy DI mis-wiring at process start.

This ADR codifies **one canonical Tier-2 Vendor-Provider Substrate pattern**:

1. A marker interface (`IMockVendorProvider`) tags every mock implementation.
2. An IHostedService startup assertion (`MockProviderProductionGuardAssertion`)
   verifies environment-vs-registration coherence — production deployments either
   register real adapters OR explicitly opt-in to mocks via
   `SUNFISH_ALLOW_MOCK_PROVIDERS=true`.
3. DI extension helpers (`AddSunfishVendorProvider<TContract, TConcrete>` +
   `UseVendorProviderIfConfigured<TContract, TConcrete>`) register mocks
   unconditionally then conditionally swap in real adapters when their env-var-keyed
   credentials are present.
4. Real vendor adapters ship as separate `providers-{vendor}` packages
   (HttpClient-only per ADR 0013), each registered via the same conditional helper.

ONR explicitly raised the option of two narrower ADRs (Option A: ADR 0096 email-only,
ADR 0097 CAPTCHA-only) vs one generalized ADR (Option B: this ADR). The Admiral
ruling RATIFIED Option B — the discipline IS the substrate; codifying it once mirrors
ADR 0013's provider-neutrality precedent and applies cleanly to future Tier-2
categories without re-authoring. Per `feedback_prefer_cleanest_long_term_option`,
the +1.5-2× ADR text cost is the correct trade.

This ADR is **substrate-tier**. The first consumers — the public signup endpoint
family (`POST /api/signup`, `POST /api/signup/verify-email`, `POST /api/signup/resend-verification`,
`POST /api/signup/check-availability`) — live in W79 sub-cohort 1 Stage-05 hand-off
territory and consume both `IEmailProvider` (this ADR) and `IBootstrapContext`
(ADR 0095) together. The W79 Stage-05 hand-off is gated on both ADRs reaching
Accepted.

## Decision drivers

**D1 — Production-safety substrate is load-bearing.** The mock-first directive only
ships safely when the substrate fails loudly at startup if production composition
resolved to mocks. This requires three coordinated invariants codified at substrate
tier (not as documentation guidance):

- **D1a (marker interface):** Every mock implementation carries `IMockVendorProvider`
  as a marker interface in addition to its vendor-neutral contract
  (`MockEmailProvider : IEmailProvider, IMockVendorProvider`;
  `InMemoryCaptchaVerifier : ICaptchaVerifier, IMockVendorProvider`). This is the
  positive identification mechanism — a registered service is "a mock" iff its
  concrete type implements `IMockVendorProvider`.
- **D1b (opt-out env var):** Production deployments that intentionally ship with
  mocks (load-test environments, closed demo deployments, on-prem trials before
  real-vendor accounts are provisioned) **must** set
  `SUNFISH_ALLOW_MOCK_PROVIDERS=true` explicitly. The env-var name is deliberately
  alarming; presence-of-truthy-value is the opt-in semantics; default-on-absence is
  the production-safe default. No alternative opt-out path (no config file, no DI
  override) — the env var is the single load-bearing escape hatch.
- **D1c (production-default invariant):** When `ASPNETCORE_ENVIRONMENT=Production`,
  the `MockProviderProductionGuardAssertion` IHostedService verifies at startup
  that for every Tier-2 contract registered with a mock concrete type, either (a)
  the corresponding real-adapter env var is present (which means the registration
  should have been replaced — this is the "typo silently wins" foot-gun closer)
  OR (b) `SUNFISH_ALLOW_MOCK_PROVIDERS=true` is set. Otherwise startup fails with
  a `MockInProductionException` enumerating the offending contracts.

Together these three invariants make the mock-first directive **production-safe by
construction**: composition errors fail at startup, not at first real load. This is
the analog of ADR 0091 R2's `TenantContextRegistrationAssertion` confused-deputy
fix; both substrate ADRs adopt the same "fail loudly at startup" mechanism for
load-bearing DI correctness.

**D2 — `IEmailProvider` is structurally distinct from `IMessagingGateway`.** ADR 0052
introduced `IMessagingGateway` as a *bidirectional thread messaging* substrate:
inbound webhook ingestion, threaded conversations, per-tenant per-sender isolation,
`ThreadToken`, `OutboundMessageRequest` carrying `ThreadId` + `Participant[]` +
`MessageVisibility`. Transactional onboarding email (welcome, verification,
invitation, password-reset) is *unidirectional fire-and-forget* with idempotency-
keyed retries, no thread, no participants array, no inbound webhook surface, and
**pre-tenant scope** (signup runs in `IBootstrapContext` per ADR 0095). Folding
transactional email into `IMessagingGateway` would conflate two structurally
distinct surfaces — exactly the kind of interface conflation ADR 0091 R2 fixed in
the tenant-context layer. ADR 0096 separates them at the contract layer:
`Foundation.Integrations.Email/IEmailProvider` is a new substrate sibling to
`Foundation.Integrations.Messaging/IMessagingGateway`, not an extension of it. The
folder layout in `foundation-integrations` is already canonical
(`Captcha/`, `Messaging/`, `Payments/`, `Signatures/`); adding `Email/` is the
consistent shape.

**D3 — `Foundation.Integrations` provider-neutrality (ADR 0013) extends to Tier-2.**
ADR 0013 codifies HttpClient-only vendor adapters as the provider-neutrality discipline:
no vendor SDK dependencies in `providers-{vendor}` packages; raw HttpClient + manual
request/response shaping. The W#28 Phase 3 reCAPTCHA adapter establishes the shape;
Postmark + Turnstile inherit it. (The Postmark `.NET` SDK exists and adds value for
retry + idempotency-key handling — that decision is forwarded to .NET-architect
council per Open Question 2; Admiral's prior is HttpClient-only for ADR 0013
supply-chain consistency.)

**D4 — Composition-tier swap, not compile-tier swap.** The substrate's value
proposition is *deploy-time* vendor swap, not *compile-time* vendor swap. Mock
registration and real-vendor registration both flow through the same
`IServiceCollection` mutation surface; the conditional registration helper
(`UseVendorProviderIfConfigured`) calls `services.Replace(...)` when its env-var
key resolves to a non-empty value. This means a single binary deployment can run
mock-only (dev/test), opt-in mock (`SUNFISH_ALLOW_MOCK_PROVIDERS=true`), or fully
real (env vars populated) — composition decides; no rebuild required.

**D5 — `ICaptchaVerifier` naming asymmetry is preserved, not retrofitted.** The
existing `ICaptchaVerifier` (ADR 0059, W#28 Phase 3) is an action-noun (the thing
that verifies CAPTCHA tokens); the new `IEmailProvider` is a role-noun (the thing
that provides email egress capability). Both .NET BCL precedents exist
(`IConfigurationProvider` is role; `IClock` and `IRandomNumberGenerator` are
action). The Tier-2 substrate pattern identifies its members via
`IMockVendorProvider` marker membership and `ProviderCategory` enum participation,
NOT via `IXProvider` naming convention. Future Tier-2 contracts adopt `IXProvider`
naming; `ICaptchaVerifier` is grandfathered in and documented as such in the
`IMockVendorProvider` xmldoc.

## Considered options

**Option A — Two narrower ADRs (email + CAPTCHA separately).** ADR 0096 covers
email substrate only; ADR 0097 covers CAPTCHA substrate separately. Each ADR is
~half the length; less abstraction-up-front. **Rejected** in favor of Option B
(Admiral ruling 2026-05-25T19:53Z): the discipline IS the substrate; codifying it
once captures the mock-first directive once and applies to every future Tier-2
vendor (storage, identity, payments-retrofit) without re-authoring. The +1.5-2× ADR
text cost of Option B is the correct trade per `feedback_prefer_cleanest_long_term_option`.

**Option B — One generalized "Tier-2 Vendor-Provider Substrate" ADR (this ADR).**
Codifies the mock-first discipline once. Email + CAPTCHA arrive as the first two
worked examples; future Tier-2 vendors retrofit by following the same pattern. The
substrate-level invariants (`IMockVendorProvider` marker + production-guard
IHostedService) apply across every Tier-2 category. **APPROVED** — this option.

**Option C — Substrate-level discipline as documentation, not codified contracts.**
Ship the mock-first directive as guidance in `_shared/engineering/` without an ADR;
let each vendor surface adopt the pattern ad hoc. **Rejected** — the production-
safety property (D1c production-default invariant) is load-bearing for security and
requires runtime enforcement, not documentation discipline. Without the startup
assertion the silent-typo foot-gun ships.

## Decision

**One generalized Tier-2 Vendor-Provider Substrate ADR (Option B) codifying
mock-first as the canonical Tier-2 implementation discipline:**

1. **Same-package mock-with-contracts layout** (Halt 1 RATIFY).
   `MockEmailProvider` ships in `packages/foundation-integrations/` alongside
   `IEmailProvider` (matching the existing `InMemoryCaptchaVerifier` + `ICaptchaVerifier`
   co-location pattern). No separate `foundation-integrations-mocks` package.

2. **`Foundation.Integrations.Email/` subnamespace with new `IEmailProvider`**
   structurally distinct from `IMessagingGateway` (Halt 2 RATIFY; see D2). New
   folder `packages/foundation-integrations/Email/` carries `IEmailProvider.cs`,
   `MockEmailProvider.cs`, plus supporting types (`EmailMessage`,
   `EmailDispatchResult`, etc.).

3. **Dual-council MANDATORY on this ADR text AND on each Step 1/2/3
   implementation PR** (Halt 3 OVERRIDE; .NET-architect + security-engineering).
   The startup-assertion mechanism is itself a security-relevant substrate seam
   (analog to the ADR 0091 R2 A1 confused-deputy fix that came back AMBER from
   sec-eng). ADR 0095 sister-precedent set dual-council MANDATORY on the ADR text
   precisely to avoid Rev-1-too-narrow → Rev-2-with-strengthening churn; ADR 0096
   follows the same discipline.

4. **`ProviderCategory.Captcha = 10` + `ProviderCategory.TransactionalEmail = 11`
   enum extensions in Step 1 PR** (Halt 4 RATIFY). Engineer picks the exact integer
   values at Step 1 authoring time (appending stable slots; not renumbering).

5. **`ICaptchaVerifier` is kept as-is; `IXProvider` naming convention documented**
   in `IMockVendorProvider` xmldoc (Halt 5 RATIFY; see D5).

6. **`MessageStream` is a free-form string property in ADR 0096**; W80 Stage-05
   defines the initial taxonomy (Halt 6 RATIFY).

7. **Turnstile site-key delivery to frontend is out of scope for ADR 0096**;
   W80 Stage-05 defines the bridge-to-frontend site-key delivery endpoint (Halt 7
   RATIFY).

8. **Implementation step sequencing:** Step 1 substrate package extension →
   (Step 2 Postmark adapter ∥ Step 3 Turnstile adapter; parallel) → Step 4 W79
   composition-root wiring (Halt 8 RATIFY).

9. **`MockProviderProductionGuardAssertion` IHostedService is load-bearing
   substrate** (per D1; codified independent of any halt-condition resolution).
   Three invariants: `IMockVendorProvider` marker on every mock concrete type;
   `SUNFISH_ALLOW_MOCK_PROVIDERS=true` as the sole opt-out env var; production-
   default-fail invariant when `ASPNETCORE_ENVIRONMENT=Production` and neither
   real-adapter env vars are present nor opt-out env var is set.

## Substrate / layering notes

**Interaction with ADR 0013 (Foundation.Integrations).** ADR 0096 is a direct
elaboration of ADR 0013's provider-neutrality discipline applied to a specific
implementation pattern (mock-first). HttpClient-only vendor adapters per ADR 0013
§Enforcement is inherited; Step 2 (Postmark) + Step 3 (Turnstile) PRs each ship
HttpClient-only adapters. The Postmark SDK question is forwarded to council
(Open Question 2).

**Interaction with ADR 0052 (Bidirectional Messaging Substrate).** ADR 0052's
`IMessagingGateway` covers a structurally distinct surface (bidirectional thread
messaging; see D2). ADR 0096 explicitly declines to fold transactional email into
`IMessagingGateway`. Forward-watch (§Revisit triggers): the ADR 0052 `IMessagingGateway`
DI registration could be retrofitted with `IMockVendorProvider` marker at its next
amendment opportunity — that's a separate ADR 0052 amendment, not ADR 0096 scope.

**Interaction with ADR 0091 R2 (ITenantContext Divergence).** The
`MockProviderProductionGuardAssertion` IHostedService is the **direct stylistic and
mechanistic analog** of ADR 0091 R2's `TenantContextRegistrationAssertion` (also
IHostedService; also "fail loudly at startup"; also closes a silent-DI-mis-wiring
foot-gun). The two assertions are independent (different concerns: one tenant-context
correctness, one mock-provider production-safety) but share the same substrate
discipline. Future startup assertions for substrate-tier safety properties should
adopt the same shape.

**Interaction with ADR 0092 (Substrate-Tenant-Keyed Repository).** ADR 0092 is the
Tier-1 domain-block discipline (concrete DI; no swap); ADR 0096 is the Tier-2
category-provider discipline (bounded vendor swap). The two are siblings in the
slotting-architecture taxonomy (shipyard#152) — Tier-1 never mocks-then-swaps;
Tier-2 mocks-first-then-swaps; Tier-3 (capability-plugin; flight-deck surface;
not sunfish) is runtime-swap. ADR 0096 does NOT touch Tier-1 domain blocks.

**Interaction with ADR 0094 (IAuditEventReader).** ADR 0094's read-substrate is
Tier-1 (concrete DI; no vendor swap); ADR 0096 does not touch it. Audit emission
into Tier-2 vendor adapters (e.g., emitting a `email.send.attempted` audit event
from `PostmarkEmailProvider`) follows the pattern-009 SPOT-CHECK discipline at the
Step 2/3 PR layer — covered by Halt 3 dual-council SPOT-CHECK.

**Interaction with ADR 0095 (Bootstrap Context).** ADR 0095 establishes the
`IBootstrapContext` substrate for pre-tenant signup. The signup handler runs in
bootstrap scope; its first-real-load consumers of Tier-2 vendor substrate are
`IEmailProvider` (welcome / verification email send) and `ICaptchaVerifier`
(Turnstile token verification at form submit). W79 sub-cohort 1 Stage-05 hand-off
wires both `IBootstrapContext` + `IEmailProvider` + `ICaptchaVerifier` together;
the hand-off cites both ADR 0095 and ADR 0096 in its §Substrate consumed.

**Tier-2 category-provider boundary.** ADR 0096 is the canonical pattern for Tier-2
vendor substrates. The substrate scope **is**: email transactional, CAPTCHA,
storage, identity (OIDC), payments retrofit (`IPaymentGateway` already exists per
ADR 0051), messaging (`IMessagingGateway` already exists per ADR 0052 — retrofit
candidate). The substrate scope **is not**: Tier-1 domain blocks (tenant/property/
lease/invoice/audit; ADR 0092), Tier-3 capability-plugins (TTS/STT/image/LLM;
flight-deck surface).

## Implementation roadmap

Four Step PRs. Step 1 is the substrate-package extension (the load-bearing surface);
Steps 2 + 3 are the vendor adapters and run in parallel; Step 4 is W79 sub-cohort
composition-root wiring (cited as forward-link, not delivered by this ADR).

### Step 1 — Substrate-package extension PR

Branch shape: `feat/adr-0096-step-1-tier-2-vendor-substrate` (Engineer-authored
post-ADR Acceptance).

Scope:

- `packages/foundation-integrations/` (extend):
  - `IMockVendorProvider.cs` — marker interface; empty (no members);
    xmldoc documents the `IXProvider` naming convention + grandfathered
    `ICaptchaVerifier` asymmetry.
  - `Email/IEmailProvider.cs` — contract: `Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)`.
  - `Email/EmailMessage.cs` — record carrying `From`, `To[]`, `Subject`, `BodyHtml`,
    `BodyText`, `MessageStream` (free-form string per Halt 6), `IdempotencyKey`,
    `EmailDispatchId`.
  - `Email/EmailDispatchResult.cs` — discriminated-union: `Accepted(MessageId)` |
    `RateLimited(RetryAfter)` | `Rejected(Reason)` | `TransportError(Detail)`.
  - `Email/MockEmailProvider.cs` — `: IEmailProvider, IMockVendorProvider`; writes
    to in-memory store + console log + optional dev inbox UI route (W79 endpoint
    territory, not implemented here).
  - `Captcha/InMemoryCaptchaVerifier.cs` (retrofit) — adds `IMockVendorProvider`
    marker membership; preserves existing `AlwaysPass()` / `AlwaysFail()` /
    `WithMagicToken(token)` static factories (or adds them if not yet present per
    ONR audit).
  - `DependencyInjection/VendorProviderServiceCollectionExtensions.cs`:
    - `AddSunfishVendorProvider<TContract, TConcrete>(this IServiceCollection)`
      registers the mock concrete unconditionally + registers a
      `ProviderDescriptor` against `IProviderRegistry`.
    - `UseVendorProviderIfConfigured<TContract, TConcrete>(this IServiceCollection, string envVarKey)`
      checks `Environment.GetEnvironmentVariable(envVarKey)`; when truthy, calls
      `services.Replace(ServiceDescriptor.Scoped<TContract, TConcrete>())` AND
      updates the `IProviderRegistry` `ProviderDescriptor`. Otherwise no-op.
  - `DependencyInjection/MockProviderProductionGuardAssertion.cs` —
    `: IHostedService`. On `StartAsync`:
    - If `ASPNETCORE_ENVIRONMENT != "Production"`, return immediately.
    - If `Environment.GetEnvironmentVariable("SUNFISH_ALLOW_MOCK_PROVIDERS") == "true"`,
      return immediately.
    - Else: resolve every registered `IMockVendorProvider`-implementing service
      from the IServiceProvider; if any are present, throw
      `MockInProductionException` enumerating the offending contracts. Startup
      fails.
  - `MockInProductionException.cs` — typed exception carrying the list of
    offending contract type names.

- `packages/foundation-catalog/` (extend):
  - `Bundles/ProviderCategory.cs`: add `Captcha = 10` and `TransactionalEmail = 11`
    (or next-available stable slots — Engineer picks at authoring time).

- Unit tests in `packages/foundation-integrations.tests/`:
  - `MockProviderProductionGuardAssertionTests` — covers all branches: non-prod
    bypass, opt-out env-var bypass, prod-with-mock-no-opt-out throws, prod-with-no-
    mocks passes. **Note** — per the ADR 0095 R2 .NET-architect A3 amendment, the
    test resolves marker-implementing services from a stub `IServiceCollection`
    fixture; it does NOT exercise the production `IHttpContextAccessor`-dependent
    surfaces (which are null at `IHostedService.StartAsync`).
  - `VendorProviderServiceCollectionExtensionsTests` — registration-presence
    contract + conditional-swap behavior under env-var presence/absence.
  - `InMemoryCaptchaVerifierTests` — marker-membership assertion + factory-method
    behavior.
  - `MockEmailProviderTests` — send-to-memory + idempotency-key honoring.

- Documentation:
  - xmldoc on every introduced type per ADR 0069 §A0 discipline.
  - `IMockVendorProvider` xmldoc explicitly names the `IXProvider` naming
    convention and the `ICaptchaVerifier` grandfathered exception (per D5).

**Council review (Halt 3 OVERRIDE): MANDATORY dual-council at PR-open.**
.NET-architect council reviews the DI helper shape, the IHostedService assertion
mechanism, and the conditional-swap semantics. Security-engineering council
reviews the production-guard invariants, the env-var opt-out semantics, and the
silent-typo foot-gun closure.

### Step 2 — Postmark adapter PR (parallel with Step 3)

Branch shape: `feat/adr-0096-step-2-providers-postmark`.

Scope:

- New package `packages/providers-postmark/`:
  - `PostmarkEmailProvider.cs` — `: IEmailProvider`. HttpClient-based (no SDK per
    ADR 0013 supply-chain discipline; SDK question forwarded to council per
    Open Question 2). Resolves `POSTMARK_API_KEY` via `IHostSecretResolver` (or
    direct env var if `IHostSecretResolver` not yet ratified by council per Open
    Question 3).
  - `PostmarkProviderDescriptor.cs` — registers `ProviderCategory.TransactionalEmail`
    descriptor with `IProviderRegistry`.
  - DI extension: `AddPostmarkEmailProvider(this IServiceCollection)` which calls
    `UseVendorProviderIfConfigured<IEmailProvider, PostmarkEmailProvider>("POSTMARK_API_KEY")`.
  - Maps `EmailMessage` to Postmark's `/email` payload; honors `IdempotencyKey`
    as Postmark's `Headers["X-PM-Tag"]` or equivalent (council to ratify exact
    field per Open Question 4).
  - HttpClient registration with retry policy (Polly or `IHttpClientFactory`
    transient handler); council to ratify exact retry geometry.

- Tests: integration tests against Postmark sandbox stamp; unit tests against an
  `IHttpMessageHandler` fake covering 200 / 4xx / 5xx / timeout branches.

**Council review (Halt 3 OVERRIDE): MANDATORY dual-council SPOT-CHECK at PR-open.**
Sec-eng reviews credential handling (API key resolution path + transport security
+ idempotency-key shape against replay-attack concerns). .NET-architect reviews
the HttpClient + Polly geometry + the `ProviderDescriptor` registration coherence.

### Step 3 — Turnstile adapter PR (parallel with Step 2)

Branch shape: `feat/adr-0096-step-3-providers-turnstile`.

Scope:

- New package `packages/providers-turnstile/`:
  - `TurnstileCaptchaVerifier.cs` — `: ICaptchaVerifier`. HttpClient-based.
    Resolves `TURNSTILE_SECRET_KEY` via `IHostSecretResolver` (or direct env var
    pending council ratification).
  - `TurnstileProviderDescriptor.cs` — registers `ProviderCategory.Captcha`
    descriptor with `IProviderRegistry`.
  - DI extension: `AddTurnstileCaptchaVerifier(this IServiceCollection)` which
    calls `UseVendorProviderIfConfigured<ICaptchaVerifier, TurnstileCaptchaVerifier>("TURNSTILE_SECRET_KEY")`.
  - Maps to Cloudflare's `/turnstile/v0/siteverify` endpoint. Action parameter
    handling deferred per Open Question 5 (Admiral prior: defer to
    `IExtendedCaptchaVerifier` if-and-when wanted).

- Tests: integration tests against Cloudflare's documented test secrets
  (`1x0000000000000000000000000000000AA` always passes; `2x0000000000000000000000000000000AA`
  always fails); unit tests against `IHttpMessageHandler` fake.

**Council review (Halt 3 OVERRIDE): MANDATORY dual-council SPOT-CHECK at PR-open.**
Sec-eng reviews the verify-call payload shape (`secret` field MUST NOT log;
`remoteip` field handling; response-validation semantics). .NET-architect reviews
the HttpClient geometry and the `ProviderDescriptor` registration coherence.

### Step 4 — W79 composition-root wiring (NOT directly delivered by this ADR)

W79 sub-cohort 1 Stage-05 hand-off territory. signal-bridge `Program.cs` calls:

```csharp
services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
services.UseVendorProviderIfConfigured<IEmailProvider, PostmarkEmailProvider>("POSTMARK_API_KEY");

services.AddSunfishVendorProvider<ICaptchaVerifier, InMemoryCaptchaVerifier>();
services.UseVendorProviderIfConfigured<ICaptchaVerifier, TurnstileCaptchaVerifier>("TURNSTILE_SECRET_KEY");

services.AddHostedService<MockProviderProductionGuardAssertion>();
```

Plus the four new signup endpoint routes (`POST /api/signup`,
`POST /api/signup/verify-email`, `POST /api/signup/resend-verification`,
`POST /api/signup/check-availability`). **pattern-009 SPOT-CHECK applies** at the
W79 PR-open per fleet conventions (Bridge endpoint + frontend rebind pair —
4 new routes).

## Alternatives considered (rejections)

**Alt A — Fold transactional email into `IMessagingGateway`.** REJECTED per D2.
`IMessagingGateway` is bidirectional thread messaging; transactional email is
unidirectional pre-tenant fire-and-forget. Folding them would conflate two
structurally distinct surfaces (interface conflation; same anti-pattern ADR 0091
R2 fixed in the tenant-context layer).

**Alt B — Separate `*-mocks` package layout** (e.g.,
`packages/foundation-integrations-mocks/`). REJECTED per Halt 1. The existing
`InMemoryCaptchaVerifier` already co-locates with `ICaptchaVerifier`; relocating
it would be a small breaking change without benefit. The `IMockVendorProvider`
marker + production-guard IHostedService provides the production-safety guarantee
that a separate `*-mocks` package boundary would otherwise enforce via Roslyn
analyzer — and does so at runtime in every deployment rather than only at build
time.

**Alt C — Compile-time mock-vs-production selection** (e.g., `#if MOCK_PROVIDERS`
preprocessor directives or two NuGet package targets — `Sunfish.Providers.Postmark`
vs `Sunfish.Providers.Postmark.Mock`). REJECTED per D4. The substrate value
proposition is deploy-time vendor swap; compile-time selection requires two
builds, breaks the single-binary deployment story, and prevents the env-var
escape hatch.

**Alt D — Single-ADR-per-vendor (Option A).** REJECTED per Admiral ruling
2026-05-25T19:53Z. The discipline IS the substrate; codifying it once captures
the mock-first directive once and applies to every future Tier-2 vendor. Per
`feedback_prefer_cleanest_long_term_option` the +1.5-2× ADR text cost is correct.

**Alt E — `MockProviderProductionGuardAssertion` as a Roslyn analyzer instead of
IHostedService.** REJECTED — Roslyn analyzers run at compile time and cannot
observe the runtime DI registration tree; the analyzer would have to model the
composition-root config statically (which is fragile across multi-environment
deployments). IHostedService at process start observes the actual resolved
registrations and fails the deployment, not the build. Future amendment may add
a complementary Roslyn analyzer for an additional static-check layer (analog to
ADR 0091 R2 Step 3 analyzer), but the IHostedService is the load-bearing
mechanism.

**Alt F — Rename `ICaptchaVerifier` to `ICaptchaProvider` for naming uniformity.**
REJECTED per Halt 5 RATIFY. Existing consumers (`RecaptchaV3CaptchaVerifier` +
`InMemoryCaptchaVerifier`) churn for negligible benefit; .NET BCL precedent allows
descriptive asymmetry (role-nouns vs action-nouns); the substrate pattern is
identified by `IMockVendorProvider` membership + `ProviderCategory` participation,
not by naming convention.

## Consequences

**Positive:**

- Mock-first production-safety property is **codified at substrate tier**, not
  documentation discipline. The silent-typo foot-gun (`POSTMRK_API_KEY` instead of
  `POSTMARK_API_KEY`) fails loudly at startup; no silent-bypass shipping path.
- Tier-2 vendor categories follow **one canonical pattern**. Adding a new vendor
  (storage, identity, OIDC) requires (a) a new `IXProvider` contract + mock in
  `foundation-integrations/X/`, (b) a new `providers-{vendor}/` package, (c) a
  new `ProviderCategory.X` enum value, (d) one `UseVendorProviderIfConfigured`
  call in composition root. No new ADR per vendor.
- Existing `Foundation.Integrations` substrate (ADR 0013) is **extended, not
  refactored**. The retrofit cost is one new marker interface added to
  `InMemoryCaptchaVerifier` and `MockProviderProductionGuardAssertion` IHostedService
  registration.
- Composition-tier swap means **single-binary deployment** across dev/test/prod;
  env vars decide. Operations gains: lower image variance, simpler rollouts.

**Negative / costs:**

- Three new packages (`providers-postmark` + `providers-turnstile` + the
  `foundation-integrations` extension) ship at MVP-phase. Engineering hours
  pre-MVP: ~3-5 days across the three Step PRs (Step 2 + Step 3 parallel).
- Mocks ship in the same package as contracts — non-Sunfish consumers (none today,
  but possible in future) take a ~1-2 KB dependency on mock implementations they
  may never use. Marginal; not a real cost.
- Dual-council MANDATORY review on this ADR + each Step PR adds ~30-min dispatch
  latency per PR. Pre-paid against the Rev-2-with-strengthening churn pattern
  ADR 0095 demonstrated (one round of council is cheaper than two).

**Risks:**

- **Risk R1 — Step 1 PR scope creep.** Step 1 covers a lot (marker + IHostedService +
  two DI helpers + `IEmailProvider` + `MockEmailProvider` + `InMemoryCaptchaVerifier`
  retrofit + enum extension + tests). Engineer may split into two PRs if scope
  threshold reached (per fleet PR-cap discipline). Mitigation: explicit Step 1a/1b
  split in the W79 Stage-05 hand-off if Engineer flags.

- **Risk R2 — `IHostSecretResolver` not yet ratified.** Step 2 + Step 3 adapters
  need to resolve secrets at request time. If `IHostSecretResolver` doesn't yet
  exist in `foundation-integrations`, council either ratifies its addition in
  Step 1 OR defers to a follow-up Engineer dispatch. Open Question 3 forwards
  this to .NET-architect council. Mitigation: Step 2 + Step 3 can ship with
  direct `Environment.GetEnvironmentVariable` calls if `IHostSecretResolver`
  defers; refactored to the resolver when ratified.

- **Risk R3 — Postmark SDK vs HttpClient discrepancy.** Admiral's prior is
  HttpClient-only per ADR 0013; council may override to allow Postmark SDK.
  Mitigation: Step 2 PR ships HttpClient-only first; if council rules SDK is
  acceptable, refactor in a follow-up amendment PR.

- **Risk R4 — Production-guard assertion false-positive when load-tests deploy
  with mocks intentionally.** Mitigation: `SUNFISH_ALLOW_MOCK_PROVIDERS=true`
  opt-out env var is the documented escape hatch; load-test infrastructure docs
  must reference it.

## Open questions (forwarded to council)

These five questions are explicitly NOT pre-empted by this Rev 1 draft; they
route to dual-council attestation at PR-open per Halt 3 OVERRIDE. ONR named them
in §10 of the scaffold; the Admiral ruling forwarded them in §"5 open questions
forwarded to dual-council attestation."

**Q1 — Conditional DI registration semantics in `Microsoft.Extensions.DependencyInjection`.**
Replacing a prior registration via `services.Replace(...)` is well-documented;
conditional `services.Replace` driven by env-var presence has subtle ordering
implications around `IServiceCollection` mutation timing. Council
(.NET-architect) validates the `UseVendorProviderIfConfigured` shape against
canonical patterns; if the proposed shape needs refinement, council proposes
amendment for Rev 2.

**Q2 — Postmark `.NET` SDK vs HttpClient-only.** Postmark ships a `Postmark.NET`
SDK; reCAPTCHA adapter precedent uses HttpClient-only. Admiral's prior on Rev 1:
HttpClient-only for ADR 0013 supply-chain consistency. Council (.NET-architect)
may override; if SDK is allowed, Step 2 PR refactors accordingly.

**Q3 — `IHostSecretResolver` shape.** Postmark + Turnstile adapters need secret
resolution at request time; existing `CredentialsReference` is an opaque handle.
Council (.NET-architect): either ratify `IHostSecretResolver` addition in Step 1
OR defer to a follow-up Engineer dispatch. If deferred, Step 2 + Step 3 ship with
direct env-var lookup as documented in Risk R2.

**Q4 — Email idempotency-key semantics across retries.** If the signup endpoint
retries an email send (transient network error), is the `EmailDispatchId`
propagated to Postmark as a vendor idempotency key, or does the substrate
maintain its own dedup table? Cross-cuts with `foundation-idempotency` package.
Council (.NET-architect; sec-eng SPOT-CHECK adjacent for replay-attack concerns).
Resolution pathway: ADR 0096 cites forward-watch; Engineer aligns in W79
Stage-05 hand-off authoring.

**Q5 — Cloudflare Turnstile per-action support.** Turnstile supports an `action`
parameter for per-route tagging. Adding a parameter to `ICaptchaVerifier.VerifyAsync`
is a breaking change. Admiral's prior: defer (action does not affect verification
correctness, only Cloudflare's analytics dashboard categorization). If wanted
later, extend via `IExtendedCaptchaVerifier : ICaptchaVerifier`. Council
(.NET-architect) attestation requested.

## Revisit triggers

This ADR is revisited (Rev 2 or follow-up amendment) when any of:

1. **A third Tier-2 vendor lands** (storage, identity-OIDC, payments-retrofit) and
   the substrate pattern needs refinement based on the third worked example.
2. **The Postmark or Turnstile vendor relationship changes** (e.g., switch from
   Postmark to SendGrid). Vendor swap is the substrate value proposition — but if
   the swap surfaces a new pattern requirement, the substrate ADR is amended.
3. **Council rules on any of Q1-Q5** in a way that requires Rev 2 (e.g., approves
   Postmark SDK + amends ADR 0013 §Enforcement; ratifies `IHostSecretResolver` at
   substrate-tier; promotes per-action support to mandatory).
4. **ADR 0052 `IMessagingGateway` retrofit lands** — the marker-interface +
   production-guard retrofit on `IMessagingGateway` would be a separate ADR 0052
   amendment, but cross-references back here.
5. **The mock-first directive is repealed or amended at CIC tier.** Currently
   load-bearing per the 2026-05-25T18:08Z ratification; any change cascades to
   ADR 0096.

## References

- Admiral ruling on 8 halt conditions: `coordination/inbox/admiral-ruling-2026-05-25T1953Z-adr-0096-vendor-provider-substrate-8-halt-conditions.md`
- CIC ratification of Decisions 4 + 5 + mock-first directive: `coordination/inbox/admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`
- Onboarding-ladder Admiral ruling: `coordination/inbox/admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md`
- ONR scaffold (predecessor research): `shipyard/icm/01_discovery/research/onr-adr-0096-vendor-provider-substrate-scaffold.md` (1203 lines; via PR `shipyard#154`)
- ADR 0013 Foundation.Integrations (provider-neutrality precedent): `docs/adrs/0013-foundation-integrations.md`
- ADR 0052 Bidirectional Messaging Substrate (structurally-distinct sibling): `docs/adrs/0052-bidirectional-messaging-substrate.md`
- ADR 0091 ITenantContext Divergence Resolution (R2 / A1 startup-assertion precedent): `docs/adrs/0091-itenantcontext-divergence-resolution.md`
- ADR 0092 Substrate-Tenant-Keyed Repository (Tier-1 sibling): `docs/adrs/0092-substrate-tenant-keyed-repository.md`
- ADR 0094 IAuditEventReader (read-substrate Tier-1 sibling): `docs/adrs/0094-i-audit-event-reader.md`
- ADR 0095 Bootstrap Context (sister substrate; first consumer of `IEmailProvider` + `ICaptchaVerifier`): `docs/adrs/0095-bootstrap-context.md`
- Cerebrum: `feedback_tier2_vendor_mock_first` — canonical Tier-2 substrate discipline (2026-05-25)
- Cerebrum: `feedback_prefer_cleanest_long_term_option` — Option B layering trade rationale
- Cerebrum: `project_fleet_ruleset_config` — fleet ruleset posture (auto-merge fires on CI-green)
- Slotting-architecture recommendation: `shipyard#152` §5 (three-tier slotting; Tier-2 category-provider)
- Postmark API docs: https://postmarkapp.com/developer (forward-watch — referenced by Step 2 PR)
- Cloudflare Turnstile docs: https://developers.cloudflare.com/turnstile/ (forward-watch — referenced by Step 3 PR)
