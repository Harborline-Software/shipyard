namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// Optional warranty term embedded in <see cref="Asset"/>. Mirrors the proven
/// <c>WarrantyMetadata</c> value object from <c>blocks-property-equipment</c>:
/// records the coverage window + provider so downstream surfaces can answer
/// "still under warranty" / "expiring within N days" queries. The reminder
/// <i>scheduling</i> service itself is a follow-up slice — the
/// <see cref="ExpiresAt"/> date is the basis for the first-slice expiry query.
/// </summary>
public sealed record WarrantyTerm
{
    /// <summary>Inclusive lower bound on coverage.</summary>
    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>Inclusive upper bound on coverage.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Free-text provider name (e.g. <c>"Manufacturer"</c>, <c>"Dealer extended"</c>).</summary>
    public string? Provider { get; init; }

    /// <summary>Provider-issued policy / contract number.</summary>
    public string? PolicyNumber { get; init; }

    /// <summary>Free-text coverage notes (what's covered, exclusions).</summary>
    public string? CoverageNotes { get; init; }
}
