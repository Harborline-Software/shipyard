using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Foundation.Integrations;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.Email;

public class MockEmailProviderTests
{
    private static EmailMessage WelcomeMessage(string? idempotencyKey = null) => new()
    {
        From = new EmailAddress("welcome@harborline.example", "Harborline"),
        To = new[] { new EmailAddress("test@example.com") },
        Subject = "Welcome to Harborline",
        BodyHtml = "<p>Welcome! Verify at <a href=\"https://example/verify?token=SECRET\">link</a></p>",
        BodyText = "Welcome! Verify at https://example/verify?token=SECRET",
        MessageStream = "transactional",
        IdempotencyKey = idempotencyKey,
        CorrelationId = "corr-" + Guid.NewGuid().ToString("N"),
    };

    [Fact]
    public void CarriesMockMarker()
    {
        Assert.IsAssignableFrom<IMockVendorProvider>(new MockEmailProvider());
    }

    [Fact]
    public async Task SendAsync_AcceptsMessage_ReturnsMockMessageId()
    {
        var provider = new MockEmailProvider();
        var result = await provider.SendAsync(WelcomeMessage());

        Assert.Equal(EmailDispatchStatus.Accepted, result.Status);
        Assert.NotNull(result.MessageId);
        Assert.StartsWith("mock:", result.MessageId);
    }

    [Fact]
    public async Task SendAsync_StoresInInbox()
    {
        var provider = new MockEmailProvider();
        var msg = WelcomeMessage();
        var result = await provider.SendAsync(msg);

        Assert.Single(provider.Inbox);
        Assert.Equal(result.MessageId, provider.Inbox[0].MessageId);
        Assert.Equal(msg.Subject, provider.Inbox[0].Message.Subject);
        Assert.Equal(msg.BodyHtml, provider.Inbox[0].Message.BodyHtml);
    }

    [Fact]
    public async Task SendAsync_RejectsEmptyToList()
    {
        var provider = new MockEmailProvider();
        var msg = new EmailMessage
        {
            From = new EmailAddress("welcome@harborline.example"),
            To = Array.Empty<EmailAddress>(),
            Subject = "Hi",
            BodyText = "body",
        };

        var result = await provider.SendAsync(msg);
        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task SendAsync_RejectsBothBodiesEmpty()
    {
        var provider = new MockEmailProvider();
        var msg = new EmailMessage
        {
            From = new EmailAddress("welcome@harborline.example"),
            To = new[] { new EmailAddress("test@example.com") },
            Subject = "Hi",
            // No BodyHtml, no BodyText.
        };

        var result = await provider.SendAsync(msg);
        Assert.Equal(EmailDispatchStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task SendAsync_IdempotencyKeyHonored_ReturnsSameMessageId()
    {
        var provider = new MockEmailProvider();
        var key = "idempotent-key-" + Guid.NewGuid().ToString("N");

        var first = await provider.SendAsync(WelcomeMessage(key));
        var second = await provider.SendAsync(WelcomeMessage(key));

        Assert.Equal(first.MessageId, second.MessageId);
        // Idempotency replay does NOT add a second inbox entry.
        Assert.Single(provider.Inbox);
    }

    [Fact]
    public async Task SendAsync_NoIdempotencyKey_TwoDispatchesAreIndependent()
    {
        var provider = new MockEmailProvider();
        var first = await provider.SendAsync(WelcomeMessage());
        var second = await provider.SendAsync(WelcomeMessage());

        Assert.NotEqual(first.MessageId, second.MessageId);
        Assert.Equal(2, provider.Inbox.Count);
    }

    [Fact]
    public async Task SendAsync_HonorsCancellation()
    {
        var provider = new MockEmailProvider();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.SendAsync(WelcomeMessage(), cts.Token));
    }

    [Fact]
    public async Task SendAsync_NullMessage_Throws()
    {
        var provider = new MockEmailProvider();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.SendAsync(null!));
    }

    [Fact]
    public async Task LogDiscipline_DoesNotEmitBodyHtmlOrBodyText()
    {
        var capturing = new CapturingLogger();
        var provider = new MockEmailProvider(capturing);
        await provider.SendAsync(WelcomeMessage(idempotencyKey: "test-idem-key-with-secret-12345678"));

        Assert.NotEmpty(capturing.Messages);
        foreach (var entry in capturing.Messages)
        {
            // Substrate-tier floor (ADR 0096 Rev 2 Amendment #6) — no bodies
            // in log. The verification link's "SECRET" token appears in both
            // BodyHtml and BodyText; if either leaked, the log message would
            // contain it.
            Assert.DoesNotContain("SECRET", entry, StringComparison.Ordinal);
            Assert.DoesNotContain("<p>Welcome!", entry, StringComparison.Ordinal);
            Assert.DoesNotContain("Verify at https://", entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task LogDiscipline_DoesNotEmitFullIdempotencyKey()
    {
        var capturing = new CapturingLogger();
        var provider = new MockEmailProvider(capturing);
        var fullKey = "test-idem-key-with-secret-12345678";
        await provider.SendAsync(WelcomeMessage(idempotencyKey: fullKey));

        Assert.NotEmpty(capturing.Messages);
        foreach (var entry in capturing.Messages)
        {
            // Floor 6: idempotency keys MUST NOT be logged in full.
            // The fingerprint exposes the last 4 chars + length — never the
            // middle of the key.
            Assert.DoesNotContain(fullKey, entry, StringComparison.Ordinal);
            Assert.DoesNotContain("test-idem-key-with-secret", entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FingerprintIdempotencyKey_NullOrEmpty_ReturnsNone()
    {
        Assert.Equal("(none)", MockEmailProvider.FingerprintIdempotencyKey(null));
        Assert.Equal("(none)", MockEmailProvider.FingerprintIdempotencyKey(string.Empty));
    }

    [Fact]
    public void FingerprintIdempotencyKey_ShortKey_ReturnsLengthOnly()
    {
        var fp = MockEmailProvider.FingerprintIdempotencyKey("abc");
        Assert.Equal("len=3", fp);
    }

    [Fact]
    public void FingerprintIdempotencyKey_LongKey_ReturnsLengthAndSuffix()
    {
        var fp = MockEmailProvider.FingerprintIdempotencyKey("abcdefghij");
        Assert.Equal("len=10/...ghij", fp);
    }

    [Fact]
    public void EmailAddress_ValidatesShape()
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress(""));
        Assert.Throws<ArgumentException>(() => new EmailAddress("no-at-sign"));
        var ok = new EmailAddress("a@b.com", "Display");
        Assert.Equal("a@b.com", ok.Address);
        Assert.Equal("Display", ok.DisplayName);
    }

    /// <summary>
    /// Capturing logger fixture — records the formatted message text of
    /// every log event so tests can assert what does (and does NOT) appear
    /// in log output.
    /// </summary>
    private sealed class CapturingLogger : ILogger<MockEmailProvider>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
