using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sunfish.Foundation.PasswordHashing.DependencyInjection;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Coverage for <see cref="MockPasswordHasherProductionGuardAssertion"/> (ADR 0097 D4c) —
/// env bypass, opt-out parsing, prod-with-mock throw, prod-with-real pass, the A3
/// closed-generic discrimination idiom, and the host-startup integration tests. Runs in a
/// non-parallel collection because env-var reads are process-global state.
/// </summary>
[Collection(EnvSerial.Name)]
public sealed class MockPasswordHasherProductionGuardAssertionTests
{
    private const string EnvVar = "ASPNETCORE_ENVIRONMENT";
    private const string OptOut = "SUNFISH_ALLOW_MOCK_PASSWORD_HASHER";

    private static IServiceCollection ServicesWithMock()
    {
        var services = new ServiceCollection();
        services.Replace(ServiceDescriptor.Singleton<IPasswordHasher<TestUser>, MockPasswordHasher<TestUser>>());
        return services;
    }

    private static IServiceCollection ServicesWithReal()
    {
        var services = new ServiceCollection();
        services.AddSunfishPasswordHashing<TestUser>();
        return services;
    }

    private static Task Run(IServiceCollection services) =>
        new MockPasswordHasherProductionGuardAssertion(services).StartAsync(CancellationToken.None);

    [Fact]
    public async Task Non_production_environment_bypasses()
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Development").Set(OptOut, null);
        await Run(ServicesWithMock()); // no throw
    }

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("true")]
    public async Task Production_with_parseable_opt_out_bypasses(string optOutValue)
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, optOutValue);
        await Run(ServicesWithMock()); // no throw
    }

    [Theory]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData(null)]
    public async Task Production_with_non_truthy_opt_out_fails_closed(string? optOutValue)
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, optOutValue);
        await Assert.ThrowsAsync<MockPasswordHasherInProductionException>(() => Run(ServicesWithMock()));
    }

    [Fact]
    public async Task Production_with_mock_no_opt_out_throws_naming_the_offending_type()
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, null);

        var ex = await Assert.ThrowsAsync<MockPasswordHasherInProductionException>(
            () => Run(ServicesWithMock()));

        Assert.Equal(typeof(IPasswordHasher<TestUser>), ex.ServiceType);
        Assert.Equal(typeof(MockPasswordHasher<TestUser>), ex.ConcreteType);
    }

    [Fact]
    public async Task Production_with_real_only_passes()
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, null);
        await Run(ServicesWithReal()); // no throw
    }

    [Fact]
    public async Task Closed_generic_discrimination_only_flags_the_password_hasher_mock()
    {
        // A3 LOAD-BEARING — an unrelated mock service that is NOT IPasswordHasher<> must
        // not trip the scan; only the password-hasher mock does.
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, null);

        var services = new ServiceCollection();
        services.AddSingleton<IUnrelatedMockMarker, UnrelatedMockService>(); // carries IMockPasswordHasher
        services.Replace(ServiceDescriptor.Singleton<IPasswordHasher<TestUser>, MockPasswordHasher<TestUser>>());

        var ex = await Assert.ThrowsAsync<MockPasswordHasherInProductionException>(
            () => Run(services));

        // The flagged type is the IPasswordHasher<TestUser> registration, NOT the unrelated one.
        Assert.Equal(typeof(IPasswordHasher<TestUser>), ex.ServiceType);
    }

    [Fact]
    public async Task Unrelated_marker_carrying_service_alone_does_not_trip_the_scan()
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, null);

        var services = new ServiceCollection();
        services.AddSingleton<IUnrelatedMockMarker, UnrelatedMockService>();

        await Run(services); // no IPasswordHasher<> mock present → no throw
    }

    [Fact]
    public async Task IHost_StartAsync_throws_in_production_with_mock_no_opt_out()
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, null);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSunfishPasswordHashingSubstrate();
        builder.Services.AddSunfishMockPasswordHashing<TestUser>();

        using var host = builder.Build();

        await Assert.ThrowsAsync<MockPasswordHasherInProductionException>(() => host.StartAsync());
    }

    [Fact]
    public async Task IHost_StartAsync_succeeds_in_production_with_mock_and_opt_out()
    {
        using var _ = new EnvironmentScope().Set(EnvVar, "Production").Set(OptOut, "true");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSunfishPasswordHashingSubstrate();
        builder.Services.AddSunfishMockPasswordHashing<TestUser>();

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    // An unrelated service that also carries IMockPasswordHasher to prove the scan keys off
    // the IPasswordHasher<> ServiceType, not merely the marker.
    private interface IUnrelatedMockMarker;

    private sealed class UnrelatedMockService : IUnrelatedMockMarker, IMockPasswordHasher;
}

/// <summary>xUnit collection that serializes env-var-mutating test classes.</summary>
[CollectionDefinition(Name)]
public sealed class EnvSerial
{
    public const string Name = "env-serial";
}
