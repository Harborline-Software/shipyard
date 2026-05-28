# Sunfish.Foundation.Agreements

Cross-vertical **agreement substrate** — the common shape every binding
counterparty arrangement shares, regardless of vertical. Introduced by
[ADR 0098](../../docs/adrs/0098-block-naming-generalization.md) Step 1.

## What this is

A Tier-1 foundation package that ships **interfaces + one enum only** (Shape α):

| Type | Role |
|---|---|
| `IAgreement` | The agreement shape: id, parties, terms, status, lifecycle timestamps. Extends `IMustHaveTenant` (tenant-scoped by composition). |
| `IContractTerm` | A single term/clause/line-item (`TermId`, `TermType`, `Description`). |
| `IParty` | A counterparty (`PartyId`, `Role`, `DisplayName`). `DisplayName` carries PII discipline. |
| `AgreementStatus` | The canonical four-stage lifecycle: `Draft → PendingSignature → Active → Terminated`. |

## Why it exists (cross-vertical-reuse thesis)

Property leases, brand deals, and license agreements are all "two-or-more
parties bound by terms with a lifecycle." Rather than re-deriving that shape per
vertical, the substrate names it once at the layer **below** the vertical
blocks. Reporting, tenant-scoped querying, and lifecycle handling can then be
written against `IAgreement` across verticals.

Per ADR 0098 Halt 5, the package is named `foundation-agreements` (not
`foundation-contracts`) to avoid colliding with the TypeScript `@sunfish/contracts`
package and the `System.Diagnostics.Contracts` namespace.

## Canonical vertical-block implementation pattern

A vertical implements the interfaces on its own aggregate:

```csharp
public sealed class Lease : IAgreement
{
    public TenantId TenantId { get; init; }            // inherited contract from IMustHaveTenant
    public string AgreementId => LeaseId.Value;
    public IReadOnlyList<IParty> Parties { get; init; } = [];   // Parties[0] = lessor
    public IReadOnlyList<IContractTerm> Terms { get; init; } = [];
    public AgreementStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ActivatedAt { get; init; }
    public DateTimeOffset? TerminatedAt { get; init; }
}
```

`blocks-leases` adopts `IAgreement` as the cross-vertical-reuse exemplar
post-MVP (ADR 0098 Halt 8 Option α / Step 8).

## Shape α — drift discipline

- **Zero external `PackageReference`s.** One `ProjectReference` to
  `foundation-multitenancy` (for the `IMustHaveTenant` marker chain) — nothing else.
- **No DI helper at Step 1.** Consumers wire concrete implementations in their own
  composition roots. An `AddSunfishAgreements<TConcrete>` helper is *additive* in a
  later Step if a vertical adoption surfaces a concrete substrate-DI need.
- **`AgreementStatus` ordinals are pinned** (Draft = 0, …). Do not reorder; persisted
  ordinals must stay stable. Per-vertical lifecycle nuances map onto these four
  values as vertical sub-states; they do not extend the substrate enum.
- **`IParty.DisplayName` is PII** (ADR 0098 §S1). Vertical adopters apply audit-log
  redaction, a tier-redacted projection, and keep it out of info-level logs.
