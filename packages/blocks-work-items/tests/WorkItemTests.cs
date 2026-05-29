using System.Text.RegularExpressions;
using Sunfish.Blocks.WorkItems.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkItems.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="WorkItem"/> per
/// <c>blocks-work-schema-design.md</c> §2.4.
/// </summary>
public sealed class WorkItemTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_GeneratesNumberInFormat()
    {
        var now = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
        var wo = WorkItem.Create(
            Tenant, "Replace bathroom faucet", WorkItemKind.Repair,
            Priority.Normal, Actor, createdAt: now);

        Assert.Matches(@"^WO-20260516-[0-9a-f]{7}$", wo.Number);
    }

    [Fact]
    public void Create_KindRepair_StatusIsNew()
    {
        var wo = WorkItem.Create(
            Tenant, "Leaky faucet", WorkItemKind.Repair, Priority.Normal, Actor);
        Assert.Equal(WorkItemStatus.New, wo.Status);
    }

    [Fact]
    public void Transition_ValidTransition_UpdatesStatus()
    {
        var wo = WorkItem.Create(
            Tenant, "Triage me", WorkItemKind.Task, Priority.Normal, Actor);
        wo.Transition(WorkItemStatus.Triaged, Actor);
        Assert.Equal(WorkItemStatus.Triaged, wo.Status);
    }

    [Fact]
    public void Transition_InvalidTransition_Throws()
    {
        var wo = WorkItem.Create(
            Tenant, "Direct-jump-to-completed", WorkItemKind.Task, Priority.Normal, Actor);

        var ex = Assert.Throws<InvalidStatusTransitionException>(
            () => wo.Transition(WorkItemStatus.Completed, Actor));
        Assert.Equal(WorkItemStatus.New, ex.From);
        Assert.Equal(WorkItemStatus.Completed, ex.To);
    }

    [Fact]
    public void Transition_ToTerminalState_CannotExit()
    {
        var wo = WorkItem.Create(
            Tenant, "Drive to Closed", WorkItemKind.Task, Priority.Normal, Actor);
        // Walk the legal path to Closed.
        wo.Transition(WorkItemStatus.Triaged, Actor);
        wo.Transition(WorkItemStatus.Scheduled, Actor);
        wo.Transition(WorkItemStatus.InProgress, Actor);
        wo.Transition(WorkItemStatus.Completed, Actor);
        wo.Transition(WorkItemStatus.Verified, Actor);
        wo.Transition(WorkItemStatus.Closed, Actor);

        Assert.Throws<InvalidStatusTransitionException>(
            () => wo.Transition(WorkItemStatus.New, Actor));
        Assert.Throws<InvalidStatusTransitionException>(
            () => wo.Transition(WorkItemStatus.InProgress, Actor));
    }

    [Fact]
    public void SeveritySafety_RequiresDueBy()
    {
        Assert.Throws<ArgumentException>(() => WorkItem.Create(
            Tenant, "Leaking gas line",
            WorkItemKind.Repair, Priority.Critical, Actor,
            severity: WorkItemSeverity.Safety,
            dueBy: null));
    }

    [Fact]
    public void SeverityHabitability_RequiresDueBy()
    {
        Assert.Throws<ArgumentException>(() => WorkItem.Create(
            Tenant, "Heating system out in winter",
            WorkItemKind.Repair, Priority.High, Actor,
            severity: WorkItemSeverity.Habitability,
            dueBy: null));
    }

    [Fact]
    public void SeveritySafety_WithDueBy_Succeeds()
    {
        var wo = WorkItem.Create(
            Tenant, "Loose stair railing",
            WorkItemKind.Repair, Priority.High, Actor,
            severity: WorkItemSeverity.Safety,
            dueBy: DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(WorkItemSeverity.Safety, wo.Severity);
    }

    [Fact]
    public void Transition_InProgress_StampsStartedAt()
    {
        var wo = WorkItem.Create(
            Tenant, "Stamp test", WorkItemKind.Task, Priority.Normal, Actor);
        wo.Transition(WorkItemStatus.Triaged, Actor);
        wo.Transition(WorkItemStatus.Scheduled, Actor);
        Assert.Null(wo.StartedAt);

        wo.Transition(WorkItemStatus.InProgress, Actor);

        Assert.NotNull(wo.StartedAt);
    }

    [Fact]
    public void Transition_BumpsVersion()
    {
        var wo = WorkItem.Create(
            Tenant, "Version bump", WorkItemKind.Task, Priority.Normal, Actor);
        var before = wo.Version;
        wo.Transition(WorkItemStatus.Triaged, Actor);
        Assert.Equal(before + 1, wo.Version);
    }
}
