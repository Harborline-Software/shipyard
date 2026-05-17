using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.Reports.Cartridges.ProfitAndLossByProperty;
using Sunfish.Blocks.Reports.Exceptions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// W#72 PR 5 — unit tests for <see cref="ProfitAndLossByPropertyCartridge"/>.
/// Seeds an <see cref="InMemoryJournalStore"/> with posted entries and asserts
/// revenue/expense/net-income projections, per-property bucketing, and filters.
/// </summary>
public sealed class ProfitAndLossByPropertyCartridgeTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly TenantId Tenant = new("tenant-pnl");
    private static readonly PrincipalId Principal = PrincipalId.FromBytes(new byte[32]);

    // Reference date used across tests.
    private static readonly DateOnly Today = new(2026, 5, 17);

    // ──────────────────────────────────────────────────────────────────
    //  Account seeds
    // ──────────────────────────────────────────────────────────────────

    private static GLAccount RevAcct(string code, string name = "Revenue")
        => GLAccount.Create(GLAccountId.NewId(), Chart, code, name,
            GLAccountType.Revenue, AccountSubtype.OperatingIncome, "USD");

    private static GLAccount ExpAcct(string code, string name = "Expense")
        => GLAccount.Create(GLAccountId.NewId(), Chart, code, name,
            GLAccountType.Expense, AccountSubtype.OperatingExpense, "USD");

    // ──────────────────────────────────────────────────────────────────
    //  Builder helpers
    // ──────────────────────────────────────────────────────────────────

    private static ReportExecutionContext Context(DateOnly? asOf = null)
    {
        var dt = asOf ?? Today;
        var utc = new DateTimeOffset(dt.Year, dt.Month, dt.Day, 12, 0, 0, TimeSpan.Zero);
        return new ReportExecutionContext(Tenant, "marker:pnl:1", utc, Principal);
    }

    private static (ProfitAndLossByPropertyCartridge Cartridge,
                    InMemoryJournalStore Journals,
                    InMemoryAccountResolver Accounts)
        Build(IEnumerable<GLAccount>? seedAccounts = null)
    {
        var journals = new InMemoryJournalStore();
        var accounts = new InMemoryAccountResolver(seedAccounts ?? Array.Empty<GLAccount>());
        var cartridge = new ProfitAndLossByPropertyCartridge(journals, accounts);
        return (cartridge, journals, accounts);
    }

    /// <summary>
    /// Post a revenue entry (credit revenue account, debit a clearing account).
    /// </summary>
    private static JournalEntry RevenueEntry(
        GLAccountId revenueAccountId,
        GLAccountId clearingAccountId,
        decimal amount,
        DateOnly date,
        string? propertyId = null)
    {
        PropertyId? propTag = propertyId is not null ? new PropertyId(propertyId) : default(PropertyId?);
        var lines = new[]
        {
            new JournalEntryLine(clearingAccountId, debit: amount, credit: 0m)
                with { PropertyId = propTag },
            new JournalEntryLine(revenueAccountId, debit: 0m, credit: amount)
                with { PropertyId = propTag },
        };
        return new JournalEntry(JournalEntryId.NewId(), date, "Revenue", lines, Instant.Now)
            with { Status = JournalEntryStatus.Posted, ChartId = Chart };
    }

    /// <summary>
    /// Post an expense entry (debit expense account, credit a clearing account).
    /// </summary>
    private static JournalEntry ExpenseEntry(
        GLAccountId expenseAccountId,
        GLAccountId clearingAccountId,
        decimal amount,
        DateOnly date,
        string? propertyId = null)
    {
        PropertyId? propTag = propertyId is not null ? new PropertyId(propertyId) : default(PropertyId?);
        var lines = new[]
        {
            new JournalEntryLine(expenseAccountId, debit: amount, credit: 0m)
                with { PropertyId = propTag },
            new JournalEntryLine(clearingAccountId, debit: 0m, credit: amount)
                with { PropertyId = propTag },
        };
        return new JournalEntry(JournalEntryId.NewId(), date, "Expense", lines, Instant.Now)
            with { Status = JournalEntryStatus.Posted, ChartId = Chart };
    }

    // Clearing account — asset type so it doesn't appear in P&L revenue/expense.
    private static readonly GLAccountId ClearingId = GLAccountId.NewId();

    // ──────────────────────────────────────────────────────────────────
    //  Edge case — empty chart
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_EmptyJournal_ReturnsZeroTotalsAndEmptyPropertyRows()
    {
        var rev = RevAcct("4000");
        var exp = ExpAcct("5000");
        var (sut, _, _) = Build(new[] { rev, exp });

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Empty(result.ByProperty);
        Assert.Equal(0m, result.Totals.TotalRevenue);
        Assert.Equal(0m, result.Totals.TotalExpenses);
        Assert.Equal(0m, result.Totals.NetIncome);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Revenue sign convention
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_SingleRevenueEntry_TotalRevenueIsPositive()
    {
        var rev = RevAcct("4000", "Rental Revenue");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 1000m, Today, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal(1000m, result.Totals.TotalRevenue);
        Assert.Equal(0m, result.Totals.TotalExpenses);
        Assert.Equal(1000m, result.Totals.NetIncome);

        var propRow = result.ByProperty.Single();
        Assert.Equal("prop-A", propRow.PropertyKey);
        Assert.Equal(1000m, propRow.TotalRevenue);
        Assert.Single(propRow.RevenueLines);
        Assert.Equal(1000m, propRow.RevenueLines[0].Amount);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Expense sign convention
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_SingleExpenseEntry_TotalExpensesIsPositive()
    {
        var exp = ExpAcct("5000", "Maintenance");
        var (sut, journals, _) = Build(new[] { exp });
        await journals.SaveAtomicAsync(ExpenseEntry(exp.Id, ClearingId, 500m, Today, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal(0m, result.Totals.TotalRevenue);
        Assert.Equal(500m, result.Totals.TotalExpenses);
        Assert.Equal(-500m, result.Totals.NetIncome);

        var propRow = result.ByProperty.Single();
        Assert.Equal(500m, propRow.TotalExpenses);
        Assert.Single(propRow.ExpenseLines);
        Assert.Equal(500m, propRow.ExpenseLines[0].Amount);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Net income = Revenue - Expenses
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_RevenueAndExpense_NetIncomeIsRevMinusExp()
    {
        var rev = RevAcct("4000");
        var exp = ExpAcct("5000");
        var (sut, journals, _) = Build(new[] { rev, exp });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 3000m, Today, "prop-A"));
        await journals.SaveAtomicAsync(ExpenseEntry(exp.Id, ClearingId, 1200m, Today, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        var row = result.ByProperty.Single(r => r.PropertyKey == "prop-A");
        Assert.Equal(3000m, row.TotalRevenue);
        Assert.Equal(1200m, row.TotalExpenses);
        Assert.Equal(1800m, row.NetIncome);
        Assert.Equal(1800m, result.Totals.NetIncome);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Property bucketing
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_MultipleProperties_EachBucketedSeparately()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 1000m, Today, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 2000m, Today, "prop-B"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal(2, result.ByProperty.Count);
        Assert.Equal(1000m, result.ByProperty.Single(r => r.PropertyKey == "prop-A").TotalRevenue);
        Assert.Equal(2000m, result.ByProperty.Single(r => r.PropertyKey == "prop-B").TotalRevenue);
        Assert.Equal(3000m, result.Totals.TotalRevenue);
    }

    [Fact]
    public async Task PnL_NullPropertyId_RolledIntoUnassigned()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 750m, Today, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        var unassigned = result.ByProperty.Single(r => r.PropertyKey == "Unassigned");
        Assert.Equal(750m, unassigned.TotalRevenue);
    }

    [Fact]
    public async Task PnL_UnassignedSortsLast()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 100m, Today, "prop-Z"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 50m, Today, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal("Unassigned", result.ByProperty.Last().PropertyKey);
    }

    [Fact]
    public async Task PnL_PropertyKeys_OrderedOrdinalAscendingUnassignedLast()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 100m, Today, "prop-C"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 100m, Today, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 100m, Today, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        var keys = result.ByProperty.Select(r => r.PropertyKey).ToList();
        Assert.Equal("prop-A", keys[0]);
        Assert.Equal("prop-C", keys[1]);
        Assert.Equal("Unassigned", keys[2]);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Period window filtering
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_EntryAfterPeriodEnd_Excluded()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        var periodEnd = new DateOnly(2026, 3, 31);
        // This entry is after the period end — should be excluded.
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 500m, new DateOnly(2026, 4, 1), "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart, PeriodEnd = periodEnd });

        Assert.Empty(result.ByProperty);
        Assert.Equal(0m, result.Totals.TotalRevenue);
    }

    [Fact]
    public async Task PnL_EntryBeforePeriodStart_Excluded()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        var periodStart = new DateOnly(2026, 4, 1);
        // This entry is before the period start — should be excluded.
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 500m, new DateOnly(2026, 3, 31), "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters
            {
                ChartId = Chart,
                PeriodStart = periodStart,
                PeriodEnd = Today,
            });

        Assert.Empty(result.ByProperty);
        Assert.Equal(0m, result.Totals.TotalRevenue);
    }

    [Fact]
    public async Task PnL_EntriesOnPeriodBoundaries_Included()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 3, 31);
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 100m, start, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 200m, end, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters
            {
                ChartId = Chart,
                PeriodStart = start,
                PeriodEnd = end,
            });

        Assert.Equal(300m, result.Totals.TotalRevenue);
    }

    [Fact]
    public async Task PnL_PeriodStartAfterPeriodEnd_ThrowsValidationException()
    {
        var (sut, _, _) = Build();
        await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            sut.ExecuteAsync(Context(),
                new ProfitAndLossByPropertyParameters
                {
                    ChartId = Chart,
                    PeriodStart = new DateOnly(2026, 12, 31),
                    PeriodEnd = new DateOnly(2026, 1, 1),
                }));
    }

    // ──────────────────────────────────────────────────────────────────
    //  Property filter
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_PropertyIdsFilter_OmitsOtherProperties()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 1000m, Today, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 2000m, Today, "prop-B"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters
            {
                ChartId = Chart,
                PropertyIds = new[] { "prop-A" },
            });

        Assert.Single(result.ByProperty);
        Assert.Equal("prop-A", result.ByProperty[0].PropertyKey);
        Assert.Equal(1000m, result.Totals.TotalRevenue);
    }

    [Fact]
    public async Task PnL_PropertyIdsFilter_ExcludesUnassigned()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 1000m, Today, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 500m, Today, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters
            {
                ChartId = Chart,
                PropertyIds = new[] { "prop-A" },
            });

        Assert.DoesNotContain(result.ByProperty, r => r.PropertyKey == "Unassigned");
        Assert.Equal(1000m, result.Totals.TotalRevenue);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Portfolio totals consistency
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_Totals_EqualSumOfByPropertyRows()
    {
        var rev = RevAcct("4000");
        var exp = ExpAcct("5000");
        var (sut, journals, _) = Build(new[] { rev, exp });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 1000m, Today, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 2000m, Today, "prop-B"));
        await journals.SaveAtomicAsync(ExpenseEntry(exp.Id, ClearingId, 400m, Today, "prop-A"));
        await journals.SaveAtomicAsync(ExpenseEntry(exp.Id, ClearingId, 600m, Today, propertyId: null));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        var sumRev = result.ByProperty.Sum(r => r.TotalRevenue);
        var sumExp = result.ByProperty.Sum(r => r.TotalExpenses);
        Assert.Equal(sumRev, result.Totals.TotalRevenue);
        Assert.Equal(sumExp, result.Totals.TotalExpenses);
        Assert.Equal(result.Totals.TotalRevenue - result.Totals.TotalExpenses, result.Totals.NetIncome);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Draft entries excluded
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_DraftEntry_Excluded()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        // Draft entry — should be ignored.
        var lines = new[]
        {
            new JournalEntryLine(ClearingId, debit: 500m, credit: 0m),
            new JournalEntryLine(rev.Id, debit: 0m, credit: 500m),
        };
        var draftEntry = new JournalEntry(JournalEntryId.NewId(), Today, "Draft", lines, Instant.Now)
            with { Status = JournalEntryStatus.Draft, ChartId = Chart };
        await journals.SaveAtomicAsync(draftEntry);

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal(0m, result.Totals.TotalRevenue);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Cross-chart isolation
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_EntriesForOtherChart_Excluded()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        var otherChart = ChartOfAccountsId.NewId();
        var lines = new[]
        {
            new JournalEntryLine(ClearingId, debit: 999m, credit: 0m),
            new JournalEntryLine(rev.Id, debit: 0m, credit: 999m),
        };
        var otherChartEntry = new JournalEntry(JournalEntryId.NewId(), Today, "Other", lines, Instant.Now)
            with { Status = JournalEntryStatus.Posted, ChartId = otherChart };
        await journals.SaveAtomicAsync(otherChartEntry);

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal(0m, result.Totals.TotalRevenue);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Account line ordering
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_RevenueLines_OrderedByAccountCodeOrdinal()
    {
        var rev1 = RevAcct("4200", "Late Fees");
        var rev2 = RevAcct("4000", "Rent");
        var (sut, journals, _) = Build(new[] { rev1, rev2 });
        await journals.SaveAtomicAsync(RevenueEntry(rev1.Id, ClearingId, 100m, Today, "prop-A"));
        await journals.SaveAtomicAsync(RevenueEntry(rev2.Id, ClearingId, 500m, Today, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        var row = result.ByProperty.Single();
        Assert.Equal("4000", row.RevenueLines[0].AccountCode);
        Assert.Equal("4200", row.RevenueLines[1].AccountCode);
    }

    // ──────────────────────────────────────────────────────────────────
    //  AsOfDate wiring (PeriodEnd defaults to context date)
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PnL_NullPeriodEnd_DefaultsToContextDate()
    {
        var rev = RevAcct("4000");
        var (sut, journals, _) = Build(new[] { rev });
        await journals.SaveAtomicAsync(RevenueEntry(rev.Id, ClearingId, 100m, Today, "prop-A"));

        var result = await sut.ExecuteAsync(Context(),
            new ProfitAndLossByPropertyParameters { ChartId = Chart });

        Assert.Equal(Today, result.PeriodEnd);
    }
}
