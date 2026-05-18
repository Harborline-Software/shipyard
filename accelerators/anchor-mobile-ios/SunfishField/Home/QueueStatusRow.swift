import SwiftUI
import GRDB

// MARK: - QueueStatusSnapshot

/// A point-in-time snapshot of the outbound queue + blob-store state.
/// Read from GRDB once per refresh; drives the `QueueStatusRow` + the
/// compaction-policy colour coding per ADR 0028-A2.7.
public struct QueueStatusSnapshot: Equatable, Sendable {
    /// Number of rows in `event_queue` regardless of status.
    public let totalEventCount: Int
    /// Number of rows with `queue_status = 'pending'`.
    public let pendingCount: Int
    /// Number of rows with `queue_status = 'uploading'`.
    public let uploadingCount: Int
    /// Number of rows with `queue_status = 'failed-permanent'`.
    public let failedPermanentCount: Int
    /// Total bytes currently consumed by the blob store.
    public let blobBytes: Int64
    /// ISO-8601 string of the most-recently-acked event's `captured_at`,
    /// or `nil` when no events have ever been acked on this device.
    public let lastAckedAt: Date?

    public static let empty = QueueStatusSnapshot(
        totalEventCount: 0,
        pendingCount: 0,
        uploadingCount: 0,
        failedPermanentCount: 0,
        blobBytes: 0,
        lastAckedAt: nil
    )
}

// MARK: - QueueStatusViewModel

/// Observable view model for the queue-status row. Reads GRDB + BlobStore
/// state on demand; exposed to SwiftUI via `@StateObject` in the owning view.
///
/// Per W#23 P6 hand-off: no direct network calls — surfaces state from the
/// existing `SyncEngine` and underlying GRDB persistence.
@MainActor
public final class QueueStatusViewModel: ObservableObject {
    @Published public private(set) var snapshot: QueueStatusSnapshot = .empty
    @Published public private(set) var isRefreshing: Bool = false
    @Published public private(set) var isForceSyncing: Bool = false
    @Published public private(set) var lastRefreshError: String? = nil

    private let database: AppDatabase
    private let blobStore: BlobStore
    private let syncEngine: SyncEngine

    public init(database: AppDatabase, blobStore: BlobStore, syncEngine: SyncEngine) {
        self.database = database
        self.blobStore = blobStore
        self.syncEngine = syncEngine
    }

    // MARK: Derived state — compaction-policy thresholds

    /// True when either queue depth or blob usage crosses the 80% warning
    /// threshold per `CompactionPolicy.shouldWarn`.
    public var isWarning: Bool {
        CompactionPolicy.shouldWarn(
            queueDepth: snapshot.pendingCount,
            blobBytes: snapshot.blobBytes
        )
    }

    /// Non-nil when either queue depth or blob usage hits the 100% hard cap.
    /// The value carries the specific reason so the UI can display context.
    public var captureBlockReason: CompactionPolicy.CaptureBlockReason? {
        CompactionPolicy.captureBlocker(
            queueDepth: snapshot.pendingCount,
            blobBytes: snapshot.blobBytes
        )
    }

    /// Colour to apply to the queue-status row indicator per ADR 0028-A2.7:
    /// green (normal) → yellow (80%+ warn) → red (100% block).
    public var statusColor: Color {
        if captureBlockReason != nil { return .red }
        if isWarning { return .yellow }
        return .green
    }

    // MARK: Actions

    /// Refresh queue + blob-store stats from GRDB. Idempotent when already
    /// refreshing.
    public func refresh() async {
        guard !isRefreshing else { return }
        isRefreshing = true
        lastRefreshError = nil
        defer { isRefreshing = false }
        do {
            let snap = try await fetchSnapshot()
            snapshot = snap
        } catch {
            lastRefreshError = error.localizedDescription
        }
    }

    /// Trigger an immediate drain of the pending queue via the existing
    /// `SyncEngine`. Does NOT bypass retry policy — it simply kicks the
    /// engine now rather than waiting for the next background schedule.
    public func forceSyncNow() async {
        guard !isForceSyncing else { return }
        isForceSyncing = true
        defer { isForceSyncing = false }
        do {
            _ = try await syncEngine.drainNextBatch()
            await refresh()
        } catch {
            lastRefreshError = error.localizedDescription
        }
    }

    // MARK: Private helpers

    private func fetchSnapshot() async throws -> QueueStatusSnapshot {
        let rows = try await Task.detached(priority: .userInitiated) { [database] in
            try database.queue.read { db -> (total: Int, pending: Int, uploading: Int, failedPermanent: Int, lastAckedAt: Date?) in
                let total = try Int.fetchOne(db, sql: "SELECT COUNT(*) FROM event_queue") ?? 0
                let pending = try Int.fetchOne(
                    db,
                    sql: "SELECT COUNT(*) FROM event_queue WHERE queue_status = ?",
                    arguments: [QueueStatus.pending.rawValue]) ?? 0
                let uploading = try Int.fetchOne(
                    db,
                    sql: "SELECT COUNT(*) FROM event_queue WHERE queue_status = ?",
                    arguments: [QueueStatus.uploading.rawValue]) ?? 0
                let failedPermanent = try Int.fetchOne(
                    db,
                    sql: "SELECT COUNT(*) FROM event_queue WHERE queue_status = ?",
                    arguments: [QueueStatus.failedPermanent.rawValue]) ?? 0
                // Last acked: the most-recent captured_at among acked rows.
                let lastAckedStr = try String?.fetchOne(
                    db,
                    sql: "SELECT MAX(captured_at) FROM event_queue WHERE queue_status = ?",
                    arguments: [QueueStatus.acked.rawValue])
                var lastAckedAt: Date? = nil
                if let str = lastAckedStr {
                    lastAckedAt = ISO8601DateFormatter().date(from: str)
                }
                return (total, pending, uploading, failedPermanent, lastAckedAt)
            }
        }.value
        let blobBytes = (try? blobStore.totalBytes()) ?? 0
        return QueueStatusSnapshot(
            totalEventCount: rows.total,
            pendingCount: rows.pending,
            uploadingCount: rows.uploading,
            failedPermanentCount: rows.failedPermanent,
            blobBytes: blobBytes,
            lastAckedAt: rows.lastAckedAt
        )
    }
}

// MARK: - QueueStatusRow

/// Compact summary row rendered at the bottom of `HomeView`.
///
/// Displays pending-event count, blob-storage usage, last-sync timestamp,
/// and retry-policy status. Colour-coded per ADR 0028-A2.7 thresholds.
/// Tapping the row, or the "Sync Now" button, triggers `forceSyncNow`.
public struct QueueStatusRow: View {
    @ObservedObject var viewModel: QueueStatusViewModel

    public init(viewModel: QueueStatusViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            statusHeader
            syncDetails
            if let reason = viewModel.captureBlockReason {
                blockerBanner(reason: reason)
            } else if viewModel.isWarning {
                warningBanner
            }
        }
        .padding(12)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(Color(.systemBackground))
                .shadow(color: .black.opacity(0.08), radius: 4, x: 0, y: 2)
        )
        .overlay(
            RoundedRectangle(cornerRadius: 10)
                .stroke(viewModel.statusColor.opacity(0.4), lineWidth: 1.5)
        )
        .task { await viewModel.refresh() }
    }

    // MARK: Sub-views

    private var statusHeader: some View {
        HStack(spacing: 8) {
            Circle()
                .fill(viewModel.statusColor)
                .frame(width: 10, height: 10)
            Text(statusTitle)
                .font(.headline)
                .foregroundStyle(.primary)
            Spacer()
            if viewModel.isRefreshing || viewModel.isForceSyncing {
                ProgressView().scaleEffect(0.75)
            } else {
                Button(action: { Task { await viewModel.forceSyncNow() } }) {
                    Label("Sync Now", systemImage: "arrow.trianglehead.2.clockwise")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.tint)
                }
                .buttonStyle(.borderless)
                .disabled(viewModel.pendingCountForDisplay == 0)
            }
        }
    }

    private var syncDetails: some View {
        HStack(alignment: .firstTextBaseline, spacing: 16) {
            LabeledDetail(
                label: "Queued",
                value: viewModel.pendingCountDisplay)
            LabeledDetail(
                label: "Blobs",
                value: viewModel.blobBytesDisplay)
            LabeledDetail(
                label: "Last sync",
                value: viewModel.lastSyncDisplay)
            if viewModel.snapshot.failedPermanentCount > 0 {
                LabeledDetail(
                    label: "Failed",
                    value: "\(viewModel.snapshot.failedPermanentCount)",
                    valueColor: .red)
            }
        }
    }

    private func blockerBanner(reason: CompactionPolicy.CaptureBlockReason) -> some View {
        HStack(spacing: 6) {
            Image(systemName: "exclamationmark.octagon.fill")
                .foregroundStyle(.red)
            Text(blockerMessage(for: reason))
                .font(.caption)
                .foregroundStyle(.red)
        }
    }

    private var warningBanner: some View {
        HStack(spacing: 6) {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundStyle(.yellow)
            Text("Queue near capacity. Sync to free space.")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }

    // MARK: Display helpers

    private var statusTitle: String {
        if viewModel.captureBlockReason != nil { return "Queue Full" }
        if viewModel.isWarning { return "Queue Near Capacity" }
        if viewModel.snapshot.uploadingCount > 0 { return "Syncing…" }
        return "Queue Status"
    }

    private func blockerMessage(for reason: CompactionPolicy.CaptureBlockReason) -> String {
        switch reason {
        case .queueFull(let count):
            return "Queue full (\(count) events). New captures blocked until queue drains."
        case .blobStorageExceeded(let bytes):
            let mb = bytes / (1024 * 1024)
            return "Blob storage full (\(mb) MB). New captures blocked until blobs sync."
        }
    }
}

// MARK: - QueueStatusViewModel display helpers

extension QueueStatusViewModel {
    /// Pending count used by the Sync Now button enabled-state check.
    var pendingCountForDisplay: Int { snapshot.pendingCount }

    var pendingCountDisplay: String {
        let n = snapshot.pendingCount
        if n == 0 && snapshot.totalEventCount == 0 { return "None" }
        return "\(n)"
    }

    var blobBytesDisplay: String {
        let bytes = snapshot.blobBytes
        if bytes == 0 { return "0 MB" }
        let mb = Double(bytes) / (1024 * 1024)
        return String(format: "%.1f MB", mb)
    }

    var lastSyncDisplay: String {
        guard let date = snapshot.lastAckedAt else { return "Never" }
        let formatter = RelativeDateTimeFormatter()
        formatter.unitsStyle = .abbreviated
        return formatter.localizedString(for: date, relativeTo: Date())
    }
}

// MARK: - LabeledDetail

/// Compact key/value pair used within `QueueStatusRow`.
private struct LabeledDetail: View {
    let label: String
    let value: String
    var valueColor: Color = .primary

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
                .textCase(.uppercase)
            Text(value)
                .font(.caption.monospacedDigit())
                .foregroundStyle(valueColor)
        }
    }
}
