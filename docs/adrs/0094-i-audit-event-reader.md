---
id: 94
title: IAuditEventReader — Read-Side Audit Substrate Primitive
status: Proposed
date: 2026-05-21
proposed-date: 2026-05-21
author: Admiral
tier: kernel
pipeline_variant: sunfish-api-change

concern:
  - audit
  - multi-tenancy
  - security
  - api-contract
  - persistence

enables:
  - audit-trail-viewer
  - forensics-query-substrate
  - cohort-4-audit-events-bridge-endpoint
  - read-write-separation-on-audit-substrate

composes:
  - 8   # Foundation.MultiTenancy
  - 49  # Audit-Trail Substrate (write side; this ADR adds the read side)
  - 69  # ADR Authoring Discipline
  - 91  # ITenantContext Divergence Resolution
  - 92  # Substrate Tenant-Keyed Repository Contract Pattern

extends:
  - 49  # this ADR is the read-side counterpart to IAuditTrail (write side)

supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # Admiral-scope ADR; cohort-4 Engineer PR 0 consumes; pre-auth granted on a per-PR basis only

amendments: []
---

# ADR 0094 — `IAuditEventReader` — Read-Side Audit Substrate Primitive

**Status:** Proposed
**Date:** 2026-05-21
**Resolves:** Cohort-4 C3 audit-trail-viewer Stage-06 hand-off (`shipyard#81`) halt condition H2 — *"`IAuditTrail` doesn't have query-side primitives"*. The existing `IAuditTrail.QueryAsync` is a single-tuple AND-filter stream (sufficient for kernel-internal callers); the cohort-4 viewer's pagination shape, drill-down-by-id semantics, and CSV-export streaming pattern want a dedicated read-side contract.

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Kernel.Audit.IAuditTrail` | Existing | yes — `shipyard/packages/kernel-audit/IAuditTrail.cs:39` |
| `Sunfish.Kernel.Audit.AuditRecord` | Existing | yes — `shipyard/packages/kernel-audit/AuditRecord.cs:49` |
| `Sunfish.Kernel.Audit.AuditQuery` | Existing | yes — `shipyard/packages/kernel-audit/AuditQuery.cs:21` |
| `Sunfish.Kernel.Audit.AuditEventType` | Existing | yes — `shipyard/packages/kernel-audit/AuditEventType.cs:18` |
| `Sunfish.Kernel.Audit.AuditEventType.TenantBoundaryViolation` | Existing | yes — `shipyard/packages/kernel-audit/AuditEventType.cs:33` |
| `Sunfish.Kernel.Audit.InMemoryAuditTrail` | Existing | yes — `shipyard/packages/kernel-audit/InMemoryAuditTrail.cs:37` |
| `Sunfish.Kernel.Audit.EventLogBackedAuditTrail` | Existing | yes — `shipyard/packages/kernel-audit/EventLogBackedAuditTrail.cs` |
| `Sunfish.Foundation.MultiTenancy.ITenantContext` | Existing (ADR 0091 foundation) | yes — `shipyard/packages/foundation-multitenancy/ITenantContext.cs:8` |
| `Sunfish.Foundation.Persistence.ITenantScopedRepository<TEntity, TKey>` | Existing (ADR 0092 marker) | yes — `shipyard/packages/foundation-persistence/ITenantScopedRepository.cs:79` |
| `Sunfish.Foundation.Persistence.IMustHaveTenant` | Existing | yes |
| `Sunfish.Foundation.Assets.Common.TenantId` | Existing | yes |
| `Sunfish.Foundation.Crypto.PrincipalId` | Existing | yes |
| `Sunfish.Kernel.Audit.IAuditEventReader` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Kernel.Audit.AuditEventReaderQuery` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Kernel.Audit.AuditEventPage` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Kernel.Audit.AuditEventCursor` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Kernel.Audit.InMemoryAuditEventReader` | Introduced by this ADR | no — added in Step 1 PR |
| ADR 0049 (audit-trail substrate; write side) | Existing | yes — `shipyard/docs/adrs/0049-audit-trail-substrate.md` |
| ADR 0091 (ITenantContext Divergence Resolution) | Existing | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` |
| ADR 0092 (Substrate Tenant-Keyed Repository Contract Pattern) | Existing | yes — `shipyard/docs/adrs/0092-substrate-tenant-keyed-repository-contract.md` |
| Cohort-4 Stage-06 hand-off (PR shipyard#81; halt condition H2) | Existing | yes — `shipyard/icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md` (in shipyard#81 worktree) |
| net-architect verdict on shipyard#71 (class-private helper pattern) | Existing | yes — `coordination/inbox/council-verdict-2026-05-21T1229Z-net-architect-shipyard-71-bridge-audit-emitter.md` |
| sec-eng verdict on shipyard#69 (9-layer defense-in-depth model) | Existing | yes — `coordination/inbox/council-verdict-2026-05-21T1228Z-security-engineering-shipyard-69-ignore-query-filters-severity.md` |
| `Sunfish.Blocks.Financial.Payments.Services.InMemoryPaymentRepository` (canonical pattern reference) | Existing | yes — `shipyard/packages/blocks-financial-payments/Services/InMemoryPaymentRepository.cs:15` |
| `Sunfish.Blocks.Maintenance.Services.InMemoryMaintenanceService` (canonical pattern reference) | Existing | yes — `shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs:33` |

§A0 totals: 24 cited references. Existing & verified: 19. Introduced by this ADR: 5 (`IAuditEventReader` interface, `AuditEventReaderQuery` record, `AuditEventPage` record, `AuditEventCursor` opaque value, `InMemoryAuditEventReader` reference implementation).

**§A0 prose note.** `IAuditTrail.QueryAsync` exists today and is sufficient for kernel-internal callers (compliance projections, retention reporters) — see ADR 0049's "Initial contract surface" and `InMemoryAuditTrail.QueryAsync` at line 57. This ADR does NOT supersede that contract; it adds a parallel read-side contract optimized for the cohort-4 viewer's call shapes (paginated lists with cursor, get-by-id with uniform-empty cross-tenant, streaming export). The two contracts coexist; `IAuditEventReader` is the canonical surface that Bridge endpoints + future read-side consumers SHOULD adopt, with `IAuditTrail.QueryAsync` retained for ADR 0049's kernel-internal stream.

---

## Context

Cohort-4 (W#78) ships a C3 audit-trail viewer per the V2 #6 scope survey — a paginated table of audit events with filter, drill-down, and CSV export, surfaced at `/audit-trail` in the Anchor React app. The Stage-06 hand-off (`shipyard#81`) prescribes an Engineer prereq PR 0 that opens a `GET /api/v1/audit-events` Bridge endpoint family. That endpoint family's handlers want a read-side substrate primitive — a tenant-scoped, paginated, get-by-id-capable, streaming-export-capable contract — that the existing `IAuditTrail` write-side interface does not provide.

The existing `IAuditTrail` surface has two methods (per `packages/kernel-audit/IAuditTrail.cs`):

```csharp
public interface IAuditTrail
{
    ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default);
    IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default);
}
```

`AppendAsync` is the write side (`foundation-recovery` per ADR 0046 #48f, the cohort-2 service-layer Option A guards per ADR 0092 §A6, the V2 #3 Bridge audit-emission retrofit per the net-architect verdict on `shipyard#71`). `QueryAsync` is a single-tuple AND-filter stream — sufficient for in-process kernel consumers (compliance projections, retention reporters) but mismatched with the cohort-4 viewer's call shapes:

1. **Paginated list** — the viewer wants a bounded-size page plus a `next_cursor`; `IAsyncEnumerable` is a stream-everything-or-cancel-mid-iteration shape, not a give-me-N-rows-and-tell-me-where-to-resume shape. The cohort-4 hand-off §4.2 calls for `page_size=50` with a `cursor` query parameter; the handler needs to stop at N rows AND produce a forward-resumable handle.

2. **Get-by-id with uniform cross-tenant empty** — the viewer's detail page (FED PR 2 per the hand-off §6) fetches a single audit event by id. The W#23.3 P1 uniform-404 precedent + ADR 0092 §A3 "Diagnostic non-leak invariant" says cross-tenant `Get*` returns null (same code path as not-found), no diagnostic leak. `IAuditTrail.QueryAsync` cannot express this — it is filter-stream-only; there is no `GetByIdAsync`. Engineer PR 0 either grows an inline filter over `QueryAsync` (couples viewer to the stream shape) or grows a separate read-side primitive (this ADR).

3. **Streaming CSV export** — the CSV-export endpoint (hand-off §4.5) wants a tenant-scoped streaming source that bypasses pagination — the full result set under filter, streamed to the HTTP response without buffering. `QueryAsync` already returns `IAsyncEnumerable<AuditRecord>` but its `AuditQuery` lacks the cursor-decoded-resume-point + correlation_id + pagination-bounded shape the cohort-4 endpoint needs.

4. **Filter shape mismatch** — `AuditQuery` (per `packages/kernel-audit/AuditQuery.cs`) carries `TenantId + EventType + OccurredAfter/OccurredBefore + IssuedBy`. Cohort-4 wants `TenantId + EventType + From/To + CorrelationId + Cursor + PageSize`. The cursor + page_size + correlation_id dimensions are new; the existing `IssuedBy` dimension is not in cohort-4 scope (forward-watched for a future security-review surface).

Three options to address this gap. The decision space is shaped by ADR 0092's "substrate canonical" framing (class-private helper for emitters per the net-architect verdict on shipyard#71) and ADR 0091/0092's tenant-scoping discipline (server-derived `TenantId`, EXPLICIT first-positional parameter, uniform cross-tenant empty per A3).

---

## Decision drivers

- **Cohort-4 viewer ships ~2-3h Engineer + ~3-5h FED; substrate work should be minimal.** The Stage-06 hand-off explicitly punts substrate decisions to halt condition H2 with the disposition *"if substrate work is substantive, STOP + file engineer-question."* This ADR resolves the question before Engineer reaches that halt — the cleanest path is a small new read-side interface that mirrors the substrate's existing tenant-scoping discipline.

- **ADR 0092 §A3 diagnostic non-leak invariant.** Cross-tenant `GetByIdAsync(other-tenant-audit-id)` MUST return null (uniform-empty), not throw, not 404-with-diagnostic, not 403. Any chosen option must compose with ADR 0092's substrate norm.

- **ADR 0092 §A6 audit-emission at tenant-boundary violations.** When the reader detects a cross-tenant probe (caller-supplied audit-id belongs to another tenant), it MUST emit `AuditEventType.TenantBoundaryViolation` per the canonical 5-field payload shape (`entity_type`, `entity_id`, `requested_tenant`, `actual_tenant`, `correlation_id`). This is the only Sunfish substrate that audit-emits ON ITSELF — a delicate recursion the design must handle without infinite-loops.

- **ADR 0091 R2 tenant scoping — server-derived, EXPLICIT first-positional parameter.** The `TenantId` parameter MUST be EXPLICIT on every method (per ADR 0092 §Q1 fixed resolution); ambient resolution at the implementation level is acceptable for cross-cutting concerns but SHALL NOT replace the explicit contract parameter. The cohort-4 Bridge handler sources `TenantId` from `tenantContext.TenantId` (ADR 0091) and forwards it; the reader interface SHALL take it as the first positional parameter.

- **ADR 0049's read-write asymmetry.** `IAuditTrail` is named for the WRITE side (it is the trail consumers append to). Its `QueryAsync` is a kernel-internal subscription-style stream for projections and retention reporters; treating it as the canonical read API for UI surfaces would conflate two distinct use-classes (in-process domain stream vs. paginated UI query). Read-write separation (CQRS shape) is the substrate's idiomatic resolution.

- **No new package; same kernel-audit assembly.** Whatever shape this ADR picks ships inside `packages/kernel-audit/` (not a new package). The shape is small (one interface + 3-4 supporting types + one in-memory reference impl); a new package would be unjustified scaffolding cost.

- **Council-attestation discipline (per ADR 0069).** This ADR is kernel-tier and audit-substrate-shaping; pre-merge council review by BOTH `.NET-architect` and `security-engineering` is mandatory before promotion to `Accepted` (mirrors the dual-council discipline on ADR 0091 R2 and ADR 0092 Rev 2).

- **Recursion-safe audit-emission.** A reader that emits `TenantBoundaryViolation` audit events must NOT call back through `IAuditEventReader` to verify those emissions — that path is for the write-side `IAuditTrail.AppendAsync`. The reader and writer share substrate (kernel `IEventLog` per ADR 0049) but the reader does not emit to itself.

---

## Considered options

### Option A — Extend existing `IAuditTrail` with read methods

Add `GetAsync(TenantId tenantId, Guid auditId, CancellationToken ct)`, `ListAsync(AuditEventReaderQuery query, CancellationToken ct)`, and pagination + cursor types to the existing `IAuditTrail` interface. Existing `IAuditTrail` consumers (`foundation-recovery`, `DefaultPaymentApplicationService`, Bridge audit emitter) keep their write-side usage; new read-side consumers (cohort-4 Bridge handler, future audit-related UIs) call the new methods on the same interface.

**Pro:**
- Smallest surface area at the substrate level (one interface, not two).
- No new abstractions to wire through DI; existing `services.AddScoped<IAuditTrail, EventLogBackedAuditTrail>()` registration already covers consumers.
- Existing tests + reference implementations (`InMemoryAuditTrail`, `EventLogBackedAuditTrail`) extend in place.

**Con:**
- **Conflates read and write contracts.** Every consumer that injects `IAuditTrail` (currently 11 in the substrate — `foundation-recovery`, recovery handlers, payment service, taxonomy substrate, work-orders, leases, etc.) gains read-side methods it does not use. Interface-segregation violation.
- **Cannot insulate the write-side semantics under a read-only role.** Bridge handlers that should only read end up able to write (`AppendAsync` is on the same surface). Sec-eng disposition: ADR 0092's "compile-time tenant boundary" framing argues the same shape here — read consumers should not be able to write.
- **`IAuditTrail.QueryAsync` semantics drift.** The existing `IAsyncEnumerable<AuditRecord>` stream-everything contract conflicts with paginated-page semantics; either `QueryAsync` becomes a separate-from-`ListAsync` method on the same interface (two read methods doing nearly-the-same-thing) or the existing `QueryAsync` morphs (breaking change for kernel-internal consumers).
- **Reference-implementation cost.** `EventLogBackedAuditTrail` (kernel) + `InMemoryAuditTrail` (test/dev) both grow read-side methods they may not need for their primary use cases (production audit append + restart-volatile dev/test).
- **Tests across consumers expand.** Every existing `Mock<IAuditTrail>` test fixture gains methods it does not exercise; mocking burden grows.

**Verdict: rejected.** Interface-segregation violation is the deciding factor; the cohort-4 viewer's read-side needs are structurally distinct from the kernel-internal write+stream needs that `IAuditTrail` serves.

### Option B — Create new `IAuditEventReader` interface alongside `IAuditTrail` (read-write separation; CQRS shape) [RECOMMENDED]

Introduce `IAuditEventReader` as a parallel interface in `packages/kernel-audit/`. Add a single reference implementation (`InMemoryAuditEventReader`) that shares the in-memory store with `InMemoryAuditTrail` via constructor injection (or DI-resolved store), so test fixtures get consistent behavior without two parallel stores. Production reference implementation (`EventLogBackedAuditEventReader`) layers over the same kernel `IEventLog` substrate as `EventLogBackedAuditTrail` — no second storage path.

**Pro:**
- **Read-write separation per CQRS conventions.** Bridge handlers and UI-adjacent consumers inject `IAuditEventReader`; in-process write-side consumers continue to inject `IAuditTrail`. Interface-segregation respected. Compile-time boundary between "code that can read audit" and "code that can write audit."
- **Paginated + cursor + correlation_id + page_size first-class.** The new interface is shaped for the cohort-4 viewer's call surfaces, not retrofitted onto a stream-everything contract. `GetByIdAsync` returns `Task<AuditRecord?>` (uniform-empty cross-tenant per ADR 0092 §A3) which `IAsyncEnumerable` cannot express idiomatically.
- **No change to `IAuditTrail`.** ADR 0049's contract stays stable; kernel-internal consumers (compliance projections, retention reporters) are unaffected. Zero migration cost for the 11 existing consumers.
- **Substrate-canonical implementation pattern.** `InMemoryAuditEventReader` mirrors `InMemoryPaymentRepository` (ADR 0092 §A6 canonical) — class-private `EmitTenantBoundaryViolationAsync` helper per the net-architect verdict on shipyard#71; tenant-scoping discipline per ADR 0091 R2 + ADR 0092 §Q1 (EXPLICIT `TenantId` first-positional parameter); uniform-empty cross-tenant per ADR 0092 §A3.
- **Defense-in-depth respected.** Per the sec-eng verdict on shipyard#69 §"Defense-in-depth analysis" (9-layer model), `IAuditEventReader` slots into layer 6 (audit emission on cross-tenant probes) WITHOUT compromising layers 1-5 (`HasQueryFilter`, `.WhereTenant`, analyzer enforcement at Step 4a/4b/4c, `[WithoutTenantFilter]` attribute, service-layer Option A guards). The new interface composes with the existing substrate, not in tension with it.
- **Marker-conformance ambiguity is absent.** `AuditRecord` already implements `IMustHaveTenant`. The reader is NOT an `ITenantScopedRepository<AuditRecord, Guid>` (ADR 0092's marker) because it is kernel-tier read-side, not block-cluster CRUD; it inherits the tenant-scoping discipline from the marker without claiming the marker itself. Cohort-4 Engineer PR 0 carries a brief xmldoc note in `IAuditEventReader.cs` clarifying this (one paragraph in the interface remarks).

**Con:**
- **One additional interface in the kernel-audit package.** Modest scaffolding cost: ~80 LOC for the interface + xmldoc, ~50 LOC for the in-memory reference impl, ~30 LOC for the event-log-backed reference impl, ~120 LOC for read-side tests = roughly 280 LOC total for the new substrate surface.
- **Consumers must distinguish read and write paths in DI.** Bridge handler injects `IAuditEventReader auditReader, IAuditTrail auditTrail` (the latter only when the handler also writes — e.g., the same Bridge endpoint emits `TenantBoundaryViolation` via the audit trail AND queries via the reader). Two DI registrations per consumer. Mitigation: the two paths are already split in cohort-4's hand-off — handler emits via `IAuditTrail.AppendAsync` (existing) and queries via `IAuditEventReader` (new).
- **Tests grow modestly.** Bridge endpoint integration tests now wire both `Mock<IAuditEventReader>` and `Mock<IAuditTrail>` where they previously would have wired only the former. Net test cost is small (mocks compose).

**Verdict: adopted.** Read-write separation is the standard substrate-design resolution for this shape; the cost (one new interface + 280 LOC of substrate code) is small relative to the structural clarity gain.

### Option C — Mark `IAuditEventReader` as a behavioral facet on the existing `IAuditTrail` implementation (single class implements both)

Introduce `IAuditEventReader` as an interface (same as Option B) but bind both interfaces to the same concrete implementation class (`EventLogBackedAuditTrail` implements `IAuditTrail` AND `IAuditEventReader`). DI registers two interface-to-implementation maps onto the same instance: `services.AddScoped<EventLogBackedAuditTrail>()` + `services.AddScoped<IAuditTrail>(sp => sp.GetRequiredService<EventLogBackedAuditTrail>())` + `services.AddScoped<IAuditEventReader>(sp => sp.GetRequiredService<EventLogBackedAuditTrail>())`. Consumers inject the interface they want; the runtime instance is shared.

**Pro:**
- Read-write separation at the contract level (matches Option B's interface-segregation benefit at the consumer layer).
- Single storage path; no risk of read/write divergence within an implementation.
- Smaller test surface than Option B (one class to test through two interface lenses).

**Con:**
- **DI complexity.** The "register a concrete + two interfaces forwarding to it" pattern is non-idiomatic in the substrate. ADR 0092 §C5 mandates Scoped lifetime for `ITenantScopedRepository<,>` implementations and ships a startup-assertion extension that scans for lifetime overrides; that assertion would need an extension to recognize "two interfaces, same concrete" without flagging it.
- **Test substrate drift.** `InMemoryAuditTrail` exists today and is widely consumed in tests; a single class implementing both interfaces would need an `InMemoryAuditTrailAndReader` (or rename `InMemoryAuditTrail` to absorb the new methods). The rename ripples through every existing test that names `InMemoryAuditTrail`. Migration cost is real.
- **Couples deployment of reader to writer.** If future Bridge architectures decompose audit (e.g., read-side scales out separately from write-side, behind a CDC pipeline), Option C makes that decomposition harder. Option B's separate interfaces are decomposition-friendly from day one.
- **Cuts against the substrate-canonical separation.** ADR 0049's substrate-impl insulation discipline argued for parallel-to-`Kernel.Ledger` separation; Option C re-merges what ADR 0049's framing kept distinct. Sec-eng disposition (per shipyard#69 verdict): "lossy → Warning; leaky → Error" — couplings that constrain future scaling are leaky, not lossy.

**Verdict: rejected.** DI complexity and substrate-impl coupling cost outweigh the modest test-substrate savings. Option B's clean separation is the right shape for a substrate primitive that may grow read-side scale-out in the future.

---

## Decision

**Adopt Option B.** Introduce `IAuditEventReader` as a new interface in `packages/kernel-audit/` alongside the existing `IAuditTrail`. Ship a single in-memory reference implementation (`InMemoryAuditEventReader`) and a single event-log-backed reference implementation (`EventLogBackedAuditEventReader`). Bridge handlers (cohort-4 Engineer PR 0 + future read-side consumers) inject `IAuditEventReader`; in-process write-side consumers continue to inject `IAuditTrail`.

### Contract surface (Step 1)

```csharp
// ── Sunfish.Kernel.Audit ─────────────────────────────────────────────────
namespace Sunfish.Kernel.Audit;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

/// <summary>
/// Read-side audit substrate primitive. Counterpart to the write-side
/// <see cref="IAuditTrail"/> interface (ADR 0049). Optimized for paginated
/// UI surfaces (the cohort-4 audit-trail viewer per W#78), drill-down
/// detail pages, and streaming CSV export. Tenant-scoping discipline
/// inherits ADR 0091's <c>ITenantContext</c> resolution + ADR 0092's
/// EXPLICIT-first-positional-parameter + uniform-empty-cross-tenant +
/// audit-emission-on-cross-tenant-probe substrate norms.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layering.</b> Per ADR 0094 (this ADR), <c>IAuditEventReader</c> is
/// the canonical read-side surface for the audit substrate. The
/// production implementation (<see cref="EventLogBackedAuditEventReader"/>)
/// layers over the SAME kernel <c>IEventLog</c> substrate as
/// <see cref="EventLogBackedAuditTrail"/> — read and write share storage;
/// the separation is contractual, not physical.
/// </para>
///
/// <para>
/// <b>Read-write separation rationale.</b> ADR 0049's <see cref="IAuditTrail.QueryAsync"/>
/// remains the kernel-internal subscription-style stream for compliance
/// projections and retention reporters. <c>IAuditEventReader</c> is the
/// UI-adjacent paginated-list + get-by-id + export-stream contract for
/// Bridge handlers and future audit-trail viewers. The two contracts
/// coexist; new read-side consumers SHOULD prefer this interface.
/// </para>
///
/// <para>
/// <b>Tenant scoping (ADR 0091 + ADR 0092).</b> Every method accepts
/// <see cref="TenantId"/> as the FIRST positional parameter. Sourcing the
/// tenant value is the caller's responsibility — at the Bridge layer the
/// canonical pattern is
/// <c>var tenantId = new TenantId(tenantContext.TenantId)</c>. Cross-tenant
/// reads return uniform-empty (null for <see cref="GetByIdAsync"/>; empty
/// page for <see cref="ListAsync"/> and <see cref="StreamAsync"/>) per
/// ADR 0092 §A3 — same code path as not-found, no diagnostic leak.
/// </para>
///
/// <para>
/// <b>Audit emission on cross-tenant probes (ADR 0092 §A6).</b> When
/// <see cref="GetByIdAsync"/> finds an <c>AuditRecord</c> whose
/// <c>TenantId</c> does not equal the caller's <c>tenantId</c>, the
/// implementation SHALL emit
/// <c>AuditEventType.TenantBoundaryViolation</c> before returning null.
/// Emission goes through the WRITE-side <see cref="IAuditTrail"/> — the
/// reader does not append to itself, avoiding recursion. The canonical
/// 5-field payload (<c>entity_type</c>, <c>entity_id</c>,
/// <c>requested_tenant</c>, <c>actual_tenant</c>, <c>correlation_id</c>)
/// is constructed inline per the net-architect verdict on shipyard#71
/// (class-private helper pattern). <see cref="ListAsync"/> +
/// <see cref="StreamAsync"/> filter by tenant at the query boundary and
/// do NOT emit per-result; ADR 0092 §A6 explicitly carves out
/// list-time per-row emission as out-of-scope for the substrate norm.
/// </para>
///
/// <para>
/// <b>Not an <c>ITenantScopedRepository&lt;AuditRecord, Guid&gt;</c>.</b>
/// The marker interface (ADR 0092) is for block-cluster CRUD repositories
/// (Invoice, Bill, Payment, Lease, etc.). <see cref="IAuditEventReader"/>
/// is a kernel-tier read-side primitive that INHERITS the substrate's
/// tenant-scoping discipline (EXPLICIT parameter, uniform-empty,
/// audit-emission) without claiming the marker itself. The Step 4a/4b/4c
/// analyzers do not currently scan kernel-audit; if a future analyzer
/// extension covers kernel-tier reads, this interface joins the surveyed
/// set.
/// </para>
/// </remarks>
public interface IAuditEventReader
{
    /// <summary>
    /// Fetch a single audit record by <paramref name="auditId"/>, scoped to
    /// <paramref name="tenantId"/>. Returns null if the record does not exist
    /// OR belongs to another tenant (uniform-empty per ADR 0092 §A3 — no
    /// diagnostic leak distinguishing "not found" from "belongs to other
    /// tenant"). Implementations SHALL emit
    /// <c>AuditEventType.TenantBoundaryViolation</c> on the cross-tenant path
    /// before returning null (audit emission via the write-side
    /// <see cref="IAuditTrail"/>; see interface remarks).
    /// </summary>
    /// <param name="tenantId">The calling tenant (server-derived from <see cref="Sunfish.Foundation.MultiTenancy.ITenantContext"/> per ADR 0091).</param>
    /// <param name="auditId">The record's stable identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditRecord?> GetByIdAsync(
        TenantId tenantId,
        Guid auditId,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch a single page of audit records matching <paramref name="query"/>,
    /// scoped to <paramref name="tenantId"/>. Records are returned in
    /// reverse-chronological order (<c>OccurredAt DESC, AuditId DESC</c>) so
    /// the most recent events appear first. Returns an empty page (no
    /// records, <c>NextCursor = null</c>) if the tenant has no matching
    /// records — the same code path as "tenant has records but none match
    /// the filter."
    /// </summary>
    /// <param name="tenantId">The calling tenant.</param>
    /// <param name="query">Filter + pagination. <see cref="AuditEventReaderQuery.PageSize"/> capped at 200 per ADR 0094 §"Pagination posture" (matches cohort-4 hand-off §4.2 §Server REJECTS).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditEventPage> ListAsync(
        TenantId tenantId,
        AuditEventReaderQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Stream every audit record matching <paramref name="query"/>, scoped
    /// to <paramref name="tenantId"/>. Backs the CSV-export endpoint (cohort-4
    /// hand-off §4.5) and any future bulk-read consumer. Unlike
    /// <see cref="ListAsync"/>, this method does NOT cap result-set size
    /// at the page level — the caller is responsible for any upstream cap
    /// (cohort-4 Engineer PR 0 enforces a 10M-row hard cap at the handler
    /// layer per the hand-off's Adversarial Brief Decision 6).
    /// </summary>
    /// <param name="tenantId">The calling tenant.</param>
    /// <param name="query">Filter only; pagination fields (<see cref="AuditEventReaderQuery.PageSize"/>, <see cref="AuditEventReaderQuery.Cursor"/>) are IGNORED for stream calls.</param>
    /// <param name="ct">Cancellation token. Cancelling ends enumeration cleanly.</param>
    IAsyncEnumerable<AuditRecord> StreamAsync(
        TenantId tenantId,
        AuditEventReaderQuery query,
        CancellationToken ct = default);
}

/// <summary>
/// Read-side query shape for <see cref="IAuditEventReader"/>. Distinct from
/// <see cref="AuditQuery"/> (the kernel-internal stream filter for
/// <see cref="IAuditTrail.QueryAsync"/>) because the read-side surface
/// targets UI / Bridge call shapes (pagination, cursor, correlation-id
/// lookup) rather than the kernel-internal compliance-projection stream
/// pattern.
/// </summary>
/// <param name="EventType">Optional. Match a single event type (e.g., <c>AuditEventType.TenantBoundaryViolation</c>). Combine multiple queries to OR across types.</param>
/// <param name="From">Optional. Inclusive lower bound on <see cref="AuditRecord.OccurredAt"/>.</param>
/// <param name="To">Optional. Inclusive upper bound on <see cref="AuditRecord.OccurredAt"/>.</param>
/// <param name="CorrelationId">Optional. Match records whose payload carries this correlation-id (drill-down from a downstream entity to its originating audit events).</param>
/// <param name="PageSize">Page size for <see cref="IAuditEventReader.ListAsync"/>. Capped at 200; defaults to 50 if omitted. Ignored by <see cref="IAuditEventReader.StreamAsync"/>.</param>
/// <param name="Cursor">Opaque continuation token from a prior <see cref="IAuditEventReader.ListAsync"/> response's <see cref="AuditEventPage.NextCursor"/>. Tenant-bound — see <see cref="AuditEventCursor"/>.</param>
public sealed record AuditEventReaderQuery(
    AuditEventType? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? CorrelationId = null,
    int PageSize = 50,
    AuditEventCursor? Cursor = null);

/// <summary>
/// A single page of audit records returned by
/// <see cref="IAuditEventReader.ListAsync"/>.
/// </summary>
/// <param name="Records">The records in this page (reverse-chronological order). Empty if no records match.</param>
/// <param name="NextCursor">Opaque continuation token; pass back to <see cref="IAuditEventReader.ListAsync"/> via <see cref="AuditEventReaderQuery.Cursor"/> to fetch the next page. Null when there are no more pages.</param>
/// <param name="HasMore">True when <paramref name="NextCursor"/> is non-null (convenience for UI consumers that prefer a boolean over a null check).</param>
public sealed record AuditEventPage(
    IReadOnlyList<AuditRecord> Records,
    AuditEventCursor? NextCursor,
    bool HasMore);

/// <summary>
/// Opaque pagination continuation token. Carries the (occurred_at,
/// audit_id) point to resume after, plus a tenant-id signature so the
/// implementation can reject cross-tenant cursor reuse per the cohort-4
/// hand-off Adversarial Brief Decision 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wire format.</b> The Bridge layer (cohort-4 Engineer PR 0) is
/// responsible for base64-encoding + signing the cursor for HTTP transport;
/// the substrate primitive carries the structured value-object. Bridge
/// serialization signs the tuple via <see cref="IOperationSigner"/> per
/// the hand-off's Decision 2 + Decision 5 mitigations. The substrate
/// itself does NOT mandate the wire format — only the structural shape.
/// </para>
///
/// <para>
/// <b>Tenant binding (Decision 5 — mid-page tenant-switch).</b> Decoded
/// cursors arriving from a different tenant than the caller's current
/// <see cref="TenantId"/> SHALL be rejected at the Bridge handler (400
/// "tenant_changed_reload_page" per the hand-off §4.2). The substrate
/// implementation rejects via uniform-empty on the resulting list call
/// (cross-tenant probe path); the Bridge handler's signature-check fires
/// first.
/// </para>
/// </remarks>
/// <param name="OccurredAt">Resume-after point on <see cref="AuditRecord.OccurredAt"/> (records strictly older are included).</param>
/// <param name="AuditId">Tie-breaker on <see cref="AuditRecord.AuditId"/> for records sharing <see cref="OccurredAt"/>.</param>
/// <param name="TenantId">The tenant this cursor was issued to. Bridge signature verification rejects cross-tenant reuse before the cursor reaches the substrate.</param>
public sealed record AuditEventCursor(
    DateTimeOffset OccurredAt,
    Guid AuditId,
    TenantId TenantId);
```

### Reference implementations (Step 2)

Ship two reference implementations in `packages/kernel-audit/`:

1. **`InMemoryAuditEventReader`** — restart-volatile; shares the in-memory store with `InMemoryAuditTrail` via constructor injection so test fixtures get consistent read-after-write behavior without two parallel stores.

   ```csharp
   public sealed class InMemoryAuditEventReader : IAuditEventReader
   {
       private readonly InMemoryAuditTrail _trail;      // shared in-memory store
       private readonly IAuditTrail _emitter;            // for audit-emission on cross-tenant probes

       public InMemoryAuditEventReader(
           InMemoryAuditTrail trail,
           IAuditTrail emitter)
       {
           _trail = trail;
           _emitter = emitter;
       }

       public async Task<AuditRecord?> GetByIdAsync(
           TenantId tenantId,
           Guid auditId,
           CancellationToken ct = default)
       {
           // Look up via the shared store's snapshot.
           // If found AND tenant matches → return.
           // If found AND tenant DOES NOT match → emit TenantBoundaryViolation + return null.
           // If not found → return null (no audit emission; not-found is not a probe signal).
       }

       public async Task<AuditEventPage> ListAsync(/* ... */) { /* ... */ }

       public IAsyncEnumerable<AuditRecord> StreamAsync(/* ... */) { /* ... */ }

       // Class-private helper per net-architect verdict on shipyard#71
       // (canonical 5-field payload; mirrors InMemoryPaymentRepository.EmitTenantBoundaryViolationAsync):
       private async ValueTask EmitTenantBoundaryViolationAsync(
           string entityId,
           TenantId requestedTenant,
           TenantId actualTenant,
           CancellationToken ct)
       {
           // build AuditPayload + sign + AppendAsync via _emitter
           // (NOT through the reader — recursion-safe)
       }
   }
   ```

2. **`EventLogBackedAuditEventReader`** — layers over the kernel `IEventLog` substrate (same path `EventLogBackedAuditTrail` uses); reads via the event-log's `ReplayAsync` filtered to audit events; pagination via in-memory ordering + skip-while-cursor. Future EF Core-backed implementation (when audit persistence migrates to a SQL surface per ADR 0049's future audit-infrastructure track) uses `HasQueryFilter` per ADR 0092 §"Step 2 EFCore query-filter convention".

### DI registration shape (Step 3)

`packages/kernel-audit/DependencyInjection/ServiceCollectionExtensions.cs` gains a new extension method (or amends the existing `AddSunfishKernelAudit`):

```csharp
public static IServiceCollection AddSunfishKernelAuditReader(this IServiceCollection services)
{
    services.AddScoped<IAuditEventReader, EventLogBackedAuditEventReader>();
    // InMemory variant registered via a separate AddSunfishKernelAuditInMemory()
    // extension for test fixtures + dev hosts; mirrors the existing
    // AddSunfishKernelAudit / AddSunfishKernelAuditInMemory split.
    return services;
}
```

Scoped lifetime per ADR 0092 §C5 (the substrate's standing DI-lifetime mandate; reader lifetime parallels writer lifetime so they share the same DI scope when both are injected into the same handler).

### Filter API design — what's in, what's out

**In scope (cohort-4 demand):**
- `EventType` — single-type match; combine multiple queries to OR.
- `From` / `To` — inclusive date range; Bridge handler validates `From ≤ To` and the 1-year max range (cohort-4 hand-off Adversarial Brief Decision 4).
- `CorrelationId` — drill-down from a downstream entity (Invoice, Payment, Work Order) to its originating audit events. Optional; when present, `From`/`To` MAY be ignored by Bridge handlers (hand-off §4.2 lists this as a Bridge-handler rule, not a substrate rule — the substrate accepts both and the handler decides).
- `PageSize` — 1..200; default 50.
- `Cursor` — opaque continuation.

**Out of scope (forward-watched; future ADR amendments):**
- `IssuedBy` — principal-filter for security-review (was in `AuditQuery`); deferred to a future security-review surface ADR. Reader callers wanting this today fall back to `IAuditTrail.QueryAsync`.
- `Multi-event-type OR` — caller composes multiple `ListAsync` calls or uses the future security-review surface.
- `Free-text payload search` — explicitly NOT in scope; payload is opaque dictionary per `AuditPayload`. A future compliance-search ADR may add structured payload-index support.
- `Cross-tenant audit query` (super-admin tenant-spanning view) — explicitly NOT in scope per cohort-4 §1.3; deferred to a future super-admin surface ADR (C2 in V2 #6).

### Pagination posture (cursor vs. offset)

**Cursor-based pagination.** Rationale (sec-eng concurrence required at council attestation):

1. **Append-only data class.** Audit records are append-only by definition (ADR 0049). Offset pagination on append-only data is stable across page boundaries — but cursor pagination is ALSO stable AND avoids the O(N) cost of scanning past N records on every page-N+1 request. For long-retention audit data (Phase 2 commercial scope: 7-year IRS retention), the offset cost grows unboundedly; cursor cost stays constant per page.

2. **Decision 2 + Decision 5 mitigations need cursor structure.** The cohort-4 hand-off Adversarial Brief Decision 2 (cross-tenant cursor forgery) and Decision 5 (mid-page tenant-switch) both rely on the cursor carrying a tenant-id signature that the Bridge layer can sign + verify. An offset (just an integer N) cannot carry tenant context. Cursor carries it natively.

3. **No cross-page count assumed.** UI surfaces that need "total count" for a result set are out-of-scope at cohort-4 (the hand-off's Decision 6 explicitly caps export at 10M and does NOT compute a total). When/if total-count surfaces emerge, a future ADR amendment adds a `CountAsync` method to the reader; the cursor + page model is unaffected.

### Performance posture (lazy vs. eager hydration)

**Lazy by default.** `GetByIdAsync` returns a hydrated `AuditRecord` (single read). `ListAsync` returns up-to-PageSize hydrated records (bounded read; PageSize ≤ 200). `StreamAsync` is `IAsyncEnumerable` — the consumer iterates and the implementation yields one record at a time (memory bounded to one record + the kernel `IEventLog`'s buffer; no in-memory accumulation per the hand-off §4.5 Decision 6 mitigation).

For the cohort-4 Bridge endpoint family, the CSV-export streaming pattern (per the hand-off §4.5) writes one CSV row per `StreamAsync` yield + flushes to the HTTP response — never accumulating a full result set in memory. The 10M-row hard cap is enforced at the handler layer (incrementing a counter on yield); the substrate primitive itself does not cap.

### Recursion safety — audit-emission within the reader

When `GetByIdAsync` detects a cross-tenant probe, it emits via the WRITE-side `IAuditTrail.AppendAsync` (constructor-injected), NOT through `IAuditEventReader`. The emission writes a new `TenantBoundaryViolation` `AuditRecord`; that record is later READABLE through `IAuditEventReader` by callers within the correct tenant scope, but is never read BY the emitter as part of the emission path. No recursion possible.

`ListAsync` and `StreamAsync` do NOT emit per-row — they filter at the query boundary by tenant, returning empty results when the caller's tenant has no matching records. ADR 0092 §A6 explicitly designates list-time per-row emission as out-of-scope for the substrate norm (rationale in the ADR: "too noisy; a future aggregate-level emission may surface list-time filtering counts").

---

## Consequences

### Positive

- **Cohort-4 Engineer PR 0 unblocked.** Stage-06 hand-off halt condition H2 is resolved by this ADR's contract surface; Engineer can build the Bridge `audit-events` endpoint family against `IAuditEventReader` without growing inline filters over `IAuditTrail.QueryAsync` and without a sec-eng SPOT-CHECK punt to a later cohort.
- **Read-write separation at the substrate level.** Future audit-related surfaces (audit-of-audit per a forward-watched ADR; compliance-search per a future ADR; super-admin tenant-spanning view per the V2 #6 C2 candidate) inherit the same `IAuditEventReader` shape and extend it through additional methods or marker-extension ADRs. The pattern compounds.
- **Substrate-canonical implementation pattern preserved.** Class-private emit helper (net-architect verdict on shipyard#71); EXPLICIT `TenantId` first-positional parameter (ADR 0092 §Q1); uniform-empty cross-tenant (ADR 0092 §A3); audit-emission on cross-tenant probes (ADR 0092 §A6); Scoped DI lifetime (ADR 0092 §C5). No new substrate norms introduced; existing norms applied to a new surface.
- **Defense-in-depth respected (sec-eng 9-layer model).** `IAuditEventReader` slots into layers 6 + 7 (audit emission on cross-tenant probes + integration tests for uniform-empty) without compromising layers 1-5 (HasQueryFilter, .WhereTenant, Step 4a/4b/4c analyzers, [WithoutTenantFilter] attribute, service-layer Option A guards). Sec-eng can re-use the shipyard#71 SPOT-CHECK pattern when reviewing the cohort-4 Engineer PR 0.
- **ADR 0049 stays stable.** No change to `IAuditTrail`; existing 11 consumers (foundation-recovery, recovery handlers, payment service, taxonomy substrate, work-orders, leases, etc.) continue to inject the write-side interface they already use. Zero migration cost on the write side.
- **Test substrate consistent.** `InMemoryAuditEventReader` shares the in-memory store with `InMemoryAuditTrail` via constructor injection; test fixtures get read-after-write consistency without parallel-store bookkeeping. Existing kernel-audit tests are unaffected.

### Negative

- **One additional interface in the kernel-audit package.** Modest scaffolding cost (~280 LOC across interface + two reference implementations + tests). Mitigation: the contract is small (3 methods + 3 supporting types); growth gated on real future demand.
- **Bridge handlers that both read AND write audit inject two interfaces.** Cohort-4 Engineer PR 0's handler injects `IAuditEventReader auditReader, IAuditTrail auditTrail` (the latter for `TenantBoundaryViolation` emission). DI registration grows by one line. Mitigation: this is the standard CQRS shape; the two-interface injection signals the read+write nature of the handler to reviewers.
- **`AuditQuery` (kernel-internal) and `AuditEventReaderQuery` (UI-adjacent) coexist with overlapping fields.** Both carry `EventType`, time range. Mitigation: the kernel-internal shape is for compliance projections (no pagination, no correlation-id lookup); the UI-adjacent shape is for Bridge handlers. The duplication is intentional contract-design choice (interface-segregation) rather than accidental drift.
- **Forward-watched filter dimensions accumulate as deferred work.** `IssuedBy` (principal-filter for security review), free-text payload search, super-admin tenant-spanning view all deferred. Mitigation: each lands as a future ADR amendment or follow-on ADR when demand materializes; the cohort-4 hand-off explicitly carves these out of scope and the reader's surface is open to extension.

### Trust impact / Security & privacy

- **`IAuditEventReader` adds a new READ path through the audit substrate.** The threat model question: can a caller use this path to read other tenants' audit records? Mitigation: uniform-empty cross-tenant (ADR 0092 §A3) + audit-emission on cross-tenant `GetByIdAsync` probes (ADR 0092 §A6) + EXPLICIT `TenantId` first-positional parameter (ADR 0092 §Q1) + cursor-tenant-binding (this ADR's `AuditEventCursor.TenantId` + Bridge-layer signature verification per cohort-4 hand-off Decision 2 + 5). The 9-layer defense-in-depth model (sec-eng verdict on shipyard#69) applies.

- **Recursion-safe audit-emission.** Reader emits via write-side `IAuditTrail.AppendAsync`, not through itself. The emitted `TenantBoundaryViolation` record is later readable by correct-tenant callers; that emission path does NOT call back into `IAuditEventReader` during the emission. No infinite-loop possible.

- **Audit emission of audit-reader access is OUT OF SCOPE.** "Audit who read the audit log" (audit-of-audit) is forward-watched per the cohort-4 hand-off §H6. The reader does NOT emit `AuditEventType.AuditTrailViewerAccessed` (or similar) on every `ListAsync`/`GetByIdAsync` call. When/if regulatory demand materializes (e.g., SOC 2 Type II evidence pack requires audit-of-audit), a future ADR amends this contract to add the emission path.

- **PII in audit payloads is forward-watched.** ADR 0049 does NOT (yet) specify a `[Pii]` tagging convention for audit-payload fields. The cohort-4 viewer's payload pretty-print defaults to display-verbatim with a `[Pii]` mask if tagging is present. When/if PII tagging lands in ADR 0049 amendments, `IAuditEventReader` may need a `RedactionPolicy` parameter on `GetByIdAsync` to mask sensitive fields at the substrate boundary; until then, redaction is a Bridge-layer or FED-layer concern.

- **Cursor forgery threat (cohort-4 Decision 2).** Bridge serializes the cursor via `IOperationSigner` (Ed25519); substrate-layer cursor decoding rejects malformed cursors via `ArgumentException`; substrate-layer cross-tenant cursor reuse falls through to uniform-empty. The cursor is NOT a security boundary on its own (per the hand-off's Decision 2 disposition) — `HasQueryFilter` + EXPLICIT `TenantId` parameter is. Cursor-tenant-binding is defense-in-depth.

---

## Compatibility plan

- **Backward compatibility.** `IAuditTrail` is unchanged. All 11 existing consumers (foundation-recovery, recovery handlers, payment service, taxonomy substrate, work-orders, leases, etc.) continue to use `AppendAsync` (write) and `QueryAsync` (kernel-internal stream) without modification. Zero migration cost.

- **Forward compatibility.** `IAuditEventReader` is purely additive. New consumers (cohort-4 Engineer PR 0, future audit-UI surfaces, future compliance-search ADR) inject the new interface; existing consumers can adopt it incrementally without coordination.

- **DI registration.** `packages/kernel-audit/DependencyInjection/ServiceCollectionExtensions.cs` gains `AddSunfishKernelAuditReader()` (production) + `AddSunfishKernelAuditReaderInMemory()` (test/dev). Hosts that already call `AddSunfishKernelAudit()` add one new line; hosts that don't (i.e., that don't use the read-side surface) need no change.

- **Affected packages.**
  - `packages/kernel-audit/` — interface + supporting types + 2 reference implementations + DI extensions + tests added (Step 1 PR; this ADR's primary scope).
  - `signal-bridge/Sunfish.Bridge/Audit/` — new `AuditEventsEndpoints.cs` file consuming `IAuditEventReader` (cohort-4 Engineer PR 0; OUT of this ADR's scope but UNBLOCKED by this ADR).
  - `sunfish/apps/web/src/api/audit-trail.ts` + `AuditTrailPage.tsx` + `AuditEventDetailPage.tsx` — cohort-4 FED PRs (out of this ADR's scope; downstream consumers).

- **Test substrate.** `packages/kernel-audit/tests/AuditEventReaderTests.cs` (new file) ships with the Step 1 PR. Test surface mirrors existing `AuditTrailTests.cs`:
  - `GetByIdAsync_TenantMatch_ReturnsRecord` — happy path
  - `GetByIdAsync_TenantMismatch_ReturnsNull` — uniform-empty per ADR 0092 §A3
  - `GetByIdAsync_TenantMismatch_EmitsTenantBoundaryViolation` — per ADR 0092 §A6
  - `GetByIdAsync_NotFound_ReturnsNullNoEmission` — not-found path is silent
  - `ListAsync_TenantMatch_ReturnsScopedPage` — pagination happy path
  - `ListAsync_CrossTenantCursor_ReturnsEmpty` — Decision 5 substrate behavior
  - `ListAsync_PageSizeOverLimit_ThrowsArgumentException` — input validation
  - `StreamAsync_TenantMatch_StreamsScopedRecords` — streaming happy path
  - `StreamAsync_Cancelled_EndsEnumeration` — cancellation semantics

---

## Implementation checklist

- [ ] **Step 1 — Substrate primitives (this ADR's Stage-06 PR).** Add `packages/kernel-audit/IAuditEventReader.cs` + `AuditEventReaderQuery.cs` + `AuditEventPage.cs` + `AuditEventCursor.cs`. Pure interface + supporting records; no implementation in this PR. Xmldoc cites this ADR (0094) + ADR 0091 + ADR 0092 + ADR 0049.
- [ ] **Step 2 — In-memory reference implementation.** Add `packages/kernel-audit/InMemoryAuditEventReader.cs` mirroring `InMemoryAuditTrail`'s shape; shares the in-memory store via constructor injection. Class-private `EmitTenantBoundaryViolationAsync` helper per the net-architect verdict on shipyard#71.
- [ ] **Step 3 — Event-log-backed reference implementation.** Add `packages/kernel-audit/EventLogBackedAuditEventReader.cs` layered over the kernel `IEventLog` substrate (same path `EventLogBackedAuditTrail` uses).
- [ ] **Step 4 — DI extensions.** Amend `packages/kernel-audit/DependencyInjection/ServiceCollectionExtensions.cs` with `AddSunfishKernelAuditReader()` (production) + `AddSunfishKernelAuditReaderInMemory()` (test/dev).
- [ ] **Step 5 — Tests.** Add `packages/kernel-audit/tests/AuditEventReaderTests.cs` covering the 9 test cases enumerated in §Compatibility plan above.
- [ ] **Step 6 — Cohort-4 Engineer PR 0 consumption.** Engineer PR 0 (separate PR, cohort-4 W#78) consumes `IAuditEventReader` at the Bridge handler layer. NOT part of this ADR's Step 1-5 scope, but the down-stream verification path.

Step 1-5 land as a single PR (kernel-audit substrate change; modest LOC; one new file per interface + impl + tests). Cohort-4 Engineer PR 0 is the downstream consumer that triggers Step 6.

---

## Open questions

- **Q1 — Bridge-layer cursor serialization format.** The substrate primitive carries `AuditEventCursor` as a structured value-object; the Bridge layer signs + base64-encodes the cursor for HTTP transport. Should the wire format be standardized at the substrate level (e.g., a `CursorEnvelope` record with `Version`, `SignedPayload`, `Signature` fields) or left to the Bridge layer's discretion (per the cohort-4 hand-off §4.2's reference to `IOperationSigner`)? **Disposition:** punt to Bridge layer for cohort-4; revisit if a second consumer (future compliance-search surface) needs to share the wire format.

- **Q2 — `IssuedBy` filter promotion.** ADR 0049's `AuditQuery` carries an `IssuedBy` (PrincipalId) filter for security-review use cases. This ADR's `AuditEventReaderQuery` omits it (cohort-4 doesn't need it). Should it be added to the new shape for parity, or stay omitted until a future security-review surface ADR? **Disposition:** omit for now (YAGNI); add via amendment when demand materializes.

- **Q3 — Marker interface for kernel-tier reads.** ADR 0092's `ITenantScopedRepository<TEntity, TKey>` is block-cluster-scoped. Should a parallel marker (e.g., `IKernelTenantScopedReader<TRecord>`) be introduced to cover `IAuditEventReader` and future kernel-tier read surfaces, so the Step 4a/4b/4c analyzers can extend their scan to kernel-audit? **Disposition:** punt to a future ADR. Cohort-4 doesn't gate on analyzer coverage of kernel-audit; the SPOT-CHECK pattern (sec-eng on shipyard#71 + future cohort-4 PR 0) carries the gap until the analyzer extension lands.

- **Q4 — `StreamAsync` cap at the substrate layer.** Should `StreamAsync` honor a substrate-layer hard cap (e.g., 10M records) or leave the cap entirely to handler-layer enforcement? **Disposition:** leave to handler layer (cohort-4 enforces 10M); the substrate primitive is uncapped to preserve future-extensibility for legitimate bulk-export use cases (regulatory exports, e.g., IRS audit support per Phase 2 commercial scope).

---

## Revisit triggers

- **Audit-of-audit emission lands.** If/when SOC 2 Type II evidence pack or a regulatory requirement adds "audit who read the audit log," `IAuditEventReader` SHOULD revisit emission semantics on `ListAsync`/`GetByIdAsync`. Likely shape: opt-in per-call audit emission via a `RecordAccess: true` query field; default off to preserve cohort-4's no-emission posture.

- **PII tagging in ADR 0049.** If/when ADR 0049 amends to add a `[Pii]` tagging convention for audit-payload fields, `IAuditEventReader` SHOULD revisit redaction semantics. Likely shape: a `RedactionPolicy` parameter on `GetByIdAsync` and `ListAsync` that the substrate uses to mask sensitive fields before returning records to the caller.

- **Super-admin tenant-spanning audit surface (V2 #6 C2 candidate).** When the super-admin C2 cohort activates, `IAuditEventReader`'s tenant-scoping discipline (every method takes `TenantId` first-positional) may need a sibling contract (`ICrossTenantAuditEventReader` or similar) that explicitly carries `TenantId.System` semantics + super-admin authorization gates. Likely shape: a new ADR introduces the sibling; this ADR's tenant-scoped reader remains the default.

- **EF Core / SQL persistence for audit.** When/if audit persistence migrates from kernel `IEventLog` to a SQL surface (future ADR 0076 or amendment to ADR 0049), `EventLogBackedAuditEventReader` is replaced by `EfCoreAuditEventReader`; the contract `IAuditEventReader` is unchanged. The migration is implementation-only.

- **Compliance-search surface materializes.** Free-text or structured-payload search across audit records (deferred per §"Filter API design — out of scope") would extend `IAuditEventReader` with a `SearchAsync` method or warrant a sibling `IAuditEventSearcher` interface. Either path is additive; this ADR is unaffected until the demand surfaces.

---

## References

### Predecessor and sister ADRs

- [ADR 0049](./0049-audit-trail-substrate.md) — Audit-trail substrate (write side; `IAuditTrail`); this ADR is the read-side counterpart.
- [ADR 0091](./0091-itenantcontext-divergence-resolution.md) — `Foundation.MultiTenancy.ITenantContext` tenant-resolution primitive.
- [ADR 0092](./0092-substrate-tenant-keyed-repository-contract.md) — Substrate tenant-keyed repository contract pattern; this ADR inherits §Q1 (EXPLICIT first-positional `TenantId`), §A3 (uniform-empty cross-tenant), §A6 (audit-emission on tenant-boundary violations), §C5 (Scoped DI lifetime).
- [ADR 0046](./0046-foundation-recovery.md) — `Foundation.Recovery` Phase 1; sub-pattern #48f audit-trail emission shape.
- [ADR 0069](./0069-adr-authoring-discipline.md) — ADR authoring discipline (pre-merge council + §A0 + three-direction-review).

### Roadmap and specifications

- Cohort-4 C3 Audit-Trail Viewer Stage-06 hand-off (PR shipyard#81) — §1.3 scope, §4 Engineer PR 0 (consumes `IAuditEventReader`), §9 Halt H2 (resolved by this ADR).
- V2 #6 cohort-4 scope survey (PR shipyard#74) — C3 anchor recommendation + 18/21 candidate-matrix rank.
- V2 #3 audit-emission Bridge retrofit research (PR shipyard#71) — net-architect verdict provides the class-private helper pattern this ADR adopts for `InMemoryAuditEventReader.EmitTenantBoundaryViolationAsync`.

### Council verdicts (advisory; informs this ADR's pattern choices)

- `coordination/inbox/council-verdict-2026-05-21T1229Z-net-architect-shipyard-71-bridge-audit-emitter.md` — class-private helper pattern (NOT shared emitter class).
- `coordination/inbox/council-verdict-2026-05-21T1228Z-security-engineering-shipyard-69-ignore-query-filters-severity.md` — 9-layer defense-in-depth model.

### Existing code / substrates

- `packages/kernel-audit/IAuditTrail.cs:39` — write-side substrate primitive (this ADR's read-side counterpart).
- `packages/kernel-audit/AuditRecord.cs:49` — record shape (consumed unchanged by `IAuditEventReader`).
- `packages/kernel-audit/AuditQuery.cs:21` — kernel-internal stream filter (retained alongside the new read-side `AuditEventReaderQuery`).
- `packages/kernel-audit/InMemoryAuditTrail.cs:37` — in-memory writer (shared store with new `InMemoryAuditEventReader`).
- `packages/kernel-audit/EventLogBackedAuditTrail.cs` — production writer (parallel shape to new `EventLogBackedAuditEventReader`).
- `packages/blocks-financial-payments/Services/InMemoryPaymentRepository.cs:15` + `:135` — canonical class-private `EmitTenantBoundaryViolationAsync` helper pattern (referenced by net-architect verdict on shipyard#71).
- `packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs:33` + `:210` — canonical parameterized-`entityType` class-private emitter pattern.
- `packages/foundation-persistence/ITenantScopedRepository.cs:79` — ADR 0092 marker (this ADR does NOT claim it, but inherits the discipline).
- `packages/foundation-multitenancy/ITenantContext.cs:8` — ADR 0091 tenant-resolution primitive (upstream of every `TenantId` value the reader receives).

### External

- CQRS (Command Query Responsibility Segregation) — read-write separation pattern. Greg Young's introduction (`https://cqrs.files.wordpress.com/2010/11/cqrs_documents.pdf`). This ADR adopts the lightweight interface-segregation variant; no separate datastore.

---

## Pre-acceptance audit (5-minute self-check)

> **D1 — substrate-tier ADRs:** Do NOT enable auto-merge before pre-merge council returns a verdict. Set PR description to "Awaiting dual-council SPOT-CHECK per ADR 0069 + Admiral instruction." Dispatch Opus + xhigh council subagents (both `.NET-architect` AND `security-engineering`) with explicit structural pressure-test points. Apply amendments; then enable auto-merge.

- [x] **AHA pass.** Three options considered (Option A extend-existing, Option B new-interface, Option C marker-on-same-impl). Each has explicit verdict + rationale. Option B is the cleanest substrate-canonical choice.
- [x] **FAILED conditions / kill triggers.** Reverse this ADR if: (a) dual council returns BLOCKER on §"Decision" pattern choice; (b) cohort-4 Engineer PR 0 SPOT-CHECK surfaces a substrate-design flaw that the reader cannot accommodate (e.g., a cross-tenant uniform-empty path turns out to be insufficient and a separate authorization-error path is needed). Revisit triggers also flag long-term reversal conditions (audit-of-audit; PII tagging; super-admin surface).
- [x] **Rollback strategy.** Reader is purely additive. If rejected post-Step-1 ship, revert the kernel-audit PR (one file delete for interface; one file delete each for two reference impls; one DI extension method removed) — `IAuditTrail` unchanged; existing consumers unaffected. Cohort-4 Engineer PR 0 then falls back to growing inline filters over `IAuditTrail.QueryAsync` (the path the hand-off H2 originally tried to punt).
- [x] **Confidence level.** MEDIUM-HIGH. The substrate design is straightforward CQRS read-write separation; the pattern is well-established and inherits proven substrate norms (ADR 0091 + ADR 0092). The novel surface is the cursor-tenant-binding mechanism, which the cohort-4 hand-off Adversarial Brief Decisions 2 + 5 already pressure-tested; council attestation will verify those pressure-tests against the substrate-level value object. Confidence drops to MEDIUM if Q3 (kernel-tier marker for analyzer coverage) becomes a dual-council BLOCKER — but the disposition (punt to future ADR) is defensible.
- [x] **Cited-symbol verification.** §A0 enumerates 24 cited references; 19 verified Existing (paths + line numbers cited where applicable), 5 explicitly marked "Introduced by this ADR" + tracked in Implementation checklist Step 1-5. Per the cited-symbol verification helper at the bottom of the ADR template, ran `grep -oE "Sunfish\.[A-Z][A-Za-z0-9.]+"` over the draft and verified every short name exists in `packages/` (existing) or is explicitly named as introduced.
- [x] **Anti-pattern scan.** Glanced at the 21-AP list in `.claude/rules/universal-planning.md`:
  - AP-1 (unvalidated assumptions): caught — assumptions are pinned in §Context (`IAuditTrail.QueryAsync` shape mismatch is the load-bearing claim; verified by reading `IAuditTrail.cs` + `AuditQuery.cs`).
  - AP-3 (vague success criteria): caught — §Compatibility plan + §Implementation checklist define measurable Step 1-6 outcomes.
  - AP-9 (skipping Stage 0): three options considered; cohort-4 hand-off H2 is the discovery prompt.
  - AP-10 (first-idea unchallenged): Option A (extend `IAuditTrail`) was the initial-shape obvious-choice; rejected on interface-segregation grounds in favor of Option B.
  - AP-12 (timeline fantasy): no time estimates beyond "modest LOC; small PR"; the cohort-4 hand-off itself carries time estimates downstream.
  - AP-21 (assumed facts without sources): every load-bearing claim has either an ADR cite, a file:line cite, or a council-verdict beacon cite.
- [x] **Revisit triggers.** Named 5 triggers in §"Revisit triggers" (audit-of-audit; PII tagging; super-admin surface; EF Core / SQL persistence; compliance-search).
- [x] **Cold Start Test.** A fresh contributor reading this ADR + the cohort-4 hand-off can execute Step 1-5 implementation without further author clarification — the contract surface code-block in §Decision is the spec; the reference implementations described in §"Reference implementations (Step 2)" provide the shape; the test surface in §Compatibility plan enumerates the test cases.
- [x] **Sources cited.** Every load-bearing claim cites either an ADR (0049 / 0091 / 0092 / 0046 / 0069), a file:line (e.g., `IAuditTrail.cs:39`), a council verdict (shipyard#71 + shipyard#69), or a hand-off doc (cohort-4 Stage-06 hand-off).

### Cited-symbol verification helper (run results)

```bash
ADR=docs/adrs/0094-i-audit-event-reader.md
grep -oE "Sunfish\.[A-Z][A-Za-z0-9.]+" "$ADR" | sort -u
```

Run by author 2026-05-21. All `Sunfish.*` short-names found in `packages/` (existing) or explicitly introduced by this ADR per §A0 audit. No MISSING entries.

---

*This ADR enforces the lightweight Universal Planning Framework checks per `.claude/rules/universal-planning.md` and the ADR authoring discipline in ADR 0069 (pre-merge council + §A0 + three-direction). Status will move to `Accepted` after both `.NET-architect` and `security-engineering` councils return GREEN-attested verdicts.*
