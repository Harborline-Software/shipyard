using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// CRUD surface over the payments substrate. Persistence-backed implementations
/// (SQLite, Postgres) shadow this binding; <see cref="InMemoryPaymentRepository"/>
/// backs the v1 desktop path.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Insert a new payment. Throws if a payment with the same id already exists.</summary>
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Get a payment by id. Returns null when missing.</summary>
    Task<Payment?> GetAsync(PaymentId id, CancellationToken cancellationToken = default);

    /// <summary>Replace an existing payment record. Throws if the id is unknown.</summary>
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>List all payments in a chart, ordered by <c>PaymentDate</c> descending.</summary>
    Task<IReadOnlyList<Payment>> ListByChartAsync(ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>List all payments for a specific party (customer or vendor) in a chart.</summary>
    Task<IReadOnlyList<Payment>> ListByPartyAsync(ChartOfAccountsId chartId, PartyId partyId, CancellationToken cancellationToken = default);
}
