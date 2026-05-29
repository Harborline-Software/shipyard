using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.Reviews.Models;

/// <summary>
/// Opaque identifier for an <see cref="ReviewReport"/> record.
/// Wire form: plain string (UUID recommended).
/// </summary>
[JsonConverter(typeof(ReviewReportIdJsonConverter))]
public readonly record struct ReviewReportId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ReviewReportId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ReviewReportId id) => id.Value;

    /// <summary>Creates a new <see cref="ReviewReportId"/> backed by a fresh <see cref="Guid"/>.</summary>
    public static ReviewReportId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class ReviewReportIdJsonConverter : JsonConverter<ReviewReportId>
{
    public override ReviewReportId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ReviewReportId must be a non-null string.");
        return new ReviewReportId(str);
    }

    public override void Write(Utf8JsonWriter writer, ReviewReportId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
