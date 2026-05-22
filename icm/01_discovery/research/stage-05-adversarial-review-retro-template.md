# Stage-05 Adversarial Review post-pilot retrospective scaffolding

**Authored by:** ONR (V7 batch item #5)
**Requester:** Admiral (per `admiral-directive-2026-05-22T12-45Z` item #5)
**Authored at:** 2026-05-22T13-10Z

---

## Purpose

ADR 0093 (Stage-05 Adversarial Review Protocol Amendment) is LIVE (MERGED at shipyard#104 on 2026-05-21T15:48Z). **Cohort-4 audit-trail viewer is the first canonical pilot** (per V3 #1 hand-off; shipyard#81 MERGED with the first R3 Adversarial Brief instance).

This document scaffolds the retrospective ONR will author when cohort-4 ships:
- Metrics templates
- Pre-Stage-05 baseline vs post-Stage-05 comparison framework
- Retrospective doc template

---

## 1. Pre-Stage-05 baseline (cohort-1 + cohort-2 + cohort-3)

Baseline metrics from cohort-1, cohort-2, cohort-3 BEFORE ADR 0093 protocol enforcement:

### 1.1 Council dispatch metrics (per V5 #8 SPOT-CHECK SLA analysis)

| Cohort | PRs dispatched to sec-eng | Median dispatch latency | Outliers |
|---|---|---|---|
| Cohort-1 | ~4 | ~30 min | PR 3 ~8h (write-path complexity) |
| Cohort-2 | ~7 (4 substrate + 3 frontend) | ~30-60 min | PR 3 ~3h |
| Cohort-3 | (in flight; not yet shipped) | TBD | TBD |

### 1.2 Council verdict density (items per verdict)

| Cohort | Median items per sec-eng verdict | Outliers |
|---|---|---|
| Cohort-1 | ~5 | PR 3 ~12 items (write-path scrutiny) |
| Cohort-2 | ~6 | substrate PR 0c ~10 items |
| Cohort-3 | TBD | TBD |

### 1.3 AMBER-fold-cycle count (per PR; pre-Stage-05)

| Cohort | PRs with AMBER → fold-cycle iteration | Iteration count median |
|---|---|---|
| Cohort-1 | PR 3 (Maintenance) | 1 fold (amendment then GREEN attest) |
| Cohort-2 | PR 0a/b/c/d (substrate; AMBER each on first review then GREEN attest) | 1 fold each |
| ADR 0091 R1 → R2 | both councils AMBER → fold → GREEN re-attest | 1 major fold cycle |
| ADR 0092 R1 → R2 | both councils AMBER → fold → GREEN re-attest | 1 major fold cycle |

**Baseline fold-rate:** ~1 fold per PR with AMBER (median).

### 1.4 Forward-watched items per cohort

| Cohort | Forward-watched items per cohort | Cohort-N+ consumption rate |
|---|---|---|
| Cohort-1 | ~5 (cohort-2 picked up 3+; cohort-3 picked up 1) | High |
| Cohort-2 | ~8 (cohort-3 picked up 2; cohort-4 picked up 3; persistence hand-off picked up 2) | Medium |
| Cohort-3 | TBD | TBD |

---

## 2. Post-Stage-05 expected improvements (cohort-4+)

The R3 Adversarial Brief protocol catches issues at Stage-05 (hand-off authoring) rather than at Stage-06 sec-eng SPOT-CHECK. Expected effects:

### 2.1 Reduced AMBER-fold-cycle count

**Hypothesis:** Adversarial Brief surfaces ~70-80% of issues sec-eng would catch at SPOT-CHECK; AMBER folds reduce from ~1 per PR to ~0.2-0.3 per PR.

**Measurement:** count AMBER verdicts on cohort-4 + cohort-5 + cohort-6 PRs; compare to baseline.

### 2.2 Reduced council verdict density

**Hypothesis:** Pre-surfaced concerns get addressed before sec-eng sees the PR; verdict density drops from ~5-6 items per verdict to ~2-3.

**Measurement:** items-per-verdict count on cohort-4+ vs baseline.

### 2.3 Faster Stage-06 implementation

**Hypothesis:** Engineer + FED have fewer AMBER iteration cycles; PRs ship faster (no "rebase + amend after sec-eng verdict" delay).

**Measurement:** Stage-06 PR open-to-merge wall-clock time; cohort-4+ vs cohort-2 baseline.

### 2.4 Increased forward-watched-per-Stage-05 ratio

**Hypothesis:** Adversarial Brief surfaces forward-watched items proactively that pre-protocol cohort-N hand-offs missed (e.g., cohort-2 hand-off didn't enumerate cross-tenant attack vectors as explicitly as cohort-4 hand-off did via the 8-bullet brief).

**Measurement:** forward-watched count in Stage-05 hand-off; cohort-4+ vs cohort-3 baseline.

---

## 3. Retrospective doc template

After cohort-4 ships (PASS gate met), ONR authors retrospective at `shipyard/icm/07_review/stage-05-adversarial-review-cohort-4-retrospective.md`:

```markdown
# Stage-05 Adversarial Review retrospective — Cohort-4 pilot

**Authored by:** ONR
**Authored at:** <date when cohort-4 ships>
**Pilot cohort:** Cohort-4 audit-trail viewer (W#78)

---

## 1. Pilot summary

- Cohort-4 Stage-05 hand-off authored YYYY-MM-DD (shipyard#81)
- Adversarial Brief: 8 bullets (per V3 #4 prototype)
- Cohort-4 close-out (W#78 ledger flip): YYYY-MM-DD

## 2. Metrics vs baseline

| Metric | Baseline (cohort-1/2/3 median) | Cohort-4 actual | Delta |
|---|---|---|---|
| AMBER folds per PR | 1.0 | <X> | <%> |
| Items per sec-eng verdict | 5-6 | <X> | <%> |
| Open-to-merge wall-clock | <baseline> | <X> | <%> |
| Forward-watched per hand-off | <baseline> | 11+ | <%> |
| Stage-05 catch rate (% of sec-eng concerns pre-surfaced) | 0% | <X%> | <%> |
| Dispatch latency (per V5 #8 SLA) | 30-60 min median | <X> | <%> |

## 3. Wins

- (List 3-5 wins observed; e.g., "Adversarial Brief Decision 1 caught cross-tenant scope query parameter — sec-eng SPOT-CHECK would have caught it but at 30-min dispatch latency; Stage-05 surfaced it at hand-off authoring time")
- (Specific examples of issues caught at Stage-05 that would have AMBER'd at Stage-06)

## 4. Misses

- (List items that AMBER'd at Stage-06 despite Adversarial Brief)
- (Identify gap in the brief: was it scope-creep beyond 8 bullets? was the bullet shape wrong? was the worst-case framing too narrow?)

## 5. Process improvements

- (Adjustments to V3 #4 prototype template)
- (Suggested expansion to brief: e.g., "8 bullets minimum; allow up to 12 for write-path PRs")
- (Pre-flight checklist additions)

## 6. Recommendations for cohort-5+ adoption

- (List structural changes to Stage-05 protocol)
- (Defaults for cohort-5+ Adversarial Briefs)
- (Test-eng-council dispatch criteria refinements)

## 7. Sec-eng + .NET-architect council feedback

- (Quoted feedback from cohort-4 sec-eng SPOT-CHECK verdict)
- (Council confirmation: did the Adversarial Brief reduce review burden?)

---

— ONR, <date>
```

---

## 4. Comparison framework (pre-Stage-05 vs post-Stage-05)

| Dimension | Pre-Stage-05 baseline (cohort-1/2/3) | Post-Stage-05 expected (cohort-4+) |
|---|---|---|
| Where issues surface | Stage-06 SPOT-CHECK (after Engineer/FED implementation) | Stage-05 hand-off authoring (before implementation) |
| Cost per AMBER | Engineer/FED rebase + amend (~1-2h iteration cycle) | Hand-off revision (~30-45 min ONR iteration) |
| Sec-eng cognitive load | Higher (5-6 items per verdict; ~3h verdict density) | Lower (2-3 items per verdict; faster verdict) |
| Forward-watched coverage | Variable; depends on hand-off author's diligence | Systematic; 8-bullet template forces enumeration |
| First-pass GREEN rate (% of PRs that hit GREEN without AMBER fold) | ~30-50% (estimated; not directly measured) | ~70-80% (expected) |

---

## 5. Metrics collection mechanism

ONR will populate the retrospective using:

1. **Council-verdict beacons** in `coordination/inbox/council-verdict-*.md` — count AMBER vs GREEN per cohort PR; count items per verdict
2. **GitHub PR metadata** — `gh pr view <N> --json mergedAt,createdAt` for open-to-merge wall-clock
3. **Hand-off Adversarial Brief sections** — count forward-watched per hand-off
4. **ONR's V5 #8 SPOT-CHECK SLA analysis baseline** — pre-Stage-05 dispatch latency
5. **QM Phase 0 instrumentation** (per V3 addendum #9-#11) — automated dispatch + verdict latency capture

---

## 6. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **Retro authoring timing** — when cohort-4 PASS gate met (W#78 ledger flip) OR earlier when sec-eng SPOT-CHECK verdict lands? ONR recommends post-PASS-gate (full data).
2. **Metrics collection automation** — should QM daemon collect Stage-05 metrics ongoing (similar to dispatch latency telemetry) vs ONR manual snapshots?
3. **Comparison to QM Phase 0 metrics** — QM is instrumenting sec-eng dispatch latency per V3 addendum; coordinate ONR retro with QM Phase 0 evidence consolidation?

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T12-45Z` item #5
2. ADR 0093 (Accepted at shipyard#104; 2026-05-21T15:48Z)
3. V3 #4 Adversarial Brief template prototype (shipyard#78 MERGED)
4. V3 #1 cohort-4 audit-trail viewer hand-off (shipyard#81 MERGED) — first canonical R3 instance
5. V5 #8 SPOT-CHECK SLA historical analysis (shipyard#89) — baseline metrics
6. V5 #3 test-eng-council subagent definition (shipyard#90 MERGED)
7. Council-verdict beacons inbox (per §5 metrics collection)

---

## 8. What ONR does next

V7 #5 deliverable complete. Files `onr-status-*-v7-item-5-stage-05-retro-scaffolding-complete.md`. Proceeds to V7 #3 (MVP demo critical-path analysis).

— ONR, 2026-05-22T13:10Z
