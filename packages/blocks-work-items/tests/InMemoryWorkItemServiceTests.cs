using Sunfish.Blocks.WorkItems.Events;
using Sunfish.Blocks.WorkItems.Models;
using Sunfish.Blocks.WorkItems.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkItems.Tests;

/// <summary>
/// W#60 P4 PR 4 — coverage for <see cref="InMemoryWorkItemService"/>
/// + tenant-isolation gate per hand-off H5.
/// </summary>
public sealed class InMemoryWorkItemServiceTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly TenantId OtherTenant = new("other-tenant-2");
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public async Task CreateWorkItem_ValidInput_ReturnsNew()
    {
        var (svc, _, events) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Test repair", WorkItemKind.Repair, Priority.Normal, Actor);

        Assert.NotNull(wo);
        Assert.Equal(WorkItemStatus.New, wo.Status);
        Assert.Single(events.Events.OfType<WorkItemCreatedEvent>());
    }

    [Fact]
    public async Task TransitionWorkItem_ValidTransition_Success()
    {
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkItemKind.Task, Priority.Normal, Actor);

        var triaged = await svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.Triaged, Actor);

        Assert.Equal(WorkItemStatus.Triaged, triaged.Status);
    }

    [Fact]
    public async Task TransitionWorkItem_InvalidTransition_Throws()
    {
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkItemKind.Task, Priority.Normal, Actor);

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(
            () => svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.Completed, Actor));
    }

    [Fact]
    public async Task TransitionToCompleted_EmitsWorkItemCompletedEvent()
    {
        var (svc, _, events) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkItemKind.Task, Priority.Normal, Actor);
        await svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.Triaged, Actor);
        await svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.Scheduled, Actor);
        await svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.InProgress, Actor);

        await svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.Completed, Actor);

        Assert.Single(events.Events.OfType<WorkItemCompletedEvent>());
    }

    [Fact]
    public async Task AssignWorkItem_SetsAssigneeAndContractor()
    {
        var (svc, _, events) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkItemKind.Task, Priority.Normal, Actor);
        var assignee = Guid.NewGuid();
        var contractorId = Guid.NewGuid();

        var assigned = await svc.AssignAsync(Tenant, wo.Id, assignee, contractorId, Actor);

        Assert.Equal(assignee, assigned.AssignedToPartyId);
        Assert.Equal(contractorId, assigned.ContractorId);
        Assert.Single(events.Events.OfType<WorkItemAssignedEvent>());
    }

    [Fact]
    public async Task SoftDelete_SetsDeletedAt_CannotTransition()
    {
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkItemKind.Task, Priority.Normal, Actor);

        await svc.SoftDeleteAsync(Tenant, wo.Id, Actor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransitionAsync(Tenant, wo.Id, WorkItemStatus.Triaged, Actor));
    }

    [Fact]
    public async Task GetById_WrongTenant_ReturnsNull()
    {
        // H5 invariant: cross-tenant reads must fail closed.
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Tenant A repair", WorkItemKind.Repair, Priority.Normal, Actor);

        var crossTenantRead = await svc.GetByIdAsync(OtherTenant, wo.Id);

        Assert.Null(crossTenantRead);
    }

    [Fact]
    public async Task CreateRepairTicket_ReturnsPersistedTicket()
    {
        var (svc, _, _) = NewHarness();
        var ticket = await svc.CreateRepairTicketAsync(
            Tenant, "Sink clogged", Actor, description: "Kitchen sink");

        Assert.NotNull(ticket);
        Assert.Equal("Sink clogged", ticket.Title);
    }

    private static (InMemoryWorkItemService Svc, InMemoryWorkItemRepository Repo, InMemoryWorkItemEventPublisher Events) NewHarness()
    {
        var repo = new InMemoryWorkItemRepository();
        var events = new InMemoryWorkItemEventPublisher();
        var svc = new InMemoryWorkItemService(repo, events);
        return (svc, repo, events);
    }
}
