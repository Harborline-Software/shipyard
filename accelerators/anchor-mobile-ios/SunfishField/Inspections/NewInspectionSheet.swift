import SwiftUI

// MARK: - NewInspectionSheet

/// Ad hoc inspection creation modal. Accessible via the "+" button in
/// `InspectionListView`. Emits `.InspectionScheduled` via the queue service;
/// the Bridge handler maps this to `IInspectionsService.ScheduleAsync` on sync.
///
/// W#23.3 Phase 3. V1 fields: propertyId (unit EntityId string) + inspectorName.
/// A full property/unit picker is deferred until offline property lists are cached.
public struct NewInspectionSheet: View {
    let queueService: any EventQueueServicing
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32

    @Environment(\.dismiss) private var dismiss

    @State private var propertyId = ""
    @State private var inspectorName = ""
    @State private var isSubmitting = false
    @State private var submitError: String?

    private var isValid: Bool {
        !propertyId.trimmingCharacters(in: .whitespaces).isEmpty &&
        !inspectorName.trimmingCharacters(in: .whitespaces).isEmpty
    }

    public var body: some View {
        NavigationStack {
            Form {
                Section("Inspection details") {
                    TextField("Unit ID (e.g. unit:tenant/unit-001)", text: $propertyId)
                        .autocorrectionDisabled()
                        #if os(iOS)
                        .textInputAutocapitalization(.never)
                        #endif
                    TextField("Inspector name", text: $inspectorName)
                }

                if let err = submitError {
                    Section {
                        Text(err)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }
            }
            .navigationTitle("New Inspection")
            #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
            #endif
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSubmitting {
                        ProgressView().scaleEffect(0.75)
                    } else {
                        Button("Schedule") {
                            Task { await submit() }
                        }
                        .disabled(!isValid)
                    }
                }
            }
        }
    }

    // MARK: Submit

    private func submit() async {
        isSubmitting = true
        submitError = nil
        defer { isSubmitting = false }

        do {
            let payload = try JsonCanonical.serialize(InspectionScheduledPayload(
                propertyId: propertyId.trimmingCharacters(in: .whitespaces),
                inspectorName: inspectorName.trimmingCharacters(in: .whitespaces),
                scheduledFor: ISO8601DateFormatter().string(from: Date())
            ))
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .InspectionScheduled,
                payload: payload,
                blobRef: nil,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
            try await queueService.appendAsync(envelope: envelope)
            dismiss()
        } catch {
            submitError = "Submit failed: \(error.localizedDescription)"
        }
    }

    private func nextSeq() -> UInt64 {
        UInt64(max(0, Date().timeIntervalSince1970 * 1000))
    }
}
