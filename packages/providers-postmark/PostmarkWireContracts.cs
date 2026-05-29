using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Providers.Postmark;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for the Postmark wire layer.
/// Postmark's API uses PascalCase property names, so no naming policy is
/// applied; null members are omitted so optional fields (HtmlBody, TextBody,
/// MessageStream) are not serialized when absent.
/// </summary>
internal static class PostmarkJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// Request body for Postmark's <c>POST /email</c> endpoint. Only the
/// provider-neutral fields the <see cref="Sunfish.Foundation.Integrations.Email.EmailMessage"/>
/// substrate carries are serialized — no Postmark template engine, scheduling,
/// suppression-list, or analytics fields are exposed (F5 / ADR 0013).
/// </summary>
internal sealed record PostmarkSendRequest
{
    [JsonPropertyName("From")]
    public required string From { get; init; }

    [JsonPropertyName("To")]
    public required string To { get; init; }

    [JsonPropertyName("Subject")]
    public required string Subject { get; init; }

    [JsonPropertyName("HtmlBody")]
    public string? HtmlBody { get; init; }

    [JsonPropertyName("TextBody")]
    public string? TextBody { get; init; }

    [JsonPropertyName("MessageStream")]
    public string? MessageStream { get; init; }
}

/// <summary>
/// Wire-shape of Postmark's <c>POST /email</c> response. Per Postmark docs:
/// <c>MessageID</c> (GUID), <c>ErrorCode</c> (0 = success), <c>Message</c>
/// (human-readable status / error), <c>To</c>, <c>SubmittedAt</c>. Only the
/// fields the adapter maps onto <see cref="Sunfish.Foundation.Integrations.Email.EmailDispatchResult"/>
/// are modeled.
/// </summary>
internal sealed record PostmarkSendResponse
{
    [JsonPropertyName("MessageID")]
    public string? MessageId { get; init; }

    [JsonPropertyName("ErrorCode")]
    public int ErrorCode { get; init; }

    [JsonPropertyName("Message")]
    public string? Message { get; init; }

    [JsonPropertyName("SubmittedAt")]
    public string? SubmittedAt { get; init; }

    [JsonPropertyName("To")]
    public string? To { get; init; }
}
