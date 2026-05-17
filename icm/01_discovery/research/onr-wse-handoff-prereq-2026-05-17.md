# ONR research — WS-E (outbound messaging) Stage 06 hand-off prerequisite scoping (2026-05-17)

**Requester:** Admiral (via `admiral-directive-2026-05-17T21-08Z-onr-wse-handoff-prerequisite-scoping.md`)
**Scope:** Identify the **specific design questions** the WS-E Stage 06 hand-off must answer to be buildable. **In scope:** the six prerequisite categories named in the directive (contract design, webhook ingestion, tenant scoping, compliance gates, test/dev shim, ERPNext-coexistence). **Out of scope:** drafting the WS-E hand-off itself, vendor re-survey (settled in the 2026-05-17 ADR 0052 substrate research), and ADR 0052 drafting (deferred per ratified path). **Authoritative sources consulted:** ADR 0052 Accepted (with A1–A5 + Minor 2026-04-29 amendments), ADR 0013 (provider-neutrality + enforcement gate), ADR 0026 → superseded by ADR 0031 (Bridge hybrid relay posture), ADR 0043 (T2-MSG-INGRESS), ADR 0049 (audit substrate), and the prior ONR vendor-survey deliverable.
**Status:** Final (scoping research; not an ADR; not the hand-off itself).

---

## TL;DR

1. **Contract design** is mostly settled by ADR 0052 (`IOutboundMessageGateway` + `IInboundMessageReceiver` already specified). The WS-E hand-off needs to **freeze three under-specified points**: (a) the error-model taxonomy, (b) the retry / resilience-policy ownership boundary (per ADR 0013, adapter-owned via `Microsoft.Extensions.Http.Resilience` — but the hand-off must name which faults the gateway surfaces vs. swallows), and (c) the explicit *exclusion list* of vendor features (raw template languages, vendor-native scheduling, vendor-side suppression lists) that consumers MUST go vendor-direct for.
2. **Webhook ingestion** lands at Bridge (hosted relay per ADR 0031). The hand-off must specify the **relay-to-node forwarding contract** — the missing primitive between Bridge's webhook receiver and the local Sunfish node's `IInboundMessageReceiver`. Two viable shapes (sync daemon piggyback vs. dedicated `messaging-inbound` channel); recommend **dedicated channel** for blast-radius isolation.
3. **Tenant scoping** has structural primitives (ADR 0013 `CredentialsReference`, ADR 0052 `MessagingProviderConfig`, ADR 0008 `TenantId`) but no **configuration-write surface**. The hand-off must specify *where* a tenant's Postmark API key, Twilio Account SID, sender domain, and A2P 10DLC brand reference get authored — Bridge admin UI, anchor-app settings, or per-node config file. Recommend Bridge admin UI as default with anchor-app read-only echo.
4. **Compliance gates** split cleanly: A2P 10DLC + CAN-SPAM are **substrate-time** (gate at adapter-config validation; refuse to dispatch without registration evidence); GDPR cross-border + TCPA per-recipient consent are **vendor-time + consumer-time** (substrate provides hooks, downstream owns UX). The hand-off must name which gates **fail-closed at startup** vs. **fail-closed at first-send**.
5. **Test/dev shim** has a clear shape: `InMemoryOutboundMessageGateway` (already in ADR 0052 implementation checklist) + a **capture-and-replay harness** for parity tests + an HTTP-record/replay layer (WireMock.NET or VCR-style cassettes) for adapter-level integration tests. The hand-off must pick **one** cassette format and name the artifact storage path so adapter authors don't reinvent it.
6. **ERPNext coexistence** is the highest-leverage open question. WS-E **composes alongside** Frappe Email + Frappe SMS rather than replacing them — Sunfish owns the audit-substrate-integrated bidirectional substrate (per ADR 0052); Frappe retains its native channels for Frappe-domain notifications (workflow emails, Frappe reminders). The hand-off must specify the **handoff seam** (Frappe→Sunfish event bridge for emitting Frappe-side messages into the Sunfish audit substrate, and Sunfish→Frappe event bridge for visibility into Frappe-dispatched messages). Recommend: read-only mirror in Phase 2.1; bidirectional in Phase 2.3.

---

## Section 1 — Contract design

**Mostly settled by ADR 0052 §"Initial contract surface"** — `IOutboundMessageGateway.DispatchAsync(OutboundMessage, CancellationToken)` returns `OutboundDispatchHandle`; `GetStatusAsync(handle, ct)` returns `OutboundDispatchStatus` (8-value enum: Queued, Sent, Delivered, Bounced, Complained, Opened, Clicked, Failed). The directive's reference to `IOutboundEmailSender` / `IOutboundSmsSender` / `IOutboundVoiceSender` (per-channel splits) is **superseded** by ADR 0052's accepted channel-multiplexed `IOutboundMessageGateway` (Email | Sms enum on `OutboundMessage`). The hand-off should propagate that correction.

**What the hand-off must still settle:**

1. **Error-model taxonomy.** ADR 0052 names `Failed` as a terminal state but doesn't enumerate failure causes. Hand-off needs an `OutboundDispatchFailure` discriminated union: `ProviderAuthRejected | ProviderRateLimited | TenantConfigMissing | ContentRejected | RecipientBlocked | PermanentBounce | TransientNetworkError | ComplianceGateRejected`. Each maps to retry-policy behavior (transient → adapter resilience pipeline retries; permanent → surface to caller; compliance → audit-emit and abort).
2. **Retry-policy ownership boundary.** ADR 0013 mandates adapters own resilience via `Microsoft.Extensions.Http.Resilience`. Hand-off must specify: (a) which retry attempts emit per-attempt `MessageDispatched` audit records vs. one rollup; (b) whether `DispatchAsync` blocks until terminal or returns immediately with a `Queued` handle; (c) timeout budget per channel (recommend: email 30s, SMS 10s, voice 60s).
3. **Vendor-feature exclusion list.** ADR 0013 §"Domain concepts are Sunfish-modeled, not vendor-mirrored" forbids vendor-feature passthroughs. Hand-off must name the explicit exclusions: (a) **vendor template engines** (Postmark Templates, SendGrid Dynamic Templates) — consumers render content upstream and pass a finalized `MessageContent` body; (b) **vendor scheduling** (Postmark `DeliveryStartAt`) — Sunfish-side `IClock`-driven scheduling only; (c) **vendor suppression lists** — Sunfish owns consent state per ADR 0052 A5; (d) **vendor analytics dashboards** — observability via Sunfish telemetry, not vendor consoles.

**Voice channel:** ADR 0052 currently enumerates `MessageChannel { Email, Sms }` only. The vendor survey recommended Twilio Voice as incidental at our scale. Hand-off must decide: add `Voice` to the enum (forcing `MessageContent` schema-expansion for TwiML / IVR payloads) or scope voice as a **separate ADR follow-up**. Recommend the latter — voice is sufficiently different (synchronous, no thread model, no inbound symmetry) that it warrants its own contract.

---

## Section 2 — Webhook ingestion

**ADR 0031 (Bridge hybrid relay)** places the webhook ingress at Bridge by structural mandate: Postmark/Twilio webhooks need a public HTTPS endpoint with a stable URL, which a local-first node behind NAT cannot offer. Bridge already has `accelerators/bridge` listed in ADR 0052 §"Affected packages" with "webhook receiver endpoints + per-tenant gateway config UI + unrouted inbox triage view."

**The missing primitive — relay-to-node forwarding.** ADR 0052 specifies the node-side `IInboundMessageReceiver`. It does NOT specify how an envelope arriving at Bridge's webhook endpoint reaches the right tenant's local node. Two viable patterns:

- **Option A — Sync-daemon piggyback.** Inbound envelopes ride the existing sync-daemon channel (Paper §6.2) between Bridge and the tenant's node(s). One transport, one auth model, one observability surface. **Risk:** sync-daemon protocol is already load-bearing for CRDT replication; messaging-inbound traffic patterns are bursty and asymmetric. A spam-storm inbound event could starve CRDT sync.
- **Option B — Dedicated `messaging-inbound` channel.** A separate WebSocket / SSE / long-poll channel from Bridge to the tenant node, scoped purely to inbound envelopes. Independent rate-limit, independent backpressure, independent failure-domain. **Cost:** second transport to operate.

**Recommendation:** Option B (dedicated channel). Blast-radius isolation matters more than transport-unification at our scale. The hand-off must specify: (a) channel transport (SSE preferred for one-way push semantics + simple resume), (b) auth (per-tenant node-bound macaroon per ADR 0032), (c) backpressure shape (Bridge queues unrouted-inbox-bound envelopes locally if the node is offline > 24h; emits `MessageRoutingDelayed` audit record), (d) at-least-once semantics with `WebhookEventEnvelope.EventId`-keyed dedup at the node (ADR 0013 already specifies this envelope shape).

**Signature verification** is unambiguous per ADR 0052 A1: Bridge verifies the provider signature at ingress; the per-vendor signing scheme (Postmark HMAC, Twilio `X-Twilio-Signature`) is adapter-owned. Hand-off must name: signature-verify failure ratio threshold for alerting (per A5, < 0.1%).

**Reply-to-egress symmetry.** ADR 0052 establishes that egress originates at the node (the node calls `IOutboundMessageGateway.DispatchAsync`). The actual HTTP call to Postmark/Twilio happens **from where?** Hand-off must settle: (a) node-direct (the node has its own outbound HTTPS to the provider — works for non-NAT'd nodes; constrained on residential CGNAT); or (b) Bridge-relayed (the node sends a `DispatchRequest` to Bridge over the same dedicated channel; Bridge calls the provider). Recommend **Bridge-relayed** as the default; node-direct as an opt-in for sovereign-hosting tenants. This also simplifies per-tenant credential resolution (Section 3) — credentials live at Bridge, never on the node.

---

## Section 3 — Tenant scoping

**Primitives are in place:** `TenantId` (ADR 0008), `CredentialsReference` (ADR 0013), `MessagingProviderConfig` (ADR 0052 + A3-introduced `SenderIsolationMode` enum: `SharedDomain | PerTenantStream | PerTenantSubdomain`), `IThreadTokenIssuer` per-tenant key (ADR 0052 A2 via `ITenantKeyProvider`). What's missing is the **authoring surface** — where tenants set these values.

**Per-tenant configuration shape (hand-off must finalize):**

```yaml
# Authored at Bridge admin UI; persisted under tenant-key encryption per ADR 0052
tenant: <TenantId>
messaging:
  email:
    provider: postmark | sendgrid | ses
    credentials_ref: <opaque CredentialsReference per ADR 0013>
    sender_domain: <e.g. notices.acme-properties.com>
    sender_isolation: SharedDomain | PerTenantStream | PerTenantSubdomain
    dkim_status: PendingVerification | Verified | Failed
  sms:
    provider: twilio | aws-eum
    credentials_ref: <opaque>
    a2p_10dlc:
      brand_id: <provider-side Brand ID>
      campaign_id: <provider-side Campaign ID>
      registration_status: Pending | Approved | Rejected
    sender_numbers: [+1NNNNNNNNNN, ...]
  egress_route: NodeDirect | BridgeRelayed  # per Section 2 decision
  thread_token_ttl: 90.00:00:00              # per ADR 0052 A2; overridable
```

**Authoring-surface decision the hand-off must settle:**

- **Option α — Bridge admin UI (recommended).** Tenant administrator authenticates to Bridge's existing admin tier (ADR 0006/0031), authors config in a form, Bridge persists under tenant-key encryption. Node receives config via sync (read-only mirror).
- **Option β — Anchor-app settings page.** Tenant administrator authors at the local node; node pushes to Bridge over the dedicated channel.
- **Option γ — Per-node config file.** Manual YAML editing on each node.

Recommend α — Bridge-authoritative, node-mirror — because (a) it matches where credentials physically live (Section 2 recommendation), (b) the A2P 10DLC brand registration is a Bridge-mediated workflow anyway (provider portals are HTTP-only), (c) it preserves the audit trail (config-change events emit to ADR 0049's substrate at Bridge, not scattered across nodes).

**Open question for the hand-off:** does WS-E ship with a Bridge admin UI form in Phase 2.1, or is the Phase 2.1 default a Bridge operator-authored JSON blob (advanced-user only) with the UI deferred to Phase 2.2? Vendor survey assumes 10-100 tenants × 50-500 msg/mo; the hand-off should match that with a UI-first default if FED has Stage 06 capacity, JSON-first if not.

---

## Section 4 — Compliance gates

**Gate-time taxonomy the hand-off must adopt:**

| Gate | When enforced | Failure mode | Owner |
|---|---|---|---|
| **A2P 10DLC registration** (SMS) | Substrate-time at adapter init + first-send | Fail-closed; refuse to dispatch any SMS until `a2p_10dlc.registration_status == Approved` | Adapter (`providers-sms-twilio` validates `MessagingProviderConfig.sms.a2p_10dlc`) |
| **CAN-SPAM unsubscribe header** (Email) | Substrate-time at content rendering | Fail-closed; refuse to dispatch email without `List-Unsubscribe` header | `blocks-messaging` content-rendering layer |
| **CAN-SPAM physical-address footer** | Substrate-time at content rendering | Fail-closed; refuse to dispatch email without sender postal address in body | Same |
| **TCPA per-recipient consent** | Consumer-time at dispatch initiation | Fail-closed at dispatch; substrate exposes `IConsentLedger.CheckAsync` per recipient | Substrate provides hook; consumer cluster (Owner Cockpit, work orders) owns consent UX |
| **GDPR cross-border data-residency** | Substrate-time at adapter selection | Fail-closed if tenant has EU recipients and adapter region is non-EU | Adapter config validation; hand-off must name which adapters publish residency metadata |
| **DKIM/SPF/DMARC verified** (Email) | Substrate-time at startup health check | Degraded mode: dispatch allowed but emits `DeliverabilityRiskElevated` audit; alerts ops | Adapter (`providers-email-postmark` polls vendor verification API) |

**Substrate-time vs. vendor-time split:**

- **Substrate-time (Sunfish enforces):** A2P 10DLC registration check, CAN-SPAM header injection, GDPR residency match, consent-ledger lookup.
- **Vendor-time (provider enforces):** Postmark's own bounce-and-complaint suppression list, Twilio's carrier-level filtering, provider-side opt-out keyword handling (STOP/HELP/UNSUBSCRIBE).
- **Consumer-time (downstream cluster enforces):** consent UX (gathering opt-in), DPA acceptance flow, jurisdiction-aware language selection.

**The hand-off must name two failure-mode policies:**

1. **At startup:** does WS-E refuse to start an adapter whose tenant config fails compliance validation (fail-closed startup) or start in degraded-no-dispatch mode (start + alert)? Recommend fail-closed startup with a Bridge admin override for "I know the registration is in flight" cases.
2. **At first-send:** if a tenant attempts dispatch without consent for a recipient, does the substrate **silently drop** + audit, or **return `ComplianceGateRejected`** to the caller? Recommend the latter; silent-drop creates ghost-message bugs.

---

## Section 5 — Test/dev shim

**Three layers the hand-off must scope:**

1. **In-process reference gateway.** ADR 0052 implementation checklist already names `In-memory reference IOutboundMessageGateway + IInboundMessageReceiver shipped in foundation-integrations for tests/demos`. Hand-off must add: (a) deterministic message-id generation for snapshot tests, (b) controllable clock injection for retry-test scenarios, (c) explicit `OutboundDispatchStatus`-transition stepping for testing consumer state machines, (d) inbound-envelope injection API for testing routing logic. Suggested name: `Sunfish.Foundation.Integrations.Messaging.Testing.FakeMessageGateway`.

2. **Capture-and-replay harness.** ADR 0052 A5 mandates a Postmark↔SendGrid parity test with byte-equivalence exclusion list. Hand-off must specify: (a) capture-storage location (recommend: `shipyard/icm/_state/messaging-fixtures/<timestamp>-<provider>-<msgtype>.json`), (b) fixture rotation policy (refresh on adapter version bump; not in CI hot path), (c) the exact exclusion-list config file path.

3. **HTTP-level integration recording.** For adapter-level tests that need real HTTP behavior without live API calls. Two viable shapes: **WireMock.NET** (stub-server, declarative) or **VCR-style cassettes** (record-once-replay-many, captures real provider responses). Recommend **WireMock.NET** because (a) the .NET ecosystem already standardizes on it, (b) cassettes coupling fixtures to provider-API-versions creates churn, (c) WireMock's request-matching declarative shape doubles as adapter-contract documentation. Hand-off must name the stub-server lifecycle (per-test-class vs. per-test) and the stub-config storage path.

**Inner-loop posture:** local-first dev means no live Postmark / Twilio API keys in the inner loop. Hand-off must enumerate which `dotnet test` invocations are allowed to hit real vendor sandboxes (recommend: explicit opt-in env var `SUNFISH_MESSAGING_LIVE_TESTS=1`; default `dotnet test` uses fakes-only).

---

## Section 6 — ERPNext coexistence path

**The W#60 pivot** (per `2026-05-13_w60-final-stack-foss-substitutability-recheck.md`) positions Sunfish to layer over ERPNext/Frappe. Frappe ships with `Frappe Email` (SMTP/IMAP-based outbound + inbound parsing) and `Frappe SMS` (HTTP-gateway abstraction). These are mature, ERPNext-domain-integrated, and used by Frappe-native modules for workflow notifications, comment alerts, and password-reset flows.

**The decision the hand-off must adopt: composes (not replaces, not coexists ignorantly).**

| Path | Description | Verdict |
|---|---|---|
| **Replace** | WS-E becomes the only messaging substrate; Frappe Email/SMS disabled | **Rejected.** Disables Frappe-native workflows; tenants lose ERPNext comment notifications, password-resets, Frappe-domain alerts. High blast radius. |
| **Coexist ignorantly** | WS-E and Frappe Email/SMS run side-by-side with no awareness of each other | **Rejected.** Duplicate sends become invisible; audit trail bifurcates (Sunfish audit substrate sees WS-E events; Frappe `Communication` doctype sees Frappe events). Tenants can't reconstruct "what did this customer receive from us this month?" |
| **Compose** | WS-E owns audit-substrate-integrated bidirectional substrate for Sunfish-domain messages; Frappe Email/SMS retain workflow notifications; a bridge mirrors Frappe events into the Sunfish audit substrate | **Recommended.** |

**Compose-path mechanics the hand-off must specify:**

1. **Sunfish-domain vs. Frappe-domain message ownership.** Domain ownership rule: any message tied to a Sunfish-substrate concern (work orders, vendor coordination, leasing applications, audit-logged compliance notices, statements) goes through `IOutboundMessageGateway`. Any message tied to a Frappe-domain workflow (Frappe `Communication` doctype, password resets, Frappe comment-notify) stays in Frappe Email/SMS. The hand-off must publish the **domain-routing matrix** so consumers know which channel to use.
2. **Frappe→Sunfish event mirror (Phase 2.1).** A subscriber to Frappe's `Communication` doctype hooks emits read-only events into Sunfish's audit substrate as `FrappeMessageDispatched` audit records (so the unified audit trail sees both). No content stored Sunfish-side; only metadata + Frappe reference.
3. **Sunfish→Frappe event mirror (Phase 2.3).** WS-E egress events emit a parallel record into Frappe's `Communication` doctype so Frappe's UI can show "Sunfish sent X to this customer." Deferred to Phase 2.3 because Frappe doctype mutation from external code is invasive — needs design.
4. **Shared sender identity, separate dispatch path.** Both substrates send from `notices.<tenant-domain>.com` (the same sender domain), but via separate transactional accounts (Frappe → Frappe's configured SMTP; WS-E → Postmark adapter). This isolates deliverability blast-radius per A3 (a Frappe-side spam complaint doesn't poison Sunfish's Postmark sender reputation).
5. **Compliance gate alignment.** Frappe's email config has no A2P 10DLC awareness (Frappe SMS bypasses TCPA registration entirely in its current shape). Hand-off must name: WS-E's compliance gates apply to WS-E traffic; Frappe Email/SMS is **out of scope** for substrate-time enforcement; tenant must configure Frappe-side compliance independently. This is a documentation deliverable, not a code deliverable.

---

## Open questions (named for Admiral to settle when authoring the WS-E hand-off)

| ID | Question | Recommendation |
|---|---|---|
| **OQ-WSE-1** | Egress route default: NodeDirect vs. BridgeRelayed | **BridgeRelayed** (Section 2); simplifies credential locality + works on NAT'd nodes |
| **OQ-WSE-2** | Relay-to-node forwarding transport: sync-daemon piggyback vs. dedicated channel | **Dedicated `messaging-inbound` SSE channel** (Section 2) |
| **OQ-WSE-3** | Voice channel in `MessageChannel` enum: extend now or defer to separate ADR | **Defer to separate ADR**; voice semantics diverge enough to warrant its own contract surface |
| **OQ-WSE-4** | Tenant configuration authoring: Bridge admin UI vs. JSON blob (Phase 2.1 scope) | **Bridge admin UI if FED Stage 06 capacity allows; JSON blob otherwise**, with UI as Phase 2.2 follow-up |
| **OQ-WSE-5** | Compliance gate startup posture: fail-closed at start vs. degraded-no-dispatch with alert | **Fail-closed at start**, with operator override flag for "registration in flight" |
| **OQ-WSE-6** | First-send compliance violation: silent-drop+audit vs. `ComplianceGateRejected` return | **Return `ComplianceGateRejected`**; silent-drop creates ghost-message bugs |
| **OQ-WSE-7** | HTTP-level integration test layer: WireMock.NET vs. VCR-style cassettes | **WireMock.NET**; less version-coupling, doubles as contract documentation |
| **OQ-WSE-8** | Live-vendor-sandbox test gating | **Opt-in env var `SUNFISH_MESSAGING_LIVE_TESTS=1`**; default test runs are fakes-only |
| **OQ-WSE-9** | ERPNext coexistence: replace / coexist / **compose** | **Compose**; Sunfish owns audit-integrated bidirectional; Frappe retains workflow notifications |
| **OQ-WSE-10** | Frappe↔Sunfish event mirror: Phase 2.1 read-only one-way vs. bidirectional | **Phase 2.1 read-only (Frappe→Sunfish)**; bidirectional in Phase 2.3 |
| **OQ-WSE-11** | Per-channel dispatch timeout budgets | **Email 30s / SMS 10s / Voice (when added) 60s**; defaults overridable per `MessagingProviderConfig` |
| **OQ-WSE-12** | Error-model taxonomy enumeration — does the hand-off freeze the 8-variant `OutboundDispatchFailure` set, or leave room for vendor-specific extensions? | **Freeze the 8 variants**; vendor-specific causes map into the closest variant + audit-record carries vendor detail |

---

## Sources cited

**Primary (Sunfish authoritative):**

1. ADR 0052 — Bidirectional Messaging Substrate (Accepted 2026-04-29, A1–A5 + Minor amendments landed same day). `shipyard/docs/adrs/0052-bidirectional-messaging-substrate.md`. Retrieved 2026-05-17.
2. ADR 0013 — Foundation.Integrations + Provider-Neutrality Policy (Accepted). `shipyard/docs/adrs/0013-foundation-integrations.md`. Retrieved 2026-05-17.
3. ADR 0031 — Bridge Hybrid Multi-tenant SaaS (supersedes ADR 0026). Cited via ADR 0026 supersession header. Retrieved 2026-05-17.
4. ADR 0043 — Unified threat model (T2-MSG-INGRESS catalog entry per ADR 0052 A1). Retrieved 2026-05-17.
5. ADR 0049 — Audit Trail Substrate. Cited via ADR 0052 §"Audit-substrate integration." Retrieved 2026-05-17.
6. ONR research — ADR 0052 outbound messaging substrate (2026-05-17). Status beacon `coordination/inbox/onr-status-2026-05-17T20-45Z-adr-0052-outbound-messaging-research.md`. **Primary (ONR's own prior deliverable).**

**Secondary (cluster + workstream context):**

7. `2026-05-13_w60-final-stack-foss-substitutability-recheck.md` — W#60 ERPNext/Frappe pivot final stack. `shipyard/icm/01_discovery/output/`. Retrieved 2026-05-17.
8. Admiral directive `admiral-directive-2026-05-17T21-08Z-onr-wse-handoff-prerequisite-scoping.md` — this research's source directive.
9. Council review of ADR 0052 (`0052-council-review-2026-04-29.md`) referenced via the ADR 0052 amendment header.

**Tertiary (external — vendor docs, regulatory text):**

10. Postmark Inbound webhook reference (cited in ADR 0052 References).
11. Twilio Programmable Messaging webhook reference (cited in ADR 0052 References).
12. CTIA A2P 10DLC registration framework (tertiary; cited from prior ONR vendor survey 2026-05-17).
13. CAN-SPAM Act 15 U.S.C. §§ 7701–7713 (statute; tertiary citation; substrate-relevant clauses are `List-Unsubscribe` header + physical-address requirement).
14. TCPA 47 U.S.C. § 227 + 47 C.F.R. § 64.1200 (statute; tertiary; per-recipient consent requirement).
15. Frappe Framework docs — `Communication` doctype + Frappe Email + Frappe SMS modules (tertiary; consulted via the W#60 stack research).

---

*ONR research delivered 2026-05-17. Scoping deliverable; not the hand-off itself. Hand-off authorship is Admiral territory; this memo names what the hand-off must settle to be buildable. Stand by for clarification questions.*
