namespace Sunfish.Foundation.Agreements;

/// <summary>
/// A counterparty to an <see cref="IAgreement"/> — the substrate-level
/// abstraction over the role-bearing entities a vertical's agreement binds
/// (lessor/lessee, brand/creator, licensor/licensee, …) per ADR 0098.
/// </summary>
/// <remarks>
/// Vertical blocks implement <see cref="IParty"/> on their own party
/// aggregates. <see cref="Role"/> uses vertical-defined role-marker strings;
/// the canonical markers are <c>lessor</c> / <c>lessee</c> / <c>brand</c> /
/// <c>creator</c> / <c>licensor</c> / <c>licensee</c> (a vertical may add its
/// own). See <see cref="IAgreement.Parties"/> for the ordering convention.
/// </remarks>
public interface IParty
{
    /// <summary>Stable identifier for this party within its vertical's store.</summary>
    string PartyId { get; }

    /// <summary>
    /// Vertical-defined role marker (e.g. <c>lessor</c>, <c>lessee</c>,
    /// <c>brand</c>, <c>creator</c>, <c>licensor</c>, <c>licensee</c>).
    /// </summary>
    string Role { get; }

    /// <summary>
    /// Human-readable display name for the party.
    /// </summary>
    /// <remarks>
    /// <strong>PII discipline (ADR 0098 §S1 substrate-tier minimum-floor).</strong>
    /// <see cref="DisplayName"/> is potentially personally-identifying. Vertical
    /// adopters MUST: (1) treat it as PII in audit-log emission (redact or hash
    /// in audit payloads rather than emitting verbatim); (2) expose a
    /// tier-redacted projection for non-privileged read surfaces; (3) keep it out
    /// of info-level logs. The substrate declares the floor; enforcement lives in
    /// each vertical's projection + audit + logging code.
    /// </remarks>
    string DisplayName { get; }
}
