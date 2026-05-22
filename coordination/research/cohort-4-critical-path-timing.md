# Cohort-4 critical-path timing analysis

**Authored by:** ONR (V13 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-22T19-05Z` item V13 #3)
**Authored at:** 2026-05-22T19-50Z

---

## Summary

Cohort-4 (audit-trail viewer) critical path analyzed at **5 steps** in the
production sequence. With current state (shipyard#100 MERGED Step 1-5 in-
memory; sunfish#58 + sunfish#59 + sunfish#61 OPEN), the remaining critical
path is **~4-7 calendar days** serial, **~3-4 calendar days** parallelizable.

---

## 1. Cohort-4 production sequence (per V9 #1 + V13 #4)

```
Step 1: shipyard#100 IAuditEventReader Step 1-5 (kernel-audit package)
        STATUS: MERGED 2026-05-21T13:51Z ✓
        │
        ▼
Step 2: signal-bridge audit-events Bridge endpoint family
        (consumer of #100)
        STATUS: NOT YET OPEN
        ESTIMATED: ~1-2 days Engineer time
        │
        ▼
Step 3: sunfish#58 audit-events.ts type stubs
        STATUS: OPEN; can merge anytime
        │
        ▼
Step 4: FED PR 1 cohort-4 live-rebind half (per shipyard#119 spec)
        STATUS: NOT YET OPEN
        ESTIMATED: ~2-3 days FED time (18 RTL tests; A1+A2 close)
        │
        ▼
Step 5: sunfish#59 auto-merge fires
        STATUS: OPEN; DRAFT-ahead; auto-merge DISARMED until pair gates clear
```

---

## 2. Per-step timing breakdown

### 2.1 Step 1 — shipyard#100 (DONE)

**Calendar:** complete (MERGED 2026-05-21T13:51Z).

### 2.2 Step 2 — signal-bridge audit-events endpoint family

**Engineer effort:** ~1-2 days
**Effort breakdown:**
- AuditEventsEndpoint.cs handler (List + GetById + reserve CSV/Export route)
- AuditEventsRequestDtos.cs + ResponseDtos.cs
- Cursor signing at Bridge layer (per V11 #2 §5.4 + V12 #3 + V13 #2)
- DI wiring (consume IAuditEventReader; register opaque-cursor signer)
- 10-12 integration tests per V9 #1 §5.3

**Council SPOT-CHECK SLA:** 30 min dispatch + 30-60 min verdict = ~1.5h gate
**Potential AMBER fold:** ~1 day if sec-eng AMBER on cursor signing OR cross-tenant
test

**Calendar:** ~2-3 days end-to-end (authoring + SPOT-CHECK + potential fold)

### 2.3 Step 3 — sunfish#58 type stubs

**FED effort:** trivial (already authored; awaiting merge)
**Gate:** depends only on `audit-events.ts` types being aligned with Step 2's
wire contract. Currently inline types — needs Step 2 confirmation before merge
**Calendar:** ~30 min review + merge after Step 2 contract frozen

### 2.4 Step 4 — FED PR 1 cohort-4 live-rebind

**FED effort:** ~2-3 days
**Effort breakdown** (per V9 #1 §3):
- useAuditEvents TanStack hook + cursor pagination + filter wiring
- A1 close: TenantBoundaryViolation 5-field structured render + defense-in-depth tenant-assertion
- A2 close: severity coloring + severity filter dropdown + filter button removal
- 7 frontend-architect nits: Badge replacement (1), htmlFor labels (4), Link instead of button (7)
- 18 RTL tests per V9 #1 §3.6-§3.7
- PR description acceptance criteria checklist

**Council SPOT-CHECK SLA:** dual (sec-eng + frontend-architect); 30 min each
dispatch + 30-60 min verdicts = ~2-3h gate

**Potential AMBER fold:**
- ~1-2 days if sec-eng finds new AMBER on A1 implementation
- ~1 day if frontend-architect AMBER on Badge migration

**Calendar:** ~3-5 days end-to-end (authoring + dual SPOT-CHECK + potential fold)

### 2.5 Step 5 — sunfish#59 auto-merge

**Calendar:** ~30 min (auto-merge fires automatically once gates clear)
**Gate:** Engineer Step 2 MERGED + FED Step 4 MERGED + sunfish#58 MERGED

---

## 3. Serial vs parallel timing

### 3.1 Serial (worst case)

```
Step 1 (DONE)                          [0 days remaining]
Step 2 (Engineer ~2-3 days)             [+2-3 days]
Step 3 (FED ~30 min review/merge)       [+0.05 days]
Step 4 (FED ~3-5 days)                  [+3-5 days]
Step 5 (auto-merge fires)               [+0.05 days]

Total serial: ~5-8 calendar days remaining
```

### 3.2 Parallel (best case)

Identify opportunities:

**Parallel opportunity 1: FED can start Step 4 against Engineer's DRAFT Step 2**

While Engineer is authoring Step 2 (~2-3 days), FED can begin Step 4 authoring
against:
- Engineer's typescript-spec branch (if Engineer authors `audit-events.ts` type
  stubs first as part of Step 2 prep)
- V9 #1 §3 shipyard#119 spec (already canonical reference for FED PR 1)

FED Step 4 authoring overlaps with Step 2 authoring. Gate stays at Step 5
auto-merge — neither can land before Step 2 MERGED.

**Parallel opportunity 2: sunfish#58 merges in parallel with Step 2 final shape**

If Step 2 cursor signing reveals type changes needed in sunfish#58, sunfish#58
can stay OPEN through Step 2 + adopt final shape in a single fast-follow commit
when Step 2's wire contract finalizes.

**Parallel opportunity 3: shipyard#100 → Step 2 substrate readiness**

shipyard#100 already gives Engineer everything needed to author Step 2. No
gating wait.

### 3.3 Parallel-best timing

```
Step 2 + Step 4 parallel-authored:
  ├── Engineer Step 2 authoring: ~2-3 days
  └── FED Step 4 authoring (against V9 #1 spec): ~3-5 days (overlaps)

Critical path = max(Step 2, Step 4) = Step 4 = ~3-5 days
+ Step 5 auto-merge = +0.05 days
Total parallel: ~3-5 calendar days remaining
```

---

## 4. Risk hotspots

### 4.1 Council SPOT-CHECK SLA discipline

**Risk:** SLA slippage. Per V5 #8 baseline: ~30 min median dispatch; outlier 8h
(cohort-1 PR 3 incident).

If sec-eng dispatch slips >2h on Step 2 OR Step 4, FED's Step 4 work is at risk
of context-rot waiting for verdict. QM daemon (per fleet-conventions §SPOT-CHECK
dispatch SLA) provides backstop after 1h.

**Mitigation:** ONR forward-watches SLA on Step 2 + Step 4 Ready-flips; escalates
to Admiral if dispatch >2h.

### 4.2 AMBER amendment cycles

**Risk:** Sec-eng AMBER on cursor signing (Step 2) OR A1 client tenant-assertion
(Step 4) introduces ~1-2 day fold cycle each.

Per V7 #5 Stage-05 retro hypothesis: Adversarial Brief catches ~70-80% pre-fold.
Cohort-4 IS the first Stage-05 pilot (per cohort-4 hand-off §2 Adversarial
Brief; per V7 #5 scaffolding). Forward-watch: how many AMBER folds emerge in
cohort-4 vs baseline cohorts 1/2/3?

**Mitigation:** Engineer + FED both have shipyard#119 (V9 #1) + shipyard#127
(V11 #2) + shipyard#131 (V12 #3) + cohort-4 hand-off (shipyard#81) — extensive
pre-PR spec. Reduces AMBER probability vs greenfield.

### 4.3 Cursor signing layer choice ambiguity

**Risk:** Step 2 Engineer chooses substrate-layer cursor signing (V10 #1
original spec) instead of Bridge-layer (V11 #2 + V12 #3 + V13 #2 recommendation).
Forces re-design + refactoring.

**Mitigation:** V11 #2 + V12 #3 + V13 #2 all clearly state Bridge-layer; ADR
0094 §"Recursion safety" supports. Engineer pre-flight check.

### 4.4 sunfish#58 type drift

**Risk:** Step 2 wire contract changes mid-flight; sunfish#58 inline types
diverge.

**Mitigation:** Step 2 wire shape pinned by shipyard#119 (V9 #1 §3.2 + §3.3) +
V9 #1 §5 Bridge endpoint dependency. Engineer authors to spec; FED tracks.

### 4.5 18 RTL tests authoring time

**Risk:** FED Step 4 tests take longer than 2-3 days estimate (specifically the
A1 cross-tenant TBV render test + cursor mid-page-tenant-switch test).

**Mitigation:** V9 #1 §3.6 test enumeration provides explicit test list; FED
doesn't iterate on test design.

### 4.6 sunfish#59 auto-merge timing

**Risk:** Auto-merge fires before all 4 prerequisite PRs merge (race condition
on gates).

**Mitigation:** Pair-gate already disarmed per FED status beacon (per V9 #1 §1.5
+ council verdict 2026-05-22T1445Z). FED + Admiral coordinate explicit re-arm
when Step 2+4+58 all MERGED.

---

## 5. Parallelization recommendations (for Admiral routing)

### 5.1 Recommendation 1: Encourage Step 2 + Step 4 parallel-author

**Action:** Admiral dispatches Engineer V4 #1 (Step 2) AND FED V26 #1 (Step 4)
in same session. Both authoring against shipyard#119 / shipyard#127 / shipyard#131
canonical specs.

**Saving:** ~2-3 days vs serial.

### 5.2 Recommendation 2: Engineer + FED coordination beacon

**Action:** Engineer files `engineer-status-*-step-2-wire-contract-frozen.md`
when Step 2 wire-contract decisions finalize (cursor signing, error codes, query
param names). FED ingests for Step 4 type alignment.

**Saving:** prevents Step 4 → Step 2 retroactive refactoring (~0.5-1 day).

### 5.3 Recommendation 3: SPOT-CHECK SLA monitoring

**Action:** ONR + QM daemon double-watch Step 2 + Step 4 Ready-flips for
council dispatch SLA conformance. Escalate to Admiral on >2h dispatch.

**Saving:** prevents 1+ day session-rot if dispatch stalls.

### 5.4 Recommendation 4: Step 6 follow-on (EventLogBacked) does NOT block Step 5

Per V13 #2 finding: Step 6 (EventLogBackedAuditEventReader) is NOT gating
cohort-4 demo. In-memory implementation acceptable for demo.

**Action:** Admiral does NOT dispatch Step 6 work until cohort-4 demo ships.
Step 6 → V14+ batch.

---

## 6. Cohort-4 demo readiness

**Critical-path to demo-ready cohort-4:** ~3-5 calendar days remaining
(parallel-best case).

Post-Step-5 (sunfish#59 MERGED), cohort-4 audit-trail viewer is demo-ready:
- `/audit-trail` route lists audit events (mock data swapped for live via
  useAuditEvents)
- `/audit-trail/:auditId` detail page renders structured TBV payloads
- Severity coloring distinguishes Security.* events
- Cursor pagination works end-to-end with tenant-bound opaque cursor

**Post-demo:** ONR V7 #5 Stage-05 retro fires (first pilot retrospective).

---

## 7. Pattern emergence forward-watches at cohort-4 close

Per V11 #1 + V12 #2 forward-watches:

When cohort-4 close-out (Step 5 MERGED):
- **pattern-009-cohort-4-audit-pair** (candidate per sunfish#59 verdict) —
  3rd qualifying instance of pattern-009? Promotion trigger?
- **pattern-tenant-id-signed-opaque-cursor** (V10 #1 candidate) — 1st instance
  via Step 2 cursor signing
- **pattern-defense-in-depth-tenant-assert-client-side** (V9 #1 emergence) —
  1st instance via Step 4 A1 client guard
- **pattern-severity-event-prefix-coloring** (V9 #1 emergence) — 1st instance
  via Step 4 A2 severity coloring
- **pattern-canonical-audit-payload-shape** (V10 #2 emergence) — 2nd instance
  via shipyard#100 (also 1st instance evidence in cohort-2 financial substrate)

---

## 8. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **Parallel authoring authorization** — Admiral dispatches Engineer V4 #1
   AND FED V26 #1 in same session for ~2-3 day saving? ONR recommends YES.
2. **Step 6 timing** — Admiral defers Step 6 (EventLogBacked) to V14+ post-
   cohort-4-demo? ONR recommends YES per V13 #2 finding.
3. **SPOT-CHECK SLA monitoring** — ONR + QM daemon double-watch? ONR recommends
   YES; provides 1h safety net.
4. **Stage-05 retro authoring timing** — ONR fires retro authoring at Step 5
   MERGED (cohort-4 close-out) OR at sunfish#59 auto-merge fires? ONR
   recommends at sunfish#59 auto-merge fires (last cohort artifact lands).

---

## 9. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T19-05Z` item V13 #3
2. V9 #1 cohort-4 FED PR-by-PR specs (shipyard#119) — 18 RTL tests + A1/A2 close
3. shipyard#100 (kernel-audit Step 1-5; MERGED 2026-05-21)
4. V11 #2 ADR 0094 consultation (shipyard#127) — cursor signing layer
5. V12 #3 Engineer V3 #1 supplement (shipyard#131) — cursor signing layer
6. V13 #2 ADR 0094 Step 2+ scoping (shipyard#135) — Step 6 not gating
7. V13 #4 Engineer V3 #1 progress tracking (shipyard#133) — shipyard#100 finding
8. cohort-4 Stage-06 hand-off (shipyard `icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md`)
9. council-verdict-2026-05-22T1445Z (sec-eng AMBER A1+A2) +
   council-verdict-2026-05-22T1213Z (frontend-architect GREEN+nits)
10. ADR 0093 (Stage-05 Adversarial Review Protocol; first pilot)
11. V7 #5 Stage-05 retro scaffolding (shipyard#110)
12. fleet-conventions §SPOT-CHECK dispatch SLA

---

## 10. What ONR does next

V13 #3 timing analysis complete. V13 #5 (cohort-10 baseline metrics) remains
conditional on QM V5 #3 landing — NOT MET per V13 dispatch check. ONR files
V13 complete idle beacon.

— ONR, 2026-05-22T19:50Z
