using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Integrations;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.VendorProvider;

public sealed class MockVendorEnvVarRegistryTests
{
    [Fact]
    public void Register_ThenTryGet_ReturnsKey()
    {
        var registry = new MockVendorEnvVarRegistry();
        registry.Register(typeof(IEmailProvider), "POSTMARK_API_KEY");

        Assert.Equal("POSTMARK_API_KEY", registry.TryGetEnvVarKey(typeof(IEmailProvider)));
        Assert.Equal("POSTMARK_API_KEY", registry.Entries[typeof(IEmailProvider)]);
    }

    [Fact]
    public void TryGet_Unregistered_ReturnsNull()
        => Assert.Null(new MockVendorEnvVarRegistry().TryGetEnvVarKey(typeof(IEmailProvider)));

    [Fact]
    public void Register_Duplicate_LastWriteWins()
    {
        var registry = new MockVendorEnvVarRegistry();
        registry.Register(typeof(IEmailProvider), "OLD_KEY");
        registry.Register(typeof(IEmailProvider), "NEW_KEY");

        Assert.Equal("NEW_KEY", registry.TryGetEnvVarKey(typeof(IEmailProvider)));
    }
}

public sealed class MockEmailProviderTests
{
    private static EmailMessage Message(string? idempotencyKey = null) => new()
    {
        From = "no-reply@sunfish.example",
        To = new[] { "user@example.com" },
        Subject = "Verify your email",
        BodyHtml = "<a href='https://app/verify?token=secret'>verify</a>",
        MessageStream = "outbound",
        IdempotencyKey = idempotencyKey,
        EmailDispatchId = "dispatch-1",
    };

    [Fact]
    public async Task SendAsync_StoresMessage_ReturnsAccepted()
    {
        var provider = new MockEmailProvider();
        var result = await provider.SendAsync(Message(), CancellationToken.None);

        var accepted = Assert.IsType<EmailDispatchResult.Accepted>(result);
        Assert.False(string.IsNullOrWhiteSpace(accepted.MessageId));
        Assert.Single(provider.Sent);
    }

    [Fact]
    public async Task SendAsync_SameIdempotencyKey_DeDuplicates()
    {
        var provider = new MockEmailProvider();
        var r1 = (EmailDispatchResult.Accepted)await provider.SendAsync(Message("key-1"), CancellationToken.None);
        var r2 = (EmailDispatchResult.Accepted)await provider.SendAsync(Message("key-1"), CancellationToken.None);

        Assert.Equal(r1.MessageId, r2.MessageId);
        Assert.Single(provider.Sent); // second call did not add a new send
    }

    [Fact]
    public async Task SendAsync_DistinctKeys_BothSent()
    {
        var provider = new MockEmailProvider();
        await provider.SendAsync(Message("key-a"), CancellationToken.None);
        await provider.SendAsync(Message("key-b"), CancellationToken.None);

        Assert.Equal(2, provider.Sent.Count);
    }

    [Fact]
    public void MockEmailProvider_CarriesMockMarker()
        => Assert.IsAssignableFrom<IMockVendorProvider>(new MockEmailProvider());
}

public sealed class InMemoryCaptchaVerifierMarkerTests
{
    private static readonly IPAddress Ip = IPAddress.Parse("203.0.113.7");

    [Fact]
    public void CarriesMockMarker()
        => Assert.IsAssignableFrom<IMockVendorProvider>(new InMemoryCaptchaVerifier());

    [Fact]
    public async Task AlwaysPass_AnyTokenPasses()
    {
        var result = await InMemoryCaptchaVerifier.AlwaysPass().VerifyAsync("anything", Ip, CancellationToken.None);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task AlwaysFail_AnyTokenFails()
    {
        var result = await InMemoryCaptchaVerifier.AlwaysFail().VerifyAsync("anything", Ip, CancellationToken.None);
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task WithMagicToken_OnlyMagicPasses()
    {
        var verifier = InMemoryCaptchaVerifier.WithMagicToken("magic");
        Assert.True((await verifier.VerifyAsync("magic", Ip, CancellationToken.None)).Passed);
        Assert.False((await verifier.VerifyAsync("other", Ip, CancellationToken.None)).Passed);
    }
}
