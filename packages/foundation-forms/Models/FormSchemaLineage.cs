namespace Sunfish.Foundation.Forms.Models;

/// <summary>
/// Optional extension lineage for a <see cref="FormSchema"/> — declares
/// that this schema extends another schema by adding fields, sections,
/// or rules (ADR 0055 §"Type Customization Model", composing ADR 0005).
/// </summary>
/// <remarks>
/// <para>
/// Extension is purely declarative on the keystone substrate. The form
/// engine + entity store layered on top use the lineage to:
/// </para>
/// <list type="bullet">
/// <item><description>Render the parent's sections before the extension's
///   sections (familiar layout for users who know the parent type).</description></item>
/// <item><description>Allow a single entity instance to satisfy both the
///   parent schema and the extended schema's invariants — useful for the
///   property-equipment EXTEND pattern (an extended <c>Property</c> is
///   still a <c>Property</c> for system code that doesn't know about the
///   extension).</description></item>
/// <item><description>Surface the lineage in the audit trail when schema
///   evolution events fire.</description></item>
/// </list>
/// <para>
/// Extension is acyclic and shallow in v1 — a schema may extend at most
/// one parent, and chains of extension are limited to depth 3 (parent /
/// child / grandchild). The keystone registry enforces the depth limit at
/// registration time.
/// </para>
/// </remarks>
/// <param name="ParentSchemaId">The schema this schema extends. MUST refer
/// to a schema already registered in the same registry.</param>
/// <param name="ParentVersion">Specific parent version this extension targets.
/// Pinned so that a parent's version bump does not silently shift the
/// extension's effective shape.</param>
public sealed record FormSchemaLineage(
    FormSchemaId ParentSchemaId,
    SemanticVersion ParentVersion);
