namespace Sunfish.Foundation.Forms.Models;

/// <summary>
/// A grouping of fields within a <see cref="FormDefinition"/> (ADR 0055
/// §"Section-based permissions"; composes the dynamic-forms-authorization
/// UPF Approach F).
/// </summary>
/// <remarks>
/// <para>
/// Sections are the unit at which authorization is enforced. The form engine
/// intersects the section list with the actor's macaroon scope (ADR 0032);
/// a vendor or sub-tenant collaborator sees only the sections their token
/// authorizes, with the rest collapsed or omitted entirely depending on
/// the rendering surface. Field-level annotations remain available as an
/// escape hatch (declared in <see cref="FieldOverlay"/>) but section-level
/// is the canonical posture.
/// </para>
/// </remarks>
/// <param name="Id">Stable section identifier, unique within the owning
/// <see cref="FormDefinition"/>. Recommended format <c>{purpose}</c>
/// (<c>tenancy</c>, <c>financial</c>, <c>maintenance</c>). MUST be non-empty.</param>
/// <param name="Title">Localized section title.</param>
/// <param name="Fields">Ordered list of field names that belong to this
/// section. Field names MUST be valid JSON property names within the
/// schema's <c>JsonSchema</c> document.</param>
/// <param name="Access">Read / write authorization for this section.</param>
public sealed record FormSection(
    string Id,
    InternationalizedText Title,
    IReadOnlyList<string> Fields,
    SectionAccess Access);

/// <summary>
/// Read / write authorization for a <see cref="FormSection"/> (ADR 0055
/// §"Section-based permissions").
/// </summary>
/// <remarks>
/// <para>
/// Roles are opaque strings interpreted by the consumer's authorization
/// substrate (typically the macaroon scope vocabulary; for example
/// <c>tenant:admin</c>, <c>vendor:maintenance</c>, <c>collaborator:invited</c>).
/// The keystone substrate does not bind to a specific role taxonomy —
/// authorization enforcement lives in the form engine layered on top.
/// </para>
/// <para>
/// An empty <see cref="ReadRoles"/> list means "no role can read this
/// section" (closed by default). An empty <see cref="WriteRoles"/> means
/// the section is read-only for everyone. The
/// <see cref="ReadConditionExpression"/> is an optional Tier-1
/// (JSON Schema if/then/else) conditional that further narrows read
/// authorization beyond the role check ("vendors with role
/// <c>vendor:maintenance</c> can read this section only when
/// <c>status === 'open'</c>").
/// </para>
/// </remarks>
/// <param name="ReadRoles">Opaque role tokens authorized to read this section.</param>
/// <param name="WriteRoles">Opaque role tokens authorized to write this section.</param>
/// <param name="ReadConditionExpression">Optional Tier-1 conditional further
/// narrowing read authorization; the rule evaluator (forthcoming) interprets
/// this against the entity instance.</param>
public sealed record SectionAccess(
    IReadOnlyList<string> ReadRoles,
    IReadOnlyList<string> WriteRoles,
    string? ReadConditionExpression = null);
