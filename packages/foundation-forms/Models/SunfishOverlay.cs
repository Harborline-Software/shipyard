namespace Sunfish.Foundation.Forms.Models;

/// <summary>
/// Sunfish-specific overlay carried alongside a <see cref="FormSchema"/>'s
/// JSON Schema document (ADR 0055 §"Schema Registry").
/// </summary>
/// <remarks>
/// <para>
/// JSON Schema 2020-12 expresses structural validation but says nothing
/// about UI rendering, internationalization, section-based authorization,
/// or cross-field rules. The overlay carries everything Sunfish adds on
/// top — labels, control hints, PII classification, sections, rules — as
/// a single composable record so the schema definition stays a coherent
/// unit on the CRDT sync substrate (ADR 0028) and the audit trail
/// (ADR 0049).
/// </para>
/// <para>
/// <b>Invariants checked by <see cref="IFormSchemaRegistry"/>:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Every key in <see cref="Fields"/> MUST correspond to
///   a property declared in the schema's JSON Schema document.</description></item>
/// <item><description>Every field referenced by a <see cref="FormSection.Fields"/>
///   list MUST appear in <see cref="Fields"/>.</description></item>
/// <item><description>Every <see cref="FormSection.Id"/> MUST be unique
///   within the overlay.</description></item>
/// <item><description>Every <see cref="RuleDefinition.Id"/> MUST be unique
///   within the overlay.</description></item>
/// </list>
/// <para>
/// The keystone registry enforces these invariants at registration time;
/// callers receive a <c>FormSchemaValidationException</c> on violation
/// (forthcoming exception type — for the keystone PR the in-memory
/// reference registry throws <see cref="ArgumentException"/> with a
/// descriptive message, and the exception type will be promoted to a
/// dedicated class once a second consumer surfaces the need).
/// </para>
/// </remarks>
/// <param name="Fields">Per-field overlay map keyed by JSON property name.</param>
/// <param name="Sections">Ordered list of sections; section order is the
/// default field-presentation order for the form engine.</param>
/// <param name="Rules">Cross-field rules attached to this schema (the
/// rule-engine package interprets them; the keystone stores them as data).</param>
/// <param name="Title">Optional localized title for the schema.</param>
/// <param name="Description">Optional localized description.</param>
public sealed record SunfishOverlay(
    IReadOnlyDictionary<string, FieldOverlay> Fields,
    IReadOnlyList<FormSection> Sections,
    IReadOnlyList<RuleDefinition> Rules,
    InternationalizedText? Title = null,
    InternationalizedText? Description = null)
{
    /// <summary>
    /// Empty overlay (no fields, no sections, no rules) — useful for tests
    /// and for bootstrap schemas registered before the authoring UX exists.
    /// </summary>
    public static SunfishOverlay Empty { get; } = new(
        Fields: new Dictionary<string, FieldOverlay>(),
        Sections: Array.Empty<FormSection>(),
        Rules: Array.Empty<RuleDefinition>());
}
