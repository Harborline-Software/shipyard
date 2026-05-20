using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceRepositoryTests
{
    private static TenantId Tenant() => new("acme");
    private static TenantId OtherTenant() => new("other");

    private static Invoice NewInvoice(
        ChartOfAccountsId chart,
        PartyId customer,
        string number = "INV-001",
        TenantId? tenant = null) =>
        Invoice.Create(
            tenantId: tenant ?? Tenant(),
            chartId: chart,
            invoiceNumber: number,
            customerId: customer,
            issueDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 5, 31),
            lines: new[]
            {
                InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1m, 100m, GLAccountId.NewId()),
            },
            arAccountId: GLAccountId.NewId());

    [Fact]
    public async Task Upsert_RoundtripsViaGet()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(Tenant(), inv);

        var fetched = await repo.GetAsync(Tenant(), inv.Id);
        Assert.NotNull(fetched);
        Assert.Equal(inv.Id, fetched!.Id);
        Assert.Equal(100m, fetched.Total);
    }

    [Fact]
    public async Task GetByNumber_ChartScoped_OnlyMatchesSameChart()
    {
        var repo = new InMemoryInvoiceRepository();
        var chartA = ChartOfAccountsId.NewId();
        var chartB = ChartOfAccountsId.NewId();
        var customer = PartyId.NewId();
        var aInv = NewInvoice(chartA, customer, "INV-001");
        var bInv = NewInvoice(chartB, customer, "INV-001");
        await repo.UpsertAsync(Tenant(), aInv);
        await repo.UpsertAsync(Tenant(), bInv);

        var foundA = await repo.GetByNumberAsync(Tenant(), chartA, "INV-001");
        var foundB = await repo.GetByNumberAsync(Tenant(), chartB, "INV-001");

        Assert.Equal(aInv.Id, foundA?.Id);
        Assert.Equal(bInv.Id, foundB?.Id);
        Assert.NotEqual(foundA?.Id, foundB?.Id);
    }

    [Fact]
    public async Task GetByNumber_TombstonedRow_ReturnsNull()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var inv = NewInvoice(chart, PartyId.NewId(), "INV-DEAD");
        await repo.UpsertAsync(Tenant(), inv);
        await repo.SoftDeleteAsync(Tenant(), inv.Id, PartyId.NewId(), "test cleanup");

        Assert.Null(await repo.GetByNumberAsync(Tenant(), chart, "INV-DEAD"));
        Assert.Null(await repo.GetAsync(Tenant(), inv.Id));
    }

    [Fact]
    public async Task ListByChart_ExcludesOtherCharts_AndTombstones()
    {
        var repo = new InMemoryInvoiceRepository();
        var chartA = ChartOfAccountsId.NewId();
        var chartB = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(Tenant(), NewInvoice(chartA, PartyId.NewId(), "A1"));
        await repo.UpsertAsync(Tenant(), NewInvoice(chartA, PartyId.NewId(), "A2"));
        await repo.UpsertAsync(Tenant(), NewInvoice(chartB, PartyId.NewId(), "B1"));

        var aDead = NewInvoice(chartA, PartyId.NewId(), "A-DEAD");
        await repo.UpsertAsync(Tenant(), aDead);
        await repo.SoftDeleteAsync(Tenant(), aDead.Id, PartyId.NewId(), null);

        var aRows = await repo.ListByChartAsync(Tenant(), chartA);
        Assert.Equal(2, aRows.Count);
        Assert.DoesNotContain(aRows, x => x.Id == aDead.Id);
    }

    [Fact]
    public async Task ListByCustomer_FiltersToOneCustomer()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        await repo.UpsertAsync(Tenant(), NewInvoice(chart, alice, "A1"));
        await repo.UpsertAsync(Tenant(), NewInvoice(chart, alice, "A2"));
        await repo.UpsertAsync(Tenant(), NewInvoice(chart, bob, "B1"));

        var aliceInvoices = await repo.ListByCustomerAsync(Tenant(), chart, alice);
        Assert.Equal(2, aliceInvoices.Count);
        Assert.All(aliceInvoices, i => Assert.Equal(alice, i.CustomerId));
    }

    [Fact]
    public async Task SoftDelete_OnUnknownId_ReturnsFalse()
    {
        var repo = new InMemoryInvoiceRepository();
        var result = await repo.SoftDeleteAsync(Tenant(), InvoiceId.NewId(), PartyId.NewId(), null);
        Assert.False(result);
    }

    [Fact]
    public async Task SoftDelete_IsIdempotent()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(Tenant(), inv);

        Assert.True(await repo.SoftDeleteAsync(Tenant(), inv.Id, PartyId.NewId(), "first"));
        Assert.True(await repo.SoftDeleteAsync(Tenant(), inv.Id, PartyId.NewId(), "second"));
    }

    [Fact]
    public async Task Upsert_OnTombstonedInvoice_Throws()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(Tenant(), inv);
        await repo.SoftDeleteAsync(Tenant(), inv.Id, PartyId.NewId(), null);

        var bumped = inv with { Notes = "should not save" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpsertAsync(Tenant(), bumped));
    }

    // ── Cohort-2 PR 0a tenant-keying tests (pattern-009-tenant-keying-retrofit candidate) ──

    [Fact]
    public async Task GetAsync_CrossTenant_ReturnsNull()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var inv = NewInvoice(chart, PartyId.NewId(), "INV-A", tenant: Tenant());
        await repo.UpsertAsync(Tenant(), inv);

        // OtherTenant reads with the same id → uniform null (no diagnostic leak).
        var crossTenantRead = await repo.GetAsync(OtherTenant(), inv.Id);
        Assert.Null(crossTenantRead);
    }

    [Fact]
    public async Task ListByChartAsync_FiltersByTenant()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var customer = PartyId.NewId();

        await repo.UpsertAsync(Tenant(), NewInvoice(chart, customer, "A-1", tenant: Tenant()));
        await repo.UpsertAsync(Tenant(), NewInvoice(chart, customer, "A-2", tenant: Tenant()));
        await repo.UpsertAsync(OtherTenant(), NewInvoice(chart, customer, "B-1", tenant: OtherTenant()));

        var tenantARows = await repo.ListByChartAsync(Tenant(), chart);
        Assert.Equal(2, tenantARows.Count);
        Assert.All(tenantARows, i => Assert.Equal(Tenant(), i.TenantId));

        var tenantBRows = await repo.ListByChartAsync(OtherTenant(), chart);
        Assert.Single(tenantBRows);
        Assert.All(tenantBRows, i => Assert.Equal(OtherTenant(), i.TenantId));
    }

    [Fact]
    public async Task ListByCustomerAsync_FiltersByTenant()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();
        var customer = PartyId.NewId();

        await repo.UpsertAsync(Tenant(), NewInvoice(chart, customer, "A-1", tenant: Tenant()));
        await repo.UpsertAsync(OtherTenant(), NewInvoice(chart, customer, "B-1", tenant: OtherTenant()));

        var tenantARows = await repo.ListByCustomerAsync(Tenant(), chart, customer);
        Assert.Single(tenantARows);
        Assert.Equal(Tenant(), tenantARows[0].TenantId);
    }

    [Fact]
    public async Task GetByNumberAsync_CrossTenantSameNumber_ReturnsNull()
    {
        var repo = new InMemoryInvoiceRepository();
        var chart = ChartOfAccountsId.NewId();

        await repo.UpsertAsync(Tenant(), NewInvoice(chart, PartyId.NewId(), "INV-COLLIDE", tenant: Tenant()));
        await repo.UpsertAsync(OtherTenant(), NewInvoice(chart, PartyId.NewId(), "INV-COLLIDE", tenant: OtherTenant()));

        var tenantAHit = await repo.GetByNumberAsync(Tenant(), chart, "INV-COLLIDE");
        var tenantBHit = await repo.GetByNumberAsync(OtherTenant(), chart, "INV-COLLIDE");

        Assert.NotNull(tenantAHit);
        Assert.NotNull(tenantBHit);
        Assert.Equal(Tenant(), tenantAHit!.TenantId);
        Assert.Equal(OtherTenant(), tenantBHit!.TenantId);
        Assert.NotEqual(tenantAHit.Id, tenantBHit.Id);
    }

    [Fact]
    public async Task UpsertAsync_TenantMismatch_ThrowsArgumentException()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId(), tenant: Tenant());

        await Assert.ThrowsAsync<ArgumentException>(() => repo.UpsertAsync(OtherTenant(), inv));
    }

    [Fact]
    public async Task SoftDeleteAsync_CrossTenant_ReturnsFalse()
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice(ChartOfAccountsId.NewId(), PartyId.NewId(), tenant: Tenant());
        await repo.UpsertAsync(Tenant(), inv);

        var crossDelete = await repo.SoftDeleteAsync(OtherTenant(), inv.Id, PartyId.NewId(), "should not delete");
        Assert.False(crossDelete);

        // Verify the row survived.
        Assert.NotNull(await repo.GetAsync(Tenant(), inv.Id));
    }

    [Fact]
    public void Invoice_ImplementsIMustHaveTenant()
    {
        Assert.True(typeof(Sunfish.Foundation.MultiTenancy.IMustHaveTenant).IsAssignableFrom(typeof(Invoice)));
    }

    [Fact]
    public void IInvoiceRepository_ImplementsITenantScopedRepositoryMarker()
    {
        var marker = typeof(Sunfish.Foundation.Persistence.ITenantScopedRepository<Invoice, InvoiceId>);
        Assert.True(marker.IsAssignableFrom(typeof(IInvoiceRepository)));
    }
}
