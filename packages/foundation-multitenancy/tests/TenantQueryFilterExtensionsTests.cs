using System.Reflection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.MultiTenancy.Tests;

/// <summary>
/// Unit tests for <see cref="TenantQueryFilterExtensions"/>.
/// Uses in-memory IQueryable via AsQueryable() — no EF Core dependency required.
/// </summary>
public class TenantQueryFilterExtensionsTests
{
    // ---------------------------------------------------------------------------
    // Minimal test entity implementing IMustHaveTenant
    // ---------------------------------------------------------------------------

    private sealed class TestEntity : IMustHaveTenant
    {
        public required TenantId TenantId { get; init; }
        public required string Name { get; init; }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ITenantContext ResolvedContext(string tenantValue) =>
        new StubTenantContext(new TenantMetadata { Id = new TenantId(tenantValue), Name = tenantValue });

    private static ITenantContext UnresolvedContext() =>
        new StubTenantContext(null);

    private sealed class StubTenantContext(TenantMetadata? tenant) : ITenantContext
    {
        public TenantMetadata? Tenant { get; } = tenant;
    }

    private static IQueryable<TestEntity> MixedTenantQuery() =>
        new[]
        {
            new TestEntity { TenantId = new TenantId("acme"), Name = "acme-1" },
            new TestEntity { TenantId = new TenantId("acme"), Name = "acme-2" },
            new TestEntity { TenantId = new TenantId("beta"), Name = "beta-1" },
            new TestEntity { TenantId = new TenantId("beta"), Name = "beta-2" },
            new TestEntity { TenantId = new TenantId("gamma"), Name = "gamma-1" },
        }.AsQueryable();

    // ---------------------------------------------------------------------------
    // Test 1: WhereTenant(ITenantContext) filters to current tenant
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_FiltersToCurrentTenant()
    {
        var ctx = ResolvedContext("acme");
        var query = MixedTenantQuery();

        var results = query.WhereTenant(ctx).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("acme", r.TenantId.Value));
        Assert.Contains(results, r => r.Name == "acme-1");
        Assert.Contains(results, r => r.Name == "acme-2");
    }

    // ---------------------------------------------------------------------------
    // Test 2: WhereTenant(TenantId) overload filters correctly
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_ByTenantId_FiltersCorrectly()
    {
        var betaId = new TenantId("beta");
        var query = MixedTenantQuery();

        var results = query.WhereTenant(betaId).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("beta", r.TenantId.Value));
        Assert.Contains(results, r => r.Name == "beta-1");
        Assert.Contains(results, r => r.Name == "beta-2");
    }

    // ---------------------------------------------------------------------------
    // Test 3: Unresolved tenant (Tenant is null) throws InvalidOperationException
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_OnUnresolvedTenant_ThrowsInvalidOperationException()
    {
        var unresolvedCtx = UnresolvedContext();
        var query = MixedTenantQuery();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            query.WhereTenant(unresolvedCtx).ToList());

        Assert.Contains("ITenantContext has no resolved tenant", ex.Message);
        Assert.Contains("ADR 0091", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // Test 4: WhereTenant is composable in a longer LINQ chain
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_ChainComposable()
    {
        var ctx = ResolvedContext("acme");
        var query = MixedTenantQuery();

        // Pre-filter: only names ending in "1"; post-filter: double-check tenant
        var results = query
            .Where(e => e.Name.EndsWith("1"))
            .WhereTenant(ctx)
            .Where(e => e.TenantId.Value.Length > 0)
            .ToList();

        Assert.Single(results);
        Assert.Equal("acme-1", results[0].Name);
        Assert.Equal("acme", results[0].TenantId.Value);
    }

    // ---------------------------------------------------------------------------
    // Test 5: Compile-time constraint is where T : IMustHaveTenant (reflection)
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_OnIMustHaveTenantConstraint_CompileTimeEnforced()
    {
        // Locate the WhereTenant<T>(IQueryable<T>, ITenantContext) overload via reflection
        var methods = typeof(TenantQueryFilterExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(TenantQueryFilterExtensions.WhereTenant))
            .ToList();

        Assert.Equal(2, methods.Count);

        foreach (var method in methods)
        {
            var typeParams = method.GetGenericArguments();
            Assert.Single(typeParams);

            var tParam = typeParams[0];
            var constraints = tParam.GetGenericParameterConstraints();

            Assert.Contains(constraints, c => c == typeof(IMustHaveTenant));
        }
    }

    // ---------------------------------------------------------------------------
    // Test 6: Single-tenant query returns all rows when all match
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_ReturnsAllRows_WhenAllMatchTenant()
    {
        var ctx = ResolvedContext("acme");
        var singleTenantQuery = new[]
        {
            new TestEntity { TenantId = new TenantId("acme"), Name = "a" },
            new TestEntity { TenantId = new TenantId("acme"), Name = "b" },
        }.AsQueryable();

        var results = singleTenantQuery.WhereTenant(ctx).ToList();

        Assert.Equal(2, results.Count);
    }

    // ---------------------------------------------------------------------------
    // Test 7: WhereTenant returns empty when no rows match
    // ---------------------------------------------------------------------------

    [Fact]
    public void WhereTenant_ReturnsEmpty_WhenNoRowsMatchTenant()
    {
        var ctx = ResolvedContext("unknown-tenant");
        var query = MixedTenantQuery();

        var results = query.WhereTenant(ctx).ToList();

        Assert.Empty(results);
    }

    // ---------------------------------------------------------------------------
    // Tests 8 / 9 / 10: sec-eng amendment C1 — sentinel TenantId rejection
    // ---------------------------------------------------------------------------
    //
    // The TenantId overload throws ArgumentException when given a sentinel
    // (default, TenantId.System, or any __-prefixed sentinel) so a silently
    // unscoped "all-tenants" predicate is structurally impossible. Cross-tenant
    // reads must go through the IgnoreQueryFilters attestation path per
    // ADR 0092 §A4/§B4.

    [Fact]
    public void WhereTenant_OnDefaultTenantId_ThrowsArgumentException()
    {
        var query = MixedTenantQuery();
        var ex = Assert.Throws<ArgumentException>(() => query.WhereTenant(default(TenantId)));
        Assert.Contains("sentinel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhereTenant_OnSystemSentinel_ThrowsArgumentException()
    {
        var query = MixedTenantQuery();
        var ex = Assert.Throws<ArgumentException>(() => query.WhereTenant(TenantId.System));
        Assert.Contains("sentinel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhereTenant_OnDoubleUnderscoreSentinel_ThrowsArgumentException()
    {
        var query = MixedTenantQuery();
        var ex = Assert.Throws<ArgumentException>(() => query.WhereTenant(new TenantId("__staging__")));
        Assert.Contains("sentinel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
