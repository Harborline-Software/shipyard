using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// CRUD surface over the AP bill substrate. Mirrors AR's
/// <c>IInvoiceRepository</c> with vendor-specific query methods
/// (lookup by vendor + vendor-supplied bill number, list open bills
/// per vendor, etc.).
///
/// <para>
/// <b>Cohort-2 PR 0b tenant-keying retrofit (pattern-009-tenant-keying-retrofit
/// candidate; ADR 0092 Step 1).</b> Every method takes <see cref="TenantId"/>
/// as the FIRST positional parameter (analyzer-enforced at ADR 0092 Step 4c).
/// Read methods filter by tenant and return null / empty on cross-tenant —
/// uniform-404 invariant per ADR 0092 §"Diagnostic non-leak invariant". Write
/// methods assert <c>entity.TenantId == tenantId</c>; mismatch throws
/// <see cref="ArgumentException"/>.
/// </para>
/// </summary>
public interface IBillRepository : ITenantScopedRepository<Bill, BillId>
{
    /// <summary>
    /// Insert or update a bill. Throws on a tombstoned target.
    /// <see cref="ArgumentException"/> when <c>bill.TenantId</c> does not
    /// match <paramref name="tenantId"/>.
    /// </summary>
    Task UpsertAsync(TenantId tenantId, Bill bill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a bill by id. Returns null when missing, tombstoned, OR scoped to a
    /// different tenant (uniform-404 invariant — no diagnostic leak). Cross-tenant
    /// reads emit <c>AuditEventType.TenantBoundaryViolation</c> when audit
    /// emission is wired.
    /// </summary>
    Task<Bill?> GetAsync(TenantId tenantId, BillId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a bill by the vendor's own number, scoped to (tenant, chart, vendor).
    /// The composite key catches duplicate-bill submissions from the same vendor.
    /// </summary>
    Task<Bill?> GetByVendorBillNumberAsync(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId vendorId,
        string billNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Find a bill by its external-ref tag (e.g. ERPNext sync), scoped to <paramref name="tenantId"/>.</summary>
    Task<Bill?> GetByExternalRefAsync(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        string externalRef,
        CancellationToken cancellationToken = default);

    /// <summary>List all live (non-tombstoned) bills in a chart for <paramref name="tenantId"/>.</summary>
    Task<IReadOnlyList<Bill>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>List all live bills for a given vendor within a chart, scoped to <paramref name="tenantId"/>.</summary>
    Task<IReadOnlyList<Bill>> ListByVendorAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId vendorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List bills currently in an Open state (Received / Approved / PartiallyPaid)
    /// for <paramref name="tenantId"/>. Optionally filter by vendor / property.
    /// </summary>
    Task<IReadOnlyList<Bill>> QueryOpenAsync(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId? vendorId = null,
        string? propertyId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstone a bill (sets <c>DeletedAtUtc</c> + <c>DeletedReason</c>).
    /// Idempotent. Returns false when the id is unknown OR scoped to a different
    /// tenant (uniform-404).
    /// </summary>
    Task<bool> SoftDeleteAsync(TenantId tenantId, BillId id, PartyId actor, string? reason, CancellationToken cancellationToken = default);
}
