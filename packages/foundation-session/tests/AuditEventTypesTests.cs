using Xunit;

namespace Sunfish.Foundation.Session.Tests;

public sealed class AuditEventTypesTests
{
    [Theory]
    [InlineData(SessionEstablishmentReason.PasswordLogin, "Auth.SessionEstablished.PasswordLogin")]
    [InlineData(SessionEstablishmentReason.VerifyEmailCompletion, "Auth.SessionEstablished.VerifyEmail")]
    [InlineData(SessionEstablishmentReason.MagicLinkConsume, "Auth.SessionEstablished.MagicLink")]
    public void SessionEstablishedFor_maps_each_reason_to_its_canonical_event_type(
        SessionEstablishmentReason reason, string expected)
        => Assert.Equal(expected, AuditEventTypes.SessionEstablishedFor(reason));

    [Fact]
    public void Constants_are_the_stable_dotted_identifiers()
    {
        Assert.Equal("Auth.LoginFailed", AuditEventTypes.LoginFailed);
        Assert.Equal("Auth.SignedOut", AuditEventTypes.SignedOut);
        Assert.Equal("Auth.SessionRevoked", AuditEventTypes.SessionRevoked);
        Assert.Equal("Auth.SessionTenantMismatch", AuditEventTypes.SessionTenantMismatch);
    }
}
