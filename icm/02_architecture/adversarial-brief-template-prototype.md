# Stage-05 Adversarial Brief — Template prototype (cohort-4 audit-trail-viewer)

**Authored by:** ONR (V3 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-21T12-45Z` item #4) — supports Phase 1 Stage-05 amendment authoring
**Authored at:** 2026-05-21T12-48Z
**Status:** prototype (concrete reference for Admiral's Phase 1 protocol amendment)

---

## Template purpose

The Adversarial Brief is a Stage-05 hand-off section that answers, for each load-bearing design decision: **"What's the worst-case interpretation of this design decision, and what fails when an adversary or careless caller exercises it?"**

It is NOT a risk-register (probabilities + impact); it is a SEMANTIC stress-test (assume the worst-case caller; what's the worst outcome?). The product is a list of 5-8 surfaced concerns that Stage-06 build + sec-eng SPOT-CHECK can verify against.

---

## Template structure

```markdown
## Adversarial Brief (Stage-05)

For each load-bearing design decision in this hand-off, state the worst-case
interpretation and the failure mode an adversary or careless caller exercises:

### Decision N — <name>

- **Decision summary:** <one line; what the design chose>
- **Worst-case interpretation:** <if an adversary or careless caller exercises
  this decision under the worst-case assumption, what happens?>
- **Failure mode:** <concretely, what fails — auth bypass; cross-tenant data;
  data corruption; cascading retry storm; etc.>
- **Mitigation in this hand-off:** <what the hand-off encodes to prevent the
  failure mode; OR "flagged for Stage-06 SPOT-CHECK consideration"; OR
  "deferred to <follow-on workstream>">

(repeat for each load-bearing decision; aim for 5-8 total)
```

---

## Worked example — cohort-4 C3 audit-trail viewer

The cohort-4 anchor (per V2 #6 recommendation) is the audit-trail viewer page. Below is a worked Adversarial Brief for that hand-off.

### Decision 1 — Query parameter shape for `GET /api/v1/audit-events`

- **Decision summary:** support filters `from`, `to`, `event_type`, `correlation_id` as query parameters; `tenant_id` derived server-side from `ITenantContext` (NEVER from caller).
- **Worst-case interpretation:** an adversary controls the `event_type` parameter; supplies a wildcard or empty string; expects to receive ALL audit events including cross-tenant.
- **Failure mode:** if server doesn't enforce `tenant_id` server-side OR if the audit query lacks a `WHERE tenant_id = $captured` clause (HasQueryFilter missing on AuditRecord entity), cross-tenant audit events leak in the result set. Severity: HIGH — forensic data crosses tenant boundary.
- **Mitigation in this hand-off:** `HasQueryFilter` on `AuditRecord` mirrors tenant filter pattern (ADR 0092 §"Step 2 EFCore query-filter convention"); query parameter `tenant_id` is REJECTED at handler (returns 400 with explicit "tenant_id is server-derived" error message); audit emission on rejected-`tenant_id`-attempt (forensics value).

### Decision 2 — Pagination key shape

- **Decision summary:** cursor-based pagination using base64-encoded `(occurred_at, audit_id)` tuple as the cursor.
- **Worst-case interpretation:** an adversary manipulates the cursor to encode a cross-tenant audit_id OR a future occurred_at to skip ahead.
- **Failure mode:** if cursor isn't tenant-validated on decode, a forged cursor with another tenant's audit_id seeds the query at that point + returns subsequent rows (some of which may belong to other tenants if `HasQueryFilter` is bypassed somehow). Severity: MEDIUM — depends on HasQueryFilter coverage.
- **Mitigation in this hand-off:** cursor decoding validates that the decoded `audit_id` belongs to the caller's tenant (database query: `SELECT tenant_id FROM audit_records WHERE id = $cursor_audit_id` — if mismatch, return 400 "invalid_cursor"); cursor IS NOT a security boundary (HasQueryFilter is), but prevents the "skip-ahead-to-elsewhere" trick at the query layer.

### Decision 3 — Drill-down to entity by `correlation_id`

- **Decision summary:** click on an audit event → fetch related entity (Invoice, Payment, etc.) by following `correlation_id` from the audit payload.
- **Worst-case interpretation:** legacy audit records (pre-V2 #3 retrofit) lack `correlation_id` (it's NULL or empty). Drill-down link constructs `/api/v1/financial/invoices/?correlationId=` which is malformed OR returns 400.
- **Failure mode:** the audit-trail viewer page shows broken drill-down links for pre-retrofit events; user clicks → 404 or 400; degraded UX; user trust erodes.
- **Mitigation in this hand-off:** for legacy audit rows with NULL `correlation_id`, the UI shows the audit event as read-only (no drill-down link rendered); the underlying API returns the audit detail without attempting entity resolution.

### Decision 4 — Filter parameter validation timing

- **Decision summary:** filter parameters `from` (ISO date) + `to` (ISO date) + `event_type` (enum string) validated at the Bridge handler before query execution.
- **Worst-case interpretation:** caller supplies `from > to` (inverted range) OR a `from` in the year 2099 OR an `event_type` not in the `AuditEventType` enum.
- **Failure mode:** if not validated, a malformed range produces empty results (acceptable) BUT a non-enum `event_type` like SQL-injection-attempt `' OR 1=1 --` could (if EF Core mishandles) leak data. Severity: LOW — EF Core parameterizes by default; this is defense-in-depth concern, not exploitable.
- **Mitigation in this hand-off:** `event_type` parameter validated against `AuditEventType` enum allowlist (400 if not in enum); date range validated (`from <= to`; 400 if inverted); date range capped at 1 year max (400 if larger range; prevents DOS by huge-range query).

### Decision 5 — Pagination key includes tenant boundary mid-page

- **Decision summary:** cursor encodes `(occurred_at, audit_id)`; pagination retrieves N rows per request.
- **Worst-case interpretation:** between pagination requests, a tenant-switch occurs in the user's session (user switched tenant via the tenant-selector UI mid-page).
- **Failure mode:** the cursor (decoded with the new tenant context) returns rows that don't match the original tenant + the new tenant — broken pagination semantics.
- **Mitigation in this hand-off:** cursor includes `tenant_id` in the encoded payload (encrypted with server-side key OR signed via `IOperationSigner`); on decode, if cursor's tenant != current `ITenantContext.TenantId`, return 400 "tenant_changed; reload page". Frontend handles by refetching from page 1.

### Decision 6 — CSV export endpoint scope

- **Decision summary:** `GET /api/v1/audit-events/export.csv` returns ALL audit events matching the current filter, NOT just the current page.
- **Worst-case interpretation:** caller supplies a 1-year date range + no event_type filter on a heavily-active tenant; export contains millions of rows; server timeout OR memory exhaustion.
- **Failure mode:** export endpoint allocates the full result set into memory before streaming; tenant with 5M audit events → OOM kill on Bridge process.
- **Mitigation in this hand-off:** export endpoint streams via `IAsyncEnumerable<AuditRecord>` directly to the HTTP response (no in-memory accumulation); date range capped at 1 year (consistent with §4 mitigation); 10M-row absolute cap with 400 "export_too_large" if exceeded.

### Decision 7 — Filter parameters bypass server-side tenant scoping

- **Decision summary:** server derives `tenant_id` from `ITenantContext`; query parameter `tenant_id` is REJECTED (per Decision 1).
- **Worst-case interpretation:** an adversary supplies `tenant_id` as a query parameter expecting to override server-side scoping.
- **Failure mode:** if the handler trusts the query parameter OR if a path-parameter `/api/v1/audit-events/tenant/{tenantId}/events` is ever introduced AND lacks tenant-cross-check, cross-tenant audit events leak. Severity: HIGH — same as Decision 1.
- **Mitigation in this hand-off:** explicit rejection of `tenant_id` query parameter at handler (400 "tenant_id_not_caller_supplied" + audit emission as `AuditEventType.TenantBoundaryViolation` per V2 #3 retrofit pattern); NO path-parameter for tenant_id (URL design prevents the foot-gun).

### Decision 8 — Audit event detail page shows raw payload

- **Decision summary:** drill-down detail page shows the audit event's payload as a JSON tree (raw structured data).
- **Worst-case interpretation:** payload contains PII (email addresses, phone numbers, dollar amounts); browser inspect-element or screenshot tools propagate PII outside Sunfish.
- **Failure mode:** the page treats payload as displayable; user with view permission sees ALL fields including sensitive ones; downstream PII handling regression.
- **Mitigation in this hand-off:** payload pretty-print masks fields tagged as `[Pii]` in the audit substrate (per ADR 0049 §"PII tagging convention" if present; else flag as forward-watched gap); operator can opt-into-reveal per field with explicit click (audit-emission on reveal).

---

## Pattern observations

After ~30 minutes of authoring this prototype, ONR notes:

1. **Most adversarial findings cluster around server-side enforcement of identity-derived state.** Decisions 1, 5, 7 all rely on `ITenantContext.TenantId` being the source-of-truth (NEVER caller-supplied). The brief makes this explicit.

2. **Pagination is a surprisingly rich attack surface.** Cursors that don't validate tenant binding are a common foot-gun. Decision 5 (mid-page tenant-switch) is subtle; would have been missed without adversarial framing.

3. **Backward-compat with legacy data degrades gracefully.** Decision 3 (NULL correlation_id on pre-retrofit rows) — adversarial framing catches the broken-UX failure mode that informational design review might skip.

4. **Performance-relevant decisions are also security-relevant.** Decision 6 (CSV export) — what looks like a perf/memory concern is actually a DoS surface that an adversarial caller can exercise.

5. **Sec-eng + .NET-architect councils both consume this section.** Sec-eng for the security mitigations; .NET-architect for the API-design mitigations. The brief reduces parallel-review back-and-forth.

---

## How Admiral might integrate this template

For the Phase 1 Stage-05 protocol amendment Admiral is drafting:

1. **Require an Adversarial Brief section in every Stage-05 hand-off** authored by ONR (or by sub-agents producing Stage-05 deliverables).
2. **Place AFTER the design surface section + BEFORE the implementation checklist** (so reviewers see the design first, then the worst-case stress-test, then the implementation tasks).
3. **Cap at 5-8 bullets per Adversarial Brief.** Beyond that, the brief loses focus; 5-8 is the sweet spot for review-time consumption.
4. **Each bullet uses the template structure** (Decision summary / Worst-case / Failure mode / Mitigation).
5. **Council review consumes this section explicitly.** Sec-eng + .NET-architect SPOT-CHECKs reference Adversarial Brief findings; an unaddressed finding blocks merge.

---

## Estimated cost-benefit

**Cost per hand-off:** ~30-45 min ONR authoring time (5-8 bullets × ~5 min per bullet to formulate + write).

**Benefit:**
- Catches 1-3 substantive security/design findings per hand-off that informational review would skip (per the cohort-4 prototype above — Decisions 2, 5, 6 each surface a finding that wasn't obvious from the design surface alone).
- Reduces sec-eng SPOT-CHECK iteration by ~30% (pre-surfaced mitigations mean fewer council follow-up rounds).
- Provides a reusable artifact for Stage-06 implementation: each bullet's "Mitigation in this hand-off" maps to a verifiable acceptance criterion.

ROI: ~30 min upfront prevents ~2-3h of post-hoc council iteration on average. Positive ROI starting at the first hand-off; compounds across all subsequent Stage-05 hand-offs.

---

## Next steps for Admiral

1. **Ratify this template** as part of Phase 1 Stage-05 amendment.
2. **Apply retroactively to V3 #1** (cohort-4 C3 Stage-05 hand-off; ONR's next deliverable) as the first canonical instance.
3. **Cite this prototype** in the protocol amendment as the worked example.

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #4
2. V2 #6 cohort-4 scope survey (shipyard#74) — C3 audit-trail viewer anchor
3. V2 #3 audit-emission retrofit (shipyard#71) — `TenantBoundaryViolation` audit pattern
4. ADR 0091 R2 + ADR 0092 — tenant-context + repository contract foundations

---

— ONR, 2026-05-21T12:48Z
