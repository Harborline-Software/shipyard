---
id: 95
title: Bootstrap Context substrate for pre-tenant signup
status: Proposed
date: 2026-05-25
proposed-date: 2026-05-25
author: Admiral
tier: foundation
pipeline_variant: sunfish-api-change

concern:
  - multi-tenancy
  - identity
  - security
  - api-contract
  - threat-model

enables:
  - public-signup-endpoint-family
  - email-verification-substrate
  - invitation-accept-substrate
  - webhook-receiver-bootstrap-future
  - cross-tenant-federation-bootstrap-future

composes:
  - 8   # Foundation.MultiTenancy
  - 31  # Bridge as Hybrid Multi-Tenant SaaS (control-plane vs data-plane boundary)
  - 69  # ADR Authoring Discipline (pre-merge council + §A0 + three-direction)
  - 91  # ITenantContext Divergence Resolution (sum-interface facade + DI-helper precedent)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-tier ADR; Step 1/2 PRs each carry mandatory dual-council SPOT-CHECK per the §"Council review" decision below

amendments: []
---

# ADR 0095 — Bootstrap Context substrate for pre-tenant signup

**Status:** Proposed (Revision 2; awaiting pre-merge dual-council re-attestation on the AMBER amendments folded from Rev 1 verdicts per ADR 0069 + Halt 3 of `admiral-ruling-2026-05-25T1531Z-adr-0095-bootstrap-context-6-halt-conditions.md`)
**Date:** 2026-05-25
**Resolves:** Onboarding-ladder Decision 1 (per `admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md` — Option A APPROVED: new ADR, not an ADR 0091 amendment). Closes the substrate gap surfaced by V8 #3 onboarding-ladder Stage-02 scoping (`shipyard#117`) — the public signup endpoint family has no DI binding to use today because every request pipeline in signal-bridge binds a post-tenant context (`IBrowserTenantContext` data-plane or `Sunfish.Foundation.Authorization.ITenantContext` control-plane). Sub-cohort 1 substrate (W79) Stage-05 hand-off authoring is gated on this ADR's promotion to Accepted.
**Council inputs (Revision 1, AMBER ×2):** `coordination/inbox/council-verdict-2026-05-25T1605Z-security-engineering-shipyard-153-adr-0095-rev-1.md` (sec-eng-council; 5 amendments A–E folded into Rev 2); `coordination/inbox/council-verdict-2026-05-25T1606Z-net-architect-shipyard-153-adr-0095-rev-1.md` (.NET-architect council; 3 load-bearing amendments A1–A3 + 5 clarifications A4–A8 folded into Rev 2). Both verdicts attested NO RED blockers; architectural shape (Option C) is GREEN-attestable; AMBER findings are honesty-of-enforcement-mechanism and minimum-floor-floor-commitment additions at ADR tier, not ratification-shape rejections.
**Predecessor research:** `shipyard/icm/01_discovery/research/onr-adr-0095-bootstrap-context-scaffold.md` (753 lines; ONR; merged via `shipyard#148` 2026-05-25T15:33Z); `admiral-ruling-2026-05-25T1531Z-adr-0095-bootstrap-context-6-halt-conditions.md` (Admiral disposed all 6 halts RATIFY-ONR-RECOMMENDATION; no DEFERRED-CIC).

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-25 | Admiral | Initial draft. Folds ONR scaffold (Option C recommendation) and Admiral ruling on the 6 halt conditions (all RATIFY-ONR-RECOMMENDATION). Mirrors ADR 0091 R2 layout: `AddSunfishBootstrapContext<TConcrete>` DI helper + `BootstrapAndTenantMutualExclusionAssertion` IHostedService + `BootstrapAndTenantMutualExclusionAnalyzer` Roslyn analyzer (3-step migration; analyzer ships at Step 3 — the ADR 0091 R2 amendment A2 precedent). New package `packages/foundation-bootstrap/`. Pipeline routing via `app.UseWhen` branch BEFORE `TenantSubdomainResolutionMiddleware`. Bootstrap → post-tenant transition via child `IServiceScope` inside the signup handler. Endpoint marker style: `MapBootstrapEndpoints(IEndpointRouteBuilder)` extension method. Status: Proposed (awaiting dual-council). |
| 2 | 2026-05-25 | Admiral | Folds dual-council AMBER amendments on Rev 1. **Load-bearing amendments (Amendment 1–4):** (1) splits the mutual-exclusion enforcement story into two honest layers — IHostedService startup assertion verifies REGISTRATION-PRESENCE + composition-root opt-in (positive invariants only); Step 3 Roslyn analyzer enforces the negative invariant (no constructor injects both `IBootstrapContext` and any post-tenant interface). (sec-eng Gap A + .NET-arch A1 overlap.) (2) rewrites §"Handler Lifecycle" step 5 to adopt the `ITenantContextSeed` scoped-holder pattern; the previous "child scope registers a DemoTenantContext-equivalent" claim did not compose with .NET DI scope semantics. (.NET-arch A2.) (3) Step 1 unit tests verify registration-presence + composition-root contract, NOT resolution-validation; the production `BootstrapContext` concrete depends on `IHttpContextAccessor` which is null at `IHostedService.StartAsync`. (.NET-arch A3.) (4) `SunfishBridgeDbContext` MUST-NOT-resolve invariant adopts ADR-tier minimum-floor enforcement: constructor guard with explicit `RequireTenantBoundDbContext` typed marker; "unreachable by construction, not by docstring discipline." (sec-eng Gap B.) **Security minimum-floor amendments (Amendment 5–7):** (5) new §"Bootstrap branch input policy" subsection covering Origin-header validation + concrete length caps (CorrelationId ≤128, CaptchaToken ≤2048, IdempotencyKey ≤128 ULID-shape). (sec-eng Gap C.) (6) bootstrap endpoints MUST declare `AllowAnonymous()` explicitly at registration — Step 3 analyzer enforces this + the inverse mutual-exclusion case. (sec-eng Gap D.) (7) rate-limit deferral commits to non-permissive-default minimum-floor: fixed-window 5/min signup, 10/min verify-email, 429 with Retry-After; no fallback to AspNetCore RateLimiter default-allow. (sec-eng Gap E.) **Clarifications (Amendments A4–A8 from .NET-arch):** §Pipeline routing diagram reconciles with current minimal-API auto-routing in Program.cs; UseAntiforgery placement collapses to bootstrap-branch-only; `RateLimitBucketKey` nullability semantics pinned with route-only fallback shape; Step 3 analyzer scope tightened from prior "DI-resolved dependency tree" (which Roslyn can't actually model) to a syntactic constructor-parameter scan; OpenAPI/Swagger forward-watch added; Q3 (`DemoAuthWarningFilter`) closed at ADR text time (startup-filter, not per-request — no bootstrap-branch interaction). No §A0 cited-symbol additions (sec-eng confirmed Rev 2 introduces no new symbols beyond the 6 already enumerated, modulo `ITenantContextSeed` which is named in W79 hand-off scope per Amendment 2 §"Out of scope" follow-on). Status: Proposed (awaiting dual-council Rev 2 re-attest). |

Promotion path: both councils self-attest GREEN via inbox status on Revision 2 → Admiral promotes ADR to `Accepted`. If a council returns AMBER again, Admiral folds amendments into Revision 3 (same cadence precedent). **Step 1, Step 2, and Step 3 implementation PRs each carry their own mandatory dual-council SPOT-CHECK at PR-open** (per the 6-halt ruling, Halt 3) — these are not gated on ADR re-attest; they are independent council pulls on the implementation surface.

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Bootstrap.IBootstrapContext` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Bootstrap.DependencyInjection.AddSunfishBootstrapContext<TConcrete>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Bootstrap.DependencyInjection.BootstrapAndTenantMutualExclusionAssertion` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.Bootstrap.Middleware.BootstrapContextResolutionMiddleware` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Bootstrap.Routing.BootstrapEndpointRouteBuilderExtensions.MapBootstrapEndpoints` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Bootstrap.Analyzers.BootstrapAndTenantMutualExclusionAnalyzer` | Introduced by this ADR | no — added in Step 3 PR |
| `Sunfish.Foundation.Authorization.ITenantContext` (facade) | Existing | yes — `shipyard/packages/foundation-authorization/ITenantContext.cs` (sum-interface per ADR 0091 R2 Step 1) |
| `Sunfish.Foundation.MultiTenancy.ITenantContext` | Existing | yes — `shipyard/packages/foundation-multitenancy/ITenantContext.cs` |
| `Sunfish.Foundation.Authorization.ICurrentUser` | Existing | yes — `shipyard/packages/foundation-authorization/ICurrentUser.cs` (introduced ADR 0091 R2 Step 1) |
| `Sunfish.Foundation.Authorization.IAuthorizationContext` | Existing | yes — `shipyard/packages/foundation-authorization/IAuthorizationContext.cs` (introduced ADR 0091 R2 Step 1) |
| `Sunfish.Foundation.Authorization.DependencyInjection.AddSunfishTenantContext` | Existing | yes — `shipyard/packages/foundation-authorization/DependencyInjection/TenantContextServiceCollectionExtensions.cs` (introduced ADR 0091 R2 Step 1 / A1) |
| `Sunfish.Foundation.Authorization.DependencyInjection.TenantContextScopeAssertion` | Existing | yes — `shipyard/packages/foundation-authorization/DependencyInjection/TenantContextScopeAssertion.cs` (introduced ADR 0091 R2 Step 1 / A1) |
| `Sunfish.Bridge.Middleware.IBrowserTenantContext` | Existing (unchanged; data-plane) | yes — `signal-bridge/Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` |
| `Sunfish.Bridge.Middleware.TenantSubdomainResolutionMiddleware` | Existing (unchanged; runs on the non-bootstrap pipeline branch only after Step 2 lands) | yes — `signal-bridge/Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs` |
| `Sunfish.Bridge.Services.ITenantRegistry` | Existing | yes — `signal-bridge/Sunfish.Bridge/Services/ITenantRegistry.cs` |
| `Sunfish.Bridge.Services.ITenantRegistry.CreateAsync` | Existing (tests-only callsite today; signup wires it into a production callsite for the first time in W79) | yes — `signal-bridge/Sunfish.Bridge/Services/TenantRegistry.cs` (verified by repo-wide grep: tests + xmldoc + migration-comment + seeder-bypass note are the only references; no production caller) |
| `Sunfish.Bridge.Data.SunfishBridgeDbContext` | Existing (unchanged by this ADR; the bootstrap-scope MUST-NOT-resolve invariant is the ADR-tier statement; the "how" is W79 hand-off scope) | yes — `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` (captures `tenant.TenantId` in `readonly string _currentTenantId` at line 22; query filters at lines 78-80 + 144-165) |
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing | yes — `shipyard/packages/foundation/Assets/Common/TenantId.cs` |
| ADR 0008 (Foundation.MultiTenancy) | Existing | yes — `shipyard/docs/adrs/0008-foundation-multitenancy.md` |
| ADR 0031 (Bridge as Hybrid Multi-Tenant SaaS) | Existing | yes — `shipyard/docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md` (Wave 5.1 control-plane vs data-plane boundary) |
| ADR 0069 (ADR Authoring Discipline) | Existing | yes — `shipyard/docs/adrs/0069-adr-authoring-discipline.md` (governs pre-merge council + §A0) |
| ADR 0091 (ITenantContext Divergence Resolution) | Existing — ergonomics + DI-helper + analyzer precedent for this ADR | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (Accepted Rev 2; council-attested GREEN 2026-05-19) |
| ADR 0093 (Stage-05 Adversarial Review Protocol Amendment) | Existing — governs the Step 1/2/3 PR review cadence | yes — `shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md` |
| ADR 0094 (IAuditEventReader — Read-Side Audit Substrate Primitive) | Existing — substrate-ADR cadence reference | yes — `shipyard/docs/adrs/0094-i-audit-event-reader.md` |
| Pattern `pattern-009` (Bridge endpoint + frontend rebind pair) | Existing | yes — `shipyard/_shared/engineering/standing-approved-patterns.md` |
| ONR scaffold | Existing | yes — `shipyard/icm/01_discovery/research/onr-adr-0095-bootstrap-context-scaffold.md` |
| Admiral ruling — 6 halt conditions | Existing | yes — `coordination/inbox/admiral-ruling-2026-05-25T1531Z-adr-0095-bootstrap-context-6-halt-conditions.md` |
| Admiral ruling — onboarding-ladder 10 decisions | Existing | yes — `coordination/inbox/admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md` |

§A0 totals: 27 cited references. Existing & verified: 21. Introduced by this ADR: 6 (`IBootstrapContext` interface, `AddSunfishBootstrapContext<TConcrete>` extension, `BootstrapAndTenantMutualExclusionAssertion` IHostedService, `BootstrapContextResolutionMiddleware`, `MapBootstrapEndpoints` extension, `BootstrapAndTenantMutualExclusionAnalyzer` Roslyn analyzer).

**§A0 prose note.** The 6 introduced symbols are split across 3 PRs: Step 1 ships the interface + DI helper + startup assertion (3 of 6); Step 2 ships the middleware + endpoint-routing extension (2 of 6); Step 3 ships the Roslyn analyzer + CI gate (1 of 6). Each step PR re-runs §A0 against its own slice. The analyzer-ships-late cadence is the ADR 0091 R2 amendment A2 precedent (analyzer shipped at Step 4, not Step 1, due to the same proportionality argument: shipping the analyzer in the same PR as the substrate is non-mechanical work that doubles the Step 1 PR scope; ADR 0091 R2's deferred-analyzer-with-doc-comment-discipline-in-the-interim pattern carried zero pipeline-mixing regressions during the Step 1–3 window — same projected outcome here).

---

## Context

Sunfish has no concept of a "pre-tenant request" today. Every request pipeline in signal-bridge — the canonical SaaS-posture composition root — either (a) carries a tenant subdomain that resolves via `TenantSubdomainResolutionMiddleware` (data-plane requests), or (b) is rejected with HTTP 404 before any application code sees it (apex / unknown / `Pending` / reserved-slug requests). All four tenant-context interfaces (`Sunfish.Foundation.Authorization.ITenantContext` facade, `Sunfish.Foundation.MultiTenancy.ITenantContext`, `ICurrentUser`, `IAuthorizationContext`) assume post-tenant binding; their xmldoc contracts and ADR 0091 R2's `TenantContextScopeAssertion` invariant all anchor on that assumption. `IBrowserTenantContext` is data-plane only and throws on `IsResolved=false` reads by design.

The public signup endpoint family — `POST /api/signup`, `GET /api/signup/verify-email/{signed-token}`, `POST /api/signup/check-email-available`, `POST /api/invitations/accept/{signed-token}` — is the first surface that requires a non-zero pre-tenant window. These four endpoints live on the apex host (not a tenant subdomain); they need to read client IP, antiforgery state, CAPTCHA verdict, rate-limit bucket key, idempotency key, and request-correlation ID **before any tenant exists** (signup) or **before a particular tenant has been resolved from the URL** (verify-email and invitation-accept use signed tokens that carry the tenant identity inside the token payload, not the URL host). Today's middleware actively rejects this case: apex host → empty slug → reserved → 404.

`Sunfish.Bridge.Services.ITenantRegistry.CreateAsync` is the production-side "create a tenant" surface. It exists today and is consumed only from tests; repo-wide grep finds zero production callers. Signup is the first surface that wires it into a production code path — that wiring is W79 hand-off scope, gated on this ADR's promotion to Accepted.

The ONR scaffold (`shipyard/icm/01_discovery/research/onr-adr-0095-bootstrap-context-scaffold.md`) audited the status quo, enumerated five candidate designs (Options A–E), and recommended Option C: a distinct `IBootstrapContext` interface in a new `packages/foundation-bootstrap/` package, with a dedicated DI helper, a Roslyn analyzer, and a startup `IHostedService` mutual-exclusion assertion that mirrors ADR 0091 R2's ergonomics. The 6 halt conditions ONR flagged for Admiral disposition were all ratified RATIFY-ONR-RECOMMENDATION (0 DEFERRED-CIC); this ADR folds those rulings into the canonical decision.

---

## Decision drivers

- **ADR 0091 R2 amendment A1 precedent.** The textbook confused-deputy seam that A1 closes (a single request scope resolving more than one identity surface) re-opens immediately if a Bootstrap Context is allowed to coexist with the post-tenant facade in the same scope. ADR 0091 R2 Revision 1 picked the smaller option (no analyzer + no startup assertion) and council came back AMBER requiring both. Adopting the fully-hardened shape up-front is the standing-memo `feedback_prefer_cleanest_long_term_option` posture. **Bias to substrate-correct over ship-fast.**
- **Mutual-exclusion enforcement is split across two honest layers, not one (Rev 2 / sec-eng Gap A + .NET-arch A1).** The bootstrap-vs-tenant invariant CANNOT be enforced one-to-one in the shape of ADR 0091 R2's `TenantContextScopeAssertion`. ADR 0091 R2's assertion verifies a POSITIVE invariant ("four interfaces resolve to the same scoped instance" — provable at startup by resolving all four and `ReferenceEquals`). The bootstrap-vs-tenant invariant is a NEGATIVE invariant ("no constructor / no request scope simultaneously injects both `IBootstrapContext` AND any post-tenant context") — NOT provable at startup, because the application's root `IServiceCollection` deliberately holds both bindings (one per pipeline branch). The enforcement story therefore SPLITS across two layers, both fail-closed:
  - **Layer 1 (composition-root opt-in, runtime; secondary gate).** `BootstrapAndTenantMutualExclusionAssertion` `IHostedService` at startup verifies (a) `IBootstrapContext` is registered as scoped with a non-null `TConcrete`; (b) the concrete implements the interface; (c) the composition root called BOTH `AddSunfishBootstrapContext<>()` AND `AddSunfishTenantContext<>()` (or the bootstrap branch is the only branch the composition root claims). This catches gross composition-root misconfiguration (e.g., only the bootstrap helper called and no post-tenant helper) but does NOT enforce the per-constructor / per-request mutual-exclusion invariant. Fail-closed: throws `InvalidOperationException` at `StartAsync` if either invariant is violated; application does not boot.
  - **Layer 2 (constructor parameter scan, compile-time; primary gate).** `BootstrapAndTenantMutualExclusionAnalyzer` Roslyn analyzer (Step 3) walks the syntactic constructor declarations of types registered inside the `MapBootstrapEndpoints` call-site delegate tree; flags any constructor that takes both `IBootstrapContext` AND a post-tenant interface parameter directly (`Sunfish.Foundation.Authorization.ITenantContext` facade or any narrowed variant from ADR 0091 Step 3 narrowing, or `IBrowserTenantContext`). Bounded by what Roslyn's SemanticModel can see (does NOT model `IServiceProvider.GetService` inside method bodies or factory delegates inside `services.AddScoped(sp => ...)`); the 80% case (direct constructor injection) is covered; the 20% case (handler-body service-locator usage) remains code-review territory — same trade-off ADR 0091 R2 A2's `RequestContextMixingAnalyzer` accepts.
  - **Bridging the Step 1–2 window (no analyzer yet).** Doc-comment + reviewer discipline carry the invariant. ADR 0091 R2 A2's identical deferral window observed zero pipeline-mixing regressions; same projected outcome here.
  - **What Layer 1 explicitly does NOT claim.** Layer 1 does NOT "verify at startup that no request scope simultaneously resolves both" (the literal reading would require either trivially passing — both bindings present at root, both resolvable — or trivially failing — both bindings present at root, refuse to boot). The honest minimum-floor commitment is registration-presence + composition-root opt-in. Per-constructor enforcement IS the analyzer (Layer 2). The ADR commits to FAIL-CLOSED at both layers; the layers are non-redundant.
- **Substrate-tier blast radius.** The interface shape constrains 4+ Stage-05 hand-offs downstream (W79 substrate; W80 Surfaces A+B signup + verification; W82 Surface D invitations; post-MVP webhook-receiver + cross-tenant federation surfaces — see §"Out of scope but flagged"). Choosing the wrong shape — e.g., reusing `IBrowserTenantContext` with a "null tenant" mode — propagates across the entire onboarding ladder. ADR captures it once.
- **`TenantSubdomainResolutionMiddleware` invariant preservation.** The middleware's xmldoc invariant (lines 18–19: *any request reaching application code has a resolved `IBrowserTenantContext`*) is itself a security gate. Weakening it with a per-path "skip the gate" exemption creates a load-bearing exception future maintainers can extend badly. A separate pipeline branch (`app.UseWhen(...)`) preserves the contract intact AND surfaces bootstrap routing as a first-class composition concern.
- **`SunfishBridgeDbContext` empty-string-capture risk.** The DbContext constructor captures `tenant.TenantId` for the `HasQueryFilter` lambda at line 22 + lines 78–80 + 144–165. In bootstrap scope the captured value would be empty-string with undefined filter behavior (the facade default impl returns `Tenant?.Id.ToString() ?? string.Empty`; legacy entities filter on `TenantId == _currentTenantId`). ADR-tier statement of the invariant ("bootstrap scope MUST NOT resolve `SunfishBridgeDbContext`") closes the seam at design time; the "how" (opt-out DI registration vs separate read-only DbContext for the email-uniqueness check) is W79 hand-off scope.
- **`SunfishBridgeDbContext` MUST-NOT-resolve runtime enforcement (Rev 2 / sec-eng Gap B; fail-closed minimum-floor).** The invariant above is enforced by a constructor-level runtime guard in `SunfishBridgeDbContext` that takes an explicit `RequireTenantBoundDbContext` typed marker injected from the post-tenant DI scope only. The marker is registered exclusively by the non-bootstrap pipeline branch's DI helper (added in Step 2 / W79 hand-off); resolving `SunfishBridgeDbContext` in bootstrap scope fails fast with `InvalidOperationException` because the required marker is unregistered. ADR commits to the FAIL-CLOSED invariant at ADR tier: "the empty-string `HasQueryFilter` undefined-behavior path MUST be unreachable by construction, not by docstring discipline." The Rev 1 deferral to W79 of the entire enforcement story is too permissive — the minimum-floor (constructor guard with typed marker) is ratified here; W79 may further harden via opt-out registration or a separate read-only `SunfishBridgeReadOnlyDbContext` for the email-uniqueness check, but the constructor guard is non-negotiable. Alternative enforcement mechanisms — scoped factory that throws OR full opt-out registration — are W79-substitutable PROVIDED the FAIL-CLOSED-by-construction invariant is preserved; ADR 0095 Rev 2 commits to that minimum-floor, not to a specific mechanism among the three.
- **`ITenantRegistry.CreateAsync` first-production-callsite invariant.** Today the surface is tests-only; signup is the first production wiring. ADR 0095 ratification gates W79 hand-off authoring on the substrate's shape so the production wiring lands in a known-good DI scope.
- **Council-attestation discipline (per ADR 0069).** Substrate-tier and security-critical (defines the pre-auth surface area + closes a confused-deputy seam). MANDATORY pre-merge dual-council review on the ADR text AND on Step 1 + Step 2 implementation PRs; Step 3 (analyzer ship) needs .NET-architect council, sec-eng SPOT-CHECK optional. Mirrors ADR 0091 R2 and ADR 0094 cadence.
- **Forward-compatible with future bootstrap consumers.** Halt 6 flagged webhook-receiver bootstrap and cross-tenant federation bootstrap as known future consumers. ADR 0095 Rev 1 does NOT commit to an inheritance hierarchy now (YAGNI); if a 2nd-instance consumer with substantive overlap emerges, the V11 #1 sub-pattern-split precedent applies (promote `IBootstrapContext` to a base interface at amendment time, add `IWebhookContext : IBootstrapContext`).

---

## Considered options

### Option A — Inline (no interface; per-endpoint locals)

Each signup-family endpoint reads `HttpContext.Connection.RemoteIpAddress`, antiforgery, CAPTCHA-verdict, idempotency-key, etc. directly from `HttpContext`. No new interface, no DI binding, no analyzer.

- Pro: zero ceremony; smallest diff (~2 files).
- Pro: no DI invariant to maintain.
- Con: every signup-family endpoint reimplements the same correlation/IP/captcha read; testing surface is `HttpContext` not a typed mockable object.
- Con: no analyzer can enforce "this endpoint is bootstrap-only" — a careless developer reads `ITenantContext.TenantId` inside the same handler and ships the confused-deputy seam. Exactly the regression ADR 0091 R2 A1 closed.
- **Verdict: rejected.** Re-introduces the conflated-context smell ADR 0091 R2 just closed. Substrate-tier change deserves substrate-tier ergonomics.

### Option B — Reuse `Sunfish.Foundation.MultiTenancy.ITenantContext` with `IsResolved=false`

Bind a Bootstrap implementation of the existing tenant context interface that always returns `Tenant=null` / `IsResolved=false`. The pre-tenant pipeline already has the binding shape; the "unresolved" branch is the bootstrap window.

- Pro: zero new interfaces.
- Pro: existing consumers that check `IsResolved` get a defined story for "not yet."
- Con: ADR 0091 R2's invariant assumes `AddSunfishTenantContext<TConcrete>` is called once and aliases four interfaces to a single scoped instance. A Bootstrap variant breaks this — either skip the helper (tripping `TenantContextScopeAssertion`) or alias four interfaces to a "null tenant" instance (breaking the assumption that `Tenant` is non-null in any post-tenant consumer reading `Tenant.Id`).
- Con: conflates "tenant exists but not yet resolved by middleware" (transient middleware-mis-ordering bug — fail loudly) with "tenant intentionally absent because the request is pre-tenant" (intended state — happy path). The interface should distinguish these, not fold them.
- Con: does not carry the bootstrap-specific surface area (CAPTCHA verdict, IP, idempotency key) — those still come from `HttpContext` ad-hoc.
- **Verdict: rejected.** Overloads the post-tenant interface to mean two different things — the conflated-interface smell pattern ADR 0091 just removed.

### Option C — `IBootstrapContext` as a distinct interface + dedicated `AddSunfishBootstrapContext` DI helper + mutual-exclusion enforcement [RECOMMENDED]

A new interface `Sunfish.Foundation.Bootstrap.IBootstrapContext` (in a new `packages/foundation-bootstrap/` package) with the bootstrap-specific surface area as first-class typed members, paired with a dedicated DI helper, a startup `IHostedService` mutual-exclusion assertion, and a Roslyn analyzer. Mirrors ADR 0091 R2's ergonomics one-to-one.

- Pro: distinguishes "pre-tenant by design" from "tenant unresolved due to bug."
- Pro: mirrors the ADR 0091 R2 mental model and reuses its DI-helper + startup-assertion patterns — low cognitive cost for the team.
- Pro: carries the bootstrap-specific surface area (CorrelationId, ClientIp, CaptchaToken, IdempotencyKey, RateLimitBucketKey) as first-class typed members.
- Pro: analyzer + runtime assertion close the confused-deputy seam BEFORE the first request can hit it (matches ADR 0091 R2 A1's fail-closed posture).
- Pro: new package isolates pre-tenant substrate from post-tenant substrate — mirrors the ADR 0091 Step 1 corrigendum precedent (`packages/foundation-authorization/` was extracted for the same conceptual-separability reason).
- Con: new interface + DI helper + analyzer + assertion = ~5–8 files across Step 1 + Step 2 + Step 3 PRs. Heavier than Option A or B.
- Con: requires the cluster-home, pipeline-routing, transition-mechanism, endpoint-marker, and council-timing decisions Admiral has now ratified (6 halt conditions).
- **Verdict: adopted.** Substrate-tier change merits substrate-tier ergonomics. The blast radius — 4 onboarding sub-cohorts plus post-MVP webhook + federation surfaces — justifies the up-front interface cost. Both ONR and Admiral converge on this verdict.

### Option D — Repurpose `IBrowserTenantContext` with a "pre-tenant" mode

Extend `IBrowserTenantContext` with an `IsBootstrap: bool` field; populate the context as "bootstrap" on signup routes (bypassing the registry lookup).

- Pro: no new interface to manage.
- Con: `IBrowserTenantContext` is data-plane-specific (Ed25519 + Argon2id + slug + `TeamPublicKey` + `AuthSalt`); none of these surface in the pre-tenant signup window. Folding bootstrap into it bloats the interface and re-creates the multi-purpose-interface smell.
- Con: the `IsResolved`-vs-`IsBootstrap` state space becomes confusing — what does it mean to be `IsBootstrap && IsResolved`? Or `!IsBootstrap && !IsResolved`?
- **Verdict: rejected.** Same conflation pathology as Option B, applied to `IBrowserTenantContext` instead.

### Option E — Hybrid: nullable pre-tenant fields on an `HttpContext` extension

Add an `IBootstrapContext` extension method on `HttpContext` that lazily populates from the request — no DI binding. The signup handler calls `HttpContext.GetBootstrapContext()`.

- Pro: no DI plumbing; smallest interface footprint.
- Pro: easy to unit-test if `HttpContext` is mockable.
- Con: loses analyzer enforcement (no DI binding to detect).
- Con: reads-from-HttpContext directly is the Option A failure mode in disguise.
- Con: breaks the ADR 0091 R2 ergonomics convention (scoped DI binding for every request-scope surface).
- **Verdict: rejected.** Saves a DI registration at the cost of the mutual-exclusion guarantee.

### Decision matrix

| Criterion | A (inline) | B (reuse MT) | C (new I/F) | D (extend Browser) | E (HttpContext ext) |
|---|---|---|---|---|---|
| Distinguishes pre-tenant from mid-tenant-unresolved | no | no | **yes** | partly | no |
| Carries bootstrap surface (CAPTCHA, IP, idempotency) | no | no | **yes** | no | partly |
| Analyzer can enforce mutual exclusion | no | no | **yes** | no | no |
| Mirrors ADR 0091 R2 ergonomics | no | partly | **yes** | no | no |
| Effort (files touched, Steps 1+2+3) | ~2 | ~3 | ~5–8 | ~3 | ~3 |
| Risk of confused-deputy regression | HIGH | HIGH | LOW | HIGH | HIGH |

---

## Decision

**Adopt Option C.** Introduce `IBootstrapContext` as a distinct, single-responsibility interface in a new `packages/foundation-bootstrap/` package. Ship a dedicated `AddSunfishBootstrapContext<TConcrete>` DI helper that registers the concrete + a startup `BootstrapAndTenantMutualExclusionAssertion` `IHostedService`. Route bootstrap requests through a separate pipeline branch via `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments(...))` placed BEFORE `TenantSubdomainResolutionMiddleware` — the tenant-subdomain middleware's xmldoc invariant is preserved intact. Register bootstrap endpoints via a `MapBootstrapEndpoints(IEndpointRouteBuilder)` extension method (no `[BootstrapEndpoint]` attribute). Inside the signup handler, transition from bootstrap scope to post-tenant scope by creating a child `IServiceScope` after `ITenantRegistry.CreateAsync` returns (Mechanism α; not attribute-driven exemption). Ship `BootstrapAndTenantMutualExclusionAnalyzer` in Step 3, following the ADR 0091 R2 amendment A2 deferred-analyzer cadence.

### Initial contract surface (Step 1)

```csharp
// ── Sunfish.Foundation.Bootstrap (new package) ───────────────────────────
namespace Sunfish.Foundation.Bootstrap;

using System.Net;

/// <summary>
/// Scoped per-request surface for the pre-tenant window. Bound on the
/// bootstrap pipeline branch only (see <see cref="Middleware.BootstrapContextResolutionMiddleware"/>
/// and <see cref="Routing.BootstrapEndpointRouteBuilderExtensions.MapBootstrapEndpoints"/>);
/// mutually exclusive with the post-tenant interfaces (<see cref="Sunfish.Foundation.Authorization.ITenantContext"/>
/// facade, <see cref="Sunfish.Foundation.MultiTenancy.ITenantContext"/>,
/// <see cref="Sunfish.Foundation.Authorization.ICurrentUser"/>,
/// <see cref="Sunfish.Foundation.Authorization.IAuthorizationContext"/>,
/// <see cref="Sunfish.Bridge.Middleware.IBrowserTenantContext"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Mutual exclusion (ADR 0095 §Decision drivers — two-layer enforcement).</b>
/// No constructor / no request scope MUST inject both <c>IBootstrapContext</c>
/// and any post-tenant context (<see cref="Sunfish.Foundation.Authorization.ITenantContext"/>
/// facade or any narrowed variant + <see cref="Sunfish.Bridge.Middleware.IBrowserTenantContext"/>).
/// Enforcement splits across two layers, both fail-closed:
/// (a) <see cref="Analyzers.BootstrapAndTenantMutualExclusionAnalyzer"/> (Step 3
/// Roslyn analyzer; PRIMARY gate) scans constructor parameters of types
/// registered inside <see cref="Routing.BootstrapEndpointRouteBuilderExtensions.MapBootstrapEndpoints"/>
/// call-site and flags any constructor injecting both interfaces; and
/// (b) <see cref="DependencyInjection.BootstrapAndTenantMutualExclusionAssertion"/>
/// (startup IHostedService; SECONDARY gate) verifies registration-presence +
/// composition-root opt-in (both helpers were called). The startup assertion
/// CANNOT verify "no request scope simultaneously resolves" because the root
/// container deliberately holds both bindings; per-constructor enforcement
/// IS the analyzer. During the Step 1–2 window before the analyzer ships,
/// doc-comment + reviewer discipline carry the invariant (the ADR 0091 R2
/// amendment A2 precedent — zero pipeline-mixing regressions observed during
/// that window).
/// </para>
///
/// <para>
/// <b>Bootstrap → post-tenant transition (ADR 0095 §"Handler Lifecycle").</b>
/// The bootstrap scope MUST NOT resolve <see cref="Sunfish.Bridge.Data.SunfishBridgeDbContext"/>
/// because that DbContext captures <c>tenant.TenantId</c> at construction
/// (line 22) for the per-entity <c>HasQueryFilter</c> lambda; in bootstrap
/// scope the captured value would be the facade's empty-string default and
/// the filter behavior is undefined. Enforcement is by constructor guard
/// with an explicit <c>RequireTenantBoundDbContext</c> typed marker
/// registered exclusively on the non-bootstrap pipeline branch (see
/// §Decision drivers — Rev 2 minimum-floor commitment). After
/// <c>ITenantRegistry.CreateAsync</c> returns the new tenant, the signup
/// handler creates a child <c>IServiceScope</c> via the injected
/// <c>IServiceScopeFactory</c>, populates a scoped <c>ITenantContextSeed</c>
/// (the W79 hand-off names the type) immediately before any resolution
/// inside the child scope, then resolves the post-tenant context family
/// (which reads from the seed) and writes the initial User aggregate; the
/// child scope disposes; the outer bootstrap scope continues for
/// audit-emission + email-dispatch. See §"Compatibility plan / Handler
/// Lifecycle" below for the 6-step ordering and the canonical α-1
/// scoped-holder mechanism.
/// </para>
///
/// <para>
/// <b>Surface area is small and intentional.</b> Five members. Any additional
/// pre-tenant correlation needs (e.g., webhook-signature verdict, federation
/// token payload) ship as separate interfaces or as a Revision 2 amendment;
/// see §"Out of scope but flagged" for the forward-watch policy.
/// </para>
/// </remarks>
public interface IBootstrapContext
{
    /// <summary>
    /// Stable request correlation ID; flows into logs + audit events emitted
    /// from the bootstrap pipeline. Generated by
    /// <see cref="Middleware.BootstrapContextResolutionMiddleware"/> at the
    /// pipeline-branch entry point (one ID per request; reuses
    /// <c>X-Correlation-Id</c> header value if the caller supplied one).
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Client IP (post-X-Forwarded-For evaluation per ASP.NET Core's
    /// <c>UseForwardedHeaders</c> configuration); null when the underlying
    /// connection has no addressable peer (test contexts).
    /// </summary>
    IPAddress? ClientIp { get; }

    /// <summary>
    /// CAPTCHA verdict token from the form payload; null pre-verification
    /// or for endpoints that don't require CAPTCHA (e.g., verify-email
    /// links that already carry signed-token authentication).
    /// </summary>
    string? CaptchaToken { get; }

    /// <summary>
    /// Idempotency key from the <c>X-Idempotency-Key</c> header; null when
    /// the caller didn't supply one. Signup itself is non-idempotent;
    /// invitation-accept is idempotency-required by handler contract.
    /// </summary>
    string? IdempotencyKey { get; }

    /// <summary>
    /// Bucket key for the AspNetCore <c>RateLimiter</c> (per-IP layer +
    /// per-route+per-IP layer). Non-null and deterministic. Populated by
    /// <see cref="Middleware.BootstrapContextResolutionMiddleware"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="ClientIp"/> is non-null, the bucket key shape is
    /// <c>ip:{normalized-ip}</c> for the per-IP layer and
    /// <c>route:{normalized-path}|ip:{normalized-ip}</c> for the
    /// per-route+per-IP layer. When <see cref="ClientIp"/> is null (test
    /// contexts; no addressable peer), the bucket key falls back to a
    /// route-only bucket of the shape <c>route:{normalized-path}</c> — the
    /// per-IP layer becomes a route-bucket fallback rather than a null /
    /// throwing key. Concrete normalization (IPv6 prefix collapse,
    /// path-case folding, etc.) is W79 Stage-05 hand-off scope; the ADR-
    /// tier invariant is non-null + deterministic + non-trivial.
    /// Policy values (window size, request limits, burst allowances) are
    /// W79 hand-off scope per §"Forward configuration"; the non-permissive-
    /// default minimum-floor invariant ratified in Rev 2 (Amendment 7)
    /// applies; security-engineering SPOT-CHECK confirms values at Step 2
    /// PR-open.
    /// </remarks>
    string RateLimitBucketKey { get; }
}

// ── Sunfish.Foundation.Bootstrap.DependencyInjection ─────────────────────
namespace Sunfish.Foundation.Bootstrap.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public static class BootstrapContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TConcrete"/> as the scoped implementation
    /// of <see cref="IBootstrapContext"/>, and installs the startup
    /// <see cref="BootstrapAndTenantMutualExclusionAssertion"/>
    /// <see cref="IHostedService"/> that verifies no scope simultaneously
    /// resolves <see cref="IBootstrapContext"/> and any post-tenant context
    /// (<see cref="Sunfish.Foundation.Authorization.ITenantContext"/> facade
    /// or any narrowed variant + <see cref="Sunfish.Bridge.Middleware.IBrowserTenantContext"/>).
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Sunfish.Foundation.Authorization.DependencyInjection.TenantContextServiceCollectionExtensions.AddSunfishTenantContext{TConcrete}"/>
    /// (ADR 0091 R2 amendment A1) — same DI-helper-plus-assertion shape.
    /// </remarks>
    public static IServiceCollection AddSunfishBootstrapContext<TConcrete>(
        this IServiceCollection services)
        where TConcrete : class, IBootstrapContext
    {
        services.AddScoped<TConcrete>();
        services.AddScoped<IBootstrapContext>(sp => sp.GetRequiredService<TConcrete>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IHostedService, BootstrapAndTenantMutualExclusionAssertion>());
        return services;
    }
}
```

### Substrate / layering notes

- **`packages/foundation-bootstrap/`** sits at the foundation tier (substrate; framework-agnostic), as a sibling to `packages/foundation-authorization/` and `packages/foundation-multitenancy/`. Both `foundation-authorization` and `foundation-bootstrap` reference `packages/foundation/` (for `TenantId` and other shared primitives) but neither references the other — pre-tenant and post-tenant substrates are kept structurally disjoint. Future hosts (Sunfish desktop / Anchor / tender / flight-deck) consuming pre-tenant surfaces (if any) reuse this package without depending on the post-tenant cluster.
- **`Sunfish.Foundation.Authorization.ITenantContext` (facade) + the three narrowed interfaces** are NOT consumed in bootstrap scope. The bootstrap pipeline branch's DI scope EXCLUDES the post-tenant facade. `Sunfish.Foundation.Authorization.DependencyInjection.AddSunfishTenantContext<TConcrete>` continues to run on the non-bootstrap pipeline branch only (after Step 2 lands).
- **`Sunfish.Bridge.Middleware.IBrowserTenantContext`** is unchanged — data-plane only, runs on the non-bootstrap pipeline branch only. The MUST-NOT-mix invariant between `IBrowserTenantContext` and the control-plane interfaces (preserved by ADR 0091 R2 amendment A2's pending Step 4 analyzer) extends to also mean MUST-NOT-mix with `IBootstrapContext` — the Step 3 analyzer covers this 5th-interface case.
- **`Sunfish.Bridge.Data.SunfishBridgeDbContext`** MUST NOT be resolved in bootstrap scope (ADR-required invariant per Decision Drivers; see §"Compatibility plan / Handler Lifecycle"). **Rev 2 minimum-floor enforcement (sec-eng Gap B):** the DbContext's constructor requires an explicit `RequireTenantBoundDbContext` typed marker; the marker is registered exclusively in the non-bootstrap pipeline branch's DI scope (added in Step 2 / W79 hand-off). Resolution in bootstrap scope fails fast with `InvalidOperationException` due to missing marker. The W79 hand-off may FURTHER harden the enforcement (opt-out registration for `SunfishBridgeDbContext` in the bootstrap branch's `IServiceCollection`; or a separate `SunfishBridgeReadOnlyDbContext` for the email-uniqueness check); the constructor-guard minimum-floor is ADR-ratified and non-substitutable downward. ADR 0095 states the invariant + the minimum-floor enforcement; the "how to further harden" is W79.
- **ADR 0091 Step 3 narrowing forward-compat.** Halt 1 reasoning observed that consumption sites currently pick the Authorization sum-interface facade until ADR 0091 Step 3 narrows. `IBootstrapContext` lives in a new namespace (`Sunfish.Foundation.Bootstrap`) that does not collide with either `Authorization` or `MultiTenancy`; ADR 0091 Step 3's downstream narrowing does not affect this ADR.

---

## Handler Lifecycle (signup handler 6-step ordering)

Per Halt 2-β disposition (Mechanism α adopted): the signup handler transitions from bootstrap scope to post-tenant scope by creating a child `IServiceScope` after `ITenantRegistry.CreateAsync` returns. The 6-step ordering inside the signup handler is:

1. **Bootstrap-only DI scope active.** `IBootstrapContext` is the only context-shape binding resolved in this scope (verified by `BootstrapAndTenantMutualExclusionAssertion` + Step 3 analyzer). No post-tenant interface is reachable.
2. **Validate signup payload.** No DB access. Pure model validation (DataAnnotations + business-rule checks).
3. **Email-uniqueness check via dedicated read-only DbContext.** This DbContext is control-plane-only (`TenantRegistrations` table per ADR 0031 Wave 5.1) and does NOT consume `Sunfish.Foundation.Authorization.ITenantContext`. Implementation-detail (separate read-only DbContext vs `SunfishBridgeDbContext` opt-out) is W79 hand-off scope; **the ADR-tier statement is that `SunfishBridgeDbContext` MUST NOT be resolved in bootstrap scope** (Decision Drivers).
4. **Call `ITenantRegistry.CreateAsync`.** Writes to control-plane `TenantRegistrations` (outside the tenant-query-filter set in `SunfishBridgeDbContext` per ADR 0031). Returns the newly-created `TenantMetadata`.
5. **Create child `IServiceScope` via `IServiceScopeFactory`; populate `ITenantContextSeed`; resolve post-tenant context family from the child scope; write initial User aggregate; dispose child scope.** Per .NET DI scope semantics, scoped registrations live at the root container — the handler CANNOT "register a `DemoTenantContext`-equivalent for the new tenant" at the moment a child scope is created. The composition-correct mechanism (α-1, the canonical pattern; Rev 2 / .NET-arch A2) uses a scoped mutable seed-holder service populated immediately before any post-tenant resolution inside the child scope:

   ```csharp
   // ── Registered at the post-tenant pipeline branch's composition root
   //    (Step 2 / W79 hand-off introduces ITenantContextSeed) ────────────
   services.AddScoped<ITenantContextSeed, MutableTenantContextSeed>();
   services.AddScoped<Sunfish.Foundation.Authorization.ITenantContext>(sp =>
       new SeededTenantContext(sp.GetRequiredService<ITenantContextSeed>()));

   // ── Inside the signup handler, after CreateAsync returns ─────────────
   using var childScope = _scopeFactory.CreateScope();
   childScope.ServiceProvider
       .GetRequiredService<ITenantContextSeed>()
       .Bind(newTenantId);
   // ⮡ from this line forward inside childScope, resolution of
   //   ITenantContext / ICurrentUser / IAuthorizationContext / facade /
   //   SunfishBridgeDbContext (with RequireTenantBoundDbContext marker
   //   present) all see the bound tenant.
   var userRepo = childScope.ServiceProvider.GetRequiredService<IUserAggregateRepository>();
   await userRepo.PersistInitialUserAsync(newUser, ct);
   ```

   The seed-holder pattern composes cleanly with `AddSunfishTenantContext<>` (the helper that registers the four-interface facade aliasing); the four scoped aliases all flow through the same `SeededTenantContext` instance per the ADR 0091 R2 A1 invariant — `ReferenceEquals` across the four interfaces is preserved inside the child scope. The mutually-exclusive alternative (`AsyncLocal<TenantId?>` populated immediately before `_scopeFactory.CreateScope()`) is REJECTED as the canonical pattern because AsyncLocal-based threading state is harder to reason about under concurrent in-process handlers and complicates testing; α-1 (scoped holder) is the ratified mechanism. The child scope disposes after the write commits; the seed-holder is released with the scope; nothing leaks across scopes.
6. **Bootstrap scope continues for audit-emission + email-dispatch.** Welcome-email link signing, audit events (signup completion), email dispatch via ADR 0096 substrate (downstream) — all run in the outer bootstrap scope.

**Why Mechanism α and not Mechanism β (attribute exemption).** Mechanism β (an `[BootstrapEndpoint]` attribute exempting the handler from the startup mutual-exclusion assertion) introduces an exemption pathway that's more compact AND more error-prone — future maintainers can copy the attribute to non-bootstrap handlers and silently weaken the invariant. Mechanism α also composes cleanly with the foundation-authorization `AddSunfishTenantContext` helper (the child scope registers its own `TenantContext` for the post-tenant write; the outer bootstrap scope continues uncontaminated for audit + email).

---

## Pipeline routing

Per Halt 2 disposition (Pathway 1 adopted): bootstrap requests route through a separate pipeline branch BEFORE `TenantSubdomainResolutionMiddleware`.

```
Request → [implicit minimal-API routing — Step 2 makes UseRouting explicit]
        → app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/signup")
                              || ctx.Request.Path.StartsWithSegments("/api/invitations/accept"),
                      bootstrapBranch =>
                      {
                          bootstrapBranch.UseMiddleware<OriginValidationMiddleware>();   // apex-host Origin check
                          bootstrapBranch.UseMiddleware<BootstrapContextResolutionMiddleware>();
                          bootstrapBranch.UseRateLimiter();          // per-IP + per-route layers (Rev 2 non-permissive-default minimum-floor)
                          bootstrapBranch.UseAntiforgery();          // signup form POSTs — bootstrap-branch-only call site
                          bootstrapBranch.UseMiddleware<CaptchaVerificationMiddleware>(); // Step 2 detail; W79 hand-off
                          // endpoint handlers registered via MapBootstrapEndpoints (see below)
                      })
        → app.UseMiddleware<TenantSubdomainResolutionMiddleware>()
        → app.UseAuthorization()
        → endpoint handlers (non-bootstrap; antiforgery on the non-bootstrap branch is applied per-endpoint via [ValidateAntiForgeryToken] or the global authorization policy, NOT via a second outer `app.UseAntiforgery()` call)
```

**Rev 2 / .NET-arch A4 reconciliation against current `signal-bridge/Sunfish.Bridge/Program.cs` (lines 130–270).** Today's composition root does NOT call `UseRouting()` explicitly — ASP.NET Core minimal APIs auto-wire `UseRouting` at the start of the pipeline. Step 2 introduces an explicit `app.UseRouting()` call at the top of the SaaS-posture composition root (before the `app.UseWhen` branch) so the routing placement is diagram-fidelity-stable and `UseWhen`'s placement is unambiguous. Engineer ships the explicit `UseRouting` call in the Step 2 PR.

**Rev 2 / .NET-arch A5 antiforgery placement resolution.** Antiforgery middleware is invoked exactly ONCE per pipeline branch. The bootstrap branch calls `bootstrapBranch.UseAntiforgery()` inside the `UseWhen` body. The non-bootstrap branch does NOT call `app.UseAntiforgery()` at the outer pipeline — endpoint-level `[ValidateAntiForgeryToken]` attributes (or equivalent per-endpoint metadata) carry the post-tenant-branch antiforgery enforcement. This collapses the Rev 1 Q2 "does UseWhen inherit antiforgery" question to a definite answer: it does not need to, because antiforgery lives inside each branch. (Q2 in §"Open questions" is reframed as "branch-internal antiforgery shape" rather than "inheritance from outer.")

Implementation lives in signal-bridge `Program.cs` SaaS-posture composition root; Step 2 PR registers the branch. The bootstrap branch composes its own middleware stack and never inherits `TenantSubdomainResolutionMiddleware`'s contract — the middleware's xmldoc invariant (lines 18–19) is preserved intact for the non-bootstrap branch.

### Bootstrap branch input policy (Rev 2 / sec-eng Gap C)

The bootstrap pipeline branch is the highest-Origin-spoofing-risk surface in the application (pre-auth, public, accepting POST/PATCH from arbitrary network origins). ADR 0095 Rev 2 ratifies these minimum-floor input invariants at ADR tier; Step 2 PR sec-eng SPOT-CHECK confirms they are wired:

**Origin-header validation (pre-tenant POST/PATCH/DELETE).** All POST/PATCH/DELETE handlers registered via `MapBootstrapEndpoints` MUST validate the request `Origin` header against the configured apex host (e.g., `RootHost` from `TenantResolutionOptions` in signal-bridge). If `Origin` is missing on a state-mutating request, or if it does not match the apex host (case-insensitive scheme+host+port equality), the request is rejected with HTTP 403 and a non-diagnostic body (no leak of expected origin shape; no echo of submitted origin into the response). Enforcement is via a dedicated `OriginValidationMiddleware` placed FIRST inside the bootstrap branch (before `BootstrapContextResolutionMiddleware`) so a failing Origin check short-circuits before any further bootstrap state is constructed. ASP.NET Core's built-in antiforgery does NOT validate `Origin` — that's why this is a separate guard, not a fold-in.

**Input length caps (substrate-tier minimum floor).** All `IBootstrapContext` string-typed members and request-input strings that flow into audit emission or persistence MUST be length-capped at the resolution middleware boundary before any downstream consumer sees them. Concrete floors (substrate-tier; W79 may tighten further but not loosen):
- `CorrelationId` (server-generated or `X-Correlation-Id` header echo): max 128 characters; pattern `^[A-Za-z0-9_-]{1,128}$`. Header values failing the pattern are dropped (server generates a fresh ID); they are not echoed into logs or audit events as-is.
- `CaptchaToken` (form payload field): max 2048 characters. Tokens exceeding the cap are rejected with HTTP 400 at middleware boundary (no truncation; no partial-token capture into audit).
- `IdempotencyKey` (`X-Idempotency-Key` header): max 128 characters; ULID-shape upper bound (`^[0-9A-HJKMNP-TV-Z]{1,128}$` — Crockford-base32 superset). Header values exceeding the cap are rejected with HTTP 400.

**Why these caps live at ADR tier.** The bootstrap branch is the pre-auth surface; an attacker can feed arbitrary-length headers / form fields. Caps deferred to W79 hand-off create a window where Step 2 could ship with no caps (or AspNetCore form-option defaults that may be more permissive than the substrate intent) and the sec-eng SPOT-CHECK becomes the only gate. Ratifying concrete numbers in ADR 0095 Rev 2 lifts the floor to substrate-tier; W79 may tighten (e.g., reduce CaptchaToken to 1024) but not loosen.

**Pre-existing antiforgery + new CAPTCHA verification.** Origin validation + antiforgery + CAPTCHA verification are three independent defenses, all inside the bootstrap branch (ordering per §"Pipeline routing"). Origin protects against cross-site-scripted POSTs; antiforgery protects against same-origin reflected token reuse; CAPTCHA protects against bot signup. The three are non-redundant; the SPOT-CHECK on Step 2 PR confirms all three are wired in the documented order.

### Bootstrap endpoint registration (Step 2)

Per Halt 5 disposition (extension-method approach adopted): bootstrap endpoints are registered via an extension method on `IEndpointRouteBuilder`:

```csharp
namespace Sunfish.Foundation.Bootstrap.Routing;

public static class BootstrapEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Registers the bootstrap endpoint family inside an
    /// <see cref="IEndpointRouteBuilder"/>. Call from inside the bootstrap
    /// pipeline branch only (see ADR 0095 §"Pipeline routing"); the
    /// <see cref="Analyzers.BootstrapAndTenantMutualExclusionAnalyzer"/>
    /// (Step 3) scans handler registrations inside the call site of this
    /// method and flags any handler whose constructor (or DI-resolved
    /// dependency tree) reaches a post-tenant context interface.
    /// </summary>
    public static IEndpointRouteBuilder MapBootstrapEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // signup endpoint family registrations land here
        // (signup, verify-email, check-email-available, invitations-accept).
        // Exact endpoint shapes are W80 / W82 Stage-05 hand-off scope.
        return endpoints;
    }
}
```

The extension method serves three purposes: (a) makes the bootstrap-nature explicit at the registration site (`Program.cs`); (b) gives the Step 3 analyzer a stable site to scan for post-tenant-context dependencies; (c) provides a hook for future bootstrap consumers to register additional endpoints (webhook-receiver, cross-tenant federation — see §"Out of scope but flagged").

**Why extension method, not `[BootstrapEndpoint]` attribute.** Attributes are easy to forget on individual endpoint methods. Extension methods make the bootstrap-nature explicit at the **composition root**, which is where reviewers reading `Program.cs` look to understand the request-routing surface. (The "Aspire-style service-registration convention" framing in Rev 1 is corrected here — Aspire app-host orchestrates PROJECTS, not service-collections inside a project; the convention referenced is the per-project DI-helper convention symmetric with `AddSunfishBootstrapContext` / `AddSunfishTenantContext`, not Aspire app-host composition.)

#### Bootstrap endpoints MUST declare `AllowAnonymous()` explicitly (Rev 2 / sec-eng Gap D)

All handlers registered inside `MapBootstrapEndpoints` MUST declare `.AllowAnonymous()` at registration time — by explicit method call, not by omission of a `RequireAuthorization` directive. Example:

```csharp
endpoints.MapPost("/api/signup", SignupHandler.HandleAsync)
    .AllowAnonymous()        // ← Rev 2 / sec-eng Gap D MANDATORY
    .DisableAntiforgery()    // ← if this endpoint exempts antiforgery; per-endpoint, audited
    .WithName("PublicSignup");
```

The Step 3 Roslyn analyzer enforces this invariant alongside the mutual-exclusion check: any handler registered inside `MapBootstrapEndpoints` whose endpoint-registration chain does NOT include `AllowAnonymous()` produces a diagnostic. This prevents the inverse failure mode — a future maintainer registers an auth-required handler via `MapBootstrapEndpoints` and inadvertently ships it as pre-auth-by-omission. The convention is "explicit at registration is the rule; omission is a code-review and analyzer failure," symmetric with the bootstrap-vs-tenant mutual-exclusion analyzer rule.

---

## Forward configuration (rate-limit policy values)

Per Halt 4 disposition: `IBootstrapContext.RateLimitBucketKey` is defined here as the substrate property; the concrete policy values (per-IP window, per-route+per-IP window, request limits, burst allowances) are W79 Stage-05 hand-off scope. The implementation substrate is AspNetCore's built-in `RateLimiter` (per onboarding-ladder ruling Decision 6 — Option A APPROVED; .NET 11 built-in, in-memory backing for MVP, Redis backing forward-watched for multi-pod scale-out). Security-engineering SPOT-CHECK on the Step 2 implementation PR confirms the policy values at PR-open time.

### Rate-limit non-permissive-default minimum-floor (Rev 2 / sec-eng Gap E)

ADR 0095 Rev 2 ratifies the following minimum-floor invariants at ADR tier — values may be tightened (smaller windows, lower request counts) by W79 hand-off or Step 2 implementation, but never loosened:

- **No fallback to AspNetCore RateLimiter default-allow.** Step 2 PR-open MUST ship with concrete policy values configured for every endpoint registered via `MapBootstrapEndpoints` (signup, verify-email, check-email-available, invitations-accept). The bootstrap branch's `UseRateLimiter()` call MUST NOT inherit any "default policy allows all" behavior. If no policy is configured for a bootstrap-branch endpoint, the analyzer + sec-eng SPOT-CHECK BOTH fail.
- **No inheritance of the apex `app.UseRateLimiter()` policy.** The bootstrap branch composes its OWN `UseRateLimiter()` call inside the `UseWhen` body (per §"Pipeline routing"); it does NOT rely on outer-pipeline rate-limit configuration. Bootstrap policy values are independent of (and typically stricter than) non-bootstrap policy values.
- **Concrete minimum-floor values (Step 2 PR-open).** These are the ADR-tier MINIMUM floors; W79 hand-off may tighten:
  - `POST /api/signup` — fixed-window 5 requests / minute / IP; burst 0; HTTP 429 with `Retry-After` header set to the window remainder.
  - `POST /api/signup/check-email-available` — fixed-window 20 requests / minute / IP; burst 5; HTTP 429 with `Retry-After`.
  - `GET /api/signup/verify-email/{signed-token}` — fixed-window 10 requests / minute / IP; burst 5; HTTP 429 with `Retry-After`.
  - `POST /api/invitations/accept/{signed-token}` — fixed-window 10 requests / minute / IP; burst 0; HTTP 429 with `Retry-After`.
  - Per-route-+-per-IP layer (route+IP composite): same per-IP shape with route discrimination so a single attacker IP cannot exhaust the global pool by spreading across routes.

Per-IP buckets fall back to the route-only `RateLimitBucketKey` shape when `ClientIp` is null (test contexts; see `RateLimitBucketKey` xmldoc remarks); the route-only fallback retains a non-trivial rate cap (the same numbers above, but bucketed per route only).

The Step 2 PR sec-eng SPOT-CHECK confirms: (a) per-IP layer is configured for the bootstrap branch; (b) per-route+per-IP layer is configured for at least `/api/signup` and `/api/invitations/accept`; (c) no bootstrap-branch endpoint inherits the apex `app.UseRateLimiter()` policy by default; (d) `Retry-After` header is set on 429 responses. Hard-coding the floors at ADR tier converts the "deferred decision" into a "deferred parameter with substrate-ratified minimum-floor," which is the correct substrate-vs-configuration split.

Hard-coding values inside this ADR would have forced amendment churn for any production-tuning observation; ratifying minimum-floors (not maximum-ceilings) lets W79 + Step 2 implementation tune downward without ADR amendment.

---

## Consequences

### Positive

- Type-system-enforced single-responsibility for the pre-tenant window: `IBootstrapContext` carries one concern (request-correlation surface in the pre-tenant scope), distinct from the post-tenant context family.
- Startup `BootstrapAndTenantMutualExclusionAssertion` + Step 3 `BootstrapAndTenantMutualExclusionAnalyzer` close the confused-deputy seam BEFORE the first request can hit it — same fail-closed posture as ADR 0091 R2 A1 + A2.
- ADR 0031 Wave 5.1 control-plane vs data-plane boundary becomes structurally enforceable for the pre-tenant case: control-plane signup goes through bootstrap; data-plane reads go through `IBrowserTenantContext`; the two pipelines are disjoint.
- New `packages/foundation-bootstrap/` package isolates pre-tenant substrate from post-tenant substrate — mirrors the ADR 0091 Step 1 corrigendum's `foundation-authorization` extraction; both follow the same "namespaces that should be conceptually separable" pattern.
- 4 onboarding sub-cohorts (W79 substrate; W80 Surfaces A+B; W82 Surface D invitations) and post-MVP forward-watched surfaces (webhook-receiver bootstrap; cross-tenant federation bootstrap) all reference one substrate ADR — no per-cohort substrate re-litigation.
- Mechanism α (child `IServiceScope` for the post-tenant write) is .NET-idiomatic and testable; doesn't require attribute-driven exemption pathways that future maintainers can copy badly.
- `MapBootstrapEndpoints` extension method makes the bootstrap-nature explicit at the composition root — reviewers reading `Program.cs` see the request-routing surface at a glance.
- The standing-memo `feedback_prefer_cleanest_long_term_option` posture is honored: Option C buys a substrate-correct shape. Rev 1 → Rev 2 council-amendment cycle (~+30 min Admiral; ~+60 min dual-council Rev 2 re-attest) was driven by HONESTY-OF-ENFORCEMENT findings (the Layer 1 startup assertion + child IServiceScope claims as drafted did not compose with .NET DI semantics) and MINIMUM-FLOOR commitments (Origin validation + length caps + rate-limit floors + DbContext constructor guard). Rev 2 lifts those gaps to ADR tier explicitly; Engineer ships against an accurate spec.

### Negative

- 3-step migration (Step 1 substrate; Step 2 middleware + endpoint registration; Step 3 analyzer + CI gate) spans multiple Engineer PRs and consumes ~5–8 net files across the package.
- During the Step 1–2 window before the Step 3 analyzer ships, per-constructor pipeline-mixing detection relies on doc-comment + reviewer discipline. The Layer 1 startup assertion (registration-presence + composition-root opt-in) does NOT close the per-constructor case — only the Step 3 analyzer does. The risk is named explicitly here per the ADR 0091 R2 amendment A2 precedent (which carried zero pipeline-mixing regressions during its Step 1–3 window — same projected outcome here). The constructor guard on `SunfishBridgeDbContext` (Rev 2 minimum-floor / sec-eng Gap B) closes the highest-risk silent-data-leak path during the same window.
- W79 Stage-05 hand-off authoring is gated on this ADR's promotion to Accepted (and on the W79 hand-off resolving the implementation-detail Q4 — `SunfishBridgeDbContext` opt-out vs separate read-only DbContext for the email-uniqueness check). Sub-cohort 1 substrate work cannot begin until both gates pass.
- Each Step PR carries its own mandatory dual-council SPOT-CHECK at PR-open (per Halt 3); Step 3's sec-eng SPOT-CHECK is optional. Engineer-time cost is real but proportionate to the substrate-tier change.
- The `MapBootstrapEndpoints` extension's call-site-scan analyzer (Step 3) is bounded by Roslyn's SemanticModel — direct constructor-parameter injection of both `IBootstrapContext` and any post-tenant interface IS detected; service-locator usage inside handler method bodies (e.g., `sp.GetRequiredService<ITenantContext>()` inside a handler) is NOT detected. The 80% case is covered (direct constructor injection); the 20% case (service-locator inside method bodies) remains code-review territory. This is the same trade-off ADR 0091 R2 A2 `RequestContextMixingAnalyzer` accepts.

### Trust impact / Security & privacy

- **Pre-auth surface area is now substrate-tier and explicit.** Before this ADR, the public signup endpoint family would have had to invent its own pre-tenant DI shape ad-hoc — exactly the path that produced ADR 0091's confused-deputy seam. Mandatory dual-council SPOT-CHECK on Step 1 + Step 2 PRs closes the seam at design time.
- **Mutual exclusion is enforced at compile time (PRIMARY) and at composition-root configuration time (SECONDARY) — two honest layers, both fail-closed (Rev 2).** Step 3 `BootstrapAndTenantMutualExclusionAnalyzer` (Roslyn; PRIMARY gate) walks constructor parameters of types registered inside `MapBootstrapEndpoints` and flags any constructor injecting both `IBootstrapContext` AND any post-tenant interface — this is the per-constructor enforcement layer. Startup `BootstrapAndTenantMutualExclusionAssertion` (`IHostedService`; SECONDARY gate) verifies registration-presence + composition-root opt-in (both helpers called; types implement their interfaces). The startup assertion CANNOT verify per-request mutual exclusion because the root container deliberately holds both bindings — that property is enforceable only by the per-constructor analyzer (or, in the Step 1–2 window, by reviewer + doc-comment discipline; the ADR 0091 R2 A2 precedent observed zero pipeline-mixing regressions in the equivalent window). The split is honest at ADR tier; both layers fail closed; neither is redundant.
- **`SunfishBridgeDbContext` empty-string-capture risk is structurally avoided by constructor guard + typed marker (Rev 2 / sec-eng Gap B minimum-floor).** The DbContext's constructor requires an explicit `RequireTenantBoundDbContext` typed marker that is registered exclusively in the non-bootstrap pipeline branch's DI scope (added in Step 2 / W79 hand-off). Resolving `SunfishBridgeDbContext` in bootstrap scope fails fast with `InvalidOperationException` because the required marker is unregistered. The empty-string-`HasQueryFilter` undefined-behavior scenario from ONR scaffold §2.2 cannot occur by construction; the FAIL-CLOSED invariant is ratified at ADR tier, not by docstring discipline. W79 may further harden via opt-out registration or a separate `SunfishBridgeReadOnlyDbContext` for the email-uniqueness check, but the constructor-guard minimum-floor is non-negotiable.
- **Bootstrap branch input policy is substrate-tier.** Origin-header validation + input length caps (CorrelationId ≤128, CaptchaToken ≤2048, IdempotencyKey ≤128) are ADR-tier minimum-floors per §"Bootstrap branch input policy" — not deferred to W79 hand-off. The bootstrap branch is the pre-auth surface; Step 2 PR-open SPOT-CHECK confirms all three caps are in place plus the Origin-validation middleware ordering.
- **`ITenantRegistry.CreateAsync` first-production-callsite is gated on ratified substrate.** Today's tests-only callsite becomes signup's production wiring only after ADR 0095 promotes to Accepted; W79 hand-off cannot specify the wiring before that gate clears.
- **Rate-limit + CAPTCHA policy substrate (Rev 2 non-permissive-default minimum-floor).** `IBootstrapContext.RateLimitBucketKey` + the bootstrap pipeline branch's `UseRateLimiter` + `CaptchaVerificationMiddleware` constitute the first-line anti-DDoS + anti-bot surface for public signup. The non-permissive-default minimum-floor (no fallback to AspNetCore RateLimiter default-allow; fixed-window 5/min signup, 10/min verify-email; 429 with `Retry-After`) is ratified at ADR tier per §"Forward configuration"; W79-scope is tightening, not loosening.
- **The mutual-exclusion invariant extends ADR 0091 R2's `TenantContextScopeAssertion` from 4 interfaces to 5 (the 5th being `IBootstrapContext`).** Step 1 ships the (honest) Layer 1 startup assertion; Step 3 ships Layer 2 (the analyzer). The bridging window relies on doc-comment + reviewer discipline + the constructor-guard on `SunfishBridgeDbContext` (Rev 2 minimum-floor) closing the highest-risk silent-data-leak path.

---

## Compatibility plan

### Migration path (3 steps)

**Step 1 — substrate ship (`packages/foundation-bootstrap/`).** Introduce `Sunfish.Foundation.Bootstrap.IBootstrapContext` + `Sunfish.Foundation.Bootstrap.DependencyInjection.AddSunfishBootstrapContext<TConcrete>` + `Sunfish.Foundation.Bootstrap.DependencyInjection.BootstrapAndTenantMutualExclusionAssertion`. No production consumer yet (consumers wire in Step 2 + W79 hand-off). Step 1 ships in one PR routed through full `sunfish-api-change` pipeline; mandatory dual-council (.NET-architect + security-engineering) SPOT-CHECK at PR-open. Include a `// TODO (Step 3 — ADR 0095): ship BootstrapAndTenantMutualExclusionAnalyzer` comment in the `IBootstrapContext` source as a forward-watch beacon (mirrors ADR 0091 R2 amendment A2's same-place marker).

**Step 2 — middleware + pipeline-branch composition root opt-in.** Ship `Sunfish.Foundation.Bootstrap.Middleware.BootstrapContextResolutionMiddleware` (resolves `IBootstrapContext` per request: generates `CorrelationId`, reads `X-Forwarded-For`-evaluated `ClientIp`, reads `CaptchaToken` from form payload + `IdempotencyKey` from header, computes `RateLimitBucketKey` from `ClientIp` + route). Ship `Sunfish.Foundation.Bootstrap.Routing.BootstrapEndpointRouteBuilderExtensions.MapBootstrapEndpoints` extension. Update signal-bridge SaaS-posture `Program.cs` composition root to add the `app.UseWhen(ctx => ...)` bootstrap branch BEFORE `TenantSubdomainResolutionMiddleware`. Step 2 ships in one PR; mandatory dual-council SPOT-CHECK at PR-open; security-engineering SPOT-CHECK confirms rate-limit policy values per Halt 4. The bootstrap branch initially has zero endpoints registered inside `MapBootstrapEndpoints` — endpoint shapes are W80 / W82 hand-off scope. (**Note on pattern-009:** Step 2 ships pipeline routing + the extension-method shape, NOT the 4 production endpoints. The W80 / W82 Stage-06 PRs that actually map the 4 signup endpoints carry pattern-009 SPOT-CHECK at their own PR-open per fleet conventions — see §SPOT-CHECK dispatch SLA in `.claude/rules/fleet-conventions.md`. Step 2's own dual-council SPOT-CHECK covers the pipeline-routing change, not pattern-009.)

**Step 3 — Roslyn analyzer + CI gate.** Ship `Sunfish.Foundation.Bootstrap.Analyzers.BootstrapAndTenantMutualExclusionAnalyzer` following the existing `foundation-wayfinder-analyzers` pattern (the same analyzer-package shape ADR 0091 R2 amendment A2's `RequestContextMixingAnalyzer` follows). Per Rev 2 / .NET-arch A6, the analyzer's scope is a CONSTRUCTOR PARAMETER SCAN (recursive across declared constructor parameter types; bounded by what Roslyn's SemanticModel can see — does NOT model `IServiceProvider.GetService` inside method bodies or factory delegates inside `services.AddScoped(sp => ...)`; same trade-off as ADR 0091 R2 A2 `RequestContextMixingAnalyzer`). The analyzer walks the syntactic call-site tree of `MapBootstrapEndpoints` and flags any registered handler whose constructor parameter scan reaches both `IBootstrapContext` AND any of `Sunfish.Foundation.Authorization.ITenantContext` (facade or any narrowed variant) OR `Sunfish.Bridge.Middleware.IBrowserTenantContext` OR any other post-tenant context interface registered via `AddSunfishTenantContext`. Additionally (Rev 2 / sec-eng Gap D): the analyzer flags any `MapBootstrapEndpoints`-registered endpoint whose endpoint-registration chain does NOT include `.AllowAnonymous()` (the inverse failure case — auth-required handler registered in the bootstrap branch). Diagnostic rule IDs + `Error` severity (per `BridgeAuditEmissionAnalyzer` precedent on shipyard#71) per Step 3 hand-off. CI gate wired. Step 3 ships in one PR; .NET-architect council SPOT-CHECK mandatory; security-engineering SPOT-CHECK optional (per Halt 3 disposition).

### Affected packages

- `shipyard/packages/foundation-bootstrap/` (new) — Step 1 ships the interface + DI helper + assertion; Step 2 ships the middleware + endpoint-routing extension; Step 3 ships the analyzer (analyzer in a sibling `foundation-bootstrap-analyzers` csproj per the `foundation-wayfinder-analyzers` precedent — exact sub-csproj layout is Engineer decision at Step 3 PR planning).
- `signal-bridge/Sunfish.Bridge/Program.cs` — Step 2 adds the `app.UseWhen(...)` bootstrap branch; calls `AddSunfishBootstrapContext<TConcrete>` for the concrete impl (Engineer ships the `BootstrapContextResolutionMiddleware`-resolved concrete in Step 2).
- `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` — Step 2 / W79 hand-off adds a constructor parameter of typed marker `RequireTenantBoundDbContext` (per Rev 2 / sec-eng Gap B minimum-floor) — small surface change; the marker is supplied only by the post-tenant DI branch so resolution in bootstrap scope fails fast. ADR-tier invariant "MUST NOT resolve in bootstrap scope" is enforced by the constructor guard.
- W79 / W80 / W82 Stage-05 hand-off documents reference ADR 0095 §Decision and Steps 1–3 as substrate prerequisites; first signup-endpoint consumer wires inside `MapBootstrapEndpoints` in W80 (Surface A+B). ADR 0095 itself does NOT ship any endpoint.

### Impact surface estimate

- Step 1 PR: ~5 files in shipyard (new package + Sunfish.Foundation.Bootstrap.csproj + IBootstrapContext.cs + BootstrapContextServiceCollectionExtensions.cs + BootstrapAndTenantMutualExclusionAssertion.cs + xmldoc-comment TODO marker). Net source compatibility: zero consumer changes required (no production consumer yet).
- Step 2 PR: ~4–5 files in shipyard (BootstrapContextResolutionMiddleware.cs + OriginValidationMiddleware.cs (per Rev 2 / sec-eng Gap C) + BootstrapEndpointRouteBuilderExtensions.cs + concrete `BootstrapContext.cs` + the post-tenant `RequireTenantBoundDbContext` typed marker per Rev 2 / sec-eng Gap B; the marker likely lives in `foundation-authorization` since that's where the post-tenant DI helper lives) + 1 file in signal-bridge (`Program.cs` composition-root update including explicit `UseRouting()` + the marker registration on the non-bootstrap branch + rate-limit policy registrations matching the Rev 2 minimum-floors). May include a concrete `CaptchaVerificationMiddleware` skeleton if W79 hand-off requires it pre-W79.
- Step 3 PR: ~3–4 files in shipyard (analyzer source + analyzer test fixtures + CI gate wire-in to existing analyzer-test infrastructure).

### Pattern-catalog cross-check

`standing-approved-patterns.md` `pattern-009` (Bridge endpoint + frontend rebind pair) covers Step 2's frontend-binding case in a forward-watch sense — but Step 2 ships pipeline-routing infrastructure and the `MapBootstrapEndpoints` extension, NOT the 4 signup production endpoints (those land in W80 / W82). The W80 / W82 PRs that ACTUALLY ship the new signup endpoint routes carry pattern-009 SPOT-CHECK at their own PR-open per fleet conventions. Step 2's own dual-council SPOT-CHECK (per Halt 3) covers the pipeline-routing change and the `MapBootstrapEndpoints` shape, not pattern-009 specifically.

---

## Implementation checklist

- [ ] **Step 1 PR — `packages/foundation-bootstrap/` substrate (substrate ship; no production consumer)**
  - [ ] Create `shipyard/packages/foundation-bootstrap/Sunfish.Foundation.Bootstrap.csproj` (mirrors `foundation-authorization` csproj layout; references `packages/foundation/` for shared primitives if any are reused)
  - [ ] Add `Sunfish.Foundation.Bootstrap.IBootstrapContext` interface (5 members: `CorrelationId`, `ClientIp`, `CaptchaToken`, `IdempotencyKey`, `RateLimitBucketKey`) with full xmldoc per §"Initial contract surface"
  - [ ] Add `// TODO (Step 3 — ADR 0095): ship BootstrapAndTenantMutualExclusionAnalyzer` marker comment in `IBootstrapContext.cs` (mirrors ADR 0091 R2 amendment A2 marker placement)
  - [ ] Add `Sunfish.Foundation.Bootstrap.DependencyInjection.BootstrapContextServiceCollectionExtensions.AddSunfishBootstrapContext<TConcrete>` extension method
  - [ ] Add `Sunfish.Foundation.Bootstrap.DependencyInjection.BootstrapAndTenantMutualExclusionAssertion` IHostedService (verifies at startup: REGISTRATION-PRESENCE — `IBootstrapContext` is registered scoped with a non-null `TConcrete` that implements the interface; PLUS COMPOSITION-ROOT-OPT-IN — both `AddSunfishBootstrapContext<>()` AND `AddSunfishTenantContext<>()` were called against `IServiceCollection`. Per-request mutual exclusion is enforced by the Step 3 analyzer, NOT by this assertion. Per Rev 2 / .NET-arch A1 + A3.)
  - [ ] Unit tests for the DI helper + the assertion (per Rev 2 / .NET-arch A3 — tests verify REGISTRATION-PRESENCE + composition-root contract, NOT resolution-validation of the production concrete; the production `BootstrapContext` concrete depends on `IHttpContextAccessor` which returns null at `IHostedService.StartAsync` time). Positive cases: (a) both helpers called → `StartAsync` succeeds; (b) only bootstrap helper called → `StartAsync` succeeds if the composition root opts into bootstrap-only mode (W79 hand-off may add a configuration flag) OR fails with a clear "no post-tenant helper called" diagnostic. Negative cases: (a) `AddSunfishBootstrapContext` not called but bootstrap branch registered → `StartAsync` fails; (b) `IBootstrapContext` registered without a `TConcrete` (e.g., `services.AddScoped<IBootstrapContext>(sp => null!)`) → `StartAsync` fails with non-null check. Mid-request behavior of the bootstrap concrete (correct `CorrelationId` / `ClientIp` / `CaptchaToken` / `IdempotencyKey` / `RateLimitBucketKey` population) is verified by Step 2 integration tests (real HTTP request through the pipeline), NOT by Step 1 startup-assertion tests.
  - [ ] Pass Step 1 PR readiness §"Council review" requirements below before opening PR
  - [ ] Both councils SPOT-CHECK GREEN before merge (per Halt 3)

- [ ] **Step 2 PR — middleware + pipeline-branch composition root opt-in (no signup endpoints yet; W80 ships those)**
  - [ ] Add `Sunfish.Foundation.Bootstrap.Middleware.OriginValidationMiddleware` (per Rev 2 / sec-eng Gap C — validates `Origin` header on POST/PATCH/DELETE against configured apex host; rejects mismatched / missing-on-state-mutating with HTTP 403 non-diagnostic body; FIRST middleware inside bootstrap branch before `BootstrapContextResolutionMiddleware`)
  - [ ] Add `Sunfish.Foundation.Bootstrap.Middleware.BootstrapContextResolutionMiddleware` (resolves `IBootstrapContext` per request: generates `CorrelationId` or echoes validated `X-Correlation-Id` (`^[A-Za-z0-9_-]{1,128}$`); reads `X-Forwarded-For`-evaluated `ClientIp` per ASP.NET Core `UseForwardedHeaders` policy; reads `CaptchaToken` from form payload (max 2048 chars, reject HTTP 400 if exceeded); reads `IdempotencyKey` from `X-Idempotency-Key` header (max 128 chars ULID-shape `^[0-9A-HJKMNP-TV-Z]{1,128}$`, reject HTTP 400 if exceeded); computes `RateLimitBucketKey` per §"Initial contract surface" xmldoc remarks — `ip:{normalized-ip}` if `ClientIp` present, `route:{normalized-path}` if `ClientIp` null)
  - [ ] Add `Sunfish.Foundation.Bootstrap.Routing.BootstrapEndpointRouteBuilderExtensions.MapBootstrapEndpoints(IEndpointRouteBuilder)` extension method (initially empty body — bootstrap endpoints register in W80 / W82)
  - [ ] Add concrete `Sunfish.Foundation.Bootstrap.BootstrapContext` class (or whatever Engineer-chosen concrete name) that the middleware populates
  - [ ] Update `signal-bridge/Sunfish.Bridge/Program.cs` SaaS-posture composition root to add explicit `app.UseRouting()` at top of pipeline (per Rev 2 / .NET-arch A4), then `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/signup") || ctx.Request.Path.StartsWithSegments("/api/invitations/accept"), bootstrapBranch => { UseMiddleware<OriginValidationMiddleware> + UseMiddleware<BootstrapContextResolutionMiddleware> + UseRateLimiter + UseAntiforgery + UseMiddleware<CaptchaVerificationMiddleware> + MapBootstrapEndpoints })` BEFORE `UseMiddleware<TenantSubdomainResolutionMiddleware>`. NOTE: the second outer `app.UseAntiforgery()` call from Rev 1 is REMOVED per Rev 2 / .NET-arch A5; non-bootstrap branch uses per-endpoint `[ValidateAntiForgeryToken]` instead.
  - [ ] Call `AddSunfishBootstrapContext<TConcrete>()` in DI registration
  - [ ] Wire the non-bootstrap pipeline branch's DI scope to register `RequireTenantBoundDbContext` typed marker (per Rev 2 / sec-eng Gap B); confirm bootstrap branch's DI scope does NOT register the marker (so `SunfishBridgeDbContext` constructor-guard fails fast in bootstrap scope per the minimum-floor)
  - [ ] Configure rate-limit policies for bootstrap branch per Rev 2 / sec-eng Gap E non-permissive-default minimum-floor: `POST /api/signup` (5/min/IP); `POST /api/signup/check-email-available` (20/min/IP burst 5); `GET /api/signup/verify-email/{token}` (10/min/IP burst 5); `POST /api/invitations/accept/{token}` (10/min/IP burst 0); fixed-window; 429 with `Retry-After`
  - [ ] Integration test: a request to `/api/signup/check-email-available` hits the bootstrap branch's middleware stack (verified via test scaffolding for the empty `MapBootstrapEndpoints` body — Engineer adds a test endpoint at PR-time or uses a probe-endpoint pattern)
  - [ ] Integration test: Origin-header missing or mismatched on POST → HTTP 403 with non-diagnostic body
  - [ ] Integration test: `CorrelationId` / `CaptchaToken` / `IdempotencyKey` exceeding length caps → HTTP 400 at middleware boundary
  - [ ] Pass Step 2 PR readiness §"Council review" requirements below before opening PR
  - [ ] Both councils SPOT-CHECK GREEN before merge (per Halt 3); security-engineering SPOT-CHECK confirms rate-limit policy values match Rev 2 minimum-floors + Origin validation + length caps are wired

- [ ] **Step 3 PR — `BootstrapAndTenantMutualExclusionAnalyzer` (Roslyn) + CI gate**
  - [ ] Add `Sunfish.Foundation.Bootstrap.Analyzers.BootstrapAndTenantMutualExclusionAnalyzer` source (sibling csproj per the `foundation-wayfinder-analyzers` pattern; exact sub-csproj layout is Engineer decision at Step 3 PR planning)
  - [ ] Analyzer performs CONSTRUCTOR PARAMETER SCAN (recursive across declared constructor parameter types; bounded by Roslyn SemanticModel — does NOT model `IServiceProvider.GetService` inside method bodies or factory delegates inside `services.AddScoped(sp => ...)`; same trade-off as ADR 0091 R2 A2 `RequestContextMixingAnalyzer`) on all types registered inside `MapBootstrapEndpoints` call-site tree; flags any handler whose scan reaches BOTH `IBootstrapContext` AND any of `Sunfish.Foundation.Authorization.ITenantContext` (facade or narrowed) OR `Sunfish.Bridge.Middleware.IBrowserTenantContext` OR any other post-tenant context interface registered via `AddSunfishTenantContext`
  - [ ] Analyzer additionally flags any `MapBootstrapEndpoints`-registered endpoint whose endpoint-registration chain does NOT include `.AllowAnonymous()` (per Rev 2 / sec-eng Gap D inverse failure case)
  - [ ] Diagnostic rule IDs + `Error` severity (per `BridgeAuditEmissionAnalyzer` precedent on shipyard#71); separate rule ID per detected case (mutual-exclusion violation; missing AllowAnonymous)
  - [ ] Analyzer test fixtures: (a) positive (handler with only `IBootstrapContext` + `AllowAnonymous` → no diagnostic); (b) negative-direct (handler with `IBootstrapContext` + `Sunfish.Foundation.Authorization.ITenantContext` → mutual-exclusion diagnostic); (c) negative-narrowed (handler with `IBootstrapContext` + `Sunfish.Foundation.MultiTenancy.ITenantContext` narrowed variant → diagnostic); (d) negative-browser (handler with `IBootstrapContext` + `IBrowserTenantContext` → diagnostic); (e) inverse-failure (handler in `MapBootstrapEndpoints` chain without `.AllowAnonymous()` → diagnostic)
  - [ ] CI gate wired into existing analyzer-test infrastructure
  - [ ] .NET-architect council SPOT-CHECK GREEN before merge; security-engineering SPOT-CHECK optional (per Halt 3)

---

## Out of scope but flagged (forward-watch)

Per Halt 6 disposition: ADR 0095 Rev 1 does NOT commit to an inheritance hierarchy for future bootstrap consumers. Two known future consumers are flagged for forward-watch:

- **Webhook-receiver bootstrap.** Stripe-style webhooks arrive at the apex host with no tenant in the URL; payload carries the tenant identity (e.g., a Stripe `Customer` whose metadata maps to a Sunfish tenant). The webhook handler's pre-payload-verification window is structurally similar to the signup pre-tenant window — needs `CorrelationId`, `ClientIp`, rate-limit bucket, idempotency — and would consume `IBootstrapContext` directly OR a derived `IWebhookContext` (with webhook-signature verdict added) once a 2nd consumer emerges.
- **Cross-tenant federation bootstrap.** A federated query arriving at the apex host with a federation token in the request envelope. Same shape: pre-payload-verification window with `IBootstrapContext`-shaped needs.

**Forward-watch policy:** if a 2nd-instance bootstrap consumer (webhook-receiver) emerges with surface that overlaps `IBootstrapContext` substantively, promote `IBootstrapContext` to the base interface at that ADR-amendment time (V11 #1 sub-pattern-split precedent: `IBootstrapContext` becomes the base; `IWebhookContext : IBootstrapContext` adds webhook-specific members; `ISignupBootstrapContext : IBootstrapContext` if signup surface diverges). Do NOT speculatively author the hierarchy before a 2nd consumer exists — YAGNI applies; speculative inheritance hierarchies authored before a 2nd consumer exists tend to optimize for the wrong axis.

**Rev 2 / .NET-arch A2 follow-on — `ITenantContextSeed` substrate.** The `ITenantContextSeed` scoped-holder service introduced in §"Handler Lifecycle" step 5 (canonical α-1 mechanism for the bootstrap → post-tenant child-scope transition) is W79 Stage-05 hand-off scope; its declaration is not in this ADR's substrate cluster (the `foundation-authorization` package, which already houses `AddSunfishTenantContext`, is the likely home — W79 chooses). If `ITenantContextSeed` grows beyond signup (e.g., a federated query handler needs to seed a tenant inside a child scope after token verification), it may warrant its own ADR amendment to ratify the broader use. ADR 0095 does NOT pre-declare the broader contract.

**Rev 2 / .NET-arch A7 forward-watch — OpenAPI / Swagger metadata for bootstrap endpoints.** Bootstrap endpoints will appear in the application's OpenAPI document by default. Some bootstrap endpoints (e.g., `/api/signup`) SHOULD appear in the public OpenAPI surface (it's how the React SPA's API client is generated); others (e.g., `/api/invitations/accept/{token}`) may be internal-only and warrant `WithMetadata(new ExcludeFromDescriptionAttribute())` at endpoint registration. OpenAPI / Swagger metadata for bootstrap endpoints is W80 / W82 hand-off scope; Step 2 `MapBootstrapEndpoints` extension does not pin any default OpenAPI behavior. Per-endpoint metadata (visible to SPA-client-gen vs internal-only) is declared at endpoint registration time inside the W80 / W82 PRs.

ADR 0096 (Email Dispatch Substrate) is a sibling forward-watch — currently on hold per onboarding-ladder ruling Decision 2 conditional on CIC ruling on email provider (Decision 4). ADR 0095 §"Handler Lifecycle" step 6 ("email-dispatch via ADR 0096 substrate") forward-references ADR 0096 but does NOT depend on it being authored first; signup substrate can ship before email substrate (signup-completion audit events fire whether the welcome email dispatches successfully or not).

**`dotnet watch` / hot-reload interaction with the IHostedService assertion (forward-watch; .NET-arch FW-1).** Hot-reload re-runs `Build()` but not `IHostedService.StartAsync` after a mid-session DI registration change. If a developer modifies `AddSunfishBootstrapContext` registration in mid-session, the assertion will not re-fire. This is a known .NET 11 hot-reload limitation; not Rev 2 scope but flagged here as a developer-experience nuance.

---

## Council review

Per Halt 3 disposition (MANDATORY pre-merge dual-council review):

### On the ADR text (Revision 1 → Revision 2)

- Both `.NET-architect` and `security-engineering` councils dispatched in parallel on Revision 1 (2026-05-25T15:31Z dispatch); both returned AMBER (`council-verdict-2026-05-25T1605Z-security-engineering-shipyard-153-adr-0095-rev-1.md`; `council-verdict-2026-05-25T1606Z-net-architect-shipyard-153-adr-0095-rev-1.md`). No RED blockers. Substantive amendments folded into Revision 2 (this revision) per the §"Revision history" Rev 2 row.
- Both councils dispatched again for Rev 2 re-attest. Both verdicts required GREEN before Rev 2 promotes from Proposed to Accepted.
- If either council returns AMBER on Rev 2, Admiral folds amendments into Revision 3 (same cadence precedent) before re-attest.
- **Admiral dispatch SLA:** 30 minutes from PR-status-beacon consumption per fleet convention.

### On Step 1 implementation PR

- Both councils SPOT-CHECK at PR-open (mandatory; not gated on ADR re-attest).
- `.NET-architect` reviews: package shape; DI helper ergonomics; startup assertion REGISTRATION-PRESENCE + composition-root opt-in correctness (per Rev 2 / .NET-arch A1 + A3 honest framing — not "no scope simultaneously resolves" claim); .NET 11 idiomatics; unit-test cases for both positive (both helpers called) + negative (only bootstrap helper / missing `TConcrete`) per Rev 2 Step 1 checklist.
- `security-engineering` reviews: mutual-exclusion two-layer split (Layer 1 IHostedService + Layer 2 Roslyn analyzer per Rev 2 §Decision drivers); xmldoc clarity on what Layer 1 does NOT claim; xmldoc clarity on the "MUST NOT resolve `SunfishBridgeDbContext` in bootstrap scope" invariant; placeholder for the typed-marker design (full Gap B Construction-guard shape lands in Step 2 PR).

### On Step 2 implementation PR

- Both councils SPOT-CHECK at PR-open (mandatory).
- `.NET-architect` reviews: middleware ordering inside the bootstrap branch (Origin → Bootstrap → RateLimiter → Antiforgery → Captcha per Rev 2 / §"Pipeline routing"); explicit `app.UseRouting()` placement; `app.UseWhen` composition; `MapBootstrapEndpoints` shape; `RequireTenantBoundDbContext` marker registration on the non-bootstrap branch.
- `security-engineering` reviews: Origin-validation middleware presence + apex-host policy + non-diagnostic 403 (per Rev 2 / sec-eng Gap C); input length caps wired in `BootstrapContextResolutionMiddleware` per Rev 2 floors; rate-limit policy values match Rev 2 non-permissive-default minimum-floor (5/min signup, 10/min verify-email, etc.) + 429 with `Retry-After`; CAPTCHA verification ordering; X-Forwarded-For evaluation; constructor-guard on `SunfishBridgeDbContext` is wired and fails-fast in bootstrap scope (per Rev 2 / sec-eng Gap B); `TenantSubdomainResolutionMiddleware` xmldoc contract preservation; per-endpoint antiforgery wiring on non-bootstrap branch (per Rev 2 / .NET-arch A5 single-call placement).

### On Step 3 implementation PR

- `.NET-architect` SPOT-CHECK at PR-open (mandatory).
- `security-engineering` SPOT-CHECK optional (per Halt 3).
- `.NET-architect` reviews: analyzer correctness — CONSTRUCTOR PARAMETER SCAN scope per Rev 2 / .NET-arch A6 (not "DI-resolved dependency tree"); diagnostic rule IDs (one per detection case: mutual-exclusion violation + missing-AllowAnonymous) + `Error` severity; test fixture coverage for all 5 cases per Rev 2 Step 3 checklist; CI gate wiring.

---

## Open questions

Carried-open from the 6-halt ruling §"Open-questions delta" + reshaped per Rev 2 council amendments:

- **Q1 (implementation-detail; .NET-architect resolves at Step 1 PR review).** Now reframed per Rev 2 / .NET-arch A1 + A3: the IHostedService assertion does NOT perform service-collection introspection NOR runtime resolution-attempt of the production concrete (the production concrete depends on `IHttpContextAccessor` which is null at `StartAsync`). The Layer 1 assertion verifies registration-presence + composition-root opt-in only (per §Decision drivers and Step 1 checklist). Q1 carry-open: does the assertion benefit from optional service-collection-snapshot introspection in addition (e.g., to flag misconfigured `Lifetime` enum on the bootstrap registration)? .NET-architect council resolves at Step 1 PR review; non-blocking.
- **Q2 (implementation-detail; Step 2 PR resolves — answer now ratified at Rev 2).** Antiforgery middleware lives bootstrap-branch-only (inside the `UseWhen` body); the non-bootstrap branch uses per-endpoint `[ValidateAntiForgeryToken]` (or equivalent endpoint metadata), NOT a second outer `app.UseAntiforgery()` call (per Rev 2 §"Pipeline routing" A5 reconciliation). Step 2 PR confirms the wire-in; sec-eng SPOT-CHECK confirms branch-internal antiforgery is configured for POST/PATCH bootstrap endpoints.
- **Q3 (CLOSED at Rev 2 / .NET-arch A8).** `DemoAuthWarningFilter` is an `IStartupFilter`, not a per-request middleware (signal-bridge `Program.cs` line ~293 in the SaaS-posture composition helper). It fires once at startup and reports on the COMPOSITION-ROOT registration of `DemoTenantContext`, not on per-request scope membership. The bootstrap pipeline branch does not change `DemoTenantContext`'s composition-root presence; the filter's "DemoTenantContext bound" check sees the same outcome as today. There is no bootstrap-branch interaction; Q3 is a no-op and closes here. Step 2 implementation does NOT need to exempt or adjust the filter.
- **Q4 (implementation-detail; W79 Stage-05 hand-off resolves — minimum-floor ratified at Rev 2).** `SunfishBridgeDbContext` lifecycle in bootstrap scope is bounded by the Rev 2 minimum-floor: constructor guard with `RequireTenantBoundDbContext` typed marker (per §Decision drivers and §"Substrate / layering notes"); resolution in bootstrap scope fails fast with `InvalidOperationException` due to missing marker. W79 may further harden via (a) opt-out registration; (b) separate `SunfishBridgeReadOnlyDbContext` for the email-uniqueness check; or (c) keep the constructor guard alone if no read-only DbContext is needed. Q4 is the choice among (a) / (b) / (c) at W79 hand-off; ADR 0095 does not pre-judge.

---

## Revisit triggers

- **2nd-instance bootstrap consumer emerges.** Webhook-receiver bootstrap or cross-tenant federation bootstrap reaches Stage-02 scoping. Trigger: file ADR 0095 Revision 2 amendment promoting `IBootstrapContext` to a base interface; add the new consumer's derived interface. V11 #1 sub-pattern-split precedent applies.
- **ADR 0096 (Email Dispatch Substrate) authors and integrates.** Email substrate's API surface may want a reference into `IBootstrapContext` (e.g., correlation_id flows from signup audit-event through to email dispatch). Confirm forward-watch reference in §"Out of scope" remains accurate; amendment is appropriate if a new contract field surfaces.
- **`SunfishBridgeDbContext` empty-string risk surfaces in another DbContext.** If a 2nd `SunfishBridgeDbContext`-shaped DbContext emerges with the same constructor-captures-TenantId pattern, ADR 0095's "MUST NOT resolve in bootstrap scope" invariant generalizes to "no `IMustHaveTenant`-keyed DbContext resolves in bootstrap scope." Amendment captures the generalization.
- **Layer 1 startup-assertion scope hardens (Rev 2 reframed Q1).** Once .NET-architect council resolves at Step 1 PR review whether the Layer 1 IHostedService benefits from optional service-collection-snapshot introspection (e.g., to flag misconfigured `Lifetime` enum on the bootstrap registration) in addition to the registration-presence + composition-root opt-in checks, an amendment may codify the implementation path.
- **Forward-watched Q1 of ONR scaffold's §8 list: production OIDC-impl ADR.** If a future ADR introduces a production OIDC impl that touches both bootstrap and post-tenant scopes, confirm ADR 0095's mutual-exclusion invariant is preserved (OIDC impl belongs in post-tenant scope; bootstrap retains its small surface).

---

## References

### Predecessor and sister ADRs

- [ADR 0008](./0008-foundation-multitenancy.md) — `Foundation.MultiTenancy.ITenantContext`; pre-tenant case named in §"Open questions" item 3 (resolved indirectly here for the pre-tenant slice).
- [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Hybrid Multi-Tenant SaaS; Wave 5.1 control-plane vs data-plane boundary that this ADR makes type-enforceable for the pre-tenant case.
- [ADR 0069](./0069-adr-authoring-discipline.md) — pre-merge council requirement; mandates the dual-council SPOT-CHECK on Step 1 + Step 2 PRs.
- [ADR 0091](./0091-itenantcontext-divergence-resolution.md) — `ITenantContext` divergence resolution; precedent for the DI-helper + startup-assertion + analyzer ergonomics (Revision 2 amendments A1 + A2). The 5-interface mutual-exclusion invariant this ADR introduces is a strict extension of ADR 0091 R2's 4-interface invariant.
- [ADR 0093](./0093-stage-05-adversarial-review-protocol-amendment.md) — Stage-05 Adversarial Review Protocol; governs the Step 1 / Step 2 / Step 3 PR review cadence on this substrate.
- [ADR 0094](./0094-i-audit-event-reader.md) — `IAuditEventReader`; substrate-ADR cadence reference (kernel-tier read-side primitive with dual-council attestation; analogous shape).

### Roadmap and specifications

- `admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md` — Decision 1 (Option A APPROVED: new ADR). Decisions 3 + 7 + 8 affect downstream substrate-cluster routing.
- `admiral-ruling-2026-05-25T1531Z-adr-0095-bootstrap-context-6-halt-conditions.md` — all 6 halts disposed RATIFY-ONR-RECOMMENDATION; 0 DEFERRED-CIC.
- V8 #3 onboarding-ladder Stage-02 scoping — `shipyard/icm/01_discovery/research/onboarding-ladder-stage-02-scoping.md`.
- V11 #4 10-decisions resolution scaffold for Admiral ruling — `shipyard/icm/01_discovery/research/onboarding-ladder-10-decisions-scaffold-for-admiral-ruling.md`.
- W79 / W80 / W82 Stage-05 hand-off documents — TBD; gated on this ADR's promotion to Accepted (W79 substrate) and on ADR 0096 (W80 email) per onboarding-ladder ruling Decision 10 sequencing (`1 -> 2 -> (3+4 parallel) -> 5`).

### Existing code / substrates

- `signal-bridge/Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs` — middleware whose contract this ADR preserves intact by routing bootstrap requests through a separate pipeline branch.
- `signal-bridge/Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` — data-plane context; mutual-exclusion with `IBootstrapContext` extends ADR 0091 R2's 4-interface invariant to 5.
- `signal-bridge/Sunfish.Bridge/Services/TenantRegistry.cs` + `ITenantRegistry.cs` — `CreateAsync` first-production-callsite invariant per §"Decision drivers."
- `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` — DbContext whose constructor-captures-TenantId behavior is the ADR-required "MUST NOT resolve in bootstrap scope" invariant.
- `shipyard/packages/foundation-authorization/DependencyInjection/TenantContextServiceCollectionExtensions.cs` — `AddSunfishTenantContext<TConcrete>` ergonomics mirror.
- `shipyard/packages/foundation-authorization/DependencyInjection/TenantContextScopeAssertion.cs` — startup assertion shape mirror.

### Research and ratification

- `shipyard/icm/01_discovery/research/onr-adr-0095-bootstrap-context-scaffold.md` — 753-line ONR scaffold; the canonical research input. Sections 1–6 source the §Context, §Considered options, §Pipeline routing, §Handler Lifecycle, §Forward configuration, and §References blocks of this ADR.
- ONR scaffold §7 (6 halt conditions) — all disposed by `admiral-ruling-2026-05-25T1531Z-adr-0095-bootstrap-context-6-halt-conditions.md`; rulings folded into this ADR's §Decision + §Compatibility plan + §"Out of scope but flagged" + §"Council review" sections.

### External

- Finbuckle.MultiTenant `[SkipTenantResolution]` attribute pattern — `Finbuckle/Finbuckle.MultiTenant` GitHub README. Reference architecture cited in ONR scaffold §5.1; rejected for the same reason this ADR rejects Option D — attribute-driven mutual exclusion is more error-prone than DI-driven mutual exclusion.
- ASP.NET Core `IHostBuilder` / `WebApplicationBuilder` pre-resolution patterns — Microsoft Learn docs (general knowledge). Reference architecture cited in ONR scaffold §5.2; shape-analog for the "small, scoped, with explicit contract about what it's for" pattern.
- .NET Aspire app-host composition model — Aspire preview docs (general knowledge). Reference architecture cited in ONR scaffold §5.3; shape-analog for the `AddSunfishBootstrapPipeline` extension method.
- ASP.NET Core `RateLimiter` — Microsoft Learn docs. Implementation substrate per onboarding-ladder ruling Decision 6 (Option A APPROVED).

---

## Pre-acceptance audit (5-minute self-check)

> **D1 — substrate-tier ADRs:** Do NOT enable auto-merge before pre-merge council returns a verdict. Set PR description to "Awaiting pre-merge dual-council per ADR 0069 + ADR 0095 §Council review." Dispatch Opus + xhigh council subagents with explicit structural pressure-test points. Apply amendments; then enable auto-merge. See [ADR 0069](./0069-adr-authoring-discipline.md) §D1.

- [x] **AHA pass.** Considered 5 alternatives (Options A / B / D / E in addition to recommended Option C — see §"Considered options"). Documented why each was rejected. Reused ONR scaffold's analysis; Admiral ruling RATIFY-ratified Option C.
- [x] **FAILED conditions / kill triggers.** Named ≥1 condition under which this decision should be reversed or aborted: Step 1 PR's dual-council SPOT-CHECK returning BLOCKING on the cluster home (Halt 1) or pipeline routing (Halt 2) reopens those halts; if the Step 3 analyzer's constructor-parameter-scan scope (Rev 2 / .NET-arch A6) is judged insufficient by .NET-architect council AND no equivalent mechanism is feasible (e.g., even constructor-parameter scan misses too much of the real-world failure mode), Step 3 cannot ship as substrate-protection and the ADR re-opens Layer 2 enforcement design.
- [x] **Rollback strategy.** Step 1 ships substrate with no production consumer; rolling back is a single `git revert` on the Step 1 PR. Step 2 introduces a no-endpoint bootstrap branch; rolling back removes the `app.UseWhen` block (one-PR revert). Step 3 ships an analyzer + CI gate; rolling back removes the analyzer package + CI gate (one-PR revert). All three steps are revertible in isolation; no irreversible substrate change across the migration. W79 hand-off authoring is gated on this ADR Accepted; if rollback occurs before W79 lands, no production wiring is at risk.
- [x] **Confidence level.** HIGH (Rev 2). The ONR scaffold's analysis is thorough (753 lines, 21 cited sources), the 6 halt conditions are all disposed under Admiral authority (no DEFERRED-CIC), the ergonomics mirror an Accepted-with-GREEN-dual-council substrate ADR (0091 R2), and Rev 2 incorporated all 8 dual-council AMBER amendments — both councils confirmed no RED blockers; substrate-shape attestation is GREEN-attestable on Rev 2. Confidence-reducers: Layer 1 startup-assertion scope-hardening (Rev 2 reframed Q1) is unresolved at Step 1 PR review (non-blocking); other open questions are now ADR-tier-resolved or Step 2 / W79 hand-off scope.
- [x] **Cited-symbol verification.** Every `Sunfish.*` symbol cited in §Decision + §"Compatibility plan" + §"Implementation checklist" + §"Out of scope but flagged" has been verified to exist at the cited name + namespace (existing surfaces) OR is explicitly marked "Introduced by this ADR" with a Step number in §A0 (introduced surfaces, all 6 added in Step 1 / Step 2 / Step 3 PRs per checklist). `ITenantRegistry.CreateAsync` first-production-callsite invariant verified by repo-wide grep on signal-bridge (zero production callers; tests + xmldoc + migration-comment + seeder-bypass note are the only references).
- [x] **Anti-pattern scan.** Glanced at the 21-AP list in `.claude/rules/universal-planning.md`. AP-1 (unvalidated assumptions): the ONR scaffold validated all load-bearing assumptions via codebase grep + ADR cross-reference. AP-3 (vague success criteria): each Step has concrete checklist items with verifiable outputs. AP-9 (skipping Stage 0): ONR scaffold IS Stage-0 for this ADR. AP-12 (timeline fantasy): no timelines in this ADR — sequence is gated on council verdicts + W79 hand-off authoring. AP-21 (assumed facts without sources): every load-bearing claim cites either ADR 0091 R2 + amendments, the ONR scaffold, the 6-halt ruling, or a verifiable code location.
- [x] **Revisit triggers.** 5 conditions named in §"Revisit triggers" (2nd-instance bootstrap consumer; ADR 0096 integration; SunfishBridgeDbContext-shape generalization; Layer 1 startup-assertion scope hardening per Rev 2 reframed Q1; future production OIDC-impl).
- [x] **Cold Start Test.** A fresh Engineer reading this ADR + ONR scaffold + 6-halt ruling should be able to execute Step 1 PR from this ADR alone. Step 1 implementation checklist names every file to create + every member to add + every test to write (with Rev 2 honest framing of what the IHostedService assertion actually verifies — REGISTRATION-PRESENCE + composition-root opt-in, not "no scope simultaneously resolves"). Step 2 + Step 3 reference the W79 / W80 / W82 hand-off scopes for the bounded open implementation details (reframed Q1; Q2 ratified at Rev 2; Q3 closed at Rev 2; Q4 bounded by Rev 2 constructor-guard minimum-floor) but do not require the hand-offs to exist before Step 1 ships.
- [x] **Sources cited.** Every load-bearing factual claim has a reference: ADR 0091 R2 + amendments precedents; ADR 0031 Wave 5.1 boundary; ADR 0049 read-write asymmetry analog; ADR 0069 council discipline; ADR 0093 review protocol; ADR 0094 substrate cadence; ONR scaffold for status-quo audit; 6-halt ruling for disposition cites; fleet conventions for pattern-009 + SPOT-CHECK SLA; onboarding-ladder ruling for upstream context.
