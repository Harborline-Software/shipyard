# Workstream #20 — Bidirectional Messaging Substrate — Stage 06 hand-off

**From:** ONR (Admiral directive `admiral-directive-2026-05-17T23-15Z-onr-ws-e-handoff-authoring-and-itenantcontext-discovery`)
**To:** Engineer
**Created:** 2026-04-28 (original); revised 2026-05-17 (this revision — Phases 4-9 deepened per ADR 0052 amendments + prereq-scoping decisions)
**Status:** in-flight (Phases 0/1/2/3 shipped; Phases 4-9 + Bridge-side sub-phases queued)
**Workstream:** #20 (Bidirectional Messaging Substrate — cluster #4 spine; WS-E in current MASTER-PLAN slug)
**Spec:** [ADR 0052](../../../docs/adrs/0052-bidirectional-messaging-substrate.md) (Accepted 2026-04-29; amendments A1–A5 + Minor landed 2026-04-29)
**Prereq research:** [`onr-wse-handoff-prereq-2026-05-17.md`](../../01_discovery/research/onr-wse-handoff-prereq-2026-05-17.md) — 12 open questions (OQ-WSE-1…12) answered; this hand-off freezes those answers as build directives.
**Addendum (still load-bearing):** [`property-messaging-substrate-stage06-addendum.md`](./property-messaging-substrate-stage06-addendum.md) (Phase 0 `ITenantKeyProvider` stub — shipped PR #294)
**Pipeline variant:** `sunfish-feature-change` (new substrate; no breaking changes — Phases 0/1/2/3 already in production)
**Estimated remaining effort:** ~12–16h Engineer time (Phases 4-9 + new Phase 5a Bridge SSE channel)
**PR count remaining:** 6–8 PRs (Phase 5 may split into 5a / 5b / 5c)
**Pre-merge council:** **MANDATORY** on Phases 4 + 5b + 5c (provider-credential surface; webhook trust boundary; egress relay credentials). Security-engineering subagent + .NET architect subagent both required. Phases 6/8/9 are checklist-class — no council unless surface changes mid-flight.
**Attribution:** No third-party-code carry. Postmark.NET / Twilio.NET vendor SDKs are isolated to `providers-*` packages per ADR 0013 enforcement gate; vendor licenses unchanged (MIT for both).

---

## Gate conditions

This hand-off resumes mid-stream. Verify before opening any new PR:

```bash
# All four predecessor phases must be on main
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^(foundation-integrations|blocks-messaging|foundation-recovery)$"
# Expected: all three present
git log --all --oneline -- packages/foundation-integrations/Messaging/ packages/blocks-messaging/ packages/foundation-recovery/TenantKey/
# Expected: PRs #273 (contracts), #276 (substrate), #294 (TenantKey stub), #302 (HmacThreadTokenIssuer)
```

| Predecessor | PR | Verified by |
|---|---|---|
| Phase 0 — `ITenantKeyProvider` stub in `foundation-recovery/TenantKey/` | #294 | grep `ITenantKeyProvider` namespace exists; tests pass |
| Phase 1 — `Sunfish.Foundation.Integrations.Messaging` contracts | #273 | namespace compiles; `IOutboundMessageGateway` + `IInboundMessageReceiver` + supporting types present |
| Phase 2 — `blocks-messaging` substrate scaffold (Thread + Message + InMemory) | #276 | package builds; `ISunfishEntityModule` registered |
| Phase 3 — `HmacThreadTokenIssuer` per A2 | #302 | mint/verify round-trip < 5ms p95 in unit benchmarks; revocation log working |

If any gate is unmet → STOP; drop `onr-question-*` beacon naming the gap. Do not proceed to Phase 4.

---

## Phase 2.1 scope (revised per prereq scoping)

This hand-off ships Phase 2.1 of ADR 0052: **bidirectional outbound + inbound for email via Postmark**, with the Bridge-side relay infrastructure that makes inbound webhooks reachable. SMS (Twilio) and second-email-vendor parity (SendGrid) are Phase 2.2 follow-ons; per-tenant DKIM/SPF/DMARC (custom domains) is Phase 2.3.

Frozen decisions from `onr-wse-handoff-prereq-2026-05-17.md` (the 12 OQs) that this hand-off encodes as build directives:

| OQ | Decision | Implication for Phases 4–9 |
|---|---|---|
| OQ-WSE-1 | Egress route default = **BridgeRelayed**; NodeDirect = opt-in (sovereign hosts) | Phase 5c authors the Bridge-side egress relay; node POSTs `DispatchRequest` over the SSE channel |
| OQ-WSE-2 | Inbound relay = **dedicated `messaging-inbound` SSE channel** (Bridge → node) | Phase 5a authors the SSE channel; not sync-daemon piggyback |
| OQ-WSE-3 | Voice channel = **deferred** to separate ADR | `MessageChannel` enum stays `{ Email, Sms }` for Phase 2.1; no `Voice` |
| OQ-WSE-4 | Tenant config authoring = **Bridge operator-authored JSON for Phase 2.1**; admin UI = Phase 2.2 | Phase 2.1 ships JSON-schema-validated config; no FED workload this phase |
| OQ-WSE-5 | Compliance gate startup posture = **fail-closed at start**, operator override flag for "registration in flight" | Phase 4 adapter init validates; `MessagingProviderConfig.ComplianceOverrides` carries override |
| OQ-WSE-6 | First-send compliance failure = **return `ComplianceGateRejected`**; no silent drop | Phase 4 adapter returns `OutboundDispatchFailure.ComplianceGateRejected`; surfaces to caller |
| OQ-WSE-7 | HTTP-level integration tests = **WireMock.NET** | Phase 4 tests use WireMock.NET stubs, not VCR cassettes |
| OQ-WSE-8 | Live-vendor-sandbox test gating = **opt-in env var** `SUNFISH_MESSAGING_LIVE_TESTS=1` | Phase 4 + Phase 7 tests check env var; default = fakes-only |
| OQ-WSE-9 | ERPNext coexistence = **compose** (not replace, not coexist-ignorantly) | Frappe→Sunfish read-only mirror = Phase 2.1 doc + adapter shim; bidirectional = Phase 2.3 (out of scope this hand-off) |
| OQ-WSE-10 | Frappe→Sunfish mirror in Phase 2.1 = **read-only one-way** | Phase 8 doc names the Frappe `Communication` doctype subscriber pattern; no code in Phase 2.1 |
| OQ-WSE-11 | Per-channel timeouts = **Email 30s / SMS 10s** (SMS not in Phase 2.1) | Phase 4 adapter sets 30s HTTP timeout; resilience pipeline retries inside that budget |
| OQ-WSE-12 | Error-model taxonomy = **freeze 8-variant `OutboundDispatchFailure`** | Phase 4 ships the discriminated union (see below); vendor-specific causes map into closest variant + carry vendor detail on the audit record |

The hand-off DOES NOT settle (deferred to subsequent hand-offs, named so Admiral can route follow-ups):

- **SMS dispatch (Twilio adapter + A2P 10DLC registration flow)** — Phase 2.2
- **Per-tenant DKIM/SPF/DMARC custom domains** — Phase 2.3 (revisit trigger named in ADR 0052 A3)
- **Bridge admin UI for tenant config authoring** — Phase 2.2 (FED workstream; this hand-off ships operator-authored JSON only)
- **Frappe→Sunfish bidirectional event mirror** — Phase 2.3 (Frappe doctype mutation is invasive; needs design)
- **Voice channel contract** — separate ADR (deferred per OQ-WSE-3)
- **Per-tenant secret-resolution adapter** — depends on ADR 0046 Stage 06 completion (KEK hierarchy)

---

## Scope summary — what Phases 4–9 ship

1. **`providers-postmark` adapter** (Phase 4) — first email outbound + inbound provider, isolated per ADR 0013 enforcement gate
2. **Bridge `messaging-inbound` SSE channel** (Phase 5a) — dedicated transport for Bridge→node forwarding of inbound envelopes
3. **Inbound 5-layer defense at Bridge** (Phase 5b) — ADR 0052 A1 implementation
4. **Bridge-relayed egress path** (Phase 5c) — default outbound route: node POSTs to Bridge; Bridge calls provider
5. **Audit emission** (Phase 6) — 17 typed `AuditEventType` constants + factory
6. **Cross-package integration tests** (Phase 7) — end-to-end smoke covering all 5 defense layers + Bridge SSE round-trip
7. **apps/docs** (Phase 8) — 4 docs pages + provider-selection guidance
8. **Ledger flip** (Phase 9) — W#20 row → `built`

**NOT in scope (explicit):** SendGrid parity adapter, Twilio SMS adapter + A2P 10DLC, public-listings inquiry surface, Frappe coexistence implementation (Phase 8 doc only), Bridge admin UI form, per-tenant subdomain DKIM/SPF/DMARC.

---

## Phases

### Phase 0 — `ITenantKeyProvider` stub in `foundation-recovery/` ✅ SHIPPED

PR #294. Per the addendum. Provides per-tenant HMAC key material consumed by Phase 3.

### Phase 1 — `Sunfish.Foundation.Integrations.Messaging` contracts ✅ SHIPPED

PR #273. Full namespace shipped per ADR 0052 §"Initial contract surface":

- `IOutboundMessageGateway` (egress)
- `IInboundMessageReceiver` (ingress)
- `IThreadTokenIssuer` (per A2)
- `IInboundMessageScorer` (per A1 — `NullScorer` default)
- `IUnroutedTriageQueue`
- `IThreadStore` (cluster-level wrapper around the records; `SplitAsync` for Minor amendment)
- All envelope/event/config records (`OutboundMessage`, `OutboundDispatchHandle`, `OutboundDispatchStatus`, `InboundEnvelope`, `InboundReceiveOutcome`, `MessagingProviderConfig`, `Thread`, `Message`, `ThreadParticipant`, `MessageVisibility` 3-value enum)

### Phase 2 — `blocks-messaging` substrate scaffold ✅ SHIPPED

PR #276. Entity model + `InMemoryThreadStore` + `InMemoryOutboundMessageGateway` + `MessagingEntityModule` + DI extension `AddInMemoryMessaging()`.

### Phase 3 — `HmacThreadTokenIssuer` per A2 ✅ SHIPPED

PR #302. HMAC-SHA256 over `{tenantId}:{threadId}:{notBeforeUtc:O}` with key from `ITenantKeyProvider.DeriveKeyAsync(tenant, "thread-token-hmac", ct)`. 90-day TTL, base32 34-char format, append-only revocation, 7-day rotation grace window. Verify < 5ms p95.

---

### Phase 4 — `providers-postmark` adapter (~4–5h)

**Package:** `packages/providers-postmark/`
**Namespace root:** `Sunfish.Providers.Postmark`
**PR title:** `feat(providers-postmark): Phase 2.1 first email adapter — outbound + inbound (ADR 0052 A1+A5 + ADR 0013)`
**Council:** **MANDATORY** — security-engineering (vendor-credential surface + signature-verify boundary) + .NET-architect (resilience pipeline shape + provider-neutrality conformance).

#### Types and contract impls

**Outbound side — `PostmarkOutboundMessageGateway.cs`**

Implements `IOutboundMessageGateway` from Phase 1. Wraps `Postmark.PostmarkClient` from the `Postmarkdotnet` NuGet (5.3.0+). Specific behaviors:

```csharp
namespace Sunfish.Providers.Postmark;

public sealed class PostmarkOutboundMessageGateway : IOutboundMessageGateway
{
    // DispatchAsync semantics (frozen per OQ-WSE-11 + OQ-WSE-12):
    //
    // 1. Returns OutboundDispatchHandle IMMEDIATELY after Postmark accepts (HTTP 200 OK
    //    with MessageID). Status transition Queued → Sent happens at this point — Sent
    //    means "provider accepted," NOT "delivered." Caller polls GetStatusAsync for
    //    Delivered / Bounced / Complained / Opened / Clicked transitions.
    //
    // 2. Resilience pipeline: Microsoft.Extensions.Http.Resilience standard pipeline
    //    (Retry: exponential backoff, max 3 attempts; CircuitBreaker: 0.5 failure ratio
    //    over 30s window; Timeout: per-attempt 10s, overall 30s — matches OQ-WSE-11
    //    Email 30s budget). Configured via AddStandardResilienceHandler() on the
    //    HttpClient registered for Postmark.
    //
    // 3. Per-attempt audit emission: EACH retry attempt emits MessageDispatchAttempt
    //    (new AuditEventType in Phase 6) with attempt-number + outcome. Successful
    //    terminal dispatch emits MessageDispatched (rollup). Audit records are correlated
    //    via OutboundMessage.Audit.CorrelationId. Rationale: per-attempt visibility is
    //    needed for vendor-incident forensics; rollup-only loses retry behavior.
    //
    // 4. Failure-mode mapping: Postmark error codes → OutboundDispatchFailure variants:
    //
    //    Postmark error code             →  Variant
    //    -------------------------------    -------------------------------
    //    401, 422 (invalid token)        →  ProviderAuthRejected
    //    429, 500-class                  →  ProviderRateLimited / TransientNetworkError
    //    300, 406 (inactive recipient)   →  RecipientBlocked
    //    400 (invalid email body)        →  ContentRejected
    //    405 (suppressed by us)          →  RecipientBlocked
    //    Network / DNS / TLS failures    →  TransientNetworkError
    //    Pre-dispatch compliance fail    →  ComplianceGateRejected (this adapter's
    //                                       startup validation; see §Compliance gates)
    //    Hard bounce on first send       →  PermanentBounce (rare; usually arrives
    //                                       via webhook, not sync response)
    //    Missing tenant config           →  TenantConfigMissing (raised before HTTP)
    //
    // 5. Vendor detail preservation: full Postmark response (status code + ErrorCode +
    //    Message) carried on OutboundDispatchFailure.VendorDetail (string, opaque to
    //    contract callers; logged + audit-emitted).

    public Task<OutboundDispatchHandle> DispatchAsync(OutboundMessage message, CancellationToken ct);

    // GetStatusAsync: queries Postmark's GET /messages/outbound/{messageid}/details endpoint.
    // Maps Postmark Status string → OutboundDispatchStatus enum:
    //   Sent | Queued                    → Queued / Sent
    //   Bounced | SoftBounced             → Bounced
    //   SpamComplaint                     → Complained
    //   Opened / Clicked (event webhook)  → Opened / Clicked (terminal-ish; substrate
    //                                       stops polling after Delivered + 24h)
    public Task<OutboundDispatchStatus> GetStatusAsync(OutboundDispatchHandle handle, CancellationToken ct);
}
```

**`OutboundDispatchFailure` discriminated union (FROZEN — per OQ-WSE-12; ship in `foundation-integrations/Messaging/`)**

```csharp
namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Terminal failure detail for an outbound dispatch. Exactly 8 variants per OQ-WSE-12.
/// Vendor-specific causes map into the closest variant; raw vendor detail rides on VendorDetail.
/// </summary>
public abstract record OutboundDispatchFailure(string? VendorDetail)
{
    public sealed record ProviderAuthRejected(string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record ProviderRateLimited(TimeSpan? RetryAfter, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record TenantConfigMissing(string ConfigKey, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record ContentRejected(string Reason, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record RecipientBlocked(IdentityRef Recipient, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record PermanentBounce(IdentityRef Recipient, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record TransientNetworkError(int? HttpStatus, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
    public sealed record ComplianceGateRejected(string GateName, string Reason, string? VendorDetail) : OutboundDispatchFailure(VendorDetail);
}
```

`OutboundDispatchHandle` is extended to optionally carry a `Failure` field (null on success). Result-type pattern. Existing `OutboundDispatchStatus.Failed` retained but consumers should read `Handle.Failure` for the typed reason.

**Inbound side — `PostmarkInboundParser.cs` + `PostmarkSignatureVerifier.cs`**

Both ship in the same `providers-postmark` package (per ADR 0052 §"Affected packages" — inbound and outbound for the same vendor live in one package per the symmetry argument).

```csharp
public sealed class PostmarkInboundParser
{
    /// <summary>
    /// Parses a Postmark Inbound JSON payload into InboundEnvelope. Verifies signature
    /// via PostmarkSignatureVerifier BEFORE parsing the body — fail-fast on bad sigs.
    /// </summary>
    public Task<InboundEnvelope> ParseAsync(
        ReadOnlyMemory<byte> rawBody,
        IReadOnlyDictionary<string,string> headers,
        TenantId tenant,
        CancellationToken ct);
}

public sealed class PostmarkSignatureVerifier
{
    /// <summary>
    /// Verifies Postmark webhook signature. Postmark uses BasicAuth (username = "postmark",
    /// password = per-server webhook secret). Per ADR 0052 A1, signature verify happens
    /// at the Bridge edge (Layer 1 of the 5-layer defense) BEFORE any parsing.
    /// </summary>
    public bool Verify(
        IReadOnlyDictionary<string,string> headers,
        string expectedSecret);
}
```

#### Vendor-feature exclusion list (FROZEN per prereq scoping §1)

The adapter MUST NOT expose these Postmark features to consumers via the gateway contract:

| Excluded feature | Postmark API | Reason | Compensating contract |
|---|---|---|---|
| **Vendor template engine** | `TemplateAlias`, `TemplateModel` on `/email/withTemplate` | ADR 0013 §"Domain concepts are Sunfish-modeled, not vendor-mirrored" | Consumers render content upstream (Mustache / Razor / Yeoman-side); pass finalized `MessageContent` |
| **Vendor scheduling** | `DeliveryStartAt` on `/email` | Substrate owns time via `IClock`; vendor-side scheduling is opaque to audit | Sunfish-side scheduler enqueues `DispatchAsync` at the desired time |
| **Vendor suppression list** | `/bounces/{id}/activate`, `/suppressions` | ADR 0052 A5 — Sunfish owns consent state | `IConsentLedger` (substrate; not in this hand-off) is the authoritative source |
| **Vendor analytics dashboards** | Postmark UI / Stats API for opens/clicks | Observability is Sunfish telemetry surface | `MessageOpened` / `MessageClicked` audit events drive Sunfish dashboards |
| **Inbound vendor template parsing** | Postmark Inbound `Stream` routing | Substrate routes by `ThreadToken` / fuzzy match per ADR 0052 §"Threading semantics" | All inbound envelopes pass through `IInboundMessageReceiver` uniformly |

Encoded in `BannedSymbols.txt` (provider-neutrality analyzer allow-list — see Phase 4 acceptance criterion).

#### Compliance gates (per OQ-WSE-5 + OQ-WSE-6)

Adapter init validates `MessagingProviderConfig.Email`:

```yaml
# Minimum required fields for adapter to start
email:
  provider: postmark
  credentials_ref: <CredentialsReference per ADR 0013>
  sender_domain: <e.g. notices.acme-properties.com>
  sender_isolation: SharedDomain | PerTenantStream | PerTenantSubdomain
  dkim_status: PendingVerification | Verified | Failed
  # CAN-SPAM compliance (substrate-time, fail-closed)
  physical_address: <required; rendered in email footer>
  list_unsubscribe_endpoint: <required; URL or mailto:>
  # Optional: operator overrides (fail-closed-but-runnable)
  compliance_overrides:
    allow_unverified_dkim: false  # default; flip true only for testing
    allow_missing_physical_address: false  # never flip in production
```

Adapter throws `MessagingAdapterStartupFailure` if any required field is missing or invalid. Per OQ-WSE-5: **fail-closed at startup** — refuse to start; emit `MessagingAdapterStartupFailed` audit; alert ops. Override flags are operator-only via the Bridge-side config; substrate never auto-overrides.

Per OQ-WSE-6: at first-send, if a recipient's consent check fails, return `OutboundDispatchFailure.ComplianceGateRejected` to the caller. Do NOT silent-drop. Do NOT auto-suppress (the substrate's `IConsentLedger` is the suppression authority; not the vendor's).

#### DI extension

```csharp
namespace Sunfish.Providers.Postmark.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PostmarkOutboundMessageGateway, PostmarkInboundParser,
    /// PostmarkSignatureVerifier. Reads MessagingProviderConfig from
    /// IOptionsMonitor; resolves credentials lazily per dispatch via
    /// the ICredentialsResolver from ADR 0013.
    /// </summary>
    public static IServiceCollection AddPostmarkMessagingProvider(
        this IServiceCollection services,
        IConfiguration config);
}
```

#### Tests

WireMock.NET-based (per OQ-WSE-7). One stub server per test class; stubs declared inline (no fixture files).

Coverage:

- Outbound success (HTTP 200 + MessageID returned)
- Each `OutboundDispatchFailure` variant (8 mapping tests; one per error class)
- Resilience pipeline retries on 500-class (verify exactly 3 attempts; verify total time ≤ 30s)
- Resilience pipeline circuit-break (verify open after 5 consecutive 500s; verify half-open after 30s)
- Per-attempt audit emission (each retry emits `MessageDispatchAttempt`)
- Signature verify success against documented Postmark webhook fixture
- Signature verify rejection (rejected secret; rejected header tampering)
- Inbound parsing against Postmark documented fixture (4 fixtures: with-attachments, plain-text, HTML-only, MIME-multipart)
- Compliance startup validation (missing `physical_address` throws `MessagingAdapterStartupFailure`)
- Live-vendor opt-in: tests gated on `SUNFISH_MESSAGING_LIVE_TESTS=1` env var per OQ-WSE-8 (skipped by default; CI does not have the env var)

#### What Phase 4 does NOT include

- SendGrid parity adapter (Phase 2.2)
- Twilio SMS adapter (Phase 2.2)
- A2P 10DLC compliance gates (Phase 2.2 — Twilio-only concern)
- `IConsentLedger` implementation (separate substrate; this adapter consumes the interface)
- Bridge-side webhook receiver controller (that's Phase 5b)
- Bridge-side egress relay endpoint (that's Phase 5c)

#### Gate (PASS/FAIL)

- `dotnet build` clean with `SUNFISH_PROVNEUT_001` analyzer enabled (no Postmark namespace references outside this package)
- All 8 `OutboundDispatchFailure` mapping tests pass
- Resilience pipeline behavior verified against WireMock.NET stubs
- Signature verify works against documented fixtures (positive + 2 negatives)
- Compliance startup-validation fails fast on missing fields
- Council approves (security-engineering + .NET architect)

#### Halt conditions (Phase 4)

- **Postmark.NET SDK signature-verify API has changed** (Postmark sometimes evolves the BasicAuth scheme) → `onr-question-*` naming the change; ADR 0052 amendment may be needed.
- **Provider-neutrality analyzer false-positive** on a legitimate Postmark type inside `providers-postmark` → `onr-question-*` with the specific symbol; analyzer maintainer may need to update allow-list.
- **`ICredentialsResolver` from ADR 0013 not yet implemented** — at credential-resolution time, the gateway has no way to retrieve the Postmark API key from `CredentialsReference`. If still pending: `onr-question-*` naming the dep; Admiral routes to credential-resolution workstream.
- **`MessageContent` schema lacks a field this adapter needs** (e.g., reply-to header) → `onr-question-*` naming the field; freeze adapter pending contract amendment.

#### Acceptance criteria (Phase 4 — cumulative)

- [ ] `Sunfish.Providers.Postmark.PostmarkOutboundMessageGateway` ships, implements `IOutboundMessageGateway`
- [ ] `Sunfish.Providers.Postmark.PostmarkInboundParser` parses 4 documented fixture payloads
- [ ] `Sunfish.Providers.Postmark.PostmarkSignatureVerifier` verifies + rejects per Postmark spec
- [ ] `OutboundDispatchFailure` discriminated union ships in `foundation-integrations/Messaging/`; 8 variants; sealed records
- [ ] Resilience pipeline: 3-attempt retry, 30s overall budget, circuit-break at 0.5 failure ratio
- [ ] Per-attempt `MessageDispatchAttempt` audit emission (rolled up by `OutboundMessage.Audit.CorrelationId`)
- [ ] `BannedSymbols.txt` lists the 5 excluded vendor features; analyzer pass
- [ ] WireMock.NET tests cover all 8 failure variants + resilience behavior + signature verify
- [ ] `MessagingAdapterStartupFailure` thrown on missing required config; fail-closed
- [ ] DI extension `AddPostmarkMessagingProvider(IConfiguration)` shipped

---

### Phase 5a — Bridge `messaging-inbound` SSE channel (~2–3h)

**Package:** `accelerators/bridge/Sunfish.Bridge/Features/Messaging/` (new feature folder under existing Bridge accelerator)
**Namespace root:** `Sunfish.Bridge.Features.Messaging`
**PR title:** `feat(bridge): Phase 2.1 messaging-inbound SSE channel + node forwarding (ADR 0052 + OQ-WSE-2)`
**Council:** advisory only (no credential surface; auth scheme is reused from ADR 0032)

#### What this phase ships

The **dedicated transport** between Bridge's public webhook surface (Layer 5 termination point) and the tenant's local node's `IInboundMessageReceiver`. Per OQ-WSE-2, this is a separate channel from the sync-daemon — blast-radius isolation.

Wire shape:

```
Provider (Postmark Inbound)
   │  HTTPS POST /webhooks/messaging/postmark
   ▼
Bridge:  MessagingInboundController
   │   (Phase 5b — 5-layer defense terminates here)
   ▼
Bridge:  MessagingInboundQueue (per-tenant in-memory; backed by SQLite WAL for restart durability)
   │   Server-Sent Events stream
   │   GET /messaging-inbound/{tenantId}?since=<eventId>
   │   Auth: per-tenant node-bound macaroon (Authorization: Bearer <macaroon>)
   ▼
Node:  IInboundMessageReceiver.ReceiveAsync(InboundEnvelope, ct)
   │   At-least-once; node dedupes via WebhookEventEnvelope.EventId
   ▼
Local thread-store (via blocks-messaging IThreadStore)
```

#### Contract surface

In `Sunfish.Foundation.Integrations.Messaging` (extending Phase 1 contracts):

```csharp
/// <summary>
/// Envelope wrapping an InboundEnvelope with the Bridge-assigned EventId for
/// at-least-once delivery + node-side dedup. Bridge mints the EventId on receipt;
/// nodes dedupe on EventId before invoking IInboundMessageReceiver.
/// </summary>
public sealed record WebhookEventEnvelope
{
    public required string EventId { get; init; }    // ULID; Bridge-assigned at Layer 1 (sig-verified-ingress)
    public required TenantId Tenant { get; init; }
    public required InboundEnvelope Envelope { get; init; }
    public required DateTimeOffset BridgeReceivedAt { get; init; }
    public required int RetryAttempt { get; init; }   // 0 = first delivery; incremented if node NAKs
}
```

#### Bridge-side implementation

- `MessagingInboundController.cs` — webhook receiver endpoint per provider (Postmark today; SendGrid/Twilio Phase 2.2). Phase 5b handles the 5-layer defense; this controller mints the `EventId` after Layer 1 + 2 pass and enqueues.
- `MessagingInboundQueue.cs` — per-tenant queue. In-memory + SQLite WAL for restart durability. FIFO per tenant. Backpressure: when a node has been offline > 24h and the queue exceeds 1000 envelopes per tenant, oldest envelopes are emitted as `MessageRoutingDelayed` audit + held at Bridge in an `unrouted-inbox` view for operator triage. Configurable per `MessagingProviderConfig.IngressBackpressure`.
- `MessagingInboundSseEndpoint.cs` — SSE handler at `GET /messaging-inbound/{tenantId}?since=<eventId>`. Streams envelopes as they arrive. Resume via `?since=` is mandatory (node reconnects after restart pick up from last-acked EventId).
- `NodeMacaroonValidator.cs` — validates the per-tenant node-bound macaroon per ADR 0032. Macaroon caveats: `tenant = <TenantId>` + `audience = messaging-inbound`. Rejects macaroons whose audience doesn't match.

#### Node-side implementation

- `BridgeRelayedInboundMessageReceiver.cs` (in `blocks-messaging` — node-side; existing `InMemoryMessagingGateway` companion). Connects to Bridge SSE endpoint on startup; resumes via `?since=` from `IDedupStore.LastAckedEventId(tenant)`. For each envelope:
  1. Check `IDedupStore.SeenAsync(envelope.EventId)`. If seen → emit `WebhookEventDeduplicated` audit; ACK to Bridge; skip.
  2. Invoke `IInboundMessageReceiver.ReceiveAsync(envelope.Envelope, ct)`.
  3. On success: `IDedupStore.MarkSeenAsync(envelope.EventId)`; ACK to Bridge (HTTP POST `/messaging-inbound/{tenantId}/ack/{eventId}`).
  4. On transient error: do NOT ack; Bridge retries on next SSE pump.
  5. On permanent error: ack + emit `MessageRoutingFailed` audit.

`IDedupStore` is a new substrate primitive (in `blocks-messaging`). In-memory + SQLite WAL persistence. Retention: 30 days of EventIds (longer than longest realistic retry window). Eviction policy: oldest-first when row count exceeds 100k per tenant.

#### Tests

- Bridge mint+enqueue+SSE-emit round-trip (in-process WebApplicationFactory)
- Macaroon validation: accept valid; reject wrong tenant; reject wrong audience; reject expired
- At-least-once: Bridge re-sends an envelope when no ACK within 10s; node dedupes
- Resume: node disconnects mid-stream; reconnects with `?since=<lastAckedEventId>`; receives missing envelopes only
- Backpressure: simulate offline node for 25h; verify Bridge queues + emits `MessageRoutingDelayed` audit + holds in operator-triage view

#### Gate

- SSE endpoint streams under load (100 envelopes/s sustained for 10s — verifies SQLite WAL doesn't lock)
- At-least-once verified with intentional ACK drops
- Macaroon validator passes all 4 test cases
- Backpressure emits `MessageRoutingDelayed` exactly once per envelope held > 24h

#### Halt conditions (Phase 5a)

- **Bridge's existing SSE infrastructure (if any) uses a different framing convention** that this endpoint would conflict with → `onr-question-*` naming the existing convention; reconcile.
- **`Sunfish.Bridge.Auth.NodeMacaroon` (per ADR 0032) doesn't exist on main yet** — macaroon issuance flow is a sibling workstream → `onr-question-*` naming the gap; stub with shared-secret HMAC if necessary, but ADR 0032 alignment is the long-term path.
- **SQLite WAL on Bridge's data tier has a known concurrency limit** that 100/s envelope throughput would exceed → `onr-question-*`; consider PostgreSQL-backed queue as fallback.

#### Acceptance criteria (Phase 5a)

- [ ] `WebhookEventEnvelope` record + `IDedupStore` interface ship in `foundation-integrations/Messaging/`
- [ ] Bridge `MessagingInboundController` + `MessagingInboundQueue` + `MessagingInboundSseEndpoint` + `NodeMacaroonValidator` ship
- [ ] Node-side `BridgeRelayedInboundMessageReceiver` + `SqliteDedupStore` ship in `blocks-messaging`
- [ ] At-least-once + dedup verified end-to-end in integration test
- [ ] Resume via `?since=<eventId>` works after node restart
- [ ] Backpressure path emits `MessageRoutingDelayed` exactly once per > 24h-held envelope

---

### Phase 5b — Inbound 5-layer defense at Bridge (~2–3h)

**Package:** Same as Phase 5a (Bridge `Features/Messaging/`)
**PR title:** `feat(bridge): Phase 2.1 inbound 5-layer defense + thread routing (ADR 0052 A1 + A4)`
**Council:** **MANDATORY** — security-engineering (public-internet boundary; T2-MSG-INGRESS per ADR 0043)

#### What this phase ships

The 5-layer defense per ADR 0052 amendment A1, executed at Bridge BEFORE enqueueing to the messaging-inbound channel. Order matters — each layer fails fast.

| # | Layer | Implementation | Failure outcome |
|---|---|---|---|
| **0** | **EventId dedup (idempotency)** | `IDedupStore.SeenAsync(eventId)` at Bridge edge | Emit `WebhookEventDeduplicated` audit; return 200 OK; skip remaining layers |
| **1** | **Provider signature verify** | `PostmarkSignatureVerifier.Verify` (Phase 4); per-provider extensible | Return 401; emit `InboundSignatureVerifyFailed`; alert if rate > 0.1% (A5 success criterion) |
| **2** | **Sender allow-list per tenant** | `MessagingProviderConfig.AllowedSenderDomains` + `AllowedFromAddresses`; Phase 2.1 default = empty (accept all but score) | Emit `InboundSenderRejected`; return 200 OK; envelope held in unrouted-inbox |
| **3** | **Rate limit (sliding window)** | 30/hr per sender, 300/hr per tenant; per-tenant overridable | Emit `MessageRateLimitExceeded`; return 200 OK (soft-reject — Postmark would retry on 5xx); envelope dropped |
| **4** | **`IInboundMessageScorer`** | Default `NullScorer` (always 0); pluggable | Emit `InboundContentScored`; envelope continues if score < threshold (default: continue all) |
| **5** | **Thread routing** | `ThreadToken` lookup first; fuzzy sender-recency matching per A4 (Email) or 14-day window (SMS — Phase 2.2) | Match → emit `MessageRouted` + enqueue to SSE channel scoped to matched thread. No match → emit `MessageQueuedForUnroutedTriage` + enqueue to `IUnroutedTriageQueue` |

Layer 0 (EventId dedup) is the new addition — the existing handoff specified 5 layers but didn't name idempotency. ADR 0052 implicitly requires it (webhook providers retry; without dedup, we get duplicate threads).

#### Signature verifier extension point

`IProviderSignatureVerifier` — interface in `foundation-integrations/Messaging/` (already shipped in Phase 1 if present; otherwise ship in this PR). Concrete impls live in `providers-*` packages (`PostmarkSignatureVerifier` in `providers-postmark`). Bridge looks up the verifier by `ProviderKey` (`"postmark"`, `"sendgrid"`, `"twilio"`) on the envelope header.

#### Tests

- Each layer's reject path produces the correct audit event + HTTP status
- Layer 0 dedup: send same EventId twice; second call returns 200 OK with `WebhookEventDeduplicated` audit; envelope NOT enqueued twice
- Signature verify failure rate alerting: simulate 100 webhooks with 1% bad sigs; verify alert threshold triggered exactly once
- Thread routing: token-match wins over fuzzy-match (per A4 tiebreaker rule)
- Unrouted triage path: envelope without token or matching sender enqueues to `IUnroutedTriageQueue`

#### Gate

- All 6 layers' tests pass (layer 0 + layers 1-5)
- A4 fuzzy matching works for the Email path (SMS deferred)
- Signature-verify failure-rate alert fires per A5

#### Halt conditions (Phase 5b)

- **`IUnroutedTriageQueue` interface lacks `EnqueueAsync` overload matching the envelope+context shape** → check Phase 1 contract; if missing, `onr-question-*`.
- **`IInboundMessageScorer.ScoreAsync` semantics ambiguous** (does the substrate accept-with-score, or reject-on-threshold?) → `onr-question-*`; Phase 2.1 ships pass-through but contract must be clear.

#### Acceptance criteria (Phase 5b)

- [ ] All 6 layers ship in order (0 first, then 1→5)
- [ ] Each layer emits the named audit event on reject
- [ ] EventId dedup at layer 0 prevents duplicate enqueue
- [ ] Signature-verify failure-rate alerting works
- [ ] A4 token+fuzzy resolution: token-match wins on tiebreaker
- [ ] Unrouted-triage path queues envelopes for operator review

---

### Phase 5c — Bridge-relayed egress path (~2h)

**Package:** Same as Phase 5a (Bridge `Features/Messaging/`)
**PR title:** `feat(bridge): Phase 2.1 Bridge-relayed egress + node DispatchRequest forwarding (ADR 0052 + OQ-WSE-1)`
**Council:** **MANDATORY** — security-engineering (credential locality — Postmark API key resides at Bridge, never on the node)

#### Why this phase exists

Per OQ-WSE-1: Phase 2.1 default egress route is **BridgeRelayed**. The local node does not hold provider API keys; credentials live at Bridge. The node POSTs a `DispatchRequest` to Bridge over the same dedicated channel (REST POST, not SSE) and Bridge calls Postmark on the node's behalf.

#### Wire shape

```
Node:  PostmarkOutboundMessageGateway   ← Phase 4 impl
   │   .DispatchAsync(OutboundMessage)
   │   If MessagingProviderConfig.EgressRoute == BridgeRelayed:
   │     wrap as DispatchRequest; POST to Bridge
   ▼
Bridge:  MessagingEgressController
   │   POST /messaging-egress/{tenantId}/dispatch
   │   Auth: per-tenant node-bound macaroon (audience = messaging-egress)
   │   Resolves CredentialsReference → Postmark API key (via ADR 0013 CredentialsResolver)
   ▼
Bridge:  invokes the SAME PostmarkOutboundMessageGateway with credentials injected
   │   (the gateway is credential-agnostic by design — accepts an HttpClient with auth pre-applied)
   ▼
Postmark API
   │   200 OK + MessageID
   ▼
Bridge returns OutboundDispatchHandle to node
```

Node sees `OutboundDispatchHandle` as if it dispatched locally; transparent.

#### Implementation

- `MessagingEgressController.cs` — Bridge endpoint. Accepts `DispatchRequest` (envelope = `OutboundMessage` + `ProviderKey`). Validates macaroon (audience = `messaging-egress`). Resolves credentials. Invokes the in-Bridge `PostmarkOutboundMessageGateway` instance (registered at Bridge startup; not the node's instance). Returns the handle.
- `DispatchRequest` / `DispatchResponse` records in `foundation-integrations/Messaging/`. Carry the full `OutboundMessage` + provider key + correlation ID.
- Node-side wrapper: `BridgeRelayedOutboundMessageGateway.cs` in `blocks-messaging`. Implements `IOutboundMessageGateway`; for each call, wraps and POSTs to Bridge. DI registration switches between `BridgeRelayed` and `NodeDirect` based on `MessagingProviderConfig.EgressRoute`.

#### Audit emission

Both sides emit:

- Node: `BridgeRelayRequestSent` (with correlation ID)
- Bridge: `BridgeRelayRequestReceived` + `MessageDispatched` (terminal — same correlation ID)
- Failure cases: `BridgeRelayRequestFailed` (transport-level) vs. `MessageDispatched` failure variants (provider-level)

#### Tests

- Round-trip dispatch: node calls `IOutboundMessageGateway.DispatchAsync`; Postmark stub at Bridge returns 200; node receives handle
- Credential injection: Bridge resolves `CredentialsReference` → real key; verifies HTTP authorization header on outbound Postmark call (WireMock.NET capture)
- Macaroon audience mismatch: node calls with `messaging-inbound` audience → 403; emits `BridgeRelayRequestFailed`
- Node-direct opt-in: `EgressRoute = NodeDirect`; DI registers `PostmarkOutboundMessageGateway` directly; verifies no Bridge call (sovereign-hosting path)

#### Gate

- Round-trip dispatch works end-to-end
- Credentials never appear in node-side logs or audit records
- Macaroon audience enforced

#### Halt conditions (Phase 5c)

- **`ICredentialsResolver` (per ADR 0013) signature changed** since Phase 4 → reconcile.
- **Bridge's existing per-tenant credential storage** doesn't have a Postmark-shaped slot → may need a config-schema migration; `onr-question-*` to scope.

#### Acceptance criteria (Phase 5c)

- [ ] `DispatchRequest` + `DispatchResponse` records shipped
- [ ] Bridge `MessagingEgressController` validates macaroon + resolves creds + dispatches
- [ ] Node-side `BridgeRelayedOutboundMessageGateway` implements `IOutboundMessageGateway`
- [ ] DI registration switches on `MessagingProviderConfig.EgressRoute`
- [ ] Credential never crosses node boundary (verified by negative test)

---

### Phase 6 — Audit emission (~1.5h)

**Package:** `packages/kernel-audit/` (existing — extend with new `AuditEventType` constants)
**PR title:** `feat(kernel-audit): Phase 2.1 messaging — 17 AuditEventType + MessagingAuditPayloadFactory (ADR 0049 + ADR 0052)`
**Council:** advisory only (additive surface; no breaking change)

#### Constants to add

Under a new divider in `AuditEventType.cs`:

```csharp
// ===== ADR 0052 — Bidirectional Messaging =====
public static readonly AuditEventType MessageDispatched              = new("MessageDispatched");
public static readonly AuditEventType MessageDispatchAttempt         = new("MessageDispatchAttempt");        // NEW per Phase 4 retry visibility
public static readonly AuditEventType MessageDeliveryStatusChanged   = new("MessageDeliveryStatusChanged");
public static readonly AuditEventType MessageOpened                  = new("MessageOpened");
public static readonly AuditEventType MessageClicked                 = new("MessageClicked");
public static readonly AuditEventType MessageReceived                = new("MessageReceived");
public static readonly AuditEventType MessageRouted                  = new("MessageRouted");
public static readonly AuditEventType MessageRoutedAmbiguous         = new("MessageRoutedAmbiguous");        // SMS multiple-thread match (A4)
public static readonly AuditEventType MessageRoutingDelayed          = new("MessageRoutingDelayed");        // NEW — node offline > 24h, Bridge backpressure
public static readonly AuditEventType MessageRoutingFailed           = new("MessageRoutingFailed");        // NEW — permanent failure post-Layer-5
public static readonly AuditEventType MessageQueuedForUnroutedTriage = new("MessageQueuedForUnroutedTriage");
public static readonly AuditEventType MessageRateLimitExceeded       = new("MessageRateLimitExceeded");
public static readonly AuditEventType InboundSignatureVerifyFailed   = new("InboundSignatureVerifyFailed");
public static readonly AuditEventType InboundSenderRejected          = new("InboundSenderRejected");
public static readonly AuditEventType WebhookEventDeduplicated       = new("WebhookEventDeduplicated");    // NEW — Layer 0 dedup
public static readonly AuditEventType BridgeRelayRequestSent         = new("BridgeRelayRequestSent");      // NEW — egress relay node-side
public static readonly AuditEventType BridgeRelayRequestReceived     = new("BridgeRelayRequestReceived");  // NEW — egress relay Bridge-side
public static readonly AuditEventType BridgeRelayRequestFailed       = new("BridgeRelayRequestFailed");    // NEW — egress relay transport failure
public static readonly AuditEventType ThreadCreated                  = new("ThreadCreated");
public static readonly AuditEventType ThreadSplit                    = new("ThreadSplit");                 // per Minor amendment
public static readonly AuditEventType ThreadClosed                   = new("ThreadClosed");
public static readonly AuditEventType ThreadTokenRevoked             = new("ThreadTokenRevoked");
public static readonly AuditEventType MessagingAdapterStartupFailed  = new("MessagingAdapterStartupFailed"); // NEW — fail-closed startup
public static readonly AuditEventType DeliverabilityRiskElevated     = new("DeliverabilityRiskElevated");  // NEW — DKIM/SPF/DMARC degraded
```

23 total (was 12 in the original hand-off; +11 new entries reflecting Phase 5a/5b/5c additions + per-attempt retry visibility + fail-closed startup).

#### `MessagingAuditPayloadFactory`

Mirror the W#31 + W#19 factory pattern. One static factory method per event type. Each carries:

- `TenantId Tenant`
- `Guid CorrelationId` (from `OutboundMessage.Audit.CorrelationId` or freshly minted)
- `ThreadId? Thread` (where applicable)
- `MessageId? Message` (where applicable)
- Redacted projection of sensitive content per ADR 0052 §"Trust impact" (subject + sender + recipient + metadata — NOT body)
- Vendor detail string for `MessageDispatched` failures + `MessageDispatchAttempt` (preserves Postmark error code/message for forensics; never PII)

#### Tests

- One unit test per `AuditEventType` verifying payload shape (23 tests)
- One redaction-verification test per payload kind (PII never leaks into payload)
- One correlation-chain test (multi-attempt retry produces N `MessageDispatchAttempt` + 1 terminal `MessageDispatched` sharing same `CorrelationId`)

#### Gate

- 23 `AuditEventType` constants ship
- Factory has one method per event type
- All 23 payload tests pass
- Redaction verified

#### Halt conditions (Phase 6)

- **`AuditEventType` registration conflict** with an existing event name → namespace via "Messaging." prefix if needed; `onr-question-*` if not obvious.
- **Audit payload contract changed** (ADR 0049 has been revised mid-flight) → reconcile or pause.

#### Acceptance criteria (Phase 6)

- [ ] 23 new `AuditEventType` constants ship
- [ ] `MessagingAuditPayloadFactory` covers all 23
- [ ] Redaction verified — no message body in any payload
- [ ] Correlation chain verified across retry attempts

---

### Phase 7 — Cross-package integration tests (~1.5h)

**Package:** `packages/blocks-messaging/tests/` (new integration-test subproject)
**PR title:** `test(blocks-messaging): Phase 2.1 end-to-end integration suite`
**Council:** advisory only

End-to-end scenarios with all of Phase 4 + 5a + 5b + 5c + 6 wired:

1. **Outbound happy path:** node calls `IOutboundMessageGateway.DispatchAsync(workOrderAssignment)` → Bridge-relayed → Postmark stub → 200 OK + MessageID → handle returned → `MessageDispatched` + `BridgeRelayRequestSent` + `BridgeRelayRequestReceived` audit chain
2. **Inbound happy path:** Postmark inbound webhook → Bridge Layer 0-5 pass → SSE channel → node `IInboundMessageReceiver` → thread match → `MessageReceived` + `MessageRouted` audit chain
3. **Retry visibility:** Postmark stub returns 500 twice, then 200 → 2 `MessageDispatchAttempt` + 1 terminal `MessageDispatched`, all correlated
4. **Duplicate webhook:** Postmark resends same webhook → second arrival deduped at Layer 0 → `WebhookEventDeduplicated` audit; node never invoked twice
5. **Compliance reject:** outbound to a recipient on the consent-revoked list → `OutboundDispatchFailure.ComplianceGateRejected` returned to caller; `MessageDispatchAttempt` with failure variant on audit chain
6. **Node-offline backpressure:** Bridge holds inbound envelopes for 25h while node is offline → `MessageRoutingDelayed` emitted; envelopes resume to node on reconnect (resume via `?since=`)
7. **Cross-package smoke for W#19:** `IThreadStore.SplitAsync` callable from Work Orders Phase 6 (already verified via stub; re-verify against this hand-off's wiring)

#### Gate

- All 7 scenarios pass
- No flakiness over 100-iteration loop (Bridge + node WebApplicationFactory)

#### Halt conditions (Phase 7)

- **WebApplicationFactory two-host scenario (Bridge + node)** can't share SQLite-WAL state cleanly → `onr-question-*`; consider in-memory SQLite for tests.

#### Acceptance criteria (Phase 7)

- [ ] All 7 scenarios pass
- [ ] 100-iteration stability check passes

---

### Phase 8 — apps/docs (~1.5h)

**Package:** `apps/docs/` (existing — add new pages)
**PR title:** `docs(blocks-messaging): Phase 2.1 substrate documentation`
**Council:** advisory only

Four documentation pages with the following outlines:

#### Page 1 — `apps/docs/blocks/messaging/overview.md`

Substrate overview for application developers consuming the messaging substrate.

```markdown
# Bidirectional Messaging Substrate — overview

## What this substrate provides
- Outbound + inbound durable messaging (Email Phase 2.1; SMS Phase 2.2)
- Thread-based conversation model with 3-tier visibility
- Audit-substrate-integrated event emission
- Per-tenant provider config + credential isolation
- Cryptographically-bound thread routing via ThreadToken (HMAC-SHA256)

## Quick start (consumer)
1. Reference `Sunfish.Foundation.Integrations.Messaging` and (if using in-process default) `Sunfish.Blocks.Messaging`
2. DI: `services.AddInMemoryMessaging()` for tests; for production: `services.AddPostmarkMessagingProvider(config)`
3. Inject `IOutboundMessageGateway`; call `DispatchAsync(OutboundMessage)`
4. Pull `OutboundDispatchStatus` via `GetStatusAsync(handle)` or react to delivery events via audit subscription

## Three-tier visibility model
- Public: all thread participants can read
- PartyPair: enforced via participant-set membership (2-party thread)
- OperatorOnly: substrate-emitted system messages

## ThreadToken usage
- Substrate mints opaque, tenant-scoped, HMAC-bound tokens
- Tokens round-trip via Reply-To header (email) or [Ref:...] body marker (SMS, Phase 2.2)
- 90-day TTL; revocable via `IThreadTokenIssuer.RevokeAsync`
- ALWAYS use `IThreadTokenIssuer.Verify` — never compare token strings directly

## Failure-mode taxonomy
[Table: 8 OutboundDispatchFailure variants → when each fires → consumer-side handling]

## Audit event vocabulary
[Table: 23 AuditEventType constants → meaning → when emitted]

## Cross-references
- ADR 0052 (substrate spec)
- ADR 0013 (provider-neutrality + adapter pattern)
- ADR 0049 (audit substrate this emits to)
```

#### Page 2 — `apps/docs/foundation-integrations/messaging.md`

Contract surface reference (API doc).

```markdown
# Sunfish.Foundation.Integrations.Messaging — contract reference

## Egress
### IOutboundMessageGateway
[XML doc + signature + behavior contract for DispatchAsync + GetStatusAsync]
[Code example: minimal dispatch]

### OutboundMessage record
[Required fields; field semantics; per-field validation rules]

### OutboundDispatchHandle + OutboundDispatchStatus
[State diagram: Queued → Sent → Delivered/Bounced/Complained/Opened/Clicked]

### OutboundDispatchFailure (discriminated union)
[8 variants documented; mapping table from provider errors → variant]

## Ingress
### IInboundMessageReceiver
[XML doc; idempotency contract; consumer responsibilities]

### InboundEnvelope + InboundReceiveOutcome
[Field semantics; 4 outcome states]

### WebhookEventEnvelope (NEW — Phase 5a)
[EventId + at-least-once + dedup contract]

## Threading
### IThreadStore
[Create / Get / Split / Append semantics]

### IThreadTokenIssuer (per A2)
[Mint / Verify / Revoke; cryptographic properties; key-rotation cascade]

## Defense extensibility
### IInboundMessageScorer (per A1)
[Plugin interface; default NullScorer; threshold semantics]

### IUnroutedTriageQueue
[Enqueue / List / Resolve]

## Configuration
### MessagingProviderConfig
[Per-tenant config schema; SenderIsolationMode enum; EgressRoute enum; ComplianceOverrides]
```

#### Page 3 — `apps/docs/providers/postmark.md`

Postmark adapter operator + developer reference.

```markdown
# providers-postmark — operator + developer reference

## Configuration (per tenant)
[Required fields; how to obtain a Postmark server token; sender_domain DNS setup]

## Compliance gates this adapter enforces
[Substrate-time fail-closed list with operator override flags]

## Error mapping
[Table: Postmark error code → OutboundDispatchFailure variant → consumer-side action]

## Resilience pipeline
- Retry: exponential backoff, max 3 attempts
- Circuit breaker: 0.5 failure ratio over 30s
- Timeout: per-attempt 10s; overall 30s
- Per-attempt audit emission (MessageDispatchAttempt)

## Webhook signature verification
[How to obtain webhook signing secret; how Bridge verifies; what failure looks like]

## BannedSymbols (analyzer allow-list)
[List of Postmark types reachable from consumer code; what's forbidden + why]

## Live-vendor testing
[Opt-in via SUNFISH_MESSAGING_LIVE_TESTS=1; sandbox-token setup; CI exclusion]

## Operational runbook
[What "Postmark down" looks like; how to fail open vs. fail closed; ops alerts]
```

#### Page 4 — `apps/docs/bridge/messaging-inbound.md`

Bridge SSE channel reference (operator + node-developer audience).

```markdown
# Bridge messaging-inbound SSE channel

## Why this channel exists
[Blast-radius isolation from sync-daemon per OQ-WSE-2]

## Wire protocol
[GET /messaging-inbound/{tenantId}?since=<eventId>; SSE event format; ack via POST]

## At-least-once delivery + dedup
[Bridge retries; node dedupes via WebhookEventEnvelope.EventId; IDedupStore retention]

## Authentication
[Per-tenant node-bound macaroon per ADR 0032; audience = messaging-inbound]

## Backpressure behavior
[Node offline > 24h → MessageRoutingDelayed audit → Bridge holds in unrouted-inbox]

## Resume semantics
[?since=<lastAckedEventId>; node restart recovery]

## Operational runbook
[Operator-triage of unrouted-inbox; bulk-route + bulk-reject operations]
```

#### Kitchen-sink seed page (TODO if Yeoman block-into-kitchen-sink wiring isn't ready)

`apps/kitchen-sink/messaging-demo.md` — page demonstrating outbound vendor work-order assignment → vendor email reply → thread routing → audit emission. Flag as TODO if the Yeoman-side wiring scaffold isn't ready. Do NOT block Phase 8 on this — note the gap; route to Yeoman via `onr-question-*`.

#### Gate

- All 4 docs pages build cleanly in `apps/docs`
- No broken cross-references
- All code examples in pages compile against the as-shipped contract surface

#### Halt conditions (Phase 8)

- **`apps/docs` build pipeline** has changed since the last Stage-06 ship → reconcile.
- **Yeoman block-into-kitchen-sink wiring** not present → TODO + onr-question to PAO/Yeoman; do not block Phase 8 ship.

#### Acceptance criteria (Phase 8)

- [ ] 4 docs pages ship
- [ ] All cross-references resolve
- [ ] All code examples compile
- [ ] Kitchen-sink TODO documented if wiring isn't ready

---

### Phase 9 — Ledger flip (~0.5h)

**PR title:** `chore(icm): flip W#20 ledger row → built (Phases 0–9 complete)`

Update `icm/_state/workstreams/W20-bidirectional-messaging-substrate.md`:

- `status: built`
- `status_cell`: `built (Phases 0–9 complete; Phase 2.2 SendGrid + Twilio queued; Phase 2.3 per-tenant DKIM/Frappe-bidirectional deferred)`
- Append merged PR list to `## Notes`
- Append entry to `icm/_state/active-workstreams.md`

#### Gate

- Ledger row flipped
- `active-workstreams.md` summary regenerated

#### Acceptance criteria (Phase 9)

- [ ] W#20 row → `built`
- [ ] PR list complete in notes

---

## Total decomposition (revised)

| Phase | Subject | Hours | PR | Status |
|---|---|---|---|---|
| 0 | `ITenantKeyProvider` stub | 0.5 | #294 | ✅ |
| 1 | `foundation-integrations` Messaging contracts | 2–3 | #273 | ✅ |
| 2 | `blocks-messaging` substrate scaffold | 4–6 | #276 | ✅ |
| 3 | `HmacThreadTokenIssuer` per A2 | 1–2 | #302 | ✅ |
| 4 | `providers-postmark` adapter + `OutboundDispatchFailure` union | 4–5 | new | pending |
| 5a | Bridge `messaging-inbound` SSE channel + dedup | 2–3 | new | pending |
| 5b | Inbound 6-layer defense at Bridge (0 + 1–5) | 2–3 | new | pending |
| 5c | Bridge-relayed egress path | 2 | new | pending |
| 6 | Audit emission — 23 `AuditEventType` | 1.5 | new | pending |
| 7 | Cross-package integration tests | 1.5 | new | pending |
| 8 | apps/docs (4 pages) | 1.5 | new | pending |
| 9 | Ledger flip | 0.5 | new | pending |
| **Remaining total** | | **~14–18.5h** | **7–8 PRs** | |

---

## Global halt conditions

In addition to per-phase halts, file `onr-question-*` (or `engineer-question-*` per Engineer's beacon prefix) if any of these surface at any phase:

- **ADR 0052 amendments revised mid-flight** (an A6+ amendment lands while this hand-off is in flight) → pause; reconcile to the revised contract.
- **`Sunfish.Foundation.Integrations` namespace stability broken** by another workstream (W#14 enforcement-gate evolution, W#15 secrets management) → coordinate.
- **Postmark vendor incident or pricing change** mid-flight that invalidates the vendor selection from the prior ONR research → flag to Admiral; vendor swap is contract-clean (`IOutboundMessageGateway` is provider-neutral) but operationally non-trivial.
- **Provider-neutrality analyzer (`SUNFISH_PROVNEUT_001`) fires on a legitimate symbol** inside a `providers-*` package → maintainer needs to update the allow-list; do not work-around by suppressing.

---

## Acceptance criteria (cumulative — Phases 4–9)

- [ ] `providers-postmark` ships with all 8 `OutboundDispatchFailure` mappings + resilience pipeline + signature verifier
- [ ] `OutboundDispatchFailure` discriminated union (8 variants) shipped in `foundation-integrations/Messaging/`
- [ ] Vendor-feature exclusion list encoded in `BannedSymbols.txt`; analyzer pass
- [ ] Bridge `messaging-inbound` SSE channel with at-least-once + EventId dedup + resume
- [ ] `WebhookEventEnvelope` + `IDedupStore` + `SqliteDedupStore` ship
- [ ] 6-layer inbound defense (Layer 0 dedup + Layers 1–5) verified
- [ ] Bridge-relayed egress path: credentials never cross node boundary
- [ ] Macaroon audience enforcement (messaging-inbound vs. messaging-egress)
- [ ] 23 new `AuditEventType` constants + `MessagingAuditPayloadFactory`
- [ ] Redaction verified — no message body in any audit payload
- [ ] Correlation chain across multi-attempt retries
- [ ] End-to-end integration test suite (7 scenarios; 100-iteration stability)
- [ ] 4 docs pages ship + cross-references resolve
- [ ] W#20 ledger row → `built`

---

## Beacon protocol

- File `onr-status-*-ws-e-handoff-complete.md` when this hand-off is itself complete (i.e., this revision is on file). **Done as part of this directive's deliverable.**
- File `engineer-question-*` if any halt condition fires during build.
- File `engineer-status-*` per phase when each PR is open + ready for council/review.
- File `engineer-status-*-w20-built.md` when Phase 9 flips the ledger.

---

## References

- [ADR 0052 — Bidirectional Messaging Substrate](../../../docs/adrs/0052-bidirectional-messaging-substrate.md) — substrate spec + amendments A1–A5 + Minor
- [Council review](../../07_review/output/adr-audits/0052-council-review-2026-04-29.md) — surfaced the 5 amendments
- [Cluster intake](../../00_intake/output/property-messaging-substrate-intake-2026-04-28.md) — original scope
- [ONR prereq scoping](../../01_discovery/research/onr-wse-handoff-prereq-2026-05-17.md) — 12 OQs answered; this hand-off freezes them
- [Stage-06 hand-off addendum](./property-messaging-substrate-stage06-addendum.md) — Phase 0 stub spec
- [W#19 Work Orders hand-off](./property-work-orders-stage06-handoff.md) — Phase 6 consumes `IThreadStore.SplitAsync` from W#20 (resolved via stub)
- [W#31 Foundation.Taxonomy](./foundation-taxonomy-phase1-stage06-handoff.md) — AuditEventType + payload-factory cardinality pattern this hand-off mirrors
- [Structural reference handoff](./blocks-financial-payments-stage06-handoff.md) — canonical Stage-06 shape this hand-off follows
- ADR 0008 (multi-tenancy), 0013 (provider-neutrality), 0015 (entity-module), 0032 (macaroons), 0043 (T2 ingress threat tier per A1), 0046 (Foundation.Recovery for per-tenant keys per A2), 0049 (audit substrate)
- [Postmark Inbound webhook reference](https://postmarkapp.com/developer/user-guide/inbound)
- [Postmark.NET SDK](https://github.com/ActiveCampaign/postmark-dotnet) (vendor; isolated to `providers-postmark`)
- [WireMock.NET](https://github.com/WireMock-Net/WireMock.Net) (test substrate per OQ-WSE-7)

---

*Hand-off revised 2026-05-17 by ONR per Admiral directive 2026-05-17T23-15Z. Phases 0–3 shipped; Phases 4–9 are buildable as specified. Engineer picks up Phase 4 first (council mandatory); subsequent phases land per the decomposition table.*
