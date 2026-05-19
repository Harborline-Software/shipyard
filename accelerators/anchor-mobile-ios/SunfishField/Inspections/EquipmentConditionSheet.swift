import SwiftUI

// MARK: - EquipmentConditionSheet

/// Modal sheet for recording an equipment condition assessment.
///
/// Emits `.EquipmentConditionRecorded` via `EventQueueServicing`.
/// Optional photo stored via `BlobStore`; `photoRef` is the content-SHA256.
/// Per W#23.3 Phase 2 hand-off.
public struct EquipmentConditionSheet: View {
    let inspectionId: String
    let equipment: EquipmentListItem
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32
    let onRecorded: () -> Void

    @Environment(\.dismiss) private var dismiss

    @State private var selectedRating: ConditionRatingLocal = .good
    @State private var note: String = ""
    @State private var blobRef: String?
    @State private var isSubmitting = false
    @State private var submitError: String?
    @State private var showPicker = false

    public init(
        inspectionId: String,
        equipment: EquipmentListItem,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32,
        onRecorded: @escaping () -> Void
    ) {
        self.inspectionId = inspectionId
        self.equipment = equipment
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
                Section("Equipment") {
                    LabeledContent("Name", value: equipment.name)
                }

                Section("Condition Rating") {
                    Picker("Rating", selection: $selectedRating) {
                        ForEach(ConditionRatingLocal.allCases, id: \.self) { rating in
                            Text(rating.displayName).tag(rating)
                        }
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
                }

                Section("Notes (Optional)") {
                    TextField("Observations, recommendations…", text: $note, axis: .vertical)
                        .lineLimit(3...6)
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
            .navigationTitle("Assess \(equipment.name)")
            #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
            #endif
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                        .disabled(isSubmitting)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Record") {
                        Task { await submit() }
                    }
                    .disabled(isSubmitting)
                }
            }
            #if os(iOS)
            .sheet(isPresented: $showPicker) {
                EquipmentConditionImagePicker { jpegData in
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
            let noteValue: String? = note.trimmingCharacters(in: .whitespaces).isEmpty ? nil : note
            let payload = try JsonCanonical.serialize(EquipmentConditionRecordedPayload(
                inspectionId: inspectionId,
                equipmentId: equipment.id,
                rating: selectedRating.rawValue,
                note: noteValue,
                photoRef: blobRef
            ))
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .EquipmentConditionRecorded,
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

// MARK: - EquipmentConditionImagePicker (iOS only)

#if os(iOS)
import UIKit

private struct EquipmentConditionImagePicker: UIViewControllerRepresentable {
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
