using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Import.Census;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// Workstream A1 — coverage for the Pass-1 orchestrator
/// <see cref="ErpnextChartImportPass"/> (post-MVP WBS A1; migration-importer spec
/// §3.2/§3.4). Fixture-only — synthetic hand-built sources, no real ERPNext dump.
/// Proves: topological parent-first ordering, cycle → <c>Rejected</c> (not throw),
/// dangling-parent → <c>Rejected</c>, duplicate-key → <c>Rejected</c>, and census
/// conservation across every exit (ADR 0100 C2).
/// </summary>
public sealed class ErpnextChartImportPassTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    // ---- builders ----------------------------------------------------

    private static ErpnextAccountSource Account(
        string name, string? parent = null, string? type = "Bank", bool isGroup = false) =>
        new(Name: name,
            Modified: "2026-05-16 12:00:00",
            AccountName: name,
            AccountNumber: null,
            ParentAccountName: parent,
            AccountType: type,
            IsGroup: isGroup,
            Disabled: false);

    private static ErpnextChartImportPass NewPass(out InMemoryAccountResolver resolver)
    {
        resolver = new InMemoryAccountResolver();
        var accountImporter = new ErpnextAccountImporter(resolver);
        var costCenterImporter = new ErpnextCostCenterImporter(
            PropertyAliasMap.Empty, new InMemoryClassificationStore());
        return new ErpnextChartImportPass(accountImporter, costCenterImporter);
    }

    // ---- happy path: toposort + census --------------------------------

    [Fact]
    public async Task Run_HappyChart_AllInserted_AndCensusConserves()
    {
        var pass = NewPass(out var resolver);
        // Intentionally INPUT children-before-parents to prove the toposort reorders.
        var accounts = new[]
        {
            Account("leaf-bank", parent: "current-assets", type: "Bank"),
            Account("current-assets", parent: "assets-root", type: null, isGroup: true),
            Account("assets-root", parent: null, type: null, isGroup: true),
        };

        var result = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);

        Assert.Equal(3, result.AccountCensus.Inserted);
        Assert.Equal(0, result.AccountCensus.Rejected);
        Assert.True(result.AccountCensus.IsConserved(3));
        Assert.True(result.AllAccepted);

        // The leaf resolved its parent FK — only possible if the parent imported first.
        var leaf = resolver.SeededAccounts.Single(a => a.ExternalRef == "leaf-bank");
        Assert.NotNull(leaf.ParentAccountId);
    }

    [Fact]
    public void TopologicalSort_OrdersParentsBeforeChildren()
    {
        var accounts = new[]
        {
            Account("c", parent: "b"),
            Account("b", parent: "a"),
            Account("a", parent: null),
        };

        var topo = ErpnextChartImportPass.TopologicalSort(accounts);

        Assert.Empty(topo.Unresolved);
        var order = topo.Sorted.Select(s => s.Name).ToList();
        Assert.Equal(new[] { "a", "b", "c" }, order);
        // Every parent precedes its child.
        Assert.True(order.IndexOf("a") < order.IndexOf("b"));
        Assert.True(order.IndexOf("b") < order.IndexOf("c"));
    }

    [Fact]
    public void TopologicalSort_Forest_KeepsDeterministicInputOrderAmongRoots()
    {
        var accounts = new[]
        {
            Account("root-1", parent: null),
            Account("root-2", parent: null),
            Account("child-of-1", parent: "root-1"),
        };

        var topo = ErpnextChartImportPass.TopologicalSort(accounts);
        var order = topo.Sorted.Select(s => s.Name).ToList();

        Assert.Equal(3, order.Count);
        Assert.True(order.IndexOf("root-1") < order.IndexOf("child-of-1"));
    }

    // ---- cycle detection → Rejected (not throw) -----------------------

    [Fact]
    public async Task Run_ChartWithCycle_RejectsCycleParticipants_NoThrow()
    {
        var pass = NewPass(out _);
        // a → b → a is a 2-cycle. "ok-root" is a clean unrelated account.
        var accounts = new[]
        {
            Account("ok-root", parent: null, type: "Bank"),
            Account("a", parent: "b", type: "Bank"),
            Account("b", parent: "a", type: "Bank"),
        };

        // No throw — the bad edge must not abort the whole chart.
        var result = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);

        Assert.Equal(1, result.AccountCensus.Inserted);   // ok-root
        Assert.Equal(2, result.AccountCensus.Rejected);   // a + b
        Assert.True(result.AccountCensus.IsConserved(3)); // 1 + 2 == 3
        Assert.False(result.AllAccepted);

        var rejects = result.AccountRejects;
        Assert.Equal(2, rejects.Count);
        Assert.All(rejects, r => Assert.Equal(
            ImportRejectReason.UnresolvedReference.ToString(), r.ReasonCode));
        Assert.All(rejects, r => Assert.Equal("Account", r.DocType));
        Assert.Contains(rejects, r => r.ExternalRef == "a");
        Assert.Contains(rejects, r => r.ExternalRef == "b");
    }

    [Fact]
    public void TopologicalSort_SelfCycle_IsRejected()
    {
        var accounts = new[] { Account("self", parent: "self") };

        var topo = ErpnextChartImportPass.TopologicalSort(accounts);

        Assert.Empty(topo.Sorted);
        var unresolved = Assert.Single(topo.Unresolved);
        Assert.Equal(ErpnextChartImportPass.UnresolvedKind.Cycle, unresolved.Kind);
    }

    // ---- dangling parent → Rejected -----------------------------------

    [Fact]
    public async Task Run_DanglingParent_RejectsChild_AndConserves()
    {
        var pass = NewPass(out _);
        var accounts = new[]
        {
            Account("orphan", parent: "missing-parent-not-in-set", type: "Bank"),
        };

        var result = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);

        Assert.Equal(0, result.AccountCensus.Inserted);
        Assert.Equal(1, result.AccountCensus.Rejected);
        Assert.True(result.AccountCensus.IsConserved(1));

        var reject = Assert.Single(result.AccountRejects);
        Assert.Equal(ImportRejectReason.UnresolvedReference.ToString(), reject.ReasonCode);
        Assert.Equal("orphan", reject.ExternalRef);
        Assert.Contains("missing-parent-not-in-set", reject.RuleViolated!);
    }

    // ---- duplicate natural key → Rejected -----------------------------

    [Fact]
    public async Task Run_DuplicateName_FirstWins_DuplicateRejected_AndConserves()
    {
        var pass = NewPass(out _);
        var accounts = new[]
        {
            Account("dup", parent: null, type: "Bank"),
            Account("dup", parent: null, type: "Bank"), // duplicate natural key
        };

        var result = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);

        Assert.Equal(1, result.AccountCensus.Inserted);
        Assert.Equal(1, result.AccountCensus.Rejected);
        Assert.True(result.AccountCensus.IsConserved(2)); // conservation holds with the dup

        var reject = Assert.Single(result.AccountRejects);
        Assert.Equal(ImportRejectReason.DuplicateExternalRef.ToString(), reject.ReasonCode);
    }

    // ---- census conservation under a mixed chart ----------------------

    [Fact]
    public async Task Run_MixedChart_ConservesEveryRecord()
    {
        var pass = NewPass(out _);
        var accounts = new[]
        {
            Account("root", parent: null, type: null, isGroup: true), // Inserted
            Account("bank", parent: "root", type: "Bank"),            // Inserted
            Account("cyc-x", parent: "cyc-y", type: "Bank"),          // Rejected (cycle)
            Account("cyc-y", parent: "cyc-x", type: "Bank"),          // Rejected (cycle)
            Account("orphan", parent: "ghost", type: "Bank"),         // Rejected (dangling)
        };

        var result = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);

        // 2 inserted + 3 rejected == 5 source records. No record vanished.
        Assert.Equal(2, result.AccountCensus.Inserted);
        Assert.Equal(3, result.AccountCensus.Rejected);
        Assert.Equal(5, result.AccountCensus.Accounted);
        Assert.True(result.AccountCensus.IsConserved(accounts.Length));
        Assert.Equal(accounts.Length, result.AccountOutcomes.Count);
    }

    // ---- idempotency re-run via the orchestrator ----------------------

    [Fact]
    public async Task Run_Twice_SecondRunSkipsAtSameVersion()
    {
        var pass = NewPass(out _);
        var accounts = new[] { Account("root", parent: null, type: "Bank") };

        var first = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);
        Assert.Equal(1, first.AccountCensus.Inserted);

        // Re-run the SAME pass instance (same resolver) at the same version.
        var second = await pass.RunAsync(accounts, Array.Empty<ErpnextCostCenterSource>(), Chart);
        Assert.Equal(1, second.AccountCensus.Skipped);
        Assert.Equal(0, second.AccountCensus.Inserted);
        Assert.True(second.AccountCensus.IsConserved(1));
    }

    // ---- empty chart edge case ----------------------------------------

    [Fact]
    public async Task Run_EmptyChart_ConservesZero()
    {
        var pass = NewPass(out _);

        var result = await pass.RunAsync(
            Array.Empty<ErpnextAccountSource>(), Array.Empty<ErpnextCostCenterSource>(), Chart);

        Assert.Equal(0, result.AccountCensus.Accounted);
        Assert.True(result.AccountCensus.IsConserved(0));
        Assert.Empty(result.AccountOutcomes);
    }

    // ---- cost-center census conservation through the orchestrator -----

    [Fact]
    public async Task Run_WithCostCenters_ConservesCostCenterCensus()
    {
        var pass = NewPass(out _);
        var accounts = new[] { Account("root", parent: null, type: "Bank") };
        var costCenters = new[]
        {
            new ErpnextCostCenterSource("cc-1", "2026-05-16", "Property A"),
            new ErpnextCostCenterSource("cc-grp", "2026-05-16", "All Centers", IsGroup: true),
        };

        var result = await pass.RunAsync(accounts, costCenters, Chart);

        // Leaf cost-center → Inserted (classification); group → Skipped.
        Assert.Equal(1, result.CostCenterCensus.Inserted);
        Assert.Equal(1, result.CostCenterCensus.Skipped);
        Assert.True(result.CostCenterCensus.IsConserved(costCenters.Length));
    }
}
