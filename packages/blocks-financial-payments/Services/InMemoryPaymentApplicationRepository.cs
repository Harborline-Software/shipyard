using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialPayments.Models;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// In-memory <see cref="IPaymentApplicationRepository"/>. Thread-safe via
/// <c>ConcurrentDictionary</c>.
///
/// <para>
/// <b>Balance updates are NOT performed here.</b> This repository is a pure
/// storage layer. <c>IPaymentApplicationService</c> (PR 3) is responsible for
/// updating <see cref="Payment.UnappliedAmount"/>, Invoice/Bill balances, and
/// status fields after adding or deleting application records.
/// </para>
/// </summary>
public sealed class InMemoryPaymentApplicationRepository : IPaymentApplicationRepository
{
    private readonly ConcurrentDictionary<PaymentApplicationId, PaymentApplication> _applications = new();

    /// <inheritdoc />
    public Task AddAsync(PaymentApplication application, CancellationToken cancellationToken = default)
    {
        if (application is null) throw new ArgumentNullException(nameof(application));
        if (!_applications.TryAdd(application.Id, application))
            throw new InvalidOperationException($"PaymentApplication '{application.Id.Value}' already exists.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PaymentApplication?> GetAsync(PaymentApplicationId id, CancellationToken cancellationToken = default)
    {
        _applications.TryGetValue(id, out var app);
        return Task.FromResult<PaymentApplication?>(app);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(PaymentApplicationId id, CancellationToken cancellationToken = default)
    {
        var removed = _applications.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PaymentApplication>> ListByPaymentAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
    {
        var rows = _applications.Values
            .Where(a => a.PaymentId == paymentId)
            .ToList();
        return Task.FromResult<IReadOnlyList<PaymentApplication>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PaymentApplication>> ListByTargetAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var rows = _applications.Values
            .Where(a => string.Equals(a.TargetId, targetId, StringComparison.Ordinal))
            .ToList();
        return Task.FromResult<IReadOnlyList<PaymentApplication>>(rows);
    }
}
