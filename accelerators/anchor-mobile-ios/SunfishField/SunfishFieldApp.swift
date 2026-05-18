import SwiftUI

// `@main` is gated to Xcode builds only. The SPM library target compiles
// the same source file but skips the entry-point declaration so the
// `_main` symbol does not collide with the SPM test runner's `_main`.
#if !SWIFT_PACKAGE
@main
#endif
struct SunfishFieldApp: App {
    @StateObject private var environment = FieldAppEnvironment.shared

    // Shared infrastructure initialised once at app start.
    // Phase 2 opens the database; Phase 4 wires the sync engine.
    private static let appDatabase: AppDatabase? = try? AppDatabase.open(
        at: Self.appSupportDirectory)
    private static let blobStore: BlobStore? = try? BlobStore(
        rootDirectory: Self.blobDirectory)
    private static var syncEngine: SyncEngine? {
        guard let db = appDatabase, let blobs = blobStore,
              let baseURL = FieldAppEnvironment.shared.pairingResult
                  .flatMap({ URL(string: $0.anchorBaseUrl) })
        else { return nil }
        let queueService = EventQueueService(database: db)
        return SyncEngine(
            queueService: queueService,
            bridgeBaseURL: baseURL,
            pairingTokenBearer: FieldAppEnvironment.shared.pairingResult?.tenantId,
            urlSession: URLSession.shared)
    }

    var body: some Scene {
        WindowGroup {
            if environment.isPaired,
               let pairingResult = environment.pairingResult,
               let database = Self.appDatabase,
               let blobStore = Self.blobStore,
               let syncEngine = Self.syncEngine
            {
                HomeView(
                    pairingResult: pairingResult,
                    database: database,
                    blobStore: blobStore,
                    syncEngine: syncEngine)
                    .environmentObject(environment)
            } else {
                // Phase 5 PairingFlow: renders until the device is paired.
                // Substrate v1 shows a placeholder until P5 is linked.
                PairingPlaceholderView()
                    .environmentObject(environment)
            }
        }
    }

    // MARK: Sandbox paths

    private static var appSupportDirectory: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return base.appendingPathComponent("SunfishField")
    }

    private static var blobDirectory: URL {
        appSupportDirectory.appendingPathComponent("blobs")
    }
}

// MARK: - PairingPlaceholderView

/// Temporary placeholder for the Phase 5 `PairingFlow` view.
/// Replaced by the real `PairingFlow` once P5 is linked into the target.
private struct PairingPlaceholderView: View {
    @EnvironmentObject private var environment: FieldAppEnvironment

    var body: some View {
        VStack(spacing: 20) {
            Image(systemName: "link.circle.fill")
                .font(.system(size: 64))
                .foregroundStyle(.tint)
            Text("Sunfish Field")
                .font(.largeTitle.weight(.bold))
            Text("Pair this device with Anchor to begin.")
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)

            // Development convenience: simulate a successful pairing so
            // HomeView can be exercised in the Simulator without a live
            // Bridge. Conditionally compiled out of release builds.
            #if DEBUG
            Button("Simulate Pairing (Debug)") {
                let mock = PairingResult(
                    tenantId: "tenant-dev",
                    anchorBaseUrl: "http://localhost:5000",
                    expiresAt: Date().addingTimeInterval(86400 * 30))
                mock.saveToKeychain()
                environment.pairingResult = mock
            }
            .buttonStyle(.borderedProminent)
            #endif
        }
        .padding(32)
    }
}
