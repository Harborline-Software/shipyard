import SwiftUI

// MARK: - HomeView

/// Main SwiftUI scene rendered post-pairing.
///
/// Per W#23 P6 hand-off: lists capture-flow entry points (placeholder for
/// Phase 7+ flows); queue-status row at the bottom; settings sheet accessible
/// from the navigation bar.
///
/// Entry-point routing: `SunfishFieldApp` checks for a stored `PairingResult`
/// in the `FieldAppEnvironment` and shows `HomeView` when paired, or
/// `PairingFlow` when not yet paired.
public struct HomeView: View {
    @StateObject private var queueStatusViewModel: QueueStatusViewModel
    @State private var showSettings = false

    private let pairingResult: PairingResult
    private let database: AppDatabase
    private let blobStore: BlobStore
    private let syncEngine: SyncEngine

    public init(
        pairingResult: PairingResult,
        database: AppDatabase,
        blobStore: BlobStore,
        syncEngine: SyncEngine
    ) {
        self.pairingResult = pairingResult
        self.database = database
        self.blobStore = blobStore
        self.syncEngine = syncEngine
        _queueStatusViewModel = StateObject(wrappedValue: QueueStatusViewModel(
            database: database,
            blobStore: blobStore,
            syncEngine: syncEngine
        ))
    }

    public var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    captureFlowSection
                    Spacer(minLength: 0)
                    queueSection
                }
                .padding(.horizontal)
                .padding(.vertical, 16)
            }
            .navigationTitle("Sunfish Field")
            .navigationBarTitleDisplayMode(.large)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showSettings = true
                    } label: {
                        Label("Settings", systemImage: "gear")
                    }
                }
            }
            .sheet(isPresented: $showSettings) {
                SettingsView(
                    pairingResult: pairingResult,
                    database: database,
                    blobStore: blobStore,
                    syncEngine: syncEngine,
                    onUnpaired: handleUnpaired
                )
            }
        }
    }

    // MARK: Sub-views

    /// Lists the 6 capture-flow entry points per ADR 0028-A2.1.
    /// Phases 7+ implement the actual capture views; this phase ships
    /// the skeleton structure. Each entry is disabled when
    /// `captureBlockReason` is non-nil (queue/blob hard cap reached).
    private var captureFlowSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Capture")
                .font(.title3.weight(.semibold))
                .foregroundStyle(.primary)

            let isBlocked = queueStatusViewModel.captureBlockReason != nil

            VStack(spacing: 8) {
                ForEach(CaptureFlowKind.allCases) { flow in
                    CaptureFlowRow(
                        flow: flow,
                        isBlocked: isBlocked)
                }
            }

            if isBlocked {
                Label("Captures blocked — sync queue to continue.", systemImage: "lock.fill")
                    .font(.caption)
                    .foregroundStyle(.red)
            }
        }
    }

    private var queueSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Sync Queue")
                .font(.title3.weight(.semibold))
                .foregroundStyle(.primary)

            QueueStatusRow(viewModel: queueStatusViewModel)
        }
    }

    // MARK: Helpers

    private func handleUnpaired() {
        // Notify the host app environment that the device has been unpaired;
        // `SunfishFieldApp` observes `FieldAppEnvironment.isPaired` and
        // transitions back to `PairingFlow`.
        FieldAppEnvironment.shared.clearPairing()
    }
}

// MARK: - CaptureFlowKind

/// The 6 capture domains per ADR 0028-A2.1 event-type table.
/// Each case maps to a Phase 7+ SwiftUI capture view.
public enum CaptureFlowKind: String, CaseIterable, Identifiable {
    case receipt = "Receipt"
    case asset = "Asset"
    case inspection = "Inspection"
    case signature = "Signature"
    case mileage = "Mileage"
    case workOrderResponse = "Work Order Response"

    public var id: String { rawValue }

    var systemImageName: String {
        switch self {
        case .receipt: return "doc.text"
        case .asset: return "cube.box"
        case .inspection: return "checklist"
        case .signature: return "signature"
        case .mileage: return "car"
        case .workOrderResponse: return "wrench.and.screwdriver"
        }
    }

    /// Phase gate: `true` once the corresponding W#23.x capture-flow
    /// hand-off is merged.  Substrate v1 ships all as `false`.
    var isImplemented: Bool { false }
}

// MARK: - CaptureFlowRow

/// A single capture-flow entry in the `HomeView` capture section.
private struct CaptureFlowRow: View {
    let flow: CaptureFlowKind
    let isBlocked: Bool

    var body: some View {
        HStack(spacing: 14) {
            Image(systemName: flow.systemImageName)
                .font(.title3)
                .foregroundStyle(rowForegroundStyle)
                .frame(width: 28)

            VStack(alignment: .leading, spacing: 2) {
                Text(flow.rawValue)
                    .font(.body)
                    .foregroundStyle(rowForegroundStyle)
                if !flow.isImplemented {
                    Text("Coming soon")
                        .font(.caption)
                        .foregroundStyle(.tertiary)
                }
            }

            Spacer()

            if isBlocked {
                Image(systemName: "lock.fill")
                    .font(.caption)
                    .foregroundStyle(.red.opacity(0.7))
            } else {
                Image(systemName: "chevron.right")
                    .font(.caption)
                    .foregroundStyle(.quaternary)
            }
        }
        .padding(.vertical, 10)
        .padding(.horizontal, 14)
        .background(
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(.secondarySystemBackground))
        )
        .opacity(isDisabled ? 0.5 : 1.0)
        // Phase 7+: replace with NavigationLink to the capture view.
    }

    private var isDisabled: Bool { isBlocked || !flow.isImplemented }

    private var rowForegroundStyle: some ShapeStyle {
        isDisabled ? AnyShapeStyle(.secondary) : AnyShapeStyle(.primary)
    }
}

// MARK: - FieldAppEnvironment

/// Lightweight observable environment that `SunfishFieldApp` observes
/// to route between `PairingFlow` and `HomeView`.
///
/// Phase 5 (pairing flow) writes `pairingResult` on successful pairing and
/// persists it to Keychain. Phase 6 reads it and provides `clearPairing()`
/// for the unpair action.
public final class FieldAppEnvironment: ObservableObject {
    public static let shared = FieldAppEnvironment()

    @Published public var pairingResult: PairingResult?

    private init() {
        // On launch: attempt to restore a previously-persisted pairing result
        // from Keychain. Phase 5 writes this on successful pairing.
        pairingResult = PairingResult.loadFromKeychain()
    }

    /// Clears the in-memory + Keychain pairing state, causing the app root to
    /// transition back to `PairingFlow`.
    public func clearPairing() {
        PairingResult.removeFromKeychain()
        pairingResult = nil
    }

    /// Returns true when the device is currently paired.
    public var isPaired: Bool { pairingResult != nil }
}
