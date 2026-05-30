using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Forms.DependencyInjection;

/// <summary>
/// DI registration extensions for the Foundation.Forms keystone substrate.
/// </summary>
public static class FormsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="IFormDefinitionStore"/> reference
    /// implementation as a singleton. Suitable for tests, single-process
    /// bootstrapping, and the early authoring-UX iteration loop. The
    /// production Postgres-backed registry registers via a separate
    /// extension method on the entity-store package (forthcoming).
    /// </summary>
    /// <remarks>
    /// Registration is idempotent — calling this method twice does not
    /// double-register or throw. The first registration wins, matching the
    /// fleet's convention for substrate DI hygiene.
    /// </remarks>
    public static IServiceCollection AddInMemoryFormDefinitionStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IFormDefinitionStore, InMemoryFormDefinitionStore>();
        return services;
    }
}
