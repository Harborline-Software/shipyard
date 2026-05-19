import SwiftUI

// MARK: - EquipmentListItem

/// Lightweight display-only projection of an equipment record for capture flows.
///
/// Passed into `AssetCaptureView` by the home screen after fetching from Bridge
/// via `GET /api/v1/equipment`. For v1 smoke tests a stub item is acceptable
/// (per W#23.2 hand-off Note 1 — the equipment-list API ships as a follow-up).
public struct EquipmentListItem: Identifiable, Equatable, Sendable, Hashable {
    public let id: String
    public let name: String

    public init(id: String, name: String) {
        self.id = id
        self.name = name
    }
}

// MARK: - AssetCaptureView

/// Full-screen camera sheet for capturing an equipment asset photo.
///
/// Per W#23.2 hand-off Phase 1. Capture flow:
/// 1. iOS: presents `UIImagePickerController` in `.camera` mode.
/// 2. JPEG-compresses the selected image (quality 0.85).
/// 3. Stores bytes in `BlobStore` — returns lowercase hex SHA-256 `blobRef`.
/// 4. Encodes `AssetCapturePayload` via `JsonCanonical.serialize`.
/// 5. Appends an `EventEnvelope` (eventType `.Asset`) to `queueService`.
/// 6. Shows "Photo queued" banner and auto-dismisses.
///
/// Sync is handled automatically by the existing `SyncEngine` background loop.
/// `UIImagePickerController` is iOS-only; macOS shows an informational placeholder.
public struct AssetCaptureView: View {

    let equipment: EquipmentListItem
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32

    @Environment(\.dismiss) private var dismiss
    @State private var showPicker = false
    @State private var showConfirmation = false
    @State private var captureError: String?
    @State private var isProcessing = false

    public init(
        equipment: EquipmentListItem,
        queueService: any EventQueueServicing,
        blobStore: BlobStore,
        deviceId: String,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32
    ) {
        self.equipment = equipment
        self.queueService = queueService
        self.blobStore = blobStore
        self.deviceId = deviceId
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
    }

    public var body: some View {
        NavigationStack {
            VStack(spacing: 24) {
                VStack(spacing: 6) {
                    Text(equipment.name)
                        .font(.title2.weight(.semibold))
                    Text("Equipment ID: \(equipment.id)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                #if os(iOS)
                Button {
                    showPicker = true
                } label: {
                    Label("Take Photo", systemImage: "camera.fill")
                }
                .buttonStyle(.borderedProminent)
                .disabled(isProcessing)
                #else
                Text("Camera capture is not available on this platform.")
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                #endif

                if isProcessing {
                    ProgressView()
                }

                if showConfirmation {
                    Label("Photo queued", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                        .font(.body.weight(.medium))
                }

                if let err = captureError {
                    Label(err, systemImage: "exclamationmark.triangle.fill")
                        .foregroundStyle(.red)
                        .font(.callout)
                        .multilineTextAlignment(.center)
                }

                Spacer()
            }
            .padding(.horizontal)
            .padding(.top, 32)
            .navigationTitle("Capture Equipment")
            #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
            .sheet(isPresented: $showPicker) {
                ImagePickerRepresentable { jpegData in
                    guard let data = jpegData else { return }
                    Task { await handleCapture(jpegData: data) }
                }
            }
            #endif
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                        .disabled(isProcessing)
                }
            }
        }
    }

    // MARK: - Capture handling

    private func handleCapture(jpegData: Data) async {
        await MainActor.run {
            isProcessing = true
            captureError = nil
            showConfirmation = false
        }

        do {
            let blobRef = try blobStore.put(jpegData)
            let payloadData = try JsonCanonical.serialize(
                AssetCapturePayload(equipmentId: equipment.id)
            )
            let envelope = EventEnvelope(
                deviceLocalSeq: nextSeq(),
                capturedAt: Date(),
                deviceId: deviceId,
                eventType: .Asset,
                payload: payloadData,
                blobRef: blobRef,
                capturedUnderKernel: capturedUnderKernel,
                capturedUnderSchemaEpoch: capturedUnderSchemaEpoch
            )
            try await queueService.appendAsync(envelope: envelope)

            await MainActor.run {
                isProcessing = false
                showConfirmation = true
            }

            try await Task.sleep(nanoseconds: 1_500_000_000)
            await MainActor.run { dismiss() }

        } catch {
            await MainActor.run {
                isProcessing = false
                captureError = "Capture failed: \(error.localizedDescription)"
            }
        }
    }

    /// Monotonically-increasing sequence number scoped to this process.
    /// v1 substrate uses a timestamp-millis fallback; Phase 4 sync engine
    /// replaces this with the GRDB `device_local_seq` autoincrement value.
    private func nextSeq() -> UInt64 {
        UInt64(max(0, Date().timeIntervalSince1970 * 1000))
    }
}

// MARK: - ImagePickerRepresentable (iOS only)

#if os(iOS)
import UIKit

/// Wraps `UIImagePickerController` in `.camera` source mode.
///
/// On selection: compresses the picked image to JPEG (quality 0.85) and calls
/// `onFinished` with the data. On cancel: calls `onFinished(nil)`.
private struct ImagePickerRepresentable: UIViewControllerRepresentable {
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
            let jpegData = image?.jpegData(compressionQuality: 0.85)
            onFinished(jpegData)
        }

        func imagePickerControllerDidCancel(_ picker: UIImagePickerController) {
            picker.dismiss(animated: true)
            onFinished(nil)
        }
    }
}
#endif
