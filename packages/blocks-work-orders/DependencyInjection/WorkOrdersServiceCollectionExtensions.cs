using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Blocks.WorkOrders.DependencyInjection;

/// <summary>
/// Deprecated DI extension stub for the renamed <c>blocks-work-items</c> package
/// (was <c>blocks-work-orders</c>) per ADR 0098. <see cref="System.Runtime.CompilerServices.TypeForwardedToAttribute"/>
/// forwards TYPES but not extension methods (which compile to fully-qualified references in the
/// consumer assembly's IL), so this stub preserves the old <c>AddBlocksWorkOrders</c> call-site by
/// delegating to <c>Sunfish.Blocks.WorkItems.DependencyInjection.WorkItemsServiceCollectionExtensions.AddBlocksWorkItems</c>
/// (ADR 0098 §"Per-rename migration pattern" A6). Hidden from IntelliSense via
/// <see cref="EditorBrowsableAttribute"/>; still callable from already-compiled code.
/// </summary>
[Obsolete(
    "Sunfish.Blocks.WorkOrders.DependencyInjection.AddBlocksWorkOrders is renamed to "
    + "Sunfish.Blocks.WorkItems.DependencyInjection.AddBlocksWorkItems per ADR 0098.",
    false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class WorkOrdersServiceCollectionExtensions
{
    /// <summary>
    /// Deprecated alias for
    /// <c>Sunfish.Blocks.WorkItems.DependencyInjection.WorkItemsServiceCollectionExtensions.AddBlocksWorkItems</c>.
    /// </summary>
    public static IServiceCollection AddBlocksWorkOrders(this IServiceCollection services)
        => Sunfish.Blocks.WorkItems.DependencyInjection.WorkItemsServiceCollectionExtensions
            .AddBlocksWorkItems(services);
}
