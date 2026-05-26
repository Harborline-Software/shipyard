using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Integrations.DependencyInjection;

/// <summary>
/// Default <see cref="IMockVendorEnvVarRegistry"/> — a thin
/// <see cref="Dictionary{TKey,TValue}"/> wrapper. Registered as a singleton
/// INSTANCE (not a type) so that composition-root-time writes (during
/// <c>services.AddX</c>, before the provider is built) and <c>StartAsync</c>-time
/// reads share the same object. Last write wins for a duplicate contract.
/// </summary>
public sealed class MockVendorEnvVarRegistry : IMockVendorEnvVarRegistry
{
    private readonly Dictionary<Type, string> _map = new();

    /// <inheritdoc />
    public void Register(Type contractType, string envVarKey)
    {
        ArgumentNullException.ThrowIfNull(contractType);
        ArgumentException.ThrowIfNullOrWhiteSpace(envVarKey);
        _map[contractType] = envVarKey;
    }

    /// <inheritdoc />
    public string? TryGetEnvVarKey(Type contractType)
        => _map.TryGetValue(contractType, out var key) ? key : null;

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, string> Entries => _map;
}
