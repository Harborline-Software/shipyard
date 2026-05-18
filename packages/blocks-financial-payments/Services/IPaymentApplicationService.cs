using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialPayments.Services;

// ──────────────────────────────────────────────────────────────────────────────
// Result / error types
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Error discriminant for <see cref="IPaymentApplicationService.ApplyAsync"/>.</summary>
public enum ApplyError
{
    None = 0,
    UnknownPayment,
    UnknownTarget,

    /// <summary>
    /// Direction-matching invariant violated: Inbound → Bill or Outbound → Invoice.
    /// This MUST be checked BEFORE repository lookups to prevent timing attacks
    /// that could reveal target existence by observing error-type differences.
    /// </summary>
    DirectionMismatch,

    /// <summary><c>amountApplied &gt; Payment.UnappliedAmount</c>.</summary>
    InsufficientUnapplied,

    /// <summary><c>amountApplied + discountAmount + writeoffAmount &gt; target.Balance</c>.</summary>
    TargetBalanceInsufficient,

    /// <summary>Payment and target have different <c>Currency</c> values.</summary>
    CurrencyMismatch,

    /// <summary>Target Invoice/Bill is in a terminal state (Voided, WrittenOff, etc.).</summary>
    TargetTerminal,
}

/// <summary>Error discriminant for <see cref="IPaymentApplicationService.UnapplyAsync"/>.</summary>
public enum UnapplyError
{
    None = 0,
    UnknownApplication,
}

/// <summary>Result of <see cref="IPaymentApplicationService.ApplyAsync"/>.</summary>
public sealed record ApplyResult(PaymentApplication? Application, ApplyError Error, string? ErrorMessage);

/// <summary>Result of <see cref="IPaymentApplicationService.UnapplyAsync"/>.</summary>
public sealed record UnapplyResult(bool Success, UnapplyError Error, string? ErrorMessage);

// ──────────────────────────────────────────────────────────────────────────────
// Service interface (stub — implemented in PR 3; SECURITY SPOT-CHECK REQUIRED)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// High-level application service that applies or removes a
/// <see cref="PaymentApplication"/> and keeps all downstream balances
/// consistent.
///
/// <para>
/// <b>Declared here (PR 1) so downstream can reference the interface at compile
/// time.</b> <c>DefaultPaymentApplicationService</c> ships in PR 3 — which
/// requires a security spot-check before merging (direction-matching invariant
/// is a financial correctness gate).
/// </para>
///
/// <para>
/// <b>On success, ApplyAsync:</b>
/// <list type="number">
///   <item>Creates a <see cref="PaymentApplication"/> record.</item>
///   <item>Updates Invoice/Bill: <c>AmountPaid += amountApplied</c>; recomputes balance + status.</item>
///   <item>Updates Payment: <c>UnappliedAmount -= amountApplied</c>; recomputes status.</item>
///   <item>Emits <c>Financial.PaymentApplied</c> audit event.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>On success, UnapplyAsync:</b>
/// <list type="number">
///   <item>Deletes the <see cref="PaymentApplication"/> record.</item>
///   <item>Restores Invoice/Bill: <c>AmountPaid -= amountApplied</c>; recomputes balance + status.</item>
///   <item>Restores Payment: <c>UnappliedAmount += amountApplied</c>; recomputes status.</item>
///   <item>Emits <c>Financial.PaymentUnapplied</c> audit event.</item>
/// </list>
/// </para>
/// </summary>
public interface IPaymentApplicationService
{
    /// <summary>
    /// Apply <paramref name="amountApplied"/> of <paramref name="paymentId"/> to
    /// <paramref name="targetId"/> (Invoice or Bill, discriminated by <paramref name="appliedTo"/>).
    ///
    /// <para>Direction-matching is checked FIRST — before any repository lookup.</para>
    /// </summary>
    Task<ApplyResult> ApplyAsync(
        PaymentId paymentId,
        AppliedTo appliedTo,
        string targetId,
        decimal amountApplied,
        decimal discountAmount,
        decimal writeoffAmount,
        PartyId actor,
        CancellationToken ct = default);

    /// <summary>
    /// Remove a specific application (correction path). Restores balances on the
    /// Invoice/Bill and on the Payment.
    /// </summary>
    Task<UnapplyResult> UnapplyAsync(
        PaymentApplicationId applicationId,
        PartyId actor,
        CancellationToken ct = default);
}
