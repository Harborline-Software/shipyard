using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.DependencyInjection;

[Collection(nameof(EnvVarCollection))]
public class VendorProviderServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishVendorProviderSubstrate_RegistersRegistryAndAssertion()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();

        Assert.Contains(services, d => d.ServiceType == typeof(IMockVendorEnvVarRegistry));
        Assert.Contains(services, d => d.ImplementationFactory is not null
                                       && d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
    }

    [Fact]
    public void AddSunfishVendorProvider_RegistersMockAtSingletonByDefault()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(MockEmailProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddSunfishVendorProvider_RespectsExplicitLifetime()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>(ServiceLifetime.Scoped);

        var descriptor = services.First(d => d.ServiceType == typeof(IEmailProvider));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void UseVendorProviderIfConfigured_AlwaysRecordsRegistryMapping()
    {
        using var scope = new EnvVarScope(("POSTMARK_API_KEY_TEST_RECORD", null));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.UseVendorProviderIfConfigured<IEmailProvider, MockEmailProvider>("POSTMARK_API_KEY_TEST_RECORD");

        // Even though the env var is null and no swap fires, the registry
        // MUST still record the mapping per ADR 0096 §D1c amendment #3.
        var registry = services
            .First(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry))
            .ImplementationInstance as IMockVendorEnvVarRegistry;
        Assert.NotNull(registry);
        Assert.True(registry.TryGet(typeof(IEmailProvider), out var key));
        Assert.Equal("POSTMARK_API_KEY_TEST_RECORD", key);
    }

    [Fact]
    public void UseVendorProviderIfConfigured_EnvVarAbsent_KeepsMockRegistration()
    {
        using var scope = new EnvVarScope(("POSTMARK_API_KEY_TEST_ABSENT", null));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>("POSTMARK_API_KEY_TEST_ABSENT");

        var descriptor = services.First(d => d.ServiceType == typeof(IEmailProvider));
        Assert.Equal(typeof(MockEmailProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void UseVendorProviderIfConfigured_EnvVarEmptyString_TreatedAsAbsent()
    {
        // POSTMARK_API_KEY="" foot-gun closure per amendment #2.
        using var scope = new EnvVarScope(("POSTMARK_API_KEY_TEST_EMPTY", ""));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>("POSTMARK_API_KEY_TEST_EMPTY");

        var descriptor = services.First(d => d.ServiceType == typeof(IEmailProvider));
        Assert.Equal(typeof(MockEmailProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void UseVendorProviderIfConfigured_EnvVarPresent_SwapsToRealAdapter()
    {
        using var scope = new EnvVarScope(("POSTMARK_API_KEY_TEST_SWAP", "pm-test-token"));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>("POSTMARK_API_KEY_TEST_SWAP");

        var descriptor = services.First(d => d.ServiceType == typeof(IEmailProvider));
        Assert.Equal(typeof(FakeRealEmailProvider), descriptor.ImplementationType);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void UseVendorProviderIfConfigured_PreservesPriorLifetime(ServiceLifetime priorLifetime)
    {
        using var scope = new EnvVarScope(("POSTMARK_API_KEY_TEST_LIFETIME", "pm-test-token"));

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>(priorLifetime);
        services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>("POSTMARK_API_KEY_TEST_LIFETIME");

        var descriptor = services.First(d => d.ServiceType == typeof(IEmailProvider));
        Assert.Equal(priorLifetime, descriptor.Lifetime);
        Assert.Equal(typeof(FakeRealEmailProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void UseVendorProviderIfConfigured_NullOrWhitespaceEnvVarKey_Throws()
    {
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();

        Assert.Throws<ArgumentException>(() =>
            services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>(""));
        Assert.Throws<ArgumentException>(() =>
            services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>("   "));
    }

    [Fact]
    public void UseVendorProviderIfConfigured_WithoutSubstrateInit_Throws()
    {
        var services = new ServiceCollection();
        // NOT calling AddSunfishVendorProviderSubstrate() — substrate primitive
        // not registered.
        Assert.Throws<InvalidOperationException>(() =>
            services.UseVendorProviderIfConfigured<IEmailProvider, FakeRealEmailProvider>("ANY_KEY"));
    }

    [Fact]
    public void AddSunfishVendorProvider_CaptchaVerifier_CompilesWithMarker()
    {
        // Compile-time verification that InMemoryCaptchaVerifier (which
        // ADR 0096 Step 1 retrofitted with the IMockVendorProvider marker)
        // satisfies the generic constraint on AddSunfishVendorProvider.
        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<ICaptchaVerifier, InMemoryCaptchaVerifier>();

        var descriptor = services.First(d => d.ServiceType == typeof(ICaptchaVerifier));
        Assert.Equal(typeof(InMemoryCaptchaVerifier), descriptor.ImplementationType);
    }

    // The following commented-out code documents the compile-time constraint
    // that prevents non-marker concretes from being registered as mocks. The
    // line is verified by uncommenting it and observing a CS0311 / CS0315
    // compile error referencing IMockVendorProvider.
    //
    // [Fact]
    // public void AddSunfishVendorProvider_NonMarkerConcrete_FailsToCompile()
    // {
    //     var services = new ServiceCollection();
    //     services.AddSunfishVendorProviderSubstrate();
    //     // COMPILE-FAIL: FakeRealEmailProvider does NOT implement IMockVendorProvider.
    //     services.AddSunfishVendorProvider<IEmailProvider, FakeRealEmailProvider>();
    // }

    /// <summary>
    /// Stand-in "real" adapter for swap-target tests — implements
    /// <see cref="IEmailProvider"/> but NOT <see cref="IMockVendorProvider"/>.
    /// Mirrors the asymmetry the substrate enforces.
    /// </summary>
    private sealed class FakeRealEmailProvider : IEmailProvider
    {
        public Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(EmailDispatchResult.Accepted("fake-real:" + Guid.NewGuid().ToString("N")));
    }
}
