using Microsoft.EntityFrameworkCore;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

/// <summary>
/// Verifies the <c>EntitiesBodyJsonContentIndex</c> migration applies cleanly through the
/// fixture's <c>MigrateAsync</c> path and produces the expected partial GIN content index
/// over <c>entities.body_json</c>. The <c>CREATE INDEX CONCURRENTLY</c> statement runs with
/// <c>suppressTransaction: true</c>, so this test also exercises the non-transactional
/// migration path end-to-end against a real PostgreSQL 16 container.
/// </summary>
public sealed class EntitiesBodyJsonContentIndexTests : IClassFixture<PostgresAssetStoreFixture>
{
    private readonly PostgresAssetStoreFixture _fixture;

    public EntitiesBodyJsonContentIndexTests(PostgresAssetStoreFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_CreatesPartialGinContentIndex_OnBodyJson()
    {
        await using var ctx = _fixture.CreateFactory().CreateDbContext();

        var defs = await ctx.Database
            .SqlQueryRaw<string>(
                """
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE indexname = 'ix_entities_body_json_gin'
                """)
            .ToListAsync();

        var def = Assert.Single(defs);

        // GIN access method with jsonb_path_ops keeps the index small/fast for containment.
        Assert.Contains("USING gin", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jsonb_path_ops", def, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("body_json", def, StringComparison.OrdinalIgnoreCase);

        // Partial predicate excludes tombstoned rows so the index only covers live entities.
        Assert.Contains("deleted_at IS NULL", def, StringComparison.OrdinalIgnoreCase);
    }
}
