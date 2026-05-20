using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// CRUD surface over the payments substrate. Persistence-backed implementations
/// (SQLite, Postgres) shadow this binding; <see cref="InMemoryPaymentRepository"/>
/// backs the v1 desktop path.
///
/// <para>
/// <b>Cohort-2 PR 0c tenant-keying retrofit (pattern-009-tenant-keying-retrofit
/// candidate; ADR 0092 Step 1).</b> Every method takes <see cref="TenantId"/>
/// as the FIRST positional parameter. Read methods filter by tenant and return
/// null / empty on cross-tenant (uniform-404 per ADR 0092). Write methods
/// assert <c>payment.TenantId == tenantId</c>; mismatch throws
/// <see cref="ArgumentException"/>.
/// </para>
///
/// <para>
/// PR 0c is the REPOSITORY-layer companion to W#68 PR 3 Option A's
/// SERVICE-layer tenant isolation amendment. Together they provide
/// defense-in-depth: cross-tenant access is rejected at both layers.
/// </para>
/// </summary>
public interface IPaymentRepository : ITenantScopedRepository<Payment, PaymentId>
{
    /// <summary>
    /// Insert a new payment. Throws if a payment with the same id already exists.
    /// <see cref="ArgumentException"/> when <c>payment.TenantId</c> does not match
    /// <paramref name="tenantId"/>.
    /// </summary>
    Task AddAsync(TenantId tenantId, Payment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a payment by id. Returns null when missing OR scoped to a different
    /// tenant (uniform-404). Cross-tenant reads emit
    /// <c>AuditEventType.TenantBoundaryViolation</c> when audit emission is wired.
    /// </summary>
    Task<Payment?> GetAsync(TenantId tenantId, PaymentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace an existing payment record. Throws if the id is unknown OR
    /// scoped to a different tenant (cross-tenant overwrite attempt;
    /// <see cref="ArgumentException"/>).
    /// </summary>
    Task UpdateAsync(TenantId tenantId, Payment payment, CancellationToken cancellationToken = default);

    /// <summary>List all payments in a chart for <paramref name="tenantId"/>, ordered by <c>PaymentDate</c> descending.</summary>
    Task<IReadOnlyList<Payment>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default);

    /// <summary>List all payments for a specific party (customer or vendor) within a chart, scoped to <paramref name="tenantId"/>.</summary>
    Task<IReadOnlyList<Payment>> ListByPartyAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId partyId, CancellationToken cancellationToken = default);
}
