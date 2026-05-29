using Sunfish.Blocks.Reviews.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// Optional filter parameters for <see cref="IReviewsService.ListReviewsAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListReviewsQuery
{
    /// <summary>When set, only inspections for this unit are returned.</summary>
    public EntityId? UnitId { get; init; }

    /// <summary>When set, only inspections in this phase are returned.</summary>
    public ReviewPhase? Phase { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListReviewsQuery Empty { get; } = new();
}
