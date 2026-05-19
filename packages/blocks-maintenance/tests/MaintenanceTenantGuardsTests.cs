using NSubstitute;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// PR 0 Option D — service-layer tenant-isolation guards on
/// <see cref="InMemoryMaintenanceService"/>. Per admiral-ruling-2026-05-19T05-50Z
/// + ADR 0091 + ADR 0092: cross-tenant <c>Get*</c> returns uniform null +
/// emits <see cref="AuditEventType.TenantBoundaryViolation"/>; <c>List*</c>
/// silently filters; unresolved tenant context throws on production-ctor
/// service invocation.
/// </summary>
public class MaintenanceTenantGuardsTests
{
    private static readonly TenantId TenantA = new("tenant-A");
    private static readonly TenantId TenantB = new("tenant-B");

    private static ITenantContext Ctx(TenantId? id)
    {
        var stub = new StubTenantContext();
        stub.Set(id);
        return stub;
    }

    private static Vendor SeedVendor(VendorId id, TenantId tenant) => new()
    {
        Id = id,
        TenantId = tenant,
        DisplayName = $"Vendor for {tenant.Value}",
        Status = VendorStatus.Active,
        OnboardingState = VendorOnboardingState.Active,
    };

    // ──────────────────────────────────────────────────────────────────
    //  GetVendorAsync / GetWorkOrderAsync — cross-tenant returns null
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVendorAsync_CrossTenant_ReturnsNull()
    {
        var svc = new InMemoryMaintenanceService(Ctx(TenantA));
        // Seed a TenantB vendor directly into the service via the
        // permissive create path (CreateVendorAsync stamps from CurrentTenant
        // which is TenantA; so we use reflection-free injection through a
        // second instance that shares no state — but the simpler approach is
        // to create as TenantA, then mutate the stored Vendor's TenantId
        // through the public surface). For PR 0 scope we'll just seed via a
        // service constructed for TenantB and a shared dictionary is not
        // available; use the back-door of creating a TenantB-tenant Vendor
        // via a separate permissive-mode service is not portable. Instead,
        // we directly construct + insert the Vendor by going around the
        // public surface using a side-channel: a second service with the
        // TenantB context.
        //
        // Since the In-Memory store is per-instance, the simplest portable
        // assertion: call CreateVendorAsync under the TenantA context, then
        // verify the returned Vendor IS retrievable under TenantA but NOT
        // under a re-targeted service constructed for TenantB.
        var created = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "TestVendor" });
        Assert.Equal(TenantA, created.TenantId);

        // The vendor IS retrievable in its own tenant.
        Assert.NotNull(await svc.GetVendorAsync(created.Id));

        // Re-target: simulate a request arriving with TenantB context. Same
        // service instance (same backing store), but the context has flipped.
        var svcAsB = ReconfigureTenant(svc, TenantB);
        Assert.Null(await svcAsB.GetVendorAsync(created.Id));
    }

    [Fact]
    public async Task GetWorkOrderAsync_CrossTenant_ReturnsNull()
    {
        var svc = new InMemoryMaintenanceService(Ctx(TenantA));

        // Seed a request, then create a WO so it carries TenantA via the
        // CreateWorkOrderRequest.Tenant field. (CreateWorkOrderRequest takes
        // Tenant explicitly; not derived from context.)
        var req = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = new EntityId("urn", "test", "p1"),
            RequestedByDisplayName = "u",
            Description = "x",
            Priority = MaintenancePriority.Normal,
            RequestedDate = new DateOnly(2026, 5, 18),
        });

        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            Tenant = TenantA,
            RequestId = req.Id,
            AssignedVendorId = new VendorId("vendor-1"),
            ScheduledDate = new DateOnly(2026, 5, 18),
        });

        Assert.NotNull(await svc.GetWorkOrderAsync(wo.Id));

        var svcAsB = ReconfigureTenant(svc, TenantB);
        Assert.Null(await svcAsB.GetWorkOrderAsync(wo.Id));
    }

    // ──────────────────────────────────────────────────────────────────
    //  ListVendorsAsync / ListWorkOrdersAsync — tenant filter
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListVendorsAsync_TenantFiltered()
    {
        var ctx = new StubTenantContext();
        ctx.Set(TenantA);
        var svc = new InMemoryMaintenanceService(ctx);

        // Create 2 vendors under TenantA.
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "A1" });
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "A2" });

        // Flip to TenantB and create 1.
        ctx.Set(TenantB);
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "B1" });

        // Flip back to TenantA and list — should see only A1 + A2.
        ctx.Set(TenantA);
        var aResults = new List<Vendor>();
        await foreach (var v in svc.ListVendorsAsync(ListVendorsQuery.Empty))
            aResults.Add(v);
        Assert.Equal(2, aResults.Count);
        Assert.All(aResults, v => Assert.Equal(TenantA, v.TenantId));

        // Flip to TenantB and list — should see only B1.
        ctx.Set(TenantB);
        var bResults = new List<Vendor>();
        await foreach (var v in svc.ListVendorsAsync(ListVendorsQuery.Empty))
            bResults.Add(v);
        var single = Assert.Single(bResults);
        Assert.Equal(TenantB, single.TenantId);
    }

    [Fact]
    public async Task ListWorkOrdersAsync_TenantFiltered()
    {
        var svc = new InMemoryMaintenanceService(Ctx(TenantA));

        var req1 = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = new EntityId("urn", "test", "p1"),
            RequestedByDisplayName = "u",
            Description = "x",
            Priority = MaintenancePriority.Normal,
            RequestedDate = new DateOnly(2026, 5, 18),
        });
        await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            Tenant = TenantA,
            RequestId = req1.Id,
            AssignedVendorId = new VendorId("v"),
            ScheduledDate = new DateOnly(2026, 5, 18),
        });
        await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            Tenant = TenantB,
            RequestId = req1.Id,
            AssignedVendorId = new VendorId("v"),
            ScheduledDate = new DateOnly(2026, 5, 18),
        });

        // List under TenantA — should see only the TenantA WO.
        var aResults = new List<WorkOrder>();
        await foreach (var w in svc.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty))
            aResults.Add(w);
        Assert.Single(aResults);
        Assert.Equal(TenantA, aResults[0].Tenant);

        // Flip to TenantB — should see only the TenantB WO.
        var svcAsB = ReconfigureTenant(svc, TenantB);
        var bResults = new List<WorkOrder>();
        await foreach (var w in svcAsB.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty))
            bResults.Add(w);
        Assert.Single(bResults);
        Assert.Equal(TenantB, bResults[0].Tenant);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Audit emission on Get* tenant-boundary violation
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVendorAsync_CrossTenant_EmitsTenantBoundaryViolationAudit()
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

        var violations = auditTrail.Records
            .Where(r => r.EventType.Equals(AuditEventType.TenantBoundaryViolation))
            .ToList();
        Assert.Single(violations);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Test-mode parameterless ctor stays permissive (back-compat)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParameterlessCtor_StaysPermissive_NoTenantFiltering()
    {
        // The parameterless ctor is kept for test-rig back-compat. With no
        // tenant context resolved, the guards skip filtering — existing
        // tests that don't care about tenant boundaries keep working.
        var svc = new InMemoryMaintenanceService();
        var a1 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "a1" });

        // Vendor stamped with default(TenantId) since no context.
        Assert.Equal(default, a1.TenantId);

        // GetVendorAsync still works (permissive mode).
        Assert.NotNull(await svc.GetVendorAsync(a1.Id));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reuse the same backing store + audit deps by swapping the
    /// <c>StubTenantContext</c>'s resolved tenant in place. Production
    /// ITenantContext implementations (request-scoped) resolve from the
    /// ambient HTTP request; this test seam emulates that flip without
    /// constructing a second service instance with a separate store.
    /// </summary>
    private static InMemoryMaintenanceService ReconfigureTenant(
        InMemoryMaintenanceService existing,
        TenantId newTenant)
    {
        // The Stub holds a settable tenant; flip it.
        var ctxField = existing.GetType()
            .GetField("_tenantContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var stub = (StubTenantContext?)ctxField.GetValue(existing);
        Assert.NotNull(stub);
        stub!.Set(newTenant);
        return existing;
    }
}

internal sealed class StubTenantContext : ITenantContext
{
    private TenantMetadata? _tenant;

    public TenantMetadata? Tenant => _tenant;

    public void Set(TenantId? id)
    {
        _tenant = id is { } value
            ? new TenantMetadata { Id = value, Name = value.Value }
            : null;
    }
}

internal sealed class RecordingAuditTrail : IAuditTrail
{
    private readonly List<AuditRecord> _records = new();
    public IReadOnlyList<AuditRecord> Records => _records;

    public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        _records.Add(record);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var r in _records) yield return r;
        await Task.CompletedTask;
    }
}

internal sealed class PassthroughSigner : IOperationSigner
{
    public PrincipalId IssuerId => default;

    public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
    {
        return ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, default));
    }
}

