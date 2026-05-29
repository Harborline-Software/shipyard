using Sunfish.Blocks.Assets.Domain;
using Sunfish.Foundation.Integrations.Payments;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests.Domain;

public sealed class DepreciationScheduleTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AutoCalculate_DefaultsOff()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.StraightLine,
            Basis = Money.Usd(10_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
        };

        Assert.False(schedule.AutoCalculate);
    }

    [Fact]
    public void DepreciableAmount_IsBasisMinusSalvage()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.StraightLine,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(2_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
        };

        Assert.Equal(8_000m, schedule.DepreciableAmount.Amount);
    }

    [Fact]
    public void StraightLine_HalfwayThroughLife_IsHalfDepreciated()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.StraightLine,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(0m),
            StartDate = Start,
            UsefulLifeYears = 10,
        };

        // 5 years into a 10-year life ≈ 50% (365.25-day year basis).
        var fiveYears = Start.AddDays(365.25 * 5);
        var accumulated = schedule.AccumulatedDepreciation(fiveYears).Amount;

        Assert.InRange(accumulated, 4_900m, 5_100m);
    }

    [Fact]
    public void StraightLine_PastEndOfLife_CapsAtDepreciableAmount()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.StraightLine,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(1_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
        };

        var wayPastLife = Start.AddYears(20);
        var accumulated = schedule.AccumulatedDepreciation(wayPastLife).Amount;
        var bookValue = schedule.BookValue(wayPastLife).Amount;

        Assert.Equal(9_000m, accumulated); // depreciable = 10000 - 1000
        Assert.Equal(1_000m, bookValue);   // floored at salvage
    }

    [Fact]
    public void BeforeStartDate_NothingDepreciated()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.StraightLine,
            Basis = Money.Usd(10_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
        };

        var before = Start.AddDays(-10);
        Assert.Equal(0m, schedule.AccumulatedDepreciation(before).Amount);
        Assert.Equal(10_000m, schedule.BookValue(before).Amount);
    }

    [Fact]
    public void None_ComputesZeroDepreciation()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.None,
            Basis = Money.Usd(10_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
        };

        var later = Start.AddYears(3);
        Assert.Equal(0m, schedule.AccumulatedDepreciation(later).Amount);
        Assert.Equal(10_000m, schedule.BookValue(later).Amount);
    }

    [Fact]
    public void DecliningBalance_FirstYear_AppliesRateToOpeningBookValue()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.DecliningBalance,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(0m),
            StartDate = Start,
            UsefulLifeYears = 5,
            DecliningBalanceRate = 0.40m, // double-declining for a 5-year life
        };

        // After exactly one full year: 10000 * 0.40 = 4000.
        var oneYear = Start.AddDays(365.25 + 1);
        var accumulated = schedule.AccumulatedDepreciation(oneYear).Amount;

        Assert.Equal(4_000m, accumulated);
    }

    [Fact]
    public void DecliningBalance_SecondYear_AppliesRateToDecliningBookValue()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.DecliningBalance,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(0m),
            StartDate = Start,
            UsefulLifeYears = 5,
            DecliningBalanceRate = 0.40m,
        };

        // Year 1: 4000. Year 2: (10000-4000)*0.40 = 2400. Accumulated = 6400.
        var twoYears = Start.AddDays((365.25 * 2) + 1);
        var accumulated = schedule.AccumulatedDepreciation(twoYears).Amount;

        Assert.Equal(6_400m, accumulated);
    }

    [Fact]
    public void DecliningBalance_DoesNotDepreciateBelowSalvage()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.DecliningBalance,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(8_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
            DecliningBalanceRate = 0.40m,
        };

        // Unbounded year-1 expense would be 4000, but the salvage floor caps the
        // depreciable amount at 2000.
        var oneYear = Start.AddDays(365.25 + 1);
        var accumulated = schedule.AccumulatedDepreciation(oneYear).Amount;

        Assert.Equal(2_000m, accumulated);
        Assert.Equal(8_000m, schedule.BookValue(oneYear).Amount);
    }

    [Fact]
    public void UnitsOfProduction_DepreciatesProportionalToUsage()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.UnitsOfProduction,
            Basis = Money.Usd(10_000m),
            SalvageValue = Money.Usd(0m),
            StartDate = Start,
            UsefulLifeYears = 5,
            ExpectedLifetimeUnits = 100_000m,
        };

        // 25,000 of 100,000 units consumed = 25% of the 10,000 depreciable amount.
        var accumulated = schedule.AccumulatedDepreciation(Start.AddYears(1), unitsConsumed: 25_000m).Amount;

        Assert.Equal(2_500m, accumulated);
    }

    [Fact]
    public void UnitsOfProduction_WithoutExpectedUnits_ComputesZero()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.UnitsOfProduction,
            Basis = Money.Usd(10_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
            ExpectedLifetimeUnits = null,
        };

        var accumulated = schedule.AccumulatedDepreciation(Start.AddYears(1), unitsConsumed: 50_000m).Amount;

        Assert.Equal(0m, accumulated);
    }

    [Fact]
    public void DepreciableAmount_SalvageExceedingBasis_FlooredAtZero()
    {
        var schedule = new DepreciationSchedule
        {
            Method = DepreciationMethod.StraightLine,
            Basis = Money.Usd(5_000m),
            SalvageValue = Money.Usd(8_000m),
            StartDate = Start,
            UsefulLifeYears = 5,
        };

        Assert.Equal(0m, schedule.DepreciableAmount.Amount);
        Assert.Equal(0m, schedule.AccumulatedDepreciation(Start.AddYears(3)).Amount);
    }
}
