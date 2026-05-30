using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.Migration.Erpnext.Extraction;
using Sunfish.Blocks.Migration.Erpnext.Reconciliation;
using Sunfish.Blocks.Migration.Erpnext.Reporting;
using Sunfish.Blocks.Migration.Erpnext.Verification;
using Sunfish.Foundation.Import.Extraction;
using Sunfish.Foundation.Import.Outcomes;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Reporting;

/// <summary>
/// Unit coverage for <see cref="MigrationReportRenderer"/> — importer Pass 6's pure markdown
/// renderer (spec §4.6 step 6). Asserts the report is structurally complete (all nine sections
/// present), deterministic (same input → byte-identical output, scrambled collections sorted),
/// renders the F2 null-currency-assumed-USD warning (carried from shipyard#205), and surfaces the
/// two halting conditions with their CO-facing labels. No disk, no clock — the renderer is pure.
/// </summary>
public sealed class MigrationReportRendererTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 5, 16, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Renders_all_nine_sections_in_order()
    {
        var markdown = MigrationReportRenderer.Render(PassedInput());

        Assert.Contains("# ERPNext → Sunfish Migration Report", markdown);
        var headings = new[]
        {
            "## 1. Run Summary",
            "## 2. Trial Balance",
            "## 3. AR Aging Diff",
            "## 4. AP Aging Diff",
            "## 5. Per-Account Balance Diff",
            "## 6. Reject-Bin Summary",
            "## 7. Unapplied Payments",
            "## 8. Cost-Center Resolution",
            "## 9. Warnings",
        };
        var lastIndex = -1;
        foreach (var heading in headings)
        {
            var index = markdown.IndexOf(heading, StringComparison.Ordinal);
            Assert.True(index >= 0, $"missing section heading: {heading}");
            Assert.True(index > lastIndex, $"section out of order: {heading}");
            lastIndex = index;
        }
    }

    [Fact]
    public void Passed_run_renders_passed_overall_result()
    {
        var markdown = MigrationReportRenderer.Render(PassedInput());

        Assert.Contains("**Overall result:** PASSED", markdown);
        Assert.Contains("**BALANCED**", markdown);
    }

    [Fact]
    public void Trial_balance_mismatch_renders_failed_label_and_hard_halt_note()
    {
        var input = PassedInput() with
        {
            Verification = VerificationResult(
                VerificationOutcome.TrialBalanceMismatch,
                trialBalance: new TrialBalanceResult(
                    SignedTotal: 150.00m,
                    Subtotals: new[] { new AccountTypeSubtotal(GLAccountType.Asset, 150.00m) },
                    UnclassifiedAccountCount: 0)),
        };

        var markdown = MigrationReportRenderer.Render(input);

        Assert.Contains("**Overall result:** FAILED — trial balance mismatch (run rolled back)", markdown);
        Assert.Contains("**MISMATCH** — signed total $150.00", markdown);
        Assert.Contains("Hard halt", markdown);
    }

    [Fact]
    public void Renders_null_currency_assumed_usd_warning_with_count()
    {
        // F2 (.NET-architect finding carried from shipyard#205): the silent null→USD assumption
        // must be auditable in the report.
        var input = PassedInput() with
        {
            Warnings = new[] { ReportWarning.NullCurrencyAssumedUsd(7) },
        };

        var markdown = MigrationReportRenderer.Render(input);

        Assert.Contains("**NullCurrencyAssumedUsd** (7):", markdown);
        Assert.Contains("assumed to be the chart base currency (USD)", markdown);
        Assert.DoesNotContain("No warnings.", markdown);
    }

    [Fact]
    public void Empty_warnings_renders_no_warnings_line()
    {
        var markdown = MigrationReportRenderer.Render(PassedInput());

        Assert.Contains("## 9. Warnings", markdown);
        Assert.Contains("No warnings.", markdown);
    }

    [Fact]
    public void Output_is_deterministic_for_identical_input()
    {
        var input = PassedInput();

        var first = MigrationReportRenderer.Render(input);
        var second = MigrationReportRenderer.Render(input);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Per_account_diffs_are_sorted_by_absolute_difference_descending()
    {
        var input = PassedInput() with
        {
            Verification = VerificationResult(
                VerificationOutcome.Passed,
                accountBalances: new AccountBalanceDiffResult(
                    Checked: true,
                    Diffs: new[]
                    {
                        new AccountBalanceDiff("1000", 0m, 5.00m),     // |5|
                        new AccountBalanceDiff("2000", 0m, 50.00m),    // |50|
                        new AccountBalanceDiff("3000", 0m, 0.50m),     // |0.5|
                    })),
        };

        var markdown = MigrationReportRenderer.Render(input);

        var i2000 = markdown.IndexOf("| 2000 |", StringComparison.Ordinal);
        var i1000 = markdown.IndexOf("| 1000 |", StringComparison.Ordinal);
        var i3000 = markdown.IndexOf("| 3000 |", StringComparison.Ordinal);
        Assert.True(i2000 >= 0 && i1000 >= 0 && i3000 >= 0);
        Assert.True(i2000 < i1000, "largest abs diff should sort first");
        Assert.True(i1000 < i3000, "smallest abs diff should sort last");
    }

    [Fact]
    public void Doctype_census_surfaces_unmapped_count_and_sorts_entries()
    {
        var inventory = new ErpnextSourceInventory(
            new[]
            {
                new ErpnextDocTypeCensusEntry("Sales Invoice", ErpnextDocTypeClassification.Mapped, 1200),
                new ErpnextDocTypeCensusEntry("Custom Widget", ErpnextDocTypeClassification.UnmappedUnknown, 4),
                new ErpnextDocTypeCensusEntry("Series", ErpnextDocTypeClassification.KnownIrrelevant, 900),
            },
            SourceAccessMode.MariaDbDump);
        var input = PassedInput() with
        {
            RunSummary = new RunSummary("run-xyz", GeneratedAt, inventory, Array.Empty<PassDuration>()),
        };

        var markdown = MigrationReportRenderer.Render(input);

        Assert.Contains("Mapped DocTypes: 1", markdown);
        Assert.Contains("Known-irrelevant DocTypes: 1", markdown);
        Assert.Contains("Unmapped-unknown DocTypes (see `_unmapped/`): 1", markdown);
        // Sort is by Classification (Mapped=0 first) then DocType ordinal.
        var mapped = markdown.IndexOf("| Sales Invoice |", StringComparison.Ordinal);
        var irrelevant = markdown.IndexOf("| Series |", StringComparison.Ordinal);
        var unmapped = markdown.IndexOf("| Custom Widget |", StringComparison.Ordinal);
        Assert.True(mapped < irrelevant && irrelevant < unmapped, "census not ordered by classification");
    }

    [Fact]
    public void Reject_bin_groups_by_reason_and_totals()
    {
        var input = PassedInput() with
        {
            RejectBin = new[]
            {
                ImportFailure.Of("INV-1", "Sales Invoice", ImportRejectReason.UnsupportedCurrency),
                ImportFailure.Of("INV-2", "Sales Invoice", ImportRejectReason.UnsupportedCurrency),
                ImportFailure.Of("JE-1", "Journal Entry", ImportRejectReason.ConstraintViolation),
            },
        };

        var markdown = MigrationReportRenderer.Render(input);

        Assert.Contains("| UnsupportedCurrency | 2 |", markdown);
        Assert.Contains("| ConstraintViolation | 1 |", markdown);
        Assert.Contains("Total rejected: 3.", markdown);
    }

    [Fact]
    public void Unapplied_payments_lists_only_non_applied_outcomes()
    {
        var reconciliation = new ReconciliationPassResult(new[]
        {
            PaymentReconciliationOutcome.Applied(new PaymentId("pay-applied"), AppliedTo.Invoice, "INV-9", 100m),
            PaymentReconciliationOutcome.Unmatched(new PaymentId("pay-unmatched")),
            PaymentReconciliationOutcome.Ambiguous(
                new PaymentId("pay-ambiguous"), AppliedTo.Invoice, new[] { "INV-1", "INV-2" }),
        });
        var input = PassedInput() with { Reconciliation = reconciliation };

        var markdown = MigrationReportRenderer.Render(input);

        Assert.Contains("3 unapplied payments: 1 applied, 1 ambiguous, 1 unmatched.", markdown);
        Assert.Contains("| pay-unmatched | Unmatched |", markdown);
        Assert.Contains("| pay-ambiguous | Ambiguous | INV-1, INV-2 |", markdown);
        // The applied payment must NOT appear in the unapplied table.
        Assert.DoesNotContain("pay-applied", markdown);
    }

    [Fact]
    public void Cost_center_resolutions_render_counts_and_rows()
    {
        var input = PassedInput() with
        {
            CostCenterResolutions = new[]
            {
                CostCenterResolution.ToProperty("CC-Main", PropertyId.NewId()),
            },
        };

        var markdown = MigrationReportRenderer.Render(input);

        Assert.Contains("1 cost-centers resolved: 1 → existing property, 0 → new classification.", markdown);
        Assert.Contains("| CC-Main | Property", markdown);
    }

    // ── Builders ──────────────────────────────────────────────────────────

    /// <summary>A fully-populated, all-clear PASSED report input; tests override single arms via `with`.</summary>
    private static MigrationReportInput PassedInput() =>
        new(
            RunSummary: new RunSummary(
                "run-abc",
                GeneratedAt,
                new ErpnextSourceInventory(Array.Empty<ErpnextDocTypeCensusEntry>(), SourceAccessMode.MariaDbDump),
                new[] { new PassDuration("Pass 6 — Verification", TimeSpan.FromSeconds(1.5)) }),
            Verification: VerificationResult(VerificationOutcome.Passed),
            Reconciliation: new ReconciliationPassResult(Array.Empty<PaymentReconciliationOutcome>()),
            RejectBin: Array.Empty<ImportFailure>(),
            CostCenterResolutions: Array.Empty<CostCenterResolution>(),
            Warnings: Array.Empty<ReportWarning>());

    /// <summary>Builds a <see cref="VerificationResult"/>, defaulting every arm to its all-clear shape.</summary>
    private static VerificationResult VerificationResult(
        VerificationOutcome outcome,
        TrialBalanceResult? trialBalance = null,
        AgingDiffResult? arAging = null,
        AgingDiffResult? apAging = null,
        AccountBalanceDiffResult? accountBalances = null,
        InvoiceBalanceCheckResult? invoiceBalances = null) =>
        new(
            outcome,
            trialBalance ?? new TrialBalanceResult(0m, Array.Empty<AccountTypeSubtotal>(), 0),
            arAging ?? AgingDiffResult.NotChecked,
            apAging ?? AgingDiffResult.NotChecked,
            accountBalances ?? AccountBalanceDiffResult.NotChecked,
            invoiceBalances ?? new InvoiceBalanceCheckResult(0, Array.Empty<InvoiceBalanceDiscrepancy>()));
}
