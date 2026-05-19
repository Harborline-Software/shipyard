# Hand-off — `blocks-financial-ar` per-lease payment queries (RentRollV2 substrate-TODO closure)

**From:** Admiral (workstream-ledger authoring session)
**To:** Engineer
**Created:** 2026-05-18
**Status:** `ready-to-build` (gated — see §Gate conditions below)
**Workstream:** W#73 — blocks-financial-ar: per-lease payment queries (W#72 PR 6 substrate-TODO closure)
**Spec source:** [`icm/02_architecture/blocks-financial-schema-design.md`](../../02_architecture/blocks-financial-schema-design.md) §3.5 (`Invoice`), §3.9 (`Payment`), §3.10 (`PaymentApplication`)
**Triage source:** [`coordination/inbox/admiral-status-2026-05-17T23-30Z-w72-substrate-todos-triage.md`](../../../coordination/inbox/admiral-status-2026-05-17T23-30Z-w72-substrate-todos-triage.md)
**Pipeline:** `sunfish-feature-change` (additive query surface; no API break)
**Effort:** Sonnet `medium` (mechanical Stage 06; no novel substrate; reuses W#68 + blocks-financial-ar patterns)
**Estimated effort:** ~4-6h Engineer (2 PRs; ~15-20 tests + docs)
**PR count:** 2 PRs
**Pre-merge council:** NOT required (substrate-only query surface; no auth, no payment-application invariant change). Standard reviewer spot-check; promote to council only if the reviewer flags a halt.
**Attribution:** None. Pure additive query layer; no third-party pattern borrowed.

---

## Gate conditions

Verify all three before opening PR 1:

```bash
# 1. W#68 PR 1 merged — Payment entity must be on main
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-payments/Models/Payment.cs

# 2. blocks-financial-ar shipped — IInvoiceRepository must be on main
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-ar/Services/IInvoiceRepository.cs

# 3. W#72 PR 6 merged — RentRollCartridge.cs with cross-cluster TODO sites must exist
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Cartridges/RentRoll/RentRollCartridge.cs
grep -n "D4: TODO(cross-cluster)" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Cartridges/RentRoll/RentRollCartridge.cs
# Expected: 4+ lines naming D4: TODO(cross-cluster) at lines ~302-303, ~347-348
```

If any gate is unmet, **STOP** — file `engineer-question-*` naming the unmet gate. Do not stub a query against types that don't yet exist on main; the triage report's PR-#20-merge state is a snapshot, not a stable contract.

---

## Context

### Cluster gate position

```
W#68 blocks-financial-payments    (Payment + PaymentApplication)             gated
W#71 blocks-docs                  (Attachment substrate, 6 PRs)              gated
W#69 blocks-docs-core             (Document entity layer)                    gated
W#70 blocks-docs-wiki             (Wiki + Policy)                            gated
W#72 blocks-reports               (cartridge substrate + 5 cartridges; PR 6 = RentRollV2)  gated
W#73 blocks-financial-ar          ← THIS HAND-OFF  (per-lease query layer; closes PR 6 TODOs)
W#75 blocks-leases                (rent-schedule escalators — parallel, independent of this hand-off)
```

W#73 sits after W#72 PR 6 lands the `RentRollCartridge` with `LastPaymentDate: null` and `PrepaidBalance: 0m` stubbed and marked `// D4: TODO(cross-cluster)` at four call-sites (vacant-unit row lines 302-303; occupied-unit row lines 347-348). Closure of those four stub sites is the entire deliverable.

### Architectural seam — READ BEFORE OPENING PR 1

`blocks-financial-ar.Invoice` (canonical, per schema §3.5) does **NOT** carry a `LeaseId` field today. It carries:

```csharp
public required PartyId CustomerId { get; init; }
public string? PropertyId { get; init; }
```

The legacy `Sunfish.Blocks.RentCollection.Invoice` (a separate, pre-canonical type in `blocks-rent-collection`) DOES carry `LeaseId` — but that type is on a migration path (G14; see `RentCollection.xml` `TODO: migrate to Sunfish.Blocks.Leases.Models.LeaseId once G14 is on main`).

Per-lease payment query requires resolving the Invoice ↔ Lease join. Three options, ranked:

| Option | Description | Trade-off |
|---|---|---|
| **A (recommended)** | Add `LeaseId? Lease { get; init; }` to `blocks-financial-ar.Invoice` as a nullable additive extension | Simplest; mirrors `PropertyId? string` precedent; all existing invoices have `null` Lease (back-compat); future invoices set Lease on issue |
| B | Maintain external `IInvoiceLeaseLink` join map in `blocks-financial-ar/Services/` | Avoids touching Invoice; requires a write-path for the link; adds a read query layer; more code |
| C | Defer to legacy `blocks-rent-collection.Invoice.LeaseId`; query that side at runtime | Couples the canonical AR query surface to a legacy type with a pending G14 migration; will break when G14 lands |

**This hand-off recommends Option A.** It is mechanical, additive, and stays inside the canonical entity. PR 1 implements Option A unless the reviewer disagrees — in which case Engineer halts and files `engineer-question-*` (see §Halt conditions).

---

## What this hand-off ships

### PR 1 — Invoice ↔ Lease seam + `ILeasePaymentQueryService` substrate + in-memory impl

Package: `packages/blocks-financial-ar/`
Namespace root: `Sunfish.Blocks.FinancialAr`

**Models:**
- `Invoice.cs` — add `public LeaseId? Lease { get; init; }` (nullable; back-compat default `null`). Import `Sunfish.Blocks.Leases.Models.LeaseId` via ProjectReference (add to csproj if not already present).

**Services (new):**
- `ILeasePaymentQueryService.cs` — interface:
  ```csharp
  public interface ILeasePaymentQueryService
  {
      Task<DateOnly?> GetLastPaymentDateByLeaseAsync(LeaseId leaseId, CancellationToken ct = default);
      Task<decimal> GetPrepaidBalanceByLeaseAsync(LeaseId leaseId, CancellationToken ct = default);
  }
  ```
- `InMemoryLeasePaymentQueryService.cs` — implementation:
  - **GetLastPaymentDateByLeaseAsync:** query `IInvoiceRepository` for invoices where `Invoice.Lease == leaseId` → for each invoice, query `IPaymentApplicationRepository.ListByTargetAsync(invoice.Id, AppliedTo.Invoice, ct)` → for each application, resolve `Payment` via `IPaymentRepository.GetAsync(application.PaymentId, ct)` → return `max(Payment.PaymentDate)` across the set. Returns `null` if no payments.
  - **GetPrepaidBalanceByLeaseAsync:** sum `Payment.UnappliedAmount` over Payments where `Payment.PartyId` is in `Lease.Tenants` AND `Payment.Direction == PaymentDirection.Inbound` AND `Payment.UnappliedAmount > 0`. (PrepaidBalance is "customer money held but not yet applied to an invoice." It is not lease-direct; it is customer-direct, filtered to the lease's tenants.)

**DI extension:**
- Update `FinancialArServiceCollectionExtensions.AddSunfishFinancialAr()` to register `ILeasePaymentQueryService` → `InMemoryLeasePaymentQueryService` (scoped or singleton mirroring existing pattern — match what `IArAgingService` uses).

**csproj dependencies (verify; some may already be present):**
```xml
<ProjectReference Include="..\blocks-leases\Sunfish.Blocks.Leases.csproj" />
<ProjectReference Include="..\blocks-financial-payments\Sunfish.Blocks.FinancialPayments.csproj" />
```

**Tests (PR 1):** ≥8 unit tests in `packages/blocks-financial-ar/tests/LeasePaymentQueryServiceTests.cs`:
- Empty invoices + payments for lease → `GetLastPaymentDate` returns `null`; `GetPrepaidBalance` returns `0m`
- One invoice with one paid application → `GetLastPaymentDate` returns that Payment's date
- Two invoices, two paid applications on different dates → returns max date
- One partially-applied payment (Payment.UnappliedAmount > 0) → counts toward PrepaidBalance, AND the application's date counts toward LastPaymentDate
- Outbound payment with same PartyId → **ignored** by GetPrepaidBalance (direction filter is the integrity gate)
- Cross-tenant invariant: `Invoice.Lease == leaseId` from a different tenant must be filtered out (test must be run with two tenants in the repo)
- LeaseId with no invoices on-file → `GetLastPaymentDate` returns `null`; `GetPrepaidBalance` returns `0m`
- Lease with multiple tenants on `Lease.Tenants` → PrepaidBalance sums payments from ANY tenant party (not just the first)

**Acceptance criteria (PR 1):**
- All 8+ tests green
- Existing `blocks-financial-ar` test suite still 100% green (no regression in `IArAgingService` or `IInvoicePostingService` tests)
- `Invoice.cs` additive change documented in apps/docs (deferred to PR 2 — PR 1 ships the substrate only)
- `ILeasePaymentQueryService` resolves via DI in a fresh test fixture (DI registration test)

---

### PR 2 — `RentRollCartridge` wire-up + docs + ledger flip

**Code changes in `packages/blocks-reports/Cartridges/RentRoll/`:**
- Inject `ILeasePaymentQueryService` into `RentRollCartridge` constructor (alongside existing dependencies).
- Replace at line ~302-303 (vacant-unit row): `LastPaymentDate: null` → call site uses `null` correctly (vacant units have no lease; do not call the query service for vacant units — leave `null`); `PrepaidBalance: 0m` → same (no lease, no balance).
- Replace at line ~347-348 (occupied-unit row): `LastPaymentDate: null` → `await _leasePaymentQueryService.GetLastPaymentDateByLeaseAsync(currentLease.Id, ct)`; `PrepaidBalance: 0m` → `await _leasePaymentQueryService.GetPrepaidBalanceByLeaseAsync(currentLease.Id, ct)`.
- Remove all four `// D4: TODO(cross-cluster)` markers from those lines.
- Remove the D4 doc-comment block at lines 66-68 (the TODO doc-comment that names this exact workstream).
- Add csproj `ProjectReference` to `blocks-financial-ar` (verify; may already be present via transitive dependency).

**Docs:** `apps/docs/blocks/financial-ar/per-lease-payment-queries.md` — usage walkthrough (~30 lines):
- What `ILeasePaymentQueryService` does
- DI registration
- The Invoice ↔ Lease seam decision (Option A — nullable `Lease` on Invoice)
- Two example query call-sites
- Caveats: PrepaidBalance is customer-money-direction-filtered (Inbound only); LastPaymentDate is the max across all paid applications, not the most-recent invoice's payment

**Ledger flip:** update `active-workstreams.md` W#73 row → `built` with both PR numbers. Standard PR body with test count.

**Tests (PR 2):** ≥4 unit/integration tests in `packages/blocks-reports/tests/RentRollCartridgeTests.cs` (extend existing test file, do NOT create a new one):
- Occupied unit with one paid invoice → cartridge result row carries the payment's date and `0m` prepaid (no unapplied)
- Occupied unit with a prepaid customer (unapplied payment) → cartridge result row carries the prepaid amount
- Vacant unit → cartridge result row carries `null` LastPaymentDate + `0m` PrepaidBalance (unchanged from today's stubbed behavior; verify no regression)
- Cross-tenant isolation: cartridge for tenant A does not surface tenant B's payment dates

**Acceptance criteria (PR 2):**
- All 4+ new tests green; existing W#72 PR 6 tests still green
- No `// D4: TODO(cross-cluster)` markers remain in `RentRollCartridge.cs` (verify with `grep -n "D4: TODO(cross-cluster)" .../RentRollCartridge.cs` returning zero matches)
- D4 doc-comment block at lines 66-68 removed (the TODO doc-comment that named this workstream)
- W#73 row flipped to `built` in `active-workstreams.md`

---

## Package structure (post-PR-1)

```
packages/blocks-financial-ar/
├── Models/
│   ├── Invoice.cs                          (modified: + LeaseId? Lease)
│   └── ... (existing models unchanged)
├── Services/
│   ├── IInvoiceRepository.cs               (existing)
│   ├── IArAgingService.cs                  (existing)
│   ├── ILeasePaymentQueryService.cs        (NEW; PR 1)
│   ├── InMemoryLeasePaymentQueryService.cs (NEW; PR 1)
│   └── ... (existing services unchanged)
├── DependencyInjection/
│   └── FinancialArServiceCollectionExtensions.cs  (modified: + ILeasePaymentQueryService registration)
└── tests/
    ├── LeasePaymentQueryServiceTests.cs    (NEW; PR 1; ≥8 tests)
    └── ... (existing tests unchanged)
```

---

## Audit invariants

These MUST hold after every operation and are verified by the test suite:

1. **Tenant-keying invariant.** `ILeasePaymentQueryService.GetLastPaymentDateByLeaseAsync` and `GetPrepaidBalanceByLeaseAsync` MUST honor the calling tenant's scope — Lease lookups go through `Lease : IMustHaveTenant`. A cross-tenant LeaseId from another tenant returns `null` / `0m` (treated as not-found). Test must exercise this explicitly.
2. **Direction filter.** `GetPrepaidBalanceByLeaseAsync` MUST filter `Payment.Direction == PaymentDirection.Inbound`. Outbound payments to the lease's tenant parties (if any — unusual but possible in the data model) are AP-side and must NOT be summed into a prepaid AR balance.
3. **Unapplied-only.** `GetPrepaidBalanceByLeaseAsync` sums `Payment.UnappliedAmount`, not `Payment.Amount`. A payment that has been fully applied has `UnappliedAmount = 0` and contributes nothing.
4. **Back-compat.** Existing invoices without `Lease` set (Lease == null) are NOT returned by `GetLastPaymentDateByLeaseAsync` for any LeaseId; they remain queryable by the existing `IInvoiceRepository` surface unchanged.

---

## Standing pattern claim

**None.** This is mechanical Stage 06 work that follows existing `blocks-financial-ar` patterns:

- Tenant-keying via the existing `IMustHaveTenant` interface on Lease (already enforced; no new pattern).
- DI registration via `AddSunfishFinancialAr()` extension (existing pattern; just add one more service line).
- In-memory implementation pattern mirroring `InMemoryArAgingService` and `InMemoryInvoiceRepository` (existing patterns; no novel concurrency or transaction story).
- Two-overload constructor with both-or-neither audit emission is **NOT** required here — this service emits no audit events (read-only query surface; no state mutation). If a reviewer requests audit emission for compliance, halt and file question — that would be scope expansion.

No new standing patterns are introduced. No ADR is required. No NOTICE entry is required.

---

## Halt conditions

Stop and file `engineer-question-*` if any of these arise:

1. **Gate not met** — any of the three gate-condition `ls` / `grep` checks fail. Do not proceed; do not stub against non-existent types.
2. **Reviewer disagrees with Option A** — if a reviewer pushes back on adding `LeaseId? Lease` to `blocks-financial-ar.Invoice` (preferring Option B external join map, or Option C legacy-type deference), halt PR 1 and file question. The Admiral triage explicitly named §3.5 as "no `LeaseId`" — Option A is the additive recommendation, but it is a substrate change worth confirming.
3. **G14 migration collision** — if you discover that the legacy `blocks-rent-collection` G14 migration is mid-flight (touching `LeaseId` types) at the same time as this work, halt PR 1. Concurrent type changes on the same conceptual seam will cause merge pain; coordinate with whoever owns G14 before proceeding.
4. **`Invoice.Lease == leaseId` query is unexpectedly expensive** — if `IInvoiceRepository` lacks an efficient "filter by Lease" query and the in-memory implementation must do a full scan, that is acceptable for v1 (in-memory; small data sets). But if PR 1 reveals that EFCore-backed `IInvoiceRepository` on origin/main can't push the predicate down without a new index, halt PR 1 — file question and ask whether to add the index in this workstream or defer to a separate index-management workstream.
5. **Direction-filter ambiguity** — if you encounter test data where an Inbound payment has been made by a non-lease-tenant Party (e.g. a third-party benefactor paying a tenant's rent), the PrepaidBalance filter `Payment.PartyId IN Lease.Tenants` will exclude it. That is the intended semantic for v1, but flag in PR 1 description so reviewers know the limit. Do not halt for this — just document.

---

## PR commit message templates

```
feat(blocks-financial-ar): PR 1 — Invoice.Lease seam + ILeasePaymentQueryService + in-memory impl (W#73)
feat(blocks-reports): PR 2 — RentRollCartridge wires per-lease payment queries; D4 TODOs closed (W#73)
```

---

## Effort + model selection (per `effort-policy.md`)

This is mechanical Stage 06 work from a complete spec: per `effort-policy.md` row "Implementation from a clear, complete plan" → **Sonnet 4.6 `medium`**. The Admiral triage already did the architectural reasoning; Engineer's job is to mirror the existing `IArAgingService` pattern and wire up two methods. Reach for Opus 4.7 + `high` only if the seam-resolution decision (Option A vs B vs C) becomes contentious during review.
