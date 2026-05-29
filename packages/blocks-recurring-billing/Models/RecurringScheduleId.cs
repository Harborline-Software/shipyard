using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.RecurringBilling.Models;

/// <summary>Opaque identifier for a <see cref="RecurringSchedule"/>.</summary>
[JsonConverter(typeof(RecurringScheduleIdJsonConverter))]
public readonly record struct RecurringScheduleId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator RecurringScheduleId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(RecurringScheduleId id) => id.Value;

    /// <summary>Generates a new unique id backed by <see cref="Guid"/>.</summary>
    public static RecurringScheduleId NewId() => new(Guid.NewGuid().ToString());
}

internal sealed class RecurringScheduleIdJsonConverter : JsonConverter<RecurringScheduleId>
{
    public override RecurringScheduleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("RecurringScheduleId must be a non-null string.");
        return new RecurringScheduleId(str);
    }

    public override void Write(Utf8JsonWriter writer, RecurringScheduleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
