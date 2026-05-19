import Foundation

// MARK: - Bridge DTO

/// Inspection summary returned by `GET /api/v1/field/inspections`.
public struct InspectionListItem: Codable, Identifiable, Equatable, Sendable {
    public let id: String
    public let propertyId: String
    public let phase: String
    public let scheduledFor: String?
    public let templateName: String?
    public let totalItems: Int
    public let respondedItems: Int

    public init(
        id: String,
        propertyId: String,
        phase: String,
        scheduledFor: String?,
        templateName: String?,
        totalItems: Int,
        respondedItems: Int
    ) {
        self.id = id
        self.propertyId = propertyId
        self.phase = phase
        self.scheduledFor = scheduledFor
        self.templateName = templateName
        self.totalItems = totalItems
        self.respondedItems = respondedItems
    }
}

// MARK: - Local phase enum

/// Local mirror of `InspectionPhase` for the list + detail screens.
/// Raw values match the Bridge JSON strings.
public enum InspectionPhaseLocal: String, Sendable, Equatable {
    case scheduled = "Scheduled"
    case inProgress = "InProgress"

    public var displayName: String {
        switch self {
        case .scheduled: return "Scheduled"
        case .inProgress: return "In Progress"
        }
    }

    public var chipColor: String {
        switch self {
        case .scheduled: return "blue"
        case .inProgress: return "orange"
        }
    }

    init?(rawString: String) {
        self.init(rawValue: rawString)
    }
}

// MARK: - Checklist

/// A single checklist item's local state during an inspection walkthrough.
public struct ChecklistItemState: Identifiable, Sendable {
    public let id: String
    public let description: String
    public var response: ChecklistResponse?
    public var note: String

    public init(id: String, description: String) {
        self.id = id
        self.description = description
        self.response = nil
        self.note = ""
    }

    /// True when the item has received any response.
    public var isResponded: Bool { response != nil }
}

/// Checklist item response value. Raw values are the canonical wire strings.
public enum ChecklistResponse: String, Codable, CaseIterable, Sendable {
    case pass = "pass"
    case fail = "fail"
    case na   = "na"

    public var displayName: String {
        switch self {
        case .pass: return "Pass"
        case .fail: return "Fail"
        case .na:   return "N/A"
        }
    }
}

// MARK: - Condition rating (W#23.3 Phase 2)

/// Equipment condition rating. Raw values match `Sunfish.Blocks.Inspections.Models.ConditionRating`.
public enum ConditionRatingLocal: String, Codable, CaseIterable, Sendable {
    case good   = "Good"
    case fair   = "Fair"
    case poor   = "Poor"
    case failed = "Failed"

    public var displayName: String { rawValue }
}

// MARK: - Deficiency severity

/// Deficiency severity levels. Raw values match `Sunfish.Blocks.Inspections.Models.DeficiencySeverity`.
public enum DeficiencySeverity: String, Codable, CaseIterable, Sendable {
    case low      = "Low"
    case medium   = "Medium"
    case high     = "High"
    case critical = "Critical"

    public var displayName: String { rawValue }
}

// MARK: - Event payloads (W#23.3)

public struct InspectionStartedPayload: Codable, Sendable {
    public let inspectionId: String
    public let propertyId: String
    public let templateId: String?
}

public struct ChecklistResponseRecordedPayload: Codable, Sendable {
    public let inspectionId: String
    public let itemId: String
    public let response: String
    public let note: String?
}

public struct DeficiencyRecordedPayload: Codable, Sendable {
    public let inspectionId: String
    public let itemId: String
    public let description: String
    public let severity: String
    public let photoRef: String?
}

public struct EquipmentConditionRecordedPayload: Codable, Sendable {
    public let inspectionId: String
    public let equipmentId: String
    public let rating: String
    public let note: String?
    public let photoRef: String?
}

public struct InspectionCompletedPayload: Codable, Sendable {
    public let inspectionId: String
    public let completedAt: String
}
