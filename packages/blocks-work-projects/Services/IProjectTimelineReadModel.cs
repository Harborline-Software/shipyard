using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// Read-side projection that assembles a <see cref="Project"/> and its
/// <see cref="ProjectMilestone"/>s into Gantt-renderable timeline rows.
///
/// <para>
/// Backs the <c>projects.ganttView.enabled</c> bundle feature
/// (<c>foundation-catalog</c> project-management bundle manifest). The
/// underlying schedule data already lives on the entities — project
/// planned/actual date ranges + per-milestone planned/actual dates +
/// the single-predecessor dependency edge
/// (<see cref="ProjectMilestone.PredecessorMilestoneId"/>). This contract
/// surfaces that data as an ordered, dependency-aware row set so a Gantt
/// cockpit (C2.2 Bridge endpoint / C2.3 web view) can render it without
/// reaching into the repositories directly or re-deriving the ordering.
/// </para>
///
/// <para>
/// Read-only and tenant-scoped (H5): every accessor takes a
/// <see cref="TenantId"/> and returns only rows owned by that tenant.
/// No mutation surface — the timeline is a derived view of the project +
/// milestone write models.
/// </para>
/// </summary>
public interface IProjectTimelineReadModel
{
    /// <summary>
    /// Build the timeline for a single project, or null when the project
    /// is unknown / not owned by <paramref name="tenantId"/>. Milestone
    /// bars are ordered by planned date (matching the milestone
    /// repository's ordering); each carries its predecessor edge so the
    /// renderer can draw dependency links.
    /// </summary>
    Task<ProjectTimeline?> GetTimelineAsync(
        TenantId tenantId,
        ProjectId id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gantt-ready view of a project: the project's own planned/actual span
/// plus its milestone bars in planned-date order.
/// </summary>
public sealed record ProjectTimeline(
    ProjectId ProjectId,
    string Code,
    string Name,
    ProjectStatus Status,
    DateOnly? PlannedStart,
    DateOnly? PlannedEnd,
    DateOnly? ActualStart,
    DateOnly? ActualEnd,
    decimal? PercentComplete,
    IReadOnlyList<ProjectTimelineMilestone> Milestones);

/// <summary>
/// A single milestone bar within a <see cref="ProjectTimeline"/>.
/// <see cref="PredecessorMilestoneId"/> carries the dependency edge for
/// the Gantt renderer (null when the milestone has no predecessor).
/// </summary>
public sealed record ProjectTimelineMilestone(
    MilestoneId Id,
    string Code,
    string Name,
    MilestoneKind Kind,
    MilestoneStatus Status,
    DateOnly PlannedDate,
    DateOnly? ActualDate,
    MilestoneId? PredecessorMilestoneId);

/// <summary>
/// Default <see cref="IProjectTimelineReadModel"/>. Composes the existing
/// <see cref="InMemoryProjectRepository"/> +
/// <see cref="InMemoryProjectMilestoneRepository"/> — no new storage.
/// </summary>
public sealed class InMemoryProjectTimelineReadModel : IProjectTimelineReadModel
{
    private readonly InMemoryProjectRepository _projects;
    private readonly InMemoryProjectMilestoneRepository _milestones;

    public InMemoryProjectTimelineReadModel(
        InMemoryProjectRepository projects,
        InMemoryProjectMilestoneRepository milestones)
    {
        _projects   = projects   ?? throw new ArgumentNullException(nameof(projects));
        _milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
    }

    public Task<ProjectTimeline?> GetTimelineAsync(
        TenantId tenantId,
        ProjectId id,
        CancellationToken cancellationToken = default)
    {
        var p = _projects.GetById(tenantId, id);
        if (p is null)
            return Task.FromResult<ProjectTimeline?>(null);

        // GetByProject already enforces tenant-scoping, soft-delete
        // filtering, and PlannedDate ordering.
        var bars = _milestones
            .GetByProject(tenantId, id)
            .Select(m => new ProjectTimelineMilestone(
                m.Id,
                m.Code,
                m.Name,
                m.Kind,
                m.Status,
                m.PlannedDate,
                m.ActualDate,
                m.PredecessorMilestoneId))
            .ToList();

        var timeline = new ProjectTimeline(
            p.Id,
            p.Code,
            p.Name,
            p.Status,
            p.PlannedStartDate,
            p.PlannedEndDate,
            p.ActualStartDate,
            p.ActualEndDate,
            p.PercentComplete,
            bars);

        return Task.FromResult<ProjectTimeline?>(timeline);
    }
}
