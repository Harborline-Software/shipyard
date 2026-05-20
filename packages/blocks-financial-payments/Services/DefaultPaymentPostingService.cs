using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// Default <see cref="IPaymentPostingService"/>. Coordinates the payment
/// repository, the application repository, the AR / AP repositories (for
/// bounce-path balance restoration), the chart's account resolver, and the
/// ledger posting service — mirrors AR's <c>InvoicePostingService</c>
/// and AP's <c>BillPostingService</c> shape.
///
/// <para>
/// <b>Control-account resolution:</b> per the Stage 02 spec, the clearing
/// journal entry posts against the chart's default AR control account
/// (Inbound) or default AP control account (Outbound). PR 2 resolves this by
/// enumerating the chart's accounts via <see cref="IAccountResolver"/> and
/// picking the first active postable account whose
/// <see cref="GLAccount.Subtype"/> matches
/// <see cref="AccountSubtype.AccountsReceivable"/> /
/// <see cref="AccountSubtype.AccountsPayable"/>. Charts with multiple AR or
/// AP control accounts (a Phase 2 multi-tenancy refinement) will need an
/// explicit selection mechanism — track via follow-on hand-off.
/// </para>
/// </summary>
public sealed class DefaultPaymentPostingService : IPaymentPostingService
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentApplicationRepository _applications;
    private readonly IInvoiceRepository _invoices;
    private readonly IBillRepository _bills;
    private readonly IAccountResolver _accountResolver;
    private readonly IJournalPostingService _journals;
    private readonly ITenantContext _tenantContext;

    public DefaultPaymentPostingService(
        ITenantContext tenantContext,
        IPaymentRepository payments,
        IPaymentApplicationRepository applications,
        IInvoiceRepository invoices,
        IBillRepository bills,
        IAccountResolver accountResolver,
        IJournalPostingService journals)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _applications = applications ?? throw new ArgumentNullException(nameof(applications));
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _accountResolver = accountResolver ?? throw new ArgumentNullException(nameof(accountResolver));
        _journals = journals ?? throw new ArgumentNullException(nameof(journals));
    }

    private TenantId CurrentTenantId =>
        _tenantContext.Tenant?.Id
            ?? throw new InvalidOperationException("DefaultPaymentPostingService requires a resolved tenant on the ambient ITenantContext.");

    // ──────────────────────────────────────────────────────────────────
    //  ClearAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ClearResult> ClearAsync(PaymentId id, PartyId actor, CancellationToken ct = default)
    {
        var payment = await _payments.GetAsync(id, ct).ConfigureAwait(false);
        if (payment is null)
            return new ClearResult(null, null, ClearError.UnknownPayment, $"Payment '{id.Value}' not found.");

        // Idempotent: any non-Draft status that already has a clearing JE returns success.
        if (payment.Status != PaymentStatus.Draft && payment.JournalEntryId is { } existingEntry)
            return new ClearResult(payment, existingEntry, ClearError.None, "Already cleared; no-op.");

        if (payment.Status != PaymentStatus.Draft)
            return new ClearResult(payment, null, ClearError.InvalidStatusForClear,
                $"Cannot clear payment in status '{payment.Status}' — only Draft is clearable.");

        if (payment.BankAccountId is not { } bankAccountId)
            return new ClearResult(payment, null, ClearError.JournalRejected,
                "Payment has no BankAccountId; cannot post clearing journal entry.");

        var controlAccount = await ResolveControlAccountAsync(payment.ChartId, payment.Direction, ct).ConfigureAwait(false);
        if (controlAccount is null)
            return new ClearResult(payment, null, ClearError.JournalRejected,
                $"Chart '{payment.ChartId.Value}' has no active {(payment.Direction == PaymentDirection.Inbound ? "AccountsReceivable" : "AccountsPayable")} control account configured.");

        var jeLines = BuildClearLines(payment.Direction, bankAccountId, controlAccount.Id, payment.Amount);
        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: payment.PaymentDate,
            memo: $"Clear payment {payment.PaymentNumber}",
            lines: jeLines,
            createdAtUtc: Instant.Now,
            sourceReference: $"payment-clear:{payment.Id.Value}");

        var postResult = await _journals.PostAsync(entry, ct).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new ClearResult(payment, null, ClearError.JournalRejected, postResult.Detail);

        // Status after clearing depends on how much has been applied. In the
        // standard flow Applications is empty at clear time so we land on
        // Unapplied; the PartiallyApplied / Applied branches are defensive
        // for callers that pre-stage applications before clearing.
        var appliedTotal = payment.Applications.Sum(a => a.AmountApplied);
        var newStatus = DeriveClearedStatus(payment.Amount, appliedTotal);

        var cleared = payment with
        {
            Status = newStatus,
            JournalEntryId = entry.Id,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = payment.Version + 1,
        };
        await _payments.UpdateAsync(cleared, ct).ConfigureAwait(false);

        return new ClearResult(cleared, entry.Id, ClearError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  BounceAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<BounceResult> BounceAsync(PaymentId id, string reason, PartyId actor, CancellationToken ct = default)
    {
        var payment = await _payments.GetAsync(id, ct).ConfigureAwait(false);
        if (payment is null)
            return new BounceResult(null, null, BounceError.UnknownPayment, $"Payment '{id.Value}' not found.");

        if (payment.Status is not (PaymentStatus.Unapplied or PaymentStatus.PartiallyApplied or PaymentStatus.Applied))
            return new BounceResult(payment, null, BounceError.InvalidStatusForBounce,
                $"Cannot bounce payment in status '{payment.Status}'. Only Unapplied / PartiallyApplied / Applied are bounceable.");

        if (payment.JournalEntryId is null)
            return new BounceResult(payment, null, BounceError.InvalidStatusForBounce,
                "Payment has no clearing journal entry to reverse.");

        if (payment.BankAccountId is not { } bankAccountId)
            return new BounceResult(payment, null, BounceError.JournalRejected,
                "Payment has no BankAccountId; cannot post reversal journal entry.");

        var controlAccount = await ResolveControlAccountAsync(payment.ChartId, payment.Direction, ct).ConfigureAwait(false);
        if (controlAccount is null)
            return new BounceResult(payment, null, BounceError.JournalRejected,
                $"Chart '{payment.ChartId.Value}' has no active {(payment.Direction == PaymentDirection.Inbound ? "AccountsReceivable" : "AccountsPayable")} control account configured.");

        var reversalLines = BuildReversalLines(payment.Direction, bankAccountId, controlAccount.Id, payment.Amount);
        var reversal = new JournalEntry(
            id: JournalEntryId.NewId(),
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            memo: $"Bounce payment {payment.PaymentNumber}: {reason}",
            lines: reversalLines,
            createdAtUtc: Instant.Now,
            sourceReference: $"payment-bounce:{payment.Id.Value}");

        var postResult = await _journals.PostAsync(reversal, ct).ConfigureAwait(false);
        if (!postResult.IsSuccess)
            return new BounceResult(payment, null, BounceError.JournalRejected, postResult.Detail);

        // For each prior application: restore the Invoice/Bill balance and delete the application row.
        // This is the only place in the cluster where the posting service touches Invoice/Bill
        // repositories directly — intentional per the Stage 02 spec so the bounce is atomic from
        // the caller's perspective.
        var priorApplications = await _applications.ListByPaymentAsync(payment.Id, ct).ConfigureAwait(false);
        foreach (var application in priorApplications)
        {
            switch (application.AppliedTo)
            {
                case AppliedTo.Invoice:
                    await RestoreInvoiceBalanceAsync(application, actor, ct).ConfigureAwait(false);
                    break;
                case AppliedTo.Bill:
                    await RestoreBillBalanceAsync(application, actor, ct).ConfigureAwait(false);
                    break;
            }

            await _applications.DeleteAsync(application.Id, ct).ConfigureAwait(false);
        }

        var bounced = payment with
        {
            Status = PaymentStatus.Bounced,
            BouncedByEntryId = reversal.Id,
            UnappliedAmount = payment.Amount,
            Applications = [],
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = payment.Version + 1,
        };
        await _payments.UpdateAsync(bounced, ct).ConfigureAwait(false);

        return new BounceResult(bounced, reversal.Id, BounceError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  VoidAsync
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VoidResult> VoidAsync(PaymentId id, string reason, PartyId actor, CancellationToken ct = default)
    {
        var payment = await _payments.GetAsync(id, ct).ConfigureAwait(false);
        if (payment is null)
            return new VoidResult(null, VoidError.UnknownPayment, $"Payment '{id.Value}' not found.");

        if (payment.Status != PaymentStatus.Draft)
            return new VoidResult(payment, VoidError.InvalidStatusForVoid,
                $"Cannot void payment in status '{payment.Status}' — only Draft is voidable.");

        var voided = payment with
        {
            Status = PaymentStatus.Voided,
            Notes = string.IsNullOrWhiteSpace(reason) ? payment.Notes : $"Voided: {reason}",
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = payment.Version + 1,
        };
        await _payments.UpdateAsync(voided, ct).ConfigureAwait(false);

        return new VoidResult(voided, VoidError.None, null);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals — JE construction
    // ──────────────────────────────────────────────────────────────────

    private static IReadOnlyList<JournalEntryLine> BuildClearLines(
        PaymentDirection direction,
        GLAccountId bankAccountId,
        GLAccountId controlAccountId,
        decimal amount) => direction switch
        {
            // Inbound: customer pays us → Dr Bank / Cr AR control.
            PaymentDirection.Inbound =>
            [
                new JournalEntryLine(bankAccountId, debit: amount, credit: 0m),
                new JournalEntryLine(controlAccountId, debit: 0m, credit: amount),
            ],
            // Outbound: we pay vendor → Dr AP control / Cr Bank.
            PaymentDirection.Outbound =>
            [
                new JournalEntryLine(controlAccountId, debit: amount, credit: 0m),
                new JournalEntryLine(bankAccountId, debit: 0m, credit: amount),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown payment direction."),
        };

    private static IReadOnlyList<JournalEntryLine> BuildReversalLines(
        PaymentDirection direction,
        GLAccountId bankAccountId,
        GLAccountId controlAccountId,
        decimal amount) => direction switch
        {
            // Reversal of Inbound clearing: Dr AR control / Cr Bank.
            PaymentDirection.Inbound =>
            [
                new JournalEntryLine(controlAccountId, debit: amount, credit: 0m),
                new JournalEntryLine(bankAccountId, debit: 0m, credit: amount),
            ],
            // Reversal of Outbound clearing: Dr Bank / Cr AP control.
            PaymentDirection.Outbound =>
            [
                new JournalEntryLine(bankAccountId, debit: amount, credit: 0m),
                new JournalEntryLine(controlAccountId, debit: 0m, credit: amount),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown payment direction."),
        };

    // ──────────────────────────────────────────────────────────────────
    //  Internals — account resolution
    // ──────────────────────────────────────────────────────────────────

    private async Task<GLAccount?> ResolveControlAccountAsync(
        ChartOfAccountsId chartId,
        PaymentDirection direction,
        CancellationToken ct)
    {
        var subtype = direction switch
        {
            PaymentDirection.Inbound => AccountSubtype.AccountsReceivable,
            PaymentDirection.Outbound => AccountSubtype.AccountsPayable,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown payment direction."),
        };

        var accounts = await _accountResolver.EnumerateForChartAsync(chartId, includeInactive: false, ct).ConfigureAwait(false);
        return accounts.FirstOrDefault(a => a.Subtype == subtype && a.IsPostable);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals — Invoice / Bill balance restoration on bounce
    // ──────────────────────────────────────────────────────────────────

    private async Task RestoreInvoiceBalanceAsync(PaymentApplication application, PartyId actor, CancellationToken ct)
    {
        var invoice = await _invoices.GetAsync(new InvoiceId(application.TargetId), ct).ConfigureAwait(false);
        if (invoice is null) return;

        var restoredAmountPaid = Math.Max(0m, invoice.AmountPaid - application.AmountApplied);
        var restoredBalance = invoice.Total - restoredAmountPaid;
        var restoredStatus = restoredAmountPaid == 0m ? InvoiceStatus.Issued : InvoiceStatus.PartiallyPaid;

        var updated = invoice with
        {
            AmountPaid = restoredAmountPaid,
            Balance = restoredBalance,
            Status = restoredStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = invoice.Version + 1,
        };
        await _invoices.UpsertAsync(updated, ct).ConfigureAwait(false);
    }

    private async Task RestoreBillBalanceAsync(PaymentApplication application, PartyId actor, CancellationToken ct)
    {
        var bill = await _bills.GetAsync(CurrentTenantId, new BillId(application.TargetId), ct).ConfigureAwait(false);
        if (bill is null) return;

        var restoredAmountPaid = Math.Max(0m, bill.AmountPaid - application.AmountApplied);
        var restoredBalance = bill.Total - restoredAmountPaid;
        var restoredStatus = restoredAmountPaid == 0m ? BillStatus.Received : BillStatus.PartiallyPaid;

        var updated = bill with
        {
            AmountPaid = restoredAmountPaid,
            Balance = restoredBalance,
            Status = restoredStatus,
            UpdatedAtUtc = Instant.Now,
            UpdatedBy = actor,
            Version = bill.Version + 1,
        };
        await _bills.UpsertAsync(CurrentTenantId, updated, ct).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals — status derivation
    // ──────────────────────────────────────────────────────────────────

    private static PaymentStatus DeriveClearedStatus(decimal amount, decimal appliedTotal)
    {
        if (appliedTotal <= 0m) return PaymentStatus.Unapplied;
        if (appliedTotal >= amount) return PaymentStatus.Applied;
        return PaymentStatus.PartiallyApplied;
    }
}
