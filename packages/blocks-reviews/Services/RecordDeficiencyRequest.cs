using Sunfish.Blocks.Reviews.Models;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// Payload for recording a new <see cref="Deficiency"/> via
/// <see cref="IReviewsService.RecordDeficiencyAsync"/>.
/// </summary>
public sealed record RecordDeficiencyRequest
{
    /// <summary>The inspection during which this deficiency was observed.</summary>
    public required ReviewId ReviewId { get; init; }

    /// <summary>The checklist item that triggered this deficiency.</summary>
    public required ReviewChecklistItemId ItemId { get; init; }

    /// <summary>Severity of the deficiency.</summary>
    public required DeficiencySeverity Severity { get; init; }

    /// <summary>Human-readable description of the deficiency.</summary>
    public required string Description { get; init; }
}
