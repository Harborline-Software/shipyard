import XCTest
@testable import SunfishField

/// W#23.3 Phase 1 — unit tests for inspection payload models and event type additions.
final class InspectionModelsTests: XCTestCase {

    // MARK: - DeficiencySeverity wire values

    func testDeficiencySeverity_rawValues_matchDotNetEnum() {
        XCTAssertEqual(DeficiencySeverity.low.rawValue,      "Low")
        XCTAssertEqual(DeficiencySeverity.medium.rawValue,   "Medium")
        XCTAssertEqual(DeficiencySeverity.high.rawValue,     "High")
        XCTAssertEqual(DeficiencySeverity.critical.rawValue, "Critical")
    }

    func testChecklistResponse_rawValues_wireStable() {
        XCTAssertEqual(ChecklistResponse.pass.rawValue, "pass")
        XCTAssertEqual(ChecklistResponse.fail.rawValue, "fail")
        XCTAssertEqual(ChecklistResponse.na.rawValue,   "na")
    }

    // MARK: - InspectionStartedPayload

    func testInspectionStartedPayload_roundTrip() throws {
        let original = InspectionStartedPayload(
            inspectionId: "insp-001",
            propertyId: "prop-abc",
            templateId: "tmpl-001"
        )
        let data = try JsonCanonical.serialize(original)
        let decoded = try JSONDecoder().decode(InspectionStartedPayload.self, from: data)
        XCTAssertEqual(decoded.inspectionId, original.inspectionId)
        XCTAssertEqual(decoded.propertyId, original.propertyId)
        XCTAssertEqual(decoded.templateId, original.templateId)
    }

    func testInspectionStartedPayload_canonicalKeyOrder() throws {
        let payload = InspectionStartedPayload(
            inspectionId: "i1",
            propertyId: "p1",
            templateId: nil
        )
        let data = try JsonCanonical.serialize(payload)
        let json = try XCTUnwrap(String(data: data, encoding: .utf8))
        // Alphabetical: inspectionId < propertyId < templateId
        let iIdx = try XCTUnwrap(json.range(of: "inspectionId")).lowerBound
        let pIdx = try XCTUnwrap(json.range(of: "propertyId")).lowerBound
        XCTAssertLessThan(iIdx, pIdx, "inspectionId must precede propertyId")
    }

    // MARK: - DeficiencyRecordedPayload

    func testDeficiencyRecordedPayload_roundTrip() throws {
        let original = DeficiencyRecordedPayload(
            inspectionId: "insp-001",
            itemId: "item-3",
            description: "Broken window latch",
            severity: DeficiencySeverity.high.rawValue,
            photoRef: nil
        )
        let data = try JsonCanonical.serialize(original)
        let decoded = try JSONDecoder().decode(DeficiencyRecordedPayload.self, from: data)
        XCTAssertEqual(decoded.description, original.description)
        XCTAssertEqual(decoded.severity, "High")
    }

    func testDeficiencyRecordedPayload_canonicalKeyOrder() throws {
        let payload = DeficiencyRecordedPayload(
            inspectionId: "i1",
            itemId: "it1",
            description: "test",
            severity: "Low",
            photoRef: nil
        )
        let data = try JsonCanonical.serialize(payload)
        let json = try XCTUnwrap(String(data: data, encoding: .utf8))
        // Alphabetical: description < inspectionId < itemId < photoRef < severity
        let dIdx = try XCTUnwrap(json.range(of: "description")).lowerBound
        let iIdx = try XCTUnwrap(json.range(of: "inspectionId")).lowerBound
        let sIdx = try XCTUnwrap(json.range(of: "severity")).lowerBound
        XCTAssertLessThan(dIdx, iIdx, "description must precede inspectionId")
        XCTAssertLessThan(iIdx, sIdx, "inspectionId must precede severity")
    }

    // MARK: - InspectionCompletedPayload

    func testInspectionCompletedPayload_roundTrip() throws {
        let original = InspectionCompletedPayload(
            inspectionId: "insp-001",
            completedAt: "2026-05-18T20:00:00Z"
        )
        let data = try JsonCanonical.serialize(original)
        let decoded = try JSONDecoder().decode(InspectionCompletedPayload.self, from: data)
        XCTAssertEqual(decoded.inspectionId, original.inspectionId)
        XCTAssertEqual(decoded.completedAt, original.completedAt)
    }

    // MARK: - EventType additions

    func testEventType_inspectionCases_rawValues() {
        XCTAssertEqual(EventType.InspectionStarted.rawValue,          "InspectionStarted")
        XCTAssertEqual(EventType.ChecklistResponseRecorded.rawValue,  "ChecklistResponseRecorded")
        XCTAssertEqual(EventType.DeficiencyRecorded.rawValue,         "DeficiencyRecorded")
        XCTAssertEqual(EventType.EquipmentConditionRecorded.rawValue, "EquipmentConditionRecorded")
        XCTAssertEqual(EventType.InspectionCompleted.rawValue,        "InspectionCompleted")
    }

    func testEventType_allCases_count() {
        // 6 original + 5 W#23.3 inspection sub-events = 11
        XCTAssertEqual(EventType.allCases.count, 11)
    }
}
