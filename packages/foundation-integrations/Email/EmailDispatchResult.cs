using System;

namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Outcome of <see cref="IEmailProvider.SendAsync"/>. Closed discriminated union
/// (ADR 0096): exactly one of <see cref="Accepted"/>, <see cref="RateLimited"/>,
/// <see cref="Rejected"/>, or <see cref="TransportError"/>. The private
/// constructor keeps the hierarchy closed to these four cases.
/// </summary>
public abstract record EmailDispatchResult
{
    private EmailDispatchResult() { }

    /// <summary>The provider accepted the message for delivery.</summary>
    /// <param name="MessageId">Provider-assigned message identifier (for delivery tracking / webhooks).</param>
    public sealed record Accepted(string MessageId) : EmailDispatchResult;

    /// <summary>The provider rate-limited the request; retry after the indicated delay.</summary>
    /// <param name="RetryAfter">Server-advised backoff before retrying.</param>
    public sealed record RateLimited(TimeSpan RetryAfter) : EmailDispatchResult;

    /// <summary>The provider rejected the message (permanent; do not retry as-is).</summary>
    /// <param name="Reason">Provider-supplied rejection reason (safe to log / surface).</param>
    public sealed record Rejected(string Reason) : EmailDispatchResult;

    /// <summary>A transport-level failure occurred (network / TLS / timeout); may be transient.</summary>
    /// <param name="Detail">Diagnostic detail (no secrets; safe to log).</param>
    public sealed record TransportError(string Detail) : EmailDispatchResult;
}
