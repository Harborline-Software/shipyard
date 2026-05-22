# Q3 — Kernel-tier substrate-marker disposition research (2026-05-21)

**Authored by:** ONR (V6 batch item #6)
**Requester:** Admiral (per `admiral-directive-2026-05-21T15-55Z` item #6)
**Authored at:** 2026-05-21T16-00Z
**Status:** research feeding ADR 0094 (drafting in progress per V4 #1+#2 Admiral routing)

---

## Scope

V6 #6 directive references "ADR 0094 §"Decision drivers" + Q3 disposition: kernel-tier reads are marker-free until Q3 resolves. What's Q3?"

**Observation 2026-05-21T16:00Z:** ADR 0094 not yet on disk at `shipyard/docs/adrs/0094-*.md` (Admiral drafting per V4 #1+#2 deferral ruling). Research is FORWARD-WATCHING — informs ADR 0094's drafting rather than downstream of it.

ONR's V2 #1 ADR 0091 Steps 3+4 research (shipyard#68) + V3 #1 cohort-4 hand-off (shipyard#81) + V5 #4 W#60 P4 PR 2 impl spec (shipyard#92) all reference `IAuditEventReader` substrate decisions that ADR 0094 would formalize.

---

## TL;DR

1. **The Q3 question is:** does the kernel-tier (e.g., `kernel-audit`, `IAuditEventReader`) need substrate-marker conformance under ADR 0092 substrate-tier convention (`ITenantScopedRepository<TEntity, TKey>`)?

2. **Marker-free posture (current):** kernel-tier services like `IAuditTrail` (write-side) + `IAuditEventReader` (read-side; pending ADR 0094) don't implement `ITenantScopedRepository` because they're not strictly "repository" surfaces — they're audit-substrate-specific.

3. **Q3 disposition tradeoff:**
   - **PRO marker conformance:** uniform substrate-tier contract; analyzer-enforceable (Step 4a `TenantFilterBypassAnalyzer`); reduces special-case carve-outs
   - **PRO marker-free:** kernel-tier is conceptually distinct from block-cluster repositories; marker conformance may be a syntactic-fit-not-semantic-fit; audit substrate has its own access patterns (append-only write; query-by-correlation_id read) that don't map cleanly to the marker's "read by entity ID" assumption

4. **ONR's read:** Q3 should resolve to **HYBRID — kernel-tier audit substrate stays marker-free but adopts the tenant-keyed parameter convention** (`TenantId tenantId` first-positional on every audit read method, mirroring ADR 0092 §"Decision" §"EXPLICIT (amendment C1)" pattern). Best-of-both: substrate-uniform interface shape without forcing the marker-conformance carve-out.

5. **Evidence/decision triggers for Q3 resolution:**
   - ADR 0094 IAuditEventReader draft shipping (Admiral; pending)
   - Cohort-4 audit-trail viewer Engineer prereq PR 0 (uses `IAuditEventReader`; first concrete consumer)
   - Step 4a analyzer (`TenantFilterBypassAnalyzer`) — does it flag IAuditEventReader implementations? If YES, conformance forced; if NO, marker-free acceptable

6. **If Q3 resolves "kernel-tier reads need substrate-marker conformance":** ADR 0094 `IAuditEventReader` interface extends `ITenantScopedRepository<AuditRecord, Guid>` (the audit_id as the entity key). Migration cost ~1-2h Engineer; mostly mechanical interface extension.

---

## 1. The substrate-tier marker (ADR 0092)

Per ADR 0092 Step 1 (shipped at PR #47):

```csharp
namespace Sunfish.Foundation.Persistence;

public interface ITenantScopedRepository<TEntity, TKey>
    where TEntity : IMustHaveTenant
{
    // Zero members; marker interface
}
```

Step 4a analyzer (`TenantFilterBypassAnalyzer`) flags any class implementing `ITenantScopedRepository<,>` that:
- Doesn't apply HasQueryFilter at the model layer
- OR doesn't apply inline `Where(e => e.TenantId == _capturedTenantId)`
- OR doesn't use the (forthcoming per V2 #2) `.WhereTenant(...)` extension method

Currently kernel-tier services (audit + recovery + other foundation packages) don't adopt this marker.

---

## 2. Kernel-tier characteristics (why marker-free is the current default)

### 2.1 `IAuditTrail` (existing; write-side)

```csharp
public interface IAuditTrail
{
    Task AppendAsync(AuditRecord record, CancellationToken ct);
    // ... write-side only
}
```

- **Not a repository:** doesn't expose `GetAsync(TenantId, AuditId)` style query
- **Tenant scoping:** `AuditRecord.TenantId` is on the entity; `AppendAsync` consumer (Bridge handler) passes the entity with its tenant baked in
- **Marker NOT needed:** write-only; no tenant-filter semantics

### 2.2 `IAuditEventReader` (pending; ADR 0094 read-side)

Inferred shape per V3 #1 cohort-4 hand-off + V5 #4 impl spec:

```csharp
public interface IAuditEventReader
{
    Task<AuditQueryResult> QueryAsync(TenantId tenantId, AuditQuery query, CancellationToken ct);
    Task<AuditRecord?> GetByIdAsync(TenantId tenantId, Guid auditId, CancellationToken ct);
    Task<IAsyncEnumerable<AuditRecord>> QueryStreamAsync(TenantId tenantId, AuditQuery query, CancellationToken ct);
}
```

- **Read-side:** has `GetByIdAsync(TenantId, Guid)` shape that IS marker-conformant
- **Tenant scoping:** explicit `TenantId tenantId` first-positional per V5 #4 + ADR 0092 §"Decision" precedent
- **Marker eligible:** the shape DOES fit `ITenantScopedRepository<AuditRecord, Guid>`

### 2.3 Other kernel-tier candidates

- `kernel-security` — cryptographic primitives; no tenant-keyed reads; marker-free justified
- `kernel-sync` — sync envelope handlers; some tenant-keyed reads; potential marker candidate
- `foundation-recovery` — `ITenantKeyProvider` is tenant-keyed; potential marker candidate

---

## 3. Q3 disposition — three options

### Option A — Kernel-tier stays marker-free (current state)

**Rationale:**
- Conceptual distinction between kernel-tier (substrate) and block-cluster (domain) repositories
- Avoids carve-out cascade (every kernel-tier interface adds `where TEntity : IMustHaveTenant` constraint)
- Audit substrate has its own access patterns that don't fit the marker cleanly

**Cost:**
- Step 4a analyzer special-cases kernel-tier (whitelist OR namespace-based skip)
- Reviewer discipline catches tenant-filter gaps in kernel-tier rather than analyzer

### Option B — Kernel-tier adopts marker conformance

**Rationale:**
- Uniform substrate-tier contract
- Analyzer-enforceable; no carve-out
- Forces consistent tenant-filter pattern across all reads

**Cost:**
- Mechanical interface extension on all kernel-tier reads (~1-2h Engineer per interface)
- `IAuditEventReader` extends `ITenantScopedRepository<AuditRecord, Guid>` — adds the `where TEntity : IMustHaveTenant` constraint check at compile time

### Option C — HYBRID (ONR recommended)

Kernel-tier stays marker-free BUT adopts the **tenant-keyed parameter convention**: every read method has `TenantId tenantId` first-positional (mirroring ADR 0092 §"Decision" §"EXPLICIT (amendment C1)").

**Rationale:**
- Best-of-both: substrate-uniform interface shape (same parameter convention as block-cluster repositories) without forcing marker-conformance carve-out
- Step 4a analyzer reads the parameter convention rather than the marker; broader applicability
- ADR 0094 `IAuditEventReader` interface signature is identical to Option B in practice; only the marker extension is omitted

**Cost:**
- Marker IS NOT applied → carve-out vs Option B; analyzer extension may be needed
- Minimal: parameter convention is already the design per V5 #4

---

## 4. Evidence/decision triggers for Q3 resolution

### Trigger 1 — ADR 0094 draft shipping

When Admiral ships ADR 0094, the IAuditEventReader interface declaration FORCES the Q3 disposition:
- If declared `: ITenantScopedRepository<AuditRecord, Guid>` → Option B
- If declared without marker → Option A or Option C

### Trigger 2 — Cohort-4 Engineer prereq PR 0 shipping

The first concrete `IAuditEventReader` implementation in cohort-4 PR 0 (audit-events Bridge endpoint family per V3 #1) consumes the interface. If Engineer's PR 0 ships without marker, Option A/C is the de facto disposition.

### Trigger 3 — Step 4a analyzer rule design

When Engineer ships ADR 0091 Step 4a TenantFilterBypassAnalyzer (per V3 #3 sequencing), the analyzer's detection logic determines whether kernel-tier interfaces are in scope:
- If analyzer scans by `ITenantScopedRepository<,>` marker → only Option B-conformant kernel-tier classes are checked
- If analyzer scans by tenant-keyed-parameter-convention → both Option B + Option C are checked

---

## 5. ONR recommendation

**Q3 resolves: Option C — HYBRID.** Kernel-tier audit substrate stays marker-free; adopts tenant-keyed parameter convention.

Justification:
- Cleaner conceptual separation (kernel-tier vs block-cluster)
- No carve-out cascade in kernel-tier interfaces
- Step 4a analyzer can be designed to detect tenant-keyed-parameter-convention (broader rule; covers more cases)
- Migration cost is zero (ADR 0094 already shapes IAuditEventReader with tenant-keyed parameters per V5 #4)

---

## 6. Migration if Q3 resolves to Option B (alternative)

If Admiral / sec-eng decides Option B (kernel-tier needs marker conformance):

```csharp
// Option B — IAuditEventReader extends marker
namespace Sunfish.Foundation.Persistence;

public interface IAuditEventReader : ITenantScopedRepository<AuditRecord, Guid>
{
    Task<AuditQueryResult> QueryAsync(TenantId tenantId, AuditQuery query, CancellationToken ct);
    Task<AuditRecord?> GetByIdAsync(TenantId tenantId, Guid auditId, CancellationToken ct);
    Task<IAsyncEnumerable<AuditRecord>> QueryStreamAsync(TenantId tenantId, AuditQuery query, CancellationToken ct);
}

// AuditRecord must implement IMustHaveTenant (already does per ADR 0091)
```

Migration cost: ~30 min Engineer (add `: ITenantScopedRepository<AuditRecord, Guid>` to the interface declaration; ensure `AuditRecord : IMustHaveTenant`).

---

## 7. Open questions for Admiral routing

1. **Q3 disposition — Option C HYBRID (ONR recommended) vs Option B FULL conformance vs Option A current marker-free?**
2. **ADR 0094 draft shipping timing** — Admiral routing per V4 #2 deferral; when does Q3 get formalized?
3. **Step 4a analyzer rule scope** — by-marker (forces Option B) vs by-parameter-convention (Option C compatible)?

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T15-55Z` item #6
2. ADR 0094 — not yet on disk (Admiral drafting per V4 #1+#2 deferral); this research informs the drafting
3. ADR 0092 (Accepted) §"Decision" + amendment C1 (EXPLICIT tenant-keyed parameter convention)
4. ADR 0091 R2 (Accepted) — kernel-tier ITenantContext narrowing
5. V2 #1 ADR 0091 Steps 3+4 research (shipyard#68) — ITenantContext consumer inventory
6. V3 #1 cohort-4 audit-trail viewer hand-off (shipyard#81) — IAuditEventReader first consumer
7. V5 #4 W#60 P4 PR 2 impl spec (shipyard#92) — IIdempotencyKeyStore precedent (kernel-tier marker-free pattern)
8. V5 #5 ADR 0091 Steps 5+6 (shipyard#91) — substrate ladder context

---

## 9. What ONR does next

V6 #6 deliverable complete. Files `onr-status-*-v6-item-6-q3-disposition-complete.md`. Proceeds to V6 #3 (ADR 0091 Step 7+ scoping) per Admiral resequencing; defers heavy items #1 + #7 awaiting Admiral routing on pacing question per `onr-question-2026-05-21T15-58Z-v6-context-budget-and-pacing.md`.

— ONR, 2026-05-21T16:00Z
