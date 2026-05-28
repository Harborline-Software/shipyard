using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Agreements;

/// <summary>
/// The cross-vertical agreement substrate: the common shape every binding
/// counterparty arrangement shares, regardless of vertical (a lease, a brand
/// deal, a license agreement, …) per ADR 0098.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Multi-tenancy by composition (ADR 0098 §A1; ADR 0008 precedent).</strong>
/// <see cref="IAgreement"/> extends <see cref="IMustHaveTenant"/>; the
/// <c>TenantId</c> property is INHERITED from the marker chain
/// (<see cref="IMustHaveTenant"/> : <c>ITenantScoped</c>) rather than
/// redeclared here. This preserves the repo-wide
/// <c>WhereTenant&lt;T&gt;() where T : IMustHaveTenant</c> query-filter constraint
/// for any tenant-scoped agreement query (e.g. <c>query.WhereTenant&lt;Lease&gt;(tenantId)</c>
/// once <c>Lease : IAgreement</c>).
/// </para>
/// <para>
/// <strong>Cross-vertical reuse thesis.</strong> Vertical blocks implement
/// <see cref="IAgreement"/> on their aggregate (blocks-leases adopts it as the
/// exemplar post-MVP per ADR 0098 Halt 8 Option α; future blocks-brand-deals /
/// blocks-license-agreements follow). The substrate ships interfaces + the
/// <see cref="AgreementStatus"/> enum only — no concrete implementation, no DI
/// helper (Shape α per ADR 0098 §6 Step 1).
/// </para>
/// </remarks>
public interface IAgreement : IMustHaveTenant
{
    /// <summary>Stable identifier for this agreement within its vertical's store.</summary>
    string AgreementId { get; }

    /// <summary>
    /// The counterparties to the agreement, in deterministic order.
    /// Conventionally <c>Parties[0]</c> is the primary counterparty (lessor /
    /// brand / licensor). Exposed as <see cref="IReadOnlyList{T}"/> so positional
    /// access is part of the contract (ADR 0098 §A7).
    /// </summary>
    IReadOnlyList<IParty> Parties { get; }

    /// <summary>
    /// The agreement's terms, in deterministic vertical-defined sort order.
    /// Exposed as <see cref="IReadOnlyList{T}"/> so positional access is part of
    /// the contract (ADR 0098 §A7).
    /// </summary>
    IReadOnlyList<IContractTerm> Terms { get; }

    /// <summary>The current lifecycle stage of the agreement.</summary>
    AgreementStatus Status { get; }

    /// <summary>When the agreement record was created.</summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When the agreement transitioned to <see cref="AgreementStatus.Active"/>,
    /// or <see langword="null"/> while it is still Draft / PendingSignature.
    /// </summary>
    DateTimeOffset? ActivatedAt { get; }

    /// <summary>
    /// When the agreement transitioned to <see cref="AgreementStatus.Terminated"/>,
    /// or <see langword="null"/> while it has not been terminated.
    /// </summary>
    DateTimeOffset? TerminatedAt { get; }

    // TenantId is inherited from IMustHaveTenant : ITenantScoped (ADR 0098 §A1) —
    // intentionally NOT redeclared here.
}
