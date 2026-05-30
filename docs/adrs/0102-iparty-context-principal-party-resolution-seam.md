---
id: 102
title: IPartyContext Principal→Party Resolution Seam
status: Accepted
date: 2026-05-30
proposed-date: 2026-05-30
accepted-date: 2026-05-30
author: Admiral (Fleet Coordinator)
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - principal-party-resolution
  - confused-deputy-prevention
  - tenant-scoped-identity
  - multi-tenant-isolation
  - server-derived-actor-resolution

enables:
  - pm-pilot-pr2-write-surface
  - server-derived-actor-resolution-on-all-write-paths
  - people-foundation-backed-resolver-swap

composes:
  - 91   # ITenantContext Divergence Resolution — the facade injected here is the Authorization sum-interface, NOT the MultiTenancy narrowed variant (signal-bridge#34 trap)
  - 99   # First-Party Session Establishment — the validated principal (sub/tid/roles) this seam reads UserId + TenantId off of; ADR 0099's token carries NO pid, which is precisely why this seam exists
  - 100  # ERPNext Data Import Contract — the party rows the real people-foundation-backed resolver will eventually map against

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-defining identity seam; confused-deputy-relevant. Dual-council MANDATORY (security-engineering + .NET-architect) on the SAME pass that attests shipyard#216. This ADR rides alongside the build PR; it does NOT serialize the build behind a multi-revision Accept cycle (the seam shape is already constrained by the codebase's named follow-up).
---

# ADR 0102 — IPartyContext Principal→Party Resolution Seam

**Status:** Accepted (2026-05-30). Substrate-defining: this ADR pins the canonical seam that maps an
authenticated session principal (UserId within a Tenant) to its domain `PartyId`, and decides **where
that seam lives** and **what security invariants it MUST hold by construction**. It rode alongside the
build PR **shipyard#216** (`feat/party-context-resolution-substrate`, squash-merged to `main` as
`d14a4e6`); the SAME dual-council pass (security-engineering MANDATORY + .NET-architect) attested both
the ADR text and the PR. **Both councils returned GREEN with no amendment** (see Council review
below), so the ADR flips Proposed→Accepted on dual-GREEN + merge as committed.

**Date:** 2026-05-30

**Resolves:** The PM-pilot write surface (and every future write path that records an actor) needs a
`Guid PartyId` for the authenticated caller, but **no canonical UserId→PartyId resolver exists
anywhere in the platform.** This was confirmed empirically (Engineer A2 investigation,
2026-05-30T1205Z — outcome (c), "no resolver exists"): the ADR-0099 session principal carries
`sub`(UserId) / `tid`(TenantId) / roles only — **no `pid`**; `blocks-people-foundation`'s four
interfaces are none of them principal→party; `foundation-authorization`'s `ICurrentUser` exposes
`UserId` + `Roles` only; and `foundation-ship-common`'s `IActorPrincipalResolver` is a different
identity model (crypto `ActorId`→`Principal`, not a session UserId, not a `Party`). Absent a canonical
seam, each write endpoint would be tempted to accept a **body-supplied** `actingPartyId` — re-opening
the D1 confused-deputy bypass. This ADR resolves that gap with the one seam the codebase already
names as its intended home.

**Predecessor ruling:** `coordination/inbox/admiral-ruling-2026-05-30T1220Z-pm-pilot-pr2-a2-disposition-option1-ipartycontext-substrate-go.md`
(Admiral RULE Option 1 — canonical `IPartyContext` substrate, a forced choice under "prefer cleanest
long-term option" + the 0605Z pre-authorization of the paired-substrate-add branch).

**Changelog:**

- **Rev 1 — initial draft (2026-05-30):** authored alongside shipyard#216; reflects the Engineer's
  proposed + shipped surface (substrate-claim 2026-05-30T1243Z + pr-opened 2026-05-30T1249Z). Status
  Proposed pending the dual-council pass on shipyard#216.
- **Rev 2 — Accepted (2026-05-30):** dual-council returned GREEN with no amendment
  (security-engineering @ 2026-05-30, .NET-architect @ 2026-05-30T1305Z); shipyard#216 squash-merged to
  `main` as `d14a4e6`. Status flips Proposed→Accepted. No shape change; the on-disk surface matches this
  ADR's A0 table.

---

## A0 cited-symbol audit

Per the ADR 0093/0096/0097/0099/0100/0101 cited-symbol audit discipline. **Existing & verified** =
present on `shipyard/main` at authoring (cross-referenced against ADR 0101's A0 audit @ origin/main
and the Engineer A2 investigation 2026-05-30T1205Z). **Introduced by this ADR** = ships in
shipyard#216; the dual-council verifies the on-disk surface against this table.

| Symbol / Path | Classification | Source |
|---|---|---|
| `Sunfish.Foundation.Authorization.ITenantContext` (sum-interface facade: IS both `ICurrentUser` and `MultiTenancy.ITenantContext`) | **Existing.** The single injected principal object this seam reads `UserId` + `Tenant.Id` off of. ADR 0091 R2 facade; the signal-bridge#34 CS0104 trap is avoided by injecting THIS, not the MultiTenancy narrowed variant. | ADR 0101 A0 audit (line 105 / compose-91); ADR 0091 |
| `Sunfish.Foundation.Authorization.ICurrentUser` (`UserId` (string) + `Roles`) | **Existing.** Exposes the session `UserId` but NOT a `PartyId` — the gap this seam closes. | Engineer A2 (2026-05-30T1205Z) |
| `Sunfish.Foundation.MultiTenancy.ITenantContext` (`Tenant`, tenant scope) | **Existing.** Source of the ambient `Tenant.Id` that scopes resolution. | ADR 0091 |
| `TenantId` (`Sunfish.Foundation...Assets.Common.TenantId`) | **Existing.** The tenant key type the resolver signature requires. | ADR 0101 A0 audit; Engineer substrate-claim §Placement |
| `Sunfish.Blocks.PeopleFoundation.IPartyWriteService` (xmldoc names the canonical `IPartyContext` as "the Halt-1 follow-up whoever lands across the platform") | **Existing.** This ADR realizes the seam that doc names; verifies the name is not a novel invention. | Engineer A2 — `IPartyWriteService.cs:13-25` |
| ADR-0099 session principal (`sub`/`tid`/roles; **no `pid`**) | **Existing (Accepted ADR 0099).** The validated token this seam derives from; its lack of a `pid` claim is the forcing reason a resolver is required rather than a token-claim read. | ADR 0099 |
| `IPrincipalPartyResolver` (`ValueTask<Guid?> ResolveAsync(string userId, TenantId tenantId, ct)`) | **Introduced by this ADR** (ships in shipyard#216). The swappable mapping primitive. | shipyard#216 |
| `IPartyContext` (`ValueTask<Guid> GetCurrentPartyIdAsync(ct)`, no id params) | **Introduced by this ADR** (ships in shipyard#216). The ambient facade — the canonical name. | shipyard#216 |
| `PartyContext`, `InMemoryPrincipalPartyResolver`, `PrincipalPartyMapping`, `PrincipalPartyResolutionException`, `AddSunfishPartyContext(...)` | **Introduced by this ADR** (ship in shipyard#216). Impl, v1 in-memory resolver + seed record, typed throw, DI helper. | shipyard#216 |

---

## Context

### The gap (empirical, confirmed before designing)

The PM-pilot write services in `shipyard/packages/blocks-work-projects/Services/` are **actor-explicit
by design**: each write method takes a server-supplied `Guid` party id (`actingPartyId` /
`workerPartyId` / `approverPartyId` / `rejecterPartyId` / `createdBy` / `updatedBy`) and **delegates
principal→party resolution to the caller** (the Bridge). That is the correct domain-layer shape — the
domain should not know about HTTP sessions. But it pushes a hard question onto the Bridge: *given the
authenticated session principal, what is its `PartyId`?*

The Engineer's A2 investigation (2026-05-30T1205Z) answered that question with **outcome (c): no
resolver exists anywhere.** None of the four candidate homes provides a session-principal→`PartyId`
mapping:

1. **ADR-0099 session principal** carries `sub` (UserId), `tid` (TenantId), and roles — **no `pid`
   claim.** The token cannot be read for a PartyId.
2. **`blocks-people-foundation`** has four interfaces (`IPass2PartyUpserter`, `IPartyWriteService`,
   `IErpnextPartyImporter`, `IPartyReadModel`) — none is principal→party. `IPartyReadModel` maps
   `party→displayName`, NOT `user→party`. `IPartyWriteService.cs:13-25` itself names the missing
   canonical `IPartyContext` as the Halt-1 follow-up.
3. **`foundation-authorization` `ICurrentUser`** exposes `UserId` + `Roles` only.
4. **`foundation-ship-common` `IActorPrincipalResolver`** is a crypto `ActorId`→`Principal` model —
   not a session UserId, not a `Party`.

### Why a seam, and why now

Without a canonical seam, the path of least resistance for a write-endpoint builder is to accept a
**body-supplied** `actingPartyId` — which is the **D1 confused-deputy bypass**: a caller asserting
"I am acting as party X" with no server-side proof. The seam exists to make the *correct* path the
*only* path: a write handler asks one ambient accessor for "the current principal's PartyId" and gets
a server-derived `Guid`, with no id ever crossing the wire.

This is **not a novel invention** — `IPartyWriteService` already names `IPartyContext` as the platform
follow-up. That materially lowers the architectural risk and is why this ADR rides alongside the build
rather than gating it behind a multi-revision Accept cycle (per the 1220Z ruling).

---

## Decision

**Introduce the canonical principal→party resolution seam as a two-layer Tier-1 substrate in
`foundation-authorization`: a swappable `IPrincipalPartyResolver` primitive beneath an ambient
`IPartyContext` facade, with an in-memory v1 resolver and a Bridge-prod-scope DI registration — such
that the four security invariants hold by construction, not by convention.**

**D1 — Two-layer seam, mirroring how `ITenantContext` sits over its backing.** The split isolates the
**security-critical ambient facade** (what sec-eng cares about) from the **swappable data lookup**
(what changes when the real people-foundation impl lands):

- **`IPrincipalPartyResolver`** — the swappable mapping primitive:
  `ValueTask<Guid?> ResolveAsync(string userId, TenantId tenantId, CancellationToken ct = default)`.
  `null` = no party maps to this `(tenant, userId)`. **Tenant-scoped by signature.** v1 impl
  `InMemoryPrincipalPartyResolver` keys a seeded `(TenantId, userId) → Guid` map. The real
  people-foundation-backed impl is a later swap **behind this seam** (Tier-1 discipline). The `async`
  / `ValueTask` signature future-proofs the DB-backed swap — no later breaking change.
- **`IPartyContext`** — the ambient facade (the canonical name people-foundation already names):
  `ValueTask<Guid> GetCurrentPartyIdAsync(CancellationToken ct = default)`. **No id parameters.**
  Impl `PartyContext` injects the **ONE** `Authorization.ITenantContext` sum-interface, reads
  `.UserId` + `.Tenant.Id` off that **single instance**, and delegates to `IPrincipalPartyResolver`.

**D2 — Placement: `foundation-authorization`, acyclic with zero new package references.**
`foundation-authorization` already references `foundation` (`Assets.Common.TenantId`) +
`foundation-multitenancy` (`MultiTenancy.ITenantContext`, `TenantMetadata`) and houses `ICurrentUser`
/ `IAuthorizationContext` / the `Authorization.ITenantContext` facade. Placing both interfaces + the
in-memory impl + the DI helper here co-locates the seam with the identity family it belongs to and
adds **no new package references**. (The .NET-architect council confirms the acyclic / zero-new-ref
claim against the `.csproj` and rules whether a dedicated `foundation-party-context` package would be
preferable — Engineer left that explicitly open.)

**D3 — `IPartyContext` returns a raw `Guid`, NOT a people-foundation `Party`.** The facade returns the
opaque id only, so the substrate takes **no dependency on `blocks-people-foundation`**. The layering
stays clean: the real people-foundation-backed resolver sits ABOVE this seam later without inverting
it.

**D4 — In-memory v1; real impl is a later swap behind the seam.** Per Tier-1 domain-block discipline
(concrete DI, never runtime-swapped at the *manifest* layer, but the impl behind the interface is
replaceable). The v1 `InMemoryPrincipalPartyResolver` is seeded; the people-foundation-backed impl
(reading real party rows, e.g. post-ERPNext-import per ADR 0100) replaces it behind the same
`IPrincipalPartyResolver` interface with no change to the facade or any consumer.

**D5 — Bridge prod-scope registration.** `AddSunfishPartyContext(...)` (mirroring
`AddSunfishTenantContext`) registers the resolver + the `PartyContext` facade. The Bridge calls this in
prod scope so **every** PM-pilot write endpoint resolves the actor through this ONE seam — never a
body-supplied id. **Lifetimes:** the facade MUST be **scoped** (it depends on the scoped
`ITenantContext`); the resolver MAY be a **singleton** iff its impl captures no scoped dependency (the
in-memory impl holds only its immutable seeded map and receives `userId` + `tenantId` as method
params — no captive dependency). The .NET-architect council confirms the registered lifetimes and the
no-captive-dependency property.

### Security invariants (pinned — realized by construction, verified by the sec-eng SPOT-CHECK)

These ARE the A2 negative-match, realized at the resolver boundary:

1. **Same-token derivation (confused-deputy guard).** `PartyContext` injects ONE
   `Authorization.ITenantContext` and reads `.UserId` AND `.Tenant.Id` off that **same object**. There
   is **no seam** to combine a UserId from one principal with a TenantId from another — they are the
   same instance. This is the strongest form of the guard the `ICurrentUser` doc warns about.
2. **Tenant-scoped resolution.** `IPrincipalPartyResolver.ResolveAsync` takes `TenantId` in its
   signature; the facade passes the ambient principal's `Tenant.Id`; the in-memory impl keys on
   `(TenantId, userId)`. A tenant-A principal **cannot** resolve a tenant-B party.
3. **Never body-supplied (D1-bypass guard).** `IPartyContext.GetCurrentPartyIdAsync` takes **no** id
   parameters, so no write path can thread a body-supplied
   `actingPartyId`/`workerPartyId`/`approverPartyId`/`rejecterPartyId`/`createdBy`/`updatedBy`/`partyId`.
   The bypass is closed **by construction**.
4. **F4 self-approval single-resolution.** A write handler calls `GetCurrentPartyIdAsync` **once** and
   uses that single resolved `PartyId` on **both** sides of the approver-vs-`TimeEntry.WorkerPartyId`
   equality check — never two independent resolutions.

**Fail-closed corollary (sec-eng MANDATORY check):** when the resolver returns `null` (mis-provisioned
principal — a UserId with no mapped party) OR when no authenticated principal is present,
`GetCurrentPartyIdAsync` MUST throw `PrincipalPartyResolutionException`. It MUST NOT fall through to
`Guid.Empty` / `default(Guid)` — an all-zeros PartyId leaking to a write path would be a
confused-deputy hole.

---

## Consequences

**Positive:**

- The PM-pilot write surface gets a canonical, server-derived actor — the body-supplied `actingPartyId`
  bypass is closed by construction, not by per-endpoint vigilance.
- Realizes the exact seam `blocks-people-foundation` already names — zero architectural novelty, low
  risk, high consistency with the identity family.
- The two-layer split lets the real people-foundation-backed resolver drop in behind
  `IPrincipalPartyResolver` later with no consumer change and no layering inversion.
- Reuses the proven `ITenantContext` ambient-facade shape; the same-token guard is structural.

**Negative / costs:**

- **A seeded in-memory resolver is not the production data source.** v1 maps `(TenantId, userId) →
  Guid` from a seed; until the people-foundation-backed impl lands, a real principal must be seeded to
  resolve. This is acceptable for the PM-pilot path (the seam is correct; the data source swaps
  behind it) and is the same in-memory-first discipline as the other ADR-009x substrates.
- **One more DI registration on the Bridge prod path** (`AddSunfishPartyContext`). Mechanical;
  mirrors `AddSunfishTenantContext`.

**Rollback / revisit:**

- Rollback = git revert of shipyard#216 (the in-memory first slice is side-effect-free; no migration
  runs).
- **Revisit if:** (a) the real people-foundation-backed resolver needs a different signature (e.g. a
  batch resolve) — extend `IPrincipalPartyResolver` additively; (b) a future identity model needs
  principal→party for a non-session actor (service principals, API keys) — that is a *sibling* resolver
  behind the same facade pattern, not a change to this seam; (c) CIC mandates the seam be isolated to a
  dedicated `foundation-party-context` package — a mechanical move, no contract change.
- **Kill trigger:** RED from either council on the same-token-derivation or fail-closed-null invariants
  blocks the merge and forces an amendment (the ADR + PR amend together).

---

## Council review (MANDATORY — the SAME pass attests this ADR and shipyard#216)

Per the substrate-tier Halt cadence (ADR 0095/0096/0097/0098/0099/0100/0101) AND the 1220Z ruling's
dual-council-on-substrate-PR-open requirement, this seam carries **dual-council MANDATORY**:

- **security-engineering — MANDATORY — GREEN.** The four pinned invariants + the fail-closed null-path
  determination, verified with file:line evidence against the shipyard#216 surface. All four invariants
  PASS; the fail-closed null-path check PASSES (`PartyContext.GetCurrentPartyIdAsync` throws on
  no-principal, unresolved-tenant, AND resolver-null — `PartyContext.cs:36-39, :43-44`; non-nullable
  `Guid` + `?? throw` structurally forbid a `Guid.Empty` leak); same-token derivation is structural
  (`ITenantContext.cs:44-48` sum-interface; `TenantContextServiceCollectionExtensions.cs:39-43`
  registers all facets from the same concrete instance). No amendment required.
  (`coordination/inbox/council-verdict-sec-eng-2026-05-30-shipyard-216-ipartycontext-substrate.md`.)
- **.NET-architect — GREEN.** All five reviewed dimensions SOUND: placement in
  `foundation-authorization` is correct and adds **zero** new package references (`.csproj`
  byte-identical to `main`, acyclic) — a dedicated `foundation-party-context` package is REJECTED as
  net-negative; naming matches the seam `IPartyWriteService.cs:24` already documents by name;
  layering returns a raw `Guid` (no people-foundation edge); `ValueTask` signatures future-proof the
  DB-backed swap. The load-bearing finding: the captive-dependency risk is **absent by construction**
  — `AddSunfishPartyContext` registers the resolver SINGLETON + facade SCOPED, and
  `InMemoryPrincipalPartyResolver` injects only an immutable seeded `IReadOnlyDictionary` (no scoped
  service), while the scoped `PartyContext` passes `userId` + `tenant.Id` as method params, so the
  singleton holds zero request state and is thread-safe. No amendment required.
  (`coordination/inbox/council-verdict-net-arch-2026-05-30-shipyard-216-ipartycontext-substrate.md`.)

Both councils wrote a `council-verdict-*-shipyard-216-*` beacon and returned **GREEN with no
amendment**. shipyard#216 squash-merged to `main` as `d14a4e6` on CI-green; this ADR is **Accepted**.

**Out-of-scope note carried forward (not blocking; for the future people-foundation-backed swap):**
when `IPrincipalPartyResolver` becomes DB-backed, the swap author MUST re-evaluate the singleton
lifetime — a resolver injecting a scoped `DbContext`/connection CANNOT remain singleton (that would be
the captive-dependency bug the v1 correctly avoids). The seam is shaped to allow scoped re-registration
of the resolver with **no facade change**; the documented invariant is "resolver lifetime is the
implementer's call; the facade stays scoped." (.NET-architect, out-of-scope observation.)

---

## References

- `coordination/inbox/admiral-ruling-2026-05-30T1220Z-pm-pilot-pr2-a2-disposition-option1-ipartycontext-substrate-go.md` — the Option-1 ruling this ADR records.
- `coordination/inbox/engineer-question-2026-05-30T1205Z-pr2-a2-principal-resolution-no-resolver-substrate.md` — the A2 investigation (outcome (c)).
- `coordination/inbox/engineer-status-2026-05-30T1243Z-ipartycontext-substrate-claim.md` + `...1249Z-shipyard-216-ipartycontext-substrate-pr-opened.md` — the proposed + shipped surface.
- shipyard#216 (`feat/party-context-resolution-substrate`) — the build PR this ADR rides alongside.
- `packages/foundation-authorization/` — the seam's home (houses `ICurrentUser` / `IAuthorizationContext` / the `Authorization.ITenantContext` facade).
- `packages/blocks-people-foundation/Services/IPartyWriteService.cs:13-25` — names the canonical `IPartyContext` as the Halt-1 follow-up this realizes.
- ADR 0091 (ITenantContext Divergence — facade), 0099 (Session Establishment — the principal this derives from), 0100 (ERPNext Data Import — the eventual party rows).
