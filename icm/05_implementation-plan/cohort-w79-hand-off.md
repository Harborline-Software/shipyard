# W#79 onboarding sub-cohort 1 — Stage-05 hand-off

**Authored by:** ONR (post-ADR-0095/0096-Accepted dispatch)
**Requester:** Admiral (per `admiral-directive-2026-05-25T2230Z-onr-w79-stage-05-handoff-authoring.md`)
**Authored at:** 2026-05-25T22:50Z
**Rev 2 authored at:** 2026-05-26T01:30Z (folds 3-council triple-AMBER Stage-05 verdicts + 5 Admiral architectural rulings per `admiral-directive-2026-05-26T0100Z-onr-w79-rev-2-fold-with-5-architectural-rulings.md`)
**Workstream:** W#79 — onboarding sub-cohort 1 (substrate consumption + signup/verify-email Bridge + frontend rebind)
**Status:** Stage-05 hand-off Rev 2 — pending sec-eng + .NET-arch + test-eng re-attest; Stage-06 build gated on (a) Engineer ADR 0095 Step 2 PR MERGED (`MapBootstrapEndpoints` extension + bootstrap pipeline branch); (b) Engineer ADR 0096 Step 1 PR MERGED (Tier-2 vendor substrate); (c) Stage-05 triple-council Rev 2 verdicts GREEN.

## Rev 2 — what changed vs Rev 1

Rev 1 returned triple-AMBER on 2026-05-26T00:25Z/00:40Z/00:50Z. Rev 2 absorbs 5 Admiral architectural rulings + 19 substantive amendments + ~10 minor nits. See `admiral-directive-2026-05-26T0100Z-onr-w79-rev-2-fold-with-5-architectural-rulings.md` for the canonical fold map. Summary:

- **D1** — `IOperationSigner` API: spec realigned to existing `SignAsync<VerificationTokenPayload>(...)` surface (no invented `SignVerificationToken` / `TryVerifyVerificationToken` methods).
- **D2** — `TenantRegistration` schema extended with `AdminEmailNormalized` / `EmailVerified` / `PasswordHash` + EF migration scoped to PR 0; `IUserAggregateRepository` package home = `Sunfish.Bridge.Data` (D2.a).
- **D3** — `ITenantRegistry` split into `IBootstrapTenantRegistry` (bootstrap-scope-resolvable; read-only DbContext) + `ITenantRegistry` (tenant-bound; full DbContext) — closes the transitive bootstrap-scope contradiction.
- **D4** — `/api/v1/auth/check-availability` endpoint REMOVED (enumeration-defense alignment with H2 always-202); endpoint count 4 → 3 throughout the spec.
- **D5** — Pre-tenant audit emission lands in ADR 0049 substrate with `tenant_id = SUNFISH_SYSTEM_TENANT` sentinel (D5.a covers Engineer adding sentinel + partition routing in PR 0 if ADR 0049 doesn't yet support it).

Plus: sec-eng A (PasswordHasher interface) + C (replay-window via redemption-state) + D (constant-time sham-hash on existing-email path) + H (M3/M4 production-guard factory-registered mock detection) + .NET-arch B1/B2/C1/D1/F1/K1 + ~10 minor nits + test-eng T1-T6 coverage expansion (production-guard M1-M12 + 10 ProblemDetails per-discriminator + ITenantContextSeed 6 unit tests + selector-strategy floor + env-var isolation discipline + pair-merge cascade per-step test gating).

---

## 1. Purpose + scope

W#79 is the first cohort that consumes the two substrate ADRs Accepted 2026-05-25:

- **ADR 0095 Bootstrap Context** (`packages/foundation-bootstrap/`; `IBootstrapContext` + `AddSunfishBootstrapContext<TConcrete>` + `BootstrapAndTenantMutualExclusionAssertion` IHostedService; `ITenantContextSeed` scoped-holder pattern; `RequireTenantBoundDbContext` constructor guard on `SunfishBridgeDbContext`; bootstrap pipeline branch via `app.UseWhen` + `MapBootstrapEndpoints`).
- **ADR 0096 Tier-2 Vendor-Provider Substrate** (`IEmailProvider` + `MockEmailProvider` in `packages/foundation-integrations/Email/`; `IMockVendorProvider` marker; `IMockVendorEnvVarRegistry` + `MockVendorEnvVarRegistry`; `MockProviderProductionGuardAssertion` IHostedService; `AddSunfishVendorProvider<TContract, TConcrete>` + `UseVendorProviderIfConfigured<TContract, TConcrete>` DI helpers; `ProviderCategory.Captcha = 10` + `ProviderCategory.TransactionalEmail = 11`).

W#79 sub-cohort 1 (per onboarding-ladder ruling Decision 9; W79-W83 allocated; this hand-off covers W#79 only) wires the public signup flow against both substrates: signup + verify-email Bridge endpoints (PR 0); handler-side IBootstrapContext + IEmailProvider + ICaptchaVerifier consumption (PR 1); SignupPage + VerifyEmailPage frontend rebind from mock to real Bridge (PR 2); end-to-end + contract tests (PR 3). Sub-cohort 2 (W#80) covers Surfaces A+B (full signup + verification copy + email templating + i18n + welcome-flow UX polish); sub-cohort 3 (W#81) covers Surface C (first-property wizard); sub-cohort 4 (W#82) covers Surface D (invitations); sub-cohort 5 (W#83) is polish. W#79 is the substrate-consumption layer that unblocks all four downstream sub-cohorts.

**Scope-in:**
- **3 new Bridge endpoint routes** on the bootstrap pipeline branch (`POST /api/v1/auth/signup`, `POST /api/v1/auth/verify-email`, `POST /api/v1/auth/resend-verification`). W#79 uses `/api/v1/auth/*` per fleet convention for versioned auth APIs. **`POST /api/v1/auth/check-availability` REMOVED per Admiral ruling D4 (2026-05-26T01:00Z)** — its existence as a yes/no endpoint contradicts the H2 always-202 enumeration-defense. UX rationale for inline email-availability is weak under H2 (the user finds out via the OOB email, not at form-submit); slug-uniqueness is enforced at submit time via the canonical signup 400 `tenant_slug_taken` discriminator. If a future workstream wants a "real-time email check" UX with proper threat-model accommodation, route via separate workstream + new ADR (likely Tier-2 substrate involvement for the enumeration-resistant primitive).
- Wire SignupHandler + VerifyEmailHandler in signal-bridge (or a new `signal-bridge/Sunfish.Bridge.Onboarding/` host module per Decision 7 cluster naming — halt: see Halt H4).
- α-1 child-`IServiceScope` transition with `ITenantContextSeed` per ADR 0095 §"Handler Lifecycle" step 5.
- W#79 ships **Shape A — mocks-only** per ADR 0096 §"Step 4 W79 composition-root wiring" until Engineer ships Step 2 (Postmark) + Step 3 (Turnstile) adapter PRs. Production runtime requires `SUNFISH_ALLOW_MOCK_PROVIDERS=true` until real adapters land.
- Frontend rebind on SignupPage + VerifyEmailPage from any current mock to real Bridge endpoints (pattern-009 PAIR).
- Rate-limit policy values per ADR 0095 Rev 2 non-permissive-default minimum-floor (5/min signup; 10/min verify-email; 3/min resend-verification per-IP + per-email; 429 + Retry-After). (Rev 2: check-availability floor REMOVED per D4.)
- Email-uniqueness check on `TenantRegistrations` control-plane table.
- `ITenantRegistry.CreateAsync` first-production-callsite.

**Scope-out (deferred to W#80+ Surfaces A/B/C/D or post-MVP):**
- Real Postmark + Turnstile vendor adapters (Engineer's Tier-2/D + Tier-2/E queue items per substrate-ladder directive; ship in parallel to W#79; not blocking).
- Real email body templates + i18n (W#80 — sub-cohort 2 Surfaces A+B).
- MockEmailProvider `/dev/inbox` UI route + frontend page (halt: see Halt H1).
- Invitation aggregate cluster (W#82 — sub-cohort 4).
- First-property wizard (W#81 — sub-cohort 3).
- Cookie-based session establishment after verified email (W#80 — sub-cohort 2; signup completion writes the initial User aggregate inside the child scope, but session establishment lives downstream).
- Turnstile site-key delivery to frontend endpoint (ADR 0096 Halt 7 RATIFY — out of scope for ADR 0096; W#80 endpoint shape).

**Patterns this hand-off claims:**
- `pattern-009-w79-onboarding-signup-pair` — Bridge endpoint family + frontend rebind PAIR (pattern-009 STANDING; this is a qualifying instance, not first-instance).
- `pattern-tier2-mock-first-substrate` — first-instance CANDIDATE; promotes from 4-instance shipping per [[tier2-vendor-mock-first]] memory.
- `pattern-bootstrap-context-consumption` — first-instance CANDIDATE.
- `pattern-onboarding-rate-limit` — first-instance CANDIDATE; non-permissive-default rate limit (Rev 2 Amendment 7 minimum-floors).

Stage-05 adversarial-review framework specs for each first-instance pattern are in §6 below.

---

## 1.5 Rev 2 — Admiral architectural rulings (load-bearing constraints)

Per `admiral-directive-2026-05-26T0100Z-onr-w79-rev-2-fold-with-5-architectural-rulings.md`, Admiral ruled 5 architectural decisions that constrain Rev 2 fold. These are summarized here for reviewer convenience; the canonical text is in the directive.

### D1 — `IOperationSigner` API surface (sec-eng B)

**Rule:** Rev 2 uses the existing `SignAsync<T>` surface on `IOperationSigner` (verified at `shipyard/packages/foundation/Crypto/IOperationSigner.cs` lines 1-24):

```csharp
public interface IOperationSigner
{
    PrincipalId IssuerId { get; }
    ValueTask<SignedOperation<T>> SignAsync<T>(
        T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default);
}
```

W#79 defines a `VerificationTokenPayload` record and invokes `signer.SignAsync<VerificationTokenPayload>(payload, issuedAt, nonce, ct)`. The returned `SignedOperation<VerificationTokenPayload>` envelope is canonical-JSON-serialized + Base64Url-encoded into the URL token.

Verification is the inverse: the URL token is Base64Url-decoded + canonical-JSON-deserialized into a `SignedOperation<VerificationTokenPayload>`; verification uses the canonical verify shape associated with `IOperationSigner` substrate (Engineer's PR 1 may either reuse an existing `IOperationVerifier` substrate primitive OR ship a minimal companion verify-method as substrate-touch in PR 0). **No new methods invented on `IOperationSigner`.** If a more specialized signer surface is genuinely needed later, route via a new substrate ADR (post-MVP).

### D2 — `TenantRegistration` schema extension (sec-eng E)

**Rule:** Extend the existing `TenantRegistration` entity in `Sunfish.Bridge.Data/Entities/TenantRegistration.cs` with three new fields. EF Core migration scope = W#79 PR 0:

| Field | Type | Why |
|---|---|---|
| `AdminEmailNormalized` | `string` (indexed, unique-when-not-null) | Queryable for uniqueness from bootstrap scope. Lowercased canonical form. Control-plane field. Existing query path used `TenantRegistrations.Slug`; signup adds an email-uniqueness gate. |
| `EmailVerified` | `bool` (default `false`) | One-shot redemption flag (per sec-eng C; closes replay-window). On `VerifyEmail` first-redemption, atomically updates `EmailVerified=true` inside the EFCore transaction. |
| `PasswordHash` | `string` (nullable; non-null after verify-email completes) | Stored on TenantRegistration row at signup time. **Rationale:** TenantRegistration is the control-plane aggregate; the admin's password belongs at admin-registration tier, not at data-plane User-aggregate tier. The first-tenant-admin's password lives here; future invited users' passwords live on the User aggregate. |

EF migration named `AddOnboardingFieldsToTenantRegistration` ships in PR 0 alongside the schema edit. **`CreateAsync` signature changes** (4-param command shape vs 3-param today); see §4.2.1 + Decision 9 below.

**D2.a sub-ruling — `IUserAggregateRepository` package home.** The `IUserAggregateRepository` referenced by the child-scope write path (originally inferred to live anywhere) is RULED to live in `Sunfish.Bridge.Data` (next to TenantRegistration entity). If substrate-locality concerns emerge in Rev 3 (e.g., the User aggregate becomes blocks-shared), move at that point — for W#79 PR 0/PR 1 it lives in `Sunfish.Bridge.Data`. Add to §4.1.1 PR 0 files-touched.

### D3 — `ITenantRegistry` bootstrap-scope split (.NET-arch L)

**Rule:** Split into two interfaces:

- **`IBootstrapTenantRegistry`** (new; bootstrap-scope-resolvable) — backed by `SunfishBridgeReadOnlyDbContext` (per Decision 8 disposition). Surface:
  ```csharp
  public interface IBootstrapTenantRegistry
  {
      Task<bool> ExistsByEmailAsync(string emailNormalized, CancellationToken ct);
      Task<bool> ExistsBySlugAsync(string slugNormalized, CancellationToken ct);
      Task<UniquenessCheck> CheckUniquenessAsync(
          string emailNormalized, string slugNormalized, CancellationToken ct);
      Task<TenantRegistration?> GetPendingByEmailAsync(string emailNormalized, CancellationToken ct);
      Task<TenantRegistration?> GetPendingByTokenTargetAsync(Guid tenantId, CancellationToken ct);
      Task<TenantRegistration> CreatePendingAsync(CreatePendingTenantCommand command, CancellationToken ct);
      Task<bool> TryConsumeEmailVerificationAsync(
          Guid tenantId, string emailFromToken, CancellationToken ct);
  }
  ```
  - `CheckUniquenessAsync` returns `UniquenessCheck(SlugReserved, SlugTaken, EmailVerifiedTenantExists, EmailUnverifiedTenantExists, ExistingTenantId)`.
  - `CreatePendingAsync` writes a NEW `TenantRegistration` row with `EmailVerified=false`. This is the bootstrap-scope write path (control-plane TenantRegistrations write is bootstrap-scope-permitted by construction since `TenantRegistrations` is NOT under HasQueryFilter per ADR 0031).
  - `TryConsumeEmailVerificationAsync` is the atomic one-shot consumption per sec-eng C: reads the row, asserts `EmailVerified IS false`, atomically updates to `true`, returns the boolean (true on consumed; false on already-consumed → caller decides whether to fall through to 200-idempotent or some other disposition).
  - **DbContext used:** `SunfishBridgeReadOnlyDbContext` for reads; **same read-only context for the writes above** because `TenantRegistrations` table writes are control-plane and bootstrap-resolvable. (Engineer's PR 0 confirms whether `SunfishBridgeReadOnlyDbContext` ships as truly read-only by EF semantics — if so, write paths use a NARROW `BootstrapWriteDbContext` that maps only `TenantRegistrations` + nothing else; documented in §4.1.4 below.)
- **`ITenantRegistry`** (existing; tenant-bound; unchanged at substrate-shape tier) — post-verify-email tenant materialization. Constructor still takes `SunfishBridgeDbContext`; `RequireTenantBoundDbContext` constructor guard fails resolution in bootstrap scope. **Existing call sites unchanged** — only the new bootstrap consumption path uses `IBootstrapTenantRegistry`.

**Mechanism for split:**
- Bootstrap `SignupHandler` + `VerifyEmailHandler` + `ResendVerificationHandler` inject `IBootstrapTenantRegistry`.
- Verify-email's tenant-materialization path (post-token-redemption; the moment the user account is provisioned and the tenant transitions from "registration pending" to "active tenant") opens the child-IServiceScope (α-1 per ADR 0095 Rev 2 Amendment 2), seeds `ITenantContextSeed.Bind(tenantId)`, and resolves `ITenantRegistry` from the CHILD scope for the tenant-bound write.

**LOC budget:** ~80-120 lines of spec text (this section + §4.1.4 + §4.2.1/§4.2.2 handler-body changes); lands in PR 0 substrate setup before PR 1 handler bodies.

### D4 — `check-availability` endpoint removal (sec-eng F)

**Rule:** REMOVE `/api/v1/auth/check-availability` from W#79 scope entirely. Endpoint count drops 4 → 3.

Affected sections rewritten throughout this hand-off:
- §1 Scope-in (done above).
- §3.4 — section removed.
- §3.5 — `tenant_slug_*` discriminators retained (still surfaced from signup); discriminator narrative updated to not reference check-availability.
- §3.7 — `check-availability` row removed from rate-limit table.
- §4.1.1 — `CheckAvailabilityHandler.cs` + `CheckAvailabilityRequest.cs` + `CheckAvailabilityResponse.cs` removed from files-touched.
- §4.1.2 — `MapOnboardingEndpoints` registers 3 routes, not 4.
- §4.1.3 — `onboarding-check` rate-limit policy registration removed.
- §4.2.4 — `CheckAvailabilityHandler` section REMOVED (the §4.2.4 anchor is preserved with a removed-note for cross-references).
- §4.4 — Playwright + MSW scope drops the check-availability scenarios.

### D5 — Pre-tenant audit emission destination (sec-eng G)

**Rule:** Pre-tenant signup events emit to the existing ADR 0049 audit substrate with `tenant_id = SUNFISH_SYSTEM_TENANT` (well-known sentinel `Guid` reserved for system-scope audit partitioning).

**D5.a sub-ruling — sentinel + partition routing.** If ADR 0049's substrate does not currently support a system-tenant sentinel, Engineer adds it as W#79 PR 0 substrate work alongside the TenantRegistration schema migration. The sentinel lives in `Sunfish.Bridge.Data/Constants/SystemTenant.cs` (or equivalent) as `public static readonly Guid Id = new("00000000-0000-0000-0000-000000000000")` OR a reserved non-zero Guid (Engineer picks; documents in PR 0 description).

**Audit field redaction:**
- `email` — logged in full (audit-relevant for fraud-prevention forensics; full email enables enumeration-campaign detection).
- `client_ip` — hashed via SHA-256 (privacy floor; GDPR alignment for pre-tenant data minimization).
- `user_agent` — truncated to 200 chars (fingerprint retention without unbounded log growth).
- `correlation_id` — logged plain (already low-entropy + intentionally per-request).
- `password_hash` — NEVER logged in any form (substrate-tier invariant; mirrors ADR 0096 Rev 2 Amendment #6 BodyText/BodyHtml discipline).

Audit events emitted under `SUNFISH_SYSTEM_TENANT`:
- `OnboardingSignupAttempted` — request received (after CAPTCHA, before uniqueness check).
- `OnboardingSignupCaptchaRejected` — CAPTCHA failed.
- `OnboardingSignupRateLimited` — 429 fired (post-middleware).
- `OnboardingSignupAccepted` — 202 returned (regardless of fresh vs unverified-existing vs verified-existing per H2 always-202).
- `OnboardingVerifyEmailAttempted` — token presented.
- `OnboardingVerifyEmailRejected` — token invalid/expired/already-consumed.
- `OnboardingVerifyEmailConsumed` — first-redemption fired; tenant transitioned to active.
- `OnboardingResendVerificationAttempted` — resend request received.

Post-tenant audit events (post-verify-email, when tenant is active) emit to the resolved-tenant audit stream — cross-scope seam closes because the tenant exists.

---

## 2. Substrate consumed (cross-references)

- **ADR 0095** Bootstrap Context — Accepted 2026-05-25T16:28Z (`shipyard/docs/adrs/0095-bootstrap-context.md`)
- **ADR 0096** Tier-2 Vendor-Provider Substrate — Accepted 2026-05-25T21:45Z (`shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md`)
- **ADR 0091 R2** ITenantContext Divergence Resolution — Accepted (`shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md`) — `AddSunfishTenantContext<TConcrete>` ergonomics mirror; sum-interface facade qualification per [[itenantcontext-consumption-qualification]] memory.
- **ADR 0093 Rev 4** Stage-05 Adversarial Review Protocol — provisionally Accepted (`shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md`) — governs §6 adversarial-review framework specs + §7 trigger matrix.
- **ADR 0031** Bridge as Hybrid Multi-Tenant SaaS — control-plane vs data-plane boundary; `TenantRegistrations` table is control-plane (not under `SunfishBridgeDbContext.HasQueryFilter`).
- **Onboarding-ladder ruling** (`coordination/inbox/admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md`) — Decisions 1, 3, 6, 7, 8, 9, 10.
- **CIC mock-first directive** (`coordination/inbox/admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`) — Postmark + Turnstile + canonical Tier-2 discipline.
- **6 slotting residual Qs RATIFIED** (`coordination/inbox/admiral-ruling-2026-05-25T2200Z-cic-slotting-6-residual-questions-resolved.md`) — Q1 tier vocabulary locked (Tier 1/2/3); not load-bearing for W#79 substrate but vocabulary anchors the pattern-naming.

**Active PRs gating this hand-off:**
- **shipyard#158** (Engineer; ADR 0095 Step 1 `Foundation.Bootstrap` substrate; OPEN) — gates PR 0 + PR 1 (handler + endpoint registration consumes the IBootstrapContext + AddSunfishBootstrapContext API).
- **Engineer ADR 0096 Step 1 PR** (forthcoming per substrate-ladder directive Tier-1 B) — gates PR 1 (handler consumes IEmailProvider + IMockVendorProvider + ICaptchaVerifier).
- **Engineer ADR 0095 Step 2 PR** (forthcoming per Tier-2 C) — gates PR 0 (bootstrap pipeline branch + `MapBootstrapEndpoints` + middleware stack + DbContext constructor guard).

**Engineer queue position visibility (informational; not authored here):**

```
1. shipyard#158 ADR 0095 Step 1                       [Engineer; OPEN; dual-council pending]
   └──┐
2. shipyard ADR 0096 Step 1 substrate                 [Engineer; NOT OPEN; parallel to #158]
   └──┐
3. shipyard ADR 0095 Step 2 (Bridge pipeline branch)  [Engineer; gates on #158]
   └──┐
4. signal-bridge W#79 PR 0 (this spec; 3 endpoints)   [Engineer; gates on 1+2+3]
      └─consumes IBootstrapContext + IEmailProvider─┐
5. signal-bridge W#79 PR 1 (handler bodies)          [Engineer; gates on 4]
                                                     │
6. sunfish W#79 PR 2 (frontend rebind PAIR)          [FED; pair with 4 via auto-merge]
7. sunfish W#79 PR 3 (e2e + contract tests)          [FED/test-eng; gates on 4+5+6]
```

The hand-off below specifies PRs 4-7 (W#79 PR 0 / PR 1 / PR 2 / PR 3 in hand-off numbering).

---

## 3. Pre-flight contract freeze

Per ADR 0093 Rev 4 Amendment I (S05-1 wire-contract reconciliation) + Amendment J (S05-2 RFC 7807 ProblemDetails field-name pin): frontend + backend MUST agree on the wire shapes BEFORE Engineer opens PR 0. Discrepancies surface here, not at Stage-06 SPOT-CHECK cycle 0 RED.

### 3.1 Wire-contract reconciliation — `POST /api/v1/auth/signup`

**Request shape (Frontend → Bridge):**

```typescript
interface SignupRequest {
  email: string;                    // RFC 5322 syntax; lowercased server-side
  password: string;                 // ≥10 chars per ASVS Level 1 minimum-floor
  tenant_slug: string;              // 3-30 chars; ^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$ ; reserved-slug list checked server-side
  tenant_display_name: string;      // 1-80 chars; non-whitespace-trimmed
  captcha_token: string;            // Turnstile / mock token (substrate accepts opaque string ≤2048 chars per ADR 0095 Amendment 5)
}
```

Headers:
- `Content-Type: application/json`
- `Origin: <apex host>` (REQUIRED per ADR 0095 Rev 2 §"Bootstrap branch input policy" Origin-header validation; non-apex Origin returns 403)
- `X-Correlation-Id: <client-supplied-id>` (OPTIONAL; ≤128 chars matching `^[A-Za-z0-9_-]{1,128}$`; server generates fresh if absent/invalid)
- `X-Idempotency-Key: <ULID>` (OPTIONAL; ≤128 chars matching `^[0-9A-HJKMNP-TV-Z]{1,128}$` Crockford-base32-superset)

**Response 200 shape (Bridge → Frontend):**

| Server DTO field | Frontend interface field | Source of truth | Reconciliation status |
|---|---|---|---|
| `SignupAcceptedResponse.email_dispatch_id` (`string`) | `SignupAcceptedResponse.email_dispatch_id: string` | `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/SignupAcceptedResponse.cs` (or wherever Engineer slots the DTO in PR 0) | MATCH |
| `SignupAcceptedResponse.correlation_id` (`string`) | `SignupAcceptedResponse.correlation_id: string` | (same) | MATCH |
| `SignupAcceptedResponse.tenant_id` (`string` Guid) | (frontend MUST NOT declare `tenant_id`) | n/a | NEGATIVE-MATCH — server does NOT return `tenant_id` on signup-acceptance; tenant is created but session is not established until verify-email completes; exposing tenant_id pre-verification reveals tenant-creation success to a yet-to-be-verified email |
| (server does not emit `password_hash`) | (frontend MUST NOT declare `password_hash`) | n/a | NEGATIVE-MATCH (paranoid; defense-in-depth on the never-leak-password-hash invariant) |
| (server does not emit `verification_token`) | (frontend MUST NOT declare `verification_token`) | n/a | NEGATIVE-MATCH — verification token is delivered ONLY via email; frontend never sees it on signup response |
| (server does not emit `next_url` / redirect hint) | (frontend MUST NOT declare a redirect target on signup response) | n/a | NEGATIVE-MATCH — frontend's next step is a static `/auth/verify-email/pending` page; no server-supplied redirect |

The negative-match rows are load-bearing per ADR 0093 Rev 4 Amendment I. Cohort-4 cycle-0 RED A1-FAIL (fictional `tenant_id` / `payload` / `signatures` on `AuditEventDetail`) is the precedent trap; W#79 frontend MUST NOT declare any field the server does not emit.

### 3.2 Wire-contract reconciliation — `POST /api/v1/auth/verify-email`

**Request shape:**

```typescript
interface VerifyEmailRequest {
  verification_token: string;       // opaque server-signed (Ed25519 per IOperationSigner; carries email + tenant_id + nbf/exp); ≤512 chars
}
```

Headers: same as signup minus `X-Idempotency-Key` (verify-email is idempotency-required by handler contract per ADR 0095 §"Initial contract surface" `IdempotencyKey` xmldoc — halt: see Halt H3).

**Response 200 shape:**

| Server DTO field | Frontend interface field | Source of truth | Reconciliation status |
|---|---|---|---|
| `VerifyEmailAcceptedResponse.email` (`string`) | `VerifyEmailAcceptedResponse.email: string` | `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/VerifyEmailAcceptedResponse.cs` | MATCH |
| `VerifyEmailAcceptedResponse.tenant_slug` (`string`) | `VerifyEmailAcceptedResponse.tenant_slug: string` | (same) | MATCH |
| `VerifyEmailAcceptedResponse.tenant_display_name` (`string`) | `VerifyEmailAcceptedResponse.tenant_display_name: string` | (same) | MATCH |
| (server does not emit `tenant_id`) | (frontend MUST NOT declare `tenant_id`) | n/a | NEGATIVE-MATCH — verify-email response carries the user-facing identifiers (slug + display name); tenant Guid stays server-side |
| (server does not emit `session_token` or `auth_token`) | (frontend MUST NOT declare auth tokens here) | n/a | NEGATIVE-MATCH — W#79 does NOT establish session on verify; session establishment is W#80 sub-cohort 2 scope |
| (server does not emit `welcome_url`) | (frontend MUST NOT declare a server-supplied next URL) | n/a | NEGATIVE-MATCH — frontend navigates to a static "verified; please log in" page; W#80 ratifies the next-step flow |

### 3.3 Wire-contract reconciliation — `POST /api/v1/auth/resend-verification`

**Request shape:**

```typescript
interface ResendVerificationRequest {
  email: string;                    // RFC 5322; same canonicalization as signup
}
```

Headers: standard bootstrap headers.

**Response 202 shape (always 202 even if email is unknown — uniform-202 invariant; do not leak existence):**

```typescript
// Server returns 202 with empty body OR a constant `{ "status": "queued" }` envelope.
// Frontend treats 202 as "we'll resend if applicable; check your inbox".
interface ResendVerificationResponse {
  status: 'queued';                 // constant; never reveals whether email was actually known
}
```

**Negative-match:**
- (server does NOT echo the submitted email) — uniform-202 invariant. (No row in table; the constant-envelope shape is the contract.)
- (server does NOT return `email_dispatch_id`) — caller cannot probe dispatch existence. (No row.)

### 3.4 (REMOVED per D4) — `POST /api/v1/auth/check-availability`

Per Admiral ruling D4 (2026-05-26T01:00Z), the check-availability endpoint is REMOVED from W#79 scope. Its existence as a yes/no endpoint contradicts the H2 always-202 enumeration-defense. Slug uniqueness is enforced via the canonical signup 400 `tenant_slug_taken` discriminator at submit time; email uniqueness is silently absorbed by H2 always-202 + OOB notification. UX for inline-feedback during form fill is regressed; reviewed product impact is acceptable.

Anchor preserved for cross-reference stability; see §1 scope-in and §1.5 D4 for the canonical ruling.

### 3.5 Error response shape — onboarding endpoint family (ADR 0093 Rev 4 Amendment J)

400-class responses use RFC 7807 ProblemDetails. The Bridge serializer emits `title` (not `error`) as the error-discriminator field. Frontend error handlers MUST read `body.title === '<discriminator>'`.

**Fleet convention re: ProblemDetails field name (.NET-arch F2):** Per ADR 0093 Rev 4 Amendment J, the fleet uses `title` as the discriminator (NOT RFC 7807's canonical `type` URI). ASP.NET Core's default `Results.Problem(title:, statusCode:)` overload sets `title` directly; the fleet relies on this. Frontend MUST read `body.title` (NOT `body.error`, NOT `body.type`). ASP.NET Core may also emit `type` with a default URI (e.g., `https://tools.ietf.org/html/rfc9110#section-15.5.1` for 400); frontend ignores. The fleet convention is rationalized at protocol tier.

**Known 400/403/429 discriminators (signup + verify-email + resend family, 9 total post-Rev-2):**

- `validation_failed` — generic shape-validation failure (required-field missing; format check failed). Response body's `errors[]` array carries per-field detail.
- ~~`email_already_registered`~~ — **RETIRED in Rev 2 per Admiral ruling H2 (always-202).** Signup never exposes verified-duplicate status; the OOB notification email is the disposition for verified-existing emails (see §6.1 Decision 2). Frontend MUST NOT declare an `EmailAlreadyRegisteredError` typed-error class.
- `tenant_slug_taken` — signup with a slug already in `TenantRegistrations`.
- `tenant_slug_reserved` — signup with a reserved slug (admin, www, api, app, demo, etc.; reserved-list canonicalized in `signal-bridge/Sunfish.Bridge/Services/TenantRegistry.cs` or W#79 introduces if not present).
- `tenant_slug_invalid_shape` — slug fails `^[a-z0-9][a-z0-9-]{1,28}[a-z0-9]$` regex.
- `verification_token_invalid` — verify-email with token that fails Ed25519 signature check, is malformed, or has invalid `aud`/`iss`. Returned on any `SignedOperation<VerificationTokenPayload>` deserialize/verify failure or stale-aud mismatch (per D1 surface alignment).
- `verification_token_expired` — token signature valid but `exp` past now (1h TTL per H9; sec-eng C closes replay via one-shot consumption — see §4.2.2 Step 4).
- `captcha_failed` — Turnstile / mock CAPTCHA verification returned non-success.
- `rate_limited` — 429 response per §3.7 floors. Carries `Retry-After` header.
- `origin_invalid` — 403 response. Origin header missing on POST or did not match apex host. (Per ADR 0095 §"Bootstrap branch input policy" Origin validation returns 403 with non-diagnostic body — frontend treats as transport failure, not user-correctable.)

**Rev 2 deletion:** `verification_token_already_used` is REMOVED from the discriminator list. Per H9 disposition (200-idempotent + sec-eng C one-shot redemption-state), the verify-email handler returns 200 on already-consumed tokens — there is no 400 path that exposes "already consumed" as a distinct discriminator. (Closes the verification_token_already_used → 9-discriminator confusion; per test-eng Gap T2 B2.)

**Frontend typed-error contract:**

Each 400/429 discriminator becomes a typed-error class in the frontend (`ValidationFailedError`, `TenantSlugTakenError`, `TenantSlugReservedError`, `TenantSlugInvalidShapeError`, `CaptchaFailedError`, `RateLimitedError`, `VerificationTokenInvalidError`, `VerificationTokenExpiredError`). The 403 `origin_invalid` is surfaced as a transport-failure banner (no user-correctable input).

**Discriminator single-source-of-truth (test-eng T2):** discriminator string literals are defined ONCE as a `const` export in `sunfish/apps/web/src/api/onboarding-discriminators.ts` (TypeScript) + mirrored at `signal-bridge/Sunfish.Bridge.Onboarding/OnboardingDiscriminators.cs` (C#). A contract test asserts the two const sets are equal byte-for-byte. This pins string-drift at the source.

```typescript
// sunfish/apps/web/src/api/onboarding-discriminators.ts
export const SignupDiscriminator = {
  VALIDATION_FAILED: 'validation_failed',
  TENANT_SLUG_TAKEN: 'tenant_slug_taken',
  TENANT_SLUG_RESERVED: 'tenant_slug_reserved',
  TENANT_SLUG_INVALID_SHAPE: 'tenant_slug_invalid_shape',
  VERIFICATION_TOKEN_INVALID: 'verification_token_invalid',
  VERIFICATION_TOKEN_EXPIRED: 'verification_token_expired',
  CAPTCHA_FAILED: 'captcha_failed',
  RATE_LIMITED: 'rate_limited',
  ORIGIN_INVALID: 'origin_invalid',
} as const;

export type SignupDiscriminatorValue = typeof SignupDiscriminator[keyof typeof SignupDiscriminator];
```

```csharp
// signal-bridge/Sunfish.Bridge.Onboarding/OnboardingDiscriminators.cs
public static class OnboardingDiscriminators
{
    public const string ValidationFailed = "validation_failed";
    public const string TenantSlugTaken = "tenant_slug_taken";
    public const string TenantSlugReserved = "tenant_slug_reserved";
    public const string TenantSlugInvalidShape = "tenant_slug_invalid_shape";
    public const string VerificationTokenInvalid = "verification_token_invalid";
    public const string VerificationTokenExpired = "verification_token_expired";
    public const string CaptchaFailed = "captcha_failed";
    public const string RateLimited = "rate_limited";
    public const string OriginInvalid = "origin_invalid";
}
```

**429 shape (rate-limit; ADR 0095 Rev 2 Amendment 7 floors):**

```
HTTP/1.1 429 Too Many Requests
Retry-After: <seconds>
Content-Type: application/problem+json
{"title": "rate_limited", "status": 429, "detail": "Rate limit exceeded for this endpoint."}
```

Frontend reads `Retry-After` header (NOT the body) for retry-wait countdown UX.

### 3.6 Origin + length-cap discipline

Per ADR 0095 Rev 2 Amendment 5 (input length caps; substrate-tier minimum-floor):

- `Origin` header REQUIRED on all signup-family POSTs; missing or non-apex → 403 with non-diagnostic body. Frontend always sends `Origin` (browsers do by default for cross-site requests; for same-site POSTs the browser sends `Origin` for POST per Fetch spec).
- `X-Correlation-Id` ≤128 chars `^[A-Za-z0-9_-]{1,128}$`; invalid → server drops + generates fresh (NOT a 400).
- `captcha_token` body field ≤2048 chars; exceeded → 400 `validation_failed`.
- `X-Idempotency-Key` ≤128 chars ULID-shape `^[0-9A-HJKMNP-TV-Z]{1,128}$`; exceeded → 400 `validation_failed`.

These caps are validated at `BootstrapContextResolutionMiddleware` BEFORE any handler sees the request.

### 3.7 Rate-limit floors (ADR 0095 Rev 2 Amendment 7; non-permissive-default minimum-floor)

| Endpoint | Per-IP layer | Per-route+per-IP layer | Per-entity layer | Burst | 429 Retry-After |
|---|---|---|---|---|---|
| `POST /api/v1/auth/signup` | 5 / min / IP fixed-window | 5 / min / (route+IP) | n/a (signup doesn't have an entity key — email is the candidate but always-202 + sham-hash already absorb floor) | 0 | window remainder |
| `POST /api/v1/auth/verify-email` | 10 / min / IP fixed-window | 10 / min / (route+IP) | n/a | 5 | window remainder |
| `POST /api/v1/auth/resend-verification` | 3 / min / IP fixed-window | 3 / min / (route+IP) | 3 / min / (email-keyed) | 0 | window remainder |
| ~~`POST /api/v1/auth/check-availability`~~ | (REMOVED per D4) | | | | |

W#79 ratifies these as MINIMUM floors; W#79 implementation MAY tighten (smaller windows, lower counts) but MUST NOT loosen. The resend-verification per-email key prevents an attacker from amplifying a single victim email by spreading across IPs (defense-in-depth on email-flood abuse).

**Per-email key computation (.NET-arch G2 pin).** Lowercased email → SHA-256 of UTF-8 bytes → take **first 16 bytes (32 hex chars)** as the bucket key. For a population of 10K-100K users, 16-byte prefix gives ~2^64 collision-space — sufficient for collision-resistant bucketing without unbounded bucket-key cardinality. Engineer's PR 0 sets the prefix length explicitly:

```csharp
private static string ComputeEmailBucketKey(string emailNormalized)
{
    // SHA-256 first-16-bytes; 32 hex chars; .NET-arch G2 ratified prefix length.
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(emailNormalized));
    return Convert.ToHexString(bytes.AsSpan(0, 16));
}
```

**Partition-key resolver worked example (.NET-arch G1).** Per ADR 0095 Rev 2: per-IP fallback to route-only when `ClientIp` is null (test contexts). The `PartitionedRateLimiter<HttpContext>` shape:

```csharp
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var route = httpContext.Request.Path.ToString();
        // Route-only fallback per RateLimitBucketKey xmldoc remarks (ADR 0095 Rev 2).
        var key = ip is null ? $"route:{route}" : $"ip:{ip}:route:{route}";

        // Pick window + permit-count by route prefix (signup vs verify-email vs resend).
        var (permitLimit, queueLimit) = route switch
        {
            var r when r.StartsWith("/api/v1/auth/signup")                  => (5, 0),
            var r when r.StartsWith("/api/v1/auth/verify-email")            => (10, 5),
            var r when r.StartsWith("/api/v1/auth/resend-verification")     => (3, 0),
            _                                                                => (60, 5), // non-onboarding default
        };

        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
    });

    // Separate per-email partition for resend-verification.
    // Resolved inside the handler post-input-canonicalization since the email
    // isn't visible at middleware-tier (it's a body field, not a header or URL part).
    // See §4.2.3 ResendVerificationHandler.
});
```

The handler-tier per-email partition for resend-verification lives inside the handler (see §4.2.3); the middleware-tier partition handles per-IP + per-(route+IP). Engineer's PR 0 ships both partitions; sec-eng + .NET-arch SPOT-CHECK verifies.

### 3.8 Mandatory `AllowAnonymous()` + analyzer enforcement (ADR 0095 Rev 2 Amendment 6)

All 3 endpoints registered inside `MapBootstrapEndpoints` MUST declare `.AllowAnonymous()` at registration time. The Step 3 Roslyn analyzer (Engineer Tier-3 F per substrate-ladder directive) enforces this; W#79 PR 0 ships before the analyzer, so the discipline is reviewer-enforced for this PR (per ADR 0095 §"Consequences/Negative" Step 1-2 doc-comment-discipline window).

Example:

```csharp
endpoints.MapPost("/api/v1/auth/signup", SignupHandler.HandleAsync)
    .AllowAnonymous()
    .DisableAntiforgery()        // bootstrap-branch antiforgery is invoked at pipeline level; per-endpoint DisableAntiforgery only if the endpoint deliberately exempts (signup form-payloads use antiforgery)
    .WithName("OnboardingSignup");
```

### 3.9 Pair-merge cascade plan (ADR 0093 Rev 4 Amendment L; Rev 2 per-step test-gating per test-eng T6)

W#79 PR 0 (Bridge endpoints) + W#79 PR 2 (frontend rebind) form a pattern-009 PAIR. Sequencing + test-suite gating per step:

| Step | Owner | Deliverable | Tests gated at this step | Cycle |
|---|---|---|---|---|
| 1 | Engineer | shipyard#158 ADR 0095 Step 1 MERGED | (substrate test suite per ADR 0095) | (gate) |
| 2 | Engineer | shipyard ADR 0096 Step 1 MERGED | (substrate test suite per ADR 0096) | (gate; parallel to step 1) |
| 3 | Engineer | shipyard ADR 0095 Step 2 MERGED (`MapBootstrapEndpoints` + pipeline branch + DbContext constructor guard) | (substrate test suite per ADR 0095 Step 2) | (gate) |
| 4 | Engineer | W#79 PR 0 (Bridge endpoints scaffold-only returning 501 ProblemDetails + DI registrations + DbContext schema migration + IBootstrapTenantRegistry interface + SUNFISH_SYSTEM_TENANT sentinel + EF migration for TenantRegistration fields) | §4.1.5 5 integration tests (routing wire-up + AllowAnonymous + rate-limit floor + Origin reject + length-cap reject) + §4.2.7 ITenantContextSeed 6 unit tests + EF-migration apply-rollback smoke test | Substrate cycle |
| 5 | FED | W#79 PR 2 DRAFT — SignupPage + VerifyEmailPage scaffolding bound against real Bridge URLs. PR 0's 501 ProblemDetails triggers the "service not yet available" banner per §4.3.3. | §4.3.4 SignupPage tests 1, 2, 9 only (form-renders + slug-regex + captcha-mock) + banner-presence test; tests 3-8/11-12 wait for PR 1 merge (Bridge handlers active). Tests run AGAINST MOCK MSW (typed-error mapping verified at MSW tier in cycle 1). | Cycle 1 |
| 6 | sec-eng + frontend-arch | Cycle 1 SPOT-CHECK on FED DRAFT — expects AMBER if FED-Cycle-1-commit-clean-banner; expects RED if FED-Cycle-1-commit-silently-hides-dead-code | (no new tests; verdict only) | Cycle 1 |
| 7 | Engineer | W#79 PR 1 (handler bodies + α-1 child-scope transition + IBootstrapTenantRegistry + IEmailProvider + ICaptchaVerifier consumption + first-tenant + first-user write + ProblemDetails per-discriminator emission) MERGED | §4.2.6 13 handler integration tests (9 baseline + sec-eng C one-shot + sec-eng D constant-work + audit-emission test) + §4.2.5 14 production-guard tests M1-M14 + §4.2.6.PD 9 ProblemDetails backend per-discriminator tests = 36 PR 1 tests | Substrate cycle |
| 8 | FED | W#79 PR 2 amendment commit — Cycle 2 banner removed; full RTL suite + error-handling per-discriminator wired | §4.3.4 12 SignupPage + §4.3.5 6 VerifyEmailPage RTL tests; §4.3.6 9 per-discriminator typed-error tests (matched 1:1 against §3.5 discriminators) | Cycle 2 |
| 9 | sec-eng + frontend-arch | Cycle 2 re-attest — GREEN gate for auto-merge cascade | (no new tests; verdict only) | Cycle 2 |
| 10 | FED/test-eng | W#79 PR 3 (e2e + contract tests + MSW-vs-real-Bridge parity verification) | §4.4 3 Playwright specs + §4.4.4 MSW-vs-Bridge parity test + cross-stack contract test asserting MSW handlers byte-for-byte match Bridge handler responses | Cycle 2 |

**Constraint per Amendment L.** Cycle 1's DRAFT MUST NOT silently hide a non-functional feature. The signup form MUST be wired to a real Bridge URL (not a service-worker mock); if the handler body is not yet wired and the Bridge returns **501 ProblemDetails with `title: 'not_implemented'`**, the frontend renders a "service not yet available" banner cleanly with a forward-watch comment. Cleanly-removed-with-forward-watch is the AMBER posture; silently-dead-code is the RED posture. (Per .NET-arch I1: the OR-shaped "no handler body" option in Rev 1 is structurally unavailable — `MapPost` requires a delegate; PR 0 ships handlers returning 501 ProblemDetails as the canonical scaffold-only shape.)

**MSW-vs-real-Bridge parity (test-eng T6 B2 + Gap T6 step-10 column).** Step 10's MSW handlers in `sunfish/apps/web/msw/onboarding-handlers.ts` are verified against the real Bridge response shapes at PR 3 cycle. The parity test fixture spins up the test-env Bridge + captures the response JSON for each of the 9 discriminator paths + asserts byte-for-byte equality with the MSW handler's emitted body. Without parity, MSW lies are invisible.

### 3.10 Commit-message pre-flight (ADR 0093 Rev 4 Amendment K)

Before pushing any W#79 commit:

```bash
git log -1 --format=%B | grep -E '[A-Za-z]#[0-9]'
```

Returns nothing → safe to push. Returns matches → rephrase: `Refs: shipyard#158` as a footer (with leading blank line), or "the sibling shipyard PR" inline, or strip the inline ref entirely.

**Cross-repo PRs W#79 implementation will likely cite:** shipyard#158 (Step 1 substrate), the not-yet-numbered ADR 0095 Step 2 (Bridge pipeline branch), the not-yet-numbered ADR 0096 Step 1, signal-bridge bridge-endpoint PR for W#79 PR 0, sunfish frontend PR for W#79 PR 2 + PR 3.

---

## 4. PR breakdown

### 4.1 PR 0 — Bridge endpoint pair (signal-bridge)

**Repo:** `signal-bridge`
**Branch suggestion:** `feat/w79-pr0-auth-endpoints`
**Estimated lines:** ~600-900 (3 endpoint registrations + 3 handler skeletons + DTOs + DI registrations + integration test scaffolds)
**Estimated effort:** 1-2 days Engineer time
**Council SPOT-CHECK on Ready-flip:** sec-eng (pattern-009 PAIR MANDATORY per fleet-conventions §SPOT-CHECK SLA — 3 NEW routes; substrate-touch) + .NET-architect (substrate-touch on first-instance consumer of ADR 0095 + ADR 0096 substrates) + frontend-architect (pattern-009 PAIR — Bridge half).

**Gating on upstream:** shipyard#158 (ADR 0095 Step 1) MERGED + shipyard ADR 0096 Step 1 MERGED + shipyard ADR 0095 Step 2 (Bridge pipeline branch + `MapBootstrapEndpoints` extension method) MERGED. If any are not yet shipped, PR 0 holds DRAFT; do NOT open as Ready until all three are merged.

#### 4.1.1 Files touched

| File | Action | Notes |
|---|---|---|
| `signal-bridge/Sunfish.Bridge/Program.cs` | extend | Add `endpoints.MapBootstrapEndpoints()` call inside the bootstrap pipeline branch; configure rate-limit policies per §3.7 floors; call `services.AddSunfishBootstrapContext<BootstrapContext>()`; call `services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>()`; call `services.AddSunfishVendorProvider<ICaptchaVerifier, InMemoryCaptchaVerifier>()`; register `IBootstrapTenantRegistry` (bootstrap-scope) + `ITenantRegistry` (tenant-bound; existing); register both IHostedService assertions in canonical order (ADR 0095 first, ADR 0096 second). |
| `signal-bridge/Sunfish.Bridge.Onboarding/Sunfish.Bridge.Onboarding.csproj` | new | New project (per Admiral ruling H4 RATIFY-ONR option a + .NET-arch D1). Worked-example contents below. |
| `signal-bridge/Sunfish.Bridge.Onboarding/` | new module | Houses `OnboardingEndpoints.cs`, `OnboardingDiscriminators.cs`, `Handlers/SignupHandler.cs`, `Handlers/VerifyEmailHandler.cs`, `Handlers/ResendVerificationHandler.cs`, `Contracts/*.cs`. **CheckAvailabilityHandler / CheckAvailability*.cs REMOVED per D4.** |
| `signal-bridge/Sunfish.Bridge.Onboarding/OnboardingEndpoints.cs` | new | Extension method `MapOnboardingEndpoints(IEndpointRouteBuilder)` called from inside `MapBootstrapEndpoints` body. Registers **3 endpoints** with `.AllowAnonymous()` per ADR 0095 Rev 2 Amendment 6. |
| `signal-bridge/Sunfish.Bridge.Onboarding/OnboardingDiscriminators.cs` | new | `public static class OnboardingDiscriminators` with the 9 discriminator string constants per §3.5; consumed by handler emission + ProblemDetailsDiscriminatorTests assertion + frontend MSW parity test. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/SignupRequest.cs` | new | Request DTO; record. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/SignupAcceptedResponse.cs` | new | Response DTO; record. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/VerifyEmailRequest.cs` | new | Request DTO. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/VerifyEmailAcceptedResponse.cs` | new | Response DTO. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/ResendVerificationRequest.cs` | new | Request DTO. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/ResendVerificationResponse.cs` | new | Constant `{"status":"queued"}` envelope; record with single property. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Contracts/VerificationTokenPayload.cs` | new | Record consumed by `IOperationSigner.SignAsync<VerificationTokenPayload>` (per D1). Fields: `Email` (string), `TenantId` (Guid), `Audience` (string; canonical `"sunfish.verify-email"` literal for token-class disambiguation). `IssuedAt` + nonce are envelope-tier per `SignedOperation<T>` shape; not payload-tier. Expiration is handled at verify-time by `signer`'s replay-window per its substrate contract OR by handler-side `nbf+ttl` check (Engineer's PR 1 ratifies). |
| `signal-bridge/Sunfish.Bridge.Onboarding/Handlers/SignupHandler.cs` | new | Skeleton returns `Results.Problem(...)` with `title: "not_implemented"` and status 501 in PR 0; full body in PR 1 (per .NET-arch I1: no "no handler body" option — `MapPost` requires a delegate). |
| `signal-bridge/Sunfish.Bridge.Onboarding/Handlers/VerifyEmailHandler.cs` | new | Same skeleton/scaffold pattern. |
| `signal-bridge/Sunfish.Bridge.Onboarding/Handlers/ResendVerificationHandler.cs` | new | Same. |
| `signal-bridge/Sunfish.Bridge.Data/IBootstrapTenantRegistry.cs` | new | Bootstrap-scope-resolvable read+write tenant-registry interface per D3. Lives in `Sunfish.Bridge.Data` (control-plane lives next to TenantRegistration entity). |
| `signal-bridge/Sunfish.Bridge.Data/BootstrapTenantRegistry.cs` | new | Concrete implementation; constructor takes `SunfishBridgeReadOnlyDbContext`. ~80 LOC. |
| `signal-bridge/Sunfish.Bridge.Data/IUserAggregateRepository.cs` | new | Per D2.a sub-ruling: lives in `Sunfish.Bridge.Data`. Surface: `PersistInitialUserAsync(InitialUser, CancellationToken)` + `MarkEmailVerifiedAsync(Guid tenantId, string email, CancellationToken)`. |
| `signal-bridge/Sunfish.Bridge.Data/UserAggregateRepository.cs` | new | Concrete; constructor takes `SunfishBridgeDbContext` (tenant-bound; resolved only from child scope per ADR 0095 Rev 2 Amendment 4). ~60 LOC. |
| `signal-bridge/Sunfish.Bridge.Data/Entities/TenantRegistration.cs` | modify | Add `AdminEmailNormalized: string?`, `EmailVerified: bool`, `PasswordHash: string?` per D2. Index on `AdminEmailNormalized` (filtered: WHERE NOT NULL; uniqueness gate on signup). |
| `signal-bridge/Sunfish.Bridge.Data/Entities/User.cs` | new | First-user aggregate entity; tenant-bound (TenantId FK to TenantRegistration). Carries `Email`, `EmailVerified`, `CreatedAt`. Password hash lives on TenantRegistration per D2 schema decision. |
| `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeReadOnlyDbContext.cs` | new | Read-only DbContext for bootstrap-scope queries (no `RequireTenantBoundDbContext` marker); maps ONLY `TenantRegistrations` DbSet; `OnModelCreating` is explicitly minimal (no HasQueryFilter inheritance). Per Decision 8 disposition + .NET-arch C1 mechanism. |
| `signal-bridge/Sunfish.Bridge.Data/Constants/SystemTenant.cs` | new (per D5.a if not already shipped) | `public static class SystemTenant { public static readonly Guid Id = new("00000000-0000-0000-0000-000000000000"); }`. Used as audit `tenant_id` for pre-tenant events. |
| `signal-bridge/Sunfish.Bridge.Data/Migrations/<timestamp>_AddOnboardingFieldsToTenantRegistration.cs` | new | EF Core migration adding the 3 columns + filtered-unique index on AdminEmailNormalized + User entity table. |
| `shipyard/packages/foundation-authorization/ITenantContextSeed.cs` | new | Substrate primitive per H6 ruling. Bind-once API: `void Bind(Guid tenantId); Guid? TenantId { get; }`. |
| `shipyard/packages/foundation-authorization/MutableTenantContextSeed.cs` | new | Concrete; uses `Interlocked.CompareExchange<Guid>` for bind-once thread-safety. ~30 LOC. |
| `shipyard/packages/foundation-authorization/SeededTenantContext.cs` | new | Adapter implementing `ITenantContext` + `ICurrentUser` + `IAuthorizationContext` + sum-interface facade via single instance per ADR 0091 R2 A1; resolves TenantId via `ITenantContextSeed`. |
| `shipyard/packages/foundation-authorization/RequireTenantBoundDbContext.cs` | new | Sealed empty class (.NET-arch C1 shape ruling); resolved as `SunfishBridgeDbContext` constructor parameter; registered ONLY on the non-bootstrap branch. Engineer's PR 0 (ADR 0095 Step 2) ships the actual registration mechanism. |
| `signal-bridge/Sunfish.Bridge.Tests/Onboarding/OnboardingEndpointsIntegrationTests.cs` | new | Wire-up tests: routes registered + AllowAnonymous + rate-limit policies applied; mock-providers respond OK in test env. **5 tests** per §4.1.5. |
| `signal-bridge/Sunfish.Bridge.Tests/Onboarding/ProblemDetailsDiscriminatorTests.cs` | new (PR 1) | Backend ProblemDetails per-discriminator pinning per test-eng T2; 9 tests asserting `title` field matches each of the 9 OnboardingDiscriminators constants when corresponding handler error path fires. |
| `signal-bridge/Sunfish.Bridge.Tests/Onboarding/MockProviderGuardIntegrationTests.cs` | new (PR 1) | 12 production-guard tests M1-M12 per test-eng T1 (canonical-form parsing variants + ASPNETCORE_ENVIRONMENT branches) + factory-registered mock detection (sec-eng H / M3 + M4 tests; see §4.2.5). |
| `signal-bridge/Sunfish.Bridge.Tests/Onboarding/EnvVarScope.cs` | new (PR 1) | `IDisposable` helper for env-var test isolation per test-eng T5 + ADR 0096 .NET-arch A5; captures + restores `Environment.SetEnvironmentVariable` mutations. |
| `shipyard/packages/foundation-authorization/tests/MutableTenantContextSeedTests.cs` | new (PR 0; ships with substrate) | 6 unit tests per test-eng T3 (bind-once / read-before-bind / bind-twice-throws / concurrent-bind / SeededTenantContext.TenantId resolution / scope-leak isolation). |

**Worked-example: `Sunfish.Bridge.Onboarding.csproj` contents (per .NET-arch D1 + D2).** Sdk = `Microsoft.NET.Sdk` + explicit FrameworkReference for the minimal-API surface (NOT `Microsoft.NET.Sdk.Web` — this project hosts handler classes, not the app):

```xml
<!-- signal-bridge/Sunfish.Bridge.Onboarding/Sunfish.Bridge.Onboarding.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <IsPackable>false</IsPackable>
    <!-- NU1510 suppression if direct PackageReferences become required after preview.4
         NuGet resolution shift (foundation-authorization precedent commit b4475df). -->
    <NoWarn>$(NoWarn);NU1510</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.Core" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Bridge\Sunfish.Bridge.csproj" />
    <ProjectReference Include="..\Sunfish.Bridge.Data\Sunfish.Bridge.Data.csproj" />
    <ProjectReference Include="$(SiblingShipyardPath)\packages\foundation-bootstrap\Sunfish.Foundation.Bootstrap.csproj" />
    <ProjectReference Include="$(SiblingShipyardPath)\packages\foundation-integrations\Sunfish.Foundation.Integrations.csproj" />
    <ProjectReference Include="$(SiblingShipyardPath)\packages\foundation-authorization\Sunfish.Foundation.Authorization.csproj" />
    <ProjectReference Include="$(SiblingShipyardPath)\packages\foundation\Sunfish.Foundation.csproj" />
  </ItemGroup>
</Project>
```

**Test project rationale (.NET-arch D3):** Onboarding integration tests reuse `Sunfish.Bridge.Tests` rather than a sibling `Sunfish.Bridge.Onboarding.Tests` project to maintain a single `IHost.StartAsync` test harness per ADR 0095 Rev 2 Amendment 3 registration-presence pattern. Tests live under `Sunfish.Bridge.Tests/Onboarding/`.

**PR 0 / PR 1 split rationale.** Split shields the substrate registrations (bootstrap pipeline branch + DI wiring + endpoint registration shape) from the handler bodies (α-1 child-scope transition + repository writes + email dispatch). PR 0 is reviewable by sec-eng + .NET-arch + frontend-arch in isolation; PR 1 layers the handler logic. If PR 0's scope grows past Engineer's comfort, Engineer MAY merge PR 0 + PR 1 into a single PR with §6 adversarial-review framework applied across the union; ONR's recommendation is the split for review clarity. (Halt: see Halt H5 if the split itself becomes a routing question.)

#### 4.1.2 `OnboardingEndpoints.cs` registration shape

```csharp
namespace Sunfish.Bridge.Onboarding;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/signup", SignupHandler.HandleAsync)
            .AllowAnonymous()                 // ADR 0095 Rev 2 Amendment 6 MANDATORY
            .WithName("OnboardingSignup");

        endpoints.MapPost("/api/v1/auth/verify-email", VerifyEmailHandler.HandleAsync)
            .AllowAnonymous()
            .WithName("OnboardingVerifyEmail");

        endpoints.MapPost("/api/v1/auth/resend-verification", ResendVerificationHandler.HandleAsync)
            .AllowAnonymous()
            .WithName("OnboardingResendVerification");

        // NOTE: `/api/v1/auth/check-availability` REMOVED per Admiral ruling D4 (2026-05-26).
        // See §1.5 D4 + §3.4 (removed-note) for rationale.

        return endpoints;
    }
}
```

Called from inside `MapBootstrapEndpoints` body (the extension method shipped by ADR 0095 Step 2). The substrate's empty `MapBootstrapEndpoints` body is filled by chained calls to per-cohort `Map*Endpoints` extensions; W#79 adds `MapOnboardingEndpoints` (registering 3 routes post-Rev-2).

#### 4.1.3 Rate-limit policy registration (in `Program.cs`)

Per ADR 0095 Rev 2 Amendment 7:

```csharp
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("onboarding-signup", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("onboarding-verify-email", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 5;
    });
    options.AddFixedWindowLimiter("onboarding-resend", limiter =>
    {
        limiter.PermitLimit = 3;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    // `onboarding-check` policy REMOVED per Admiral ruling D4.
    options.OnRejected = async (context, ct) =>
    {
        // Set Retry-After header to window remainder
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "rate_limited",
                Status = 429,
                Detail = "Rate limit exceeded for this endpoint."
            }, cancellationToken: ct);
    };
});
```

Policies attached to endpoints via `.RequireRateLimiting("onboarding-signup")` (etc.) on each `MapPost` call. **Per-IP + per-(route+IP) layers:** the per-IP layer is the global limiter; the per-(route+IP) layer is achieved by partitioning the limiter key by `(ip, route)` tuple. Engineer chooses concrete partitioning shape; sec-eng SPOT-CHECK verifies.

#### 4.1.4 DI registration (in `Program.cs`)

```csharp
// ADR 0095 Step 1 substrate (forthcoming when shipyard#158 + Step 2 land)
services.AddSunfishBootstrapContext<BootstrapContext>();

// ADR 0096 Step 1 substrate (forthcoming when Tier-1 B lands)
services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
services.AddSunfishVendorProvider<ICaptchaVerifier, InMemoryCaptchaVerifier>();

// IHostedService startup assertions; canonical ordering:
services.AddHostedService<BootstrapAndTenantMutualExclusionAssertion>();   // ADR 0095 — registered FIRST
services.AddHostedService<MockProviderProductionGuardAssertion>();         // ADR 0096 — registered SECOND

// IBootstrapTenantRegistry (D3): registered at root; bootstrap-scope-resolvable;
// reads/writes TenantRegistrations control-plane via SunfishBridgeReadOnlyDbContext.
services.AddScoped<IBootstrapTenantRegistry, BootstrapTenantRegistry>();

// ITenantRegistry (existing): tenant-bound; constructor takes SunfishBridgeDbContext
// (which carries RequireTenantBoundDbContext guard per ADR 0095 Rev 2 Amendment 4);
// resolves ONLY from the child IServiceScope post-Bind().
// (No change to existing TenantRegistry registration; left unchanged.)

// IUserAggregateRepository (D2.a; tenant-bound write path; child-scope-resolved):
services.AddScoped<IUserAggregateRepository, UserAggregateRepository>();

// SunfishBridgeReadOnlyDbContext (Decision 8 disposition; bootstrap-scope-resolvable):
// Maps ONLY the TenantRegistrations DbSet. Explicitly minimal OnModelCreating; no HasQueryFilter.
services.AddDbContext<SunfishBridgeReadOnlyDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Bridge")));

// Pin ProblemDetails serializer to emit `title` (NOT `type`) as the discriminator.
// Per ADR 0093 Rev 4 Amendment J. ASP.NET Core's default Results.Problem(title:, statusCode:)
// overload sets `title`; this AddProblemDetails call ratifies the convention at the framework tier.
services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        // Ensure `title` is the consumer-facing discriminator; `type` may be auto-populated
        // by ASP.NET Core with an RFC URI which is informational, not the discriminator.
        // No-op customization here pins the contract — handler emissions set `title` directly.
    };
});

// IPasswordHasher<TUser> — default PBKDF2 V3 concrete per .NET-arch K1/K2.
// NOT auto-registered by Microsoft.AspNetCore.Identity.Core; must be explicit.
services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();

// ITenantContextSeed scoped-holder (per H6 ratified; lives in foundation-authorization):
services.AddScoped<ITenantContextSeed, MutableTenantContextSeed>();

// SeededTenantContext aliases the four-interface facade per ADR 0091 R2 A1 invariant
// (preserves ReferenceEquals across ITenantContext / ICurrentUser / IAuthorizationContext / facade).
// .NET-arch B1.b explicit 4-line aliasing form (chosen over B1.a because AddSunfishTenantContext
// today does NOT accept a seed-holder-as-concrete shape; verified at
// shipyard/packages/foundation-authorization/DependencyInjection/TenantContextServiceCollectionExtensions.cs).
services.AddScoped<SeededTenantContext>(sp =>
    new SeededTenantContext(sp.GetRequiredService<ITenantContextSeed>()));
services.AddScoped<Sunfish.Foundation.Authorization.ITenantContext>(sp =>
    sp.GetRequiredService<SeededTenantContext>());
services.AddScoped<Sunfish.Foundation.Authorization.ICurrentUser>(sp =>
    sp.GetRequiredService<SeededTenantContext>());
services.AddScoped<Sunfish.Foundation.Authorization.IAuthorizationContext>(sp =>
    sp.GetRequiredService<SeededTenantContext>());
// Sum-interface facade (if separately declared per ADR 0091 R2 pattern):
services.AddScoped<Sunfish.Foundation.Authorization.ITenantContextFacade>(sp =>
    sp.GetRequiredService<SeededTenantContext>());

// RequireTenantBoundDbContext marker — per .NET-arch C1.z mechanism:
// NOT registered at root. The non-bootstrap pipeline branch's middleware
// (BootstrapContextResolutionMiddleware's inverse path, post-tenant-resolution)
// opens a child scope via ChildServiceScopeMiddleware which calls
// `services.AddScoped<RequireTenantBoundDbContext>()` on the branch's IServiceCollection
// (per ADR 0095 Step 2 substrate API). The marker is resolvable ONLY inside non-bootstrap
// scope. Bootstrap-branch's IServiceCollection does NOT have this registration; resolving
// SunfishBridgeDbContext in bootstrap scope throws standard .NET DI:
//   `InvalidOperationException: Unable to resolve service for type 'RequireTenantBoundDbContext'
//    while attempting to activate 'SunfishBridgeDbContext'.`
// Engineer's PR 0 substrate-tier code in ADR 0095 Step 2 ships the branch-isolated registration;
// W#79 PR 0 consumes the mechanism, does NOT re-implement.
//
// NOTE: This is a DELIBERATE deviation from idiomatic ASP.NET Core DI (root container holds all
// registrations) in service of the bootstrap-vs-tenant-bound resolvability split. ADR 0095 §"Pipeline
// routing" + Rev 2 Amendment 4 ratifies the mechanism.

// (Implementation reference for SunfishBridgeDbContext constructor signature change per .NET-arch C1:
//   public SunfishBridgeDbContext(
//       DbContextOptions<SunfishBridgeDbContext> options,
//       IEnumerable<ISunfishEntityModule> modules,
//       ITenantContext tenantContext,
//       RequireTenantBoundDbContext _)   // new parameter; underscore name; never read
//   { ... }
// DesignTimeDbContextFactory updates to pass `new RequireTenantBoundDbContext()` at design time.
// Both shipped by Engineer's PR 0 substrate-tier work, not W#79 application code.)
```

**`.Bind()` contract (.NET-arch B2 + sec-eng FW-1 pin).** The `MutableTenantContextSeed` exposes:

```csharp
public sealed class MutableTenantContextSeed : ITenantContextSeed
{
    private Guid _tenantId;
    private int _bound; // 0 = not bound; 1 = bound

    public Guid? TenantId
    {
        get
        {
            if (Volatile.Read(ref _bound) == 0)
                throw new InvalidOperationException(
                    "Tenant context seed not bound; resolve only after .Bind() inside the child scope.");
            return _tenantId;
        }
    }

    public void Bind(Guid tenantId)
    {
        if (Interlocked.CompareExchange(ref _bound, 1, 0) != 0)
            throw new InvalidOperationException(
                "Tenant context already seeded for this scope.");
        _tenantId = tenantId;
    }
}
```

Semantics pinned at spec time:
- **One-shot.** Second `Bind()` call in the same scope throws `InvalidOperationException("Tenant context already seeded for this scope.")`. Prevents silent tenant-context mutation across two `Bind()` calls in the same handler.
- **Pre-Bind resolution.** Throws `InvalidOperationException("Tenant context seed not bound; resolve only after .Bind() inside the child scope.")`. NEVER returns `Guid.Empty` (the HasQueryFilter foot-gun ADR 0095 Rev 2 Gap B closes).
- **Thread-safety.** `Bind()` is thread-safe (uses `Interlocked.CompareExchange<int>`). Reads via `Volatile.Read`. Within a single async-disposable scope, .NET DI conventions imply single-threaded resolution, but the seed could be captured by a `Task.Run` continuation — the lock prevents silent races.

Tests covering these semantics live in `shipyard/packages/foundation-authorization/tests/MutableTenantContextSeedTests.cs` per test-eng T3 (6 unit tests; see §4.2.7).

**ITenantContextSeed package home (per ADR 0095 §"Out of scope but flagged" .NET-arch A2 follow-on, ratified at H6).** Lives in `shipyard/packages/foundation-authorization/` alongside `AddSunfishTenantContext`.

#### 4.1.5 Acceptance criteria for PR 0 (sec-eng + .NET-arch + frontend-arch GREEN)

- [ ] 3 endpoints registered inside `MapBootstrapEndpoints` body; all 3 carry `.AllowAnonymous()`.
- [ ] Rate-limit policies configured for all 3 endpoints; floors meet §3.7 minimum.
- [ ] `Origin` validation middleware fires before `BootstrapContextResolutionMiddleware` (ADR 0095 §"Pipeline routing" ordering).
- [ ] DI registrations: `AddSunfishBootstrapContext<BootstrapContext>()` + 2 mock-vendor registrations + 2 IHostedService (ordering A first then B).
- [ ] `ITenantContextSeed` + `MutableTenantContextSeed` + `SeededTenantContext` shipped in `packages/foundation-authorization/`.
- [ ] `RequireTenantBoundDbContext` marker registered on the non-bootstrap branch only.
- [ ] Integration test: hitting `/api/v1/auth/signup` returns 501 (scaffold) — confirms routing wire-up.
- [ ] Integration test: hitting `/api/v1/auth/signup` 6× in 60s returns 429 with `Retry-After` (rate-limit floor).
- [ ] Integration test: hitting `/api/v1/auth/signup` with no `Origin` header returns 403 (non-diagnostic body).
- [ ] Integration test: signup with `captcha_token` >2048 chars returns 400 with `validation_failed` ProblemDetails.
- [ ] Pre-flight commit-message check (Amendment K) — no `<word>#<digit>` in body.
- [ ] PR description includes: pattern claims (pattern-009 standing instance + pattern-tier2-mock-first-substrate first-instance candidate + pattern-bootstrap-context-consumption first-instance candidate + pattern-onboarding-rate-limit first-instance candidate); §A0 cited-symbol audit (per ADR 0069 if substrate-shaping).

### 4.2 PR 1 — Substrate consumption (handler bodies)

**Repo:** `signal-bridge`
**Branch suggestion:** `feat/w79-pr1-onboarding-handlers`
**Estimated lines:** ~700-1100 (4 handler bodies + ITenantRegistry production-wire-in + child-scope transition + tests; substrate types from PR 0)
**Estimated effort:** 2-3 days Engineer time
**Council SPOT-CHECK on Ready-flip:** sec-eng (MANDATORY; α-1 child-scope transition is security-critical confused-deputy-seam) + .NET-architect (MANDATORY; first production consumer of ADR 0095 + ADR 0096 substrates; ITenantRegistry first-production-callsite invariant per ADR 0095 §Decision drivers).

#### 4.2.1 SignupHandler — α-1 child-scope transition body (Rev 2)

Rev 2 fold absorbs sec-eng A (IPasswordHasher interface), sec-eng D (constant-work discipline), sec-eng C (one-shot via TenantRegistration.EmailVerified), D1 (IOperationSigner.SignAsync<T>), D3 (IBootstrapTenantRegistry), .NET-arch K1 (IPasswordHasher<UserEntity>), .NET-arch K2 (V3 versioned-hash for ADR 0097), H2 (always-202 unconditional — drops the email_already_registered branch + the EmailUnverifiedTenantExists branch is folded into always-202 + OOB):

**Note (FW-A1 reconciliation):** Spec uses `await using var childScope = scopeFactory.CreateAsyncScope()` (async-disposable). ADR 0095's example uses sync `using var childScope = _scopeFactory.CreateScope()` — the sync example is illustrative; the .NET-idiomatic async-disposable shape is correct here because `await userRepo.PersistInitialUserAsync(...)` mandates async-disposable scope (sync `using` would dispose synchronously before async work in nested scoped services completes; EFCore `DbContext.DisposeAsync` is the canonical example). Engineer's Step 3 Roslyn analyzer (Tier-3 F) should NOT read sync-vs-async-using as a regression.

```csharp
namespace Sunfish.Bridge.Onboarding.Handlers;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Authorization;
using Sunfish.Foundation.Bootstrap;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Integrations.Email;
using Sunfish.Bridge.Data;

public static class SignupHandler
{
    public static async Task<IResult> HandleAsync(
        [FromBody] SignupRequest request,
        IBootstrapContext bootstrapContext,
        ICaptchaVerifier captcha,
        IEmailProvider email,
        IBootstrapTenantRegistry tenantRegistry,    // D3 — bootstrap-scope-resolvable
        IServiceScopeFactory scopeFactory,
        IOperationSigner signer,
        IPasswordHasher<UserEntity> passwordHasher, // sec-eng A + .NET-arch K1 — interface, NEVER static
        IOnboardingAuditEmitter auditEmitter,        // D5 — emits to SUNFISH_SYSTEM_TENANT partition
        ILogger<SignupHandler> logger,
        CancellationToken ct)
    {
        // STEP 1 — Bootstrap-only DI scope active.
        // IBootstrapContext is the only context-shape binding resolvable here.
        // ITenantContext / ICurrentUser / IAuthorizationContext are NOT resolvable in this scope
        // (per Step 3 analyzer; doc-comment-discipline during Step 1-2 window).

        // STEP 2 — Validate signup payload (no DB access).
        var validationResult = SignupRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            await auditEmitter.EmitSignupAttemptedAsync(
                bootstrapContext, request.Email, outcome: "validation_failed", ct);
            return Results.Problem(
                title: OnboardingDiscriminators.ValidationFailed,
                statusCode: 400,
                detail: validationResult.ToProblemDetails());
        }

        // STEP 2a — CAPTCHA verification (substrate-tier; mock or real depending on registration).
        var captchaResult = await captcha.VerifyAsync(
            new CaptchaVerificationRequest(
                Token: request.CaptchaToken,
                RemoteIp: bootstrapContext.ClientIp?.ToString()),
            ct);
        if (!captchaResult.Succeeded)
        {
            await auditEmitter.EmitSignupCaptchaRejectedAsync(
                bootstrapContext, request.Email, ct);
            return Results.Problem(
                title: OnboardingDiscriminators.CaptchaFailed,
                statusCode: 400,
                detail: "CAPTCHA verification failed.");
        }

        // STEP 2b — CONSTANT-WORK DISCIPLINE (sec-eng D — closes timing-attack on H2).
        // Hash the user-supplied password UNCONDITIONALLY here, BEFORE the uniqueness check.
        // PasswordHasher.HashPassword is the wall-clock-dominant cost (PBKDF2 v3 ~100k iterations
        // ~tens of ms). Equalizing this cost across all H2 branches (fresh / unverified-existing /
        // verified-existing) narrows the timing-distinguisher floor to sub-ms DB-query variation.
        //
        // The hash output is COMPUTED ON EVERY REQUEST; consumed only on the fresh-path; discarded
        // on the existing-path. This is a deliberate constant-work invariant — do NOT optimize it
        // away with a "skip if existing" branch. Per sec-eng D + ADR 0095 Rev 2 timing-floor.
        //
        // Hash output is ASP.NET Identity V3 format (version-byte 0x01; PBKDF2-HMAC-SHA256;
        // configurable iteration count). Future ADR 0097 Argon2id swap will detect old hashes via
        // the version byte and trigger PasswordVerificationResult.SuccessRehashNeeded on next login.
        // Per .NET-arch K2 — handler is non-load-bearing on algorithm choice.
        var stubUser = new UserEntity { Email = request.Email.Trim().ToLowerInvariant() };
        var passwordHashCandidate = passwordHasher.HashPassword(stubUser, request.Password);

        // STEP 3 — Email + slug uniqueness check via SunfishBridgeReadOnlyDbContext.
        // Per Decision 8: bootstrap-scope-resolvable read-only DbContext; queries control-plane
        // TenantRegistrations table (NOT under HasQueryFilter); see §6.1 Decision 8.
        // (Rev 2 .NET-arch O fix: prior Rev 1 referenced "§5.4 below"; corrected to §6.1 Decision 8.)
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var slugNormalized = request.TenantSlug.Trim().ToLowerInvariant();

        var uniquenessCheck = await tenantRegistry.CheckUniquenessAsync(
            emailNormalized, slugNormalized, ct);

        // SLUG-side discriminator paths remain — these don't leak email-existence; they leak
        // slug-existence (which is already a public namespace property — slug -> tenant URL).
        if (uniquenessCheck.SlugReserved)
        {
            await auditEmitter.EmitSignupAttemptedAsync(
                bootstrapContext, request.Email, outcome: "slug_reserved", ct);
            return Results.Problem(
                title: OnboardingDiscriminators.TenantSlugReserved, statusCode: 400);
        }

        if (uniquenessCheck.SlugTaken)
        {
            await auditEmitter.EmitSignupAttemptedAsync(
                bootstrapContext, request.Email, outcome: "slug_taken", ct);
            return Results.Problem(
                title: OnboardingDiscriminators.TenantSlugTaken, statusCode: 400);
        }

        // EMAIL-side: H2 always-202 ratified.
        // Three sub-paths converge on a 202 response (see §6.1 Decision 2 Rev 2):
        //
        //  (a) Fresh email + valid slug -> CreatePendingAsync + verification email dispatch
        //      + 202 with email_dispatch_id.
        //  (b) Unverified-existing email -> quietly resend verification email + 202 with
        //      email_dispatch_id (same shape as fresh path).
        //  (c) Verified-existing email -> dispatch OOB notification email ("someone tried to
        //      sign up with your address") + 202 with email_dispatch_id (same shape).
        //
        // All three responses are byte-for-byte identical to the caller. Discarded
        // passwordHashCandidate on paths (b) and (c) preserves constant-work invariant.

        string emailDispatchId;
        if (uniquenessCheck.EmailVerifiedTenantExists)
        {
            // Path (c): OOB notification per H2 ratification.
            // (Per-victim-email rate-limit gates the dispatch; sec-eng I forward-watched.)
            emailDispatchId = await SendVerifiedExistingNotificationAsync(
                email, emailNormalized, uniquenessCheck.ExistingTenantId!.Value, bootstrapContext, ct);
            await auditEmitter.EmitSignupAttemptedAsync(
                bootstrapContext, request.Email, outcome: "verified_existing_oob_dispatched", ct);
        }
        else if (uniquenessCheck.EmailUnverifiedTenantExists)
        {
            // Path (b): quietly resend verification.
            emailDispatchId = await SendVerificationEmailAsync(
                email, signer, emailNormalized, uniquenessCheck.ExistingTenantId!.Value,
                bootstrapContext, ct);
            await auditEmitter.EmitSignupAttemptedAsync(
                bootstrapContext, request.Email, outcome: "unverified_existing_resend_dispatched", ct);
        }
        else
        {
            // Path (a): fresh registration.

            // STEP 4 — Create the pending TenantRegistration row.
            // Per D2 schema extension: AdminEmailNormalized + EmailVerified=false + PasswordHash
            // all written here. Per D3: bootstrap-scope CreatePendingAsync uses
            // SunfishBridgeReadOnlyDbContext write path (or narrow BootstrapWriteDbContext;
            // Engineer's PR 0 picks).
            var newRegistration = await tenantRegistry.CreatePendingAsync(
                new CreatePendingTenantCommand(
                    EmailNormalized: emailNormalized,
                    SlugNormalized: slugNormalized,
                    DisplayName: request.TenantDisplayName,
                    PasswordHash: passwordHashCandidate),
                ct);

            // STEP 5 — Child IServiceScope for post-tenant initial-User write (α-1 mechanism).
            // Per ADR 0095 Rev 2 Amendment 2.
            await using var childScope = scopeFactory.CreateAsyncScope();
            childScope.ServiceProvider
                .GetRequiredService<ITenantContextSeed>()
                .Bind(newRegistration.TenantId);

            var userRepo = childScope.ServiceProvider.GetRequiredService<IUserAggregateRepository>();
            await userRepo.PersistInitialUserAsync(
                new InitialUser(
                    TenantId: newRegistration.TenantId,
                    Email: emailNormalized,
                    EmailVerified: false),
                ct);
            // Child scope disposes here on await using.

            // STEP 6 — Bootstrap scope continues: verification email dispatch.
            emailDispatchId = await SendVerificationEmailAsync(
                email, signer, emailNormalized, newRegistration.TenantId, bootstrapContext, ct);

            await auditEmitter.EmitSignupAcceptedAsync(
                bootstrapContext, request.Email, newRegistration.TenantId, ct);
        }

        // ALL THREE PATHS converge here: identical 202 response shape per H2 RATIFY.
        return Results.Accepted(
            value: new SignupAcceptedResponse(
                EmailDispatchId: emailDispatchId,
                CorrelationId: bootstrapContext.CorrelationId));
    }

    // SendVerificationEmailAsync — uses IOperationSigner.SignAsync<VerificationTokenPayload> per D1.
    private static async Task<string> SendVerificationEmailAsync(
        IEmailProvider email,
        IOperationSigner signer,
        string emailNormalized,
        Guid tenantId,
        IBootstrapContext bootstrapContext,
        CancellationToken ct)
    {
        // D1: existing SignAsync<T> surface; no SignVerificationToken invented method.
        // Envelope carries the payload + IssuedAt + nonce; signer signs the canonical-JSON form.
        var payload = new VerificationTokenPayload(
            Email: emailNormalized,
            TenantId: tenantId,
            Audience: "sunfish.verify-email");
        var signed = await signer.SignAsync(
            payload,
            issuedAt: DateTimeOffset.UtcNow,
            nonce: Guid.NewGuid(),
            ct);
        // Serialize SignedOperation<VerificationTokenPayload> envelope to canonical JSON
        // + Base64Url-encode for the URL token. (Engineer's PR 1 references
        // foundation/Crypto SignedOperationEncoder if it exists, OR ships the encoder helper
        // inline. Either way, no new methods invented on IOperationSigner.)
        var verificationToken = SignedOperationEncoder.EncodeBase64Url(signed);

        var dispatchResult = await email.SendAsync(
            new EmailMessage(
                From: "noreply@sunfish.app",          // halt H7: from-address shape
                To: new[] { emailNormalized },
                Subject: "Verify your Sunfish email",
                BodyText: $"Click to verify: https://sunfish.app/auth/verify-email?token={verificationToken}",
                BodyHtml: null,                       // W#80 ships HTML template + i18n
                MessageStream: "transactional-onboarding",
                IdempotencyKey: bootstrapContext.IdempotencyKey ?? $"signup-{tenantId}",
                EmailDispatchId: Guid.NewGuid().ToString()),
            ct);

        return dispatchResult switch
        {
            EmailDispatchResult.Accepted accepted => accepted.MessageId,
            _ => throw new InvalidOperationException(
                $"Email dispatch failed for tenant {tenantId}; result: {dispatchResult}")
        };
    }

    // OOB notification path for H2 verified-existing — no signed token; just a notification.
    private static async Task<string> SendVerifiedExistingNotificationAsync(
        IEmailProvider email,
        string emailNormalized,
        Guid existingTenantId,
        IBootstrapContext bootstrapContext,
        CancellationToken ct)
    {
        var dispatchResult = await email.SendAsync(
            new EmailMessage(
                From: "noreply@sunfish.app",
                To: new[] { emailNormalized },
                Subject: "Sunfish signup attempt for your account",
                BodyText: "Someone tried to sign up using your email address. " +
                          "If this wasn't you, no action is required. " +
                          "If you'd like to sign in, visit https://sunfish.app/auth/login.",
                BodyHtml: null,
                MessageStream: "transactional-onboarding",
                IdempotencyKey: $"oob-existing-{existingTenantId}-{DateTimeOffset.UtcNow:yyyy-MM-dd}", // per-day dedup
                EmailDispatchId: Guid.NewGuid().ToString()),
            ct);

        return dispatchResult switch
        {
            EmailDispatchResult.Accepted accepted => accepted.MessageId,
            EmailDispatchResult.RateLimited _ => Guid.NewGuid().ToString(),  // silent drop; caller still gets a dispatch-id-shape
            _ => throw new InvalidOperationException(
                $"OOB notification dispatch failed for tenant {existingTenantId}; result: {dispatchResult}")
        };
    }
}
```

**Critical invariants enforced in PR 1 (Rev 2):**

- **Constant-work discipline (sec-eng D).** `passwordHasher.HashPassword(...)` runs UNCONDITIONALLY at STEP 2b — closes the H2 timing-attack on verified-vs-fresh enumeration. Integration test asserts p50/p95 latency parity across the 3 paths.
- **IPasswordHasher<UserEntity> interface ONLY (sec-eng A + .NET-arch K1).** No static `PasswordHasher.Hash(...)` calls anywhere. ADR 0097 future Argon2id swap requires zero callsite changes (handler signature unchanged; DI registration swap only).
- **V3 versioned hash output (.NET-arch K2).** ASP.NET Identity's `PasswordHasher<TUser>` defaults to `PasswordHasherCompatibilityMode.IdentityV3` (PBKDF2-HMAC-SHA256 + version byte 0x01). ADR 0097 migration uses the byte to detect old hashes + trigger `PasswordVerificationResult.SuccessRehashNeeded` on next login.
- **D1 — IOperationSigner.SignAsync<VerificationTokenPayload>.** No invented signer methods; uses existing async substrate API. Token envelope = `SignedOperation<VerificationTokenPayload>` canonical-JSON + Base64Url.
- **D3 — IBootstrapTenantRegistry bootstrap-scope-resolvable.** SignupHandler injects `IBootstrapTenantRegistry` (NOT `ITenantRegistry`); resolves via `SunfishBridgeReadOnlyDbContext`. `ITenantRegistry` (tenant-bound) is reserved for child-scope post-Bind resolution paths.
- **D5 — Audit emission to SUNFISH_SYSTEM_TENANT partition.** All pre-tenant audit events (validation_failed / captcha_rejected / slug_reserved / slug_taken / verified_existing_oob_dispatched / unverified_existing_resend_dispatched / signup_accepted) route via `IOnboardingAuditEmitter` to ADR 0049 substrate with `tenant_id = SystemTenant.Id`. Email + IP-hashed + UA-truncated-200 + correlation_id plain (per redaction policy).
- **H2 RATIFY always-202.** All three email-side sub-paths converge on a byte-for-byte identical 202 response. No `email_already_registered` 400 branch (retired Rev 2).
- α-1 scoped-holder pattern per ADR 0095 Rev 2 Amendment 2 — `ITenantContextSeed.Bind()` IMMEDIATELY before any post-tenant resolution inside the child scope.
- `await using var childScope` — async-disposable; scope disposes after the write commits.
- `IUserAggregateRepository` resolved from child scope (NOT outer bootstrap scope) — confirms `SeededTenantContext` is reachable in the child scope.
- The outer `SendVerificationEmailAsync` continues on the **outer bootstrap scope**, not the child scope — audit + email-dispatch are bootstrap-scope concerns per ADR 0095 §"Handler Lifecycle" step 6.
- `IEmailProvider` injection consumes mock (MockEmailProvider) until Engineer Tier-2 D ships PostmarkEmailProvider; production guard fires if not opted-out (`SUNFISH_ALLOW_MOCK_PROVIDERS=true` required for `ASPNETCORE_ENVIRONMENT=Production` mock-only deploys).
- `ICaptchaVerifier` likewise consumes InMemoryCaptchaVerifier (now `: IMockVendorProvider` via Step 1 retrofit).

#### 4.2.2 VerifyEmailHandler (Rev 2)

Rev 2 fold absorbs D1 (existing IOperationSigner surface, no invented verify-method), sec-eng C (one-shot redemption-state via IBootstrapTenantRegistry.TryConsumeEmailVerificationAsync), D3 (split registries — bootstrap registry for token-validate + one-shot consume; tenant registry resolved from child-scope ONLY when tenant-bound write is required), H9 200-idempotent (test-eng T2 removes verification_token_already_used discriminator), D5 audit-emission:

```csharp
public static async Task<IResult> HandleAsync(
    [FromBody] VerifyEmailRequest request,
    IBootstrapContext bootstrapContext,
    IBootstrapTenantRegistry tenantRegistry,    // D3 — bootstrap-resolvable; token-validate + one-shot consume
    IServiceScopeFactory scopeFactory,
    IOperationSigner signer,
    IOperationVerifier verifier,                // companion to IOperationSigner per Engineer's PR 0 substrate API (D1 alignment)
    IOnboardingAuditEmitter auditEmitter,       // D5 audit emission
    ILogger<VerifyEmailHandler> logger,
    CancellationToken ct)
{
    // STEP 1 — Bootstrap-only scope.

    // STEP 2 — Decode + verify the signed envelope per D1.
    // The URL token is the Base64Url-encoded canonical-JSON of SignedOperation<VerificationTokenPayload>.
    // Use the canonical verify substrate (IOperationVerifier; ships in Engineer's PR 0 alongside the encoder
    // helper). NO invented OperationSignerResult discriminated union — verify returns success-or-throws OR
    // a typed-result envelope ratified by Engineer's substrate PR.
    //
    // Engineer's substrate-tier verify-shape (one of):
    //   ValueTask<VerifiedOperation<T>> VerifyAsync<T>(string token, CancellationToken ct);   (throws on invalid)
    //   ValueTask<VerifyResult<T>> TryVerifyAsync<T>(string token, CancellationToken ct);     (typed result)
    //
    // Rev 2 spec assumes the second shape (TryVerifyAsync returns typed result). If Engineer ships the
    // first (throws-on-invalid), the handler wraps in try/catch and maps the exception shape to the
    // same discriminator routing.
    var verifyOutcome = await verifier.TryVerifyAsync<VerificationTokenPayload>(
        request.VerificationToken, ct);

    if (verifyOutcome is VerifyResult<VerificationTokenPayload>.Invalid)
    {
        await auditEmitter.EmitVerifyEmailRejectedAsync(
            bootstrapContext, reason: "token_invalid", ct);
        return Results.Problem(
            title: OnboardingDiscriminators.VerificationTokenInvalid, statusCode: 400);
    }

    if (verifyOutcome is VerifyResult<VerificationTokenPayload>.Expired)
    {
        await auditEmitter.EmitVerifyEmailRejectedAsync(
            bootstrapContext, reason: "token_expired", ct);
        return Results.Problem(
            title: OnboardingDiscriminators.VerificationTokenExpired, statusCode: 400);
    }

    if (verifyOutcome is not VerifyResult<VerificationTokenPayload>.Valid valid)
    {
        // Defensive default — any non-Valid is treated as invalid.
        await auditEmitter.EmitVerifyEmailRejectedAsync(
            bootstrapContext, reason: "token_invalid", ct);
        return Results.Problem(
            title: OnboardingDiscriminators.VerificationTokenInvalid, statusCode: 400);
    }

    // STEP 3 — Audience check: payload must claim the verify-email audience.
    if (valid.Payload.Audience != "sunfish.verify-email")
    {
        await auditEmitter.EmitVerifyEmailRejectedAsync(
            bootstrapContext, reason: "audience_mismatch", ct);
        return Results.Problem(
            title: OnboardingDiscriminators.VerificationTokenInvalid, statusCode: 400);
    }

    // STEP 4 — ATOMIC one-shot consumption per sec-eng C.
    //
    // This is the closure of the H9 1h-TTL replay window. The TryConsumeEmailVerificationAsync
    // method reads the TenantRegistration row, asserts EmailVerified IS false, atomically updates
    // to true, returns boolean (true on consumed; false on already-consumed or tenant-not-found).
    //
    // Per H9 RATIFY 200-idempotent: already-consumed AND tenant-not-found both fall through to the
    // 200 path (idempotent UX; cannot leak "was already redeemed" by response-shape).
    var consumed = await tenantRegistry.TryConsumeEmailVerificationAsync(
        valid.Payload.TenantId, valid.Payload.Email, ct);

    if (consumed)
    {
        // First-time redemption fired.
        // STEP 5 — Materialize the User-aggregate write inside the child scope (α-1 transition).
        await using var childScope = scopeFactory.CreateAsyncScope();
        childScope.ServiceProvider
            .GetRequiredService<ITenantContextSeed>()
            .Bind(valid.Payload.TenantId);

        var userRepo = childScope.ServiceProvider.GetRequiredService<IUserAggregateRepository>();
        await userRepo.MarkEmailVerifiedAsync(
            valid.Payload.TenantId, valid.Payload.Email, ct);
        // Child scope disposes here.

        await auditEmitter.EmitVerifyEmailConsumedAsync(
            bootstrapContext, valid.Payload.TenantId, ct);
    }
    else
    {
        // Already-consumed OR tenant-not-found path. Idempotent per H9 RATIFY.
        await auditEmitter.EmitVerifyEmailRejectedAsync(
            bootstrapContext, reason: "already_consumed_or_missing", ct);
    }

    // STEP 6 — Read-back from bootstrap-scope read-only DbContext for response body.
    // (Read after the optional write; the consumed-or-not branch above is the auth boundary.)
    var registration = await tenantRegistry.GetPendingByTokenTargetAsync(
        valid.Payload.TenantId, ct);

    if (registration is null || registration.AdminEmailNormalized is null)
    {
        // Tenant-gone-after-redemption edge; surface as invalid token (defensive).
        return Results.Problem(
            title: OnboardingDiscriminators.VerificationTokenInvalid, statusCode: 400);
    }

    return Results.Ok(new VerifyEmailAcceptedResponse(
        Email: registration.AdminEmailNormalized,
        TenantSlug: registration.Slug,
        TenantDisplayName: registration.DisplayName));
}
```

**Critical invariants enforced (sec-eng C + D1 + D3 + H9 RATIFY):**

- **One-shot redemption-state (sec-eng C).** `TryConsumeEmailVerificationAsync` is the atomic gate; replays beyond the consumption point fall through to the 200-idempotent shape. The H9 1h-TTL combined with the consumed-flag closes the replay-readable window.
- **D1 — IOperationSigner verify surface.** Uses the canonical substrate verify shape (Engineer's PR 0 substrate API; no invented `TryVerifyVerificationToken` / `OperationSignerResult` types).
- **D3 — IBootstrapTenantRegistry for read + atomic consume.** ITenantRegistry (tenant-bound) is NOT injected; the tenant materialization path uses `IUserAggregateRepository` from the child scope post-Bind. Bootstrap-scope can write to TenantRegistrations control-plane row (the one-shot flag); tenant-bound writes go through child scope.
- **H9 RATIFY 200-idempotent.** Already-consumed token → 200 with the same response body shape; no `verification_token_already_used` discriminator (retired per Rev 2 + test-eng T2 9-discriminator floor).
- **D5 audit.** All branches emit; system-tenant partition; redaction policy applied.
- **No tenant_id leakage.** Response body carries only `Email + TenantSlug + TenantDisplayName` per §3.2 NEGATIVE-MATCH rows.

#### 4.2.3 ResendVerificationHandler — uniform-202 + per-email rate-limit + D5 audit (Rev 2)

```csharp
public static async Task<IResult> HandleAsync(
    [FromBody] ResendVerificationRequest request,
    IBootstrapContext bootstrapContext,
    IBootstrapTenantRegistry tenantRegistry,    // D3
    IEmailProvider email,
    IOperationSigner signer,
    IPerEmailRateLimiter perEmailRateLimiter,   // §3.7 handler-tier per-entity-key partition
    IOnboardingAuditEmitter auditEmitter,       // D5
    CancellationToken ct)
{
    // ALWAYS returns 202; uniform-202 invariant.
    var emailNormalized = request.Email.Trim().ToLowerInvariant();
    var emailBucketKey = ComputeEmailBucketKey(emailNormalized);   // SHA-256 first-16-bytes per .NET-arch G2

    await auditEmitter.EmitResendVerificationAttemptedAsync(
        bootstrapContext, request.Email, ct);

    // Per-email rate-limit gate.
    using var lease = await perEmailRateLimiter.AcquireAsync(emailBucketKey, permits: 1, ct);
    if (!lease.IsAcquired)
    {
        // Silent drop — uniform-202 invariant means caller can't distinguish rate-limited from sent.
        // Audit still emitted for fraud-forensics.
        return Results.Accepted(value: new ResendVerificationResponse(Status: "queued"));
    }

    var lookup = await tenantRegistry.GetPendingByEmailAsync(emailNormalized, ct);
    if (lookup is not null && lookup.EmailVerified == false)
    {
        // Quietly re-send verification email; do not surface lookup result.
        _ = await SignupHandler.SendVerificationEmailAsync(
            email, signer, emailNormalized, lookup.TenantId, bootstrapContext, ct);
    }

    return Results.Accepted(value: new ResendVerificationResponse(Status: "queued"));
}

private static string ComputeEmailBucketKey(string emailNormalized)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(emailNormalized));
    return Convert.ToHexString(bytes.AsSpan(0, 16));    // 32 hex chars per .NET-arch G2
}
```

#### 4.2.4 (REMOVED per D4) — CheckAvailabilityHandler

Per Admiral ruling D4 (2026-05-26T01:00Z), this handler is REMOVED. Anchor preserved for cross-reference stability; see §1.5 D4 + §3.4 (removed-note).

#### 4.2.5 MockEmailProvider production-guard verification (Rev 2 — M1-M12 + sec-eng H factory-registered mock detection)

PR 1's deployment story is **Shape A — mocks-only** per ADR 0096 §"Step 4 W79 composition-root wiring." Rev 2 expands the production-guard test matrix from M1+M2 to **M1-M12** per test-eng T1 (canonical-form parsing variants + ASPNETCORE_ENVIRONMENT branches) + extends to factory-registered mock detection per sec-eng H (M3 + M4):

| # | Env: mock-bound? | `SUNFISH_ALLOW_MOCK_PROVIDERS` | `POSTMARK_API_KEY` | `ASPNETCORE_ENVIRONMENT` | Registration shape | Expected | Closes |
|---|---|---|---|---|---|---|---|
| M1 | yes | (unset) | (unset) | Production | `AddSingleton<IEmailProvider, MockEmailProvider>()` | THROW (names IEmailProvider + POSTMARK_API_KEY) | D1c silent-typo foot-gun |
| M2 | yes | `"true"` | (unset) | Production | type-bound | SUCCESS | D1b opt-out happy path |
| M3 | yes | (unset) | (unset) | Production | **factory-registered: `AddSingleton<IEmailProvider>(sp => new MockEmailProvider(...))`** | THROW | **sec-eng H Threat 5** — guard inspects `ImplementationFactory` returns; closes ImplementationType-null escape |
| M4 | yes | `"true"` | (unset) | Production | factory-registered | SUCCESS | sec-eng H factory + opt-out interaction |
| M5 | yes | `"True"` | (unset) | Production | type-bound | SUCCESS | bool.TryParse canonical-form parsing |
| M6 | yes | `"TRUE"` | (unset) | Production | type-bound | SUCCESS | bool.TryParse canonical-form parsing |
| M7 | yes | `"false"` | (unset) | Production | type-bound | THROW | bool.TryParse canonical-form parsing fail-closed |
| M8 | yes | `"1"` | (unset) | Production | type-bound | THROW (fail-closed) | non-parseable value fail-closed (ADR 0096 R2 sec-eng B1) |
| M9 | yes | `"yes"` | (unset) | Production | type-bound | THROW (fail-closed) | non-parseable value fail-closed |
| M10 | yes | (unset) | `"pm-token-xxxxx"` | Production | type-bound | SUCCESS | real-adapter-env-var presence path (no opt-out needed) |
| M11 | yes | (unset) | `""` (empty string) | Production | type-bound | THROW | `!IsNullOrWhiteSpace` semantic — empty-string is absent |
| M12 | yes | (unset) | `"   "` (whitespace) | Production | type-bound | THROW | `!IsNullOrWhiteSpace` semantic — whitespace is absent |
| M13 | yes | (unset) | (unset) | Testing | type-bound | SUCCESS | Testing-env early-return per ADR 0096 D1c |
| M14 | yes | (unset) | (unset) | Development | type-bound | SUCCESS | Development-env early-return |

**Total: 14 production-guard tests** (originally specced as M1-M12 in test-eng T1; sec-eng H adds the factory-registered variants M3/M4; M13/M14 cover the ASPNETCORE_ENVIRONMENT early-return branches that close §9 risk-row "MockEmailProvider production-guard fires unexpectedly in CI").

**Implementation note for sec-eng H Threat 5 closure (M3).** The guard must walk `IServiceCollection` for `ServiceDescriptor` entries whose `ImplementationFactory` is non-null + execute the factory on a probe `ServiceProvider` to inspect the returned runtime type for `IMockVendorProvider`. ADR 0096 Step 1 substrate (Engineer's Tier-1 B PR) is the canonical home for this walk; if Step 1 ships only the type-bound inspection, sec-eng SPOT-CHECK on Step 1 surfaces the gap + Engineer's Step 2 amendment extends. Decorator-pattern mock detection (sec-eng H sub-concern 2) is FORWARD-WATCHED (not blocking for W#79; promoted at next-substrate-touch).

Tests live in `signal-bridge/Sunfish.Bridge.Tests/Onboarding/MockProviderGuardIntegrationTests.cs`. All tests use `EnvVarScope` IDisposable + `[Collection("EnvVarSerial")]` per §4.2.8 env-var isolation discipline.

#### 4.2.6 Acceptance criteria for PR 1 (Rev 2)

- [ ] SignupHandler body implements α-1 transition per ADR 0095 §"Handler Lifecycle" + Rev 2 constant-work discipline (sec-eng D) + H2 RATIFY always-202 (3-branch convergence: fresh / unverified-existing / verified-existing) + D5 audit emission.
- [ ] `IPasswordHasher<UserEntity>` injected; NO static `PasswordHasher.Hash(...)` calls anywhere (sec-eng A + .NET-arch K1).
- [ ] `passwordHasher.HashPassword(...)` runs UNCONDITIONALLY at STEP 2b regardless of email-branch (sec-eng D constant-work). Integration test asserts p50/p95 latency parity across 3 H2 branches (`+/- 25%` floor; CI flake-resilient bound).
- [ ] `IOperationSigner.SignAsync<VerificationTokenPayload>(...)` per D1 — NO invented `SignVerificationToken` method.
- [ ] `IBootstrapTenantRegistry` injected (NOT `ITenantRegistry`); per D3 split.
- [ ] `ITenantContextSeed.Bind()` called IMMEDIATELY before any post-tenant resolution inside the child scope; one-shot semantics enforced (throws on second call); see §4.1.4 Bind() contract + §4.2.7 unit tests.
- [ ] `await using` async-disposable child scope; outer scope continues for audit + email.
- [ ] `SunfishBridgeDbContext` NEVER resolved in bootstrap scope; constructor guard verified per .NET-arch C1.z mechanism (test asserts `InvalidOperationException` when resolved from bootstrap-branch DI scope).
- [ ] Email-uniqueness check via `SunfishBridgeReadOnlyDbContext` (W#79 disposition of ADR 0095 Q4 = option b; §6.1 Decision 8); explicitly minimal `OnModelCreating` (no HasQueryFilter inheritance).
- [ ] `IBootstrapTenantRegistry.CreatePendingAsync` first production callsite working; control-plane `TenantRegistrations` write succeeds; outside HasQueryFilter set.
- [ ] EF migration `AddOnboardingFieldsToTenantRegistration` applied; AdminEmailNormalized filtered-unique index in place; rollback test passes.
- [ ] VerifyEmailHandler uses `IOperationVerifier.TryVerifyAsync<VerificationTokenPayload>` per D1; audience-mismatch check; `TryConsumeEmailVerificationAsync` atomic one-shot (sec-eng C); H9 200-idempotent on already-consumed.
- [ ] ResendVerificationHandler returns uniform-202 regardless of email-existence; per-email rate-limit via SHA-256 first-16-bytes key (.NET-arch G2); D5 audit always emitted; re-sends only when tenant is unverified.
- [ ] CheckAvailabilityHandler **REMOVED** per D4 — not in scope.
- [ ] MockEmailProvider production-guard tests **M1-M14** passing per §4.2.5 (includes factory-registered M3/M4 per sec-eng H).
- [ ] MockEmailProvider console-log discipline: emits To[]/Subject/EmailDispatchId/IdempotencyKey/MessageStream only — NEVER BodyHtml/BodyText (ADR 0096 Rev 2 Amendment #6).
- [ ] InMemoryCaptchaVerifier carries `IMockVendorProvider` marker (consumed test).
- [ ] Audit emission on EVERY pre-tenant signup branch + verify-email outcome (D5); `tenant_id = SystemTenant.Id`; email full / IP SHA-256 hashed / UA truncated 200 chars / correlation_id plain; PasswordHash NEVER logged.
- [ ] §4.2.7 `MutableTenantContextSeedTests` (6 unit tests) passing.
- [ ] §4.2.8 env-var isolation discipline — all env-mutating tests use `EnvVarScope IDisposable` + `[Collection("EnvVarSerial")]`.
- [ ] §4.2.6.PD ProblemDetails per-discriminator backend tests (9 tests; one per discriminator) passing — verifies `body.title` matches the corresponding `OnboardingDiscriminators` constant; `body.error` field is NEVER emitted.
- [ ] PR description includes acceptance-criteria checklist + §A0 cited-symbol audit.
- [ ] Pre-flight commit-message check (Amendment K) clean.

#### 4.2.6.PD ProblemDetails backend per-discriminator tests (test-eng T2 fold)

Lives in `signal-bridge/Sunfish.Bridge.Tests/Onboarding/ProblemDetailsDiscriminatorTests.cs`. 9 backend tests, one per discriminator in §3.5 post-Rev-2:

| # | Test | Discriminator | Closes |
|---|---|---|---|
| PD1 | `signup payload missing email -> 400 title='validation_failed'` | `validation_failed` | T2 + Amendment J |
| PD2 | `signup with reserved slug -> 400 title='tenant_slug_reserved'` | `tenant_slug_reserved` | T2 + Amendment J |
| PD3 | `signup with taken slug -> 400 title='tenant_slug_taken'` | `tenant_slug_taken` | T2 + Amendment J |
| PD4 | `signup with invalid-shape slug -> 400 title='tenant_slug_invalid_shape'` | `tenant_slug_invalid_shape` | T2 + Amendment J |
| PD5 | `signup with failed CAPTCHA -> 400 title='captcha_failed'` | `captcha_failed` | T2 + Amendment J |
| PD6 | `verify-email with malformed token -> 400 title='verification_token_invalid'` | `verification_token_invalid` | T2 + Amendment J |
| PD7 | `verify-email with expired token -> 400 title='verification_token_expired'` | `verification_token_expired` | T2 + Amendment J |
| PD8 | `signup at 6th request in 60s -> 429 title='rate_limited' + Retry-After header` | `rate_limited` | T2 + §3.7 |
| PD9 | `signup with missing Origin -> 403 title='origin_invalid'` | `origin_invalid` | T2 + §3.6 |

Each test:
- Asserts the response body's `title` field equals the canonical `OnboardingDiscriminators.<Name>` constant (NOT a string literal — proves the const-export is the single source of truth).
- Asserts `body.error` field is NOT present (proves the fleet `title`-not-`error` convention per Amendment J).
- For PD8: asserts `Retry-After` header is present with parseable seconds value.

#### 4.2.7 `ITenantContextSeed` substrate-primitive unit tests (test-eng T3 fold)

Lives in `shipyard/packages/foundation-authorization/tests/MutableTenantContextSeedTests.cs`. **6 unit tests** pinning the substrate primitive semantics introduced at §4.1.4:

| # | Test | Closes |
|---|---|---|
| T3.1 | `Bind once + read succeeds` (seed.Bind(tenantId); seededContext.TenantId == tenantId) | Happy-path semantics |
| T3.2 | `Read before bind throws InvalidOperationException with message naming "not bound"` | Pre-Bind resolution invariant; prevents Guid.Empty HasQueryFilter foot-gun |
| T3.3 | `Bind twice throws InvalidOperationException with message naming "already seeded"` | One-shot bind semantics; prevents silent tenant-mutation |
| T3.4 | `Concurrent Bind from two threads: one wins (Interlocked.CompareExchange), the other throws` | Thread-safety semantics; prevents silent last-write-wins races |
| T3.5 | `SeededTenantContext.TenantId returns the bound Guid after Bind via Interlocked-protected read` | Volatile.Read invariant |
| T3.6 | `SeededTenantContext.TenantId accessed in a scope where seed was never bound throws via the seed-holder's pre-Bind invariant` | Seed-holder propagates error to facade adapter |

These tests ship in PR 0 (substrate primitives ship in PR 0 per the directive); handler integration tests in PR 1 are the integration-tier complement.

#### 4.2.8 Env-var test isolation discipline (test-eng T5 + ADR 0096 .NET-arch A5)

Tests in `MockProviderGuardIntegrationTests.cs` and any other test class that mutates `Environment.SetEnvironmentVariable` MUST satisfy ONE of:

1. **Preferred: `IEnvironment` abstraction.** Production code reads via `IEnvironment.GetVariable(name)`; tests inject a mock `IEnvironment` with per-test state. No real `Environment` mutation; no isolation problem. (If ADR 0096 substrate ships `IEnvironment`, W#79 consumes; if not, W#79 falls back to option 2 — Engineer's PR 1 verifies.)

2. **Acceptable: `IDisposable` restoration + xUnit `[Collection]` discipline.**
   - Test class declares `[Collection("EnvVarSerial")]` to opt out of parallel execution.
   - Test bodies use `EnvVarScope : IDisposable` helper (shipped in `Sunfish.Bridge.Tests/Onboarding/EnvVarScope.cs`) that captures original value + restores on Dispose:
     ```csharp
     using var _ = new EnvVarScope("ASPNETCORE_ENVIRONMENT", "Production");
     using var __ = new EnvVarScope("SUNFISH_ALLOW_MOCK_PROVIDERS", "true");
     // ... test body ...
     // Dispose restores original values in reverse order.
     ```
   - Test asserts post-test that env vars are restored to baseline (defensive integration-tier guard).

**PROHIBITED in W#79 tests:**
- Naked `Environment.SetEnvironmentVariable(...)` without IDisposable restoration.
- Setting env vars in `[Fact]`-body code without `[Collection]` serialization.
- Cross-test env-var dependencies (each test must declare its own EnvVarScope; never rely on a prior test's env-var state).

### 4.3 PR 2 — Frontend rebind (sunfish)

**Repo:** `sunfish`
**Branch suggestion:** `feat/w79-pr2-onboarding-frontend-rebind`
**Estimated lines:** ~600-900 (SignupPage + VerifyEmailPage + auth hooks + typed-error contracts + RTL tests)
**Estimated effort:** 2-3 days FED time
**Council SPOT-CHECK on Ready-flip:** sec-eng (pattern-009 PAIR — frontend half; cycle-1 expects AMBER per §3.9 cascade plan) + frontend-architect (MANDATORY; first-instance signup flow).

#### 4.3.1 Files touched

| File | Action | Notes |
|---|---|---|
| `sunfish/apps/web/src/pages/auth/SignupPage.tsx` | new or rewrite | Multi-step form: email + password + tenant_slug + tenant_display_name + captcha widget; submit → `POST /api/v1/auth/signup` |
| `sunfish/apps/web/src/pages/auth/VerifyEmailPage.tsx` | new or rewrite | Receives `?token=<verification_token>` query param; submits → `POST /api/v1/auth/verify-email` |
| `sunfish/apps/web/src/pages/auth/VerifyEmailPendingPage.tsx` | new | Static "check your inbox" page; reached post-signup-accept; "Resend" button → `POST /api/v1/auth/resend-verification` |
| `sunfish/apps/web/src/api/onboarding.ts` | new | TanStack mutation hooks: `useSignup`, `useVerifyEmail`, `useResendVerification`; typed-error contracts per §3.5. (Rev 2: `useCheckAvailability` REMOVED per D4.) |
| `sunfish/apps/web/src/api/onboarding-discriminators.ts` | new | Const-export single-source-of-truth for the 9 discriminator string literals per §3.5 + test-eng T2 + .NET-arch F2; consumed by typed-error class definitions + RTL per-discriminator tests + MSW handlers + MSW-vs-Bridge parity test. |
| `sunfish/apps/web/src/api/onboarding.types.ts` | new | TypeScript interfaces matching §3.1-§3.4 wire shapes (POSITIVE matches only; negative-match fields are NOT declared per Amendment I discipline) |
| `sunfish/apps/web/src/components/CaptchaWidget.tsx` | new | Wraps Turnstile widget; in dev/mock mode renders a `mock-pass` token + checkbox UX |
| `sunfish/apps/web/src/pages/auth/__tests__/SignupPage.test.tsx` | new | ≥10 RTL tests per §4.3.4 |
| `sunfish/apps/web/src/pages/auth/__tests__/VerifyEmailPage.test.tsx` | new | ≥6 RTL tests |
| `sunfish/apps/web/src/api/__tests__/onboarding.test.ts` | new | Typed-error contract tests + hook behavior |

#### 4.3.2 `useSignup` hook spec

```typescript
import { useMutation } from '@tanstack/react-query';
import type { SignupRequest, SignupAcceptedResponse } from './onboarding.types';

export class ValidationFailedError extends Error { readonly cause = 'validation_failed'; }
export class TenantSlugTakenError extends Error { readonly cause = 'tenant_slug_taken'; }
export class TenantSlugReservedError extends Error { readonly cause = 'tenant_slug_reserved'; }
export class TenantSlugInvalidShapeError extends Error { readonly cause = 'tenant_slug_invalid_shape'; }
export class CaptchaFailedError extends Error { readonly cause = 'captcha_failed'; }
export class RateLimitedError extends Error {
  readonly cause = 'rate_limited';
  constructor(public readonly retryAfterSeconds: number) { super('Rate limit exceeded'); }
}

export function useSignup() {
  return useMutation<SignupAcceptedResponse, Error, SignupRequest>({
    mutationFn: async (request) => {
      const response = await fetch('/api/v1/auth/signup', {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
          // Origin set by browser automatically for same-site POSTs
        },
        body: JSON.stringify(request),
      });

      if (response.status === 429) {
        const retryAfter = parseInt(response.headers.get('Retry-After') ?? '60', 10);
        throw new RateLimitedError(retryAfter);
      }

      if (!response.ok) {
        const body = await response.json();
        // Per Amendment J: discriminator is in body.title, NOT body.error.
        switch (body.title) {
          case 'validation_failed':         throw new ValidationFailedError(body.detail ?? '');
          case 'tenant_slug_taken':         throw new TenantSlugTakenError();
          case 'tenant_slug_reserved':      throw new TenantSlugReservedError();
          case 'tenant_slug_invalid_shape': throw new TenantSlugInvalidShapeError();
          case 'captcha_failed':            throw new CaptchaFailedError();
          default: throw new Error(`Unknown error: ${body.title}`);
        }
      }

      return response.json() as Promise<SignupAcceptedResponse>;
    },
  });
}
```

Same shape for `useVerifyEmail` + `useResendVerification` per §3.2/3.3 shapes. (`useCheckAvailability` REMOVED per Rev 2 D4.) Typed-error classes consume the const-export from `onboarding-discriminators.ts` to pin discriminator string literals to a single source of truth per .NET-arch F2 + test-eng T2.

**Post-Rev-2 typed-error class roster (8 classes):**
- `ValidationFailedError` (`validation_failed`)
- `TenantSlugTakenError` (`tenant_slug_taken`)
- `TenantSlugReservedError` (`tenant_slug_reserved`)
- `TenantSlugInvalidShapeError` (`tenant_slug_invalid_shape`)
- `CaptchaFailedError` (`captcha_failed`)
- `RateLimitedError` (`rate_limited`, carries retryAfterSeconds)
- `VerificationTokenInvalidError` (`verification_token_invalid`)
- `VerificationTokenExpiredError` (`verification_token_expired`)

Plus `OriginInvalidError` rendered as transport-failure banner (403 non-user-correctable; not in the typed-error switch since the user cannot resolve client-side).

**Rev 2 retirement:** `EmailAlreadyRegisteredError` REMOVED (H2 RATIFY); no email-already-registered branch in `useSignup` error switch.

#### 4.3.3 Cycle-1 DRAFT posture (per §3.9 cascade plan Amendment L)

**Cycle 1 (FED PR 2 OPEN, DRAFT):**
- SignupPage form-rendering + state machine + client-side validation wired.
- `useSignup` hook bound to real `/api/v1/auth/signup` URL.
- IF PR 0 (Bridge) ships scaffold-only (501): SignupPage renders a "service not yet available — your account couldn't be created" banner cleanly with `// TODO(w79-pr1): real handler body lands in PR 1 — remove this banner once 501s stop` forward-watch comment.
- IF PR 0 + PR 1 both shipped: full submission flow works against the live handler.
- Cycle-1 sec-eng + frontend-arch dispatch expects AMBER per Amendment L.

**Cycle 2 (FED PR 2 amendment commit, post-Engineer-PR-1-merge):**
- Banner removed; full happy-path flow operational.
- Error-handling fully wired; typed-error contracts per §3.5 land in this cycle.
- Cycle-2 re-attest expects GREEN; auto-merge cascade fires.

**Forbidden:** silently-dead-code (a wired Submit button that does nothing) or a structurally-present-functionally-absent CAPTCHA widget. Cleanly-removed-with-forward-watch is AMBER; silently-dead-code is RED. Cohort-4 sunfish#71 RED is the canonical precedent trap.

#### 4.3.4 Test expectations — `SignupPage.test.tsx` (Rev 2: 12 baseline tests, retired EmailAlreadyRegistered)

Minimum 12 React Testing Library tests (Rev 1 had 12 with `email_already_registered` test #5; Rev 2 replaces #5 with `202 navigates to verify-email/pending` confirmation since H2 RATIFY drops the 400 discriminator):

| # | Test | Closes |
|---|---|---|
| 1 | `renders signup form with email + password + slug + display-name fields` | baseline |
| 2 | `client-side validation prevents submit when slug fails regex` | baseline + Amendment J discriminator (matches `tenant_slug_invalid_shape` server-side discriminator) |
| 3 | `submission posts canonical JSON shape to /api/v1/auth/signup` | §3.1 wire contract |
| 4 | `202 response navigates to /auth/verify-email/pending with email_dispatch_id` | §3.1 happy path |
| 5 | `(Rev 2 RETIRED) email_already_registered branch removed per H2 RATIFY` — replaced by test verifying signup with already-verified email STILL navigates to verify-email/pending (no error branch) | §3.5 H2 RATIFY |
| 6 | `400 tenant_slug_taken surfaces field-scoped error on slug input` | §3.5 typed error |
| 7 | `429 rate_limited displays Retry-After countdown` | §3.7 rate-limit + Retry-After header read |
| 8 | `403 origin_invalid surfaces transport-failure banner (NOT user-correctable)` | §3.6 origin |
| 9 | `captcha widget renders in mock mode with mock-pass token shape` | dev-mode CAPTCHA |
| 10 | `frontend does NOT declare tenant_id or verification_token in TypeScript interfaces` | Amendment I negative-match (static-check via type assertion test) |
| 11 | `error handler reads body.title, never body.error` | Amendment J discriminator pin |
| 12 | `password field does not log to console on submit` | defense-in-depth (no-log-secret) |

#### 4.3.4.PD Per-discriminator RTL pinning tests (test-eng T2 fold)

Lives in `sunfish/apps/web/src/api/__tests__/onboarding-discriminators.test.tsx` (or co-located with the typed-error class definitions). **9 tests, one per discriminator** in §3.5 post-Rev-2; each imports the discriminator string from the `SignupDiscriminator` const-export to pin single-source-of-truth:

| # | Test | Discriminator | Closes |
|---|---|---|---|
| T2.1 | `400 with title=SignupDiscriminator.VALIDATION_FAILED surfaces ValidationFailedError instance` | `validation_failed` | T2 + Amendment J |
| T2.2 | `400 with title=SignupDiscriminator.TENANT_SLUG_TAKEN surfaces TenantSlugTakenError` | `tenant_slug_taken` | T2 + Amendment J |
| T2.3 | `400 with title=SignupDiscriminator.TENANT_SLUG_RESERVED surfaces TenantSlugReservedError` | `tenant_slug_reserved` | T2 + Amendment J |
| T2.4 | `400 with title=SignupDiscriminator.TENANT_SLUG_INVALID_SHAPE surfaces TenantSlugInvalidShapeError` | `tenant_slug_invalid_shape` | T2 + Amendment J |
| T2.5 | `400 with title=SignupDiscriminator.CAPTCHA_FAILED surfaces CaptchaFailedError` | `captcha_failed` | T2 + Amendment J |
| T2.6 | `400 with title=SignupDiscriminator.VERIFICATION_TOKEN_INVALID surfaces VerificationTokenInvalidError` (VerifyEmailPage) | `verification_token_invalid` | T2 + Amendment J |
| T2.7 | `400 with title=SignupDiscriminator.VERIFICATION_TOKEN_EXPIRED surfaces VerificationTokenExpiredError + resend CTA` (VerifyEmailPage) | `verification_token_expired` | T2 + Amendment J |
| T2.8 | `429 with title=SignupDiscriminator.RATE_LIMITED surfaces RateLimitedError + retryAfterSeconds parsed from Retry-After header` | `rate_limited` | T2 + §3.7 |
| T2.9 | `403 origin_invalid surfaces transport-failure banner (not user-correctable; OriginInvalidBanner component renders)` | `origin_invalid` | T2 + §3.6 |

Each test ALSO asserts:
- `body.title === SignupDiscriminator.<NAME>` (using the imported const; if the const string drifts, the test fails — pins single-source-of-truth).
- `body.error` field is NOT consulted (proves the fleet `title`-not-`error` convention per Amendment J).

**Cross-stack contract test** (`onboarding-discriminator-contract.test.ts`) asserts the TypeScript `SignupDiscriminator` const-export object matches the C# `OnboardingDiscriminators` static-class field set byte-for-byte. Without this test, TS and C# can silently drift.

#### 4.3.7 Selector strategy floor (test-eng T4 fold — cohort-3 buglog 642/643 precedent)

All RTL + Playwright tests in W#79 MUST use one of:
- `findByRole(...)` / `getByRole(...)` for semantic elements (headings, buttons, textboxes, links).
- `findByLabelText(...)` / `getByLabelText(...)` for form fields.
- `findByTestId(...)` / `getByTestId(...)` for components without a semantic role.

**PROHIBITED in W#79 tests:**
- Bare `getByText(...)` / `findByText(...)` for headings or buttons (use `getByRole('heading' | 'button', { name })` instead).
- `getByDisplayValue(...)` for empty inputs (use `getByLabelText` + assert `value`).

**Cohort-3 buglog precedent:** bug 642 (ArAging) + bug 643 (TrialBalance) both surfaced as RTL multi-match where `getByText` hit subtitle + heading. Fix in both cases was `findByRole('heading')`. W#79 is greenfield; the floor can be set proactively with zero migration cost.

**Reviewer-discipline gate:**
- PR 2 frontend-architect SPOT-CHECK verifies no `getByText` calls on heading/button/role-bearing elements.
- PR 3 test-eng-council SPOT-CHECK extends the gate to Playwright `page.getByText` calls.
- (Optional follow-on:) An ESLint rule banning bare `getByText` in `*.test.tsx` files lands as a separate fleet PR (forward-watch).

#### 4.3.5 Test expectations — `VerifyEmailPage.test.tsx`

Minimum 6 RTL tests:

| # | Test | Closes |
|---|---|---|
| 1 | `reads token from URL query param and submits to /api/v1/auth/verify-email` | §3.2 wire contract |
| 2 | `200 response surfaces verified-tenant-display-name in welcome message` | happy path |
| 3 | `400 verification_token_invalid surfaces "token expired or invalid" message` | §3.5 typed error |
| 4 | `400 verification_token_expired surfaces "resend" CTA` | §3.5 typed error + UX |
| 5 | `frontend does NOT declare tenant_id or session_token` | Amendment I negative-match |
| 6 | `verification_token NEVER logged or rendered to DOM beyond the URL` | defense-in-depth |

#### 4.3.6 Acceptance criteria for PR 2 (Rev 2)

- [ ] All 12 SignupPage RTL tests (§4.3.4) + all 6 VerifyEmailPage RTL tests (§4.3.5) + 9 per-discriminator RTL tests (§4.3.4.PD) passing.
- [ ] `sunfish/apps/web/src/api/onboarding-discriminators.ts` const-export ships; typed-error class definitions import from it (NOT inline string literals).
- [ ] Cross-stack contract test (`onboarding-discriminator-contract.test.ts`) asserts TS `SignupDiscriminator` const-export object matches C# `OnboardingDiscriminators` static-class field set byte-for-byte (9 string constants on each side; passing test = ZERO divergence).
- [ ] §4.3.7 selector-strategy floor enforced — no `getByText` on heading/button/role-bearing elements; reviewer SPOT-CHECK gates the discipline.
- [ ] TypeScript interfaces in `onboarding.types.ts` carry ONLY POSITIVE-match fields per §3.1-§3.3 (no `tenant_id`, no `verification_token`, no `session_token`, no `password_hash`).
- [ ] Cycle-1 DRAFT posture is AMBER per §3.9 + §4.3.3 (clean banner + forward-watch comment); Cycle-2 amendment removes banner.
- [ ] `EmailAlreadyRegisteredError` typed-error class NOT shipped (retired per Rev 2 H2 RATIFY).
- [ ] PR description includes acceptance-criteria checklist + `@candidate-pattern: pattern-009-w79-onboarding-signup-pair` claim per fleet-conventions PR-description convention.
- [ ] Pre-flight commit-message check (Amendment K) clean.

### 4.4 PR 3 — e2e + contract tests (sunfish / cross-stack)

**Repo:** `sunfish` (Playwright e2e + cross-stack contract tests)
**Branch suggestion:** `feat/w79-pr3-onboarding-e2e-contract-tests`
**Estimated lines:** ~400-600 (Playwright spec + MSW contract handlers + cross-stack assertion suite)
**Estimated effort:** 1-2 days FED/test-eng time
**Council SPOT-CHECK on Ready-flip:** test-eng-council (MANDATORY per ADR 0093 Rev 4 trigger matrix — Stage-05 hand-off has >5 test cases as acceptance criteria + substrate-touching PR; coverage-model verification) + frontend-architect (optional; SPA flow assertion).

#### 4.4.1 Files touched

| File | Action | Notes |
|---|---|---|
| `sunfish/apps/web/e2e/onboarding-happy-path.spec.ts` | new | Playwright; spins up bridge in test env; full signup → verify-email → land on welcome |
| `sunfish/apps/web/e2e/onboarding-rate-limit.spec.ts` | new | Playwright; submits signup 6× rapidly → 6th gets 429 + Retry-After UX |
| `sunfish/apps/web/e2e/onboarding-mock-email-inbox.spec.ts` | new | Playwright; against test-env MockEmailProvider; asserts email body NOT in server log; asserts in-memory store carries the verification link |
| `sunfish/apps/web/src/api/__tests__/onboarding-contract.test.ts` | new | MSW-handler-based contract tests per Amendment M (forward-watch promotion to MUST once MSW infra ships fleet-wide); pins typed-error contracts against server DTO source |
| `sunfish/apps/web/msw/onboarding-handlers.ts` | new | MSW handlers for `/api/v1/auth/*` mirroring server DTO shapes per §3.1-§3.4 |

#### 4.4.2 Cross-stack invariants asserted (Rev 2 — drops check-availability + adds MSW parity)

- **Happy path:** signup → 202 with email_dispatch_id → verify-email page accepts token from email body → 200 with tenant_slug + tenant_display_name. Full stack alive.
- **Rate-limit floor:** 6 rapid signups from same IP → 6th returns 429 with `Retry-After` header set.
- **Mock-email inbox:** signup → MockEmailProvider receives the EmailMessage with `MessageStream: "transactional-onboarding"`; the in-memory store accessible via test-only inspection API (per H1 RATIFY option c — Bridge test-only inspection endpoint; UI deferred to W#80); the server log does NOT contain `BodyText` or `BodyHtml` (ADR 0096 Rev 2 Amendment #6 verification).
- **Mock-vendor production-guard fires:** test fixture spins up `WebApplicationBuilder` with `ASPNETCORE_ENVIRONMENT=Production` + no opt-out → `IHost.StartAsync` throws `MockInProductionException`. (Covered already by PR 1 §4.2.5 tests M1-M14; PR 3 cross-links.)
- **α-1 child-scope correctness:** signup completes → User aggregate written via `IUserAggregateRepository` resolved from child scope → outer bootstrap scope's audit emission carries correct correlation-id. (Asserted via cross-stack log inspection.)
- **Bridge endpoint conformance to ProblemDetails:** all 4xx responses carry `title` field (not `error`); verified by MSW-handler authoring against real Bridge response capture.
- **(Rev 2 new) MSW-vs-real-Bridge parity (test-eng T6 B2):** for each of the 9 discriminator paths (§3.5), the cross-stack parity test:
  - Spins up the test-env Bridge.
  - Drives the Bridge handler down the matching error path via crafted request.
  - Captures the actual Bridge response body + status + headers.
  - Asserts the MSW handler in `sunfish/apps/web/msw/onboarding-handlers.ts` returns a byte-for-byte identical body + status + Retry-After header (for `rate_limited`).
  - Asserts MSW handler emits discriminator strings sourced from the `SignupDiscriminator` const-export — proves single-source-of-truth all the way through.
  - Without parity, MSW lies are invisible to the cycle-1 test suite (per test-eng T6).
- **H2 RATIFY always-202 byte-for-byte:** 3 signup paths (fresh / unverified-existing / verified-existing) ALL return byte-for-byte identical 202 envelopes. Playwright test fires 3 signups (one per state) + asserts response.body equality.
- **(Rev 2 new) Constant-work latency floor (sec-eng D):** Bridge integration test fires 100 signups against the 3 H2 paths (fresh / unverified-existing / verified-existing); asserts p50/p95 latency falls within `+/- 25%` across paths. (CI flake-resilient bound; loosen to `+/- 35%` if CI runner variance proves higher.)

#### 4.4.3 Acceptance criteria for PR 3 (Rev 2)

- [ ] All Playwright e2e specs pass against test-env Bridge + MockEmailProvider.
- [ ] MSW handlers in `onboarding-handlers.ts` mirror server DTO shapes byte-for-byte per §3.1-§3.3 (check-availability handlers REMOVED per D4).
- [ ] MSW-vs-real-Bridge parity test (§4.4.4 below) passes for all 9 discriminators.
- [ ] MSW handlers consume discriminator strings from `SignupDiscriminator` const-export (NO inline string literals).
- [ ] Contract-test suite asserts every 400/429/403 discriminator from §3.5 has a corresponding typed-error class (8 typed-errors + 1 transport-banner; `EmailAlreadyRegisteredError` ABSENT per H2 RATIFY).
- [ ] §4.4.5 H2 RATIFY 3-path byte-for-byte 202 equality test passes.
- [ ] §4.4.6 constant-work latency floor test passes (`+/- 25%` p50/p95 across 3 H2 paths over 100 requests each).
- [ ] §4.3.7 selector-strategy floor extended to Playwright `page.getByText` calls (no bare getByText on headings/buttons).
- [ ] test-eng-council SPOT-CHECK GREEN on Ready-flip (coverage-model gate per ADR 0093 Rev 4).
- [ ] PR description references the 12 SignupPage + 6 VerifyEmailPage + 9 per-discriminator tests from PR 2 as the unit-tier; PR 3's contribution is the integration + e2e tier.
- [ ] Pre-flight commit-message check (Amendment K) clean.

#### 4.4.4 MSW-vs-real-Bridge parity test (Rev 2 — test-eng T6 B2)

File: `sunfish/apps/web/src/api/__tests__/onboarding-msw-parity.test.ts`. Single test suite, 9 test cases (one per discriminator):

```typescript
describe('MSW handlers byte-for-byte match real Bridge', () => {
  for (const discriminator of Object.values(SignupDiscriminator)) {
    it(`${discriminator}: MSW response equals Bridge response`, async () => {
      // 1. Drive Bridge handler down the error path matching `discriminator`.
      const bridgeResponse = await fireBridgeErrorPath(discriminator);
      // 2. Drive MSW handler down the same path.
      const mswResponse = await fireMswErrorPath(discriminator);
      // 3. Byte-for-byte body + status + Retry-After header equality.
      expect(mswResponse.status).toBe(bridgeResponse.status);
      expect(await mswResponse.json()).toEqual(await bridgeResponse.json());
      expect(mswResponse.headers.get('Retry-After'))
        .toBe(bridgeResponse.headers.get('Retry-After'));
    });
  }
});
```

Helper `fireBridgeErrorPath` spins up the in-memory test-env Bridge + sends a crafted request that lands on the named discriminator's error branch. Helper `fireMswErrorPath` invokes the MSW handler via `msw/server.use` + `fetch`. Test passes iff every (body, status, headers) tuple matches.

#### 4.4.5 H2 RATIFY 3-path equality test (Rev 2)

File: `sunfish/apps/web/e2e/onboarding-h2-equality.spec.ts`. Playwright spec:

```typescript
test('signup with fresh / unverified-existing / verified-existing emails return identical 202 envelopes', async ({ request }) => {
  const responses = await Promise.all([
    request.post('/api/v1/auth/signup', { data: freshSignupBody }),
    request.post('/api/v1/auth/signup', { data: unverifiedExistingSignupBody }),
    request.post('/api/v1/auth/signup', { data: verifiedExistingSignupBody }),
  ]);
  // All 3 must be 202.
  for (const r of responses) expect(r.status()).toBe(202);
  // All 3 must have IDENTICAL envelope shape (the email_dispatch_id values differ;
  // strip them before equality; ensure no other fields leak the email-state).
  const bodies = await Promise.all(responses.map(r => r.json()));
  const stripped = bodies.map(b => ({ ...b, email_dispatch_id: 'REDACTED' }));
  expect(stripped[0]).toEqual(stripped[1]);
  expect(stripped[1]).toEqual(stripped[2]);
});
```

#### 4.4.6 Constant-work latency floor test (Rev 2 — sec-eng D)

File: `signal-bridge/Sunfish.Bridge.Tests/Onboarding/SignupHandlerTimingTests.cs`. Bridge integration test (NOT Playwright — runs in test-env Bridge directly to avoid network noise):

```csharp
[Fact]
public async Task SignupHandler_constant_work_p95_latency_within_25_percent_across_H2_paths()
{
    const int iterations = 100;
    var freshLatencies = await RunSignupBatch(freshEmailFactory, iterations);
    var unverifiedLatencies = await RunSignupBatch(unverifiedExistingEmailFactory, iterations);
    var verifiedLatencies = await RunSignupBatch(verifiedExistingEmailFactory, iterations);

    var p50Fresh = Percentile(freshLatencies, 0.50);
    var p50Unverified = Percentile(unverifiedLatencies, 0.50);
    var p50Verified = Percentile(verifiedLatencies, 0.50);

    var p50Range = Math.Max(p50Fresh, Math.Max(p50Unverified, p50Verified)) -
                   Math.Min(p50Fresh, Math.Min(p50Unverified, p50Verified));
    var p50Mid = (p50Fresh + p50Unverified + p50Verified) / 3;
    Assert.True(p50Range / p50Mid < 0.25,
        $"p50 latency variance {p50Range/p50Mid:P} exceeds 25% floor; " +
        $"constant-work discipline (sec-eng D) likely regressed.");

    // Same assertion for p95.
}
```

CI flake-resilience: if CI runner variance proves higher than ~25%, the bound loosens to `+/- 35%`; floor remains `+/- 50%` as the absolute regression detector. Engineer's PR 1 ratifies the actual bound based on first 5 CI runs.

---

## 5. Test coverage matrix — cumulative W#79

When all 4 PRs land, the cumulative cohort coverage matrix:

| Test name | PR | Closes | Layer |
|---|---|---|---|
| `IBootstrapContext registered scoped; tenant interfaces unreachable in bootstrap scope` | PR 0 | ADR 0095 Layer 1 | DI |
| `MapBootstrapEndpoints chain registers 4 onboarding routes with AllowAnonymous` | PR 0 | ADR 0095 Amendment 6 | route |
| `Rate-limit policies fire 6× signup → 429 + Retry-After` | PR 0 | ADR 0095 Amendment 7 | middleware |
| `Origin missing on POST → 403 non-diagnostic` | PR 0 | ADR 0095 Amendment 5 (Gap C) | middleware |
| `captcha_token >2048 chars → 400 validation_failed` | PR 0 | ADR 0095 Amendment 5 | middleware |
| `SignupHandler validates payload + checks uniqueness + creates tenant + child-scope-writes user + emits email` | PR 1 | 6-step α-1 lifecycle | handler |
| `ITenantContextSeed.Bind() called before any post-tenant resolution inside child scope` | PR 1 | ADR 0095 Rev 2 Amendment 2 (α-1) | handler |
| `SunfishBridgeDbContext resolved in bootstrap scope throws InvalidOperationException` | PR 1 | ADR 0095 Rev 2 Amendment 4 (Gap B) | DbContext guard |
| `VerifyEmailHandler validates token + marks user verified inside child scope` | PR 1 | ADR 0095 lifecycle | handler |
| `ResendVerificationHandler returns uniform-202 regardless of email existence` | PR 1 | enumeration-leak defense | handler |
| ~~CheckAvailabilityHandler returns uniform boolean~~ | (REMOVED per D4) | (n/a — endpoint dropped) | (n/a) |
| `MockEmailProvider production-guard throws when env=Production + no opt-out` | PR 1 | ADR 0096 D1c | IHostedService |
| `MockEmailProvider production-guard succeeds when SUNFISH_ALLOW_MOCK_PROVIDERS=true` | PR 1 | ADR 0096 D1c opt-out | IHostedService |
| `MockEmailProvider console log captures To+Subject+EmailDispatchId; NEVER BodyHtml/BodyText` | PR 1 | ADR 0096 Rev 2 Amendment #6 | provider |
| `InMemoryCaptchaVerifier marked IMockVendorProvider` | PR 1 | ADR 0096 Step 1 retrofit | DI |
| `SignupPage renders form; submits canonical JSON shape` | PR 2 | §3.1 wire contract | RTL |
| `SignupPage reads body.title (NOT body.error) on 400` | PR 2 | Amendment J discriminator pin | RTL |
| `SignupPage 429 displays Retry-After countdown` | PR 2 | §3.7 | RTL |
| `SignupPage TypeScript interface does NOT declare tenant_id/verification_token` | PR 2 | Amendment I negative-match | RTL/type-assert |
| `VerifyEmailPage reads token from URL + submits + surfaces tenant_display_name on success` | PR 2 | §3.2 wire contract | RTL |
| `VerifyEmailPage verification_token NEVER logged or rendered to DOM` | PR 2 | defense-in-depth | RTL |
| `Playwright happy-path: signup → verify-email → land on welcome` | PR 3 | full-stack | e2e |
| `Playwright rate-limit floor: 6× signup → 429` | PR 3 | §3.7 | e2e |
| `Playwright mock-email-inbox: BodyText not in server log; in-memory store carries link` | PR 3 | ADR 0096 #6 + dev-inbox | e2e |
| `MSW contract handlers byte-for-byte mirror server DTO shapes` | PR 3 | Amendment M (forward-watch) | contract |

**Cumulative test count target (Rev 2 fold):** ~90 new tests across substrate-DI / handler / page / e2e layers (expanded from Rev 1's ~25). Distribution:

| PR | Tier | Test count | Notes |
|---|---|---|---|
| PR 0 | Substrate-DI integration | 5 | §4.1.5 (routing + AllowAnonymous + rate-limit floor + Origin reject + length-cap reject) |
| PR 0 | Substrate primitive unit | 6 | §4.2.7 MutableTenantContextSeedTests (T3 fold) |
| PR 0 | EF migration smoke | 1 | apply + rollback for AddOnboardingFieldsToTenantRegistration |
| PR 1 | Handler integration | 13 | §4.2.6 baseline 9 + sec-eng C one-shot + sec-eng D constant-work + audit-emission test + auth-mismatch test |
| PR 1 | Production-guard | 14 | §4.2.5 M1-M14 (T1 expansion + sec-eng H factory-registered M3/M4) |
| PR 1 | ProblemDetails per-discriminator (backend) | 9 | §4.2.6.PD (T2 fold) |
| PR 2 | RTL SignupPage | 12 | §4.3.4 baseline (#5 reshaped post-H2 RATIFY) |
| PR 2 | RTL VerifyEmailPage | 6 | §4.3.5 baseline |
| PR 2 | Per-discriminator RTL | 9 | §4.3.4.PD (T2 fold) |
| PR 2 | Const-export contract | 1 | cross-stack TS-vs-C# const equality |
| PR 3 | Playwright e2e | 3 | §4.4.1 (happy-path + rate-limit + mock-inbox) |
| PR 3 | MSW-vs-Bridge parity | 9 | §4.4.4 (T6 B2 fold; one per discriminator) |
| PR 3 | H2 RATIFY 3-path equality | 1 | §4.4.5 (Rev 2) |
| PR 3 | Constant-work latency floor | 1 (1 test, 2 assertions: p50 + p95) | §4.4.6 (sec-eng D fold) |
| **Cumulative** | | **~90 tests** | substrate-tier coverage-precision lift per test-eng T1+T2+T3+T6 |

Sister cohort precedent: cohort-4 audit-trail-viewer (W#60) shipped with ~45 cumulative tests at similar surface area; W#79's larger count reflects the new substrate primitives + ProblemDetails per-discriminator discipline (the highest-value bug-prevention investment per Amendment J).

---

## 6. Stage-05 adversarial review framework spec (ADR 0093 Rev 4)

Per ADR 0093 Rev 4 §"Adversarial Brief — template" + the 5-8 (or 12 for substrate-shaping) bullet cap: W#79 is substrate-shaping (consumes 2 new substrate ADRs; introduces 3 new routes; first-instance candidate for 3 patterns). The brief extends to up to 12 bullets per the substrate-shaping escape hatch.

### 6.1 Adversarial Brief

#### Decision 1 — `/api/v1/auth/*` path prefix on bootstrap pipeline branch

- **Decision summary:** Use `/api/v1/auth/*` prefix for signup-family endpoints; bootstrap pipeline branch's `UseWhen` predicate matches `/api/v1/auth/` AND `/api/invitations/accept`. ADR 0095 §"Pipeline routing" used `/api/signup` — W#79 uses `/api/v1/auth/*` for versioned auth API consistency. (Halt H4 surfaces this for Admiral reconciliation.)
- **Worst-case interpretation:** An attacker discovers that the bootstrap-branch predicate is path-prefix-based; crafts a non-onboarding endpoint at `/api/v1/auth/leaky` and observes that it skips `TenantSubdomainResolutionMiddleware`, exposing tenant-bound surfaces via the bootstrap branch.
- **Failure mode:** Cross-tenant data leak via a non-onboarding endpoint accidentally routed through the bootstrap branch.
- **Mitigation in this hand-off:** `MapBootstrapEndpoints` chains only `MapOnboardingEndpoints` (W#79) + `MapInvitationsEndpoints` (W#82); the `UseWhen` predicate matches narrow path prefixes, not the whole `/api/v1/`. Step 3 Roslyn analyzer enforces that any handler registered via `MapBootstrapEndpoints` does NOT inject post-tenant interfaces. Sec-eng SPOT-CHECK on PR 0 verifies the prefix list.

#### Decision 2 — `email_already_registered` shape RESOLVED per H2 RATIFY (always-202)

- **Decision summary (Rev 2):** RESOLVED per Admiral ruling H2 RATIFY (b+c — always-202 + OOB notification + per-email rate-limit). Signup against ANY email-state (fresh / unverified-existing / verified-existing) returns 202 with byte-for-byte identical envelope. Verified-existing path additionally dispatches an OOB "someone tried to sign up with your address" notification email; per-email per-day dedup on the OOB dispatch prevents amplification.
- **Worst-case interpretation:** Previously: asymmetric response leaks verified-tenant existence. Post-Rev-2: caller cannot distinguish the three email-states by response shape; constant-work discipline (sec-eng D) closes the timing-distinguisher; OOB notification gives the legitimate-account-owner a fraud-detection signal.
- **Failure mode (Rev 2):** Closed at substrate-tier. Frontend has NO email-state-aware UX branching; signup always navigates to "check your inbox" page.
- **Mitigation in this hand-off (Rev 2):** Already-202 enforced at handler tier; OOB notification per-email-per-day dedup (`IdempotencyKey: $"oob-existing-{tenantId}-{date:yyyy-MM-dd}"`); constant-work passwordHasher.HashPassword(...) per-request closes timing channel; D5 audit emits to SUNFISH_SYSTEM_TENANT partition for fraud-pattern forensics (high-rate enumeration-campaign signal). Frontend retires `EmailAlreadyRegisteredError` typed-error per §3.5.

#### Decision 3 — Verification token replay window RESOLVED per H9 RATIFY + sec-eng C one-shot

- **Decision summary (Rev 2):** RESOLVED per Admiral ruling H9 RATIFY (a + d — 200-idempotent + TTL reduced from 24h to 1h) + sec-eng C blocking finding (one-shot consumption via server-stored `TenantRegistration.EmailVerified` flag, atomically transitioned inside the verify-email handler).
- **Worst-case interpretation (Rev 1):** captured token replays successfully after legit user verified; attacker reads tenant_slug + display_name back indefinitely until 24h TTL.
- **Worst-case interpretation (Rev 2):** captured token can be replayed ONCE within 1h TTL; on first valid verify, the handler atomically marks `EmailVerified=true`; subsequent replays land on the consumed-path 200-idempotent shape but the underlying state has no further mutation. Attacker who steals a fresh token gets at most one verify-success in <1h window; legitimate user's subsequent visits land on idempotent 200 (no UX regression).
- **Failure mode:** Closed at substrate-tier. Replay-readable window narrowed from 24h to 1h TTL + bounded by atomic consumption flag.
- **Mitigation in this hand-off (Rev 2):**
  - Token TTL reduced 24h → 1h per H9.
  - `TenantRegistration.EmailVerified: bool` field added per D2 schema extension.
  - `IBootstrapTenantRegistry.TryConsumeEmailVerificationAsync` is the atomic gate: reads the row, asserts `EmailVerified IS false`, atomically updates to `true`, returns boolean (true on consumed; false on already-consumed → falls through to 200-idempotent path).
  - Already-consumed responses are byte-for-byte identical to first-redemption responses (no leakage of "was already redeemed" via response shape).
  - Audit emission (D5) fires on every redemption attempt regardless of outcome — fraud-forensics signal for replay-attempt detection.
  - HTTPS + verified-email delivery channel for original token (existing defense-in-depth; no change).

#### Decision 4 — `MockEmailProvider` production-guard semantics

- **Decision summary:** `MockProviderProductionGuardAssertion` throws on `ASPNETCORE_ENVIRONMENT=Production` + mock registered + no opt-out + no real-adapter env-var present. PR 1 ships `Shape A — mocks-only` per ADR 0096 §"Step 4 W79." Production deploys MUST set `SUNFISH_ALLOW_MOCK_PROVIDERS=true` until Postmark adapter (Engineer Tier-2 D) ships.
- **Worst-case interpretation:** Operator deploys to production with mocks; sees `SUNFISH_ALLOW_MOCK_PROVIDERS=true` is needed but doesn't understand the implication; signups silently send no email; users never verify; no signups ever succeed.
- **Failure mode:** Silent feature unavailability; users blocked from completing signup.
- **Mitigation in this hand-off:** (a) MockEmailProvider in opt-out-on production logs a WARN-level message on every dispatch ("Mock email provider in use; verification URL is `<url>`; this is opt-out behavior — set `POSTMARK_API_KEY` to swap to real Postmark"); operator sees the URL in logs even if email is "delivered" to /dev/null. (b) deployment runbook explicitly documents the opt-out-vs-real story; (c) MockEmailProvider's in-memory store is queryable via test-only inspection API (NOT in production — gated by `ASPNETCORE_ENVIRONMENT != "Production"` per ADR 0096 #6). The W#79 hand-off ships (a) + (b); (c) is W#80 territory (dev `/dev/inbox` UI route — Halt H1).

#### Decision 5 — Per-email rate-limit key (resend-verification floor)

- **Decision summary:** Resend-verification floor uses a per-email rate-limit key (3/min/email) in addition to per-IP (3/min/IP). Per ADR 0095 §3.7 floors.
- **Worst-case interpretation:** Attacker controls a botnet of IPs; spams the resend-verification endpoint with a single victim email to flood their inbox. Per-IP floor doesn't help (each IP is under its own 3/min budget); per-email floor catches it.
- **Failure mode:** Email flooding of a target user; brand damage; potential email-deliverability degradation (mail server flagging Sunfish as a spam source).
- **Mitigation in this hand-off:** Per-email key in §3.7 floors. The key is computed inside the handler post-input-canonicalization (lowercased email → SHA-256 prefix for bucket lookup). Engineer ships the bucket-key computation in PR 0's rate-limit policy registration; sec-eng SPOT-CHECK verifies the per-email partitioning is in place.

#### Decision 6 — Bootstrap → post-tenant transition via α-1 scoped-holder

- **Decision summary:** SignupHandler creates a child `IServiceScope` via `IServiceScopeFactory`; populates `ITenantContextSeed.Bind(tenantId)` IMMEDIATELY before resolving any post-tenant service from the child scope. Per ADR 0095 Rev 2 Amendment 2.
- **Worst-case interpretation:** Future maintainer adds a new post-tenant service resolution to SignupHandler but does it on the OUTER bootstrap scope (not the child scope) — sees that the seed is populated and assumes the seed reaches the outer scope too. The outer scope's post-tenant services were NEVER registered (only the child scope was wired), so the resolution fails with a NullReferenceException OR an empty-string `TenantId` propagates into a query filter.
- **Failure mode:** Cross-tenant data leak via empty-string TenantId HasQueryFilter behavior (the exact failure ADR 0095's Rev 2 Gap B closes).
- **Mitigation in this hand-off:** (a) `RequireTenantBoundDbContext` marker is registered ONLY on the non-bootstrap branch's DI scope; resolving `SunfishBridgeDbContext` in bootstrap scope fails fast. (b) Step 3 analyzer (Engineer Tier-3 F) statically flags any constructor that injects both `IBootstrapContext` and any post-tenant interface. (c) Until the analyzer ships, code-review discipline + the `// Resolves only inside child scope` comment block at line 81-83 of `SignupHandler.HandleAsync` per §4.2.1 carries the invariant.

#### Decision 7 — `ITenantRegistry.CreateAsync` first production callsite

- **Decision summary:** `ITenantRegistry.CreateAsync` is wired into the SignupHandler. Per ADR 0095 §"Decision drivers" first-production-callsite invariant. The substrate exists today but has only test callers.
- **Worst-case interpretation:** `ITenantRegistry.CreateAsync` carries an undocumented invariant (e.g., "must be called from within an EFCore transaction") that was OK for tests but breaks under production load.
- **Failure mode:** Tenant creation races between concurrent signups for the same slug; OR partial-state writes (tenant created, user not created) on transaction failure.
- **Mitigation in this hand-off:** (a) Engineer's PR 1 audits `TenantRegistry.cs` + `ITenantRegistry.cs` for transaction-shape invariants and either adds explicit transaction-wrap inside `CreateAsync` body OR documents the invariant via xmldoc + SignupHandler scope. (b) The slug-uniqueness check is INSIDE the same transaction as the tenant write (DB-tier unique index on slug as the ultimate guard). (c) Integration test: 2 concurrent signups for the same slug → exactly 1 succeeds; the other gets `tenant_slug_taken` (idempotent against race).

#### Decision 8 — Email-uniqueness check via read-only DbContext (Q4 disposition)

- **Decision summary:** Per ADR 0095 Q4: W#79 disposition = option (b) — separate `SunfishBridgeReadOnlyDbContext` for the email-uniqueness check, registered on the bootstrap branch's DI scope WITHOUT the `RequireTenantBoundDbContext` marker. The read-only DbContext queries `TenantRegistrations` (control-plane; not under `HasQueryFilter`); cannot reach data-plane tables; cannot leak cross-tenant data.
- **Worst-case interpretation:** The read-only DbContext is misconfigured (the dev who added it copies `SunfishBridgeDbContext`'s `OnModelCreating` and accidentally inherits the `HasQueryFilter` config) — empty-string filter behavior exposes all tenants' data.
- **Failure mode:** Cross-tenant data leak via misconfigured read-only DbContext.
- **Mitigation in this hand-off:** (a) `SunfishBridgeReadOnlyDbContext` shipped with explicitly-empty `OnModelCreating` (or strictly only `TenantRegistrations` entity registered; nothing else); (b) constructor DOES NOT take `RequireTenantBoundDbContext` marker (read-only is bootstrap-resolvable by design); (c) integration test: read-only DbContext resolved in bootstrap scope CAN query `TenantRegistrations`; CANNOT query any tenant-bound entity (compile-error because the DbSet isn't declared).

---

### 6.2 First-instance pattern candidate scaffolds

Per ADR 0093 Rev 4 §"Adversarial Brief" + the pattern-emergence cadence (V11 #1 sub-pattern-split precedent): three patterns claim first-instance status with W#79.

#### Pattern 1 — `pattern-tier2-mock-first-substrate` (first-instance candidate)

**Status:** CANDIDATE; promotes from 4 instances per [[tier2-vendor-mock-first]] memory + ADR 0096 §"Implementation roadmap." Co-evolves with Engineer's Tier-2 D (Postmark) + Tier-2 E (Turnstile) PRs.

**Pattern shape:**
1. Vendor-neutral contract (`IXProvider`) ships in `foundation-integrations/X/` alongside a `MockXProvider` implementation marked `: IXProvider, IMockVendorProvider`.
2. `AddSunfishVendorProvider<TContract, TConcrete>()` registers the mock unconditionally + writes `(TContract → envVarKey)` into `IMockVendorEnvVarRegistry`.
3. Real vendor adapter ships in `packages/providers-{vendor}/` as a separate package; registered via `UseVendorProviderIfConfigured<TContract, TConcrete>(envVarKey)` which `services.Replace`s the mock when the env-var is non-empty.
4. `MockProviderProductionGuardAssertion` IHostedService verifies at startup that `ASPNETCORE_ENVIRONMENT=Production` deploys with mock-registered contracts have either (a) the real-adapter env-var present OR (b) `SUNFISH_ALLOW_MOCK_PROVIDERS=true` opt-out. Fail-loudly on production-default-mock.

**Adversarial-review framework (for Engineer's Tier-2 D + E SPOT-CHECK + future Tier-2 vendors):**
- **Threat 1:** Typo'd env-var → silent mock-in-production. Closed by `IMockVendorEnvVarRegistry` + assertion-message enumeration of expected keys.
- **Threat 2:** Mock concrete log leaks secret. Closed by ADR 0096 Rev 2 Amendment #6 substrate-tier log discipline (no `BodyHtml`/`BodyText` for email; analogous floors for other vendors).
- **Threat 3:** Mock dev-inbox UI shipped in production. Closed by 3-prong gate (mock-provider-registered AND non-Production AND dev-host allow-list).
- **Threat 4:** Future Tier-2 contract added without marker constraint. Closed by `where TConcrete : class, TContract, IMockVendorProvider` compile-time constraint on `AddSunfishVendorProvider`.
- **Threat 5 (Rev 2 — sec-eng H):** Mock registered via implementation-factory (`services.AddSingleton<IEmailProvider>(sp => new MockEmailProvider(...))`) escapes ServiceDescriptor.ImplementationType inspection (ImplementationType is null on factory-registered entries). Closed by `MockProviderProductionGuardAssertion` startup body inspecting `ServiceDescriptor.ImplementationFactory` returns — for each candidate IServiceCollection entry with non-null ImplementationFactory, resolve a probe instance from each candidate + check runtime type for the `IMockVendorProvider` marker. Test coverage: §4.2.5 M3 + M4 (factory-registered mock detection; production-default-fail + opt-out-success paths).
- **Threat 6 (Rev 2; forward-watch):** Decorator-pattern mock (`services.Decorate<IEmailProvider, LoggingMockEmailProvider>()` where `LoggingMockEmailProvider` wraps a real provider but doesn't carry the marker). The decorator escapes the runtime-type check. Forward-watched; promoted at next substrate-touch (Engineer's Step 1 or Step 2 amendment if decorator-walking is shipped).

**Promotion to STANDING:** when W#79 (instance 1) + Engineer Tier-2 D Postmark (instance 2) + Engineer Tier-2 E Turnstile (instance 3) all ship without regression and a 4th instance (likely future `IBlobStorageProvider` or `IExternalIdpProvider`) lands cleanly using the same pattern.

#### Pattern 2 — `pattern-bootstrap-context-consumption` (first-instance candidate)

**Status:** CANDIDATE; W#79 is the first consumer of ADR 0095's `IBootstrapContext` substrate. Webhook-receiver bootstrap + cross-tenant federation bootstrap are forward-watched 2nd/3rd-instance candidates per ADR 0095 §"Out of scope but flagged."

**Pattern shape:**
1. Handler registered inside `MapBootstrapEndpoints` (extension method registered inside bootstrap pipeline branch's `UseWhen` body).
2. Handler depends on `IBootstrapContext` for correlation-id, client-ip, captcha-token, idempotency-key, rate-limit-bucket-key.
3. Handler does NOT inject ANY post-tenant context interface (`Sunfish.Foundation.Authorization.ITenantContext` facade or narrowed; `IBrowserTenantContext`).
4. When the handler needs to write tenant-scoped state (e.g., write initial User aggregate after Tenant creation), it transitions to a child `IServiceScope` via the α-1 scoped-holder mechanism: `IServiceScopeFactory.CreateAsyncScope` → `ITenantContextSeed.Bind(tenantId)` → resolve post-tenant services from the child scope.
5. Handler emits audit + dispatches integrations on the OUTER bootstrap scope; the child scope's lifetime is bounded to the post-tenant write.
6. `MapBootstrapEndpoints` handlers MUST declare `.AllowAnonymous()` at registration time.

**Adversarial-review framework (for future bootstrap-consumer hand-offs):**
- **Threat 1:** Maintainer adds `ITenantContext` injection to a bootstrap-handler constructor. Closed by Step 3 Roslyn analyzer (constructor-parameter-scan) + reviewer discipline during Step 1-2 window.
- **Threat 2:** Maintainer resolves a post-tenant service from the OUTER bootstrap scope (not the child scope), invoking it after `ITenantContextSeed.Bind()` on the child scope. Closed by `RequireTenantBoundDbContext` marker (only registered on non-bootstrap branch); other post-tenant services are unreachable in bootstrap scope by construction.
- **Threat 3:** Maintainer registers a non-AllowAnonymous handler via `MapBootstrapEndpoints`. Closed by Step 3 analyzer's inverse-failure detection (Rev 2 sec-eng Gap D); reviewer discipline during Step 1-2 window.
- **Threat 4:** Bootstrap-branch pipeline ordering reordered (e.g., RateLimiter before Origin). Closed by sec-eng SPOT-CHECK at Step 2 Engineer PR + at every W#79+ consumer PR.

**Promotion to STANDING:** when W#79 + W#82 (invitations; 2nd-instance) + post-MVP webhook-receiver-bootstrap (3rd-instance candidate) all ship without regression.

#### Pattern 3 — `pattern-onboarding-rate-limit` (first-instance candidate)

**Status:** CANDIDATE; non-permissive-default rate-limit minimum-floor per ADR 0095 Rev 2 Amendment 7. Co-pre-authorized with W#79 PR 0; promotes to standing on second-instance application (likely a webhook-receiver bootstrap; needs analogous non-permissive floor).

**Pattern shape:**
1. Pre-auth, public-internet-facing endpoint family configures `AspNetCore RateLimiter` with EXPLICIT per-endpoint policies; never relies on `AspNetCore RateLimiter` default-allow.
2. Each endpoint declares (a) per-IP limit + (b) per-(route+IP) limit + (c) for endpoints with a natural per-entity key (email, account-id), a per-entity-key limit as a third partition.
3. 429 response carries `Retry-After` header set to window-remainder; body is RFC 7807 ProblemDetails with `title: "rate_limited"` discriminator.
4. Minimum-floors ratified at substrate-ADR tier; implementation MAY tighten but MUST NOT loosen.
5. Per-entity-key partitioning lives in handler-tier (post-input-canonicalization); per-IP + per-(route+IP) lives in middleware-tier.

**Adversarial-review framework:**
- **Threat 1:** Future endpoint added to `MapBootstrapEndpoints` without explicit policy → default-allow path. Closed by Step 3 analyzer (inverse-failure case extends to `RequireRateLimiting` check; halt for Engineer's Step 3 PR).
- **Threat 2:** Per-IP floor loosened in a future amendment "to reduce false-positives." Closed by ADR-tier minimum-floor ratification; loosening requires ADR amendment + sec-eng dual-council.
- **Threat 3:** Retry-After header dropped on 429. Closed by integration test asserting Retry-After present on every 429 response.

**Promotion to STANDING:** when W#79 + W#82 + post-MVP webhook-receiver bootstrap all consume the non-permissive-floor + Retry-After + per-entity-key shape.

### 6.3 Rev 2 forward-watches (non-blocking; tracked for future cohorts)

Per the 3-council triple-AMBER fold, these forward-watches retain status post-Rev-2 (no Rev 2 amendment required; each is deferred to a named future PR or substrate-touch):

**From sec-eng verdict (Gaps I, J, K):**
- **FW-sec-eng-I — OOB notification per-victim-email rate-limit.** H2 OOB notification dispatch needs per-victim-email-per-day quota (~1 email/email/24h) to prevent attacker-driven amplification on a victim inbox. SignupHandler §4.2.1 ships an IdempotencyKey shape (`"oob-existing-{tenantId}-{date:yyyy-MM-dd}"`) that the email substrate's dedup uses; PR 1 SPOT-CHECK verifies the substrate honors the dedup key. If substrate-tier dedup doesn't materialize, surface at PR 1 SPOT-CHECK + lift to per-email rate-limit at handler tier.
- **FW-sec-eng-J — Origin validation single-apex-host assumption.** §3.6 + ADR 0095 Step 2 PR substrate-tier work; W#79 hand-off assumes `TenantResolutionOptions.AllowedApexHosts` (allowlist) shape; Engineer's Step 2 PR SPOT-CHECK verifies. If Step 2 ships single-host shape, surface as Step 2 SPOT-CHECK finding.
- **FW-sec-eng-K — VerifyEmailHandler CAPTCHA omission rationale.** Rev 2 includes inline comment block in §4.2.2 STEP 1 (already specced; verify-email originates from emailed link → token-signature replaces CAPTCHA as bot-defense surface). No further fold needed.

**From .NET-arch verdict (FW-1 through FW-6):**
- **FW-A1 — Async-disposable reconciliation.** Folded into §4.2.1 inline note (Rev 2; see "Note (FW-A1 reconciliation)" above).
- **FW-C2 — §A0 cited-symbol audit Step 3 analyzer status.** §A0 cited-symbol audit (referenced at §4.1.4 + §6.1 Decision 6) should mark "Engineer's Step 3 analyzer in-flight; W#79 ships before analyzer; doc-comment-discipline applies during Step 1-2 window." Engineer's PR 0 description ratifies.
- **FW-E1 — §3.9 step 6 AMBER-vs-RED conditional.** Folded into §3.9 cascade table (Rev 2; "expects AMBER if FED-Cycle-1-commit-clean-banner; expects RED if FED-Cycle-1-commit-silently-hides-dead-code").
- **FW-H1 — Cumulative test count target.** Updated post-fold from ~25 to ~90 per §5; test-eng-council coverage-model verdict.
- **FW-5 — ADR 0049 audit substrate bootstrap-scope compatibility.** D5 + D5.a sub-ruling address; Engineer's PR 0 ships SystemTenant sentinel + partition routing if ADR 0049 doesn't yet support. SPOT-CHECK verifies.
- **FW-6 — OpenAPI/Swagger metadata for 3 new endpoints.** §4.1.1 `OnboardingEndpoints.cs` has `.WithName(...)` but no `.WithOpenApi()` or `.Produces<SignupAcceptedResponse>(202)`. Engineer's Step 2 Bridge pipeline branch PR decides OpenAPI metadata posture for bootstrap-branch endpoints; W#79 follows whatever Step 2 ratifies. Forward-watch only.

**From test-eng verdict (FW-1, FW-2, FW-3):**
- **FW-MSW — MSW byte-for-byte drift detection at scale.** §4.4.4 MSW-vs-Bridge parity test (Rev 2 fold) covers the W#79 surface; as W#80 (site-key) + W#82 (invitations) + future onboarding endpoints land, hand-authored MSW drift compounds. ONR evaluates OpenAPI-driven MSW generation at W#80 hand-off authoring; the per-discriminator const-export pattern (§3.5) is the seed for that lift.
- **FW-Fixtures — Fixture-builder factoring across W#79 PRs.** §5 ~90 cumulative tests construct `SignupRequest`, `VerifyEmailRequest`, `EmailMessage`, `TenantRegistration` fixtures. Stage-06 SPOT-CHECK on PR 1 verifies Engineer ships `SignupRequestBuilder`, `VerifyEmailRequestBuilder`, `TestEmailMessage`, `TestTenantRegistration` fixture builders in `signal-bridge/Sunfish.Bridge.Tests/Onboarding/Fixtures/` — minimum 4 builders for the 4 surfaces.
- **FW-IClock — Verify-email token-expired test infrastructure.** §3.5 `verification_token_expired` discriminator coverage (PD7) requires either `IClock` / `TimeProvider` abstraction with `FakeClock` OR hand-issued expired tokens (sign-with-past-`exp`). Rev 2 spec defers to Engineer's PR 1 — recommended: pre-signed expired-token fixtures (simpler; lower substrate-touch). Pattern likely emerges as `pattern-substrate-time-abstraction` first-instance candidate at a future cohort.

**Additional Rev 2 forward-watches:**
- **Constant-work timing-floor pattern emergence (sec-eng D resolution).** Hand-off's sec-eng D resolution (constant-work signup discipline) introduces a pattern that will recur in invitation-accept (W#82) + any future public-facing flow with branch-asymmetric handler logic. Forward-watch for pattern promotion to `pattern-constant-work-discipline` first-instance at W#82.
- **ADR 0097 PasswordHasher Substrate.** Sec-eng dual-council MANDATORY per Admiral H8 ruling. When ONR scaffolds, the F3 / F4 amendments (hash-version-tagging + rehash-on-next-login per .NET-arch K2) fold into ADR 0097's migration-path section.
- **Decorator-pattern mock detection (sec-eng H sub-concern 2).** Folded as Pattern 1 Threat 6 forward-watch in §6.2; promoted at next-substrate-touch on ADR 0096 (likely Step 2 amendment if Engineer's mock-inspection walks decorators).

---

## 7. Council dispatch — per-PR trigger matrix

Per ADR 0093 Rev 4 §"Council dispatch — trigger matrix" + fleet-conventions §SPOT-CHECK dispatch SLA:

| PR | sec-eng-council (Stage-05 + SPOT-CHECK) | .NET-architect-council | frontend-architect | test-eng-council |
|---|---|---|---|---|
| **Stage-05 hand-off** (this document) | YES (substrate-touch — first-instance consumer of 2 new substrate ADRs; new endpoint family; new patterns claimed) | YES (substrate-touch) | NO (Stage-05 is sec-eng + .NET-arch + test-eng; frontend-arch enters at PR 2 Ready-flip) | YES (>5 test cases + substrate-touching; coverage-model verification) |
| **PR 0** (Bridge endpoints; pattern-009 PAIR) | YES MANDATORY (pattern-009 + 3 new routes + first-instance pattern claims) | YES MANDATORY (substrate-touch + ITenantContextSeed package home) | YES MANDATORY (pattern-009 PAIR — Bridge half) | NO (Stage-05 already gated it) |
| **PR 1** (Handler bodies + α-1 transition + ITenantRegistry first prod) | YES MANDATORY (α-1 confused-deputy-seam is security-critical; ITenantRegistry first-prod-callsite invariant) | YES MANDATORY (substrate-tier handler shape; new precedent for bootstrap → post-tenant transition) | NO | NO (Stage-05 + sec-eng cover this) |
| **PR 2** (Frontend rebind; pattern-009 PAIR — frontend half) | YES (Cycle-1 expects AMBER; Cycle-2 expects GREEN per §3.9 cascade) | NO | YES MANDATORY (pattern-009 PAIR + first-instance signup flow) | NO |
| **PR 3** (e2e + contract tests) | OPTIONAL (cross-stack flow assertion) | OPTIONAL | OPTIONAL | YES MANDATORY (coverage-model gate; >5 test cases) |

**Admiral dispatch SLA:** 30 minutes from Ready-flip beacon consumption per fleet-conventions §"SPOT-CHECK dispatch SLA." QM daemon backstops missed dispatches within 1 hour.

---

## 8. Halt conditions surfaced (route to Admiral)

The following decisions are surfaced for Admiral disposition BEFORE Engineer opens PR 0. Each is foreseeable from substrate-ADR text + this hand-off; resolving them ahead of Stage-06 saves cycle 0 RED.

### H1 — MockEmailProvider `/dev/inbox` UI route — W#79 scope or W#80 deferred?

**Context:** ADR 0096 Rev 2 Amendment #6 ratifies the 3-prong gate (mock-provider-resolved + non-Production + dev-host-allow-list) for any dev inbox UI route, but explicitly leaves the implementation to W79 Stage-05. Without an inspection UI, dev-loop UX for testing the signup flow is degraded (developer must read server logs or write integration tests to see the verification URL).

**Options:**
- (a) Ship `/dev/inbox` UI in W#79 — adds ~150 LOC FED + ~80 LOC Bridge (test-only inspection endpoint behind 3-prong gate).
- (b) Defer to W#80 — sub-cohort 2 already owns email-templating + welcome-flow UX polish; the dev inbox naturally co-locates.
- (c) Test-only inspection endpoint (no UI) — Bridge exposes `GET /dev/inbox?email=<email>` returning the in-memory store JSON; e2e tests use it; no React page in W#79.

**ONR recommendation:** Option (c). Keeps W#79 focused on substrate-consumption; e2e tests get the inspection capability they need; W#80 layers the React UI on top. Halt for Admiral disposition.

### H2 — `email_already_registered` discriminator shape (Decision 2 in §6.1 brief)

**Context:** Signup against a verified-Tenant email returns 400 `email_already_registered`; signup against an unverified-Tenant email returns 202 `queued` (quietly re-sends). The asymmetric response leaks verified-tenant existence (enumeration vector).

**Options:**
- (a) Always-202 — drops the `email_already_registered` discriminator entirely; frontend cannot distinguish; UX regresses for legitimate "I forgot I had an account" flows.
- (b) Always-202 + out-of-band notification email — "someone tried to sign up with your address; if it was you, log in"; defends against enumeration; small phishing-vector trade.
- (c) Rate-limit-aggressive (per-email per-day) — keeps the discriminator but makes enumeration economically expensive.
- (d) Always-202 BUT a separate "I forgot password" / "I forgot I have an account" flow that requires email-verification before disclosing existence.

**ONR recommendation:** Option (b) + (c). Halt for Admiral disposition (this is a UX-vs-security tradeoff Admiral should rule on; CIC may want to consult).

### H3 — Verify-email idempotency-key semantics

**Context:** ADR 0095 §"Initial contract surface" `IdempotencyKey` xmldoc says: "Signup itself is non-idempotent; invitation-accept is idempotency-required by handler contract." Verify-email is not addressed.

**Options:**
- (a) Verify-email is idempotency-required (handler refuses request without `X-Idempotency-Key`).
- (b) Verify-email is idempotency-optional (handler accepts request with or without key; semantically idempotent by token-replay-friendliness — Decision 3 in §6.1).
- (c) Verify-email is idempotency-banned (server discards any submitted key; treats verify as inherently idempotent via Decision 3 disposition).

**ONR recommendation:** Option (b). The token itself carries idempotency-semantics (one token can be replayed); a client-supplied key adds no value. Halt for Admiral disposition.

### H4 — Onboarding cluster home: `signal-bridge/Sunfish.Bridge.Onboarding/` new project or sub-folder inside `Sunfish.Bridge/`?

**Context:** Onboarding-ladder ruling Decision 7 says `packages/blocks-onboarding/` is the FOUNDATION cluster home (shipyard packages). But the Bridge endpoints + handlers live in signal-bridge, not shipyard. ADR 0095 §"Affected packages" speaks of "signal-bridge `Program.cs`" but does NOT explicitly carve a new `Sunfish.Bridge.Onboarding` project; ADR 0095 leaves the module-boundary decision to W#79.

**Options:**
- (a) New project `signal-bridge/Sunfish.Bridge.Onboarding/` (separate .csproj; consumes `packages/foundation-bootstrap` + `packages/foundation-integrations`). Clean boundary; future onboarding endpoints layer here.
- (b) New folder inside `signal-bridge/Sunfish.Bridge/` (no new project); lower ceremony; faster to ship.
- (c) Folder inside `Sunfish.Bridge` for W#79 + extract to dedicated project at W#80 sub-cohort 2 when invitations + email templates land.

**Path-prefix decision (related):** ADR 0095 §"Pipeline routing" uses `/api/signup/*` + `/api/invitations/accept/*`. W#79 proposes `/api/v1/auth/*` for versioned auth API consistency with future fleet posture. Halt resolution should pin both questions together.

**ONR recommendation:** Option (a) + `/api/v1/auth/*` prefix. Aligns with Decision 7 cluster-as-coherent-product-capability framing; gives a natural future home; +30 min ceremony. Halt for Admiral disposition.

### H5 — PR 0 / PR 1 split — endpoint registrations + handler bodies or single PR?

**Context:** §4.1 splits the Bridge work into PR 0 (endpoint registrations + DTOs + 501-skeleton handlers + DI wiring) and PR 1 (handler bodies + α-1 transition + tests). The rationale is reviewability under SPOT-CHECK — sec-eng + .NET-arch + frontend-arch can review the pipeline shape in PR 0 in isolation before handler-body logic lands. The alternative is a single PR consolidating both.

**Options:**
- (a) Two PRs (PR 0 + PR 1) per this hand-off.
- (b) Single PR — Engineer's call when scope-cap of ~1500 LOC is reachable.

**ONR recommendation:** Two PRs per §4.1 rationale. Engineer's discretion to merge if scope is comfortable. Halt only if Admiral wants a hard ruling.

### H6 — `ITenantContextSeed` package home

**Context:** ADR 0095 Rev 2 .NET-arch A2 follow-on recommends `foundation-authorization` as the likely home for `ITenantContextSeed` + `MutableTenantContextSeed` + `SeededTenantContext`. W#79 RATIFIES that choice in §4.1.4 but flags as halt for Admiral confirmation.

**Options:**
- (a) `shipyard/packages/foundation-authorization/` (per .NET-arch A2 recommendation).
- (b) `shipyard/packages/foundation-authorization-seeded/` (separate sub-package isolating the seed-holder pattern).
- (c) `shipyard/packages/foundation-bootstrap/` (co-locate with the substrate that needs it).

**ONR recommendation:** Option (a). Smallest blast radius; foundation-authorization already owns `AddSunfishTenantContext`; SeededTenantContext is structurally a TenantContext-variant. Halt for Admiral confirmation.

### H7 — Welcome-email `From` address shape (substrate gap?)

**Context:** §4.2.1 SendVerificationEmailAsync hard-codes `From: "noreply@sunfish.app"`. Where does this configuration live? `IOptions<EmailDispatchOptions>` would be the canonical pattern, but no such options-type exists in ADR 0096 substrate. (ADR 0096 Step 2 Postmark + Step 3 Turnstile adapter PRs ship per-vendor options; a substrate-level `EmailDispatchOptions.FromAddress` is not yet defined.)

**Options:**
- (a) W#79 ships `EmailDispatchOptions` in `foundation-integrations/Email/` with a `FromAddress` property; binds from `IConfiguration` via `IOptions<EmailDispatchOptions>`; consumed by handlers.
- (b) W#79 hard-codes `From: "noreply@sunfish.app"`; W#80 introduces the options-type when email-templating lands.
- (c) `IEmailProvider` carries the from-address as a constructor-injected configuration; each vendor adapter resolves its own from-address.

**ONR recommendation:** Option (a). Substrate-level configuration belongs at substrate-tier; future vendors all need the same address. ~30 LOC; small surface. Halt for Admiral disposition.

### H8 — Password hash substrate

**Context:** §4.2.1 calls `PasswordHasher.Hash(request.Password)`. No `PasswordHasher` substrate exists in shipyard today (grep confirms; halt for Admiral to confirm or surface the gap).

**Options:**
- (a) W#79 introduces `Sunfish.Foundation.Identity/IPasswordHasher` + `Argon2idPasswordHasher` (concrete; consumes `Konscious.Security.Cryptography.Argon2` or equivalent). New substrate primitive — substrate-ADR territory? OR co-pre-authorized as part of W#79 if it's a small +1 surface.
- (b) Reuse `ASP.NET Core Identity`'s `IPasswordHasher<TUser>` from `Microsoft.AspNetCore.Identity` — comes with PBKDF2 default; tunable to Argon2 via custom implementation.
- (c) Defer to a separate ADR 0097 "Foundation.Identity password substrate"; W#79 ships placeholder + halts on production-readiness.

**ONR recommendation:** Option (b) initially; future ADR 0097 hardens to Argon2id explicitly if PBKDF2 default becomes a concern. Halt for Admiral — this is substrate-shaping and may warrant its own ADR.

### H9 — Verify-email already-redeemed disposition (Decision 3 in §6.1)

**Context:** Verify-email handler treats already-verified tenant as success (200 idempotent) per §4.2.2 OR fails with `verification_token_already_used` discriminator (defensive against replay). Decision 3 in §6.1 brief covers the threat-model angle.

**Options:**
- (a) 200 idempotent — same response shape as fresh-verify; UX-friendly for "I already verified" loops.
- (b) 400 `verification_token_already_used` — fail-loudly; protects against replay-disclosure.
- (c) 410 Gone — same protection as (b) but more semantically correct per HTTP spec.
- (d) Token TTL reduced from 24h → 1h — shrinks the replay window; idempotent disposition then has minimal threat exposure.

**ONR recommendation:** Option (a) + (d). Token TTL reduction (24h → 1h) per ASVS Level 1 minimum-floor for short-lived verification tokens; idempotent disposition retains UX-friendliness. Halt for Admiral disposition.

### H10 — Turnstile site-key delivery to frontend (forward-watch only)

**Context:** ADR 0096 Halt 7 RATIFIED that Turnstile site-key delivery is out of scope for ADR 0096; W#80 ships the Bridge endpoint. W#79's `CaptchaWidget.tsx` ships in `mock` mode for now (any token like `mock-pass` passes verification via MockCaptchaProvider). Forward-watch: W#80 site-key delivery shape.

**No active halt** — flagged for cross-cohort tracking + W#80 hand-off authoring reminder.

---

## 9. Risk + mitigation

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Engineer Tier-2 B (ADR 0096 Step 1) lands BEFORE Engineer Tier-1 A (ADR 0095 Step 2) — W#79 PR 0 can't open because Bridge pipeline branch unavailable | MEDIUM | LOW (Engineer can sequence) | Per substrate-ladder directive, A + B run in parallel; A's Step 2 (Bridge pipeline branch) gates W#79 PR 0; if B finishes first, PR 0 holds DRAFT until A's Step 2 ships. Engineer authoritative on sequencing. |
| α-1 child-scope transition has subtle DI lifetime bug — `ITenantContextSeed.Bind()` not visible to child-scope-resolved services | MEDIUM | HIGH (cross-tenant data leak) | Integration test in PR 1 §4.2.6 verifies `IUserAggregateRepository.PersistInitialUserAsync` writes to the correct tenant (asserts the Tenant FK on the User row matches the tenant_id seeded). |
| `SunfishBridgeReadOnlyDbContext` accidentally inherits HasQueryFilter from copying SunfishBridgeDbContext OnModelCreating (Decision 8 in §6.1) | LOW | HIGH (cross-tenant leak via uniqueness check) | Decision 8 mitigation: empty OnModelCreating; only `TenantRegistrations` entity registered; integration test verifies. Halt H1 disposition reduces blast radius (test-only inspection endpoint vs full UI). |
| MockEmailProvider production-guard fires unexpectedly in CI (test fixtures don't set `SUNFISH_ALLOW_MOCK_PROVIDERS`) | LOW | MEDIUM (CI flakiness) | Test fixtures set `ASPNETCORE_ENVIRONMENT=Testing` (not Production); production-guard early-returns. Confirmed by ADR 0096 §"Decision drivers" D1c. |
| MockEmailProvider console-log leaks BodyText in older log capture (ADR 0096 Rev 2 #6 floor not enforced by analyzer) | LOW | MEDIUM (verification token leak) | Reviewer-discipline + integration test in PR 1 §4.2.6 asserts console log capture does NOT contain BodyText. |
| Step 3 Roslyn analyzer (Engineer Tier-3 F) ships AFTER W#79 — bootstrap-pipeline-mixing detection relies on code review | LOW-MEDIUM | HIGH (silent confused-deputy regression) | (a) `RequireTenantBoundDbContext` marker closes the highest-risk silent-data-leak path; (b) ADR 0091 R2 A2 deferred-analyzer window saw zero pipeline-mixing regressions — same projected outcome; (c) reviewer-discipline pre-Step-3-analyzer. |
| Pattern-009 SPOT-CHECK on PR 0 finds new AMBER beyond the Stage-05 brief items | MEDIUM | LOW (1 fold cycle) | Adversarial Brief in §6.1 covers 8 decisions in detail; sec-eng cycle 0 should land GREEN-with-nits or AMBER-with-known-amendments. |
| FED Cycle-1 DRAFT silently hides a non-functional CAPTCHA widget (Amendment L RED case) | LOW (cascade plan documented) | MEDIUM | §3.9 cascade plan explicit; §4.3.3 names the cleanly-removed-with-forward-watch posture; cycle-1 sec-eng SPOT-CHECK verifies. |
| W#79 ships before Engineer Tier-2 D (Postmark) lands → production deploys forced to `SUNFISH_ALLOW_MOCK_PROVIDERS=true` indefinitely | HIGH (likely) | LOW (intended substrate posture for now) | Documented in §4.1 Shape A; ADR 0096 §"Step 4 W79 composition-root wiring" explicitly anticipates this; operator runbook references opt-out. |
| ADR 0095 Q4 disposition (option b — separate read-only DbContext) introduces new substrate primitive not council-attested at Stage-05 | MEDIUM | LOW (small surface; bootstrap-scope-only; no tenant-data exposure by construction) | §6.1 Decision 8 covers; sec-eng + .NET-arch Stage-05 verdict on this hand-off attests the disposition. |
| `ITenantRegistry.CreateAsync` has undocumented invariant that breaks under production load (Decision 7 in §6.1) | MEDIUM | MEDIUM (race + partial-state) | §6.1 Decision 7 mitigation: Engineer's PR 1 audits `TenantRegistry.cs` for transaction-shape invariants; integration test for concurrent same-slug signup. |

---

## 10. Per-PR ratification summary

| PR | Stage-05 gate (this hand-off) | Stage-06 PR-open gate | Auto-merge fires when |
|---|---|---|---|
| Hand-off itself | sec-eng-council + .NET-architect-council + test-eng-council Stage-05 verdicts (this dispatch) | n/a | GREEN-or-AMBER-with-amendments on all 3 → Stage-06 PR 0 unblocks |
| PR 0 | (covered by hand-off Stage-05) | sec-eng + .NET-arch + frontend-arch SPOT-CHECK (pattern-009 PAIR; substrate-touch) | All 3 GREEN + CI green + halt resolutions in place |
| PR 1 | (covered by hand-off Stage-05) | sec-eng + .NET-arch SPOT-CHECK | Both GREEN + CI green + PR 0 merged |
| PR 2 Cycle 1 (DRAFT) | (covered by hand-off Stage-05) | sec-eng (Cycle 1; expect AMBER) + frontend-arch | Cycle 1 AMBER acknowledged; not yet auto-merging |
| PR 2 Cycle 2 (amendment commit) | n/a (re-attest) | sec-eng (Cycle 2 re-attest; expect GREEN) + frontend-arch | Both GREEN + CI green + PR 0 merged + PR 1 merged |
| PR 3 | (covered by hand-off Stage-05) | test-eng-council Stage-06 SPOT-CHECK (or skipped if Stage-05 test-eng covered) | GREEN + CI green |

---

## 11. Decisions surfaced (route to Admiral via inbox) — RESOLVED in Rev 2

All halt conditions H1-H9 RESOLVED per `admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md` (Admiral pre-Rev-2 ruling) AND `admiral-directive-2026-05-26T0100Z-onr-w79-rev-2-fold-with-5-architectural-rulings.md` (Admiral Rev 2 architectural rulings D1-D5).

| Halt | Disposition | Folded at |
|---|---|---|
| H1 — Mock /dev/inbox scope | option (c) test-only inspection endpoint; UI deferred to W#80 | §10 + W#80 hand-off |
| H2 — `email_already_registered` shape | RATIFY (b+c) — always-202 + OOB + per-email rate-limit | §3.5 + §4.2.1 + §6.1 Decision 2 |
| H3 — Verify-email idempotency-key | option (b) optional; token carries idempotency-semantics | §3.2 |
| H4 — Onboarding cluster home + path | RATIFY-ONR (a) new project + `/api/v1/auth/*` | §4.1.1 + §4.1.2 |
| H5 — PR 0/PR 1 split | RATIFY-ONR two PRs; Engineer discretion override | §4.1 + §4.2 |
| H6 — ITenantContextSeed package home | RATIFY-ONR (a) foundation-authorization | §4.1.4 + §4.1.1 |
| H7 — EmailDispatchOptions.FromAddress | option (a) substrate-tier ~30 LOC | §4.2.1 (handler hard-codes for now; substrate option pending) |
| H8 — PasswordHasher substrate | RATIFY (b) IPasswordHasher<TUser> + future ADR 0097 Argon2id | §4.2.1 + .NET-arch K1/K2 fold |
| H9 — Verify-email already-redeemed | RATIFY (a+d) 200-idempotent + TTL 1h + sec-eng C one-shot | §4.2.2 + §6.1 Decision 3 |
| H10 (forward-watch only) | Turnstile site-key delivery deferred to W#80 | (W#80 hand-off) |

**Rev 2 architectural-ruling additions** (D1-D5) absorbed; see §1.5 + per-amendment fold. No new halt conditions opened in Rev 2.

---

## 12. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-25T2230Z-onr-w79-stage-05-handoff-authoring.md` (the directive)
2. `shipyard/docs/adrs/0095-bootstrap-context.md` (Accepted Rev 2)
3. `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md` (Accepted Rev 2)
4. `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (Accepted Rev 2; α-1 ergonomics precedent)
5. `shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md` (Accepted Rev 4; S05-1 through S05-5 amendments)
6. `shipyard/docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md` (Wave 5.1 control-plane vs data-plane)
7. `coordination/inbox/admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md` (Decisions 1, 3, 6, 7, 8, 9, 10)
8. `coordination/inbox/admiral-ruling-2026-05-25T1531Z-adr-0095-bootstrap-context-6-halt-conditions.md` (Halts 1-6 disposition)
9. `coordination/inbox/admiral-ruling-2026-05-25T1953Z-adr-0096-vendor-provider-substrate-8-halt-conditions.md` (8 halts disposed; Halt 3 OVERRIDE)
10. `coordination/inbox/admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md` (Postmark + Turnstile + canonical Tier-2 discipline)
11. `coordination/inbox/admiral-ruling-2026-05-25T2200Z-cic-slotting-6-residual-questions-resolved.md` (Q1 tier vocabulary locked; Q3 IMinimumSpecResolver 11th dimension forward-watch)
12. `coordination/inbox/admiral-directive-2026-05-25T2210Z-engineer-substrate-ladder-post-adr-0095-0096-accepted.md` (Engineer's substrate ladder; Tier-1 A + B parallel, Tier-2 C/D/E sequencing)
13. `shipyard/icm/01_discovery/research/onr-adr-0095-bootstrap-context-scaffold.md` (predecessor research)
14. `shipyard/icm/01_discovery/research/onr-adr-0096-vendor-provider-substrate-scaffold.md` (predecessor research; mock-first discipline)
15. `shipyard/icm/_state/handoffs/cohort-4-fed-pr-specs.md` (V9 #1 cohort-4 hand-off; shape mirror for this hand-off)
16. fleet-conventions `.claude/rules/fleet-conventions.md` §"SPOT-CHECK dispatch SLA" + §"Commit-message commitlint traps" + §"Beacon naming"
17. `[[tier2-vendor-mock-first]]` memory (canonical Tier-2 substrate discipline)
18. `[[three-tier-slotting-vocabulary]]` memory (Tier 1/2/3 naming locked)
19. `[[itenantcontext-consumption-qualification]]` memory (Authorization sum-interface facade at consumption sites)
20. `[[onr-worktree-per-deliverable]]` + `[[onr-questions-via-inbox]]` memories

---

## 13. What ONR does next

This hand-off (V14-style deliverable; W#79 cohort Stage-05) is the substrate-consumption hand-off Admiral routed via `admiral-directive-2026-05-25T2230Z-onr-w79-stage-05-handoff-authoring.md`.

After PR open + status-beacon:

- ONR stands by for Admiral disposition on Halts H1-H9 (single-ruling preferred per §11).
- After Halts resolved, ONR may file a follow-up amendment to this hand-off (Rev 2) folding dispositions; OR Engineer proceeds directly against this hand-off + the inbox ruling.
- Secondary deliverables per directive §"Secondary/Tertiary":
  - **Q2 plugin-substrate promotion Stage-05 spec** (post-MVP; Engineer ~3-day lift; ratifies the flight-deck plugin manifest → shipyard substrate promotion).
  - **Q6 tender deep integration Stage-05 spec** (post-MVP; ~1-day po-mac subagent lift; `@sunfish/contracts` `file:` ref + bundle-manifest reader + plugin-health UI).
  - **Substrate-PR adversarial review patterns retrospective** (ADR 0093 Rev 4 first-pilot retro scaffolding follow-on).

ONR will pick up the Q2/Q6 specs if W#79 is blocked on halt resolution or cleared queue; substrate-PR retro is tertiary slack-window work.

— ONR, 2026-05-25T22:50Z (Rev 1)
— ONR, 2026-05-26T01:30Z (Rev 2 fold of 3-council triple-AMBER verdicts + 5 Admiral architectural rulings)
