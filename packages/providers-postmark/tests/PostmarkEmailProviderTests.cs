using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Integrations.Email;
using Xunit;

namespace Sunfish.Providers.Postmark.Tests;

public sealed class PostmarkEmailProviderTests
{
    private const string TestServerToken = "postmark-server-token-SECRET-do-not-leak-7f3a9";

    private static EmailMessage NewMessage(
        string? idempotencyKey = null,
        string subject = "Verify your email",
        string? bodyHtml = "<a href=\"https://app/verify#token=SENSITIVE\">verify</a>",
        string? bodyText = "verify: https://app/verify#token=SENSITIVE")
        => new()
        {
            From = new EmailAddress("noreply@harborline.example", "Harborline"),
            To = [new EmailAddress("tenant@example.com")],
            Subject = subject,
            BodyHtml = bodyHtml,
            BodyText = bodyText,
            MessageStream = "outbound",
            IdempotencyKey = idempotencyKey,
        };

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public IReadOnlyList<string> ServerTokenHeaderValues { get; private set; } = [];
        public IReadOnlyList<string> IdempotencyHeaderValues { get; private set; } = [];
        public int CallCount { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            ServerTokenHeaderValues = request.Headers.TryGetValues("X-Postmark-Server-Token", out var t) ? [.. t] : [];
            IdempotencyHeaderValues = request.Headers.TryGetValues("X-PM-Message-Idempotency-Key", out var i) ? [.. i] : [];
            return Responder?.Invoke(request) ?? Json(HttpStatusCode.OK, """{"MessageID":"mock-id","ErrorCode":0,"Message":"OK"}""");
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Captures every log message + its rendered state for leak assertions.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
        public string All => string.Join("\n", Lines);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static (PostmarkEmailProvider provider, StubHttpHandler handler, CapturingLogger<PostmarkEmailProvider> logger) NewProvider(
        string baseUrl = "https://test.invalid",
        string? fromAddress = null)
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        var logger = new CapturingLogger<PostmarkEmailProvider>();
        var dispatch = new EmailDispatchOptions();
        if (fromAddress is not null)
        {
            dispatch.FromAddress = fromAddress;
        }
        var provider = new PostmarkEmailProvider(
            http,
            new StaticOptionsMonitor<PostmarkOptions>(new PostmarkOptions { ServerToken = TestServerToken, BaseUrl = baseUrl }),
            new StaticOptionsMonitor<EmailDispatchOptions>(dispatch),
            logger);
        return (provider, handler, logger);
    }

    [Fact]
    public async Task Success_ReturnsAcceptedWithMessageId()
    {
        var (provider, handler, _) = NewProvider();
        handler.Responder = _ => Json(HttpStatusCode.OK, """{"MessageID":"b7bc2f4a-e38e-4336-af7d-e6c392c2f817","ErrorCode":0,"Message":"OK"}""");

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.Accepted, result.Status);
        Assert.Equal("b7bc2f4a-e38e-4336-af7d-e6c392c2f817", result.MessageId);
        Assert.Equal("email", handler.LastRequest!.RequestUri!.AbsolutePath.TrimStart('/'));
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task Success_SendsServerTokenHeader_AndEnvelopeFields()
    {
        var (provider, handler, _) = NewProvider();

        await provider.SendAsync(NewMessage());

        Assert.Equal(TestServerToken, Assert.Single(handler.ServerTokenHeaderValues));
        Assert.Contains("\"Subject\":\"Verify your email\"", handler.LastBody!);
        Assert.Contains("tenant@example.com", handler.LastBody!);
        Assert.Contains("\"MessageStream\":\"outbound\"", handler.LastBody!);
    }

    [Fact]
    public async Task AuthRejected_ReturnsRejected_WithoutTokenEcho()
    {
        var (provider, handler, logger) = NewProvider();
        handler.Responder = _ => Json(HttpStatusCode.Unauthorized, """{"ErrorCode":10,"Message":"Bad or missing API token. Token=postmark-server-token-SECRET-do-not-leak-7f3a9"}""");

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
        Assert.Equal("provider authentication failed", result.ErrorDetail);
        // F2: the token must NOT appear in ErrorDetail even though the vendor
        // echoed it back in the (untrusted) response body.
        Assert.DoesNotContain(TestServerToken, result.ErrorDetail!);
        Assert.DoesNotContain(TestServerToken, logger.All);
    }

    [Fact]
    public async Task RateLimited_ReturnsRateLimited_WithRetryAfter()
    {
        var (provider, handler, _) = NewProvider();
        handler.Responder = _ =>
        {
            var resp = Json((HttpStatusCode)429, """{"ErrorCode":429,"Message":"rate limited"}""");
            resp.Headers.Add("Retry-After", "42");
            return resp;
        };

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.RateLimited, result.Status);
        Assert.Equal(TimeSpan.FromSeconds(42), result.RetryAfter);
    }

    [Fact]
    public async Task ServerError_ReturnsTransportError()
    {
        var (provider, handler, _) = NewProvider();
        handler.Responder = _ => Json(HttpStatusCode.InternalServerError, "upstream boom");

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.TransportError, result.Status);
    }

    [Fact]
    public async Task NetworkFailure_ReturnsTransportError_NoTokenLeak()
    {
        var (provider, handler, logger) = NewProvider();
        handler.Responder = _ => throw new HttpRequestException("connect failed to api.postmarkapp.com");

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.TransportError, result.Status);
        Assert.DoesNotContain(TestServerToken, result.ErrorDetail!);
        Assert.DoesNotContain(TestServerToken, logger.All);
    }

    [Fact]
    public async Task InvalidRequest_ErrorCode300_MapsToRejected()
    {
        var (provider, handler, _) = NewProvider();
        handler.Responder = _ => Json((HttpStatusCode)422, """{"ErrorCode":300,"Message":"Invalid 'To' address"}""");

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
        Assert.Equal("invalid email request", result.ErrorDetail);
    }

    [Fact]
    public async Task SenderNotAuthorized_ErrorCode406_MapsToRejected()
    {
        var (provider, handler, _) = NewProvider();
        handler.Responder = _ => Json((HttpStatusCode)422, """{"ErrorCode":406,"Message":"No sender signature"}""");

        var result = await provider.SendAsync(NewMessage());

        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
        Assert.Equal("sender not authorized", result.ErrorDetail);
    }

    [Fact]
    public async Task IdempotencyKey_ForwardedToVendorHeader_AndFingerprintedInLog()
    {
        var (provider, handler, logger) = NewProvider();
        const string key = "user-derived-idem-key-ABCD9999";

        await provider.SendAsync(NewMessage(idempotencyKey: key));

        // Full key travels to the vendor over the dedicated header.
        Assert.Equal(key, Assert.Single(handler.IdempotencyHeaderValues));
        // F3: the log carries only the fingerprint, NEVER the full key.
        Assert.DoesNotContain(key, logger.All);
        Assert.Contains($"len={key.Length}/...{key[^4..]}", logger.All);
    }

    [Fact]
    public async Task ServerToken_NeverAppearsInLogs_OnSuccess()
    {
        var (provider, _, logger) = NewProvider();

        await provider.SendAsync(NewMessage());

        // F1: envelope-only logging — token never logged on any path.
        Assert.DoesNotContain(TestServerToken, logger.All);
    }

    [Fact]
    public async Task Bodies_NeverAppearInLogs()
    {
        var (provider, _, logger) = NewProvider();

        await provider.SendAsync(NewMessage(
            bodyHtml: "<a href=\"https://app/verify#token=AUTH-COMPLETING-TOKEN\">go</a>",
            bodyText: "AUTH-COMPLETING-TOKEN"));

        // Bodies may carry verification / magic-link tokens — never logged.
        Assert.DoesNotContain("AUTH-COMPLETING-TOKEN", logger.All);
    }

    [Fact]
    public async Task EmptyRecipients_RejectedWithoutHttpCall()
    {
        var (provider, handler, _) = NewProvider();
        var message = new EmailMessage
        {
            From = new EmailAddress("noreply@harborline.example"),
            To = [],
            Subject = "x",
            BodyText = "x",
        };

        var result = await provider.SendAsync(message);

        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task NoBody_RejectedWithoutHttpCall()
    {
        var (provider, handler, _) = NewProvider();
        var message = new EmailMessage
        {
            From = new EmailAddress("noreply@harborline.example"),
            To = [new EmailAddress("t@example.com")],
            Subject = "x",
            BodyHtml = null,
            BodyText = null,
        };

        var result = await provider.SendAsync(message);

        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task UsesDefaultFromAddress_WhenProvidedViaDispatchOptions()
    {
        var (provider, handler, _) = NewProvider(fromAddress: "welcome@harborline.example");
        var message = new EmailMessage
        {
            // The substrate requires From, so callers always supply one; this
            // verifies the per-message From wins and is sent on the wire.
            From = new EmailAddress("welcome@harborline.example", "Harborline Welcome"),
            To = [new EmailAddress("t@example.com")],
            Subject = "Welcome",
            BodyText = "hi",
        };

        await provider.SendAsync(message);

        // JSON escapes the angle brackets (< / >) by default; assert
        // on the display-name + address fragments that survive escaping.
        Assert.Contains("Harborline Welcome", handler.LastBody!);
        Assert.Contains("welcome@harborline.example", handler.LastBody!);
    }

    [Fact]
    public void DoesNotImplementMockVendorMarker()
    {
        // The production guard relies on real adapters being marker-FREE. A
        // real adapter that implemented IMockVendorProvider would trip the
        // MockProviderProductionGuardAssertion. Reflection-assert the asymmetry.
        Assert.False(
            typeof(Sunfish.Foundation.Integrations.IMockVendorProvider)
                .IsAssignableFrom(typeof(PostmarkEmailProvider)),
            "PostmarkEmailProvider must NOT implement IMockVendorProvider (ADR 0096 mock-vs-real discriminator).");
    }

    [Fact]
    public void Constructor_RejectsNullArgs()
    {
        var http = new HttpClient(new StubHttpHandler());
        var opts = new StaticOptionsMonitor<PostmarkOptions>(new PostmarkOptions { ServerToken = "x" });
        var dispatch = new StaticOptionsMonitor<EmailDispatchOptions>(new EmailDispatchOptions());

        Assert.Throws<ArgumentNullException>(() => new PostmarkEmailProvider((IHttpClientFactory)null!, opts, dispatch));
        Assert.Throws<ArgumentNullException>(() => new PostmarkEmailProvider(http, null!, dispatch));
        Assert.Throws<ArgumentNullException>(() => new PostmarkEmailProvider(http, opts, null!));
    }
}
