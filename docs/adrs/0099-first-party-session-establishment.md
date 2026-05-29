---
id: 99
title: First-Party Session Establishment Substrate
status: Accepted
date: 2026-05-29
proposed-date: 2026-05-29
author: ONR
tier: foundation
pipeline_variant: sunfish-api-change

concern:
  - session-management
  - authentication
  - csrf
  - cookie-security
  - multi-tenant-isolation
  - single-use-token-atomicity
  - production-safety

enables:
  - w79-password-login-completion
  - w79-verify-email-auto-login
  - wse-magic-link-session-establishment
  - production-claims-backed-tenant-context
  - byo-oidc-category-provider-future

composes:
  - 84   # TenantId Sentinel Governance (sentinel-tenant guard; a session MUST NOT bind to TenantId.System)
  - 91   # ITenantContext Divergence Resolution (R2 §2.2 tenant cross-check + §2.3 single-concrete-class invariant; SessionBackedTenantContext replaces DemoTenantContext via the facade)
  - 95   # Bootstrap Context substrate (verify-email + magic-link consume run on the pre-tenant bootstrap branch; session-establish is the privilege-elevation event out of bootstrap scope)
  - 96   # Tier-2 Vendor-Provider Substrate (a future BYO-OIDC IdP is a Tier-2 category-provider; the production-guard + mock-first discipline precedent informs the demo-seam disposition)
  - 97   # PasswordHasher Substrate (the credential-verify half; /auth/login calls IPasswordHasher.VerifyHashedPassword then establishes the session — two halves of one login gate)
  - 98   # Block-Naming Generalization (substrate-tier ADR cadence + dual-council MANDATORY precedent)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-tier ADR; ADR text + each Step 1/2/3/4 implementation PR carries mandatory dual-council per H9 / §"Council review"

amendments:
  - rev: 2
    date: 2026-05-29
    author: ONR
    summary: >
      Dual-AMBER fold. Folds all 7 security-engineering amendments (A-G; council-verdict
      2026-05-29T0305Z) + all 8 .NET-architect amendments (A1-A8) + 6 clarifications (C1-C6;
      council-verdict 2026-05-29T0315Z). Applies Admiral-ruled defaults for O-1 (auto-login-on-verify,
      POST-gated) + O-3 (rate-limit v1 floor, CAPTCHA deferred Tier-2). No RED; substrate spine
      attested correct. See §"Revision 2 fold" for the amendment→resolution map. Awaiting dual-council RE-ATTEST.
---

# ADR 0099 — First-Party Session Establishment Substrate

**Status:** Accepted (dual-council re-attest GREEN 2026-05-29). Rev 1 returned dual-AMBER (sec-eng + .NET-architect); Rev 2 folds all 15 amendments + 6 clarifications + applies the Admiral-ruled O-1/O-3 defaults — see §"Revision 2 fold"; both councils re-attested GREEN on the AMBER fold per ADR 0069 + the substrate-tier Halt-9 cadence established by ADR 0095/0096/0097/0098. Dual-council MANDATORY (security-engineering + .NET-architect) on this ADR text AND on each implementation PR per §"Council review".

**Date:** 2026-05-29

**Resolves:** The MVP launch gate's *un-named half*. ADR 0097 (PasswordHasher, Argon2id) verifies a credential; it does not establish a session. W#79 signup/verify-email shipped (`admiral-ruling-2026-05-28T1630Z-w79-closed-main-fixed-pattern-009-ratified.md`) but the system has **no `/auth/login` route and no session-issuance mechanism anywhere** — a verified user cannot log in. The MVP feature-priority survey (`research-mvp-feature-priority-2026-05-29T0205Z.md`) correctly names ADR 0097 as launch-blocking but has a blind spot: hashing a password without session-issuance still cannot log anyone in. This ADR is the co-equal other half of the auth launch gate. It also resolves WS-E Halt **H-WSE-2** (the magic-link verify step's session-establishment dependency, `wse-tenant-comms-hand-off.md` §3 / shipyard#173).

**Decision (settled at dispatch):** BUILD first-party session establishment — cookie-based, same-origin Backend-for-Frontend (BFF) posture, server-side (stateful) session store. CIC delegated build-vs-buy to the research (`onr-session-substrate-research-2026-05-29.md`, shipyard#175); the research recommended BUILD first-party (cookie/BFF v1); Admiral is proceeding on that. A future BYO-OIDC IdP is a deferred Tier-2 `category-provider` (ADR 0096 discipline) that layers an additional authentication *scheme* on top of this same session machinery — it does not remove the need for the first-party substrate.

**Predecessor research:** `shipyard/icm/01_discovery/research/onr-session-substrate-research-2026-05-29.md` (248 lines; ONR; shipyard#175) — ground-truth of the orphaned `MockOktaService` + the build-vs-buy comparison + the S1-S11 security surface + the PR decomposition this ADR ratifies. Sibling: `shipyard/icm/05_implementation-plan/wse-tenant-comms-hand-off.md` (shipyard#173) — the magic-link → session seam (H-WSE-2) and the atomic `TryConsume` redemption precedent. Companion scoping: `shipyard/icm/01_discovery/research/production-oidc-impl-adr-scoping-2026-05-20.md` — the sibling OIDC-impl ADR (this substrate is its partial prerequisite + superset; see §1.4).

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-29 | ONR | Initial draft. Folds the session-substrate research (`onr-session-substrate-research-2026-05-29.md`, shipyard#175) recommendation (BUILD first-party; cookie/BFF v1; server-side session store) per Admiral dispatch on the settled CIC build-vs-buy decision. Codifies the `ISessionEstablisher` single-seam abstraction (login + verify-email + magic-link all route through it); ASP.NET Core `AddAuthentication().AddCookie()` + `app.UseAuthentication()` (ordered before `UseAuthorization`); `ISessionStore` server-side opaque-id store (in-memory v1 → SQLite/Postgres prod, mirroring the JournalStore in-memory→backed progression); `SessionBackedTenantContext` replacing `DemoTenantContext` (single concrete class implementing the `Foundation.Authorization` sum-interface facade per ADR 0091 R2 §2.3, unified with the OIDC-impl scoping's `ClaimsBackedTenantContext`); the S1-S11 security surface as ratified requirements; the `Auth.*` AuditEventType constants; the demo-seam + `MockOktaService` disposition. Eight ADR-text halt conditions (H1-H8). Five-PR decomposition (ADR + 4 impl PRs). Status: Proposed (awaiting dual-council). |
| 2 | 2026-05-29 | ONR | **Dual-AMBER fold** (no RED; substrate spine attested correct — floor-lifts, not redesign). Folds **sec-eng A-G** (`council-verdict-2026-05-29T0305Z`): (A) CSRF positive testable invariant — default-secure global `.RequireAntiforgery()` + reviewed opt-out + login-CSRF pre-auth-token / post-`SignInAsync` re-issue; (B) TTL 8h-absolute/30min-idle server-side-enforced; (C) S1 fixation regen MECHANISM + no-pre-auth-record; (D) reuse `IsSystemSentinel` + fail-closed on unresolved subdomain; (E) base64url entropy floor + `FixedTimeEquals` on the S9 token compare; (F) middleware ordering (merged with .NET-arch A1); (G) single-concrete-class-PER-SCOPE. Folds **.NET-arch A1-A8 + C1-C6** (`council-verdict-2026-05-29T0315Z`): (A1) 4-constraint `UseAuthentication` ordinal; (A2) production-guard PRESENCE polarity; (A3) `DemoTenantContext` already dev-gated (shipyard#44) — narrow PR-2 delta; (A4) cite LANDED `foundation-authorization` precedent; (A5/O-6) pinned `ISessionEstablisher` signature; (A6) opaque-id-only cookie via `OnValidatePrincipal` rehydration; (A7) reuse `AddSunfishTenantContext<TConcrete>`; (A8/O-7) `foundation-session` acyclic ProjectRef direction. Applies **Admiral-ruled** O-1 (auto-login-on-verify, POST-gated) + O-3 (rate-limit v1 floor; CAPTCHA deferred Tier-2). Status: Proposed (awaiting dual-council RE-ATTEST). |

Promotion path: both councils self-attest GREEN via inbox status on Revision 2's fold → Admiral promotes ADR to `Accepted`. (Rev 1 returned dual-AMBER; Rev 2 folds all amendments per the ADR 0095 R2 / 0096 R2 / 0097 R2 / 0098 R2 precedent.) **Each implementation PR (Step 2 substrate, Step 3 `/auth/login`, Step 4 verify/magic-link, Step 5 backed store) carries its own mandatory dual-council SPOT-CHECK at PR-open** per H9 — independent council pulls on the session/auth surface, not gated on ADR re-attest. Note also: any route under `/api/v1/cockpit/auth/**` or `/api/v1/cockpit/identity/**` is identity-surface and is **always full-pipeline dual-council** — NOT `pattern-009`-eligible (`standing-approved-patterns.md` pattern-009 "Does NOT match" criteria).

---

## A0 cited-symbol audit

Per the ADR 0093 / 0096 / 0097 cited-symbol audit discipline. Classifications: **Existing & verified** (on `shipyard/main` or `signal-bridge/main` at authoring, path-checked); **Introduced by this ADR** (ships in a Step PR); **In-flight, pending merge** (file exists on an OPEN PR's branch, not yet on main).

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.Session.ISessionEstablisher` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Session.ISessionStore` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Session.InMemorySessionStore` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Session.SessionRecord` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Session.SessionEstablishmentReason` (enum) | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Foundation.Session.DependencyInjection.AddSunfishSessionEstablishment` | Introduced by this ADR | no — added in Step 2 PR |
| `Sunfish.Bridge.Authorization.SessionBackedTenantContext` | Introduced by this ADR (replaces DemoTenantContext) | no — added in Step 2 PR |
| `Sunfish.Bridge.Features.Auth.AuthEndpoints` (`/auth/login`, `/auth/logout`) | Introduced by this ADR | no — added in Step 3 PR |
| `Sunfish.Bridge.Authorization.DemoTenantContext` | Existing — **ALREADY `IsDevelopment()`-gated** (ADR 0091 Step 1, shipyard#44; NOT gated by this ADR — .NET-arch A3). This ADR adds the **production counterpart** `SessionBackedTenantContext` (the production path currently registers NO `ITenantContext`). | yes — `signal-bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` (hardcoded `demo-tenant` / `demo-user` / `Manager` / `HasPermission => true`; one-time DEMO-AUTH warning; xmldoc says "replaced by a claims-backed ITenantContext before production"); dev-gate verified at `Program.cs` `if (builder.Environment.IsDevelopment()) { AddSunfishTenantContext<DemoTenantContext>(); ... }` |
| `Sunfish.Foundation.Authorization.SeededTenantContext` | Existing — SECOND concrete facade impl on main (bootstrap→post-tenant child scope; ADR 0095 α-1 / ADR 0091 R2 A1). Coexists with `DemoTenantContext` (dev) + the new `SessionBackedTenantContext` (prod) — three SCOPE-DISJOINT impls (sec-eng G / .NET-arch G3). | yes — `shipyard/packages/foundation-authorization/SeededTenantContext.cs` |
| `Sunfish.Foundation.Authorization.DependencyInjection.AddSunfishTenantContext<TConcrete>` | Existing (ADR 0091 R2 §A1 DI helper) — the SANCTIONED facade-registration path: aliases all facade interfaces to one scoped instance + installs `TenantContextScopeAssertion : IHostedService` same-instance check. `SessionBackedTenantContext` registers VIA this helper (.NET-arch A7), NOT hand-rolled. | yes — `shipyard/packages/foundation-authorization/` (ADR 0091 R2 §A1) |
| `Sunfish.Foundation.MultiTenancy.TenantId.IsSystemSentinel` | Existing — rejects `default(TenantId)`, `__system__`, AND any `__`-prefixed sentinel. H4/S8 reuses THIS guard (sec-eng D1), not only ADR 0084. | yes — `shipyard/packages/foundation-multitenancy/TenantQueryFilterExtensions.cs:159` (`TenantId.System == "__system__"`; prior sec-eng SPOT-CHECK amendment C1, 2026-05-21) |
| `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals` | Existing (BCL; ADR 0097 ratified its use) — MANDATORY for any in-memory compare of a candidate single-use token against a stored value on the S9 consume path (sec-eng E1). | yes — ADR 0097 §discipline (line 685); BCL |
| `Sunfish.Bridge.Authorization.AuthenticatedTenantPolicy` | Existing (consumer; unchanged shape) | yes — `signal-bridge/Sunfish.Bridge/Authorization/AuthenticatedTenantPolicy.cs` |
| `Sunfish.Bridge.Authorization.AccountantPolicy` | Existing (consumer; unchanged shape) | yes — `signal-bridge/Sunfish.Bridge/Authorization/AccountantPolicy.cs` |
| `Sunfish.Bridge.Authorization.DemoAuthWarningFilter` | Existing (dev-only seam; disposition §6) | yes — `signal-bridge/Sunfish.Bridge/Authorization/DemoAuthWarningFilter.cs` |
| `Sunfish.Bridge.Features.Identity.IdentityEndpoints` | Existing (reads `ctx.User.FindFirst("sub")` OR `ClaimTypes.NameIdentifier` from an unpopulated principal — this ADR populates it; the rehydrated principal uses the literal `"sub"` for determinism, C5) | yes — `signal-bridge/Sunfish.Bridge/Features/Identity/IdentityEndpoints.cs:160-161` (`ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value`); 6 `.RequireAuthorization()` routes (lines 28-32) |
| `app.UseAuthorization()` / `AddAuthorization` / `AddAntiforgery` / pipeline order | Existing (Bridge `Program.cs`) | yes — current pipeline (line numbers re-verified by .NET-arch at attestation; the Rev-1 `:173`/`:486`/`:631`/`:497` were STALE): `:185 MapBootstrapEndpoints()` (ADR-0095 UseWhen branch; self-composes routing+ratelimiter; bypasses subdomain mw), `:191 UseMiddleware<TenantSubdomainResolutionMiddleware>()` (binds `IBrowserTenantContext`), `:195 UseWebSockets()`, `:197 UseAntiforgery()`, `:198 UseAuthorization()`. **NO `AddAuthentication`/`AddCookie`/`AddJwtBearer`/`AddIdentity`/`UseAuthentication` anywhere** (re-confirmed by grep). The `UseAuthentication` insert point (H6 / sec-eng F / .NET-arch A1) is AFTER `:191` and BEFORE `:197`/`:198`. |
| `SunfishBridgeDbContext` ctor dependency on `ITenantContext` | Existing — registered unconditionally in `Program.cs`; the Production composition path currently registers NO `ITenantContext` (DemoTenantContext is dev-gated), so Bridge is presently **un-deployable to Production** (DbContext dependency unsatisfiable). PR 2's `SessionBackedTenantContext` registration is what makes Bridge Production-deployable w.r.t. `ITenantContext` (.NET-arch A2). | yes — `signal-bridge/Sunfish.Bridge/` (DbContext unconditional registration) |
| `Sunfish.Foundation.Authorization.ITenantContext` (sum-interface facade) | Existing — **LANDED Tier-1 package**; the placement precedent for the new `foundation-session` package (.NET-arch A4: cite this LANDED package, NOT the in-flight `foundation-password-hashing`). It is also the exact package whose facade `SessionBackedTenantContext` implements + demonstrates the NU1510 NoWarn pattern. | yes — `shipyard/packages/foundation-authorization/ITenantContext.cs` + `Sunfish.Foundation.Authorization.csproj` (ADR 0091 Step 1, on main) |
| `Sunfish.Foundation.Authorization.ICurrentUser` | Existing | yes — `shipyard/packages/foundation-authorization/` (ADR 0091 R2 Step 1) |
| `Sunfish.Foundation.Authorization.IAuthorizationContext` | Existing | yes — `shipyard/packages/foundation-authorization/` (ADR 0091 R2 Step 1) |
| `Sunfish.Bridge.Middleware.IBrowserTenantContext` / `TenantSubdomainResolutionMiddleware` | Existing (data-plane; subdomain-resolved tenant for the S8 cross-check) | yes — `signal-bridge/Sunfish.Bridge/Middleware/` (ADR 0091 R2 §A0; MUST-NOT-mix with control-plane contexts) |
| `Microsoft.AspNetCore.Identity.IPasswordHasher<TUser>.VerifyHashedPassword` → `PasswordVerificationResult` | Existing (BCL; ADR 0097 substrate) | yes — ADR 0097 `Argon2idPasswordHasher<TUser>` returns `Success` / `SuccessRehashNeeded` / `Failed`; `/auth/login` consumes this |
| `Sunfish.Foundation.PasswordHashing.Argon2idPasswordHasher<TUser>` + `packages/foundation-password-hashing/` | In-flight (ADR 0097 Step 1; Proposed Rev 2) | partial — ADR 0097 on main; Step 1 substrate package + `packages/foundation-password-hashing/` are in Engineer's flight, **NOT yet on `shipyard/main`** (grep empty). Cited only as a *sibling* own-package-geometry precedent (the same Tier-1 own-package shape ADR 0097 adopts); the LANDED placement authority is `foundation-authorization` (.NET-arch A4). |
| `TenantMagicLinkIssued` / `TenantMagicLinkConsumed` (AuditEventType) | In-flight (WS-E hand-off shipyard#173; not yet built) | partial — specified in `wse-tenant-comms-hand-off.md`; this ADR adds the `Auth.SessionEstablished.MagicLink` counterpart on the consume→session edge |
| `MockOktaService` (standalone `Sdk.Web` mock OIDC IdP) | Existing (orphaned; disposition §6) | yes — `signal-bridge/MockOktaService/` (full Okta-shaped mock; PKCE; NO password validation; never wired to Bridge via `AddJwtBearer`) |
| ADR 0091 R2 §2.2 (tenant cross-check) / §2.3 (single-concrete-class facade) | Existing | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` |
| ADR 0095 (Bootstrap Context) | Existing | yes — `shipyard/docs/adrs/0095-bootstrap-context.md` |
| ADR 0096 (Tier-2 Vendor-Provider Substrate) | Existing (Proposed) | yes — `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md` |
| ADR 0097 (PasswordHasher Substrate) | Existing (Proposed) | yes — `shipyard/docs/adrs/0097-passwordhasher-substrate.md` |
| `standing-approved-patterns.md` pattern-009 ("/api/v1/cockpit/auth/** … always full pipeline") | Existing | yes — `shipyard/_shared/engineering/standing-approved-patterns.md` lines 279-318 (identity-surface carve-out at line 290) |

§A0 totals (Rev 2): 33 cited references. Existing & verified: 24 (Rev 2 added six dual-council-flagged existing symbols: `SeededTenantContext`, `AddSunfishTenantContext<TConcrete>`, `TenantId.IsSystemSentinel`, `CryptographicOperations.FixedTimeEquals`, `SunfishBridgeDbContext`-ctor-dependency, and refined the `DemoTenantContext`/pipeline-order rows). Introduced by this ADR: 7 (`ISessionEstablisher`, `ISessionStore`, `InMemorySessionStore`, `SessionRecord`, `SessionEstablishmentReason`, `AddSunfishSessionEstablishment`, `SessionBackedTenantContext` + `AuthEndpoints`). In-flight pending merge: 2 (`Argon2idPasswordHasher<TUser>` + `packages/foundation-password-hashing/` ADR 0097 Step 1; the WS-E magic-link audit-event family).

---

## Context

The fleet shipped signup + verify-email (W#79, CLOSED) and ratified a password-hashing primitive (ADR 0097, Argon2id, Proposed→Accepted-track). Neither establishes a session. Ground-truth (research §1, re-verified at this ADR's authoring):

1. **Bridge has authorization, not authentication.** `Program.cs` has `AddAuthorization`, `UseAuthorization` (`:198`; the Rev-1 `:173`/`:486`/`:631`/`:497` line refs were stale — .NET-arch re-verified the current pipeline), `UseAntiforgery` (`:197`) and `AddAntiforgery(... "X-XSRF-TOKEN")` — but **no `AddAuthentication`, no `AddCookie`, no `AddJwtBearer`, no `AddIdentity`, and no `app.UseAuthentication()`**. There is no authentication *scheme* registered. `RequireAuthorization()`'s `RequireAuthenticatedUser()` checks `HttpContext.User.Identity.IsAuthenticated`; with no scheme, `HttpContext.User` is the anonymous principal. `IdentityEndpoints` (`:160-161`) reads `ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value` from a principal that, in production, is never populated (this ADR populates it with `"sub"` — C5).

2. **The only thing that "logs anyone in" today is `DemoTenantContext`** — a dev-only DI service hardcoding `demo-tenant` / `demo-user` / `Manager` / `HasPermission => true`, emitting a one-time "DEMO AUTH SEAM ACTIVE … Replace with a real ITenantContext implementation before production deployment" warning. Its xmldoc explicitly says it is "replaced by a claims-backed `ITenantContext` … before production."

3. **The orphaned `MockOktaService`** (a standalone `Microsoft.NET.Sdk.Web` mock OIDC IdP — discovery/authorize/token/userinfo/introspect/jwks/logout, PKCE, no password validation) proves the original intended path was a third-party IdP (Okta-shaped). That integration was never completed — Bridge never wires `AddJwtBearer` against it. It is aspirational scaffolding, not a working seam.

4. **The MVP gap is precise:** a user who signs up and verifies their email lands in a "verified but cannot log in" dead-end — there is no `/auth/login` route and no session-creation anywhere. ADR 0097 verifies the credential; nothing establishes the session. **These are two halves of one launch gate; neither alone ships login.**

5. **Three MVP-gating flows all need the same primitive** (research §4): password login (post-ADR-0097), verify-email completion ("now logged in"), and WS-E magic-link consume (passwordless link → session). Each is an "establish session" call. Without one session-establishment seam, each would grow its own divergent session-creation code — a security-surface multiplication this ADR exists to prevent.

This is a Halt-9-class cryptographic/auth substrate, co-equal with ADR 0097, and is dual-council MANDATORY at ADR text + every implementation PR.

---

## Decision drivers

- **The two MVP-gating passwordless flows fit first-party naturally and fit OIDC badly.** Verify-email-completion and magic-link-consume are link-click → single-use-token-validate → session events. First-party session calls `SignInAsync` directly after token validation — no redirect. An OIDC redirect dance (`/authorize` → IdP → callback → token exchange) is architecturally awkward for a self-hosted magic link (it is not a built-in Keycloak realm feature; it requires an extension — research §2.2, sources 15/16).
- **Bridge is already a same-origin BFF.** Bridge serves the React SPA same-origin in production. The canonical 2026 best practice for first-party SPAs is exactly BFF + HttpOnly/Secure/SameSite cookies, NOT browser-held bearer tokens in `localStorage` (which has no XSS protection). Cookies get the `HttpOnly`/`SameSite` protections that `localStorage` cannot (research sources 12/13).
- **Zero recurring cost, zero vendor lock-in, self-hostable.** A first-party session aligns with the product's MIT-open-source, local-first, self-hostable identity. A SaaS IdP carries per-MAU pricing and migrates auth out of the codebase — a self-hosting customer would need their own IdP subscription.
- **First-party session machinery is unavoidable even if a vendor IdP is later added.** A BYO-OIDC `category-provider` (Tier-2) would still need (a) a session to hold the validated principal across requests, and (b) the passwordless flows that do not go through OIDC. So "buy" does not eliminate "build" — it adds a vendor *scheme* feeding this same session. This makes first-party the correct foundation regardless of the eventual BUY decision, and SIMPLIFIES the future OIDC-impl ADR (its `ClaimsBackedTenantContext` becomes "one more scheme feeding the same session," not a parallel universe — §1.4 of the research).
- **The session surface is well-trodden ASP.NET Core territory.** Owning the security surface (fixation/CSRF/rotation/revocation/isolation) is exactly the sec-eng dual-council's job; the mechanisms are standard `Microsoft.AspNetCore.Authentication.Cookies` + the existing antiforgery wiring.
- **Substrate-tier cadence precedent.** ADR 0091 R2 (single-concrete-class facade) + ADR 0095 (bootstrap branch + production-guard) + ADR 0096 (mock-first + production-guard) + ADR 0097 (Tier-1 primitive + dual-council) establish the discipline this ADR follows.

---

## Decision

**BUILD a first-party session-establishment substrate. v1 = cookie-based, same-origin BFF, server-side (stateful) session store.** A deferred, opt-in Tier-2 BYO-OIDC `category-provider` (Option B, folded later via the OIDC-impl ADR) is explicitly out of v1 scope but is the named extension point.

### D1 — Session primitive: STATEFUL (server-side store), not stateless signed token

**v1 recommendation: an opaque session id in an HttpOnly cookie, backed by a server-side `ISessionStore`** (in-memory v1 → SQLite/Postgres prod). **Justification for the multi-tenant ERP** (this is the load-bearing recommendation; see Adversarial Brief AB-1):

- **Server-side revocation is non-negotiable for a financial app.** A stateless signed token (encrypted-claims cookie or self-issued JWT) cannot be revoked before its expiry without a server-side denylist — which re-introduces server state, defeating the only reason to go stateless. Logout, admin force-logout, and stolen-cookie invalidation (S7) all require the session to die server-side, immediately. A stateful store gives this for free.
- **Tenant-binding cross-check (S8 / ADR 0091 R2 §2.2) is cleaner against a stored record.** The session record holds the bound `TenantId`; the per-request subdomain-resolved tenant (`IBrowserTenantContext`) is compared against it at the pipeline boundary. With a stateless token, the bound tenant rides in the cookie and a tampered/replayed cookie's tenant claim is only as trustworthy as the signature — workable, but the stored-record path is the simpler-to-reason-about isolation boundary.
- **Idle-timeout + sliding expiry (S6) are trivial against a store** (touch `LastSeenUtc` on each authenticated request) and awkward against an immutable signed token (you must re-issue on every slide, multiplying crypto work).
- **The cookie carries no session material** — only the opaque id. No claims, no tenant, no roles in the cookie. This minimizes the blast radius of cookie theft to "one revocable session," not "a self-contained bearer of authority."

The cost — Bridge owns a session store — is acceptable and mirrors the existing JournalStore in-memory→backed progression. The store is the natural home for revocation, idle-timeout, and the multi-tenant binding. **A2 (first-party JWT + refresh rotation) is explicitly deferred** to a future Tauri/mobile-client lane where a native shell cannot hold a same-origin cookie cleanly (§9 out-of-scope-but-flagged); it is NOT the v1 mechanism.

Mechanism: ASP.NET Core `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)` + `app.UseAuthentication()` inserted at the precise ordinal H6 pins (after `TenantSubdomainResolutionMiddleware`, before `UseAntiforgery` + `UseAuthorization`).

**Cookie carries ONLY the opaque id — resolving the Rev-1 internal contradiction (.NET-arch A6).** Rev 1 said both "the cookie carries no session material — only the opaque id" AND "the cookie's `ClaimsPrincipal` carries `sub` + `tid`." These contradict: default `AddCookie` + `SignInAsync(principal)` serializes the principal's claims INTO the (encrypted) cookie. The opaque-id-only posture (the §D1 thesis AB-1 leans on) is achieved via **`OnValidatePrincipal` rehydration**, and Rev 2 pins it:

> `ISessionEstablisher.EstablishAsync` calls `HttpContext.SignInAsync` with a **minimal principal carrying ONLY the opaque session id** (a single claim, e.g. `sid`) — NOT `sub`/`tid`/roles. `OnValidatePrincipal` looks up the `SessionRecord` by `sid`; if valid, it REPLACES `context.Principal` (via `context.ReplacePrincipal(...)` + `context.ShouldRenew`) with a freshly-built principal carrying `sub` (.NET-arch C5: the literal claim name `"sub"`, matching `IdentityEndpoints:160-161`), `tid` (the bound `TenantId`), and roles — all read from the SERVER-SIDE record, never the cookie. The cookie is thus a pure bearer-of-opaque-id; `HttpContext.User` is populated from the authoritative store. The S8 isolation argument is not weakened by a `tid` riding the cookie, because no `tid` rides the cookie. (Data Protection encrypts/MACs the cookie either way; the opaque-id-only posture minimizes blast radius per AB-1.)

### D2 — `ISessionEstablisher`: the single session-create seam (H7) — signature PINNED (.NET-arch O-6 RESOLVED)

All three entry points route through ONE abstraction. No divergent session creation. The .NET-architect council resolved O-6 and pinned the exact signature (Rev 2 folds it verbatim — three sharpenings vs the Rev-1 sketch: request-record bundling, strong-typed `TenantId`, `ValueTask` return):

```csharp
namespace Sunfish.Foundation.Session;

/// <summary>The SINGLE session-create seam (ADR 0099 H7). All three auth entry
/// points — password login, verify-email completion, magic-link consume —
/// route through EstablishAsync. No other code path calls SignInAsync.</summary>
public interface ISessionEstablisher
{
    /// <summary>Regenerates the session id (S1 fixation), writes a SessionRecord
    /// to ISessionStore, issues the HttpOnly+Secure+SameSite cookie via
    /// SignInAsync (minimal opaque-id-only principal; D1/A6), and emits the
    /// reason-specific Auth.SessionEstablished.* audit.</summary>
    /// <returns>The established session's record (caller uses SessionId for audit
    /// correlation; never returns the raw cookie — SignInAsync owns the cookie write).</returns>
    ValueTask<SessionEstablishmentResult> EstablishAsync(
        SessionEstablishmentRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

public sealed record SessionEstablishmentRequest(
    string UserId,                       // -> the "sub" claim
    TenantId TenantId,                   // strong-typed; MUST NOT be TenantId.System (ADR 0084 / IsSystemSentinel)
    SessionEstablishmentReason Reason);  // PasswordLogin | VerifyEmailCompletion | MagicLinkConsume

public sealed record SessionEstablishmentResult(
    string SessionId,                    // opaque CSPRNG id (>=128-bit, base64url; S4) for audit correlation
    DateTimeOffset IssuedUtc,
    DateTimeOffset AbsoluteExpiryUtc);
```

Three sharpenings the council fixed vs the Rev-1 positional sketch:
1. **Params bundled into a `SessionEstablishmentRequest` record** — three call sites + future MFA-step extensibility (O-S5) argue for a request object so adding a field (e.g. `MfaCompleted`) does not break call-site signatures.
2. **`TenantId TenantId`, not `string`** — strong-typed per ADR 0091's `TenantId.FromString` centralization; the sentinel-reject (ADR 0084 / `IsSystemSentinel`) happens at the typed boundary, not via string compare inside the establisher.
3. **`HttpContext httpContext` stays a SEPARATE parameter** (not inside the request record) — the request record stays serializable/testable independent of the web pipeline, while `SignInAsync` genuinely needs the live `HttpContext`. This keeps `foundation-session` referencing only `Microsoft.AspNetCore.Http.Abstractions` (`HttpContext`) + `Microsoft.AspNetCore.Authentication.Abstractions` (`SignInAsync` extension), NOT `foundation-authorization` — see O-7 / A8 for the acyclic package layering.

`EstablishAsync` behavior: regenerate the session id (S1 fixation; C1 mechanism) → write a `SessionRecord` to `ISessionStore` → `SignInAsync` issues the cookie (minimal opaque-id principal; A6) → emit the reason-specific `Auth.SessionEstablished.*` audit. `SessionEstablishmentReason` ∈ { `PasswordLogin`, `VerifyEmailCompletion`, `MagicLinkConsume` }. Login (Step 3), verify-email-completion (Step 4), and magic-link-consume (Step 4 / WS-E) all call `EstablishAsync`; none creates a session by any other path. This is the H7 single-seam invariant the .NET-architect council attests GREEN. (Including the ADR-0095 bootstrap-branch verify-email-completion call — it routes through the SAME `ISessionEstablisher`, so it is single-seam-compliant despite the pipeline-branch split; see H6/A1.)

### D3 — `SessionBackedTenantContext` is the production-scope facade impl (single-concrete-class-PER-SCOPE)

A single concrete production class implements the `Sunfish.Foundation.Authorization` **sum-interface facade** (`ITenantContext` + `ICurrentUser` + `IAuthorizationContext`), reading the authenticated principal `OnValidatePrincipal` rehydrated from the `SessionRecord` (D1/A6). Per `[[itenantcontext-consumption-qualification]]`, consumption sites pick the **Authorization sum-interface facade**, NOT the `Foundation.MultiTenancy` narrowed variant, until ADR 0091 Step 3 narrows.

**"Single concrete class" is PER SCOPE, not globally (sec-eng G).** ADR 0091 R2 §2.3's invariant is single-concrete-class-**per-resolution-scope** (one instance provides all facade interfaces coherently WITHIN a scope), NOT one impl globally. On main there are ALREADY two facade impls (`DemoTenantContext` dev-gated; `SeededTenantContext` bootstrap→post-tenant child scope per ADR 0095 α-1) — Step 2 adds a THIRD (`SessionBackedTenantContext`, production request scope). The three are **scope-disjoint**:

| Concrete facade impl | Resolution scope | Gating |
|---|---|---|
| `SessionBackedTenantContext` | Production request scope | registered when `IsProduction()` (PR 2) |
| `SeededTenantContext` | bootstrap→post-tenant child scope | ADR 0095 α-1 / ADR 0091 R2 A1 |
| `DemoTenantContext` | dev request scope | ALREADY `IsDevelopment()`-gated (shipyard#44; .NET-arch A3) |

**Registration via the SANCTIONED helper (.NET-arch A7).** The production composition root registers `SessionBackedTenantContext` through the EXISTING `AddSunfishTenantContext<SessionBackedTenantContext>()` helper (ADR 0091 R2 §A1), which (a) aliases all facade interfaces to the same scoped instance, and (b) installs the `TenantContextScopeAssertion : IHostedService` same-instance invariant. It is NOT hand-registered (e.g. `AddScoped<ITenantContext, SessionBackedTenantContext>()`) — hand-registering would BYPASS the ADR-0091 same-instance assertion and re-open the divergent-binding hole ADR 0091 A1 exists to prevent. `AddSunfishSessionEstablishment` registers ONLY the session-specific services (`ISessionEstablisher`, `ISessionStore`/`InMemorySessionStore`, options, audit constants, the production-guard `IHostedService`); the two helpers compose.

**No `ClaimsBackedTenantContext` as a separate class (.NET-arch O-6).** Rev 1's "unification point with `ClaimsBackedTenantContext`" framing is superseded: there is ONLY `SessionBackedTenantContext`, which reads `HttpContext.User` (the principal `OnValidatePrincipal` rehydrated). A future OIDC scheme produces THE SAME `HttpContext.User` (from a validated OIDC token, via its own `OnTokenValidated` → `EstablishAsync`), which `SessionBackedTenantContext` reads unchanged. The future OIDC lane does NOT add a second `ITenantContext` impl; it adds an authentication SCHEME feeding the same `ISessionEstablisher`/`SessionRecord`. `ClaimsBackedTenantContext` collapses INTO `SessionBackedTenantContext`.

### D4 — Mock-first / 3-tier slotting disposition: session is a **tier-1 domain-block**

Session establishment is **tier-1 (`domain-block`)** — concrete machinery never swapped for a vendor at runtime (per the three-tier slotting vocabulary: tier-1 = concrete DI never swapped; tier-2 = bounded vendor swap; tier-3 = runtime plugin). It is NOT a Tier-2 `category-provider`. There is no "session vendor"; the *authentication scheme that precedes session-establish* is the Tier-2 swap point (password vs magic-link vs future BYO-OIDC), not the session primitive itself.

**Mock variant for tests:** the `ISessionStore` has an `InMemorySessionStore` that is the v1 *production-and-test* implementation (in-memory v1; SQLite/Postgres in Step 5) — this is a tier-1 in-memory→backed progression (like JournalStore), NOT a "mock-first vendor" in the ADR 0096 sense. There is no `IMockSessionEstablisher` production-guard family — the production guard that matters here is the `DemoTenantContext`-must-not-register-in-Production assertion (§6), which mirrors ADR 0095's bootstrap mutual-exclusion assertion and ADR 0096's `MockProviderProductionGuardAssertion` in spirit. Tests construct a real `ISessionEstablisher` against an `InMemorySessionStore`; no mock-marker conflation with ADR 0096/0097 mock families.

### D5 — `MockOktaService` disposition: keep, dev-only, do NOT wire into v1

Retain `MockOktaService` under dev-only, marked as future-BYO-OIDC test scaffold. Do NOT wire it into the v1 session substrate (no `AddJwtBearer` against it). It becomes useful again only when the Tier-2 BYO-OIDC `category-provider` is built (future OIDC-impl ADR), as the dev backing IdP for that scheme's integration tests. Retiring it now would discard correct OIDC-shaped scaffolding the future lane will want; wiring it now would add an unused authentication scheme to the v1 surface. Park it.

---

## Mechanism (substrate shape)

```csharp
// signal-bridge/Sunfish.Bridge/Program.cs (Step 2)

// (1) Facade registration via the SANCTIONED ADR-0091 helper (.NET-arch A7) — Production only.
//     SessionBackedTenantContext is the production-scope facade impl (D3); DemoTenantContext stays
//     dev-gated (already so, shipyard#44 / A3); the production-guard (below) asserts the polarity.
if (builder.Environment.IsProduction())
    builder.Services.AddSunfishTenantContext<SessionBackedTenantContext>();   // aliases facade + same-instance assertion

// (2) Session-specific services only (NOT the facade — A7).
builder.Services.AddSunfishSessionEstablishment(opts => { ... });   // ISessionEstablisher + ISessionStore
     // (InMemorySessionStore v1) + options + Auth.* audit constants + the presence/absence production-guard IHostedService

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
     .AddCookie(opts => {
         opts.Cookie.HttpOnly = true;                            // S3
         opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;   // S3
         opts.Cookie.SameSite = SameSiteMode.Strict;             // S3 (first-party same-origin; Strict v1 — O-4 confirmed)
         opts.SlidingExpiration = true;                          // S6 idle (cookie hint only; server-side record is authority — B2)
         opts.ExpireTimeSpan = TimeSpan.FromMinutes(30);         // S6 idle DEFAULT 30min (client hint; the store gate is authority)
         opts.Events.OnValidatePrincipal = <store check + tenant cross-check + TTL enforce + rehydrate>;  // S6/S7/S8 + A6
     });

// ... pipeline ordering — H6 / sec-eng F / .NET-arch A1 — the FULL 4-constraint ordinal:
app.MapBootstrapEndpoints();                                     // :185 ADR-0095 UseWhen branch (bypasses subdomain mw)
app.UseMiddleware<TenantSubdomainResolutionMiddleware>();        // :191 binds IBrowserTenantContext
app.UseWebSockets();                                             // :195
app.UseAuthentication();   // INSERTED HERE — after TenantSubdomain (S8 needs IBrowserTenantContext), before UseAntiforgery + UseAuthorization
app.UseAntiforgery();      // :197 (now runs against the authenticated principal)
app.UseAuthorization();    // :198
```

- **`ISessionEstablisher`** — the single session-create seam (D2; signature pinned). The only thing that calls `SignInAsync`.
- **`ISessionStore` / `InMemorySessionStore` / `SessionRecord`** — server-side opaque-id store; atomic create/read/touch/remove. **`SessionRecord` is a `sealed record` (init-only; .NET-arch C2)** = { `SessionId` (CSPRNG ≥128-bit, base64url, S4), `UserId`, `TenantId` (strong-typed), `IssuedUtc`, `AbsoluteExpiryUtc`, `LastSeenUtc` (the ONLY mutable-by-touch field — touch produces a new record via `with`), `Reason` }. **`ISessionStore` methods return `ValueTask<...>` (.NET-arch C3)** (async-ready for the PR-5 backed store; in-memory completes synchronously but does not break the interface). In-memory v1 = `ConcurrentDictionary<string, SessionRecord>`; touch is a `TryUpdate`/replace (records immutable); the S9 single-use-token compare-and-delete is `ConcurrentDictionary` atomic in-memory and MUST be DB-atomic in the PR-5 backed impl.
- **`OnValidatePrincipal`** (async; .NET-arch C4) — on every request carrying a session cookie: (a) look up the `SessionRecord` by the cookie's opaque `sid`; reject (`RejectPrincipal` + sign-out, 401) if absent/revoked (S7); (b) **enforce TTL server-side (sec-eng B2)**: reject if `now > AbsoluteExpiryUtc` OR `now - LastSeenUtc > idleTimeout` — the SERVER-SIDE record is the authority; the cookie `ExpireTimeSpan` is a redundant client hint, not the gate; (c) **cross-check tenant, fail-closed (sec-eng D)**: reject (401 + `Auth.SessionTenantMismatch`) when the record's `TenantId` ≠ the subdomain-resolved `IBrowserTenantContext.Tenant`, OR the subdomain tenant is **unresolved/absent** (fail-closed, not pass-through), OR the record's `TenantId.IsSystemSentinel` is true (reuse the EXISTING guard — rejects `default`/`__system__`/`__`-prefixed; not only ADR 0084); (d) touch `LastSeenUtc` for sliding idle (S6); (e) **rehydrate (A6)**: `ReplacePrincipal` with a freshly-built principal carrying `"sub"`/`tid`/roles read from the record. `OnValidatePrincipal` is a **no-op for requests carrying no session cookie** (anonymous → `UseAuthorization`'s `RequireAuthenticatedUser()` 401) and does NOT run for the AllowAnonymous bootstrap routes (they are in the `MapBootstrapEndpoints` bypass branch; cookie middleware is not in that branch — .NET-arch C4).
- **`SessionBackedTenantContext`** — reads the validated principal/record; implements the facade (D3).

---

## Security surface (sec-eng dual-council territory — H1-H5, S1-S11)

Folded from research §3. This is a Halt-9-class auth substrate; sec-eng MANDATORY at ADR text + every implementation PR.

| # | Surface | Ratified requirement | Halt |
|---|---|---|---|
| S1 | **Session fixation** | `ISessionEstablisher.EstablishAsync` regenerates the session id on EVERY privilege change. **Mechanism (sec-eng C1):** mint a FRESH CSPRNG id (S4), write a NEW `SessionRecord`, issue the cookie for the new id, and invalidate any inbound session id/record + sign out the old ticket. The new id is NEVER derived from, and never reuses, any inbound (pre-auth/anonymous) id. **No pre-auth record (sec-eng C2):** the substrate MUST NOT establish any server-side `SessionRecord` for an unauthenticated/bootstrap principal that `EstablishAsync` could later "upgrade" — there is no pre-auth session to fixate (aligns with ADR 0095: bootstrap context is a request-scoped seed, not a session). Tested: a request carrying a pre-auth cookie id, after `EstablishAsync`, holds a DIFFERENT id and the pre-auth record is gone. | H1 |
| S2 | **CSRF — positive, testable enforcement invariant (sec-eng A; the won't-waive amendment)** | The CSRF model is **default-secure global**, NOT per-handler author-memory. The session substrate registers cookie-auth mutating routes under an endpoint group with **antiforgery required-by-default** (`.RequireAntiforgery()` / `app.UseAntiforgery()` auto-validation for cookie-auth endpoints). Any route opting OUT (`.DisableAntiforgery()`) MUST carry an explicit, reviewed justification. The invariant flips from "remember to add CSRF" to **"CSRF is on unless you explicitly and visibly turn it off"** — startup/arch-test-PROVABLE, not an obligation. (Ground-truth: Rev 1's "MUST cover all routes" was an obligation on author memory; the Bridge today enforces antiforgery per-handler-manually via explicit `ValidateRequestAsync` on only THREE endpoint families — `FinancialEndpoints`/`JournalEntriesEndpoints`/`WorkOrdersEndpoint` — so a new `/auth/login`/`/auth/logout` or any future cookie-auth mutating route that forgets the call ships CSRF-exposed and passes CI. The default-secure group closes this.) **Login-CSRF specifics:** `/auth/login` POST is antiforgery-validated; the token is obtainable PRE-auth via the existing `GET …/antiforgery-token` pattern (the pre-login user has no session); and the antiforgery token is RE-ISSUED (`GetAndStoreTokens`) AFTER `SignInAsync` so the SPA's post-login mutating requests carry a token valid for the NEW principal, not a stale pre-auth token. **Arch-test floor:** a startup assertion / architecture test proves every `/api/v1/cockpit/auth/**` + cookie-auth mutating endpoint is antiforgery-required-by-default or carries a documented, reviewed opt-out marker. | H2 |
| S3 | **Cookie flags** | `HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Strict` (first-party same-origin; O-4 confirms `Strict` for v1, never `None`/`Lax`-as-default). No session material readable by JS — only the opaque id rides the cookie (A6: claims rehydrated server-side, never serialized into the cookie). Magic-link/verify establish the cookie ON the link-landing request (the single-use token, not a pre-existing cookie, authenticates that request), so `Strict` does not break them. | H2 |
| S4 | **Token entropy + comparison discipline (sec-eng E)** | Session ids + single-use tokens from a CSPRNG (`RandomNumberGenerator.Fill(Span<byte>)`), **≥16 random bytes (≥128 bits), base64url-encoded** (URL-safe; NOT hex-doubling-the-length-for-no-entropy-gain; no delimiter-collision format). (The orphaned mock used 24-32 random bytes — match or exceed.) **Any in-memory comparison of a candidate single-use token against a stored value (S9 consume path) MUST use `CryptographicOperations.FixedTimeEquals`** (ADR 0097 discipline) — no `==` token compare. Opaque-id STORE LOOKUPS are by-key (dictionary/DB-index, no byte compare) and are exempt from the constant-time requirement. | H3 (shared store) |
| S5 | **Rotation** (deferred A2 JWT only) | Out of v1 scope (stateful store + idle/absolute TTL replaces refresh rotation for cookie sessions). Documented for the future Tauri/mobile JWT lane: rolling refresh + token-family theft detection. | — (§9) |
| S6 | **TTL / idle timeout — defaults RATIFIED + server-side-enforced (sec-eng B)** | **Defaults (sec-eng ratified, O-2 RESOLVED): absolute lifetime 8h, idle (sliding) timeout 30min, both operator-configurable.** 8h matches a single financial workday; 30min matches OWASP guidance for sensitive/financial apps (15-30min; 30min is the ceiling, NOT 60). **Reject 60min idle as the default** — acceptable only as an operator override. **Substrate floor (non-negotiable):** idle MUST be ≤ absolute; both enforced SERVER-SIDE at `OnValidatePrincipal` against the `SessionRecord` (reject if `now > AbsoluteExpiryUtc` OR `now - LastSeenUtc > idleTimeout`). `SlidingExpiration = true` on the cookie alone is INSUFFICIENT — it slides the cookie ticket, not the server-side record; the record is the authority (D1). The cookie `ExpireTimeSpan` is a redundant client hint, not the gate. | H5 |
| S7 | **Logout / revocation** | Server-side session invalidation (remove `SessionRecord`), NOT just cookie-clear — a stolen cookie dies on logout. Admin force-logout + "log out all sessions" enabled by the store. | H5 |
| S8 | **Multi-tenant session isolation — fail-closed (sec-eng D)** | `OnValidatePrincipal` rejects (401 + `Auth.SessionTenantMismatch`) when **(a)** the record's `TenantId` ≠ the subdomain-resolved tenant (`IBrowserTenantContext` / `TenantSubdomainResolutionMiddleware`; ADR 0091 R2 §2.2), **(b)** the subdomain tenant is **unresolved/absent** (FAIL-CLOSED, not pass-through — an attacker who suppresses/malforms the subdomain must NOT get a tenant-unconstrained session), OR **(c)** the record's `TenantId.IsSystemSentinel` is true (reuse the EXISTING guard at `TenantQueryFilterExtensions.cs:159` — rejects `default(TenantId)`/`__system__`/`__`-prefixed; `default` rejection matters: a `SessionRecord` whose `TenantId` was never set must NOT validate). No cross-tenant session reuse; no tenant-unconstrained session. (Depends on H6/A1 ordinal: `UseAuthentication` AFTER `TenantSubdomainResolutionMiddleware`, else `IBrowserTenantContext` is unbound at `OnValidatePrincipal` and the check fails-OPEN.) | H4 |
| S9 | **Single-use-token atomicity (sec-eng A/E1 + O-1 POST-gate)** | The verify-email + magic-link consume path MUST be atomic compare-and-delete (no check-then-use TOCTOU). Mirrors the W#79 `TryConsumeEmailVerificationAsync` gate (read row → assert flag-false → atomically transition → return bool; already-consumed falls through to byte-identical idempotent handling). The session-establish call happens ONLY on the first-consume branch. Any in-memory candidate-vs-stored token compare uses `FixedTimeEquals` (S4/E1). **O-1 auto-login-on-verify (Admiral-ruled, see §"Open questions"): the verify-completion MUST be a POST** (the landing GET shows a "click to continue" that POSTs) so an email-client/security-gateway GET prefetch cannot consume the single-use token and mint-then-discard a session. The atomic compare-and-delete + POST-gate are what make auto-login-on-verify safe against the prefetch/forward vector. | H3 |
| S10 | **Audit emission (.NET-arch C6: existing constant home)** | New `AuditEventType` constants — `Auth.SessionEstablished.PasswordLogin`, `Auth.SessionEstablished.VerifyEmail`, `Auth.SessionEstablished.MagicLink`, `Auth.LoginFailed`, `Auth.SignedOut`, `Auth.SessionRevoked`, `Auth.SessionTenantMismatch` (S8 trigger) — are added to the EXISTING `AuditEventType` constant surface (wherever `TenantMagicLinkIssued`/`Consumed` land per the WS-E hand-off, shipyard#173), NOT a parallel constant home. Login-failure events feed the rate-limit/lockout signal (S11). | H1-H5 (cross) |
| S11 | **Brute-force / rate-limit (O-3 Admiral-ruled)** | `/auth/login` needs rate-limiting + (optional) lockout-after-N-failures via ASP.NET Core rate-limiting middleware, per-credential SHA-256-prefix bucket key (W#79 §3.7 precedent) — **rate-limiting is the v1 floor**. CAPTCHA gate (Turnstile, shipyard#128 / ADR 0096) is **DEFERRED post-MVP as a separate Tier-2 add** (Admiral-ruled O-3); the login route CONSUMES `ICaptchaVerifier` when the gate is added, but this ADR's v1 does not author it. | H2 (login PR) |

---

## Integration points

- **(a) W#79 password login (post-ADR-0097):** new `Sunfish.Bridge.Features.Auth.AuthEndpoints` `/auth/login` → resolve user → `IPasswordHasher<UserEntity>.VerifyHashedPassword` (ADR 0097 Argon2id; returns `Success` / `SuccessRehashNeeded` / `Failed`) → on `Success`/`SuccessRehashNeeded`, `ISessionEstablisher.EstablishAsync(reason: PasswordLogin)`; on `SuccessRehashNeeded` ALSO trigger ADR 0097's lazy rehash-on-next-login. On `Failed`, emit `Auth.LoginFailed` + feed the rate-limiter (S11). ADR 0097 verifies the credential; THIS ADR establishes the session. (Route is `/api/v1/cockpit/auth/**`-family → always full-pipeline dual-council, never pattern-009.)
- **(b) W#79 verify-email completion ("now logged in"):** after `VerifyEmail` succeeds (its `TryConsume` atomic gate, S9), call `ISessionEstablisher.EstablishAsync(reason: VerifyEmailCompletion)`. Closes the "verified but cannot log in" dead-end. **UX RULED (Admiral-ruled O-1, CIC-overridable): auto-login on verify**, POST-gated. sec-eng cleared it CONDITIONALLY-ACCEPTABLE: it inherits the full S9 + S4 + S1 discipline (CSPRNG ≥128-bit token; atomic compare-and-delete; S1 fixation regen on establish) AND the verify-completion is a **POST** (the landing GET shows "click to continue" that POSTs) so an email-prefetch/security-gateway GET cannot consume the single-use token. With those conditions, the verify link's session-grant power is equivalent to a magic-link (which the ADR already accepts as a first-class establish path) — no new class of risk. The bootstrap-branch nuance (A1): this `EstablishAsync` call runs INSIDE the ADR-0095 `MapBootstrapEndpoints` UseWhen branch (which bypasses `TenantSubdomainResolutionMiddleware`); the cookie is issued via `HttpContext.SignInAsync(scheme)` (scheme resolved from DI, independent of middleware position) and is first VALIDATED by `OnValidatePrincipal` on the user's NEXT request through the main pipeline — the bootstrap branch is an establish-only (write) path, not a validate (read) path, so it needs no `UseAuthentication` of its own.
- **(c) WS-E magic-link verify (H-WSE-2 resolved):** validate single-use token (S9 atomic, the `TryConsume` pattern shared with verify-email) → `ISessionEstablisher.EstablishAsync(reason: MagicLinkConsume)`. The session substrate owns session-create; WS-E (shipyard#173) owns token-issue + email-deliver. Audit `Auth.SessionEstablished.MagicLink`. This ADR is the substrate the WS-E magic-link PR-2 depends on; with it Accepted, the WS-E "validate-and-redirect-to-login (degraded)" fallback is unnecessary.
- **(d) Existing `AuthenticatedTenantPolicy` / `ITenantContext` consumption:** the established session populates `HttpContext.User` with the `sub` claim `IdentityEndpoints.TryResolve` (`:160`) and `RequireAuthenticatedUser()` already expect — closing the "authorization gated on a never-populated principal" structural bug. The session's bound tenant satisfies ADR 0091 R2 §2.2 (S8). `SessionBackedTenantContext` (facade; ADR 0091 R2 §2.3) replaces `DemoTenantContext`.

---

## Demo-seam + MockOktaService disposition

- **`DemoTenantContext` is ALREADY `IsDevelopment()`-gated (ADR 0091 Step 1, shipyard#44 — NOT gated by this ADR; .NET-arch A3).** The dev/demo behavior is unchanged. **The actual PR-2 delta is narrower:** (i) add the Production-branch `SessionBackedTenantContext` registration via `AddSunfishTenantContext<SessionBackedTenantContext>()` — the production path currently registers NO `ITenantContext` at all, so `SunfishBridgeDbContext` (which takes `ITenantContext` as a ctor dependency) is presently un-resolvable in Production and Bridge is un-deployable there; PR 2 is what makes Bridge Production-deployable w.r.t. `ITenantContext` — and (ii) add the presence/absence production-guard. **PR 2 must NOT re-gate the already-gated `DemoTenantContext`** (churn + merge-conflict risk against the live ADR-0091 wiring).
- **Production-guard POLARITY — fail-closed PRESENCE + ABSENCE (.NET-arch A2; the Rev-1 polarity was inverted).** A startup `IHostedService` asserts, when `IHostEnvironment.IsProduction()`:
  - **(a) PRESENCE** — the registration tree contains a `ServiceDescriptor` for the `Foundation.Authorization.ITenantContext` facade whose `ImplementationType` is `SessionBackedTenantContext` (fail-closed `throw` if ABSENT — the DbContext dependency is otherwise unsatisfiable; an absence-only guard is a false-negative trap that green-lights the current broken Production state).
  - **(b) ABSENCE** — no `ServiceDescriptor` resolves to `DemoTenantContext` (or any `IMock*`/demo seam) (fail-closed `throw` if PRESENT).
  - **Reuse, don't duplicate (.NET-arch A7):** mirror / extend ADR 0091 R2 §A1's `TenantContextScopeAssertion` captured-`IServiceCollection`-snapshot scan (`ServiceDescriptor.ImplementationType`, NOT runtime resolution) rather than authoring a parallel one. This is the same GREEN-attested registration-snapshot pattern as ADR 0095's `BootstrapAndTenantMutualExclusionAssertion` + ADR 0096's `MockProviderProductionGuardAssertion`. This is the production-guard for the session tier (D4).
- **`MockOktaService` → keep, dev-only, do NOT wire v1** (D5). Mark as future-BYO-OIDC test scaffold.

---

## Revision 2 fold — dual-council AMBER → amendment→resolution map

Rev 1 returned dual-AMBER (no RED): security-engineering (`council-verdict-2026-05-29T0305Z`, 7 amendments A-G) + .NET-architect (`council-verdict-2026-05-29T0315Z`, 8 amendments A1-A8 + 6 clarifications C1-C6). The substrate spine (D1 stateful store, H7 single-seam, S1/S3/S4/S7/S8/S9 shape) was attested correct in both — these are floor-lifts, not redesigns, the same Rev-1-AMBER → Rev-2-GREEN fold pattern as ADR 0095/0096/0097/0098. All 15 amendments + 6 clarifications are folded below; the two Admiral-ruled product defaults (O-1, O-3) are applied and flagged CIC-overridable.

### security-engineering (7 amendments — all folded)

| # | Amendment | Resolution in Rev 2 |
|---|---|---|
| **A** | **CSRF positive, testable enforcement invariant (the won't-waive one).** Rev-1 stated "MUST cover ALL routes" as an author-memory obligation; the Bridge enforces antiforgery per-handler-manually on only 3 endpoint families. | **S2** rewritten as a default-secure global invariant: cookie-auth mutating routes under an antiforgery-required-by-default endpoint group (`.RequireAntiforgery()` / `UseAntiforgery` auto-validation), explicit reviewed `.DisableAntiforgery()` opt-out, startup/arch-test-PROVABLE. Login-CSRF: `/auth/login` POST validated, pre-auth token via `GET …/antiforgery-token`, token re-issued post-`SignInAsync` (A3 stale-token interaction). H2 updated. PR-2 row adds the default-secure endpoint group. |
| **B** | **TTL defaults as a substrate minimum, server-side-enforced.** | **S6**: absolute 8h / idle 30min ratified (60min rejected as default); idle ≤ absolute; both enforced server-side at `OnValidatePrincipal` against the record; cookie `ExpireTimeSpan` is a client hint, not the gate. Mechanism block `ExpireTimeSpan = 30min` + `OnValidatePrincipal` (b). H5 updated. |
| **C** | **S1 fixation: regen MECHANISM + no pre-auth record.** | **S1**: mint fresh CSPRNG id, new record, invalidate inbound id/record, never derive from pre-auth (C1); NO server-side `SessionRecord` for an unauthenticated principal (C2). H1 + AB-3 reflect the mechanism. |
| **D** | **H4/S8: reuse `IsSystemSentinel`; fail-closed on unresolved subdomain.** | **S8** + `OnValidatePrincipal` (c): reject on mismatch, on unresolved/absent subdomain (fail-closed), OR `IsSystemSentinel` (existing guard, `default`/`__system__`/`__`-prefixed; not only ADR 0084). H4 updated. New A0 row for `IsSystemSentinel`. |
| **E** | **S4: encoding floor + `FixedTimeEquals` on the token compare.** | **S4**: ≥16 bytes, base64url; `FixedTimeEquals` MANDATORY on any candidate-vs-stored single-use-token compare (S9 path); by-key store lookups exempt. H3 updated. New A0 row for `FixedTimeEquals`. |
| **F** | **H6 middleware ordering relative to `UseAntiforgery`.** | Merged with .NET-arch A1 into the full 4-constraint ordinal — see A1 below. |
| **G** | **D3 "single concrete class" → PER SCOPE (three impls coexist).** | **D3** rewritten: single-concrete-class-per-resolution-scope; table of three scope-disjoint impls (`SessionBackedTenantContext` prod / `SeededTenantContext` bootstrap / `DemoTenantContext` dev). H6 updated. New A0 row for `SeededTenantContext`. |

sec-eng open-question rulings folded: **O-1** auto-login CONDITIONALLY-ACCEPTABLE (→ POST-gated, S9 + Integration (b)); **O-4** `Strict` confirmed (→ S3); **O-5** in-memory single-instance-only (→ §Consequences).

### .NET-architect (8 amendments + 6 clarifications — all folded)

| # | Amendment | Resolution in Rev 2 |
|---|---|---|
| **A1** | **Middleware ordering: full 4-constraint ordinal (load-bearing).** `UseAuthentication` AFTER `TenantSubdomainResolutionMiddleware` (S8 reads `IBrowserTenantContext`) + BEFORE `UseAntiforgery` + BEFORE `UseAuthorization` + bootstrap-branch reconciliation. | **H6** + Mechanism block pin the resulting order `MapBootstrapEndpoints → TenantSubdomain → UseWebSockets → UseAuthentication → UseAntiforgery → UseAuthorization`. Bootstrap-branch establish-only path reconciled in §D2 + Integration (b) + H7. Merges sec-eng F. |
| **A2** | **Production-guard polarity inverted: assert PRESENCE of `SessionBackedTenantContext`, not just absence of Demo.** | **Demo-seam disposition** + H6: two-pronged fail-closed PRESENCE (throw if `SessionBackedTenantContext` absent — DbContext dependency otherwise unsatisfiable) + ABSENCE (throw if `DemoTenantContext` present). New A0 row for the `SunfishBridgeDbContext` ctor dependency. |
| **A3** | **`DemoTenantContext` already dev-gated (shipyard#44); narrow the PR-2 delta.** | A0 `DemoTenantContext` row + Demo-seam disposition + D3 corrected: already gated; PR-2 delta is (i) add production `SessionBackedTenantContext` registration, (ii) add the guard; MUST NOT re-gate. |
| **A4** | **Cite LANDED `foundation-authorization` precedent, not unlanded `foundation-password-hashing`.** | A0 `ITenantContext`/`foundation-authorization` row = LANDED placement precedent; `foundation-password-hashing` row marked in-flight / sibling-only. O-7 cites `foundation-authorization` for NU1510 + geometry. |
| **A5** | **Pin O-6 `ISessionEstablisher` shape rather than forwarding open.** | §D2 folds the pinned signature verbatim; O-6 marked RESOLVED. |
| **A6** | **Opaque-id-cookie vs claims-in-cookie internal contradiction.** | §D1 Mechanism: `SignInAsync` writes a minimal `sid`-only principal; `OnValidatePrincipal` rehydrates `sub`/`tid`/roles from the server record via `ReplacePrincipal`. `OnValidatePrincipal` (e) + S3 reflect it. |
| **A7** | **Reuse `AddSunfishTenantContext<TConcrete>` helper; don't hand-roll.** | §D3 + Mechanism block (1): `AddSunfishTenantContext<SessionBackedTenantContext>()`; `AddSunfishSessionEstablishment` registers only session-specific services. New A0 row for the helper. |
| **A8** | **`foundation-session` ProjectRef direction (circular-ref trap).** | §O-7 resolution: refs → `foundation` + `Http.Abstractions` + `Authentication.Abstractions`, NOT `foundation-authorization`; acyclic; `SessionBackedTenantContext` + `AuthEndpoints` stay in Bridge. PR-2 row + §D2 sharpening 3 reflect it. |
| **C1** | TFM `net11.0` (inherits `Directory.Build.props`). | §O-7 resolution notes `net11.0`. |
| **C2** | `SessionRecord` = `sealed record`, init-only, `LastSeenUtc` only mutable. | Mechanism block `SessionRecord` bullet. |
| **C3** | `ISessionStore` returns `ValueTask`; in-memory = `ConcurrentDictionary` + `TryUpdate` touch; backed impl DB-atomic. | Mechanism block `ISessionStore` bullet. |
| **C4** | `OnValidatePrincipal` async + no-op for no-cookie / bootstrap-branch requests. | Mechanism block `OnValidatePrincipal` bullet (trailing clause). |
| **C5** | Pin the `"sub"` claim name (not `ClaimTypes.NameIdentifier`). | §D1 Mechanism rehydration bullet + A0 `IdentityEndpoints` row. |
| **C6** | Audit constants on the EXISTING `AuditEventType` surface (WS-E placement). | **S10** updated. |

.NET-architect open-question rulings folded: **O-6** signature pinned + `ClaimsBackedTenantContext` collapses into `SessionBackedTenantContext` (→ §D2/§D3); **O-7** new `packages/foundation-session/` Tier-1 package, acyclic layering (→ §O-7 resolution + PR-2 row).

### Admiral-ruled product defaults (CIC-overridable)

- **O-1** — auto-login-on-verify, **POST-gated** (sec-eng cleared CONDITIONALLY-ACCEPTABLE on the S9 + S4 + S1 + POST-gate conditions). Folded into S9 + Integration (b) + H8.
- **O-3** — **rate-limiting is the v1 floor; CAPTCHA (Turnstile) deferred post-MVP** as a separate Tier-2 add. Folded into S11 + H8 + O-S4.

---

## Adversarial Brief

Per ADR 0093 Rev 4. Worst-case interpretation of each load-bearing decision; the questions that surface interface/contract-completeness gaps at design time rather than at Stage-06 SPOT-CHECK.

- **AB-1 (D1 stateful-vs-stateless).** *Worst case if we picked stateless signed-token v1:* logout cannot revoke before expiry → a stolen cookie remains valid for the full TTL despite the user clicking "log out"; admin force-logout is impossible; a tenant claim in the cookie is trusted on signature alone, widening the S8 isolation surface. **Mitigation: D1 mandates the server-side store.** The cookie carries only an opaque id; authority reads the record. Stateless deferred to the future JWT lane (§9) where it is justified by a non-cookie client.
- **AB-2 (D2 single-seam).** *Worst case if login / verify / magic-link each created sessions independently:* three divergent `SignInAsync` callsites, three places to forget S1 fixation regen, three audit-event shapes, three tenant-binding implementations — the exact security-surface multiplication that produces "fixed in one path, vulnerable in another." **Mitigation: H7 — `ISessionEstablisher.EstablishAsync` is the ONLY session-create path; the three entry points are reasons, not implementations.**
- **AB-3 (S1 fixation).** *Worst case:* the pre-auth bootstrap/anonymous session id is carried into the authenticated session → an attacker who fixes a victim's pre-auth session id rides the post-auth elevation. **Mitigation: `EstablishAsync` mints a FRESH CSPRNG id, writes a new record, and invalidates any inbound id/record — never derives from or reuses a pre-auth id (sec-eng C1); and NO server-side `SessionRecord` exists for an unauthenticated principal (sec-eng C2) — there is no pre-auth session to fixate. Tested (H1).**
- **AB-4 (S8 tenant isolation).** *Worst case:* a session minted on `tenant-a.app` is replayed against `tenant-b.app` (same cookie, different subdomain) and the authorization layer trusts the cookie's tenant → cross-tenant data access; OR an attacker suppresses the subdomain to get a tenant-unconstrained session. **Mitigation: `OnValidatePrincipal` cross-checks the record's bound `TenantId` against the subdomain-resolved tenant on EVERY request, FAIL-CLOSED — rejecting on mismatch AND on unresolved/absent subdomain (sec-eng D); mismatch → 401 + `Auth.SessionTenantMismatch` (S8 / ADR 0091 R2 §2.2). Reject `TenantId.IsSystemSentinel` (existing guard; `default`/`__system__`/`__`-prefixed). Requires the H6/A1 ordinal (`UseAuthentication` AFTER the subdomain middleware) or the check fails-OPEN.**
- **AB-5 (S9 TOCTOU).** *Worst case:* verify-email / magic-link consume reads "token unused," then (race) a second concurrent request also reads "unused" before either marks it consumed → the single-use token establishes TWO sessions. **Mitigation: atomic compare-and-delete (the W#79 `TryConsume` gate); session-establish only on the first-consume branch; already-consumed → byte-identical idempotent handling (no leakage).**
- **AB-6 (S7 revocation + S2 CSRF interaction).** *Worst case:* logout clears the cookie client-side but the server-side record lingers → a captured cookie still authenticates; OR a mutating cookie-auth route ships without antiforgery → CSRF. **Mitigation: logout removes the `SessionRecord` server-side (S7); ALL mutating cookie-auth routes carry `X-XSRF-TOKEN` antiforgery (S2/H2).**
- **AB-7 (`HttpContext.User` population gap + wrong ordinal).** *Worst case if left implicit:* the ADR ships the session store but wires `UseAuthentication` at the wrong ordinal — e.g. BEFORE `TenantSubdomainResolutionMiddleware` (which satisfies a naive "before UseAuthorization" reading but leaves `IBrowserTenantContext` unbound at `OnValidatePrincipal`, silently no-op'ing the S8 cross-check → fail-OPEN cross-tenant hole) — OR the demo seam silently papers production, OR the production path registers no `ITenantContext` at all (current broken state) and an absence-only guard green-lights it. **Mitigation: H6 — the FULL 4-constraint `UseAuthentication` ordinal (after `TenantSubdomainResolutionMiddleware`, before `UseAntiforgery` + `UseAuthorization`; A1); the production-guard asserts both PRESENCE of `SessionBackedTenantContext` AND ABSENCE of `DemoTenantContext` in Production (A2), fail-closed.**

---

## Council halt-conditions (ADR-text gate)

Dual-council MANDATORY (sec-eng + .NET-architect) on this ADR text AND each implementation PR (H9). Sec-eng owns H1-H5; .NET-architect owns H6-H7; CIC ratifies H8.

- **H1 (sec-eng MANDATORY)** — session-fixation regeneration on privilege change (S1): the MECHANISM (fresh CSPRNG id, new record, invalidate inbound id/record, never derive from pre-auth; NO pre-auth `SessionRecord` exists — sec-eng C) is specified at the `ISessionEstablisher` seam and tested.
- **H2 (sec-eng MANDATORY)** — cookie flags HttpOnly+Secure+SameSite=Strict (S3); **CSRF default-secure global enforcement invariant** — antiforgery required-by-default on cookie-auth mutating routes with explicit reviewed opt-out, startup/arch-test-PROVABLE (S2/sec-eng A; NOT author-memory "MUST cover"); login-CSRF (pre-auth token + post-`SignInAsync` re-issue); `/auth/login` rate-limit (S11).
- **H3 (sec-eng MANDATORY)** — single-use-token atomicity (S9) is compare-and-delete, no TOCTOU; CSPRNG entropy ≥16 bytes / ≥128-bit, base64url (S4); `FixedTimeEquals` on any candidate-vs-stored token compare (sec-eng E1); shared with verify-email + magic-link.
- **H4 (sec-eng MANDATORY)** — multi-tenant session isolation cross-check (S8 / ADR 0091 R2 §2.2) at the `OnValidatePrincipal` boundary, **FAIL-CLOSED on mismatch AND on unresolved/absent subdomain tenant** (sec-eng D); `TenantId.IsSystemSentinel` rejected (reuse the EXISTING guard — `default`/`__system__`/`__`-prefixed; not only ADR 0084).
- **H5 (sec-eng MANDATORY)** — server-side revocation (S7) — logout invalidates the server-side record, stolen cookie dies; absolute (8h) + idle (30min) TTL (S6/sec-eng B) enforced SERVER-SIDE at `OnValidatePrincipal` (the cookie `ExpireTimeSpan` is a client hint, not the gate; idle ≤ absolute).
- **H6 (.NET-architect MANDATORY)** — **the FULL 4-constraint `UseAuthentication` ordinal** (sec-eng F + .NET-arch A1): inserted AFTER `TenantSubdomainResolutionMiddleware` (so `OnValidatePrincipal`'s S8 check reads the subdomain-bound `IBrowserTenantContext`) AND BEFORE `UseAntiforgery` AND `UseAuthorization`; resulting order `MapBootstrapEndpoints → TenantSubdomainResolutionMiddleware → UseWebSockets → UseAuthentication → UseAntiforgery → UseAuthorization`. **Production-guard PRESENCE + ABSENCE polarity** (.NET-arch A2): PRESENCE of `SessionBackedTenantContext` in Production (fail-closed if absent) AND ABSENCE of `DemoTenantContext` (fail-closed if present), reusing `TenantContextScopeAssertion`'s scan (.NET-arch A7). `SessionBackedTenantContext` registered via the EXISTING `AddSunfishTenantContext<TConcrete>` helper; single-concrete-class-PER-SCOPE (three scope-disjoint impls coexist — sec-eng G / .NET-arch O-6).
- **H7 (.NET-architect MANDATORY)** — `ISessionEstablisher` is the SINGLE session-create seam (signature PINNED, .NET-arch O-6); login + verify + magic-link all route through `EstablishAsync` — INCLUDING the bootstrap-branch verify-email-completion call (same seam despite the pipeline-branch split; A1); no divergent session creation (AB-2).
- **H8 (CIC)** — the settled decisions are ratified: BUILD first-party (D1 ✓ at dispatch); stateful server-side store (D1; sec-eng concurs — AB-1 dispositive); **auto-login-on-verify UX, POST-gated (O-1, Admiral-ruled, CIC-overridable — sec-eng cleared CONDITIONALLY-ACCEPTABLE)**; **rate-limit v1 floor / CAPTCHA deferred Tier-2 (O-3, Admiral-ruled)**; `MockOktaService` kept-dev-only (D5). O-2/O-4/O-5 resolved by sec-eng; O-6/O-7 resolved by .NET-architect (§"Open questions").

---

## Implementation roadmap (PR decomposition)

| PR | Scope | Repo | Owner | Depends on | Council | Effort |
|---|---|---|---|---|---|---|
| **PR 1** (this) | ADR 0099 drafted + dual-council reviewed + ratified | shipyard | ONR | research (#175) + WS-E (#173) | sec-eng + .NET-arch MANDATORY (ADR text) | ~12-16h |
| **PR 2** | Session substrate: new `packages/foundation-session/` (`ISessionEstablisher` + `ISessionStore`/`InMemorySessionStore` + `SessionRecord` + `SessionEstablishmentReason` + `AddSunfishSessionEstablishment`; ProjectRefs → `foundation` + `Http.Abstractions` + `Authentication.Abstractions`, NOT `foundation-authorization`; A8). In Bridge: `AddCookie` + `app.UseAuthentication()` (4-constraint ordinal, A1) + `SessionBackedTenantContext` (production-scope facade, registered via existing `AddSunfishTenantContext<TConcrete>`, A7; NOT re-gating already-gated `DemoTenantContext`, A3) + presence/absence production-guard (A2) + `Auth.*` audit constants (on the existing `AuditEventType` surface, C6) + the default-secure CSRF endpoint group (sec-eng A) | signal-bridge (+ new `shipyard/packages/foundation-session`) | Engineer | ADR Accepted | sec-eng + .NET-arch MANDATORY SPOT-CHECK | ~8-12h |
| **PR 3** | `/auth/login` + `/auth/logout` route (`AuthEndpoints`) + ADR-0097 `IPasswordHasher.VerifyHashedPassword` wiring + `Auth.LoginFailed`/`SignedOut` audit + S11 rate-limit | signal-bridge | Engineer | PR 2 + ADR 0097 Step 1 on main | sec-eng + .NET-arch MANDATORY (`/api/v1/cockpit/auth/**` identity surface — full pipeline, NOT pattern-009) | ~5-8h |
| **PR 4** | verify-email-completion → `EstablishAsync(VerifyEmailCompletion)` (W#79 closure) + magic-link-consume → `EstablishAsync(MagicLinkConsume)` (WS-E tie-in; H-WSE-2 resolved) | signal-bridge | Engineer | PR 2 + W#79 verify on main + WS-E magic-link token-issue (#173 PR-2) | sec-eng MANDATORY SPOT-CHECK | ~5-8h |
| **PR 5** | Production session store (SQLite/Postgres-backed `ISessionStore`) + admin force-logout / "log out all sessions" + idle-timeout enforcement hardening | signal-bridge (+ `packages/foundation-session`) | Engineer | PR 2 | sec-eng MANDATORY SPOT-CHECK | ~6-10h |

Total ~36-54h across ADR + 4 impl PRs. Engineer claim-beacon protocol applies (`[[substrate-claim-beacon-protocol]]`) — substrate-tier; re-fetch-main immediately-before-push. PR 2 is the substrate gate; PR 3/PR 4/PR 5 fan out from it. PR 4's magic-link half is gated on the WS-E magic-link token-issue PR (shipyard#173 PR-2) AND on PR 2.

---

## Out-of-scope-but-flagged

- **O-S1 — BYO-OIDC `category-provider` (Tier-2).** A vendor IdP (Okta/Auth0/Entra/Keycloak) is a deferred, opt-in Tier-2 swap folded later via the OIDC-impl ADR (`production-oidc-impl-adr-scoping-2026-05-20.md`). It adds an authentication *scheme* (`AddJwtBearer` / OIDC) that, after token validation, calls the SAME `ISessionEstablisher` — re-framing `ClaimsBackedTenantContext` as "one more scheme feeding `SessionBackedTenantContext`," not a parallel universe. `MockOktaService` is its dev backing IdP (D5).
- **O-S2 — Tauri / mobile session lane (A2 JWT).** A native shell cannot hold a same-origin cookie cleanly; that client needs first-party short-lived access JWT + rotating refresh (token-family theft detection, S5). Deferred to a sibling ADR; do NOT build in v1.
- **O-S3 — Phase 5 mesh auth (ADR 0061 Headscale).** Separate future ADR.
- **O-S4 — CAPTCHA gate on `/auth/login`.** Turnstile (shipyard#128 / ADR 0096 Tier-2) is **DEFERRED post-MVP** (O-3 Admiral-ruled): rate-limiting (S11) is the v1 brute-force floor; the login route CONSUMES `ICaptchaVerifier` when the gate is added but this ADR does not author it.
- **O-S5 — MFA / passkeys / SSO.** Post-MVP. The session substrate is MFA-extensible (MFA is a step BEFORE `EstablishAsync`); not in v1.

---

## Open questions — ALL RESOLVED (Rev 2)

### CIC / Admiral (UX / scope rulings)
- **O-1 — RESOLVED (Admiral-ruled, CIC-overridable): auto-login on verify, POST-gated.** sec-eng cleared it CONDITIONALLY-ACCEPTABLE — auto-login-on-verify IS a magic-link by another name (no new risk class) IF AND ONLY IF it inherits the full S9 + S4 + S1 discipline (CSPRNG ≥128-bit single-use token; atomic compare-and-delete; S1 fixation regen) AND the verify-completion is a **POST** (the landing GET shows "click to continue" that POSTs) so an email-prefetch/security-gateway GET cannot consume the single-use token and mint-then-discard a session. Folded into S9 + Integration (b). (CIC may override to redirect-to-login; sec-eng does not require it on security grounds.)
- **O-3 — RESOLVED (Admiral-ruled): rate-limiting is the v1 floor; CAPTCHA deferred post-MVP.** Turnstile CAPTCHA (shipyard#128 / ADR 0096) ships LATER as a separate Tier-2 add (the login route consumes `ICaptchaVerifier` when added). Folded into S11.

### security-engineering council
- **O-2 — RESOLVED (sec-eng ratified): absolute 8h / idle 30min, operator-configurable, server-side-enforced.** 60min idle REJECTED as a default (operator-override only). Idle ≤ absolute. Folded into S6.
- **O-4 — RESOLVED (sec-eng confirmed): `SameSite=Strict` for v1.** No MVP flow depends on an EXISTING session cookie on a cross-site inbound navigation; magic-link/verify establish the cookie ON the link-landing request (the token, not a pre-existing cookie, authenticates that request), so `Strict` holds. Folded into S3.
- **O-5 — RESOLVED (sec-eng): in-memory v1 acceptable, SINGLE-INSTANCE-ONLY.** Two documented consequences: (a) a Bridge restart logs everyone out (fail-SAFE mass-revocation, acceptable for MVP); (b) in-memory does NOT provide cross-instance revocation durability — **a scaled-out / HA / multi-replica Bridge REQUIRES the PR-5 backed store before it is multi-instance** (a session/revocation on instance A is invisible to instance B). This is a deployment-topology security constraint, not just a durability nicety. Folded into §Consequences. ONR: in-memory v1 (PR 2) → SQLite/Postgres backed (PR 5), matching the JournalStore progression.

### .NET-architect council
- **O-6 — RESOLVED (.NET-architect): `ISessionEstablisher` signature PINNED + `ClaimsBackedTenantContext` collapses into `SessionBackedTenantContext`.** ONE concrete class (NOT a thin shared base — a base + two derived classes would re-introduce the multi-class divergence ADR 0091 A1 forbids). `SessionBackedTenantContext` reads `HttpContext.User` (rehydrated from the `SessionRecord`); a future OIDC scheme produces THE SAME `HttpContext.User` and feeds the same `ISessionEstablisher` — it adds a SCHEME, not a second `ITenantContext` impl. Pinned signature in §D2; collapse in §D3.
- **O-7 — RESOLVED (.NET-architect): new `shipyard/packages/foundation-session/` Tier-1 package; acyclic ProjectRef direction.** Holds ONLY abstractions + in-memory impl (`ISessionEstablisher`, `ISessionStore`, `InMemorySessionStore`, `SessionRecord`, `SessionEstablishmentReason`, `AddSunfishSessionEstablishment`). ProjectRefs: → `foundation` (for `TenantId`) + `Microsoft.AspNetCore.Http.Abstractions` (`HttpContext`) + `Microsoft.AspNetCore.Authentication.Abstractions` (`SignInAsync`) — and **explicitly NOT `foundation-authorization`** (the establisher takes `UserId`/`TenantId` primitives, not the facade — keeps `foundation-session` reusable by a future OIDC scheme package without dragging authz-facade transitive deps, and avoids the ADR-0091 circular-ProjectRef trap by construction; A8). Direction: `foundation` ← `foundation-authorization`; `foundation` ← `foundation-session`; Bridge ← both. Acyclic. `SessionBackedTenantContext` + `AuthEndpoints` stay IN Bridge. NU1510 NoWarn per the LANDED `foundation-authorization` csproj precedent (A4). TFM `net11.0` (inherits `Directory.Build.props`; C1).

---

## Consequences

**Positive:** closes the MVP auth launch gate (with ADR 0097); one session-create seam (H7) prevents security-surface multiplication; resolves WS-E H-WSE-2; populates `HttpContext.User` (fixes the never-populated-principal structural bug); zero recurring cost / zero lock-in / self-hostable; simplifies the future OIDC-impl ADR (one more scheme, same session); reuses the existing antiforgery wiring.

**Negative / cost:** Bridge now owns a session-security surface (fixation/CSRF/revocation/isolation) — mitigated by the sec-eng dual-council gate at ADR + every PR and by the well-trodden ASP.NET Core mechanisms. A server-side store is one more piece of state. **In-memory v1 is SINGLE-INSTANCE-ONLY (sec-eng O-5):** a Bridge restart logs everyone out (a fail-SAFE mass-revocation, acceptable for MVP), AND — the deployment-topology constraint — in-memory does NOT provide cross-instance revocation durability, so a **scaled-out / HA / multi-replica Bridge REQUIRES the PR-5 backed store before it is multi-instance** (a session or revocation on instance A is invisible to instance B). The production-guard is a new fail-closed startup invariant; note that PR 2's `SessionBackedTenantContext` registration is also what first makes Bridge Production-deployable w.r.t. `ITenantContext` (the production path currently registers none — .NET-arch A2/A3).

**Reversibility:** the `ISessionEstablisher` seam is the abstraction boundary — swapping in-memory → backed store (PR 5) or adding an OIDC scheme (future) is additive, not a rewrite. If the cookie/BFF posture proves wrong for a future client, the JWT lane (O-S2) is a parallel scheme, not a replacement.

---

## ADR-protocol compliance

- **Council requirement (ADR 0069):** substrate-tier + Halt-9-class → dual-council MANDATORY (sec-eng + .NET-architect) on ADR text AND each implementation PR. `requires-council: [dotnet-architect, security-engineering]` in frontmatter.
- **Composes:** ADR 0084 (sentinel guard), 0091 (facade + tenant cross-check), 0095 (bootstrap branch + production-guard precedent), 0096 (Tier-2 + mock-first/production-guard precedent), 0097 (credential-verify half), 0098 (substrate cadence).
- **Pattern-catalog cross-check:** `/api/v1/cockpit/auth/**` and `/api/v1/cockpit/identity/**` routes are identity-surface → **always full-pipeline dual-council, NEVER pattern-009** (`standing-approved-patterns.md` pattern-009 "Does NOT match" line 290). PR authors MUST NOT claim `@standing-pattern: pattern-009` on any auth/identity route in this roadmap.
- **Slot:** highest ADR on `shipyard/main` at authoring is 0098 (Block-Naming); 0099 confirmed by `ls docs/adrs/` (0097 PasswordHasher + 0098 Block-Naming both on main as of fetch 2026-05-29).

---

— ONR, 2026-05-29 (Rev 2; dual-AMBER fold complete — 7 sec-eng (A-G) + 8 .NET-arch (A1-A8) + 6 clarifications (C1-C6) folded; O-1/O-3 Admiral-ruled defaults applied; awaiting dual-council RE-ATTEST)
