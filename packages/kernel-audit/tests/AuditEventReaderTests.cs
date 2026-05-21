using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Audit.DependencyInjection;

namespace Sunfish.Kernel.Audit.Tests;

/// <summary>
/// Tests for <see cref="IAuditEventReader"/> + <see cref="InMemoryAuditEventReader"/>.
/// Covers per-spec test cases from ADR 0094 §Compatibility plan plus
/// forward-watch items FW1 (audit-payload field count) and FW2 (DI lifetime
/// assertion).
/// </summary>
public sealed class AuditEventReaderTests
{
    // ── Harness ───────────────────────────────────────────────────────────────

    private static (
        InMemoryAuditTrail trail,
        IAuditEventReader reader,
        IOperationSigner signer,
        KeyPair keys,
        ServiceProvider sp) BuildHarness()
    {
        var keys = KeyPair.Generate();
        var signer = new Ed25519Signer(keys);

        var sp = new ServiceCollection()
            .AddSingleton<IOperationSigner>(signer)
            .AddSunfishKernelAuditReaderInMemory()
            .BuildServiceProvider();

        // Resolve from a scope so Scoped registrations live in the same scope.
        var scope = sp.CreateScope();

        return (
            scope.ServiceProvider.GetRequiredService<InMemoryAuditTrail>(),
            scope.ServiceProvider.GetRequiredService<IAuditEventReader>(),
            signer,
            keys,
            sp);
    }

    private static async Task<AuditRecord> MakeRecordAsync(
        IOperationSigner signer,
        TenantId tenantId,
        AuditEventType eventType,
        DateTimeOffset? occurredAt = null,
        Guid? auditId = null)
    {
        var at = occurredAt ?? DateTimeOffset.UtcNow;
        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["note"] = "test",
        });
        var signed = await signer.SignAsync(payload, at, Guid.NewGuid());
        return new AuditRecord(
            AuditId: auditId ?? Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: at,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
    }

    // ── TC-1: GetByIdAsync happy path ─────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_TenantMatch_ReturnsRecord()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-a");
            var record = await MakeRecordAsync(signer, tenantId, AuditEventType.KeyRecoveryCompleted);
            await trail.AppendAsync(record);

            var result = await reader.GetByIdAsync(tenantId, record.AuditId);

            Assert.NotNull(result);
            Assert.Equal(record.AuditId, result!.AuditId);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── TC-2: GetByIdAsync cross-tenant returns null + emits violation ────────

    [Fact]
    public async Task GetByIdAsync_TenantMismatch_ReturnsNullAndEmitsTenantBoundaryViolation()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var ownerTenant = new TenantId("owner-tenant");
            var callerTenant = new TenantId("caller-tenant");

            var record = await MakeRecordAsync(signer, ownerTenant, AuditEventType.KeyRecoveryCompleted);
            await trail.AppendAsync(record);

            var countBefore = trail.Count;
            var result = await reader.GetByIdAsync(callerTenant, record.AuditId);

            // Uniform-empty per ADR 0092 §A3.
            Assert.Null(result);

            // TenantBoundaryViolation MUST be emitted per ADR 0092 §A6.
            Assert.Equal(countBefore + 1, trail.Count);
            var snapshot = trail.Snapshot();
            var violation = snapshot.LastOrDefault();
            Assert.NotNull(violation);
            Assert.Equal(AuditEventType.TenantBoundaryViolation, violation!.EventType);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── TC-3: ListAsync reverse-chronological tenant-scoped results ───────────

    [Fact]
    public async Task ListAsync_TenantMatch_ReturnsReverseChronologicalPage()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantA = new TenantId("tenant-a");
            var tenantB = new TenantId("tenant-b");
            var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Append 3 records for tenant-a and 1 for tenant-b.
            var r1 = await MakeRecordAsync(signer, tenantA, AuditEventType.KeyRecoveryInitiated, t0);
            var r2 = await MakeRecordAsync(signer, tenantA, AuditEventType.KeyRecoveryAttested, t0.AddMinutes(1));
            var r3 = await MakeRecordAsync(signer, tenantA, AuditEventType.KeyRecoveryCompleted, t0.AddMinutes(2));
            var rB = await MakeRecordAsync(signer, tenantB, AuditEventType.KeyRecoveryInitiated, t0.AddMinutes(5));

            await trail.AppendAsync(r1);
            await trail.AppendAsync(r2);
            await trail.AppendAsync(r3);
            await trail.AppendAsync(rB);

            var query = new AuditEventReaderQuery(PageSize: 10);
            var page = await reader.ListAsync(tenantA, query);

            // Only tenant-a's records.
            Assert.Equal(3, page.Records.Count);
            Assert.All(page.Records, r => Assert.Equal(tenantA, r.TenantId));

            // Reverse-chronological order.
            Assert.Equal(r3.AuditId, page.Records[0].AuditId);
            Assert.Equal(r2.AuditId, page.Records[1].AuditId);
            Assert.Equal(r1.AuditId, page.Records[2].AuditId);

            Assert.False(page.HasMore);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── TC-4: Tie-OccurredAt cursor walks both records (ADR 0094 Amendment 2.3) ─

    [Fact]
    public async Task ListAsync_TieOccurredAt_CursorWalksBoth()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-x");
            // Same OccurredAt, different AuditIds — cursor tie-breaker is load-bearing.
            var sharedAt = new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);
            var idA = Guid.NewGuid();
            var idB = Guid.NewGuid();

            var rA = await MakeRecordAsync(signer, tenantId, AuditEventType.KeyRecoveryInitiated, sharedAt, idA);
            var rB = await MakeRecordAsync(signer, tenantId, AuditEventType.KeyRecoveryAttested, sharedAt, idB);

            await trail.AppendAsync(rA);
            await trail.AppendAsync(rB);

            // Page size 1 — first page should return the "higher" AuditId (DESC),
            // second page should return the "lower" AuditId via the tie-breaker.
            var page1 = await reader.ListAsync(tenantId, new AuditEventReaderQuery(PageSize: 1));
            Assert.Single(page1.Records);
            Assert.True(page1.HasMore, "HasMore must be true when there is a second record");
            Assert.NotNull(page1.NextCursor);

            var page2 = await reader.ListAsync(tenantId, new AuditEventReaderQuery(
                PageSize: 1,
                Cursor: page1.NextCursor));
            Assert.Single(page2.Records);
            Assert.False(page2.HasMore, "No more records after the second");

            // Together both AuditIds are covered — no silent drop.
            var seenIds = new HashSet<Guid>
            {
                page1.Records[0].AuditId,
                page2.Records[0].AuditId,
            };
            Assert.Contains(idA, seenIds);
            Assert.Contains(idB, seenIds);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── TC-5: StreamAsync ignores PageSize/Cursor and streams all records ─────

    [Fact]
    public async Task StreamAsync_TenantMatch_StreamsAllMatchingRecords()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-s");
            var t0 = DateTimeOffset.UtcNow;

            for (var i = 0; i < 5; i++)
            {
                await trail.AppendAsync(
                    await MakeRecordAsync(signer, tenantId, AuditEventType.KeyRecoveryInitiated,
                        t0.AddSeconds(i)));
            }

            // PageSize=1 and a cursor from page1 should be ignored by StreamAsync.
            var page1 = await reader.ListAsync(tenantId, new AuditEventReaderQuery(PageSize: 1));
            var query = new AuditEventReaderQuery(PageSize: 1, Cursor: page1.NextCursor);

            var streamed = new List<AuditRecord>();
            await foreach (var r in reader.StreamAsync(tenantId, query))
            {
                streamed.Add(r);
            }

            // StreamAsync should return all 5 records, ignoring the cursor.
            Assert.Equal(5, streamed.Count);
            Assert.All(streamed, r => Assert.Equal(tenantId, r.TenantId));
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── TC-6: Cross-tenant cursor reuse returns uniform-empty ─────────────────

    [Fact]
    public async Task ListAsync_CrossTenantCursor_ReturnsEmptyPage()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantA = new TenantId("tenant-a");
            var tenantB = new TenantId("tenant-b");
            var t0 = DateTimeOffset.UtcNow;

            // Add records for tenantA so page1 generates a cursor.
            for (var i = 0; i < 3; i++)
            {
                await trail.AppendAsync(
                    await MakeRecordAsync(signer, tenantA, AuditEventType.KeyRecoveryInitiated,
                        t0.AddSeconds(i)));
            }

            var page1 = await reader.ListAsync(tenantA, new AuditEventReaderQuery(PageSize: 1));
            Assert.NotNull(page1.NextCursor);

            // Reuse tenantA's cursor while calling as tenantB.
            // Cursor.TenantId == tenantA != tenantB → uniform-empty per ADR 0094 §AuditEventCursor.
            var crossPage = await reader.ListAsync(tenantB, new AuditEventReaderQuery(
                PageSize: 10,
                Cursor: page1.NextCursor));

            Assert.Empty(crossPage.Records);
            Assert.Null(crossPage.NextCursor);
            Assert.False(crossPage.HasMore);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── FW1: Audit-emission payload carries all 5 canonical fields ────────────

    [Fact]
    public async Task FW1_GetByIdAsync_TenantMismatch_EmissionPayloadHasFiveCanonicalFields()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var ownerTenant = new TenantId("fw1-owner");
            var callerTenant = new TenantId("fw1-caller");

            var record = await MakeRecordAsync(signer, ownerTenant, AuditEventType.KeyRecoveryCompleted);
            await trail.AppendAsync(record);

            await reader.GetByIdAsync(callerTenant, record.AuditId);

            var snapshot = trail.Snapshot();
            var violation = snapshot
                .First(r => r.EventType == AuditEventType.TenantBoundaryViolation);

            var body = violation.Payload.Payload.Body;

            // FW1: canonical 5-field payload per ADR 0094 §Decision drivers +
            // ADR 0092 §A6 field-count reconciliation (forward-watch item FW2
            // from the sec-eng GREEN verdict).
            Assert.True(body.ContainsKey("entity_type"),      "missing entity_type");
            Assert.True(body.ContainsKey("entity_id"),        "missing entity_id");
            Assert.True(body.ContainsKey("requested_tenant"), "missing requested_tenant");
            Assert.True(body.ContainsKey("actual_tenant"),    "missing actual_tenant");
            Assert.True(body.ContainsKey("correlation_id"),   "missing correlation_id");

            Assert.Equal(5, body.Count);

            // Values are non-null and non-empty.
            Assert.Equal("AuditRecord",          body["entity_type"]?.ToString());
            Assert.Equal(record.AuditId.ToString("D"), body["entity_id"]?.ToString());
            Assert.Equal(callerTenant.Value,     body["requested_tenant"]?.ToString());
            Assert.Equal(ownerTenant.Value,      body["actual_tenant"]?.ToString());
            Assert.False(string.IsNullOrEmpty(body["correlation_id"]?.ToString()),
                "correlation_id must be a non-empty string");
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── FW2: DI lifetime assertion fires when InMemoryAuditTrail is Transient ──

    [Fact]
    public void FW2_DI_ValidateInMemoryAuditTrailLifetime_ThrowsWhenTransient()
    {
        var services = new ServiceCollection();

        // Intentionally register InMemoryAuditTrail as Transient — the
        // assertion should catch this and throw.
        services.AddTransient<InMemoryAuditTrail>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ServiceCollectionExtensions.ValidateInMemoryAuditTrailLifetime(services));

        Assert.Contains("Transient", ex.Message);
        Assert.Contains("ADR 0094", ex.Message);
    }

    // ── TC-7: GetByIdAsync not-found returns null without emission ────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNullNoEmission()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-nf");
            var countBefore = trail.Count;

            var result = await reader.GetByIdAsync(tenantId, Guid.NewGuid());

            Assert.Null(result);
            // No emission for not-found (absence is not a probe signal per ADR 0094).
            Assert.Equal(countBefore, trail.Count);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }

    // ── TC-8: StreamAsync cancellation ends enumeration ───────────────────────

    [Fact]
    public async Task StreamAsync_Cancelled_EndsEnumeration()
    {
        var (trail, reader, signer, keys, sp) = BuildHarness();
        try
        {
            var tenantId = new TenantId("tenant-cancel");
            var t0 = DateTimeOffset.UtcNow;

            for (var i = 0; i < 5; i++)
            {
                await trail.AppendAsync(
                    await MakeRecordAsync(signer, tenantId, AuditEventType.KeyRecoveryInitiated,
                        t0.AddSeconds(i)));
            }

            using var cts = new CancellationTokenSource();
            var streamed = new List<AuditRecord>();

            // Cancel after the first record.
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var r in reader.StreamAsync(tenantId, new AuditEventReaderQuery(), cts.Token))
                {
                    streamed.Add(r);
                    cts.Cancel();
                }
            });

            // We got at least one record before cancellation.
            Assert.True(streamed.Count >= 1);
            // We did NOT get all 5.
            Assert.True(streamed.Count < 5);
        }
        finally { keys.Dispose(); sp.Dispose(); }
    }
}
