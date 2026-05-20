import Foundation
import GRDB

/// Local-first persistence layer for the field-capture app.
///
/// Wraps GRDB's `DatabaseQueue` against a file-backed SQLite database in the
/// app sandbox at `Library/Application Support/SunfishField/database.sqlite`.
///
/// **Encryption status (Phase 2 substrate v1):** plaintext SQLite + iOS file-
/// system encryption via `NSFileProtectionComplete` (the file is unreadable
/// while the device is locked; readable once unlocked). SQLCipher whole-
/// database encryption is deferred to a follow-up phase pending the SPM-
/// packaging story for SQLCipher (the standard `groue/GRDB.swift` package
/// targets system SQLite which does not include SQLCipher patches; switching
/// requires either a fork or a separate SPM-packaged SQLCipher.swift, both
/// of which have unsettled licensing + maintenance trade-offs at the
/// `pre-release-latest-first` policy stage). Per ADR 0028-A2.3 the encryption
/// is required for shipping; substrate v1 documents the gap as a halt-
/// condition deferral with TODO.
public final class AppDatabase: @unchecked Sendable {
    /// File-system path to the database under the app sandbox.
    public let databasePath: URL

    /// Underlying GRDB queue. All reads + writes funnel through here.
    public let queue: DatabaseQueue

    /// Open (or create) the database at the standard sandbox location and
    /// run any pending schema migrations. The caller owns the returned
    /// instance for the lifetime of the app.
    public static func open(at directory: URL) throws -> AppDatabase {
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let url = directory.appendingPathComponent("database.sqlite")
        var configuration = Configuration()
        configuration.foreignKeysEnabled = true
        configuration.label = "SunfishField"
        let queue = try DatabaseQueue(path: url.path, configuration: configuration)
        try queue.write { db in
            // TODO(SQLCipher): apply the SQLCipher key here once the SPM
            // packaging is settled. For substrate v1 the database is
            // plaintext under file-system protection only.
            _ = db
        }
        let database = AppDatabase(databasePath: url, queue: queue)
        try database.applyMigrations()
        try database.applyDataProtection()
        return database
    }

    private init(databasePath: URL, queue: DatabaseQueue) {
        self.databasePath = databasePath
        self.queue = queue
    }

    /// Designated initialiser for unit tests: wraps a caller-supplied
    /// `DatabaseQueue` (typically in-memory) without applying data-protection
    /// attributes. Not for production use.
    internal init(forTesting queue: DatabaseQueue) {
        // Use a sentinel path; actual I/O goes to the in-memory queue.
        self.databasePath = URL(fileURLWithPath: ":memory:")
        self.queue = queue
    }

    private func applyMigrations() throws {
        var migrator = DatabaseMigrator()
        V1Migration.register(in: &migrator)
        V2Migration.register(in: &migrator)
        try migrator.migrate(queue)
    }

    // MARK: Inspection cache

    /// Replace the entire inspections cache with the supplied list.
    public func cacheInspections(_ items: [InspectionListItem]) throws {
        let now = ISO8601DateFormatter().string(from: Date())
        try queue.write { db in
            try db.execute(sql: "DELETE FROM inspections")
            for item in items {
                try db.execute(
                    sql: """
                    INSERT INTO inspections
                        (id, property_id, phase, scheduled_for, template_name,
                         total_items, responded_items, cached_at)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    arguments: [
                        item.id, item.propertyId, item.phase,
                        item.scheduledFor, item.templateName,
                        item.totalItems, item.respondedItems, now,
                    ]
                )
            }
        }
    }

    /// Returns all cached inspections along with their cache timestamp.
    /// Returns an empty array if the table is empty.
    public func cachedInspections() throws -> [(item: InspectionListItem, cachedAt: Date)] {
        try queue.read { db in
            let rows = try Row.fetchAll(db, sql: """
                SELECT id, property_id, phase, scheduled_for, template_name,
                       total_items, responded_items, cached_at
                FROM inspections
                ORDER BY scheduled_for ASC
                """)
            let formatter = ISO8601DateFormatter()
            return rows.compactMap { row -> (InspectionListItem, Date)? in
                guard
                    let id: String = row["id"],
                    let propertyId: String = row["property_id"],
                    let phase: String = row["phase"],
                    let totalItems: Int = row["total_items"],
                    let respondedItems: Int = row["responded_items"],
                    let cachedAtStr: String = row["cached_at"],
                    let cachedAt = formatter.date(from: cachedAtStr)
                else { return nil }
                let item = InspectionListItem(
                    id: id,
                    propertyId: propertyId,
                    phase: phase,
                    scheduledFor: row["scheduled_for"],
                    templateName: row["template_name"],
                    totalItems: totalItems,
                    respondedItems: respondedItems
                )
                return (item, cachedAt)
            }
        }
    }

    /// Mark the database file as Complete-protection (per ADR 0028-A2.3).
    /// Background URLSession reads need to consult the database while the
    /// device is locked — Phase 4 will need to negotiate the protection
    /// class against background-task constraints; substrate v1 ships
    /// Complete and documents the open question.
    ///
    /// macOS hosts (test runners + dev machines) and Linux hosts skip this
    /// call — the file-system protection attribute is iOS-only. macCatalyst
    /// is included since it ships against the iOS Foundation APIs.
    private func applyDataProtection() throws {
        #if os(iOS) || targetEnvironment(macCatalyst)
        let attributes: [FileAttributeKey: Any] = [
            .protectionKey: FileProtectionType.complete,
        ]
        try FileManager.default.setAttributes(attributes, ofItemAtPath: databasePath.path)
        #endif
    }
}
