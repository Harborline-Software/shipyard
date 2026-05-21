using System.Collections.Immutable;
using System.Diagnostics;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Persistence;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Atomic write boundary for journal entries. Implementations wrap the
/// underlying persistence (SQLite in production) and surface a
/// commit-or-rollback semantic the
/// <see cref="JournalPostingService"/> can drive without knowing the
/// storage details.
///
/// <para>
/// <b>Cohort-2 PR 0d tenant-keying retrofit (pattern-009-tenant-keying-retrofit
/// 4th instance + ratification trigger; ADR 0092 Step 1).</b> Every method
/// takes <see cref="TenantId"/> as the FIRST positional parameter
/// (analyzer-enforced at ADR 0092 Step 4c). Write methods assert
/// <c>entry.TenantId == tenantId</c> at the boundary; mismatch throws
/// <see cref="ArgumentException"/>.
/// </para>
/// </summary>
public interface IJournalStore : ITenantScopedRepository<JournalEntry, JournalEntryId>
{
    /// <summary>
    /// Persist <paramref name="entry"/> + its lines as a single atomic
    /// unit. Throws on any failure — implementations MUST roll back any
    /// partial writes before propagating.
    /// <see cref="ArgumentException"/> when <c>entry.TenantId</c> does not
    /// match <paramref name="tenantId"/>.
    /// </summary>
    Task SaveAtomicAsync(TenantId tenantId, JournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Snapshot of persisted entries for <paramref name="tenantId"/> — for
    /// test assertions about rollback / partial-write behaviour.
    /// Cross-tenant rows are filtered out (uniform-empty per ADR 0092 §A3).
    /// Implementations may return a defensive copy.
    /// </summary>
    IReadOnlyList<JournalEntry> Snapshot(TenantId tenantId);
}

/// <summary>
/// In-memory <see cref="IJournalStore"/>. Saves to a backing list;
/// supports an injected failure-trigger predicate so tests can induce
/// commit-time exceptions to exercise the rollback path.
///
/// <para>
/// Cohort-2 PR 0d tenant-keying retrofit. Writes assert
/// <c>entry.TenantId == tenantId</c>; mismatch throws
/// <see cref="ArgumentException"/>. <see cref="Snapshot"/> filters by tenant.
/// Cross-tenant writes against an existing-id row emit
/// <c>AuditEventType.TenantBoundaryViolation</c> when audit emission is wired.
/// </para>
/// </summary>
public sealed class InMemoryJournalStore : IJournalStore
{
    private readonly List<JournalEntry> _entries = new();
    private readonly object _gate = new();
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _auditTenant;

    /// <summary>Creates the store without audit emission (tests, demos).</summary>
    public InMemoryJournalStore()
    {
    }

    /// <summary>
    /// Creates the store with audit emission wired through
    /// <paramref name="auditTrail"/> + <paramref name="signer"/>;
    /// <paramref name="auditTenant"/> is the tenant attribution applied to
    /// emitted records.
    /// </summary>
    public InMemoryJournalStore(IAuditTrail auditTrail, IOperationSigner signer, TenantId auditTenant)
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

    /// <summary>
    /// If set, invoked before each save. Returning <c>true</c> raises
    /// an <see cref="InvalidOperationException"/> simulating a
    /// commit-time storage failure.
    /// </summary>
    public Func<JournalEntry, bool>? FailIf { get; set; }

    /// <inheritdoc />
    public async Task SaveAtomicAsync(TenantId tenantId, JournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.TenantId.Equals(tenantId))
        {
            await EmitTenantBoundaryViolationAsync(entry.Id.Value, tenantId, entry.TenantId, cancellationToken).ConfigureAwait(false);
            throw new ArgumentException(
                $"JournalEntry '{entry.Id.Value}' carries TenantId '{entry.TenantId.Value}' but caller passed tenantId '{tenantId.Value}'.",
                nameof(entry));
        }

        if (FailIf is not null && FailIf(entry))
        {
            // Simulate a mid-commit storage failure. NO partial state
            // mutation — the list is unchanged.
            throw new InvalidOperationException("InMemoryJournalStore: induced failure for rollback test.");
        }

        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<JournalEntry> Snapshot(TenantId tenantId)
    {
        lock (_gate)
        {
            return _entries.Where(e => e.TenantId.Equals(tenantId)).ToList();
        }
    }

    // ── Audit emission (ADR 0092 §A6 canonical payload shape — cohort-2 PR 0a sec-eng GREEN template) ──
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
            ["entity_type"]       = "JournalEntry",
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
