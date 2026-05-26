using System.Collections.Generic;

namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// A transactional email to dispatch via <see cref="IEmailProvider.SendAsync"/>
/// (ADR 0096). Vendor-neutral; adapters map this to their wire format.
/// </summary>
public sealed record EmailMessage
{
    /// <summary>Sender address (e.g. <c>"no-reply@sunfish.example"</c>).</summary>
    public required string From { get; init; }

    /// <summary>Recipient address(es). At least one is expected; adapter validates.</summary>
    public required IReadOnlyList<string> To { get; init; }

    /// <summary>Subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>HTML body; null when only a text body is supplied.</summary>
    public string? BodyHtml { get; init; }

    /// <summary>Plain-text body; null when only an HTML body is supplied.</summary>
    public string? BodyText { get; init; }

    /// <summary>
    /// Free-form message-stream selector (per ADR 0096 Halt 6) — e.g. a Postmark
    /// message stream id. Provider-interpreted; the substrate does not constrain
    /// its value.
    /// </summary>
    public required string MessageStream { get; init; }

    /// <summary>
    /// Optional caller-supplied idempotency key; null when not provided. When
    /// present, providers should de-duplicate retries by this key.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Stable dispatch identifier assigned by the caller for correlation across
    /// logs / audit / provider webhooks. Distinct from the provider-assigned
    /// <see cref="EmailDispatchResult.Accepted.MessageId"/>.
    /// </summary>
    public required string EmailDispatchId { get; init; }
}
