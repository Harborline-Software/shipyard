namespace Sunfish.Blocks.WorkItems.Models;

/// <summary>
/// Severity classification for <see cref="WorkItem"/>; drives
/// due-by SLAs + the safety-due-by invariant
/// (<see cref="WorkItemSeverity.Safety"/> + <see cref="WorkItemSeverity.Habitability"/>
/// require a non-null <c>DueBy</c> at creation time).
/// </summary>
public enum WorkItemSeverity
{
    /// <summary>Cosmetic-only finding; no functional impact.</summary>
    Cosmetic,

    /// <summary>Minor functional issue; no safety / habitability impact.</summary>
    Minor,

    /// <summary>Major functional issue; tenant-impacting.</summary>
    Major,

    /// <summary>Safety hazard; DueBy required.</summary>
    Safety,

    /// <summary>Habitability / code-compliance issue; DueBy required.</summary>
    Habitability,
}
