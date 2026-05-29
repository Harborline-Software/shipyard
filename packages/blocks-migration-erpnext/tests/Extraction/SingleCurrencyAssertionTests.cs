using System;
using Sunfish.Blocks.Migration.Erpnext.Extraction;
using Xunit;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Extraction;

/// <summary>
/// USD-only assertion tests (ADR 0100 OQ-2 / CIC build parameter).
/// A non-USD currency row must throw <see cref="InvalidOperationException"/> —
/// fail loud, do NOT coerce (spec §USD-only directive).
/// </summary>
public sealed class SingleCurrencyAssertionTests
{
    /// <summary>
    /// USD (exactly) passes the assertion without throwing.
    /// </summary>
    [Theory]
    [InlineData("USD")]
    [InlineData("usd")]
    [InlineData("Usd")]
    public void Usd_passes_assertion(string currency)
    {
        // Should NOT throw.
        MariaDbDumpExtractor.AssertUsd(currency, "Sales Invoice", "SINV-0001");
    }

    /// <summary>
    /// Null or blank currency passes the assertion (defaults to USD per DTO spec).
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_currency_passes_assertion(string? currency)
    {
        // Should NOT throw — null/blank defaults to USD.
        MariaDbDumpExtractor.AssertUsd(currency, "Sales Invoice", "SINV-0001");
    }

    /// <summary>
    /// Non-USD currency throws <see cref="InvalidOperationException"/>.
    /// </summary>
    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("CAD")]
    [InlineData("MXN")]
    [InlineData("JPY")]
    public void Non_usd_currency_throws_InvalidOperationException(string currency)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MariaDbDumpExtractor.AssertUsd(currency, "Sales Invoice", "SINV-0099"));

        Assert.Contains("Non-USD currency", ex.Message, StringComparison.OrdinalIgnoreCase);
        // The exception message must include the currency code and docType.
        Assert.Contains(currency, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales Invoice", ex.Message, StringComparison.OrdinalIgnoreCase);
        // The exception message must NOT include monetary amounts (C9).
        Assert.DoesNotContain("900.00", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("1000.00", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The assertion includes ExternalRef in the exception message (opaque id —
    /// safe to include; it's the ERPNext document name, not a monetary value).
    /// </summary>
    [Fact]
    public void Exception_message_includes_external_ref_not_amounts()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            MariaDbDumpExtractor.AssertUsd("EUR", "Purchase Invoice", "PINV-0042"));

        Assert.Contains("PINV-0042", ex.Message, StringComparison.Ordinal);
        // Confirm: no amount in the message.
        Assert.DoesNotContain("amount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The 4-LLC CIC inventory assumption: all 4 companies (Acero/Bosco/Escola/Shirin)
    /// are USD-only. This test captures the assertion as a documented invariant.
    /// A non-USD row is an anomaly, not a common path.
    /// </summary>
    [Fact]
    public void Four_llc_usd_assumption_is_an_explicit_invariant()
    {
        // ADR 0100 OQ-2 / CIC directive: all 4 LLCs confirmed USD-only.
        // Any non-USD row is a data anomaly — fail loud per spec.
        // This test documents the invariant; the preceding tests enforce it.
        var companies = new[] { "Acero", "Bosco", "Escola", "Shirin" };
        Assert.Equal(4, companies.Length);

        // Each company's records should have currency = "USD" or null (defaults USD).
        // Confirmed by CIC directive 2026-05-29.
        foreach (var company in companies)
        {
            // Should not throw for USD.
            MariaDbDumpExtractor.AssertUsd("USD", "Sales Invoice", $"SINV-{company}-001");
        }
    }
}
