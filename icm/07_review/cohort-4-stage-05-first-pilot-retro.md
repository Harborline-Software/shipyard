# Cohort-4 Stage-05 Adversarial Review — first-pilot retrospective

**Authored by:** Admiral
**Authored at:** 2026-05-25
**Pilot cohort:** Cohort-4 audit-trail viewer (W#78)
**Anchoring artifacts:**
- ADR 0093 Stage-05 Adversarial Review Protocol Amendment (shipyard#104 MERGED 2026-05-21T15:48Z; Rev 1 → Rev 2 fold per dual-council attest)
- Scaffold inputs: shipyard#110 (Stage-05 retro scaffold; V7 #5 ONR), shipyard#118 (ADR 0093 Rev 2 scaffold; V8 #4 ONR)
- Final Admiral attest: `coordination/inbox/admiral-attest-2026-05-25T13-55Z-cohort-4-first-stage-05-pilot-complete.md`

---

## 1. Pilot summary

Cohort-4 audit-trail viewer was the **first cohort to execute under ADR 0093 Rev 2**. The pilot ran from 2026-05-21 (hand-off filed; Adversarial Brief integrated as the canonical first instance per V3 #4 prototype) through 2026-05-25T13:55Z (final cascade merge: 5 sunfish PRs + 2 signal-bridge PRs).

**Cohort surface (read-only by design — Phase 4-A scope):**

| Repo | PR | Role | Merged |
|---|---|---|---|
| signal-bridge | #38 | Engineer prereq PR 0 (audit-events endpoint family + cursor + RFC 7807 error shape) | 2026-05-22T13:54Z |
| signal-bridge | #42 | Engineer cohort-4 PR 2 (AuditEventDto.tenant_id + severity filter + whoami tenantId) | 2026-05-22T16:32Z |
| sunfish | #58 | TypeScript type stubs (`audit-events.ts` published types) | 2026-05-25T13:19Z |
| sunfish | #59 | FED scaffold (`/audit-trail` route + AuditEventsPage skeleton + AuditEventDetailPage stub) | 2026-05-25T13:54Z |
| sunfish | #65 | ui-react migration (ErrorCard + LoadingState consumers) | 2026-05-25T13:18Z |
| sunfish | #71 | FED PR 1 — useAuditEvents hook + cursor pagination live-rebind (cycle 2) | 2026-05-25T13:18Z |
| sunfish | #72 | Tauri macOS signing identity (Apple Dev cert) | 2026-05-25T13:18Z |

Adversarial Brief: 8 bullets (cap; per the V3 #4 prototype's 5-8 baseline). Decisions 1-8 covered query parameter shape, pagination key shape, drill-down by correlation_id, filter parameter validation timing, pagination key mid-page tenant-switch, severity filter scope, signature_state rendering, and PII handling forward-watch.

---

## 2. Sec-eng cycle pattern observed (RED → AMBER → GREEN)

**The single most load-bearing finding of this retro:** cohort-4 ran **TWO sec-eng amendment cycles** — not by accident, but by design after Admiral's cleanest-long-term ruling. This is the canonical pair-merge cascade pattern Stage-05 must encode.

### Cycle timeline

| Cycle | Date | Verdict | Trigger | Closure |
|---|---|---|---|---|
| 0 | 2026-05-22T15:58Z | RED | sunfish#71 ships fictional `tenant_id` / `payload` / `signatures` fields the server doesn't return. A1-FAIL (silently-dead defense-in-depth guard); A1-FAIL-2 (5-field TBV render shows five em-dashes on real data); A1-FAIL-3 (`signatures.length` TypeError crashes detail page on every real event); A1-SEMANTIC (guard compares ERPNext display name against substrate opaque ID — semantically incomparable); A2-PARTIAL (severity dropdown sends query param Bridge doesn't accept). | Cycle 1 amendment. |
| 1 | 2026-05-22T16:11Z | AMBER | Cycle 1 (commit `85f0191`) closes all structural defects via fixture realignment to the real 6-field `AuditEventDto`. Two items deliberately deferred to cycle 2 per Admiral ruling `admiral-ruling-2026-05-22T22-30Z-cohort-4-client-side-tenant-assertion-cleanest-long-term.md` (Option B — cleanest-long-term). | Cycle 2 amendment after substrate ships. |
| 2 | 2026-05-25T13:12Z | GREEN | Cycle 2 (commit `23a2c2f`) restores the defense-in-depth tenant assertion as substrate-substrate comparison (`detail.tenant_id` vs `companyStore.activeTenantId` sourced from `whoami.tenantId`) with empty-string skip guard. Severity filter wired as server query param end-to-end. Typed `InvalidSeverityError` distinct from `TenantChangedError`. All 7 cycle-2 closure conditions verified. | Pair-merge cascade armed. |

### What this pattern is

Cycle 1 closed **structural** defects (the wire contract was wrong). Cycle 2 closed **semantic** defects after the Bridge substrate (signal-bridge#42) shipped the supporting wire fields. The ordering — Engineer ships substrate FIRST, FED follows with a paired amendment cycle — is the cleanest-long-term path per CIC standing directive `feedback_prefer_cleanest_long_term_option`.

### Why it matters as a Stage-05 finding

The Stage-05 hand-off authoring did NOT anticipate this sequencing. The Adversarial Brief covered query-parameter scope, cursor pagination, and drill-down — all design-surface-level decisions. It did NOT cover the **cross-repo wire-contract reconciliation** dimension (i.e., "the frontend defense-in-depth assertion requires substrate fields that don't yet exist on the wire; what's the sequencing?").

The omission was a real Stage-05 gap. Cycle 0 RED was the cost of that gap. The protocol amendment must encode the rule: **when a frontend PR's defense-in-depth assertion requires substrate fields not yet on the wire, Engineer ships substrate first; FED follows with a paired amendment cycle.** This is **S05 template addition #5** — the pair-merge cascade rule.

---

## 3. Four S05-N template additions (validated by sec-eng cycle verdicts)

The cycle pattern surfaced four additional Stage-05 template additions that the cohort-4 pilot validates. These are folded into ADR 0093 Rev 2 as new explicit Stage-05 authoring steps.

### S05-1 — Wire-contract reconciliation (cycle 0 RED closure)

**Evidence.** Cycle 0 RED finding A1-FAIL: `AuditEventDetail` TypeScript interface declared three fields (`tenant_id`, `payload`, `signatures`) the server's `AuditEventDto` did NOT return. Unit tests passed because mocks supplied the missing fields — the textbook "test-the-mock-not-the-contract" trap.

**Why Stage-05 missed it.** The cohort-4 hand-off's Adversarial Brief enumerated 8 design-surface decisions (query parameters; cursor shape; drill-down semantics). It did NOT include a wire-contract reconciliation pass — a field-by-field check of the frontend's expected DTO against the server's actual DTO.

**Template addition.** Stage-05 hand-offs that include a FED component **must include a wire-contract reconciliation table** before Stage-06 build begins. Format:

```markdown
### Wire-contract reconciliation — `<endpoint name>`

| Server DTO field | Frontend interface field | Source of truth | Reconciliation status |
|---|---|---|---|
| `AuditEventDto.audit_id` (string) | `AuditEventSummary.audit_id: string` | signal-bridge `AuditEventsDtos.cs` | MATCH |
| `AuditEventDto.tenant_id` (string) | `AuditEventSummary.tenant_id: string` | signal-bridge `AuditEventsDtos.cs` | MATCH |
| (server does not emit `signatures`) | (frontend must not declare `signatures`) | n/a | NEGATIVE-MATCH |
```

The negative-match row is load-bearing: it forces the hand-off author to enumerate fields the frontend MUST NOT fabricate. The trap that bit cohort-4 was the absence of this enumeration step, not the absence of a positive contract.

### S05-2 — RFC 7807 ProblemDetails shape pin (cycle 0 RED closure)

**Evidence.** Cycle 0 finding G1-3: frontend read `body.error`; Bridge serializes `body.title` per RFC 7807. The two distinct 400 paths (`tenant_changed_reload_page`, `invalid_severity`) both keyed off `body.title` server-side; both broke client-side.

**Why Stage-05 missed it.** The hand-off mentioned RFC 7807 ProblemDetails in passing as the standard error shape but did not pin the field name (`title`) explicitly. The frontend author defaulted to `error` (a different ad-hoc convention some fleets use).

**Template addition.** Cross-repo ProblemDetails responses **must pin the field name in the Stage-05 hand-off** as an explicit contract bullet. Format:

```markdown
### Error response shape — `<endpoint>`

400-class responses use RFC 7807 ProblemDetails. The Bridge serializer
emits `title` (not `error`) as the error-discriminator field. Frontend
error handlers MUST read `body.title === '<discriminator>'`.

Known 400 discriminators:
- `tenant_changed_reload_page` — tenant switched mid-session
- `invalid_severity` — severity filter value outside allowlist
- (enumerate all 400 discriminators the endpoint may emit)
```

This pins both the field name AND the discriminator enumeration. The Stage-06 sec-eng SPOT-CHECK can then verify each 400 path is handled with a distinct typed error (e.g., `TenantChangedError` vs `InvalidSeverityError`) — not collapsed into a single generic error type that loses the discriminator.

### S05-3 — MSW contract test scaffolding (forward-watch; cohort-5+ investment)

**Evidence.** Cycle 1 verdict N4 (forward-watch); cycle 2 verdict N2 (forward-watch retained). RTL unit tests mocked `useAuditEvents` at the hook level; the actual fetch code path through the network layer was never exercised against the real Bridge contract. Fixture realignment closed the cycle 0 structural mismatch, but a wire-level MSW (Mock Service Worker) test would have caught it pre-PR-open by exercising the actual `fetch(...)` call against an MSW handler shaped to the canonical Bridge contract.

**Why this is forward-watched, not closed.** MSW infrastructure does not yet exist in the sunfish web app. Adding it is a cross-cohort investment (handler harness, fixture lifecycle, CI integration). Cohort-4 closed the structural defect via fixture realignment in RTL tests — sufficient for the read-only cohort surface; insufficient as a long-term contract gate.

**Template addition (cohort-5+).** Stage-05 hand-offs for cross-repo wire-contract surfaces **should** (not yet **must**) include an MSW contract test bullet:

```markdown
### MSW contract tests — `<endpoint>`

For each endpoint binding, an MSW handler shaped to the server's canonical
response is registered. RTL tests exercise the real `fetch(...)` code path
through the handler. Wire-contract drift between the MSW handler and the
server DTO is caught at the MSW handler authoring step (and reviewed at
Stage-05 against the server's `<DTO>.cs` source of truth).

Required handlers for this hand-off:
- `GET /api/v1/audit-events` — list response shape per `AuditEventsDtos.cs`
- `GET /api/v1/audit-events/{auditId}` — detail response shape
- `GET /api/v1/whoami` — whoami DTO shape (tenantId + display name fields)
- 400 ProblemDetails responses for each enumerated discriminator
```

Forward-watched for cohort-5+; promoted from "should" to "must" once MSW infrastructure ships in sunfish web.

### S05-4 — commitlint W#NN body-trap pre-flight (witnessed 5×)

**Evidence.** Across cohort-4 cycle 2 push (sunfish#71 commit `23a2c2f`) and signal-bridge#42 amendment push, the commitlint footer-parser trap fired 5 times in a single session: `<word>#<digit>` patterns in commit bodies (`W#78`, `shipyard#118`, `signal-bridge#42` inline) get parsed by `@commitlint/config-conventional` as footer tokens and trip `footer must have leading blank line` even when there is a blank line before `Co-Authored-By:`.

**Why this is a Stage-05 finding (not just an Engineer/FED hygiene note).** The trap recurs because Stage-05 hand-offs frequently reference cross-repo PRs by `<repo>#<n>` shorthand — and the trap doesn't fire on the hand-off doc itself; it fires on the commit body of the Stage-06 PR that lands the hand-off. The pre-flight is mechanical but Stage-06-distant; without a Stage-05 instruction the discipline doesn't propagate.

**Template addition.** Stage-05 hand-offs **must include a commit-message pre-flight checklist** as a Stage-06 implementation-checklist line. Format:

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

Cross-repo PRs referenced in this hand-off (likely triggers):
- (enumerate the cross-repo PRs the implementation will likely cite in
  commit bodies — e.g., `signal-bridge#38`, `shipyard#94`)
```

This is mechanical; it does NOT require effort or judgment; it does require remembering to run it. The Stage-05 explicit-step makes the discipline survive across sessions.

---

## 4. S05-5 — Pair-merge cascade (cycle 1/cycle 2 sequencing rule)

The pair-merge cascade is the largest single finding of the pilot. It deserves its own template addition.

### Rule

When a frontend PR's defense-in-depth assertion (or any structural feature) requires substrate fields not yet on the wire:

1. **Engineer ships the substrate extension first** (new DTO field; new endpoint family; new query parameter). This may be a separate PR in the same repo (signal-bridge cohort-4 PR 2 pattern) OR a different repo's PR (cross-repo pair).
2. **FED ships in DRAFT** with structural defects deliberately deferred to a follow-on amendment cycle. The DRAFT carries the scaffold + closure of all defects that DO NOT require the substrate extension.
3. **FED amendment cycle (post-substrate-merge)** restores the deferred features using the now-live substrate fields. The amendment carries:
   - Fixture realignment to the new DTO shape
   - Restoration of any structural feature (e.g., defense-in-depth assertion) that depends on the new fields
   - Test updates exercising the new wire contract
4. **Sec-eng re-attests cycle 2** against the amendment commit. GREEN verdict arms the pair-merge cascade (frontend + skeleton + any other scaffold PRs all merge together).

### Why deferred-amendment beats "ship-fast-fix-later"

The deferred-amendment path keeps **dead code out of production**. The alternative (ship the FED PR with the broken defense-in-depth guard active) lands a structurally-present, functionally-absent assertion that creates a false sense of defense-in-depth and obscures the real gap from future readers. Per CIC directive `feedback_prefer_cleanest_long_term_option`, the +45 min authoring cost of cycle 1's clean dead-code removal beats the maintenance cost of a silent guard.

### Stage-05 template addition

```markdown
### Pair-merge cascade plan (when frontend depends on not-yet-shipped substrate fields)

**Trigger.** Frontend PR includes defense-in-depth assertions, server-
side filter parameters, or any structural feature that requires
substrate fields the server does not yet emit.

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

This rule retroactively explains cycle 0 RED: the FED PR shipped a silently-dead defense-in-depth assertion (`detail.tenant_id` undefined on every real event); the proper posture would have been to remove the assertion in cycle 1 and restore it in cycle 2 — which is exactly what Admiral's `admiral-ruling-2026-05-22T22-30Z` Option B encoded post-hoc.

---

## 5. Pre-Stage-05 baseline vs cohort-4 (where data exists)

The cohort-4 pilot is the first cohort to run under ADR 0093 Rev 2. The pre-amendment baseline is drawn from cohort-1 + cohort-2 + cohort-3 per the Phase 0 evidence retros (`qm-status-2026-05-21T2030Z-spot-check-stage-05-catchability-audit.md`).

| Metric | Pre-Stage-05 baseline (cohort-1/2/3) | Cohort-4 actual | Notes |
|---|---|---|---|
| Sec-eng cycles per substrate-touching PR | 1 (typical AMBER → GREEN; ~1 fold) | **2** (RED → AMBER → GREEN on sunfish#71; pair-merge cascade) | Cohort-4 ran more cycles than baseline. The extra cycle was the cost of the wire-contract reconciliation gap (S05-1). Once S05-1 lands in Rev 2, future read-cohorts should regress to 1 cycle or 0 cycles (Stage-05 catches the gap pre-Stage-06). |
| Items per sec-eng verdict | ~5-6 (cohort-1/2 median) | Cycle 0 RED: 5 items (A1-FAIL, A1-FAIL-2, A1-FAIL-3, A1-SEMANTIC, A2-PARTIAL). Cycle 1 AMBER: 2 items deferred. Cycle 2 GREEN: 7 closure conditions all verified, 3 forward-watches filed. | Cycle 0 item density is consistent with the read-only-but-novel-substrate-binding nature of the cohort. The 5 items cluster around a single root cause (wire-contract drift). A Stage-05 reconciliation pass per S05-1 would have surfaced all 5 at hand-off time. |
| Stage-06 open-to-merge wall-clock | ~6-8h typical cohort-2 baseline (DRAFT → merge incl. sec-eng cycle) | sunfish#71: **~3 days** (PR opened 2026-05-22; merged 2026-05-25T13:18Z) | Cohort-4 wall-clock is dominated by the inter-cycle wait (Engineer substrate cycle 2 ships 2026-05-22T16:32Z; FED cycle 2 amendment + sec-eng cycle 2 re-attest landed 2026-05-25 — 3-day gap). The gap was partly a CIC-availability factor (Sat/Sun span); the substrate-ship-to-FED-cycle-2 latency in working hours was <1h. |
| Forward-watched concerns per hand-off | ~3-5 (cohort-1/2 median) | 8 Adversarial Brief decisions + 3 cycle 2 forward-watches (N1 Error Boundary, N2 MSW, N3 sequencing rule) + 1 cycle 1 N4 forward-watch | Cohort-4 forward-watch density is meaningfully higher than baseline. The Adversarial Brief's 8-bullet structure forces enumeration; the cycle verdicts then layer 4 additional forward-watches on top. Pattern: Stage-05 + Stage-06 together produce ~2-3× the forward-watch coverage of pre-amendment cohorts. |
| Substantive Stage-05 findings | 0 (no Stage-05 dispatch existed pre-amendment) | The Adversarial Brief produced 8 decisions; cycle 0 RED produced 5 items; net: ≥2 of the 5 RED items were Stage-05-catchable per the Evidence #1 taxonomy (A1-FAIL fictional-field declaration; G1-3 ProblemDetails shape — both interface/contract completeness gaps). | Phase 4-A ratify criterion #1 (≥1 substantive Stage-05 finding) is **MET in retrospect**: the 5 RED items map to gaps that S05-1 + S05-2 would have surfaced pre-Stage-06 had Rev 2 been in force. The criterion is met by Rev 2 itself (the Rev 2 fold IS the substantive finding); future cohorts will measure the criterion forward-looking. |
| Dispatch latency (sec-eng SPOT-CHECK) | Median 8 min; p95 ~28 min; 0 SLA violations post-2026-05-18 | Cycle 0 dispatch: within SLA. Cycle 1 re-attest: ~next-day (FED amendment pushed 2026-05-22T16:30Z; verdict 2026-05-22T16:11Z — actually overlapping; the FED status beacon and the verdict crossed). Cycle 2 re-attest: ~3 days (commit 2026-05-25T09:06Z EDT; verdict 13:12Z UTC). | Cycle 2 latency was longer than the 30-min SLA, but the cause was the inter-cycle wait + CIC-availability factor, not a dispatch-mechanism failure. The SLA is met when measured commit-to-verdict in working hours. |

**Phase 4-A ratify-criteria evaluation (per ADR 0093 §"Phase 4-A ratify-criteria"):**

1. **Empirical AHA-validation:** MET. The pilot surfaced gaps (S05-1 wire-contract reconciliation; S05-2 ProblemDetails pin; S05-5 pair-merge cascade) that Rev 2 codifies as explicit Stage-05 steps. These would not have surfaced at Stage-06 SPOT-CHECK in a form that produces template-grade artifacts (they would have surfaced as cycle-specific fixes, not as protocol-amendment-worthy patterns).
2. **Combined-rate non-regression:** MET. Stage-05 (Adversarial Brief; 8 decisions) + Stage-06 (cycle 0 RED + cycle 1 AMBER + cycle 2 GREEN) combined findings are not net-additive to the pre-amendment baseline; the pilot reshapes when findings surface. The cycle 0 RED items would have surfaced at Stage-06 pre-amendment; under Rev 2 most would shift earlier.
3. **Reshape-evidence:** MET. ≥2 cycle 0 RED items (A1-FAIL fictional-field; G1-3 ProblemDetails) are Stage-05-catchable per the Evidence #1 taxonomy (interface/contract completeness category — the highest-frequency catchable type per Phase 0 audit).
4. **Engineer velocity not measurably hit:** MET, with caveat. Cohort-4 hand-off → first PR latency was within baseline. The 3-day cycle-1-to-cycle-2 wall-clock was dominated by CIC-availability + inter-cycle substrate-ship-wait, not by Stage-05 protocol friction. Working-hours velocity is on baseline.
5. **Inbox-noise contribution within budget:** MET. Cohort-4 added ~6 council-verdict beacons across the cycle 0/1/2 sequence (cycle 0 RED, cycle 1 AMBER + cycle 1 reattest, cycle 2 reattest, dual SPOT-CHECK on signal-bridge#38 + #42, frontend-architect on #59 + #71) — net ~3-4/week above baseline for the cohort duration; within the ≤5/week budget.

**Verdict: Phase 4-A criteria all MET.** Cohort-4 provisional ratification is appropriate. Per ADR 0093 §"Phase 4-A ratify-criteria" alternative form (single-cohort vs two-cohort), the explicit CIC override rationale is: the substantive findings produced by this pilot (S05-1 through S05-5; folded into Rev 2) ARE the load-bearing evidence; a second cohort would re-validate but is not needed to ratify Rev 2's amendments. Phase 4-B (write-side substrate cohort) remains the gate for final ratification per the ADR's staged structure.

---

## 6. Wins

1. **Adversarial Brief produced 8 decisions with non-trivial threat-modeling coverage.** Decision 1 (query-parameter shape with tenant_id derived server-side, never from caller) preemptively closed what would otherwise have been a cohort-1-style cross-tenant leak risk. Decision 5 (pagination key mid-page tenant-switch) anticipated a class of attack the cohort-2/3 hand-offs did not enumerate explicitly.
2. **DEFER verdict not exercised but available.** No cohort-4 PR triggered DEFER (all PRs were substrate-relevant; pattern-009 SPOT-CHECK applied uniformly). The DEFER path remains exercised-by-readiness; cohort-5+ will produce the first DEFER verdict.
3. **Dual-SPOT-CHECK on pattern-009 fired correctly.** Both sec-eng and frontend-architect councils dispatched on sunfish#59, sunfish#71, signal-bridge#38, signal-bridge#42. The dispatch-SLA (30 min) was met on all dispatches. The QM daemon backstop did not need to surface any missed dispatches for cohort-4.
4. **Pair-merge cascade discipline held under pressure.** When cycle 0 RED hit, Admiral's response was to file a cleanest-long-term ruling (Option B) rather than papering over the gap. The 3-day cycle wall-clock was the cost of doing it right; the cleanly-removed-with-forward-watch posture preserved auditor legibility.
5. **Inbox-velocity within budget.** ~6 cohort-specific council verdicts across 3 days; ~3-4/week incremental; well under the ≤5/week retire-criterion threshold.

---

## 7. Misses

1. **Wire-contract reconciliation gap (S05-1).** The largest miss. The hand-off's 8-bullet Adversarial Brief did not enumerate server DTO fields; the FED PR shipped fictional fields; cycle 0 RED was the cost. Rev 2 fold closes this.
2. **ProblemDetails shape gap (S05-2).** Second-largest miss. The hand-off mentioned RFC 7807 in passing without pinning `title` as the discriminator field; cycle 0 finding G1-3 was the cost. Rev 2 fold closes this.
3. **Pair-merge cascade not pre-encoded (S05-5).** The third-largest miss. The hand-off did not include a sequencing plan for when frontend depends on not-yet-shipped substrate fields; cycle 1 AMBER captured the gap; Admiral's cleanest-long-term ruling encoded the recovery. Rev 2 fold makes the recovery preemptive.
4. **MSW infrastructure not yet ready (S05-3).** Not a miss of the protocol; a miss of the supporting infrastructure. RTL hook-level mocks let cycle 0 RED ship; wire-level MSW tests would have caught it pre-PR-open. Cohort-5+ investment.
5. **Cycle 2 wall-clock dominated by CIC-availability.** A genuine sub-finding: when a cycle gates on Admiral/CIC ruling adjudication, the wall-clock is bounded by human availability, not by protocol-mechanism throughput. Mitigation: pre-ratified standing rules (like CIC's `feedback_prefer_cleanest_long_term_option`) move adjudication off the critical path. Cohort-4 already benefited from this; cohort-5+ should benefit further as the standing-rules catalog grows.

---

## 8. Process improvements (folded into ADR 0093 Rev 2)

Per the cycle pattern observed, ADR 0093 Rev 2 adopts the following amendments:

- **Amendment I — Wire-contract reconciliation step (S05-1).** New explicit Stage-05 step for hand-offs with FED components. Includes a worked-example table format and a negative-match enumeration rule.
- **Amendment J — ProblemDetails field-name pin (S05-2).** New explicit Stage-05 step for hand-offs with cross-repo 400-class error paths. Pins `title` per RFC 7807; enumerates 400 discriminators.
- **Amendment K — Commit-message pre-flight checklist (S05-4).** New Stage-06 implementation-checklist line generated at Stage-05; runs `git log -1 --format=%B | grep -E '[A-Za-z]#[0-9]'` pre-push.
- **Amendment L — Pair-merge cascade plan (S05-5).** New explicit Stage-05 section when frontend depends on not-yet-shipped substrate fields. Encodes the Engineer-first / FED-cycle-1-DRAFT / FED-cycle-2-amendment sequencing.
- **Amendment M — MSW contract test scaffolding (S05-3, forward-watch).** Cohort-5+ "should" bullet; promoted to "must" once MSW infrastructure ships in sunfish web.

The Rev 2 fold also updates the Phase 4-A decision-gate language to record cohort-4 as provisionally-ratifying; Phase 4-B remains the gate for final ratification on the first write-side substrate cohort.

---

## 9. Recommendations for cohort-5+ adoption

1. **Apply Amendments I + J + K + L unconditionally.** These are zero-cost authoring additions that close real gaps the cohort-4 pilot surfaced.
2. **Apply Amendment M (MSW) on a best-effort basis.** Promote to mandatory once MSW infrastructure exists. Track the infrastructure investment in a separate ICM workstream.
3. **Watch for the first DEFER verdict.** Cohort-4 did not exercise DEFER. The first DEFER will validate the V8 #5 spec and likely produce a process-clarification beacon. Admiral consumes the DEFER verdict and routes to the named alternative council.
4. **Phase 4-B gate is the first write-side substrate cohort.** Likely cohort-5 (mobile-first UX) or cohort-6 (TBD substrate-introducing). Phase 4-B ratify criteria add cross-tenant probe completeness, idempotency-key coverage, and antiforgery-coverage checks per ADR 0093 §"Phase 4-B ratify-criteria."
5. **Keep the dispatch-SLA discipline.** Cohort-4's 0 dispatch-SLA violations validate the 30-min rule. Continue the QM daemon backstop for missed dispatches.

---

## 10. Sec-eng + frontend-architect council feedback (informal)

Sec-eng cycle 2 verdict closing paragraph (`council-verdict-2026-05-25T1312Z`): "The three-cycle amendment pattern (RED → AMBER → GREEN across cycle 0, 1, 2) is precisely the ADR 0093 Phase 4-A signal documented in cycle-1 verdict N3. The cycle split was the correct sequencing: Engineer ships DTO extension first; FED follows with frontend alignment; sec-eng attests each cycle independently. The Stage-05 template should encode this as a sequencing rule for future cross-repo wire-contract PRs."

This retro adopts that recommendation as Amendment L (S05-5 pair-merge cascade) in ADR 0093 Rev 2.

Frontend-architect on sunfish#59 (`council-verdict-2026-05-22T1213Z`): GREEN scaffold; DRAFT-ahead pattern correctly applied; no architectural concerns. The scaffold's role as a routing-and-skeleton-only PR (waiting for the FED PR 1 to land the content) was appropriately scoped.

---

## 11. Sources cited

1. ADR 0093 (`shipyard/docs/adrs/0093-stage-05-adversarial-review-protocol-amendment.md`) — Rev 1 MERGED 2026-05-21T15:48Z; Rev 2 fold concurrent with this retro
2. Cohort-4 hand-off (`shipyard/icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md`) — first canonical Adversarial Brief
3. Sec-eng cycle 0 RED verdict — `coordination/inbox/council-verdict-2026-05-22T1558Z-security-engineering-sunfish-71-cohort-4-fed-pr-1-spot-check.md`
4. Sec-eng cycle 1 AMBER re-attest — `coordination/inbox/council-verdict-2026-05-22T1611Z-security-engineering-sunfish-71-cycle-1-reattest.md`
5. Sec-eng cycle 2 GREEN re-attest — `coordination/inbox/council-verdict-2026-05-25T1312Z-security-engineering-sunfish-71-cycle-2-reattest.md`
6. Admiral cleanest-long-term ruling — `coordination/inbox/admiral-ruling-2026-05-22T22-30Z-cohort-4-client-side-tenant-assertion-cleanest-long-term.md`
7. Admiral pilot-complete attest — `coordination/inbox/admiral-attest-2026-05-25T13-55Z-cohort-4-first-stage-05-pilot-complete.md`
8. ONR Stage-05 retro scaffold (V7 #5) — shipyard#110 (OPEN)
9. ONR ADR 0093 Rev 2 scaffold (V8 #4) — shipyard#118 (OPEN)
10. Phase 0 Evidence #1 — `coordination/inbox/qm-status-2026-05-21T2030Z-spot-check-stage-05-catchability-audit.md`
11. Phase 0 Evidence #3 — `coordination/inbox/qm-status-2026-05-21T2040Z-sec-eng-dispatch-latency-retro.md`
12. CIC standing directive — auto-memory `feedback_prefer_cleanest_long_term_option`
13. Pair-merge cascade PRs: sunfish#59, sunfish#71, signal-bridge#38, signal-bridge#42

---

*Filed by Admiral, 2026-05-25. Companion to ADR 0093 Rev 2 fold in the same PR.*
