using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Blocks.Reports.Cartridges.ArAgingSummary;
using Sunfish.Blocks.Reports.DependencyInjection;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// W#72 PR 3 — DI wiring smoke tests for
/// <see cref="ArAgingSummaryServiceCollectionExtensions.AddArAgingSummaryCartridge"/>.
/// Verifies that after calling
/// <c>AddBlocksReportsSubstrate() + AddArAgingSummaryCartridge() + UseBlocksReports()</c>
/// the registry contains <see cref="ReportKind.ArAgingSummary"/> and the
/// runner is resolvable.
/// </summary>
public sealed class AddArAgingSummaryCartridgeTests
{
    private static System.IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Wire upstream cluster stubs directly — no AddBlocksFinancialAr
        // convenience extension needed for test purposes; the cartridge
        // only depends on the IArAgingService + IPartyReadModel interfaces.
        services.AddSingleton<IInvoiceRepository>(new InMemoryInvoiceRepository());
        services.AddSingleton<IArAgingService, ArAgingService>();
        services.AddSingleton<IPartyReadModel>(new InMemoryPartyRepository());

        services
            .AddBlocksReportsSubstrate()
            .AddArAgingSummaryCartridge();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddArAgingSummaryCartridge_RegistersCartridgeWithRegistry()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var registry = sp.GetRequiredService<ReportCartridgeRegistry>();
        Assert.Contains(ReportKind.ArAgingSummary, registry.RegisteredKinds);
    }

    [Fact]
    public void AddArAgingSummaryCartridge_CartridgeIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var cartridge = sp.GetRequiredService<
            IReportCartridge<ArAgingSummaryParameters, ArAgingSummaryResult>>();
        Assert.NotNull(cartridge);
        Assert.Equal(ReportKind.ArAgingSummary, cartridge.Kind);
    }

    [Fact]
    public void AddArAgingSummaryCartridge_IReportRunnerIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var runner = sp.GetRequiredService<IReportRunner>();
        Assert.NotNull(runner);
    }
}
