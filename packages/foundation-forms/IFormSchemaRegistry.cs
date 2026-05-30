using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Forms.Exceptions;
using Sunfish.Foundation.Forms.Models;

namespace Sunfish.Foundation.Forms;

/// <summary>
/// Foundation-tier facade for the dynamic-forms schema registry (ADR 0055
/// keystone). The registry is the canonical authority for what schemas
/// exist in a tenant, what versions are live, and what lifecycle status
/// each revision holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a foundation-tier facade vs binding directly to
/// <c>Sunfish.Kernel.Schema.ISchemaRegistry</c>:</b> the kernel registry
/// stores raw JSON Schema documents content-addressed by their CID; the
/// keystone registry stores the higher-level <see cref="FormSchema"/>
/// composite (JSON Schema + overlay + lifecycle + tenant scoping). Many
/// consumers — the form engine, the entity store, the authoring UX —
/// need the composite, not the raw document. The facade also gives the
/// foundation-tier latitude to swap kernel-tier storage (in-memory in v1;
/// Postgres + CRDT in v1.1; bundle-distributed marketplace shards in v2)
/// without rippling the change through every consumer.
/// </para>
/// <para>
/// <b>Concurrency model:</b> all registry methods are safe for concurrent
/// calls from multiple async contexts. The in-memory reference
/// implementation uses a single <c>SemaphoreSlim</c> to serialise mutation
/// paths; the production Postgres-backed implementation will use row-level
/// versioning. Callers MUST treat <see cref="FormSchema"/> records
/// returned from the registry as immutable — registering a corrected
/// revision is the supported pattern, never mutating an in-memory record.
/// </para>
/// <para>
/// <b>Audit emission:</b> a registry implementation MAY emit kernel-audit
/// records for lifecycle transitions per ADR 0055 §"Trust impact" + ADR
/// 0049. The keystone interface does not surface an audit hook because
/// the audit substrate composition happens at the implementation layer
/// — the in-memory reference impl emits no audit; the production impl
/// emits via the kernel-audit substrate it already references.
/// </para>
/// </remarks>
public interface IFormSchemaRegistry
{
    /// <summary>
    /// Loads a specific revision of a schema.
    /// </summary>
    /// <param name="tenant">Tenant boundary; the registry will not return
    /// schemas from any other tenant.</param>
    /// <param name="id">Schema id.</param>
    /// <param name="version">Specific version to load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="FormSchemaNotFoundException">No schema with the
    /// requested id at the requested version exists in this tenant.</exception>
    ValueTask<FormSchema> GetAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default);

    /// <summary>
    /// Loads the current Published revision of a schema (the highest
    /// <see cref="SemanticVersion"/> among revisions at status
    /// <see cref="FormSchemaStatus.Published"/>). Returns <see langword="null"/>
    /// when no Published revision exists (the schema may be Draft-only or
    /// not yet registered at all).
    /// </summary>
    ValueTask<FormSchema?> GetCurrentPublishedAsync(TenantId tenant, FormSchemaId id, CancellationToken ct = default);

    /// <summary>
    /// Registers a new schema revision. The (tenant, id, version) tuple
    /// MUST not already exist; revisions are immutable.
    /// </summary>
    /// <exception cref="FormSchemaConflictException">A revision at this
    /// (tenant, id, version) is already registered.</exception>
    /// <exception cref="FormSchemaValidationException">The overlay
    /// violates an invariant (orphan section field, duplicate section id,
    /// etc.) or the JSON Schema text fails the registry's parse check.</exception>
    ValueTask<FormSchema> RegisterAsync(FormSchema schema, CancellationToken ct = default);

    /// <summary>
    /// Transitions a schema revision from <see cref="FormSchemaStatus.Draft"/>
    /// to <see cref="FormSchemaStatus.Published"/>. No-op if the revision is
    /// already Published. Throws when the revision is Deprecated or
    /// Withdrawn (forward-only transitions; rollback registers a new
    /// version with the desired state).
    /// </summary>
    ValueTask<FormSchema> PublishAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default);

    /// <summary>
    /// Transitions a Published revision to <see cref="FormSchemaStatus.Deprecated"/>.
    /// </summary>
    ValueTask<FormSchema> DeprecateAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default);

    /// <summary>
    /// Transitions a revision to <see cref="FormSchemaStatus.Withdrawn"/>.
    /// </summary>
    ValueTask<FormSchema> WithdrawAsync(TenantId tenant, FormSchemaId id, SemanticVersion version, CancellationToken ct = default);

    /// <summary>
    /// Enumerates all schemas registered for a tenant. Ordering is by
    /// (id ascending, version ascending). Includes all lifecycle statuses
    /// — filter at the call site if a status-specific view is needed.
    /// </summary>
    IAsyncEnumerable<FormSchema> ListByTenantAsync(TenantId tenant, CancellationToken ct = default);
}
