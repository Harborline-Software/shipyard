using System.Diagnostics;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPayments.Models;
using Sunfish.Blocks.FinancialPayments.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Blocks.FinancialPayments.Tests;

/// <summary>
/// Round-trip tests for <see cref="InMemoryPaymentApplicationRepository"/>.
/// Covers Add / Get / Delete / List and the boundary that balance updates
/// are NOT performed by the repository.
/// </summary>
public class PaymentApplicationRepositoryTests
{
    private static readonly TenantId TestTenant = new("tenant-test");
    private static readonly TenantId OtherTenant = new("tenant-other");

    private static PaymentApplication NewApp(
        PaymentId? paymentId = null,
        string? targetId = null,
        AppliedTo appliedTo = AppliedTo.Invoice,
        decimal amountApplied = 100m,
        TenantId? tenantOverride = null) =>
        PaymentApplication.Create(
            tenantId: tenantOverride ?? TestTenant,
            paymentId: paymentId ?? PaymentId.NewId(),
            appliedTo: appliedTo,
            targetId: targetId ?? "invoice-" + Guid.NewGuid(),
            amountApplied: amountApplied,
            appliedDate: new DateOnly(2026, 5, 18));

    [Fact]
    public async Task AddAndGet_RoundTrip()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp();

        await repo.AddAsync(TestTenant, app);
        var result = await repo.GetAsync(TestTenant, app.Id);

        Assert.NotNull(result);
        Assert.Equal(app.Id, result!.Id);
        Assert.Equal(app.AmountApplied, result.AmountApplied);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNotFound()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var result = await repo.GetAsync(TestTenant, PaymentApplicationId.NewId());
        Assert.Null(result);
    }

    [Fact]
    public async Task Add_ThrowsOnDuplicateId()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp();

        await repo.AddAsync(TestTenant, app);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AddAsync(TestTenant, app));
    }

    [Fact]
    public async Task Delete_RemovesRecord_ReturnsTrue()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp();
        await repo.AddAsync(TestTenant, app);

        var deleted = await repo.DeleteAsync(TestTenant, app.Id);

        Assert.True(deleted);
        Assert.Null(await repo.GetAsync(TestTenant, app.Id));
    }

    [Fact]
    public async Task Delete_IdempotentOnMissingId_ReturnsFalse()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var result = await repo.DeleteAsync(TestTenant, PaymentApplicationId.NewId());
        Assert.False(result);
    }

    [Fact]
    public async Task ListByPayment_ReturnsOnlyMatchingPayment()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var payA = PaymentId.NewId();
        var payB = PaymentId.NewId();

        await repo.AddAsync(TestTenant, NewApp(paymentId: payA));
        await repo.AddAsync(TestTenant, NewApp(paymentId: payA));
        await repo.AddAsync(TestTenant, NewApp(paymentId: payB));

        var results = await repo.ListByPaymentAsync(TestTenant, payA);
        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal(payA, a.PaymentId));
    }

    [Fact]
    public async Task ListByTarget_ReturnsOnlyMatchingTarget()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        const string targetId = "invoice-target-1";

        await repo.AddAsync(TestTenant, NewApp(targetId: targetId));
        await repo.AddAsync(TestTenant, NewApp(targetId: targetId));
        await repo.AddAsync(TestTenant, NewApp(targetId: "invoice-other"));

        var results = await repo.ListByTargetAsync(TestTenant, targetId);
        Assert.Equal(2, results.Count);
        Assert.All(results, a => Assert.Equal(targetId, a.TargetId));
    }

    [Fact]
    public async Task Repository_DoesNotUpdatePaymentBalance()
    {
        // Confirm the repository is a pure store: adding an application
        // does NOT alter any Payment record (balance updates belong to
        // IPaymentApplicationService in PR 3).
        var payRepo = new InMemoryPaymentRepository();
        var appRepo = new InMemoryPaymentApplicationRepository();

        var pmt = Payment.Create(
            tenantId: TestTenant,
            chartId: ChartOfAccountsId.NewId(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-001",
            partyId: new Sunfish.Blocks.People.Foundation.Models.PartyId("cust-1"),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 500m,
            method: PaymentMethod.ACH);

        await payRepo.AddAsync(TestTenant, pmt);
        var app = NewApp(paymentId: pmt.Id, amountApplied: 200m);
        await appRepo.AddAsync(TestTenant, app);

        // Payment in payRepo is UNCHANGED — 500m UnappliedAmount still 500m
        var reloaded = await payRepo.GetAsync(TestTenant, pmt.Id);
        Assert.Equal(500m, reloaded!.UnappliedAmount);
    }

    // ── Cohort-2 PR 0c tenant-keying tests (pattern-009-tenant-keying-retrofit candidate) ──

    [Fact]
    public async Task GetAsync_CrossTenant_ReturnsNull()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp(amountApplied: 50m, tenantOverride: TestTenant);
        await repo.AddAsync(TestTenant, app);

        var crossTenantRead = await repo.GetAsync(OtherTenant, app.Id);
        Assert.Null(crossTenantRead);
    }

    [Fact]
    public async Task ListByPaymentAsync_FiltersByTenant()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var paymentA = PaymentId.NewId();
        var paymentB = PaymentId.NewId();

        await repo.AddAsync(TestTenant, NewApp(paymentId: paymentA, tenantOverride: TestTenant));
        await repo.AddAsync(OtherTenant, NewApp(paymentId: paymentB, tenantOverride: OtherTenant));

        var tenantAApps = await repo.ListByPaymentAsync(TestTenant, paymentA);
        Assert.Single(tenantAApps);
        Assert.All(tenantAApps, a => Assert.Equal(TestTenant, a.TenantId));

        var crossList = await repo.ListByPaymentAsync(TestTenant, paymentB);
        Assert.Empty(crossList);
    }

    [Fact]
    public async Task AddAsync_TenantMismatch_ThrowsArgumentException()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp(tenantOverride: TestTenant);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.AddAsync(OtherTenant, app));
    }

    [Fact]
    public async Task DeleteAsync_CrossTenant_ReturnsFalse()
    {
        var repo = new InMemoryPaymentApplicationRepository();
        var app = NewApp(tenantOverride: TestTenant);
        await repo.AddAsync(TestTenant, app);

        var crossDelete = await repo.DeleteAsync(OtherTenant, app.Id);
        Assert.False(crossDelete);
        Assert.NotNull(await repo.GetAsync(TestTenant, app.Id));
    }

    [Fact]
    public async Task PaymentRepo_GetAsync_CrossTenant_ReturnsNull()
    {
        var repo = new InMemoryPaymentRepository();
        var pmt = Payment.Create(
            tenantId: TestTenant,
            chartId: ChartOfAccountsId.NewId(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-CROSS",
            partyId: new Sunfish.Blocks.People.Foundation.Models.PartyId("cust-cross"),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.ACH);
        await repo.AddAsync(TestTenant, pmt);

        var crossRead = await repo.GetAsync(OtherTenant, pmt.Id);
        Assert.Null(crossRead);
    }

    [Fact]
    public async Task PaymentRepo_AddAsync_TenantMismatch_ThrowsArgumentException()
    {
        var repo = new InMemoryPaymentRepository();
        var pmt = Payment.Create(
            tenantId: TestTenant,
            chartId: ChartOfAccountsId.NewId(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-MIS",
            partyId: new Sunfish.Blocks.People.Foundation.Models.PartyId("cust-mis"),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.ACH);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.AddAsync(OtherTenant, pmt));
    }

    [Fact]
    public void IPaymentRepository_ImplementsITenantScopedRepositoryMarker()
    {
        var marker = typeof(Sunfish.Foundation.Persistence.ITenantScopedRepository<Payment, PaymentId>);
        Assert.True(marker.IsAssignableFrom(typeof(IPaymentRepository)));
    }

    [Fact]
    public void IPaymentApplicationRepository_ImplementsITenantScopedRepositoryMarker()
    {
        var marker = typeof(Sunfish.Foundation.Persistence.ITenantScopedRepository<PaymentApplication, PaymentApplicationId>);
        Assert.True(marker.IsAssignableFrom(typeof(IPaymentApplicationRepository)));
    }

    // ── Audit-emission regression tests (sec-eng AMBER amendment A3 — ADR 0092 §A6) ──
    //
    // Mirrors the cohort 2 PR 0a InvoiceRepositoryTests canonical payload shape:
    //   entity_type, entity_id, requested_tenant, actual_tenant, correlation_id

    private static AuditRecord SingleViolation(RecordingAuditTrail trail)
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
        Assert.Equal(expectedEntityType, body["entity_type"]);
        Assert.Equal(expectedEntityId,   body["entity_id"]);
        Assert.Equal(expectedRequested.Value, body["requested_tenant"]);
        Assert.Equal(expectedActual.Value,    body["actual_tenant"]);
        Assert.NotNull(body["correlation_id"]);
        Assert.IsType<string>(body["correlation_id"]);
        Assert.False(string.IsNullOrWhiteSpace((string)body["correlation_id"]!));
    }

    [Fact]
    public async Task PaymentRepo_GetAsync_CrossTenant_EmitsAuditWithCanonicalPayload()
    {
        var trail = new RecordingAuditTrail();
        var repo = new InMemoryPaymentRepository(trail, new PassthroughSigner(), TestTenant);
        var pmt = Payment.Create(
            tenantId: TestTenant,
            chartId: ChartOfAccountsId.NewId(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-AUDIT-A",
            partyId: new Sunfish.Blocks.People.Foundation.Models.PartyId("cust-a"),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.ACH);
        await repo.AddAsync(TestTenant, pmt);

        Assert.Null(await repo.GetAsync(OtherTenant, pmt.Id));

        var violation = SingleViolation(trail);
        AssertCanonicalPayload(violation, "Payment", pmt.Id.Value, OtherTenant, TestTenant);
    }

    [Fact]
    public async Task PaymentRepo_UpdateAsync_CrossTenantExistingRow_EmitsAudit()
    {
        var trail = new RecordingAuditTrail();
        var repo = new InMemoryPaymentRepository(trail, new PassthroughSigner(), TestTenant);
        var pmt = Payment.Create(
            tenantId: TestTenant,
            chartId: ChartOfAccountsId.NewId(),
            direction: PaymentDirection.Inbound,
            paymentNumber: "PMT-AUDIT-B",
            partyId: new Sunfish.Blocks.People.Foundation.Models.PartyId("cust-b"),
            paymentDate: new DateOnly(2026, 5, 18),
            amount: 100m,
            method: PaymentMethod.ACH);
        await repo.AddAsync(TestTenant, pmt);

        var shadow = pmt with { TenantId = OtherTenant };
        await Assert.ThrowsAsync<ArgumentException>(() => repo.UpdateAsync(OtherTenant, shadow));

        var violation = SingleViolation(trail);
        AssertCanonicalPayload(violation, "Payment", pmt.Id.Value, OtherTenant, TestTenant);
    }

    [Fact]
    public async Task ApplicationRepo_GetAsync_CrossTenant_EmitsAuditWithCanonicalPayload()
    {
        var trail = new RecordingAuditTrail();
        var repo = new InMemoryPaymentApplicationRepository(trail, new PassthroughSigner(), TestTenant);
        var app = NewApp(tenantOverride: TestTenant);
        await repo.AddAsync(TestTenant, app);

        Assert.Null(await repo.GetAsync(OtherTenant, app.Id));

        var violation = SingleViolation(trail);
        AssertCanonicalPayload(violation, "PaymentApplication", app.Id.Value, OtherTenant, TestTenant);
    }

    [Fact]
    public async Task ApplicationRepo_DeleteAsync_CrossTenant_EmitsAudit()
    {
        var trail = new RecordingAuditTrail();
        var repo = new InMemoryPaymentApplicationRepository(trail, new PassthroughSigner(), TestTenant);
        var app = NewApp(tenantOverride: TestTenant);
        await repo.AddAsync(TestTenant, app);

        Assert.False(await repo.DeleteAsync(OtherTenant, app.Id));

        var violation = SingleViolation(trail);
        AssertCanonicalPayload(violation, "PaymentApplication", app.Id.Value, OtherTenant, TestTenant);
    }

    [Fact]
    public async Task AuditEmission_PicksUpAmbientActivityCorrelationId()
    {
        var trail = new RecordingAuditTrail();
        var repo = new InMemoryPaymentApplicationRepository(trail, new PassthroughSigner(), TestTenant);
        var app = NewApp(tenantOverride: TestTenant);
        await repo.AddAsync(TestTenant, app);

        using var activity = new Activity("test-correlation").Start();
        Assert.NotNull(activity.Id);

        Assert.Null(await repo.GetAsync(OtherTenant, app.Id));

        var violation = SingleViolation(trail);
        Assert.Equal(activity.Id, violation.Payload.Payload.Body["correlation_id"]);
    }
}

internal sealed class RecordingAuditTrail : IAuditTrail
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

internal sealed class PassthroughSigner : IOperationSigner
{
    public PrincipalId IssuerId => default;

    public ValueTask<SignedOperation<T>> SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)
    {
        return ValueTask.FromResult(new SignedOperation<T>(payload, IssuerId, issuedAt, nonce, default));
    }
}
