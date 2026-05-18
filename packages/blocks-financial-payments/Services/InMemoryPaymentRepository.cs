using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// In-memory <see cref="IPaymentRepository"/>. State lives in a
/// <c>ConcurrentDictionary</c> keyed by <see cref="PaymentId"/>; secondary
/// queries scan the values — acceptable for the in-memory v1 path with
/// O(payments-per-tenant) complexity. A SQLite-backed implementation lands in
/// the follow-on substrate hand-off and shadows this binding.
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<PaymentId, Payment> _payments = new();

    /// <inheritdoc />
    public Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment is null) throw new ArgumentNullException(nameof(payment));
        if (!_payments.TryAdd(payment.Id, payment))
            throw new InvalidOperationException($"Payment '{payment.Id.Value}' already exists.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Payment?> GetAsync(PaymentId id, CancellationToken cancellationToken = default)
    {
        _payments.TryGetValue(id, out var payment);
        return Task.FromResult<Payment?>(payment);
    }

    /// <inheritdoc />
    public Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment is null) throw new ArgumentNullException(nameof(payment));
        if (!_payments.ContainsKey(payment.Id))
            throw new InvalidOperationException($"Payment '{payment.Id.Value}' not found; cannot update.");
        _payments[payment.Id] = payment;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Payment>> ListByChartAsync(ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        var rows = _payments.Values
            .Where(p => p.ChartId == chartId)
            .OrderByDescending(p => p.PaymentDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Payment>> ListByPartyAsync(ChartOfAccountsId chartId, PartyId partyId, CancellationToken cancellationToken = default)
    {
        var rows = _payments.Values
            .Where(p => p.ChartId == chartId && p.PartyId == partyId)
            .OrderByDescending(p => p.PaymentDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(rows);
    }
}
