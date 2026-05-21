# Workstream #20 — Bidirectional Messaging Substrate — Stage 06 Phase 10+ Addendum

**Workstream:** #20 (Bidirectional Messaging Substrate)
**Companion to:** `property-messaging-substrate-stage06-handoff.md` (Phases 1-3 substrate) + `property-messaging-substrate-stage06-phases-4-9-addendum.md` (Phases 4-9 Postmark + SSE + audit)
**Spec:** [ADR 0052](../../docs/adrs/0052-bidirectional-messaging-substrate.md) (Accepted 2026-04-29; A1-A5 + Minor)
**Authored by:** ONR
**Authored at:** 2026-05-21T12-36Z
**Status:** Active (forward-watched; Phase 4-9 in flight; Phase 10+ ratification pending sec-eng + .NET-architect on delivery-ack semantics)

---

## Re-authoring context

V2 batch research queue item #7 per `admiral-directive-2026-05-21T09-15Z`. Extends the WS-E messaging substrate hand-off with Phase 10+ deep design:
- Phase 10 — delivery acknowledgement semantics
- Phase 11 — backpressure + flow control
- Phase 12 — dead-letter queue + retry policy
- Phase 13+ — operational observability + tracing

Phases 0-3 SHIPPED (Phase 1 contracts + Phase 2 blocks-messaging + Phase 3 ThreadToken HMAC). Phases 4-9 ready-to-build per addendum at shipyard#58. This Phase 10+ addendum is forward-watch only; no Engineer kickoff before Phase 9 completes.

---

## 1. Phase 10 — Delivery acknowledgement semantics

### 1.1 Three delivery semantics options

| Option | Semantics | Use case | Engineering cost |
|---|---|---|---|
| **A — At-most-once** | Send + forget; no retry; no ack | Notifications where loss is acceptable (e.g., "reminder, you may have a meeting") | Lowest |
| **B — At-least-once** | Send + retry until ack OR DLQ | Most business messaging (vendor coordination; tenant portal; CPA notifications) | Medium |
| **C — Exactly-once** | Send + idempotent receipt | Audit-relevant messaging (e.g., signed-acknowledgement compliance flows) | Highest |

**ONR recommends Option B (at-least-once) as Phase 10 default + Option C (exactly-once) opt-in for audit-flagged threads.** Option A is too lossy for the property-ops cluster; Option C is too expensive as default but appropriate for right-of-entry notices + tenant portal signed receipts.

### 1.2 Per-message metadata for Option B (at-least-once)

```csharp
public sealed record MessageDeliveryMetadata
{
    public required DeliverySemantics Semantics { get; init; }  // AtMostOnce | AtLeastOnce | ExactlyOnce
    public required int MaxRetries { get; init; }                // default 5
    public required TimeSpan InitialRetryDelay { get; init; }    // default 30s
    public required TimeSpan MaxBackoff { get; init; }           // default 1h
    public string? IdempotencyKey { get; init; }                  // ExactlyOnce only
    public TimeSpan? DeadLetterAfter { get; init; }              // default 7 days
}
```

### 1.3 Outbound ack flow (provider webhooks)

Postmark/Twilio webhooks emit `Delivered` / `Bounced` / `Complained` events. The substrate routes these to `IOutboundMessageGateway.GetStatusAsync` callers AND updates the per-message delivery state machine:

```
Queued → Sent → Delivered     (success terminal)
                  → Bounced     (failure terminal; trigger retry per Option B/C)
                  → Complained  (failure terminal; trigger suppression + audit)
              → Failed (transient) → retry (per Option B/C; exponential backoff)
                                  → DLQ (after MaxRetries)
```

### 1.4 Inbound ack flow (receiver-side)

`IInboundMessageReceiver.ReceiveAsync` returns `InboundReceiveOutcome`:
- `Routed` — message landed in the correct thread → ack provider
- `Duplicate` — Layer 0 EventId dedup detected → ack provider (idempotent)
- `UnroutedTriage` — Layer 5 routing failed → ack provider; queue for operator review
- `Rejected` — Layer 1-4 reject (sig invalid, sender disallowed, rate-limit, content-scored-malicious) → ack provider with reject reason audit

Provider ack semantics:
- Postmark Inbound: HTTP 200 response → ack; non-200 → provider retries
- Twilio: same — return HTTP 200 to acknowledge

### 1.5 ExactlyOnce opt-in design

For audit-relevant flows (right-of-entry notice; tenant signed receipt):

```csharp
var message = new OutboundMessage
{
    // ...
    DeliveryMetadata = new MessageDeliveryMetadata
    {
        Semantics = DeliverySemantics.ExactlyOnce,
        IdempotencyKey = $"right-of-entry:{rightOfEntryNoticeId.Value}",
        MaxRetries = 5,
        DeadLetterAfter = TimeSpan.FromDays(30),    // longer for compliance flows
    },
};
```

Receiver-side dedup uses `IdempotencyKey` + `MessageId` to detect re-delivery. Audit emits `MessageDeliveryReplaySuppressed` on detected re-delivery.

---

## 2. Phase 11 — Backpressure + flow control

### 2.1 Outbound backpressure

If provider rate-limits (e.g., Postmark 429) OR circuit breaker opens (per Phase 4 resilience pipeline):

- Queue outbound messages locally (per-tenant FIFO; max queue size configurable; default 1000 per tenant)
- Emit `MessageQueuedForRetry` audit event per queued message
- Resume on circuit close OR provider rate-limit window elapses
- Queue overflow (>1000 per tenant) → drop oldest + emit `MessageDropped_QueueOverflow`

### 2.2 Inbound backpressure

If node is offline (per Phase 5a SSE channel):

- Bridge enqueues per-tenant FIFO (per Phase 5a; cap 10,000)
- After 24h node-offline → `MessageRoutingDelayed` (per Phase 6 audit constants)
- After 7d → unrouted-inbox triage view (operator action)
- Queue overflow → `MessageRoutingFailed` audit + drop oldest

### 2.3 Cross-tenant fairness

Per-tenant queues prevent one tenant's burst from starving another. Bridge processes outbound queue with round-robin across active tenant queues; configurable priority weighting if needed (out-of-scope for Phase 11; future).

### 2.4 Observability hooks

Phase 11 emits per-tenant queue depth metrics:
- `messaging.outbound.queue.depth{tenant_id}` — current depth
- `messaging.inbound.queue.depth{tenant_id}` — current depth
- `messaging.outbound.queue.dropped{tenant_id}` — count over time

OpenTelemetry exporter integration (per Phase 13+ below).

---

## 3. Phase 12 — Dead-letter queue + retry policy

### 3.1 DLQ shape

A message hits DLQ when:
- Outbound retries exhausted (MaxRetries reached)
- Inbound triage-queue elapses (e.g., 30 days unrouted)
- Manual operator action (rare; for known-bad messages)

DLQ entries persist with full payload + retry history + last-known status. Operators can:
- Inspect (UI / API; Phase 13+)
- Replay (re-queue after fixing the underlying issue)
- Discard (permanent removal; audit-logged)

### 3.2 DLQ schema

```csharp
public sealed record DeadLetterEntry(
    Guid Id,
    TenantId Tenant,
    DeliverySemantics OriginalSemantics,
    Direction Direction,           // Outbound | Inbound
    MessageEnvelope Envelope,       // full original payload
    IReadOnlyList<RetryAttempt> RetryHistory,
    DateTimeOffset DeadLetteredAt,
    DateTimeOffset? ReplayedAt,
    Guid? ReplayedByOperatorId,
    DateTimeOffset? DiscardedAt,
    string? DiscardReason);

public sealed record RetryAttempt(
    int AttemptNumber,
    DateTimeOffset AttemptedAt,
    OutboundDispatchFailure? Failure,
    TimeSpan DurationToFailure);
```

### 3.3 DLQ retention policy

Per ADR 0049 audit retention (default 7 years for tenant-data audit records). DLQ entries are audit-relevant → 7-year retention. Discarded entries retained but marked `DiscardedAt` (audit-loud).

### 3.4 Replay semantics

Operator-initiated replay:
1. Operator selects DLQ entry in UI (Phase 13+)
2. Operator reviews payload + failure history
3. Operator clicks "Replay" → creates new OutboundDispatch with same envelope + fresh IdempotencyKey
4. Original DLQ entry marked `ReplayedAt` with operator id; new dispatch tracked separately
5. Audit emits `MessageDeadLetterReplayed`

---

## 4. Phase 13+ — Operational observability + tracing

### 4.1 Distributed tracing (OpenTelemetry)

Every message gets an `Activity` span from `IOutboundMessageGateway.DispatchAsync` through provider HTTP call through audit emission. Span attributes:
- `messaging.message_id`
- `messaging.tenant_id`
- `messaging.channel` (Email | Sms)
- `messaging.provider` (Postmark | SendGrid | Twilio)
- `messaging.attempt_number`
- `messaging.outcome` (Queued | Sent | Delivered | Bounced | Failed)

Cross-process tracing: Bridge → node SSE channel carries `traceparent` + `tracestate` headers per W3C Trace Context. Node-side `IInboundMessageReceiver` continues the trace span.

### 4.2 Metrics (Prometheus/OTel)

```
# Outbound
messaging.outbound.dispatched_total{tenant_id, channel, provider}
messaging.outbound.delivered_total{tenant_id, channel, provider}
messaging.outbound.failed_total{tenant_id, channel, provider, failure_variant}
messaging.outbound.dispatch_latency_seconds{tenant_id, channel, provider}

# Inbound
messaging.inbound.received_total{tenant_id, channel, provider, outcome}
messaging.inbound.rejected_total{tenant_id, channel, provider, reject_layer}
messaging.inbound.routing_latency_seconds{tenant_id, channel, provider}

# Queues
messaging.outbound.queue.depth{tenant_id}
messaging.inbound.queue.depth{tenant_id}
messaging.dead_letter.depth{tenant_id, direction}

# Resilience
messaging.outbound.retry_total{tenant_id, channel, provider}
messaging.outbound.circuit_breaker.state{tenant_id, channel, provider}
```

### 4.3 Operator dashboards

Phase 13+ ships a dashboard (likely Grafana or a Sunfish-internal dashboard) consuming the metrics above:

- Per-tenant outbound delivery rate (target ≥99% per ADR 0052 A5)
- Per-tenant unrouted triage rate (target ≤5% per ADR 0052 A5)
- Per-provider error rate
- DLQ depth
- Token verify p95 latency (<5ms per ADR 0052 A5)
- Signature verify failure rate (<0.1% per ADR 0052 A5)

### 4.4 Alerts

- Circuit breaker open > 5 min → alert oncall
- DLQ depth > 100 (per tenant) → alert operator
- Unrouted triage > 5% rolling 1h → alert operator
- Stuck queue (no progress > 24h) → alert operator (per ADR 0052 A5 stuck-state alerting)
- Token verify p95 > 5ms → performance regression alert

---

## 5. Phase sequencing + sequence assumptions

| Phase | Estimate | Depends on |
|---|---|---|
| Phase 10 — Delivery acks | 4-6h | Phase 4 (Postmark adapter) + Phase 5 (inbound SSE) |
| Phase 11 — Backpressure | 3-4h | Phase 10 |
| Phase 12 — DLQ | 4-6h | Phase 10 + Phase 11 |
| Phase 13+ — Observability | 4-8h | Phase 10 + 11 + 12 (consumes their metric emissions) |

**Total Phase 10+:** ~15-24h. Multiple PRs; Engineer's choice on sequencing within the cluster.

---

## 6. Sec-eng surfaces for Phase 10+

| Phase | Sec-eng concern | Mitigation |
|---|---|---|
| 10 (Delivery acks) | ExactlyOnce idempotency-key injection attack (caller-supplied; collision-attempted) | Key uniqueness scope = `(tenant_id, idempotency_key)`; max key length 256 chars; UUID format recommended |
| 11 (Backpressure) | Per-tenant queue saturation as DoS vector (one tenant fills queue, blocks fairness) | Per-tenant queue caps; round-robin across tenants; rate-limit at provider boundary |
| 12 (DLQ) | DLQ replay reuse of leaked credentials (attacker replays a captured DLQ entry) | Idempotency-key uniqueness; replay creates NEW dispatch with new key; audit emits replay |
| 13+ (Observability) | Tenant-id label cardinality explosion (cardinality bomb on metrics emit) | Bounded metric label set (tenant_id is bounded by tenant count; channel + provider are bounded enums); no caller-supplied free-form labels |

---

## 7. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **Delivery semantics default — at-least-once (ONR recommended) vs at-most-once?** ONR's read: at-least-once is the canonical business-messaging default; at-most-once is too lossy for property-ops cluster.
2. **DLQ persistence — same DB as audit records (ADR 0049) OR separate?** ONR recommends same DB; audit-relevant; 7-year retention applies.
3. **OpenTelemetry vs Prometheus-native metrics?** ONR recommends OTel (provides both metrics + traces; vendor-neutral).

### For security-engineering council

1. **ExactlyOnce idempotency-key uniqueness scope — `(tenant_id, key)` (ONR recommended) vs `(key)` global?** Per-tenant scoping prevents cross-tenant key collisions.
2. **DLQ replay — operator-only (ONR recommended) vs caller-replay-via-API?** Operator-only reduces attack surface; caller-replay would require authentication that the original sender is replaying.
3. **Tracing span attributes — emit `tenant_id` in span context (ONR recommended; forensics value) vs redact (cross-tenant trace leakage concern)?** Spans are per-process; cross-tenant leakage requires the trace collector to be tenant-isolated. Confirm.

### For CIC

1. **Phase 10+ sequencing — ship Phase 10 in cohort-N+1 OR wait for Phase 4-9 cluster to land first?** ONR recommends sequential (Phases 4-9 → Phase 10+).

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #7 — parent directive
2. `shipyard/icm/_state/handoffs/property-messaging-substrate-stage06-handoff.md` — canonical Phase 1-3 hand-off
3. `shipyard/icm/_state/handoffs/property-messaging-substrate-stage06-phases-4-9-addendum.md` (shipyard#58) — Phase 4-9 addendum
4. ADR 0052 (bidirectional messaging substrate; A1-A5 + Minor)
5. ADR 0049 (audit substrate; 7-year retention)
6. W3C Trace Context specification (`traceparent` + `tracestate` headers)
7. OpenTelemetry .NET SDK documentation
8. Postmark Inbound + Twilio webhook semantics (ack via HTTP 200)

---

## 9. What ONR does next

V2 research queue cleared. Per proceed-continuously discipline:

- Item #7 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-cleared-fed-idle.md` (queue exhausted; ready for V3 dispatch).
- Return to idle, polling per heartbeat protocol.

— ONR, 2026-05-21T12-36Z
