using System.Net;

namespace Sunfish.Foundation.Bootstrap;

// TODO (Step 3 — ADR 0095): ship BootstrapAndTenantMutualExclusionAnalyzer
// (Roslyn) for per-constructor mutual-exclusion enforcement. Until it lands,
// doc-comment + reviewer discipline carry the invariant (ADR 0091 R2
// amendment A2 precedent). The Step 1 BootstrapAndTenantMutualExclusionAssertion
// is the SECONDARY gate (registration-presence + composition-root opt-in only).

/// <summary>
/// Scoped per-request surface for the pre-tenant window (signup / invitation-
/// accept / verify-email). Bound on the bootstrap pipeline branch only;
/// mutually exclusive with the post-tenant interfaces
/// (<c>Sunfish.Foundation.Authorization.ITenantContext</c> facade,
/// <c>Sunfish.Foundation.MultiTenancy.ITenantContext</c>,
/// <c>Sunfish.Foundation.Authorization.ICurrentUser</c>,
/// <c>Sunfish.Foundation.Authorization.IAuthorizationContext</c>,
/// <c>Sunfish.Bridge.Middleware.IBrowserTenantContext</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Mutual exclusion (ADR 0095).</b> No constructor / no request scope MUST
/// inject both <c>IBootstrapContext</c> and any post-tenant context. Enforcement
/// splits across two fail-closed layers: (a) the Step 3
/// <c>BootstrapAndTenantMutualExclusionAnalyzer</c> (PRIMARY; per-constructor
/// scan) and (b) the Step 1
/// <see cref="DependencyInjection.BootstrapAndTenantMutualExclusionAssertion"/>
/// (SECONDARY; startup registration-presence + composition-root opt-in). The
/// startup assertion CANNOT verify "no request scope simultaneously resolves"
/// because the root container deliberately holds both bindings; per-constructor
/// enforcement IS the analyzer. During the Step 1–2 window before the analyzer
/// ships, doc-comment + reviewer discipline carry the invariant.
/// </para>
/// <para>
/// <b>Bootstrap → post-tenant transition (ADR 0095 §"Handler Lifecycle").</b>
/// The bootstrap scope MUST NOT resolve <c>Sunfish.Bridge.Data.SunfishBridgeDbContext</c>
/// (it captures <c>tenant.TenantId</c> at construction for per-entity query
/// filters; in bootstrap scope the captured value would be the facade's
/// empty-string default and filter behavior is undefined). After
/// <c>ITenantRegistry.CreateAsync</c> returns, the signup handler creates a
/// child <c>IServiceScope</c>, populates a scoped <c>ITenantContextSeed</c>,
/// resolves the post-tenant family from the child scope, writes the initial
/// User aggregate, then disposes the child scope; the outer bootstrap scope
/// continues for audit + email. The seed-holder mechanism + DbContext guard
/// ship in Step 2 (W79 hand-off).
/// </para>
/// <para>
/// <b>Surface area is small and intentional.</b> Five members. Additional
/// pre-tenant correlation needs ship as separate interfaces or a Revision
/// amendment (see ADR 0095 §"Out of scope but flagged").
/// </para>
/// </remarks>
public interface IBootstrapContext
{
    /// <summary>
    /// Stable request correlation ID; flows into logs + audit events emitted
    /// from the bootstrap pipeline. Generated at the pipeline-branch entry
    /// point (one ID per request; reuses a validated <c>X-Correlation-Id</c>
    /// header value if the caller supplied one).
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Client IP (post-<c>X-Forwarded-For</c> evaluation per ASP.NET Core's
    /// <c>UseForwardedHeaders</c> configuration); <see langword="null"/> when
    /// the underlying connection has no addressable peer (test contexts).
    /// </summary>
    IPAddress? ClientIp { get; }

    /// <summary>
    /// CAPTCHA verdict token from the form payload; <see langword="null"/>
    /// pre-verification or for endpoints that don't require CAPTCHA (e.g.,
    /// verify-email links that already carry signed-token authentication).
    /// </summary>
    string? CaptchaToken { get; }

    /// <summary>
    /// Idempotency key from the <c>X-Idempotency-Key</c> header;
    /// <see langword="null"/> when the caller didn't supply one. Signup itself
    /// is non-idempotent; invitation-accept is idempotency-required by handler
    /// contract.
    /// </summary>
    string? IdempotencyKey { get; }

    /// <summary>
    /// Bucket key for the ASP.NET Core <c>RateLimiter</c> (per-IP layer +
    /// per-route+per-IP layer). Non-null and deterministic.
    /// </summary>
    /// <remarks>
    /// When <see cref="ClientIp"/> is non-null, the bucket key shape is
    /// <c>ip:{normalized-ip}</c> for the per-IP layer and
    /// <c>route:{normalized-path}|ip:{normalized-ip}</c> for the
    /// per-route+per-IP layer. When <see cref="ClientIp"/> is null, the bucket
    /// key falls back to a route-only bucket <c>route:{normalized-path}</c>.
    /// Concrete normalization + policy values are W79 Stage-05 hand-off scope;
    /// the ADR-tier invariant is non-null + deterministic + non-trivial.
    /// </remarks>
    string RateLimitBucketKey { get; }
}
