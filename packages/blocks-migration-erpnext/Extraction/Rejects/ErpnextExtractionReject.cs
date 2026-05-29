using Sunfish.Foundation.Import.Outcomes;

namespace Sunfish.Blocks.Migration.Erpnext.Extraction.Rejects;

/// <summary>
/// The A0-extraction-level reject projection — a structured, ALLOWLISTED record
/// of why an ERPNext source record could not be extracted into a typed
/// <c>Erpnext*Source</c> DTO (ADR 0100 C9 / C-LOG-REJECT acceptance test;
/// sec-eng amendment (B)).
/// </summary>
/// <remarks>
/// <para>
/// <b>Allowlisted projection invariant (C9 / C-LOG-REJECT).</b> This record
/// carries ONLY:
/// <list type="bullet">
///   <item><see cref="ReasonCode"/> — a bounded, machine-readable reject code.</item>
///   <item><see cref="DocType"/> — the ERPNext DocType name.</item>
///   <item><see cref="ExternalRef"/> — the ERPNext <c>name</c> (opaque id, NOT PII).</item>
///   <item><see cref="FieldName"/> — optional offending FIELD NAME (never the value).</item>
/// </list>
/// There is intentionally NO field that can carry the raw <c>Erpnext*Source</c>
/// payload, a party's email/phone/name, or a per-line monetary amount. The C9
/// prohibition is structural: these are the only fields the type has.
/// </para>
/// <para>
/// This type is distinct from <see cref="ImportFailure"/> (the foundation-import
/// primitive) to keep the extraction-level vs. pass-level reject surfaces bounded
/// independently. An extraction reject is converted to an <see cref="ImportFailure"/>
/// by the orchestrator (A7) when it reaches the pass boundary.
/// </para>
/// </remarks>
/// <param name="ReasonCode">A bounded, machine-readable reject reason (see <see cref="ImportRejectReason"/> for canonical values).</param>
/// <param name="DocType">The ERPNext DocType the rejected record belongs to (e.g. <c>"Account"</c>).</param>
/// <param name="ExternalRef">The ERPNext <c>name</c> natural key (opaque id; safe to log).</param>
/// <param name="FieldName">Optional NAME of the offending field — never the field's VALUE.</param>
public sealed record ErpnextExtractionReject(
    string ReasonCode,
    string DocType,
    string ExternalRef,
    string? FieldName = null)
{
    /// <summary>
    /// Builds a reject from a canonical <see cref="ImportRejectReason"/> (the
    /// recommended path to keep reason codes bounded and stable for the
    /// reject-bin report).
    /// </summary>
    public static ErpnextExtractionReject Of(
        ImportRejectReason reason,
        string docType,
        string externalRef,
        string? fieldName = null)
        => new(reason.ToString(), docType, externalRef, fieldName);

    /// <summary>
    /// Converts this extraction-level reject to the foundation-import
    /// <see cref="ImportFailure"/> primitive for pass-boundary reporting.
    /// </summary>
    public ImportFailure ToImportFailure()
        => new(ExternalRef, DocType, ReasonCode, FieldName);
}
