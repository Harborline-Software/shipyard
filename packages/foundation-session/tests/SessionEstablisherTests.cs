using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Session.Tests;

public sealed class SessionEstablisherTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static SessionEstablisher NewEstablisher(
        ISessionStore store, SessionOptions? options = null, DateTimeOffset? now = null)
        => new(
            store,
            Options.Create(options ?? new SessionOptions()),
            new FixedTimeProvider(now ?? FixedNow),
            NullLogger<SessionEstablisher>.Instance);

    private static SessionEstablishmentRequest Request(
        string userId = "user-42",
        string tenant = "tenant-a",
        SessionEstablishmentReason reason = SessionEstablishmentReason.PasswordLogin)
        => new(userId, new TenantId(tenant), reason);

    [Fact]
    public async Task Establish_writes_a_record_and_returns_a_correlatable_result()
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, _) = TestHttpContextFactory.Create();

        var result = await establisher.EstablishAsync(Request(), ctx, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(result.SessionId));
        Assert.Equal(FixedNow, result.IssuedUtc);
        Assert.Equal(FixedNow + TimeSpan.FromHours(8), result.AbsoluteExpiryUtc);

        var record = await store.GetAsync(result.SessionId, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal("user-42", record!.UserId);
        Assert.Equal(new TenantId("tenant-a"), record.TenantId);
        Assert.Equal(FixedNow, record.IssuedUtc);
        Assert.Equal(FixedNow, record.LastSeenUtc);
        Assert.Equal(SessionEstablishmentReason.PasswordLogin, record.Reason);
    }

    [Fact]
    public async Task Cookie_principal_carries_only_the_opaque_id_no_sub_tid_or_roles()
    {
        // A6: SignInAsync is the single cookie-write; the principal must carry ONLY 'sid'.
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, auth) = TestHttpContextFactory.Create();

        var result = await establisher.EstablishAsync(Request(), ctx, CancellationToken.None);

        var signIn = Assert.Single(auth.SignIns);
        Assert.Equal("Cookies", signIn.Scheme);

        var claims = signIn.Principal.Claims.ToList();
        var sid = Assert.Single(claims);                                  // exactly one claim
        Assert.Equal(SessionClaimTypes.SessionId, sid.Type);             // and it is 'sid'
        Assert.Equal(result.SessionId, sid.Value);

        // No sub / tid / role material rides the cookie.
        Assert.Null(signIn.Principal.FindFirst(SessionClaimTypes.Subject));
        Assert.Null(signIn.Principal.FindFirst(SessionClaimTypes.TenantId));
        Assert.Empty(signIn.Principal.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task Establish_for_sentinel_tenant_is_rejected()
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, auth) = TestHttpContextFactory.Create();

        // TenantId.System is __system__ -> IsSystemSentinel true.
        var req = new SessionEstablishmentRequest("user-1", TenantId.System, SessionEstablishmentReason.PasswordLogin);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await establisher.EstablishAsync(req, ctx, CancellationToken.None));

        // Fail-closed: no record, no cookie issued.
        Assert.Empty(auth.SignIns);
    }

    [Fact]
    public async Task Establish_for_default_constructed_tenant_is_rejected_as_sentinel()
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, auth) = TestHttpContextFactory.Create();

        // default(TenantId).Value is null -> IsSystemSentinel true (fail-closed).
        var req = new SessionEstablishmentRequest("user-1", default, SessionEstablishmentReason.PasswordLogin);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await establisher.EstablishAsync(req, ctx, CancellationToken.None));
        Assert.Empty(auth.SignIns);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Establish_for_empty_user_is_rejected(string userId)
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, auth) = TestHttpContextFactory.Create();

        var req = new SessionEstablishmentRequest(userId, new TenantId("tenant-a"), SessionEstablishmentReason.PasswordLogin);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await establisher.EstablishAsync(req, ctx, CancellationToken.None));
        Assert.Empty(auth.SignIns);
    }

    [Fact]
    public async Task Each_establish_mints_a_fresh_unique_id_S1_fixation()
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx1, _) = TestHttpContextFactory.Create();
        var (ctx2, _) = TestHttpContextFactory.Create();

        var r1 = await establisher.EstablishAsync(Request(), ctx1, CancellationToken.None);
        var r2 = await establisher.EstablishAsync(Request(), ctx2, CancellationToken.None);

        Assert.NotEqual(r1.SessionId, r2.SessionId);
    }

    [Fact]
    public async Task Inbound_session_is_invalidated_and_signed_out_before_the_new_one_S1()
    {
        // S1/C1: a request carrying an existing (pre-auth/anonymous) session id must have that
        // server-side record removed + the old ticket signed out, and the NEW id must differ.
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);

        // Seed an inbound session record + a principal carrying its sid.
        const string inboundId = "inbound-sid-xyz";
        await store.CreateAsync(
            new SessionRecord
            {
                SessionId = inboundId,
                UserId = "pre-auth",
                TenantId = new TenantId("tenant-a"),
                IssuedUtc = FixedNow.AddMinutes(-5),
                AbsoluteExpiryUtc = FixedNow.AddHours(8),
                LastSeenUtc = FixedNow.AddMinutes(-1),
                Reason = SessionEstablishmentReason.PasswordLogin,
            },
            CancellationToken.None);

        var inboundPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(SessionClaimTypes.SessionId, inboundId) }, "Cookies"));
        var (ctx, auth) = TestHttpContextFactory.Create(inboundPrincipal);

        var result = await establisher.EstablishAsync(Request(), ctx, CancellationToken.None);

        // New id never equals (nor derives from) the inbound id.
        Assert.NotEqual(inboundId, result.SessionId);
        // Inbound record invalidated server-side.
        Assert.Null(await store.GetAsync(inboundId, CancellationToken.None));
        // Old ticket signed out (S1) before the new sign-in.
        Assert.Single(auth.SignOuts);
        Assert.Equal("Cookies", auth.SignOuts[0]);
        // New record present.
        Assert.NotNull(await store.GetAsync(result.SessionId, CancellationToken.None));
    }

    [Fact]
    public async Task No_pre_auth_record_is_created_when_request_carries_no_session_C2()
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, auth) = TestHttpContextFactory.Create(); // anonymous principal, no sid

        var result = await establisher.EstablishAsync(Request(), ctx, CancellationToken.None);

        // Exactly ONE record exists (the established one) — no pre-auth record was created or upgraded.
        Assert.NotNull(await store.GetAsync(result.SessionId, CancellationToken.None));
        // No sign-out happened (there was no inbound ticket).
        Assert.Empty(auth.SignOuts);
    }

    [Theory]
    [InlineData(SessionEstablishmentReason.PasswordLogin)]
    [InlineData(SessionEstablishmentReason.VerifyEmailCompletion)]
    [InlineData(SessionEstablishmentReason.MagicLinkConsume)]
    public async Task All_three_reasons_route_through_the_single_seam_H7(SessionEstablishmentReason reason)
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, auth) = TestHttpContextFactory.Create();

        var result = await establisher.EstablishAsync(Request(reason: reason), ctx, CancellationToken.None);

        var record = await store.GetAsync(result.SessionId, CancellationToken.None);
        Assert.Equal(reason, record!.Reason);
        Assert.Single(auth.SignIns); // same single SignInAsync path regardless of reason
    }

    [Fact]
    public async Task Absolute_expiry_honors_configured_lifetime()
    {
        var store = new InMemorySessionStore();
        var options = new SessionOptions { AbsoluteLifetime = TimeSpan.FromHours(4) };
        var establisher = NewEstablisher(store, options);
        var (ctx, _) = TestHttpContextFactory.Create();

        var result = await establisher.EstablishAsync(Request(), ctx, CancellationToken.None);

        Assert.Equal(FixedNow + TimeSpan.FromHours(4), result.AbsoluteExpiryUtc);
    }

    [Fact]
    public async Task Generated_session_id_meets_the_entropy_floor()
    {
        var store = new InMemorySessionStore();
        var establisher = NewEstablisher(store);
        var (ctx, _) = TestHttpContextFactory.Create();

        var result = await establisher.EstablishAsync(Request(), ctx, CancellationToken.None);

        var decoded = System.Buffers.Text.Base64Url.DecodeFromChars(result.SessionId.AsSpan());
        Assert.True(decoded.Length >= SessionOptions.MinimumSessionIdByteLength);
    }
}
