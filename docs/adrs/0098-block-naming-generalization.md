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

**Status:** Proposed (Revision 2; awaiting pre-merge dual-council re-attestation per ADR 0069 + Halt 10. Revision 1 dual-AMBER from `.NET-architect` (12 amendments) + `security-engineering` (5 amendments) folded per Admiral pre-rulings; see Revision history below).
**Date:** 2026-05-26
**Resolves:** PAO source UPF (`coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`; CIC ratified 2026-05-26T03:42Z) — the block-naming-generalization wave required before open-source adoption locks the Shipyard substrate's property-vertical-specific names. Folds Admiral 10-halt ruling (`coordination/inbox/admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md`) including the two CIC-ratified ONR counter-findings (Halts 2 + 4) that REJECT the `kernel-lease` rename (Flease distributed-lock coordinator; preserve) and the `blocks-tenant-admin` rename (SaaS-tenant admin surface; preserve). Sub-cohort 1 substrate (W79) Stage-05 hand-off + `flight-deck#33` story-structure module Stage-05 authoring remain gated on this ADR's promotion to Accepted alongside ADRs 0095 + 0096.
**Council inputs:** Revision 1 dual-AMBER (.NET-architect 2026-05-26T10:47Z + security-engineering 2026-05-26T10:46Z); 17 amendments folded into Revision 2 per the Admiral pre-rulings P1–P5 (see Revision history). Halt 10 MANDATES dual-council on this Revision 2 text. The pattern mirrors ADR 0095 R2 and ADR 0096 R2: AMBER amendments folded into Revision 2; GREEN dual-attest promotes to Accepted.
**Predecessor research:** `shipyard/icm/01_discovery/research/onr-adr-0098-block-naming-generalization-scaffold.md` (562 lines; ONR; via the sibling shipyard PR opened 2026-05-26T04:45Z; ONR status `coordination/inbox/onr-status-2026-05-26T0445Z-block-naming-generalization-scaffold-complete.md`). Scaffold surfaced 10 halt conditions including two BLOCKING semantic counter-findings; Admiral ruling consolidated all 10 dispositions (`admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md`) with CIC override of PAO UPF on Halts 2 + 4.

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-26 | Admiral | Initial draft. Folds ONR scaffold (5 confirmed renames + 1 new substrate package + 1 mandatory Roslyn deprecation analyzer) and Admiral 10-halt ruling. Two BLOCKING ONR counter-findings ratified by CIC override of PAO UPF: `kernel-lease` is the Flease distributed-lock coordinator (NOT a property-domain abstraction; preserve as-is); `blocks-tenant-admin` is the SaaS-tenant admin surface (NOT a counterparty portal; preserve as-is). Six PAO-proposed renames PROCEED. New `packages/foundation-agreements/` substrate package introduces `IAgreement` + `IContractTerm` + `IParty` interfaces + `AgreementStatus` enum (canonical name `foundation-agreements`, not `foundation-contracts`, per Halt 5 RATIFY — avoids cross-language collision with TypeScript `@sunfish/contracts` package + `System.Diagnostics.Contracts` overlap). Per-rename migration pattern: new package at new name + `TypeForwardedTo` re-export shim from old name + `[Obsolete]` deprecation notice + major version bump + archive after one release cycle. Roslyn deprecation analyzer (Step 7) mandatory per Halt 7 RATIFY + `feedback_prefer_cleanest_long_term_option`. Entity-shape generalization OUT OF SCOPE per Halt 6 — Option α (name-rename-only; cross-vertical entity-shape refactor when 2nd-vertical consumer surfaces). Vertical-block parallel-implementation policy: Option α (cross-vertical reuse; `blocks-leases` adopts `IAgreement` post-MVP as exemplar) per Halt 8. Kernel-codename README additions carved OUT of ADR 0098 scope to a separate docs-only PR per Halt 9. Dual-council MANDATORY on ADR text + Step 1 implementation PR per Halt 10; Steps 2-6 carry SPOT-CHECK on the standard rename-and-shim mechanical-renames pattern. Status: Proposed (awaiting dual-council). |
| 2 | 2026-05-26 | Admiral | Dual-AMBER fold. .NET-architect council 2026-05-26T10:47Z returned AMBER with 12 amendments (A1–A12; 9 load-bearing + 3 sentence-level); security-engineering council 2026-05-26T10:46Z returned AMBER with 5 amendments (S1–S5). All 17 amendments folded per Admiral pre-rulings (P1: A1 RATIFY — `IAgreement` extends `IMustHaveTenant` via marker chain; P2: A3 RATIFY — Step 7 analyzer ships in NEW `packages/foundation-block-naming.analyzers/`, NOT extending `foundation-wayfinder-analyzers`; P3: A4 RATIFY — diagnostic ID `SUNFISH_BLOCKDEP001` per fleet `SUNFISH_<AREA>NNN` convention; P4: A9 RATIFY — Step 1 Shape α — interfaces+enum only + ZERO external `PackageReferences` + ONE `ProjectReference` to `foundation-multitenancy` per A1; P5: S5 RATIFY — Step 6 sec-eng SPOT-CHECK ADD because FHA + FCRA compliance surface — `DemographicProfile`, `AdverseActionNotice`, `BackgroundCheckResult` — is materially access-control + audit + redaction relevant). Substantive sec-eng folds: S1 `IParty.DisplayName` PII discipline lifted to substrate-tier minimum-floor (audit-log treatment + tier-redacted projection + logging discipline); S2 Step 5 W#28 capability-tier invariant minimum-floors enumerated (type-name preservation + macaroon caveat-name preservation + audit-payload-discriminator preservation + Defense bit-equivalent preservation); S3 deprecation-window 90-day archive cap pinned + CVE cross-reference discipline; S4 Step 7 analyzer cross-language scope (TypeScript-side parallel deprecation via Step 7b ESLint rule; cross-language ProjectReference graph audit; route-path preservation). Substantive .NET-arch folds: A2 Step 6 cross-reference table completed (`LeaseOfferId` → `OfferTermsId` + `LeaseOfferStatus` → `OfferTermsStatus`); A5 `TypeForwardedTo` cookbook completion (public-only forwarding + `TypeForwards.cs` single-file convention + `GenerateAssemblyInfo=false` + `InternalsVisibleTo` audit); A6 extension-method forwarding via `[Obsolete] + [EditorBrowsable(Never)]` stub; A7 collection types pinned (`IReadOnlyList<IParty>` + `IReadOnlyList<IContractTerm>`). Advisory + sentence-level folds: A8 `IAgreementMustHaveParties` deferred to Step 8 (informational); A10 signal-bridge consumer-update sentence (~10+ direct consumers); A11 Risk R2 + Q3 downgraded — verified-clean grep of ADR 0057 (zero `LeaseOffer` references); A12 Step 1 `foundation-multitenancy` ProjectReference dep noted in parallelism analysis. Status: Proposed (awaiting dual-council re-attestation). |

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
| `Sunfish.Foundation.BlockNaming.Analyzers.BlockNameDeprecationAnalyzer` (`SUNFISH_BLOCKDEP001`) | Introduced by this ADR (analyzer ships in NEW `packages/foundation-block-naming.analyzers/` per A3; diagnostic ID per A4) | no — added in Step 7 PR (Roslyn analyzer) |
| `Sunfish.Foundation.MultiTenancy.IMustHaveTenant` (typed marker; `IAgreement` extends this per A1) | Existing — typed marker interface; `TenantId` inherited via `IMustHaveTenant : ITenantScoped` chain | yes — `shipyard/packages/foundation-multitenancy/ITenantScoped.cs` declares `IMustHaveTenant : ITenantScoped`; `TenantQueryFilterExtensions.WhereTenant<T>() where T : IMustHaveTenant` is the canonical repo-wide constraint pattern |
| `packages/foundation-block-naming.analyzers/` (NEW dedicated analyzer host package per A3) | Introduced by this ADR | no — added in Step 7 PR (matches `foundation-ships-office.analyzers` convention; netstandard2.0) |
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

§A0 totals: 34 cited references. Existing & verified: 23 (adds `IMustHaveTenant` typed-marker dep per A1). Introduced by this ADR: 11 (4 new substrate types in `foundation-agreements` — `IAgreement` + `IContractTerm` + `IParty` + `AgreementStatus`; 5 renamed namespaces — `RecurringBilling` + `WorkItems` + `Reviews` + `Listings` + `AcquisitionPipeline`; 1 new Roslyn analyzer — `BlockNameDeprecationAnalyzer` in NEW host package `packages/foundation-block-naming.analyzers/` per A3 + A4).

**§A0 prose note.** The 11 introduced symbols are split across 7 Step PRs: Step 1 ships the new substrate (4 of 11 — `IAgreement` + `IContractTerm` + `IParty` + `AgreementStatus`); Steps 2-6 each ship one renamed namespace (5 of 11); Step 7 ships the deprecation analyzer + its new host package (2 of 11 — `BlockNameDeprecationAnalyzer` + `packages/foundation-block-naming.analyzers/`). Each step PR re-runs §A0 against its own slice. The analyzer-ships-late cadence is the ADR 0091 R2 amendment A2 precedent + ADR 0095 R2 Step 3 analyzer precedent: shipping the analyzer in the same PR as the substrate-shape changes is non-mechanical work that compounds Step 1 scope. The same projected outcome (zero pipeline-mixing regressions during the Steps 2-6 window) applies because the renames are non-breaking-at-source via `TypeForwardedTo` re-export shims; the analyzer at Step 7 then closes the migration window with compile-time warnings on continued use of the deprecated namespaces. Per A3, the analyzer ships in a NEW dedicated host package (`foundation-block-naming.analyzers`) matching the `foundation-ships-office.analyzers` per-substrate-analyzer-package convention, NOT extending the unrelated `foundation-wayfinder-analyzers` (which is scoped to Wayfinder/AtlasSchemaDescriptor per ADR 0065).

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

## Substrate / layering notes

**`foundation-agreements` substrate-tier slotting.** The new `foundation-agreements` package sits at the foundation layer between the kernel (domain-free distributed-systems primitives — `kernel-lease` / `kernel-ledger` / `kernel-runtime`) and Tier-1 domain-blocks (vertical-specific consumers — `blocks-leases` today; future `blocks-brand-deals`, `blocks-license-agreements`). The substrate carries:

- `IAgreement : IMustHaveTenant` — the cross-vertical abstraction; EXTENDS `Sunfish.Foundation.MultiTenancy.IMustHaveTenant` (typed-marker composition per A1 + Admiral pre-ruling P1); `TenantId` is inherited via the marker chain (`IMustHaveTenant : ITenantScoped`), NOT declared locally. The interface adds 7 properties (`AgreementId`, `Parties`, `Terms`, `Status`, `CreatedAt`, `ActivatedAt`, `TerminatedAt`); `TenantId` flows from the marker. Per A7 + Admiral cleanest-long-term-option discipline: `Parties` is `IReadOnlyList<IParty>` and `Terms` is `IReadOnlyList<IContractTerm>` (deterministic ordering; `Parties[0]` is the primary counterparty by convention — lessor / brand / licensor; vertical-defined sort on `Terms`).
- `IContractTerm` — a single bound term within an agreement (vertical-defined `TermType` marker); equivalent across vertical instances of agreement.
- `IParty` — a participant in an agreement (vertical-defined `Role` marker — lessor / lessee / brand / creator / licensor / licensee / employer / employee / vendor / customer); 3 properties (`PartyId`, `Role`, `DisplayName`). `DisplayName` is PII at vertical adoption time; see §"PII discipline on `IParty.DisplayName`" below.
- `AgreementStatus` — 4-stage lifecycle enum (Draft → PendingSignature → Active → Terminated). Per-vertical refinements (lease has SignedButNotYetCommenced; brand-deal has Negotiating-then-PendingSignature) modeled as vertical-block sub-states without altering the substrate enum.

**Why this substrate sits at the foundation layer, not the kernel layer.** Kernel-tier substrate is **domain-free** (distributed-systems primitives + persistence primitives + audit emission primitives — `kernel-lease` is the canonical example; the Flease distributed-lock coordinator carries zero domain semantics). Foundation-tier substrate carries **domain-generic abstractions** that vertical blocks implement — the substrate has domain meaning (an "agreement" is a domain concept) but is generic across all verticals that have the concept. The three-tier slotting vocabulary (CIC ratified 2026-05-25) names this tier explicitly; ADR 0098 introduces `foundation-agreements` as a foundation-tier substrate alongside the existing `foundation-bootstrap` (ADR 0095) and `foundation-integrations` (ADRs 0013, 0096) packages. The renamed Tier-1 domain-blocks (`blocks-recurring-billing`, `blocks-work-items`, `blocks-reviews`, `blocks-listings`, `blocks-acquisition-pipeline`) sit ABOVE the foundation layer and consume foundation-tier substrate where appropriate. The relationship is: `blocks-leases` implements `IAgreement` from `foundation-agreements`; both ARE tier-1 substrate but at different sublayers (foundation = generic; block = vertical-specific).

**Why `TenantId` flows from `IMustHaveTenant` (typed-marker composition, NOT property redeclaration).** Per A1 (`.NET`-architect amendment) + Admiral pre-ruling P1 RATIFY: `IAgreement` EXTENDS `Sunfish.Foundation.MultiTenancy.IMustHaveTenant`; the `TenantId` property is inherited from the marker chain (`IMustHaveTenant : ITenantScoped`), NOT declared locally on `IAgreement`. This honors the multi-tenancy invariant **by composition** rather than redeclaration. Verified by reading `packages/foundation-multitenancy/ITenantScoped.cs`: `IMustHaveTenant` is a TYPED MARKER INTERFACE (zero properties of its own; `TenantId` comes from `ITenantScoped`); the repo-wide canonical constraint pattern is `WhereTenant<T>() where T : IMustHaveTenant` (per `packages/foundation-multitenancy/TenantQueryFilterExtensions.cs`). Extending the marker preserves the `WhereTenant<T>()` constraint pattern for any consumer that wants tenant-filtered Agreement queries (e.g., `query.WhereTenant<Lease>(tenantId)` works once `Lease : IAgreement : IMustHaveTenant`). Future Step 1 PR may add a typed-marker analog (`IAgreementMustHaveParties` for "at least 2 Parties" or similar) per the `IMustHaveTenant` precedent; per A8 (`.NET`-architect advisory) this is DEFERRED to Step 8 (post-MVP) when a 2nd-vertical consumer surfaces — premature without a concrete materialization. Per A12: Step 1 carries an inbound ProjectReference to `Sunfish.Foundation.MultiTenancy` (the marker extension dep); see §Implementation roadmap §Parallelism for the restore-graph implication.

**PII discipline on `IParty.DisplayName` (substrate-tier minimum-floor per S1).** Per S1 (security-engineering amendment): `IParty` carries 3 properties (`PartyId`, `Role`, `DisplayName`); the substrate is deliberately thin (no `Email`, `TaxId`, `Address`, or other directly-PII-bearing fields), but `DisplayName` IS PII in every realistic vertical implementation — vertical adopters populate it with renter names, brand-deal contact names, licensor/licensee identities, employer/employee identities. The substrate ratifies the following discipline at substrate-tier minimum-floor; vertical-block implementations of `IAgreement` MUST honor (Step 8 adoption SHALL verify):

- **(S1a) Audit-log treatment.** Audit emissions for `IAgreement`-implementing aggregates (via `IAuditEmitter`) SHALL ship `IParty.DisplayName` only when an explicit per-vertical `IPartyAuditRedactionPolicy` is registered against the audit emitter; default-deny on absence. The `PublicListingAuditEmitter.cs` W#28 capability-tier-promotion discipline (per ADR 0059) is the precedent — audit payloads ship tier-redacted by default, full-PII payloads only when an explicit policy registration permits.
- **(S1b) Tier-redacted projection.** When `IAgreement` is rendered to a non-counterparty observer (e.g., a property manager viewing a brand-deal-counterparty Agreement from the brand side; OR a creator viewing a brand-deal-counterparty Agreement from the brand side), vertical implementations SHALL provide an `IPartyProjector` mapping `(IParty, IObserverContext) → IPartyView` that the rendering surface invokes; absence of an `IPartyProjector` registration produces a `PartyProjectorMissingException` at first render-time rather than silently shipping unredacted PII. The ADR 0059 tier-redacted-projection-on-`Listing` precedent applies here analogously.
- **(S1c) Logging discipline.** Substrate-tier code paths emitting structured logs SHALL log `IParty.PartyId` (opaque identifier) by default; `IParty.DisplayName` SHALL be opt-in via per-call `LogPartyDisplayName(...)` extension that emits to a redacted log sink configured per the deployment posture. The `RecaptchaV3CaptchaVerifier.cs` reCAPTCHA-secret-in-form-not-URL discipline is the precedent — secrets/PII never log verbatim through standard `ILogger`.

Steps 2-6 SPOT-CHECKs verify the floors hold across each renamed block's existing audit + logging discipline; Step 8 deferred-adoption work for `blocks-leases.Lease → IAgreement` materializes the floors against the existing lessor/lessee PII surface.

**Why the cross-language collision with `@sunfish/contracts` (TS) matters.** The Shipyard fleet ships shared TypeScript types via `packages/contracts/` (`@sunfish/contracts` package per its package.json: "Shared TypeScript interface definitions for the Sunfish ERPNext property management stack"). Naming a parallel .NET substrate `Sunfish.Foundation.Contracts` would not collide at runtime (different ecosystems) but would create a continuous documentary disambiguation cost ("the contracts package — which one?"). The recommended `foundation-agreements` substrate name + `IAgreement` interface name avoid the collision cleanly while matching the dominant semantic concept (an agreement IS what each vertical block implements). The same rationale applies to the `System.Diagnostics.Contracts` BCL namespace + the .NET "contract-first programming" community convention (Eiffel-style design-by-contract assertions). All three collisions stack to argue against `foundation-contracts`; the `foundation-agreements` name avoids all three. Halt 5 RATIFY codifies the choice.

**Interaction with ADR 0008 (Foundation.MultiTenancy).** `IAgreement.TenantId` honors the canonical multi-tenancy invariant. Step 1 PR's `IAgreement` interface declares the property with xmldoc citing ADR 0008 + the `IMustHaveTenant` precedent. No ADR 0008 amendment needed.

**Interaction with ADR 0091 R2 (ITenantContext Divergence Resolution).** ADR 0091 R2's `ITenantContext` facade-vs-narrowed posture affects how consumers of the renamed blocks inject tenant context. The renames preserve consumption sites; per `feedback_itenantcontext_consumption_qualification` (memory 2026-05-22), consumers continue to inject the Authorization sum-interface facade until ADR 0091 Step 3 narrows. ADR 0098 does NOT change this — the renames are namespace-level + assembly-name-level only; consumer DI patterns are unchanged. **However** (per A10 sentence-level tightening): signal-bridge has ~10+ direct consumers of the renaming targets (verified via grep across `Sunfish.Blocks.{RentCollection,WorkOrders,Inspections,PublicListings,PropertyLeasingPipeline}` in `signal-bridge/Sunfish.Bridge/Listings/`, `Field/`, `Cockpit/`); these consumers WILL see `SUNFISH_BLOCKDEP001` warnings during the migration window and update at their own pace per the deprecation-window discipline.

**Interaction with ADR 0093 (Stage-05 Adversarial Review Protocol Amendment).** The 7 Step PRs follow the Stage-05 cadence (claim-beacon + spec + PR with binary gates + council review). Per the substrate-claim-beacon protocol (`feedback_substrate_claim_beacon_protocol`; Engineer 2026-05-26T03:14Z), all substrate-tier PRs require a pre-authoring claim beacon. Step 1 (new substrate) is substrate-tier; Steps 2-6 (mechanical renames) are substrate-touching (modifying substrate package boundaries); Engineer files claim beacons for each Step PR before authoring. Per-claim-beacon authoring time ~3-5 minutes given the rename mapping is enumerated in §Implementation roadmap below.

**Interaction with ADR 0094 (IAuditEventReader).** ADR 0094's read-substrate is Tier-1 (concrete DI; no vendor swap). The renamed blocks emit audit events via the existing `IAuditEmitter` substrate (unchanged); the renames preserve audit emission contracts. No ADR 0094 amendment needed.

**Interaction with ADR 0095 R2 (Bootstrap Context).** ADR 0095 is pre-tenant substrate. The renames in ADR 0098 are post-tenant (all 8 PAO-proposed candidates are tenant-scoped blocks). The two ADRs do not interact.

**Interaction with ADR 0096 R2 (Tier-2 Vendor-Provider Substrate).** ADR 0096 codifies Tier-2 substrate (email + CAPTCHA + production-guard); ADR 0098 codifies Tier-1 domain-block substrate renames + a new foundation-tier substrate. The two ADRs do not interact directly except via:

- The renamed `blocks-recurring-billing` consumes `Foundation.Integrations.Payments` (Tier-2 payments provider) via existing ProjectReference. The reference is preserved across the rename.
- The renamed `blocks-listings` interacts with Tier-2 storage providers (asset bucket for listing photos) via existing ProjectReference. Preserved.

No ADR 0096 amendment needed.

**Interaction with `pattern-009` (Bridge endpoint + frontend rebind pair).** Per `feedback_pattern009_scope` (memory): pattern-009 SPOT-CHECK applies to NEW routes, NOT to refactors of existing routes. The downstream signal-bridge consumer-update PRs (post-Accept) touch existing routes by changing `using` statements + DTO type names; they do NOT add new routes. Pattern-009 does NOT trigger on the rename wave. Confirmed.

**Tier-1 domain-block boundary.** The 5 renamed blocks remain Tier-1 domain-blocks (concrete DI; never swapped at runtime per the three-tier vocabulary). The new `foundation-agreements` package is Tier-1 foundation-tier substrate (concrete interface definitions consumed by vertical blocks). Neither is Tier-2 (category-provider; bounded vendor swap) nor Tier-3 (capability-plugin; runtime swap). ADR 0098 does NOT touch Tier-2 or Tier-3 substrate.

**Open-source posture preservation per ADR 0018.** All renamed packages remain MIT-licensed per ADR 0018 governance posture. The new `foundation-agreements` package inherits the same license. No license-tier changes.

## Implementation roadmap

Nine Step PRs. Step 1 is the substrate-package new-creation (the load-bearing surface; carries dual-council MANDATORY per Halt 10). Steps 2-6 are mechanical rename-and-shim PRs (parallel-after-Step-1; each carries .NET-architect SPOT-CHECK; Step 5 additionally carries sec-eng SPOT-CHECK because the W#28 capability-tier surface is touched). Step 7 is the Roslyn deprecation analyzer (MANDATORY per Halt 7; .NET-architect MANDATORY). Step 8 is `blocks-leases` `IAgreement` adoption as cross-vertical-reuse exemplar (post-MVP slack-window per Halt 8). Step 9 is the kernel-codename README docs-only PR (post-Accept per Halt 9).

### Step 1 — `foundation-agreements` new package PR (dual-council MANDATORY per Halt 10)

Branch shape: `feat/adr-0098-step-1-foundation-agreements` (Engineer-authored post-ADR Acceptance).

Scope:

- New package `packages/foundation-agreements/` (Shape α per A9 + Admiral pre-ruling P4 RATIFY: interfaces+enum ONLY; ZERO external `PackageReferences`; ONE `ProjectReference` to `foundation-multitenancy` per A1 marker extension; matches the `foundation-multitenancy` marker-package precedent — zero DI helpers shipped at Step 1; consumers wire concrete `IAgreement` implementations directly in their composition roots; DI helper additive at Step 8 when a vertical-block adoption surfaces a concrete substrate-DI need):
  - `Sunfish.Foundation.Agreements.csproj`:
    ```xml
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <PackageId>Sunfish.Foundation.Agreements</PackageId>
        <Description>Cross-vertical agreement substrate (IAgreement + IContractTerm + IParty + AgreementStatus) per ADR 0098. Vertical blocks (blocks-leases today; future blocks-brand-deals, blocks-license-agreements) implement IAgreement.</Description>
        <IsPackable>true</IsPackable>
      </PropertyGroup>
      <ItemGroup>
        <!-- IAgreement extends IMustHaveTenant per ADR 0008's typed-marker precedent (per A1). -->
        <ProjectReference Include="..\foundation-multitenancy\Sunfish.Foundation.MultiTenancy.csproj" />
      </ItemGroup>
    </Project>
    ```
    No external `PackageReferences`; no DI registration helper. If a future Step needs a substrate-level DI helper (e.g., `AddSunfishAgreements<TConcrete>`), authoring is additive — a follow-on PR ships the helper without disturbing Step 1's interface-only shape.
  - `IAgreement.cs` — per A1 + A7 + Admiral pre-ruling P1:
    ```csharp
    using Sunfish.Foundation.MultiTenancy;
    namespace Sunfish.Foundation.Agreements;

    public interface IAgreement : IMustHaveTenant
    {
        string AgreementId { get; }
        IReadOnlyList<IParty> Parties { get; }      // Deterministic ordering; conventionally Parties[0] is the primary counterparty (lessor / brand / licensor)
        IReadOnlyList<IContractTerm> Terms { get; } // Deterministic ordering; vertical-defined sort
        AgreementStatus Status { get; }
        DateTimeOffset CreatedAt { get; }
        DateTimeOffset? ActivatedAt { get; }        // Null until Status transitions to Active
        DateTimeOffset? TerminatedAt { get; }       // Null until Status transitions to Terminated
        // TenantId inherited from IMustHaveTenant : ITenantScoped (per A1)
    }
    ```
    xmldoc cites ADR 0008 + ADR 0098 + the cross-vertical-reuse rationale + the typed-marker composition pattern (A1) + the IReadOnlyList ordering convention (A7).
  - `IContractTerm.cs` — 3-property interface (`TermId`, `TermType`, `Description`); xmldoc describes vertical-defined `TermType` marker convention.
  - `IParty.cs` — 3-property interface (`PartyId`, `Role`, `DisplayName`); xmldoc enumerates canonical role markers (lessor / lessee / brand / creator / licensor / licensee) + cites §"PII discipline on `IParty.DisplayName`" for vertical adopter responsibility.
  - `AgreementStatus.cs` — 4-value enum (Draft = 0, PendingSignature = 1, Active = 2, Terminated = 3); xmldoc describes per-vertical refinement convention.
  - Unit tests at `packages/foundation-agreements.tests/` — interface-shape verification (compilation tests; per ADR 0095 R2 / 0096 R2 Step 1 test precedent — registration-presence + contract verification, not resolution-validation). Tests verify: (a) `IAgreement : IMustHaveTenant` (marker extension compiles); (b) `Parties` is `IReadOnlyList<IParty>` (positional access); (c) `Terms` is `IReadOnlyList<IContractTerm>` (positional access); (d) `AgreementStatus` enum value ordering (Draft=0 baseline).

- Documentation:
  - xmldoc on every introduced type per ADR 0069 §A0 discipline.
  - Package README documenting the substrate-tier role + cross-vertical-reuse thesis + the canonical vertical-block implementation pattern.
  - Cross-reference from ADR 0095 R2 / 0096 R2 README touchpoints if any (none expected — ADR 0098 substrate is independent).

- csproj integration:
  - Add `packages/foundation-agreements/Sunfish.Foundation.Agreements.csproj` to the Shipyard solution file (the workspace-wide `Sunfish.sln`).
  - Inbound dependency: ONE `ProjectReference` to `..\foundation-multitenancy\Sunfish.Foundation.MultiTenancy.csproj` (per A1 + Shape α per A9). No external `PackageReferences`. No ProjectReferences from existing packages to `foundation-agreements` in Step 1 (the substrate is consumed in Step 8 deferred work).

**Council review (Halt 10 MANDATORY): dual-council at PR-open.** .NET-architect reviews substrate API design (interface shape, property semantics, multi-tenancy invariant honoring, enum lifecycle modeling). Security-engineering SPOT-CHECK optional — no security surface in Step 1 (no new request paths; no credential surface; no DI-resolved scoped state).

Effort: ~3-4h Engineer + Sonnet medium subagent (substrate authoring is highly templated against the ADR 0095 / 0096 Step 1 precedent).

### Step 2 — `blocks-rent-collection` → `blocks-recurring-billing` rename + shim PR

Branch shape: `feat/adr-0098-step-2-blocks-recurring-billing`.

Scope:

- New package `packages/blocks-recurring-billing/`:
  - `Sunfish.Blocks.RecurringBilling.csproj` — new csproj at new path; new namespace `Sunfish.Blocks.RecurringBilling`; all source moved + adjusted (file-level namespace declarations updated; type-name renames per §"Cross-reference table — types-per-package" below).
  - All public-API types preserved; `RentLedgerEntry` → `RecurringLedgerEntry`; `IRentLedgerService` → `IRecurringLedgerService`; remaining types (`Invoice`, `Payment`, `BankAccount`, `BillingFrequency`, `LateFeePolicy`) preserved unchanged (already domain-generic). Per A2 tail-bullet: if `RentLedgerEntry` has a strongly-typed identifier wrapper (`RentLedgerEntryId` record struct or equivalent), it renames in lockstep — Engineer verifies at Step 2 PR authoring time (likely no separate identifier wrapper exists, but the audit is part of the per-Step Engineer-spec checklist per the §"Per-rename migration pattern" cookbook).

- Existing package `packages/blocks-rent-collection/` retained as re-export shim:
  - csproj retained; ProjectReference to `Sunfish.Blocks.RecurringBilling.csproj`.
  - Source files replaced with `[assembly: TypeForwardedTo(typeof(...))]` declarations for every public type — `.NET`-idiomatic binary-compatibility rename mechanism. Consumers compiled against the old assembly name continue to resolve correctly.
  - Package-level assembly attribute: `[Obsolete("Sunfish.Blocks.RentCollection is renamed to Sunfish.Blocks.RecurringBilling per ADR 0098; this package will be removed in the major version following the cross-vertical-reuse rename wave's deprecation cycle.")]` — warning (false), not error, per migration discipline.

- ProjectReference updates within shipyard for internal consumers of `blocks-rent-collection` (limited to in-tree references; sunfish desktop + signal-bridge are downstream of Shipyard and update at their own pace via the shim).

- Major version bump on both new and shim packages per SemVer + per the §"Per-rename migration pattern" below.

**Council review (Halt 10): .NET-architect SPOT-CHECK at PR-open.** Per-shim correctness review + ProjectReference graph coherence verification.

Effort: ~2-3h Engineer (or Sonnet medium subagent for the mechanical-rename portion).

### Step 3 — `blocks-work-orders` → `blocks-work-items` rename + shim PR

Branch shape: `feat/adr-0098-step-3-blocks-work-items`.

Scope: Same pattern as Step 2. `Sunfish.Blocks.WorkOrders.csproj` → `Sunfish.Blocks.WorkItems.csproj`; `WorkOrder` → `WorkItem`; `WorkOrderStatus` → `WorkItemStatus`; `IWorkOrderService` → `IWorkItemService`. `TypeForwardedTo` shim from `blocks-work-orders`. ProjectReference updates internal.

**Council review (Halt 10): .NET-architect SPOT-CHECK at PR-open.**

Effort: ~1-2h Engineer.

### Step 4 — `blocks-inspections` → `blocks-reviews` rename + shim PR

Branch shape: `feat/adr-0098-step-4-blocks-reviews`.

Scope: Same pattern as Step 2. `Sunfish.Blocks.Inspections.csproj` → `Sunfish.Blocks.Reviews.csproj`; `Inspection` → `Review`; `InspectionTemplate` → `ReviewTemplate`; `InspectionResponse` → `ReviewResponse`; `IInspectionsService` → `IReviewsService`. Property-domain-coupling types (`Deficiency`, `EquipmentConditionAssessment`) PRESERVED unchanged — entity-shape generalization is OUT OF SCOPE per Halt 6 Option α. The `EquipmentConditionAssessment` retains its ProjectReference to `blocks-property-equipment`; flagged in §"Forward watch" as cross-vertical-divergence trigger.

`TypeForwardedTo` shim from `blocks-inspections`. ProjectReference updates internal.

**Council review (Halt 10): .NET-architect SPOT-CHECK at PR-open.**

Effort: ~1-2h Engineer.

### Step 5 — `blocks-public-listings` → `blocks-listings` rename + shim PR

Branch shape: `feat/adr-0098-step-5-blocks-listings`.

Scope: Same pattern as Step 2. `Sunfish.Blocks.PublicListings.csproj` → `Sunfish.Blocks.Listings.csproj`; `PublicListing` → `Listing` (singular type rename). Pipeline-mechanics-types (`ListingPhotoRef`, `RedactionPolicy`, `ShowingAvailability`, `RenderedListing`, `IListingRepository`, `IListingRenderer`, `ICapabilityPromoter`) preserved unchanged. The W#28 capability-tier-promotion surface is the security-relevant boundary; sec-eng SPOT-CHECK additionally MANDATORY per Halt 10.

`TypeForwardedTo` shim from `blocks-public-listings`. ProjectReference updates internal.

**Council review (Halt 10): .NET-architect SPOT-CHECK + sec-eng SPOT-CHECK at PR-open.** Sec-eng verifies the rename preserves the W#28 capability-tier-promotion access-control surface invariants per the enumerated minimum-floors below (per S2 — security-engineering amendment; Rev 2 ADR-ratified):

**W#28 capability-tier invariant minimum-floors (Step 5 SPOT-CHECK verifies):**

  - **(S2a) Type-name preservation across Capabilities/Defense/Audit subsystems.** Step 5 SHALL preserve all type names within `Capabilities/`, `Defense/`, and `Audit/` subdirectories verbatim (per the W#28 surface inventory: `ICapabilityPromoter`, `MacaroonCapabilityPromoter`, `IProspectCapabilityVerifier`, `MacaroonProspectCapabilityVerifier`, `ProspectCaveatNames`, `IInquiryFormDefense`, `IInquiryRateLimiter`, `InMemoryInquiryRateLimiter`, `InquiryFormDefense`, `InquiryFormDefenseOptions`, `IEmailMxResolver`, `StubEmailMxResolver`, `PublicListingAuditEmitter`, `PublicListingAuditPayloadFactory`). Only the `PublicListing` aggregate → `Listing` rename and the namespace rename are in scope. Sec-eng SPOT-CHECK verifies the W#28 type names are intact post-rename.
  - **(S2b) Macaroon caveat-name namespace preservation.** `ProspectCaveatNames.cs` is the string-constant registry for macaroon caveat names (e.g., `"public-listings.prospect-tier-1"`). If these strings reference the namespace name (which they shouldn't — macaroon caveats are runtime tokens, not namespace-coupled), the rename breaks issued-and-not-yet-expired macaroons. Step 5 SHALL audit `ProspectCaveatNames` for namespace-coupled string constants; any string constant referencing `PublicListings` namespace SHALL be preserved verbatim OR migrated with an explicit caveat-rotation discipline flagged at the Step 5 PR.
  - **(S2c) Audit emission payload schema preservation.** `PublicListingAuditPayloadFactory.cs` produces audit payloads with type discriminator strings. If the discriminator is the `.NET` type FullName (e.g., `"Sunfish.Blocks.PublicListings.PublicListing"`), the rename changes the discriminator value in audit emissions — breaking downstream audit-log analytics that filter on the discriminator. Step 5 SHALL audit `PublicListingAuditPayloadFactory` for namespace-coupled discriminator strings; any namespace-coupled discriminator SHALL be either preserved verbatim (with `[Obsolete]` migration period) OR migrated with explicit audit-log-consumer migration coordination flagged at the Step 5 PR.
  - **(S2d) InquiryFormDefense rate-limit + MX validation discipline preservation.** The Defense subsystem ships the public-facing untrusted-input boundary. The rename SHALL NOT alter the Defense semantics (rate-limit thresholds, MX validation behavior). Step 5 SHALL bit-equivalent-preserve all `Defense/` subsystem behavior; sec-eng SPOT-CHECK verifies via test-equivalence between old-name and new-name code paths.

Effort: ~2-3h Engineer (incrementally higher than Steps 3/4 due to W#28 cross-cutting + dual SPOT-CHECK coordination + the enumerated minimum-floor verification).

### Step 6 — `blocks-property-leasing-pipeline` → `blocks-acquisition-pipeline` rename + shim PR

Branch shape: `feat/adr-0098-step-6-blocks-acquisition-pipeline`.

Scope: Same pattern as Step 2. `Sunfish.Blocks.PropertyLeasingPipeline.csproj` → `Sunfish.Blocks.AcquisitionPipeline.csproj`. Per A2 (`.NET`-architect amendment) the "Lease" prefix appears in **three co-declared types** (verified by grep across `packages/blocks-property-leasing-pipeline/Models/`): `LeaseOffer` (record at `Models/LeaseOffer.cs`) + `LeaseOfferId` (record struct at `Models/Identifiers.cs`) + `LeaseOfferStatus` (enum at `Models/LeaseOffer.cs`). All three rename in lockstep: `LeaseOffer` → `OfferTerms`; `LeaseOfferId` → `OfferTermsId`; `LeaseOfferStatus` → `OfferTermsStatus`. Without the lockstep rename, consumers would compile against an awkward `OfferTerms { Id = new LeaseOfferId(...), Status = LeaseOfferStatus.Issued }` post-rename surface. Everything else — `Inquiry`, `Prospect`, `Application`, `DecisioningFacts`, `DemographicProfile`, `BackgroundCheckRequest`, `BackgroundCheckResult`, `AdverseActionNotice` — preserved unchanged. FHA + FCRA coupling (`DemographicProfile` + `AdverseActionNotice` + `BackgroundCheckResult`) preserved per Halt 6 Option α; flagged in §"Forward watch" as cross-vertical-divergence trigger.

`TypeForwardedTo` shim from `blocks-property-leasing-pipeline` (covers all three renamed types + the un-renamed pipeline types). ProjectReference updates internal. Per A11 (`.NET`-architect verification): ADR 0057 (Leasing-Pipeline + Fair Housing) contains ZERO `LeaseOffer` references (verified by grep at Rev 2 authoring time); no amendment side-letter trigger. Q3 RESOLVED at Rev 2 fold.

**Council review (Halt 10): .NET-architect SPOT-CHECK + sec-eng SPOT-CHECK at PR-open** (per S5 + Admiral pre-ruling P5 RATIFY). The rename touches the FHA + FCRA compliance surface (`DemographicProfile` + `AdverseActionNotice` + `BackgroundCheckResult` — all retained per Halt 6 Option α but living in a renamed namespace + assembly). Sec-eng verifies the rename preserves: (a) `DemographicProfile` redaction discipline (FHA-quarantined PII fields — protected-class attributes — must not leak in audit emissions or non-redacted projections post-rename); (b) `AdverseActionNotice` access-control + audit-trail (FCRA-mandated notice surface; who can issue / view + audit-trail completeness unchanged); (c) `BackgroundCheckResult` retention discipline (third-party-data PII — criminal/credit history — retention window unchanged). The .NET-architect SPOT-CHECK additionally verifies the lockstep rename of all three `LeaseOffer*` types per A2 and the `TypeForwardedTo` shim covers the record struct + enum. This brings Step 6 to parity with Step 5 (dual SPOT-CHECK) — both touch security-relevant compliance surfaces (W#28 capability-tier for Step 5; FHA/FCRA for Step 6).

Effort: ~2-3h Engineer (incrementally similar to Step 5 due to dual SPOT-CHECK coordination + the three-type lockstep rename).

### Step 7 — `BlockNameDeprecationAnalyzer` Roslyn analyzer PR (MANDATORY per Halt 7; .NET-architect MANDATORY per Halt 10)

Branch shape: `feat/adr-0098-step-7-block-name-deprecation-analyzer`.

Scope:

- Extend `packages/foundation-wayfinder-analyzers/` (existing analyzer package per ADR 0095 R2 Step 3 precedent) with a new analyzer `BlockNameDeprecationAnalyzer`. Emits `SUNFISH_ADR_0098_DEPRECATED_BLOCK_NAME` diagnostic at compile time on:
  - `using Sunfish.Blocks.RentCollection;` (suggests `using Sunfish.Blocks.RecurringBilling;`)
  - `using Sunfish.Blocks.WorkOrders;` (suggests `using Sunfish.Blocks.WorkItems;`)
  - `using Sunfish.Blocks.Inspections;` (suggests `using Sunfish.Blocks.Reviews;`)
  - `using Sunfish.Blocks.PublicListings;` (suggests `using Sunfish.Blocks.Listings;`)
  - `using Sunfish.Blocks.PropertyLeasingPipeline;` (suggests `using Sunfish.Blocks.AcquisitionPipeline;`)
  - Direct fully-qualified-name uses (`Sunfish.Blocks.RentCollection.Invoice` etc.) — same diagnostic.
  - Type-level renames within the rename targets (`LeaseOffer` → `OfferTerms`, `RentLedgerEntry` → `RecurringLedgerEntry`, etc.) — direct type-name suggestions where applicable.

- Default severity: `Warning`. Per `feedback_prefer_cleanest_long_term_option` + ADR 0091 R2 amendment A2 + ADR 0095 R2 Step 3 analyzer precedent, the analyzer's compile-warning is louder than the `[Obsolete]` warning alone + CI-enforceable in `WarningsAsErrors` mode at consumer projects' discretion.

- Code-fix provider: where applicable, ships a Roslyn code-fix that rewrites the deprecated `using` / fully-qualified name to the new namespace. Code-fix is non-mandatory (analyzer ships first; code-fix is a follow-on if Engineer time permits at Step 7 authoring; otherwise deferred).

- Unit tests: AnalyzerTestFramework xUnit tests covering each deprecated-name case + non-deprecated control case (must NOT warn on uses of the renamed-target namespaces).

- CI gate addition (optional; Engineer-judgment at Step 7 authoring): add `SUNFISH_ADR_0098_DEPRECATED_BLOCK_NAME` to the `WarningsAsErrors` list in the Shipyard solution Directory.Build.props if council ratifies. Default: warning-only; downstream consumer projects opt in by their own `WarningsAsErrors` configuration.

**Council review (Halt 10 MANDATORY): .NET-architect MANDATORY at PR-open.** Reviews analyzer code quality + diagnostic ID assignment + code-fix correctness (if shipped) + severity selection rationale. Sec-eng SPOT-CHECK optional (no security surface).

Effort: ~2-3h Engineer + ~1h .NET-architect council review.

### Step 8 — `blocks-leases` `IAgreement` adoption (post-MVP slack-window per Halt 8; deferred)

Branch shape: `feat/adr-0098-step-8-blocks-leases-iagreement-adoption` (deferred; NOT delivered by ADR 0098 Acceptance; ships during post-MVP slack-window per CIC dispatch).

Scope (forward-watch):

- `blocks-leases` adds ProjectReference to `foundation-agreements`.
- `Sunfish.Blocks.Leases.Lease` aggregate implements `Sunfish.Foundation.Agreements.IAgreement` — first cross-vertical-reuse exemplar.
- `Lease.AgreementId` returns the existing strongly-typed `LeaseId`'s underlying string (per `IAgreement.AgreementId: string`); `Lease.TenantId` returns existing tenant scoping; `Lease.Parties` materializes existing lessor + lessee + co-signers as `IParty` instances with role markers; `Lease.Terms` materializes existing rent-amount + lease-duration + escalation + other terms as `IContractTerm` instances; `Lease.Status` maps existing lease lifecycle to `AgreementStatus`.
- ADR 0057 (Leasing Pipeline + Fair Housing) may require amendment-side-letter touch — flagged for Admiral consideration when Step 8 dispatches.
- Subsequent vertical blocks (future `blocks-brand-deals`, `blocks-license-agreements`) implement `IAgreement` at their authoring time.

**Council review:** dispatched at Step 8 authoring time; ADR 0098 does not pre-pin council cadence for Step 8 (the cross-vertical-reuse step is conceptually GREEN-attestable but ships in a different temporal window).

Effort: ~3-4h Engineer at Step 8 dispatch time.

### Step 9 — Kernel-codename READMEs (post-Accept docs-only PR per Halt 9; deferred)

Branch shape: `docs/adr-0098-step-9-kernel-codename-readmes` (deferred; separate docs-only PR; NOT part of ADR 0098 Acceptance critical path).

Scope:

- Author README.md for each kernel-codename package without one today:
  - `packages/blocks-quarterdeck/` (apex entry-point block per ADR 0080)
  - `packages/blocks-sick-bay/` (operational-health aggregation surface per ADR 0082)
  - `packages/blocks-engine-room/` (runtime telemetry block)
  - `packages/blocks-ships-office/` (content-aggregation surface per ADR 0083)
  - `packages/blocks-tactical/` (anomaly-detection block per ADR 0081)
  - `packages/blocks-crew-comms/` (crew-communications block)
  - Any other codename-named packages without a current README.

- Each README documents (a) the codename's source-of-truth ADR if any, (b) the substantive role (in domain-grounded terms, not fleet-codename only), (c) the consumer pattern, (d) the cross-vertical applicability note where relevant.

**Council review:** none required for docs-only.

Effort: ~3-4h Sonnet medium subagent dispatch (delegated work per ONR scaffold suggestion).

### Per-rename migration pattern (Steps 2-6 canonical mechanic)

For each of the 5 confirmed renames, the migration pattern is:

1. **New package shipped.** New csproj at new path (e.g., `packages/blocks-recurring-billing/Sunfish.Blocks.RecurringBilling.csproj`), new namespace (`Sunfish.Blocks.RecurringBilling`), all source moved + adjusted (file-level namespace declarations updated; type-name renames per §"Cross-reference table — types-per-package" below).
2. **Old package retained as re-export shim.** Old csproj keeps its name, references the new csproj via ProjectReference, re-exports all public types via `[assembly: TypeForwardedTo(typeof(<NewNamespace>.<Type>))]` declarations (the `.NET`-idiomatic binary-compatibility rename mechanism). Consumers compiled against the old assembly name continue to resolve correctly.
3. **Old package deprecated.** Package-level assembly attribute: `[Obsolete("<OldNamespace> is renamed to <NewNamespace> per ADR 0098; this package will be removed in the major version following the cross-vertical-reuse rename wave's deprecation cycle.", false /* warning, not error */)]`.
4. **Major version bump.** Both new and shim packages bump per SemVer to the next major version; renames are binary-compat-survivable but consumer tooling depending on AssemblyName WILL break — major bump is honest.
5. **Archive after one release cycle.** After one full release cycle of co-shipping new + shim (typically 1-2 months at current Shipyard cadence per ADR 0011 Bundle Versioning + Upgrade Policy), the shim package is archived (csproj removed; published NuGet package marked listed=false on nuget.org).
6. **Step 7 Roslyn analyzer ships after Steps 2-6 land.** Emits compile-time warnings on `using` of the deprecated namespaces + direct fully-qualified-name uses; suggests the new namespace + new type name where applicable.

### Cross-reference table — types-per-package (Steps 2-6 detailed mapping)

| Old package | Old key types | New package | New key types (renamed?) | Notes |
|---|---|---|---|---|
| `blocks-rent-collection` (`Sunfish.Blocks.RentCollection`) | `Invoice`, `Payment`, `BankAccount`, `BillingFrequency`, `LateFeePolicy`, `RentLedgerEntry`, `IRentLedgerService` | `blocks-recurring-billing` (`Sunfish.Blocks.RecurringBilling`) | `Invoice`, `Payment`, `BankAccount`, `BillingFrequency`, `LateFeePolicy`, `RecurringLedgerEntry`, `IRecurringLedgerService` | "Rent" → "Recurring" renames in 2 of 7 types (the ledger entry + service) |
| `blocks-work-orders` (`Sunfish.Blocks.WorkOrders`) | `WorkOrder`, `WorkOrderStatus`, `IWorkOrderService` | `blocks-work-items` (`Sunfish.Blocks.WorkItems`) | `WorkItem`, `WorkItemStatus`, `IWorkItemService` | "Order" → "Item" renames throughout |
| `blocks-inspections` (`Sunfish.Blocks.Inspections`) | `Inspection`, `InspectionTemplate`, `InspectionResponse`, `Deficiency`, `EquipmentConditionAssessment`, `IInspectionsService` | `blocks-reviews` (`Sunfish.Blocks.Reviews`) | `Review`, `ReviewTemplate`, `ReviewResponse`, `Deficiency`, `EquipmentConditionAssessment`, `IReviewsService` | "Inspection" → "Review" renames; entity-coupling to `blocks-property-equipment` retained but flagged for future generalization (Halt 6 Option α) |
| `blocks-public-listings` (`Sunfish.Blocks.PublicListings`) | `PublicListing`, `ListingPhotoRef`, `RedactionPolicy`, `ShowingAvailability`, `RenderedListing`, `IListingRepository`, `IListingRenderer`, `ICapabilityPromoter` | `blocks-listings` (`Sunfish.Blocks.Listings`) | `Listing`, `ListingPhotoRef`, `RedactionPolicy`, `ShowingAvailability`, `RenderedListing`, `IListingRepository`, `IListingRenderer`, `ICapabilityPromoter` | "PublicListing" → "Listing" (singular type rename); pipeline-mechanics-types retained |
| `blocks-property-leasing-pipeline` (`Sunfish.Blocks.PropertyLeasingPipeline`) | `Inquiry`, `Prospect`, `Application`, `DecisioningFacts`, `DemographicProfile`, `BackgroundCheckRequest`, `BackgroundCheckResult`, `AdverseActionNotice`, `LeaseOffer`, `ILeasingPipelineService` | `blocks-acquisition-pipeline` (`Sunfish.Blocks.AcquisitionPipeline`) | `Inquiry`, `Prospect`, `Application`, `DecisioningFacts`, `DemographicProfile`, `BackgroundCheckRequest`, `BackgroundCheckResult`, `AdverseActionNotice`, `OfferTerms` (renamed from `LeaseOffer`), `IAcquisitionPipelineService` | "Lease" appears only in `LeaseOffer` — rename to `OfferTerms`; FHA/FCRA-specific entities retained but flagged for future generalization (Halt 6 Option α) |

### Parallelism + total scope

- **Step 1 is gating** for Steps 2-6 because Steps 2-6 do not depend on the substrate (the renamed blocks do NOT implement `IAgreement` at this time per Halt 8 Option α — that is Step 8 deferred work). Step 1 can therefore land **in parallel** with Steps 2-6 if desired; Engineer-judgment on sequencing.
- **Steps 2-6 are pure mechanical renames + shims**; they can run in **parallel** as separate PRs once each branches from main (no inter-PR dependencies; each touches a disjoint subset of `packages/`). Engineer cap (per `feedback_engineer_pr_count_cap` = 10 in-flight; 2026-05-21 ratification) accommodates the parallel wave easily.
- **Step 7 (analyzer) depends on Steps 2-6 all having landed** — the analyzer needs to know all the rename mappings to emit the per-namespace + per-type suggestions.
- **Total Engineer lift:** ~13-19 hours across Steps 1-7 (Step 1: ~3-4h; Steps 2-6: ~1-3h each = ~8-13h aggregate; Step 7: ~2-3h). Substantially less than the alternative of incremental renames spread across 3-6 months of substrate-block PR churn per D5.
- **Downstream consumer-update lift (post-Accept; not Engineer-blocking):** ~10-15 hours across sunfish desktop + signal-bridge consumer PRs; happens at downstream's own pace within the deprecation window per the re-export-shim discipline.
- **Council attestation cadence:** 1 dual-council MANDATORY on ADR text (this PR; Halt 10); 1 dual-council MANDATORY on Step 1 PR; 5 SPOT-CHECKs on Steps 2-6 (Step 5 has 2 SPOT-CHECKs — .NET-arch + sec-eng); 1 .NET-architect MANDATORY on Step 7 analyzer.

### Rollback story

Per-step rollback is trivial because each Step PR is mechanical-rename-only:

- **Step 1 rollback** (new substrate package): `git revert` removes the foundation-agreements package. No downstream depends on it yet (Step 8 deferred). Cost: minutes.
- **Steps 2-6 rollback** (per-block rename + shim): `git revert` the rename PR. Re-export shim disappears; downstream consumers revert their consumption updates. Cost: ~30 minutes per rollback.
- **Step 7 rollback** (analyzer): disable the analyzer (remove from `foundation-wayfinder-analyzers` registration) OR `git revert` the analyzer PR. Cost: minutes.
- **Step 8 rollback** (deferred): does not affect ADR 0098 Acceptance; rollback is at Step 8 authoring time + its own dispatch.

Rollback is mechanical because each Step preserves the old package (shim) for the entire deprecation window — no destructive operations until shim archive (which is its own PR, separate from this ADR; not in the ADR 0098 critical path).

## Alternatives considered (rejection summary)

The §"Considered options" section above enumerated eight design alternatives (Options A-H) with per-option rationale + disposition. This section consolidates the rejections for quick reference + names the CIC-ratification + halt-ruling provenance for each REJECT.

| Option | Description | Disposition | Provenance |
|---|---|---|---|
| A | Status quo (no rename) | **REJECTED** | CIC ratification of PAO UPF 2026-05-26T03:42Z; Admiral routing ruling 2026-05-26T0345Z. Per D1 (open-source adoption lock-in irreversibility) + D2 (cross-vertical reuse thesis). |
| B | Per-vertical foundation packages (`foundation-property-contracts` + `foundation-media-contracts`) | **REJECTED** | Defeats cross-vertical substrate reuse premise; per D2. |
| C | In-place renames without re-export wrappers (hard-break) | **REJECTED** | Hostile breaking-change experience for downstream consumers; per D5 + D1 governance posture. |
| D | Rename `kernel-lease` → `kernel-agreement` (PAO UPF Rename 1) | **REJECTED — BLOCKING** | Halt 2 OVERRIDE-PAO; RATIFY-ONR-COUNTER-FINDING. CIC override of PAO UPF ratified 2026-05-26T04:55Z. Per Admiral 10-halt ruling: kernel-lease is the Flease distributed-lock coordinator (Lamport / Chubby / Flease lineage), NOT a property-domain abstraction. Already domain-free in the relevant sense; rename would destroy the distributed-systems-literature semantic anchor + collide with the new `foundation-agreements` substrate. |
| E | Rename `blocks-tenant-admin` → `blocks-party-portal` (PAO UPF Rename 7) | **REJECTED — BLOCKING** | Halt 4 OVERRIDE-PAO; RATIFY-ONR-COUNTER-FINDING. CIC override of PAO UPF ratified 2026-05-26T04:55Z. Per Admiral 10-halt ruling: blocks-tenant-admin is the SaaS-tenant admin surface (GitHub-org-settings analog), NOT a counterparty portal. Renaming would destroy correct-for-purpose meaning + imply a self-service surface this block does NOT ship. If counterparty-portal needed, author a NEW block. |
| F | Name new substrate `foundation-contracts` (with `IContract` interface) — PAO UPF naming | **REJECTED** | Halt 5 RATIFY-ONR. Cross-language collision with TypeScript `@sunfish/contracts` package + `System.Diagnostics.Contracts` BCL namespace overlap + .NET "contract-first programming" community convention three-way ambiguity. The recommended `foundation-agreements` substrate avoids all three collisions. |
| G | Entity-shape generalization within ADR 0098 scope | **REJECTED** | Halt 6 RATIFY-ONR (Option α — name-only renames). Pre-emptive entity-shape generalization risks over-abstracting for hypothetical needs (premature abstraction). Entity-shape divergence deferred until a 2nd-vertical consumer surfaces with concrete divergence requirements. |
| H | Carve kernel-codename README additions into ADR 0098 scope | **REJECTED** | Halt 9 RATIFY-ONR. ADR 0098 is substrate-tier rename scope; codename-README is docs-only hygiene. Carved out as Step 9 separate docs-only PR post-Accept. |

**Bonus alternative — Roslyn analyzer optional, not mandatory.** Halt 7 ratified MANDATORY (analyzer ships at Step 7). The "optional" alternative was REJECTED per `feedback_prefer_cleanest_long_term_option` + ADR 0091 R2 amendment A2 + ADR 0095 R2 Step 3 analyzer precedent. The compile-time warning is louder + CI-enforceable + closes the silent-uses-of-deprecated-namespace foot-gun that `[Obsolete]` alone cannot.

**Bonus alternative — Option β vertical-block parallel implementation policy.** Halt 8 ratified Option α (cross-vertical reuse via substrate inheritance). The "Option β — name-rename-only, vertical-parallel-blocks-later" alternative was REJECTED. Option α captures the cross-vertical-reuse value proposition immediately; Option β would force Flight Deck Phase 1+ to author parallel blocks (`blocks-media-recurring-billing`, etc.) duplicating substrate-tier mechanics. Option α defers only the entity-shape divergence work, not the structural substrate-tier shape.

## Consequences

**Positive:**

- **Open-source adoption lock-in pre-empted at low cost.** The rename wave completes BEFORE community downstream consumption locks the property-vertical-specific names. ~1-2 Engineer-day cost; saved cost grows monotonically with downstream adoption depth. Per D1.
- **Cross-vertical substrate reuse is codified at substrate tier**, not documentation discipline. The new `foundation-agreements` package gives Flight Deck Phase 1+ a concrete substrate to consume + future verticals (rights-licensing, employment-pipeline) a substrate to extend. Per D2.
- **Two PAO-proposed renames REJECTED with semantic grounds**, preserving `kernel-lease` (Flease coordinator) + `blocks-tenant-admin` (SaaS-tenant admin) at their correct-for-purpose names. ~7+ Engineer-days of wasted rename work averted per the Admiral 10-halt ruling Process Win section. Per D4.
- **Substrate-tier ADR cadence (Halt 10 MANDATORY dual-council + Step 1 dual-council)** prevents the Rev-1-too-narrow → Rev-2-with-strengthening churn pattern ADR 0095 + 0096 both demonstrated. Pre-paid council attestation against ratification-shape coherence.
- **Roslyn deprecation analyzer (Step 7 MANDATORY per Halt 7)** closes the silent-uses-of-deprecated-namespace foot-gun. Compile-warning is louder than `[Obsolete]` alone + CI-enforceable in `WarningsAsErrors` mode. Per D5 cleanest-long-term-option + ADR 0091 R2 amendment A2 + ADR 0095 R2 Step 3 analyzer precedent.
- **Composition-tier non-disruption.** `TypeForwardedTo` re-export shims mean downstream repos (sunfish desktop + signal-bridge + future flight-deck) update at their own pace WITHIN the deprecation window. Hard-break risk window is one release cycle per `.NET` binary-compat-rename mechanics. Per D5.

**Negative / costs:**

- **5 new csprojs + 5 retained shim csprojs ship in the same release cycle.** Engineering hours pre-MVP: ~13-19 hours across Steps 1-7 (per §Implementation roadmap §Parallelism + total scope). Downstream consumer-update lift: ~10-15 hours across sunfish desktop + signal-bridge (per ONR scaffold §5.1; happens at downstream's own pace within deprecation window). Total ~23-34 Engineer-hours across Shipyard + downstream consumers.
- **Dual-council MANDATORY on ADR text + Step 1 PR + Step 7 analyzer PR** adds ~30-min dispatch latency per. Pre-paid against the Rev-2-with-strengthening churn pattern.
- **Cross-language naming guidance burden.** `foundation-agreements` (`.NET`) + `@sunfish/contracts` (TypeScript) live in the same monorepo at distinct sub-packages; new developers must orient on which is which. The naming choice (Halt 5) minimizes the disambiguation cost but does not eliminate it.

**Risks:**

- **Risk R1 — Step 1 PR scope creep.** Step 1 covers the new substrate package + 4 interface types + 1 enum + xmldoc + tests. Engineer may split into two PRs if scope threshold reached (per fleet PR-cap discipline). Mitigation: explicit Step 1a/1b split call at Step 1 authoring if Engineer flags.

- **Risk R2 — `LeaseOffer` → `OfferTerms` ADR 0057 amendment trigger.** ADR 0057 (Leasing Pipeline + Fair Housing) references `LeaseOffer` by name; the Step 6 rename to `OfferTerms` MAY require an ADR 0057 amendment side-letter. Mitigation: flagged in §"Open questions" below; Admiral-judgment at Step 6 authoring time.

- **Risk R3 — Deprecation-cycle calendar slippage.** "One release cycle" is implicit cadence (per ADR 0011); Shipyard release-on-readiness has no fixed schedule yet. The shim packages remain on nuget.org indefinitely if no release cycle ships before MVP launch. Mitigation: Admiral pins a calendar date at Step 6 PR closing time (e.g., "shim packages archived 2026-08-01") rather than leaving "next release cycle" as the trigger.

- **Risk R4 — Roslyn analyzer false-positive cost.** The Step 7 analyzer warns on every `using <DeprecatedNamespace>;` and every fully-qualified-name use. In a long-tailed downstream codebase (signal-bridge has ~60 consumption sites across the 5 renamed namespaces per the ONR scaffold §5.1 audit), the analyzer noise during the migration window is real. Mitigation: shim-then-deprecation-window discipline gives downstream time to migrate before the analyzer fires; downstream consumers may opt the warning out via per-project `<NoWarn>SUNFISH_ADR_0098_DEPRECATED_BLOCK_NAME</NoWarn>` while they migrate.

- **Risk R5 — Cross-vertical substrate IAgreement shape mismatch when Step 8 adopts.** The `IAgreement` shape pinned at Step 1 may not exactly match `Sunfish.Blocks.Leases.Lease`'s actual entity geometry; Step 8 adoption may surface refinement needs. Mitigation: Step 1 interface shape is INTENTIONALLY thin (6 properties on `IAgreement`; 3 on `IContractTerm`; 3 on `IParty`) to minimize implementation friction; any refinement is a Step 8-time amendment, not an ADR 0098 amendment.

## Open questions (forwarded)

These open questions are explicitly NOT pre-empted by this Rev 1 draft; they route to dual-council attestation at PR-open per Halt 10 OR to Step-PR authoring time per the step's council cadence. ONR scaffold §9 named the questions; this section forwards them at ADR-tier so council attestation can dispose.

**Q1 — `Agreement` typed-marker for substrate invariant.** Per the `IMustHaveTenant` precedent in `Foundation.MultiTenancy` (ADR 0008), a typed marker enforces invariants at compile time. A `Foundation.Agreements.IAgreementMustHaveParties` marker could enforce "an Agreement must have at least 2 Parties" at compile + runtime. Decided OUT of ADR 0098 scope; forwarded to .NET-architect council at Step 1 PR review for Engineer-judgment. If ratified at Step 1, Engineer adds the marker; otherwise deferred to Step 8 or beyond.

**Q2 — `LeaseId` ↔ `AgreementId` strongly-typed reconciliation.** When `blocks-leases.Lease` implements `Foundation.Agreements.IAgreement` (Step 8 deferred), does the existing `LeaseId`-as-`string` strongly-typed wrapper collapse to the substrate's `AgreementId`-as-`string` plain-string? Or stay separate strongly-typed? Decided OUT of ADR 0098 scope; forwarded to .NET-architect council at Step 8 PR authoring time.

**Q3 — ADR 0057 amendment side-letter for `LeaseOffer` → `OfferTerms` rename.** ADR 0057 references `LeaseOffer` by name. Step 6 PR's rename may require an ADR 0057 amendment side-letter (per ADR 0069 §A0 cited-symbol audit discipline). Decided OUT of ADR 0098 scope; flagged for Admiral consideration at Step 6 PR closing time.

**Q4 — Roslyn analyzer scope: `using` vs fully-qualified.** Step 7 analyzer ratified MANDATORY (Halt 7); the analyzer emits warnings on `using <DeprecatedNamespace>;` AND on fully-qualified-name uses (`Sunfish.Blocks.RentCollection.Invoice`). Whether the analyzer ALSO flags type-only renames within the rename targets (`LeaseOffer` → `OfferTerms`, `RentLedgerEntry` → `RecurringLedgerEntry`) is implementation-detail. Decided OUT of ADR 0098 scope; .NET-architect council resolves at Step 7 PR review.

**Q5 — Deprecation cadence pinning.** Per ADR 0011 (Bundle Versioning + Upgrade Policy), Shipyard release cadence is implicit (release-on-readiness; no fixed schedule). "One release cycle" therefore translates to "the next time Shipyard tags a release after Step 6 ships." Admiral may want to commit to a calendar date (e.g., "shim packages archived 2026-08-01") rather than leaving "next release cycle" as the trigger. Decided OUT of ADR 0098 scope; flagged for Admiral at Step 6 PR closing time per Risk R3 mitigation.

## Revisit triggers

This ADR is revisited (Rev 2 or follow-up amendment) when any of:

1. **A third semantic counter-finding emerges** for any of the 5 confirmed renames — i.e., further code-archaeology reveals that one of the renames the Admiral 10-halt ruling RATIFIED should be RE-REJECTED. Unlikely (ONR's pre-Rev-1 audit was thorough; scaffold §2.1 enumerated per-package semantic content) but the contingency exists for completeness.
2. **Step 8 cross-vertical reuse adoption surfaces substrate-shape refinement needs.** When `blocks-leases.Lease` implements `IAgreement`, the materialization of existing `Lease` properties into `IAgreement.Parties` + `IAgreement.Terms` may surface shape adjustments. If so, ADR 0098 amendment refines the substrate; not an ADR 0091-style full rework.
3. **A second 2nd-vertical block (`blocks-brand-deals`, `blocks-license-agreements`, etc.) adopts `IAgreement`.** When the second vertical implements the substrate, divergence concerns from Halt 6 forward-watch (Option β) may surface concrete requirements. ADR 0098 amendment refines based on the 2nd-vertical implementation experience.
4. **The downstream consumer-update lift exceeds the §"Consequences" estimate by >2×.** If sunfish desktop + signal-bridge consumer-update PRs take substantially longer than the ~10-15 hour estimate, the re-export-shim discipline may need amendment (e.g., longer deprecation window; richer code-fix provider in the Step 7 analyzer).
5. **Open-source community downstream feedback** identifies a substrate-shape concern that warrants amendment. Per ADR 0018 governance posture, community feedback is a first-class input to substrate-tier ADR refinement; an ADR 0098 amendment is the right venue for community-feedback-driven refinement.

## References

- PAO source UPF (cross-vertical-reuse driver): `coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`
- Admiral routing ruling (original Requests A+B authorization; CIC ratified 2026-05-26T03:42Z): `coordination/inbox/admiral-ruling-2026-05-26T0345Z-pao-routing-block-naming-flight-deck-storymodule.md`
- Admiral 10-halt resolution ruling (CIC ratified 2026-05-26T04:55Z; 2 BLOCKING ONR counter-findings ratified + 8 dispositions): `coordination/inbox/admiral-ruling-2026-05-26T0500Z-adr-0098-block-naming-10-halts-resolved.md`
- ONR scaffold predecessor research (562 lines): `shipyard/icm/01_discovery/research/onr-adr-0098-block-naming-generalization-scaffold.md` (removed in this ADR's Commit 1 as the canonical text supersedes; archived in git history via the sibling ONR scaffold PR)
- ONR scaffold-complete status beacon: `coordination/inbox/onr-status-2026-05-26T0445Z-block-naming-generalization-scaffold-complete.md`
- ADR 0008 Foundation.MultiTenancy (IMustHaveTenant invariant; cited by `IAgreement.TenantId`): `shipyard/docs/adrs/0008-foundation-multitenancy.md`
- ADR 0011 Bundle Versioning + Upgrade Policy (release cadence driving deprecation-window timing): `shipyard/docs/adrs/0011-bundle-versioning-upgrade-policy.md`
- ADR 0018 Governance + License Posture (open-source adoption lock-in driver): `shipyard/docs/adrs/0018-governance-and-license-posture.md`
- ADR 0057 Leasing Pipeline + Fair Housing (implementing-block of rename target #6; amendment side-letter trigger per Q3): `shipyard/docs/adrs/0057-leasing-pipeline-fair-housing.md`
- ADR 0059 Public-Listing Surface (implementing-block of rename target #5; W#28 capability-tier-promotion surface): `shipyard/docs/adrs/0059-public-listing-surface.md`
- ADR 0069 ADR Authoring Discipline (governs pre-merge council + §A0 + three-direction): `shipyard/docs/adrs/0069-adr-authoring-discipline.md`
- ADR 0091 R2 ITenantContext Divergence Resolution (substrate-tier ADR cadence precedent: helper + assertion + analyzer): `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md`
- ADR 0093 Stage-05 Adversarial Review Protocol Amendment (governs Step PR review cadence): `shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md`
- ADR 0094 IAuditEventReader (read-substrate Tier-1 sibling; audit-emission contract preservation): `shipyard/docs/adrs/0094-i-audit-event-reader.md`
- ADR 0095 R2 Bootstrap Context substrate (substrate-tier ADR shape mirror; foundation-tier substrate sibling): `shipyard/docs/adrs/0095-bootstrap-context.md`
- ADR 0096 R2 Tier-2 Vendor-Provider Substrate (substrate-tier ADR shape mirror; Halt-3 OVERRIDE precedent): `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md`
- Cerebrum: `[[prefer-cleanest-long-term-option]]` (CIC directive 2026-05-21 driving D5)
- Cerebrum: `[[three-tier-slotting-vocabulary]]` (CIC ratified 2026-05-25 driving D6)
- Cerebrum: `[[substrate-claim-beacon-protocol]]` (Engineer 2026-05-26T03:14Z; applies to Steps 1 + 2-6 substrate-touching PRs)
- Cerebrum: `[[itenantcontext-consumption-qualification]]` (2026-05-22 driving §Substrate / layering notes interaction with ADR 0091 R2)
- Cerebrum: `[[pattern009-scope]]` (driving §Substrate / layering notes interaction with pattern-009 — no trigger on existing-route refactors)
- Cerebrum: `[[engineer-pr-count-cap]]` (2026-05-21 driving §Implementation roadmap parallelism analysis — 10 in-flight cap accommodates parallel rename wave)
- Slotting general UPF (the precedent for tier-1 / tier-2 / tier-3 vocabulary; sibling shipyard PR): `shipyard/icm/01_discovery/research/onr-slotting-architecture-7-gap-audit.md`
- Pattern catalog (pattern-009 scope reference): `shipyard/_shared/engineering/standing-approved-patterns.md`
- Flease distributed-lock-coordinator literature (Lamport, Chubby, Flease — Halt 2 BLOCKING counter-finding source): general distributed-systems knowledge informing the kernel-lease-is-not-property-domain finding; the canonical Flease paper is "Flease — Lease Coordination Without a Lock Server" (Hupfeld et al., 2008).
- `.NET` `TypeForwardedTo` attribute for binary-compatibility renames (Steps 2-6 mechanism): general `.NET` framework convention.
- NuGet semantic-versioning conventions for major-version-bump-on-rename (Step 2-6 versioning discipline): general NuGet packaging convention.
- `System.Diagnostics.Contracts` namespace + .NET "contract-first programming" community convention (Halt 5 rationale): general `.NET` community knowledge informing the `foundation-contracts` vs `foundation-agreements` recommendation.


