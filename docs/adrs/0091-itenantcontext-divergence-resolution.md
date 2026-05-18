---
id: 91
title: ITenantContext Divergence Resolution (Foundation.Authorization → Foundation.MultiTenancy + ICurrentUser + IAuthorizationContext)
status: Proposed
date: 2026-05-17
tier: foundation
pipeline_variant: sunfish-api-change
concern:
  - multi-tenancy
  - security
  - api-evolution
enables:
  - canonical-tenant-context-surface
  - data-plane-control-plane-separation-by-type
composes:
  - 8
  - 31
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0091 — ITenantContext Divergence Resolution

**Status:** Proposed
**Date:** 2026-05-17
**Resolves:** The "follow-up ADR" [ADR 0008](./0008-foundation-multitenancy.md) §"Relationship to existing types" explicitly promised: *"A follow-up ADR will decompose `Sunfish.Foundation.Authorization.ITenantContext` into tenant / caller / authorization concerns once a concrete migration is ready."* Also resolves the cohort-1 React-rebind drift surfaced by FED's `IBrowserTenantContext`-vs-`ITenantContext` ergonomics complaint and Admiral directive `admiral-directive-2026-05-17T23-15Z-onr-ws-e-handoff-authoring-and-itenantcontext-discovery` §"Track B."

**File-naming note (for Admiral / council):** Admiral's directive named this file `docs/adrs/0031-itenantcontext-divergence-resolution.md`. Slot 0031 is occupied by *Bridge as Hybrid Multi-Tenant SaaS* (Accepted 2026-04-23), which is upstream of this divergence. ONR filed at the next free slot 0091 to avoid collision; rename to a different number if Admiral prefers a slot closer to 0008. Cross-reference to ADR 0008 + 0031 is preserved either way.

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Authorization.ITenantContext` | Existing (4-member overloaded interface; `shipyard/packages/foundation/Authorization/ITenantContext.cs`) | yes |
| `Sunfish.Foundation.MultiTenancy.ITenantContext` | Existing (2-member pure-tenant interface; `shipyard/packages/foundation-multitenancy/ITenantContext.cs`) | yes |
| `Sunfish.Bridge.Middleware.IBrowserTenantContext` | Existing (6-member subdomain-resolved interface; `signal-bridge/Sunfish.Bridge/Middleware/IBrowserTenantContext.cs`) | yes |
| `Sunfish.Bridge.Client.Services.IBridgeRequestContext` | Existing (Blazor-circuit composition of `IBrowserTenantContext` + auth claim; `signal-bridge/Sunfish.Bridge.Client/Services/IBridgeRequestContext.cs`) | yes |
| `Sunfish.Foundation.MultiTenancy.IMustHaveTenant` | Existing (per-entity tenant-scoping marker per ADR 0008) | yes |
| `Sunfish.Foundation.MultiTenancy.TenantMetadata` | Existing (per ADR 0008 §"Surface") | yes |
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing (per ADR 0008 §"Relationship to existing types"; namespace move deferred) | yes |
| `Sunfish.Bridge.Authorization.DemoTenantContext` | Existing (`signal-bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs`) | yes |
| `Sunfish.Bridge.MigrationService.MigrationTenantContext` | Existing (`signal-bridge/Sunfish.Bridge.MigrationService/MigrationTenantContext.cs`) | yes |
| `Sunfish.Foundation.Authorization.ICurrentUser` | **Introduced** (Option B + Option C decompose target) | n/a — proposed |
| `Sunfish.Foundation.Authorization.IAuthorizationContext` | **Introduced** (Option B decompose target) | n/a — proposed |
| ADR 0008 — `foundation-multitenancy` substrate | Existing (cited; promises the follow-up this ADR fulfills) | yes |
| ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS | Existing (cited; establishes the Zone-A vs Zone-C separation that produces #3) | yes |
| Paper §17.2 — Hosted relay as managed SaaS | Existing (cited; ciphertext-at-rest constraint that forces data-plane vs control-plane split) | yes |

---

## Context

The Harborline fleet has **four distinct "tenant context" interface surfaces** in production code today:

| # | Interface | Namespace / Path | Members | Purpose |
|---|---|---|---|---|
| 1 | `ITenantContext` (legacy / overloaded) | `Sunfish.Foundation.Authorization` (shipyard `packages/foundation/Authorization/`) | `TenantId: string`, `UserId: string`, `Roles: IReadOnlyList<string>`, `HasPermission(string)` | "Current request scope" — conflates tenant identity + user identity + role list + authz check. Backs Bridge **control-plane** code (signup, billing, tenant-admin, cockpit endpoints, DbContext query filters, MigrationService). |
| 2 | `ITenantContext` (single-responsibility) | `Sunfish.Foundation.MultiTenancy` (shipyard `packages/foundation-multitenancy/`) | `Tenant: TenantMetadata?`, `IsResolved: bool` | "Pure tenant identity" — per ADR 0008's intent. **Zero production consumers today**; the package's other types (`IMustHaveTenant`, `ITenantResolver`, `ITenantCatalog`, `TenantMetadata`) are used by ~9 shipyard packages, but `ITenantContext` itself remains orphaned pending the migration ADR 0008 promised. |
| 3 | `IBrowserTenantContext` | `Sunfish.Bridge.Middleware` (signal-bridge) | `IsResolved`, `TenantId: Guid`, `Slug: string`, `TrustLevel`, `TeamPublicKey: byte[]?`, `AuthSalt: byte[]`, `Bind(...)` | **Data-plane tenant** — subdomain-resolved by `TenantSubdomainResolutionMiddleware`. Carries crypto primitives (Ed25519 team public key, Argon2id salt) needed by the Bridge browser shell to verify passphrase-signed challenges and proxy to the right hosted-node-per-tenant peer. Per paper §17.2 ciphertext-at-rest invariant, the operator can't hold OIDC-claims-backed authz here. |
| 4 | `IBridgeRequestContext` | `Sunfish.Bridge.Client.Services` (signal-bridge) | `IsResolved`, `TenantId`, `ActorId` | Blazor-circuit composition of #3's tenant + an authenticated `sub` claim. Captured at initial HTTP connection; stable across SignalR rendering. Not really a separate "tenant context" — a request-scoped snapshot for the Blazor data-flow seam. |

**The problem framings on the table:**

- **Naming collision.** Two production interfaces both called `ITenantContext` (#1, #2) live in different namespaces. `SunfishBridgeDbContext` imports both (`using Sunfish.Foundation.Authorization;` *and* `using Sunfish.Foundation.MultiTenancy;`) but currently consumes only the Authorization-flavored one. Reviewer ergonomics suffer; IDE autocomplete shows two `ITenantContext`s and the picker has no way to disambiguate intent.
- **Overloaded interface (#1).** ADR 0008 §"Tenancy concepts already exist…" already documented the anti-pattern: `Foundation.Authorization.ITenantContext` *"bundles three distinct concerns into one interface"* and *"consumers depending on any one of these concerns end up depending on all three."* The MigrationService's `MigrationTenantContext` returns `Roles = []` and `HasPermission = false` because migrations don't have an authz concept — but the interface forces them to claim they do.
- **Necessary divergence between control plane (#1 or its successor) and data plane (#3).** Paper §17.2's "operator stores ciphertext only; role keys remain on end-user devices" constraint means the data plane *cannot* use OIDC-claims-backed authz. Bridge's data plane authenticates via subdomain + passphrase-signed challenges, not via the Microsoft Identity stack. #3 is **architecturally distinct from #1**, not accidentally divergent.
- **What the cohort-1 React-rebind work surfaced.** When FED rebound a page from a Bridge endpoint to React, the FED-side React code wanted to call into the same "tenant context" the Razor pages did, but the Razor pages use #1 (Authorization) and the new SPA pages route through #3 (BrowserTenantContext). The two surfaces share `TenantId` but the types differ (`string` vs `Guid`). Without a typed canonical "tenant identity", every cross-surface boundary needs ad-hoc conversion code.

The divergence between **#1 and #2** is *accidental* (transitional drift waiting on the migration ADR 0008 promised). The divergence between **#2/#1 and #3** is *necessary* (paper-mandated by §17.2). The divergence between **#3 and #4** is *necessary* (Blazor-circuit lifetime vs. middleware-resolution lifetime).

---

## Decision drivers

- **ADR 0008's explicit promise.** §"Relationship to existing types" says *"A follow-up ADR will decompose [Authorization.ITenantContext] into tenant / caller / authorization concerns once a concrete migration is ready."* This ADR is that follow-up.
- **Paper §17.2 ciphertext-at-rest invariant.** The control-plane / data-plane separation is paper-mandated. Any reconciliation must preserve this; collapsing #3 into #1 would violate the invariant.
- **Cohort-1 React-rebind ergonomics.** FED's rebind cohort is in flight (Wave 5.3.x). Surface-area churn now is acceptable if it converges; surface-area churn later costs every rebound page twice.
- **Single-responsibility principle for foundation tier.** ADR 0069 (ADR authoring discipline) and the Wolverine review pattern penalize "request-scope blob" abstractions. The Authorization-flavored `ITenantContext` is exactly that.
- **Test ergonomics.** Every test that needs a tenant context today implements all 4 members of `Sunfish.Foundation.Authorization.ITenantContext` (TenantId + UserId + Roles + HasPermission), even when the SUT only reads `TenantId`. 10+ test files in `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/` show this duplication.
- **Source compatibility for ~14 control-plane consumers.** Bridge endpoints (`CockpitEndpoints`, `VendorsEndpoint`, `WorkOrdersEndpoint`, `PropertyDetailEndpoint`, `DashboardEndpoint`, `LeasesEndpoints`, `PropertiesEndpoints`) inject `Sunfish.Foundation.Authorization.ITenantContext` directly today. Migration ergonomics matter.
- **Security review surface.** Any decomposition touches authz claim flow. Security-engineering council review is mandatory before acceptance.

---

## Considered options

### Option A — Document the 4-surface picture; do not decompose

Update ADR 0008's "follow-up ADR will decompose…" promise to "the 4 surfaces are canonical and distinct; here's the documentation." Leave the codebase shape unchanged. Add an `apps/docs/foundation/multitenancy/tenant-context-disambiguation.md` page that maps consumer → correct interface.

- **Pro:** Zero migration cost. The XML docs on #1, #2, #3 already document the separation; we'd just consolidate that into one operator-facing page.
- **Pro:** Naming collision (#1 / #2) is policy-managed via `using` directives, which compilers + analyzers already enforce.
- **Pro:** No surface-area churn during the cohort-1 rebind work.
- **Con:** Doesn't fulfill ADR 0008's explicit promise; "follow-up ADR" remains pending in perpetuity.
- **Con:** Foundation.Authorization.ITenantContext's overload **is** an anti-pattern (per ADR 0008's own framing). Documenting around it doesn't address the problem.
- **Con:** Test code continues to implement 4 members when only 1 is needed.
- **Con:** Cohort-1 React rebind ergonomics issues persist; every cross-surface boundary needs ad-hoc conversion.

**Verdict:** Rejected unless evals show Option B's migration cost exceeds the value. The promise is on the books; explicit "we changed our mind" supersession is fine but punting indefinitely isn't.

### Option B — Decompose Foundation.Authorization.ITenantContext into three single-responsibility interfaces; migrate consumers [RECOMMENDED]

Decompose the legacy `Sunfish.Foundation.Authorization.ITenantContext` into three interfaces, each with a single responsibility:

```csharp
// In Sunfish.Foundation.MultiTenancy (existing package; canonical home for tenant resolution)
public interface ITenantContext
{
    TenantMetadata? Tenant { get; }
    bool IsResolved { get; }
}

// In Sunfish.Foundation.Authorization (existing package; renamed to its real responsibility)
public interface ICurrentUser
{
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
}

public interface IAuthorizationContext
{
    bool HasPermission(string permission);
    // Future: HasPermissionInScope(Permission permission, TenantId scope)
}
```

Consumers that genuinely need "tenant + user + authz" inject all three. Consumers that need only one inject only one (this is the unit-of-substitution win — `MigrationTenantContext` becomes "no user, no authz, just a tenant").

**Migration path** (proposed; council confirms):

1. **Step 1 — Introduce the three new interfaces** in their target packages. Authorization-side `ITenantContext` retains all four members as a transitional facade implementing all three new interfaces. Zero consumer changes. (Single PR; advisory council.)
2. **Step 2 — Migrate consumers incrementally.** Each Bridge endpoint flips its injected dependency from `ITenantContext` to the narrower one it actually uses (~14 endpoints; one PR per ~3 endpoints, batched). Each migration is reviewable in isolation.
3. **Step 3 — Migrate tests.** Test files that mock all 4 members switch to the narrower one. Removes ~10 duplicate test fixture classes.
4. **Step 4 — Deprecate `Sunfish.Foundation.Authorization.ITenantContext`** with `[Obsolete]` + ADR-supersession header.
5. **Step 5 — Delete** after a one-cohort grace period (next planning wave's release boundary).

**Renaming questions for council:**

- Should the new `ICurrentUser` keep `string UserId` or switch to a typed `UserId` record-struct? The ADR-0008-style `TenantId` precedent argues for typed; the test ergonomics argue for staying string for now.
- `IAuthorizationContext.HasPermission` is the current shape; council may want to swap to a richer claims-based model (Microsoft.AspNetCore.Authorization conventions) — flagged as **Open question O-1**.

#### Implications for #3 (`IBrowserTenantContext`)

`IBrowserTenantContext` is **NOT** decomposed under Option B. It composes the new `Foundation.MultiTenancy.ITenantContext` semantically — both expose tenant resolution — but #3's crypto-primitive members (`TrustLevel`, `TeamPublicKey`, `AuthSalt`) belong on a data-plane-specific interface, not the foundation surface.

**Recommended cleanup (sub-optional):** rename `IBrowserTenantContext` to `ITenantSubdomainContext` to make the *role* (subdomain-resolved data-plane tenant) clearer than the *deployment-surface* ("browser shell"). The Bridge browser shell is one consumer; the subdomain-resolution role might add others (e.g., the hosted-node mTLS gateway could resolve a tenant by SNI and consume the same interface). Flagged as **Open question O-2**.

#### Implications for #4 (`IBridgeRequestContext`)

`IBridgeRequestContext` is a Blazor-circuit lifetime adapter. Its existence is justified by the Blazor InteractiveServer lifetime mismatch (HttpContext becomes null during SignalR rendering). Under Option B, it still composes `IBrowserTenantContext` + an `ActorId` — internally restructured to use the new `Foundation.MultiTenancy.ITenantContext` for the tenant-only part and `ICurrentUser` for the actor-only part. No external API change.

- **Pro:** Fulfills ADR 0008's explicit promise.
- **Pro:** Test code shrinks (4-member fixture → 1-member fixture in most cases).
- **Pro:** Foundation tier converges on single-responsibility shape (matches ADR 0069's discipline).
- **Pro:** Cross-surface boundaries become typed (`TenantMetadata` is the foundation type both #1's successor and #3 expose).
- **Pro:** Reviewer ergonomics improve — `using` directive disambiguation pressure is gone (no two interfaces named `ITenantContext`).
- **Pro:** Cohort-1 rebind can use the new interfaces directly without migration churn (they're additive in Step 1).
- **Con:** Migration cost spread across 4 steps; ~5–8 PRs end-to-end across ~3 weeks of Engineer time.
- **Con:** Step 1 introduces a transitional facade (Authorization-side `ITenantContext` implements all three new interfaces) that itself has to be tested + reviewed.
- **Con:** Authorization-side namespace becomes home to two pieces (`ICurrentUser`, `IAuthorizationContext`) where ADR 0006's intent was that authorization is one concept. Council may prefer a different package home (`Sunfish.Foundation.Identity` is a candidate).

**Verdict:** Recommended. Cost is bounded + understood; benefit is the explicitly-promised decomposition + tangible test/reviewer ergonomics + cross-surface typing.

### Option C — Rename for clarity; keep the 4 surfaces as distinct interfaces; no decomposition

Rename to remove the naming collision but keep the conflated `IRequestContext`-style interface for control plane:

```csharp
// Renamed from Sunfish.Foundation.Authorization.ITenantContext
public interface IControlPlaneRequestContext   // or IBridgeAuthContext
{
    string TenantId { get; }
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}

// Unchanged
public interface Sunfish.Foundation.MultiTenancy.ITenantContext { /* ... */ }

// Renamed for role-clarity (no semantic change)
public interface Sunfish.Bridge.Middleware.ITenantSubdomainContext { /* ... */ }
```

- **Pro:** Removes naming collision (one canonical `ITenantContext`).
- **Pro:** No decomposition churn; existing consumers update only `using` directives + class names.
- **Pro:** Test fixtures don't need narrowing; current 4-member impls keep working under new names.
- **Pro:** Smaller scope than Option B; ships in ~2 PRs.
- **Con:** Doesn't address ADR 0008's "decompose" promise — the conflation persists, just with a different name.
- **Con:** The naming `IControlPlaneRequestContext` admits the design smell rather than fixing it. Reviewers reading new code still see "request-scope blob" patterns and have to write the same authz lint warnings on every code review.
- **Con:** Test code stays at 4-member fixtures.

**Verdict:** Rejected. Solves the naming collision without solving the underlying anti-pattern. Acceptable as a Phase 1 of Option B if council wants to land the rename first and decompose in a follow-on ADR — but then this ADR amends rather than supersedes itself.

---

## Decision

**Adopt Option B.** Decompose `Sunfish.Foundation.Authorization.ITenantContext` into three single-responsibility interfaces; migrate consumers in 5 phased steps; deprecate the legacy interface. Preserve `IBrowserTenantContext` (#3) and `IBridgeRequestContext` (#4) as architecturally-necessary surfaces. Optionally rename `IBrowserTenantContext` → `ITenantSubdomainContext` (Open question O-2; council decides separately).

The decomposition is conservative: each new interface has a single responsibility, the migration is staged so each PR is reviewable in isolation, and the facade pattern in Step 1 means no consumer breaks on day one.

### Surface introduced

```csharp
// Sunfish.Foundation.MultiTenancy — canonical tenant resolution (ITenantContext already exists; this is its first production consumer)
public interface ITenantContext
{
    TenantMetadata? Tenant { get; }
    bool IsResolved => Tenant is not null;
}

// Sunfish.Foundation.Authorization — narrowed to identity claims
public interface ICurrentUser
{
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
}

// Sunfish.Foundation.Authorization — narrowed to authz check
public interface IAuthorizationContext
{
    bool HasPermission(string permission);
}

// Sunfish.Foundation.Authorization — transitional facade (Step 1; deprecated in Step 4)
[Obsolete("Use Foundation.MultiTenancy.ITenantContext + Foundation.Authorization.ICurrentUser + Foundation.Authorization.IAuthorizationContext per ADR 0091. This facade will be removed after the cohort migration completes.")]
public interface ITenantContext
{
    string TenantId { get; }
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}
```

### Surface removed (after Step 5)

- `Sunfish.Foundation.Authorization.ITenantContext` (existing 4-member interface)

### Surface unchanged

- `Sunfish.Bridge.Middleware.IBrowserTenantContext` (with optional rename per O-2)
- `Sunfish.Bridge.Client.Services.IBridgeRequestContext` (internal restructure only)
- All `IMustHaveTenant`, `ITenantResolver`, `ITenantCatalog`, `TenantMetadata`, `TenantStatus` types in `Foundation.MultiTenancy`

---

## Impact surface estimate

| Surface | Touch count (estimate; verify before each PR) |
|---|---|
| Production code consuming `Sunfish.Foundation.Authorization.ITenantContext` | ~14 files in `signal-bridge/` (Bridge endpoints + DbContext + MigrationService + DemoTenantContext) |
| Test code implementing `: ITenantContext` (4-member fixture) | ~10 files in `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/` |
| Production code consuming `Sunfish.Foundation.MultiTenancy.ITenantContext` | 0 today (becomes ~14 after Step 2) |
| Production code consuming `IBrowserTenantContext` | ~8 files in `signal-bridge/Sunfish.Bridge/` (Middleware + Proxy + Features/Identity) |
| Production code consuming `IBridgeRequestContext` | 1 file (`signal-bridge/Sunfish.Bridge.Client/Services/`); internally updated |
| ADR cross-references (ADR 0008, ADR 0031) | 2 ADRs add a "see ADR 0091" footnote |
| Documentation (`apps/docs/foundation/multitenancy/tenant-context.md`) | 1 file substantial rewrite |

**Estimated Engineer time:** 6–10 hours across 5 phased PRs:

- Step 1 (introduce + facade): ~2h, 1 PR, advisory council
- Step 2 (migrate ~14 Bridge endpoints): ~3h, 3 PRs (batched), advisory council per PR
- Step 3 (migrate ~10 test fixtures): ~1h, 1 PR (mechanical)
- Step 4 (deprecate + announce): ~0.5h, 1 PR
- Step 5 (delete legacy): ~0.5h, 1 PR (after grace period)

**Pre-merge council:** **MANDATORY** for Step 1 (security-engineering + .NET architect — establishes new authz claim flow). Advisory only for Steps 2–5 (mechanical or contract-stable migrations).

---

## Compatibility plan

### Compile-time

- Step 1 facade preserves source compatibility for all 14 production consumers. They compile unchanged.
- New consumers (cohort-1 React rebind work in flight, future Phase 2 blocks) consume the new narrower interfaces from day one.

### Runtime

- DI registration in `Program.cs` registers all three new interfaces against the same concrete `DemoTenantContext`/claims-backed impl until Step 4. After Step 4, concrete impl is split (or kept unified depending on Step 1 council ruling).
- `SunfishBridgeDbContext`'s `using Sunfish.Foundation.MultiTenancy;` directive (currently imported but unused-in-consumption) becomes consumed in Step 2 — eliminates the dead-import situation.

### Security claim flow

- `DemoTenantContext` (development) implements all three new interfaces with the same hardcoded values. No security change.
- Production claims-backed impl (mentioned in `DemoTenantContext`'s xmldoc: *"replaced by a claims-backed ITenantContext that reads the authenticated tenant from OIDC/Entra/Okta"*) maps OIDC claims to the three interfaces. Council reviews the mapping carefully — particularly which claim drives `ICurrentUser.UserId` vs. `IAuthorizationContext.HasPermission`.

### Migration of `IBrowserTenantContext` to `ITenantSubdomainContext` (if O-2 council-approved)

- 8 consumer files updated (mechanical rename).
- `IBrowserTenantContext` retained as a `[Obsolete]` type alias for one cohort grace period (or removed immediately if council says).

---

## Implementation checklist (Step 1 — initial PR)

- [ ] `Sunfish.Foundation.MultiTenancy.ITenantContext` documented as canonical (XML doc updated to drop the "no production consumers yet" caveat)
- [ ] `Sunfish.Foundation.Authorization.ICurrentUser` interface added with full XML doc
- [ ] `Sunfish.Foundation.Authorization.IAuthorizationContext` interface added with full XML doc
- [ ] Transitional facade: `Sunfish.Foundation.Authorization.ITenantContext` marked `[Obsolete(message, error: false)]` with ADR 0091 reference and "use the three new interfaces" guidance
- [ ] `DemoTenantContext` implements all three new interfaces (single class; transitional)
- [ ] `MigrationTenantContext` implements only `Foundation.MultiTenancy.ITenantContext` (no user, no authz — eliminates the empty-roles + always-false-HasPermission hack)
- [ ] DI registration in `Program.cs` registers `DemoTenantContext` as all three (`AddScoped<ITenantContext>` + `AddScoped<ICurrentUser>` + `AddScoped<IAuthorizationContext>` all → `DemoTenantContext`)
- [ ] `apps/docs/foundation/multitenancy/tenant-context.md` rewritten to document the new 3-interface decomposition + the 4-surface fleet picture (with #3 + #4 noted as architecturally-distinct adjacencies)
- [ ] Security-engineering + .NET architect council approval

---

## Open questions for council

| ID | Question | Resolution path |
|---|---|---|
| **O-1** | Should `IAuthorizationContext.HasPermission(string)` evolve toward Microsoft.AspNetCore.Authorization's claims-based model, or stay simple-string-permission for now? | Stage 03 design — defer until first non-Bridge accelerator consumer (Anchor lite-mode auth, hosted-node admin surfaces); for Step 1 keep current shape |
| **O-2** | Rename `IBrowserTenantContext` → `ITenantSubdomainContext`? | Council ruling at Step 1 review; orthogonal to Option B/C choice |
| **O-3** | Should `ICurrentUser` and `IAuthorizationContext` move to a new `Sunfish.Foundation.Identity` package (cleaner separation from `Authorization`)? | Council ruling at Step 1 review; namespace move is independently a breaking change so council weighs ergonomics |
| **O-4** | When the production OIDC-claims-backed `ITenantContext` impl ships (currently only `DemoTenantContext` exists), should it be one class implementing all three, or three classes? | Production-impl ADR (separate; depends on claim-flow design); not blocking Step 1 |
| **O-5** | Cohort-1 React-rebind cohort — do those PRs adopt the new interfaces directly (Step 2 work folds into the rebind cohort) or proceed against the facade and migrate later? | FED + Engineer decide based on rebind cohort velocity; either is acceptable under this ADR |

---

## Open questions surfacing security-relevant concerns

Per Admiral directive's halt condition (*"if ITenantContext divergence turns out to be load-bearing for security (auth claims, etc.) — file `onr-question-*` immediately; security-engineering council needed"*), ONR **affirmatively flags this ADR as security-relevant** and surfaces these specific points for security-engineering review:

1. **Authz claim flow at the facade.** The transitional facade in Step 1 implements both `ICurrentUser` (reads `UserId`/`Roles` from the same concrete impl) AND `IAuthorizationContext` (reads `HasPermission` from the same concrete impl). Council should confirm the claim mapping doesn't introduce a confused-deputy seam.
2. **Cross-surface tenant identity.** `Foundation.Authorization.ITenantContext.TenantId` is `string`; `Foundation.MultiTenancy.ITenantContext.Tenant.Id` is `TenantId` (typed wrapper around Guid); `IBrowserTenantContext.TenantId` is `Guid`. The migration must preserve identity equivalence under string ↔ Guid conversion (per ADR 0008's deferred-namespace-move on `TenantId`).
3. **DbContext query-filter dependency.** `SunfishBridgeDbContext` currently captures `_currentTenantId` from `Foundation.Authorization.ITenantContext.TenantId` (string). Migration to the new `Foundation.MultiTenancy.ITenantContext.Tenant.Id` (typed) is a query-filter rewrite. Council confirms tenant-scoped EF query behavior is preserved (no row-leakage, no silent unscoped queries).
4. **DemoTenantContext warning-once logging.** The current code logs a one-process warning when the demo seam is active. Step 1's facade preservation maintains this warning. Council confirms the warning text references ADR 0091 + ADR 0031 + ADR 0008 once the migration completes.
5. **`IBrowserTenantContext` MUST-NOT-mix-pipelines invariant.** The XML doc on #3 explicitly says it MUST NOT be mixed with #1 in a single request pipeline. This ADR preserves that invariant — the new `Foundation.MultiTenancy.ITenantContext` is intended for **non-request-pipeline contexts** (DB scoping, background jobs, migration runners), not for cross-deployment-surface composition. Council confirms the pipeline-mixing prohibition is not weakened.

These do not block authoring this ADR Proposed; they are the council review surface for moving to Accepted.

---

## Revisit triggers

- **OIDC-claims-backed production impl is implemented** — the concrete claims mapping may surface that `ICurrentUser` and `IAuthorizationContext` need different shape (e.g., `IReadOnlyList<Claim>` instead of `IReadOnlyList<string>`).
- **Anchor lite-mode auth scheme lands** — if Anchor's Zone-A auth (passphrase-keys-on-device, no OIDC server) produces a third concrete impl, the interface decomposition may need refinement.
- **A hosted-node-admin surface ships** — the operator-side admin surface for managing tenant hosted-node config may consume a new "operator context" abstraction; ensure it doesn't overload `Foundation.Authorization` again.
- **`TenantId` namespace move** — the deferred move of `Foundation.Assets.Common.TenantId` → `Foundation.MultiTenancy.TenantId` per ADR 0008. When that lands, the new `ITenantContext` types align; until then, the conversion code stays.

---

## References

### Predecessor ADRs

- [ADR 0008 — Foundation.MultiTenancy](./0008-foundation-multitenancy.md) — explicitly promises the decomposition this ADR delivers
- [ADR 0031 — Bridge as Hybrid Multi-Tenant SaaS](./0031-bridge-hybrid-multi-tenant-saas.md) — establishes the Zone-A / Zone-C separation that makes `IBrowserTenantContext` architecturally distinct
- [ADR 0006 — Bridge SaaS shell](./0006-bridge-saas-shell.md) — the original framing that produced `DemoTenantContext`
- [ADR 0069 — ADR Authoring Discipline](./0069-adr-authoring-discipline.md) — §A0 cited-symbol audit + single-responsibility framing

### Paper

- §17.2 — Hosted relay as managed-SaaS deployment (ciphertext-at-rest invariant that forces #3's design)
- §20.7 — Architecture Selection Framework (Zone-A vs Zone-C separation)

### Code under review

- `shipyard/packages/foundation/Authorization/ITenantContext.cs` — Surface #1 (legacy, overloaded)
- `shipyard/packages/foundation-multitenancy/ITenantContext.cs` — Surface #2 (single-responsibility, currently orphaned)
- `signal-bridge/Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` — Surface #3 (data-plane, subdomain-resolved)
- `signal-bridge/Sunfish.Bridge.Client/Services/IBridgeRequestContext.cs` — Surface #4 (Blazor-circuit composition)
- `signal-bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` — Concrete impl #1
- `signal-bridge/Sunfish.Bridge.MigrationService/MigrationTenantContext.cs` — Concrete impl #1 in migration context
- `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` — Consumer #1 with both-namespace imports

### Test code under review

- `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/TenantRegistryTests.cs` (TestTenant — 4-member fixture)
- `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/Cockpit/{Cockpit,Vendors,WorkOrders,PropertyDetail,Dashboard}EndpointTests.cs` (TestTenantContext — 4-member fixture; 5 files)
- `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/Leases/LeasesEndpointsTests.cs` (TestTenantContext — 4-member fixture)
- `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/Properties/PropertiesEndpointsTests.cs` (TestTenantContext — 4-member fixture)
- `signal-bridge/tests/Sunfish.Bridge.Tests.Unit/SeederSmokeTests.cs` + `Middleware/TenantSubdomainResolutionMiddlewareTests.cs`

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Three options considered; Option B chosen with explicit rejection rationale for Options A + C. Option A's argument (zero cost) was real but rejected on "promise on the books" grounds. Option C's argument (smaller scope) was real but rejected on "renames the smell without fixing it" grounds.
- [x] **FAILED conditions / kill triggers.** Listed in §"Revisit triggers." If OIDC claims-backed impl reveals the decomposition is wrong, the ADR revisits.
- [x] **Rollback strategy.** Step 1 introduces additive interfaces + transitional facade — zero existing consumers break. If we abort migration mid-stream, the facade keeps everything working. If we ship Step 4 deprecation and need to revert, un-deprecate is one PR.
- [x] **Confidence level.** **HIGH** for Option B's recommended path; **MEDIUM** for the specific shape of `IAuthorizationContext` (Open question O-1). Confidence is HIGH on the decomposition principle and the migration sequence; the open questions are about details council should rule on, not the core decision.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumption): grep'd actual consumer count, named ~14 production consumers + ~10 test fixtures. AP-3 (vague phases): 5 explicit steps with PR-level scope. AP-9 (skipping Stage 0): the ADR 0008 follow-up promise IS Stage 0 in this case. AP-12 (timeline fantasy): no day-count claim made; effort estimate is bounded ranges. AP-21 (assumed facts without sources): every cited file + ADR + paper section enumerated.
- [x] **Revisit triggers.** Four named conditions, each externally observable.
- [x] **Cold Start Test.** A fresh contributor reading ADR 0008 + ADR 0031 + this ADR can identify the 4 surfaces, understand the Option B decomposition, and execute Step 1's checklist without asking for clarification.
- [x] **Sources cited.** ADR 0008 (the promise), ADR 0031 (the architecture forcing function), ADR 0006 (history), ADR 0069 (discipline), Paper §17.2 (the constraint). 8 specific code files + 8 specific test files named with paths.

---

*Authored by ONR per Admiral directive `admiral-directive-2026-05-17T23-15Z-onr-ws-e-handoff-authoring-and-itenantcontext-discovery` §"Track B". File-naming conflict with the directive's specified `0031-...` path flagged at top — slot 0031 is occupied by ADR 0031 (Bridge hybrid); this ADR sits at 0091. ONR also affirmatively flags this ADR as security-relevant per the directive's halt condition — security-engineering council review is **mandatory** before Step 1 acceptance. Standing by for council feedback.*
