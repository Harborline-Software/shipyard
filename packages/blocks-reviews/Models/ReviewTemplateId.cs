using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Reviews.Models;

/// <summary>
/// Opaque identifier for an <see cref="ReviewTemplate"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(ReviewTemplateIdJsonConverter))]
public readonly record struct ReviewTemplateId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ReviewTemplateId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ReviewTemplateId id) => id.Value;

    /// <summary>Creates a new <see cref="ReviewTemplateId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static ReviewTemplateId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class ReviewTemplateIdJsonConverter : JsonConverter<ReviewTemplateId>
{
    public override ReviewTemplateId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ReviewTemplateId must be a non-null string.");
        return new ReviewTemplateId(str);
    }

    public override void Write(Utf8JsonWriter writer, ReviewTemplateId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
