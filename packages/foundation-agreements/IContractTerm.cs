namespace Sunfish.Foundation.Agreements;

/// <summary>
/// A single term (clause / line item) of an <see cref="IAgreement"/> — the
/// substrate-level abstraction over the heterogeneous term shapes a vertical's
/// agreement carries (rent schedule, deliverable, royalty split, …) per ADR 0098.
/// </summary>
/// <remarks>
/// Vertical blocks implement <see cref="IContractTerm"/> on their own term
/// types. <see cref="TermType"/> is a vertical-defined marker string that lets
/// a consumer discriminate among a vertical's term kinds without the substrate
/// having to model each shape. See <see cref="IAgreement.Terms"/> for the
/// ordering convention.
/// </remarks>
public interface IContractTerm
{
    /// <summary>Stable identifier for this term within its vertical's store.</summary>
    string TermId { get; }

    /// <summary>
    /// Vertical-defined marker discriminating the kind of term (e.g.
    /// <c>rent-schedule</c>, <c>deliverable</c>, <c>royalty-split</c>). The
    /// substrate does not enumerate term kinds; each vertical owns its marker set.
    /// </summary>
    string TermType { get; }

    /// <summary>Human-readable description of the term's content.</summary>
    string Description { get; }
}
