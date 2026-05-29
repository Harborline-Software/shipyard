namespace Sunfish.Blocks.WorkItems.Models;

/// <summary>
/// Allowed-transitions guard for <see cref="WorkItemStatus"/> per
/// <c>blocks-work-schema-design.md</c> §2.6. The app-layer enforces
/// this on every <see cref="WorkItem.Transition"/> call; CRDT merges
/// fall back to last-write-wins and a future reconciler catches
/// illegal merged states (Stage 02 Q7).
/// </summary>
public static class WorkItemStatusMachine
{
    private static readonly Dictionary<WorkItemStatus, HashSet<WorkItemStatus>> Allowed = new()
    {
        [WorkItemStatus.New]        = new() { WorkItemStatus.Triaged, WorkItemStatus.Cancelled },
        [WorkItemStatus.Triaged]    = new() { WorkItemStatus.Estimated, WorkItemStatus.Scheduled, WorkItemStatus.Cancelled },
        [WorkItemStatus.Estimated]  = new() { WorkItemStatus.Approved, WorkItemStatus.Cancelled },
        [WorkItemStatus.Approved]   = new() { WorkItemStatus.Scheduled },
        [WorkItemStatus.Scheduled]  = new() { WorkItemStatus.InProgress, WorkItemStatus.OnHold, WorkItemStatus.Cancelled },
        [WorkItemStatus.InProgress] = new() { WorkItemStatus.OnHold, WorkItemStatus.Blocked, WorkItemStatus.Completed },
        [WorkItemStatus.OnHold]     = new() { WorkItemStatus.InProgress, WorkItemStatus.Cancelled },
        [WorkItemStatus.Blocked]    = new() { WorkItemStatus.InProgress, WorkItemStatus.Cancelled },
        [WorkItemStatus.Completed]  = new() { WorkItemStatus.Verified, WorkItemStatus.InProgress },
        [WorkItemStatus.Verified]   = new() { WorkItemStatus.Invoiced, WorkItemStatus.Closed },
        [WorkItemStatus.Invoiced]   = new() { WorkItemStatus.Closed },
        [WorkItemStatus.Closed]     = new(),   // terminal
        [WorkItemStatus.Cancelled]  = new(),   // terminal
    };

    /// <summary>
    /// True when transitioning <paramref name="from"/> → <paramref name="to"/>
    /// is allowed by the state diagram. Self-transitions
    /// (<paramref name="from"/> == <paramref name="to"/>) are always
    /// disallowed — callers should no-op instead.
    /// </summary>
    public static bool CanTransition(WorkItemStatus from, WorkItemStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Snapshot of the allowed-targets set from a given status —
    /// useful for UI hint rendering and tests.
    /// </summary>
    public static IReadOnlySet<WorkItemStatus> AllowedTransitionsFrom(WorkItemStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : new HashSet<WorkItemStatus>();
}

/// <summary>
/// Thrown by <see cref="WorkItem.Transition"/> when the requested
/// status change violates the <see cref="WorkItemStatusMachine"/>.
/// </summary>
public sealed class InvalidStatusTransitionException : InvalidOperationException
{
    public WorkItemStatus From { get; }
    public WorkItemStatus To { get; }

    public InvalidStatusTransitionException(WorkItemStatus from, WorkItemStatus to)
        : base($"Invalid WorkItem status transition: {from} → {to}.")
    {
        From = from;
        To   = to;
    }
}
