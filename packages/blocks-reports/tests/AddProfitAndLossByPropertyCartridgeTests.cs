using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.Reports.Cartridges.ProfitAndLossByProperty;
using Sunfish.Blocks.Reports.DependencyInjection;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// W#72 PR 5 — DI wiring smoke tests for
/// <see cref="ProfitAndLossByPropertyServiceCollectionExtensions.AddProfitAndLossByPropertyCartridge"/>.
/// Verifies that after calling
/// <c>AddBlocksReportsSubstrate() + AddProfitAndLossByPropertyCartridge() + UseBlocksReports()</c>
/// the registry contains <see cref="ReportKind.ProfitAndLossByProperty"/> and the
/// runner is resolvable.
/// </summary>
public sealed class AddProfitAndLossByPropertyCartridgeTests
{
    private static System.IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Upstream cluster stubs — the cartridge depends on IJournalStore
        // + IAccountResolver, both provided by blocks-financial-ledger.
        services.AddSingleton<IJournalStore, InMemoryJournalStore>();
        services.AddSingleton<IAccountResolver>(new InMemoryAccountResolver());

        services
            .AddBlocksReportsSubstrate()
            .AddProfitAndLossByPropertyCartridge();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddProfitAndLossByPropertyCartridge_RegistersCartridgeWithRegistry()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var registry = sp.GetRequiredService<ReportCartridgeRegistry>();
        Assert.Contains(ReportKind.ProfitAndLossByProperty, registry.RegisteredKinds);
    }

    [Fact]
    public void AddProfitAndLossByPropertyCartridge_CartridgeIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var cartridge = sp.GetRequiredService<
            IReportCartridge<ProfitAndLossByPropertyParameters, ProfitAndLossByPropertyResult>>();
        Assert.NotNull(cartridge);
        Assert.Equal(ReportKind.ProfitAndLossByProperty, cartridge.Kind);
    }

    [Fact]
    public void AddProfitAndLossByPropertyCartridge_IReportRunnerIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var runner = sp.GetRequiredService<IReportRunner>();
        Assert.NotNull(runner);
    }
}
