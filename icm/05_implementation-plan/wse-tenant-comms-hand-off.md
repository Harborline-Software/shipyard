# WS-E tenant communications — Stage-05 hand-off

**Workstream:** WS-E (tenant-facing communications)
**Authored by:** ONR
**Authored at:** 2026-05-29T02:35Z
**Requester:** CIC (via Admiral; pipelining directive — build sequenced AFTER Cohort-5)
**Status:** draft (pending Admiral consumption + dual/triple-council Stage-05 review per ADR 0093 Rev 4)
**Pipelining note:** Build is sequenced AFTER Cohort-5 (P1 Property Mgmt) per the
2026-05-29 MVP feature-priority survey (`coordination/inbox/research-mvp-feature-priority-2026-05-29T0205Z.md`,
P3). No rush — correctness + completeness over speed. This hand-off is spec-ready
so an engineer can pick it up the moment Cohort-5 lands + the magic-link substrate
question (H-WSE-1) is ruled on.

---

## 0. Scope-of-investigation memo

**In scope:** A Stage-05 implementation hand-off for two tenant-facing communications
deliverables building on the **ADR 0096 Tier-2 vendor-provider substrate** (the live
`Foundation.Integrations.Email/IEmailProvider` substrate, NOT the older ADR-0052 /
W#20 bidirectional messaging substrate — see §1 reconciliation):
1. The **real Postmark email adapter** (`packages/providers-postmark/`) — the Tier-2
   real-impl behind the existing `IEmailProvider`, registered conditionally on
   `POSTMARK_API_KEY` per mock-first discipline (MockEmailProvider stays default).
2. The **magic-link tenant portal** — passwordless tenant authentication: token
   issuance (single-use, TTL-bounded, atomic-redemption per the W#79
   `TryConsumeEmailVerificationAsync` pattern), Bridge endpoints, frontend
   (pattern-009 pair), security (enumeration defense, rate-limiting, token entropy).

**Out of scope:**
- The ADR-0052 / W#20 bidirectional messaging substrate (`IOutboundMessageGateway`,
  `blocks-messaging`, SSE inbound channel, 6-layer inbound defense). That is a
  SEPARATE, heavier substrate (inbound webhooks, threads, A2P 10DLC) and is NOT what
  the directive or the live ADR-0096 substrate scope. See §1 for the reconciliation
  and the recommendation to formally shelve W#20 Phase 4-9 in favor of the ADR-0096
  path. (Halt H-WSE-7.)
- SMS / Twilio (A2P 10DLC) — post-MVP; the magic-link channel is email-only for MVP.
- Real Turnstile CAPTCHA adapter (`providers-turnstile`) — separate Tier-2 E PR
  (mirrors the Postmark pattern; the magic-link portal CONSUMES `ICaptchaVerifier`
  but does not author the real adapter).
- Email-templating engine + the dev `/dev/inbox` UI (W#80 territory per W#79 H1).
- Session establishment after magic-link verification — depends on the auth-session
  substrate (see §1 dependency note + H-WSE-2).

**Authoritative sources consulted:** ADR 0096 (Accepted 2026-05-25T21:45Z) + the live
`foundation-integrations/Email/*` + `DependencyInjection/*` source; the W#79 Stage-05
hand-off (`cohort-w79-hand-off.md`, 2147 lines — the canonical Stage-05 template + the
atomic-redemption + pattern-009 + rate-limit-floor + adversarial-brief precedents); the
CIC mock-first ruling (2026-05-25T18-10Z); the onboarding-ladder ruling
(2026-05-25T1450Z); ADR 0093 Rev 4 (Stage-05 adversarial-review protocol); ADR 0095
(Bootstrap Context); the prior WS-E prereq scoping + W#20 Phase 4-9 addendum (now
superseded for the email path — see §1).

**What success looks like:** An engineer can open the first PR (Postmark adapter) the
moment Cohort-5 lands + Admiral rules on H-WSE-1 (magic-link substrate home) +
H-WSE-2 (session-establishment dependency), with no further design discovery: every
wire contract, security floor, atomic-redemption gate, PR boundary, and council
trigger is pinned here.

---

## 1. Substrate reconciliation — TWO email substrates exist; WS-E builds on ADR 0096

**This is the single most load-bearing finding in this hand-off. Read it first.**

There are two distinct, non-overlapping email/messaging substrates in shipyard. A
build engineer reading the older WS-E artifacts could waste days building the wrong
one. The reconciliation:

| Axis | ADR-0052 / W#20 (OLDER; for WS-E email purposes: SUPERSEDED) | ADR-0096 (LIVE; this hand-off builds here) |
|---|---|---|
| Contract | `IOutboundMessageGateway` + `IInboundMessageReceiver` | `IEmailProvider.SendAsync(EmailMessage, ct)` |
| Package | `blocks-messaging` + planned `providers-postmark` (never shipped) | `foundation-integrations/Email/` (SHIPPED) + planned `providers-postmark` |
| Shape | Bidirectional: threads, participants, inbound webhooks, SSE relay, 6-layer defense, A2P 10DLC | Unidirectional fire-and-forget transactional email; no thread, no inbound, no webhook |
| Status | Phases 0-3 shipped per W#20 addendum; Phase 4-9 (incl. Postmark) NEVER built | Substrate Accepted + on main; MockEmailProvider live; W#79 onboarding CONSUMES it (verify-email / resend) today |
| Production-guard | none | `MockProviderProductionGuardAssertion` (fail-closed prod default) |

ADR 0096 §D2 makes the divergence explicit in the live code: `IEmailProvider` is
"structurally distinct from `IMessagingGateway` ... no thread-id, no participants
array, no inbound webhook surface." The directive cites ADR 0096, `IEmailProvider`,
`MockEmailProvider`, and the W#79 onboarding flows — all the ADR-0096 path. **WS-E's
email adapter is the ADR-0096 Postmark adapter, full stop.**

**Recommendation (Halt H-WSE-7):** Formally mark the W#20 Phase 4-9 addendum
(`property-messaging-substrate-stage06-phases-4-9-addendum.md`) as SUPERSEDED-FOR-EMAIL
by ADR 0096 for the MVP. The bidirectional substrate (inbound tenant replies, threaded
crew-comms, SMS) remains a valid post-MVP workstream — but it is NOT WS-E-for-MVP and
NOT what the Postmark adapter in this hand-off targets. Admiral to confirm so a future
engineer doesn't resurrect the heavyweight path.

### 1.1 Substrate consumed (cross-references)

- **ADR 0096** Tier-2 Vendor-Provider Substrate — Accepted 2026-05-25T21:45Z
  (`shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md`). The
  `AddSunfishVendorProvider<TContract,TConcrete>()` + `UseVendorProviderIfConfigured<TContract,TReal>(envVarKey)`
  + `MockProviderProductionGuardAssertion` substrate IS the Postmark-adapter
  registration mechanism. Live source: `packages/foundation-integrations/`.
- **CIC mock-first ruling** (`coordination/inbox/admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`)
  — Postmark ratified; mock-first is the canonical Tier-2 discipline; "switch is
  config-only" (`POSTMARK_API_KEY` present → real adapter swaps in).
- **W#79 Stage-05 hand-off** (`cohort-w79-hand-off.md`) — the canonical Stage-05
  template + three load-bearing precedents WS-E reuses verbatim-in-spirit:
  (a) the atomic single-use redemption gate (§6.1 Decision 3:
  `IBootstrapTenantRegistry.TryConsumeEmailVerificationAsync` — read row, assert
  flag-false, atomically transition true, return bool; already-consumed falls
  through to byte-identical 200-idempotent shape);
  (b) the pattern-009 Bridge-then-frontend pair + wire-contract reconciliation
  (§3) + RFC 7807 `title`-discriminator convention;
  (c) the non-permissive rate-limit minimum-floors (§3.7) + per-entity (per-email)
  SHA-256-prefix bucket key (§3.7 .NET-arch G2).
- **ADR 0095** Bootstrap Context — the magic-link request/verify endpoints are
  PRE-tenant in the same sense as signup/verify-email (the magic-link is consumed
  by an as-yet-unauthenticated tenant user), so they belong on the bootstrap
  pipeline branch (`MapBootstrapEndpoints`) with `.AllowAnonymous()`, antiforgery
  posture, and Origin validation — same surface as W#79's onboarding family.
- **ADR 0093 Rev 4** Stage-05 Adversarial Review Protocol — governs §6 (adversarial
  brief) + §7 (council trigger matrix).
- **ADR 0046** (operation signing — `IOperationSigner` Ed25519) — magic-link tokens
  are server-signed bearer tokens; same signing primitive as W#79's verification
  token (Ed25519, carries `sub`/`tenant`/`nbf`/`exp`/`aud`).

### 1.2 Dependencies in flight (NOT yet on main — gating)

- **W#79 onboarding pipeline (`Sunfish.Bridge.Onboarding`, `IBootstrapTenantRegistry`,
  `BootstrapTenantRegistry`, `MapBootstrapEndpoints`)** — NOT on main; only in the
  ADR-0095-step-2 worktree. Magic-link reuses the bootstrap pipeline branch + the
  atomic-redemption pattern, so **W#79 PR 0 + PR 1 must land before the magic-link
  PRs open.** The Postmark adapter PR (PR-1 of this hand-off) does NOT depend on
  W#79 — it can open as soon as ADR 0096 substrate is on main (it is) + a real
  Postmark sandbox account exists.
- **Auth-session substrate** — W#79 explicitly does NOT establish a session on
  verify-email ("session establishment is W#80 sub-cohort 2 scope," §3.2). Magic-link's
  WHOLE POINT is to establish a tenant session. So magic-link's verify step needs the
  session-issuance primitive that W#79 deferred. **This is the gating substrate
  question — H-WSE-2.** If the session substrate isn't ready, magic-link verify can
  only validate-and-redirect-to-login (degraded), not log the user in.

---

## 2. PR-1 — Postmark email adapter (`packages/providers-postmark/`)

**Layer:** shipyard (new package). **Est:** 1 PR, ~400-500 LOC + ~250 LOC tests.
**Council:** sec-eng (MANDATORY — first real Tier-2 vendor adapter; secret handling)
+ .NET-architect (MANDATORY — sets the pattern for every future real Tier-2 adapter).
**Pattern:** first-instance REAL-side of `pattern-tier2-mock-first-substrate` (W#79
claimed the mock side; this is the real-adapter half — see §6.2).

### 2.1 What ships

A new package `shipyard/packages/providers-postmark/Sunfish.Providers.Postmark.csproj`
that implements `IEmailProvider` against the Postmark transactional-email HTTP API,
registered via the existing substrate helper:

```csharp
// At the signal-bridge composition root, AFTER the mock registration:
services.AddSunfishVendorProviderSubstrate();
services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();   // default
services.UseVendorProviderIfConfigured<IEmailProvider, PostmarkEmailProvider>("POSTMARK_API_KEY");
```

When `POSTMARK_API_KEY` is non-empty, `UseVendorProviderIfConfigured` does a
`services.Replace` swapping the mock for `PostmarkEmailProvider` (inheriting the prior
descriptor's lifetime — Singleton). When absent, the mock stays + the
`MockProviderProductionGuardAssertion` fails closed at startup in Production unless
`SUNFISH_ALLOW_MOCK_PROVIDERS=true`. **No code change to swap — config only**, exactly
per the CIC mock-first ruling.

`PostmarkEmailProvider` MUST NOT implement `IMockVendorProvider` (the marker asymmetry
is the canonical mock-vs-real discriminator; `UseVendorProviderIfConfigured` carries
no marker constraint and the production guard relies on real adapters being
marker-free).

### 2.2 Secret handling — `POSTMARK_API_KEY` (sec-eng-critical)

Two distinct uses of the env var, by design (per the substrate xmldoc):

1. **Registration-gate read** — `UseVendorProviderIfConfigured` reads
   `Environment.GetEnvironmentVariable("POSTMARK_API_KEY")` DIRECTLY (not via
   `IConfiguration`) because the helper runs before `BuildServiceProvider`. This read
   is presence-only (non-empty → swap fires); the value is NOT retained.
2. **Request-time secret** — the adapter's `HttpClient` call needs the actual key in
   the `X-Postmark-Server-Token` header. This MUST route through
   `IOptionsMonitor<PostmarkOptions>` bound from `IConfiguration` (standard
   ASP.NET Core options pattern), NOT a captured env-var string. This lets the key
   live in user-secrets (dev), env var (container), or a KeyVault/secret-store
   provider (prod) without code change.

**Security floors (sec-eng SPOT-CHECK verifies all):**
- F1 — `PostmarkOptions.ServerToken` is NEVER logged. The adapter's structured logs
  capture envelope only (To / Subject / MessageStream / vendor MessageId), matching
  the MockEmailProvider log discipline (no body — bodies carry magic-link tokens).
- F2 — The `X-Postmark-Server-Token` header value is never echoed in exception
  messages, telemetry, or `EmailDispatchResult.ErrorDetail`. On a 401 from Postmark,
  the adapter maps to a generic `Rejected("provider authentication failed")` — it does
  NOT include the (possibly-truncated) token.
- F3 — `IdempotencyKey` fingerprinting: same as the mock — never log in full (ADR 0096
  Floor 6; may carry user-derived entropy). The adapter forwards the key to Postmark's
  request-level idempotency header but logs only a length+suffix fingerprint.
- F4 — TLS-only egress; reject non-HTTPS Postmark base URL at options validation.
- F5 — `BannedSymbols.txt` enforced by the `SUNFISH_PROVNEUT_001` Roslyn analyzer
  (per ADR 0013 provider-neutrality gate): the adapter MUST NOT expose Postmark
  template engines, vendor scheduling (`DeliveryStartAt`), vendor suppression lists,
  or vendor analytics endpoints. Content is rendered upstream; the adapter only sends
  the finalized `EmailMessage`. (Carry-forward from W#20 addendum §2.4 — still valid
  for the ADR-0096 adapter.)

### 2.3 Postmark error → `EmailDispatchResult` mapping

The ADR-0096 `EmailDispatchResult` is a 4-status discriminated record (Accepted /
Rejected / RateLimited / TransportError) — simpler than the W#20 8-variant union
(which was for the heavyweight gateway). Map Postmark outcomes onto the 4 statuses:

| Postmark signal | HTTP | → `EmailDispatchResult` |
|---|---|---|
| Success (`ErrorCode: 0`) | 200 | `Accepted(MessageID)` |
| `10` Bad/missing API token | 401 | `Rejected("provider authentication failed")` (F2: no token echo) |
| `300` Invalid email request | 422 | `Rejected(sanitized Postmark message)` |
| `406` No sender signature | 422 | `Rejected("sender not authorized")` |
| `412` Recipient on inactive list | 422 | `Rejected("recipient blocked")` |
| `429` Rate limit exceeded | 429 | `RateLimited(retryAfter from header)` |
| `5xx` server errors | 5xx | `TransportError(...)` — caller MAY retry on its own outbox |
| Network/timeout | n/a | `TransportError(...)` |

**Resilience:** adapter-owned via `Microsoft.Extensions.Http.Resilience`
(`AddResilienceHandler`): retry (3 attempts, exponential + jitter) on 429/5xx/network;
circuit-breaker (0.5 failure ratio); 30s timeout. This matters less than in the W#20
gateway model (the email substrate is fire-and-forget; the caller does not block on
delivery confirmation) but the standard pipeline is cheap insurance against Postmark
blips. The adapter returns `TransportError` (not an exception) when retries exhaust,
so callers (e.g., signup, magic-link request) can decide whether to surface or queue.

### 2.4 `EmailDispatchOptions.FromAddress` (resolves W#79 Halt H7)

W#79 Halt H7 flagged that `From: "noreply@sunfish.app"` was hard-coded with no
options-type. WS-E is the natural place to resolve it — the magic-link emails AND the
Postmark adapter both need a configured From-address + sender-signature alignment.
PR-1 ships `EmailDispatchOptions` in `foundation-integrations/Email/` (FromAddress +
default MessageStream) bound via `IOptions<EmailDispatchOptions>`. **Flag to Admiral:**
if W#79 already shipped this between this authoring and build, PR-1 consumes the
existing type instead (pre-flight `grep EmailDispatchOptions`). This is a small
substrate touch; .NET-arch SPOT-CHECK confirms placement.

### 2.5 Tests (≥10)

`PostmarkEmailProvider_Success_ReturnsAcceptedWithMessageId`;
`_AuthRejected_ReturnsRejected_WithoutTokenEcho` (F2);
`_RateLimited_ReturnsRateLimited_WithRetryAfter`;
`_TransientError_RetriesThenTransportError`;
`_InvalidRequest_MapsToRejected`;
`_IdempotencyKey_ForwardedAndFingerprintedInLog` (F3);
`_ServerToken_NeverAppearsInLogsOrErrorDetail` (F1+F2; log-capture assertion);
`_NonHttpsBaseUrl_FailsOptionsValidation` (F4);
`_VendorTemplateApi_BannedSymbolsAnalyzerFails` (F5; compile-time);
`_RegisteredViaUseVendorProviderIfConfigured_SwapsWhenEnvVarPresent` (substrate
integration — mock present without env var, real present with env var);
`_DoesNotImplementMockVendorMarker` (reflection assertion — production-guard relies
on this).

WireMock.NET stub-server for the HTTP layer (per the W#20 addendum cassette
recommendation, still valid); no live Postmark calls in `dotnet test` (gate live
tests behind an opt-in env var).

---

## 3. PR-2/PR-3 — Magic-link tenant portal

**Layer:** signal-bridge (Bridge endpoints, PR-2) + sunfish web (frontend, PR-3) —
pattern-009 PAIR. **Council:** sec-eng MANDATORY (passwordless auth = highest-value
attack surface; token entropy + enumeration + replay) + .NET-architect MANDATORY
(new token substrate + session-establishment seam) + frontend-architect MANDATORY
(pattern-009 frontend half).

### 3.0 Flow overview

Passwordless tenant access. Two endpoints + two pages:

```
1. Tenant user enters email at /portal/login (frontend)
       │ POST /api/v1/auth/magic-link/request  { email, captcha_token }
       ▼
2. Bridge: validate captcha → look up user by email →
   (ALWAYS-202 regardless of whether email exists — enumeration defense) →
   if known: mint single-use Ed25519 token (TTL 15 min) → IEmailProvider.SendAsync(magic-link email)
       │ email body carries: https://<apex>/portal/verify#token=<signed-token>
       ▼
3. Tenant user clicks link → /portal/verify (frontend reads token from URL fragment)
       │ POST /api/v1/auth/magic-link/verify  { token }
       ▼
4. Bridge: verify Ed25519 signature + nbf/exp + aud → atomically consume
   (TryConsumeMagicLink: assert unconsumed, mark consumed, return bool) →
   if first-consume: establish session (H-WSE-2) → 200 { session established }
   if already-consumed OR invalid: byte-identical handling (no leakage)
```

The flow deliberately mirrors W#79's signup → verify-email shape (same bootstrap
branch, same Origin/antiforgery/rate-limit posture, same atomic-redemption gate, same
RFC-7807 discriminator convention) so a build engineer reuses W#79's PR 0/PR 1 code
patterns nearly verbatim.

### 3.1 Token design (sec-eng-critical)

- **Signing:** Ed25519 via `IOperationSigner` (ADR 0046) — SAME primitive as W#79's
  verification token. The token is a `SignedOperation<MagicLinkPayload>` where
  `MagicLinkPayload = { sub: <userId>, tenant: <tenantId>, jti: <random 128-bit>,
  nbf, exp, aud: "magic-link" }`.
- **Entropy:** the `jti` (token id) is 128 bits from a CSPRNG
  (`RandomNumberGenerator`). The signature provides integrity; the `jti` provides the
  single-use redemption key (stored server-side in the consumed-set).
- **TTL:** 15 minutes (tighter than W#79's verify-email 1h — magic-link is an active
  login, not a one-time account activation; ASVS recommends short-lived login tokens).
  Halt H-WSE-3 surfaces TTL for Admiral/sec-eng ruling (15 min is ONR's recommendation;
  council may want 10 or 5).
- **Single-use atomic consumption:** mirrors W#79 §6.1 Decision 3. A
  `IMagicLinkRegistry.TryConsumeAsync(jti, ct)` reads the consumed-set row (keyed on
  `jti`), asserts unconsumed, atomically marks consumed (DB unique-constraint or
  conditional-update as the ultimate race guard), returns bool. First-consume → true →
  proceed to session establishment; already-consumed → false → fall through to the
  byte-identical "invalid or expired" 200/4xx handling (NO distinct "already-used"
  discriminator — same lesson as W#79's retired `verification_token_already_used`).
- **Delivery channel:** token travels ONLY in the emailed link (URL fragment so it is
  never sent to the server in a GET / never logged in access logs / never in
  Referer). Frontend reads `window.location.hash`, POSTs it in the request body. Never
  in a query string.

### 3.2 Bridge endpoints (PR-2) — wire contracts

Both endpoints register inside `MapBootstrapEndpoints` (pre-tenant; the user is not
yet authenticated) with `.AllowAnonymous()`, antiforgery posture per the bootstrap
branch, Origin-header validation (403 on non-apex), and the rate-limit floors below.

**`POST /api/v1/auth/magic-link/request`** — ALWAYS 202, never leaks existence:

```typescript
interface MagicLinkRequest {
  email: string;          // RFC 5322; lowercased server-side; ≤320 chars
  captcha_token: string;  // Turnstile / mock; opaque ≤2048 chars
}
// Response 202 — constant envelope regardless of whether email is known:
interface MagicLinkRequestResponse { status: 'sent'; }
```
Negative-match (load-bearing per ADR 0093 Rev 4 Amendment I): server NEVER returns
whether the email matched a user, NEVER returns a dispatch id, NEVER returns the
token. Frontend always navigates to a static "check your inbox" page. Constant-work
discipline: the handler does the same work (captcha verify + a sham token-mint +
sham email-dispatch path) for unknown emails to close the timing channel — same
discipline as W#79 Decision 2 (sec-eng D).

**`POST /api/v1/auth/magic-link/verify`:**

```typescript
interface MagicLinkVerifyRequest {
  token: string;          // SignedOperation<MagicLinkPayload>; ≤512 chars
}
// Response 200 on successful first-consume:
interface MagicLinkVerifyResponse {
  tenant_slug: string;          // user-facing identifier
  tenant_display_name: string;
  // NEGATIVE-MATCH: server does NOT return tenant_id (Guid stays server-side)
  // session: established via Set-Cookie / session token per H-WSE-2 disposition
}
```

**RFC 7807 discriminators (read `body.title`, per fleet convention):**
`validation_failed`, `captcha_failed`, `magic_link_invalid` (signature/aud/malformed),
`magic_link_expired` (exp past), `rate_limited` (429 + Retry-After), `origin_invalid`
(403). NO `magic_link_already_used` discriminator — already-consumed maps to the same
disposition as invalid (closes the replay-disclosure channel, same as W#79 H9).
Discriminators defined ONCE as a `const` export shared frontend/backend with a
byte-equality contract test (W#79 §3.5 pattern).

### 3.3 Rate-limit floors (non-permissive minimum, per W#79 §3.7 pattern-onboarding-rate-limit)

| Endpoint | Per-IP | Per-(route+IP) | Per-entity | Burst | 429 Retry-After |
|---|---|---|---|---|---|
| `POST /api/v1/auth/magic-link/request` | 3 / min / IP | 3 / min / (route+IP) | 3 / min / email-keyed | 0 | window remainder |
| `POST /api/v1/auth/magic-link/verify` | 10 / min / IP | 10 / min / (route+IP) | n/a | 5 | window remainder |

Per-email key prevents an attacker spreading across IPs to flood a victim's inbox with
magic-link emails (same defense-in-depth as W#79's resend-verification per-email
floor). Email bucket key = SHA-256-first-16-bytes of lowercased email (W#79 .NET-arch
G2). Floors are minimums; implementation MAY tighten, MUST NOT loosen.

### 3.4 Frontend (PR-3) — pattern-009 pair, frontend half

Two pages in sunfish web: `/portal/login` (email entry + Turnstile/mock CAPTCHA
widget → POST request → navigate to static "check your inbox") and `/portal/verify`
(reads token from `window.location.hash`, POSTs verify, on 200 shows "logged in" +
redirects to the tenant home; on 4xx shows a generic "this link is invalid or has
expired — request a new one" — NO distinction between invalid/expired/already-used in
the UX, matching the server's non-leaky disposition). Each 4xx discriminator becomes a
typed-error class; `origin_invalid` surfaces as a transport-failure banner. Cycle-1
DRAFT binds against MSW mocks; Cycle-2 wires real Bridge after PR-2 merges (W#79 §3.9
cascade pattern).

### 3.5 Audit emission

Reuse the ADR-0049 audit substrate. New event types (or reuse existing security
events if present — pre-flight check): `Security.MagicLinkRequested` (fires on every
request, known-or-unknown, for fraud-pattern forensics — high-rate enumeration signal),
`Security.MagicLinkConsumed` (first-consume success), `Security.MagicLinkReplayAttempt`
(already-consumed or invalid verify — replay-attempt detection). For pre-tenant
emission destination, follow W#79's D5 disposition (emit to the system-tenant
partition for the request/replay events; the consumed event has a real tenant context).

---

## 4. Halt conditions (route to Admiral BEFORE build)

### H-WSE-1 — Magic-link substrate home + is magic-link an MVP feature at all?

**Context:** The onboarding-ladder scaffold explicitly listed "passwordless" as
**post-MVP** (Shell 2 §2.3 "2FA / passwordless (post-MVP)"; cross-shell forward-watch
#5 "Account recovery / password reset — not in MVP"). But the 2026-05-29 survey (P3)
and CIC's directive elevate the magic-link tenant portal to MVP ("tenant portal works
via magic-link" = a G-1 business-validation done-condition). **These conflict.** The
magic-link portal also intersects W#60 P4 (collaboration / tenant portal), which is
CIC-physical-gated to 2026-06-09.
**Options:** (a) Confirm magic-link IS MVP-critical and supersede the post-MVP framing;
(b) keep it post-MVP and only pipeline the Postmark adapter (PR-1) now; (c) ship a
narrow read-only tenant portal for MVP and defer passwordless-login to post-MVP.
**ONR recommendation:** This needs a CIC ruling (it's a scope-of-MVP call, not just an
architecture call). Recommend (a) IF the auth-session substrate (H-WSE-2) is ready;
otherwise the magic-link verify can't actually log anyone in. The Postmark adapter
(PR-1) is unconditionally worth building regardless — it's the real-vendor half of the
already-shipped mock substrate + unblocks real email for W#79 signup.

### H-WSE-2 — Auth-session establishment substrate (the gating dependency)

**Context:** W#79 deferred session establishment to "W#80 sub-cohort 2." Magic-link's
verify step MUST establish a tenant session — that's the entire feature. There is no
session-issuance primitive surveyed on main.
**Options:** (a) The session substrate ships first (separate workstream / ADR) and
magic-link consumes it; (b) magic-link verify only validates + redirects to a
password-login page (degraded — defeats the passwordless value prop); (c) magic-link
issues the session directly as part of this workstream (scope expansion — needs a
session-token design: cookie shape, TTL, refresh, CSRF, the works — likely its own
ADR + sec-eng dual-council).
**ONR recommendation:** (a) — sequence the session substrate ahead. If it doesn't
exist, magic-link is blocked on it, NOT buildable as specced. Admiral must confirm the
session substrate's existence/status before the magic-link PRs are dispatched. This is
the hardest blocker; surface to CIC.

### H-WSE-3 — Magic-link token TTL

15 min (ONR rec) vs 10 vs 5. sec-eng dual-council ruling; trades inbox-delivery-lag
tolerance against replay-window. No code blocker — a one-line `exp` config.

### H-WSE-4 — `EmailDispatchOptions.FromAddress` ownership (PR-1 vs already-shipped)

Per §2.4 — PR-1 ships it unless W#79 already did. Pre-flight grep resolves; Admiral
confirms placement (`foundation-integrations/Email/`).

### H-WSE-5 — Tenant-portal route prefix + project home

W#79 used `/api/v1/auth/*` on `Sunfish.Bridge.Onboarding`. Magic-link is auth-family;
recommend `/api/v1/auth/magic-link/*` on the same project (consistency; reuse the
bootstrap branch wiring). Confirm — or carve a `Sunfish.Bridge.Portal` project if the
tenant-portal surface will grow (read-only statements, etc., per W#60 P4).

### H-WSE-6 — shipyard#128 onboarding-ladder email/CAPTCHA decisions are STALE

**Context:** The survey (P3 + parked-items) says shipyard#128's email-provider +
CAPTCHA decisions "feed P3 (WS-E)" + should be "resolved before dispatching P3." **They
are already resolved** — CIC ratified Postmark + Turnstile + mock-first on
2026-05-25T18:08Z (`admiral-ruling-2026-05-25T18-10Z`), which `supersedes-deferral`
the onboarding-ladder ruling's Decisions 4+5. So shipyard#128's 2 CIC-routed decisions
do NOT need a fresh CIC ruling for WS-E — they're settled.
**ONR recommendation:** shipyard#128 needs only a docs-update reflecting the
2026-05-25 ratification (or close), NOT a CIC decision. Do not block WS-E dispatch on
#128. Admiral to confirm + route the #128 docs-close to QM/Engineer.

### H-WSE-7 — Formally supersede W#20 Phase 4-9 (email path) — see §1

Mark `property-messaging-substrate-stage06-phases-4-9-addendum.md` SUPERSEDED-FOR-EMAIL
by ADR 0096 so no future engineer resurrects the heavyweight `IOutboundMessageGateway`
+ `providers-postmark` (W#20 variant) path for transactional email. The bidirectional
substrate stays a valid post-MVP workstream for inbound/threaded comms + SMS.

---

## 5. PR decomposition + sequencing

| PR | Title | Layer | Owner | Depends on | Council | pattern-009 |
|---|---|---|---|---|---|---|
| **PR-1** | `providers-postmark` real email adapter | shipyard | Engineer | ADR 0096 on main (✓) + Postmark sandbox account | sec-eng + .NET-arch MANDATORY | no |
| **PR-2** | Magic-link Bridge endpoints (request + verify) | signal-bridge | Engineer | W#79 PR 0+1 on main + H-WSE-1 + H-WSE-2 ruled | sec-eng + .NET-arch + frontend-arch MANDATORY | YES (Bridge half) |
| **PR-3** | Magic-link tenant portal frontend (`/portal/login`, `/portal/verify`) | sunfish | FED | PR-2 (pattern-009 pair; pair-merge cascade) | sec-eng (Cycle-1 AMBER / Cycle-2 GREEN) + frontend-arch MANDATORY | YES (frontend half) |
| **PR-4** | e2e + cross-stack contract tests | sunfish / cross | FED/test-eng | PR-1 + PR-2 + PR-3 | test-eng MANDATORY (coverage-model gate) | no |

**Sequence:** PR-1 can go FIRST and independently (unblocks real email for W#79 too;
no magic-link dependency). PR-2 → PR-3 are the pattern-009 pair, gated on W#79 landing +
H-WSE-1/H-WSE-2 rulings. PR-4 closes the loop. The Bridge-then-frontend ordering +
per-step test-gating follows the W#79 §3.9 pair-merge cascade verbatim (PR-2 ships
scaffold-returning-501 → FED Cycle-1 DRAFT against MSW → PR-2 handler bodies merge →
FED Cycle-2 wires real Bridge → SPOT-CHECKs at each cycle).

**SPOT-CHECK SLA:** PR-2 (pattern-009 + new routes + passwordless auth) triggers
MANDATORY sec-eng SPOT-CHECK within 30 min of the Ready-flip beacon per fleet-conventions
§SPOT-CHECK dispatch SLA; QM daemon backstops within 1h.

---

## 6. Stage-05 adversarial brief (ADR 0093 Rev 4) + pattern claims

### 6.1 Adversarial brief (the decisions a council gates on)

1. **Magic-link as a bearer token.** Worst case: token leaks (email forwarded,
   screenshot, mail-server logging, browser history via query string) → attacker logs
   in as the tenant user. Mitigation: 128-bit `jti` + Ed25519 signature + 15-min TTL +
   single-use atomic consumption + token-in-URL-fragment-only (never query string,
   never server access logs) + HTTPS-only. Residual: email-account compromise =
   game over, but that is true of every passwordless + every password-reset flow.
2. **Enumeration via the request endpoint.** Worst case: attacker probes
   `magic-link/request` to learn which emails are registered tenant users. Mitigation:
   ALWAYS-202 constant envelope + constant-work discipline (sham mint + sham dispatch
   for unknown emails to close timing) + per-email rate-limit. Same posture as W#79
   signup always-202.
3. **Replay of a consumed/expired token.** Worst case: captured token replayed for a
   second login. Mitigation: atomic `TryConsumeAsync` single-use gate + 15-min TTL;
   already-consumed/expired/invalid all map to a byte-identical non-leaky disposition
   (no `magic_link_already_used` discriminator). Audit emits on every verify attempt
   for replay-attempt forensics.
4. **Session fixation / CSRF at the verify→session seam (H-WSE-2).** Worst case: the
   session issued at verify is fixable or CSRF-vulnerable. Mitigation: deferred to the
   session substrate's own sec-eng review (H-WSE-2); magic-link's job ends at "token
   atomically consumed for user X in tenant Y" — the session primitive owns
   cookie/CSRF/refresh hardening.
5. **Inbox flooding of a victim.** Worst case: attacker spams `magic-link/request`
   with a victim's email. Mitigation: per-email rate-limit floor (3/min/email) +
   per-IP. Same defense as W#79 resend-verification.
6. **Mock-in-production for the magic-link email.** Worst case: prod deploys with
   MockEmailProvider (no real Postmark key) → magic-link emails go to /dev/null →
   tenants can never log in. Mitigation: the ADR-0096 `MockProviderProductionGuardAssertion`
   fails closed at startup in Production unless `POSTMARK_API_KEY` present or explicit
   `SUNFISH_ALLOW_MOCK_PROVIDERS=true` opt-out. PR-1's adapter is what makes the real
   swap possible.
7. **Postmark API-key leak (PR-1).** Worst case: the server token appears in logs /
   error responses / telemetry. Mitigation: §2.2 floors F1+F2 (never logged, never
   echoed in `ErrorDetail`, generic auth-failure mapping). sec-eng SPOT-CHECK verifies
   with a log-capture test.
8. **Token-in-URL-fragment correctly never reaching the server on the GET.** Worst
   case: a future maintainer "simplifies" the verify flow to a `GET /portal/verify?token=...`
   → token lands in access logs + Referer headers. Mitigation: the verify is a POST
   reading the fragment client-side; document the invariant; frontend-arch SPOT-CHECK
   on PR-3 verifies no token in any GET/query-string path.

### 6.2 Pattern claims

- **`pattern-tier2-mock-first-substrate`** — PR-1 is the first REAL-adapter instance
  (W#79 claimed the mock half as instance 1). Per the W#79 §6.2 promotion criteria,
  this pattern promotes to STANDING when the mock side (W#79) + Postmark real (PR-1) +
  Turnstile real (future) all ship without regression. PR-1 is a load-bearing instance
  toward that promotion. Adversarial framework per W#79 §6.2 Pattern 1 (Threats 1-6:
  typo'd env-var, mock log leak, dev-inbox-in-prod, missing marker constraint,
  factory-registration escape, decorator escape).
- **`pattern-onboarding-rate-limit`** — PR-2 is a candidate 2nd-instance (the non-
  permissive minimum-floor + Retry-After + per-entity-key shape on a new pre-auth
  public endpoint family). Promotes per W#79 §6.2 Pattern 3 criteria.
- **`pattern-009` (Bridge endpoint + frontend rebind pair)** — PR-2/PR-3 are a formal
  pattern-009 instance; SPOT-CHECK mandatory per fleet-conventions.
- **`pattern-bootstrap-context-consumption`** — PR-2 is a candidate consumer instance
  (magic-link endpoints are pre-tenant, on `MapBootstrapEndpoints`, `.AllowAnonymous()`,
  no post-tenant interface injection until the atomic-consume → session seam).
- **NEW candidate: `pattern-single-use-token-redemption`** — the atomic
  `TryConsume<X>Async` gate (read row → assert flag → atomically transition → return
  bool; already-consumed → byte-identical idempotent disposition) now appears in W#79
  (verify-email) + magic-link (this hand-off) + foreseeably W#82 invitations. Worth
  flagging as a first-instance-already-2nd candidate for Admiral to formalize.

---

## 7. Open questions (research could not resolve; need inputs to unblock)

1. **Auth-session substrate status (H-WSE-2)** — does a session-issuance primitive
   exist on main or in flight? ONR could not find one (W#79 explicitly deferred it).
   This is the hardest gating unknown; needs Engineer/Admiral confirmation before the
   magic-link PRs can be specced as buildable end-to-end.
2. **Is magic-link MVP or post-MVP (H-WSE-1)** — the survey + directive say MVP; the
   onboarding-ladder scaffold says post-MVP. CIC scope call.
3. **W#79 landing ETA** — the magic-link PRs gate on `Sunfish.Bridge.Onboarding` +
   `IBootstrapTenantRegistry` reaching main (currently worktree-only). Sequencing input
   from Engineer.

---

## 8. Sources cited

**Primary (Sunfish authoritative — code + Accepted ADRs):**
1. ADR 0096 Tier-2 Vendor-Provider Substrate (Accepted 2026-05-25T21:45Z) +
   live source `packages/foundation-integrations/Email/{IEmailProvider,MockEmailProvider,EmailMessage,EmailDispatchResult}.cs`,
   `DependencyInjection/{VendorProviderServiceCollectionExtensions,MockProviderProductionGuardAssertion,MockInProductionException}.cs`,
   `IMockVendorProvider.cs`. Retrieved 2026-05-29.
2. W#79 Stage-05 hand-off `shipyard/icm/05_implementation-plan/cohort-w79-hand-off.md`
   (§3 wire contracts, §3.7 rate-limit floors, §6.1 Decision 3 atomic redemption,
   §6.2 pattern claims, §8 halt conditions). Retrieved 2026-05-29.
3. ADR 0093 Rev 4 Stage-05 Adversarial Review Protocol. Retrieved 2026-05-29.
4. ADR 0095 Bootstrap Context. Retrieved 2026-05-29.

**Primary (ONR's own prior deliverables — superseded for email path):**
5. `onr-wse-handoff-prereq-2026-05-17.md` (OQ-WSE-1..12 — for the ADR-0052 path;
   superseded for email by ADR 0096).
6. `property-messaging-substrate-stage06-phases-4-9-addendum.md` (W#20; superseded for
   email — see §1, H-WSE-7).

**Secondary (rulings + context):**
7. CIC mock-first ruling `admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`
   (Postmark + Turnstile + mock-first canonical discipline).
8. Onboarding-ladder ruling `admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md`
   + scaffold `onboarding-ladder-sub-cohorts-scaffold.md` (passwordless = post-MVP
   framing that H-WSE-1 reconciles).
9. MVP feature-priority survey `coordination/inbox/research-mvp-feature-priority-2026-05-29T0205Z.md` (P3 = WS-E).

**Tertiary (external — standards):**
10. OWASP ASVS Level 1 — short-lived bearer/verification token guidance (TTL floors).
11. RFC 5322 (email mailbox syntax); RFC 7807 (ProblemDetails — fleet `title`-discriminator convention).

---

*ONR Stage-05 hand-off delivered 2026-05-29. Pipelining deliverable — build sequenced
AFTER Cohort-5. Pending Admiral consumption + halt-condition rulings (esp. H-WSE-1 MVP
scope + H-WSE-2 session substrate) + dual/triple-council Stage-05 review per ADR 0093
Rev 4. Standing by for clarification.*

— ONR, 2026-05-29T02:35Z
