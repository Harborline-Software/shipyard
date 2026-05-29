using System.Diagnostics;
using System.Text;
using Sunfish.Blocks.People.Foundation.Migration;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Import.Census;
using Sunfish.Foundation.Import.Outcomes;
using Xunit;
using FoundationOutcome = Sunfish.Foundation.Import.Outcomes.ImportOutcome<Sunfish.Blocks.People.Foundation.Models.Party>;

namespace Sunfish.Blocks.People.Foundation.Tests;

/// <summary>
/// Fixture tests for the Pass-2 ERPNext party upserter against synthetic,
/// ERPNext-shaped fixtures (no real dump). Covers insert/update/skip
/// idempotency, the reject path, census conservation, and the
/// PII-non-emission invariant (ADR 0098 S1 / ADR 0100 C9).
/// </summary>
public class Pass2PartyUpserterTests
{
    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    private static (Pass2PartyUpserter Upserter, InMemoryPartyRepository Repo) NewSut()
    {
        var repo = new InMemoryPartyRepository();
        return (new Pass2PartyUpserter(repo, repo), repo);
    }

    // ── Customer / Supplier upsert ────────────────────────────────────────

    [Fact]
    public async Task UpsertCustomer_FreshSource_Inserts_AttachesRole_Contacts()
    {
        var (sut, repo) = NewSut();
        var source = new ErpnextPartyCustomerSource(
            Name: "CUST-0001",
            Modified: "2026-05-16 22:00:00",
            CustomerName: "Jane Doe",
            CustomerType: "Individual",
            EmailId: "jane@example.com",
            MobileNo: "+14155551234",
            TaxId: "TX-99");

        var outcome = await sut.UpsertCustomerAsync(source, Tenant(), Actor());

        var inserted = Assert.IsType<FoundationOutcome.Inserted>(outcome);
        Assert.Equal(ImportAction.Inserted, outcome.Action);
        Assert.Equal(PartyKind.Person, inserted.Record.Kind);
        Assert.Equal("Jane Doe", inserted.Record.DisplayName);

        var roles = await repo.GetActiveRolesAsync(inserted.Record.Id);
        Assert.Single(roles);
        Assert.Equal(PartyRoleName.Customer, roles[0].RoleName);
        Assert.Equal("CUST-0001", roles[0].RoleRecordId);

        var emails = await repo.GetActiveEmailsAsync(inserted.Record.Id);
        Assert.Single(emails);
        var phones = await repo.GetActivePhonesAsync(inserted.Record.Id);
        Assert.Single(phones);
    }

    [Fact]
    public async Task UpsertCustomer_CompanyType_MapsToOrganization()
    {
        var (sut, _) = NewSut();
        var outcome = await sut.UpsertCustomerAsync(
            new ErpnextPartyCustomerSource("CUST-0002", "2026-05-16 22:00:00", "Acme Holdings LLC", "Company"),
            Tenant(), Actor());

        var inserted = Assert.IsType<FoundationOutcome.Inserted>(outcome);
        Assert.Equal(PartyKind.Organization, inserted.Record.Kind);
    }

    [Fact]
    public async Task UpsertSupplier_FreshSource_AttachesVendorRole()
    {
        var (sut, repo) = NewSut();
        var outcome = await sut.UpsertSupplierAsync(
            new ErpnextPartySupplierSource("SUP-0001", "2026-05-16 22:00:00", "Bob's Plumbing", "Individual"),
            Tenant(), Actor());

        var inserted = Assert.IsType<FoundationOutcome.Inserted>(outcome);
        var roles = await repo.GetActiveRolesAsync(inserted.Record.Id);
        Assert.Equal(PartyRoleName.Vendor, roles[0].RoleName);
    }

    [Fact]
    public async Task UpsertCustomer_SameModified_Skips_Idempotent()
    {
        var (sut, _) = NewSut();
        var source = new ErpnextPartyCustomerSource("CUST-0003", "2026-05-16 22:00:00", "Jane Doe", "Individual");
        var actor = Actor();

        var first = await sut.UpsertCustomerAsync(source, Tenant(), actor);
        Assert.IsType<FoundationOutcome.Inserted>(first);

        var second = await sut.UpsertCustomerAsync(source, Tenant(), actor);
        var skipped = Assert.IsType<FoundationOutcome.Skipped>(second);
        Assert.Equal(ImportAction.Skipped, skipped.Action);
        // Idempotent: same party, not a duplicate.
        Assert.Equal(((FoundationOutcome.Inserted)first).Record.Id, skipped.Record.Id);
    }

    [Fact]
    public async Task UpsertCustomer_NewerModified_Updates()
    {
        var (sut, _) = NewSut();
        var actor = Actor();
        var v1 = new ErpnextPartyCustomerSource("CUST-0004", "2026-05-16 22:00:00", "Jane Doe", "Individual");
        var v2 = v1 with { Modified = "2026-05-17 09:00:00", CustomerName = "Jane A. Doe" };

        await sut.UpsertCustomerAsync(v1, Tenant(), actor);
        var outcome = await sut.UpsertCustomerAsync(v2, Tenant(), actor);

        var updated = Assert.IsType<FoundationOutcome.Updated>(outcome);
        Assert.Equal("Jane A. Doe", updated.Record.DisplayName);
    }

    // ── Reject path ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertCustomer_MissingCustomerName_Rejected_MissingRequiredField()
    {
        var (sut, _) = NewSut();
        var outcome = await sut.UpsertCustomerAsync(
            new ErpnextPartyCustomerSource("CUST-BAD", "2026-05-16 22:00:00", CustomerName: "   "),
            Tenant(), Actor());

        var rejected = Assert.IsType<FoundationOutcome.Rejected>(outcome);
        Assert.True(outcome.IsRejected);
        Assert.Null(outcome.Action);
        Assert.Equal("CUST-BAD", rejected.Failure.ExternalRef);
        Assert.Equal("Customer", rejected.Failure.DocType);
        Assert.Equal(nameof(ImportRejectReason.MissingRequiredField), rejected.Failure.ReasonCode);
        Assert.Equal("customer_name", rejected.Failure.FieldName);
    }

    [Fact]
    public async Task AttachContact_OrphanNoLinks_Rejected_UnresolvedReference()
    {
        var (sut, _) = NewSut();
        var outcome = await sut.AttachContactAsync(
            new ErpnextContactSource("CONT-ORPHAN", "ghost@example.com", "+14155550000"),
            Tenant(), Actor());

        var rejected = Assert.IsType<FoundationOutcome.Rejected>(outcome);
        Assert.Equal("CONT-ORPHAN", rejected.Failure.ExternalRef);
        Assert.Equal(nameof(ImportRejectReason.UnresolvedReference), rejected.Failure.ReasonCode);
    }

    [Fact]
    public async Task AttachContact_LinkedToImportedCustomer_Attaches_Updated()
    {
        var (sut, repo) = NewSut();
        var actor = Actor();
        var cust = await sut.UpsertCustomerAsync(
            new ErpnextPartyCustomerSource("CUST-0010", "2026-05-16 22:00:00", "Acme LLC", "Company"),
            Tenant(), actor);
        var partyId = ((FoundationOutcome.Inserted)cust).Record.Id;

        var outcome = await sut.AttachContactAsync(
            new ErpnextContactSource("CONT-1", "contact@acme.example", "+14155559999",
                new[] { new ErpnextDynamicLink("Customer", "CUST-0010") }),
            Tenant(), actor);

        var updated = Assert.IsType<FoundationOutcome.Updated>(outcome);
        Assert.Equal(partyId, updated.Record.Id);
        var emails = await repo.GetActiveEmailsAsync(partyId);
        Assert.Contains(emails, e => e.Address == "contact@acme.example");
    }

    [Fact]
    public async Task AttachContact_LinkedButNoContactInfo_Skipped()
    {
        var (sut, _) = NewSut();
        var actor = Actor();
        await sut.UpsertCustomerAsync(
            new ErpnextPartyCustomerSource("CUST-0011", "2026-05-16 22:00:00", "Acme LLC", "Company"),
            Tenant(), actor);

        var outcome = await sut.AttachContactAsync(
            new ErpnextContactSource("CONT-2", null, null,
                new[] { new ErpnextDynamicLink("Customer", "CUST-0011") }),
            Tenant(), actor);

        Assert.IsType<FoundationOutcome.Skipped>(outcome);
    }

    [Fact]
    public async Task AttachAddress_LinkedToImportedSupplier_Appends_Updated()
    {
        var (sut, repo) = NewSut();
        var actor = Actor();
        var sup = await sut.UpsertSupplierAsync(
            new ErpnextPartySupplierSource("SUP-0010", "2026-05-16 22:00:00", "Bob's Plumbing", "Company"),
            Tenant(), actor);
        var partyId = ((FoundationOutcome.Inserted)sup).Record.Id;

        var outcome = await sut.AttachAddressAsync(
            new ErpnextAddressSource("ADDR-1", "100 Main St", "Springfield", "VA", "22150",
                "United States", null, IsPrimaryAddress: true,
                new[] { new ErpnextDynamicLink("Supplier", "SUP-0010") }),
            Tenant(), actor);

        Assert.IsType<FoundationOutcome.Updated>(outcome);
        var addresses = await repo.GetActiveAddressesAsync(partyId);
        Assert.Single(addresses);
        Assert.Equal("US", addresses[0].Address.Country);
        Assert.True(addresses[0].IsPrimary);
    }

    [Fact]
    public async Task AttachAddress_Orphan_Rejected_UnresolvedReference()
    {
        var (sut, _) = NewSut();
        var outcome = await sut.AttachAddressAsync(
            new ErpnextAddressSource("ADDR-ORPHAN", "1 Nowhere", "Nowhere", "ZZ", "00000",
                "United States", null, false, Array.Empty<ErpnextDynamicLink>()),
            Tenant(), Actor());

        var rejected = Assert.IsType<FoundationOutcome.Rejected>(outcome);
        Assert.Equal(nameof(ImportRejectReason.UnresolvedReference), rejected.Failure.ReasonCode);
    }

    [Fact]
    public async Task AttachAddress_UnmappableCountry_Rejected_InvalidFieldValue()
    {
        var (sut, _) = NewSut();
        var actor = Actor();
        await sut.UpsertCustomerAsync(
            new ErpnextPartyCustomerSource("CUST-0020", "2026-05-16 22:00:00", "Acme LLC", "Company"),
            Tenant(), actor);

        var outcome = await sut.AttachAddressAsync(
            new ErpnextAddressSource("ADDR-2", "5 High St", "Atlantis", "AT", "00001",
                "Atlantis", null, false, new[] { new ErpnextDynamicLink("Customer", "CUST-0020") }),
            Tenant(), actor);

        var rejected = Assert.IsType<FoundationOutcome.Rejected>(outcome);
        Assert.Equal(nameof(ImportRejectReason.InvalidFieldValue), rejected.Failure.ReasonCode);
        Assert.Equal("country", rejected.Failure.FieldName);
    }

    // ── Census conservation ───────────────────────────────────────────────

    [Fact]
    public async Task Census_AccountsForEveryRecord_Conserved()
    {
        var (sut, _) = NewSut();
        var actor = Actor();
        var census = new ImportCensus();

        // Source set of 5 customer records: 2 fresh inserts, 1 re-import (skip),
        // 1 newer (update of the first), 1 invalid (reject).
        var fresh1 = new ErpnextPartyCustomerSource("C1", "2026-05-16 22:00:00", "Alpha", "Company");
        var fresh2 = new ErpnextPartyCustomerSource("C2", "2026-05-16 22:00:00", "Beta", "Individual");
        var bad = new ErpnextPartyCustomerSource("C3", "2026-05-16 22:00:00", "  ");

        census.Record(await sut.UpsertCustomerAsync(fresh1, Tenant(), actor)); // Inserted
        census.Record(await sut.UpsertCustomerAsync(fresh2, Tenant(), actor)); // Inserted
        census.Record(await sut.UpsertCustomerAsync(fresh1, Tenant(), actor)); // Skipped
        census.Record(await sut.UpsertCustomerAsync(
            fresh1 with { Modified = "2026-05-17 09:00:00" }, Tenant(), actor));  // Updated
        census.Record(await sut.UpsertCustomerAsync(bad, Tenant(), actor));    // Rejected

        Assert.Equal(2, census.Inserted);
        Assert.Equal(1, census.Updated);
        Assert.Equal(1, census.Skipped);
        Assert.Equal(1, census.Rejected);
        Assert.Equal(5, census.Accounted);
        census.AssertConserved(5); // throws on a vanished/double-counted record
    }

    // ── PII non-emission (ADR 0098 S1 / ADR 0100 C9) ─────────────────────

    [Fact]
    public async Task NoPii_InFailureChannel_OnlyScalarIds()
    {
        var (sut, _) = NewSut();
        var actor = Actor();

        // Reject paths that involve PII-bearing source records. Assert the
        // ImportFailure carries ONLY the opaque ERPNext name + scalar metadata —
        // never an email / phone / display name / address-line value.
        const string piiEmail = "secret-person@private.example";
        const string piiPhone = "+14155557777";
        const string piiName = "Confidential Person Name";
        const string piiAddrLine = "742 Evergreen Terrace";

        var failures = new List<ImportFailure>();

        // Missing customer_name (PII display name absent → reject on the field).
        var r1 = await sut.UpsertCustomerAsync(
            new ErpnextPartyCustomerSource("C-PII-1", "2026-05-16 22:00:00", "   ",
                EmailId: piiEmail, MobileNo: piiPhone, TaxId: "TAXSECRET"),
            Tenant(), actor);
        failures.Add(((FoundationOutcome.Rejected)r1).Failure);

        // Orphan contact carrying PII email/phone.
        var r2 = await sut.AttachContactAsync(
            new ErpnextContactSource("C-PII-2", piiEmail, piiPhone), Tenant(), actor);
        failures.Add(((FoundationOutcome.Rejected)r2).Failure);

        // Orphan address carrying a PII street line.
        var r3 = await sut.AttachAddressAsync(
            new ErpnextAddressSource("C-PII-3", piiAddrLine, "Springfield", "VA", "22150",
                "United States", null, false, Array.Empty<ErpnextDynamicLink>()),
            Tenant(), actor);
        failures.Add(((FoundationOutcome.Rejected)r3).Failure);

        foreach (var f in failures)
        {
            // The whole serialized failure must not contain any PII value.
            var serialized = $"{f.ExternalRef}|{f.DocType}|{f.ReasonCode}|{f.FieldName}|{f.RuleViolated}";
            Assert.DoesNotContain(piiEmail, serialized);
            Assert.DoesNotContain(piiPhone, serialized);
            Assert.DoesNotContain(piiName, serialized);
            Assert.DoesNotContain(piiAddrLine, serialized);
            Assert.DoesNotContain("TAXSECRET", serialized);
            // The opaque ERPNext name IS allowed (it is a non-PII id).
            Assert.StartsWith("C-PII-", f.ExternalRef);
        }
    }

    [Fact]
    public async Task NoPii_OnConsoleOrTrace_DuringHappyAndRejectPaths()
    {
        var (sut, _) = NewSut();
        var actor = Actor();

        const string piiEmail = "trace-leak@private.example";
        const string piiPhone = "+14155558888";
        const string piiName = "Trace Leak Person";
        const string piiAddrLine = "1313 Mockingbird Lane";

        // Capture Console + Trace output across a representative run.
        var captured = new StringBuilder();
        using var writer = new StringWriter(captured);
        var listener = new TextWriterTraceListener(writer);
        var originalOut = Console.Out;
        Console.SetOut(writer);
        Trace.Listeners.Add(listener);
        try
        {
            // Happy path with PII.
            await sut.UpsertCustomerAsync(
                new ErpnextPartyCustomerSource("T-1", "2026-05-16 22:00:00", piiName, "Individual",
                    EmailId: piiEmail, MobileNo: piiPhone, TaxId: "SECRET-TAX"),
                Tenant(), actor);

            // Best-effort attach with a malformed phone (caught + swallowed path).
            await sut.AttachContactAsync(
                new ErpnextContactSource("T-2", piiEmail, "not-a-phone",
                    new[] { new ErpnextDynamicLink("Customer", "T-1") }),
                Tenant(), actor);

            // Address with a PII line (happy + reject variants).
            await sut.AttachAddressAsync(
                new ErpnextAddressSource("T-3", piiAddrLine, "Springfield", "VA", "22150",
                    "United States", null, false, new[] { new ErpnextDynamicLink("Customer", "T-1") }),
                Tenant(), actor);
            await sut.AttachAddressAsync(
                new ErpnextAddressSource("T-4", piiAddrLine, "Nowhere", "ZZ", "00000",
                    "Atlantis", null, false, new[] { new ErpnextDynamicLink("Customer", "T-1") }),
                Tenant(), actor);

            Trace.Flush();
        }
        finally
        {
            Console.SetOut(originalOut);
            Trace.Listeners.Remove(listener);
        }

        var log = captured.ToString();
        Assert.DoesNotContain(piiEmail, log);
        Assert.DoesNotContain(piiPhone, log);
        Assert.DoesNotContain(piiName, log);
        Assert.DoesNotContain(piiAddrLine, log);
        Assert.DoesNotContain("SECRET-TAX", log);
    }
}
