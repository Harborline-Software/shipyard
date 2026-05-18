using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;

namespace Sunfish.Blocks.FinancialPayments.DependencyInjection;

/// <summary>
/// DI helpers for the financial-payments cluster.
/// </summary>
public static class PaymentsServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory payments substrate. Uses <c>TryAddSingleton</c>
    /// so persistence-backed implementations registered earlier by the host
    /// shadow these defaults.
    ///
    /// <para>
    /// Optional <paramref name="configure"/> lets the host override
    /// <see cref="BlocksFinancialPaymentsOptions"/>. Omitting it uses defaults
    /// (5-minute fallback polling interval).
    /// </para>
    ///
    /// <para>
    /// <b>Note:</b> <c>IPaymentApplicationService</c> is NOT registered here —
    /// its implementation ships in PR 3 alongside the direction-matching
    /// invariant. Downstream callers that need it must register it separately
    /// once that PR lands.
    /// </para>
    ///
    /// <para>
    /// <b>PR 2:</b> <see cref="IPaymentPostingService"/> registers via
    /// <see cref="DefaultPaymentPostingService"/>. The host must also have
    /// the ledger, AR, and AP substrates wired (
    /// <c>AddSunfishFinancialLedger</c>, <c>AddSunfishFinancialAr</c>,
    /// <c>AddSunfishFinancialAp</c>) — those provide
    /// <see cref="IJournalPostingService"/>, <see cref="IAccountResolver"/>,
    /// <see cref="IInvoiceRepository"/>, and <see cref="IBillRepository"/>.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSunfishFinancialPayments(
        this IServiceCollection services,
        Action<BlocksFinancialPaymentsOptions>? configure = null)
    {
        var options = new BlocksFinancialPaymentsOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
        services.TryAddSingleton<IPaymentApplicationRepository, InMemoryPaymentApplicationRepository>();
        services.TryAddScoped<IPaymentPostingService, DefaultPaymentPostingService>();

        return services;
    }
}
