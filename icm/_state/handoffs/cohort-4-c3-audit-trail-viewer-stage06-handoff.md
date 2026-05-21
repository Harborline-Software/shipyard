---
title: Cohort-4 C3 Audit-Trail Viewer — Stage 06 Hand-off
workstream: TBD (ONR recommends W#78; verify W#78 slot empty before claiming per `feedback_never_add_workstream_rows_directly_to_ledger`)
cluster: anchor-react-cohort-4 (cross-package: `signal-bridge/Sunfish.Bridge/Audit/` + `sunfish/apps/web/src/api/audit-trail.ts` + `sunfish/apps/web/src/pages/AuditTrailPage.tsx`)
pipeline: sunfish-feature-change
authored-by: ONR
authored-at: 2026-05-21T13-00Z
status: ready-to-author (Engineer prereq PR 0 can start when V2 #3 audit-emission retrofit PR merges; FED PRs gated on Engineer PR 0)
co-pre-authorized: requested
co-pre-authorized-rationale: |
  Cohort 4 anchor (C3 audit-trail viewer per V2 #6 research recommendation). 4 PRs
  total: 1 Engineer prereq + 3 FED. Pattern-009 formal applies to all 4 (cluster-
  endpoint rebind shape). The audit-trail viewer consumes the V2 #3 audit-emission
  retrofit's TenantBoundaryViolation events plus existing AuditEventType events;
  no new substrate primitives required. Engineer prereq is small (~2-3h read-side
  query endpoint). FED is mechanical mirror of cohort-2 PR 1/2 pattern.
co-pre-authorized-scope:
  - Engineer PR 0 (`GET /api/v1/audit-events` Bridge endpoint family) — sec-eng SPOT-CHECK MANDATORY (forensics surface; tenant-scoping invariant; cursor validation; CSV export DoS protection)
  - FED PR 1 (`sunfish/apps/web/src/api/audit-trail.ts` + `AuditTrailPage.tsx` table view + filter UI + CSV export) — pre-auth; pattern-009 formal
  - FED PR 2 (`AuditEventDetailPage.tsx` drill-down detail + signature verification badge + payload pretty-print) — pre-auth; pattern-009 formal
  - FED PR 3 (close-out: docs running log + ledger flip + Playwright CDP E2E smoke extension) — pre-auth; CIC sees regardless (ledger-flip PR)
  - PR-count maximum: 4 (workstream re-evaluation if scope grows)
  - PR-deviation flag triggers immediate CIC escalation
merge-tier: pre-authorized-pending-CIC-ratification
depends-on:
  - V2 #3 audit-emission Bridge retrofit (shipyard#71 research; Engineer implementation PR) — produces the `TenantBoundaryViolation` audit events the viewer surfaces. NOT strictly blocking (viewer can show existing audit events without TBV events); FOR FULL DEMO VALUE, audit retrofit should ship first.
  - ADR 0049 (audit substrate) — `IAuditTrail` + `AuditRecord` + `AttestingSignature` shapes
  - ADR 0091 R2 (Accepted) — `Foundation.MultiTenancy.ITenantContext` for server-side tenant derivation
  - Cohort-1 W#74 — `AuthenticatedTenantPolicy` precedent (reused; do NOT re-introduce)
  - Cohort-2 W#76 / Cohort-3 W#77 — pattern-009 formal precedent
  - V3 #4 Adversarial Brief template prototype (shipyard#78) — FIRST canonical instance applies here (§"Adversarial Brief" section below)
spec-source: |
  - `coordination/inbox/admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #1 — parent V3 directive
  - V2 #6 cohort-4 scope survey (shipyard#74) — C3 anchor recommendation + ranking
  - V2 #3 audit-emission Bridge retrofit research (shipyard#71) — substrate that this consumes
  - V3 #4 Adversarial Brief prototype (shipyard#78) — 8-bullet worked example for THIS hand-off
  - cohort-3 hand-off (shipyard#51) — structural template
  - ADR 0049 (audit substrate) — `IAuditTrail` interface + `AuditRecord` shape
estimated-effort: ~6-9h dev across 4 PRs (~2-3h Engineer PR 0; ~2-3h FED PR 1; ~1-2h FED PR 2; ~1h FED PR 3 close-out)
PR-count: 4
pre-merge-council:
  security-engineering: SPOT-CHECK MANDATORY on Engineer PR 0 (forensics surface; tenant-scoping invariant; cursor validation; CSV export DoS protection). NOT required on FED PR 1/2/3 (mechanical mirror under pattern-009 formal; sec-eng's surfaces are server-side).
  dotnet-architect: NOT required (Engineer PR 0 is read-side endpoint following the cohort-1/cohort-2 cluster-endpoint pattern; no novel architecture)
  frontend-reviewer: NOT required (mechanical TanStack rebind + table view; no new state-management or UI primitives)
license-posture: MIT clean-room
---

# Hand-off — Cohort-4 C3 Audit-Trail Viewer (Anchor React)

**From:** ONR (Office of Naval Research)
**To:** Engineer (for prereq PR 0 ONLY) + FED (for PR 1-3)
**Workstream:** TBD — ONR recommends **W#78**; register the source `W78-anchor-react-cohort-4-c3-audit-trail-viewer.md` row in `shipyard/icm/_state/workstreams/` and re-render the ledger before kickoff.
**Pipeline:** `sunfish-feature-change`
**Ratifications applied:**
- CIC 2026-05-17T14-30Z Tauri-first pivot (cohort-1/2/3 inherited)
- CIC ratified roadmap Q1: tenant scoping server-derived from `ITenantContext`
- CIC ratified roadmap Q3: per-page PR bundling
- Admiral V3 dispatch 2026-05-21T12-45Z item #1 (this hand-off authoring)
- V3 #4 Adversarial Brief template (this hand-off is the FIRST canonical instance per directive § "Apply R3+R4+R5")

---

## 1. Context

### 1.1 Why cohort-4 ships next + why audit-trail viewer wins anchor

Per V2 #6 cohort-4 scope survey (shipyard#74) the anchor recommendation is **C3 — Audit-trail viewer** (ranked 18/21 on the candidate matrix):

- **Substrate-ready:** ADR 0049 `IAuditTrail` exists; V2 #3 audit-emission retrofit adds `TenantBoundaryViolation` events
- **High MVP-demo unblock value:** compliance story — "every cross-tenant probe + every tenant action is cryptographically signed + queryable"
- **Effort-light:** ~6-9h Engineer + FED side; no substrate work
- **Convergence:** W#60 P4 PR 2 + pattern-012 ratification (per V3 #2 design doc); cohort-4 audit viewer renders JournalEntry events with pattern-012-compliant payload structure

### 1.2 What cohort-4 ships

| # | PR | Subject |
|---|---|---|
| 0 (Engineer; prereq) | Engineer PR 0 | `GET /api/v1/audit-events` Bridge endpoint family at `signal-bridge/Sunfish.Bridge/Audit/`; consumes existing `IAuditTrail` (read side; new `IAuditEventReader` if missing per Engineer pre-flight) |
| 1 | FED PR 1 | `sunfish/apps/web/src/api/audit-trail.ts` shared TypeScript client + `AuditTrailPage.tsx` (paginated table; filter UI for from/to/event_type/correlation_id; CSV export button) |
| 2 | FED PR 2 | `AuditEventDetailPage.tsx` (drill-down to single event; signature verification badge state machine; payload pretty-print JSON tree view with `[Pii]` masking) |
| 3 | FED PR 3 | Close-out: `apps/docs/anchor/cohort-4-audit-trail-viewer.md` (new docs page) + Playwright CDP E2E smoke extension + W#78 ledger flip |

### 1.3 What cohort-4 does NOT ship

- **Cross-chart audit filtering** — multi-chart-per-tenant is demand-driven (per V3 #5 activation gate); audit-trail viewer ships single-chart aware
- **Audit event reversal / redaction** — sensitive PII handling is forward-watched (per Adversarial Brief Decision 8 below)
- **Real-time audit stream** — viewer is paginated query, NOT live tail; SignalR / WebSocket streaming is C6 cohort candidate (V2 #6)
- **Cross-tenant admin view** — super-admin tenant-spanning audit visibility is C2 (multi-tenant admin surface; V2 #6 forward)
- **AP Aging page (cohort-3 forward-watch)** — separate cohort-N+ scope when AP Aging cartridge ships

### 1.4 Auth + CSRF + audit conventions

- **Auth:** `AuthenticatedTenantPolicy` (reused from cohort-1 PR 1; same as cohort-2 + cohort-3)
- **Tenant scoping:** server-derived from `ITenantContext.TenantId`; `tenant_id` query parameter is REJECTED (returns 400 + audit emits TenantBoundaryViolation per V2 #3 retrofit pattern)
- **CSRF:** NOT REQUIRED (read-only GET endpoints)
- **Audit:** the viewer page itself doesn't emit audit events for queries (audit of audit-viewer access is a forward-watched item — out-of-scope for cohort-4)
- **Standing pattern:** `pattern-009` formal — all 3 FED PRs ship under this for cluster-endpoint rebind

### 1.5 Why CIC sees PR 3 regardless of pre-auth

PR 3 is the **ledger-flip PR** for W#78. Per pre-authorization ruling §Step 4: ledger-flip PR is one of the items CIC always sees.

---

## 2. Adversarial Brief (R3 protocol — FIRST canonical instance)

Per V3 #4 Adversarial Brief template prototype (shipyard#78). 8 worked-example bullets per the prototype's cohort-4 example (carried forward verbatim as the canonical Adversarial Brief for this hand-off):

### Decision 1 — Query parameter shape for `GET /api/v1/audit-events`

- **Decision summary:** support filters `from`, `to`, `event_type`, `correlation_id` as query parameters; `tenant_id` derived server-side from `ITenantContext` (NEVER from caller).
- **Worst-case interpretation:** an adversary controls the `event_type` parameter; supplies a wildcard or empty string; expects to receive ALL audit events including cross-tenant.
- **Failure mode:** if server doesn't enforce `tenant_id` server-side OR if the audit query lacks a `WHERE tenant_id = $captured` clause (`HasQueryFilter` missing on `AuditRecord` entity), cross-tenant audit events leak in the result set. Severity: HIGH — forensic data crosses tenant boundary.
- **Mitigation in this hand-off:** `HasQueryFilter` on `AuditRecord` per ADR 0092 §"Step 2 EFCore query-filter convention"; query parameter `tenant_id` is REJECTED at handler (400 with explicit "tenant_id is server-derived" error message); audit emission on rejected-`tenant_id`-attempt (per V2 #3 retrofit pattern via `BridgeAuditEmitter.EmitTenantBoundaryViolationAsync`).

### Decision 2 — Pagination key shape

- **Decision summary:** cursor-based pagination using base64-encoded `(occurred_at, audit_id, tenant_id_signature)` tuple as the cursor.
- **Worst-case interpretation:** an adversary manipulates the cursor to encode a cross-tenant audit_id OR a future occurred_at to skip ahead.
- **Failure mode:** if cursor isn't tenant-validated on decode, a forged cursor with another tenant's audit_id seeds the query at that point + returns subsequent rows (some of which may belong to other tenants if `HasQueryFilter` is bypassed somehow). Severity: MEDIUM — depends on HasQueryFilter coverage.
- **Mitigation in this hand-off:** cursor signed via `IOperationSigner` (existing Ed25519 signer; ADR 0046); on decode, validate that the decoded `tenant_id_signature` matches the caller's tenant (database query: `SELECT tenant_id FROM audit_records WHERE id = $cursor_audit_id` — if mismatch, return 400 "invalid_cursor"); cursor IS NOT a security boundary (HasQueryFilter is), but prevents the "skip-ahead-to-elsewhere" trick at the query layer.

### Decision 3 — Drill-down to entity by `correlation_id`

- **Decision summary:** click on an audit event → fetch related entity (Invoice, Payment, etc.) by following `correlation_id` from the audit payload.
- **Worst-case interpretation:** legacy audit records (pre-V2 #3 retrofit) lack `correlation_id` (it's NULL or empty). Drill-down link constructs `/api/v1/financial/invoices/?correlationId=` which is malformed OR returns 400.
- **Failure mode:** the audit-trail viewer page shows broken drill-down links for pre-retrofit events; user clicks → 404 or 400; degraded UX; user trust erodes.
- **Mitigation in this hand-off:** for legacy audit rows with NULL `correlation_id`, the UI shows the audit event as read-only (no drill-down link rendered); the underlying API returns the audit detail without attempting entity resolution. FED PR 2 implements the conditional rendering.

### Decision 4 — Filter parameter validation timing

- **Decision summary:** filter parameters `from` (ISO date) + `to` (ISO date) + `event_type` (enum string) validated at the Bridge handler before query execution.
- **Worst-case interpretation:** caller supplies `from > to` (inverted range) OR a `from` in the year 2099 OR an `event_type` not in the `AuditEventType` enum.
- **Failure mode:** if not validated, a malformed range produces empty results (acceptable) BUT a non-enum `event_type` like SQL-injection-attempt `' OR 1=1 --` could (if EF Core mishandles) leak data. Severity: LOW — EF Core parameterizes by default; this is defense-in-depth concern, not exploitable.
- **Mitigation in this hand-off:** `event_type` parameter validated against `AuditEventType` enum allowlist (400 if not in enum); date range validated (`from <= to`; 400 if inverted); date range capped at 1 year max (400 if larger range; prevents DOS by huge-range query).

### Decision 5 — Pagination key mid-page tenant-switch

- **Decision summary:** cursor encodes `(occurred_at, audit_id, tenant_id_signature)`; pagination retrieves N rows per request.
- **Worst-case interpretation:** between pagination requests, a tenant-switch occurs in the user's session (user switched tenant via the tenant-selector UI mid-page).
- **Failure mode:** the cursor (decoded with the new tenant context) returns rows that don't match the original tenant + the new tenant — broken pagination semantics.
- **Mitigation in this hand-off:** cursor includes `tenant_id_signature` (signed via `IOperationSigner`); on decode, if cursor's tenant != current `ITenantContext.TenantId`, return 400 "tenant_changed; reload page". Frontend handles by refetching from page 1.

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
- **Mitigation in this hand-off:** payload pretty-print masks fields tagged as `[Pii]` in the audit substrate (per ADR 0049 §"PII tagging convention" if present; else FORWARD-WATCHED: cohort-4 ships with default-unmask + forward-watch ticket for future PII tagging audit). Operator can opt-into-reveal per field with explicit click (audit-emission on reveal, when PII tagging is in place).

---

## 3. Pre-build checklist (Engineer + FED execute before opening PR 0/1)

### 3.1 R4 protocol — Bridge endpoint exists check

Per V3 #7 readiness review + R4 protocol slot:

```bash
# Verify V2 #3 audit-emission Bridge retrofit PR is open / merged (downstream consumer)
gh -R Harborline-Software/shipyard pr list --state all --search "BridgeAuditEmitter OR audit-emission-bridge in:title,body"

# Verify IAuditTrail substrate is on main
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-audit/IAuditTrail.cs

# Verify AuditEventType.TenantBoundaryViolation constant exists (added by V2 #3 retrofit)
grep -n "TenantBoundaryViolation" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-audit/AuditEventType.cs

# Verify AuditRecord shape
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/kernel-audit/AuditRecord.cs
```

Expected: V2 #3 retrofit PR exists; substrate present. If audit-emission retrofit hasn't shipped → cohort-4 viewer ships against EXISTING audit events (legacy types only); TenantBoundaryViolation events appear after retrofit lands.

### 3.2 Confirm cohort-1/2/3 precedents on main

```bash
grep -rn "AuthenticatedTenantPolicy" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Authorization/
grep -rn "pattern-009" /Users/christopherwood/Projects/Harborline-Software/shipyard/_shared/engineering/standing-approved-patterns.md
```

### 3.3 R5 protocol — Lock file scaffolding (placeholder)

Admiral's Phase 1 Stage-05 amendment (in flight per V3 #4) is expected to codify a lock-file scaffolding convention. ONR LEAVES PLACEHOLDER section here:

> **TODO (R5):** apply Admiral's Phase 1 amendment lock-file scaffolding rule when ratified. Until then, default fleet conventions apply (worktree per deliverable; commit + push + PR within minutes; per `feedback_onr_worktree_per_deliverable`).

### 3.4 Confirm no parallel-session PRs on the same surface

```bash
gh -R Harborline-Software/sunfish pr list --state open --search "AuditTrailPage OR audit-trail in:title,body"
gh -R Harborline-Software/signal-bridge pr list --state open --search "AuditEvents OR audit-events in:title,body"
```

### 3.5 Confirm workstream row exists

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/workstreams/W78-*
```

If absent, **STOP** — Admiral registers W#78 first.

### 3.6 Confirm pre-authorization frontmatter status

Hand-off ships with `co-pre-authorized: requested`. CIC ratifies (or declines) at hand-off review.

---

## 4. Engineer prereq PR 0 — Bridge audit-events endpoint family

**Estimated effort:** ~2-3h
**Scope:** new `GET /api/v1/audit-events` Bridge endpoint family at `signal-bridge/Sunfish.Bridge/Audit/`; consumes `IAuditTrail` (existing) or adds `IAuditEventReader` read-side primitive if missing
**Commit subject:** `feat(signal-bridge): cohort-4 audit-events Bridge endpoint family (W#78 PR 0)`
**Branch:** `engineer/cohort-4-pr-0-audit-events-endpoint`
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** (forensics surface; tenant-scoping; cursor validation; CSV export)

### 4.1 Endpoints

```
GET    /api/v1/audit-events                  list / search / paginate
GET    /api/v1/audit-events/{auditId}        single event detail
GET    /api/v1/audit-events/export.csv       CSV export
```

### 4.2 Request shape — list / search

```http
GET /api/v1/audit-events?from=2026-05-01&to=2026-05-21&event_type=Messaging.MessageDispatched&page_size=50&cursor=<base64>
Authorization: Bearer <token>
```

Query params:
- `from` — ISO date; optional; default = `to - 30 days`
- `to` — ISO date; optional; default = current UTC
- `event_type` — enum string from `AuditEventType`; optional; default = all
- `correlation_id` — GUID; optional; if present, ignores `from`/`to`
- `page_size` — integer; optional; default 50; max 200
- `cursor` — opaque base64-encoded signed cursor; optional; from prior response

Server REJECTS (returns 400):
- `from > to` → "inverted_range"
- `from < to - 1 year` → "range_too_large"
- `event_type` not in `AuditEventType` enum → "invalid_event_type"
- `page_size > 200` → "page_size_too_large"
- `tenant_id` query param present → "tenant_id_not_caller_supplied" + audit `TenantBoundaryViolation` emission

### 4.3 Response shape

```json
{
  "events": [
    {
      "audit_id": "01H9K...",
      "occurred_at": "2026-05-21T12:30:45Z",
      "event_type": "Messaging.MessageDispatched",
      "correlation_id": "01H9K...",
      "payload_summary": {
        "message_id": "01H9...",
        "channel": "Email"
      },
      "signature_state": "Verified"
    }
  ],
  "next_cursor": "<base64-signed>",
  "has_more": true
}
```

### 4.4 Handler signature

```csharp
internal static async Task<Results<Ok<AuditEventsResponse>, BadRequest<ProblemDetails>>>
  HandleListAuditEventsAsync(
      [FromQuery] string? from,
      [FromQuery] string? to,
      [FromQuery] string? event_type,
      [FromQuery] string? correlation_id,
      [FromQuery] int page_size = 50,
      [FromQuery] string? cursor,
      ITenantContext tenantContext,
      IAuditTrail audit,
      IOperationSigner signer,
      BridgeAuditEmitter emitter,
      HttpContext http,
      CancellationToken ct)
{
    // Reject tenant_id if caller supplied
    if (http.Request.Query.ContainsKey("tenant_id"))
    {
        await emitter.EmitTenantBoundaryViolationAsync(
            entityType: "AuditEventsEndpoint",
            entityId: "(query-param-tenant_id)",
            requestedTenant: new TenantId(tenantContext.TenantId),
            actualTenant: new TenantId("(caller-supplied)"),
            tenantContext: tenantContext,
            ct);
        return TypedResults.BadRequest(new ProblemDetails { Detail = "tenant_id_not_caller_supplied" });
    }

    // Parse + validate cursor (signed by IOperationSigner)
    AuditCursor? decoded = null;
    if (!string.IsNullOrEmpty(cursor))
    {
        decoded = DecodeAndVerifyCursor(cursor, signer);
        if (decoded?.TenantId != new TenantId(tenantContext.TenantId))
        {
            return TypedResults.BadRequest(new ProblemDetails { Detail = "tenant_changed_reload_page" });
        }
    }

    // Validate filters
    // ... date parsing + range checks + event_type enum check ...

    // Execute query (tenant-scoped via HasQueryFilter on AuditRecord)
    var (events, nextCursor, hasMore) = await audit.QueryAsync(
        new AuditQuery
        {
            From = parsedFrom,
            To = parsedTo,
            EventType = parsedEventType,
            CorrelationId = parsedCorrelationId,
            PageSize = page_size,
            AfterCursor = decoded,
        },
        ct);

    return TypedResults.Ok(new AuditEventsResponse(events, nextCursor, hasMore));
}
```

### 4.5 CSV export endpoint

```csharp
internal static async Task<Results<FileStreamHttpResult, BadRequest<ProblemDetails>>>
  HandleExportAuditEventsCsvAsync(
      [FromQuery] string? from,
      [FromQuery] string? to,
      [FromQuery] string? event_type,
      ITenantContext tenantContext,
      IAuditTrail audit,
      HttpContext http,
      CancellationToken ct)
{
    // Same tenant_id rejection
    // Same date range validation + 1-year cap
    // 10M row absolute cap

    var stream = new MemoryStream();
    using var writer = new StreamWriter(stream, leaveOpen: true);
    await writer.WriteLineAsync("audit_id,occurred_at,event_type,correlation_id,tenant_id");

    long rowCount = 0;
    await foreach (var rec in audit.QueryStreamAsync(query, ct))
    {
        if (++rowCount > 10_000_000)
        {
            return TypedResults.BadRequest(new ProblemDetails { Detail = "export_too_large" });
        }
        await writer.WriteLineAsync($"{rec.AuditId},{rec.OccurredAt:O},{rec.EventType},{rec.CorrelationId},{rec.TenantId}");
    }
    await writer.FlushAsync();
    stream.Position = 0;

    return TypedResults.File(stream, "text/csv", $"audit-events-{DateTime.UtcNow:yyyy-MM-dd}.csv");
}
```

### 4.6 Tests (≥10 new)

- `ListAuditEvents_AuthenticatedTenant_ReturnsScopedEvents` — basic happy path
- `ListAuditEvents_TenantIdQueryParam_Returns400` — Adversarial Brief Decision 7
- `ListAuditEvents_TenantIdQueryParam_EmitsAuditEvent` — verify TenantBoundaryViolation emission
- `ListAuditEvents_InvertedRange_Returns400` — Decision 4
- `ListAuditEvents_RangeTooLarge_Returns400` — Decision 4 / Decision 6 mitigation
- `ListAuditEvents_InvalidEventType_Returns400` — Decision 4
- `ListAuditEvents_CursorFromOtherTenant_Returns400` — Decision 2
- `ListAuditEvents_CursorFromForgedSignature_Returns400` — Decision 2
- `ListAuditEvents_TenantSwitch_CursorReject_Returns400` — Decision 5
- `ExportAuditEventsCsv_TenantScopedStreaming_OK` — Decision 6
- `ExportAuditEventsCsv_AboveLimit_Returns400` — Decision 6

### 4.7 Pattern conformance

```
@standing-pattern: pattern-009 (cluster-endpoint rebind pair — server-side half)
```

### 4.8 Do NOT in this PR

- Do NOT add audit-of-audit-viewer-access emission (forward-watched)
- Do NOT add live tail / WebSocket / SSE streaming (out-of-scope; C6 candidate)
- Do NOT add cross-tenant admin view (super-admin scope; C2 candidate)

---

## 5. FED PR 1 — `audit-trail.ts` + `AuditTrailPage.tsx`

**Estimated effort:** ~2-3h
**Scope:** new `sunfish/apps/web/src/api/audit-trail.ts` shared TypeScript client + `AuditTrailPage.tsx` table view + filter UI + CSV export button + route `/audit-trail`
**Commit subject:** `feat(anchor-react): cohort-4 PR 1 audit-trail viewer page (W#78 PR 1)`
**Branch:** `fed/cohort-4-pr-1-audit-trail-viewer`
**Depends on:** Engineer PR 0 merged
**Pre-merge council:** NOT required (mechanical pattern-009 mirror)

### 5.1 TypeScript client (`audit-trail.ts`)

```typescript
export interface AuditEventListResponse {
  events: AuditEventSummary[];
  next_cursor: string | null;
  has_more: boolean;
}

export interface AuditEventSummary {
  audit_id: string;
  occurred_at: string;
  event_type: string;
  correlation_id: string | null;
  payload_summary: Record<string, unknown>;
  signature_state: 'Verified' | 'VerificationFailed' | 'NotSigned';
}

export interface AuditEventDetail extends AuditEventSummary {
  payload: Record<string, unknown>;
  signatures: AttestingSignature[];
}

export interface ListAuditEventsParams {
  from?: string;
  to?: string;
  eventType?: string;
  correlationId?: string;
  pageSize?: number;
  cursor?: string;
}

export async function listAuditEvents(params: ListAuditEventsParams): Promise<AuditEventListResponse> {
  // ... canonical fetch pattern; credentials: 'include' ...
}

export async function getAuditEventDetail(auditId: string): Promise<AuditEventDetail> {
  // ...
}

export function buildAuditCsvUrl(params: ListAuditEventsParams): string {
  // Returns URL for window.open() / link href; browser handles download
  return `/api/v1/audit-events/export.csv?${new URLSearchParams(...).toString()}`;
}
```

### 5.2 Page deliverable

`AuditTrailPage.tsx`:
- Filter sidebar: from / to date pickers; event_type dropdown (populated from `AuditEventType` constants); correlation_id text input
- Table: AuditId / OccurredAt / EventType / CorrelationId (clickable to drill-down) / SignatureState badge
- Pagination: "Load more" button using `next_cursor` (per Adversarial Brief Decision 2 + 5: cursor decoded server-side; tenant-mismatch returns 400 → frontend reloads from page 1)
- Export CSV button (opens download link via `buildAuditCsvUrl`)
- Empty state: "No audit events for the selected filters."

### 5.3 Tests

- `AuditTrailPage_Renders_WithSeededEvents`
- `AuditTrailPage_FilterChange_RefetchesEvents`
- `AuditTrailPage_LoadMore_AppendsNextPage`
- `AuditTrailPage_CursorTenantMismatch_400_TriggersReload`
- `AuditTrailPage_ExportCsv_OpensDownloadUrl`

### 5.4 Pattern conformance

```
@standing-pattern: pattern-009
```

---

## 6. FED PR 2 — `AuditEventDetailPage.tsx`

**Estimated effort:** ~1-2h
**Scope:** drill-down single-event detail page; signature verification badge state machine; payload pretty-print JSON tree view (with `[Pii]` masking when tag exists; default-unmask + forward-watch if not)
**Commit subject:** `feat(anchor-react): cohort-4 PR 2 audit-event detail page (W#78 PR 2)`
**Branch:** `fed/cohort-4-pr-2-audit-event-detail`
**Depends on:** FED PR 1 merged

### 6.1 Page deliverable

`AuditEventDetailPage.tsx`:
- Route `/audit-trail/{auditId}`
- Header: event_type + occurred_at + correlation_id
- Signature verification badge:
  - Verified → green checkmark + "Signed by IOperationSigner; Ed25519 verified"
  - VerificationFailed → red X + "Signature verification failed; investigate"
  - NotSigned → gray badge + "Legacy event (pre-signature substrate)"
- Payload pretty-print: JSON tree view with collapsible nodes
  - If `[Pii]` tag exists per ADR 0049 — mask field by default; "reveal" click emits audit (forward-watched)
  - If no `[Pii]` tag substrate — render verbatim; forward-watch flag in `apps/docs/anchor/cohort-4-audit-trail-viewer.md` for future PII audit
- Drill-down link (Decision 3): if `correlation_id` is non-null, render link to `/financial/invoices/?correlationId={id}` (or appropriate entity by event_type mapping); if null, render as plain text

### 6.2 Tests

- `AuditEventDetailPage_Renders_WithSignedEvent`
- `AuditEventDetailPage_SignatureBadge_StateMachine` (3 states)
- `AuditEventDetailPage_LegacyEventNoCorrelationId_NoDrillDown` (Decision 3)
- `AuditEventDetailPage_PayloadTreeView_ExpandCollapse`
- `AuditEventDetailPage_DrillDownLink_NavigatesToEntity`

### 6.3 Pattern conformance

```
@standing-pattern: pattern-009
```

---

## 7. FED PR 3 — Close-out

**Estimated effort:** ~1h
**Scope:** new docs page + Playwright CDP E2E smoke extension + W#78 ledger flip
**Commit subject:** `chore(anchor-react,docs): cohort-4 close-out + W#78 ledger flip (W#78 PR 3)`
**Branch:** `fed/cohort-4-pr-3-close-out`
**Depends on:** PR 2 merged
**CIC sees this PR regardless of pre-authorization** (ledger-flip PR).

### 7.1 Docs page

`apps/docs/anchor/cohort-4-audit-trail-viewer.md`:
- Page intro + when to use
- AuditEvent shape reference
- Filter UI walkthrough
- Drill-down semantics
- Signature verification interpretation
- Forward-watched: PII tagging (post-cohort-4 substrate work); audit-of-audit-viewer-access; live tail

### 7.2 E2E smoke extension

Extend cohort-3 Playwright CDP smoke test (3 new scenarios):
- AuditTrailPage renders against a seeded Bridge with 100 audit events; filter changes refetch
- AuditEventDetailPage renders for a TenantBoundaryViolation event (verifies V2 #3 retrofit integration)
- CSV export downloads file with expected row count

### 7.3 Ledger flip

Update `shipyard/icm/_state/workstreams/W78-anchor-react-cohort-4-c3-audit-trail-viewer.md` source row from `building` → `built`; run render-ledger.

### 7.4 Status beacon

File `coordination/inbox/engineer-status-2026-05-XXTHH-MMZ-w78-cohort-4-built.md` with PR refs + cohort-4 anchor closing summary.

---

## 8. Cross-cluster integration

| Frontend page | Frontend hook | Frontend client | Bridge endpoint | Bridge handler | Substrate |
|---|---|---|---|---|---|
| `AuditTrailPage.tsx` | `useAuditEvents(params)` | `audit-trail.ts:listAuditEvents` | `GET /api/v1/audit-events?...` | `AuditEventsEndpoints.HandleListAuditEventsAsync` | `IAuditTrail.QueryAsync` |
| `AuditEventDetailPage.tsx` | `useAuditEventDetail(auditId)` | `audit-trail.ts:getAuditEventDetail` | `GET /api/v1/audit-events/{id}` | `AuditEventsEndpoints.HandleGetAuditEventDetailAsync` | `IAuditTrail.GetByIdAsync` |
| `AuditTrailPage.tsx` (CSV export) | (browser-native download via link href) | `audit-trail.ts:buildAuditCsvUrl` | `GET /api/v1/audit-events/export.csv?...` | `AuditEventsEndpoints.HandleExportAuditEventsCsvAsync` | `IAuditTrail.QueryStreamAsync` |

All Bridge handlers derive `TenantId` via `ITenantContext` server-side; cursor signing via existing `IOperationSigner` (ADR 0046).

---

## 9. Halt conditions

### H1. V2 #3 audit-emission retrofit hasn't shipped

**Symptom:** Engineer PR 0 cannot emit `TenantBoundaryViolation` audit on cross-tenant probes because `BridgeAuditEmitter` helper not on main.

**Mitigation:** PR 0 can stub the emission inline (single call to `IAuditTrail.AppendAsync` with TBV event type); when V2 #3 retrofit ships, refactor to use `BridgeAuditEmitter`. Acceptable v1.

**Halt:** no halt; document the stub + create follow-on refactor task.

### H2. `IAuditTrail` doesn't have query-side primitives

**Symptom:** Existing `IAuditTrail` is write-side only (`AppendAsync`); read-side `QueryAsync` / `QueryStreamAsync` / `GetByIdAsync` not present.

**Mitigation:** Engineer PR 0 adds `IAuditEventReader` read-side primitive in `kernel-audit` package (or extends `IAuditTrail`). Adds ~1h scope.

**Halt:** if substrate work is substantive, **STOP** + file `engineer-question-*-cohort-4-audit-reader-substrate.md`; Admiral routes whether to expand PR 0 scope or split.

### H3. PAO Track C cohort-4 design direction not authored

**Symptom:** No design specs at `shipyard/_shared/design/cohort-4/`.

**Mitigation:** Audit-trail viewer UI is simpler than cohort-2 financial pages (table + filter + detail page). Cohort-1 / cohort-2 design language inherits cleanly. PAO Track C cohort-4 design is OPTIONAL for cohort-4.

**Halt:** no halt; FED proceeds with cohort-1/2/3 design baseline.

### H4. Cohort-3 hasn't fully shipped

**Symptom:** Cohort-3 reports cluster not yet on main when cohort-4 PR 0 opens.

**Mitigation:** Cohort-4 has no functional dependency on cohort-3 reports. They share design tokens (`shipyard/_shared/design/cohort-N/`) but FED can use cohort-2 baseline.

**Halt:** no halt.

### H5. `[Pii]` tag substrate not in ADR 0049

**Symptom:** Cohort-4 PR 2 cannot mask PII fields because `[Pii]` tagging convention isn't in the audit substrate.

**Mitigation:** Ship cohort-4 with default-unmask; flag in docs running log + cohort-N+ forward-watch. PII tagging is a substantive substrate add; not in cohort-4 scope.

**Halt:** no halt; documented.

### H6. Audit-of-audit-viewer-access scope creep

**Symptom:** Sec-eng SPOT-CHECK on Engineer PR 0 asks for audit emission on the audit-viewer queries themselves ("audit who looked at audit logs").

**Mitigation:** Recursive audit (audit-of-audit) is forward-watched; cohort-4 scope does NOT include it.

**Halt:** if sec-eng insists, **STOP** + file `onr-question-*-cohort-4-audit-of-audit-scope.md`. Admiral rules — likely accepts cohort-4 ships without it + forward-watches.

---

## 10. PASS gate (end-state for declaring W#78 `built`)

1. PRs 0 + 1 + 2 + 3 merged to main in sequence
2. Engineer PR 0 sec-eng SPOT-CHECK GREEN
3. All Bridge endpoint + FED tests pass
4. `pnpm --filter @sunfish/web build` + `dotnet build Sunfish.Bridge` clean
5. E2E smoke (3 new scenarios) passes
6. Network-panel verification: each page renders against Bridge audit endpoints with NO `/api/v1/erpnext/*` calls
7. `apps/docs/anchor/cohort-4-audit-trail-viewer.md` published + linked in TOC
8. Workstream W#78 ledger row reads `built` with PR refs (4 entries)
9. `coordination/inbox/engineer-status-2026-05-XXTHH-MMZ-w78-cohort-4-built.md` beacon dropped
10. No outstanding `@deviation-from-spec:` flags without CIC ack

---

## 11. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #1
2. V2 #6 cohort-4 scope survey (shipyard#74) — C3 anchor recommendation
3. V2 #3 audit-emission Bridge retrofit research (shipyard#71) — substrate dependency
4. V3 #4 Adversarial Brief template prototype (shipyard#78) — §2 worked example
5. cohort-3 hand-off (shipyard#51) — structural template
6. ADR 0049 (audit substrate) — IAuditTrail + AuditRecord + AttestingSignature
7. ADR 0091 R2 (tenant context substrate)
8. ADR 0092 (substrate tenant-keyed repository contract; HasQueryFilter on AuditRecord)
9. ADR 0046 (IOperationSigner — Ed25519 signature)

---

**End of hand-off.**

— ONR, 2026-05-21T13:00Z
