using System;
using System.Collections.Generic;
using System.Linq;

namespace Sunfish.Foundation.Integrations.DependencyInjection;

/// <summary>
/// Thrown by <see cref="MockProviderProductionGuardAssertion"/> at host start
/// when one or more Tier-2 contracts are registered with a mock concrete in a
/// <c>Production</c> environment, with neither the corresponding real-adapter
/// environment variable set nor the explicit <c>SUNFISH_ALLOW_MOCK_PROVIDERS=true</c>
/// opt-out (ADR 0096). The per-tuple message names the env-var the operator was
/// expected to set — the load-bearing closer of the silent-typo foot-gun.
/// </summary>
public sealed class MockInProductionException : Exception
{
    /// <summary>The contracts left on a mock concrete + the env-var key each expected.</summary>
    public IReadOnlyList<(Type ContractType, string EnvVarKey)> Failures { get; }

    /// <summary>Construct with the list of contracts left on a mock concrete + each expected env-var key.</summary>
    public MockInProductionException(IReadOnlyList<(Type ContractType, string EnvVarKey)> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures;
    }

    private static string BuildMessage(IReadOnlyList<(Type ContractType, string EnvVarKey)> failures)
    {
        var enumeration = string.Join(
            "; ",
            failures.Select(f => $"{f.ContractType.Name} expected {f.EnvVarKey}"));
        return "Production-environment mock providers detected without opt-out. The following "
            + "Tier-2 contracts are registered with mock concrete types but neither the corresponding "
            + "real-adapter environment variable is set nor SUNFISH_ALLOW_MOCK_PROVIDERS=true: "
            + enumeration + ".";
    }
}
