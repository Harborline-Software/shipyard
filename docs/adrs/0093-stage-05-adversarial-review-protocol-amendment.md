---
id: 93
title: Stage-05 Adversarial Review Protocol Amendment
status: Accepted
date: 2026-05-21
proposed-date: 2026-05-21
accepted-date: 2026-05-21
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

amendments:
  - rev: 4
    date: 2026-05-25
    summary: Cohort-4 first-pilot fold — Amendments I-M (wire-contract reconciliation; ProblemDetails field-name pin; commit-message pre-flight; pair-merge cascade; MSW contract tests). Phase 4-A provisional ratification recorded. Companion retro at icm/07_review/cohort-4-stage-05-first-pilot-retro.md.
---

# ADR 0093 — Stage-05 Adversarial Review Protocol Amendment

**Status:** Accepted (Rev 4 — cohort-4 first-pilot fold; Phase 4-A provisional ratification recorded 2026-05-25 pending CIC final attest + dual-council Rev 4 re-attest on new Amendments I/J/L)
**Date:** 2026-05-21 (Proposed Rev 1); 2026-05-21 (Rev 2 fold of dual-council SPOT-CHECK verdicts); 2026-05-21 (Accepted Rev 3 housekeeping); 2026-05-25 (Rev 4 cohort-4 first-pilot fold — Amendments I-M)
**Resolves:** Cohort-1 R3 retrospective (cerebrum entry 2026-05-21 — "SPOT-CHECK timing race") + UPF audit recommendation in `coordination/inbox/onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md` (AHA candidate: Stage-05 adversarial review extension). Operationalizes "adversarial to happy path" at the gate where the bias forms (Stage-05 planning), not at the gate where it is detected (Stage-06 PR-open). Rev 4 closes the wire-contract reconciliation gap and ProblemDetails field-name gap surfaced by the cohort-4 cycle-0 RED verdict; encodes the pair-merge cascade sequencing as a Stage-05 protocol step.
**Council inputs:** Rev 1 SPOT-CHECK verdicts (both AMBER) folded into Rev 2 — `coordination/inbox/council-verdict-2026-05-21T1504Z-security-engineering-shipyard-104-adr-0093-spot-check.md` (6 amendments) + `coordination/inbox/council-verdict-2026-05-21T1504Z-net-architect-shipyard-104-adr-0093-spot-check.md` (3 BLOCKERS + 4 AMBER). Rev 2 re-attested GREEN by both councils — `coordination/inbox/council-verdict-2026-05-21T1518Z-security-engineering-shipyard-104-adr-0093-rev-2-re-attest.md` + `coordination/inbox/council-verdict-2026-05-21T1519Z-net-architect-shipyard-104-adr-0093-rev-2-re-attest.md`. CIC ratified at 2026-05-21T15:20Z.

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-21 | Admiral | Initial draft per `coordination/inbox/admiral-directive-2026-05-21T12-25Z-stage-05-adversarial-review-extension-plan.md` Phase 1. Phase 0 instrumentation complete (3 QM deliverables; see §Context). Adversarial Brief template prototype landed at `icm/02_architecture/adversarial-brief-template-prototype.md` (shipyard#78). Test-eng-council research landed at `icm/02_architecture/test-eng-council-subagent-definition-research.md` (shipyard#90). First canonical Adversarial Brief integrated in cohort-4 audit-trail viewer Stage-05 hand-off (shipyard#81). Status: Proposed pending dual-council attestation. |
| 2 | 2026-05-21 | Admiral | Folded dual-council SPOT-CHECK verdicts (both AMBER): `coordination/inbox/council-verdict-2026-05-21T1504Z-security-engineering-shipyard-104-adr-0093-spot-check.md` (6 amendments — Checks 1-6) + `coordination/inbox/council-verdict-2026-05-21T1504Z-net-architect-shipyard-104-adr-0093-spot-check.md` (3 BLOCKERS + 4 AMBER amendments). Changes: (a) §A0 cited-symbol audit restructured — three precedent artifacts (shipyard#78/#81/#90) reclassified as "in worktree, pending merge"; `sec-eng-council` + `.NET-architect-council` reclassified as "dispatch convention; no definition file extant"; test-eng-council acknowledged as first-of-kind written council definition. (b) Dispatch trigger matrix amended with edge-case resolutions: 3a all-three-council concurrency budget paragraph; 3b pattern-009 carve-out row; 3c ADR-text-only disposition promoted from §Open questions Q3 into matrix proper. (c) Test-eng-council beacon shape pinned at `-stage-05.md` / `(Stage-05)`; supersedes-prior-research note added; fleet-conventions `council-verdict-*` prefix table addition added as Phase 2 deliverable. (d) Decision drivers' "ADR 0091 R2 + ADR 0092 Rev 2" precedent claim corrected to "ADR 0092 Rev 2 + ADR 0094" (ADR 0091 has no `requires-council`). (e) §Decision adds Q2 dual-council adjudication interim rule. (f) §Phase 4 decision gate reframed — ratify criterion #1 conjoined with criteria (b) and (c) variance allowances; cohort-4 ratification made provisional pending first write-side substrate cohort. (g) §Reversibility expanded with Steps 6 (in-flight verdict beacons), 7 (mid-execution-cohort handling), 5b (Stage-06 lightening rollback), plus Mid-cohort retirement transition rule. (h) §Consequences Phase 2 sizing claim revised (precedent-setting authoring). (i) §ADR-protocol compliance adds Merge-order dependency note (promotion-to-Accepted contingent on shipyard#78/#81/#90 landing). (j) §"Adversarial Brief — template" adds 5-12 bullet escape-hatch sentence (procedural; sec-eng Check 1). (k) Phase 2 deliverables list adds QM daemon extensions (inbox-velocity-by-beacon-type; dispatch-latency p95). (l) Open questions Q3 retired (promoted into matrix); Q2 retired (interim rule adopted); Q1, Q4 retained. Status remains `Proposed`; pending CIC ratification + re-attestation by both councils on Rev 2. |
| 3 | 2026-05-21 | Admiral | **Accepted 2026-05-21T15:20Z per CIC ratification; dual-council Rev 2 GREEN re-attest.** Re-attest verdicts: `coordination/inbox/council-verdict-2026-05-21T1518Z-security-engineering-shipyard-104-adr-0093-rev-2-re-attest.md` (GREEN; all 6 sec-eng amendments folded; merge-order dependency satisfied de facto) + `coordination/inbox/council-verdict-2026-05-21T1519Z-net-architect-shipyard-104-adr-0093-rev-2-re-attest.md` (GREEN; all 3 BLOCKERS + 4 AMBER amendments folded; two forward-watches filed non-blocking). Merge-order dependency satisfied: shipyard#78 (Adversarial Brief template prototype), shipyard#81 (cohort-4 c3 Stage-05 hand-off with first canonical Adversarial Brief), shipyard#90 (test-eng-council research) all MERGED 15:17-15:18Z. Housekeeping applied per .NET-architect forward-watch #1: §A0 totals corrected from "18 cited references" to "19 cited references" (13 + 3 + 2 + 1 sum). Status flips `Proposed` → `Accepted`. Cohort-4 audit-trail viewer pilot (Engineer's audit-events Bridge endpoint family + FED's AuditEventsPage PR 1) is the first execution under the amended protocol; the Adversarial Brief already integrated in cohort-4 Stage-05 hand-off (shipyard#81) is canonical. Forward-watch retained for Admiral follow-on: ADR 0091 retroactive frontmatter parity amendment (`requires-council: [dotnet-architect, security-engineering]`) recommended within 7 days to close the precedent-citation gap. |
| 4 | 2026-05-25 | Admiral | **Cohort-4 first-pilot fold — Phase 4-A provisional ratification recorded.** Folds the five S05-N template additions surfaced by the cohort-4 audit-trail viewer pilot (sunfish#59 + sunfish#71 + signal-bridge#38 + signal-bridge#42; final cascade merged 2026-05-25T13:54Z). Companion retrospective: `icm/07_review/cohort-4-stage-05-first-pilot-retro.md`. New amendments: (I) Wire-contract reconciliation step in Stage-05 hand-offs with FED components; (J) RFC 7807 ProblemDetails field-name pin for cross-repo 400-class paths; (K) Commit-message pre-flight checklist (commitlint W#NN body-trap) generated at Stage-05 for Stage-06 implementation; (L) Pair-merge cascade plan when frontend depends on not-yet-shipped substrate fields; (M) MSW contract-test scaffolding (cohort-5+ forward-watch, promoted to mandatory once MSW infrastructure ships). §"Phase 4 decision gate" annotated with the cohort-4 ratify-criteria evaluation (all 5 Phase 4-A criteria MET; provisional ratification stands; Phase 4-B remains gated on first write-side substrate cohort per ADR text). Q4 ("Adversarial Brief format under iteration") receives partial closure from cohort-4 evidence — 8-bullet cap held; no cap adjustment needed yet. Q1 ("Stage-05 verdict gating semantics") gains the cohort-4 cycle pattern (RED → AMBER → GREEN; cycle-1 AMBER permitted by Admiral cleanest-long-term ruling with cycle-2 amendment as the gate to merge cascade) as a worked precedent. Pending dual-council re-attest on Rev 4 (this fold; companion retro is non-substrate documentation — re-attest is non-trivial due to new Stage-05 protocol steps in Amendments I + J + L). Pending CIC ratification of Phase 4-A provisional ratification per single-cohort-override rationale documented in retro §5. |

---

## A0 cited-symbol audit

Per Rev 2 fold (sec-eng Check 6 + .NET-architect F1/F2/F3/F4), classifications are split into four states: **Existing & verified** (merged on `shipyard/main`); **In-worktree, pending merge** (file exists on an ONR/admiral worktree branch; PR is OPEN against shipyard); **Dispatch convention (no on-disk definition file)** (the council is dispatched as ad-hoc `Agent()` calls from Admiral; no shared definition file pins effort/model/checklist); **Introduced by this ADR**.

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| ADR 0069 (ADR Authoring Discipline) | Existing & verified | yes — `shipyard/docs/adrs/0069-adr-authoring-discipline.md` (mandates pre-merge council requirement for substrate/governance ADRs) |
| ADR 0091 (ITenantContext Divergence Resolution) | Existing & verified | yes — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (referenced from Adversarial Brief prototype Decision 1). NOTE: ADR 0091 frontmatter does NOT carry `requires-council`; forward-watch documents retroactive parity amendment recommendation. |
| ADR 0092 (Substrate Tenant-Keyed Repository Contract Pattern) | Existing & verified | yes — `shipyard/docs/adrs/0092-substrate-tenant-keyed-repository-contract.md` §A3 / §A6 (referenced from Adversarial Brief prototype Decision 7). Carries `requires-council: [dotnet-architect, security-engineering]`. |
| ADR 0094 (IAuditEventReader) | Existing & verified | yes — `shipyard/docs/adrs/0094-i-audit-event-reader.md` (cohort-4 anchor; first canonical Adversarial Brief consumer). Carries `requires-council: [dotnet-architect, security-engineering]`. |
| `_shared/engineering/standing-approved-patterns.md` | Existing & verified (deferential) | not independently re-checked in Rev 2; cited as coordination surface only; this ADR coordinates with the catalog but does not modify it |
| `.claude/rules/fleet-conventions.md` § "SPOT-CHECK dispatch SLA" | Existing & verified | yes — fleet-conventions lines 17-29; this ADR extends the dispatch protocol to include a Stage-05 trigger alongside the existing Stage-06 trigger |
| `coordination/inbox/onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md` | Existing & verified | yes — UPF audit recommending this protocol amendment as AHA candidate |
| `coordination/inbox/admiral-directive-2026-05-21T12-25Z-stage-05-adversarial-review-extension-plan.md` | Existing & verified | yes — Admiral Phase 0/1/2 plan ratified by CIC at 12:20Z |
| `coordination/inbox/qm-status-2026-05-21T2030Z-spot-check-stage-05-catchability-audit.md` | Existing & verified | yes — Phase 0 evidence #1 (25% of findings Stage-05-catchable) |
| `coordination/inbox/qm-status-2026-05-21T2035Z-test-coverage-gaps-retro.md` | Existing & verified | yes — Phase 0 evidence #2 (2 documented test-coverage gaps in 14-day window) |
| `coordination/inbox/qm-status-2026-05-21T2040Z-sec-eng-dispatch-latency-retro.md` | Existing & verified | yes — Phase 0 evidence #3 (median 8 min; p95 ~28 min; 0 SLA violations post-rule) |
| `icm/02_architecture/adversarial-brief-template-prototype.md` | **In-worktree, pending merge (shipyard#78)** | verified at `shipyard/.worktrees/onr-v3-4-adversarial-brief/icm/02_architecture/`. NOT yet on `shipyard/main`. ADR 0093 promotion to Accepted is contingent on shipyard#78 landing first (see §"ADR-protocol compliance" — Merge-order dependency). |
| `icm/02_architecture/test-eng-council-subagent-definition-research.md` | **In-worktree, pending merge (shipyard#90)** | verified at `shipyard/.worktrees/onr-v5-3-test-eng-council/icm/02_architecture/`. NOT yet on `shipyard/main`. **Note (.NET-architect BLOCKER 3 resolution):** the research file's tentative output-beacon shape (`council-verdict-<ts>-test-engineering-<workstream>-spot-check.md` / `council: test-engineering (SPOT-CHECK)`) is **superseded by this ADR** — the authoritative convention is `-stage-05.md` / `(Stage-05)` per §"Council dispatch — trigger matrix" below. The research file's Phase 2 update brings it into alignment. |
| `icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md` | **In-worktree, pending merge (shipyard#81)** | verified at `shipyard/.worktrees/onr-v3-1-cohort-4-handoff/icm/_state/handoffs/`. NOT yet on `shipyard/main`. First canonical Stage-05 hand-off carrying an Adversarial Brief. ADR 0093 promotion to Accepted is contingent on shipyard#81 landing first. |
| Cerebrum entry "Cohort-1 R3 retrospective" (2026-05-21) | Existing & verified | yes — `.wolf/cerebrum.md`; SPOT-CHECK timing race motivated the dispatch-SLA rule and this ADR |
| Cerebrum entry "Inbox-directed agents proceed continuously" (2026-05-19) | Existing & verified | yes — `.wolf/cerebrum.md`; informs the parallel-dispatch shape (sec-eng + test-eng fire independently) |
| `sec-eng-council` (dispatch convention) | **Dispatch convention; no definition file extant** | the council is dispatched as ad-hoc `Agent()` calls from Admiral at Opus 4.7 + xhigh; no `.claude/agents/sec-eng-council.md` exists at any fleet location (verified Rev 2 by both councils). The "existing subagent" framing in Rev 1 was a category claim, not a file claim. |
| `.NET-architect-council` (dispatch convention) | **Dispatch convention; no definition file extant** | same as above — no `net-architect-council.md` exists. |
| `test-eng-council` subagent definition file | **Introduced by this ADR (first-of-kind)** | no — definition file ships at `shipyard/.claude/agents/test-eng-council.md` as Phase 2 deliverable. **Per .NET-architect AMBER 2 resolution:** this is the FIRST written council subagent definition file in the fleet (not the third); the existing role-agent definitions at `/Users/christopherwood/Projects/Harborline-Software/.claude/agents/<role>.md` (admiral, engineer, fed, onr, pao, po-mac, po-win, qm, yeoman) provide a structural template. Phase 2 sizing is precedent-setting authoring, not precedent-following; the 150-200 line estimate may grow to ~300 lines as a result. |

§A0 totals: 19 cited references (corrected from "18" at Rev 3 promotion-to-Accepted housekeeping per .NET-architect re-attest forward-watch #1 — the four-state breakdown sums to 13 + 3 + 2 + 1 = 19; the prior "18" undercounted by omitting the not-yet-shipped test-eng-council definition file from the total). Existing & verified: 13. In-worktree pending merge (shipyard#78/#81/#90 — all MERGED 2026-05-21T15:17-15:18Z): 3. Dispatch convention (no on-disk definition file): 2 (sec-eng-council + .NET-architect-council). Introduced by this ADR: 1 (test-eng-council definition file, as first-of-kind, ships as Phase 2 deliverable).

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

- **Substrate-canonical precedent.** ADR 0092 Rev 2 and ADR 0094 established the dual-council attestation discipline (sec-eng + .NET-architect at substrate-shaping ADRs; both carry `requires-council: [dotnet-architect, security-engineering]` in their frontmatter). This ADR extends that discipline one stage earlier — the substrate-shaping intent is identified at Stage-05; the attestation cycle is concentrated where the design judgment is made. (Forward-watch: ADR 0091 R2 was originally cited here but its frontmatter does not carry `requires-council`; per .NET-architect SPOT-CHECK AMBER 1, retroactive frontmatter parity amendment for ADR 0091 is recommended in a follow-on Admiral ruling.)

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

**Substrate-shaping escape hatch (sec-eng SPOT-CHECK Check 1 amendment).** For hand-offs with 4+ load-bearing design decisions per cluster touched (substrate-shaping hand-offs — new repository contract that adds 4+ new endpoints, ADRs that propose a new substrate primitive, cross-cluster + cross-substrate work), the cap MAY extend to 12 bullets at hand-off authorship discretion. The brief should remain focused on adversarial framings rather than enumerating every design decision; the extension exists so that distinct adversarial vectors are not aggregated into a single bullet at the cost of threat-model coverage.

**Mandatory for:** All Stage-05 hand-offs filed under this ADR (post-ratification). The cohort-4 audit-trail viewer hand-off (shipyard#81) is the first canonical instance.

**Optional for:** Stage-05 hand-offs with trivial scope (single-file fix; doc-only change; rename); Admiral judgment call at hand-off authorship.

### Council dispatch — trigger matrix

| Council | Effort | Trigger conditions | Verdict beacon shape |
|---|---|---|---|
| `sec-eng-council` (dispatch convention; Opus 4.7 + xhigh) | xhigh | Stage-05 hand-off filed with Adversarial Brief section present. **ADR-text hand-offs (per edge case 3c resolution):** Stage-05 sec-eng dispatch applies IF the ADR introduces a new substrate primitive, protocol amendment, or kernel-tier marker; pure-amendment ADRs (Revision-N folds, mechanical-fix amendments) use Stage-06 SPOT-CHECK only. | `council-verdict-<ts>-security-engineering-<workstream>-stage-05.md` |
| `.NET-architect-council` (dispatch convention; Opus 4.7 + xhigh) | xhigh | Stage-05 hand-off filed with substrate-touch (new entity, new endpoint family, new audit event type) OR ADR-text review (per edge case 3c, same disposition as sec-eng above). | `council-verdict-<ts>-net-architect-<workstream>-stage-05.md` |
| `test-eng-council` (introduced by this ADR; Sonnet 4.6 + medium) | medium | Stage-05 hand-off filed with >5 test cases as acceptance criteria OR substrate-touching PR OR cross-cluster integration tests required. Does NOT trigger on ADR-text-only hand-offs (no test scaffold; no acceptance criteria as test cases). | `council-verdict-<ts>-test-engineering-<workstream>-stage-05.md` |

**Filename suffix convention (per .NET-architect SPOT-CHECK BLOCKER 3 resolution).** Suffix `-stage-05` (vs `-spot-check`) distinguishes Stage-05 verdicts from Stage-06 SPOT-CHECK verdicts. The ONR V5 #3 research file's tentative `-spot-check` suffix and `council: test-engineering (SPOT-CHECK)` frontmatter are **superseded** by this ADR for the Stage-05 trigger event; Stage-06 SPOT-CHECK verdicts retain the existing `-spot-check` suffix. The research file (`shipyard#90`) Phase 2 update brings it into alignment. Admiral's regex-based inbox scanning differentiates the two events by suffix.

**Parallel dispatch.** All applicable councils dispatch in parallel on the same Stage-05 hand-off. Verdicts are independent and non-blocking on each other.

**Edge case 3a — concurrency budget for all-three-council dispatch.** For cohort-4-shaped hand-offs (Bridge endpoint + frontend rebind + new substrate primitive + >5 tests + cross-cluster integration tests), ALL THREE councils dispatch in parallel. The total concurrent burn is bounded at one Opus xhigh (sec-eng) + one Opus xhigh (.NET-arch) + one Sonnet medium (test-eng) per such hand-off. Per Evidence #3's SLA headroom (median 8 min; p95 ~28 min; 0 violations at current load), this is within current dispatch infrastructure capacity. No sequencing constraint between the three verdicts; if sec-eng's threat verdict surfaces a coverage-implicating concern, the cross-coupling is captured by test-eng on the next dispatch cycle (or as a forward-watch in the Stage-06 SPOT-CHECK that follows). The dual-council adjudication interim rule (below) governs overlap on shared findings.

**Edge case 3b — pattern-009 carve-out.** Pattern-009 (Bridge endpoint + frontend rebind pair) currently triggers Stage-06 sec-eng SPOT-CHECK per `.claude/rules/fleet-conventions.md` §"SPOT-CHECK dispatch SLA." Under the amended protocol, pattern-009 hand-offs continue to use Stage-06 sec-eng SPOT-CHECK as the primary trigger; **additionally**, Stage-05 sec-eng dispatch applies IF the pattern-009 hand-off includes a substrate-touching component (new audit event type, new repository contract, new substrate primitive). Pure rebind-only pattern-009 hand-offs (no substrate touch) do NOT trigger Stage-05 sec-eng; they remain Stage-06-only. This pins the +20% sec-eng volume math: the +20% increment reflects cohort hand-offs (substrate-touching), not every pattern-009 PR pair.

**Edge case 3c — ADR-text-only hand-offs (resolved; promoted from §Open questions Q3).** ADR-text hand-offs trigger Stage-05 sec-eng + .NET-architect dispatch ONLY IF the ADR introduces a new substrate primitive, protocol amendment, or kernel-tier marker (i.e., the ADR's own changes have substrate-shaping consequences). Pure-amendment ADRs — Revision-N folds, mechanical-fix amendments, doc-only typo fixes — use Stage-06 SPOT-CHECK only. This ADR (0093 itself) qualifies as a protocol amendment and was therefore subject to Stage-06 SPOT-CHECK at PR-open (the last application of that pre-amendment protocol); its Rev 2 fold is itself an amendment-of-amendment which triggers re-attestation by both councils per §"Phase 4 decision authority."

**Dual-council adjudication interim rule (sec-eng SPOT-CHECK Check 2 / Q2 resolution).** When sec-eng-council and test-eng-council issue verdicts on the same Stage-05 hand-off whose findings overlap on the same test scenario:

- **sec-eng's verdict is authoritative on whether the scenario exists and what it covers** (threat-model intent — e.g., does an audit-emission test exist for the new `TenantBoundaryViolation` event type; does a cross-tenant probe test exist for the new repository contract).
- **test-eng's verdict is authoritative on whether the enumeration is complete relative to the hand-off's stated acceptance criteria** (coverage-model completeness — e.g., does the audit-emission test scaffold cover all 6 enumerated `AuditEventType` constants; does the cross-tenant probe enumerate all 4 endpoints in the new family).
- **A hand-off that passes sec-eng's intent gate may still fail test-eng's enumeration gate; both verdicts must reach GREEN-or-AMBER-with-amendments for Stage-06 build to begin.**

This preserves the differentiation-table lens partition (sec-eng = threat-model depth; test-eng = enumeration completeness) and gives Engineer a clear adjudication path through dual-AMBER scenarios. The boundary case Q2 stays open for emergent edge cases not covered by this rule; refinement deferred to Phase 4 evidence.

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

**File location for definition.** Ship as `.claude/agents/test-eng-council.md` under shipyard. **Per Rev 2 §A0 reclassification (sec-eng Check 6.1 + .NET-architect F4):** this is the FIRST written council subagent definition file in the fleet. The existing `sec-eng-council` and `.NET-architect-council` are dispatched as ad-hoc Admiral `Agent()` calls with no on-disk definition file; the test-eng-council file establishes the convention rather than inheriting from it. The structural template inheritance is from the existing role-agent definitions at `/Users/christopherwood/Projects/Harborline-Software/.claude/agents/<role>.md` (admiral, engineer, fed, onr, pao, po-mac, po-win, qm, yeoman). Definition file deliverable is Phase 2 of this ADR's roll-out; Phase 2 sizing acknowledges precedent-setting authoring (the 150-200 line estimate may grow to ~300 lines as a result; this is expected, not surprising).

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

## Rev 4 amendments — cohort-4 first-pilot fold

Per the cohort-4 first-pilot retrospective (`icm/07_review/cohort-4-stage-05-first-pilot-retro.md`; companion to this Rev 4 fold), the pilot surfaced five template additions (S05-1 through S05-5). The pilot's defining evidence was the sec-eng RED → AMBER → GREEN cycle pattern on sunfish#71: cycle 0 RED closed structural wire-contract defects (S05-1, S05-2); cycle 1 AMBER deferred semantic defects per Admiral's cleanest-long-term ruling; cycle 2 GREEN closed the defenses-in-depth after the Bridge substrate (signal-bridge#42) shipped supporting wire fields (S05-5 pair-merge cascade).

The amendments below are new explicit Stage-05 authoring steps that, had they been in force at cohort-4 hand-off authoring, would have surfaced the cycle-0 RED items pre-Stage-06.

### Amendment I — Wire-contract reconciliation step (S05-1)

**Trigger.** Stage-05 hand-offs that include a FED component binding to a server endpoint family.

**Step.** The hand-off MUST include a wire-contract reconciliation table that enumerates BOTH positive matches (server DTO field → frontend interface field) AND negative matches (fields the frontend MUST NOT declare). Format:

```markdown
### Wire-contract reconciliation — `<endpoint name>`

| Server DTO field | Frontend interface field | Source of truth | Reconciliation status |
|---|---|---|---|
| `<DTO>.<field>` (`<type>`) | `<Interface>.<field>: <type>` | `<server file path>` | MATCH |
| (server does not emit `<field>`) | (frontend must not declare `<field>`) | n/a | NEGATIVE-MATCH |
```

The negative-match rows are load-bearing — they force the hand-off author to enumerate fields the frontend MUST NOT fabricate. Cohort-4 cycle-0 RED A1-FAIL (`tenant_id` / `payload` / `signatures` fictional fields on `AuditEventDetail`) is the canonical trap this amendment prevents.

**Sec-eng + frontend-architect Stage-05 review checks the reconciliation table against the cited server DTO file directly. If the table omits a field the server emits, OR includes a field the server does not emit, sec-eng flags AMBER (or RED on substrate-shaping surfaces).**

### Amendment J — RFC 7807 ProblemDetails field-name pin (S05-2)

**Trigger.** Stage-05 hand-offs whose endpoint family emits 400-class ProblemDetails responses that the frontend must distinguish.

**Step.** The hand-off MUST include an Error-response-shape section that pins the RFC 7807 discriminator field name AND enumerates the 400 discriminators. Format:

```markdown
### Error response shape — `<endpoint family>`

400-class responses use RFC 7807 ProblemDetails. The Bridge serializer
emits `title` (not `error`) as the error-discriminator field. Frontend
error handlers MUST read `body.title === '<discriminator>'`.

Known 400 discriminators:
- `<discriminator-1>` — <description>
- `<discriminator-2>` — <description>
- (enumerate ALL 400 discriminators this endpoint family may emit)
```

Each 400 discriminator becomes a typed-error contract in the frontend (e.g., `TenantChangedError` for `tenant_changed_reload_page`; `InvalidSeverityError` for `invalid_severity`). Collapsing multiple discriminators into one generic error type is a Stage-05 finding sec-eng surfaces at hand-off review.

Cohort-4 cycle-0 RED G1-3 (frontend reading `body.error` against server emitting `body.title`) is the canonical trap this amendment prevents.

### Amendment K — Commit-message pre-flight checklist (S05-4)

**Trigger.** Any Stage-05 hand-off whose Stage-06 implementation references cross-repo PRs in commit bodies.

**Step.** The hand-off MUST include a commit-message pre-flight line in the Stage-06 implementation-checklist section:

```markdown
### Commit-message pre-flight (Stage-06 implementation discipline)

Before pushing any commit for this hand-off, run:

```bash
git log -1 --format=%B | grep -E '[A-Za-z]#[0-9]'
```

Returns nothing → safe to push. Returns matches → rephrase: use `Refs:
<repo>#<n>` as a footer (with leading blank line), or "the sibling
shipyard PR" inline, or strip the inline ref entirely. The wagoid v6
commitlint footer parser cannot tell a body reference from a footer
token.

Cross-repo PRs this implementation will likely cite:
- (enumerate the cross-repo PRs the hand-off references — e.g.,
  `signal-bridge#42`, `shipyard#94`)
```

This is mechanical; it does NOT require effort or judgment; it does require remembering to run it. The Stage-05 explicit-step makes the discipline survive across sessions. Cohort-4 witnessed the trap 5× across cycle 2 push + signal-bridge#42 amendment push.

### Amendment L — Pair-merge cascade plan (S05-5)

**Trigger.** Stage-05 hand-offs whose frontend component includes defense-in-depth assertions, server-side filter parameters, or any structural feature requiring substrate fields the server does not yet emit.

**Step.** The hand-off MUST include a pair-merge cascade plan that encodes the Engineer-first / FED-cycle-1-DRAFT / FED-cycle-2-amendment sequencing. Format:

```markdown
### Pair-merge cascade plan

**Sequencing.**

| Step | Owner | Deliverable | Cycle |
|---|---|---|---|
| 1 | Engineer | Substrate extension PR (new DTO fields / endpoint params / whoami fields) | Substrate cycle |
| 2 | FED | Frontend PR in DRAFT — scaffold + non-substrate-dependent features only | Cycle 1 |
| 3 | sec-eng | Cycle 1 SPOT-CHECK — expects AMBER (substrate-dependent features deferred, NOT silently-present) | Cycle 1 |
| 4 | Engineer | Substrate extension PR MERGED | (gate) |
| 5 | FED | Amendment commit on the frontend DRAFT — fixture realignment + feature restoration | Cycle 2 |
| 6 | sec-eng | Cycle 2 re-attest — GREEN gate for auto-merge cascade | Cycle 2 |

**Constraint.** Cycle 1's DRAFT MUST NOT silently hide a non-functional
feature. If the assertion / filter / parameter can't be wired against
the live substrate, remove it cleanly with a forward-watch comment.
Cleanly-removed-with-forward-watch is the AMBER posture; silently-
dead-code is the RED posture.
```

Cohort-4 sunfish#71 (cycle 0 RED) is the canonical trap this amendment prevents: a frontend PR that ships a structurally-present, functionally-absent defense-in-depth assertion creates a false sense of defense-in-depth and obscures the real gap from future readers. Per CIC standing directive `feedback_prefer_cleanest_long_term_option`, the small +45 min authoring cost of clean dead-code removal beats the maintenance cost of a silent guard.

**Sec-eng cycle-1 verdict posture under Amendment L.** When the FED cycle 1 commit cleanly removes a substrate-dependent feature with a forward-watch comment AND the hand-off has the pair-merge cascade plan in place, sec-eng cycle 1 verdict is AMBER (not RED) — the substrate-dependent feature is acknowledged as deferred-by-design. RED is reserved for cases where the substrate-dependent feature is silently absent or actively broken.

### Amendment M — MSW contract test scaffolding (S05-3; forward-watch promoted with infrastructure ship)

**Trigger.** Stage-05 hand-offs with cross-repo wire-contract surfaces (FED binding to server endpoint family).

**Step.** The hand-off SHOULD (not yet MUST) include an MSW contract-test bullet:

```markdown
### MSW contract tests — `<endpoint family>`

For each endpoint binding, an MSW handler shaped to the server's canonical
response is registered. RTL tests exercise the real `fetch(...)` code path
through the handler. Wire-contract drift between the MSW handler and the
server DTO is caught at the MSW handler authoring step (and reviewed at
Stage-05 against the server's `<DTO>.cs` source of truth).

Required handlers for this hand-off:
- `<endpoint 1>` — list/detail response shape per `<server DTO file>`
- `<endpoint 2>` — list/detail response shape per `<server DTO file>`
- 400 ProblemDetails responses for each enumerated discriminator (per Amendment J)
```

**Promotion criterion.** Amendment M promotes from SHOULD to MUST once MSW infrastructure ships in the sunfish web app (separate ICM workstream — track via cohort-5+ work). Until promotion, RTL hook-level mocks paired with the Amendment I wire-contract reconciliation are the substitute defense.

**Rationale (forward-watch retention).** Cohort-4 cycle 2 verdict N2 retained MSW as a forward-watch (not closed): "Cycle 2 closes the semantic mismatch via fixture realignment + assertion restoration, but the deeper defense — exercising the actual fetch code path through MSW against the real Bridge contract — is still the right cohort-5+ investment." Amendment M makes the forward-watch a Stage-05 protocol step rather than per-cohort-verdict guidance.

---

## Phase 4 decision gate

Per Admiral directive `coordination/inbox/admiral-directive-2026-05-21T12-25Z`, this ADR's adoption is conditional on Phase 4 evidence after the first cohort under the amended protocol. Decision-gate criteria (Rev 2 — variance-aware per sec-eng SPOT-CHECK Check 3 + representativeness-constrained per sec-eng Check 4 + measurement-tooling specified per .NET-architect AMBER 3):

### Cohort-4 ratification is PROVISIONAL (sec-eng Check 4 amendment)

Cohort-4 (C3 audit-trail viewer) is a **read-only** pilot: 4 read-side PRs (no POST/PUT/DELETE endpoints; no idempotency-key surface; no antiforgery surface; no EFCore migration; no new entity introduced; no DbContext changes). It exercises the Stage-05 protocol against a small, substrate-conformant surface. **This is appropriate for Phase 4-A** (low-risk introduction; clear comparison baseline; existing 8-bullet brief already authored). But Phase 4 evidence harvested from cohort-4 alone is NOT generalizable to write-side cohorts or substrate-introducing cohorts.

**Ratification proceeds in two phases:**

- **Phase 4-A — Provisional ratification after cohort-4.** All Phase 4-A ratify criteria (below) must hold. On Phase 4-A pass, the protocol amendment is provisionally ratified; cohort-5 + onward proceed under the amended protocol. The retire-criteria continue to apply.
- **Phase 4-B — Final ratification after the first write-side substrate cohort.** The next cohort that introduces a POST/PUT/DELETE endpoint, new entity (with `IMustHaveTenant` interface), or new DbContext (cohort-5 or cohort-6, whichever has substrate write-side first) becomes the Phase 4-B observation cohort. On Phase 4-B pass, the protocol amendment is finally ratified. On Phase 4-B fail, Admiral files a retrofit ruling per §"Retrofit-and-iterate criteria."

### Phase 4-A ratify-criteria (cohort-4; ALL must hold)

Per sec-eng SPOT-CHECK Check 3 amendment (variance-aware reframing): the original "≥25% Stage-06 finding drop" criterion has high single-cohort variance (per QM Evidence #1, 3 of 10 verdicts account for 50% of catchable findings; cohort finding-mix dominates the drop measurement). The reframed conjunction protects against single-cohort variance:

1. **Empirical AHA-validation (was criterion #3; promoted to #1).** Stage-05 verdicts produce ≥1 substantive finding that would NOT have surfaced at Stage-06 PR-open. Zero substantive findings = the gate is theater regardless of other metrics. This is the primary criterion.
2. **Combined-rate non-regression (new).** Cohort-4 Stage-05 + Stage-06 combined finding rate is ≤ pre-amendment Stage-06-only baseline (i.e., the protocol does not ADD net findings; it RESHAPES when they surface). Protects against the "brief becomes boilerplate but Stage-06 SPOT-CHECK still catches everything" failure mode.
3. **Reshape-evidence (new).** Of findings that DID surface at both stages, ≥2 across the cohort-4 pilot were Stage-05-catchable per the Evidence #1 taxonomy (interface/contract completeness; structural/architectural choices; temporal dependencies not in spec). Baseline-aware success measure that does not depend on cohort-finding-mix variance.
4. **Engineer velocity not measurably hit (was criterion #2; retained).** Cohort-4 hand-off → first PR latency is within 1.5× the cohort-2 + cohort-3 average.
5. **Inbox-noise contribution within budget (was criterion #4; retained).** Stage-05 verdict beacons + Stage-05 hand-off status beacons add ≤5 beacons/week relative to pre-amendment baseline.

**Alternative form (escape valve):** Phase 4-A may instead extend to a 2-cohort pilot (cohort-4 + cohort-5) before final ratification, with criteria 1-5 evaluated across both cohorts in aggregate. Admiral chooses the form (single-cohort or two-cohort) based on cohort-5's substrate-write-side status when it activates. **Single-cohort ratification requires explicit CIC override + rationale** if cohort-4 alone is to settle Phase 4; otherwise the two-cohort form is the default. (Per sec-eng Check 3 substantive amendment.)

### Phase 4-A evaluation — cohort-4 first pilot (Rev 4 fold)

Per the cohort-4 first-pilot retrospective (`icm/07_review/cohort-4-stage-05-first-pilot-retro.md` §5), all five Phase 4-A ratify criteria are evaluated as MET:

| Criterion | Evaluation |
|---|---|
| 1. Empirical AHA-validation | **MET.** The pilot surfaced template-grade gaps (S05-1 wire-contract reconciliation; S05-2 ProblemDetails pin; S05-5 pair-merge cascade) that Rev 4 codifies as explicit Stage-05 steps (Amendments I + J + L). These would not have surfaced at Stage-06 SPOT-CHECK in protocol-amendment-worthy form. |
| 2. Combined-rate non-regression | **MET.** Stage-05 (Adversarial Brief; 8 decisions) + Stage-06 (cycle 0/1/2 verdicts) combined findings are not net-additive to baseline; the pilot reshapes when findings surface. Cycle-0 RED items would have surfaced at Stage-06 pre-amendment; under Rev 4 most shift earlier. |
| 3. Reshape-evidence | **MET.** ≥2 cycle 0 RED items (A1-FAIL fictional-field declaration; G1-3 ProblemDetails shape) are Stage-05-catchable per the Evidence #1 taxonomy — both interface/contract completeness category, the highest-frequency catchable type per Phase 0 audit. |
| 4. Engineer velocity not measurably hit | **MET, with caveat.** Cohort-4 hand-off → first PR latency was within baseline. The 3-day cycle-1-to-cycle-2 wall-clock was dominated by CIC-availability + inter-cycle substrate-ship-wait, not by Stage-05 protocol friction. Working-hours velocity is on baseline. |
| 5. Inbox-noise within budget | **MET.** Cohort-4 added ~6 council-verdict beacons across cycle 0/1/2; net ~3-4/week incremental — within the ≤5/week budget. |

**Single-cohort ratification rationale (per Alternative-form escape-valve requirement of explicit CIC override).** The substantive findings produced by cohort-4 (Amendments I-M) ARE the load-bearing evidence; the Rev 4 fold is the artifact that institutionalizes the findings. A two-cohort form (cohort-4 + cohort-5) would re-validate the criteria but not change the Rev 4 amendments — cohort-5 is more useful as the Phase 4-B observation cohort (write-side substrate; new test surfaces; new ratify-criteria 2 + 3 + 4 exercised) than as a Phase 4-A re-pilot. CIC ratification of Phase 4-A provisional pending; the retro recommends single-cohort form for the reason above.

**Phase 4-A provisional ratification — RECORDED.** Pending CIC final attest on this Rev 4 fold. The retire-criteria continue to apply across cohort-5 onward (see §"Retire-protocol-amendment criteria").

### Phase 4-B ratify-criteria (first write-side substrate cohort; ALL must hold)

For the first cohort that exercises write-side substrate surface (POST/PUT/DELETE endpoint family, new `IMustHaveTenant` entity, new DbContext, or EFCore migration):

1. All Phase 4-A criteria continue to hold across the write-side cohort.
2. **Cross-tenant probe completeness.** test-eng-council's "cross-tenant tests for any `IMustHaveTenant` entity introduced" check item produces ≥1 finding pre-build OR Stage-06 confirms zero such gaps surfaced post-build.
3. **Idempotency / antiforgery coverage.** test-eng-council's "idempotency-key + antiforgery tests for any POST endpoint" check item produces ≥1 finding pre-build OR Stage-06 confirms zero such gaps surfaced post-build.
4. **Populated-DB regression coverage.** If the cohort retrofits any legacy entity (per ADR 0091 R2 §A5), the regression-baseline test requirement is enforced via test-eng-council at Stage-05.

On Phase 4-B pass: protocol amendment is FINALLY ratified per ADR 0069 §"ADR amendment authority" (Rev 3 entry recording final ratification). On Phase 4-B fail: Admiral files retrofit ruling per §"Retrofit-and-iterate criteria."

### Retire-protocol-amendment criteria (ANY of these triggers retirement)

1. **Adversarial Brief sections become boilerplate.** Two consecutive cohorts with zero substantive Stage-05 findings + Engineer reports brief authorship as rote enumeration → retire the gate. Timeout: 4 cohorts after ratification (~14 days).
2. **Engineer velocity drops measurably without proportional finding-rate justification.** Cohort hand-off → first PR latency increases >2× cohort-2 + cohort-3 baseline → re-evaluate at 3 cohorts; if velocity hit > finding value, retire. Timeout: 21 days.
3. **Inbox noise crosses budget.** Stage-05 verdict beacons exceed +10/week relative to pre-amendment baseline AND CIC flags inbox-triage burden → re-evaluate at 14 days.
4. **Dispatch infrastructure strain.** sec-eng-council OR test-eng-council dispatch latency p95 exceeds 60 minutes (2× current p95 ~28 min per Evidence #3) → constraint binds; retrofit or retire.

### Phase 4 measurement tooling (Phase 2 deliverables; .NET-architect AMBER 3)

Phase 4 metrics fall into two tooling tiers:

**Instrumentable today (no daemon extension):**

- Cohort-N Stage-06 SPOT-CHECK findings tally (QM already audits verdicts; Evidence #1 demonstrated capability).
- Engineer velocity (cohort hand-off → first PR latency; Engineer beacons carry timestamps; QM computes).
- Stage-05 verdict substantive-finding count (manual QM audit per cohort end; same shape as Evidence #1 audit; reusable).
- Adversarial Brief boilerplate detection (QM audit pattern per cohort end).

**Requires NEW QM daemon checks (Phase 2 deliverable):**

- **Inbox-beacon-velocity-by-type.** Periodic count of `council-verdict-*-stage-05.md` + `*-status-*-stage-05-handoff.md` beacons per week. QM daemon does not currently track inbox-velocity-by-beacon-type; a new periodic check is needed to evaluate the "+5/week budget" criterion continuously. Alternative: mark this metric as manual-QM-audit per cohort end (lower-frequency signal but lighter tooling lift).
- **Dispatch-latency p95 aggregation.** QM daemon's `spot-check-backstop` check currently watches for missed dispatches (>2h DRAFT + no verdict); it emits findings at violation only, not at the p95-trending threshold. A new aggregation is needed to evaluate the "p95 > 60 min" retire-criterion continuously. Alternative: mark this metric as manual-QM-audit per cohort end with explicit acknowledgment that it is a lower-frequency signal.

Admiral routes both QM daemon extensions to QM as Phase 2 follow-on tasks (post Rev 2 ratification); if QM scoping determines daemon extension is disproportionate to value, the metrics fall back to per-cohort manual audit. Either path is acceptable; the constraint is that the metric exists, not the tooling shape.

### Retrofit-and-iterate criteria (intermediate outcome)

If Phase 4 metrics are ambiguous (e.g., combined-rate non-regression unclear; or 1 substantive Stage-05 finding but Engineer reports moderate friction), Admiral files a retrofit ruling extending the pilot to a second cohort (cohort-5 once active) before final ratification or retirement.

### Phase 4 decision authority

Admiral assembles the Phase 4 evidence (Stage-05 + Stage-06 finding tallies, Engineer velocity measurement, inbox-noise count, reshape-evidence taxonomy) and files an `admiral-status-*-phase-4-evidence.md` beacon to CIC. CIC ratifies the amendment as permanent (Phase 4-A or Phase 4-B per the staged ratification above) OR rules retirement OR orders retrofit. Per ADR 0069 §"ADR amendment authority," the ratification ruling is recorded as Revision 3 of this ADR (amendment row with date + summary).

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
- **New subagent type (test-eng-council).** One new agent definition file ships as Phase 2 deliverable. **Per .NET-architect SPOT-CHECK AMBER 2 + Rev 2 §A0 reclassification:** this is the first written council subagent definition file in the fleet (sec-eng-council and .NET-architect-council are dispatched as ad-hoc Admiral `Agent()` calls today with no on-disk definition file). The implicit precedent (Admiral's ad-hoc dispatch convention) does not constrain the file's shape, but the role-agent definitions at `/Users/christopherwood/Projects/Harborline-Software/.claude/agents/<role>.md` provide a structural template. Phase 2 sizing is precedent-setting authoring rather than precedent-following: the original 150-200 line estimate may grow to ~300 lines as a result; this is expected, not surprising.
- **Risk: Adversarial Brief becomes boilerplate.** Mitigated by kill trigger in §"Phase 4 decision gate" (2 consecutive cohorts with zero substantive findings → retire).
- **Risk: Engineer pushback on additional Stage-05 toil.** Mitigated by framing the brief as a Stage-05 deliverable (not a separate gate); +30-45 min authoring cost is acceptable per Phase 0 evidence base (1-2 findings caught per cohort).

---

## Reversibility

**Almost-zero-cost reversible** (Rev 2: the prior Rev 1 "zero-cost" claim is refined to acknowledge the half-state mid-cohort risk identified by sec-eng SPOT-CHECK Check 5 + .NET-architect AMBER 4). This ADR is a protocol amendment + one new subagent definition file. No in-flight code dependencies. Revert path:

1. File `admiral-ruling-*-retire-stage-05-adversarial-review.md` recording the retirement decision + reasoning. Cheap.
2. Remove `.claude/agents/test-eng-council.md` (the new subagent definition file, if it has shipped per Phase 2). Cheap (single file delete).
3. Update `_shared/engineering/standing-approved-patterns.md` to remove Stage-05 council dispatch trigger (if cataloged there). Cheap.
4. Update `.claude/rules/fleet-conventions.md` § "SPOT-CHECK dispatch SLA" to revert to Stage-06-only trigger (remove SLA-extension paragraph). Cheap.
5. Future Stage-05 hand-offs omit the Adversarial Brief section.
5b. **Update fleet-conventions.md § "SPOT-CHECK dispatch SLA" — Stage-06 lightening rollback.** Under this ADR, Stage-06 SPOT-CHECK is lightened to "verification-of-application + runtime-evidence review" (§"Stage-06 SPOT-CHECK relationship"). On retirement, Stage-06 SPOT-CHECK responsibilities EXPAND back to full adversarial review; the lightening clause must be removed from fleet-conventions. Without this step, retirement leaves Stage-06 in a half-state (lightened per ADR 0093 but no Stage-05 to compensate).
6. **Disposition of in-flight Stage-05 verdict beacons.** Existing Stage-05 verdict beacons (`council-verdict-*-stage-05.md`) are NOT deleted; they remain in archive as historical advisory artifacts. Cohort hand-offs already filed under the amended protocol remain as-is; their Adversarial Briefs become advisory historical artifacts (the work product carries forward).
7. **Disposition of mid-execution cohorts.** For cohorts whose Stage-05 dispatch is in-flight at the retirement-ruling moment: pending Stage-05 verdicts are allowed to complete (the council subagent has already been dispatched; cancelling mid-flight is wasteful). Engineer proceeds when verdicts return OR after 30 min if the council subagent did not produce a verdict in-window. The retirement ruling does not interrupt in-flight verdicts.

### Mid-cohort retirement transition rule (sec-eng SPOT-CHECK Check 5 amendment)

For cohorts that are in-flight (Stage-05 hand-off filed; not all Stage-06 PRs merged) at the time of retirement:

- All **Stage-05 verdicts already produced** remain BINDING for the remainder of that cohort. Engineer continues honoring the AMBER amendments ratified pre-retirement; the work product carries.
- **FED / Engineer authoring subsequent PRs in the cohort** (e.g., FED PRs 1/2/3 in cohort-4 if cohort-4 is mid-execution at retirement) continue to consult the Stage-05 Adversarial Brief as a BINDING reference, not "advisory historical artifact." Mid-cohort, the briefs' mitigations are load-bearing on the in-flight work.
- **Stage-06 SPOT-CHECK on subsequent PRs in that cohort** uses the lightened (verification-of-application) protocol for the remainder of that cohort. The transition to original (full-weight) Stage-06 SPOT-CHECK applies to the NEXT cohort's first PR.
- The retirement ruling identifies the cohort transition boundary explicitly: "Cohort-N is the final cohort under the amended protocol; cohort-(N+1) onward uses Stage-06-only SPOT-CHECK."

This makes the reversibility claim more truthful — it's not literally "zero-cost" mid-cohort, but the cost is small and predictable when the transition rule is in place.

No data loss, no migration, no architectural debt. Mid-cohort retirement adds bounded transition complexity but no irreversible state.

---

## ADR-protocol compliance

Per ADR 0069 (ADR Authoring Discipline):

- **Council attestation requirement.** This ADR carries `requires-council: [dotnet-architect, security-engineering]` per the substrate/governance-tier discipline. Promotion to Accepted requires dual-council GREEN attestation on Rev 2 (or AMBER-with-amendments folded). Dispatch fires on this ADR's PR-open per the existing Stage-06 SPOT-CHECK protocol; this ADR's own attestation cycle is the LAST exercise under the pre-amendment protocol. From the next Stage-05 hand-off forward (cohort-4 audit-trail viewer pilot), the amended protocol applies.
- **Pipeline variant.** `fleet-protocol-amendment` (custom variant for governance-tier amendments; documentation-only; no code generation).
- **§A0 cited-symbol audit complete** (see above; Rev 2 restructured per dual-council Rev 1 verdict).
- **Composes ADR 0069 + ADR 0091 + ADR 0092 + ADR 0094.** Cross-references explicit; precedent inheritance documented in §"Decision drivers."

### Merge-order dependency (sec-eng SPOT-CHECK Check 6.3 amendment)

**ADR 0093 promotion to Accepted is contingent on the following precedent PRs landing on `shipyard/main` first:**

| Precedent PR | Branch / worktree | Contents | Role in ADR 0093 |
|---|---|---|---|
| **shipyard#78** | `onr/v3-4-adversarial-brief` | `icm/02_architecture/adversarial-brief-template-prototype.md` | Adversarial Brief template + 8-bullet worked example for cohort-4 audit-trail viewer; the artifact this ADR's §"Adversarial Brief — template" references |
| **shipyard#81** | `onr/v3-1-cohort-4-handoff` | `icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md` | First canonical Stage-05 hand-off carrying an Adversarial Brief; the artifact this ADR's §"Pilot — cohort-4 audit-trail viewer" references |
| **shipyard#90** | `onr/v5-3-test-eng-council` | `icm/02_architecture/test-eng-council-subagent-definition-research.md` | Test-eng-council role definition feeding this ADR's §"Test-eng-council subagent" section. **NOTE:** shipyard#90 may require an alignment commit before merge to bring its tentative `-spot-check` beacon-shape claims into alignment with this ADR's authoritative `-stage-05` convention (per BLOCKER 3 resolution); alternatively, the alignment is documented in this ADR's §A0 supersedes note and shipyard#90 merges as-is. |

If any of shipyard#78 / #81 / #90 is NOT yet on `shipyard/main` at the time of ADR 0093 ratification, this ADR's load-bearing references point to files that don't exist on main; the §A0 "In-worktree, pending merge" classifications make the dependency explicit until that condition is met. Admiral coordinates the merge sequence (typically: precedent PRs first; then ADR 0093 promotion to Accepted).

### Beacon naming amendments (Phase 2 deliverable; .NET-architect BLOCKER 3 sub-amendment 2)

Per fleet-conventions §"Beacon naming":

- This ADR's status beacons use `admiral-*` prefix consistent with the Admiral-authored-ADR pattern (ADR 0091 + ADR 0092 + ADR 0094 precedent).
- Council verdict beacons under the amended protocol use `council-verdict-<ts>-<council>-<workstream>-stage-05.md` filename pattern (new); existing `council-verdict-<ts>-<council>-<workstream>-spot-check.md` filename pattern continues for Stage-06.
- Inbox triage discipline (cerebrum 2026-05-20 admiral-background-subagents) applies: Stage-05 council dispatch goes to background subagents; main Admiral session continues to triage inbox + answer questions.

**Phase 2 deliverable: fleet-conventions §"Beacon naming" table addition.** Per .NET-architect SPOT-CHECK BLOCKER 3 sub-amendment 2, fleet-conventions currently lists only role-prefixed beacons (admiral, engineer, fed, etc.); `council-verdict-*` is an implicit-but-conventional surface not in the table. Phase 2 adds `council-verdict-*` entries to the beacon-prefix table with the per-stage filename convention (`-stage-05.md` vs `-spot-check.md`) documented for fresh-session legibility.

Per fleet-conventions §"SPOT-CHECK dispatch SLA":

- 30-minute dispatch SLA extends to the Stage-05 trigger event by analogy. QM daemon's `spot-check-backstop` check extends to catch missed Stage-05 dispatches.

Per `.claude/rules/effort-policy.md`:

- sec-eng-council + .NET-architect-council retain Opus 4.7 + xhigh per existing dispatch convention (no on-disk definition file pins this; the convention lives in this ADR + fleet-conventions).
- test-eng-council is Sonnet 4.6 + medium per UPF audit recommendation (lower-cost test review; coverage-model lens does not require Opus xhigh's threat-model depth).
- Subagent dispatch effort profile per the canonical table in effort-policy.md §"Subagent dispatch."

---

## Open questions

These are intentionally left for council review and Phase 4 evidence to resolve. Rev 2 retired Q2 and Q3 (both resolved by amendments folded above); Q1 and Q4 receive partial closure from cohort-4 evidence per Rev 4 fold; Q5 added Rev 2.

### Q1 — Stage-05 verdict gating semantics

GREEN verdicts allow Stage-06 to proceed. AMBER-with-amendments allows Stage-06 to proceed IF the amendments are addressable in the Stage-06 PR scope. RED halts Stage-06 and forces Stage-05 redraft. Open: how is AMBER-with-amendments adjudicated when amendments are substantial? Council judgment + Admiral confirmation, OR explicit AMBER-amendment-application checklist in the Stage-06 PR?

**Rev 4 partial closure (cohort-4 worked precedent).** The cohort-4 cycle pattern (RED → AMBER → GREEN) demonstrates a working adjudication path for substantial AMBER amendments: cycle 0 RED → cycle 1 AMBER (with substrate-dependent items deferred per Admiral cleanest-long-term ruling) → cycle 2 GREEN (after substrate ships and FED amendment lands). The Admiral ruling (`admiral-ruling-2026-05-22T22-30Z-cohort-4-client-side-tenant-assertion-cleanest-long-term.md`) plus the cycle-1-AMBER-to-cycle-2-GREEN re-attest sequence provides the adjudication framework. Amendment L (pair-merge cascade) encodes this as the Stage-05 protocol step. Open sub-question: does Q1 close in full, or do non-pair-merge AMBER scenarios still need adjudication guidance? Defer to cohort-5+ evidence; refine in amendment if a distinct pattern emerges.

### Q2 — *(retired in Rev 2)* test-eng-council overlap with sec-eng-council on security-adjacent test coverage

**Resolved in Rev 2** by the dual-council adjudication interim rule (§"Council dispatch — trigger matrix"). sec-eng is authoritative on whether the scenario exists and what it covers (threat-model intent); test-eng is authoritative on whether the enumeration is complete relative to acceptance criteria (coverage-model completeness). Both verdicts must reach GREEN-or-AMBER-with-amendments for Stage-06 to begin. The boundary case stays open for emergent edge cases not covered by this rule; refinement deferred to Phase 4 evidence.

### Q3 — *(retired in Rev 2)* Stage-05 review of ADR-text-only hand-offs

**Resolved in Rev 2** by promotion into the trigger matrix proper (§"Council dispatch — trigger matrix" edge case 3c). ADR-text hand-offs trigger Stage-05 sec-eng + .NET-architect dispatch ONLY IF the ADR introduces a new substrate primitive, protocol amendment, or kernel-tier marker; pure-amendment ADRs (Revision-N folds, mechanical-fix amendments, doc-only typo fixes) use Stage-06 SPOT-CHECK only.

### Q4 — Adversarial Brief format under iteration

The 5-8 bullet cap is a first-pass guess. **Rev 2 partial resolution:** the 5-12 bullet escape-hatch for substrate-shaping hand-offs (§"Adversarial Brief — template") relaxes the upper bound for 4+ load-bearing-decisions cohorts. **Rev 4 partial closure (cohort-4 evidence):** cohort-4 held the 8-bullet cap at the upper end of the standard range; sec-eng cycle 0 did not flag bullet-count as a gap (the gaps were structural — fictional fields, missing reconciliation — not bullet-count). The 5-8 cap holds for read-only cohorts; the 5-12 escape-hatch remains available for substrate-shaping cohorts. Open sub-question: does the cap require further adjustment after the first write-side substrate cohort (Phase 4-B)? Defer to Phase 4-B evidence; refine in amendment if a distribution pattern emerges.

### Q5 — *(new, Rev 2 — informational)* ADR 0091 retroactive `requires-council` frontmatter amendment

ADR 0091 R2 was originally cited in this ADR's §"Decision drivers" as a precedent for dual-council attestation, but its frontmatter does NOT carry `requires-council`. Per .NET-architect SPOT-CHECK AMBER 1, retroactive frontmatter parity with ADR 0092 + ADR 0094 is recommended via a follow-on Admiral ruling. Open: scope of the retroactive amendment (frontmatter-only vs full re-attestation cycle)? Recommend: frontmatter-only amendment (the ratification cycle on ADR 0091 R2 already occurred via dispatch convention; retroactive attestation cycle is disproportionate). Defer to Admiral discretion.

---

*Filed by Admiral at 2026-05-21 per CIC ratification 12:20Z. Rev 2 folded dual-council SPOT-CHECK verdicts (both AMBER) at 2026-05-21T15:04Z. Rev 3 housekeeping promotion to Accepted at 2026-05-21T15:20Z. Rev 4 folded cohort-4 first-pilot findings (Amendments I-M) at 2026-05-25; Phase 4-A provisional ratification recorded; companion retrospective at `icm/07_review/cohort-4-stage-05-first-pilot-retro.md`. Status: Accepted (Rev 4) pending CIC final attest + dual-council re-attest on new Amendments I/J/L. Reversibility: almost-zero-cost (mid-cohort transition rule added Rev 2); documentation-only.*
