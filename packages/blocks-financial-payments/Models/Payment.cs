using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.FinancialPayments.Models;

/// <summary>
/// A cash-movement event — either money received from a customer (Inbound)
/// or money sent to a vendor (Outbound). Implements <see cref="IMustHaveTenant"/>
/// so persistence adapters enforce tenant isolation at the row level.
///
/// <para>
/// <b>UnappliedAmount is cached, not computed.</b> It is kept consistent by
/// <c>IPaymentApplicationService</c> (PR 3), which decrements it on apply and
/// increments it on unapply. The invariant
/// <c>UnappliedAmount == Amount - sum(Applications[].AmountApplied)</c>
/// is verified by the test suite.
/// </para>
///
/// <para>
/// <b>PaymentNumber</b> in PR 1 is a ULID-based string supplied by the caller.
/// Sequential per-chart numbering lands in a Phase 2 follow-up — same pattern
/// as <c>Invoice.InvoiceNumber</c> in the AR cluster.
/// </para>
/// </summary>
public sealed record Payment : IMustHaveTenant
{
    /// <summary>Stable identifier.</summary>
    public required PaymentId Id { get; init; }

    /// <summary>Tenant scope. Required — non-default per <see cref="IMustHaveTenant"/>.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The chart-of-accounts under which this payment posts.</summary>
    public required ChartOfAccountsId ChartId { get; init; }

    /// <summary>
    /// Cash direction: <see cref="PaymentDirection.Inbound"/> (customer pays us) or
    /// <see cref="PaymentDirection.Outbound"/> (we pay vendor).
    /// Controls which document types may be targeted in <see cref="PaymentApplication"/>.
    /// </summary>
    public required PaymentDirection Direction { get; init; }

    /// <summary>Human-readable payment number. PR 1 callers supply directly; Phase 2 mints sequentially.</summary>
    public required string PaymentNumber { get; init; }

    /// <summary>The Party (customer or vendor) associated with this payment.</summary>
    public required PartyId PartyId { get; init; }

    /// <summary>Optional bank account handle for reconciliation.</summary>
    public GLAccountId? BankAccountId { get; init; }

    /// <summary>Date the payment was received or issued.</summary>
    public required DateOnly PaymentDate { get; init; }

    /// <summary>Gross amount of the payment.</summary>
    public required decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code; defaults to USD.</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>How the payment was transacted.</summary>
    public required PaymentMethod Method { get; init; }

    /// <summary>External reference number (e.g., check number, ACH trace ID).</summary>
    public string? Reference { get; init; }

    /// <summary>Lifecycle state.</summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>
    /// Cached: how much of <see cref="Amount"/> has not yet been applied.
    /// Invariant: <c>UnappliedAmount == Amount - sum(Applications[].AmountApplied)</c>;
    /// always <c>&gt;= 0</c>.
    /// </summary>
    public required decimal UnappliedAmount { get; init; }

    /// <summary>Snapshot of applications at last load. Updated by <c>IPaymentApplicationService</c>.</summary>
    public IReadOnlyList<PaymentApplication> Applications { get; init; } = [];

    /// <summary>The GL journal entry from <c>IPaymentPostingService.ClearAsync</c>; null until cleared.</summary>
    public JournalEntryId? JournalEntryId { get; init; }

    /// <summary>The GL reversal journal entry from <c>IPaymentPostingService.BounceAsync</c>; non-null only when bounced.</summary>
    public JournalEntryId? BouncedByEntryId { get; init; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; init; }

    /// <summary>External system reference (e.g., ERPNext <c>PE-0001</c>) for migration / sync.</summary>
    public string? ExternalRef { get; init; }

    // ── CRDT envelope ──
    public required Instant CreatedAtUtc { get; init; }
    public PartyId? CreatedBy { get; init; }
    public Instant UpdatedAtUtc { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public required long Version { get; init; }

    /// <summary>
    /// Construct a new Draft payment. <see cref="UnappliedAmount"/> is initialised to
    /// <paramref name="amount"/> (nothing applied yet). Status is
    /// <see cref="PaymentStatus.Draft"/>.
    /// </summary>
    public static Payment Create(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PaymentDirection direction,
        string paymentNumber,
        PartyId partyId,
        DateOnly paymentDate,
        decimal amount,
        PaymentMethod method,
        PartyId? createdBy = null,
        PaymentId? id = null,
        GLAccountId? bankAccountId = null,
        string currency = "USD",
        string? reference = null,
        string? notes = null,
        string? externalRef = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new Payment
        {
            Id = id ?? PaymentId.NewId(),
            TenantId = tenantId,
            ChartId = chartId,
            Direction = direction,
            PaymentNumber = paymentNumber,
            PartyId = partyId,
            BankAccountId = bankAccountId,
            PaymentDate = paymentDate,
            Amount = amount,
            Currency = currency,
            Method = method,
            Reference = reference,
            Status = PaymentStatus.Draft,
            UnappliedAmount = amount,
            Notes = notes,
            ExternalRef = externalRef,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            Version = 1,
        };
    }
}
