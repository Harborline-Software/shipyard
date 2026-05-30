// A7-5: CLI host for the ERPNext->Sunfish importer (ADR 0100, spec §8).
//
// Command tree mirrors spec §8 verbatim:
//   sunfish-migrate import erpnext --source <dir> [options]
//
// This host is deliberately thin. It owns ONLY:
//   - flag parsing (System.CommandLine),
//   - disk I/O (resolve the .sql dump under --source; parse the optional CO
//     verification-snapshot JSONs; write migration-report.md back into --source),
//   - DI composition (AddErpnextImportPipeline) + resolving the orchestrator from a scope.
// ALL import logic lives in Sunfish.Blocks.Migration.Erpnext — the host never touches a pass.
//
// Exit codes (sysexits.h-aligned, mirrors the sibling Sunfish.Tooling.Loc* tools):
//   0   — committed, OR a --dry-run that rolled back by design
//   1   — the run rolled back on a real halt (trial-balance / aging / reject-threshold),
//         or any unhandled failure (System.CommandLine's default exception middleware)
//   64  — usage error (bad --source, --from-pass out of range)
//   70  — not implemented (reserved; the erpnext path is fully implemented)
//
// CLEAN-ROOM / ADR 0100: the dump is opened READ-ONLY and lives OUTSIDE the repo at the
// CIC-supplied --source path; nothing is ever written back to ERPNext. Progress lines stay
// allowlist-clean (DocType / opaque ref / counts — never PII or amounts). RUN against a real
// dump is CIC-dump-gated (shipyard#270); BUILD + tests use synthetic v15 fixtures only.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.Migration.Erpnext.DependencyInjection;
using Sunfish.Blocks.Migration.Erpnext.Orchestration;
using Sunfish.Blocks.Migration.Erpnext.Reporting;
using Sunfish.Blocks.Migration.Erpnext.Verification;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Extraction;

namespace Sunfish.Tooling.ErpnextImport;

internal static class Program
{
    internal const int ExitSuccess = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUsage = 64;
    internal const int ExitNotImplemented = 70;

    internal const string ToolName = "Sunfish.Tooling.ErpnextImport";
    internal const string ToolVersion = "v0.1.0";

    // The three optional CO-prepared verification snapshots Pass 6 diffs against (spec §4.6).
    private const string ArAgingSnapshotFile = "ar-aging-snapshot.json";
    private const string ApAgingSnapshotFile = "ap-aging-snapshot.json";
    private const string GlBalancesSnapshotFile = "gl-balances-snapshot.json";
    private const string ReportFileName = "migration-report.md";

    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine($"{ToolName} {ToolVersion}");
            return ExitSuccess;
        }

        var root = BuildRootCommand();
        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    internal static RootCommand BuildRootCommand()
    {
        var root = new RootCommand(
            "Sunfish migration CLI — imports an external ERP's accounting data into a Sunfish " +
            "chart of accounts. See the ERPNext-to-Anchor migration importer spec §8.");

        var importCmd = new Command(
            "import",
            "Import accounting data from an external ERP into Sunfish.");
        importCmd.AddCommand(BuildErpnextCommand());

        root.AddCommand(importCmd);
        return root;
    }

    // ---------------------------------------------------------------------
    // `sunfish-migrate import erpnext --source <dir> [options]`
    // ---------------------------------------------------------------------
    private static Command BuildErpnextCommand()
    {
        var sourceOption = new Option<string>(
            name: "--source",
            description: "Export directory holding the ERPNext MariaDB .sql dump (and, optionally, " +
                         "the CO verification-snapshot JSONs). May also point directly at the .sql file. " +
                         "Opened READ-ONLY; migration-report.md is written here.")
        {
            IsRequired = true,
        };

        var targetChartOption = new Option<string?>(
            name: "--target-chart",
            description: "Destination chart-of-accounts id this run imports into. Omit to synthesize a " +
                         "fresh chart id (the multi-company manifest loop is deferred past Phase 1).");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Run all six passes against the in-memory substrate, render the report, then roll " +
                         "back — commit nothing.");

        var allowAgingDriftOption = new Option<bool>(
            name: "--allow-aging-drift",
            description: "Accept Pass 6 AR/AP aging diffs over threshold without halting; the diff is still " +
                         "reported.");

        var allowMultiCurrencySkipOption = new Option<bool>(
            name: "--allow-multi-currency-skip",
            description: "Skip rather than reject transactional records whose currency differs from the chart " +
                         "base currency.");

        var fromPassOption = new Option<int>(
            name: "--from-pass",
            getDefaultValue: () => 1,
            description: "Resume from pass N (1..6); earlier passes are skipped. Default 1.");

        var rejectThresholdOption = new Option<int?>(
            name: "--reject-threshold",
            description: "Halt-and-roll-back at the commit gate if total rejects across all passes exceed N. " +
                         "Omit for unlimited (spec default).");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Emit per-record outcome lines (DocType + opaque ref + action) instead of one line " +
                         "per pass.");

        var cmd = new Command(
            "erpnext",
            "Import an ERPNext v15 export into a Sunfish chart of accounts (six-pass importer, spec §8).")
        {
            sourceOption,
            targetChartOption,
            dryRunOption,
            allowAgingDriftOption,
            allowMultiCurrencySkipOption,
            fromPassOption,
            rejectThresholdOption,
            verboseOption,
        };

        // 8 options exceed the typed SetHandler arity AND the typed overloads discard the
        // handler's return value (it cannot set the exit code). Use the InvocationContext
        // overload: read options off ParseResult and set ctx.ExitCode explicitly.
        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await RunImportAsync(
                source: ctx.ParseResult.GetValueForOption(sourceOption)!,
                targetChart: ctx.ParseResult.GetValueForOption(targetChartOption),
                dryRun: ctx.ParseResult.GetValueForOption(dryRunOption),
                allowAgingDrift: ctx.ParseResult.GetValueForOption(allowAgingDriftOption),
                allowMultiCurrencySkip: ctx.ParseResult.GetValueForOption(allowMultiCurrencySkipOption),
                fromPass: ctx.ParseResult.GetValueForOption(fromPassOption),
                rejectThreshold: ctx.ParseResult.GetValueForOption(rejectThresholdOption),
                verbose: ctx.ParseResult.GetValueForOption(verboseOption),
                ct: ctx.GetCancellationToken()).ConfigureAwait(false);
        });

        return cmd;
    }

    private static async Task<int> RunImportAsync(
        string source,
        string? targetChart,
        bool dryRun,
        bool allowAgingDrift,
        bool allowMultiCurrencySkip,
        int fromPass,
        int? rejectThreshold,
        bool verbose,
        CancellationToken ct)
    {
        if (fromPass is < 1 or > 6)
        {
            Console.Error.WriteLine($"--from-pass must be between 1 and 6 (received {fromPass}).");
            return ExitUsage;
        }

        if (!TryResolveDump(source, out var exportRoot, out var dumpPath, out var resolveError))
        {
            Console.Error.WriteLine(resolveError);
            return ExitUsage;
        }

        // Read-only load; never connects to a database (clean-room C4).
        ISourceReader reader = await MariaDbDumpSourceReader.LoadAsync(dumpPath, ct).ConfigureAwait(false);

        var snapshots = LoadSnapshots(exportRoot);

        // Synthesized inputs — spec §8 exposes no flag for tenant, actor, or control accounts.
        // RUN against a real chart is CIC-dump-gated (#270); the RUN-enablement increment resolves
        // real control accounts from the target chart (architecture Finding 3). For the BUILD /
        // --dry-run path these are well-formed placeholders, and A7-6 tests build requests directly.
        var actor = PartyId.NewId();
        var request = new ErpnextImportRequest(
            Tenant: new TenantId("erpnext-import"),
            TargetChart: string.IsNullOrWhiteSpace(targetChart)
                ? ChartOfAccountsId.NewId()
                : new ChartOfAccountsId(targetChart),
            Actor: actor,
            AsOf: DateOnly.FromDateTime(DateTime.UtcNow),
            ControlAccounts: new ErpnextImportControlAccounts(
                ArControlAccount: GLAccountId.NewId(),
                ApControlAccount: GLAccountId.NewId(),
                DefaultIncomeAccount: GLAccountId.NewId(),
                DefaultExpenseAccount: GLAccountId.NewId()),
            Options: new ErpnextImportOptions(
                DryRun: dryRun,
                AllowAgingDrift: allowAgingDrift,
                AllowMultiCurrencySkip: allowMultiCurrencySkip,
                FromPass: fromPass,
                RejectThreshold: rejectThreshold,
                Verbose: verbose),
            Snapshots: snapshots);

        var services = new ServiceCollection();
        services.AddErpnextImportPipeline(reader, actor, request.Tenant);
        await using var provider = services.BuildServiceProvider();

        // The reconciliation applier (and therefore the orchestrator) is scoped — resolve from a scope.
        using var scope = provider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ErpnextImportOrchestrator>();

        IImportProgress progress = new ConsoleImportProgress(verbose);
        ErpnextImportRunResult result =
            await orchestrator.RunAsync(request, progress, ct).ConfigureAwait(false);

        var reportPath = Path.Combine(exportRoot, ReportFileName);
        var markdown = MigrationReportRenderer.Render(result.Report);
        await File.WriteAllTextAsync(reportPath, markdown, ct).ConfigureAwait(false);
        progress.RunFinished(reportPath);

        return MapExitCode(result);
    }

    // --source may be the export directory (then we locate the single .sql within it) or the
    // .sql file directly (then the export root is its containing directory).
    private static bool TryResolveDump(
        string source,
        out string exportRoot,
        out string dumpPath,
        out string? error)
    {
        exportRoot = string.Empty;
        dumpPath = string.Empty;
        error = null;

        if (Directory.Exists(source))
        {
            exportRoot = Path.GetFullPath(source);
            var sqlFiles = Directory.GetFiles(exportRoot, "*.sql", SearchOption.TopDirectoryOnly);
            if (sqlFiles.Length == 0)
            {
                error = $"No .sql dump found in export directory: {exportRoot}";
                return false;
            }

            if (sqlFiles.Length > 1)
            {
                error = $"Expected exactly one .sql dump in export directory but found {sqlFiles.Length}: {exportRoot}";
                return false;
            }

            dumpPath = sqlFiles[0];
            return true;
        }

        if (File.Exists(source))
        {
            dumpPath = Path.GetFullPath(source);
            exportRoot = Path.GetDirectoryName(dumpPath)!;
            return true;
        }

        error = $"--source path not found: {source}";
        return false;
    }

    private static ErpnextVerificationSnapshots LoadSnapshots(string exportRoot)
    {
        var ar = LoadArAging(Path.Combine(exportRoot, ArAgingSnapshotFile));
        var ap = LoadApAging(Path.Combine(exportRoot, ApAgingSnapshotFile));
        var gl = LoadGlBalances(Path.Combine(exportRoot, GlBalancesSnapshotFile));

        if (ar is null && ap is null && gl is null)
        {
            return ErpnextVerificationSnapshots.None;
        }

        return new ErpnextVerificationSnapshots(ar, ap, gl);
    }

    private static ArAgingSnapshot? LoadArAging(string path)
    {
        var dto = ReadSnapshot<ArAgingDto>(path);
        return dto?.Customers is null
            ? null
            : new ArAgingSnapshot(dto.Customers.Select(MapAgingRow).ToList());
    }

    private static ApAgingSnapshot? LoadApAging(string path)
    {
        var dto = ReadSnapshot<ApAgingDto>(path);
        return dto?.Vendors is null
            ? null
            : new ApAgingSnapshot(dto.Vendors.Select(MapAgingRow).ToList());
    }

    private static GlBalancesSnapshot? LoadGlBalances(string path)
    {
        var dto = ReadSnapshot<GlBalancesDto>(path);
        return dto?.Accounts is null
            ? null
            : new GlBalancesSnapshot(dto.Accounts
                .Select(a => new AccountBalanceSnapshotRow(a.AccountCode, a.SignedBalance))
                .ToList());
    }

    private static T? ReadSnapshot<T>(string path) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, SnapshotJsonOptions);
    }

    private static PartyAgingSnapshotRow MapAgingRow(PartyAgingRowDto d) =>
        new(new PartyId(d.PartyId), d.Current, d.Days0To30, d.Days31To60, d.Days61To90, d.Days90Plus);

    private static int MapExitCode(ErpnextImportRunResult result) => result.Status switch
    {
        ImportRunStatus.Committed => ExitSuccess,
        // A dry run rolls back by design — that's a success, not a failure.
        ImportRunStatus.RolledBack when result.HaltReason == ImportHaltReason.DryRun => ExitSuccess,
        // Any other rollback (trial-balance mismatch, aging failure, reject threshold) is a failure.
        _ => ExitFailure,
    };

    // On-disk JSON shapes for the optional CO snapshots, decoupled from the domain records so the
    // file schema uses bare strings (e.g. "partyId": "CUST-001") rather than the id record's shape.
    private sealed record PartyAgingRowDto(
        string PartyId,
        decimal Current,
        decimal Days0To30,
        decimal Days31To60,
        decimal Days61To90,
        decimal Days90Plus);

    private sealed record ArAgingDto(IReadOnlyList<PartyAgingRowDto>? Customers);

    private sealed record ApAgingDto(IReadOnlyList<PartyAgingRowDto>? Vendors);

    private sealed record AccountBalanceRowDto(string AccountCode, decimal SignedBalance);

    private sealed record GlBalancesDto(IReadOnlyList<AccountBalanceRowDto>? Accounts);
}

/// <summary>
/// Line-based <see cref="IImportProgress"/> for the terminal (spec §8.2): one status line per
/// pass / sub-pass on stderr, an in-place percentage bar on a TTY (with a plain non-TTY fallback),
/// and per-record outcome lines only under <c>--verbose</c>. Every method is no-throw and cheap so a
/// broken writer can never fail the import.
/// </summary>
internal sealed class ConsoleImportProgress : IImportProgress
{
    private readonly bool _verbose;
    private readonly bool _isTty;
    private string? _openStepLabel;

    public ConsoleImportProgress(bool verbose)
    {
        _verbose = verbose;
        _isTty = !Console.IsErrorRedirected;
    }

    public void RunStarting(string sourceLabel, string targetLabel)
    {
        Console.Error.WriteLine("ERPNext import");
        Console.Error.WriteLine($"  source: {sourceLabel}");
        Console.Error.WriteLine($"  target: {targetLabel}");
        Console.Error.WriteLine();
    }

    public void StepStarting(string stepLabel)
    {
        _openStepLabel = stepLabel;
        if (_isTty)
        {
            Console.Error.Write($"  {stepLabel} ...");
        }
    }

    public void StepProgress(string stepLabel, int completed, int total)
    {
        if (!_isTty || total <= 0)
        {
            return;
        }

        var pct = (int)(100.0 * completed / total);
        Console.Error.Write($"\r  {stepLabel} ... {pct,3}% ({completed}/{total})");
    }

    public void StepCompleted(string stepLabel, string resultSummary)
    {
        if (_isTty && string.Equals(_openStepLabel, stepLabel, StringComparison.Ordinal))
        {
            Console.Error.Write('\r');
        }

        Console.Error.WriteLine($"  {stepLabel} ... {resultSummary}");
        _openStepLabel = null;
    }

    public void SubStepCompleted(string subStepLabel, string resultSummary)
    {
        Console.Error.WriteLine($"      {subStepLabel} ... {resultSummary}");
    }

    public void Verbose(string line)
    {
        if (_verbose)
        {
            Console.Error.WriteLine($"      · {line}");
        }
    }

    public void RunFinished(string reportPath)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"  report: {reportPath}");
    }
}
