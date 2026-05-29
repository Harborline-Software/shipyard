using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.FinancialPayments.Services;

/// <summary>
/// In-memory <see cref="IPaymentRepository"/>. State lives in a
/// <c>ConcurrentDictionary</c> keyed by <see cref="PaymentId"/>; secondary
/// queries scan the values — acceptable for the in-memory v1 path with
/// O(payments-per-tenant) complexity. A SQLite-backed implementation lands in
/// the follow-on substrate hand-off and shadows this binding.
///
/// <para>
/// <b>Cohort-2 PR 0c tenant-keying retrofit.</b> Every <c>Get*</c> / <c>List*</c>
/// filters by the <c>tenantId</c> argument; rows belonging to a different
/// tenant are treated as not-found (uniform-404 per ADR 0092). When audit
/// emission is wired, a cross-tenant <c>Get</c> hit emits
/// <c>AuditEventType.TenantBoundaryViolation</c> before returning null.
/// Writes (<c>AddAsync</c> / <c>UpdateAsync</c>) assert
/// <c>payment.TenantId == tenantId</c>; mismatch throws
/// <see cref="ArgumentException"/>.
/// </para>
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<PaymentId, Payment> _payments = new();
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _auditTenant;

    /// <summary>Creates the repository without audit emission (tests, demos).</summary>
    public InMemoryPaymentRepository()
    {
    }

    /// <summary>
    /// Creates the repository with audit emission wired through
    /// <paramref name="auditTrail"/> + <paramref name="signer"/>;
    /// <paramref name="auditTenant"/> is the tenant attribution applied to
    /// emitted records.
    /// </summary>
    public InMemoryPaymentRepository(IAuditTrail auditTrail, IOperationSigner signer, TenantId auditTenant)
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
    public Task AddAsync(TenantId tenantId, Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment is null) throw new ArgumentNullException(nameof(payment));
        if (!payment.TenantId.Equals(tenantId))
        {
            throw new ArgumentException(
                $"Payment '{payment.Id.Value}' carries TenantId '{payment.TenantId.Value}' but caller passed tenantId '{tenantId.Value}'.",
                nameof(payment));
        }
        if (!_payments.TryAdd(payment.Id, payment))
            throw new InvalidOperationException($"Payment '{payment.Id.Value}' already exists.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Payment?> GetAsync(TenantId tenantId, PaymentId id, CancellationToken cancellationToken = default)
    {
        if (!_payments.TryGetValue(id, out var payment)) return null;
        if (!payment.TenantId.Equals(tenantId))
        {
            await EmitTenantBoundaryViolationAsync(id.Value, tenantId, payment.TenantId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        return payment;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TenantId tenantId, Payment payment, CancellationToken cancellationToken = default)
    {
        if (payment is null) throw new ArgumentNullException(nameof(payment));
        if (!payment.TenantId.Equals(tenantId))
        {
            throw new ArgumentException(
                $"Payment '{payment.Id.Value}' carries TenantId '{payment.TenantId.Value}' but caller passed tenantId '{tenantId.Value}'.",
                nameof(payment));
        }
        if (!_payments.TryGetValue(payment.Id, out var existing))
            throw new InvalidOperationException($"Payment '{payment.Id.Value}' not found; cannot update.");
        if (!existing.TenantId.Equals(tenantId))
        {
            await EmitTenantBoundaryViolationAsync(payment.Id.Value, tenantId, existing.TenantId, cancellationToken).ConfigureAwait(false);
            throw new ArgumentException(
                $"Payment id '{payment.Id.Value}' already exists under a different tenant.",
                nameof(payment));
        }
        _payments[payment.Id] = payment;
    }

    /// <inheritdoc />
    public Task<Payment?> GetByExternalRefAsync(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        string externalRef,
        CancellationToken cancellationToken = default)
    {
        var hit = _payments.Values.FirstOrDefault(p =>
            p.TenantId.Equals(tenantId)
            && p.ChartId == chartId
            && string.Equals(p.ExternalRef, externalRef, StringComparison.Ordinal));
        return Task.FromResult<Payment?>(hit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Payment>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        var rows = _payments.Values
            .Where(p => p.TenantId.Equals(tenantId) && p.ChartId == chartId)
            .OrderByDescending(p => p.PaymentDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Payment>> ListByPartyAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId partyId, CancellationToken cancellationToken = default)
    {
        var rows = _payments.Values
            .Where(p => p.TenantId.Equals(tenantId) && p.ChartId == chartId && p.PartyId == partyId)
            .OrderByDescending(p => p.PaymentDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<Payment>>(rows);
    }

    // ── Audit emission (ADR 0092 §A6 canonical payload shape — sec-eng SPOT-CHECK GREEN template) ──
    //
    // Mirror of cohort 2 PR 0a InMemoryInvoiceRepository payload shape:
    //   entity_type, entity_id, requested_tenant, actual_tenant, correlation_id
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
            ["entity_type"]       = "Payment",
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
