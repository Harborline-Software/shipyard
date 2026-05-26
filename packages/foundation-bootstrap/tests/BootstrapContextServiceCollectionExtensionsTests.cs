using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Authorization.DependencyInjection;
using Sunfish.Foundation.Bootstrap.DependencyInjection;
using Xunit;

namespace Sunfish.Foundation.Bootstrap.Tests;

/// <summary>
/// ADR 0095 Step 1 — DI helper + startup-assertion tests. These verify
/// REGISTRATION-PRESENCE + composition-root opt-in (Rev 2 / .NET-arch A3),
/// NOT resolution-validation of the production concrete (which depends on
/// IHttpContextAccessor, null at StartAsync time). Mid-request member
/// population is Step 2 integration-test scope.
/// </summary>
public sealed class BootstrapContextServiceCollectionExtensionsTests
{
    // ── DI helper ────────────────────────────────────────────────────────────

    [Fact]
    public void AddSunfishBootstrapContext_RegistersIBootstrapContextScoped()
    {
        var sp = new ServiceCollection()
            .AddSunfishBootstrapContext<TestBootstrapContext>()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        var resolved = scope.ServiceProvider.GetService<IBootstrapContext>();

        Assert.NotNull(resolved);
        Assert.IsType<TestBootstrapContext>(resolved);
    }

    [Fact]
    public void AddSunfishBootstrapContext_InterfaceAliasesSameScopedConcrete()
    {
        var sp = new ServiceCollection()
            .AddSunfishBootstrapContext<TestBootstrapContext>()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        var viaInterface = scope.ServiceProvider.GetRequiredService<IBootstrapContext>();
        var viaConcrete = scope.ServiceProvider.GetRequiredService<TestBootstrapContext>();

        Assert.Same(viaConcrete, viaInterface);
    }

    [Fact]
    public void AddSunfishBootstrapContext_RegistersAssertionHostedServiceOnce()
    {
        var sp = new ServiceCollection()
            .AddSunfishBootstrapContext<TestBootstrapContext>()
            .AddSunfishBootstrapContext<TestBootstrapContext>() // idempotent (TryAddEnumerable)
            .BuildServiceProvider();

        var assertions = sp.GetServices<IHostedService>()
            .OfType<BootstrapAndTenantMutualExclusionAssertion>()
            .ToList();

        Assert.Single(assertions);
    }

    // ── Startup assertion ────────────────────────────────────────────────────

    [Fact]
    public async Task Assertion_BothHelpersCalled_StartAsyncSucceeds()
    {
        var sp = new ServiceCollection()
            .AddSunfishBootstrapContext<TestBootstrapContext>()
            .AddSunfishTenantContext<TestPostTenantContext>()
            .BuildServiceProvider();

        var assertion = new BootstrapAndTenantMutualExclusionAssertion(
            sp.GetRequiredService<IServiceScopeFactory>());

        await assertion.StartAsync(CancellationToken.None); // does not throw
    }

    [Fact]
    public async Task Assertion_OnlyBootstrapHelper_StartAsyncThrows()
    {
        // Post-tenant context family NOT registered → composition-root opt-in fails.
        var sp = new ServiceCollection()
            .AddSunfishBootstrapContext<TestBootstrapContext>()
            .BuildServiceProvider();

        var assertion = new BootstrapAndTenantMutualExclusionAssertion(
            sp.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => assertion.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Assertion_NullBootstrapConcrete_StartAsyncThrows()
    {
        // IBootstrapContext bound to a null factory → registration-presence fails.
        var sp = new ServiceCollection()
            .AddScoped<IBootstrapContext>(_ => null!)
            .AddSunfishTenantContext<TestPostTenantContext>()
            .BuildServiceProvider();

        var assertion = new BootstrapAndTenantMutualExclusionAssertion(
            sp.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => assertion.StartAsync(CancellationToken.None));
    }

    // ── Doubles ──────────────────────────────────────────────────────────────

    private sealed class TestBootstrapContext : IBootstrapContext
    {
        public string CorrelationId => "test-correlation-id";
        public IPAddress? ClientIp => null;
        public string? CaptchaToken => null;
        public string? IdempotencyKey => null;
        public string RateLimitBucketKey => "route:/test";
    }

    // Post-tenant concrete implementing the four-interface facade, so
    // AddSunfishTenantContext<TConcrete> can wire it (exercises the opt-in check).
    private sealed class TestPostTenantContext : Sunfish.Foundation.Authorization.ITenantContext
    {
        public Sunfish.Foundation.MultiTenancy.TenantMetadata? Tenant => null;
        public string UserId => "test-user";
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public bool HasPermission(string permission) => true;
    }
}
