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

    /// <summary>A Liability account whose subtype is NOT TaxesPayable — exercises
    /// the structured <see cref="TaxRateValidationError.PayableAccountWrongSubtype"/>
    /// reject that the fan-out must count (Amendment 1) rather than swallow.</summary>
    private const string WrongSubtypeAccountId = "erp-account-ap-not-tax";

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

            // A Liability account whose subtype is wrong for a tax rate. The
            // service-layer UpsertAsync returns a structured PayableAccountWrongSubtype
            // reject (non-throwing) — the fan-out must count it, not swallow it.
            var wrongSubtype = FL.GLAccount.Create(
                id: new FL.GLAccountId(WrongSubtypeAccountId),
                chartId: Chart,
                code: "2100",
                name: "Accounts payable",
                type: FL.GLAccountType.Liability,
                subtype: FL.AccountSubtype.AccountsPayable,
                currency: "USD");
            Accounts.Upsert(wrongSubtype);

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

    [Fact]
    public async Task Upsert_MultiRow_ComponentJurisdiction_IsFlatNotCountyHierarchy()
    {
        // Fidelity guard: ERPNext multi-row taxes are stacked charge components
        // on the SAME scope, not a jurisdictional breakdown. The synthetic
        // per-row placeholder must be a FLAT node at the category's own level
        // (no fabricated County hierarchy, no parent FK) — matching the legacy
        // ErpnextTaxImporter precedent + keeping the "reconcile later" story honest.
        var fx = new Fx();
        await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Multi-1", taxName: "VA Combined", taxCategory: "Virginia", rates: new[]
            {
                (PayableAccountId, 4m, false),
                (PayableAccountId, 1m, false),
            }),
            fx.Chart);

        // Both the category node and the component node are State-level.
        var states = await fx.Jurisdictions.GetByLevelAsync(JurisdictionLevel.State);
        Assert.Equal(2, states.Count);
        var component = Assert.Single(states, j => j.Name == "Virginia (component 2)");
        Assert.Equal(JurisdictionLevel.State, component.Level);
        Assert.Null(component.ParentJurisdictionId); // flat, not parented under a County
        // No County-level invention.
        var counties = await fx.Jurisdictions.GetByLevelAsync(JurisdictionLevel.County);
        Assert.Empty(counties);
    }

    // ── Idempotency of the rate + jurisdiction fan-out (Amendment 2) ──────────

    [Fact]
    public async Task Upsert_MultiRowReImportedAtNewerModified_DoesNotDuplicateRatesOrJurisdictions()
    {
        // Re-import a 2-row template at an advancing `modified`. The TaxCode
        // takes the Updated path (which re-runs the fan-out); rate count +
        // synthetic-jurisdiction count must BOTH stay at 2 (ADR 0100 idempotency
        // — re-run never duplicate-inserts child rows).
        var fx = new Fx();
        var rates = new[]
        {
            (PayableAccountId, 4m, false),
            (PayableAccountId, 1m, false),
        };

        var first = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Multi-2", modified: "2026-01-01 00:00:00", taxName: "VA Combined", taxCategory: "Virginia", rates: rates),
            fx.Chart);
        var codeId = ((TaxOutcome.Inserted)first).Record.Id;

        var second = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Multi-2", modified: "2026-02-01 00:00:00", taxName: "VA Combined", taxCategory: "Virginia", rates: rates),
            fx.Chart);
        Assert.IsType<TaxOutcome.Updated>(second);

        var allRates = await fx.Rates.GetAllForTaxCodeAsync(codeId);
        Assert.Equal(2, allRates.Count); // not 4
        var states = await fx.Jurisdictions.GetByLevelAsync(JurisdictionLevel.State);
        Assert.Equal(2, states.Count);  // category + one component, not 3
    }

    // ── Silent-drop guard (Amendment 1): drops are counted in the detail ──────

    [Fact]
    public async Task Upsert_ZeroRateRow_IsDroppedAndCountedInDetail()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-Zero-1", taxName: "VA Zero", rates: new[]
            {
                (PayableAccountId, 5m, false),
                (PayableAccountId, 0m, false), // ERPNext informational row
            }),
            fx.Chart);

        var inserted = Assert.IsType<TaxOutcome.Inserted>(outcome);
        var persisted = await fx.Rates.GetAllForTaxCodeAsync(inserted.Record.Id);
        Assert.Single(persisted); // only the 5% row
        Assert.Contains("zero-rate: 1", inserted.Detail);
        Assert.Contains("Dropped 1 rate row", inserted.Detail);
    }

    [Fact]
    public async Task Upsert_WrongSubtypePayableAccount_RateDroppedButCountedNotSwallowed()
    {
        // The fan-out previously discarded ITaxRateLookup.UpsertAsync's result,
        // silently swallowing a wrong-subtype payable account (a C2/C5 silent
        // drop invisible to the TaxCode-granularity census). The TaxCode still
        // imports; the dropped rate MUST leave a count in the detail.
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-WrongSub-1", taxName: "VA WrongSub", rates: new[]
            {
                (WrongSubtypeAccountId, 5m, false),
            }),
            fx.Chart);

        var inserted = Assert.IsType<TaxOutcome.Inserted>(outcome);
        var persisted = await fx.Rates.GetAllForTaxCodeAsync(inserted.Record.Id);
        Assert.Empty(persisted); // rate rejected by the service-layer validator
        Assert.Contains("validation-reject: 1", inserted.Detail);
        Assert.Contains("Dropped 1 rate row", inserted.Detail);
    }

    [Fact]
    public async Task Upsert_UnresolvableAccount_DropIsCountedInDetail()
    {
        var fx = new Fx();
        var outcome = await fx.Sut.UpsertTaxTemplateAsync(
            Source("VA-NoAcct-1", taxName: "VA NoAcct", rates: new[] { ("nonexistent-account", 5m, false) }),
            fx.Chart);

        var inserted = Assert.IsType<TaxOutcome.Inserted>(outcome);
        Assert.Contains("unresolved-account: 1", inserted.Detail);
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
