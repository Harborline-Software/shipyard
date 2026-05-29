namespace Sunfish.Foundation.Import.Tests;

/// <summary>
/// Unit tests for the <see cref="ImportOutcome{T}"/> discriminated union and
/// the allowlisted <see cref="ImportFailure"/> reject projection (ADR 0100 C2 /
/// OQ-A / C9). A stand-in domain record (<see cref="FakeRecord"/>) substitutes
/// for a real block entity — A0 is the primitives package, not a consumer.
/// </summary>
public sealed class ImportOutcomeTests
{
    private sealed record FakeRecord(string Id);

    [Fact]
    public void Inserted_arm_carries_record_and_inserted_action()
    {
        var record = new FakeRecord("acct-1");
        ImportOutcome<FakeRecord> outcome = new ImportOutcome<FakeRecord>.Inserted(record, "new account");

        Assert.IsType<ImportOutcome<FakeRecord>.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.False(outcome.IsRejected);
        var inserted = (ImportOutcome<FakeRecord>.Inserted)outcome;
        Assert.Same(record, inserted.Record);
        Assert.Equal("new account", inserted.Detail);
    }

    [Fact]
    public void Updated_arm_carries_updated_action()
    {
        ImportOutcome<FakeRecord> outcome = new ImportOutcome<FakeRecord>.Updated(new FakeRecord("acct-1"));
        Assert.Equal(ImportAction.Updated, outcome.Action);
        Assert.False(outcome.IsRejected);
    }

    [Fact]
    public void Skipped_arm_carries_skipped_action_and_warning_detail()
    {
        ImportOutcome<FakeRecord> outcome =
            new ImportOutcome<FakeRecord>.Skipped(new FakeRecord("je-1"), "posted JE immutable; drift on memo");
        Assert.Equal(ImportAction.Skipped, outcome.Action);
        Assert.False(outcome.IsRejected);
        Assert.Equal("posted JE immutable; drift on memo", ((ImportOutcome<FakeRecord>.Skipped)outcome).Detail);
    }

    [Fact]
    public void Rejected_arm_carries_failure_and_null_action_and_no_record()
    {
        var failure = ImportFailure.Of(
            externalRef: "ACC-0042",
            docType: "Account",
            reason: ImportRejectReason.InvalidFieldValue,
            fieldName: "account_type",
            ruleViolated: "unknown account_type after parent walk");

        ImportOutcome<FakeRecord> outcome = new ImportOutcome<FakeRecord>.Rejected(failure);

        Assert.IsType<ImportOutcome<FakeRecord>.Rejected>(outcome);
        Assert.Null(outcome.Action);
        Assert.True(outcome.IsRejected);
        // No T on the reject arm — the only payload is the structured failure.
        var rejected = (ImportOutcome<FakeRecord>.Rejected)outcome;
        Assert.Equal("ACC-0042", rejected.Failure.ExternalRef);
        Assert.Equal("InvalidFieldValue", rejected.Failure.ReasonCode);
    }

    [Fact]
    public void Orchestrator_consumes_union_via_exhaustive_switch_expression()
    {
        // The canonical consumption shape: a switch over the four arms with a
        // THROWING discard. The union is closed to its own assembly (private
        // protected ctor), so no external arm can exist; the discard cannot be
        // hit at runtime, but the compiler still requires it (the base record is
        // not `sealed`). The discard THROWS rather than silently continuing — so
        // if a fifth arm were ever added IN this assembly without updating this
        // switch, it fails LOUD (the OQ-A "no silent drop" intent), not silently.
        static string Describe(ImportOutcome<FakeRecord> o) => o switch
        {
            ImportOutcome<FakeRecord>.Inserted => "inserted",
            ImportOutcome<FakeRecord>.Updated => "updated",
            ImportOutcome<FakeRecord>.Skipped => "skipped",
            ImportOutcome<FakeRecord>.Rejected => "rejected",
            _ => throw new InvalidOperationException($"Unhandled ImportOutcome arm: {o.GetType().Name}"),
        };

        Assert.Equal("inserted", Describe(new ImportOutcome<FakeRecord>.Inserted(new FakeRecord("x"))));
        Assert.Equal("updated", Describe(new ImportOutcome<FakeRecord>.Updated(new FakeRecord("x"))));
        Assert.Equal("skipped", Describe(new ImportOutcome<FakeRecord>.Skipped(new FakeRecord("x"))));
        Assert.Equal("rejected", Describe(
            new ImportOutcome<FakeRecord>.Rejected(ImportFailure.Of("x", "Account", ImportRejectReason.MissingRequiredField))));
    }

    [Fact]
    public void ImportFailure_factory_stringifies_reason_code()
    {
        var failure = ImportFailure.Of("PINV-1", "Purchase Invoice", ImportRejectReason.UnresolvedReference);
        Assert.Equal("UnresolvedReference", failure.ReasonCode);
        Assert.Equal("Purchase Invoice", failure.DocType);
        Assert.Null(failure.FieldName);
        Assert.Null(failure.RuleViolated);
    }

    [Fact]
    public void ImportFailure_has_no_field_capable_of_holding_raw_payload_or_pii()
    {
        // C9 / sec-eng amendment B structural assertion: the reject projection's
        // public surface is allowlisted scalar identifiers only — there is no
        // property that can carry the raw Erpnext*Source payload, party PII, or a
        // monetary amount. This guards the C-LOG-REJECT invariant at the type level.
        var allowedProps = new[]
        {
            nameof(ImportFailure.ExternalRef),
            nameof(ImportFailure.DocType),
            nameof(ImportFailure.ReasonCode),
            nameof(ImportFailure.FieldName),
            nameof(ImportFailure.RuleViolated),
        };

        var actualProps = typeof(ImportFailure)
            .GetProperties()
            .Select(p => p.Name)
            // EqualityContract is the compiler-generated record member; ignore it.
            .Where(n => n != "EqualityContract")
            .ToArray();

        Assert.Equal(allowedProps.OrderBy(x => x), actualProps.OrderBy(x => x));

        // Every property is a string or nullable string — no object/dynamic/byte[]
        // surface that could smuggle a payload.
        foreach (var prop in typeof(ImportFailure).GetProperties().Where(p => p.Name != "EqualityContract"))
        {
            Assert.Equal(typeof(string), prop.PropertyType);
        }
    }
}
