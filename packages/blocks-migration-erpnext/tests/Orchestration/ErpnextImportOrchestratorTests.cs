using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.Migration.Erpnext.DependencyInjection;
using Sunfish.Blocks.Migration.Erpnext.Orchestration;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Orchestration;

/// <summary>
/// A7-6: end-to-end coverage for <see cref="ErpnextImportOrchestrator"/> (spec §4, §8) against the
/// in-memory pipeline composed by
/// <see cref="ErpnextImportPipelineServiceCollectionExtensions.AddErpnextImportPipeline"/> on synthetic
/// v15 dump fixtures. The composition root IS the system under test: each test resolves the orchestrator
/// from a DI scope and drives <c>RunAsync</c>, so a resolve-time wiring gap, a mis-ordered commit gate,
/// or a broken <c>--from-pass</c> resume fails loudly here. No disk, no real run — RUN stays
/// CIC-dump-gated (shipyard#270); BUILD + tests exercise <c>FromSql</c> fixtures only.
/// </summary>
public sealed class ErpnextImportOrchestratorTests
{
    // Two root accounts + a *balanced* opening JE (1000 debit Bank / 1000 credit Equity). Pass 1 imports
    // both accounts (parent NULL → root, no reject); Pass 3 posts the opening JE; the posted ledger nets
    // to zero so Pass 6 verification passes and the run commits. All other DocType tables are absent —
    // the source reader yields an empty stream for a missing table, so the passes that read them no-op.
    private const string BalancedDump = """
        CREATE TABLE `tabAccount` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `account_name` varchar(140) DEFAULT NULL,
          `account_number` varchar(140) DEFAULT NULL,
          `parent_account` varchar(140) DEFAULT NULL,
          `account_type` varchar(140) DEFAULT NULL,
          `is_group` tinyint(1) NOT NULL DEFAULT 0,
          `disabled` tinyint(1) NOT NULL DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabAccount` VALUES
          ('1000 - Bank','2026-01-01 10:00:00.000000','Bank Account','1000',NULL,'Bank',0,0),
          ('2000 - Equity','2026-01-01 10:00:00.000000','Equity','2000',NULL,'Equity',0,0);

        CREATE TABLE `tabJournal Entry` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `posting_date` date DEFAULT NULL,
          `user_remark` text DEFAULT NULL,
          `voucher_type` varchar(140) DEFAULT NULL,
          `is_opening` varchar(10) DEFAULT 'No',
          `docstatus` int(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabJournal Entry` VALUES
          ('JV-2026-0001','2026-02-01 10:00:00.000000','2026-01-01','Opening entry','Opening Entry','Yes',1);

        CREATE TABLE `tabJournal Entry Account` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `parenttype` varchar(140) DEFAULT NULL,
          `account` varchar(140) DEFAULT NULL,
          `debit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
          `credit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
          `cost_center` varchar(140) DEFAULT NULL,
          `user_remark` text DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabJournal Entry Account` VALUES
          ('JEA-0001','JV-2026-0001','Journal Entry','1000 - Bank',1000.000000000,0.000000000,NULL,NULL),
          ('JEA-0002','JV-2026-0001','Journal Entry','2000 - Equity',0.000000000,1000.000000000,NULL,'Opening');
        """;

    // Same two accounts, but the opening JE is *imbalanced* (1000 debit / 900 credit). Pass 3's per-JE
    // balance gate rejects it (ConstraintViolation) BEFORE any upsert — producing exactly one reject and
    // posting nothing — which a RejectThreshold of 0 then turns into a rollback.
    private const string ImbalancedOpeningDump = """
        CREATE TABLE `tabAccount` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `account_name` varchar(140) DEFAULT NULL,
          `account_number` varchar(140) DEFAULT NULL,
          `parent_account` varchar(140) DEFAULT NULL,
          `account_type` varchar(140) DEFAULT NULL,
          `is_group` tinyint(1) NOT NULL DEFAULT 0,
          `disabled` tinyint(1) NOT NULL DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabAccount` VALUES
          ('1000 - Bank','2026-01-01 10:00:00.000000','Bank Account','1000',NULL,'Bank',0,0),
          ('2000 - Equity','2026-01-01 10:00:00.000000','Equity','2000',NULL,'Equity',0,0);

        CREATE TABLE `tabJournal Entry` (
          `name` varchar(140) NOT NULL,
          `modified` datetime(6) DEFAULT NULL,
          `posting_date` date DEFAULT NULL,
          `user_remark` text DEFAULT NULL,
          `voucher_type` varchar(140) DEFAULT NULL,
          `is_opening` varchar(10) DEFAULT 'No',
          `docstatus` int(1) DEFAULT 0,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabJournal Entry` VALUES
          ('JV-2026-0001','2026-02-01 10:00:00.000000','2026-01-01','Opening entry','Opening Entry','Yes',1);

        CREATE TABLE `tabJournal Entry Account` (
          `name` varchar(140) NOT NULL,
          `parent` varchar(140) DEFAULT NULL,
          `parenttype` varchar(140) DEFAULT NULL,
          `account` varchar(140) DEFAULT NULL,
          `debit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
          `credit_in_account_currency` decimal(21,9) DEFAULT 0.000000000,
          `cost_center` varchar(140) DEFAULT NULL,
          `user_remark` text DEFAULT NULL,
          PRIMARY KEY (`name`)
        ) ENGINE=InnoDB;
        INSERT INTO `tabJournal Entry Account` VALUES
          ('JEA-0001','JV-2026-0001','Journal Entry','1000 - Bank',1000.000000000,0.000000000,NULL,NULL),
          ('JEA-0002','JV-2026-0001','Journal Entry','2000 - Equity',0.000000000,900.000000000,NULL,'Opening');
        """;

    [Fact]
    public void Pipeline_resolves_orchestrator_from_a_scope()
    {
        // The DI-gap proof: the whole graph (passes, upserters, applier, verification, UoW) must resolve
        // end-to-end, and the orchestrator is SCOPED so it only resolves from inside a created scope.
        var services = new ServiceCollection();
        services.AddErpnextImportPipeline(
            MariaDbDumpSourceReader.FromSql(BalancedDump), PartyId.NewId(), new TenantId("test-tenant"));
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ErpnextImportOrchestrator>();

        Assert.NotNull(orchestrator);
    }

    [Fact]
    public async Task Balanced_dump_commits_with_no_halt()
    {
        var run = await RunImportAsync(BalancedDump, ErpnextImportOptions.Default);

        Assert.Equal(ImportRunStatus.Committed, run.Result.Status);
        Assert.Equal(ImportHaltReason.None, run.Result.HaltReason);
        Assert.Null(run.Result.ReportPath); // the host renders + writes migration-report.md, not the orchestrator
        Assert.NotNull(run.Result.Report);
        Assert.Empty(run.Result.Report.RejectBin); // a balanced dump imports cleanly
        Assert.True(run.Committed);
        Assert.False(run.RolledBack);
    }

    [Fact]
    public async Task Dry_run_rolls_back_and_commits_nothing()
    {
        var run = await RunImportAsync(BalancedDump, ErpnextImportOptions.Default with { DryRun = true });

        // DryRun is the first gate: reported as DryRun even though the run would otherwise have committed.
        Assert.Equal(ImportRunStatus.RolledBack, run.Result.Status);
        Assert.Equal(ImportHaltReason.DryRun, run.Result.HaltReason);
        Assert.True(run.RolledBack);
        Assert.False(run.Committed);
    }

    [Fact]
    public async Task Trial_balance_mismatch_rolls_back()
    {
        // Override the ledger read-model with one that reports a non-zero net balance, and skip every
        // posting pass (--from-pass 6) so the only thing exercised is the verification gate reading the
        // fake. The later AddSingleton wins on resolve, so the verification pass sees the unbalanced ledger.
        var run = await RunImportAsync(
            BalancedDump,
            ErpnextImportOptions.Default with { FromPass = 6 },
            customize: services => services.AddSingleton<IGeneralLedgerReadModel>(new UnbalancedLedger()));

        Assert.Equal(ImportRunStatus.RolledBack, run.Result.Status);
        Assert.Equal(ImportHaltReason.TrialBalanceMismatch, run.Result.HaltReason);
        Assert.False(run.Result.Report.Verification.IsPassed);
        Assert.True(run.RolledBack);
        Assert.False(run.Committed);
    }

    [Fact]
    public async Task Reject_threshold_exceeded_rolls_back()
    {
        // The imbalanced opening JE is the deterministic single reject; threshold 0 means 1 > 0 → rollback.
        var run = await RunImportAsync(ImbalancedOpeningDump, ErpnextImportOptions.Default with { RejectThreshold = 0 });

        Assert.Equal(ImportRunStatus.RolledBack, run.Result.Status);
        Assert.Equal(ImportHaltReason.RejectThresholdExceeded, run.Result.HaltReason);
        Assert.NotEmpty(run.Result.Report.RejectBin);
        Assert.True(run.RolledBack);
        Assert.False(run.Committed);
    }

    [Fact]
    public async Task From_pass_6_runs_only_verification_and_commits_empty_ledger()
    {
        // --from-pass 6 skips passes 1-5; verification ALWAYS runs. Nothing posts, so the real in-memory
        // ledger nets to zero → balanced → Committed. Only Pass 6 contributes a timed pass duration.
        var run = await RunImportAsync(BalancedDump, ErpnextImportOptions.Default with { FromPass = 6 });

        Assert.Equal(ImportRunStatus.Committed, run.Result.Status);
        Assert.Equal(ImportHaltReason.None, run.Result.HaltReason);
        var duration = Assert.Single(run.Result.Report.RunSummary.PassDurations);
        Assert.Equal("Pass 6 — Verification", duration.PassName);
        Assert.True(run.Committed);
    }

    // ─────────────────────────── harness ───────────────────────────

    private static async Task<RunResult> RunImportAsync(
        string dumpSql,
        ErpnextImportOptions options,
        Action<IServiceCollection>? customize = null)
    {
        var actor = PartyId.NewId();
        var tenant = new TenantId($"test-tenant-{Guid.NewGuid():N}");
        var services = new ServiceCollection();
        services.AddErpnextImportPipeline(MariaDbDumpSourceReader.FromSql(dumpSql), actor, tenant);
        customize?.Invoke(services);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ErpnextImportOrchestrator>();
        var result = await orchestrator.RunAsync(Request(actor, tenant, options));

        // The unit of work is a singleton, so the instance read here is the one the run drove.
        var unitOfWork = (InMemoryImportUnitOfWork)provider.GetRequiredService<IImportUnitOfWork>();
        return new RunResult(result, unitOfWork.Committed, unitOfWork.RolledBack);
    }

    private static ErpnextImportRequest Request(PartyId actor, TenantId tenant, ErpnextImportOptions options) =>
        new(
            Tenant: tenant,
            TargetChart: ChartOfAccountsId.NewId(),
            Actor: actor,
            AsOf: new DateOnly(2026, 12, 31),
            ControlAccounts: new ErpnextImportControlAccounts(
                ArControlAccount: GLAccountId.NewId(),
                ApControlAccount: GLAccountId.NewId(),
                DefaultIncomeAccount: GLAccountId.NewId(),
                DefaultExpenseAccount: GLAccountId.NewId()),
            Options: options,
            Snapshots: ErpnextVerificationSnapshots.None);

    private sealed record RunResult(ErpnextImportRunResult Result, bool Committed, bool RolledBack);

    /// <summary>A ledger read-model that reports a single non-zero net balance so the trial balance fails.</summary>
    private sealed class UnbalancedLedger : IGeneralLedgerReadModel
    {
        public Task<IReadOnlyDictionary<GLAccountId, decimal>> GetAccountBalancesAsOfAsync(
            TenantId tenantId,
            ChartOfAccountsId chartId,
            DateOnly asOf,
            string snapshotMarker,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<GLAccountId, decimal>>(
                new Dictionary<GLAccountId, decimal> { [GLAccountId.NewId()] = 100m });
    }
}
