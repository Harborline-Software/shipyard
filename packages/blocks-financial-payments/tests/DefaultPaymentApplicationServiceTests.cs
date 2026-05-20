using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Models.Events;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>
/// Unit tests for <see cref="DefaultPaymentApplicationService"/> covering the
/// Apply / Unapply state transitions and — most importantly — the
/// direction-matching invariant and the security-relevant ordering of
/// guards (mismatch returns BEFORE target existence is probed).
/// </summary>
public class DefaultPaymentApplicationServiceTests
{
    private static readonly TenantId TestTenant = new("tenant-test");
    private static readonly ChartOfAccountsId TestChart = new("chart-test");
    private static readonly GLAccountId BankAccount = new("acct-bank");
    private static readonly GLAccountId ArControl = new("acct-ar-control");
    private static readonly GLAccountId ApControl = new("acct-ap-control");
    private static readonly GLAccountId IncomeAccount = new("acct-rental-income");
    private static readonly GLAccountId ExpenseAccount = new("acct-utility-expense");
    private static readonly PartyId TestActor = new("party-actor");
    private static readonly PartyId TestCustomer = new("party-customer");
    private static readonly PartyId TestVendor = new("party-vendor");

    private sealed record TestRig(
        DefaultPaymentApplicationService Service,
        InMemoryPaymentRepository Payments,
        InMemoryPaymentApplicationRepository Applications,
        InMemoryInvoiceRepository Invoices,
        InMemoryBillRepository Bills,
        RecordingDomainEventPublisher Events,
        StubTenantContext TenantContext);

    private static TestRig CreateRig(TenantId? tenant = null)
    {
        var payments = new InMemoryPaymentRepository();
        var applications = new InMemoryPaymentApplicationRepository();
        var invoices = new InMemoryInvoiceRepository();
        var bills = new InMemoryBillRepository();
        var events = new RecordingDomainEventPublisher();
        var ctx = new StubTenantContext(tenant ?? TestTenant);
        var service = new DefaultPaymentApplicationService(payments, applications, invoices, bills, ctx, events);
        return new TestRig(service, payments, applications, invoices, bills, events, ctx);
    }

    private static int _invoiceSeq;

    private static Payment NewClearedPayment(PaymentDirection direction = PaymentDirection.Inbound, decimal amount = 1000m, string currency = "USD")
    {
        var draft = Payment.Create(
            tenantId: TestTenant,
            chartId: TestChart,
            direction: direction,
            paymentNumber: $"PMT-{Guid.NewGuid():N}",
            partyId: direction == PaymentDirection.Inbound ? TestCustomer : TestVendor,
            paymentDate: new DateOnly(2026, 5, 18),
            amount: amount,
            method: PaymentMethod.Check,
            bankAccountId: BankAccount,
            currency: currency);
        return draft with
        {
            Status = PaymentStatus.Unapplied,
            JournalEntryId = JournalEntryId.NewId(),
            UnappliedAmount = amount,
        };
    }

    private static Invoice NewIssuedInvoice(string invoiceId, decimal total, decimal amountPaid = 0m, InvoiceStatus status = InvoiceStatus.Issued, string currency = "USD")
    {
        // InMemoryInvoiceRepository enforces canonical `INV-YYYY-MM-DD-{REPLICA}-{NNNN+}`.
        var seq = Interlocked.Increment(ref _invoiceSeq);
        var invoiceNumber = $"INV-2026-05-18-T-{seq:D4}";
        var invoice = Invoice.Create(
            tenantId: TestTenant,
            chartId: TestChart,
            invoiceNumber: invoiceNumber,
            customerId: TestCustomer,
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 6, 1),
            lines:
            [
                InvoiceLine.Create(new InvoiceId(invoiceId), 1, "Rent", quantity: 1m, unitPrice: total, incomeAccountId: IncomeAccount),
            ],
            arAccountId: ArControl,
            id: new InvoiceId(invoiceId),
            currency: currency);
        return invoice with
        {
            Status = status,
            AmountPaid = amountPaid,
            Balance = total - amountPaid,
        };
    }

    private static Bill NewReceivedBill(string billId, decimal total, decimal amountPaid = 0m, BillStatus status = BillStatus.Received, string currency = "USD")
    {
        var bill = Bill.Create(
            tenantId: TestTenant,
            chartId: TestChart,
            billNumber: $"BILL-{billId}",
            vendorId: TestVendor,
            billDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 6, 1),
            lines:
            [
                BillLine.Create(new BillId(billId), 1, "Utility", quantity: 1m, unitPrice: total, debitAccountId: ExpenseAccount),
            ],
            apAccountId: ApControl,
            id: new BillId(billId),
            currency: currency);
        return bill with
        {
            Status = status,
            AmountPaid = amountPaid,
            Balance = total - amountPaid,
        };
    }

    // ──────────────────────────────────────────────────────────────────
    //  ApplyAsync — happy paths
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_Inbound_ToInvoice_UpdatesAllBalances_AndEmitsAppliedEvent()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 500m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        var invoice = NewIssuedInvoice(invoiceId, total: 500m);
        await rig.Invoices.UpsertAsync(invoice);

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 500m, discountAmount: 0m, writeoffAmount: 0m, TestActor);

        Assert.Equal(ApplyError.None, result.Error);
        Assert.NotNull(result.Application);

        // Payment fully applied.
        var updatedPayment = await rig.Payments.GetAsync(payment.Id);
        Assert.Equal(0m, updatedPayment!.UnappliedAmount);
        Assert.Equal(PaymentStatus.Applied, updatedPayment.Status);

        // Invoice fully paid.
        var updatedInvoice = await rig.Invoices.GetAsync(new InvoiceId(invoiceId));
        Assert.Equal(500m, updatedInvoice!.AmountPaid);
        Assert.Equal(0m, updatedInvoice.Balance);
        Assert.Equal(InvoiceStatus.Paid, updatedInvoice.Status);

        // Event emitted.
        var ev = Assert.Single(rig.Events.Published);
        Assert.Equal(PaymentEventNames.PaymentApplied, ev.EventType);
    }

    [Fact]
    public async Task ApplyAsync_Outbound_ToBill_UpdatesAllBalances()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Outbound, amount: 200m);
        await rig.Payments.AddAsync(payment);

        var billId = $"bill-{Guid.NewGuid():N}";
        var bill = NewReceivedBill(billId, total: 200m);
        await rig.Bills.UpsertAsync(TestTenant, bill);

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Bill, billId,
            amountApplied: 200m, discountAmount: 0m, writeoffAmount: 0m, TestActor);

        Assert.Equal(ApplyError.None, result.Error);

        var updatedBill = await rig.Bills.GetAsync(TestTenant, new BillId(billId));
        Assert.Equal(200m, updatedBill!.AmountPaid);
        Assert.Equal(BillStatus.Paid, updatedBill.Status);

        var updatedPayment = await rig.Payments.GetAsync(payment.Id);
        Assert.Equal(PaymentStatus.Applied, updatedPayment!.Status);
    }

    [Fact]
    public async Task ApplyAsync_Partial_LeavesInvoicePartiallyPaid_AndPaymentPartiallyApplied()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 1000m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 1000m));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 400m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.None, result.Error);

        var updatedInvoice = await rig.Invoices.GetAsync(new InvoiceId(invoiceId));
        Assert.Equal(InvoiceStatus.PartiallyPaid, updatedInvoice!.Status);
        Assert.Equal(600m, updatedInvoice.Balance);

        var updatedPayment = await rig.Payments.GetAsync(payment.Id);
        Assert.Equal(PaymentStatus.PartiallyApplied, updatedPayment!.Status);
        Assert.Equal(600m, updatedPayment.UnappliedAmount);
    }

    // ──────────────────────────────────────────────────────────────────
    //  ApplyAsync — direction-matching invariant (the security-critical paths)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_Inbound_ToBill_FailsWithDirectionMismatch()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 500m);
        await rig.Payments.AddAsync(payment);

        // Bill with the targeted id DOES exist — but the direction-match guard
        // must reject BEFORE Bill existence is probed.
        var billId = $"bill-{Guid.NewGuid():N}";
        await rig.Bills.UpsertAsync(TestTenant, NewReceivedBill(billId, total: 500m));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Bill, billId,
            amountApplied: 500m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.DirectionMismatch, result.Error);
        Assert.Empty(rig.Events.Published);
        Assert.Empty(await rig.Applications.ListByPaymentAsync(payment.Id));

        // Bill must be untouched.
        var bill = await rig.Bills.GetAsync(TestTenant, new BillId(billId));
        Assert.Equal(0m, bill!.AmountPaid);
    }

    [Fact]
    public async Task ApplyAsync_Outbound_ToInvoice_FailsWithDirectionMismatch()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Outbound, amount: 500m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 500m));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 500m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.DirectionMismatch, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_DirectionMismatch_DoesNotLeakTargetExistence()
    {
        // Security guarantee: an attacker probing with a wrong direction must
        // get the same DirectionMismatch error regardless of whether the
        // targeted Invoice/Bill exists.
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 100m);
        await rig.Payments.AddAsync(payment);

        // Case 1: target Bill does NOT exist.
        var nonexistentBill = $"bill-{Guid.NewGuid():N}";
        var probeNonexistent = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Bill, nonexistentBill,
            amountApplied: 100m, 0m, 0m, TestActor);

        // Case 2: target Bill exists.
        var existingBillId = $"bill-{Guid.NewGuid():N}";
        await rig.Bills.UpsertAsync(TestTenant, NewReceivedBill(existingBillId, total: 100m));
        var probeExisting = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Bill, existingBillId,
            amountApplied: 100m, 0m, 0m, TestActor);

        // Both must return DirectionMismatch — not UnknownTarget.
        Assert.Equal(ApplyError.DirectionMismatch, probeNonexistent.Error);
        Assert.Equal(ApplyError.DirectionMismatch, probeExisting.Error);
    }

    // ──────────────────────────────────────────────────────────────────
    //  ApplyAsync — guard tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_AmountExceedsUnapplied_FailsWithInsufficientUnapplied()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 100m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 200m));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 150m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.InsufficientUnapplied, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_AmountExceedsInvoiceBalance_FailsWithTargetBalanceInsufficient()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 1000m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 200m));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 300m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.TargetBalanceInsufficient, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_TerminalInvoice_FailsWithTargetTerminal()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 100m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 100m, status: InvoiceStatus.Voided));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 100m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.TargetTerminal, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_CurrencyMismatch_Fails()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 100m, currency: "EUR");
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 100m, currency: "USD"));

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 100m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.CurrencyMismatch, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_UnknownPayment_FailsWithUnknownPayment()
    {
        var rig = CreateRig();
        var result = await rig.Service.ApplyAsync(
            PaymentId.NewId(), AppliedTo.Invoice, "any-invoice",
            amountApplied: 1m, 0m, 0m, TestActor);
        Assert.Equal(ApplyError.UnknownPayment, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_UnknownTarget_FailsWithUnknownTarget()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 100m);
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, "nonexistent-invoice",
            amountApplied: 50m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.UnknownTarget, result.Error);
    }

    [Fact]
    public async Task ApplyAsync_NonZeroDiscountOrWriteoff_RejectedAsDeferred()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 200m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 200m));

        // Discount path.
        var withDiscount = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 100m, discountAmount: 10m, writeoffAmount: 0m, TestActor);
        Assert.Equal(ApplyError.TargetBalanceInsufficient, withDiscount.Error);
        Assert.Contains("discount", withDiscount.ErrorMessage);

        // Writeoff path.
        var withWriteoff = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 100m, discountAmount: 0m, writeoffAmount: 10m, TestActor);
        Assert.Equal(ApplyError.TargetBalanceInsufficient, withWriteoff.Error);
        Assert.Contains("writeoff", withWriteoff.ErrorMessage);
    }

    [Fact]
    public async Task ApplyAsync_TerminalPayment_Rejected()
    {
        var rig = CreateRig();
        var bouncedPayment = NewClearedPayment() with { Status = PaymentStatus.Bounced };
        await rig.Payments.AddAsync(bouncedPayment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 100m));

        var result = await rig.Service.ApplyAsync(
            bouncedPayment.Id, AppliedTo.Invoice, invoiceId,
            amountApplied: 50m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.InsufficientUnapplied, result.Error);
        Assert.Contains("Bounced", result.ErrorMessage);
    }

    // ──────────────────────────────────────────────────────────────────
    //  UnapplyAsync
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnapplyAsync_RoundTrip_RestoresAllBalancesAndEmitsEvent()
    {
        var rig = CreateRig();
        var payment = NewClearedPayment(PaymentDirection.Inbound, amount: 500m);
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        await rig.Invoices.UpsertAsync(NewIssuedInvoice(invoiceId, total: 500m));

        var apply = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId, 500m, 0m, 0m, TestActor);
        Assert.Equal(ApplyError.None, apply.Error);

        var applicationId = apply.Application!.Id;
        var unapply = await rig.Service.UnapplyAsync(applicationId, TestActor);

        Assert.Equal(UnapplyError.None, unapply.Error);
        Assert.True(unapply.Success);

        // Application removed.
        Assert.Null(await rig.Applications.GetAsync(applicationId));

        // Payment restored.
        var restoredPayment = await rig.Payments.GetAsync(payment.Id);
        Assert.Equal(500m, restoredPayment!.UnappliedAmount);
        Assert.Equal(PaymentStatus.Unapplied, restoredPayment.Status);

        // Invoice restored.
        var restoredInvoice = await rig.Invoices.GetAsync(new InvoiceId(invoiceId));
        Assert.Equal(0m, restoredInvoice!.AmountPaid);
        Assert.Equal(InvoiceStatus.Issued, restoredInvoice.Status);

        // Both events emitted: apply + unapply.
        Assert.Equal(2, rig.Events.Published.Count);
        Assert.Equal(PaymentEventNames.PaymentApplied, rig.Events.Published[0].EventType);
        Assert.Equal(PaymentEventNames.PaymentUnapplied, rig.Events.Published[1].EventType);
    }

    [Fact]
    public async Task UnapplyAsync_UnknownApplication_Fails()
    {
        var rig = CreateRig();
        var result = await rig.Service.UnapplyAsync(PaymentApplicationId.NewId(), TestActor);
        Assert.Equal(UnapplyError.UnknownApplication, result.Error);
        Assert.False(result.Success);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tenant-isolation tests (PR 3 amber-amendment)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_CrossTenantPayment_ReturnsUnknownPayment()
    {
        // Caller resolved to TenantA. Payment + Invoice in the repo belong to
        // TenantB. Service must reject without leaking that the id exists.
        var tenantA = new TenantId("tenant-A");
        var tenantB = new TenantId("tenant-B");

        var rig = CreateRig(tenant: tenantA);

        // Seed a TenantB payment + invoice (e.g., via a back-door repo write).
        var foreignPayment = Payment.Create(
            tenantId: tenantB,
            chartId: TestChart,
            direction: PaymentDirection.Inbound,
            paymentNumber: $"PMT-{Guid.NewGuid():N}",
            partyId: TestCustomer,
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.Check,
            bankAccountId: BankAccount)
            with
            {
                Status = PaymentStatus.Unapplied,
                JournalEntryId = JournalEntryId.NewId(),
            };
        await rig.Payments.AddAsync(foreignPayment);

        var foreignInvoiceId = $"inv-{Guid.NewGuid():N}";
        var seq = Interlocked.Increment(ref _invoiceSeq);
        var foreignInvoice = Invoice.Create(
            tenantId: tenantB,
            chartId: TestChart,
            invoiceNumber: $"INV-2026-05-18-T-{seq:D4}",
            customerId: TestCustomer,
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 6, 1),
            lines: [InvoiceLine.Create(new InvoiceId(foreignInvoiceId), 1, "Rent", 1m, 100m, IncomeAccount)],
            arAccountId: ArControl,
            id: new InvoiceId(foreignInvoiceId))
            with
            {
                Status = InvoiceStatus.Issued,
                Balance = 100m,
            };
        await rig.Invoices.UpsertAsync(foreignInvoice);

        var result = await rig.Service.ApplyAsync(
            foreignPayment.Id, AppliedTo.Invoice, foreignInvoiceId,
            amountApplied: 100m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.UnknownPayment, result.Error);
        // Diagnostic must not reveal tenant state.
        Assert.DoesNotContain("tenant", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Payment + Invoice must be untouched.
        Assert.Equal(PaymentStatus.Unapplied, (await rig.Payments.GetAsync(foreignPayment.Id))!.Status);
        Assert.Equal(100m, (await rig.Invoices.GetAsync(new InvoiceId(foreignInvoiceId)))!.Balance);
        Assert.Empty(rig.Events.Published);
    }

    [Fact]
    public async Task ApplyAsync_CrossTenantInvoice_ReturnsUnknownTarget()
    {
        // Direction-matches and Payment belongs to TenantA, but Invoice is
        // TenantB. The target-tenant check must reject with UnknownTarget.
        var tenantA = new TenantId("tenant-A");
        var tenantB = new TenantId("tenant-B");
        var rig = CreateRig(tenant: tenantA);

        var ownPayment = Payment.Create(
            tenantId: tenantA,
            chartId: TestChart,
            direction: PaymentDirection.Inbound,
            paymentNumber: $"PMT-{Guid.NewGuid():N}",
            partyId: TestCustomer,
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.Check,
            bankAccountId: BankAccount)
            with
            {
                Status = PaymentStatus.Unapplied,
                JournalEntryId = JournalEntryId.NewId(),
            };
        await rig.Payments.AddAsync(ownPayment);

        var foreignInvoiceId = $"inv-{Guid.NewGuid():N}";
        var seq = Interlocked.Increment(ref _invoiceSeq);
        var foreignInvoice = Invoice.Create(
            tenantId: tenantB,
            chartId: TestChart,
            invoiceNumber: $"INV-2026-05-18-T-{seq:D4}",
            customerId: TestCustomer,
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 6, 1),
            lines: [InvoiceLine.Create(new InvoiceId(foreignInvoiceId), 1, "Rent", 1m, 100m, IncomeAccount)],
            arAccountId: ArControl,
            id: new InvoiceId(foreignInvoiceId))
            with { Status = InvoiceStatus.Issued, Balance = 100m };
        await rig.Invoices.UpsertAsync(foreignInvoice);

        var result = await rig.Service.ApplyAsync(
            ownPayment.Id, AppliedTo.Invoice, foreignInvoiceId,
            amountApplied: 100m, 0m, 0m, TestActor);

        Assert.Equal(ApplyError.UnknownTarget, result.Error);
        Assert.Empty(rig.Events.Published);
        Assert.Empty(await rig.Applications.ListByPaymentAsync(ownPayment.Id));
    }

    [Fact]
    public async Task UnapplyAsync_CrossTenantApplication_ReturnsUnknownApplication()
    {
        // Apply succeeds in TenantA's rig, then switch the rig's tenant to
        // TenantB and try to unapply — service must reject.
        var tenantA = new TenantId("tenant-A");
        var tenantB = new TenantId("tenant-B");
        var rig = CreateRig(tenant: tenantA);

        var payment = Payment.Create(
            tenantId: tenantA,
            chartId: TestChart,
            direction: PaymentDirection.Inbound,
            paymentNumber: $"PMT-{Guid.NewGuid():N}",
            partyId: TestCustomer,
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.Check,
            bankAccountId: BankAccount)
            with
            {
                Status = PaymentStatus.Unapplied,
                JournalEntryId = JournalEntryId.NewId(),
            };
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        var seq = Interlocked.Increment(ref _invoiceSeq);
        var ownInvoice = Invoice.Create(
            tenantId: tenantA,
            chartId: TestChart,
            invoiceNumber: $"INV-2026-05-18-T-{seq:D4}",
            customerId: TestCustomer,
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 6, 1),
            lines: [InvoiceLine.Create(new InvoiceId(invoiceId), 1, "Rent", 1m, 100m, IncomeAccount)],
            arAccountId: ArControl,
            id: new InvoiceId(invoiceId))
            with { Status = InvoiceStatus.Issued, Balance = 100m };
        await rig.Invoices.UpsertAsync(ownInvoice);

        var apply = await rig.Service.ApplyAsync(
            payment.Id, AppliedTo.Invoice, invoiceId, 100m, 0m, 0m, TestActor);
        Assert.Equal(ApplyError.None, apply.Error);
        var applicationId = apply.Application!.Id;

        // Flip the rig's tenant context to TenantB.
        rig.TenantContext.SetTenant(tenantB);

        var result = await rig.Service.UnapplyAsync(applicationId, TestActor);

        Assert.Equal(UnapplyError.UnknownApplication, result.Error);
        Assert.False(result.Success);
        // Application must still exist (not deleted).
        Assert.NotNull(await rig.Applications.GetAsync(applicationId));
    }

    [Fact]
    public async Task Service_WithUnresolvedTenantContext_ThrowsOnInvocation()
    {
        // Unresolved tenant is a programmer error (composition-root bug).
        // It must throw immediately, not fail-closed silently.
        var rig = CreateRig();
        rig.TenantContext.SetTenant(null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ApplyAsync(PaymentId.NewId(), AppliedTo.Invoice, "any", 1m, 0m, 0m, TestActor));
    }
}

/// <summary>
/// Test fake <see cref="IDomainEventPublisher"/> that captures every envelope
/// passed to <see cref="PublishAsync"/>. Tests assert on
/// <see cref="Published"/> count + ordering.
/// </summary>
internal sealed class RecordingDomainEventPublisher : IDomainEventPublisher
{
    private readonly List<RecordedEvent> _published = new();

    public IReadOnlyList<RecordedEvent> Published => _published;

    public Task PublishAsync<TPayload>(DomainEventEnvelope<TPayload> envelope, CancellationToken cancellationToken = default)
    {
        _published.Add(new RecordedEvent(envelope.EventType, envelope.IdempotencyKey, envelope.Payload!));
        return Task.CompletedTask;
    }

    public sealed record RecordedEvent(string EventType, string IdempotencyKey, object Payload);
}

/// <summary>
/// Test fake <see cref="ITenantContext"/> with a settable tenant. Lets a
/// single test mid-flow switch the active tenant — useful for cross-tenant
/// scenarios where Apply runs as tenant A and Unapply runs as tenant B.
/// </summary>
internal sealed class StubTenantContext : ITenantContext
{
    private TenantMetadata? _tenant;

    public StubTenantContext(TenantId? id = null)
    {
        if (id is { } value)
            _tenant = new TenantMetadata { Id = value, Name = value.Value };
    }

    public TenantMetadata? Tenant => _tenant;

    public void SetTenant(TenantId? id)
    {
        _tenant = id is { } value
            ? new TenantMetadata { Id = value, Name = value.Value }
            : null;
    }
}
