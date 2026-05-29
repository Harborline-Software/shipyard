using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Reviews.Models;
using Sunfish.Blocks.Reviews.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Reviews.Tests;

public class ReviewListBlockTests : BunitContext
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly EntityId TestUnitId = new("unit", "test", "apt-1");

    private static async Task<(InMemoryReviewsService svc, ReviewTemplate template)> MakePopulatedService()
    {
        var svc = new InMemoryReviewsService();
        var template = await svc.CreateTemplateAsync(new CreateTemplateRequest
        {
            Name = "Move-In",
            Items =
            [
                new ReviewChecklistItem(ReviewChecklistItemId.NewId(), "Smoke detector?", ReviewItemKind.YesNo, true),
                new ReviewChecklistItem(ReviewChecklistItemId.NewId(), "Overall rating", ReviewItemKind.Rating1to5, false),
            ]
        });
        return (svc, template);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyService_Renders_NoReviewsPlaceholder()
    {
        Services.AddSingleton<IReviewsService, InMemoryReviewsService>();

        var cut = Render<ReviewListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-inspection-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("No inspections scheduled", cut.Markup);
    }

    [Fact]
    public async Task PopulatedService_Renders_ReviewRows()
    {
        var (svc, template) = await MakePopulatedService();

        await svc.ScheduleAsync(new ScheduleReviewRequest
        {
            TemplateId = template.Id,
            UnitId = TestUnitId,
            InspectorName = "Alice Johnson",
            ScheduledDate = new DateOnly(2026, 6, 15),
        });

        Services.AddSingleton<IReviewsService>(svc);

        var cut = Render<ReviewListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-inspection-list__loading"), TimeSpan.FromSeconds(5));

        Assert.Contains("Alice Johnson", cut.Markup);
        Assert.Contains("Scheduled", cut.Markup);
        Assert.Contains("2026-06-15", cut.Markup);
    }

    [Fact]
    public async Task DeficiencyCount_IsRenderedCorrectly_ForEachReview()
    {
        var (svc, template) = await MakePopulatedService();

        // Create and start an inspection, then record two deficiencies.
        var inspection = await svc.ScheduleAsync(new ScheduleReviewRequest
        {
            TemplateId = template.Id,
            UnitId = TestUnitId,
            InspectorName = "Bob Williams",
            ScheduledDate = new DateOnly(2026, 7, 1),
        });
        await svc.StartAsync(inspection.Id);

        await svc.RecordDeficiencyAsync(new RecordDeficiencyRequest
        {
            ReviewId = inspection.Id,
            ItemId = template.Items[0].Id,
            Severity = DeficiencySeverity.High,
            Description = "Smoke detector missing battery",
        });
        await svc.RecordDeficiencyAsync(new RecordDeficiencyRequest
        {
            ReviewId = inspection.Id,
            ItemId = template.Items[1].Id,
            Severity = DeficiencySeverity.Low,
            Description = "Light bulb out",
        });

        Services.AddSingleton<IReviewsService>(svc);

        var cut = Render<ReviewListBlock>();
        cut.WaitForState(() => !cut.Markup.Contains("sf-inspection-list__loading"), TimeSpan.FromSeconds(5));

        // The deficiency count column should show "2".
        var rows = cut.FindAll("tr.sf-inspection-list__row");
        Assert.Single(rows);

        var deficiencyCell = cut.Find("td.sf-inspection-list__col-deficiencies");
        Assert.Equal("2", deficiencyCell.TextContent.Trim());
    }
}
