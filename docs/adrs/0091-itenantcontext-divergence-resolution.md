---
id: 91
title: ITenantContext Divergence Resolution
status: Accepted
date: 2026-05-19
accepted-date: 2026-05-19
tier: foundation
pipeline_variant: sunfish-api-change
concern:
  - multi-tenancy
  - identity
  - security
  - persistence
enables:
  - tenant-context-single-responsibility
  - production-oidc-impl-future-adr
composes:
  - 8
  - 31
  - 69
  - 84
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments:
  - A1
  - A2
  - A3
  - A4
  - A5
---

# ADR 0091 — ITenantContext Divergence Resolution

**Status:** Accepted (2026-05-19; Revision 2 GREEN-attested by both .NET-architect council + security-engineering council)
**Date:** 2026-05-19 (Accepted); 2026-05-18 (Rev 1 + Rev 2 drafting)
**Resolves:** ADR 0008 §"Open questions" item 3 (ITenantContext conflated-interface smell); WS-E + W#70 hand-off `ITenantContext divergence` discovery note
**Council inputs (Revision 1, AMBER):** `coordination/inbox/council-verdict-2026-05-18T03-40Z-net-architect-adr-0091.md` + `coordination/inbox/council-verdict-2026-05-18T03-40Z-security-engineering-adr-0091.md`; consolidated in `coordination/inbox/admiral-ruling-2026-05-18T03-55Z-onr-adr-0091-consolidated-council-amendments.md`
**Council re-attestation (Revision 2, GREEN):** `coordination/inbox/council-verdict-2026-05-19T02-35Z-net-architect-adr-0091-rev-2-re-attest.md` + `coordination/inbox/council-verdict-2026-05-19T02-35Z-security-engineering-adr-0091-rev-2-re-attest.md`; promotion-to-Accepted at `coordination/inbox/admiral-status-2026-05-19T02-40Z-adr-0091-promoted-to-accepted.md`

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-18T01:57Z | ONR (Track B) | Initial draft: Option B decomposition, 5-step migration, 5 open questions, 13-symbol §A0 audit. Filed at slot 0091 (admiral-confirmed 03:30Z). Status: Proposed. |
| 2 | 2026-05-18 | Admiral (drafted on ONR's behalf; ONR dormant) | Folds 10 council-driven amendments per Admiral consolidated ruling. Adds: facade-as-sum-interface contract; `TenantId.FromString` / `ToString` helpers; `AddSunfishTenantContext` DI-helper + startup assertion (A1); `RequestContextMixingAnalyzer` (A2; ships Step 4); Step 2.0 DbContext-rewrite-as-dedicated-PR ordering; A3/A4/A5 fail-closed test gates; consolidated open-questions table; Step 1 + Step 2 PR readiness checklists; out-of-scope-but-flagged §"Production OIDC-impl ADR (future)" preamble. §A0 audit grows 13 → 16 cited symbols. Status: Proposed (Revision 2; council-amended). |
| 2-corrigendum | 2026-05-19T11:05Z | Admiral | **Step 1 package-extraction corrigendum** per admiral-ruling-2026-05-19T11-05Z-engineer-adr-0091-step-1-package-extraction-option-a (Engineer halt closed in 5 min). Engineer's Step 1 scope-survey discovered that the sum-interface extension `Foundation.Authorization.ITenantContext : Foundation.MultiTenancy.ITenantContext` creates a circular `ProjectReference` against the existing package layout (foundation-multitenancy → foundation, since `TenantId` lives in foundation). Admiral ruled adoption of Engineer's Option (a): **extract `packages/foundation-authorization/Sunfish.Foundation.Authorization.csproj`** as a new package referencing both foundation + foundation-multitenancy. Fully-qualified namespace lock survives intact (`Sunfish.Foundation.Authorization.*` unchanged); ~6-8 csproj files gain one-line ProjectReference; no source-level breaking change for the 14 legacy consumers. Implementation-detail-tier finding; no design change to ITenantContext semantics / fail-closed gates / DI registration. Per the B6 relaxation precedent on ADR 0092 (2026-05-19T07:45Z), Admiral rules in-place without council re-attest cycle — corrigenda are Admiral-level disposition. Net Step 1 PR scope: 5 new files (in new package) + 2 modified + 1 cross-repo (signal-bridge Program.cs) + ~6 csproj ProjectReference additions = ~13-15 files. |

Promotion path: both councils self-attest GREEN via inbox status on Revision 2 → Admiral promotes ADR to `Accepted`. No re-dispatch needed unless a council member flags an amendment mis-fold (in which case file `<council>-question-*.md` naming the amendment + the disagreement; ONR re-revises).

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Authorization.ITenantContext` | Existing (modified Step 1: becomes sum-interface — see §Decision) | yes — `shipyard/packages/foundation/Authorization/ITenantContext.cs` (4 members: `TenantId: string`, `UserId: string`, `Roles: IReadOnlyList<string>`, `HasPermission(string): bool`) |
| `Sunfish.Foundation.MultiTenancy.ITenantContext` | Existing (unchanged; the canonical tenant-resolution surface) | yes — `shipyard/packages/foundation-multitenancy/ITenantContext.cs` (2 members: `Tenant: TenantMetadata?`, `IsResolved: bool` default impl `=> Tenant is not null`) |
| `Sunfish.Foundation.MultiTenancy.TenantMetadata` | Existing | yes — `shipyard/packages/foundation-multitenancy/TenantMetadata.cs` |
| `Sunfish.Foundation.Authorization.ICurrentUser` | Introduced by this ADR (Step 1) | no — added in Step 1 PR |
| `Sunfish.Foundation.Authorization.IAuthorizationContext` | Introduced by this ADR (Step 1) | no — added in Step 1 PR |
| `Sunfish.Foundation.MultiTenancy.TenantId.FromString` | Introduced by this ADR (Step 1; per amendment 3) | no — added in Step 1 PR |
| `Sunfish.Foundation.MultiTenancy.TenantId.ToString` | Existing override + Step 1 audit (per amendment 3) | yes — `shipyard/packages/foundation/Assets/Common/TenantId.cs` line 64 |
| `Sunfish.Foundation.Authorization.DependencyInjection.AddSunfishTenantContext` | Introduced by this ADR (Step 1; per amendment 5 / A1) | no — added in Step 1 PR |
| `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` | Introduced by this ADR (Step 4; per amendment 6 / A2) | no — added in Step 4 PR; `// TODO (Step 4 — ADR 0091 R2 amendment A2)` marker placed in `Foundation.Authorization.ITenantContext` source in Step 1 |
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing | yes — `shipyard/packages/foundation/Assets/Common/TenantId.cs` (`Value: string`, `IsSystemSentinel`, `System` sentinel, `Default` `[Obsolete]`) |
| `Sunfish.Bridge.Middleware.IBrowserTenantContext` | Existing (unchanged; data-plane; preserves Ed25519 + Argon2id) | yes — `signal-bridge/Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` (6 read members + `Bind(...)`; MUST-NOT-mix invariant in xmldoc lines 18–19) |
| `Sunfish.Bridge.Middleware.TenantSubdomainResolutionMiddleware` | Existing (unchanged) | yes |
| `Sunfish.Bridge.Data.SunfishBridgeDbContext` | Existing (modified Step 2.0: constructor signature + filter rewrite) | yes — `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` (`_currentTenantId: string` field line 22; captured ctor line 31; legacy filters lines 78–80; typed filter builder lines 144–165) |
| `Sunfish.Bridge.Data.MigrationTenantContext` | Existing (modified Step 2: narrows to `Foundation.MultiTenancy.ITenantContext` only) | yes — referenced in council verdicts |
| `Sunfish.Foundation.Persistence.IMustHaveTenant` | Existing | yes |
| ADR 0008 (Foundation.MultiTenancy) | Existing | yes — `shipyard/docs/adrs/0008-foundation-multitenancy.md` (§"Open questions" item 3 names the divergence ADR 0091 resolves) |
| ADR 0031 (Bridge as Hybrid Multi-Tenant SaaS) | Existing — architectural upstream | yes — `shipyard/docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md` |
| ADR 0069 (ADR Authoring Discipline) | Existing — governs pre-merge council requirement | yes |
| ADR 0084 (TenantId Sentinel Governance) | Existing — defines `__` reserved-prefix guard + `TenantId.System` | yes — `shipyard/docs/adrs/0084-tenant-selection-and-sentinel-governance.md` |

§A0 totals: 16 cited Sunfish.* / ADR references. Existing & verified: 11. Introduced by this ADR: 5 (`ICurrentUser`, `IAuthorizationContext`, `TenantId.FromString`, `AddSunfishTenantContext`, `RequestContextMixingAnalyzer`).

---

## Context

`Sunfish.Foundation.Authorization.ITenantContext` is a 4-member interface (`TenantId: string`, `UserId: string`, `Roles: IReadOnlyList<string>`, `HasPermission(string): bool`) that conflates three orthogonal concerns: tenant identity, caller identity, and authorization. ADR 0008 §"Open questions" item 3 named this smell at the time `Foundation.MultiTenancy.ITenantContext` was introduced as a separate, identity-only surface (`Tenant: TenantMetadata?`, `IsResolved: bool`). The two interfaces have lived side-by-side since then, with no production consumer of the new `Foundation.MultiTenancy.ITenantContext` and 14 consumers of the legacy 4-member shape — including the security-critical `Sunfish.Bridge.Data.SunfishBridgeDbContext`, whose per-request tenant query filter is sourced from the legacy interface's `string TenantId`.

The divergence forces every legacy consumer to depend on three orthogonal concerns even when it only reads one, and produces a smell `MigrationTenantContext` exemplifies: it returns `Roles = []` and `HasPermission = false` because it has nothing useful to say about caller identity or authorization. ADR 0031's Wave 5.1 work-sequencing also names this refactor as a prerequisite for "control-plane vs data-plane" boundary enforcement: a tenant-resolution interface that carries authz claims cannot cleanly separate control-plane signup/billing/cockpit consumers from data-plane per-tenant consumers.

The WS-E + W#70 hand-off surfaced the divergence as a candidate-pattern-009 discovery and asked for a resolution ADR before the cohort-1 React-rebind work entered Step 2 endpoint migrations. The standing-approved-patterns catalog's `pattern-009` "Does NOT match" criteria explicitly excludes tenant-context-primitive changes; the full ADR pipeline is the correct route.

This ADR proposes Option B (decompose into three single-responsibility interfaces + transitional sum-interface facade) and a 5-step migration that delivers Step 1 (the facade) at zero breakage to existing consumers. Revision 2 folds 10 council-driven amendments raised by the .NET-architect and security-engineering councils (see §Revision history).

---

## Decision drivers

- **ADR 0008 promise.** §"Open questions" item 3 named the divergence as a known smell to resolve; the smell is on the books, not deniable.
- **ADR 0031 boundary.** §"Decision drivers" line 113 — *"`ITenantContext` still resolves tenants, but exclusively for control-plane concerns; it has no authority over team data."* The decomposition makes that intent enforceable by the type system.
- **.NET single-responsibility idiom.** Compare `Finbuckle.MultiTenant`'s three-way split (`ICurrentUserAccessor` / `IAuthorizationService` / `ITenantInfo`); .NET-idiomatic shape for this surface area.
- **MigrationTenantContext smell.** `Roles = []` + `HasPermission = false` for a migration runner is a code smell that disappears when migration adopts only `Foundation.MultiTenancy.ITenantContext`.
- **Cohort-1 cadence.** WS-E + W#70 need the decomposition before Step 2 endpoint migrations; the rebind cohort cannot afford a second migration wave six weeks later.
- **Security invariant (per security-engineering council).** The DbContext per-request tenant filter is the highest-value invariant in the system; any refactor MUST preserve fail-closed-under-unresolved-tenant and add a regression test gate before merge (amendments A3/A4/A5).
- **Council-attestation discipline (per ADR 0069).** This ADR is substrate-tier and security-critical; pre-merge council review is mandatory. Both .NET-architect and security-engineering councils have returned AMBER on Revision 1; Revision 2 folds their consolidated amendments.

---

## Considered options

### Option A — Document the smell; defer

Summary: keep `Foundation.Authorization.ITenantContext` at 4 members; add an xmldoc explaining the smell; add a roadmap item. No code change.

- Pro: zero engineering cost; zero migration risk.
- Con: ADR 0008's promise stays unredeemed; `MigrationTenantContext`'s `Roles = []` / `HasPermission = false` smell persists; ADR 0031's control-plane vs data-plane boundary stays informal; every new consumer inherits the conflated interface.
- Verdict: **rejected.** "Punt" is not durable design.

### Option B — Decompose into three single-responsibility interfaces + transitional sum-interface facade [RECOMMENDED]

Summary: introduce three new interfaces (`ICurrentUser`, `IAuthorizationContext`, and adopt the existing `Foundation.MultiTenancy.ITenantContext`); make the legacy `Foundation.Authorization.ITenantContext` extend all three as a transitional sum-interface facade; migrate 14 consumers in Step 2 batched PRs; mark facade `[Obsolete]` in Step 4; delete in Step 5 after one-cohort grace.

- Pro: each new interface is genuinely single-responsibility; the facade is structurally a sum (Revision 2 amendment 1) so the type system enforces the invariant that a single concrete impl provides all three coherently; consumers narrow their dependencies one PR at a time; `MigrationTenantContext` narrows to `Foundation.MultiTenancy.ITenantContext` only (strictly stronger posture — no accidental authz satisfaction); ADR 0031's control-plane vs data-plane boundary becomes type-enforceable.
- Con: 5-step migration is non-trivial; Step 2 DbContext query-filter rewrite is the highest-risk slice (security-engineering council Item 5; mitigated by amendments A3/A4/A5 + Step 2.0 dedicated-PR ordering).
- Verdict: **adopted.** Both councils endorse the direction; concerns are about contractualization (amendments folded in Revision 2), not direction.

### Option C — Rename without decompose

Summary: rename `Foundation.Authorization.ITenantContext` to `IControlPlaneRequestContext`; keep 4 members.

- Pro: clarifies the role.
- Con: admits the smell rather than fixing it; .NET single-responsibility win evaporates; every consumer still depends on three orthogonal concerns.
- Verdict: **rejected.** The rename without decomposition is a half-measure.

---

## Decision

**Adopt Option B.** Decompose the legacy `Foundation.Authorization.ITenantContext` into three single-responsibility interfaces; introduce the legacy interface as a *sum-interface facade* extending all three (Revision 2 amendment 1); migrate the 14 consumers in batched Step 2.1+ PRs after a dedicated Step 2.0 DbContext-rewrite PR (Revision 2 amendment 10); mark facade `[Obsolete]` in Step 4 after all consumers move; ship the `RequestContextMixingAnalyzer` in the same Step 4 PR (Revision 2 amendment 6 / A2); delete the facade in Step 5 after one-cohort grace.

### Initial contract surface (Step 1)

> **Package allocation corrigendum (2026-05-19T11:05Z):** The interfaces defined below live in a NEW package `packages/foundation-authorization/Sunfish.Foundation.Authorization.csproj` (not inside the existing `Sunfish.Foundation.csproj` — the sum-interface extension creates a circular ProjectReference against the existing layout). The fully-qualified namespace `Sunfish.Foundation.Authorization.*` is preserved; downstream consumers add a `<ProjectReference Include="..\..\shipyard\packages\foundation-authorization\Sunfish.Foundation.Authorization.csproj" />` line. See `coordination/inbox/admiral-ruling-2026-05-19T11-05Z-engineer-adr-0091-step-1-package-extraction-option-a.md` for the rationale + revision history table § "2-corrigendum" row.

```csharp
// ── Foundation.MultiTenancy (existing; unchanged) ─────────────────────────
namespace Sunfish.Foundation.MultiTenancy;

public interface ITenantContext
{
    TenantMetadata? Tenant { get; }
    bool IsResolved => Tenant is not null;
}

// ── Foundation.MultiTenancy.TenantId (Revision 2 amendment 3) ─────────────
// Static helpers centralize string ↔ TenantId conversion. Replaces ad-hoc
// conversion scattered across 14 endpoint files.
namespace Sunfish.Foundation.Assets.Common;

public readonly partial record struct TenantId
{
    /// <summary>
    /// Parses a string into a TenantId. Throws ArgumentException for values
    /// starting with the reserved "__" prefix (per ADR 0084 §1). Use this
    /// helper rather than the implicit-string conversion at boundary points
    /// (endpoint binding, query-string parsing) so the guard fires once,
    /// audibly, in a known location.
    /// </summary>
    public static TenantId FromString(string value) => new TenantId(value);

    // ToString() is an existing override (line 64); audited under this ADR
    // as part of amendment 3's centralization commitment.
}

// ── Foundation.Authorization — three new interfaces ──────────────────────
namespace Sunfish.Foundation.Authorization;

/// <summary>Caller identity. Single-responsibility surface for the OIDC seam.</summary>
public interface ICurrentUser
{
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
}

/// <summary>Policy evaluation primitive. Returns true iff the caller has the named permission.</summary>
public interface IAuthorizationContext
{
    bool HasPermission(string permission);
}

/// <summary>
/// Transitional sum-interface facade. Preserves source compatibility for the
/// 14 legacy consumers AND forces the concrete impl to provide all three
/// new interfaces coherently (Revision 2 amendment 1; type-level sum, not
/// three parallel DI registrations).
/// </summary>
/// <remarks>
/// Marked [Obsolete] in Step 4 after consumers migrate; deleted in Step 5
/// after the one-cohort grace period.
///
/// TODO (Step 4 — ADR 0091 R2 amendment A2): ship
/// Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer
/// in the same PR that marks this interface [Obsolete]. Analyzer fails
/// closed when a request pipeline binds both IBrowserTenantContext and the
/// control-plane facade-or-narrowed interfaces to the same endpoint.
/// </remarks>
public interface ITenantContext
    : Sunfish.Foundation.MultiTenancy.ITenantContext,
      ICurrentUser,
      IAuthorizationContext
{
    /// <summary>
    /// Legacy string TenantId. Default-implemented; delegates to Tenant?.Id.ToString().
    /// Returns string.Empty when Tenant is null (i.e., unresolved). New code MUST
    /// inject Foundation.MultiTenancy.ITenantContext instead.
    /// </summary>
    string TenantId => Tenant?.Id.ToString() ?? string.Empty;
}

// ── Foundation.Authorization.DependencyInjection (Revision 2 amendment 5 / A1) ──
namespace Sunfish.Foundation.Authorization.DependencyInjection;

public static class TenantContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers a single concrete TConcrete as the implementation of
    /// Foundation.MultiTenancy.ITenantContext + ICurrentUser +
    /// IAuthorizationContext + Foundation.Authorization.ITenantContext
    /// (the facade). Also installs a startup IHostedService assertion that
    /// verifies all three (four) interfaces resolve to the SAME instance
    /// per scope. Encodes amendment A1 in code, not in a comment.
    /// </summary>
    public static IServiceCollection AddSunfishTenantContext<TConcrete>(
        this IServiceCollection services)
        where TConcrete : class,
                          Sunfish.Foundation.MultiTenancy.ITenantContext,
                          ICurrentUser,
                          IAuthorizationContext,
                          ITenantContext;
}
```

### Substrate / layering notes

- `Foundation.MultiTenancy.ITenantContext` is the canonical tenant-resolution surface (ADR 0031 control-plane intent). `IMustHaveTenant`-bearing entities and the DbContext per-request filter consume this and nothing else after Step 2.0.
- `Foundation.Authorization.ICurrentUser` + `IAuthorizationContext` are the caller-identity + policy-evaluation surfaces. The production OIDC impl (future ADR; see §"Open questions" O-4) will provide a single concrete class implementing both, plus `Foundation.MultiTenancy.ITenantContext`, with a cross-bound `tid` claim check (see §"Out-of-scope-but-flagged").
- `Sunfish.Bridge.Middleware.IBrowserTenantContext` is unchanged — data-plane only, subdomain-resolved, crypto-primitive-bearing, MUST-NOT-mix with the control-plane contexts (invariant preserved by structure today; analyzer-enforced in Step 4 per amendment A2).
- O-3 placement decision (Revision 2 amendment 2 / O-3 consolidated ruling): `ICurrentUser` + `IAuthorizationContext` are placed in `Sunfish.Foundation.Authorization` for Step 1. The `[Obsolete]` redirect text in Step 4 bakes in this namespace; any future move to `Sunfish.Foundation.Identity` is tracked as a separate naming-cleanup ADR — not in scope here.

---

## Consequences

### Positive

- Type-system-enforced single-responsibility: each new interface carries one concern.
- `MigrationTenantContext` narrows to `Foundation.MultiTenancy.ITenantContext` only — strictly stronger posture (no accidental authz satisfaction).
- ADR 0031 control-plane vs data-plane boundary becomes type-enforceable (control-plane consumers inject the new `Foundation.MultiTenancy.ITenantContext`; data-plane consumers inject `IBrowserTenantContext`).
- Centralized `TenantId.FromString` helper (amendment 3) replaces 14 scattered ad-hoc conversions with one audited path.
- DI-helper `AddSunfishTenantContext<TConcrete>` (amendment 5 / A1) sanctions the single-concrete-class wiring in code; a future PR diverging the bindings fails the startup assertion immediately.
- Step 2.0 dedicated DbContext-rewrite PR + A3/A4/A5 fail-closed gates close the row-leakage vectors the security-engineering council enumerated.
- Step 4 `RequestContextMixingAnalyzer` (amendment 6 / A2) makes the MUST-NOT-mix-pipelines invariant compile-time enforceable.

### Negative

- 5-step migration spans multiple cohorts; Step 2.0 + Step 2.1+ batched endpoint PRs touch 14 files plus DbContext.
- Step 2 endpoint-migration PRs are NOT `pattern-009`-eligible even when co-located with rebind work (Revision 2 amendment 4) — each Step 2.1+ PR runs the full `sunfish-api-change` council pipeline.
- Step 1 facade carries default-implemented `string TenantId => Tenant?.Id.ToString() ?? string.Empty` for source compatibility. This is acceptable for the transitional period; new code MUST NOT consume the string property.
- One inherent risk (Admiral-approved deferral): the analyzer ships at Step 4, not Step 1. During Steps 1–3, pipeline-mixing detection relies on doc-comment + reviewer discipline (amendment 6 / A2). The risk is named explicitly here per Admiral ruling.

### Trust impact / Security & privacy

- The DbContext per-request tenant filter is the highest-value invariant; Step 2.0's A3/A4/A5 test gates make fail-closed-under-unresolved-tenant and sentinel/null rejection observable + regression-tested before any endpoint migration ships.
- Step 2's typed-`TenantId` filter rewrite preserves the existing capture-once-at-construction pattern (security-engineering council §"Capture pattern verdict — GREEN"). The field MUST remain `readonly` and captured at construction; no lazy `Func<TenantId>` patterns.
- The MUST-NOT-mix-pipelines invariant between `IBrowserTenantContext` (data-plane) and the control-plane interfaces is preserved by namespace + DI separation; Step 4 ships the analyzer that makes the invariant compile-time enforceable.
- `IBrowserTenantContext`'s crypto primitives (Ed25519 `TeamPublicKey`, 16-byte Argon2id `AuthSalt` per paper §17.2 ciphertext-at-rest) are not exposed on any control-plane interface — the decomposition strengthens this isolation rather than weakening it (security-engineering council Item 3 GREEN).
- The production OIDC-claims-backed impl (future ADR; see §"Out-of-scope-but-flagged") will inherit the same surface the decomposition introduces; that ADR's invariants are pre-staged here.

---

## Compatibility plan

### Migration path (5 steps)

**Step 1 — facade introduction (zero-breakage).** Introduce `Foundation.Authorization.ICurrentUser` + `IAuthorizationContext`. Convert `Foundation.Authorization.ITenantContext` to a sum-interface extending the three new contracts (`Foundation.MultiTenancy.ITenantContext` + `ICurrentUser` + `IAuthorizationContext`) with a default-implemented `string TenantId` for source compatibility. Add `Foundation.MultiTenancy.TenantId.FromString` static helper. Add `Foundation.Authorization.DependencyInjection.AddSunfishTenantContext<TConcrete>` extension + startup `IHostedService` assertion. The 14 existing consumers compile and run unchanged. Step 1 ships in one PR; sec-eng + .NET-architect councils re-attest GREEN on Revision 2 of this ADR before the PR opens.

**Step 2.0 — DbContext query-filter rewrite (dedicated PR; Revision 2 amendment 10).** Single PR that touches `SunfishBridgeDbContext` only. Constructor signature changes from `Foundation.Authorization.ITenantContext tenant` to `Foundation.MultiTenancy.ITenantContext tenant`. Constructor:
- Rejects `tenant.Tenant == null` (throws `InvalidOperationException` citing ADR 0091 R2 amendment A3); fail-closed under unresolved tenant.
- Rejects `tenant.Tenant.Id.IsSystemSentinel == true`, null `Value`, and the literal `"__system__"` in production code paths (throws `ArgumentException` citing amendment A4); migrations route through `MigrationTenantContext` + a dedicated migration DbContext, which is the only sanctioned consumer of `TenantId.System`.
- Captures the typed `TenantId` once at construction in a `readonly` field; never mutates.
- Rewrites the typed-filter builder (`ApplyTenantQueryFilters`, lines 144–165) to compare `e.TenantId.Value == _capturedTenantId.Value` while keeping the legacy string-filter shape for legacy entities; ships the A5 regression test on a populated DB (one row per known tenant + a sentinel row + a null-value row; asserts per-tenant isolation, sentinel/null exclusion, unresolved DbContext throws).
- Step 2.0 PR routes through full `sunfish-api-change` pipeline; mandatory security-engineering council review before merge.

**Step 2.1+ — batched endpoint migrations.** ~14 production consumers split into 3–4 batched PRs by cohort proximity. Each PR narrows endpoint signatures from the facade to whichever of (`Foundation.MultiTenancy.ITenantContext` / `ICurrentUser` / `IAuthorizationContext`) the endpoint actually reads. Each Step 2.1+ PR is full-pipeline `sunfish-api-change` — NOT `pattern-009`-eligible even when co-located with the cohort-1 rebind work (Revision 2 amendment 4). Cohort-1 React-rebind consumers migrate via the facade in Step 1 then narrow inside Step 2.1+ batches (Revision 2 O-5 consolidated ruling: PROCEED via facade — sec-eng path).

**Step 3 — test migration.** Test fixtures (`DemoTenantContext` + test doubles) follow production consumers. Lands after Step 2.x is complete so fixtures track real shape.

**Step 4 — facade `[Obsolete]` + analyzer ship.** Mark `Foundation.Authorization.ITenantContext` `[Obsolete]`; ship `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` (Roslyn) following the existing `foundation-wayfinder-analyzers` pattern (Revision 2 amendment 6 / A2 — Admiral-approved deferral from Step 1 to Step 4). Analyzer fails closed when both `IBrowserTenantContext` and any control-plane facade-or-narrowed interface are injected into the same endpoint.

**Step 5 — facade deletion.** Delete `Foundation.Authorization.ITenantContext` after one-cohort grace period from Step 4. Ratchet completes.

### Affected packages

- `shipyard/packages/foundation/Authorization/` — adds `ICurrentUser`, `IAuthorizationContext`; modifies `ITenantContext` (sum-interface); adds `DependencyInjection/TenantContextServiceCollectionExtensions.cs`; adds `Analyzers/RequestContextMixingAnalyzer.cs` (Step 4).
- `shipyard/packages/foundation-multitenancy/` — adds `TenantId.FromString` static (in `foundation/Assets/Common/TenantId.cs`; partial struct).
- `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` — Step 2.0 rewrite (constructor signature + filter rewrite + A3/A4 guards).
- `signal-bridge/Sunfish.Bridge/Program.cs` — DI registration switches to `AddSunfishTenantContext<DemoTenantContext>` (Step 1).
- ~14 Bridge endpoint files (enumerated by Engineer at Step 2.1 planning) — narrowed signatures (Step 2.1+).
- `signal-bridge/Sunfish.Bridge.Data/MigrationTenantContext.cs` — narrows to implement `Foundation.MultiTenancy.ITenantContext` only (Step 2).

### Impact surface estimate

- Step 1 PR: ~6 files added/modified in shipyard (foundation/Authorization + foundation-multitenancy + DI helper) + 1 file modified in signal-bridge (Program.cs DI registration). Net source compatibility: zero consumer changes required.
- Step 2.0 PR: 1 file modified in signal-bridge (`SunfishBridgeDbContext.cs`); +1 test file (A3+A4+A5 regression coverage). Mandatory sec-eng council review.
- Step 2.1+ batched PRs: ~3–4 PRs each touching ~3–4 endpoint files. Each PR runs full `sunfish-api-change` pipeline; NOT `pattern-009`-eligible.
- Step 4 PR: `[Obsolete]` attribute on facade + `RequestContextMixingAnalyzer` source + analyzer test fixtures.
- Step 5 PR: facade deletion (one PR; mechanical).

### Pattern-catalog cross-check

`standing-approved-patterns.md` `pattern-009`'s "Does NOT match" criteria includes tenant-context-primitive changes; ADR 0091 correctly routes through full council pipeline. Step 2.1+ endpoint migrations also NOT `pattern-009`-eligible even when bundled with rebind work (Revision 2 amendment 4). This ADR adds an explicit note to that effect so future cohort-PR authors do not claim `@standing-pattern: pattern-009` on tenant-context-touching files.

---

## Implementation checklist

- [ ] **Step 1 PR — facade + helpers + DI sanction**
  - [ ] Add `Foundation.Authorization.ICurrentUser` (`UserId: string`, `Roles: IReadOnlyList<string>`)
  - [ ] Add `Foundation.Authorization.IAuthorizationContext` (`HasPermission(string): bool`)
  - [ ] Convert `Foundation.Authorization.ITenantContext` to sum-interface (amendment 1): `: Sunfish.Foundation.MultiTenancy.ITenantContext, ICurrentUser, IAuthorizationContext` with default-implemented `string TenantId => Tenant?.Id.ToString() ?? string.Empty`
  - [ ] Add `// TODO (Step 4 — ADR 0091 R2 amendment A2): ship RequestContextMixingAnalyzer` comment in `Foundation.Authorization.ITenantContext` source
  - [ ] Add `Foundation.MultiTenancy.TenantId.FromString(string)` static helper (amendment 3)
  - [ ] Add `Foundation.Authorization.DependencyInjection.AddSunfishTenantContext<TConcrete>()` extension (amendment 5 / A1)
  - [ ] Add startup `IHostedService` assertion verifying all four interfaces (`Foundation.MultiTenancy.ITenantContext` + `ICurrentUser` + `IAuthorizationContext` + facade) resolve to the same scoped instance
  - [ ] Update `Sunfish.Bridge.Program.cs` DI registration to call `AddSunfishTenantContext<DemoTenantContext>`
  - [ ] Pass §"Step 1 PR readiness checklist" (below) before opening PR
  - [ ] Both councils re-attest GREEN on ADR Revision 2 before PR opens

- [ ] **Step 2.0 PR — DbContext rewrite (dedicated, before endpoint migrations) — amendment 10**
  - [ ] `SunfishBridgeDbContext` constructor signature changes to `Foundation.MultiTenancy.ITenantContext`
  - [ ] Constructor rejects `tenant.Tenant == null` (throws; A3 reference in exception message)
  - [ ] Constructor rejects `tenant.Tenant.Id.IsSystemSentinel == true`, null `Value`, literal `"__system__"` (throws; A4 reference in exception message)
  - [ ] Constructor captures typed `TenantId` once in `readonly` field; no lazy patterns
  - [ ] `ApplyTenantQueryFilters` builder rewritten for typed comparison; legacy string filters preserved for legacy entities
  - [ ] A3 unit test: unresolved-tenant DbContext throws OR every DbSet returns zero rows
  - [ ] A4 unit test: sentinel / null / `"__system__"` rejected at construction
  - [ ] A5 regression test on populated DB: one row per known tenant + sentinel row + null-value row; per-tenant isolation; sentinel/null exclusion; unresolved DbContext throws or zero rows
  - [ ] `MigrationTenantContext` narrowed to `Foundation.MultiTenancy.ITenantContext` only; migration runner uses dedicated migration DbContext (NOT `SunfishBridgeDbContext`)
  - [ ] Pass §"Step 2 PR readiness checklist — Step 2.0" (below)
  - [ ] Mandatory security-engineering council review before merge

- [ ] **Step 2.1+ PRs — batched endpoint migrations**
  - [ ] ~14 consumers split into 3–4 batched PRs by cohort proximity
  - [ ] Each PR narrows endpoint signatures from facade to the minimum needed (`Foundation.MultiTenancy.ITenantContext` / `ICurrentUser` / `IAuthorizationContext`)
  - [ ] Each PR full-pipeline `sunfish-api-change`; NOT `pattern-009`-eligible (amendment 4)
  - [ ] Cohort-1 React-rebind consumers migrate via facade in Step 1; narrow inside Step 2.1+ batches (O-5 ruling)

- [ ] **Step 3 — test migration**
  - [ ] Test fixtures (`DemoTenantContext`, test doubles) follow production consumer shape

- [ ] **Step 4 — facade `[Obsolete]` + analyzer**
  - [ ] Mark `Foundation.Authorization.ITenantContext` `[Obsolete]` (citing redirect targets)
  - [ ] Ship `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` (amendment 6 / A2) following `foundation-wayfinder-analyzers` pattern; fails closed on `IBrowserTenantContext` + control-plane mix in same endpoint

- [ ] **Step 5 — facade deletion**
  - [ ] Delete `Foundation.Authorization.ITenantContext` after one-cohort grace from Step 4

---

## Step 1 PR readiness checklist (Revision 2)

Engineer checks against this before opening the Step 1 PR; council reviewers spot-check during review. Grouped under Surface / Tests / Documentation / Council.

### Surface

- [ ] `Foundation.Authorization.ICurrentUser` added; 2 members (`UserId: string`, `Roles: IReadOnlyList<string>`)
- [ ] `Foundation.Authorization.IAuthorizationContext` added; 1 member (`HasPermission(string): bool`)
- [ ] `Foundation.Authorization.ITenantContext` declared as sum-interface: `: Sunfish.Foundation.MultiTenancy.ITenantContext, ICurrentUser, IAuthorizationContext` with default-implemented `string TenantId => Tenant?.Id.ToString() ?? string.Empty` (amendment 1)
- [ ] `// TODO (Step 4 — ADR 0091 R2 amendment A2)` analyzer-ship marker in source (amendment 6)
- [ ] `Foundation.MultiTenancy.TenantId.FromString(string)` added (amendment 3)
- [ ] `Foundation.Authorization.DependencyInjection.AddSunfishTenantContext<TConcrete>()` added (amendment 5 / A1)
- [ ] Startup `IHostedService` assertion verifies same-instance resolution across all four interfaces (A1 in code, not comment)
- [ ] `Sunfish.Bridge.Program.cs` DI registration switched to `AddSunfishTenantContext<DemoTenantContext>`

### Tests

- [ ] Unit test: `DemoTenantContext` resolves identically through each of the four interface types
- [ ] Unit test: `AddSunfishTenantContext` startup assertion FAILS when bindings diverge (e.g., second `AddScoped<IAuthorizationContext, OtherImpl>` is registered after)
- [ ] Unit test: facade's default-implemented `string TenantId` returns `Tenant?.Id.ToString() ?? string.Empty`

### Documentation

- [ ] `Foundation.Authorization.ITenantContext` xmldoc names this ADR + the Step 4 [Obsolete] timeline
- [ ] `Foundation.Authorization.ICurrentUser` xmldoc names the future production OIDC-impl ADR
- [ ] `Foundation.Authorization.IAuthorizationContext` xmldoc names `HasPermission(string)` as Step 1 design debt (O-1 ruling)
- [ ] PR description names: "ADR 0091 Revision 2 Step 1; folds amendments 1, 3, 5; commits to amendment 6 at Step 4"

### Council

- [ ] Both councils have self-attested GREEN on ADR 0091 Revision 2 via inbox status (no re-dispatch needed unless mis-fold flagged)
- [ ] Admiral has promoted ADR 0091 to `Status: Accepted` before Step 1 PR opens (per Revision 2 promotion path)
- [ ] PR description includes link to this ADR + amendment numbers + the Step 1 PR readiness checklist as a copy-pasted preflight

---

## Step 2 PR readiness checklist (Revision 2)

### Step 2.0 PR readiness — DbContext rewrite (dedicated PR; amendment 10)

#### Surface (Step 2.0)

- [ ] `SunfishBridgeDbContext` constructor signature: `Foundation.MultiTenancy.ITenantContext tenant` (NOT the facade)
- [ ] Constructor body: validates `tenant.Tenant is not null` (throws InvalidOperationException with `"ADR 0091 R2 amendment A3"` in message)
- [ ] Constructor body: validates `!tenant.Tenant.Id.IsSystemSentinel`, `tenant.Tenant.Id.Value is not null`, `tenant.Tenant.Id.Value != "__system__"` (throws ArgumentException with `"ADR 0091 R2 amendment A4"` in message)
- [ ] `_capturedTenantId` field is `readonly TenantId`; assigned once in constructor
- [ ] `ApplyTenantQueryFilters` rewritten to compare against `_capturedTenantId.Value` via typed expression-tree comparison

#### Tests (Step 2.0)

- [ ] A3 test: construct with `tenant.Tenant = null` → throws (or every DbSet returns zero rows)
- [ ] A4 test: construct with `tenant.Tenant.Id = TenantId.System` → throws; with `default(TenantId)` → throws; with `new TenantId("non-sentinel")` → succeeds
- [ ] A5 regression test: populated DB with N tenant rows + 1 sentinel row + 1 null-value row; for each known tenant context, asserts (a) sees own row, (b) does not see sentinel row, (c) does not see null-value row, (d) unresolved context throws or sees zero rows
- [ ] Migration runner test: `MigrationTenantContext` does NOT go through `SunfishBridgeDbContext`; migrations use a dedicated migration DbContext path

#### Council (Step 2.0)

- [ ] Mandatory security-engineering council review before merge (per Revision 2 amendment A3/A4/A5 BLOCKER status)
- [ ] PR description names: "ADR 0091 Revision 2 Step 2.0; folds amendments A3, A4, A5, 10"

### Step 2.1+ PR readiness — batched endpoint migrations

- [ ] Each batched PR scope ≤ 5 endpoint files
- [ ] Each PR is full `sunfish-api-change` pipeline; NOT `@standing-pattern: pattern-009` (amendment 4) even when co-located with rebind work
- [ ] Each PR narrows imported interface to the minimum needed (`Foundation.MultiTenancy.ITenantContext` if endpoint reads tenant only; `ICurrentUser` if reads caller only; `IAuthorizationContext` if reads authz only; sum-interface only if reads all three)
- [ ] Cohort-1 React-rebind consumers move from facade to narrowed shape inside the same Step 2.1+ batch (O-5 path)
- [ ] PR description names: "ADR 0091 Revision 2 Step 2.1+; cohort: <X>; consumers: <list>"
- [ ] Standard `sunfish-api-change` council attestation per pipeline variant

---

## Open questions — consolidated council rulings (Revision 2)

Per Admiral consolidated ruling `coordination/inbox/admiral-ruling-2026-05-18T03-55Z-onr-adr-0091-consolidated-council-amendments.md`. The original Revision 1 open-questions table (O-1..O-5) is replaced by this consolidation. Cite Admiral ruling as source.

| OQ | .NET-arch verdict | Sec-eng verdict | Admiral consolidation |
|---|---|---|---|
| **O-1** — `IAuthorizationContext.HasPermission(string)` evolution to claims-based (`AuthorizationResult` / `IAuthorizationRequirement` shape per ASP.NET Core)? | DEFER (premature precision; AP-15) | DEFER (acceptable Step 1 shape) | **DEFER** to production OIDC-impl ADR. ADR 0091 keeps `bool HasPermission(string)` through Step 5. |
| **O-2** — Rename `IBrowserTenantContext` → `ITenantSubdomainContext`? | PROCEED concurrent with Step 1 | PROCEED standalone or defer (security no-op) | **PROCEED, standalone PR after Step 1** (sec-eng path) — avoid coupling rename review to Step 1 review. |
| **O-3** — Move `ICurrentUser` + `IAuthorizationContext` to `Sunfish.Foundation.Identity` package? | NEEDS-MORE-INFO before Step 1 | DEFER (either placement fine on security grounds) | **STEP 1 PREREQUISITE — placement locked at `Sunfish.Foundation.Authorization` for Step 1** (Admiral lean: .NET-architect path). Future move to `Foundation.Identity` is tracked as a separate naming-cleanup ADR; not in this ADR's scope. |
| **O-4** — Production OIDC-claims-backed impl: one class or three? | DEFER (correctly punted) | BLOCKER for production-impl ADR | **DEFER** for ADR 0091. The future production-impl ADR will demand a single concrete class implementing all three interfaces — pre-staged in §"Out-of-scope-but-flagged". |
| **O-5** — Cohort-1 React-rebind adopts new interfaces directly or via facade? | PROCEED FED+Engineer discretion | PROCEED via facade (less partial-migration risk) | **PROCEED via facade — sec-eng path.** Cohort-1 React-rebind consumers move via facade in Step 1, then narrow inside Step 2.1+ batches. |

---

## Out-of-scope-but-flagged

### Production OIDC-impl ADR (future)

When ADR 0091's `ICurrentUser` + `IAuthorizationContext` receive a production OIDC-claims-backed implementation (currently the demo seam `DemoTenantContext` is the only concrete), the security-engineering council has named three invariants that future ADR MUST satisfy. Pre-staged here so the future ADR's intake carries the invariants forward:

1. **Same-token invariant.** `UserId` (from `ICurrentUser`) + `Roles` (from `ICurrentUser`) + the input to `HasPermission(string)` (on `IAuthorizationContext`) MUST come from the SAME validated token instance. Reading `sub` from one source and `roles` from another (e.g., a session cookie + a bearer token without cross-binding) is the textbook confused-deputy seam.
2. **Tenant cross-check invariant.** The `TenantId` on `Foundation.MultiTenancy.ITenantContext` MUST be cross-checked against the `tid` claim on the same validated token. Any request where the subdomain-resolved tenant disagrees with the OIDC-asserted tenant MUST be rejected at the pipeline boundary.
3. **Single-concrete-class invariant.** A single concrete class MUST implement all three interfaces (`Foundation.MultiTenancy.ITenantContext` + `ICurrentUser` + `IAuthorizationContext`). This matches the Step 1 facade pattern + eliminates the same-token invariant by construction. If the future ADR proposes three separate classes, it MUST also propose the cross-binding mechanism that ensures all three read from the same validated token — and the security-engineering council will block on that mechanism.

Admiral has named this in the consolidated ruling as "do not let O-4 ADR ship without it." Tracked.

### Future naming-cleanup ADR (`Sunfish.Foundation.Identity`)

O-3 was resolved as "lock at `Sunfish.Foundation.Authorization` for Step 1" (amendment 2). If a future ADR proposes moving `ICurrentUser` + `IAuthorizationContext` to a new `Sunfish.Foundation.Identity` package, that ADR will:

- Justify the move on package-shape grounds (boundary, transitive-dep impact, accelerator using cost)
- Include the `[Obsolete]` redirect from `Sunfish.Foundation.Authorization.ICurrentUser` → `Sunfish.Foundation.Identity.ICurrentUser` (and same for `IAuthorizationContext`)
- Run full council pipeline as a foundation-tier breaking change

---

## Revisit triggers

This ADR should be re-evaluated if any of the following occur:

- A production OIDC-claims-backed implementation ships (future ADR per §"Out-of-scope-but-flagged"); that ADR's review may surface invariants this decomposition cannot carry.
- An accelerator beyond Bridge adopts `Foundation.Authorization.*` and produces a per-accelerator concrete class divergence; the same-instance invariant (A1) may need to extend across multiple registrations.
- `Foundation.Identity` naming-cleanup ADR (O-3 future path) ships; this ADR's references to `Sunfish.Foundation.Authorization.ICurrentUser` + `IAuthorizationContext` would shift to `Sunfish.Foundation.Identity.*`.
- The MUST-NOT-mix-pipelines invariant is violated in production (i.e., the Step 4 `RequestContextMixingAnalyzer` reports a true positive that was not caught at PR review); the analyzer's enforcement model may need to tighten.
- `SunfishBridgeDbContext` migration to a per-tenant data plane (ADR 0031 Wave 5.2) supersedes the typed-`TenantId` filter rewrite altogether; the A3/A4/A5 contract may need to migrate to whichever new persistence substrate replaces it.

### FAILED conditions / kill triggers

- Step 2.0's A5 regression test cannot be made to pass (any of the per-tenant / sentinel / null cases leaks) → halt migration; file an `engineer-status-*-adr-0091-step-2.0-blocker.md`; the ADR may need a Revision 3 amendment to the DbContext rewrite mechanics.
- The Step 1 facade's default-implemented `string TenantId` causes runtime breakage in a consumer that relied on the legacy 4-member interface's reference-type semantics → halt; the facade declaration may need to be split (separate `[Obsolete]` interface + sum-interface) per the original .NET-architect council pre-amendment shape.

### Rollback strategy

- Step 1 (facade): revert is a single PR — delete `ICurrentUser`, `IAuthorizationContext`, `AddSunfishTenantContext`, `TenantId.FromString`; restore `Foundation.Authorization.ITenantContext` to its original 4-member shape. Zero downstream consumer changes required because Step 1 ships source-compatible.
- Step 2.0 (DbContext): revert is a single PR — restore the original `SunfishBridgeDbContext` constructor signature + `_currentTenantId: string` field + legacy filter expression-tree builder. Tenant isolation invariant survives the revert because the original capture-once-at-construction pattern was correct (sec-eng GREEN on capture pattern); only the contractualization (A3/A4) is lost.
- Step 2.1+ (endpoint migrations): per-PR revert. Each batched PR is independently revertable because endpoints narrow to a subset of the facade; reverting restores facade-typed parameters with no other consumer impact.
- Step 4 (`[Obsolete]` + analyzer): single PR revert removes the obsolete attribute + analyzer assembly.
- Step 5 (deletion): irreversible in the strict sense (consumer code that compiled against `Foundation.Authorization.ITenantContext` no longer compiles); however, the facade was `[Obsolete]` for one full cohort grace period before deletion, so all known consumers have migrated. Re-introduction is a Revision 3 amendment if needed.

### Confidence level

**HIGH.** Option B is the .NET-idiomatic shape (compare `Finbuckle.MultiTenant`), both councils endorse the direction, and the 10 consolidated amendments are surgical contractualization fixes rather than direction changes. The single highest-risk slice (Step 2.0 DbContext rewrite + row-leakage vectors) has explicit A3/A4/A5 fail-closed gates with mandatory sec-eng council review before merge. The decomposition strengthens the ADR 0031 control-plane vs data-plane boundary rather than weakening it. The main residual uncertainty is the Admiral-approved deferral of the `RequestContextMixingAnalyzer` from Step 1 to Step 4 — explicitly named as inherent risk in §Consequences.

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — Foundation.MultiTenancy; §"Open questions" item 3 names the divergence this ADR resolves
- [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Hybrid Multi-Tenant SaaS; architectural upstream; Wave 5.1 work-sequencing
- [ADR 0069](./0069-adr-authoring-discipline.md) — ADR authoring discipline; pre-merge council requirement
- [ADR 0084](./0084-tenant-selection-and-sentinel-governance.md) — `TenantId.System` + reserved `__` prefix guard (basis for A4's sentinel-rejection rule)

### Roadmap and specifications

- WS-E + W#70 hand-off — discovery note `ITenantContext divergence` (the trigger for this ADR)
- Paper §17.2 — ciphertext-at-rest invariant; `IBrowserTenantContext` crypto primitives unchanged
- `_shared/product/wave-5.3-decomposition.md` §5.3.A — `TenantSubdomainResolutionMiddleware` (data-plane resolution)

### Council inputs (Revision 2)

- `coordination/inbox/council-verdict-2026-05-18T03-40Z-net-architect-adr-0091.md` (AMBER; 5 conditional concerns)
- `coordination/inbox/council-verdict-2026-05-18T03-40Z-security-engineering-adr-0091.md` (AMBER; 5 conditional concerns; A3/A4/A5 = Step 2 BLOCKERS)
- `coordination/inbox/admiral-ruling-2026-05-18T03-55Z-onr-adr-0091-consolidated-council-amendments.md` (consolidated; 10 amendments)
- `coordination/inbox/admiral-status-2026-05-18T03-30Z-adr-0091-itenantcontext-council-routing.md` (dispatch + both-councils-mandatory ruling)

### Existing code / substrates

- `shipyard/packages/foundation/Authorization/ITenantContext.cs` — legacy 4-member interface (Step 1 target of the sum-interface conversion)
- `shipyard/packages/foundation-multitenancy/ITenantContext.cs` — canonical `Tenant: TenantMetadata?` surface
- `shipyard/packages/foundation/Assets/Common/TenantId.cs` — `readonly record struct`; `IsSystemSentinel`, `System` sentinel, `Default` `[Obsolete]`
- `signal-bridge/Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` — data-plane surface; MUST-NOT-mix invariant lines 18–19
- `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` — Step 2.0 rewrite target; `_currentTenantId: string` line 22; capture line 31; legacy filters lines 78–80; typed-filter builder lines 144–165

### External

- Finbuckle.MultiTenant — `ICurrentUserAccessor` / `IAuthorizationService` / `ITenantInfo` three-way split (established .NET pattern)
- ASP.NET Core `Microsoft.AspNetCore.Authorization.IAuthorizationService.AuthorizeAsync` (`AuthorizationResult` shape; deferred O-1 target)

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Considered Option A (document the smell) + Option C (rename without decompose); both rejected with reasons documented in §Considered options.
- [x] **FAILED conditions / kill triggers.** Named in §Revisit triggers — A5 unfixable + Step 1 facade default-impl runtime breakage.
- [x] **Rollback strategy.** Named per step in §Revisit triggers — Step 1, Step 2.0, Step 2.1+, Step 4, Step 5.
- [x] **Confidence level.** HIGH; rationale in §Revisit triggers.
- [x] **Cited-symbol verification.** §A0 lists 16 symbols / paths / ADRs; 11 verified existing in tree; 5 introduced by this ADR (explicitly named in checklist).
- [x] **Anti-pattern scan.** AP-1 (assumptions): captured in A3/A4/A5 test gates. AP-3 (vague success): observable PR checklists in §"Step 1 PR readiness" + §"Step 2 PR readiness". AP-9 (skipping Stage 0): both councils performed pre-merge review (AMBER → R2 amendments). AP-12 (timeline fantasy): no hour estimates; phases are scope-based. AP-21 (assumed facts): citations verified against live source files.
- [x] **Revisit triggers.** ≥5 conditions named.
- [x] **Cold Start Test.** A fresh contributor can execute Step 1 from §"Step 1 PR readiness checklist" alone (the 16 explicit items + the surface sketch in §Decision); same for Step 2.0 from §"Step 2 PR readiness checklist — Step 2.0".
- [x] **Sources cited.** Council verdicts + Admiral ruling + paper §17.2 + ADR 0008 / 0031 / 0069 / 0084 all cited inline; Finbuckle.MultiTenant + ASP.NET Core named in §External.

---

*Revision 2 authored 2026-05-18 by Admiral subagent on ONR's behalf (ONR session dormant; ONR ratifies on session revival). Promotion to `Accepted` gated on both councils self-attesting GREEN via inbox status. Council re-attestation path: file `<council>-status-*-adr-0091-rev-2-green.md` referencing this ADR + Admiral ruling.*
