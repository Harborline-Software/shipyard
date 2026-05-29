# Post-cohort-10 retrospective — scaffolding template

**Authored by:** ONR (V8 batch item #6)
**Requester:** Admiral (per `admiral-directive-2026-05-22T14-00Z` item V8 #6; per V7 #7 question #7 ruling ONR-led with Admiral ratification)
**Authored at:** 2026-05-22T14-30Z

---

## Purpose

Cohort-10 is a forward-looking milestone (~6+ cohorts beyond cohort-4 pilot). This
doc scaffolds the post-cohort-10 retrospective framework NOW — so when the milestone
arrives, the metrics-extraction protocol + retro-doc template exist + the
comparison framework is pre-pinned.

**Why scaffold now (V8 #6 vs T-cohort-10):**

1. Cohort-1/2/3/4 baselines are still warm in beacons + PRs; pinning them now
   prevents reconstruction-cost later.
2. Substrate ladders (ADR 0091/0092/0094) are in active state; the protocol for
   measuring "cumulative ADR effort" is easier to define now while the work is
   visible.
3. Per ONR's V7 #5 Stage-05 retro scaffolding precedent: scaffolding-ahead pattern
   reduces retro-authoring cost from ~1 day to ~3-4h.

---

## 1. Pre-cohort-10 substrate metrics (baseline)

Captured 2026-05-22 from current fleet state. These are the BASELINE the retro
compares against.

### 1.1 Substrate primitives (ADRs LIVE)

| ADR | Status | Cumulative effort (research + ADR + ratification + first instance) | Cluster |
|---|---|---|---|
| 0046 | LIVE | ~2-3 days | IOperationSigner |
| 0049 | LIVE | ~3-5 days | Audit substrate |
| 0052 | LIVE | ~2-3 days | Bidirectional messaging |
| 0091 R2 | LIVE | ~5-7 days (R1 → R2 fold cycle) | ITenantContext divergence |
| 0092 R2 | LIVE | ~5-7 days (R1 → R2 fold cycle) | Substrate tenant-keyed repository |
| 0093 | LIVE | ~3-4 days (Adversarial Review Protocol) | Stage-05 protocol amendment |
| 0094 | LIVE | ~2-3 days | IAuditEventReader |
| **Cumulative** | 7 ADRs | **~22-32 days** | (8 weeks of substrate effort across ~5 calendar weeks via parallelism) |

### 1.2 Pattern catalog metrics (baseline)

| Metric | Value (2026-05-22) | Notes |
|---|---|---|
| Formal patterns ratified | 6 (pattern-001 to pattern-006 + pattern-009 + pattern-009-tenant-keying-retrofit) | Per V7 #6 catalog snapshot |
| Candidate patterns | 4 (pattern-007, 008, 011-event-publisher, 012-financial-write-path, 013-cartridge-read-via-post) | |
| Hold-pending-3rd-instance | 2 (pattern-010-financial-write-path, 011-cartridge-read-via-post) | Pre-rename collision; now numbered 012/013 |
| Patterns drift-found | 1 (pattern-014 referenced but unverified) | Per V7 #6 drift finding |

### 1.3 Cohort + workstream cadence

| Cohort | Workstream | Status | Build cycle (Stage-05 → Stage-06 close-out) |
|---|---|---|---|
| Cohort-1 | W#74 Properties/Leases/Maintenance | MERGED | ~3-4 weeks |
| Cohort-2 | W#76 financial cluster | MERGED | ~3-4 weeks |
| Cohort-3 | W#77 reports framework | MERGED | ~2-3 weeks |
| Cohort-4 | W#78 audit-trail viewer | MERGED (first Stage-05 pilot) | ~2-3 weeks |
| Cohort-5 | W#79 ARR/MRR (planned) | SCOPED | TBD |
| Cohort-6 | W#80 AP Aging (planned) | SCOPED | TBD |

**Baseline cohort velocity: ~3-week median build cycle.**

### 1.4 Council dispatch cadence

| Cohort | sec-eng-council dispatches | .NET-architect dispatches | Median verdict latency |
|---|---|---|---|
| Cohort-1 | ~4 | ~2 | ~30 min (1 outlier 8h) |
| Cohort-2 | ~7 | ~4 | ~30-60 min |
| Cohort-3 | TBD | TBD | TBD |
| Cohort-4 (Stage-05 pilot) | TBD | TBD | TBD (expected lower) |

### 1.5 ONR / Engineer / FED PR velocity

| Role | PRs through cohort-4 (cumulative) | Lines (cumulative) |
|---|---|---|
| ONR | ~50+ (research / hand-offs / ADR scaffolds) | ~12,000+ |
| Engineer | ~80+ (substrate + Bridge + tests) | TBD |
| FED | ~30+ (anchor-react rebinds + UX) | TBD |

Numbers approximate; precise capture at cohort-10 retro authoring time via:
`gh pr list --search "author:@me merged:<date-range>" --limit 200`

---

## 2. Post-cohort-10 expected milestones

By the time cohort-10 completes (~6+ cohorts from cohort-4), the fleet expects to
have reached these milestones. Retro evaluates which were met.

### 2.1 Expected by cohort-10

- [ ] All 7 substrate ADRs fully consumed by all substrate cohorts (no "facade-only"
      narrow-pending consumers)
- [ ] Pattern catalog: 10+ formal patterns ratified (current 6 + ~4 new from cohorts 5-10)
- [ ] Stage-05 Adversarial Review protocol has 6+ data points (cohort-4 through 9)
- [ ] DEFER verdict in routine use (V8 #5 protocol)
- [ ] Onboarding-ladder ladder-complete (per V8 #3 scaffold)
- [ ] sec-eng-council UPF rating B-or-A (per V7 #7 UPF follow-up + ADR 0093 Rev 2)
- [ ] Test-eng-council dispatched on 3+ cohorts (operational, not just scoped)
- [ ] MVP demo SHIPPED to first paying tenant (per V7 #3 critical-path analysis)
- [ ] Multi-tenant federation ladder scoped (signal-bridge cross-tenant patterns)
- [ ] First Tauri desktop release (anchor-desktop ladder; W#81+ expected)

### 2.2 Stretch milestones

- [ ] Field-capture mobile (W#23 iOS) — Stage-05 scoped
- [ ] Cartridge marketplace operational (3+ live cartridges)
- [ ] Inverted-Stack book first chapter draft (parallel PAO work)

---

## 3. Retrospective doc template (cohort-10 retro)

Located at: `shipyard/icm/07_review/post-cohort-10-retrospective.md` (when authored).

```markdown
# Post-cohort-10 retrospective

**Authored by:** ONR
**Reviewed by:** Admiral (ratification)
**Authored at:** YYYY-MM-DD (when cohort-10 ledger flip occurs)

---

## 1. Cohort-10 close-out summary

- Cohort-10 workstream: W#NN (TBD)
- Cohort-10 close-out date: YYYY-MM-DD
- Cumulative session-equivalent effort: ~XX days across YY calendar weeks
- ONR / Engineer / FED PR counts: <fill from gh pr list>

## 2. Milestone evaluation (vs §2.1 expected)

| Expected milestone | Met? | Evidence | Notes |
|---|---|---|---|
| 7 ADRs fully consumed | YES / PARTIAL / NO | <ADR-N consumer count> | |
| 10+ formal patterns | YES / NO | <pattern catalog count> | |
| 6+ Stage-05 data points | YES / NO | <cohort count> | |
| ... | | | |

## 3. Substrate metrics — delta from baseline

| Metric | Baseline (cohort-4 close-out) | Cohort-10 close-out | Delta |
|---|---|---|---|
| Cumulative ADR effort | 22-32 days | <X> days | <%> |
| Formal pattern count | 6 | <X> | <delta> |
| Cohort build velocity | ~3 weeks | <X> weeks | <delta> |
| sec-eng-council dispatch count (per cohort) | ~5 | <X> | <delta> |
| AMBER folds per PR (Stage-05 effect) | 1.0 baseline | <X> | <%> |

## 4. Wins

(3-5 wins observed; e.g., "Adversarial Brief protocol reduced AMBER folds by X%
across cohorts 5-9; cumulative review-cycle savings ~X hours")

## 5. Misses / disappointments

(Items expected but not met; explanations)

## 6. Pattern catalog evolution

- New formal patterns ratified (list with ratification dates)
- Patterns that emerged but didn't reach 3rd-instance threshold
- Patterns that proved over-engineered and were retired
- Comparison to V7 #6 snapshot's 5 drift findings — were they resolved?

## 7. ADR evolution

- New ADRs accepted (list)
- ADR amendments / Rev 2/3/etc. (list with motivation)
- ADRs that proved foundational (frequently-cited)
- ADRs that proved over-scoped (rarely cited)

## 8. Process learnings

- Stage-05 Adversarial Brief — what worked, what didn't (cross-reference V7 #5 retro)
- Council dispatch SLA discipline — held? slipped?
- DEFER verdict usage — frequency met expectation (~5-10%)?
- Pattern numbering collisions — recurrence?
- Worktree-per-deliverable pattern — sustained?

## 9. Decision-quality assessment

- ADRs that, in retrospect, should have shipped 1+ cohort earlier (or later)?
- Patterns that, in retrospect, should have been formalized as candidates earlier?
- Architectural pivots that paid off (e.g., Tauri-first pivot 2026-05-17)?

## 10. Recommendations for cohort-11+

- Process changes
- Substrate work to prioritize
- Pattern catalog gaps to close

## 11. Cumulative substrate posture

- Multi-tenant defense-in-depth — all 9 layers operational?
- Audit substrate coverage — all event types reachable?
- ITenantContext divergence — fully resolved (Step 5 facade deleted)?
- Onboarding-ladder shipped?

## 12. Forward outlook to cohort-15

- Next-most-leveraged substrate item
- Pattern catalog target (15+ formal patterns? 20+?)
- ADR target (10+ accepted?)
- MVP customer count target

---

— ONR, YYYY-MM-DD; ratified by Admiral
```

---

## 4. Metrics-extraction protocol

ONR populates the retro using:

### 4.1 Automated capture (preferred)

When cohort-10 ledger flip occurs, ONR runs these queries:

```bash
# Cumulative substrate ADR effort (research + ADR PRs)
cd shipyard
gh pr list --search "docs(icm) author:@me merged:2026-05-15..<cohort-10-date>" --json number,mergedAt,additions,deletions

# Pattern catalog count
cat _shared/engineering/standing-approved-patterns.md | grep -E '^## pattern-' | wc -l

# Council dispatch counts (per beacon directory scan)
ls coordination/inbox/council-verdict-*.md | wc -l
ls coordination/inbox/council-verdict-*-security-engineering-*.md | wc -l
ls coordination/inbox/council-verdict-*-.NET-architect-*.md | wc -l
ls coordination/inbox/council-verdict-*-defer.md | wc -l   # DEFER counter (V8 #5)

# Engineer / FED PR counts (per repo)
gh pr list --repo Harborline-Software/shipyard --state merged --search "merged:<window>" --json number,author --limit 200
gh pr list --repo Harborline-Software/signal-bridge --state merged --search "merged:<window>" --json number,author --limit 200
gh pr list --repo Harborline-Software/sunfish --state merged --search "merged:<window>" --json number,author --limit 200

# Cohort build velocity (Stage-05 hand-off date → cohort ledger-flip date per MASTER-PLAN.md)
```

### 4.2 QM daemon integration (if available)

Per V3 addendum #9-#11, QM daemon instruments dispatch + verdict latency. If
QM Phase 0 evidence is consolidated by cohort-10, leverage it:

```bash
# QM daemon emits to coordination/instrumentation/sec-eng-dispatch-latency-*.json
# Aggregate per-cohort: median, p50, p90, outliers
```

### 4.3 Manual capture (fallback)

If automated queries fail (e.g., GitHub API rate limits), manual capture via:
- ONR's own beacon inbox (`onr-status-*` files)
- Admiral's directive files (`admiral-directive-*`)
- ADR cumulative file (`shipyard/docs/adrs/*.md`)

---

## 5. Comparison framework

The retro compares pre-cohort-10 (today) vs cohort-10 close-out:

### 5.1 Substrate maturity dimensions

| Dimension | Pre-cohort-10 (today) | Post-cohort-10 (expected) | Delta |
|---|---|---|---|
| ADR count | 7 | 10+ | +3+ |
| Pattern count | 6 formal + 4 candidate | 10+ formal | +4+ formal |
| Cohort build velocity | ~3 weeks | ~2-3 weeks (Stage-05 effect) | -1 week ideal |
| AMBER folds per PR | 1.0 | 0.2-0.3 (per V7 #5 hypothesis) | -70-80% |
| Pattern numbering collisions | 2 (010 + 011 renamed to 012 + 013) | 0 (process hardened) | -2 |
| sec-eng-council dispatch verdict density | 5-6 items / verdict | 2-3 items / verdict | -50%-60% |

### 5.2 Process maturity dimensions

| Dimension | Pre-cohort-10 (today) | Post-cohort-10 (expected) | Delta |
|---|---|---|---|
| Stage-05 Adversarial Brief routinized | Cohort-4 pilot only | Cohorts 4-10 (7 instances) | +6 |
| DEFER verdict usage | Just-introduced (V8 #5) | 5-10% of dispatches | normalize |
| Worktree-per-deliverable pattern | Established 2026-05-21 | Routine | normalize |
| sec-eng-council UPF rating | D-to-C | B-floor / A-target | UP |

### 5.3 Product maturity dimensions

| Dimension | Pre-cohort-10 (today) | Post-cohort-10 (expected) | Delta |
|---|---|---|---|
| MVP demo state | Critical-path identified (V7 #3) | SHIPPED to first paying tenant | UP |
| Tauri desktop release | Pivoted (V7 #3 mentions) | First release shipped | UP |
| Onboarding-ladder | Stage-02 scoped (V8 #3) | Built + operational | UP |
| Cartridge marketplace | 0 live | 2+ live (stretch) | UP |

---

## 6. Decision points (when retro fires)

The retrospective fires when **ALL** of these hold:

1. **Cohort-10 ledger-flip is logged in MASTER-PLAN.md** (cohort marked CLOSED)
2. **Stage-05 retro from cohort-4 has been authored** (per V7 #5 scaffold) — gates
   on this so we have the pilot baseline to compare against
3. **All 7 substrate ADRs are LIVE and consumed** — gates so we don't measure
   incomplete substrate
4. **Admiral ratification of retro authoring** — ONR drafts; Admiral ratifies
   before publication

---

## 7. Cerebrum learning

When retro completes, ONR captures fleet-cerebrum entries for:
- Pattern numbering collision lessons (if encountered cohort-5+)
- Council dispatch SLA discipline lessons (if slipped)
- Stage-05 protocol lessons (catch-rate observations)
- ADR amendment lessons (Rev 2/3 cycle observations)

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T14-00Z` item V8 #6
2. V7 #5 Stage-05 retro scaffold (shipyard#110) — comparison framework precedent
3. V7 #6 pattern catalog snapshot (shipyard#108) — pre-cohort-10 baseline
4. V7 #2 cross-cohort dependency graph (shipyard#109) — substrate ladder topology
5. V7 #3 MVP demo critical-path analysis (shipyard#111) — product milestone targets
6. V8 #5 DEFER verdict spec (shipyard#114) — process metric to track
7. ADR 0046, 0049, 0052, 0091, 0092, 0093, 0094 (substrate ADR baseline)
8. MASTER-PLAN.md (cohort cadence baseline)

---

## 9. What ONR does next

V8 #6 deliverable complete. Proceeds to V8 #3 (Onboarding-ladder Stage-02
scaffolding; the highest-leverage MVP-blocking gap per V7 #3 finding).

— ONR, 2026-05-22T14:30Z
