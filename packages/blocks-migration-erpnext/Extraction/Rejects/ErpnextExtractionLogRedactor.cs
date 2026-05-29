namespace Sunfish.Blocks.Migration.Erpnext.Extraction.Rejects;

/// <summary>
/// C-LOG enforcement helper (ADR 0100 C9 / C-LOG acceptance test). Provides
/// the ONLY sanctioned logging verbs for extraction events — each method emits
/// ONLY non-sensitive structured data: DocType, opaque <c>externalRef</c>
/// (ERPNext <c>name</c>), counts, and run-id. All PII-bearing or value-bearing
/// fields (party names, emails, phones, monetary amounts, account values) are
/// EXCLUDED by construction.
/// </summary>
/// <remarks>
/// <para>
/// The C-LOG invariant is enforced here rather than at call sites so the
/// "what is safe to log" decision is made once and enforced structurally.
/// Callers MUST route extraction-event logging through this class — they MUST NOT
/// interpolate raw <c>Erpnext*Source</c> fields into log messages directly.
/// </para>
/// <para>
/// The arch-test <c>MariaDbDumpExtractorLogRedactionTests</c> verifies no
/// extraction-level log call carries PII-marker field names.
/// </para>
/// </remarks>
public static class ErpnextExtractionLogRedactor
{
    /// <summary>
    /// Returns a safe, loggable summary for a successfully extracted record.
    /// Carries ONLY: DocType + opaque externalRef. No field values.
    /// </summary>
    public static string ExtractedRecord(string docType, string externalRef)
        => $"Extracted {docType} [{externalRef}]";

    /// <summary>
    /// Returns a safe, loggable summary for a rejected record.
    /// Carries ONLY: DocType + opaque externalRef + reasonCode + optional field NAME
    /// (never value). No PII, no monetary amounts.
    /// </summary>
    public static string RejectedRecord(
        string docType,
        string externalRef,
        string reasonCode,
        string? fieldName = null)
    {
        var field = fieldName is not null ? $" field={fieldName}" : string.Empty;
        return $"Rejected {docType} [{externalRef}] reason={reasonCode}{field}";
    }

    /// <summary>
    /// Returns a safe, loggable summary for a completed DocType pass.
    /// Carries ONLY: DocType + counts + runId. No record contents.
    /// </summary>
    public static string DocTypeCompleted(
        string docType,
        int extracted,
        int rejected,
        string runId)
        => $"DocType={docType} extracted={extracted} rejected={rejected} runId={runId}";

    /// <summary>
    /// Returns a safe, loggable summary for a non-USD currency assertion failure.
    /// Carries ONLY: DocType + opaque externalRef + currency code. No amounts.
    /// </summary>
    public static string NonUsdCurrencyRejected(
        string docType,
        string externalRef,
        string currency)
        => $"Non-USD currency rejected: {docType} [{externalRef}] currency={currency}";

    /// <summary>
    /// Returns a safe, loggable inventory summary.
    /// Carries ONLY: DocType name + row count partition label. No record contents.
    /// </summary>
    public static string InventoryEntry(string tableName, int rowCount, string partition)
        => $"Inventory {partition}: {tableName} rows={rowCount}";
}
