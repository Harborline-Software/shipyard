using Sunfish.Blocks.WorkItems.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkItems.Services;

/// <summary>
/// Write surface for <see cref="WorkItem"/> lifecycle. Wraps the
/// entity's mutation methods + emits <c>Work.*</c> events on
/// noteworthy transitions. Tenant-scoped — every read enforces
/// <see cref="WorkItem.TenantId"/> equality against the caller-
/// supplied tenant.
/// </summary>
public interface IWorkItemService
{
    /// <summary>Create a new <see cref="WorkItem"/>; emits <c>Work.WorkItemCreated</c>.</summary>
    Task<WorkItem> CreateAsync(
        TenantId tenantId,
        string title,
        WorkItemKind kind,
        Priority priority,
        Guid createdBy,
        WorkItemSeverity? severity = null,
        DateTimeOffset? dueBy = null,
        Guid? propertyId = null,
        Guid? unitId = null,
        Guid? assetId = null,
        Guid? deficiencyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Fetch a <see cref="WorkItem"/> by id within the supplied tenant. Returns null when missing or tenant-mismatched.</summary>
    Task<WorkItem?> GetByIdAsync(
        TenantId tenantId,
        WorkItemId workOrderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign the work order to a party + / or contractor; emits
    /// <c>Work.WorkItemAssigned</c>.
    /// </summary>
    Task<WorkItem> AssignAsync(
        TenantId tenantId,
        WorkItemId workOrderId,
        Guid? assignedToPartyId,
        Guid? contractorId,
        Guid assignedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition the work order to the supplied status. Throws
    /// <see cref="InvalidStatusTransitionException"/> when the
    /// transition violates <see cref="WorkItemStatusMachine"/>.
    /// Emits <c>Work.WorkItemCompleted</c> on transitions to
    /// <see cref="WorkItemStatus.Completed"/>.
    /// </summary>
    Task<WorkItem> TransitionAsync(
        TenantId tenantId,
        WorkItemId workOrderId,
        WorkItemStatus newStatus,
        Guid updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-delete; subsequent transitions are rejected.</summary>
    Task SoftDeleteAsync(
        TenantId tenantId,
        WorkItemId workOrderId,
        Guid deletedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Create a <see cref="RepairTicket"/> sidecar; tenants + frontline staff invoke this before triage.</summary>
    Task<RepairTicket> CreateRepairTicketAsync(
        TenantId tenantId,
        string title,
        Guid createdBy,
        string? description = null,
        Guid? requestedByPartyId = null,
        Guid? propertyId = null,
        Guid? unitId = null,
        CancellationToken cancellationToken = default);
}
