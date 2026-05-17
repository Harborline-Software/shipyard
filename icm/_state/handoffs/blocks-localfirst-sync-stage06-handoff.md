# Hand-off — `blocks-localfirst-sync` Multi-Device Replica Reconciliation Substrate (Phase 3 substrate)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-17
**Status:** `ready-to-build`
**Workstream:** W#60 P4 — Path II native domain, **Phase 3 substrate layer** (multi-device sync)
**Spec source:**
- [ADR 0088 Path II](../../../docs/adrs/0088-anchor-all-in-one-local-first-runtime.md) — Path II decision (CO ratified 2026-05-16); §1 (7-cluster decomposition); §4 (tiered runtime model); §5 (minimum hardware spec); Consequences (CRDT-as-primary-store semantics).
- [`_shared/product/local-node-architecture-paper.md`](../../../_shared/product/local-node-architecture-paper.md) §6.1 (gossip anti-entropy), §6.2 (sync daemon protocol), §2.2 (AP-class CRDT merge), §9 (CRDT growth + GC).
- [`icm/02_architecture/path-ii-crdt-schema-conventions.md`](../../02_architecture/path-ii-crdt-schema-conventions.md) §1 (CP/AP class split), §2 (schema conventions), §6 (peer sync behavior), §7 (Must-Not patterns).
- [`icm/02_architecture/path-ii-cross-cluster-event-bus.md`](../../02_architecture/path-ii-cross-cluster-event-bus.md) §1 (event naming), §2 (envelope), §9 (idempotency + ordering).
**Companion conventions:** [`_shared/engineering/crdt-friendly-schema-conventions.md`](../../../_shared/engineering/crdt-friendly-schema-conventions.md) (older sibling — read first), [`_shared/engineering/cross-cluster-event-bus-design.md`](../../../_shared/engineering/cross-cluster-event-bus-design.md) (companion canonical event-bus spec).
**Existing substrate (consumed, not modified):**
- `packages/foundation-events/` — `IDomainEventStore` + `IDomainEventPublisher` + `EventDispatcherHost` + `SqliteDomainEventStore` (shipped per `foundation-events-stage06-handoff.md`).
- `packages/kernel-sync/` — `IGossipDaemon` + `HandshakeProtocol` + `ISyncDaemonTransport` + `IPeerDiscovery` + `MdnsPeerDiscovery` (Wave 2.1/2.2 shipped).
- `packages/kernel-crdt/` — `ICrdtEngine` + `YDotNetCrdtEngine` (default) + `StubCrdtEngine` (Wave 1.2; ADR 0028).
- `packages/foundation-localfirst/` — `IOfflineStore` + `IOutboundQueue` + `ISyncEngine` + `IConflictResolver` + `IDataExporter` (partial; in-memory reference impls).
**Pipeline:** `sunfish-feature-change`
**Estimated effort:** ~22–28h sunfish-PM (substrate-layer package; 6 PRs; ~85–100 tests across the substrate; security-engineering council on PR 2 + PR 3; .NET architect council on PR 1; SQLite + Tailscale + CRDT integrations).
**PR count:** 6 PRs.
**Pre-merge council:** **MANDATORY on PR 1 (.NET architect)**, **MANDATORY security-engineering on PR 2 (event-cursor sync protocol)** and **PR 3 (conflict resolution policy)**. PR 4 + PR 5 + PR 6 follow standard COB self-audit (no council required unless implementation surfaces a question — see §Halt-conditions for the council-trigger list).
**Audit before build:**
```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-localfirst" 2>&1
grep -rn "Sunfish.Blocks.LocalFirst.Sync" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ --include="*.cs" 2>/dev/null | grep -v bin | grep -v obj
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^foundation-events|^kernel-sync|^kernel-crdt|^foundation-localfirst" 2>&1
```
Expected: first two commands return **empty** (greenfield package + namespace). Third returns the four shipped predecessors. If any line of the first two returns hits, **halt** + file `cob-question-*`; XO needs to reconcile with the pre-existing artifact.

---

## Context

### Why this package exists now

ADR 0088 Path II commits Anchor to a local-first runtime in which **SQLite is the primary store** and **Loro/Yjs CRDT is the sync overlay between local nodes** (ADR 0088 §4 Light tier; §Consequences "CRDT-as-primary-store semantics"). The phrase "between local nodes" carries a specific operational meaning that no shipped package yet implements:

> **Multi-device sync = a single user (or a single small team) running Anchor on 2+ devices keeps state coherent across those devices.**

Examples that drive the requirement:

1. **Property-ops principal** runs Anchor on a Surface Pro 7 (Light tier) at the office *and* on a M3 MacBook Air (Light tier) at home; both must converge on the same `Invoice`, `WorkOrder`, `MaintenanceTask`, and `WikiPage` state without a central server.
2. **Field tech** runs Anchor on a workshop laptop *and* a ruggedized tablet on-site; both replicate the same `MaintenanceSchedule` + `WorkOrder` + `Asset` state; reconciliation happens when both are on the same Tailnet.
3. **Accountant** keeps Anchor on a personal MacBook *and* the office desktop; both converge on `JournalEntry`, `Invoice`, `Bill`, `Payment` rows; CP-class entities (financial) must not duplicate.
4. **Small partnership** — two principals each running Anchor on their own device; both see the same operational state; concurrent edits to AP-class entities (notes, wiki pages, tag sets) merge cleanly via CRDT; concurrent edits to CP-class entities (financial, contracts) coordinate via event-cursor sequencing + last-writer-wins on uncommitted state with conflict reporting.

The substrate to make this work has four shipped predecessors but no shipped consumer-facing package:

| Substrate | Layer | Status | Role in multi-device sync |
|---|---|---|---|
| `kernel-sync` | Transport | Shipped (Wave 2.1/2.2) | Gossip daemon + mDNS peer discovery + handshake + delta exchange (operates over Unix sockets / named pipes / WebSocket / Tailscale) |
| `kernel-crdt` | CRDT engine | Shipped (Wave 1.2, default YDotNet) | Loro-compatible CRDT primitives (`ICrdtDocument`, `ICrdtText`, `ICrdtMap`, `ICrdtList`); used by AP-class entities for field-level merge |
| `foundation-events` | Domain events | Shipped (sibling hand-off, 2026-05-16) | `IDomainEventStore` + `IDomainEventPublisher` + `EventDispatcherHost` + `SqliteDomainEventStore`; the canonical cross-cluster event substrate |
| `foundation-localfirst` | Offline-first orchestration | Partial | `IOfflineStore` + `IOutboundQueue` + `ISyncEngine` + `IConflictResolver`; outbound queue model; in-memory reference impls only |

What's **missing** is the package that:

1. **Enumerates the replicas** participating in a multi-device coordination set (a `Replica` is a single Anchor install on a single device; identified by `ReplicaId` + `DeviceFingerprint`).
2. **Drives event-cursor synchronization** between replicas — each replica advances an event cursor; pull-push protocol exchanges domain events from `foundation-events`'s `IDomainEventStore` so that all replicas converge on the same event log (CP-class entities are then replayed identically; AP-class entities use CRDT-state-vector exchange on top).
3. **Resolves conflicts deterministically** per entity class — CP-class entities use last-writer-wins-with-conflict-emission (the loser is reported, not silently dropped); AP-class entities delegate to the underlying CRDT (Loro/Yjs merge is associative + commutative).
4. **Survives interruption** — a replica that loses network mid-sync resumes from the last confirmed cursor with no state loss; idempotent re-sync produces the same result as a clean sync.
5. **Runs as a background worker** with IPC integration so the Anchor UI can show "syncing… (3/12 replicas reached)" / "synced" / "conflict on Invoice INV-2026-05-17-CW-0042 — review".
6. **Emits domain events** for sync lifecycle (`ReplicaSyncStarted`, `ReplicaSyncCompleted`, `ReplicaSyncConflict`) so other clusters and UI surfaces can react.

This hand-off ships that package: **`blocks-localfirst-sync`**.

### Why it's a `blocks-*` package (not a `foundation-*` or `kernel-*` package)

| Layer | Role | Examples |
|---|---|---|
| **`kernel-*`** | Crypto + sync transport + CRDT engine primitives | `kernel-sync`, `kernel-crdt`, `kernel-event-bus`, `kernel-audit` |
| **`foundation-*`** | Cross-cluster domain-shaped contracts | `foundation-events`, `foundation-localfirst`, `foundation-multitenancy`, `foundation-recovery` |
| **`blocks-*`** | Business-domain composition + workflow orchestration | `blocks-financial-ar`, `blocks-work-orders`, `blocks-localfirst-sync` (this) |

`blocks-localfirst-sync` is the **business-domain composer** for multi-device replica reconciliation: it knows about replicas (a domain concept the user sees in the UI), event cursors (a domain artifact tied to the domain event store), conflict resolution policies per entity class (a domain rule tied to the CP/AP classification in `path-ii-crdt-schema-conventions.md`), and sync lifecycle events (a domain workflow). It composes on top of the kernel-tier transport (`kernel-sync`) and CRDT engine (`kernel-crdt`) and the foundation-tier event store (`foundation-events`).

It is **not** a foundation-tier package because (a) it composes multiple foundation-tier substrates rather than defining a contract, (b) it implements domain workflow (sync rounds, conflict resolution policy choice, IPC integration), and (c) other `blocks-*` clusters will subscribe to *its* lifecycle events.

Per `cross-cluster-event-bus-design.md` §10 Q3 (cluster-vs-foundation home for sync orchestration):

> The replica reconciliation surface is *consumed* by every `blocks-*` cluster but does not define a contract those clusters depend on — it observes their event emissions. Home it in `blocks-localfirst-sync` (cluster); event types it emits ship via `foundation-events`'s canonical envelope.

### Relationship to `kernel-sync` (read carefully)

| Question | `kernel-sync` (transport) | `blocks-localfirst-sync` (this) |
|---|---|---|
| Layer | Kernel | Blocks |
| Wire model | Gossip rounds; peer discovery; delta exchange; framed messages over Unix-socket / named-pipe / WebSocket / Tailscale | Application-level orchestration; consumes the gossip daemon's "delta available" signal and the CRDT engine's state-vector exchange |
| Knows about | Peers, public keys, vector clocks (Lamport), delta blobs, transport fault tolerance | Replicas, devices, event cursors (per handler, per replica), CP/AP entity-class conflict resolution policies, sync lifecycle events |
| Primary consumer | `blocks-localfirst-sync` (this) + `foundation-localfirst` outbound queue (when wired) | The Anchor UI sync-status surface, every `blocks-*` cluster that wants sync lifecycle event subscriptions |
| Persistence | None (volatile vector-clock + transient delta buffers) | SQLite: `replica_registry`, `replica_sync_cursor`, `replica_sync_conflict`, `replica_sync_run` tables |

**`blocks-localfirst-sync` does NOT re-implement `kernel-sync`.** It composes it. Specifically:

- `kernel-sync.IGossipDaemon` performs the round-trip transport (peer discovery, handshake, delta streaming). Its delta is opaque to it; what's *in* the delta is decided by the upstream layers.
- `blocks-localfirst-sync.IEventCursorSync` produces the *payload* of a sync round: it reads the local `foundation-events.IDomainEventStore` from the per-peer cursor position forward, packages new events into a `ReplicaEventBundle`, and hands the bundle to `kernel-sync` for transmission.
- On receipt of a remote `ReplicaEventBundle`, `IEventCursorSync` writes each event into its own `IDomainEventStore` (idempotent via the event's idempotency key), advances the per-peer cursor, and surfaces a `ReplicaSyncCompleted` domain event when the round closes.
- For AP-class entities whose state needs CRDT-state-vector exchange (not just event replay), `blocks-localfirst-sync` invokes `kernel-crdt.ICrdtDocument.ToStateVector()` / `ApplyDelta(...)` per Loro convention; the state-vector bytes are bundled alongside the events.

**Halt condition if confused:** if PR 1 surfaces a need to *fold* `blocks-localfirst-sync` into `kernel-sync`, halt + file `cob-question-*`. The layering is intentional and architecturally load-bearing.

### Relationship to `foundation-events` (read carefully)

`foundation-events` is the canonical cross-cluster event substrate. It provides:

- `IDomainEventStore.AppendAsync<TPayload>(DomainEventEnvelope<TPayload>)` — persists a domain event with idempotency-key uniqueness.
- `IDomainEventStore.ReadAfterAsync(cursor, batchSize)` — reads forward from a cursor (per `event_handler_cursors` per consumer).
- `IDomainEventPublisher.PublishAsync<TPayload>(TPayload payload, ...)` — high-level publish; constructs envelope; appends to store; dispatches in-process handlers via `EventDispatcherHost`.

`blocks-localfirst-sync` **consumes** this substrate in two distinct ways:

1. **As a reader (sync direction: local → remote):** `IEventCursorSync` reads the local event store from the per-peer cursor forward; bundles events; hands to `kernel-sync` for transmission.
2. **As a writer (sync direction: remote → local):** on receipt of a remote bundle, `IEventCursorSync` calls `IDomainEventStore.AppendAsync(...)` for each event. The idempotency-key UNIQUE constraint in the SQLite `domain_events` table is the canonical dedup mechanism; replay of an already-applied event is a no-op.

`blocks-localfirst-sync` also **emits** lifecycle events back into `foundation-events`:

- `ReplicaSyncStarted` — emitted when `SyncWorker` starts a round to a peer.
- `ReplicaSyncCompleted` — emitted when a round closes successfully.
- `ReplicaSyncConflict` — emitted when conflict resolution detects a CP-class collision that the policy reports (e.g., two replicas concurrently issued an `Invoice` with the same number; loser is re-keyed; the conflict surfaces in the UI).
- `ReplicaSyncFailed` — emitted on unrecoverable error after retry budget exhausted.

These lifecycle events flow through the *same* `IDomainEventStore` and *same* `EventDispatcherHost` as every other cross-cluster event. The Anchor UI subscribes via the standard `INotificationHandler<T>` pattern.

### What this hand-off ships

1. **`ReplicaId`** — canonical 2-char value-object identifier per `crdt-friendly-schema-conventions.md` §1 (relocates from local placeholders in `blocks-financial-ar`, etc.).
2. **`DeviceFingerprint`** — composable device identity (OS + machine name + first-NIC MAC hash + Anchor install id); used to detect "is this a new physical device or the same one re-paired?".
3. **`Replica`** record + **`IReplicaRegistry`** — persistent registry of all known replicas (local + remote); enumerates the multi-device coordination set; tracks (a) public key from `kernel-sync` handshake, (b) last-seen timestamp, (c) trust state.
4. **`IEventCursorSync`** + **`InMemoryEventCursorSync`** + **`SqliteEventCursorSync`** — pull-push protocol that reads from `foundation-events.IDomainEventStore` per-replica cursor and exchanges `ReplicaEventBundle` over `kernel-sync`'s delta-stream channel.
5. **`ConflictResolutionPolicy`** + per-entity-class strategy (CP: last-writer-wins-with-emission; AP: delegate-to-CRDT) + `IConflictResolutionPolicyRegistry` (per-entity-type registration).
6. **`ResumableSync`** — checkpoint + retry + bounded exponential backoff + cursor confirmation; **survives mid-round network interruption with no state loss**.
7. **`SyncWorker`** — background `BackgroundService` that drives sync rounds; integrates with `IGossipDaemon` round scheduler; IPC integration so the Anchor UI can render sync status.
8. **`IReplicaSyncStatusReporter`** — IPC-friendly status surface for the Anchor UI (last sync time per replica, conflict count, in-flight round, etc.).
9. **Lifecycle events** — `ReplicaSyncStarted`, `ReplicaSyncCompleted`, `ReplicaSyncConflict`, `ReplicaSyncFailed`, `ReplicaRegistered`, `ReplicaTrustChanged` — all flowing through `foundation-events`'s canonical envelope.
10. **SQLite tables** — `replica_registry`, `replica_sync_cursor`, `replica_sync_run`, `replica_sync_conflict` (per `crdt-friendly-schema-conventions.md` §3 SQLite storage conventions).
11. **DI extension** — `AddBlocksLocalFirstSync(this IServiceCollection)` per pattern-005.
12. **`apps/docs/blocks-localfirst-sync/overview.md`** — cluster docs page; covers the replica model, conflict resolution policy table per entity-class, and operator runbook for conflict triage.
13. **NOTICE.md** — clean-room attribution per ADR 0088 §3.

### What this hand-off does NOT ship

- **Distributed transactions across replicas.** Out of scope. Cross-replica atomicity is not a goal of the substrate; each replica's local SQLite transaction is the atomic unit. Cross-replica consistency is eventually-consistent (CRDT-merge for AP-class; coordinated-event-replay for CP-class).
- **Byzantine fault tolerance.** Out of scope. Replicas are mutually trusted within a coordination set (the trust is established via `kernel-sync`'s handshake + Tailnet ACL). A compromised replica can produce arbitrary events; that's a kernel-tier security concern that `kernel-audit` + `kernel-signatures` address separately. This hand-off assumes a non-byzantine replica set.
- **Cross-tenant or cross-organization sync.** Out of scope. The `federation-*` stack (per ADR 0029) handles inter-organization sync via the managed-relay tier; *this* hand-off is intra-team (Tailnet-local) replica reconciliation only.
- **Selective sync / sparse replication.** Out of scope for v1. Every replica replicates every event in the local tenant's stream. If a future use case requires "replica A holds only `blocks-financial-*` events; replica B holds only `blocks-work-*` events", that's a follow-on hand-off (`blocks-localfirst-sync-selective`); the current substrate is designed to permit it but doesn't implement it.
- **Encryption-at-rest within the replica registry.** The replica registry stores public keys + device fingerprints + last-seen timestamps; per `path-ii-crdt-schema-conventions.md` §7, this is not encrypted-at-rest data (it's metadata, not user-content). If the operator's threat model requires encryption-at-rest for the registry, that's a `kernel-security` follow-on.
- **Real-time push notification of remote changes.** Sync rounds are gossip-scheduled (default 30s interval per `kernel-sync.GossipDaemonOptions`). Sub-second propagation would require a different transport model (out of scope).
- **`SqliteEventCursorSync` PostgreSQL variant.** v1 ships SQLite only (matches ADR 0088 Light tier). A PostgreSQL variant for `Bridge` (Standard/Hosted tier) is a follow-on (`blocks-localfirst-sync-postgres`).
- **UI components.** This is the substrate; the Anchor UI's `SyncStatusPanel` (Blazor + React parity) ships separately in a `blocks-localfirst-sync-ui` follow-on hand-off. The IPC surface (`IReplicaSyncStatusReporter`) is the contract the UI hand-off consumes.

### CRDT-friendly conventions applied (binding)

Per `_shared/engineering/crdt-friendly-schema-conventions.md` + `path-ii-crdt-schema-conventions.md`:

| Convention | Applied where |
|---|---|
| §1 ULID identifiers | `ReplicaSyncRunId`, `ReplicaSyncConflictId`, `ReplicaEventBundleId` — strongly typed; ULID storage |
| §1 Per-replica-suffix monotonic numbering | `ReplicaSyncRun.RunNumber` — format `RUN-YYYY-MM-DD-{ReplicaId}-{seq:D6}`; per-replica monotonic |
| §2 Tombstones (soft-delete) | `Replica.RetiredAtUtc` + `Replica.RetiredReason` — never hard-delete a Replica row (retired replicas can re-appear; we want history) |
| §3 Version + RevisionVector | `Replica.Version` int + `Replica.RevisionVector` Dictionary<string,long> — Loro-managed for the AP-class fields (last-seen, trust-state) |
| §6 Posted-then-immutable | `ReplicaSyncRun` is append-only — never updated after `Completed`/`Failed`/`Cancelled`; only `Running → terminal-state` transitions allowed |
| §7 State-machine — terminal wins | `ReplicaSyncRun.Status` uses Pattern B (terminal-wins): `Failed/Cancelled > Completed > Running` |
| §10 Two-tier validation | Tier-1 write-time validation on every `Replica` persist (well-formed `ReplicaId`, well-formed `DeviceFingerprint`); Tier-2 post-merge reconciler verifies no two replicas share a (`PublicKey`, `DeviceFingerprint`) tuple after merge |
| Path II §1 CP/AP classification | `Replica` core fields = CP (identity is canonical); `Replica.lastSeenAtUtc` + `Replica.trustState` = AP (LWW under Loro); `ReplicaSyncRun` = CP (audit history); `ReplicaSyncConflict` = CP (audit history) |
| Path II §2.1 CP schema | All `*Run` and `*Conflict` records carry `Version` + `LastEventHash` + `IsPosted` markers |
| Path II §6 Peer sync behavior | This hand-off IMPLEMENTS the substrate Path II §6 describes — it IS the sync layer |

### Open question — CRDT library choice (FLAG FOR XO/CO BEFORE PR 1)

**This is the major design choice in this hand-off. Do NOT proceed to PR 1 until XO/CO ratifies.**

`kernel-crdt` currently ships **YDotNet (Yjs/yrs)** as default (per `kernel-crdt/SPIKE-OUTCOME.md` 2026-04-22) — the Loro evaluation deferred because `LoroCs` 1.10.3 was "very bare bones" (no snapshot/delta/vector-clock surface). The ADR 0088 §2 license-posture column lists **Loro (MIT)**, **Automerge (MIT)**, **Yjs (MIT)** as permissive-borrowable; the path-ii-crdt-schema-conventions §1 names **Loro** as the preferred AP-class CRDT.

There is a **mismatch** between:
- The *naming* convention (`*_loro` blob column suffix per §2.2), which implies Loro is the canonical CRDT.
- The *shipped* CRDT engine in `kernel-crdt`, which is YDotNet (Yjs/yrs).

Both Yjs and Loro are MIT-licensed; both are mathematically sound CRDTs; **for this substrate's purposes, they are functionally equivalent** — the substrate uses `kernel-crdt.ICrdtEngine.ToSnapshot()` / `ApplyV1()` per the engine-agnostic contract. The choice between them is downstream of this hand-off.

**XO recommendation (binding unless CO overrides):**

> **Proceed with the shipped YDotNet backend for v1 of this substrate**, while preserving the Loro path. The substrate uses the engine-agnostic `kernel-crdt.ICrdtEngine` contract; YDotNet vs Loro is a backend swap that does not affect this hand-off's public surface. Rename the `*_loro` column convention to `*_crdt` in a follow-on naming-canon update (the suffix is engine-implying; per `kernel-crdt/README.md` "swapping to a Loro backend later" — the contracts in the engine package stay the same, but the column-suffix convention should also stay engine-agnostic).
>
> **Rationale (recommend Loro as the eventual canonical when LoroCs surface matures):** Loro is the Rust-core lineage that the ADR 0088 §2 and path-ii-crdt-schema-conventions §1 anchored on; it has stronger growth + GC properties per paper §9 considerations; it's MIT-licensed (permissive borrow OK with attribution). When `LoroCs` exposes the snapshot/delta/vector-clock surface (next-try prerequisites per `kernel-crdt/SPIKE-OUTCOME.md`), a Loro backend lands in `kernel-crdt` (per its README "Swapping to a Loro backend later") and this substrate inherits it via DI swap with no surface change. **In the interim, YDotNet (Yjs) is the right pragmatic default.**

**If CO/XO ratifies the alternative ("custom event-merge — no third-party CRDT for the AP-class state-vector exchange"):**

- CP-class entities work the same (event-cursor replay; idempotent apply by event-id).
- AP-class entities lose CRDT-merge semantics; concurrent edits to text/set/scalar fields become last-writer-wins-by-timestamp with conflict emission (no automatic mathematical merge). This degrades the user experience for wiki / notes / tags / dashboard layout / lead pipeline.
- The package surface remains identical; only the internal `ConflictResolutionPolicy` strategy for AP-class entities changes (no `ICrdtEngine` dependency).
- Implementation effort: −2–3h (no Loro/YDotNet integration); but a future migration to a real CRDT later is harder than starting with one.

**XO does NOT recommend the custom-event-merge alternative.** AP-class entities are a meaningful slice of the domain (wiki body, tags, contact-info arrays, dashboard layout); losing CRDT-merge there means losing the local-first-collaborative pitch.

**Halt this hand-off until XO/CO returns a ratification on:** "Proceed with YDotNet via `kernel-crdt.ICrdtEngine` default; rename `*_loro` suffix to `*_crdt` in a follow-on naming-canon update; future Loro swap is a DI-only change." OR "Use custom event-merge for AP-class; defer real-CRDT to a follow-on." File `cob-question-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-crdt-choice.md` if XO/CO has not ratified by the time you read this hand-off.

### Open question — transport choice (FLAG FOR XO/CO BEFORE PR 2)

The substrate uses `kernel-sync.ISyncDaemonTransport` for delta exchange. The shipped transport options are:

| Transport | Status | When to use |
|---|---|---|
| `InMemorySyncDaemonTransport` | Shipped (tests only) | Unit + integration tests; never production |
| `UnixSocketSyncDaemonTransport` | Shipped (operational) | Single-host inter-process (e.g., Anchor desktop + Anchor menubar on the same Mac) |
| `WebSocketSyncDaemonTransport` | Shipped (operational) | Cross-host WebSocket; HTTP/2 over Tailscale (the canonical Light tier multi-device transport) |

**Transport-choice question:** is this substrate **Tailscale-only**, or does it support **multi-transport** (Tailscale + LAN-direct + future BLE-Sync)?

**XO recommendation (binding unless CO overrides):**

> **Multi-transport via the `ISyncDaemonTransport` abstraction; Tailscale (WebSocket-over-Tailnet) is the canonical Light tier default. LAN-direct (mDNS + UnixSocket-equivalent-WebSocket) is a follow-on optimization. BLE-Sync is out of scope.**
>
> Rationale: (a) `kernel-sync` already abstracts the transport; this substrate inherits the abstraction at no cost; (b) Tailscale-only is operationally simplest (the Tailnet provides discovery + auth + NAT traversal for free) and aligns with `providers-mesh-headscale` (Headscale substitution for Tailscale BSL per ADR 0067); (c) LAN-direct can be added as a follow-on without API change.

**If CO/XO ratifies Tailscale-only:**

- Drop the `MultipleTransports` configuration option in `BlocksLocalFirstSyncOptions`.
- `SyncWorker` hardcodes `WebSocketSyncDaemonTransport` via DI.
- Implementation effort: −0.5h (slightly simpler).
- Operational risk: a future use case requiring LAN-direct (e.g., offline-LAN field deployment without Tailnet reachability) requires a follow-on hand-off.

**Halt this hand-off until XO/CO returns a ratification on:** "Multi-transport via abstraction, Tailscale default" OR "Tailscale-only, simpler config". File `cob-question-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-transport-choice.md` if not ratified by PR 2 authoring.

---

## Pre-build checklist (COB executes before opening PR 1)

1. **Verify the substrate is greenfield.**

   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ | grep -E "^blocks-localfirst"
   grep -rn "Sunfish.Blocks.LocalFirst.Sync" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/ --include="*.cs" 2>/dev/null | grep -v bin | grep -v obj
   ```

   Expected: both empty. If either returns hits, **halt** + file `cob-question-*`.

2. **Verify the four predecessor substrates are shipped.**

   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/      # canonical event store
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-sync/             # transport + gossip
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-crdt/             # CRDT engine
   ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-localfirst/   # offline-first orchestration
   ```

   Expected: all four exist. If `foundation-events/` does not exist, **STOP** — the sibling event-bus hand-off must land first. Drop a `cob-question-*` beacon.

   ```bash
   grep -n "IDomainEventStore\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/*.cs 2>&1 | head -5
   grep -n "SqliteDomainEventStore\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/Sqlite/*.cs 2>&1 | head -5
   grep -n "EventDispatcherHost\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/foundation-events/*.cs 2>&1 | head -5
   grep -n "IGossipDaemon\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-sync/Gossip/IGossipDaemon.cs 2>&1
   grep -n "ICrdtEngine\b" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-crdt/ICrdtEngine.cs 2>&1
   ```

   Expected: all symbols present.

3. **Verify the two open questions are RATIFIED.**

   ```bash
   ls /Users/christopherwood/Projects/Harborline-Software/coordination/_archive/ 2>&1 | grep -E "localfirst-sync-(crdt|transport)-choice"
   ```

   Expected: at least two ruling beacons archived (one for CRDT choice; one for transport choice). If empty, **HALT** + file `cob-question-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-await-rulings.md` describing the two open questions; do NOT proceed to PR 1 without XO/CO ratification.

4. **Confirm ADR 0088 status.**

   ```bash
   grep "^status:" /Users/christopherwood/Projects/Harborline-Software/shipyard/docs/adrs/0088-anchor-all-in-one-local-first-runtime.md
   ```

   Expected: `status: Proposed` or `Accepted`. Hand-off is `ready-to-build` either way — CO ratified the design 2026-05-16.

5. **Confirm no parallel-session PRs touch `blocks-localfirst-sync/`, `foundation-events/`, `kernel-sync/`, or `kernel-crdt/`.**

   ```bash
   gh pr list --state open --search "blocks-localfirst in:title,body"
   gh pr list --state open --search "foundation-events in:title,body"
   gh pr list --state open --search "kernel-sync in:title,body"
   gh pr list --state open --search "kernel-crdt in:title,body"
   ```

   Expected: empty (or only this hand-off's own PRs). If anything else is open, file `cob-question-*` — a parallel session may be modifying a substrate this hand-off depends on.

6. **Confirm `but status` (or `git status`) is clean** and the current branch is `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

7. **Read the canonical sources.**
   - ADR 0088 in full.
   - `path-ii-crdt-schema-conventions.md` §1, §2, §6, §7 (the must-not-patterns list).
   - `path-ii-cross-cluster-event-bus.md` §1, §2, §9.
   - `crdt-friendly-schema-conventions.md` §1, §3, §10.
   - `foundation-events-stage06-handoff.md` (the predecessor; understand what `IDomainEventStore` + `EventDispatcherHost` already provide).
   - `kernel-sync/README.md` (transport layer; what gossip + handshake + peer discovery already does).
   - `kernel-crdt/README.md` + `SPIKE-OUTCOME.md` (CRDT engine; default backend; Loro deferral).

8. **License-classification gate (per ADR 0088 §3).** This hand-off touches CRDT + sync substrate; the FOSS sources surveyed below are studied for inspiration only; the implementation is clean-room. Confirm:
   - **Loro source** (MIT; permissive borrow OK with attribution) — STUDIED for substrate design; algorithm citations in source-header comments where load-bearing.
   - **Automerge source** (MIT; permissive) — STUDIED; cited in NOTICE.md (replica-sync state-vector exchange pattern).
   - **Yjs source** (MIT; permissive) — STUDIED; cited in NOTICE.md (engine-agnostic CRDT contract pattern; YDotNet is the shipped binding).
   - **Iroh source** (Apache 2.0; permissive) — STUDIED; cited in NOTICE.md (replica enumeration + multi-device pairing pattern).
   - **NO copyleft sources studied** for this hand-off (Tryton's sync layer is AGPL — explicitly excluded).
   - **No `git diff`** opened against any GPL/AGPL repo while authoring this hand-off's PRs.

---

## Per-PR deliverables

This hand-off splits into **6 PRs** by responsibility:

- **PR 1** — Package scaffold + `ReplicaId` + `DeviceFingerprint` + `Replica` + `IReplicaRegistry` + `InMemoryReplicaRegistry` + `SqliteReplicaRegistry` + DI extension. (**Substrate contract — .NET architect council MANDATORY.**)
- **PR 2** — `IEventCursorSync` pull-push protocol + `ReplicaEventBundle` + `InMemoryEventCursorSync` + `SqliteEventCursorSync` + integration with `IGossipDaemon`. (**Sync protocol — security-engineering council MANDATORY.**)
- **PR 3** — `ConflictResolutionPolicy` + per-entity-class strategy (CP last-writer-wins; AP delegate-to-CRDT) + `IConflictResolutionPolicyRegistry` + conflict emission. (**Replica trust + resolution policy — security-engineering council MANDATORY.**)
- **PR 4** — `ResumableSync` — checkpoint + retry + bounded exponential backoff + cursor confirmation + crash-resume tests.
- **PR 5** — `SyncWorker` background service + IPC integration (`IReplicaSyncStatusReporter`) + integration with `IGossipDaemon` round scheduler.
- **PR 6** — DI umbrella extension (`AddBlocksLocalFirstSync`) + docs (`apps/docs/blocks-localfirst-sync/overview.md`) + ledger flip (`active-workstreams.md` row → `built`).

PRs 1 → 2 → 3 → 4 → 5 → 6 are sequential. PR 4 can begin once PR 3 lands; PR 5 cannot start until PR 4 lands.

---

### PR 1 — Package scaffold + `ReplicaId` + `DeviceFingerprint` + `Replica` + `IReplicaRegistry` + DI

**Estimated effort:** ~4–5h
**Scope:** new package `blocks-localfirst-sync`; canonical replica-identity value-objects; persistent replica registry (SQLite + in-memory + tests); DI extension (audit-disabled + audit-enabled overloads per pattern-005); NO sync protocol yet (PR 2); NO conflict resolution yet (PR 3).
**Commit subject:** `feat(blocks-localfirst-sync): scaffold replica registry substrate per ADR 0088 Path II §1 + §4`
**Branch:** `cob/blocks-localfirst-sync-scaffold`
**Council (MANDATORY):** .NET architect — review the substrate-contract surface before merge. Specifically: `IReplicaRegistry` shape, `Replica` schema, `DeviceFingerprint` composition rules, DI extension overload pattern, SQLite migration scaffolding.

#### Package skeleton (pattern-001)

```
packages/blocks-localfirst-sync/
├── README.md
├── NOTICE.md                                              (Loro + Automerge + Yjs + Iroh attribution)
├── Sunfish.Blocks.LocalFirst.Sync.csproj
├── Models/
│   ├── ReplicaId.cs                                       (canonical; relocates from blocks-financial-ar)
│   ├── DeviceFingerprint.cs
│   ├── Replica.cs
│   ├── ReplicaTrustState.cs                               (enum)
│   ├── ReplicaSyncRunId.cs                                (PR 2; stub for PR 1)
│   ├── ReplicaSyncConflictId.cs                           (PR 3; stub for PR 1)
│   └── ReplicaEventBundleId.cs                            (PR 2; stub for PR 1)
├── Services/
│   ├── IReplicaRegistry.cs
│   ├── InMemoryReplicaRegistry.cs
│   ├── IDeviceFingerprinter.cs
│   └── DefaultDeviceFingerprinter.cs
├── Sqlite/
│   ├── SqliteReplicaRegistry.cs
│   └── Migrations/
│       └── 001_CreateReplicaRegistryTable.sql
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs                     (audit-disabled + audit-enabled overloads)
└── tests/
    ├── Sunfish.Blocks.LocalFirst.Sync.Tests.csproj
    ├── ReplicaIdTests.cs
    ├── DeviceFingerprintTests.cs
    ├── ReplicaRecordTests.cs
    ├── InMemoryReplicaRegistryTests.cs
    ├── SqliteReplicaRegistryTests.cs
    └── ServiceCollectionExtensionsTests.cs
```

#### New types — `Models/ReplicaId.cs`

Per `crdt-friendly-schema-conventions.md` §1 and the established `blocks-financial-ar/Models/ReplicaId.cs` placeholder pattern.

```text
ReplicaId
├── Value: string (exactly 2 chars; uppercase alphanumeric)
├── Validation: length-2, all alphanumeric, normalized to uppercase
├── Equality: value-equality
└── ToString: returns Value
```

Per the previous local placeholders in `blocks-financial-ar`, `blocks-financial-ap`, etc., this is the **canonical** `ReplicaId`. PR 6 of this hand-off retrofits the local placeholders to consume this canonical type (via a NUGET-style `using` directive update; no behavioral change). The retrofit ships as a separate sweep PR after PR 6 if too many consumer packages would need touching.

**Tier-1 validation (per CRDT conventions §10):**
- `ArgumentException` on null/empty/length-not-2/non-alphanumeric.
- `IsWellFormed(string)` static helper returns `bool`.
- Normalized to uppercase on construction.

#### New types — `Models/DeviceFingerprint.cs`

```text
DeviceFingerprint
├── OperatingSystem: string (e.g. "Windows 10.0.22631", "macOS 14.5", "Ubuntu 22.04")
├── MachineName: string (Environment.MachineName at fingerprint time)
├── PrimaryNicMacHash: string (SHA-256 of first non-loopback NIC MAC; hex-encoded; 64 chars)
├── AnchorInstallId: string (GUID created on first Anchor launch; persisted in OS-appropriate config)
└── Hash: string (SHA-256 of canonical-JSON of above 4 fields; idempotency key for "is this the same physical device?")
```

`DefaultDeviceFingerprinter` composes the fingerprint at install time + caches it; subsequent calls return the cached value unless `RefreshAsync()` is invoked (e.g., after hardware replacement).

**Privacy note (cited in NOTICE + apps/docs):** the MAC address is hashed (SHA-256), never stored raw. The hash is sufficient to detect "is this the same physical device?" without leaking the underlying MAC. Per `path-ii-crdt-schema-conventions.md` §7, this metadata is not encrypted-at-rest (it's not user-content), but it is hashed to prevent reverse-engineering the device's network identity from the registry.

**Tier-1 validation:**
- All 4 source fields are non-null + non-empty.
- `PrimaryNicMacHash` matches `^[a-f0-9]{64}$`.
- `AnchorInstallId` parses as `Guid`.
- `Hash` is recomputed on `IsValid()` and compared (tamper-evidence).

#### New types — `Models/Replica.cs` + `Models/ReplicaTrustState.cs`

```text
Replica (CP-class core fields; AP-class metadata fields)
├── ReplicaId: ReplicaId
├── DeviceFingerprint: DeviceFingerprint
├── PublicKey: byte[] (from kernel-sync handshake; ed25519; 32 bytes)
├── PublicKeyHash: string (SHA-256 hex; for stable lookup)
├── DisplayName: string ("Chris's Surface Pro 7"; user-settable)
├── PairedAtUtc: Instant (when this replica joined the coordination set)
├── PairedByPartyId: PartyId (which party initiated the pairing)
├── TrustState: ReplicaTrustState
├── LastSeenAtUtc: Instant? (AP: LWW; last successful sync round)
├── LastSyncRunId: ReplicaSyncRunId? (AP: LWW; last sync run id)
├── RetiredAtUtc: Instant? (tombstone; per §2 — never hard-delete)
├── RetiredReason: string? (e.g. "device lost", "device sold", "user replaced")
├── Version: long (per §3 — Loro/Yjs-managed envelope)
├── RevisionVector: IReadOnlyDictionary<string, long>? (per §3 — Loro/Yjs-managed)
└── (audit: CreatedAtUtc, UpdatedAtUtc, UpdatedBy)

ReplicaTrustState
├── Pending          (paired via kernel-sync handshake; not yet user-approved)
├── Trusted          (user-approved; sync rounds proceed)
├── Quarantined      (user-quarantined; sync rounds skip; UI shows badge)
└── Revoked          (terminal; sync rounds reject; can re-pair as a NEW replica with a different ReplicaId)
```

**State-machine rules (Tier-1 + Tier-2 validation):**

| From | Allowed targets |
|---|---|
| `Pending` | `Trusted` (user approves), `Quarantined` (user rejects), `Revoked` (terminal) |
| `Trusted` | `Quarantined` (user disables temporarily), `Revoked` (terminal) |
| `Quarantined` | `Trusted` (user re-enables), `Revoked` (terminal) |
| `Revoked` | (terminal; no transitions) |

Per `path-ii-crdt-schema-conventions.md` §7 (terminal-wins pattern): `Revoked > Quarantined > Trusted > Pending`. Resolver implemented as `ReplicaTrustStateResolver` registered with `kernel-crdt` (PR 3 wires this; PR 1 ships the static transition map).

#### New types — `Services/IReplicaRegistry.cs`

```text
IReplicaRegistry
├── Task<Replica?> GetByIdAsync(ReplicaId id, CancellationToken ct = default)
├── Task<Replica?> GetByPublicKeyHashAsync(string hash, CancellationToken ct = default)
├── Task<IReadOnlyList<Replica>> ListActiveAsync(CancellationToken ct = default)  // excludes Retired + Revoked
├── Task<IReadOnlyList<Replica>> ListAllAsync(CancellationToken ct = default)
├── Task RegisterAsync(Replica replica, CancellationToken ct = default)            // tier-1 validation; emits ReplicaRegistered
├── Task UpdateTrustStateAsync(ReplicaId id, ReplicaTrustState newState, PartyId by, string? reason, CancellationToken ct = default)
├── Task UpdateLastSeenAsync(ReplicaId id, Instant lastSeenUtc, ReplicaSyncRunId lastRunId, CancellationToken ct = default)
├── Task RetireAsync(ReplicaId id, string reason, PartyId by, CancellationToken ct = default)  // tombstone, NOT hard-delete
└── Task<Replica?> GetLocalReplicaAsync(CancellationToken ct = default)            // the canonical "this device" record
```

#### New types — `Services/IDeviceFingerprinter.cs`

```text
IDeviceFingerprinter
├── Task<DeviceFingerprint> ComputeAsync(CancellationToken ct = default)
├── Task<DeviceFingerprint> GetCachedAsync(CancellationToken ct = default)
└── Task<DeviceFingerprint> RefreshAsync(CancellationToken ct = default)
```

`DefaultDeviceFingerprinter` uses `Environment.OSVersion`, `Environment.MachineName`, `System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()` (filter out loopback + virtual + disabled), and the persisted `AnchorInstallId` from a known file path (e.g., `~/.anchor/install-id` on POSIX; `%APPDATA%\Anchor\install-id` on Windows).

#### SQLite schema — `Sqlite/Migrations/001_CreateReplicaRegistryTable.sql`

Per `path-ii-crdt-schema-conventions.md` §3 SQLite storage conventions for CP-class:

```text
CREATE TABLE LocalFirstSync_ReplicaRegistry (
  ReplicaId            TEXT NOT NULL PRIMARY KEY,
  DeviceFingerprintJson TEXT NOT NULL,
  PublicKey            BLOB NOT NULL,
  PublicKeyHash        TEXT NOT NULL UNIQUE,
  DisplayName          TEXT NOT NULL,
  PairedAtUtc          TEXT NOT NULL,
  PairedByPartyId      TEXT NOT NULL,
  TrustState           TEXT NOT NULL,                       -- enum as string code (§5 stable string codes)
  LastSeenAtUtc        TEXT NULL,
  LastSyncRunId        TEXT NULL,
  RetiredAtUtc         TEXT NULL,
  RetiredReason        TEXT NULL,
  Version              INTEGER NOT NULL DEFAULT 0,
  RevisionVectorJson   TEXT NULL,
  CreatedAtUtc         TEXT NOT NULL,
  UpdatedAtUtc         TEXT NOT NULL,
  UpdatedBy            TEXT NULL
) WITHOUT ROWID;

CREATE INDEX IX_ReplicaRegistry_TrustState  ON LocalFirstSync_ReplicaRegistry (TrustState);
CREATE INDEX IX_ReplicaRegistry_LastSeen    ON LocalFirstSync_ReplicaRegistry (LastSeenAtUtc DESC);
CREATE INDEX IX_ReplicaRegistry_PublicKeyHash ON LocalFirstSync_ReplicaRegistry (PublicKeyHash);
```

**Note:** `BLOB` for `PublicKey` per `kernel-sync` handshake's ed25519 32-byte format. `TEXT NOT NULL UNIQUE` on `PublicKeyHash` — Tier-1 uniqueness; two replicas may not share a public key.

#### DI extension — `DependencyInjection/ServiceCollectionExtensions.cs`

Per pattern-005 + the audit-disabled + audit-enabled both-or-neither cohort discipline:

```text
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlocksLocalFirstSync(
        this IServiceCollection services,
        Action<BlocksLocalFirstSyncOptions>? configure = null);

    public static IServiceCollection AddBlocksLocalFirstSync(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Sunfish:BlocksLocalFirstSync");

    // (PR 5 adds a third overload that registers the background SyncWorker)
}

public sealed class BlocksLocalFirstSyncOptions
{
    public ReplicaId? LocalReplicaId { get; init; }      // null = auto-generate from device fingerprint at startup
    public string? RegistrySqlitePath { get; init; }     // null = in-memory; set for SQLite persistence
    public bool EnableAudit { get; init; } = false;      // audit-disabled by default
    public Action<IServiceCollection>? ConfigureAudit { get; init; }
}
```

When `EnableAudit == true`, the DI extension wires `IReplicaRegistry`'s mutation calls to also `Append` to `kernel-audit.IAuditWriter`. Both-or-neither — if `EnableAudit` is true, `ConfigureAudit` must be non-null (validated; throws on `BuildServiceProvider`).

#### Tests (PR 1)

`tests/ReplicaIdTests.cs`:
- `Construction_TwoCharAlphanumeric_Succeeds` (positive cases: "CW", "A4", "ZZ", "99").
- `Construction_NullOrEmpty_Throws`.
- `Construction_LengthNot2_Throws` (1 char, 3 chars).
- `Construction_NonAlphanumeric_Throws` ("@@", "C ").
- `Construction_NormalizesToUppercase` ("cw" → "CW").
- `IsWellFormed_AcceptsValid_RejectsInvalid`.
- `Equality_ValueEquality_True`.
- `ToString_ReturnsValue`.

`tests/DeviceFingerprintTests.cs`:
- `Construction_ValidFields_Succeeds`.
- `Construction_RejectsEmptyOS`.
- `Construction_RejectsBadMacHash` (not-hex, wrong length).
- `Construction_RejectsBadAnchorInstallId` (not-a-GUID).
- `Hash_DeterministicForSameInputs`.
- `Hash_DifferentForDifferentInputs`.
- `IsValid_TamperedHash_ReturnsFalse`.
- `DefaultDeviceFingerprinter_ComputeAsync_PopulatesAllFields` (integration test; gated on test env).
- `DefaultDeviceFingerprinter_GetCachedAsync_ReturnsSameInstance`.
- `DefaultDeviceFingerprinter_RefreshAsync_RecomputesAndUpdatesCache`.

`tests/ReplicaRecordTests.cs`:
- `Construction_AllFields_PreservesValues`.
- `TrustState_Pending_IsDefault`.
- `RetiredAtUtc_Null_BeforeRetirement`.
- `Version_StartsAtZero`.

`tests/InMemoryReplicaRegistryTests.cs`:
- `RegisterAsync_NewReplica_Persists`.
- `RegisterAsync_DuplicatePublicKey_Throws`.
- `GetByIdAsync_KnownId_ReturnsReplica`.
- `GetByIdAsync_UnknownId_ReturnsNull`.
- `GetByPublicKeyHashAsync_RoundTrips`.
- `ListActiveAsync_ExcludesRetired`.
- `ListActiveAsync_ExcludesRevoked`.
- `ListAllAsync_IncludesEverything`.
- `UpdateTrustStateAsync_AllowedTransition_Succeeds` (4× positive transitions).
- `UpdateTrustStateAsync_DisallowedTransition_Throws` (3× negative).
- `UpdateLastSeenAsync_BumpsVersion`.
- `RetireAsync_SetsRetiredAtUtc_PreservesRow`.
- `RetireAsync_TwiceIdempotent`.
- `GetLocalReplicaAsync_ReturnsLocalReplica_WhenRegistered`.

`tests/SqliteReplicaRegistryTests.cs`:
- (Same matrix as in-memory, against a real SQLite file.)
- `Migration_CreatesTableAndIndexes`.
- `Migration_Idempotent_ReRunSafe`.
- `Persistence_SurvivesProcessRestart` (write, dispose connection, reopen, read).

`tests/ServiceCollectionExtensionsTests.cs`:
- `AddBlocksLocalFirstSync_NoConfigure_UsesDefaults`.
- `AddBlocksLocalFirstSync_WithSqlitePath_RegistersSqliteRegistry`.
- `AddBlocksLocalFirstSync_WithoutSqlitePath_RegistersInMemoryRegistry`.
- `AddBlocksLocalFirstSync_EnableAuditTrue_NoConfigureAudit_ThrowsOnBuild`.
- `AddBlocksLocalFirstSync_EnableAuditTrue_ConfigureAuditProvided_WiresAuditWriter`.
- `AddBlocksLocalFirstSync_CalledTwice_TryAddSemantics_SingleRegistration`.

Total new tests this PR: ~50.

#### Verification

- `dotnet build` succeeds for the new package + adds it to the solution.
- `dotnet test packages/blocks-localfirst-sync/tests/` passes all ~50 tests.
- `grep -r "Sunfish.Blocks.LocalFirst.Sync" packages/blocks-localfirst-sync/` returns hits in every `.cs` file (namespace sanity).
- The SQLite migration runs cleanly against a fresh database; re-run is idempotent.

#### Do NOT in this PR

- Do NOT implement the sync protocol. PR 2 ships `IEventCursorSync`.
- Do NOT implement conflict resolution. PR 3 ships `ConflictResolutionPolicy`.
- Do NOT touch `blocks-financial-ar`'s local `ReplicaId` placeholder. A separate retrofit sweep PR (after PR 6) handles relocation.
- Do NOT wire `SyncWorker` into the host yet. PR 5 ships the background service.
- Do NOT emit `ReplicaRegistered` event yet — wait for PR 6 once the lifecycle event catalog is complete (or ship a stub event-publisher in this PR that PR 6 wires). XO recommendation: ship the stub event-publisher in this PR; PR 6 swaps to `IDomainEventPublisher` from `foundation-events`.

---

### PR 2 — `IEventCursorSync` pull-push protocol + `ReplicaEventBundle` + transport integration

**Estimated effort:** ~5–6h
**Scope:** the pull-push sync protocol over `foundation-events.IDomainEventStore`; bundle envelope; per-replica cursor model; `kernel-sync` transport integration; NO conflict resolution (PR 3); NO resumability (PR 4); NO background worker (PR 5).
**Commit subject:** `feat(blocks-localfirst-sync): event-cursor pull-push sync protocol per Path II §6 + cross-cluster event-bus §9`
**Depends on:** PR 1 merged.
**Branch:** `cob/blocks-localfirst-sync-event-cursor`
**Council (MANDATORY):** security-engineering. The pull-push protocol is auth-adjacent — it accepts domain events from a remote replica and inserts them into the local event store. Council reviews: (a) the trust check before accepting a bundle (must be from a `Trusted` replica per `IReplicaRegistry`), (b) the idempotency contract (re-applied events must be no-op), (c) the bundle-signing requirement (bundles signed by the source replica's public key from `kernel-sync` handshake), (d) the bundle-size bound (DoS prevention), (e) the rate-limit (per-peer round limit).

#### New types — `Models/ReplicaEventBundle.cs`

```text
ReplicaEventBundle
├── BundleId: ReplicaEventBundleId                                    (ULID)
├── SourceReplicaId: ReplicaId                                        (who sent)
├── TargetReplicaId: ReplicaId                                        (who receives)
├── TenantId: string                                                  (per envelope; per-tenant scope)
├── FromCursor: long                                                  (where in source's IDomainEventStore this bundle starts; exclusive)
├── ToCursor: long                                                    (where in source's IDomainEventStore this bundle ends; inclusive)
├── Events: IReadOnlyList<DomainEventEnvelope<JsonElement>>           (the canonical envelopes; payload is JsonElement for transport-agnostic carriage)
├── CrdtStateVectors: IReadOnlyDictionary<string, byte[]>?            (optional; AP-class entity-id → Loro/Yjs state-vector bytes)
├── CrdtDeltas: IReadOnlyDictionary<string, byte[]>?                  (optional; AP-class entity-id → Loro/Yjs delta bytes)
├── BundleSignature: byte[]                                           (ed25519 signature over BundleId + From + To + Events-hash; source replica's public key from kernel-sync)
├── CreatedAtUtc: Instant
└── SchemaVersion: string                                             (default "1.0.0")
```

**Bundle-size bound:** `MaxEventsPerBundle = 1000` (default; configurable). A pull request that would exceed this is split into multiple bundles; the receiver acknowledges each independently. Per security-council guidance, the bound prevents an adversarial source from forcing the receiver to allocate arbitrary memory.

**Bundle signature:** the source replica signs the bundle with its `kernel-sync` handshake private key (ed25519). The receiver verifies the signature against the source replica's `PublicKey` from `IReplicaRegistry`. Signature failure → reject bundle + emit `ReplicaSyncFailed` (security-relevant; audit-logged via `kernel-audit`).

#### New types — `Services/IEventCursorSync.cs`

```text
IEventCursorSync
├── // Pull direction: this replica asks a remote replica for everything after its cursor
├── Task<PullResult> PullFromAsync(ReplicaId source, long localCursor, CancellationToken ct = default)
│       // implementation: invokes kernel-sync.ISyncDaemonTransport.RequestAsync(...) with a "REPLICA_EVENT_PULL" message
│       // receives ReplicaEventBundle(s); verifies signature; calls ApplyBundleAsync internally
│
├── // Push direction: this replica sends new events to a remote replica
├── Task<PushResult> PushToAsync(ReplicaId target, long peerCursor, CancellationToken ct = default)
│       // implementation: reads from local IDomainEventStore.ReadAfterAsync(peerCursor) up to MaxEventsPerBundle
│       // constructs ReplicaEventBundle; signs; transmits via kernel-sync; awaits ack
│
├── // Receive direction: a remote replica pushed a bundle to us
├── Task<ApplyBundleResult> ApplyBundleAsync(ReplicaEventBundle bundle, CancellationToken ct = default)
│       // 1. Verify source replica is Trusted (via IReplicaRegistry); reject if not
│       // 2. Verify signature against source's PublicKey; reject if invalid
│       // 3. Verify bundle.Events.Count <= MaxEventsPerBundle
│       // 4. Verify all events.TenantId match expected tenant scope
│       // 5. For each event: call IDomainEventStore.AppendAsync(...) — idempotent via idempotency-key UNIQUE
│       // 6. For each CrdtStateVector + CrdtDelta: invoke ICrdtEngine.ApplyV1(...) for the corresponding entity
│       // 7. Advance per-replica cursor (IReplicaSyncCursorRepo.UpsertCursorAsync(source, bundle.ToCursor))
│       // 8. Return ApplyBundleResult with applied/skipped/rejected counts
│
└── // Cursor management
    Task<long> GetCursorForPeerAsync(ReplicaId peer, CancellationToken ct = default)
    Task SetCursorForPeerAsync(ReplicaId peer, long cursor, CancellationToken ct = default)

PullResult
├── Bundles: IReadOnlyList<ReplicaEventBundle>
├── TotalEvents: int
├── NewCursor: long
└── Error: PullError? (None, RemoteUnreachable, SignatureInvalid, BundleTooLarge, TrustMismatch)

PushResult
├── BundleId: ReplicaEventBundleId
├── EventsSent: int
├── NewPeerCursor: long
└── Error: PushError? (None, RemoteUnreachable, RemoteRejected, RateLimited, BundleTooLarge)

ApplyBundleResult
├── EventsApplied: int
├── EventsSkipped: int (idempotent dedup)
├── EventsRejected: int (with reasons)
├── CrdtStateUpdates: int
├── NewCursor: long
└── ReplyError: ApplyError? (None, UntrustedSource, SignatureInvalid, BundleTooLarge, TenantMismatch, InternalError)
```

#### New types — per-replica cursor table

```text
CREATE TABLE LocalFirstSync_ReplicaSyncCursor (
  PeerReplicaId   TEXT NOT NULL,
  TenantId        TEXT NOT NULL,
  Cursor          INTEGER NOT NULL,
  UpdatedAtUtc    TEXT NOT NULL,
  PRIMARY KEY (PeerReplicaId, TenantId)
);
```

Note: the cursor is **per (peer, tenant)** — one Anchor install can be paired with multiple tenants (multi-team workspace switching per ADR 0032); each tenant has its own event-stream scope.

#### Integration with `kernel-sync`

`blocks-localfirst-sync.SyncProtocolMessage` adds two new wire messages:

```text
REPLICA_EVENT_PULL_REQUEST  (sent by replica A to replica B; carries A's cursor for B's stream)
REPLICA_EVENT_PULL_RESPONSE (sent by replica B to replica A; carries one or more ReplicaEventBundles)
REPLICA_EVENT_PUSH          (sent by replica A to replica B; carries one bundle of new events)
REPLICA_EVENT_PUSH_ACK      (sent by replica B to replica A; confirms bundle ID + new cursor position)
```

These are added to `kernel-sync.Protocol.Messages.cs` as CBOR-codec'd records; they ride the existing `ISyncDaemonTransport` framing. The `IGossipDaemon` invokes `IEventCursorSync.PullFromAsync` and `PushToAsync` once per round per peer (PR 5 wires this).

**Security-council review point:** the message handler in `kernel-sync` must dispatch `REPLICA_EVENT_PUSH` to `IEventCursorSync.ApplyBundleAsync`, which performs the trust + signature + size checks BEFORE inserting into `IDomainEventStore`. The handler MUST NOT bypass the trust check on the grounds that "the message came from a peer the handshake verified" — the handshake authenticates the wire connection; the trust check authorizes the application-level operation.

#### DI extension changes

Extend `ServiceCollectionExtensions.AddBlocksLocalFirstSync` to register:
- `IEventCursorSync` → `EventCursorSync` (real implementation; depends on `IDomainEventStore`, `IReplicaRegistry`, `ICrdtEngine`, `ISyncDaemonTransport`, `IGossipDaemon`)
- `IReplicaSyncCursorRepo` → `SqliteReplicaSyncCursorRepo` or `InMemoryReplicaSyncCursorRepo` per `RegistrySqlitePath` option

#### Tests (PR 2)

`tests/ReplicaEventBundleTests.cs`:
- `Construction_AllFields_Preserves`.
- `Signature_DeterministicForSameInputs`.
- `Signature_DifferentForDifferentInputs`.
- `VerifySignature_ValidKey_ReturnsTrue`.
- `VerifySignature_InvalidKey_ReturnsFalse`.
- `BundleSize_AtMaxEventsPerBundle_Allowed`.
- `BundleSize_OverMaxEventsPerBundle_Rejected`.

`tests/EventCursorSyncPullTests.cs`:
- `PullFromAsync_TrustedSource_ReturnsBundles`.
- `PullFromAsync_UntrustedSource_ReturnsTrustMismatch`.
- `PullFromAsync_RemoteUnreachable_ReturnsRemoteUnreachable_RetryNextRound`.
- `PullFromAsync_AdvancesCursor_OnSuccess`.
- `PullFromAsync_PreservesCursor_OnFailure`.

`tests/EventCursorSyncPushTests.cs`:
- `PushToAsync_HappyPath_SendsBundle_GetsAck`.
- `PushToAsync_EmptyEventStream_PushesEmptyBundle_AndUpdatesCursor`.
- `PushToAsync_BundleSplitsAtMax_SendsMultipleBundles`.
- `PushToAsync_RemoteRejected_ReturnsRemoteRejected`.
- `PushToAsync_RateLimited_BackoffApplied` (PR 4 ships the actual backoff; this PR ships the rate-limit detection).

`tests/EventCursorSyncApplyTests.cs` (security-critical):
- `ApplyBundleAsync_TrustedSource_ValidSignature_Applies`.
- `ApplyBundleAsync_UntrustedSource_Rejects` (PendingTrust → reject; Quarantined → reject; Revoked → reject).
- `ApplyBundleAsync_InvalidSignature_Rejects`.
- `ApplyBundleAsync_BundleTooLarge_Rejects` (1001 events when max is 1000).
- `ApplyBundleAsync_TenantMismatch_Rejects` (bundle for tenant X delivered to a node listening for tenant Y).
- `ApplyBundleAsync_DuplicateEvent_IsSkipped_Idempotent` (same idempotency-key already in store → skipped, not duplicated).
- `ApplyBundleAsync_AdvancesCursor_OnSuccess`.
- `ApplyBundleAsync_CrdtStateVectors_AppliedViaICrdtEngine` (verify integration with kernel-crdt).
- `ApplyBundleAsync_PartialApplyOnError_AtomicAtBundleLevel` (if event 5 of 10 fails, bundle is rolled back; cursor not advanced).
- `ApplyBundleAsync_EmitsReplicaSyncCompleted_OnSuccess`.
- `ApplyBundleAsync_EmitsReplicaSyncFailed_OnSecurityRejection_AuditLogged`.

`tests/ReplicaSyncCursorRepoTests.cs`:
- `GetCursorForPeer_NewPeer_ReturnsZero`.
- `SetCursorForPeer_PersistsAndReturnsOnGet`.
- `Cursor_IsPerTenantPerPeer` (multi-tenant scoping).

`tests/KernelSyncIntegrationTests.cs`:
- `RoundTrip_TwoInMemoryDaemons_ReplicaA_PushesEvent_ReplicaB_Receives`.
- `RoundTrip_BothReplicasPushConcurrently_BothReceive_BothCursorsAdvance`.
- `RoundTrip_NetworkPartition_BothReplicas_RecoverOnReconnect` (PR 4 ships full resume; this test verifies the protocol can resume from the saved cursor).

Total new tests this PR: ~30.

#### Verification

- `dotnet build` succeeds.
- All PR 1 tests still pass.
- New tests pass.
- The `KernelSyncIntegrationTests.RoundTrip_*` tests run end-to-end with two in-memory replicas exchanging real domain events.
- `dotnet test` reports zero security warnings.

#### Do NOT in this PR

- Do NOT implement conflict resolution. PR 3 ships `ConflictResolutionPolicy`. For now, the apply-bundle logic relies on `IDomainEventStore.AppendAsync`'s idempotency-key UNIQUE constraint for dedup; if two replicas concurrently created an event with the **same idempotency key** (genuine conflict, not duplicate), the second one is silently skipped — that's a temporary placeholder that PR 3 replaces with proper conflict detection + reporting.
- Do NOT implement resumable sync with bounded retry. PR 4 ships that.
- Do NOT wire the background `SyncWorker`. PR 5 ships that.
- Do NOT add a PostgreSQL variant. v1 SQLite-only.

---

### PR 3 — `ConflictResolutionPolicy` + per-entity-class strategy + `IConflictResolutionPolicyRegistry`

**Estimated effort:** ~4–5h
**Scope:** the conflict resolution layer; per-entity-class strategy (CP last-writer-wins-with-emission; AP delegate-to-CRDT); registry of policies; conflict emission via `ReplicaSyncConflict` events.
**Commit subject:** `feat(blocks-localfirst-sync): per-entity-class conflict resolution policy per path-ii-crdt-schema-conventions §1`
**Depends on:** PR 2 merged.
**Branch:** `cob/blocks-localfirst-sync-conflict-policy`
**Council (MANDATORY):** security-engineering. Conflict resolution determines which replica's edit "wins" when concurrent writes occur — a wrong policy can silently drop user data or violate the CP/AP classification. Council reviews: (a) the per-entity-class policy table (CP vs AP), (b) the last-writer-wins tiebreaker (HLC + replica-id lex order), (c) the conflict-emission idempotency (a conflict reported once is not re-reported on re-sync), (d) the operator escalation path (UI surfaces conflicts; user resolves).

#### New types — `Models/ConflictResolutionPolicy.cs`

```text
ConflictResolutionPolicy (per entity type)
├── EntityType: string                                    // e.g., "JournalEntry", "WikiPage", "Invoice"
├── Class: EntityClass                                    // CP or AP
├── Strategy: ConflictResolutionStrategy
├── EmitOnConflict: bool                                  // default true for CP; default false for AP (CRDT merge is silent)
└── PreserveLoserAs: ConflictPreservationMode             // Discard | StoreAsSibling | StoreAsQuarantine

EntityClass = CP | AP

ConflictResolutionStrategy
├── CpLastWriterWinsWithEmission           // CP default; HLC tiebreaker; loser surfaces via ReplicaSyncConflict
├── CpRejectSecondWriter                   // CP for financial entities; reject the second writer; user must re-do
├── CpCoordinatorSemaphore                 // CP for once-only actions (PeriodClosed); first writer wins; second rejected
├── ApDelegateToCrdt                       // AP default; Loro/Yjs merge; deterministic; no conflict emission
└── ApLastWriterWinsByHlc                  // AP fallback when CRDT not available; scalar-only

ConflictPreservationMode
├── Discard                                // loser's value is dropped; no audit trail
├── StoreAsSibling                         // loser's value persists in a `conflict_siblings` table; UI can show
└── StoreAsQuarantine                      // loser's value persists in foundation-localfirst's quarantine queue
```

#### Per-entity-class policy table (defaults — registered in PR 6 by each cluster's DI extension)

| Entity type | Class | Strategy | EmitOnConflict | PreserveLoserAs |
|---|---|---|---|---|
| `JournalEntry`, `JournalLine` | CP | `CpRejectSecondWriter` | true | `StoreAsQuarantine` |
| `Invoice`, `InvoiceLine` | CP | `CpLastWriterWinsWithEmission` (terminal-status-wins per CRDT §7 Pattern B) | true | `StoreAsSibling` |
| `Bill`, `BillLine`, `Payment`, `PaymentApplication` | CP | `CpRejectSecondWriter` | true | `StoreAsQuarantine` |
| `FiscalPeriod.status` (open/closed) | CP | `CpCoordinatorSemaphore` | true | `StoreAsQuarantine` |
| `Contract`, `SigningWorkflow`, `Signature` | CP | `CpCoordinatorSemaphore` | true | `StoreAsQuarantine` |
| `Policy`, `PolicyVersion`, `PolicyAcknowledgment` | CP | `CpLastWriterWinsWithEmission` | true | `StoreAsSibling` |
| `WikiPage.markdownBody` | AP | `ApDelegateToCrdt` | false | n/a |
| `WikiPage.title`, `Document.tags[]`, `MarketingAsset.tags[]` | AP | `ApDelegateToCrdt` | false | n/a |
| `Lead.status`, `Opportunity` | AP | `ApLastWriterWinsByHlc` | false | n/a |
| `Employee.notes`, `MaintenanceTask.notes` | AP | `ApDelegateToCrdt` | false | n/a |
| `Party.addresses[]`, `Party.phoneNumbers[]` | AP | `ApDelegateToCrdt` (OR-Set) | false | n/a |
| `DashboardWidget.config`, `Dashboard.layout` | AP | `ApLastWriterWinsByHlc` | false | n/a |
| `Replica.lastSeenAtUtc`, `Replica.trustState` (this package's own AP fields) | AP | `ApLastWriterWinsByHlc` | false | n/a |

#### New types — `Services/IConflictResolutionPolicyRegistry.cs`

```text
IConflictResolutionPolicyRegistry
├── ConflictResolutionPolicy GetPolicyFor(string entityType)
├── ConflictResolutionPolicy GetPolicyFor<TEntity>()
├── void Register(ConflictResolutionPolicy policy)        // called by each cluster's DI extension
└── IReadOnlyDictionary<string, ConflictResolutionPolicy> ListAll()

DefaultConflictResolutionPolicyRegistry
├── (in-memory dict; default policy = CpLastWriterWinsWithEmission for unknown entity types — fail-safe)
```

#### New types — `Models/ReplicaSyncConflict.cs`

```text
ReplicaSyncConflict (CP-class; append-only audit record)
├── ConflictId: ReplicaSyncConflictId                    (ULID)
├── EntityType: string
├── EntityId: string                                     (FK to the conflicted entity row)
├── WinnerReplicaId: ReplicaId
├── LoserReplicaId: ReplicaId
├── WinnerVersion: long
├── LoserVersion: long
├── WinnerHlc: string                                    (HLC timestamp)
├── LoserHlc: string
├── ResolutionStrategy: ConflictResolutionStrategy        (which strategy applied)
├── LoserPreservation: ConflictPreservationMode           (Discard / StoreAsSibling / StoreAsQuarantine)
├── LoserSiblingId: string?                              (if StoreAsSibling — id of the sibling row)
├── DetectedAtUtc: Instant
├── DetectedByReplicaId: ReplicaId                       (which replica observed the conflict)
├── ResolvedAtUtc: Instant?                              (null until operator review; some conflicts auto-resolve)
├── ResolvedByPartyId: PartyId?
└── ResolutionNotes: string?                             (operator note)
```

SQLite table `LocalFirstSync_ReplicaSyncConflict` — append-only per `path-ii-crdt-schema-conventions.md` §1 CP-class.

#### Conflict detection algorithm (per-bundle apply)

```text
For each event in bundle:
  existingEvent = store.GetByIdempotencyKeyAsync(event.IdempotencyKey)
  if existingEvent is null:
    store.AppendAsync(event)                              // happy path
    continue

  if existingEvent.IsIdenticalTo(event):
    skip                                                  // truly duplicate (idempotent re-sync)
    continue

  // CONFLICT: same idempotency-key, different payload
  policy = policyRegistry.GetPolicyFor(event.EntityType)
  switch policy.Strategy:
    case CpLastWriterWinsWithEmission:
      winner, loser = ResolveByHlc(existingEvent, event, ReplicaIdTiebreaker)
      if winner == event:
        store.ReplaceAsync(existingEvent, event)
        preserveLoser(existingEvent, policy.PreserveLoserAs)
      else:
        preserveLoser(event, policy.PreserveLoserAs)
      emit(ReplicaSyncConflict { winner, loser, ... })

    case CpRejectSecondWriter:
      // first writer wins (i.e., the one already in the store)
      preserveLoser(event, policy.PreserveLoserAs)
      emit(ReplicaSyncConflict { winner: existingEvent, loser: event, ... })
      // for financial entities, the loser must re-issue with a new id

    case CpCoordinatorSemaphore:
      // first writer wins; second is hard-rejected; UI surfaces strongly
      preserveLoser(event, policy.PreserveLoserAs)
      emit(ReplicaSyncConflict { ... }, severity: High)

    case ApDelegateToCrdt:
      // bundle.CrdtDeltas carries the merge data; apply via ICrdtEngine
      crdtEngine.ApplyV1(event.EntityId, bundle.CrdtDeltas[event.EntityId])
      // no conflict event emitted (CRDT merge is silent)

    case ApLastWriterWinsByHlc:
      winner, loser = ResolveByHlc(existingEvent, event, ReplicaIdTiebreaker)
      store.ReplaceAsync(loser, winner)
      // no conflict event emitted (LWW is silent)
```

**Tiebreaker:** HLC (Hybrid Logical Clock) wall-clock + logical counter + replica-id-lex-order. The lex-order tiebreaker mirrors the existing `IInvoiceNumberingService.ResolveCollisionAsync` pattern from `blocks-financial-ar`.

#### Tests (PR 3)

`tests/ConflictResolutionPolicyTests.cs`:
- `DefaultPolicy_UnknownEntity_IsCpLastWriterWinsWithEmission`.
- `GetPolicyFor_RegisteredEntity_ReturnsRegisteredPolicy`.
- `GetPolicyFor_Generic_ResolvesTypeName`.
- `Register_DuplicateEntity_OverwritesPrevious_WithWarning`.

`tests/CpLastWriterWinsTests.cs`:
- `Conflict_NewerHlc_Wins`.
- `Conflict_OlderHlc_Loses`.
- `Conflict_EqualHlc_ReplicaIdTiebreaker` (lex order).
- `Conflict_LoserPreservedAsSibling_WhenPolicyRequires`.
- `Conflict_EmitsReplicaSyncConflictEvent`.

`tests/CpRejectSecondWriterTests.cs`:
- `Conflict_FirstWriterWins_SecondRejected`.
- `Conflict_LoserPreservedAsQuarantine`.
- `Conflict_EmitsReplicaSyncConflictEvent_Severity_High`.

`tests/CpCoordinatorSemaphoreTests.cs`:
- `Conflict_FirstWriterWins`.
- `Conflict_SecondWriterRejected_HardError`.
- `Conflict_EmitsReplicaSyncConflictEvent`.

`tests/ApDelegateToCrdtTests.cs`:
- `ApMerge_TwoConcurrentEdits_BothApplied_ViaCrdtEngine` (use real `kernel-crdt` `YDotNetCrdtEngine`).
- `ApMerge_NoConflictEventEmitted` (CRDT merge is silent).
- `ApMerge_DeterministicOutcome` (run merge 10× on same inputs → same output).

`tests/ApLastWriterWinsByHlcTests.cs`:
- `Conflict_NewerHlc_Wins_NoCrdt`.
- `Conflict_NoConflictEventEmitted` (AP-LWW is silent).

`tests/ConflictDetectionIntegrationTests.cs`:
- `TwoReplicas_ConcurrentInvoiceCreation_SameNumber_CpRejectSecondWriter_FirstWins` (financial; second must re-key).
- `TwoReplicas_ConcurrentWikiPageEdit_ApDelegateToCrdt_BothEditsMerge` (collaborative wiki).
- `TwoReplicas_ConcurrentPolicyAcknowledgment_CpLastWriterWinsWithEmission_SiblingPreserved`.
- `Idempotent_ResyncSameConflict_DoesNotReEmit` (conflict reported once).

Total new tests this PR: ~22.

#### Do NOT in this PR

- Do NOT implement resumable sync. PR 4.
- Do NOT wire the background worker. PR 5.
- Do NOT add UI components. Out of scope; ships in `blocks-localfirst-sync-ui` follow-on.

---

### PR 4 — `ResumableSync` — checkpoint + retry + bounded exponential backoff

**Estimated effort:** ~3–4h
**Scope:** the resumability layer; per-peer round checkpoint; bounded exponential backoff on transient failures; cursor confirmation protocol; crash-resume tests.
**Commit subject:** `feat(blocks-localfirst-sync): resumable sync with bounded retry + cursor checkpoint per Path II §6`
**Depends on:** PR 3 merged.
**Branch:** `cob/blocks-localfirst-sync-resumable`

#### New types — `Services/IResumableSync.cs`

```text
IResumableSync
├── Task<RoundResult> RunRoundAsync(ReplicaId peer, CancellationToken ct = default)
│       // 1. Read last successful round checkpoint (from ReplicaSyncRun table)
│       // 2. If a prior round was Interrupted, resume from its FromCursor
│       // 3. Pull from peer; apply bundles; checkpoint cursor each N events (configurable)
│       // 4. Push to peer; await ack; checkpoint cursor on ack
│       // 5. On transient failure: bounded exponential backoff; retry up to MaxRetries
│       // 6. On permanent failure: emit ReplicaSyncFailed + mark round Failed
│       // 7. On success: mark round Completed
│
├── Task<RoundResult> ResumeInterruptedRoundAsync(ReplicaSyncRunId runId, CancellationToken ct = default)
│       // recovery on startup: find any rounds in Running state from prior process
│       // mark them Cancelled (we can't actually resume in-flight); start a fresh round at the last checkpoint
│
└── Task<IReadOnlyList<ReplicaSyncRun>> ListRecentRunsAsync(ReplicaId peer, int limit = 50, CT = default)
```

#### New types — `Models/ReplicaSyncRun.cs`

```text
ReplicaSyncRun (CP-class; append-only history of every round)
├── RunId: ReplicaSyncRunId                              (ULID)
├── RunNumber: string                                    (RUN-YYYY-MM-DD-{LocalReplicaId}-{seq:D6})
├── PeerReplicaId: ReplicaId
├── TenantId: string
├── StartedAtUtc: Instant
├── EndedAtUtc: Instant?
├── Status: ReplicaSyncRunStatus                        (Running | Completed | Failed | Cancelled)
├── BundlesPulled: int
├── BundlesPushed: int
├── EventsApplied: int
├── EventsSkipped: int
├── ConflictsDetected: int
├── FromCursor: long                                     (where this round started reading from peer)
├── ToCursor: long                                       (where this round ended)
├── PeerFromCursor: long                                 (where peer started reading from us)
├── PeerToCursor: long                                   (where peer ended)
├── ErrorReason: string?                                 (when Status = Failed)
├── RetryCount: int                                      (how many transient retries happened during this round)
├── Version: long
└── (audit fields)

ReplicaSyncRunStatus = Running | Completed | Failed | Cancelled
```

#### Bounded exponential backoff

```text
Retry schedule (configurable):
├── Attempt 1: immediate
├── Attempt 2: 1s + jitter
├── Attempt 3: 4s + jitter
├── Attempt 4: 16s + jitter
├── Attempt 5: 64s + jitter
├── Attempt 6: 256s + jitter
└── After 6 attempts: permanent failure; mark round Failed; emit ReplicaSyncFailed

MaxBackoffSeconds: 300 (hard cap; per security-council guidance on DoS prevention)
Jitter: ±20% to avoid thundering-herd retry storms
```

Transient failures (retry): `RemoteUnreachable`, `RateLimited`, `RemoteTransientError`.
Permanent failures (no retry): `TrustMismatch`, `SignatureInvalid`, `TenantMismatch`, `BundleTooLarge`.

#### Cursor checkpoint protocol

Every N events applied (default 50), the cursor is persisted to `ReplicaSyncCursor`. A crash in the middle of a bundle does not lose more than N events' worth of cursor progress; on resume, the next round re-pulls the last (≤N) events and IDomainEventStore.AppendAsync's idempotency-key UNIQUE constraint silently skips them.

#### Tests (PR 4)

`tests/ResumableSyncTests.cs`:
- `RunRoundAsync_HappyPath_Completes`.
- `RunRoundAsync_TransientFailure_RetriesAndSucceeds`.
- `RunRoundAsync_PermanentFailure_MarksRunFailed_EmitsReplicaSyncFailed`.
- `RunRoundAsync_RetryBudget_Exhausted_MarksRunFailed`.
- `RunRoundAsync_BackoffSchedule_DoublesEachAttempt`.
- `RunRoundAsync_BackoffJitter_VariesAcrossRuns`.

`tests/CursorCheckpointTests.cs`:
- `CheckpointEveryNEvents_Persists`.
- `ResumeAfterCrash_ReplaysFromCheckpoint_NoDataLoss`.
- `ResumeAfterCrash_AppliedEventsAreIdempotent_NotDuplicated`.

`tests/RecoveryTests.cs`:
- `ResumeInterruptedRoundAsync_FindsRunningRoundsFromPriorProcess_MarksCancelled`.
- `ResumeInterruptedRoundAsync_StartsFreshRound_FromLastCheckpoint`.

Total new tests this PR: ~11.

#### Do NOT in this PR

- Do NOT wire the background worker. PR 5.

---

### PR 5 — `SyncWorker` background service + IPC integration

**Estimated effort:** ~3–4h
**Scope:** the `BackgroundService` that drives sync rounds; integration with `IGossipDaemon` round scheduler; `IReplicaSyncStatusReporter` for UI IPC.
**Commit subject:** `feat(blocks-localfirst-sync): background SyncWorker + IPC status reporter`
**Depends on:** PR 4 merged.
**Branch:** `cob/blocks-localfirst-sync-worker`

#### New types — `Services/SyncWorker.cs`

```text
SyncWorker : BackgroundService
├── // Triggered by IGossipDaemon's round scheduler (default 30s interval)
├── // For each Trusted replica in IReplicaRegistry: invoke IResumableSync.RunRoundAsync
├── // Cap concurrent rounds at MaxConcurrentRounds (default 3; configurable)
├── // Skip Quarantined + Revoked + Retired replicas
├── // Update IReplicaSyncStatusReporter throughout
└── // Honor cancellation (graceful shutdown)
```

#### New types — `Services/IReplicaSyncStatusReporter.cs`

```text
IReplicaSyncStatusReporter
├── ReplicaSyncStatusSnapshot GetSnapshot()              // single-call point-in-time snapshot for UI
├── IAsyncEnumerable<ReplicaSyncStatusEvent> Subscribe(CT)  // streaming updates for UI

ReplicaSyncStatusSnapshot
├── LocalReplicaId: ReplicaId
├── LocalReplicaDisplayName: string
├── TotalReplicas: int
├── TrustedReplicas: int
├── ActiveRounds: int
├── LastFullCycleCompletedAtUtc: Instant?
├── PendingConflicts: int                                 // unresolved ReplicaSyncConflict count
├── PerReplica: IReadOnlyList<ReplicaStatusSummary>
└── (and more...)

ReplicaStatusSummary
├── ReplicaId: ReplicaId
├── DisplayName: string
├── TrustState: ReplicaTrustState
├── LastSeenAtUtc: Instant?
├── LastSuccessfulRoundAtUtc: Instant?
├── CurrentlyRunning: bool
├── LastError: string?
└── RetryCount: int
```

#### IPC contract

`IReplicaSyncStatusReporter` is exposed via Tauri IPC (Anchor desktop) + WebSocket (Bridge UI) + named-pipe (legacy Anchor MAUI). Each transport's binding ships in the `blocks-localfirst-sync-ui` follow-on hand-off; this hand-off defines the contract.

#### Tests (PR 5)

`tests/SyncWorkerTests.cs`:
- `SyncWorker_StartsRound_OnGossipTick`.
- `SyncWorker_SkipsQuarantinedReplicas`.
- `SyncWorker_CapsConcurrentRounds`.
- `SyncWorker_HonorsCancellation_DuringRound`.
- `SyncWorker_HonorsCancellation_BetweenRounds`.
- `SyncWorker_ContinuesAfterIndividualRoundFails`.

`tests/ReplicaSyncStatusReporterTests.cs`:
- `GetSnapshot_ReflectsCurrentState`.
- `Subscribe_EmitsUpdates_OnRoundCompletion`.
- `Subscribe_EmitsUpdates_OnConflictDetected`.
- `Subscribe_HonorsCancellation`.

Total new tests this PR: ~10.

---

### PR 6 — DI umbrella + docs + ledger flip

**Estimated effort:** ~2–3h
**Scope:** consolidate DI extension; ship `apps/docs/blocks-localfirst-sync/overview.md`; ship lifecycle event records via `foundation-events`; ledger flip; coordination beacon.
**Commit subject:** `feat(blocks-localfirst-sync): DI umbrella + docs + ledger flip per ADR 0088 Path II`
**Depends on:** PR 5 merged.
**Branch:** `cob/blocks-localfirst-sync-docs-and-ledger`

#### DI consolidation

```text
public static IServiceCollection AddBlocksLocalFirstSync(
    this IServiceCollection services,
    Action<BlocksLocalFirstSyncOptions>? configure = null)
{
    // Registers everything from PRs 1-5 + the SyncWorker BackgroundService
    services.AddSingleton<IReplicaRegistry, ...>();
    services.AddSingleton<IDeviceFingerprinter, DefaultDeviceFingerprinter>();
    services.AddSingleton<IEventCursorSync, EventCursorSync>();
    services.AddSingleton<IReplicaSyncCursorRepo, ...>();
    services.AddSingleton<IConflictResolutionPolicyRegistry, DefaultConflictResolutionPolicyRegistry>();
    services.AddSingleton<IResumableSync, ResumableSync>();
    services.AddSingleton<IReplicaSyncStatusReporter, ReplicaSyncStatusReporter>();
    services.AddHostedService<SyncWorker>();
    return services;
}

public static IServiceCollection AddBlocksLocalFirstSyncConflictPolicy<TEntity>(
    this IServiceCollection services,
    ConflictResolutionPolicy policy)
{
    // each cluster's DI extension calls this to register its entity policies
}
```

#### Lifecycle events — wire to `foundation-events`

Replace the PR 1 stub event publisher with `foundation-events.IDomainEventPublisher`. Lifecycle events emitted via the canonical envelope:

```text
foundation-events.IDomainEventPublisher.PublishAsync(
    new ReplicaSyncStartedPayload(runId, peerReplicaId, fromCursor),
    eventType: "LocalFirstSync.ReplicaSyncStarted",
    sourceCluster: "blocks-localfirst-sync",
    tenantId: tenantId,
    idempotencyKey: $"replica-sync-started:{runId}",
    ct);
```

Per `path-ii-cross-cluster-event-bus.md` §1 naming convention and §2 envelope.

#### `apps/docs/blocks-localfirst-sync/overview.md`

Structure (sketch):

```markdown
# blocks-localfirst-sync

Multi-device replica reconciliation substrate for the Sunfish Anchor
local-first runtime (ADR 0088 Path II Phase 3).

## What it does

Keeps Anchor running on 2+ devices coherent without a central server.

- Enumerates the replicas in the user's multi-device coordination set.
- Drives event-cursor synchronization over the kernel-sync gossip transport.
- Resolves conflicts per entity class (CP last-writer-wins-with-emission; AP CRDT-merge).
- Survives interruption — cursor checkpoint + bounded exponential backoff.
- Emits sync lifecycle events for UI and other clusters to react.

## What it doesn't do

- Distributed transactions across replicas (each replica's local SQLite transaction is the atomic unit).
- Byzantine fault tolerance (replicas are mutually trusted within a coordination set; see kernel-audit + kernel-signatures for byzantine-resistance concerns).
- Cross-organization sync (see federation-* stack per ADR 0029).
- Real-time push (sync rounds are gossip-scheduled, default 30s interval).

## Replica model

(15 lines: ReplicaId + DeviceFingerprint + trust state lifecycle.)

## Conflict resolution policy table

(20 lines: per-entity-class strategy table; cite CRDT conventions §1 + path-ii-crdt-schema-conventions §1.)

## Operator runbook — conflict triage

(25 lines: how to find ReplicaSyncConflict events; how to review sibling preservation; how to manually resolve a CP-class conflict.)

## Quickstart

(15 lines: minimal example registering DI + pairing two replicas + observing convergence.)

## Algorithms

- Event-cursor pull-push protocol → link to Path II crdt-schema-conventions §6
- Bundle signing → link to security-engineering council ruling (PR 2)
- Bounded exponential backoff → link to ResumableSync.RetrySchedule
- Conflict resolution per entity class → link to path-ii-crdt-schema-conventions §1 + crdt-friendly-schema-conventions.md §7

## Related

- `kernel-sync` (gossip transport; this package composes it)
- `kernel-crdt` (CRDT engine; AP-class entities use it for state-vector exchange)
- `foundation-events` (canonical event store; this package reads + writes through it)
- `foundation-localfirst` (offline-first orchestration; outbound queue model — this package handles the sync side)
- `federation-*` (cross-organization sync; intentionally NOT subsumed — federation-* is intra-org; this is intra-team)
```

#### Tests (PR 6)

`tests/EndToEndSyncTests.cs`:
- `EndToEnd_TwoReplicas_PairBootstrap_SyncCompletes_BothConverge`.
- `EndToEnd_ThreeReplicas_AllPairwise_AllConverge`.
- `EndToEnd_OneReplicaOffline_OtherTwoSync_OfflineCatchesUpOnReturn`.
- `EndToEnd_LifecycleEvents_FlowThroughFoundationEvents` (verify ReplicaSyncStarted/Completed/Conflict events are visible via IDomainEventStore.ReadAfterAsync).
- `EndToEnd_DiClean_ServiceProvider_BuildsAndDisposes`.

Total new tests this PR: ~5.

---

## Cross-cluster integration

### Consumes (read)

- `foundation-events.IDomainEventStore` — reads events to bundle for outbound sync; writes received events on inbound sync (idempotent via UNIQUE idempotency-key constraint).
- `foundation-events.IDomainEventPublisher` — emits lifecycle events (PR 6 swap from PR 1 stub).
- `kernel-sync.IGossipDaemon` — round scheduler; this package's `SyncWorker` hooks into the gossip tick.
- `kernel-sync.ISyncDaemonTransport` — wire transport for bundle exchange (WebSocket-over-Tailscale by default).
- `kernel-sync.HandshakeProtocol` — public-key authentication; produces the `PublicKey` field stored in `IReplicaRegistry`.
- `kernel-crdt.ICrdtEngine` — CRDT-state-vector exchange for AP-class entities.
- `kernel-audit.IAuditWriter` (optional via `EnableAudit`) — security events (bundle signature failure, trust mismatch rejection) logged to the audit chain.

### Emits (via `foundation-events`)

| Event | Trigger | Payload | Idempotency key |
|---|---|---|---|
| `LocalFirstSync.ReplicaRegistered` | New replica added to `IReplicaRegistry` | `{ replicaId, deviceFingerprintHash, publicKeyHash, pairedAtUtc, pairedByPartyId }` | `replica-registered:{replicaId}` |
| `LocalFirstSync.ReplicaTrustChanged` | TrustState transition | `{ replicaId, fromTrustState, toTrustState, changedByPartyId, reason }` | `replica-trust-changed:{replicaId}:{toTrustState}:{changedAtUtc}` |
| `LocalFirstSync.ReplicaRetired` | Replica retired (tombstone) | `{ replicaId, reason, retiredByPartyId, retiredAtUtc }` | `replica-retired:{replicaId}` |
| `LocalFirstSync.ReplicaSyncStarted` | `SyncWorker` starts a round | `{ runId, peerReplicaId, fromCursor, startedAtUtc }` | `replica-sync-started:{runId}` |
| `LocalFirstSync.ReplicaSyncCompleted` | Round completes successfully | `{ runId, peerReplicaId, eventsApplied, eventsSkipped, conflictsDetected, durationMs }` | `replica-sync-completed:{runId}` |
| `LocalFirstSync.ReplicaSyncConflict` | Conflict detected (CP-class with EmitOnConflict=true) | `{ conflictId, entityType, entityId, winnerReplicaId, loserReplicaId, strategy, loserPreservation, loserSiblingId? }` | `replica-sync-conflict:{conflictId}` |
| `LocalFirstSync.ReplicaSyncFailed` | Round failed permanently | `{ runId, peerReplicaId, errorReason, retryCount }` | `replica-sync-failed:{runId}` |

### Consumed (by other clusters)

Other clusters subscribe to `LocalFirstSync.*` events through `foundation-events`'s `EventDispatcherHost`:

- **Anchor UI** subscribes to `ReplicaSync*` for the sync-status panel (badge counts, in-flight rounds, conflict surfacing).
- **`blocks-tenant-admin`** subscribes to `ReplicaRegistered` + `ReplicaTrustChanged` for the tenant-admin replica-management page.
- **`kernel-audit`** subscribes to `ReplicaSyncFailed` (signature-invalid subset) for the security audit chain.

### Dependency direction (no circular events)

```
foundation-events  ───────▶  blocks-localfirst-sync
kernel-sync         ───────▶  blocks-localfirst-sync
kernel-crdt         ───────▶  blocks-localfirst-sync
kernel-audit        ───────▶  blocks-localfirst-sync  (optional)
foundation-localfirst ─────▶  blocks-localfirst-sync  (composes the offline-first orchestration)

blocks-localfirst-sync  ───▶  foundation-events  (emits lifecycle events back through canonical store)
blocks-localfirst-sync  ───▶  (every other cluster subscribes via foundation-events dispatcher)
```

No circular dependencies. The substrate consumes the lower layers + emits back through the canonical event substrate.

---

## Pre-merge council requirements

**Council on PR 1: .NET architect (MANDATORY).**
- Reviews: `IReplicaRegistry` surface, `Replica` schema, `DeviceFingerprint` composition, DI extension overload pattern (audit-disabled + audit-enabled both-or-neither), SQLite migration scaffolding, namespace + folder convention.
- Output: Approve / Approve-with-amendments / Request-changes. Amendments must be applied before merge.

**Council on PR 2: security-engineering (MANDATORY).**
- Reviews: Bundle signature scheme (ed25519 over BundleId + From + To + Events-hash), bundle-size bound (DoS prevention), trust check before applying (must be Trusted; rejects Pending/Quarantined/Revoked), tenant-scope check, idempotency contract, audit-log integration on security rejections.
- Output: Approve / Approve-with-amendments / Request-changes. Amendments must be applied before merge. **Council must verify** the absence of these anti-patterns: (a) trusting a remote replica's claimed tenant-id without server-side verification, (b) accepting an oversize bundle, (c) silently swallowing signature failures.

**Council on PR 3: security-engineering (MANDATORY).**
- Reviews: Per-entity-class policy table (CP vs AP correctness), last-writer-wins tiebreaker (HLC + replica-id lex order — deterministic across replicas), conflict-emission idempotency (a conflict reported once is not re-reported on re-sync), loser preservation modes (Discard / StoreAsSibling / StoreAsQuarantine), operator escalation surface (UI surfaces conflicts).
- Output: Approve / Approve-with-amendments / Request-changes. **Council must verify** the absence of these anti-patterns: (a) AP-class strategy applied to a CP-class entity (silent data loss), (b) CP-class strategy applied to an AP-class entity (false-positive conflicts blocking collaboration), (c) loser dropped without emission for any CP-class entity.

**Councils on PR 4 + PR 5 + PR 6:** standard COB self-audit. No council required unless an implementation question surfaces (e.g., the backoff schedule's max cap turns out to interact badly with a downstream rate-limit; the IPC contract turns out to leak transport details). In that case, file `cob-question-*` and escalate to XO.

**Per `feedback_council_before_automerge` and `feedback_pr_automerge_before_amendment_landed`:**
- All councils MUST complete before auto-merge enables on the affected PR.
- The PR opens as `--draft`; flips to open only after all councils have approved and amendments are pushed.
- A 4th-late-council miss (per the W#1 PR #688 incident) is the failure mode to guard against — explicitly wait for all dispatched council subagents to return verdicts.

---

## Idempotency-key catalog

Every cross-cluster event handler must be idempotent on `EventId` per `path-ii-cross-cluster-event-bus.md` §9. The idempotency keys for this hand-off's events:

| Event | Key formula |
|---|---|
| `ReplicaRegistered` | `replica-registered:{replicaId}` |
| `ReplicaTrustChanged` | `replica-trust-changed:{replicaId}:{toTrustState}:{changedAtUtcIso}` |
| `ReplicaRetired` | `replica-retired:{replicaId}` |
| `ReplicaSyncStarted` | `replica-sync-started:{runId}` |
| `ReplicaSyncCompleted` | `replica-sync-completed:{runId}` |
| `ReplicaSyncConflict` | `replica-sync-conflict:{conflictId}` |
| `ReplicaSyncFailed` | `replica-sync-failed:{runId}` |

For sync-bundle dedup (the protocol-level idempotency, not event-level):

| Operation | Key formula |
|---|---|
| Bundle apply | `{tenantId}:{sourceReplicaId}:{bundleId}` — UNIQUE per-tenant-per-source-per-bundle; SQLite `replica_applied_bundles` table |
| Cursor advance | `{tenantId}:{peerReplicaId}:cursor` — UPSERT semantics; per-tenant-per-peer single row |

The protocol-level idempotency operates **below** the event-level idempotency: the bundle ack confirms "this bundle was applied"; the event-level idempotency ensures "this specific event was not duplicated even if the bundle is re-sent". Both layers are required (the bundle dedup is the fast path; the event dedup is the catch-all).

---

## Dependencies + sequence

### Direct deps (other Sunfish packages this consumes)

- `foundation-events` (sibling, shipped) — `IDomainEventStore`, `IDomainEventPublisher`, `EventDispatcherHost`, `DomainEventEnvelope`.
- `kernel-sync` (shipped) — `IGossipDaemon`, `ISyncDaemonTransport`, `HandshakeProtocol`, `IPeerDiscovery`, `VectorClock`.
- `kernel-crdt` (shipped) — `ICrdtEngine`, `ICrdtDocument`, `ICrdtMap`, `ICrdtText`, `ICrdtList`.
- `kernel-audit` (shipped; optional via `EnableAudit`) — `IAuditWriter`.
- `foundation-localfirst` (shipped, partial) — `IOfflineStore`, `IOutboundQueue`, `IConflictResolver` (composed; not subsumed).
- `foundation` (shipped) — `ISunfishDomainEvent`, `PartyId`, `Instant`, base types.

### Transitive deps (NuGet)

- `Microsoft.Data.Sqlite` (already in solution; per `foundation-events` dependency).
- `NodaTime` (already in solution; per `Instant` usage convention).
- `System.Security.Cryptography.Algorithms` (ed25519 verification; on .NET 11 this is built-in via `NSec.Cryptography` or System.Security.Cryptography).
- `Microsoft.Extensions.Hosting.Abstractions` (for `BackgroundService`).
- `Microsoft.Extensions.DependencyInjection.Abstractions`.
- `Microsoft.Extensions.Configuration.Binder` (for `IConfiguration` overload).
- No new third-party CRDT NuGet (uses `kernel-crdt.ICrdtEngine` which is already wired to YDotNet).

### Sequence

```
PR 1 (scaffold + registry)
   ↓
PR 2 (event-cursor protocol)         ◀── security council #1
   ↓
PR 3 (conflict resolution policy)    ◀── security council #2
   ↓
PR 4 (resumable + backoff)
   ↓
PR 5 (background worker + IPC)
   ↓
PR 6 (DI umbrella + docs + ledger)
```

All PRs sequential; no parallelization.

### Downstream consumers (gated on this hand-off's PASS gate)

- `blocks-localfirst-sync-ui` (follow-on hand-off) — Anchor sync-status panel; Blazor + React parity; consumes `IReplicaSyncStatusReporter` over IPC.
- `blocks-tenant-admin` (follow-on retrofit) — replica-management page; consumes `IReplicaRegistry` for the admin UI.
- `blocks-financial-ar` + `blocks-financial-ap` + `blocks-financial-payments` (existing) — register CP-class conflict policies for their entities via `AddBlocksLocalFirstSyncConflictPolicy<TEntity>(...)`.
- Every `blocks-*` cluster — registers per-entity conflict policy in its DI extension; subscribes to `ReplicaSyncConflict` for relevant entity types.
- `kernel-audit` — security-event subscriber (signature-failure subset).
- `apps/docs` site — surfaces the operator runbook for conflict triage.

---

## License posture

### Borrowed-with-attribution (permissive — MIT / Apache 2.0)

Per ADR 0088 §2 (License Posture) and the FOSS source classification gate per §3.

#### Studied (inspired-by; cited; no code borrowed)

- **Loro** (MIT; <https://github.com/loro-dev/loro>). Studied for: CRDT-state-vector exchange pattern, replica enumeration, sync-bundle composition. Loro is the canonical CRDT the Path II conventions name; this hand-off uses `kernel-crdt.ICrdtEngine` (currently YDotNet-backed; Loro-backed when LoroCs matures). Citation in NOTICE.md and in algorithm-bearing source-header comments where load-bearing.
- **Automerge** (MIT; <https://github.com/automerge/automerge>). Studied for: per-actor cursor model, conflict-as-first-class-data philosophy (Automerge's "concurrent changes are merged; conflicts are observable" model directly informed this hand-off's `ReplicaSyncConflict` design — losers are preserved, not silently dropped). Citation in NOTICE.md.
- **Yjs** (MIT; <https://github.com/yjs/yjs>). Studied for: engine-agnostic CRDT contract pattern. YDotNet (the .NET binding to yrs, the Rust port of Yjs) is the shipped backend in `kernel-crdt`. Citation in NOTICE.md.
- **Iroh** (Apache 2.0; <https://github.com/n0-computer/iroh>). Studied for: replica enumeration + multi-device pairing UX patterns (Iroh's "doc-author-pubkey + node-id" model informed this hand-off's `Replica.PublicKey + ReplicaId` shape). The hand-off does **not** vendor Iroh code — `kernel-sync` provides the transport. Citation in NOTICE.md.

#### Borrowed (with attribution; code-level)

None at the cluster level. All third-party CRDT/sync substrate is consumed via existing kernel-tier packages (`kernel-crdt`, `kernel-sync`).

### Clean-room only (copyleft — NEVER paste, NEVER vendor)

- **Tryton's sync layer** (AGPLv3) — explicitly excluded from study. Tryton's multi-database sync is a different model (server-orchestrated, not peer-to-peer) and the AGPL posture is incompatible with Sunfish's MIT output per ADR 0088 §2.
- **Frappe's eventstream / sync** (GPLv3) — explicitly excluded; ERPNext's sync model is server-bound (Frappe framework's "doctype watchdog") and the GPL posture is incompatible.

**Discipline check before merging any PR in this hand-off:**

1. No copyleft code was opened in any editor session that produced this hand-off's PRs.
2. No identifier names from any GPL/AGPL sync source appear in the new code. (Spot-check by grep before merge.)
3. The clean-room schema in this hand-off + `path-ii-crdt-schema-conventions.md` is the source of truth; deviations require XO ratification.
4. The CRDT lib choice is **YDotNet** (Yjs) via `kernel-crdt`; the substrate uses the engine-agnostic contract. **Loro is the eventual canonical** when LoroCs surface matures; the swap is a DI-only change with no public-surface impact.

### Attribution requirements

1. The package's `Sunfish.Blocks.LocalFirst.Sync.csproj` carries `<PropertyGroup><NOTICEFile>NOTICE.md</NOTICEFile></PropertyGroup>`.
2. **`packages/blocks-localfirst-sync/NOTICE.md`** (new file in PR 1):

```markdown
# NOTICE — Sunfish.Blocks.LocalFirst.Sync

This package's design draws on the following permissively-licensed FOSS
projects, studied for understanding and cited as inspiration. No code from
these projects is reproduced; the Sunfish implementation is original code,
distributed under the MIT License.

## Inspired-by (MIT / Apache 2.0)

### Loro (MIT) — https://github.com/loro-dev/loro

Loro is a Rust-core CRDT library used (via `kernel-crdt`) for AP-class
entity state-vector exchange. The conflict-as-deterministic-merge pattern
in this package's AP-class policy strategy derives from Loro's CRDT model.
Loro is the eventual canonical CRDT for the Sunfish Path II runtime; the
current shipped backend is YDotNet (Yjs); the swap is a DI-only change.

### Automerge (MIT) — https://github.com/automerge/automerge

Automerge's "conflicts are first-class observable data; losers are not
silently dropped" philosophy directly informs this package's
ReplicaSyncConflict design. Studied for the per-actor cursor model.

### Yjs (MIT) — https://github.com/yjs/yjs

Yjs (via its Rust port yrs and the .NET binding YDotNet, shipped in
kernel-crdt) is the engine-agnostic CRDT primitive used for AP-class
collaborative editing. Studied for the engine-agnostic contract pattern.

### Iroh (Apache 2.0) — https://github.com/n0-computer/iroh

Iroh's "doc-author-pubkey + node-id" replica model informed this
package's `Replica.PublicKey + ReplicaId` schema. Iroh code is not
vendored; the transport layer is provided by kernel-sync.

## Clean-room implementation discipline

This package was authored without opening any AGPL or GPL source for any
sync-substrate-related code. Per ADR 0088 §3 clean-room implementation
discipline: copyleft sources (e.g., Tryton's sync layer; Frappe's
eventstream) are read-only-for-understanding from publicly available
documentation only; never opened in an editor while authoring this
package.
```

### Sunfish output

**All code authored under this hand-off is MIT-licensed**, per ADR 0088 §2 and the project-wide license posture.

---

## Test plan

### Per-PR minima (summary; details under each PR above)

| PR | Min tests | Coverage |
|---|---|---|
| PR 1 (scaffold + registry) | ~50 | ReplicaId + DeviceFingerprint + Replica + IReplicaRegistry + SQLite + DI |
| PR 2 (event-cursor protocol) | ~30 | bundle envelope + pull/push/apply + signature + idempotency + transport integration |
| PR 3 (conflict resolution) | ~22 | per-strategy paths + tiebreaker + emission + sibling preservation + idempotent re-conflict |
| PR 4 (resumable + backoff) | ~11 | checkpoint + retry + backoff schedule + crash recovery |
| PR 5 (worker + IPC) | ~10 | worker lifecycle + status reporter |
| PR 6 (DI umbrella + e2e) | ~5 | end-to-end 2-replica + 3-replica + offline-catch-up + lifecycle event flow |
| **Total** | **~128 new tests** | |

### Cluster-level acceptance (PASS gate at end of PR 6)

**Convergence invariant (foundational):**

> Given N replicas that all received the same event log, all N replicas produce the same state.

**A1.** `dotnet build` succeeds across the new `Sunfish.Blocks.LocalFirst.Sync` package + every downstream consumer that has registered a conflict policy via `AddBlocksLocalFirstSyncConflictPolicy`.

**A2.** `dotnet test packages/blocks-localfirst-sync/tests/` passes all ~128 new tests. `dotnet test` across all other packages reports zero regressions.

**A3. — Two-replica convergence (CP).** Two `InMemoryReplicaRegistry`-backed Anchor instances (replicas `CW` and `A4`) are paired. Replica `CW` creates 5 `JournalEntry` events via `blocks-financial-ledger`; replica `A4` creates 3 different `JournalEntry` events. After one round of sync between them:
- Both replicas' `IDomainEventStore` contain all 8 events.
- Both replicas' `IJournalEntryRepository.ListAll()` return the same 8 entries in the same order (by `EntryDate` then `EventId`).
- Both replicas' computed trial-balance sums are identical to the penny.
- `ReplicaSyncCompleted` event is observed on both replicas.

**A4. — Two-replica convergence (AP).** Two replicas concurrently edit the same `WikiPage.markdownBody`. Replica `CW` inserts "Hello, " at position 0; replica `A4` inserts "world!" at position 0. After one round of sync:
- Both replicas' wiki page body is identical (CRDT-merged).
- The merge result is deterministic across runs (re-run 10× → same output).
- No `ReplicaSyncConflict` event is emitted (AP-CRDT merge is silent).

**A5. — CP conflict detection.** Two replicas concurrently issue an `Invoice` for the same customer with overlapping `InvoiceNumber` (per-replica-suffix scheme normally prevents this; force the collision for the test). With policy `CpRejectSecondWriter`:
- The first writer (by HLC) wins; the second's invoice is preserved in the quarantine queue.
- `ReplicaSyncConflict` event is emitted exactly once, with the correct winner/loser/strategy fields.
- Re-syncing the same bundles a second time produces no additional `ReplicaSyncConflict` (idempotent).

**A6. — Idempotent re-sync.** A successful sync round is repeated immediately (without new events). Result:
- `ReplicaSyncCompleted` event is emitted again (each round is its own audit row).
- `EventsApplied == 0`, `EventsSkipped == <count from prior round>`.
- No state changes; cursor is unchanged.

**A7. — Resume after crash.** Mid-bundle application, the process is killed. On restart:
- The interrupted `ReplicaSyncRun` is marked `Cancelled`.
- A fresh round is initiated from the last checkpoint.
- All events up to the kill point are present (because checkpoint was set per N events); events between checkpoint and kill are re-pulled and silently deduped via `IDomainEventStore.AppendAsync`'s idempotency-key UNIQUE.
- No data loss; no data duplication.

**A8. — Bounded backoff.** A peer is made unreachable. `SyncWorker` retries with the bounded exponential backoff schedule (1s, 4s, 16s, 64s, 256s + jitter). After 6 attempts:
- The round is marked `Failed`; `ReplicaSyncFailed` is emitted.
- The next gossip tick (30s after the failure) starts a fresh round (the failed round is not retried in-cycle).

**A9. — Security: untrusted source rejection.** A replica with `TrustState == Quarantined` attempts to push a bundle. Result:
- `ApplyBundleAsync` returns `ApplyError.UntrustedSource`.
- `ReplicaSyncFailed` is emitted with reason "UntrustedSource".
- `kernel-audit.IAuditWriter` records the rejection (if `EnableAudit == true`).
- No events are applied; no cursor is advanced.

**A10. — Security: signature-invalid rejection.** A bundle with a tampered signature is received. Result:
- `ApplyBundleAsync` returns `ApplyError.SignatureInvalid`.
- `ReplicaSyncFailed` is emitted with reason "SignatureInvalid".
- `kernel-audit.IAuditWriter` records the security event.
- No events are applied.

**A11. — Performance.** A 2-replica round with 1,000 events per side completes end-to-end in < 5 seconds locally (CI tolerance; Surface Pro 7 target of < 10 seconds is the Phase 3 close-out acceptance, not this hand-off's).

**A12. — Three-replica convergence.** Three replicas (`CW`, `A4`, `B7`) all paired pairwise. Each issues 10 events independently. After all pairs have synced once:
- All three replicas hold all 30 events.
- All three trial-balance computations match.
- Total `ReplicaSyncCompleted` events: 6 (2 per pair).
- Zero conflicts (events are independent — different invoice numbers per replica).

---

## Halt conditions (cob-question-* beacons)

If COB hits any of these, halt the workstream + drop a `cob-question-*` beacon to `coordination/inbox/`:

### 1. CRDT lib choice not yet ratified

**Status: BLOCKING for PR 1.** Per §Open question — CRDT library choice. If XO/CO has not returned a ruling on "YDotNet via kernel-crdt default; future Loro swap is DI-only" vs "custom event-merge", **STOP** and file `cob-question-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-crdt-choice.md`. Do NOT start PR 1 without this ratification.

### 2. Transport choice not yet ratified

**Status: BLOCKING for PR 2.** Per §Open question — transport choice. If XO/CO has not returned a ruling on "multi-transport via abstraction, Tailscale default" vs "Tailscale-only", **STOP** and file `cob-question-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-transport-choice.md`. Do NOT start PR 2 without this ratification.

### 3. `foundation-events` not built

If `packages/foundation-events/` does not exist or doesn't carry `IDomainEventStore` + `IDomainEventPublisher` + `EventDispatcherHost`, **STOP** — the sibling event-bus hand-off must land first. Drop a `cob-question-*` beacon.

### 4. `kernel-sync` Wave 2.5 round-loop missing

Per `kernel-sync/README.md`, Wave 2.5 ("Round-loop wiring into `ICrdtDocument` for actual delta application on receipt") is deferred. This hand-off's PR 2 + PR 5 may surface a dependency on the round-loop wiring being live. **Mitigation:** PR 2 implements its own dispatcher on top of `ISyncDaemonTransport` framing — does not require Wave 2.5. **No halt unless** PR 2 surfaces an unexpected dependency.

### 5. `kernel-crdt` Loro backend lands mid-flight

Unlikely, but: if Loro backend lands in `kernel-crdt` before this hand-off finishes, the substrate inherits it via DI swap (no code change). No halt; flag in the cluster's apps/docs page that the backend is now Loro by default for new installs.

### 6. `ReplicaId` placeholder relocation surfaces breaking change

PR 1 ships the canonical `ReplicaId`. Existing local placeholders in `blocks-financial-ar`, `blocks-financial-ap`, etc., should be retrofitted via a separate sweep PR (after PR 6). If the retrofit surfaces a breaking change (e.g., a consumer depends on a behavior of the local placeholder that the canonical type doesn't preserve), **halt the sweep PR** + file `cob-question-*`. **Do not block this hand-off's PRs 1-6 on the retrofit sweep.**

### 7. Bundle-size bound debate

PR 2's security council may suggest tighter or looser bounds than `MaxEventsPerBundle = 1000`. **Council guidance is binding.** Document the chosen bound in the apps/docs operator runbook.

### 8. Conflict-policy table debate

PR 3's security council may dispute one or more entries in the per-entity-class policy table (e.g., council asserts `Invoice` should be `CpRejectSecondWriter` not `CpLastWriterWinsWithEmission`). **Council guidance is binding** for security-relevant policies (financial entities, contracts); **escalate to XO/CO** for non-security-relevant policies (wiki, tags, dashboard layout) if the council recommendation deviates from the table.

### 9. IPC contract leaks transport details

PR 5's `IReplicaSyncStatusReporter` is supposed to be transport-agnostic. If the implementation surface accidentally leaks Tauri-IPC-specific or WebSocket-specific types (e.g., `TauriCommand` attributes on the interface), halt + refactor. The contract is consumed by the follow-on UI hand-off and must remain transport-neutral.

### 10. `EndToEnd_LifecycleEvents` test fails

If `EndToEnd_LifecycleEvents_FlowThroughFoundationEvents` (PR 6) fails because lifecycle events aren't visible in `IDomainEventStore.ReadAfterAsync`, that means PR 6's event-publisher wiring is wrong. **Halt** + verify the swap from PR 1's stub publisher to `foundation-events.IDomainEventPublisher` is complete.

### 11. SQLite migration ordering with `foundation-events` migrations

`foundation-events` ships a `domain_events` table + `event_handler_cursors` table; this package ships `replica_registry` + `replica_sync_cursor` + `replica_sync_run` + `replica_sync_conflict`. The migration runner must apply both packages' migrations in dependency order. If a migration-ordering bug surfaces (e.g., `replica_sync_cursor` references `domain_events` via FK before `domain_events` exists), **halt** + file `cob-question-*` for a council review of migration framework.

**XO recommendation:** v1 does NOT use cross-package FK constraints (each package's tables are self-contained; cross-references are by string id at the application level). If a FK constraint sneaks in, that's a design slip — back it out.

### 12. Replica re-pairing semantics

If during PR 1 + PR 6 testing, a question surfaces about "what happens when a Revoked replica re-pairs?" or "can a Retired replica reactivate?", the answer is:

- **Revoked** is terminal. A revoked replica re-pairs as a NEW replica (new `ReplicaId`, new `DeviceFingerprint` if reasonable, new entry in `IReplicaRegistry`). The old Revoked entry persists as audit.
- **Retired** is a tombstone; the row is preserved but `ListActiveAsync` excludes it. A retired replica re-pairs as a NEW replica with a new `ReplicaId`.

**No halt; document in apps/docs operator runbook.**

### 13. Loro vs custom-event-merge — late reversal

If XO/CO ratified "YDotNet via kernel-crdt" but during PR 3 implementation it surfaces that the AP-class CRDT-merge is too tightly coupled to YDotNet specifics (defeating the engine-agnostic contract), **halt** + file `cob-question-*`. The fallback is the `ApLastWriterWinsByHlc` strategy (scalar-only AP merge); CRDT-merge for text/set fields would then be deferred to a follow-on.

---

## PASS gate (end-state for declaring this hand-off `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1–6 merged to main** sequentially.
2. **Convergence invariant verified:** acceptance tests A3 + A4 + A12 pass.
3. **CP conflict detection works:** acceptance test A5 passes.
4. **Idempotent re-sync works:** acceptance test A6 passes.
5. **Crash recovery works:** acceptance test A7 passes.
6. **Bounded backoff works:** acceptance test A8 passes.
7. **Security: untrusted-source rejection works:** acceptance test A9 passes; audit-logged on the security path.
8. **Security: signature-invalid rejection works:** acceptance test A10 passes; audit-logged.
9. **Performance:** acceptance test A11 passes (< 5s local; Phase 3 close-out target of < 10s on Surface Pro 7 deferred).
10. **All councils completed and amendments applied:** PR 1 (.NET architect), PR 2 (security-engineering), PR 3 (security-engineering).
11. **Tests pass:** ~128 new tests across the package; zero regression across the rest of the solution.
12. **`apps/docs/blocks-localfirst-sync/overview.md` published** (ships in PR 6).
13. **`active-workstreams.md`** row updated with `built` status + the 6 PR numbers (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
14. **`coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-built.md`** beacon dropped.

When the PASS gate is met, the next Phase 3 substrate hand-offs can proceed:

- `blocks-localfirst-sync-ui-stage06-handoff.md` (Anchor sync-status panel; Blazor + React parity; consumes `IReplicaSyncStatusReporter` over IPC).
- `blocks-tenant-admin-replica-management-stage06-addendum.md` (replica-management page in tenant-admin; consumes `IReplicaRegistry`).
- `blocks-financial-*-conflict-policy-registration-stage06-addendum.md` (sweep PR registering CP-class conflict policies for every financial entity).
- Eventual `blocks-localfirst-sync-selective-stage06-handoff.md` (Phase 4 follow-on; selective replication; per-cluster opt-out).
- Eventual `blocks-localfirst-sync-postgres-stage06-handoff.md` (Phase 4 follow-on; PostgreSQL variant for Bridge Standard/Hosted tier).

---

## Docs

**`apps/docs/blocks-localfirst-sync/overview.md`** — cluster docs page (ships in PR 6). Cite ADR 0088 §1 + §4 + Consequences; cite `path-ii-crdt-schema-conventions.md` §1 + §6 + §7; cite `path-ii-cross-cluster-event-bus.md` §9; cite `kernel-sync/README.md`; cite `kernel-crdt/README.md`; cite Loro/Automerge/Yjs/Iroh attribution from NOTICE.md.

Structure (sketch):

```markdown
# blocks-localfirst-sync

Multi-device replica reconciliation substrate for the Sunfish Anchor
local-first runtime.

## Overview

This package is the canonical multi-device sync substrate per ADR 0088
Path II Phase 3. It composes `kernel-sync` (gossip transport), `kernel-crdt`
(CRDT engine), and `foundation-events` (canonical event store) into a
multi-device coordination layer.

A "multi-device" coordination set is a small group of Anchor installs
(typically 2-5; one per device per user) that converge on the same domain
state without a central server. The substrate handles replica enumeration,
event-cursor synchronization, conflict resolution per entity class, and
sync lifecycle event emission.

## Quickstart

(15 lines: register DI; verify SyncWorker starts; observe `IReplicaSyncStatusReporter.GetSnapshot()`.)

## Replica model

(20 lines: ReplicaId + DeviceFingerprint + Replica + trust state lifecycle.)

## Conflict resolution policy table

(25 lines: per-entity-class strategy table from PR 3.)

## Operator runbook — conflict triage

(35 lines: how to find unresolved ReplicaSyncConflict events; how to review
sibling preservation; how to manually resolve a CP-class conflict;
how to retire / quarantine / revoke a replica.)

## Algorithms

- Event-cursor pull-push protocol → link to path-ii-crdt-schema-conventions §6
- Bundle signing → ed25519 over BundleId + From + To + Events-hash
- Conflict resolution per entity class → link to path-ii-crdt-schema-conventions §1 + crdt-friendly-schema-conventions §7
- Bounded exponential backoff → 1s/4s/16s/64s/256s + jitter; 6 retries; 300s cap
- Cursor checkpoint → every N events (default 50)

## Security model

- Replicas authenticate via `kernel-sync` handshake (ed25519).
- Bundles signed by source replica's `kernel-sync` private key.
- Trust check before applying any bundle (Pending/Quarantined/Revoked rejected).
- Bundle-size bound (default 1000 events) prevents DoS.
- Audit-logged security rejections via `kernel-audit` (when `EnableAudit == true`).

## What this does NOT do

- Distributed transactions across replicas.
- Byzantine fault tolerance.
- Cross-organization sync (see `federation-*` stack).
- Real-time push (sync rounds are 30s gossip-scheduled).
- Encryption-at-rest within the replica registry (operator threat-model concern).
- Selective sync / sparse replication (v1 replicates everything; follow-on hand-off).

## Related

- `kernel-sync` (gossip transport; composed)
- `kernel-crdt` (CRDT engine; composed for AP-class entities)
- `foundation-events` (canonical event store; consumed for read + write)
- `foundation-localfirst` (offline-first orchestration; this package handles the multi-device side)
- `federation-*` (intra-org sync via managed-relay; explicitly distinct from this intra-team substrate)
```

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `packages/foundation-events/` (predecessor; assumed shipped per the sibling hand-off — verify pre-build checklist step 2). Symbols: `IDomainEventStore`, `IDomainEventPublisher`, `EventDispatcherHost`, `SqliteDomainEventStore`, `DomainEventEnvelope`.
- `packages/kernel-sync/Gossip/IGossipDaemon.cs` ✓
- `packages/kernel-sync/Protocol/ISyncDaemonTransport.cs` ✓
- `packages/kernel-sync/Protocol/Messages.cs` ✓
- `packages/kernel-sync/Handshake/HandshakeProtocol.cs` ✓
- `packages/kernel-sync/Discovery/IPeerDiscovery.cs` ✓
- `packages/kernel-sync/Discovery/MdnsPeerDiscovery.cs` ✓
- `packages/kernel-sync/Gossip/VectorClock.cs` ✓
- `packages/kernel-crdt/ICrdtEngine.cs` ✓
- `packages/kernel-crdt/ICrdtDocument.cs` ✓
- `packages/kernel-crdt/Backends/YDotNetCrdtEngine.cs` ✓
- `packages/kernel-crdt/SPIKE-OUTCOME.md` ✓
- `packages/kernel-audit/` ✓ (for optional EnableAudit wiring)
- `packages/foundation-localfirst/` ✓ (partial — composed; not subsumed)
- ADR 0088 ✓
- `icm/02_architecture/path-ii-crdt-schema-conventions.md` ✓
- `icm/02_architecture/path-ii-cross-cluster-event-bus.md` ✓
- `_shared/engineering/crdt-friendly-schema-conventions.md` ✓
- `_shared/engineering/cross-cluster-event-bus-design.md` ✓
- `_shared/product/local-node-architecture-paper.md` ✓
- Precedent hand-off: `icm/_state/handoffs/foundation-events-stage06-handoff.md` ✓
- Precedent hand-off: `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md` ✓ (DI extension overload pattern reference)

**Introduced by this hand-off** (ships across PRs 1–6):

- New package: `packages/blocks-localfirst-sync/`
- New types: `ReplicaId` (canonical), `DeviceFingerprint`, `Replica`, `ReplicaTrustState`, `ReplicaSyncRunId`, `ReplicaSyncConflictId`, `ReplicaEventBundleId`, `ReplicaEventBundle`, `ReplicaSyncRun`, `ReplicaSyncRunStatus`, `ReplicaSyncConflict`, `ConflictResolutionPolicy`, `EntityClass`, `ConflictResolutionStrategy`, `ConflictPreservationMode`, `PullResult`, `PullError`, `PushResult`, `PushError`, `ApplyBundleResult`, `ApplyError`, `RoundResult`, `ReplicaSyncStatusSnapshot`, `ReplicaStatusSummary`, `ReplicaSyncStatusEvent`, `BlocksLocalFirstSyncOptions`, lifecycle event payloads (`ReplicaRegisteredPayload`, `ReplicaTrustChangedPayload`, `ReplicaRetiredPayload`, `ReplicaSyncStartedPayload`, `ReplicaSyncCompletedPayload`, `ReplicaSyncConflictPayload`, `ReplicaSyncFailedPayload`).
- New services: `IReplicaRegistry` + `InMemoryReplicaRegistry` + `SqliteReplicaRegistry`, `IDeviceFingerprinter` + `DefaultDeviceFingerprinter`, `IEventCursorSync` + `EventCursorSync`, `IReplicaSyncCursorRepo` + `InMemoryReplicaSyncCursorRepo` + `SqliteReplicaSyncCursorRepo`, `IConflictResolutionPolicyRegistry` + `DefaultConflictResolutionPolicyRegistry`, `IResumableSync` + `ResumableSync`, `IReplicaSyncStatusReporter` + `ReplicaSyncStatusReporter`, `SyncWorker` (BackgroundService).
- New SQLite tables: `LocalFirstSync_ReplicaRegistry`, `LocalFirstSync_ReplicaSyncCursor`, `LocalFirstSync_ReplicaSyncRun`, `LocalFirstSync_ReplicaSyncConflict`, `LocalFirstSync_ReplicaAppliedBundles`.
- New `kernel-sync` wire messages: `REPLICA_EVENT_PULL_REQUEST`, `REPLICA_EVENT_PULL_RESPONSE`, `REPLICA_EVENT_PUSH`, `REPLICA_EVENT_PUSH_ACK` (added to `Protocol/Messages.cs`).
- Docs: `apps/docs/blocks-localfirst-sync/overview.md`.
- Attribution: `packages/blocks-localfirst-sync/NOTICE.md`.

**Self-audit reminder (per ADR 0028-A10):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Do not rely on grep-only verification. Per `feedback_council_can_miss_spot_check_negative_existence`: spot-check negative existence too (verify that no other `blocks-localfirst-*` package exists before shipping; verify that `foundation-events` actually carries `IDomainEventStore`, not a similarly-named type from a different package).

---

## Cohort discipline

This hand-off is the **first Phase 3 substrate implementation hand-off under ADR 0088 Path II** (after the Phase 1 + Phase 2 cluster hand-offs). The COB self-audit pattern applied to W#34 / W#35 / W#36 / W#39 / W#40 + the financial-ledger + financial-ar hand-offs applies here verbatim:

- **Two-overload constructor (audit-disabled / audit-enabled both-or-neither) pattern** for the DI extension. **Required** in this hand-off (audit interaction is mandatory for security-relevant rejections — bundle signature-invalid, untrusted source).
- **`AddBlocksLocalFirstSync()` naming for the DI extension** — matches the cluster convention.
- **`apps/docs/blocks-localfirst-sync/overview.md` page convention** — ships in PR 6.
- **README.md at the package root** referencing Stage 02 design + ADR 0088 — ship in PR 1.
- **`ConcurrentDictionary` dedup for any in-memory cache** — applied in `InMemoryReplicaRegistry`, `InMemoryReplicaSyncCursorRepo`, `DefaultConflictResolutionPolicyRegistry`.
- **Strong-typed Id records** (ULID-backed) — applied for `ReplicaSyncRunId`, `ReplicaSyncConflictId`, `ReplicaEventBundleId`.
- **Stub interfaces for cross-cluster contracts not yet shipped** — not applicable in this hand-off (all dependencies are shipped substrates).
- **Pattern-001 (scaffold)** — applied: standard `Models/` + `Services/` + `Sqlite/` + `DependencyInjection/` + `tests/` folder layout per the convention.
- **Pattern-005 (DI)** — applied: standard `AddBlocksLocalFirstSync(Action<Options>?)` + `AddBlocksLocalFirstSync(IConfiguration, string?)` overload pair per the convention.

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-{slug}.md` in `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + add a note in the `active-workstreams.md` row for W#60.
- `ScheduleWakeup 1800s`.

If COB completes PR 6 + the PASS gate is met:

- Update `active-workstreams.md` (via the source W*.md file, not the ledger directly — per `feedback_never_add_workstream_rows_directly_to_ledger`).
- Drop `cob-status-2026-05-XXTHH-MMZ-w60-p4-localfirst-sync-built.md` to inbox.
- Continue with the next hand-off in the Phase 3 substrate queue (likely `blocks-localfirst-sync-ui-stage06-handoff.md` or `blocks-tenant-admin-replica-management-stage06-addendum.md` — whichever XO has dropped next).

---

## Cross-references

- ADR 0088: `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- Foundational paper: `_shared/product/local-node-architecture-paper.md` §2.2, §6.1, §6.2, §9.
- CRDT schema conventions: `icm/02_architecture/path-ii-crdt-schema-conventions.md` §1, §2, §3, §6, §7.
- Cross-cluster event bus design: `icm/02_architecture/path-ii-cross-cluster-event-bus.md` §1, §2, §9.
- Older sibling: `_shared/engineering/crdt-friendly-schema-conventions.md` §1, §3, §7, §10.
- Older sibling: `_shared/engineering/cross-cluster-event-bus-design.md` §1, §2, §3, §10.
- Predecessor hand-off: `icm/_state/handoffs/foundation-events-stage06-handoff.md` (the canonical event substrate this package consumes).
- Sibling hand-off (precedent for DI + scaffold patterns): `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md`.
- Cohort precedent hand-offs (substrate-only shape):
  - `foundation-mission-space-stage06-handoff.md` (W#40 — 5-PR substrate shape).
  - `foundation-versioning-stage06-handoff.md` (W#34 — substrate naming pattern).
  - `foundation-events-stage06-handoff.md` (the immediate canonical-event-bus precedent).
- Existing substrate packages composed:
  - `packages/kernel-sync/README.md` (transport layer).
  - `packages/kernel-crdt/README.md` (CRDT engine; YDotNet default; Loro deferred).
  - `packages/kernel-audit/` (optional audit-write).
  - `packages/foundation-localfirst/README.md` (offline-first orchestration; composed, not subsumed).
- FOSS source attribution: Loro (MIT), Automerge (MIT), Yjs (MIT), Iroh (Apache 2.0) — per NOTICE.md.

---

**End of hand-off.**
