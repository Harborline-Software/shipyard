using Xunit;

namespace Sunfish.Providers.Postmark.Tests;

public sealed class PostmarkOptionsTests
{
    [Fact]
    public void ValidateBaseUrl_AcceptsHttpsDefault()
    {
        var options = new PostmarkOptions { ServerToken = "x" };
        Assert.Null(options.ValidateBaseUrl());
    }

    [Fact]
    public void ValidateBaseUrl_AcceptsHttpsEuHost()
    {
        var options = new PostmarkOptions { ServerToken = "x", BaseUrl = "https://api.eu.postmarkapp.com" };
        Assert.Null(options.ValidateBaseUrl());
    }

    [Theory]
    [InlineData("http://api.postmarkapp.com")]   // cleartext — F4 rejects
    [InlineData("ftp://api.postmarkapp.com")]
    public void ValidateBaseUrl_RejectsNonHttps(string baseUrl)
    {
        var options = new PostmarkOptions { ServerToken = "x", BaseUrl = baseUrl };
        var reason = options.ValidateBaseUrl();
        Assert.NotNull(reason);
        Assert.Contains("HTTPS", reason!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void ValidateBaseUrl_RejectsMalformed(string baseUrl)
    {
        var options = new PostmarkOptions { ServerToken = "x", BaseUrl = baseUrl };
        Assert.NotNull(options.ValidateBaseUrl());
    }

    [Fact]
    public void ValidateBaseUrl_FailureMessage_NeverContainsServerToken()
    {
        const string secret = "super-secret-server-token";
        var options = new PostmarkOptions { ServerToken = secret, BaseUrl = "http://insecure.example" };
        var reason = options.ValidateBaseUrl();
        Assert.NotNull(reason);
        Assert.DoesNotContain(secret, reason!);
    }
}
