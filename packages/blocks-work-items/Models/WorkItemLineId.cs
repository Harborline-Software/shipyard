using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkItems.Models;

/// <summary>Strongly-typed identifier for a <see cref="WorkItemLine"/>. UUIDv7.</summary>
[JsonConverter(typeof(WorkItemLineIdJsonConverter))]
public readonly record struct WorkItemLineId(Guid Value)
{
    public override string ToString() => Value.ToString();
    public static WorkItemLineId NewId() => new(Guid.CreateVersion7());
    public static implicit operator Guid(WorkItemLineId id) => id.Value;
}

internal sealed class WorkItemLineIdJsonConverter : JsonConverter<WorkItemLineId>
{
    public override WorkItemLineId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("WorkItemLineId must be a non-null string.");
        return new WorkItemLineId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, WorkItemLineId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}
