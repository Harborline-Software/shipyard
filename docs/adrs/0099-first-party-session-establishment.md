---
id: 99
title: First-Party Session Establishment Substrate
status: Proposed
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

amendments: []
---

# ADR 0099 — First-Party Session Establishment Substrate

**Status:** Proposed (Revision 1; awaiting pre-merge dual-council attestation per ADR 0069 + the substrate-tier Halt-9 cadence established by ADR 0095/0096/0097/0098). Dual-council MANDATORY (security-engineering + .NET-architect) on this ADR text AND on each implementation PR per §"Council review".

**Date:** 2026-05-29

**Resolves:** The MVP launch gate's *un-named half*. ADR 0097 (PasswordHasher, Argon2id) verifies a credential; it does not establish a session. W#79 signup/verify-email shipped (`admiral-ruling-2026-05-28T1630Z-w79-closed-main-fixed-pattern-009-ratified.md`) but the system has **no `/auth/login` route and no session-issuance mechanism anywhere** — a verified user cannot log in. The MVP feature-priority survey (`research-mvp-feature-priority-2026-05-29T0205Z.md`) correctly names ADR 0097 as launch-blocking but has a blind spot: hashing a password without session-issuance still cannot log anyone in. This ADR is the co-equal other half of the auth launch gate. It also resolves WS-E Halt **H-WSE-2** (the magic-link verify step's session-establishment dependency, `wse-tenant-comms-hand-off.md` §3 / shipyard#173).

**Decision (settled at dispatch):** BUILD first-party session establishment — cookie-based, same-origin Backend-for-Frontend (BFF) posture, server-side (stateful) session store. CIC delegated build-vs-buy to the research (`onr-session-substrate-research-2026-05-29.md`, shipyard#175); the research recommended BUILD first-party (cookie/BFF v1); Admiral is proceeding on that. A future BYO-OIDC IdP is a deferred Tier-2 `category-provider` (ADR 0096 discipline) that layers an additional authentication *scheme* on top of this same session machinery — it does not remove the need for the first-party substrate.

**Predecessor research:** `shipyard/icm/01_discovery/research/onr-session-substrate-research-2026-05-29.md` (248 lines; ONR; shipyard#175) — ground-truth of the orphaned `MockOktaService` + the build-vs-buy comparison + the S1-S11 security surface + the PR decomposition this ADR ratifies. Sibling: `shipyard/icm/05_implementation-plan/wse-tenant-comms-hand-off.md` (shipyard#173) — the magic-link → session seam (H-WSE-2) and the atomic `TryConsume` redemption precedent. Companion scoping: `shipyard/icm/01_discovery/research/production-oidc-impl-adr-scoping-2026-05-20.md` — the sibling OIDC-impl ADR (this substrate is its partial prerequisite + superset; see §1.4).

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-29 | ONR | Initial draft. Folds the session-substrate research (`onr-session-substrate-research-2026-05-29.md`, shipyard#175) recommendation (BUILD first-party; cookie/BFF v1; server-side session store) per Admiral dispatch on the settled CIC build-vs-buy decision. Codifies the `ISessionEstablisher` single-seam abstraction (login + verify-email + magic-link all route through it); ASP.NET Core `AddAuthentication().AddCookie()` + `app.UseAuthentication()` (ordered before `UseAuthorization`); `ISessionStore` server-side opaque-id store (in-memory v1 → SQLite/Postgres prod, mirroring the JournalStore in-memory→backed progression); `SessionBackedTenantContext` replacing `DemoTenantContext` (single concrete class implementing the `Foundation.Authorization` sum-interface facade per ADR 0091 R2 §2.3, unified with the OIDC-impl scoping's `ClaimsBackedTenantContext`); the S1-S11 security surface as ratified requirements; the `Auth.*` AuditEventType constants; the demo-seam + `MockOktaService` disposition. Eight ADR-text halt conditions (H1-H8). Five-PR decomposition (ADR + 4 impl PRs). Status: Proposed (awaiting dual-council). |

Promotion path: both councils self-attest GREEN via inbox status on Revision 1 → Admiral promotes ADR to `Accepted`. If a council returns AMBER, Admiral folds amendments into Revision 2 (ADR 0095 R2 / 0096 R2 / 0097 R2 / 0098 R2 precedent). **Each implementation PR (Step 2 substrate, Step 3 `/auth/login`, Step 4 verify/magic-link, Step 5 backed store) carries its own mandatory dual-council SPOT-CHECK at PR-open** per H9 — independent council pulls on the session/auth surface, not gated on ADR re-attest. Note also: any route under `/api/v1/cockpit/auth/**` or `/api/v1/cockpit/identity/**` is identity-surface and is **always full-pipeline dual-council** — NOT `pattern-009`-eligible (`standing-approved-patterns.md` pattern-009 "Does NOT match" criteria).

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
| `Sunfish.Bridge.Authorization.DemoTenantContext` | Existing (replaced/dev-gated by this ADR) | yes — `signal-bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` (hardcoded `demo-tenant` / `demo-user` / `Manager` / `HasPermission => true`; one-time DEMO-AUTH warning; xmldoc says "replaced by a claims-backed ITenantContext before production") |
| `Sunfish.Bridge.Authorization.AuthenticatedTenantPolicy` | Existing (consumer; unchanged shape) | yes — `signal-bridge/Sunfish.Bridge/Authorization/AuthenticatedTenantPolicy.cs` |
| `Sunfish.Bridge.Authorization.AccountantPolicy` | Existing (consumer; unchanged shape) | yes — `signal-bridge/Sunfish.Bridge/Authorization/AccountantPolicy.cs` |
| `Sunfish.Bridge.Authorization.DemoAuthWarningFilter` | Existing (dev-only seam; disposition §6) | yes — `signal-bridge/Sunfish.Bridge/Authorization/DemoAuthWarningFilter.cs` |
| `Sunfish.Bridge.Features.Identity.IdentityEndpoints` | Existing (reads `ctx.User.FindFirst("sub")` from an unpopulated principal — this ADR populates it) | yes — `signal-bridge/Sunfish.Bridge/Features/Identity/IdentityEndpoints.cs:160` (`ctx.User.FindFirst("sub")?.Value`); 6 `.RequireAuthorization()` routes (lines 28-32) |
| `app.UseAuthorization()` / `AddAuthorization` / `AddAntiforgery` | Existing (Bridge `Program.cs`) | yes — `Program.cs:173` (`UseAuthorization`), `:486`/`:631` (`AddAuthorization`), `:497` (`AddAntiforgery(opts => opts.HeaderName = "X-XSRF-TOKEN")`). **NO `AddAuthentication`/`AddCookie`/`UseAuthentication` anywhere** (verified by grep). |
| `Sunfish.Foundation.Authorization.ITenantContext` (sum-interface facade) | Existing | yes — `shipyard/packages/foundation-authorization/ITenantContext.cs` (ADR 0091 R2 facade) |
| `Sunfish.Foundation.Authorization.ICurrentUser` | Existing | yes — `shipyard/packages/foundation-authorization/` (ADR 0091 R2 Step 1) |
| `Sunfish.Foundation.Authorization.IAuthorizationContext` | Existing | yes — `shipyard/packages/foundation-authorization/` (ADR 0091 R2 Step 1) |
| `Sunfish.Bridge.Middleware.IBrowserTenantContext` / `TenantSubdomainResolutionMiddleware` | Existing (data-plane; subdomain-resolved tenant for the S8 cross-check) | yes — `signal-bridge/Sunfish.Bridge/Middleware/` (ADR 0091 R2 §A0; MUST-NOT-mix with control-plane contexts) |
| `Microsoft.AspNetCore.Identity.IPasswordHasher<TUser>.VerifyHashedPassword` → `PasswordVerificationResult` | Existing (BCL; ADR 0097 substrate) | yes — ADR 0097 `Argon2idPasswordHasher<TUser>` returns `Success` / `SuccessRehashNeeded` / `Failed`; `/auth/login` consumes this |
| `Sunfish.Foundation.PasswordHashing.Argon2idPasswordHasher<TUser>` | In-flight (ADR 0097 Step 1; Proposed Rev 2) | partial — ADR 0097 on main; Step 1 substrate package in Engineer's flight |
| `TenantMagicLinkIssued` / `TenantMagicLinkConsumed` (AuditEventType) | In-flight (WS-E hand-off shipyard#173; not yet built) | partial — specified in `wse-tenant-comms-hand-off.md`; this ADR adds the `Auth.SessionEstablished.MagicLink` counterpart on the consume→session edge |
| `MockOktaService` (standalone `Sdk.Web` mock OIDC IdP) | Existing (orphaned; disposition §6) | yes — `signal-bridge/MockOktaService/` (full Okta-shaped mock; PKCE; NO password validation; never wired to Bridge via `AddJwtBearer`) |
| ADR 0091 R2 §2.2 (tenant cross-check) / §2.3 (single-concrete-class facade) | Existing | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` |
| ADR 0095 (Bootstrap Context) | Existing | yes — `shipyard/docs/adrs/0095-bootstrap-context.md` |
| ADR 0096 (Tier-2 Vendor-Provider Substrate) | Existing (Proposed) | yes — `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md` |
| ADR 0097 (PasswordHasher Substrate) | Existing (Proposed) | yes — `shipyard/docs/adrs/0097-passwordhasher-substrate.md` |
| `standing-approved-patterns.md` pattern-009 ("/api/v1/cockpit/auth/** … always full pipeline") | Existing | yes — `shipyard/_shared/engineering/standing-approved-patterns.md` lines 279-318 (identity-surface carve-out at line 290) |

§A0 totals: 27 cited references. Existing & verified: 18. Introduced by this ADR: 7 (`ISessionEstablisher`, `ISessionStore`, `InMemorySessionStore`, `SessionRecord`, `SessionEstablishmentReason`, `AddSunfishSessionEstablishment`, `SessionBackedTenantContext` + `AuthEndpoints`). In-flight pending merge: 2 (`Argon2idPasswordHasher<TUser>` ADR 0097 Step 1; the WS-E magic-link audit-event family).

---

## Context

The fleet shipped signup + verify-email (W#79, CLOSED) and ratified a password-hashing primitive (ADR 0097, Argon2id, Proposed→Accepted-track). Neither establishes a session. Ground-truth (research §1, re-verified at this ADR's authoring):

1. **Bridge has authorization, not authentication.** `Program.cs` has `AddAuthorization` (`:486`/`:631`), `UseAuthorization` (`:173`), and `AddAntiforgery(... "X-XSRF-TOKEN")` (`:497`) — but **no `AddAuthentication`, no `AddCookie`, no `AddJwtBearer`, no `AddIdentity`, and no `app.UseAuthentication()`**. There is no authentication *scheme* registered. `RequireAuthorization()`'s `RequireAuthenticatedUser()` checks `HttpContext.User.Identity.IsAuthenticated`; with no scheme, `HttpContext.User` is the anonymous principal. `IdentityEndpoints` (line 160) reads `ctx.User.FindFirst("sub")` from a principal that, in production, is never populated.

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

Mechanism: ASP.NET Core `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)` + `app.UseAuthentication()` ordered **before** `app.UseAuthorization()` (H6). On the session-establish edge, `HttpContext.SignInAsync` issues the cookie carrying the opaque session id; the cookie's `ClaimsPrincipal` carries `sub` (the user id `IdentityEndpoints` already reads) + a `tid` claim (the bound tenant) — but authority decisions read the server-side record, not the cookie claims.

### D2 — `ISessionEstablisher`: the single session-create seam (H7)

All three entry points route through ONE abstraction. No divergent session creation:

```
ISessionEstablisher.EstablishAsync(
    userId, tenantId, SessionEstablishmentReason reason, HttpContext ctx, ct)
  → regenerates the session id (S1 fixation),
  → writes a SessionRecord to ISessionStore (opaque id, userId, tenantId,
    IssuedUtc, AbsoluteExpiryUtc, LastSeenUtc, reason),
  → SignInAsync issues the HttpOnly+Secure+SameSite cookie,
  → emits the reason-specific Auth.SessionEstablished.* audit event.
```

`SessionEstablishmentReason` ∈ { `PasswordLogin`, `VerifyEmailCompletion`, `MagicLinkConsume` }. Login (Step 3), verify-email-completion (Step 4), and magic-link-consume (Step 4 / WS-E) all call `EstablishAsync`; none creates a session by any other path. This is the H7 single-seam invariant the .NET-architect council ratifies.

### D3 — `SessionBackedTenantContext` replaces `DemoTenantContext`

A single concrete production class implements the `Sunfish.Foundation.Authorization` **sum-interface facade** (`ITenantContext` + `ICurrentUser` + `IAuthorizationContext`) per ADR 0091 R2 §2.3 (one concrete class provides all coherently), reading the authenticated principal/session established by `ISessionEstablisher`. Per `[[itenantcontext-consumption-qualification]]`, consumption sites pick the **Authorization sum-interface facade**, NOT the `Foundation.MultiTenancy` narrowed variant, until ADR 0091 Step 3 narrows. `SessionBackedTenantContext` is the **unification point** with the OIDC-impl scoping's `ClaimsBackedTenantContext` (research §1.4): the session substrate produces the principal; this one context class reads it — the OIDC path (future) feeds the same principal into the same context. `DemoTenantContext` becomes `IsDevelopment()`-gated (§6); production registers `SessionBackedTenantContext`.

### D4 — Mock-first / 3-tier slotting disposition: session is a **tier-1 domain-block**

Session establishment is **tier-1 (`domain-block`)** — concrete machinery never swapped for a vendor at runtime (per the three-tier slotting vocabulary: tier-1 = concrete DI never swapped; tier-2 = bounded vendor swap; tier-3 = runtime plugin). It is NOT a Tier-2 `category-provider`. There is no "session vendor"; the *authentication scheme that precedes session-establish* is the Tier-2 swap point (password vs magic-link vs future BYO-OIDC), not the session primitive itself.

**Mock variant for tests:** the `ISessionStore` has an `InMemorySessionStore` that is the v1 *production-and-test* implementation (in-memory v1; SQLite/Postgres in Step 5) — this is a tier-1 in-memory→backed progression (like JournalStore), NOT a "mock-first vendor" in the ADR 0096 sense. There is no `IMockSessionEstablisher` production-guard family — the production guard that matters here is the `DemoTenantContext`-must-not-register-in-Production assertion (§6), which mirrors ADR 0095's bootstrap mutual-exclusion assertion and ADR 0096's `MockProviderProductionGuardAssertion` in spirit. Tests construct a real `ISessionEstablisher` against an `InMemorySessionStore`; no mock-marker conflation with ADR 0096/0097 mock families.

### D5 — `MockOktaService` disposition: keep, dev-only, do NOT wire into v1

Retain `MockOktaService` under dev-only, marked as future-BYO-OIDC test scaffold. Do NOT wire it into the v1 session substrate (no `AddJwtBearer` against it). It becomes useful again only when the Tier-2 BYO-OIDC `category-provider` is built (future OIDC-impl ADR), as the dev backing IdP for that scheme's integration tests. Retiring it now would discard correct OIDC-shaped scaffolding the future lane will want; wiring it now would add an unused authentication scheme to the v1 surface. Park it.

---

## Mechanism (substrate shape)

```
signal-bridge/Sunfish.Bridge/Program.cs (Step 2)
  builder.Services.AddSunfishSessionEstablishment(opts => { ... });   // registers
       ISessionEstablisher + ISessionStore (InMemorySessionStore v1) + SessionBackedTenantContext
  builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
       .AddCookie(opts => {
           opts.Cookie.HttpOnly = true;                       // S3
           opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;   // S3
           opts.Cookie.SameSite = SameSiteMode.Strict;        // S3 (first-party same-origin; Lax minimum)
           opts.SlidingExpiration = true;                     // S6 idle
           opts.ExpireTimeSpan = <idle-timeout>;              // S6 (sec-eng default; e.g. 30-60min)
           opts.Events.OnValidatePrincipal = <server-side store check + tenant cross-check>;  // S7/S8
       });
  ...
  app.UseAuthentication();   // H6 — MUST precede UseAuthorization
  app.UseAuthorization();    // existing :173
```

- **`ISessionEstablisher`** — the single session-create seam (D2). The only thing that calls `SignInAsync`.
- **`ISessionStore` / `InMemorySessionStore` / `SessionRecord`** — server-side opaque-id store; atomic create/read/touch/remove. `SessionRecord` = { `SessionId` (CSPRNG ≥128-bit, S4), `UserId`, `TenantId`, `IssuedUtc`, `AbsoluteExpiryUtc`, `LastSeenUtc`, `Reason` }.
- **`OnValidatePrincipal`** — on every authenticated request: (a) look up the `SessionRecord` by the cookie's opaque id; reject (sign-out + 401) if absent/expired/revoked (S7); (b) cross-check the record's bound `TenantId` against the subdomain-resolved `IBrowserTenantContext.Tenant` (S8 / ADR 0091 R2 §2.2) — mismatch → 401 + `Auth.SessionTenantMismatch` audit; (c) touch `LastSeenUtc` for sliding idle (S6); (d) reject `TenantId.System` sentinel binding (ADR 0084).
- **`SessionBackedTenantContext`** — reads the validated principal/record; implements the facade (D3).

---

## Security surface (sec-eng dual-council territory — H1-H5, S1-S11)

Folded from research §3. This is a Halt-9-class auth substrate; sec-eng MANDATORY at ADR text + every implementation PR.

| # | Surface | Ratified requirement | Halt |
|---|---|---|---|
| S1 | **Session fixation** | `ISessionEstablisher.EstablishAsync` regenerates the session id on EVERY privilege change — post-login, post-verify-email, post-magic-link-consume. NEVER carry a pre-auth/bootstrap session id into the authenticated session. | H1 |
| S2 | **CSRF** | The existing `X-XSRF-TOKEN` antiforgery (`Program.cs:497`) MUST cover ALL cookie-authenticated mutating routes. Cookie sessions are CSRF-exposed; this is non-negotiable under D1 (cookie). Antiforgery validation wired on every state-changing cookie-auth route. | H2 |
| S3 | **Cookie flags** | `HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Strict` (first-party same-origin; `Lax` is the floor, never `None`). No session material readable by JS — only the opaque id rides the cookie. | H2 |
| S4 | **Token entropy** | Session ids + single-use tokens from a CSPRNG (`RandomNumberGenerator.Fill(Span<byte>)`), ≥128 bits. (The orphaned mock used 24-32 random bytes for auth codes — match or exceed.) | H3 (shared store) |
| S5 | **Rotation** (deferred A2 JWT only) | Out of v1 scope (stateful store + idle/absolute TTL replaces refresh rotation for cookie sessions). Documented for the future Tauri/mobile JWT lane: rolling refresh + token-family theft detection. | — (§9) |
| S6 | **TTL / idle timeout** | Absolute session lifetime + sliding idle timeout, operator-configurable. Financial-app posture: short defaults (sec-eng to set; ONR rec 8-12h absolute / 30-60min idle — Open Q O-2). Enforced server-side via the store. | H5 |
| S7 | **Logout / revocation** | Server-side session invalidation (remove `SessionRecord`), NOT just cookie-clear — a stolen cookie dies on logout. Admin force-logout + "log out all sessions" enabled by the store. | H5 |
| S8 | **Multi-tenant session isolation** | A session bound to tenant A MUST cross-check the subdomain-resolved tenant (`IBrowserTenantContext` / `TenantSubdomainResolutionMiddleware`) against the record's bound `TenantId` (ADR 0091 R2 §2.2). Mismatch → 401 + `Auth.SessionTenantMismatch` audit. No cross-tenant session reuse. Reject `TenantId.System` binding (ADR 0084). | H4 |
| S9 | **Single-use-token atomicity** | The verify-email + magic-link consume path MUST be atomic compare-and-delete (no check-then-use TOCTOU). Mirrors the W#79 `TryConsumeEmailVerificationAsync` gate (read row → assert flag-false → atomically transition → return bool; already-consumed falls through to byte-identical idempotent handling). The session-establish call happens ONLY on the first-consume branch. | H3 |
| S10 | **Audit emission** | New `AuditEventType` constants: `Auth.SessionEstablished.PasswordLogin`, `Auth.SessionEstablished.VerifyEmail`, `Auth.SessionEstablished.MagicLink`, `Auth.LoginFailed`, `Auth.SignedOut`, `Auth.SessionRevoked`, `Auth.SessionTenantMismatch` (S8 trigger). Login-failure events feed the rate-limit/lockout signal (S11). | H1-H5 (cross) |
| S11 | **Brute-force / rate-limit** | `/auth/login` needs rate-limiting + (optional) lockout-after-N-failures via ASP.NET Core rate-limiting middleware, per-credential SHA-256-prefix bucket key (W#79 §3.7 precedent). CAPTCHA gate (Turnstile, shipyard#128 / ADR 0096) is a SEPARATE Tier-2 decision — flagged, not in this ADR's v1 (Open Q O-3). | H2 (login PR) |

---

## Integration points

- **(a) W#79 password login (post-ADR-0097):** new `Sunfish.Bridge.Features.Auth.AuthEndpoints` `/auth/login` → resolve user → `IPasswordHasher<UserEntity>.VerifyHashedPassword` (ADR 0097 Argon2id; returns `Success` / `SuccessRehashNeeded` / `Failed`) → on `Success`/`SuccessRehashNeeded`, `ISessionEstablisher.EstablishAsync(reason: PasswordLogin)`; on `SuccessRehashNeeded` ALSO trigger ADR 0097's lazy rehash-on-next-login. On `Failed`, emit `Auth.LoginFailed` + feed the rate-limiter (S11). ADR 0097 verifies the credential; THIS ADR establishes the session. (Route is `/api/v1/cockpit/auth/**`-family → always full-pipeline dual-council, never pattern-009.)
- **(b) W#79 verify-email completion ("now logged in"):** after `VerifyEmail` succeeds (its `TryConsume` atomic gate, S9), call `ISessionEstablisher.EstablishAsync(reason: VerifyEmailCompletion)`. Closes the "verified but cannot log in" dead-end. **UX: ONR recommends auto-login on verify** (MVP friction reduction) with S1 fixation regen — vs redirect to `/auth/login` (Open Q O-1, CIC UX call).
- **(c) WS-E magic-link verify (H-WSE-2 resolved):** validate single-use token (S9 atomic, the `TryConsume` pattern shared with verify-email) → `ISessionEstablisher.EstablishAsync(reason: MagicLinkConsume)`. The session substrate owns session-create; WS-E (shipyard#173) owns token-issue + email-deliver. Audit `Auth.SessionEstablished.MagicLink`. This ADR is the substrate the WS-E magic-link PR-2 depends on; with it Accepted, the WS-E "validate-and-redirect-to-login (degraded)" fallback is unnecessary.
- **(d) Existing `AuthenticatedTenantPolicy` / `ITenantContext` consumption:** the established session populates `HttpContext.User` with the `sub` claim `IdentityEndpoints.TryResolve` (`:160`) and `RequireAuthenticatedUser()` already expect — closing the "authorization gated on a never-populated principal" structural bug. The session's bound tenant satisfies ADR 0091 R2 §2.2 (S8). `SessionBackedTenantContext` (facade; ADR 0091 R2 §2.3) replaces `DemoTenantContext`.

---

## Demo-seam + MockOktaService disposition

- **`DemoTenantContext` → `IsDevelopment()`-gated.** Production composition root registers `SessionBackedTenantContext`; the dev composition root may keep `DemoTenantContext` for local UX with the `DemoAuthWarningFilter` one-time warning. A startup `IHostedService` assertion fails-closed if `DemoTenantContext` (or any `IMock*` / demo seam) is registered while `ASPNETCORE_ENVIRONMENT == "Production"` — mirrors ADR 0095's `BootstrapAndTenantMutualExclusionAssertion` + ADR 0096's `MockProviderProductionGuardAssertion` registration-snapshot scan (`ServiceDescriptor.ImplementationType`, not runtime resolution). This is the production-guard for the session tier (D4).
- **`MockOktaService` → keep, dev-only, do NOT wire v1** (D5). Mark as future-BYO-OIDC test scaffold.

---

## Adversarial Brief

Per ADR 0093 Rev 4. Worst-case interpretation of each load-bearing decision; the questions that surface interface/contract-completeness gaps at design time rather than at Stage-06 SPOT-CHECK.

- **AB-1 (D1 stateful-vs-stateless).** *Worst case if we picked stateless signed-token v1:* logout cannot revoke before expiry → a stolen cookie remains valid for the full TTL despite the user clicking "log out"; admin force-logout is impossible; a tenant claim in the cookie is trusted on signature alone, widening the S8 isolation surface. **Mitigation: D1 mandates the server-side store.** The cookie carries only an opaque id; authority reads the record. Stateless deferred to the future JWT lane (§9) where it is justified by a non-cookie client.
- **AB-2 (D2 single-seam).** *Worst case if login / verify / magic-link each created sessions independently:* three divergent `SignInAsync` callsites, three places to forget S1 fixation regen, three audit-event shapes, three tenant-binding implementations — the exact security-surface multiplication that produces "fixed in one path, vulnerable in another." **Mitigation: H7 — `ISessionEstablisher.EstablishAsync` is the ONLY session-create path; the three entry points are reasons, not implementations.**
- **AB-3 (S1 fixation).** *Worst case:* the pre-auth bootstrap/anonymous session id is carried into the authenticated session → an attacker who fixes a victim's pre-auth session id rides the post-auth elevation. **Mitigation: regenerate the session id inside `EstablishAsync` on every privilege change; never reuse a pre-auth id. Tested (H1).**
- **AB-4 (S8 tenant isolation).** *Worst case:* a session minted on `tenant-a.app` is replayed against `tenant-b.app` (same cookie, different subdomain) and the authorization layer trusts the cookie's tenant → cross-tenant data access. **Mitigation: `OnValidatePrincipal` cross-checks the record's bound `TenantId` against the subdomain-resolved tenant on EVERY request; mismatch → 401 + `Auth.SessionTenantMismatch` (S8 / ADR 0091 R2 §2.2). Reject `TenantId.System` (ADR 0084).**
- **AB-5 (S9 TOCTOU).** *Worst case:* verify-email / magic-link consume reads "token unused," then (race) a second concurrent request also reads "unused" before either marks it consumed → the single-use token establishes TWO sessions. **Mitigation: atomic compare-and-delete (the W#79 `TryConsume` gate); session-establish only on the first-consume branch; already-consumed → byte-identical idempotent handling (no leakage).**
- **AB-6 (S7 revocation + S2 CSRF interaction).** *Worst case:* logout clears the cookie client-side but the server-side record lingers → a captured cookie still authenticates; OR a mutating cookie-auth route ships without antiforgery → CSRF. **Mitigation: logout removes the `SessionRecord` server-side (S7); ALL mutating cookie-auth routes carry `X-XSRF-TOKEN` antiforgery (S2/H2).**
- **AB-7 (`HttpContext.User` population gap).** *Worst case if left implicit:* the ADR ships the session store but never wires `UseAuthentication` before `UseAuthorization`, so `RequireAuthorization()` still sees the anonymous principal and either fails-open (everyone unauthenticated) or the demo seam silently papers it in production. **Mitigation: H6 — `UseAuthentication` ordered before `UseAuthorization`; the production-guard assertion fails-closed if `DemoTenantContext` is registered in Production.**

---

## Council halt-conditions (ADR-text gate)

Dual-council MANDATORY (sec-eng + .NET-architect) on this ADR text AND each implementation PR (H9). Sec-eng owns H1-H5; .NET-architect owns H6-H7; CIC ratifies H8.

- **H1 (sec-eng MANDATORY)** — session-fixation regeneration on privilege change (S1) is specified at the `ISessionEstablisher` seam and tested.
- **H2 (sec-eng MANDATORY)** — cookie flags HttpOnly+Secure+SameSite (S3) + CSRF antiforgery coverage of ALL cookie-auth mutating routes (S2) + `/auth/login` rate-limit (S11).
- **H3 (sec-eng MANDATORY)** — single-use-token atomicity (S9) is compare-and-delete, no TOCTOU; CSPRNG entropy ≥128-bit (S4); shared with verify-email + magic-link.
- **H4 (sec-eng MANDATORY)** — multi-tenant session isolation cross-check (S8 / ADR 0091 R2 §2.2) at the `OnValidatePrincipal` pipeline boundary; `TenantId.System` rejected (ADR 0084).
- **H5 (sec-eng MANDATORY)** — server-side revocation (S7) — logout invalidates the server-side record, stolen cookie dies; absolute + idle TTL (S6) enforced server-side.
- **H6 (.NET-architect MANDATORY)** — `app.UseAuthentication()` ordered before `app.UseAuthorization()`; `SessionBackedTenantContext` is a single concrete instance providing all facade interfaces coherently (ADR 0091 R2 §2.3); production-guard fails-closed on `DemoTenantContext` in Production.
- **H7 (.NET-architect MANDATORY)** — `ISessionEstablisher` is the SINGLE session-create seam; login + verify + magic-link all route through `EstablishAsync`; no divergent session creation (AB-2).
- **H8 (CIC)** — the settled decisions are ratified: BUILD first-party (D1 ✓ at dispatch); stateful server-side store (D1 — ONR recommendation, awaiting sec-eng concurrence on O-2 defaults); auto-login-on-verify UX (O-1); `MockOktaService` kept-dev-only (D5). O-1 / O-2 / O-3 are the open rulings (§"Open questions").

---

## Implementation roadmap (PR decomposition)

| PR | Scope | Repo | Owner | Depends on | Council | Effort |
|---|---|---|---|---|---|---|
| **PR 1** (this) | ADR 0099 drafted + dual-council reviewed + ratified | shipyard | ONR | research (#175) + WS-E (#173) | sec-eng + .NET-arch MANDATORY (ADR text) | ~12-16h |
| **PR 2** | Session substrate: `ISessionEstablisher` + `ISessionStore`/`InMemorySessionStore` + `SessionRecord` + `AddCookie` + `app.UseAuthentication()` + `SessionBackedTenantContext` (replaces `DemoTenantContext`, `IsDevelopment`-gated) + production-guard assertion + `Auth.*` audit constants | signal-bridge (+ `packages/foundation-session`) | Engineer | ADR Accepted | sec-eng + .NET-arch MANDATORY SPOT-CHECK | ~8-12h |
| **PR 3** | `/auth/login` + `/auth/logout` route (`AuthEndpoints`) + ADR-0097 `IPasswordHasher.VerifyHashedPassword` wiring + `Auth.LoginFailed`/`SignedOut` audit + S11 rate-limit | signal-bridge | Engineer | PR 2 + ADR 0097 Step 1 on main | sec-eng + .NET-arch MANDATORY (`/api/v1/cockpit/auth/**` identity surface — full pipeline, NOT pattern-009) | ~5-8h |
| **PR 4** | verify-email-completion → `EstablishAsync(VerifyEmailCompletion)` (W#79 closure) + magic-link-consume → `EstablishAsync(MagicLinkConsume)` (WS-E tie-in; H-WSE-2 resolved) | signal-bridge | Engineer | PR 2 + W#79 verify on main + WS-E magic-link token-issue (#173 PR-2) | sec-eng MANDATORY SPOT-CHECK | ~5-8h |
| **PR 5** | Production session store (SQLite/Postgres-backed `ISessionStore`) + admin force-logout / "log out all sessions" + idle-timeout enforcement hardening | signal-bridge (+ `packages/foundation-session`) | Engineer | PR 2 | sec-eng MANDATORY SPOT-CHECK | ~6-10h |

Total ~36-54h across ADR + 4 impl PRs. Engineer claim-beacon protocol applies (`[[substrate-claim-beacon-protocol]]`) — substrate-tier; re-fetch-main immediately-before-push. PR 2 is the substrate gate; PR 3/PR 4/PR 5 fan out from it. PR 4's magic-link half is gated on the WS-E magic-link token-issue PR (shipyard#173 PR-2) AND on PR 2.

---

## Out-of-scope-but-flagged

- **O-S1 — BYO-OIDC `category-provider` (Tier-2).** A vendor IdP (Okta/Auth0/Entra/Keycloak) is a deferred, opt-in Tier-2 swap folded later via the OIDC-impl ADR (`production-oidc-impl-adr-scoping-2026-05-20.md`). It adds an authentication *scheme* (`AddJwtBearer` / OIDC) that, after token validation, calls the SAME `ISessionEstablisher` — re-framing `ClaimsBackedTenantContext` as "one more scheme feeding `SessionBackedTenantContext`," not a parallel universe. `MockOktaService` is its dev backing IdP (D5).
- **O-S2 — Tauri / mobile session lane (A2 JWT).** A native shell cannot hold a same-origin cookie cleanly; that client needs first-party short-lived access JWT + rotating refresh (token-family theft detection, S5). Deferred to a sibling ADR; do NOT build in v1.
- **O-S3 — Phase 5 mesh auth (ADR 0061 Headscale).** Separate future ADR.
- **O-S4 — CAPTCHA gate on `/auth/login`.** Turnstile (shipyard#128 / ADR 0096 Tier-2) is a separate decision; the login route CONSUMES `ICaptchaVerifier` when the gate is added but this ADR does not author it (O-3).
- **O-S5 — MFA / passkeys / SSO.** Post-MVP. The session substrate is MFA-extensible (MFA is a step BEFORE `EstablishAsync`); not in v1.

---

## Open questions

### For CIC (UX / scope rulings)
- **O-1** — Verify-email completion UX: auto-login on verify (ONR recommendation; MVP friction reduction; with S1 fixation regen) vs redirect to `/auth/login`?
- **O-3** — `/auth/login` CAPTCHA gate (Turnstile, shipyard#128 / ADR 0096): in this ADR's v1 (PR 3) or a separate post-MVP Tier-2 PR? (ONR: separate; rate-limit S11 is the v1 brute-force floor.)

### For security-engineering council
- **O-2** — Idle-timeout + absolute-lifetime defaults for a financial app (S6): ONR rec 8-12h absolute / 30-60min idle, operator-configurable. Council to ratify the defaults.
- **O-4** — `SameSite=Strict` vs `Lax` for v1: ONR rec `Strict` (first-party same-origin BFF; no cross-site flows in MVP). Confirm no MVP flow needs `Lax` (e.g., an emailed magic-link click that must carry the cookie on first navigation — magic-link consume is a POST after a GET landing, so `Strict` should hold; council to confirm).
- **O-5** — Session-store v1 backing: in-memory (PR 2) → SQLite/Postgres (PR 5). Confirm in-memory is acceptable for the first production cut OR whether SQLite-from-PR-2 is required (revocation works in-memory but does not survive a Bridge restart — a restart logs everyone out, which is arguably acceptable for MVP). ONR: in-memory v1, backed in PR 5, matching the JournalStore progression.

### For .NET-architect council
- **O-6** — `ISessionEstablisher` shape + the unification of `SessionBackedTenantContext` with the OIDC-impl scoping's `ClaimsBackedTenantContext` (D3 / research §4d) — one class fed by multiple schemes, or a thin shared base?
- **O-7** — `foundation-session` package placement: a new `shipyard/packages/foundation-session/` (Tier-1 primitive, like `foundation-password-hashing`) vs in-Bridge under `Sunfish.Bridge/Authorization/`? ONR leans a `packages/foundation-session/` package for the `ISessionEstablisher`/`ISessionStore`/`SessionRecord` contracts (reusable by a future OIDC scheme + testable in isolation), with `SessionBackedTenantContext` + `AuthEndpoints` staying in Bridge.

---

## Consequences

**Positive:** closes the MVP auth launch gate (with ADR 0097); one session-create seam (H7) prevents security-surface multiplication; resolves WS-E H-WSE-2; populates `HttpContext.User` (fixes the never-populated-principal structural bug); zero recurring cost / zero lock-in / self-hostable; simplifies the future OIDC-impl ADR (one more scheme, same session); reuses the existing antiforgery wiring.

**Negative / cost:** Bridge now owns a session-security surface (fixation/CSRF/revocation/isolation) — mitigated by the sec-eng dual-council gate at ADR + every PR and by the well-trodden ASP.NET Core mechanisms. A server-side store is one more piece of state (in-memory v1 means a Bridge restart logs everyone out — O-5). `DemoTenantContext` becomes dev-only, requiring the production-guard assertion (a new fail-closed startup invariant).

**Reversibility:** the `ISessionEstablisher` seam is the abstraction boundary — swapping in-memory → backed store (PR 5) or adding an OIDC scheme (future) is additive, not a rewrite. If the cookie/BFF posture proves wrong for a future client, the JWT lane (O-S2) is a parallel scheme, not a replacement.

---

## ADR-protocol compliance

- **Council requirement (ADR 0069):** substrate-tier + Halt-9-class → dual-council MANDATORY (sec-eng + .NET-architect) on ADR text AND each implementation PR. `requires-council: [dotnet-architect, security-engineering]` in frontmatter.
- **Composes:** ADR 0084 (sentinel guard), 0091 (facade + tenant cross-check), 0095 (bootstrap branch + production-guard precedent), 0096 (Tier-2 + mock-first/production-guard precedent), 0097 (credential-verify half), 0098 (substrate cadence).
- **Pattern-catalog cross-check:** `/api/v1/cockpit/auth/**` and `/api/v1/cockpit/identity/**` routes are identity-surface → **always full-pipeline dual-council, NEVER pattern-009** (`standing-approved-patterns.md` pattern-009 "Does NOT match" line 290). PR authors MUST NOT claim `@standing-pattern: pattern-009` on any auth/identity route in this roadmap.
- **Slot:** highest ADR on `shipyard/main` at authoring is 0098 (Block-Naming); 0099 confirmed by `ls docs/adrs/` (0097 PasswordHasher + 0098 Block-Naming both on main as of fetch 2026-05-29).

---

— ONR, 2026-05-29 (Rev 1; awaiting dual-council MANDATORY attestation)
