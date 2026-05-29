namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// The depreciation method applied to an <see cref="Asset"/>'s acquisition cost
/// basis over its useful life. Pure computation; no tax-jurisdiction provider
/// (a jurisdiction-aware method would promote method selection to a tier-2
/// category-provider — flagged as a revisit condition in ADR 0101 Consequences).
/// </summary>
public enum DepreciationMethod
{
    /// <summary>Equal expense each period over the useful life.</summary>
    StraightLine,

    /// <summary>Accelerated: a fixed rate applied to the declining book value each period.</summary>
    DecliningBalance,

    /// <summary>Expense proportional to actual usage (units produced / consumed) vs. expected lifetime units.</summary>
    UnitsOfProduction,

    /// <summary>No depreciation is computed (land, collectibles, or assets tracked at cost).</summary>
    None,
}
