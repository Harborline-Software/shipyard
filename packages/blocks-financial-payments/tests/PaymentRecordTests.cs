using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>Tests for <see cref="Payment"/> record invariants.</summary>
public class PaymentRecordTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static PartyId Party() => PartyId.NewId();

    private static Payment NewDraftInbound(decimal amount = 500m) =>
        Payment.Create(
            tenantId: Tenant(),
            chartId: Chart(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-001",
            partyId: Party(),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: amount,
            method: PaymentMethod.ACH);

    [Fact]
    public void Create_Inbound_InitializesWithCorrectDefaults()
    {
        var pmt = NewDraftInbound(300m);

        Assert.Equal(PaymentDirection.Inbound, pmt.Direction);
        Assert.Equal(PaymentStatus.Draft, pmt.Status);
        Assert.Equal(300m, pmt.Amount);
        Assert.Equal(300m, pmt.UnappliedAmount);
        Assert.Equal("USD", pmt.Currency);
        Assert.Equal(1, pmt.Version);
        Assert.Null(pmt.JournalEntryId);
        Assert.Empty(pmt.Applications);
    }

    [Fact]
    public void Create_Outbound_HasCorrectDirection()
    {
        var pmt = Payment.Create(
            tenantId: Tenant(),
            chartId: Chart(),
            direction: PaymentDirection.Outbound,
            paymentNumber: "PMT-002",
            partyId: Party(),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 1200m,
            method: PaymentMethod.Wire);

        Assert.Equal(PaymentDirection.Outbound, pmt.Direction);
        Assert.Equal(1200m, pmt.UnappliedAmount);
    }

    [Fact]
    public void UnappliedAmount_EqualsAmount_WhenNoApplications()
    {
        var pmt = NewDraftInbound(750m);

        // UnappliedAmount invariant: Amount - sum(Applications) = UnappliedAmount
        var sumApplied = pmt.Applications.Sum(a => a.AmountApplied);
        Assert.Equal(pmt.Amount - sumApplied, pmt.UnappliedAmount);
    }

    [Fact]
    public void UnappliedAmount_Invariant_HoldsAfterWith()
    {
        // Simulate what the application service does when recording a partial application.
        var pmt = NewDraftInbound(1000m);
        var app = PaymentApplication.Create(
            tenantId: pmt.TenantId,
            paymentId: pmt.Id,
            appliedTo: AppliedTo.Invoice,
            targetId: "invoice-abc",
            amountApplied: 400m,
            appliedDate: new DateOnly(2026, 5, 18));

        // Service would produce this record:
        var updated = pmt with
        {
            Applications = [app],
            UnappliedAmount = pmt.Amount - app.AmountApplied,
            Status = PaymentStatus.PartiallyApplied,
        };

        var sumApplied = updated.Applications.Sum(a => a.AmountApplied);
        Assert.Equal(updated.Amount - sumApplied, updated.UnappliedAmount);
        Assert.True(updated.UnappliedAmount >= 0m);
        Assert.Equal(PaymentStatus.PartiallyApplied, updated.Status);
    }

    [Fact]
    public void UnappliedAmount_NeverNegative_FullApplication()
    {
        var pmt = NewDraftInbound(500m);
        var app = PaymentApplication.Create(
            tenantId: pmt.TenantId,
            paymentId: pmt.Id,
            appliedTo: AppliedTo.Invoice,
            targetId: "invoice-xyz",
            amountApplied: 500m,
            appliedDate: new DateOnly(2026, 5, 18));

        var updated = pmt with
        {
            Applications = [app],
            UnappliedAmount = 0m,
            Status = PaymentStatus.Applied,
        };

        Assert.Equal(0m, updated.UnappliedAmount);
        Assert.True(updated.UnappliedAmount >= 0m);
        Assert.Equal(PaymentStatus.Applied, updated.Status);
    }

    [Fact]
    public void IMustHaveTenant_IsImplemented()
    {
        var pmt = NewDraftInbound();
        Assert.IsAssignableFrom<Sunfish.Foundation.MultiTenancy.IMustHaveTenant>(pmt);
    }

    [Fact]
    public void StatusExtensions_IsActive_Draft()
    {
        Assert.True(PaymentStatus.Draft.IsActive());
        Assert.False(PaymentStatus.Draft.IsTerminal());
    }

    [Fact]
    public void StatusExtensions_IsTerminal_Bounced()
    {
        Assert.True(PaymentStatus.Bounced.IsTerminal());
        Assert.False(PaymentStatus.Bounced.IsActive());
    }

    [Fact]
    public void StatusExtensions_IsTerminal_Voided()
    {
        Assert.True(PaymentStatus.Voided.IsTerminal());
        Assert.False(PaymentStatus.Voided.IsActive());
    }
}
