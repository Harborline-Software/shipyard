using Sunfish.Blocks.Reviews.Models;
using Sunfish.Blocks.PropertyEquipment.Models;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// Payload for recording a new <see cref="EquipmentConditionAssessment"/> via
/// <see cref="IReviewsService.RecordEquipmentConditionAsync"/>.
/// </summary>
public sealed record RecordEquipmentConditionRequest
{
    /// <summary>The inspection during which this assessment is being recorded.</summary>
    public required ReviewId ReviewId { get; init; }

    /// <summary>FK to the <c>Sunfish.Blocks.PropertyEquipment.Equipment</c> being rated.</summary>
    public required EquipmentId EquipmentId { get; init; }

    /// <summary>Condition rating observed.</summary>
    public required ConditionRating Condition { get; init; }

    /// <summary>Optional projection of remaining useful life in years.</summary>
    public int? ExpectedRemainingLifeYears { get; init; }

    /// <summary>Free-text observations captured by the inspector.</summary>
    public string? Observations { get; init; }

    /// <summary>Free-text recommendations (service / replace / monitor).</summary>
    public string? Recommendations { get; init; }
}
