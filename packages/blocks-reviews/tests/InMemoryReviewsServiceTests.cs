using Sunfish.Blocks.Reviews.Models;
using Sunfish.Blocks.Reviews.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Reviews.Tests;

public class InMemoryReviewsServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly EntityId TestUnitId = new("unit", "test", "unit-1");
    private static readonly EntityId TestUnitId2 = new("unit", "test", "unit-2");

    private static ReviewChecklistItem Item(string prompt, ReviewItemKind kind = ReviewItemKind.YesNo, bool required = true)
        => new(ReviewChecklistItemId.NewId(), prompt, kind, required);

    private static CreateTemplateRequest MakeTemplateRequest(string name = "Standard Move-In") =>
        new()
        {
            Name = name,
            Description = "Standard move-in checklist",
            Items =
            [
                Item("Smoke detector operational?"),
                Item("Water heater functional?", ReviewItemKind.PassFail),
                Item("Overall cleanliness rating", ReviewItemKind.Rating1to5, required: false),
            ]
        };

    private static async Task<(InMemoryReviewsService svc, ReviewTemplate template)> MakeServiceWithTemplate(string templateName = "Standard Move-In")
    {
        var svc = new InMemoryReviewsService();
        var template = await svc.CreateTemplateAsync(MakeTemplateRequest(templateName));
        return (svc, template);
    }

    private static ScheduleReviewRequest MakeScheduleRequest(ReviewTemplateId templateId, EntityId? unitId = null) =>
        new()
        {
            TemplateId = templateId,
            UnitId = unitId ?? TestUnitId,
            InspectorName = "Jane Smith",
            ScheduledDate = new DateOnly(2026, 5, 1),
        };

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    // ── Template tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplateAsync_RoundTrip_WithThreeChecklistItems()
    {
        var svc = new InMemoryReviewsService();
        var request = MakeTemplateRequest();

        var template = await svc.CreateTemplateAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(template.Id.Value));
        Assert.Equal("Standard Move-In", template.Name);
        Assert.Equal(3, template.Items.Count);

        var retrieved = await svc.GetTemplateAsync(template.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(template.Id, retrieved.Id);
        Assert.Equal(3, retrieved.Items.Count);
    }

    // ── Review lifecycle tests ─────────────────────────────────────────

    [Fact]
    public async Task ScheduleAsync_CreatesReview_InScheduledPhase()
    {
        var (svc, template) = await MakeServiceWithTemplate();

        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));

        Assert.False(string.IsNullOrWhiteSpace(inspection.Id.Value));
        Assert.Equal(ReviewPhase.Scheduled, inspection.Phase);
        Assert.Null(inspection.StartedAtUtc);
        Assert.Null(inspection.CompletedAtUtc);
        Assert.Empty(inspection.Responses);
    }

    [Fact]
    public async Task ScheduleAsync_NullTemplateId_AdHocReview_Succeeds()
    {
        var svc = new InMemoryReviewsService();

        var inspection = await svc.ScheduleAsync(new ScheduleReviewRequest
        {
            TemplateId = null,
            UnitId = TestUnitId,
            InspectorName = "Ad-Hoc Inspector",
            ScheduledDate = new DateOnly(2026, 5, 21),
        });

        Assert.False(string.IsNullOrWhiteSpace(inspection.Id.Value));
        Assert.Null(inspection.TemplateId);
        Assert.Equal(ReviewPhase.Scheduled, inspection.Phase);
    }

    [Fact]
    public async Task GenerateReportAsync_NullTemplateId_AdHocReview_ReturnsZeroItems()
    {
        var svc = new InMemoryReviewsService();
        var inspection = await svc.ScheduleAsync(new ScheduleReviewRequest
        {
            TemplateId = null,
            UnitId = TestUnitId,
            InspectorName = "Ad-Hoc Inspector",
            ScheduledDate = new DateOnly(2026, 5, 21),
        });
        await svc.StartAsync(inspection.Id);
        await svc.CompleteAsync(inspection.Id);

        var report = await svc.GenerateReportAsync(inspection.Id);

        Assert.Equal(0, report.TotalItems);
        Assert.Equal(0, report.PassedItems);
        Assert.Equal(0, report.DeficiencyCount);
    }

    [Fact]
    public async Task StartAsync_Scheduled_TransitionsToInProgress_AndSetsStartedAtUtc()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));

        var started = await svc.StartAsync(inspection.Id);

        Assert.Equal(ReviewPhase.InProgress, started.Phase);
        Assert.NotNull(started.StartedAtUtc);
    }

    [Fact]
    public async Task StartAsync_WhenNotScheduled_ThrowsInvalidOperationException()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        // Review is now InProgress — starting again must throw.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.StartAsync(inspection.Id).AsTask());
    }

    [Fact]
    public async Task RecordResponseAsync_AppendsToResponses()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        var response = new ReviewResponse(template.Items[0].Id, "yes", null);
        var updated = await svc.RecordResponseAsync(inspection.Id, response);

        Assert.Single(updated.Responses);
        Assert.Equal("yes", updated.Responses[0].ResponseValue);
    }

    [Fact]
    public async Task CompleteAsync_InProgress_TransitionsToCompleted_AndSetsCompletedAtUtc()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        var completed = await svc.CompleteAsync(inspection.Id);

        Assert.Equal(ReviewPhase.Completed, completed.Phase);
        Assert.NotNull(completed.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteAsync_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));

        // Review is Scheduled, not InProgress.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompleteAsync(inspection.Id).AsTask());
    }

    [Fact]
    public async Task GetReviewAsync_UnknownId_ReturnsNull()
    {
        var svc = new InMemoryReviewsService();

        var result = await svc.GetReviewAsync(new ReviewId("no-such-id"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListReviewsAsync_FiltersByUnitId()
    {
        var (svc, template) = await MakeServiceWithTemplate();

        await svc.ScheduleAsync(MakeScheduleRequest(template.Id, TestUnitId));
        await svc.ScheduleAsync(MakeScheduleRequest(template.Id, TestUnitId));
        await svc.ScheduleAsync(MakeScheduleRequest(template.Id, TestUnitId2));

        var forUnit1 = await CollectAsync(svc.ListReviewsAsync(new ListReviewsQuery { UnitId = TestUnitId }));
        var forUnit2 = await CollectAsync(svc.ListReviewsAsync(new ListReviewsQuery { UnitId = TestUnitId2 }));

        Assert.Equal(2, forUnit1.Count);
        Assert.Single(forUnit2);
    }

    // ── Deficiency tests ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordDeficiencyAsync_LinksToReview()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        var deficiency = await svc.RecordDeficiencyAsync(new RecordDeficiencyRequest
        {
            ReviewId = inspection.Id,
            ItemId = template.Items[0].Id,
            Severity = DeficiencySeverity.High,
            Description = "Smoke detector battery missing",
        });

        Assert.Equal(inspection.Id, deficiency.ReviewId);
        Assert.Equal(DeficiencyStatus.Open, deficiency.Status);

        var list = await CollectAsync(svc.ListDeficienciesAsync(inspection.Id));
        Assert.Single(list);
        Assert.Equal(deficiency.Id, list[0].Id);
    }

    // ── Report tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateReportAsync_CountsItemsAndDeficienciesCorrectly()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        // Record 2 of 3 items (items[0] = YesNo/yes = pass, items[1] = PassFail/fail = fail).
        await svc.RecordResponseAsync(inspection.Id, new ReviewResponse(template.Items[0].Id, "yes", null));
        await svc.RecordResponseAsync(inspection.Id, new ReviewResponse(template.Items[1].Id, "fail", null));
        await svc.CompleteAsync(inspection.Id);

        // Record one deficiency.
        await svc.RecordDeficiencyAsync(new RecordDeficiencyRequest
        {
            ReviewId = inspection.Id,
            ItemId = template.Items[1].Id,
            Severity = DeficiencySeverity.Medium,
            Description = "Water heater not functional",
        });

        var report = await svc.GenerateReportAsync(inspection.Id);

        Assert.Equal(inspection.Id, report.ReviewId);
        Assert.Equal(3, report.TotalItems);   // 3 items in template
        Assert.Equal(1, report.PassedItems);  // only items[0] passed
        Assert.Equal(1, report.DeficiencyCount);
    }

    // ── Concurrency test ──────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentRecordResponseAsync_OnSameReview_AreSerializedNoLostResponses()
    {
        var (svc, template) = await MakeServiceWithTemplate();
        var inspection = await svc.ScheduleAsync(MakeScheduleRequest(template.Id));
        await svc.StartAsync(inspection.Id);

        // Fire 10 concurrent RecordResponseAsync calls on the same inspection.
        // Each records a distinct "yes" answer against the first checklist item.
        // After all complete, the responses list must have exactly 10 entries.
        const int concurrency = 10;
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => svc.RecordResponseAsync(
                inspection.Id,
                new ReviewResponse(template.Items[0].Id, "yes", null)).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        var final = await svc.GetReviewAsync(inspection.Id);
        Assert.NotNull(final);
        Assert.Equal(concurrency, final.Responses.Count);
    }
}
