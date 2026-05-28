using System;
using System.Collections.Generic;
using Sunfish.Foundation.Agreements;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Agreements.Tests;

/// <summary>
/// Interface-shape + contract verification for the foundation-agreements
/// substrate (ADR 0098 Step 1). Per the ADR 0095 R2 / 0096 R2 Step 1 test
/// precedent these are compilation + contract assertions (the substrate ships
/// no concrete implementation + no DI), NOT resolution-validation.
/// </summary>
public sealed class AgreementContractTests
{
    // A minimal fixture implementing the substrate interfaces. The fact it
    // COMPILES is itself the primary contract assertion (e.g. IAgreement's
    // inherited TenantId must be satisfiable).
    private sealed class FakeParty : IParty
    {
        public string PartyId { get; init; } = "party-1";
        public string Role { get; init; } = "lessor";
        public string DisplayName { get; init; } = "Acme Holdings LLC";
    }

    private sealed class FakeTerm : IContractTerm
    {
        public string TermId { get; init; } = "term-1";
        public string TermType { get; init; } = "rent-schedule";
        public string Description { get; init; } = "$1,200/mo due on the 1st";
    }

    private sealed class FakeAgreement : IAgreement
    {
        public TenantId TenantId { get; init; } = new(Guid.NewGuid().ToString());
        public string AgreementId { get; init; } = "agreement-1";
        public IReadOnlyList<IParty> Parties { get; init; } = new IParty[] { new FakeParty() };
        public IReadOnlyList<IContractTerm> Terms { get; init; } = new IContractTerm[] { new FakeTerm() };
        public AgreementStatus Status { get; init; } = AgreementStatus.Draft;
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? ActivatedAt { get; init; }
        public DateTimeOffset? TerminatedAt { get; init; }
    }

    [Fact]
    public void IAgreement_extends_IMustHaveTenant_marker_chain()
    {
        // A1: tenant-scoping by composition, not redeclaration.
        Assert.True(typeof(IMustHaveTenant).IsAssignableFrom(typeof(IAgreement)));
        Assert.True(typeof(ITenantScoped).IsAssignableFrom(typeof(IAgreement)));

        IMustHaveTenant asMarker = new FakeAgreement();
        Assert.NotEqual(default, asMarker.TenantId);
    }

    [Fact]
    public void Parties_is_readonly_list_with_positional_access()
    {
        // A7: IReadOnlyList<IParty> — positional access is part of the contract.
        IAgreement agreement = new FakeAgreement();
        Assert.IsAssignableFrom<IReadOnlyList<IParty>>(agreement.Parties);
        Assert.Equal("lessor", agreement.Parties[0].Role); // Parties[0] = primary counterparty
    }

    [Fact]
    public void Terms_is_readonly_list_with_positional_access()
    {
        // A7: IReadOnlyList<IContractTerm> — positional access is part of the contract.
        IAgreement agreement = new FakeAgreement();
        Assert.IsAssignableFrom<IReadOnlyList<IContractTerm>>(agreement.Terms);
        Assert.Equal("rent-schedule", agreement.Terms[0].TermType);
    }

    [Fact]
    public void AgreementStatus_ordinals_are_pinned()
    {
        // Persisted ordinals must stay stable across releases — do not reorder.
        Assert.Equal(0, (int)AgreementStatus.Draft);
        Assert.Equal(1, (int)AgreementStatus.PendingSignature);
        Assert.Equal(2, (int)AgreementStatus.Active);
        Assert.Equal(3, (int)AgreementStatus.Terminated);
    }

    [Fact]
    public void FakeAgreement_satisfies_full_contract()
    {
        IAgreement agreement = new FakeAgreement
        {
            Status = AgreementStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow,
        };

        Assert.Equal("agreement-1", agreement.AgreementId);
        Assert.Equal(AgreementStatus.Active, agreement.Status);
        Assert.NotNull(agreement.ActivatedAt);
        Assert.Null(agreement.TerminatedAt);
        Assert.Single(agreement.Parties);
        Assert.Single(agreement.Terms);
    }
}
