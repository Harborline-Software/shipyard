namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Typed marker required as a constructor parameter by the tenant-bound
/// <c>SunfishBridgeDbContext</c> (ADR 0095 Rev 2 Amendment 4 / .NET-arch C1).
/// The marker is registered ONLY on the non-bootstrap (post-tenant) pipeline
/// branch's DI scope; the bootstrap-scope branch deliberately does NOT register
/// it, so resolving the tenant-bound DbContext in bootstrap scope fails fast
/// with a missing-dependency <c>InvalidOperationException</c> (the minimum-floor
/// enforcement of "the bootstrap scope MUST NOT resolve the tenant-bound
/// DbContext"). Empty by design — its mere presence/absence in the scope is the
/// signal.
/// </summary>
public sealed class RequireTenantBoundDbContext
{
}
