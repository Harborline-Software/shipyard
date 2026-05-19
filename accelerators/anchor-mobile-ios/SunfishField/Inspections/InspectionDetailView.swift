import SwiftUI

// MARK: - InspectionDetailView

/// Full inspection walkthrough: start → checklist → deficiency → complete.
///
/// Per W#23.3 Phase 1 hand-off. Phase 3 adds the GRDB cache write path and
/// ad-hoc inspection creation ("+" button). Phase 2 adds the equipment-
/// condition-assessment section below the checklist.
public struct InspectionDetailView: View {
    let item: InspectionListItem
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32
    let onEventQueued: () -> Void

    @Environment(\.dismiss) private var dismiss

    @State private var phase: InspectionPhaseLocal
    @State private var checklistItems: [ChecklistItemState]
    @State private var isWorking = false
    @State private var actionError: String?
    @State private var deficiencyTarget: ChecklistItemState?

    public init(
        item: InspectionListItem,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32,
        onEventQueued: @escaping () -> Void
    ) {
        self.item = item
        self.queueService = queueService
        self.blobStore = blobStore
        self.deviceId = deviceId
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
        self.onEventQueued = onEventQueued
        _phase = State(initialValue: InspectionPhaseLocal(rawValue: item.phase) ?? .scheduled)
        _checklistItems = State(initialValue: Self.makeItems(count: item.totalItems))
    }

    public var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    inspectionHeader
                    if phase == .scheduled {
                        startSection
                    } else {
                        checklistSection
                        Divider()
                            .padding(.horizontal)
                        equipmentSection
                        if allItemsResponded {
                            completeSection
                        }
                    }
                    if let err = actionError {
                        Label(err, systemImage: "exclamationmark.triangle.fill")
                            .font(.caption)
                            .foregroundStyle(.red)
                            .padding(.horizontal)
                    }
                }
                .padding(.vertical, 16)
            }
            .navigationTitle(item.templateName ?? "Inspection")
            #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
            #endif
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Close") { dismiss() }
                        .disabled(isWorking)
                }
            }
            .sheet(item: $deficiencyTarget) { target in
                RecordDeficiencySheet(
                    inspectionId: item.id,
                    checklistItem: target,
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

    // MARK: Sub-views

    private var inspectionHeader: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Label(phase.displayName, systemImage: "checkmark.seal")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(phase == .inProgress ? .orange : .blue)
                Spacer()
            }
            Text("Property: \(item.propertyId)")
                .font(.subheadline)
                .foregroundStyle(.secondary)
            if let sched = item.scheduledFor {
                Text("Scheduled: \(sched)")
                    .font(.caption)
                    .foregroundStyle(.tertiary)
            }
        }
        .padding(.horizontal)
    }

    private var startSection: some View {
        VStack(spacing: 12) {
            Text("Ready to begin the inspection walkthrough?")
                .font(.body)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .padding(.horizontal)
            Button {
                Task { await startInspection() }
            } label: {
                if isWorking {
                    ProgressView()
                } else {
                    Label("Start Inspection", systemImage: "play.fill")
                }
            }
            .buttonStyle(.borderedProminent)
            .disabled(isWorking)
        }
        .frame(maxWidth: .infinity)
        .padding(.top, 20)
    }

    private var checklistSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Text("Checklist")
                    .font(.title3.weight(.semibold))
                Spacer()
                Text("\(respondedCount)/\(checklistItems.count)")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
            }
            .padding(.horizontal)

            ProgressView(value: Double(respondedCount), total: Double(max(checklistItems.count, 1)))
                .padding(.horizontal)
                .tint(.green)

            ForEach($checklistItems) { $ci in
                ChecklistItemRow(
                    item: $ci,
                    onResponseChanged: { resp in
                        Task { await recordResponse(item: ci, response: resp) }
                    },
                    onAddDeficiency: { deficiencyTarget = ci }
                )
                .padding(.horizontal)
            }
        }
    }

    private var equipmentSection: some View {
        EquipmentConditionView(
            inspectionId: item.id,
            equipment: [],
            queueService: queueService,
            blobStore: blobStore,
            deviceId: deviceId,
            capturedUnderKernel: capturedUnderKernel,
            capturedUnderSchemaEpoch: capturedUnderSchemaEpoch,
            onEventQueued: onEventQueued
        )
    }

    private var completeSection: some View {
        VStack(spacing: 8) {
            Divider()
                .padding(.horizontal)
            Button {
                Task { await completeInspection() }
            } label: {
                if isWorking {
                    ProgressView()
                } else {
                    Label("Complete Inspection", systemImage: "checkmark.circle.fill")
                        .frame(maxWidth: .infinity)
                }
            }
            .buttonStyle(.borderedProminent)
            .tint(.green)
            .padding(.horizontal)
            .disabled(isWorking)
        }
    }

    // MARK: Derived

    private var respondedCount: Int { checklistItems.filter(\.isResponded).count }
    private var allItemsResponded: Bool {
        !checklistItems.isEmpty && checklistItems.allSatisfy(\.isResponded)
    }

    // MARK: Actions

    private func startInspection() async {
        isWorking = true
        actionError = nil
        defer { isWorking = false }
        do {
            let payload = try JsonCanonical.serialize(InspectionStartedPayload(
                inspectionId: item.id,
                propertyId: item.propertyId,
                templateId: item.templateName
            ))
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .InspectionStarted,
                payload: payload,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
            try await queueService.appendAsync(envelope: envelope)
            phase = .inProgress
            onEventQueued()
        } catch {
            actionError = "Start failed: \(error.localizedDescription)"
        }
    }

    private func recordResponse(item ci: ChecklistItemState, response: ChecklistResponse) async {
        do {
            let note: String? = ci.note.isEmpty ? nil : ci.note
            let payload = try JsonCanonical.serialize(ChecklistResponseRecordedPayload(
                inspectionId: item.id,
                itemId: ci.id,
                response: response.rawValue,
                note: note
            ))
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .ChecklistResponseRecorded,
                payload: payload,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
            try await queueService.appendAsync(envelope: envelope)
            onEventQueued()
        } catch {
            actionError = "Response record failed: \(error.localizedDescription)"
        }
    }

    private func completeInspection() async {
        isWorking = true
        actionError = nil
        defer { isWorking = false }
        do {
            let iso = ISO8601DateFormatter().string(from: Date())
            let payload = try JsonCanonical.serialize(InspectionCompletedPayload(
                inspectionId: item.id,
                completedAt: iso
            ))
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .InspectionCompleted,
                payload: payload,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
            try await queueService.appendAsync(envelope: envelope)
            onEventQueued()
            dismiss()
        } catch {
            actionError = "Complete failed: \(error.localizedDescription)"
        }
    }

    private func nextSeq() -> UInt64 {
        UInt64(max(0, Date().timeIntervalSince1970 * 1000))
    }

    private static func makeItems(count: Int) -> [ChecklistItemState] {
        guard count > 0 else { return [] }
        return (1...count).map { n in
            ChecklistItemState(id: "item-\(n)", description: "Item \(n)")
        }
    }
}

// MARK: - ChecklistItemRow

private struct ChecklistItemRow: View {
    @Binding var item: ChecklistItemState
    let onResponseChanged: (ChecklistResponse) -> Void
    let onAddDeficiency: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(item.description)
                    .font(.body)
                Spacer()
                responsePicker
            }
            if item.response == .fail {
                Button {
                    onAddDeficiency()
                } label: {
                    Label("Add Deficiency", systemImage: "exclamationmark.triangle")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.orange)
                }
                .buttonStyle(.borderless)
            }
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
    }

    private var responsePicker: some View {
        HStack(spacing: 4) {
            ForEach(ChecklistResponse.allCases, id: \.self) { resp in
                Button {
                    item.response = resp
                    onResponseChanged(resp)
                } label: {
                    Text(resp.displayName)
                        .font(.caption.weight(.semibold))
                        .padding(.horizontal, 8)
                        .padding(.vertical, 4)
                        .background(
                            RoundedRectangle(cornerRadius: 6)
                                .fill(item.response == resp
                                      ? chipFill(for: resp)
                                      : Color.secondary.opacity(0.1))
                        )
                        .foregroundStyle(item.response == resp
                                         ? chipLabel(for: resp)
                                         : AnyShapeStyle(.secondary))
                }
                .buttonStyle(.plain)
            }
        }
    }

    private func chipFill(for resp: ChecklistResponse) -> Color {
        switch resp {
        case .pass: return .green.opacity(0.2)
        case .fail: return .red.opacity(0.2)
        case .na:   return .secondary.opacity(0.2)
        }
    }

    private func chipLabel(for resp: ChecklistResponse) -> AnyShapeStyle {
        switch resp {
        case .pass: return AnyShapeStyle(.green)
        case .fail: return AnyShapeStyle(.red)
        case .na:   return AnyShapeStyle(.secondary)
        }
    }
}
