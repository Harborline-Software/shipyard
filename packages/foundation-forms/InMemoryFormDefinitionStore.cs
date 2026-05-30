using System.Runtime.CompilerServices;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Forms.Exceptions;
using Sunfish.Foundation.Forms.Models;

namespace Sunfish.Foundation.Forms;

/// <summary>
/// In-memory reference implementation of <see cref="IFormDefinitionStore"/>.
/// Suitable for tests, single-process bootstrapping, and the early
/// authoring-UX iteration loop; the production Postgres-backed
/// implementation ships in a follow-up PR that composes the
/// <c>foundation-assets-postgres</c> JSONB entity-store extension
/// (per ADR 0055 §"Entity Store").
/// </summary>
/// <remarks>
/// <para>
/// Storage shape: a nested dictionary keyed by tenant → schema id →
/// version → record. Mutation is serialised by a single
/// <see cref="SemaphoreSlim"/>; the dictionary itself is replaced on
/// each mutation so iteration over a snapshot is lock-free. The shape
/// keeps the implementation small enough for the keystone PR while
/// preserving the contract semantics that consumers will rely on once
/// the production implementation lands.
/// </para>
/// </remarks>
public sealed class InMemoryFormDefinitionStore : IFormDefinitionStore, IDisposable
{
    private readonly SemaphoreSlim _mutationLock = new(initialCount: 1, maxCount: 1);
    private readonly TimeProvider _time;

    // tenant → id → version → definition
    private Dictionary<TenantId, Dictionary<FormDefinitionId, Dictionary<SemanticVersion, FormDefinition>>> _store = new();

    /// <summary>Constructs a registry with the supplied <see cref="TimeProvider"/>.</summary>
    /// <param name="time">Time source for lifecycle-transition timestamps.
    /// Pass <see cref="TimeProvider.System"/> in production; pass a test
    /// double for deterministic clocks in tests.</param>
    public InMemoryFormDefinitionStore(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Constructs a registry using <see cref="TimeProvider.System"/>.</summary>
    public InMemoryFormDefinitionStore() : this(TimeProvider.System)
    {
    }

    /// <inheritdoc />
    public ValueTask<FormDefinition> GetAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
    {
        var snapshot = _store;
        if (snapshot.TryGetValue(tenant, out var byId) &&
            byId.TryGetValue(id, out var byVersion) &&
            byVersion.TryGetValue(version, out var schema))
        {
            return new ValueTask<FormDefinition>(schema);
        }

        throw new FormDefinitionNotFoundException(id, version, tenant);
    }

    /// <inheritdoc />
    public ValueTask<FormDefinition?> GetCurrentPublishedAsync(TenantId tenant, FormDefinitionId id, CancellationToken ct = default)
    {
        var snapshot = _store;
        if (!snapshot.TryGetValue(tenant, out var byId) || !byId.TryGetValue(id, out var byVersion))
        {
            return new ValueTask<FormDefinition?>((FormDefinition?)null);
        }

        FormDefinition? best = null;
        foreach (var revision in byVersion.Values)
        {
            if (revision.Status != FormDefinitionStatus.Published) continue;
            if (best is null || revision.Version > best.Version)
            {
                best = revision;
            }
        }

        return new ValueTask<FormDefinition?>(best);
    }

    /// <inheritdoc />
    public async ValueTask<FormDefinition> RegisterAsync(FormDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateOverlayOrThrow(definition);
        ValidateSchemaRefOrThrow(definition);

        await _mutationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var snapshot = _store;
            if (snapshot.TryGetValue(definition.Tenant, out var byId) &&
                byId.TryGetValue(definition.Id, out var byVersion) &&
                byVersion.ContainsKey(definition.Version))
            {
                throw new FormDefinitionConflictException(definition.Id, definition.Version, definition.Tenant);
            }

            // Optional lineage validation: parent MUST exist in the same tenant.
            if (definition.Lineage is { } lineage)
            {
                if (!snapshot.TryGetValue(definition.Tenant, out var lineageById) ||
                    !lineageById.TryGetValue(lineage.ParentDefinitionId, out var lineageByVersion) ||
                    !lineageByVersion.ContainsKey(lineage.ParentVersion))
                {
                    throw new FormDefinitionValidationException(
                        definition.Id,
                        $"lineage references parent '{lineage.ParentDefinitionId}' at version '{lineage.ParentVersion}' which is not registered in tenant '{definition.Tenant}'.");
                }
            }

            _store = MutateStore(snapshot, definition);
            return definition;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<FormDefinition> PublishAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => TransitionAsync(tenant, id, version, FormDefinitionStatus.Published, allowedFrom: new[] { FormDefinitionStatus.Draft, FormDefinitionStatus.Published }, ct);

    /// <inheritdoc />
    public ValueTask<FormDefinition> DeprecateAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => TransitionAsync(tenant, id, version, FormDefinitionStatus.Deprecated, allowedFrom: new[] { FormDefinitionStatus.Published, FormDefinitionStatus.Deprecated }, ct);

    /// <inheritdoc />
    public ValueTask<FormDefinition> WithdrawAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => TransitionAsync(tenant, id, version, FormDefinitionStatus.Withdrawn, allowedFrom: new[] { FormDefinitionStatus.Draft, FormDefinitionStatus.Published, FormDefinitionStatus.Deprecated, FormDefinitionStatus.Withdrawn }, ct);

    /// <inheritdoc />
    public async IAsyncEnumerable<FormDefinition> ListByTenantAsync(TenantId tenant, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _store;
        if (!snapshot.TryGetValue(tenant, out var byId))
        {
            yield break;
        }

        foreach (var idEntry in byId.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal))
        {
            foreach (var versionEntry in idEntry.Value.OrderBy(kv => kv.Key))
            {
                ct.ThrowIfCancellationRequested();
                yield return versionEntry.Value;
                await Task.Yield();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _mutationLock.Dispose();

    private async ValueTask<FormDefinition> TransitionAsync(
        TenantId tenant,
        FormDefinitionId id,
        SemanticVersion version,
        FormDefinitionStatus target,
        FormDefinitionStatus[] allowedFrom,
        CancellationToken ct)
    {
        await _mutationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var snapshot = _store;
            if (!snapshot.TryGetValue(tenant, out var byId) ||
                !byId.TryGetValue(id, out var byVersion) ||
                !byVersion.TryGetValue(version, out var existing))
            {
                throw new FormDefinitionNotFoundException(id, version, tenant);
            }

            if (!allowedFrom.Contains(existing.Status))
            {
                throw new InvalidOperationException(
                    $"FormDefinition '{id}' v{version} cannot transition from {existing.Status} to {target}; allowed source statuses are [{string.Join(", ", allowedFrom)}].");
            }

            if (existing.Status == target)
            {
                return existing;
            }

            var transitioned = existing with
            {
                Status = target,
                UpdatedAt = _time.GetUtcNow(),
            };

            _store = MutateStore(snapshot, transitioned);
            return transitioned;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private static Dictionary<TenantId, Dictionary<FormDefinitionId, Dictionary<SemanticVersion, FormDefinition>>> MutateStore(
        Dictionary<TenantId, Dictionary<FormDefinitionId, Dictionary<SemanticVersion, FormDefinition>>> snapshot,
        FormDefinition definition)
    {
        var rebuilt = new Dictionary<TenantId, Dictionary<FormDefinitionId, Dictionary<SemanticVersion, FormDefinition>>>(snapshot);
        if (!rebuilt.TryGetValue(definition.Tenant, out var byId))
        {
            byId = new Dictionary<FormDefinitionId, Dictionary<SemanticVersion, FormDefinition>>();
            rebuilt[definition.Tenant] = byId;
        }
        else
        {
            byId = new Dictionary<FormDefinitionId, Dictionary<SemanticVersion, FormDefinition>>(byId);
            rebuilt[definition.Tenant] = byId;
        }

        if (!byId.TryGetValue(definition.Id, out var byVersion))
        {
            byVersion = new Dictionary<SemanticVersion, FormDefinition>();
            byId[definition.Id] = byVersion;
        }
        else
        {
            byVersion = new Dictionary<SemanticVersion, FormDefinition>(byVersion);
            byId[definition.Id] = byVersion;
        }

        byVersion[definition.Version] = definition;
        return rebuilt;
    }

    private static void ValidateOverlayOrThrow(FormDefinition definition)
    {
        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in definition.Overlay.Sections)
        {
            if (!sectionIds.Add(section.Id))
            {
                throw new FormDefinitionValidationException(definition.Id, $"duplicate section id '{section.Id}'.");
            }

            foreach (var field in section.Fields)
            {
                if (!definition.Overlay.Fields.ContainsKey(field))
                {
                    throw new FormDefinitionValidationException(
                        definition.Id,
                        $"section '{section.Id}' references field '{field}' which is not declared in Overlay.Fields.");
                }
            }
        }

        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in definition.Overlay.Rules)
        {
            if (!ruleIds.Add(rule.Id))
            {
                throw new FormDefinitionValidationException(definition.Id, $"duplicate rule id '{rule.Id}'.");
            }

            if (rule.Scope != RuleScope.Schema && string.IsNullOrEmpty(rule.ScopeTarget))
            {
                throw new FormDefinitionValidationException(
                    definition.Id,
                    $"rule '{rule.Id}' scope '{rule.Scope}' requires a non-empty ScopeTarget.");
            }

            if (string.IsNullOrEmpty(rule.Expression))
            {
                throw new FormDefinitionValidationException(definition.Id, $"rule '{rule.Id}' has empty expression.");
            }
        }
    }

    private static void ValidateSchemaRefOrThrow(FormDefinition definition)
    {
        // The keystone holds a content-addressed reference into the kernel
        // schema registry (ADR 0055 OQ-3, dual-council ratified). The
        // canonical schema body lives in the kernel registry, which validates
        // JSON Schema 2020-12 conformance at registration. The keystone's
        // only structural invariant on SchemaRef is non-emptiness — a blank
        // reference is meaningless and indicates a caller bug.
        if (string.IsNullOrWhiteSpace(definition.SchemaRef.Value))
        {
            throw new FormDefinitionValidationException(definition.Id, "SchemaRef is empty.");
        }
    }
}
