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

TBD

## Alternatives considered (rejections)

TBD

## Consequences

TBD

## Open questions (forwarded to council)

TBD

## Revisit triggers

TBD

## References

TBD
