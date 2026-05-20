using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class BillRepositoryTests
{
    private static TenantId Tenant() => new("acme");
    private static TenantId OtherTenant() => new("other");

    private static Bill NewBill(
        ChartOfAccountsId chart,
        PartyId vendor,
        string number = "VND-001",
        string? propertyId = null,
        string? externalRef = null,
        TenantId? tenant = null) =>
        Bill.Create(
            tenantId: tenant ?? Tenant(),
            chartId: chart,
            billNumber: number,
            vendorId: vendor,
            billDate: new DateOnly(2026, 5, 1),
            dueDate: new DateOnly(2026, 5, 31),
            lines: new[] { BillLine.Create(BillId.NewId(), 1, "x", 1m, 100m, GLAccountId.NewId()) },
            apAccountId: GLAccountId.NewId(),
            propertyId: propertyId,
            externalRef: externalRef);

    [Fact]
    public async Task Upsert_RoundtripsViaGet()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(Tenant(), bill);

        var fetched = await repo.GetAsync(Tenant(), bill.Id);
        Assert.NotNull(fetched);
        Assert.Equal(bill.Id, fetched!.Id);
        Assert.Equal(100m, fetched.Total);
    }

    [Fact]
    public async Task GetByVendorBillNumber_ChartAndVendorScoped()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        var aliceBill = NewBill(chart, alice, number: "INV-001");
        var bobBill = NewBill(chart, bob, number: "INV-001");
        await repo.UpsertAsync(Tenant(), aliceBill);
        await repo.UpsertAsync(Tenant(), bobBill);

        Assert.Equal(aliceBill.Id, (await repo.GetByVendorBillNumberAsync(Tenant(), chart, alice, "INV-001"))?.Id);
        Assert.Equal(bobBill.Id, (await repo.GetByVendorBillNumberAsync(Tenant(), chart, bob, "INV-001"))?.Id);

        // Same number on a different vendor — should NOT match alice's bill.
        var aliceLookupBobNumber = await repo.GetByVendorBillNumberAsync(Tenant(), chart, alice, "INV-001");
        Assert.NotEqual(bobBill.Id, aliceLookupBobNumber?.Id);
    }

    [Fact]
    public async Task GetByExternalRef_ChartScopedAndTombstoneExcluding()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var bill = NewBill(chart, PartyId.NewId(), externalRef: "erpnext:pinv:PINV-0042");
        await repo.UpsertAsync(Tenant(), bill);

        Assert.Equal(bill.Id, (await repo.GetByExternalRefAsync(Tenant(), chart, "erpnext:pinv:PINV-0042"))?.Id);

        await repo.SoftDeleteAsync(Tenant(), bill.Id, PartyId.NewId(), "test");
        Assert.Null(await repo.GetByExternalRefAsync(Tenant(), chart, "erpnext:pinv:PINV-0042"));
    }

    [Fact]
    public async Task ListByChart_ExcludesOtherCharts_AndTombstones()
    {
        var repo = new InMemoryBillRepository();
        var chartA = ChartOfAccountsId.NewId();
        var chartB = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(Tenant(), NewBill(chartA, PartyId.NewId(), "A1"));
        await repo.UpsertAsync(Tenant(), NewBill(chartA, PartyId.NewId(), "A2"));
        await repo.UpsertAsync(Tenant(), NewBill(chartB, PartyId.NewId(), "B1"));

        var aDead = NewBill(chartA, PartyId.NewId(), "A-DEAD");
        await repo.UpsertAsync(Tenant(), aDead);
        await repo.SoftDeleteAsync(Tenant(), aDead.Id, PartyId.NewId(), null);

        var aRows = await repo.ListByChartAsync(Tenant(), chartA);
        Assert.Equal(2, aRows.Count);
    }

    [Fact]
    public async Task ListByVendor_FiltersToOneVendor()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var alice = PartyId.NewId();
        var bob = PartyId.NewId();
        await repo.UpsertAsync(Tenant(), NewBill(chart, alice, "A1"));
        await repo.UpsertAsync(Tenant(), NewBill(chart, alice, "A2"));
        await repo.UpsertAsync(Tenant(), NewBill(chart, bob, "B1"));

        var aliceBills = await repo.ListByVendorAsync(Tenant(), chart, alice);
        Assert.Equal(2, aliceBills.Count);
    }

    [Fact]
    public async Task QueryOpen_OnlyOpenStatuses_OptionalFilters()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var vendor = PartyId.NewId();

        var b1 = NewBill(chart, vendor, "B-RECV", propertyId: "P1");
        b1 = b1 with { Status = BillStatus.Received };
        await repo.UpsertAsync(Tenant(), b1);

        var b2 = NewBill(chart, vendor, "B-APPR", propertyId: "P2");
        b2 = b2 with { Status = BillStatus.Approved };
        await repo.UpsertAsync(Tenant(), b2);

        var b3 = NewBill(chart, vendor, "B-PAID", propertyId: "P1");
        b3 = b3 with { Status = BillStatus.Paid, Balance = 0m };
        await repo.UpsertAsync(Tenant(), b3);

        var b4 = NewBill(chart, vendor, "B-DISP", propertyId: "P1");
        b4 = b4 with { Status = BillStatus.Disputed };
        await repo.UpsertAsync(Tenant(), b4);

        // All open — b1 + b2 (Disputed and Paid excluded).
        var allOpen = await repo.QueryOpenAsync(Tenant(), chart);
        Assert.Equal(2, allOpen.Count);
        Assert.Contains(allOpen, b => b.Id == b1.Id);
        Assert.Contains(allOpen, b => b.Id == b2.Id);

        // Filter by property P1 — only b1.
        var p1Open = await repo.QueryOpenAsync(Tenant(), chart, propertyId: "P1");
        Assert.Single(p1Open);
        Assert.Equal(b1.Id, p1Open[0].Id);

        // Filter by vendor + property combo.
        var combo = await repo.QueryOpenAsync(Tenant(), chart, vendorId: vendor, propertyId: "P2");
        Assert.Single(combo);
        Assert.Equal(b2.Id, combo[0].Id);
    }

    [Fact]
    public async Task SoftDelete_OnUnknownId_ReturnsFalse_IsIdempotent()
    {
        var repo = new InMemoryBillRepository();
        Assert.False(await repo.SoftDeleteAsync(Tenant(), BillId.NewId(), PartyId.NewId(), null));

        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(Tenant(), bill);
        Assert.True(await repo.SoftDeleteAsync(Tenant(), bill.Id, PartyId.NewId(), "first"));
        Assert.True(await repo.SoftDeleteAsync(Tenant(), bill.Id, PartyId.NewId(), "second")); // idempotent
    }

    [Fact]
    public async Task Upsert_OnTombstonedBill_Throws()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId());
        await repo.UpsertAsync(Tenant(), bill);
        await repo.SoftDeleteAsync(Tenant(), bill.Id, PartyId.NewId(), null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.UpsertAsync(Tenant(), bill with { Notes = "should not save" }));
    }

    // ── Cohort-2 PR 0b tenant-keying tests (pattern-009-tenant-keying-retrofit candidate) ──

    [Fact]
    public async Task GetAsync_CrossTenant_ReturnsNull()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId(), tenant: Tenant());
        await repo.UpsertAsync(Tenant(), bill);

        var crossTenantRead = await repo.GetAsync(OtherTenant(), bill.Id);
        Assert.Null(crossTenantRead);
    }

    [Fact]
    public async Task ListByChartAsync_FiltersByTenant()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var vendor = PartyId.NewId();

        await repo.UpsertAsync(Tenant(), NewBill(chart, vendor, "A-1", tenant: Tenant()));
        await repo.UpsertAsync(Tenant(), NewBill(chart, vendor, "A-2", tenant: Tenant()));
        await repo.UpsertAsync(OtherTenant(), NewBill(chart, vendor, "B-1", tenant: OtherTenant()));

        var tenantARows = await repo.ListByChartAsync(Tenant(), chart);
        Assert.Equal(2, tenantARows.Count);
        Assert.All(tenantARows, b => Assert.Equal(Tenant(), b.TenantId));

        var tenantBRows = await repo.ListByChartAsync(OtherTenant(), chart);
        Assert.Single(tenantBRows);
        Assert.All(tenantBRows, b => Assert.Equal(OtherTenant(), b.TenantId));
    }

    [Fact]
    public async Task ListByVendorAsync_FiltersByTenant()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var vendor = PartyId.NewId();

        await repo.UpsertAsync(Tenant(), NewBill(chart, vendor, "A-1", tenant: Tenant()));
        await repo.UpsertAsync(OtherTenant(), NewBill(chart, vendor, "B-1", tenant: OtherTenant()));

        var tenantARows = await repo.ListByVendorAsync(Tenant(), chart, vendor);
        Assert.Single(tenantARows);
        Assert.Equal(Tenant(), tenantARows[0].TenantId);
    }

    [Fact]
    public async Task GetByVendorBillNumberAsync_CrossTenantSameNumber_DoesNotMatch()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        var vendor = PartyId.NewId();

        await repo.UpsertAsync(Tenant(), NewBill(chart, vendor, "BILL-COLLIDE", tenant: Tenant()));
        await repo.UpsertAsync(OtherTenant(), NewBill(chart, vendor, "BILL-COLLIDE", tenant: OtherTenant()));

        var tenantAHit = await repo.GetByVendorBillNumberAsync(Tenant(), chart, vendor, "BILL-COLLIDE");
        var tenantBHit = await repo.GetByVendorBillNumberAsync(OtherTenant(), chart, vendor, "BILL-COLLIDE");

        Assert.NotNull(tenantAHit);
        Assert.NotNull(tenantBHit);
        Assert.Equal(Tenant(), tenantAHit!.TenantId);
        Assert.Equal(OtherTenant(), tenantBHit!.TenantId);
        Assert.NotEqual(tenantAHit.Id, tenantBHit.Id);
    }

    [Fact]
    public async Task GetByExternalRefAsync_CrossTenant_ReturnsNull()
    {
        var repo = new InMemoryBillRepository();
        var chart = ChartOfAccountsId.NewId();
        await repo.UpsertAsync(Tenant(), NewBill(chart, PartyId.NewId(), externalRef: "erpnext:pinv:CROSS", tenant: Tenant()));

        var crossRead = await repo.GetByExternalRefAsync(OtherTenant(), chart, "erpnext:pinv:CROSS");
        Assert.Null(crossRead);
    }

    [Fact]
    public async Task UpsertAsync_TenantMismatch_ThrowsArgumentException()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId(), tenant: Tenant());

        await Assert.ThrowsAsync<ArgumentException>(() => repo.UpsertAsync(OtherTenant(), bill));
    }

    [Fact]
    public async Task SoftDeleteAsync_CrossTenant_ReturnsFalse()
    {
        var repo = new InMemoryBillRepository();
        var bill = NewBill(ChartOfAccountsId.NewId(), PartyId.NewId(), tenant: Tenant());
        await repo.UpsertAsync(Tenant(), bill);

        var crossDelete = await repo.SoftDeleteAsync(OtherTenant(), bill.Id, PartyId.NewId(), "should not delete");
        Assert.False(crossDelete);

        Assert.NotNull(await repo.GetAsync(Tenant(), bill.Id));
    }

    [Fact]
    public void Bill_ImplementsIMustHaveTenant()
    {
        Assert.True(typeof(Sunfish.Foundation.MultiTenancy.IMustHaveTenant).IsAssignableFrom(typeof(Bill)));
    }

    [Fact]
    public void IBillRepository_ImplementsITenantScopedRepositoryMarker()
    {
        var marker = typeof(Sunfish.Foundation.Persistence.ITenantScopedRepository<Bill, BillId>);
        Assert.True(marker.IsAssignableFrom(typeof(IBillRepository)));
    }
}
