# ONR research — Audit-emission Bridge-handler retrofit pre-research (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #3)
**Parent tracking:** `admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md`
**Authored by:** ONR
**Authored at:** 2026-05-21T12-10Z
**Status:** draft (ratification pending sec-eng SPOT-CHECK on the retrofit design + helper-vs-inline decision)

---

## Scope of investigation

- **In scope:** design the audit-emission retrofit for three Bridge handler families (Financial + Leases + WorkOrders) per the Admiral tracking beacon `admiral-tracking-2026-05-21T08-00Z`. Specifically: (a) survey the WorkOrdersEndpoint partial impl (success-path emission shipped; failure-path / cross-tenant emission missing); (b) design unified retrofit shape (helper class vs inline per-handler); (c) finalize the canonical 5-field payload mapping per entity type; (d) PR sequencing recommendation.
- **Out of scope:** Engineer's actual implementation PR(s) — this research scaffolds the design; Engineer owns the retrofit per tracking beacon §"Owner + sequencing".
- **Authoritative sources consulted:** `admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md`; `signal-bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs` L210-224 (verified 2026-05-21T12:10Z); ADR 0092 §A6 (TenantBoundaryViolation audit emission spec); sec-eng verdict on signal-bridge#29 (referenced by tracking beacon §"Auto-merge guidance"); ADR 0049 (audit substrate) for IAuditTrail + IOperationSigner.
- **Success looks like:** Engineer opens the retrofit PR(s) using this research's design as scaffold; sec-eng SPOT-CHECK has explicit acceptance criteria to verify against (canonical payload + emission ordering + test coverage).

---

## TL;DR

1. **Three handler families need retrofit:** `FinancialEndpoints` (cohort-2), `LeasesEndpoints` (cohort-1), `WorkOrdersEndpoint` (cohort-1). All three have uniform-404 invariant (correct) but lack `AuditEventType.TenantBoundaryViolation` emission (silent at audit).

2. **WorkOrdersEndpoint already has SUCCESS-path emission** at L210-224 (`WorkOrderCreated` event after CreateWorkOrderAsync). It does NOT have FAILURE-path emission for cross-tenant probes. The "partial impl" sec-eng referenced is the success path; retrofit adds the failure path.

3. **Canonical 5-field payload (per tracking beacon):**
   - `entity_type` (e.g., "Lease", "Payment", "WorkOrder")
   - `entity_id`
   - `requested_tenant` (caller's tenant from `ITenantContext`)
   - `actual_tenant` (entity's actual tenant)
   - `correlation_id` (`Activity.Current?.Id ?? Guid.NewGuid().ToString("N")`)

4. **ONR recommends shared `BridgeAuditEmitter` helper** in `signal-bridge/Sunfish.Bridge/Authorization/` (new file). Single helper method `EmitTenantBoundaryViolationAsync` accepts all 5 fields + `IAuditTrail` + `IOperationSigner` from DI. All three handler families call into it. Single retrofit PR (Engineer's choice per tracking beacon — but single-PR is the lower-friction option for a mechanical pattern).

5. **PR sequencing recommendation:** single PR covering all 3 handler families. Estimated ~1-2h Engineer effort. Sec-eng SPOT-CHECK MANDATORY on the retrofit PR (verifies emission matches substrate convention).

6. **Test coverage shape:** integration test per handler family asserting that cross-tenant probe (a) returns uniform-404 AND (b) emits `TenantBoundaryViolation` audit event with correct 5-field payload AND (c) populates AttestingSignatures from `IOperationSigner`.

7. **Open question for sec-eng council:** should `actual_tenant` field be REDACTED in cases where the audit log itself crosses tenant boundaries? Current pattern emits the actual tenant verbatim (forensics value); but a tenant-A admin viewing audit logs would see tenant-B's tenant-id appear, which is itself a (mild) information disclosure. Mitigation: audit logs are tenant-scoped at the substrate layer, so tenant-A's audit query never returns tenant-B's TenantBoundaryViolation events. Confirm this assumption holds; if not, redaction needed.

---

## 1. Current state — WorkOrdersEndpoint partial impl + tracking beacon precedent

### 1.1 WorkOrdersEndpoint L210-224 — SUCCESS-path emission (verified 2026-05-21T12:10Z)

```csharp
var occurredAt = DateTimeOffset.UtcNow;
var signed = await signer.SignAsync(
    new AuditPayload(new Dictionary<string, object?>
    {
        ["work_order_id"]  = wo.Id.Value,
        ["initial_status"] = wo.Status.ToString(),
    }),
    occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
await auditTrail.AppendAsync(new AuditRecord(
    AuditId:             Guid.NewGuid(),
    TenantId:            new TenantId(tenantContext.TenantId),
    EventType:           AuditEventType.WorkOrderCreated,
    OccurredAt:          occurredAt,
    Payload:             signed,
    AttestingSignatures: ImmutableArray<AttestingSignature>.Empty), ct).ConfigureAwait(false);
```

**Pattern observations:**
- `IOperationSigner.SignAsync(payload, occurredAt, Guid, ct)` returns `signed` payload
- `IAuditTrail.AppendAsync(AuditRecord(...))` persists the record
- Required AuditRecord fields: `AuditId, TenantId, EventType, OccurredAt, Payload (signed), AttestingSignatures`
- AttestingSignatures is `ImmutableArray<AttestingSignature>.Empty` for single-signer flow

**This is the success path.** Cross-tenant probe path doesn't emit anything today — it returns BadRequest before reaching this code.

### 1.2 ADR 0092 §A6 — repository-layer TenantBoundaryViolation emission

Per ADR 0092 §A6 (canonical):

> §Decision gains "Audit emission at tenant-boundary violations" — `AuditEventType.TenantBoundaryViolation` emission at repository layer

But the repository layer never fires for these Bridge handler probes because the Bridge short-circuits with `BadRequest` BEFORE reaching `IPaymentRepository.AddAsync` / `ILeaseRepository.GetAsync` / `IMaintenanceService.GetWorkOrderAsync`.

**Implication:** Bridge handler must emit at the BOUNDARY where the short-circuit occurs (where it detects cross-tenant — typically by loading the entity and checking `entity.TenantId != tenantContext.TenantId`).

### 1.3 Three handler families needing retrofit

Per tracking beacon §"What retrofit will cover":

| Handler family | File | Cross-tenant detection sites |
|---|---|---|
| `FinancialEndpoints` | `signal-bridge/Sunfish.Bridge/Financial/FinancialEndpoints.cs` | `HandleListPaymentsAsync` L198-207 (lease lookup); `HandleRecordPaymentAsync` L314-323 (lease lookup) |
| `LeasesEndpoints` | `signal-bridge/Sunfish.Bridge/Leases/LeasesEndpoints.cs` | All cross-tenant lookups (per tracking beacon — exact line refs not specified) |
| `WorkOrdersEndpoint` | `signal-bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs` | All cross-tenant lookups |

---

## 2. Proposed retrofit design

### 2.1 `BridgeAuditEmitter` helper (ONR's recommended shape)

New file: `signal-bridge/Sunfish.Bridge/Authorization/BridgeAuditEmitter.cs`

```csharp
using System.Collections.Immutable;
using System.Diagnostics;
using Sunfish.Foundation.Authorization;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// Shared audit-emission helper for Bridge handler families. Emits
/// <see cref="AuditEventType.TenantBoundaryViolation"/> with the canonical
/// 5-field payload per ADR 0092 §A6 + `admiral-tracking-2026-05-21T08-00Z`
/// when a Bridge handler detects a cross-tenant probe.
/// </summary>
public sealed class BridgeAuditEmitter
{
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;

    public BridgeAuditEmitter(IAuditTrail auditTrail, IOperationSigner signer)
    {
        _auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }

    /// <summary>
    /// Emits a TenantBoundaryViolation audit record. Caller invokes after
    /// detecting a cross-tenant probe and BEFORE returning BadRequest/404
    /// to the caller (to preserve audit trail even if the response stream
    /// is interrupted).
    /// </summary>
    public async Task EmitTenantBoundaryViolationAsync(
        string entityType,
        string entityId,
        TenantId requestedTenant,
        TenantId actualTenant,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["entity_type"]      = entityType,
            ["entity_id"]        = entityId,
            ["requested_tenant"] = requestedTenant.Value,
            ["actual_tenant"]    = actualTenant.Value,
            ["correlation_id"]   = correlationId,
        });

        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);

        await _auditTrail.AppendAsync(new AuditRecord(
            AuditId:             Guid.NewGuid(),
            TenantId:            new TenantId(tenantContext.TenantId),
            EventType:           AuditEventType.TenantBoundaryViolation,
            OccurredAt:          occurredAt,
            Payload:             signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty), ct).ConfigureAwait(false);
    }
}
```

### 2.2 DI registration

In `signal-bridge/Sunfish.Bridge/Program.cs` (or appropriate DI extension):

```csharp
services.AddScoped<BridgeAuditEmitter>();
```

(Scoped because it depends on `IAuditTrail` + `IOperationSigner` which are typically Scoped per request.)

### 2.3 Per-handler integration

Each Bridge handler that detects cross-tenant probe gains a `BridgeAuditEmitter emitter` parameter (via DI) and calls into it:

**FinancialEndpoints.HandleListPaymentsAsync** (cross-tenant lease lookup):

```csharp
internal static async Task<Results<Ok<PaymentListDto>, BadRequest<ProblemDetails>>>
  HandleListPaymentsAsync(
      [FromQuery] string leaseId,
      ITenantContext tenantContext,
      ILeaseService leases,
      IPaymentService payments,
      BridgeAuditEmitter emitter,    // NEW
      CancellationToken ct)
{
    // ... parse leaseId ...
    var lease = await leases.GetByIdAsync(LeaseId.From(leaseId), ct);
    if (lease is null)
    {
        // Lease doesn't exist in any tenant — return uniform 404 (no audit; nothing happened)
        return TypedResults.BadRequest(...);
    }
    if (lease.TenantId != new TenantId(tenantContext.TenantId))
    {
        // Cross-tenant probe — emit audit BEFORE returning uniform 404
        await emitter.EmitTenantBoundaryViolationAsync(
            entityType: "Lease",
            entityId: lease.Id.Value,
            requestedTenant: new TenantId(tenantContext.TenantId),
            actualTenant: lease.TenantId,
            tenantContext: tenantContext,
            ct);
        return TypedResults.BadRequest(...);   // uniform 404 shape
    }
    // ... continue with payments fetch ...
}
```

Same pattern for `HandleRecordPaymentAsync` (cross-tenant lease lookup before POST) + LeasesEndpoints + WorkOrdersEndpoint.

### 2.4 Per-handler entity_type values

Canonical entity_type mapping:

| Handler family | entity_type value(s) | Notes |
|---|---|---|
| `FinancialEndpoints` | `"Lease"` (cross-tenant lease probe); `"Payment"` (cross-tenant payment probe — future expansion if endpoints support direct payment-id access) | Lease is the primary key; payment is downstream |
| `LeasesEndpoints` | `"Lease"` (cross-tenant lease-id probe) | Plus future: `"Party"`, `"PropertyUnit"` if endpoints support direct access |
| `WorkOrdersEndpoint` | `"WorkOrder"` (cross-tenant work-order probe); `"Vendor"` (cross-tenant vendor probe); `"Property"` (cross-tenant property probe) | Multiple lookup points |

---

## 3. Helper vs per-handler-inline tradeoff

### 3.1 Helper class (ONR recommended)

**Pro:**
- Single change-point if canonical payload shape evolves (e.g., 6th field added)
- Easier unit test (mock `BridgeAuditEmitter`; assert it was called with right args)
- DRY across 3 handler families
- Mirrors `AddSunfishTenantContext<TConcrete>` DI helper precedent (one helper for cross-cluster pattern)

**Con:**
- New file + DI registration step
- One more indirection during review (reviewer must look at helper to verify emission shape)

### 3.2 Per-handler inline emission

**Pro:**
- Each handler is self-contained; reviewer reads one file
- No DI registration needed (just inject IAuditTrail + IOperationSigner directly)

**Con:**
- 3 copies of the emission code (drift risk; one handler may miss a field if payload evolves)
- Tests must mock IAuditTrail + IOperationSigner in 3 places
- If canonical payload changes, 3 files to update

### 3.3 ONR recommendation

**Helper class.** The DRY benefit outweighs the extra file. The substrate pattern (one shared helper) matches the canonical Bridge pattern (e.g., `AuthenticatedTenantPolicy`, `DemoTenantContext`, etc. — all single-file helpers shared across Bridge).

Open question 2 (sec-eng council) confirms or amends.

---

## 4. PR sequencing — single PR vs three small

### 4.1 Single PR (ONR recommended)

Scope: `BridgeAuditEmitter` helper + DI registration + 3 handler files updated + tests.

- ~1-2h Engineer effort per tracking beacon estimate
- One review cycle; one sec-eng SPOT-CHECK
- Lower context-switch cost for Engineer
- Faster merge → faster forensics visibility

### 4.2 Three small PRs

Scope: helper + DI in PR 1; FinancialEndpoints retrofit in PR 2; Leases + WorkOrders retrofit in PR 3.

- Lower per-PR review burden
- Easier per-PR revert (if Leases retrofit has a bug, revert PR 3 only)
- Three review cycles; three sec-eng SPOT-CHECKs

### 4.3 ONR recommendation

**Single PR.** ~1-2h is a comfortable single-PR envelope; the helper + handler integrations are tightly coupled (helper is useless without consumers); revert risk is low (mechanical pattern; consistent with existing WorkOrdersEndpoint success-path emission).

Per tracking beacon §"Owner + sequencing": "Single PR covering all 3 Bridge handler families OR three small PRs (Engineer's call)." Engineer makes the final call; ONR's research recommends single PR.

---

## 5. Test coverage strategy

### 5.1 Per-handler integration test

Each handler family's existing integration test project (`signal-bridge/Sunfish.Bridge.Tests.Integration/Financial/FinancialEndpointsTests.cs` etc.) gains:

```csharp
[Fact]
public async Task HandleListPayments_CrossTenantLease_EmitsTenantBoundaryViolation()
{
    // ARRANGE — tenant A's context; lease belongs to tenant B
    var tenantA = new TenantId("tenant-a");
    var tenantB = new TenantId("tenant-b");
    var leaseInB = await SeedLeaseForTenantAsync(tenantB);
    var auditTrail = new RecordingAuditTrail();
    var emitter = new BridgeAuditEmitter(auditTrail, new TestOperationSigner());
    
    // ACT — tenant A queries for tenant B's lease
    var result = await CallEndpointAsTenantAsync(tenantA, leaseInB.Id);
    
    // ASSERT — uniform 404 returned
    Assert.IsType<BadRequest<ProblemDetails>>(result);
    
    // ASSERT — audit event emitted with correct payload
    var event = Assert.Single(auditTrail.RecordedEvents);
    Assert.Equal(AuditEventType.TenantBoundaryViolation, event.EventType);
    Assert.Equal(tenantA.Value, event.TenantId.Value);    // emitted under caller's tenant
    var payloadDict = ExtractPayloadDictionary(event.Payload);
    Assert.Equal("Lease", payloadDict["entity_type"]);
    Assert.Equal(leaseInB.Id.Value, payloadDict["entity_id"]);
    Assert.Equal(tenantA.Value, payloadDict["requested_tenant"]);
    Assert.Equal(tenantB.Value, payloadDict["actual_tenant"]);
    Assert.NotNull(payloadDict["correlation_id"]);
}
```

### 5.2 Negative tests

- `HandleListPayments_SameTenantLease_DoesNotEmitViolation` (success path → no TenantBoundaryViolation event)
- `HandleListPayments_NonexistentLease_DoesNotEmitViolation` (entity doesn't exist anywhere → uniform 404 + no audit; nothing crossed)

### 5.3 Test count summary

| Handler family | New tests |
|---|---|
| FinancialEndpoints | 2 (List + RecordPayment) cross-tenant + 1 same-tenant + 1 nonexistent = 4 |
| LeasesEndpoints | ~3-5 (depends on number of cross-tenant lookup points) |
| WorkOrdersEndpoint | ~3-5 |
| **Total** | **~10-14 new tests** |

---

## 6. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **`BridgeAuditEmitter` helper vs per-handler-inline emission?** ONR recommends helper; ~50 LOC + 1 DI registration vs ~30 LOC × 3 handlers = ~90 LOC inline.
2. **DI lifetime for `BridgeAuditEmitter`?** ONR recommends Scoped (matches IAuditTrail + IOperationSigner lifetimes). Confirm.
3. **PR sequencing — single PR (ONR recommended) vs three small?** Engineer's call per tracking beacon; ONR recommends single.

### For security-engineering council

1. **`actual_tenant` field redaction — emit verbatim (ONR recommended; forensics value) vs redact (caller-tenant-only visibility)?** Audit logs are tenant-scoped at substrate layer; tenant-A's audit query never returns tenant-B's events; verbatim should be safe. Confirm.
2. **Emission ordering — emit BEFORE returning BadRequest (ONR recommended; preserves audit even if response interrupted) vs emit AFTER?** Latency impact: ~ms-scale audit append; acceptable on the cross-tenant-probe slow path.
3. **`AuditEventType.TenantBoundaryViolation` constant — already exists per ADR 0092 §A6 + cohort-2 PR 0 cluster substrate, OR new addition?** Verify exists; if not, the retrofit PR adds it to `kernel-audit/AuditEventType.cs`.
4. **Correlation ID source — `Activity.Current?.Id ?? Guid.NewGuid().ToString("N")` (ONR recommended; reuses existing tracing context) vs always-new GUID?** Activity.Current ties the audit event to the request trace; preferred for forensics.

---

## 7. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `IAuditTrail.AppendAsync` fails during cross-tenant probe → audit silently lost | Low | High (forensics gap) | Sec-eng test: mock IAuditTrail.AppendAsync to throw; verify handler still returns uniform 404 (no leak) AND logs at ERROR level |
| `IOperationSigner.SignAsync` fails → signed payload null → AuditRecord invalid | Low | Medium (audit dropped) | Same mitigation; assert error logging |
| `Activity.Current?.Id` returns null in non-traced contexts → correlation_id always-Guid | Medium | Low (fallback works) | Acceptable; documented |
| Cross-tenant probe under HIGH frequency (attacker fuzzing) floods audit log | Medium | Medium (storage cost; log noise) | Rate-limit at audit substrate layer (out-of-scope; future workstream) |
| Test fixture timing-races (audit append is async) | Medium | Low (flaky tests) | Use `await` semantics correctly; deterministic recording test double |
| `BridgeAuditEmitter` constructor injection fails at DI resolution if IAuditTrail not registered | Low | High (Bridge crashes at startup) | DI validation in Program.cs startup; verify via integration test |
| Retrofit drifts from canonical payload over time (developers add fields ad-hoc) | Medium | Medium (audit format drift) | Helper class centralizes; canonical payload pinned in xmldoc + tests |

---

## 8. Sources cited

### Primary sources

1. `coordination/inbox/admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md` — parent tracking beacon; canonical 5-field payload spec; retrofit scope.
2. `signal-bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs` L210-224 (verified 2026-05-21T12:10Z) — canonical Bridge audit emission pattern (success path).
3. `shipyard/docs/adrs/0092-substrate-tenant-keyed-repository-contract.md` Rev 2 Accepted — §A6 TenantBoundaryViolation audit emission spec.
4. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #3 — parent V2 directive.

### Secondary sources

5. `coordination/inbox/council-verdict-2026-05-21T0758Z-security-engineering-signal-bridge-29-spot-check.md` (referenced by tracking beacon §"What sec-eng caught") — Path A ruling.
6. ADR 0049 (audit substrate) — `IAuditTrail` + `AuditRecord` + `AuditPayload` + `AttestingSignature` shapes.
7. ADR 0046 (IOperationSigner) — Ed25519 signing for audit payloads.

### Tertiary sources

8. .NET Activity API documentation — `Activity.Current?.Id` for distributed-trace correlation.

---

## 9. What ONR does next

Returns to V2 research queue. Per proceed-continuously discipline:

- Item #3 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-item-3-audit-emission-retrofit-complete.md`.
- Proceed to V2 #4: Pattern-010 3rd-instance design research (~4-6h).

— ONR, 2026-05-21T12:10Z
