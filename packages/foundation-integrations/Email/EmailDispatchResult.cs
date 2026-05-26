namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Outcome of an <see cref="IEmailProvider.SendAsync(EmailMessage, CancellationToken)"/>
/// call. Discriminated by <see cref="Status"/>; <see cref="MessageId"/> is
/// populated on success; <see cref="ErrorDetail"/> is populated on
/// non-success outcomes.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR 0096 §D2 the outcome shape is unidirectional fire-and-forget —
/// no follow-up callback, no thread state. <see cref="MessageId"/> is the
/// vendor's tracking ID (Postmark <c>MessageID</c> GUID; mock returns a
/// substrate-generated GUID prefixed with <c>"mock:"</c>) so downstream
/// audit emission has a correlation token.
/// </para>
/// <para>
/// <see cref="EmailDispatchStatus.RateLimited"/> + <see cref="RetryAfter"/>
/// carry the vendor's retry-after hint when honoured by the adapter; callers
/// MAY schedule retries on their own outbox / queue infrastructure.
/// </para>
/// </remarks>
public sealed record EmailDispatchResult
{
    /// <summary>Outcome status.</summary>
    public required EmailDispatchStatus Status { get; init; }

    /// <summary>Vendor-assigned message identifier — populated when <see cref="Status"/> is <see cref="EmailDispatchStatus.Accepted"/>.</summary>
    public string? MessageId { get; init; }

    /// <summary>Vendor-supplied retry-after hint — populated when <see cref="Status"/> is <see cref="EmailDispatchStatus.RateLimited"/>.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Vendor or adapter-supplied error detail — populated when <see cref="Status"/> is non-success.</summary>
    public string? ErrorDetail { get; init; }

    /// <summary>Convenience constructor for the success path.</summary>
    public static EmailDispatchResult Accepted(string messageId)
        => new() { Status = EmailDispatchStatus.Accepted, MessageId = messageId };

    /// <summary>Convenience constructor for the rate-limited path.</summary>
    public static EmailDispatchResult RateLimited(TimeSpan? retryAfter = null, string? detail = null)
        => new() { Status = EmailDispatchStatus.RateLimited, RetryAfter = retryAfter, ErrorDetail = detail };

    /// <summary>Convenience constructor for the rejected path.</summary>
    public static EmailDispatchResult Rejected(string reason)
        => new() { Status = EmailDispatchStatus.Rejected, ErrorDetail = reason };

    /// <summary>Convenience constructor for the transport-error path.</summary>
    public static EmailDispatchResult TransportError(string detail)
        => new() { Status = EmailDispatchStatus.TransportError, ErrorDetail = detail };
}

/// <summary>
/// Outcome enum for <see cref="EmailDispatchResult"/>.
/// </summary>
public enum EmailDispatchStatus
{
    /// <summary>Vendor accepted the message for dispatch. <see cref="EmailDispatchResult.MessageId"/> is populated.</summary>
    Accepted = 0,

    /// <summary>Vendor rejected the message for non-transient reasons (bad address, blocked content, etc.).</summary>
    Rejected = 1,

    /// <summary>Vendor rate-limited the call. Caller MAY retry after <see cref="EmailDispatchResult.RetryAfter"/>.</summary>
    RateLimited = 2,

    /// <summary>Adapter-level transport failure (timeout, network error, vendor 5xx). Caller MAY retry.</summary>
    TransportError = 3,
}
