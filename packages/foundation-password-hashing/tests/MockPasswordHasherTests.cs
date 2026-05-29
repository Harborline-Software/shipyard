using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Coverage for <see cref="MockPasswordHasher{TUser}"/> — constant-string format (S2),
/// round-trip, and the zero-password-derived-material invariant (Floor 7).
/// </summary>
public sealed class MockPasswordHasherTests
{
    private readonly MockPasswordHasher<TestUser> _mock = new();

    [Fact]
    public void HashPassword_returns_the_constant_mock_string()
    {
        Assert.Equal("mock-hash", _mock.HashPassword(TestUser.Instance, "anything"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a-much-longer-password-with-symbols-!@#$%^&*()")]
    public void Stored_hash_embeds_no_password_derived_material(string password)
    {
        // Floor 7 by construction — regardless of the input, the stored hash is literally
        // the constant. No length, first-byte, or character-class proxy leaks.
        Assert.Equal(MockPasswordHasher<TestUser>.MockHash, _mock.HashPassword(TestUser.Instance, password));
    }

    [Fact]
    public void Verify_succeeds_for_mock_hash_with_non_empty_candidate()
    {
        var hash = _mock.HashPassword(TestUser.Instance, "pw");
        Assert.Equal(
            PasswordVerificationResult.Success,
            _mock.VerifyHashedPassword(TestUser.Instance, hash, "any-non-empty"));
    }

    [Fact]
    public void Verify_fails_on_empty_provided_password()
    {
        Assert.Equal(
            PasswordVerificationResult.Failed,
            _mock.VerifyHashedPassword(TestUser.Instance, MockPasswordHasher<TestUser>.MockHash, ""));
    }

    [Fact]
    public void Verify_fails_on_non_mock_stored_hash()
    {
        Assert.Equal(
            PasswordVerificationResult.Failed,
            _mock.VerifyHashedPassword(TestUser.Instance, "not-the-mock-hash", "candidate"));
    }

    [Fact]
    public void Verify_never_returns_SuccessRehashNeeded()
    {
        // Mock hashes are not subject to the parameter-floor upgrade discipline.
        var result = _mock.VerifyHashedPassword(
            TestUser.Instance, MockPasswordHasher<TestUser>.MockHash, "candidate");
        Assert.NotEqual(PasswordVerificationResult.SuccessRehashNeeded, result);
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void Carries_the_IMockPasswordHasher_marker()
    {
        Assert.IsAssignableFrom<IMockPasswordHasher>(_mock);
    }
}
