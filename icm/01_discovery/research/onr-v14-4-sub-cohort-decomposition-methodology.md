# Sub-cohort decomposition methodology (V14 #4)

**Authored by:** ONR (V14 batch item #4)
**Requester:** Admiral (per V14 standing-dispatch directive 2026-05-25)
**Authored at:** 2026-05-25T1830Z
**Status:** draft

---

## Scope

The fleet has shipped three cohorts that fanned into sub-PR shapes:

- **Cohort-2 PR 0a/0b/0c/0d** (tenant-keying retrofit pass across 4 financial cluster
  endpoints; sec-eng + .NET-architect dual SPOT-CHECK per pattern-009-tenant-keying-
  retrofit; 4 distinct PRs).
- **Cohort-3 PR cluster** (cartridge-backed reports: 1 shared-infra PR + 4 page PRs +
  close-out; cohort-3 design direction PR #116 frames the split).
- **Cohort-4 cycle-1 / cycle-2** (cross-repo wire-contract pair where the cycle split
  was the cleanest-long-term path per admiral-ruling-2026-05-22T22-30Z; Engineer DTO ship
  separated from FED frontend alignment).

Is there a canonical methodology for when sub-cohorts are correct versus when the cohort
is mis-scoped and should have been one PR? This research proposes a Stage-05
"should-this-fan-out" heuristic grounded in the three retrospectives.

**Out of scope:** the Stage-05 template itself (covered under V14 #3); specific cohort
re-scoping recommendations; ADR 0093 amendments (parallel work).

---

## TL;DR

1. **Sub-cohort decomposition is correct when:** (a) a single PR's diff would exceed
   ~1500 lines net, (b) the work crosses repository boundaries with substrate-first
   sequencing dependencies, (c) the work touches multiple independent-but-paired surfaces
   that can ship in parallel, OR (d) the council SPOT-CHECK budget per PR is being
   exceeded.

2. **Sub-cohort decomposition is INCORRECT when:** (a) the sub-PRs cannot stand alone
   semantically (each sub-PR breaks main if shipped alone), (b) the sub-PRs serialize on
   each other (no parallel value), (c) sub-PRs duplicate the same review burden that one
   PR would carry, OR (d) the cohort is mis-scoped (the work is genuinely one decision).

3. **Three canonical sub-cohort shapes have emerged:**
   - **Independent-parallel (cohort-2 type):** sibling PRs that share a pattern but each
     stands alone. The cohort frames the cluster; each sub-PR ships independently.
   - **Substrate-then-consumer (cohort-3 type):** one shared-infra PR ships primitives;
     subsequent PRs consume them. The shared-infra PR gates the rest.
   - **Cross-repo wire-contract (cohort-4 type):** Engineer DTO ships first; FED
     consumes. The cycle-1 / cycle-2 split is forced by cross-repo sequencing.

4. **The proposed Stage-05 heuristic ("should-this-fan-out?") is a 5-question gate** —
   answer the 5 questions before authoring Stage-05; if 2+ answers point toward fan-out,
   author as a sub-cohort cluster.

5. **Sub-cohort patterns have visible naming conventions** that should be codified:
   - PR-0a/0b/0c/0d (independent-parallel; cohort-2-style)
   - PR-1 / PR-2 / PR-3 with a designated "anchor" PR (substrate-then-consumer)
   - cycle-1 / cycle-2 with explicit Admiral ruling (cross-repo wire-contract)

6. **Anti-patterns to watch for:**
   - Mis-scoped cohort whose "sub-PRs" are actually a single architectural decision
     dressed as multiple PRs (review-load shifting, not actual decomposition)
   - Sub-cohorts where the SPOT-CHECK load multiplies but the decision-load doesn't
   - Sub-cohorts where the council has to read all sub-PRs in sequence anyway
     (decomposition didn't actually parallelize review)

7. **The cohort-4 cycle split was correct.** Per admiral-ruling-2026-05-22T22-30Z, the
   Option B cleanest-long-term path REQUIRED a cross-repo wire-contract sequencing rule
   that pattern-S05-4 codifies. The cycle-2 GREEN attest (2026-05-25T13:12Z) confirms
   the methodology held.

---

## 1. Three canonical sub-cohort shapes — case studies

### 1.1 Independent-parallel: cohort-2 PR 0a/0b/0c/0d

**Source artifacts:**
- `coordination/inbox/admiral-directive-2026-05-20T13-15Z-engineer-cohort-2-pr-0a-and-0b-amend-together.md`
- `coordination/inbox/council-verdict-2026-05-20T12-25Z-security-engineering-cohort-2-pr-0a-spot-check.md`
- `coordination/inbox/council-verdict-2026-05-20T19-10Z-security-engineering-cohort-2-pr-0b-spot-check.md`
- `coordination/inbox/council-verdict-2026-05-20T19-10Z-security-engineering-cohort-2-pr-0c-spot-check.md`
- `coordination/inbox/council-verdict-2026-05-21T0045Z-security-engineering-cohort-2-pr-0d-spot-check.md`
- `coordination/inbox/admiral-ruling-2026-05-20T19-30Z-engineer-cohort-2-pr-0a-0b-0c-unified-green-attest.md`
- `coordination/inbox/admiral-attest-2026-05-21T01-10Z-shipyard-64-pr-0d-dual-green-promote.md`

**Shape:** Cohort-2 PR 0 was the tenant-keying retrofit pass across 4 financial cluster
endpoints. Rather than ship as one ~2000-line PR, the work split into:

- PR 0a: LeaseDetailPage (Invoice retrofit)
- PR 0b: AccountingPage (Bill retrofit)
- PR 0c: RentCollectionPage (Payment retrofit)
- PR 0d: JournalEntryImporter (JE retrofit)

Each PR carries `@candidate-pattern: pattern-009-tenant-keying-retrofit`. Each ships
independently (no cross-PR dependencies). Council verdict per-PR. Total ratification
arc: 4 PRs → pattern ratifies to formal.

**Why decomposition was correct:**

1. **Diff-size:** unified PR would have been ~2000 lines; over the 1500-line standing
   threshold.
2. **Parallel-shippable:** PR 0a, 0b, 0c had no cross-dependencies (PR 0d was substrate-
   tier and depended on substrate-side changes Engineer was making in parallel).
3. **SPOT-CHECK ratification:** the pattern was a candidate; the 4-instance shipping
   completed the dual-gate ratification (4 sec-eng + .NET-architect SPOT-CHECKs across
   the cohort produced the ratification evidence).
4. **Review parallelization:** sec-eng could review PR 0a and PR 0b in the same session
   (different files; consistent pattern); the cluster-shape made it efficient.

**Why decomposition could have been wrong (but wasn't):**

- If the 4 PRs shared the same handler file: they would serialize on rebase, defeating
  parallel shipment.
- If pattern-009-tenant-keying-retrofit had not been a known candidate: each PR would
  bear full council review independently — duplicative.
- If the 4 surfaces had subtle architectural differences: a single unified design
  decision would have been more coherent than 4 distinct verdict threads.

None of these counter-conditions held; cohort-2 PR 0 decomposition was the right call.

**Anti-pattern this surface AVOIDED:** treating the 4 endpoints as one "tenant-keying
retrofit" PR that the council would have RED-flagged for diff-size + interleaved
concerns.

### 1.2 Substrate-then-consumer: cohort-3 PR cluster

**Source artifacts:**
- `shipyard/_shared/design/cohort-3/INDEX.md` (PR #116 MERGED 2026-05-22)
- `shipyard/icm/01_discovery/research/cohort-3-pr-cluster-consolidated-spec.md` (ONR V10 #3 PR #123 OPEN)
- Cohort-3 PR structure: PR 1 (shared-infra) + PR 2-5 (page PRs) + PR 6 (close-out)

**Shape:** Cohort-3 ships 4 cartridge-backed reports + cross-page primitives. PR 1 ships
the shared-infra (`<ProvisionalityBanner>`, `<ExportCsvButton>`, `<ReportFilterBar>`,
`<ChartSelector>`, `<RunButton>`) — primitives needed by every subsequent page PR. PRs
2-5 consume the primitives.

**Why decomposition was correct:**

1. **Shared-infra dependency:** PRs 2-5 cannot ship without PR 1's primitives. Sequencing
   is structural.
2. **Parallel-after-anchor:** PRs 2-5 ship in parallel once PR 1 lands. No cross-page
   dependencies.
3. **Pattern-candidate ratification:** PR 1 introduces candidate patterns 015/016/017;
   PRs 2-5 are first-instance exercises. 4 instances in 4 PRs feed the ratification arc.
4. **PAO direction surface:** PRs 2-5 each carry distinct PAO design direction (per-page
   docs in `_shared/design/cohort-3/`). Coherent decomposition.

**Why decomposition could have been wrong (but wasn't):**

- If PR 1 contained too many primitives (>5): splitting PR 1 might be needed.
- If PRs 2-5 shared significant code: code-reuse refactoring should have happened in PR 1.
- If the cartridges were architecturally divergent: separate cohorts would be more
  appropriate than separate PRs in one cohort.

None held. Cohort-3 decomposition was the right call.

**Anti-pattern this surface AVOIDED:** treating the 4 reports as one mega-PR (review-
unmanageable; pattern-claim accounting opaque).

### 1.3 Cross-repo wire-contract: cohort-4 cycle-1 / cycle-2

**Source artifacts:**
- `coordination/inbox/council-verdict-2026-05-22T1558Z-security-engineering-sunfish-71-cohort-4-fed-pr-1-spot-check.md` (cycle 0 RED)
- `coordination/inbox/council-verdict-2026-05-22T1611Z-security-engineering-sunfish-71-cycle-1-reattest.md` (cycle 1 AMBER)
- `coordination/inbox/council-verdict-2026-05-25T1312Z-security-engineering-sunfish-71-cycle-2-reattest.md` (cycle 2 GREEN)
- `coordination/inbox/admiral-ruling-2026-05-22T22-30Z-cohort-4-client-side-tenant-assertion-cleanest-long-term.md` (Option B ruling)

**Shape:** Cohort-4 PR 1 (sunfish#71) initially attempted to ship full A1+A2+G1 closure
in a single PR. Cycle-0 RED verdict surfaced that the A1 client-side tenant assertion
required an Engineer-side DTO extension (substrate `tenant_id` + whoami `tenantId`) that
hadn't shipped yet. Admiral ruled Option B: cycle 1 closes structural wire-contract
defects WITHOUT the defense-in-depth assertion; cycle 2 closes the defense-in-depth
assertion AFTER Engineer cohort-4 PR 2 lands the DTO extension.

This is fundamentally different from cohort-2 (sibling PRs) and cohort-3 (substrate-then-
consumer): the cycle split is **within a single PR's amendment history**, not across
multiple distinct PRs. PR sunfish#71 has cycle-1 commit (85f0191) + cycle-2 commit
(23a2c2f); the cycle boundary is the Admiral ruling, not a separate PR.

**Why decomposition was correct:**

1. **Cross-repo wire-contract sequencing dependency:** the defense-in-depth assertion
   requires a wire field that didn't yet exist on the server's DTO. Shipping the
   assertion before the DTO extension would create dead code (which is what cycle 0's
   attempt produced).
2. **Cleanest-long-term path (per CIC standing directive 2026-05-21):** the alternative
   was to paper over the gap with optional chaining or dead-code guards, which would
   technically pass the test suite but ship a structurally false claim. Option B's cycle
   split was the cleanest path.
3. **Sec-eng verdict granularity:** the cycle structure allows sec-eng to verdict each
   cycle independently — cycle-1 AMBER for "structural defects closed; semantic defense
   deferred," cycle-2 GREEN for "semantic defense restored after substrate extension."

**Why decomposition could have been wrong (but wasn't):**

- If Engineer's DTO extension had been a one-line addition: the cycle split would have
  been over-engineered; just block FED on Engineer's PR ship and avoid the amendment
  cycle.
- If the FED amendment was massive: shipping the cycle 1 closure independently might not
  have been viable.

In the cohort-4 actual case, Engineer's DTO extension was ~5 fields across 3 files (non-
trivial); FED's cycle-1 closure was ~7 items requiring substantial rework; FED's cycle-2
restoration was ~7 closure conditions on a separate amendment. The cycle split was the
right granularity.

**Anti-pattern this surface AVOIDED:** shipping a defense-in-depth claim that doesn't
fire in production (the cycle-0 RED's R1 finding).

---

## 2. Proposed Stage-05 5-question heuristic

Before authoring a Stage-05 plan, run these 5 questions. If 2+ answers point toward
fan-out, author as a sub-cohort cluster:

### Q1 — Diff-size estimate

> "If I author this as one PR, will the unified diff exceed ~1500 lines net?"

- **Yes → fan-out signal.** 1500-line PRs are review-burdensome and trigger council
  rejections per the standing-approved-patterns catalog's revoke conditions.
- **No → no fan-out signal from this dimension.**

### Q2 — Cross-repo wire-contract dependency

> "Does this work touch more than one repo with a substrate-first sequencing dependency?
> (e.g., Engineer extends a Bridge DTO that FED then consumes; substrate primitive ships
> before consumer)"

- **Yes → fan-out signal.** Cross-repo sequencing forces cycle-split OR multi-PR structure
  to avoid shipping consumers before substrate.
- **No → no fan-out signal from this dimension.**

### Q3 — Independent-but-paired surfaces

> "Does this work touch multiple independent surfaces that share a pattern but can each
> stand alone?"

- **Yes → fan-out signal.** Independent-parallel decomposition (cohort-2-style) is
  appropriate.
- **No → no fan-out signal from this dimension.**

### Q4 — Council SPOT-CHECK budget

> "Will the unified PR exceed the council's per-PR SPOT-CHECK budget (typically ~30-90
> min for sec-eng + .NET-architect dual SPOT-CHECK)?"

- **Yes → fan-out signal.** Council review fatigue → AMBER verdicts and rework.
- **No → no fan-out signal from this dimension.**

### Q5 — Standalone semantic coherence

> "Can each potential sub-PR stand alone semantically? (Does main still build + tests
> pass if only sub-PR 1 ships, or do all sub-PRs need to ship together?)"

- **Yes (each sub-PR is standalone) → fan-out is SAFE.** Independent-parallel or
  substrate-then-consumer pattern fits.
- **No (sub-PRs require each other) → fan-out is RISKY.** Either ship as one PR OR use
  a cycle-split pattern (cohort-4-style) where amendments to one PR are the granularity.

### Decision matrix

| Q1 (diff) | Q2 (cross-repo) | Q3 (parallel surfaces) | Q4 (council) | Q5 (standalone) | Recommendation |
|---|---|---|---|---|---|
| Yes | No | No | No | No | Single PR; trim scope OR file as one large PR with ADM warning |
| Yes | No | Yes | Yes | Yes | Independent-parallel cohort (cohort-2 style) |
| No | Yes | No | No | No | Cross-repo cycle-split (cohort-4 style) |
| Yes | No | Yes | No | Yes | Substrate-then-consumer (cohort-3 style) — if there's a shared-infra surface; else independent-parallel |
| No | No | No | No | Yes | Single PR; no fan-out needed |
| No | No | No | No | No | Single PR; review scope (small PRs that don't stand alone are unusual; might be mis-scoped) |
| Yes | Yes | Yes | Yes | Mixed | Hybrid — see §3 |

---

## 3. Edge cases

### 3.1 Cross-repo coordination (cohort-4 pattern)

When Q2 = Yes AND the cross-repo work is sequencing-bound (Engineer must ship before
FED), the cycle-split pattern is the right shape. Cycle-1 ships structural alignment;
cycle-2 ships semantic restoration after substrate extension.

**Stage-05 plan must include:**
- Explicit naming of which repo ships first
- The cycle-1 / cycle-2 boundary (what amendment closes which cycle)
- Admiral ruling reference (if cleanest-long-term-path is engaged)
- Forward-watch comments in code naming the cycle-2 follow-on

**Per cohort-4 evidence:** the cycle-1 / cycle-2 sub-cohort structure is appropriate when
the cross-repo dependency is "Engineer ships substrate DTO/feature; FED consumes." It is
NOT appropriate when the cross-repo work is independent (each repo's PR ships
independently with no sequencing dependency — that's just a sibling-PR cluster).

### 3.2 Substrate amendment cascades (cohort-2 pattern)

When Q3 = Yes AND the surfaces share a candidate pattern that's not yet ratified, the
independent-parallel pattern is the right shape. Each sub-PR exercises the candidate
pattern; cluster-completion produces ratification evidence.

**Stage-05 plan must include:**
- The list of sub-PRs by sequence number (0a / 0b / 0c / 0d)
- The candidate pattern being exercised
- The pattern-009-tenant-keying-retrofit-style dual SPOT-CHECK requirement (if applicable)
- Ratification-trigger conditions (e.g., "4 instances ratify the pattern")

### 3.3 Independent-but-paired surfaces (cohort-3 pattern)

When Q3 = Yes AND Q1 = Yes AND there's a shared-infra surface, the substrate-then-
consumer pattern is the right shape. The shared-infra PR (PR 1) ships first; consumer
PRs (PR 2-5) ship in parallel after.

**Stage-05 plan must include:**
- The shared-infra PR's primitive list
- The consumer PR list with explicit "depends on PR 1 merging" gates
- Pattern-claim accounting per consumer PR

### 3.4 Hybrid cohorts (cohort-5 forecast)

The cohort-5 PROPOSED scope (per V14 #2) bundles Property Mgmt cluster (independent-
parallel) WITH AP Aging (substrate-then-consumer). This is a hybrid:

- Property Mgmt FED PRs 1-3 are independent-parallel (each rebinds a different page)
- AP Aging Engineer PR 5 + FED PR 6 are substrate-then-consumer (cartridge ships first;
  page consumes)

The Stage-05 plan should treat the two layers separately:
- Layer 1: independent-parallel sub-cohort (PR 1-3)
- Layer 2: substrate-then-consumer sub-cohort (PR 5-6)

Per-layer Stage-05 sub-sections; no need to force unification.

---

## 4. Anti-patterns the methodology guards against

### 4.1 Anti-pattern A — Mis-scoped cohort whose "sub-PRs" are review-load shifting

**Symptom:** A Stage-05 plan fans out into 5 sub-PRs but each sub-PR has the same review
load as one unified PR would have had. The council has to read all 5 sub-PRs to understand
the cohort; no parallelization happens.

**Detection:** Q4 answer should distinguish — if council can review sub-PRs in parallel
sessions, fan-out is correct; if council must read them serially anyway, fan-out is
load-shifting.

**Fix:** unify the sub-PRs into one PR (with appropriate scope-trim) OR find a real
parallelization axis.

### 4.2 Anti-pattern B — Sub-cohorts that can't stand alone

**Symptom:** Each sub-PR depends on the others to function (main breaks if any sub-PR
ships alone). The cohort is one logical decision dressed as multiple PRs.

**Detection:** Q5 answer is No across the board.

**Fix:** ship as one PR. If the diff is too large, scope-trim.

### 4.3 Anti-pattern C — Cycle splits that aren't substrate-sequenced

**Symptom:** A PR amendment cycle (cycle-1 / cycle-2) is invoked but there's no cross-
repo dependency. The cycle structure is being used as a "let me try again" mechanism
rather than a substrate-sequencing structure.

**Detection:** Q2 answer is No; the cycle split is local to one PR.

**Fix:** ship as a single PR; if review identifies defects, fix them in-place (no
cycle structure needed). Cycle structure is for cross-repo sequencing only.

### 4.4 Anti-pattern D — Independent-parallel without a shared pattern

**Symptom:** Multiple PRs ship as a "cohort" but they don't share a pattern claim. The
cohort framing is administrative (review-batch grouping), not architectural.

**Detection:** Q3 answer is technically Yes (multiple surfaces) but no pattern-claim
unification.

**Fix:** ship as independent PRs without cohort framing. OR identify a candidate pattern
that the PRs would exercise (and use the cohort to ratify it).

---

## 5. Naming convention proposal

For consistency, Stage-05 plans should adopt one of three sub-cohort naming patterns
based on the shape:

| Shape | Naming convention | Example |
|---|---|---|
| Independent-parallel | `PR-Na`, `PR-Nb`, `PR-Nc`, `PR-Nd` (N is the cohort number; a/b/c/d are letters) | Cohort-2 PR 0a/0b/0c/0d |
| Substrate-then-consumer | `PR-1` (anchor) + `PR-2..N` (consumers) | Cohort-3 PR 1 (shared-infra) + PR 2-5 (pages) |
| Cross-repo wire-contract | `cycle-1`, `cycle-2` (within a single PR's amendment history) + cross-repo Engineer PR | Cohort-4 sunfish#71 cycle-1 + cycle-2 + signal-bridge#42 |

The Admiral ruling at sub-cohort dispatch should name the convention explicitly so the
agent dispatching the work knows what shape to expect.

---

## 6. Stage-05 template addition (proposed)

The methodology should be encoded in the Stage-05 template as a new "Section 3.5 —
Sub-cohort decomposition decision" with the 5-question heuristic. ONR proposes:

```markdown
## 3.5 — Sub-cohort decomposition decision

Before proceeding to Section 4 (Phases), answer the 5-question heuristic:

| # | Question | Answer | Fan-out signal? |
|---|---|---|---|
| Q1 | Will unified diff exceed ~1500 lines net? | YES / NO | YES → fan-out |
| Q2 | Cross-repo wire-contract sequencing? | YES / NO | YES → cycle-split |
| Q3 | Independent-but-paired surfaces with shared pattern? | YES / NO | YES → independent-parallel |
| Q4 | Unified PR exceeds council SPOT-CHECK budget? | YES / NO | YES → fan-out |
| Q5 | Each sub-PR standalone semantically? | YES / NO | NO → ship as single PR (or cycle-split) |

Recommended decomposition: [single PR / independent-parallel / substrate-then-consumer / cross-repo cycle-split / hybrid].

Sub-cohort naming convention: [PR-Na/Nb/Nc/Nd | PR-1 + PR-2..N | cycle-1 + cycle-2].

Cross-references: ADR 0093 (Stage-05 Adversarial Review); fleet conventions § sub-cohort
decomposition.
```

This is a ~20-line addition to the Stage-05 template; trivial to fold into ADR 0093
Rev 2 or Rev 3.

---

## 7. Open questions for Admiral

1. **Ratification timing for the 5-question heuristic.** Fold into ADR 0093 Rev 2
   alongside S05-1..-4 (per V14 #3), OR ratify as a separate Stage-05 template addendum
   (e.g., new ADR or `_shared/engineering/sub-cohort-decomposition-methodology.md`)?
   ONR recommends folding into ADR 0093 Rev 2 if subagent has bandwidth.

2. **Naming convention codification.** The PR-Na/Nb/Nc/Nd convention is well-established
   for cohort-2 style; the PR-1 anchor convention is informal for cohort-3 style; the
   cycle-1/cycle-2 convention is well-established for cohort-4 style. Should ONR file a
   small ADR amendment formally codifying the three conventions?

3. **Cohort-5 hybrid handling.** If cohort-5 = Property Mgmt + AP Aging hybrid (per V14
   #2), should the Stage-05 plan use the hybrid pattern with two layers (each treated
   independently)? ONR recommends yes; this is the cleanest framing.

4. **Sub-cohort retrospective cadence.** Should each sub-cohort (cohort-2 PR 0a-d; cohort-
   3 PRs 1-6; cohort-4 cycle-1/cycle-2) produce a per-shape retrospective informing future
   decompositions? ONR's experience suggests yes; the retrospective is 30-60 min and
   surfaces gaps cheaply.

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-20T13-15Z-engineer-cohort-2-pr-0a-and-0b-amend-together.md` (cohort-2 PR 0 sub-cohort framing)
2. `coordination/inbox/council-verdict-2026-05-20T12-25Z-security-engineering-cohort-2-pr-0a-spot-check.md` + 0b/0c/0d verdicts (cohort-2 PR 0 ratification evidence)
3. `coordination/inbox/admiral-ruling-2026-05-20T19-30Z-engineer-cohort-2-pr-0a-0b-0c-unified-green-attest.md` (cohort-2 closure)
4. `coordination/inbox/admiral-attest-2026-05-21T01-10Z-shipyard-64-pr-0d-dual-green-promote.md` (pattern-009-tenant-keying-retrofit promotion)
5. `shipyard/_shared/design/cohort-3/INDEX.md` (cohort-3 PR cluster shape; PR #116 MERGED 2026-05-22)
6. ONR V10 #3 `cohort-3-pr-cluster-consolidated-spec.md` (cohort-3 cluster framing)
7. `coordination/inbox/council-verdict-2026-05-22T1558Z-security-engineering-sunfish-71-cohort-4-fed-pr-1-spot-check.md` (cohort-4 cycle 0 RED)
8. `coordination/inbox/council-verdict-2026-05-22T1611Z-security-engineering-sunfish-71-cycle-1-reattest.md` (cohort-4 cycle 1 AMBER)
9. `coordination/inbox/council-verdict-2026-05-25T1312Z-security-engineering-sunfish-71-cycle-2-reattest.md` (cohort-4 cycle 2 GREEN)
10. `coordination/inbox/admiral-ruling-2026-05-22T22-30Z-cohort-4-client-side-tenant-assertion-cleanest-long-term.md` (Option B ruling; cycle split precedent)
11. `shipyard/_shared/engineering/standing-approved-patterns.md` § "Anything where the diff exceeds 1500 lines net" (~PR-size signal for Q1)
12. `feedback_prefer_cleanest_long_term_option` (memory note — informs Q5 + cycle split decisioning)
13. ADR 0093 Stage-05 Adversarial Review (target for methodology addition)

---

## 9. What ONR does next

V14 #4 deliverable complete. Files `onr-status-2026-05-25T1830Z-v14-4-sub-cohort-
decomposition-methodology-complete.md`. Proceeds to V14 #5 (pattern catalog drift audit
post-cohort-3).

— ONR, 2026-05-25T18:30Z
