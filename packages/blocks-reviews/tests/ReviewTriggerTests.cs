using System.Text.Json;
using Sunfish.Blocks.Reviews.Models;
using Xunit;

namespace Sunfish.Blocks.Reviews.Tests;

public class ReviewTriggerTests
{
    [Fact]
    public void All_five_values_present()
    {
        var values = Enum.GetValues<ReviewTrigger>();
        Assert.Equal(5, values.Length);
        Assert.Contains(ReviewTrigger.Annual, values);
        Assert.Contains(ReviewTrigger.MoveIn, values);
        Assert.Contains(ReviewTrigger.MoveOut, values);
        Assert.Contains(ReviewTrigger.PostRepair, values);
        Assert.Contains(ReviewTrigger.OnDemand, values);
    }

    [Fact]
    public void Json_round_trips_as_string_default()
    {
        // Default JSON-serializer of an enum is integer; verify that's the
        // shape we're committing to (matches existing enum patterns in this
        // package — DeficiencySeverity, DeficiencyStatus, ReviewPhase).
        var json = JsonSerializer.Serialize(ReviewTrigger.MoveIn);
        Assert.Equal(((int)ReviewTrigger.MoveIn).ToString(), json);

        var roundTripped = JsonSerializer.Deserialize<ReviewTrigger>(json);
        Assert.Equal(ReviewTrigger.MoveIn, roundTripped);
    }
}
