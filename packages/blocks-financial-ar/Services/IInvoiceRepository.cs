using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// CRUD surface over the AR invoice substrate. Persistence-backed
/// implementations (SQLite, Postgres) ship in a follow-on substrate
/// hand-off; <see cref="InMemoryInvoiceRepository"/> backs the v1 desktop
/// path. Posting / numbering / aging / event emission ride on top.
///
/// <para>
/// <b>Cohort-2 PR 0a tenant-keying retrofit (pattern-009-tenant-keying-retrofit
/// candidate; ADR 0092 Step 1).</b> Every method takes <see cref="TenantId"/>
/// as the FIRST positional parameter (analyzer-enforced at ADR 0092 Step 4c
/// when that ships). Read methods filter by tenant and return null / empty on
/// cross-tenant — same code path as not-found (no diagnostic leak per ADR 0092
/// §"Diagnostic non-leak invariant"). Write methods assert
/// <c>entity.TenantId == tenantId</c> at the boundary; throw
/// <see cref="ArgumentException"/> on mismatch (caller bug — defensive-depth
/// at the substrate level).
/// </para>
/// </summary>
public interface IInvoiceRepository : ITenantScopedRepository<Invoice, InvoiceId>
{
    /// <summary>
    /// Insert or update an invoice. Throws on a tombstoned target.
    /// <see cref="ArgumentException"/> when <c>invoice.TenantId</c> does not
    /// match <paramref name="tenantId"/>.
    /// </summary>
    Task UpsertAsync(TenantId tenantId, Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an invoice by id. Returns null when missing, tombstoned, OR scoped
    /// to a different tenant (uniform-404 invariant — no diagnostic leak).
    /// Cross-tenant reads emit <c>AuditEventType.TenantBoundaryViolation</c>
    /// when audit emission is wired.
    /// </summary>
    Task<Invoice?> GetAsync(TenantId tenantId, InvoiceId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find an invoice by its customer-facing number within a chart, scoped to
    /// <paramref name="tenantId"/>. Returns null on miss OR cross-tenant.
    /// </summary>
    Task<Invoice?> GetByNumberAsync(TenantId tenantId, ChartOfAccountsId chartId, string invoiceNumber, CancellationToken cancellationToken = default);

    /// <summary>List all live (non-tombstoned) invoices in a chart for <paramref name="tenantId"/>.</summary>
    Task<IReadOnlyList<Invoice>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>List all live invoices for a given customer within a chart, scoped to <paramref name="tenantId"/>.</summary>
    Task<IReadOnlyList<Invoice>> ListByCustomerAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstone an invoice (sets <c>DeletedAtUtc</c> + <c>DeletedReason</c>). Idempotent:
    /// a second call on an already-deleted row is a no-op. Returns false when the id is
    /// unknown OR scoped to a different tenant (uniform-404).
    /// </summary>
    Task<bool> SoftDeleteAsync(TenantId tenantId, InvoiceId id, PartyId actor, string? reason, CancellationToken cancellationToken = default);
}
