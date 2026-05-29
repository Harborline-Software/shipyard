using Sunfish.Blocks.Reviews.Models;

namespace Sunfish.Blocks.Reviews.Services;

/// <summary>
/// Payload for creating a new <see cref="ReviewTemplate"/> via
/// <see cref="IReviewsService.CreateTemplateAsync"/>.
/// </summary>
public sealed record CreateTemplateRequest
{
    /// <summary>Human-readable template name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description explaining the purpose or scope of the template.</summary>
    public string? Description { get; init; }

    /// <summary>Ordered list of checklist items for this template.</summary>
    public required IReadOnlyList<ReviewChecklistItem> Items { get; init; }
}
