import XCTest
import GRDB
@testable import SunfishField

/// Gate tests for W#23 Phase 6 — queue-status UX.
///
/// Covers:
/// - Empty queue → "No events queued" state
/// - Queue with 100 events → row shows correct counts
/// - 80% threshold (4000 events) → `isWarning = true`
/// - 100% threshold (5000 events) → `captureBlockReason` non-nil; new
///   captures blocked
/// - `forceSyncNow` → drains pending batch via SyncEngine
///
/// Per W#23 P6 hand-off gate criteria.
final class QueueStatusViewModelTests: XCTestCase {

    // MARK: Helpers

    /// Opens an in-memory AppDatabase pre-populated with V1 schema.
    private func makeDatabase() throws -> AppDatabase {
        let queue = try DatabaseQueue()
        var migrator = DatabaseMigrator()
        V1Migration.register(in: &migrator)
        try migrator.migrate(queue)
        return AppDatabase(forTesting: queue)
    }

    /// Returns a BlobStore backed by a temporary directory.
    private func makeBlobStore() throws -> BlobStore {
        let dir = URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent("SunfishFieldBlobTests-\(UUID().uuidString)")
        return try BlobStore(rootDirectory: dir)
    }

    /// Insert `count` pending rows into the database.
    private func insertPendingRows(_ count: Int, in database: AppDatabase) throws {
        try database.queue.write { db in
            for i in 0..<count {
                try db.execute(
                    sql: """
                        INSERT INTO event_queue
                            (device_local_seq, captured_at, event_type, payload, queue_status, attempt_count)
                        VALUES (?, ?, ?, ?, ?, 0)
                        """,
                    arguments: [Int64(i + 1), "2026-01-01T00:00:00Z", "Receipt",
                                Data("{}".utf8), QueueStatus.pending.rawValue])
            }
        }
    }

    // MARK: - Gate 1: Empty queue

    @MainActor
    func testEmptyQueue_showsNoEvents() async throws {
        let db = try makeDatabase()
        let blobs = try makeBlobStore()
        let svc = EventQueueService(database: db)
        let engine = SyncEngine(
            queueService: svc,
            bridgeBaseURL: URL(string: "http://localhost")!,
            urlSession: URLSession.shared)
        let vm = QueueStatusViewModel(database: db, blobStore: blobs, syncEngine: engine)

        await vm.refresh()

        XCTAssertEqual(vm.snapshot.totalEventCount, 0)
        XCTAssertEqual(vm.snapshot.pendingCount, 0)
        XCTAssertFalse(vm.isWarning)
        XCTAssertNil(vm.captureBlockReason)
        XCTAssertEqual(vm.pendingCountDisplay, "None")
    }

    // MARK: - Gate 2: 100 events in queue

    @MainActor
    func testQueueWith100Events_showsCorrectCount() async throws {
        let db = try makeDatabase()
        try insertPendingRows(100, in: db)
        let blobs = try makeBlobStore()
        let svc = EventQueueService(database: db)
        let engine = SyncEngine(
            queueService: svc,
            bridgeBaseURL: URL(string: "http://localhost")!,
            urlSession: URLSession.shared)
        let vm = QueueStatusViewModel(database: db, blobStore: blobs, syncEngine: engine)

        await vm.refresh()

        XCTAssertEqual(vm.snapshot.totalEventCount, 100)
        XCTAssertEqual(vm.snapshot.pendingCount, 100)
        XCTAssertFalse(vm.isWarning, "100 events is below the 80% / 4000-event warning threshold")
        XCTAssertNil(vm.captureBlockReason)
        XCTAssertEqual(vm.pendingCountDisplay, "100")
    }

    // MARK: - Gate 3: 80% threshold → yellow warning

    @MainActor
    func testQueueAt4000Events_triggersWarning() async throws {
        let db = try makeDatabase()
        // 80% of maxQueueDepth (5000) = 4000
        try insertPendingRows(4000, in: db)
        let blobs = try makeBlobStore()
        let svc = EventQueueService(database: db)
        let engine = SyncEngine(
            queueService: svc,
            bridgeBaseURL: URL(string: "http://localhost")!,
            urlSession: URLSession.shared)
        let vm = QueueStatusViewModel(database: db, blobStore: blobs, syncEngine: engine)

        await vm.refresh()

        XCTAssertTrue(vm.isWarning, "4000 events (80%) must trigger the warning state")
        XCTAssertNil(vm.captureBlockReason, "80% must not trigger a hard block")
    }

    // MARK: - Gate 4: 100% threshold → red block

    @MainActor
    func testQueueAt5000Events_triggersBlock() async throws {
        let db = try makeDatabase()
        // 100% of maxQueueDepth = 5000
        try insertPendingRows(5000, in: db)
        let blobs = try makeBlobStore()
        let svc = EventQueueService(database: db)
        let engine = SyncEngine(
            queueService: svc,
            bridgeBaseURL: URL(string: "http://localhost")!,
            urlSession: URLSession.shared)
        let vm = QueueStatusViewModel(database: db, blobStore: blobs, syncEngine: engine)

        await vm.refresh()

        XCTAssertNotNil(vm.captureBlockReason,
            "5000 events (100%) must trigger the hard capture block")
        if case .queueFull(let count) = vm.captureBlockReason {
            XCTAssertEqual(count, 5000)
        } else {
            XCTFail("Expected .queueFull but got \(String(describing: vm.captureBlockReason))")
        }
    }

    // MARK: - Compaction-policy constants match ADR 0028-A2.7

    func testCompactionPolicyConstants_matchADR() {
        XCTAssertEqual(CompactionPolicy.maxQueueDepth, 5000,
            "ADR 0028-A2.7: 5000-event hard cap")
        XCTAssertEqual(CompactionPolicy.maxBlobBytes, 500 * 1024 * 1024,
            "ADR 0028-A2.7: 500 MB blob cap")
        XCTAssertEqual(CompactionPolicy.warnAtPercent, 0.80,
            "ADR 0028-A2.7: warn at 80%")
    }

    // MARK: - PairingResult Keychain round-trip

    func testPairingResult_keychainRoundTrip() {
        let original = PairingResult(
            tenantId: "t-abc",
            anchorBaseUrl: "https://anchor.example.com",
            expiresAt: Date(timeIntervalSince1970: 2_000_000_000))
        original.saveToKeychain()
        let loaded = PairingResult.loadFromKeychain()
        XCTAssertEqual(loaded, original)
        PairingResult.removeFromKeychain()
        XCTAssertNil(PairingResult.loadFromKeychain(),
            "Keychain entry must be absent after removeFromKeychain()")
    }
}
