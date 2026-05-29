using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// Behavioral coverage for <see cref="Argon2idPasswordHasher{TUser}"/> — round-trip,
/// pepper-via-KnownSecret, parameter-floor upgrade trigger, legacy-V3 fallback, PHC parse
/// robustness, input-length bounds (S6), and Blazor-Server deadlock-safety (A5).
/// </summary>
public sealed class Argon2idPasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_round_trips_at_OWASP_minimum()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        const string password = "correct horse battery staple";

        var hash = hasher.HashPassword(TestUser.Instance, password);

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(TestUser.Instance, hash, password));
    }

    [Fact]
    public void Verify_fails_on_wrong_password()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        var hash = hasher.HashPassword(TestUser.Instance, "the-right-one");

        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(TestUser.Instance, hash, "a-different-one"));
    }

    [Fact]
    public void Two_hashes_of_same_password_differ_due_to_per_hash_salt()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        const string password = "salted-uniqueness";

        var a = hasher.HashPassword(TestUser.Instance, password);
        var b = hasher.HashPassword(TestUser.Instance, password);

        Assert.NotEqual(a, b);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(TestUser.Instance, a, password));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(TestUser.Instance, b, password));
    }

    [Fact]
    public void Pepper_via_KnownSecret_round_trips_and_wrong_pepper_fails()
    {
        var pepper = new byte[16];
        Array.Fill(pepper, (byte)0xAB);
        var peppered = new Argon2idPasswordHasher<TestUser>(
            Options.Create(new Argon2idHashOptions { Pepper = pepper }));

        const string password = "peppered-secret";
        var hash = peppered.HashPassword(TestUser.Instance, password);

        // Same pepper verifies.
        Assert.Equal(
            PasswordVerificationResult.Success,
            peppered.VerifyHashedPassword(TestUser.Instance, hash, password));

        // Different pepper fails (the KnownSecret participates in the hash construction).
        var wrongPepper = new byte[16];
        Array.Fill(wrongPepper, (byte)0xCD);
        var wrong = new Argon2idPasswordHasher<TestUser>(
            Options.Create(new Argon2idHashOptions { Pepper = wrongPepper }));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            wrong.VerifyHashedPassword(TestUser.Instance, hash, password));

        // No pepper at all also fails — confirms pepper is NOT XOR-ed into the password bytes
        // (an XOR scheme over a 16-char password would only affect the first 16 bytes; here
        // the secret participates in the whole construction so absence flips the result).
        var noPepper = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            noPepper.VerifyHashedPassword(TestUser.Instance, hash, password));
    }

    [Fact]
    public void Below_floor_parameters_in_stored_hash_trigger_SuccessRehashNeeded()
    {
        // Hash at the OWASP minimum (m=19456), then verify against a hasher whose floor is
        // m=46080 — the stored hash is below the verifying floor → parameter upgrade trigger.
        var lowFloor = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        const string password = "needs-rehash";
        var hash = lowFloor.HashPassword(TestUser.Instance, password);

        var highFloor = new Argon2idPasswordHasher<TestUser>(
            Options.Create(new Argon2idHashOptions { MemoryKib = 46080 }));

        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            highFloor.VerifyHashedPassword(TestUser.Instance, hash, password));
    }

    [Fact]
    public void At_or_above_floor_parameters_return_plain_Success()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        const string password = "at-floor";
        var hash = hasher.HashPassword(TestUser.Instance, password);

        // Verifying hasher at the SAME floor → Success (not SuccessRehashNeeded).
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(TestUser.Instance, hash, password));
    }

    [Fact]
    public void Legacy_V3_pbkdf2_hash_verifies_with_SuccessRehashNeeded()
    {
        // Pre-compute a known legacy ASP.NET Identity V3 hash via the BCL default.
        var legacy = new PasswordHasher<TestUser>(
            Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            }));
        const string password = "legacy-pbkdf2-password";
        var legacyHash = legacy.HashPassword(TestUser.Instance, password);

        var argon = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));

        // Legacy verify success → algorithm-upgrade trigger.
        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded,
            argon.VerifyHashedPassword(TestUser.Instance, legacyHash, password));

        // Wrong password against a legacy hash → Failed.
        Assert.Equal(
            PasswordVerificationResult.Failed,
            argon.VerifyHashedPassword(TestUser.Instance, legacyHash, "wrong"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-phc-and-not-base64-$$$")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$bm90LWJhc2U2NA$")] // empty hash section
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$$aGFzaA==")] // empty salt section
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2FsdA==$aGFzaA==")] // wrong algorithm token
    [InlineData("$argon2id$v=99$m=19456,t=2,p=1$c2FsdA==$aGFzaA==")] // wrong version
    [InlineData("$argon2id$v=19$m=abc,t=2,p=1$c2FsdA==$aGFzaA==")] // non-numeric param
    [InlineData("$argon2id$v=19$m=19456,t=2$c2FsdA==$aGFzaA==")] // missing p param
    public void Malformed_or_corrupt_hashes_return_Failed_without_throwing(string corrupt)
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));

        var result = hasher.VerifyHashedPassword(TestUser.Instance, corrupt, "any-candidate");

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void HashPassword_throws_ArgumentException_on_overlong_password()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        var overlong = new string('x', 4097); // > 4096 (S6).

        var ex = Assert.Throws<ArgumentException>(() => hasher.HashPassword(TestUser.Instance, overlong));
        Assert.Equal("password", ex.ParamName);
    }

    [Fact]
    public void VerifyHashedPassword_returns_Failed_on_overlong_provided_password()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        var hash = hasher.HashPassword(TestUser.Instance, "short-enough");
        var overlong = new string('x', 4097); // > 4096 (S6).

        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(TestUser.Instance, hash, overlong));
    }

    [Fact]
    public void VerifyHashedPassword_returns_Failed_on_overlong_stored_hash_without_parsing()
    {
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));
        var overlongHash = "$argon2id$" + new string('A', 1100); // > 1024 (S6).

        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(TestUser.Instance, overlongHash, "candidate"));
    }

    [Fact]
    public void HashPassword_completes_under_single_thread_SynchronizationContext_without_deadlock()
    {
        // A5 LOAD-BEARING — simulate Blazor Server's single-thread SynchronizationContext.
        // Without the Task.Run hop, awaiting Konscious's GetBytesAsync continuation on the
        // captured context could deadlock. The test asserts completion within a bound.
        var hasher = new Argon2idPasswordHasher<TestUser>(Options.Create(new Argon2idHashOptions()));

        using var dispatcher = new SingleThreadSynchronizationContext();
        string? hash = null;
        Exception? failure = null;

        var done = new ManualResetEventSlim(false);
        dispatcher.Post(_ =>
        {
            try
            {
                hash = hasher.HashPassword(TestUser.Instance, "blazor-server-probe");
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                done.Set();
            }
        }, null);

        var completed = done.Wait(TimeSpan.FromSeconds(30));

        Assert.True(completed, "HashPassword deadlocked under a single-thread SynchronizationContext");
        Assert.Null(failure);
        Assert.NotNull(hash);
    }

    /// <summary>A minimal pumping single-thread SynchronizationContext for the deadlock test.</summary>
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
        private readonly Thread _thread;

        public SingleThreadSynchronizationContext()
        {
            _thread = new Thread(Pump) { IsBackground = true };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        private void Pump()
        {
            SetSynchronizationContext(this);
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
        }
    }
}
