# signal-bridge#37 PR description amendment — scaffold for Engineer apply

**Authored by:** ONR (V12 batch item #1)
**Requester:** Admiral (per `admiral-directive-2026-05-22T18-25Z` item V12 #1; per V11 #1 split ruling)
**Authored at:** 2026-05-22T18-35Z
**Target:** Engineer applies this scaffold directly to signal-bridge#37 PR description

---

## Purpose

Per V11 #1 (shipyard#124) pattern-012 canonical framing research recommendation
Option B (split), signal-bridge#37's PR description must be amended to:

1. Replace `pattern-012-financial-write-path (3rd-instance ratification trigger)`
   tag with `pattern-012b-accountant-grade-write-path (1st instance)`
2. Reference the V11 #1 framing research + Admiral ruling (forthcoming)
3. Acknowledge the 3 outstanding A1-A3 sec-eng amendments still pending

ONR provides the scaffold below. **Engineer applies directly to signal-bridge#37
via `gh pr edit 37 --body "..."` or PR edit UI**; ONR does NOT push to the PR.

---

## Section 1 — Pattern claim amendment (replace existing line)

**Current pattern claim in signal-bridge#37 description:**

```markdown
@candidate-pattern: pattern-012-financial-write-path (3rd-instance ratification trigger)
@deviation-from-spec: CSRF inlined (per pattern-012 Q1 ONR recommendation; ratification adopts INLINED form — CIC ratifies the form change at promotion).
```

**Replace with:**

```markdown
@standing-pattern: pattern-009 (Bridge endpoint + frontend rebind pair — formal)
@candidate-pattern: pattern-012b-accountant-grade-write-path (1st instance per V11 #1 split ruling)
@deviation-from-spec: AccountantPolicy registered but role-claim enforcement is forward-watch (the auth substrate does not yet surface role claims through the policy layer; the handler asserts the Accountant role via ITenantContext.Roles for the 403 surface). Inline forward-watch comment in AccountantPolicy.cs cites the deferred work.
@deviation-from-spec: IChartCatalogService.GetDefaultChartIdAsync used in lieu of the spec's hypothetical ResolveChartAsync(tenantId, chartCode) (multi-chart-per-tenant is V2 #5 forward-watch). v1 substrate exposes the default chart only; the request's chart_code is accepted as "the tenant's default" for ratification.
```

**Rationale:** Per V11 #1 (shipyard#124) Option B split, pattern-012 split into:
- `pattern-012a-tenant-scoped-write-path` — separated CSRF; no idempotency
- `pattern-012b-accountant-grade-write-path` — inlined CSRF; mandatory Idempotency-Key

signal-bridge#37 is the 1st instance of pattern-012b (NOT the "3rd-instance
ratification trigger" of pattern-012 — that framing was incorrect; the 2nd
instance was unaccounted-for per .NET-architect verdict 2026-05-22T1234Z).

---

## Section 2 — References to add to PR description

Add a "## References" section (or amend existing):

```markdown
## References

- **V11 #1 pattern-012 framing research** — `shipyard#124` (canonical framing for the
  pattern-012 split; recommended Option B = split into -012a + -012b sub-patterns)
- **Admiral ruling** — `admiral-ruling-2026-05-22T18-25Z-pattern-012-split-option-b-approved.md`
  (pending Admiral authoring per V11 question routing; reference forward when ruling lands)
- **.NET-architect council verdict** — `council-verdict-2026-05-22T1234Z-net-architect-...`
  (original RED-flag on pattern-012 framing; motivated V11 #1 split research)
- **Sibling shipyard PR** — `shipyard#113` (foundation-idempotency substrate;
  pattern-012b idempotency-key store dependency)
- **ADR 0092 §A6** — canonical 5-field audit payload (substrate emission)
- **ADR 0094** — IAuditEventReader (audit-trail viewing; consumes events emitted
  by this PR's handler)
- **V10 #2 audit-payload canonical research** — `shipyard#122` (5-field substrate
  canonical confirmation)
```

---

## Section 3 — Outstanding A1-A3 sec-eng amendments acknowledgment

Engineer acknowledges 3 sec-eng amendments outstanding (per cohort-2 + cohort-4
SPOT-CHECK precedent, sec-eng-council typically AMBER-flags on first review).
Apply per signal-bridge#36 precedent:

```markdown
## Outstanding sec-eng amendments (apply per signal-bridge#36 precedent)

When sec-eng-council SPOT-CHECK fires on this PR Ready-flip, expect three A-level
findings to close in this PR:

- **A1 — Cross-tenant TenantBoundaryViolation emission**: verify handler emits
  TBV per ADR 0092 §A6 canonical 5-field shape when chart-probe returns
  `chart_not_found` on cross-tenant attempt. Engineer authoring sequence:
  1. handler emits TBV via IAuditTrail.AppendAsync before returning 400
  2. test asserts emission with canonical 5-field payload (entity_type=Chart,
     entity_id=chart_code, requested_tenant=ctx, actual_tenant=chart-owner,
     correlation_id)
  3. test uses V10 #2 + V11 #1 + V10 #2 + V11 #2 reference impl pattern

- **A2 — Idempotency-Key charset + length guards**: verify guard rejects malformed
  Idempotency-Key headers at substrate boundary BEFORE consuming bandwidth.
  Apply pattern-012b candidate signature §3 invariants:
  - Format: UUID-v4 OR 32-128 alphanumeric chars
  - Length: 32-128 (mandatory cap; per V10 #1 PR #5 spec)
  - Test: malformed key returns 400 (NOT 422 — format guard, not validation)

- **A3 — ProblemDetails.Status RFC 7807 conformance**: every error surface (400,
  403, 409, 422) returns ProblemDetails per RFC 7807; verify status field matches
  HTTP status code (not free-form). Apply per cohort-4 hand-off §4.2 precedent.

Each amendment should be:
1. Applied to handler (1 code change per amendment; ~10-30 LOC each)
2. Tested (1-2 new tests per amendment)
3. Re-attested by sec-eng-council after fold
4. Documented in PR description as "Closes A1/A2/A3 amendments"
```

---

## Section 4 — Acceptance criteria template (PR-description "Test plan" section update)

Engineer can extend the existing "Test plan" with these acceptance criteria
once amendments are applied:

```markdown
## Acceptance criteria

### Pattern-012b canonical conformance (V11 #1 split ruling)

- [x] Tenant-derived server-side from ITenantContext; DTO carries no tenant_id ✓
- [x] CSRF inlined in body; antiforgery validation fail-fast before all other validation ✓
- [x] Mandatory Idempotency-Key header (400 if absent) — A2 amendment scope
- [x] Idempotency scope = (key, tenant); cross-tenant replay isolation ✓
- [x] Canonical 7-field audit payload at Bridge layer ✓
- [x] AccountantPolicy 403 surface ✓
- [x] Closed-period 422 surface ✓
- [x] Exact decimal DR/CR comparison ✓
- [x] 6 typed error surfaces: 201 / 200-idempotent / 400 / 403 / 409 / 422 ✓

### V10 #2 / V11 #1 canonical audit shape

- [ ] A1 — Cross-tenant TBV emission with canonical 5-field payload (substrate
      emission shape; NOT to be confused with the 7-field Bridge-layer
      JournalEntryPosted emission, which is a different event type)

### V10 #1 / V10 #2 idempotency hardening (forward-watch on signal-bridge#37)

- [ ] A2 — Idempotency-Key charset + length guards at substrate boundary

### RFC 7807 conformance

- [ ] A3 — All error surfaces return ProblemDetails with status field matching HTTP code
```

---

## Section 5 — Forward-watches to note

Add a "## Forward-watches" section:

```markdown
## Forward-watches (post-merge)

- **Pattern-012b 3rd-instance ratification**: this PR is 1st instance of pattern-012b.
  2nd-instance candidates: future AP invoice posting, journal void/reverse, payment
  capture, etc. Per V12 #2 forward-watch protocol (ONR research forthcoming).
- **AccountantPolicy role-claim enforcement**: auth substrate role-claim integration
  is deferred; handler asserts via ITenantContext.Roles inline today.
- **Multi-chart-per-tenant ResolveChartAsync**: V2 #5 forward-watch; v1 substrate
  exposes default chart only.
- **Cursor signing layer choice (V11 #2 §5.4)**: Bridge-layer signing recommended
  per V11 #2 §5.4; Engineer V3 #1 (IAuditEventReader) integration will codify.
- **EventLogBackedAuditEventReader (ADR 0094 Step 2)**: fires post-Engineer V3 #1
  Step 1 in-memory implementation merge.
```

---

## Section 6 — Total amendment LOC estimate

- Section 1 (pattern claim): ~3 lines edited
- Section 2 (references): ~10 lines added
- Section 3 (sec-eng amendments): ~30 lines added (acknowledgment + scope)
- Section 4 (acceptance criteria): ~15 lines added/refined
- Section 5 (forward-watches): ~10 lines added

**Total PR description amendment: ~70 lines added/edited.**

---

## Engineer apply instructions

```bash
# Engineer pulls latest signal-bridge#37 PR body
gh pr view 37 --repo Harborline-Software/signal-bridge --json body --jq '.body' > /tmp/sb37-current.md

# Apply scaffold per sections 1-5 above; output to /tmp/sb37-amended.md

# Push amendment
gh pr edit 37 --repo Harborline-Software/signal-bridge --body-file /tmp/sb37-amended.md
```

ONR does NOT push to the PR; Engineer applies via direct edit.

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T18-25Z` item V12 #1
2. V11 #1 pattern-012 canonical framing research (shipyard#124)
3. V10 #1 Engineer ladder PR-by-PR specs (shipyard#121) — pattern-012b signature reference
4. V10 #2 audit-payload canonical research (shipyard#122) — 5-field
5. V11 #2 ADR 0094 Step 1 consultation (shipyard#127) — cursor signing layer
6. V11 #3 InMemoryMaintenanceService migration scope (shipyard#125) — substrate emission pattern
7. ADR 0092 §A6 + ADR 0094 + ADR 0046 + ADR 0049
8. signal-bridge#36 (CLOSED; precedent for sec-eng amendment apply cycle)
9. signal-bridge#37 (OPEN; this PR target)

---

## What ONR does next

V12 #1 scaffold complete. Engineer applies. ONR proceeds to V12 #2 (pattern-012a/b
3rd-instance forward-watch protocol).

— ONR, 2026-05-22T18:35Z
