# Audit-payload field count reconciliation (2026-05-21)

**Authored by:** ONR (V6 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-21T15-55Z` item #2)
**Authored at:** 2026-05-21T16-05Z

---

## Scope

V6 #2 directive flags 3 variants of the `TenantBoundaryViolation` audit payload across the fleet:
- ADR 0094 (pending; cites 5-field per cross-referencing context)
- ADR 0092 §A6 (Accepted; cites 4-field per directive observation)
- `InMemoryMaintenanceService.cs:210-222` ships 3-field

Reconcile: what's canonical? Code archaeology + ADR amendment recommendation.

---

## TL;DR

1. **Canonical shape: 5-field** per V2 #3 audit-emission Bridge retrofit research (shipyard#71) and `admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md`.

2. **Three observed variants:**
   - **5-field (canonical):** `entity_type` + `entity_id` + `requested_tenant` + `actual_tenant` + `correlation_id` (V2 #3 retrofit; cohort-2 substrate PR 0a-d precedent; Admiral tracking beacon)
   - **3-field (InMemoryMaintenanceService:210-222):** `entity_type` + `entity_id` + `observed_tenant` — predates V2 #3 retrofit; legacy shape from W#23.3 P1 sec-eng amendment A2 (per pattern-009 catalog line 300)
   - **4-field (ADR 0092 §A6 referenced):** ADR 0092 §A6 doesn't enumerate the fields inline at the consulted section; the 4-field count likely comes from ADR 0092 §A6's referenced sub-section or an in-flight Rev 3 amendment ONR couldn't locate this session

3. **Root cause:** the 5-field canonical was codified in V2 #3 retrofit research AFTER InMemoryMaintenanceService shipped its 3-field implementation (per W#23.3 P1 sec-eng amendment A2 in pattern-009 catalog history). ADR 0092 §A6 was authored BEFORE V2 #3 retrofit; may have lower-count enumeration.

4. **Recommendation: ADR 0092 §A6 mini-amendment to formalize 5-field.** Mechanical edit; ratifies the V2 #3 + Admiral tracking beacon as canonical. Pre-Step-4b enforcement: sec-eng SPOT-CHECK on any PR emitting `TenantBoundaryViolation` verifies 5-field shape.

5. **Engineer migration cost:** ~30 min — update `InMemoryMaintenanceService.cs:210-222` to emit 5-field (add `requested_tenant` + `correlation_id`; rename `observed_tenant` → `actual_tenant`). One-PR change to bring InMemoryMaintenanceService into canonical shape.

---

## 1. The 5-field canonical (per V2 #3 + Admiral tracking)

Per `coordination/inbox/admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md` §"Canonical payload":

```
5-field per substrate convention (PR 0a/b/c/d):
- entity_type (e.g., "Lease", "Payment", "WorkOrder")
- entity_id
- requested_tenant (caller's tenant from ITenantContext)
- actual_tenant (entity's actual tenant)
- correlation_id (Activity.Current?.Id ?? Guid.NewGuid().ToString("N"))
```

This is the cohort-2 substrate PR 0a-d precedent (set by ONR's V2 #3 retrofit research at shipyard#71). All 4 cohort-2 substrate PRs (shipyard#52/57/60/64) emit `TenantBoundaryViolation` with this 5-field shape.

---

## 2. The 3-field legacy variant (InMemoryMaintenanceService:210-222)

```csharp
private async ValueTask EmitTenantBoundaryViolationAsync(string entityType, string entityId, CancellationToken ct)
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

**Code archaeology:** this shipped at PR #38 (blocks-maintenance PR 0 Option D — service-layer tenant guards on InMemoryMaintenanceService; merged 2026-05-19T03:22Z per W#23.3 P1 sec-eng amendment A2 context).

**Missing fields vs canonical:**
- No `requested_tenant` — implied by caller's `ITenantContext` but not explicit in payload
- No `correlation_id` — forensics traceability gap
- `observed_tenant` semantically = `actual_tenant` of the canonical 5-field shape; naming inconsistent

**Migration path:** rename `observed_tenant` → `actual_tenant`; add `requested_tenant` (from `CurrentTenantId` parameter); add `correlation_id` (Activity.Current?.Id). ~30 min Engineer effort.

---

## 3. ADR 0092 §A6 — unverified 4-field claim

Per V6 #2 directive: "ADR 0092 §A6 enumerates 4-field." ONR's reading of ADR 0092 §A6:

- §A6 in the amendment changelog (line 50): "Audit emission at tenant-boundary violations"
- §A6 in the body (line 103): "§Decision gains 'Audit emission at tenant-boundary violations' — `AuditEventType.TenantBoundaryViolation` emission at repository layer"

The text does NOT enumerate the field shape inline at the consulted lines. The 4-field count cited in the V6 #2 directive may come from:
- A nested ADR 0092 §A6 sub-section not visible in the consulted excerpt
- An ADR 0092 Rev 3 amendment in flight (not yet on disk)
- An informal Admiral interpretation between ADRs

**Resolution path:** ONR recommends Admiral verify the ADR 0092 §A6 field-count claim against the canonical source. Two scenarios:
- **Scenario A** — ADR 0092 §A6 has a 4-field enumeration inline; reconcile to 5 via mini-amendment
- **Scenario B** — ADR 0092 §A6 doesn't enumerate; the V6 #2 directive's "4-field" reference is a misread; no amendment needed; just clarify the canonical shape in cerebrum / fleet-conventions

---

## 4. Recommended reconciliation PR scope

**ONR proposes:**

### Mini-amendment to ADR 0092 §A6 (if Scenario A applies)

```
Add explicit 5-field enumeration to ADR 0092 §"Audit emission at tenant-boundary violations" section:

> The canonical TenantBoundaryViolation audit payload SHALL include 5 fields:
> - entity_type (e.g., "Lease", "Payment", "WorkOrder")
> - entity_id
> - requested_tenant (caller's tenant from ITenantContext)
> - actual_tenant (entity's actual tenant)
> - correlation_id (Activity.Current?.Id ?? Guid.NewGuid().ToString("N"))
> 
> Per V2 #3 audit-emission Bridge retrofit research (shipyard#71) +
> admiral-tracking-2026-05-21T08-00Z. All substrate emissions adopt
> this shape.
```

### InMemoryMaintenanceService.cs migration PR

Update `EmitTenantBoundaryViolationAsync` to emit 5-field canonical. ~30 min Engineer + advisory sec-eng review.

### Cerebrum + fleet-conventions update

Add canonical 5-field shape to `feedback_fleet_*.md` or equivalent so future agents have a single source of truth.

---

## 5. Open questions for Admiral routing

1. **ADR 0092 §A6 field-count claim verification** — Scenario A (inline 4-field enumeration) vs Scenario B (no inline enumeration; 4-field is misread)?
2. **Mini-amendment vs Rev 3** — if Scenario A, fold into ADR 0092 Rev 3 (when triggered) vs separate amendment PR now?
3. **InMemoryMaintenanceService.cs migration timing** — bundle into V6 audit-payload reconciliation PR (single sweep) vs separate Engineer follow-on?

---

## 6. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T15-55Z` item #2
2. `coordination/inbox/admiral-tracking-2026-05-21T08-00Z-cross-tenant-audit-emission-bridge-handler-retrofit.md` — canonical 5-field
3. V2 #3 audit-emission Bridge retrofit research (shipyard#71) — canonical 5-field origin
4. shipyard/packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs:205-222 — 3-field legacy variant
5. shipyard/docs/adrs/0092-substrate-tenant-keyed-repository-contract.md §A6 (Accepted) — amendment changelog + body reference
6. PR #38 (blocks-maintenance PR 0 Option D; merged 2026-05-19T03:22Z) — InMemoryMaintenanceService shipping context

---

## 7. What ONR does next

V6 #2 deliverable complete. Files V6-partial-complete idle beacon per Admiral ruling 16:00Z (V6 #2 + #3 + #6 shipped; #1 + #4 + #5 + #7 deferred to V7 dispatch in next session).

— ONR, 2026-05-21T16:05Z
