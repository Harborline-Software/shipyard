using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// A depreciation schedule for an <see cref="Asset"/>. Pure computation — given
/// the acquisition cost basis, the method, the salvage value, the useful life,
/// and a point in time, it computes accumulated + periodic depreciation and the
/// remaining book value. No external provider; no side effects.
/// </summary>
/// <remarks>
/// Satisfies the <c>assets.depreciation.autoCalculate</c> feature key, which
/// <b>defaults to <c>false</c></b> per the bundle manifest — the schedule may be
/// attached to an asset, but auto-calculation is opt-in
/// (see <see cref="AutoCalculate"/>). <see cref="DepreciationMethod.UnitsOfProduction"/>
/// additionally requires <see cref="ExpectedLifetimeUnits"/> to be set; when it
/// is null the schedule computes zero depreciation (the usage basis is unknown).
/// </remarks>
public sealed record DepreciationSchedule
{
    /// <summary>The depreciation method to apply.</summary>
    public required DepreciationMethod Method { get; init; }

    /// <summary>Cost basis being depreciated (the asset's acquisition cost).</summary>
    public required Money Basis { get; init; }

    /// <summary>
    /// Estimated residual value at the end of the useful life. The depreciable
    /// amount is <c>Basis - SalvageValue</c>. Defaults to a zero-amount
    /// <see cref="Money"/> in the basis currency when not set.
    /// </summary>
    public Money? SalvageValue { get; init; }

    /// <summary>The date depreciation begins (typically the acquisition date).</summary>
    public required DateTimeOffset StartDate { get; init; }

    /// <summary>Useful life in years. Must be positive for time-based methods.</summary>
    public required int UsefulLifeYears { get; init; }

    /// <summary>
    /// For <see cref="DepreciationMethod.DecliningBalance"/>: the annual rate
    /// applied to the declining book value (e.g. <c>0.20m</c> for 20% per year,
    /// or <c>2m / UsefulLifeYears</c> for double-declining). Defaults to
    /// double-declining (<c>2 / UsefulLifeYears</c>) when not set.
    /// </summary>
    public decimal? DecliningBalanceRate { get; init; }

    /// <summary>
    /// For <see cref="DepreciationMethod.UnitsOfProduction"/>: the total expected
    /// lifetime production units (e.g. machine-hours, miles). Required for that
    /// method; ignored otherwise.
    /// </summary>
    public decimal? ExpectedLifetimeUnits { get; init; }

    /// <summary>
    /// Whether auto-calculation is enabled for this asset. Defaults to
    /// <c>false</c>, matching the <c>assets.depreciation.autoCalculate</c>
    /// manifest default — the schedule data exists but downstream auto-calc is
    /// opt-in.
    /// </summary>
    public bool AutoCalculate { get; init; }

    /// <summary>
    /// The depreciable amount: <c>Basis - SalvageValue</c>, floored at zero.
    /// </summary>
    public Money DepreciableAmount
    {
        get
        {
            var salvage = SalvageValue?.Amount ?? 0m;
            var depreciable = Basis.Amount - salvage;
            if (depreciable < 0m)
            {
                depreciable = 0m;
            }
            return new Money(depreciable, Basis.Currency);
        }
    }

    /// <summary>
    /// Accumulated depreciation from <see cref="StartDate"/> through
    /// <paramref name="asOf"/>, using <paramref name="unitsConsumed"/> for
    /// <see cref="DepreciationMethod.UnitsOfProduction"/>. Never exceeds
    /// <see cref="DepreciableAmount"/>; never negative.
    /// </summary>
    /// <param name="asOf">The point in time to compute accumulated depreciation through.</param>
    /// <param name="unitsConsumed">
    /// Cumulative production units consumed (only used by
    /// <see cref="DepreciationMethod.UnitsOfProduction"/>).
    /// </param>
    public Money AccumulatedDepreciation(DateTimeOffset asOf, decimal unitsConsumed = 0m)
    {
        var currency = Basis.Currency;
        var depreciable = DepreciableAmount.Amount;

        if (Method == DepreciationMethod.None || depreciable <= 0m || asOf <= StartDate)
        {
            return new Money(0m, currency);
        }

        var elapsedYears = ElapsedYears(asOf);

        var accumulated = Method switch
        {
            DepreciationMethod.StraightLine => StraightLineAccumulated(depreciable, elapsedYears),
            DepreciationMethod.DecliningBalance => DecliningBalanceAccumulated(depreciable, elapsedYears),
            DepreciationMethod.UnitsOfProduction => UnitsOfProductionAccumulated(depreciable, unitsConsumed),
            _ => 0m,
        };

        accumulated = Clamp(accumulated, 0m, depreciable);
        return new Money(accumulated, currency);
    }

    /// <summary>
    /// Remaining book value as of <paramref name="asOf"/>:
    /// <c>Basis - AccumulatedDepreciation</c>. Never falls below the salvage value.
    /// </summary>
    public Money BookValue(DateTimeOffset asOf, decimal unitsConsumed = 0m)
    {
        var accumulated = AccumulatedDepreciation(asOf, unitsConsumed).Amount;
        var book = Basis.Amount - accumulated;
        var salvage = SalvageValue?.Amount ?? 0m;
        if (book < salvage)
        {
            book = salvage;
        }
        return new Money(book, Basis.Currency);
    }

    private double ElapsedYears(DateTimeOffset asOf)
    {
        if (UsefulLifeYears <= 0)
        {
            return 0d;
        }
        var years = (asOf - StartDate).TotalDays / 365.25d;
        return years < 0d ? 0d : years;
    }

    private decimal StraightLineAccumulated(decimal depreciable, double elapsedYears)
    {
        if (UsefulLifeYears <= 0)
        {
            return depreciable;
        }
        var fraction = (decimal)Math.Min(elapsedYears / UsefulLifeYears, 1d);
        return depreciable * fraction;
    }

    private decimal DecliningBalanceAccumulated(decimal depreciable, double elapsedYears)
    {
        if (UsefulLifeYears <= 0)
        {
            return depreciable;
        }
        var rate = DecliningBalanceRate ?? (2m / UsefulLifeYears);
        if (rate <= 0m)
        {
            return 0m;
        }

        // Apply the rate to the declining BOOK value year over year, capped at the
        // number of full years elapsed, then the salvage floor is enforced by the
        // DepreciableAmount clamp in the caller.
        var fullYears = (int)Math.Floor(elapsedYears);
        var openingBookValue = Basis.Amount;
        var salvage = SalvageValue?.Amount ?? 0m;
        var accumulated = 0m;

        for (var year = 0; year < fullYears && year < UsefulLifeYears; year++)
        {
            var bookValue = openingBookValue - accumulated;
            if (bookValue <= salvage)
            {
                break;
            }
            var periodExpense = bookValue * rate;
            // Don't depreciate below the salvage value within a period.
            if (bookValue - periodExpense < salvage)
            {
                periodExpense = bookValue - salvage;
            }
            accumulated += periodExpense;
        }

        return accumulated;
    }

    private decimal UnitsOfProductionAccumulated(decimal depreciable, decimal unitsConsumed)
    {
        if (ExpectedLifetimeUnits is not { } lifetimeUnits || lifetimeUnits <= 0m || unitsConsumed <= 0m)
        {
            return 0m;
        }
        var fraction = unitsConsumed / lifetimeUnits;
        if (fraction > 1m)
        {
            fraction = 1m;
        }
        return depreciable * fraction;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;
}
