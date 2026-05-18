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
    /// <b>PR 2 / PR 3:</b> <see cref="IPaymentPostingService"/> registers via
    /// <see cref="DefaultPaymentPostingService"/> and
    /// <see cref="IPaymentApplicationService"/> registers via
    /// <see cref="DefaultPaymentApplicationService"/>. The host must also have
    /// the ledger, AR, and AP substrates wired (
    /// <c>AddSunfishFinancialLedger</c>, <c>AddSunfishFinancialAr</c>,
    /// <c>AddSunfishFinancialAp</c>) — those provide
    /// <see cref="IJournalPostingService"/>, <see cref="IAccountResolver"/>,
    /// <see cref="IInvoiceRepository"/>, and <see cref="IBillRepository"/>.
    /// </para>
    ///
    /// <para>
    /// <b>PR 3 amber-amendment:</b> the host MUST also register
    /// <see cref="Sunfish.Foundation.MultiTenancy.ITenantContext"/>.
    /// <see cref="DefaultPaymentApplicationService"/> consumes it for
    /// service-level tenant-isolation guards; invoking the service without a
    /// resolved tenant throws <see cref="InvalidOperationException"/>.
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
        services.TryAddScoped<IPaymentApplicationService, DefaultPaymentApplicationService>();

        return services;
    }
}
