using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Session.DependencyInjection;
using Xunit;

namespace Sunfish.Foundation.Session.Tests;

public sealed class SessionEstablishmentServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_store_as_singleton_and_establisher_as_scoped()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSunfishSessionEstablishment();

        var storeDescriptor = services.Single(d => d.ServiceType == typeof(ISessionStore));
        Assert.Equal(ServiceLifetime.Singleton, storeDescriptor.Lifetime);
        Assert.Equal(typeof(InMemorySessionStore), storeDescriptor.ImplementationType);

        var establisherDescriptor = services.Single(d => d.ServiceType == typeof(ISessionEstablisher));
        Assert.Equal(ServiceLifetime.Scoped, establisherDescriptor.Lifetime);
        Assert.Equal(typeof(SessionEstablisher), establisherDescriptor.ImplementationType);
    }

    [Fact]
    public void Does_not_register_any_authorization_facade_A7()
    {
        // A7: AddSunfishSessionEstablishment registers ONLY session-specific services — never the
        // Foundation.Authorization facade. Assert nothing in the Sunfish.Foundation.Authorization
        // namespace was registered by this helper.
        var services = new ServiceCollection().AddLogging();
        services.AddSunfishSessionEstablishment();

        Assert.DoesNotContain(services, d =>
            (d.ServiceType.Namespace?.StartsWith("Sunfish.Foundation.Authorization", StringComparison.Ordinal) ?? false));
    }

    [Fact]
    public void Resolves_a_working_establisher_from_the_container()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSunfishSessionEstablishment();
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var establisher = scope.ServiceProvider.GetRequiredService<ISessionEstablisher>();
        var store = provider.GetRequiredService<ISessionStore>();

        Assert.IsType<SessionEstablisher>(establisher);
        Assert.IsType<InMemorySessionStore>(store);
    }

    [Fact]
    public void Applies_configured_options()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSunfishSessionEstablishment(opts =>
        {
            opts.AbsoluteLifetime = TimeSpan.FromHours(2);
            opts.IdleTimeout = TimeSpan.FromMinutes(10);
            opts.SessionIdByteLength = 24;
        });
        using var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IOptions<SessionOptions>>().Value;
        Assert.Equal(TimeSpan.FromHours(2), resolved.AbsoluteLifetime);
        Assert.Equal(TimeSpan.FromMinutes(10), resolved.IdleTimeout);
        Assert.Equal(24, resolved.SessionIdByteLength);
    }

    [Fact]
    public void Invalid_options_fail_fast_at_registration()
    {
        var services = new ServiceCollection().AddLogging();

        // idle > absolute -> Validate throws at registration, not at first resolve.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddSunfishSessionEstablishment(opts =>
            {
                opts.AbsoluteLifetime = TimeSpan.FromMinutes(5);
                opts.IdleTimeout = TimeSpan.FromMinutes(30);
            }));
    }

    [Fact]
    public void Registers_a_time_provider_when_none_present()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSunfishSessionEstablishment();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TimeProvider>());
    }
}
