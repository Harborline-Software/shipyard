namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Stand-in user entity for the <c>TUser</c> type parameter. The substrate is
/// TUser-agnostic (the hash computation does not read the user); any reference type works.
/// </summary>
internal sealed class TestUser
{
    public static readonly TestUser Instance = new();
}

/// <summary>
/// Captures and restores process-global environment variables across a test's lifetime so
/// the env-var-reading production-guard assertion tests do not leak state across xUnit
/// parallelization (ADR 0097 §"Test isolation discipline"). Used with a
/// <c>using</c> statement or constructor/Dispose pair.
/// </summary>
internal sealed class EnvironmentScope : IDisposable
{
    private readonly List<(string Name, string? OriginalValue)> _saved = new();

    public EnvironmentScope Set(string name, string? value)
    {
        _saved.Add((name, Environment.GetEnvironmentVariable(name)));
        Environment.SetEnvironmentVariable(name, value);
        return this;
    }

    public void Dispose()
    {
        // Restore in reverse order so nested sets unwind correctly.
        for (var i = _saved.Count - 1; i >= 0; i--)
        {
            Environment.SetEnvironmentVariable(_saved[i].Name, _saved[i].OriginalValue);
        }
    }
}
