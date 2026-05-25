# Stage-05 protocol amendments post-cohort-4 — additional findings (V14 #3)

**Authored by:** ONR (V14 batch item #3)
**Requester:** Admiral (per V14 standing-dispatch directive 2026-05-25)
**Authored at:** 2026-05-25T1700Z
**Status:** draft
**Parallel work:** Admiral subagent authoring ADR 0093 Rev 2 (S05-1/-2/-3/-4 ratifications)

---

## Scope

The cohort-4 first-pilot Stage-05 Adversarial Review surfaced three template gaps
(S05-1/-2/-3) on the first sec-eng RED verdict and one additional gap (S05-4, the cycle
split sequencing pin) on the cycle-1 AMBER re-attest. These are being folded into ADR 0093
Rev 2 by an Admiral subagent in parallel.

This research asks: **what ADDITIONAL Stage-05 amendments should be considered based on
the cohort-4 evidence beyond S05-1..-4?** ONR scans the cycle-0 RED, cycle-1 AMBER, and
cycle-2 GREEN verdicts for gaps the named four don't close.

**Out of scope:** the ratification of S05-1..-4 themselves (Admiral subagent's deliverable);
the Adversarial Brief format (separately worked); the cycle split sequencing rule (covered
under S05-4).

---

## TL;DR

1. **S05-1/-2/-3/-4 close the obvious wire-contract + cycle-split gaps.** The remaining
   surface area is narrower but non-trivial. ONR identifies **6 additional candidate
   template additions** (S05-5 through S05-10), each grounded in a specific cohort-4
   verdict finding the named four don't address.

2. **S05-5 (PROPOSED) — Assertion-claim audit.** The cycle-0 RED's R1 finding ("Cross-tenant
   defense R1 claim is structurally present, functionally dead") surfaces a documentation/
   code-comment mismatch with security-claim weight. Stage-05 should require, for any PR
   description that claims a defense-in-depth posture, a corresponding traceable test that
   exercises the actual defense path against the real wire contract — not a mock. Mocks
   are fine for happy-path coverage; defense-in-depth claims require contract-pinned
   evidence.

3. **S05-6 (PROPOSED) — Cross-store consistency pin.** The cycle-0 A1-SEMANTIC finding
   surfaced a tenant-ID-vs-display-name semantic mismatch between `useCompanyStore.
   activeCompany` (ERPNext display name) and the substrate's `TenantId.Value` (opaque ID).
   The two stores serialize the same conceptual entity but with different identifiers.
   Stage-05 should require, for any frontend PR that compares two store values or
   substrate-derived fields, an explicit type-+-origin pin documenting which identifier
   space each value lives in.

4. **S05-7 (PROPOSED) — Test-fixture wire-shape mandate.** Cycle-0 O1 ("Test fixtures don't
   exercise the wire contract") is the root cause beneath A1-1, A1-2, A1-3. While S05-3
   (MSW contract test scaffolding) covers the deeper defense, S05-7 codifies the minimum:
   every frontend test fixture for a wire-shape SHALL be constructed by deriving from the
   server's DTO file (either by code-generation or by a comment block pinning the field-by-
   field equivalence to the server file by path). Mocks may not invent fields.

5. **S05-8 (PROPOSED) — ProblemDetails-shape negative test.** Cycle-0 G1-3 surfaced a wire-
   shape mismatch (`body.error` vs `body.title`) that all 22 unit tests passed because the
   mocks invented the wrong shape. Stage-05 should require a single contract-shape negative
   test per error code: "when the server returns 400 with `Title: 'foo'`, the client
   correctly recognizes `body.title === 'foo'` and dispatches the expected error handler."
   This is a single test per error code, not per assertion site — cheap to add.

6. **S05-9 (PROPOSED) — Dead-UI affordance scan.** Cycle-0 A2-2 surfaced a dead severity
   filter (server silently ignored the query param). Stage-05 should require, for any UI
   affordance that changes server behavior (filter, sort, paginate, toggle), a per-
   affordance test that asserts EITHER (a) the server response measurably differs OR (b)
   the affordance is documented as client-only with an explicit forward-watch to wire it
   server-side. Dead UI affordances are not bugs but they defeat demo narratives.

7. **S05-10 (PROPOSED) — Error-boundary scope verification.** Cycle-2 N1 ("A1-SEMANTIC
   assertion throws in render function — Error Boundary scope") forward-watched whether
   `AuditEventDetailPage` is wrapped by an appropriate Error Boundary. Stage-05 should
   require, for any PR that introduces a render-function throw or uncatchable error path,
   a one-line verification of which Error Boundary handles it. If no Boundary exists, the
   PR adds one or explicitly defers via forward-watch.

8. **Net cohort-4 evidence yield: S05-1 through S05-10 (10 template additions).** S05-1..-4
   are in flight via Admiral's ADR 0093 Rev 2 subagent; S05-5..-10 are ONR's additional
   surfacings. The total set should be ratified incrementally — ratify S05-1..-4 on cohort-4
   close, then layer S05-5..-10 as cohort-5 evidence accumulates.

9. **Pattern reasonable-defense vs hardening:** S05-5 through S05-10 are NOT regressions
   against the cohort-3 cluster (cohort-3 did not surface these gaps because it was single-
   repo + no defense-in-depth claim + no novel filter parameters). They surface specifically
   because cohort-4 was the first cross-repo wire-contract cohort with defense-in-depth
   claims. Future cross-repo cohorts will benefit; same-repo cohorts inherit the discipline
   without major cost.

---

## 1. Cohort-4 verdict findings outside S05-1..-4 scope

ONR re-read the three cohort-4 verdicts (cycle 0 RED, cycle 1 AMBER, cycle 2 GREEN) end-
to-end and clustered every finding into "addressed by S05-1/-2/-3/-4" vs "candidate for
additional S05-N."

### 1.1 Cycle 0 RED verdict (2026-05-22T15:58Z) — additional findings

| Finding | S05-1..-4 coverage? | Candidate S05-N |
|---|---|---|
| A1-1 Client-side tenant assertion dead code | S05-1 wire-contract recon | also S05-5 (claim audit) |
| A1-2 TBV reads wrong field | S05-1 | (closed) |
| A1-3 signatures.length TypeError | S05-1 | also S05-7 (fixture mandate) |
| A1-4 tenant_id vs activeCompany semantic mismatch | partially S05-1 | **S05-6 (cross-store consistency pin)** |
| A2-2 Dead severity filter (server ignores param) | NOT closed by S05-1..-4 | **S05-9 (dead-UI affordance scan)** |
| G1-3 ProblemDetails body.error vs body.title | S05-2 ProblemDetails shape pin | also **S05-8 (negative-test)** |
| O1 Tests don't exercise wire contract | S05-3 MSW scaffolding | also **S05-7 (fixture-shape mandate)** |
| O5 Tenant_id naming consistency | covered by S05-1 wire-recon | (closed) |
| O6 Inline types gone | (positive note) | (closed) |
| O7 Test 6 misnamed | (positive note) | (closed) |
| R1 Cross-tenant defense claim structurally present, functionally dead | NOT closed by S05-1..-4 | **S05-5 (assertion-claim audit)** |

### 1.2 Cycle 1 AMBER verdict (2026-05-22T16:11Z) — additional findings

| Finding | S05-1..-4 coverage? | Candidate S05-N |
|---|---|---|
| Items 1-7 closure | (cycle 1 close) | (closed) |
| N1 PR description claim cleanup | NOT closed by S05-1..-4 | also **S05-5 (claim audit)** |
| N2 Stage-05 template additions (continued) | reaffirms S05-1..-3 | (closed) |
| N3 Stage-05 retro signal — cycle split pattern | **S05-4 (cycle split sequencing rule)** | (closed) |
| N4 MSW contract test forward-watch | reaffirms S05-3 | (closed) |

### 1.3 Cycle 2 GREEN verdict (2026-05-25T13:12Z) — additional findings

| Finding | S05-1..-4 coverage? | Candidate S05-N |
|---|---|---|
| Cycle-2 closure conditions 1-7 | (cycle 2 close) | (closed) |
| 8-item SPOT-CHECK checklist | (sec-eng spot-check framing) | (closed) |
| N1 A1-SEMANTIC throws in render function — Error Boundary scope | NOT closed by S05-1..-4 | **S05-10 (error-boundary verification)** |
| N2 MSW contract test forward-watch | reaffirms S05-3 | (closed) |
| N3 Stage-05 retro signal — cross-repo cycle split | **S05-4** | (closed) |

---

## 2. Six proposed additional Stage-05 amendments

### 2.1 S05-5 — Assertion-claim audit

**Anchor finding:** Cycle 0 RED R1 (cross-tenant defense claim structurally present,
functionally dead) + Cycle 1 AMBER N1 (PR description claim cleanup).

**Problem:** A PR description that claims "client-side defense-in-depth assertion" looks
like an audit trail when reviewed at merge time. If the underlying code can never fire the
assertion (e.g., compares against a field that doesn't exist on the wire), the claim is
false. Future security reviews depending on this claim are misled.

**Proposed template addition (S05-5):**

> When a Stage-05 plan claims a defense-in-depth posture (e.g., "client-side tenant
> assertion," "additional audit emission on retry path," "secondary input validation"), the
> plan SHALL name a single test that exercises the actual defense path against the real
> wire contract. The test must distinguish between the defense firing (expected behavior
> when the defense is exercised) and the defense being inactive (no observable behavior
> when normal path). Mocks may provide the input data but the assertion-under-test must
> run against the production code path, not a substituted code path.

**Cost:** ~10 minutes of plan-authoring time; one extra unit test per defense claim. Cheap.

**Anti-pattern this closes:** "test the mock, not the contract" applied to defense
claims specifically.

### 2.2 S05-6 — Cross-store consistency pin

**Anchor finding:** Cycle 0 RED A1-4 (tenant_id vs activeCompany semantic mismatch).

**Problem:** When a frontend stores multiple identifiers for the same conceptual entity
in different stores (e.g., `companyStore.activeCompany` = ERPNext display name AND
`companyStore.activeTenantId` = substrate opaque ID), code reading both must know which is
which. The Stage-05 plan should require an explicit type-+-origin pin so the wrong
identifier doesn't end up in the wrong comparison.

**Proposed template addition (S05-6):**

> When a Stage-05 plan introduces or extends a client-side store with multiple identifier
> fields for the same conceptual entity (e.g., a tenant has both a display name and an
> opaque substrate ID), the plan SHALL include a one-paragraph identifier-space pin naming
> each field, its source (which whoami/API/store), and the comparisons that are valid
> across each field. Future comparisons against either field shall reference this pin.

**Cost:** ~5 minutes of plan-authoring time; a one-paragraph pin in the Stage-05 doc.

**Anti-pattern this closes:** semantic mismatch between fields that name the same thing
but carry different value-spaces.

### 2.3 S05-7 — Test-fixture wire-shape mandate

**Anchor finding:** Cycle 0 O1 (test fixtures don't exercise the wire contract).

**Problem:** Frontend unit tests routinely construct in-memory mock objects that look like
DTOs. When the frontend's TypeScript interface drifts from the server's DTO, the mocks
silently align to the (now-wrong) TypeScript interface and the tests pass against the
fictional shape. S05-3 (MSW contract tests) closes this at the deep end (real fetch path
runs). S05-7 closes it at the shallow end (fixtures must trace back to the server DTO).

**Proposed template addition (S05-7):**

> Every frontend test fixture object intended to represent a server response (e.g.,
> `MOCK_TBV_DETAIL` representing an `AuditEventDto`) SHALL include a comment block above
> the fixture pinning the source DTO file by path and listing the fields field-by-field.
> If the server DTO has 6 fields, the fixture has 6 fields. New fields added to the server
> require a fixture update with an explicit reference to the cross-repo PR adding the
> field. Mock fixtures may not invent fields not on the server DTO.

**Cost:** ~5 minutes of plan-authoring time per fixture-heavy PR. Most cohort-4-style
PRs have 1-2 fixtures.

**Anti-pattern this closes:** wire-shape drift via test-fixture invention.

### 2.4 S05-8 — ProblemDetails-shape negative test

**Anchor finding:** Cycle 0 RED G1-3 (body.error vs body.title).

**Problem:** S05-2 (ProblemDetails shape pin) requires the Stage-05 plan to document the
wire shape. But a documented pin in the plan doesn't catch a misaligned code path at the
runtime layer. A single negative test per error code closes the runtime layer cheaply.

**Proposed template addition (S05-8):**

> When a Stage-05 plan specifies a client-side error-code branch (e.g., "handle 400 with
> body.title === 'tenant_changed_reload_page'"), the plan SHALL include a single contract-
> shape negative test that:
>
> 1. Sets up a mock 400 response with the canonical ProblemDetails shape
>    (`{ type, title, detail, status }`) and the expected title value
> 2. Invokes the actual fetch code path (not a mocked hook)
> 3. Asserts the expected typed-error class is thrown OR the expected handler is invoked
>
> One test per error-code branch is sufficient; the goal is to catch wire-shape
> mismatches at the fetch layer.

**Cost:** ~15 minutes per error-code branch. cohort-4 had 2 error codes
(`tenant_changed_reload_page`, `invalid_severity`), so ~30 minutes total.

**Anti-pattern this closes:** mock-the-hook-not-the-fetch wire-shape blindness for error
codes specifically.

### 2.5 S05-9 — Dead-UI affordance scan

**Anchor finding:** Cycle 0 A2-2 (severity filter dropdown dead — server silently ignores).

**Problem:** A UI affordance (filter, sort, toggle, paginate) that changes the URL but does
NOT measurably change the response is dead UX. It defeats demo narratives, confuses
operators, and signals a forgotten server-side wire. Stage-05 should require explicit
verification that every URL-affecting affordance is server-honored — or explicitly
documented as client-only.

**Proposed template addition (S05-9):**

> When a Stage-05 plan introduces a UI affordance that affects an outbound request (filter
> parameter, sort field, pagination cursor, toggle that changes payload), the plan SHALL
> document, per affordance:
>
> 1. Whether the affordance is server-honored (an existing or planned server endpoint
>    consumes the parameter and returns a measurably different response) OR client-only
>    (the affordance affects only client-side rendering and is documented as such with a
>    forward-watch to wire server-side later)
> 2. For server-honored: a verification test that asserts the response measurably differs
>    between two affordance values
> 3. For client-only: the explicit forward-watch entry + rationale (e.g., "page_size is
>    50; data already loaded; server-side filter is a cohort-N+ investment")
>
> Dead affordances (the parameter is sent but the server does not consume it AND the client
> does not filter post-hoc) are not acceptable.

**Cost:** ~5 minutes per affordance in the Stage-05 plan. Most pages have 2-4 affordances.

**Anti-pattern this closes:** "the dropdown changes the URL but the table doesn't change"
class of bug.

### 2.6 S05-10 — Error-boundary scope verification

**Anchor finding:** Cycle 2 N1 (A1-SEMANTIC throws in render function — Error Boundary
scope).

**Problem:** When a PR introduces a render-function throw (e.g., `throw new
TenantChangedError()` inside JSX), the throw propagates to the nearest React Error
Boundary. If no Boundary exists at the appropriate scope, the throw crashes the entire
app surface. Stage-05 should require, for any render-function throw, an explicit Error
Boundary scope pin.

**Proposed template addition (S05-10):**

> When a Stage-05 plan introduces a render-function throw (e.g., `throw new
> CustomError()` inside the component body or a hook that throws synchronously), the plan
> SHALL identify the Error Boundary that catches the throw. If no Boundary exists at the
> intended scope, the PR adds one (with appropriate fallback UI) or explicitly defers the
> Boundary addition via a forward-watch with a stated risk (e.g., "throw will propagate
> to the app-level boundary, fallback is the generic ErrorCard; acceptable for v0 given
> the throw is a hard-halt signal").

**Cost:** ~5 minutes per render-throw in the Stage-05 plan.

**Anti-pattern this closes:** silent app-level crashes from local exception paths whose
operator never intended them to escape.

---

## 3. Adversarial Brief format observations (separate from S05-N additions)

The cohort-4 cycle 0 RED verdict was thorough but not particularly adversarial in its
opening framing — the verdict header reads as a verdict, not as a worst-case interpretation.
The follow-on Stage-05 retros are the adversarial output, but those land downstream of the
verdict, not in the verdict.

ONR observation (non-binding):

- The current Adversarial Brief format (per ADR 0093 Rev 2 scaffold ONR PR #118) places
  the worst-case interpretation at the front. The cohort-4 verdict's verdict header does
  the same — names the worst plausible failure mode in 1-2 sentences before the detailed
  evidence.
- The cohort-4 verdict structure (verdict-summary in frontmatter + verdict header + numbered
  findings) is consistent with the Adversarial Brief format. No structural amendment needed
  on the verdict side.
- One observation: the cohort-4 verdict's frontmatter `verdict-summary` is ~90 lines (the
  full A1-A2-G1 narrative). For Admiral consumption, a one-line `verdict-summary` would
  be useful; the full narrative could move to the body. **Recommendation: add a one-line
  `verdict-headline` frontmatter field** to all council verdicts going forward. Cheap; saves
  Admiral ~30 seconds per verdict triage.

This is a verdict-format observation, not a Stage-05 template addition. Route to ADR 0093
Rev 2 sub-author if appropriate.

---

## 4. Worst-case interpretation methodology refinements

Per ADR 0093's worst-case-interpretation framing, the cohort-4 cycle 0 verdict applied the
methodology cleanly: every dead-code claim was traced to a runtime failure mode (signatures.
length TypeError; payload em-dashes; tenant assertion never fires). The methodology worked.

Refinement candidates surfaced by the cohort-4 evidence:

### 4.1 Methodology candidate M1 — Branch reachability proof

When a verdict claims "branch X never fires in production," the methodology should require
either (a) a trace from the wire contract showing the branch's pre-condition is unreachable
OR (b) a runtime experiment showing the branch's expected effect is absent. The cohort-4
cycle 0 verdict did both for the A1 finding (traced wire contract + would have failed at
runtime). Codifying this as a methodology pin is cheap.

### 4.2 Methodology candidate M2 — Wire-contract burden of proof

Currently the Stage-05 worst-case interpretation methodology asks "what's the worst plausible
failure mode?" The cohort-4 evidence suggests an additional question: "if this PR's
frontend type definition disagrees with the server's DTO, where does the disagreement
surface? (Field name? Field shape? Field absence?)" This question forces the reviewer to
walk the wire contract before defending the PR.

### 4.3 Methodology candidate M3 — Test-fixture authenticity check

Adversarial review of the cohort-4 cycle 0 PR would have asked: "if I take this PR's
fixtures and feed them to the actual server, does the server produce equivalent responses?"
A no-answer would have surfaced the wire-shape drift before merge. Stage-05 template
addition S05-7 covers this; the methodology pin would be: "always ask whether fixtures are
server-authentic."

These three methodology candidates can be folded into the worst-case-interpretation
prompt in the next ADR 0093 revision. Route to Admiral subagent.

---

## 5. Ratification path

ONR recommends a phased ratification:

**Phase 1 (immediate; via Admiral subagent's ADR 0093 Rev 2):**
- S05-1 (wire-contract reconciliation)
- S05-2 (ProblemDetails shape pin)
- S05-3 (MSW contract test scaffolding — at minimum, a forward-watch)
- S05-4 (cycle split sequencing rule)

**Phase 2 (this research; ratification when cohort-5 surfaces evidence):**
- S05-5 (assertion-claim audit) — recommend ratify immediately; low cost, high signal
- S05-6 (cross-store consistency pin) — recommend ratify immediately
- S05-7 (test-fixture wire-shape mandate) — recommend ratify immediately
- S05-8 (ProblemDetails-shape negative test) — recommend ratify immediately
- S05-9 (dead-UI affordance scan) — recommend ratify immediately
- S05-10 (error-boundary scope verification) — recommend ratify on first cohort-5 instance

**Phase 3 (verdict-format observations; route to ADR 0093 Rev 3 if applicable):**
- One-line `verdict-headline` frontmatter field
- Methodology candidates M1/M2/M3 as worst-case-interpretation prompt refinements

---

## 6. Cost-benefit summary

| Addition | Plan-authoring cost | Code cost | Coverage |
|---|---|---|---|
| S05-5 assertion-claim audit | 10 min | 1 test per defense claim | Defense-in-depth claim audit |
| S05-6 cross-store consistency pin | 5 min | 0 (doc pin) | Identifier-space mismatch |
| S05-7 fixture wire-shape mandate | 5 min | 0 (comment block) | Fixture invention |
| S05-8 ProblemDetails negative test | 15 min | 1 test per error code | Wire-shape blindness for errors |
| S05-9 dead-UI affordance scan | 5 min per affordance | 0-1 test per affordance | Dead UX affordances |
| S05-10 error-boundary verification | 5 min per render-throw | 0-1 boundary addition | Silent app-level crashes |
| **Net (typical cohort-4-style PR)** | **+40-60 min Stage-05** | **+3-5 tests** | 6 distinct anti-pattern classes |

The cohort-4 cycle 0 → cycle 2 cost was ~3 days wall-clock + 2 amendment cycles + sec-eng
verdict load × 3. ONR estimates S05-5..-10 in Stage-05 would have caught 4 of 5 cohort-4
defects at plan-authoring time. Total Stage-05 author cost ~+45 minutes; total wall-clock
saved ~24-48h. Strong ROI.

---

## 7. Open questions for Admiral

1. **Ratification timing.** Do S05-5..-10 land in ADR 0093 Rev 2 (alongside S05-1..-4) or
   in ADR 0093 Rev 3 (post-cohort-4 close, after evidence consolidates)? ONR recommends
   Rev 2 for S05-5/-6/-7/-8/-9 immediately; Rev 3 for S05-10 (less battle-tested).

2. **Methodology refinements M1/M2/M3 routing.** Should these flow into ADR 0093 Rev 2
   prompt revisions, or wait for a separate Stage-05 retro pass? ONR recommends Rev 2 if the
   subagent has bandwidth; otherwise defer.

3. **Verdict-format `verdict-headline` field.** Cheap; saves Admiral triage time. Should
   ONR file a separate small PR to add this to the council-verdict template, or include
   in ADR 0093 Rev 2?

4. **Sec-eng / .NET-architect cross-validation.** S05-5..-10 are ONR's surfacings; should
   Admiral dispatch a sec-eng SPOT-CHECK on this research before ratification? The
   precedent (V7 #6 catalog snapshot, V7 #5 Stage-05 retro scaffold) was ratification
   without independent council review. Sec-eng cross-validation would cost ~30-60 min;
   ONR recommends it for S05-5/-8/-10 (security-adjacent additions) but not S05-6/-7/-9.

---

## 8. Sources cited

1. `coordination/inbox/council-verdict-2026-05-22T1558Z-security-engineering-sunfish-71-cohort-4-fed-pr-1-spot-check.md` (cycle 0 RED; ~~ primary evidence)
2. `coordination/inbox/council-verdict-2026-05-22T1611Z-security-engineering-sunfish-71-cycle-1-reattest.md` (cycle 1 AMBER)
3. `coordination/inbox/council-verdict-2026-05-25T1312Z-security-engineering-sunfish-71-cycle-2-reattest.md` (cycle 2 GREEN)
4. `coordination/inbox/admiral-ruling-2026-05-22T22-30Z-cohort-4-client-side-tenant-assertion-cleanest-long-term.md` (Option B cleanest-long-term path; precedent for cycle split)
5. `shipyard/docs/adrs/0093-stage-05-adversarial-review.md` (ADR 0093 baseline)
6. `shipyard/docs/adrs/0094-i-audit-event-reader.md` (substrate referenced in cohort-4 cycle verdicts)
7. ONR V8 #6 post-cohort-10 retrospective scaffolding (shipyard PR #115 OPEN) — adjacent
   retro work
8. ONR V8 #4 ADR 0093 Rev 2 scaffold (shipyard PR #118 OPEN) — parallel Admiral subagent
   work; S05-1..-4 source
9. `feedback_prefer_cleanest_long_term_option` (memory note — informs S05-4 cycle split
   sequencing rule)
10. `feedback_check_existing_pr_state_before_specifying` (memory note — related discipline
    but distinct from S05-N additions)

---

## 9. What ONR does next

V14 #3 deliverable complete. Files `onr-status-2026-05-25T1700Z-v14-3-stage-05-amendments-
post-cohort-4-complete.md`. Proceeds to V14 #4 (sub-cohort decomposition methodology).

— ONR, 2026-05-25T17:00Z
