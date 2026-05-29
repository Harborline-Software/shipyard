namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// Discriminator for the kind of event captured in an
/// <see cref="AssetLifecycleEvent"/>. Closed set in first-slice (mirrors the
/// <c>EquipmentLifecycleEventType</c> precedent), with asset-management-specific
/// transitions (<see cref="Deployed"/>, <see cref="Depreciated"/>,
/// <see cref="Transferred"/>) replacing the property-flavored
/// <c>Installed</c> / <c>MileageRecorded</c> arms.
/// </summary>
public enum AssetLifecycleEventType
{
    /// <summary>The asset was acquired (purchased, leased, donated-in).</summary>
    Acquired,

    /// <summary>The asset was deployed / placed into active service.</summary>
    Deployed,

    /// <summary>The asset was serviced (maintenance, tune-up, repair).</summary>
    Serviced,

    /// <summary>The asset was inspected (audit, safety check, condition assessment).</summary>
    Inspected,

    /// <summary>A warranty claim was filed against the asset.</summary>
    WarrantyClaimed,

    /// <summary>A depreciation entry was recorded against the asset.</summary>
    Depreciated,

    /// <summary>The asset was transferred (location, custodian, cost center).</summary>
    Transferred,

    /// <summary>The asset was replaced (a new asset record supersedes this one).</summary>
    Replaced,

    /// <summary>The asset was disposed of (sold, scrapped, written off).</summary>
    Disposed,

    /// <summary>A photo was added to the asset record.</summary>
    PhotoAdded,

    /// <summary>Free-text notes were updated.</summary>
    NotesUpdated,
}
