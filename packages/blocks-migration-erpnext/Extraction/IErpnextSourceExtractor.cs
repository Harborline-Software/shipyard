using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialAr.Migration;
using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialTax.Migration;
using Sunfish.Blocks.People.Foundation.Migration;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// Read-only ERPNext source extractor — the C6 access-mode seam (ADR 0100 C6).
/// v1 ships exactly ONE implementation (<see cref="MariaDbDumpExtractor"/>).
/// A future REST/CSV adapter implements this interface with zero changes to A1–A6
/// (the C-MODE invariant): the upsert passes depend ONLY on this interface and the
/// frozen <c>Erpnext*Source</c> DTOs — never on a concrete adapter or a
/// source-table name.
/// </summary>
/// <remarks>
/// <para>
/// <b>STRICTLY READ-ONLY (ADR 0100 C4 clean-room).</b> This interface exposes
/// ONLY read operations (<c>Read*Async</c> methods + <see cref="ReadInventoryAsync"/>).
/// There is intentionally no write / update / delete / upsert method against the
/// ERPNext source. An arch-test asserts no member of this interface or any
/// <c>*/Extraction/</c> type performs a source write (C-CLEANROOM).
/// </para>
/// <para>
/// <b>Streaming (<c>IAsyncEnumerable</c>), not materialized lists.</b> CIC's
/// portfolio is ~10K records across DocTypes; streaming keeps the pass budget
/// honest and avoids materializing every journal-entry line in memory at once.
/// The orchestrator (A7) consumes per-record and threads each into the matching
/// upserter, which returns <c>ImportOutcome&lt;T&gt;</c>.
/// </para>
/// <para>
/// <b>Access mode.</b> The mode that produced a run is visible ONLY via
/// <see cref="ReadInventoryAsync"/> (the <see cref="ErpnextSourceInventory.SourceMode"/>
/// descriptor). Above the seam, A1–A6 are mode-agnostic.
/// </para>
/// </remarks>
public interface IErpnextSourceExtractor
{
    // ---- Pass-1: chart of accounts ----

    /// <summary>Streams every Account row as a typed DTO (Pass-1 chart).</summary>
    IAsyncEnumerable<ErpnextAccountSource> ReadAccountsAsync(CancellationToken ct = default);

    /// <summary>Streams every Cost Center row as a typed DTO (Pass-1 cost-centers).</summary>
    IAsyncEnumerable<ErpnextCostCenterSource> ReadCostCentersAsync(CancellationToken ct = default);

    // ---- Pass-2: reference data ----

    /// <summary>
    /// Streams every Fiscal Year row, joined to Fiscal Year Company
    /// to populate <see cref="ErpnextFiscalYearSource.CompanyShortName"/> (Pass-2.2 periods).
    /// </summary>
    IAsyncEnumerable<ErpnextFiscalYearSource> ReadFiscalYearsAsync(CancellationToken ct = default);

    /// <summary>Streams every Customer row (Pass-2.1 parties).</summary>
    IAsyncEnumerable<ErpnextPartyCustomerSource> ReadCustomersAsync(CancellationToken ct = default);

    /// <summary>Streams every Supplier row (Pass-2.1 parties).</summary>
    IAsyncEnumerable<ErpnextPartySupplierSource> ReadSuppliersAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams every Contact row with its Dynamic Link child rows
    /// (Pass-2.1 parties). Each contact's <see cref="ErpnextContactSource.Links"/> is
    /// populated from the Dynamic Link table filtered to parenttype = 'Contact'.
    /// </summary>
    /// <remarks>
    /// Requires a JOIN across two tables — stacked follow-up PR.
    /// This method throws <see cref="NotImplementedException"/> in this PR.
    /// </remarks>
    IAsyncEnumerable<ErpnextContactSource> ReadContactsAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams every Address row with its Dynamic Link child rows
    /// (Pass-2.1 parties). Each address's <see cref="ErpnextAddressSource.Links"/> is
    /// populated from the Dynamic Link table filtered to parenttype = 'Address'.
    /// </summary>
    /// <remarks>
    /// Requires a JOIN across two tables — stacked follow-up PR.
    /// This method throws <see cref="NotImplementedException"/> in this PR.
    /// </remarks>
    IAsyncEnumerable<ErpnextAddressSource> ReadAddressesAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams every tax template row, merging Sales Taxes and Charges Template
    /// with Purchase Taxes and Charges Template, with rate rows joined from the
    /// corresponding child tables (Pass-2.3 tax).
    /// </summary>
    /// <remarks>
    /// Requires a JOIN across parent + child tables — stacked follow-up PR.
    /// This method throws <see cref="NotImplementedException"/> in this PR.
    /// </remarks>
    IAsyncEnumerable<ErpnextTaxTemplateSource> ReadTaxTemplatesAsync(CancellationToken ct = default);

    // ---- Pass-3/4: transactional ----

    /// <summary>
    /// Streams every journal entry with its account lines (Journal Entry joined to
    /// Journal Entry Account) — Pass-3 opening balance + Pass-4.4 manual JEs.
    /// </summary>
    /// <remarks>
    /// Requires a JOIN across two tables — stacked follow-up PR.
    /// This method throws <see cref="NotImplementedException"/> in this PR.
    /// </remarks>
    IAsyncEnumerable<ErpnextJournalEntrySource> ReadJournalEntriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams every sales invoice with its line items (Sales Invoice joined to
    /// Sales Invoice Item) — Pass-4.1. Non-USD rows throw
    /// <see cref="InvalidOperationException"/> (USD-only assertion per ADR 0100 OQ-2).
    /// </summary>
    /// <remarks>
    /// Requires a JOIN across two tables — stacked follow-up PR.
    /// This method throws <see cref="NotImplementedException"/> in this PR.
    /// </remarks>
    IAsyncEnumerable<ErpnextSalesInvoiceSource> ReadSalesInvoicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Streams every purchase invoice with its line items (Purchase Invoice joined to
    /// Purchase Invoice Item) — Pass-4.2. Non-USD rows throw
    /// <see cref="InvalidOperationException"/> (USD-only assertion per ADR 0100 OQ-2).
    /// </summary>
    /// <remarks>
    /// Requires a JOIN across two tables — stacked follow-up PR.
    /// This method throws <see cref="NotImplementedException"/> in this PR.
    /// </remarks>
    IAsyncEnumerable<ErpnextPurchaseInvoiceSource> ReadPurchaseInvoicesAsync(CancellationToken ct = default);

    // ---- Inventory (C5 DocType-census) ----

    /// <summary>
    /// Census of DocTypes present in the source vs. the v1 mapping (C5 / C-MAP
    /// acceptance test). Returns three partitions: mapped (will be extracted),
    /// known-irrelevant (system/framework tables; intentionally ignored), and
    /// unmapped-unknown (present in dump, not in map, not on ignore allowlist —
    /// visible in the migration report's <c>_unmapped/</c> section, never silently
    /// dropped). Also carries the <see cref="ErpnextSourceInventory.SourceMode"/>
    /// descriptor for the C6 provenance forward-hook.
    /// </summary>
    Task<ErpnextSourceInventory> ReadInventoryAsync(CancellationToken ct = default);
}
