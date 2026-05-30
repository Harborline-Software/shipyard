using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.WorkProjects.Services;

/// <summary>
/// In-memory store for <see cref="TimeEntry"/>. Tenant-scoped reads
/// enforce <see cref="TimeEntry.TenantId"/> match (H5 — cross-tenant
/// reads return null).
/// </summary>
public sealed class InMemoryTimeEntryRepository
{
    private readonly ConcurrentDictionary<TimeEntryId, TimeEntry> _entries = new();

    public void Upsert(TimeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Id] = entry;
    }

    public TimeEntry? GetById(TenantId tenantId, TimeEntryId id)
    {
        if (!_entries.TryGetValue(id, out var entry)) return null;
        if (!entry.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)) return null;
        return entry;
    }

    /// <summary>
    /// Snapshot of all non-deleted entries in a tenant — internal so
    /// the public surface matches the future Postgres impl (which
    /// won't expose an unbounded tenant scan). Tests + <c>TimeLog</c>
    /// builds inside the assembly via <c>InternalsVisibleTo</c>.
    /// </summary>
    internal IReadOnlyList<TimeEntry> ListByTenant(TenantId tenantId)
        => _entries.Values
            .Where(e => e.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && e.DeletedAt is null)
            .ToList();

    /// <summary>
    /// Non-deleted entries for a single project within the tenant (H5).
    /// Project-scoped predicate — the public read surface
    /// (<see cref="ITimeEntryService.GetByProjectAsync"/>) routes here so
    /// the future Postgres impl can serve it with a single indexed query
    /// rather than an unbounded tenant scan.
    /// </summary>
    internal IReadOnlyList<TimeEntry> ListByProject(TenantId tenantId, ProjectId projectId)
        => _entries.Values
            .Where(e => e.TenantId.Value.Equals(tenantId.Value, StringComparison.Ordinal)
                        && e.ProjectId == projectId
                        && e.DeletedAt is null)
            .ToList();
}
