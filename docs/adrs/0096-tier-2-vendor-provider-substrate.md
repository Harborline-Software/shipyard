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

TBD

## Decision drivers

TBD

## Considered options

TBD

## Decision

TBD-summary-bullets

## Substrate / layering notes

TBD

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
