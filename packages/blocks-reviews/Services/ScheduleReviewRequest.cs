using Sunfish.Blocks.Reviews.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// Payload for scheduling a new <see cref="Review"/> via
/// <see cref="IReviewsService.ScheduleAsync"/>.
/// </summary>
public sealed record ScheduleReviewRequest
{
    /// <summary>
    /// The template that defines the checklist for this inspection.
    /// <see langword="null"/> for ad-hoc inspections that are not derived from a template.
    /// </summary>
    public ReviewTemplateId? TemplateId { get; init; }

    /// <summary>The unit to be inspected.</summary>
    public required EntityId UnitId { get; init; }

    /// <summary>Display name of the person who will conduct the inspection.</summary>
    public required string InspectorName { get; init; }

    /// <summary>The calendar date on which the inspection is scheduled.</summary>
    public required DateOnly ScheduledDate { get; init; }

    /// <summary>
    /// Optional trigger categorizing why this inspection is being scheduled.
    /// Set for move-in / move-out / post-repair contexts so the delta
    /// projection can pair them. Defaults to <see langword="null"/> for
    /// callers that don't care or are pre-revision.
    /// </summary>
    public ReviewTrigger? Trigger { get; init; }
}
