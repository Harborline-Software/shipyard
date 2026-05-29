using System.Text.Json;
using Sunfish.Blocks.Assets.Domain;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests.Domain;

public sealed class AssetIdTests
{
    [Fact]
    public void NewId_ProducesDistinctNonEmptyValues()
    {
        var a = AssetId.NewId();
        var b = AssetId.NewId();

        Assert.False(string.IsNullOrWhiteSpace(a.Value));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ImplicitConversions_RoundTrip()
    {
        AssetId id = "asset-42";
        string asString = id;

        Assert.Equal("asset-42", asString);
        Assert.Equal("asset-42", id.ToString());
    }

    [Fact]
    public void JsonConverter_SerializesAsBareString()
    {
        var id = new AssetId("asset-99");

        var json = JsonSerializer.Serialize(id);

        Assert.Equal("\"asset-99\"", json);
    }

    [Fact]
    public void JsonConverter_DeserializesFromBareString()
    {
        var id = JsonSerializer.Deserialize<AssetId>("\"asset-7\"");

        Assert.Equal(new AssetId("asset-7"), id);
    }
}
