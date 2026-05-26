using System;
using System.Collections.Generic;

namespace Sunfish.Foundation.Integrations.DependencyInjection;

/// <summary>
/// Singleton registry capturing the <c>(TContract → real-adapter env-var key)</c>
/// mapping recorded by <c>UseVendorProviderIfConfigured</c> at composition-root
/// call time (ADR 0096, sec-eng #3). <see cref="MockProviderProductionGuardAssertion"/>
/// reads it at <c>StartAsync</c> so it can name the exact env-var key per failing
/// contract in <see cref="MockInProductionException"/> — closing the silent-typo
/// foot-gun.
/// </summary>
public interface IMockVendorEnvVarRegistry
{
    /// <summary>Record the env-var key expected to activate the real adapter for <paramref name="contractType"/>.</summary>
    void Register(Type contractType, string envVarKey);

    /// <summary>Look up the env-var key for <paramref name="contractType"/>, or null when none recorded.</summary>
    string? TryGetEnvVarKey(Type contractType);

    /// <summary>All recorded mappings (for enumeration by the guard assertion).</summary>
    IReadOnlyDictionary<Type, string> Entries { get; }
}
