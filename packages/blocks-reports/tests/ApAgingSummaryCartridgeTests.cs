using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Blocks.Reports.Cartridges.ApAgingSummary;
using Sunfish.Blocks.Reports.Exceptions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// Unit tests for <see cref="ApAgingSummaryCartridge"/>.
/// Each test seeds an <see cref="InMemoryBillRepository"/> through the
/// canonical <see cref="ApAgingService"/> so the full per-bill bucket
/// classification path executes.
/// Mirrors <see cref="ArAgingSummaryCartridgeTests"/> with vendor/bill semantics.
/// </summary>
public sealed class ApAgingSummaryCartridgeTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly TenantId Tenant = new("tenant-ap-aging");
    private static readonly PrincipalId Principal = PrincipalId.FromBytes(new byte[32]);
    private static readonly GLAccountId ExpenseAccount = GLAccountId.NewId();
    private static readonly GLAccountId ApAccount = GLAccountId.NewId();

    // Reference date: 2026-05-17 (mirrors AR counterpart)
    private static readonly DateOnly Today = new(2026, 5, 17);
    private static int _billSeq = 0;

    private static ReportExecutionContext Context(DateOnly? asOf = null)
    {
        var dt = asOf ?? Today;
        var utc = new DateTimeOffset(dt.Year, dt.Month, dt.Day, 12, 0, 0, TimeSpan.Zero);
        return new ReportExecutionContext(Tenant, "marker:ap:1", utc, Principal);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────

    private static (ApAgingSummaryCartridge Cartridge,
                    InMemoryBillRepository Bills,
                    InMemoryPartyRepository Parties)
        Build()
    {
        var bills = new InMemoryBillRepository();
        var aging = new ApAgingService(new StubTenantContext(Tenant), bills);
        var parties = new InMemoryPartyRepository();
        var cartridge = new ApAgingSummaryCartridge(aging, parties);
        return (cartridge, bills, parties);
    }

    private static Bill MakeReceivedBill(
        PartyId vendorId,
        DateOnly dueDate,
        decimal amount,
        string? propertyId = null)
    {
        var seq = System.Threading.Interlocked.Increment(ref _billSeq);
        var billNumber = $"VND-2026-{seq:D4}";
        var billId = BillId.NewId();
        var line = BillLine.Create(
            billId: billId,
            lineNumber: 1,
            description: "Services",
            quantity: 1m,
            unitPrice: amount,
            debitAccountId: ExpenseAccount,
            propertyId: propertyId);

        var bill = Bill.Create(
            tenantId: Tenant,
            chartId: Chart,
            billNumber: billNumber,
            vendorId: vendorId,
            billDate: dueDate.AddDays(-30),
            dueDate: dueDate,
            lines: new[] { line },
            apAccountId: ApAccount,
            propertyId: propertyId);

        // Promote to Received (open) so the aging service classifies it.
        return bill with { Status = BillStatus.Received, Balance = amount };
    }

    private static async Task<PartyId> SeedPartyAsync(InMemoryPartyRepository parties, string name)
    {
        var party = await parties.CreateAsync(Tenant, PartyKind.Person, name, PartyId.NewId());
        return party.Id;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Edge case — empty
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_EmptyChart_ReturnsZeroRowsAndZeroTotals()
    {
        var (sut, _, _) = Build();
        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Empty(result.ByVendor);
        Assert.Empty(result.ByProperty);
        Assert.Empty(result.TopOverdue);
        Assert.Equal(0m, result.Totals.Current);
        Assert.Equal(0m, result.Totals.Days0To30);
        Assert.Equal(0m, result.Totals.Days31To60);
        Assert.Equal(0m, result.Totals.Days61To90);
        Assert.Equal(0m, result.Totals.Days90Plus);
        Assert.Equal(0m, result.Totals.TotalOpen);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Edge case — single record
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_SingleBillCurrent_AppearsInCurrentBucket()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        // Due tomorrow — still current.
        var bill = MakeReceivedBill(vendor, Today.AddDays(1), 100m);
        await bills.UpsertAsync(Tenant, bill);

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Single(result.ByVendor);
        Assert.Equal(100m, result.ByVendor[0].Current);
        Assert.Equal(0m, result.ByVendor[0].Days90Plus);
        Assert.Equal(100m, result.Totals.Current);
    }

    [Fact]
    public async Task ApAgingSummary_SingleBill90PlusDays_AppearsInTopOverdue()
    {
        var (sut, bills, parties) = Build();
        var vendorId = await SeedPartyAsync(parties, "Acme Plumbing");

        // 100 days past due.
        var bill = MakeReceivedBill(vendorId, Today.AddDays(-100), 500m);
        await bills.UpsertAsync(Tenant, bill);

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart, TopOverdueN = 5 });

        Assert.Single(result.TopOverdue);
        Assert.Equal(vendorId, result.TopOverdue[0].VendorId);
        Assert.Equal("Acme Plumbing", result.TopOverdue[0].VendorName);
        Assert.Equal(500m, result.TopOverdue[0].Days90PlusBalance);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Vendor rollup
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_MultipleBillsSameVendor_AggregatedInOneRow()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 100m));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(10), 200m));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(-10), 50m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Single(result.ByVendor);
        Assert.Equal(300m, result.ByVendor[0].Current); // 100 + 200 current
        Assert.Equal(50m, result.ByVendor[0].Days0To30); // 10 days past due
        Assert.Equal(350m, result.ByVendor[0].TotalOpen);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Property rollup
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_MultipleBillsSameProperty_AggregatedInOneRow()
    {
        var (sut, bills, _) = Build();
        var v1 = PartyId.NewId();
        var v2 = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(v1, Today.AddDays(5), 100m, "prop-A"));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(v2, Today.AddDays(5), 200m, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        var propRow = result.ByProperty.Single(r => r.GroupKey == "prop-A");
        Assert.Equal(300m, propRow.Current);
    }

    [Fact]
    public async Task ApAgingSummary_BillWithNullPropertyId_RolledIntoUnassigned()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 75m, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        var unassigned = result.ByProperty.Single(r => r.GroupKey == "Unassigned");
        Assert.Equal(75m, unassigned.Current);
    }

    [Fact]
    public async Task ApAgingSummary_UnassignedSortsLast()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 50m, "prop-Z"));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 50m, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Equal("Unassigned", result.ByProperty.Last().GroupKey);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Filters
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_VendorIdsFilter_OmitsOtherVendors()
    {
        var (sut, bills, _) = Build();
        var included = PartyId.NewId();
        var excluded = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(included, Today.AddDays(5), 100m));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(excluded, Today.AddDays(5), 200m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters
            {
                ChartId = Chart,
                VendorIds = new[] { included },
            });

        Assert.Single(result.ByVendor);
        Assert.Equal(included.Value, result.ByVendor[0].GroupKey);
        Assert.Equal(100m, result.Totals.TotalOpen);
    }

    [Fact]
    public async Task ApAgingSummary_PropertyIdsFilter_OmitsOtherProperties()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 100m, "prop-A"));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 200m, "prop-B"));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters
            {
                ChartId = Chart,
                PropertyIds = new[] { "prop-A" },
            });

        Assert.Single(result.ByProperty);
        Assert.Equal("prop-A", result.ByProperty[0].GroupKey);
        Assert.Equal(100m, result.Totals.TotalOpen);
    }

    // When the property filter is active, bills with null PropertyId (Unassigned)
    // are excluded from the filtered view.
    [Fact]
    public async Task ApAgingSummary_PropertyIdsFilter_ExcludesUnassigned()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 100m, "prop-A"));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 50m, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters
            {
                ChartId = Chart,
                PropertyIds = new[] { "prop-A" },
            });

        Assert.DoesNotContain(result.ByProperty, r => r.GroupKey == "Unassigned");
        Assert.Equal(100m, result.Totals.TotalOpen);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Parameter validation
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_TopOverdueNNegative_ThrowsValidationException()
    {
        var (sut, _, _) = Build();
        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(),
                new ApAgingSummaryParameters { ChartId = Chart, TopOverdueN = -1 }));
    }

    [Fact]
    public async Task ApAgingSummary_TopOverdueNOverCap_ThrowsValidationException()
    {
        var (sut, _, _) = Build();
        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(),
                new ApAgingSummaryParameters { ChartId = Chart, TopOverdueN = 101 }));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Top-overdue behaviour
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_TopOverdue_OrderedDescendingBy90Plus()
    {
        var (sut, bills, _) = Build();
        var small = PartyId.NewId();
        var large = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(small, Today.AddDays(-100), 100m));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(large, Today.AddDays(-100), 500m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart, TopOverdueN = 10 });

        Assert.Equal(2, result.TopOverdue.Count);
        Assert.Equal(500m, result.TopOverdue[0].Days90PlusBalance);
        Assert.Equal(100m, result.TopOverdue[1].Days90PlusBalance);
    }

    [Fact]
    public async Task ApAgingSummary_TopOverdueN_RespectsCap()
    {
        var (sut, bills, _) = Build();
        for (var i = 0; i < 5; i++)
        {
            var v = PartyId.NewId();
            await bills.UpsertAsync(Tenant, MakeReceivedBill(v, Today.AddDays(-100), 100m));
        }

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart, TopOverdueN = 3 });

        Assert.Equal(3, result.TopOverdue.Count);
    }

    [Fact]
    public async Task ApAgingSummary_TopOverdueN_ZeroReturnsEmpty()
    {
        var (sut, bills, _) = Build();
        var v = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(v, Today.AddDays(-100), 100m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart, TopOverdueN = 0 });

        Assert.Empty(result.TopOverdue);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Totals consistency
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_Totals_EqualSumOfByVendorRows()
    {
        var (sut, bills, _) = Build();
        var v1 = PartyId.NewId();
        var v2 = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(v1, Today.AddDays(5), 100m));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(v1, Today.AddDays(-10), 50m));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(v2, Today.AddDays(-50), 200m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        var summedFromVendors = result.ByVendor.Sum(r => r.TotalOpen);
        Assert.Equal(summedFromVendors, result.Totals.TotalOpen);
    }

    [Fact]
    public async Task ApAgingSummary_Totals_EqualSumOfByPropertyRows()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(5), 100m, "prop-A"));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(-10), 50m, "prop-B"));
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, Today.AddDays(-50), 200m, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        var summedFromProperty = result.ByProperty.Sum(r => r.TotalOpen);
        Assert.Equal(summedFromProperty, result.Totals.TotalOpen);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Tenant isolation — different tenant's bills must not appear
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_TenantIsolation_OtherTenantBillsExcluded()
    {
        var otherTenant = new TenantId("other-tenant-ap");
        var ownBills = new InMemoryBillRepository();
        var aging = new ApAgingService(new StubTenantContext(Tenant), ownBills);
        var cartridge = new ApAgingSummaryCartridge(aging, new InMemoryPartyRepository());

        var myVendor = PartyId.NewId();
        var theirVendor = PartyId.NewId();
        var myBill = MakeReceivedBill(myVendor, Today.AddDays(5), 100m);
        // Seed other tenant's bill into the SAME repo with a different TenantId
        var otherBill = myBill with
        {
            Id = BillId.NewId(),
            TenantId = otherTenant,
            VendorId = theirVendor,
        };
        await ownBills.UpsertAsync(Tenant, myBill);
        await ownBills.UpsertAsync(otherTenant, otherBill);

        var result = await cartridge.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        // Should only see the bill scoped to Tenant, not the other tenant's bill.
        Assert.Single(result.ByVendor);
        Assert.Equal(myVendor.Value, result.ByVendor[0].GroupKey);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Party name resolution
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_VendorName_ResolvedFromPartyReadModel()
    {
        var (sut, bills, parties) = Build();
        var vendorId = await SeedPartyAsync(parties, "Harbor Plumbers LLC");
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendorId, Today.AddDays(5), 100m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Equal("Harbor Plumbers LLC", result.ByVendor.Single().GroupLabel);
    }

    [Fact]
    public async Task ApAgingSummary_UnknownVendor_FallsBackToPartyIdValue()
    {
        var (sut, bills, _) = Build();
        var vendorId = PartyId.NewId();
        // Do NOT seed a Party record — resolution should degrade gracefully.
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendorId, Today.AddDays(5), 100m));

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Equal(vendorId.Value, result.ByVendor.Single().GroupLabel);
    }

    // ──────────────────────────────────────────────────────────────────
    //  AsOfDate wiring
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_ExplicitAsOfDate_UsedForBucketClassification()
    {
        var (sut, bills, _) = Build();
        var vendor = PartyId.NewId();
        // Due 40 days before the explicit as-of — should appear in Days31To60.
        var explicitAsOf = new DateOnly(2026, 6, 1);
        var dueDate = explicitAsOf.AddDays(-40);
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, dueDate, 300m));

        var result = await sut.ExecuteAsync(Context(explicitAsOf),
            new ApAgingSummaryParameters { ChartId = Chart, AsOfDate = explicitAsOf });

        Assert.Equal(explicitAsOf, result.AsOf);
        Assert.Equal(300m, result.ByVendor.Single().Days31To60);
    }

    [Fact]
    public async Task ApAgingSummary_NullAsOfDate_DefaultsToContextDate()
    {
        var (sut, _, _) = Build();
        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        Assert.Equal(Today, result.AsOf);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Bucket boundary correctness — mirrors AR test for completeness
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApAgingSummary_AllBuckets_PopulatedCorrectly()
    {
        var (sut, bills, _) = Build();
        var asOf = Today;
        var vendor = PartyId.NewId();
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, asOf.AddDays(5),   100m)); // Current
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, asOf.AddDays(-15), 200m)); // 0-30
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, asOf.AddDays(-45), 300m)); // 31-60
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, asOf.AddDays(-75), 400m)); // 61-90
        await bills.UpsertAsync(Tenant, MakeReceivedBill(vendor, asOf.AddDays(-120), 500m)); // 90+

        var result = await sut.ExecuteAsync(Context(),
            new ApAgingSummaryParameters { ChartId = Chart });

        var row = result.ByVendor.Single();
        Assert.Equal(100m, row.Current);
        Assert.Equal(200m, row.Days0To30);
        Assert.Equal(300m, row.Days31To60);
        Assert.Equal(400m, row.Days61To90);
        Assert.Equal(500m, row.Days90Plus);
        Assert.Equal(1500m, row.TotalOpen);
        Assert.Equal(1500m, result.Totals.TotalOpen);
    }
}
