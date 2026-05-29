using Sunfish.Foundation.MultiTenancy;
using TenantId = Sunfish.Foundation.Assets.Common.TenantId;

namespace Sunfish.Blocks.Assets.Domain;

/// <summary>
/// Append-only lifecycle event for an <see cref="Asset"/>. Provides the
/// audit-grade history needed by maintenance, inspections, depreciation, and
/// reporting. Events are immutable once appended.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the proven <c>EquipmentLifecycleEvent</c> shape — with one
/// <b>deliberate divergence (ADR 0101 A3):</b> the incumbent
/// <c>EquipmentLifecycleEvent</c> carries a required <c>PropertyId Property</c>
/// snapshot field; <see cref="AssetLifecycleEvent"/> <b>deliberately DROPS
/// it</b>. The greenfield <see cref="Asset"/> is property-agnostic (D3), so
/// there is no <c>Property</c> to snapshot. A build that mirrors the precedent
/// must NOT reintroduce a <c>Property</c> / <c>PropertyId</c> field — doing so
/// would resurrect the property coupling D3 forbids.
/// </para>
/// <para>
/// Per <see cref="IMustHaveTenant"/>; tenant scoping is mandatory.
/// <b>Audit-trail integration deferred</b> — the kernel-audit substrate
/// (<c>Sunfish.Kernel.Audit</c>) is the eventual emission target (ADR 0049);
/// the first-slice carries the domain event in the in-memory
/// <see cref="Services.IAssetLifecycleEventStore"/> only.
/// </para>
/// </remarks>
public sealed record AssetLifecycleEvent : IMustHaveTenant
{
    /// <summary>Stable identifier for this event.</summary>
    public required Guid EventId { get; init; }

    /// <summary>FK to the asset this event describes.</summary>
    public required AssetId Asset { get; init; }

    /// <summary>Owning tenant. Required (default-rejected by persistence adapters).</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Discriminator for the event kind.</summary>
    public required AssetLifecycleEventType EventType { get; init; }

    /// <summary>Wall-clock time at which the event occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Opaque reference to the principal who recorded the event (operator,
    /// vendor, inspector). First-slice ships this as a string; migrates to a
    /// typed <c>IdentityRef</c> when the identity-substrate hand-off lands.
    /// </summary>
    public required string RecordedBy { get; init; }

    /// <summary>Free-text notes captured with the event.</summary>
    public string? Notes { get; init; }

    /// <summary>Event-type-specific payload (e.g. service vendor + cost; warranty claim id).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
