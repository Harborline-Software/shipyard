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
- Rate-limit policy values per ADR 0095 Rev 2 non-permissive-default minimum-floor (5/min signup; 10/min verify-email; 20/min check-availability; 429 + Retry-After).
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
4. signal-bridge W#79 PR 0 (this spec; 4 endpoints)   [Engineer; gates on 1+2+3]
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

All 4 endpoints registered inside `MapBootstrapEndpoints` MUST declare `.AllowAnonymous()` at registration time. The Step 3 Roslyn analyzer (Engineer Tier-3 F per substrate-ladder directive) enforces this; W#79 PR 0 ships before the analyzer, so the discipline is reviewer-enforced for this PR (per ADR 0095 §"Consequences/Negative" Step 1-2 doc-comment-discipline window).

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
| 7 | Engineer | W#79 PR 1 (handler bodies + α-1 child-scope transition + IBootstrapTenantRegistry + IEmailProvider + ICaptchaVerifier consumption + first-tenant + first-user write + ProblemDetails per-discriminator emission) MERGED | §4.2.6 13 handler integration tests (9 baseline + sec-eng C one-shot + sec-eng D constant-work + audit-emission test) + §4.2.5 12 production-guard tests M1-M12 + §4.2.6.PD 9 ProblemDetails backend per-discriminator tests = 34 PR 1 tests | Substrate cycle |
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
**Estimated lines:** ~600-900 (4 endpoint registrations + 4 handler skeletons + DTOs + DI registrations + integration test scaffolds)
**Estimated effort:** 1-2 days Engineer time
**Council SPOT-CHECK on Ready-flip:** sec-eng (pattern-009 PAIR MANDATORY per fleet-conventions §SPOT-CHECK SLA — 4 NEW routes; substrate-touch) + .NET-architect (substrate-touch on first-instance consumer of ADR 0095 + ADR 0096 substrates) + frontend-architect (pattern-009 PAIR — Bridge half).

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

- [ ] 4 endpoints registered inside `MapBootstrapEndpoints` body; all 4 carry `.AllowAnonymous()`.
- [ ] Rate-limit policies configured for all 4 endpoints; floors meet §3.7 minimum.
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

#### 4.2.1 SignupHandler — α-1 child-scope transition body

Per ADR 0095 §"Handler Lifecycle" 6-step ordering:

```csharp
namespace Sunfish.Bridge.Onboarding.Handlers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Authorization;
using Sunfish.Foundation.Bootstrap;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Integrations.Email;

public static class SignupHandler
{
    public static async Task<IResult> HandleAsync(
        [FromBody] SignupRequest request,
        IBootstrapContext bootstrapContext,
        ICaptchaVerifier captcha,
        IEmailProvider email,
        ITenantRegistry tenantRegistry,
        IServiceScopeFactory scopeFactory,
        IOperationSigner signer,
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
            return Results.Problem(
                title: "validation_failed",
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
            return Results.Problem(
                title: "captcha_failed",
                statusCode: 400,
                detail: "CAPTCHA verification failed.");
        }

        // STEP 3 — Email-uniqueness check via dedicated read-only DbContext.
        // (Implementation-detail per ADR 0095 Q4: W#79 picks among (a) opt-out DbContext registration,
        // (b) separate SunfishBridgeReadOnlyDbContext, (c) constructor-guard alone — see §5.4 below
        // for the W#79 disposition: option (b) — separate read-only DbContext for the uniqueness check.)
        // The read-only DbContext queries TenantRegistrations (control-plane; not under HasQueryFilter).

        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var slugNormalized = request.TenantSlug.Trim().ToLowerInvariant();

        // (Implementation: ITenantRegistry.CheckEmailUniqueAsync + .CheckSlugAvailableAsync
        // both query TenantRegistrations via the read-only DbContext.)
        var uniquenessCheck = await tenantRegistry.CheckUniquenessAsync(
            emailNormalized,
            slugNormalized,
            ct);

        if (uniquenessCheck.SlugReserved)
            return Results.Problem(title: "tenant_slug_reserved", statusCode: 400);

        if (uniquenessCheck.SlugTaken)
            return Results.Problem(title: "tenant_slug_taken", statusCode: 400);

        if (uniquenessCheck.EmailVerifiedTenantExists)
            return Results.Problem(title: "email_already_registered", statusCode: 400);

        if (uniquenessCheck.EmailUnverifiedTenantExists)
        {
            // Halt H2 disposition (preliminary): quiet re-send + 202 envelope to avoid
            // tenant-enumeration leak via signup-vs-resend probing.
            // Final disposition per Admiral ruling on Halt H2.
            var resendDispatchId = await SendVerificationEmailAsync(
                email, signer, emailNormalized, uniquenessCheck.ExistingTenantId!.Value,
                bootstrapContext, ct);
            return Results.Accepted(
                value: new SignupAcceptedResponse(
                    EmailDispatchId: resendDispatchId,
                    CorrelationId: bootstrapContext.CorrelationId));
        }

        // STEP 4 — Call ITenantRegistry.CreateAsync.
        // First production callsite per ADR 0095 §Decision drivers invariant.
        var newTenant = await tenantRegistry.CreateAsync(
            new CreateTenantCommand(
                EmailNormalized: emailNormalized,
                SlugNormalized: slugNormalized,
                DisplayName: request.TenantDisplayName,
                PasswordHash: PasswordHasher.Hash(request.Password)),
            ct);

        // STEP 5 — Child IServiceScope for post-tenant write (α-1 mechanism per ADR 0095).
        await using var childScope = scopeFactory.CreateAsyncScope();
        childScope.ServiceProvider
            .GetRequiredService<ITenantContextSeed>()
            .Bind(newTenant.TenantId);

        var userRepo = childScope.ServiceProvider.GetRequiredService<IUserAggregateRepository>();
        await userRepo.PersistInitialUserAsync(
            new InitialUser(
                TenantId: newTenant.TenantId,
                Email: emailNormalized,
                PasswordHash: newTenant.PasswordHash,
                EmailVerified: false),
            ct);
        // Child scope disposes here on await using.

        // STEP 6 — Bootstrap scope continues: audit-emission + email-dispatch.
        var emailDispatchId = await SendVerificationEmailAsync(
            email, signer, emailNormalized, newTenant.TenantId, bootstrapContext, ct);

        // Audit event: TenantSignup (one tenant created; verification email queued).
        // (Implementation per ADR 0049 audit substrate; emits to control-plane audit stream.)

        return Results.Accepted(
            value: new SignupAcceptedResponse(
                EmailDispatchId: emailDispatchId,
                CorrelationId: bootstrapContext.CorrelationId));
    }

    private static async Task<string> SendVerificationEmailAsync(
        IEmailProvider email,
        IOperationSigner signer,
        string emailNormalized,
        Guid tenantId,
        IBootstrapContext bootstrapContext,
        CancellationToken ct)
    {
        var verificationToken = signer.SignVerificationToken(
            new VerificationTokenPayload(
                Email: emailNormalized,
                TenantId: tenantId,
                IssuedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(24)));

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
}
```

**Critical invariants enforced in PR 1:**

- α-1 scoped-holder pattern per ADR 0095 Rev 2 Amendment 2 — `ITenantContextSeed.Bind()` IMMEDIATELY before any post-tenant resolution inside the child scope.
- `await using var childScope` — async-disposable; scope disposes after the write commits.
- `IUserAggregateRepository` resolved from child scope (NOT outer bootstrap scope) — confirms `SeededTenantContext` is reachable in the child scope.
- The outer `await SendVerificationEmailAsync` continues on the **outer bootstrap scope**, not the child scope — audit + email-dispatch are bootstrap-scope concerns per ADR 0095 §"Handler Lifecycle" step 6.
- `IEmailProvider` injection consumes mock (MockEmailProvider) until Engineer Tier-2 D ships PostmarkEmailProvider; production guard fires if not opted-out (`SUNFISH_ALLOW_MOCK_PROVIDERS=true` required for `ASPNETCORE_ENVIRONMENT=Production` mock-only deploys).
- `ICaptchaVerifier` likewise consumes InMemoryCaptchaVerifier (now `: IMockVendorProvider` via Step 1 retrofit).
- `PasswordHasher.Hash` shape MUST be ASVS Level 2 conformant (Argon2id or BCrypt 12+ cost; W#79 ratifies via halt H8 if substrate is missing).

#### 4.2.2 VerifyEmailHandler

```csharp
public static async Task<IResult> HandleAsync(
    [FromBody] VerifyEmailRequest request,
    IBootstrapContext bootstrapContext,
    ITenantRegistry tenantRegistry,
    IServiceScopeFactory scopeFactory,
    IOperationSigner signer,
    CancellationToken ct)
{
    // STEP 1 — Bootstrap-only scope.
    // STEP 2 — Verify signed token (Ed25519 per IOperationSigner).
    var verificationResult = signer.TryVerifyVerificationToken(request.VerificationToken);
    if (verificationResult is not OperationSignerResult.Valid valid)
    {
        return verificationResult switch
        {
            OperationSignerResult.Invalid     => Results.Problem(title: "verification_token_invalid", statusCode: 400),
            OperationSignerResult.Expired     => Results.Problem(title: "verification_token_expired", statusCode: 400),
            _                                  => Results.Problem(title: "verification_token_invalid", statusCode: 400),
        };
    }

    // STEP 3 — Look up Tenant by token payload (NOT URL slug).
    // The token carries tenant_id; the read-only DbContext loads it.
    var tenantLookup = await tenantRegistry.GetByIdAsync(valid.Payload.TenantId, ct);
    if (tenantLookup is null)
    {
        // Token signed a tenant_id that no longer exists. Treat as invalid token (uniform-404 / 400 invariant).
        return Results.Problem(title: "verification_token_invalid", statusCode: 400);
    }

    if (tenantLookup.EmailVerified)
    {
        // Token already redeemed. Idempotency invariant: return 200 with same response shape as fresh verify.
        // (Replay-attack defense: same response shape regardless of token-state to avoid leakage of "was already redeemed.")
        // OR: fail with verification_token_already_used (halt H9 — disposition needed).
        // W#79 RECOMMENDED disposition: return 200 (idempotent) to match resend-friendly UX; halt for Admiral.
        return Results.Ok(new VerifyEmailAcceptedResponse(
            Email: tenantLookup.AdminEmailNormalized,
            TenantSlug: tenantLookup.Slug,
            TenantDisplayName: tenantLookup.DisplayName));
    }

    // STEP 4 — Mark tenant + user as email-verified.
    // This is a write; uses child IServiceScope for the post-tenant write per ADR 0095.
    await using var childScope = scopeFactory.CreateAsyncScope();
    childScope.ServiceProvider
        .GetRequiredService<ITenantContextSeed>()
        .Bind(tenantLookup.TenantId);

    var userRepo = childScope.ServiceProvider.GetRequiredService<IUserAggregateRepository>();
    await userRepo.MarkEmailVerifiedAsync(tenantLookup.TenantId, valid.Payload.Email, ct);

    // STEP 6 — Audit emission (bootstrap scope).
    // EmailVerified audit event emitted; correlation-id carried from bootstrap context.

    return Results.Ok(new VerifyEmailAcceptedResponse(
        Email: tenantLookup.AdminEmailNormalized,
        TenantSlug: tenantLookup.Slug,
        TenantDisplayName: tenantLookup.DisplayName));
}
```

#### 4.2.3 ResendVerificationHandler — uniform-202

```csharp
public static async Task<IResult> HandleAsync(
    [FromBody] ResendVerificationRequest request,
    IBootstrapContext bootstrapContext,
    ITenantRegistry tenantRegistry,
    IEmailProvider email,
    IOperationSigner signer,
    CancellationToken ct)
{
    // ALWAYS returns 202; uniform-202 invariant.
    var emailNormalized = request.Email.Trim().ToLowerInvariant();

    // (Per-email rate-limit key — separate from per-IP — applied at middleware level.)

    var lookup = await tenantRegistry.GetByEmailAsync(emailNormalized, ct);
    if (lookup is not null && !lookup.EmailVerified)
    {
        // Quietly re-send verification email; do not surface lookup result.
        _ = await SendVerificationEmailAsync(
            email, signer, emailNormalized, lookup.TenantId, bootstrapContext, ct);
    }

    return Results.Accepted(value: new ResendVerificationResponse(Status: "queued"));
}
```

#### 4.2.4 CheckAvailabilityHandler — uniform `available` boolean

```csharp
public static async Task<IResult> HandleAsync(
    [FromBody] CheckAvailabilityRequest request,
    ITenantRegistry tenantRegistry,
    CancellationToken ct)
{
    // Shape validation first.
    if (request.Field is not ("email" or "tenant_slug"))
        return Results.Problem(title: "validation_failed", statusCode: 400);

    if (string.IsNullOrWhiteSpace(request.Value) || request.Value.Length > 256)
        return Results.Problem(title: "validation_failed", statusCode: 400);

    var normalized = request.Value.Trim().ToLowerInvariant();

    var available = request.Field switch
    {
        "email"       => await tenantRegistry.IsEmailAvailableAsync(normalized, ct),
        "tenant_slug" => await tenantRegistry.IsSlugAvailableAsync(normalized, ct),
        _              => false
    };

    return Results.Ok(new CheckAvailabilityResponse(Available: available));
}
```

#### 4.2.5 MockEmailProvider production-guard verification

PR 1's deployment story is **Shape A — mocks-only** per ADR 0096 §"Step 4 W79 composition-root wiring." Two distinct test surfaces verify the guard:

- **Test M1 — Production-default fail.** `WebApplicationBuilder().Build()` with `AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>()` + `ASPNETCORE_ENVIRONMENT=Production` + NO opt-out + NO `POSTMARK_API_KEY` env var → assert `IHost.StartAsync` throws `MockInProductionException` whose message names `IEmailProvider` AND `POSTMARK_API_KEY`. (Confirms ADR 0096 D1c silent-typo foot-gun closer.)
- **Test M2 — Production-allow-with-opt-out.** Same fixture but with `SUNFISH_ALLOW_MOCK_PROVIDERS=true` → assert `IHost.StartAsync` succeeds; signup endpoint works against MockEmailProvider; email dispatched to in-memory store; no body leaks to console log.

Tests live in `signal-bridge/Sunfish.Bridge.Tests/Onboarding/MockProviderGuardIntegrationTests.cs`.

#### 4.2.6 Acceptance criteria for PR 1

- [ ] SignupHandler body implements 6-step α-1 transition per ADR 0095 §"Handler Lifecycle."
- [ ] `ITenantContextSeed.Bind()` called IMMEDIATELY before any post-tenant resolution inside the child scope.
- [ ] `await using` async-disposable child scope; outer scope continues for audit + email.
- [ ] `SunfishBridgeDbContext` NEVER resolved in bootstrap scope; constructor guard verified (test asserts `InvalidOperationException` when resolved from bootstrap-branch DI scope).
- [ ] Email-uniqueness check via separate read-only DbContext (W#79 disposition of ADR 0095 Q4 = option b; §5.4).
- [ ] `ITenantRegistry.CreateAsync` first production callsite working; control-plane `TenantRegistrations` write succeeds; outside HasQueryFilter set.
- [ ] VerifyEmailHandler validates Ed25519 token, looks up tenant by payload tenant_id, marks user verified inside child scope.
- [ ] ResendVerificationHandler returns uniform-202 regardless of email-existence; re-sends only when tenant is unverified.
- [ ] CheckAvailabilityHandler returns uniform `available` boolean; no reason-code leakage.
- [ ] MockEmailProvider production-guard tests M1 + M2 passing per §4.2.5.
- [ ] MockEmailProvider console-log discipline: emits To[]/Subject/EmailDispatchId/IdempotencyKey/MessageStream only — NEVER BodyHtml/BodyText (ADR 0096 Rev 2 Amendment #6).
- [ ] InMemoryCaptchaVerifier carries `IMockVendorProvider` marker (consumed test).
- [ ] Audit emission on signup-completion + email-verified — per ADR 0049 substrate; correlation-id flows from bootstrap context.
- [ ] PR description includes acceptance-criteria checklist + §A0 cited-symbol audit.
- [ ] Pre-flight commit-message check (Amendment K) clean.

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
| `sunfish/apps/web/src/api/onboarding.ts` | new | TanStack mutation hooks: `useSignup`, `useVerifyEmail`, `useResendVerification`, `useCheckAvailability`; typed-error contracts per §3.5 |
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
export class EmailAlreadyRegisteredError extends Error { readonly cause = 'email_already_registered'; }
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
          case 'email_already_registered':  throw new EmailAlreadyRegisteredError();
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

Same shape for `useVerifyEmail` + `useResendVerification` + `useCheckAvailability` per §3.2/3.3/3.4 shapes.

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

#### 4.3.4 Test expectations — `SignupPage.test.tsx`

Minimum 10 React Testing Library tests:

| # | Test | Closes |
|---|---|---|
| 1 | `renders signup form with email + password + slug + display-name fields` | baseline |
| 2 | `client-side validation prevents submit when slug fails regex` | baseline + Amendment J discriminator (matches `tenant_slug_invalid_shape` server-side discriminator) |
| 3 | `submission posts canonical JSON shape to /api/v1/auth/signup` | §3.1 wire contract |
| 4 | `200 response navigates to /auth/verify-email/pending with email_dispatch_id` | §3.1 happy path |
| 5 | `400 email_already_registered surfaces friendly inline message` | §3.5 typed error |
| 6 | `400 tenant_slug_taken surfaces field-scoped error on slug input` | §3.5 typed error |
| 7 | `429 rate_limited displays Retry-After countdown` | §3.7 rate-limit + Retry-After header read |
| 8 | `403 origin_invalid surfaces transport-failure banner (NOT user-correctable)` | §3.6 origin |
| 9 | `captcha widget renders in mock mode with mock-pass token shape` | dev-mode CAPTCHA |
| 10 | `frontend does NOT declare tenant_id or verification_token in TypeScript interfaces` | Amendment I negative-match (static-check via type assertion test) |
| 11 | `error handler reads body.title, never body.error` | Amendment J discriminator pin |
| 12 | `password field does not log to console on submit` | defense-in-depth (no-log-secret) |

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

#### 4.3.6 Acceptance criteria for PR 2

- [ ] All 12 SignupPage RTL tests + all 6 VerifyEmailPage RTL tests passing.
- [ ] TypeScript interfaces in `onboarding.types.ts` carry ONLY POSITIVE-match fields per §3.1-§3.4 (no `tenant_id`, no `verification_token`, no `session_token`, no `password_hash`).
- [ ] Cycle-1 DRAFT posture is AMBER (banner present + forward-watch comment) per §3.9 + §4.3.3.
- [ ] Cycle-2 amendment commit removes banner; full flow operational.
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

#### 4.4.2 Cross-stack invariants asserted

- **Happy path:** signup → 202 with email_dispatch_id → verify-email page accepts token from email body → 200 with tenant_slug + tenant_display_name. Full stack alive.
- **Rate-limit floor:** 6 rapid signups from same IP → 6th returns 429 with `Retry-After` header set.
- **Mock-email inbox:** signup → MockEmailProvider receives the EmailMessage with `MessageStream: "transactional-onboarding"`; the in-memory store accessible via test-only inspection API (Halt: see Halt H1 for the dev `/dev/inbox` UI route decision); the server log does NOT contain `BodyText` or `BodyHtml` (ADR 0096 Rev 2 Amendment #6 verification).
- **Mock-vendor production-guard fires:** test fixture spins up `WebApplicationBuilder` with `ASPNETCORE_ENVIRONMENT=Production` + no opt-out → `IHost.StartAsync` throws `MockInProductionException`. (Covered already by PR 1 §4.2.5 test M1; PR 3 cross-links.)
- **α-1 child-scope correctness:** signup completes → User aggregate written via `IUserAggregateRepository` resolved from child scope → outer bootstrap scope's audit emission carries correct correlation-id. (Asserted via cross-stack log inspection.)
- **Bridge endpoint conformance to ProblemDetails:** all 4xx responses carry `title` field (not `error`); verified by MSW-handler authoring against real Bridge response capture.

#### 4.4.3 Acceptance criteria for PR 3

- [ ] All Playwright e2e specs pass against test-env Bridge + MockEmailProvider.
- [ ] MSW handlers in `onboarding-handlers.ts` mirror server DTO shapes byte-for-byte per §3.1-§3.4.
- [ ] Contract-test suite asserts every 400 discriminator from §3.5 has a corresponding typed-error class.
- [ ] test-eng-council SPOT-CHECK GREEN on Ready-flip (coverage-model gate per ADR 0093 Rev 4).
- [ ] PR description references the 12 SignupPage + 6 VerifyEmailPage tests from PR 2 as the unit-tier; PR 3's contribution is the integration + e2e tier.
- [ ] Pre-flight commit-message check (Amendment K) clean.

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
| `CheckAvailabilityHandler returns uniform boolean; no reason-code leakage` | PR 1 | enumeration-leak defense | handler |
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

**Cumulative test count target:** ~25 new tests across substrate-DI / handler / page / e2e layers.

---

## 6. Stage-05 adversarial review framework spec (ADR 0093 Rev 4)

Per ADR 0093 Rev 4 §"Adversarial Brief — template" + the 5-8 (or 12 for substrate-shaping) bullet cap: W#79 is substrate-shaping (consumes 2 new substrate ADRs; introduces 4 new routes; first-instance candidate for 3 patterns). The brief extends to up to 12 bullets per the substrate-shaping escape hatch.

### 6.1 Adversarial Brief

#### Decision 1 — `/api/v1/auth/*` path prefix on bootstrap pipeline branch

- **Decision summary:** Use `/api/v1/auth/*` prefix for signup-family endpoints; bootstrap pipeline branch's `UseWhen` predicate matches `/api/v1/auth/` AND `/api/invitations/accept`. ADR 0095 §"Pipeline routing" used `/api/signup` — W#79 uses `/api/v1/auth/*` for versioned auth API consistency. (Halt H4 surfaces this for Admiral reconciliation.)
- **Worst-case interpretation:** An attacker discovers that the bootstrap-branch predicate is path-prefix-based; crafts a non-onboarding endpoint at `/api/v1/auth/leaky` and observes that it skips `TenantSubdomainResolutionMiddleware`, exposing tenant-bound surfaces via the bootstrap branch.
- **Failure mode:** Cross-tenant data leak via a non-onboarding endpoint accidentally routed through the bootstrap branch.
- **Mitigation in this hand-off:** `MapBootstrapEndpoints` chains only `MapOnboardingEndpoints` (W#79) + `MapInvitationsEndpoints` (W#82); the `UseWhen` predicate matches narrow path prefixes, not the whole `/api/v1/`. Step 3 Roslyn analyzer enforces that any handler registered via `MapBootstrapEndpoints` does NOT inject post-tenant interfaces. Sec-eng SPOT-CHECK on PR 0 verifies the prefix list.

#### Decision 2 — `email_already_registered` shape (verified vs unverified Tenant)

- **Decision summary:** signup against a verified-Tenant email returns 400 `email_already_registered`; signup against an unverified-Tenant email returns 202 `queued` (quietly re-sends verification). Distinct response shapes are a tenant-existence leak.
- **Worst-case interpretation:** An attacker probes signup with arbitrary emails. 400 `email_already_registered` reveals verified-tenant existence; 202 `queued` reveals nothing about whether the email is unknown or unverified-registered. The asymmetric response leaks information.
- **Failure mode:** Email-enumeration → targeted phishing of confirmed Sunfish customers.
- **Mitigation in this hand-off:** Halt H2 surfaces this for Admiral disposition. Mitigation options: (a) always-202 (drops the `email_already_registered` discriminator entirely — frontend can't distinguish; UX regresses for legitimate "I forgot I had an account" flows); (b) always-202 with an out-of-band notification email "someone tried to sign up with your address"; (c) rate-limit-aggressive (per-email per-day) to make enumeration economically expensive. ONR recommends (b) + (c); Admiral disposition needed.

#### Decision 3 — Verification token re-use semantics

- **Decision summary:** verify-email handler treats already-verified tenant as success (200 with same response shape as fresh-verify). Idempotency-friendly. (Halt H9.)
- **Worst-case interpretation:** Token-replay window: a captured verification URL (stolen email log; victim's browser history) replays successfully after the user has verified. Attacker hits the verify endpoint with the captured token and gets the tenant_slug + display_name back (no auth token, but a tenant-existence and naming confirmation).
- **Failure mode:** Information disclosure (tenant_slug + display_name to an attacker who captured a verification URL).
- **Mitigation in this hand-off:** Token TTL is 24 hours per §4.2.1 (`SignVerificationToken` `ExpiresAt`). Verification token is one-shot in terms of state-change but multi-shot for read-back. Defense-in-depth: token is delivered exclusively via verified email + HTTPS; if email is compromised, the threat model already includes account takeover. ALTERNATIVE: 410 Gone on already-verified to deny replay-read; UX regresses for legitimate "I already verified" cases. Halt H9 surfaces for Admiral disposition.

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

---

## 7. Council dispatch — per-PR trigger matrix

Per ADR 0093 Rev 4 §"Council dispatch — trigger matrix" + fleet-conventions §SPOT-CHECK dispatch SLA:

| PR | sec-eng-council (Stage-05 + SPOT-CHECK) | .NET-architect-council | frontend-architect | test-eng-council |
|---|---|---|---|---|
| **Stage-05 hand-off** (this document) | YES (substrate-touch — first-instance consumer of 2 new substrate ADRs; new endpoint family; new patterns claimed) | YES (substrate-touch) | NO (Stage-05 is sec-eng + .NET-arch + test-eng; frontend-arch enters at PR 2 Ready-flip) | YES (>5 test cases + substrate-touching; coverage-model verification) |
| **PR 0** (Bridge endpoints; pattern-009 PAIR) | YES MANDATORY (pattern-009 + 4 new routes + first-instance pattern claims) | YES MANDATORY (substrate-touch + ITenantContextSeed package home) | YES MANDATORY (pattern-009 PAIR — Bridge half) | NO (Stage-05 already gated it) |
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

## 11. Decisions surfaced (route to Admiral via inbox)

Per `feedback_onr_questions_via_inbox`:

1. **H1** — MockEmailProvider `/dev/inbox` scope (W#79 inspection endpoint only; UI deferred to W#80; ONR recommends option c).
2. **H2** — `email_already_registered` discriminator (always-202 + OOB notification + per-email rate-limit; ONR recommends b+c).
3. **H3** — Verify-email idempotency-key (optional; token carries idempotency-semantics; ONR recommends b).
4. **H4** — Onboarding cluster home + path prefix (new project + `/api/v1/auth/*`; ONR recommends a).
5. **H5** — PR 0/PR 1 split (two PRs per readability; ONR recommends two; Engineer discretion).
6. **H6** — `ITenantContextSeed` package home (`foundation-authorization`; ONR recommends a per ADR 0095 .NET-arch A2 follow-on).
7. **H7** — `EmailDispatchOptions.FromAddress` substrate-tier or hard-code (substrate-tier; ONR recommends a; ~30 LOC).
8. **H8** — `PasswordHasher` substrate (reuse `IPasswordHasher<TUser>`; future ADR 0097 hardens to Argon2id; ONR recommends b; substrate-shaping — Admiral may want dedicated ADR).
9. **H9** — Verify-email already-redeemed (200 idempotent + reduce TTL to 1h; ONR recommends a+d).
10. **H10** (forward-watch only, no halt) — Turnstile site-key delivery deferred to W#80 per ADR 0096 Halt 7.

ONR recommends Admiral file a single ruling-beacon resolving H1–H9 in one pass before Engineer opens PR 0; this avoids 9 separate beacon round-trips.

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

— ONR, 2026-05-25T22:50Z
