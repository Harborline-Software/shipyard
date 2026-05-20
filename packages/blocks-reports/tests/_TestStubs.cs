using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// Minimal <see cref="ITenantContext"/> stub for tests that need to construct
/// services (e.g. <see cref="Sunfish.Blocks.FinancialAr.Services.ArAgingService"/>)
/// which now require an ambient tenant after cohort-2 PR 0a's tenant-keying
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
