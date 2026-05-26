namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Compile-time marker for the <b>mock</b> concrete of a Tier-2 vendor-provider
/// contract (ADR 0096). Carries no members. A concrete that implements a Tier-2
/// egress contract AND <see cref="IMockVendorProvider"/> is the in-process /
/// no-network stand-in used in dev, test, and closed-demo deployments; the real
/// HttpClient-backed adapter MUST NOT implement this marker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a marker, not configuration.</b> The marker lets the startup
/// <c>MockProviderProductionGuardAssertion</c> detect — by
/// <c>ServiceDescriptor.ImplementationType</c> scan of the registration tree —
/// that a mock concrete is wired for a contract in a Production environment
/// without the operator having set the real-adapter env-var (or the explicit
/// <c>SUNFISH_ALLOW_MOCK_PROVIDERS</c> opt-out). The
/// <c>AddSunfishVendorProvider&lt;TContract, TConcrete&gt;</c> DI helper makes
/// "the mock concrete carries the marker" a <i>compile error</i> if violated
/// (<c>where TConcrete : class, TContract, IMockVendorProvider</c>), not a
/// runtime no-op.
/// </para>
/// <para>
/// <b>Tier-2 contract naming conventions</b> (documented here as the canonical
/// reference for new vendor contracts):
/// <list type="bullet">
///   <item><b><c>IXProvider</c></b> — the egress-surface contract naming
///     convention (e.g. <c>IEmailProvider</c>, future <c>ISmsProvider</c>).
///     Noun-form provider surface.</item>
///   <item><b><c>IXProviderConfig</c></b> — the configuration-binding naming
///     convention (e.g. <c>ICaptchaProviderConfig</c>, which already follows
///     this convention today — the convention is half-applied in the substrate).</item>
///   <item><b><c>ICaptchaVerifier</c></b> — a <i>grandfathered</i> egress-surface
///     action-noun exception (per ADR 0059 / W#28 Phase 3). New contracts use
///     the <c>IXProvider</c> noun-form; <c>ICaptchaVerifier</c> is not
///     renamed.</item>
/// </list>
/// </para>
/// </remarks>
public interface IMockVendorProvider
{
}
