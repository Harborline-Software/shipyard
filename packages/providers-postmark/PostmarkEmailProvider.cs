using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Providers.Postmark;

/// <summary>
/// Postmark transactional-email adapter for <see cref="IEmailProvider"/> (ADR
/// 0096 Tier-2 vendor-provider substrate). Posts the finalized
/// <see cref="EmailMessage"/> to Postmark's documented
/// <c>POST /email</c> endpoint via <see cref="HttpClient"/> (no Postmark SDK
/// dependency) and maps the vendor response onto the 4-status
/// <see cref="EmailDispatchResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Real-adapter, NOT a mock:</strong> this type deliberately does NOT
/// implement <see cref="Sunfish.Foundation.Integrations.IMockVendorProvider"/>.
/// The marker asymmetry is the canonical mock-vs-real discriminator the
/// <see cref="Sunfish.Foundation.Integrations.DependencyInjection.MockProviderProductionGuardAssertion"/>
/// relies on; <c>UseVendorProviderIfConfigured</c> carries no marker constraint,
/// and a real adapter that wrongly implemented the marker would trip the
/// production guard. Enforced by a reflection test.
/// </para>
/// <para>
/// <strong>Secret handling (ADR 0096 floors F1/F2):</strong> the Postmark server
/// token is read from <see cref="PostmarkOptions.ServerToken"/> (bound via
/// <see cref="IOptionsMonitor{TOptions}"/>) and set ONLY on the
/// <c>X-Postmark-Server-Token</c> request header. It is never logged, never
/// placed in <see cref="EmailDispatchResult.ErrorDetail"/>, and never echoed in
/// an exception. A Postmark 401 (bad/missing token) maps to a generic
/// <c>Rejected("provider authentication failed")</c> with no token fragment.
/// </para>
/// <para>
/// <strong>Log discipline (F1/F3):</strong> structured logs capture the routing
/// envelope only — recipients / subject / message-stream / vendor MessageId +
/// a redacted idempotency fingerprint. Bodies are NEVER logged (they may carry
/// signup-verification or magic-link tokens whose disclosure completes
/// authentication — the same invariant the mock enforces). The idempotency key
/// is fingerprinted, never logged in full (Floor 6).
/// </para>
/// <para>
/// <strong>Provider-neutrality (F5 / ADR 0013):</strong> the adapter sends only
/// the finalized <see cref="EmailMessage"/>. It exposes no Postmark template
/// engine, scheduling (<c>DeliveryStartAt</c>), suppression list, or analytics
/// surface — content is rendered upstream.
/// </para>
/// </remarks>
public sealed class PostmarkEmailProvider : IEmailProvider
{
    /// <summary>Provider identifier for diagnostics / future telemetry tagging.</summary>
    public const string ProviderName = "postmark";

    private const string ServerTokenHeader = "X-Postmark-Server-Token";
    private const string IdempotencyKeyHeader = "X-PM-Message-Idempotency-Key";
    private const string SendEndpoint = "email";

    private readonly Func<HttpClient> _httpClientFactory;
    private readonly IOptionsMonitor<PostmarkOptions> _options;
    private readonly IOptionsMonitor<EmailDispatchOptions> _dispatchOptions;
    private readonly ILogger<PostmarkEmailProvider> _logger;

    /// <summary>
    /// DI constructor — resolves the named <see cref="HttpClient"/> from
    /// <see cref="IHttpClientFactory"/> per call so the adapter participates in
    /// the resilience pipeline + handler-lifetime rotation configured by
    /// <see cref="PostmarkEmailProviderServiceCollectionExtensions.AddPostmarkEmailProvider(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, string)"/>.
    /// This shape (factory, not a captured client) is required because the
    /// substrate's <c>UseVendorProviderIfConfigured</c> activates the adapter by
    /// implementation TYPE — every constructor parameter must be DI-resolvable,
    /// and a bare <see cref="HttpClient"/> is not.
    /// </summary>
    public PostmarkEmailProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PostmarkOptions> options,
        IOptionsMonitor<EmailDispatchOptions> dispatchOptions,
        ILogger<PostmarkEmailProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClientFactory = () => httpClientFactory.CreateClient(PostmarkOptions.HttpClientName);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dispatchOptions = dispatchOptions ?? throw new ArgumentNullException(nameof(dispatchOptions));
        _logger = logger ?? NullLogger<PostmarkEmailProvider>.Instance;
    }

    /// <summary>
    /// Test constructor — accepts a pre-built <see cref="HttpClient"/> (e.g.,
    /// over a stub handler) so unit tests can assert request shape + response
    /// mapping without an <see cref="IHttpClientFactory"/>.
    /// </summary>
    internal PostmarkEmailProvider(
        HttpClient httpClient,
        IOptionsMonitor<PostmarkOptions> options,
        IOptionsMonitor<EmailDispatchOptions> dispatchOptions,
        ILogger<PostmarkEmailProvider>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClientFactory = () => httpClient;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dispatchOptions = dispatchOptions ?? throw new ArgumentNullException(nameof(dispatchOptions));
        _logger = logger ?? NullLogger<PostmarkEmailProvider>.Instance;
    }

    /// <inheritdoc />
    public async Task<EmailDispatchResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (message.To.Count == 0)
        {
            return EmailDispatchResult.Rejected("To[] must contain at least one recipient.");
        }

        if (string.IsNullOrWhiteSpace(message.BodyHtml) && string.IsNullOrWhiteSpace(message.BodyText))
        {
            return EmailDispatchResult.Rejected("At least one of BodyHtml / BodyText must be non-empty.");
        }

        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ServerToken))
        {
            // Misconfiguration: the adapter swapped in but no token is bound.
            // Map to a generic transport error — never name or echo the (empty)
            // token. The production guard plus options validation should catch
            // this earlier; this is the defensive fallback.
            _logger.LogError("PostmarkEmailProvider invoked with no server token configured.");
            return EmailDispatchResult.TransportError("email provider is not configured");
        }

        var payload = BuildPayload(message);
        var httpClient = _httpClientFactory();

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
            {
                Content = JsonContent.Create(payload, options: PostmarkJson.Options),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // Secret set ONLY here, ONLY on the request header (F1/F2).
            request.Headers.TryAddWithoutValidation(ServerTokenHeader, options.ServerToken);
            // Idempotency key forwarded to Postmark's request-level dedup header
            // (F3). The full key travels to the vendor over TLS but is NEVER
            // logged in full — only a fingerprint reaches the structured log.
            if (!string.IsNullOrEmpty(message.IdempotencyKey))
            {
                request.Headers.TryAddWithoutValidation(IdempotencyKeyHeader, message.IdempotencyKey);
            }

            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            // Network-layer failure (DNS, connect, TLS). F2: the exception
            // message from HttpClient does not contain the token; we still map
            // to a generic transport error rather than surfacing ex.Message
            // verbatim to avoid leaking endpoint/internal detail to callers.
            _logger.LogWarning("PostmarkEmailProvider transport failure: {Reason}", ex.GetType().Name);
            return EmailDispatchResult.TransportError("transport failure contacting email provider");
        }
        catch (TaskCanceledException)
        {
            // HttpClient timeout surfaces as TaskCanceledException without the
            // caller's token being cancelled.
            _logger.LogWarning("PostmarkEmailProvider request timed out.");
            return EmailDispatchResult.TransportError("email provider request timed out");
        }

        using (response)
        {
            return await MapResponseAsync(response, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private PostmarkSendRequest BuildPayload(EmailMessage message)
    {
        var dispatch = _dispatchOptions.CurrentValue;

        // From: per-message override wins; else the substrate default.
        var from = FormatAddress(message.From) ?? FormatDefaultFrom(dispatch);
        var to = string.Join(",", message.To.Select(FormatAddress).Where(a => a is not null));
        var stream = message.MessageStream ?? dispatch.DefaultMessageStream;

        return new PostmarkSendRequest
        {
            From = from,
            To = to,
            Subject = message.Subject,
            HtmlBody = string.IsNullOrWhiteSpace(message.BodyHtml) ? null : message.BodyHtml,
            TextBody = string.IsNullOrWhiteSpace(message.BodyText) ? null : message.BodyText,
            MessageStream = string.IsNullOrWhiteSpace(stream) ? null : stream,
        };
    }

    private async Task<EmailDispatchResult> MapResponseAsync(
        HttpResponseMessage response,
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        PostmarkSendResponse? body = null;
        try
        {
            body = await response.Content
                .ReadFromJsonAsync<PostmarkSendResponse>(PostmarkJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException or HttpRequestException)
        {
            // Body unreadable — fall through to status-code-only mapping.
            _logger.LogWarning("PostmarkEmailProvider could not parse response body ({Status}).", (int)response.StatusCode);
        }

        // Success path: HTTP 200 with Postmark ErrorCode 0.
        if (response.StatusCode == HttpStatusCode.OK && body is { ErrorCode: 0 })
        {
            var messageId = string.IsNullOrWhiteSpace(body.MessageId)
                ? Guid.NewGuid().ToString("N")
                : body.MessageId!;
            LogDispatch(messageId, message);
            return EmailDispatchResult.Accepted(messageId);
        }

        // Rate limited.
        if (response.StatusCode == (HttpStatusCode)429 || body?.ErrorCode == 429)
        {
            var retryAfter = ReadRetryAfter(response);
            _logger.LogWarning("PostmarkEmailProvider rate-limited (retryAfter={RetryAfter}).", retryAfter);
            return EmailDispatchResult.RateLimited(retryAfter, "provider rate limit exceeded");
        }

        // Auth failure — F2: NEVER echo the token. Postmark ErrorCode 10 = bad
        // or missing API token; surfaces as HTTP 401 / 422 depending on tier.
        if (response.StatusCode == HttpStatusCode.Unauthorized || body?.ErrorCode == 10)
        {
            _logger.LogError("PostmarkEmailProvider authentication rejected by provider.");
            return EmailDispatchResult.Rejected("provider authentication failed");
        }

        // Server errors → transport error; caller MAY retry on its own outbox.
        if ((int)response.StatusCode >= 500)
        {
            _logger.LogWarning("PostmarkEmailProvider server error ({Status}).", (int)response.StatusCode);
            return EmailDispatchResult.TransportError($"provider server error (HTTP {(int)response.StatusCode})");
        }

        // Remaining 4xx with a Postmark ErrorCode → Rejected with a SANITIZED
        // message. Map the documented non-transient codes to stable reasons;
        // unknown codes fall back to the (Postmark-supplied, token-free) message.
        var reason = body?.ErrorCode switch
        {
            300 => "invalid email request",
            406 => "sender not authorized",
            412 => "recipient blocked",
            _ => Sanitize(body?.Message) ?? $"provider rejected request (HTTP {(int)response.StatusCode})",
        };
        _logger.LogWarning("PostmarkEmailProvider rejected request (errorCode={ErrorCode}).", body?.ErrorCode);
        return EmailDispatchResult.Rejected(reason);
    }

    private void LogDispatch(string messageId, EmailMessage message)
    {
        // Envelope only — NO bodies (F1). Idempotency key fingerprinted (F3).
        var recipients = string.Join(",", message.To.Select(t => t.Address));
        _logger.LogInformation(
            "PostmarkEmailProvider dispatch: provider={Provider} messageId={MessageId} to={To} subject={Subject} stream={Stream} idempotency={IdempotencyFingerprint}",
            ProviderName,
            messageId,
            recipients,
            message.Subject,
            message.MessageStream ?? "(default)",
            FingerprintIdempotencyKey(message.IdempotencyKey));
    }

    private static string? FormatAddress(EmailAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(address.DisplayName)
            ? address.Address
            : $"{address.DisplayName} <{address.Address}>";
    }

    private static string FormatDefaultFrom(EmailDispatchOptions dispatch)
        => string.IsNullOrWhiteSpace(dispatch.FromDisplayName)
            ? dispatch.FromAddress
            : $"{dispatch.FromDisplayName} <{dispatch.FromAddress}>";

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>
    /// Returns a redacted fingerprint of the idempotency key (length + last 4
    /// chars). Never returns the full key (ADR 0096 Floor 6 — keys may carry
    /// user-derived entropy). Mirrors the mock's fingerprint shape so log
    /// consumers see a uniform format across mock + real adapters.
    /// </summary>
    internal static string FingerprintIdempotencyKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "(none)";
        }

        return key.Length <= 4 ? $"len={key.Length}" : $"len={key.Length}/...{key[^4..]}";
    }

    /// <summary>
    /// Collapses a vendor-supplied error message to a single trimmed line and
    /// caps its length so an oversized provider payload cannot bloat
    /// <see cref="EmailDispatchResult.ErrorDetail"/>. Returns null for
    /// null/whitespace input.
    /// </summary>
    private static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        const int cap = 256;
        return oneLine.Length <= cap ? oneLine : oneLine[..cap];
    }
}
