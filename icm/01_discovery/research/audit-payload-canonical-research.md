# Audit-payload field-count canonicalization research

**Authored by:** ONR (V10 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-22T16-20Z` item V10 #2; follow-up to V6 #2)
**Authored at:** 2026-05-22T16-50Z

---

## Purpose

V6 #2 surfaced the canonical 5-field TenantBoundaryViolation payload but did not
sweep the substrate-tier emitters. This research:

1. Inventories ALL `TenantBoundaryViolation` emitters across Bridge + substrate layers
2. Identifies non-canonical emitters
3. Recommends ADR 0092 §A6 mini-amendment formalizing the 5-field shape
4. Identifies the migration work to bring non-canonical emitters into compliance

---

## 1. Canonical 5-field shape (per ADR 0092 §A6 + ADR 0094)

Per `.net-architect council verdict on shipyard#71` (referenced inline in
`signal-bridge/Sunfish.Bridge/Financial/FinancialEndpoints.cs:425-433`):

```csharp
var payload = new AuditPayload(new Dictionary<string, object?>
{
    ["entity_type"]       = entityType,
    ["entity_id"]         = entityId,
    ["requested_tenant"]  = requestedTenant.Value,
    ["actual_tenant"]     = actualTenant.Value,
    ["correlation_id"]    = correlationId,
});
```

**Canonical fields:**
1. `entity_type` (string) — type of the entity that was probed (e.g., `"Payment"`)
2. `entity_id` (string) — id of the entity (formatted per entity's canonical id)
3. `requested_tenant` (string; tenant id) — the tenant the caller's `ITenantContext` reported
4. `actual_tenant` (string; tenant id) — the tenant the entity actually belongs to
5. `correlation_id` (string) — `System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("N")`

**Why all 5:**
- `entity_type` + `entity_id` — identify what was probed
- `requested_tenant` + `actual_tenant` — identify the boundary that was crossed
  (without these, the audit record cannot reconstruct the cross-tenant probe class)
- `correlation_id` — link to the originating request for forensic reconstruction

---

## 2. Emitter inventory (cross-fleet sweep)

### 2.1 Bridge-layer emitters (signal-bridge/) — CANONICAL (5-field)

| Emitter | File | Lines | Fields | Status |
|---|---|---|---|---|
| FinancialEndpoints.EmitTenantBoundaryViolation | signal-bridge/Sunfish.Bridge/Financial/FinancialEndpoints.cs | 434-462 | 5 (canonical) | ✓ |
| WorkOrdersEndpoint.EmitTenantBoundaryViolation | signal-bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs | 276+ | 5 (canonical; verified per signal-bridge#31) | ✓ |
| LeasesEndpoints.EmitTenantBoundaryViolation | signal-bridge/Sunfish.Bridge/Leases/LeasesEndpoints.cs | 120+ | 5 (canonical; verified per signal-bridge#31) | ✓ |
| VendorsEndpoint.EmitTenantBoundaryViolation | signal-bridge/Sunfish.Bridge/Cockpit/VendorsEndpoint.cs (per #33 tranche 2) | TBC | 5 (canonical; signal-bridge#33 tranche 2) | ✓ |

**Bridge layer (layer 6 of 9-layer defense): 100% canonical 5-field shape.**

### 2.2 Substrate-layer emitters (shipyard/packages/) — INCONSISTENT

| Emitter | File | Lines | Fields | Status |
|---|---|---|---|---|
| InMemoryPaymentRepository.EmitTenantBoundaryViolation | shipyard/packages/blocks-financial-payments/Services/InMemoryPaymentRepository.cs | 135-160 | 5 (canonical) | ✓ |
| InMemoryPaymentApplicationRepository.EmitTenantBoundaryViolation | shipyard/packages/blocks-financial-payments/Services/InMemoryPaymentApplicationRepository.cs | 123-145 | 5 (canonical) | ✓ |
| InMemoryJournalStore.EmitTenantBoundaryViolation | shipyard/packages/blocks-financial-ledger/Services/IJournalStore.cs | 137-160 | 5 (canonical) | ✓ |
| **InMemoryMaintenanceService.EmitTenantBoundaryViolation** | **shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs** | **210-222** | **3 (NON-CANONICAL)** | **✗** |

**Non-canonical emitter detail (line 210-222):**

```csharp
private async ValueTask EmitTenantBoundaryViolationAsync(string entityType, string entityId, CancellationToken ct)
{
    if (_auditTrail is null || _signer is null) return;

    var observed = CurrentTenantId;
    var payload = new AuditPayload(new Dictionary<string, object?>
    {
        ["entity_type"]      = entityType,
        ["entity_id"]        = entityId,
        ["observed_tenant"]  = observed?.Value ?? "(unresolved)",   // ← NON-CANONICAL field name
    });
    await EmitAsync(AuditEventType.TenantBoundaryViolation, payload, ct).ConfigureAwait(false);
}
```

**Deltas vs canonical:**
- **MISSING**: `requested_tenant` (the tenant the caller's `ITenantContext` reported)
- **MISSING**: `actual_tenant` (the tenant the entity belongs to)
- **MISSING**: `correlation_id` (request correlation)
- **NON-CANONICAL FIELD NAME**: `observed_tenant` — does not map to any canonical field
  - Semantically `observed_tenant` is closest to `requested_tenant` (the tenant the caller is in)
  - But the canonical shape requires BOTH `requested_tenant` AND `actual_tenant` to enable cross-tenant probe analysis

**Why this matters:** A cross-tenant audit record from `InMemoryMaintenanceService`
cannot answer the question "what was the cross-tenant boundary that was crossed?"
because `actual_tenant` is absent. The forensic value is degraded.

### 2.3 Call-site shape (Maintenance signature)

The call-site signature `EmitTenantBoundaryViolationAsync(string entityType, string entityId, CancellationToken ct)` (line 210) does not pass `actual_tenant` — meaning the impl can't recover it. Migration requires call-site changes:

Call site 1 (`InMemoryMaintenanceService.cs:318`):
```csharp
await EmitTenantBoundaryViolationAsync("Vendor", id.Value, ct).ConfigureAwait(false);
```

Call site 2 (`InMemoryMaintenanceService.cs:703`):
```csharp
await EmitTenantBoundaryViolationAsync("WorkOrder", id.Value, ct).ConfigureAwait(false);
```

Both lose access to `actual_tenant`. The fix requires both:
1. The Get* method that detects the cross-tenant case must capture the entity's
   `TenantId` BEFORE returning null
2. Pass it to `EmitTenantBoundaryViolationAsync` as a new `TenantId actualTenant` parameter

### 2.4 Other substrate (untouched / forward-watch)

Other substrate clusters that COULD emit `TenantBoundaryViolation` but currently don't:
- `blocks-leases` substrate — Bridge-side `LeasesEndpoints` emits; substrate-side?
- `blocks-businesscases` substrate — entitlement reads; need audit?
- `foundation-channels` — channel-tenant binding; cross-channel probes?

Per V9 #1 forward-watch + ADR 0092 §A3 uniform-404 invariant, every substrate
Get-by-Id implementation SHOULD emit `TenantBoundaryViolation` on cross-tenant.
Some substrates may not yet implement this — out of scope for V10 #2 (covered by
ADR 0091/0092 Step 7+ forward-watches).

---

## 3. Patterns observed

### 3.1 Pattern conformance gap

Per fleet-cerebrum + V8 #4 ADR 0093 amendments + sec-eng-council Check 1 (cross-tenant isolation):

> **canonical 5-field TenantBoundaryViolation payload at Bridge AND substrate layers**

`InMemoryMaintenanceService` violates this — substrate layer 8 emission shape diverges from layer 6 emission shape.

### 3.2 sec-eng-council Check 1 implication

A sec-eng SPOT-CHECK on any cohort-1 Maintenance work that touches the
`EmitTenantBoundaryViolationAsync` helper SHOULD flag this divergence. Historical
review of cohort-1 PR 3 verdicts: write-path scrutiny was high (per V5 #8
baseline; cohort-1 PR 3 was the 12-item outlier). The shape divergence was likely
introduced before sec-eng-council ratified the canonical 5-field shape in
`.net-architect verdict on shipyard#71`.

### 3.3 Root cause hypothesis

Per the inline comment at FinancialEndpoints.cs:426-432:

> *Class-private helper per net-architect verdict on shipyard#71. Emits
> the canonical 5-field TenantBoundaryViolation payload [...].*

The canonical shape was ratified at shipyard#71 (.net-architect verdict). The
Maintenance substrate emission predates that ratification — it was authored
earlier (cohort-1 W#74) and never retroactively brought into compliance.

---

## 4. Recommended ADR 0092 §A6 mini-amendment

ADR 0092 (substrate tenant-keyed repository) currently has §A6 audit-emission
guidance. ONR recommends a **mini-amendment** formalizing the canonical 5-field
shape at substrate layer 8 (in addition to Bridge layer 6 where it's already
canonical).

### 4.1 Proposed ADR 0092 amendment text (scaffold; Admiral authors final)

```markdown
## §A6 amendment — Canonical TenantBoundaryViolation payload shape (5-field)

**Effective:** YYYY-MM-DD (per Admiral ratification)

Every `AuditEventType.TenantBoundaryViolation` emission MUST carry the canonical
5-field payload regardless of emission layer (Bridge or substrate):

```csharp
var payload = new AuditPayload(new Dictionary<string, object?>
{
    ["entity_type"]       = "<EntityName>",     // e.g., "Payment", "WorkOrder", "Lease"
    ["entity_id"]         = entityId,           // string format per entity's canonical id
    ["requested_tenant"]  = requestedTenant.Value,  // caller's ITenantContext.TenantId
    ["actual_tenant"]     = actualTenant.Value,     // entity's TenantId
    ["correlation_id"]    = correlationId,           // Activity.Current?.Id ?? Guid
});
```

### Rationale

- `requested_tenant` + `actual_tenant` together identify the cross-tenant
  boundary. Either alone is insufficient for forensic reconstruction.
- `correlation_id` links to the originating request. Mandatory for cross-event
  correlation.
- The canonical shape is identical at Bridge layer 6 and substrate layer 8;
  audit-trail consumers (ADR 0094 IAuditEventReader; viewer UI) can render
  uniformly regardless of which layer emitted.

### Migration

Existing substrate emitters non-compliant with the 5-field shape (per V10 #2
inventory):

- `InMemoryMaintenanceService.EmitTenantBoundaryViolationAsync` — currently 3-field
  with non-canonical `observed_tenant`. Migration PR brings emitter to canonical
  shape; call sites updated to pass `actualTenant` parameter.

Non-compliant emitters MUST be migrated within one cohort of this amendment
landing. Sec-eng-council SPOT-CHECK on any PR touching a non-compliant emitter
flags it as AMBER until migration ships.

### Forward-watch

Future substrate emitters (per ADR 0091/0092 Step 7+ consumer audits) MUST
follow this shape from day one. Roslyn analyzer `RequestContextMixingAnalyzer`
(per ADR 0091 Step 4 / shipyard#68 §4) does NOT currently check audit-emission
shape; potential analyzer extension is forward-watched.
```

### 4.2 Migration PR scope (Engineer authors post-amendment ratification)

**Branch suggestion:** `feat/maintenance-substrate-tbv-5-field-canonicalization`
**Estimated effort:** ~2-4h Engineer time
**Estimated LOC:** ~50-100 (signature changes + call-site updates + 2-3 tests)

Files touched:
1. `shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs`
   - Modify `EmitTenantBoundaryViolationAsync` signature: add `TenantId actualTenant` parameter
   - Update payload dict to canonical 5 fields
   - Replace `observed_tenant` → `requested_tenant` + `actual_tenant`
   - Add `correlation_id` from `Activity.Current?.Id ?? Guid.NewGuid().ToString("N")`
   - Update call sites at lines 318 + 703 (capture entity.TenantId before returning null)
2. `shipyard/packages/blocks-maintenance/tests/` — extend WorkOrderAuditEmissionTests + add canonical-shape verification

Test additions:
```csharp
[Fact]
public async Task TenantBoundaryViolation_Payload_Carries_All_Five_Canonical_Fields()
{
    var (svc, trail) = NewServiceCapturing(out _);
    // Setup: WorkOrder owned by Tenant A; call GetWorkOrderAsync as Tenant B
    var workOrder = await NewWorkOrderInTenantA(svc);

    using var _ = ScopeAsTenant("tenant-b");
    await svc.GetWorkOrderAsync(workOrder.Id, default);

    var emission = trail.Records.Single(r => r.EventType == AuditEventType.TenantBoundaryViolation);
    Assert.Equal("WorkOrder", emission.Payload["entity_type"]);
    Assert.Equal(workOrder.Id.Value, emission.Payload["entity_id"]);
    Assert.Equal("tenant-b", emission.Payload["requested_tenant"]);
    Assert.Equal("tenant-a", emission.Payload["actual_tenant"]);
    Assert.NotNull(emission.Payload["correlation_id"]);
}
```

### 4.3 SPOT-CHECK dispatch

Migration PR Ready-flip:
- sec-eng-council (MANDATORY; Check 1 cross-tenant isolation + Check 3 audit emission completeness)
- .NET-architect (recommended; signature change ripples to call sites)

---

## 5. ADR 0092 §A6 mini-amendment — ratification path

Per V7 #7 + V8 #4 precedent (ADR amendments are Admiral/Captain territory; ONR provides scaffold):

1. **Admiral authors** ADR 0092 amendment text using §4.1 scaffold above
2. **shipyard PR** opens with `docs(adrs): ADR 0092 §A6 5-field canonical mini-amendment`
3. **.NET-architect council** SPOT-CHECK on amendment PR
4. **sec-eng-council** SPOT-CHECK on amendment PR (consumer of the shape)
5. **MERGE** → migration PR fires (Engineer; per §4.2 scope)

**Cumulative effort:** ~1 day end-to-end (amendment authoring + migration PR + dual SPOT-CHECK).

---

## 6. Pattern emergence forward-watch

If this 5-field shape becomes the canonical shape for OTHER cross-cluster
audit events (e.g., `AuthenticationFailed`, `CrossClusterMixingDetected`),
formalize as a candidate pattern:

**Candidate pattern: pattern-canonical-audit-payload-shape**
- 5-field minimum: entity_type, entity_id, requested_X, actual_X, correlation_id
- Domain-specific X varies (tenant for TBV; principal for AuthFailed; etc.)
- Applies at every emission layer (Bridge + substrate + future cross-cluster)
- Analyzer-enforceable (future Roslyn extension)

ONR consumes via post-cohort-10 retro per V8 #6 scaffold.

---

## 7. Decisions surfaced (route to Admiral)

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **ADR 0092 §A6 mini-amendment authoring** — Admiral authors? ONR provides
   scaffold per V8 #4 precedent.
2. **Migration PR ownership** — Engineer authors? ONR provides spec; assignment
   to Engineer queue.
3. **Migration urgency** — fold into V10 #1 Engineer ladder (PR #1 Step 3 batch?)
   OR standalone PR? ONR recommends standalone (Maintenance substrate is not in
   ADR 0091 Step 3 scope).
4. **Other-emitter forward-watch** — should ALL future substrate Get-by-Id impls
   audit-emit on cross-tenant? ONR recommends YES per ADR 0092 §A3 + V9 #1 forward-watch.
5. **Roslyn analyzer extension** — future audit-shape analyzer? Defer to V11 or later;
   not urgent.

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T16-20Z` item V10 #2
2. ONR V6 #2 (audit-payload field-count canonicalization initial work) — first surfaced 3-field divergence
3. ADR 0092 R2 §A3 + §A6 (substrate tenant-keyed repository + audit emission)
4. ADR 0094 (IAuditEventReader)
5. `signal-bridge/Sunfish.Bridge/Financial/FinancialEndpoints.cs:425-462` — canonical 5-field reference impl
6. `shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs:210-222` — non-canonical 3-field emitter
7. `.net-architect council verdict on shipyard#71` — ratification of canonical shape
8. signal-bridge#31 + #33 (cross-tenant audit emission retrofit + tranche 2)
9. fleet-conventions §SPOT-CHECK dispatch SLA
10. V8 #4 ADR 0093 Rev 2 scaffolding for Admiral (shipyard#118) — ADR-amendment scaffolding pattern precedent
11. V10 #1 Engineer substrate ladder PR-by-PR specs (shipyard#121)

---

## 9. What ONR does next

V10 #2 deliverable complete. Proceeds to V10 #3 (cohort-3 PR cluster spec
consolidation; ~1-2h light).

— ONR, 2026-05-22T16:50Z
