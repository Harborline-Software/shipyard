using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// Workstream A3 — coverage for the Pass-3 orchestrator
/// <see cref="ErpnextOpeningBalancePass"/> (post-MVP WBS A3; migration-importer spec
/// §4.3). Fixture-only — synthetic hand-built sources, no real ERPNext dump.
/// Proves: opening-vs-non-opening filtering, per-JE balance gate → <c>Rejected</c>
/// (not throw), unresolved-account → <c>Rejected</c>, census conservation over the
/// opening subset, idempotent re-run (run-twice count-stable), tenant-scope
/// rejection of the system sentinel, and the opening trial-balance aggregate
/// (ADR 0100 C1/C2/C3/C5).
/// </summary>
public sealed class ErpnextOpeningBalancePassTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly TenantId TestTenant = new("tenant-erpnext-test");

    // ---- happy path: opening filter + census conservation -------------

    [Fact]
    public async Task Run_OpeningEntries_AllInserted_AndCensusConserves()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.OpeningSource("opening-cash", 1000m),
            h.OpeningSource("opening-equity", 1000m, flip: true),
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(0, result.Census.Rejected);
        Assert.True(result.Census.IsConserved(2));
        Assert.True(result.AllAccepted);
        Assert.Equal(2, result.Outcomes.Count);
        Assert.Equal(0, result.NonOpeningCount);
        Assert.Equal(2, h.Store.Snapshot(TestTenant).Count);
    }

    // ---- only is_opening entries are imported; others deferred --------

    [Fact]
    public async Task Run_FiltersOutNonOpeningEntries_AndCountsThem()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.OpeningSource("opening-1", 500m),
            h.NonOpeningSource("standalone-1", 500m),     // deferred to Pass 4.4
            h.NonOpeningSource("standalone-2", 250m),     // deferred to Pass 4.4
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // Only the one opening entry was imported.
        Assert.Equal(1, result.Census.Inserted);
        Assert.True(result.Census.IsConserved(1)); // census conserves over the OPENING subset
        Assert.Equal(2, result.NonOpeningCount);    // the two standalone JEs reported, not imported
        Assert.Single(result.Outcomes);
        // The deferred entries did NOT post.
        Assert.Single(h.Store.Snapshot(TestTenant));
    }

    // ---- per-JE balance gate → Rejected (not throw) -------------------

    [Fact]
    public async Task Run_ImbalancedOpeningEntry_Rejected_NoThrow_AndConserves()
    {
        var h = new Harness();
        // Debit 1000 vs credit 900 — imbalanced opening entry.
        var imbalanced = h.OpeningSource("opening-bad", 1000m) with
        {
            Lines = new[]
            {
                new ErpnextJournalEntryLineSource(h.AccountAExtRef, 1000m, 0m, null, null),
                new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 900m, null, null),
            },
        };
        var sources = new[] { h.OpeningSource("opening-ok", 500m), imbalanced };

        // No throw — the imbalanced entry must not abort the whole pass.
        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(1, result.Census.Inserted);  // opening-ok
        Assert.Equal(1, result.Census.Rejected);  // opening-bad
        Assert.True(result.Census.IsConserved(2));
        Assert.False(result.AllAccepted);

        var reject = Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.ConstraintViolation.ToString(), reject.ReasonCode);
        Assert.Equal("Journal Entry", reject.DocType);
        Assert.Equal("opening-bad", reject.ExternalRef);
        Assert.NotNull(reject.RuleViolated);
        Assert.Contains("imbalanced", reject.RuleViolated!);
        // The imbalanced entry produced no posted record.
        Assert.Single(h.Store.Snapshot(TestTenant));
    }

    // ---- unresolved account → Rejected (from the shipped importer) ----

    [Fact]
    public async Task Run_OpeningEntryUnknownAccount_Rejected_AndConserves()
    {
        var h = new Harness();
        var unknown = "non-existent-account";
        var badRef = h.OpeningSource("opening-bad-ref", 100m) with
        {
            // Still balances (so it passes the orchestrator's balance gate) but
            // references an account the resolver doesn't know — the shipped importer
            // rejects it as UnresolvedReference.
            Lines = new[]
            {
                new ErpnextJournalEntryLineSource(unknown, 100m, 0m, null, null),
                new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 100m, null, null),
            },
        };

        var result = await h.Pass.RunAsync(TestTenant, new[] { badRef }, Chart);

        Assert.Equal(0, result.Census.Inserted);
        Assert.Equal(1, result.Census.Rejected);
        Assert.True(result.Census.IsConserved(1));

        var reject = Assert.Single(result.Rejects);
        Assert.Equal(ImportRejectReason.UnresolvedReference.ToString(), reject.ReasonCode);
        Assert.Contains(unknown, reject.RuleViolated!);
        Assert.Empty(h.Store.Snapshot(TestTenant));
    }

    // ---- idempotent re-run (run-twice count-stable; ADR 0100 C1) ------

    [Fact]
    public async Task Run_Twice_SecondRunAllSkipped_NoDuplicateInserts()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.OpeningSource("opening-cash", 1000m),
            h.OpeningSource("opening-equity", 1000m, flip: true),
        };

        var first = await h.Pass.RunAsync(TestTenant, sources, Chart);
        Assert.Equal(2, first.Census.Inserted);
        Assert.Equal(0, first.Census.Skipped);

        // Re-import the SAME source set against the SAME store.
        var second = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // C1: re-run produces ZERO Inserted/Updated — all Skipped, no duplicate posts.
        Assert.Equal(0, second.Census.Inserted);
        Assert.Equal(0, second.Census.Updated);
        Assert.Equal(2, second.Census.Skipped);
        Assert.True(second.Census.IsConserved(2));
        // The store still holds exactly the two original entries — no fan-out.
        Assert.Equal(2, h.Store.Snapshot(TestTenant).Count);
    }

    // ---- tenant-scope: rows bind to the threaded target tenant (ADR 0100 C3) ----

    [Fact]
    public async Task Run_ImportedRows_AllScopedToTargetTenant()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.OpeningSource("opening-cash", 1000m),
            h.OpeningSource("opening-equity", 1000m, flip: true),
        };

        await h.Pass.RunAsync(TestTenant, sources, Chart);

        // C-TENANT (a): every imported row's TenantId equals the --target-tenant.
        var rows = h.Store.Snapshot(TestTenant);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, e => Assert.Equal(TestTenant, e.TenantId));
    }

    [Fact]
    public async Task Run_TenantThreadedFromParameter_NotFromSourceData()
    {
        // C-TENANT (d/e): ErpnextJournalEntrySource carries NO company/tenant hint,
        // so the effective tenant can ONLY arrive as the threaded parameter. Proof:
        // the SAME source set imported under two different target tenants lands its
        // rows in the respective tenant — never derived from (constant) source data.
        var h = new Harness();
        var tenantA = new TenantId("tenant-A");
        var tenantB = new TenantId("tenant-B");
        var sources = new[] { h.OpeningSource("opening-cash", 1000m) };

        await h.Pass.RunAsync(tenantA, sources, Chart);
        await h.Pass.RunAsync(tenantB, sources, Chart);

        var rowsA = h.Store.Snapshot(tenantA);
        var rowsB = h.Store.Snapshot(tenantB);
        Assert.Single(rowsA);
        Assert.Single(rowsB);
        Assert.All(rowsA, e => Assert.Equal(tenantA, e.TenantId));
        Assert.All(rowsB, e => Assert.Equal(tenantB, e.TenantId));
    }

    // ---- opening trial-balance aggregate ------------------------------

    [Fact]
    public async Task Run_OpeningEntries_TrialBalanceNetsToZero()
    {
        var h = new Harness();
        // A balanced pair: 1000 debit cash, 1000 credit equity.
        var sources = new[]
        {
            h.OpeningSource("opening-cash", 1000m),
            h.OpeningSource("opening-equity", 1000m, flip: true),
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(2000m, result.OpeningDebitTotal);
        Assert.Equal(2000m, result.OpeningCreditTotal);
        Assert.Equal(0m, result.OpeningTrialBalanceNet);
        Assert.True(result.OpeningTrialBalances);
    }

    [Fact]
    public async Task Run_RejectedEntry_ExcludedFromTrialBalanceAggregate()
    {
        var h = new Harness();
        var imbalanced = h.OpeningSource("opening-bad", 1000m) with
        {
            Lines = new[]
            {
                new ErpnextJournalEntryLineSource(h.AccountAExtRef, 1000m, 0m, null, null),
                new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 900m, null, null),
            },
        };
        var sources = new[] { h.OpeningSource("opening-ok", 500m), imbalanced };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // Only the accepted entry contributes to the aggregate (the rejected one
        // produced no local record).
        Assert.Equal(500m, result.OpeningDebitTotal);
        Assert.Equal(500m, result.OpeningCreditTotal);
    }

    // ---- mixed set census conservation --------------------------------

    [Fact]
    public async Task Run_MixedOpeningSet_ConservesEveryRecord()
    {
        var h = new Harness();
        var unknown = "ghost-account";
        var sources = new[]
        {
            h.OpeningSource("ok-1", 100m),                                   // Inserted
            h.OpeningSource("ok-2", 200m, flip: true),                       // Inserted
            h.OpeningSource("bad-balance", 100m) with                        // Rejected (imbalance)
            {
                Lines = new[]
                {
                    new ErpnextJournalEntryLineSource(h.AccountAExtRef, 100m, 0m, null, null),
                    new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 50m, null, null),
                },
            },
            h.OpeningSource("bad-ref", 100m) with                            // Rejected (unresolved)
            {
                Lines = new[]
                {
                    new ErpnextJournalEntryLineSource(unknown, 100m, 0m, null, null),
                    new ErpnextJournalEntryLineSource(h.AccountBExtRef, 0m, 100m, null, null),
                },
            },
            h.NonOpeningSource("not-opening", 100m),                         // deferred (not counted in census)
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        // 2 inserted + 2 rejected == 4 opening source records. No opening record vanished.
        Assert.Equal(2, result.Census.Inserted);
        Assert.Equal(2, result.Census.Rejected);
        Assert.Equal(4, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(4));
        Assert.Equal(4, result.Outcomes.Count);
        Assert.Equal(1, result.NonOpeningCount);
    }

    // ---- empty set edge case ------------------------------------------

    [Fact]
    public async Task Run_NoEntries_ConservesZero()
    {
        var h = new Harness();

        var result = await h.Pass.RunAsync(
            TestTenant, Array.Empty<ErpnextJournalEntrySource>(), Chart);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
        Assert.Empty(result.Outcomes);
        Assert.Equal(0, result.NonOpeningCount);
        Assert.True(result.OpeningTrialBalances); // 0 - 0 == 0
    }

    [Fact]
    public async Task Run_OnlyNonOpeningEntries_ImportsNothing_ConservesZeroOpening()
    {
        var h = new Harness();
        var sources = new[]
        {
            h.NonOpeningSource("standalone-1", 100m),
            h.NonOpeningSource("standalone-2", 200m),
        };

        var result = await h.Pass.RunAsync(TestTenant, sources, Chart);

        Assert.Equal(0, result.Census.Accounted);
        Assert.True(result.Census.IsConserved(0));
        Assert.Equal(2, result.NonOpeningCount);
        Assert.Empty(h.Store.Snapshot(TestTenant));
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryAccountResolver Accounts { get; }
        public InMemoryJournalStore Store { get; } = new();
        public GLAccount AccountA { get; }
        public GLAccount AccountB { get; }
        public string AccountAExtRef => "acc-A";
        public string AccountBExtRef => "acc-B";
        public ErpnextOpeningBalancePass Pass { get; }

        public Harness()
        {
            AccountA = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "1110", name: "Bank",
                type: GLAccountType.Asset, subtype: AccountSubtype.BankAccount, currency: "USD",
                externalRef: AccountAExtRef);
            AccountB = GLAccount.Create(
                id: GLAccountId.NewId(), chartId: Chart, code: "3000", name: "Opening Equity",
                type: GLAccountType.Equity, subtype: AccountSubtype.RetainedEarnings, currency: "USD",
                externalRef: AccountBExtRef);
            Accounts = new InMemoryAccountResolver(new[] { AccountA, AccountB });
            var periods = new InMemoryPeriodResolver();
            var user = new StaticUserContext("importer", new[] { "FinancialAdmin" });
            var posting = new JournalPostingService(Accounts, periods, Store, user, TimeProvider.System);
            var importer = new ErpnextJournalEntryImporter(Accounts, posting, Store);
            Pass = new ErpnextOpeningBalancePass(importer);
        }

        /// <summary>
        /// A balanced opening JE: debit <paramref name="amount"/> to account A,
        /// credit <paramref name="amount"/> to account B (or flipped when
        /// <paramref name="flip"/> is set, to vary which account is debited).
        /// </summary>
        public ErpnextJournalEntrySource OpeningSource(string name, decimal amount, bool flip = false) =>
            new(Name: name,
                Modified: "2026-05-16 12:00:00",
                PostingDate: new DateOnly(2026, 1, 1),
                Memo: "Opening balance",
                VoucherType: "Opening Entry",
                IsOpening: true,
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

        /// <summary>A balanced NON-opening JE (deferred to Pass 4.4 by this pass).</summary>
        public ErpnextJournalEntrySource NonOpeningSource(string name, decimal amount) =>
            new(Name: name,
                Modified: "2026-05-16 12:00:00",
                PostingDate: new DateOnly(2026, 2, 1),
                Memo: "Standalone JE",
                VoucherType: "Journal Entry",
                IsOpening: false,
                DocStatus: 1,
                Lines: new[]
                {
                    new ErpnextJournalEntryLineSource(AccountAExtRef, amount, 0m, null, null),
                    new ErpnextJournalEntryLineSource(AccountBExtRef, 0m, amount, null, null),
                });
    }
}
