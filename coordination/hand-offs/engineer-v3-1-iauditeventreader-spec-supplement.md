# Engineer V3 #1 — IAuditEventReader supplementary spec

**Authored by:** ONR (V12 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-22T18-25Z` item V12 #3; supersedes V10 #1 §4)
**Authored at:** 2026-05-22T18-55Z
**Target audience:** Engineer V3 #1 picking up `shipyard/packages/kernel-audit/`

---

## Status

**Canonical reference for Engineer V3 #1.** Supersedes V10 #1 §4 (shipyard#121
Engineer ladder PR-by-PR specs §4 IAuditEventReader) per V11 #2 (shipyard#127)
ADR 0094 Step 1 Engineer consultation findings.

V10 #1 §4 had 3 divergences from ADR 0094 Accepted text — this supplement
corrects them. Engineer follows THIS doc, NOT shipyard#121 §4.

---

## 1. Canonical signature (per ADR 0094)

```csharp
namespace Sunfish.Kernel.Audit;

using Sunfish.Foundation.Assets.Common;   // TenantId, AuditRecord
using Sunfish.Foundation.Crypto;          // IOperationSigner

public interface IAuditEventReader
{
    /// <summary>
    /// Fetch a single audit record by id, tenant-scoped.
    /// Returns null on not-found OR cross-tenant (uniform-empty per ADR 0092 §A3).
    /// Emits TenantBoundaryViolation on cross-tenant path via IAuditTrail.AppendAsync
    /// (NOT self-emit; write-side substrate; canonical 5-field payload per ADR 0092 §A6).
    /// </summary>
    /// <param name="tenantId">Calling tenant — server-derived from ITenantContext per ADR 0091.</param>
    /// <param name="auditId">Record's stable identifier (Guid).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuditRecord?> GetByIdAsync(
        TenantId tenantId,
        Guid auditId,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch a single page of audit records matching query, tenant-scoped.
    /// Returns empty page on no-match OR cross-tenant.
    /// Does NOT emit per-row TenantBoundaryViolation (ADR 0092 §A6 carve-out).
    /// </summary>
    Task<AuditEventPage> ListAsync(
        TenantId tenantId,
        AuditEventQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Stream audit records matching query, tenant-scoped, lazy (per-batch hydration).
    /// Returns empty enumerable on no-match OR cross-tenant.
    /// Suitable for CSV export and long-result iteration.
    /// </summary>
    IAsyncEnumerable<AuditRecord> StreamAsync(
        TenantId tenantId,
        AuditEventQuery query,
        CancellationToken ct = default);
}

public sealed record AuditEventQuery(
    string? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? CorrelationId = null,
    int PageSize = 50,
    string? Cursor = null);

public sealed record AuditEventPage(
    IReadOnlyList<AuditRecord> Records,
    string? NextCursor);
```

---

## 2. Key invariants (canonical per ADR 0094)

1. **TenantId as FIRST positional parameter** — explicit, not implicit. Caller
   sources via `var tenantId = new TenantId(tenantContext.TenantId);` at Bridge layer.
2. **Uniform-empty cross-tenant** — `GetByIdAsync` null; `ListAsync` empty page;
   `StreamAsync` empty enumerable. NO discriminating error.
3. **Audit emission on `GetByIdAsync` cross-tenant** — emits
   `AuditEventType.TenantBoundaryViolation` via WRITE-SIDE `IAuditTrail` (not
   self-emit). Canonical 5-field payload.
4. **`ListAsync` + `StreamAsync` do NOT emit per-result** — filter at query
   boundary per ADR 0092 §A6.
5. **Cursor opaque + signed** — base64-encoded; signing layer = **Bridge layer**
   (NOT substrate; per V11 #2 §5.4 recommendation; aligns with ADR 0094 recursion
   safety guidance). Substrate receives + returns opaque blob.
6. **PageSize 1..200; default 50** — substrate enforces; throws ArgumentException
   if out of range.
7. **No Severity field** — UI severity-prefix filter is Bridge-layer concern;
   Bridge maps `?severity=Security` to `EventType="Security.*"` prefix match.

---

## 3. Reference implementation — `InMemoryAuditEventReader`

```csharp
public sealed class InMemoryAuditEventReader : IAuditEventReader
{
    private readonly InMemoryAuditTrail _writer;   // shares storage with write-side
    private readonly IAuditTrail _auditEmitter;    // for TBV emission on cross-tenant probe

    public InMemoryAuditEventReader(
        InMemoryAuditTrail writer,
        IAuditTrail auditEmitter)
    {
        _writer = writer;
        _auditEmitter = auditEmitter;
    }

    public async Task<AuditRecord?> GetByIdAsync(
        TenantId tenantId,
        Guid auditId,
        CancellationToken ct = default)
    {
        var record = await _writer.GetAsync(auditId, ct).ConfigureAwait(false);

        if (record is null) return null;

        if (!record.TenantId.Equals(tenantId))
        {
            // Cross-tenant probe — emit canonical 5-field TBV
            await EmitTenantBoundaryViolationAsync(
                entityType: "AuditRecord",
                entityId: auditId.ToString(),
                requestedTenant: tenantId,
                actualTenant: record.TenantId,
                ct: ct).ConfigureAwait(false);
            return null;   // uniform-empty
        }

        return record;
    }

    public Task<AuditEventPage> ListAsync(
        TenantId tenantId,
        AuditEventQuery query,
        CancellationToken ct = default)
    {
        if (query.PageSize < 1 || query.PageSize > 200)
            throw new ArgumentException("PageSize must be 1-200", nameof(query));

        IEnumerable<AuditRecord> rows = _writer.AllRecords
            .Where(r => r.TenantId.Equals(tenantId));   // Layer 8 substrate guard

        if (!string.IsNullOrEmpty(query.EventType))
            rows = rows.Where(r => r.EventType.Equals(new AuditEventType(query.EventType)));
        if (query.From.HasValue)
            rows = rows.Where(r => r.OccurredAt >= query.From.Value);
        if (query.To.HasValue)
            rows = rows.Where(r => r.OccurredAt < query.To.Value);
        if (query.CorrelationId.HasValue)
            rows = rows.Where(r => r.Payload.GetCorrelationId() == query.CorrelationId.Value);

        // Cursor decode (opaque; signed externally by Bridge; substrate trusts blob)
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            var (cursorOccurredAt, cursorAuditId) = DecodeOpaqueCursor(query.Cursor);
            rows = rows.Where(r =>
                r.OccurredAt < cursorOccurredAt ||
                (r.OccurredAt == cursorOccurredAt && r.AuditId.CompareTo(cursorAuditId) < 0));
        }

        var paged = rows
            .OrderByDescending(r => r.OccurredAt)
            .ThenByDescending(r => r.AuditId)
            .Take(query.PageSize + 1)
            .ToList();

        var hasNext = paged.Count > query.PageSize;
        var pageRows = hasNext ? paged.Take(query.PageSize).ToList() : paged;
        var nextCursor = hasNext ? EncodeOpaqueCursor(pageRows.Last()) : null;

        return Task.FromResult(new AuditEventPage(pageRows, nextCursor));
    }

    public async IAsyncEnumerable<AuditRecord> StreamAsync(
        TenantId tenantId,
        AuditEventQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Lazy: iterate ListAsync results; yield per row
        AuditEventPage? page = null;
        string? cursor = query.Cursor;

        do
        {
            ct.ThrowIfCancellationRequested();
            page = await ListAsync(tenantId, query with { Cursor = cursor }, ct).ConfigureAwait(false);
            foreach (var record in page.Records)
                yield return record;
            cursor = page.NextCursor;
        } while (cursor is not null);
    }

    private async Task EmitTenantBoundaryViolationAsync(
        string entityType,
        string entityId,
        TenantId requestedTenant,
        TenantId actualTenant,
        CancellationToken ct)
    {
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["entity_type"]      = entityType,
            ["entity_id"]        = entityId,
            ["requested_tenant"] = requestedTenant.Value,
            ["actual_tenant"]    = actualTenant.Value,
            ["correlation_id"]   = correlationId,
        });
        // Emit via WRITE-side IAuditTrail (not self-emit; avoids recursion per ADR 0094)
        var record = new AuditRecord(
            AuditId:             Guid.NewGuid(),
            TenantId:            requestedTenant,
            EventType:           AuditEventType.TenantBoundaryViolation,
            OccurredAt:          DateTimeOffset.UtcNow,
            Payload:             payload,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditEmitter.AppendAsync(record, ct).ConfigureAwait(false);
    }

    // Opaque cursor encoding — Bridge-layer signs the blob; substrate just (en|de)codes structure
    private static string EncodeOpaqueCursor(AuditRecord r) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{r.OccurredAt:O}|{r.AuditId}"));

    private static (DateTimeOffset, Guid) DecodeOpaqueCursor(string cursor)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var parts = decoded.Split('|');
        if (parts.Length != 2) throw new ArgumentException("Malformed cursor");
        return (DateTimeOffset.Parse(parts[0]), Guid.Parse(parts[1]));
    }
}
```

**Note on cursor signing:** This in-memory impl uses unsigned cursor for simplicity.
The production `EventLogBackedAuditEventReader` + Bridge layer signs externally
via `IOperationSigner.SignAsync`; substrate receives + returns signed-opaque blobs.
Per V11 #2 §5.4 — signing happens at the Bridge layer, not substrate.

---

## 4. DI registration

```csharp
namespace Sunfish.Kernel.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishKernelAudit(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryAuditTrail>();
        services.AddSingleton<IAuditTrail>(sp => sp.GetRequiredService<InMemoryAuditTrail>());
        services.AddSingleton<IAuditEventReader>(sp => new InMemoryAuditEventReader(
            sp.GetRequiredService<InMemoryAuditTrail>(),
            sp.GetRequiredService<IAuditTrail>()));
        return services;
    }
}
```

Lifetime: **Singleton** for in-memory implementations (test + dev); future
`EventLogBackedAuditEventReader` is **Scoped** per-request.

---

## 5. Test coverage (≥12 tests)

| # | Test name | Closes |
|---|---|---|
| 1 | `GetByIdAsync_OwnTenant_ReturnsRecord` | baseline |
| 2 | `GetByIdAsync_NotFound_ReturnsNull` | uniform-empty |
| 3 | `GetByIdAsync_CrossTenant_ReturnsNull_EmitsTenantBoundaryViolation_WithCanonical5FieldPayload` | uniform-404 + V10 #2 canonical |
| 4 | `ListAsync_OwnTenant_NoFilters_ReturnsTenantScopedResults` | baseline |
| 5 | `ListAsync_EventTypeFilter_FiltersByExactType` | baseline |
| 6 | `ListAsync_DateRangeFilter_FiltersByOccurredAt` | baseline |
| 7 | `ListAsync_CorrelationIdFilter_FiltersByPayloadCorrelationId` | baseline |
| 8 | `ListAsync_PageSizeOutOfRange_ThrowsArgumentException` | invariant |
| 9 | `ListAsync_Cursor_ReturnsNextPage` | pagination |
| 10 | `ListAsync_TupleCompareCursor_HandlesSameOccurredAt` | tuple invariant |
| 11 | `ListAsync_CrossTenant_ReturnsEmptyPage_DoesNotEmitTBV` | uniform-empty + carve-out |
| 12 | `StreamAsync_OwnTenant_StreamsAllPages` | streaming |
| 13 | `StreamAsync_LazyIteration_DoesNotFetchPagesEagerly` | lazy invariant |
| 14 | `StreamAsync_CrossTenant_ReturnsEmptyEnumerable` | uniform-empty |

---

## 6. Pattern conformance

Engineer V3 #1 PR description (PR description, NOT commit body):

```markdown
@standing-pattern: pattern-009-tenant-keying-retrofit (formal) — TenantId as EXPLICIT first parameter
@candidate-pattern: pattern-canonical-audit-payload-shape (V10 #2 + V11 #1 emergent candidate; 2nd instance)
```

---

## 7. Forward-watches for follow-on PRs (per V11 #2 §6)

- **EventLogBackedAuditEventReader (ADR 0094 Step 2)** — fires post-Step 1 merge
- **Bridge endpoint family** (Engineer cohort-4 PR 0) — depends on this Step 1
- **CSV export endpoint** (cohort-4 Engineer PR 1; uses `StreamAsync`)
- **CountAsync future amendment** (when UI needs total-count)
- **IssuedBy filter** (deferred to future security-review ADR)

---

## 8. Implementation checklist (Engineer pre-flight)

- [ ] Package: `shipyard/packages/kernel-audit/`
- [ ] Project file: `Sunfish.Kernel.Audit.csproj` (multi-targeted if needed)
- [ ] Namespace: `Sunfish.Kernel.Audit`
- [ ] Reference dependencies: ADR 0049 (`IAuditTrail`), ADR 0092 (`TenantId`), ADR 0046 (`IOperationSigner` — for production impl only; in-memory dev can skip)
- [ ] `using` directives: `System.Diagnostics` (Activity); `System.Runtime.CompilerServices` (EnumeratorCancellation); `Sunfish.Foundation.Assets.Common`
- [ ] DI extension: `AddSunfishKernelAudit()` registers Singleton InMemoryAuditTrail + InMemoryAuditEventReader
- [ ] Tests: at least 14 per §5; use NSubstitute or RecordingAuditTrail mock for write-side
- [ ] Build: `dotnet build` succeeds against ADR 0049 + ADR 0092 + ADR 0046 references
- [ ] Council SPOT-CHECK: sec-eng + .NET-architect dual MANDATORY per V10 #1 §4.7

---

## 9. Comparison to V10 #1 §4 (for Engineer awareness)

| Aspect | V10 #1 §4 (incorrect) | This supplement (canonical per ADR 0094) |
|---|---|---|
| TenantId param | Implicit (via DI ITenantContext) | **EXPLICIT first positional** |
| Primary list method | `GetByTenantAsync` | `ListAsync` |
| Detail method | `GetByIdAsync` (no tenantId param) | `GetByIdAsync(tenantId, auditId, ct)` |
| Streaming | not specified | `StreamAsync` with `IAsyncEnumerable<AuditRecord>` |
| Filter shape | `AuditEventFilters` with Severity field | `AuditEventQuery` (NO Severity field) |
| Severity filter | Substrate parameter | **Bridge-layer maps to EventType prefix-match** |
| Cursor signing | Substrate (in V10 #1) | **Bridge layer** (recommended) |
| Page shape | `AuditEventListResult` (custom) | `AuditEventPage(Records, NextCursor)` |

**Engineer guidance:** follow this supplement. V10 #1 §4 is superseded.

---

## 10. PR description acceptance criteria template

```markdown
## Implements ADR 0094 Step 1 (per V12 #3 supplement)

### Canonical signature
- [x] `IAuditEventReader` interface with explicit TenantId first parameter
- [x] Methods: `GetByIdAsync`, `ListAsync`, `StreamAsync`
- [x] `AuditEventQuery` (no Severity field; Bridge maps separately)
- [x] `AuditEventPage(Records, NextCursor)` 2-field shape

### Invariants
- [x] Cross-tenant uniform-empty on all 3 methods
- [x] TBV emission ONLY on `GetByIdAsync` cross-tenant path
- [x] TBV payload: canonical 5-field (V10 #2 + V11 #1 alignment)
- [x] No per-row TBV emission on `ListAsync` / `StreamAsync`
- [x] PageSize 1..200; default 50; throws ArgumentException out of range

### Tests
- [x] 14+ unit tests per §5

### DI
- [x] `AddSunfishKernelAudit()` extension; Singleton lifetime for InMemoryAuditEventReader

### Forward-watches
- [ ] EventLogBackedAuditEventReader follow-on (post-merge)
- [ ] Bridge endpoint family (Engineer cohort-4 PR 0)
- [ ] Cursor signing at Bridge layer (this PR uses unsigned in-memory; signing added in cohort-4 PR 0)
```

---

## 11. SPOT-CHECK dispatch matrix (per V11 #2 + V10 #1)

| Council | Mandatory? | Rationale |
|---|---|---|
| **sec-eng-council** | **MANDATORY** | Cross-tenant invariants + audit emission + canonical 5-field per V10 #2 / V11 #1 |
| **.NET-architect** | **MANDATORY** | New substrate primitive + interface API design |
| test-eng-council | RECOMMENDED | New substrate; baseline coverage |
| frontend-architect | DEFER | No frontend surface |

---

## 12. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T18-25Z` item V12 #3
2. `shipyard/docs/adrs/0094-i-audit-event-reader.md` (Accepted) — canonical source
3. V10 #1 Engineer substrate ladder PR-by-PR specs (shipyard#121) — V10 #1 §4 superseded by this supplement
4. V10 #2 audit-payload canonical research (shipyard#122)
5. V11 #1 pattern-012 canonical framing research (shipyard#124) — pattern-canonical-audit-payload-shape candidate
6. V11 #2 ADR 0094 Step 1 Engineer consultation (shipyard#127) — divergence finding
7. V11 #3 InMemoryMaintenanceService migration scope (shipyard#125) — 5-field canonical reference
8. ADR 0091 R2 + ADR 0092 R2 + ADR 0046 + ADR 0049
9. fleet-conventions §SPOT-CHECK dispatch SLA

---

## 13. What ONR does next

V12 #3 supplement complete. V12 #4 conditional on QM V5 #3 landing (NOT met
during V12 session per V12 dispatch check). V12 batch complete; ONR files
idle beacon.

— ONR, 2026-05-22T18:55Z
