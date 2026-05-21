# ONR research — Production OIDC-impl ADR scoping (2026-05-20)

**Requester:** Admiral (per `admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` item #5)
**Authored by:** ONR
**Authored at:** 2026-05-20T12-08Z
**Status:** draft (scoping research only; NOT a draft ADR; ratification pending CIC review of decision-area framing)

---

## Scope of investigation

- **In scope:** scoping the eventual production OIDC-impl ADR — its title, decision areas, open questions, estimated complexity envelope, dependency posture. Identifies WHICH design choices need ratification + the order they should be sequenced.
- **Out of scope:** drafting the ADR itself (separate work item once scoping confirms scope envelope); choosing a specific OIDC provider (decision area D1 below; CIC ratification); Phase 5 peer-sync auth design (ADR 0061 Headscale; separate workstream); accountant Anchor install + Headscale device registration (W#60 P5 future).
- **Authoritative sources consulted:** ADR 0091 Rev 2 (Accepted) §"Out-of-scope-but-flagged > Production OIDC-impl ADR (future)"; `DemoTenantContext.cs` (current dev seam); cohort-1 `AuthenticatedTenantPolicy.cs`; cohort-2 PR 3 CSRF + audit pattern (`HandleCreateWorkOrderAsync`); W#60 P4 hand-off (Tauri Stronghold + multi-role auth + magic-link); W#18 vendor magic-link precedent; ADR 0046 (IOperationSigner); ADR 0061 (Headscale mesh; Phase 5 future).
- **Success looks like:** Admiral + CIC have a clear scope envelope for the production OIDC-impl ADR (likely numbered ADR 0093+ given current ADR slot use); decision areas are enumerated with crisp open questions; the ADR can be drafted as a separate research work item with a known target shape.

---

## TL;DR

1. **Proposed ADR title:** `ADR 0XXX — Production OIDC Authentication for Sunfish` (or "Sunfish Production Identity"; "Production Claims-Backed ITenantContext"). ONR recommends title that names the OUTCOME (claims-backed ITenantContext replacement) rather than the mechanism (OIDC) — the ADR's purpose is replacing `DemoTenantContext`, OIDC is one viable mechanism among several.

2. **Three pre-staged invariants** from ADR 0091 R2 §"Out-of-scope-but-flagged > Production OIDC-impl ADR (future)" carry forward verbatim. These define the security floor; the future ADR ratifies the MECHANISM that satisfies them.

3. **10 decision areas** are surfaced (§3): OIDC provider choice; multi-tenant claims shape; concrete-class structure; token + refresh handling; session signer integration; post-auth audit emission; DI registration; demo seam removal timing; Tauri shell auth flow; magic-link interaction.

4. **Complexity estimate: HIGH** — production OIDC touches Bridge auth + frontend auth + Tauri shell auth + audit + audit-event-type catalog + (potentially) Headscale mesh auth in Phase 5. ADR effort ~15-20h drafting; implementation effort 20-40h depending on provider choice + scope decisions.

5. **Dependency posture** — gated on ADR 0091 Step 2.0 landing (Step 2.0 is in-flight per queue item #3 research). NOT gated on cohort-3 or cohort-2 close-out. Best timing: after W#60 P3 PASS + before W#60 P4 PR 2 (Accountant Bridge role) starts.

6. **Phase 5 cross-reference** — production OIDC interacts with Phase 5 peer-sync auth (ADR 0061 Headscale mesh). The ADR proposed here is for Bridge-side production identity; Phase 5 needs mesh-bound identity (likely an extension or sibling ADR). Scoping flags this interaction; the proposed ADR explicitly does NOT cover mesh auth.

7. **CIC ratification needed** on title + scope envelope + complexity tier before ADR drafting begins. Filing as separate work item; this scoping doc is the input.

---

## 1. Why this ADR is needed now

ADR 0091 Rev 2 §"Out-of-scope-but-flagged > Production OIDC-impl ADR (future)" deferred the production-impl design to a future ADR with three pre-staged invariants. As cohort-1 + cohort-2 + W#60 P4 land, the demo seam (`DemoTenantContext`) becomes increasingly load-bearing:

- **Cohort-1 + cohort-2 (shipped)** — `AuthenticatedTenantPolicy` requires only `RequireAuthenticatedUser()`. The demo seam returns hardcoded `Roles = [Manager]` + `HasPermission => true`. All cohort-shipped endpoints work because the demo seam grants everything.

- **W#60 P4 PR 2 (planned per queue item #2 research)** — Accountant role enforcement requires real role claims. The demo seam can't enforce `role == accountant` without becoming a config-driven multi-role seam (Path B per W#60 P4 research §1 PR 2), which is essentially the production replacement.

- **W#60 P4 PR 3 (planned)** — Magic-link tenant portal needs JWT validation + claims propagation. Without production OIDC, magic-link is the ONLY identity surface; with production OIDC, magic-link becomes one of multiple auth flows (OIDC for staff + magic-link for tenants + magic-link for CPA).

- **ADR 0091 Step 2.0 (in flight)** — narrows the DbContext constructor to `Foundation.MultiTenancy.ITenantContext`. After Step 2.0 lands, the production OIDC impl provides `Foundation.MultiTenancy.ITenantContext` (the Tenant + IsResolved surface) + `ICurrentUser` (UserId + Roles) + `IAuthorizationContext` (HasPermission) — all three from the same concrete class per ADR 0091 R2 amendment 1 sum-interface contract.

**Trigger timing:** the production OIDC-impl ADR should land BEFORE W#60 P4 PR 2 (Accountant Bridge role) opens. Otherwise PR 2 ships an interim multi-role demo seam that has to be re-replaced.

---

## 2. Pre-staged invariants from ADR 0091 R2

The production OIDC-impl ADR MUST satisfy these three invariants (named verbatim in ADR 0091 R2 §"Out-of-scope-but-flagged"):

### 2.1 Same-token invariant

> `UserId` (from `ICurrentUser`) + `Roles` (from `ICurrentUser`) + the input to `HasPermission(string)` (on `IAuthorizationContext`) MUST come from the SAME validated token instance. Reading `sub` from one source and `roles` from another (e.g., a session cookie + a bearer token without cross-binding) is the textbook confused-deputy seam.

**Implication:** the production impl reads from ONE token; if multiple identity surfaces are involved (e.g., session cookie + access token + refresh token), they must cross-bind via `aud` + `tid` + `sid` claims so all three derive from the same originating validation event.

### 2.2 Tenant cross-check invariant

> The `TenantId` on `Foundation.MultiTenancy.ITenantContext` MUST be cross-checked against the `tid` claim on the same validated token. Any request where the subdomain-resolved tenant disagrees with the OIDC-asserted tenant MUST be rejected at the pipeline boundary.

**Implication:** Bridge resolves tenant via subdomain (`TenantSubdomainResolutionMiddleware` per ADR 0091 R2 A0 audit) AND from the OIDC token's `tid` claim. The two MUST match; mismatch → 401 + `AuthorizationDenied_TenantMismatch` audit event.

### 2.3 Single-concrete-class invariant

> A single concrete class MUST implement all three interfaces (`Foundation.MultiTenancy.ITenantContext` + `ICurrentUser` + `IAuthorizationContext`). This matches the Step 1 facade pattern + eliminates the same-token invariant by construction. If the future ADR proposes three separate classes, it MUST also propose the cross-binding mechanism that ensures all three read from the same validated token — and the security-engineering council will block on that mechanism.

**Implication:** one concrete class `ClaimsBackedTenantContext` (or similar name) implements all four interfaces (the three above plus the facade `Foundation.Authorization.ITenantContext`). Registered via `AddSunfishTenantContext<ClaimsBackedTenantContext>` per ADR 0091 R2 amendment A1 helper; the startup assertion enforces same-instance resolution across all four interface types.

---

## 3. Decision areas (10)

### D1. OIDC provider choice

**Question:** which OIDC provider does Sunfish integrate with by default?

**Options:**

| Provider | Posture | Pro | Con |
|---|---|---|---|
| **Self-hosted Keycloak** | Open-source; deployable in same docker-compose as Sunfish Bridge | Fully sovereign; no external dependency; aligns with local-first | Operator runs Keycloak; backup + upgrade burden; SSO config UX |
| **Auth0** | SaaS; bring-your-own tenant | Mature SSO; broad social-IdP support; cheap at < 7000 active users | External dependency; vendor lock for auth flows; CIC pricing oversight |
| **Microsoft Entra ID** (formerly Azure AD) | SaaS; B2C tier | Best-in-class enterprise IdP; SCIM provisioning; tight Microsoft 365 integration | Microsoft lock-in; complex tier pricing; less small-tenant-friendly |
| **Okta** | SaaS | Enterprise pedigree; broad protocol support | Most expensive of SaaS options; complex for small fleet |
| **Bring-your-own (BYO IdP)** | Sunfish supports any OIDC-compliant IdP; per-tenant configuration | Maximum flexibility; matches local-first ethos | Tenant operator burden; SSO config UX must support arbitrary IdPs |

**ONR provisional recommendation:** **Self-hosted Keycloak as default + BYO IdP support as opt-in.** Aligns with local-first sovereignty; gives small tenants a working default; large tenants can BYO their existing IdP.

**Open question for CIC:** confirm or amend the default choice. SaaS providers (Auth0/Entra/Okta) are off-the-table if local-first sovereignty is non-negotiable.

### D2. Multi-tenant claims shape

**Question:** how does the OIDC token convey tenant identity?

**Standard claims to use:**
- `sub` — stable user identifier (within the tenant)
- `tid` — tenant identifier (Microsoft Entra precedent; widely understood)
- `aud` — audience; should be `sunfish-bridge` or per-tenant `sunfish-<tenant-slug>`
- `roles` — array of role strings (`accountant`, `cpa`, `tenant`, `owner`, `spouse`, etc.)
- `iss` — issuer; for Keycloak default, `https://<keycloak-host>/realms/<tenant-realm>`

**Open question for sec-eng council:** should `tid` claim be REQUIRED on every token, OR derived from `iss` realm? If `iss` is per-tenant, `tid` is redundant; if `iss` is shared (multi-tenant realm), `tid` is required.

### D3. Concrete-class structure

**Question:** single concrete class implementing all four interfaces (per ADR 0091 R2 invariant 2.3) — what's the resolution flow?

**Proposed shape:**

```csharp
namespace Sunfish.Bridge.Authorization;

public sealed class ClaimsBackedTenantContext :
    Foundation.MultiTenancy.ITenantContext,
    Foundation.Authorization.ICurrentUser,
    Foundation.Authorization.IAuthorizationContext,
    Foundation.Authorization.ITenantContext    // facade
{
    private readonly ClaimsPrincipal _principal;
    private readonly ITenantSubdomainResolver _subdomainResolver;
    private readonly TenantMetadata _resolvedTenant;

    public ClaimsBackedTenantContext(
        IHttpContextAccessor httpContextAccessor,
        ITenantSubdomainResolver subdomainResolver)
    {
        _principal = httpContextAccessor.HttpContext?.User
            ?? throw new InvalidOperationException("ClaimsPrincipal unavailable");
        _subdomainResolver = subdomainResolver;

        // Resolve tenant from subdomain
        var subdomainTenant = _subdomainResolver.Resolve(httpContextAccessor.HttpContext);

        // Resolve tenant from tid claim
        var tidClaim = _principal.FindFirst("tid")?.Value;

        // Invariant 2.2: cross-check
        if (tidClaim != null && !string.Equals(tidClaim, subdomainTenant.Id.Value, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Tenant subdomain does not match OIDC tid claim. " +
                "See ADR 0091 R2 production OIDC-impl invariant 2.2.");
        }

        _resolvedTenant = subdomainTenant;
    }

    // Foundation.MultiTenancy.ITenantContext
    public TenantMetadata? Tenant => _resolvedTenant;
    public bool IsResolved => true;

    // Foundation.Authorization.ICurrentUser
    public string UserId => _principal.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("sub claim missing");
    public IReadOnlyList<string> Roles => _principal.FindAll("roles").Select(c => c.Value).ToList().AsReadOnly();

    // Foundation.Authorization.IAuthorizationContext
    public bool HasPermission(string permission) =>
        _principal.FindAll("permission").Any(c => c.Value == permission);

    // Facade
    public string TenantId => _resolvedTenant.Id.ToString();
}
```

**Open question for .NET-architect council:** confirm or amend the resolution flow. Alternative: defer tenant resolution to middleware-time (set in `HttpContext.Items`) and read from there in the ctor.

### D4. Token + refresh handling

**Question:** how are access tokens + refresh tokens managed?

**Decision points:**
- **Storage:** Tauri Stronghold (per W#60 P4 PR 1) holds the access token; refresh token goes alongside under a separate key (`bridge-refresh-token`)
- **Token lifetime:** access token 15-60min (operator-configurable); refresh token 30 days
- **Rotation:** rolling refresh (each refresh issues a new refresh token; old one invalidated); detect re-use as compromise indicator
- **Revocation:** OIDC revocation endpoint integration; revoke-on-logout from Tauri shell

**Open question for sec-eng council:** rolling refresh vs absolute refresh lifetime? Rolling is more secure (compromise window short) but adds complexity (token store keeps changing); absolute is simpler (refresh until expiry) but a leaked refresh is valid for 30 days.

### D5. Session signer integration (existing `IOperationSigner` per ADR 0046)

**Question:** does the production OIDC impl re-use `IOperationSigner` (existing Ed25519 signer) for ANYTHING, OR is OIDC signing independent?

**Current state:** `IOperationSigner` signs operations + magic-link JWTs (per W#18 + W#60 P4 P3 spec). OIDC tokens are signed by the IdP (Keycloak/Auth0/Entra), not by `IOperationSigner`.

**Decision:** OIDC token signing is IdP-owned (independent from `IOperationSigner`). Bridge's role is VERIFICATION (using IdP's JWKS endpoint), not signing.

**Cross-reference:** magic-link tokens continue to be signed by `IOperationSigner` (Bridge-issued; not IdP-issued). The two coexist; the user's session may carry one OIDC token + one magic-link JWT for different audiences.

### D6. Post-auth audit emission

**Question:** what `AuditEventType` constants are needed for auth events?

**Proposed additions:**

```csharp
// New under Sunfish.Kernel.Audit.AuditEventType
public const string UserAuthenticated         = "Auth.UserAuthenticated";       // successful sign-in
public const string UserSignedOut             = "Auth.UserSignedOut";
public const string TokenRefreshed            = "Auth.TokenRefreshed";
public const string TokenRefreshRejected      = "Auth.TokenRefreshRejected";    // rolling-refresh re-use detection
public const string AuthorizationDenied       = "Auth.AuthorizationDenied";     // generic
public const string AuthorizationDenied_TenantMismatch = "Auth.AuthorizationDenied.TenantMismatch";  // invariant 2.2 trigger
public const string RoleEvaluated             = "Auth.RoleEvaluated";           // per-policy decision (high-volume; debug-only)
```

**Open question for sec-eng council:** is `RoleEvaluated` (per-policy decision) too high-volume to emit for every request? Likely yes; emit only on DENIAL OR at sampling rate.

### D7. DI registration

**Question:** how does production OIDC register at startup?

**Proposed shape:**

```csharp
// In Sunfish.Bridge.Program.cs (replaces AddSunfishTenantContext<DemoTenantContext>)
services.AddSunfishTenantContext<ClaimsBackedTenantContext>();

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = config["Oidc:Authority"];    // e.g., "https://keycloak.example.com/realms/sunfish"
        options.Audience = config["Oidc:Audience"];      // e.g., "sunfish-bridge"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // JWKS fetched from .well-known/jwks.json
        };
    });
```

The `AddSunfishTenantContext<ClaimsBackedTenantContext>` helper (from ADR 0091 R2 A1) ensures the four interfaces resolve to the same instance per scope.

### D8. Demo seam removal timing

**Question:** when does `DemoTenantContext` get removed?

**Options:**
- **Option α — Big-bang removal in the production OIDC PR.** Delete `DemoTenantContext` + `DemoAuthWarningFilter` in the same PR that ships `ClaimsBackedTenantContext`.
- **Option β — Side-by-side via feature flag.** Both registered; environment variable selects `Demo` for dev, `ClaimsBacked` for prod. Demo seam removed in a follow-on cleanup PR.
- **Option γ — Per-environment registration.** `Program.cs` uses environment check (`IsDevelopment()` vs `IsProduction()`) to register one or the other. Demo seam stays in the codebase indefinitely.

**ONR provisional recommendation:** **Option β (feature flag)** for the transition; **Option γ (per-environment)** as the long-term default. The demo seam IS useful for local dev; keeping it under an `IsDevelopment()` gate is the canonical .NET pattern.

**Open question for sec-eng council:** confirm or amend. Sec-eng may want the demo seam REMOVED from any prod-built artifact; this changes the analyzer story (a `ClaimsBackedTenantContext`-only build flavor).

### D9. Tauri shell auth flow (interaction with W#60 P4 PR 1)

**Question:** how does the Tauri Stronghold credential store interact with OIDC tokens?

**Current state (per W#60 P4 PR 1):** Stronghold stores a single `bridge-token` (access token). First-launch UI redirects to `/auth/login?redirect=tauri://localhost`; Bridge issues the token; Tauri captures + stores.

**Production OIDC implications:**
- Stronghold key namespace expands: `bridge-access-token` + `bridge-refresh-token` + `bridge-id-token` (the latter for user info)
- First-launch flow: Tauri WebView opens the IdP's authorization endpoint (OIDC code flow with PKCE) → IdP redirects to `tauri://localhost?code=...` → Tauri exchanges code for tokens at the IdP's token endpoint (NOT Bridge; OIDC code flow is direct IdP↔client) → tokens stored in Stronghold → subsequent requests to Bridge use the access token as Bearer
- Logout: revoke refresh token at IdP + clear Stronghold

**Open question for .NET-architect council:** is OIDC code flow with PKCE the right Tauri flow, OR should Tauri use a Bridge-mediated flow (Bridge proxies the IdP exchange)? PKCE direct is the OIDC standard but requires Tauri to handle the IdP communication.

### D10. Magic-link interaction (W#60 P4 PR 3 + W#18 vendor pattern)

**Question:** how do magic-link JWTs (vendor + tenant + CPA) coexist with OIDC tokens?

**Decision:** they are SEPARATE auth flows. OIDC = staff identity (owner/spouse/accountant/manager); magic-link = external identity (vendor/tenant/CPA). They never appear on the same request:

- Cockpit + accountant requests carry an OIDC Bearer token; `ClaimsBackedTenantContext` resolves the user
- Magic-link requests carry a magic-link JWT (signed by `IOperationSigner`); a separate `MagicLinkTenantContext` resolves
- Bridge's middleware pipeline reads `Authorization` header; if `Bearer` scheme → OIDC; if `MagicLink` scheme (custom) → magic-link

**Audience separation:** OIDC `aud = sunfish-bridge`; magic-link `aud = vendor-portal | tenant-portal | cpa-portal`. Cross-audience token use is rejected.

**Open question for sec-eng council:** confirm the audience separation. Should magic-link tokens also use Bearer scheme with audience claim (more standard) OR a custom scheme (more visibly distinct)?

---

## 4. Open questions (consolidated; for ADR drafting)

### For .NET-architect council

1. D1 — OIDC provider choice: Keycloak self-hosted default + BYO IdP opt-in (ONR recommendation)
2. D3 — Concrete-class structure: middleware-time tenant resolution vs ctor-time?
3. D7 — DI registration shape (ASP.NET Core 11 standard JwtBearer; confirm)
4. D9 — Tauri OIDC flow: direct PKCE vs Bridge-mediated?

### For security-engineering council

1. D2 — Multi-tenant claims: `tid` required vs derived from `iss`?
2. D4 — Token + refresh: rolling refresh vs absolute lifetime?
3. D6 — `RoleEvaluated` audit emission: sampling rate or DENIAL-only?
4. D8 — Demo seam: Option β (feature flag) vs Option γ (per-environment)?
5. D10 — Magic-link scheme: Bearer with audience claim vs custom scheme?

### For CIC

1. D1 confirm/amend default OIDC provider choice
2. Title proposal (§5) confirm/amend
3. Complexity envelope acceptable (§6) — ADR ~15-20h drafting; impl ~20-40h
4. Phase 5 cross-reference framing — production OIDC ADR (proposed) covers Bridge-side; Phase 5 mesh auth is separate ADR

---

## 5. Title proposal

ONR recommends: **`ADR 0XXX — Production Claims-Backed ITenantContext (OIDC integration)`**

Rationale:
- Names the OUTCOME (claims-backed `ITenantContext` replacement) — the ADR's purpose is replacing `DemoTenantContext`
- Names the MECHANISM (OIDC) — gives readers the right mental model
- Mirrors ADR 0091 R2 title shape (`ITenantContext Divergence Resolution`) — readers immediately see the lineage

Alternatives considered:
- `ADR 0XXX — Production OIDC Authentication for Sunfish` — too mechanism-first
- `ADR 0XXX — Sunfish Production Identity` — too broad; doesn't name the surface it touches
- `ADR 0XXX — Claims-Backed ITenantContext` — drops the OIDC mechanism; ambiguous about how

ADR slot: next available after current slot use (ADR 0091 + 0092 are recent; ADR 0093+ likely available). Verify slot at ADR-drafting kickoff.

---

## 6. Complexity envelope

**ADR drafting effort:** ~15-20h
- ~3h scope-of-investigation memo
- ~5h decision-area analysis (10 areas above; ratify with councils)
- ~3h sample code surfaces (`ClaimsBackedTenantContext`, DI registration, audit events)
- ~2h migration plan from `DemoTenantContext` to `ClaimsBackedTenantContext`
- ~2h §"Out-of-scope-but-flagged" pre-staging for Phase 5 mesh auth follow-up
- ~3h council-review revision cycle (analogous to ADR 0091 R1 → R2)

**Implementation effort (Engineer):** ~20-40h
- ~4-8h Bridge OIDC integration (JwtBearer + JWKS + ClaimsBackedTenantContext)
- ~6-12h Tauri OIDC flow (PKCE code flow + Stronghold integration extension)
- ~4-8h Keycloak deployment (if Keycloak chosen as default)
- ~3-6h test fixtures + integration tests
- ~3-6h `DemoTenantContext` deprecation (feature flag OR per-environment)

**Total project:** ~35-60h (ADR + impl). Should be staged across 3-5 PRs:
- PR 1: ADR drafted + council reviewed + ratified
- PR 2: Bridge OIDC integration + `ClaimsBackedTenantContext`
- PR 3: Tauri OIDC flow (extension of W#60 P4 PR 1 Stronghold)
- PR 4: Keycloak deployment artifacts (`docker-compose.prod.yml` extension)
- PR 5: Demo seam deprecation + migration cleanup

---

## 7. Dependencies + sequencing

### Hard dependencies (block ADR drafting)

- **ADR 0091 Step 2.0 must land first** — `SunfishBridgeDbContext` constructor takes `Foundation.MultiTenancy.ITenantContext`; `ClaimsBackedTenantContext` must satisfy this. Step 2.0 is in flight per queue item #3 research.

### Soft dependencies (block ADR ratification but not drafting)

- **W#60 P4 PR 1 (Stronghold) on main** — Tauri OIDC flow extends Stronghold integration; can't ratify Tauri-side decisions without Stronghold landed.
- **W#60 P3 PASS** — Phase 4 work depends on Phase 3 acceptance; OIDC ADR fits BETWEEN P3 PASS and P4 PR 2 (Accountant role).

### Triggers for ADR drafting

- ADR 0091 Step 2.0 PR opens (signal that the substrate is contract-locked)
- W#60 P3 PASS achieved (signal that Tauri shell is production-ready)

### Triggers for ADR-blocked re-evaluation

- If CIC chooses a SaaS OIDC provider (D1), the ADR can defer Keycloak deployment artifacts to a separate workstream
- If W#60 P4 pivots to a non-OIDC auth approach, this ADR is voided

---

## 8. Out-of-scope (will become a sibling ADR if needed)

- **Phase 5 peer-sync mesh auth** — accountant Anchor install + Headscale device registration + mTLS over mesh tunnels. ADR 0061 Headscale Mesh VPN is the architectural ancestor; Phase 5 will need a dedicated ADR.
- **Decentralized identity (DID / verifiable credentials)** — out of scope; mentioned only to disclaim coverage. Future ADR if Sunfish ever needs it.
- **Federated single sign-on across multiple Sunfish tenants** — out of scope. Each tenant has its own OIDC realm.
- **Hardware-token / WebAuthn / FIDO2 enforcement** — defer to a follow-on hardening ADR if compliance demands it.
- **Tenant offboarding / data export auth** — separate concern; covered by ADR-to-be-named for tenant lifecycle.

---

## 9. Risks (ADR drafting + impl phase)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| OIDC provider choice (D1) becomes contentious; ADR drafting stalls | Medium | High (ADR delay) | ONR recommendation + provisional choice; CIC ratifies; defer SaaS-vs-self-hosted tradeoff with a "default for now; BYO for sovereignty" pattern |
| Tauri PKCE flow has UX rough edges (webview redirect handling) | Medium | Medium (UX polish) | Prototype + UX validation before ratifying D9 |
| `DemoTenantContext` removal breaks dev workflow | Low | Medium (developer friction) | Option β feature flag preserves the demo seam during transition |
| Magic-link + OIDC audience confusion | Medium | High (auth bypass) | Strict audience separation per D10; sec-eng council attestation |
| Keycloak operational burden if chosen as default | Medium | Medium (operator complexity) | Docker-compose deployment + minimal upgrade story; BYO IdP is the escape hatch |
| OIDC token storage in Tauri Stronghold conflicts with W#60 P4 PR 1 key namespace | Low | Low (mechanical fix) | Extend key namespace per D9 |
| Phase 5 mesh auth requires architectural changes to OIDC token | Medium | Medium (future ADR) | Pre-stage Phase 5 cross-reference in §"Out-of-scope-but-flagged" of the production OIDC ADR |

---

## 10. Sources cited

### Primary sources

1. `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` Rev 2 (Accepted; promoted 2026-05-19T02:40Z) — §"Out-of-scope-but-flagged > Production OIDC-impl ADR (future)" pre-stages the 3 invariants.
2. `signal-bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` — the dev seam being replaced; current shape verified 2026-05-20.
3. `signal-bridge/Sunfish.Bridge/Authorization/AuthenticatedTenantPolicy.cs` — current cohort-1 policy precedent; `RequireAuthenticatedUser()` only; production OIDC adds role binding.
4. `shipyard/icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md` — Phase 4 multi-role context (Accountant + CPA + Tenant + Magic-link).
5. `shipyard/icm/01_discovery/research/w60-p4-collaboration-track-research-2026-05-20.md` — Phase 4 research (this session's queue item #2); references this scoping work as future-ADR.

### Secondary sources

6. `shipyard/docs/adrs/0046-actor-principal-resolver.md` — `IOperationSigner` (Ed25519 signer; coexists with OIDC for magic-link signing).
7. `shipyard/docs/adrs/0061-headscale-mesh-vpn.md` — Phase 5 mesh auth (out-of-scope; future ADR).
8. `shipyard/docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md` — Bridge control-plane vs data-plane boundary; production OIDC operates in control-plane.
9. `coordination/inbox/admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` — parent directive (Item #5).

### Tertiary sources (referenced; not primary)

10. OpenID Connect Core 1.0 + Discovery 1.0 specs (transitive; standard claims like `sub`, `tid`, `aud`, `iss`).
11. ASP.NET Core 11 `Microsoft.AspNetCore.Authentication.JwtBearer` documentation (recommended DI registration shape).
12. Keycloak operations docs (deployment + realm config; transitive if D1 ratifies Keycloak default).
13. OIDC PKCE (RFC 7636) — recommended for native-app OIDC flows (Tauri).

---

## 11. What ONR does next

Returns to research queue. Per proceed-continuously discipline:

- Item #5 deliverable complete (this doc).
- File `onr-status-*-research-queue-item-5-oidc-scoping-complete.md` (open questions surfaced in §4 captured inline per `feedback_questions_via_inbox` — Admiral routes).
- Proceed to Item #6: ONR gitbutler workflow audit (self-process hygiene; final item).

— ONR, 2026-05-20T12:08Z
