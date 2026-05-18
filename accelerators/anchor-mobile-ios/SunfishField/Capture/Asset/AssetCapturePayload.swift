import Foundation

/// Canonical-JSON-encoded payload for an `EventType.Asset` envelope.
/// Transmitted inside `EventEnvelope.payload` (base64-encoded Data).
/// The accompanying photo blob is referenced via `EventEnvelope.blobRef`.
///
/// Per W#23.2 hand-off Phase 1. Mirrors `AssetCapturePayload` on the
/// .NET / Bridge side (`Sunfish.Bridge.Field.AssetCapturePayload`).
public struct AssetCapturePayload: Codable, Sendable, Equatable, Hashable {

    /// The equipment this photo is associated with.
    public let equipmentId: String

    /// Photo role. v1 only ships "primary"; supplementary deferred.
    public let photoKind: PhotoKind

    /// Optional free-text notes recorded by the field agent.
    public let notes: String?

    public init(
        equipmentId: String,
        photoKind: PhotoKind = .primary,
        notes: String? = nil
    ) {
        self.equipmentId = equipmentId
        self.photoKind = photoKind
        self.notes = notes
    }

    public enum PhotoKind: String, Codable, Sendable, CaseIterable {
        case primary
        // supplementary deferred to W#23.2 follow-up
    }
}
