using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Outcomes;

namespace Sunfish.Blocks.FinancialPayments.Migration;

/// <summary>
/// Default <see cref="IErpnextPaymentImporter"/>. Maps an ERPNext
/// <c>Payment Entry</c> onto a canonical <see cref="Payment"/> and upserts it
/// via the tenant-scoped <see cref="IPaymentRepository"/>. Idempotent on
/// <c>ExternalRef == "erpnext:pe:{name}"</c> with a <see cref="Payment.ExternalRefVersion"/>
/// version gate (ADR 0100 C1 / C7). Returns the canonical
/// <c>Sunfish.Foundation.Import</c> <see cref="ImportOutcome{T}"/> DU
/// (ADR 0100 C2 / OQ-A): every reject is a structured <see cref="ImportFailure"/>
/// arm rather than a thrown exception or a record-less skip.
/// </summary>
public sealed class ErpnextPaymentImporter : IErpnextPaymentImporter
{
    /// <summary>The ERPNext DocType this importer consumes — for census + reject provenance.</summary>
    public const string DocType = "Payment Entry";

    /// <summary>External-ref namespace prefix; mirrors the AP <c>erpnext:pinv:</c> convention.</summary>
    public const string ExternalRefPrefix = "erpnext:pe:";

    private readonly IPaymentRepository _payments;

    public ErpnextPaymentImporter(IPaymentRepository payments)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
    }

    /// <inheritdoc />
    public async Task<ImportOutcome<Payment>> UpsertPaymentAsync(
        TenantId tenantId,
        ErpnextPaymentSource source,
        ChartOfAccountsId chartId,
        PartyId partyPartyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var externalRef = ExternalRefPrefix + source.Name;

        // ── Validation gates (reject, never throw — ADR 0100 C2/C5) ──

        if (!TryMapDirection(source.PaymentType, out var direction))
        {
            return Reject(
                source.Name,
                ImportRejectReason.InvalidFieldValue,
                fieldName: "payment_type",
                ruleViolated: "payment_type must be 'Receive' (Inbound) or 'Pay' (Outbound)");
        }

        // Non-USD currency is deferred to v2 (ADR 0100 §10.2). A null/blank
        // currency is treated as the USD default (matches Payment.Create).
        var currency = string.IsNullOrWhiteSpace(source.Currency) ? "USD" : source.Currency.Trim();
        if (!string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return Reject(
                source.Name,
                ImportRejectReason.UnsupportedCurrency,
                fieldName: "currency",
                ruleViolated: "multi-currency import is deferred to v2; only USD is supported");
        }

        if (source.PaidAmount <= 0m)
        {
            return Reject(
                source.Name,
                ImportRejectReason.InvalidFieldValue,
                fieldName: "paid_amount",
                ruleViolated: "paid_amount must be greater than zero");
        }

        if (source.UnallocatedAmount < 0m || source.UnallocatedAmount > source.PaidAmount)
        {
            return Reject(
                source.Name,
                ImportRejectReason.ConstraintViolation,
                fieldName: "unallocated_amount",
                ruleViolated: "unallocated_amount must be within [0, paid_amount]");
        }

        // ── Idempotency / version gate (ADR 0100 C1/C7) ──

        var existing = await _payments
            .GetByExternalRefAsync(tenantId, chartId, externalRef, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Same or older source version → no write (count-stable re-run).
            if (string.CompareOrdinal(source.Modified, existing.ExternalRefVersion ?? string.Empty) <= 0)
            {
                return new ImportOutcome<Payment>.Skipped(
                    existing,
                    "already imported at the same or a newer source version");
            }

            // Strictly newer source version → update mutable fields in place,
            // preserving the operator's Notes (never overwritten by the importer).
            var updated = existing with
            {
                Direction = direction,
                PartyId = partyPartyId,
                PaymentDate = source.PostingDate,
                Amount = source.PaidAmount,
                UnappliedAmount = source.UnallocatedAmount,
                Method = MapMethod(source.ModeOfPayment),
                Currency = currency,
                Reference = source.ReferenceNo,
                ExternalRefVersion = source.Modified,
                UpdatedAtUtc = Instant.Now,
                Version = existing.Version + 1,
            };
            await _payments.UpdateAsync(tenantId, updated, cancellationToken).ConfigureAwait(false);
            return new ImportOutcome<Payment>.Updated(updated);
        }

        // ── Insert path ──

        var payment = Payment.Create(
            tenantId: tenantId,
            chartId: chartId,
            direction: direction,
            paymentNumber: source.Name,
            partyId: partyPartyId,
            paymentDate: source.PostingDate,
            amount: source.PaidAmount,
            method: MapMethod(source.ModeOfPayment),
            currency: currency,
            reference: source.ReferenceNo,
            externalRef: externalRef,
            externalRefVersion: source.Modified) with
        {
            // ERPNext Payment Entries are submitted/cleared documents; the
            // unallocated portion drives UnappliedAmount. We do NOT post a GL
            // entry here (clearing/posting is a downstream concern); the
            // imported payment lands as an unapplied cash record.
            UnappliedAmount = source.UnallocatedAmount,
        };

        await _payments.AddAsync(tenantId, payment, cancellationToken).ConfigureAwait(false);
        return new ImportOutcome<Payment>.Inserted(payment);
    }

    /// <summary>ERPNext <c>payment_type</c> → <see cref="PaymentDirection"/>.</summary>
    private static bool TryMapDirection(string paymentType, out PaymentDirection direction)
    {
        switch (paymentType)
        {
            case "Receive":
                direction = PaymentDirection.Inbound;
                return true;
            case "Pay":
                direction = PaymentDirection.Outbound;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    /// <summary>
    /// ERPNext <c>mode_of_payment</c> → <see cref="PaymentMethod"/>. ERPNext's
    /// mode-of-payment is a free-form master, so unknown values map to
    /// <see cref="PaymentMethod.Other"/> rather than rejecting the record (the
    /// payment is still valid cash movement — only the method classification is
    /// unknown).
    /// </summary>
    private static PaymentMethod MapMethod(string? modeOfPayment) =>
        (modeOfPayment?.Trim().ToLowerInvariant()) switch
        {
            "cash"           => PaymentMethod.Cash,
            "cheque"         => PaymentMethod.Check,
            "check"          => PaymentMethod.Check,
            "ach"            => PaymentMethod.ACH,
            "bank draft"     => PaymentMethod.ACH,
            "wire transfer"  => PaymentMethod.Wire,
            "wire"           => PaymentMethod.Wire,
            "credit card"    => PaymentMethod.Card,
            "debit card"     => PaymentMethod.Card,
            "card"           => PaymentMethod.Card,
            _                => PaymentMethod.Other,
        };

    /// <summary>
    /// Build a <see cref="ImportOutcome{T}.Rejected"/> from a canonical reason.
    /// Only allowlisted, safe identifiers cross the boundary (ADR 0100 C9):
    /// the opaque ERPNext name + field NAME + a rule descriptor — never a
    /// monetary amount, a party name/PII, or the raw source payload.
    /// </summary>
    private static ImportOutcome<Payment>.Rejected Reject(
        string sourceName,
        ImportRejectReason reason,
        string? fieldName = null,
        string? ruleViolated = null) =>
        new(ImportFailure.Of(
            externalRef: sourceName,
            docType: DocType,
            reason: reason,
            fieldName: fieldName,
            ruleViolated: ruleViolated));
}
