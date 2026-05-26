using Sunfish.Foundation.Integrations.DependencyInjection;
using Sunfish.Foundation.Integrations.Email;

namespace Sunfish.Foundation.Integrations.Tests.DependencyInjection;

public class MockVendorEnvVarRegistryTests
{
    [Fact]
    public void Register_RecordsMapping_TryGetReturnsIt()
    {
        var registry = new MockVendorEnvVarRegistry();
        registry.Register(typeof(IEmailProvider), "POSTMARK_API_KEY");

        Assert.True(registry.TryGet(typeof(IEmailProvider), out var key));
        Assert.Equal("POSTMARK_API_KEY", key);
    }

    [Fact]
    public void TryGet_UnknownContract_ReturnsFalse()
    {
        var registry = new MockVendorEnvVarRegistry();

        Assert.False(registry.TryGet(typeof(IEmailProvider), out var key));
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void Register_DuplicateContract_LastWriterWins()
    {
        var registry = new MockVendorEnvVarRegistry();
        registry.Register(typeof(IEmailProvider), "POSTMARK_API_KEY_V1");
        registry.Register(typeof(IEmailProvider), "POSTMARK_API_KEY_V2");

        Assert.True(registry.TryGet(typeof(IEmailProvider), out var key));
        Assert.Equal("POSTMARK_API_KEY_V2", key);
    }

    [Fact]
    public void Entries_ReturnsAllInRegistrationOrder()
    {
        var registry = new MockVendorEnvVarRegistry();
        registry.Register(typeof(IEmailProvider), "POSTMARK_API_KEY");
        registry.Register(typeof(Captcha.ICaptchaVerifier), "TURNSTILE_SECRET_KEY");

        var entries = registry.Entries.ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(typeof(IEmailProvider), entries[0].ContractType);
        Assert.Equal("POSTMARK_API_KEY", entries[0].EnvVarKey);
        Assert.Equal(typeof(Captcha.ICaptchaVerifier), entries[1].ContractType);
        Assert.Equal("TURNSTILE_SECRET_KEY", entries[1].EnvVarKey);
    }

    [Fact]
    public void Register_NullContractType_Throws()
    {
        var registry = new MockVendorEnvVarRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, "POSTMARK_API_KEY"));
    }

    [Fact]
    public void Register_NullOrWhitespaceEnvVarKey_Throws()
    {
        var registry = new MockVendorEnvVarRegistry();
        // ArgumentNullException : ArgumentException — use ThrowsAny to
        // accept both the null and whitespace branches uniformly.
        Assert.ThrowsAny<ArgumentException>(() => registry.Register(typeof(IEmailProvider), ""));
        Assert.ThrowsAny<ArgumentException>(() => registry.Register(typeof(IEmailProvider), "   "));
        Assert.ThrowsAny<ArgumentException>(() => registry.Register(typeof(IEmailProvider), null!));
    }
}
