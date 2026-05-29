using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Blocks.WorkItems.Models;

/// <summary>
/// Strongly-typed identifier for a <see cref="WorkItem"/>. Backed by
/// a UUIDv7 (sortable by mint-time) per
/// <c>_shared/engineering/crdt-friendly-schema-conventions.md</c> §1.
/// </summary>
// Inspired by Apache OFBiz WorkEffort module (Apache 2.0) — clean-room expression.
[JsonConverter(typeof(WorkItemIdJsonConverter))]
public readonly record struct WorkItemId(Guid Value)
{
    public override string ToString() => Value.ToString();

    /// <summary>Mint a fresh time-sortable id (UUIDv7).</summary>
    public static WorkItemId NewId() => new(Guid.CreateVersion7());

    public static implicit operator Guid(WorkItemId id) => id.Value;
}

internal sealed class WorkItemIdJsonConverter : JsonConverter<WorkItemId>
{
    public override WorkItemId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("WorkItemId must be a non-null string.");
        return new WorkItemId(Guid.Parse(str));
    }

    public override void Write(Utf8JsonWriter writer, WorkItemId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value.ToString());
}
