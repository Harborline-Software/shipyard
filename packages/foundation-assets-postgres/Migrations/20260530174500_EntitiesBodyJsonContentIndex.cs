using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunfish.Foundation.Assets.Postgres.Migrations
{
    /// <summary>
    /// Adds a partial GIN content index over <c>entities.body_json</c> for JSONB
    /// containment (<c>@&gt;</c>) lookups on live (non-tombstoned) rows. This is the
    /// dynamic-forms keystone's JSONB content-index strategy (ADR 0055 Rev 4,
    /// net-arch Finding A + the JSONB index-sequencing invariant): the shipped
    /// <c>InitialSchema</c> migration created only scalar B-tree indexes
    /// (<c>ix_entities_schema</c>, <c>ix_entities_tenant</c>); this adds the first
    /// content index over the JSONB body.
    /// </summary>
    /// <remarks>
    /// The index is raw-SQL-only (no <c>OnModelCreating</c> mapping), so it is
    /// intentionally absent from <see cref="AssetStoreDbContext"/>'s model and the
    /// model snapshot — EF cannot express a partial GIN <c>jsonb_path_ops</c> index
    /// declaratively.
    /// </remarks>
    public partial class EntitiesBodyJsonContentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // jsonb_path_ops keeps the GIN index small and fast for containment (@>)
            // queries at the cost of key-existence (?) operators, which the form-engine
            // query shapes do not need. The partial predicate (deleted_at IS NULL)
            // excludes tombstoned rows so the index only covers live entities.
            //
            // CONCURRENTLY avoids an ACCESS EXCLUSIVE lock on a populated entities
            // table in production. CONCURRENTLY cannot run inside a transaction, so the
            // statement is emitted with suppressTransaction: true — EF would otherwise
            // wrap migration commands in a transaction and Postgres would reject it.
            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_entities_body_json_gin
                    ON entities USING gin (body_json jsonb_path_ops)
                    WHERE deleted_at IS NULL;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_entities_body_json_gin;",
                suppressTransaction: true);
        }
    }
}
