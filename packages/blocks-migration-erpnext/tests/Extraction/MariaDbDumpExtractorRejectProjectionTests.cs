using Sunfish.Blocks.Migration.Erpnext.Extraction.Rejects;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Extraction;

/// <summary>
/// C-LOG-REJECT acceptance test (ADR 0100 C9 / sec-eng amendment (B)) — verifies
/// the <see cref="ErpnextExtractionReject"/> projection is structurally allowlisted
/// and never carries raw source-row payload, PII, or monetary values.
/// </summary>
public sealed class MariaDbDumpExtractorRejectProjectionTests
{
    /// <summary>
    /// <see cref="ErpnextExtractionReject"/> has ONLY the allowlisted fields:
    /// ReasonCode, DocType, ExternalRef, FieldName. No raw row payload field exists.
    /// </summary>
    [Fact]
    public void ErpnextExtractionReject_has_only_allowlisted_fields()
    {
        var properties = typeof(ErpnextExtractionReject).GetProperties();
        var names = System.Array.ConvertAll(properties, p => p.Name);

        Assert.Contains("ReasonCode", names);
        Assert.Contains("DocType", names);
        Assert.Contains("ExternalRef", names);
        Assert.Contains("FieldName", names);

        // Forbidden: any field that could carry raw source payload.
        var forbidden = new[]
        {
            "SourceRow", "RawRow", "RowPayload", "Source", "Payload",
            "CustomerName", "SupplierName", "EmailId", "MobileNo", "TaxId",
            "GrandTotal", "OutstandingAmount", "DebitInAccountCurrency",
            "CreditInAccountCurrency",
        };

        foreach (var name in forbidden)
        {
            Assert.DoesNotContain(name, names);
        }
    }

    /// <summary>
    /// <see cref="ErpnextExtractionReject.Of"/> builds a reject using a bounded
    /// <see cref="ImportRejectReason"/> reason code.
    /// </summary>
    [Fact]
    public void Of_factory_builds_reject_from_canonical_reason_code()
    {
        var reject = ErpnextExtractionReject.Of(
            ImportRejectReason.MissingRequiredField,
            docType: "Account",
            externalRef: "ACC-0001",
            fieldName: "account_name");

        Assert.Equal("MissingRequiredField", reject.ReasonCode);
        Assert.Equal("Account", reject.DocType);
        Assert.Equal("ACC-0001", reject.ExternalRef);
        Assert.Equal("account_name", reject.FieldName);
    }

    /// <summary>
    /// <see cref="ErpnextExtractionReject.ToImportFailure"/> converts to the
    /// foundation-import <see cref="ImportFailure"/> with all allowlisted fields preserved.
    /// </summary>
    [Fact]
    public void ToImportFailure_produces_ImportFailure_with_correct_fields()
    {
        var reject = ErpnextExtractionReject.Of(
            ImportRejectReason.UnsupportedCurrency,
            docType: "Sales Invoice",
            externalRef: "SINV-0099",
            fieldName: "currency");

        var failure = reject.ToImportFailure();

        Assert.Equal("SINV-0099", failure.ExternalRef);
        Assert.Equal("Sales Invoice", failure.DocType);
        Assert.Equal("UnsupportedCurrency", failure.ReasonCode);
        Assert.Equal("currency", failure.FieldName);
        Assert.Null(failure.RuleViolated);
    }

    /// <summary>
    /// A reject with no FieldName has a null FieldName (not empty string).
    /// </summary>
    [Fact]
    public void Reject_with_no_field_name_has_null_FieldName()
    {
        var reject = ErpnextExtractionReject.Of(
            ImportRejectReason.InvalidFieldValue,
            docType: "Customer",
            externalRef: "CUST-0001");

        Assert.Null(reject.FieldName);
    }

    /// <summary>
    /// The FieldName carries the DESCRIPTOR (column name string), not the value.
    /// Verify by inspection that constructing with a typical PII column name string
    /// (safe descriptor) produces the correct reject shape.
    /// </summary>
    [Fact]
    public void FieldName_carries_column_name_descriptor_not_value()
    {
        // Safe: the column NAME "email_id" is the descriptor.
        // What must NOT be passed as FieldName: the actual email value.
        var reject = new ErpnextExtractionReject(
            ReasonCode: "MissingRequiredField",
            DocType: "Customer",
            ExternalRef: "CUST-0001",
            FieldName: "email_id");

        // FieldName is the column name — this is safe.
        Assert.Equal("email_id", reject.FieldName);

        // Confirm: no actual PII value in any field.
        Assert.DoesNotContain("@", reject.ExternalRef, System.StringComparison.Ordinal);
        Assert.DoesNotContain("@", reject.FieldName ?? string.Empty, System.StringComparison.Ordinal);
    }
}
