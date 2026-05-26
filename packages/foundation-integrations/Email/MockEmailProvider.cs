using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// Mock <see cref="IEmailProvider"/> per ADR 0096 Step 1. Records every
/// dispatch in an in-memory inbox for test assertion access and writes a
/// structured log entry capturing the routing envelope only. Carries the
/// <see cref="IMockVendorProvider"/> marker so the substrate-tier
/// <see cref="DependencyInjection.MockProviderProductionGuardAssertion"/>
/// can identify it in production-environment composition.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Log discipline (ADR 0096 Step 1 floor; Rev 2 Amendment #6):</strong>
/// log entries capture ONLY <see cref="EmailMessage.To"/> /
/// <see cref="EmailMessage.Subject"/> / <see cref="EmailMessage.MessageStream"/>
/// + the substrate-generated <c>MessageId</c> + a redacted
/// <see cref="EmailMessage.IdempotencyKey"/> fingerprint. The log MUST NOT
/// emit <see cref="EmailMessage.BodyHtml"/> or
/// <see cref="EmailMessage.BodyText"/> — these may contain signup
/// verification links whose tokens complete authentication; logging the
/// body in load-test or closed-demo deployments running with
/// <c>SUNFISH_ALLOW_MOCK_PROVIDERS=true</c> leaks verification tokens via
/// stdout, allowing anyone with log access to complete a signup as
/// anyone. The <see cref="EmailMessage.IdempotencyKey"/> is fingerprinted
/// (last 4 chars + prefix length) — never logged in full — because the key
/// may carry user-derived entropy (ADR 0096 §"Vendor adapter security
/// floors" Floor 6).
/// </para>
/// <para>
/// <strong>Inbox inspection:</strong> the full message (including bodies)
/// is retained in <see cref="Inbox"/> for test assertion access — the
/// canonical inspection path is the in-memory store, NOT the log. Tests
/// reading the log to verify body content would defeat the log discipline
/// invariant.
/// </para>
/// <para>
/// <strong>Idempotency:</strong> when <see cref="EmailMessage.IdempotencyKey"/>
/// is non-null and a previous dispatch with the same key exists in
/// <see cref="Inbox"/>, the prior <see cref="EmailDispatchResult.MessageId"/>
/// is returned — matching Postmark's documented vendor-side idempotency
/// semantics.
/// </para>
/// </remarks>
public sealed class MockEmailProvider : IEmailProvider, IMockVendorProvider
{
    private readonly ConcurrentDictionary<string, EmailDispatchRecord> _byMessageId = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _byIdempotencyKey = new(StringComparer.Ordinal);
    private readonly List<string> _order = new();
    private readonly object _orderLock = new();
    private readonly ILogger<MockEmailProvider> _logger;

    /// <summary>Snapshot of every dispatched message in dispatch order. Test-only inspection path.</summary>
    public IReadOnlyList<EmailDispatchRecord> Inbox
    {
        get
        {
            lock (_orderLock)
            {
                return _order.Select(id => _byMessageId[id]).ToArray();
            }
        }
    }

    /// <summary>
    /// Constructs a <see cref="MockEmailProvider"/> with a no-op logger.
    /// Convenience overload for tests; DI registers the typed
    /// <see cref="ILogger{T}"/> variant.
    /// </summary>
    public MockEmailProvider() : this(NullLogger<MockEmailProvider>.Instance)
    {
    }

    /// <summary>Constructs a <see cref="MockEmailProvider"/> with an injected logger.</summary>
    public MockEmailProvider(ILogger<MockEmailProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (message.To.Count == 0)
        {
            return Task.FromResult(EmailDispatchResult.Rejected("To[] must contain at least one recipient."));
        }

        if (string.IsNullOrWhiteSpace(message.BodyHtml) && string.IsNullOrWhiteSpace(message.BodyText))
        {
            return Task.FromResult(EmailDispatchResult.Rejected("At least one of BodyHtml / BodyText must be non-empty."));
        }

        // Idempotency-key honouring per the Postmark vendor-parity contract:
        // a second SendAsync with the same key returns the prior dispatch's
        // MessageId without re-recording.
        if (!string.IsNullOrEmpty(message.IdempotencyKey)
            && _byIdempotencyKey.TryGetValue(message.IdempotencyKey, out var priorMessageId))
        {
            LogDispatch(priorMessageId, message, isIdempotentReplay: true);
            return Task.FromResult(EmailDispatchResult.Accepted(priorMessageId));
        }

        var messageId = "mock:" + Guid.NewGuid().ToString("N");
        _byMessageId[messageId] = new EmailDispatchRecord(messageId, message, DateTimeOffset.UtcNow);
        if (!string.IsNullOrEmpty(message.IdempotencyKey))
        {
            _byIdempotencyKey[message.IdempotencyKey] = messageId;
        }
        lock (_orderLock)
        {
            _order.Add(messageId);
        }

        LogDispatch(messageId, message, isIdempotentReplay: false);
        return Task.FromResult(EmailDispatchResult.Accepted(messageId));
    }

    /// <summary>
    /// Clears the in-memory inbox. Test convenience; not part of the
    /// <see cref="IEmailProvider"/> contract.
    /// </summary>
    public void ClearInbox()
    {
        _byMessageId.Clear();
        _byIdempotencyKey.Clear();
        lock (_orderLock)
        {
            _order.Clear();
        }
    }

    private void LogDispatch(string messageId, EmailMessage message, bool isIdempotentReplay)
    {
        // Log discipline (ADR 0096 Step 1 floor): To / Subject /
        // MessageStream / MessageId / idempotency fingerprint. NO bodies.
        var fingerprint = FingerprintIdempotencyKey(message.IdempotencyKey);
        var recipients = string.Join(",", message.To.Select(t => t.Address));

        _logger.LogInformation(
            "MockEmailProvider dispatch: messageId={MessageId} to={To} subject={Subject} stream={Stream} idempotency={IdempotencyFingerprint} replay={Replay}",
            messageId,
            recipients,
            message.Subject,
            message.MessageStream ?? "(default)",
            fingerprint,
            isIdempotentReplay);
    }

    /// <summary>
    /// Returns a redacted fingerprint of the idempotency key (last 4 chars +
    /// length prefix). Never returns the full key. Per ADR 0096 §"Vendor
    /// adapter security floors" Floor 6 — idempotency keys may carry
    /// user-derived entropy and become a re-identification vector.
    /// </summary>
    internal static string FingerprintIdempotencyKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "(none)";
        }

        if (key.Length <= 4)
        {
            return $"len={key.Length}";
        }

        return $"len={key.Length}/...{key[^4..]}";
    }
}

/// <summary>
/// One entry in <see cref="MockEmailProvider.Inbox"/> — the substrate-
/// generated <c>MessageId</c>, the full <see cref="EmailMessage"/>, and the
/// dispatch timestamp.
/// </summary>
public sealed record EmailDispatchRecord(string MessageId, EmailMessage Message, DateTimeOffset DispatchedAt);
