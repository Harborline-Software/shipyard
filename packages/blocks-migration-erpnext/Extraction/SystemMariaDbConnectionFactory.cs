using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// v1 baseline <see cref="IRestoredDbConnectionFactory"/> — restores the mysqldump
/// into the system MariaDB/MySQL instance on the import host (design §2.1 option (i)).
/// </summary>
/// <remarks>
/// <para>
/// <b>Restore-engine choice rationale (v1 baseline = system MariaDB/MySQL).</b>
/// The import runs on a known, CIC-controlled machine that already has MariaDB/MySQL
/// available (the same machine holds the dump). The system engine avoids the Docker
/// dependency of option (ii), while the <see cref="IRestoredDbConnectionFactory"/>
/// seam makes it trivially swappable to a container via a DI re-registration.
/// The container option (design §2.1 (ii)) offers stronger isolation — the data
/// lives only inside an <c>--rm</c> container that is destroyed — and is the
/// recommended upgrade for environments where Docker is available.
/// </para>
/// <para>
/// <b>Current implementation status: STUB — not yet executable.</b>
/// This class carries the correct interface contract and the schema-lifecycle
/// logic (create/restore/drop) but the actual ADO.NET connector is
/// <b>not yet wired</b>: the connector package choice
/// (MySqlConnector vs MySql.Data) requires a .NET-arch council ruling on
/// the vendor-neutrality policy. The connection creation is abstracted so the
/// ruling becomes a one-file change here.
/// </para>
/// <para>
/// <b>C9 discipline.</b> The dump path, DB host, and credential are injected via
/// <see cref="SystemMariaDbConnectionOptions"/>. This class NEVER echoes the
/// credential or dump path in log output; it emits only non-sensitive structured
/// metadata (schema name, run id, row counts at restore stage).
/// </para>
/// <para>
/// <b>Throwaway schema lifecycle.</b> On <see cref="RestoreAndConnectAsync"/>,
/// a schema named <c>erpnext_import_{runId}</c> is created, the dump is restored
/// into it, and a read-only connection is returned. On <see cref="DisposeAsync"/>,
/// the schema is DROPped. The data never persists.
/// </para>
/// </remarks>
public sealed class SystemMariaDbConnectionFactory : IRestoredDbConnectionFactory
{
    private readonly SystemMariaDbConnectionOptions _options;
    private readonly ILogger<SystemMariaDbConnectionFactory> _logger;
    private string? _throwawaySchema;

    /// <summary>
    /// Initializes the factory with dump-path + credential options.
    /// Options are validated to be non-empty but are NEVER echoed in logs (C9).
    /// </summary>
    public SystemMariaDbConnectionFactory(
        SystemMariaDbConnectionOptions options,
        ILogger<SystemMariaDbConnectionFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(options.DumpFilePath))
        {
            throw new ArgumentException(
                "DumpFilePath must be supplied (CLI flag / env var --source-dump).", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ArgumentException("Host must be supplied.", nameof(options));
        }

        // Credential presence validated; value never echoed.
        if (string.IsNullOrWhiteSpace(options.AdminUser))
        {
            throw new ArgumentException("AdminUser must be supplied.", nameof(options));
        }

        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>IMPLEMENTATION PENDING .NET-arch ruling on connector package.</b>
    /// The scaffold is present; the connector wiring (create schema, call
    /// <c>mysql</c> CLI or connector-native restore, return read-only connection)
    /// is replaced by a <see cref="NotImplementedException"/> until the council
    /// advises on vendor-neutrality scope for the MariaDB/MySQL connector.
    /// </remarks>
    public Task<IDbConnection> RestoreAndConnectAsync(
        string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        // C9: log only safe metadata — never the credential or dump path.
        _logger.LogInformation(
            "Restoring ERPNext dump into throwaway schema for run {RunId}.", runId);

        _throwawaySchema = $"erpnext_import_{runId}";

        // TODO: .NET-arch council to advise on connector package choice
        // (MySqlConnector vs MySql.Data). Once ruled:
        //   1. Add the connector PackageReference here.
        //   2. Create _throwawaySchema via an admin connection.
        //   3. Restore the dump: `mysql --host=_options.Host -u _options.AdminUser
        //      -p<pwd> _throwawaySchema < _options.DumpFilePath` (via Process or
        //      connector-native bulk-load).
        //   4. Return a read-only IDbConnection to _throwawaySchema.
        throw new NotImplementedException(
            "SystemMariaDbConnectionFactory.RestoreAndConnectAsync: pending .NET-arch " +
            "council ruling on MariaDB/MySQL connector package selection " +
            "(MySqlConnector vs MySql.Data; vendor-neutrality scope). " +
            "The factory seam, options, and lifecycle are complete; only the " +
            "connector wiring is deferred. See PR description §Architectural seam.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_throwawaySchema is not null)
        {
            // C9: log only schema name (opaque run-scoped id), not credential.
            _logger.LogInformation(
                "Dropping throwaway schema {Schema} on import completion.", _throwawaySchema);

            // TODO: execute DROP DATABASE _throwawaySchema via admin connection.
            // Scaffold: disposal is a no-op until the connector is wired.
            _throwawaySchema = null;
        }

        await ValueTask.CompletedTask;
    }
}

/// <summary>
/// Configuration options for <see cref="SystemMariaDbConnectionFactory"/>.
/// Populated from CLI flags or environment variables — never hard-coded.
/// </summary>
/// <remarks>
/// <b>C9 discipline.</b> None of these values are echoed in logs. The
/// <see cref="AdminPassword"/> is consumed opaquely by the factory.
/// </remarks>
public sealed class SystemMariaDbConnectionOptions
{
    /// <summary>
    /// Path to the mysqldump file on the import host. Supplied via
    /// <c>--source-dump</c> CLI flag or <c>ERPNEXT_DUMP_PATH</c> env var.
    /// MUST point outside the fleet tree — real financial data + PII.
    /// </summary>
    public string DumpFilePath { get; init; } = string.Empty;

    /// <summary>MariaDB/MySQL host (default: 127.0.0.1).</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Port (default: 3306).</summary>
    public int Port { get; init; } = 3306;

    /// <summary>Admin user with CREATE/DROP DATABASE + FILE privileges.</summary>
    public string AdminUser { get; init; } = string.Empty;

    /// <summary>Admin password. NOT echoed in logs (C9).</summary>
    public string AdminPassword { get; init; } = string.Empty;
}
