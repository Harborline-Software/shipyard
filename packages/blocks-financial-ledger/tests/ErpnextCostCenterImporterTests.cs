using Sunfish.Blocks.FinancialLedger.Migration;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;

namespace Sunfish.Blocks.FinancialLedger.Tests;

/// <summary>
/// Workstream A1 — coverage for the §3.4 cost-center → Property/Classification
/// heuristic (<see cref="ErpnextCostCenterImporter"/>). Fixture-only — synthetic
/// hand-built cost-center sources. Proves the three-rung resolution order
/// (custom Property DocType → alias map → created Classification), group-node
/// skip, idempotency, and the DU outcome shape.
/// </summary>
public sealed class ErpnextCostCenterImporterTests
{
    private static ErpnextCostCenterSource CostCenter(
        string name, string displayName, string? parent = null, bool isGroup = false, bool disabled = false) =>
        new(Name: name,
            Modified: "2026-05-16 12:00:00",
            CostCenterName: displayName,
            ParentCostCenterName: parent,
            IsGroup: isGroup,
            Disabled: disabled);

    // ---- rung 3: fallback to a created Classification -----------------

    [Fact]
    public async Task Upsert_NoPropertyMatch_CreatesClassification()
    {
        var store = new InMemoryClassificationStore();
        var sut = new ErpnextCostCenterImporter(PropertyAliasMap.Empty, store);

        var outcome = await sut.UpsertFromErpnextAsync(CostCenter("cc-1", "Marketing"));

        var inserted = Assert.IsType<ImportOutcome<CostCenterResolution>.Inserted>(outcome);
        Assert.Equal(CostCenterResolutionKind.CreatedClassification, inserted.Record.Kind);
        Assert.NotNull(inserted.Record.Classification);
        Assert.Equal("Marketing", inserted.Record.Classification!.Name);
        Assert.Equal("cc-1", inserted.Record.Classification.ExternalRef);
        // The classification was persisted to the store for idempotency.
        Assert.Single(store.All);
    }

    // ---- rung 2: CO-authored alias map --------------------------------

    [Fact]
    public async Task Upsert_AliasMapMatch_ResolvesToProperty()
    {
        var store = new InMemoryClassificationStore();
        var aliasMap = new PropertyAliasMap(new[]
        {
            new PropertyAliasEntry("Acero - 123 Main St", "PROP-0001"),
        });
        var sut = new ErpnextCostCenterImporter(aliasMap, store);

        var outcome = await sut.UpsertFromErpnextAsync(CostCenter("cc-acero", "Acero - 123 Main St"));

        var inserted = Assert.IsType<ImportOutcome<CostCenterResolution>.Inserted>(outcome);
        Assert.Equal(CostCenterResolutionKind.ResolvedToProperty, inserted.Record.Kind);
        Assert.Equal(new PropertyId("PROP-0001"), inserted.Record.PropertyId);
        Assert.Null(inserted.Record.Classification);
        // Property resolution does NOT create a classification.
        Assert.Empty(store.All);
    }

    [Fact]
    public async Task Upsert_AliasMapMatch_IsCaseInsensitive()
    {
        var aliasMap = new PropertyAliasMap(new[]
        {
            new PropertyAliasEntry("Bosco - 456 Oak Ave", "PROP-0002"),
        });
        var sut = new ErpnextCostCenterImporter(aliasMap, new InMemoryClassificationStore());

        var outcome = await sut.UpsertFromErpnextAsync(CostCenter("cc-bosco", "bosco - 456 OAK ave"));

        var inserted = Assert.IsType<ImportOutcome<CostCenterResolution>.Inserted>(outcome);
        Assert.Equal(new PropertyId("PROP-0002"), inserted.Record.PropertyId);
    }

    // ---- rung 1: custom Property DocType resolver wins ----------------

    [Fact]
    public async Task Upsert_CustomPropertyResolverMatch_WinsOverAliasAndClassification()
    {
        // Alias map would ALSO match — prove the custom resolver takes precedence.
        var aliasMap = new PropertyAliasMap(new[]
        {
            new PropertyAliasEntry("123 Main St", "ALIAS-WRONG"),
        });
        var sut = new ErpnextCostCenterImporter(
            aliasMap,
            new InMemoryClassificationStore(),
            propertyResolver: name => name == "123 Main St" ? new PropertyId("CUSTOM-RIGHT") : (PropertyId?)null);

        var outcome = await sut.UpsertFromErpnextAsync(CostCenter("cc-x", "123 Main St"));

        var inserted = Assert.IsType<ImportOutcome<CostCenterResolution>.Inserted>(outcome);
        Assert.Equal(new PropertyId("CUSTOM-RIGHT"), inserted.Record.PropertyId);
    }

    // ---- group nodes are skipped --------------------------------------

    [Fact]
    public async Task Upsert_GroupCostCenter_IsSkipped()
    {
        var sut = new ErpnextCostCenterImporter(PropertyAliasMap.Empty, new InMemoryClassificationStore());

        var outcome = await sut.UpsertFromErpnextAsync(
            CostCenter("cc-grp", "All Cost Centers", isGroup: true));

        var skipped = Assert.IsType<ImportOutcome<CostCenterResolution>.Skipped>(outcome);
        Assert.Equal(ImportAction.Skipped, outcome.Action);
        Assert.NotNull(skipped.Detail);
    }

    // ---- missing name → Rejected --------------------------------------

    [Fact]
    public async Task Upsert_MissingName_IsRejected()
    {
        var sut = new ErpnextCostCenterImporter(PropertyAliasMap.Empty, new InMemoryClassificationStore());

        var outcome = await sut.UpsertFromErpnextAsync(CostCenter("", "Nameless"));

        var rejected = Assert.IsType<ImportOutcome<CostCenterResolution>.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Equal(ImportRejectReason.MissingRequiredField.ToString(), rejected.Failure.ReasonCode);
        Assert.Equal("Cost Center", rejected.Failure.DocType);
    }

    // ---- idempotency ---------------------------------------------------

    [Fact]
    public async Task Upsert_Reimport_ReturnsSkipped()
    {
        var store = new InMemoryClassificationStore();
        var sut = new ErpnextCostCenterImporter(PropertyAliasMap.Empty, store);
        var source = CostCenter("cc-1", "Marketing");

        var first = await sut.UpsertFromErpnextAsync(source);
        Assert.IsType<ImportOutcome<CostCenterResolution>.Inserted>(first);

        var again = await sut.UpsertFromErpnextAsync(source);

        Assert.IsType<ImportOutcome<CostCenterResolution>.Skipped>(again);
        // No second classification created.
        Assert.Single(store.All);
    }

    // ---- disabled cost-center → inactive classification ---------------

    [Fact]
    public async Task Upsert_DisabledCostCenter_CreatesInactiveClassification()
    {
        var store = new InMemoryClassificationStore();
        var sut = new ErpnextCostCenterImporter(PropertyAliasMap.Empty, store);

        var outcome = await sut.UpsertFromErpnextAsync(
            CostCenter("cc-off", "Retired Center", disabled: true));

        var inserted = Assert.IsType<ImportOutcome<CostCenterResolution>.Inserted>(outcome);
        Assert.False(inserted.Record.Classification!.IsActive);
    }

    // ---- alias-map helper unit coverage -------------------------------

    [Fact]
    public void PropertyAliasMap_Empty_AlwaysMisses()
    {
        Assert.False(PropertyAliasMap.Empty.TryResolve("anything", out _));
    }

    [Fact]
    public void PropertyAliasMap_SkipsBlankEntries()
    {
        var map = new PropertyAliasMap(new[]
        {
            new PropertyAliasEntry("", "PROP-X"),
            new PropertyAliasEntry("Good", ""),
            new PropertyAliasEntry("Valid", "PROP-OK"),
        });

        Assert.False(map.TryResolve("", out _));
        Assert.False(map.TryResolve("Good", out _));
        Assert.True(map.TryResolve("Valid", out var id));
        Assert.Equal("PROP-OK", id);
    }
}
