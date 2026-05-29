using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Migration;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Census;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>
/// Tests for <see cref="ErpnextPaymentPass"/> — the A4.3 orchestration pass.
/// Asserts census conservation (ADR 0100 C2), reject-not-drop on unresolved party
/// + per-record reject, run-twice idempotency (C1), and tenant-first threading (C3).
/// </summary>
public class ErpnextPaymentPassTests
{
    private static TenantId Tenant() => new("acme");
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();

    private sealed record Sut(ErpnextPaymentPass Pass, InMemoryPaymentRepository Repo);

    private static Sut NewSut()
    {
        var repo = new InMemoryPaymentRepository();
        return new Sut(new ErpnextPaymentPass(new ErpnextPaymentImporter(repo)), repo);
    }

    private static ErpnextPaymentSource Source(
        string name,
        string paymentType = "Receive",
        decimal paidAmount = 1000m,
        decimal unallocated = 1000m,
        string? currency = "USD",
        string modified = "2026-05-17 12:00:00") =>
        new(
            Name: name,
            Modified: modified,
            PaymentType: paymentType,
            ModeOfPayment: "Cash",
            Party: "CUST-" + name,
            PostingDate: new DateOnly(2026, 5, 17),
            PaidAmount: paidAmount,
            UnallocatedAmount: unallocated,
            Currency: currency,
            ReferenceNo: null);

    // A resolver that always resolves to a fresh party.
    private static PartyId? AlwaysResolve(ErpnextPaymentSource _) => PartyId.NewId();

    // ── Happy path + conservation ──────────────────────────────────────

    [Fact]
    public async Task Run_AllResolvable_ImportsAll_CensusConserves()
    {
        var sut = NewSut();
        var sources = new[] { Source("PE-1"), Source("PE-2"), Source("PE-3") };

        var result = await sut.Pass.RunAsync(Tenant(), sources, Chart(), AlwaysResolve);

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
            Tenant(), Array.Empty<ErpnextPaymentSource>(), Chart(), AlwaysResolve);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
    }

    // ── Reject-not-drop ────────────────────────────────────────────────

    [Fact]
    public async Task Run_UnresolvedParty_RejectsNotDrops_StillConserves()
    {
        var sut = NewSut();
        var sources = new[] { Source("PE-1"), Source("PE-2"), Source("PE-3") };

        // Resolve all but PE-2.
        PartyId? Resolver(ErpnextPaymentSource s) =>
            s.Name == "PE-2" ? (PartyId?)null : PartyId.NewId();

        var result = await sut.Pass.RunAsync(Tenant(), sources, Chart(), Resolver);

        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(1, result.Census.Rejected);
        Assert.Equal(3, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(sources.Length));

        var reject = Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.UnresolvedReference.ToString(), reject.ReasonCode);
        Assert.Equal("party", reject.FieldName);
        Assert.Equal("PE-2", reject.ExternalRef);
    }

    [Fact]
    public async Task Run_PerRecordReject_DoesNotAbortPass_StillConserves()
    {
        var sut = NewSut();
        var sources = new[]
        {
            Source("PE-1"),
            Source("PE-2", currency: "EUR"),   // importer rejects (UnsupportedCurrency)
            Source("PE-3"),
        };

        var result = await sut.Pass.RunAsync(Tenant(), sources, Chart(), AlwaysResolve);

        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(1, result.Census.Rejected);
        Assert.Equal(3, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(sources.Length));

        var reject = Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.UnsupportedCurrency.ToString(), reject.ReasonCode);
        Assert.False(result.AllAccepted);
    }

    // ── Idempotency (C1) ───────────────────────────────────────────────

    [Fact]
    public async Task Run_Twice_SecondRunAllSkipped_CountStable()
    {
        var sut = NewSut();
        var chart = Chart();
        var sources = new[] { Source("PE-1"), Source("PE-2") };

        // Stable resolver: same party id per source name across both runs, so the
        // second run is a true idempotent re-import (not a party churn).
        var ids = new Dictionary<string, PartyId>();
        PartyId? Stable(ErpnextPaymentSource s)
        {
            if (!ids.TryGetValue(s.Name, out var id))
            {
                id = PartyId.NewId();
                ids[s.Name] = id;
            }
            return id;
        }

        var first = await sut.Pass.RunAsync(Tenant(), sources, chart, Stable);
        Assert.Equal(2, first.Census.Inserted);

        var second = await sut.Pass.RunAsync(Tenant(), sources, chart, Stable);
        Assert.Equal(0, second.Census.Inserted);
        Assert.Equal(2, second.Census.Skipped);
        Assert.True(second.Census.IsConserved(sources.Length));

        // Count-stable: still exactly two payments.
        var all = await sut.Repo.ListByChartAsync(Tenant(), chart);
        Assert.Equal(2, all.Count);
    }

    // ── Tenant scope (C3) ──────────────────────────────────────────────

    [Fact]
    public async Task Run_ThreadsTenantIntoEveryPayment()
    {
        var sut = NewSut();
        var tenant = new TenantId("gamma-llc");
        var chart = Chart();
        var sources = new[] { Source("PE-1"), Source("PE-2") };

        await sut.Pass.RunAsync(tenant, sources, chart, AlwaysResolve);

        var all = await sut.Repo.ListByChartAsync(tenant, chart);
        Assert.Equal(2, all.Count);
        Assert.All(all, p => Assert.Equal(tenant, p.TenantId));
    }
}
