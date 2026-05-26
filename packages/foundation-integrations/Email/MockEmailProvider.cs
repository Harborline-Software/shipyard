using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Integrations.Email;

/// <summary>
/// In-process mock <see cref="IEmailProvider"/> for dev / test / closed-demo
/// deployments (ADR 0096). Carries <see cref="IMockVendorProvider"/> so the
/// production guard can detect it. Retains every sent <see cref="EmailMessage"/>
/// in memory — that store (not the log) is the canonical inspection path.
/// </summary>
/// <remarks>
/// <b>Log discipline (substrate-tier floor; ADR 0096 Rev 2 / sec-eng #6).</b>
/// When console logging is enabled, the log captures ONLY <c>To</c>,
/// <c>Subject</c>, <c>EmailDispatchId</c>, <c>IdempotencyKey</c>, and
/// <c>MessageStream</c>. It MUST NEVER log <c>BodyHtml</c> / <c>BodyText</c> —
/// signup verification links whose tokens complete authentication live in the
/// body; logging them under <c>SUNFISH_ALLOW_MOCK_PROVIDERS=true</c> would leak
/// verification tokens via stdout. Tests read <see cref="Sent"/>, not the log.
/// </remarks>
public sealed class MockEmailProvider : IEmailProvider, IMockVendorProvider
{
    private readonly object _gate = new();
    private readonly List<EmailMessage> _sent = new();
    private readonly Dictionary<string, string> _messageIdByIdempotencyKey = new(StringComparer.Ordinal);
    private readonly bool _logToConsole;

    /// <summary>Construct the mock; set <paramref name="logToConsole"/> to emit the disciplined (body-free) console log.</summary>
    public MockEmailProvider(bool logToConsole = false) => _logToConsole = logToConsole;

    /// <summary>Snapshot of messages accepted so far (canonical test-inspection path).</summary>
    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_gate) { return _sent.ToList(); } }
    }

    /// <inheritdoc />
    public Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            // Idempotency: same key → same prior result (no duplicate send).
            if (message.IdempotencyKey is { } key
                && _messageIdByIdempotencyKey.TryGetValue(key, out var priorId))
            {
                return Task.FromResult<EmailDispatchResult>(new EmailDispatchResult.Accepted(priorId));
            }

            var messageId = Guid.NewGuid().ToString("N");
            _sent.Add(message);
            if (message.IdempotencyKey is { } k)
            {
                _messageIdByIdempotencyKey[k] = messageId;
            }

            if (_logToConsole)
            {
                // Body fields deliberately omitted (verification-token leak floor).
                Console.WriteLine(
                    $"[MockEmailProvider] To={string.Join(",", message.To)} "
                    + $"Subject=\"{message.Subject}\" EmailDispatchId={message.EmailDispatchId} "
                    + $"IdempotencyKey={message.IdempotencyKey ?? "(none)"} "
                    + $"MessageStream={message.MessageStream} MessageId={messageId}");
            }

            return Task.FromResult<EmailDispatchResult>(new EmailDispatchResult.Accepted(messageId));
        }
    }
}
