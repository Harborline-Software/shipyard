using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>
    /// Registers <see cref="NoopFormDefinitionStore"/> as the default
    /// <see cref="IFormDefinitionStore"/> via <c>TryAddSingleton</c> —
    /// reads return empty / <see cref="Exceptions.FormDefinitionNotFoundException"/>;
    /// lifecycle mutators throw <see cref="NotSupportedException"/>.
    /// Read-side composition (Ship's Office browser, status surfaces) gets
    /// a non-throwing default; host composition overrides with
    /// <see cref="AddInMemoryFormDefinitionStore"/> (or a Postgres-backed
    /// registration) when authoring is wired.
    /// </summary>
    /// <remarks>
    /// Block-tier packages (cf. <c>blocks-ships-office</c>) call this from
    /// their <c>AddSunfish*Defaults()</c> extension so that consumers
    /// composing the block without an explicit forms store still get a
    /// non-throwing read surface. <c>TryAdd</c> ensures a real store
    /// already registered by the host wins.
    /// </remarks>
    public static IServiceCollection TryAddNoopFormDefinitionStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFormDefinitionStore, NoopFormDefinitionStore>();
        return services;
    }
}
