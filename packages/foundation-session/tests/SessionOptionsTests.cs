using System;
using Xunit;

namespace Sunfish.Foundation.Session.Tests;

public sealed class SessionOptionsTests
{
    [Fact]
    public void Defaults_match_the_sec_eng_ratified_floor()
    {
        var opts = new SessionOptions();

        Assert.Equal(TimeSpan.FromHours(8), opts.AbsoluteLifetime);   // S6: 8h absolute
        Assert.Equal(TimeSpan.FromMinutes(30), opts.IdleTimeout);     // S6: 30min idle
        Assert.Equal(32, opts.SessionIdByteLength);                   // S4: 256-bit default
    }

    [Fact]
    public void Defaults_pass_validation()
    {
        var ex = Record.Exception(() => new SessionOptions().Validate());
        Assert.Null(ex);
    }

    [Fact]
    public void Idle_greater_than_absolute_is_rejected()
    {
        var opts = new SessionOptions
        {
            AbsoluteLifetime = TimeSpan.FromMinutes(10),
            IdleTimeout = TimeSpan.FromMinutes(30),
        };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => opts.Validate());
        Assert.Equal(nameof(SessionOptions.IdleTimeout), ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_absolute_is_rejected(int hours)
    {
        var opts = new SessionOptions { AbsoluteLifetime = TimeSpan.FromHours(hours) };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => opts.Validate());
        Assert.Equal(nameof(SessionOptions.AbsoluteLifetime), ex.ParamName);
    }

    [Fact]
    public void Non_positive_idle_is_rejected()
    {
        var opts = new SessionOptions { IdleTimeout = TimeSpan.Zero };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => opts.Validate());
        Assert.Equal(nameof(SessionOptions.IdleTimeout), ex.ParamName);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(15)]
    public void Session_id_below_the_entropy_floor_is_rejected(int byteLength)
    {
        var opts = new SessionOptions { SessionIdByteLength = byteLength };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => opts.Validate());
        Assert.Equal(nameof(SessionOptions.SessionIdByteLength), ex.ParamName);
    }

    [Fact]
    public void Session_id_at_the_entropy_floor_is_accepted()
    {
        var opts = new SessionOptions
        {
            SessionIdByteLength = SessionOptions.MinimumSessionIdByteLength,
        };
        var ex = Record.Exception(() => opts.Validate());
        Assert.Null(ex);
    }

    [Fact]
    public void Entropy_floor_constant_is_128_bit()
        => Assert.Equal(16, SessionOptions.MinimumSessionIdByteLength);
}
