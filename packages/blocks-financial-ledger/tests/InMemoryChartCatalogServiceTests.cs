using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

public sealed class InMemoryChartCatalogServiceTests
{
    [Fact]
    public async Task GetDefault_BeforeRegister_ReturnsNull()
    {
        var sut = new InMemoryChartCatalogService();
        var tenant = new TenantId("acme");

        Assert.Null(await sut.GetDefaultChartIdAsync(tenant));
    }

    [Fact]
    public async Task Register_ThenGetDefault_RoundTrips()
    {
        var sut = new InMemoryChartCatalogService();
        var tenant = new TenantId("acme");
        var chartId = new ChartOfAccountsId("chart-acme-default");

        await sut.RegisterDefaultChartAsync(tenant, chartId);

        Assert.Equal(chartId, await sut.GetDefaultChartIdAsync(tenant));
    }

    [Fact]
    public async Task Register_LastWriteWins()
    {
        var sut = new InMemoryChartCatalogService();
        var tenant = new TenantId("acme");
        var first = new ChartOfAccountsId("chart-acme-v1");
        var second = new ChartOfAccountsId("chart-acme-v2");

        await sut.RegisterDefaultChartAsync(tenant, first);
        await sut.RegisterDefaultChartAsync(tenant, second);

        Assert.Equal(second, await sut.GetDefaultChartIdAsync(tenant));
    }

    [Fact]
    public async Task GetDefault_DifferentTenants_AreIndependent()
    {
        var sut = new InMemoryChartCatalogService();
        var alice = new TenantId("alice");
        var bob = new TenantId("bob");
        var aliceChart = new ChartOfAccountsId("chart-alice");
        var bobChart = new ChartOfAccountsId("chart-bob");

        await sut.RegisterDefaultChartAsync(alice, aliceChart);
        await sut.RegisterDefaultChartAsync(bob, bobChart);

        Assert.Equal(aliceChart, await sut.GetDefaultChartIdAsync(alice));
        Assert.Equal(bobChart,   await sut.GetDefaultChartIdAsync(bob));
    }

    [Fact]
    public async Task GetDefault_OnDefaultTenantId_ReturnsNull()
    {
        var sut = new InMemoryChartCatalogService();
        Assert.Null(await sut.GetDefaultChartIdAsync(default));
    }

    [Fact]
    public async Task Register_OnDefaultTenantId_ThrowsArgumentException()
    {
        var sut = new InMemoryChartCatalogService();
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.RegisterDefaultChartAsync(default, new ChartOfAccountsId("c1")));
    }

    [Fact]
    public async Task Register_OnDefaultChartId_ThrowsArgumentException()
    {
        var sut = new InMemoryChartCatalogService();
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.RegisterDefaultChartAsync(new TenantId("acme"), default));
    }
}
