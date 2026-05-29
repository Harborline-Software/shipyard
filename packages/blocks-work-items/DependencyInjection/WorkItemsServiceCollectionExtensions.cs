using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.WorkItems.Events;
using Sunfish.Blocks.WorkItems.Services;

namespace Sunfish.Blocks.WorkItems.DependencyInjection;

/// <summary>
/// DI extension for <c>blocks-work-orders</c>. Registers the write
/// surface (<see cref="IWorkItemService"/>, <see cref="IMaintenanceScheduleService"/>),
/// the read-side projection (<see cref="IContractorReadModel"/>),
/// the event publisher seam (<see cref="IWorkItemEventPublisher"/>),
/// the RRULE expansion stub (<see cref="IRruleExpansionService"/>),
/// the <c>DeficiencyRaised</c> consumer
/// (<see cref="IDeficiencyRaisedHandler"/>), and the local
/// <see cref="IPartyReadModel"/> stub that disappears via a one-line
/// re-namespace sweep when <c>blocks-people-foundation</c> ships.
/// </summary>
public static class WorkItemsServiceCollectionExtensions
{
    /// <summary>
    /// Register the work-orders cluster surface. Idempotent via
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService, TImplementation}(IServiceCollection)"/>
    /// — re-invocation does not double-register.
    /// </summary>
    public static IServiceCollection AddBlocksWorkItems(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Storage seams (in-memory; SQLite-backed prod impl lands in a
        // follow-on persistence hand-off).
        services.TryAddSingleton<InMemoryWorkItemRepository>();
        services.TryAddSingleton<InMemoryContractorRepository>();

        // Write surfaces.
        services.TryAddSingleton<IWorkItemService, InMemoryWorkItemService>();
        services.TryAddSingleton<IMaintenanceScheduleService, InMemoryMaintenanceScheduleService>();

        // Read-side projections.
        services.TryAddSingleton<IContractorReadModel>(
            sp => sp.GetRequiredService<InMemoryContractorRepository>());

        // RRULE expansion stub (FREQ=DAILY/WEEKLY/MONTHLY[;INTERVAL=N]).
        services.TryAddSingleton<IRruleExpansionService, InMemoryRruleExpansionService>();

        // Cross-cluster event publisher seam — local until
        // foundation-events ships.
        services.TryAddSingleton<IWorkItemEventPublisher, InMemoryWorkItemEventPublisher>();

        // DeficiencyRaised consumer (inbound from blocks-property-*).
        services.TryAddSingleton<IDeficiencyRaisedHandler, InMemoryDeficiencyRaisedHandler>();

        // LOCAL STUB for blocks-people-foundation's IPartyReadModel.
        services.TryAddSingleton<IPartyReadModel, InMemoryPartyReadModel>();

        return services;
    }
}
