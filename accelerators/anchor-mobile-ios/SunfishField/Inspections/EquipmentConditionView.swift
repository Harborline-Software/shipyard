import SwiftUI

// MARK: - EquipmentConditionView

/// Equipment condition assessment section for InspectionDetailView.
///
/// Accessed via the "Assess Equipment" section below the checklist (Phase 2).
/// The caller injects an equipment list; when W#23.2's GRDB equipment table is
/// not available the caller passes an empty array and this view shows the
/// offline placeholder message per the W#23.3 Phase 2 hand-off.
public struct EquipmentConditionView: View {
    let inspectionId: String
    let equipment: [EquipmentListItem]
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32
    let onEventQueued: () -> Void

    @State private var assessmentTarget: EquipmentListItem?

    public init(
        inspectionId: String,
        equipment: [EquipmentListItem],
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32,
        onEventQueued: @escaping () -> Void
    ) {
        self.inspectionId = inspectionId
        self.equipment = equipment
        self.queueService = queueService
        self.blobStore = blobStore
        self.deviceId = deviceId
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
        self.onEventQueued = onEventQueued
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("Equipment Condition")
                    .font(.title3.weight(.semibold))
                Spacer()
            }
            .padding(.horizontal)

            if equipment.isEmpty {
                Text("Equipment list unavailable offline")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .padding(.horizontal)
            } else {
                ForEach(equipment, id: \.id) { item in
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(item.name)
                                .font(.body)
                            Text(item.id)
                                .font(.caption)
                                .foregroundStyle(.tertiary)
                        }
                        Spacer()
                        Button("Assess") {
                            assessmentTarget = item
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                    }
                    .padding(12)
                    .background(
                        RoundedRectangle(cornerRadius: 8)
                            #if os(iOS)
                            .fill(Color(.secondarySystemBackground))
                            #else
                            .fill(Color(nsColor: .controlBackgroundColor))
                            #endif
                    )
                    .padding(.horizontal)
                }
            }
        }
        .sheet(item: $assessmentTarget) { target in
            EquipmentConditionSheet(
                inspectionId: inspectionId,
                equipment: target,
                queueService: queueService,
                blobStore: blobStore,
                deviceId: deviceId,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch,
                onRecorded: { onEventQueued() }
            )
        }
    }
}
