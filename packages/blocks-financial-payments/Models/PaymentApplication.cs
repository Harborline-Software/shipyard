using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialPayments.Models;

/// <summary>
/// The many-to-many link between a <see cref="Payment"/> and its target
/// Invoice or Bill. One <see cref="Payment"/> may have multiple applications
/// (partial payments against separate line-items); one Invoice/Bill may be
/// reduced by multiple payments.
///
/// <para>
/// <b>Direction-matching invariant (§3.10 validation rule 1):</b>
/// <list type="bullet">
///   <item><see cref="PaymentDirection.Inbound"/> payment → <see cref="AppliedTo.Invoice"/> only.</item>
///   <item><see cref="PaymentDirection.Outbound"/> payment → <see cref="AppliedTo.Bill"/> only.</item>
/// </list>
/// This invariant is enforced by <c>IPaymentApplicationService.ApplyAsync</c> (PR 3)
/// and verified in the test suite.
/// </para>
///
/// <para>
/// <b>Amounts:</b> <see cref="AmountApplied"/> + <see cref="DiscountAmount"/> +
/// <see cref="WriteoffAmount"/> must be &lt;= the target's balance at time of
/// application. The in-memory repository does NOT enforce this — enforcement
/// lives in <c>DefaultPaymentApplicationService</c> (PR 3).
/// </para>
/// </summary>
public sealed record PaymentApplication
{
    /// <summary>Stable identifier.</summary>
    public required PaymentApplicationId Id { get; init; }

    /// <summary>The payment being applied.</summary>
    public required PaymentId PaymentId { get; init; }

    /// <summary>Discriminator: which document type <see cref="TargetId"/> refers to.</summary>
    public required AppliedTo AppliedTo { get; init; }

    /// <summary>
    /// The <c>InvoiceId</c> or <c>BillId</c> string value being reduced.
    /// String union per spec §3.10 — the concrete type is discriminated by <see cref="AppliedTo"/>.
    /// </summary>
    public required string TargetId { get; init; }

    /// <summary>Portion of the payment's gross amount credited to the target balance.</summary>
    public required decimal AmountApplied { get; init; }

    /// <summary>Early-pay discount granted; zero if none. GL: Discount Allowed expense line (§6.1).</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Short-pay write-off; zero if none. GL: Bad Debt expense line (§6.1).</summary>
    public decimal WriteoffAmount { get; init; }

    /// <summary>Date the funds were applied (may differ from <c>Payment.PaymentDate</c> for back-dated corrections).</summary>
    public required DateOnly AppliedDate { get; init; }

    // ── Audit ──
    public required Instant CreatedAtUtc { get; init; }

    /// <summary>Construct a new application record.</summary>
    public static PaymentApplication Create(
        PaymentId paymentId,
        AppliedTo appliedTo,
        string targetId,
        decimal amountApplied,
        DateOnly appliedDate,
        decimal discountAmount = 0m,
        decimal writeoffAmount = 0m,
        PaymentApplicationId? id = null,
        Instant? createdAtUtc = null)
    {
        return new PaymentApplication
        {
            Id = id ?? PaymentApplicationId.NewId(),
            PaymentId = paymentId,
            AppliedTo = appliedTo,
            TargetId = targetId,
            AmountApplied = amountApplied,
            DiscountAmount = discountAmount,
            WriteoffAmount = writeoffAmount,
            AppliedDate = appliedDate,
            CreatedAtUtc = createdAtUtc ?? Instant.Now,
        };
    }
}
