import Foundation
import GRDB

/// Phase 3 schema migration — adds the inspections offline cache table.
/// Per W#23.3 Phase 3 hand-off: cache populated on successful
/// `GET /api/v1/field/inspections`; stale indicator if `cached_at > 24h`.
public enum V2Migration {
    public static let identifier = "v2"

    public static func register(in migrator: inout DatabaseMigrator) {
        migrator.registerMigration(identifier) { db in
            try db.create(table: "inspections") { t in
                t.primaryKey("id", .text)
                t.column("property_id", .text).notNull()
                t.column("phase", .text).notNull()
                t.column("scheduled_for", .text)
                t.column("template_name", .text)
                t.column("total_items", .integer).notNull().defaults(to: 0)
                t.column("responded_items", .integer).notNull().defaults(to: 0)
                t.column("cached_at", .text).notNull()
            }
        }
    }
}
