# sec-eng-council DEFER verdict — design spec

**Authored by:** ONR (V8 batch item #5)
**Requester:** Admiral (per `admiral-directive-2026-05-22T14-00Z` item V8 #5; per V7 #7 question #6 ruling APPROVED)
**Authored at:** 2026-05-22T14-20Z

---

## Purpose

Per ADR 0093 (Stage-05 Adversarial Review Protocol Amendment; shipyard#104 MERGED)
and the sec-eng-council subagent definition (shipyard#107 MERGED), the council
returns one of **GREEN / AMBER / RED**.

This spec introduces a **4th verdict: DEFER** per V7 #7 UPF follow-up §3.2 finding
("Research needs identification — AMBER currently"; the council has no escape hatch
for out-of-scope PRs).

---

## 1. Motivation

### 1.1 The current gap

The sec-eng-council 8-item checklist is scoped to security-engineering concerns:
cross-tenant isolation, CSRF, audit emission, idempotency, input validation,
crypto/signing, auth policy, defense-in-depth layering.

**A growing class of PRs sits outside that scope:**
- Pure design-system token changes (PAO + Yeoman territory; no security surface)
- Pure analytics-tracking instrumentation (no auth/audit touch)
- Pure documentation-only PRs that trigger SPOT-CHECK by file-pattern match but
  have no code change
- Pure pattern-catalog hygiene PRs (e.g., ONR's #88 and Admiral's #103)
- Frontend-only accessibility fixes that don't touch Bridge endpoints

**Current behavior:** sec-eng-council returns **GREEN** on these PRs (no security
findings ≠ no review value). This dilutes the meaning of GREEN and inflates the
verdict count.

**Worse:** if a PR has BOTH a security-relevant change AND an out-of-scope change,
the GREEN verdict cannot distinguish "security GREEN + non-security GREEN" from
"security GREEN + non-security out-of-scope."

### 1.2 DEFER semantics

**DEFER** = "this PR is out-of-scope for sec-eng review; route to the appropriate
council (.NET-architect / accessibility / frontend / test-eng / pattern-catalog /
etc.) OR confirm no council review is needed."

DEFER is NOT a failure verdict; it's a routing signal.

---

## 2. DEFER vs GREEN vs AMBER vs RED — decision tree

```
                Is the PR within sec-eng-council scope?
                (touches one of the 8 checklist domains)
                              │
                              ▼
                ┌────────── YES ──────────────┐
                │                              │
                ▼                              ▼
        Apply 8-item checklist           ┌── DEFER ──┐
                │                        │            │
                ▼                        ▼            ▼
        ┌── findings? ──┐         out-of-scope   route to
        │      │         │        beacon         appropriate
        ▼      ▼         ▼                       council (or
      RED    AMBER     GREEN                     skip-review)
   (blocking) (conditional) (clean)
```

### 2.1 DEFER decision criteria

The council files DEFER when **ALL** of the following hold:

1. **No item in the 8-item checklist is materially exercised by this PR.** I.e., the
   PR doesn't introduce or modify: a Bridge endpoint, a substrate primitive, an
   audit event type, a cross-tenant probe path, an Idempotency-Key surface, an
   input-validation boundary, a signing surface, or an auth policy declaration.

2. **There exists a non-empty set of OTHER councils** (or "skip-review") that more
   appropriately serve the PR. The DEFER beacon names which council(s).

3. **The dispatch was not triggered by a specifically-named security relevance**
   (e.g., Admiral dispatched with "audit emission new event type please verify" —
   if the security relevance is named in dispatch, DEFER is inappropriate; council
   must verdict GREEN/AMBER/RED on the named concern).

### 2.2 DEFER vs RED

| Dimension | DEFER | RED |
|---|---|---|
| Blast radius | "Wrong reviewer" | "Blocking concern" |
| Re-dispatch needed? | YES (to correct council) | NO (PR must change first) |
| Merge blocker? | NO (re-routed; not blocking) | YES |
| Council burden | <2 min (skim → DEFER beacon) | Full review |

### 2.3 DEFER vs AMBER

| Dimension | DEFER | AMBER |
|---|---|---|
| Triggered by | Out-of-scope | In-scope, conditional concern |
| Engineer action | None (Admiral re-routes) | Apply amendments + re-attest |
| Beacon body | Routing rationale | Amendments list |

### 2.4 DEFER vs "skip review"

DEFER is filed by **the council itself** after reading the PR. "Skip review" would
be an **Admiral-routed** decision (Admiral judges the PR doesn't need any council
review).

The distinction matters because: if Admiral skips review when the PR was security-
relevant, no council catches it. If the council DEFERS, the cognitive load shifts
to Admiral to re-route, but no security-relevant content is missed.

**Rule of thumb:** Admiral should dispatch sec-eng-council on any PR that *might*
touch the 8 checklist domains. Sec-eng-council DEFERS when it actually doesn't.
This optimizes for catch-rate over council efficiency.

---

## 3. DEFER beacon shape

Beacon filename: `coordination/inbox/council-verdict-<ts>-security-engineering-<workstream>-<event>-defer.md`

(The `-defer.md` suffix is canonical; mirrors `-spot-check.md` / `-stage-05.md`
suffixes for non-DEFER verdicts.)

Beacon body:

```markdown
---
type: council-verdict
council: security-engineering
workstream: <e.g., W#76 cohort-2 financial>
pr: <PR URL or hand-off ref>
verdict: DEFER
defer-target: <list of councils — e.g., ".NET-architect, frontend-architect">
defer-rationale: <one-line summary>
---

## Summary

<1 paragraph: why this PR is out-of-scope for sec-eng-council; which council(s)
or process should review instead>

## Out-of-scope evidence

- Checklist items skimmed: <e.g., "All 8 checks skimmed; PR diff touches only
  `shipyard/_shared/design/tokens/*.json` (design tokens). No Bridge endpoint,
  no substrate primitive, no audit event, no auth policy.">
- Diff coverage: <e.g., "100% of diff is in `_shared/design/` — pure PAO/Yeoman
  surface">

## Recommended route

- **Primary council**: <name> — for <reason>
- **Secondary council (if applicable)**: <name> — for <reason>
- **OR skip-review-justification**: <if recommending Admiral skip routing further>

## Admiral action required

- Re-dispatch to <named council>
- OR file `admiral-ruling-*-skip-review-*.md` if no further review needed

## Forward-watched concerns (informational; do NOT block merge)

<list; usually empty for DEFER>
```

---

## 4. DEFER use cases (canonical examples)

### 4.1 Pure design-token PR (PAO/Yeoman territory)

PR touches only `shipyard/_shared/design/tokens/*.json`. No Bridge endpoint changes.

→ **DEFER**; route to PAO/Yeoman review (no council needed; PAO ratifies designs).

### 4.2 Pure pattern-catalog hygiene PR

PR amends `shipyard/_shared/engineering/standing-approved-patterns.md` to add a
formal pattern entry. No code change.

→ **DEFER**; routes to Admiral for fleet-wide ratification (or as a no-review
informational doc).

### 4.3 Pure frontend a11y fix

PR fixes WCAG contrast ratios in `sunfish/apps/web/src/components/`. No backend or
auth change.

→ **DEFER**; routes to frontend-architect-council (if/when that council exists)
OR FED reviewer.

### 4.4 Documentation-only PR

PR updates `README.md` or `MIGRATION.md` text only.

→ **DEFER**; skip-review-justification.

### 4.5 Pattern-009 SPOT-CHECK on a route that ISN'T new

Per `feedback_pattern009_scope` memory: pattern-009 SPOT-CHECK triggers on NEW
routes, not on new event-type cases in existing dispatchers. A PR that adds an
event-type case to an existing dispatcher is out-of-scope.

→ **DEFER**; route to .NET-architect (event-type-cases are .NET-architect domain).

---

## 5. Frequency expectation

Based on retroactive analysis of cohort-1/2/3/4 PRs:

- ~70% of SPOT-CHECK dispatches: actually in-scope (verdict GREEN/AMBER/RED apply)
- ~20% partially in-scope (Bridge endpoint + non-security adjacent change; verdict
  GREEN/AMBER/RED on Bridge surface; non-security adjacent change noted as
  forward-watch)
- ~10% out-of-scope candidates (DEFER would apply)

Cohort-4+ adoption may shift this: with the Stage-05 Adversarial Brief surfacing
issues before Stage-06, sec-eng SPOT-CHECK gets dispatched only when truly relevant,
so DEFER frequency may drop to ~5%.

---

## 6. Update to sec-eng-council subagent definition

The companion change to `shipyard/.claude/agents/sec-eng-council.md`:

- Add DEFER as 4th verdict in the verdict-options enumeration
- Add §"DEFER verdict — when to use" section pointing to this design spec
- Update §"Output beacon format" with DEFER beacon shape
- Reference: ADR 0093 §"Council dispatch — trigger matrix" (no change to ADR
  needed; DEFER is a verdict refinement, not a protocol amendment)

PR shape: this design doc + the agent definition update + a single line in
`shipyard/_shared/engineering/standing-approved-patterns.md` referencing DEFER as
available (no new pattern entry; just a footnote in the existing sec-eng-council
section if one exists).

---

## 7. Cerebrum learning

Add to fleet cerebrum after PR merge:

> **sec-eng-council 4th verdict DEFER** (added 2026-05-22 per V8 #5):
> Out-of-scope PRs get DEFER, not GREEN. DEFER routes to named council OR
> skip-review-justification. Decision-tree in §2; canonical examples in §4.
> Beacon shape: `council-verdict-*-security-engineering-*-defer.md`.

Add to ONR's auto-memory after PR merge:
- `feedback_sec_eng_council_defer_verdict.md` — when DEFER applies; routing protocol

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T14-00Z` item V8 #5
2. V7 #7 sec-eng-council UPF follow-up (shipyard#112) — surfaced DEFER need
3. ADR 0093 (shipyard#104 MERGED)
4. sec-eng-council subagent definition (shipyard#107 MERGED)
5. `feedback_pattern009_scope` memory — pattern-009 scope precedent
6. Retroactive cohort-1/2/3/4 PR analysis (V7 batch findings)

---

## 9. What ONR does next

V8 #5 design doc + agent-def update committed together. PR opens. ONR proceeds
to V8 #6 (post-cohort-10 retrospective scaffolding).

— ONR, 2026-05-22T14:20Z
