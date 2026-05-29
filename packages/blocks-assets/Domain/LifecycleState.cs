namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// The current lifecycle status of an <see cref="Asset"/>. Per ADR 0101 D2,
/// transitions between states are recorded as <see cref="AssetLifecycleEvent"/>s
/// (this field is the current snapshot; the event log is the history).
/// </summary>
public enum LifecycleState
{
    /// <summary>Created but not yet placed into service.</summary>
    Draft,

    /// <summary>In active service.</summary>
    Active,

    /// <summary>Temporarily out of service for maintenance, repair, or inspection.</summary>
    InMaintenance,

    /// <summary>Withdrawn from service but not yet disposed (awaiting sale, scrapping, transfer).</summary>
    Retired,

    /// <summary>Disposed of (sold, scrapped, written off). Terminal; soft-deleted via <see cref="Asset.DisposedAt"/>.</summary>
    Disposed,
}
