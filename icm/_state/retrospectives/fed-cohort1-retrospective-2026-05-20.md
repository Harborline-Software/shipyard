---
title: FED — Anchor React Rebind Cohort 1 Retrospective
workstream: W74 — Anchor React Rebind Cohort 1
author: fed
date: 2026-05-20
status: final
---

# FED Retrospective — Anchor React Rebind Cohort 1

**Workstream:** W#74  
**Cohort scope:** PropertiesPage + LeasesPage + LeaseDetailPage + MaintenancePage rebind from ERPNext to Bridge native cluster endpoints  
**Closed:** 2026-05-19  
**PRs:** sunfish #1–3, #11, #13 (anchored to `apps/anchor-react/`); shipyard #11, #28, #32 (signal-bridge + ERPNext deprecation)

---

## What shipped

| PR | Scope | Key changes | Status |
|---|---|---|---|
| sunfish#1 | PropertiesPage | Bridge `/api/v1/properties` cluster endpoint + first `@candidate-pattern: pattern-009` instance | Merged |
| sunfish#2 | LeasesPage + LeaseDetailPage | Bridge `/api/v1/leases` cluster + IMustHaveTenant A1+A2 amendments after sec-eng | Merged |
| sunfish#3 (+ bridge#11 + shipyard#28) | MaintenancePage | Bridge `/api/v1/cockpit/work-orders/*` rebind + CSRF wiring + audit emission (sec-eng SPOT-CHECK remediation) | Merged |
| sunfish#11 + shipyard#32 | Close-out | ERPNext `@deprecated` marks + Playwright CDP smoke test + W#74 ledger flip | Merged |

Pattern `pattern-009` (Bridge endpoint + frontend rebind pair) formally ratified after 4 clean shippings.

---

## What went well

**Handoff quality:** The Stage 06 hand-off authored by XO was detailed enough that FED could begin implementation on the first pass without clarifying questions. The `pre-merge-council` table in the handoff spec was especially valuable — it removed ambiguity about which council reviews were required on which PR.

**ERPNext deprecation phasing:** Marking `getProperties()`, `getLeases()`, `getMaintenanceTickets()` as `@deprecated` in `apps/anchor-react/src/api/erpnext.ts` while keeping them callable gave clear signal without a hard break. The Playwright smoke test in PR 4 proved the rebind worked end-to-end before the ledger flip.

**Atomic PR bundling (per-page):** The per-page PR shape (Bridge endpoint + React fetcher rebind bundled) worked well. Reviewers saw a complete, testable change in each PR. The council could trace the security surface for one page at a time.

**TanStack Query hook isolation:** Because each page's data fetching was already isolated into `useProperties()`, `useLeases()`, etc., the rebind only touched the hooks' fetch implementations — not the page components. Minimal diff for each PR.

---

## What didn't go well

### Post-mortem 1 — SPOT-CHECK delay on PR 3 (8h gap)

**What happened:** PR 3 (MaintenancePage) opened DRAFT with a `@candidate-pattern: pattern-009` claim. Admiral's SPOT-CHECK dispatch was delayed ~8h from PR open. The sec-eng council verdict (when finally produced) caught a real audit-emission bug that would have shipped if PR 3 merged during that window.

**Impact:** PR 3 sat DRAFT for a day longer than needed; the critical-path unblock for cohort-2 `pattern-009-formal-ratification` was delayed.

**Fix:** Fleet added the 30-minute SPOT-CHECK SLA to fleet-conventions.md + a QM daemon backstop (hourly check for >2h DRAFT pattern-009 PRs without a council verdict).

**Lesson for FED:** When opening a DRAFT PR with `@candidate-pattern` or `@standing-pattern` claims, file a status beacon to Admiral immediately so the SLA clock starts. Don't assume Admiral notices from the PR alone.

### Post-mortem 2 — Audit emission bug in MaintenancePage

**What happened:** PR 3's Bridge endpoint for creating work orders was not emitting the `TenantBoundaryViolation` audit event on cross-tenant write attempts. The SPOT-CHECK surface review caught this. The audit emission was added before merge.

**Root cause:** The `CreateWorkOrderAsync` handler was lifted from the cockpit pattern, which was read-only. Write-path audit emission wasn't in the original cockpit pattern (pattern-009 was a candidate; write-path audit wasn't specified).

**Fix:** A3 amendment added to PR 3: `EmitTenantBoundaryViolationAsync` on rejected cross-tenant CreateWorkOrder calls.

**Lesson:** New write-path endpoints need an explicit audit-emission review step in the council checklist. The read-path uniform-404 was clean; the write-path audit was the gap.

### Post-mortem 3 — LeaseDetailPage payments scope deferred mid-cohort

**What happened:** The original hand-off included `LeaseDetailPage` payment data in the rebind scope. During PR 2 implementation, the `IPaymentRepository` wasn't yet tenant-keyed (cohort-2 engineering dependency). Scope was narrowed to read-only lease data; payments were deferred to cohort-2 PR 0c.

**Impact:** PR 2 shipped without the payments table rebind. A mock was used. This required a mock-swap PR in cohort-2 (sunfish#17/#18/#19).

**Lesson for cohort-2:** When a page's data surface spans multiple repositories and some aren't yet tenant-keyed, scope the PR to only the clean repositories. Flag the deferred surfaces explicitly so the mock-swap PR can be planned in advance.

---

## Patterns established

### pattern-009 — Bridge endpoint + frontend rebind pair

**First instance:** PropertiesPage (sunfish#1)  
**Ratified:** After 4 clean shippings (PRs 1–4)  
**Shape:**
- Bridge cluster endpoint: `GET /api/v1/<cluster>/<resource>` in `Bridge/<Cluster>/Endpoints.cs`
- React hook: `use<Resource>()` in `apps/web/src/hooks/use<Resource>.ts`
- Tenant context: server-derived from session (not a frontend parameter)
- CSRF: required on all mutating endpoints (POST/PATCH/DELETE)
- Audit: `EmitTenantBoundaryViolationAsync` on rejected cross-tenant reads (uniform-404) + writes (ArgumentException)

Cohort-2 applies this pattern to the remaining repositories with the tenant-keying retrofit (pattern-009-tenant-keying-retrofit).

### CSRF wiring convention

First instance: PR 3 (MaintenancePage create work order). The Bridge Bridge CSRF token is fetched once per session via `GET /api/v1/csrf-token` and injected into all mutating fetch calls via the `X-CSRF-Token` header. Implemented in `apps/web/src/lib/csrf.ts`.

---

## Forward scope context

Cohort-2 (W#76) extends the rebind to:
- LeaseDetailPage payments (sunfish#17) — requires IPaymentRepository tenant-keyed (shipyard PR 0c)
- AccountingPage (sunfish#18) — requires IInvoiceRepository tenant-keyed (shipyard PR 0a)
- Additional pages per cohort-2 scope survey

Cohort-3 (pending PAO Track C design direction) covers the ui-react v0.3 substrate promotions.

---

## Metrics

| Metric | Value |
|---|---|
| PRs shipped | 5 sunfish + 2 signal-bridge + 2 shipyard = 9 total |
| Pages rebound | PropertiesPage, LeasesPage, LeaseDetailPage (partial), MaintenancePage |
| ERPNext routes deprecated | 4 (`getProperties`, `getLeases`, `getLease`, `getMaintenanceTickets`) |
| Pattern-009 instances | 4 (ratified to formal) |
| Council reviews dispatched | 1 sec-eng SPOT-CHECK (PR 1) + 1 sec-eng SPOT-CHECK (PR 3 post-audit-emission discovery) |
| Amendments after SPOT-CHECK | 1 (PR 3 A1: audit emission on write path) |
| Calendar days (PR 1 open → PR 4 merged) | ~2 days |
