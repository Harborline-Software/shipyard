# Security-engineering council subagent — UPF follow-up

**Authored by:** ONR (V7 batch item #7)
**Requester:** Admiral (per `admiral-directive-2026-05-22T12-45Z` item #7)
**Authored at:** 2026-05-22T13-45Z

---

## Purpose

Apply the **Universal Planning Framework** (`.claude/rules/universal-planning.md`) to the sec-eng-council subagent definition (MERGED at shipyard#107, 2026-05-21T??:??Z) to surface follow-up gaps before the protocol calcifies through cohort-5/6 use.

UPF Stage-0 discovery + Stage-1 plan-section checks + Stage-2 meta-validation are applied retroactively to the agent definition.

---

## 1. UPF Stage-0 (Discovery) — gaps surfaced retroactively

### 1.1 Existing Work check — partial credit

The sec-eng-council agent definition references ADR 0093 + ADR 0091 + ADR 0092 + ADR 0094 (audit substrate) — good. But does NOT reference:

- **pattern-009 formal pattern (Bridge endpoint + frontend binding)** — referenced obliquely as "cohort-2 precedent" but not by exact pattern ID
- **pattern-012 financial-write-path candidate** — referenced by name but not by status (candidate vs formal)
- **pattern-009-tenant-keying-retrofit (formalized 2026-05-22 at shipyard#103)** — not yet referenced; predates merge of #107 by hours

**Recommendation:** ADR 0093 should pin pattern catalog references by exact ID + status; sec-eng-council uses that pinned list. Drift will otherwise accumulate.

### 1.2 Feasibility — covered

The 8-item checklist is mechanical and the dispatch convention is well-defined. Feasibility GREEN.

### 1.3 Better Alternatives — UNDER-EXPLORED

The 8-item list was authored from cohort-2 + cohort-4 hand-off analysis. Was a smaller list (e.g., 4-item "primary risk classes") considered? UPF Stage-0 §0.9 (AHA Effect): could the council's value be captured with a 3-item structural review (cross-tenant + audit + auth-policy) + a free-form judgment slot?

**Recommendation:** ONR proposes a follow-up dispatch experiment: for 1-2 cohort-5 PRs, run a parallel 4-item simplified review vs the canonical 8-item review. Compare verdict density + catch rate. If 4-item catches ~90% of 8-item findings, the protocol can be lightened.

### 1.4 Official Docs (UPF "always for coding") — needs reference

The agent definition embeds the 8-item checklist in the agent prompt. Best practice per UPF: link to a versioned source-of-truth doc (e.g., `shipyard/_shared/engineering/security-engineering-checklist.md`) so updates flow to one place.

**Recommendation:** Extract 8-item checklist into a separate doc; agent definition links to it. Enables versioned evolution.

### 1.5 ROI analysis — implicit, not explicit

The dispatch cost (Sonnet 4.6 + medium effort + per-PR dispatch) was not benchmarked. At V5 #3 authoring time, ONR did not estimate token spend per dispatch. Post-V3 #1 cohort-4 + cohort-4 follow-on PRs (#92, #88), we have data:

- Estimated ~12-18k tokens per Stage-06 SPOT-CHECK dispatch (PR diff + 8 checks + verdict)
- Estimated ~25-35k tokens per Stage-05 review dispatch (hand-off + Adversarial Brief + 8 checks)

**Recommendation:** Add ROI section to agent definition with these benchmarks. Helps Admiral judge dispatch frequency (e.g., is sec-eng-council too expensive to run on EVERY substrate PR, or right-sized?).

### 1.6 Constraints — UNDER-DOCUMENTED

The agent definition does NOT document:

- Maximum PR size at which review remains tractable (estimated ~500-line diff before context fragments)
- What to do if dispatch exceeds context budget (split into multiple dispatches? escalate to Admiral?)
- What to do if the PR is rebased mid-review (the diff context changes mid-stream)

**Recommendation:** Add "Dispatch constraints" section to agent definition.

---

## 2. UPF Stage-1 (Plan) — sections present vs missing

### 2.1 Present in agent definition

| Section | Status |
|---|---|
| Context & Why | ✓ (frontmatter description) |
| Success Criteria | ✓ (verdict GREEN/AMBER/RED with definitions) |
| Phases | ✓ (Stage-05 vs Stage-06 SPOT-CHECK) |
| Verification | ✓ (8-item checklist) |

### 2.2 MISSING from agent definition

| UPF section | Gap | Recommendation |
|---|---|---|
| **Assumptions & Validation** | No "Assumption → VALIDATE BY → IMPACT IF WRONG" table | Add: e.g., "Assumes ADR 0093 8-item checklist is exhaustive for current threat model → VALIDATE BY cohort-5 retrospective → IMPACT IF WRONG: silently miss a new class of issue (e.g., supply-chain attack on substrate, key rotation race)" |
| **FAILED conditions / kill triggers** | No "agent should escalate to Admiral if X" trigger | Add: if 3+ consecutive AMBER reviews on the same PR family OR if a SPOT-CHECK takes >2x the typical dispatch time, escalate |
| **Resume Protocol** | What if dispatch timed out mid-review? | Add: file partial-verdict beacon at `coordination/inbox/council-verdict-partial-*.md`; Admiral re-dispatches |
| **Rollback Strategy** | What if council's own checklist has a bug? | Add: Admiral has ratification authority to override AMBER → GREEN if checklist gap is identified; codify ratification protocol |
| **Post-Completion Plan** | No cadence for checklist evolution | Add: ONR-led 6-cohort retrospective (after cohort-10) to update checklist based on findings; tied to ADR 0093 amendments |

---

## 3. UPF Stage-2 (Meta-validation) — checks applied

### 3.1 Delegation strategy clarity — GREEN

Sec-eng-council is dispatched ad-hoc by Admiral; explicit. Sonnet 4.6 + medium effort. No ambiguity.

### 3.2 Research needs identification — AMBER

The agent definition does not include a "research needed" escape hatch. If the agent encounters a novel issue (e.g., a new auth mechanism it hasn't seen), it cannot defer to ONR for research. Currently the agent must verdict (GREEN/AMBER/RED) within its checklist scope.

**Recommendation:** Add a 4th verdict type — **DEFER** — for issues that require ONR research before the council can produce a verdict. Or: explicit escalation protocol "if check N has no precedent, file `council-research-request-*.md` to ONR before continuing."

### 3.3 Review gate placement — GREEN

Stage-05 review (before Stage-06 implementation) and Stage-06 SPOT-CHECK (after implementation, before merge) are the right gates. Well-placed.

### 3.4 Anti-pattern scan (21 UPF patterns)

Applied to sec-eng-council:

| Anti-pattern | Triggered? | Notes |
|---|---|---|
| #1 (unvalidated assumptions) | YES | 8-item list assumed exhaustive; never validated |
| #2 (vague phases) | NO | Stage-05 vs Stage-06 are concrete |
| #3 (vague success criteria) | NO | GREEN/AMBER/RED are concrete |
| #5 (plan ending at deploy) | YES | No post-MERGE retrospective protocol |
| #6 (missing Resume Protocol) | YES | No timeout/resumption story |
| #7 (delegation without contracts) | NO | Beacon format pinned |
| #11 (zombie projects — no kill criteria) | YES | When does sec-eng-council retire? After how many cohorts? |
| #14 (wrong detail distribution) | PARTIAL | 8 checks are uniform-depth; some need more nuance (Check 1 cross-tenant) than others (Check 4 idempotency-key) |
| #15 (premature precision) | PARTIAL | "12-18k tokens per dispatch" estimate is illusory precision; needs actual benchmark |
| #18 (unverifiable gates) | YES | "Forward-watched concerns" gate has no binding force |

**Recommendation:** Address anti-patterns 1, 5, 6, 11, 14, 18 in next ADR 0093 amendment (probably ADR 0093 Rev 2).

### 3.5 Cold Start Test — PASSES

A fresh sec-eng-council session reading the agent definition can apply the checklist. Cold Start GREEN.

### 3.6 Plan Hygiene Protocol — partial

No version field on the agent definition. If the agent definition evolves (e.g., 8 checks → 9 checks), there is no mechanism to mark previously-applied verdicts as "applied with version N" vs "applied with version N+1." Drift risk.

**Recommendation:** Add `version: 1.0` to frontmatter. Bump on amendments.

### 3.7 Discovery Consolidation Check — fails

Each dispatch is stateless. If sec-eng-council discovers a recurring pattern across PRs (e.g., "every cohort-2-style PR misses the idempotency-key test"), there is no mechanism to consolidate that into the checklist.

**Recommendation:** Add a "Lessons learned" beacon shape: at the end of every dispatch, sec-eng-council can optionally file `council-lesson-*.md` with a single learning that ONR or Admiral can consume into the checklist.

---

## 4. Quality Rubric for sec-eng-council subagent

Per UPF rubric (C / B / A / Excellent):

- **C (Viable):** 5 CORE + 1 CONDITIONAL → sec-eng-council has 4/5 CORE + 0 CONDITIONAL → **D (Below Viable)**
- **B (Solid):** + Stage 0 + FAILED conditions + Confidence Level + Cold Start Test → adds FAILED conditions + Confidence Level → would reach B if §3 recommendations applied
- **A (Excellent):** + sparring + Review Checkpoints + Reference Library + Knowledge Capture → would reach A if Discovery Consolidation + post-cohort-10 retrospective added

**Verdict:** sec-eng-council currently rates **D-to-C** on UPF. The amendments in §1-§3 would bring it to **B** at minimum, **A** if the discovery-consolidation loop is closed.

---

## 5. Recommended next actions (route to Admiral)

Priority order:

1. **Add `version: 1.0` + FAILED-condition kill triggers + Resume Protocol** to agent definition — ~30-min edit
2. **Extract 8-item checklist to `shipyard/_shared/engineering/security-engineering-checklist.md`** — ~1h refactor
3. **Add DEFER verdict + council-research-request beacon protocol** — ~1h doc + ADR 0093 Rev 2
4. **Plan post-cohort-10 retrospective for checklist evolution** — calendar entry; ONR ownership
5. **Add token-spend benchmark to agent definition (after cohort-5 SPOT-CHECK data lands)** — ~30 min

Total: ~3-4 hours of cumulative agent-definition + ADR work, post-cohort-5.

---

## 6. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **ADR 0093 Rev 2 ownership** — Admiral authors ADR 0093 Rev 2 with these amendments? ONR provides scaffold per V4 #1+#2 precedent?
2. **DEFER verdict introduction** — meaningful addition OR scope creep? ONR recommends YES for novel-pattern PRs.
3. **post-cohort-10 retrospective ownership** — ONR-led OR Admiral-led? ONR recommends ONR with Admiral ratification per ADR-authoring-is-Admiral-territory precedent.

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T12-45Z` item #7
2. sec-eng-council subagent definition (`shipyard/.claude/agents/sec-eng-council.md`; merged at shipyard#107)
3. ADR 0093 Stage-05 Adversarial Review Protocol Amendment (shipyard#104)
4. Universal Planning Framework (`.claude/rules/universal-planning.md`)
5. V5 #3 sec-eng-council authoring (shipyard#90 MERGED)
6. V7 #5 Stage-05 retro scaffolding (shipyard#110 — same V7 batch)
7. Cohort-4 sec-eng-council first-pilot data (cohort-4 PRs)

---

## 8. What ONR does next

V7 #7 deliverable complete. **V7 partial-complete pattern fires** per V6 precedent: 5 of 7 items shipped (#6, #2, #5, #3, #7); heavy items #1 (cohort-4 FED PR-by-PR detail) + #4 (Engineer ladder PR-by-PR specs) deferred to V8 batch (each ~1 day; together too heavy for same session).

ONR files V7 partial-complete idle beacon. Awaits V8 dispatch.

— ONR, 2026-05-22T13:45Z
