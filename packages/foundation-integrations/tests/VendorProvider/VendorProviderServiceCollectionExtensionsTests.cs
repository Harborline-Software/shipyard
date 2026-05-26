using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.VendorProvider;

[Collection(EnvVarMutatingCollection.Name)]
public sealed class VendorProviderServiceCollectionExtensionsTests
{
    private const string Postmark = "POSTMARK_API_KEY";

    private sealed class StubRealEmailProvider : IEmailProvider
    {
        public Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken ct)
            => Task.FromResult<EmailDispatchResult>(new EmailDispatchResult.Accepted("real"));
    }

    [Fact]
    public void AddSunfishVendorProvider_RegistersMockAsContract()
    {
        var sp = new ServiceCollection()
            .AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>()
            .BuildServiceProvider();

        Assert.IsType<MockEmailProvider>(sp.GetRequiredService<IEmailProvider>());
    }

    [Fact]
    public void UseVendorProviderIfConfigured_EnvAbsent_KeepsMock()
    {
        using var env = new EnvVarScope(Postmark);
        env.Set(Postmark, null);

        var sp = new ServiceCollection()
            .AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>()
            .UseVendorProviderIfConfigured<IEmailProvider, StubRealEmailProvider>(Postmark)
            .BuildServiceProvider();

        Assert.IsType<MockEmailProvider>(sp.GetRequiredService<IEmailProvider>());
    }

    [Fact]
    public void UseVendorProviderIfConfigured_EnvEmpty_TreatedAsAbsent()
    {
        using var env = new EnvVarScope(Postmark);
        env.Set(Postmark, "   "); // whitespace == absent

        var sp = new ServiceCollection()
            .AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>()
            .UseVendorProviderIfConfigured<IEmailProvider, StubRealEmailProvider>(Postmark)
            .BuildServiceProvider();

        Assert.IsType<MockEmailProvider>(sp.GetRequiredService<IEmailProvider>());
    }

    [Fact]
    public void UseVendorProviderIfConfigured_EnvPresent_SwapsInRealAdapter()
    {
        using var env = new EnvVarScope(Postmark);
        env.Set(Postmark, "pm-secret");

        var sp = new ServiceCollection()
            .AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>()
            .UseVendorProviderIfConfigured<IEmailProvider, StubRealEmailProvider>(Postmark)
            .BuildServiceProvider();

        Assert.IsType<StubRealEmailProvider>(sp.GetRequiredService<IEmailProvider>());
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void UseVendorProviderIfConfigured_PreservesLifetimeOnSwap(ServiceLifetime lifetime)
    {
        using var env = new EnvVarScope(Postmark);
        env.Set(Postmark, "pm-secret");

        var services = new ServiceCollection();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>(lifetime: lifetime);
        services.UseVendorProviderIfConfigured<IEmailProvider, StubRealEmailProvider>(Postmark);

        var matching = services.Where(d => d.ServiceType == typeof(IEmailProvider)).ToList();
        var descriptor = Assert.Single(matching);
        Assert.Equal(typeof(StubRealEmailProvider), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void UseVendorProviderIfConfigured_RecordsEnvVarKeyInRegistry_Unconditionally()
    {
        using var env = new EnvVarScope(Postmark);
        env.Set(Postmark, null); // even when the swap does NOT happen

        var services = new ServiceCollection();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.UseVendorProviderIfConfigured<IEmailProvider, StubRealEmailProvider>(Postmark);

        var registry = (IMockVendorEnvVarRegistry)services
            .Single(d => d.ServiceType == typeof(IMockVendorEnvVarRegistry)).ImplementationInstance!;
        Assert.Equal(Postmark, registry.TryGetEnvVarKey(typeof(IEmailProvider)));
    }
}
