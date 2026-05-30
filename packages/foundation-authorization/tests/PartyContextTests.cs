using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization.DependencyInjection;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Authorization.Tests;

/// <summary>
/// Verifies the canonical principal→party seam: the in-memory
/// <see cref="IPrincipalPartyResolver"/> mapping, and the ambient <see cref="IPartyContext"/>
/// facade's same-token-derivation + tenant-scoped + never-body-supplied invariants.
/// </summary>
public class PartyContextTests
{
    private static readonly Guid AlicePartyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BobPartyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Configurable principal standing in for the wired sum-interface facade.</summary>
    private sealed class FakePrincipal : ITenantContext
    {
        public FakePrincipal(string userId, string? tenantId)
        {
            UserId = userId;
            Tenant = tenantId is null
                ? null
                : new TenantMetadata { Id = new TenantId(tenantId), Name = tenantId };
        }

        public TenantMetadata? Tenant { get; }
        public string UserId { get; }
        public IReadOnlyList<string> Roles { get; } = Array.Empty<string>();
        public bool HasPermission(string permission) => false;
    }

    private static InMemoryPrincipalPartyResolver SeededResolver() =>
        new(new[]
        {
            new PrincipalPartyMapping(new TenantId("tenant-a"), "alice", AlicePartyId),
            new PrincipalPartyMapping(new TenantId("tenant-b"), "bob", BobPartyId),
        });

    // ── Resolver primitive ────────────────────────────────────────────────────

    [Fact]
    public async Task InMemoryResolver_ResolvesSeededMapping()
    {
        var resolver = SeededResolver();
        var party = await resolver.ResolveAsync("alice", new TenantId("tenant-a"));
        Assert.Equal(AlicePartyId, party);
    }

    [Fact]
    public async Task InMemoryResolver_ReturnsNull_ForUnknownUser()
    {
        var resolver = SeededResolver();
        var party = await resolver.ResolveAsync("nobody", new TenantId("tenant-a"));
        Assert.Null(party);
    }

    [Fact]
    public async Task InMemoryResolver_IsTenantScoped_SameUserDifferentTenantDoesNotMatch()
    {
        var resolver = SeededResolver();
        // 'alice' is seeded in tenant-a only; asking under tenant-b must not match.
        var party = await resolver.ResolveAsync("alice", new TenantId("tenant-b"));
        Assert.Null(party);
    }

    // ── Facade happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task PartyContext_ResolvesCurrentPrincipalParty()
    {
        var ctx = new PartyContext(new FakePrincipal("alice", "tenant-a"), SeededResolver());
        var party = await ctx.GetCurrentPartyIdAsync();
        Assert.Equal(AlicePartyId, party);
    }

    [Fact]
    public async Task PartyContext_DerivesTenantFromSameInjectedPrincipal()
    {
        // 'alice' is seeded under tenant-a. A principal carrying userId 'alice' but tenant-b
        // resolves NOTHING — proving the tenant used for resolution comes from the principal,
        // not a default or a caller-supplied value.
        var ctx = new PartyContext(new FakePrincipal("alice", "tenant-b"), SeededResolver());
        await Assert.ThrowsAsync<PrincipalPartyResolutionException>(
            () => ctx.GetCurrentPartyIdAsync().AsTask());
    }

    // ── Throw paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PartyContext_Throws_WhenNoAuthenticatedPrincipal()
    {
        var ctx = new PartyContext(new FakePrincipal(string.Empty, "tenant-a"), SeededResolver());
        await Assert.ThrowsAsync<PrincipalPartyResolutionException>(
            () => ctx.GetCurrentPartyIdAsync().AsTask());
    }

    [Fact]
    public async Task PartyContext_Throws_WhenTenantUnresolved()
    {
        var ctx = new PartyContext(new FakePrincipal("alice", tenantId: null), SeededResolver());
        await Assert.ThrowsAsync<PrincipalPartyResolutionException>(
            () => ctx.GetCurrentPartyIdAsync().AsTask());
    }

    [Fact]
    public async Task PartyContext_Throws_WhenNoPartyMapsToPrincipal()
    {
        var ctx = new PartyContext(new FakePrincipal("carol", "tenant-a"), SeededResolver());
        await Assert.ThrowsAsync<PrincipalPartyResolutionException>(
            () => ctx.GetCurrentPartyIdAsync().AsTask());
    }

    [Fact]
    public async Task PartyContext_CrossTenantPrincipalCannotResolveAnotherTenantsParty()
    {
        // 'bob' (a real party in tenant-b) cannot be resolved by a tenant-a principal.
        var ctx = new PartyContext(new FakePrincipal("bob", "tenant-a"), SeededResolver());
        await Assert.ThrowsAsync<PrincipalPartyResolutionException>(
            () => ctx.GetCurrentPartyIdAsync().AsTask());
    }

    // ── DI wiring (end-to-end through AddSunfishTenantContext) ─────────────────

    private sealed class DemoPrincipal : ITenantContext
    {
        public TenantMetadata? Tenant { get; } = new()
        {
            Id = new TenantId("tenant-a"),
            Name = "tenant-a",
        };
        public string UserId => "alice";
        public IReadOnlyList<string> Roles { get; } = Array.Empty<string>();
        public bool HasPermission(string permission) => false;
    }

    [Fact]
    public async Task AddSunfishPartyContext_RegistersFacadeAndResolver_EndToEndResolution()
    {
        var services = new ServiceCollection();
        services.AddSunfishTenantContext<DemoPrincipal>();
        services.AddSunfishPartyContext(seed =>
            seed.Add(new PrincipalPartyMapping(new TenantId("tenant-a"), "alice", AlicePartyId)));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IPrincipalPartyResolver>());
        var ctx = scope.ServiceProvider.GetRequiredService<IPartyContext>();

        var party = await ctx.GetCurrentPartyIdAsync(CancellationToken.None);
        Assert.Equal(AlicePartyId, party);
    }
}
