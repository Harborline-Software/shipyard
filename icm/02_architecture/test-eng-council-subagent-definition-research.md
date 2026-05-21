# Test-Eng-Council subagent definition research (2026-05-21)

**Authored by:** ONR (V5 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #3) — feeds Admiral Phase 1 ADR 0093 authoring (Stage-05 Adversarial Review Protocol Amendment)
**Authored at:** 2026-05-21T14-35Z
**Status:** research feeding ADR 0093 drafting

---

## Scope

Per UPF audit recommendation: test-coverage authoring as a TRIGGERED role at Stage-05 gate; Sonnet 4.6 medium effort. Research the test-eng-council subagent's role, trigger conditions, output beacon shape, memory model.

---

## TL;DR

1. **Test-eng-council subagent purpose:** at Stage-05 hand-off review, verify the test-coverage scaffold meets the hand-off's stated acceptance criteria — not just security (sec-eng's domain) but functional + regression coverage. Catch test-coverage gaps before Stage-06 implementation starts.

2. **Differs from sec-eng-council:**
   - **sec-eng-council** — security surface; cross-tenant; CSRF; audit emission; cryptographic primitives. Threat-model lens.
   - **test-eng-council** — functional coverage; edge cases enumerated; happy-path + failure-path test count; regression coverage on legacy entities; performance benchmark expectations.
   - **Both dispatch in parallel** at the same Stage-05 gate; verdicts are independent.

3. **Trigger conditions:** Stage-05 hand-offs that have an Adversarial Brief section (R3 protocol) automatically dispatch sec-eng-council. Test-eng-council dispatches when:
   - The hand-off references >5 test cases as acceptance criteria (test-density threshold)
   - The hand-off has a substrate-touching PR (new repository contract, new endpoint family, new audit event type)
   - Cross-cluster integration tests are required (Phase 7 of WS-E substrate; cohort-N+1 integration with cohort-N closing)

4. **Output beacon shape:** `council-verdict-<timestamp>-test-engineering-<workstream>-spot-check.md` mirroring sec-eng/.NET-arch shape. Frontmatter `council: test-engineering (SPOT-CHECK)`.

5. **Memory model: stateless per dispatch.** Each Stage-05 review is independent; test-eng-council subagent reads the hand-off + companion PRs but has no cross-hand-off memory. (Differs from QM daemon which has persistent state.)

6. **First canonical dispatch candidate:** cohort-4 audit-trail viewer Stage-05 hand-off (shipyard#81; first canonical R3 Adversarial Brief instance). Test-eng dispatch verifies the ~12-14 test count + signature-verification badge state-machine coverage + drill-down legacy data graceful-degradation tests.

---

## 1. Concrete Stage-05 review checklist (test-eng-council subagent)

For each Stage-05 hand-off, the test-eng-council subagent verifies:

### Acceptance-criteria test enumeration

- Per-PR test count: hand-off names a minimum count per PR; subagent verifies the test names listed cover the canonical surface
- Happy-path + failure-path coverage: each surface has at least 1 happy + 1 failure case enumerated
- Cross-tenant tests for any IMustHaveTenant entity introduced
- Idempotency tests for any POST endpoint with Idempotency-Key (per pattern-012)
- Audit-emission tests for any new AuditEventType constant introduced

### Edge-case enumeration

- For each happy-path case, what's the analogous failure-path? Did the hand-off enumerate?
- For each new entity, what's the IMustHaveTenant interface compliance?
- For each new endpoint, what's the cross-tenant probe test (per cohort-2 hand-off precedent)?
- For each new POST, what's the antiforgery + idempotency-key test?

### Integration-test coverage

- Are integration tests scoped per cluster?
- Does the hand-off name the test-fixture project (e.g., `Sunfish.Blocks.X.IntegrationTests/`)?
- Are WireMock.NET cassettes (or equivalent) named per scenario?

### Regression-baseline check

- For any legacy entity touched (e.g., `Project`, `TaskItem`, `AuditRecord`), is there a "regression test on populated DB" requirement?
- Per ADR 0091 R2 §A5 amendment: populated-DB regression on every retrofit PR.

### Performance benchmark expectations

- Does the hand-off enumerate performance-relevant acceptance criteria? (e.g., "ListPayments returns within 200ms p95 at 10K rows")
- Are benchmark tests named?

### Test-fixture seeding pattern

- Are seed-data construction patterns documented? (e.g., per V1 #3 ADR 0091 Step 2.0 A5 test: "one row per known tenant + a sentinel row + a null-value row")

---

## 2. Differentiation from sec-eng-council

| Dimension | sec-eng-council | test-eng-council |
|---|---|---|
| Lens | Threat model | Coverage model |
| Primary focus | Cross-tenant; CSRF; audit emission; crypto primitives | Test enumeration; failure-path; regression; performance |
| Verdict shape | GREEN / AMBER / RED with security-specific items | GREEN / AMBER / RED with coverage-specific items |
| Dispatch trigger | Stage-05 R3 Adversarial Brief always; pattern-009 SPOT-CHECK | Stage-05 hand-off with >5 tests OR substrate-touching OR cross-cluster |
| Frontmatter `council:` | `security-engineering (SPOT-CHECK)` | `test-engineering (SPOT-CHECK)` |
| Output filename | `council-verdict-<ts>-security-engineering-*` | `council-verdict-<ts>-test-engineering-*` |
| Model + effort | Opus 4.7 + xhigh (per CIC ratification) | **Sonnet 4.6 + medium** (per UPF audit recommendation; lower-cost test review) |
| Memory across dispatches | None (stateless) | None (stateless) |
| SLA per V5 #8 | 30-60 min dispatch + 30-60 min response | 30 min dispatch + 30 min response (faster; lower review density) |
| Overlap | sec-eng covers security tests | test-eng covers ALL other tests; no overlap |

**Parallel dispatch:** for substrate Stage-05 hand-offs (e.g., cohort-4 audit-trail viewer), BOTH councils dispatch in parallel. Verdicts are independent + non-blocking on each other.

---

## 3. Trigger conditions (decision matrix)

```
Stage-05 hand-off filed →
    Has R3 Adversarial Brief? → DISPATCH sec-eng-council
    Has >5 test cases? → DISPATCH test-eng-council
    Has substrate touch (new entity / new endpoint / new audit event)? → DISPATCH test-eng-council
    Cross-cluster integration tests required? → DISPATCH test-eng-council
    Neither? → Stage-05 advisory only; no council dispatch
```

Typical pattern for cohort hand-offs:
- Cohort-1 PR 1 (Properties; pure GET) → sec-eng only (Adversarial Brief; <5 tests)
- Cohort-2 PR 3 (RentCollection POST; CSRF + audit) → BOTH (R3 + multi-PR substrate)
- Cohort-3 PR 1 (RentRoll v2 rewrite) → BOTH (cartridge integration + cross-cluster)
- Cohort-4 audit-trail viewer Stage-05 → BOTH (per shipyard#81 with R3 Adversarial Brief + 12-14 tests)

---

## 4. Output beacon shape

Per fleet-conventions §"Beacon naming" + V4 #6 recommendations:

```
council-verdict-<timestamp>-test-engineering-<workstream>-spot-check.md
```

Frontmatter:

```yaml
---
type: council-verdict
council: test-engineering (SPOT-CHECK)
workstream: <e.g., W#78 cohort-4 audit-trail viewer>
pr: <e.g., shipyard#81>
verdict: GREEN | AMBER | RED
---
```

Body sections:
1. Summary verdict (1 paragraph)
2. Per-item review (typically 5-10 items mirroring sec-eng verdict shape)
3. Blockers (verbatim per RED items)
4. Conditional concerns (AMBER items)
5. Forward-watched concerns (informational)

---

## 5. Memory model — stateless per dispatch

Each Stage-05 review is independent. Subagent reads:
- The hand-off file
- Companion PRs (if any)
- Referenced ADRs
- Cerebrum (auto-load)
- Fleet-conventions (auto-load)

But does NOT carry memory across dispatches:
- Subagent N+1 doesn't know what subagent N saw
- Each dispatch is fresh; reduces cognitive-context risk; eases parallel dispatch

**Coordination via beacons** — if subagent N flagged a gap that needs cross-hand-off attention, it does so via the verdict beacon's "Forward-watched concerns" section. Subsequent dispatches can reference prior verdicts via inbox search.

---

## 6. First canonical dispatch candidate

**Cohort-4 audit-trail viewer Stage-05 hand-off (shipyard#81) — first canonical test-eng-council dispatch.**

Per V3 #1 cohort-4 hand-off:
- ~12-14 integration tests across the 4 PRs
- New `IAuditEventReader` substrate primitive (if not existing)
- Cross-tenant probe tests (Decision 7 in Adversarial Brief)
- Signature-verification badge state-machine (3 states; FED PR 2)
- Drill-down legacy-data graceful-degradation tests (Decision 3)
- CSV export DoS-protection tests (Decision 6)

These shape match the test-eng dispatch trigger (>5 tests + substrate touch + cross-cluster).

ADR 0093 Phase 4 decision-gate criteria can name cohort-4 as the canonical pilot.

---

## 7. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council (informational)

1. Test-eng-council Sonnet 4.6 medium effort — confirm appropriate (vs Opus 4.7 if review density warrants)

### For security-engineering council

1. **Overlap clarification** — sec-eng's "Item 9 audit-emission integration test" (per W#68 PR 3 verdict) — is this sec-eng territory OR test-eng territory? Boundary needs clarification.

### For CIC (ADR 0093 input)

1. Test-eng-council subagent definition — confirm shape per this research
2. Dispatch trigger criteria (>5 tests OR substrate OR cross-cluster) — refine?
3. Pilot in cohort-4 audit-trail viewer Stage-05 review — confirm

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #3
2. UPF audit recommendation (referenced by directive); `onr-status-2026-05-21T1159Z-upf-audit-security-officer-agent-proposal.md`
3. V3 #4 Adversarial Brief template prototype (shipyard#78)
4. V3 #1 cohort-4 Stage-05 hand-off (shipyard#81) — first canonical pilot
5. V5 #8 SPOT-CHECK SLA historical analysis (shipyard#89) — SLA refinement for parallel dispatch
6. `.claude/agents/` — existing council subagent definitions (sec-eng + .NET-arch precedents)

---

## 9. What ONR does next

V5 #3 deliverable complete. Files `onr-status-*-v5-item-3-test-eng-council-research-complete.md`. Proceeds to V5 #5 (ADR 0091 Step 5+6 pre-research) per Admiral resequencing.

— ONR, 2026-05-21T14:35Z
