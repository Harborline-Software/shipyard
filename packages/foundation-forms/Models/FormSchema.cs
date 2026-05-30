using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Forms.Models;

/// <summary>
/// <b>Canonical foundation-tier dynamic-forms keystone</b> (ADR 0055).
/// A <see cref="FormSchema"/> is the single load-bearing record on which
/// the entire dynamic-forms substrate composes — every other surface
/// (the entity instance store, the form engine, the rule evaluator, the
/// CRDT sync substrate, the audit trail, the admin authoring UX) binds to
/// this type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Composition vs. duplication.</b> A form schema is fundamentally
/// a JSON Schema 2020-12 document plus the Sunfish overlay (sections,
/// rules, permissions, i18n) plus lifecycle metadata. The kernel-tier
/// <c>Sunfish.Kernel.Schema.Schema</c> (in <c>kernel-schema-registry</c>)
/// is the content-addressed canonicalization of the raw JSON Schema
/// document; the keystone <see cref="FormSchema"/> is the higher-level
/// composite that adds everything beyond pure structural validation.
/// The keystone holds the JSON Schema document by value (the
/// <see cref="JsonSchema"/> property) — a future PR can layer a
/// content-addressed reference if the storage cost of inlining motivates
/// it, but for v1 the document inlines so consumers see the full schema
/// without a second round-trip to the kernel registry.
/// </para>
/// <para>
/// <b>Identity is the tuple (Id, Version).</b> A schema id is reusable
/// across versions; each version is a distinct, immutable record. The
/// registry indexes by id and returns the highest <see cref="Version"/>
/// at status <see cref="FormSchemaStatus.Published"/> when no specific
/// version is requested.
/// </para>
/// <para>
/// <b>Tenant isolation.</b> Every schema is tenant-scoped via
/// <see cref="Tenant"/>. The registry enforces that lookups, listings,
/// and lineage references all stay within a tenant boundary;
/// cross-tenant schema sharing (the marketplace v2 ambition in ADR 0055
/// §"Revisit triggers") is explicitly out of scope for the keystone.
/// </para>
/// <para>
/// <b>Audit emission.</b> The registry emits a kernel-audit record for
/// every lifecycle transition (Register / Publish / Deprecate / Withdraw)
/// per ADR 0055 §"Trust impact" + ADR 0049. The audit event types ship
/// with the rule-engine + entity-store PRs that follow the keystone —
/// the keystone records the lifecycle data so those audit emissions have
/// something to read; it does NOT itself emit audit events (no
/// kernel-audit reference on the keystone csproj, to keep the keystone
/// composable in test contexts that do not stand up audit infrastructure).
/// </para>
/// </remarks>
/// <param name="Id">Schema id (reusable across versions; the
/// <c>(Id, Version)</c> tuple is unique).</param>
/// <param name="Version">Semantic version of this revision.</param>
/// <param name="Status">Lifecycle status. New schemas typically register
/// as <see cref="FormSchemaStatus.Draft"/> and transition to
/// <see cref="FormSchemaStatus.Published"/> via the registry's
/// <c>PublishAsync</c> call.</param>
/// <param name="Tenant">Owning tenant; the registry enforces tenant isolation
/// on all lookups.</param>
/// <param name="Owner">Identity reference for the authoring principal
/// (typically the tenant admin who registered the schema; for seed data
/// use <see cref="IdentityRef.System"/>).</param>
/// <param name="JsonSchema">Raw JSON Schema 2020-12 document text. The
/// keystone treats this as an opaque string at registration time;
/// validation that the text is syntactically well-formed JSON happens at
/// registration; validation that the text is a syntactically valid
/// JSON Schema document is the registry's responsibility (the in-memory
/// reference impl in v1 does the JSON-parse check only; the production
/// Postgres-backed registry composes <c>JsonSchema.Net</c> for full
/// 2020-12 validation, per ADR 0055 OQ-DF1).</param>
/// <param name="Overlay">The Sunfish overlay — fields, sections, rules,
/// i18n. The registry enforces overlay invariants (every field referenced
/// by a section appears in <see cref="SunfishOverlay.Fields"/>; section
/// and rule ids are unique; etc.).</param>
/// <param name="Lineage">Optional extension lineage (this schema extends
/// another schema). Composed with ADR 0005 type customization.</param>
/// <param name="CreatedAt">UTC timestamp at which this revision was first
/// registered.</param>
/// <param name="UpdatedAt">UTC timestamp at which this revision's
/// <see cref="Status"/> was last transitioned. Equals
/// <see cref="CreatedAt"/> on a freshly-registered revision.</param>
public sealed record FormSchema(
    FormSchemaId Id,
    SemanticVersion Version,
    FormSchemaStatus Status,
    TenantId Tenant,
    IdentityRef Owner,
    string JsonSchema,
    SunfishOverlay Overlay,
    FormSchemaLineage? Lineage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
