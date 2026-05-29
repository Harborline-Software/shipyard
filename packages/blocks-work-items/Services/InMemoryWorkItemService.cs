using Sunfish.Blocks.WorkItems.Events;
using Sunfish.Blocks.WorkItems.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkItems.Services;

/// <summary>
/// Default <see cref="IWorkItemService"/> backed by
/// <see cref="InMemoryWorkItemRepository"/>. Production hosts wire
/// a SQLite-backed repository in a follow-on persistence hand-off;
/// the service shape stays the same.
/// </summary>
public sealed class InMemoryWorkItemService : IWorkItemService
{
    private readonly InMemoryWorkItemRepository _repo;
    private readonly IWorkItemEventPublisher _events;

    public InMemoryWorkItemService(
        InMemoryWorkItemRepository repo,
        IWorkItemEventPublisher events)
    {
        _repo   = repo   ?? throw new ArgumentNullException(nameof(repo));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    /// <inheritdoc />
    public async Task<WorkItem> CreateAsync(
        TenantId tenantId, string title, WorkItemKind kind, Priority priority, Guid createdBy,
        WorkItemSeverity? severity = null,
        DateTimeOffset? dueBy = null,
        Guid? propertyId = null, Guid? unitId = null, Guid? assetId = null, Guid? deficiencyId = null,
        CancellationToken cancellationToken = default)
    {
        var wo = WorkItem.Create(tenantId, title, kind, priority, createdBy, severity, dueBy);
        // Attach cross-cluster anchors so downstream queries (e.g., the
        // DeficiencyRaised handler's idempotency lookup) can find the
        // WO by deficiencyId / propertyId / etc.
        wo.AttachAnchors(
            propertyId:   propertyId,
            unitId:       unitId,
            assetId:      assetId,
            deficiencyId: deficiencyId);
        _repo.Upsert(wo);
        await _events.PublishWorkItemCreatedAsync(
            new WorkItemCreatedEvent(
                WorkItemId:  wo.Id,
                TenantId:     wo.TenantId,
                Number:       wo.Number,
                Kind:         wo.Kind,
                Priority:     wo.Priority,
                PropertyId:   propertyId,
                UnitId:       unitId,
                AssetId:      assetId,
                DeficiencyId: deficiencyId,
                CreatedBy:    createdBy),
            cancellationToken).ConfigureAwait(false);
        return wo;
    }

    /// <inheritdoc />
    public Task<WorkItem?> GetByIdAsync(TenantId tenantId, WorkItemId workOrderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_repo.GetById(tenantId, workOrderId));

    /// <inheritdoc />
    public async Task<WorkItem> AssignAsync(
        TenantId tenantId, WorkItemId workOrderId,
        Guid? assignedToPartyId, Guid? contractorId, Guid assignedBy,
        CancellationToken cancellationToken = default)
    {
        var wo = RequireWorkItem(tenantId, workOrderId);
        wo.Assign(assignedToPartyId, contractorId, assignedBy);
        _repo.Upsert(wo);
        await _events.PublishWorkItemAssignedAsync(
            new WorkItemAssignedEvent(
                WorkItemId:       wo.Id,
                TenantId:          wo.TenantId,
                AssignedToPartyId: assignedToPartyId,
                ContractorId:      contractorId,
                AssignedBy:        assignedBy),
            cancellationToken).ConfigureAwait(false);
        return wo;
    }

    /// <inheritdoc />
    public async Task<WorkItem> TransitionAsync(
        TenantId tenantId, WorkItemId workOrderId,
        WorkItemStatus newStatus, Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var wo = RequireWorkItem(tenantId, workOrderId);
        if (wo.DeletedAt is not null)
            throw new InvalidOperationException(
                $"WorkItem {wo.Id.Value} is soft-deleted; transitions are rejected.");
        wo.Transition(newStatus, updatedBy);
        _repo.Upsert(wo);
        if (newStatus == WorkItemStatus.Completed)
        {
            await _events.PublishWorkItemCompletedAsync(
                new WorkItemCompletedEvent(
                    WorkItemId: wo.Id,
                    TenantId:    wo.TenantId,
                    CompletedAt: wo.CompletedAt ?? DateTimeOffset.UtcNow,
                    CompletedBy: updatedBy),
                cancellationToken).ConfigureAwait(false);
        }
        return wo;
    }

    /// <inheritdoc />
    public Task SoftDeleteAsync(
        TenantId tenantId, WorkItemId workOrderId, Guid deletedBy,
        CancellationToken cancellationToken = default)
    {
        var wo = RequireWorkItem(tenantId, workOrderId);
        wo.SoftDelete(deletedBy);
        _repo.Upsert(wo);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RepairTicket> CreateRepairTicketAsync(
        TenantId tenantId, string title, Guid createdBy,
        string? description = null,
        Guid? requestedByPartyId = null, Guid? propertyId = null, Guid? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var ticket = RepairTicket.Create(
            title:              title,
            createdBy:          createdBy,
            description:        description,
            requestedByPartyId: requestedByPartyId,
            propertyId:         propertyId,
            unitId:             unitId);
        _repo.UpsertTicket(ticket);
        return Task.FromResult(ticket);
    }

    private WorkItem RequireWorkItem(TenantId tenantId, WorkItemId workOrderId)
    {
        var wo = _repo.GetById(tenantId, workOrderId);
        if (wo is null)
            throw new InvalidOperationException(
                $"WorkItem {workOrderId.Value} not found in tenant {tenantId.Value} "
                + "(missing, soft-deleted, or cross-tenant).");
        return wo;
    }
}
