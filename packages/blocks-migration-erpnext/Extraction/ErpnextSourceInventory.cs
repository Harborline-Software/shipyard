using System.Collections.Generic;
using Sunfish.Foundation.Import.Extraction;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// <summary>
/// Return type of <see cref="IErpnextSourceExtractor.ReadInventoryAsync"/> —
/// the census of DocTypes present in the source vs. the v1 mapping (ADR 0100 C5
/// / C-MAP acceptance test).
/// </summary>
/// <remarks>
/// <para>
/// Three partitions (C-MAP):
/// <list type="bullet">
///   <item>
///     <see cref="MappedDocTypes"/> — present in <see cref="ErpnextDocTypeMap"/> and
///     will be extracted by the matching <c>Read*Async</c> method.
///   </item>
///   <item>
///     <see cref="KnownIrrelevantDocTypes"/> — on the
///     <see cref="KnownIrrelevantDocTypes"/> allowlist (<c>tabDocType</c>,
///     <c>tabSingles</c>, session/permission tables, etc.); deliberately ignored —
///     never financial data.
///   </item>
///   <item>
///     <see cref="UnmappedUnknownDocTypes"/> — present in the dump, NOT in the map,
///     NOT on the ignore allowlist. These are COUNTED and listed; they are visible in
///     the migration report's <c>_unmapped/</c> section and are never silently
///     dropped (C5). A custom <c>tabProperty</c>/<c>tabLease</c> DocType would
///     surface here when the A0 CIC open question H-CIC-2 is open.
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>C9 discipline.</b> The inventory carries ONLY DocType names and row-counts —
/// never the row contents, field values, or any PII.
/// </para>
/// </remarks>
public sealed class ErpnextSourceInventory
{
    /// <summary>
    /// The access mode that produced this inventory — recorded in the migration
    /// report for C6 provenance. v1's sole value is
    /// <see cref="SourceAccessMode.MariaDbDump"/>.
    /// </summary>
    public SourceAccessMode SourceMode { get; init; } = SourceAccessMode.MariaDbDump;

    /// <summary>
    /// DocTypes present in the dump that are in <see cref="ErpnextDocTypeMap"/> and
    /// will be extracted normally. Key = tab-prefixed table name (e.g.
    /// <c>"tabAccount"</c>); value = row count.
    /// </summary>
    public IReadOnlyDictionary<string, int> MappedDocTypes { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// DocTypes present in the dump that are on the known-irrelevant allowlist
    /// (system/framework DocTypes — <c>tabDocType</c>, <c>tabSingles</c>,
    /// <c>tabSeries</c>, <c>tabVersion</c>, session/permission tables, etc.).
    /// These are deliberately NOT extracted; listing them keeps the
    /// <see cref="UnmappedUnknownDocTypes"/> section signal-rich.
    /// Key = table name; value = row count.
    /// </summary>
    public IReadOnlyDictionary<string, int> KnownIrrelevantDocTypes { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// DocTypes present in the dump that are NOT in <see cref="ErpnextDocTypeMap"/>
    /// AND NOT on the known-irrelevant allowlist. These are visible to CIC in the
    /// migration report's <c>_unmapped/</c> section — never silently dropped (C5
    /// C-MAP acceptance test). A non-zero count here is a CIC review trigger, not
    /// an error. Key = table name; value = row count.
    /// </summary>
    public IReadOnlyDictionary<string, int> UnmappedUnknownDocTypes { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// Total source row count across ALL DocType partitions (mapped + known-irrelevant
    /// + unmapped-unknown). Informational — used for the overall run report.
    /// </summary>
    public int TotalSourceRows =>
        SumValues(MappedDocTypes) + SumValues(KnownIrrelevantDocTypes) + SumValues(UnmappedUnknownDocTypes);

    private static int SumValues(IReadOnlyDictionary<string, int> d)
    {
        var total = 0;
        foreach (var v in d.Values)
        {
            total += v;
        }

        return total;
    }
}
