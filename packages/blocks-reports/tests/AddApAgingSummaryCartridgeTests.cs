using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Blocks.Reports.Cartridges.ApAgingSummary;
using Sunfish.Blocks.Reports.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// DI wiring smoke tests for
/// <see cref="ApAgingSummaryServiceCollectionExtensions.AddApAgingSummaryCartridge"/>.
/// Verifies that after calling
/// <c>AddBlocksReportsSubstrate() + AddApAgingSummaryCartridge() + UseBlocksReports()</c>
/// the registry contains <see cref="ReportKind.ApAgingSummary"/> and the
/// runner is resolvable.
/// Mirrors <see cref="AddArAgingSummaryCartridgeTests"/> with AP semantics.
/// </summary>
public sealed class AddApAgingSummaryCartridgeTests
{
    private static System.IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Wire upstream cluster stubs directly — no AddBlocksFinancialAp
        // convenience extension needed for test purposes; the cartridge
        // only depends on the IApAgingService + IPartyReadModel interfaces.
        services.AddSingleton<IBillRepository>(new InMemoryBillRepository());
        services.AddSingleton<ITenantContext>(new StubTenantContext(new TenantId("tenant-ap-di-smoke")));
        services.AddSingleton<IApAgingService, ApAgingService>();
        services.AddSingleton<IPartyReadModel>(new InMemoryPartyRepository());

        services
            .AddBlocksReportsSubstrate()
            .AddApAgingSummaryCartridge();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddApAgingSummaryCartridge_RegistersCartridgeWithRegistry()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var registry = sp.GetRequiredService<ReportCartridgeRegistry>();
        Assert.Contains(ReportKind.ApAgingSummary, registry.RegisteredKinds);
    }

    [Fact]
    public void AddApAgingSummaryCartridge_CartridgeIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var cartridge = sp.GetRequiredService<
            IReportCartridge<ApAgingSummaryParameters, ApAgingSummaryResult>>();
        Assert.NotNull(cartridge);
        Assert.Equal(ReportKind.ApAgingSummary, cartridge.Kind);
    }

    [Fact]
    public void AddApAgingSummaryCartridge_IReportRunnerIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var runner = sp.GetRequiredService<IReportRunner>();
        Assert.NotNull(runner);
    }
}
