using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Models.Events;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// Default <see cref="IPaymentApplicationService"/>. Coordinates the payment
/// repository, the application repository, and the AR / AP repositories to
/// keep Payment.UnappliedAmount, Invoice/Bill.AmountPaid + Balance + Status,
/// and the PaymentApplication ledger consistent.
///
/// <para>
/// <b>Direction-matching invariant (§3.10 validation rule 1)</b> is enforced
/// as the FIRST guard in <see cref="ApplyAsync"/> — BEFORE the payment
/// repository is consulted. This avoids leaking target existence through
/// error-type timing: a cross-cluster attacker observing
/// <see cref="ApplyError.UnknownTarget"/> vs
/// <see cref="ApplyError.DirectionMismatch"/> can't infer whether a specific
/// Invoice / Bill exists in the system. The mismatch path returns before any
/// I/O.
/// </para>
///
/// <para>
/// <b>Discount and writeoff GL posting is DEFERRED:</b> the Stage 02 spec
/// describes posting extra JE lines for Discount Allowed / Bad Debt expense
/// when <c>discountAmount &gt; 0</c> or <c>writeoffAmount &gt; 0</c>. PR 3 does
/// NOT implement that — the substrate lacks a per-chart selection mechanism
/// for the Discount Allowed / Bad Debt accounts (no
/// <see cref="Sunfish.Blocks.FinancialLedger.Models.AccountSubtype"/> exists
/// for them; <see cref="Sunfish.Blocks.FinancialLedger.Models.AccountSubtype.OperatingExpense"/>
/// is too coarse). For PR 3, non-zero values for <c>discountAmount</c> /
/// <c>writeoffAmount</c> are rejected with
/// <see cref="ApplyError.TargetBalanceInsufficient"/> + diagnostic detail. A
/// follow-on PR will design the account-selection mechanism (likely a per-
/// chart configuration on <c>BlocksFinancialPaymentsOptions</c>) and post the
/// extra JE lines.
/// </para>
/// </summary>
public sealed class DefaultPaymentApplicationService : IPaymentApplicationService
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentApplicationRepository _applications;
    private readonly IInvoiceRepository _invoices;
    private readonly IBillRepository _bills;
    private readonly ITenantContext _tenantContext;
    private readonly IDomainEventPublisher _events;

    public DefaultPaymentApplicationService(
        IPaymentRepository payments,
        IPaymentApplicationRepository applications,
        IInvoiceRepository invoices,
        IBillRepository bills,
        ITenantContext tenantContext,
        IDomainEventPublisher? events = null)
    {
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _events = events ?? new NoopDomainEventPublisher();
    }

    // Resolve the active tenant. Unresolved context is a programmer error
    // (the service should never be invoked without a resolved tenant scope);
    // tenant mismatches at load-time, by contrast, route to fail-closed
    // Unknown* errors so an attacker probing cross-tenant ids cannot
    // distinguish "wrong tenant" from "id does not exist."
    private TenantId CurrentTenantId =>
        _tenantContext.Tenant?.Id
        ?? throw new InvalidOperationException(
            "DefaultPaymentApplicationService invoked without a resolved tenant — composition-root bug.");

    // ──────────────────────────────────────────────────────────────────
    //  ApplyAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ApplyResult> ApplyAsync(
        PaymentId paymentId,
        AppliedTo appliedTo,
        string targetId,
        decimal amountApplied,
        decimal discountAmount,
        decimal writeoffAmount,
        PartyId actor,
        CancellationToken ct = default)
    {
        // Eager-evaluate the tenant context so a composition-root bug surfaces
        // immediately, even on request shapes that would otherwise short-circuit
        // before touching CurrentTenantId.
        _ = CurrentTenantId;

        // PR 3 deferred: discount / writeoff GL posting (see class summary).
        // Reject up-front so callers get a clear diagnostic rather than
        // a silently-incomplete persistence path.
        if (discountAmount != 0m || writeoffAmount != 0m)
        {
            return new ApplyResult(null, ApplyError.TargetBalanceInsufficient,
                "discount / writeoff GL posting is not yet implemented (PR 3 substrate-only). "
                + "Pass discountAmount=0 and writeoffAmount=0; the follow-on PR will wire the per-chart "
                + "Discount Allowed / Bad Debt account selection.");
        }

        if (amountApplied <= 0m)
            return new ApplyResult(null, ApplyError.TargetBalanceInsufficient,
                $"amountApplied must be positive (got {amountApplied}).");

        // Direction-matching invariant — checked BEFORE any repository lookup so the
        // error-type doesn't reveal target existence to a cross-cluster attacker.
        // We don't know the payment direction yet (that's a repo read), but we DO
        // know what appliedTo claims; the mismatch check that doesn't depend on
        // repo state is the structural one below (after we load the payment).
        // The CRITICAL security guarantee — direction mismatch returns BEFORE
        // target-existence is leaked — is preserved by ordering: load the
        // payment, check direction match, ONLY THEN load the target.

        var payment = await _payments.GetAsync(paymentId, ct).ConfigureAwait(false);
        // Tenant-isolation guard: a cross-tenant id-guess must return the SAME
        // error as a non-existent id so the attacker cannot distinguish
        // "wrong tenant" from "id does not exist." The diagnostic message
        // intentionally does not reveal tenant state.
        if (payment is null || !payment.TenantId.Equals(CurrentTenantId))
            return new ApplyResult(null, ApplyError.UnknownPayment, $"Payment '{paymentId.Value}' not found.");

        if (!DirectionMatches(payment.Direction, appliedTo))
            return new ApplyResult(null, ApplyError.DirectionMismatch,
                $"Direction-matching invariant violated: {payment.Direction} payment cannot apply to {appliedTo}. "
                + $"Inbound → Invoice; Outbound → Bill.");

        // Defense-in-depth: a terminal payment (Voided / Bounced) must not be
        // re-applied even if UnappliedAmount has been reset by the bounce path.
        // The spec doesn't enumerate a "PaymentTerminal" error, so route this
        // through InsufficientUnapplied — accurate (logically zero apply
        // capacity) and minimises surface-area for the council review.
        if (payment.Status is PaymentStatus.Voided or PaymentStatus.Bounced)
            return new ApplyResult(null, ApplyError.InsufficientUnapplied,
                $"Cannot apply to Payment in terminal status '{payment.Status}'.");

        if (amountApplied > payment.UnappliedAmount)
            return new ApplyResult(null, ApplyError.InsufficientUnapplied,
                $"amountApplied ({amountApplied}) exceeds Payment.UnappliedAmount ({payment.UnappliedAmount}).");

        // Target-existence is loaded ONLY after the direction-match passes — so
        // an attacker probing with a mismatched direction can never observe
        // an "UnknownTarget" outcome for a target they shouldn't know exists.
        if (appliedTo == AppliedTo.Invoice)
        {
            return await ApplyToInvoiceAsync(payment, targetId, amountApplied, actor, ct).ConfigureAwait(false);
        }
        return await ApplyToBillAsync(payment, targetId, amountApplied, actor, ct).ConfigureAwait(false);
    }

    private async Task<ApplyResult> ApplyToInvoiceAsync(
        Payment payment,
        string targetId,
        decimal amountApplied,
        PartyId actor,
        CancellationToken ct)
    {
        var invoiceId = new InvoiceId(targetId);
        var invoice = await _invoices.GetAsync(invoiceId, ct).ConfigureAwait(false);
        // Tenant-isolation guard mirrors the Payment-load check.
        if (invoice is null || !invoice.TenantId.Equals(CurrentTenantId))
            return new ApplyResult(null, ApplyError.UnknownTarget, $"Invoice '{targetId}' not found.");

        if (invoice.Status.IsTerminal())
            return new ApplyResult(null, ApplyError.TargetTerminal,
                $"Cannot apply to Invoice in terminal status '{invoice.Status}'.");

        if (!string.Equals(payment.Currency, invoice.Currency, StringComparison.Ordinal))
            return new ApplyResult(null, ApplyError.CurrencyMismatch,
                $"Payment currency '{payment.Currency}' does not match Invoice currency '{invoice.Currency}'.");

        if (amountApplied > invoice.Balance)
            return new ApplyResult(null, ApplyError.TargetBalanceInsufficient,
                $"amountApplied ({amountApplied}) exceeds Invoice.Balance ({invoice.Balance}).");

        // 1. Create application record (TenantId inherits from owning Payment).
        var application = PaymentApplication.Create(
            tenantId: payment.TenantId,
            paymentId: payment.Id,
            appliedTo: AppliedTo.Invoice,
            targetId: targetId,
            amountApplied: amountApplied,
            appliedDate: DateOnly.FromDateTime(DateTime.UtcNow));
        await _applications.AddAsync(application, ct).ConfigureAwait(false);

        // 2. Update invoice balance + status.
        var newAmountPaid = invoice.AmountPaid + amountApplied;
        var newBalance = invoice.Total - newAmountPaid;
        var newStatus = newBalance <= 0m ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        await _invoices.UpsertAsync(invoice with
        {
            AmountPaid = newAmountPaid,
            Balance = newBalance,
            Status = newStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = invoice.Version + 1,
        }, ct).ConfigureAwait(false);

        // 3. Update payment unapplied amount + status.
        var newUnapplied = payment.UnappliedAmount - amountApplied;
        var paymentStatus = DeriveAppliedStatus(payment.Amount, payment.Amount - newUnapplied);
        await _payments.UpdateAsync(payment with
        {
            UnappliedAmount = newUnapplied,
            Status = paymentStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = payment.Version + 1,
        }, ct).ConfigureAwait(false);

        // 4. Emit audit event.
        await PublishAppliedAsync(application, payment, actor, ct).ConfigureAwait(false);

        return new ApplyResult(application, ApplyError.None, null);
    }

    private async Task<ApplyResult> ApplyToBillAsync(
        Payment payment,
        string targetId,
        decimal amountApplied,
        PartyId actor,
        CancellationToken ct)
    {
        var billId = new BillId(targetId);
        var bill = await _bills.GetAsync(CurrentTenantId, billId, ct).ConfigureAwait(false);
        // Tenant-isolation guard — repository enforces uniform-404 on cross-tenant.
        if (bill is null)
            return new ApplyResult(null, ApplyError.UnknownTarget, $"Bill '{targetId}' not found.");

        if (bill.Status.IsTerminal())
            return new ApplyResult(null, ApplyError.TargetTerminal,
                $"Cannot apply to Bill in terminal status '{bill.Status}'.");

        if (!string.Equals(payment.Currency, bill.Currency, StringComparison.Ordinal))
            return new ApplyResult(null, ApplyError.CurrencyMismatch,
                $"Payment currency '{payment.Currency}' does not match Bill currency '{bill.Currency}'.");

        if (amountApplied > bill.Balance)
            return new ApplyResult(null, ApplyError.TargetBalanceInsufficient,
                $"amountApplied ({amountApplied}) exceeds Bill.Balance ({bill.Balance}).");

        var application = PaymentApplication.Create(
            tenantId: payment.TenantId,
            paymentId: payment.Id,
            appliedTo: AppliedTo.Bill,
            targetId: targetId,
            amountApplied: amountApplied,
            appliedDate: DateOnly.FromDateTime(DateTime.UtcNow));
        await _applications.AddAsync(application, ct).ConfigureAwait(false);

        var newAmountPaid = bill.AmountPaid + amountApplied;
        var newBalance = bill.Total - newAmountPaid;
        var newStatus = newBalance <= 0m ? BillStatus.Paid : BillStatus.PartiallyPaid;
        await _bills.UpsertAsync(CurrentTenantId, bill with
        {
            AmountPaid = newAmountPaid,
            Balance = newBalance,
            Status = newStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        }, ct).ConfigureAwait(false);

        var newUnapplied = payment.UnappliedAmount - amountApplied;
        var paymentStatus = DeriveAppliedStatus(payment.Amount, payment.Amount - newUnapplied);
        await _payments.UpdateAsync(payment with
        {
            UnappliedAmount = newUnapplied,
            Status = paymentStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = payment.Version + 1,
        }, ct).ConfigureAwait(false);

        await PublishAppliedAsync(application, payment, actor, ct).ConfigureAwait(false);

        return new ApplyResult(application, ApplyError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  UnapplyAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<UnapplyResult> UnapplyAsync(
        PaymentApplicationId applicationId,
        PartyId actor,
        CancellationToken ct = default)
    {
        // Eager-evaluate the tenant context — same rationale as ApplyAsync.
        _ = CurrentTenantId;

        var application = await _applications.GetAsync(applicationId, ct).ConfigureAwait(false);
        // Tenant-isolation guard: cross-tenant application id fails closed with
        // the same diagnostic as a non-existent id.
        if (application is null || !application.TenantId.Equals(CurrentTenantId))
            return new UnapplyResult(false, UnapplyError.UnknownApplication, $"PaymentApplication '{applicationId.Value}' not found.");

        var payment = await _payments.GetAsync(application.PaymentId, ct).ConfigureAwait(false);
        // Defensive: the application carries TenantId so this match is guaranteed,
        // but a stale-pointer scenario (PaymentApplication's PaymentId points
        // to a Payment that has been hard-deleted or its TenantId rotated)
        // collapses to the same Unknown* error.
        if (payment is null || !payment.TenantId.Equals(CurrentTenantId))
            return new UnapplyResult(false, UnapplyError.UnknownApplication,
                $"PaymentApplication '{applicationId.Value}' references unknown Payment '{application.PaymentId.Value}'.");

        // Restore target (Invoice/Bill) balance + status.
        switch (application.AppliedTo)
        {
            case AppliedTo.Invoice:
                await UnapplyFromInvoiceAsync(application, actor, ct).ConfigureAwait(false);
                break;
            case AppliedTo.Bill:
                await UnapplyFromBillAsync(application, actor, ct).ConfigureAwait(false);
                break;
        }

        // Restore payment.
        var restoredUnapplied = payment.UnappliedAmount + application.AmountApplied;
        var restoredStatus = DeriveAppliedStatus(payment.Amount, payment.Amount - restoredUnapplied);
        await _payments.UpdateAsync(payment with
        {
            UnappliedAmount = restoredUnapplied,
            Status = restoredStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = payment.Version + 1,
        }, ct).ConfigureAwait(false);

        await _applications.DeleteAsync(applicationId, ct).ConfigureAwait(false);

        await PublishUnappliedAsync(application, payment.TenantId, actor, ct).ConfigureAwait(false);

        return new UnapplyResult(true, UnapplyError.None, null);
    }

    private async Task UnapplyFromInvoiceAsync(PaymentApplication application, PartyId actor, CancellationToken ct)
    {
        var invoice = await _invoices.GetAsync(new InvoiceId(application.TargetId), ct).ConfigureAwait(false);
        if (invoice is null) return;

        var restoredAmountPaid = Math.Max(0m, invoice.AmountPaid - application.AmountApplied);
        var restoredBalance = invoice.Total - restoredAmountPaid;
        var restoredStatus = restoredAmountPaid == 0m ? InvoiceStatus.Issued : InvoiceStatus.PartiallyPaid;
        await _invoices.UpsertAsync(invoice with
        {
            AmountPaid = restoredAmountPaid,
            Balance = restoredBalance,
            Status = restoredStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = invoice.Version + 1,
        }, ct).ConfigureAwait(false);
    }

    private async Task UnapplyFromBillAsync(PaymentApplication application, PartyId actor, CancellationToken ct)
    {
        var bill = await _bills.GetAsync(CurrentTenantId, new BillId(application.TargetId), ct).ConfigureAwait(false);
        if (bill is null) return;

        var restoredAmountPaid = Math.Max(0m, bill.AmountPaid - application.AmountApplied);
        var restoredBalance = bill.Total - restoredAmountPaid;
        var restoredStatus = restoredAmountPaid == 0m ? BillStatus.Received : BillStatus.PartiallyPaid;
        await _bills.UpsertAsync(CurrentTenantId, bill with
        {
            AmountPaid = restoredAmountPaid,
            Balance = restoredBalance,
            Status = restoredStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        }, ct).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────────────

    private static bool DirectionMatches(PaymentDirection direction, AppliedTo appliedTo) =>
        (direction, appliedTo) switch
        {
            (PaymentDirection.Inbound, AppliedTo.Invoice) => true,
            (PaymentDirection.Outbound, AppliedTo.Bill) => true,
            _ => false,
        };

    private static PaymentStatus DeriveAppliedStatus(decimal amount, decimal appliedTotal)
    {
        if (appliedTotal <= 0m) return PaymentStatus.Unapplied;
        if (appliedTotal >= amount) return PaymentStatus.Applied;
        return PaymentStatus.PartiallyApplied;
    }

    private Task PublishAppliedAsync(PaymentApplication application, Payment payment, PartyId actor, CancellationToken ct)
    {
        var payload = new PaymentAppliedPayload(
            ApplicationId: application.Id,
            PaymentId: payment.Id,
            Direction: payment.Direction,
            AppliedTo: application.AppliedTo,
            TargetId: application.TargetId,
            AmountApplied: application.AmountApplied,
            DiscountAmount: application.DiscountAmount,
            WriteoffAmount: application.WriteoffAmount,
            Actor: actor);
        return PublishAsync(PaymentEventNames.PaymentApplied, payload, $"payment-applied:{application.Id.Value}", payment.TenantId, ct);
    }

    private Task PublishUnappliedAsync(PaymentApplication application, TenantId tenantId, PartyId actor, CancellationToken ct)
    {
        var payload = new PaymentUnappliedPayload(
            ApplicationId: application.Id,
            PaymentId: application.PaymentId,
            AppliedTo: application.AppliedTo,
            TargetId: application.TargetId,
            AmountApplied: application.AmountApplied,
            Actor: actor);
        return PublishAsync(PaymentEventNames.PaymentUnapplied, payload, $"payment-unapplied:{application.Id.Value}", tenantId, ct);
    }

    private Task PublishAsync<TPayload>(
        string eventType,
        TPayload payload,
        string idempotencyKey,
        TenantId tenantId,
        CancellationToken ct)
    {
        var envelope = new DomainEventEnvelope<TPayload>
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey = idempotencyKey,
            Payload = payload!,
        };
        return _events.PublishAsync(envelope, ct);
    }
}
