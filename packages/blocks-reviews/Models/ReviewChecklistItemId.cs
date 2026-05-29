using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Reviews.Models;

/// <summary>
/// Opaque identifier for an <see cref="ReviewChecklistItem"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(ReviewChecklistItemIdJsonConverter))]
public readonly record struct ReviewChecklistItemId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ReviewChecklistItemId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ReviewChecklistItemId id) => id.Value;

    /// <summary>Creates a new <see cref="ReviewChecklistItemId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static ReviewChecklistItemId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class ReviewChecklistItemIdJsonConverter : JsonConverter<ReviewChecklistItemId>
{
    public override ReviewChecklistItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ReviewChecklistItemId must be a non-null string.");
        return new ReviewChecklistItemId(str);
    }

    public override void Write(Utf8JsonWriter writer, ReviewChecklistItemId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
