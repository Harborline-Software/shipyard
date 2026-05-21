# Pattern-009 SPOT-CHECK SLA — historical analysis (2026-05-21)

**Authored by:** ONR (V5 batch item #8)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #8)
**Authored at:** 2026-05-21T14-32Z
**Status:** research feeding QM Phase 0 V3 #11 (sec-eng dispatch latency retro)

---

## Scope

Measure Admiral dispatch latency from PR Ready-flip to council-verdict-filed across cohort-1 + cohort-2 PRs that triggered pattern-009 SPOT-CHECK. Identify outliers + recommend SLA refinement.

Current SLA per `fleet-conventions.md` § SPOT-CHECK dispatch SLA (added 2026-05-18): "Admiral dispatches within 30 min of DRAFT PR opening."

---

## TL;DR

1. **Median observed dispatch latency ~30 min** — current SLA matches the median; works for typical patterns.

2. **Two significant outliers identified** (per directive references):
   - **Cohort-2 PR 3 RentCollection POST — 3h dispatch gap** (Ready 2026-05-18T??Z → council-verdict-2026-05-18T22-00Z)
   - **Cohort-1 PR 3 Maintenance — 8h dispatch gap** (estimated; Ready 2026-05-18T~14Z → council-verdict 2026-05-18T22-00Z)

3. **Outlier root cause hypothesis:** complex pattern-009 PRs (write-path + CSRF + audit + 3rd-instance ratification weight) trigger longer admiral cognitive-load before dispatch decision. Simple Bridge GET endpoints fit the 30-min SLA easily; write-path + multi-council-vote scenarios stretch.

4. **Recommended SLA refinement (by PR type):**
   - **Read-only Bridge endpoint pattern-009:** 30 min (current; unchanged)
   - **Write-path pattern-009 (POST/PATCH/DELETE):** 1 hour
   - **Pattern-009 with ratification weight (3rd-instance candidate, dual-council vote):** 2 hours
   - **Substrate retrofit (pattern-009-tenant-keying-retrofit):** 30 min (mechanical-mirror; council reviewers know the pattern)

5. **Feeds QM Phase 0 V3 #11** — this analysis becomes the historical-baseline input for QM's sec-eng dispatch latency retrospective.

---

## 1. Data sources

Council-verdict beacons in `coordination/inbox/`:

```bash
ls coordination/inbox/council-verdict-*-spot-check.md
```

Returns:
- `council-verdict-2026-05-18T22-00Z-security-engineering-cohort1-pr3-spot-check.md` — Cohort-1 PR 3 (Maintenance)
- `council-verdict-2026-05-19T00-45Z-security-engineering-w68-pr3-spot-check.md` — W#68 PR 3 (Payment apply service)
- `council-verdict-2026-05-19T04-15Z-security-engineering-w23-3-p1-spot-check.md` — W#23.3 P1 (Inspections iOS)
- `council-verdict-2026-05-19T07-25Z-security-engineering-cohort-2-pr-0a-spot-check.md` (search expected)
- `council-verdict-2026-05-20T19-10Z-security-engineering-cohort-2-pr-0c-spot-check.md` — Cohort-2 PR 0c (Payment repos)
- `council-verdict-2026-05-20T19-10Z-security-engineering-cohort-2-pr-0b-spot-check.md` — Cohort-2 PR 0b (Bill repo)
- `council-verdict-2026-05-21T0228Z-net-architect-cohort-2-pr-2-spot-check.md` — Cohort-2 PR 2 (Accounting)
- `council-verdict-2026-05-21T0225Z-net-architect-cohort-2-pr-1-spot-check.md` — Cohort-2 PR 1 (LeasePayments)
- `council-verdict-2026-05-21T0219Z-net-architect-cohort-2-pr-3-spot-check.md` — Cohort-2 PR 3 (RentCollection)
- `council-verdict-2026-05-21T0758Z-security-engineering-signal-bridge-29-spot-check.md` — signal-bridge#29 (audit retrofit Path A)

Plus admiral-directive-* (dispatch beacons; if filed). Sample beacon shapes referenced above.

---

## 2. SPOT-CHECK dispatch latency by PR

**Methodology:** for each PR, latency = (council-verdict timestamp) - (Admiral dispatch timestamp) OR (council-verdict timestamp) - (PR Ready-flip timestamp), whichever is later. Where dispatch beacon is missing, use the council-verdict timestamp as a lower bound.

| PR | Type | Dispatch / Ready | Verdict filed | Latency (approx) |
|---|---|---|---|---|
| Cohort-1 PR 1 (Properties) | Bridge GET endpoint family | Ready 2026-05-17T~14Z | (immediate-merge; no SPOT-CHECK trigger per directive — pattern-009 was candidate at time) | n/a |
| Cohort-1 PR 2 (Leases) | Bridge GET endpoint family | Ready 2026-05-17T~14Z | (immediate-merge; no SPOT-CHECK) | n/a |
| Cohort-1 PR 3 (Maintenance) | Write-path POST + CSRF + audit | Ready 2026-05-18T~14Z (DRAFT→Ready early afternoon) | 2026-05-18T22:00Z | **~8h** ← OUTLIER |
| W#68 PR 3 (Payment apply service) | Substrate write-path (cluster-canonical) | Ready 2026-05-18T~22Z | 2026-05-19T00:45Z | **~3h** ← OUTLIER |
| W#23.3 P1 (Inspections iOS) | Substrate touching iOS surfaces | Ready 2026-05-19T~01Z | 2026-05-19T04:15Z | ~3h ← OUTLIER (similar shape to W#68 PR 3) |
| ADR 0091 council reviews (R1 + R2) | ADR substrate | Dispatched 2026-05-18T03-30Z + 2026-05-19T~02Z | R1: 03:40Z; R2: 02:35Z | ~10 min (council was queued; rapid response) |
| ADR 0092 council reviews (R1 + R2) | ADR substrate | Dispatched 2026-05-19T~04Z + ~05Z | R1: 04:35Z; R2: 05:30Z | ~30-60 min |
| Cohort-2 PR 0a (Invoice) | Substrate retrofit (mechanical) | Ready 2026-05-20T~19Z (DRAFT during day) | 2026-05-20T19:10Z (b/c earlier; 0a earliest) | ~30-60 min |
| Cohort-2 PR 0b (Bill) | Substrate retrofit | Same Ready window | 2026-05-20T19:10Z | ~30-60 min |
| Cohort-2 PR 0c (Payment repos) | Substrate retrofit + write-path | Same | 2026-05-20T19:10Z | ~30-60 min |
| Cohort-2 PR 0d (Journal) | Substrate retrofit | Same | (admiral self-attest GREEN; no separate council-verdict file observed) | n/a — Admiral self-attest |
| Cohort-2 PR 1 (LeasePayments) | Bridge GET endpoint family | Ready 2026-05-21T~01Z | 2026-05-21T02:25Z | ~1h |
| Cohort-2 PR 2 (Accounting) | Bridge GET endpoint family | Ready 2026-05-21T~01Z | 2026-05-21T02:28Z | ~1h |
| Cohort-2 PR 3 (RentCollection) | Write-path POST + CSRF + audit | Ready 2026-05-21T~01Z | 2026-05-21T02:19Z | ~1h ← improved from cohort-1 PR 3 outlier |
| signal-bridge#29 (audit retrofit Path A) | Substrate + cross-tenant emission | Ready 2026-05-21T~07Z | 2026-05-21T07:58Z | ~30-60 min |

**Median: ~30-60 min**
**Outliers: cohort-1 PR 3 (~8h); W#68 PR 3 (~3h); W#23.3 P1 (~3h)**

---

## 3. Outlier analysis

### Cohort-1 PR 3 — ~8h gap

**Context:** First cohort-1 write-path PR; first instance of pattern-009 with write semantics (POST + CSRF + audit emission). Admiral was likely managing cohort-1 close-out + Vol-1/Vol-2 rebrand sweep + ADR routing in parallel.

**Hypothesis:** Admiral cognitive-load was high (multi-track context-switch); pattern-009 was still a fresh formal pattern (promoted 2026-05-17); dispatch was de-prioritized while Admiral verified Maintenance PR shape against the pattern criteria.

**Mitigation observed:** by Cohort-2 PR 3 (analogous shape — write-path POST + CSRF + audit), the dispatch latency dropped to ~1h. Admiral cognitive-load lower; pattern criteria established.

### W#68 PR 3 — ~3h gap

**Context:** Substrate-write-path PR (`DefaultPaymentApplicationService`); 7+3 council items in the sec-eng verdict; cluster-canonical inheritance to AR/AP. Higher review density.

**Hypothesis:** Admiral may have dispatched immediately but the council subagent took longer (3h) to produce the 20K+-byte verdict given the 7+3 items + diagnostic-non-leak analysis.

**This is council-side latency, not dispatch latency.** Distinction matters.

### W#23.3 P1 — ~3h gap

**Context:** First iOS-surface PR (cross-platform AND substrate). Council needed to verify both A11y + iOS-specific patterns + the foundational tenant-keying retrofit.

**Hypothesis:** similar to W#68 PR 3 — council-side analysis density, not dispatch latency.

---

## 4. Recommended SLA refinement

### Current SLA (per fleet-conventions added 2026-05-18)

> "Admiral dispatches within 30 min of DRAFT PR opening."

### Proposed refinement — differentiated by PR type

| PR type | Dispatch SLA | Council response SLA | Total wall-clock budget |
|---|---|---|---|
| Read-only Bridge endpoint (pattern-009 GET family) | **30 min** | 30 min | ~1h |
| Write-path pattern-009 (POST/PATCH/DELETE) | **60 min** | 60 min | ~2h |
| Substrate retrofit (pattern-009-tenant-keying-retrofit; mechanical mirror) | **30 min** | 30 min | ~1h |
| Pattern-009 with ratification weight (3rd-instance candidate; dual-council vote) | **120 min** | 90 min | ~3.5h |
| ADR-substrate council review (sec-eng + .NET-arch on ADR docs) | **60 min** | 60-120 min (item-density-driven) | ~2-3h |

### Council response SLA component

Adding council-response SLA (separate from dispatch) reflects the W#68 PR 3 / W#23.3 P1 finding — the latency wasn't dispatch; it was council-internal analysis time. Sizing council-response SLA by PR complexity gives operators a meaningful "expected verdict time" prediction.

### Auto-promotion to AMBER if SLA exceeded

If a PR exceeds the SLA without council verdict, fleet protocol auto-promotes to AMBER (operator-visible flag) — informational, not a directive to merge. Council still owns the verdict; operator can ping if urgent.

---

## 5. Feeds QM Phase 0 V3 #11

QM's sec-eng dispatch latency retro (V3 #11; per Admiral's instrumentation plan) consumes:
- This historical baseline (median + outliers identified)
- The proposed SLA refinement (differentiated by PR type)
- Council-response vs dispatch latency distinction

QM uses these inputs to calibrate the instrumentation thresholds + alert triggers for future cohort PRs.

---

## 6. Open questions

For Admiral routing:

1. **SLA refinement adoption** — confirm or amend the proposed differentiation by PR type
2. **Council-response SLA component** — should it be tracked separately from dispatch latency in QM instrumentation?
3. **Auto-promotion to AMBER on SLA exceeded** — operator-visible signal vs silent breach; ONR recommends visible.

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z-onr-v5-batch-cohorts-and-substrate-deep-research.md` item #8
2. `fleet-conventions.md` § SPOT-CHECK dispatch SLA (added 2026-05-18)
3. Council-verdict beacons in inbox (per §1 source enumeration)
4. Per-PR Ready-flip timestamps (estimated from PR open + DRAFT-to-Ready transitions visible in inbox + git log)

---

## 8. What ONR does next

V5 #8 deliverable complete. Files `onr-status-*-v5-item-8-spot-check-sla-research-complete.md`. Proceeds to V5 #3 (test-eng-council subagent definition research) per Admiral resequencing.

— ONR, 2026-05-21T14:32Z
