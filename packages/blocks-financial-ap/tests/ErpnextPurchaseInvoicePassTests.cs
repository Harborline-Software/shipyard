using Sunfish.Blocks.FinancialAp.Migration;
using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Census;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

/// <summary>
/// Tests for <see cref="ErpnextPurchaseInvoicePass"/> — the A4.2 orchestration pass
/// over the converged <see cref="IErpnextPurchaseInvoiceImporter"/> DU. Asserts census
/// conservation (ADR 0100 C2), reject-not-drop on unresolved supplier, run-twice
/// idempotency (C1/C7), and tenant-first threading (C3).
/// </summary>
public class ErpnextPurchaseInvoicePassTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static GLAccountId Account() => GLAccountId.NewId();

    private sealed record Sut(ErpnextPurchaseInvoicePass Pass, InMemoryBillRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryBillRepository();
        var importer = new ErpnextPurchaseInvoiceImporter(repo);
        return new Sut(new ErpnextPurchaseInvoicePass(importer), repo);
    }

    private static ErpnextPurchaseInvoiceSource Source(
        string name,
        string modified = "2026-05-17 12:00:00",
        string status = "Submitted",
        decimal grandTotal = 1000m,
        decimal outstanding = 1000m) =>
        new(
            Name: name,
            Modified: modified,
            Supplier: "SUPP-" + name,
            BillNo: "BILL-" + name,
            PostingDate: new DateOnly(2026, 5, 17),
            DueDate: new DateOnly(2026, 6, 17),
            BillDate: new DateOnly(2026, 5, 16),
            Currency: "USD",
            Items: new[] { new ErpnextPurchaseInvoiceItem("Office supplies", 10m, 100m, 1000m) },
            Status: status,
            GrandTotal: grandTotal,
            OutstandingAmount: outstanding);

    private static PartyId? AlwaysResolve(ErpnextPurchaseInvoiceSource _) => PartyId.NewId();

    // ── Happy path + conservation ──────────────────────────────────────

    [Fact]
    public async Task Run_AllResolvable_ImportsAll_CensusConserves()
    {
        var sut = NewSut();
        var sources = new[] { Source("PINV-1"), Source("PINV-2"), Source("PINV-3") };

        var result = await sut.Pass.RunAsync(
            Tenant(), sources, Chart(), Account(), Account(), AlwaysResolve);

        Assert.Equal(3, result.Imported);
        Assert.Equal(3, result.Census.Inserted);
        Assert.Equal(3, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(sources.Length));
        Assert.True(result.AllAccepted);
        Assert.Empty(result.Rejects);
    }

    [Fact]
    public async Task Run_EmptySet_ConservesZero()
    {
        var sut = NewSut();

        var result = await sut.Pass.RunAsync(
            Tenant(), Array.Empty<ErpnextPurchaseInvoiceSource>(), Chart(), Account(), Account(), AlwaysResolve);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
    }

    // ── Reject-not-drop ────────────────────────────────────────────────

    [Fact]
    public async Task Run_UnresolvedSupplier_RejectsNotDrops_StillConserves()
    {
        var sut = NewSut();
        var sources = new[] { Source("PINV-1"), Source("PINV-2"), Source("PINV-3") };

        PartyId? Resolver(ErpnextPurchaseInvoiceSource s) =>
            s.Name == "PINV-2" ? (PartyId?)null : PartyId.NewId();

        var result = await sut.Pass.RunAsync(
            Tenant(), sources, Chart(), Account(), Account(), Resolver);

        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(1, result.Census.Rejected);
        Assert.Equal(3, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(sources.Length));

        var reject = Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.UnresolvedReference.ToString(), reject.ReasonCode);
        Assert.Equal("supplier", reject.FieldName);
        Assert.Equal("PINV-2", reject.ExternalRef);
        Assert.False(result.AllAccepted);
    }

    // ── Idempotency (C1/C7) ────────────────────────────────────────────

    [Fact]
    public async Task Run_Twice_SecondRunAllSkipped_CountStable()
    {
        var sut = NewSut();
        var chart = Chart();
        var ap = Account();
        var expense = Account();
        var sources = new[] { Source("PINV-1"), Source("PINV-2") };

        // Stable resolver: same party id per source name across both runs.
        var ids = new Dictionary<string, PartyId>();
        PartyId? Stable(ErpnextPurchaseInvoiceSource s)
        {
            if (!ids.TryGetValue(s.Name, out var id))
            {
                id = PartyId.NewId();
                ids[s.Name] = id;
            }
            return id;
        }

        var first = await sut.Pass.RunAsync(Tenant(), sources, chart, ap, expense, Stable);
        Assert.Equal(2, first.Census.Inserted);

        var second = await sut.Pass.RunAsync(Tenant(), sources, chart, ap, expense, Stable);
        Assert.Equal(0, second.Census.Inserted);
        Assert.Equal(2, second.Census.Skipped);
        Assert.True(second.Census.IsConserved(sources.Length));
    }

    // ── Tenant scope (C3) ──────────────────────────────────────────────

    [Fact]
    public async Task Run_ThreadsTenantIntoEveryBill()
    {
        var sut = NewSut();
        var tenant = new TenantId("gamma-llc");
        var chart = Chart();
        var sources = new[] { Source("PINV-1"), Source("PINV-2") };

        var result = await sut.Pass.RunAsync(
            tenant, sources, chart, Account(), Account(), AlwaysResolve);

        Assert.All(
            result.Outcomes.OfType<ImportOutcome<Bill>.Inserted>(),
            ins => Assert.Equal(tenant, ins.Record.TenantId));
    }
}
