namespace Sunfish.Foundation.Integrations.Tests.DependencyInjection;

/// <summary>
/// IDisposable env-var restoration helper per ADR 0096 §"test isolation
/// discipline." Captures the current value of one or more env vars at
/// construction time, applies new values, and restores the originals on
/// dispose. Used in conjunction with xUnit
/// <see cref="Xunit.CollectionAttribute"/> (<see cref="EnvVarCollection"/>)
/// to serialize tests that mutate process-global env-var state.
/// </summary>
internal sealed class EnvVarScope : IDisposable
{
    private readonly Dictionary<string, string?> _originals = new(StringComparer.Ordinal);

    public EnvVarScope(params (string Key, string? Value)[] vars)
    {
        ArgumentNullException.ThrowIfNull(vars);
        foreach (var (key, value) in vars)
        {
            _originals[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public void Dispose()
    {
        foreach (var (key, original) in _originals)
        {
            Environment.SetEnvironmentVariable(key, original);
        }
    }
}

/// <summary>
/// xUnit collection that serializes tests touching process-global env-var
/// state. Tests in this collection do not run in parallel with each other.
/// </summary>
[CollectionDefinition(nameof(EnvVarCollection), DisableParallelization = true)]
public sealed class EnvVarCollection
{
    // Marker — xUnit collection definition.
}
