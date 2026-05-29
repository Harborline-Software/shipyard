# WS-E Phase 10 — Delivery Acknowledgement deep research (2026-05-21)

**Authored by:** ONR (V4 batch item #5)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-05Z` item #5)
**Authored at:** 2026-05-21T14-22Z
**Status:** draft (sec-eng + .NET-architect council review at Engineer Phase 10 PR opening)

---

## Scope

V2 #7 (shipyard#75) sketched WS-E Phase 10+ at the high level — at-most-once vs at-least-once vs exactly-once. V4 #5 deepens Phase 10 specifically:

- **Per-substrate-concern semantics selection** (which use cases need which guarantee?)
- **Failure modes + recovery protocols** (concrete sequences)
- **Test scaffolding requirements** (cassette + harness shape)

Companion to V1 #4 WS-E Phases 4-9 addendum (shipyard#58) + V2 #7 Phase 10+ addendum (shipyard#75).

---

## TL;DR

1. **Three substrate concerns map to three semantics:**
   - **Notifications** (reminder emails, sync-status pings, FYI messages) → **at-most-once**. Loss acceptable; retry would create user-visible duplicate
   - **Business messaging** (vendor coordination, tenant portal, CPA notifications, statement delivery) → **at-least-once** + receiver-side dedup via `WebhookEventEnvelope.EventId` (per V1 #4 Phase 5b Layer 0)
   - **Audit-relevant signed flows** (right-of-entry notices, tenant signed receipts, dispute notices) → **exactly-once** + `IdempotencyKey` mandatory + receiver replay-suppression audit

2. **At-least-once is the default** for the substrate; opt-into-exactly-once via `MessageDeliveryMetadata.Semantics = ExactlyOnce` + caller-supplied `IdempotencyKey`.

3. **At-most-once requires opt-in via `Semantics = AtMostOnce` + `MaxRetries = 0`** — explicit; never implicit (silent default).

4. **Three failure modes documented:**
   - **Provider rate-limit (transient)** — circuit breaker open; queue locally; resume on close
   - **Permanent bounce / spam complaint** — drain to DLQ + sender-side suppression list update + audit
   - **Receiver-side fail (node offline)** — Bridge queues per-tenant FIFO; 24h backpressure → MessageRoutingDelayed audit; 7d → unrouted-inbox triage

5. **Test scaffolding requirements**: WireMock.NET cassettes (per V1 #4 Phase 7 recommendation) + `Sunfish.Blocks.Messaging.IntegrationTests/` project + new `DeliveryAckSemantics` test class with one happy-path + one failure-path per semantics variant.

6. **Forward-watched:** explicit-once dedup window length (24h proposed) needs sec-eng confirmation (longer = more storage; shorter = duplicate-suppression-window narrower).

---

## 1. Per-substrate-concern semantics map

### 1.1 At-most-once

**Use cases (cohort-2+ context):**
- Inbox dashboard "reminder" emails (e.g., "rent due soon")
- Sync-status pings ("your accountant is now online")
- Low-stakes operational notices that fire frequently

**Why at-most-once:**
- Frequent retransmit creates user-visible duplicate (annoying)
- Loss is acceptable (user can refresh / next periodic fires anyway)
- Provider-side rate-limit savings

**Configuration:**
```csharp
new MessageDeliveryMetadata
{
    Semantics = DeliverySemantics.AtMostOnce,
    MaxRetries = 0,                       // no retry
    InitialRetryDelay = TimeSpan.Zero,
    MaxBackoff = TimeSpan.Zero,
    DeadLetterAfter = null,               // no DLQ
}
```

**Failure mode:** transient failure → message LOST. Audit emits `MessageDispatchFailed` with `outcome=LostByDesign`.

**Test scaffold:**
```csharp
[Fact]
public async Task AtMostOnce_TransientFailure_LosesMessage_NoRetry()
{
    // Arrange — Postmark mock returns 503 (transient)
    var gateway = SetupGatewayWithMock(httpStatus: 503);
    var msg = WithSemantics(DeliverySemantics.AtMostOnce);

    // Act — DispatchAsync; expect single attempt
    var result = await gateway.DispatchAsync(msg, CancellationToken.None);

    // Assert
    Assert.Equal(OutboundDispatchStatus.Failed, result.Status);
    Assert.Single(MockServer.Requests);  // exactly 1 attempt; no retry
    Assert.Contains(auditTrail.RecordedEvents,
        e => e.EventType == "Messaging.MessageDispatchFailed");
}
```

### 1.2 At-least-once (DEFAULT)

**Use cases (most business messaging):**
- Vendor coordination thread messages
- Tenant portal magic-link delivery
- CPA notifications about year-end timing
- Statement delivery emails (monthly cycle)
- Work-order assignment notifications

**Why at-least-once + dedup:**
- Business-critical: missing a message has cost
- Provider transient failures shouldn't lose messages
- Receiver-side dedup (V1 #4 Phase 5b Layer 0) handles the duplicate-delivery side effect
- This is the canonical business-messaging default

**Configuration:**
```csharp
new MessageDeliveryMetadata
{
    Semantics = DeliverySemantics.AtLeastOnce,
    MaxRetries = 5,                       // default per V1 #4
    InitialRetryDelay = TimeSpan.FromSeconds(30),
    MaxBackoff = TimeSpan.FromHours(1),
    IdempotencyKey = null,                // not required for AtLeastOnce
    DeadLetterAfter = TimeSpan.FromDays(7),
}
```

**Failure mode 1 — transient (e.g., Postmark 429):**
- Retry per `MaxRetries` (5 attempts, exponential backoff capped at 1h)
- Audit emits `MessageDispatchAttempt` per attempt
- If all retries exhausted → DLQ + `MessageDispatchFailed` + operator-replay path

**Failure mode 2 — permanent (e.g., Postmark hard bounce):**
- Single attempt; no retry (permanent error)
- Audit emits `MessageDispatchFailed` with `failure_variant=PermanentBounce`
- Sender-side suppression list updated for the recipient
- DLQ entry created for operator review

**Receiver-side dedup:**
- V1 #4 Phase 5b Layer 0 EventId dedup at `IInboundMessageReceiver.ReceiveAsync`
- 30-day retention on dedup store (acceptable; rare to have duplicate beyond 30d)
- Duplicate detection emits `WebhookEventDeduplicated` audit

**Test scaffold:**
```csharp
[Fact]
public async Task AtLeastOnce_TransientFailure_RetriesUpToMaxRetries()
{
    var gateway = SetupGatewayWithMock(httpStatus: 503);
    var msg = WithSemantics(DeliverySemantics.AtLeastOnce, maxRetries: 3);

    var result = await gateway.DispatchAsync(msg, CancellationToken.None);

    Assert.Equal(OutboundDispatchStatus.Failed, result.Status);
    Assert.Equal(3, MockServer.Requests.Count);  // 3 attempts
    Assert.Equal(3, auditTrail.CountOf("Messaging.MessageDispatchAttempt"));
}

[Fact]
public async Task AtLeastOnce_PermanentBounce_NoRetry_EmitsDispatchFailed()
{
    var gateway = SetupGatewayWithMock(/* simulate hard bounce webhook */);
    var msg = WithSemantics(DeliverySemantics.AtLeastOnce);

    var result = await gateway.DispatchAsync(msg, CancellationToken.None);

    Assert.Equal(OutboundDispatchStatus.Failed, result.Status);
    Assert.Single(MockServer.Requests);  // 1 attempt only
    Assert.Contains(auditTrail.RecordedEvents,
        e => e.EventType == "Messaging.MessageDispatchFailed"
          && e.Payload.GetString("failure_variant") == "PermanentBounce");
}

[Fact]
public async Task AtLeastOnce_ReceiverDedup_DuplicateDelivery_Suppressed()
{
    var envelope = NewInboundEnvelope(eventId: Guid.NewGuid());
    var receiver = new DedupInboundMessageReceiver(dedupStore, innerReceiver);

    var outcome1 = await receiver.ReceiveAsync(envelope, CancellationToken.None);
    var outcome2 = await receiver.ReceiveAsync(envelope, CancellationToken.None);  // duplicate

    Assert.Equal(InboundReceiveOutcome.Routed, outcome1);
    Assert.Equal(InboundReceiveOutcome.Duplicate, outcome2);
    Assert.Contains(auditTrail.RecordedEvents,
        e => e.EventType == "Messaging.WebhookEventDeduplicated");
}
```

### 1.3 Exactly-once

**Use cases (audit-relevant compliance flows):**
- Right-of-entry notices (legal compliance audit trail)
- Tenant signed-receipt deliveries
- Dispute notice deliveries
- Certified mail digital-equivalents

**Why exactly-once:**
- Compliance audit trail must not show duplicates (audit-loud)
- Receiver-side signed-acknowledgement required
- Sender-side suppression of replay attacks

**Configuration:**
```csharp
new MessageDeliveryMetadata
{
    Semantics = DeliverySemantics.ExactlyOnce,
    MaxRetries = 5,                                  // retry on transient
    InitialRetryDelay = TimeSpan.FromSeconds(30),
    MaxBackoff = TimeSpan.FromHours(1),
    IdempotencyKey = "right-of-entry:01H9K..." // caller-supplied; REQUIRED
                                                     // for ExactlyOnce
    DeadLetterAfter = TimeSpan.FromDays(30),         // longer for compliance
}
```

**Failure mode 1 — transient with IdempotencyKey replay:**
- Retry per V1 #4 Phase 4 resilience pipeline
- Provider may see duplicate request with same IdempotencyKey
- Postmark / SendGrid / Twilio support idempotency-key headers natively
- Receiver-side dedup (V1 #4 Phase 5b Layer 0) catches if message lands twice

**Failure mode 2 — caller submits same IdempotencyKey + different payload:**
- Sender-side rejection: `OutboundDispatchFailure.IdempotencyConflict` (NEW variant added to the 8-variant union from V1 #4 §2.1)
- Audit emits `MessageDispatchFailed` with `failure_variant=IdempotencyConflict`

**Failure mode 3 — receiver-side replay detection:**
- Inbound envelope with seen-before IdempotencyKey → `InboundReceiveOutcome.Duplicate`
- Audit emits `MessageDeliveryReplaySuppressed` (NEW; mirrors Layer 0 `WebhookEventDeduplicated` but for ExactlyOnce semantics specifically)

**Test scaffold:**
```csharp
[Fact]
public async Task ExactlyOnce_WithIdempotencyKey_Retry_DeduplicatesOnProviderSide()
{
    var gateway = SetupGatewayWithMockAndIdempotency();
    var key = $"test-{Guid.NewGuid()}";
    var msg = WithSemantics(DeliverySemantics.ExactlyOnce, idempotencyKey: key);

    var result1 = await gateway.DispatchAsync(msg, CancellationToken.None);
    var result2 = await gateway.DispatchAsync(msg, CancellationToken.None);  // re-submit

    // Provider sees the same IdempotencyKey twice
    Assert.Equal(OutboundDispatchStatus.Sent, result1.Status);
    Assert.Equal(OutboundDispatchStatus.Sent, result2.Status);  // idempotent replay
    Assert.Single(MockServer.UniqueIdempotencyKeys);  // provider deduped
}

[Fact]
public async Task ExactlyOnce_SameKey_DifferentPayload_Rejects_IdempotencyConflict()
{
    var key = $"test-{Guid.NewGuid()}";
    var msg1 = WithSemantics(DeliverySemantics.ExactlyOnce, idempotencyKey: key, body: "v1");
    var msg2 = WithSemantics(DeliverySemantics.ExactlyOnce, idempotencyKey: key, body: "v2");

    await gateway.DispatchAsync(msg1, CancellationToken.None);
    var result = await gateway.DispatchAsync(msg2, CancellationToken.None);

    Assert.Equal(OutboundDispatchStatus.Failed, result.Status);
    Assert.Contains(auditTrail.RecordedEvents,
        e => e.EventType == "Messaging.MessageDispatchFailed"
          && e.Payload.GetString("failure_variant") == "IdempotencyConflict");
}

[Fact]
public async Task ExactlyOnce_ReceiverSideReplay_EmitsReplaySuppressed()
{
    var envelope = NewInboundEnvelope(
        eventId: Guid.NewGuid(),
        idempotencyKey: $"compliance-{Guid.NewGuid()}");
    var receiver = new DedupInboundMessageReceiver(dedupStore, innerReceiver);

    var outcome1 = await receiver.ReceiveAsync(envelope, CancellationToken.None);
    var outcome2 = await receiver.ReceiveAsync(envelope, CancellationToken.None);

    Assert.Equal(InboundReceiveOutcome.Routed, outcome1);
    Assert.Equal(InboundReceiveOutcome.Duplicate, outcome2);
    Assert.Contains(auditTrail.RecordedEvents,
        e => e.EventType == "Messaging.MessageDeliveryReplaySuppressed");
}
```

---

## 2. Failure modes catalog

### 2.1 Sender-side failure modes

| Mode | Detection | Recovery |
|---|---|---|
| Provider rate-limit (429) | HTTP status | Retry per resilience pipeline (V1 #4 Phase 4) |
| Provider 5xx server error | HTTP status | Retry per resilience pipeline |
| Network timeout | Resilience timeout (30s email; 10s SMS; 60s voice) | Retry per resilience pipeline; circuit-break at 0.5 failure ratio |
| Provider auth rejected (e.g., expired API key) | HTTP 401 | Terminal — emit `OutboundDispatchFailure.ProviderAuthRejected` |
| Content rejected (e.g., DKIM mismatch) | HTTP 422 | Terminal — emit `OutboundDispatchFailure.ContentRejected` |
| Recipient blocked (suppression list) | HTTP 422 OR webhook | Terminal per-recipient — emit `OutboundDispatchFailure.RecipientBlocked` |
| Permanent bounce | Webhook | Terminal — emit `OutboundDispatchFailure.PermanentBounce` + sender-side suppression update |
| Idempotency conflict | Internal dedup check | Terminal — emit `OutboundDispatchFailure.IdempotencyConflict` |
| Compliance gate (e.g., CAN-SPAM footer missing) | Substrate-time at content rendering | Terminal — emit `OutboundDispatchFailure.ComplianceGateRejected` |

### 2.2 Receiver-side failure modes

| Mode | Detection | Recovery |
|---|---|---|
| Provider signature invalid | V1 #4 Phase 5b Layer 1 | Reject; emit `Messaging.WebhookSignatureFailed` |
| Sender allow-list reject | V1 #4 Phase 5b Layer 2 | Reject; emit `Messaging.SenderRejectedByAllowList` |
| Per-tenant rate-limit exceeded | V1 #4 Phase 5b Layer 3 | Reject; emit `Messaging.InboundRateLimitExceeded` |
| Content scoring (Layer 4) | `IInboundMessageScorer` | Per score: reject OR triage; emit `Messaging.InboundContentScored` |
| Manual routing failed (Layer 5) | Thread resolution fails | Triage queue; emit `Messaging.InboundRoutingFailed` |
| EventId duplicate (Layer 0) | Dedup store | Suppress; emit `Messaging.WebhookEventDeduplicated` |
| IdempotencyKey replay (ExactlyOnce only) | Idempotency store | Suppress; emit `Messaging.MessageDeliveryReplaySuppressed` |
| Node offline | Bridge SSE channel breakage | Bridge queues per V1 #4 Phase 5a backpressure; 24h → audit |

### 2.3 Cross-cutting failure: DLQ entry creation

When sender-side retries exhausted OR receiver-side routing-triage elapses (30d default), the message enters DLQ per V2 #7 §3. DLQ schema + replay semantics documented there.

---

## 3. Test scaffolding requirements

### 3.1 Project structure

```
shipyard/packages/blocks-messaging/
├── tests/
│   └── ... (existing unit tests; Phase 2 substrate)
└── (entity + service impls)

shipyard/packages/providers-postmark/
├── tests/
│   └── ... (existing unit tests; Phase 4 adapter)
└── (Postmark adapter)

shipyard/packages/blocks-messaging.IntegrationTests/        ← NEW (Phase 10)
├── Sunfish.Blocks.Messaging.IntegrationTests.csproj
├── DeliveryAckSemanticsTests.cs                            ← per-semantics tests
├── ProviderResilienceTests.cs                              ← Phase 4 resilience
├── InboundDedupTests.cs                                    ← Phase 5b Layer 0
├── BridgeAuditEmissionTests.cs                             ← Phase 6 audit
├── DeadLetterQueueTests.cs                                 ← V2 #7 Phase 12
└── cassettes/postmark/                                     ← WireMock.NET fixtures
    ├── happy-path-200.json
    ├── rate-limit-429.json
    ├── auth-rejected-401.json
    ├── hard-bounce-webhook.json
    └── ...
```

### 3.2 Cassette format (WireMock.NET; per V1 #4 §5 recommendation)

```json
{
  "Request": {
    "Methods": ["POST"],
    "Url": "/email/withTemplate"
  },
  "Response": {
    "StatusCode": 429,
    "Headers": {
      "Content-Type": "application/json",
      "Retry-After": "30"
    },
    "BodyAsJson": {
      "ErrorCode": 429,
      "Message": "Rate limit exceeded"
    }
  }
}
```

Per-scenario isolation; cassette files are version-controlled + diff-able.

### 3.3 Test fixture base class

```csharp
public abstract class DeliveryAckSemanticsTestBase : IClassFixture<MessagingIntegrationFixture>
{
    protected readonly MessagingIntegrationFixture _fixture;

    protected DeliveryAckSemanticsTestBase(MessagingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    protected OutboundMessage WithSemantics(
        DeliverySemantics semantics,
        int maxRetries = 5,
        string? idempotencyKey = null,
        string body = "test")
    {
        return new OutboundMessage { /* canonical test envelope */ };
    }

    protected void AssertAuditEmitted(string eventType, int expectedCount = 1)
    {
        var count = _fixture.AuditTrail.RecordedEvents.Count(e => e.EventType == eventType);
        Assert.Equal(expectedCount, count);
    }
}
```

### 3.4 Test coverage minimum (per V4 #5 directive)

- 3 happy-path tests (one per semantics variant)
- 3 failure-path tests (one per semantics variant; primary failure mode each)
- 6 cross-semantics tests (idempotency conflict; receiver dedup; DLQ entry; replay suppression; circuit breaker open; node offline backpressure)

**Total minimum:** 12 integration tests at Phase 10 acceptance.

---

## 4. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **Default semantics for legacy messaging without explicit Metadata?** ONR recommends DEFAULT TO AtLeastOnce; explicit AtMostOnce + ExactlyOnce require opt-in via `Semantics` enum on `MessageDeliveryMetadata`. Confirm.
2. **IdempotencyConflict failure variant — should it be added to the 8-variant `OutboundDispatchFailure` union (becoming 9)?** ONR recommends YES; this is the audit-explicit reject path for ExactlyOnce mode. Confirm.

### For security-engineering council

1. **ExactlyOnce dedup window length — 24h proposed; longer (7d, 30d) = more storage; shorter = duplicate-suppression-window narrower.** ONR's read: 24h matches the resilience pipeline retry budget (max ~1h backoff × 5 attempts = ~5h envelope; 24h covers operator-replay slack). Confirm.
2. **Receiver-side `MessageDeliveryReplaySuppressed` audit emission — separate audit event from `WebhookEventDeduplicated` (Layer 0)? ONR recommends YES — semantics differ (Layer 0 is wire-level dedup; replay-suppressed is application-level Idempotency-Key dedup).** Confirm.
3. **Compliance-flow ExactlyOnce — should the `DeadLetterAfter` be longer (30d proposed) than business AtLeastOnce (7d)?** ONR recommends YES — legal compliance audit needs longer recovery window. Confirm.

### For CIC

1. **Phase 10 sequencing — ship after Phase 4-9 cluster lands (per V2 #7 dependency); confirm gated.**

---

## 5. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-05Z-onr-v4-batch-adrs-cohorts-and-cleanup.md` item #5
2. V1 #4 WS-E Phases 4-9 addendum (shipyard#58) — resilience pipeline + Layer 0 dedup
3. V2 #7 WS-E Phase 10+ addendum (shipyard#75) — high-level Phase 10 sketch
4. `shipyard/docs/adrs/0052-bidirectional-messaging-substrate.md` (Accepted) — substrate spec
5. WireMock.NET v1.5+ documentation
6. RFC 7240 (Prefer header) — idempotency hint mechanism reference

---

## 6. What ONR does next

V4 #5 deliverable complete (this doc). Files `onr-status-*-v4-item-5-ws-e-phase-10-deep-research-complete.md`. V4 effective scope cleared (4 ONR items shipped: #4 + #5 + #6 + #7; ADR items #1+#2 owned by Admiral; #3 was redundant with V3 #3).

Files V4-cleared idle beacon triggering V5 dispatch per directive.

— ONR, 2026-05-21T14:22Z
