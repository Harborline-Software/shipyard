using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <b>Note:</b> <c>IPaymentPostingService</c> and
    /// <c>IPaymentApplicationService</c> are NOT registered here — their
    /// implementations ship in PRs 2 and 3 respectively. Downstream callers
    /// that need those services must register them separately once those PRs
    /// land.
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

        return services;
    }
}
