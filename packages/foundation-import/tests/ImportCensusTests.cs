namespace Sunfish.Foundation.Import.Tests;

/// <summary>
/// Unit tests for the <see cref="ImportCensus"/> record-conservation primitive
/// (ADR 0100 C2; C-CENSUS). Proves every record routes into exactly one bucket
/// and the conservation invariant
/// (<c>Inserted+Updated+Skipped+Rejected+Halted == source</c>) holds / fails loud.
/// </summary>
public sealed class ImportCensusTests
{
    private sealed record FakeRecord(string Id);

    private static ImportOutcome<FakeRecord> Inserted() => new ImportOutcome<FakeRecord>.Inserted(new FakeRecord("x"));
    private static ImportOutcome<FakeRecord> Updated() => new ImportOutcome<FakeRecord>.Updated(new FakeRecord("x"));
    private static ImportOutcome<FakeRecord> Skipped() => new ImportOutcome<FakeRecord>.Skipped(new FakeRecord("x"));

    private static ImportOutcome<FakeRecord> Rejected() =>
        new ImportOutcome<FakeRecord>.Rejected(ImportFailure.Of("x", "Account", ImportRejectReason.InvalidFieldValue));

    [Fact]
    public void Record_routes_each_arm_into_its_own_bucket()
    {
        var census = new ImportCensus();
        census.Record(Inserted());
        census.Record(Inserted());
        census.Record(Updated());
        census.Record(Skipped());
        census.Record(Skipped());
        census.Record(Skipped());
        census.Record(Rejected());

        Assert.Equal(2, census.Inserted);
        Assert.Equal(1, census.Updated);
        Assert.Equal(3, census.Skipped);
        Assert.Equal(1, census.Rejected);
        Assert.Equal(0, census.Halted);
        Assert.Equal(7, census.Accounted);
    }

    [Fact]
    public void Halted_records_count_toward_conservation()
    {
        var census = new ImportCensus();
        census.Record(Inserted());
        census.RecordHalted();
        census.RecordHalted();

        Assert.Equal(1, census.Inserted);
        Assert.Equal(2, census.Halted);
        Assert.Equal(3, census.Accounted);
    }

    [Fact]
    public void Conservation_holds_when_every_source_record_is_accounted()
    {
        // C-CENSUS: a fixture where every record is importable OR deliberately broken;
        // every record lands in exactly one bucket → census == source count.
        var census = new ImportCensus();
        // 4 importable + 1 deliberately-broken == 5 source records.
        census.Record(Inserted());
        census.Record(Inserted());
        census.Record(Updated());
        census.Record(Skipped());
        census.Record(Rejected());

        Assert.True(census.IsConserved(sourceRecordCount: 5));
        census.AssertConserved(5); // does not throw
    }

    [Fact]
    public void AssertConserved_throws_loudly_when_a_record_vanished()
    {
        var census = new ImportCensus();
        census.Record(Inserted()); // only 1 accounted...

        // ...but the source had 2 records → one vanished without a report line.
        var ex = Assert.Throws<ImportCensusViolationException>(() => census.AssertConserved(2));
        Assert.Equal(2, ex.ExpectedSourceCount);
        Assert.Equal(1, ex.AccountedCount);
        Assert.False(census.IsConserved(2));
    }

    [Fact]
    public void AssertConserved_throws_when_a_record_was_double_counted()
    {
        var census = new ImportCensus();
        census.Record(Inserted());
        census.Record(Inserted()); // 2 accounted but source only had 1

        Assert.Throws<ImportCensusViolationException>(() => census.AssertConserved(1));
    }

    [Fact]
    public void Record_rejects_null_outcome()
    {
        var census = new ImportCensus();
        Assert.Throws<ArgumentNullException>(() => census.Record<FakeRecord>(null!));
    }
}
