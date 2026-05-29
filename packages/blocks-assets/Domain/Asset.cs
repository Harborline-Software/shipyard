using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.MultiTenancy;
using TenantId = Sunfish.Foundation.Assets.Common.TenantId;

namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// A physical, property-agnostic asset — fleet vehicle, manufacturing machine,
/// facility asset, IT hardware, etc. The asset-management bundle's domain core.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR 0101, this domain is built greenfield by generalizing the proven
/// <c>blocks-property-equipment</c> <c>Equipment</c> shape — strongly-typed
/// opaque ids, <see cref="IMustHaveTenant"/>, append-only lifecycle events,
/// in-memory-first tenant-scoped repositories — <b>without</b> inheriting its
/// property coupling.
/// </para>
/// <para>
/// <b>Property-agnostic (D3):</b> an <see cref="Asset"/> has a free-text
/// <see cref="Location"/>, <b>NOT</b> a required <c>PropertyId</c> — a fleet
/// vehicle or a CNC machine does not belong to a property. <c>blocks-assets</c>
/// therefore takes NO dependency on <c>blocks-property-equipment</c> /
/// <c>blocks-properties</c>.
/// </para>
/// <para>
/// <b>Money from day one (D2):</b> <see cref="AcquisitionCost"/> is a typed
/// <see cref="Money"/> (ADR 0051), avoiding the <c>decimal? -&gt; Money</c>
/// migration debt that <c>Equipment.AcquisitionCost</c> still carries.
/// </para>
/// <para>
/// Implements <see cref="IMustHaveTenant"/>; persistence adapters reject records
/// with the default / system <see cref="TenantId"/>. Soft-delete via
/// <see cref="DisposedAt"/>.
/// </para>
/// </remarks>
public sealed record Asset : IMustHaveTenant
{
    /// <summary>Stable identifier for this asset.</summary>
    public required AssetId Id { get; init; }

    /// <summary>Owning tenant. Required (default-rejected by persistence adapters).</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Coarse classification (fleet vehicle, manufacturing equipment, IT hardware, …).</summary>
    public required AssetCategory Category { get; init; }

    /// <summary>Human-friendly name (e.g. <c>"Forklift #3"</c>, <c>"Haas VF-2 CNC mill"</c>).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Manufacturer (e.g. <c>"Toyota"</c>, <c>"Haas"</c>).</summary>
    public string? Make { get; init; }

    /// <summary>Model designation.</summary>
    public string? Model { get; init; }

    /// <summary>Serial number / VIN captured from the asset.</summary>
    public string? SerialNumber { get; init; }

    /// <summary>Acquisition timestamp.</summary>
    public DateTimeOffset? AcquiredAt { get; init; }

    /// <summary>
    /// Acquisition cost basis for depreciation / reporting. Typed
    /// <see cref="Money"/> from day one (ADR 0051).
    /// </summary>
    public Money? AcquisitionCost { get; init; }

    /// <summary>
    /// Opaque reference to the receipt documenting the acquisition cost.
    /// First-slice carries this as a <see cref="string"/>; migrates to a typed
    /// <c>ReceiptId?</c> when the Receipts module ships.
    /// </summary>
    public string? AcquisitionReceiptRef { get; init; }

    /// <summary>Expected useful life in years (depreciation input).</summary>
    public int? ExpectedUsefulLifeYears { get; init; }

    /// <summary>Current lifecycle status; transitions recorded as <see cref="AssetLifecycleEvent"/>s.</summary>
    public required LifecycleState LifecycleState { get; init; }

    /// <summary>Optional warranty term.</summary>
    public WarrantyTerm? Warranty { get; init; }

    /// <summary>
    /// Optional depreciation schedule. Auto-calculation is opt-in
    /// (<see cref="DepreciationSchedule.AutoCalculate"/> defaults to <c>false</c>).
    /// </summary>
    public DepreciationSchedule? Depreciation { get; init; }

    /// <summary>
    /// Free-text location (<b>NOT</b> a <c>PropertyId</c> — see D3). E.g.
    /// <c>"North yard"</c>, <c>"Plant 2, Bay 4"</c>, <c>"Remote / field"</c>.
    /// </summary>
    public string? Location { get; init; }

    /// <summary>Free-text operator notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Reference to a primary photo in the blob-storage substrate (opaque string first-slice).</summary>
    public string? PrimaryPhotoBlobRef { get; init; }

    /// <summary>Record-creation timestamp; immutable after first persist.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Disposition timestamp (sale, scrapping, write-off). Soft-delete marker:
    /// records remain queryable via <c>includeDisposed: true</c> but are
    /// excluded from default listings.
    /// </summary>
    public DateTimeOffset? DisposedAt { get; init; }

    /// <summary>Free-text reason captured on disposition.</summary>
    public string? DisposalReason { get; init; }
}
