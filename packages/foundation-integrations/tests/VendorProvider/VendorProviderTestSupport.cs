using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Integrations.Tests.VendorProvider;

/// <summary>
/// Captures + restores process-global environment variables across a test, so
/// the env-var-driven vendor-provider guard tests don't leak state (ADR 0096
/// Rev 2 / .NET-arch A5 test-isolation discipline). Use with
/// <c>[Collection(EnvVarMutatingCollection.Name)]</c> to also disable xUnit
/// parallelization across env-mutating classes.
/// </summary>
internal sealed class EnvVarScope : IDisposable
{
    private readonly Dictionary<string, string?> _original = new(StringComparer.Ordinal);

    public EnvVarScope(params string[] keysToCapture)
    {
        foreach (var key in keysToCapture)
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
        }
    }

    public EnvVarScope Set(string key, string? value)
    {
        if (!_original.ContainsKey(key))
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
        }
        Environment.SetEnvironmentVariable(key, value);
        return this;
    }

    public void Dispose()
    {
        foreach (var (key, value) in _original)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

/// <summary>xUnit collection that serializes env-var-mutating test classes (no parallel env races).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvVarMutatingCollection
{
    public const string Name = "EnvVarMutating";
}
