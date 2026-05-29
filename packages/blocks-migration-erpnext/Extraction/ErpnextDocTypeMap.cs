using System.Collections.Generic;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// The v1 versioned shape map — the SINGLE authoritative table mapping each
/// supported <c>tab&lt;DocType&gt;</c> to its extractor method and target DTO
/// (ADR 0100 C5 / spec §4.1 / design §2.2). This is the SHAPE map (table to DTO);
/// the SEMANTIC maps (e.g. <c>account_type</c> to <c>GLAccountType</c>,
/// <c>voucher_type</c> to entry kind) live in the per-pass upserters (A1+) that
/// own them — A0 does not duplicate those mappings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Versioned with ERPNext v15.</b> The <c>tab*</c> schema + enum sets
/// (<c>account_type</c>, <c>voucher_type</c>) are pinned to ERPNext v15 per the
/// CIC build parameter (directive 2026-05-29). A future schema change is a
/// deliberate map revision here, not a silent drift.
/// </para>
/// <para>
/// <b>Unknown DocTypes are COUNTED, not silently dropped (C5 C-MAP).</b>
/// <see cref="IErpnextSourceExtractor.ReadInventoryAsync"/> partitions every
/// <c>tab*</c> table in the dump into mapped / known-irrelevant /
/// unmapped-unknown; only the mapped tables route through this map.
/// </para>
/// </remarks>
public static class ErpnextDocTypeMap
{
    /// <summary>
    /// ERPNext v15 version pin. This constant is the authoritative version label
    /// recorded in the migration report; it is NOT a runtime version check.
    /// </summary>
    public const string ErpnextVersion = "v15";

    /// <summary>
    /// The primary <c>tab*</c> table for each mapped entry — the table queried
    /// for the header record. Key = raw table name (e.g. <c>"tabAccount"</c>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, ErpnextDocTypeEntry> PrimaryTables =
        new Dictionary<string, ErpnextDocTypeEntry>(System.StringComparer.Ordinal)
        {
            // ---- Pass-1: chart of accounts ----
            ["tabAccount"] = new(
                TableName: "tabAccount",
                DocTypeName: "Account",
                ExtractorMethod: "ReadAccountsAsync",
                TargetDto: "ErpnextAccountSource",
                ChildTable: null),

            ["tabCost Center"] = new(
                TableName: "tabCost Center",
                DocTypeName: "Cost Center",
                ExtractorMethod: "ReadCostCentersAsync",
                TargetDto: "ErpnextCostCenterSource",
                ChildTable: null),

            // ---- Pass-2.2: fiscal years (joined to Fiscal Year Company for CompanyShortName) ----
            ["tabFiscal Year"] = new(
                TableName: "tabFiscal Year",
                DocTypeName: "Fiscal Year",
                ExtractorMethod: "ReadFiscalYearsAsync",
                TargetDto: "ErpnextFiscalYearSource",
                ChildTable: "tabFiscal Year Company"),

            // ---- Pass-2.1: parties ----
            ["tabCustomer"] = new(
                TableName: "tabCustomer",
                DocTypeName: "Customer",
                ExtractorMethod: "ReadCustomersAsync",
                TargetDto: "ErpnextPartyCustomerSource",
                ChildTable: null),

            ["tabSupplier"] = new(
                TableName: "tabSupplier",
                DocTypeName: "Supplier",
                ExtractorMethod: "ReadSuppliersAsync",
                TargetDto: "ErpnextPartySupplierSource",
                ChildTable: null),

            // Contact and Address require Dynamic Link join — stacked follow-up PR
            ["tabContact"] = new(
                TableName: "tabContact",
                DocTypeName: "Contact",
                ExtractorMethod: "ReadContactsAsync",
                TargetDto: "ErpnextContactSource",
                ChildTable: "tabDynamic Link"),

            ["tabAddress"] = new(
                TableName: "tabAddress",
                DocTypeName: "Address",
                ExtractorMethod: "ReadAddressesAsync",
                TargetDto: "ErpnextAddressSource",
                ChildTable: "tabDynamic Link"),

            // ---- Pass-2.3: tax ----
            // Both sales and purchase tax templates map to ErpnextTaxTemplateSource;
            // both are scanned in ReadTaxTemplatesAsync (stacked follow-up PR)
            ["tabSales Taxes and Charges Template"] = new(
                TableName: "tabSales Taxes and Charges Template",
                DocTypeName: "Sales Taxes and Charges Template",
                ExtractorMethod: "ReadTaxTemplatesAsync",
                TargetDto: "ErpnextTaxTemplateSource",
                ChildTable: "tabSales Taxes and Charges"),

            ["tabPurchase Taxes and Charges Template"] = new(
                TableName: "tabPurchase Taxes and Charges Template",
                DocTypeName: "Purchase Taxes and Charges Template",
                ExtractorMethod: "ReadTaxTemplatesAsync",
                TargetDto: "ErpnextTaxTemplateSource",
                ChildTable: "tabPurchase Taxes and Charges"),

            // ---- Pass-3 / Pass-4.4: journal entries ----
            ["tabJournal Entry"] = new(
                TableName: "tabJournal Entry",
                DocTypeName: "Journal Entry",
                ExtractorMethod: "ReadJournalEntriesAsync",
                TargetDto: "ErpnextJournalEntrySource",
                ChildTable: "tabJournal Entry Account"),

            // ---- Pass-4.1: sales invoices ----
            ["tabSales Invoice"] = new(
                TableName: "tabSales Invoice",
                DocTypeName: "Sales Invoice",
                ExtractorMethod: "ReadSalesInvoicesAsync",
                TargetDto: "ErpnextSalesInvoiceSource",
                ChildTable: "tabSales Invoice Item"),

            // ---- Pass-4.2: purchase invoices ----
            ["tabPurchase Invoice"] = new(
                TableName: "tabPurchase Invoice",
                DocTypeName: "Purchase Invoice",
                ExtractorMethod: "ReadPurchaseInvoicesAsync",
                TargetDto: "ErpnextPurchaseInvoiceSource",
                ChildTable: "tabPurchase Invoice Item"),
        };

    /// <summary>
    /// Returns true if <paramref name="tableName"/> is a mapped primary table in v1.
    /// </summary>
    public static bool IsMapped(string tableName) =>
        PrimaryTables.ContainsKey(tableName);
}

/// <summary>
/// One entry in the <see cref="ErpnextDocTypeMap"/> — the shape mapping for a single
/// ERPNext DocType.
/// </summary>
/// <param name="TableName">The primary <c>tab*</c> table name (e.g. <c>"tabAccount"</c>).</param>
/// <param name="DocTypeName">The human-readable DocType name (e.g. <c>"Account"</c>).</param>
/// <param name="ExtractorMethod">The <see cref="IErpnextSourceExtractor"/> method name that extracts this type.</param>
/// <param name="TargetDto">The frozen <c>Erpnext*Source</c> DTO type name this maps to.</param>
/// <param name="ChildTable">Optional child table joined to build the DTO (e.g. <c>"tabJournal Entry Account"</c>); null for single-table DocTypes.</param>
public sealed record ErpnextDocTypeEntry(
    string TableName,
    string DocTypeName,
    string ExtractorMethod,
    string TargetDto,
    string? ChildTable);
