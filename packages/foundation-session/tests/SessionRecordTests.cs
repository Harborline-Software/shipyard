using System;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Session.Tests;

public sealed class SessionRecordTests
{
    private static SessionRecord NewRecord(DateTimeOffset issued, TimeSpan absolute) => new()
    {
        SessionId = "sid-abc",
        UserId = "user-1",
        TenantId = new TenantId("tenant-a"),
        IssuedUtc = issued,
        AbsoluteExpiryUtc = issued + absolute,
        LastSeenUtc = issued,
        Reason = SessionEstablishmentReason.PasswordLogin,
    };

    [Fact]
    public void Touch_produces_a_new_record_and_does_not_mutate_the_original()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var original = NewRecord(t0, TimeSpan.FromHours(8));

        var t1 = t0.AddMinutes(5);
        var touched = original.Touch(t1);

        Assert.NotSame(original, touched);
        Assert.Equal(t0, original.LastSeenUtc);           // original unchanged (immutable)
        Assert.Equal(t1, touched.LastSeenUtc);            // touched advanced
        // every other field preserved
        Assert.Equal(original.SessionId, touched.SessionId);
        Assert.Equal(original.UserId, touched.UserId);
        Assert.Equal(original.TenantId, touched.TenantId);
        Assert.Equal(original.IssuedUtc, touched.IssuedUtc);
        Assert.Equal(original.AbsoluteExpiryUtc, touched.AbsoluteExpiryUtc);
        Assert.Equal(original.Reason, touched.Reason);
    }

    [Fact]
    public void IsExpired_when_past_absolute_lifetime()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var record = NewRecord(t0, TimeSpan.FromHours(8));

        // Touched recently (no idle expiry) but past the absolute 8h.
        var now = t0.AddHours(8).AddSeconds(1);
        var recent = record.Touch(now.AddMinutes(-1));

        Assert.True(recent.IsExpired(now, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void IsExpired_when_idle_window_exceeded()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var record = NewRecord(t0, TimeSpan.FromHours(8)); // LastSeen == t0

        // Within the absolute window, but 31min since LastSeen with a 30min idle gate.
        var now = t0.AddMinutes(31);
        Assert.True(record.IsExpired(now, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void IsExpired_false_within_both_windows()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var record = NewRecord(t0, TimeSpan.FromHours(8));

        var now = t0.AddMinutes(20);
        Assert.False(record.IsExpired(now, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void Idle_expiry_boundary_is_exclusive_at_exactly_the_timeout()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var record = NewRecord(t0, TimeSpan.FromHours(8));

        // Exactly at the idle timeout: now - LastSeen == idleTimeout -> not yet expired (> is strict).
        var now = t0.AddMinutes(30);
        Assert.False(record.IsExpired(now, TimeSpan.FromMinutes(30)));
    }
}
