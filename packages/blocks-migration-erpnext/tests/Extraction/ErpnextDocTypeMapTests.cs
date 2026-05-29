using System;
using System.Linq;
using Sunfish.Blocks.Migration.Erpnext.Extraction;
using Xunit;

namespace Sunfish.Blocks.Migration.Erpnext.Tests.Extraction;

/// <summary>
/// Tests for <see cref="ErpnextDocTypeMap"/> — v1 map completeness + version pin
/// (ADR 0100 C5 / spec §4.1).
/// </summary>
public sealed class ErpnextDocTypeMapTests
{
    /// <summary>
    /// The v1 map includes all 11 primary DocType tables listed in the design §2.2
    /// (tabAccount, tabCost Center, tabFiscal Year, tabCustomer, tabSupplier,
    /// tabContact, tabAddress, tabSales Taxes and Charges Template,
    /// tabPurchase Taxes and Charges Template, tabJournal Entry,
    /// tabSales Invoice, tabPurchase Invoice).
    /// </summary>
    [Theory]
    [InlineData("tabAccount")]
    [InlineData("tabCost Center")]
    [InlineData("tabFiscal Year")]
    [InlineData("tabCustomer")]
    [InlineData("tabSupplier")]
    [InlineData("tabContact")]
    [InlineData("tabAddress")]
    [InlineData("tabSales Taxes and Charges Template")]
    [InlineData("tabPurchase Taxes and Charges Template")]
    [InlineData("tabJournal Entry")]
    [InlineData("tabSales Invoice")]
    [InlineData("tabPurchase Invoice")]
    public void V1_map_includes_all_design_tables(string tableName)
    {
        Assert.True(
            ErpnextDocTypeMap.IsMapped(tableName),
            $"Table '{tableName}' should be in the v1 ERPNext DocType map (design §2.2).");
    }

    /// <summary>
    /// The version pin is ERPNext v15 per CIC build parameter.
    /// </summary>
    [Fact]
    public void Version_is_pinned_to_v15()
    {
        Assert.Equal("v15", ErpnextDocTypeMap.ErpnextVersion);
    }

    /// <summary>
    /// Every entry in the map has a non-null, non-empty target DTO name.
    /// </summary>
    [Fact]
    public void All_entries_have_non_empty_target_dto()
    {
        foreach (var (table, entry) in ErpnextDocTypeMap.PrimaryTables)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(entry.TargetDto),
                $"Table '{table}' has an empty TargetDto in the map.");

            Assert.False(
                string.IsNullOrWhiteSpace(entry.ExtractorMethod),
                $"Table '{table}' has an empty ExtractorMethod in the map.");
        }
    }

    /// <summary>
    /// All mapped table names follow the ERPNext convention of starting with "tab"
    /// (case-sensitive) — a basic schema-sanity check.
    /// </summary>
    [Fact]
    public void All_mapped_tables_start_with_tab_prefix()
    {
        foreach (var tableName in ErpnextDocTypeMap.PrimaryTables.Keys)
        {
            Assert.True(
                tableName.StartsWith("tab", StringComparison.Ordinal),
                $"Table name '{tableName}' does not follow the 'tab' prefix convention.");
        }
    }

    /// <summary>
    /// Known-irrelevant DocTypes do NOT overlap with the mapped DocTypes — the two
    /// sets must be disjoint (a DocType cannot be both extracted and ignored).
    /// </summary>
    [Fact]
    public void Mapped_and_known_irrelevant_sets_are_disjoint()
    {
        var overlap = ErpnextDocTypeMap.PrimaryTables.Keys
            .Where(t => KnownIrrelevantDocTypes.All.Contains(t))
            .ToList();

        Assert.True(
            overlap.Count == 0,
            $"Tables are in both the mapped set and the known-irrelevant allowlist (must be disjoint): " +
            string.Join(", ", overlap));
    }

    /// <summary>
    /// <see cref="ErpnextDocTypeMap.IsMapped"/> returns false for a table not in the v1 map.
    /// </summary>
    [Theory]
    [InlineData("tabProperty")]
    [InlineData("tabLease")]
    [InlineData("tabNonExistent")]
    public void IsMapped_returns_false_for_unknown_tables(string tableName)
    {
        Assert.False(ErpnextDocTypeMap.IsMapped(tableName));
    }
}
