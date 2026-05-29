using System.Linq;
using System.Reflection;
using Sunfish.Blocks.Migration.Erpnext.Extraction.Rejects;
using Xunit;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Extraction;

/// <summary>
/// C-LOG acceptance test (ADR 0100 C9) — verifies the log-redactor emits ONLY
/// safe metadata (DocType, opaque externalRef, counts, runId) and NEVER carries
/// PII-marker field names or value placeholders.
/// </summary>
public sealed class MariaDbDumpExtractorLogRedactionTests
{
    // PII field names that must never appear in log output from the extractor.
    private static readonly string[] PiiFieldNames =
    {
        "customer_name", "supplier_name", "email_id", "mobile_no", "tax_id",
        "address_line1", "address_line2", "city", "state", "pincode",
        "CustomerName", "SupplierName", "EmailId", "MobileNo", "TaxId",
        "AddressLine1", "AddressLine2", "City", "State", "Pincode",
    };

    // Monetary value markers that must never appear in log output.
    private static readonly string[] MonetaryMarkers =
    {
        "grand_total", "outstanding_amount", "debit_in_account_currency",
        "credit_in_account_currency", "GrandTotal", "OutstandingAmount",
        "DebitInAccountCurrency", "CreditInAccountCurrency",
        "rate", "amount", "Rate", "Amount",
    };

    /// <summary>
    /// <see cref="ErpnextExtractionLogRedactor.ExtractedRecord"/> output must not
    /// contain PII field names.
    /// </summary>
    [Fact]
    public void ExtractedRecord_output_contains_no_pii_field_names()
    {
        var output = ErpnextExtractionLogRedactor.ExtractedRecord("Customer", "CUST-0001");

        foreach (var field in PiiFieldNames)
        {
            Assert.DoesNotContain(field, output, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// <see cref="ErpnextExtractionLogRedactor.RejectedRecord"/> output must not
    /// contain PII field names — it carries only the field NAME (descriptor), not value.
    /// </summary>
    [Fact]
    public void RejectedRecord_output_contains_no_pii_values()
    {
        var output = ErpnextExtractionLogRedactor.RejectedRecord(
            "Customer", "CUST-0001", "MissingRequiredField", fieldName: "email_id");

        // The field NAME "email_id" IS allowed (it's the descriptor, not the value).
        // What must NOT appear: the actual email value (e.g. "billing@acme.example").
        Assert.DoesNotContain("billing@acme.example", output, System.StringComparison.Ordinal);
        Assert.DoesNotContain("mobile_no_value", output, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <see cref="ErpnextExtractionLogRedactor.DocTypeCompleted"/> output must not
    /// contain monetary amount markers.
    /// </summary>
    [Fact]
    public void DocTypeCompleted_output_contains_no_monetary_markers()
    {
        var output = ErpnextExtractionLogRedactor.DocTypeCompleted(
            "Sales Invoice", extracted: 10, rejected: 0, runId: "abc123");

        foreach (var marker in MonetaryMarkers)
        {
            Assert.DoesNotContain(marker, output, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// <see cref="ErpnextExtractionLogRedactor.NonUsdCurrencyRejected"/> output
    /// contains currency code but NOT monetary amounts.
    /// </summary>
    [Fact]
    public void NonUsdCurrencyRejected_output_contains_currency_code_not_amounts()
    {
        var output = ErpnextExtractionLogRedactor.NonUsdCurrencyRejected(
            "Sales Invoice", "SINV-0099", "EUR");

        Assert.Contains("EUR", output, System.StringComparison.Ordinal);
        Assert.Contains("SINV-0099", output, System.StringComparison.Ordinal);
        // Ensure no amount literals appear.
        Assert.DoesNotContain("900.00", output, System.StringComparison.Ordinal);
        Assert.DoesNotContain("grand_total", output, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <see cref="ErpnextExtractionLogRedactor.InventoryEntry"/> output contains only
    /// DocType + partition + row count — no record contents.
    /// </summary>
    [Fact]
    public void InventoryEntry_output_contains_only_safe_metadata()
    {
        var output = ErpnextExtractionLogRedactor.InventoryEntry("tabAccount", 42, "mapped");

        Assert.Contains("tabAccount", output, System.StringComparison.Ordinal);
        Assert.Contains("42", output, System.StringComparison.Ordinal);
        Assert.Contains("mapped", output, System.StringComparison.Ordinal);

        foreach (var pii in PiiFieldNames)
        {
            Assert.DoesNotContain(pii, output, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
