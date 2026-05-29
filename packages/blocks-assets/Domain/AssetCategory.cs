namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// Coarse classification for an <see cref="Asset"/>. Replaces the
/// property-flavored fixed <c>EquipmentClass</c> enum with the categories the
/// asset-management bundle's personas need (fleet, manufacturing, facility, IT
/// hardware, …).
/// </summary>
/// <remarks>
/// Per ADR 0101 D2 / OQ-3 (RESOLVED, .NET-architect AMBER), the first slice
/// <b>SHALL ship a closed enum</b> — mirroring the <c>EquipmentClass</c> OQ-A2
/// deferral precedent and keeping C1.1 in-memory + side-effect-free.
/// <b>Registry backing is a named follow-up unit, NOT part of the C1.1
/// substrate.</b> A subsequent unit will back <see cref="AssetCategory"/> with
/// <c>kernel-schema-registry.ISchemaRegistry</c>; this enum is the first-slice
/// stand-in and must not be expanded into a registry integration here.
/// </remarks>
public enum AssetCategory
{
    /// <summary>Fleet vehicle (car, truck, van, trailer).</summary>
    FleetVehicle,

    /// <summary>Manufacturing / production equipment (CNC machine, press, conveyor).</summary>
    ManufacturingEquipment,

    /// <summary>Facility asset (HVAC, elevator, generator, fixed plant).</summary>
    FacilityAsset,

    /// <summary>IT hardware (laptop, server, network gear, peripheral).</summary>
    ItHardware,

    /// <summary>Office equipment and furniture (desk, printer, copier).</summary>
    OfficeEquipment,

    /// <summary>Tooling and instruments (hand tools, test gear, calibration instruments).</summary>
    ToolingAndInstruments,

    /// <summary>Catch-all for assets that don't fit the above buckets; tag context with <see cref="Asset.Notes"/>.</summary>
    Other,
}
