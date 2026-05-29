using Sunfish.Blocks.WorkItems.Models;
using Sunfish.Blocks.WorkItems.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkItems.Events;

/// <summary>
/// Default <see cref="IDeficiencyRaisedHandler"/>. Uses
/// <see cref="InMemoryWorkItemRepository.FindByDeficiencyId"/> for
/// the idempotency check, then delegates creation to
/// <see cref="IWorkItemService.CreateAsync"/>. Severity-string
/// mapping is intentionally narrow (Safety/Habitability map to a
/// real severity; everything else maps to Major) — the
/// blocks-property cluster's severity vocabulary is the source of
/// truth; unknown values default to Major so the WO still has a
/// defensible classification.
/// </summary>
public sealed class InMemoryDeficiencyRaisedHandler : IDeficiencyRaisedHandler
{
    private readonly InMemoryWorkItemRepository _repo;
    private readonly IWorkItemService _workOrderService;
    private readonly TenantId _tenantId;

    public InMemoryDeficiencyRaisedHandler(
        InMemoryWorkItemRepository repo,
        IWorkItemService workOrderService,
        TenantId? tenantId = null)
    {
        _repo             = repo             ?? throw new ArgumentNullException(nameof(repo));
        _workOrderService = workOrderService ?? throw new ArgumentNullException(nameof(workOrderService));
        _tenantId         = tenantId ?? TenantId.System;
    }

    /// <inheritdoc />
    public async Task<WorkItem> HandleAsync(
        DeficiencyRaisedEvent evt,
        Guid handledBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Idempotency — never create a second WO for the same
        // deficiency.
        var existing = _repo.FindByDeficiencyId(_tenantId, evt.DeficiencyId);
        if (existing is not null) return existing;

        var severity = MapSeverity(evt.Severity);
        var dueBy    = evt.DueBy ?? RequireDueByForSafetyOrHabitability(severity);
        var priority = severity switch
        {
            WorkItemSeverity.Safety       => Priority.Critical,
            WorkItemSeverity.Habitability => Priority.High,
            WorkItemSeverity.Major        => Priority.High,
            _                              => Priority.Normal,
        };

        return await _workOrderService.CreateAsync(
            tenantId:     _tenantId,
            title:        evt.Description,
            kind:         WorkItemKind.Repair,
            priority:     priority,
            createdBy:    handledBy,
            severity:     severity,
            dueBy:        dueBy,
            propertyId:   evt.PropertyId,
            unitId:       evt.UnitId,
            assetId:      evt.AssetId,
            deficiencyId: evt.DeficiencyId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static WorkItemSeverity MapSeverity(string severity)
        => severity?.Trim().ToLowerInvariant() switch
        {
            "safety"       => WorkItemSeverity.Safety,
            "habitability" => WorkItemSeverity.Habitability,
            "major"        => WorkItemSeverity.Major,
            "minor"        => WorkItemSeverity.Minor,
            "cosmetic"     => WorkItemSeverity.Cosmetic,
            _              => WorkItemSeverity.Major,
        };

    /// <summary>
    /// Default DueBy when the deficiency event didn't carry one but
    /// the mapped severity requires it. Falls back to 48h for Safety,
    /// 7d for Habitability — the SLA the blocks-property cluster
    /// surfaces today; PR 5's wiring layer can override per-tenant
    /// policy.
    /// </summary>
    private static DateTimeOffset? RequireDueByForSafetyOrHabitability(WorkItemSeverity severity)
        => severity switch
        {
            WorkItemSeverity.Safety       => DateTimeOffset.UtcNow.AddHours(48),
            WorkItemSeverity.Habitability => DateTimeOffset.UtcNow.AddDays(7),
            _                              => null,
        };
}
