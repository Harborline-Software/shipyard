using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.Forms;
using Sunfish.Foundation.Forms.Models;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Kernel.Signatures.Models;
using Xunit;
using PartyId = Sunfish.Blocks.People.Foundation.Models.PartyId;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public class ShipsOfficeProviderTests
{
    private static readonly TenantId TenantA = new("alpha");

    private static ShipsOfficeDataProvider Build(
        ShipsOfficeOptions? options = null,
        IBundleCatalog? bundleCatalog = null,
        ILeaseService? leaseService = null,
        ILeaseDocumentVersionLog? leaseDocLog = null,
        IMaintenanceService? maintenanceService = null,
        IW9DocumentService? w9Service = null,
        IFormDefinitionStore? formDefinitions = null,
        IMissionEnvelopeProvider? missionEnvelopeProvider = null,
        TimeProvider? timeProvider = null)
    {
        bundleCatalog ??= EmptyBundleCatalog();
        leaseService ??= EmptyLeaseService();
        leaseDocLog ??= Substitute.For<ILeaseDocumentVersionLog>();
        maintenanceService ??= EmptyMaintenanceService();
        w9Service ??= Substitute.For<IW9DocumentService>();
        formDefinitions ??= new NoopFormDefinitionStore();
        missionEnvelopeProvider ??= NoopMissionEnvelopeProvider();

        return new ShipsOfficeDataProvider(
            bundleCatalog,
            leaseService,
            leaseDocLog,
            maintenanceService,
            w9Service,
            formDefinitions,
            missionEnvelopeProvider,
            Options.Create(options ?? new ShipsOfficeOptions()),
            timeProvider);
    }

    private static IBundleCatalog EmptyBundleCatalog()
    {
        var catalog = Substitute.For<IBundleCatalog>();
        catalog.GetBundles().Returns(Array.Empty<BusinessCaseBundleManifest>());
        return catalog;
    }

    private static ILeaseService EmptyLeaseService()
    {
        var svc = Substitute.For<ILeaseService>();
        svc.ListAsync(Arg.Any<ListLeasesQuery>(), Arg.Any<CancellationToken>())
           .Returns(AsyncEnumerable.Empty<Lease>());
        return svc;
    }

    private static IMaintenanceService EmptyMaintenanceService()
    {
        var svc = Substitute.For<IMaintenanceService>();
        svc.ListVendorsAsync(Arg.Any<ListVendorsQuery>(), Arg.Any<CancellationToken>())
           .Returns(AsyncEnumerable.Empty<Vendor>());
        return svc;
    }

    private static IMissionEnvelopeProvider NoopMissionEnvelopeProvider()
        => Substitute.For<IMissionEnvelopeProvider>();

    private static FormDefinition NewDefinition(
        string id,
        FormDefinitionStatus status,
        DateTimeOffset updatedAt,
        string? title = null,
        int major = 1,
        int minor = 0,
        int patch = 0,
        IdentityRef? owner = null)
    {
        var overlay = title is null
            ? SunfishOverlay.Empty
            : SunfishOverlay.Empty with { Title = InternationalizedText.FromInvariant(title) };
        return new FormDefinition(
            Id: new FormDefinitionId(id),
            Version: new SemanticVersion(major, minor, patch),
            Status: status,
            Tenant: TenantA,
            Owner: owner ?? IdentityRef.System,
            SchemaRef: new SchemaId($"sha256:test-{id}-{major}.{minor}.{patch}"),
            Overlay: overlay,
            Lineage: null,
            CreatedAt: updatedAt,
            UpdatedAt: updatedAt);
    }

    // ── Snapshot: empty baseline ──────────────────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_ReturnsEmptyDocuments_WhenNoSources()
    {
        var snapshot = await Build().GetSnapshotAsync(TenantA);
        Assert.Empty(snapshot.Documents);
        Assert.Equal(0, snapshot.TotalCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_Cancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build().GetSnapshotAsync(TenantA, cts.Token));
    }

    // ── Snapshot: BundleManifest projection ──────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_BundleManifest_ProjectsCorrectly()
    {
        var manifest = new BusinessCaseBundleManifest
        {
            Key = "sunfish.bundles.test",
            Name = "Test Bundle",
            Version = "1.0.0",
            Status = BundleStatus.GA,
        };
        var catalog = Substitute.For<IBundleCatalog>();
        catalog.GetBundles().Returns(new[] { manifest });

        var snapshot = await Build(bundleCatalog: catalog).GetSnapshotAsync(TenantA);

        var view = Assert.Single(snapshot.Documents);
        Assert.Equal(ShipsOfficeDocumentKind.BundleManifest, view.Kind);
        Assert.Equal("Test Bundle", view.Title);
        Assert.Equal(DocumentStatus.Published, view.Status);
        Assert.Equal("1.0.0", view.VersionLabel);
        Assert.Equal(ActorId.Sunfish, view.LastModifiedBy);
        Assert.Equal($"bundle:{manifest.Key}", view.Id.Value);
    }

    [Theory]
    [InlineData(BundleStatus.Draft, DocumentStatus.Draft)]
    [InlineData(BundleStatus.Preview, DocumentStatus.Published)]
    [InlineData(BundleStatus.GA, DocumentStatus.Published)]
    [InlineData(BundleStatus.Deprecated, DocumentStatus.Archived)]
    public async Task GetSnapshotAsync_BundleManifest_MapsStatusCorrectly(
        BundleStatus bundleStatus, DocumentStatus expectedDocStatus)
    {
        var manifest = new BusinessCaseBundleManifest { Key = "k", Name = "N", Version = "1.0", Status = bundleStatus };
        var catalog = Substitute.For<IBundleCatalog>();
        catalog.GetBundles().Returns(new[] { manifest });

        var snapshot = await Build(bundleCatalog: catalog).GetSnapshotAsync(TenantA);
        Assert.Equal(expectedDocStatus, Assert.Single(snapshot.Documents).Status);
    }

    // ── Snapshot: LeaseDocument projection ───────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_LeaseDocument_ProjectsLatestVersion()
    {
        var leaseId = LeaseId.NewId();
        var versionId = new LeaseDocumentVersionId(Guid.NewGuid());
        var authoredAt = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var authoredBy = new ActorId("operator-1");

        var lease = new Lease
        {
            Id = leaseId,
            TenantId = TenantA,
            UnitId = new EntityId("unit", "units", "unit-1"),
            Tenants = new[] { new PartyId("party-1") },
            Landlord = new PartyId("landlord-1"),
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            MonthlyRent = 1500m,
            Phase = LeasePhase.Active,
            DocumentVersions = new List<LeaseDocumentVersionId> { versionId },
        };

        var leaseVersion = new LeaseDocumentVersion
        {
            Id = versionId,
            Lease = leaseId,
            VersionNumber = 1,
            DocumentHash = new ContentHash(new byte[32]),
            DocumentBlobRef = "blob/ref/1",
            AuthoredBy = authoredBy,
            AuthoredAt = authoredAt,
            ChangeSummary = "Initial",
        };

        var leaseService = Substitute.For<ILeaseService>();
        leaseService.ListAsync(Arg.Any<ListLeasesQuery>(), Arg.Any<CancellationToken>())
                    .Returns(new[] { lease }.ToAsyncEnumerable());

        var leaseDocLog = Substitute.For<ILeaseDocumentVersionLog>();
        leaseDocLog.GetLatestAsync(leaseId, Arg.Any<CancellationToken>())
                   .Returns(leaseVersion);

        var snapshot = await Build(leaseService: leaseService, leaseDocLog: leaseDocLog)
            .GetSnapshotAsync(TenantA);

        var view = Assert.Single(snapshot.Documents);
        Assert.Equal(ShipsOfficeDocumentKind.LeaseDocument, view.Kind);
        Assert.Contains(leaseId.Value, view.Title);
        Assert.Equal(DocumentStatus.Published, view.Status);
        Assert.Equal("v1", view.VersionLabel);
        Assert.Equal(authoredBy, view.LastModifiedBy);
        Assert.Equal(authoredAt, view.UpdatedAt);
    }

    [Fact]
    public async Task GetSnapshotAsync_LeaseDocument_SkipsLeasesWithNoDocuments()
    {
        var lease = new Lease
        {
            Id = LeaseId.NewId(),
            TenantId = TenantA,
            UnitId = new EntityId("unit", "units", "unit-1"),
            Tenants = new[] { new PartyId("party-1") },
            Landlord = new PartyId("landlord-1"),
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            MonthlyRent = 1200m,
            Phase = LeasePhase.Draft,
            DocumentVersions = Array.Empty<LeaseDocumentVersionId>(),
        };

        var leaseService = Substitute.For<ILeaseService>();
        leaseService.ListAsync(Arg.Any<ListLeasesQuery>(), Arg.Any<CancellationToken>())
                    .Returns(new[] { lease }.ToAsyncEnumerable());

        var snapshot = await Build(leaseService: leaseService).GetSnapshotAsync(TenantA);
        Assert.Empty(snapshot.Documents);
    }

    [Theory]
    [InlineData(LeasePhase.Draft, DocumentStatus.Draft)]
    [InlineData(LeasePhase.AwaitingSignature, DocumentStatus.PendingSignature)]
    [InlineData(LeasePhase.Executed, DocumentStatus.Published)]
    [InlineData(LeasePhase.Active, DocumentStatus.Published)]
    [InlineData(LeasePhase.Renewed, DocumentStatus.Published)]
    [InlineData(LeasePhase.Terminated, DocumentStatus.Archived)]
    [InlineData(LeasePhase.Cancelled, DocumentStatus.Archived)]
    public async Task GetSnapshotAsync_LeaseDocument_MapsPhaseToStatus(
        LeasePhase phase, DocumentStatus expectedStatus)
    {
        var leaseId = LeaseId.NewId();
        var versionId = new LeaseDocumentVersionId(Guid.NewGuid());

        var lease = new Lease
        {
            Id = leaseId,
            TenantId = TenantA,
            UnitId = new EntityId("unit", "units", "unit-1"),
            Tenants = new[] { new PartyId("party-1") },
            Landlord = new PartyId("landlord-1"),
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            MonthlyRent = 1200m,
            Phase = phase,
            DocumentVersions = new List<LeaseDocumentVersionId> { versionId },
        };

        var leaseVersion = new LeaseDocumentVersion
        {
            Id = versionId,
            Lease = leaseId,
            VersionNumber = 1,
            DocumentHash = new ContentHash(new byte[32]),
            DocumentBlobRef = "blob/1",
            AuthoredBy = new ActorId("op"),
            AuthoredAt = DateTimeOffset.UtcNow,
            ChangeSummary = "Test",
        };

        var leaseService = Substitute.For<ILeaseService>();
        leaseService.ListAsync(Arg.Any<ListLeasesQuery>(), Arg.Any<CancellationToken>())
                    .Returns(new[] { lease }.ToAsyncEnumerable());

        var leaseDocLog = Substitute.For<ILeaseDocumentVersionLog>();
        leaseDocLog.GetLatestAsync(leaseId, Arg.Any<CancellationToken>())
                   .Returns(leaseVersion);

        var snapshot = await Build(leaseService: leaseService, leaseDocLog: leaseDocLog)
            .GetSnapshotAsync(TenantA);

        Assert.Equal(expectedStatus, Assert.Single(snapshot.Documents).Status);
    }

    // ── Snapshot: VendorW9 projection ─────────────────────────────────────────

    [Fact]
    public async Task GetSnapshotAsync_VendorW9_ProjectsWithoutTin()
    {
        var w9Id = new W9DocumentId(Guid.NewGuid());
        var receivedAt = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var vendor = new Vendor
        {
            Id = new VendorId(Guid.NewGuid().ToString()),
            TenantId = new TenantId("tenant-test"),
            DisplayName = "Acme Contractors",
            Status = VendorStatus.Active,
            OnboardingState = VendorOnboardingState.Active,
            W9 = w9Id,
        };

        var w9 = new W9Document
        {
            Id = w9Id,
            Vendor = vendor.Id,
            LegalName = "Acme Contractors LLC",
            TaxClassification = W9TaxClassification.LLC,
            TinEncrypted = new EncryptedField(new byte[16], new byte[12], 1),
            Address = new W9MailingAddress("123 Main St", null, "Seattle", "WA", "98101"),
            ReceivedAt = receivedAt,
        };

        var maintenanceSvc = Substitute.For<IMaintenanceService>();
        maintenanceSvc.ListVendorsAsync(Arg.Any<ListVendorsQuery>(), Arg.Any<CancellationToken>())
                      .Returns(new[] { vendor }.ToAsyncEnumerable());

        var w9Svc = Substitute.For<IW9DocumentService>();
        w9Svc.GetAsync(w9Id, TenantA, Arg.Any<CancellationToken>()).Returns(w9);

        var snapshot = await Build(maintenanceService: maintenanceSvc, w9Service: w9Svc)
            .GetSnapshotAsync(TenantA);

        var view = Assert.Single(snapshot.Documents);
        Assert.Equal(ShipsOfficeDocumentKind.VendorW9, view.Kind);
        Assert.Equal("Acme Contractors", view.Title);
        Assert.Equal(DocumentStatus.Draft, view.Status);
        Assert.Equal(receivedAt, view.UpdatedAt);
        Assert.Null(view.VersionLabel);
    }

    [Fact]
    public async Task GetSnapshotAsync_VendorW9_SkipsVendorsWithNoW9()
    {
        var vendor = new Vendor
        {
            Id = new VendorId(Guid.NewGuid().ToString()),
            TenantId = new TenantId("tenant-test"),
            DisplayName = "No W9 Vendor",
            Status = VendorStatus.Active,
            OnboardingState = VendorOnboardingState.Pending,
            W9 = null,
        };

        var maintenanceSvc = Substitute.For<IMaintenanceService>();
        maintenanceSvc.ListVendorsAsync(Arg.Any<ListVendorsQuery>(), Arg.Any<CancellationToken>())
                      .Returns(new[] { vendor }.ToAsyncEnumerable());

        var snapshot = await Build(maintenanceService: maintenanceSvc).GetSnapshotAsync(TenantA);
        Assert.Empty(snapshot.Documents);
    }

    [Fact]
    public async Task GetSnapshotAsync_VendorW9_VerifiedVendorIsPublished()
    {
        var w9Id = new W9DocumentId(Guid.NewGuid());
        var verifiedAt = new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var verifiedBy = new ActorId("admin-1");

        var vendor = new Vendor
        {
            Id = new VendorId(Guid.NewGuid().ToString()),
            TenantId = new TenantId("tenant-test"),
            DisplayName = "Verified Vendor",
            Status = VendorStatus.Active,
            OnboardingState = VendorOnboardingState.Active,
            W9 = w9Id,
        };

        var w9 = new W9Document
        {
            Id = w9Id,
            Vendor = vendor.Id,
            LegalName = "Verified Vendor Inc",
            TaxClassification = W9TaxClassification.CCorp,
            TinEncrypted = new EncryptedField(new byte[16], new byte[12], 1),
            Address = new W9MailingAddress("1 Corp Dr", null, "Redmond", "WA", "98052"),
            ReceivedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            VerifiedAt = verifiedAt,
            VerifiedBy = verifiedBy,
        };

        var maintenanceSvc = Substitute.For<IMaintenanceService>();
        maintenanceSvc.ListVendorsAsync(Arg.Any<ListVendorsQuery>(), Arg.Any<CancellationToken>())
                      .Returns(new[] { vendor }.ToAsyncEnumerable());

        var w9Svc = Substitute.For<IW9DocumentService>();
        w9Svc.GetAsync(w9Id, TenantA, Arg.Any<CancellationToken>()).Returns(w9);

        var snapshot = await Build(maintenanceService: maintenanceSvc, w9Service: w9Svc)
            .GetSnapshotAsync(TenantA);

        var view = Assert.Single(snapshot.Documents);
        Assert.Equal(DocumentStatus.Published, view.Status);
        Assert.Equal(verifiedAt, view.UpdatedAt);
        Assert.Equal(verifiedBy, view.LastModifiedBy);
    }

    // ── SearchAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_NullQuery_Throws()
    {
        var enumerator = Build().SearchAsync(TenantA, null!, default).GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_YieldsNoResults_WhenNoSources()
    {
        var query = new ShipsOfficeSearchQuery(null, null, null);
        var results = new List<ShipsOfficeDocumentView>();
        await foreach (var v in Build().SearchAsync(TenantA, query))
            results.Add(v);
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_TextFilter_CaseInsensitive()
    {
        var catalog = Substitute.For<IBundleCatalog>();
        catalog.GetBundles().Returns(new[]
        {
            new BusinessCaseBundleManifest { Key = "a", Name = "Alpha Bundle", Version = "1.0" },
            new BusinessCaseBundleManifest { Key = "b", Name = "Beta Package", Version = "1.0" },
        });

        var query = new ShipsOfficeSearchQuery(TextQuery: "alpha", KindFilter: null, StatusFilter: null);
        var results = new List<ShipsOfficeDocumentView>();
        await foreach (var v in Build(bundleCatalog: catalog).SearchAsync(TenantA, query))
            results.Add(v);

        Assert.Single(results);
        Assert.Equal("Alpha Bundle", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_KindFilter_Restricts()
    {
        var catalog = Substitute.For<IBundleCatalog>();
        catalog.GetBundles().Returns(new[]
        {
            new BusinessCaseBundleManifest { Key = "a", Name = "Bundle A", Version = "1.0" },
        });

        var query = new ShipsOfficeSearchQuery(
            TextQuery: null,
            KindFilter: new[] { ShipsOfficeDocumentKind.LeaseDocument },
            StatusFilter: null);

        var results = new List<ShipsOfficeDocumentView>();
        await foreach (var v in Build(bundleCatalog: catalog).SearchAsync(TenantA, query))
            results.Add(v);

        Assert.Empty(results);
    }

    // ── DI registration ───────────────────────────────────────────────────────

    [Fact]
    public async Task NoopContentEditorSurface_ReturnsCancelledNotSaved()
    {
        var surface = new NoopContentEditorSurface();
        var result = await surface.EditAsync(TenantA, new ShipsOfficeDocumentId("doc-1"));
        Assert.False(result.WasSaved);
        Assert.Null(result.NewVersionLabel);
    }

    [Fact]
    public void AddSunfishShipsOfficeDefaults_RegistersProviderAndEditor()
    {
        var maintenanceSvc = EmptyMaintenanceService();
        var services = new ServiceCollection();
        services.AddSingleton(EmptyBundleCatalog());
        services.AddSingleton(EmptyLeaseService());
        services.AddSingleton(Substitute.For<ILeaseDocumentVersionLog>());
        services.AddSingleton(maintenanceSvc);
        services.AddSingleton(Substitute.For<IW9DocumentService>());
        services.AddSingleton(NoopMissionEnvelopeProvider());
        services.AddSunfishShipsOffice();
        services.AddSunfishShipsOfficeDefaults();
        using var sp = services.BuildServiceProvider();

        Assert.IsType<ShipsOfficeDataProvider>(sp.GetService<IShipsOfficeDataProvider>());
        Assert.IsType<NoopContentEditorSurface>(sp.GetService<IContentEditorSurface>());
    }

    // ── H4 invariant — strengthened per XO ruling (W#55 Phase 2b) ────────────

    /// <summary>
    /// W#55 Phase 2b H4 (load-bearing) — <see cref="ShipsOfficeDataProvider"/>
    /// MUST NOT depend on <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>.
    /// Per ADR 0046-A2 §4 + ADR 0083 §Trust impact: Ship's Office browse uses
    /// <c>IW9DocumentService.GetAsync</c> only; TIN excluded from the view.
    /// Two-layered evidence per W#54 P2 cohort precedent:
    /// (1) AssemblyName-level — no <c>Sunfish.Foundation.Recovery</c> reference.
    /// (2) Type-graph walk — fields, ctor params, method params, return types,
    ///     generic args, and method-body locals checked for <c>IFieldDecryptor</c>.
    /// </summary>
    [Fact]
    public void Provider_DoesNotReference_FoundationRecovery()
    {
        const string ForbiddenAssembly = "Sunfish.Foundation.Recovery";
        const string ForbiddenName = "IFieldDecryptor";

        var assembly = typeof(ShipsOfficeDataProvider).Assembly;

        // (1) AssemblyName-level.
        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
        Assert.DoesNotContain(ForbiddenAssembly, referencedAssemblies);

        // (2) Type-graph walk.
        var providerType = typeof(ShipsOfficeDataProvider);
        const BindingFlags AllMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        foreach (var field in providerType.GetFields(AllMembers))
            AssertNotForbidden(field.FieldType, $"field {field.Name}", ForbiddenName);

        foreach (var ctor in providerType.GetConstructors(AllMembers))
        {
            foreach (var p in ctor.GetParameters())
                AssertNotForbidden(p.ParameterType, $"ctor param {p.Name}", ForbiddenName);
        }

        foreach (var method in providerType.GetMethods(AllMembers))
        {
            AssertNotForbidden(method.ReturnType, $"return of {method.Name}", ForbiddenName);
            foreach (var p in method.GetParameters())
                AssertNotForbidden(p.ParameterType, $"param {p.Name} of {method.Name}", ForbiddenName);
            var body = method.GetMethodBody();
            if (body is null) continue;
            foreach (var local in body.LocalVariables)
                AssertNotForbidden(local.LocalType, $"local in {method.Name}", ForbiddenName);
        }
    }

    private static void AssertNotForbidden(Type type, string site, string forbiddenName)
    {
        Assert.DoesNotContain(forbiddenName, type.FullName ?? string.Empty);
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                AssertNotForbidden(arg, site + " (generic arg)", forbiddenName);
        }
    }

    // ── DynamicTemplate kind via canonical IFormDefinitionStore (FN-4) ────────

    [Fact]
    public async Task GetSnapshotAsync_IncludesDynamicTemplate_FromFormDefinitionStore()
    {
        var updatedAt = new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero);
        var owner = new IdentityRef("user", "admin-1");
        var def = NewDefinition(
            "vendor-onboarding",
            FormDefinitionStatus.Published,
            updatedAt,
            title: "Vendor Onboarding Form",
            owner: owner);

        var store = Substitute.For<IFormDefinitionStore>();
        store.ListByTenantAsync(TenantA, Arg.Any<CancellationToken>())
             .Returns(new[] { def }.ToAsyncEnumerable());

        var snapshot = await Build(formDefinitions: store).GetSnapshotAsync(TenantA);

        var doc = Assert.Single(snapshot.Documents);
        Assert.Equal(ShipsOfficeDocumentKind.DynamicTemplate, doc.Kind);
        Assert.Equal("Vendor Onboarding Form", doc.Title);
        Assert.Equal(DocumentStatus.Published, doc.Status);
        Assert.Equal("form-definition:vendor-onboarding", doc.Id.Value);
        Assert.Equal(updatedAt, doc.UpdatedAt);
        Assert.Equal("user:admin-1", doc.LastModifiedBy.Value);
        Assert.Equal("1.0.0", doc.VersionLabel);
    }

    [Fact]
    public void DynamicTemplate_View_Carries_MetadataOnly_NoSchemaPayload()
    {
        // §Trust anchor: the canonical FormDefinition keystone is a rich
        // type (Overlay/SchemaRef/Lineage/Fields/Sections/Rules). The
        // DynamicTemplate projection MUST surface metadata ONLY — the
        // ShipsOfficeDocumentView is the redaction boundary. Pin the view's
        // property surface so a future keystone field can't silently leak
        // schema payload into the browse view; redaction lives here, not
        // in the source type's shape (which now legitimately grows).
        var viewProps = typeof(ShipsOfficeDocumentView)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(x => x)
            .ToArray();
        var expected = new[]
        {
            "Id", "Kind", "LastModifiedBy", "Status", "Title", "UpdatedAt", "VersionLabel",
        };
        Assert.Equal(expected, viewProps);
    }

    [Fact]
    public async Task SearchAsync_FiltersDynamicTemplate_ByStatusFilter()
    {
        var now = DateTimeOffset.UtcNow;
        var store = Substitute.For<IFormDefinitionStore>();
        store.ListByTenantAsync(TenantA, Arg.Any<CancellationToken>())
             .Returns(new[]
             {
                 NewDefinition("draft-form", FormDefinitionStatus.Draft, now, title: "Draft Form"),
                 NewDefinition("published-form", FormDefinitionStatus.Published, now, title: "Published Form"),
                 NewDefinition("withdrawn-form", FormDefinitionStatus.Withdrawn, now, title: "Withdrawn Form"),
             }.ToAsyncEnumerable());

        var provider = Build(formDefinitions: store);
        var draftQuery = new ShipsOfficeSearchQuery(
            TextQuery: null, KindFilter: null, StatusFilter: DocumentStatus.Draft,
            PageSize: 50, PageToken: null);
        var results = new List<ShipsOfficeDocumentView>();
        await foreach (var v in provider.SearchAsync(TenantA, draftQuery))
            results.Add(v);

        var draftOnly = Assert.Single(results);
        Assert.Equal(ShipsOfficeDocumentKind.DynamicTemplate, draftOnly.Kind);
        Assert.Equal("Draft Form", draftOnly.Title);
        Assert.Equal(DocumentStatus.Draft, draftOnly.Status);
    }
}
