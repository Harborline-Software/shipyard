using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.DependencyInjection;

[Collection(nameof(EnvVarCollection))]
public class MockProviderProductionGuardAssertionTests
{
    private const string AspNetCoreEnvVar = "ASPNETCORE_ENVIRONMENT";
    private const string OptOutEnvVar = "SUNFISH_ALLOW_MOCK_PROVIDERS";
    private const string PostmarkApiKey = "POSTMARK_API_KEY";

    // (i) Non-prod bypass.

    [Fact]
    public async Task NonProduction_BypassesCheck()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Development"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, null));

        var (assertion, _) = BuildAssertionWithMockEmail();
        await assertion.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProductionEnv_NoMocks_Passes()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        // No mock vendor registrations at all.
        var registry = (IMockVendorEnvVarRegistry)services
            .First(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))
            .ImplementationInstance!;
        var assertion = new MockProviderProductionGuardAssertion(services, registry);

        await assertion.StartAsync(CancellationToken.None);
    }

    // (ii) Opt-out env-var bypass — case-variant probes.

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("tRuE")]
    public async Task ProductionEnv_OptOutTrueCaseVariants_Bypass(string optOutValue)
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, optOutValue),
            (PostmarkApiKey, null));

        var (assertion, _) = BuildAssertionWithMockEmail();
        await assertion.StartAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("")]
    public async Task ProductionEnv_NonTrueOptOut_FailsClosed(string optOutValue)
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, optOutValue),
            (PostmarkApiKey, null));

        var (assertion, _) = BuildAssertionWithMockEmail();
        await Assert.ThrowsAsync<MockInProductionException>(
            () => assertion.StartAsync(CancellationToken.None));
    }

    // (iii) Prod-with-mock-no-opt-out throws — message names contract + env-var.

    [Fact]
    public async Task ProductionEnv_MockRegistered_NoOptOut_NoRealAdapterEnv_Throws()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, null));

        var (assertion, _) = BuildAssertionWithMockEmail();
        var ex = await Assert.ThrowsAsync<MockInProductionException>(
            () => assertion.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(IEmailProvider), ex.Message);
        Assert.Contains(PostmarkApiKey, ex.Message);
        Assert.Single(ex.Failures);
        Assert.Equal(typeof(IEmailProvider), ex.Failures[0].ContractType);
        Assert.Equal(PostmarkApiKey, ex.Failures[0].EnvVarKey);
    }

    // (iv) Real-adapter env var present → no failure (swap should have fired).

    [Fact]
    public async Task ProductionEnv_RealAdapterEnvPresent_NoFailure()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, "pm-real-token"));

        // We construct a scenario where the mock is still in the descriptor
        // list — this would mean the caller registered the mock + recorded
        // the env-var in the registry but never called the swap helper.
        // Per ADR 0096 §D1c the assertion treats env-var-present as "the
        // swap was wired correctly elsewhere" and does not fail.
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        var registry = (MockVendorEnvVarRegistry)services
            .First(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))
            .ImplementationInstance!;
        registry.Register(typeof(IEmailProvider), PostmarkApiKey);

        var assertion = new MockProviderProductionGuardAssertion(services, registry);
        await assertion.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProductionEnv_RealAdapterEnvWhitespaceOnly_TreatedAsAbsent()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, "   "));

        var (assertion, _) = BuildAssertionWithMockEmail();
        await Assert.ThrowsAsync<MockInProductionException>(
            () => assertion.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProductionEnv_MultipleMocks_FailureListEnumeratesAll()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, null),
            ("TURNSTILE_SECRET_KEY", null));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.AddSunfishVendorProvider<Captcha.ICaptchaVerifier, Captcha.InMemoryCaptchaVerifier>();
        var registry = (MockVendorEnvVarRegistry)services
            .First(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))
            .ImplementationInstance!;
        registry.Register(typeof(IEmailProvider), PostmarkApiKey);
        registry.Register(typeof(Captcha.ICaptchaVerifier), "TURNSTILE_SECRET_KEY");

        var assertion = new MockProviderProductionGuardAssertion(services, registry);
        var ex = await Assert.ThrowsAsync<MockInProductionException>(
            () => assertion.StartAsync(CancellationToken.None));

        Assert.Equal(2, ex.Failures.Count);
        Assert.Contains(ex.Failures, f => f.ContractType == typeof(IEmailProvider) && f.EnvVarKey == PostmarkApiKey);
        Assert.Contains(ex.Failures, f => f.ContractType == typeof(Captcha.ICaptchaVerifier) && f.EnvVarKey == "TURNSTILE_SECRET_KEY");
    }

    // (v) Integration test — IHost.StartAsync throws.

    [Fact]
    public async Task IntegrationTest_HostStartAsync_ThrowsWhenProductionMockNoOptOut()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, null));

        var host = BuildMinimalHostWith(services =>
        {
            services.AddSunfishVendorProviderSubstrate();
            services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
            services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>(PostmarkApiKey);
        });
        var ex = await Assert.ThrowsAsync<MockInProductionException>(
            () => host.StartAsync(CancellationToken.None));

        Assert.Contains(nameof(IEmailProvider), ex.Message);
        Assert.Contains(PostmarkApiKey, ex.Message);
        await host.StopAsync(CancellationToken.None);
    }

    // (vi) Integration test — IHost.StartAsync succeeds with opt-out.

    [Fact]
    public async Task IntegrationTest_HostStartAsync_SucceedsWithOptOut()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, "true"),
            (PostmarkApiKey, null));

        var host = BuildMinimalHostWith(services =>
        {
            services.AddSunfishVendorProviderSubstrate();
            services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
            services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>(PostmarkApiKey);
        });
        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task IntegrationTest_HostStartAsync_SucceedsWhenRealAdapterEnvPresent()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null),
            (PostmarkApiKey, "pm-real-token"));

        var host = BuildMinimalHostWith(services =>
        {
            services.AddSunfishVendorProviderSubstrate();
            services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
            services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>(PostmarkApiKey);
        });
        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
    }

    // Factory-only descriptors are out of scope per ADR §D1c — confirm
    // the assertion does not crash and does not treat them as mock.

    [Fact]
    public async Task ProductionEnv_FactoryRegisteredService_IgnoredByScan()
    {
        using var scope = new EnvVarScope(
            (AspNetCoreEnvVar, "Production"),
            (OptOutEnvVar, null));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        // Factory-registered IEmailProvider — assertion cannot inspect it
        // for IMockVendorProvider membership without resolving the factory.
        services.AddSingleton<IEmailProvider>(_ => new MockEmailProvider());

        var registry = (IMockVendorEnvVarRegistry)services
            .First(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))
            .ImplementationInstance!;

        var assertion = new MockProviderProductionGuardAssertion(services, registry);
        await assertion.StartAsync(CancellationToken.None);
    }

    /// <summary>
    /// Builds a minimal <see cref="IHost"/> via <see cref="HostBuilder"/>
    /// (NOT <see cref="Host.CreateApplicationBuilder()"/>) so the test does
    /// not depend on the full Configuration / FileProviders transitive chain
    /// — the substrate-tier assertion only needs IServiceCollection +
    /// IHostedService surfaces, both of which the minimal HostBuilder
    /// provides.
    /// </summary>
    private static IHost BuildMinimalHostWith(Action<IServiceCollection> configureServices)
    {
        return new HostBuilder()
            .ConfigureServices((_, services) => configureServices(services))
            .Build();
    }

    private static (MockProviderProductionGuardAssertion Assertion, IServiceCollection Services) BuildAssertionWithMockEmail()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        var registry = (MockVendorEnvVarRegistry)services
            .First(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))
            .ImplementationInstance!;
        registry.Register(typeof(IEmailProvider), PostmarkApiKey);
        var assertion = new MockProviderProductionGuardAssertion(services, registry);
        return (assertion, services);
    }

    /// <summary>
    /// Stand-in real adapter for integration tests — implements
    /// <see cref="IEmailProvider"/> but NOT <see cref="IMockVendorProvider"/>.
    /// </summary>
    private sealed class FakeRealEmailProvider : IEmailProvider
    {
        public Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(EmailDispatchResult.Accepted("fake-real:" + Guid.NewGuid().ToString("N")));
    }
}
