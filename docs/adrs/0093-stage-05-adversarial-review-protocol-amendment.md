---
id: 93
title: Stage-05 Adversarial Review Protocol Amendment
status: Proposed
date: 2026-05-21
proposed-date: 2026-05-21
accepted-date: null
author: Admiral
tier: governance
pipeline_variant: fleet-protocol-amendment

concern:
  - fleet-governance
  - security
  - test-coverage
  - council-dispatch
  - stage-05-handoff

enables:
  - earlier-adversarial-review
  - stage-05-coverage-authoring
  - test-eng-council-subagent-type
  - reduced-stage-06-spot-check-rework
  - happy-path-bias-pre-emption

composes:
  - 69  # ADR Authoring Discipline (council attestation requirement; this ADR uses dual-council attestation itself)
  - 91  # ITenantContext Divergence Resolution (R3 Adversarial Brief precedent: cohort-4 audit-trail viewer cites ADR 0091 in Decision 1/7)
  - 92  # Substrate Tenant-Keyed Repository Contract Pattern (R3 Adversarial Brief precedent: cohort-4 audit-trail viewer cites §A3 / §A6 in Decisions 2/5)
  - 94  # IAuditEventReader (cohort-4 anchor; first canonical Adversarial Brief integrated in shipyard#81)

extends: []

supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # Admiral-scope governance ADR; protocol amendment; ratified by CIC + dual-council attestation before Accepted

amendments: []
---

# ADR 0093 — Stage-05 Adversarial Review Protocol Amendment

**Status:** Proposed (will move to Accepted after dual-council attestation + CIC ratification per ADR 0069)
**Date:** 2026-05-21 (Proposed)
**Resolves:** Cohort-1 R3 retrospective (cerebrum entry 2026-05-21 — "SPOT-CHECK timing race") + UPF audit recommendation in `coordination/inbox/onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md` (AHA candidate: Stage-05 adversarial review extension). Operationalizes "adversarial to happy path" at the gate where the bias forms (Stage-05 planning), not at the gate where it is detected (Stage-06 PR-open).
**Council inputs:** Pending — this ADR is itself the protocol amendment that mandates dual-council attestation at Stage-05 going forward; the ADR's own attestation cycle (dispatch on PR-open at shipyard) is the first instance of the new protocol exercised against itself.

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-21 | Admiral | Initial draft per `coordination/inbox/admiral-directive-2026-05-21T12-25Z-stage-05-adversarial-review-extension-plan.md` Phase 1. Phase 0 instrumentation complete (3 QM deliverables; see §Context). Adversarial Brief template prototype landed at `icm/02_architecture/adversarial-brief-template-prototype.md` (shipyard#78). Test-eng-council research landed at `icm/02_architecture/test-eng-council-subagent-definition-research.md` (shipyard#90). First canonical Adversarial Brief integrated in cohort-4 audit-trail viewer Stage-05 hand-off (shipyard#81). Status: Proposed pending dual-council attestation. |

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| ADR 0069 (ADR Authoring Discipline) | Existing | yes — `shipyard/docs/adrs/0069-adr-authoring-discipline.md` (mandates pre-merge council requirement for substrate/governance ADRs) |
| ADR 0091 (ITenantContext Divergence Resolution) | Existing | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (referenced from Adversarial Brief prototype Decision 1) |
| ADR 0092 (Substrate Tenant-Keyed Repository Contract Pattern) | Existing | yes — `shipyard/docs/adrs/0092-substrate-tenant-keyed-repository-contract.md` §A3 / §A6 (referenced from Adversarial Brief prototype Decision 7) |
| ADR 0094 (IAuditEventReader) | Existing | yes — `shipyard/docs/adrs/0094-i-audit-event-reader.md` (cohort-4 anchor; first canonical Adversarial Brief consumer) |
| `_shared/engineering/standing-approved-patterns.md` | Existing | yes — fleet-wide standing-pattern catalog; this ADR coordinates with the catalog but does not modify it |
| `.claude/rules/fleet-conventions.md` § "SPOT-CHECK dispatch SLA" | Existing | yes — fleet-conventions; this ADR extends the dispatch protocol to include a Stage-05 trigger alongside the existing Stage-06 trigger |
| `coordination/inbox/onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md` | Existing | yes — UPF audit recommending this protocol amendment as AHA candidate |
| `coordination/inbox/admiral-directive-2026-05-21T12-25Z-stage-05-adversarial-review-extension-plan.md` | Existing | yes — Admiral Phase 0/1/2 plan ratified by CIC at 12:20Z |
| `coordination/inbox/qm-status-2026-05-21T2030Z-spot-check-stage-05-catchability-audit.md` | Existing | yes — Phase 0 evidence #1 (25% of findings Stage-05-catchable) |
| `coordination/inbox/qm-status-2026-05-21T2035Z-test-coverage-gaps-retro.md` | Existing | yes — Phase 0 evidence #2 (2 documented test-coverage gaps in 14-day window) |
| `coordination/inbox/qm-status-2026-05-21T2040Z-sec-eng-dispatch-latency-retro.md` | Existing | yes — Phase 0 evidence #3 (median 8 min; p95 ~28 min; 0 SLA violations post-rule) |
| `icm/02_architecture/adversarial-brief-template-prototype.md` (shipyard#78) | Existing | yes — Adversarial Brief template + 8-bullet worked example for cohort-4 audit-trail viewer |
| `icm/02_architecture/test-eng-council-subagent-definition-research.md` (shipyard#90) | Existing | yes — test-eng-council role definition feeding this ADR's §"Test-eng-council subagent" section |
| `icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md` (shipyard#81) | Existing | yes — first canonical Stage-05 hand-off carrying an Adversarial Brief |
| Cerebrum entry "Cohort-1 R3 retrospective" (2026-05-21) | Existing | yes — `.wolf/cerebrum.md`; SPOT-CHECK timing race motivated the dispatch-SLA rule and this ADR |
| Cerebrum entry "Inbox-directed agents proceed continuously" (2026-05-19) | Existing | yes — `.wolf/cerebrum.md`; informs the parallel-dispatch shape (sec-eng + test-eng fire independently) |
| `sec-eng-council` subagent (existing) | Existing | yes — dispatched via Admiral `Agent` call; Opus 4.7 + xhigh |
| `.NET-architect-council` subagent (existing) | Existing | yes — dispatched via Admiral `Agent` call; Opus 4.7 + xhigh |
| `test-eng-council` subagent | Introduced by this ADR | no — definition shipped under shipyard `.claude/agents/test-eng-council.md` in Step 2 of this ADR's roll-out |

§A0 totals: 18 cited references. Existing & verified: 17. Introduced by this ADR: 1 (the `test-eng-council` subagent definition file).

---

## Context

The fleet's adversarial review of cyber-security and test-coverage concerns currently fires at **Stage-06 PR-open** via Admiral-dispatched `sec-eng-council` (and where relevant `.NET-architect-council`) SPOT-CHECK. The cohort-1 R3 retrospective (cerebrum entry 2026-05-21) identified that adversarial review at this stage is *too late* in two distinct ways:

1. **Timing race against auto-merge.** Per fleet ruleset (`strict_required_status_checks_policy: false`; cerebrum entry 2026-05-18), auto-merge fires on CI-green alone. SPOT-CHECK is an Admiral process gate, not a GitHub-enforced check. The cohort-1 PR 3 case (signal-bridge#11) — an 8-hour gap between Ready-flip and SPOT-CHECK dispatch — exposed the failure mode: a PR can ship before its security review lands. The 2026-05-18 dispatch-SLA rule (30 minutes from Admiral consumption) closed the most acute symptom (subsequent post-rule dispatches median 8 min, p95 ~28 min, 0 SLA violations per `qm-status-2026-05-21T2040Z-sec-eng-dispatch-latency-retro.md`), but the structural cause — adversarial review running after the build is already PR-opened — remained.

2. **Happy-path bias is baked into Stage-05 planning.** The Engineer (or whoever authors the Stage-05 implementation plan) optimizes for the design they have just authored. By Stage-06 PR-open, the build is committed to a specific shape; any adversarial finding at SPOT-CHECK either lands as a forward-watch (deferred to a follow-on PR) or forces rework after CI has already passed. The bias forms at Stage-05; the review fires at Stage-06; the gate is one stage too late.

The UPF audit on the CIC-proposed "security officer" agent (`coordination/inbox/onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md`) examined this gap and surfaced an **AHA candidate**: instead of adding a standing agent to the org chart, **promote the existing sec-eng-council dispatch from per-PR-open to per-Stage-05-hand-off**, and extend the Stage-05 hand-off template with an "Adversarial Brief" section that structures the worst-case interpretation pass. CIC ratified the AHA candidate at 2026-05-21T12:20Z. The Phase 0 instrumentation evidence (three QM retrospective deliverables, all shipped) validated the premise quantitatively. This ADR is the Phase 1 deliverable.

### Phase 0 evidence (instrumentation summary)

Per Admiral directive `coordination/inbox/admiral-directive-2026-05-21T12-25Z-stage-05-adversarial-review-extension-plan.md`, QM completed three retrospective audits before this ADR was authored. Numbers turn the AHA candidate from "intuition" to "evidence-backed protocol amendment."

**Evidence #1 — Stage-05 catchability of recent SPOT-CHECK findings** (`qm-status-2026-05-21T2030Z-spot-check-stage-05-catchability-audit.md`).

Of the 10 newest sec-eng + .NET-architect council verdicts (all dated 2026-05-21; covering shipyard#69, shipyard#71, shipyard#86, shipyard#100, shipyard#102, signal-bridge#31), QM tagged each finding as "Stage-05-catchable" or "Stage-06-only-addressable." Result:

- **Approximately 14 of 56 distinct findings (25%) were Stage-05-catchable** — meaning the design decision or specification gap was fully resolvable with information available at architecture-and-planning time, before any code was written.
- The catchable findings cluster into three types: **interface/contract completeness gaps** (highest frequency — e.g., cursor predicate semantics not specified, DI lifetime constraint not documented, sentinel-TenantId guard not called out); **structural/architectural choices** (medium frequency — e.g., helper class placement, severity-level framing); **temporal dependencies not surfaced in the spec** (lower frequency — e.g., "Bridge is sole emission layer until cohort-2 Step 2 merges" timing facts).
- The remaining 75% of findings (implementation-detail forward-watches, recursion-safety properties, CI/runtime correctness) are not Stage-05-catchable and remain the legitimate domain of Stage-06 SPOT-CHECK.

**Implication.** A focused Stage-05 review pass targeting the "interface/contract completeness" category (highest-density catchable type) would pay the clearest dividend. The Adversarial Brief template (§"Decision" below) operationalizes this finding by structuring authorship around the worst-case interpretation of each load-bearing design decision — exactly the question that surfaces interface/contract completeness gaps.

**Evidence #2 — Test-coverage gaps in the 14-day window** (`qm-status-2026-05-21T2035Z-test-coverage-gaps-retro.md`).

Of the ~127 PRs merged across the fleet (shipyard 40, sunfish 29, signal-bridge 26, flight-deck 25, tender 7) in the last 14 days, QM identified **2 unresolved test-coverage gaps** explicitly flagged by council verdicts at SPOT-CHECK but unaddressed post-merge:

- **signal-bridge#11 — Bridge-layer `WorkOrderCreated` audit emission test.** The block-level `WorkOrderAuditEmissionTests` exercised the audit ctor directly, bypassing the Bridge DI shape. sec-eng SPOT-CHECK flagged the gap; no follow-up PR is visible in inbox or archive. The gap compounded with the cohort-1 R3 retrospective (the same PR also missed its SPOT-CHECK window by 8 hours).
- **signal-bridge#29 — FinancialEndpoints unit tests entirely missing.** No `tests/Sunfish.Bridge.Tests.Unit/Financial/` directory exists in the worktree; WorkOrdersEndpoint has 2 test files; FinancialEndpoints has none. sec-eng flagged as a quality finding (non-blocking) but no follow-on test PR is visible.

The pattern is consistent: **Bridge handler files have been extended (WorkOrders in cohort-1, Financial in cohort-2) faster than their test coverage has been updated.** Test-coverage authoring currently has no clear owner — Engineer authors implementation tests; sec-eng-council flags coverage gaps but does not author tests; QM tracks gaps but does not close them. This is the gap that motivates the parallel `test-eng-council` subagent type (§"Test-eng-council subagent" below) — a triggered role that reviews test-coverage scaffolds at the Stage-05 gate, before implementation begins.

**Evidence #3 — sec-eng-council dispatch latency** (`qm-status-2026-05-21T2040Z-sec-eng-dispatch-latency-retro.md`).

For the 8 sec-eng-council SPOT-CHECK verdicts with computable dispatch anchors (post-SLA-rule era; 2026-05-18 onward):

- **Median latency: ~8 minutes**
- **P95 latency: ~28 minutes**
- **Maximum observed (post-rule): ~30 minutes (borderline)**
- **SLA violations (>30 min, post-rule era): 0**
- **Pre-rule outlier: cohort-1 PR 3 at 8 hours (the gap that motivated the SLA rule).**

**Implication.** The existing sec-eng-council dispatch infrastructure operates well within the 30-minute SLA at current load. Adding a Stage-05 dispatch event (~1 additional fire per cohort hand-off, +20% sec-eng volume) is bounded; the SLA headroom is robust enough to absorb the additional load without restructuring the dispatch mechanism. The infrastructure scales; the protocol amendment is feasible at current capacity.

### Validation evidence (qualitative)

Three council verdicts in the 72 hours preceding this ADR validated the underlying thesis — that adversarial review catches real flaws — even though they fired at Stage 06 rather than Stage 05:

- **shipyard#86 (ADR 0094 ADR-text SPOT-CHECK)** — .NET-architect issued 3 AMBER findings (kernel-tier-reads-marker-free precedent missing from Decision drivers; cursor tuple-compare predicate not explicit; InMemoryAuditEventReader DI lifetime Transient footgun). All three are interface/contract completeness gaps a Stage-05 review pass would have flagged.
- **signal-bridge#31 (audit emission retrofit SPOT-CHECK)** — sec-eng issued 1 forward-watch on timing side-channel (await-before-return). A Stage-05 Adversarial Brief asking "what if the await is observable to a caller measuring response time?" would have surfaced the concern at design time.
- **shipyard#102 (WhereTenant SPOT-CHECK)** — sec-eng issued 2 AMBER (missing IsSystemSentinel guard on TenantId overload; WhereTenant callsite audit deferral). Both are contract-completeness gaps; both Stage-05-catchable.

Three verdicts; three real findings; all Stage-05-catchable in retrospect. The pattern is robust enough to motivate the protocol change. The Phase 4 decision gate (§"Phase 4 decision gate" below) operationalizes the next round of validation — pilot on cohort-4; measure Stage-05 finding rate against Stage-06 finding rate; ratify or retire.

---

## Decision drivers

- **Cohort-1 R3 retrospective.** SPOT-CHECK timing race (8h gap on signal-bridge#11) exposed the structural cost of Stage-06-only adversarial review. The 2026-05-18 dispatch-SLA rule closed the latency symptom; this ADR closes the timing-stage cause.

- **UPF audit recommendation.** The CIC-proposed standing "security officer" agent was rejected per UPF anti-pattern audit (`onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md`) on 9 of 21 anti-patterns. The AHA candidate (this protocol amendment) addresses the genuine gap (timing of adversarial review) at the cost of one documentation amendment + one new triggered subagent type, instead of one new standing agent at Opus + xhigh. UPF-discipline precedent is preserved (Prevent-over-Detect; Trigger-over-Standing).

- **Phase 0 evidence (25% Stage-05-catchable).** The 25% number is the load-bearing data point. It is large enough to justify the protocol amendment (1 in 4 SPOT-CHECK findings is preventable at Stage-05; for a 5-PR cohort this is 1-2 findings per cohort earlier) and small enough to confirm that Stage-06 SPOT-CHECK remains the dominant gate (75% of findings are not Stage-05-catchable and continue to require runtime/CI evidence). Both gates carry; neither is redundant.

- **Test-coverage authoring has no current owner.** Evidence #2 documents two unresolved gaps; the pattern is structural (Engineer authors implementation tests; sec-eng-council flags coverage gaps but does not close them; QM tracks but does not author). The Stage-05 gate is the natural intercept point: scaffolds are mandatory before implementation begins; a triggered test-eng-council subagent at Sonnet medium effort can audit the scaffold against the hand-off's acceptance criteria.

- **Dispatch infrastructure scales.** Evidence #3 confirms 0 SLA violations post-rule with median 8-minute latency. Adding one more dispatch event per cohort hand-off (+20% sec-eng volume) is within current capacity; no infrastructure rebuild required.

- **Adversarial-to-happy-path discipline operationalized at the right gate.** The Engineer's bias is toward the plan they have just authored. The Adversarial Brief is a structured AP-10 inoculation (first-idea-unchallenged failures surfaced at authorship). Sec-eng + test-eng councils review the brief alongside the design; gaps surface BEFORE Stage-06 implementation begins.

- **Zero-cost reversibility.** Stage-05 adversarial review extension is reversible at zero cost. If Phase 4 gate fails, revert the protocol amendment + retire the test-eng-council subagent type. No in-flight code dependencies; documentation-only change.

- **Substrate-canonical precedent.** ADR 0091 R2 and ADR 0092 Rev 2 established the dual-council attestation discipline (sec-eng + .NET-architect at substrate-shaping ADRs). This ADR extends that discipline one stage earlier — the substrate-shaping intent is identified at Stage-05; the attestation cycle is concentrated where the design judgment is made.

- **Council-fresh-perspective preserved.** The triggered (per-event) dispatch model retains sec-eng-council's per-task fresh-perspective value. Each Stage-05 dispatch is a fresh subagent with no memory of prior dispatches. Standing-continuity (which the rejected security-officer proposal would have introduced) would erode that.

- **Existing PR-open SPOT-CHECK retains its role.** Stage-06 SPOT-CHECK is NOT removed. It is lightened — the council verifies that Stage-05 amendments were applied and checks for runtime evidence (CI green; integration test outputs; observability signals). The two gates carry distinct responsibilities: Stage-05 reviews intent; Stage-06 reviews execution.

---

## Considered options

### Option A — Status quo (Stage-06-only SPOT-CHECK)

Continue the current pattern: sec-eng + .NET-architect councils dispatch only at Stage-06 PR-open. The 2026-05-18 dispatch-SLA rule remains the primary mitigation for the cohort-1 R3 timing race; no protocol change.

**Pro:**
- Zero authoring cost.
- No new subagent definitions; no protocol amendments.
- Existing infrastructure unchanged.

**Con:**
- **Phase 0 evidence (25% Stage-05-catchable) is unaddressed.** One in four findings continues to surface at the gate where rework is most expensive (after CI has passed).
- **Test-coverage authoring gap (Evidence #2) is unaddressed.** Two open gaps in the 14-day window; no mechanism to close them.
- **The cohort-1 R3 retrospective's structural cause (adversarial review at the wrong stage) is unaddressed.** The SLA rule closes the latency symptom but not the cause.
- **Happy-path bias remains baked into Stage-05.** The Engineer optimizes for the plan just authored; adversarial framing arrives one stage too late to influence the design.

**Verdict: rejected.** The cohort-1 R3 retrospective + Phase 0 evidence + open test-coverage gaps collectively make status quo not a viable disposition.

### Option B — Standing security officer agent (the original CIC proposal)

Add a new standing agent to the fleet org chart with scope = cyber security + code reviews + test coverage + quality. Always-on; adversarial-to-happy-path posture; Opus 4.7 + xhigh effort.

**Pro:**
- Single-point-of-ownership for security and quality.
- Continuous monitoring (in principle) — could catch concerns that triggered dispatches miss.
- Strong "adversarial" framing at the fleet level.

**Con (per UPF audit, summarized):**
- **9 of 21 UPF anti-patterns tripped** — including unvalidated assumptions (AP-1), vague phases (AP-2), vague success criteria (AP-3), no rollback (AP-4), first idea unchallenged (AP-10), no kill criteria (AP-11), confidence without evidence (AP-13), hallucinated effort estimates (AP-15), unverifiable gates (AP-18).
- **Conflates four disciplines** (cyber-security, code review, test coverage, quality) into one role; each is a separate specialty with separate evidence bases.
- **Existing cyber-security surface is saturated** — 33 council verdict beacons in 72 hours preceding the audit; sec-eng-council dispatch operates well within its SLA. Adding a standing agent on top is redundant detection layered on saturated detection.
- **Most expensive standing seat in the fleet.** Opus 4.7 + xhigh continuously is the highest model-effort configuration available; equivalent to a second Admiral seat.
- **No clear daily workstream.** Standing agents need a daily output; security work is event-driven; idle-window problem is unresolved.
- **Inverts the fleet's "triggered depth" capacity model** — sec-eng-council currently runs at Opus xhigh BUT only on dispatch; standing inverts that to continuous burn at the same effort.
- **Dispute resolution undefined.** What happens when security officer says "no" and Engineer disagrees? Veto without escalation creates paralysis; veto without enforcement is theater.

**Verdict: rejected per UPF audit + CIC ratification 2026-05-21T12:20Z.** The proposal addresses the LEAST of the actual evidence base at the GREATEST cost. The genuine gap (timing of adversarial review) is closed by Option C below at a fraction of the cost.

### Option C — Stage-05 adversarial review extension (RECOMMENDED)

Extend the Stage-05 hand-off template with an "Adversarial Brief" section. Dispatch sec-eng-council (existing) + test-eng-council (new) on Stage-05 hand-off filing. Engineer's Stage-06 build is gated on Stage-05 verdicts. Stage-06 SPOT-CHECK remains (lighter, verification-of-application).

**Pro:**
- **Addresses 25% of findings at the right gate** (Phase 0 Evidence #1). The remaining 75% continue to require Stage-06 review; both gates carry distinct responsibilities.
- **Reuses existing subagent infrastructure.** sec-eng-council dispatch mechanism unchanged; only the trigger event is extended.
- **Zero new agents in the org chart.** test-eng-council is a triggered subagent type (Sonnet 4.6 + medium per UPF recommendation), not a standing role; no idle-window problem.
- **Test-coverage authoring gap addressed.** test-eng-council reviews the test scaffold at Stage-05 before implementation begins; gaps surface in time to be closed.
- **Reversible at zero cost.** If Phase 4 gate fails, revert; documentation-only change; no in-flight code dependencies.
- **Captures cohort-1 R3 retrospective's structural cause.** Adversarial review fires at Stage-05; auto-merge race window narrows because the PR is not yet open.
- **Composes with existing fleet conventions.** Dispatch-SLA rule extends to the new trigger event; standing-pattern catalog references the new gate; CodeRabbit / CodeQL / QM-daemon backstops unchanged.

**Con:**
- **+30-45 min Stage-05 authoring time per hand-off** (Adversarial Brief authoring). Mitigated by the worked-example template + by integrating brief authoring into existing hand-off flow.
- **+20% sec-eng-council volume** (one dispatch per cohort hand-off in addition to the per-PR Stage-06 SPOT-CHECKs). Dispatch infrastructure scales (Evidence #3); marginal cost is bounded.
- **Inbox-noise contribution.** Each Stage-05 hand-off produces 1-2 additional council verdict beacons; estimated +3-5 beacons/week. Inbox is already 300+ beacons but the marginal contribution is small.
- **Adversarial Brief could become boilerplate.** Kill trigger (§"Phase 4 decision gate") guards against this — 2 consecutive cohorts with zero Stage-05 findings retires the gate.

**Verdict: adopted.** The cost (one protocol amendment + one new triggered subagent type + ~30-45 min/cohort authoring) is small relative to the structural gain (25% of findings caught earlier; test-coverage gap closed; happy-path bias inoculated at the right gate).

### Option D — Both Stage-05 + Stage-06 dual-pass (considered)

Extend Stage-05 review (Option C) AND keep full-weight Stage-06 SPOT-CHECK (current state). Both gates carry full review responsibility; council reviews each design twice.

**Pro:**
- Highest defense-in-depth.
- Catches any drift between Stage-05 plan and Stage-06 implementation.

**Con:**
- **Cost doubles.** Two full council reviews per cohort. +40% sec-eng-council volume (vs +20% for Option C).
- **Review fatigue.** Second pass reviewing the same design erodes fresh-perspective value; second-pass verdicts converge on first-pass amendments without independent insight.
- **Inbox noise.** +6-10 beacons/week instead of +3-5.

**Verdict: rejected.** The Stage-06 lightening in Option C (verification-of-application, not full re-review) preserves the defense-in-depth benefit at half the cost. The two gates' responsibilities split cleanly: Stage-05 reviews intent; Stage-06 reviews execution.

---

## Decision

**Adopt Option C.** Extend the Stage-05 hand-off template with a mandatory "Adversarial Brief" section. Dispatch `sec-eng-council` (existing subagent; Opus 4.7 + xhigh) AND `test-eng-council` (new subagent type; Sonnet 4.6 + medium) on Stage-05 hand-off filing per the trigger matrix in §"Test-eng-council subagent" below. Engineer's Stage-06 build SHALL NOT begin until Stage-05 verdicts are GREEN or AMBER-with-clear-amendments. Stage-06 SPOT-CHECK is retained but lightened to verification-of-application + runtime-evidence review.

### Adversarial Brief — template

Reference: `icm/02_architecture/adversarial-brief-template-prototype.md` (shipyard#78; 8-bullet worked example for cohort-4 audit-trail viewer).

The Adversarial Brief is a Stage-05 hand-off section that answers, for each load-bearing design decision: **"What's the worst-case interpretation of this design decision, and what fails when an adversary or careless caller exercises it?"** It is NOT a risk register (probabilities + impact); it is a SEMANTIC stress-test (assume the worst-case caller; what's the worst outcome?). The product is a list of 5-8 surfaced concerns that Stage-06 build + sec-eng SPOT-CHECK can verify against.

**Structure (per decision):**

```markdown
### Decision N — <name>

- **Decision summary:** <one line; what the design chose>
- **Worst-case interpretation:** <if an adversary or careless caller exercises
  this decision under the worst-case assumption, what happens?>
- **Failure mode:** <concretely, what fails — auth bypass; cross-tenant data;
  data corruption; cascading retry storm; etc.>
- **Mitigation in this hand-off:** <what the hand-off encodes to prevent the
  failure mode; OR "flagged for Stage-06 SPOT-CHECK consideration"; OR
  "deferred to <follow-on workstream>">
```

**Placement.** AFTER the design-surface section + BEFORE the implementation checklist. Reviewers consume the design first, then the worst-case stress-test, then the implementation tasks.

**Cap.** 5-8 bullets per Adversarial Brief. Beyond that, the brief loses focus.

**Mandatory for:** All Stage-05 hand-offs filed under this ADR (post-ratification). The cohort-4 audit-trail viewer hand-off (shipyard#81) is the first canonical instance.

**Optional for:** Stage-05 hand-offs with trivial scope (single-file fix; doc-only change; rename); Admiral judgment call at hand-off authorship.

### Council dispatch — trigger matrix

| Council | Effort | Trigger conditions | Verdict beacon shape |
|---|---|---|---|
| `sec-eng-council` (existing; Opus 4.7 + xhigh) | xhigh | Stage-05 hand-off filed with Adversarial Brief section present | `council-verdict-<ts>-security-engineering-<workstream>-stage-05.md` |
| `.NET-architect-council` (existing; Opus 4.7 + xhigh) | xhigh | Stage-05 hand-off filed with substrate-touch (new entity, new endpoint family, new audit event type) OR ADR-text review | `council-verdict-<ts>-net-architect-<workstream>-stage-05.md` |
| `test-eng-council` (new; Sonnet 4.6 + medium) | medium | Stage-05 hand-off filed with >5 test cases as acceptance criteria OR substrate-touching PR OR cross-cluster integration tests required | `council-verdict-<ts>-test-engineering-<workstream>-stage-05.md` |

**Parallel dispatch.** All applicable councils dispatch in parallel on the same Stage-05 hand-off. Verdicts are independent and non-blocking on each other.

**SLA.** 30 minutes from Admiral consumption of the Stage-05 hand-off status beacon (mirrors fleet-conventions §"SPOT-CHECK dispatch SLA" for the Stage-06 trigger). QM daemon's `spot-check-backstop` check extends to catch missed Stage-05 dispatches by analogy.

### Test-eng-council subagent

Per `icm/02_architecture/test-eng-council-subagent-definition-research.md` (shipyard#90; V5 #3 ONR research feeding this ADR), the test-eng-council subagent's role + checklist + trigger conditions + output beacon shape + memory model are defined as follows:

**Role.** At Stage-05 hand-off review, verify the test-coverage scaffold meets the hand-off's stated acceptance criteria — not just security (sec-eng's domain) but functional + regression coverage. Catch test-coverage gaps before Stage-06 implementation starts.

**Differentiation from sec-eng-council:**

| Dimension | sec-eng-council | test-eng-council |
|---|---|---|
| Lens | Threat model | Coverage model |
| Primary focus | Cross-tenant; CSRF; audit emission; crypto primitives | Test enumeration; failure-path; regression; performance |
| Model + effort | Opus 4.7 + xhigh | Sonnet 4.6 + medium |
| Memory across dispatches | None (stateless) | None (stateless) |
| Overlap | sec-eng covers security tests | test-eng covers ALL other tests; no overlap |

**Review checklist (Stage-05):**

1. **Acceptance-criteria test enumeration.** Per-PR test count; happy-path + failure-path coverage; cross-tenant tests for any `IMustHaveTenant` entity introduced; idempotency tests for any POST endpoint with Idempotency-Key; audit-emission tests for any new `AuditEventType` constant introduced.
2. **Edge-case enumeration.** For each happy-path case, what's the analogous failure-path? For each new entity, what's the `IMustHaveTenant` interface compliance? For each new endpoint, what's the cross-tenant probe test (per cohort-2 hand-off precedent)? For each new POST, what's the antiforgery + idempotency-key test?
3. **Integration-test coverage.** Are integration tests scoped per cluster? Does the hand-off name the test-fixture project? Are WireMock.NET cassettes (or equivalent) named per scenario?
4. **Regression-baseline check.** For any legacy entity touched (e.g., `Project`, `TaskItem`, `AuditRecord`), is there a "regression test on populated DB" requirement per ADR 0091 R2 §A5 amendment?
5. **Performance benchmark expectations.** Does the hand-off enumerate performance-relevant acceptance criteria? Are benchmark tests named?
6. **Test-fixture seeding pattern.** Are seed-data construction patterns documented?

**Output beacon shape:**

```
council-verdict-<timestamp>-test-engineering-<workstream>-stage-05.md
```

Frontmatter:

```yaml
---
type: council-verdict
council: test-engineering (Stage-05)
workstream: <e.g., W#78 cohort-4 audit-trail viewer>
handoff: <e.g., shipyard#81>
verdict: GREEN | AMBER | RED
---
```

Body sections: Summary verdict (1 paragraph); per-item review (typically 5-10 items); blockers (RED); conditional concerns (AMBER); forward-watched concerns (informational).

**Memory model.** Stateless per dispatch. Each subagent reads the hand-off + companion PRs + referenced ADRs + cerebrum (auto-load) + fleet-conventions (auto-load), but carries no memory across dispatches. Cross-dispatch coordination flows via beacon (forward-watched concerns in the verdict body).

**File location for definition.** Ship as `.claude/agents/test-eng-council.md` under shipyard, mirroring the convention for existing council subagent definitions. Definition file deliverable is Phase 2 of this ADR's roll-out.

### Stage-06 SPOT-CHECK relationship

The existing Stage-06 SPOT-CHECK is NOT removed. It is lightened — the Stage-06 council pass shifts from "full adversarial review of design + implementation" to "verification that Stage-05 amendments were applied + runtime-evidence review."

**Stage-06 SPOT-CHECK responsibilities under this ADR:**

1. **Verify Stage-05 amendment application.** Each AMBER finding in the Stage-05 verdict should be addressed in the Stage-06 PR. Council confirms.
2. **Runtime-evidence review.** Items not Stage-05-catchable (implementation-detail forward-watches, recursion-safety properties, CI/runtime correctness — per Phase 0 Evidence #1 the remaining 75%) get full review.
3. **Drift detection.** If the Stage-06 implementation deviates substantially from the Stage-05 plan, council flags the drift and dispatches re-review.

**Stage-06 SPOT-CHECK is no longer responsible for:**

- First-pass adversarial review of design decisions already covered in the Stage-05 Adversarial Brief.
- Interface/contract completeness checks already verified at Stage-05 (the 25% category per Phase 0 Evidence #1).

### Pilot — cohort-4 audit-trail viewer

The cohort-4 C3 audit-trail viewer (`icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md`; shipyard#81) is the first canonical instance under the amended protocol. The hand-off already carries an Adversarial Brief section (8 bullets per the V3 #4 prototype). Pilot constraints:

- **Stage-05 sec-eng-council dispatch.** Filed on shipyard#81 PR-open (Stage-05 hand-off filing).
- **Stage-05 test-eng-council dispatch.** Triggered by >5 test cases (12-14 per the hand-off) + substrate touch (`IAuditEventReader` per ADR 0094) + cross-cluster (FED + Engineer + Bridge endpoint family).
- **Stage-06 build (Engineer PR 0 + FED PRs 1/2/3) gated on Stage-05 verdicts.** GREEN or AMBER-with-amendments allows Stage-06 to proceed; RED halts and forces Stage-05 redraft.
- **Phase 4 decision-gate measurement.** Cohort-4 Stage-06 SPOT-CHECK findings are tallied against cohort-2 + cohort-3 Stage-06 baselines. If Stage-06 findings drop ≥25% (consistent with Phase 0 Evidence #1) AND Engineer reports no measurable velocity hit, the protocol amendment ratifies. Otherwise, retrofit or retire.

---

## Phase 4 decision gate

Per Admiral directive `coordination/inbox/admiral-directive-2026-05-21T12-25Z`, this ADR's adoption is conditional on Phase 4 evidence after the first cohort under the amended protocol. Decision-gate criteria:

### Ratify-protocol-amendment-permanently criteria (ALL must hold)

1. **Cohort-4 Stage-06 SPOT-CHECK findings drop ≥25%** relative to cohort-2 + cohort-3 Stage-06 baseline (consistent with Phase 0 Evidence #1's 25% Stage-05-catchability number).
2. **Engineer velocity is not measurably hit.** Cohort-4 hand-off → first PR latency is within 1.5× the cohort-2 + cohort-3 average. (Per cerebrum 2026-05-19 inbox-directed-agents discipline, per-task ack between queue items is over-conservative; the Stage-05 gate must not regress on this.)
3. **Stage-05 verdicts produce ≥1 substantive finding** that would NOT have surfaced at Stage-06 PR-open. (Validates the AHA premise empirically. Zero substantive findings = the gate is theater.)
4. **Inbox-noise contribution stays within budget.** Stage-05 verdict beacons + Stage-05 hand-off status beacons add ≤5 beacons/week relative to pre-amendment baseline.

### Retire-protocol-amendment criteria (ANY of these triggers retirement)

1. **Adversarial Brief sections become boilerplate.** Two consecutive cohorts with zero substantive Stage-05 findings + Engineer reports brief authorship as rote enumeration → retire the gate. Timeout: 4 cohorts after ratification (~14 days).
2. **Engineer velocity drops measurably without proportional finding-rate justification.** Cohort hand-off → first PR latency increases >2× cohort-2 + cohort-3 baseline → re-evaluate at 3 cohorts; if velocity hit > finding value, retire. Timeout: 21 days.
3. **Inbox noise crosses budget.** Stage-05 verdict beacons exceed +10/week relative to pre-amendment baseline AND CIC flags inbox-triage burden → re-evaluate at 14 days.
4. **Dispatch infrastructure strain.** sec-eng-council OR test-eng-council dispatch latency p95 exceeds 60 minutes (2× current p95 ~28 min per Evidence #3) → constraint binds; retrofit or retire.

### Retrofit-and-iterate criteria (intermediate outcome)

If Phase 4 metrics are ambiguous (e.g., Stage-06 finding drop is 15% rather than 25%; or 1 substantive Stage-05 finding but Engineer reports moderate friction), Admiral files a retrofit ruling extending the pilot to a second cohort (cohort-5 once active) before final ratification or retirement.

### Phase 4 decision authority

Admiral assembles the Phase 4 evidence (Stage-05 + Stage-06 finding tallies, Engineer velocity measurement, inbox-noise count) and files an `admiral-status-*-phase-4-evidence.md` beacon to CIC. CIC ratifies the amendment as permanent OR rules retirement OR orders retrofit. Per ADR 0069 §"ADR amendment authority," the ratification ruling is recorded as Revision 2 of this ADR (amendment row with date + summary).

---

## Consequences

### Positive

- **25% of findings caught at the right gate.** Phase 0 Evidence #1 quantifies the structural gain.
- **Test-coverage authoring gap closed.** test-eng-council subagent at Sonnet medium effort owns the gap; Stage-05 trigger ensures it fires before implementation begins.
- **Happy-path bias inoculated at authorship.** Adversarial Brief operationalizes AP-10 (first-idea-unchallenged) at the gate where the bias forms.
- **Cohort-1 R3 retrospective's structural cause addressed.** Adversarial review fires before any PR exists; auto-merge race window narrows.
- **No new agents in org chart.** UPF-discipline precedent (Trigger-over-Standing) preserved. Org chart remains lean at 9 agents.
- **Dual-council attestation discipline (ADR 0091 R2 + ADR 0092 Rev 2) extended one stage earlier.** Substrate-shaping intent identified at Stage-05; attestation concentrated where design judgment is made.
- **Reversibility at zero cost.** Documentation-only change; revertible via single Admiral ruling + retirement of subagent definition file.

### Negative

- **+30-45 min Stage-05 authoring time per hand-off.** Bounded; integrated into existing hand-off authoring flow per the worked-example template.
- **+20% sec-eng-council volume.** Within current dispatch capacity (Evidence #3: 0 SLA violations at present load); marginal token cost.
- **+3-5 inbox beacons/week.** Marginal contribution; inbox is already 300+ beacons.
- **New subagent type (test-eng-council).** One new agent definition file (~150-200 lines) ships as Phase 2 deliverable; existing council subagent precedents minimize authoring cost.
- **Risk: Adversarial Brief becomes boilerplate.** Mitigated by kill trigger in §"Phase 4 decision gate" (2 consecutive cohorts with zero substantive findings → retire).
- **Risk: Engineer pushback on additional Stage-05 toil.** Mitigated by framing the brief as a Stage-05 deliverable (not a separate gate); +30-45 min authoring cost is acceptable per Phase 0 evidence base (1-2 findings caught per cohort).

---

## Reversibility

**Zero-cost reversible.** This ADR is a protocol amendment + one new subagent definition file. No in-flight code dependencies. Revert path:

1. File `admiral-ruling-*-retire-stage-05-adversarial-review.md` recording the retirement decision + reasoning.
2. Remove `.claude/agents/test-eng-council.md` (the new subagent definition file).
3. Update `_shared/engineering/standing-approved-patterns.md` to remove Stage-05 council dispatch trigger (if cataloged there).
4. Update `.claude/rules/fleet-conventions.md` § "SPOT-CHECK dispatch SLA" to revert to Stage-06-only trigger.
5. Future Stage-05 hand-offs omit the Adversarial Brief section.

Cohort hand-offs already filed under the amended protocol remain as-is; their Adversarial Briefs become advisory historical artifacts. No data loss, no migration, no architectural debt.

---

## ADR-protocol compliance

Per ADR 0069 (ADR Authoring Discipline):

- **Council attestation requirement.** This ADR carries `requires-council: [dotnet-architect, security-engineering]` per the substrate/governance-tier discipline. Promotion to Accepted requires dual-council GREEN attestation. Dispatch fires on this ADR's PR-open per the existing Stage-06 SPOT-CHECK protocol; this ADR's own attestation cycle is the LAST exercise under the pre-amendment protocol. From the next Stage-05 hand-off forward (cohort-4 audit-trail viewer pilot), the amended protocol applies.
- **Pipeline variant.** `fleet-protocol-amendment` (custom variant for governance-tier amendments; documentation-only; no code generation).
- **§A0 cited-symbol audit complete** (see above).
- **Composes ADR 0069 + ADR 0091 + ADR 0092 + ADR 0094.** Cross-references explicit; precedent inheritance documented in §"Decision drivers."

Per fleet-conventions §"Beacon naming":

- This ADR's status beacons use `admiral-*` prefix consistent with the Admiral-authored-ADR pattern (ADR 0091 + ADR 0092 + ADR 0094 precedent).
- Council verdict beacons under the amended protocol use `council-verdict-<ts>-<council>-<workstream>-stage-05.md` filename pattern (new); existing `council-verdict-<ts>-<council>-<workstream>-spot-check.md` filename pattern continues for Stage-06.
- Inbox triage discipline (cerebrum 2026-05-20 admiral-background-subagents) applies: Stage-05 council dispatch goes to background subagents; main Admiral session continues to triage inbox + answer questions.

Per fleet-conventions §"SPOT-CHECK dispatch SLA":

- 30-minute dispatch SLA extends to the Stage-05 trigger event by analogy. QM daemon's `spot-check-backstop` check extends to catch missed Stage-05 dispatches.

Per `.claude/rules/effort-policy.md`:

- sec-eng-council + .NET-architect-council retain Opus 4.7 + xhigh per existing discipline.
- test-eng-council is Sonnet 4.6 + medium per UPF audit recommendation (lower-cost test review; coverage-model lens does not require Opus xhigh's threat-model depth).
- Subagent dispatch effort profile per the canonical table in effort-policy.md §"Subagent dispatch."

---

## Open questions

These are intentionally left for council review and Phase 4 evidence to resolve.

### Q1 — Stage-05 verdict gating semantics

GREEN verdicts allow Stage-06 to proceed. AMBER-with-amendments allows Stage-06 to proceed IF the amendments are addressable in the Stage-06 PR scope. RED halts Stage-06 and forces Stage-05 redraft. Open: how is AMBER-with-amendments adjudicated when amendments are substantial? Council judgment + Admiral confirmation, OR explicit AMBER-amendment-application checklist in the Stage-06 PR? Defer to Phase 4 evidence; refine in amendment if pattern emerges.

### Q2 — test-eng-council overlap with sec-eng-council on security-adjacent test coverage

Per ONR V5 #3 research, sec-eng-council's "audit-emission integration test" item (W#68 PR 3 verdict precedent) sits at the boundary. Open: is this sec-eng territory (security-emission semantics) OR test-eng territory (coverage enumeration)? Resolution: both councils review per their respective lenses; security-emission semantics is sec-eng's call; the coverage-enumeration completeness is test-eng's call. Refine in amendment if the boundary proves ambiguous in practice.

### Q3 — Stage-05 review of ADR-text-only hand-offs

This ADR is itself an ADR-text-only deliverable. ADR-text hand-offs (no implementation; no test scaffold; no cross-cluster integration) do not trigger test-eng-council per the matrix (no test cases as acceptance criteria). Open: should sec-eng-council still dispatch at Stage-05 for ADR-text deliverables, or is the existing Stage-06 ADR-text SPOT-CHECK (precedent: shipyard#86 dual-council attestation on ADR 0094) sufficient? Tentative answer: Stage-05 dispatch applies only if the ADR proposes a new substrate primitive or protocol change (this ADR qualifies); pure ADR-text amendments without new substrate impact may continue to use Stage-06-only attestation. Refine in amendment.

### Q4 — Adversarial Brief format under iteration

The 5-8 bullet cap is a first-pass guess. Open: does the cap need tightening (3-5 bullets to avoid bloat) or loosening (8-12 bullets for substrate-shaping hand-offs)? Phase 4 evidence will inform; refine in amendment.

---

*Filed by Admiral at 2026-05-21 per CIC ratification 12:20Z. Status: Proposed pending dual-council attestation. Reversibility: zero-cost; documentation-only.*
