import SwiftUI

// MARK: - RecordDeficiencySheet

/// Modal for recording a deficiency on a failed checklist item.
///
/// Per W#23.3 Phase 1 hand-off. Captures description, severity, and an
/// optional photo (1 photo, PHPicker on iOS). On submit emits a
/// `DeficiencyRecorded` event envelope via `EventQueueServicing`.
public struct RecordDeficiencySheet: View {
    let inspectionId: String
    let checklistItem: ChecklistItemState
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32
    let onRecorded: () -> Void

    @Environment(\.dismiss) private var dismiss

    @State private var description = ""
    @State private var severity: DeficiencySeverity = .medium
    @State private var blobRef: String?
    @State private var isSubmitting = false
    @State private var submitError: String?
    @State private var showPicker = false

    public init(
        inspectionId: String,
        checklistItem: ChecklistItemState,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32,
        onRecorded: @escaping () -> Void
    ) {
        self.inspectionId = inspectionId
        self.checklistItem = checklistItem
        self.queueService = queueService
        self.blobStore = blobStore
        self.deviceId = deviceId
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
        self.onRecorded = onRecorded
    }

    public var body: some View {
        NavigationStack {
            Form {
                Section("Deficiency Description") {
                    TextField(
                        "Describe the deficiency (required)",
                        text: $description,
                        axis: .vertical
                    )
                    .lineLimit(3...6)
                }

                Section("Severity") {
                    Picker("Severity", selection: $severity) {
                        ForEach(DeficiencySeverity.allCases, id: \.self) { s in
                            Text(s.displayName).tag(s)
                        }
                    }
                    .pickerStyle(.segmented)
                }

                Section("Photo (Optional)") {
                    #if os(iOS)
                    if let ref = blobRef {
                        HStack {
                            Image(systemName: "photo.fill")
                                .foregroundStyle(.green)
                            Text("Photo attached")
                                .font(.caption)
                            Spacer()
                            Button("Remove", role: .destructive) { blobRef = nil }
                                .font(.caption)
                                .buttonStyle(.borderless)
                        }
                    } else {
                        Button {
                            showPicker = true
                        } label: {
                            Label("Add Photo", systemImage: "camera")
                        }
                        .disabled(isSubmitting)
                    }
                    #else
                    Text("Camera not available on this platform.")
                        .foregroundStyle(.secondary)
                        .font(.caption)
                    #endif
                }

                if let err = submitError {
                    Section {
                        Label(err, systemImage: "exclamationmark.triangle.fill")
                            .font(.caption)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle("Record Deficiency")
            #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
            #endif
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                        .disabled(isSubmitting)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Submit") {
                        Task { await submit() }
                    }
                    .disabled(description.trimmingCharacters(in: .whitespaces).isEmpty || isSubmitting)
                }
            }
            #if os(iOS)
            .sheet(isPresented: $showPicker) {
                DeficiencyImagePicker { jpegData in
                    guard let data = jpegData else { return }
                    Task { await storePhoto(data) }
                }
            }
            #endif
        }
    }

    // MARK: Actions

    private func submit() async {
        isSubmitting = true
        submitError = nil
        defer { isSubmitting = false }
        do {
            let trimmed = description.trimmingCharacters(in: .whitespaces)
            let payload = try JsonCanonical.serialize(DeficiencyRecordedPayload(
                inspectionId: inspectionId,
                itemId: checklistItem.id,
                description: trimmed,
                severity: severity.rawValue,
                photoRef: blobRef
            ))
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .DeficiencyRecorded,
                payload: payload,
                blobRef: blobRef,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
            try await queueService.appendAsync(envelope: envelope)
            onRecorded()
            dismiss()
        } catch {
            submitError = "Submit failed: \(error.localizedDescription)"
        }
    }

    private func storePhoto(_ jpegData: Data) async {
        do {
            let ref = try blobStore.put(jpegData)
            await MainActor.run { blobRef = ref }
        } catch {
            await MainActor.run { submitError = "Photo store failed: \(error.localizedDescription)" }
        }
    }

    private func nextSeq() -> UInt64 {
        UInt64(max(0, Date().timeIntervalSince1970 * 1000))
    }
}

// MARK: - DeficiencyImagePicker (iOS only)

#if os(iOS)
import UIKit

private struct DeficiencyImagePicker: UIViewControllerRepresentable {
    let onFinished: (Data?) -> Void

    func makeCoordinator() -> Coordinator { Coordinator(onFinished: onFinished) }

    func makeUIViewController(context: Context) -> UIImagePickerController {
        let picker = UIImagePickerController()
        picker.sourceType = .camera
        picker.delegate = context.coordinator
        return picker
    }

    func updateUIViewController(_: UIImagePickerController, context _: Context) {}

    final class Coordinator: NSObject,
        UIImagePickerControllerDelegate,
        UINavigationControllerDelegate
    {
        let onFinished: (Data?) -> Void
        init(onFinished: @escaping (Data?) -> Void) { self.onFinished = onFinished }

        func imagePickerController(
            _ picker: UIImagePickerController,
            didFinishPickingMediaWithInfo info: [UIImagePickerController.InfoKey: Any]
        ) {
            picker.dismiss(animated: true)
            let image = info[.originalImage] as? UIImage
            onFinished(image?.jpegData(compressionQuality: 0.85))
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            picker.dismiss(animated: true)
            onFinished(nil)
        }
    }
}
#endif
