using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Sunfish.Foundation.Session.Tests;

/// <summary>
/// S4 entropy + encoding discipline. <see cref="SessionIdGenerator"/> is internal; reached via
/// <c>InternalsVisibleTo</c>.
/// </summary>
public sealed class SessionIdGeneratorTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(48)]
    public void Generated_id_decodes_to_the_requested_byte_length(int byteLength)
    {
        var id = SessionIdGenerator.Generate(byteLength);

        // base64url round-trips back to exactly byteLength bytes.
        var decoded = Base64Url.DecodeFromChars(id.AsSpan());
        Assert.Equal(byteLength, decoded.Length);
    }

    [Fact]
    public void Generated_id_is_url_safe_base64url_no_padding()
    {
        var id = SessionIdGenerator.Generate(32);

        // base64url alphabet: A-Z a-z 0-9 - _ ; never + / = (the standard-base64 / padding chars).
        Assert.DoesNotContain('+', id);
        Assert.DoesNotContain('/', id);
        Assert.DoesNotContain('=', id);
        Assert.All(id, c => Assert.True(
            char.IsLetterOrDigit(c) || c == '-' || c == '_',
            $"Unexpected character '{c}' in base64url id."));
    }

    [Fact]
    public void Byte_length_below_the_floor_is_hard_floored_defensively()
    {
        // Even if called below the floor, the generator clamps to the 128-bit minimum rather
        // than emitting a weak id.
        var id = SessionIdGenerator.Generate(4);
        var decoded = Base64Url.DecodeFromChars(id.AsSpan());
        Assert.Equal(SessionOptions.MinimumSessionIdByteLength, decoded.Length);
    }

    [Fact]
    public void Generated_ids_are_unique_across_many_draws()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 10_000; i++)
        {
            Assert.True(ids.Add(SessionIdGenerator.Generate(32)), "CSPRNG produced a duplicate id.");
        }
    }
}
