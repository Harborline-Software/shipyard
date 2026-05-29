using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.PasswordHashing.DependencyInjection;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Per-floor below / at / above coverage for <see cref="Argon2idParameterFloorAssertion"/>
/// (ADR 0097 S3) plus the host-startup integration test.
/// </summary>
public sealed class Argon2idParameterFloorAssertionTests
{
    private static Task StartAssertion(Argon2idHashOptions options)
    {
        var assertion = new Argon2idParameterFloorAssertion(Options.Create(options));
        return assertion.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MemoryKib_below_floor_throws_naming_the_parameter()
    {
        var ex = await Assert.ThrowsAsync<Argon2idBelowFloorException>(
            () => StartAssertion(new Argon2idHashOptions { MemoryKib = 1024 }));

        Assert.Equal("MemoryKib", ex.ParameterName);
        Assert.Equal(19456, ex.ExpectedFloor);
        Assert.Equal(1024, ex.ActualValue);
    }

    [Fact]
    public async Task MemoryKib_at_floor_passes()
    {
        await StartAssertion(new Argon2idHashOptions { MemoryKib = 19456 });
    }

    [Fact]
    public async Task MemoryKib_above_floor_passes()
    {
        await StartAssertion(new Argon2idHashOptions { MemoryKib = 46080 });
    }

    [Theory]
    // Iterations (Floor 4 — 2)
    [InlineData("Iterations", 1u, 2u, 3u)]
    // DegreeOfParallelism (Floor 5 — 1) — uint can't go below 0, so use the at/above pair
    // and a synthetic below via 0.
    [InlineData("DegreeOfParallelism", 0u, 1u, 2u)]
    // SaltLengthBytes (Floor 1 — 16)
    [InlineData("SaltLengthBytes", 8u, 16u, 32u)]
    // HashLengthBytes (Floor 2 — 32)
    [InlineData("HashLengthBytes", 16u, 32u, 64u)]
    public async Task Each_floor_has_below_at_above_behavior(
        string parameterName, uint below, uint at, uint above)
    {
        // below → throws
        var ex = await Assert.ThrowsAsync<Argon2idBelowFloorException>(
            () => StartAssertion(WithParameter(parameterName, below)));
        Assert.Equal(parameterName, ex.ParameterName);

        // at → passes
        await StartAssertion(WithParameter(parameterName, at));

        // above → passes
        await StartAssertion(WithParameter(parameterName, above));
    }

    [Fact]
    public async Task Pepper_over_ceiling_throws_and_within_ceiling_passes()
    {
        var over = new byte[65]; // > 64 (Floor 6 future-enablement bound)
        var ex = await Assert.ThrowsAsync<Argon2idBelowFloorException>(
            () => StartAssertion(new Argon2idHashOptions { Pepper = over }));
        Assert.Equal("Pepper.Length", ex.ParameterName);

        var within = new byte[64]; // == 64 passes
        await StartAssertion(new Argon2idHashOptions { Pepper = within });

        // null pepper passes (MVP default)
        await StartAssertion(new Argon2idHashOptions { Pepper = null });
    }

    [Fact]
    public async Task IHost_StartAsync_throws_when_configured_below_floor()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSunfishPasswordHashingSubstrate();
        builder.Services.AddSunfishPasswordHashing<TestUser>(o => o.MemoryKib = 1024);

        using var host = builder.Build();

        // Both defense-in-depth catches fire on the same below-floor misconfiguration.
        // At host startup, the IHostedService constructor resolves
        // IOptions<Argon2idHashOptions>.Value, which triggers the
        // Argon2idHashOptionsValidator (IValidateOptions<T>) FIRST — surfacing an
        // OptionsValidationException whose inner failure carries the floor-violation
        // message. The Argon2idParameterFloorAssertion's own Argon2idBelowFloorException is
        // the catch when the options snapshot is supplied directly (the unit tests above).
        // Either way, the process fails closed before serving the first request.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());
        Assert.True(
            ex is OptionsValidationException or Argon2idBelowFloorException,
            $"expected a floor-violation exception, got {ex.GetType()}");
        Assert.Contains("MemoryKib", ex.Message);
        Assert.Contains("19456", ex.Message);
    }

    [Fact]
    public async Task IHost_StartAsync_succeeds_at_OWASP_minimum()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSunfishPasswordHashingSubstrate();
        builder.Services.AddSunfishPasswordHashing<TestUser>();

        using var host = builder.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    private static Argon2idHashOptions WithParameter(string parameterName, uint value) => parameterName switch
    {
        "Iterations" => new Argon2idHashOptions { Iterations = value },
        "DegreeOfParallelism" => new Argon2idHashOptions { DegreeOfParallelism = value },
        "SaltLengthBytes" => new Argon2idHashOptions { SaltLengthBytes = value },
        "HashLengthBytes" => new Argon2idHashOptions { HashLengthBytes = value },
        "MemoryKib" => new Argon2idHashOptions { MemoryKib = value },
        _ => throw new ArgumentOutOfRangeException(nameof(parameterName), parameterName, null),
    };
}
