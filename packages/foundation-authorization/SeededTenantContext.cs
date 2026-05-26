using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Post-tenant context adapter used inside the child <c>IServiceScope</c> of the
/// bootstrap → post-tenant transition (ADR 0095 α-1). A single instance provides
/// all four bindings (<see cref="Sunfish.Foundation.MultiTenancy.ITenantContext"/>,
/// <see cref="ICurrentUser"/>, <see cref="IAuthorizationContext"/>, and the sum
/// facade <see cref="ITenantContext"/>) per ADR 0091 R2 A1, resolving the tenant
/// from <see cref="ITenantContextSeed"/>.
/// </summary>
/// <remarks>
/// During the signup bootstrap write there is no authenticated principal yet —
/// the scope exists to TENANT-SCOPE the initial-user write, not to authorize a
/// caller. Accordingly <see cref="UserId"/> is empty, <see cref="Roles"/> is
/// empty, and <see cref="HasPermission"/> is least-privilege (<see langword="false"/>).
/// <see cref="Tenant"/> is null until the seed is bound (matching the facade's
/// empty-string <c>TenantId</c> default).
/// </remarks>
public sealed class SeededTenantContext : ITenantContext
{
    private readonly ITenantContextSeed _seed;

    public SeededTenantContext(ITenantContextSeed seed)
        => _seed = seed ?? throw new ArgumentNullException(nameof(seed));

    /// <inheritdoc />
    public TenantMetadata? Tenant =>
        _seed.TenantId is { } id
            ? new TenantMetadata { Id = new TenantId(id.ToString()), Name = id.ToString() }
            : null;

    /// <inheritdoc />
    public string UserId => string.Empty;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles => Array.Empty<string>();

    /// <inheritdoc />
    public bool HasPermission(string permission) => false;
}
