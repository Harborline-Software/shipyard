using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.PasswordHashing.DependencyInjection;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Coverage for the three DI helpers — substrate-init idempotency (A6), services.Replace
/// idempotency (A4), the configure delegate, lifetime control (C1), and the
/// TryAddEnumerable dedup semantics.
/// </summary>
public sealed class PasswordHashingServiceCollectionExtensionsTests
{
    private sealed class OtherUser { }

    [Fact]
    public void Substrate_registers_both_hosted_service_assertions_exactly_once()
    {
        var services = new ServiceCollection();

        services.AddSunfishPasswordHashingSubstrate();
        services.AddSunfishPasswordHashingSubstrate(); // calling twice must NOT duplicate (A6)

        var hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        Assert.Single(hostedServices, IsProductionGuard);
        Assert.Single(hostedServices, IsFloorAssertion);
    }

    [Fact]
    public void Calling_substrate_after_multiple_mock_registrations_still_yields_one_of_each()
    {
        var services = new ServiceCollection();

        services.AddSunfishPasswordHashingSubstrate();
        services.AddSunfishMockPasswordHashing<TestUser>();
        services.AddSunfishMockPasswordHashing<OtherUser>();

        var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
        Assert.Single(hostedServices, IsProductionGuard);
        Assert.Single(hostedServices, IsFloorAssertion);
    }

    [Fact]
    public void AddSunfishPasswordHashing_registers_the_argon2id_concrete_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddSunfishPasswordHashing<TestUser>();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPasswordHasher<TestUser>));
        Assert.Equal(typeof(Argon2idPasswordHasher<TestUser>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddSunfishPasswordHashing_applies_the_configure_delegate()
    {
        var services = new ServiceCollection();
        services.AddSunfishPasswordHashing<TestUser>(o => o.MemoryKib = 46080);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Argon2idHashOptions>>().Value;

        Assert.Equal(46080u, options.MemoryKib);
    }

    [Fact]
    public void AddSunfishPasswordHashing_displaces_a_prior_registration_idempotently()
    {
        // A4 LOAD-BEARING — the W#79 MVP-era BCL registration is displaced by services.Replace.
        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher<TestUser>, PasswordHasher<TestUser>>();

        services.AddSunfishPasswordHashing<TestUser>();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPasswordHasher<TestUser>));
        Assert.Equal(typeof(Argon2idPasswordHasher<TestUser>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddSunfishMockPasswordHashing_registers_the_mock_via_replace()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPasswordHasher<TestUser>, PasswordHasher<TestUser>>();

        services.AddSunfishMockPasswordHashing<TestUser>();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPasswordHasher<TestUser>));
        Assert.Equal(typeof(MockPasswordHasher<TestUser>), descriptor.ImplementationType);
    }

    [Fact]
    public void AddSunfishPasswordHashing_honors_a_requested_lifetime()
    {
        var services = new ServiceCollection();
        services.AddSunfishPasswordHashing<TestUser>(lifetime: ServiceLifetime.Scoped);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPasswordHasher<TestUser>));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddSunfishPasswordHashing_registers_the_options_validator()
    {
        var services = new ServiceCollection();
        services.AddSunfishPasswordHashing<TestUser>();

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IValidateOptions<Argon2idHashOptions>)
                && d.ImplementationType == typeof(Argon2idHashOptionsValidator));
    }

    [Fact]
    public void Resolved_argon2id_concrete_round_trips()
    {
        var services = new ServiceCollection();
        services.AddSunfishPasswordHashing<TestUser>();
        using var provider = services.BuildServiceProvider();

        var hasher = provider.GetRequiredService<IPasswordHasher<TestUser>>();
        var hash = hasher.HashPassword(TestUser.Instance, "resolved-and-verified");

        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(TestUser.Instance, hash, "resolved-and-verified"));
    }

    // The production guard is registered via a factory closure (it captures the
    // IServiceCollection); resolving it against an empty provider yields the concrete.
    // The floor assertion is registered by ImplementationType.
    private static bool IsProductionGuard(ServiceDescriptor d) =>
        d.ImplementationFactory is not null
        && d.ImplementationFactory(EmptyProvider.Instance) is MockPasswordHasherProductionGuardAssertion;

    private static bool IsFloorAssertion(ServiceDescriptor d) =>
        d.ImplementationType == typeof(Argon2idParameterFloorAssertion);

    private sealed class EmptyProvider : IServiceProvider
    {
        public static readonly EmptyProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
