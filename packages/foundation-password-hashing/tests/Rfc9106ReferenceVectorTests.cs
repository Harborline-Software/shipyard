using System.Reflection;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using Xunit;

namespace Sunfish.Foundation.PasswordHashing.Tests;

/// <summary>
/// RFC 9106 §5 reference-vector parity (≥3 vectors; ADR 0097 S4 sec-eng substrate
/// amendment) + Argon2id primitive-selection assertion (S5). The reference vectors pin the
/// Konscious primitive against the immutable published spec so a future Konscious bump that
/// silently drifts from spec conformance is caught at substrate tier. The primitive-selection
/// assertion verifies the substrate instantiates <c>Argon2id</c> specifically — the
/// hardcoded PHC prefix does NOT cross-verify the underlying variant.
/// </summary>
public sealed class Rfc9106ReferenceVectorTests
{
    // RFC 9106 §5 shared inputs (identical across the 5.1 / 5.2 / 5.3 vectors).
    private static readonly byte[] Password = Repeat(0x01, 32);
    private static readonly byte[] Salt = Repeat(0x02, 16);
    private static readonly byte[] Secret = Repeat(0x03, 8);
    private static readonly byte[] AssociatedData = Repeat(0x04, 12);
    private const int MemoryKib = 32;
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int TagLength = 32;

    [Fact]
    public void Rfc9106_5_3_Argon2id_reference_vector_matches()
    {
        // RFC 9106 §5.3 Argon2id Tag.
        var expected = HexBytes(
            "0d 64 0d f5 8d 78 76 6c 08 c0 37 a3 4a 8b 53 c9 "
            + "d0 1e f0 45 2d 75 b6 5e b5 25 20 e9 6b 01 e6 59");

        using var argon2id = new Argon2id(Password)
        {
            Salt = Salt,
            KnownSecret = Secret,
            AssociatedData = AssociatedData,
            Iterations = Iterations,
            MemorySize = MemoryKib,
            DegreeOfParallelism = Parallelism,
        };

        var actual = argon2id.GetBytes(TagLength);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rfc9106_5_2_Argon2i_reference_vector_matches()
    {
        // RFC 9106 §5.2 Argon2i Tag — confirms the spec-conformant input setup is correct
        // and the three variants are genuinely distinct (Floor 8 rationale).
        var expected = HexBytes(
            "c8 14 d9 d1 dc 7f 37 aa 13 f0 d7 7f 24 94 bd a1 "
            + "c8 de 6b 01 6d d3 88 d2 99 52 a4 c4 67 2b 6c e8");

        using var argon2i = new Argon2i(Password)
        {
            Salt = Salt,
            KnownSecret = Secret,
            AssociatedData = AssociatedData,
            Iterations = Iterations,
            MemorySize = MemoryKib,
            DegreeOfParallelism = Parallelism,
        };

        var actual = argon2i.GetBytes(TagLength);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Rfc9106_5_1_Argon2d_reference_vector_matches()
    {
        // RFC 9106 §5.1 Argon2d Tag.
        var expected = HexBytes(
            "51 2b 39 1b 6f 11 62 97 53 71 d3 09 19 73 42 94 "
            + "f8 68 e3 be 39 84 f3 c1 a1 3a 4d b9 fa be 4a cb");

        using var argon2d = new Argon2d(Password)
        {
            Salt = Salt,
            KnownSecret = Secret,
            AssociatedData = AssociatedData,
            Iterations = Iterations,
            MemorySize = MemoryKib,
            DegreeOfParallelism = Parallelism,
        };

        var actual = argon2d.GetBytes(TagLength);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void The_three_variants_produce_distinct_tags_for_identical_input()
    {
        // Floor 8 rationale — Argon2id is NOT Argon2i and NOT Argon2d; a typo in the
        // substrate that instantiated the wrong variant must be observable.
        using var argon2id = new Argon2id(Password) { Salt = Salt, KnownSecret = Secret, AssociatedData = AssociatedData, Iterations = Iterations, MemorySize = MemoryKib, DegreeOfParallelism = Parallelism };
        using var argon2i = new Argon2i(Password) { Salt = Salt, KnownSecret = Secret, AssociatedData = AssociatedData, Iterations = Iterations, MemorySize = MemoryKib, DegreeOfParallelism = Parallelism };
        using var argon2d = new Argon2d(Password) { Salt = Salt, KnownSecret = Secret, AssociatedData = AssociatedData, Iterations = Iterations, MemorySize = MemoryKib, DegreeOfParallelism = Parallelism };

        var id = argon2id.GetBytes(TagLength);
        var i = argon2i.GetBytes(TagLength);
        var d = argon2d.GetBytes(TagLength);

        Assert.NotEqual(id, i);
        Assert.NotEqual(id, d);
        Assert.NotEqual(i, d);
    }

    [Fact]
    public void Argon2idPasswordHasher_selects_the_Argon2id_variant_specifically()
    {
        // S5 primitive-selection assertion. The PHC wire prefix is hardcoded "$argon2id$"
        // and does NOT cross-verify the underlying primitive. Independently recompute the
        // hash from the salt + parameters the substrate emitted using each variant; only
        // the Argon2id recomputation must match. This catches a variant typo in the
        // substrate code that the wire format would otherwise hide.
        var hasher = new Argon2idPasswordHasher<TestUser>(
            Options.Create(new Argon2idHashOptions())); // OWASP-minimum defaults.

        const string password = "primitive-selection-probe-9106";
        var phc = hasher.HashPassword(TestUser.Instance, password);

        Assert.True(
            Argon2idPasswordHasher<TestUser>.TryParsePhcString(phc.AsSpan(), out var parts),
            "substrate-emitted PHC string must parse");

        var pwBytes = Encoding.UTF8.GetBytes(password);

        var idBytes = ComputeVariant<Argon2id>(pwBytes, parts);
        var iBytes = ComputeVariant<Argon2i>(pwBytes, parts);
        var dBytes = ComputeVariant<Argon2d>(pwBytes, parts);

        Assert.Equal(parts.Hash, idBytes);   // Argon2id recomputation matches the stored hash.
        Assert.NotEqual(parts.Hash, iBytes); // Argon2i does NOT.
        Assert.NotEqual(parts.Hash, dBytes); // Argon2d does NOT.

        // Belt-and-suspenders: confirm the variant FullName the substrate would instantiate.
        Assert.Equal("Konscious.Security.Cryptography.Argon2id", typeof(Argon2id).FullName);
    }

    private static byte[] ComputeVariant<TArgon>(byte[] passwordBytes, Argon2idPhcParts parts)
        where TArgon : Argon2
    {
        var argon = (Argon2)Activator.CreateInstance(typeof(TArgon), passwordBytes)!;
        // Argon2 base exposes Salt / Iterations / MemorySize / DegreeOfParallelism settable props.
        SetProp(argon, "Salt", parts.Salt);
        SetProp(argon, "Iterations", (int)parts.Iterations);
        SetProp(argon, "MemorySize", (int)parts.MemoryKib);
        SetProp(argon, "DegreeOfParallelism", (int)parts.DegreeOfParallelism);
        try
        {
            return argon.GetBytes(parts.Hash.Length);
        }
        finally
        {
            (argon as IDisposable)?.Dispose();
        }
    }

    private static void SetProp(object target, string name, object value)
    {
        var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"property {name} not found on {target.GetType()}");
        prop.SetValue(target, value);
    }

    private static byte[] Repeat(byte value, int count)
    {
        var bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static byte[] HexBytes(string hex)
    {
        var tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            bytes[i] = Convert.ToByte(tokens[i], 16);
        }

        return bytes;
    }
}
