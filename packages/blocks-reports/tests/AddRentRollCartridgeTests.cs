using System;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Blocks.Reports.Cartridges.RentRoll;
using Sunfish.Blocks.Reports.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// W#72 PR 6 — DI wiring smoke tests for
/// <see cref="RentRollServiceCollectionExtensions.AddRentRollCartridge"/>.
/// Wires upstream cluster stubs directly (matching the PR 3 pattern from
/// <see cref="AddArAgingSummaryCartridgeTests"/>).
/// </summary>
public sealed class AddRentRollCartridgeTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // Wire upstream cluster stubs directly — avoids pulling in the full
        // AspNetCore framework reference from blocks-leases in the test host.
        services.AddSingleton<ILeaseService>(new InMemoryLeaseService());
        services.AddSingleton<IInvoiceRepository>(new InMemoryInvoiceRepository());
        services.AddSingleton<ITenantContext>(new StubTenantContext(new TenantId("tenant-di-smoke")));
        services.AddSingleton<IArAgingService, ArAgingService>();
        services.AddSingleton<IPartyReadModel>(new InMemoryPartyRepository());

        services
            .AddBlocksReportsSubstrate()
            .AddRentRollCartridge();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddRentRollCartridge_RegistersCartridgeWithRegistry()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var registry = sp.GetRequiredService<ReportCartridgeRegistry>();
        Assert.Contains(ReportKind.RentRoll, registry.RegisteredKinds);
    }

    [Fact]
    public void AddRentRollCartridge_CartridgeIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var cartridge = sp.GetRequiredService<
            IReportCartridge<RentRollParameters, RentRollResult>>();
        Assert.NotNull(cartridge);
        Assert.Equal(ReportKind.RentRoll, cartridge.Kind);
    }

    [Fact]
    public void AddRentRollCartridge_IReportRunnerIsResolvable()
    {
        var sp = BuildProvider();
        sp.UseBlocksReports();

        var runner = sp.GetRequiredService<IReportRunner>();
        Assert.NotNull(runner);
    }
}
