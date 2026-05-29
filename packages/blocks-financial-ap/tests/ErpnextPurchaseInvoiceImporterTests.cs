using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class ErpnextPurchaseInvoiceImporterTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Account() => GLAccountId.NewId();
    private static PartyId Vendor() => PartyId.NewId();

    private sealed record Sut(ErpnextPurchaseInvoiceImporter Importer, InMemoryBillRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryBillRepository();
        return new Sut(new ErpnextPurchaseInvoiceImporter(repo), repo);
    }

    private static ErpnextPurchaseInvoiceSource NewSource(
        string name = "PINV-0001",
        string modified = "2026-05-17 12:00:00",
        string status = "Submitted",
        decimal grandTotal = 1000m,
        decimal outstanding = 1000m,
        string? billNo = "VND-INV-001",
        params ErpnextPurchaseInvoiceItem[] items) =>
        new(
            Name: name,
            Modified: modified,
            Supplier: "SUP-0001",
            BillNo: billNo,
            PostingDate: new DateOnly(2026, 5, 17),
            DueDate: new DateOnly(2026, 6, 17),
            BillDate: new DateOnly(2026, 5, 16),
            Currency: "USD",
            Items: items.Length > 0 ? items : new[]
            {
                new ErpnextPurchaseInvoiceItem("Consulting hours", 10m, 100m, 1000m),
            },
            Status: status,
            GrandTotal: grandTotal,
            OutstandingAmount: outstanding);

    // ── Insert path ───────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPurchaseInvoice_FreshSource_InsertsCanonicalBill()
    {
        var sut = NewSut();
        var source = NewSource();

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.NotNull(inserted.Record);
        Assert.Equal(BillStatus.Received, inserted.Record.Status);
        Assert.Equal(1000m, inserted.Record.Total);
        Assert.Equal(1000m, inserted.Record.Balance);
        Assert.Equal("erpnext:pinv:PINV-0001", inserted.Record.ExternalRef);
        // C7: the source Modified stamp lives in ExternalRefVersion, not Notes.
        Assert.Equal("2026-05-17 12:00:00", inserted.Record.ExternalRefVersion);
        Assert.Null(inserted.Record.Notes);
        Assert.Equal("VND-INV-001", inserted.Record.BillNumber);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_BlankBillNo_FallsBackToErpnextName()
    {
        var sut = NewSut();
        var source = NewSource(billNo: "");

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal("PINV-0001", inserted.Record.BillNumber);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_DraftSource_BillNumberKept()
    {
        var sut = NewSut();
        var source = NewSource(status: "Draft");

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal(BillStatus.Draft, inserted.Record.Status);
        Assert.Equal("VND-INV-001", inserted.Record.BillNumber);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_MultipleLines_PreserveOrder()
    {
        var sut = NewSut();
        var source = NewSource(
            grandTotal: 1300m, outstanding: 1300m,
            items: new[]
            {
                new ErpnextPurchaseInvoiceItem("Item A", 2m, 500m, 1000m),
                new ErpnextPurchaseInvoiceItem("Item B", 3m, 100m, 300m),
            });

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal(2, inserted.Record.Lines.Count);
        Assert.Equal(1000m, inserted.Record.Lines[0].Amount);
        Assert.Equal(300m, inserted.Record.Lines[1].Amount);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_OutstandingLessThanGrandTotal_DerivesAmountPaidAndBalance()
    {
        var sut = NewSut();
        var source = NewSource(grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal(BillStatus.PartiallyPaid, inserted.Record.Status);
        Assert.Equal(600m, inserted.Record.AmountPaid);
        Assert.Equal(400m, inserted.Record.Balance);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NullExpenseAccount_FallsBackToDefault()
    {
        var sut = NewSut();
        var defaultExpense = Account();
        var source = NewSource(
            items: new[] { new ErpnextPurchaseInvoiceItem("Hours", 1m, 500m, 500m, ExpenseAccount: null) },
            grandTotal: 500m, outstanding: 500m);

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), defaultExpense);

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal(defaultExpense, inserted.Record.Lines[0].DebitAccountId);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NullBillDate_FallsBackToPostingDate()
    {
        var sut = NewSut();
        var source = NewSource() with { BillDate = null };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal(source.PostingDate, inserted.Record.BillDate);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NullCurrency_DefaultsToUsd()
    {
        var sut = NewSut();
        var source = NewSource() with { Currency = null };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var inserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(outcome);
        Assert.Equal("USD", inserted.Record.Currency);
    }

    // ── Skipped / Updated paths ───────────────────────────────────────

    [Fact]
    public async Task UpsertPurchaseInvoice_SameModifiedKey_ReturnsSkipped()
    {
        var sut = NewSut();
        var chart = Chart();
        var vendor = Vendor();
        var ap = Account();
        var expense = Account();
        var source = NewSource();

        var first = await sut.Importer.UpsertPurchaseInvoiceAsync(Tenant(), source, chart, vendor, ap, expense);
        var second = await sut.Importer.UpsertPurchaseInvoiceAsync(Tenant(), source, chart, vendor, ap, expense);

        var firstInserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(first);
        var secondSkipped = Assert.IsType<ImportOutcome<Bill>.Skipped>(second);
        Assert.Equal(ImportAction.Inserted, first.Action);
        Assert.Equal(ImportAction.Skipped, second.Action);
        Assert.Equal(firstInserted.Record.Id, secondSkipped.Record.Id);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_AdvancedModifiedKey_ReturnsUpdated()
    {
        var sut = NewSut();
        var chart = Chart();
        var vendor = Vendor();
        var ap = Account();
        var expense = Account();
        var v1 = NewSource(modified: "2026-05-17 12:00:00", grandTotal: 1000m, outstanding: 1000m);
        var v2 = NewSource(modified: "2026-05-18 09:00:00", grandTotal: 1000m, outstanding: 400m, status: "Partly Paid");

        var first = await sut.Importer.UpsertPurchaseInvoiceAsync(Tenant(), v1, chart, vendor, ap, expense);
        var second = await sut.Importer.UpsertPurchaseInvoiceAsync(Tenant(), v2, chart, vendor, ap, expense);

        var firstInserted = Assert.IsType<ImportOutcome<Bill>.Inserted>(first);
        var secondUpdated = Assert.IsType<ImportOutcome<Bill>.Updated>(second);
        Assert.Equal(ImportAction.Updated, second.Action);
        Assert.Equal(firstInserted.Record.Id, secondUpdated.Record.Id);
        Assert.Equal(BillStatus.PartiallyPaid, secondUpdated.Record.Status);
        Assert.Equal(600m, secondUpdated.Record.AmountPaid);
        Assert.Equal(2L, secondUpdated.Record.Version);
        // C7: the version stamp advanced on the dedicated companion field.
        Assert.Equal("2026-05-18 09:00:00", secondUpdated.Record.ExternalRefVersion);
    }

    // ── Rejected paths (ADR 0100 C2/OQ-A — missing-required-field) ────

    [Fact]
    public async Task UpsertPurchaseInvoice_EmptyName_ReturnsRejected()
    {
        var sut = NewSut();
        var source = NewSource() with { Name = "" };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var rejected = Assert.IsType<ImportOutcome<Bill>.Rejected>(outcome);
        Assert.Null(outcome.Action);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal(ErpnextPurchaseInvoiceImporter.DocType, rejected.Failure.DocType);
        Assert.Equal("name", rejected.Failure.FieldName);
        Assert.Equal("(unknown)", rejected.Failure.ExternalRef);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_EmptySupplier_ReturnsRejected()
    {
        var sut = NewSut();
        var source = NewSource() with { Supplier = "" };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var rejected = Assert.IsType<ImportOutcome<Bill>.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("supplier", rejected.Failure.FieldName);
        Assert.Equal("PINV-0001", rejected.Failure.ExternalRef);
    }

    [Fact]
    public async Task UpsertPurchaseInvoice_NoItems_ReturnsRejected()
    {
        var sut = NewSut();
        var source = NewSource() with { Items = Array.Empty<ErpnextPurchaseInvoiceItem>() };

        var outcome = await sut.Importer.UpsertPurchaseInvoiceAsync(
            Tenant(), source, Chart(), Vendor(), Account(), Account());

        var rejected = Assert.IsType<ImportOutcome<Bill>.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("items", rejected.Failure.FieldName);
    }

    // ── Status mapping ────────────────────────────────────────────────

    [Theory]
    [InlineData("Draft", BillStatus.Draft)]
    [InlineData("", BillStatus.Draft)]
    [InlineData(null, BillStatus.Draft)]
    [InlineData("Submitted", BillStatus.Received)]
    [InlineData("Overdue", BillStatus.Received)]
    [InlineData("Return", BillStatus.Received)]
    [InlineData("Debit Note Issued", BillStatus.Received)]
    [InlineData("Partly Paid", BillStatus.PartiallyPaid)]
    [InlineData("Partly Paid and Discounted", BillStatus.PartiallyPaid)]
    [InlineData("Paid", BillStatus.Paid)]
    [InlineData("Paid and Discounted", BillStatus.Paid)]
    [InlineData("Cancelled", BillStatus.Voided)]
    [InlineData("WhateverFuture", BillStatus.Draft)] // unknown → safest non-posting state
    public void MapStatus_HandlesAllKnownAndUnknownCodes(string? erpnext, BillStatus expected)
    {
        Assert.Equal(expected, ErpnextPurchaseInvoiceImporter.MapStatus(erpnext));
    }
}
