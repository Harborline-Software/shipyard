# Engineer substrate ladder — PR-by-PR specs

**Authored by:** ONR (V10 batch item #1 PRIMARY)
**Requester:** Admiral (per `admiral-directive-2026-05-22T16-20Z` item V10 #1; carryover from V8 #2 + V9 #2 carryover)
**Authored at:** 2026-05-22T16-30Z
**Workstream coverage:** W#78 cohort-4 substrate + ADR 0091 R2 Steps 3+4 + foundation-idempotency

---

## 1. Purpose + scope

Implementation-ready spec for the **5-PR Engineer substrate ladder** spanning:

1. **ADR 0091 Step 3** — Consumption refactor (Engineer V2 #1; Phase 3)
2. **ADR 0091 Step 4** — Analyzer (`RequestContextMixingAnalyzer`) + facade `[Obsolete]`
3. **ADR 0094 Step 1** — `IAuditEventReader` substrate (cohort-4 prereq)
4. **signal-bridge audit-events endpoint family** — cohort-4 Engineer PR 0
5. **foundation-idempotency** (shipyard#113) follow-on amendments

The 5 PRs constitute Engineer's substrate critical path through the MVP window.
Each section provides: file-level scope, test expectations, pattern conformance,
SPOT-CHECK dispatch matrix, halt conditions, and forward-watches.

**State at V10 #1 authoring (2026-05-22T16:30Z):**

| PR | State | Gate |
|---|---|---|
| #1 ADR 0091 Step 3 | NOT OPEN | gated on shipyard#68 (folded; re-attest pending) |
| #2 ADR 0091 Step 4 | NOT OPEN | gated on Step 3 |
| #3 ADR 0094 Step 1 | NOT OPEN | gated on cohort-4 Engineer queue position |
| #4 signal-bridge endpoint | NOT OPEN | gated on #3 |
| #5 foundation-idempotency follow-on | shipyard#113 OPEN | follow-on amendments TBD |

---

## 2. PR #1 — ADR 0091 Step 3 (consumption refactor)

**Branch suggestion:** `feat/adr-0091-step-3-consumption-refactor`
**Estimated effort:** 12-24h Engineer time (~6-8 batched PRs by package proximity OR one large PR)
**Estimated LOC:** ~1,500-2,500 across 9 packages (per shipyard#68 §3.1 inventory)
**Workstream:** W#75 substrate / Engineer V2 #1 Phase 3
**Council requirements:** .NET-architect (mandatory; cross-package contract narrowing)

### 2.1 Files touched (package inventory per shipyard#68 §3.1)

Facade consumers (9 packages reference `Sunfish.Foundation.Authorization`):

| Package | Likely narrow target | Notes |
|---|---|---|
| `packages/blocks-financial-payments/` | `Foundation.MultiTenancy.ITenantContext` | Per Option A guards |
| `packages/blocks-financial-ledger/` | `Foundation.MultiTenancy.ITenantContext` | `IJournalStore` tenant-keyed |
| `packages/blocks-financial-ar/` | `Foundation.MultiTenancy.ITenantContext` | Invoice tenant-keyed |
| `packages/blocks-financial-ap/` | `Foundation.MultiTenancy.ITenantContext` | Bill tenant-keyed |
| `packages/blocks-leases/` | `Foundation.MultiTenancy.ITenantContext` | `ILeaseService.ListAsync` consumer |
| `packages/blocks-businesscases/` | **KEEP facade** (post-audit) | EntitlementSnapshotBlock reads user + roles |
| `packages/blocks-subscriptions/` | **KEEP facade** (post-audit) | Subscription ops may need user + roles |
| `packages/foundation/tests/` | Narrow per consumer | NotificationContractsTests mock construction |
| `packages/foundation-authorization/tests/` | unchanged | Step 1 acceptance tests |

Production-code narrowing pattern (per shipyard#68 §3.4):

```csharp
// Before (facade-injected; over-broad)
public class JournalEntryService
{
    private readonly Foundation.Authorization.ITenantContext _ctx;
    public JournalEntryService(Foundation.Authorization.ITenantContext ctx) { _ctx = ctx; }

    public async Task PostAsync(...)
    {
        var tenantId = _ctx.TenantId;  // only reads tenant
        // ... no user or roles consumption ...
    }
}

// After (narrowed to MultiTenancy variant)
public class JournalEntryService
{
    private readonly Foundation.MultiTenancy.ITenantContext _ctx;
    public JournalEntryService(Foundation.MultiTenancy.ITenantContext ctx) { _ctx = ctx; }

    public async Task PostAsync(...)
    {
        var tenantId = _ctx.TenantId;
    }
}
```

Test-fixture migration alongside production-code narrowing:

```csharp
// Before
var ctx = new DemoTenantContext(tenantId: "t1", userId: "u1", roles: new[] { "admin" });

// After (narrowed)
var ctx = new DemoMultiTenancyContext(tenantId: "t1");
```

### 2.2 Folded amendments from shipyard#68 (per .NET-architect verdict 2026-05-21T1228Z)

Engineer MUST honor in Step 3 + Step 4 PRs:

- **Fold A** (Hidden Concern #2): Canonical-vs-directive framing is single deliverable.
  Engineer treats Step 3 (test migration) + Step 3.0 (consumption sweep) as ONE unit
  of work per consumer site.
- **Fold B** (Hidden Concern #1): Analyzer detection (Step 4) uses semantic-model
  symbol resolution. Affects PR #2 (analyzer), not PR #1 directly — but Engineer's
  consumption-sweep approach should use semantic model where possible.
- **Fold C** (Q2 verdict): Step 4 analyzer test #6 is repurposed; affects PR #2.

### 2.3 Pre-flight checklist (Engineer executes before opening PR)

- [ ] Verify shipyard#68 MERGED + .NET-architect re-attest GREEN (after folds applied)
- [ ] Verify Step 2.0 (DbContext rewrite) MERGED on main
- [ ] Verify Step 2.1+ batched endpoint migrations status (may or may not be required pre-Step-3)
- [ ] Pull latest main; ensure no merge conflicts in target packages
- [ ] Confirm consumer inventory matches shipyard#68 §3.1 (run `grep -rn "Sunfish.Foundation.Authorization" packages/` and reconcile)

### 2.4 PR strategy — batched vs monolithic

Per shipyard#68 §3.4 + §6 ONR analysis:

**Option A (batched, recommended):** 6-8 PRs by package proximity:
- PR A1: `blocks-financial-payments` + `blocks-financial-ledger` (cluster)
- PR A2: `blocks-financial-ar` + `blocks-financial-ap` (cluster)
- PR A3: `blocks-leases`
- PR A4: `foundation/tests` + `foundation-authorization/tests`
- PR A5: `blocks-businesscases` audit + (keep-facade rationale doc)
- PR A6: `blocks-subscriptions` audit + (keep-facade rationale doc)

**Option B (monolithic):** Single PR with ~1,500-2,500 LOC + ~9 packages. Higher
review burden; harder to revert if regression; .NET-architect SPOT-CHECK density
likely exceeds the 5-6 items per verdict baseline.

**ONR recommendation:** Option A (batched). Engineer's call. If monolithic,
flag .NET-architect dispatch density expectation explicitly.

### 2.5 Tests

Each batched PR adds:
- **Per-package**: 2-4 unit tests verifying the narrowed-interface consumer
  works at the new contract surface
- **Per-package**: 1-2 fixture-migration verification tests (DemoTenantContext
  → DemoMultiTenancyContext where applicable)
- **Cross-package**: 1-2 integration tests verifying full request pipeline still
  resolves correctly with mixed-narrowed consumers

Cumulative: ~30-60 test additions per shipyard#68 §1.3 estimate.

### 2.6 Pattern conformance

- **pattern-009-tenant-keying-retrofit** (formal; shipyard#103) — consumed; new
  consumers join the narrowed-MultiTenancy variant pattern.

### 2.7 SPOT-CHECK dispatch matrix

| Event | Council | Notes |
|---|---|---|
| Each batched PR Ready-flip | .NET-architect | Per fleet-conventions §SPOT-CHECK SLA; 30-min dispatch |
| First batched PR | sec-eng-council DEFER expected | Per V8 #5 DEFER protocol — no Bridge endpoints; consumption-narrowing only |

### 2.8 Halt conditions

- H1: shipyard#68 re-attest finds new AMBER → Step 3 PR authoring blocks
- H2: Step 2.0 DbContext rewrite not yet MERGED → Step 3 PR authoring blocks
- H3: `blocks-businesscases` or `blocks-subscriptions` audit reveals they actually
  DON'T need user + roles → re-narrow + extra PR
- H4: Test-fixture migration breaks a test that was relying on facade-shaped
  DemoTenantContext → fixture-migration PR ships first

### 2.9 Forward-watched concerns

- Step 4 facade-removal countdown begins after Step 3 ships (one-cohort grace per ADR 0091 R2)
- Bridge-side consumers (signal-bridge/) — Step 2.1+ batched endpoint migrations
  may overlap; coordinate scope to avoid double-touch
- `Foundation.Authorization` facade `[Obsolete]` (Step 4) firing on remaining consumers — manage warning count

---

## 3. PR #2 — ADR 0091 Step 4 (analyzer + facade `[Obsolete]`)

**Branch suggestion:** `feat/adr-0091-step-4-analyzer-and-obsolete`
**Estimated effort:** ~2-3 days Engineer time
**Estimated LOC:** ~800-1,200 (analyzer project + tests + `[Obsolete]` attribute + suppressions)
**Workstream:** W#75 substrate / Engineer V2 #2
**Council requirements:** .NET-architect (mandatory; analyzer design) + sec-eng (mandatory; cross-pipeline invariant)

### 3.1 Files created/touched

**New analyzer project:**

```
packages/foundation-authorization/
├── ITenantContext.cs                    [modify — add [Obsolete] per §3.5]
├── Sunfish.Foundation.Authorization.csproj
└── Sunfish.Foundation.Authorization.Analyzers/   [NEW project]
    ├── Sunfish.Foundation.Authorization.Analyzers.csproj
    ├── RequestContextMixingAnalyzer.cs
    ├── DiagnosticDescriptors.cs                  [SUNFISH_AUTH_001]
    └── Properties/
        └── AssemblyInfo.cs
```

**New test project:**

```
packages/foundation-authorization/tests/
└── Sunfish.Foundation.Authorization.Analyzers.Tests/
    ├── *.csproj
    └── RequestContextMixingAnalyzerTests.cs   [≥6 tests per §3.4]
```

### 3.2 Analyzer implementation (per shipyard#68 §4.4 fold B)

**MUST USE: semantic-model symbol resolution.** Pseudo-code per shipyard#68:

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequestContextMixingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SUNFISH_AUTH_001";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticDescriptors.Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var ctor = (ConstructorDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var paramTypes = ctor.ParameterList.Parameters
            .Select(p => semanticModel.GetSymbolInfo(p.Type!).Symbol as INamedTypeSymbol)
            .Where(s => s != null)
            .ToList();

        var hasBrowser = paramTypes.Any(s => s!.ToDisplayString() == "Sunfish.Bridge.Middleware.IBrowserTenantContext");
        var controlPlaneSymbols = new[]
        {
            "Sunfish.Foundation.Authorization.ITenantContext",
            "Sunfish.Foundation.MultiTenancy.ITenantContext",
            "Sunfish.Foundation.Authorization.ICurrentUser",
            "Sunfish.Foundation.Authorization.IAuthorizationContext"
        };
        var hasControlPlane = paramTypes.Any(s => controlPlaneSymbols.Contains(s!.ToDisplayString()));

        if (hasBrowser && hasControlPlane)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.Rule,
                ctor.GetLocation(),
                ctor.Identifier.Text,
                /* control-plane interface name */ "...");
            context.ReportDiagnostic(diagnostic);
        }
    }

    // AnalyzeMethod + AnalyzeFieldDeclaration follow same pattern
}
```

### 3.3 `[Obsolete]` attribute on facade

Per shipyard#68 §4.6:

```csharp
namespace Sunfish.Foundation.Authorization;

[Obsolete(
    "Inject one of Foundation.MultiTenancy.ITenantContext, ICurrentUser, IAuthorizationContext " +
    "instead of the facade. Register via AddSunfishTenantContext<TConcrete>. " +
    "Facade removal scheduled for Step 5 (one-cohort grace from Step 4). See ADR 0091 R2.")]
public interface ITenantContext
    : Sunfish.Foundation.MultiTenancy.ITenantContext, ICurrentUser, IAuthorizationContext
{
    string TenantId => Tenant?.Id.ToString() ?? string.Empty;
}
```

**Severity:** Warning (NOT Error). Per shipyard#68 §6 Q3, ONR recommends Warning;
.NET-architect to confirm on re-attest.

### 3.4 Test coverage (≥6 analyzer tests per shipyard#68 §4.5 + fold C)

Positive tests (analyzer fires):

```csharp
[Fact] public async Task DetectsMixing_InConstructor()
[Fact] public async Task DetectsMixing_NarrowedInterface()         // IBrowserTenantContext + MultiTenancy
[Fact] public async Task DetectsMixing_InMethodSignature()         // [FromServices] params
```

Negative tests (analyzer does NOT fire — fold C critical):

```csharp
[Fact] public async Task DoesNotFire_DataPlaneOnly()
[Fact] public async Task DoesNotFire_ControlPlaneOnly()
[Fact] public async Task DoesNotFire_SeparateClassesSameCompositionRoot()  // CANONICAL Bridge wiring; fold C
```

Additional optional tests:
- `DetectsMixing_InFieldDeclaration` (field-injection consumer)
- `DoesNotFire_UsingDirective` (verifies semantic-model resolution works on `using`-imported types)

### 3.5 Suppressions for test code

Tests legitimately need to construct objects with both interface variants for
cross-cluster integration scenarios. Engineer adds:

```csharp
// In Sunfish.Bridge.Tests.Integration test fixtures only:
[SuppressMessage("Sunfish.Architecture", "SUNFISH_AUTH_001",
    Justification = "Test fixture intentionally constructs cross-pipeline scenario for verification")]
```

Per shipyard#68 §7 risk row 2.

### 3.6 NuGet packaging

The analyzer ships as:
- Analyzer DLL in `Sunfish.Foundation.Authorization.Analyzers.dll`
- Referenced as `<Analyzer />` ItemGroup in consuming projects
- Bundled into `Sunfish.Foundation.Authorization` NuGet package (multi-targeted)

Per shipyard#68 §4.2 reference to `foundation-wayfinder-analyzers` pattern.

### 3.7 IHostedService propagation verification (per shipyard#68 §5)

Extend `TenantContextScopeAssertion.cs` (existing IHostedService) with:

```csharp
public sealed class CrossClusterMixingAssertion : IHostedService
{
    private readonly IServiceProvider _services;
    public CrossClusterMixingAssertion(IServiceProvider services) { _services = services; }

    public Task StartAsync(CancellationToken ct)
    {
        // Scan all registered services for IBrowserTenantContext registrations
        // For each, verify it's NOT also registered as control-plane interface
        // If detected: throw at startup. Fail-closed.
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

Defense-in-depth: compile-time analyzer + runtime startup assertion.

### 3.8 SPOT-CHECK dispatch matrix

| Event | Council | Notes |
|---|---|---|
| PR Ready-flip | .NET-architect (analyzer design + fold B + C verification) + sec-eng (cross-pipeline invariant) | Dual SPOT-CHECK; both mandatory |
| `[Obsolete]` severity confirmation | .NET-architect | Per shipyard#68 §6 Q3 |

### 3.9 Halt conditions

- H1: Step 3 PR(s) not all MERGED → Step 4 authoring blocks
- H2: Analyzer false-positive on legitimate same-composition-root case → fold C verification gap; fix tests
- H3: NU1510 fires on Analyzer.csproj → suppress (per fleet-conventions / memory)
- H4: `foundation-wayfinder-analyzers` pattern doesn't match what's actually shipped → Engineer pre-flight verification; restructure analyzer project

### 3.10 Forward-watched concerns

- Step 5 facade deletion (one-cohort grace post-Step 4)
- Remaining `[Obsolete]` warnings on `blocks-businesscases` / `blocks-subscriptions`
  during grace window — managed; cleanup in Step 5
- IBrowserTenantContext direct-use detection (scope creep per shipyard#68 §6;
  future analyzer extension if needed)

---

## 4. PR #3 — ADR 0094 Step 1 (IAuditEventReader substrate)

**Branch suggestion:** `feat/adr-0094-step-1-audit-event-reader`
**Estimated effort:** ~1-2 days Engineer time
**Estimated LOC:** ~280-400 (substrate + tests; per ADR 0094 §B "Con" cost estimate)
**Workstream:** W#78 cohort-4 substrate
**Council requirements:** .NET-architect (mandatory; new substrate primitive) + sec-eng (mandatory; cross-tenant invariant + uniform-404)

### 4.1 Files created

**New substrate package interface + types in `packages/kernel-audit/`:**

```
packages/kernel-audit/
├── src/
│   ├── IAuditEventReader.cs           [NEW; primary interface]
│   ├── AuditEventSummary.cs           [NEW; list-result record]
│   ├── AuditEventDetail.cs            [NEW; detail-result record]
│   ├── AuditEventCursor.cs            [NEW; opaque cursor type]
│   └── AuditEventFilters.cs           [NEW; query-filter DTO]
└── InMemoryAuditEventReader.cs        [NEW; in-memory impl for tests]
```

### 4.2 `IAuditEventReader` interface

```csharp
namespace Sunfish.Kernel.Audit;

public interface IAuditEventReader
{
    Task<AuditEventListResult> GetByTenantAsync(
        AuditEventFilters filters,
        AuditEventCursor? cursor,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AuditEventDetail?> GetByIdAsync(
        Guid auditId,
        CancellationToken cancellationToken);
}

public sealed record AuditEventListResult(
    IReadOnlyList<AuditEventSummary> Events,
    AuditEventCursor? NextCursor);

public sealed record AuditEventSummary(
    Guid AuditId,
    DateTimeOffset OccurredAt,
    string EventType,
    string Actor,
    Guid CorrelationId,
    Dictionary<string, object> PayloadSummary,
    SignatureState SignatureState);

public sealed record AuditEventDetail(
    Guid AuditId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    string EventType,
    string Actor,
    Guid CorrelationId,
    Dictionary<string, object> Payload,
    SignatureState SignatureState,
    byte[]? Signature);

public sealed record AuditEventFilters(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? EventType,
    Guid? CorrelationId,
    string? Severity);  // V9 #1 A2 — NEW severity prefix filter

public sealed record AuditEventCursor(string OpaqueValue);

public enum SignatureState { Verified, VerificationFailed, NotSigned }
```

### 4.3 Cross-tenant invariant implementation

Per ADR 0092 §A3 uniform-404 invariant:

```csharp
public async Task<AuditEventDetail?> GetByIdAsync(Guid auditId, CancellationToken ct)
{
    var record = await _store.GetAsync(auditId, ct);
    if (record is null) return null;                                  // missing → null
    if (record.TenantId != _tenantContext.TenantId.Value) return null; // cross-tenant → null (uniform-404)
    return MapToDetail(record);
}
```

**Tuple-compare walking predicate for cursor pagination:**

```csharp
public async Task<AuditEventListResult> GetByTenantAsync(...)
{
    IQueryable<AuditRecord> query = _store
        .WhereTenant(_tenantContext.TenantId)            // Layer 4 EF global filter
        .Where(r => r.TenantId == _tenantContext.TenantId.Value);  // Layer 8 belt+suspenders

    if (filters.From.HasValue) query = query.Where(r => r.OccurredAt >= filters.From);
    if (filters.To.HasValue) query = query.Where(r => r.OccurredAt < filters.To);
    if (!string.IsNullOrEmpty(filters.EventType)) query = query.Where(r => r.EventType == filters.EventType);
    if (filters.CorrelationId.HasValue) query = query.Where(r => r.CorrelationId == filters.CorrelationId);
    if (!string.IsNullOrEmpty(filters.Severity))
        query = query.Where(r => r.EventType.StartsWith(filters.Severity + "."));  // A2 prefix-match

    // Cursor-based pagination: tuple-compare on (OccurredAt, AuditId) DESC
    if (cursor != null)
    {
        var (cursorOccurredAt, cursorAuditId) = DecodeCursor(cursor);
        query = query.Where(r =>
            r.OccurredAt < cursorOccurredAt ||
            (r.OccurredAt == cursorOccurredAt && r.AuditId < cursorAuditId));
    }

    var rows = await query
        .OrderByDescending(r => r.OccurredAt)
        .ThenByDescending(r => r.AuditId)
        .Take(pageSize + 1)   // fetch one extra to determine if next page exists
        .ToListAsync(ct);

    var hasNext = rows.Count > pageSize;
    var pageRows = hasNext ? rows.Take(pageSize).ToList() : rows;
    var nextCursor = hasNext ? EncodeCursor(pageRows.Last()) : null;

    return new AuditEventListResult(
        pageRows.Select(MapToSummary).ToList(),
        nextCursor);
}
```

### 4.4 Cursor encoding (signed; IOperationSigner)

Per cohort-4 hand-off Decision 2 + ADR 0046:

```csharp
private string EncodeCursor(AuditRecord lastRow)
{
    var cursorData = $"{lastRow.OccurredAt:O}|{lastRow.AuditId}|{_tenantContext.TenantId}";
    var signed = _operationSigner.SignAsync(cursorData).Result;
    return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cursorData}|{signed}"));
}

private (DateTimeOffset, Guid) DecodeCursor(AuditEventCursor cursor)
{
    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.OpaqueValue));
    var parts = decoded.Split('|');
    if (parts.Length != 4) throw new ArgumentException("Malformed cursor");

    var occurredAt = DateTimeOffset.Parse(parts[0]);
    var auditId = Guid.Parse(parts[1]);
    var cursorTenantId = parts[2];
    var signature = parts[3];

    // Cross-tenant cursor reuse rejection
    if (cursorTenantId != _tenantContext.TenantId.Value.ToString())
        throw new TenantChangedException("tenant_changed_reload_page");

    // Verify signature
    var unsigned = $"{parts[0]}|{parts[1]}|{parts[2]}";
    if (!_operationSigner.VerifyAsync(unsigned, signature).Result)
        throw new ArgumentException("Invalid cursor signature");

    return (occurredAt, auditId);
}
```

### 4.5 Tests (≥12 substrate tests)

| # | Test | Closes |
|---|---|---|
| 1 | `GetByTenantAsync_NoFilters_ReturnsTenantScopedResults` | baseline |
| 2 | `GetByTenantAsync_FromToFilters_FiltersByDateRange` | baseline |
| 3 | `GetByTenantAsync_EventTypeFilter_FiltersByExactType` | baseline |
| 4 | `GetByTenantAsync_SeverityFilter_FiltersByEventTypePrefix` | V9 #1 A2 |
| 5 | `GetByTenantAsync_Cursor_ReturnsNextPage` | baseline |
| 6 | `GetByTenantAsync_TupleCompareCursor_HandlesSameOccurredAtTimestamp` | tuple invariant |
| 7 | `GetByTenantAsync_CrossTenantCursor_ThrowsTenantChangedException` | uniform-404 |
| 8 | `GetByTenantAsync_TamperedCursor_ThrowsArgumentException` | signature verify |
| 9 | `GetByIdAsync_OwnTenant_ReturnsDetail` | baseline |
| 10 | `GetByIdAsync_CrossTenant_ReturnsNull` | uniform-404 |
| 11 | `GetByIdAsync_NotFound_ReturnsNull` | uniform-404 |
| 12 | `GetByTenantAsync_LastPage_ReturnsNullNextCursor` | pagination end |

### 4.6 Pattern conformance

- **pattern-009-tenant-keying-retrofit** (formal) — consumed
- **pattern-tenant-id-signed-opaque-cursor** (candidate; V9 #1 forward-watch) —
  first instance; promote to candidate per V8 #6 cadence
- **pattern-uniform-404-cross-tenant** (potential candidate; V9 #1 forward-watch) —
  add via ADR 0092 §A3 alignment

### 4.7 SPOT-CHECK dispatch matrix

| Event | Council | Notes |
|---|---|---|
| PR Ready-flip | .NET-architect (substrate primitive design) + sec-eng (cross-tenant invariants) | Dual SPOT-CHECK |
| Stage-05 hand-off (already MERGED at shipyard#81) | sec-eng (Stage-05 Adversarial Review per ADR 0093 first pilot) | DONE |

### 4.8 Halt conditions

- H1: ADR 0094 itself needs amendment based on substrate-impl findings → file question to Admiral
- H2: `IOperationSigner` Singleton DI lifetime not yet registered (per ADR 0046) → fix prerequisite
- H3: EF Core `WhereTenant()` extension not available on `AuditRecord` DbSet → coordinate with Step 2.0 owner
- H4: Tuple-compare cursor performance issue at scale (single-DB testing reveals query plan thrash) → index advice for ops

### 4.9 Forward-watched concerns

- Audit-event payload size — large payloads may slow list page query; consider
  Project-Summary-vs-Detail split (`AuditEventSummary` strips heavy payload)
- Index requirements: `(TenantId, OccurredAt DESC, AuditId DESC)` for cursor query
- Cursor TTL — opaque cursors are infinite-lived; consider TTL field in encoded payload (post-MVP)

---

## 5. PR #4 — signal-bridge audit-events endpoint family (Engineer PR 0)

**Branch suggestion:** `feat/audit-events-endpoint-family`
**Repository:** `signal-bridge/`
**Estimated effort:** ~1-2 days Engineer time
**Estimated LOC:** ~600-900 (endpoint handlers + tests; per cohort-4 hand-off §4)
**Workstream:** W#78 cohort-4 / Engineer PR 0
**Council requirements:** sec-eng (MANDATORY; pattern-009 PAIR + cohort-4 ADR 0093 retro-eligible) + .NET-architect (mandatory)

### 5.1 Files created in signal-bridge

```
signal-bridge/Sunfish.Bridge/
└── Audit/
    ├── AuditEventsEndpoint.cs                   [NEW]
    ├── AuditEventsRequestDtos.cs                [NEW]
    └── AuditEventsResponseDtos.cs               [NEW]
signal-bridge/Sunfish.Bridge.Tests.Integration/
└── Audit/
    └── AuditEventsEndpointTests.cs              [NEW; ≥10 tests]
```

### 5.2 Endpoint family per cohort-4 hand-off §4

```csharp
app.MapGroup("/api/v1/audit-events")
   .RequireAuthorization("AuthenticatedTenantPolicy")
   .MapAuditEventsEndpoints();

public static class AuditEventsEndpoint
{
    public static RouteGroupBuilder MapAuditEventsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync);
        group.MapGet("/{auditId:guid}", GetByIdAsync);
        // group.MapGet("/export.csv", ExportCsvAsync);  // deferred to Engineer PR 1
        return group;
    }

    public static async Task<IResult> ListAsync(
        [AsParameters] AuditEventsListRequest request,
        IAuditEventReader reader,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var filters = new AuditEventFilters(
            From: request.From,
            To: request.To,
            EventType: request.EventType,
            CorrelationId: request.CorrelationId,
            Severity: request.Severity);   // V9 #1 A2

        AuditEventCursor? cursor = null;
        if (!string.IsNullOrEmpty(request.Cursor))
            cursor = new AuditEventCursor(request.Cursor);

        try
        {
            var result = await reader.GetByTenantAsync(
                filters, cursor, pageSize: 50, ct);
            return Results.Ok(new AuditEventsListResponse(result.Events, result.NextCursor?.OpaqueValue));
        }
        catch (TenantChangedException)
        {
            return Results.BadRequest(new { error = "tenant_changed_reload_page" });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("cursor"))
        {
            return Results.BadRequest(new { error = "invalid_cursor" });
        }
    }

    public static async Task<IResult> GetByIdAsync(
        Guid auditId,
        IAuditEventReader reader,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var detail = await reader.GetByIdAsync(auditId, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }
}

public sealed record AuditEventsListRequest(
    [FromQuery] DateTimeOffset? From,
    [FromQuery] DateTimeOffset? To,
    [FromQuery(Name = "event_type")] string? EventType,
    [FromQuery(Name = "correlation_id")] Guid? CorrelationId,
    [FromQuery] string? Severity,
    [FromQuery] string? Cursor);

public sealed record AuditEventsListResponse(
    IReadOnlyList<AuditEventSummary> Events,
    string? NextCursor);
```

### 5.3 Tests (≥10 integration tests)

| # | Test | Closes |
|---|---|---|
| 1 | `GET_AuditEvents_OwnTenant_ReturnsList` | baseline |
| 2 | `GET_AuditEvents_FromToFilter_NarrowsResults` | baseline |
| 3 | `GET_AuditEvents_EventTypeFilter_NarrowsResults` | baseline |
| 4 | `GET_AuditEvents_SeverityFilter_FiltersByPrefix` | V9 #1 A2 |
| 5 | `GET_AuditEvents_CursorPagination_ReturnsNextPage` | baseline |
| 6 | `GET_AuditEvents_CrossTenantCursor_Returns400TenantChangedReload` | V9 #1 G1 |
| 7 | `GET_AuditEvents_TamperedCursor_Returns400InvalidCursor` | signature verify |
| 8 | `GET_AuditEventsById_OwnTenant_ReturnsDetail` | baseline |
| 9 | `GET_AuditEventsById_CrossTenant_Returns404` | V9 #1 A1 uniform-404 |
| 10 | `GET_AuditEvents_AnonymousRequest_Returns401` | auth policy |
| 11 | `GET_AuditEvents_AuthenticatedTenantPolicyRequired` | auth policy |
| 12 | `GET_AuditEvents_QueryString_DoesNotExposeTenantId` | tenant scoping |

### 5.4 Pattern conformance

- **pattern-009** (Bridge endpoint + frontend rebind pair; formal) — PAIRS with sunfish FED PR 1
- **pattern-009-cohort-4-audit-pair** (candidate; per cohort-4 sunfish#59 verdict)
- **pattern-tenant-id-signed-opaque-cursor** (candidate; #3 substrate emergence)
- **pattern-uniform-404-cross-tenant** (potential candidate)

### 5.5 SPOT-CHECK dispatch matrix

| Event | Council | Notes |
|---|---|---|
| PR Ready-flip | sec-eng (MANDATORY pattern-009) + .NET-architect (Bridge endpoint design) | Dual SPOT-CHECK; 30-min Admiral dispatch |

### 5.6 Halt conditions

- H1: PR #3 (`IAuditEventReader` substrate) not yet MERGED → Bridge endpoint authoring blocks
- H2: AuthenticatedTenantPolicy not yet defined → register policy first; coordinate with cohort-1 precedent
- H3: sec-eng SPOT-CHECK AMBER finding requires fold → ~1 day recovery
- H4: Cursor format mismatch with sunfish FED expectations → coordinate via beacon

### 5.7 Forward-watched concerns

- CSV export endpoint (cohort-4 hand-off §4.5; deferred to Engineer PR 1; not in #4)
- Rate-limit on audit-events endpoint (anonymous can DoS the list?) — covered by AuthenticatedTenantPolicy
- Audit-of-audit-viewer-access (cohort-4 hand-off H6 forward-watch) — does the viewer-query itself emit an audit event? NO per hand-off scope decision

---

## 6. PR #5 — foundation-idempotency follow-on amendments (shipyard#113)

**Branch suggestion:** `feat/foundation-idempotency-amendments`
**Estimated effort:** ~0.5-1 day Engineer time (depends on shipyard#113 SPOT-CHECK findings)
**Estimated LOC:** ~200-400 amendments
**Workstream:** W#60 P4 (work-item shorthand)
**Council requirements:** sec-eng-council (if shipyard#113 council verdicts surfaced AMBER findings)

### 6.1 Context — shipyard#113

shipyard#113: `feat(foundation-idempotency): IIdempotencyKeyStore primitive (work-item shorthand W60 P4 PR 2 prereq)` — OPEN.

The PR introduces the `IIdempotencyKeyStore` primitive substrate for Bridge POST
endpoints (per pattern-012 financial-write-path candidate). Follow-on amendments
fire based on council verdicts on shipyard#113 (when those land).

### 6.2 Expected amendment scope (per pattern-012 candidate + cohort-2 financial precedent)

Likely amendments based on cohort-2 financial-write-path patterns + pattern-012 maturity:

- **24h TTL enforcement** — cache window per Idempotency-Key
- **`409 RELOAD` response shape** — return cached body on duplicate key
- **Key validation** — format guards (e.g., UUID v4 mandatory; rejected if not)
- **Key normalization** — case-insensitive vs case-sensitive (decision needed)
- **Telemetry** — emit metric on cache hit / miss
- **DI lifetime** — Scoped (per-request) or Singleton (across requests with key-scoped lifetime)?

### 6.3 Files likely touched

```
packages/foundation-idempotency/
├── src/
│   ├── IIdempotencyKeyStore.cs        [modify if interface changes]
│   ├── IdempotencyOptions.cs          [NEW or modify; TTL config]
│   ├── IdempotencyMiddleware.cs       [NEW or modify; 409 RELOAD shape]
│   ├── IdempotencyKeyValidator.cs     [NEW; format guard]
│   └── InMemoryIdempotencyKeyStore.cs [extend]
└── tests/
    └── IdempotencyTests.cs            [extend with TTL + validation tests]
```

### 6.4 Tests (incremental)

- TTL expiration: key cached at T0; re-submitted at T+24h-ε returns cached body; re-submitted at T+24h+ε creates fresh record
- 409 RELOAD body shape: same status + headers + body as original response
- Key format rejection: malformed Idempotency-Key returns 400
- Telemetry: cache hit / miss emit Counter / Histogram (test via Prometheus exposition format)

### 6.5 Pattern conformance

- **pattern-012-financial-write-path** (candidate) — promoted to formal once 3rd instance lands (currently 2-instance: cohort-2 financial + shipyard#113 substrate)

### 6.6 SPOT-CHECK dispatch matrix

| Event | Council | Notes |
|---|---|---|
| Each amendment PR Ready-flip | sec-eng (Idempotency-Key handling per checklist Check 4) | Standard sec-eng SPOT-CHECK |
| pattern-012 promotion (if 3rd instance lands) | sec-eng (formalize) + .NET-architect (architecture) | Per V8 #5 / V8 #6 pattern catalog cadence |

### 6.7 Halt conditions

- H1: shipyard#113 council verdicts surface RED findings → Engineer applies amendments before #5 amendments fire
- H2: 24h TTL implementation requires substrate primitive that doesn't yet exist (e.g., timed-eviction cache) → coordinate with Engineer scope
- H3: 409 RELOAD body persistence (24h) costs > expected → may need TTL-tiered persistence strategy

### 6.8 Forward-watched concerns

- Idempotency-Key scope: per-tenant vs global? (per-tenant recommended for tenant-isolated mutations)
- Idempotency-Key persistence backend: in-memory (dev) vs Redis (production)?
- Idempotency interaction with retry policies (e.g., Polly): does retry use same key?
- 3rd-instance trigger for pattern-012 promotion

---

## 7. Cumulative PR ladder timeline

```
┌────────────────────────────────────────────────────────────────┐
│  PR #1 ADR 0091 Step 3 consumption refactor (6-8 batched)     │
│    └── ~3-5 days end-to-end (batched + reviews)               │
│         │                                                       │
│         ▼                                                       │
│  PR #2 ADR 0091 Step 4 analyzer + [Obsolete] (~2-3 days)       │
│    └── facade grace window begins                              │
│                                                                  │
│         (parallel, independent)                                 │
│                                                                  │
│  PR #3 ADR 0094 Step 1 IAuditEventReader (~1-2 days)           │
│    └── gates cohort-4 PR 4                                     │
│         │                                                       │
│         ▼                                                       │
│  PR #4 signal-bridge audit-events endpoint (~1-2 days)         │
│    └── pairs with sunfish FED PR 1 (per V9 #1)                 │
│                                                                  │
│         (parallel, independent)                                 │
│                                                                  │
│  PR #5 foundation-idempotency amendments (~0.5-1 day)          │
│    └── fires post-shipyard#113 verdicts                        │
└────────────────────────────────────────────────────────────────┘

Critical path: ~7-10 days Engineer time across all 5 PRs (with parallelism)
Serial path: ~9-12 days Engineer time
```

### 7.1 Parallelism opportunities

- PRs #1 + #3 can run parallel (different sub-trees; ADR 0091 vs ADR 0094)
- PRs #4 + #2 can run parallel post-#3 + post-Step-3 respectively
- PR #5 is independent timing-wise; fires when shipyard#113 verdicts land

### 7.2 Council dispatch density

Expected SPOT-CHECK density across the 5-PR ladder:

| PR | sec-eng | .NET-architect | frontend-architect | test-eng |
|---|---|---|---|---|
| #1 (Step 3) | DEFER expected | MANDATORY (each batch) | — | — |
| #2 (Step 4) | MANDATORY | MANDATORY | — | recommended |
| #3 (Audit reader) | MANDATORY | MANDATORY | — | recommended |
| #4 (Bridge endpoint) | MANDATORY | MANDATORY | — | — |
| #5 (Idempotency) | MANDATORY | — | — | — |

Total dispatches: ~12-15 across all 5 PRs.

---

## 8. Cross-ladder forward-watches

Items spanning multiple PRs:

1. **shipyard#68 re-attest dependency** — PR #1 (Step 3) cannot Ready-flip until
   shipyard#68 re-attest closes GREEN with .NET-architect (Q3 [Obsolete] severity).
2. **Step 5 facade deletion countdown** — one-cohort grace begins when PR #2 ships.
3. **pattern-012 promotion** — fires when 3rd instance lands; PR #5 amendments
   are part of the 2nd-instance maturity.
4. **pattern-tenant-id-signed-opaque-cursor** — emerges in PR #3; candidate registration recommended.
5. **pattern-uniform-404-cross-tenant** — emerges in PR #3 + PR #4; candidate registration recommended.
6. **Cohort-4 retrospective** — when cohort-4 ships (PR #3 + #4 + sunfish FED PR 1 + sunfish#59 auto-merge fires), ONR's V7 #5 Stage-05 retro authoring fires per V8 #6 retro cadence.

---

## 9. Pattern emergence forward-watches (consolidated)

| Pattern | Current state | PRs contributing | Promotion trigger |
|---|---|---|---|
| pattern-009 | formal | PR #4 (PAIR pattern) | — |
| pattern-009-tenant-keying-retrofit | formal | PR #1 | — |
| pattern-012-financial-write-path | candidate | PR #5 (2nd instance) | 3rd instance → formal |
| pattern-tenant-id-signed-opaque-cursor | (new) | PR #3 (1st instance) | 2nd instance → candidate |
| pattern-uniform-404-cross-tenant | (new) | PR #3 + #4 (multi-instance same PR) | 2nd cohort cross-instance → candidate |
| pattern-defense-in-depth-tenant-assert-client-side | (new; V9 #1) | (sunfish FED PR 1) | 2nd cohort → candidate |
| pattern-severity-event-prefix-coloring | (new; V9 #1) | (sunfish FED PR 1) | 2nd UI surface → candidate |

ONR consumes via post-cohort-10 retrospective per V8 #6 scaffold.

---

## 10. Decisions surfaced (route to Admiral)

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **PR #1 strategy** — batched (6-8 PRs; ONR recommended) vs monolithic (1 PR)?
2. **PR #5 timing** — fires post-shipyard#113 SPOT-CHECK verdicts, OR pre-emptively
   author amendments now? ONR recommends post-verdict (avoid wasted authoring).
3. **pattern candidates** — promote `pattern-tenant-id-signed-opaque-cursor` and
   `pattern-uniform-404-cross-tenant` to candidate now, OR wait for 2nd-instance?
   ONR recommends register-now (V8 #6 cadence).
4. **Step 5 facade deletion timing** — defined as "one cohort post-Step 4"; what
   cohort qualifies as the grace endpoint? Cohort-5? Cohort-6? CIC routing.
5. **PR #4 CSV export** — defer to Engineer PR 1 (per cohort-4 hand-off) confirmed?
   Or fold into #4? ONR recommends defer (PR is already 600-900 LOC).

---

## 11. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T16-20Z` item V10 #1
2. shipyard#68 (`icm/01_discovery/research/adr-0091-step-3-and-4-research-2026-05-21.md`) — ADR 0091 R2 source spec + folded amendments
3. ADR 0094 (IAuditEventReader; Accepted 2026-05-21)
4. ADR 0091 R2 (ITenantContext divergence resolution; Accepted)
5. ADR 0092 (substrate tenant-keyed; Accepted)
6. ADR 0046 (IOperationSigner; Accepted)
7. ADR 0049 (audit substrate; Accepted)
8. V9 #1 cohort-4 FED PR-by-PR specs (shipyard#119) — A1 + A2 + Bridge dependency
9. cohort-4 Stage-06 hand-off (shipyard#81 MERGED)
10. shipyard#113 (foundation-idempotency primitive; OPEN)
11. pattern-009 + pattern-009-tenant-keying-retrofit (formal patterns)
12. pattern-012-financial-write-path (candidate)
13. fleet-conventions §SPOT-CHECK dispatch SLA

---

## 12. What ONR does next

V10 #1 PRIMARY deliverable complete. Continues V10 sequence:
- V10 #2 (audit-payload field-count canonicalization research; ~1-2h light)
- V10 #3 (cohort-3 PR cluster spec consolidation; ~1-2h light)

— ONR, 2026-05-22T16:30Z
