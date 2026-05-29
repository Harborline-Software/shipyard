using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Migration;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Sunfish.Foundation.Import.Census;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;
using TaxOutcome = Sunfish.Foundation.Import.Outcomes.ImportOutcome<Sunfish.Blocks.FinancialTax.Models.TaxCode>;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// Fixture tests for the Pass-2 ERPNext tax upserter against synthetic
/// ERPNext-shaped fixtures (no real dump). Covers insert/update/skip
/// idempotency, the TaxRate child fan-out, jurisdiction synthesis, the reject
/// path, and census conservation (ADR 0100 A2.3).
/// </summary>
public class Pass2TaxUpserterTests
{
    private const string PayableAccountId = "erp-account-tax-payable";

    private sealed class Fx
    {
        public InMemoryAccountResolver Accounts { get; } = new();
        public InMemoryTaxCodeStore Codes { get; } = new();
        public InMemoryTaxRateLookup Rates { get; }
        public InMemoryTaxJurisdictionStore Jurisdictions { get; } = new();
        public Pass2TaxUpserter Sut { get; }
        public FL.ChartOfAccountsId Chart { get; }

        public Fx()
        {
            Chart = FL.ChartOfAccountsId.NewId();
            var payable = FL.GLAccount.Create(
                id: new FL.GLAccountId(PayableAccountId),
                chartId: Chart,
                code: "2200",
                name: "Tax payable",
                type: FL.GLAccountType.Liability,
                subtype: FL.AccountSubtype.TaxesPayable,
                currency: "USD");
            Accounts.Upsert(payable);
            Rates = new InMemoryTaxRateLookup(Accounts);
            Sut = new Pass2TaxUpserter(Codes, Rates, Jurisdictions, Accounts);
        }
    }

    private static ErpnextTaxTemplateSource Source(
        string name,
        string modified = "2026-01-01 00:00:00",
        bool disabled = false,
        string taxName = "VA Sales Tax",
        string? taxCategory = "Virginia",
        params (string AccountHead, decimal Rate, bool Inclusive)[] rates)
    {
        var rows = (rates.Length == 0
                ? new[] { (PayableAccountId, 5m, false) }
                : rates)
            .Select(r => new ErpnextTaxTemplateRateRow(r.Item1, r.Item2, r.Item3))
            .ToList();
        return new ErpnextTaxTemplateSource(name, modified, taxName, taxCategory, rows, disabled);
    }

    // ── Upsert happy paths ────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_NewSource_InsertsTaxCodeAndRate()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(Source("VA-Sales-001"), fx.Chart);

        var inserted = Assert.IsType<TaxOutcome.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.Equal("VA Sales Tax", inserted.Record.Code);
        Assert.True(inserted.Record.IsActive);

        var rates = await fx.Rates.GetAllForTaxCodeAsync(inserted.Record.Id);
        Assert.Single(rates);
        Assert.Equal(5m, rates[0].RatePercent);
        Assert.Equal(new DateOnly(2000, 1, 1), rates[0].EffectiveDate);
    }

    [Fact]
    public async Task Upsert_SameModified_Skips_Idempotent()
    {
        var fx = new Fx();
        await fx.Sut.UpsertTaxTemplateAsync(Source("VA-Sales-001"), fx.Chart);

        var again = await fx.Sut.UpsertTaxTemplateAsync(Source("VA-Sales-001"), fx.Chart);
        Assert.IsType<TaxOutcome.Skipped>(again);
        Assert.Equal(ImportAction.Skipped, again.Action);
    }

    [Fact]
    public async Task Upsert_NewerModified_Updates()
    {
        var fx = new Fx();
        await fx.Sut.UpsertTaxTemplateAsync(Source("VA-Sales-001", "2026-01-01 00:00:00"), fx.Chart);

        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Sales-001", "2026-02-01 00:00:00", taxName: "VA Sales Tax v2"), fx.Chart);
        var updated = Assert.IsType<TaxOutcome.Updated>(outcome);
        Assert.Equal("VA Sales Tax v2", updated.Record.Code);
    }

    [Fact]
    public async Task Upsert_TwoRateRows_CreatesTwoTaxRates()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Sales-002", rates: new[]
            {
                (PayableAccountId, 4m, false),
                (PayableAccountId, 1m, false),
            }),
            fx.Chart);

        var rates = await fx.Rates.GetAllForTaxCodeAsync(((TaxOutcome.Inserted)outcome).Record.Id);
        Assert.Equal(2, rates.Count);
    }

    [Fact]
    public async Task Upsert_InclusiveRow_MapsToVatInclusive()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VAT-001", taxName: "VAT", rates: new[] { (PayableAccountId, 20m, true) }),
            fx.Chart);

        var inserted = Assert.IsType<TaxOutcome.Inserted>(outcome);
        Assert.Equal(TaxKind.VAT, inserted.Record.Kind);
        Assert.Equal(TaxApplication.Inclusive, inserted.Record.Application);
    }

    [Fact]
    public async Task Upsert_DisabledTrue_SetsIsActiveFalse()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(Source("VA-Sales-003", disabled: true), fx.Chart);
        Assert.False(((TaxOutcome.Inserted)outcome).Record.IsActive);
    }

    [Fact]
    public async Task Upsert_UnresolvableAccountHead_DropsRateButImportsCode()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Sales-004", rates: new[] { ("nonexistent-account", 5m, false) }),
            fx.Chart);

        var inserted = Assert.IsType<TaxOutcome.Inserted>(outcome);
        // TaxCode still imported (importer-spec §4.2: TaxCategory without rate
        // still imported, flagged in reconcile); but no rate row persisted.
        var rates = await fx.Rates.GetAllForTaxCodeAsync(inserted.Record.Id);
        Assert.Empty(rates);
    }

    // ── Jurisdiction synthesis ────────────────────────────────────────────

    [Fact]
    public async Task Upsert_SameCategoryTwice_ReusesOneJurisdiction()
    {
        var fx = new Fx();
        await fx.Sut.UpsertTaxTemplateAsync(Source("VA-A", taxName: "VA A", taxCategory: "Virginia"), fx.Chart);
        await fx.Sut.UpsertTaxTemplateAsync(Source("VA-B", taxName: "VA B", taxCategory: "Virginia"), fx.Chart);

        var jurisdictions = await fx.Jurisdictions.GetByLevelAsync(JurisdictionLevel.State);
        Assert.Single(jurisdictions, j => j.Name == "Virginia");
    }

    [Fact]
    public async Task Upsert_NoCategory_MintsPlaceholderJurisdiction()
    {
        var fx = new Fx();
        await fx.Sut.UpsertTaxTemplateAsync(Source("NC-1", taxName: "No Cat", taxCategory: null), fx.Chart);

        var jurisdictions = await fx.Jurisdictions.GetByLevelAsync(JurisdictionLevel.State);
        Assert.Contains(jurisdictions, j => j.Name == "(uncategorized ERPNext tax)");
    }

    // ── Reject path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_MissingTaxName_Rejected_MissingRequiredField()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(Source("BAD-1", taxName: "  "), fx.Chart);

        var rejected = Assert.IsType<TaxOutcome.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Null(outcome.Action);
        Assert.Equal("BAD-1", rejected.Failure.ExternalRef);
        Assert.Equal(nameof(ImportRejectReason.MissingRequiredField), rejected.Failure.ReasonCode);
        Assert.Equal("tax_name", rejected.Failure.FieldName);
    }

    // ── Census conservation ───────────────────────────────────────────────

    [Fact]
    public async Task Census_TaxCodes_Conserved()
    {
        var fx = new Fx();
        var census = new ImportCensus();

        // 4 source records: 2 inserts, 1 re-import (skip), 1 invalid (reject).
        census.Record(await fx.Sut.UpsertTaxTemplateAsync(Source("T-1", taxName: "Tax 1"), fx.Chart));    // Inserted
        census.Record(await fx.Sut.UpsertTaxTemplateAsync(Source("T-2", taxName: "Tax 2"), fx.Chart));    // Inserted
        census.Record(await fx.Sut.UpsertTaxTemplateAsync(Source("T-1", taxName: "Tax 1"), fx.Chart));    // Skipped
        census.Record(await fx.Sut.UpsertTaxTemplateAsync(Source("T-3", taxName: "  "), fx.Chart));       // Rejected

        Assert.Equal(2, census.Inserted);
        Assert.Equal(1, census.Skipped);
        Assert.Equal(1, census.Rejected);
        Assert.Equal(4, census.Accounted);
        census.AssertConserved(4);
    }
}
