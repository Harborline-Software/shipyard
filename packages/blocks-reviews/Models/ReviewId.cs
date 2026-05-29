using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Reviews.Models;

/// <summary>
/// Opaque identifier for an <see cref="Review"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(ReviewIdJsonConverter))]
public readonly record struct ReviewId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ReviewId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ReviewId id) => id.Value;

    /// <summary>Creates a new <see cref="ReviewId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static ReviewId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class ReviewIdJsonConverter : JsonConverter<ReviewId>
{
    public override ReviewId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ReviewId must be a non-null string.");
        return new ReviewId(str);
    }

    public override void Write(Utf8JsonWriter writer, ReviewId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
