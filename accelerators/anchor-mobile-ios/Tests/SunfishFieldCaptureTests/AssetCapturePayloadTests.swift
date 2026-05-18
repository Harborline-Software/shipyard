import XCTest
@testable import SunfishField

/// W#23.2 Phase 1 — unit tests for `AssetCapturePayload`.
final class AssetCapturePayloadTests: XCTestCase {

    /// Round-trip encode + decode via standard JSONDecoder preserves all fields.
    func testEncode_roundTrip() throws {
        let original = AssetCapturePayload(
            equipmentId: "equip-abc-123",
            photoKind: .primary,
            notes: "Nameplate visible, minor rust"
        )
        let data = try JsonCanonical.serialize(original)
        let decoded = try JSONDecoder().decode(AssetCapturePayload.self, from: data)
        XCTAssertEqual(decoded, original)
    }

    /// Canonical JSON must emit keys in alphabetical order.
    /// Expected key order: equipmentId, notes, photoKind.
    func testCanonicalJson_keyOrder() throws {
        let payload = AssetCapturePayload(
            equipmentId: "e1",
            photoKind: .primary,
            notes: "test"
        )
        let data = try JsonCanonical.serialize(payload)
        let json = try XCTUnwrap(String(data: data, encoding: .utf8))

        let equipIdx = try XCTUnwrap(json.range(of: "equipmentId")).lowerBound
        let notesIdx = try XCTUnwrap(json.range(of: "notes")).lowerBound
        let kindIdx  = try XCTUnwrap(json.range(of: "photoKind")).lowerBound

        XCTAssertLessThan(equipIdx, notesIdx,  "equipmentId must precede notes")
        XCTAssertLessThan(notesIdx, kindIdx,   "notes must precede photoKind")
    }

    /// PhotoKind raw string must be "primary" (wire-format stability).
    func testPhotoKind_rawValue() {
        XCTAssertEqual(AssetCapturePayload.PhotoKind.primary.rawValue, "primary")
    }
}
