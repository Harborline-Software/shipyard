using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Foundation.Authorization.Tests;

/// <summary>
/// ADR 0095 Step 2 — bind-once tenant-context seed + the SeededTenantContext
/// adapter (the α-1 child-scope transition primitive).
/// </summary>
public sealed class MutableTenantContextSeedTests
{
    [Fact]
    public void TenantId_BeforeBind_IsNull()
        => Assert.Null(new MutableTenantContextSeed().TenantId);

    [Fact]
    public void Bind_ThenRead_ReturnsBoundTenant()
    {
        var seed = new MutableTenantContextSeed();
        var tenant = Guid.NewGuid();
        seed.Bind(tenant);
        Assert.Equal(tenant, seed.TenantId);
    }

    [Fact]
    public void Bind_Twice_Throws()
    {
        var seed = new MutableTenantContextSeed();
        seed.Bind(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => seed.Bind(Guid.NewGuid()));
    }

    [Fact]
    public void Bind_Concurrent_ExactlyOneWinner()
    {
        var seed = new MutableTenantContextSeed();
        int successes = 0;
        Parallel.For(0, 64, _ =>
        {
            try
            {
                seed.Bind(Guid.NewGuid());
                Interlocked.Increment(ref successes);
            }
            catch (InvalidOperationException)
            {
                // expected for all but the first
            }
        });
        Assert.Equal(1, successes);
        Assert.NotNull(seed.TenantId);
    }

    [Fact]
    public void SeededTenantContext_ResolvesTenantFromSeed()
    {
        var seed = new MutableTenantContextSeed();
        var ctx = new SeededTenantContext(seed);

        // before bind: no tenant; facade TenantId is empty-string per ADR 0091 default
        Assert.Null(ctx.Tenant);
        Assert.Equal(string.Empty, ((ITenantContext)ctx).TenantId);

        var tenant = Guid.NewGuid();
        seed.Bind(tenant);

        Assert.NotNull(ctx.Tenant);
        Assert.Equal(tenant.ToString(), ctx.Tenant!.Id.Value);
        Assert.Equal(tenant.ToString(), ((ITenantContext)ctx).TenantId);
        Assert.Empty(ctx.UserId);
        Assert.Empty(ctx.Roles);
        Assert.False(ctx.HasPermission("anything"));
    }

    [Fact]
    public void SeparateSeeds_DoNotLeakAcrossScopes()
    {
        var seedA = new MutableTenantContextSeed();
        var seedB = new MutableTenantContextSeed();
        var ctxA = new SeededTenantContext(seedA);
        var ctxB = new SeededTenantContext(seedB);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        seedA.Bind(tenantA);
        seedB.Bind(tenantB);

        Assert.Equal(tenantA.ToString(), ctxA.Tenant!.Id.Value);
        Assert.Equal(tenantB.ToString(), ctxB.Tenant!.Id.Value);
        Assert.NotEqual(ctxA.Tenant!.Id.Value, ctxB.Tenant!.Id.Value);
    }
}
