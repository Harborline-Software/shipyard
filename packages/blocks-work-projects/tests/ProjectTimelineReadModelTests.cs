using System.Text.Json;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// C2.1 cockpit-readiness coverage for
/// <see cref="IProjectTimelineReadModel"/> — the read-side projection
/// backing the <c>projects.ganttView.enabled</c> bundle feature.
///
/// <para>
/// These tests prove the Gantt/timeline substrate is cockpit-ready:
/// the projection assembles project + milestone schedule data, holds
/// tenant-scoping (H5), preserves milestone ordering + dependency edges,
/// and serializes cleanly to JSON for the C2.2 Bridge endpoint.
/// </para>
/// </summary>
public sealed class ProjectTimelineReadModelTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly TenantId OtherTenant = new("test-tenant-2");
    private static readonly Guid Actor = Guid.NewGuid();

    private static (InMemoryProjectTimelineReadModel readModel,
                    InMemoryProjectRepository projects,
                    InMemoryProjectMilestoneRepository milestones) NewHarness()
    {
        var projects   = new InMemoryProjectRepository();
        var milestones = new InMemoryProjectMilestoneRepository();
        return (new InMemoryProjectTimelineReadModel(projects, milestones), projects, milestones);
    }

    private static Project NewProject(TenantId tenant)
        => Project.Create(
            tenant,
            ProjectId.NewId(),
            "PRJ-2026-T00001",
            "Client engagement",
            ProjectKind.Generic,
            Priority.Normal,
            ownerPartyId: Actor,
            createdBy: Actor,
            createdAt: Instant.Now,
            plannedStartDate: new DateOnly(2026, 6, 1),
            plannedEndDate: new DateOnly(2026, 9, 30));

    private static ProjectMilestone NewMilestone(
        TenantId tenant, ProjectId pid, string code, DateOnly planned, MilestoneId? predecessor = null)
        => ProjectMilestone.Create(
            tenant, MilestoneId.NewId(), pid, code, $"Milestone {code}",
            MilestoneKind.Schedule, planned, Actor, Instant.Now,
            predecessorMilestoneId: predecessor);

    [Fact]
    public async Task GetTimeline_UnknownProject_ReturnsNull()
    {
        var (rm, _, _) = NewHarness();
        var result = await rm.GetTimelineAsync(Tenant, ProjectId.NewId());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTimeline_AssemblesProjectSpanAndMilestones()
    {
        var (rm, projects, milestones) = NewHarness();
        var p = NewProject(Tenant);
        projects.Upsert(p);
        milestones.Upsert(NewMilestone(Tenant, p.Id, "M1", new DateOnly(2026, 6, 15)));
        milestones.Upsert(NewMilestone(Tenant, p.Id, "M2", new DateOnly(2026, 8, 1)));

        var timeline = await rm.GetTimelineAsync(Tenant, p.Id);

        Assert.NotNull(timeline);
        Assert.Equal(p.Id, timeline!.ProjectId);
        Assert.Equal("PRJ-2026-T00001", timeline.Code);
        Assert.Equal(new DateOnly(2026, 6, 1), timeline.PlannedStart);
        Assert.Equal(new DateOnly(2026, 9, 30), timeline.PlannedEnd);
        Assert.Equal(2, timeline.Milestones.Count);
    }

    [Fact]
    public async Task GetTimeline_OrdersMilestonesByPlannedDate()
    {
        var (rm, projects, milestones) = NewHarness();
        var p = NewProject(Tenant);
        projects.Upsert(p);
        // Insert out of order; projection must surface planned-date order.
        milestones.Upsert(NewMilestone(Tenant, p.Id, "Late", new DateOnly(2026, 8, 1)));
        milestones.Upsert(NewMilestone(Tenant, p.Id, "Early", new DateOnly(2026, 6, 15)));

        var timeline = await rm.GetTimelineAsync(Tenant, p.Id);

        Assert.NotNull(timeline);
        Assert.Equal("Early", timeline!.Milestones[0].Code);
        Assert.Equal("Late", timeline.Milestones[1].Code);
    }

    [Fact]
    public async Task GetTimeline_CarriesDependencyEdge()
    {
        var (rm, projects, milestones) = NewHarness();
        var p = NewProject(Tenant);
        projects.Upsert(p);
        var first = NewMilestone(Tenant, p.Id, "M1", new DateOnly(2026, 6, 15));
        var second = NewMilestone(Tenant, p.Id, "M2", new DateOnly(2026, 8, 1), predecessor: first.Id);
        milestones.Upsert(first);
        milestones.Upsert(second);

        var timeline = await rm.GetTimelineAsync(Tenant, p.Id);

        Assert.NotNull(timeline);
        var firstBar = Assert.Single(timeline!.Milestones, m => m.Code == "M1");
        var secondBar = Assert.Single(timeline.Milestones, m => m.Code == "M2");
        Assert.Null(firstBar.PredecessorMilestoneId);
        Assert.Equal(first.Id, secondBar.PredecessorMilestoneId);
    }

    [Fact]
    public async Task GetTimeline_TenantScoped_DoesNotLeakAcrossTenants()
    {
        var (rm, projects, milestones) = NewHarness();
        var p = NewProject(Tenant);
        projects.Upsert(p);
        milestones.Upsert(NewMilestone(Tenant, p.Id, "M1", new DateOnly(2026, 6, 15)));

        // Same project id queried under a different tenant — H5 must hold.
        var leaked = await rm.GetTimelineAsync(OtherTenant, p.Id);
        Assert.Null(leaked);
    }

    [Fact]
    public async Task GetTimeline_SerializesToJsonRoundTrip()
    {
        var (rm, projects, milestones) = NewHarness();
        var p = NewProject(Tenant);
        projects.Upsert(p);
        var first = NewMilestone(Tenant, p.Id, "M1", new DateOnly(2026, 6, 15));
        milestones.Upsert(first);
        milestones.Upsert(NewMilestone(Tenant, p.Id, "M2", new DateOnly(2026, 8, 1), predecessor: first.Id));

        var timeline = await rm.GetTimelineAsync(Tenant, p.Id);
        Assert.NotNull(timeline);

        // Prove the projection is wire-ready for the C2.2 Bridge endpoint:
        // strongly-typed ids carry JsonConverters, so a round-trip is lossless.
        var json = JsonSerializer.Serialize(timeline);
        var back = JsonSerializer.Deserialize<ProjectTimeline>(json);

        Assert.NotNull(back);
        Assert.Equal(timeline!.ProjectId, back!.ProjectId);
        Assert.Equal(timeline.Milestones.Count, back.Milestones.Count);
        Assert.Equal(
            timeline.Milestones[1].PredecessorMilestoneId,
            back.Milestones[1].PredecessorMilestoneId);
    }

    /// <summary>
    /// Cockpit-readiness assertion: the substrate satisfies the two
    /// <c>projects.*</c> featureDefaults from the project-management
    /// bundle manifest. <c>projects.budgetTracking.enabled</c> is backed
    /// by <see cref="ProjectBudget"/> + <see cref="ProjectActual"/> (asserted
    /// in their own suites); <c>projects.ganttView.enabled</c> is backed by
    /// the timeline projection exercised here — schedule span + ordered,
    /// dependency-aware milestone bars are all present and serializable.
    /// </summary>
    [Fact]
    public async Task GanttFeatureDefault_Satisfiable_TimelineExposesScheduleAndDeps()
    {
        var (rm, projects, milestones) = NewHarness();
        var p = NewProject(Tenant);
        projects.Upsert(p);
        var first = NewMilestone(Tenant, p.Id, "M1", new DateOnly(2026, 6, 15));
        milestones.Upsert(first);
        milestones.Upsert(NewMilestone(Tenant, p.Id, "M2", new DateOnly(2026, 8, 1), predecessor: first.Id));

        var timeline = await rm.GetTimelineAsync(Tenant, p.Id);

        Assert.NotNull(timeline);
        // Schedule span available for the project bar.
        Assert.True(timeline!.PlannedStart.HasValue && timeline.PlannedEnd.HasValue);
        // Every milestone bar carries a planned date (the Gantt x-axis anchor).
        Assert.All(timeline.Milestones, m => Assert.True(m.PlannedDate != default));
        // At least one dependency edge is expressible end-to-end.
        Assert.Contains(timeline.Milestones, m => m.PredecessorMilestoneId is not null);
    }
}
