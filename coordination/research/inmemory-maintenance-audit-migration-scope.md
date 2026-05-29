# InMemoryMaintenanceService audit-payload migration — Engineer scope

**Authored by:** ONR (V11 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-22T17-15Z` item V11 #3)
**Authored at:** 2026-05-22T17-40Z
**Target audience:** Engineer V4 follow-on pickup
**Source finding:** V10 #2 audit-payload canonicalization research (shipyard#122)

---

## 1. Summary

Migrate `InMemoryMaintenanceService.EmitTenantBoundaryViolationAsync` from
non-canonical 3-field shape to canonical 5-field shape per ADR 0092 §A6.

**Current shape (3-field, non-canonical):**
```csharp
{ entity_type, entity_id, observed_tenant }
```

**Target shape (5-field, canonical):**
```csharp
{ entity_type, entity_id, requested_tenant, actual_tenant, correlation_id }
```

**Estimated effort:** ~30-45 min Engineer time
**Estimated LOC delta:** ~60-80 (signature + 2 call sites + 1-2 new tests)
**PR sizing:** Small (single-file changes; 1 test file extension)
**Branch suggestion:** `feat/maintenance-substrate-tbv-5-field-canonicalization`

---

## 2. Files affected

| File | Change | LOC delta |
|---|---|---|
| `shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs` | Modify `EmitTenantBoundaryViolationAsync` signature + 2 call sites | ~+30 / -10 |
| `shipyard/packages/blocks-maintenance/tests/MaintenanceTenantGuardsTests.cs` | Extend existing `GetVendorAsync_CrossTenant_EmitsTenantBoundaryViolationAudit` + add canonical-shape assertion test | ~+30 / -2 |
| `shipyard/packages/blocks-maintenance/tests/WorkOrderAuditEmissionTests.cs` | (Optional) Add canonical-shape assertion if WorkOrder path also tested | ~+15 / -0 |

**Total touched files:** 2-3 (single-file production change + 1-2 test extensions).

---

## 3. Production-code changes

### 3.1 Helper signature change

**Current (line 210 of `InMemoryMaintenanceService.cs`):**
```csharp
private async ValueTask EmitTenantBoundaryViolationAsync(
    string entityType,
    string entityId,
    CancellationToken ct)
{
    if (_auditTrail is null || _signer is null) return;

    var observed = CurrentTenantId;
    var payload = new AuditPayload(new Dictionary<string, object?>
    {
        ["entity_type"]      = entityType,
        ["entity_id"]        = entityId,
        ["observed_tenant"]  = observed?.Value ?? "(unresolved)",
    });
    await EmitAsync(AuditEventType.TenantBoundaryViolation, payload, ct).ConfigureAwait(false);
}
```

**Target:**
```csharp
private async ValueTask EmitTenantBoundaryViolationAsync(
    string entityType,
    string entityId,
    TenantId actualTenant,
    CancellationToken ct)
{
    if (_auditTrail is null || _signer is null) return;

    var requestedTenant = CurrentTenantId;
    var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

    var payload = new AuditPayload(new Dictionary<string, object?>
    {
        ["entity_type"]       = entityType,
        ["entity_id"]         = entityId,
        ["requested_tenant"]  = requestedTenant?.Value ?? "(unresolved)",
        ["actual_tenant"]     = actualTenant.Value,
        ["correlation_id"]    = correlationId,
    });
    await EmitAsync(AuditEventType.TenantBoundaryViolation, payload, ct).ConfigureAwait(false);
}
```

**Required additional `using` directive:**
```csharp
using System.Diagnostics;   // for Activity.Current
```

### 3.2 Call site 1 — `GetVendorAsync` (line 318)

**Current:**
```csharp
public async ValueTask<Vendor?> GetVendorAsync(VendorId id, CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    _vendors.TryGetValue(id, out var vendor);

    if (vendor is not null && CurrentTenantId is { } tenant && !vendor.TenantId.Equals(tenant))
    {
        await EmitTenantBoundaryViolationAsync("Vendor", id.Value, ct).ConfigureAwait(false);
        return null;
    }

    return vendor;
}
```

**Target:**
```csharp
public async ValueTask<Vendor?> GetVendorAsync(VendorId id, CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();
    _vendors.TryGetValue(id, out var vendor);

    if (vendor is not null && CurrentTenantId is { } tenant && !vendor.TenantId.Equals(tenant))
    {
        // Capture actualTenant BEFORE returning null (otherwise it's lost)
        await EmitTenantBoundaryViolationAsync("Vendor", id.Value, vendor.TenantId, ct).ConfigureAwait(false);
        return null;
    }

    return vendor;
}
```

### 3.3 Call site 2 — `GetWorkOrderAsync` (line 703)

**Current:**
```csharp
if (workOrder is not null && CurrentTenantId is { } tenant && !workOrder.TenantId.Equals(tenant))
{
    await EmitTenantBoundaryViolationAsync("WorkOrder", id.Value, ct).ConfigureAwait(false);
    return null;
}
```

**Target:**
```csharp
if (workOrder is not null && CurrentTenantId is { } tenant && !workOrder.TenantId.Equals(tenant))
{
    await EmitTenantBoundaryViolationAsync("WorkOrder", id.Value, workOrder.TenantId, ct).ConfigureAwait(false);
    return null;
}
```

---

## 4. Test updates

### 4.1 Extend existing test (`MaintenanceTenantGuardsTests.cs:194-213`)

Existing test asserts emission occurs. Extend to assert canonical 5-field shape:

```csharp
[Fact]
public async Task GetVendorAsync_CrossTenant_EmitsTenantBoundaryViolationAudit_With5FieldCanonicalShape()
{
    var auditTrail = new RecordingAuditTrail();
    var signer = new PassthroughSigner();

    var ctx = new StubTenantContext();
    ctx.Set(TenantA);

    var svc = new InMemoryMaintenanceService(ctx, auditTrail, signer);
    var created = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "v" });

    // Flip to TenantB on the SAME service + audit-trail and probe.
    ctx.Set(TenantB);
    Assert.Null(await svc.GetVendorAsync(created.Id));

    var violation = auditTrail.Records.Single(r =>
        r.EventType.Equals(AuditEventType.TenantBoundaryViolation));

    // V10 #2 / V11 #3 — canonical 5-field shape per ADR 0092 §A6
    Assert.Equal("Vendor", violation.Payload["entity_type"]);
    Assert.Equal(created.Id.Value, violation.Payload["entity_id"]);
    Assert.Equal(TenantB.Value, violation.Payload["requested_tenant"]);   // caller's ctx
    Assert.Equal(TenantA.Value, violation.Payload["actual_tenant"]);      // entity's tenant
    Assert.NotNull(violation.Payload["correlation_id"]);
}
```

### 4.2 Add WorkOrder canonical-shape test

```csharp
[Fact]
public async Task GetWorkOrderAsync_CrossTenant_EmitsTenantBoundaryViolationAudit_With5FieldCanonicalShape()
{
    var auditTrail = new RecordingAuditTrail();
    var signer = new PassthroughSigner();

    var ctx = new StubTenantContext();
    ctx.Set(TenantA);
    var svc = new InMemoryMaintenanceService(ctx, auditTrail, signer);

    // ... create vendor + maintenance request + work order in TenantA ...
    var workOrder = await CreateWorkOrderInTenantAsync(svc);

    ctx.Set(TenantB);
    Assert.Null(await svc.GetWorkOrderAsync(workOrder.Id));

    var violation = auditTrail.Records.Single(r =>
        r.EventType.Equals(AuditEventType.TenantBoundaryViolation));

    Assert.Equal("WorkOrder", violation.Payload["entity_type"]);
    Assert.Equal(workOrder.Id.Value, violation.Payload["entity_id"]);
    Assert.Equal(TenantB.Value, violation.Payload["requested_tenant"]);
    Assert.Equal(TenantA.Value, violation.Payload["actual_tenant"]);
    Assert.NotNull(violation.Payload["correlation_id"]);
}
```

### 4.3 Test for pre-tenant-context (legacy permissive ctor)

Verify the legacy `InMemoryMaintenanceService()` parameterless ctor (which is
permissive — no tenant filtering) does NOT emit when there's no tenant context:

The existing test `ParameterlessCtor_StaysPermissive_NoTenantFiltering` covers
non-emission already. Verify it remains GREEN after migration (no audit record
emitted; no need to assert canonical shape).

---

## 5. Pre-flight verification

Engineer runs before opening PR:

```bash
cd shipyard/packages/blocks-maintenance/
dotnet build  # ensures Activity using directive resolves
dotnet test --filter "Cross"  # runs cross-tenant tests
# Expect: GetVendorAsync_CrossTenant_EmitsTenantBoundaryViolationAudit_With5FieldCanonicalShape PASS
# Expect: GetWorkOrderAsync_CrossTenant_EmitsTenantBoundaryViolationAudit_With5FieldCanonicalShape PASS
# Expect: ParameterlessCtor_StaysPermissive_NoTenantFiltering PASS
```

---

## 6. SPOT-CHECK dispatch matrix

PR Ready-flip:

| Council | Mandatory? | Rationale |
|---|---|---|
| **sec-eng-council** | **MANDATORY** | Check 1 (cross-tenant isolation) + Check 3 (audit emission completeness) — canonical shape conformance is sec-eng's lane |
| .NET-architect | Recommended | Signature change ripples to 2 call sites; minor scope but worth a look |
| frontend-architect | DEFER | No frontend surface |

ONR forward-watches sec-eng-council verdict against V10 #2 / V11 #1 +
ADR 0092 §A6 expected mini-amendment.

---

## 7. PR description acceptance criteria (Engineer authors)

```markdown
## Closes V10 #2 / V11 #3 finding

- [x] Helper signature accepts `TenantId actualTenant` parameter
- [x] Payload emits canonical 5 fields: entity_type, entity_id, requested_tenant, actual_tenant, correlation_id
- [x] Non-canonical `observed_tenant` field removed
- [x] correlation_id sourced from Activity.Current?.Id with Guid.NewGuid() fallback
- [x] GetVendorAsync call site captures vendor.TenantId before returning null
- [x] GetWorkOrderAsync call site captures workOrder.TenantId before returning null
- [x] Existing test extended to assert canonical 5-field shape
- [x] WorkOrder test added with canonical 5-field shape assertion
- [x] ParameterlessCtor_StaysPermissive_NoTenantFiltering still GREEN (regression check)

## ADR 0092 §A6 alignment

If ADR 0092 §A6 mini-amendment (per V10 #2 §4.1 scaffold) has not yet landed when
this PR opens, file as forward-watch in PR description; sec-eng acknowledges.
If amendment has landed, this PR brings InMemoryMaintenanceService into compliance.
```

---

## 8. Halt conditions

- **H1**: ADR 0092 §A6 mini-amendment not yet authored → Engineer may proceed
  with this migration anyway; the canonical shape exists at Bridge layer per
  shipyard#71 verdict (no new ADR amendment strictly required to fix existing
  divergence). Forward-watch in PR description.
- **H2**: Other test files reference `observed_tenant` directly → grep before
  opening PR; update if found.
- **H3**: WorkOrder migration test infrastructure (CreateWorkOrderInTenantAsync
  helper) doesn't exist → adapt from existing WorkOrderAuditEmissionTests setup;
  no new fixture needed.

---

## 9. Forward-watches post-merge

- **Future substrate emitters** that introduce `TenantBoundaryViolation` emission
  MUST use canonical 5-field shape from day 1 (per ADR 0092 §A6 expected
  amendment).
- **Roslyn analyzer extension** (V10 #2 §5 forward-watch + V8 #4 amendment
  proposal) — potential analyzer that verifies all `TenantBoundaryViolation`
  emissions carry canonical fields. Defer until 2nd-instance similar issue
  emerges OR Admiral prioritizes.

---

## 10. Pattern conformance

- pattern-009-tenant-keying-retrofit (formal) — consumed
- pattern-canonical-audit-payload-shape (V10 #2 emergent candidate; 1st instance
  via this migration; V11 #1 referenced pattern-012 audit shape reconciliation)

---

## 11. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **Migration urgency** — fold into V10 #1 Engineer ladder PR sequence (where?),
   OR standalone Engineer V4 follow-on PR? ONR recommends standalone (small;
   independent of ADR 0091/0094 work).
2. **ADR 0092 §A6 amendment timing** — author amendment BEFORE migration PR (so
   PR aligns to canonical doc), OR migrate first and amendment follows? ONR
   recommends amendment-first for clean authorial intent.
3. **Engineer pickup** — assign to Engineer V4 batch? Or wait for natural
   queue-position to surface?

---

## 12. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T17-15Z` item V11 #3
2. V10 #2 audit-payload canonicalization research (shipyard#122) — discovery + recommendation
3. V11 #1 pattern-012 canonical framing research (shipyard#124) — pattern-canonical-audit-payload-shape candidate
4. `shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs:210-222, :318, :703`
5. `shipyard/packages/blocks-maintenance/tests/MaintenanceTenantGuardsTests.cs:194-213`
6. ADR 0092 §A3 + §A6 (uniform-404 + canonical payload)
7. ADR 0049 + ADR 0094 (audit substrate + reader)
8. `signal-bridge/Sunfish.Bridge/Financial/FinancialEndpoints.cs:425-462` — canonical reference impl

---

## 13. What ONR does next

V11 #3 scope deliverable complete. Proceeds to V11 #2 (ADR 0094 Step 1
consultation; ~1-2h).

— ONR, 2026-05-22T17:40Z
