using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reviews.Models;

/// <summary>
/// A single execution of an <see cref="ReviewTemplate"/> against a specific unit,
/// carried out by a named inspector on a scheduled date.
/// </summary>
/// <param name="Id">Unique identifier for this inspection.</param>
/// <param name="TemplateId">The template that defines the checklist for this inspection, or <see langword="null"/> for ad-hoc inspections.</param>
/// <param name="UnitId">The unit being inspected.</param>
/// <param name="InspectorName">Display name of the person conducting the inspection.</param>
/// <param name="ScheduledDate">The calendar date on which the inspection is scheduled.</param>
/// <param name="Phase">Current lifecycle phase of the inspection.</param>
/// <param name="StartedAtUtc">The instant the inspection transitioned to <see cref="ReviewPhase.InProgress"/>, or <see langword="null"/> if not yet started.</param>
/// <param name="CompletedAtUtc">The instant the inspection transitioned to <see cref="ReviewPhase.Completed"/>, or <see langword="null"/> if not yet completed.</param>
/// <param name="Responses">Responses collected so far, in the order they were recorded.</param>
/// <param name="Trigger">Why this inspection was scheduled (annual / move-in / move-out / post-repair / on-demand). Optional for backward compat with pre-revision records — defaults to <see langword="null"/>.</param>
public sealed record Review(
    ReviewId Id,
    ReviewTemplateId? TemplateId,
    EntityId UnitId,
    string InspectorName,
    DateOnly ScheduledDate,
    ReviewPhase Phase,
    Instant? StartedAtUtc,
    Instant? CompletedAtUtc,
    IReadOnlyList<ReviewResponse> Responses,
    ReviewTrigger? Trigger = null);
