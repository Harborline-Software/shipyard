// CLEAN-ROOM ATTRIBUTION (ADR 0100 C4 (b) / spec §3.4 / §9.5)
//
// This file references the DATA FORMAT of ERPNext / Frappe — the `tab<DocType>`
// table naming convention, standard DocType field labels, and child-table
// correlation conventions (parent/parenttype columns) — as a FORMAT-REFERENCE-ONLY
// data-interchange contract. NO Frappe framework code (controllers, validators,
// workflow logic, DocType-definition JSON, or server-side Python) is derived,
// copied, or included.
//
// ERPNext is released under the GNU General Public License v3.0 (GPLv3).
// Frappe Framework is released under the MIT License.
// This project (Harborline Software Shipyard) is released under the MIT License.
//
// The `tab*` table names and column names used below are public schema of an
// ERPNext database instance. They are the data FORMAT, not code, and are
// referenced solely to read a customer's own data for migration into Sunfish.

using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialAr.Migration;
using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialTax.Migration;
using Sunfish.Blocks.Migration.Erpnext.Extraction.Rejects;
using Sunfish.Blocks.People.Foundation.Migration;
using Sunfish.Foundation.Import.Extraction;
using Sunfish.Foundation.Import.Outcomes;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// v1 sole implementation of <see cref="IErpnextSourceExtractor"/> — restores the
/// mysqldump into an ephemeral throwaway DB via <see cref="IRestoredDbConnectionFactory"/>
/// and issues parameterized read-only <c>SELECT</c> statements to stream the frozen
/// <c>Erpnext*Source</c> DTOs (ADR 0100 C4 / design §2.1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Restore-to-DB vs. #183 streaming primitives (design §2.1 / PR description seam).</b>
/// This extractor uses the restore-to-DB path (real SQL + JOINs) for all reads.
/// It does NOT use <c>MariaDbDumpSourceReader</c> from <c>foundation-import</c>
/// for the JOIN reads — those require a JOIN across two <c>tab*</c> tables (e.g.
/// <c>tabJournal Entry</c> + <c>tabJournal Entry Account</c>), which stream-parsing
/// the SQL text makes a hand-rolled buffer+correlate nightmare. It DOES compose
/// from <c>foundation-import</c>:
/// <list type="bullet">
///   <item><see cref="SourceAccessMode.MariaDbDump"/> — the access-mode descriptor surfaced
///         via <see cref="ErpnextSourceInventory.SourceMode"/>.</item>
///   <item><see cref="ImportFailure"/> / <see cref="ImportRejectReason"/> — the allowlisted
///         reject projection types.</item>
/// </list>
/// For the five single-table DocTypes (Account, CostCenter, Customer, Supplier, FiscalYear),
/// the implementation COULD use <c>MariaDbDumpSourceReader</c> as an alternative path —
/// but since the restore-to-DB path is already required for the JOIN DocTypes, composing
/// both paths in one extractor adds complexity for no gain. All reads go through the
/// restored DB. The .NET-arch council is invited to adjudicate this decision.
/// </para>
/// <para>
/// <b>USD-only assertion (ADR 0100 OQ-2 / CIC build parameter).</b>
/// Rows with a <c>currency</c> column value other than <c>"USD"</c> (case-insensitive)
/// throw <see cref="InvalidOperationException"/> — fail loud, do NOT coerce. The
/// exception message includes DocType + externalRef + observed currency but NOT
/// monetary amounts (C9).
/// </para>
/// <para>
/// <b>Stacked follow-up methods.</b> Methods that require JOIN across two tables
/// (<c>ReadContactsAsync</c>, <c>ReadAddressesAsync</c>, <c>ReadTaxTemplatesAsync</c>,
/// <c>ReadJournalEntriesAsync</c>, <c>ReadSalesInvoicesAsync</c>,
/// <c>ReadPurchaseInvoicesAsync</c>) throw <see cref="NotImplementedException"/>
/// in this PR. They are stubs, not silent empty returns — a caller that invokes
/// them gets a loud failure rather than silently receiving zero records.
/// </para>
/// </remarks>
public sealed class MariaDbDumpExtractor : IErpnextSourceExtractor
{
    private readonly IRestoredDbConnectionFactory _connectionFactory;
    private readonly ILogger<MariaDbDumpExtractor> _logger;

    /// <summary>
    /// Initializes the extractor. The connection factory owns the dump-restore
    /// lifecycle; the extractor only calls <see cref="IRestoredDbConnectionFactory.RestoreAndConnectAsync"/>
    /// once per run (the run-id is generated internally).
    /// </summary>
    public MariaDbDumpExtractor(
        IRestoredDbConnectionFactory connectionFactory,
        ILogger<MariaDbDumpExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    // ---- Pass-1: chart of accounts ----

    /// <inheritdoc />
    public async IAsyncEnumerable<ErpnextAccountSource> ReadAccountsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        using var cmd = connection.Connection.CreateCommand();

        // SELECT-only against the throwaway restored DB (C4 (c)).
        // No PII in tabAccount; field-name log is safe.
        cmd.CommandText =
            "SELECT name, modified, account_name, account_number, " +
            "parent_account, account_type, is_group, disabled " +
            "FROM `tabAccount` " +
            "ORDER BY name";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            var name = reader.GetString(0);

            // C9: log only the opaque externalRef (name), not field values.
            _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.ExtractedRecord("Account", name));

            yield return new ErpnextAccountSource(
                Name: name,
                Modified: reader.GetString(1),
                AccountName: reader.GetString(2),
                AccountNumber: GetNullableString(reader, 3),
                ParentAccountName: GetNullableString(reader, 4),
                AccountType: GetNullableString(reader, 5),
                IsGroup: GetBool(reader, 6),
                Disabled: GetBool(reader, 7));
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ErpnextCostCenterSource> ReadCostCentersAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        using var cmd = connection.Connection.CreateCommand();

        cmd.CommandText =
            "SELECT name, modified, cost_center_name, parent_cost_center, " +
            "is_group, disabled " +
            "FROM `tabCost Center` " +
            "ORDER BY name";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            var name = reader.GetString(0);

            _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.ExtractedRecord("Cost Center", name));

            yield return new ErpnextCostCenterSource(
                Name: name,
                Modified: reader.GetString(1),
                CostCenterName: reader.GetString(2),
                ParentCostCenterName: GetNullableString(reader, 3),
                IsGroup: GetBool(reader, 4),
                Disabled: GetBool(reader, 5));
        }
    }

    // ---- Pass-2.2: fiscal years ----

    /// <inheritdoc />
    public async IAsyncEnumerable<ErpnextFiscalYearSource> ReadFiscalYearsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        using var cmd = connection.Connection.CreateCommand();

        // LEFT JOIN to tabFiscal Year Company to get CompanyShortName.
        // The child table has one row per company per fiscal year; we take the
        // first company's short-name (for single-company or multi-LLC installs
        // the first linked company is typically the primary one). The A2.2 upserter
        // synthesizes the period grid from the date bounds; it does not use the
        // company short-name for uniqueness — only for the label.
        cmd.CommandText =
            "SELECT fy.name, fy.modified, fy.year_start_date, fy.year_end_date, " +
            "fyc.company_abbr, fy.is_short_year " +
            "FROM `tabFiscal Year` fy " +
            "LEFT JOIN `tabFiscal Year Company` fyc ON fyc.parent = fy.name " +
            "GROUP BY fy.name " +
            "ORDER BY fy.year_start_date";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            var name = reader.GetString(0);

            _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.ExtractedRecord("Fiscal Year", name));

            // year_start_date and year_end_date are DATE columns; read as string and parse.
            var startDate = DateOnly.Parse(reader.GetString(2));
            var endDate = DateOnly.Parse(reader.GetString(3));

            yield return new ErpnextFiscalYearSource(
                Name: name,
                Modified: reader.GetString(1),
                YearStartDate: startDate,
                YearEndDate: endDate,
                CompanyShortName: GetNullableString(reader, 4),
                IsShortYear: GetBool(reader, 5));
        }
    }

    // ---- Pass-2.1: parties (single-table; simple) ----

    /// <inheritdoc />
    public async IAsyncEnumerable<ErpnextPartyCustomerSource> ReadCustomersAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        using var cmd = connection.Connection.CreateCommand();

        // PII fields: customer_name, email_id, mobile_no, tax_id.
        // C9: these are READ into the DTO for the upserter to consume;
        // they are NEVER echoed in log output — log only the opaque 'name'.
        cmd.CommandText =
            "SELECT name, modified, customer_name, customer_type, " +
            "email_id, mobile_no, tax_id, disabled " +
            "FROM `tabCustomer` " +
            "ORDER BY name";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            var name = reader.GetString(0);

            // C9: log only the opaque id — never PII fields.
            _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.ExtractedRecord("Customer", name));

            yield return new ErpnextPartyCustomerSource(
                Name: name,
                Modified: reader.GetString(1),
                CustomerName: reader.GetString(2),
                CustomerType: GetNullableString(reader, 3),
                EmailId: GetNullableString(reader, 4),
                MobileNo: GetNullableString(reader, 5),
                TaxId: GetNullableString(reader, 6),
                Disabled: GetBool(reader, 7));
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ErpnextPartySupplierSource> ReadSuppliersAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        using var cmd = connection.Connection.CreateCommand();

        cmd.CommandText =
            "SELECT name, modified, supplier_name, supplier_type, " +
            "email_id, mobile_no, tax_id, disabled " +
            "FROM `tabSupplier` " +
            "ORDER BY name";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            var name = reader.GetString(0);

            _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.ExtractedRecord("Supplier", name));

            yield return new ErpnextPartySupplierSource(
                Name: name,
                Modified: reader.GetString(1),
                SupplierName: reader.GetString(2),
                SupplierType: GetNullableString(reader, 3),
                EmailId: GetNullableString(reader, 4),
                MobileNo: GetNullableString(reader, 5),
                TaxId: GetNullableString(reader, 6),
                Disabled: GetBool(reader, 7));
        }
    }

    // ---- JOIN-requiring methods: stacked follow-up PR ----

    /// <inheritdoc />
    public IAsyncEnumerable<ErpnextContactSource> ReadContactsAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "ReadContactsAsync: stacked follow-up PR. " +
            "Requires JOIN across tabContact + tabDynamic Link. " +
            "See PR description for scope decision.");

    /// <inheritdoc />
    public IAsyncEnumerable<ErpnextAddressSource> ReadAddressesAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "ReadAddressesAsync: stacked follow-up PR. " +
            "Requires JOIN across tabAddress + tabDynamic Link. " +
            "See PR description for scope decision.");

    /// <inheritdoc />
    public IAsyncEnumerable<ErpnextTaxTemplateSource> ReadTaxTemplatesAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "ReadTaxTemplatesAsync: stacked follow-up PR. " +
            "Requires JOIN across tabSales/Purchase Taxes and Charges Template + rate child tables. " +
            "See PR description for scope decision.");

    /// <inheritdoc />
    public IAsyncEnumerable<ErpnextJournalEntrySource> ReadJournalEntriesAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "ReadJournalEntriesAsync: stacked follow-up PR. " +
            "Requires JOIN across tabJournal Entry + tabJournal Entry Account. " +
            "See PR description for scope decision.");

    /// <inheritdoc />
    public IAsyncEnumerable<ErpnextSalesInvoiceSource> ReadSalesInvoicesAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "ReadSalesInvoicesAsync: stacked follow-up PR. " +
            "Requires JOIN across tabSales Invoice + tabSales Invoice Item. " +
            "USD-only assertion will be enforced. " +
            "See PR description for scope decision.");

    /// <inheritdoc />
    public IAsyncEnumerable<ErpnextPurchaseInvoiceSource> ReadPurchaseInvoicesAsync(
        CancellationToken ct = default)
        => throw new NotImplementedException(
            "ReadPurchaseInvoicesAsync: stacked follow-up PR. " +
            "Requires JOIN across tabPurchase Invoice + tabPurchase Invoice Item. " +
            "USD-only assertion will be enforced. " +
            "See PR description for scope decision.");

    // ---- Inventory (C5 census) ----

    /// <inheritdoc />
    public async Task<ErpnextSourceInventory> ReadInventoryAsync(
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);

        var mapped = new Dictionary<string, int>(StringComparer.Ordinal);
        var knownIrrelevant = new Dictionary<string, int>(StringComparer.Ordinal);
        var unmapped = new Dictionary<string, int>(StringComparer.Ordinal);

        // Enumerate all tab* tables in the restored DB and classify each.
        using var tablesCmd = connection.Connection.CreateCommand();

        // INFORMATION_SCHEMA.TABLES is read-only metadata access — still SELECT-only (C4 (c)).
        tablesCmd.CommandText =
            "SELECT TABLE_NAME " +
            "FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() " +
            "AND TABLE_NAME LIKE 'tab%' " +
            "ORDER BY TABLE_NAME";

        using var tablesReader = tablesCmd.ExecuteReader();
        var allTables = new List<string>();

        while (tablesReader.Read())
        {
            allTables.Add(tablesReader.GetString(0));
        }

        tablesReader.Close();

        foreach (var tableName in allTables)
        {
            ct.ThrowIfCancellationRequested();

            var rowCount = GetTableRowCount(connection.Connection, tableName);

            if (ErpnextDocTypeMap.IsMapped(tableName))
            {
                mapped[tableName] = rowCount;
                _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.InventoryEntry(
                    tableName, rowCount, "mapped"));
            }
            else if (KnownIrrelevantDocTypes.All.Contains(tableName))
            {
                knownIrrelevant[tableName] = rowCount;
                _logger.LogDebug("{Log}", ErpnextExtractionLogRedactor.InventoryEntry(
                    tableName, rowCount, "known-irrelevant"));
            }
            else
            {
                unmapped[tableName] = rowCount;
                // Unmapped-unknown is logged at Info level — it is a CIC review trigger.
                _logger.LogInformation("{Log}", ErpnextExtractionLogRedactor.InventoryEntry(
                    tableName, rowCount, "unmapped-unknown"));
            }
        }

        return new ErpnextSourceInventory
        {
            SourceMode = SourceAccessMode.MariaDbDump,
            MappedDocTypes = mapped,
            KnownIrrelevantDocTypes = knownIrrelevant,
            UnmappedUnknownDocTypes = unmapped,
        };
    }

    // ---- Private helpers ----

    /// <summary>
    /// Opens a connection to the restored DB by delegating to the factory.
    /// Each streaming method call gets a fresh connection (and a fresh throwaway
    /// schema); the factory disposes (drops the schema) when the caller
    /// <c>await using</c>-disposes the returned <see cref="RestoredConnection"/>.
    /// </summary>
    private async Task<RestoredConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var conn = await _connectionFactory.RestoreAndConnectAsync(runId: runId, ct: ct);
        return new RestoredConnection(conn, _connectionFactory);
    }

    /// <summary>
    /// Pairs an <see cref="IDbConnection"/> with its owning
    /// <see cref="IRestoredDbConnectionFactory"/> so a single <c>await using</c>
    /// on the caller side disposes both — closing the connection and dropping the
    /// throwaway schema.
    /// </summary>
    private sealed class RestoredConnection : IAsyncDisposable
    {
        private readonly IDbConnection _connection;
        private readonly IRestoredDbConnectionFactory _factory;

        internal RestoredConnection(IDbConnection connection, IRestoredDbConnectionFactory factory)
        {
            _connection = connection;
            _factory = factory;
        }

        /// <summary>The open <see cref="IDbConnection"/> to the throwaway restored schema.</summary>
        internal IDbConnection Connection => _connection;

        public async ValueTask DisposeAsync()
        {
            _connection.Close();
            await _factory.DisposeAsync();
        }
    }

    private static int GetTableRowCount(IDbConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        // Backtick-quote the table name to handle spaces in ERPNext tab* names.
        cmd.CommandText = $"SELECT COUNT(*) FROM `{tableName}`";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string? GetNullableString(IDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static bool GetBool(IDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return false;
        }

        // ERPNext stores boolean columns as tinyint(1); 0 = false, non-zero = true.
        return reader.GetInt32(ordinal) != 0;
    }

    /// <summary>
    /// Asserts a currency value is USD (case-insensitive) per CIC build parameter
    /// (ADR 0100 OQ-2 / directive). Throws <see cref="InvalidOperationException"/>
    /// for any non-USD value — fail loud, do NOT coerce.
    /// </summary>
    /// <remarks>
    /// The exception message includes DocType + externalRef + currency code but
    /// NOT monetary amounts (C9).
    /// </remarks>
    internal static void AssertUsd(string? currency, string docType, string externalRef)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            // Null/blank currency defaults to USD (the DTO comment confirms this convention).
            return;
        }

        if (!string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Non-USD currency detected and is out-of-v1-scope (ADR 0100 OQ-2). " +
                $"DocType={docType}, ExternalRef={externalRef}, Currency={currency}. " +
                "CIC confirmed all 4 LLCs are USD-only. A non-USD row indicates a data " +
                "anomaly — review the source before re-running. " +
                "DO NOT coerce to USD; fail loud per spec.");
        }
    }
}
