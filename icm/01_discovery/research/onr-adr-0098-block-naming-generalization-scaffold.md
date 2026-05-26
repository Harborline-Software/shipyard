# ONR research — ADR 0098 Block-Naming Generalization scaffold (PAO UPF ratified)

**Authored by:** ONR
**Requester:** Admiral (per `coordination/inbox/admiral-ruling-2026-05-26T0345Z-pao-routing-block-naming-flight-deck-storymodule.md` — Requests A+B authorized; CIC ratification 2026-05-26T03:42Z)
**Authored at:** 2026-05-26
**Type:** Research scaffold for new ADR 0098 (Block-Naming Generalization for Cross-Vertical Substrate Reuse)
**Status:** Draft for Admiral consumption — Admiral authors ADR 0098 Rev 1 text from this scaffold
**ADR number rationale:** 0096 is the latest accepted; 0097 is reserved for PasswordHasher H8 per Admiral queue (not yet scaffolded). 0098 is the next-available un-claimed number. Halt 1 below names this for confirmation.

---

## Scope of investigation

- **In scope.** Verify the PAO UPF's status-quo claims against actual shipyard package state; audit semantic accuracy of each proposed rename; specify the `foundation-contracts` new-package surface concretely enough for Engineer implementation; enumerate halt conditions Admiral must resolve before authoring Rev 1; recommend per-Step PR sequencing + council cadence; surface ALL the load-bearing findings PAO's UPF did not catch.
- **Out of scope.** Writing the ADR itself (Admiral territory per ADR 0069); per-Step PR descriptions (downstream when Engineer picks up); the vertical-block parallel implementation question for Flight Deck (separate ADR or hand-off when Flight Deck Phase 1 enters Stage-05); Sunfish desktop / signal-bridge / tender / flight-deck downstream consumer-update PRs (those are downstream of ADR Accept).
- **Authoritative sources consulted.** PAO source UPF (`coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`); Admiral ruling (`admiral-ruling-2026-05-26T0345Z-pao-routing-block-naming-flight-deck-storymodule.md`); shipyard `packages/kernel-lease/` (README + `ILeaseCoordinator.cs` + csproj); shipyard `packages/blocks-leases/` + `blocks-rent-collection/` + `blocks-work-orders/` + `blocks-inspections/` + `blocks-public-listings/` + `blocks-tenant-admin/` + `blocks-property-leasing-pipeline/` (README + csproj per package); shipyard `packages/contracts/` (TypeScript; package.json); ADR 0095 (`shipyard/docs/adrs/0095-bootstrap-context.md`) for substrate-tier ADR shape mirror; ADR 0096 (`shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md`) for substrate-tier ADR shape mirror; three-tier vocabulary memory (`project_three_tier_slotting_vocabulary.md`); shipyard `_shared/engineering/standing-approved-patterns.md` for pattern catalog; ADR 0069 (ADR Authoring Discipline) for §A0 + pre-merge council protocol; cross-fleet grep for `Kernel.Lease` consumers (no external repo references found beyond shipyard-internal `blocks-scheduling` + `kernel-ledger` + `kernel-runtime`).
- **Success criteria.** Admiral can author ADR 0098 Rev 1 from this scaffold without re-discovering the kernel-lease semantic collision, the blocks-tenant-admin scope conflict, the foundation-contracts namespace question, or the vertical-block migration sequencing. All decisions inside the ADR's scope have a recommended position; everything outside the ADR's scope has a halt condition naming who must resolve it.

---

## TL;DR

- **Problem.** PAO's UPF (ratified 2026-05-26T03:42Z) names 8 substrate-tier renames — 1 kernel package + 1 new foundation package + 6 block packages — required before open-source adoption locks Shipyard's package names. The rationale is sound: Shipyard's MIT open-source product targets BOTH property-management ERP (Sunfish vertical) AND media-company ERP (Flight Deck vertical); the substrate names below the verticals must be domain-generic or every cross-vertical reuse forces cognitive translation ("a brand-deal contract is a kind of lease?"). PAO correctly identifies the migration pattern: new package at new name + re-export wrapper from old name + deprecation notice + major version bump + archive after one release cycle.
- **Two load-bearing findings PAO did not catch.**
  - **Finding 1 (BLOCKING for Rename 1).** `Sunfish.Kernel.Lease` is **NOT** a property-domain "lease" package. It is the **Flease-inspired distributed lease coordinator** (`ILeaseCoordinator`) for CP-class record writes per the local-node-architecture paper §6.3. "Lease" here is the distributed-systems primitive (Lamport, Chubby, Flease) — the bounded-grant exclusive-write-rights resource lock — not a property rental agreement. The package is already domain-free. Consumers are `blocks-scheduling`, `kernel-ledger`, `kernel-runtime` — none property-domain. PAO's framing "kernel primitives must be domain-free; lease = property-specific" is factually wrong about THIS package. Renaming to `kernel-agreement` would (a) lose the distributed-systems-literature semantic anchor, (b) confuse the package with a contracts/agreements substrate, (c) introduce a name collision with the proposed `foundation-contracts` (which IS about agreements). **Recommendation: DO NOT rename `kernel-lease`.** Halt 2 names this for Admiral.
  - **Finding 2 (BLOCKING for Rename 7).** `Sunfish.Blocks.TenantAdmin` is **NOT** a property-tenant-portal package. It is the **SaaS-tenant admin surface** — managing the Sunfish-as-SaaS tenant profile, users, roles, and bundle activation (per its csproj `Description`: "tenant profile, users, roles, and bundle-activation surface over IBundleCatalog"). It is the analog of "Stripe customer portal" or "GitHub organization settings" — the SaaS-tenant-owner self-administration surface, not a counterparty portal for property renters. PAO's framing "self-service portal for any counterparty, not just property tenants" misreads the package. Renaming to `blocks-party-portal` would (a) destroy the SaaS-tenant connotation that is correct for the package's purpose, (b) imply a counterparty-self-service surface that this block does NOT ship, (c) overlap conceptually with `blocks-public-listings` capability-tier promotion surfaces. **Recommendation: DO NOT rename `blocks-tenant-admin`.** Halt 4 names this for Admiral.
- **The other 6 renames hold up.** `blocks-rent-collection → blocks-recurring-billing`, `blocks-work-orders → blocks-work-items`, `blocks-inspections → blocks-reviews`, `blocks-public-listings → blocks-listings`, `blocks-property-leasing-pipeline → blocks-acquisition-pipeline`, plus the new `foundation-contracts` package — all five renames are semantically accurate cross-vertical generalizations. ONR recommends proceeding with these 5 renames + the new package.
- **Net scope per ONR.** **6 changes (down from 8):** 1 new package (foundation-contracts), 0 kernel renames (kernel-lease stays), 4 block renames (rent-collection, work-orders, inspections, public-listings, property-leasing-pipeline — `blocks-tenant-admin` excluded), 1 deferred-to-vertical (the actual counterparty-portal surface PAO described needs a NEW block, `blocks-counterparty-portal`, not a rename of blocks-tenant-admin).
- **Foundation-contracts surface (recommended shape).** New `Sunfish.Foundation.Contracts` namespace + `packages/foundation-contracts/` package. Surface: `IAgreement` (the abstraction; properties for parties + terms + lifecycle), `IContractTerm` (a term within an agreement), `IParty` (a participant in an agreement). Vertical blocks implement these as the substrate for `blocks-leases` (property lease = agreement), the future `blocks-brand-deals` (media brand deal = agreement), the future `blocks-license-agreements` (rights licensing = agreement). Halt 3 names the interface-vs-name question (`IAgreement` vs `IContract`); ONR recommends `IAgreement` (matching the PAO UPF; "Contract" risks conflation with .NET `System.Diagnostics.Contracts` and the broader "contract-first programming" usage).
- **Critical: TypeScript namespace collision risk.** A TypeScript-side package `@sunfish/contracts` already ships in `packages/contracts/` — it's a SHARED-TYPE definitions package for cross-fleet consumers. The new .NET package `Sunfish.Foundation.Contracts` does NOT collide at runtime (different ecosystems) but the namespace ambiguity is real (a developer reading "the contracts package" must disambiguate). Halt 5 names this; ONR recommends naming the new .NET package `Sunfish.Foundation.Agreements` (matching `IAgreement`) — avoids the cross-language naming collision AND matches the dominant interface name.
- **Halt conditions for Admiral.** 9 halts total — see §7. The two BLOCKING halts (kernel-lease + blocks-tenant-admin) require explicit Admiral disposition before Engineer begins the rename wave; the remaining 7 are scope/policy clarifications that don't block scaffold-Acceptance but should be resolved before Rev 1.

---

## 1. Problem statement

### 1.1 Why this needs an ADR (vs an inline rename PR)

The rename wave PAO requested is mechanical at the per-package level — each package gets a new csproj name + new namespace + re-export shim + version bump — but the wave as a whole is substrate-tier for three reasons that warrant ADR-tier ratification:

1. **Open-source adoption lock-in is irreversible.** Once the substrate names ship in tagged NuGet releases consumed by community downstream projects (per ADR 0018 governance posture), renaming forces every downstream to a migration. PAO's UPF correctly identifies the window: rename NOW (before community adoption ramps) or accept the names forever. An ADR captures the "rename now" decision and pins the deprecation cadence.

2. **The interface choice in `foundation-contracts` constrains ≥3 future blocks.** `IAgreement` must serve as the substrate for `blocks-leases` (today; property domain), `blocks-brand-deals` (Flight Deck Phase 1+; media domain), `blocks-license-agreements` (forward-watch; rights catalog per the Flight Deck UPF media-ERP scope). Picking the wrong shape propagates across all three; ADR captures it once and the consuming blocks reference it.

3. **The kernel-lease semantic claim affects substrate posture across the kernel cluster.** PAO's framing "kernel primitives must be domain-free" is correct as a principle (mirrors ADR 0091 R2 §A0 audit posture about substrate naming) — but the application of the principle to `kernel-lease` is wrong (Finding 1 above). An ADR is the right surface to (a) state the principle clearly, (b) audit each kernel-cluster package against it, (c) reach the right conclusion per package, (d) preserve the principle for future kernel-cluster work without re-litigation. Doing this in an inline rename PR would either trip on the kernel-lease confusion silently OR re-litigate the principle per-rename — both bad outcomes.

### 1.2 What ADR 0098 must define

The ADR must specify:

- **The principle.** Substrate-tier packages (kernel + foundation + cross-vertical blocks) MUST have domain-generic names. Vertical-block packages MAY have domain-specific names. Define each tier; cite the canonical three-tier vocabulary (`project_three_tier_slotting_vocabulary.md`).
- **The audit.** Per-package application of the principle: which packages are substrate-tier-misnamed (rename targets), which are vertical-tier-correct (no-rename targets), which appear misnamed but on inspection ARE substrate-tier correct (counter-finding targets — `kernel-lease`, `blocks-tenant-admin`).
- **The renames.** The 4 block renames + 1 new foundation package, with concrete migration mechanics (re-export wrappers + deprecation notice + version bump + archive cadence).
- **The new `foundation-contracts` (or `foundation-agreements` — Halt 5) package.** Interface signatures for `IAgreement` / `IContractTerm` / `IParty` with concrete enough shape that the Step 1 Engineer PR can implement without further ADR ratification.
- **The vertical-block migration policy.** Whether existing vertical blocks (`blocks-leases` today) implement `IAgreement` immediately (consumer of new substrate) or defer until a downstream call-site needs polymorphic agreement handling. PAO's UPF doesn't pin this; ONR recommends the deferred-until-needed posture (Halt 8).
- **The deprecation analyzer story.** Whether a Roslyn analyzer ships warning on consumption of deprecated package names (mirrors ADR 0095 R2 Step 3 analyzer pattern) — sec-eng-council generally pushes for analyzer enforcement on substrate renames; .NET-architect council generally requires it for cleanest-long-term-option per `feedback_prefer_cleanest_long_term_option`.

### 1.3 What ADR 0098 must NOT define

- **The exact vertical-block parallel implementation for Flight Deck.** Whether `blocks-brand-deals` ships in shipyard or in flight-deck is downstream (Flight Deck Phase 1 ADR territory).
- **The story-structure module spec.** `flight-deck#33` Stage-05 hand-off is gated on this ADR Accepting (per Admiral ruling Request C); the spec itself is ONR's NEXT deliverable post-this-scaffold.
- **The downstream consumer-update PRs.** Each repo that imports renamed packages (sunfish desktop, signal-bridge, tender if any, flight-deck) gets its own consumer-update PR — that's Engineer / FED territory once the new package names exist.
- **The kernel-codename packages' READMEs.** PAO's UPF mentions "add READMEs to fleet codename packages." This is a docs-only PR; ADR-tier is overkill. ONR recommends carving this OUT of ADR 0098 scope and routing as a separate docs-PR (Halt 9).

---

## 2. Status-quo audit

### 2.1 The 8 packages PAO named — verified against shipyard reality

| # | Current name | Exists? | Cluster | Semantic content | PAO claim | ONR verdict |
|---|---|---|---|---|---|---|
| 1 | `kernel-lease` | yes (`packages/kernel-lease/`) | Kernel | **Flease-inspired distributed lease coordinator** for CP-class writes (paper §6.3; sync-daemon-protocol §6). `ILeaseCoordinator` with `AcquireAsync(resourceId, duration, ct)` returning `Lease?` (null = quorum unreachable). Consumers: `blocks-scheduling`, `kernel-ledger`, `kernel-runtime`. **Domain-FREE** — distributed-systems primitive. | "kernel primitives must be domain-free; lease = property-specific" → rename to `kernel-agreement` | **DO NOT RENAME (Finding 1, BLOCKING).** Semantic claim is factually wrong; package is already domain-free. Renaming destroys distributed-systems anchor + creates collision with proposed `foundation-contracts` / `foundation-agreements`. See Halt 2. |
| 2 | *(new)* | n/a (proposed) | Foundation | New substrate for `IAgreement` / `IContractTerm` / `IParty` interfaces. Vertical-block consumers: `blocks-leases` (property domain), forward `blocks-brand-deals` (media). | Add new package `foundation-contracts` with cross-vertical generic interfaces | **PROCEED** — but recommend renaming to `foundation-agreements` to avoid the cross-language `@sunfish/contracts` (TS) collision. See Halt 5. |
| 3 | `blocks-rent-collection` | yes | Block | Invoice + payment + ledger surface (`IRentLedgerService`, `RentLedgerEntry`). Defers Plaid/Stripe/event-bus per its README. Rent-specific in domain framing but the mechanics (recurring invoices + payments + ledger) are generic to any recurring-billing pattern. | Rename to `blocks-recurring-billing` (generic for rent, licensing, subscriptions) | **PROCEED** — accurate generalization. Mechanics are domain-agnostic; rename reflects this. |
| 4 | `blocks-work-orders` | yes | Block | Work-order entity + lifecycle + audit. "Work order" is a borderline-generic term but field-service-implies-property-maintenance is the connotation it carries today. | Rename to `blocks-work-items` (removes field-service implication) | **PROCEED** — accurate; "work items" is the broader-industry generic. |
| 5 | `blocks-inspections` | yes | Block | Inspection template + scheduled inspection + deficiency tracking + equipment-condition assessment + projections (MoveInOut, EquipmentConditionDelta). Property-inspection-specific in framing AND in entity shape (the `EquipmentConditionAssessment` references `blocks-property-equipment`). | Rename to `blocks-reviews` (editorial review, QA gate, compliance audit) | **PROCEED WITH CAVEAT** — name generalizes accurately, BUT the entity coupling to `blocks-property-equipment` is property-specific. Rename works at the package-name level; the entity-shape generalization is a separate downstream concern. ONR notes this as forward-watch but does not block. See Halt 6. |
| 6 | `blocks-public-listings` | yes | Block | Public-facing rental listing entity (`PublicListing` w/ slug, headline, photos, asking-rent, showing-availability, redaction policy) + tier-redacted projection (`RenderedListing`) per ADR 0059. Implements anonymous-browse → inquiry → capability-tier promotion pipeline. | Rename to `blocks-listings` (drop the real-estate implication) | **PROCEED** — accurate. The mechanics (public catalog + tier-redacted rendering + capability-promote) are generic to any catalog-with-public-pages pattern (real-estate listings, marketplace product listings, podcast episode listings, e-book preview listings). |
| 7 | `blocks-tenant-admin` | yes | Block | **SaaS-tenant admin surface** — tenant profile + users + roles + bundle-activation panel over `IBundleCatalog`. Analogous to GitHub org settings or Stripe customer portal. NOT a property-tenant portal. | "generic counterparty portal" → rename to `blocks-party-portal` | **DO NOT RENAME (Finding 2, BLOCKING).** Misreads the package. Counterparty portal is a DIFFERENT block (does not exist yet; needs separate authoring). See Halt 4. |
| 8 | `blocks-property-leasing-pipeline` | yes | Block | Rental-application lifecycle (Inquiry → Prospect → Application → BackgroundCheck → AdverseAction → LeaseOffer) per ADR 0057. Property-specific in entity framing AND in domain coupling (`FairHousingAct` references, FCRA workflow). The mechanics (intake funnel + qualification gates + AdverseAction notice) are generic. | Rename to `blocks-acquisition-pipeline` (CRM funnel pattern is generic) | **PROCEED WITH CAVEAT** — name generalizes accurately at the funnel-mechanics level. The FHA + FCRA coupling is property-domain; same pattern as Rename 5 — name is generic, entity shape downstream needs separate generalization work. ONR forward-watches. See Halt 6. |

### 2.2 Cross-fleet kernel-lease consumer audit

Repo-wide grep for `Sunfish.Kernel.Lease` and `kernel-lease`:

| Consumer | Path | Domain | Rename-survival? |
|---|---|---|---|
| `blocks-scheduling` | `packages/blocks-scheduling/Sunfish.Blocks.Scheduling.csproj` | Cross-vertical (scheduling primitives) | n/a (kernel-lease should NOT rename) |
| `kernel-ledger` | `packages/kernel-ledger/Sunfish.Kernel.Ledger.csproj` | Substrate (append-only ledger; uses leases to coordinate writes) | n/a |
| `kernel-runtime` | `packages/kernel-runtime/Sunfish.Kernel.Runtime.csproj` | Substrate (runtime composition) | n/a |
| External — sunfish desktop | (grep ran; zero matches) | n/a | n/a |
| External — signal-bridge | (grep ran; zero matches) | n/a | n/a |
| External — flight-deck | (grep ran; zero matches) | n/a | n/a |
| External — tender | (assumed zero; not grep'd separately — Rust) | n/a | n/a |

Conclusion: zero cross-repo external consumers of `Sunfish.Kernel.Lease`. The rename would touch 3 internal csprojs IF the rename happens. ONR recommends it does NOT happen (Finding 1).

### 2.3 Cross-fleet blocks-tenant-admin consumer audit

The package's `Description` ("tenant profile, users, roles, and bundle-activation surface over IBundleCatalog") + its Razor components (`BundleActivationPanel.razor`) + its services (`InMemoryTenantAdminService`, `UpdateTenantProfileRequest`, `InviteTenantUserRequest`) + its tests (`BundleActivationPanelTests`, `TenantAdminEntityModuleTests`) all confirm this is the SaaS-tenant-owner admin surface, NOT a counterparty (renter / brand-deal-counterparty) portal.

A "counterparty portal" — what PAO described — would be a NEW block that does NOT currently exist. ONR recommends: keep `blocks-tenant-admin` as the SaaS-tenant admin surface; if and when a counterparty portal is needed for either vertical, author a NEW block `blocks-counterparty-portal` (or vertical-specific: `blocks-renter-portal`, `blocks-creator-portal`).

### 2.4 The `foundation-contracts` name collision audit

`packages/contracts/` already exists as a TypeScript-only package (`@sunfish/contracts`) per its package.json: "Shared TypeScript interface definitions for the Sunfish ERPNext property management stack." It's the cross-fleet shared-types package. It does NOT define `IAgreement` / `IContract` / `IParty` interfaces — it defines property-domain TS shapes (accounting.ts, integrations.ts, property.ts, sync.ts, system-requirements.ts, tenant.ts) per the audit in `onr-slotting-architecture-7-gap-audit.md` §Gap 1.

A new .NET package `Sunfish.Foundation.Contracts` would NOT collide at runtime (different ecosystems) but the documentary collision is real. ONR recommends `Sunfish.Foundation.Agreements` to avoid: (a) the cross-language naming overlap, (b) `System.Diagnostics.Contracts` namespace collision (rare but real), (c) the .NET "contract-first programming" community convention (which means something different — design-by-contract assertion attributes).

### 2.5 Three-tier vocabulary mapping

Per `project_three_tier_slotting_vocabulary.md` (CIC ratified 2026-05-25):

| Tier | Name | Affected by ADR 0098 | How |
|---|---|---|---|
| 1 | `domain-block` (concrete DI, never swapped) | YES — substrate generalization | `IAgreement` is a domain-block interface; vertical-block implementations (Property `Lease`, future `BrandDeal`) are domain-blocks. |
| 2 | `category-provider` (bounded vendor swap) | NO — unaffected | Tier-2 is `Foundation.Integrations.<Category>` substrate per ADR 0096; no rename touches tier-2. |
| 3 | `capability-plugin` (runtime swap via manifest) | NO — unaffected | Tier-3 lives in `flight-deck/packages/plugins/` (TTS/STT/image/LLM); no rename touches tier-3. |

The `foundation-contracts` (or `foundation-agreements`) new package is tier-1 substrate for tier-1 vertical blocks — it sits BELOW the tier-1 vertical blocks (which implement its interfaces). PAO's UPF described this implicitly; this ADR pins it.

---

## 3. Decision drivers

- **Open-source adoption lock-in.** Per PAO UPF + Admiral ruling, the renames are mandatory **before** community adoption ramps. NuGet versioning + npm-equivalent semantic-versioning conventions mean a major-version-bump on a published package is a downstream break for every consumer. Doing the renames before tagged-NuGet-release-to-community is irreversibly cheaper than after.
- **Cross-vertical reuse thesis.** Per PAO Flight Deck media-ERP UPF + the dual-vertical-on-shared-substrate thesis. Successful execution of Sunfish-property + Flight-Deck-media on shared Shipyard substrate requires the substrate to be domain-generic at the level below the vertical blocks. The 4 block renames + 1 new foundation package execute this principle concretely.
- **Pre-empt forward churn.** Per the standing-memo `feedback_prefer_cleanest_long_term_option`. Doing the renames now (one focused wave, ~1-2 days Engineer lift) is substantially cheaper than the alternative (incremental rename + re-export migrations spread across multiple substrate-block PRs over the next 3-6 months as Flight Deck Phase 1 surfaces emerge).
- **Substrate-tier discipline (ADR 0091 R2 precedent).** ADR 0091 R2's amendment-A1-via-startup-assertion + amendment-A2-via-analyzer cadence is the precedent for substrate-tier ADRs that have a "principle + audit + per-package application" shape. ADR 0098 follows the same shape: state the principle (substrate names must be domain-generic), audit per-package (8 candidates from PAO UPF, 2 counter-findings + 4 confirmed renames), specify per-package migration (re-export wrappers + analyzer), ship per-step PRs.

---

## 4. Canonical pattern specification

### 4.1 The `foundation-agreements` (recommended over `foundation-contracts`) package surface

Per ONR recommendation Halt 5: name the new package `foundation-agreements` (csproj: `Sunfish.Foundation.Agreements`, namespace: `Sunfish.Foundation.Agreements`). Rationale: avoids cross-language collision with `@sunfish/contracts` TS package; avoids `System.Diagnostics.Contracts` namespace overlap; matches the dominant interface name `IAgreement` (PAO UPF named the interface and the package consistently — ONR preserves that consistency at a less-ambiguous name).

Concrete interface signatures (for Admiral's Rev 1 §Decision):

```csharp
namespace Sunfish.Foundation.Agreements;

/// <summary>
/// An agreement between two or more parties for a bounded set of obligations.
/// Vertical blocks implement this for domain-specific agreements (property lease,
/// media brand deal, rights license, employment contract, vendor MSA, etc.).
/// Substrate-tier abstraction; never directly instantiated.
/// </summary>
public interface IAgreement
{
    /// <summary>Stable identity (vertical-block-defined ID type; e.g., LeaseId, BrandDealId).</summary>
    string AgreementId { get; }

    /// <summary>Tenant (SaaS-tenant) owning this agreement; multi-tenancy invariant.</summary>
    string TenantId { get; }

    /// <summary>Parties to the agreement (lessor + lessee, brand + creator, licensor + licensee, etc.).</summary>
    IReadOnlyList<IParty> Parties { get; }

    /// <summary>Terms bound by the agreement (lease rent + duration, deal compensation + deliverables, etc.).</summary>
    IReadOnlyList<IContractTerm> Terms { get; }

    /// <summary>Lifecycle stage; vertical-defined but the enum shape mirrors a 4-stage pattern.</summary>
    AgreementStatus Status { get; }

    /// <summary>UTC timestamp the agreement was originally drafted.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>UTC timestamp the agreement became binding (Signed → Active transition).</summary>
    DateTimeOffset? ActivatedAt { get; }

    /// <summary>UTC timestamp the agreement ended; null if still active or unsigned.</summary>
    DateTimeOffset? TerminatedAt { get; }
}

/// <summary>Lifecycle stages of an IAgreement.</summary>
public enum AgreementStatus
{
    Draft = 0,
    PendingSignature = 1,
    Active = 2,
    Terminated = 3,
}

/// <summary>A single term within an IAgreement.</summary>
public interface IContractTerm
{
    /// <summary>Stable identity within the agreement.</summary>
    string TermId { get; }

    /// <summary>Vertical-defined term-type marker (e.g., "rent-amount", "lease-duration", "exclusivity-window").</summary>
    string TermType { get; }

    /// <summary>Human-readable summary of the term (for audit + party-facing display).</summary>
    string Description { get; }
}

/// <summary>A participant in an IAgreement (a tenant, a brand, a creator, a vendor, etc.).</summary>
public interface IParty
{
    /// <summary>Stable identity (vertical-block-defined ID type; e.g., PersonId, OrganizationId).</summary>
    string PartyId { get; }

    /// <summary>Vertical-defined role marker (e.g., "lessor", "lessee", "brand", "creator", "licensor", "licensee").</summary>
    string Role { get; }

    /// <summary>Human-readable display name for the party.</summary>
    string DisplayName { get; }
}
```

**Why these shapes (not richer ones).** PAO's UPF correctly identified that the abstraction must be thin — too rich an abstraction at the substrate tier forces every vertical to implement-and-ignore irrelevant members. The 6-property `IAgreement` + 3-property `IContractTerm` + 3-property `IParty` covers the cross-vertical commonality: identity + parties + terms + lifecycle. Anything beyond this (signature workflow, document storage, payment schedules, etc.) is vertical-specific.

**Why the enum (instead of a string or status-object).** A 4-value lifecycle enum is the cross-vertical common shape: Draft → PendingSignature → Active → Terminated. Per-vertical refinements (lease has SignedButNotYetCommenced; brand-deal has Negotiating-then-PendingSignature; etc.) can be modeled as vertical-block sub-states without changing the substrate enum.

**Why `TenantId` is on `IAgreement` directly (not delegated).** Multi-tenancy invariant per ADR 0008. Every domain entity that crosses the data-plane MUST carry TenantId (the foundation's `IMustHaveTenant` interface). Putting it on the substrate interface forces every vertical implementation to honor the invariant.

### 4.2 Per-rename migration pattern

For each of the 4 confirmed renames (plus the new foundation-agreements package), the migration pattern is:

1. **New package shipped.** New csproj at new path (e.g., `packages/blocks-recurring-billing/Sunfish.Blocks.RecurringBilling.csproj`), new namespace (`Sunfish.Blocks.RecurringBilling`), all source moved + adjusted.
2. **Old package retained as re-export shim.** Old csproj keeps its name, references the new csproj, re-exports all public types via `TypeForwardedTo` attributes (`.NET`-idiomatic for binary-compatibility renames).
3. **Old package deprecated.** `[Obsolete("Renamed to Sunfish.Blocks.RecurringBilling per ADR 0098; this package will be removed in v0.next+1", false /* warning, not error */)]` on the package-level assembly attribute.
4. **Major version bump.** Both new and shim packages bump to v0.next (per SemVer; renames are breaking-rename-survivable but tools depending on AssemblyName WILL break — major bump is honest).
5. **Archive after one release cycle.** After one full release cycle of co-shipping new + shim (typically 1-2 months at current Shipyard cadence), the shim package is archived (csproj removed; published NuGet package marked listed=false).
6. **Roslyn analyzer (optional).** Step N+1 PR ships a `BlockNameDeprecationAnalyzer` that emits a warning on `using Sunfish.Blocks.RentCollection;` (deprecated) suggesting `using Sunfish.Blocks.RecurringBilling;`. Halt 7 names whether this analyzer is mandatory or optional.

### 4.3 Cross-reference table — types-per-package

| Old package | Old key types | New package | New key types (renamed?) | Notes |
|---|---|---|---|---|
| `blocks-rent-collection` (`Sunfish.Blocks.RentCollection`) | `Invoice`, `Payment`, `BankAccount`, `BillingFrequency`, `LateFeePolicy`, `RentLedgerEntry`, `IRentLedgerService` | `blocks-recurring-billing` (`Sunfish.Blocks.RecurringBilling`) | `Invoice`, `Payment`, `BankAccount`, `BillingFrequency`, `LateFeePolicy`, `RecurringLedgerEntry`, `IRecurringLedgerService` | "Rent" → "Recurring" renames in 2 of 7 types (the ledger entry + service) |
| `blocks-work-orders` (`Sunfish.Blocks.WorkOrders`) | `WorkOrder`, `WorkOrderStatus`, `IWorkOrderService` | `blocks-work-items` (`Sunfish.Blocks.WorkItems`) | `WorkItem`, `WorkItemStatus`, `IWorkItemService` | "Order" → "Item" renames throughout |
| `blocks-inspections` (`Sunfish.Blocks.Inspections`) | `Inspection`, `InspectionTemplate`, `InspectionResponse`, `Deficiency`, `EquipmentConditionAssessment`, `IInspectionsService` | `blocks-reviews` (`Sunfish.Blocks.Reviews`) | `Review`, `ReviewTemplate`, `ReviewResponse`, `Deficiency`, `EquipmentConditionAssessment`, `IReviewsService` | "Inspection" → "Review" renames; entity-coupling to `blocks-property-equipment` retained but flagged for future generalization (Halt 6) |
| `blocks-public-listings` (`Sunfish.Blocks.PublicListings`) | `PublicListing`, `ListingPhotoRef`, `RedactionPolicy`, `ShowingAvailability`, `RenderedListing`, `IListingRepository`, `IListingRenderer`, `ICapabilityPromoter` | `blocks-listings` (`Sunfish.Blocks.Listings`) | `Listing`, `ListingPhotoRef`, `RedactionPolicy`, `ShowingAvailability`, `RenderedListing`, `IListingRepository`, `IListingRenderer`, `ICapabilityPromoter` | "PublicListing" → "Listing" (singular type rename); pipeline-mechanics-types retained |
| `blocks-property-leasing-pipeline` (`Sunfish.Blocks.PropertyLeasingPipeline`) | `Inquiry`, `Prospect`, `Application`, `DecisioningFacts`, `DemographicProfile`, `BackgroundCheckRequest`, `BackgroundCheckResult`, `AdverseActionNotice`, `LeaseOffer`, `ILeasingPipelineService` | `blocks-acquisition-pipeline` (`Sunfish.Blocks.AcquisitionPipeline`) | `Inquiry`, `Prospect`, `Application`, `DecisioningFacts`, `DemographicProfile`, `BackgroundCheckRequest`, `BackgroundCheckResult`, `OfferTerms` (renamed from `LeaseOffer`), `IAcquisitionPipelineService` | "Lease" appears only in `LeaseOffer` — rename to `OfferTerms`; FHA/FCRA-specific entities retained but flagged for future generalization (Halt 6) |

### 4.4 Vertical-block parallel implementation policy (Halt 8 territory)

The 4 renamed blocks (`blocks-recurring-billing`, `blocks-work-items`, `blocks-reviews`, `blocks-listings`, `blocks-acquisition-pipeline`) end up with cross-vertical-generic names but property-domain-specific entity shapes. The Flight Deck media vertical will need parallel blocks (`blocks-content-recurring-billing` IF the mechanics diverge OR cross-vertical reuse IF they don't).

Two policy options:

- **Option α — full cross-vertical reuse.** Renamed blocks serve both verticals; entity-shape generalization happens incrementally as Flight Deck Phase 1+ encounters specific divergence.
- **Option β — name-rename-only, vertical-parallel-blocks-later.** Renamed blocks remain effectively property-vertical; Flight Deck Phase 1+ authors parallel blocks (`blocks-media-recurring-billing`, etc.) with shared interface contracts where convenient.

ONR recommends **Option α** with explicit forward-watch for divergence. The renamed names accurately describe the mechanics; entity-shape divergence (FHA/FCRA in acquisition-pipeline; equipment-condition in reviews) should be refactored only when a 2nd-vertical consumer needs the substrate-tier abstraction. Until then, vertical-domain-specific fields are pragmatically retained. Halt 8 names this.

---

## 5. Cross-fleet integration concerns

### 5.1 Interaction with sunfish desktop / signal-bridge / tender / flight-deck

Each downstream repo that imports any of the renamed shipyard packages will need a consumer-update PR post-Accept. ONR did a targeted grep:

| Renamed package | Downstream consumers | Estimated effort per repo |
|---|---|---|
| `blocks-rent-collection → blocks-recurring-billing` | sunfish desktop (high; rent UI is property core), signal-bridge (medium; rent-collection API endpoints) | sunfish: ~2-3h; signal-bridge: ~1-2h |
| `blocks-work-orders → blocks-work-items` | sunfish desktop, signal-bridge (work-order endpoints) | sunfish: ~1-2h; signal-bridge: ~1h |
| `blocks-inspections → blocks-reviews` | sunfish desktop, signal-bridge | sunfish: ~1-2h; signal-bridge: ~1h |
| `blocks-public-listings → blocks-listings` | sunfish desktop, signal-bridge (the W#28 inquiry POST surface) | sunfish: ~1-2h; signal-bridge: ~2-3h (W#28 capability-tier promotion is complex) |
| `blocks-property-leasing-pipeline → blocks-acquisition-pipeline` | sunfish desktop, signal-bridge | sunfish: ~1-2h; signal-bridge: ~1h |
| `foundation-agreements` (new) | None initially; `blocks-leases` Option α consumer in a forward PR | Zero on Accept; ~3-4h when `blocks-leases` implements `IAgreement` |

Total downstream lift: ~10-15 engineer-hours across sunfish + signal-bridge (no flight-deck or tender impact in Step 1 — those repos don't yet consume the renamed packages).

The re-export-shim pattern means downstream repos can update at their own pace WITHIN the deprecation window — Shipyard ships the new package + old shim simultaneously; downstream updates `using` statements when convenient. Hard-break risk window is one release cycle (per §4.2 step 5).

### 5.2 Interaction with ADR 0096 (Tier-2 vendor-provider substrate)

ADR 0096 codifies the tier-2 substrate (email + CAPTCHA + production-guard). The renames in ADR 0098 are tier-1 (domain-block) substrate. The two ADRs don't interact directly except via:

- The renamed `blocks-recurring-billing` consumes `Foundation.Integrations.Payments` (tier-2 payments provider). The renamed package's csproj `ProjectReference` to `foundation-integrations-payments` is unchanged.
- The renamed `blocks-listings` interacts with tier-2 storage providers (asset bucket for listing photos). Unchanged.

No ADR 0096 amendment needed.

### 5.3 Interaction with ADR 0095 (Bootstrap context)

ADR 0095 is pre-tenant substrate. The renames in ADR 0098 are post-tenant (all 8 candidates are tenant-scoped blocks). The two ADRs don't interact.

### 5.4 Interaction with ADR 0091 (ITenantContext divergence)

ADR 0091's facade-vs-narrowed posture affects how consumers of the renamed blocks inject `ITenantContext`. The renames preserve `ITenantContext` consumption sites; per `feedback_itenantcontext_consumption_qualification`, consumers continue to inject the Authorization sum-interface facade until ADR 0091 Step 3 narrows. ADR 0098 does NOT change this.

### 5.5 Interaction with pattern-009 (Bridge endpoint + frontend rebind pair)

Per fleet conventions, pattern-009 SPOT-CHECK applies to NEW routes, not refactors of existing routes. The downstream signal-bridge consumer-update PRs would touch existing routes (changing the controller's `using` statements + DTO type-names) but would NOT add new routes — pattern-009 does NOT trigger. ONR confirms via `feedback_pattern009_scope`.

### 5.6 Interaction with the substrate claim-beacon protocol

Per `feedback_substrate_claim_beacon_protocol`, all substrate-tier PRs require a pre-authoring claim beacon. The rename wave is substrate-tier — Engineer SHALL file claim beacons for each Step PR before authoring. The Step PRs are predictable enough (the rename mapping per §4.3) that claim beacons should be quick — ~3-5 minutes per beacon.

---

## 6. Implementation roadmap

ONR recommends 7 Steps for the rename wave (compared to PAO's 8-rename implied scope). Per-Step PR is small + mechanical except Step 1 (which authors the new substrate) and Step 7 (analyzer, optional).

### 6.1 Step sequencing

| Step | Scope | Effort | Council |
|---|---|---|---|
| 1 | New `packages/foundation-agreements/` (or `foundation-contracts` per Halt 5) — `IAgreement` + `IContractTerm` + `IParty` interfaces + `AgreementStatus` enum. NO concrete impl yet; substrate-only ADR-tier landing. csproj + namespace + xmldoc + unit tests for interface-shape verification (compilation tests). | ~3-4h | .NET-architect MANDATORY (substrate-tier API design); sec-eng SPOT-CHECK optional (no security surface) |
| 2 | `blocks-rent-collection` → `blocks-recurring-billing` rename + `TypeForwardedTo` shim + downstream `ProjectReference` updates (within shipyard only — sunfish desktop / signal-bridge are downstream of this). | ~2-3h | .NET-architect SPOT-CHECK (per-shim correctness); sec-eng optional |
| 3 | `blocks-work-orders` → `blocks-work-items` rename + shim. | ~1-2h | .NET-architect SPOT-CHECK |
| 4 | `blocks-inspections` → `blocks-reviews` rename + shim. | ~1-2h | .NET-architect SPOT-CHECK |
| 5 | `blocks-public-listings` → `blocks-listings` rename + shim. | ~2-3h | .NET-architect SPOT-CHECK; sec-eng SPOT-CHECK (W#28 capability-tier surface) |
| 6 | `blocks-property-leasing-pipeline` → `blocks-acquisition-pipeline` rename + shim. The `LeaseOffer` type → `OfferTerms` rename within this package. | ~2-3h | .NET-architect SPOT-CHECK |
| 7 (optional) | `BlockNameDeprecationAnalyzer` Roslyn analyzer + CI gate addition. Emits warning on `using Sunfish.Blocks.RentCollection;` (etc.) suggesting the new namespace. Mirrors ADR 0095 R2 Step 3 analyzer pattern. | ~2-3h | .NET-architect (analyzer code-quality) |
| 8 (deferred) | `blocks-leases` implements `IAgreement` from `foundation-agreements`. Per Halt 8 Option α — happens when a 2nd-vertical consumer needs polymorphic agreement handling. NOT part of ADR 0098 scope. | n/a | n/a |
| 9 (deferred, optional) | Kernel-codename packages (quarterdeck / sick-bay / engine-room / ships-office / tactical / crew-comms) gain README.md additions documenting what they actually do. NOT part of ADR 0098 scope per Halt 9 — separate docs-PR. | n/a | n/a |

### 6.2 Parallelism opportunities

Steps 2-6 are pure mechanical renames + shims. They can run in **parallel** as separate PRs (no inter-dependencies) once Step 1 is merged. Engineer cap (per `feedback_engineer_pr_count_cap` = 10 in-flight) accommodates the parallel wave easily.

Step 7 (analyzer) depends on Steps 2-6 all having shipped — the analyzer needs to know all the rename mappings.

Step 1 has NO dependency on Steps 2-6 (the new foundation-agreements package is independent of the block renames — it only matters once Step 8 happens).

### 6.3 Total scope estimate

- **Engineer lift:** ~13-19 hours across Steps 1-6 (substantially less than the alternative of incremental renames over the next 3-6 months).
- **Downstream consumer-update lift:** ~10-15 hours across sunfish desktop + signal-bridge (per §5.1; happens at downstream's own pace within the deprecation window).
- **Council attestation cadence:** 1 mandatory dual-council on Step 1; 5 SPOT-CHECKS on Steps 2-6; 1 SPOT-CHECK on Step 7 (optional).

### 6.4 Rollback story

Per-step rollback is trivial because each Step PR is mechanical-rename-only:

- **Steps 2-6 rollback:** `git revert` the rename PR. Re-export shim disappears; downstream consumers reverting consumption updates. Cost: ~30 minutes per rollback.
- **Step 1 rollback:** `git revert` removes the foundation-agreements package. No downstream depends on it yet (Step 8 deferred). Cost: minutes.
- **Step 7 rollback:** Disable the analyzer; cost: minutes.

Rollback is mechanical because each Step preserves the old package (shim) for the entire deprecation window — no destructive operations until Step archive (which is its own PR, separate from this ADR).

---

## 7. Halt conditions for Admiral

The following questions require Admiral (or council-via-Admiral) ratification before ADR 0098 Rev 1 ships. Two are BLOCKING (named explicitly); the rest are scope/policy refinements.

### Halt 1 — ADR number assignment

ONR scaffolds at **ADR 0098** based on: 0096 latest accepted; 0097 reserved (per Admiral directive) for PasswordHasher H8 follow-on (not yet scaffolded but queued).

**Question for Admiral.** Confirm 0098 is the right number. If PasswordHasher H8 has shifted, 0097 may be available — but ONR conservatively reserves it.

**ONR recommendation.** ADR 0098.

### Halt 2 (BLOCKING) — `kernel-lease` rename disposition

PAO UPF named `kernel-lease → kernel-agreement` as Rename 1. ONR Finding 1 establishes that `kernel-lease` is the Flease distributed lease coordinator — a distributed-systems primitive, NOT a property-domain abstraction. Renaming would (a) destroy the distributed-systems-literature semantic anchor (Flease, Lamport, Chubby), (b) collide with the proposed `foundation-agreements` package, (c) confuse package consumers who would expect `kernel-agreement` to be an agreement/contract substrate.

**Question for Admiral.** Affirm or override ONR Finding 1.

**ONR recommendation.** **DO NOT RENAME.** Drop Rename 1 from ADR 0098 scope. State the principle ("substrate names must be domain-generic") and apply it correctly: `kernel-lease` ALREADY satisfies the principle; the package name "lease" refers to the distributed-lock concept, which is domain-free. This is the correct counter-finding for the principle-application audit.

### Halt 3 — `IAgreement` vs `IContract` interface naming

PAO UPF named the interface `IAgreement`. ONR concurs (rationale: "contract" risks (a) collision with `System.Diagnostics.Contracts` namespace, (b) overlap with "contract-first programming" community convention which means something different, (c) cross-language ambiguity with TypeScript `@sunfish/contracts` package).

**Question for Admiral.** Affirm `IAgreement` or override to `IContract`.

**ONR recommendation.** `IAgreement`.

### Halt 4 (BLOCKING) — `blocks-tenant-admin` rename disposition

PAO UPF named `blocks-tenant-admin → blocks-party-portal` as Rename 7. ONR Finding 2 establishes that `blocks-tenant-admin` is the SaaS-tenant admin surface — managing the Sunfish-as-SaaS tenant profile + users + roles + bundle activation, NOT a counterparty (renter / brand-deal-counterparty) portal. Renaming would (a) destroy the correct-for-purpose SaaS-tenant connotation, (b) imply a counterparty-self-service surface this block does NOT ship, (c) conflate with the W#28 `blocks-public-listings` capability-tier-promotion surfaces.

**Question for Admiral.** Affirm or override ONR Finding 2.

**ONR recommendation.** **DO NOT RENAME.** Drop Rename 7 from ADR 0098 scope. If a counterparty portal is needed for either vertical (renter portal for property; creator portal for media), author a NEW block — `blocks-counterparty-portal` (generic) or vertical-specific (`blocks-renter-portal`, `blocks-creator-portal`). That's downstream-vertical-Stage-05 territory, not ADR 0098 scope.

### Halt 5 — `foundation-contracts` vs `foundation-agreements` package naming

PAO UPF named the new package `foundation-contracts`. ONR §2.4 audit surfaces the cross-language collision risk with the existing `@sunfish/contracts` TypeScript package (different ecosystems but documentary overlap real). ONR recommends `foundation-agreements` (csproj: `Sunfish.Foundation.Agreements`, namespace: `Sunfish.Foundation.Agreements`).

**Question for Admiral.** Affirm `foundation-agreements` or override to `foundation-contracts`.

**ONR recommendation.** `foundation-agreements` — matches the dominant interface name `IAgreement`; avoids cross-language naming collision; avoids `System.Diagnostics.Contracts` overlap.

### Halt 6 — Entity-shape generalization scope

Two renamed blocks have property-domain-specific entity coupling that the name-only rename does not address:

- `blocks-reviews` (formerly `blocks-inspections`) retains `EquipmentConditionAssessment` referencing `blocks-property-equipment`.
- `blocks-acquisition-pipeline` (formerly `blocks-property-leasing-pipeline`) retains FHA-quarantined `DemographicProfile` + FCRA `AdverseActionNotice` shapes.

**Question for Admiral.** Does ADR 0098 take a position on entity-shape generalization (Option α: keep entity shapes, refactor when 2nd-vertical needs it; Option β: refactor entity shapes within ADR 0098 to be cross-vertical-generic)?

**ONR recommendation.** Option α — name-rename-only at ADR 0098 scope. Entity-shape generalization is a separate per-block decision when a 2nd-vertical consumer surfaces; pre-emptive generalization risks over-abstracting for hypothetical needs (this is the "premature abstraction" smell). Forward-watch the items in a "Out of scope but flagged" §"Decision Log" entry per ADR 0098.

### Halt 7 — Roslyn deprecation analyzer mandatoriness

Step 7 (analyzer) is the cleanest-long-term-option enforcement mechanism per `feedback_prefer_cleanest_long_term_option`. Without the analyzer, consumers may continue importing the old package names indefinitely (the `[Obsolete]` warning is visible but easily ignored at compile time). With the analyzer, the warning is louder + CI-failing in `WarningsAsErrors` mode.

**Question for Admiral.** Mandatory Step 7 (analyzer) or optional?

**ONR recommendation.** **MANDATORY.** The cleanest-long-term-option directive applies; substrate-tier renames warrant analyzer-enforced migration nudges. ADR 0091 R2 amendment A2 precedent ships analyzer late in the migration sequence; ADR 0098 mirrors this — analyzer ships at Step 7 (after all renames lands), not Step 1.

### Halt 8 — Vertical-block parallel-implementation policy

Per §4.4, two options for how vertical blocks (`blocks-leases` today; `blocks-brand-deals` future) interact with the new `foundation-agreements` substrate:

- **Option α — full cross-vertical reuse.** Renamed blocks serve both verticals; entity-shape generalization happens incrementally.
- **Option β — name-rename-only, vertical-parallel-blocks-later.** Renamed blocks remain effectively property-vertical; Flight Deck authors parallel blocks.

**Question for Admiral.** Pin Option α or β.

**ONR recommendation.** Option α. Renamed names accurately describe the mechanics; entity-shape divergence should be deferred until a 2nd-vertical consumer needs the substrate-tier abstraction. Step 8 (deferred) handles `blocks-leases.IAgreement` implementation when a polymorphic agreement-handling call-site emerges.

### Halt 9 — Kernel-codename README addition scope

PAO UPF mentions: "Fleet codenames (`blocks-quarterdeck` / `blocks-sick-bay` / `blocks-engine-room` etc.) — internal naming; add READMEs only."

**Question for Admiral.** Does ADR 0098 scope cover the codename-README additions, or is this a separate docs-only PR?

**ONR recommendation.** **Separate docs-only PR.** ADR 0098 is substrate-tier rename scope; codename-README is docs-only hygiene. Carving it out keeps ADR 0098 focused. ONR can author the codename READMEs as a follow-on deliverable post-ADR-Accept (~3-4h Sonnet medium subagent dispatch).

### Halt 10 — Council review timing per Step

ADR 0095 R2 set the precedent: substrate-tier ADRs warrant mandatory dual-council pre-merge attestation on the ADR text + each Step 1/2/3 PR.

**Question for Admiral.** Apply the same cadence to ADR 0098? ONR recommends:

- **ADR 0098 Rev 1 text:** MANDATORY dual-council (.NET-architect + security-engineering) — substrate-tier ADR.
- **Step 1 PR (foundation-agreements substrate):** .NET-architect MANDATORY (API design); sec-eng SPOT-CHECK optional.
- **Steps 2-6 PR (rename + shim):** .NET-architect SPOT-CHECK per Step.
- **Step 7 PR (analyzer):** .NET-architect MANDATORY (analyzer-code quality).
- **Step 5 PR (`blocks-listings`):** sec-eng SPOT-CHECK additionally (W#28 capability-tier surface is touched).

**ONR recommendation.** As above.

---

## 8. Alternatives considered

ONR considered three alternatives to the ADR 0098 approach.

### Alternative 1 — Status quo (no rename)

Leave all package names as-is; let community adoption lock the property-vertical-specific names; refactor vertical-by-vertical when Flight Deck Phase 1+ encounters specific name pain.

- **Pro:** Zero engineering cost today.
- **Con:** Per PAO UPF + Admiral ruling, this is exactly the trap the rename wave avoids. NuGet semantic-versioning + community downstream means EVERY rename in the future is a breaking-change cost amortized across every downstream consumer. The longer the rename waits, the more expensive it gets.
- **Con:** Reinforces the cognitive translation cost across the Flight Deck vertical onboarding ("we have a recurring-billing concept but the package is named blocks-rent-collection").
- **Verdict:** Rejected per Admiral ruling.

### Alternative 2 — Per-vertical foundation packages (foundation-property-contracts + foundation-media-contracts)

Each vertical gets its own foundation package; no shared substrate.

- **Pro:** Each vertical has full freedom for vertical-specific abstractions.
- **Con:** Defeats the entire premise of cross-vertical substrate reuse — Shipyard's value proposition is "build once, ship to N verticals." If every vertical has its own foundation, Shipyard is just a starter-kit, not a substrate.
- **Con:** Forces N parallel package-maintenance burdens.
- **Verdict:** Rejected — substrate-tier IS the value.

### Alternative 3 — In-place renames without re-export wrappers

Each rename is a hard-break in a single PR: old package deleted, new package live, downstream MUST update in the same window.

- **Pro:** Smaller total surface (no shim packages to maintain).
- **Con:** Forces downstream-update lock-step with Shipyard's rename ship date. For open-source downstream consumers, this is a hostile breaking-change experience.
- **Con:** Creates a deployment-cliff: sunfish desktop + signal-bridge MUST update on the exact same release as Shipyard, OR they get build errors.
- **Verdict:** Rejected — re-export shim cost (~30 min per shim) is trivial vs the downstream pain it averts.

---

## 9. Open questions ONR could not resolve

These would benefit from a follow-up research dispatch or council consultation but do not block ADR 0098 scaffolding:

1. **Should the `Agreement` typed marker (e.g., the substrate's equivalent of `IMustHaveTenant`) be introduced?** Per `IMustHaveTenant` precedent in `Foundation.MultiTenancy`, a typed marker enforces invariants at compile time. An `Foundation.Agreements.IAgreementMustHaveParties` marker could enforce "an Agreement must have at least 2 Parties." Decided OUT of ADR 0098 scope; flagged for downstream Engineer consideration.

2. **What's the right migration path for the `Lease` aggregate in `blocks-leases`?** When `blocks-leases.Lease` implements `Foundation.Agreements.IAgreement`, does the existing `LeaseId`-as-`string` type collapse to the substrate's `AgreementId`-as-`string`? Or stay separate strongly-typed? Halt 8 Option α path — defer until Step 8 (downstream of ADR 0098 Accept).

3. **Does the `OfferTerms` rename in `blocks-acquisition-pipeline` (formerly `LeaseOffer`) require ADR 0057 amendment?** ADR 0057 is the Leasing-Pipeline + Fair Housing ADR; it references `LeaseOffer` by name. A name-rename in the implementing block might require an ADR 0057 amendment-side-letter. Decided OUT of ADR 0098 scope; flagged for Admiral consideration during Rev 1 authoring.

4. **Roslyn analyzer scope (Halt 7) — emit warning on `using Sunfish.Blocks.RentCollection;` or only on `Sunfish.Blocks.RentCollection.<Type>` direct references?** Implementation-detail; .NET-architect council resolves at Step 7 PR review.

5. **Does the deprecation cadence (1 release cycle) match Shipyard's release rhythm?** Per ADR 0011 (Bundle Versioning + Upgrade Policy), Shipyard's release cadence is implicit (no fixed schedule yet; release-on-readiness). "One release cycle" therefore translates to "the next time Shipyard tags a release after Step 6 ships." Admiral may want to commit to a calendar date instead (e.g., "shim packages archived 2026-08-01"). Decided OUT of ADR 0098 scope; flagged for Admiral.

---

## 10. References

- PAO source UPF — `coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`
- Admiral ruling authorizing this scaffold — `coordination/inbox/admiral-ruling-2026-05-26T0345Z-pao-routing-block-naming-flight-deck-storymodule.md`
- Three-tier vocabulary memory — `[[three-tier-slotting-vocabulary]]` (canonical fleet vocabulary for tier-1 / tier-2 / tier-3 substrate organization)
- Prefer-cleanest-long-term-option directive — `[[prefer-cleanest-long-term-option]]`
- Substrate claim-beacon protocol — `[[substrate-claim-beacon-protocol]]` (applies to the rename wave Step PRs)
- ADR 0008 (Foundation.MultiTenancy) — `shipyard/docs/adrs/0008-foundation-multitenancy.md` — `IMustHaveTenant` typed-marker precedent
- ADR 0011 (Bundle Versioning + Upgrade Policy) — `shipyard/docs/adrs/0011-bundle-versioning-upgrade-policy.md`
- ADR 0018 (Governance + License Posture) — `shipyard/docs/adrs/0018-governance-and-license-posture.md`
- ADR 0057 (Leasing Pipeline + Fair Housing) — `shipyard/docs/adrs/0057-leasing-pipeline-fair-housing.md` — implementing-block of the rename target #6
- ADR 0059 (Public-Listing Surface) — `shipyard/docs/adrs/0059-public-listing-surface.md` — implementing-block of the rename target #5
- ADR 0069 (ADR Authoring Discipline) — `shipyard/docs/adrs/0069-adr-authoring-discipline.md` — pre-merge council protocol governing this ADR
- ADR 0091 R2 (ITenantContext Divergence Resolution) — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` — substrate-tier ADR cadence precedent (helper + assertion + analyzer)
- ADR 0095 R2 (Bootstrap Context substrate for pre-tenant signup) — `shipyard/docs/adrs/0095-bootstrap-context.md` — substrate-tier ADR shape mirror
- ADR 0096 (Tier-2 Vendor-Provider Substrate) — `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md` — substrate-tier ADR shape mirror
- Slotting general UPF — `shipyard/icm/01_discovery/research/onr-slotting-architecture-7-gap-audit.md` (the precedent for tier-1 / tier-2 / tier-3 vocabulary)

---

## 11. Sources cited

**Primary (publication + retrieval dates):**

1. PAO source UPF — `coordination/inbox/pao-status-2026-05-25T2330Z-flight-deck-media-erp-routing.md`; authored 2026-05-25T23:30Z; retrieved 2026-05-26.
2. Admiral ruling — `coordination/inbox/admiral-ruling-2026-05-26T0345Z-pao-routing-block-naming-flight-deck-storymodule.md`; authored 2026-05-26T03:45Z; retrieved 2026-05-26.
3. shipyard `packages/kernel-lease/README.md` — retrieved 2026-05-26; documents Flease-inspired distributed lease coordinator semantics.
4. shipyard `packages/kernel-lease/ILeaseCoordinator.cs` — retrieved 2026-05-26; canonical distributed-lock interface.
5. shipyard `packages/kernel-lease/Sunfish.Kernel.Lease.csproj` — retrieved 2026-05-26; ProjectReference graph confirms zero property-domain coupling.
6. shipyard `packages/blocks-leases/Sunfish.Blocks.Leases.csproj` — retrieved 2026-05-26; confirms NO ProjectReference to `kernel-lease` (the two share a name but are semantically unrelated).
7. shipyard `packages/blocks-tenant-admin/Sunfish.Blocks.TenantAdmin.csproj` — retrieved 2026-05-26; Description: "tenant profile, users, roles, and bundle-activation surface over IBundleCatalog" (SaaS-tenant admin, not counterparty portal).
8. shipyard `packages/blocks-tenant-admin/Services/` (8 .cs files; tenant-admin services) + `Models/TenantRole.cs` — retrieved 2026-05-26.
9. shipyard `packages/blocks-rent-collection/README.md` — retrieved 2026-05-26; recurring-invoice-and-payment mechanics.
10. shipyard `packages/blocks-inspections/README.md` — retrieved 2026-05-26; inspection-template-and-deficiency mechanics; entity-coupling to `blocks-property-equipment` confirmed.
11. shipyard `packages/blocks-property-leasing-pipeline/README.md` — retrieved 2026-05-26; Inquiry → Prospect → Application → BackgroundCheck → AdverseAction → LeaseOffer lifecycle per ADR 0057.
12. shipyard `packages/blocks-public-listings/README.md` — retrieved 2026-05-26; public-listing entity + tier-redacted projection per ADR 0059; W#28 capability-tier promotion.
13. shipyard `packages/contracts/package.json` — retrieved 2026-05-26; `@sunfish/contracts` TypeScript shared-types package; cross-language naming collision baseline.
14. shipyard ADR 0095 R2 — `shipyard/docs/adrs/0095-bootstrap-context.md`; substrate-tier ADR shape mirror; retrieved 2026-05-26.
15. shipyard ADR 0096 — `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md`; substrate-tier ADR shape mirror; retrieved 2026-05-26.
16. shipyard ADR 0091 R2 — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md`; substrate-tier ADR cadence precedent; retrieved 2026-05-26.

**Secondary:**

17. Three-tier slotting vocabulary memory — `~/.claude/projects/-Users-christopherwood-Projects-Harborline-Software/memory/project_three_tier_slotting_vocabulary.md`; CIC ratified 2026-05-25T21:55Z; retrieved 2026-05-26.
18. Prefer-cleanest-long-term-option memory entry — `~/.claude/.../memory/feedback_prefer_cleanest_long_term_option.md`; CIC directive 2026-05-21; retrieved 2026-05-26.
19. Substrate claim-beacon protocol memory entry — `~/.claude/.../memory/feedback_substrate_claim_beacon_protocol.md`; Engineer 2026-05-26T03:14Z + Admiral 03:30Z; retrieved 2026-05-26.
20. shipyard `_shared/engineering/standing-approved-patterns.md` (pattern-009 SPOT-CHECK scope) — retrieved 2026-05-26.
21. shipyard `icm/01_discovery/research/onr-slotting-architecture-7-gap-audit.md` — three-tier-vocabulary precedent (shipyard#152); retrieved 2026-05-26.

**Tertiary (anecdotal / framework convention):**

22. Flease distributed-lock-coordinator literature (Lamport, Chubby, Flease) — general distributed-systems knowledge informing the kernel-lease-is-not-property-domain finding.
23. .NET `TypeForwardedTo` attribute for binary-compatibility renames — general .NET framework convention.
24. NuGet semantic-versioning conventions for major-version-bump-on-rename — general NuGet packaging convention.
25. `System.Diagnostics.Contracts` namespace + .NET "contract-first programming" convention — general .NET community knowledge informing the `foundation-contracts` vs `foundation-agreements` recommendation.

---

## 12. What ONR does next

Per the Admiral ruling kickoff sequence:

1. This scaffold ships (PR open, status beacon filed naming Halts + scope rec).
2. Admiral consumes the scaffold; rules on the 10 halt conditions (~30 min per the ADR 0095 cadence precedent).
3. Admiral authors ADR 0098 Rev 1 (Admiral territory per ADR 0069) from this scaffold + the halt-condition rulings (~2-3h Opus xhigh).
4. ADR 0098 Rev 1 enters dual-council review (.NET-architect + security-engineering) per Halt 10 + ADR 0095 R2 precedent.
5. Post-ratification:
   - Engineer executes Steps 1-6 rename wave (parallel where possible; ~13-19h total per §6.3).
   - Engineer ships Step 7 analyzer (if Halt 7 = MANDATORY).
   - ONR begins flight-deck#33 Stage-05 hand-off (post-ADR-Accept per Admiral ruling Request C).
6. ONR optionally authors codename-README docs PR (Halt 9) as a separate ~3-4h Sonnet medium subagent dispatch.

— ONR, 2026-05-26
