using Sunfish.Blocks.FinancialAr.Migration;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class ErpnextSalesInvoiceImporterTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Account() => GLAccountId.NewId();
    private static PartyId Customer() => PartyId.NewId();

    private sealed record Sut(
        ErpnextSalesInvoiceImporter Importer,
        InMemoryInvoiceRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryInvoiceRepository();
        var numbering = new InMemoryInvoiceNumberingService(new ReplicaId("CW"));
        return new Sut(new ErpnextSalesInvoiceImporter(repo, numbering), repo);
    }

    private static ErpnextSalesInvoiceSource NewSource(
        string name = "SINV-0001",
        string modified = "2026-05-17 12:00:00",
        string status = "Submitted",
        decimal grandTotal = 1000m,
        decimal outstanding = 1000m,
        params ErpnextSalesInvoiceItem[] items) =>
        new(
            Name: name,
            Modified: modified,
            Customer: "CUST-0001",
            PostingDate: new DateOnly(2026, 5, 17),
            DueDate: new DateOnly(2026, 6, 17),
            Currency: "USD",
            Items: items.Length > 0 ? items : new[]
            {
                new ErpnextSalesInvoiceItem("Consulting hours", 10m, 100m, 1000m),
            },
            Status: status,
            GrandTotal: grandTotal,
            OutstandingAmount: outstanding);

    // ── Insert path ───────────────────────────────────────────────────

    [Fact]
    public async Task UpsertSalesInvoice_FreshSource_InsertsCanonicalInvoice()
    {
        var sut = NewSut();
        var source = NewSource();

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.NotNull(inserted.Record);
        Assert.Equal(InvoiceStatus.Issued, inserted.Record.Status);
        Assert.Equal(1000m, inserted.Record.Total);
        Assert.Equal(1000m, inserted.Record.Balance);
        Assert.Equal("erpnext:sinv:SINV-0001", inserted.Record.ExternalRef);
        Assert.Contains("erpnextModified:2026-05-17 12:00:00", inserted.Record.Notes!);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NonDraft_MintsCanonicalInvoiceNumber()
    {
        var sut = NewSut();
        var source = NewSource(status: "Submitted");

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.True(InvoiceNumberFormat.IsWellFormed(inserted.Record.InvoiceNumber));
    }

    [Fact]
    public async Task UpsertSalesInvoice_DraftSource_AcceptsEmptyInvoiceNumber()
    {
        var sut = NewSut();
        var source = NewSource(status: "Draft");

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.Equal(InvoiceStatus.Draft, inserted.Record.Status);
        Assert.Equal("", inserted.Record.InvoiceNumber);
    }

    [Fact]
    public async Task UpsertSalesInvoice_MultipleLines_PreservesOrderAndAmounts()
    {
        var sut = NewSut();
        var source = NewSource(
            grandTotal: 1300m, outstanding: 1300m,
            items: new[]
            {
                new ErpnextSalesInvoiceItem("Item A", 2m, 500m, 1000m),
                new ErpnextSalesInvoiceItem("Item B", 3m, 100m, 300m),
            });

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.Equal(2, inserted.Record.Lines.Count);
        Assert.Equal(1000m, inserted.Record.Lines[0].Amount);
        Assert.Equal(300m, inserted.Record.Lines[1].Amount);
        Assert.Equal("Item A", inserted.Record.Lines[0].Description);
        Assert.Equal(1, inserted.Record.Lines[0].LineNumber);
        Assert.Equal(2, inserted.Record.Lines[1].LineNumber);
    }

    [Fact]
    public async Task UpsertSalesInvoice_OutstandingLessThanGrandTotal_DerivesAmountPaidAndBalance()
    {
        var sut = NewSut();
        var source = NewSource(grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.Equal(InvoiceStatus.PartiallyPaid, inserted.Record.Status);
        Assert.Equal(600m, inserted.Record.AmountPaid);
        Assert.Equal(400m, inserted.Record.Balance);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NullIncomeAccountOnItem_FallsBackToDefault()
    {
        var sut = NewSut();
        var defaultIncome = Account();
        var source = NewSource(items: new[]
        {
            new ErpnextSalesInvoiceItem("Hours", 1m, 500m, 500m, IncomeAccount: null),
        }, grandTotal: 500m, outstanding: 500m);

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), defaultIncome);

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.Equal(defaultIncome, inserted.Record.Lines[0].IncomeAccountId);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NullCurrency_DefaultsToUsd()
    {
        var sut = NewSut();
        var source = NewSource() with { Currency = null };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(outcome);
        Assert.Equal("USD", inserted.Record.Currency);
    }

    // ── Skipped / Updated paths ───────────────────────────────────────

    [Fact]
    public async Task UpsertSalesInvoice_SameModifiedKey_ReturnsSkipped()
    {
        var sut = NewSut();
        var chart = Chart();
        var customer = Customer();
        var ar = Account();
        var income = Account();
        var source = NewSource();

        var first = await sut.Importer.UpsertSalesInvoiceAsync(Tenant(), source, chart, customer, ar, income);
        var second = await sut.Importer.UpsertSalesInvoiceAsync(Tenant(), source, chart, customer, ar, income);

        var firstInserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(first);
        var secondSkipped = Assert.IsType<ImportOutcome<Invoice>.Skipped>(second);
        Assert.Equal(ImportAction.Inserted, first.Action);
        Assert.Equal(ImportAction.Skipped, second.Action);
        Assert.Equal(firstInserted.Record.Id, secondSkipped.Record.Id);
    }

    [Fact]
    public async Task UpsertSalesInvoice_AdvancedModifiedKey_ReturnsUpdated_WithNewState()
    {
        var sut = NewSut();
        var chart = Chart();
        var customer = Customer();
        var ar = Account();
        var income = Account();

        var v1 = NewSource(modified: "2026-05-17 12:00:00", grandTotal: 1000m, outstanding: 1000m);
        var v2 = NewSource(modified: "2026-05-18 09:00:00", grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var first = await sut.Importer.UpsertSalesInvoiceAsync(Tenant(), v1, chart, customer, ar, income);
        var second = await sut.Importer.UpsertSalesInvoiceAsync(Tenant(), v2, chart, customer, ar, income);

        var firstInserted = Assert.IsType<ImportOutcome<Invoice>.Inserted>(first);
        var secondUpdated = Assert.IsType<ImportOutcome<Invoice>.Updated>(second);
        Assert.Equal(ImportAction.Updated, second.Action);
        Assert.Equal(firstInserted.Record.Id, secondUpdated.Record.Id); // same canonical id (stable across update)
        Assert.Equal(InvoiceStatus.PartiallyPaid, secondUpdated.Record.Status);
        Assert.Equal(600m, secondUpdated.Record.AmountPaid);
        Assert.Equal(400m, secondUpdated.Record.Balance);
        Assert.Contains("erpnextModified:2026-05-18 09:00:00", secondUpdated.Record.Notes!);
        Assert.DoesNotContain("erpnextModified:2026-05-17 12:00:00", secondUpdated.Record.Notes!);
        Assert.Equal(2L, secondUpdated.Record.Version);
    }

    // ── Rejected paths (ADR 0100 C2/OQ-A — missing-required-field) ────

    [Fact]
    public async Task UpsertSalesInvoice_EmptyName_ReturnsRejected()
    {
        var sut = NewSut();
        var source = NewSource() with { Name = "" };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var rejected = Assert.IsType<ImportOutcome<Invoice>.Rejected>(outcome);
        Assert.Null(outcome.Action);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal(ErpnextSalesInvoiceImporter.DocType, rejected.Failure.DocType);
        Assert.Equal("name", rejected.Failure.FieldName);
        // When the natural key (name) is itself empty, we cite the sentinel — never a value.
        Assert.Equal("(unknown)", rejected.Failure.ExternalRef);
    }

    [Fact]
    public async Task UpsertSalesInvoice_EmptyCustomer_ReturnsRejected()
    {
        var sut = NewSut();
        var source = NewSource() with { Customer = "" };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var rejected = Assert.IsType<ImportOutcome<Invoice>.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("customer", rejected.Failure.FieldName);
        Assert.Equal("SINV-0001", rejected.Failure.ExternalRef);
    }

    [Fact]
    public async Task UpsertSalesInvoice_NoItems_ReturnsRejected()
    {
        var sut = NewSut();
        var source = NewSource() with { Items = Array.Empty<ErpnextSalesInvoiceItem>() };

        var outcome = await sut.Importer.UpsertSalesInvoiceAsync(
            Tenant(), source, Chart(), Customer(), Account(), Account());

        var rejected = Assert.IsType<ImportOutcome<Invoice>.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("items", rejected.Failure.FieldName);
    }

    // ── Status mapping ────────────────────────────────────────────────

    [Theory]
    [InlineData("Draft", InvoiceStatus.Draft)]
    [InlineData("", InvoiceStatus.Draft)]
    [InlineData(null, InvoiceStatus.Draft)]
    [InlineData("Submitted", InvoiceStatus.Issued)]
    [InlineData("Overdue", InvoiceStatus.Issued)]
    [InlineData("Return", InvoiceStatus.Issued)]
    [InlineData("Credit Note Issued", InvoiceStatus.Issued)]
    [InlineData("Partly Paid", InvoiceStatus.PartiallyPaid)]
    [InlineData("Partly Paid and Discounted", InvoiceStatus.PartiallyPaid)]
    [InlineData("Paid", InvoiceStatus.Paid)]
    [InlineData("Paid and Discounted", InvoiceStatus.Paid)]
    [InlineData("Cancelled", InvoiceStatus.Voided)]
    [InlineData("WhateverFuture", InvoiceStatus.Draft)] // unknown → safest non-posting state
    public void MapStatus_HandlesAllKnownAndUnknownCodes(string? erpnext, InvoiceStatus expected)
    {
        Assert.Equal(expected, ErpnextSalesInvoiceImporter.MapStatus(erpnext));
    }
}
