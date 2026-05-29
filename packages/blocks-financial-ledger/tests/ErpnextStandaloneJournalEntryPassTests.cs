using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// Workstream A4.4 — coverage for the Pass-4.4 orchestrator
/// <see cref="ErpnextStandaloneJournalEntryPass"/> (post-MVP WBS A4.4; migration-importer
/// spec §4.4). Fixture-only — synthetic hand-built sources, no real ERPNext dump. The
/// symmetric complement to A3 (<see cref="ErpnextOpeningBalancePass"/>). Proves:
/// standalone-vs-opening filtering, DocStatus partition (docstatus==1 only is imported;
/// Draft/Cancelled counted-not-dropped), imbalance → <c>Rejected</c> (not throw),
/// unresolved-account → <c>Rejected</c>, census conservation over the submitted-standalone
/// subset, idempotent re-run (run-twice count-stable → <c>Skipped</c> never duplicate),
/// tenant-scope threading (rows land in the threaded tenant, not derived from source)
/// (ADR 0100 C1/C2/C3/C5).
/// </summary>
public sealed class ErpnextStandaloneJournalEntryPassTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly TenantId TestTenant = new("tenant-erpnext-test");

    // ---- happy path: standalone filter + census conservation ----------

    [Fact]
    public async Task Run_SubmittedStandaloneEntries_AllInserted_AndCensusConserves()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("je-1", 1000m),
            h.StandaloneSource("je-2", 500m, flip: true),
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(0, result.Census.Rejected);
        Assert.True(result.Census.IsConserved(2));
        Assert.True(result.AllAccepted);
        Assert.Equal(2, result.Outcomes.Count);
        Assert.Equal(2, result.Posted);
        Assert.Equal(0, result.OpeningCount);
        Assert.Equal(0, result.NonSubmittedCount);
        Assert.Equal(2, h.Store.Snapshot(TestTenant).Count);
    }

    // ---- opening entries are deferred to Pass 3 -----------------------

    [Fact]
    public async Task Run_FiltersOutOpeningEntries_AndCountsThem()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("je-1", 500m),
            h.OpeningSource("opening-1", 500m),       // deferred to Pass 3
            h.OpeningSource("opening-2", 250m),       // deferred to Pass 3
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // Only the one standalone entry is in census scope.
        Assert.Equal(1, result.Census.Accounted);
        Assert.Equal(1, result.Census.Inserted);
        Assert.True(result.Census.IsConserved(1));
        Assert.Equal(2, result.OpeningCount);
        Assert.Equal(0, result.NonSubmittedCount);
        Assert.Single(result.Outcomes);
        Assert.Single(h.Store.Snapshot(TestTenant));
    }

    // ---- DocStatus partition: only docstatus==1 imported --------------

    [Fact]
    public async Task Run_NonSubmittedStandaloneEntries_NotImported_ButCounted()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("je-submitted", 500m),                       // docstatus 1 → imported
            h.StandaloneSource("je-draft", 100m) with { DocStatus = 0 },    // Draft → counted, not imported
            h.StandaloneSource("je-cancelled", 200m) with { DocStatus = 2 },// Cancelled → counted, not imported
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // Census only spans the single submitted entry.
        Assert.Equal(1, result.Census.Accounted);
        Assert.Equal(1, result.Census.Inserted);
        Assert.True(result.Census.IsConserved(1));
        Assert.Equal(0, result.OpeningCount);
        Assert.Equal(2, result.NonSubmittedCount);
        Assert.Single(result.Outcomes);
        // Only the submitted entry posted.
        Assert.Single(h.Store.Snapshot(TestTenant));
    }

    // ---- imbalance → Rejected (not throw) -----------------------------

    [Fact]
    public async Task Run_ImbalancedStandaloneEntry_IsRejected_NotThrown()
    {
        var h = new Harness();
        var imbalanced = h.StandaloneSource("je-bad", 1000m) with
        {
            Lines = new[]
            {
                new ErpnextJournalEntryLineSource(h.AccountAExtRef, 1000m, 0m, null, null),
                new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 900m, null, null),
            },
        };
        var sources = new[] { h.StandaloneSource("je-ok", 500m), imbalanced };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(1, result.Census.Inserted);
        Assert.Equal(1, result.Census.Rejected);
        Assert.True(result.Census.IsConserved(2));
        Assert.False(result.AllAccepted);
        Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.ConstraintViolation.ToString(), result.Rejects[0].ReasonCode);
        Assert.Equal("je-bad", result.Rejects[0].ExternalRef);
        // Only the balanced entry posted.
        Assert.Single(h.Store.Snapshot(TestTenant));
    }

    // ---- unresolved account → Rejected --------------------------------

    [Fact]
    public async Task Run_UnresolvedAccount_IsRejected()
    {
        var h = new Harness();
        var badRef = h.StandaloneSource("je-bad-ref", 100m) with
        {
            Lines = new[]
            {
                new ErpnextJournalEntryLineSource("acc-DOES-NOT-EXIST", 100m, 0m, null, null),
                new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 100m, null, null),
            },
        };
        var sources = new[] { badRef };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(0, result.Census.Inserted);
        Assert.Equal(1, result.Census.Rejected);
        Assert.True(result.Census.IsConserved(1));
        Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.UnresolvedReference.ToString(), result.Rejects[0].ReasonCode);
        Assert.Equal("je-bad-ref", result.Rejects[0].ExternalRef);
        Assert.Empty(h.Store.Snapshot(TestTenant));
    }

    // ---- idempotency: run twice, counts stay stable -------------------

    [Fact]
    public async Task Run_Twice_IsIdempotent_SecondRunSkipsNoDuplicates()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("je-1", 1000m),
            h.StandaloneSource("je-2", 500m, flip: true),
        };

        var first = await h.Pass.RunAsync(TestTenant, sources, Chart);
        Assert.Equal(2, first.Census.Inserted);
        Assert.Equal(2, h.Store.Snapshot(TestTenant).Count);

        // Re-run the SAME source set against the SAME store.
        var second = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // No duplicate inserts: the second run skips both (posted entries immutable).
        Assert.Equal(0, second.Census.Inserted);
        Assert.Equal(2, second.Census.Skipped);
        Assert.Equal(0, second.Census.Rejected);
        Assert.True(second.Census.IsConserved(2));
        // Store count is STABLE — the invariant the run-twice test guards (ADR 0100 C1).
        Assert.Equal(2, h.Store.Snapshot(TestTenant).Count);
    }

    // ---- tenant scope: rows land in the threaded tenant ---------------

    [Fact]
    public async Task Run_ImportedRows_AllScopedToTargetTenant()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("je-1", 1000m),
            h.StandaloneSource("je-2", 500m, flip: true),
        };

        await h.Pass.RunAsync(TestTenant, sources, Chart);

        var posted = h.Store.Snapshot(TestTenant);
        Assert.Equal(2, posted.Count);
        Assert.All(posted, e => Assert.Equal(TestTenant, e.TenantId));
    }

    [Fact]
    public async Task Run_TenantThreadedFromParameter_NotFromSourceData()
    {
        // The SAME source set imported under two different tenants lands rows in the
        // respective tenants — proving the tenant is threaded from the parameter, not
        // derived from any source field (ADR 0100 C3 / C-TENANT d/e).
        var tenantA = new TenantId("tenant-A");
        var tenantB = new TenantId("tenant-B");
        var store = new InMemoryJournalStore();
        var h = new Harness(store);
        var sources = new[] { h.StandaloneSource("je-shared", 750m) };

        await h.Pass.RunAsync(tenantA, sources, Chart);
        await h.Pass.RunAsync(tenantB, sources, Chart);

        Assert.Single(store.Snapshot(tenantA));
        Assert.Single(store.Snapshot(tenantB));
        Assert.All(store.Snapshot(tenantA), e => Assert.Equal(tenantA, e.TenantId));
        Assert.All(store.Snapshot(tenantB), e => Assert.Equal(tenantB, e.TenantId));
    }

    // ---- mixed batch: insert + reject + opening + non-submitted -------

    [Fact]
    public async Task Run_MixedBatch_ConservesOverSubmittedStandaloneSubsetOnly()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("ok-1", 100m),                                // Inserted
            h.StandaloneSource("ok-2", 200m, flip: true),                    // Inserted
            h.StandaloneSource("bad-balance", 100m) with                     // Rejected (imbalance)
            {
                Lines = new[]
                {
                    new ErpnextJournalEntryLineSource(h.AccountAExtRef, 100m, 0m, null, null),
                    new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 50m, null, null),
                },
            },
            h.StandaloneSource("bad-ref", 100m) with                         // Rejected (unresolved)
            {
                Lines = new[]
                {
                    new ErpnextJournalEntryLineSource("acc-MISSING", 100m, 0m, null, null),
                    new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 100m, null, null),
                },
            },
            h.OpeningSource("an-opening", 100m),                             // deferred to Pass 3
            h.StandaloneSource("a-draft", 100m) with { DocStatus = 0 },      // non-submitted (counted)
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // Census spans the FOUR submitted-standalone entries (2 ok + 2 bad).
        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(2, result.Census.Rejected);
        Assert.Equal(4, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(4));
        Assert.Equal(1, result.OpeningCount);
        Assert.Equal(1, result.NonSubmittedCount);
        Assert.Equal(2, result.Rejects.Count);
        Assert.Equal(2, h.Store.Snapshot(TestTenant).Count);
    }

    // ---- empty + edge cases -------------------------------------------

    [Fact]
    public async Task Run_EmptySet_ConservesZero()
    {
        var h = new Harness();

        var result = await h.Pass.RunAsync(
            TestTenant, Array.Empty<ErpnextJournalEntrySource>(), Chart);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
        Assert.Empty(result.Outcomes);
        Assert.Equal(0, result.OpeningCount);
        Assert.Equal(0, result.NonSubmittedCount);
        Assert.True(result.AllAccepted);
    }

    [Fact]
    public async Task Run_OnlyOpeningEntries_ImportsNothing_ConservesZeroStandalone()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.OpeningSource("opening-1", 100m),
            h.OpeningSource("opening-2", 200m),
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
        Assert.Equal(2, result.OpeningCount);
        Assert.Equal(0, result.NonSubmittedCount);
        Assert.Empty(h.Store.Snapshot(TestTenant));
    }

    [Fact]
    public async Task Run_OnlyNonSubmittedStandalone_ImportsNothing_ConservesZero()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.StandaloneSource("draft-1", 100m) with { DocStatus = 0 },
            h.StandaloneSource("cancelled-1", 200m) with { DocStatus = 2 },
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
        Assert.Equal(0, result.OpeningCount);
        Assert.Equal(2, result.NonSubmittedCount);
        Assert.Empty(h.Store.Snapshot(TestTenant));
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryAccountResolver Accounts { get; }
        public InMemoryJournalStore Store { get; }
        public GLAccount AccountA { get; }
        public GLAccount AccountB { get; }
        public string AccountAExtRef => "acc-A";
        public string AccountBExtRef => "acc-B";
        public ErpnextStandaloneJournalEntryPass Pass { get; }

        public Harness() : this(new InMemoryJournalStore())
        {
        }

        public Harness(InMemoryJournalStore store)
        {
            Store = store;
            AccountA = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "1110", name: "Bank",
                type: GLAccountType.Asset, subtype: AccountSubtype.BankAccount, currency: "USD",
                externalRef: AccountAExtRef);
            AccountB = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "4000", name: "Revenue",
                type: GLAccountType.Revenue, subtype: AccountSubtype.OperatingIncome, currency: "USD",
                externalRef: AccountBExtRef);
            Accounts = new InMemoryAccountResolver(new[] { AccountA, AccountB });
            var periods = new InMemoryPeriodResolver();
            var user = new StaticUserContext("importer", new[] { "FinancialAdmin" });
            var posting = new JournalPostingService(Accounts, periods, Store, user, TimeProvider.System);
            var importer = new ErpnextJournalEntryImporter(Accounts, posting, Store);
            Pass = new ErpnextStandaloneJournalEntryPass(importer);
        }

        /// <summary>
        /// A balanced, SUBMITTED (docstatus==1), non-opening standalone JE: debit
        /// <paramref name="amount"/> to account A, credit <paramref name="amount"/> to
        /// account B (or flipped when <paramref name="flip"/> is set).
        /// </summary>
        public ErpnextJournalEntrySource StandaloneSource(string name, decimal amount, bool flip = false) =>
            new(Name: name,
                Modified: "2026-05-16 12:00:00",
                PostingDate: new DateOnly(2026, 2, 1),
                Memo: "Standalone JE",
                VoucherType: "Journal Entry",
                IsOpening: false,
                DocStatus: 1,
                Lines: flip
                    ? new[]
                    {
                        new ErpnextJournalEntryLineSource(AccountBExtRef, amount, 0m, null, null),
                        new ErpnextJournalEntryLineSource(AccountAExtRef, 0m, amount, null, null),
                    }
                    : new[]
                    {
                        new ErpnextJournalEntryLineSource(AccountAExtRef, amount, 0m, null, null),
                        new ErpnextJournalEntryLineSource(AccountBExtRef, 0m, amount, null, null),
                    });

        /// <summary>A balanced OPENING JE (deferred to Pass 3 by this pass).</summary>
        public ErpnextJournalEntrySource OpeningSource(string name, decimal amount) =>
            new(Name: name,
                Modified: "2026-05-16 12:00:00",
                PostingDate: new DateOnly(2026, 1, 1),
                Memo: "Opening balance",
                VoucherType: "Opening Entry",
                IsOpening: true,
                DocStatus: 1,
                Lines: new[]
                {
                    new ErpnextJournalEntryLineSource(AccountAExtRef, amount, 0m, null, null),
                    new ErpnextJournalEntryLineSource(AccountBExtRef, 0m, amount, null, null),
                });
    }
}
