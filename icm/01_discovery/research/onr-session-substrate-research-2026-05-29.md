# ONR research — Session-establishment (auth/login) substrate (2026-05-29)

**Requester:** Admiral (dispatch 2026-05-29; CIC build-vs-buy ruling + session-substrate ADR are the consumers)
**Authored by:** ONR
**Authored at:** 2026-05-29T03:00Z
**Status:** draft (Stage-01/02 research; feeds a CIC build-vs-buy ruling + a session-substrate ADR; NOT a draft ADR)

---

## Scope of investigation

- **In scope:** ground-truth of the existing `MockOktaService` / `MockTokenGenerator` stub + all fleet Okta/OIDC/session references; the build-vs-buy fork (first-party session issuance vs third-party IdP); the session security surface (fixation/CSRF/cookie-flags/entropy/rotation/TTL/revocation/multi-tenant isolation/single-use-token); integration with W#79 password-login (post-ADR-0097), W#79 verify-email completion, WS-E magic-link, and the existing `AuthenticatedTenantPolicy`/`ITenantContext` consumption; a proposed session-substrate ADR outline + council halt-conditions + PR decomposition.
- **Out of scope:** drafting the ADR itself (separate work item); the *claims-backed `ITenantContext`* production-OIDC-impl ADR already scoped in `production-oidc-impl-adr-scoping-2026-05-20.md` (this session-substrate ADR is its SIBLING and partial PREREQUISITE — see §1.4); password-hashing primitive (ADR 0097, Accepted, Engineer in flight — this ADR CONSUMES it); Phase 5 mesh auth (ADR 0061 Headscale; separate future ADR); choosing a specific vendor SKU/pricing tier.
- **Authoritative sources consulted (primary, first-party):** `signal-bridge/MockOktaService/Program.cs` + `Services/MockTokenGenerator.cs` + `Services/IMockTokenGenerator.cs` + `.csproj`; `signal-bridge/Sunfish.Bridge/Program.cs` (auth-region lines 173/486/631); `Sunfish.Bridge/Authorization/{DemoTenantContext,AuthenticatedTenantPolicy,AccountantPolicy,DemoAuthWarningFilter}.cs`; `Sunfish.Bridge/Features/Identity/IdentityEndpoints.cs`; `shipyard/packages/foundation-authorization/{ITenantContext,ICurrentUser,IAuthorizationContext}.cs`; ADR 0097 attest (`admiral-attest-2026-05-27T1645Z`); W#79 closure ruling (`admiral-ruling-2026-05-28T1630Z`); `w60-collaboration-phase4-stage06-handoff.md` (magic-link substrate); `production-oidc-impl-adr-scoping-2026-05-20.md`; ADR 0091 R2 invariants.
- **Success looks like:** CIC has a crisp build-vs-buy decision frame with a clear ONR recommendation; the session-substrate ADR has a known target shape, council halt-conditions, and PR decomposition; the relationship between this ADR, ADR 0097 (password hashing), the OIDC-impl scoping doc, and WS-E magic-link is unambiguous.

---

## TL;DR

1. **Ground-truth: the gap is real and precise.** Bridge has `AddAuthorization` + `UseAuthorization` + role/tenant policies, but **NO `AddAuthentication` / `AddCookie` / `AddJwtBearer` / `AddIdentity` scheme**. Nothing populates `HttpContext.User`. `IdentityEndpoints` and every `RequireAuthorization()` route read `ctx.User.FindFirst("sub")` from a `ClaimsPrincipal` that, in production, is the anonymous principal. The only thing that "logs anyone in" today is `DemoTenantContext` (hardcoded `demo-user` / `Manager` / `HasPermission => true`, dev-only). **Signup + verify-email ship (W#79 CLOSED), but the system cannot establish a session — there is no `/auth/login` route and no session issuance anywhere.**

2. **The `MockOktaService` stub proves the original intended path was a third-party OIDC IdP (Okta-shaped).** It is a *full standalone mock IdP* (its own `Microsoft.NET.Sdk.Web` service) mimicking Okta's `/oauth2/v1/*` URL shape: discovery, `/authorize` (PKCE + login page + optional 2FA), `/token` (authorization_code + password + client_credentials), `/userinfo`, `/introspect`, `/jwks`, `/logout`. **But it is orphaned** — Bridge never wires `AddJwtBearer` to consume its JWKS. It validates no passwords (any non-empty username/password is accepted). It is aspirational scaffolding from an abandoned/paused "integrate Okta" intent, not a working seam.

3. **Build-vs-buy recommendation: BUILD first-party session.** Bridge already serves the React SPA same-origin in prod (a de-facto Backend-for-Frontend posture) — the canonical 2026 best practice for first-party SPAs is exactly BFF + HttpOnly/Secure/SameSite cookies, not browser-held bearer tokens. The two flows that gate MVP — verify-email-completion and WS-E magic-link — do NOT and SHOULD NOT go through an OIDC redirect; they establish a session directly after single-use-token validation. First-party session is the natural fit, has zero recurring cost, zero vendor lock-in, and aligns with the fleet's local-first/self-hosted/MIT-open-source ethos. A third-party IdP is a poor fit for the passwordless flows, adds recurring cost + lock-in, and would itself be a Tier-2 `category-provider` (BYO-OIDC) that the first-party substrate must support *anyway* — so first-party is not avoidable even if a vendor is later added.

4. **Top security halt-conditions (sec-eng dual-council territory):** (a) session-fixation — regenerate the session identifier on privilege change (post-login, post-verify, post-magic-link-consume); (b) CSRF — the existing `X-XSRF-TOKEN` antiforgery wiring MUST cover all cookie-authenticated mutating routes; (c) cookie flags HttpOnly + Secure + SameSite (Lax min, Strict preferred for first-party); (d) single-use-token atomicity — the verify-email + magic-link consume path MUST be atomic compare-and-delete (mirrors W#79 + W#60 P4 discipline); (e) multi-tenant session isolation — a session issued for tenant A MUST cross-check the subdomain-resolved tenant (ADR 0091 R2 invariant 2.2); (f) revocation/logout — server-side session invalidation, not just cookie-clear.

5. **MVP-criticality: YES — production-launch blocker, co-equal with ADR 0097.** The MVP-priority research doc (`research-mvp-feature-priority-2026-05-29T0205Z`) correctly names ADR 0097 password-hashing as "the ONE substrate item genuinely launch-blocking" — but it has a **blind spot**: password-hashing without session-issuance still cannot log anyone in. Hashing a password verifies a credential; it does not create a session. These are two halves of one launch gate. ADR 0097 (verify the credential) + this ADR (establish the session) together unblock real auth. Neither alone ships login.

6. **Sequencing: this ADR should be drafted NOW, in parallel with ADR 0097 Step 1/2.** It is a hard prerequisite for W#79 login-completion, WS-E magic-link (P3), and the eventual OIDC-impl ADR (which establishes sessions FROM OIDC tokens — same session machinery). It is NOT gated on cohort-5 (P1) feature work.

7. **CIC ratification needed on:** (D1) build-vs-buy verdict (ONR leans BUILD); (D2) cookie-session vs first-party-JWT mechanism; (D3) whether to retire or repurpose `MockOktaService`; (D4) session-store backing (in-memory v1 → SQLite/Postgres prod); (D5) ADR slot + title.

---

## 1. History / existing state (ground-truth)

### 1.1 The `MockOktaService` stub — what it actually is

`signal-bridge/MockOktaService/` is a **standalone ASP.NET Core web service** (`<Project Sdk="Microsoft.NET.Sdk.Web">`), not a library wired into Bridge. It is a **mock OIDC Identity Provider** that imitates Okta's endpoint shape:

| Endpoint | Behavior |
|---|---|
| `GET /.well-known/openid-configuration` | OIDC discovery doc; advertises `authorization_code`, `implicit`, `refresh_token`, `password`, `client_credentials` grants; PKCE `S256`; RS256 id-token signing |
| `GET /oauth2/v1/authorize` | OAuth2 authorize with **PKCE required** (`code_challenge`); renders an HTML login page; optional 2FA page (hardcoded code `123456`) |
| `POST /oauth2/v1/login` + `/verify-2fa` | Login form post → issues auth code. **No password validation** — only checks username/password are non-empty |
| `POST /oauth2/v1/token` | `authorization_code` (validates PKCE code_verifier, client_id/redirect_uri match, code expiry), plus legacy `password` + `client_credentials` grants |
| `GET /oauth2/v1/userinfo` | Bearer-token → claims |
| `POST /oauth2/v1/introspect`, `/revoke`, `GET /oauth2/v1/keys` (JWKS), `/logout` | OIDC introspection / JWKS / end-session |

`MockTokenGenerator` issues `JwtSecurityToken`s signed by `MockSigningKeys`, 1-hour expiry, with standard OIDC claims (`sub`, `email`, `name`, `scope`, `token_use`). It is well-built dev scaffolding (correlation-id logging, hashed-for-log PII, PKCE S256 enforcement) — but it is a **mock IdP, not a session issuer**.

**Ground-truth verdict:** the stub answers the dispatch's question directly — **a third-party IdP (Okta) WAS the original intended path.** Someone built (or scaffolded from a template) a faithful Okta-shaped mock so Bridge could integrate `AddJwtBearer` against `Authority = <okta-or-mock>`. That integration **was never completed**: there is no `AddJwtBearer`/`AddAuthentication` in Bridge consuming it (§1.2). The mock IdP is orphaned — runnable, but nothing downstream is wired to it.

### 1.2 What Bridge actually has (and lacks)

Confirmed by grep of `signal-bridge/Sunfish.Bridge/`:

- **`AddAuthorization`** — `Program.cs:486` (cockpit + authenticated-tenant + accountant policies) and `:631` (relay posture). **`UseAuthorization`** at `:173`.
- **Antiforgery** — `Program.cs:497` `AddAntiforgery(opts => opts.HeaderName = "X-XSRF-TOKEN")` (W#74 PR 3; SPA CSRF wiring already present).
- **NO `AddAuthentication`. NO `AddCookie`. NO `AddJwtBearer`. NO `AddIdentity`. NO `app.UseAuthentication()`.** There is no authentication *scheme* registered at all.
- **`DemoTenantContext`** (`Authorization/DemoTenantContext.cs`) — the ONLY thing standing in for an identity. Hardcoded `TenantId = "demo-tenant"`, `UserId = "demo-user"`, `Roles = [Manager]`, `HasPermission => true`. Logs a one-time "DEMO AUTH SEAM ACTIVE … Replace with a real ITenantContext implementation before production deployment" warning. Its XML-doc explicitly says: *"in production this class is replaced by a claims-backed `ITenantContext` that reads the authenticated tenant from OIDC/Entra/Okta."*

**The structural problem:** `RequireAuthorization()`'s `RequireAuthenticatedUser()` checks `HttpContext.User.Identity.IsAuthenticated`. With no authentication scheme, `HttpContext.User` is the default anonymous principal. In **dev**, `DemoTenantContext` (a DI service, separate from `HttpContext.User`) papers over this for the handlers that read it directly. But `IdentityEndpoints.TryResolve` (and any `RequireAuthorization()` policy) reads `ctx.User.FindFirst("sub")` — which is **never populated** because nothing authenticates the user into `HttpContext.User`. The authorization layer is gated on a claims principal that no production code path ever fills.

### 1.3 W#79 signup + verify-email issues no session (confirmed)

W#79 (onboarding) is CLOSED (`admiral-ruling-2026-05-28T1630Z`): `/auth/signup`, `/auth/verify-email`, `/auth/verify-email/pending`, `/auth/resend-verification` all merged and live. The route inventory in `research-mvp-feature-priority-2026-05-29T0205Z` lists exactly those four — **and conspicuously NO `/auth/login`**. Signup creates a tenant + user (control-plane via `TenantLifecycleCoordinator` / `TenantRegistry`); verify-email confirms the address. **Neither establishes a session.** A user who signs up and verifies their email is in a "verified but cannot log in" state — there is no mechanism to turn a verified user into an authenticated session. This is the literal MVP gap.

### 1.4 Relationship to the prior OIDC-impl scoping (`production-oidc-impl-adr-scoping-2026-05-20.md`)

A prior ONR deliverable scoped a **production OIDC-impl ADR** (proposed "ADR 0XXX — Production Claims-Backed ITenantContext (OIDC integration)"). That doc and this one are **siblings, not duplicates**, and clarifying the boundary is load-bearing for CIC:

- **The OIDC-impl ADR** answers: "when a user authenticates *via OIDC*, how does Bridge turn the validated OIDC token into a claims-backed `ITenantContext`?" It assumes a session/identity already arrives (as a JWT from an IdP).
- **THIS session-substrate ADR** answers: "how does *any* user become authenticated in the first place — for the password + magic-link + verify-email flows that do NOT involve OIDC?" It is the mechanism that *establishes* the `HttpContext.User` / session that everything (including a future OIDC path) consumes.

**Critical insight:** the first-party session substrate is a **partial prerequisite** for, and a **superset of**, the OIDC path. The OIDC-impl ADR's `ClaimsBackedTenantContext` still needs a session-establishment mechanism to hold the validated principal across requests (cookie or token). If CIC rules BUILD (first-party), the OIDC-impl ADR becomes "one more authentication scheme feeding the same first-party session machinery" rather than a parallel universe. This SIMPLIFIES the OIDC-impl ADR. The earlier scoping doc's existence is also evidence that the fleet has already been circling this — the session gap is the un-named half of that scoping.

### 1.5 WS-E magic-link substrate is already specified (not yet built)

`w60-collaboration-phase4-stage06-handoff.md` specifies the magic-link substrate: single-use OR 24h-time-limited JWT, token store in Bridge (in-memory v1; redis/db prod), audit events `TenantMagicLinkIssued` + `TenantMagicLinkConsumed` following the W#18 `VendorMagicLinkIssued` precedent in `kernel-sync`. **The magic-link consume step is itself a session-establishment event** — validate single-use token → create session. This is the same session machinery as login + verify-email. The session substrate ADR should own the session-creation half; WS-E owns the token-issuance + email-delivery half. The current ONR WS-E heartbeat already flags this as "H-WSE-2 gap" — this ADR resolves it.

---

## 2. Build-vs-buy comparison (the core architectural fork)

### 2.1 Option A — First-party session (BUILD) — RECOMMENDED

In-house session issuance after authentication. Two viable mechanisms (D2, sec-eng to ratify):

- **A1 — Cookie session (RECOMMENDED sub-option):** ASP.NET Core `AddAuthentication().AddCookie()`. Post-auth, `HttpContext.SignInAsync` issues an HttpOnly+Secure+SameSite cookie carrying an opaque session id (server-side session store) OR an encrypted claims payload. Bridge already serves React same-origin in prod → this is the canonical **Backend-for-Frontend (BFF)** pattern, which 2026 guidance explicitly recommends for first-party SPAs over browser-held bearer tokens (cookies get HttpOnly/SameSite XSS+CSRF protection that localStorage cannot).
- **A2 — First-party JWT + refresh rotation:** Bridge issues its own short-lived (5-15min) access JWT (signed by the existing `IOperationSigner` Ed25519 per ADR 0046, or a dedicated key) + a rotating refresh token (family-tracked for theft detection), stored in an HttpOnly cookie. More moving parts; better for a future mobile/Tauri client that can't hold a cookie cleanly.

**Fit with the MVP flows:**
- **Verify-email-completion** → "now logged in": after `VerifyEmail` succeeds, call `SignInAsync` directly. No redirect. Natural fit. (Closes the W#79 gap.)
- **WS-E magic-link**: validate single-use token → `SignInAsync`. No redirect. Natural fit.
- **Password login (post-ADR-0097)**: `/auth/login` → `IPasswordHasher.VerifyHashedPassword` (ADR 0097) → `SignInAsync`. Natural fit.

**Pros:** zero recurring cost; zero vendor lock-in; aligns with local-first/self-hosted/MIT ethos; the passwordless flows fit naturally (no OIDC redirect impedance); reuses existing antiforgery wiring; CIC retains full control of the security surface. **Cons:** the fleet owns the security surface (session-fixation, rotation, revocation) — but this is exactly the sec-eng dual-council's job, and the surface is well-trodden ASP.NET Core territory.

### 2.2 Option B — Third-party IdP (BUY)

Offload authentication to Okta / Auth0 / Entra ID / Clerk / Keycloak (self-hosted). The orphaned `MockOktaService` indicates this was the original intent.

**Fit assessment:**
- **OIDC redirect impedance with passwordless flows.** OIDC is a redirect-based browser dance (`/authorize` → IdP → callback → token exchange). The MVP-gating flows — verify-email-completion and magic-link-consume — are *already* link-click → session events; forcing them through an OIDC redirect is architecturally awkward (you'd be wrapping a magic link inside an OIDC authorization request, or running the IdP's own magic-link extension — which, per current research, is NOT a built-in Keycloak realm feature and requires an extension). The 2026 magic-link research is explicit: link-click validates a single-use token and creates a session directly; OIDC redirect is not the ideal path for self-hosted magic-link.
- **Recurring cost + lock-in.** SaaS IdPs (Okta/Auth0/Entra/Clerk) carry per-MAU pricing and migrate auth flows out of the codebase — direct conflict with the MIT-open-source, self-hostable product posture (a self-hosting customer would need their own IdP subscription).
- **Tier-2 framing.** Per the three-tier slotting vocabulary, a third-party IdP is a **`category-provider` (Tier-2)** — a bounded vendor swap behind a category interface, ship Mock impl FIRST + real vendor conditional on env-var (ADR 0096 discipline). Crucially: **even if CIC wants BYO-OIDC support, the first-party session substrate must exist anyway** to (a) hold the session post-OIDC-token-validation and (b) serve the passwordless flows that don't go through OIDC. So "buy" does not eliminate "build" — it adds a vendor lane on top of it.

**Pros:** offloads MFA/social-login/SSO/password-reset UX; enterprise customers may already have an IdP. **Cons:** recurring cost; lock-in; redirect impedance with the MVP-gating passwordless flows; conflicts with self-hostable MIT posture; does not remove the need for first-party session machinery.

### 2.3 Recommendation

**BUILD first-party session (Option A, sub-option A1 cookie/BFF for v1), with a Tier-2 BYO-OIDC `category-provider` as a deferred, opt-in add-on (Option B folded in later via the OIDC-impl ADR).**

Reasoning: (1) the two MVP-gating flows are passwordless link-click → session events that fit first-party naturally and fit OIDC redirect badly; (2) Bridge is already a same-origin BFF, the canonical first-party SPA pattern; (3) zero recurring cost + zero lock-in + self-hostable aligns with the product's MIT/local-first identity; (4) first-party session machinery is unavoidable *even if* a vendor IdP is later added, so it is the correct foundation regardless of the eventual BUY decision; (5) ADR 0097 (Argon2id) + first-party `/auth/login` is the shortest path to a real launch-blocking login. The OIDC-impl scoping doc's `ClaimsBackedTenantContext` is then re-framed as an additional authentication scheme feeding this same session, simplifying that future ADR.

---

## 3. Security surface (sec-eng dual-council territory)

Common to both options; sec-eng MANDATORY at ADR time (this is a Halt 9-class crypto/auth substrate, like ADR 0097).

| # | Surface | Requirement |
|---|---|---|
| S1 | **Session fixation** | Regenerate the session identifier on every privilege change — post-login, post-verify-email, post-magic-link-consume. Never carry a pre-auth session id into the authenticated session. |
| S2 | **CSRF** | The existing `X-XSRF-TOKEN` antiforgery (Program.cs:497) MUST cover ALL cookie-authenticated mutating routes. Cookie sessions are CSRF-exposed; bearer-in-Authorization-header is not — if A1 (cookie), antiforgery is non-negotiable. |
| S3 | **Cookie flags** | `HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Lax` (minimum; `Strict` preferred for first-party same-origin). No session material readable by JS. |
| S4 | **Token entropy** | Session ids / single-use tokens from a CSPRNG (`RandomNumberGenerator.GetBytes`), ≥128 bits. The mock already uses 24-32 random bytes for auth codes — match or exceed. |
| S5 | **Refresh / rotation** (if A2 JWT) | Rolling refresh with token-family tracking; re-use of a rotated refresh token → revoke the whole family + force re-login (theft detection). Access token 5-15min; refresh 24h-30d (shorter for a financial app). |
| S6 | **TTL / idle timeout** | Absolute session lifetime + sliding idle timeout. Financial-app posture argues for short (e.g. 8-12h absolute, 30-60min idle). Operator-configurable. |
| S7 | **Logout / revocation** | Server-side session invalidation (not just cookie-clear) so a stolen cookie dies on logout. Requires a server-side session store (favors A1 opaque-id over A2 stateless-JWT, OR a JWT denylist). |
| S8 | **Multi-tenant session isolation** | A session issued for tenant A MUST cross-check the subdomain-resolved tenant (`TenantSubdomainResolutionMiddleware` / `IBrowserTenantContext`) against the session's bound tenant — ADR 0091 R2 invariant 2.2 (tenant cross-check). Mismatch → 401 + audit. This is the `ITenantContext` tie-in (§4d). |
| S9 | **Single-use-token atomicity** | The verify-email + magic-link consume path MUST be atomic compare-and-delete (no check-then-use TOCTOU). Mirrors W#79 verify-email + W#60 P4 magic-link discipline. Token store must support atomic remove-returning-value. |
| S10 | **Audit emission** | New `AuditEventType` constants: `Auth.SessionEstablished`, `Auth.SessionEstablished.MagicLink`, `Auth.SessionEstablished.VerifyEmail`, `Auth.LoginFailed`, `Auth.SignedOut`, `Auth.SessionRevoked`, `Auth.SessionTenantMismatch` (S8 trigger). Login-failure throttling/lockout (brute-force defense) feeds these. |
| S11 | **Brute-force / rate-limit** | `/auth/login` needs rate-limiting + (optional) lockout-after-N-failures. ASP.NET Core rate-limiting middleware. CAPTCHA gate is a separate Tier-2 decision (shipyard#128 surfaced Turnstile). |

---

## 4. Integration points

- **(a) W#79 password login (post-ADR-0097):** new `/auth/login` route (Bridge) → resolve user → `IPasswordHasher<TUser>.VerifyHashedPassword` (ADR 0097 Argon2id substrate, Step 1/2) → on success, `SignInAsync` (A1) or issue first-party JWT (A2). This is the route that does not yet exist. ADR 0097 verifies the credential; THIS ADR establishes the session. Both required for login.
- **(b) W#79 verify-email completion ("now logged in"):** after `VerifyEmail` succeeds, the same session-establishment call. Closes the "verified but cannot log in" dead-end. Optionally: auto-login on verify, or redirect to `/auth/login` (CIC UX call — recommend auto-login for MVP friction reduction, with S1 fixation regen).
- **(c) WS-E magic-link verify:** validate single-use token (S9 atomic) → session-establishment call. The session substrate owns session-create; WS-E owns token-issue + email-deliver. Audit `Auth.SessionEstablished.MagicLink`.
- **(d) Existing `AuthenticatedTenantPolicy` / `ITenantContext` consumption:** the session populates `HttpContext.User` with the `sub` claim that `IdentityEndpoints.TryResolve` and `RequireAuthenticatedUser()` already expect. The session's bound tenant must satisfy ADR 0091 R2 invariant 2.2 (cross-check vs subdomain). **Consumption-site interface choice:** per `[[itenantcontext-consumption-qualification]]`, pick the **`Sunfish.Foundation.Authorization.ITenantContext` sum-interface FACADE** (NOT the `Foundation.MultiTenancy` narrowed variant) until ADR 0091 Step 3 narrows. A production `SessionTenantContext` (replacing `DemoTenantContext`) implements the facade and reads from the established session — same single-concrete-class invariant as ADR 0091 R2 §2.3 / the OIDC-impl scoping's `ClaimsBackedTenantContext`. **These two production-context classes should be unified** — the session substrate produces the principal; the context class reads it. Recommend one `SessionBackedTenantContext` that the OIDC path also feeds (§1.4).

---

## 5. Proposed session-substrate ADR

### 5.1 Title + slot

**`ADR 0099 — First-Party Session Establishment Substrate`** (slot: highest current ADR on main is 0096; 0097 PasswordHasher + 0098 Block-Naming are Accepted/merging but not yet on disk in `docs/adrs/`; verify slot at drafting kickoff — likely **0099 or 0100**). Title names the OUTCOME (session establishment) + the posture (first-party). Subtitle could note "(cookie/BFF; OIDC-scheme-extensible)".

### 5.2 Outline

1. **Context** — the gap (§1); ADR 0097 verifies credentials but nothing establishes a session; W#79 signup/verify ship but no login; magic-link consume needs session-create.
2. **Decision** — BUILD first-party session (cookie/BFF v1) per CIC ruling; vendor IdP deferred as Tier-2 BYO-OIDC `category-provider`.
3. **Mechanism** — `AddAuthentication().AddCookie()` + `app.UseAuthentication()` (before `UseAuthorization`); `ISessionEstablisher` abstraction with three call sites (login / verify-email / magic-link); server-side session store (in-memory v1 → SQLite/Postgres prod, mirroring the JournalStore pattern).
4. **Production `SessionBackedTenantContext`** — replaces `DemoTenantContext`; single concrete class implementing the Authorization sum-interface facade; reads the established principal; satisfies ADR 0091 R2 §2.2 + §2.3 invariants; unified with the OIDC-impl scoping's `ClaimsBackedTenantContext`.
5. **Security surface** — §3 S1-S11 folded in as ratified requirements.
6. **Audit** — §3 S10 `AuditEventType` constants.
7. **Demo-seam disposition** — `DemoTenantContext` → `IsDevelopment()`-gated (Option β/γ from the OIDC scoping doc D8); production build registers `SessionBackedTenantContext`.
8. **MockOktaService disposition** (D3) — retire OR repurpose as the dev backing for a future BYO-OIDC test. Recommend: keep under dev-only, mark as future-OIDC test scaffold, do NOT wire into v1.
9. **Out-of-scope-but-flagged** — OIDC-scheme integration (folds the OIDC-impl scoping doc); Tauri/mobile session (cookie doesn't fit a native shell cleanly — A2 JWT lane); Phase 5 mesh auth.
10. **Implementation roadmap** — §5.4 PR decomposition.

### 5.3 Council halt-conditions (gate at ADR-ratify + Step-1 implementation)

- **H1 (sec-eng MANDATORY)** — session-fixation regeneration on privilege change (S1) is specified and tested.
- **H2 (sec-eng MANDATORY)** — cookie flags HttpOnly+Secure+SameSite (S3) + CSRF coverage of all cookie-auth mutating routes (S2).
- **H3 (sec-eng MANDATORY)** — single-use-token atomicity (S9) is compare-and-delete, no TOCTOU.
- **H4 (sec-eng MANDATORY)** — multi-tenant session isolation cross-check (S8 / ADR 0091 R2 §2.2) at the pipeline boundary.
- **H5 (sec-eng MANDATORY)** — server-side revocation (S7); logout invalidates server-side, stolen cookie dies.
- **H6 (.NET-arch)** — `UseAuthentication` ordered before `UseAuthorization`; `SessionBackedTenantContext` single-instance-per-scope across all facade interfaces (ADR 0091 R2 §2.3).
- **H7 (.NET-arch)** — `ISessionEstablisher` abstraction is the SINGLE session-create seam (login + verify + magic-link all route through it; no divergent session creation).
- **H8 (CIC)** — build-vs-buy verdict ratified; mechanism (cookie A1 vs JWT A2) ratified; MockOktaService disposition ratified.

### 5.4 Rough PR decomposition

| PR | Scope | Council | Effort |
|---|---|---|---|
| **PR 1** | ADR 0099 drafted + dual-council reviewed + ratified | sec-eng + .NET-arch MANDATORY | ~12-16h |
| **PR 2** | Session substrate: `AddCookie` + `app.UseAuthentication()` + `ISessionEstablisher` + in-memory session store + `SessionBackedTenantContext` (replaces DemoTenantContext, `IsDevelopment`-gated) | sec-eng SPOT-CHECK | ~8-12h |
| **PR 3** | `/auth/login` route + ADR-0097 `IPasswordHasher` verify wiring + login audit + rate-limit | sec-eng SPOT-CHECK (pattern-009 if new route) | ~5-8h |
| **PR 4** | verify-email-completion → session (W#79 closure) + magic-link-consume → session (WS-E tie-in) | sec-eng SPOT-CHECK | ~5-8h |
| **PR 5** | Production session store (SQLite/Postgres-backed) + revocation/logout + idle-timeout | sec-eng SPOT-CHECK | ~6-10h |

Total ~36-54h across ADR + 4 impl PRs. Engineer claim-beacon protocol applies (`[[substrate-claim-beacon-protocol]]`) — substrate-tier.

---

## 6. Open questions (for CIC + councils)

### For CIC (build-vs-buy ruling)
1. **D1** — BUILD first-party (ONR recommendation) vs BUY third-party IdP vs hybrid (first-party + deferred BYO-OIDC Tier-2)?
2. **D2** — cookie/BFF session (A1, ONR recommendation) vs first-party JWT + refresh rotation (A2)?
3. **D3** — retire `MockOktaService`, or keep as dev-only future-OIDC test scaffold (ONR: keep, dev-gated, do not wire v1)?
4. **D5** — title + ADR slot (ONR: `ADR 0099 — First-Party Session Establishment Substrate`).
5. Verify-email UX: auto-login on verify (ONR recommendation, MVP friction) vs redirect to `/auth/login`?

### For security-engineering council
6. **D4** — session store backing for v1: in-memory (simplest) vs SQLite-from-the-start (revocation-capable)? ONR: in-memory v1 + SQLite in PR 5, matching the JournalStore in-memory→backed progression.
7. Idle-timeout + absolute-lifetime defaults for a financial app (S6)?
8. Login rate-limit + lockout policy (S11); CAPTCHA gate (shipyard#128 Turnstile) — in this ADR or separate?

### For .NET-architect council
9. `ISessionEstablisher` shape + unification of `SessionBackedTenantContext` with the OIDC-impl scoping's `ClaimsBackedTenantContext` (§4d).
10. Tauri/mobile session lane (A2 JWT) — in-scope-flagged here or deferred to a sibling ADR?

---

## 7. Sources cited

### Primary (first-party — code + ratified ADRs/rulings; retrieved 2026-05-29)
1. `signal-bridge/MockOktaService/Program.cs` — mock OIDC IdP endpoints (discovery/authorize/token/userinfo/introspect/jwks/logout); PKCE; no password validation.
2. `signal-bridge/MockOktaService/Services/MockTokenGenerator.cs` + `IMockTokenGenerator.cs` + `MockOktaService.csproj` — JWT issuance; standalone `Sdk.Web` service.
3. `signal-bridge/Sunfish.Bridge/Program.cs` — auth region (AddAuthorization :486/:631; UseAuthorization :173; AddAntiforgery :497); NO AddAuthentication.
4. `signal-bridge/Sunfish.Bridge/Authorization/{DemoTenantContext,AuthenticatedTenantPolicy,AccountantPolicy}.cs` — demo seam + policies.
5. `signal-bridge/Sunfish.Bridge/Features/Identity/IdentityEndpoints.cs` — reads `ctx.User.FindFirst("sub")` from an unpopulated principal.
6. `shipyard/packages/foundation-authorization/{ITenantContext,ICurrentUser,IAuthorizationContext}.cs` — sum-interface facade.
7. `coordination/inbox/admiral-attest-2026-05-27T1645Z-shipyard-167-adr-0097-rev-2-dual-council-green.md` — ADR 0097 Argon2id PasswordHasher Accepted; Step 1/2 roadmap.
8. `coordination/inbox/admiral-ruling-2026-05-28T1630Z-w79-closed-main-fixed-pattern-009-ratified.md` — W#79 CLOSED (signup/verify/resend; no login route).
9. `coordination/inbox/research-mvp-feature-priority-2026-05-29T0205Z.md` — MVP route inventory (no `/auth/login`); ADR 0097 named as the launch gate (session gap un-named).
10. `shipyard/icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md` — magic-link substrate (single-use/24h JWT, token store, audit events).
11. `shipyard/icm/01_discovery/research/production-oidc-impl-adr-scoping-2026-05-20.md` — sibling OIDC-impl ADR scoping + ADR 0091 R2 invariants (§2.1-2.3).

### Secondary (commentary / best-practice; retrieved 2026-05-29)
12. Auth0 — "Cookies, Tokens, or JWTs? The ASP.NET Core Identity Dilemma" — cookie recommended for first-party SPAs; BFF for security-critical SPAs. <https://auth0.com/blog/cookies-tokens-jwt-the-aspnet-core-identity-dilemma/>
13. Red-gate Simple Talk — "ASP.NET Core Cookie Authentication: Setup, JWT Claims, and Revocation" — HttpOnly/Secure/SameSite=Strict; server-side revocation. <https://www.red-gate.com/simple-talk/development/dotnet-development/using-auth-cookies-in-asp-net-core/>
14. Steve Bang — "Refresh Token Rotation with ASP.NET Core Auth" — rotation + family-tracking theft detection. <https://www.steve-bang.com/blog/refresh-token-rotation-aspnet-core>
15. SuperTokens — "Magic Links" — single-use short-lived token → validate (unused/unexpired) → create session; no OIDC redirect. <https://supertokens.com/blog/magiclinks>
16. The Main Thread — "Quarkus/Keycloak Passwordless Magic Links" — magic-link is a Keycloak extension, NOT a built-in realm feature (OIDC redirect not the ideal self-hosted path). <https://www.the-main-thread.com/p/passwordless-login-quarkus-magic-links-keycloak>

### Tertiary (standards; transitive)
17. OWASP Session Management Cheat Sheet — session-id regeneration on privilege change; entropy; cookie flags; idle+absolute timeout (folded into §3 S1/S3/S4/S6).
18. ASP.NET Core 11 `Microsoft.AspNetCore.Authentication.Cookies` / antiforgery docs (transitive; mechanism shape).

---

## 8. What ONR does next

- Deliverable complete (this doc). Pointer beacon filed: `onr-status-2026-05-29T<HHMM>Z-session-substrate-research.md`.
- Open questions (§6) surfaced inline per `[[questions-via-inbox]]` — Admiral routes the build-vs-buy to CIC + the security/arch questions to the councils at ADR-drafting kickoff.
- Stand by for CIC build-vs-buy ruling; on ratification, available to author the ADR 0099 draft (separate work item, ~12-16h, dual-council MANDATORY).

— ONR, 2026-05-29T03:00Z
