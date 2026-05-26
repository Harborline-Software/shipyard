using System;

namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Bind-once scoped holder that carries the tenant id into a freshly-created
/// child <c>IServiceScope</c> during the bootstrap → post-tenant transition
/// (ADR 0095 §"Handler Lifecycle", α-1 mechanism). Populated immediately after
/// <c>ITenantRegistry.CreateAsync</c> returns the new tenant and before any
/// post-tenant context (<see cref="ITenantContext"/> facade,
/// <see cref="ICurrentUser"/>, <see cref="IAuthorizationContext"/>) is resolved
/// inside the child scope; <see cref="SeededTenantContext"/> reads it.
/// </summary>
/// <remarks>
/// Registered scoped at the post-tenant pipeline branch's composition root.
/// Bind-once: a second <see cref="Bind"/> within the same scope throws (a scope
/// is bound to exactly one tenant). A scope whose seed is never bound exposes a
/// null <see cref="TenantId"/> (and a null <c>Tenant</c> via the facade).
/// </remarks>
public interface ITenantContextSeed
{
    /// <summary>Binds this scope's tenant id. Throws on a second bind (bind-once).</summary>
    void Bind(Guid tenantId);

    /// <summary>The bound tenant id, or <see langword="null"/> when not yet bound.</summary>
    Guid? TenantId { get; }
}
