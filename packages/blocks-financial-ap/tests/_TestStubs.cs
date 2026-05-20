using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.FinancialAp.Tests;

/// <summary>
/// Minimal <see cref="ITenantContext"/> stub for tests that need to construct
/// services requiring an ambient tenant after the cohort-2 PR 0b tenant-keying
/// retrofit.
/// </summary>
internal sealed class StubTenantContext : ITenantContext
{
    public StubTenantContext(TenantId id)
    {
        Tenant = new TenantMetadata { Id = id, Name = id.Value };
    }
    public TenantMetadata? Tenant { get; }
}
