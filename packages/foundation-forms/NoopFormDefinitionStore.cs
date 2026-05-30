using System.Runtime.CompilerServices;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Forms.Exceptions;
using Sunfish.Foundation.Forms.Models;

namespace Sunfish.Foundation.Forms;

/// <summary>
/// Read-only empty placeholder implementation of
/// <see cref="IFormDefinitionStore"/> (ADR 0055 §"Schema Registry" line 417
/// relocation artifact, FN-4). Lookups return <see langword="null"/> /
/// <see cref="FormDefinitionNotFoundException"/>; enumeration yields no
/// rows; lifecycle mutators throw <see cref="NotSupportedException"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Hosts that have not yet wired a real
/// <see cref="IFormDefinitionStore"/> (in-memory or Postgres) still need
/// surfaces that <i>read</i> the registry — most notably the Ship's Office
/// browser (ADR 0083) which lists <c>DynamicTemplate</c> rows — to compose
/// without throwing. This Noop satisfies that read-side composition while
/// fail-loud rejecting any attempted mutation; the composition root replaces
/// it with a real store the moment forms authoring is wired.
/// </para>
/// <para>
/// <b>Mutator posture (NotSupported, not silent no-op).</b> A silent
/// no-op on <see cref="RegisterAsync"/> / <see cref="PublishAsync"/> /
/// <see cref="DeprecateAsync"/> / <see cref="WithdrawAsync"/> would be a
/// trust hazard — callers would believe a definition was registered when
/// it was not, and the entity store / audit trail would silently drift.
/// Throwing <see cref="NotSupportedException"/> matches the fleet's
/// "fail-loud rather than fail-safe-to-success" §Trust posture (cf.
/// <c>blocks-ships-office</c>'s status-mapping fail-loud sweep).
/// </para>
/// <para>
/// <b>DI registration.</b> Registered via
/// <see cref="DependencyInjection.FormsServiceCollectionExtensions.TryAddNoopFormDefinitionStore"/>
/// as a <c>TryAddSingleton</c> default — host composition overrides with
/// <see cref="DependencyInjection.FormsServiceCollectionExtensions.AddInMemoryFormDefinitionStore"/>
/// (or a Postgres-backed registration when that adapter ships) at the
/// composition root.
/// </para>
/// </remarks>
public sealed class NoopFormDefinitionStore : IFormDefinitionStore
{
    /// <inheritdoc />
    /// <exception cref="FormDefinitionNotFoundException">Always — the Noop
    /// store contains no revisions, so every <c>Get</c> is a miss.</exception>
    public ValueTask<FormDefinition> GetAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => throw new FormDefinitionNotFoundException(id, version, tenant);

    /// <inheritdoc />
    /// <remarks>Always returns <see langword="null"/> — the Noop store has
    /// no Published revision for any definition.</remarks>
    public ValueTask<FormDefinition?> GetCurrentPublishedAsync(TenantId tenant, FormDefinitionId id, CancellationToken ct = default)
        => new((FormDefinition?)null);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — the Noop store is
    /// read-only; the composition root must register a real
    /// <see cref="IFormDefinitionStore"/> for authoring flows.</exception>
    public ValueTask<FormDefinition> RegisterAsync(FormDefinition definition, CancellationToken ct = default)
        => throw new NotSupportedException(ReadOnlyMessage);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — Noop store is read-only.</exception>
    public ValueTask<FormDefinition> PublishAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => throw new NotSupportedException(ReadOnlyMessage);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — Noop store is read-only.</exception>
    public ValueTask<FormDefinition> DeprecateAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => throw new NotSupportedException(ReadOnlyMessage);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always — Noop store is read-only.</exception>
    public ValueTask<FormDefinition> WithdrawAsync(TenantId tenant, FormDefinitionId id, SemanticVersion version, CancellationToken ct = default)
        => throw new NotSupportedException(ReadOnlyMessage);

    /// <inheritdoc />
    /// <remarks>Always empty — the Noop store has no definitions.</remarks>
#pragma warning disable CS1998 // intentional: contract is async-enumerable; an empty sequence with no await is the canonical Noop shape
    public async IAsyncEnumerable<FormDefinition> ListByTenantAsync(TenantId tenant, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        yield break;
    }
#pragma warning restore CS1998

    private const string ReadOnlyMessage =
        "NoopFormDefinitionStore is a read-only placeholder; register a real IFormDefinitionStore " +
        "(AddInMemoryFormDefinitionStore for in-process / single-tenant scenarios, or a Postgres-backed " +
        "store via the foundation-assets-postgres extension) at the composition root to enable authoring.";
}
