using System;
using System.Threading;

namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Default <see cref="ITenantContextSeed"/>. Bind-once + thread-safe.
/// </summary>
/// <remarks>
/// The bound tenant id is held in a boxed-object field updated via
/// <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>. <see cref="Guid"/>
/// is a value type, so <c>Interlocked.CompareExchange&lt;Guid&gt;</c> is NOT
/// available (the generic overload requires a reference type); boxing the Guid
/// into the <see cref="object"/> holder is the correct lock-free bind-once
/// primitive — the CAS from <see langword="null"/> succeeds exactly once.
/// </remarks>
public sealed class MutableTenantContextSeed : ITenantContextSeed
{
    // null until bound; thereafter a boxed Guid. Written once via CAS-from-null.
    private object? _bound;

    /// <inheritdoc />
    public Guid? TenantId => _bound is Guid g ? g : null;

    /// <inheritdoc />
    public void Bind(Guid tenantId)
    {
        var prior = Interlocked.CompareExchange(ref _bound, tenantId, null);
        if (prior is not null)
        {
            throw new InvalidOperationException(
                "ITenantContextSeed is bind-once; this scope's tenant is already bound. "
                + "A child IServiceScope must be bound to exactly one tenant.");
        }
    }
}
