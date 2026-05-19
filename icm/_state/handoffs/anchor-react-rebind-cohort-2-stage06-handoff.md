---
title: Anchor React Bridge Rebind Cohort 2 — Stage 06 Hand-off
workstream: W#76 — Anchor React Rebind Cohort 2 (registered in `active-workstreams.md` 2026-05-18 after collision-rename ruling at `admiral-ruling-2026-05-18T23-15Z-fed-cohort1-pr4-ledger-blockers-resolved.md`; per `feedback_never_add_workstream_rows_directly_to_ledger` the source `W76-*.md` row + render-ledger must exist before kickoff)
cluster: anchor-react-rebind (cross-package: `signal-bridge/Sunfish.Bridge/Financial/` + `sunfish/apps/web/` + `shipyard/packages/blocks-financial-*`)
pipeline: sunfish-feature-change
authored-by: ONR
authored-at: 2026-05-19T00-00Z
status: pre-authorized (CIC granted 2026-05-19T08-15Z; ready-to-execute when Engineer queue reaches cohort-2 PR 0a-d after ADR 0091 Step 1 + ADR 0092 Step 1 on main)
co-pre-authorized: granted
co-pre-authorized-by: cic
co-pre-authorized-date: 2026-05-19
co-pre-authorized-ratification-ref: admiral-question-2026-05-19T07-25Z-cic-mvp-critical-path-three-asks-packaged + CIC grant 2026-05-19T08-15Z + admiral-status-2026-05-19T08-15Z-w76-cohort-2-pre-authorization-granted-broadcast
co-pre-authorized-rationale: |
  Cohort 2 is the second cohort under the Tauri-first pivot. The financial-page
  rebinds (PR 1 / PR 2 / PR 3) are mechanical mirrors of the cohort-1 cluster-
  endpoint pattern (pattern-009 — ratified after cohort-1 per admiral-status
  2026-05-17T23-30Z-pattern-009-promoted). The substrate-cluster PRs
  (PR 0a/b/c/d) introduce a tenant-keyed repository-contract retrofit across
  the financial cluster — large blast radius but mechanical-canonical. PR 3
  is the only judgment-heavy surface (write path + CSRF + audit; mirrors
  cohort-1 PR 3 MaintenancePage create). Sec-eng SPOT-CHECK is MANDATORY on
  each PR 0X (cross-tenant surface; pattern-009-tenant-keying-retrofit
  candidate) and PR 3 (write-path).
co-pre-authorized-scope:
  - PR 0a (blocks-financial-ar tenant-keyed repository contract) — sec-eng SPOT-CHECK MANDATORY; pre-auth conditional on council clean
  - PR 0b (blocks-financial-ap tenant-keyed repository contract) — sec-eng SPOT-CHECK MANDATORY; pre-auth pattern-mirror of PR 0a
  - PR 0c (blocks-financial-payments tenant-keyed repository contract — supplements W#68 PR 3 Option A SERVICE-layer amendment at the REPOSITORY layer) — sec-eng SPOT-CHECK MANDATORY
  - PR 0d (blocks-financial-ledger tenant-keyed repository contract) — sec-eng SPOT-CHECK MANDATORY
  - PR 1 (`/api/v1/financial/payments?leaseId=` + LeaseDetailPage payments rebind, RB-8) — pre-auth; pattern-009 formal
  - PR 2 (`/api/v1/financial/accounting/{summary,outstanding}` + AccountingPage rebind, RB-7) — pre-auth; pre-flight on Invoice IMustHaveTenant compliance
  - PR 3 (`POST /api/v1/financial/payments` + RentCollectionPage rebind, RB-9) — sec-eng SPOT-CHECK MANDATORY (write path + cross-tenant + CSRF surfaces)
  - PR 4 (ERPNext deprecation marks + E2E smoke + ledger flip) — CO sees regardless (ledger-flip PR per pre-auth ruling §Step 4)
  - PR-count maximum: 8 (workstream re-evaluation if scope grows)
  - PR-deviation flag triggers immediate CIC escalation for that PR
merge-tier: pre-authorized-pending-CIC-ratification
depends-on:
  - W#68 PR 1 — `Payment` entity on main (HARD GATE on PR 1/2/3 execution START; authoring is independent)
  - W#68 PR 2 — `IPaymentService` cluster contract
  - W#68 PR 3 + Engineer Option A amendment per `admiral-directive-amendment-2026-05-19T01-00Z-engineer-w68-pr3-amber-tenant-isolation-amendment.md` — SERVICE-layer tenant isolation precedent (PR 0c builds the REPOSITORY-layer companion)
  - W#73 — `blocks-financial-ar` per-lease payment queries (gates PR 1's `?leaseId=` query shape)
  - Cohort-1 W#74 — `AuthenticatedTenantPolicy` precedent (registered in cohort-1 PR 1; cohort-2 PRs reuse, not re-introduce)
  - Cohort-1 W#74 PR 3 — cockpit CSRF + audit pattern (`HandleCreateWorkOrderAsync` + `IAntiforgery.ValidateRequestAsync` + `AuditEventType.*` emission) — cohort-2 PR 3 mirrors this contract for `POST /api/v1/financial/payments`
  - W#68 PR 3 security-engineering council verdict (`council-verdict-2026-05-19T00-45Z-security-engineering-w68-pr3-spot-check.md`) — Item 8 Option B substrate-wide fix IS this hand-off's PR 0 cluster
  - FED cohort-2 scope survey (`fed-status-2026-05-19T0030Z-cohort2-scope-survey.md`) — primary scope input
  - Admiral ruling 2026-05-19T00-35Z + scope-expansion directive 2026-05-19T01-00Z (this hand-off's parents)
spec-source: |
  - `coordination/inbox/fed-status-2026-05-19T0030Z-cohort2-scope-survey.md` (PRIMARY — scope inventory + tenant-keying gaps + new financial family layout)
  - `coordination/inbox/admiral-ruling-2026-05-19T00-35Z-fed-cohort2-survey-accepted-routing.md` (4-track routing; Track B is THIS hand-off)
  - `coordination/inbox/admiral-directive-2026-05-19T01-00Z-onr-cohort2-handoff-scope-expansion-substrate-tenant-keying.md` (PR 0 cluster scope addition)
  - `coordination/inbox/council-verdict-2026-05-19T00-45Z-security-engineering-w68-pr3-spot-check.md` (Items 8 + 10 + forward-watch list)
  - `coordination/inbox/admiral-directive-amendment-2026-05-19T01-00Z-engineer-w68-pr3-amber-tenant-isolation-amendment.md` (Service-layer Option A precedent)
  - `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-1-stage06-handoff.md` (structural template)
  - `shipyard/icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` (cohort scoping; §2 surface inventory, §4 Cohort 2 row)
  - `shipyard/_shared/engineering/standing-approved-patterns.md` (pattern-009 — formal post-cohort-1; pattern-009-tenant-keying-retrofit — candidate, this hand-off is the qualifying instance)
estimated-effort: ~10–14h dev across 8 PRs (~1.5h × 4 PR 0 substrate, ~1h PR 1, ~1h PR 2, ~2h PR 3 incl. CSRF + audit + sec-eng wait, ~1.5h PR 4 incl. E2E smoke)
PR-count: 8
pre-merge-council:
  security-engineering: SPOT-CHECK MANDATORY on PR 0a / 0b / 0c / 0d (cross-tenant surface; pattern-009-tenant-keying-retrofit candidate); SPOT-CHECK MANDATORY on PR 3 (write-path + CSRF + cross-tenant). NOT required on PR 1 / PR 2 (mechanical mirrors of cohort-1 PR 1 pattern under pattern-009-formal).
  dotnet-architect: NOT required (mechanical Bridge endpoint family addition; pattern lifted verbatim from cohort-1 `LeasesEndpoints.cs` / `PropertiesEndpoints.cs`)
  frontend-reviewer: NOT required (mechanical fetcher swap inside existing TanStack hooks; no new state-management or UI patterns)
license-posture: MIT clean-room (all frontend code is original; no FOSS source studied for this rebind)
---

# Hand-off — Anchor React Bridge Rebind Cohort 2 (Financial cluster: Accounting + LeaseDetail payments + RentCollection)

**From:** ONR (Office of Naval Research; research session)
**To:** Engineer (sunfish technical corps) — fallback: po-mac
**Workstream:** **W#76 Anchor React Rebind Cohort 2** (canonical; renumbered from W#75 to W#76 after the 2026-05-18T23-15Z collision-rename ruling at `admiral-ruling-2026-05-18T23-15Z-fed-cohort1-pr4-ledger-blockers-resolved.md` — rent-schedule-escalators took W#75; cohort-2 took W#76). Source row file should exist at `shipyard/icm/_state/workstreams/W76-anchor-react-rebind-cohort-2.md` with the ledger re-rendered before kickoff (per `feedback_never_add_workstream_rows_directly_to_ledger`).
**Pipeline:** `sunfish-feature-change`
**Ratifications applied:**
- CIC 2026-05-17T14-30Z Tauri-first pivot ratification (cohort-1 inherited).
- CIC ratified roadmap Q1: **tenant scoping is server-derived from `ITenantContext`**, NOT a frontend query param. Cohort-2 inherits — PR 1's `?leaseId=` is the only frontend-supplied query parameter (a domain key, not a tenant filter).
- CIC ratified roadmap Q2: **ERPNext passthrough kept through Cohort 4**; cohort-2 marks the three financial ERPNext functions `@deprecated` in PR 4.
- CIC ratified roadmap Q3: **per-page PR bundling** (one PR per page, Bridge endpoint + frontend rebind landed together).
- Admiral ruling 2026-05-19T00-35Z (cohort-2 4-track routing; Track B is this hand-off authoring).
- Admiral scope-expansion 2026-05-19T01-00Z (PR 0 cluster — substrate-wide tenant-keyed repository contract, Option B from W#68 PR 3 sec-eng verdict).

---

## 1. Context

### 1.1 Why Cohort 2 ships now

The Phase 1 financial cluster work has shipped: `blocks-financial-ar` (Invoice + AR service), `blocks-financial-ap` (Bill + AP service), and W#68 (`blocks-financial-payments` — Payment entity + IPaymentService + DefaultPaymentApplicationService with W#68 PR 3 Option A tenant-isolation amendment). The substrate is now ready for Bridge surfacing.

What remains on the frontend: `sunfish/apps/web/src/api/erpnext.ts` is still the data plane for the three financial pages — `AccountingPage`, `LeaseDetailPage` (payments section), and `RentCollectionPage`. Cohort 2 is the **financial cluster rebind**.

Cohort 2 also closes a security-relevant gap surfaced by W#68 PR 3 sec-eng SPOT-CHECK (verdict 2026-05-19T00-45Z Item 8): the financial repository contracts (AR / AP / Payments / Ledger) accept opaque ids with NO tenant parameter. Engineer's PR 3 Option A amendment closes the gap at the SERVICE layer for Payments only; cohort-2 closes it at the REPOSITORY layer across the entire financial cluster (defense-in-depth + cleaner contract). The PR 0a/b/c/d cluster is the substrate-wide companion to Engineer's Option A.

### 1.2 What Cohort 2 ships

| # | PR | Subject |
|---|---|---|
| 1 | **PR 0a** | `blocks-financial-ar` tenant-keyed repository contract — add `tenantId` parameter to every `IInvoiceRepository` method; update `InMemoryInvoiceRepository`; thread through `InvoicePostingService` |
| 2 | **PR 0b** | `blocks-financial-ap` tenant-keyed repository contract — same shape; `IBillRepository` + `BillPostingService` |
| 3 | **PR 0c** | `blocks-financial-payments` tenant-keyed repository contract — `IPaymentRepository` + `IPaymentApplicationRepository` + `DefaultPaymentApplicationService` (REPOSITORY-layer companion to W#68 PR 3 Option A SERVICE-layer amendment); also ensures `PaymentApplication.IMustHaveTenant` if not yet landed in PR 3 amendment |
| 4 | **PR 0d** | `blocks-financial-ledger` tenant-keyed repository contract — `IJournalRepository` (or equivalent ledger primitive) |
| 5 | **PR 1** | `GET /api/v1/financial/payments?leaseId=` Bridge endpoint family (NEW) + `LeaseDetailPage` payments-section rebind (RB-8) |
| 6 | **PR 2** | `GET /api/v1/financial/accounting/summary` + `GET /api/v1/financial/accounting/outstanding` (NEW) + `AccountingPage` rebind (RB-7) |
| 7 | **PR 3** | `POST /api/v1/financial/payments` (NEW write path) + `RentCollectionPage` rebind (RB-9). CSRF + audit emission (`AuditEventType.PaymentRecorded`); MANDATORY sec-eng SPOT-CHECK |
| 8 | **PR 4** | ERPNext deprecation marks on three financial functions + Playwright CDP E2E smoke extension + `apps/docs/anchor/anchor-react-rebind.md` cohort-2 section + W#76 ledger flip |

After Cohort 2 lands, the three financial pages render without ERPNext running and the financial cluster's repository contracts are tenant-keyed end-to-end.

### 1.3 What Cohort 2 does NOT ship

- **Reports pages** (`PLReport`, `RentRoll`) — Cohort 3 scope (already call `/api/v1/reports/*`; client-file rename + `@deprecated` marks only; see FED survey §6).
- **MaintenancePage status-update writeback** — deferred from cohort-1; not cohort-2 scope per FED survey §4.
- **Persistence-layer transactional fixes** — W#68 PR 3 sec-eng verdict Items 5 + 6 (`SERIALIZABLE` transactions; outbox pattern) are HARD GATES on the SQLite/Postgres persistence hand-off, NOT cohort-2 work. Lifted into a future workstream (likely `blocks-financial-payments-persistence-stage06-handoff.md`).
- **`Currency` validation at entity construction** — W#68 PR 3 verdict Item 4 forward-watch; defensive-depth follow-up.
- **`ApplyError.PaymentTerminal` / `DiscountWriteoffUnsupported` constants** — bundled into the discount-account-selection follow-on PR per W#68 PR 3 verdict Items 2 + 3.
- **`UnapplyFromInvoiceAsync` status-restore data-loss fix** — minor block-engineering follow-up per W#68 PR 3 verdict Item 7.
- **`AuditEventType.PaymentApplied` / `PaymentUnapplied` constants** — mechanical kernel-audit follow-up. **EXCEPT** for `AuditEventType.PaymentRecorded` which IS in scope (cohort-2 PR 3 introduces the write path and must emit a typed audit event).
- **Multi-device CRDT sync** — out of scope per roadmap §1 (cohort-1 inherited).
- **ERPNext route deletion** — Cohort 4 (RB-12). Cohort-2 marks `@deprecated` only.

### 1.4 Auth, CSRF, audit conventions (recap)

- **Auth:** `AuthenticatedTenantPolicy` (registered in cohort-1 PR 1) — REUSED by cohort-2; do NOT re-introduce.
- **Tenant scoping:** server-derived from `ITenantContext.TenantId`; frontend does NOT pass tenant parameters.
- **CSRF (PR 3 only):** `IAntiforgery.ValidateRequestAsync` per the cohort-1 PR 3 `HandleCreateWorkOrderAsync` pattern. Frontend round-trips the antiforgery token via the same mechanism as `MaintenancePage`'s create flow.
- **Audit (PR 3 only):** emit `AuditEventType.PaymentRecorded` (NEW constant — add to `shipyard/packages/kernel-audit/AuditEventType.cs` in PR 3 alongside the endpoint). The constant should sit next to existing `PaymentAuthorized` (see kernel-audit `AuditEventType.cs`).
- **Standing pattern:** `pattern-009` (formal — ratified after cohort-1 three-clean-shipping milestone per admiral-status 2026-05-17T23-30Z-pattern-009-promoted). All cohort-2 page rebinds (PR 1 / PR 2 / PR 3) ship under `@standing-pattern: pattern-009`. PR 0a/b/c/d ship under `@candidate-pattern: pattern-009-tenant-keying-retrofit` (this hand-off is the qualifying instance; ratify after the four PR 0 PRs ship clean).

### 1.5 Why CIC sees PR 4 regardless of pre-auth

PR 4 is the **ledger-flip PR** for W#76. Per the pre-authorization ruling §Step 4:

> Even under pre-authorization, CIC is brought into the loop if ANY of: **Ledger-flip PR** (final PR of the workstream — CIC always sees workstream completions) [...]

PR 4 also extends the Playwright CDP smoke test; CIC sees the rebind has actually landed and the financial pages can stand without ERPNext.

---

## 2. Pre-build checklist (Engineer executes before opening PR 0a)

Run each step; halt on any unexpected state.

### 2.1 Confirm W#68 cluster dependencies on main

```bash
# W#68 PR 1 (Payment entity)
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-payments/Models/Payment.cs
# W#68 PR 2 (IPaymentService cluster contract)
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-payments/Services/IPaymentService.cs
# W#68 PR 3 (DefaultPaymentApplicationService + Option A amendment)
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-payments/Services/DefaultPaymentApplicationService.cs
# Confirm Option A amendment landed — the service ctor MUST take ITenantContext:
grep -n "ITenantContext" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-payments/Services/DefaultPaymentApplicationService.cs
# Confirm PaymentApplication.IMustHaveTenant:
grep -n "IMustHaveTenant" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-payments/Models/PaymentApplication.cs
```

Expected: all exist; Option A amendment landed (DefaultPaymentApplicationService injects ITenantContext); PaymentApplication implements IMustHaveTenant. If any are missing, **STOP** — file `engineer-question-*` naming the missing dependency. Cohort-2 PR 0c BUILDS ON Option A; if Option A hasn't landed, PR 0c authoring is fine but execution must wait.

### 2.2 Confirm cohort-1 AuthenticatedTenantPolicy is registered

```bash
grep -rn "AuthenticatedTenantPolicy\|AuthenticatedTenantPolicyName" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Authorization/ 2>/dev/null
grep -rn "MapPropertiesEndpoints\|MapLeasesEndpoints" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Program.cs 2>/dev/null
```

Expected: policy is registered and consumed by Properties + Leases endpoints. Cohort-2 PR 1 reuses it for the new `/api/v1/financial/*` family.

### 2.3 Confirm Invoice IMustHaveTenant compliance status (soft gate per FED survey §4)

```bash
grep -n "IMustHaveTenant" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-ar/Models/Invoice.cs
grep -n "TenantId" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-financial-ar/Models/Invoice.cs
```

Expected per FED survey: `Invoice` has a `TenantId` field but does NOT implement `IMustHaveTenant`. **Close this in PR 0a** — when threading tenant through `IInvoiceRepository`, also add `: IMustHaveTenant` to `Invoice` so DI-level auto-filter wiring works for downstream Bridge consumption (PR 2 AccountingPage). If the interface adoption surfaces ripples beyond AR (e.g., a po-mac-scale retrofit similar to Lease in cohort-1), **see Halt H3.**

### 2.4 Confirm no parallel-session PRs touch the same surface

```bash
gh -R Harborline-Software/shipyard pr list --state open --search "blocks-financial in:title,body"
gh -R Harborline-Software/signal-bridge pr list --state open --search "Financial OR financial in:title,body"
gh -R Harborline-Software/sunfish pr list --state open --search "AccountingPage OR LeaseDetailPage OR RentCollectionPage in:title,body"
```

Expected: empty (or only this hand-off's own PRs). If anything else is open and would conflict, file `engineer-question-*`.

### 2.5 Confirm `gt status` / `git status` is clean

Current branch should be `main` (or a fresh worktree from `main` per fleet-conventions §"Git worktree location" — all worktrees under `<repo>/.worktrees/<branch>/`; NEVER under `/tmp/` or `/var/folders/`).

### 2.6 Confirm the workstream row exists

```bash
grep -n "W#76\|anchor-react-rebind-cohort-2" /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/active-workstreams.md
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/workstreams/W76-* 2>&1
```

Expected: workstream registered as `ready-to-build`. If absent, **STOP** — Admiral must register the workstream first (the hand-off file referencing W#76 is not enough on its own; the source `W76-*.md` file plus render-ledger must run).

### 2.7 Confirm the pre-authorization frontmatter status

The hand-off ships with `co-pre-authorized: requested`. CIC ratifies (or declines) at hand-off review. **Engineer must NOT open PRs under the pre-authorization shortcut until the frontmatter says `co-pre-authorized: granted`.** If it says `declined`, fall back to the per-PR-CIC-click model (still ship the work — just don't arm auto-merge without CIC click).

### 2.8 Confirm CSRF / antiforgery infrastructure is wired

```bash
grep -rn "IAntiforgery\|ValidateRequestAsync\|AddAntiforgery" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/ 2>/dev/null | head -10
grep -rn "HandleCreateWorkOrderAsync\|IAntiforgery" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Maintenance/ 2>/dev/null | head -5
```

Expected: CSRF middleware + `IAntiforgery` are wired (precedent: cohort-1 PR 3 `HandleCreateWorkOrderAsync`). Cohort-2 PR 3 follows the exact same pattern. If absent (cohort-1 PR 3 didn't ship the antiforgery integration), **STOP** — file `engineer-question-*`; PR 3 cannot ship without CSRF.

### 2.9 Confirm `IDomainEventPublisher` is wired (not noop) at Bridge level

Per W#68 PR 3 sec-eng verdict Item 9 forward-watch, the Bridge composition root that hosts the payments block MUST register a real `IDomainEventPublisher` (not the noop fallback). PR 3 emits `AuditEventType.PaymentRecorded` via the publisher; a noop drops the audit silently.

```bash
grep -rn "IDomainEventPublisher\|RecordingDomainEventPublisher\|NoopDomainEventPublisher" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/ 2>/dev/null | head -10
```

Expected: a real publisher is registered. If only the noop is in place, **see Halt H7** — PR 3 must include a Bridge-level integration test asserting non-noop wiring per the W#68 PR 3 verdict Item 9 follow-up.

### 2.10 Read the supporting docs once

Skim:
- `coordination/inbox/fed-status-2026-05-19T0030Z-cohort2-scope-survey.md` (PRIMARY scope input)
- `coordination/inbox/council-verdict-2026-05-19T00-45Z-security-engineering-w68-pr3-spot-check.md` (Items 8 + 10 forward-watch list)
- `coordination/inbox/admiral-directive-amendment-2026-05-19T01-00Z-engineer-w68-pr3-amber-tenant-isolation-amendment.md` (Option A precedent for PR 0c)
- `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-1-stage06-handoff.md` (structural template; particularly §3.18–§3.25 for PR 3 CSRF + audit pattern)
- `signal-bridge/Sunfish.Bridge/Leases/LeasesEndpoints.cs` + `Properties/PropertiesEndpoints.cs` (cited templates for the new `/api/v1/financial/*` family)
- `signal-bridge/Sunfish.Bridge/Maintenance/MaintenanceEndpoints.cs` (cited template for PR 3 write-path)
- `sunfish/apps/web/src/api/cockpit.ts` + `src/api/leases.ts` + `src/api/maintenance.ts` (frontend client templates)
- `shipyard/_shared/engineering/standing-approved-patterns.md` (pattern-009 in particular)

---

## 3. Per-PR deliverables

Cohort 2 splits into **8 PRs** in two clusters:

**PR 0 cluster (substrate)** — sequenced BEFORE the rebind PRs. Each PR 0X is a mechanical-large repository-contract retrofit on one financial block. PR 0a → PR 0b → PR 0c → PR 0d, sequential for review-load smoothing; technically parallelizable.

**PR 1–4 cluster (rebind + close-out)** — sequenced AFTER PR 0 cluster lands. PR 1 → PR 2 → PR 3 → PR 4.

Pre-authorization grants the inter-cluster ordering; CIC may rule at hand-off review to interleave (e.g., PR 0a + PR 0c first if Engineer's W#68 PR 3 Option A persists a contract gap that needs the REPO-layer companion sooner).

---

### PR 0a — `blocks-financial-ar` tenant-keyed repository contract

**Estimated effort:** ~1.5h
**Scope:** add `tenantId` parameter (or `ITenantContext`) to every `IInvoiceRepository` method; update `InMemoryInvoiceRepository`; thread through `InvoicePostingService`; tests
**Commit subject:** `feat(blocks-financial-ar): tenant-key IInvoiceRepository contract (cohort 2 PR 0a)`
**Branch:** `engineer/cohort-2-pr-0a-blocks-financial-ar-tenant-keying`
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — first instance of pattern-009-tenant-keying-retrofit; sets precedent for PR 0b/c/d.

#### 3.1 Repository contract change

Edit `shipyard/packages/blocks-financial-ar/Services/IInvoiceRepository.cs`:

```csharp
public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(TenantId tenantId, InvoiceId id, CancellationToken ct);
    Task<IReadOnlyList<Invoice>> ListByTenantAsync(TenantId tenantId, /* existing filters */, CancellationToken ct);
    Task<IReadOnlyList<Invoice>> ListByLeaseAsync(TenantId tenantId, LeaseId leaseId, /* filters */, CancellationToken ct);
    // NEW: every method gains tenantId as first parameter
    Task AddAsync(TenantId tenantId, Invoice invoice, CancellationToken ct);
    Task UpsertAsync(TenantId tenantId, Invoice invoice, CancellationToken ct);
    // ... etc for every existing method
}
```

**Pattern:** `tenantId` is the FIRST parameter on every method (consistent positional convention; auditable via grep). Methods that load an entity by id verify `loaded.TenantId == tenantId` before returning; mismatched tenant returns `null` (same code path as not-found — no diagnostic leak).

#### 3.2 In-memory implementation

Edit `shipyard/packages/blocks-financial-ar/InMemory/InMemoryInvoiceRepository.cs`:

- Storage shape stays `Dictionary<InvoiceId, Invoice>` (do NOT shard by tenant; entities ALREADY carry `TenantId` via `IMustHaveTenant`).
- Every `Get`/`List` filters by `tenantId` first.
- `Add` / `Upsert` assert `invoice.TenantId == tenantId` at the boundary; throw `ArgumentException` on mismatch (caller bug — never reachable in correct code; defensive).

#### 3.3 Invoice IMustHaveTenant compliance (close FED §4 soft gate)

Edit `shipyard/packages/blocks-financial-ar/Models/Invoice.cs`:

- Add `: IMustHaveTenant` to the type declaration (matches `Payment` + `Bill` pattern).
- Verify `TenantId` field is already non-nullable + required (per FED survey, the field exists but the interface isn't implemented).

#### 3.4 Service-layer threading

Edit `shipyard/packages/blocks-financial-ar/Services/InvoicePostingService.cs` (and any other service inside `blocks-financial-ar` that calls `IInvoiceRepository`):

- Inject `ITenantContext` into the ctor (mirror cohort-1 / W#68 PR 3 Option A pattern).
- Every call to `_invoices.GetAsync(...)` / `_invoices.ListByTenantAsync(...)` / etc passes `tenantContext.TenantId` as the first argument.

#### 3.5 Tests (≥6 new)

New + updated tests in `shipyard/packages/blocks-financial-ar/tests/`:

- `InvoiceRepository_GetAsync_CrossTenant_ReturnsNull` — caller A loads tenant-B invoice id → returns `null`.
- `InvoiceRepository_ListByTenant_FiltersByTenant` — repository populated with invoices from two tenants; list-call with tenant A returns only tenant-A rows.
- `InvoiceRepository_AddAsync_TenantMismatch_Throws` — caller passes `tenantId=A` but `invoice.TenantId=B` → `ArgumentException`.
- `InvoicePostingService_CrossTenantInvoice_ReturnsUnknownInvoice` — service-layer mirror of cohort-1 / Option A pattern.
- `Invoice_ImplementsIMustHaveTenant_TypeCheck` — reflection or compile-time assertion that `Invoice : IMustHaveTenant`.
- Update existing tests in `InvoicePostingServiceTests.cs` to pass tenant through call sites.

#### 3.6 Pattern conformance

```
@candidate-pattern: pattern-009-tenant-keying-retrofit (FIRST INSTANCE — this is the qualifying shipping for the pattern; promote to formal after PR 0b / 0c / 0d also ship clean)
```

PR 0a is the structural template the other three PR 0 PRs mirror.

#### 3.7 Do NOT in this PR

- Do NOT touch `blocks-financial-ap`, `blocks-financial-payments`, `blocks-financial-ledger` — those are PR 0b / 0c / 0d.
- Do NOT touch Bridge endpoints — out of scope (Bridge consumes the new contract in PR 2 AccountingPage).
- Do NOT extend invoice domain logic (no new fields, no new business methods); contract-shape change ONLY.

---

### PR 0b — `blocks-financial-ap` tenant-keyed repository contract

**Estimated effort:** ~1.5h
**Scope:** same as PR 0a, applied to `IBillRepository` + `InMemoryBillRepository` + `BillPostingService`
**Commit subject:** `feat(blocks-financial-ap): tenant-key IBillRepository contract (cohort 2 PR 0b)`
**Branch:** `engineer/cohort-2-pr-0b-blocks-financial-ap-tenant-keying`
**Depends on:** PR 0a merged (sequential for review-load + pattern-009-tenant-keying-retrofit pattern matching)
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — second instance of pattern-009-tenant-keying-retrofit.

Mirrors PR 0a structurally:

- `IBillRepository` methods gain `tenantId` first parameter.
- `InMemoryBillRepository` filters by tenant; assert on mismatch.
- Verify `Bill : IMustHaveTenant` (per FED survey context — should already be compliant; double-check).
- `BillPostingService` injects `ITenantContext`; threads through all repo calls.
- ≥5 new/updated tests parallel to PR 0a's set.

Pattern conformance: `@candidate-pattern: pattern-009-tenant-keying-retrofit (second instance)`.

---

### PR 0c — `blocks-financial-payments` tenant-keyed repository contract

**Estimated effort:** ~2h
**Scope:** `IPaymentRepository` + `IPaymentApplicationRepository` tenant-keyed; `DefaultPaymentApplicationService` updated to USE the new contracts (W#68 PR 3 Option A SERVICE-layer guards become defense-in-depth alongside REPO-layer filtering)
**Commit subject:** `feat(blocks-financial-payments): tenant-key payment repository contracts (cohort 2 PR 0c)`
**Branch:** `engineer/cohort-2-pr-0c-blocks-financial-payments-tenant-keying`
**Depends on:** PR 0b merged; W#68 PR 3 Option A amendment on main (the service-layer companion)
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — write-path-adjacent + companion to the W#68 PR 3 Option A amendment council already issued AMBER on. Sec-eng should self-attest GREEN on this PR with reference to the W#68 PR 3 verdict if PR 0c implementation matches their Option B request.

#### 3.8 Repository contract change

Edit both `shipyard/packages/blocks-financial-payments/Services/IPaymentRepository.cs` and `IPaymentApplicationRepository.cs`:

Same shape as PR 0a — every method gains `tenantId` first parameter.

#### 3.9 PaymentApplication IMustHaveTenant compliance

Per W#68 PR 3 sec-eng verdict Item 8: `PaymentApplication` SHOULD implement `IMustHaveTenant`. **If W#68 PR 3 Option A amendment already added this**, verify and proceed; otherwise add `: IMustHaveTenant` + required `TenantId` field to `PaymentApplication.cs`, populated from `payment.TenantId` at the `PaymentApplication.Create` call site (line ~248 per W#68 verdict).

#### 3.10 Service-layer integration

Edit `DefaultPaymentApplicationService.cs`:

- `ITenantContext` is already injected (per W#68 PR 3 Option A amendment).
- Every call to `_payments.GetAsync(...)` / `_applications.GetAsync(...)` / `_invoices.GetAsync(...)` / `_bills.GetAsync(...)` now passes `tenantContext.TenantId` as the first parameter.
- The Option A SERVICE-layer guard (verify `loaded.TenantId == tenantContext.TenantId` after load) stays in place as defense-in-depth — under PR 0c contracts, the repo returns `null` for cross-tenant queries, so the Option A guards become unreachable in correct code. Keep them; flag with a comment: `// Defense-in-depth — repo-layer filter (cohort-2 PR 0c) blocks cross-tenant load; this is the service-layer check from W#68 PR 3 Option A.`

#### 3.11 Tests (≥8 new)

Parallel to PR 0a's set plus:

- `PaymentApplicationRepository_GetAsync_CrossTenant_ReturnsNull` (new — substrate-level).
- `DefaultPaymentApplicationService_ApplyAsync_CrossTenantPayment_AfterPR0c_StillRejected` (regression on Option A behavior under PR 0c contracts — service-layer guard becomes unreachable but the returned error is still `UnknownPayment`, not `null`-propagation crash).
- `PaymentApplication_ImplementsIMustHaveTenant_TypeCheck`.

Pattern conformance: `@candidate-pattern: pattern-009-tenant-keying-retrofit (third instance — pattern qualifies for ratification on PR 0d merge)`.

---

### PR 0d — `blocks-financial-ledger` tenant-keyed repository contract

**Estimated effort:** ~1.5h
**Scope:** `IJournalRepository` (or whatever ledger primitive is canonical) tenant-keyed
**Commit subject:** `feat(blocks-financial-ledger): tenant-key journal repository contract (cohort 2 PR 0d)`
**Branch:** `engineer/cohort-2-pr-0d-blocks-financial-ledger-tenant-keying`
**Depends on:** PR 0c merged
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — fourth instance; this PR's clean shipping is the trigger to promote `pattern-009-tenant-keying-retrofit` from `@candidate-pattern` to formal `@standing-pattern` (Admiral promotes via `admiral-status-*-pattern-009-tenant-keying-retrofit-promoted.md`).

Mirrors PR 0a structurally. Final substrate cleanup before the rebind PRs begin.

**Pre-flight verification before PR 0d:** confirm the canonical journal repository surface name. The FED survey enumerates `IJournalRepository` as the likely name; verify against `shipyard/packages/blocks-financial-ledger/Services/` actual surface. If the canonical primitive has a different name (e.g., `ILedgerEntryRepository`), use the actual name and update the hand-off log.

Pattern conformance: `@candidate-pattern: pattern-009-tenant-keying-retrofit (fourth instance — pattern qualifies for formal ratification)`.

---

### PR 1 — `/api/v1/financial/payments?leaseId=` + `LeaseDetailPage` payments rebind (RB-8)

**Estimated effort:** ~1h
**Scope:** new Bridge route `/api/v1/financial/payments` (GET only); reuse cohort-1 `AuthenticatedTenantPolicy`; new frontend client; `usePayments` hook rebind in `LeaseDetailPage`
**Commit subject:** `feat(anchor-react,bridge): rebind LeaseDetailPage payments to /api/v1/financial/payments (cohort 2 PR 1)`
**Branch:** `engineer/cohort-2-pr-1-leasedetail-payments-rebind`
**Depends on:** PR 0c merged (payments repository tenant-keyed); W#73 (per-lease payment queries on `blocks-financial-ar` — verify status before PR 1)
**Pre-merge council:** NOT required — mechanical mirror of cohort-1 PR 1/2 cluster-endpoint pattern; pattern-009 formal.

#### 3.12 Bridge endpoint family — `/api/v1/financial/payments`

New file: `signal-bridge/Sunfish.Bridge/Financial/PaymentsEndpoints.cs`.

Pattern lifted from cohort-1 `LeasesEndpoints.cs` with these adaptations:

1. **Route prefix:** `/api/v1/financial/payments` (top-level financial family).
2. **Authorization:** `AuthenticatedTenantPolicy` (REUSE from cohort-1).
3. **Endpoint:** single `GET /` with required `?leaseId=` query parameter; returns list of payments for that lease (tenant-scoped).
4. **DTO shape:**

```text
PaymentListDto:
  payments: PaymentSummaryDto[]

PaymentSummaryDto:
  paymentId: string         // Payment.Id.Value
  leaseId: string           // Payment.LeaseId.Value
  amount: decimal           // Payment.Amount
  currency: string          // Payment.Currency (ISO 4217)
  direction: string         // "Inbound" | "Outbound"
  receivedAt: string        // ISO timestamp
  status: string            // Payment.Status.ToString()
```

5. **Handler signature:**

```csharp
internal static async Task<Results<Ok<PaymentListDto>, BadRequest<ProblemDetails>>>
  HandleListPaymentsAsync(
      [FromQuery] string leaseId,
      ITenantContext tenantContext,
      IPaymentService payments,
      CancellationToken ct)
```

Validation:
- `leaseId` required, parses to `LeaseId` (return 400 with ProblemDetails on miss).

Implementation calls `payments.ListByLeaseAsync(tenantContext.TenantId, LeaseId.From(leaseId), ct)` — the new tenant-keyed contract from PR 0c (plus the W#73 per-lease query shape from `blocks-financial-ar`).

6. **Registration:** add `MapPaymentsEndpoints` extension; wire in `Program.cs` next to `MapLeasesEndpoints()`.

#### 3.13 Frontend client + hook rebind

New file: `sunfish/apps/web/src/api/financial.ts` (or split into `financial/payments.ts` if the surface grows).

```text
export async function listPaymentsByLease(leaseId: string): Promise<PaymentListResponse> {
  const resp = await fetch(`/api/v1/financial/payments?leaseId=${encodeURIComponent(leaseId)}`, {
    credentials: 'include',
  })
  // ... standard error handling per cohort-1 leases.ts pattern ...
}
```

Edit `sunfish/apps/web/src/hooks/usePayments.ts` (or wherever the hook lives — verify against current source layout; FED survey references `usePayments()`):

- Swap import: `getPayments` from `@/api/erpnext` → `listPaymentsByLease` from `@/api/financial`.
- Hook now takes a required `leaseId` parameter (cohort-1 H5 documented the ID format divergence; cohort-2 PR 1 closes the regression by using new financial IDs end-to-end).

Edit `sunfish/apps/web/src/pages/LeaseDetailPage.tsx`:
- Remove the cohort-1-era "Payment history will appear after the next migration step" banner (H5 from cohort-1).
- Wire `usePayments(lease.leaseId)` into the existing payments section.
- Update field mapping: `payment.name` → `payment.paymentId`, `payment.posting_date` → `payment.receivedAt`, etc.

#### 3.14 Tests

New: `signal-bridge/Sunfish.Bridge.Tests.Unit/Financial/PaymentsEndpointsTests.cs` (~6-8 tests):
- `ListPayments_AuthenticatedTenant_ReturnsPayments`
- `ListPayments_UnauthenticatedCaller_Returns401`
- `ListPayments_MissingLeaseId_Returns400`
- `ListPayments_InvalidLeaseId_Returns400`
- `ListPayments_CrossTenant_LeaseFromOtherTenant_ReturnsEmpty` (regression on PR 0c contract — payments from other tenant's lease are NOT returned)
- `ListPayments_LeaseWithNoPayments_ReturnsEmpty`

Frontend tests: update `LeaseDetailPage.test.tsx` or its payments-section test; MSW mocks for the new endpoint.

#### 3.15 Pattern conformance

```
@standing-pattern: pattern-009 (cluster-endpoint rebind pair — formal post-cohort-1)
```

#### 3.16 Do NOT in this PR

- Do NOT add `POST /` to the payments family — PR 3.
- Do NOT touch the accounting endpoints — PR 2.
- Do NOT introduce idempotency keys on the GET — naturally idempotent.

---

### PR 2 — `/api/v1/financial/accounting/{summary,outstanding}` + `AccountingPage` rebind (RB-7)

**Estimated effort:** ~1h
**Scope:** two GET endpoints; AccountingPage rebind; reuse `AuthenticatedTenantPolicy`
**Commit subject:** `feat(anchor-react,bridge): rebind AccountingPage to /api/v1/financial/accounting (cohort 2 PR 2)`
**Branch:** `engineer/cohort-2-pr-2-accountingpage-rebind`
**Depends on:** PR 1 merged (sequential); PR 0a merged (Invoice IMustHaveTenant compliance landed)
**Pre-merge council:** NOT required — pattern-009 formal mirror.

#### 3.17 Bridge endpoint family — `/api/v1/financial/accounting`

New file: `signal-bridge/Sunfish.Bridge/Financial/AccountingEndpoints.cs`.

Two endpoints:

1. **`GET /summary`** — returns aggregated AR balances for the tenant (total invoiced, total received, total outstanding, by-period buckets).
2. **`GET /outstanding`** — returns a list of outstanding invoice rows (per-invoice detail; tenant-scoped).

Consumes `IFinancialArService` (from `blocks-financial-ar`; the new tenant-keyed contract from PR 0a). Both endpoints derive `tenantId` from `ITenantContext` server-side.

DTO shapes mirror the existing ERPNext payload shapes consumed by `AccountingPage` today (field names: verify by reading `sunfish/apps/web/src/api/erpnext.ts` `getAccountingSummary()` / `getAccountingOutstanding()` and inspecting `AccountingPage.tsx` consumption). **Do not invent new field names** — preserve the React-side type shape to keep the rebind mechanical.

#### 3.18 Frontend client + page rebind

Extend `sunfish/apps/web/src/api/financial.ts` with `getAccountingSummary()` + `getAccountingOutstanding()`.

Edit `sunfish/apps/web/src/pages/AccountingPage.tsx`:
- Swap imports from `@/api/erpnext` → `@/api/financial`.
- TanStack query keys stay the same shape.

#### 3.19 Tests

New: `Sunfish.Bridge.Tests.Unit/Financial/AccountingEndpointsTests.cs` (~6 tests):
- `GetSummary_AuthenticatedTenant_ReturnsAggregates`
- `GetSummary_Unauthenticated_Returns401`
- `GetSummary_CrossTenant_ZeroBalances` (no leakage)
- `GetOutstanding_AuthenticatedTenant_ReturnsRows`
- `GetOutstanding_CrossTenant_Empty`
- `GetOutstanding_EmptyAr_ReturnsEmpty`

Frontend: update `AccountingPage.test.tsx`; MSW mocks.

#### 3.20 Pattern conformance

```
@standing-pattern: pattern-009
```

#### 3.21 Pre-flight verification (FED §4 soft gate)

Before PR 2 opens, verify PR 0a closed the `Invoice : IMustHaveTenant` compliance gap. If Invoice still lacks the interface (PR 0a deferred for any reason), **STOP** + file `engineer-question-*`; PR 2 depends on the AR DI auto-filter wiring.

---

### PR 3 — `POST /api/v1/financial/payments` + `RentCollectionPage` rebind (RB-9)

**Estimated effort:** ~2h (incl. CSRF + audit + sec-eng wait)
**Scope:** new write-path endpoint; CSRF antiforgery; `AuditEventType.PaymentRecorded` emission; `RentCollectionPage` rebind with token round-trip UX
**Commit subject:** `feat(anchor-react,bridge): rebind RentCollectionPage POST to /api/v1/financial/payments (cohort 2 PR 3)`
**Branch:** `engineer/cohort-2-pr-3-rentcollection-rebind`
**Depends on:** PR 2 merged; PR 0c merged (payments repository tenant-keyed); W#68 PR 3 Option A on main (service-layer tenant isolation precedent)
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — write path with cross-tenant + CSRF + audit-emission surfaces. Council scope:
  - Does the POST handler correctly enforce CSRF via `IAntiforgery.ValidateRequestAsync` (mirror of cohort-1 PR 3 `HandleCreateWorkOrderAsync`)?
  - Does the handler thread `tenantContext.TenantId` into the `IPaymentService.RecordAsync` call?
  - Is `AuditEventType.PaymentRecorded` emitted via `IDomainEventPublisher` (NOT the noop) after successful mutation?
  - Does the handler reject cross-tenant lease references (caller submits a `leaseId` belonging to tenant B → return 400 or 404, no diagnostic leak)?
  - Are at least 3 sec-relevant tests present (CSRF rejection, cross-tenant lease rejection, audit-emission round-trip)?

#### 3.22 Bridge endpoint extension — `POST /api/v1/financial/payments`

Extend `signal-bridge/Sunfish.Bridge/Financial/PaymentsEndpoints.cs` (from PR 1).

Handler signature (mirror cohort-1 PR 3 `HandleCreateWorkOrderAsync`):

```csharp
internal static async Task<Results<Ok<RecordPaymentResponse>, BadRequest<ProblemDetails>, UnauthorizedHttpResult>>
  HandleRecordPaymentAsync(
      RecordPaymentRequest request,
      ITenantContext tenantContext,
      IAntiforgery antiforgery,
      IPaymentService payments,
      IAuditTrail audit,
      HttpContext http,
      CancellationToken ct)
```

Flow:
1. `await antiforgery.ValidateRequestAsync(http)` — throws if token missing/invalid.
2. Validate request fields: `leaseId` parses to `LeaseId`; `amount` > 0; `currency` is non-empty ISO 4217 (uppercase); `direction` parses to `PaymentDirection`.
3. Verify `leaseId` exists for the tenant (calls `leases.GetByIdAsync(tenantContext.TenantId, leaseId, ct)`; cross-tenant lease returns `null` per cohort-1 contract → respond 400 with generic "lease not found" — no tenant-B information leaked).
4. `payments.RecordAsync(tenantContext.TenantId, request.ToPayment(), ct)`.
5. Emit `AuditEventType.PaymentRecorded` via `audit.RecordAsync(...)` after successful mutation.
6. Return `Ok(new RecordPaymentResponse(paymentId, status, recordedAt))`.

#### 3.23 New AuditEventType constant

Edit `shipyard/packages/kernel-audit/AuditEventType.cs`:

Add `PaymentRecorded` constant adjacent to existing `PaymentAuthorized` (~line 56 per W#68 PR 3 verdict Item 9 context). Update the audit-trail filter test if one exists.

#### 3.24 Request DTO

```text
RecordPaymentRequest:
  leaseId: string          // required; lease must exist on tenant
  amount: decimal          // required; > 0
  currency: string         // required; ISO 4217 uppercase (e.g., "USD")
  direction: 'Inbound' | 'Outbound'   // required
  paidAt: string?          // optional ISO timestamp; defaults to server time if absent
  externalRef: string?     // optional caller-supplied reference (check number, etc.)

RecordPaymentResponse:
  paymentId: string
  status: string           // initial state (typically "Received" or "Pending")
  recordedAt: string       // ISO timestamp
```

#### 3.25 Frontend client + page rebind

Extend `sunfish/apps/web/src/api/financial.ts`:

```text
export async function recordPayment(input: RecordPaymentInput): Promise<RecordPaymentResult> {
  // 1. Fetch antiforgery token (mirror cohort-1 PR 3 cockpit/api.ts createWorkOrder flow)
  const tokenResp = await fetch('/antiforgery/token', { credentials: 'include' })
  // ... extract token from header or body per cohort-1 convention ...

  // 2. POST with token in header
  const resp = await fetch('/api/v1/financial/payments', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'RequestVerificationToken': antiforgeryToken,
    },
    body: JSON.stringify(input),
  })
  // ... standard error handling ...
}
```

Edit `sunfish/apps/web/src/pages/RentCollectionPage.tsx`:
- Swap `recordPayment` import from `@/api/erpnext` → `@/api/financial`.
- Error UX: explicit error states for (a) token-fetch failure, (b) token-rejection on submit (likely a stale page → suggest reload), (c) lease-not-found (server-side cross-tenant rejection), (d) generic 5xx.

#### 3.26 Tests (≥10 new)

Bridge endpoint integration tests:
- `RecordPayment_ValidRequest_ReturnsCreatedId`
- `RecordPayment_MissingAntiforgeryToken_Returns400`
- `RecordPayment_InvalidAntiforgeryToken_Returns400`
- `RecordPayment_MissingLeaseId_Returns400`
- `RecordPayment_NegativeAmount_Returns400`
- `RecordPayment_NonIso4217Currency_Returns400`
- `RecordPayment_CrossTenantLease_Returns400` (NO diagnostic leak about tenant-B)
- `RecordPayment_CrossTenantLease_NoAuditEvent` (audit emission suppressed on rejected path)
- `RecordPayment_SuccessfulMutation_EmitsAuditEventViaRealPublisher` (asserts NOT-noop publisher captured the event; closes W#68 PR 3 verdict Item 9 forward-watch for the payments cluster)
- `RecordPayment_Unauthenticated_Returns401`

Frontend tests: update `RentCollectionPage.test.tsx`; MSW mocks for token-fetch + POST; assert all four error states render correctly.

#### 3.27 Pattern conformance

```
@standing-pattern: pattern-009 (cluster-endpoint rebind pair — formal post-cohort-1)
```

Plus an explicit forward-claim:

```
@candidate-pattern: pattern-010-financial-write-path (Bridge POST endpoint + CSRF + tenant-derived audit emission + cross-tenant rejection without diagnostic leak — first instance in financial cluster; subsequent financial write paths can pattern-match)
```

ONR proposes `pattern-010` for catalog ratification IF PR 3 ships clean and one subsequent financial write path (e.g., refund / void) pattern-matches; otherwise stays as a candidate for future ratification.

#### 3.28 Do NOT in this PR

- Do NOT add `Voided` / `Bounced` transitions — separate workstream (W#68 follow-on or cohort-3).
- Do NOT add the `@deprecated` mark to `erpnext.ts.recordPayment` — that's PR 4.
- Do NOT introduce a refund or unapply endpoint — substrate `UnapplyAsync` exists on `IPaymentApplicationService` but is NOT cohort-2 surface.

---

### PR 4 — Cohort 2 cleanup, smoke extension, docs, ledger flip

**Estimated effort:** ~1.5h
**Scope:** mark three `erpnext.ts` financial exports `@deprecated` with dev-mode console warning; extend cohort-1 Playwright CDP E2E smoke to assert the three financial pages render without ERPNext network calls; append cohort-2 section to `apps/docs/anchor/anchor-react-rebind.md`; flip W#76 ledger row from `building` → `built`
**Commit subject:** `chore(anchor-react,docs): cohort 2 cleanup — deprecate erpnext financial entries, extend smoke test, flip W#76 (cohort 2 PR 4)`
**Branch:** `engineer/cohort-2-pr-4-cleanup`
**Depends on:** PR 3 merged
**Pre-merge council:** NOT required (cleanup + docs + ledger flip). **CIC sees this PR regardless of pre-authorization** — ledger-flip PR per pre-auth ruling §Step 4.

#### 3.29 ERPNext deprecation marks

Edit `sunfish/apps/web/src/api/erpnext.ts`. For each financial export consumed by Cohort 2:

- `getAccountingSummary()`
- `getAccountingOutstanding()`
- `getPayments()` (used by `usePayments()` against the old ID-format)
- `recordPayment()`

Add JSDoc `@deprecated` + dev-mode console warning (mirror cohort-1 PR 4 §3.26 shape):

```text
/**
 * @deprecated Cohort 2 (W#76) rebound this surface to /api/v1/financial/payments.
 * Use `import { recordPayment } from '@/api/financial'` instead.
 * This ERPNext-backed entry will be removed in Cohort 4 (RB-12) per the
 * Anchor React rebind roadmap.
 */
```

#### 3.30 E2E smoke extension

Extend cohort-1's Playwright CDP smoke test (lives in `sunfish/apps/web/tests/e2e/` per cohort-1 PR 4 §3.27) to add three new page scenarios:

- `AccountingPage` renders against a Bridge with ERPNext stopped (PR 0a invoice fixtures seeded).
- `LeaseDetailPage` payments section renders (PR 0c payment fixtures seeded; uses the new `?leaseId=` query).
- `RentCollectionPage` record-payment flow round-trips: fetches antiforgery token, posts, sees success state, network panel shows zero `/api/v1/erpnext/payments` calls.

Network-panel assertion: each page makes zero requests to `/api/v1/erpnext/*` for the financial endpoints; allowlist non-financial ERPNext calls (e.g., property list if Cohort 2 doesn't rebind it — but cohort-1 did, so the allowlist should be empty for the three pages in scope).

#### 3.31 Docs running log

Append cohort-2 section to `sunfish/apps/docs/anchor/anchor-react-rebind.md` (path may vary; verify cohort-1 PR 4 placement):

- Pages rebound (3): AccountingPage, LeaseDetailPage payments, RentCollectionPage.
- Bridge endpoints introduced: `/api/v1/financial/payments` (GET + POST), `/api/v1/financial/accounting/{summary,outstanding}`.
- Substrate work: PR 0a/b/c/d tenant-keyed repository contracts on the financial cluster.
- Standing-pattern claims: `pattern-009` (formal, for rebind pairs); `pattern-009-tenant-keying-retrofit` (qualifying-candidate, four-instance shipping triggers formal ratification); `pattern-010-financial-write-path` (forward-candidate from PR 3).
- ERPNext deprecation timeline: marks land here; deletion at Cohort 4 (RB-12).

#### 3.32 Ledger flip

Update `shipyard/icm/_state/workstreams/W76-anchor-react-rebind-cohort-2.md` source row from `building` → `built`; run render-ledger; commit the rendered `active-workstreams.md` change.

#### 3.33 Status beacon

File `coordination/inbox/engineer-status-2026-05-XXTHH-MMZ-w76-cohort-2-built.md` naming each PR's merge SHA, summarizing the four PR 0 instances of `pattern-009-tenant-keying-retrofit`, and proposing the pattern for catalog ratification.

---

## 4. Cross-cluster integration

| Frontend page | Frontend hook | Frontend client | Bridge endpoint | Bridge handler | Repository / service |
|---|---|---|---|---|---|
| `AccountingPage.tsx` | `useAccountingSummary()` + `useAccountingOutstanding()` | `sunfish/apps/web/src/api/financial.ts` | `GET /api/v1/financial/accounting/summary` + `/outstanding` | `Sunfish.Bridge.Financial.AccountingEndpoints.HandleGetSummaryAsync` + `HandleGetOutstandingAsync` | `Sunfish.Blocks.FinancialAr.Services.IFinancialArService.*` (tenant-keyed per PR 0a) |
| `LeaseDetailPage.tsx` (payments section) | `usePayments(leaseId)` | `sunfish/apps/web/src/api/financial.ts` | `GET /api/v1/financial/payments?leaseId=` | `Sunfish.Bridge.Financial.PaymentsEndpoints.HandleListPaymentsAsync` | `Sunfish.Blocks.FinancialPayments.Services.IPaymentService.ListByLeaseAsync(TenantId, LeaseId, ct)` (tenant-keyed per PR 0c) |
| `RentCollectionPage.tsx` | TanStack mutation | `sunfish/apps/web/src/api/financial.ts` | `POST /api/v1/financial/payments` | `Sunfish.Bridge.Financial.PaymentsEndpoints.HandleRecordPaymentAsync` | `Sunfish.Blocks.FinancialPayments.Services.IPaymentService.RecordAsync(TenantId, Payment, ct)` (tenant-keyed per PR 0c) |

All Bridge handlers resolve `TenantId` via `ITenantContext`; no tenant filter is accepted as a query parameter (cohort-1 inherited).

---

## 5. Idempotency-key catalog

**Read endpoints:** naturally idempotent at the protocol level; no idempotency keys.

**`POST /api/v1/financial/payments`:** NOT marked idempotent in cohort-2 v1. A double-submit creates two payments. This matches the cohort-1 `POST /api/v1/maintenance/work-orders/` pattern (also not idempotent in v1).

Follow-on consideration: financial double-submits have higher operational consequence than work-order double-submits (duplicate revenue records vs duplicate tickets). If user behavior shows real duplicate-submit patterns, a follow-on PR adds `Idempotency-Key` header support — caller-supplied key is hashed alongside the request body; second submit within 24h returns the same `RecordPaymentResponse`. **Not in cohort-2 scope; logged as forward-watched (see §7).**

---

## 6. Dependencies + sequence

**Critical path:** PR 0a → PR 0b → PR 0c → PR 0d → PR 1 → PR 2 → PR 3 → PR 4. Sequential for review-load smoothing + pattern-009-tenant-keying-retrofit pattern-matching across the PR 0 cluster.

**Parallel work possible (Engineer's call at hand-off review):**
- PR 0a + PR 0b are technically independent (different blocks); could ship in parallel. Sec-eng SPOT-CHECK is the bottleneck.
- PR 0c MUST land before PR 1 (PR 1 reads through the tenant-keyed payments contract).
- PR 0a MUST land before PR 2 (PR 2 reads through the tenant-keyed AR contract).
- PR 0d is the slowest critical-path dependency-wise; could ship in parallel with PR 1 if Engineer prefers.

**External gates (non-blocking but noted):**
- W#73 (per-lease payment queries on `blocks-financial-ar`) — gates PR 1's `?leaseId=` query shape. Verify status before PR 1 opens.
- W#68 PR 3 Option A amendment on main — gates PR 0c (Option A service-layer guards become defense-in-depth alongside PR 0c repo-layer filtering).
- Future MaintenancePage status-update endpoint — cohort-2 doesn't touch this; deferred.

---

## 7. Forward-watched concerns (logged; not blocking cohort-2)

Drawn primarily from W#68 PR 3 sec-eng verdict (2026-05-19T00-45Z) + cohort-2 surface analysis. Cohort-2 does NOT close these; each becomes its own workstream or block-engineering follow-up.

1. **Persistence hand-off MUST add transactions** (W#68 PR 3 verdict Item 5) — gates the SQLite/Postgres `IPaymentRepository` / `IInvoiceRepository` / `IBillRepository` / `IJournalRepository` persistence implementation. `SERIALIZABLE` isolation OR optimistic concurrency via `Version` field. **Lifted into the future `blocks-financial-*-persistence-stage06-handoff.md`.**

2. **Persistence hand-off MUST use outbox pattern for event emission** (Item 6) — same gate. Real `IDomainEventPublisher` implementations can fail; outbox-table-in-same-tx is the canonical resolution.

3. **Bridge-level audit-emission integration test** (Item 9) — PARTIALLY closed by cohort-2 PR 3 §3.26 test `RecordPayment_SuccessfulMutation_EmitsAuditEventViaRealPublisher`. The broader concern (Bridge composition root wires real publisher across ALL payment events, not just `PaymentRecorded`) remains for the persistence hand-off + a future `AuditEventType.PaymentApplied` / `PaymentUnapplied` constants follow-up.

4. **`AuditEventType.PaymentApplied` / `PaymentUnapplied` constants** (Item 9 follow-up) — kernel-audit mechanical follow-up. Cohort-2 PR 3 adds `PaymentRecorded`; the Apply/Unapply pair is the W#68 PR 3 surface, NOT cohort-2 write-path. Bundle into a small kernel-audit PR.

5. **`ApplyError.PaymentTerminal` + `DiscountWriteoffUnsupported` constants** (Items 2 + 3) — bundled into the discount-account-selection follow-on PR.

6. **`Currency` field validation at entity construction** (Item 4) — defensive-depth; ISO 4217 validation OR uppercase normalization on `Payment.Create` / `Invoice.Create` / `Bill.Create`. Cohort-2 PR 3 §3.22 step 2 validates currency at the BRIDGE boundary (uppercase ISO 4217 required); entity-level validation is the deeper fix.

7. **`UnapplyFromInvoiceAsync` status-restore data-loss** (Item 7) — minor data-loss; block-engineering follow-up on `blocks-financial-payments`.

8. **Substrate-wide tenant-keyed repository contract** (Item 8 Option B) — **THIS IS cohort-2's PR 0 cluster.** When PR 0d ships, this forward-watch closes.

9. **ReportsEndpoints tenant-keying audit** (FED §6 cohort-3 pre-flight) — cohort-3 scope. Cohort-2 does NOT touch.

10. **Idempotency-Key header on `POST /api/v1/financial/payments`** (§5 above) — follow-on if operational data shows duplicate-submit patterns.

11. **Refund / unapply Bridge surface** — substrate `UnapplyAsync` exists on `IPaymentApplicationService` but has no Bridge surface; future workstream.

---

## 8. Test plan summary

| PR | New Bridge / block tests | New frontend tests | Sec-eng SPOT-CHECK | Total |
|---|---|---|---|---|
| PR 0a (AR tenant-keying) | ~6 | — | YES (first instance) | ~6 |
| PR 0b (AP tenant-keying) | ~5 | — | YES (second instance) | ~5 |
| PR 0c (payments tenant-keying) | ~8 | — | YES (third instance — write-adjacent) | ~8 |
| PR 0d (ledger tenant-keying) | ~5 | — | YES (fourth — pattern-ratification trigger) | ~5 |
| PR 1 (LeaseDetail payments) | ~7 | ~3 | — | ~10 |
| PR 2 (AccountingPage) | ~6 | ~3 | — | ~9 |
| PR 3 (RentCollectionPage) | ~10 | ~5 | YES (write-path + CSRF + audit) | ~15 |
| PR 4 (cleanup + smoke) | E2E smoke (3 new scenarios) | — | — | ~3 E2E |
| **Total** | **~47 unit/integration + 3 E2E** | **~11** | **5 sec-eng SPOT-CHECKs** | **~61** |

### Cohort-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds for `Sunfish.Bridge` + every test project + every `blocks-financial-*` package.
**A2.** `pnpm --filter @sunfish/web test` passes all PR 1 + PR 2 + PR 3 frontend tests.
**A3.** `pnpm --filter @sunfish/web build` succeeds.
**A4.** E2E smoke (cohort-1 baseline + cohort-2 three new scenarios) passes against a seeded Bridge with ERPNext **NOT running**.
**A5.** Network-panel verification: each of the three financial pages renders without `/api/v1/erpnext/*` calls.
**A6.** ERPNext routes still resolve at the Bridge level (passthrough not deleted).
**A7.** `sunfish/apps/docs/anchor/anchor-react-rebind.md` cohort-2 section published + linked in TOC.
**A8.** Workstream W#76 ledger row reads `built`; PR refs #N..#N+7 captured.
**A9.** `coordination/inbox/engineer-status-2026-05-XXTHH-MMZ-w76-cohort-2-built.md` beacon dropped.
**A10.** No `@deviation-from-spec:` flag landed without CIC acknowledgement.
**A11.** Sec-eng SPOT-CHECK GREEN on PR 0a / 0b / 0c / 0d (four instances of pattern-009-tenant-keying-retrofit).
**A12.** Sec-eng SPOT-CHECK GREEN on PR 3 (write-path + CSRF + audit).
**A13.** `pattern-009-tenant-keying-retrofit` proposal: if PR 0a..0d ship clean, the W#76 status beacon includes a section proposing pattern ratification at CIC's next standing-patterns review.

---

## 9. Halt conditions (`engineer-question-*` beacons)

### H1. W#68 PR 1 hasn't merged at PR 1 execution time

**Symptom:** PR 1 cannot resolve `Payment` entity type when building the new Bridge endpoint family.

**Mitigation:** Authoring is unblocked even when W#68 PR 1 is gated (per directive 2026-05-19T00-35Z). Execution START on PR 1/2/3 must wait. PR 0a/b/d can ship immediately; PR 0c waits on Option A.

**Halt:** if PR 1 needs to start and `Payment` isn't on main, **STOP** + file `engineer-question-2026-05-XXTHH-MMZ-w76-pr1-payment-missing.md`. Admiral routes — typically a wait, not a scope change.

### H2. `/api/v1/financial/*` family naming conflicts with existing cockpit family

**Symptom:** Pre-build §2.4 scan finds an open PR or existing branch that's already adding `/api/v1/cockpit/financial/*` or similar — naming overlap that would cause merge conflicts or surface-confusion.

**Mitigation:** Confirm the chosen path (`/api/v1/financial/*` top-level, NOT under `/api/v1/cockpit/financial/*`) — this is consistent with cohort-1's `AuthenticatedTenantPolicy`-on-top-level pattern.

**Halt:** if a competing surface plan exists, **STOP** + file `onr-question-*` for Admiral to deconflict. Re-author the hand-off if the path needs to change.

### H3. `Invoice : IMustHaveTenant` adoption ripples beyond AR

**Symptom:** Adding `: IMustHaveTenant` to `Invoice.cs` in PR 0a surfaces consumers (downstream services, sunfish/apps/desktop, signal-bridge listeners) that didn't expect the interface contract — failures in the form of missing field initializers or "TenantId not set" runtime exceptions during entity construction.

**Mitigation:** Cohort-1's `Lease` retrofit (W#27 Party retrofit, separate workstream) faced a similar shape. The cohort-2 hand-off does NOT contain a full Invoice retrofit; if PR 0a discovers ripples beyond ~10 LOC of incidental fixes, split.

**Halt:** if ripples are substantive, **STOP** + file `onr-question-2026-05-XXTHH-MMZ-w76-invoice-imusthavetenant-retrofit.md`. Admiral may route a separate retrofit workstream (po-mac-scale similar to Lease) before PR 0a can land.

### H4. Repository-contract change has consumers outside the financial cluster

**Symptom:** Compiling PR 0a (or 0b / 0c / 0d) fails because something OUTSIDE `blocks-financial-*` calls the repository directly — a Bridge handler in `signal-bridge/Sunfish.Bridge/Cockpit/`, a desktop service in `sunfish/apps/desktop/`, a Wayfinder catalog consumer, etc.

**Mitigation:** Repository contracts are MEANT to be internal-cluster. Cross-cluster consumers are a smell — they bypass the service layer that should already be tenant-aware. Update such consumers to pass tenant through.

**Halt:** if a cross-cluster consumer has substantive logic that depends on the old contract shape, **STOP** + file `onr-question-2026-05-XXTHH-MMZ-w76-pr0X-cross-cluster-consumer.md`. ONR investigates whether the consumer needs a separate facade or whether scope expansion is needed.

### H5. PR 0 cluster blast-radius too large to land cleanly in one cohort

**Symptom:** PR 0a's blast radius (LOC across all consumers) is significantly larger than estimated — Engineer's audit finds 30+ call sites of `IInvoiceRepository.GetAsync(InvoiceId)` and threading all of them takes ≥2× the estimate.

**Mitigation:** Split is acceptable — propose to Admiral a cohort-2 partial-scope: ship PR 0a + 0c (the two highest-risk surfaces) in cohort-2; defer PR 0b + 0d to a follow-on workstream `W#7X-financial-tenant-keying-completion`.

**Halt:** if Engineer estimates total PR 0 cluster effort >12h (vs ~6h estimated), **STOP** + file `engineer-question-2026-05-XXTHH-MMZ-w76-pr0-blast-radius.md` proposing the split. Admiral rules on cohort-2 partial-scope vs full-scope.

### H6. CSRF UX needs deeper research than the cohort-1 PR 3 precedent

**Symptom:** PR 3 frontend implementation (§3.25) hits a UX edge case the cohort-1 pattern doesn't cover — e.g., token rotation mid-session forcing a re-fetch, or the antiforgery middleware rejects the token under a specific browser cookie configuration.

**Mitigation:** Cohort-1 PR 3 shipped successfully with the documented round-trip pattern. If PR 3 hits a novel case, the precedent should be revisited.

**Halt:** **STOP** + file `pao-question-*` to CIC requesting ONR routing for a small CSRF UX research pass. ONR investigates and emits a follow-up status with the recommended UX shape.

### H7. `IDomainEventPublisher` is only the noop at Bridge level

**Symptom:** Pre-build §2.9 reveals the Bridge composition root only registers `NoopDomainEventPublisher`. PR 3's audit emission would silently no-op.

**Mitigation:** Either (a) PR 3 includes the Bridge-level wiring of a real publisher (with a small composition-root edit + the new integration test), OR (b) a separate prerequisite PR ships the publisher wiring before PR 3.

**Halt:** if the publisher wiring requires substantive Bridge-side work (e.g., wiring a real message queue), **STOP** + file `engineer-question-*` to Admiral. Option (a) is cheaper if the wiring is mechanical; option (b) is cleaner if it's not.

### H8. ERPNext deprecation breaks a non-cohort-2 page

**Symptom:** Same shape as cohort-1 H8 — adding `@deprecated` to `getAccountingSummary()` / `getAccountingOutstanding()` / `getPayments()` / `recordPayment()` triggers a compile error or audit-check failure because a non-cohort-2 page (or test helper, story, feature-flag config) imports these.

**Mitigation:** Cohort-1 H8 mitigation applies — verify TypeScript treats `@deprecated` as a warning not an error; audit non-cohort-2 consumers; update or annotate as part of PR 4.

**Halt:** if a non-cohort-2 consumer is found with a legitimate use, **STOP** + file `engineer-question-*`. Admiral rules on whether to defer the deprecation or rebind the consumer.

### H9. `IPaymentService.RecordAsync` does not exist on the existing service surface

**Symptom:** PR 3 §3.22 calls `payments.RecordAsync(...)` but `IPaymentService` doesn't have this method on main.

**Mitigation:** Cohort-1 H6 shape — small `blocks-financial-payments` cluster surface gap. Two options:
1. Add the method inline in PR 3 (one-line interface + impl extension).
2. File a tiny prerequisite PR to `blocks-financial-payments` that ships `RecordAsync` before PR 3.

ONR recommends option 1 if mechanical (new ULID, set initial status to "Received", persist via repository); option 2 if `RecordAsync` needs substantive logic (event emission, validators, ledger posting hookup).

**Halt:** if PR 3's create logic surfaces substantive blocks-financial-payments work beyond pure persistence, **STOP** + file `engineer-question-*`. Likely a small W#68 follow-on.

### H10. Playwright CDP infrastructure regression

**Symptom:** Same shape as cohort-1 H7 — the CDP harness isn't running. Cohort-2 inherits cohort-1's smoke test infrastructure; if cohort-1 didn't actually wire it, cohort-2 can't extend it.

**Mitigation:** Same as cohort-1 H7 — manual-test checklist substitute; file follow-on workstream.

**Halt:** not a hard halt; ship PR 4 with a manual-test checklist substitute if needed.

---

## 10. PASS gate (end-state for declaring W#76 `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 0a + 0b + 0c + 0d + 1 + 2 + 3 + 4 merged to `main`** in sequence (or per Engineer-elected parallelization).
2. **All Bridge endpoint + block tests pass** (A1).
3. **Frontend tests pass** (A2 + A3).
4. **E2E smoke test passes** for the three new cohort-2 scenarios OR (per H10) a manual-test checklist is published with PR 4 and CIC accepts the manual-test substitute.
5. **Network-panel verification:** each of the three financial pages renders against a Bridge with ERPNext stopped, with NO `/api/v1/erpnext/*` financial calls.
6. **ERPNext passthrough still works** at the Bridge level — Cohort 4 deletes; Cohort 2 only deprecates.
7. **`sunfish/apps/docs/anchor/anchor-react-rebind.md` cohort-2 section published** + linked in the docs TOC.
8. **Workstream W#76 ledger row reads `built`** with PR refs (8 entries).
9. **`coordination/inbox/engineer-status-2026-05-XXTHH-MMZ-w76-cohort-2-built.md`** beacon dropped.
10. **No outstanding `@deviation-from-spec:` flags** without CIC acknowledgement.
11. **`pattern-009-tenant-keying-retrofit` catalog candidacy** — if Engineer observed four clean shippings on PR 0a..0d, the W#76 status beacon includes a section proposing pattern ratification at CIC's next standing-patterns review. Admiral promotes via separate beacon if ratified.
12. **`pattern-010-financial-write-path` forward-candidate** — PR 3 ships under the candidate claim; a future financial-write-path PR (refund, void) is the qualifying second instance for ratification.

When the PASS gate is met, the next cohort hand-off can proceed:

- `anchor-react-rebind-cohort-3-stage06-handoff.md` — PLReport + RentRoll (reports cluster; mostly client-file rename per FED survey §6 — lighter than cohort-2).

---

## 11. Companion artifacts

PAO Track C (cohort-2 design direction; parallel beacon `admiral-directive-2026-05-19T00-35Z-onr-pao-cohort2-handoff-authoring-and-design-direction.md`) ships under `shipyard/_shared/design/cohort-2/`. FED's execution of PR 1/2/3 consumes:
- This hand-off (Track B output — engineering contract).
- PAO Track C output (UX direction + ASCII mockups + design-token specs — design contract).

Both must land before FED begins PR 1 execution (post-W#68 PR 1 merge).

---

**End of hand-off.**

— ONR, 2026-05-19T00-00Z
