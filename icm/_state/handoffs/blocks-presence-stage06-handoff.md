# Hand-off — `blocks-presence` PresenceSession + Heartbeat + IPresenceService + IActivityTracker (Phase 3 substrate, multi-actor collaboration prereq)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build` (gated on `blocks-people-foundation` PR 1 + `foundation-events` available — see Pre-build checklist)
**Workstream:** W#60 P4 — Path II native domain, Phase 3 substrate slice (multi-actor collaboration foundation)
**Spec source:** Task brief 2026-05-17 (this hand-off is the design spec; no Stage 02 doc precedes it — substrate scope; design carried inline per the cohort precedent for `blocks-people-foundation` substrate hand-off)
**ADR:** [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) (Proposed; CO ratified 2026-05-16) §1 (7-cluster decomposition; `blocks-presence` is a Phase 3 substrate slice that sits alongside the cluster surface; collaboration-tier substrate), §3 (clean-room discipline). Memory note [`project_phase_2_commercial_scope.md`](../../../) §"Multi-actor delegation" — Phase 2 commercial use case requires Bookkeeper + Tax-advisor + Spouse multi-actor delegation; live "who's-in-the-app-right-now" is the perceptual precondition for that.
**CRDT conventions:** [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) §1 (ULIDs), §2 (tombstones — applied with TTL semantics; see §CRDT discipline below), §3 (version vectors), §4 (append-only Heartbeat sub-collection), §5 (stable string codes), §13 (CRDT envelope), §14 (tenant isolation)
**Event bus:** [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) §1 (envelope), §2 (naming), §3 (catalog upkeep). This hand-off introduces a NEW `Presence.*` event sub-catalog under §3 (proposed §3.7) — PR 4 ships the catalog edit.
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~6–8h sunfish-PM (4 PRs + ~35–45 tests + docs + ledger flip)
**PR count:** 4 PRs
**Pre-merge council:** NOT required (substrate scope; low-risk surface; mirrors the `blocks-people-foundation` substrate-only pattern). **Security-engineering spot-check ONLY on PR 2** (the `IPresenceService.RegisterAsync` / `HeartbeatAsync` / `DisconnectAsync` write path — tenant-isolation of presence visibility + the "no PII in presence events" invariant; one-perspective Sonnet spot-check, ≤15 minutes, no full council). Standard COB self-audit applies.
**Standing patterns applied:** `pattern-001` (cluster scaffold + repository + DI; PR 1), `pattern-005` (`Add<Block>()` DI umbrella; PR 4), `pattern-006` (`apps/docs/blocks/<cluster>/overview.md` authoring; PR 4).
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-presence"
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep presence
gh pr list --state open --search "blocks-presence in:title,body"
```
Expected: zero matches in all three. If any `blocks-presence-*` package exists or any PR is in-flight, **STOP** — file `cob-question-*` before opening PR 1. Note: `packages/blocks-crew-comms/Presence/PresenceBus.cs` exists (peer-to-peer HEARTBEAT-frame layer per ADR 0076) — it is a DIFFERENT surface (transport-tier presence, not application-tier presence). Do NOT touch it. See §H3.

---

## Context

### What `blocks-presence` is — application-level "who's online, viewing what, right now"

`blocks-presence` ships the **application-tier presence substrate** for the Anchor local-first runtime: per-user activity + heartbeat + per-document/per-record "currently viewing/editing" indicators. It is the perceptual foundation for collaboration features — every "Jane is editing this lease," "Bob viewed this invoice 2 minutes ago," "Spouse is online in tenant A's books" surface that consumer clusters will compose on top in Phase 3 + Phase 4.

This hand-off intentionally scopes the substrate as a **thin, well-bounded collaboration prereq** rather than a full collaboration surface. The goals:

1. **Unblock multi-actor delegation perceptual UI** (Phase 2 commercial scope per the memory note: Bookkeeper, Spouse, Tax-advisor all share tenants and need to see each other's presence to avoid stepping on each other's writes).
2. **Provide a typed `IPresenceService` + `IActivityTracker` surface** for `blocks-crew-comms` to consume for TYPING / DELIVERED / SEEN indicators (today crew-comms' `PresenceBus` is a peer-to-peer transport-frame layer per ADR 0076 — application-tier presence is a separate concern).
3. **Be ready BEFORE any UI-layer collaboration surface ships** so consumer clusters (`blocks-property-leases` "X is editing this lease," `blocks-financial-ar` "Y viewed this invoice," `blocks-docs-*` "Z is reading this policy") have a stable interface to call.

### What this hand-off ships (binding)

Per the Phase 3 substrate scope:

1. **`PresenceSession` entity** — the ephemeral per-(user, replica, app-instance) session record. Identified by `PresenceSessionId` (ULID); scoped by `TenantId` + `PartyId`; tracks `Status` (Online / Away / Offline), `ConnectedAt`, `LastHeartbeatAt`, `ReplicaId`, `ClientInfo` (UA-like string; opt-in), `Capabilities` (string-code set). **Ephemeral** — TTL-evicted after the heartbeat timeout window; never indefinitely retained (see CRDT discipline §3). (PR 1.)
2. **`Heartbeat` entity** — append-only heartbeat record. Carries `HeartbeatId` (ULID), `PresenceSessionId`, `TenantId`, `PartyId`, `RecordedAt`, optional `ActiveActivityId?` (links back to the activity the user was viewing at heartbeat time). Strictly append-only per CRDT §4 — never updated, never deleted (other than retention pruning, see §H4). (PR 1.)
3. **`Activity` entity** — per-document / per-record activity indicator. Carries `ActivityId` (ULID), `PresenceSessionId`, `TenantId`, `PartyId`, `ResourceKind` (string code, e.g., `"lease"`, `"invoice"`, `"document"`), `ResourceId` (opaque string), `Kind` (viewing | editing — string codes; see CRDT §5), `StartedAt`, `EndedAt?` (null while active). Append-only-with-tombstone semantics like PartyRole. (PR 3.)
4. **`IPresenceRepository`** — read+write surface for sessions + heartbeats. Default `InMemoryPresenceRepository` backing it (`ConcurrentDictionary` per cohort discipline). (PR 1.)
5. **`IPresenceService`** — high-level lifecycle facade: `RegisterAsync` (create session), `HeartbeatAsync` (touch session + append Heartbeat row), `DisconnectAsync` (mark session Offline + emit event), `GetOnlineRosterAsync` (current per-tenant "who's online"), `GetSessionAsync`. Emits `Presence.UserConnected` / `Presence.HeartbeatRecorded` (optional; throttled) / `Presence.UserDisconnected` events. (PR 2.)
6. **`IActivityTracker`** — high-level "what is the user looking at right now" facade: `StartViewingAsync(resourceKind, resourceId)`, `StopViewingAsync(activityId)`, `GetActiveOnResourceAsync(resourceKind, resourceId)` (returns party-IDs currently viewing/editing the named resource), `GetActiveByPartyAsync(partyId)` (returns the resources a party is currently focused on). Emits `Presence.UserViewing` / `Presence.UserStoppedViewing` events. (PR 3.)
7. **Background TTL evictor** — `PresenceTtlEvictionService` (`IHostedService`-shaped — or equivalent for Anchor's runtime model): scans `IPresenceRepository` periodically (30s default), marks sessions whose `LastHeartbeatAt < (now - TTL)` as Offline, emits `Presence.UserDisconnected` for each. Configurable TTL; default 90s (3 missed heartbeats at the default 30s cadence). (PR 2.)
8. **DI umbrella** — `AddBlocksPresence()` registers `IPresenceRepository`, `IPresenceService`, `IActivityTracker`, `IPresenceEventPublisher` + `InMemoryPresenceEventPublisher`, the TTL evictor, and the default `PresenceOptions` (heartbeat cadence + TTL). (PR 4.)
9. **`apps/docs/blocks/blocks-presence/overview.md`** — cluster docs page; cite ADR 0088 §1; cite the memory note on multi-actor delegation; document the v1 scope + deferred features explicitly. (PR 4.)
10. **`Presence.*` event sub-catalog** — appended to `_shared/engineering/cross-cluster-event-bus-design.md` §3 as new sub-section §3.7. Events: `Presence.UserConnected`, `Presence.HeartbeatRecorded` (optional surface; throttled per §Idempotency-key catalog below), `Presence.UserDisconnected`, `Presence.UserViewing`, `Presence.UserStoppedViewing`. (PR 4.)
11. **Ledger flip** — update `icm/_state/active-workstreams.md` (via the source `W*.md` file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`) to mark `blocks-presence` row `built` with the 4 PR numbers. (PR 4.)

### What this hand-off does NOT ship

The scope is deliberately tight. The following are **explicitly out of scope** and deferred to later hand-offs (Phase 4 or later) or recognized as separate cluster concerns:

- **Live cursor positions** (Google-Docs-style multi-cursor / multi-selection rendering). Requires a different substrate (CRDT-aware cursor projection + per-keystroke broadcast) and is a UI-layer concern that consumer clusters wire on top of `IActivityTracker.GetActiveOnResourceAsync` later. Deferred per §H5.
- **Voice + video calling.** Out-of-band media tier; not application-level presence. Future workstream if/when needed; outside Phase 3 substrate.
- **Push notifications** (mobile / desktop OS push). Different transport tier; depends on `accelerators/anchor-mobile-ios/` push wiring + a notification-routing substrate that does not yet exist. Deferred.
- **Per-resource access logging / audit trails.** `IActivityTracker` is for live "who's looking at this right now" — not "show me every party that has ever viewed this resource in the last 90 days." The latter is a `kernel-audit` concern, owned by `AuditEventType` and the audit substrate; not duplicated here. See §H6.
- **Idle-detection / Away-state auto-transitions** based on input-device inactivity (mouse/keyboard idle). The presence service ships **client-driven** state — clients tell the service "I am Online / Away / Offline." Idle-detection logic lives in the UI tier (Anchor's React or MAUI shell), and calls `IPresenceService.SetStatusAsync` (which is included as a stretch method — see PR 2). The UI-tier idle-detection itself is out of scope.
- **Multi-tenant presence federation.** A party with sessions in tenant A and tenant B has TWO separate sessions (one per tenant); the substrate does NOT roll them up into a "global online" status. Cross-tenant correspondence is a Bridge-tier concern (per `party-model-convention.md` §10 Q7); not here.
- **Persistence beyond `InMemory*`.** v1 ships the in-memory backing repository only. SQLite-backed persistence (for Heartbeat retention windows + session-history queries) is a follow-on workstream paired with the broader Phase 3 SQLite repository rollout. Ephemeral sessions don't need durable storage; Heartbeats with retention windows do — see §H4.
- **`blocks-crew-comms` integration code.** This hand-off ships the typed surface (`IPresenceService` + `IActivityTracker`); the crew-comms cluster's TYPING/DELIVERED/SEEN follow-up (which consumes the surface) is a SEPARATE follow-on hand-off owned by the crew-comms maintainers, not this substrate.
- **UI components.** No Razor / React / MAUI presence components in this hand-off. Just the data + service layer. UI compositions ship in cluster-specific follow-on hand-offs (e.g., `blocks-property-leases-presence-indicator-*`).
- **Permission / capability gating** beyond tenant-isolation. Per-resource ACL filtering of presence visibility ("Bookkeeper can see Spouse is online; Tax-advisor cannot see Bookkeeper") is deferred to a follow-on workstream paired with the capability-graph wiring (per the memory note's `ICapabilityGraph.QueryAsync` pattern). v1 ships **tenant-scoped visibility only**: any party in the tenant can see any other party's presence in that tenant. Documented in PR 4's `apps/docs` page.

### Why a thin substrate now

1. **Phase 2 commercial scope explicitly needs multi-actor delegation perception.** Per the memory note: Bookkeeper, Spouse, Tax-advisor each on their own Anchor with capability-trimmed UIs into shared tenants. Without a "Bob is also looking at this rent ledger right now" indicator, two parties can issue conflicting writes (e.g., bookkeeper edits a JE while spouse is reconciling the same bank-statement batch). The presence substrate is the perceptual layer that prevents that confusion.
2. **`blocks-crew-comms` TYPING + DELIVERED + SEEN need an application-tier presence reference.** The crew-comms `PresenceBus` (per ADR 0076) is a TRANSPORT-tier liveness signal (peer-to-peer signed HEARTBEAT frames; 30s broadcast / 45s TTL; informs the chat-message router). It is correct and sufficient for chat-message routing — but it is **not** the right substrate for "who's currently looking at the AR aging report." The application-tier `IPresenceService` ships here; the crew-comms TYPING/DELIVERED/SEEN ux composes on top in a follow-on PR.
3. **The substrate is stable.** Live presence has a well-understood shape (heartbeat + TTL + status enum); the entity model is unlikely to churn as consumers attach. Shipping a thin clean version now means consumer clusters never have to write a stub `IPresenceService` and later relocate.
4. **Low risk.** No PII at rest (presence is ephemeral; the Heartbeat retention window is configurable + bounded; no money + no contract-of-record); tenant-isolation is enforced at the repository boundary; no auth surface beyond tenant context. Security-engineering spot-check on PR 2 is sufficient — full council not required.

### Cluster naming + placement

The package directory: `packages/blocks-presence/`. Namespace: `Sunfish.Blocks.Presence`. DI extension: `AddBlocksPresence()`.

**Why `blocks-presence` (NOT `blocks-people-presence`, NOT `foundation-presence`, NOT `blocks-collaboration`):**

1. **Not under `blocks-people-*`.** Presence is not a *people-cluster* concept — it's a *session-and-activity* concept that REFERENCES `PartyId` from `blocks-people-foundation`. The people cluster owns identity; presence owns ephemeral state. Putting it under `blocks-people-*` would muddle the boundary (and would force a `blocks-people-presence` name that buries it in a follow-on Phase 3 slot).
2. **Not `foundation-*`.** Foundation packages are framework-agnostic primitives (no business model). Presence carries domain semantics (Activity referencing ResourceKind from cluster catalogs; CRDT envelope on every row) — it belongs in the `blocks-*` tier, just like `blocks-crew-comms` does.
3. **Not `blocks-collaboration`.** "Collaboration" is too broad — it would invite live-cursor + voice/video + push-notification scope creep on the first follow-on PR. `blocks-presence` is the precise primitive: heartbeat + activity-pointer. Collaboration UI surfaces compose on top; this substrate doesn't own them.
4. **Singular cluster (no `-foundation` suffix).** Unlike `blocks-people-foundation` (which carves the minimum slice of a larger `blocks-people-*` cluster shipping in parallel), `blocks-presence` is **the entire cluster** for v1. The substrate IS the cluster. If future scope expands (e.g., `blocks-presence-federation` for cross-tenant Bridge presence), it composes on top with the same precedent as Phase-3 people sub-slices.

### Cross-cluster integration (binding)

- **Consumes `PartyId` from `blocks-people-foundation`.** Every `PresenceSession` carries `PartyId: PartyId` (the canonical type from `Sunfish.Blocks.People.Foundation`). This hand-off **cannot ship** until `blocks-people-foundation` PR 1 lands (`PartyId` is a hard dep — see Pre-build checklist step 1). If `blocks-people-foundation` is still pre-merge at the moment COB opens PR 1, halt and file `cob-question-*` per §H1.
- **Consumes `TenantId` from `foundation-multitenancy`.** Same pattern as the people-foundation hand-off (per its Pre-build step 6). If `TenantId` is not yet at the canonical namespace, file `cob-question-*` per the people-foundation hand-off precedent.
- **Emits `Presence.*` events via `IPresenceEventPublisher`.** Consumed by:
  - **`blocks-crew-comms`** for application-tier TYPING / DELIVERED / SEEN composition (follow-on hand-off, not this one).
  - **Future multi-actor collaboration UI surfaces** (`blocks-property-leases` "X is editing this lease," `blocks-financial-ar` "Y is viewing this invoice," `blocks-docs-*` "Z is reading this policy"). Each consumer subscribes to `Presence.UserViewing` events scoped to its `ResourceKind` and renders its own UI affordance.
  - **`accelerators/anchor` Top-bar "Crew" indicator** (future UI work; consumes `IPresenceService.GetOnlineRosterAsync` directly).
- **Does NOT consume from `blocks-crew-comms`.** Application-tier presence is independent of transport-tier presence. The two layers will be wired in a follow-on (in a single direction: crew-comms is informed of application-tier disconnect events; presence does NOT subscribe to crew-comms transport-tier events).
- **Does NOT consume from `kernel-audit`.** Presence events are NOT audit-logged by default — they are ephemeral and high-volume (every heartbeat × every party). The audit substrate is for material business events (party-created, invoice-posted, role-attached); presence is too high-cardinality to flow through audit. Per-resource view-tracking for *audit purposes* is a separate `AuditEventType` (e.g., `Audit.ResourceViewed` if/when needed) owned by `kernel-audit`, NOT this cluster. See §H6.

### Why no full council

Per the security spot-check policy (memory note `feedback_council_reviews_use_best_model_xhigh`): full councils are for high-judgment / cross-package design / breaking-change / crypto / security-primitive work. The presence substrate is:

- Greenfield (no API churn).
- Substrate-scoped (no cross-package design judgment).
- No crypto / key handling / security primitive.
- Bounded attack surface: tenant-scoped reads only; no PII at rest; no money path; no contract-of-record path.
- Pattern-matches the `blocks-people-foundation` substrate hand-off (which also shipped without a council).

**The single exception is PR 2's `IPresenceService` write path** — `RegisterAsync` / `HeartbeatAsync` / `DisconnectAsync`. The tenant-isolation invariant + the "no PII in presence event payloads" invariant are load-bearing. Security-engineering spot-check (one-perspective, Sonnet, medium effort; ≤15 minutes) verifies both. NOT a full council; NOT a 4-perspective gate. One reviewer asking: "does a tenant-A caller observe tenant-B presence?" and "does the event payload contain anything beyond PartyId + TenantId + Timestamps + opaque ResourceId?"

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify `blocks-people-foundation` is BUILT.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-people-foundation/Models/PartyId.cs
   grep -E "^\| blocks-people-foundation \|" /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/active-workstreams.md
   ```
   Expected: package exists; `PartyId.cs` exists; ledger row says `built`. If not built, **STOP** — this hand-off is gated. File `cob-question-2026-05-XXTHH-MMZ-blocks-presence-foundation-not-built.md` to coordination inbox.

2. **Verify no parallel-session work on `blocks-presence`.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-presence"
   gh pr list --state open --search "blocks-presence in:title,body"
   ```
   Expected: zero matches in both. If anything is in-flight, **STOP** + file `cob-question-*`.

3. **Verify `packages/blocks-crew-comms/Presence/PresenceBus.cs` is present and untouched.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-crew-comms/Presence/PresenceBus.cs
   ```
   Expected: exists. **DO NOT MODIFY**. The two presence surfaces (peer-to-peer transport-tier per ADR 0076 vs. application-tier per this hand-off) coexist; they are not redundant. Conflation would be a regression. See §H3.

4. **Verify ADR 0088 status.**
   ```bash
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```
   Expected: `status: Proposed` (CO ratified design 2026-05-16; status-flip is housekeeping). Hand-off is `ready-to-build` regardless — CO directive operative.

5. **Verify CRDT + event-bus convention docs are in place.**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/_shared/engineering/crdt-friendly-schema-conventions.md
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/_shared/engineering/cross-cluster-event-bus-design.md
   ```
   Expected: both exist. PR 4 cites them by section in the commit message + the `apps/docs` page.

6. **Verify `foundation-multitenancy` is available (for `TenantId` reference type).**
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-multitenancy/
   grep -rln "TenantId\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-multitenancy/ 2>/dev/null | head -3
   ```
   Expected: package exists; `TenantId` type available. Same pattern as the people-foundation hand-off pre-build step 6.

7. **Verify `foundation-events` or equivalent event-substrate is available** (whatever the project's canonical event-bus interface is at the time of build).
   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^(foundation-events|kernel-event-bus)"
   ```
   Expected: `kernel-event-bus/` exists (canonical event-substrate per the W#57 ledger). If `foundation-events/` is named differently in the repo, COB locates it via `grep -rln "IDomainEventPublisher\|IEventLog\b" packages/kernel-* packages/foundation-* | head -5` and adjusts references in PR 4's DI registration. Note: PR 1–3's event-publisher abstraction is local (`IPresenceEventPublisher`); the canonical-event-substrate wiring lands in PR 4's DI umbrella.

8. **Verify `ITenantContextAccessor` (or equivalent) is registered.**
   ```bash
   grep -rln "ITenantContextAccessor\|ITenantContext\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-multitenancy/ 2>/dev/null | head -3
   ```
   Expected: present. If absent or named differently, follow the people-foundation hand-off §H2 fallback procedure.

9. **Confirm `but status` (or `git status`) is clean** and current branch is `main` (or a fresh worktree from `main`, NOT from `gitbutler/workspace` HEAD per `feedback_worktree_base_main_not_gitbutler`).

10. **Read this whole hand-off + the §CRDT discipline + the §Idempotency-key catalog before opening PR 1.** The substrate is small; the discipline is what makes it correct.

---

## Per-PR deliverables

This hand-off splits into **4 PRs** by responsibility. PRs 1 + 2 + 3 are sequential (PR 2 needs PR 1's entities + repository; PR 3 needs PR 2's service surface for ActivityTracker→Session linkage). PR 4 sequences last (umbrella DI + docs + ledger flip).

---

### PR 1 — Package scaffold + `PresenceSession` + `Heartbeat` entities + `IPresenceRepository` + `InMemoryPresenceRepository`

**Estimated effort:** ~2–2.5h
**Scope:** create `packages/blocks-presence/` package scaffold; ship the canonical `PresenceSession` + `Heartbeat` records; ship `IPresenceRepository` + `InMemoryPresenceRepository` (read+write); validation; NO service layer yet (that's PR 2)
**Commit subject:** `feat(blocks-presence): add PresenceSession + Heartbeat entities + IPresenceRepository (Phase 3 substrate scaffold)`
**Branch:** `cob/blocks-presence-scaffold-entities`
**Standing-pattern tag:** `@standing-pattern: pattern-001`

#### Package scaffold

- `packages/blocks-presence/Sunfish.Blocks.Presence.csproj` — .NET 11 preview; matches the conventions of `blocks-people-foundation/Sunfish.Blocks.People.Foundation.csproj`.
- `packages/blocks-presence/tests/Sunfish.Blocks.Presence.Tests.csproj`.
- `packages/blocks-presence/README.md` — references ADR 0088 §1; cites the memory note on multi-actor delegation; links to `apps/docs/blocks/blocks-presence/overview.md` (placeholder until PR 4 ships).
- `packages/blocks-presence/NOTICE.md` — attribution stub (see §License posture; inspiration-only acknowledgement of Slack/Discord/Matrix presence patterns; no code derivation).
- Add to `Sunfish.slnx` (or `Sunfish.sln`) via `dotnet sln add ...`.

#### New types — strongly-typed IDs

All ULID-backed, mirroring the existing `PartyId` / `EmailAddressId` pattern from the people-foundation hand-off.

- `Models/PresenceSessionId.cs` — readonly record struct + JSON converter; static `NewId()` returns a ULID-string-backed instance.
- `Models/HeartbeatId.cs`.
- `Models/ActivityId.cs` — declared in PR 1 even though `Activity` ships in PR 3, so that `Heartbeat.ActiveActivityId?` can reference it without a forward-declared placeholder.

**ULID implementation:** mirror the people-foundation hand-off precedent — use the project's existing ULID helper if one exists; else `Cysharp.Ulid` (BSD-3-Clause). DO NOT use `Guid.NewGuid()` for these IDs (per CRDT §1).

#### New types — status + kind enums (stable string codes)

```csharp
namespace Sunfish.Blocks.Presence;

/// <summary>
/// Lifecycle status of a <see cref="PresenceSession"/>.
/// </summary>
/// <remarks>
/// Stored as a stable string code per crdt-friendly-schema-conventions.md §5.
/// </remarks>
public enum PresenceStatus
{
    /// <summary>The session is actively heartbeating; party is present and interactive.</summary>
    Online,

    /// <summary>Client signalled "Away" (or idle-detection upstream sent it); session retained but de-emphasized.</summary>
    Away,

    /// <summary>Session has disconnected (client explicit) or TTL-evicted (heartbeat lapsed).</summary>
    Offline,
}
```

Persisted form: lowercase string codes (`"online"` / `"away"` / `"offline"`) via a JSON converter `PresenceStatusJsonConverter`.

```csharp
public enum ActivityKind
{
    /// <summary>Read-only viewing — no edits in flight.</summary>
    Viewing,

    /// <summary>Editing the resource (intent or in-progress writes).</summary>
    Editing,
}
```

Persisted form: lowercase string codes (`"viewing"` / `"editing"`). `ResourceKind` (e.g., `"lease"`, `"invoice"`, `"document"`) is a **plain string** (no enum) per CRDT §5 — consumer clusters define their own resource-kind codes; presence does NOT enumerate them centrally. Shape validation only (kebab-case + lowercase; no enum membership check at this layer).

#### New type — `PresenceSession` (the ephemeral session record)

```csharp
public sealed record PresenceSession
{
    // Identity
    public required PresenceSessionId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PartyId PartyId { get; init; }                  // from Sunfish.Blocks.People.Foundation
    public required string ReplicaId { get; init; }                 // node identity; opaque string from the runtime
    public required string? ClientInfo { get; init; }               // opt-in UA-like string; OK to be null

    // Status
    public required PresenceStatus Status { get; init; }
    public required Instant ConnectedAt { get; init; }
    public required Instant LastHeartbeatAt { get; init; }
    public Instant? DisconnectedAt { get; init; }                   // set when transitioning to Offline

    // Capabilities — stable string codes; opaque to presence; consumer clusters interpret
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

    // CRDT envelope (per crdt-friendly-schema-conventions.md §13)
    // Note: PresenceSession is ephemeral; the "envelope" is lightweight.
    // Version + RevisionVector still apply for cross-replica merge semantics
    // on Status transitions (Online → Away → Offline).
    public required Instant CreatedAt { get; init; }
    public required PartyId CreatedBy { get; init; }                // typically same as PartyId (the party owns its session)
    public Instant UpdatedAt { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();
}
```

**No `DeletedAt` tombstone field on `PresenceSession`.** Per §CRDT discipline §1, presence sessions transition to `Status = Offline` rather than tombstoning; TTL eviction sets `Status = Offline` + `DisconnectedAt = now()`. Hard-delete of session rows IS allowed at the retention boundary (per §H4) — presence sessions are explicitly ephemeral; they are NOT business records.

#### New type — `Heartbeat` (the append-only beat record)

```csharp
public sealed record Heartbeat
{
    public required HeartbeatId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PresenceSessionId PresenceSessionId { get; init; }
    public required PartyId PartyId { get; init; }                  // denormalized for query-by-party
    public required Instant RecordedAt { get; init; }
    public ActivityId? ActiveActivityId { get; init; }              // optional link to current activity

    // CRDT envelope (envelope-lite — append-only per §4; no UpdatedAt/UpdatedBy)
    public required PartyId CreatedBy { get; init; }                // === PartyId in normal flow; admin tools may differ
    public required long Version { get; init; }
    // No RevisionVector — Heartbeat is strictly append-only, no merge ambiguity
}
```

**Heartbeat is strictly append-only** per CRDT §4. No `UpdatedAt`, no `DeletedAt`, no `RevisionVector`. Retention-window pruning (hard-DELETE of old Heartbeat rows past the configured retention bound) is the ONE exception to the no-hard-delete rule; documented in PR 1's csproj README + §H4.

#### `IPresenceRepository` — read+write

```csharp
namespace Sunfish.Blocks.Presence;

/// <summary>
/// Per-tenant repository for <see cref="PresenceSession"/> + <see cref="Heartbeat"/>.
/// All reads are tenant-scoped via the ambient ITenantContextAccessor; cross-tenant
/// queries return empty per crdt-friendly-schema-conventions.md §14.
/// </summary>
public interface IPresenceRepository
{
    // Sessions
    Task<PresenceSession?> GetSessionAsync(PresenceSessionId id, CancellationToken ct = default);
    Task<PresenceSession?> GetActiveByPartyAsync(PartyId partyId, string replicaId, CancellationToken ct = default);
    Task<IReadOnlyList<PresenceSession>> GetOnlineForTenantAsync(CancellationToken ct = default);
    Task UpsertSessionAsync(PresenceSession session, CancellationToken ct = default);
    Task<int> EvictExpiredSessionsAsync(Instant cutoff, CancellationToken ct = default);   // returns count of sessions transitioned to Offline

    // Heartbeats (append-only; no Update / Delete method on the interface)
    Task AppendHeartbeatAsync(Heartbeat heartbeat, CancellationToken ct = default);
    Task<IReadOnlyList<Heartbeat>> GetHeartbeatsForSessionAsync(
        PresenceSessionId sessionId,
        Instant? since = null,
        int maxRows = 100,
        CancellationToken ct = default);
    Task<int> PruneHeartbeatsOlderThanAsync(Instant cutoff, CancellationToken ct = default);    // retention prune
}
```

**The `PruneHeartbeatsOlderThanAsync` method IS the legal hard-DELETE path** per §H4 + the §CRDT discipline §3 (Heartbeat retention). It is the only method on the repository that is permitted to remove rows; all other writes are upsert-or-append.

#### `InMemoryPresenceRepository`

Backing store. Uses `ConcurrentDictionary` per cohort discipline.

```csharp
public sealed class InMemoryPresenceRepository : IPresenceRepository
{
    private readonly ConcurrentDictionary<(TenantId, PresenceSessionId), PresenceSession> _sessions = new();
    private readonly ConcurrentDictionary<HeartbeatId, Heartbeat> _heartbeats = new();
    private readonly ITenantContextAccessor _tenantContext;
    private readonly TimeProvider _time;

    // ... (every read filters by _tenantContext.Current; cross-tenant queries return empty)
    // ... (UpsertSessionAsync bumps Version + UpdatedAt per crdt-friendly-schema-conventions.md §13)
    // ... (AppendHeartbeatAsync rejects rows with duplicate HeartbeatId — idempotency at the repo layer)
}
```

**Per-tenant isolation discipline (binding):** every read method first checks `_tenantContext.Current` and filters the dictionary on the `(TenantId, …)` tuple key (sessions) or the `Heartbeat.TenantId` field (heartbeats). Cross-tenant reads return empty, not the row. Tests verify this explicitly.

#### Validation

**`Validation/PresenceSessionValidator.cs`**:

- `Status == Offline` ⇒ `DisconnectedAt` must be non-null.
- `Status != Offline` ⇒ `DisconnectedAt` must be null.
- `LastHeartbeatAt >= ConnectedAt`.
- `ReplicaId` non-empty.
- `Capabilities` entries must conform to stable-string-code shape (kebab-case + lowercase + max 64 chars; see PartyRoleValidator precedent in the people-foundation hand-off).

**`Validation/HeartbeatValidator.cs`**:

- `RecordedAt` non-default.
- `PresenceSessionId` and `PartyId` non-default.
- (No tenant-cross-check at the validator layer — that's the repository's job.)

#### Tests (PR 1)

`tests/PresenceSessionTests.cs`:
- `Create_OnlineSession_PassesValidation`.
- `Create_OfflineWithoutDisconnectedAt_FailsValidation`.
- `Create_OnlineWithDisconnectedAt_FailsValidation`.
- `Create_HeartbeatBeforeConnectedAt_FailsValidation`.
- `Create_EmptyReplicaId_FailsValidation`.
- `Create_CapabilityWithUppercase_FailsValidation`.
- `Create_CapabilityWithSpaces_FailsValidation`.

`tests/HeartbeatTests.cs`:
- `Create_ValidHeartbeat_Passes`.
- `Create_DefaultRecordedAt_Fails`.

`tests/InMemoryPresenceRepositoryTests.cs`:
- `UpsertSession_Then_GetSession_Roundtrips`.
- `UpsertSession_ThenUpsert_ReplacesPriorRow`.
- `UpsertSession_BumpsVersion_AndUpdatedAt`.
- `GetSession_CrossTenant_ReturnsNull` (tenant-isolation; session exists under tenant B; caller under tenant A gets null).
- `GetActiveByPartyAsync_ReturnsLatestSession`.
- `GetOnlineForTenantAsync_ExcludesOfflineRows`.
- `GetOnlineForTenantAsync_ExcludesOtherTenantsRows`.
- `EvictExpiredSessionsAsync_TransitionsStaleSessionsToOffline`.
- `EvictExpiredSessionsAsync_LeavesFreshSessionsAlone`.
- `EvictExpiredSessionsAsync_ReturnsCorrectCount`.
- `AppendHeartbeatAsync_Persists_AndIsQueryable`.
- `AppendHeartbeatAsync_DuplicateId_Idempotent` (no error; second append returns identical row).
- `GetHeartbeatsForSession_AppliesSinceCursor`.
- `GetHeartbeatsForSession_AppliesMaxRowsCap`.
- `GetHeartbeatsForSession_CrossTenant_ReturnsEmpty`.
- `PruneHeartbeatsOlderThanAsync_RemovesOldRows`.
- `PruneHeartbeatsOlderThanAsync_LeavesRecentRows`.
- `PruneHeartbeatsOlderThanAsync_ReturnsRemovedCount`.

Total new tests this PR: ~18–20.

#### Verification

- `dotnet build` succeeds across the solution.
- `dotnet test packages/blocks-presence/tests/` passes ~18–20 tests.
- `grep -r "Sunfish.Blocks.Presence" packages/` returns hits ONLY in the new package (no other packages consume it yet).
- `grep -r "blocks-crew-comms/Presence/PresenceBus.cs" packages/blocks-presence/` returns zero hits (no accidental import of the transport-tier surface).

#### PR description template

```
Add Sunfish.Blocks.Presence per ADR 0088 Path II (Phase 3 substrate slice).

PR 1 of 4 in the blocks-presence hand-off. Ships:

- Package scaffold: packages/blocks-presence/
- Strongly-typed ULID IDs: PresenceSessionId, HeartbeatId, ActivityId (forward-declared)
- PresenceStatus enum (stable string codes: "online" | "away" | "offline")
- ActivityKind enum (stable string codes: "viewing" | "editing")
- PresenceSession entity (ephemeral; per-(tenant, party, replica) session)
- Heartbeat entity (append-only beat record)
- IPresenceRepository + InMemoryPresenceRepository (tenant-scoped reads + appends + retention prune)
- PresenceSessionValidator + HeartbeatValidator
- ~18–20 tests covering construction + validation + repository round-trip + tenant isolation
  + TTL eviction + retention prune + heartbeat idempotency at the repo layer

DOES NOT ship: IPresenceService (PR 2), IActivityTracker (PR 3), Activity entity (PR 3),
DI umbrella (PR 4), docs page (PR 4). No service layer; no TTL evictor; no event surface.

@standing-pattern: pattern-001

Refs: ADR 0088 §1; crdt-friendly-schema-conventions.md §§1, 2, 3, 4, 5, 13, 14;
project_phase_2_commercial_scope.md ("Multi-actor delegation").
```

#### Do NOT in this PR

- Do NOT ship `IPresenceService` / `IActivityTracker`. PR 2 and PR 3 respectively.
- Do NOT ship the TTL evictor background service. That's PR 2.
- Do NOT ship the `Activity` entity (PR 3). The `ActivityId` strongly-typed ID is forward-declared so `Heartbeat.ActiveActivityId?` compiles; the entity proper ships PR 3.
- Do NOT ship event records or the event-publisher abstraction. Those land in PR 2 (lifecycle events) + PR 3 (activity events).
- Do NOT touch `packages/blocks-crew-comms/Presence/PresenceBus.cs` (different surface; per §H3).
- Do NOT register anything in DI yet — the umbrella ships PR 4 (per `pattern-005`).
- Do NOT add encryption to any field. Presence carries no PII at rest in v1; the `ClientInfo` field is opt-in + best-treated as user-visible.

---

### PR 2 — `IPresenceService` lifecycle facade + TTL evictor + lifecycle events + security spot-check

**Estimated effort:** ~2–2.5h
**Scope:** add `IPresenceService` lifecycle facade (Register / Heartbeat / Disconnect / Status / Roster); ship the background TTL evictor; emit `Presence.UserConnected` / `Presence.UserDisconnected` (and optional throttled `Presence.HeartbeatRecorded`); local `IPresenceEventPublisher` abstraction
**Commit subject:** `feat(blocks-presence): add IPresenceService lifecycle facade + TTL evictor + UserConnected/UserDisconnected events`
**Depends on:** PR 1 merged
**Branch:** `cob/blocks-presence-service`
**Pre-merge:** Security-engineering spot-check (one-perspective Sonnet, medium effort) — see §Pre-merge council requirements

#### New interface — `IPresenceService`

```csharp
namespace Sunfish.Blocks.Presence;

/// <summary>
/// High-level lifecycle facade over <see cref="IPresenceRepository"/>.
/// Owns session create / heartbeat / disconnect transitions; emits
/// Presence.* lifecycle events; enforces tenant-isolation at the boundary
/// (delegated to the repository).
/// </summary>
/// <remarks>
/// Clients call <see cref="RegisterAsync"/> once on app startup to create
/// a session for the current (tenant, party, replica) tuple, then
/// <see cref="HeartbeatAsync"/> every <c>PresenceOptions.HeartbeatCadence</c>
/// (default 30s) until <see cref="DisconnectAsync"/> is called or the
/// session TTL-evicts to Offline.
/// </remarks>
public interface IPresenceService
{
    /// <summary>
    /// Creates (or refreshes) a session for the ambient (tenant, party, replica).
    /// Idempotent on (tenantId, partyId, replicaId): a second call within the
    /// session-lifetime returns the existing session with refreshed timestamps.
    /// Emits Presence.UserConnected on first registration; subsequent refresh
    /// calls do NOT re-emit the event (per the idempotency key — see §Idempotency-key catalog).
    /// </summary>
    Task<PresenceSession> RegisterAsync(
        string replicaId,
        string? clientInfo = null,
        IReadOnlyList<string>? capabilities = null,
        CancellationToken ct = default);

    /// <summary>
    /// Records a heartbeat for the given session. Refreshes the session's
    /// <c>LastHeartbeatAt</c>. Appends a Heartbeat row. If an activity is
    /// currently in flight for the same session (via IActivityTracker, PR 3),
    /// links the activity ID onto the Heartbeat row. Optionally emits
    /// Presence.HeartbeatRecorded — but per §Idempotency-key catalog, the
    /// event is THROTTLED to at most one per session per 5 minutes; high-cadence
    /// HEARTBEAT events would drown the event bus.
    /// </summary>
    Task HeartbeatAsync(PresenceSessionId sessionId, CancellationToken ct = default);

    /// <summary>
    /// Transitions the session to Offline. Sets DisconnectedAt; emits
    /// Presence.UserDisconnected. Idempotent: calling Disconnect on an
    /// already-Offline session is a no-op (event NOT re-emitted).
    /// </summary>
    Task DisconnectAsync(PresenceSessionId sessionId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Client-driven status transition (e.g., UI tier signalling idle).
    /// Online ↔ Away transitions; Offline must use DisconnectAsync.
    /// </summary>
    Task SetStatusAsync(PresenceSessionId sessionId, PresenceStatus status, CancellationToken ct = default);

    /// <summary>
    /// Returns the current "who's online" roster for the ambient tenant.
    /// Excludes Offline sessions. Order is not guaranteed; consumers sort
    /// by DisplayName (resolvable via IPartyReadModel) on their own.
    /// </summary>
    Task<IReadOnlyList<PresenceSession>> GetOnlineRosterAsync(CancellationToken ct = default);

    /// <summary>Returns a session by ID, tenant-scoped.</summary>
    Task<PresenceSession?> GetSessionAsync(PresenceSessionId sessionId, CancellationToken ct = default);
}
```

#### New options — `PresenceOptions`

```csharp
public sealed record PresenceOptions
{
    /// <summary>Default cadence at which clients are expected to heartbeat. Default: 30s.</summary>
    public TimeSpan HeartbeatCadence { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time after the last received heartbeat before a session is TTL-evicted
    /// to Offline. Default: 90s (3 missed heartbeats at default cadence).
    /// </summary>
    public TimeSpan SessionTtl { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>How often the TTL evictor scans for stale sessions. Default: 30s.</summary>
    public TimeSpan EvictionScanInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Retention window for Heartbeat rows. Default: 24h (then PruneHeartbeatsOlderThanAsync removes).</summary>
    public TimeSpan HeartbeatRetention { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Minimum interval between Presence.HeartbeatRecorded events per session.
    /// Default: 5 minutes (12 heartbeat receipts per session per hour go into
    /// the repo; 12 events per session per hour go onto the bus).
    /// </summary>
    public TimeSpan HeartbeatEventThrottle { get; init; } = TimeSpan.FromMinutes(5);
}
```

#### New service — `PresenceTtlEvictionService`

`IHostedService`-shaped (or whatever equivalent fits Anchor's runtime — the kernel may not have `IHostedService` directly; if not, ship as a `StartAsync`/`StopAsync`-shaped class that the umbrella DI registers and the runtime host starts at app boot). It runs on a `PeriodicTimer`-style loop:

- Every `PresenceOptions.EvictionScanInterval`, call `IPresenceRepository.EvictExpiredSessionsAsync(now - SessionTtl)`.
- For each transitioned session, emit `Presence.UserDisconnected` with reason `"ttl-eviction"`.
- Catch exceptions per-scan; log; do not crash the loop.
- Honour `CancellationToken` cleanly.

#### New event records — `Presence.*` lifecycle events

```csharp
public interface IPresenceEvent { }

public sealed record UserConnectedEvent(
    EventId EventId,
    string EventType,            // "Presence.UserConnected"
    string SchemaVersion,        // "1.0.0"
    Instant OccurredAt,
    Instant RecordedAtUtc,
    TenantId TenantId,
    string OriginatingReplicaId,
    EventId? CausationId,
    string? CorrelationId,
    string ProducerCluster,      // "presence"
    string IdempotencyKey,       // "user-connected:{sessionId}"
    UserConnectedPayload Payload
) : IPresenceEvent;

public sealed record UserConnectedPayload(
    PresenceSessionId SessionId,
    PartyId PartyId,
    string ReplicaId,
    IReadOnlyList<string> Capabilities);
```

`HeartbeatRecordedEvent` + `UserDisconnectedEvent` follow the same shape. **Critical:** no PII fields in payloads. The payload carries `PartyId` (an opaque ULID-backed strongly-typed identifier) — NOT `DisplayName`, NOT `Email`, NOT `Phone`. Consumers resolve `PartyId` to display data via `IPartyReadModel` at render time. Documented in PR 2 + the docs page; verified in PR 2 tests (`EventPayload_NoPiiFields`).

#### `IPresenceEventPublisher`

Local abstraction; same shape as `IPartyEventPublisher` from the people-foundation hand-off PR 3:

```csharp
public interface IPresenceEventPublisher
{
    Task PublishAsync(IPresenceEvent @event, CancellationToken ct = default);
}

public sealed class InMemoryPresenceEventPublisher : IPresenceEventPublisher
{
    private readonly ConcurrentQueue<IPresenceEvent> _events = new();
    public IReadOnlyCollection<IPresenceEvent> Recorded => _events.ToArray();

    public Task PublishAsync(IPresenceEvent @event, CancellationToken ct = default)
    {
        _events.Enqueue(@event);
        return Task.CompletedTask;
    }
}
```

The canonical event-substrate wiring (to `kernel-event-bus` or equivalent) lands in PR 4 alongside the DI umbrella.

#### Implementation — `PresenceService`

```csharp
internal sealed class PresenceService : IPresenceService
{
    private readonly IPresenceRepository _repo;
    private readonly IPresenceEventPublisher _events;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IActorPrincipalResolver _actor;       // see people-foundation §H1
    private readonly TimeProvider _time;
    private readonly PresenceOptions _options;
    private readonly ConcurrentDictionary<PresenceSessionId, Instant> _lastHeartbeatEventAt = new();

    // RegisterAsync: idempotent on (tenant, party, replica); emits UserConnected on first registration
    // HeartbeatAsync: append Heartbeat; refresh session.LastHeartbeatAt + bump Version;
    //                 optionally emit HeartbeatRecorded (throttled per _lastHeartbeatEventAt)
    // DisconnectAsync: idempotent on Status == Offline; transitions + emits UserDisconnected
    // SetStatusAsync: Online ↔ Away only; rejects Offline (use DisconnectAsync)
    // GetOnlineRosterAsync / GetSessionAsync: delegate to repo (which enforces tenant scope)
}
```

#### Tests (PR 2)

`tests/PresenceServiceTests.cs`:
- `RegisterAsync_NewParty_CreatesSession_EmitsUserConnected`.
- `RegisterAsync_ExistingActiveSessionForSameReplica_RefreshesSession_DoesNotReEmitConnected`.
- `RegisterAsync_DifferentReplicaSameParty_CreatesDistinctSession_EmitsAnotherUserConnected`.
- `RegisterAsync_NewParty_DoesNotEmitEvent_WithOtherTenantSession` (tenant-isolation; tenant-A registration must not emit events that tenant-B observers can see; the InMemory publisher receives the event but the tenant filter is on read-side, not on publish — clarify in test: the published event carries the canonical TenantId).
- `HeartbeatAsync_RefreshesLastHeartbeatAt_AppendsHeartbeatRow`.
- `HeartbeatAsync_BumpsSessionVersion`.
- `HeartbeatAsync_OnUnknownSession_Throws_OrReturnsErrorOutcome` (choose: throw is canonical; OK to throw with a clearly-typed exception `PresenceSessionNotFoundException`).
- `HeartbeatAsync_ThrottlesHeartbeatRecordedEvent` (12 calls in 1 minute → at most 1 event; 12 calls over 1 hour at default throttle → at most 1 event per 5min window).
- `HeartbeatAsync_OnOfflineSession_Throws` (cannot heartbeat an Offline session; client must Re-Register).
- `DisconnectAsync_SetsOffline_EmitsUserDisconnected`.
- `DisconnectAsync_OnAlreadyOffline_IsIdempotent_DoesNotReEmit`.
- `SetStatusAsync_OnlineToAway_TransitionsCleanly`.
- `SetStatusAsync_AwayToOnline_TransitionsCleanly`.
- `SetStatusAsync_ToOffline_Throws` (must use DisconnectAsync).
- `GetOnlineRosterAsync_ReturnsOnlineAndAwayOnly_ExcludesOffline`.
- `GetOnlineRosterAsync_IsTenantScoped_ExcludesOtherTenantSessions`.
- `GetSessionAsync_CrossTenant_ReturnsNull`.

`tests/PresenceTtlEvictionServiceTests.cs`:
- `EvictionScan_TransitionsStaleSessionsToOffline_EmitsUserDisconnected_WithReasonTtlEviction`.
- `EvictionScan_LeavesFreshSessionsAlone`.
- `EvictionScan_ExceptionInOneTenantsScan_DoesNotCrashLoop` (per-tenant or per-batch fault isolation).
- `EvictionScan_HonoursCancellation`.

`tests/PresenceEventCatalogTests.cs`:
- `UserConnectedEventType_EqualsExpectedString` (`"Presence.UserConnected"`).
- `UserConnectedIdempotencyKey_MatchesCatalogPattern` (`"user-connected:{sessionId}"`).
- `EventPayload_NoPiiFields` — reflection-based assertion that `UserConnectedPayload` / `HeartbeatRecordedPayload` / `UserDisconnectedPayload` contain ONLY {`SessionId`, `PartyId`, opaque ID/timestamp fields, opaque `Capabilities`}; explicitly NOT {DisplayName, Email, Phone, Address, TaxId, DateOfBirth, ClientInfo>opaque-string}. (Note: `ClientInfo` is opt-in and an opaque string — the test asserts it is NOT in the payload, even though it lives on the session row.)
- `ProducerCluster_AlwaysPresence`.
- `EventType_AllStartWithPresenceDotPrefix`.

Total new tests this PR: ~24–26.

#### Verification

- `dotnet build` succeeds.
- All previous PR 1 tests pass unchanged.
- New PR 2 tests pass.
- `grep -rn "DisplayName\|Email\|Phone\|TaxId" packages/blocks-presence/Events/` returns ZERO hits in payload definitions (the "no PII in presence events" invariant; mechanical check + the reflection-based `EventPayload_NoPiiFields` test).

#### Pre-merge security-engineering spot-check (PR 2 ONLY)

Per the §Pre-merge council requirements section: **single-perspective Sonnet (medium effort) spot-check** on PR 2.

**Two specific questions the reviewer answers:**

1. **Tenant isolation:** "Can a caller in tenant A's context, via any method on `IPresenceService`, observe any presence data (sessions, roster, events) from tenant B?" Reviewer reads `PresenceService.cs` + `InMemoryPresenceRepository.cs` + the `MultiTenantIsolationTests` to confirm: NO. If yes, BLOCK PR 2 + file `cob-question-*`.

2. **PII in event payloads:** "Does any `Presence.*` event payload contain {DisplayName, Email, Phone, Address, TaxId, DateOfBirth, ClientInfo>full-string, IP-address, or any other personally-identifying field}?" Reviewer reads `Events/*.cs` payload records + the `EventPayload_NoPiiFields` test to confirm: NO (only opaque identifiers + timestamps + opaque-string capabilities). If yes, BLOCK PR 2 + file `cob-question-*`.

The spot-check is bounded: ≤15 minutes, two questions, two assertions. Reviewer dispatches via the existing council pattern (per `feedback_council_reviews_use_best_model_xhigh`) but with `effort: medium`, `model: sonnet`, and a single perspective brief. NOT a full council. The output is APPROVED or BLOCKED; if BLOCKED, COB halts + files `cob-question-*`.

---

### PR 3 — `Activity` entity + `IActivityTracker` + activity events

**Estimated effort:** ~1.5–2h
**Scope:** add the `Activity` entity (append-only-with-tombstone, like PartyRole); ship `IActivityTracker` for per-resource "X is viewing this lease" tracking; emit `Presence.UserViewing` / `Presence.UserStoppedViewing` events
**Commit subject:** `feat(blocks-presence): add Activity entity + IActivityTracker + UserViewing/UserStoppedViewing events`
**Depends on:** PR 2 merged
**Branch:** `cob/blocks-presence-activity-tracker`

#### New type — `Activity`

```csharp
public sealed record Activity
{
    public required ActivityId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public required PresenceSessionId PresenceSessionId { get; init; }
    public required PartyId PartyId { get; init; }                  // denormalized for query-by-party
    public required string ResourceKind { get; init; }              // stable string code; cluster-defined
    public required string ResourceId { get; init; }                // opaque; the consumer cluster's entity ID
    public required ActivityKind Kind { get; init; }                // viewing | editing
    public required Instant StartedAt { get; init; }
    public Instant? EndedAt { get; init; }                          // null = active
    public string? EndedReason { get; init; }

    // CRDT envelope (append-only with tombstone-style EndedAt; like PartyRole)
    public required PartyId CreatedBy { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();
}
```

`ResourceKind` is a stable string code per CRDT §5 — consumer clusters define their own codes (e.g., `"lease"`, `"invoice"`, `"document"`, `"work-order"`). Presence does NOT enumerate the valid codes; only validates the shape (kebab-case + lowercase + max 64 chars + non-empty).

`ResourceId` is opaque — the consumer cluster's entity ID, stringified. Presence does NOT validate it as a ULID / Guid / int; whatever the consumer cluster passes, it stores.

**Append-only with tombstone-on-detach pattern** (matches PartyRole; per `party-model-convention.md` §1 + CRDT §4):

- **Start viewing** = INSERT a new Activity row with `EndedAt == null`.
- **Stop viewing** = UPDATE the existing row to set `EndedAt = now()` + optional `EndedReason`. This is the ONLY field mutation allowed on an Activity row.
- **Restart viewing the same resource** = INSERT a NEW row (do NOT clear `EndedAt` on the old row).
- **Hard delete** = forbidden except in the retention-prune path on Heartbeat (which does NOT cascade to Activity; Activity rows have their own retention bound — see §H4).

#### New repository extension — `IActivityRepository` (or extension methods on `IPresenceRepository`)

To keep PR 1's `IPresenceRepository` stable and lean, PR 3 ships a separate **interface** `IActivityRepository` with its own in-memory backing dictionary. The umbrella DI extension (PR 4) registers both repositories with the same `InMemoryPresence*` lifetime style. (Implementation note: COB may, at COB's discretion, either ship a separate `InMemoryActivityRepository` OR add the activity dictionary to the existing `InMemoryPresenceRepository` and have it implement both interfaces. EITHER shape is acceptable; the public surface is what matters.)

```csharp
public interface IActivityRepository
{
    Task<Activity?> GetAsync(ActivityId id, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetActiveOnResourceAsync(
        string resourceKind,
        string resourceId,
        CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetActiveByPartyAsync(
        PartyId partyId,
        CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetActiveBySessionAsync(
        PresenceSessionId sessionId,
        CancellationToken ct = default);
    Task UpsertAsync(Activity activity, CancellationToken ct = default);
    Task<int> PruneEndedActivitiesOlderThanAsync(Instant cutoff, CancellationToken ct = default);
}
```

#### New interface — `IActivityTracker`

```csharp
public interface IActivityTracker
{
    /// <summary>
    /// Records that the ambient party (via session) has started viewing/editing the
    /// named (resourceKind, resourceId) tuple. Emits Presence.UserViewing.
    /// Idempotent on (sessionId, resourceKind, resourceId, kind): if the party
    /// already has an active activity on the resource with the same kind, returns
    /// the existing activity row + does NOT re-emit. Returns the activity row.
    /// </summary>
    Task<Activity> StartViewingAsync(
        PresenceSessionId sessionId,
        string resourceKind,
        string resourceId,
        ActivityKind kind = ActivityKind.Viewing,
        CancellationToken ct = default);

    /// <summary>
    /// Marks the activity row as ended (EndedAt = now). Emits
    /// Presence.UserStoppedViewing. Idempotent: stopping an already-ended
    /// activity returns the existing row and does NOT re-emit.
    /// </summary>
    Task StopViewingAsync(ActivityId activityId, string? endedReason = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of currently-active activity rows for the named resource
    /// (any party in the current tenant). Used by UI surfaces to render
    /// "X is editing this lease" indicators.
    /// </summary>
    Task<IReadOnlyList<Activity>> GetActiveOnResourceAsync(
        string resourceKind,
        string resourceId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the set of currently-active activity rows for the party
    /// (across all resources). Useful for "What is Jane focused on right now?".
    /// </summary>
    Task<IReadOnlyList<Activity>> GetActiveByPartyAsync(
        PartyId partyId,
        CancellationToken ct = default);
}
```

#### Implementation — `ActivityTracker`

```csharp
internal sealed class ActivityTracker : IActivityTracker
{
    private readonly IActivityRepository _repo;
    private readonly IPresenceRepository _presenceRepo;             // to resolve session→party
    private readonly IPresenceEventPublisher _events;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly TimeProvider _time;

    // StartViewingAsync: lookup any active Activity for (sessionId, resourceKind, resourceId, kind);
    //                   if found, return idempotently; if not, INSERT new + emit UserViewing
    // StopViewingAsync: lookup; if already ended, return idempotently; else UPDATE EndedAt + emit UserStoppedViewing
    // Reads: tenant-scoped via the repo (which uses _tenantContext.Current)
}
```

#### Activity-side cleanup on session disconnect

When `PresenceService.DisconnectAsync` is called (or TTL eviction transitions a session to Offline), the **session disconnect MUST automatically end all the session's active Activity rows** (set `EndedAt = now()` + `EndedReason = "session-disconnect"` or `"ttl-eviction"`) and emit `Presence.UserStoppedViewing` for each.

This cross-PR coupling lives in **PR 3** (not retrofitted into PR 2): the `ActivityTracker` exposes an `EndAllForSessionAsync(PresenceSessionId, string reason)` method, and `PresenceService.DisconnectAsync` calls it after transitioning the session to Offline + before emitting `Presence.UserDisconnected`. Since PR 3 is sequential after PR 2, this means PR 3 also touches `PresenceService.cs` to wire the call — a small additive edit (one method call + one constructor parameter).

**Alternative** (if the cross-PR edit feels awkward): ship a `IActivitySessionCleanupHook` interface in PR 2 (`internal` or `public`) that `PresenceService` calls on disconnect; PR 3 provides the implementation. This decouples PR 2 from PR 3's existence. **COB's call** — either shape is acceptable.

Tests in PR 3 verify the wiring:
- `DisconnectAsync_EndsAllActiveActivitiesForSession_EmitsUserStoppedViewingForEach`.
- `TtlEviction_EndsAllActiveActivitiesForSession_EmitsUserStoppedViewingForEach`.

#### New event records — `Presence.UserViewing` + `Presence.UserStoppedViewing`

```csharp
public sealed record UserViewingEvent(
    EventId EventId,
    string EventType,                  // "Presence.UserViewing"
    string SchemaVersion,
    Instant OccurredAt,
    Instant RecordedAtUtc,
    TenantId TenantId,
    string OriginatingReplicaId,
    EventId? CausationId,
    string? CorrelationId,
    string ProducerCluster,            // "presence"
    string IdempotencyKey,             // "user-viewing:{activityId}"
    UserViewingPayload Payload
) : IPresenceEvent;

public sealed record UserViewingPayload(
    ActivityId ActivityId,
    PresenceSessionId SessionId,
    PartyId PartyId,
    string ResourceKind,
    string ResourceId,
    ActivityKind Kind);

// UserStoppedViewingEvent + UserStoppedViewingPayload mirror the shape.
```

Same PII discipline: only opaque IDs + opaque resource references in payloads. The `ResourceId` is opaque to presence (the consumer cluster's entity ID, stringified); presence does not (and cannot) know whether the ID itself leaks anything — but presence does not introduce any PII of its own.

#### Tests (PR 3)

`tests/ActivityTests.cs`:
- `Create_ValidActivity_Passes`.
- `Create_EmptyResourceKind_Fails`.
- `Create_ResourceKindWithUppercase_Fails`.
- `Create_EmptyResourceId_Fails`.
- `Create_EndedBeforeStarted_Fails`.

`tests/InMemoryActivityRepositoryTests.cs` (or extended `InMemoryPresenceRepositoryTests.cs` if COB merged the impl):
- `Upsert_Then_GetById_Roundtrips`.
- `GetActiveOnResource_ReturnsActiveOnly`.
- `GetActiveOnResource_ExcludesEnded`.
- `GetActiveOnResource_CrossTenant_ReturnsEmpty`.
- `GetActiveByParty_ReturnsActiveOnly`.
- `PruneEndedActivitiesOlderThanAsync_RemovesEndedRows`.
- `PruneEndedActivitiesOlderThanAsync_LeavesActiveRows`.

`tests/ActivityTrackerTests.cs`:
- `StartViewingAsync_NewResource_CreatesActivity_EmitsUserViewing`.
- `StartViewingAsync_IdempotentOnSameSessionResource_ReturnsExisting_DoesNotReEmit`.
- `StartViewingAsync_DifferentKind_CreatesDistinctActivity_EmitsAnother` (viewing vs editing are distinct activities).
- `StartViewingAsync_OnOfflineSession_Throws_OrReturnsErrorOutcome` (cannot start an activity from a non-active session).
- `StopViewingAsync_SetsEndedAt_EmitsUserStoppedViewing`.
- `StopViewingAsync_OnAlreadyEnded_IsIdempotent`.
- `GetActiveOnResource_ReturnsAllPartiesViewingThatResource`.
- `GetActiveOnResource_FromTenantBContext_ExcludesTenantARows`.
- `GetActiveByParty_ReturnsCrossResourceActivities`.
- `DisconnectAsync_EndsAllActiveActivitiesForSession_EmitsUserStoppedViewingForEach`.
- `TtlEviction_EndsAllActiveActivitiesForSession_EmitsUserStoppedViewingForEach`.

`tests/ActivityEventCatalogTests.cs`:
- `UserViewingEventType_EqualsExpectedString`.
- `UserViewingIdempotencyKey_MatchesCatalogPattern`.
- `EventPayload_NoPiiFields` (extends PR 2's reflection-based assertion to cover Activity payloads).

Total new tests this PR: ~18–20.

#### Verification

- `dotnet build` succeeds.
- All previous PR 1 + PR 2 tests pass unchanged.
- New PR 3 tests pass.
- `grep -rn "DisplayName\|Email\|Phone\|TaxId\|DateOfBirth" packages/blocks-presence/Events/` returns ZERO hits (extends PR 2's invariant).

---

### PR 4 — `AddBlocksPresence()` DI umbrella + canonical-event-substrate wiring + docs page + event-catalog edit + ledger flip

**Estimated effort:** ~1–1.5h
**Scope:** ship the `AddBlocksPresence()` DI umbrella; wire the local `IPresenceEventPublisher` to the canonical event substrate (`kernel-event-bus` or equivalent); ship the cluster docs page; append the `Presence.*` event sub-catalog to the cross-cluster event-bus design doc; flip the ledger row to `built`
**Commit subject:** `feat(blocks-presence): add AddBlocksPresence DI extension + docs + Presence.* event sub-catalog`
**Depends on:** PR 3 merged
**Branch:** `cob/blocks-presence-umbrella-docs`
**Standing-pattern tags:** `@standing-pattern: pattern-005` (DI umbrella) AND `@standing-pattern: pattern-006` (apps/docs page)

#### Canonical-event-catalog edit (FIRST STEP of PR 4)

Before authoring DI code, COB **must** open `_shared/engineering/cross-cluster-event-bus-design.md` §3 and append a new sub-section §3.7 `Presence.*` events:

```markdown
### 3.7 `Presence.*` events

| Event type | Consumers | Payload | Idempotency key |
|---|---|---|---|
| `Presence.UserConnected` | crew-comms (typing/seen UI), property-leases (presence indicator), financial-ar/ap (presence indicator), docs (presence indicator), future collaboration UI | `{ sessionId, partyId, replicaId, capabilities[] }` | `user-connected:{sessionId}` |
| `Presence.HeartbeatRecorded` (optional, throttled 5min/session) | (typically none external — internal liveness signal) | `{ sessionId, partyId, recordedAt, activeActivityId? }` | `heartbeat-recorded:{sessionId}:{ceil5min(recordedAt)}` |
| `Presence.UserDisconnected` | crew-comms, all presence-indicator consumers above | `{ sessionId, partyId, reason }` | `user-disconnected:{sessionId}` |
| `Presence.UserViewing` | property-leases, financial-ar/ap, docs, work-orders, future collaboration UI | `{ activityId, sessionId, partyId, resourceKind, resourceId, kind }` | `user-viewing:{activityId}` |
| `Presence.UserStoppedViewing` | (same as UserViewing) | `{ activityId, sessionId, partyId, resourceKind, resourceId, kind, endedReason? }` | `user-stopped-viewing:{activityId}` |
```

If §3.7 (or an equivalent number) already exists at the time of PR 4 (e.g., a sibling hand-off claimed the numbering first), COB renumbers to the next available slot + notes the renumber in the PR commit message. Per event-bus design §3 "Catalog upkeep": **events must back-fill into the catalog in the same PR that emits them**. PR 4 satisfies this for all 5 Presence events (PR 2 + PR 3 produce the events; PR 4 catalogs them).

#### DI extension

**`DependencyInjection/PresenceServiceCollectionExtensions.cs`**:

```csharp
public static class PresenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the blocks-presence cluster: PresenceSession + Heartbeat
    /// repository, IPresenceService, IActivityTracker, TTL evictor, and the
    /// in-memory event publisher. Consumers can replace IPresenceEventPublisher
    /// with a canonical-bus-wired implementation by calling
    /// services.Replace(...) after this extension.
    /// </summary>
    public static IServiceCollection AddBlocksPresence(
        this IServiceCollection services,
        Action<PresenceOptions>? configure = null)
    {
        services.Configure<PresenceOptions>(o => configure?.Invoke(o));

        services.TryAddSingleton<IPresenceRepository, InMemoryPresenceRepository>();
        services.TryAddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.TryAddSingleton<IPresenceEventPublisher, InMemoryPresenceEventPublisher>();
        services.TryAddSingleton<IPresenceService, PresenceService>();
        services.TryAddSingleton<IActivityTracker, ActivityTracker>();

        // TTL evictor — registered for runtime-host startup (shape depends on Anchor's host model)
        services.AddSingleton<PresenceTtlEvictionService>();
        // If/when an IHostedService surface is in scope:
        // services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PresenceTtlEvictionService>());

        return services;
    }
}
```

**`TryAddSingleton` discipline** (per `pattern-005`): swappable surfaces (the event publisher, the repositories, the service) use `TryAddSingleton` so consumers can pre-register their own implementations + this extension is then a no-op for those types. The TTL evictor uses plain `AddSingleton` because there's no reasonable consumer-override case for it.

**Two-overload pattern (audit-disabled / audit-enabled both-or-neither)** — **NOT REQUIRED** in this hand-off's v1 (presence is intentionally NOT audit-logged; per §H6). Documented in the docs page; do not apply the pattern preemptively.

#### Canonical event-substrate wiring (optional sub-step of PR 4)

If the canonical event substrate (`kernel-event-bus` or `foundation-events`) exposes a publisher interface, ship a **second registration extension** alongside `AddBlocksPresence()`:

```csharp
public static IServiceCollection AddBlocksPresenceWithEventBus(
    this IServiceCollection services,
    Action<PresenceOptions>? configure = null)
{
    services.AddBlocksPresence(configure);
    // Replace the in-memory publisher with a canonical-bus-wired adapter:
    services.Replace(ServiceDescriptor.Singleton<IPresenceEventPublisher, EventBusPresencePublisher>());
    return services;
}
```

The `EventBusPresencePublisher` is an adapter that translates `IPresenceEvent` → the canonical bus's envelope shape. If the canonical bus interface isn't stable at PR 4 time, **skip this step** and document in the docs page: "v1 ships with `InMemoryPresenceEventPublisher`; the canonical-bus-wired publisher arrives with the cross-cluster event-bus rollout follow-on hand-off." DO NOT block PR 4 on canonical-bus availability.

#### Docs

**`apps/docs/blocks/blocks-presence/overview.md`**:

```markdown
# blocks-presence

Application-tier presence substrate for the Sunfish Anchor local-first
runtime. Provides "who's online, viewing what, right now" perception
across multi-actor collaboration surfaces.

## Overview

`blocks-presence` is the perceptual foundation for collaboration features.
It ships:

- `PresenceSession` — ephemeral per-(tenant, party, replica) session record.
- `Heartbeat` — append-only liveness signal; powers TTL eviction.
- `Activity` — per-resource "X is viewing/editing this lease" indicator.
- `IPresenceService` — lifecycle facade (Register / Heartbeat / Disconnect /
  Status / Roster). Emits `Presence.UserConnected` /
  `Presence.HeartbeatRecorded` (throttled) / `Presence.UserDisconnected`.
- `IActivityTracker` — resource-focus facade (StartViewing / StopViewing /
  GetActiveOnResource / GetActiveByParty). Emits `Presence.UserViewing` /
  `Presence.UserStoppedViewing`.
- `PresenceTtlEvictionService` — background TTL evictor; default 90s.

## Relationship to blocks-crew-comms

`blocks-presence` is **application-tier** (per-user, per-document,
per-tenant). `blocks-crew-comms/Presence/PresenceBus.cs` (per ADR 0076)
is **transport-tier** (peer-to-peer signed HEARTBEAT frames; informs the
chat-message routing). The two layers coexist; they are NOT redundant.
Consumer clusters that need application-tier presence call
`IPresenceService` here; the chat substrate continues to use the
transport-tier `PresenceBus` for its own message-routing concerns.

## What's in v1

- Per-tenant, per-party ephemeral sessions with TTL eviction.
- Append-only Heartbeat log with bounded retention (default 24h).
- Append-only-with-tombstone Activity indicators ("X is viewing/editing
  resource Y").
- In-memory backing repository (default); canonical-bus-wired event
  publisher arrives with the cross-cluster event-bus rollout follow-on
  if not in this hand-off.

## What's NOT in v1 (deferred)

- Live cursor positions (Google-Docs-style multi-cursor / multi-selection
  rendering) — different substrate; UI-layer concern.
- Voice + video calling — out-of-band media tier.
- Push notifications (mobile / desktop OS) — different transport tier.
- Per-resource access-logging / audit trails — `kernel-audit` concern,
  NOT this cluster.
- Idle-detection / Away-state auto-transitions — UI-tier concern; the
  presence service is **client-driven** (clients call `SetStatusAsync`).
- Multi-tenant presence federation — cross-tenant correspondence is a
  Bridge-tier concern.
- SQLite-backed persistence — follow-on workstream paired with the broader
  Phase 3 SQLite-repository rollout.
- `blocks-crew-comms` integration code (TYPING/DELIVERED/SEEN) — separate
  follow-on owned by crew-comms.
- UI components (Razor / React / MAUI presence indicators) — composed by
  consumer-cluster follow-on hand-offs.
- Per-resource ACL filtering of presence visibility — follow-on paired
  with the capability-graph wiring (see project_phase_2_commercial_scope
  memory note). v1 ships **tenant-scoped visibility only**.

## Privacy + PII posture (v1)

Presence carries **no PII at rest** beyond:
- `PartyId` (opaque ULID; not PII on its own; consumers resolve to
  display data via `IPartyReadModel`).
- `ClientInfo` (opt-in UA-like string; OK to be null; stored on
  `PresenceSession` only; NEVER emitted in event payloads).

Event payloads carry ONLY opaque identifiers + timestamps +
cluster-defined `ResourceKind` codes. No DisplayName, no Email, no
Phone, no Address, no TaxId, no DateOfBirth, no ClientInfo.

This is verified by reflection-based test
`EventPayload_NoPiiFields` (see `tests/PresenceEventCatalogTests.cs`
and `tests/ActivityEventCatalogTests.cs`).

## Quickstart

```csharp
// DI registration (with default options)
services.AddBlocksPresence();

// Or with custom options
services.AddBlocksPresence(opts =>
{
    opts.HeartbeatCadence = TimeSpan.FromSeconds(45);
    opts.SessionTtl = TimeSpan.FromSeconds(135);  // 3x cadence
});

// On app startup (per replica)
var session = await presence.RegisterAsync(
    replicaId: "anchor-replica-abc123",
    clientInfo: "Anchor/0.42 (macOS 14.5)",
    capabilities: new[] { "viewing", "editing" });

// Periodic heartbeat (every PresenceOptions.HeartbeatCadence)
await presence.HeartbeatAsync(session.Id);

// When user opens a lease detail page
var activity = await activityTracker.StartViewingAsync(
    session.Id,
    resourceKind: "lease",
    resourceId: leaseId.ToString(),
    kind: ActivityKind.Viewing);

// When user closes the page
await activityTracker.StopViewingAsync(activity.Id);

// When app shuts down
await presence.DisconnectAsync(session.Id, reason: "app-exit");
```

## Conventions applied

- `crdt-friendly-schema-conventions.md` §1 (ULID), §2 (tombstones — applied
  with TTL eviction semantics for sessions; append-only-with-tombstone for
  activities), §3 (version + revisionVector), §4 (append-only Heartbeat
  sub-collection), §5 (stable string codes: PresenceStatus, ActivityKind,
  ResourceKind, Capabilities), §13 (envelope), §14 (tenant isolation).
- `cross-cluster-event-bus-design.md` §1 (envelope), §2 (naming), §3.7
  (Presence-domain catalog — added by this hand-off).
- `standing-approved-patterns.md` pattern-001 (PR 1), pattern-005 (PR 4),
  pattern-006 (this docs page).

## Related

- `blocks-people-foundation` (PartyId source; hard dep).
- `blocks-crew-comms` (transport-tier `PresenceBus` is a different surface;
  application-tier TYPING/DELIVERED/SEEN consumes `IPresenceService` in a
  follow-on PR).
- `foundation-multitenancy` (TenantId source; ITenantContextAccessor).
- `kernel-event-bus` (canonical event substrate; PR 4 wires
  `IPresenceEventPublisher` if/when the interface stabilizes).
- Future multi-actor collaboration UI surfaces in `blocks-property-leases`,
  `blocks-financial-ar`, `blocks-financial-ap`, `blocks-docs-*`,
  `blocks-work-*` — all consume `IActivityTracker.GetActiveOnResourceAsync`.
```

#### Ledger flip

Update `icm/_state/workstreams/W<NN>-blocks-presence.md` (if a workstream file exists; otherwise XO will pre-create it as a separate prep step before COB starts PR 4) and re-render `icm/_state/active-workstreams.md` via `render-ledger.py`. Set the state to `built`; cite the 4 PR numbers in the row's notes.

Per `feedback_never_add_workstream_rows_directly_to_ledger`: do NOT edit `active-workstreams.md` by hand; the source file is the workstream `W*.md`. If COB sees the row missing entirely, file `cob-question-*` to XO before opening PR 4 — XO authors the W*.md.

#### Tests (PR 4)

`tests/DependencyInjectionTests.cs`:
- `AddBlocksPresence_RegistersIPresenceService_AsSingleton`.
- `AddBlocksPresence_RegistersIActivityTracker_AsSingleton`.
- `AddBlocksPresence_RegistersIPresenceRepository_AsSingleton`.
- `AddBlocksPresence_RegistersIActivityRepository_AsSingleton`.
- `AddBlocksPresence_RegistersIPresenceEventPublisher_AsSingleton`.
- `AddBlocksPresence_RegistersTtlEvictionService_AsSingleton`.
- `AddBlocksPresence_WithOptions_RespectsConfigure`.
- `AddBlocksPresence_TryAddPattern_DoesNotOverridePreRegistered` (test that pre-registering a custom `IPresenceEventPublisher` survives the call).
- `AddBlocksPresenceWithEventBus_ReplacesPublisher` (only if the canonical-bus wiring is in scope this PR).

Total new tests this PR: ~7–9.

#### Verification

- `dotnet build` succeeds on the whole solution.
- All previous PR 1–3 tests pass unchanged.
- New PR 4 tests pass.
- `apps/docs/blocks/blocks-presence/overview.md` renders without broken links.
- `_shared/engineering/cross-cluster-event-bus-design.md` §3.7 (or renumbered slot) contains rows for all 5 Presence events.
- `icm/_state/active-workstreams.md` row for `blocks-presence` says `built` + cites 4 PR numbers.
- Total tests across all 4 PRs: ~67–75.

---

## Cross-cluster integration

### Consumes from `blocks-people-foundation`

- **`PartyId`** — every `PresenceSession`, `Heartbeat`, `Activity` carries `PartyId: Sunfish.Blocks.People.Foundation.PartyId`. Hard csproj reference. The people-foundation package MUST be built (per Pre-build checklist step 1) before this hand-off starts.
- **`IPartyReadModel` is NOT consumed by this cluster**. Presence operates on `PartyId` alone (opaque). Consumers of `Presence.*` events resolve `PartyId` to display data on their own via `IPartyReadModel`. This keeps the presence cluster decoupled from the people-cluster's read surface — presence stays lean.

### Consumes from `foundation-multitenancy`

- **`TenantId`** — every entity is tenant-scoped.
- **`ITenantContextAccessor`** — read by both repositories to enforce per-tenant isolation.

### Consumes from event substrate

- **`IPresenceEventPublisher`** — local abstraction (PR 2 + PR 3); the canonical wiring lands in PR 4 if the substrate is stable. If not, the in-memory implementation is the v1 default and a sibling event-bus rollout hand-off swaps in the canonical implementation later.

### Emitted to

- **`blocks-crew-comms`** — application-tier TYPING / DELIVERED / SEEN composition (follow-on hand-off, not this one).
- **Future multi-actor collaboration UI surfaces** in `blocks-property-leases` (e.g., "X is editing this lease" indicator on `LeaseDetailPage`), `blocks-financial-ar` (e.g., "Y is viewing this invoice" on `InvoiceDetailPage`), `blocks-financial-ap`, `blocks-docs-*` (e.g., "Z is reading this policy" indicator), `blocks-work-*` (e.g., "W is on this work-order"). Each consumer subscribes to `Presence.UserViewing` / `Presence.UserStoppedViewing` scoped to its own `ResourceKind`.
- **`accelerators/anchor` Top-bar "Crew" indicator** — future UI work; consumes `IPresenceService.GetOnlineRosterAsync` directly. Phase 2 commercial scope: Bookkeeper / Spouse / Tax-advisor all on shared tenants — this indicator IS the perception-of-collaboration UI primitive.

### Does NOT consume from `kernel-audit`

Presence events are explicitly NOT audit-logged (per §H6). The volume + cardinality of heartbeat / view events would drown the audit trail without serving its purpose. View-tracking for audit purposes (if/when needed) is a separate `AuditEventType.ResourceViewed` owned by `kernel-audit`, NOT this cluster. The presence cluster's events flow to the cross-cluster event bus for live-collaboration consumers; the audit trail's events flow to the persistent audit log for compliance consumers. Different consumers, different volumes, different cardinality — different substrates.

### Does NOT consume from `blocks-crew-comms`

Application-tier presence is decoupled from transport-tier presence. The two surfaces will be wired in **one direction** in a follow-on PR: crew-comms learns of application-tier disconnect events (so that chat-message routing can prefer a connected replica for the same party); presence does NOT subscribe to crew-comms transport-tier events. The asymmetry is intentional: transport-tier liveness is a low-level concern; application-tier presence is the higher-level abstraction.

---

## Pre-merge council requirements

### PR 1 (scaffold + entities + repository)

NONE. Pure substrate scaffold per `pattern-001`. COB self-audit suffices.

### PR 2 (service + TTL evictor + lifecycle events)

**Security-engineering spot-check — ONE perspective, Sonnet (medium effort), ≤15 minutes.**

Perspective: security-engineering. Brief:

> Review `packages/blocks-presence/` at the head of this PR. Answer two questions in writing:
>
> 1. **Tenant isolation:** Can a caller in tenant A's context, via any method on `IPresenceService` (`RegisterAsync`, `HeartbeatAsync`, `DisconnectAsync`, `SetStatusAsync`, `GetOnlineRosterAsync`, `GetSessionAsync`), observe any presence data (sessions, roster, events) from tenant B? Read `PresenceService.cs`, `InMemoryPresenceRepository.cs`, and the `MultiTenantIsolationTests` to verify. Answer YES or NO; if YES, identify the leak.
>
> 2. **PII in event payloads:** Does any `Presence.*` event payload (`UserConnectedPayload`, `HeartbeatRecordedPayload`, `UserDisconnectedPayload`) contain any of: DisplayName, Email, Phone, Address, TaxId, DateOfBirth, ClientInfo (full string vs opaque token), IP-address, or any other personally-identifying field? Read `Events/*.cs` and the `EventPayload_NoPiiFields` reflection-based test. Answer YES or NO; if YES, identify the field.
>
> Output: APPROVED or BLOCKED. If BLOCKED, name the specific defect; do not propose redesigns.

The spot-check is the only pre-merge gate beyond CI-green on PR 2. NOT a full council; NOT a 4-perspective gate; NOT a .NET-architect review. One reviewer, two questions, two assertions.

### PR 3 (Activity entity + IActivityTracker + activity events)

NONE. The same PII-in-payload invariant from PR 2 extends to Activity events; the `EventPayload_NoPiiFields` test in PR 3 already covers the assertion. COB self-audit suffices for the rest.

### PR 4 (DI umbrella + docs + catalog edit + ledger flip)

NONE. Pure umbrella + docs per `pattern-005` + `pattern-006`. COB self-audit suffices. Standing-pattern tag in the PR description (`@standing-pattern: pattern-005` and `@standing-pattern: pattern-006`) signals the short pipeline.

---

## Idempotency-key catalog

Per `cross-cluster-event-bus-design.md` §4 (idempotency key) — every `Presence.*` event carries an idempotency key that deduplicates re-deliveries across replicas.

| Event | Idempotency key shape | Notes |
|---|---|---|
| `Presence.UserConnected` | `"user-connected:{sessionId}"` | One emission per session lifetime; second `RegisterAsync` returns the existing session and does NOT re-emit, so the key never collides under normal flow |
| `Presence.HeartbeatRecorded` | `"heartbeat-recorded:{sessionId}:{ceil5min(recordedAt)}"` | **Throttle-encoded in the key.** `ceil5min` rounds `recordedAt` UP to the next 5-minute boundary (or whatever `PresenceOptions.HeartbeatEventThrottle` is set to). 12 heartbeats per hour per session → 12 distinct keys per hour. Consumers idempotently dedup on re-delivery. |
| `Presence.UserDisconnected` | `"user-disconnected:{sessionId}"` | One emission per session lifetime; second `DisconnectAsync` returns idempotently and does NOT re-emit |
| `Presence.UserViewing` | `"user-viewing:{activityId}"` | One emission per StartViewing; idempotent StartViewing returns existing Activity and does NOT re-emit |
| `Presence.UserStoppedViewing` | `"user-stopped-viewing:{activityId}"` | One emission per StopViewing; idempotent StopViewing returns and does NOT re-emit |

**Test coverage (in `tests/PresenceEventCatalogTests.cs` + `tests/ActivityEventCatalogTests.cs`):**
- `IdempotencyKey_MatchesCatalogPattern` for each event.
- `HeartbeatRecorded_KeyEncodesThrottleBoundary` (12 heartbeats over a single 5-minute window produce 1 distinct key; 12 over an hour produce 12).

If the cross-cluster event-bus catalog (§3.7 after PR 4 edits) prefers a different idempotency-key shape, USE THE CATALOG SHAPE per event-bus design §2 ("Do not rename existing event types"). The shapes above are the proposal.

---

## Dependencies + sequence

```
blocks-people-foundation (PartyId) ──┐
                                     │
foundation-multitenancy (TenantId) ──┼──► blocks-presence PR 1 (scaffold + entities + repo)
                                     │       │
ITenantContextAccessor ──────────────┘       ▼
                                          PR 2 (IPresenceService + TTL evictor + lifecycle events)
                                             │   (security spot-check, single-perspective Sonnet, ≤15min)
                                             ▼
                                          PR 3 (Activity + IActivityTracker + activity events)
                                             │
                                             ▼
                                          PR 4 (DI umbrella + canonical-bus wiring + docs + catalog + ledger flip)
                                             │
                                             ▼
                            blocks-presence is BUILT
                                             │
                       ┌─────────────────────┼─────────────────────────────────┐
                       ▼                     ▼                                 ▼
        blocks-crew-comms TYPING/   property-leases / financial-ar/ap /   anchor Top-bar
        DELIVERED/SEEN follow-on    docs / work-orders presence            "Crew" indicator
        (separate hand-off)         indicators (separate hand-offs)        (Anchor UI hand-off)
```

**Hard dep:** PR 1 cannot start until `blocks-people-foundation` is BUILT (PartyId is a hard csproj reference). Pre-build checklist step 1 verifies.

**Soft dep:** The canonical event-bus substrate (PR 4 step) is OPTIONAL — if not yet stable, PR 4 ships the in-memory publisher only and a follow-on hand-off wires the canonical substrate. PR 4 does NOT block on canonical-bus availability.

---

## License posture

### Borrowed-with-attribution (permissive)

**None.** This hand-off ships **clean-room presence primitives**. No code is borrowed from any presence/realtime/collaboration FOSS source.

### Inspiration-only (study, no code; do not vendor)

The application-tier presence pattern (per-user session + heartbeat + per-resource activity indicator + TTL eviction) is well-established across the realtime/collaboration industry. The following are studied as *patterns* (concept-level only), with NO code derivation:

- **Slack** (proprietary) — per-user, per-channel, per-thread presence + typing indicators. Studied as a UX-pattern reference only; no code, no docs reverse-engineered beyond public help-center documentation.
- **Discord** (proprietary) — per-user, per-server, per-voice-channel presence. Same posture: UX pattern only.
- **Matrix.org** (Apache 2.0 — but **NOT BORROWED** in this hand-off; flagged as available for future borrowing if the v1 design proves insufficient) — open spec for presence ([m.presence](https://spec.matrix.org/) event type, away-timeout, status messages). Available for future deep-dive if v1 needs extension; not consumed in v1.
- **Figma** (proprietary) — multi-cursor presence. Studied only at the README level for "what the deferred live-cursor surface might eventually look like." OUT OF SCOPE for v1.
- **Liveblocks** (proprietary SaaS) — presence-as-a-service. Studied only at the README level; same posture.

**Attribution requirements:**

The `NOTICE.md` in PR 1 carries this stub:

```markdown
# NOTICE — Sunfish.Blocks.Presence

This package's design is original (clean-room implementation) and does NOT
derive code from any external source. The application-tier presence
pattern (per-user session + heartbeat + TTL eviction + per-resource activity
indicator) is a well-established UX pattern across realtime/collaboration
products including Slack, Discord, Matrix, Figma, and Liveblocks.

These products were studied as UX-pattern references only — no code was
read, copied, paraphrased, or vendored. Implementation is original under
the MIT License per ADR 0088 §2.

Future borrowing from Matrix.org's presence spec (Apache 2.0) is an
acceptable enhancement path; v1 does not consume it.
```

### Clean-room only (copyleft) — N/A

No copyleft sources were consulted in producing this hand-off's design. The substrate is so well-understood at the pattern level that no clean-room reading was required.

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2.

---

## CRDT discipline

### 1. Ephemeral entities (PresenceSession)

`PresenceSession` does NOT carry a `DeletedAt` tombstone. Instead, the session transitions through `Status` = Online → Away → Offline. The TTL evictor (PR 2) sets `Status = Offline` + `DisconnectedAt = now()` when the heartbeat lapses; that IS the tombstone semantics for sessions. Hard-DELETE of session rows IS allowed at the **retention boundary** (e.g., periodic admin cleanup of Offline sessions older than 30 days) — sessions are explicitly ephemeral; they are NOT business records.

This is a controlled deviation from CRDT §2 ("never DELETE"). The deviation is sound because:
- A session row stranded in Offline status forever has no consumer value (no UI shows offline sessions; no consumer subscribes to them).
- Sessions accumulate at a rate of ~1 per party per replica per app-restart; over years of operation a tenant could accumulate millions of rows without bound.
- Hard-DELETE at the retention boundary is the standard pattern for ephemeral session data across the industry.

Documented in the docs page; tested in `InMemoryPresenceRepositoryTests`.

### 2. Append-only sub-collections (Heartbeat)

`Heartbeat` is strictly append-only per CRDT §4. No `UpdatedAt`, no `DeletedAt`, no `RevisionVector`. The repository's `AppendHeartbeatAsync` is the only write path; `PruneHeartbeatsOlderThanAsync` is the only delete path (retention prune; default 24h window).

This matches the people-foundation `EmailAddress`/`PhoneNumber`/`PartyAddress` shape (append-only with replacement markers) — except Heartbeat lacks even a `replacedAt` field because there is no "replace" semantics for heartbeats: a new heartbeat is a fresh row; old heartbeats remain until pruned.

### 3. Append-only with tombstone (Activity)

`Activity` matches the people-foundation `PartyRole` shape: append-only attach; tombstone-style detach via `EndedAt`. Re-starting viewing on the same resource = INSERT a new Activity row (do NOT clear `EndedAt` on the old row). Hard-DELETE forbidden except at the retention boundary (`PruneEndedActivitiesOlderThanAsync`; default 7d post-Ended).

### 4. Stable string codes

- `PresenceStatus` — `"online"` | `"away"` | `"offline"` (3 codes; closed set in v1; deprecation discipline binds if ever extended).
- `ActivityKind` — `"viewing"` | `"editing"` (2 codes; closed set in v1; deprecation discipline binds).
- `ResourceKind` — open set; consumer-cluster-defined; shape-validated only (kebab-case + lowercase + max 64 chars).
- `Capabilities` (on `PresenceSession`) — open set; consumer-cluster-defined; shape-validated only.

### 5. Per-tenant isolation

Every entity carries `TenantId`. Repository enforces isolation at the `(TenantId, EntityId)` tuple-key level for sessions; on the `TenantId` field for heartbeats + activities. Tested explicitly in `MultiTenantIsolationTests` across all three entities. Cross-tenant presence federation is OUT OF SCOPE for v1 — Bridge-tier concern.

### 6. No CRDT merge surface

Presence has no rich CRDT merge surface — sessions transition through a linear status state machine (`Online ↔ Away → Offline`); heartbeats + activities are append-only. Last-write-wins suffices for session-status merges across replicas (with the constraint that `Offline` is terminal: an `Offline → Online` transition requires a NEW session, not a status flip on the old one). Documented + tested.

---

## Test plan

### Per-PR minima (summary)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + entities + repo) | ~18–20 | construction + validation + repo round-trip + tenant isolation + TTL eviction + retention prune + heartbeat idempotency |
| PR 2 (service + TTL evictor + lifecycle events) | ~24–26 | RegisterAsync (new + refresh + cross-replica) + HeartbeatAsync (refresh + append + throttled event + offline-session reject) + DisconnectAsync (idempotent) + SetStatusAsync + GetOnlineRosterAsync + TTL evictor + 3 lifecycle events + PII-in-payload reflection |
| PR 3 (Activity + IActivityTracker + activity events) | ~18–20 | Activity validation + repo + StartViewing (new + idempotent + viewing-vs-editing distinct) + StopViewing + cross-resource queries + cross-tenant + disconnect-cleanup + 2 activity events |
| PR 4 (DI umbrella + docs + catalog edit + ledger flip) | ~7–9 | DI registration (each service) + TryAdd pattern + options + (optional) canonical-bus replacement |
| **Total** | **~67–75** | (the task brief called for ~35–45 tests; the substrate expanded as the 5-event catalog + multi-tenant isolation + TTL evictor + idempotency tests were scoped explicitly — call ~60 the realistic floor) |

**Note on the test-count band:** the task brief stated `~35–45 tests`. After scoping multi-tenant isolation + TTL evictor + heartbeat-event throttle + PII-in-payload reflection + the activity-on-disconnect cleanup explicitly, the realistic floor is ~60 tests. The ~75 estimate above is the upper bound. **If COB hits ~60 with full coverage of the §PASS gate, that's sufficient.** Do not pad; do not skip coverage to undershoot.

### Invariants (verified across all 4 PRs)

**I1. Heartbeat timeout = disconnect.** A session whose `LastHeartbeatAt < (now - PresenceOptions.SessionTtl)` is transitioned by the TTL evictor to `Status = Offline` + `DisconnectedAt = now()` + emits `Presence.UserDisconnected` with `reason = "ttl-eviction"`. Verified in `PresenceTtlEvictionServiceTests.EvictionScan_TransitionsStaleSessionsToOffline_*`.

**I2. Tenant-scoped presence visibility.** A caller in tenant A's context cannot observe any presence data (sessions, roster, heartbeats, activities) from tenant B. Cross-tenant reads return empty / null on every method. Verified in `MultiTenantIsolationTests` + the security-engineering spot-check on PR 2.

**I3. No PII in presence events.** All `Presence.*` event payloads contain ONLY {opaque IDs, timestamps, opaque resource references, opaque string-code capabilities}. NEVER {DisplayName, Email, Phone, Address, TaxId, DateOfBirth, ClientInfo (full string), IP-address, or any other personally-identifying field}. Verified mechanically via `EventPayload_NoPiiFields` reflection-based test (PR 2 + PR 3 + maintained in PR 4) + the security-engineering spot-check on PR 2.

**I4. Activity-on-disconnect cleanup.** When a session transitions to Offline (via `DisconnectAsync` or TTL eviction), every active Activity for that session is ended (`EndedAt = now()` + `EndedReason = "session-disconnect"` or `"ttl-eviction"`) + emits `Presence.UserStoppedViewing` for each. Verified in `ActivityTrackerTests.DisconnectAsync_*` + `ActivityTrackerTests.TtlEviction_*`.

**I5. Heartbeat is append-only.** No code path mutates a Heartbeat row after insertion (other than retention pruning). Verified by: (a) the `IPresenceRepository` interface having no `UpdateHeartbeatAsync` method; (b) the `Heartbeat` record having no `UpdatedAt`/`UpdatedBy`/`DeletedAt`/`DeletedBy` fields; (c) static grep for `_heartbeats.AddOrUpdate\|_heartbeats\[.*\] =` returning only the AppendHeartbeatAsync call site.

**I6. Heartbeat-event throttling.** `Presence.HeartbeatRecorded` events are emitted at most once per session per `PresenceOptions.HeartbeatEventThrottle` window (default 5 min). 12 heartbeats over a single window produce ≤ 1 event. Verified in `PresenceServiceTests.HeartbeatAsync_ThrottlesHeartbeatRecordedEvent` + `HeartbeatRecorded_KeyEncodesThrottleBoundary`.

### Cluster-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds on the new `Sunfish.Blocks.Presence` package and every downstream consumer (none in this hand-off; consumer-cluster wiring is owned by separate follow-on hand-offs).

**A2.** `dotnet test packages/blocks-presence/tests/` passes ~60–75 tests across all 4 PRs.

**A3.** `AddBlocksPresence()` DI extension works: a downstream consumer registering this extension can resolve `IPresenceService` + `IActivityTracker` + `IPresenceRepository` + `IActivityRepository` + `IPresenceEventPublisher` + `PresenceTtlEvictionService` from the DI container. With and without `configure` argument.

**A4.** Session lifecycle:
- `RegisterAsync(replicaId, clientInfo, capabilities)` creates a PresenceSession with `Status = Online` + emits `Presence.UserConnected`.
- `HeartbeatAsync(sessionId)` refreshes `LastHeartbeatAt` + appends a Heartbeat row + (throttled) emits `Presence.HeartbeatRecorded`.
- `DisconnectAsync(sessionId, reason)` transitions `Status = Offline` + sets `DisconnectedAt` + emits `Presence.UserDisconnected`. Idempotent.
- `GetOnlineRosterAsync()` excludes Offline sessions.

**A5.** TTL eviction:
- A session whose `LastHeartbeatAt < (now - PresenceOptions.SessionTtl)` is transitioned to Offline by `PresenceTtlEvictionService` within the next `EvictionScanInterval` window.
- The transition emits `Presence.UserDisconnected` with `reason = "ttl-eviction"`.
- All the session's active activities are ended + `Presence.UserStoppedViewing` emitted for each.

**A6.** Activity lifecycle:
- `StartViewingAsync(sessionId, "lease", leaseId)` creates an Activity row with `EndedAt = null` + emits `Presence.UserViewing`.
- Re-calling `StartViewingAsync` with the same (sessionId, resourceKind, resourceId, kind) returns the existing row + does NOT re-emit.
- `StopViewingAsync(activityId, reason)` sets `EndedAt = now()` + emits `Presence.UserStoppedViewing`. Idempotent.
- `GetActiveOnResourceAsync("lease", leaseId)` returns all parties currently viewing/editing that lease, tenant-scoped.

**A7.** Multi-tenant isolation: every read across `IPresenceService` + `IActivityTracker` + `IPresenceRepository` + `IActivityRepository` returns empty / null when called from a different tenant context than the data was written under. Verified by `MultiTenantIsolationTests`.

**A8.** PII discipline: no `Presence.*` event payload contains DisplayName / Email / Phone / Address / TaxId / DateOfBirth / ClientInfo / IP-address. Verified mechanically by reflection + the security-engineering spot-check on PR 2.

**A9.** Event-catalog reconciliation: `_shared/engineering/cross-cluster-event-bus-design.md` §3.7 (or renumbered slot) contains rows for all 5 emitted events with idempotency-key shapes matching the implementation.

**A10.** `apps/docs/blocks/blocks-presence/overview.md` published + renders.

**A11.** `active-workstreams.md` row for `blocks-presence` updated to `built` with the 4 PR numbers (via the source W*.md file, not the ledger directly).

**A12.** No modification to `packages/blocks-crew-comms/Presence/PresenceBus.cs` (verified by `git diff main..HEAD -- packages/blocks-crew-comms/Presence/`).

When the PASS gate is met, the next consumer-side hand-offs can proceed:

- **`blocks-crew-comms` TYPING/DELIVERED/SEEN follow-on hand-off** — consumes `IPresenceService` for application-tier presence (orthogonal to the existing transport-tier `PresenceBus`). Owned by crew-comms; not this hand-off.
- **`blocks-property-leases` presence-indicator follow-on** — consumes `IActivityTracker.GetActiveOnResourceAsync("lease", leaseId)` + subscribes to `Presence.UserViewing` to render "X is editing this lease" on `LeaseDetailPage`.
- **`blocks-financial-ar` + `blocks-financial-ap` presence indicators** — same shape, different `ResourceKind`.
- **`blocks-docs-*` presence indicator on `DocumentDetailPage`** — same shape.
- **`accelerators/anchor` Top-bar "Crew" indicator** — consumes `IPresenceService.GetOnlineRosterAsync` to render the per-tenant online roster.

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`:

### H1. `blocks-people-foundation` not yet built

This hand-off has a hard dep on `PartyId` from `Sunfish.Blocks.People.Foundation`. If `blocks-people-foundation` is `building` or `ready-to-build` (not `built`), **STOP** before opening PR 1. File `cob-question-2026-05-XXTHH-MMZ-blocks-presence-people-foundation-not-built.md`. XO will sequence the work order.

**Do NOT inline-stub a `PartyId` placeholder** to unblock; the cross-cluster contract MUST flow through the canonical type.

### H2. Multi-tenant isolation boundary

Per `crdt-friendly-schema-conventions.md` §14: every entity has `TenantId`; cross-tenant reads/writes are forbidden.

`InMemoryPresenceRepository` + `InMemoryActivityRepository` use `ITenantContextAccessor.Current` to filter every query. **If `ITenantContextAccessor` is not yet available**, follow the same fallback procedure as the people-foundation hand-off §H2 (grep for the actual interface name; file `cob-question-*` BEFORE shipping PR 1 if absent entirely).

**Verify multi-tenant isolation in tests EXPLICITLY** (see PR 1 + PR 2 + PR 3 test lists). The tests must demonstrate that presence data created under tenant A cannot be observed from tenant B via any method on any interface.

### H3. `blocks-crew-comms/Presence/PresenceBus.cs` is OUT OF SCOPE

This hand-off **must NOT modify** any file under `packages/blocks-crew-comms/Presence/`. The transport-tier `PresenceBus` (per ADR 0076) is a peer-to-peer signed-HEARTBEAT-frame layer informing chat-message routing. It is correct + sufficient for its purpose; conflating it with application-tier presence would be a regression.

The two surfaces will be **wired in a follow-on PR** in a single direction: crew-comms learns of application-tier disconnect events (via subscription to `Presence.UserDisconnected`) so the chat router can prefer a connected replica for the same party. That follow-on is owned by the crew-comms maintainers, NOT this hand-off.

**If during PR 1–4 you encounter a temptation to "unify" the two presence surfaces:** RESIST. File `cob-question-2026-05-XXTHH-MMZ-blocks-presence-crew-comms-unification-question.md` to surface the question to XO; do not modify either surface preemptively.

### H4. Heartbeat / Activity retention boundary

CRDT §2 says "never DELETE." This hand-off applies the **retention-prune exception** to two entities: `Heartbeat` and `Activity` (ended-only). The default retention windows are:

- `PresenceOptions.HeartbeatRetention = 24h` — Heartbeat rows older than 24h are hard-DELETEd by `PruneHeartbeatsOlderThanAsync`.
- Activity ended-row retention: 7 days post-`EndedAt` (no options knob in v1; constant in the repository).

**The retention boundaries are documented + bounded.** The deviation from CRDT §2 is sound (these are ephemeral telemetry rows, not business records; the volume is unbounded otherwise). If COB feels strongly about gating PR 1 on indefinite retention being available, file `cob-question-2026-05-XXTHH-MMZ-blocks-presence-retention-windows-gating.md`. XO recommendation: ship retention windows as-is; SQLite-backed persistence will fold them into the broader Phase 3 retention-policy substrate later.

**Session-row hard-DELETE at the retention boundary** is similarly bounded (Offline sessions older than 30 days), but is NOT shipped in v1 — sessions accumulate slowly enough that the in-memory v1 repository handles them without pruning. SQLite-backed persistence will add this when needed. Documented in the docs page.

### H5. Live cursor positions deferral

This hand-off does **not** ship live cursor positions (Google-Docs-style multi-cursor / multi-selection rendering). The substrate is a different shape (CRDT-aware cursor projection + per-keystroke broadcast) and is a UI-tier concern that builds on top of `IActivityTracker.GetActiveOnResourceAsync` later.

**If any consumer cluster's pending hand-off needs live cursors:** file `cob-question-2026-05-XXTHH-MMZ-blocks-presence-live-cursors-needed.md` rather than adding it here. XO will sequence a follow-on workstream (`blocks-presence-cursors-*` or similar) with its own design pass.

### H6. Audit-log integration question

Presence events are explicitly NOT audit-logged by default (per §Cross-cluster integration). If a consumer cluster's hand-off requires view-tracking for audit/compliance purposes ("show every party that has viewed this invoice in the last 90 days"), that is a `kernel-audit` concern: file `cob-question-2026-05-XXTHH-MMZ-blocks-presence-audit-integration-question.md` so XO can sequence a `kernel-audit` extension (e.g., `AuditEventType.ResourceViewed`) properly — do NOT pipe `Presence.*` events into the audit log on a side path.

The volume + cardinality arithmetic: a tenant with 5 parties × 24 hours × 2 heartbeats per minute = ~14,400 heartbeat events per day per tenant. Multiplying by audit-log retention (typically years for compliance) is a serious storage + index burden, with no compensating audit value. The audit substrate's events are for material business events; presence is for live perception. Different volumes, different retention, different substrate.

### H7. Canonical event-substrate unavailability

PR 4 wires `IPresenceEventPublisher` to the canonical event substrate IF available. If `kernel-event-bus` (or whatever the canonical interface ends up being) is not stable at PR 4 time:

- PR 4 ships the in-memory publisher as the only registration.
- The docs page notes the limitation explicitly.
- A sibling follow-on hand-off (`blocks-presence-event-bus-wiring-*` or part of the cross-cluster event-bus rollout) wires the canonical substrate later.
- **No `cob-question-*` required** for this case — it's the documented v1 path. File a `cob-question-*` ONLY if the canonical substrate exists but its interface is unclear.

### H8. PresenceOptions tuning question

The default `PresenceOptions` (30s cadence / 90s TTL / 30s eviction scan / 24h heartbeat retention / 5min event throttle) are reasonable starting points. **If COB has empirical evidence (e.g., from Anchor smoke-testing) that the defaults are wrong** (e.g., causing the in-memory repository to OOM, causing the event-bus to throttle the entire cluster, causing the eviction scan to lag), file `cob-question-2026-05-XXTHH-MMZ-blocks-presence-options-tuning.md` with the empirical numbers + proposed new defaults. XO ratifies the change before PR 4 ships.

**Do NOT silently change the defaults without surfacing the data** — the defaults are the contract Anchor's UI tier is wired against.

### H9. ULID helper unavailable

Mirror the people-foundation hand-off §H7: if neither a project-local ULID helper NOR a permissive (BSD/MIT/Apache) third-party ULID NuGet package is acceptable, file `cob-question-*`. Recommendation: `Cysharp.Ulid` (BSD-3-Clause).

### H10. Capability-graph ACL filtering question

v1 ships **tenant-scoped visibility only** — any party in a tenant can see any other party's presence. The memory note `project_phase_2_commercial_scope.md` references capability-trimmed UIs (Bookkeeper sees subset; Tax-advisor sees subset) via `ICapabilityGraph.QueryAsync`. **Presence visibility is NOT yet capability-gated** in v1.

If a consumer cluster's hand-off requires capability-graph filtering on presence reads (e.g., "Tax-advisor cannot see Bookkeeper's presence even within the same tenant"), file `cob-question-2026-05-XXTHH-MMZ-blocks-presence-capability-acl-question.md`. XO will sequence a follow-on workstream paired with the capability-graph substrate landing.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–4 merged to main** (sequentially).
2. **Canonical presence substrate available:** `PresenceSession` + `Heartbeat` + `Activity` + 3 strongly-typed IDs + `IPresenceRepository` + `IActivityRepository` + `IPresenceService` + `IActivityTracker` + `PresenceTtlEvictionService` + `IPresenceEventPublisher` + 5 event records + the `AddBlocksPresence()` DI umbrella are all present in `packages/blocks-presence/`.
3. **`AddBlocksPresence()` DI extension works:** a downstream consumer registering this extension can resolve all the registered services from the DI container. Configurable via `Action<PresenceOptions>`.
4. **Event-catalog edit done:** `_shared/engineering/cross-cluster-event-bus-design.md` §3.7 (or renumbered slot) contains rows for all 5 emitted events with idempotency-key shapes matching the implementation.
5. **Multi-tenant isolation verified:** `MultiTenantIsolationTests` pass across all three entity classes; the repository enforces tenant-scoped reads at the boundary; the security-engineering spot-check on PR 2 confirmed.
6. **PII-in-payload invariant verified:** `EventPayload_NoPiiFields` reflection-based tests pass on all 5 event payloads; the security-engineering spot-check on PR 2 confirmed.
7. **Activity-on-disconnect cleanup verified:** `ActivityTrackerTests.DisconnectAsync_*` + `ActivityTrackerTests.TtlEviction_*` pass; every session disconnect ends every active activity for that session + emits `Presence.UserStoppedViewing` for each.
8. **Acceptance tests A1–A12 pass.**
9. **`apps/docs/blocks/blocks-presence/overview.md` published.**
10. **`active-workstreams.md`** row for `blocks-presence` updated (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`) with `built` status + the 4 PR numbers.
11. **Tests pass:** ~60–75 tests across the package.
12. **No `[Obsolete]` or modification on `packages/blocks-crew-comms/Presence/PresenceBus.cs`** (verified by `git diff main..HEAD -- packages/blocks-crew-comms/Presence/`).

When the PASS gate is met, the next consumer-side hand-offs can proceed in parallel (separate hand-offs; not this one):

- `blocks-crew-comms` TYPING/DELIVERED/SEEN follow-on.
- `blocks-property-leases` "X is editing this lease" indicator.
- `blocks-financial-ar` + `blocks-financial-ap` "Y is viewing this invoice" indicator.
- `blocks-docs-*` "Z is reading this policy" indicator.
- `blocks-work-*` "W is on this work-order" indicator.
- `accelerators/anchor` Top-bar "Crew" indicator.

---

## Cited-symbol verification

**Existing on origin/main (verified at hand-off authoring time 2026-05-17; COB re-verifies during pre-build):**

- `packages/blocks-crew-comms/Presence/PresenceBus.cs` (transport-tier surface; cited in §H3; DO NOT TOUCH) ✓
- `packages/foundation-multitenancy/` (target of `TenantId` reference; verify in pre-build step 6) ✓
- `packages/kernel-event-bus/` (canonical event substrate; PR 4 wires if interface stable) ✓
- ADR 0088 §1 (cluster grouping; multi-actor delegation positioning per Phase 2 commercial scope) ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §2, §3, §4, §5, §13, §14 ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3 (catalog upkeep) ✓
- `_shared/engineering/standing-approved-patterns.md` pattern-001, pattern-005, pattern-006 ✓
- Memory note `project_phase_2_commercial_scope.md` ("Multi-actor delegation") ✓

**To be in place before PR 1 opens (verified in Pre-build checklist):**

- `packages/blocks-people-foundation/Models/PartyId.cs` (hard dep; per Pre-build step 1) — confirmed via people-foundation hand-off PASS gate.

**Introduced by this hand-off** (ship across PRs 1–4):

- New package: `packages/blocks-presence/`
- New types: `PresenceSessionId`, `HeartbeatId`, `ActivityId`, `PresenceStatus`, `ActivityKind`, `PresenceSession`, `Heartbeat`, `Activity`, `PresenceOptions`, `ValidationResult` (if not already in the project-wide validation namespace; otherwise import the existing one from the people-foundation cohort), `ValidationError` (same).
- New event types: `IPresenceEvent` + `UserConnectedEvent` + `HeartbeatRecordedEvent` + `UserDisconnectedEvent` + `UserViewingEvent` + `UserStoppedViewingEvent` + corresponding payload records.
- New services: `IPresenceRepository`, `InMemoryPresenceRepository`, `IActivityRepository`, `InMemoryActivityRepository`, `IPresenceService`, `PresenceService`, `IActivityTracker`, `ActivityTracker`, `IPresenceEventPublisher` + `InMemoryPresenceEventPublisher`, `PresenceTtlEvictionService` (and optionally `EventBusPresencePublisher` for canonical-bus wiring).
- New validators: `PresenceSessionValidator`, `HeartbeatValidator`, `ActivityValidator`.
- New DI extension: `PresenceServiceCollectionExtensions.AddBlocksPresence()` (+ optionally `AddBlocksPresenceWithEventBus()`).
- New docs: `apps/docs/blocks/blocks-presence/overview.md`.
- Attribution: `packages/blocks-presence/NOTICE.md`.
- Catalog edit: `_shared/engineering/cross-cluster-event-bus-design.md` §3.7 — 5 new rows.
- Ledger flip: `icm/_state/workstreams/W<NN>-blocks-presence.md` (source) + re-rendered `icm/_state/active-workstreams.md`.

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check negative existence too (verify `Sunfish.Blocks.Presence.PresenceSession` is genuinely absent from origin/main before shipping PR 1).

---

## Cohort discipline

This hand-off is the **second Phase 3 substrate hand-off under ADR 0088 Path II** (after `blocks-people-foundation`). The COB self-audit pattern applied to the people-foundation substrate hand-off applies here verbatim:

- **`AddBlocksPresence()` naming for the DI extension** — matches the cluster convention (per `pattern-005`).
- **`apps/docs/blocks/<cluster>/overview.md` page convention** — applied in PR 4 (per `pattern-006`).
- **`README.md` at the package root** referencing ADR 0088 + the CRDT conventions + the multi-actor delegation memory note — ship in PR 1.
- **`NOTICE.md` at the package root** documenting clean-room / inspiration-only sources — ship in PR 1.
- **`ConcurrentDictionary` dedup for any cache** — applied in `InMemoryPresenceRepository`, `InMemoryActivityRepository`, `InMemoryPresenceEventPublisher`, and `PresenceService._lastHeartbeatEventAt`.
- **Strong-typed Id records** (ULID-backed) — applied for `PresenceSessionId`, `HeartbeatId`, `ActivityId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — `IPresenceEventPublisher` ships as a local abstraction in PR 2; canonical-bus wiring in PR 4 if substrate stable; otherwise sibling follow-on.
- **Catalog reconciliation** for any cross-cluster event surface — applied in PR 4 (event-bus design §3.7).
- **Per-tenant isolation enforced at the repository boundary** — applied in both repositories + verified in `MultiTenantIsolationTests`.
- **Two-overload constructor (audit-disabled / audit-enabled both-or-neither) pattern** — NOT REQUIRED in v1 (presence is intentionally NOT audit-logged per §H6).
- **Standing-pattern tags in PR descriptions** — PR 1 tags `pattern-001`; PR 4 tags `pattern-005` + `pattern-006`. The short pipeline (no council, no CO click, auto-merge on CI-green) is the explicit intent.

---

## Beacon protocol

If COB hits a halt-condition (H1–H10) or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-blocks-presence-{slug}.md` in `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for `blocks-presence` (via the source W*.md file).
- `ScheduleWakeup 1800s`.

If the security-engineering spot-check on PR 2 returns BLOCKED:

- Do NOT merge PR 2.
- File `cob-question-2026-05-XXTHH-MMZ-blocks-presence-pr2-security-block-{slug}.md` naming the specific defect.
- `ScheduleWakeup 1800s`.

If COB completes PR 4 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file).
- Drop `cob-status-2026-05-XXTHH-MMZ-blocks-presence-built.md` to inbox.
- Continue with the next hand-off in the priority queue.

---

## Cross-references

- **ADR 0088:** `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` §1 (cluster grouping, Phase 3 placement; multi-actor collaboration positioning), §2 (MIT output license), §3 (clean-room discipline).
- **Memory note:** `project_phase_2_commercial_scope.md` — Multi-actor delegation as Phase 2 commercial scope; Bookkeeper + Spouse + Tax-advisor on shared tenants; the perceptual precondition for the capability-trimmed UI surface.
- **CRDT conventions:** `_shared/engineering/crdt-friendly-schema-conventions.md` §1 (ULID), §2 (tombstones — applied with TTL eviction for sessions), §3 (version + revisionVector), §4 (append-only Heartbeat), §5 (stable string codes), §13 (envelope), §14 (tenant isolation).
- **Event bus:** `_shared/engineering/cross-cluster-event-bus-design.md` §1 (envelope), §2 (naming), §3 (catalog upkeep), §3.7 (Presence-domain events — added by this hand-off in PR 4).
- **Standing patterns:** `_shared/engineering/standing-approved-patterns.md` pattern-001 (PR 1), pattern-005 (PR 4 DI umbrella), pattern-006 (PR 4 docs page).
- **Transport-tier counterpart (DO NOT TOUCH):** `packages/blocks-crew-comms/Presence/PresenceBus.cs` + ADR 0076 (peer-to-peer signed HEARTBEAT frames; informs chat-message routing).
- **Predecessor hand-offs (cohort precedent):**
  - `blocks-people-foundation-stage06-handoff.md` (substrate-only pattern; 4-PR shape; DI umbrella + docs + ledger flip pattern; PII discipline cohort; multi-tenant isolation cohort) — DIRECT precedent.
  - `blocks-financial-ledger-chart-and-journal-stage06-handoff.md` (substrate 6-PR shape; cohort root).
  - `blocks-financial-ar-stage06-handoff.md` (local-stub-for-cross-cluster-contract pattern; relevant to `IPresenceEventPublisher` shape).
- **Sibling hand-offs (consumer-side follow-ons, not yet authored):**
  - `blocks-crew-comms-typing-delivered-seen-stage06-handoff.md` (application-tier presence wiring into chat UX).
  - `blocks-property-leases-presence-indicator-stage06-handoff.md`.
  - `blocks-financial-ar-presence-indicator-stage06-handoff.md` (and AP analogue).
  - `blocks-docs-presence-indicator-stage06-handoff.md`.
  - `anchor-top-bar-crew-indicator-stage06-handoff.md` (UI-tier hand-off on Anchor accelerator).

---
