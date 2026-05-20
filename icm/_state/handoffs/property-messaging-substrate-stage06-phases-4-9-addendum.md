# Workstream #20 — Bidirectional Messaging Substrate — Stage 06 Phases 4-9 Addendum

**Workstream:** #20 (Bidirectional Messaging Substrate)
**Companion to:** `property-messaging-substrate-stage06-handoff.md` (Phases 1-3 foundational substrate; 363 lines)
**Spec:** [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) (Accepted 2026-04-29; A1–A5 + Minor landed)
**Authored by:** ONR (re-authoring; Track A reconstruction)
**Authored at:** 2026-05-20T12-05Z
**Status:** Active (Phase 0-3 shipped per status update below; Phase 4-9 design ready-to-build)

---

## Re-authoring context

The original Track A hand-off was authored as an "in-place revision" of `property-messaging-substrate-stage06-handoff.md` at 2026-05-18T01:48Z (987 lines / 57KB per `onr-status-2026-05-18T01-48Z-ws-e-handoff-complete.md`). That revision was never committed to git and was lost to a gitbutler virtual-branch switch (the missing-artifact pattern Admiral flagged in `admiral-status-2026-05-19T02-30Z`).

This addendum reconstructs the Phase 4-9 deep design content per:
- The prior status beacon's structural summary (PR refs, phase splits, audit-event additions)
- The prereq scoping doc at `shipyard/icm/01_discovery/research/onr-wse-handoff-prereq-2026-05-17.md` (OQ-WSE-1 through OQ-WSE-12)
- ADR 0052 amendments A1-A5 + Minor

The original 363-line `property-messaging-substrate-stage06-handoff.md` remains canonical for Phase 1-3 substrate. This addendum layers Phase 4-9 detail without overwriting the canonical file.

---

## 1. Phase status update (Phases 0-3 shipped)

Per prior Track A status (2026-05-18T01:48Z):

| Phase | Subject | PR | Status |
|---|---|---|---|
| 0 | Workstream registration + ICM scaffolding | #294 | ✅ SHIPPED |
| 1 | Contracts in `foundation-integrations/Messaging/` | #273 | ✅ SHIPPED |
| 2 | `blocks-messaging` entities + InMemory implementations | #276 | ✅ SHIPPED |
| 3 | `HmacThreadTokenIssuer` + per-tenant key resolution | #302 | ✅ SHIPPED |

**Verification at re-authoring (2026-05-20):** `shipyard/packages/blocks-messaging/` exists with `Models/`, `Services/`, `Data/`, `DependencyInjection/`, `tests/`, `README.md` per `ls` 2026-05-20T12:00Z. `providers-postmark` package NOT yet present (only `providers-mesh-headscale` + `providers-recaptcha` per `ls shipyard/packages/`), confirming Phase 4 work has not landed.

**Phases 4-9 (this addendum's scope):**

| Phase | Subject | Estimate | Status |
|---|---|---|---|
| 4 | `providers-postmark` first email adapter | 3–4h | ready-to-build |
| 5a | Bridge ↔ node `messaging-inbound` SSE channel | 2–3h | ready-to-build (gated on `Sunfish.Bridge.Auth.NodeMacaroon`; halt H-5a) |
| 5b | 6-layer inbound defense (Layer 0 EventId dedup + 5 layers from A1) | 2–3h | ready-to-build |
| 5c | Bridge-relayed egress (default; sovereign-hosting opt-in for node-direct) | 2h | ready-to-build (gated on `ICredentialsResolver`; halt H-5c) |
| 6 | Audit emission (23 `AuditEventType` constants — expanded from initial 12) | 1–2h | ready-to-build |
| 7 | Cross-package integration tests | 1h | ready-to-build |
| 8 | `apps/docs/` — 4 pages | 1h | ready-to-build |
| 9 | Ledger flip W#20 → built | 0.5h | ready-to-build |
| **Total Phase 4-9** | | **12–16h** | |

---

## 2. Phase 4 — `providers-postmark` first email adapter (deep design)

**Location:** new package `shipyard/packages/providers-postmark/Sunfish.Providers.Postmark.csproj`
**Estimate:** 3-4h
**Council:** MANDATORY (security-engineering + .NET-architect)

### 2.1 `OutboundDispatchFailure` discriminated union (FROZEN per OQ-WSE-12)

ADR 0052's `OutboundDispatchStatus` names `Failed` as terminal but doesn't enumerate causes. Phase 4 introduces an 8-variant discriminated union:

```csharp
namespace Sunfish.Foundation.Integrations.Messaging;

public abstract record OutboundDispatchFailure
{
    public sealed record ProviderAuthRejected(string ProviderKey, string ErrorCode) : OutboundDispatchFailure;
    public sealed record ProviderRateLimited(string ProviderKey, TimeSpan? RetryAfter) : OutboundDispatchFailure;
    public sealed record TenantConfigMissing(TenantId Tenant, string MissingField) : OutboundDispatchFailure;
    public sealed record ContentRejected(string Reason, string? ProviderErrorCode) : OutboundDispatchFailure;
    public sealed record RecipientBlocked(MessageRecipient Recipient, string Reason) : OutboundDispatchFailure;
    public sealed record PermanentBounce(MessageRecipient Recipient, string BounceCode, string Diagnostic) : OutboundDispatchFailure;
    public sealed record TransientNetworkError(string DiagnosticMessage, Exception? Underlying = null) : OutboundDispatchFailure;
    public sealed record ComplianceGateRejected(string GateName, string Reason) : OutboundDispatchFailure;
}
```

**Retry-policy mapping per failure variant:**

| Variant | Adapter resilience pipeline retry? | Surface to caller? | Audit emission |
|---|---|---|---|
| `ProviderAuthRejected` | NO (operator action required) | YES (terminal) | `MessageDispatchFailed` (immediate) |
| `ProviderRateLimited` | YES (3 attempts; respect `RetryAfter`) | If retries exhausted, terminal | `MessageDispatchAttempt` per retry |
| `TenantConfigMissing` | NO | YES (terminal at adapter init) | `MessagingAdapterStartupFailed` |
| `ContentRejected` | NO | YES (terminal) | `MessageDispatchFailed` |
| `RecipientBlocked` | NO | YES (terminal per-recipient) | `MessageDispatchFailed` |
| `PermanentBounce` | NO | YES (terminal) | `MessageDispatchFailed` (via webhook) |
| `TransientNetworkError` | YES (3 attempts + 30s + circuit-break at 0.5 failure rate) | If retries exhausted, terminal | `MessageDispatchAttempt` per retry |
| `ComplianceGateRejected` | NO | YES (terminal; abort) | `MessageDispatchFailed` (with gate name in payload) |

**Resilience pipeline (per OQ-WSE-1 retry-policy ownership boundary):**

```csharp
// Adapter-owned via Microsoft.Extensions.Http.Resilience
services.AddHttpClient<PostmarkClient>()
    .AddResilienceHandler("postmark-egress", builder => builder
        .AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests
                                || r.StatusCode == HttpStatusCode.InternalServerError
                                || r.StatusCode == HttpStatusCode.BadGateway
                                || r.StatusCode == HttpStatusCode.ServiceUnavailable
                                || r.StatusCode == HttpStatusCode.GatewayTimeout)
                .Handle<HttpRequestException>(),
        })
        .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30),
            SamplingDuration = TimeSpan.FromSeconds(30),
        })
        .AddTimeout(TimeSpan.FromSeconds(30)));  // Email per OQ-WSE-1; SMS=10s, Voice=60s
```

### 2.2 Postmark error → variant mapping table

| Postmark error code | HTTP status | Maps to variant |
|---|---|---|
| `10` Bad or missing API token | 401 | `ProviderAuthRejected("postmark", "10")` |
| `300` Invalid email request | 422 | `ContentRejected(Postmark message, "300")` |
| `406` You don't have a Sender Signature | 422 | `TenantConfigMissing(tenant, "sender_signature")` |
| `412` This recipient is on the inactive list | 422 | `RecipientBlocked(recipient, "inactive-list")` |
| `429` Rate limit exceeded | 429 | `ProviderRateLimited("postmark", retryAfter from header)` |
| `500/502/503/504` Server errors | 5xx | `TransientNetworkError` (resilience pipeline retries) |
| `HardBounce` (via webhook) | webhook | `PermanentBounce(recipient, "HardBounce", diagnostic)` |
| `SpamComplaint` (via webhook) | webhook | `RecipientBlocked(recipient, "spam-complaint")` |

### 2.3 Per-attempt audit emission

Each resilience-pipeline attempt emits `MessageDispatchAttempt` (NEW Phase 6 audit event); the terminal outcome emits `MessageDispatched` (success) or `MessageDispatchFailed` (terminal failure). This gives operators per-retry visibility per OQ-WSE-1 sub-question (a).

### 2.4 Vendor-feature exclusion list — `BannedSymbols.txt`

ADR 0013 §"Domain concepts are Sunfish-modeled, not vendor-mirrored" forbids vendor-feature passthroughs. Phase 4 explicitly excludes 5 Postmark features:

```
# providers-postmark BannedSymbols.txt
# Per ADR 0013 + WS-E hand-off Phase 4

# Vendor template engines (consumers render content upstream)
PostmarkClient.SendEmailWithTemplate
PostmarkClient.SendEmailBatchWithTemplates
PostmarkClient.GetTemplate
PostmarkClient.GetTemplates
TemplatedPostmarkMessage

# Vendor scheduling (Sunfish-side IClock-driven scheduling only)
PostmarkMessage.DeliveryStartAt

# Vendor suppression lists (Sunfish owns consent state per ADR 0052 A5)
PostmarkClient.GetSuppressions
PostmarkClient.CreateSuppressions
PostmarkClient.DeleteSuppressions

# Vendor analytics dashboards (observability via Sunfish telemetry, not vendor consoles)
PostmarkClient.GetMessageOpens
PostmarkClient.GetMessageClicks
PostmarkClient.GetEmailStats
```

The `SUNFISH_PROVNEUT_001` Roslyn analyzer enforces this at build time per ADR 0013's enforcement gate (active since W#14 PR #196).

### 2.5 Fail-closed compliance startup posture (OQ-WSE-5/6)

At adapter init (DI registration), `providers-postmark` validates:

```csharp
public static IServiceCollection AddPostmarkProvider(this IServiceCollection services, IConfiguration config)
{
    // ...
    services.AddSingleton<IHostedService, PostmarkProviderStartupValidator>();
    return services;
}

internal sealed class PostmarkProviderStartupValidator : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Validate per-tenant MessagingProviderConfig.email entries:
        // - credentials_ref resolvable via ICredentialsResolver?
        // - sender_domain present + non-empty?
        // - CAN-SPAM physical-address footer template present?
        // - List-Unsubscribe header template present?
        // If any check fails, throw + emit MessagingAdapterStartupFailed audit event.
        // App fails to start — fail-closed at startup per OQ-WSE-5/6.
    }
}
```

This is the canonical fail-closed-at-startup pattern. Operators MUST configure these before the app boots; runtime drift (e.g., credentials revoked) emits `MessageDispatchFailed` per-send and is visible via audit substrate.

### 2.6 Tests (≥10 new)

- `PostmarkOutboundGateway_AuthRejected_ReturnsProviderAuthRejected`
- `PostmarkOutboundGateway_RateLimited_RetriesUpToThreeTimes_RespectsRetryAfter`
- `PostmarkOutboundGateway_TransientNetworkError_CircuitBreakerOpens_After50PercentFailures`
- `PostmarkOutboundGateway_HardBounce_EmitsPermanentBounceVariant`
- `PostmarkOutboundGateway_StartupValidator_MissingCredentials_Throws`
- `PostmarkOutboundGateway_StartupValidator_MissingCanSpamFooter_Throws`
- `PostmarkOutboundGateway_VendorFeatureUsage_BannedSymbolsAnalyzerFails` (compile-time test)
- `PostmarkOutboundGateway_PerAttemptAuditEmission_RecordsEachRetry`
- `PostmarkOutboundGateway_ComplianceGateRejected_AbortsBeforeDispatch`
- `PostmarkOutboundGateway_SuccessfulDispatch_EmitsMessageDispatched`

---

## 3. Phase 5 — Bridge ↔ node messaging-inbound channel + Bridge-relayed egress

Phase 5 was split into 5a / 5b / 5c per OQ-WSE-2 (blast-radius isolation) + OQ-WSE-1 (egress route default).

### 3.1 Phase 5a — Dedicated `messaging-inbound` SSE channel (2-3h)

**Location:** `signal-bridge/Sunfish.Bridge/Messaging/` + `sunfish/apps/desktop/src-tauri/src/messaging/`
**Council:** advisory only (transport layer; no novel security surface — auth is per-tenant macaroon)
**Halt:** H-5a — gated on `Sunfish.Bridge.Auth.NodeMacaroon` (ADR 0032). If macaroon issuance not yet on main, Phase 5a stubs with shared-secret HMAC and files `engineer-question-*` for the long-term macaroon path.

#### Transport choice: Server-Sent Events (SSE)

Per OQ-WSE-2 transport sub-question: SSE over WebSocket because:
- One-way push (Bridge → node) — SSE's natural fit
- Simple resume via `Last-Event-ID` header + `?since=<eventId>` query
- No client-to-server framing complexity
- HTTP/1.1 + HTTP/2 compatible; no WebSocket-proxy quirks

Channel endpoint: `GET /api/v1/messaging-inbound/stream?since=<eventId>` (per-tenant node-bound macaroon auth)

#### Auth: per-tenant node-bound macaroon (ADR 0032)

```csharp
// Macaroon caveats per-tenant + per-node:
// - audience = "messaging-inbound"
// - tenant = <TenantId>
// - node_id = <NodeId>  // bound to this specific node install
// - issued_at = <UTC>
// - expires_at = issued_at + 30 days
```

Macaroon presented as `Authorization: Macaroon <base64-token>` header. Bridge verifies + extracts tenant + node_id + audience; rejects mismatched audience or expired tokens.

#### Backpressure shape

Per OQ-WSE-2 backpressure sub-question:

- Bridge enqueues inbound envelopes locally (per-tenant FIFO) when the node is offline.
- After 24h node-offline, Bridge emits `MessageRoutingDelayed` audit event per envelope.
- After 7 days, Bridge surfaces in the unrouted-inbox triage view (operator-action).
- Queue cap per tenant: 10,000 envelopes (configurable); overflow → drop oldest + emit `MessageRoutingFailed` audit.

#### At-least-once + EventId dedup

`WebhookEventEnvelope.EventId` is GUID-typed; node-side `IDedupStore` keyed on EventId with 30-day retention. Duplicate delivery → `WebhookEventDeduplicated` audit event (Layer 0 of Phase 5b's defense).

### 3.2 Phase 5b — 6-layer inbound defense (Layer 0 EventId dedup + 5 layers from A1)

**Council:** MANDATORY (security-engineering — T2-MSG-INGRESS per ADR 0043)

ADR 0052 amendment A1 specified 5 layers of inbound defense:
1. Provider signature verification
2. Sender allow-list
3. Per-tenant rate limit
4. Content-scoring hook (`IInboundMessageScorer`)
5. Manual unrouted-triage

Phase 5b adds **Layer 0: EventId-keyed dedup** at the node-side `IInboundMessageReceiver`. This eliminates the "duplicate delivery from Bridge replays" attack surface (an attacker who replays a captured Bridge → node SSE event cannot trigger re-processing).

```csharp
public sealed class DedupInboundMessageReceiver : IInboundMessageReceiver
{
    private readonly IDedupStore _dedupStore;
    private readonly IInboundMessageReceiver _inner;

    public async Task<InboundReceiveOutcome> ReceiveAsync(InboundEnvelope envelope, CancellationToken ct)
    {
        // Layer 0: EventId dedup
        if (await _dedupStore.AlreadyProcessedAsync(envelope.EventId, ct))
        {
            await _audit.EmitAsync(new WebhookEventDeduplicatedAudit(envelope.EventId, envelope.Tenant), ct);
            return InboundReceiveOutcome.Duplicate;
        }
        await _dedupStore.MarkProcessedAsync(envelope.EventId, ct);

        return await _inner.ReceiveAsync(envelope, ct);
    }
}
```

### 3.3 Phase 5c — Bridge-relayed egress (default; sovereign-hosting opt-in for node-direct)

**Council:** MANDATORY (security-engineering — credential locality)
**Halt:** H-5c — gated on `ICredentialsResolver` (ADR 0013). If not yet on main, Phase 5c stubs with config-file-resolved credentials + files `engineer-question-*` for the long-term resolver path.

Per OQ-WSE-1 default decision: **Bridge-relayed egress is the default**. Node calls Bridge over the dedicated channel; Bridge calls Postmark/Twilio.

#### Rationale (from prereq doc §2)

- Credentials live at Bridge, never on the node — simpler per-tenant credential resolution (ADR 0013 `CredentialsReference`).
- Node behind residential CGNAT can dispatch via Bridge without needing outbound HTTPS to providers (most non-business networks have outbound 443 open, but some enterprise firewalls block; Bridge proxies).
- Provider-side rate limits are per-account, so coordinating from Bridge prevents N nodes from racing the rate limit.

#### Node-direct opt-in (sovereign-hosting)

Tenants who self-host Bridge AND want direct provider HTTPS from the node set `MessagingProviderConfig.egress_route = NodeDirect`. The macaroon then includes `audience = messaging-egress` claim (in addition to `messaging-inbound`); credentials are pushed to the node via the secure channel under tenant-key encryption.

#### Per-tenant node-bound macaroon for egress

```csharp
// For Bridge-relayed default:
// Node POST /api/v1/messaging-egress/dispatch
//   Authorization: Macaroon <token with audience="messaging-egress">
//   Body: DispatchRequest { tenant, thread, channel, recipients, content, attachments, audit-correlation }
// Bridge verifies macaroon + audience + tenant scope; resolves credentials via ICredentialsResolver;
// dispatches to Postmark/Twilio; returns OutboundDispatchHandle to node.

// For NodeDirect opt-in:
// Node receives credentials at SSE handshake time (encrypted with tenant key); decrypts locally;
// calls provider directly. Macaroon audience MUST include "messaging-egress" + sender-domain caveats.
```

**Credentials never cross node boundary in clear** — they're either resolved at Bridge (Bridge-relayed default) OR pushed to the node under tenant-key encryption (NodeDirect opt-in; tenant key remains node-local).

---

## 4. Phase 6 — Audit emission (23 `AuditEventType` constants; expanded from initial 12)

**Location:** `shipyard/packages/kernel-audit/AuditEventType.cs`
**Estimate:** 1-2h
**Council:** advisory only

Phase 6 ships the audit-event constants + `MessagingAuditPayloadFactory` (one factory method per event type) per ADR 0049 substrate.

### 4.1 The 23 constants

Initial Phase 6 spec (per `property-messaging-substrate-stage06-handoff.md` line 233) was 12 constants. Track A expanded to 23 to cover the per-attempt audit emission + backpressure events + Layer 0 dedup + Bridge-relay path + startup-validator + deliverability-risk surface.

```csharp
public static class AuditEventType
{
    // ... existing constants ...

    // Phase 6 — messaging substrate (23 NEW constants)

    // Outbound (egress) — 7
    public const string MessageDispatched         = "Messaging.MessageDispatched";          // success terminal
    public const string MessageDispatchAttempt    = "Messaging.MessageDispatchAttempt";     // per-retry; NEW
    public const string MessageDispatchFailed     = "Messaging.MessageDispatchFailed";      // terminal failure
    public const string BridgeRelayDispatchStart  = "Messaging.BridgeRelayDispatchStart";   // Bridge-relayed egress path; NEW
    public const string BridgeRelayDispatchEnd    = "Messaging.BridgeRelayDispatchEnd";     // Bridge-relayed; NEW
    public const string MessageRoutingDelayed     = "Messaging.MessageRoutingDelayed";      // backpressure 24h+; NEW
    public const string MessageRoutingFailed      = "Messaging.MessageRoutingFailed";       // backpressure cap overflow; NEW

    // Inbound (ingress) — 8
    public const string MessageReceived           = "Messaging.MessageReceived";            // routed successfully
    public const string WebhookEventDeduplicated  = "Messaging.WebhookEventDeduplicated";   // Layer 0; NEW
    public const string WebhookSignatureFailed    = "Messaging.WebhookSignatureFailed";     // Layer 1
    public const string SenderRejectedByAllowList = "Messaging.SenderRejectedByAllowList";  // Layer 2
    public const string InboundRateLimitExceeded  = "Messaging.InboundRateLimitExceeded";   // Layer 3
    public const string InboundContentScored      = "Messaging.InboundContentScored";       // Layer 4 (any score)
    public const string InboundRoutingFailed      = "Messaging.InboundRoutingFailed";       // Layer 5 → triage
    public const string InboundTriageActioned     = "Messaging.InboundTriageActioned";      // operator action on triage

    // Thread/visibility — 3
    public const string ThreadCreated             = "Messaging.ThreadCreated";
    public const string ThreadSplit               = "Messaging.ThreadSplit";                // IThreadStore.SplitAsync
    public const string ThreadTokenRevoked        = "Messaging.ThreadTokenRevoked";

    // Config + startup — 3
    public const string MessagingProviderConfigured     = "Messaging.MessagingProviderConfigured";     // tenant config change
    public const string MessagingAdapterStartupFailed   = "Messaging.MessagingAdapterStartupFailed";   // fail-closed startup; NEW
    public const string DeliverabilityRiskElevated      = "Messaging.DeliverabilityRiskElevated";      // DKIM/SPF/DMARC degradation; NEW

    // Consent + compliance — 2
    public const string ConsentRecorded           = "Messaging.ConsentRecorded";
    public const string ComplianceGateBlocked     = "Messaging.ComplianceGateBlocked";
}
```

### 4.2 `MessagingAuditPayloadFactory` shape

```csharp
public static class MessagingAuditPayloadFactory
{
    public static AuditEventPayload MessageDispatched(MessageId messageId, TenantId tenant, MessageChannel channel, OutboundDispatchHandle handle) { /* */ }
    public static AuditEventPayload MessageDispatchAttempt(MessageId messageId, int attemptNumber, string providerKey, TimeSpan elapsed) { /* */ }
    // ... one factory method per event type
}
```

Each factory method takes typed parameters + returns `AuditEventPayload`; consumers emit via `IAuditTrail.RecordAsync(auditEventType, payload, ct)`.

### 4.3 Tests

- One round-trip test per event type (factory produces valid payload; AuditTrail records; query reproduces).
- Cross-tenant isolation test: emitting an event in tenant A's context doesn't surface in tenant B's audit query.

---

## 5. Phase 7 — Cross-package integration tests

**Estimate:** 1h
**Council:** advisory only

Phase 7 adds a `Sunfish.Blocks.Messaging.IntegrationTests/` project that exercises the full stack:
- `OutboundMessageGateway.DispatchAsync` → Bridge-relayed egress → Postmark-mocked HTTP via WireMock.NET cassettes
- Postmark webhook → Bridge ingress → SSE channel → node `IInboundMessageReceiver` → thread routing
- `IThreadStore.SplitAsync` round-trip (W#19 Phase 6 consumer integration)
- Audit-emission round-trip per event type (12 tests covering all 23 constants)

### 5.1 Cassette format choice (OQ-WSE-9)

Recommendation: **WireMock.NET JSON cassettes** at `tests/cassettes/postmark/<scenario>.json`. Reasons:
- Native .NET support; no Python/Ruby toolchain dependency
- Cassette files are version-controlled + diff-able
- Per-scenario isolation; failed test doesn't pollute other scenarios

Alternative considered: VCR-style .NET adaptations (e.g., Scrutor) — less mature; not recommended.

---

## 6. Phase 8 — `apps/docs/` pages

**Estimate:** 1h
**Council:** advisory only

Four pages ship in Phase 8:

| Path | Audience | Content scope |
|---|---|---|
| `apps/docs/blocks/messaging/overview.md` | Consumer-facing | Substrate intro + when to consume (work orders, leasing pipeline, public listings, statements) + ADR 0052 cross-reference |
| `apps/docs/foundation-integrations/messaging.md` | Consumer-facing (developer) | Full contract reference: `IOutboundMessageGateway`, `IInboundMessageReceiver`, `IThreadStore`, `IThreadTokenIssuer`, all DTOs |
| `apps/docs/providers/postmark.md` | Operator + developer | Postmark adapter reference: per-tenant config schema, error-mapping table, BannedSymbols list, ops runbook (DKIM/SPF/DMARC setup) |
| `apps/docs/bridge/messaging-inbound.md` | Operator | SSE wire protocol + macaroon auth + backpressure runbook + at-least-once semantics |

Kitchen-sink demo at `apps/kitchen-sink/` is OPTIONAL — Phase 8 ships docs only; demo waits on Yeoman block-into-kitchen-sink wiring.

---

## 7. Phase 9 — Ledger flip

**Estimate:** 0.5h

Update `shipyard/icm/_state/workstreams/W20-property-messaging-substrate.md` source row from `building` → `built`; run `render-ledger.py`; commit the rendered `active-workstreams.md` change. File `engineer-status-2026-05-XXTHH-MMZ-w20-built.md` (or `cob-status-*`) with the 8 PR refs (#294 Phase 0; #273 Phase 1; #276 Phase 2; #302 Phase 3; Phases 4-9 PR refs from this addendum's execution).

---

## 8. Council requirements (per phase)

| Phase | sec-eng | .NET-arch | Notes |
|---|---|---|---|
| 4 (providers-postmark) | **MANDATORY** | **MANDATORY** | First post-enforcement-gate provider adapter; sets pattern for SendGrid/Twilio follow-ons |
| 5a (SSE channel) | advisory | advisory | Auth = macaroon (ADR 0032) which has prior sec-eng review |
| 5b (6-layer inbound) | **MANDATORY** | advisory | T2-MSG-INGRESS per ADR 0043 |
| 5c (Bridge-relayed egress) | **MANDATORY** | advisory | Credential locality |
| 6 (audit emission) | advisory | advisory | Mirrors ADR 0049 substrate; precedent set |
| 7 (integration tests) | advisory | advisory | Test infrastructure |
| 8 (docs) | advisory | advisory | Documentation |
| 9 (ledger flip) | none | none | Mechanical |

---

## 9. What this addendum does NOT cover (deferred to Phase 2.2 / 2.3)

- **SMS adapter (Twilio + A2P 10DLC)** — Phase 2.2. Requires brand registration workflow on Bridge side; out of Phase 2.1 substrate.
- **SendGrid parity adapter** — Phase 2.2. Validates provider-neutrality across two email providers.
- **Bridge admin UI for tenant config authoring** — Phase 2.2 (FED workstream). Phase 2.1 ships operator-authored JSON only per OQ-WSE-4.
- **Per-tenant DKIM/SPF/DMARC custom domains** — Phase 2.3. ADR 0052 amendment A3 names this as a deliverability isolation upgrade.
- **Frappe→Sunfish bidirectional event mirror** — Phase 2.3. Frappe doctype mutation is invasive; defer until substrate is proven in production.
- **Voice channel** — separate ADR per OQ-WSE-3. Voice is sufficiently different (synchronous, no thread model, no inbound symmetry) that it warrants its own contract.

---

## 10. Open questions (existing; not blocking Phase 4-9 execution)

### From Track A authoring (2026-05-18T01:48Z status beacon)

1. **`Sunfish.Foundation.Recovery.ITenantKeyProvider`** — Phase 4 + 5c depend on it for credential resolution via tenant key. If still pending on main, Engineer hits halt condition at Phase 4 startup-validator implementation; recommend Admiral verify the workstream is on main before Engineer picks up Phase 4. **Status at 2026-05-20:** not re-verified in this addendum; Engineer pre-flight checks per §11.
2. **`Sunfish.Bridge.Auth.NodeMacaroon` (ADR 0032)** — Phase 5a needs the per-tenant node-bound macaroon issuance flow. If macaroon issuance is not yet on main, Phase 5a stubs with shared-secret HMAC and files an `engineer-question-*` for the long-term path. **Halt:** H-5a.
3. **`ICredentialsResolver` (ADR 0013) implementation status** — Phase 4 + 5c depend on it for credential resolution. **Halt:** H-5c.

### From this addendum re-authoring (2026-05-20)

4. **Original lost Track A vendor-research deliverable** — referenced by `onr-status-2026-05-17T20-45Z-adr-0052-outbound-messaging-research.md` at `shipyard/icm/01_discovery/research/onr-adr-0052-outbound-messaging-substrate-2026-05-17.md`. Was 21886 bytes at 2026-05-17T16:34Z; never committed; lost to gitbutler virtual-branch switch. Track A authoring did not require it (the prereq scoping at `onr-wse-handoff-prereq-2026-05-17.md` is the canonical input). **Status at 2026-05-20:** flagged for Admiral's awareness; if reconstruction is wanted, the vendor-survey scope was Postmark/SendGrid/SES/Mailgun/Twilio/Vonage with deliverability + .NET SDK + pricing analysis; can be re-run as a separate queue item.

---

## 11. Pre-flight checks (Engineer executes before Phase 4 PR opens)

### 11.1 Confirm Phase 1-3 + dependencies on main

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-messaging/Services/IThreadStore.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-integrations/Messaging/IOutboundMessageGateway.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-recovery/ITenantKeyProvider.cs
grep -rn "ICredentialsResolver" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-integrations/ 2>/dev/null | head -3
grep -rn "NodeMacaroon" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Auth/ 2>/dev/null | head -3
```

Expected: Phase 1-3 substrate present; `ITenantKeyProvider` + `ICredentialsResolver` + `NodeMacaroon` present (or halt conditions trigger).

### 11.2 Confirm no parallel-session PRs

```bash
gh -R Harborline-Software/shipyard pr list --state open --search "providers-postmark OR messaging in:title,body"
gh -R Harborline-Software/signal-bridge pr list --state open --search "messaging-inbound OR messaging-egress in:title,body"
```

Expected: empty.

### 11.3 Confirm `SUNFISH_PROVNEUT_001` analyzer active

```bash
grep -rn "SUNFISH_PROVNEUT_001\|BannedSymbols" /Users/christopherwood/Projects/Harborline-Software/shipyard/Directory.Build.props 2>/dev/null
```

Expected: analyzer registered globally (per W#14 PR #196 merged 2026-04-28).

### 11.4 Confirm worktree per fleet-conventions

Per `fleet-conventions §Git worktree location`: all worktrees under `<repo>/.worktrees/<branch>/`. Engineer's Phase 4 PR should use:

```bash
cd shipyard
git worktree add ./.worktrees/feat-w20-phase-4-providers-postmark -b feat/w20-phase-4-providers-postmark origin/main
```

---

## 12. References

- `shipyard/icm/_state/handoffs/property-messaging-substrate-stage06-handoff.md` (canonical Phase 1-3 hand-off; 363 lines)
- `shipyard/icm/01_discovery/research/onr-wse-handoff-prereq-2026-05-17.md` (prereq scoping; OQ-WSE-1 through OQ-WSE-12)
- `coordination/inbox/onr-status-2026-05-18T01-48Z-ws-e-handoff-complete.md` (original Track A status; structural summary of the lost 987-line revision)
- `coordination/inbox/admiral-status-2026-05-19T02-30Z-cohort2-handoff-onr-canonical-admiral-subagent-aborted.md` § "Also flagged: ONR's missing-artifact pattern" — the directive to re-author
- `coordination/inbox/admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` item #4 — research queue dispatch (this addendum is the deliverable)
- `shipyard/docs/adrs/0052-bidirectional-messaging-substrate.md` Accepted (A1-A5 + Minor) — full substrate spec
- ADR 0013 (provider-neutrality + enforcement gate); ADR 0031 (Bridge hybrid relay); ADR 0032 (NodeMacaroon); ADR 0043 (T2-MSG-INGRESS); ADR 0046 (Foundation.Recovery); ADR 0049 (audit substrate)

---

**End of addendum.**

— ONR, 2026-05-20T12:05Z
