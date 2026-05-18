using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Blocks.Reports.Cartridges.TrialBalance;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// W#72 PR 2 — determinism assertions for the Trial Balance cartridge.
/// Mirrors the contract from <see cref="ReportCartridgeDeterminismTests{TCartridge,TParams,TResult}"/>
/// but implements the assertions directly because <see cref="TrialBalanceResult"/>
/// carries <c>IReadOnlyList&lt;TrialBalanceRow&gt;</c> and
/// <c>IReadOnlyList&lt;string&gt;</c> properties whose reference-based
/// equality breaks C# record structural equality. Per-field assertions
/// preserve the invariant without modifying the result type.
/// </summary>
public sealed class TrialBalanceDeterminismTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly TenantId Tenant = new("tenant-det");
    private static readonly PrincipalId Principal = PrincipalId.FromBytes(new byte[32]);

    private static ChartOfAccounts MakeChart()
        => new ChartOfAccounts(Chart, LegalEntityId.NewId(), "Det", "USD", 1, 1, null, true,
            Instant.Now, Instant.Now);

    private static (TrialBalanceCartridge Cartridge, InMemoryJournalStore Journals) Build()
    {
        var charts = new StubChartRepoForDeterminism { Chart = MakeChart() };
        var accounts = new InMemoryAccountResolver(
            new[]
            {
                GLAccount.Create(GLAccountId.NewId(), Chart, "1000", "Cash",
                    GLAccountType.Asset, AccountSubtype.BankAccount, "USD"),
                GLAccount.Create(GLAccountId.NewId(), Chart, "4000", "Revenue",
                    GLAccountType.Revenue, AccountSubtype.OperatingIncome, "USD"),
            });
        var periods = new InMemoryFiscalPeriodRepository();
        var journals = new InMemoryJournalStore();
        var ledger = new InMemoryGeneralLedgerReadModel(journals);
        return (new TrialBalanceCartridge(charts, accounts, periods, ledger), journals);
    }

    private static TrialBalanceParameters Parameters()
        => new TrialBalanceParameters { ChartId = Chart, AsOfDate = new System.DateOnly(2026, 12, 31) };

    private static ReportExecutionContext Context()
        => new ReportExecutionContext(Tenant, "marker:det:1",
            new System.DateTimeOffset(2026, 5, 17, 12, 0, 0, System.TimeSpan.Zero), Principal);

    private static void AssertResultsEqual(TrialBalanceResult r1, TrialBalanceResult r2)
    {
        Assert.Equal(r1.ChartId, r2.ChartId);
        Assert.Equal(r1.AsOf, r2.AsOf);
        Assert.Equal(r1.PeriodId, r2.PeriodId);
        Assert.Equal(r1.TotalDebit, r2.TotalDebit);
        Assert.Equal(r1.TotalCredit, r2.TotalCredit);
        Assert.Equal(r1.IsBalanced, r2.IsBalanced);
        Assert.Equal(r1.IsProvisional, r2.IsProvisional);
        Assert.Equal(r1.Warnings, r2.Warnings, System.StringComparer.Ordinal);
        Assert.Equal(r1.Rows.Count, r2.Rows.Count);
        for (var i = 0; i < r1.Rows.Count; i++)
            Assert.Equal(r1.Rows[i], r2.Rows[i]);  // TrialBalanceRow is a simple record (no collection props)
    }

    [Fact]
    public async Task ExecuteAsync_IsDeterministic_AcrossRepeatedRuns()
    {
        var (sut, _) = Build();
        var ctx = Context();
        var p = Parameters();
        var r1 = await sut.ExecuteAsync(ctx, p);
        var r2 = await sut.ExecuteAsync(ctx, p);
        AssertResultsEqual(r1, r2);
    }

    [Fact]
    public async Task ExecuteAsync_SameMarker_SameResult()
    {
        // Documentation test: two *distinct* contexts that share the same snapshot marker
        // MUST produce equal results (the marker is the sole upstream-state input).
        var (sut, _) = Build();
        var p = Parameters();
        var ctx1 = Context();
        // ctx2 is a fresh instance but carries the same snapshot marker value.
        var ctx2 = new ReportExecutionContext(Tenant, ctx1.SnapshotMarker,
            new System.DateTimeOffset(2026, 5, 17, 12, 0, 0, System.TimeSpan.Zero), Principal);
        var r1 = await sut.ExecuteAsync(ctx1, p);
        var r2 = await sut.ExecuteAsync(ctx2, p);
        AssertResultsEqual(r1, r2);
    }

    private sealed class StubChartRepoForDeterminism : IChartRepository
    {
        public ChartOfAccounts? Chart { get; set; }
        public Task<ChartOfAccounts?> GetAsync(
            ChartOfAccountsId chartId, CancellationToken ct = default)
            => Task.FromResult(Chart);
    }
}
