using System.Diagnostics;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Persistence;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

// ── Shared test doubles (mirrors InvoiceRepositoryTests template) ──────────

internal sealed class JournalStoreRecordingAuditTrail : IAuditTrail
{
    private readonly List<AuditRecord> _records = new();
    public IReadOnlyList<AuditRecord> Records => _records;

    public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        _records.Add(record);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var r in _records) yield return r;
        await Task.CompletedTask;
    }
}

internal sealed class JournalStorePassthroughSigner : IOperationSigner
{
    public PrincipalId IssuerId => default;

    public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
        => ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, default));
}

/// <summary>
/// Cohort-2 PR 0d — audit-emission regression tests for
/// <see cref="InMemoryJournalStore"/>. Mirrors the sec-eng AMBER amendment
/// A3 canonical template from PR 0a InvoiceRepositoryTests.
///
/// Canonical 5-field payload shape: entity_type, entity_id,
/// requested_tenant, actual_tenant, correlation_id.
/// </summary>
public sealed class InMemoryJournalStoreTests
{
    private static readonly TenantId Tenant = new("acme-ledger");
    private static readonly TenantId OtherTenant = new("other-ledger");
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();
    private static readonly GLAccountId AccountA = GLAccountId.NewId();
    private static readonly GLAccountId AccountB = GLAccountId.NewId();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static InMemoryJournalStore NewAuditingStore(out JournalStoreRecordingAuditTrail trail)
    {
        trail = new JournalStoreRecordingAuditTrail();
        return new InMemoryJournalStore(trail, new JournalStorePassthroughSigner(), Tenant);
    }

    private static JournalEntry NewPostedEntry(TenantId tenantId)
    {
        var lines = new[]
        {
            new JournalEntryLine(AccountA, debit: 100m, credit: 0m),
            new JournalEntryLine(AccountB, debit: 0m, credit: 100m),
        };
        return new JournalEntry(
            id: JournalEntryId.NewId(),
            tenantId: tenantId,
            entryDate: new DateOnly(2026, 5, 20),
            memo: "audit-test",
            lines: lines,
            createdAtUtc: Instant.Now)
            with { Status = JournalEntryStatus.Posted, ChartId = Chart };
    }

    private static AuditRecord SingleViolation(JournalStoreRecordingAuditTrail trail)
    {
        var violations = trail.Records
            .Where(r => r.EventType.Equals(AuditEventType.TenantBoundaryViolation))
            .ToList();
        Assert.Single(violations);
        return violations[0];
    }

    private static void AssertCanonicalPayload(
        AuditRecord record,
        string expectedEntityType,
        string expectedEntityId,
        TenantId expectedRequested,
        TenantId expectedActual)
    {
        var body = record.Payload.Payload.Body;
        Assert.Equal(expectedEntityType,         body["entity_type"]);
        Assert.Equal(expectedEntityId,           body["entity_id"]);
        Assert.Equal(expectedRequested.Value,    body["requested_tenant"]);
        Assert.Equal(expectedActual.Value,       body["actual_tenant"]);
        Assert.NotNull(body["correlation_id"]);
        Assert.IsType<string>(body["correlation_id"]);
        Assert.False(string.IsNullOrWhiteSpace((string)body["correlation_id"]!));
    }

    // ── Test 1 — SaveAtomicAsync cross-tenant throws + emits audit ────────────

    [Fact]
    public async Task SaveAtomicAsync_CrossTenant_ThrowsArgumentException_AndEmitsAudit()
    {
        var store = NewAuditingStore(out var trail);

        // Entry carries OtherTenant but caller passes Tenant → boundary violation.
        var entry = NewPostedEntry(OtherTenant);
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAtomicAsync(Tenant, entry));

        var violation = SingleViolation(trail);
        AssertCanonicalPayload(
            violation,
            expectedEntityType:  "JournalEntry",
            expectedEntityId:    entry.Id.Value,
            expectedRequested:   Tenant,
            expectedActual:      OtherTenant);
    }

    // ── Test 2 — Snapshot filters by tenant ──────────────────────────────────

    [Fact]
    public async Task Snapshot_FiltersByTenant()
    {
        var store = new InMemoryJournalStore();

        var tenantEntry = NewPostedEntry(Tenant);
        var otherEntry  = NewPostedEntry(OtherTenant);

        await store.SaveAtomicAsync(Tenant, tenantEntry);
        await store.SaveAtomicAsync(OtherTenant, otherEntry);

        var tenantSnap = store.Snapshot(Tenant);
        var otherSnap  = store.Snapshot(OtherTenant);

        Assert.Single(tenantSnap);
        Assert.Equal(tenantEntry.Id, tenantSnap[0].Id);

        Assert.Single(otherSnap);
        Assert.Equal(otherEntry.Id, otherSnap[0].Id);
    }

    // ── Test 3 — Audit emission picks up ambient Activity correlation id ──────

    [Fact]
    public async Task AuditEmission_PicksUpAmbientActivityCorrelationId()
    {
        var store = NewAuditingStore(out var trail);
        var entry = NewPostedEntry(OtherTenant);

        using var activity = new Activity("journal-test-correlation").Start();
        Assert.NotNull(activity.Id);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAtomicAsync(Tenant, entry));

        var violation = SingleViolation(trail);
        Assert.Equal(activity.Id, violation.Payload.Payload.Body["correlation_id"]);
    }

    // ── Test 4 — No audit trail wired → SaveAtomicAsync still throws, no NRE ─

    [Fact]
    public async Task AuditEmission_WithoutWiredAuditTrail_NoOp()
    {
        // Parameter-less ctor — no IAuditTrail, no IOperationSigner.
        // Cross-tenant write still throws ArgumentException but no NRE on the
        // missing audit wiring.
        var store = new InMemoryJournalStore();
        var entry = NewPostedEntry(OtherTenant);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAtomicAsync(Tenant, entry));
        // No exception other than the expected ArgumentException — no NRE.
    }

    // ── Test 5 — IJournalStore implements ITenantScopedRepository marker ──────

    [Fact]
    public void IJournalStore_ImplementsITenantScopedRepositoryMarker()
    {
        Assert.True(
            typeof(ITenantScopedRepository<JournalEntry, JournalEntryId>)
                .IsAssignableFrom(typeof(InMemoryJournalStore)));
    }

    // ── Test 6 — JournalEntry implements IMustHaveTenant ─────────────────────

    [Fact]
    public void JournalEntry_ImplementsIMustHaveTenant()
    {
        Assert.True(typeof(IMustHaveTenant).IsAssignableFrom(typeof(JournalEntry)));

        var lines = new[]
        {
            new JournalEntryLine(AccountA, debit: 50m, credit: 0m),
            new JournalEntryLine(AccountB, debit: 0m, credit: 50m),
        };
        var entry = new JournalEntry(
            id: JournalEntryId.NewId(),
            tenantId: Tenant,
            entryDate: new DateOnly(2026, 5, 20),
            memo: "implements-imusthavetenant",
            lines: lines,
            createdAtUtc: Instant.Now);

        IMustHaveTenant mustHaveTenant = entry;
        Assert.Equal(Tenant, mustHaveTenant.TenantId);
    }
}
