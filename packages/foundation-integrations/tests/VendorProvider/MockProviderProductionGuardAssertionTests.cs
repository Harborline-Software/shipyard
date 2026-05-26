using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Integrations;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.VendorProvider;

[Collection(EnvVarMutatingCollection.Name)]
public sealed class MockProviderProductionGuardAssertionTests
{
    private const string EnvKey = "ASPNETCORE_ENVIRONMENT";
    private const string OptOutKey = "SUNFISH_ALLOW_MOCK_PROVIDERS";
    private const string Postmark = "POSTMARK_API_KEY";

    // A non-mock real adapter (no IMockVendorProvider marker) for the swap path.
    private sealed class StubRealEmailProvider : IEmailProvider
    {
        public Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken ct)
            => Task.FromResult<EmailDispatchResult>(new EmailDispatchResult.Accepted("real"));
    }

    private static (IReadOnlyList<ServiceDescriptor> Snapshot, MockVendorEnvVarRegistry Registry) MockEmail()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        var registry = new MockVendorEnvVarRegistry();
        registry.Register(typeof(IEmailProvider), Postmark);
        return (services.ToList(), registry);
    }

    [Fact]
    public async Task NonProduction_IsInert()
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Development").Set(OptOutKey, null).Set(Postmark, null);
        var (snapshot, registry) = MockEmail();

        var guard = new MockProviderProductionGuardAssertion(snapshot, registry);
        await guard.StartAsync(CancellationToken.None); // no throw
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public async Task Production_MockWithOptOutTrue_IsInert(string optOut)
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Production").Set(OptOutKey, optOut).Set(Postmark, null);
        var (snapshot, registry) = MockEmail();

        var guard = new MockProviderProductionGuardAssertion(snapshot, registry);
        await guard.StartAsync(CancellationToken.None); // no throw
    }

    [Theory]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData(null)]
    public async Task Production_MockWithoutValidOptOut_Throws(string? optOut)
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Production").Set(OptOutKey, optOut).Set(Postmark, null);
        var (snapshot, registry) = MockEmail();

        var guard = new MockProviderProductionGuardAssertion(snapshot, registry);
        await Assert.ThrowsAsync<MockInProductionException>(() => guard.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Production_MockNoOptOut_NamesContractAndEnvVar()
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Production").Set(OptOutKey, null).Set(Postmark, null);
        var (snapshot, registry) = MockEmail();

        var guard = new MockProviderProductionGuardAssertion(snapshot, registry);
        var ex = await Assert.ThrowsAsync<MockInProductionException>(() => guard.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(IEmailProvider), ex.Message);
        Assert.Contains(Postmark, ex.Message);
        Assert.Contains(ex.Failures, f => f.ContractType == typeof(IEmailProvider) && f.EnvVarKey == Postmark);
    }

    [Fact]
    public async Task Production_RealAdapterEnvSet_IsInert()
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Production").Set(OptOutKey, null).Set(Postmark, "pm-secret-xyz");
        var (snapshot, registry) = MockEmail();

        var guard = new MockProviderProductionGuardAssertion(snapshot, registry);
        await guard.StartAsync(CancellationToken.None); // real env present → no throw
    }

    [Fact]
    public async Task Production_NoMockDescriptors_IsInert()
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey);
        env.Set(EnvKey, "Production").Set(OptOutKey, null);
        var services = new ServiceCollection();
        services.AddSingleton<IEmailProvider, StubRealEmailProvider>(); // not a mock
        var registry = new MockVendorEnvVarRegistry();

        var guard = new MockProviderProductionGuardAssertion(services.ToList(), registry);
        await guard.StartAsync(CancellationToken.None); // no throw
    }

    // ── Integration: full DI composition → guard resolved as IHostedService ──
    // Resolves the guard the way the generic host would (GetServices<IHostedService>)
    // and invokes StartAsync — proving AddMockProviderProductionGuard wired it +
    // the snapshot/registry flow end-to-end, without the full Host runtime.

    private static MockProviderProductionGuardAssertion ResolveGuard()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.UseVendorProviderIfConfigured<IEmailProvider, StubRealEmailProvider>(Postmark);
        services.AddMockProviderProductionGuard();
        var sp = services.BuildServiceProvider();
        return sp.GetServices<IHostedService>().OfType<MockProviderProductionGuardAssertion>().Single();
    }

    [Fact]
    public async Task Integration_ProdMockNoOptOut_GuardThrowsAtStart()
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Production").Set(OptOutKey, null).Set(Postmark, null);

        var guard = ResolveGuard();
        var ex = await Assert.ThrowsAsync<MockInProductionException>(() => guard.StartAsync(CancellationToken.None));
        Assert.Contains(Postmark, ex.Message);
    }

    [Fact]
    public async Task Integration_WithOptOut_GuardStartSucceeds()
    {
        using var env = new EnvVarScope(EnvKey, OptOutKey, Postmark);
        env.Set(EnvKey, "Production").Set(OptOutKey, "true").Set(Postmark, null);

        var guard = ResolveGuard();
        await guard.StartAsync(CancellationToken.None); // no throw
    }
}
