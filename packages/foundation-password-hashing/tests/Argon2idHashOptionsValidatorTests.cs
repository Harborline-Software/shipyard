using Microsoft.Extensions.Options;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Defense-in-depth options-layer floor enforcement (ADR 0097 C3).
/// </summary>
public sealed class Argon2idHashOptionsValidatorTests
{
    private readonly Argon2idHashOptionsValidator _validator = new();

    [Fact]
    public void Defaults_validate_successfully()
    {
        Assert.True(_validator.Validate(null, new Argon2idHashOptions()).Succeeded);
    }

    [Fact]
    public void Single_below_floor_parameter_fails_with_a_descriptive_message()
    {
        var result = _validator.Validate(null, new Argon2idHashOptions { MemoryKib = 1024 });

        Assert.True(result.Failed);
        Assert.Contains("MemoryKib", result.FailureMessage);
        Assert.Contains("19456", result.FailureMessage);
    }

    [Fact]
    public void Above_floor_parameters_validate_successfully()
    {
        var result = _validator.Validate(null, new Argon2idHashOptions
        {
            MemoryKib = 65536,
            Iterations = 3,
            DegreeOfParallelism = 2,
            SaltLengthBytes = 32,
            HashLengthBytes = 64,
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Multiple_below_floor_parameters_yield_a_single_failure_listing_all_violations()
    {
        var result = _validator.Validate(null, new Argon2idHashOptions
        {
            MemoryKib = 1024,
            Iterations = 1,
            SaltLengthBytes = 8,
        });

        Assert.True(result.Failed);
        Assert.Contains("MemoryKib", result.FailureMessage);
        Assert.Contains("Iterations", result.FailureMessage);
        Assert.Contains("SaltLengthBytes", result.FailureMessage);
    }

    [Fact]
    public void Pepper_over_ceiling_fails()
    {
        var over = new byte[65];
        var result = _validator.Validate(null, new Argon2idHashOptions { Pepper = over });

        Assert.True(result.Failed);
        Assert.Contains("Pepper.Length", result.FailureMessage);
    }
}
