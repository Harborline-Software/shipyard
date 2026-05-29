using Sunfish.Blocks.WorkItems.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkItems.Events;

/// <summary>
/// Cross-cluster event-publication seam for work-order lifecycle
/// events per <c>cross-cluster-event-bus-design.md</c> §3.2
/// (<c>Work.*</c> catalog). Same shape as sibling cluster
/// publishers (financial / periods / tax) — local interface here
/// until the canonical <c>foundation-events</c> substrate ships.
/// </summary>
public interface IWorkItemEventPublisher
{
    /// <summary>
    /// Emit <c>Work.WorkItemCreated</c>. Idempotency key per
    /// catalog: <c>wo-created:{workOrderId}</c>.
    /// </summary>
    Task PublishWorkItemCreatedAsync(
        WorkItemCreatedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emit <c>Work.WorkItemAssigned</c>. Idempotency key per
    /// catalog: <c>wo-assigned:{workOrderId}:{occurredAtTicks}</c>
    /// (re-fire safe — reassignments are allowed).
    /// </summary>
    Task PublishWorkItemAssignedAsync(
        WorkItemAssignedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Emit <c>Work.WorkItemCompleted</c>. Idempotency key per
    /// catalog: <c>wo-completed:{workOrderId}</c> (one-shot).
    /// </summary>
    Task PublishWorkItemCompletedAsync(
        WorkItemCompletedEvent payload,
        CancellationToken cancellationToken = default);
}

/// <summary>Payload for <c>Work.WorkItemCreated</c>.</summary>
public sealed record WorkItemCreatedEvent(
    WorkItemId WorkItemId,
    TenantId TenantId,
    string Number,
    WorkItemKind Kind,
    Priority Priority,
    Guid? PropertyId,
    Guid? UnitId,
    Guid? AssetId,
    Guid? DeficiencyId,
    Guid CreatedBy);

/// <summary>Payload for <c>Work.WorkItemAssigned</c>.</summary>
public sealed record WorkItemAssignedEvent(
    WorkItemId WorkItemId,
    TenantId TenantId,
    Guid? AssignedToPartyId,
    Guid? ContractorId,
    Guid AssignedBy);

/// <summary>Payload for <c>Work.WorkItemCompleted</c>.</summary>
public sealed record WorkItemCompletedEvent(
    WorkItemId WorkItemId,
    TenantId TenantId,
    DateTimeOffset CompletedAt,
    Guid CompletedBy);
