using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sunfish.Foundation.Integrations.DependencyInjection;

/// <summary>
/// Startup guard (ADR 0096): in a <c>Production</c> environment, fails the host
/// fast if any Tier-2 contract is still bound to a mock concrete
/// (<see cref="IMockVendorProvider"/>) without the operator either setting the
/// real-adapter env-var or the explicit <c>SUNFISH_ALLOW_MOCK_PROVIDERS=true</c>
/// opt-out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mechanism — registration-snapshot scan (NOT runtime resolution; ADR 0096
/// Rev 2 / Path α).</b> The guard scans a captured <see cref="ServiceDescriptor"/>
/// snapshot for descriptors whose <c>ImplementationType</c> (or
/// <c>ImplementationInstance</c> type) implements <see cref="IMockVendorProvider"/>.
/// <c>IServiceProvider.GetServices&lt;IMockVendorProvider&gt;()</c> would return
/// empty because mock concretes are registered as <c>TContract</c>, not as the
/// marker. This mirrors the ADR 0091 R2 A1 / ADR 0095 R2 A3 "inspect the
/// registration tree, not the runtime resolution tree" precedent. Factory-only
/// descriptors (no <c>ImplementationType</c>) are out of scope — vendor adapters
/// register by type via the constrained <c>AddSunfishVendorProvider</c> helper.
/// </para>
/// <para>
/// <b>Ordering.</b> This runs AFTER ADR 0095's
/// <c>BootstrapAndTenantMutualExclusionAssertion</c> (registration order, set by
/// the W79 composition root). Independent invariants on disjoint properties.
/// </para>
/// </remarks>
public sealed class MockProviderProductionGuardAssertion : IHostedService
{
    private const string EnvironmentKey = "ASPNETCORE_ENVIRONMENT";
    private const string ProductionValue = "Production";
    private const string MockOptOutKey = "SUNFISH_ALLOW_MOCK_PROVIDERS";

    private readonly IReadOnlyList<ServiceDescriptor> _snapshot;
    private readonly IMockVendorEnvVarRegistry _envVarRegistry;

    /// <summary>
    /// Construct with a captured <see cref="ServiceDescriptor"/> registration
    /// snapshot (taken after all vendor registrations) + the shared
    /// <see cref="IMockVendorEnvVarRegistry"/>.
    /// </summary>
    public MockProviderProductionGuardAssertion(
        IReadOnlyList<ServiceDescriptor> registrationSnapshot,
        IMockVendorEnvVarRegistry envVarRegistry)
    {
        _snapshot = registrationSnapshot ?? throw new ArgumentNullException(nameof(registrationSnapshot));
        _envVarRegistry = envVarRegistry ?? throw new ArgumentNullException(nameof(envVarRegistry));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Non-Production: the guard is inert (mocks are expected in dev/test/demo).
        if (!string.Equals(
                Environment.GetEnvironmentVariable(EnvironmentKey),
                ProductionValue,
                StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        // Opt-out: canonical bool.TryParse — "true"/"True"/"TRUE" pass; "false",
        // and non-parseable values ("1"/"yes"/"on") are fail-closed (false).
        var optOut = bool.TryParse(Environment.GetEnvironmentVariable(MockOptOutKey), out var parsed) && parsed;
        if (optOut)
        {
            return Task.CompletedTask;
        }

        var failures = new List<(Type ContractType, string EnvVarKey)>();
        foreach (var descriptor in _snapshot)
        {
            var implType = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();
            if (implType is null || !typeof(IMockVendorProvider).IsAssignableFrom(implType))
            {
                continue;
            }

            var realEnvVarKey = _envVarRegistry.TryGetEnvVarKey(descriptor.ServiceType);
            var realAdapterConfigured =
                realEnvVarKey is not null
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(realEnvVarKey));

            if (!realAdapterConfigured)
            {
                failures.Add((
                    descriptor.ServiceType,
                    realEnvVarKey ?? "(no real-adapter env-var registered)"));
            }
        }

        if (failures.Count > 0)
        {
            throw new MockInProductionException(failures);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
