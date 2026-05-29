# ONR research — ADR 0096 Tier-2 Vendor-Provider Substrate scaffold (onboarding-ladder D2 unblocked)

**Authored by:** ONR
**Requester:** Admiral (per `admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md` — Option C unblocked; CIC ratified Decisions 4 + 5 + mock-first directive)
**Authored at:** 2026-05-25
**Type:** Research scaffold for new ADR 0096 (Tier-2 Vendor-Provider Substrate)
**Status:** Draft for Admiral consumption — Admiral authors ADR 0096 Rev 1 text from this scaffold

---

## Scope of investigation

- **In scope:** Define the Tier-2 Vendor-Provider substrate that the mock-first
  directive codifies; audit the status-quo (what exists in
  `Sunfish.Foundation.Integrations` today); enumerate scope options for ADR 0096
  itself (email-only vs generalized substrate); recommend a recommended option;
  specify the canonical mock-first pattern for email + CAPTCHA; surface the new
  surfaces ADR 0096 must introduce; surface halt conditions Admiral must resolve
  before ADR 0096 Rev 1 authoring.
- **Out of scope:** Writing ADR 0096 itself (Admiral territory); per-PR
  Stage-05 hand-off authoring (W79; downstream); rate-limit substrate (Decision
  6 build, separate); Bootstrap Context interface (ADR 0095; consuming substrate
  but distinct); the email substrate's payload semantics (ADR 0052 already
  defines bidirectional thread messaging; ADR 0096 narrows scope to
  *transactional onboarding email*, not thread messaging).
- **Authoritative sources consulted:** ADR 0013 (Foundation.Integrations +
  provider-neutrality policy); ADR 0052 (bidirectional messaging substrate);
  ADR 0091 R2 (ITenantContext divergence resolution); shipyard `packages/foundation-integrations/`
  (full audit — `IProviderRegistry`, `ProviderDescriptor`, `ProviderCategory`,
  `Captcha/`, `Messaging/`, `Payments/`, `Signatures/`); shipyard
  `packages/providers-recaptcha/` (canonical adapter reference); slotting
  recommendation `shipyard/icm/01_discovery/research/onr-slotting-architecture-general-upf.md`
  §5 (three-tier `category-provider` semantics); Admiral ruling
  `admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`
  (CIC ratification + mock-first directive); Admiral ruling
  `admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md`
  (Decisions 4 + 5 context). Prior-art landmarks: ASP.NET Core
  `IServiceProvider` keyed-service pattern (.NET 8+); Microsoft.Extensions.DependencyInjection
  conditional-registration patterns; Postmark `/email` API surface; Cloudflare
  Turnstile `/siteverify` endpoint.
- **Success criteria:** Admiral can author ADR 0096 Rev 1 from this scaffold
  without re-discovering the status-quo audit, the mock-first pattern shape, or
  the scope decision (A-vs-B). Every load-bearing claim cites a primary source.
  Each halt condition names the actor who must resolve it.

---

## TL;DR

- **Problem.** CIC's "mock-first, vendor-swap-later" directive applies to every
  Tier-2 `category-provider` surface (email, CAPTCHA, eventually storage,
  payments, identity per slotting recommendation §5). The Foundation.Integrations
  substrate (ADR 0013) already establishes the `provider-neutral
  contract + providers-{vendor} adapter` shape — but the **mock-first discipline
  is not yet codified** as a substrate-tier invariant, and the email substrate
  (`IEmailProvider` for transactional onboarding emails) does not yet exist.
- **What ADR 0096 must define.** (a) The canonical mock-first discipline — every
  Tier-2 vendor surface ships an in-memory `MockXProvider` alongside the contract
  in `foundation-integrations`, real-vendor adapters ship as separate `providers-{vendor}`
  packages, registration is config-gated (env var → real; default → mock);
  (b) the new `IEmailProvider` substrate for transactional onboarding email
  (Postmark adapter follows in `providers-postmark`); (c) the
  `ITurnstileCaptchaVerifier` adapter slotting into the existing `ICaptchaVerifier`
  substrate (`providers-turnstile`); (d) the missing `ProviderCategory.Captcha`
  enum value (currently only `Messaging` exists, which is bidirectional
  thread-messaging per ADR 0052).
- **Recommended scope option.** **Option B — Generalized "Tier-2 Vendor-Provider
  Substrate" covering email + CAPTCHA + future vendors in one canonical pattern.**
  The mock-first directive is substrate-tier discipline, not per-vendor policy;
  capturing it once and applying it to every Tier-2 surface mirrors how ADR 0013
  captured provider-neutrality once and applied it to every category. Option A
  (email-only ADR 0096; CAPTCHA-only ADR 0097) duplicates the discipline-
  authoring across two ADRs with identical content sections.
- **Halt conditions for Admiral.** (1) Mock-package layout — `foundation-integrations-mocks`
  separate package vs alongside the contracts in `foundation-integrations`?
  (2) `IEmailProvider` contract home — `foundation-integrations/Email/` (new
  subnamespace) vs reuse `IMessagingGateway` substrate? (3) Council review
  scope — pattern is a substrate-tier discipline ADR; ONR recommends MANDATORY
  .NET-architect council review on the ADR text + Step 1 (substrate-package) PR,
  sec-eng SPOT-CHECK on Step 2 (transactional-email adapter, since the adapter
  ships secrets handling). (4) Whether to widen `ProviderCategory` enum
  immediately (add `Captcha`, `TransactionalEmail`) or defer to a follow-up
  `foundation-catalog` PR. (5) Whether the existing `ICaptchaVerifier` (W#28
  Phase 3.1) is renamed or kept as-is — current naming is provider-neutral but
  the substrate could promote `ICaptchaProvider` to align with the new
  `IXProvider` convention this ADR introduces.

---

## 1. Problem statement

### 1.1 What is the Tier-2 Vendor-Provider Substrate?

Per the slotting-architecture recommendation (shipyard `onr-slotting-architecture-general-upf.md`
§5; now on main), Sunfish's runtime composition surface partitions into three tiers:

| Tier | Swap shape | Examples | Mock-then-swap discipline? |
|---|---|---|---|
| **Tier 1 domain-block** | Concrete DI; no swap | tenant, property, lease, invoice, audit | No — always concrete |
| **Tier 2 category-provider** | Bounded vendor swap at deploy time (config-only) | email, CAPTCHA, storage, payments, identity | **YES — this ADR codifies it** |
| **Tier 3 capability-plugin** | Runtime swap (per-request, per-tenant) | TTS, STT, image, LLM (flight-deck) | Different shape; out of scope |

ADR 0013 established the **provider-neutrality** half of Tier-2 — domain modules
reference contracts, never vendor SDKs; adapters live in `providers-{vendor}`
packages. CIC's 2026-05-25T18:08Z directive adds the **mock-first discipline**:
every Tier-2 surface ships a `MockXProvider` reference implementation in the
contracts package, and the default DI registration binds the mock. Real-vendor
adapters register **conditionally** when their environment variables / secrets
are present, overriding the mock at composition root.

This combination — provider-neutral contracts + canonical in-memory mock + config-
gated real-vendor override — is the **canonical Tier-2 implementation discipline**
the Admiral ruling §"Substrate alignment" names. ADR 0096 codifies it.

### 1.2 Why this needs an ADR (vs an inline implementation note)

Four reasons make this substrate-tier and worth Admiral-authority ratification:

1. **The discipline applies across categories.** Once codified, every future
   Tier-2 substrate (Storage in W#XX, IdentityProvider in OIDC follow-up,
   PaymentGateway upgrade, etc.) consumes the same pattern. Without an ADR,
   each category re-discovers the convention; with one, each just references it.
   The same logic that justified ADR 0013 covering all categories at once
   applies here.

2. **The config-gated registration shape is non-obvious.** Default-mock + env-
   var-overridden real provider requires a specific DI shape — see §3.3.
   Without an ADR, three Engineer dispatches (W79 email, W79 CAPTCHA, W80
   verification-email) each invent their own variant. With an ADR, one
   canonical helper (`AddSunfishVendorProvider<TContract, TMock>()` + a
   conditional `Use<TReal>(env: ENV_VAR_NAME)` extension) ships once.

3. **The mock-first pattern has security-critical posture.** A misconfigured
   production deployment that **silently falls back to the mock** because the
   env var was not set is a critical incident — production signups would
   bypass real CAPTCHA, real email verification would never send. The ADR
   must specify **startup-time assertions** that production environments
   either bind real providers OR explicitly opt into mock binding (e.g.,
   `SUNFISH_ALLOW_MOCK_PROVIDERS=true` in dev/staging). Without this guard,
   the convention is foot-gunny.

4. **The `IEmailProvider` substrate is genuinely new surface area.** The
   existing `IMessagingGateway` (ADR 0052) is bidirectional thread messaging
   — inbound webhook ingestion, threaded conversations, per-tenant per-sender
   isolation. Transactional onboarding email (welcome, verification, invitation)
   is unidirectional fire-and-forget with idempotency-keyed retries. Folding
   the two would conflate scopes; separating them is the ADR 0091-style
   conflated-interface fix.

### 1.3 What ADR 0096 must NOT define

To stay scoped, ADR 0096 must explicitly **not** define:

- The Postmark adapter's wire format (Engineer territory in `providers-postmark`
  package; ADR cites the contract `IEmailProvider` only).
- The Turnstile adapter's verify-call payload (Engineer territory in
  `providers-turnstile` package).
- The signup endpoint's email-send invocation points (W80 Surface-A+B
  Stage-05 hand-off).
- Template authoring / i18n for email bodies (out of scope; future ADR or
  Stage-05 implementation detail).
- Rate-limiting at the email level (cross-cuts with Decision 6 AspNetCore
  RateLimiter substrate; consume the rate-limit substrate, do not define new
  policy here).
- The Bootstrap Context interaction with `IEmailProvider` / `ICaptchaVerifier`
  (ADR 0095 defines Bootstrap; ADR 0096 just states "consumed in bootstrap
  scope" as a non-binding note).

These all consume the Tier-2 substrate; none of them belong in the substrate ADR.

---

## 2. Status-quo audit

### 2.1 Foundation.Integrations surface that exists today

Findings from a full scan of `shipyard/packages/foundation-integrations/`:

| Symbol / path | Existing? | Role | Reusable as-is? |
|---|---|---|---|
| `Sunfish.Foundation.Integrations.IProviderRegistry` | yes (ADR 0013) | Adapter enumeration / lookup by category + key | **Yes** — ADR 0096 consumes it |
| `Sunfish.Foundation.Integrations.ProviderDescriptor` | yes | Metadata record (key, category, name, version, capabilities) | **Yes** — every adapter registers a descriptor |
| `Sunfish.Foundation.Integrations.InMemoryProviderRegistry` | yes | Default in-memory registry | **Yes** |
| `Sunfish.Foundation.Catalog.Bundles.ProviderCategory` | yes (ADR 0007) | Enum: `Billing`, `Payments`, `BankingFeed`, `FeatureFlags`, `ChannelManager`, `Messaging`, `Storage`, `IdentityProvider`, `Other` | **Partial — missing `Captcha` + `TransactionalEmail`** (see Halt 4) |
| `Sunfish.Foundation.Integrations.CredentialsReference` | yes | Opaque vault reference (never plaintext secrets) | **Yes** — Postmark + Turnstile adapters use it |
| `Sunfish.Foundation.Integrations.ProviderHealthStatus` | yes | Enum: `Unknown`, `Healthy`, `Degraded`, `Unhealthy` | **Yes** — every adapter reports |
| `Sunfish.Foundation.Integrations.ServiceCollectionExtensions.AddSunfishIntegrations` | yes | Registers in-memory registry + dispatcher + cursor store | **Yes — but needs extension** for the new `AddSunfishVendorProvider<,>` helper |
| `Sunfish.Foundation.Integrations.Captcha.ICaptchaVerifier` | yes (W#28 Phase 3) | CAPTCHA verify contract — `VerifyAsync(token, clientIp, ct) → CaptchaVerifyResult` | **Yes** — Turnstile slots in via this contract |
| `Sunfish.Foundation.Integrations.Captcha.InMemoryCaptchaVerifier` | yes | Test fixture; pre-seeded tokens; in-memory journal | **Yes — promote to canonical mock per §3.2.2** |
| `Sunfish.Foundation.Integrations.Captcha.ICaptchaProviderConfig` | yes | Config contract: SiteKey, SecretKey, MinPassingScore | **Yes** — Turnstile config implements it |
| `Sunfish.Providers.Recaptcha.RecaptchaV3CaptchaVerifier` | yes (W#28 Phase 3.1) | Canonical adapter reference (HttpClient-based; no vendor SDK) | **Reference pattern** for `TurnstileCaptchaVerifier` |
| `Sunfish.Foundation.Integrations.Messaging.IMessagingGateway` | yes (ADR 0052) | Bidirectional thread messaging — outbound+inbound, per-tenant config, abuse scoring | **NOT for transactional onboarding email** — see §1.2 #4 |
| `Sunfish.Foundation.Integrations.Messaging.OutboundMessageRequest` | yes | Thread-scoped payload — has `ThreadId`, `Participant[]`, `MessageVisibility`, `ThreadToken` | **NOT for onboarding email** — onboarding email is pre-tenant + no thread |
| `Sunfish.Foundation.Integrations.Payments.IPaymentGateway` | yes (ADR 0051) | Payment substrate | Out of scope for ADR 0096; demonstrates the Tier-2 shape ADR 0096 generalizes |
| `Sunfish.Foundation.Integrations.Signatures.ISignatureCapture` | yes (ADR 0054) | Signature substrate stub | Out of scope; same Tier-2 shape |

### 2.2 The four hard gaps

1. **No `IEmailProvider` interface exists** for unidirectional transactional
   onboarding email. The closest analog (`IMessagingGateway`) is bidirectional
   thread-messaging and structurally inappropriate (§1.2 #4 + §2.1 row).

2. **No canonical `MockXProvider` discipline is codified.** `InMemoryCaptchaVerifier`
   exists and is exactly the shape the mock-first directive wants — but its
   xmldoc says "**NOT for production**" and its callers explicitly opt-in via
   test-fixture pathways. ADR 0096 needs to (a) bless this pattern as
   canonical, (b) make the mock the **default DI binding** in dev/test (rather
   than a manually-wired test-only fixture), (c) require startup assertions
   that production hosts have either bound real adapters OR explicitly opted
   into mock binding.

3. **No config-gated `Use<TReal>(env: ENV_VAR_NAME)` pattern exists.** Today's
   provider adapters are registered unconditionally by the host's composition
   root (signal-bridge `Program.cs` directly calls
   `services.AddSingleton<ICaptchaVerifier, RecaptchaV3CaptchaVerifier>()`
   when the host wants reCAPTCHA). The mock-first directive requires a
   uniform "default-mock; override conditionally on env var" pattern so a
   deployment swaps providers via config, not code.

4. **`ProviderCategory` enum is incomplete.** Missing `Captcha` (anti-bot
   protection) and `TransactionalEmail` (onboarding email — distinct from
   `Messaging` which is thread-messaging per ADR 0052). Adapters cannot
   register a `ProviderDescriptor` with the correct category until these are
   added (or until ADR 0096 ratifies extending the enum).

### 2.3 What the substrate already gets right (and ADR 0096 must preserve)

- **Provider-neutral contracts in `foundation-integrations`** (ADR 0013). Domain
  modules consume `ICaptchaVerifier`, never `Sunfish.Providers.Recaptcha.RecaptchaV3CaptchaVerifier`.
- **Adapters live in `providers-{vendor}` packages.** `providers-recaptcha`,
  `providers-mesh-headscale` already established. `providers-postmark` and
  `providers-turnstile` follow.
- **HttpClient-based, no vendor SDK.** `RecaptchaV3CaptchaVerifier` posts to
  the Google verify endpoint via `HttpClient`; no Google SDK reference. Postmark
  + Turnstile adapters follow the same pattern (Postmark has a `.NET` SDK
  available — ADR 0096 should specify whether to use it or stay HttpClient-only;
  ONR recommends HttpClient-only for the same reasons ADR 0013 §enforcement
  cites — supply-chain surface + lock-in posture).
- **`CredentialsReference` keeps secrets out of contracts.** No `ApiKey` field
  on contract surfaces; adapters resolve credentials via the host's
  secrets manager.
- **`ProviderDescriptor` per adapter.** Every adapter registers metadata; Bridge
  admin enumerates "what's wired."

---

## 3. Mock-first pattern specification

### 3.1 The shape of the canonical pattern

ADR 0096 codifies the following discipline for every Tier-2 vendor-provider
surface:

#### 3.1.1 Three artifacts per category

For each Tier-2 category (email, CAPTCHA, storage, etc.), the substrate ships:

1. **A provider-neutral contract** in `Sunfish.Foundation.Integrations.<Category>/`
   (e.g., `Sunfish.Foundation.Integrations.Email.IEmailProvider`).
2. **A canonical mock implementation** in the same package — `MockXProvider`
   (e.g., `Sunfish.Foundation.Integrations.Email.MockEmailProvider`). Ships
   in-tree alongside the contract because (a) the contract and mock are
   developed together, (b) the mock is intentionally consumed in dev/test
   production paths (not just unit tests).
3. **Zero or more real-vendor adapters** in separate `providers-{vendor}`
   packages — `providers-postmark` ships `PostmarkEmailProvider`,
   `providers-sendgrid` ships `SendGridEmailProvider` if a 2nd-instance
   real vendor emerges.

#### 3.1.2 The new DI helper

A new extension method on `IServiceCollection` in `Sunfish.Foundation.Integrations`:

```csharp
public static class VendorProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers the canonical Mock implementation for a Tier-2 vendor surface.
    /// Real-vendor adapters override this at composition root by calling
    /// services.AddSingleton<TContract, TRealAdapter>() AFTER this helper.
    /// </summary>
    /// <typeparam name="TContract">The vendor-neutral contract (e.g., IEmailProvider).</typeparam>
    /// <typeparam name="TMock">The mock implementation (e.g., MockEmailProvider).</typeparam>
    public static IServiceCollection AddSunfishVendorProvider<TContract, TMock>(
        this IServiceCollection services)
        where TContract : class
        where TMock : class, TContract;

    /// <summary>
    /// Conditionally registers a real adapter for a Tier-2 vendor surface.
    /// If <paramref name="environmentVariableName"/> is set to a non-empty value
    /// at composition time, swaps the registration to <typeparamref name="TRealAdapter"/>;
    /// otherwise leaves the prior (Mock) registration in place.
    /// </summary>
    public static IServiceCollection UseVendorProviderIfConfigured<TContract, TRealAdapter>(
        this IServiceCollection services,
        string environmentVariableName,
        Action<IServiceProvider, TRealAdapter>? configure = null)
        where TContract : class
        where TRealAdapter : class, TContract;
}
```

Composition root usage (shape-only; signal-bridge `Program.cs` adapts):

```csharp
services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
services.UseVendorProviderIfConfigured<IEmailProvider, PostmarkEmailProvider>("POSTMARK_API_KEY");

services.AddSunfishVendorProvider<ICaptchaVerifier, InMemoryCaptchaVerifier>();
services.UseVendorProviderIfConfigured<ICaptchaVerifier, TurnstileCaptchaVerifier>("TURNSTILE_SECRET_KEY");
```

#### 3.1.3 Startup safety assertion

A new `IHostedService` —
`Sunfish.Foundation.Integrations.MockProviderProductionGuardAssertion` — runs at
startup, enumerates all Tier-2 contracts the host has registered, and **fails
fast** if any registration in a production environment is still bound to a
`MockX` type. Production environments may explicitly opt out by setting
`SUNFISH_ALLOW_MOCK_PROVIDERS=true` (e.g., a load-test environment or a closed
demo deployment).

Detection mechanism: the mock types implement a marker interface
`Sunfish.Foundation.Integrations.IMockVendorProvider` (zero-member;
metadata-only). The assertion enumerates registered services and any
implementation type assignable to `IMockVendorProvider` triggers a check
against `IHostEnvironment.IsProduction()` + the opt-out env var.

Without this guard the convention is silent-foot-gunny — a typo in the env-var
name in a deployment manifest produces a working app that silently never sends
real email or never validates real CAPTCHA. The startup assertion is the
ADR 0091 R2-style "fail loudly at startup" pattern.

### 3.2 Email substrate — `IEmailProvider`

#### 3.2.1 Contract surface (sketch)

```csharp
namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Egress contract for unidirectional transactional onboarding email
/// (welcome, verification, invitation, password-reset). Distinct from
/// Sunfish.Foundation.Integrations.Messaging which covers bidirectional
/// thread messaging per ADR 0052.
/// </summary>
public interface IEmailProvider
{
    Task<EmailDispatchResult> SendAsync(
        EmailDispatchRequest request,
        CancellationToken ct);
}

public sealed record EmailDispatchRequest
{
    /// <summary>Stable substrate id; precedes provider's own message id; idempotency key.</summary>
    public required EmailDispatchId Id { get; init; }

    /// <summary>Optional tenant scope; null for pre-tenant signup window per ADR 0095.</summary>
    public TenantId? Tenant { get; init; }

    /// <summary>From-address; substrate validates RFC 5321 conformance.</summary>
    public required EmailAddress From { get; init; }

    /// <summary>Reply-To; optional.</summary>
    public EmailAddress? ReplyTo { get; init; }

    /// <summary>Single recipient — onboarding email is 1:1 not broadcast.</summary>
    public required EmailAddress To { get; init; }

    public required string Subject { get; init; }

    /// <summary>Plain-text body — every transactional message MUST have a text part.</summary>
    public required string TextBody { get; init; }

    /// <summary>Optional HTML body.</summary>
    public string? HtmlBody { get; init; }

    /// <summary>Provider-specific message stream / category for analytics + suppression management.</summary>
    public string? MessageStream { get; init; }

    /// <summary>Per-substrate correlation id; logs + audit-trail attribution.</summary>
    public required string CorrelationId { get; init; }
}

public sealed record EmailDispatchResult(
    EmailDispatchId Id,
    string? ProviderMessageId,
    EmailDispatchStatus Status,
    string? Error);

public enum EmailDispatchStatus
{
    Accepted = 0,        // queued for delivery
    Rejected = 1,        // recipient malformed / from-address unverified / etc.
    ProviderError = 2,   // upstream provider failure; retriable
    QuotaExceeded = 3,   // rate-limited by vendor
}
```

#### 3.2.2 Mock implementation — `MockEmailProvider`

Three observable behaviors the mock provides (per CIC directive):

| Behavior | Mechanism | Why |
|---|---|---|
| **In-memory store** | `IReadOnlyList<EmailDispatchRequest> SentMessages` property | Test fixtures assert "the welcome email was sent" without smtp. |
| **Console log** | `ILogger<MockEmailProvider>.LogInformation("[MockEmail] sent to {To}: {Subject}", ...)` on every send | Local dev visibility: developer sees signup → mock email logged in the terminal without checking memory. |
| **Optional dev inbox UI** | A Bridge admin route `/admin/dev/mock-inbox` reads `MockEmailProvider.SentMessages` and renders the list (only registered when the mock is bound + `IHostEnvironment.IsDevelopment()`) | "Dev inbox UI" CIC directive item; click an entry to see the raw body. |

Optional behavior knobs (for chaos / failure testing):

- `MockEmailProvider.SimulateFailureRate { get; set; }` — `0.0` (default) to
  `1.0`; non-zero values return `EmailDispatchStatus.ProviderError` randomly.
- `MockEmailProvider.SimulateQuotaExceededAfter { get; set; }` — N successful
  sends then start returning `EmailDispatchStatus.QuotaExceeded`.

Implements `Sunfish.Foundation.Integrations.IMockVendorProvider` (marker
interface for startup-assertion detection).

#### 3.2.3 Postmark adapter — `providers-postmark` (outline)

Per ADR 0013 + the recaptcha-v3 reference, the Postmark adapter:

- Lives in `shipyard/packages/providers-postmark/`.
- Package id: `Sunfish.Providers.Postmark`.
- HttpClient-based (POSTs to `https://api.postmarkapp.com/email`); no Postmark
  .NET SDK dependency (per ADR 0013 supply-chain posture).
- Implements `IEmailProvider`.
- Config record `PostmarkConfig : IEmailProviderConfig` — `ApiToken` (resolved
  from `CredentialsReference`), `DefaultMessageStream`, `BaseUrl` (overridable
  for tests + on-prem proxies).
- Registers a `ProviderDescriptor` with `Key=sunfish.providers.postmark`,
  `Category=ProviderCategory.TransactionalEmail` (new enum value per Halt 4),
  `Capabilities=["transactional", "templated"]`, `SupportedRegions=["*"]`.

Engineer authors the implementation in W79 Stage-05 follow-up; ADR 0096 just
specifies the contract + package layout.

### 3.3 CAPTCHA substrate — reuse + extend existing `ICaptchaVerifier`

#### 3.3.1 Reuse, don't rename

`Sunfish.Foundation.Integrations.Captcha.ICaptchaVerifier` already exists and
matches the canonical Tier-2 shape (W#28 Phase 3 substrate; ADR 0059). The
`InMemoryCaptchaVerifier` already implements exactly the mock pattern this
ADR codifies — pre-seeded tokens, in-memory journal, configurable minimum
passing score.

ADR 0096 reuses both as-is with three changes:

1. **`InMemoryCaptchaVerifier` implements `IMockVendorProvider`** marker
   interface (one-line change; enables startup assertion detection).
2. **Default DI registration changes** — `AddSunfishIntegrations` currently
   does NOT register `ICaptchaVerifier` at all (callers wire reCAPTCHA at
   composition root). After ADR 0096, `AddSunfishIntegrations` calls
   `AddSunfishVendorProvider<ICaptchaVerifier, InMemoryCaptchaVerifier>()` by
   default; real adapters override.
3. **`InMemoryCaptchaVerifier` gains "always-pass" + "always-fail" convenience
   constructors** (per CIC directive — mock should support multiple modes).
   Today's API requires pre-seeding each token; the new convenience constructors:
   - `InMemoryCaptchaVerifier.AlwaysPass()` — every token verifies with score 1.0.
   - `InMemoryCaptchaVerifier.AlwaysFail()` — every token returns Passed=false, score 0.0.
   - `InMemoryCaptchaVerifier.WithMagicToken("mock-pass")` — only the magic
     token verifies; all others fail. (Default for the substrate's default DI
     registration; provides happy-path-by-default while still blocking
     untrusted clients during dev fuzzing.)

#### 3.3.2 Turnstile adapter — `providers-turnstile` (outline)

Per the recaptcha-v3 reference (which is literally the canonical template):

- Lives in `shipyard/packages/providers-turnstile/`.
- Package id: `Sunfish.Providers.Turnstile`.
- HttpClient-based (POSTs to `https://challenges.cloudflare.com/turnstile/v0/siteverify`).
- Implements `ICaptchaVerifier`.
- Config record `TurnstileConfig : ICaptchaProviderConfig` — `SiteKey`, `SecretKey`,
  `MinPassingScore` (Turnstile does not return a score; treats success as 1.0
  to fit the contract — record this in xmldoc).
- Registers a `ProviderDescriptor` with `Key=sunfish.providers.turnstile`,
  `Category=ProviderCategory.Captcha` (new enum value per Halt 4),
  `Capabilities=["bot-protection", "privacy-respecting"]`.

Engineer authors the implementation in W79 Stage-05 follow-up; ADR 0096
specifies the contract + package layout.

### 3.4 Test / dev / prod environment matrix

| Environment | Email binding | CAPTCHA binding | Notes |
|---|---|---|---|
| Unit tests | `MockEmailProvider` (manually wired into the test's `IServiceCollection`) | `InMemoryCaptchaVerifier` (pre-seeded) | Tests construct their own instances; no env vars needed. |
| Integration tests | `MockEmailProvider` (default) | `InMemoryCaptchaVerifier.AlwaysPass()` | Default registrations; `SUNFISH_ALLOW_MOCK_PROVIDERS=true` set in CI. |
| Local dev | `MockEmailProvider` (default; console + dev inbox UI) | `InMemoryCaptchaVerifier.WithMagicToken("mock-pass")` (default) | Default registrations; opt-out via local `.env` setting `POSTMARK_API_KEY` / `TURNSTILE_SECRET_KEY` for real-vendor smoke testing. |
| Staging | `PostmarkEmailProvider` OR `MockEmailProvider` (if `SUNFISH_ALLOW_MOCK_PROVIDERS=true`) | `TurnstileCaptchaVerifier` OR `InMemoryCaptchaVerifier` (if opt-in) | Real env vars set in deployment manifest; opt-in for staging-without-real-vendor scenarios. |
| Production | `PostmarkEmailProvider` (required; assertion fails fast otherwise) | `TurnstileCaptchaVerifier` (required; assertion fails fast otherwise) | `SUNFISH_ALLOW_MOCK_PROVIDERS` must be unset/false; absence of real env vars triggers startup failure. |

The matrix encodes a defensible default: dev/test = mocks for ergonomics;
prod = real-or-fail-fast.

---

## 4. Options analysis — ADR 0096 scope

Five candidate shapes for the ADR 0096 boundary, ordered by increasing
generalization:

### Option A — ADR 0096 email-only; ADR 0097 CAPTCHA-only

Two ADRs, one per category. Each carries:
- Mock-first discipline (duplicated).
- One contract surface.
- One canonical mock.
- One real adapter outline.

- **Pro:** Smaller per-ADR text; reviewable as discrete units.
- **Pro:** Loosely coupled; CAPTCHA ADR can land after email if scheduling
  diverges.
- **Con:** Duplicates the mock-first discipline section across both ADRs.
  Future Tier-2 ADRs (storage, payments-upgrade, identity) re-duplicate.
- **Con:** Misses the substrate-tier framing CIC's directive named — the
  discipline IS the substrate; vendor surfaces are instances.
- **Con:** Higher total authoring + council-review cost (2× ADR cycles).
- **Verdict:** Rejected — duplicates discipline across two ADRs.

### Option B — Generalized "Tier-2 Vendor-Provider Substrate" (email + CAPTCHA + future) [RECOMMENDED]

One ADR codifies:
- The mock-first discipline (canonical Tier-2 pattern; new DI helpers; startup
  assertion).
- The two new concrete substrates (email — new contract; CAPTCHA —
  existing contract, blessed + extended).
- The two real-vendor adapter outlines (Postmark; Turnstile).
- Forward-application to future Tier-2 categories (storage, payments-upgrade,
  identity) — no commitments, just the pattern reference.

- **Pro:** Captures the directive once; future Tier-2 ADRs (W#XX-storage,
  W#YY-identity) reference ADR 0096 instead of re-deriving.
- **Pro:** Mirrors how ADR 0013 captured provider-neutrality once for all
  categories.
- **Pro:** One ADR cycle; lower total cost.
- **Pro:** Engineer reads one canonical ADR + the categories' contract surfaces;
  no duplication for the team to keep consistent.
- **Pro:** Aligns with CIC's "mocked services should be set up for now and
  eventually replaced with real subscriptions" — the directive is universal,
  not vendor-specific.
- **Con:** Longer ADR; ~2x the text of Option A's email-only ADR (still
  comparable to ADR 0091 R2 + ADR 0094 substrate-tier ADRs).
- **Con:** Slightly higher council-review surface; reviewers parse two
  contracts at once.
- **Verdict:** **Recommended.** The substrate is the discipline; codifying it
  once and applying it to two concrete substrates is the cleanest long-term
  shape per `feedback_prefer_cleanest_long_term_option`.

### Option C — Discipline-only ADR 0096; separate per-category ADRs 0097 (email) + 0098 (CAPTCHA)

Three ADRs:
- ADR 0096 = mock-first discipline + DI helpers + startup assertion only.
- ADR 0097 = `IEmailProvider` contract + Postmark adapter outline.
- ADR 0098 = `ICaptchaVerifier` reuse-and-extend + Turnstile adapter outline.

- **Pro:** Maximum separation; each ADR has a single tight scope.
- **Pro:** Future Tier-2 ADRs add their own per-category ADR, not amendments.
- **Con:** Three ADR cycles for one CIC directive.
- **Con:** ADR 0096 (discipline-only) is hard to read in isolation — the
  pattern is abstract without concrete instances.
- **Con:** Cited example: ADR 0013 ships discipline + concrete examples
  (CredentialsReference, ProviderDescriptor) in the same ADR; this proposal
  goes the other way for no clear reason.
- **Verdict:** Rejected — fragmentation cost > separation-of-concerns
  benefit.

### Option D — Fold into ADR 0013 amendment

Amend ADR 0013 (Foundation.Integrations) with the mock-first discipline +
new helpers + email substrate addition.

- **Pro:** Substrate cohesion; ADR 0013 is the canonical Foundation.Integrations
  ADR.
- **Con:** ADR 0013 is already 2026-04-19 Accepted; amendments would
  substantially restructure §Decision; risks breaking forward-watch.
- **Con:** Per ADR 0069, amendments are for narrow drift; mock-first is a
  substantive new discipline.
- **Verdict:** Rejected — amendments aren't for new disciplines.

### Option E — Drop ADR; encode as cerebrum + per-PR Stage-05 spec

No ADR; the discipline lives in cerebrum + each Engineer Stage-05 spec
explicitly cites it.

- **Pro:** Lowest authoring cost.
- **Con:** No canonical reference; subsequent Tier-2 categories re-discover.
- **Con:** Substrate-tier discipline without ADR-tier visibility violates
  ADR 0069's authoring discipline (substrate decisions are ADR-tier).
- **Verdict:** Rejected — substrate-tier discipline merits substrate-tier ADR.

### Decision matrix

| Criterion | A (2 ADRs) | B (1 generalized) | C (3 ADRs) | D (0013 amend) | E (no ADR) |
|---|---|---|---|---|---|
| Captures discipline once | partly | **yes** | partly | yes | no |
| Future Tier-2 ADRs reuse | partly | **yes** | yes | yes | no |
| Single ADR cycle | no | **yes** | no | partly | yes |
| Reviewable surface size | small | medium | small | large | n/a |
| Aligns with ADR 0013 precedent | partly | **yes** | partly | conflicts | conflicts |
| Aligns with CIC directive's framing | partly | **yes** | partly | partly | conflicts |
| Total authoring + council cost | high | **medium** | high | medium | low (wrong tier) |

Option B wins on every criterion that matters; pays for it with ~2x the per-ADR
text vs Option A.

---

## 5. Mock implementation details

### 5.1 `MockEmailProvider`

(Per §3.2.2; restated as Implementation-checklist material for the ADR.)

- Lives in `shipyard/packages/foundation-integrations/Email/MockEmailProvider.cs`.
- Implements `IEmailProvider` and `IMockVendorProvider`.
- Maintains a thread-safe `ConcurrentBag<EmailDispatchRequest>` of every send.
- Logs every send via `ILogger<MockEmailProvider>` (`LogInformation` level —
  developer-visible in console output).
- Exposes:
  - `IReadOnlyCollection<EmailDispatchRequest> SentMessages { get; }` — test
    assertions consume this.
  - `void Clear()` — test fixtures reset between cases.
  - `double SimulateFailureRate { get; set; }` — `0.0` default; non-zero
    triggers random `ProviderError`.
- Returns `EmailDispatchResult` with `ProviderMessageId =
  $"mock-{Guid.NewGuid()}"` for accepted sends.

### 5.2 Dev-mode inbox UI

A new minimal Bridge admin route — registered only when `MockEmailProvider`
is bound AND `IHostEnvironment.IsDevelopment()`:

- `GET /admin/dev/mock-inbox` — renders a list of sent emails (subject, to,
  timestamp, link to view body).
- `GET /admin/dev/mock-inbox/{id}` — renders the full email body (text + HTML
  toggle).
- Authentication: `[Authorize]` with a dev-only policy; off in production
  unconditionally (the route is not registered when `!IsDevelopment()`).

Implementation: a `MapMockInboxEndpoints(IEndpointRouteBuilder)` extension in
`foundation-integrations/Email/DevInboxEndpoints.cs`; signal-bridge `Program.cs`
calls it conditionally.

The dev inbox UI is **not required by the ADR text** — ADR 0096 specifies the
substrate; the dev UI is a Stage-05 implementation deliverable. The ADR cites
it as "out of scope for the substrate ADR; Engineer ships in W79 Stage-05."

### 5.3 `InMemoryCaptchaVerifier` extensions

Per §3.3.1 #3, the existing W#28 fixture gains three static factory methods:

```csharp
public sealed class InMemoryCaptchaVerifier : ICaptchaVerifier, IMockVendorProvider
{
    public static InMemoryCaptchaVerifier AlwaysPass()
        => new InMemoryCaptchaVerifier(minPassingScore: 0.0); // every token >= 0.0; "always-pass" semantics

    public static InMemoryCaptchaVerifier AlwaysFail()
        => new AlwaysFailCaptchaVerifier();

    public static InMemoryCaptchaVerifier WithMagicToken(string magicToken)
    {
        var verifier = new InMemoryCaptchaVerifier();
        verifier.Seed(magicToken, score: 1.0);
        return verifier;
    }
    // existing constructors preserved
}
```

The `AlwaysFail` variant requires a small private subclass because the current
`VerifyAsync` returns `Passed=false` for unseeded tokens by default — exactly
the "always-fail" semantics for an empty-seed instance. Two paths:
- (a) Document that `new InMemoryCaptchaVerifier()` IS the always-fail
  factory (no new code; just a documentation change).
- (b) Ship explicit static factory methods for discoverability.

ONR recommends (b) — the explicit static factories make test/dev intent clear
at the call site.

### 5.4 Marker interface — `IMockVendorProvider`

```csharp
namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Marker interface implemented by canonical Mock implementations of Tier-2
/// vendor-provider contracts. The startup safety assertion enumerates
/// registered services and fails fast in production environments when any
/// implementation type implements this interface (unless explicit opt-out
/// via SUNFISH_ALLOW_MOCK_PROVIDERS=true).
/// </summary>
public interface IMockVendorProvider { }
```

Zero-member; metadata only. Mock implementations declare it; real adapters
do not.

### 5.5 Startup assertion — `MockProviderProductionGuardAssertion`

```csharp
namespace Sunfish.Foundation.Integrations;

public sealed class MockProviderProductionGuardAssertion : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        // Skip if explicit opt-in
        if (string.Equals(
                Environment.GetEnvironmentVariable("SUNFISH_ALLOW_MOCK_PROVIDERS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (!_hostEnvironment.IsProduction()) return Task.CompletedTask;

        // Enumerate registered services; find any whose ImplementationType
        // (or factory's resolved type) implements IMockVendorProvider.
        var mockBindings = _serviceDescriptors
            .Where(d => d.ImplementationType is { } t
                        && typeof(IMockVendorProvider).IsAssignableFrom(t))
            .Select(d => d.ServiceType.Name)
            .ToArray();

        if (mockBindings.Length > 0)
        {
            throw new InvalidOperationException(
                "Production host has mock vendor-provider bindings registered: " +
                $"[{string.Join(", ", mockBindings)}]. " +
                "Either bind real adapters (set their env vars) or opt out " +
                "with SUNFISH_ALLOW_MOCK_PROVIDERS=true. See ADR 0096.");
        }

        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

Resolves `IServiceCollection` descriptor snapshot at composition time (a
small snapshot service captures them; details Engineer's call).

---

## 6. Real vendor implementation outline

ADR 0096 specifies contracts + mocks; the real adapters are Engineer territory
in `providers-postmark` + `providers-turnstile` packages. The ADR cites:

### 6.1 Postmark — `Sunfish.Providers.Postmark`

- **Wire:** POST `https://api.postmarkapp.com/email` with header
  `X-Postmark-Server-Token: <secret>`; JSON body
  `{ From, To, Subject, TextBody, HtmlBody?, MessageStream? }`.
- **Auth:** `X-Postmark-Server-Token` resolved from `CredentialsReference`
  (via `IHostSecretResolver` or similar host abstraction; ADR 0013 already
  defines the resolution discipline — no plaintext in config).
- **Response mapping:** Postmark returns
  `{ "MessageID": "...", "ErrorCode": 0, "Message": "OK" }` for accepted;
  `ErrorCode != 0` maps to `EmailDispatchStatus.Rejected` /
  `EmailDispatchStatus.QuotaExceeded`.
- **Retry posture:** Adapter does NOT retry on `ProviderError`; the consuming
  block decides retry policy via Polly or background job. Idempotency handled
  via Postmark's idempotency-key header (or substrate-side via
  `EmailDispatchId`).
- **ProviderDescriptor:** `Key="sunfish.providers.postmark"`, `Category=TransactionalEmail`,
  `Capabilities=["transactional", "templated", "message-streams"]`, `Version="1.0.0"`.

### 6.2 Turnstile — `Sunfish.Providers.Turnstile`

- **Wire:** POST `https://challenges.cloudflare.com/turnstile/v0/siteverify`
  with form body `{ secret, response, remoteip }` (form-encoded, not JSON).
- **Auth:** `secret` field in form body, resolved from `CredentialsReference`.
- **Response mapping:** Turnstile returns
  `{ "success": bool, "error-codes": [...], "challenge_ts": "...",
  "hostname": "...", "action": "...", "cdata": "..." }`. Note: **Turnstile
  does not return a score** — the substrate sets `Score=1.0` when `success=true`,
  `Score=0.0` when `success=false`, and the `MinPassingScore` check is a no-op
  (always passes the threshold when the token is valid). Adapter xmldoc must
  document this divergence from reCAPTCHA v3.
- **ProviderDescriptor:** `Key="sunfish.providers.turnstile"`, `Category=Captcha`,
  `Capabilities=["bot-protection", "privacy-respecting", "no-score"]`,
  `Version="1.0.0"`.

---

## 7. Cross-fleet integration concerns

### 7.1 Interaction with ADR 0095 Bootstrap Context

`IEmailProvider.SendAsync` is invoked from the pre-tenant signup window
(welcome email, verification link). The Bootstrap Context (ADR 0095) is the
scoped DI primitive the signup handler injects; the handler then injects
`IEmailProvider` and calls `SendAsync` with `Tenant=null` (the
`EmailDispatchRequest.Tenant` field is nullable specifically for this).

ADR 0096 cites ADR 0095 in §References + §Compatibility plan; states the
invariant "post-Accepted ADR 0095 + ADR 0096, the signup handler injects
`IBootstrapContext` + `IEmailProvider` together"; **does not define** the
signup handler shape (W80 Stage-05).

Similarly `ICaptchaVerifier.VerifyAsync` is invoked from Bootstrap scope; the
`CaptchaToken` lives on `IBootstrapContext.CaptchaToken` per the ADR 0095
scaffold; the handler reads it and calls `VerifyAsync`.

### 7.2 Interaction with ADR 0052 (bidirectional messaging substrate)

ADR 0052's `IMessagingGateway` covers **bidirectional thread messaging** —
inbound webhook ingestion + abuse scoring + per-tenant per-sender isolation +
thread tokens. ADR 0096's `IEmailProvider` covers **unidirectional
transactional onboarding email** — one-shot fire-and-forget with idempotency.

Both contracts live in `foundation-integrations/Messaging` and
`foundation-integrations/Email` respectively. **No interface compatibility**
— a `PostmarkEmailProvider` does not implement `IMessagingGateway` and
vice versa. If a future use case needs both shapes (e.g., a customer-support
inbox that's both transactional and threaded), the consuming block resolves
both contracts and selects per call.

ADR 0096 cites ADR 0052 in §Decision drivers explaining why
`IMessagingGateway` is not extended.

### 7.3 Test isolation

Each integration test gets its own DI container; therefore each gets its own
`MockEmailProvider` and `InMemoryCaptchaVerifier` instance. The `Clear()`
method on `MockEmailProvider` is provided for cases where a test composes
multiple sub-scenarios within one container. The `InMemoryCaptchaVerifier`'s
`Seed()` method is per-instance; no cross-test pollution.

The substrate's `MockProviderProductionGuardAssertion` IS exercised by tests
that boot the full host — those set `SUNFISH_ALLOW_MOCK_PROVIDERS=true` in
their test fixture / CI environment to opt out.

### 7.4 Production deployment

The deployment manifest (Helm chart / Terraform / Bicep — whichever Sunfish
adopts; out of ADR 0096 scope) sets the real-vendor env vars:

```
POSTMARK_API_KEY=<postmark-server-token>
TURNSTILE_SECRET_KEY=<turnstile-secret>
TURNSTILE_SITE_KEY=<turnstile-site-key>
SUNFISH_ALLOW_MOCK_PROVIDERS=false  # default; explicit for clarity
```

Composition root binds the real adapters; the startup assertion verifies no
`IMockVendorProvider`-implementing bindings remain.

Misconfiguration scenarios + their handling:
| Misconfig | Detected by | Behavior |
|---|---|---|
| `POSTMARK_API_KEY` typo (env var unset) | Startup assertion | App fails to start with "mock email provider bound in production" |
| `POSTMARK_API_KEY` set but invalid value | First `SendAsync` call → 401 from Postmark | App returns `EmailDispatchStatus.Rejected`; signup endpoint surfaces user-facing error per W80 Stage-05 |
| `TURNSTILE_SECRET_KEY` unset | Startup assertion | App fails to start |
| `TURNSTILE_SITE_KEY` unset (frontend) | Frontend smoke test | Frontend script fails; signup form unrenderable. Out of ADR 0096 scope (frontend territory). |

### 7.5 Forward-application — what comes after email + CAPTCHA

The same pattern applies to:

- **Storage** — `IBlobStorage` contract; `MockBlobStorage` in-memory store;
  `S3BlobStorage` / `AzureBlobStorage` adapters in `providers-aws-s3` /
  `providers-azure-blob`. Use case: avatar uploads, document attachments.
  Future ADR.
- **IdentityProvider** — `IExternalIdpProvider` contract; `MockIdpProvider`;
  `providers-okta`, `providers-entra`. Already on horizon per OIDC scoping
  research. Future ADR.
- **Payments upgrade** — `IPaymentGateway` already exists (ADR 0051) but does
  not yet have the mock-first DI discipline. Folding into the new pattern is
  a low-risk amendment (existing `InMemoryPaymentGateway` matches the mock
  shape; just needs `IMockVendorProvider` marker + DI helper retrofit). Cite
  in §Forward-watch.
- **BankingFeed, FeatureFlags, ChannelManager** — existing categories with
  varying maturity; each retrofits when its W#XX cohort lands.

ADR 0096 §Forward-watch cites these; ADR 0096 §Decision applies only to
email + CAPTCHA + future-when-they-land.

---

## 8. Recommended option with reasoning

**Option B — Generalized "Tier-2 Vendor-Provider Substrate" covering email +
CAPTCHA + future vendors in one canonical pattern.**

Three load-bearing reasons:

1. **The discipline IS the substrate.** CIC's directive "mocked services should
   be set up for now and eventually replaced with real subscriptions" is a
   universal Tier-2 statement, not a per-vendor preference. Codifying it once
   matches the directive's framing; codifying it twice (Option A) or three
   times (Option C) requires repeated text + repeated council review.

2. **ADR 0013's precedent.** ADR 0013 (Foundation.Integrations) shipped
   discipline (provider-neutrality) + concrete categories
   (CredentialsReference, ProviderDescriptor, IWebhookEventHandler) + initial
   examples (Messaging, FeatureFlags) in one ADR. ADR 0096 generalizing the
   mock-first discipline + the two new vendor surfaces follows the same
   precedent.

3. **Engineer + council reviewer cognitive load.** One ADR; two contracts; one
   helper pattern. The mock-first discipline is reviewed once; both contract
   surfaces are reviewed in the same pass; future Tier-2 ADRs read this ADR
   and reuse the pattern. Per `feedback_prefer_cleanest_long_term_option` the
   +45-min authoring cost of Option B over Option A is correct.

The recommendation closes the A-vs-B halt that Admiral routed to this scaffold.

### Risk acknowledgment — why Option B is not obviously dominant

- **Larger ADR text.** Option B will be ~1.5-2x the length of an email-only
  ADR. ADR 0091 R2 and ADR 0094 successfully shipped at similar lengths; not
  blocking but increases pre-merge council surface.
- **Two contracts reviewed at once.** A council reviewer disagreeing with
  `IEmailProvider`'s shape would block ADR 0096's CAPTCHA half as collateral.
  Mitigation: §Considered options should include a per-contract option-walkthrough
  in the ADR Rev 1 text so reviewers can deltf-comment.

These are real costs but small relative to Option B's structural benefits.

### Confidence level

**HIGH.** The recommendation is grounded in (a) CIC's explicit directive framing,
(b) ADR 0013's substrate-tier precedent, (c) the visible savings on future
Tier-2 ADRs (storage, identity, payments-retrofit), and (d) the
`feedback_prefer_cleanest_long_term_option` directive's bias toward substrate-
correct authoring over speed.

---

## 9. Halt conditions Admiral must resolve

### Halt 1 — Mock-package layout

Two candidates for where `MockEmailProvider`, `InMemoryCaptchaVerifier`, and
`IMockVendorProvider` live:

| Option | Pro | Con |
|---|---|---|
| (a) Same package as contracts (`foundation-integrations`) | Mock + contract co-located; one package the dev/test consumer references; matches today's `InMemoryCaptchaVerifier` placement | Mock implementation code ships in every consumer of the contract (mostly fine — small bytes); the package becomes "contracts + mocks" rather than pure contracts |
| (b) Separate `foundation-integrations-mocks` package | Pure contract package; mock pulled in only when needed; supports stricter ADR 0013 §enforcement (production composition can use a Roslyn analyzer to prevent referencing `*-mocks` package) | Two packages to maintain; dev/test ergonomics require explicit reference; departs from today's layout |

**ONR recommendation:** Option (a) — same package as contracts. Matches today's
`InMemoryCaptchaVerifier` layout; the byte cost is negligible; the
`IMockVendorProvider` marker + startup assertion provides the production-
safety guarantee that the Option (b) Roslyn analyzer would otherwise enforce.
The Admiral ruling §Substrate-alignment used the example
`packages/foundation-integrations-mocks/` — ONR's recommendation diverges from
that example based on the layout analysis above; halt retained because the
example was illustrative not prescriptive but Admiral may prefer the
explicit separation.

### Halt 2 — `IEmailProvider` contract home

Two candidates:

| Option | Notes |
|---|---|
| (a) New subnamespace `Sunfish.Foundation.Integrations.Email/` | Matches existing `Captcha/`, `Messaging/`, `Payments/`, `Signatures/` layout. ONR recommendation. |
| (b) Reuse `Sunfish.Foundation.Integrations.Messaging.IMessagingGateway` | Folds onboarding email into ADR 0052's bidirectional substrate. Rejected per §1.2 #4 (conflated-scope smell). |

**ONR recommendation:** (a). The Captcha/Email/Messaging/Payments/Signatures
folder layout is already canonical; one more folder for Email is consistent.

### Halt 3 — Council review scope

ADR 0096 is substrate-tier and introduces (a) new DI helpers consumed by
every Tier-2 surface, (b) startup assertions that fail-fast in production,
(c) handling of vendor credentials (Postmark token; Turnstile secret).

**ONR recommendation:**
- **.NET-architect council: MANDATORY pre-merge review** on the ADR text +
  Step 1 implementation PR (substrate package work — DI helpers, marker
  interface, startup assertion). The DI-helper signature has subtle implications
  (extension ordering, conditional registration semantics) the council should
  validate.
- **Security-engineering council: SPOT-CHECK on Step 2 + Step 3 implementation
  PRs** (Postmark + Turnstile adapters — they handle the actual secrets).
  Substrate ADR text itself does not require sec-eng review because no
  endpoint surface ships.

### Halt 4 — `ProviderCategory` enum extension timing

The enum is missing `Captcha` + `TransactionalEmail`. Three options:

| Option | Notes |
|---|---|
| (a) Add both as part of ADR 0096 Step 1 PR | Cleanest; the descriptors land with the contracts they describe |
| (b) Defer enum extension to a separate `foundation-catalog` PR | Decouples; allows independent review/merge |
| (c) Use `Other` for both initially; promote later | Lowest immediate change; deferred clean-up |

**ONR recommendation:** (a) — add `Captcha = 10` and `TransactionalEmail = 11`
as part of ADR 0096 Step 1 PR. Both belong with the substrate; deferring just
adds a small-step PR that delays clean descriptors. Enum is in
`foundation-catalog`, not `foundation-integrations` — Step 1 PR will touch
both packages.

### Halt 5 — `ICaptchaVerifier` naming

The existing W#28 contract is `ICaptchaVerifier` (verb-ish, action-oriented).
The new email contract proposed is `IEmailProvider` (noun-ish, role-oriented).
Two paths:

| Option | Notes |
|---|---|
| (a) Keep `ICaptchaVerifier` as-is; introduce `IEmailProvider` alongside | Naming inconsistency; documented quirk |
| (b) Rename `ICaptchaVerifier` to `ICaptchaProvider`; type-forward + obsolete the old name | Consistent naming; one breaking change (mitigated by type-forward) |

**ONR recommendation:** (a). `ICaptchaVerifier` is in use by W#28 Phase 3.1 +
the `RecaptchaV3CaptchaVerifier` adapter; renaming creates a breaking-change
cohort for negligible benefit. Document the asymmetry in xmldoc: "By
convention, Tier-2 contracts are named `IXProvider`; `ICaptchaVerifier`
preceded this convention (W#28 ADR 0059) and is kept for backward compat."

### Halt 6 — Postmark message-streams support

Postmark supports per-stream configuration (transactional vs broadcast).
The substrate's `EmailDispatchRequest.MessageStream` is nullable; if null, the
Postmark adapter uses its configured `DefaultMessageStream`. Question for
Admiral: should ADR 0096 mandate a stream taxonomy ("transactional",
"verification", "invitation", "broadcast") or leave it free-form?

**ONR recommendation:** Free-form string in ADR 0096; a follow-up Stage-05
hand-off (W80 — Surface-A+B signup) defines the initial stream names. Tight
coupling to a taxonomy in the ADR risks churn when post-MVP marketing /
notifications categories emerge.

### Halt 7 — Turnstile site-key delivery to frontend

Turnstile requires a public site key in the browser to render the challenge
widget. The substrate's `TurnstileConfig.SiteKey` is read by the .NET adapter
for documentation purposes only — the actual frontend rendering is in
`signal-bridge/Sunfish.Bridge/wwwroot/` or a separate frontend package.

Question: does ADR 0096 specify the bridge-to-frontend site-key handoff
mechanism? Two paths:

| Option | Notes |
|---|---|
| (a) ADR 0096 cites it as out-of-scope; W80 Stage-05 defines the endpoint that exposes the site key | Cleanest separation; substrate ADR stays narrow |
| (b) ADR 0096 defines a `GET /api/integrations/turnstile/site-key` Bridge endpoint | Adds an endpoint surface to the substrate ADR |

**ONR recommendation:** (a). The substrate ADR specifies the contract +
mocks; endpoint exposure is W80 Stage-05's call.

### Halt 8 — Implementation sequencing

ADR 0096 Implementation checklist will sequence:
1. Substrate package work (DI helpers, marker, assertion, `IEmailProvider`,
   `MockEmailProvider`, `InMemoryCaptchaVerifier` retrofit, enum extension).
2. `providers-postmark` package + Postmark adapter.
3. `providers-turnstile` package + Turnstile adapter.
4. Composition-root wiring in signal-bridge (W79 Stage-05 dispatch).

Steps 2 + 3 can ship in parallel post-Step 1. Step 4 is W79 Stage-05's
responsibility, not directly ADR 0096's implementation, but cited as the
"first consumer" forward-link.

**ONR recommendation:** sequence as above; Steps 2 + 3 parallel.

---

## 10. Open questions ONR could not resolve from the codebase

These would benefit from follow-up research or council consultation but do
not block ADR 0096 scaffolding:

1. **Conditional DI registration in Microsoft.Extensions.DependencyInjection
   — supported semantics.** Replacing a prior registration via `services.Replace(...)`
   is well-documented; conditional `services.Replace` driven by env-var presence
   has subtle ordering implications around `IServiceCollection` mutation timing.
   .NET-architect council can validate the proposed `UseVendorProviderIfConfigured`
   shape against canonical patterns.

2. **Postmark `.NET` SDK vs HttpClient-only.** Postmark ships a `Postmark.NET`
   SDK; reCAPTCHA adapter precedent uses HttpClient-only. ADR 0096 should
   commit to one; ONR recommends HttpClient-only for consistency with the
   ADR 0013 §enforcement supply-chain posture, but the Postmark SDK adds value
   for retry + idempotency-key handling. .NET-architect council resolves.

3. **`IHostSecretResolver` shape.** Postmark + Turnstile adapters need to
   resolve secrets at request time; the existing `CredentialsReference` is
   an opaque handle. The resolution mechanism (vault adapter, env var fallback,
   user-secrets) needs a defined interface. If `IHostSecretResolver` does not
   yet exist in `foundation-integrations`, ADR 0096 either specifies it or
   defers to a follow-up Engineer dispatch.

4. **Email idempotency-key semantics across retries.** If the signup endpoint
   retries an email send (transient network error), is the `EmailDispatchId`
   propagated to Postmark as a vendor idempotency key, or does the substrate
   maintain its own dedup table? Cross-cuts with `foundation-idempotency`
   (existing package per
   `feat-foundation-idempotency` branch). Resolution: ADR 0096 cites the
   forward-watch; Engineer aligns in W79 Stage-05.

5. **Cloudflare Turnstile per-action support.** Turnstile supports an `action`
   parameter for per-route tagging (signup, login, etc.). Substrate-side, this
   could be a property on `ICaptchaVerifier.VerifyAsync(... actionName)`,
   but adding a parameter to the existing contract is a breaking change.
   Resolution: defer; the `action` parameter does not affect verification
   correctness, only Cloudflare's analytics dashboard categorization. If
   wanted later, extend via `IExtendedCaptchaVerifier : ICaptchaVerifier`.

---

## 11. Sources cited

**Primary (publication + retrieval dates):**

1. Admiral ruling on Decisions 4 + 5 + mock-first directive —
   `coordination/inbox/admiral-ruling-2026-05-25T18-10Z-cic-decisions-4-5-vendors-with-mock-first.md`;
   authored 2026-05-25; retrieved 2026-05-25.
2. Admiral ruling on 10 decisions —
   `coordination/inbox/admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md`;
   authored 2026-05-25; retrieved 2026-05-25.
3. ADR 0013 (Foundation.Integrations + Provider-Neutrality Policy) —
   `shipyard/docs/adrs/0013-foundation-integrations.md`; Accepted 2026-04-19;
   retrieved 2026-05-25.
4. ADR 0052 (Bidirectional Messaging Substrate) —
   `shipyard/docs/adrs/0052-bidirectional-messaging-substrate.md`;
   retrieved 2026-05-25.
5. ADR 0091 R2 (ITenantContext divergence resolution) —
   `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md`;
   retrieved 2026-05-25.
6. shipyard `packages/foundation-integrations/IProviderRegistry.cs` — retrieved 2026-05-25.
7. shipyard `packages/foundation-integrations/ProviderDescriptor.cs` — retrieved 2026-05-25.
8. shipyard `packages/foundation-integrations/InMemoryProviderRegistry.cs` — retrieved 2026-05-25.
9. shipyard `packages/foundation-integrations/ServiceCollectionExtensions.cs` —
   retrieved 2026-05-25.
10. shipyard `packages/foundation-integrations/Captcha/ICaptchaVerifier.cs` —
    retrieved 2026-05-25.
11. shipyard `packages/foundation-integrations/Captcha/InMemoryCaptchaVerifier.cs` —
    retrieved 2026-05-25.
12. shipyard `packages/foundation-integrations/Messaging/IMessagingGateway.cs` —
    retrieved 2026-05-25.
13. shipyard `packages/foundation-integrations/Messaging/OutboundMessageRequest.cs` —
    retrieved 2026-05-25.
14. shipyard `packages/foundation-integrations/Messaging/MessagingProviderConfig.cs` —
    retrieved 2026-05-25.
15. shipyard `packages/foundation-integrations/README.md` — retrieved 2026-05-25.
16. shipyard `packages/foundation-catalog/Bundles/ProviderCategory.cs` —
    retrieved 2026-05-25.
17. shipyard `packages/providers-recaptcha/RecaptchaV3CaptchaVerifier.cs` —
    retrieved 2026-05-25 (canonical adapter reference).
18. shipyard `packages/providers-recaptcha/RecaptchaV3Config.cs` —
    retrieved 2026-05-25.
19. shipyard `packages/providers-recaptcha/Sunfish.Providers.Recaptcha.csproj` —
    retrieved 2026-05-25 (canonical adapter package layout).

**Secondary:**

20. Slotting-architecture recommendation — `shipyard/icm/01_discovery/research/onr-slotting-architecture-general-upf.md`;
    on main; retrieved 2026-05-25 (Tier-2 `category-provider` semantics §5).
21. ADR 0095 Bootstrap Context scaffold — `shipyard/.worktrees/onr-adr-0095-bootstrap-context-scaffold/icm/01_discovery/research/onr-adr-0095-bootstrap-context-scaffold.md`;
    authored 2026-05-25; retrieved 2026-05-25 (consuming-substrate
    cross-reference; scaffold cadence template).
22. ADR 0069 (ADR authoring discipline) — `shipyard/docs/adrs/0069-adr-authoring-discipline.md`;
    retrieved 2026-05-25 (substrate-tier + pre-merge council requirements).
23. `feedback_prefer_cleanest_long_term_option` memory entry —
    codifies substrate-correct over ship-fast bias.

**Tertiary (vendor / framework convention; verified externally):**

24. Postmark `/email` API — `https://postmarkapp.com/developer/api/email-api`;
    retrieved 2026-05-25 (request shape, response shape, idempotency key
    semantics).
25. Cloudflare Turnstile `/siteverify` — `https://developers.cloudflare.com/turnstile/get-started/server-side-validation/`;
    retrieved 2026-05-25 (request/response shape; no-score behavior).
26. Microsoft.Extensions.DependencyInjection conditional-registration patterns —
    Microsoft Learn `dotnet/core/extensions/dependency-injection`; general
    knowledge.

---

## 12. What ONR does next

Per the Admiral ruling §Forward-sequence:

1. This scaffold ships (PR open, status beacon filed).
2. Admiral consumes the scaffold; authors ADR 0096 Rev 1 (Admiral territory,
   NOT ONR).
3. ADR 0096 Rev 1 enters council review (.NET-architect MANDATORY; sec-eng
   SPOT-CHECK on follow-on adapter PRs per Halt 3).
4. Post-ratification:
   - Engineer dispatches Step 1 substrate-package PR (DI helpers, marker,
     assertion, `IEmailProvider` + `MockEmailProvider`, `InMemoryCaptchaVerifier`
     retrofit, enum extension).
   - W79 Stage-05 hand-off authoring proceeds (substrate-cohort 1 — uses
     `IBootstrapContext` from ADR 0095 + `IEmailProvider` + `ICaptchaVerifier`
     from ADR 0096).
5. After ADR 0096 + ADR 0095 ship, the four sub-cohort Stage-05 hand-offs
   (W80, W81, W82, W83) reference both ADRs.

— ONR, 2026-05-25
