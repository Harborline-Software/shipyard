namespace Sunfish.Foundation.Forms.Models;

/// <summary>
/// Tier of a rule's expression language (ADR 0055 §"Three-tier rules").
/// </summary>
/// <remarks>
/// Tier-3 (<see cref="PowerFx"/>) is RESERVED on the keystone but deferred
/// to v2 per ADR 0055 §"Three-tier rules" — the keystone accepts the enum
/// value so authored definitions can declare future intent without forcing
/// a schema migration when v2 lands; the rule-engine evaluator (separate
/// package) is what actually rejects Tier-3 expressions in v1.
/// </remarks>
public enum RuleTier
{
    /// <summary>JSON Schema 2020-12 <c>if/then/else</c> — used for visibility,
    /// required, readonly conditionals (Tier 1).</summary>
    JsonSchema = 0,

    /// <summary>JsonLogic with Sunfish custom operators — used for cross-field
    /// validation (Tier 2).</summary>
    JsonLogic = 1,

    /// <summary>Microsoft Power Fx (open-sourced reference impl) — RESERVED
    /// for computed fields in v2; v1 evaluator rejects.</summary>
    PowerFx = 2,
}

/// <summary>
/// Scope at which a rule evaluates (ADR 0055 §"Three-tier rules").
/// </summary>
public enum RuleScope
{
    /// <summary>Rule attaches to a single field; the field is the evaluation root.</summary>
    Field = 0,

    /// <summary>Rule attaches to a section; section visibility / required-state
    /// changes apply to all fields in the section.</summary>
    Section = 1,

    /// <summary>Rule attaches to the whole schema instance; used for cross-section
    /// invariants ("if status is ARCHIVED, all sections become read-only").</summary>
    Schema = 2,
}

/// <summary>
/// What a rule does when it evaluates to true (ADR 0055 §"Three-tier rules").
/// </summary>
public enum RuleActionKind
{
    /// <summary>Toggles the visibility of the rule's scope.</summary>
    Visibility = 0,

    /// <summary>Toggles the required state of the rule's scope.</summary>
    Required = 1,

    /// <summary>Toggles the read-only state of the rule's scope.</summary>
    ReadOnly = 2,

    /// <summary>Raises a validation error if the rule evaluates false at save time.</summary>
    Validate = 3,

    /// <summary>Computes the value of the rule's scope from the expression. Tier-3
    /// only in v1; the v1 evaluator rejects Compute actions on non-Power-Fx tiers
    /// to avoid accidental coupling of Tier-1/2 rules to mutation semantics.</summary>
    Compute = 4,
}
