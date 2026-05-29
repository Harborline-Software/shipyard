using Sunfish.Blocks.WorkItems.Models;
using Xunit;

namespace Sunfish.Blocks.WorkItems.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="WorkItemStatusMachine"/> per
/// <c>blocks-work-schema-design.md</c> §2.6.
/// </summary>
public sealed class WorkItemStatusMachineTests
{
    [Fact]
    public void AllTerminalStates_ReturnEmptyTransitions()
    {
        Assert.Empty(WorkItemStatusMachine.AllowedTransitionsFrom(WorkItemStatus.Closed));
        Assert.Empty(WorkItemStatusMachine.AllowedTransitionsFrom(WorkItemStatus.Cancelled));
    }

    [Fact]
    public void StateMachine_AllowsTriagedSkippingEstimate_DirectToScheduled()
    {
        Assert.True(WorkItemStatusMachine.CanTransition(
            WorkItemStatus.Triaged, WorkItemStatus.Scheduled));
    }

    [Fact]
    public void StateMachine_RejectsBackwardsFromCompletedToTriaged()
    {
        Assert.False(WorkItemStatusMachine.CanTransition(
            WorkItemStatus.Completed, WorkItemStatus.Triaged));
    }

    [Fact]
    public void StateMachine_AllowsCompletedReopenToInProgress()
    {
        Assert.True(WorkItemStatusMachine.CanTransition(
            WorkItemStatus.Completed, WorkItemStatus.InProgress));
    }

    [Fact]
    public void StateMachine_RejectsSelfTransitions()
    {
        // Self-transitions are NOT in the allowed map — callers should
        // no-op instead of round-tripping the state machine.
        Assert.False(WorkItemStatusMachine.CanTransition(
            WorkItemStatus.InProgress, WorkItemStatus.InProgress));
    }

    [Fact]
    public void StateMachine_NewToCancelled_Allowed()
    {
        Assert.True(WorkItemStatusMachine.CanTransition(
            WorkItemStatus.New, WorkItemStatus.Cancelled));
    }
}
