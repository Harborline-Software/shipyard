using Sunfish.Blocks.FinancialPayments.Models;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// CRUD surface over the payment-application substrate.
///
/// <para>
/// <b>Important:</b> This repository stores and retrieves
/// <see cref="PaymentApplication"/> rows only — it does NOT update
/// <see cref="Payment.UnappliedAmount"/>, Invoice balances, or Bill balances.
/// Balance mutation is the responsibility of <c>IPaymentApplicationService</c> (PR 3).
/// </para>
/// </summary>
public interface IPaymentApplicationRepository
{
    /// <summary>Insert a new application record.</summary>
    Task AddAsync(PaymentApplication application, CancellationToken cancellationToken = default);

    /// <summary>Get an application by id. Returns null when missing.</summary>
    Task<PaymentApplication?> GetAsync(PaymentApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an application record (unapply path). Returns false when the id
    /// is unknown (idempotent: double-delete is safe).
    /// </summary>
    Task<bool> DeleteAsync(PaymentApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>List all applications for a given payment.</summary>
    Task<IReadOnlyList<PaymentApplication>> ListByPaymentAsync(PaymentId paymentId, CancellationToken cancellationToken = default);

    /// <summary>List all applications targeting a given Invoice or Bill (identified by string id).</summary>
    Task<IReadOnlyList<PaymentApplication>> ListByTargetAsync(string targetId, CancellationToken cancellationToken = default);
}
