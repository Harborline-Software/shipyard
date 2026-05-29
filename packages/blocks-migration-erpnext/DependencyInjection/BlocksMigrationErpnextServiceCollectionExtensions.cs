using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Migration.Erpnext.Extraction;

namespace Sunfish.Blocks.Migration.Erpnext.DependencyInjection;

/// <summary>
/// DI registration for the A0 ERPNext extraction adapter (ADR 0100 C6).
/// Mirrors the <c>AddSunfish*</c> extension shape used across the fleet.
/// </summary>
/// <remarks>
/// <para>
/// Registers <see cref="MariaDbDumpExtractor"/> as the v1 sole implementation of
/// <see cref="IErpnextSourceExtractor"/> (scoped — one extractor per import run).
/// The connection factory is also scoped so its throwaway-DB lifecycle
/// (create on first use, drop on scope disposal) aligns with the import run.
/// </para>
/// <para>
/// <b>Pending connector wiring.</b> <see cref="SystemMariaDbConnectionFactory"/>
/// is registered as the connection factory but its
/// <see cref="SystemMariaDbConnectionFactory.RestoreAndConnectAsync"/> throws
/// <see cref="NotImplementedException"/> until the .NET-arch council rules on
/// the connector package choice (MySqlConnector vs MySql.Data). The DI wiring
/// is complete; the connector implementation is the deferred piece.
/// </para>
/// </remarks>
public static class BlocksMigrationErpnextServiceCollectionExtensions
{
    /// <summary>
    /// Registers the A0 ERPNext extraction adapter and its system-MariaDB
    /// connection factory. The dump path and credentials are supplied via
    /// <paramref name="configureOptions"/> (populated from CLI flags or env vars
    /// — never hard-coded per C9).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">Action to configure <see cref="SystemMariaDbConnectionOptions"/>.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddErpnextExtractionAdapter(
        this IServiceCollection services,
        Action<SystemMariaDbConnectionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Options — populated from CLI flag/env var; never echoed in logs (C9).
        var options = new SystemMariaDbConnectionOptions();
        configureOptions?.Invoke(options);

        services.TryAddSingleton(options);

        // Connection factory — scoped so the throwaway DB lifecycle (create/drop)
        // aligns with the DI scope that wraps one import run.
        services.TryAddScoped<IRestoredDbConnectionFactory, SystemMariaDbConnectionFactory>();

        // The extractor itself — scoped so it shares the connection factory scope.
        services.TryAddScoped<IErpnextSourceExtractor, MariaDbDumpExtractor>();

        return services;
    }
}
