using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Blocks.Inspections.DependencyInjection;

/// <summary>
/// Deprecated DI extension stub for the renamed <c>blocks-reviews</c> package
/// (was <c>blocks-inspections</c>) per ADR 0098. <see cref="System.Runtime.CompilerServices.TypeForwardedToAttribute"/>
/// forwards TYPES but not extension methods, so this stub preserves the old
/// <c>AddInMemoryInspections</c> call-site by delegating to
/// <c>Sunfish.Blocks.Reviews.DependencyInjection.ReviewsServiceCollectionExtensions.AddInMemoryReviews</c>
/// (ADR 0098 §"Per-rename migration pattern" A6). Hidden from IntelliSense; still callable from
/// already-compiled code.
/// </summary>
[Obsolete(
    "Sunfish.Blocks.Inspections.DependencyInjection.AddInMemoryInspections is renamed to "
    + "Sunfish.Blocks.Reviews.DependencyInjection.AddInMemoryReviews per ADR 0098.",
    false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class InspectionsServiceCollectionExtensions
{
    /// <summary>
    /// Deprecated alias for
    /// <c>Sunfish.Blocks.Reviews.DependencyInjection.ReviewsServiceCollectionExtensions.AddInMemoryReviews</c>.
    /// </summary>
    public static IServiceCollection AddInMemoryInspections(this IServiceCollection services)
        => Sunfish.Blocks.Reviews.DependencyInjection.ReviewsServiceCollectionExtensions
            .AddInMemoryReviews(services);
}
