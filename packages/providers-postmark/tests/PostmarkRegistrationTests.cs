using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Integrations;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;
using Xunit;

namespace Sunfish.Providers.Postmark.Tests;

/// <summary>
/// Substrate-integration tests for the conditional mock → Postmark swap. These
/// mutate <c>POSTMARK_API_KEY</c>; not parallelized so the env-var state does
/// not cross-contaminate. The collection definition pins them to a single
/// worker.
/// </summary>
[Collection(nameof(PostmarkEnvVarCollection))]
public sealed class PostmarkRegistrationTests : IDisposable
{
    private const string EnvVar = "POSTMARK_API_KEY";
    private readonly string? _saved = Environment.GetEnvironmentVariable(EnvVar);

    public void Dispose() => Environment.SetEnvironmentVariable(EnvVar, _saved);

    private static ServiceDescriptor EmailDescriptor(IServiceCollection services)
        => Assert.Single(services, d => d.ServiceType == typeof(IEmailProvider));

    [Fact]
    public void EnvVarAbsent_MockStaysRegistered()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.AddPostmarkEmailProvider();

        Assert.Equal(typeof(MockEmailProvider), EmailDescriptor(services).ImplementationType);
    }

    [Fact]
    public void EnvVarEmpty_MockStaysRegistered()
    {
        Environment.SetEnvironmentVariable(EnvVar, "");

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.AddPostmarkEmailProvider();

        Assert.Equal(typeof(MockEmailProvider), EmailDescriptor(services).ImplementationType);
    }

    [Fact]
    public void EnvVarPresent_PostmarkSwapsIn()
    {
        Environment.SetEnvironmentVariable(EnvVar, "pm-key-present");

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.AddPostmarkEmailProvider();

        Assert.Equal(typeof(PostmarkEmailProvider), EmailDescriptor(services).ImplementationType);
    }

    [Fact]
    public void EnvVarMapping_AlwaysRecordedForProductionGuard()
    {
        Environment.SetEnvironmentVariable(EnvVar, null);

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.AddPostmarkEmailProvider();

        var registry = (IMockVendorEnvVarRegistry)Assert.Single(
            services, d => d.ServiceType == typeof(IMockVendorEnvVarRegistry)).ImplementationInstance!;
        Assert.True(registry.TryGet(typeof(IEmailProvider), out var key));
        Assert.Equal("POSTMARK_API_KEY", key);
    }

    [Fact]
    public void SwappedRealAdapter_DoesNotCarryMockMarker()
    {
        Environment.SetEnvironmentVariable(EnvVar, "pm-key-present");

        var services = new ServiceCollection();
        services.AddSunfishVendorProviderSubstrate();
        services.AddSunfishVendorProvider<IEmailProvider, MockEmailProvider>();
        services.AddPostmarkEmailProvider();

        var impl = EmailDescriptor(services).ImplementationType!;
        Assert.False(typeof(IMockVendorProvider).IsAssignableFrom(impl));
    }
}

[CollectionDefinition(nameof(PostmarkEnvVarCollection), DisableParallelization = true)]
public sealed class PostmarkEnvVarCollection;
