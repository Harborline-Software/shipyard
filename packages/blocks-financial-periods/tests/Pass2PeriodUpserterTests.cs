using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Migration;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Census;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;
using FyOutcome = Sunfish.Foundation.Import.Outcomes.ImportOutcome<Sunfish.Blocks.FinancialPeriods.Models.FiscalYear>;
using PeriodOutcome = Sunfish.Foundation.Import.Outcomes.ImportOutcome<Sunfish.Blocks.FinancialPeriods.Models.FiscalPeriod>;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// Fixture tests for the Pass-2 fiscal-year + fiscal-period upserter against
/// synthetic ERPNext-shaped fixtures (no real dump). Covers insert/update/skip
/// idempotency, the reject path, period synthesis, and census conservation
/// (ADR 0100 A2.2).
/// </summary>
public sealed class Pass2PeriodUpserterTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    private sealed class Harness
    {
        public InMemoryFiscalYearRepository Years { get; } = new();
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public Pass2PeriodUpserter Sut { get; }

        public Harness()
        {
            Sut = new Pass2PeriodUpserter(Years, Periods, TimeProvider.System);
        }
    }

    private static ErpnextFiscalYearSource NewSource(
        string name, string modified,
        DateOnly? start = null, DateOnly? end = null)
        => new(
            Name: name,
            Modified: modified,
            YearStartDate: start ?? new DateOnly(2026, 1, 1),
            YearEndDate: end ?? new DateOnly(2026, 12, 31),
            CompanyShortName: "Acme",
            IsShortYear: false);

    // ── Fiscal-year upsert ────────────────────────────────────────────────

    [Fact]
    public async Task UpsertFiscalYear_NewSource_Inserts()
    {
        var h = new Harness();
        var outcome = await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);

        var inserted = Assert.IsType<FyOutcome.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.Equal(FiscalYearStatus.Open, inserted.Record.Status);
        Assert.Equal(Chart, inserted.Record.ChartId);
        Assert.Equal("FY-2026", inserted.Record.ExternalRef);
    }

    [Fact]
    public async Task UpsertFiscalYear_SameVersion_Skips_Idempotent()
    {
        var h = new Harness();
        var source = NewSource("FY-2026", "2026-04-15 10:00:00");
        await h.Sut.UpsertFiscalYearAsync(source, Chart);

        var again = await h.Sut.UpsertFiscalYearAsync(source, Chart);
        Assert.IsType<FyOutcome.Skipped>(again);
        Assert.Equal(ImportAction.Skipped, again.Action);
    }

    [Fact]
    public async Task UpsertFiscalYear_HigherVersion_Updates()
    {
        var h = new Harness();
        await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);

        var outcome = await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-05-20 10:00:00"), Chart);
        Assert.IsType<FyOutcome.Updated>(outcome);
    }

    [Fact]
    public async Task UpsertFiscalYear_LowerVersion_Skips()
    {
        var h = new Harness();
        await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-05-20 10:00:00"), Chart);

        var outcome = await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);
        Assert.IsType<FyOutcome.Skipped>(outcome);
    }

    [Fact]
    public async Task UpsertFiscalYear_UnparseableModified_Rejected_UnparseableSource()
    {
        var h = new Harness();
        var outcome = await h.Sut.UpsertFiscalYearAsync(NewSource("FY-BAD", "not-a-timestamp"), Chart);

        var rejected = Assert.IsType<FyOutcome.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Null(outcome.Action);
        Assert.Equal("FY-BAD", rejected.Failure.ExternalRef);
        Assert.Equal("Fiscal Year", rejected.Failure.DocType);
        Assert.Equal(nameof(ImportRejectReason.UnparseableSource), rejected.Failure.ReasonCode);
        Assert.Equal("modified", rejected.Failure.FieldName);
    }

    [Fact]
    public async Task UpsertFiscalYear_StartAfterEnd_Rejected_ConstraintViolation()
    {
        var h = new Harness();
        var outcome = await h.Sut.UpsertFiscalYearAsync(
            NewSource("FY-BAD-DATES", "2026-04-15 10:00:00",
                start: new DateOnly(2026, 12, 31), end: new DateOnly(2026, 1, 1)),
            Chart);

        var rejected = Assert.IsType<FyOutcome.Rejected>(outcome);
        Assert.Equal(nameof(ImportRejectReason.ConstraintViolation), rejected.Failure.ReasonCode);
    }

    // ── Period synthesis ──────────────────────────────────────────────────

    [Fact]
    public async Task SynthesizePeriods_FreshFy_InsertsTwelveMonthly()
    {
        var h = new Harness();
        var fy = (FyOutcome.Inserted)await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);

        var outcomes = await h.Sut.SynthesizePeriodsAsync(fy.Record.Id);

        Assert.Equal(12, outcomes.Count);
        Assert.All(outcomes, o => Assert.IsType<PeriodOutcome.Inserted>(o));
        var stored = await h.Periods.GetByFiscalYearAsync(fy.Record.Id);
        Assert.Equal(12, stored.Count);
        Assert.All(stored, p => Assert.Equal(FiscalPeriodStatus.Open, p.Status));
    }

    [Fact]
    public async Task SynthesizePeriods_AlreadySynthesized_SkipsAll_Idempotent()
    {
        var h = new Harness();
        var fy = (FyOutcome.Inserted)await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);
        await h.Sut.SynthesizePeriodsAsync(fy.Record.Id);

        var again = await h.Sut.SynthesizePeriodsAsync(fy.Record.Id);
        Assert.Equal(12, again.Count);
        Assert.All(again, o => Assert.IsType<PeriodOutcome.Skipped>(o));
    }

    [Fact]
    public async Task SynthesizePeriods_UnknownFy_ReturnsEmpty()
    {
        var h = new Harness();
        var outcomes = await h.Sut.SynthesizePeriodsAsync(FiscalYearId.NewId());
        Assert.Empty(outcomes);
    }

    [Fact]
    public async Task SynthesizePeriods_Quarterly_InsertsFour()
    {
        var h = new Harness();
        var fy = (FyOutcome.Inserted)await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);

        var outcomes = await h.Sut.SynthesizePeriodsAsync(fy.Record.Id, FiscalPeriodKind.Quarterly);
        Assert.Equal(4, outcomes.Count);
        Assert.All(outcomes, o => Assert.IsType<PeriodOutcome.Inserted>(o));
    }

    // ── Census conservation ───────────────────────────────────────────────

    [Fact]
    public async Task Census_FiscalYears_Conserved()
    {
        var h = new Harness();
        var census = new ImportCensus();

        // 4 FY source records: 2 inserts, 1 re-import (skip), 1 unparseable (reject).
        var fy1 = NewSource("FY-A", "2026-04-15 10:00:00", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        var fy2 = NewSource("FY-B", "2026-04-15 10:00:00");
        var bad = NewSource("FY-C", "garbage");

        census.Record(await h.Sut.UpsertFiscalYearAsync(fy1, Chart)); // Inserted
        census.Record(await h.Sut.UpsertFiscalYearAsync(fy2, Chart)); // Inserted
        census.Record(await h.Sut.UpsertFiscalYearAsync(fy1, Chart)); // Skipped
        census.Record(await h.Sut.UpsertFiscalYearAsync(bad, Chart)); // Rejected

        Assert.Equal(2, census.Inserted);
        Assert.Equal(1, census.Skipped);
        Assert.Equal(1, census.Rejected);
        Assert.Equal(4, census.Accounted);
        census.AssertConserved(4);
    }

    [Fact]
    public async Task Census_PeriodSynthesis_ConservedAgainstSourceCount()
    {
        var h = new Harness();
        var census = new ImportCensus();
        var fy = (FyOutcome.Inserted)await h.Sut.UpsertFiscalYearAsync(NewSource("FY-2026", "2026-04-15 10:00:00"), Chart);

        var outcomes = await h.Sut.SynthesizePeriodsAsync(fy.Record.Id);
        foreach (var o in outcomes) census.Record(o);

        // 12 synthesized periods all accounted for as inserts.
        Assert.Equal(12, census.Inserted);
        census.AssertConserved(outcomes.Count);
    }
}
