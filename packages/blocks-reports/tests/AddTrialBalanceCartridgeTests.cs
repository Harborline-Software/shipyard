using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Blocks.Reports.Cartridges.TrialBalance;
using Sunfish.Blocks.Reports.DependencyInjection;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// W#72 PR 2 — DI wiring smoke tests for
/// <see cref="TrialBalanceServiceCollectionExtensions.AddTrialBalanceCartridge"/>.
/// Verifies that after calling <c>AddBlocksReportsSubstrate()</c> +
/// <c>AddTrialBalanceCartridge()</c> + upstream-cluster registrations +
/// <c>UseBlocksReports()</c>, the registry contains
/// <see cref="ReportKind.TrialBalance"/> and the runner can dispatch it.
/// </summary>
public sealed class AddTrialBalanceCartridgeTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Upstream cluster stubs — manual registration because no DI
        // convenience extension exists for IAccountResolver / IGeneralLedgerReadModel
        // in the ledger package (only IAccountingService is wrapped). This matches
        // the test-environment pattern used in TrialBalanceCartridgeTests.
        services.AddSingleton<IChartRepository>(new InMemoryChartRepository());
        services.AddSingleton<IFiscalPeriodRepository>(new InMemoryFiscalPeriodRepository());
        services.AddSingleton<IJournalStore, InMemoryJournalStore>();
        services.AddSingleton<IAccountResolver>(new InMemoryAccountResolver());
        services.AddSingleton<IGeneralLedgerReadModel>(sp =>
            new InMemoryGeneralLedgerReadModel(sp.GetRequiredService<IJournalStore>()));

        services
            .AddBlocksReportsSubstrate()
            .AddTrialBalanceCartridge();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddTrialBalanceCartridge_RegistersCartridgeWithRegistry()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var registry = sp.GetRequiredService<ReportCartridgeRegistry>();
        Assert.Contains(ReportKind.TrialBalance, registry.RegisteredKinds);
    }

    [Fact]
    public void AddTrialBalanceCartridge_RunnerResolvesTrialBalanceCartridge()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        // Smoke: resolving the typed cartridge from DI does not throw.
        var cartridge = sp.GetRequiredService<IReportCartridge<TrialBalanceParameters, TrialBalanceResult>>();
        Assert.NotNull(cartridge);
        Assert.Equal(ReportKind.TrialBalance, cartridge.Kind);
    }

    [Fact]
    public void AddTrialBalanceCartridge_IReportRunnerIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var runner = sp.GetRequiredService<IReportRunner>();
        Assert.NotNull(runner);
    }
}
