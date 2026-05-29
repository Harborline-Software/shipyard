using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Migration;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>
/// Tests for <see cref="ErpnextPaymentImporter"/> — the A4.3 per-record ERPNext
/// Payment Entry upserter. Asserts the foundation-import <see cref="ImportOutcome{T}"/>
/// DU contract (ADR 0100 C2/OQ-A), C1/C7 idempotency, tenant-first threading
/// (C3/D1), and the allowlisted reject shape (C9).
/// </summary>
public class ErpnextPaymentImporterTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static PartyId Party() => PartyId.NewId();

    private sealed record Sut(ErpnextPaymentImporter Importer, InMemoryPaymentRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryPaymentRepository();
        return new Sut(new ErpnextPaymentImporter(repo), repo);
    }

    private static ErpnextPaymentSource NewSource(
        string name = "PE-0001",
        string modified = "2026-05-17 12:00:00",
        string paymentType = "Receive",
        string? modeOfPayment = "Cash",
        decimal paidAmount = 1000m,
        decimal unallocated = 1000m,
        string? currency = "USD",
        string? referenceNo = "CHK-1001") =>
        new(
            Name: name,
            Modified: modified,
            PaymentType: paymentType,
            ModeOfPayment: modeOfPayment,
            Party: "CUST-0001",
            PostingDate: new DateOnly(2026, 5, 17),
            PaidAmount: paidAmount,
            UnallocatedAmount: unallocated,
            Currency: currency,
            ReferenceNo: referenceNo);

    // ── Insert path ───────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_FreshReceive_InsertsInboundPayment()
    {
        var sut = NewSut();
        var party = Party();

        var outcome = await sut.Importer.UpsertPaymentAsync(Tenant(), NewSource(), Chart(), party);

        var inserted = Assert.IsType<ImportOutcome<Payment>.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.False(outcome.IsRejected);
        Assert.Equal(PaymentDirection.Inbound, inserted.Record.Direction);
        Assert.Equal(party, inserted.Record.PartyId);
        Assert.Equal(1000m, inserted.Record.Amount);
        Assert.Equal(1000m, inserted.Record.UnappliedAmount);
        Assert.Equal(PaymentMethod.Cash, inserted.Record.Method);
        Assert.Equal("erpnext:pe:PE-0001", inserted.Record.ExternalRef);
        Assert.Equal("2026-05-17 12:00:00", inserted.Record.ExternalRefVersion);
        Assert.Equal("CHK-1001", inserted.Record.Reference);
        Assert.Equal("PE-0001", inserted.Record.PaymentNumber);
        // C7: version stamp lives in the indexed field, never in Notes.
        Assert.Null(inserted.Record.Notes);
    }

    [Fact]
    public async Task Upsert_FreshPay_InsertsOutboundPayment()
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(paymentType: "Pay"), Chart(), Party());

        var inserted = Assert.IsType<ImportOutcome<Payment>.Inserted>(outcome);
        Assert.Equal(PaymentDirection.Outbound, inserted.Record.Direction);
    }

    [Fact]
    public async Task Upsert_PartiallyAllocated_CarriesUnallocatedAsUnapplied()
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(paidAmount: 1000m, unallocated: 400m), Chart(), Party());

        var inserted = Assert.IsType<ImportOutcome<Payment>.Inserted>(outcome);
        Assert.Equal(1000m, inserted.Record.Amount);
        Assert.Equal(400m, inserted.Record.UnappliedAmount);
    }

    [Theory]
    [InlineData("Cash", PaymentMethod.Cash)]
    [InlineData("Cheque", PaymentMethod.Check)]
    [InlineData("Check", PaymentMethod.Check)]
    [InlineData("ACH", PaymentMethod.ACH)]
    [InlineData("Wire Transfer", PaymentMethod.Wire)]
    [InlineData("Credit Card", PaymentMethod.Card)]
    [InlineData("Bitcoin", PaymentMethod.Other)]
    [InlineData(null, PaymentMethod.Other)]
    public async Task Upsert_MapsModeOfPayment(string? mode, PaymentMethod expected)
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(modeOfPayment: mode), Chart(), Party());

        var inserted = Assert.IsType<ImportOutcome<Payment>.Inserted>(outcome);
        Assert.Equal(expected, inserted.Record.Method);
    }

    [Fact]
    public async Task Upsert_NullCurrency_DefaultsToUsdAndInserts()
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(currency: null), Chart(), Party());

        var inserted = Assert.IsType<ImportOutcome<Payment>.Inserted>(outcome);
        Assert.Equal("USD", inserted.Record.Currency);
    }

    // ── Idempotency / version gate (C1/C7) ─────────────────────────────

    [Fact]
    public async Task Upsert_SameSourceTwice_SecondIsSkipped_CountStable()
    {
        var sut = NewSut();
        var chart = Chart();
        var party = Party();
        var source = NewSource();

        var first = await sut.Importer.UpsertPaymentAsync(Tenant(), source, chart, party);
        Assert.IsType<ImportOutcome<Payment>.Inserted>(first);

        var second = await sut.Importer.UpsertPaymentAsync(Tenant(), source, chart, party);
        var skipped = Assert.IsType<ImportOutcome<Payment>.Skipped>(second);
        Assert.Equal(ImportAction.Skipped, second.Action);
        Assert.NotNull(skipped.Record);

        // Count-stable: exactly one payment exists.
        var all = await sut.Repo.ListByChartAsync(Tenant(), chart);
        Assert.Single(all);
    }

    [Fact]
    public async Task Upsert_OlderSourceVersion_IsSkipped()
    {
        var sut = NewSut();
        var chart = Chart();
        var party = Party();

        await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(modified: "2026-05-17 12:00:00"), chart, party);

        var older = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(modified: "2026-05-16 09:00:00"), chart, party);

        Assert.IsType<ImportOutcome<Payment>.Skipped>(older);
    }

    [Fact]
    public async Task Upsert_NewerSourceVersion_IsUpdated()
    {
        var sut = NewSut();
        var chart = Chart();
        var party = Party();

        await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(modified: "2026-05-17 12:00:00", paidAmount: 1000m, unallocated: 1000m),
            chart, party);

        var newer = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(modified: "2026-05-18 08:00:00", paidAmount: 1500m, unallocated: 1500m),
            chart, party);

        var updated = Assert.IsType<ImportOutcome<Payment>.Updated>(newer);
        Assert.Equal(ImportAction.Updated, newer.Action);
        Assert.Equal(1500m, updated.Record.Amount);
        Assert.Equal(1500m, updated.Record.UnappliedAmount);
        Assert.Equal("2026-05-18 08:00:00", updated.Record.ExternalRefVersion);
        Assert.True(updated.Record.Version > 1);

        // Still exactly one payment after the update.
        var all = await sut.Repo.ListByChartAsync(Tenant(), chart);
        Assert.Single(all);
    }

    // ── Reject arm (C2/C9) — structured, allowlisted ───────────────────

    [Fact]
    public async Task Upsert_UnknownPaymentType_RejectsInvalidFieldValue()
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(paymentType: "Internal Transfer"), Chart(), Party());

        var rejected = Assert.IsType<ImportOutcome<Payment>.Rejected>(outcome);
        Assert.Null(outcome.Action);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.InvalidFieldValue.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("payment_type", rejected.Failure.FieldName);
        Assert.Equal("Payment Entry", rejected.Failure.DocType);
        Assert.Equal("PE-0001", rejected.Failure.ExternalRef);
    }

    [Fact]
    public async Task Upsert_NonUsdCurrency_RejectsUnsupportedCurrency()
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(currency: "EUR"), Chart(), Party());

        var rejected = Assert.IsType<ImportOutcome<Payment>.Rejected>(outcome);
        Assert.Equal(ImportRejectReason.UnsupportedCurrency.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("currency", rejected.Failure.FieldName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Upsert_NonPositiveAmount_RejectsInvalidFieldValue(int amount)
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(paidAmount: amount, unallocated: 0m), Chart(), Party());

        var rejected = Assert.IsType<ImportOutcome<Payment>.Rejected>(outcome);
        Assert.Equal(ImportRejectReason.InvalidFieldValue.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("paid_amount", rejected.Failure.FieldName);
    }

    [Theory]
    [InlineData(1000, -1)]
    [InlineData(1000, 1500)]
    public async Task Upsert_UnallocatedOutOfRange_RejectsConstraintViolation(int paid, int unallocated)
    {
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(paidAmount: paid, unallocated: unallocated), Chart(), Party());

        var rejected = Assert.IsType<ImportOutcome<Payment>.Rejected>(outcome);
        Assert.Equal(ImportRejectReason.ConstraintViolation.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("unallocated_amount", rejected.Failure.FieldName);
    }

    [Fact]
    public async Task Reject_FailureCarriesNoMonetaryOrPartyValues()
    {
        // C9: the reject projection carries only allowlisted scalar identifiers.
        var sut = NewSut();

        var outcome = await sut.Importer.UpsertPaymentAsync(
            Tenant(), NewSource(name: "PE-9999", currency: "GBP", paidAmount: 7777m), Chart(), Party());

        var rejected = Assert.IsType<ImportOutcome<Payment>.Rejected>(outcome);
        var failure = rejected.Failure;

        // Only the opaque name, doctype, reason, field name, and rule descriptor.
        Assert.Equal("PE-9999", failure.ExternalRef);
        Assert.DoesNotContain("7777", failure.RuleViolated ?? string.Empty);
        Assert.DoesNotContain("CUST", failure.RuleViolated ?? string.Empty);
        Assert.DoesNotContain("GBP", failure.RuleViolated ?? string.Empty);
    }

    // ── Tenant scope (C3/D1) ───────────────────────────────────────────

    [Fact]
    public async Task Upsert_ThreadsTenantIntoPayment()
    {
        var sut = NewSut();
        var tenant = new TenantId("beta-corp");

        var outcome = await sut.Importer.UpsertPaymentAsync(tenant, NewSource(), Chart(), Party());

        var inserted = Assert.IsType<ImportOutcome<Payment>.Inserted>(outcome);
        Assert.Equal(tenant, inserted.Record.TenantId);
    }

    [Fact]
    public async Task Upsert_DifferentTenants_DoNotCollideOnSameExternalRef()
    {
        var sut = NewSut();
        var chart = Chart();
        var source = NewSource();

        var a = await sut.Importer.UpsertPaymentAsync(new TenantId("acme"), source, chart, Party());
        var b = await sut.Importer.UpsertPaymentAsync(new TenantId("beta"), source, chart, Party());

        // Each tenant gets its own insert — no cross-tenant idempotency collision.
        Assert.IsType<ImportOutcome<Payment>.Inserted>(a);
        Assert.IsType<ImportOutcome<Payment>.Inserted>(b);
    }
}
