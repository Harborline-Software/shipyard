namespace Sunfish.Foundation.Agreements;

/// <summary>
/// The canonical four-stage lifecycle of an <see cref="IAgreement"/>, shared
/// across every vertical that implements the agreement substrate (leases,
/// brand deals, license agreements, …) per ADR 0098.
/// </summary>
/// <remarks>
/// This is a deliberately minimal substrate enum. Per-vertical refinements
/// (e.g. a lease's <c>SignedButNotYetCommenced</c>, or a brand deal's
/// <c>Negotiating</c> sub-state of <see cref="PendingSignature"/>) are modeled
/// as vertical-block sub-states that map onto one of these canonical values —
/// they do NOT alter this enum. Keeping the substrate enum stable preserves
/// cross-vertical reporting and the <see cref="IAgreement.Status"/> contract.
/// Numeric values are pinned (Draft = 0 …) so that persisted ordinals remain
/// stable across releases; do not reorder.
/// </remarks>
public enum AgreementStatus
{
    /// <summary>Authored but not yet circulated for signature. The initial state.</summary>
    Draft = 0,

    /// <summary>Circulated to the parties and awaiting the signatures required to activate.</summary>
    PendingSignature = 1,

    /// <summary>Fully executed and in force. Corresponds to <see cref="IAgreement.ActivatedAt"/> being set.</summary>
    Active = 2,

    /// <summary>Ended — whether by expiry, cancellation, or mutual termination. Corresponds to <see cref="IAgreement.TerminatedAt"/> being set.</summary>
    Terminated = 3,
}
