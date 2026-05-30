using System.Runtime.CompilerServices;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Forms.Exceptions;
using Sunfish.Foundation.Forms.Models;

namespace Sunfish.Foundation.Forms;

/// <summary>
/// In-memory reference implementation of <see cref="IFormSchemaRegistry"/>.
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
public sealed class InMemoryFormSchemaRegistry : IFormSchemaRegistry, IDisposable
{
    private readonly SemaphoreSlim _mutationLock = new(initialCount: 1, maxCount: 1);
    private readonly TimeProvider _time;

    // tenant → id → version → schema
    private Dictionary<TenantId, Dictionary<FormSchemaId, Dictionary<SemanticVersion, FormSchema>>> _store = new();

    /// <summary>Constructs a registry with the supplied <see cref="TimeProvider"/>.</summary>
    /// <param name="time">Time source for lifecycle-transition timestamps.
    /// Pass <see cref="TimeProvider.System"/> in production; pass a test
    /// double for deterministic clocks in tests.</param>
    public InMemoryFormSchemaRegistry(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <summary>Constructs a registry using <see cref="TimeProvider.System"/>.</summary>
    public InMemoryFormSchemaRegistry() : this(TimeProvider.System)
    {
    }

    /// <inheritdoc />
    public ValueTask<FormSchema> GetAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default)
    {
        var snapshot = _store;
        if (snapshot.TryGetValue(tenant, out var byId) &&
            byId.TryGetValue(id, out var byVersion) &&
            byVersion.TryGetValue(version, out var schema))
        {
            return new ValueTask<FormSchema>(schema);
        }

        throw new FormSchemaNotFoundException(id, version, tenant);
    }

    /// <inheritdoc />
    public ValueTask<FormSchema?> GetCurrentPublishedAsync(TenantId tenant, FormSchemaId id, CancellationToken ct = default)
    {
        var snapshot = _store;
        if (!snapshot.TryGetValue(tenant, out var byId) || !byId.TryGetValue(id, out var byVersion))
        {
            return new ValueTask<FormSchema?>((FormSchema?)null);
        }

        FormSchema? best = null;
        foreach (var revision in byVersion.Values)
        {
            if (revision.Status != FormSchemaStatus.Published) continue;
            if (best is null || revision.Version > best.Version)
            {
                best = revision;
            }
        }

        return new ValueTask<FormSchema?>(best);
    }

    /// <inheritdoc />
    public async ValueTask<FormSchema> RegisterAsync(FormSchema schema, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ValidateOverlayOrThrow(schema);
        ValidateJsonSchemaTextOrThrow(schema);

        await _mutationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var snapshot = _store;
            if (snapshot.TryGetValue(schema.Tenant, out var byId) &&
                byId.TryGetValue(schema.Id, out var byVersion) &&
                byVersion.ContainsKey(schema.Version))
            {
                throw new FormSchemaConflictException(schema.Id, schema.Version, schema.Tenant);
            }

            // Optional lineage validation: parent MUST exist in the same tenant.
            if (schema.Lineage is { } lineage)
            {
                if (!snapshot.TryGetValue(schema.Tenant, out var lineageById) ||
                    !lineageById.TryGetValue(lineage.ParentSchemaId, out var lineageByVersion) ||
                    !lineageByVersion.ContainsKey(lineage.ParentVersion))
                {
                    throw new FormSchemaValidationException(
                        schema.Id,
                        $"lineage references parent '{lineage.ParentSchemaId}' at version '{lineage.ParentVersion}' which is not registered in tenant '{schema.Tenant}'.");
                }
            }

            _store = MutateStore(snapshot, schema);
            return schema;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask<FormSchema> PublishAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default)
        => TransitionAsync(tenant, id, version, FormSchemaStatus.Published, allowedFrom: new[] { FormSchemaStatus.Draft, FormSchemaStatus.Published }, ct);

    /// <inheritdoc />
    public ValueTask<FormSchema> DeprecateAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default)
        => TransitionAsync(tenant, id, version, FormSchemaStatus.Deprecated, allowedFrom: new[] { FormSchemaStatus.Published, FormSchemaStatus.Deprecated }, ct);

    /// <inheritdoc />
    public ValueTask<FormSchema> WithdrawAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default)
        => TransitionAsync(tenant, id, version, FormSchemaStatus.Withdrawn, allowedFrom: new[] { FormSchemaStatus.Draft, FormSchemaStatus.Published, FormSchemaStatus.Deprecated, FormSchemaStatus.Withdrawn }, ct);

    /// <inheritdoc />
    public async IAsyncEnumerable<FormSchema> ListByTenantAsync(TenantId tenant, [EnumeratorCancellation] CancellationToken ct = default)
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

    private async ValueTask<FormSchema> TransitionAsync(
        TenantId tenant,
        FormSchemaId id,
        SemanticVersion version,
        FormSchemaStatus target,
        FormSchemaStatus[] allowedFrom,
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
                throw new FormSchemaNotFoundException(id, version, tenant);
            }

            if (!allowedFrom.Contains(existing.Status))
            {
                throw new InvalidOperationException(
                    $"FormSchema '{id}' v{version} cannot transition from {existing.Status} to {target}; allowed source statuses are [{string.Join(", ", allowedFrom)}].");
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

    private static Dictionary<TenantId, Dictionary<FormSchemaId, Dictionary<SemanticVersion, FormSchema>>> MutateStore(
        Dictionary<TenantId, Dictionary<FormSchemaId, Dictionary<SemanticVersion, FormSchema>>> snapshot,
        FormSchema schema)
    {
        var rebuilt = new Dictionary<TenantId, Dictionary<FormSchemaId, Dictionary<SemanticVersion, FormSchema>>>(snapshot);
        if (!rebuilt.TryGetValue(schema.Tenant, out var byId))
        {
            byId = new Dictionary<FormSchemaId, Dictionary<SemanticVersion, FormSchema>>();
            rebuilt[schema.Tenant] = byId;
        }
        else
        {
            byId = new Dictionary<FormSchemaId, Dictionary<SemanticVersion, FormSchema>>(byId);
            rebuilt[schema.Tenant] = byId;
        }

        if (!byId.TryGetValue(schema.Id, out var byVersion))
        {
            byVersion = new Dictionary<SemanticVersion, FormSchema>();
            byId[schema.Id] = byVersion;
        }
        else
        {
            byVersion = new Dictionary<SemanticVersion, FormSchema>(byVersion);
            byId[schema.Id] = byVersion;
        }

        byVersion[schema.Version] = schema;
        return rebuilt;
    }

    private static void ValidateOverlayOrThrow(FormSchema schema)
    {
        var sectionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in schema.Overlay.Sections)
        {
            if (!sectionIds.Add(section.Id))
            {
                throw new FormSchemaValidationException(schema.Id, $"duplicate section id '{section.Id}'.");
            }

            foreach (var field in section.Fields)
            {
                if (!schema.Overlay.Fields.ContainsKey(field))
                {
                    throw new FormSchemaValidationException(
                        schema.Id,
                        $"section '{section.Id}' references field '{field}' which is not declared in Overlay.Fields.");
                }
            }
        }

        var ruleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in schema.Overlay.Rules)
        {
            if (!ruleIds.Add(rule.Id))
            {
                throw new FormSchemaValidationException(schema.Id, $"duplicate rule id '{rule.Id}'.");
            }

            if (rule.Scope != RuleScope.Schema && string.IsNullOrEmpty(rule.ScopeTarget))
            {
                throw new FormSchemaValidationException(
                    schema.Id,
                    $"rule '{rule.Id}' scope '{rule.Scope}' requires a non-empty ScopeTarget.");
            }

            if (string.IsNullOrEmpty(rule.Expression))
            {
                throw new FormSchemaValidationException(schema.Id, $"rule '{rule.Id}' has empty expression.");
            }
        }
    }

    private static void ValidateJsonSchemaTextOrThrow(FormSchema schema)
    {
        if (string.IsNullOrWhiteSpace(schema.JsonSchema))
        {
            throw new FormSchemaValidationException(schema.Id, "JsonSchema text is empty.");
        }

        // The in-memory reference impl checks JSON well-formedness only.
        // Full JSON Schema 2020-12 conformance lives in the production
        // Postgres-backed implementation per ADR 0055 OQ-DF1 (NJsonSchema
        // or JsonSchema.Net — selection deferred to the production-impl PR).
        try
        {
            using var doc = JsonDocument.Parse(schema.JsonSchema);
        }
        catch (JsonException ex)
        {
            throw new FormSchemaValidationException(schema.Id, $"JsonSchema text is not valid JSON: {ex.Message}");
        }
    }
}
