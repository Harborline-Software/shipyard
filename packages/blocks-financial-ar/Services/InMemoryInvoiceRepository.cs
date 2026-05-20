using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// In-memory <see cref="IInvoiceRepository"/>. State lives in a single
/// <c>ConcurrentDictionary</c> keyed by <see cref="InvoiceId"/>;
/// secondary queries (by chart, by number, by customer) scan the values
/// — fine for the in-memory v1 with O(invoices) on a single tenant. A
/// SQLite-backed implementation lands in the follow-on substrate
/// hand-off and shadows this binding.
///
/// <para>
/// <b>Cohort-2 PR 0a tenant-keying retrofit.</b> Every <c>Get*</c> /
/// <c>List*</c> filters by the <c>tenantId</c> argument; rows belonging
/// to a different tenant are treated as not-found (uniform-404 per ADR
/// 0092 §"Diagnostic non-leak invariant"). When audit emission is wired
/// (via the <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/>
/// ctor), a cross-tenant <c>Get</c> hit emits
/// <c>AuditEventType.TenantBoundaryViolation</c> before returning null.
/// Writes (<c>UpsertAsync</c> / <c>SoftDeleteAsync</c>) assert
/// <c>entity.TenantId == tenantId</c> at the boundary; mismatch throws
/// <see cref="ArgumentException"/>.
/// </para>
/// </summary>
public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly ConcurrentDictionary<InvoiceId, Invoice> _invoices = new();
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _auditTenant;

    /// <summary>Creates the repository without audit emission (tests, demos).</summary>
    public InMemoryInvoiceRepository()
    {
    }

    /// <summary>
    /// Creates the repository with audit emission wired through
    /// <paramref name="auditTrail"/> + <paramref name="signer"/>;
    /// <paramref name="auditTenant"/> is the tenant attribution applied to
    /// emitted records (typically the system tenant or the request's
    /// resolved tenant).
    /// </summary>
    public InMemoryInvoiceRepository(IAuditTrail auditTrail, IOperationSigner signer, TenantId auditTenant)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (auditTenant == default)
        {
            throw new ArgumentException("TenantId is required for audit emission.", nameof(auditTenant));
        }
        _auditTrail = auditTrail;
        _signer = signer;
        _auditTenant = auditTenant;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(TenantId tenantId, Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        if (!invoice.TenantId.Equals(tenantId))
        {
            throw new ArgumentException(
                $"Invoice '{invoice.Id.Value}' carries TenantId '{invoice.TenantId.Value}' but caller passed tenantId '{tenantId.Value}'.",
                nameof(invoice));
        }

        if (_invoices.TryGetValue(invoice.Id, out var existing))
        {
            if (existing.DeletedAtUtc is not null)
            {
                throw new InvalidOperationException($"Invoice '{invoice.Id.Value}' is tombstoned; further mutations are not permitted.");
            }
            if (!existing.TenantId.Equals(tenantId))
            {
                // Cross-tenant write attempt against an existing row — caller
                // bug. Surface as ArgumentException (same family as the
                // boundary check above).
                await EmitTenantBoundaryViolationAsync(invoice.Id.Value, tenantId, existing.TenantId, cancellationToken).ConfigureAwait(false);
                throw new ArgumentException(
                    $"Invoice id '{invoice.Id.Value}' already exists under a different tenant.",
                    nameof(invoice));
            }
        }

        // Drafts may carry an empty InvoiceNumber (PR 3 mints on Issue).
        // Issued+ invoices MUST match the canonical numbering format —
        // a malformed number would surface as a bad ERPNext-importer
        // payload or a misuse of `Invoice.Create` with hand-rolled string.
        if (invoice.Status != Models.InvoiceStatus.Draft
            && !InvoiceNumberFormat.IsWellFormed(invoice.InvoiceNumber))
        {
            throw new InvalidOperationException(
                $"Invoice '{invoice.Id.Value}' is in status '{invoice.Status}' but its InvoiceNumber '{invoice.InvoiceNumber}' does not match the canonical format 'INV-YYYY-MM-DD-{{Replica}}-{{NNNN}}'.");
        }

        _invoices[invoice.Id] = invoice;
    }

    /// <inheritdoc />
    public async Task<Invoice?> GetAsync(TenantId tenantId, InvoiceId id, CancellationToken cancellationToken = default)
    {
        if (!_invoices.TryGetValue(id, out var inv)) return null;
        if (inv.DeletedAtUtc is not null) return null;
        if (!inv.TenantId.Equals(tenantId))
        {
            await EmitTenantBoundaryViolationAsync(id.Value, tenantId, inv.TenantId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        return inv;
    }

    /// <inheritdoc />
    public Task<Invoice?> GetByNumberAsync(TenantId tenantId, ChartOfAccountsId chartId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var hit = _invoices.Values.FirstOrDefault(i =>
            i.DeletedAtUtc is null
            && i.TenantId.Equals(tenantId)
            && i.ChartId == chartId
            && string.Equals(i.InvoiceNumber, invoiceNumber, StringComparison.Ordinal));
        return Task.FromResult<Invoice?>(hit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Invoice>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        var rows = _invoices.Values
            .Where(i => i.DeletedAtUtc is null && i.TenantId.Equals(tenantId) && i.ChartId == chartId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Invoice>> ListByCustomerAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId customerId, CancellationToken cancellationToken = default)
    {
        var rows = _invoices.Values
            .Where(i => i.DeletedAtUtc is null && i.TenantId.Equals(tenantId) && i.ChartId == chartId && i.CustomerId == customerId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(rows);
    }

    /// <inheritdoc />
    public async Task<bool> SoftDeleteAsync(TenantId tenantId, InvoiceId id, PartyId actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (!_invoices.TryGetValue(id, out var inv)) return false;
        if (!inv.TenantId.Equals(tenantId))
        {
            await EmitTenantBoundaryViolationAsync(id.Value, tenantId, inv.TenantId, cancellationToken).ConfigureAwait(false);
            return false;
        }
        if (inv.DeletedAtUtc is not null) return true; // idempotent

        var now = Instant.Now;
        _invoices[id] = inv with
        {
            DeletedAtUtc = now,
            DeletedBy = actor,
            DeletedReason = reason,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = inv.Version + 1,
        };
        return true;
    }

    // ── Audit emission (ADR 0092 §A6 canonical payload shape — sec-eng SPOT-CHECK GREEN template) ──
    //
    // Payload carries:
    //   entity_type     — fixed string per repository
    //   entity_id       — opaque entity identifier value
    //   requested_tenant — the tenant the CALLER passed (sec-eng AMBER amendment: was "observed_tenant")
    //   actual_tenant    — the tenant the ENTITY actually carries (sec-eng AMBER amendment A1)
    //   correlation_id   — current Activity.Id when set, Guid fallback otherwise (sec-eng AMBER amendment A2)
    //
    // No entity-specific content (amounts, terminal-state strings, display
    // names) per ADR 0092 §A6 diagnostic non-leak invariant.
    private async ValueTask EmitTenantBoundaryViolationAsync(
        string entityId,
        TenantId requestedTenant,
        TenantId actualTenant,
        CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;

        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["entity_type"]       = "Invoice",
            ["entity_id"]         = entityId,
            ["requested_tenant"]  = requestedTenant.Value,
            ["actual_tenant"]     = actualTenant.Value,
            ["correlation_id"]    = correlationId,
        });
        var occurredAt = DateTimeOffset.UtcNow;
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _auditTenant,
            EventType: AuditEventType.TenantBoundaryViolation,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
