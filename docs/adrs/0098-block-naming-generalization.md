---
id: 98
title: Block-Naming Generalization for Cross-Vertical Substrate Reuse
status: Proposed
date: 2026-05-26
proposed-date: 2026-05-26
author: Admiral
tier: foundation
pipeline_variant: sunfish-api-change

concern:
  - substrate-naming
  - cross-vertical-reuse
  - open-source-adoption-lock-in
  - tier-1-domain-block-discipline
  - api-contract

enables:
  - flight-deck-media-erp-vertical
  - foundation-agreements-substrate
  - cross-vertical-block-reuse
  - blocks-leases-iagreement-future
  - blocks-brand-deals-future

composes:
  - 8    # Foundation.MultiTenancy (IMustHaveTenant invariant on IAgreement)
  - 18   # Governance + License Posture (open-source adoption lock-in driver)
  - 57   # Leasing-Pipeline + Fair Housing (implementing-block of rename target #6)
  - 59   # Public-Listing Surface (implementing-block of rename target #5)
  - 69   # ADR Authoring Discipline (pre-merge council + §A0 + three-direction)
  - 91   # ITenantContext Divergence Resolution (substrate-tier ADR cadence precedent)
  - 95   # Bootstrap Context substrate (substrate-tier ADR shape mirror)
  - 96   # Tier-2 Vendor-Provider Substrate (substrate-tier ADR shape mirror)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-tier ADR; ADR text carries mandatory dual-council per Halt 10; Step 1 implementation PR carries its own dual-council per the §"Council review" decision below

amendments: []
---

# ADR 0098 — Block-Naming Generalization for Cross-Vertical Substrate Reuse

**Status:** Proposed (Revision 1; awaiting pre-merge dual-council attestation per ADR 0069 + Halt 10 of `coordination/inbox/admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md`)
**Date:** 2026-05-26
**Resolves:** PAO source UPF (`coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`; CIC ratified 2026-05-26T03:42Z) — the block-naming-generalization wave required before open-source adoption locks the Shipyard substrate's property-vertical-specific names. Folds Admiral 10-halt ruling (`coordination/inbox/admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md`) including the two CIC-ratified ONR counter-findings (Halts 2 + 4) that REJECT the `kernel-lease` rename (Flease distributed-lock coordinator; preserve) and the `blocks-tenant-admin` rename (SaaS-tenant admin surface; preserve). Sub-cohort 1 substrate (W79) Stage-05 hand-off + `flight-deck#33` story-structure module Stage-05 authoring remain gated on this ADR's promotion to Accepted alongside ADRs 0095 + 0096.
**Council inputs:** Initial draft; no prior council verdicts. Halt 10 MANDATES dual-council on this Revision 1 text (.NET-architect + security-engineering). The pattern mirrors ADR 0095 R2 and ADR 0096 R2: AMBER amendments (if any) fold into Revision 2; GREEN dual-attest promotes to Accepted.
**Predecessor research:** `shipyard/icm/01_discovery/research/onr-adr-0098-block-naming-generalization-scaffold.md` (562 lines; ONR; via the sibling shipyard PR opened 2026-05-26T04:45Z; ONR status `coordination/inbox/onr-status-2026-05-26T0445Z-block-naming-generalization-scaffold-complete.md`). Scaffold surfaced 10 halt conditions including two BLOCKING semantic counter-findings; Admiral ruling consolidated all 10 dispositions (`admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md`) with CIC override of PAO UPF on Halts 2 + 4.

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-26 | Admiral | Initial draft. Folds ONR scaffold (5 confirmed renames + 1 new substrate package + 1 mandatory Roslyn deprecation analyzer) and Admiral 10-halt ruling. Two BLOCKING ONR counter-findings ratified by CIC override of PAO UPF: `kernel-lease` is the Flease distributed-lock coordinator (NOT a property-domain abstraction; preserve as-is); `blocks-tenant-admin` is the SaaS-tenant admin surface (NOT a counterparty portal; preserve as-is). Six PAO-proposed renames PROCEED. New `packages/foundation-agreements/` substrate package introduces `IAgreement` + `IContractTerm` + `IParty` interfaces + `AgreementStatus` enum (canonical name `foundation-agreements`, not `foundation-contracts`, per Halt 5 RATIFY — avoids cross-language collision with TypeScript `@sunfish/contracts` package + `System.Diagnostics.Contracts` overlap). Per-rename migration pattern: new package at new name + `TypeForwardedTo` re-export shim from old name + `[Obsolete]` deprecation notice + major version bump + archive after one release cycle. Roslyn deprecation analyzer (Step 7) mandatory per Halt 7 RATIFY + `feedback_prefer_cleanest_long_term_option`. Entity-shape generalization OUT OF SCOPE per Halt 6 — Option α (name-rename-only; cross-vertical entity-shape refactor when 2nd-vertical consumer surfaces). Vertical-block parallel-implementation policy: Option α (cross-vertical reuse; `blocks-leases` adopts `IAgreement` post-MVP as exemplar) per Halt 8. Kernel-codename README additions carved OUT of ADR 0098 scope to a separate docs-only PR per Halt 9. Dual-council MANDATORY on ADR text + Step 1 implementation PR per Halt 10; Steps 2-6 carry SPOT-CHECK on the standard rename-and-shim mechanical-renames pattern. Status: Proposed (awaiting dual-council). |

Promotion path: both councils self-attest GREEN via inbox status on Revision 1 → Admiral promotes ADR to `Accepted`. If a council returns AMBER, Admiral folds amendments into Revision 2 (ADR 0095 R2 / 0096 R2 precedent). **Step 1 implementation PR carries its own mandatory dual-council SPOT-CHECK at PR-open** (per Halt 10) — independent council pull on the new-substrate surface. Steps 2-6 (the mechanical renames + `TypeForwardedTo` shims) carry .NET-architect SPOT-CHECK only; Step 5 (`blocks-listings`) additionally carries sec-eng SPOT-CHECK because the W#28 capability-tier surface is touched. Step 7 (analyzer) carries .NET-architect MANDATORY (analyzer-code quality).

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Agreements.IAgreement` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Agreements.IContractTerm` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Agreements.IParty` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Agreements.AgreementStatus` (enum) | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Blocks.RecurringBilling.*` (new namespace + types) | Introduced by this ADR | no — added in Step 2 PR (rename target of `Sunfish.Blocks.RentCollection.*`) |
| `Sunfish.Blocks.WorkItems.*` (new namespace + types) | Introduced by this ADR | no — added in Step 3 PR (rename target of `Sunfish.Blocks.WorkOrders.*`) |
| `Sunfish.Blocks.Reviews.*` (new namespace + types) | Introduced by this ADR | no — added in Step 4 PR (rename target of `Sunfish.Blocks.Inspections.*`) |
| `Sunfish.Blocks.Listings.*` (new namespace + types) | Introduced by this ADR | no — added in Step 5 PR (rename target of `Sunfish.Blocks.PublicListings.*`) |
| `Sunfish.Blocks.AcquisitionPipeline.*` (new namespace + types; includes `OfferTerms` ← `LeaseOffer`) | Introduced by this ADR | no — added in Step 6 PR (rename target of `Sunfish.Blocks.PropertyLeasingPipeline.*`) |
| `Sunfish.Tooling.Analyzers.BlockNameDeprecationAnalyzer` | Introduced by this ADR | no — added in Step 7 PR (Roslyn analyzer) |
| `Sunfish.Kernel.Lease.ILeaseCoordinator` | Existing (preserved per Halt 2 BLOCKING; the Flease distributed-lock coordinator) | yes — `shipyard/packages/kernel-lease/ILeaseCoordinator.cs`; README documents Flease-inspired CP-class lease coordination |
| `Sunfish.Blocks.TenantAdmin.*` | Existing (preserved per Halt 4 BLOCKING; SaaS-tenant admin surface) | yes — `shipyard/packages/blocks-tenant-admin/Sunfish.Blocks.TenantAdmin.csproj`; Description: "tenant profile, users, roles, and bundle-activation surface over IBundleCatalog" |
| `Sunfish.Blocks.Leases.*` | Existing (no rename; cross-vertical `IAgreement` adoption deferred to Step 8 post-MVP per Halt 8 Option α) | yes — `shipyard/packages/blocks-leases/Sunfish.Blocks.Leases.csproj` |
| `Sunfish.Blocks.RentCollection.*` | Existing (rename source for Step 2) | yes — `shipyard/packages/blocks-rent-collection/` |
| `Sunfish.Blocks.WorkOrders.*` | Existing (rename source for Step 3) | yes — `shipyard/packages/blocks-work-orders/` |
| `Sunfish.Blocks.Inspections.*` | Existing (rename source for Step 4) | yes — `shipyard/packages/blocks-inspections/` |
| `Sunfish.Blocks.PublicListings.*` | Existing (rename source for Step 5) | yes — `shipyard/packages/blocks-public-listings/` |
| `Sunfish.Blocks.PropertyLeasingPipeline.*` (includes `LeaseOffer`) | Existing (rename source for Step 6) | yes — `shipyard/packages/blocks-property-leasing-pipeline/` |
| `@sunfish/contracts` (TypeScript package) | Existing (cross-language naming collision baseline; drives `foundation-agreements` choice per Halt 5) | yes — `shipyard/packages/contracts/package.json` |
| ADR 0008 (Foundation.MultiTenancy) | Existing — `IMustHaveTenant` typed-marker precedent for `IAgreement.TenantId` | yes — `shipyard/docs/adrs/0008-foundation-multitenancy.md` |
| ADR 0011 (Bundle Versioning + Upgrade Policy) | Existing — release cadence driving deprecation-window timing | yes — `shipyard/docs/adrs/0011-bundle-versioning-upgrade-policy.md` |
| ADR 0018 (Governance + License Posture) | Existing — open-source adoption lock-in driver | yes — `shipyard/docs/adrs/0018-governance-and-license-posture.md` |
| ADR 0057 (Leasing-Pipeline + Fair Housing) | Existing — implementing-block of rename target #6 (`blocks-property-leasing-pipeline`) | yes — `shipyard/docs/adrs/0057-leasing-pipeline-fair-housing.md` |
| ADR 0059 (Public-Listing Surface) | Existing — implementing-block of rename target #5 (`blocks-public-listings`); W#28 capability-tier promotion lives here | yes — `shipyard/docs/adrs/0059-public-listing-surface.md` |
| ADR 0069 (ADR Authoring Discipline) | Existing — governs pre-merge council + §A0 + three-direction | yes — `shipyard/docs/adrs/0069-adr-authoring-discipline.md` |
| ADR 0091 (ITenantContext Divergence Resolution) | Existing — substrate-tier ADR cadence precedent (helper + assertion + analyzer) | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` |
| ADR 0095 (Bootstrap Context substrate) | Existing — substrate-tier ADR shape mirror | yes — `shipyard/docs/adrs/0095-bootstrap-context.md` |
| ADR 0096 (Tier-2 Vendor-Provider Substrate) | Existing — substrate-tier ADR shape mirror | yes — `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md` |
| Pattern `pattern-009` (Bridge endpoint + frontend rebind pair) | Existing — does NOT trigger on this ADR's downstream surface (renames preserve existing routes; no NEW routes) | yes — `shipyard/_shared/engineering/standing-approved-patterns.md` |
| Three-tier slotting vocabulary | Existing — Tier-1 domain-block discipline applies to all renames + the new substrate | yes — `~/.claude/projects/-Users-christopherwood-Projects-Harborline-Software/memory/project_three_tier_slotting_vocabulary.md` (CIC ratified 2026-05-25) |
| ONR scaffold | Existing | yes — `shipyard/icm/01_discovery/research/onr-adr-0098-block-naming-generalization-scaffold.md` (562 lines; ONR; sibling shipyard PR) |
| Admiral ruling — 10 halt conditions | Existing | yes — `coordination/inbox/admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md` |
| Admiral ruling — PAO routing authorization | Existing | yes — `coordination/inbox/admiral-ruling-2026-05-26T0345Z-pao-routing-block-naming-flight-deck-storymodule.md` |
| PAO source UPF | Existing | yes — `coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md` |

§A0 totals: 32 cited references. Existing & verified: 22. Introduced by this ADR: 10 (4 new substrate types in `foundation-agreements` — `IAgreement` + `IContractTerm` + `IParty` + `AgreementStatus`; 5 renamed namespaces — `RecurringBilling` + `WorkItems` + `Reviews` + `Listings` + `AcquisitionPipeline`; 1 new Roslyn analyzer — `BlockNameDeprecationAnalyzer`).

**§A0 prose note.** The 10 introduced symbols are split across 7 Step PRs: Step 1 ships the new substrate (4 of 10 — `IAgreement` + `IContractTerm` + `IParty` + `AgreementStatus`); Steps 2-6 each ship one renamed namespace (5 of 10); Step 7 ships the deprecation analyzer (1 of 10). Each step PR re-runs §A0 against its own slice. The analyzer-ships-late cadence is the ADR 0091 R2 amendment A2 precedent + ADR 0095 R2 Step 3 analyzer precedent: shipping the analyzer in the same PR as the substrate-shape changes is non-mechanical work that compounds Step 1 scope. The same projected outcome (zero pipeline-mixing regressions during the Steps 2-6 window) applies because the renames are non-breaking-at-source via `TypeForwardedTo` re-export shims; the analyzer at Step 7 then closes the migration window with compile-time warnings on continued use of the deprecated namespaces.

---

## Context

Shipyard is the framework-agnostic application platform underpinning the Harborline-Software fleet. Per ADR 0018 governance posture, Shipyard ships as **MIT-licensed open-source software** targeting BOTH the **property-management ERP vertical** (Sunfish; today) AND the **media-company ERP vertical** (Flight Deck; Phase 1 in scope per PAO UPF `pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`). The dual-vertical thesis is load-bearing: Shipyard's value proposition is "build the substrate once; ship N verticals on top of it." Both verticals consume the same Tier-1 domain-block substrate; only the Tier-1 vertical-block layer carries domain-specific names.

PAO's UPF identified a class of substrate-tier packages whose current names encode property-vertical-specific semantics that block cross-vertical reuse:

- `blocks-rent-collection` reads as "rent for a property" — but the mechanics (recurring invoice + payment + ledger) are equally the mechanics of recurring license fees, recurring subscription billing, recurring brand-deal retainers, recurring royalty distributions.
- `blocks-work-orders` reads as "work order for a property" (field-service maintenance) — but the mechanics (assignable work item + lifecycle + audit) are equally the mechanics of editorial assignments, podcast production tasks, video shoot call-sheets.
- `blocks-inspections` reads as "property inspection" — but the mechanics (template + scheduled assessment + deficiency tracking) are equally the mechanics of editorial reviews, QA gates, compliance audits.
- `blocks-public-listings` reads as "real-estate listing" — but the mechanics (public catalog + tier-redacted projection + capability-tier promotion) are equally the mechanics of marketplace product listings, podcast episode listings, e-book preview listings.
- `blocks-property-leasing-pipeline` reads as "property rental application" — but the mechanics (intake funnel + qualification gates + adverse-action notice) are equally the mechanics of media-rights acquisition pipelines, vendor onboarding funnels, employment-application pipelines.
- A new `foundation-agreements` package introduces `IAgreement` + `IContractTerm` + `IParty` interfaces enabling vertical blocks (`blocks-leases` today; future `blocks-brand-deals`, `blocks-license-agreements`) to share a substrate-tier abstraction.

These names were correct-for-purpose when Sunfish was a single-vertical project. With the dual-vertical thesis ratified and open-source adoption ramping (per ADR 0018), the names must generalize **before** community downstream consumption locks them in. NuGet semantic-versioning + community downstream means every future rename is a breaking-change cost amortized across every consumer; the cost grows monotonically with adoption depth. Per `feedback_prefer_cleanest_long_term_option` (CIC directive 2026-05-21): the +1-2-Engineer-days rename-wave cost is the correct trade against the alternative of incremental renames spread across 3-6 months of substrate-block PR churn as Flight Deck Phase 1 surfaces emerge.

ONR's scaffold (562 lines) audited the 8 packages PAO named. **Two of the 8 PAO-proposed renames are factually wrong.** The scaffold surfaced these as BLOCKING semantic counter-findings:

- **`kernel-lease`** is **NOT** the property-domain "lease" package PAO assumed. Reading `packages/kernel-lease/README.md` + `ILeaseCoordinator.cs` + the package csproj confirms it is the **Flease-inspired distributed lease coordinator** (Lamport / Chubby / Flease lineage) for CP-class record writes per the local-node-architecture paper §6.3 and `sync-daemon-protocol §6`. "Lease" here is the distributed-systems primitive — the bounded-grant exclusive-write-rights resource lock — NOT a property rental agreement. Repo-wide grep finds zero property-domain consumers (`blocks-scheduling`, `kernel-ledger`, `kernel-runtime` are the only consumers; all substrate-tier; none property-domain). Crucially, `blocks-leases` (the property-lease block) has **no ProjectReference to `kernel-lease`** — the two share a name in English but are semantically unrelated. The package is already domain-free in the relevant sense. Renaming it to `kernel-agreement` would destroy the distributed-systems-literature semantic anchor + collide with the new `foundation-agreements` package + confuse package consumers expecting `kernel-agreement` to be an agreement/contract substrate.
- **`blocks-tenant-admin`** is **NOT** the property-tenant-portal package PAO assumed. Reading the csproj Description ("tenant profile, users, roles, and bundle-activation surface over IBundleCatalog") + the package's services (`InMemoryTenantAdminService`, `UpdateTenantProfileRequest`, `InviteTenantUserRequest`) + Razor components (`BundleActivationPanel.razor`) + tests (`BundleActivationPanelTests`, `TenantAdminEntityModuleTests`) confirms this is the **SaaS-tenant admin surface** — the Sunfish-as-SaaS-tenant-owner self-administration surface, analogous to GitHub organization settings or Stripe customer portal. The "tenant" is the multi-tenant SaaS-platform tenant (the Sunfish-customer-org), NOT a property counterparty (renter / brand-deal counterparty). Renaming it to `blocks-party-portal` would destroy the correct-for-purpose SaaS-tenant connotation + imply a counterparty-self-service surface this block does NOT ship + conflate with the W#28 `blocks-public-listings` capability-tier-promotion surfaces.

The Admiral 10-halt ruling (`admiral-ruling-2026-05-26T0500Z`) RATIFIED both ONR counter-findings via CIC override of the PAO UPF (Halts 2 + 4). The remaining 6 PAO-proposed renames hold up; the net ADR 0098 scope is **5 confirmed renames + 1 new substrate package + 1 mandatory Roslyn deprecation analyzer + 1 mandatory deferred docs-only PR carve-out** (Halt 9). The scaffold's other 6 non-blocking halts (1, 3, 5, 6, 7, 8) were all RATIFY-ONR; Halt 10 codified dual-council cadence (MANDATORY on ADR text + Step 1; SPOT-CHECK on Steps 2-6; MANDATORY on Step 7 analyzer).

This ADR is **substrate-tier**. The first downstream consumers are the existing vertical blocks (`blocks-leases` Option α adoption per Halt 8 deferred to Step 8 post-MVP) + the sunfish desktop + signal-bridge consumer-update PRs (each Engineer-authored downstream of this ADR's promotion to Accepted). `flight-deck#33` (story-structure module Stage-05 hand-off) and W79 sub-cohort 1 Stage-05 hand-off authoring both remain gated on this ADR's promotion alongside ADRs 0095 + 0096.

## Decision drivers

**D1 — Open-source adoption lock-in is irreversible.** Per ADR 0018 governance posture, Shipyard ships MIT-licensed; community downstream consumers will land soon. Once substrate names ship in tagged NuGet releases consumed by community projects, every rename forces every downstream consumer to a migration. The window to rename without imposing downstream cost is NOW. PAO's UPF correctly identifies this; the Admiral routing ruling (`admiral-ruling-2026-05-26T0345Z`) ratified the urgency.

**D2 — Cross-vertical reuse is the substrate value proposition.** Per the dual-vertical thesis (Sunfish-property + Flight-Deck-media on shared Shipyard substrate), successful execution requires the substrate to be domain-generic at the layer below the vertical blocks. The 5 block renames + 1 new foundation package execute this principle concretely. Without these renames, every Flight Deck developer would pay a continuous cognitive translation cost ("the recurring-billing concept is in `blocks-rent-collection`?") and every cross-vertical refactor would carry friction.

**D3 — Substrate-tier discipline (ADR 0091 R2 + ADR 0095 R2 + ADR 0096 R2 precedent).** Substrate-tier ADRs follow a "principle + audit + per-package application + DI-helper/assertion/analyzer cadence" shape. ADR 0098 follows the same shape: state the principle (substrate names must be domain-generic at the layer below vertical blocks), audit per-package (8 candidates from PAO UPF → 6 confirmed + 2 BLOCKING-preserve), specify per-package migration (new package at new name + `TypeForwardedTo` shim + `[Obsolete]` + version bump + deprecation analyzer), ship per-Step PRs (Step 1 substrate + Steps 2-6 renames + Step 7 analyzer).

**D4 — Semantic precision wins over naming-pattern uniformity (Halts 2 + 4 BLOCKING counter-findings).** Substrate-tier renames are filesystem-grounded substrate archaeology, not naming-pattern application. PAO's UPF correctly identified the principle (substrate names must be domain-generic) but applied it to two packages where the principle was already satisfied (kernel-lease is domain-free in the distributed-systems-primitive sense; blocks-tenant-admin is correct-for-purpose in the SaaS-tenant-admin sense). ONR's pre-Rev-1 counter-finding caught both. The lesson — **PAO's UPFs are strategic + naming-pattern-focused; ONR's scaffolds add filesystem-grounded substrate-archaeology** — is captured by the Admiral 10-halt ruling. ADR 0098 honors both ONR counter-findings by REJECTING both renames at ADR text time.

**D5 — Cleanest long-term option per `feedback_prefer_cleanest_long_term_option`** (CIC directive 2026-05-21). When choosing between ship-fast convention and substrate-correct cleanest path, ALWAYS pick the cleanest long-term option. Applied to ADR 0098: (a) ship the new `foundation-agreements` substrate Step 1 + the 5 renames + the deprecation analyzer + the re-export shims **now** (one focused 1-2 Engineer-day wave) rather than incremental renames spread across 3-6 months of substrate-block PR churn; (b) mandate the Roslyn deprecation analyzer (Step 7) rather than leaving `[Obsolete]` alone to do the work (the analyzer's compile-warning is louder + CI-enforceable in `WarningsAsErrors` mode); (c) name the new package `foundation-agreements` rather than `foundation-contracts` to pre-empt the cross-language collision with the existing TypeScript `@sunfish/contracts` package + the `System.Diagnostics.Contracts` namespace overlap + the .NET "contract-first programming" community convention.

**D6 — Tier-1 domain-block discipline per the three-tier slotting vocabulary** (CIC ratified 2026-05-25). The 5 renames are all Tier-1 domain-blocks (concrete DI; never swapped at runtime); the new `foundation-agreements` package sits at the foundation tier between kernel (domain-free distributed-systems primitives) and Tier-1 domain-blocks (vertical-specific consumers). Tier-2 (category-provider; bounded vendor swap; ADR 0096) and Tier-3 (capability-plugin; runtime swap; Flight Deck surface) are unaffected by this ADR.

## Considered options

**Option A — Status quo (no rename).** Leave all package names as-is; let community adoption lock the property-vertical-specific names; refactor vertical-by-vertical when Flight Deck Phase 1+ encounters specific name pain. **REJECTED** per CIC ratification of the PAO UPF (2026-05-26T03:42Z) and Admiral routing ruling (`admiral-ruling-2026-05-26T0345Z`). Per D1, this is exactly the trap the rename wave avoids: NuGet semantic-versioning + community downstream means every rename in the future is a breaking-change cost amortized across every downstream consumer; the longer the rename waits, the more expensive it gets. Per D2, the cognitive translation cost on Flight Deck vertical onboarding is continuous + compounding.

**Option B — Per-vertical foundation packages (foundation-property-contracts + foundation-media-contracts).** Each vertical gets its own foundation package; no shared substrate. **REJECTED** per D2. This defeats the entire premise of cross-vertical substrate reuse: Shipyard's value proposition is "build once, ship to N verticals." If every vertical has its own foundation, Shipyard is a starter-kit, not a substrate. Forces N parallel package-maintenance burdens. Negates the dual-vertical thesis.

**Option C — In-place renames without re-export wrappers.** Each rename is a hard-break in a single PR: old package deleted, new package live, downstream MUST update in the same window. **REJECTED** per D5 cleanest-long-term-option discipline. Hard-break renames are a hostile breaking-change experience for open-source downstream consumers (per D1 governance posture). The re-export-shim cost (~30 min per shim) is trivial vs the downstream-update friction it averts. The deployment-cliff problem (sunfish desktop + signal-bridge MUST update on the exact same release as Shipyard or they get build errors) is solved by `TypeForwardedTo` shims at near-zero cost.

**Option D — Rename `kernel-lease` to `kernel-agreement`.** PAO UPF Rename 1 (per `pao-status-2026-05-25T2330Z`). **REJECTED** per Halt 2 BLOCKING (ONR Finding 1 ratified by CIC override). `kernel-lease` is the Flease distributed-lock coordinator (Lamport / Chubby / Flease lineage), NOT a property-domain abstraction. The package is already domain-free in the relevant sense (distributed-systems primitive). Renaming would (a) destroy the distributed-systems-literature semantic anchor (Flease + Lamport + Chubby), (b) collide with the new `foundation-agreements` package introduced by this ADR (which IS the agreements/contracts substrate), (c) confuse package consumers who would expect `kernel-agreement` to be an agreements/contracts substrate.

**Option E — Rename `blocks-tenant-admin` to `blocks-party-portal`.** PAO UPF Rename 7. **REJECTED** per Halt 4 BLOCKING (ONR Finding 2 ratified by CIC override). `blocks-tenant-admin` is the SaaS-tenant admin surface (the Sunfish-customer-org owner self-administration surface; GitHub-org-settings analog), NOT a counterparty (renter / brand-deal-counterparty) portal. Renaming would (a) destroy the correct-for-purpose SaaS-tenant connotation, (b) imply a counterparty-self-service surface this block does NOT ship, (c) overlap conceptually with the W#28 `blocks-public-listings` capability-tier-promotion surfaces, (d) create confusion with the multi-tenancy substrate (`Foundation.MultiTenancy` per ADR 0008). If a counterparty-portal surface is needed for either vertical (renter-portal for property; creator-portal for media), the correct response is to author a NEW block (`blocks-counterparty-portal` or vertical-specific: `blocks-renter-portal`, `blocks-creator-portal`) — that is downstream-vertical-Stage-05 territory, NOT ADR 0098 scope.

**Option F — Name the new substrate `foundation-contracts` (with `IContract` interface).** PAO UPF naming. **REJECTED** per Halt 5 RATIFY-ONR. The existing TypeScript `@sunfish/contracts` package (`shipyard/packages/contracts/package.json`) ships at the same name; while a `.NET` package `Sunfish.Foundation.Contracts` would not collide at runtime (different ecosystems), the documentary collision is real (a developer reading "the contracts package" must disambiguate). The `.NET` `System.Diagnostics.Contracts` namespace ships in BCL — code-by-contract assertion attributes (`Contract.Requires`, `Contract.Ensures`); namespace overlap. The .NET "contract-first programming" community convention means design-by-contract; introducing `Sunfish.Foundation.Contracts` would create three-way community-convention ambiguity. The recommended `foundation-agreements` (with `IAgreement` interface) avoids all three collisions and matches the dominant interface name from PAO's UPF.

**Option G — Entity-shape generalization within ADR 0098 scope.** Refactor the renamed blocks' entity shapes to be cross-vertical-generic at ADR 0098 ratification time (e.g., remove `EquipmentConditionAssessment` from `blocks-reviews`; remove FHA-quarantined `DemographicProfile` from `blocks-acquisition-pipeline`). **REJECTED** per Halt 6 RATIFY-ONR (Option α — name-rename-only). Pre-emptive entity-shape generalization risks over-abstracting for hypothetical needs (the "premature abstraction" smell). Entity-shape divergence is best deferred until a 2nd-vertical consumer surfaces with concrete divergence requirements; until then, vertical-domain-specific fields are pragmatically retained inside renamed-but-substrate-correct package boundaries. ADR 0098 covers name-only renames; entity-shape generalization is a separate per-block decision when a 2nd-vertical consumer surfaces.

**Option H — Carve kernel-codename README additions into ADR 0098 scope.** Author READMEs for the fleet-codename packages (`blocks-quarterdeck`, `blocks-sick-bay`, `blocks-engine-room`, etc.) inside this ADR. **REJECTED** per Halt 9 RATIFY-ONR. ADR 0098 is substrate-tier rename scope; codename-README is docs-only hygiene. Carving it out keeps ADR 0098 focused. ONR can author the codename READMEs as a follow-on deliverable post-ADR-Accept (~3-4h Sonnet medium subagent dispatch).

## Decision

**Block-Naming Generalization: 5 renames + 1 new substrate package + 1 mandatory Roslyn deprecation analyzer; 2 PAO-proposed renames REJECTED at ADR text time per CIC-ratified ONR counter-findings; entity-shape generalization out of scope (Option α).**

The decision folds the Admiral 10-halt ruling (`admiral-ruling-2026-05-26T0500Z`) verbatim. Per-halt disposition table:

| # | Halt subject | Disposition | Rationale |
|---|---|---|---|
| H1 | ADR number assignment | **RATIFY-ONR** (0098) | 0097 reserved for PasswordHasher H8 follow-on; 0098 is next-available. |
| H2 | `kernel-lease` rename | **OVERRIDE-PAO; RATIFY-ONR-COUNTER-FINDING (DO NOT RENAME)** | BLOCKING semantic. `kernel-lease` is the Flease distributed-lock coordinator (Lamport / Chubby / Flease lineage); already domain-free in the distributed-systems-primitive sense; `blocks-leases` has no ProjectReference to `kernel-lease`; the rename would destroy the distributed-systems-literature semantic anchor + collide with the new `foundation-agreements` package + confuse consumers. CIC override of PAO UPF ratified 2026-05-26T04:55Z. |
| H3 | `IAgreement` vs `IContract` interface naming | **RATIFY-ONR** (`IAgreement`) | "Contract" collides with `System.Diagnostics.Contracts` BCL namespace + general .NET contract-design terminology + the existing TypeScript `@sunfish/contracts` package; `IAgreement` is semantically precise + collision-free. |
| H4 | `blocks-tenant-admin` rename | **OVERRIDE-PAO; RATIFY-ONR-COUNTER-FINDING (DO NOT RENAME)** | BLOCKING semantic. `blocks-tenant-admin` is the SaaS-tenant admin surface (GitHub-org-settings analog); the "tenant" is multi-tenant SaaS-platform tenant, NOT property counterparty; renaming to `blocks-party-portal` would destroy correct-for-purpose meaning + imply a counterparty-self-service surface this block does NOT ship + conflate with multi-tenancy substrate. If counterparty-portal needed, author a NEW block (`blocks-counterparty-portal` or vertical-specific). CIC override of PAO UPF ratified 2026-05-26T04:55Z. |
| H5 | `foundation-contracts` vs `foundation-agreements` | **RATIFY-ONR** (`foundation-agreements`) | The TypeScript `@sunfish/contracts` package already exists; cross-language naming collision; `System.Diagnostics.Contracts` overlap; `foundation-agreements` matches the dominant `IAgreement` interface name. |
| H6 | Entity-shape generalization scope | **RATIFY-ONR** (Option α: name-only) | Renames are name-only; entity-shape generalization is post-MVP per-block work refactored when a 2nd-vertical consumer needs polymorphic handling. Pre-emptive generalization risks premature abstraction. |
| H7 | Roslyn deprecation analyzer mandatoriness | **RATIFY-ONR** (MANDATORY) | Per `feedback_prefer_cleanest_long_term_option`: deprecated-name warning at compile time prevents silent uses of old names during migration window. ADR 0091 R2 amendment A2 + ADR 0095 R2 Step 3 analyzer precedent confirms the pattern is fleet-standard. |
| H8 | Vertical-block parallel-implementation policy | **RATIFY-ONR** (Option α — cross-vertical reuse) | Vertical blocks (`blocks-leases`, future `blocks-brand-deals`) implement `IAgreement` from `foundation-agreements`; cross-vertical reuse via substrate inheritance. Step 8 (post-MVP slack-window) delivers `blocks-leases` `IAgreement` adoption as cross-vertical-reuse exemplar; downstream-vertical-Stage-05 territory for `blocks-brand-deals`. |
| H9 | Kernel-codename README addition scope | **RATIFY-ONR** (separate docs-only PR) | ADR 0098 is substrate-tier rename scope; codename-README is docs-only hygiene. Carving it out keeps ADR 0098 focused; codename READMEs route as a follow-on docs-only PR (~3-4h Sonnet medium subagent dispatch). |
| H10 | Council review timing per Step | **RATIFY-ONR** (dual-council MANDATORY on ADR text + Step 1; SPOT-CHECK on Steps 2-6 + sec-eng on Step 5; MANDATORY on Step 7 analyzer) | Substrate-tier ADRs warrant ADR 0096-style Halt-3 OVERRIDE for the ADR text + the new-substrate Step 1; the remaining mechanical-rename Steps carry standard SPOT-CHECK cadence (with sec-eng SPOT-CHECK on Step 5 because the W#28 capability-tier surface is touched); the analyzer Step 7 carries .NET-architect MANDATORY for analyzer-code quality. |

**Net scope after CIC-ratified halt rulings:**

| # | Change | Disposition | Layer |
|---|---|---|---|
| 1 | NEW `foundation-agreements` package with `IAgreement` + `IContractTerm` + `IParty` + `AgreementStatus` | **PROCEED** | Foundation (Tier-1 substrate) |
| 2 | ~~`kernel-lease` → `kernel-agreement`~~ | **REJECTED** | Kernel (Flease distributed-lock coordinator; preserve as-is) |
| 3 | `blocks-rent-collection` → `blocks-recurring-billing` | **PROCEED** | Block (Tier-1 domain-block) |
| 4 | `blocks-work-orders` → `blocks-work-items` | **PROCEED** | Block |
| 5 | `blocks-inspections` → `blocks-reviews` | **PROCEED** | Block |
| 6 | `blocks-public-listings` → `blocks-listings` | **PROCEED** | Block |
| 7 | ~~`blocks-tenant-admin` → `blocks-party-portal`~~ | **REJECTED** | Block (SaaS-tenant admin surface; preserve as-is) |
| 8 | `blocks-property-leasing-pipeline` → `blocks-acquisition-pipeline` | **PROCEED** | Block |
| 9 | Roslyn `BlockNameDeprecationAnalyzer` (MANDATORY per H7) | **PROCEED** | Tooling (analyzer) |
| 10 | Kernel-codename READMEs | **CARVE-OUT** (separate docs-only PR) | Docs-only (post-Accept) |

**Final ADR 0098 scope:** 5 renames + 1 new substrate package + 1 Roslyn deprecation analyzer + 1 deferred docs-only PR carve-out.
