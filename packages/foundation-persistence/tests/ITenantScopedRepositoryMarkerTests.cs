using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Persistence.Tests;

/// <summary>
/// ADR 0092 Step 1 — verifies the <see cref="ITenantScopedRepository{TEntity,TKey}"/>
/// marker interface is consumable: it constrains <c>TEntity</c> to
/// <see cref="IMustHaveTenant"/>, requires no members from implementers,
/// and supports the canonical method-signature shape from the ADR's
/// "Canonical method-signature shape" §.
/// </summary>
public class ITenantScopedRepositoryMarkerTests
{
    /// <summary>
    /// Test entity implementing the constraint <see cref="IMustHaveTenant"/>.
    /// Confirms the marker accepts any IMustHaveTenant-bearing entity type.
    /// </summary>
    private sealed record TestEntity(TenantId TenantId, string Id) : IMustHaveTenant;

    /// <summary>
    /// Stable opaque key type for the marker's TKey slot.
    /// </summary>
    private readonly record struct TestKey(string Value);

    /// <summary>
    /// Sample consumer interface shaped per the ADR §"Canonical
    /// method-signature shape": TenantId as the first parameter on every
    /// method. No additional members are required by the marker itself —
    /// the interface body is the consumer's own contract.
    /// </summary>
    private interface ITestEntityRepository
        : ITenantScopedRepository<TestEntity, TestKey>
    {
        Task<TestEntity?> GetAsync(TenantId tenantId, TestKey id, CancellationToken ct);
        Task<System.Collections.Generic.IReadOnlyList<TestEntity>> ListAsync(TenantId tenantId, CancellationToken ct);
        Task AddAsync(TenantId tenantId, TestEntity entity, CancellationToken ct);
        Task UpsertAsync(TenantId tenantId, TestEntity entity, CancellationToken ct);
    }

    [Fact]
    public void Marker_HasNoMembers_NoContractWeightAtTypeSystem()
    {
        // The marker carries zero contractual weight at the type-system
        // level — implementers do not need to satisfy any method or
        // property. Enforcement of the per-tenant-filter invariant is
        // the Step 4 analyzer suite's responsibility (Step 4a/4b/4c
        // forward-watch TODOs in the marker xmldoc).
        var memberCount = typeof(ITenantScopedRepository<TestEntity, TestKey>).GetMembers().Length;
        Assert.Equal(0, memberCount);
    }

    [Fact]
    public void Marker_RequiresIMustHaveTenant_OnTEntity()
    {
        // TEntity is constrained to IMustHaveTenant — verifies the
        // generic constraint compiles and reflects in the type-system.
        var args = typeof(ITenantScopedRepository<,>).GetGenericArguments();
        var entityParam = args[0];
        var constraints = entityParam.GetGenericParameterConstraints();
        Assert.Contains(typeof(IMustHaveTenant), constraints);
    }

    [Fact]
    public void Marker_Composes_WithBespokeRepositoryInterface()
    {
        // The marker is opt-in: consumers add `: ITenantScopedRepository<...>`
        // to their existing bespoke interface. This test confirms the
        // composition works at the type-system level.
        var iface = typeof(ITestEntityRepository);
        Assert.Contains(typeof(ITenantScopedRepository<TestEntity, TestKey>), iface.GetInterfaces());
    }
}
