using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.RecurringBilling.Models;
using Sunfish.Blocks.RecurringBilling.Services;
using Xunit;

namespace Sunfish.Blocks.RecurringBilling.Tests;

public class RecurringLedgerBlockTests : BunitContext
{
    public RecurringLedgerBlockTests()
    {
        Services.AddSingleton<IRecurringBillingService, InMemoryRecurringBillingService>();
    }

    [Fact]
    public void RecurringLedgerBlock_EmptyService_RendersNoInvoicesPlaceholder()
    {
        var cut = Render<RecurringLedgerBlock>();

        Assert.Contains("No invoices", cut.Markup);
    }

    [Fact]
    public async Task RecurringLedgerBlock_WithInvoices_RendersExpectedRows()
    {
        // Pre-populate the in-memory service before rendering.
        var svc = (InMemoryRecurringBillingService)Services.GetRequiredService<IRecurringBillingService>();

        var schedule = await svc.CreateScheduleAsync(new CreateScheduleRequest(
            LeaseId: "lease-abc",
            StartDate: new DateOnly(2025, 1, 1),
            EndDate: null,
            MonthlyAmount: 1500m,
            DueDayOfMonth: 1));

        await svc.GenerateInvoiceAsync(schedule.Id, new DateOnly(2025, 1, 1));
        await svc.GenerateInvoiceAsync(schedule.Id, new DateOnly(2025, 2, 1));

        var cut = Render<RecurringLedgerBlock>();

        // Two rows should be present, each containing the lease ID.
        var rows = cut.FindAll("tr.sf-rent-ledger__row");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Contains("lease-abc", row.TextContent));
    }

    [Fact]
    public async Task RecurringLedgerBlock_OverdueInvoice_ShowsPositiveAgingDays()
    {
        var svc = (InMemoryRecurringBillingService)Services.GetRequiredService<IRecurringBillingService>();

        var schedule = await svc.CreateScheduleAsync(new CreateScheduleRequest(
            LeaseId: "lease-overdue",
            StartDate: new DateOnly(2024, 1, 1),
            EndDate: null,
            MonthlyAmount: 800m,
            DueDayOfMonth: 1));

        // Invoice for Jan 2024 — well in the past.
        await svc.GenerateInvoiceAsync(schedule.Id, new DateOnly(2024, 1, 1));

        // Set Today so the block computes a deterministic aging value.
        var today = new DateOnly(2025, 6, 1);
        var cut = Render<RecurringLedgerBlock>(p => p
            .Add(b => b.Today, today));

        // The aging cell should not show "-" — it should show a positive number.
        var agingCells = cut.FindAll("td");
        // The 6th td per row is the aging column; pick the last td in the first row.
        var firstRowCells = cut.FindAll("tr.sf-rent-ledger__row")[0].QuerySelectorAll("td");
        var agingText = firstRowCells[5].TextContent.Trim();
        Assert.NotEqual("-", agingText);
        Assert.True(int.Parse(agingText) > 0);
    }
}
