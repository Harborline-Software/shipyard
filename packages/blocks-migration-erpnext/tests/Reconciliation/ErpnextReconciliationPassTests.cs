using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Blocks.Migration.Erpnext.Reconciliation;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Reconciliation;

/// <summary>
/// Unit coverage for <see cref="ErpnextReconciliationPass"/> — importer Pass 5 (spec §4.5).
/// Drives the pass against InMemory repos seeded with synthetic Payment/Invoice/Bill rows;
/// uses a recording <see cref="IReconciliationApplier"/> fake to assert dispatch shape
/// without taking a hard dependency on the runtime <c>IPaymentApplicationService</c>.
/// </summary>
public sealed class ErpnextReconciliationPassTests
{
    private static readonly TenantId Tenant = new($"test-tenant-{Guid.NewGuid():N}");
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly GLAccountId ArAccount = GLAccountId.NewId();
    private static readonly GLAccountId ApAccount = GLAccountId.NewId();

    [Fact]
    public async Task Unique_inbound_match_applies_payment_to_invoice()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 250.00m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);

        var invoice = NewInvoice(amount: 250.00m, customer, issueDate: new(2026, 5, 14));
        await invoices.UpsertAsync(Tenant, invoice);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(0, result.AmbiguousCount);
        Assert.Equal(0, result.UnmatchedCount);
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(PaymentReconciliationOutcomeKind.Applied, outcome.Kind);
        Assert.Equal(AppliedTo.Invoice, outcome.Target);
        Assert.Equal(invoice.Id.ToString(), outcome.TargetId);
        Assert.Equal(250.00m, outcome.AmountApplied);
        Assert.Single(applier.RecordedApplies);
    }

    [Fact]
    public async Task Unique_outbound_match_applies_payment_to_bill()
    {
        var vendor = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewOutboundPayment(amount: 1200.00m, vendor, paymentDate: new(2026, 5, 20));
        await payments.AddAsync(Tenant, payment);

        var bill = NewBill(amount: 1200.00m, vendor, billDate: new(2026, 5, 22));
        await bills.UpsertAsync(Tenant, bill);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.AppliedCount);
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(AppliedTo.Bill, outcome.Target);
        Assert.Equal(bill.Id.ToString(), outcome.TargetId);
    }

    [Fact]
    public async Task Ambiguous_multiple_matches_surface_candidates_unapplied()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 500.00m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);

        var inv1 = NewInvoice(amount: 500.00m, customer, issueDate: new(2026, 5, 14));
        var inv2 = NewInvoice(amount: 500.00m, customer, issueDate: new(2026, 5, 16));
        await invoices.UpsertAsync(Tenant, inv1);
        await invoices.UpsertAsync(Tenant, inv2);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.AmbiguousCount);
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(PaymentReconciliationOutcomeKind.Ambiguous, outcome.Kind);
        Assert.NotNull(outcome.AmbiguousCandidateIds);
        Assert.Equal(2, outcome.AmbiguousCandidateIds!.Count);
        Assert.Contains(inv1.Id.ToString(), outcome.AmbiguousCandidateIds);
        Assert.Contains(inv2.Id.ToString(), outcome.AmbiguousCandidateIds);
        Assert.Empty(applier.RecordedApplies); // ambiguous → never applies
    }

    [Fact]
    public async Task No_match_leaves_payment_unmatched()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 333.33m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);
        // No invoice in repo.

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.UnmatchedCount);
        var outcome = Assert.Single(result.Outcomes);
        Assert.Equal(PaymentReconciliationOutcomeKind.Unmatched, outcome.Kind);
        Assert.Null(outcome.Target);
        Assert.Null(outcome.TargetId);
        Assert.Empty(applier.RecordedApplies);
    }

    [Fact]
    public async Task Date_outside_window_does_not_match()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 100m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);
        // Invoice 8 days off — outside the default ±7-day window.
        var invoice = NewInvoice(amount: 100m, customer, issueDate: new(2026, 5, 7));
        await invoices.UpsertAsync(Tenant, invoice);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.UnmatchedCount);
        Assert.Empty(applier.RecordedApplies);
    }

    [Fact]
    public async Task Configurable_date_window_widens_match()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 100m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);
        var invoice = NewInvoice(amount: 100m, customer, issueDate: new(2026, 5, 1)); // 14 days off
        await invoices.UpsertAsync(Tenant, invoice);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart, new ReconciliationOptions { DateWindowDays = 14 });

        Assert.Equal(1, result.AppliedCount);
    }

    [Fact]
    public async Task Different_party_does_not_match()
    {
        var customerA = PartyId.NewId();
        var customerB = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 100m, customerA, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);
        var invoice = NewInvoice(amount: 100m, customerB, issueDate: new(2026, 5, 15));
        await invoices.UpsertAsync(Tenant, invoice);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.UnmatchedCount);
    }

    [Fact]
    public async Task Different_amount_does_not_match()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        var payment = NewInboundPayment(amount: 100m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);
        var invoice = NewInvoice(amount: 99.99m, customer, issueDate: new(2026, 5, 15));
        await invoices.UpsertAsync(Tenant, invoice);

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.UnmatchedCount);
    }

    [Fact]
    public async Task Fully_applied_payments_are_skipped()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, applier) = CreateRepos();

        // UnappliedAmount = 0 → skipped entirely (not in the unapplied set).
        var fullyApplied = NewInboundPayment(amount: 100m, customer, paymentDate: new(2026, 5, 15), unappliedAmount: 0m);
        await payments.AddAsync(Tenant, fullyApplied);
        await invoices.UpsertAsync(Tenant, NewInvoice(amount: 100m, customer, issueDate: new(2026, 5, 15)));

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Empty(result.Outcomes);
        Assert.Empty(applier.RecordedApplies);
    }

    [Fact]
    public async Task Applier_reject_records_unmatched()
    {
        var customer = PartyId.NewId();
        var (payments, invoices, bills, _) = CreateRepos();
        var applier = new RecordingReconciliationApplier(succeed: false); // applier rejects

        var payment = NewInboundPayment(amount: 100m, customer, paymentDate: new(2026, 5, 15));
        await payments.AddAsync(Tenant, payment);
        await invoices.UpsertAsync(Tenant, NewInvoice(amount: 100m, customer, issueDate: new(2026, 5, 15)));

        var result = await new ErpnextReconciliationPass(payments, invoices, bills, applier)
            .RunAsync(Tenant, Chart);

        Assert.Equal(1, result.UnmatchedCount);
        Assert.Single(applier.RecordedApplies); // applier was attempted, rejected
    }

    // ─────────────────────────── test fixtures ───────────────────────────

    private static (InMemoryPaymentRepository, InMemoryInvoiceRepository, InMemoryBillRepository, RecordingReconciliationApplier) CreateRepos() =>
        (new InMemoryPaymentRepository(), new InMemoryInvoiceRepository(), new InMemoryBillRepository(), new RecordingReconciliationApplier(succeed: true));

    private static Payment NewInboundPayment(decimal amount, PartyId customer, DateOnly paymentDate, decimal? unappliedAmount = null)
    {
        var p = Payment.Create(
            tenantId: Tenant,
            chartId: Chart,
            direction: PaymentDirection.Inbound,
            paymentNumber: $"PAY-IN-{Guid.NewGuid():N}",
            partyId: customer,
            paymentDate: paymentDate,
            amount: amount,
            method: PaymentMethod.Check);
        return unappliedAmount is null ? p : p with { UnappliedAmount = unappliedAmount.Value };
    }

    private static Payment NewOutboundPayment(decimal amount, PartyId vendor, DateOnly paymentDate, decimal? unappliedAmount = null)
    {
        var p = Payment.Create(
            tenantId: Tenant,
            chartId: Chart,
            direction: PaymentDirection.Outbound,
            paymentNumber: $"PAY-OUT-{Guid.NewGuid():N}",
            partyId: vendor,
            paymentDate: paymentDate,
            amount: amount,
            method: PaymentMethod.Check);
        return unappliedAmount is null ? p : p with { UnappliedAmount = unappliedAmount.Value };
    }

    // Per memory [[feedback_invoice_number_format_test_fixtures]] — non-Draft Invoice fixtures need canonical
    // `INV-YYYY-MM-DD-{Replica}-{NNNN}` format or InMemoryInvoiceRepository.UpsertAsync throws.
    private static int _invoiceCounter;
    private static string NextInvoiceNumber(DateOnly issueDate) =>
        $"INV-{issueDate:yyyy-MM-dd}-01-{System.Threading.Interlocked.Increment(ref _invoiceCounter) % 10_000:D4}";

    // Use Invoice.Create + override Total/Balance so the fixture amount drives matching without needing real lines.
    private static Invoice NewInvoice(decimal amount, PartyId customer, DateOnly issueDate) =>
        Invoice.Create(
            tenantId: Tenant,
            chartId: Chart,
            invoiceNumber: NextInvoiceNumber(issueDate),
            customerId: customer,
            issueDate: issueDate,
            dueDate: issueDate.AddDays(30),
            lines: Array.Empty<InvoiceLine>(),
            arAccountId: ArAccount) with { Total = amount, Balance = amount, Status = InvoiceStatus.Issued };

    private static Bill NewBill(decimal amount, PartyId vendor, DateOnly billDate) =>
        Bill.Create(
            tenantId: Tenant,
            chartId: Chart,
            billNumber: $"BILL-{Guid.NewGuid():N}",
            vendorId: vendor,
            billDate: billDate,
            dueDate: billDate.AddDays(30),
            lines: Array.Empty<BillLine>(),
            apAccountId: ApAccount) with { Total = amount, Balance = amount };

    private sealed class RecordingReconciliationApplier : IReconciliationApplier
    {
        private readonly bool _succeed;
        public List<(TenantId Tenant, PaymentId Payment, AppliedTo Target, string TargetId, decimal Amount)> RecordedApplies { get; } = [];

        public RecordingReconciliationApplier(bool succeed)
        {
            _succeed = succeed;
        }

        public Task<bool> ApplyAsync(
            TenantId tenantId,
            PaymentId paymentId,
            AppliedTo appliedTo,
            string targetId,
            decimal amountApplied,
            CancellationToken cancellationToken = default)
        {
            RecordedApplies.Add((tenantId, paymentId, appliedTo, targetId, amountApplied));
            return Task.FromResult(_succeed);
        }
    }
}
