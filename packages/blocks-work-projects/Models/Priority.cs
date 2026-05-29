namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Generic priority classifier — mirrors
/// <c>Sunfish.Blocks.WorkItems.Models.Priority</c>. Local definition
/// until a canonical foundation-priorities home is ratified; same
/// pattern as the sibling work-items cluster.
/// </summary>
public enum Priority
{
    /// <summary>Lowest urgency.</summary>
    Low,

    /// <summary>Default urgency.</summary>
    Normal,

    /// <summary>Elevated.</summary>
    High,

    /// <summary>Critical — drop other work.</summary>
    Urgent,
}
