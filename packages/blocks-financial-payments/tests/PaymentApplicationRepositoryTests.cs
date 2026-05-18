using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>
/// Round-trip tests for <see cref="InMemoryPaymentApplicationRepository"/>.
/// Covers Add / Get / Delete / List and the boundary that balance updates
/// are NOT performed by the repository.
/// </summary>
public class PaymentApplicationRepositoryTests
{
    private static PaymentApplication NewApp(
        PaymentId? paymentId = null,
        string? targetId = null,
        AppliedTo appliedTo = AppliedTo.Invoice,
        decimal amountApplied = 100m) =>
        PaymentApplication.Create(
            paymentId: paymentId ?? PaymentId.NewId(),
            appliedTo: appliedTo,
            targetId: targetId ?? "invoice-" + Guid.NewGuid(),
            amountApplied: amountApplied,
            appliedDate: new DateOnly(2026, 5, 18));

    [Fact]
    public async Task AddAndGet_RoundTrip()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp();

        await repo.AddAsync(app);
        var result = await repo.GetAsync(app.Id);

        Assert.NotNull(result);
        Assert.Equal(app.Id, result!.Id);
        Assert.Equal(app.AmountApplied, result.AmountApplied);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNotFound()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var result = await repo.GetAsync(PaymentApplicationId.NewId());
        Assert.Null(result);
    }

    [Fact]
    public async Task Add_ThrowsOnDuplicateId()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp();

        await repo.AddAsync(app);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddAsync(app));
    }

    [Fact]
    public async Task Delete_RemovesRecord_ReturnsTrue()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp();
        await repo.AddAsync(app);

        var deleted = await repo.DeleteAsync(app.Id);

        Assert.True(deleted);
        Assert.Null(await repo.GetAsync(app.Id));
    }

    [Fact]
    public async Task Delete_IdempotentOnMissingId_ReturnsFalse()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var result = await repo.DeleteAsync(PaymentApplicationId.NewId());
        Assert.False(result);
    }

    [Fact]
    public async Task ListByPayment_ReturnsOnlyMatchingPayment()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var payA = PaymentId.NewId();
        var payB = PaymentId.NewId();

        await repo.AddAsync(NewApp(paymentId: payA));
        await repo.AddAsync(NewApp(paymentId: payA));
        await repo.AddAsync(NewApp(paymentId: payB));

        var results = await repo.ListByPaymentAsync(payA);
        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal(payA, a.PaymentId));
    }

    [Fact]
    public async Task ListByTarget_ReturnsOnlyMatchingTarget()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        const string targetId = "invoice-target-1";

        await repo.AddAsync(NewApp(targetId: targetId));
        await repo.AddAsync(NewApp(targetId: targetId));
        await repo.AddAsync(NewApp(targetId: "invoice-other"));

        var results = await repo.ListByTargetAsync(targetId);
        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal(targetId, a.TargetId));
    }

    [Fact]
    public async Task Repository_DoesNotUpdatePaymentBalance()
    {
        // Confirm the repository is a pure store: adding an application
        // does NOT alter any Payment record (balance updates belong to
        // IPaymentApplicationService in PR 3).
        var payRepo = new InMemoryPaymentRepository();
        var appRepo = new InMemoryPaymentApplicationRepository();

        var pmt = Payment.Create(
            tenantId: new("acme"),
            chartId: ChartOfAccountsId.NewId(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-001",
            partyId: new Sunfish.Blocks.People.Foundation.Models.PartyId("cust-1"),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 500m,
            method: PaymentMethod.ACH);

        await payRepo.AddAsync(pmt);
        var app = NewApp(paymentId: pmt.Id, amountApplied: 200m);
        await appRepo.AddAsync(app);

        // Payment in payRepo is UNCHANGED — 500m UnappliedAmount still 500m
        var reloaded = await payRepo.GetAsync(pmt.Id);
        Assert.Equal(500m, reloaded!.UnappliedAmount);
    }
}
