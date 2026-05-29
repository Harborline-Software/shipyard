using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Assets.Services;

/// <summary>
/// Internal guard enforcing that asset-domain repository operations always run
/// against a real tenant. The system / default <see cref="TenantId"/>
/// (per <see cref="TenantId.IsSystemSentinel"/>, which is also <c>true</c> for a
/// default-constructed value) is rejected — satisfying the C1.1 PASS gate
/// (ADR 0101): "repos reject <c>TenantId.System</c>".
/// </summary>
internal static class TenantGuard
{
    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="tenant"/> is a
    /// system / default sentinel (fail-closed for the multi-tenant query path).
    /// </summary>
    public static void Require(TenantId tenant)
    {
        if (tenant.IsSystemSentinel)
        {
            throw new ArgumentException(
                "Asset-domain operations require a real tenant; the system / default "
                + "TenantId sentinel is rejected (fail-closed multi-tenant isolation, ADR 0084 / 0101).",
                nameof(tenant));
        }
    }
}
