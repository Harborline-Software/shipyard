# Hand-off — `blocks-leases` rent-schedule escalators + ProjectedNextMonthRent

**From:** Admiral (workstream-ledger authoring session)
**To:** Engineer (PRs 1+2 substrate) + FED (PR 3 UI rebind)
**Created:** 2026-05-18
**Status:** `ready-to-build` — no gate; independent of the financial-payments + docs cluster
**Workstream:** W#74 — blocks-leases: rent-schedule escalators (W#72 PR 6 v2-projection closure)
**Spec source:** [`packages/blocks-leases/Models/Lease.cs`](../../../packages/blocks-leases/Models/Lease.cs) (today: flat `MonthlyRent decimal` only) + [`coordination/inbox/admiral-status-2026-05-17T23-30Z-w72-substrate-todos-triage.md`](../../../coordination/inbox/admiral-status-2026-05-17T23-30Z-w72-substrate-todos-triage.md) §2 (justification: `ProjectedNextMonthRent` is a Lease-extension, not a cross-cluster join)
**Pipeline:** `sunfish-feature-change` (additive entity extension; no API break; back-compat via default-empty schedule)
**Effort:** Sonnet `medium` (mechanical Stage 06; no novel substrate; reuses existing `Lease : IMustHaveTenant` pattern)
**Estimated effort:** ~6-9h (PRs 1+2 ~4-5h Engineer; PR 3 ~2-4h FED)
**PR count:** 3 PRs (substrate ×2 + UI ×1)
**Pre-merge council:** NOT required. Substrate-only with no auth, no payment, no audit-emission, no cross-cluster invariant. Standard reviewer spot-check on PR 1 (entity extension); promote to council only if a reviewer flags a halt.
**Attribution:** None. Pure additive value-object collection on Lease + pure-function projection; no third-party pattern borrowed.

---

## Gate conditions

**No external gate.** This workstream is independent of:
- W#68 blocks-financial-payments (financial cluster)
- W#71 / W#69 / W#70 (docs cluster)
- W#72 blocks-reports (cartridge cluster)
- W#73 blocks-financial-ar per-lease payment queries (the sibling RentRollV2 substrate-TODO closure)

The only soft sequencing concern: W#72 PR 6 (RentRollCartridge) lands the `ProjectedNextMonthRent: current.MonthlyRent` v2-simplification at line 346 of `RentRollCartridge.cs`. PR 2 of THIS workstream replaces that line with an `IRentProjectionService` call. If W#72 PR 6 has not yet merged when you reach PR 2 of W#74, halt PR 2 (not PR 1) and file `engineer-question-*`; W#72 PR 6 is a sibling workstream's deliverable, not a dependency for the substrate work.

PR 1 (entity + value object) can ship today. PR 2 (service + cartridge wire-up) waits for W#72 PR 6 on main. PR 3 (FED UI) can ship in parallel with PR 2 or after.

Verify before opening PR 1:

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-leases/Models/Lease.cs
# Expected: file exists with flat `MonthlyRent decimal` field (today's state)

grep -n "IMustHaveTenant" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-leases/Models/Lease.cs
# Expected: `public sealed record Lease : IMustHaveTenant` (already wired 2026-05-17 per W#74-Cohort-1)
```

---

## Context

### Rationale (non-trivial)

Today's `Lease` record is intentionally thin (cf. doc-comment in `Models/Lease.cs`: "Intentionally thin for the first pass; full workflow surface (signature, execution, renewal, termination) is deferred to follow-up work"). Adding a rent-escalator schedule is the **first non-trivial workflow extension** to the Lease entity beyond the W#27 multi-party retrofit.

The Admiral triage at `admiral-status-2026-05-17T23-30Z-w72-substrate-todos-triage.md` correctly classifies `ProjectedNextMonthRent` as a `blocks-leases` extension, NOT a cross-cluster join — the projection is a pure function over Lease state. Putting it elsewhere (e.g. in `blocks-financial-ar` or in `RentRollCartridge` itself) would mis-locate the concept and require a cross-cluster read to do the math.

The escalator schedule is a value-object collection where each entry carries:
- An effective `DateOnly` (when the new rate takes effect)
- A new `decimal` MonthlyRent (the rate after that date)
- An optional reason code (`RentEscalatorReason`: Annual, CPI, Renewal, MarketAdjustment, Other)
- An optional `string? Notes`

`ProjectedRentForMonth(YearMonth)` is a pure function over the schedule — no service call, no DB hit, no audit event. Tenant-keying is enforced transitively by `Lease : IMustHaveTenant` (already wired); escalators inherit the parent Lease's tenant scope.

### What this hand-off ships

```
W#74 PR 1   blocks-leases   RentEscalator value object + Lease.RentSchedule additive field + ProjectedRentForMonth pure-fn
W#74 PR 2   blocks-leases   IRentProjectionService + InMemoryRentProjectionService + DI + RentRollCartridge wire-up
W#74 PR 3   blocks-leases   LeaseRentScheduleBlock.razor (FED) + leases-list UI rebind to surface next escalator
```

---

## PR 1 — `RentEscalator` value object + `Lease.RentSchedule` additive field + pure-function projection

**Owner:** Engineer (sunfish-PM)

Package: `packages/blocks-leases/`
Namespace root: `Sunfish.Blocks.Leases`

**New types in `Models/`:**

- `RentEscalator.cs` — value object record:
  ```csharp
  public sealed record RentEscalator
  {
      public required DateOnly EffectiveDate { get; init; }
      public required decimal MonthlyRent { get; init; }
      public required RentEscalatorReason Reason { get; init; }
      public string? Notes { get; init; }
  }
  ```
- `RentEscalatorReason.cs` — enum:
  ```csharp
  public enum RentEscalatorReason
  {
      Annual = 0,
      Cpi = 1,
      Renewal = 2,
      MarketAdjustment = 3,
      Other = 4,
  }
  ```

**Modified `Models/Lease.cs`:**

Add a new field after `MonthlyRent`:

```csharp
/// <summary>
/// Rent-escalator schedule. Empty by default (flat-rent leases). Entries are evaluated by
/// <see cref="LeaseRentProjection.ProjectedRentForMonth"/> to compute the effective rate for a target month.
/// </summary>
/// <remarks>
/// Additive extension 2026-05-18 (W#74). Existing Lease records without escalators continue to return
/// <see cref="MonthlyRent"/> from the projection function (back-compat). Each escalator inherits this
/// Lease's <see cref="TenantId"/> transitively; no per-escalator tenant column required.
/// </remarks>
public IReadOnlyList<RentEscalator> RentSchedule { get; init; } = Array.Empty<RentEscalator>();
```

**New `Models/LeaseRentProjection.cs` (static helper):**

```csharp
public static class LeaseRentProjection
{
    /// <summary>
    /// Returns the effective monthly rent for the given target month.
    /// Selects the most-recent <see cref="RentEscalator.EffectiveDate"/> that is on or before the first
    /// day of the target month. If no escalator applies, returns <see cref="Lease.MonthlyRent"/> (the base).
    /// Pure function; no I/O.
    /// </summary>
    public static decimal ProjectedRentForMonth(this Lease lease, DateOnly targetMonthFirstDay)
    {
        // Tie-break: identical EffectiveDate values are sorted by Reason ordinal (deterministic).
        var applicable = lease.RentSchedule
            .Where(e => e.EffectiveDate <= targetMonthFirstDay)
            .OrderByDescending(e => e.EffectiveDate)
            .ThenByDescending(e => (int)e.Reason)
            .FirstOrDefault();
        return applicable?.MonthlyRent ?? lease.MonthlyRent;
    }
}
```

**EFCore configuration (if applicable):**
- Check whether `Sunfish.Blocks.Leases` has an EFCore configuration file (`LeaseConfiguration.cs` or similar). If yes, configure `RentSchedule` as an owned collection (`OwnsMany`). If no (in-memory only on main), skip this — note in PR description that EFCore wiring is deferred to whenever Leases persistence ships.

**Tests (PR 1):** ≥10 unit tests in `packages/blocks-leases/tests/LeaseRentProjectionTests.cs` (NEW):
- Empty schedule + base `MonthlyRent` → `ProjectedRentForMonth(any month)` returns base
- One escalator (effective 2026-01-01) + query month 2025-12-01 → returns base (escalator not yet effective)
- One escalator (effective 2026-01-01) + query month 2026-01-01 → returns escalator's MonthlyRent
- One escalator (effective 2026-01-01) + query month 2026-06-01 → returns escalator's MonthlyRent
- Two escalators (2026-01-01 + 2026-07-01) + query month 2026-06-01 → returns first escalator's rate
- Two escalators (2026-01-01 + 2026-07-01) + query month 2026-08-01 → returns second escalator's rate
- Same EffectiveDate twice with different Reasons → tie-break is deterministic (latest Reason ordinal wins; document)
- Lease with non-default `TenantId` + escalator collection → projection function does NOT require a TenantContext (pure function; no tenant lookup)
- Lease record-equality: two Leases with the same RentSchedule are equal (test sealed-record value-equality across IReadOnlyList<RentEscalator>; if the default record equality doesn't structurally compare the list, document and add a custom equality member — but prefer the default)
- Back-compat: a fresh `Lease` constructed without specifying `RentSchedule` defaults to `Array.Empty<RentEscalator>()` and `ProjectedRentForMonth` returns base `MonthlyRent`

**Acceptance criteria (PR 1):**
- All 10+ new tests green
- Existing `blocks-leases` test suite still 100% green (no regression in lease-creation, party-roles, document-version tests)
- `LeaseAuditPayloadFactory` is NOT modified (no audit emission for schedule edits in PR 1; that comes in PR 2 if needed)
- `Lease.cs` doc-comment updated to remove "intentionally thin" hint about deferred follow-up work for rent terms (note that escalators are now first-class; remaining deferred items: signature workflow, execution, renewal, termination)

---

## PR 2 — `IRentProjectionService` + DI + `RentRollCartridge` wire-up

**Owner:** Engineer (sunfish-PM)

**New services in `packages/blocks-leases/Services/`:**

- `IRentProjectionService.cs`:
  ```csharp
  public interface IRentProjectionService
  {
      /// <summary>
      /// Projects the effective monthly rent for the given lease and target month.
      /// Returns null if the lease is not found (Engineer treats this same as today's stubbed behavior).
      /// </summary>
      Task<decimal?> ProjectAsync(LeaseId leaseId, DateOnly targetMonthFirstDay, CancellationToken ct = default);
  }
  ```
- `InMemoryRentProjectionService.cs` — implementation:
  - Resolves Lease via the existing `ILeaseService.GetAsync(LeaseId, ct)` or whichever repository surface is on main.
  - Calls `lease.ProjectedRentForMonth(targetMonthFirstDay)` (the PR 1 pure function).
  - Returns `null` if the lease lookup returns null. Otherwise returns the projected decimal.
  - Honors tenant scope transitively: `ILeaseService` is tenant-keyed; cross-tenant LeaseId returns null.

**DI extension:**
- Update `LeasesServiceCollectionExtensions.AddSunfishLeases()` (or whatever the existing extension is named) to register `IRentProjectionService` → `InMemoryRentProjectionService`.

**Wire-up in `packages/blocks-reports/Cartridges/RentRoll/RentRollCartridge.cs`:**
- Inject `IRentProjectionService` into `RentRollCartridge` constructor.
- Replace line ~346: `ProjectedNextMonthRent: current.MonthlyRent` → `ProjectedNextMonthRent: await rentProjectionService.ProjectAsync(currentLease.Id, nextMonthFirstDay, ct) ?? current.MonthlyRent`. (Fallback to `current.MonthlyRent` if projection returns null, matching today's v2-simplification semantics for the not-found edge case.)
- Remove the `// D4: v2 projects current rent unchanged` comment from line 346 (this workstream closes that simplification).
- Add csproj `ProjectReference` to `blocks-leases` if not already present (likely already present via existing W#72 PR 6 wiring).

**Docs:** `apps/docs/blocks/leases/rent-schedule-escalators.md` (NEW; ~40 lines):
- What `RentEscalator` is + the `RentEscalatorReason` enum
- How to add escalators to a Lease (PR 1 substrate)
- How `IRentProjectionService` consumes the schedule
- The pure-function `ProjectedRentForMonth` semantics + tie-break rule
- One end-to-end example: Lease with $1500 base + 2026-07-01 CPI escalator to $1575 + projection for August 2026 → $1575
- Back-compat note: leases without RentSchedule continue to return `MonthlyRent`

**Tests (PR 2):** ≥6 tests:
- 3 unit tests on `IRentProjectionService` in `packages/blocks-leases/tests/RentProjectionServiceTests.cs` (NEW):
  - Found Lease + no escalators → returns base MonthlyRent
  - Found Lease + applicable escalator → returns escalator rate
  - Unknown LeaseId → returns null
- 3 integration tests on RentRollCartridge in `packages/blocks-reports/tests/RentRollCartridgeTests.cs` (extend existing file):
  - Occupied unit with a flat-rent Lease → cartridge row carries `MonthlyRent` for `ProjectedNextMonthRent`
  - Occupied unit with a Lease that has a future escalator (effective next month) → cartridge row carries the escalator rate
  - Occupied unit with a Lease that has a past escalator → cartridge row carries the post-escalation rate

**Acceptance criteria (PR 2):**
- All 6+ new tests green
- W#72 PR 6 existing RentRollCartridge tests still green (no regression)
- The `// D4: v2 projects current rent unchanged` comment removed from line 346 (verify with `grep -n "v2 projects current rent" .../RentRollCartridge.cs` returning zero matches)
- W#74 PRs 1+2 ledger row stays `building` (PR 3 closes the workstream)

---

## PR 3 — UI surface (FED-owned)

**Owner:** FED (front-end developer; substrate-only Engineer work ends at PR 2)

**New Razor component:** `packages/blocks-leases/LeaseRentScheduleBlock.razor`
- Read-side: list current escalators for a lease (chronological); show "applies starting YYYY-MM-DD" + "amount" + "reason" per row
- Write-side: add an escalator (date picker + rent input + reason dropdown + optional notes)
- Remove an escalator (delete row, confirmation prompt)
- Surfaces the effective rate for "next month" prominently above the schedule (using `LeaseRentProjection.ProjectedRentForMonth(nextMonthFirstDay)` from PR 1)

**Rebind existing leases-list UI** to optionally surface "next change on YYYY-MM-DD" copy when a future escalator exists. Locate this in `packages/blocks-leases/` or wherever the leases-list view lives on main; do not duplicate logic — call into `LeaseRentProjection`.

**Tests (PR 3):** Component tests in `packages/blocks-leases/tests/LeaseRentScheduleBlockTests.cs` (NEW; ≥4 tests):
- Empty schedule renders "no scheduled escalators"
- Adding an escalator updates the displayed list (round-trip through component state)
- The "next month rate" panel updates correctly when an escalator is added
- Removing an escalator works (state mutation + re-render)

**Acceptance criteria (PR 3):**
- All 4+ new component tests green
- Visual review: a sample lease with three escalators renders cleanly at 1280×800 and 375×667 (sunfish standard breakpoints)
- The leases-list UI rebind does not regress the existing W#27 retrofit (LeaseHolderRole display unchanged)
- W#74 row flipped to `built` in `active-workstreams.md` (FED owns the ledger flip on PR 3 merge)

---

## Package structure (post-PR-3)

```
packages/blocks-leases/
├── Models/
│   ├── Lease.cs                            (modified: + RentSchedule field; doc-comment updated)
│   ├── RentEscalator.cs                    (NEW; PR 1)
│   ├── RentEscalatorReason.cs              (NEW; PR 1; enum)
│   ├── LeaseRentProjection.cs              (NEW; PR 1; static extension)
│   └── ... (existing models unchanged)
├── Services/
│   ├── IRentProjectionService.cs           (NEW; PR 2)
│   ├── InMemoryRentProjectionService.cs    (NEW; PR 2)
│   └── ... (existing services unchanged)
├── DependencyInjection/
│   └── LeasesServiceCollectionExtensions.cs  (modified PR 2: + IRentProjectionService registration)
├── LeaseRentScheduleBlock.razor            (NEW; PR 3)
└── tests/
    ├── LeaseRentProjectionTests.cs         (NEW; PR 1; ≥10 tests)
    ├── RentProjectionServiceTests.cs       (NEW; PR 2; ≥3 tests)
    ├── LeaseRentScheduleBlockTests.cs      (NEW; PR 3; ≥4 tests)
    └── ... (existing tests unchanged)
```

---

## Audit invariants

These MUST hold and are verified by the test suite:

1. **Tenant-keying invariant.** `RentEscalator` does NOT carry a `TenantId` field; it inherits from its parent `Lease : IMustHaveTenant`. `IRentProjectionService.ProjectAsync` honors tenant scope via the underlying `ILeaseService` lookup (cross-tenant LeaseId returns null).
2. **Back-compat invariant.** Existing `Lease` records created before PR 1 default to `RentSchedule = Array.Empty<RentEscalator>()` and `ProjectedRentForMonth(any)` returns `MonthlyRent`. All existing W#27 + cohort-1 lease tests must continue to pass with zero changes.
3. **Pure-function invariant.** `LeaseRentProjection.ProjectedRentForMonth` performs no I/O, emits no audit events, allocates no async state. It is a `static` extension method over the record's value-typed list. This is the simplest possible computation; reviewers should reject any complication.
4. **Deterministic tie-break.** Two escalators with the same `EffectiveDate` resolve by `Reason` ordinal (descending; latest Reason value wins). This is a documented arbitrary choice; it exists to make the projection function deterministic in the face of pathological data.

---

## Standing pattern claim

**None.** This is mechanical Stage 06 work that follows existing `blocks-leases` patterns:

- Tenant-keying via the existing `IMustHaveTenant` interface on Lease (already enforced 2026-05-17 per W#74-Cohort-1; no new pattern).
- Value-object collection on a record (mirrors `Lease.PartyRoles`, `Lease.DocumentVersions`, `Lease.PartySignatures` — all `IReadOnlyList<T>` with `Array.Empty<T>()` defaults).
- Pure-function extension method on the entity record (no existing precedent in `blocks-leases`, but this is too lightweight to warrant a new pattern claim).
- In-memory service implementation pattern mirroring `InMemoryLeaseService` (existing pattern; no novel concurrency).
- DI registration via the existing `AddSunfishLeases()` extension.

No new standing patterns are introduced. No ADR is required. No NOTICE entry is required. No audit-emission story is added (read-only projection; mutation of `RentSchedule` itself goes through the existing Lease update path and inherits the existing Lease audit emission if any).

---

## Halt conditions

Stop and file `engineer-question-*` if any of these arise:

1. **Lease record-equality breaks** — if adding `IReadOnlyList<RentEscalator> RentSchedule` to the sealed record causes the default value-equality to misbehave (e.g. two Leases with structurally-equal escalator lists comparing unequal because the list references differ), halt PR 1. The fix is a custom equality member, but document the trade-off before committing. Test #9 in the PR 1 suite explicitly exercises this.
2. **EFCore-backed Lease persistence on main has a migration in flight** — if you discover a Leases persistence migration mid-flight on origin/main when PR 1 opens, halt and file question. Concurrent owned-collection migrations on the same entity are merge-painful; coordinate before proceeding.
3. **`Lease.cs` doc-comment "intentionally thin" wording is load-bearing elsewhere** — if any other test, doc, or ADR cites the "intentionally thin" wording (e.g. a council finding referencing "follow-up workflow surface"), halt PR 1's doc-comment update. The wording-update is small but cross-references matter.
4. **PR 2 wire-up site has moved** — if W#72 PR 6 has merged but `RentRollCartridge.cs` line 346 is no longer the `ProjectedNextMonthRent: current.MonthlyRent` site (post-merge refactor moved it elsewhere), search the file for `// D4: v2 projects current rent` and update the marker location. If that comment is also gone, halt PR 2 and file question — the cartridge has been refactored in a way the triage didn't anticipate.
5. **FED PR 3 reveals that the existing leases-list UI doesn't import a Lease service** — if FED discovers that the leases-list view today renders a static `MonthlyRent` field without a service injection point (no `IRentProjectionService` consumer wiring possible), halt PR 3 and file question. The "next change on" UI surface may need a new service-injection seam before it can render dynamically.

---

## PR commit message templates

```
feat(blocks-leases): PR 1 — RentEscalator value object + Lease.RentSchedule + ProjectedRentForMonth pure fn (W#74)
feat(blocks-leases): PR 2 — IRentProjectionService + RentRollCartridge wire-up; D4 v2-simplification closed (W#74)
feat(blocks-leases): PR 3 — LeaseRentScheduleBlock.razor + leases-list UI rebind (W#74; FED)
```

---

## Effort + model selection (per `effort-policy.md`)

This is mechanical Stage 06 work from a complete spec: per `effort-policy.md` row "Implementation from a clear, complete plan" → **Sonnet 4.6 `medium`**. The Admiral triage already did the architectural reasoning (rent-escalator schedule as a Lease extension, not a cross-cluster join); Engineer's job on PRs 1+2 is to mirror the existing `Lease.PartyRoles` collection pattern and add a pure-function projection. FED's PR 3 is mostly Razor template work over existing patterns.

Reach for Opus 4.7 + `high` only if PR 1's record-equality halt-condition surfaces and the equality fix requires non-trivial reasoning about value-typed collections in sealed records.
