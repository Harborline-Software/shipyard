# ADR 0093 Rev 2 — scaffolding for Admiral authoring

**Authored by:** ONR (V8 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-22T14-00Z` item V8 #4; per V7 #7 question #5 ruling Admiral authors final / ONR scaffolds)
**Authored at:** 2026-05-22T15-00Z

---

## Purpose

V7 #7 UPF follow-up rated sec-eng-council subagent **D-to-C on UPF**; recommended
amendments would bring it to **B-floor, A-target**. This scaffold packages those
UPF findings into prescriptive ADR-text drafts that Admiral consumes when
authoring ADR 0093 Rev 2.

**Scope boundaries:**
- ONR provides scaffold (this doc) — drafts ADR-text fragments + identifies decision
  points
- Admiral authors the final `shipyard/docs/adrs/0093-stage-05-adversarial-review.md`
  Rev 2 (per cerebrum rule: ADR authoring is Admiral/Captain territory)
- ONR routes Admiral decisions back through inbox per `feedback_onr_questions_via_inbox`

---

## 1. Current ADR 0093 Rev 1 context

ADR 0093 Rev 1 LIVE at shipyard#104 (MERGED 2026-05-21T15:48Z). Scope:

- Stage-05 Adversarial Review protocol amendment
- Adversarial Brief section in hand-offs (per V3 #4 prototype)
- sec-eng-council Stage-05 dispatch + Stage-06 SPOT-CHECK dispatch matrix
- 8-item review checklist (now embedded in sec-eng-council agent definition)

**Rev 1 gaps** (per V7 #7 UPF audit):
- Missing: Assumptions & Validation table
- Missing: FAILED conditions / kill triggers
- Missing: Resume Protocol (for dispatch timeouts)
- Missing: Discovery Consolidation loop (lessons learned across dispatches)
- Missing: Post-cohort retrospective protocol
- Anti-patterns triggered: #1, #5, #6, #11, #14, #18 (per V7 #7 §3.4)

---

## 2. Recommended Rev 2 amendments (with ADR-text drafts)

### 2.1 Amendment A — Version field + amendments protocol

**Why:** Anti-pattern #18 (unverifiable gates). Without versioning, "GREEN under
which checklist?" is ambiguous when the checklist evolves.

**ADR-text draft:**

```markdown
## Amendment A — Versioned checklist + amendments protocol (Rev 2)

The sec-eng-council 8-item checklist (currently embedded in
`shipyard/.claude/agents/sec-eng-council.md`) carries a version field:

> sec-eng-council checklist: **version 2.0** (effective 2026-MM-DD per ADR 0093 Rev 2)

Council verdicts cite the checklist version in their beacon frontmatter:

```yaml
---
type: council-verdict
council: security-engineering
checklist-version: 2.0
...
---
```

Amendments to the checklist follow a standing protocol:
1. ONR drafts amendment scaffold (this ADR's authoring pattern)
2. Admiral authors final amendment text into the ADR
3. Version bumps (1.0 → 2.0 → 2.1 etc.)
4. Effective date pinned in ADR header
5. Council operates under the amended checklist from effective date forward
```

**Decision point for Admiral:** version-bumping convention (semver vs flat-N)?

### 2.2 Amendment B — Assumptions & Validation table

**Why:** Anti-pattern #1 (unvalidated assumptions). Rev 1 has implicit assumptions
that need to be explicit and validatable.

**ADR-text draft:**

```markdown
## Amendment B — Assumptions & Validation (Rev 2)

| Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|
| The 8-item checklist is exhaustive for the current threat model | Cohort-10 post-pilot retrospective (per V8 #6 scaffold) — measure catch rate of issues NOT covered by the 8 items | Silently miss a new class of issue (e.g., supply-chain attack, key rotation race, GDPR-style data-locality requirement) |
| Stage-05 Adversarial Brief catches ~70-80% of issues that would AMBER at Stage-06 | Cohort-4 pilot retrospective (per V7 #5 scaffold) | Stage-05 protocol over-promises; protocol value-proposition needs revision |
| sec-eng-council dispatch cost is ~12-18k tokens per SPOT-CHECK + ~25-35k tokens per Stage-05 | QM Phase 0 instrumentation per V3 addendum #9-#11 | Dispatch frequency assumption breaks; may need to throttle dispatch |
| Admiral-arranged dispatch SLA (30 min) is sustainable | QM daemon SLA-breach detection per fleet-conventions §SPOT-CHECK dispatch SLA | Stalled SPOT-CHECKs let bugs through (per cohort-1 PR 3 incident that motivated SLA) |
| Council verdicts are reproducible across dispatch instances (stateless) | Spot-audit: dispatch the SAME PR twice; compare verdicts | Verdict drift undermines trust; may need state-carrying verdicts |
```

**Decision point for Admiral:** which assumptions to keep explicit vs deferred?

### 2.3 Amendment C — FAILED conditions / kill triggers

**Why:** Anti-pattern #11 (zombie projects — no kill criteria). Rev 1 has no
escalation triggers.

**ADR-text draft:**

```markdown
## Amendment C — FAILED conditions / kill triggers (Rev 2)

The sec-eng-council escalates to Admiral for re-scoping when ANY of:

1. **Repeat-AMBER trigger:** 3+ consecutive AMBER verdicts on the same PR family
   within a single cohort. Indicates the Adversarial Brief is missing a recurring
   class of issue.
2. **Dispatch-density-blowup trigger:** SPOT-CHECK dispatch exceeds 2x typical
   wall-clock (e.g., a 30-min median SPOT-CHECK takes >1h). Indicates either
   PR size has exceeded reviewability, OR the checklist has accumulated cruft
   that needs trimming.
3. **DEFER-rate-spike trigger:** DEFER verdicts exceed 15% of dispatches in a
   cohort (baseline 5-10% per V8 #5 spec). Indicates Admiral routing has
   degraded; non-sec-eng PRs are being mis-dispatched.
4. **Verdict-drift trigger:** spot-audit reveals GREEN/AMBER inconsistency on the
   same PR across two dispatches. Indicates stateless-verdict assumption is
   breaking; council needs state.
5. **Tech-corps-feedback trigger:** Engineer or FED files 3+ council-feedback
   beacons in a single cohort indicating false-positives or unclear amendments.

When triggered: council files `council-escalation-*.md` to Admiral; Admiral
authors an Rev 3 amendment OR rescopes the council.
```

**Decision point for Admiral:** thresholds (3, 2x, 15%, 3) reasonable, or adjust?

### 2.4 Amendment D — Resume Protocol

**Why:** Anti-pattern #6 (missing Resume Protocol). Rev 1 doesn't say what
happens if a sec-eng-council dispatch times out or hits context budget mid-review.

**ADR-text draft:**

```markdown
## Amendment D — Resume Protocol (Rev 2)

If a sec-eng-council dispatch encounters one of:
- Context-budget exhaustion mid-review (>75% context used, checklist incomplete)
- Tool-call timeout
- Network failure on PR diff fetch
- Token budget exhaustion (per ROI assumption in §B)

the council MUST file a **partial verdict beacon**:

```yaml
---
type: council-verdict-partial
council: security-engineering
checklist-version: <N>
pr: <PR URL>
checks-completed: <e.g., "1, 2, 3, 5"; lists which checks ran>
checks-pending: <e.g., "4, 6, 7, 8">
verdict-so-far: <GREEN | AMBER | RED — based on completed checks only>
reason-for-partial: <"context exhausted at 78%" | "tool timeout on diff fetch" | etc.>
---
```

Admiral re-dispatches the council with the partial verdict as context for the
follow-up dispatch. The follow-up completes the pending checks and files a final
verdict that merges the partial + new findings.
```

**Decision point for Admiral:** partial-verdict beacon shape — proposed inline, or
extract to separate file?

### 2.5 Amendment E — DEFER verdict integration (per V8 #5)

**Why:** Per V7 #7 question #6 ruling APPROVED, DEFER added as 4th verdict.
V8 #5 (shipyard#114) ships the agent-definition + design spec. Rev 2 of ADR 0093
SHOULD reference DEFER in the protocol layer.

**ADR-text draft:**

```markdown
## Amendment E — DEFER verdict (Rev 2)

Per V8 #5 (shipyard#114), the council carries a 4th verdict: **DEFER**.

DEFER fires when ALL of:
1. No item in the 8-item checklist is materially exercised by the PR
2. Other councils (or "skip-review") more appropriately serve the PR
3. The dispatch was not triggered by a specifically-named security relevance

DEFER is NOT a failure verdict; it routes the PR to the named council.

See `icm/01_discovery/research/sec-eng-council-defer-verdict-spec.md` (V8 #5)
for the complete DEFER protocol including decision-tree, canonical use cases,
and beacon shape.
```

**Decision point for Admiral:** inline the DEFER decision-tree from V8 #5 spec
into ADR 0093 Rev 2, or reference by link?

### 2.6 Amendment F — Discovery Consolidation loop

**Why:** Anti-pattern (UPF §3.7 Stage-2 check). Rev 1's stateless dispatch means
recurring findings don't compound into checklist evolution.

**ADR-text draft:**

```markdown
## Amendment F — Discovery Consolidation loop (Rev 2)

The council can OPTIONALLY file a `council-lesson-*.md` beacon at the end of any
dispatch when a recurring pattern is observed:

```yaml
---
type: council-lesson
council: security-engineering
checklist-version: <N>
pattern: <one-line description>
recurrence-count: <how many times this dispatch has seen the pattern>
recommendation: <"add to checklist as Check 9" | "update Check 3 to also probe X" | etc.>
---

## Description
<paragraph: what the recurring pattern is and why it matters>

## Recommendation
<paragraph: how the checklist should evolve to catch this proactively>
```

ONR consumes `council-lesson-*` beacons during retrospective authoring (per V7 #5
cohort-4 retro scaffold + V8 #6 post-cohort-10 scaffold). When 3+ lessons coalesce
into a common amendment, ONR drafts an Rev N+1 scaffold; Admiral ratifies.

This closes the discovery-consolidation loop: stateless per-dispatch + state-
accumulating across cohorts.
```

**Decision point for Admiral:** opt-in (council files when it chooses) vs
required (every dispatch files a lesson, even if "no new lesson observed")?

### 2.7 Amendment G — Post-cohort retrospective cadence

**Why:** Anti-pattern #5 (plan ending at deploy). Rev 1 has no protocol for
checklist evolution beyond ad-hoc.

**ADR-text draft:**

```markdown
## Amendment G — Post-cohort retrospective cadence (Rev 2)

The sec-eng-council checklist is reviewed at:

- **Per-cohort touchpoint (lightweight):** ONR's Stage-05 retro per cohort (per
  V7 #5 scaffold) captures Stage-05 Brief vs Stage-06 verdict deltas
- **Cohort-10 retrospective (heavyweight):** ONR-led, Admiral-ratified
  retrospective per V8 #6 scaffold; evaluates the checklist against ~6+ cohorts
  of data; proposes Rev N+1 amendments
- **Triggered retrospective:** any FAILED-condition (§C) automatically schedules
  an out-of-cycle retro within 1 cohort

Retrospective deliverables:
- Catch rate per check (which checks fired most? least?)
- DEFER frequency vs prediction (~5-10% baseline per V8 #5)
- Council token-cost vs prediction
- Recurring lesson patterns (consolidated from §F beacons)
```

**Decision point for Admiral:** cadence — per-cohort + cohort-10 + triggered?
Or just cohort-10?

### 2.8 Amendment H — Extract checklist to versioned doc

**Why:** UPF Stage-0 §1.4. Currently the 8-item checklist is embedded in the
agent definition. Versioning is easier if the checklist is extracted.

**ADR-text draft:**

```markdown
## Amendment H — Versioned checklist doc (Rev 2)

The 8-item checklist is extracted to:

`shipyard/_shared/engineering/security-engineering-checklist.md`

Carries:
- Version header (current: 2.0)
- Effective-date header
- Per-check section with Stage-05 + Stage-06 specifications
- Change log (Rev 1 → Rev 2 deltas, etc.)

The sec-eng-council agent definition references the checklist by path and
version; updates to the checklist flow through PR + Admiral ratification, not
through agent-definition edits.

Engineer / FED can read the checklist file to anticipate review findings
without needing to read the entire ADR.
```

**Decision point for Admiral:** path — `_shared/engineering/` (proposed) vs
elsewhere (e.g., `docs/security/`)?

---

## 3. Amendments NOT recommended in Rev 2 (deferred to Rev 3+)

Items that ONR considered but defers:

- **Token-cost benchmark** — wait for cohort-5/6 SPOT-CHECK data (real, not estimated)
- **Cross-council coordination** — Stage-05 / Stage-06 split with .NET-architect council
  for Pattern-013 (cartridge-read-via-POST) cases; defer until 2nd-instance emerges
- **Roslyn-analyzer for checklist items** — long-term ambition; not Rev 2

---

## 4. ADR 0093 Rev 2 — recommended structure

When Admiral authors `shipyard/docs/adrs/0093-stage-05-adversarial-review.md` Rev 2:

```markdown
# ADR 0093 — Stage-05 Adversarial Review Protocol (Rev 2)

**Status:** Accepted (Rev 2 — 2026-MM-DD)
**Supersedes:** Rev 1 (shipyard#104)
**Authored by:** Admiral
**Effective:** YYYY-MM-DD

## Rev 2 changes from Rev 1

- Amendment A: Versioned checklist + amendments protocol
- Amendment B: Assumptions & Validation table
- Amendment C: FAILED conditions / kill triggers
- Amendment D: Resume Protocol for dispatch interruption
- Amendment E: DEFER verdict integration (per V8 #5)
- Amendment F: Discovery Consolidation loop (council-lesson beacons)
- Amendment G: Post-cohort retrospective cadence
- Amendment H: Versioned checklist extracted to _shared/engineering/

## Status quo (preserved from Rev 1)

- Stage-05 Adversarial Brief in hand-offs (V3 #4 prototype)
- sec-eng-council Stage-05 + Stage-06 SPOT-CHECK dispatch matrix
- 8-item checklist (now extracted per Amendment H)
- Verdict types: GREEN, AMBER, RED, DEFER (DEFER added per Amendment E)

[... per-amendment sections inserted here from §2 of this scaffold ...]
```

---

## 5. Concurrent PR strategy

ONR recommends Rev 2 ship as:

1. **ADR 0093 Rev 2 text PR** (Admiral authors) — full ADR Rev 2 document
2. **Checklist extraction PR** (Admiral or QM) — `_shared/engineering/security-
   engineering-checklist.md` with version 2.0 header
3. **Agent definition update PR** (ONR can author if Admiral delegates) — agent
   def references extracted checklist + new amendments

Three PRs OR one bundled PR — Admiral choice. ONR recommends bundled for cohesion
(per V6 cerebrum learning: avoid splitting tightly-coupled changes).

---

## 6. Cerebrum / auto-memory updates

When Rev 2 lands:

**Cerebrum entries (add to fleet `.wolf/cerebrum.md`):**
- Sec-eng-council Rev 2 protocol effective YYYY-MM-DD
- DEFER frequency baseline 5-10% per cohort
- FAILED-condition thresholds (3-AMBER, 2x-dispatch-time, 15%-DEFER, etc.)

**Auto-memory entries (add to ONR's memory dir):**
- `feedback_sec_eng_council_rev_2_protocol.md`
- `feedback_sec_eng_council_failed_conditions.md`

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T14-00Z` item V8 #4
2. V7 #7 sec-eng-council UPF follow-up (shipyard#112) — source of amendment gaps
3. V7 #5 Stage-05 retro scaffold (shipyard#110) — retro cadence precedent
4. V8 #5 DEFER verdict spec (shipyard#114) — Amendment E source
5. V8 #6 post-cohort-10 retro scaffold (shipyard#115) — cadence pattern
6. ADR 0093 Rev 1 (shipyard#104 MERGED) — base document
7. sec-eng-council subagent definition (shipyard#107 MERGED)
8. Universal Planning Framework (`.claude/rules/universal-planning.md`)

---

## 8. Open questions for Admiral

Routed via this scaffold; ONR has no further work pending until Admiral authors:

1. Version-bumping convention (semver vs flat-N)?
2. Assumptions table — keep all 5 explicit, or trim?
3. FAILED-condition thresholds (3/2x/15%/3) — adjust?
4. Partial-verdict beacon shape — inline in ADR or separate file?
5. DEFER decision-tree — inline in ADR 0093 Rev 2 or reference V8 #5?
6. council-lesson beacon — opt-in vs required?
7. Retrospective cadence — per-cohort + cohort-10 + triggered, or just cohort-10?
8. Checklist extraction path — `_shared/engineering/` (proposed) vs `docs/security/`?
9. Concurrent PR strategy — 3 PRs vs 1 bundled?

---

## 9. What ONR does next

V8 #4 deliverable complete. **V8 batch partial-complete** fires per V6 + V7
precedent: 4 of 6 V8 items shipped (#0 fold + #5 DEFER + #6 retro scaffold +
#3 onboarding + #4 ADR 0093 Rev 2 scaffold). #1 + #2 (each ~1 day) deferred to
V9 separate-session focuses per V6 cerebrum learning.

ONR files V8 partial-complete idle beacon. Awaits V9 dispatch.

— ONR, 2026-05-22T15:00Z
