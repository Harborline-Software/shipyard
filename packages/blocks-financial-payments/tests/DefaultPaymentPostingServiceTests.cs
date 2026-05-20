using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;
// VoidError + VoidResult exist in AR, AP, and Payments — alias to the Payments
// types so unqualified references in the tests bind unambiguously.
using VoidError = Sunfish.Blocks.FinancialPayments.Services.VoidError;
using VoidResult = Sunfish.Blocks.FinancialPayments.Services.VoidResult;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>
/// Unit tests for <see cref="DefaultPaymentPostingService"/> covering the
/// Clear / Bounce / Void state transitions, JE construction per direction,
/// idempotency, and the bounce-path Invoice / Bill balance restoration.
/// </summary>
public class DefaultPaymentPostingServiceTests
{
    // ──────────────────────────────────────────────────────────────────
    //  Test scaffolding
    // ──────────────────────────────────────────────────────────────────

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
        DefaultPaymentPostingService Service,
        InMemoryPaymentRepository Payments,
        InMemoryPaymentApplicationRepository Applications,
        InMemoryInvoiceRepository Invoices,
        InMemoryBillRepository Bills,
        InMemoryAccountResolver Accounts,
        FakeJournalPostingService Journals);

    private sealed class StubTenantContext : ITenantContext
    {
        public StubTenantContext(TenantId id)
        {
            Tenant = new TenantMetadata { Id = id, Name = id.Value };
        }
        public TenantMetadata? Tenant { get; }
    }

    private static TestRig CreateRig()
    {
        var payments = new InMemoryPaymentRepository();
        var applications = new InMemoryPaymentApplicationRepository();
        var invoices = new InMemoryInvoiceRepository();
        var bills = new InMemoryBillRepository();
        var accounts = new InMemoryAccountResolver(SeedAccounts());
        var journals = new FakeJournalPostingService();
        var ctx = new StubTenantContext(TestTenant);
        var service = new DefaultPaymentPostingService(ctx, payments, applications, invoices, bills, accounts, journals);
        return new TestRig(service, payments, applications, invoices, bills, accounts, journals);
    }

    private static IEnumerable<GLAccount> SeedAccounts() =>
    [
        GLAccount.Create(BankAccount, TestChart, "1010", "Operating Bank", GLAccountType.Asset, AccountSubtype.BankAccount, "USD"),
        GLAccount.Create(ArControl, TestChart, "1200", "Accounts Receivable", GLAccountType.Asset, AccountSubtype.AccountsReceivable, "USD"),
        GLAccount.Create(ApControl, TestChart, "2100", "Accounts Payable", GLAccountType.Liability, AccountSubtype.AccountsPayable, "USD"),
        GLAccount.Create(IncomeAccount, TestChart, "4000", "Rental Income", GLAccountType.Revenue, AccountSubtype.OperatingIncome, "USD"),
        GLAccount.Create(ExpenseAccount, TestChart, "6000", "Utilities", GLAccountType.Expense, AccountSubtype.OperatingExpense, "USD"),
    ];

    private static Payment NewDraft(PaymentDirection direction = PaymentDirection.Inbound, decimal amount = 1000m) =>
        Payment.Create(
            tenantId: TestTenant,
            chartId: TestChart,
            direction: direction,
            paymentNumber: $"PMT-{Guid.NewGuid():N}",
            partyId: direction == PaymentDirection.Inbound ? TestCustomer : TestVendor,
            paymentDate: new DateOnly(2026, 5, 18),
            amount: amount,
            method: PaymentMethod.Check,
            bankAccountId: BankAccount);

    private static int _invoiceSeq;

    private static Invoice NewIssuedInvoice(string invoiceId, decimal total, decimal amountPaid = 0m, InvoiceStatus status = InvoiceStatus.Issued)
    {
        // Repository enforces canonical `INV-YYYY-MM-DD-{REPLICA}-{NNNN+}` format for non-Draft invoices.
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
            id: new InvoiceId(invoiceId));
        return invoice with
        {
            Status = status,
            AmountPaid = amountPaid,
            Balance = total - amountPaid,
        };
    }

    private static Bill NewReceivedBill(string billId, decimal total, decimal amountPaid = 0m, BillStatus status = BillStatus.Received)
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
            id: new BillId(billId));
        return bill with
        {
            Status = status,
            AmountPaid = amountPaid,
            Balance = total - amountPaid,
        };
    }

    // ──────────────────────────────────────────────────────────────────
    //  ClearAsync tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearAsync_Inbound_PostsDrBank_CrAr_AndTransitionsToUnapplied()
    {
        var rig = CreateRig();
        var payment = NewDraft(PaymentDirection.Inbound, amount: 1500m);
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.ClearAsync(payment.Id, TestActor);

        Assert.Equal(ClearError.None, result.Error);
        Assert.NotNull(result.Payment);
        Assert.Equal(PaymentStatus.Unapplied, result.Payment!.Status);
        Assert.NotNull(result.JournalEntryId);

        var entry = Assert.Single(rig.Journals.PostedEntries);
        Assert.Equal(2, entry.Lines.Count);

        var debit = Assert.Single(entry.Lines, l => l.Debit > 0m);
        var credit = Assert.Single(entry.Lines, l => l.Credit > 0m);
        Assert.Equal(BankAccount, debit.AccountId);
        Assert.Equal(1500m, debit.Debit);
        Assert.Equal(ArControl, credit.AccountId);
        Assert.Equal(1500m, credit.Credit);
    }

    [Fact]
    public async Task ClearAsync_Outbound_PostsDrAp_CrBank_AndTransitionsToUnapplied()
    {
        var rig = CreateRig();
        var payment = NewDraft(PaymentDirection.Outbound, amount: 750m);
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.ClearAsync(payment.Id, TestActor);

        Assert.Equal(ClearError.None, result.Error);
        Assert.Equal(PaymentStatus.Unapplied, result.Payment!.Status);

        var entry = Assert.Single(rig.Journals.PostedEntries);
        var debit = Assert.Single(entry.Lines, l => l.Debit > 0m);
        var credit = Assert.Single(entry.Lines, l => l.Credit > 0m);
        Assert.Equal(ApControl, debit.AccountId);
        Assert.Equal(750m, debit.Debit);
        Assert.Equal(BankAccount, credit.AccountId);
        Assert.Equal(750m, credit.Credit);
    }

    [Fact]
    public async Task ClearAsync_Idempotent_AlreadyClearedReturnsExistingJournalEntry()
    {
        var rig = CreateRig();
        var payment = NewDraft();
        await rig.Payments.AddAsync(payment);

        var firstClear = await rig.Service.ClearAsync(payment.Id, TestActor);
        Assert.Equal(ClearError.None, firstClear.Error);
        var firstJournalId = firstClear.JournalEntryId!.Value;

        var secondClear = await rig.Service.ClearAsync(payment.Id, TestActor);
        Assert.Equal(ClearError.None, secondClear.Error);
        Assert.Equal(firstJournalId, secondClear.JournalEntryId);
        Assert.Single(rig.Journals.PostedEntries); // no duplicate JE posted
    }

    [Fact]
    public async Task ClearAsync_NonDraftWithoutJournalEntry_ReturnsInvalidStatusForClear()
    {
        var rig = CreateRig();
        var payment = NewDraft() with { Status = PaymentStatus.Voided };
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.ClearAsync(payment.Id, TestActor);

        Assert.Equal(ClearError.InvalidStatusForClear, result.Error);
        Assert.Null(result.JournalEntryId);
        Assert.Empty(rig.Journals.PostedEntries);
    }

    [Fact]
    public async Task ClearAsync_UnknownPayment_ReturnsUnknownPayment()
    {
        var rig = CreateRig();
        var result = await rig.Service.ClearAsync(PaymentId.NewId(), TestActor);
        Assert.Equal(ClearError.UnknownPayment, result.Error);
        Assert.Empty(rig.Journals.PostedEntries);
    }

    [Fact]
    public async Task ClearAsync_MissingArControlAccount_ReturnsJournalRejected()
    {
        var rig = CreateRig();
        // Replace AR control with the same id but inactive — resolver filter excludes it.
        var inactiveAr = GLAccount.Create(ArControl, TestChart, "1200", "Accounts Receivable", GLAccountType.Asset, AccountSubtype.AccountsReceivable, "USD")
            with { IsActive = false };
        rig.Accounts.Upsert(inactiveAr);

        var payment = NewDraft(PaymentDirection.Inbound);
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.ClearAsync(payment.Id, TestActor);

        Assert.Equal(ClearError.JournalRejected, result.Error);
        Assert.Contains("AccountsReceivable", result.ErrorMessage);
        Assert.Empty(rig.Journals.PostedEntries);
    }

    // ──────────────────────────────────────────────────────────────────
    //  BounceAsync tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BounceAsync_Inbound_PostsReversalAndDeletesApplicationsAndRestoresInvoiceBalance()
    {
        var rig = CreateRig();
        // Seed an Inbound payment in Applied state, with one application against an Invoice.
        var payment = NewDraft(PaymentDirection.Inbound, amount: 500m) with
        {
            Status = PaymentStatus.Applied,
            UnappliedAmount = 0m,
            JournalEntryId = JournalEntryId.NewId(),
        };
        await rig.Payments.AddAsync(payment);

        var invoiceId = $"inv-{Guid.NewGuid():N}";
        var invoice = NewIssuedInvoice(invoiceId, total: 500m, amountPaid: 500m, status: InvoiceStatus.Paid);
        await rig.Invoices.UpsertAsync(TestTenant, invoice);

        var application = PaymentApplication.Create(TestTenant, payment.Id, AppliedTo.Invoice, invoiceId, amountApplied: 500m, appliedDate: new DateOnly(2026, 5, 18));
        await rig.Applications.AddAsync(application);

        var result = await rig.Service.BounceAsync(payment.Id, "NSF", TestActor);

        Assert.Equal(BounceError.None, result.Error);
        Assert.Equal(PaymentStatus.Bounced, result.Payment!.Status);
        Assert.Equal(500m, result.Payment.UnappliedAmount);

        // Reversal JE: Dr AR control / Cr Bank
        var entry = Assert.Single(rig.Journals.PostedEntries);
        var debit = Assert.Single(entry.Lines, l => l.Debit > 0m);
        var credit = Assert.Single(entry.Lines, l => l.Credit > 0m);
        Assert.Equal(ArControl, debit.AccountId);
        Assert.Equal(BankAccount, credit.AccountId);

        // Application deleted.
        var orphan = await rig.Applications.GetAsync(application.Id);
        Assert.Null(orphan);

        // Invoice balance restored.
        var restored = await rig.Invoices.GetAsync(TestTenant, new InvoiceId(invoiceId));
        Assert.NotNull(restored);
        Assert.Equal(0m, restored!.AmountPaid);
        Assert.Equal(500m, restored.Balance);
        Assert.Equal(InvoiceStatus.Issued, restored.Status);
    }

    [Fact]
    public async Task BounceAsync_Outbound_RestoresBillBalanceAndPostsReversal()
    {
        var rig = CreateRig();
        var payment = NewDraft(PaymentDirection.Outbound, amount: 200m) with
        {
            Status = PaymentStatus.Applied,
            UnappliedAmount = 0m,
            JournalEntryId = JournalEntryId.NewId(),
        };
        await rig.Payments.AddAsync(payment);

        var billId = $"bill-{Guid.NewGuid():N}";
        var bill = NewReceivedBill(billId, total: 200m, amountPaid: 200m, status: BillStatus.Paid);
        await rig.Bills.UpsertAsync(bill);

        var application = PaymentApplication.Create(TestTenant, payment.Id, AppliedTo.Bill, billId, amountApplied: 200m, appliedDate: new DateOnly(2026, 5, 18));
        await rig.Applications.AddAsync(application);

        var result = await rig.Service.BounceAsync(payment.Id, "stop payment", TestActor);

        Assert.Equal(BounceError.None, result.Error);
        Assert.Equal(PaymentStatus.Bounced, result.Payment!.Status);

        // Reversal JE for Outbound: Dr Bank / Cr AP control.
        var entry = Assert.Single(rig.Journals.PostedEntries);
        var debit = Assert.Single(entry.Lines, l => l.Debit > 0m);
        var credit = Assert.Single(entry.Lines, l => l.Credit > 0m);
        Assert.Equal(BankAccount, debit.AccountId);
        Assert.Equal(ApControl, credit.AccountId);

        var restored = await rig.Bills.GetAsync(new BillId(billId));
        Assert.NotNull(restored);
        Assert.Equal(0m, restored!.AmountPaid);
        Assert.Equal(BillStatus.Received, restored.Status);
    }

    [Fact]
    public async Task BounceAsync_NonClearedStatus_ReturnsInvalidStatusForBounce()
    {
        var rig = CreateRig();
        var payment = NewDraft(); // Draft
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.BounceAsync(payment.Id, "n/a", TestActor);

        Assert.Equal(BounceError.InvalidStatusForBounce, result.Error);
        Assert.Empty(rig.Journals.PostedEntries);
    }

    // ──────────────────────────────────────────────────────────────────
    //  VoidAsync tests
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VoidAsync_Draft_TransitionsToVoidedAndPostsNoJournalEntry()
    {
        var rig = CreateRig();
        var payment = NewDraft();
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.VoidAsync(payment.Id, "data-entry error", TestActor);

        Assert.Equal(VoidError.None, result.Error);
        Assert.Equal(PaymentStatus.Voided, result.Payment!.Status);
        Assert.Empty(rig.Journals.PostedEntries);
    }

    [Fact]
    public async Task VoidAsync_ClearedPayment_ReturnsInvalidStatusForVoid()
    {
        var rig = CreateRig();
        var payment = NewDraft() with { Status = PaymentStatus.Unapplied, JournalEntryId = JournalEntryId.NewId() };
        await rig.Payments.AddAsync(payment);

        var result = await rig.Service.VoidAsync(payment.Id, "n/a", TestActor);

        Assert.Equal(VoidError.InvalidStatusForVoid, result.Error);
        Assert.Empty(rig.Journals.PostedEntries);
    }
}

/// <summary>
/// Test fake for <see cref="IJournalPostingService"/> that records every
/// entry passed to <see cref="PostAsync"/> and returns success. Callers
/// that need to simulate a journal rejection set <see cref="NextResult"/>
/// before invoking the service under test.
/// </summary>
internal sealed class FakeJournalPostingService : IJournalPostingService
{
    private readonly List<JournalEntry> _posted = new();

    public IReadOnlyList<JournalEntry> PostedEntries => _posted;

    public PostResult? NextResult { get; set; }

    public Task<PostResult> PostAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        _posted.Add(entry);
        var result = NextResult ?? new PostResult(entry with { Status = JournalEntryStatus.Posted, PostedAtUtc = Instant.Now }, PostError.None, null);
        NextResult = null;
        return Task.FromResult(result);
    }
}
