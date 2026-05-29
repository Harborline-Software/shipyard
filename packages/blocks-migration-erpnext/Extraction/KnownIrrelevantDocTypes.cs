using System.Collections.Generic;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// The allowlist of ERPNext system/framework DocTypes that the A0 extractor
/// deliberately ignores (ADR 0100 C5 / spec §4.2). These are NOT financial data;
/// listing them separately keeps the <c>_unmapped/</c> section of the migration
/// report signal-rich — only genuinely unknown, potentially-financial DocTypes
/// appear there.
/// </summary>
/// <remarks>
/// <para>
/// This allowlist is versioned with the ERPNext v15 schema. If a future ERPNext
/// version renames or adds system DocTypes, update this list deliberately — do NOT
/// silently widen it to suppress report noise.
/// </para>
/// <para>
/// The convention here is to list the raw <c>tab*</c> table names as they appear
/// in the dump (e.g. <c>"tabDocType"</c>, not <c>"DocType"</c>).
/// </para>
/// </remarks>
public static class KnownIrrelevantDocTypes
{
    /// <summary>
    /// The complete allowlist. Every table name in this set is treated as
    /// known-irrelevant in <see cref="IErpnextSourceExtractor.ReadInventoryAsync"/>.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        System.StringComparer.Ordinal)
    {
        // ---- Frappe framework meta-data DocTypes ----
        "tabDocType",
        "tabDocField",
        "tabDocPerm",
        "tabDocAction",
        "tabDocEvent",
        "tabDocType Link",
        "tabDocType State",
        "tabCustom Field",
        "tabCustom DocPerm",
        "tabCustomize Form",
        "tabCustomize Form Field",
        "tabProperty Setter",
        "tabClient Script",

        // ---- Frappe singles (key/value blob store for global config) ----
        "tabSingles",

        // ---- Frappe naming series / sequence state ----
        "tabSeries",
        "tabNaming Series",

        // ---- Frappe versioning / change log ----
        "tabVersion",
        "tabDocument Follow",

        // ---- Frappe user / session / auth tables ----
        "tabUser",
        "tabUser Permission",
        "tabUser Role",
        "tabUser Type",
        "tabUser Email Template",
        "tabHas Role",
        "tabRole",
        "tabRole Permission",
        "tabRole Profile",
        "tabSessions",
        "tabToken Cache",
        "tabOAuth Bearer Token",
        "tabOAuth Client",
        "tabOAuth Authorization Code",

        // ---- Frappe workflow / state machine ----
        "tabWorkflow",
        "tabWorkflow State",
        "tabWorkflow Action",
        "tabWorkflow Action Master",
        "tabWorkflow Transition",

        // ---- Frappe scheduler / background jobs ----
        "tabScheduled Job Type",
        "tabScheduled Job Log",
        "tabRQ Job",
        "tabRQ Worker",

        // ---- Frappe email queue / notification ----
        "tabEmail Queue",
        "tabEmail Queue Recipient",
        "tabNotification",
        "tabNotification Log",
        "tabEmail Domain",
        "tabEmail Account",
        "tabEmail Template",
        "tabAuto Email Report",
        "tabDigest",

        // ---- Frappe file / attachment infra ----
        "tabFile",
        "tabFile Manager",

        // ---- Frappe translation / locale ----
        "tabTranslation",
        "tabLanguage",

        // ---- Frappe error / log infra ----
        "tabError Log",
        "tabError Snapshot",
        "tabActivity Log",
        "tabAccess Log",
        "tabEvent Producer",
        "tabEvent Consumer",
        "tabEvent Update Log",
        "tabEvent Sync Log",

        // ---- Frappe print / report format ----
        "tabPrint Format",
        "tabPrint Settings",
        "tabReport",
        "tabQuery Report",
        "tabReport Filter",
        "tabRestricted IP",

        // ---- ERPNext global settings singletons ----
        "tabGlobal Defaults",
        "tabSystem Settings",
        "tabAccounting Settings",
        "tabStock Settings",
        "tabBuying Settings",
        "tabSelling Settings",
        "tabHR Settings",

        // ---- ERPNext naming/address config (not the Address records themselves) ----
        "tabAddress Template",
        "tabCountry",
        "tabState",
        "tabCurrency",
        "tabCurrency Exchange",
        "tabExchange Rate Revaluation",

        // ---- ERPNext UOM (unit of measure) meta ----
        "tabUOM",
        "tabUOM Conversion Factor",

        // ---- ERPNext item group / category meta (not financial) ----
        "tabItem Group",
        "tabBrand",
        "tabItem Attribute",

        // ---- ERPNext warehouse / location meta ----
        "tabWarehouse",
        "tabWarehouse Type",
        "tabBin",
    };
}
