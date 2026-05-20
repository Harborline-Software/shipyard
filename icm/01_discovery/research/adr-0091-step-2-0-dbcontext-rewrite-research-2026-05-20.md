# ONR research — ADR 0091 Step 2.0 DbContext Rewrite Implementation Pre-research (2026-05-20)

**Requester:** Admiral (per `admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` item #3)
**Authored by:** ONR
**Authored at:** 2026-05-20T12-00Z
**Status:** draft (ratification pending sec-eng + .NET-architect council review of fail-closed gate pseudo-code + A5 regression test seeding pattern)

---

## Scope of investigation

- **In scope:** the `SunfishBridgeDbContext` rewrite per ADR 0091 Rev 2 (Accepted) Step 2.0 — fail-closed-on-unresolved-tenant guard (A3), sentinel/null TenantId rejection (A4), legacy↔typed filter equivalence regression (A5), constructor signature narrowing from `Foundation.Authorization.ITenantContext` (facade) to `Foundation.MultiTenancy.ITenantContext` (pure tenant-resolution surface).
- **Out of scope:** Step 2.1+ batched endpoint migrations (~14 consumers; separate workstreams); Step 1 facade work (already shipped per Engineer's PR #44 / Admiral ruling 2026-05-19T12:35Z); ADR 0092 EF Core query-filter PRs (gated on Step 2.0 landing per directive).
- **Authoritative sources consulted:** ADR 0091 Rev 2 (Accepted; promoted 2026-05-19T02:40Z); current `SunfishBridgeDbContext.cs` source (167 lines verified 2026-05-20T12:00Z); ADR 0092 §C5 + §C6 (DI lifetime + ChangeTracker semantics); cohort-1 PR 1 sec-eng council verdict (typed TenantId capture-once-at-construction pattern; GREEN); fleet `IMustHaveTenant` shape.
- **Success looks like:** Engineer can open the Step 2.0 PR using this research doc's pseudo-code + test patterns + risk register as the implementation scaffold; sec-eng + .NET-architect councils can SPOT-CHECK against the seeded patterns.

---

## TL;DR

1. **Constructor signature change is mechanical**: `Foundation.Authorization.ITenantContext tenant` → `Foundation.MultiTenancy.ITenantContext tenant`. The facade still resolves to the same concrete impl (Demo for now, claims-backed eventually); the narrowed type prevents accidental authz reads inside the DbContext.

2. **A3 + A4 guards are constructor body additions** (~15 LOC + exception messages). The guards throw at construction so a misconfigured request pipeline cannot create a leaky DbContext silently.

3. **A5 regression test is the highest-implementation-risk slice**: requires a populated DB with multiple tenants + a sentinel row + a null-value row to assert per-tenant isolation, sentinel/null exclusion, AND unresolved-DbContext-throws — all in a single test. Pseudo-code below.

4. **Typed TenantId comparison vs legacy string comparison** is a layering exercise: `IMustHaveTenant` entities (new world) use `entityTenantId.Value == _capturedTenantId.Value`; legacy entities (Project/TaskItem/AuditRecord) keep `e.TenantId == _capturedTenantId.Value` (string-on-string).

5. **Migrations route through a dedicated migration DbContext** (NOT `SunfishBridgeDbContext`). `MigrationTenantContext` implements only `Foundation.MultiTenancy.ITenantContext` and resolves to `TenantId.System` — the sentinel `SunfishBridgeDbContext` REJECTS at construction. Migration DbContext design is in §5.

6. **EF Core preview surface considerations** — `HasQueryFilter` continues to work but EF Core 10+ preview surfaces may shift; preview.4 already invalidated the `OwnsMany().ToJson()` pattern (current SunfishBridgeDbContext uses a value converter workaround for `SupportContacts`). The query-filter API should be stable but verify against current preview build before Step 2.0 PR opens.

7. **One backward-compatibility consideration**: tests that construct `SunfishBridgeDbContext` directly (without going through DI) must be updated — they currently pass an `ITenantContext` facade; post-Step-2.0 they pass a `Foundation.MultiTenancy.ITenantContext`. The `DemoTenantContext` already implements both (sum-interface from Step 1) so the impl side is fine; test fixture types need to be reviewed.

---

## 1. Current state — `SunfishBridgeDbContext.cs` (verified 2026-05-20T12:00Z)

### 1.1 Constructor signature

```csharp
public SunfishBridgeDbContext(
    DbContextOptions<SunfishBridgeDbContext> options,
    IEnumerable<ISunfishEntityModule> modules,
    Sunfish.Foundation.Authorization.ITenantContext tenant)    // FACADE (Step 1 sum-interface)
    : base(options)
{
    _modules = modules;
    _currentTenantId = tenant.TenantId;                          // STRING capture via facade default-impl
}
```

- Takes the **facade** `Foundation.Authorization.ITenantContext` (Step 1 sum-interface).
- Captures `tenant.TenantId` (the default-implemented `string TenantId => Tenant?.Id.ToString() ?? string.Empty`).
- Stores the captured value in `private readonly string _currentTenantId;` field.
- **No guards** — accepts null TenantId, empty string, sentinel string, etc.

### 1.2 Tenant-filter implementation

`ApplyTenantQueryFilters` (lines 144-165):

```csharp
private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
    {
        if (!typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType)) continue;

        // (T e) => e.TenantId.Value == _currentTenantId
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var tenantIdProperty = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));
        var tenantIdValue = Expression.Property(tenantIdProperty, "Value");
        var currentTenantRef = Expression.Field(Expression.Constant(this), nameof(_currentTenantId));
        var equal = Expression.Equal(tenantIdValue, currentTenantRef);
        var lambda = Expression.Lambda(equal, parameter);

        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
    }
}
```

- Loops through every `IMustHaveTenant` entity type.
- Builds expression tree `(e) => e.TenantId.Value == _currentTenantId` (STRING comparison via `TenantId.Value`).
- Captures `this._currentTenantId` via `Expression.Field` — EF Core parameterizes this per-DbContext-instance, so each scope sees its own tenant's rows.

### 1.3 Legacy entity filters (lines 76-80)

```csharp
modelBuilder.Entity<Project>().HasQueryFilter(e => e.TenantId == _currentTenantId);
modelBuilder.Entity<TaskItem>().HasQueryFilter(e => e.TenantId == _currentTenantId);
modelBuilder.Entity<AuditRecord>().HasQueryFilter(e => e.TenantId == _currentTenantId);
```

Three legacy entities (`Project`, `TaskItem`, `AuditRecord`) use raw `string TenantId` field (do NOT implement `IMustHaveTenant`). Filter compares string-on-string.

### 1.4 Control-plane carve-out

`TenantRegistration` entity (lines 99-137) is configured but **NOT tenant-filtered** — control-plane queries need to see every tenant row for admin/billing/support operations per ADR 0031 Wave 5.1.

### 1.5 Module composition (ADR 0015)

Constructor takes `IEnumerable<ISunfishEntityModule> modules`; `OnModelCreating` loops through modules and calls `module.Configure(modelBuilder)` for each. Module-contributed `IMustHaveTenant` entities automatically pick up the tenant filter via `ApplyTenantQueryFilters`.

### 1.6 EF Core preview surface notes

- Current code uses `Property().HasConversion(converter, comparer)` for `SupportContacts` jsonb column — workaround for EF Core preview.3 obsoleting `OwnsMany().ToJson()` (line 121 comment).
- Migrations infrastructure (separate from this DbContext) is referenced in the code comments but not visible inline — needs separate verification.

---

## 2. Step 2.0 changes required per ADR 0091 Rev 2

Per ADR §"Step 2.0 — DbContext query-filter rewrite":

### 2.1 Constructor signature change

```csharp
public SunfishBridgeDbContext(
    DbContextOptions<SunfishBridgeDbContext> options,
    IEnumerable<ISunfishEntityModule> modules,
    Sunfish.Foundation.MultiTenancy.ITenantContext tenant)    // NARROWED
    : base(options)
```

- `Foundation.Authorization.ITenantContext` (facade) → `Foundation.MultiTenancy.ITenantContext` (pure tenant-resolution surface).
- The facade still works at the DI layer (it's the sum-interface that resolves to the same concrete impl); the narrowed parameter type prevents the DbContext from accidentally reading authz claims.

### 2.2 A3 fail-closed guard

Constructor REJECTS `tenant.Tenant == null` (throws `InvalidOperationException` citing amendment A3). Fail-closed under unresolved tenant.

### 2.3 A4 sentinel/null rejection

Constructor REJECTS:
- `tenant.Tenant.Id.IsSystemSentinel == true`
- `tenant.Tenant.Id.Value == null`
- `tenant.Tenant.Id.Value == "__system__"` (literal sentinel; defense-in-depth)

Throws `ArgumentException` citing amendment A4. Migrations route through `MigrationTenantContext` + dedicated migration DbContext (which is the ONLY sanctioned consumer of `TenantId.System`).

### 2.4 Field type change

`private readonly string _currentTenantId;` → `private readonly TenantId _capturedTenantId;` (typed; assigned once at construction; never mutates).

### 2.5 Query-filter rewrite

`ApplyTenantQueryFilters` rewritten:
- Build expression tree `(e) => e.TenantId.Value == _capturedTenantId.Value` (compares typed TenantId's `.Value` strings)
- Comparison is functionally identical to current STRING-on-STRING; the change is the SOURCE of the comparand (typed field instead of raw string)

### 2.6 Legacy filter rewrite

`modelBuilder.Entity<Project>().HasQueryFilter(e => e.TenantId == _capturedTenantId.Value);`

Legacy entities still use raw `string TenantId`; the comparand changes from `_currentTenantId` (string) to `_capturedTenantId.Value` (extracted string). Semantically identical comparison.

### 2.7 No control-plane changes

`TenantRegistration` configuration stays exactly as today; it remains unfiltered.

---

## 3. Implementation pseudo-code (3 fail-closed gates)

### 3.1 Updated constructor

```csharp
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Assets.Common;

public class SunfishBridgeDbContext : DbContext
{
    private readonly IEnumerable<ISunfishEntityModule> _modules;
    private readonly TenantId _capturedTenantId;     // CHANGED: typed

    public SunfishBridgeDbContext(
        DbContextOptions<SunfishBridgeDbContext> options,
        IEnumerable<ISunfishEntityModule> modules,
        Sunfish.Foundation.MultiTenancy.ITenantContext tenant)    // CHANGED: narrowed
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        // A3: fail-closed under unresolved tenant
        if (tenant.Tenant is null)
        {
            throw new InvalidOperationException(
                "SunfishBridgeDbContext cannot be constructed with an unresolved tenant. " +
                "Caller must resolve a non-null TenantMetadata before injecting " +
                "Foundation.MultiTenancy.ITenantContext. See ADR 0091 R2 amendment A3.");
        }

        // A4: sentinel / null / literal "__system__" rejection
        var tenantId = tenant.Tenant.Id;
        if (tenantId.IsSystemSentinel)
        {
            throw new ArgumentException(
                "SunfishBridgeDbContext rejects the system sentinel tenant. Migrations must " +
                "use MigrationTenantContext + a dedicated migration DbContext (the only " +
                "sanctioned consumer of TenantId.System). See ADR 0091 R2 amendment A4.",
                nameof(tenant));
        }
        if (tenantId.Value is null)
        {
            throw new ArgumentException(
                "SunfishBridgeDbContext rejects null TenantId.Value. See ADR 0091 R2 amendment A4.",
                nameof(tenant));
        }
        if (tenantId.Value == "__system__")
        {
            // Defense-in-depth: belt-and-suspenders against bypass of IsSystemSentinel check
            // via direct string construction outside the TenantId factory paths
            throw new ArgumentException(
                "SunfishBridgeDbContext rejects literal '__system__' tenant id. " +
                "See ADR 0091 R2 amendment A4.",
                nameof(tenant));
        }

        _modules = modules;
        _capturedTenantId = tenantId;        // CHANGED: typed assignment
    }

    // ... DbSets unchanged ...
}
```

### 3.2 Updated `ApplyTenantQueryFilters`

```csharp
private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
    {
        if (!typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
        {
            continue;
        }

        // Build: (T e) => e.TenantId.Value == _capturedTenantId.Value
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var entityTenantIdProperty = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));
        var entityTenantIdValue = Expression.Property(entityTenantIdProperty, "Value");

        var capturedTenantIdRef = Expression.Field(
            Expression.Constant(this),
            nameof(_capturedTenantId));
        var capturedTenantIdValue = Expression.Property(capturedTenantIdRef, "Value");

        var equal = Expression.Equal(entityTenantIdValue, capturedTenantIdValue);
        var lambda = Expression.Lambda(equal, parameter);

        modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
    }
}
```

Difference vs current (line 144-165): the comparand is `Expression.Property(capturedTenantIdRef, "Value")` instead of the bare `Expression.Field(...)`. Two property dereferences (`_capturedTenantId.Value`) vs one field dereference. EF Core's expression tree visitor will still parameterize the captured value per-DbContext-instance.

### 3.3 Updated legacy filters

```csharp
#pragma warning disable CS0618
modelBuilder.Entity<Project>().HasQueryFilter(e => e.TenantId == _capturedTenantId.Value);
modelBuilder.Entity<TaskItem>().HasQueryFilter(e => e.TenantId == _capturedTenantId.Value);
modelBuilder.Entity<AuditRecord>().HasQueryFilter(e => e.TenantId == _capturedTenantId.Value);
#pragma warning restore CS0618
```

Minor change: comparand is `_capturedTenantId.Value` (extracted string) instead of `_currentTenantId` (raw string). Filter expression shape is functionally identical.

---

## 4. Backward-compatibility considerations

### 4.1 DI registration

`Sunfish.Bridge/Program.cs` (or wherever the DbContext is registered):

Current path (pseudo):
```csharp
services.AddDbContext<SunfishBridgeDbContext>(...);
services.AddScoped<Foundation.Authorization.ITenantContext, DemoTenantContext>();
// DemoTenantContext also satisfies Foundation.MultiTenancy.ITenantContext via sum-interface
```

Post-Step-2.0:
```csharp
services.AddDbContext<SunfishBridgeDbContext>(...);
services.AddSunfishTenantContext<DemoTenantContext>();    // ADR 0091 R2 amendment A1 helper
// AddSunfishTenantContext registers DemoTenantContext as:
//   - Foundation.MultiTenancy.ITenantContext (NEW: needed by SunfishBridgeDbContext post-Step-2.0)
//   - Foundation.Authorization.ICurrentUser
//   - Foundation.Authorization.IAuthorizationContext
//   - Foundation.Authorization.ITenantContext (facade; existing consumers)
// All four interfaces resolve to the SAME scoped instance per amendment A1.
```

The `AddSunfishTenantContext<TConcrete>` helper (ADR Step 1 work; in flight per Engineer's PR #44) is the right registration path.

### 4.2 Test fixtures

Any test that constructs `SunfishBridgeDbContext` directly (without DI) must update its tenant-context mock:

```csharp
// Before (Step 1):
var ctx = new SunfishBridgeDbContext(options, modules, demoTenantContext);
// where demoTenantContext : Foundation.Authorization.ITenantContext (the facade)

// After (Step 2.0):
var ctx = new SunfishBridgeDbContext(options, modules, demoTenantContext);
// where demoTenantContext : Foundation.MultiTenancy.ITenantContext (or implements both)
// — DemoTenantContext implements both via the sum-interface; tests using it don't change
// — Custom mock test fixtures may need to add Foundation.MultiTenancy.ITenantContext.Tenant property
```

**Test-fixture review checklist (Engineer pre-flight):**

```bash
grep -rn "new SunfishBridgeDbContext" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/ --include="*.cs" 2>&1
grep -rn "ITenantContext\s*=\s*" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge.Data.Tests*/ --include="*.cs" 2>&1
```

Each call site that mocks `ITenantContext` for direct DbContext construction needs the `Tenant` property populated (or use the facade-backed concrete `DemoTenantContext`).

### 4.3 Migrations infrastructure

Per ADR §"Step 2.0" + amendment A4: migrations route through `MigrationTenantContext` + a dedicated migration DbContext.

**Current state:** `MigrationTenantContext` is referenced in the codebase (per ADR §A0 cited-symbol audit, "Existing (modified Step 2: narrows to `Foundation.MultiTenancy.ITenantContext` only)"). The migration DbContext that consumes it may not yet exist as a separate type — needs verification.

**Required design for migration DbContext:**

```csharp
public class MigrationDbContext : DbContext
{
    private readonly IEnumerable<ISunfishEntityModule> _modules;

    public MigrationDbContext(
        DbContextOptions<MigrationDbContext> options,
        IEnumerable<ISunfishEntityModule> modules)
        : base(options)
    {
        _modules = modules;
        // NO tenant context capture
        // NO tenant query filters applied
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureTenantRegistration(modelBuilder);    // shared with SunfishBridgeDbContext
        // ... legacy PM-domain mappings ...
        // ... module configurations ...
        // NO ApplyTenantQueryFilters call
    }
}
```

Key properties:
- Does NOT take `ITenantContext` in constructor (migrations need cross-tenant write access).
- Does NOT apply tenant query filters.
- Configured separately in `Program.cs` for the migration runner path.
- EF Core migration commands (`dotnet ef migrations add`, `database update`) target THIS DbContext, NOT `SunfishBridgeDbContext`.

**ALTERNATIVE design (smaller diff):** keep migrations on `SunfishBridgeDbContext` but inject `MigrationTenantContext` (which provides `Tenant.Id = TenantId.System`). The A4 guard would need a narrow exception for this case — but that breaks the fail-closed contract. **NOT RECOMMENDED.** Migrations should use a separate DbContext.

### 4.4 ADR 0092 dependency unblock

Per directive: "De-risks Engineer's eventual work (which unblocks ADR 0092 Step 2 EF Core query-filter PRs per B2 BLOCKER)."

ADR 0092 Step 2 EF Core query-filter PRs require:
- Predictable `_capturedTenantId.Value` shape (typed extraction; this Step 2.0 provides)
- Fail-closed-on-unresolved-tenant contract (this Step 2.0 provides via A3)
- Sentinel/null exclusion in query path (this Step 2.0 provides via A4)

After Step 2.0 lands, ADR 0092 Step 2 can ship its query-filter contract changes against a stable DbContext shape.

---

## 5. A5 regression test pseudo-code (highest implementation risk)

The A5 test is the most complex part of Step 2.0. Per ADR §"Step 2.0 PR readiness — Tests": *"A5 regression test on populated DB: one row per known tenant + sentinel row + null-value row; per-tenant isolation; sentinel/null exclusion; unresolved DbContext throws or zero rows."*

### 5.1 Test seeding pattern

```csharp
public sealed class A5_TenantIsolationRegressionTests : IClassFixture<MigrationDbFixture>
{
    private readonly MigrationDbFixture _migrationFixture;

    public A5_TenantIsolationRegressionTests(MigrationDbFixture migrationFixture)
    {
        _migrationFixture = migrationFixture;
    }

    [Fact]
    public async Task A5_PopulatedDb_PerTenantIsolation_NoSentinelLeak_NoNullLeak()
    {
        // ARRANGE — seed via MigrationDbContext (cross-tenant write access)
        await _migrationFixture.ResetAsync();    // truncate test tables

        using var migContext = _migrationFixture.CreateMigrationContext();

        var tenantA = new TenantId("tenant-a-known");
        var tenantB = new TenantId("tenant-b-known");
        var tenantC = new TenantId("tenant-c-known");

        // Use IMustHaveTenant entities from blocks-financial-ar (Invoice) — chosen because
        // they're the most-canonical post-cohort-2 tenant-keyed entity. Alternative: use a
        // test-only entity inside Sunfish.Bridge.Data.Tests that explicitly implements
        // IMustHaveTenant; cleaner but requires test-only registration.
        migContext.Invoices.Add(new Invoice { TenantId = tenantA, /* invoice fields */ });
        migContext.Invoices.Add(new Invoice { TenantId = tenantB, /* */ });
        migContext.Invoices.Add(new Invoice { TenantId = tenantC, /* */ });

        // Sentinel row — only inserted via MigrationDbContext (SunfishBridgeDbContext would
        // reject TenantId.System at the construction guard)
        migContext.Invoices.Add(new Invoice { TenantId = TenantId.System, /* */ });

        // Null-value row — direct-SQL insert because TenantId.Create likely guards against
        // null-string construction. Use ExecuteSqlRaw to bypass entity validation:
        await migContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO invoices (id, tenant_id_value, /* other cols */) VALUES (gen_random_uuid(), NULL, /* */)");

        await migContext.SaveChangesAsync();

        // ACT + ASSERT for each known tenant
        foreach (var tenant in new[] { tenantA, tenantB, tenantC })
        {
            using var ctx = _migrationFixture.CreateContextForTenant(tenant);
            var visibleInvoices = await ctx.Invoices.AsNoTracking().ToListAsync();

            // (a) sees own row
            Assert.Contains(visibleInvoices, i => i.TenantId.Value == tenant.Value);

            // (b) does not see sentinel row
            Assert.DoesNotContain(visibleInvoices, i => i.TenantId.IsSystemSentinel);

            // (c) does not see null-value row
            Assert.DoesNotContain(visibleInvoices, i => i.TenantId.Value == null);

            // (d) does not see other tenants' rows (per-tenant isolation)
            Assert.All(visibleInvoices, i => Assert.Equal(tenant.Value, i.TenantId.Value));
        }
    }

    [Fact]
    public void A3_UnresolvedTenant_DbContextConstruction_Throws()
    {
        // ARRANGE
        var unresolvedTenantCtx = new UnresolvedTenantContext();    // Tenant = null
        var options = _migrationFixture.GetDbContextOptions();
        var modules = _migrationFixture.GetModules();

        // ACT + ASSERT
        var ex = Assert.Throws<InvalidOperationException>(
            () => new SunfishBridgeDbContext(options, modules, unresolvedTenantCtx));
        Assert.Contains("ADR 0091 R2 amendment A3", ex.Message);
    }

    [Fact]
    public void A4_SentinelTenantId_DbContextConstruction_Throws()
    {
        // ARRANGE
        var sentinelTenantCtx = new SystemSentinelTenantContext();    // Tenant.Id = TenantId.System
        var options = _migrationFixture.GetDbContextOptions();
        var modules = _migrationFixture.GetModules();

        // ACT + ASSERT
        var ex = Assert.Throws<ArgumentException>(
            () => new SunfishBridgeDbContext(options, modules, sentinelTenantCtx));
        Assert.Contains("ADR 0091 R2 amendment A4", ex.Message);
    }

    [Fact]
    public void A4_NullTenantIdValue_DbContextConstruction_Throws()
    {
        // ARRANGE
        var nullValueTenantCtx = new NullValueTenantContext();    // Tenant.Id.Value = null
        var options = _migrationFixture.GetDbContextOptions();
        var modules = _migrationFixture.GetModules();

        // ACT + ASSERT
        var ex = Assert.Throws<ArgumentException>(
            () => new SunfishBridgeDbContext(options, modules, nullValueTenantCtx));
        Assert.Contains("ADR 0091 R2 amendment A4", ex.Message);
    }

    [Fact]
    public void A4_LiteralSystemString_DbContextConstruction_Throws()
    {
        // ARRANGE — direct construction of TenantId from "__system__" string (bypassing
        // factory paths that would guard via IsSystemSentinel check)
        // (Assumes TenantId has a public string-arg constructor or internal-visible factory)
        var literalSystemTenantCtx = new LiteralSystemTenantContext();    // Tenant.Id.Value = "__system__"
        var options = _migrationFixture.GetDbContextOptions();
        var modules = _migrationFixture.GetModules();

        // ACT + ASSERT
        var ex = Assert.Throws<ArgumentException>(
            () => new SunfishBridgeDbContext(options, modules, literalSystemTenantCtx));
        Assert.Contains("ADR 0091 R2 amendment A4", ex.Message);
    }
}
```

### 5.2 Test fixture design (`MigrationDbFixture`)

```csharp
public sealed class MigrationDbFixture : IAsyncLifetime
{
    private readonly DbContextOptions<MigrationDbContext> _migrationOptions;
    private readonly DbContextOptions<SunfishBridgeDbContext> _bridgeOptions;
    private readonly IEnumerable<ISunfishEntityModule> _modules;

    public MigrationDbFixture()
    {
        // Use PostgreSQL test container OR a SQLite in-memory database depending
        // on cohort-2's persistence-hand-off resolution. PostgreSQL is more
        // faithful (jsonb columns, SERIALIZABLE isolation) but slower; SQLite is
        // fast but doesn't enforce all production constraints.
        // Recommendation: PostgreSQL with Testcontainers for A5 specifically
        // (jsonb + SERIALIZABLE matter for sentinel-bypass attack vectors).
        // ...
    }

    public async Task ResetAsync() { /* truncate test tables */ }
    public MigrationDbContext CreateMigrationContext() { /* return new MigrationDbContext(...) */ }
    public SunfishBridgeDbContext CreateContextForTenant(TenantId tenant) { /* return new SunfishBridgeDbContext(...) */ }
    public DbContextOptions<SunfishBridgeDbContext> GetDbContextOptions() => _bridgeOptions;
    public IEnumerable<ISunfishEntityModule> GetModules() => _modules;

    public Task InitializeAsync() { /* spin up test DB */ }
    public Task DisposeAsync() { /* tear down */ }
}
```

### 5.3 Helper mock tenant-context types

```csharp
internal sealed class UnresolvedTenantContext : Foundation.MultiTenancy.ITenantContext
{
    public TenantMetadata? Tenant => null;
    public bool IsResolved => false;
}

internal sealed class SystemSentinelTenantContext : Foundation.MultiTenancy.ITenantContext
{
    public TenantMetadata? Tenant { get; } = new TenantMetadata
    {
        Id = TenantId.System,
        Name = "system",
    };
    public bool IsResolved => true;
}

internal sealed class NullValueTenantContext : Foundation.MultiTenancy.ITenantContext
{
    public TenantMetadata? Tenant { get; } = new TenantMetadata
    {
        Id = new TenantId(null!),    // intentional null
        Name = "null-value",
    };
    public bool IsResolved => true;
}

internal sealed class LiteralSystemTenantContext : Foundation.MultiTenancy.ITenantContext
{
    public TenantMetadata? Tenant { get; } = new TenantMetadata
    {
        Id = new TenantId("__system__"),    // literal sentinel (bypasses IsSystemSentinel check)
        Name = "literal-system",
    };
    public bool IsResolved => true;
}
```

### 5.4 Implementation risks for A5 test seeding

1. **TenantId factory guards `null` and `"__system__"` strings.** If `TenantId.Create(string)` rejects these inputs, the mock contexts can't construct them via the standard path. Need internal-visible factory OR direct field assignment.
2. **`Invoice` entity invariants may reject `TenantId.IsSystemSentinel`.** If the Invoice constructor validates the tenant id (per cohort-2 substrate work), the sentinel-row seed via `MigrationDbContext.Invoices.Add(...)` may fail at entity construction. Need direct SQL insert (`ExecuteSqlRaw`) for the sentinel + null-value rows.
3. **`IClassFixture<MigrationDbFixture>`** is xUnit; switch to NUnit's `[OneTimeSetUp]` or MSTest equivalents if cohort-1's test framework choice differs from cohort-2's.
4. **Test database lifetime** — A5 needs an isolated test DB; reusing the dev DB risks pollution. Testcontainers + ephemeral PostgreSQL is the safest pattern.
5. **Migration runner test** — per ADR §"Step 2.0 PR readiness — Tests": *"Migration runner test: MigrationTenantContext does NOT go through SunfishBridgeDbContext; migrations use a dedicated migration DbContext path."* This is a separate test (likely in the migrations test project) that asserts the migration runner DOES NOT throw at SunfishBridgeDbContext construction (because migrations don't construct it at all).

---

## 6. EF Core preview surface considerations

### 6.1 Current preview level

Fleet's EF Core version is preview.4 era (per `feedback_nu1510_suppression` memory entry: "After .NET preview.3 → preview.4 bumps, some packages need direct PackageReference declarations"). Step 2.0 PR should verify the EF Core preview level at commit time + flag any breaking-changes-on-trunk vs preview.4.

### 6.2 `HasQueryFilter` API stability

`ModelBuilder.Entity<T>().HasQueryFilter(predicate)` has been stable since EF Core 2.x. The expression-tree-based path used in `ApplyTenantQueryFilters` is also stable. Preview.5+ has not (as of January 2026 knowledge cutoff) introduced breaking changes to this API.

**Forward-watch:** EF Core 11+ may introduce named query filters (multiple filters per entity); if it does, the Step 2.0 implementation's single-filter pattern is forward-compatible (named filters are additive).

### 6.3 Expression-tree compatibility

The captured-field-as-Constant-Expression pattern (`Expression.Field(Expression.Constant(this), nameof(_capturedTenantId))`) is the canonical EF Core query-filter pattern; documented + tested by Microsoft. Step 2.0 preserves this pattern.

### 6.4 Potential preview.5+ concerns

- **Complex types** (preview.5+) may eventually replace value converters for jsonb columns like `SupportContacts`. NOT a Step 2.0 concern; flagged for awareness.
- **AOT compatibility** — EF Core's expression-tree query filters generate IL at runtime; .NET 11 AOT compilation may require explicit annotations. NOT a Step 2.0 concern (Sunfish ships JIT); flagged for awareness if AOT becomes a fleet target.

---

## 7. Implementation risks (Engineer focus)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| A5 test seeding fails due to entity invariants rejecting sentinel/null TenantId | Medium | High (test cannot run) | Direct SQL insert for sentinel + null-value rows; bypass entity validation; document in test code |
| `TenantId.Create("__system__")` rejected by factory guard | Medium | Medium (test mock unconstructable) | Use internal-visible factory OR direct field assignment in LiteralSystemTenantContext |
| Migration DbContext doesn't yet exist as a separate type | Medium | Medium (Step 2.0 PR also creates it) | Step 2.0 PR scope grows; OR refactor migrations to current DbContext with explicit A4 carve-out (NOT RECOMMENDED) |
| Test fixtures using direct DbContext construction fail to compile | High | Low (mechanical fix) | Grep + update all `new SunfishBridgeDbContext(...)` call sites |
| EF Core preview.N introduces breaking change to HasQueryFilter | Low | High (Step 2.0 blocked) | Verify against trunk EF Core at PR commit time |
| jsonb SupportContacts converter incompatible with new constructor | Low | Low (separable concern) | SupportContacts is on TenantRegistration (NOT IMustHaveTenant); unaffected by query filter changes |
| Sec-eng SPOT-CHECK flags A4 literal-string check as redundant | Low | Low (defense-in-depth justifiable) | ADR §A4 explicitly requires both `IsSystemSentinel` + literal `"__system__"` checks; cite ADR |
| Migration runner test discovers SunfishBridgeDbContext IS instantiated by migrations | Medium | High (architectural rework) | Pre-flight grep before Step 2.0 PR: search for `dotnet ef migrations` paths + verify they target a separate context |
| Performance regression on populated A5 test (testcontainers cold-start) | Medium | Low (test annoyance, not prod) | Cache containers; warm before run |

---

## 8. Open questions

For Admiral routing per `feedback_questions_via_inbox`:

### For .NET-architect council

1. **Migration DbContext shape — separate type (recommended) vs SunfishBridgeDbContext with A4 carve-out?** ONR recommends separate type; council attests or amends.
2. **Test fixture migration scope — is Step 2.0 PR the right place to update ALL direct-construction test fixtures, OR should each test project's update be a separate follow-on PR?** Likely Step 2.0 to keep CI green; council confirms.
3. **EF Core preview level pinning for Step 2.0 PR — should the PR pin a specific preview.N, OR ship against trunk?** Affects regression test stability.

### For security-engineering council

1. **A4 literal `"__system__"` defense-in-depth check — confirm or amend.** ADR §A4 requires both `IsSystemSentinel` + literal string; ONR includes both in the pseudo-code; council attests.
2. **A5 test database choice — PostgreSQL via Testcontainers (recommended) vs SQLite in-memory?** PostgreSQL is more faithful (jsonb, SERIALIZABLE); SQLite is faster.
3. **Migration runner sanity test — recommended shape: "migrations do NOT construct SunfishBridgeDbContext; if they do, fail loudly." Confirm the test exists OR ships with Step 2.0.**

### For CIC

1. **ADR 0092 Step 2 EF Core query-filter PR sequencing — does Step 2.0 land BEFORE 0092 Step 2 (per directive's "unblocks ADR 0092 Step 2 EF Core query-filter PRs"), OR can they ship in parallel?** ONR recommends sequential (Step 2.0 → 0092 Step 2) to avoid contract drift.
2. **Step 2.1+ batched endpoint migration timing — sequenced after Step 2.0 ships, OR in parallel with Step 2.0?** Per ADR §"Step 2.0 — DbContext query-filter rewrite (dedicated PR; Revision 2 amendment 10)": Step 2.0 is single PR; Step 2.1+ are batched. Recommend strict serial.

---

## 9. Sources cited

### Primary sources

1. `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` Rev 2 — Accepted 2026-05-19T02:40Z per `admiral-status-2026-05-19T02-40Z-adr-0091-promoted-to-accepted.md`. Step 2.0 spec at §"Compatibility plan — Step 2.0 — DbContext query-filter rewrite (dedicated PR; Revision 2 amendment 10)".
2. `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` (167 lines verified 2026-05-20T12:00Z) — current implementation; all line references in §1 verified against this file.
3. `shipyard/docs/adrs/0092-itenantscopedrepository-fleet-mark.md` (referenced; not deeply inspected; cited per directive as the consumer of Step 2.0's contract).
4. `coordination/inbox/admiral-ruling-2026-05-18T03-55Z-onr-adr-0091-consolidated-council-amendments.md` — consolidated amendment list; A3 + A4 + A5 are Step 2 BLOCKERS per §"Step 2 BLOCKERS".
5. `coordination/inbox/council-verdict-2026-05-18T03-40Z-security-engineering-adr-0091.md` — security-engineering Item 5 (balance race + persistence hand-off requirement) inform the typed-TenantId capture-once-at-construction pattern.

### Secondary sources

6. `shipyard/packages/foundation-multitenancy/ITenantContext.cs` — `Tenant: TenantMetadata?` + `IsResolved` shape; the narrowed surface Step 2.0 uses.
7. `shipyard/packages/foundation-multitenancy/TenantMetadata.cs` — `Id: TenantId` + `Name: string`; the type Step 2.0 reads from.
8. `shipyard/packages/foundation/Assets/Common/TenantId.cs` — `readonly record struct` with `Value: string`, `IsSystemSentinel`, `System` sentinel, `Default [Obsolete]`. ADR 0084 reserved-prefix guard documented here.
9. `shipyard/packages/foundation-persistence/IMustHaveTenant.cs` — `TenantId: TenantId` property; the canonical post-cohort-2 entity marker.

### Tertiary sources

10. `coordination/inbox/admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` — parent directive (Item #3).
11. EF Core 10/11 preview documentation (transitive; HasQueryFilter API stability claim).
12. Microsoft EF Core query-filter expression-tree patterns (documented at Microsoft.EntityFrameworkCore.Modeling level; the captured-field-as-constant pattern).
13. Testcontainers for .NET (recommended for PostgreSQL-backed A5 test isolation).

---

## 10. What ONR does next

Returns to research queue. Per proceed-continuously discipline:

- Item #3 deliverable complete (this doc).
- File `onr-status-*-research-queue-item-3-adr-0091-step-2-0-research-complete.md` (questions surfaced in §8 captured inline per `feedback_questions_via_inbox` — Admiral routes).
- Proceed to Item #4: WS-E (W#20 phases 4-9) blocks-crew-comms hand-off re-authoring.

— ONR, 2026-05-20T12:00Z
