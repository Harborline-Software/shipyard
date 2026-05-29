using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// The swappable restore-engine seam (ADR 0100 C4 / design §2.1).
/// Abstracts where the throwaway restored DB comes from — system MariaDB/MySQL,
/// an ephemeral container, or a future embedded engine — so
/// <see cref="MariaDbDumpExtractor"/> is engine-agnostic and the engine choice
/// does not leak into A1–A6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Throwaway-DB lifecycle.</b> The factory creates a FRESH throwaway database
/// per run (schema named per <c>runId</c>, e.g. <c>erpnext_import_{runId}</c>),
/// restores the dump into it, returns a read-only connection, and DROPS the schema
/// on disposal. The financial data does NOT persist after the import completes.
/// </para>
/// <para>
/// <b>Read-only connection.</b> The returned connection MUST be a read-only
/// connection to the throwaway schema. <see cref="MariaDbDumpExtractor"/> issues
/// only parameterized <c>SELECT</c> statements through it — no DDL or DML touches
/// the data (the DDL/DML is the restore, owned by THIS factory before the connection
/// is returned). The arch-test C-CLEANROOM verifies the extractor never issues a
/// write.
/// </para>
/// <para>
/// <b>v1 baseline: system MariaDB/MySQL (option (i) from design §2.1).</b>
/// The v1 implementation (<see cref="SystemMariaDbConnectionFactory"/>) restores
/// into the system MariaDB/MySQL instance on the import host. The container option
/// (design §2.1 (ii)) offers stronger isolation (data lives only in an
/// <c>--rm</c> container) and is the recommended upgrade path; swapping is a
/// DI re-registration, not a caller change.
/// </para>
/// <para>
/// <b>C9 discipline.</b> The factory receives the dump path and DB credential
/// via constructor injection (from CLI flag/env var via DI). It NEVER echoes the
/// credential or the dump path in logs.
/// </para>
/// </remarks>
public interface IRestoredDbConnectionFactory : IAsyncDisposable
{
    /// <summary>
    /// Restores the dump into a fresh throwaway schema, returning a read-only
    /// connection open to that schema. The throwaway schema is dropped when this
    /// factory instance is disposed (see <see cref="IAsyncDisposable"/>).
    /// </summary>
    /// <param name="runId">
    /// An opaque per-run identifier used to name the throwaway schema
    /// (e.g. a short GUID segment). Must be safe for use in a schema name
    /// (alphanumeric + underscore). The caller is responsible for uniqueness.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An open, read-only <see cref="IDbConnection"/> to the throwaway restored
    /// schema. The caller MUST NOT write to this connection. The connection is
    /// closed and the schema dropped when this factory instance is disposed.
    /// </returns>
    Task<IDbConnection> RestoreAndConnectAsync(string runId, CancellationToken ct = default);
}
