using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization.DependencyInjection;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Authorization.Tests;

/// <summary>
/// ADR 0091 Step 1 — verifies the sum-interface facade resolves identically
/// through each of the 4 interface bindings (Foundation.MultiTenancy.ITenantContext,
/// ICurrentUser, IAuthorizationContext, Foundation.Authorization.ITenantContext)
/// and that the IHostedService startup assertion (A1) fails when the bindings
/// diverge.
/// </summary>
public class TenantContextFacadeTests
{
    private sealed class DemoTenantContext : ITenantContext
    {
        public TenantMetadata? Tenant { get; } = new()
        {
            Id = new TenantId("demo-tenant"),
            Name = "demo",
        };
        public string UserId => "demo-user";
        public IReadOnlyList<string> Roles { get; } = new[] { "admin", "tenant-owner" };
        public bool HasPermission(string permission) => true;
    }

    [Fact]
    public void AddSunfishTenantContext_AllFourInterfacesResolveToSameScopedInstance()
    {
        var services = new ServiceCollection();
        services.AddSunfishTenantContext<DemoTenantContext>();
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var multiTenancy = scope.ServiceProvider.GetRequiredService<MultiTenancy.ITenantContext>();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationContext>();
        var facade = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        Assert.Same(multiTenancy, currentUser);
        Assert.Same(currentUser, authorization);
        Assert.Same(authorization, facade);
    }

    [Fact]
    public async Task ScopeAssertion_FailsOnStartAsync_WhenBindingsDiverge()
    {
        // First wire the canonical bindings via AddSunfishTenantContext...
        var services = new ServiceCollection();
        services.AddSunfishTenantContext<DemoTenantContext>();

        // ...then divergently REPLACE the IAuthorizationContext binding (simulates a
        // future DI registration mistake that broke the same-instance invariant).
        services.AddScoped<IAuthorizationContext>(_ => new SecondaryAuthContext());

        using var provider = services.BuildServiceProvider();
        var assertion = new TenantContextScopeAssertion(provider.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => assertion.StartAsync(CancellationToken.None));
    }

    [Fact]
    public void Facade_DefaultImplementedStringTenantId_ReturnsTenantIdOrEmpty()
    {
        // When Tenant is set, facade.TenantId delegates to Tenant.Id.ToString().
        ITenantContext ctx = new DemoTenantContext();
        Assert.Equal("demo-tenant", ctx.TenantId);

        // When Tenant is null (unresolved), facade.TenantId returns string.Empty.
        ITenantContext unresolved = new UnresolvedTenantContext();
        Assert.Equal(string.Empty, unresolved.TenantId);
    }

    private sealed class SecondaryAuthContext : IAuthorizationContext
    {
        public bool HasPermission(string permission) => false;
    }

    private sealed class UnresolvedTenantContext : ITenantContext
    {
        public TenantMetadata? Tenant => null;
        public string UserId => string.Empty;
        public IReadOnlyList<string> Roles { get; } = Array.Empty<string>();
        public bool HasPermission(string permission) => false;
    }
}
