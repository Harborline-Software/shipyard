namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Unidirectional fire-and-forget transactional email payload — per ADR 0096
/// §D2, structurally distinct from
/// <see cref="Messaging.IMessagingGateway"/>'s bidirectional thread-messaging
/// substrate (no <c>ThreadId</c>, no <c>Participant[]</c>, no inbound
/// webhook surface, pre-tenant scope).
/// </summary>
/// <remarks>
/// <para>
/// Use cases: welcome / verification / invitation / password-reset / receipt
/// emails issued from the public signup pipeline (ADR 0095
/// <c>IBootstrapContext</c> scope) or from post-tenant transactional flows
/// (invoice issued, lease renewal reminder, etc.).
/// </para>
/// <para>
/// <strong>Idempotency:</strong> <see cref="IdempotencyKey"/> is the
/// caller-supplied dedup token honoured by the vendor adapter (Postmark
/// supports request-level idempotency via dedicated headers; the mock
/// honours the key in its in-memory store). Substrate-tier semantics of the
/// key (substrate-maintained dedup table vs vendor-side dedup) are
/// forwarded to ADR 0096 Open Question 4.
/// </para>
/// <para>
/// <strong>MessageStream:</strong> free-form per ADR 0096 Halt 6 (the
/// initial taxonomy lands in W80 Stage-05). Postmark consumes the stream
/// to bucket transactional vs broadcast vs marketing; null/empty defaults
/// to the vendor's default stream.
/// </para>
/// </remarks>
public sealed record EmailMessage
{
    /// <summary>Sender address. MUST be a vendor-authorised From address (Postmark sender signature, etc.).</summary>
    public required EmailAddress From { get; init; }

    /// <summary>Recipient addresses. MUST contain at least one entry; vendor adapters MAY cap the array length.</summary>
    public required IReadOnlyList<EmailAddress> To { get; init; }

    /// <summary>Subject line. MUST be ≤RFC 5322 §2.1.1 998-character limit; vendor adapters MAY tighten.</summary>
    public required string Subject { get; init; }

    /// <summary>HTML body. MAY be null when <see cref="BodyText"/> is set; at least one body MUST be non-null.</summary>
    public string? BodyHtml { get; init; }

    /// <summary>Plain-text body. MAY be null when <see cref="BodyHtml"/> is set; at least one body MUST be non-null.</summary>
    public string? BodyText { get; init; }

    /// <summary>
    /// Free-form stream / category tag the vendor uses to bucket dispatch
    /// (Postmark: <c>MessageStream</c>). Null defaults to the vendor's
    /// default transactional stream. Per ADR 0096 Halt 6 (W80 Stage-05
    /// defines the initial taxonomy).
    /// </summary>
    public string? MessageStream { get; init; }

    /// <summary>
    /// Caller-supplied idempotency key. Honoured by the vendor adapter for
    /// retry deduplication. Substrate-tier: MUST NOT be logged at adapter
    /// tier (per ADR 0096 §"Vendor adapter security floors" Floor 6) —
    /// may carry user-derived entropy that becomes a re-identification
    /// vector.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Caller-side dispatch correlation ID. Distinct from the vendor's
    /// message ID returned in <see cref="EmailDispatchResult"/>.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// RFC 5322 mailbox: address + optional display name. Validates the address
/// shape on construction (non-empty, contains <c>@</c>). Vendor adapters MAY
/// apply stricter syntactic or DNS-MX validation.
/// </summary>
public sealed record EmailAddress
{
    /// <summary>The RFC 5322 address (e.g., <c>welcome@harborline.example</c>).</summary>
    public string Address { get; }

    /// <summary>Optional display name (e.g., <c>"Harborline Welcome"</c>).</summary>
    public string? DisplayName { get; }

    /// <summary>Constructs an <see cref="EmailAddress"/> with the given address + optional display name.</summary>
    public EmailAddress(string address, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (!address.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Email address must contain '@'.",
                nameof(address));
        }
        Address = address;
        DisplayName = displayName;
    }
}
