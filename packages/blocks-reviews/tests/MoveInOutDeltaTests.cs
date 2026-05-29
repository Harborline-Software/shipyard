using Sunfish.Blocks.Reviews.Models;
using Sunfish.Blocks.Reviews.Services;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Reviews.Tests;

public class MoveInOutDeltaTests
{
    private static readonly EntityId TestUnitId = new("unit", "test", "delta-unit");

    private static async Task<(InMemoryReviewsService svc, ReviewTemplate template, ReviewChecklistItem item)> SetupAsync()
    {
        var svc = new InMemoryReviewsService();
        var item = new ReviewChecklistItem(ReviewChecklistItemId.NewId(), "Walls clean?", ReviewItemKind.PassFail, true);
        var template = await svc.CreateTemplateAsync(new CreateTemplateRequest
        {
            Name = "Move-In/Out",
            Description = "",
            Items = [item],
        });
        return (svc, template, item);
    }

    private static async Task<Review> ScheduleStartCompleteAsync(
        InMemoryReviewsService svc,
        ReviewTemplateId templateId,
        ReviewTrigger trigger,
        DateOnly scheduledDate,
        ReviewResponse[] responses)
    {
        var scheduled = await svc.ScheduleAsync(new ScheduleReviewRequest
        {
            TemplateId = templateId,
            UnitId = TestUnitId,
            InspectorName = "Inspector",
            ScheduledDate = scheduledDate,
            Trigger = trigger,
        });
        var started = await svc.StartAsync(scheduled.Id);
        foreach (var r in responses)
            await svc.RecordResponseAsync(started.Id, r);
        return await svc.CompleteAsync(started.Id);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_pairs_recent_move_in_and_move_out_with_response_deltas()
    {
        var (svc, template, item) = await SetupAsync();

        var moveIn = await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveIn, new DateOnly(2025, 6, 1),
            new[] { new ReviewResponse(item.Id, "pass", null) });
        var moveOut = await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveOut, new DateOnly(2026, 4, 28),
            new[] { new ReviewResponse(item.Id, "fail", null) });

        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);

        Assert.NotNull(delta);
        Assert.Equal(TestUnitId, delta!.UnitId);
        Assert.Equal(moveIn.Id, delta.MoveIn.Id);
        Assert.Equal(moveOut.Id, delta.MoveOut.Id);
        Assert.Single(delta.ResponseDeltas);
        Assert.Equal(item.Id, delta.ResponseDeltas[0].ItemId);
        Assert.Equal("pass", delta.ResponseDeltas[0].MoveInValue);
        Assert.Equal("fail", delta.ResponseDeltas[0].MoveOutValue);
        Assert.True(delta.ResponseDeltas[0].Changed);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_returns_null_when_only_move_in_present()
    {
        var (svc, template, item) = await SetupAsync();
        await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveIn, new DateOnly(2025, 6, 1),
            new[] { new ReviewResponse(item.Id, "pass", null) });

        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);
        Assert.Null(delta);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_returns_null_when_only_move_out_present()
    {
        var (svc, template, item) = await SetupAsync();
        await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveOut, new DateOnly(2026, 4, 28),
            new[] { new ReviewResponse(item.Id, "fail", null) });

        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);
        Assert.Null(delta);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_returns_null_when_neither_present()
    {
        var svc = new InMemoryReviewsService();
        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);
        Assert.Null(delta);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_uses_most_recent_when_multiple_pairs_exist()
    {
        var (svc, template, item) = await SetupAsync();
        await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveIn, new DateOnly(2024, 6, 1),
            new[] { new ReviewResponse(item.Id, "pass", null) });
        var newerMoveIn = await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveIn, new DateOnly(2025, 6, 1),
            new[] { new ReviewResponse(item.Id, "pass", null) });
        await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveOut, new DateOnly(2025, 5, 1),
            new[] { new ReviewResponse(item.Id, "fail", null) });
        var newerMoveOut = await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveOut, new DateOnly(2026, 4, 28),
            new[] { new ReviewResponse(item.Id, "pass", null) });

        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);

        Assert.NotNull(delta);
        Assert.Equal(newerMoveIn.Id, delta!.MoveIn.Id);
        Assert.Equal(newerMoveOut.Id, delta.MoveOut.Id);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_emits_equipment_condition_deltas_with_degradation_flag()
    {
        var (svc, template, item) = await SetupAsync();
        var equipmentA = EquipmentId.NewId();
        var equipmentB = EquipmentId.NewId();

        // Move-in inspection — record a response then condition assessments while in-progress
        var inScheduled = await svc.ScheduleAsync(new ScheduleReviewRequest { TemplateId = template.Id, UnitId = TestUnitId, InspectorName = "X", ScheduledDate = new DateOnly(2025, 6, 1), Trigger = ReviewTrigger.MoveIn });
        var inStarted = await svc.StartAsync(inScheduled.Id);
        await svc.RecordResponseAsync(inStarted.Id, new ReviewResponse(item.Id, "pass", null));
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { ReviewId = inStarted.Id, EquipmentId = equipmentA, Condition = ConditionRating.Good });
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { ReviewId = inStarted.Id, EquipmentId = equipmentB, Condition = ConditionRating.Good });
        await svc.CompleteAsync(inStarted.Id);

        // Move-out — equipmentA degraded, equipmentB unchanged
        var outScheduled = await svc.ScheduleAsync(new ScheduleReviewRequest { TemplateId = template.Id, UnitId = TestUnitId, InspectorName = "X", ScheduledDate = new DateOnly(2026, 4, 28), Trigger = ReviewTrigger.MoveOut });
        var outStarted = await svc.StartAsync(outScheduled.Id);
        await svc.RecordResponseAsync(outStarted.Id, new ReviewResponse(item.Id, "pass", null));
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { ReviewId = outStarted.Id, EquipmentId = equipmentA, Condition = ConditionRating.Poor });
        await svc.RecordEquipmentConditionAsync(new RecordEquipmentConditionRequest { ReviewId = outStarted.Id, EquipmentId = equipmentB, Condition = ConditionRating.Good });
        await svc.CompleteAsync(outStarted.Id);

        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);

        Assert.NotNull(delta);
        Assert.Equal(2, delta!.EquipmentConditionDeltas.Count);
        var aDelta = delta.EquipmentConditionDeltas.Single(d => d.EquipmentId.Equals(equipmentA));
        Assert.Equal(ConditionRating.Good, aDelta.MoveInCondition);
        Assert.Equal(ConditionRating.Poor, aDelta.MoveOutCondition);
        Assert.True(aDelta.Degraded);

        var bDelta = delta.EquipmentConditionDeltas.Single(d => d.EquipmentId.Equals(equipmentB));
        Assert.False(bDelta.Degraded);
    }

    [Fact]
    public async Task GetMoveInOutDeltaAsync_ignores_inspections_for_other_units()
    {
        var (svc, template, item) = await SetupAsync();
        var otherUnitId = new EntityId("unit", "test", "other-unit");

        // Move-in for our unit
        await ScheduleStartCompleteAsync(svc, template.Id, ReviewTrigger.MoveIn, new DateOnly(2025, 6, 1),
            new[] { new ReviewResponse(item.Id, "pass", null) });

        // Move-out for a DIFFERENT unit; should NOT pair with our move-in
        var otherScheduled = await svc.ScheduleAsync(new ScheduleReviewRequest
        {
            TemplateId = template.Id,
            UnitId = otherUnitId,
            InspectorName = "X",
            ScheduledDate = new DateOnly(2026, 4, 28),
            Trigger = ReviewTrigger.MoveOut,
        });
        await svc.StartAsync(otherScheduled.Id);
        await svc.CompleteAsync(otherScheduled.Id);

        var delta = await svc.GetMoveInOutDeltaAsync(TestUnitId);
        Assert.Null(delta);
    }
}
