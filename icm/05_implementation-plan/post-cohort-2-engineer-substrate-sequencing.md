# Post-Cohort-2 Engineer Substrate Sequencing Plan

**Authored by:** ONR (V3 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-21T12-45Z` item #3)
**Authored at:** 2026-05-21T12-52Z
**Status:** draft (Engineer ratifies sequence at Stage-06 kickoff)

---

## Scope

V2 batch shipped three pre-research deliverables that inform Engineer's post-cohort-2 substrate work:

1. **ADR 0091 Steps 3+4 pre-research** (shipyard#68; 441 lines) — test fixture migration + facade `[Obsolete]` + `RequestContextMixingAnalyzer`
2. **ADR 0092 Step 2 EFCore query-filter** (shipyard#69; 321 lines) — `HasQueryFilter` convention + optional `.WhereTenant(...)` extension
3. **Audit-emission Bridge retrofit** (shipyard#71; 403 lines) — `BridgeAuditEmitter` helper + 3 handler families

Engineer needs a unified sequencing plan with cross-phase dependencies, sizing, prerequisites, and deliverables.

---

## TL;DR — Recommended sequence

| Order | Phase | Effort | Why this position |
|---|---|---|---|
| **1** | Audit-emission Bridge retrofit (V2 #3) | ~1-2h | Smallest scope; immediately unblocks forensics; no other phase depends on it; quickest substrate-hardening win |
| **2** | ADR 0091 Step 2.0 (DbContext rewrite — already pre-researched in V1 #3) | ~3-4h | Foundational for Steps 3+4 and ADR 0092 Step 2; ships SunfishBridgeDbContext narrowing + A3/A4 guards |
| **3** | ADR 0092 Step 2 EFCore (per cluster; batched) | ~2-3h × 4 clusters = ~8-12h total | Per-cluster shipment; financial blocks first (cohort-2 PR 0 cluster aligned); each PR is small + sec-eng-attestable |
| **4** | ADR 0091 Step 3 (test fixture migration) | ~12-24h across 6-8 PRs | Mechanical; per-package; cleanup AFTER Step 2.0 + Step 2.1+ land |
| **5** | ADR 0091 Step 4 (facade `[Obsolete]` + `RequestContextMixingAnalyzer`) | ~6-8h | Single PR; ratifies the consumption sweep |
| **6** | ADR 0091 Step 5 (facade deletion) | ~1-2h | One-cohort grace from Step 4; mechanical |

**Total Engineer effort:** ~31-52h across the substrate work. Spread across ~3-5 weeks at realistic per-day capacity given other workstream interleaving.

---

## 1. Cross-phase dependency map

```
Audit retrofit (V2 #3)
       │ (no upstream dep)
       ▼
   ships first — forensics live

ADR 0091 Step 2.0 (DbContext rewrite)
       │ (depends on Step 1 = SHIPPED at PR#44)
       ▼
   ships next — substrate ready

ADR 0092 Step 2 (per cluster)         ADR 0091 Step 3 (test fixtures)
       │ (depends on Step 2.0)               │ (depends on Step 2.0 + 2.1+)
       ▼                                      ▼
   ships per cluster                  ships AFTER per-cluster
   (financial first; then              endpoint migrations narrow
   blocks-leases; blocks-               from facade to narrowed
   maintenance; etc.)                  interface
       │                                      │
       ▼                                      ▼
       │                                      │
       └──────────────┬───────────────────────┘
                      │
                      ▼
              ADR 0091 Step 4
              (facade [Obsolete]
              + RequestContextMixingAnalyzer)
                      │
                      ▼
              ADR 0091 Step 5
              (facade deletion)
              [one-cohort grace]
```

---

## 2. Phase-by-phase sizing + prerequisites

### Phase 1 — Audit-emission Bridge retrofit (V2 #3)

**Prerequisites:**
- Tracking beacon `admiral-tracking-2026-05-21T08-00Z` (filed)
- V2 #3 research (shipyard#71)
- IAuditTrail + IOperationSigner already shipped (ADR 0049)

**Deliverables:**
- New `signal-bridge/Sunfish.Bridge/Authorization/BridgeAuditEmitter.cs`
- DI registration in Program.cs
- Per-handler integration in FinancialEndpoints + LeasesEndpoints + WorkOrdersEndpoint
- ~10-14 integration tests
- New `AuditEventType.TenantBoundaryViolation` constant (or verify exists; per V2 #3 §6 sec-eng Q3)

**Pre-merge council:** sec-eng SPOT-CHECK MANDATORY (per tracking beacon).

**Why first:** smallest scope (~1-2h); no dependencies; immediate compliance/forensics value; consumed by cohort-4 anchor (V2 #6 recommendation C3 audit-trail viewer).

### Phase 2 — ADR 0091 Step 2.0 (DbContext rewrite; already pre-researched at V1 #3)

**Prerequisites:**
- Step 1 SHIPPED at PR#44 (foundation-authorization package + sum-interface facade)
- V1 #3 research at shipyard#56

**Deliverables:**
- `SunfishBridgeDbContext` constructor narrowed to `Foundation.MultiTenancy.ITenantContext`
- A3 fail-closed guard (`tenant.Tenant == null` throws)
- A4 sentinel/null/`"__system__"` rejection
- `_capturedTenantId` typed field; readonly
- `ApplyTenantQueryFilters` updated to use typed `.Value` comparison
- Legacy filter updates (Project + TaskItem + AuditRecord)
- Separate `MigrationDbContext` for migration runner (no tenant filters; no tenant capture)
- ≥6 tests (A3, A4 sentinel/null/literal, A5 populated-DB regression)

**Pre-merge council:** sec-eng SPOT-CHECK MANDATORY (per ADR 0091 R2 amendments A3/A4/A5).

**Why second:** ADR 0092 Step 2 + ADR 0091 Steps 3+4 all consume the narrowed constructor surface; Step 2.0 is the gate.

### Phase 3 — ADR 0092 Step 2 EFCore (per-cluster batched)

**Prerequisites:**
- Step 2.0 (Phase 2) merged
- V2 #2 research at shipyard#69

**Deliverables (per cluster):**
- Cluster `DbContext` (if separate from `SunfishBridgeDbContext`) gains `HasQueryFilter` on each `IMustHaveTenant` entity
- OR cluster's per-entity model config gains the filter (depends on cluster's persistence shape)
- ≥3 tests per cluster: per-tenant filter applies; cross-tenant query returns empty; sentinel/null rows excluded
- `grep -rn 'WithoutQueryFilters\|IgnoreQueryFilters' <cluster-path>` output in PR description (per ADR 0092 amendment C8)
- Per amendment A4: any `.WithoutQueryFilters()` callsite during pre-Step-4b window requires inline sec-eng `council-verdict-*` beacon

**Per-cluster sizing:**
- blocks-financial-ar / -ap / -payments / -ledger — ~2-3h each (4 PRs; can ship in parallel per fleet PR-cap allowance)
- blocks-leases — ~2-3h (depends on substrate readiness)
- blocks-maintenance — ~2-3h
- blocks-public-listings + blocks-messaging — ~2-3h each
- Total: ~16-24h across ~8 PRs

**Pre-merge council:** sec-eng SPOT-CHECK per cluster (pattern-009-tenant-keying-retrofit candidate during Step 2 ratification window).

**Optional companion:** `.WhereTenant(...)` extension method (per V2 #2 §3) ships as SEPARATE foundation-persistence PR BEFORE the per-cluster Step 2 PRs (~1h Engineer); analyzer (Step 4a) can then recognize it as a satisfying mechanism.

**Why third:** unblocks Step 3 test migration; consumed by audit-trail viewer page (Phase 4) for tenant-filtered audit queries.

### Phase 4 — ADR 0091 Step 3 (test fixture migration)

**Prerequisites:**
- Step 2.0 (Phase 2) + Step 2.1+ endpoint migrations + Step 2 per-cluster (Phase 3) all on main

**Deliverables (per package):**
- `DemoTenantContext` test fixture updates to track production-consumer shape (narrowed interface usage)
- Test double types updated where production code narrowed
- ~30-60 test fixture updates across ~12 consumer packages
- Per package: 1-2 PRs; ~1-2h each

**Pre-merge council:** advisory only (test code; not security-relevant surface).

**Per-package sizing:** ~1-2h × 12 packages = ~12-24h; can batch by package proximity.

**Why fourth:** mechanical cleanup AFTER endpoint migrations narrow to specific interfaces; can ship in parallel with Phase 5 once Step 2.x is fully landed.

### Phase 5 — ADR 0091 Step 4 (`[Obsolete]` + analyzer)

**Prerequisites:**
- Step 3 (Phase 4) test fixtures updated (so consumers compile against narrowed interfaces; obsolete warnings don't flood CI on unmigrated consumers)
- V2 #1 research at shipyard#68 (this hand-off's substrate)

**Deliverables:**
- `[Obsolete(...)]` attribute on `Foundation.Authorization.ITenantContext` (facade)
- `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` Roslyn analyzer
  - Diagnostic ID: `SUNFISH_AUTH_001`
  - Severity: Error (per V2 #1 §6 sec-eng Q1; pending council confirm)
  - Detection: DI registration sites + ctor params + method params + field declarations
- ≥6 analyzer tests
- Separate `Sunfish.Foundation.Authorization.Analyzers.csproj` (per foundation-wayfinder-analyzers precedent)
- Optional companion: `CrossClusterMixingAssertion` IHostedService (per V2 #1 §5 runtime verification)

**Pre-merge council:** .NET-architect SPOT-CHECK + sec-eng SPOT-CHECK (analyzer is the load-bearing security primitive for the MUST-NOT-mix invariant).

**Sizing:** ~6-8h single PR.

**Why fifth:** ratifies the consumption-sweep work; makes the no-mix invariant compile-time enforceable.

### Phase 6 — ADR 0091 Step 5 (facade deletion)

**Prerequisites:**
- Step 4 (Phase 5) on main + one-cohort grace period elapsed
- All facade consumers migrated to narrowed interfaces

**Deliverables:**
- Delete `Foundation.Authorization.ITenantContext` (the facade)
- Update all remaining consumers that didn't migrate (should be zero by this point)
- Optional: delete the `[Obsolete]` attribute from any test fixtures that reference the facade

**Pre-merge council:** advisory only (mechanical deletion; the analyzer + obsolete-warning forcing-function already pushed consumers to migrate).

**Sizing:** ~1-2h.

**Why sixth:** deletion is the final ratchet; cannot land until all consumers migrate.

---

## 3. Parallelization opportunities

Engineer's PR-cap raise (per directive 2026-05-19T12-40Z) allows up to 5 substrate-cluster PRs in parallel after pattern-009-tenant-keying-retrofit ratifies. Applies here:

- **Phase 1 + Phase 2 — sequential.** Phase 1 (audit retrofit) is independent of Step 2.0; can ship in parallel with Phase 2 prep but the audit-retrofit + DbContext-rewrite are different file surfaces, so parallel PRs OK.
- **Phase 3 — fully parallelizable across clusters.** 4 financial clusters first (cohort-2 PR 0 cluster relationship); then non-financial clusters; up to 5 concurrent PRs.
- **Phase 4 + Phase 5 — sequential within Step 3 → Step 4 → Step 5.** But Phase 4 (test migration) can batch packages in parallel PRs.

**Maximum-parallelism shape:**

```
Week 1: Phase 1 (1 PR) + Phase 2 prep (research consumed)
Week 2: Phase 2 (1 PR) → Phase 3 begins
Week 3-4: Phase 3 × 4-5 PRs in parallel (financial clusters)
Week 5-6: Phase 3 × 3-4 PRs in parallel (non-financial clusters)
Week 6-7: Phase 4 × 3 PRs in parallel (test migration batches)
Week 7-8: Phase 5 (1 PR)
Week 12+ (after grace): Phase 6 (1 PR)
```

**~8 weeks for the entire substrate ladder; ~12 weeks with the post-Step-4 one-cohort grace.**

---

## 4. Engineer PR-by-PR deliverable list

This is the prioritized PR list Engineer can pull from sequentially:

| # | PR title (proposed) | Phase | Effort | Council |
|---|---|---|---|---|
| 1 | `feat(signal-bridge): BridgeAuditEmitter + 3-handler audit-emission retrofit` | 1 | 1-2h | sec-eng SPOT-CHECK |
| 2 | `feat(signal-bridge): SunfishBridgeDbContext narrow to Foundation.MultiTenancy + A3/A4/A5 guards (ADR 0091 Step 2.0)` | 2 | 3-4h | sec-eng SPOT-CHECK |
| 3 | `feat(foundation-persistence): WhereTenant extension method (optional companion)` | 3a | 1h | optional |
| 4 | `feat(blocks-financial-ar): HasQueryFilter on Invoice (ADR 0092 Step 2)` | 3 | 2h | sec-eng SPOT-CHECK |
| 5-7 | `feat(blocks-financial-{ap,payments,ledger}): HasQueryFilter (ADR 0092 Step 2)` × 3 | 3 | 2h each | sec-eng SPOT-CHECK each |
| 8-11 | `feat(blocks-{leases,maintenance,public-listings,messaging}): HasQueryFilter` × 4 | 3 | 2h each | sec-eng SPOT-CHECK each |
| 12-19 | `chore(packages/{...}/tests): migrate test fixtures to narrowed ITenantContext (ADR 0091 Step 3)` × 8 | 4 | 1-2h each | advisory |
| 20 | `feat(foundation-authorization): facade [Obsolete] + RequestContextMixingAnalyzer (ADR 0091 Step 4)` | 5 | 6-8h | .NET-architect + sec-eng SPOT-CHECK |
| 21 | `chore(foundation-authorization): delete facade (ADR 0091 Step 5)` | 6 | 1-2h | advisory |

**Total PR count:** ~21 PRs. **Total Engineer effort:** ~40-60h (within margins on the per-phase estimates).

---

## 5. Risks + mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Step 2.0 ships before audit retrofit; cross-tenant probes go silent for longer | Medium | Medium | Sequence audit retrofit BEFORE Step 2.0 (per Phase 1 first; addresses) |
| Step 3 test migration triggers `[Obsolete]` warnings on test fixtures that compile-but-warn | High | Low | Acceptable; signals progress; Phase 5 ships the Obsolete attribute alongside Step 3 completion |
| Per-cluster Step 2 PR fails sec-eng SPOT-CHECK on first cluster (financial-ar) | Medium | Medium | Engineer iterates per cluster; pattern lock-in after first GREEN |
| `WhereTenant` extension method is gold-plated; Engineer skips it | Medium | Low | Optional companion; Phase 3 ships without if Engineer prefers HasQueryFilter-only |
| Migration DbContext separation requires architectural rework | Medium | High (scope expansion) | Pre-research at V1 #3 surfaces this; Engineer plans Phase 2 with separate DbContext as line item |
| Engineer parallelism exceeds the PR-cap raise (5 concurrent) | Low | Low | Phase 3 + Phase 4 fit within cap; Phase 6 deletion is single PR |

---

## 6. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #3
2. V1 #3 ADR 0091 Step 2.0 research (shipyard#56) — already-merged substrate
3. V2 #1 ADR 0091 Steps 3+4 research (shipyard#68)
4. V2 #2 ADR 0092 Step 2 EFCore research (shipyard#69)
5. V2 #3 audit-emission Bridge retrofit (shipyard#71)
6. ADR 0091 R2 (Accepted) — phases canonical reference
7. ADR 0092 (Accepted) — Step 2 EFCore + amendments A4 + C8
8. `admiral-tracking-2026-05-21T08-00Z` — audit retrofit tracking beacon

---

— ONR, 2026-05-21T12:52Z
