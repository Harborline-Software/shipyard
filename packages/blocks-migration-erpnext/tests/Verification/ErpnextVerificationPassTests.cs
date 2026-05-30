using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.Migration.Erpnext.Verification;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using ApModels = Sunfish.Blocks.FinancialAp.Models;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Verification;

/// <summary>
/// Unit coverage for <see cref="ErpnextVerificationPass"/> — importer Pass 6 (spec §4.6).
/// Drives the read-only pass against hand fakes that return canned ledger balances, account
/// resolutions, aging summaries, and invoices so each of the four checks can be exercised in
/// isolation. No disk, no DB, no real run — the pass is pure over its read interfaces.
/// </summary>
public sealed class ErpnextVerificationPassTests
{
    private static readonly TenantId Tenant = new($"test-tenant-{Guid.NewGuid():N}");
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly DateOnly AsOf = new(2026, 5, 16);

    [Fact]
    public async Task Balanced_ledger_no_snapshots_passes()
    {
        var asset = Account("1000", GLAccountType.Asset);
        var revenue = Account("4000", GLAccountType.Revenue);
        var pass = NewPass(
            balances: new() { [asset.Id] = 100m, [revenue.Id] = -100m },
            accounts: new[] { asset, revenue });

        var result = await pass.RunAsync(Tenant, Chart, AsOf);

        Assert.Equal(VerificationOutcome.Passed, result.Outcome);
        Assert.True(result.TrialBalance.IsBalanced);
        Assert.Equal(0m, result.TrialBalance.SignedTotal);
        Assert.False(result.ArAging.Checked);
        Assert.False(result.ApAging.Checked);
        Assert.False(result.AccountBalances.Checked);
    }

    [Fact]
    public async Task Unbalanced_ledger_halts_with_trial_balance_mismatch()
    {
        var asset = Account("1000", GLAccountType.Asset);
        var revenue = Account("4000", GLAccountType.Revenue);
        var pass = NewPass(
            balances: new() { [asset.Id] = 100m, [revenue.Id] = -99m },
            accounts: new[] { asset, revenue });

        var result = await pass.RunAsync(Tenant, Chart, AsOf);

        Assert.Equal(VerificationOutcome.TrialBalanceMismatch, result.Outcome);
        Assert.False(result.TrialBalance.IsBalanced);
        Assert.Equal(1m, result.TrialBalance.SignedTotal);
    }

    [Fact]
    public async Task Unresolved_account_balances_count_toward_total_but_are_unclassified()
    {
        var asset = Account("1000", GLAccountType.Asset);
        var revenue = Account("4000", GLAccountType.Revenue);
        var mysteryA = GLAccountId.NewId();
        var mysteryB = GLAccountId.NewId();

        // Two accounts the resolver can't classify; their balances net to zero so the ledger
        // still balances exactly — proving the unclassified balances are still summed (defense
        // against a missing-account-type bug being masked).
        var pass = NewPass(
            balances: new()
            {
                [asset.Id] = 100m,
                [revenue.Id] = -100m,
                [mysteryA] = 5m,
                [mysteryB] = -5m,
            },
            accounts: new[] { asset, revenue });

        var result = await pass.RunAsync(Tenant, Chart, AsOf);

        Assert.True(result.TrialBalance.IsBalanced);
        Assert.Equal(2, result.TrialBalance.UnclassifiedAccountCount);
    }

    [Fact]
    public async Task Ar_aging_within_threshold_passes()
    {
        var customer = PartyId.NewId();
        var pass = NewPass(
            balances: BalancedBooks(out var accounts),
            accounts: accounts,
            arRows: new[] { ArRow(customer, AgingBucket.Current, 100.00m) });

        var snapshot = new ArAgingSnapshot(new[] { Aging(customer, current: 100.00m) });
        var result = await pass.RunAsync(Tenant, Chart, AsOf, arSnapshot: snapshot);

        Assert.Equal(VerificationOutcome.Passed, result.Outcome);
        Assert.True(result.ArAging.Checked);
        Assert.True(result.ArAging.WithinThreshold);
    }

    [Fact]
    public async Task Ar_aging_over_threshold_halts_with_aging_failure()
    {
        var customer = PartyId.NewId();
        var pass = NewPass(
            balances: BalancedBooks(out var accounts),
            accounts: accounts,
            arRows: new[] { ArRow(customer, AgingBucket.Current, 100.50m) });

        var snapshot = new ArAgingSnapshot(new[] { Aging(customer, current: 100.00m) });
        var result = await pass.RunAsync(Tenant, Chart, AsOf, arSnapshot: snapshot);

        Assert.Equal(VerificationOutcome.AgingReconciliationFailed, result.Outcome);
        Assert.False(result.ArAging.WithinThreshold);
        var party = Assert.Single(result.ArAging.ExceedingRows);
        Assert.Equal(customer, party.PartyId);
        var bucket = Assert.Single(party.Buckets);
        Assert.Equal(100.00m, bucket.Expected);
        Assert.Equal(100.50m, bucket.Actual);
        Assert.Equal(0.50m, bucket.Diff);
    }

    [Fact]
    public async Task Ar_aging_drift_downgraded_to_warning_when_flag_set()
    {
        var customer = PartyId.NewId();
        var pass = NewPass(
            balances: BalancedBooks(out var accounts),
            accounts: accounts,
            arRows: new[] { ArRow(customer, AgingBucket.Current, 100.50m) });

        var snapshot = new ArAgingSnapshot(new[] { Aging(customer, current: 100.00m) });
        var result = await pass.RunAsync(
            Tenant, Chart, AsOf,
            arSnapshot: snapshot,
            options: new VerificationOptions { AllowAgingDrift = true });

        // Drift is still recorded (not within threshold) but the run is allowed to pass.
        Assert.Equal(VerificationOutcome.Passed, result.Outcome);
        Assert.False(result.ArAging.WithinThreshold);
    }

    [Fact]
    public async Task Ap_aging_over_threshold_halts_with_aging_failure()
    {
        var vendor = PartyId.NewId();
        var pass = NewPass(
            balances: BalancedBooks(out var accounts),
            accounts: accounts,
            apRows: new[] { ApRow(vendor, ApModels.AgingBucket.Days31To60, 250.00m) });

        var snapshot = new ApAgingSnapshot(new[] { Aging(vendor, days31To60: 200.00m) });
        var result = await pass.RunAsync(Tenant, Chart, AsOf, apSnapshot: snapshot);

        Assert.Equal(VerificationOutcome.AgingReconciliationFailed, result.Outcome);
        Assert.False(result.ApAging.WithinThreshold);
    }

    [Fact]
    public async Task Per_account_diffs_are_sorted_by_absolute_difference_descending()
    {
        var a1 = Account("1000", GLAccountType.Asset);
        var a2 = Account("2000", GLAccountType.Liability);
        var a3 = Account("3000", GLAccountType.Asset);
        var pass = NewPass(
            balances: new() { [a1.Id] = 100m, [a2.Id] = 10m, [a3.Id] = 100m },
            accounts: new[] { a1, a2, a3 });

        var snapshot = new GlBalancesSnapshot(new[]
        {
            new AccountBalanceSnapshotRow("1000", 50m),       // diff +50
            new AccountBalanceSnapshotRow("2000", 200m),      // diff -190 (largest abs)
            new AccountBalanceSnapshotRow("3000", 99.995m),   // diff 0.005 → within tolerance, excluded
        });
        var result = await pass.RunAsync(Tenant, Chart, AsOf, glSnapshot: snapshot);

        Assert.True(result.AccountBalances.Checked);
        Assert.Equal(2, result.AccountBalances.Diffs.Count);
        Assert.Equal("2000", result.AccountBalances.Diffs[0].AccountCode);
        Assert.Equal("1000", result.AccountBalances.Diffs[1].AccountCode);
        Assert.Equal(-190m, result.AccountBalances.Diffs[0].Diff);
    }

    [Fact]
    public async Task Invoice_balance_discrepancy_is_detected_for_in_scope_statuses_only()
    {
        var consistent = Inv(InvoiceStatus.Issued, total: 100m, amountPaid: 30m, balance: 70m);
        var inconsistent = Inv(InvoiceStatus.PartiallyPaid, total: 100m, amountPaid: 40m, balance: 99m);
        var draftIgnored = Inv(InvoiceStatus.Draft, total: 100m, amountPaid: 0m, balance: 5m);

        var pass = NewPass(
            balances: BalancedBooks(out var accounts),
            accounts: accounts,
            invoices: new[] { consistent, inconsistent, draftIgnored });

        var result = await pass.RunAsync(Tenant, Chart, AsOf);

        Assert.Equal(2, result.InvoiceBalances.InvoicesChecked); // Draft excluded
        var discrepancy = Assert.Single(result.InvoiceBalances.Discrepancies);
        Assert.Equal(inconsistent.Id, discrepancy.InvoiceId);
        Assert.Equal(60m, discrepancy.ExpectedBalance);
        Assert.Equal(99m, discrepancy.RecordedBalance);
    }

    // ─────────────────────────── fixtures ───────────────────────────

    private static ErpnextVerificationPass NewPass(
        Dictionary<GLAccountId, decimal> balances,
        IReadOnlyList<GLAccount> accounts,
        IReadOnlyList<AgingRow>? arRows = null,
        IReadOnlyList<ApModels.AgingRow>? apRows = null,
        IReadOnlyList<Invoice>? invoices = null) =>
        new(
            new FakeLedger(balances),
            new FakeResolver(accounts),
            new FakeArAging(arRows ?? Array.Empty<AgingRow>()),
            new FakeApAging(apRows ?? Array.Empty<ApModels.AgingRow>()),
            new FakeInvoices(invoices ?? Array.Empty<Invoice>()));

    private static GLAccount Account(string code, GLAccountType type) =>
        new(GLAccountId.NewId(), code, $"Account {code}", type);

    /// <summary>A trivially balanced two-account ledger for tests focused on other checks.</summary>
    private static Dictionary<GLAccountId, decimal> BalancedBooks(out IReadOnlyList<GLAccount> accounts)
    {
        var asset = Account("1000", GLAccountType.Asset);
        var revenue = Account("4000", GLAccountType.Revenue);
        accounts = new[] { asset, revenue };
        return new() { [asset.Id] = 100m, [revenue.Id] = -100m };
    }

    private static AgingRow ArRow(PartyId customer, AgingBucket bucket, decimal balance) =>
        new(InvoiceId.NewId(), "INV-2026-05-16-01-0001", customer, PropertyId: null,
            IssueDate: AsOf, DueDate: AsOf, DaysPastDue: 0, Total: balance, AmountPaid: 0m,
            Balance: balance, Bucket: bucket);

    private static ApModels.AgingRow ApRow(PartyId vendor, ApModels.AgingBucket bucket, decimal balance) =>
        new(ApModels.BillId.NewId(), "BILL-0001", vendor, PropertyId: null,
            BillDate: AsOf, DueDate: AsOf, DaysPastDue: 0, Total: balance, AmountPaid: 0m,
            Balance: balance, Bucket: bucket);

    private static PartyAgingSnapshotRow Aging(
        PartyId party, decimal current = 0m, decimal days0To30 = 0m, decimal days31To60 = 0m,
        decimal days61To90 = 0m, decimal days90Plus = 0m) =>
        new(party, current, days0To30, days31To60, days61To90, days90Plus);

    private static Invoice Inv(InvoiceStatus status, decimal total, decimal amountPaid, decimal balance) =>
        Invoice.Create(
            tenantId: Tenant, chartId: Chart, invoiceNumber: $"INV-2026-05-16-01-{Guid.NewGuid():N}".Substring(0, 22),
            customerId: PartyId.NewId(), issueDate: AsOf, dueDate: AsOf,
            lines: Array.Empty<InvoiceLine>(), arAccountId: GLAccountId.NewId())
            with
        { Status = status, Total = total, AmountPaid = amountPaid, Balance = balance };

    // Hand fakes — each implements only the member Pass 6 calls; the rest throw to make an
    // accidental new dependency a loud failure rather than a silent empty result.

    private sealed class FakeLedger(IReadOnlyDictionary<GLAccountId, decimal> balances) : IGeneralLedgerReadModel
    {
        public Task<IReadOnlyDictionary<GLAccountId, decimal>> GetAccountBalancesAsOfAsync(
            TenantId tenantId, ChartOfAccountsId chartId, DateOnly asOf, string snapshotMarker,
            CancellationToken cancellationToken = default) => Task.FromResult(balances);
    }

    private sealed class FakeResolver(IEnumerable<GLAccount> accounts) : IAccountResolver
    {
        private readonly Dictionary<GLAccountId, GLAccount> _byId = accounts.ToDictionary(a => a.Id);

        public Task<GLAccount?> GetAsync(GLAccountId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.TryGetValue(id, out var account) ? account : null);

        public Task<IReadOnlyList<GLAccount>> EnumerateForChartAsync(
            ChartOfAccountsId chartId, bool includeInactive = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeArAging(IReadOnlyList<AgingRow> rows) : IArAgingService
    {
        public Task<AgingSummary> GetAgingForChartAsync(ChartOfAccountsId chartId, DateOnly asOf, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgingSummary(asOf, 0m, 0m, 0m, 0m, 0m, 0m, rows));

        public Task<AgingSummary> GetAgingForCustomerAsync(ChartOfAccountsId chartId, PartyId customerId, DateOnly asOf, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgingSummary> GetAgingForPropertyAsync(ChartOfAccountsId chartId, string propertyId, DateOnly asOf, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeApAging(IReadOnlyList<ApModels.AgingRow> rows) : Sunfish.Blocks.FinancialAp.Services.IApAgingService
    {
        public Task<ApModels.AgingSummary> GetAgingForChartAsync(ChartOfAccountsId chartId, DateOnly asOf, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApModels.AgingSummary(asOf, 0m, 0m, 0m, 0m, 0m, 0m, rows));

        public Task<ApModels.AgingSummary> GetAgingForVendorAsync(ChartOfAccountsId chartId, PartyId vendorId, DateOnly asOf, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApModels.AgingSummary> GetAgingForPropertyAsync(ChartOfAccountsId chartId, string propertyId, DateOnly asOf, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeInvoices(IReadOnlyList<Invoice> invoices) : IInvoiceRepository
    {
        public Task<IReadOnlyList<Invoice>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default) =>
            Task.FromResult(invoices);

        public Task UpsertAsync(TenantId tenantId, Invoice invoice, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Invoice?> GetAsync(TenantId tenantId, InvoiceId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Invoice?> GetByNumberAsync(TenantId tenantId, ChartOfAccountsId chartId, string invoiceNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Invoice>> ListByCustomerAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> SoftDeleteAsync(TenantId tenantId, InvoiceId id, PartyId actor, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
