using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.RecurringBilling.Services;
using Sunfish.Foundation.Localization;

namespace Sunfish.Blocks.RecurringBilling.DependencyInjection;

/// <summary>
/// Extension methods for registering rent-collection services in a
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class RecurringBillingServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="InMemoryRecurringBillingService"/> as the
    /// <see cref="IRecurringBillingService"/> implementation. Also contributes the
    /// open-generic <see cref="ISunfishLocalizer{T}"/> binding so consumers can
    /// resolve the rent-collection <c>SharedResource</c> bundle. Caller is
    /// responsible for wiring <c>services.AddLocalization()</c> in the composition
    /// root (matches the Bridge pattern; class libraries don't take a hard
    /// PackageReference on <c>Microsoft.Extensions.Localization</c>).
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryRecurringBilling(this IServiceCollection services)
    {
        services.AddSingleton<IRecurringBillingService, InMemoryRecurringBillingService>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>)));
        return services;
    }
}
