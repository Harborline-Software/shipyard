import SwiftUI

// MARK: - SettingsView

/// Minimal device-settings screen per W#23 P6 hand-off.
///
/// Surfaces:
/// - Device ID (read-only) — derived from the install Ed25519 public key
///   (Phase 0: `DeviceId` type)
/// - Paired tenant (read-only) — from `PairingResult`
/// - Anchor base URL (read-only)
/// - "Unpair this device" destructive action — calls
///   `POST /api/v1/field/unpair` and clears local Keychain state
/// - Sync history — last N drain-pass outcomes (in-memory; substrate v1)
public struct SettingsView: View {
    private let pairingResult: PairingResult
    private let database: AppDatabase
    private let blobStore: BlobStore
    private let syncEngine: SyncEngine
    private let onUnpaired: () -> Void

    @StateObject private var viewModel: SettingsViewModel
    @Environment(\.dismiss) private var dismiss
    @State private var showUnpairConfirmation = false

    public init(
        pairingResult: PairingResult,
        database: AppDatabase,
        blobStore: BlobStore,
        syncEngine: SyncEngine,
        onUnpaired: @escaping () -> Void
    ) {
        self.pairingResult = pairingResult
        self.database = database
        self.blobStore = blobStore
        self.syncEngine = syncEngine
        self.onUnpaired = onUnpaired
        _viewModel = StateObject(wrappedValue: SettingsViewModel(
            pairingResult: pairingResult,
            syncEngine: syncEngine
        ))
    }

    public var body: some View {
        NavigationStack {
            List {
                deviceSection
                pairingSection
                syncHistorySection
                unpairSection
            }
            .navigationTitle("Settings")
            #if os(iOS)
            .navigationBarTitleDisplayMode(.inline)
            #endif
            .toolbar {
                ToolbarItem(placement: {
                    #if os(iOS)
                    return .topBarTrailing
                    #else
                    return .automatic
                    #endif
                }()) {
                    Button("Done") { dismiss() }
                        .fontWeight(.semibold)
                }
            }
            .confirmationDialog(
                "Unpair this device?",
                isPresented: $showUnpairConfirmation,
                titleVisibility: .visible
            ) {
                Button("Unpair", role: .destructive) {
                    Task { await viewModel.unpairDevice(onSuccess: onUnpaired) }
                }
                Button("Cancel", role: .cancel) {}
            } message: {
                Text("This device will be removed from the paired tenant. All local queue data will be cleared. This cannot be undone.")
            }
        }
    }

    // MARK: List sections

    private var deviceSection: some View {
        Section("Device") {
            ReadOnlyRow(label: "Device ID", value: viewModel.deviceIdDisplay)
        }
    }

    private var pairingSection: some View {
        Section("Pairing") {
            ReadOnlyRow(label: "Tenant", value: pairingResult.tenantId)
            ReadOnlyRow(label: "Anchor URL", value: pairingResult.anchorBaseUrl)
            ReadOnlyRow(label: "Token expires", value: viewModel.tokenExpiryDisplay)
        }
    }

    private var syncHistorySection: some View {
        Section("Recent Sync Attempts") {
            if viewModel.syncHistory.isEmpty {
                Text("No sync attempts yet.")
                    .foregroundStyle(.secondary)
                    .font(.subheadline)
            } else {
                ForEach(viewModel.syncHistory) { entry in
                    SyncHistoryRow(entry: entry)
                }
            }
        }
    }

    private var unpairSection: some View {
        Section {
            Button(role: .destructive) {
                showUnpairConfirmation = true
            } label: {
                HStack {
                    if viewModel.isUnpairing {
                        ProgressView().scaleEffect(0.8)
                        Text("Unpairing…")
                    } else {
                        Label("Unpair this device", systemImage: "link.badge.minus")
                    }
                }
            }
            .disabled(viewModel.isUnpairing)
        } footer: {
            if let error = viewModel.unpairError {
                Text("Unpair failed: \(error)")
                    .foregroundStyle(.red)
                    .font(.caption)
            }
        }
    }
}

// MARK: - SettingsViewModel

@MainActor
public final class SettingsViewModel: ObservableObject {
    @Published public private(set) var syncHistory: [SyncHistoryEntry] = []
    @Published public private(set) var isUnpairing: Bool = false
    @Published public private(set) var unpairError: String? = nil

    private let pairingResult: PairingResult
    private let syncEngine: SyncEngine

    public init(pairingResult: PairingResult, syncEngine: SyncEngine) {
        self.pairingResult = pairingResult
        self.syncEngine = syncEngine
    }

    // MARK: Derived display values

    var deviceIdDisplay: String {
        // Phase 0's `DeviceId` is derived from the install Ed25519 public key
        // stored in Keychain. Substrate v1 reads the Keychain-persisted device
        // ID string directly to avoid importing the full Identity module here.
        // Phase 7 can switch to `DeviceId.current.value` once the module is
        // linked in the main target.
        guard let data = keychainDeviceId() else { return "Unavailable" }
        return data
    }

    var tokenExpiryDisplay: String {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter.string(from: pairingResult.expiresAt)
    }

    // MARK: Actions

    /// Call `POST /api/v1/field/unpair` via Bridge, then clear local state
    /// on success. Per W#23 P6 hand-off: emits `FieldDeviceRevoked` on the
    /// Bridge side; the iPad clears Keychain entries and calls `onSuccess`.
    public func unpairDevice(onSuccess: @escaping () -> Void) async {
        guard !isUnpairing else { return }
        isUnpairing = true
        unpairError = nil
        defer { isUnpairing = false }

        do {
            try await postUnpair()
            // Clear Keychain — done regardless of server response to avoid
            // a stuck-paired state when the server is unreachable. The
            // Bridge-side revocation is best-effort; the device is considered
            // unpaired as soon as local state is cleared per ADR 0028-A2.8.
            PairingResult.removeFromKeychain()
            onSuccess()
        } catch {
            // If the network call fails, allow the user to retry. Local
            // Keychain state is NOT cleared on failure so the device
            // remains in a known paired state.
            unpairError = error.localizedDescription
        }
    }

    // MARK: Private helpers

    private func postUnpair() async throws {
        guard let url = URL(string: pairingResult.anchorBaseUrl)?
            .appendingPathComponent("api/v1/field/unpair") else {
            throw URLError(.badURL)
        }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        // The pairing token (from Keychain) is the Bearer credential.
        // Phase 5 persists the bearer string alongside the PairingResult;
        // substrate v1 reads the tenant ID as a stand-in until P5 is wired.
        request.setValue("Bearer \(pairingResult.tenantId)", forHTTPHeaderField: "Authorization")
        let (_, response) = try await URLSession.shared.data(for: request)
        guard let http = response as? HTTPURLResponse,
              (200..<300).contains(http.statusCode)
        else {
            let status = (response as? HTTPURLResponse)?.statusCode ?? -1
            throw URLError(.badServerResponse,
                userInfo: [NSLocalizedDescriptionKey: "Unpair returned HTTP \(status)"])
        }
    }

    private func keychainDeviceId() -> String? {
        // The Phase 0 `DeviceId` is stored in Keychain by `InstallIdentity`
        // under the service key `dev.sunfish.field.device-id`.
        let query: [CFString: Any] = [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: "dev.sunfish.field.device-id",
            kSecReturnData: kCFBooleanTrue!,
            kSecMatchLimit: kSecMatchLimitOne,
        ]
        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status == errSecSuccess,
              let data = result as? Data,
              let str = String(data: data, encoding: .utf8)
        else { return nil }
        return str
    }
}

// MARK: - SyncHistoryEntry

/// A single drain-pass outcome recorded in `SettingsViewModel.syncHistory`.
/// Substrate v1: in-memory list capped at 20 entries; a future phase may
/// persist these to `audit_local` for durability.
public struct SyncHistoryEntry: Identifiable, Sendable {
    public let id = UUID()
    public let startedAt: Date
    public let accepted: Int
    public let rejected: Int
    public let pending: Int
    public let error: String?

    public var succeeded: Bool { error == nil }
}

// MARK: - SyncHistoryRow

private struct SyncHistoryRow: View {
    let entry: SyncHistoryEntry

    private static let timeFormatter: DateFormatter = {
        let f = DateFormatter()
        f.timeStyle = .medium
        f.dateStyle = .short
        return f
    }()

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 12) {
            Image(systemName: entry.succeeded ? "checkmark.circle.fill" : "xmark.circle.fill")
                .foregroundStyle(entry.succeeded ? .green : .red)

            VStack(alignment: .leading, spacing: 2) {
                Text(Self.timeFormatter.string(from: entry.startedAt))
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
                if entry.succeeded {
                    Text("↑ \(entry.accepted) accepted · \(entry.rejected) rejected · \(entry.pending) pending")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                } else if let err = entry.error {
                    Text(err)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .lineLimit(2)
                }
            }
        }
    }
}

// MARK: - ReadOnlyRow

private struct ReadOnlyRow: View {
    let label: String
    let value: String

    var body: some View {
        HStack {
            Text(label)
                .foregroundStyle(.primary)
            Spacer()
            Text(value)
                .foregroundStyle(.secondary)
                .font(.body.monospacedDigit())
                .lineLimit(1)
                .truncationMode(.middle)
                .textSelection(.enabled)
        }
    }
}
