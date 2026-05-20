---
title: Anchor React Bridge Rebind Cohort 3 — Stage 06 Hand-off
workstream: TBD (ONR recommends W#77 — register in `active-workstreams.md` before kickoff; verify W#77 slot empty via `ls shipyard/icm/_state/workstreams/W77-*` per `feedback_never_add_workstream_rows_directly_to_ledger`)
cluster: anchor-react-rebind (cross-package: `signal-bridge/Sunfish.Bridge/Reports/` + `sunfish/apps/web/src/api/reports.ts` + `sunfish/apps/web/src/pages/reports/` + `shipyard/packages/blocks-reports/` consumer)
pipeline: sunfish-feature-change
authored-by: ONR
authored-at: 2026-05-20T11-45Z
status: ready-to-author (executable after Engineer ships the cartridge-runner Bridge endpoint family — see §"Engineer prereq PR 0" below; authoring is unblocked NOW)
co-pre-authorized: requested
co-pre-authorized-rationale: |
  Cohort 3 is the third cohort under the Tauri-first pivot. The 5 frontend PRs are
  mechanical mirrors of pattern-009 (formal post-cohort-1) for the cluster-endpoint
  rebind shape — Engineer ships one POST endpoint family per cartridge; FED rebinds
  one page per cartridge. No write-path; no CSRF; no audit-emission surface. The
  one novel surface element is the read-via-POST pattern (cartridge parameter
  envelopes carry typed lists + opaque IDs + DateOnly fields, not URL-safe;
  forced-POST-for-read carve-out per `pattern-009 §B1`). Sec-eng SPOT-CHECK
  MANDATORY on PR 1 (api layer; first instance of read-via-POST + cartridge
  parameter validation shape) to confirm the carve-out applies; subsequent PRs
  are pattern-009 mechanical mirrors.
co-pre-authorized-scope:
  - PR 1 (api/reports.ts shared layer — TypeScript analogues of 4 cartridge parameter + result types) — sec-eng SPOT-CHECK MANDATORY (read-via-POST carve-out confirmation; cartridge parameter validation surface)
  - PR 2 (RentRoll v2 rewrite — ERPNext → cartridge) — pre-auth; pattern-009 formal
  - PR 3 (PLReport → ProfitAndLossByPropertyPage rewrite — ERPNext → cartridge) — pre-auth; pattern-009 formal
  - PR 4 (TrialBalancePage new — first cohort-3 net-new page) — pre-auth; pattern-009 formal
  - PR 5 (ArAgingPage new + optional DaysDuePill extraction) — pre-auth; pattern-009 formal; CIC sees this PR regardless (ledger-flip PR per pre-auth ruling §Step 4 — combines the last consumer rebind with the workstream close-out)
  - PR-count maximum: 5 FED PRs (workstream re-evaluation if scope grows; AP Aging deferred to cohort-4 — separate workstream)
  - PR-deviation flag triggers immediate CIC escalation for that PR
merge-tier: pre-authorized-pending-CIC-ratification
depends-on:
  - W#72 `blocks-reports` Phase 1 MVP cartridges on main (HARD GATE on FED PR execution; authoring is independent) — TrialBalance / ArAgingSummary / ProfitAndLossByProperty / RentRoll all shipped per FED scope survey 2026-05-19T12:00Z
  - Engineer prereq PR 0 — Bridge cartridge-runner endpoint family at `signal-bridge/Sunfish.Bridge/Reports/` replacing existing ERPNext-backed `ReportsEndpoints.cs` — see §"Engineer prereq PR 0" below for spec; this hand-off DOES NOT author Engineer's PR; it documents the contract FED depends on
  - Cohort-1 W#74 — `AuthenticatedTenantPolicy` precedent (registered in cohort-1 PR 1; cohort-3 reports endpoints reuse the same policy)
  - Cohort-2 W#76 — pattern-009 formal precedent (cohort-2 shipped 5 pattern-009 PRs); cohort-3 inherits the pattern
  - ADR 0091 Rev 2 (Accepted 2026-05-19T02:40Z per `admiral-status-2026-05-19T02-40Z-adr-0091-promoted-to-accepted.md`) — Bridge endpoints derive `TenantId` server-side via `ITenantContext`; cohort-3 endpoints honor the same contract
  - PAO Track C cohort-3 design direction at `shipyard/_shared/design/cohort-3/` — NOT YET AUTHORED; required before FED PR 2-5 reach Ready (see Halt H1)
  - FED cohort-3 scope survey (`fed-status-2026-05-19T1200Z-cohort-3-scope-survey.md`) — primary scope input
  - Admiral research queue dispatch (`admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md`) — this hand-off's parent directive (item #1)
spec-source: |
  - `coordination/inbox/fed-status-2026-05-19T1200Z-cohort-3-scope-survey.md` (PRIMARY — cartridge inventory, page mapping, Bridge endpoint contract, PR cluster shape, halt conditions)
  - `coordination/inbox/admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` (this hand-off's parent; queue item #1)
  - `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-2-stage06-handoff.md` (W#76; ONR's own prior work; structural template — cohort-3 mirrors §1-§11)
  - `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-1-stage06-handoff.md` (W#74; foundational template — auth policy + AuthenticatedTenantPolicy precedent)
  - `shipyard/packages/blocks-reports/` — `Cartridges/{TrialBalance,ArAgingSummary,ProfitAndLossByProperty,RentRoll}/` — substrate consumer
  - `shipyard/packages/blocks-reports/Services/IReportRunner.cs` + `IReportCartridge.cs` — runner contract
  - `shipyard/_shared/engineering/standing-approved-patterns.md` (pattern-009 — formal post-cohort-1; pattern-011-cartridge-read-via-post — proposed candidate, this hand-off PR 1 is the qualifying instance)
  - `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (Accepted; Bridge endpoint tenant-isolation contract)
estimated-effort: ~6–9h dev across 5 FED PRs (~1h PR 1 api layer, ~1.5h PR 2 RentRoll rewrite, ~1.5h PR 3 PL rewrite, ~1h PR 4 TrialBalance new, ~1.5h PR 5 ArAging new incl. ledger flip)
PR-count: 5 (FED only; Engineer prereq PR 0 is a separate workstream-attached PR)
pre-merge-council:
  security-engineering: SPOT-CHECK MANDATORY on PR 1 (read-via-POST carve-out + cartridge parameter validation surface — first instance establishes the pattern). NOT required on PR 2 / PR 3 / PR 4 / PR 5 (mechanical mirrors of cohort-2 PR 1/PR 2 pattern under pattern-009-formal).
  dotnet-architect: NOT required (Engineer prereq PR 0 carries any .NET-architect review; this hand-off is FED-scope only)
  frontend-reviewer: NOT required (mechanical fetcher swap inside existing TanStack hooks + 2 new pages + 2 page rewrites; no new state-management or UI primitives — `DaysDuePill` is the one shared component candidate, extracted in PR 5 if cohort-2 has merged it elsewhere already)
license-posture: MIT clean-room (all frontend code is original; cohort-3 reads from `blocks-reports` cartridge surface + Bridge endpoints; no FOSS source studied for this rebind)
---

# Hand-off — Anchor React Bridge Rebind Cohort 3 (Reports cluster: Trial Balance + AR Aging + P&L by Property + Rent Roll v2)

**From:** ONR (Office of Naval Research; research session)
**To:** Engineer (sunfish technical corps — for prereq PR 0 ONLY) and FED (sunfish technical corps frontend — for PR 1-5)
**Workstream:** TBD — ONR recommends **W#77 Anchor React Rebind Cohort 3**; register the source `W77-anchor-react-rebind-cohort-3.md` row in `shipyard/icm/_state/workstreams/` and re-render the ledger before kickoff (per `feedback_never_add_workstream_rows_directly_to_ledger`). W#77 slot confirmed empty 2026-05-20 by ONR pre-flight check.
**Pipeline:** `sunfish-feature-change`
**Ratifications applied:**
- CIC 2026-05-17T14-30Z Tauri-first pivot ratification (cohort-1 + cohort-2 inherited).
- CIC ratified roadmap Q1: **tenant scoping is server-derived from `ITenantContext`**, NOT a frontend query param. Cohort-3 inherits — cartridge parameter envelopes carry `ChartId`, optional `PropertyIds[]` / `CustomerIds[]`, `AsOfDate` etc., but NEVER a tenant filter (server resolves).
- CIC ratified roadmap Q2: **ERPNext passthrough kept through Cohort 4**; cohort-3 marks the two ERPNext-backed report functions (`getRentRoll`, `getProfitLoss`, `exportProfitLoss`) `@deprecated` in PR 5 (ledger-flip PR).
- CIC ratified roadmap Q3: **per-page PR bundling** (one PR per cartridge consumer page; PR 1 is the shared api layer that all 4 consumer PRs depend on).
- Admiral research-queue dispatch 2026-05-19T22-50Z (this hand-off authoring; queue item #1).
- ADR 0091 Rev 2 (Accepted 2026-05-19T02-40Z) — Bridge endpoint tenant-isolation contract inherited.

---

## 1. Context

### 1.1 Why Cohort 3 ships now

The Phase 1 reports cluster has shipped: `blocks-reports` (W#72 MVP) with 4 cartridges (TrialBalance / ArAgingSummary / ProfitAndLossByProperty / RentRoll) live on main. `IReportRunner` runs cartridges by `ReportKind`; each cartridge produces a strongly-typed `ReportRunResult<T>` envelope with provisionality + warnings.

What remains on the frontend: `sunfish/apps/web/src/api/erpnext.ts` is the data plane for both currently-shipping report pages (`RentRoll.tsx` + `PLReport.tsx`); two routes don't exist yet at all (`TrialBalancePage` + `ArAgingPage`).

Cohort 3 is the **reports cluster rebind + new-page introduction**:
- Two ERPNext-backed pages get rewritten to consume cartridge endpoints (RentRoll v2; ProfitAndLossByPropertyPage replaces PLReport).
- Two new pages introduce cartridges not previously surfaced (TrialBalancePage; ArAgingPage).
- One shared api layer (`apps/web/src/api/reports.ts`) carries TypeScript analogues of the 4 cartridge parameter + result types.

Cohort 3 also introduces the **read-via-POST pattern** (cartridge parameters carry typed lists + opaque IDs + DateOnly fields, not URL-safe). Sec-eng SPOT-CHECK on PR 1 confirms the read-POST carve-out per `pattern-009 §B1` (or files a clarifying ruling if the catalog doesn't yet name it).

### 1.2 What Cohort 3 ships

| # | PR | Subject |
|---|---|---|
| 0 (Engineer; prereq) | **Engineer prereq PR 0** | Bridge cartridge-runner endpoint family at `signal-bridge/Sunfish.Bridge/Reports/` (4× `POST /api/v1/reports/{kind}`); REPLACES existing ERPNext-backed `ReportsEndpoints.cs`. Detailed contract in §"Engineer prereq PR 0" below. |
| 1 | **FED PR 1** | `sunfish/apps/web/src/api/reports.ts` — shared TypeScript api layer (4 typed fetch functions + `ReportRunResult<T>` envelope + all 4 cartridge parameter + result type interfaces) |
| 2 | **FED PR 2** | `RentRollPage.tsx` v2 rewrite — ERPNext → cartridge; ExpiringWindowDays filter, DelinquencyBucket coloring, OccupancyStatus badges, IncludeVacant toggle, portfolio summary row, provisionality banner |
| 3 | **FED PR 3** | `PLReport.tsx` → `ProfitAndLossByPropertyPage.tsx` rewrite — per-property accordion, portfolio totals tile, Export CSV, provisionality banner |
| 4 | **FED PR 4** | `TrialBalancePage.tsx` NEW + route `/reports/trial-balance` — IsBalanced badge, period selector, zero-balance toggle, provisionality banner, Export CSV |
| 5 | **FED PR 5** | `ArAgingPage.tsx` NEW + route `/reports/ar-aging`; ByCustomer/ByProperty tabs, TopDelinquent section; OPTIONAL `DaysDuePill` extraction into shared component; **ledger-flip + ERPNext @deprecated marks + Playwright CDP E2E smoke extension + docs running log update** (combined close-out per cohort-1/cohort-2 PR 4 precedent — cohort-3 has one fewer rebind PR so the close-out fits into the last consumer PR) |

After Cohort 3 lands, four report pages render against cartridges (no ERPNext at runtime); `RentRoll.tsx` + `PLReport.tsx` are retired; `TrialBalancePage` + `ArAgingPage` are new surfaces.

### 1.3 What Cohort 3 does NOT ship

- **AP Aging page (`/reports/ap-aging`)** — DEFERRED to cohort-4. `Cartridges/ApAgingSummary/` does not exist in `blocks-reports`; `ReportKind.ApAgingSummary` is a reserved enum stub only. Cohort-3 leaves a `// TODO(W#77+): AP Aging page — blocked on ApAgingSummaryCartridge shipping` comment in the nav (reserved route slot, no component). Cohort-4 picks up after Engineer ships the cartridge.
- **PDF export** — listed as reserved in `ReportKind` enum comments. Cohort-3 ships CSV export only.
- **Cross-cohort component extraction beyond `DaysDuePill`** — if more shared components emerge (e.g., a generic provisionality banner), extract in a follow-on cleanup PR, not as cohort-3 scope creep.
- **Persistence-layer transactional fixes** — out of cohort-3 scope (reports are read-only; no persistence write path).
- **Multi-device CRDT sync** — out of scope per roadmap §1 (cohort-1 + cohort-2 inherited).
- **ERPNext route deletion** — Cohort 4 (RB-12). Cohort-3 marks `@deprecated` only.
- **Cohort-2 substrate work (tenant-keyed repository contracts)** — cohort-2's PR 0 cluster shipped tenant-keying on the financial blocks; cohort-3 does NOT extend to `blocks-reports` substrate (reports cartridges are read-only and consume `TenantId` explicitly via `IReportRunner.RunAsync` — no repository contract change needed).

### 1.4 Auth, CSRF, audit conventions (recap + cohort-3 specifics)

- **Auth:** `AuthenticatedTenantPolicy` (registered in cohort-1 PR 1; reused by cohort-2; cohort-3 reuses again — do NOT re-introduce).
- **Tenant scoping:** server-derived from `ITenantContext.TenantId`; cartridge parameter envelopes do NOT carry a tenant filter. `IReportRunner.RunAsync(reportKind, tenantId, principalId, parameters, ct)` takes `tenantId` + `principalId` as explicit positional arguments — Engineer's prereq PR 0 extracts both from the authenticated session.
- **CSRF:** NOT REQUIRED for cohort-3. The cartridge-runner endpoints are POSTs, but they are **read-only POST** (parameter shape mandates POST; no mutation occurs). Per pattern-009 §B1 read-POST carve-out (or a clarifying ruling if the catalog does not yet name it), these endpoints don't require `IAntiforgery.ValidateRequestAsync`. **Sec-eng SPOT-CHECK on PR 1 confirms this disposition.**
- **Audit:** NOT REQUIRED for cohort-3. Reports are read-only; no audit event emission on each report run (existing `IReportRunner.RunAsync` may already record run telemetry inside the runner — that's substrate-side and out of cohort-3 scope).
- **Standing pattern:** `pattern-009` (formal — ratified after cohort-1, applied through cohort-2). All cohort-3 frontend PRs ship under `@standing-pattern: pattern-009`. The new read-via-POST shape on PR 1 is proposed as `@candidate-pattern: pattern-011-cartridge-read-via-post` (qualifying-instance; ratification pending a second cartridge-cluster usage — likely cohort-4 ApAgingSummary).

### 1.5 Why CIC sees PR 5 regardless of pre-auth

PR 5 is the **ledger-flip PR** for W#77 (combined with the last consumer page rebind — cohort-3 has 5 PRs vs cohort-1/cohort-2's 4-page-then-close-out shape). Per the pre-authorization ruling §Step 4:

> Even under pre-authorization, CIC is brought into the loop if ANY of: **Ledger-flip PR** (final PR of the workstream — CIC always sees workstream completions) [...]

PR 5 also extends the cohort-1/cohort-2 Playwright CDP smoke test to cover the 4 new report pages; CIC sees the cohort-3 close-out hits the smoke gate.

---

## 2. Pre-build checklist (FED executes before opening PR 1)

Run each step; halt on any unexpected state.

### 2.1 Confirm W#72 cartridges on main

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Cartridges/
# expected: ArAgingSummary, ProfitAndLossByProperty, RentRoll, TrialBalance (4 dirs)
# verify: no ApAgingSummary (deferred to cohort-4)
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Services/IReportRunner.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Services/IReportCartridge.cs
```

Expected: all 4 cartridge dirs present; runner + cartridge interfaces exist. If any missing, **STOP** — file `engineer-question-*` naming the missing dependency.

### 2.2 Confirm Engineer prereq PR 0 status

Engineer's prereq PR 0 ships the Bridge cartridge-runner endpoint family. This hand-off documents the contract FED depends on (§"Engineer prereq PR 0" below) but does NOT author Engineer's PR.

```bash
gh pr list --state open --repo Harborline-Software/shipyard --search "ReportsEndpoints OR cartridge-runner OR reports/trial-balance in:title,body"
# expected: Engineer's PR 0 listed; if not yet opened, FED authoring is blocked at execution start
```

If Engineer hasn't yet opened PR 0, FED can still start PR 1 (api layer) authoring against the **contract spec** in §"Engineer prereq PR 0" below — but PR 1 cannot MERGE until Engineer PR 0 has merged (CI integration tests on PR 1 will need real cartridge endpoints to exercise).

### 2.3 Confirm cohort-1 + cohort-2 precedents are on main

```bash
grep -rn "AuthenticatedTenantPolicy\|AuthenticatedTenantPolicyName" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Authorization/ 2>/dev/null
grep -rn "MapPropertiesEndpoints\|MapLeasesEndpoints\|MapPaymentsEndpoints\|MapAccountingEndpoints" /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Program.cs 2>/dev/null
```

Expected: cohort-1 properties + leases endpoints registered; cohort-2 payments + accounting endpoints registered. Engineer PR 0 adds `MapReportsEndpoints` next to these.

### 2.4 Confirm no parallel-session PRs touch the same surface

```bash
gh -R Harborline-Software/sunfish pr list --state open --search "RentRoll OR PLReport OR TrialBalance OR ArAging OR cohort-3 in:title,body"
gh -R Harborline-Software/signal-bridge pr list --state open --search "Reports OR cartridge in:title,body"
gh -R Harborline-Software/shipyard pr list --state open --search "blocks-reports OR cohort-3 in:title,body"
```

Expected: empty (or only this hand-off's PRs + Engineer prereq PR 0). If anything else is open and would conflict, file `engineer-question-*`.

### 2.5 Confirm `gt status` / `git status` is clean

Current branch should be `main` (or a fresh worktree from `main` per fleet-conventions §"Git worktree location" — all worktrees under `<repo>/.worktrees/<branch>/`; NEVER under `/tmp/` or `/var/folders/`).

### 2.6 Confirm the workstream row exists

```bash
grep -n "W#77\|anchor-react-rebind-cohort-3" /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/active-workstreams.md
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/workstreams/W77-* 2>&1
```

Expected: workstream registered as `ready-to-build`. If absent, **STOP** — Admiral must register the workstream first (the hand-off file referencing W#77 is not enough on its own; the source `W77-*.md` file plus render-ledger must run).

### 2.7 Confirm the pre-authorization frontmatter status

The hand-off ships with `co-pre-authorized: requested`. CIC ratifies (or declines) at hand-off review. **FED must NOT open PRs under the pre-authorization shortcut until the frontmatter says `co-pre-authorized: granted`.** If `declined`, fall back to the per-PR-CIC-click model (still ship the work — just don't arm auto-merge without CIC click).

### 2.8 Confirm PAO Track C cohort-3 design direction lands

```bash
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/_shared/design/cohort-3/ 2>&1
# expected: directory exists with: provisionality-banner.md, trial-balance-page.md, ar-aging-page.md, profit-and-loss-by-property-page.md, rent-roll-v2-page.md, export-csv-hook.md (or similar PAO breakdown)
```

If `shipyard/_shared/design/cohort-3/` does NOT exist when FED reaches PR 2 (first consumer page rebind), **STOP** + see Halt H1. PR 1 (api layer) can ship without PAO direction (api layer has no design surface); PR 2-5 require design specs.

### 2.9 Confirm `IReportRunner` + cartridge contract on the worktree

```bash
grep -rn "interface IReportRunner\|RunAsync" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Services/IReportRunner.cs
grep -rn "TenantId\|PrincipalId\|parameters" /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-reports/Services/IReportRunner.cs
```

Expected: `RunAsync(ReportKind, TenantId, PrincipalId, parameters, ct)` shape. Engineer's PR 0 calls this directly inside each `POST /api/v1/reports/{kind}` handler.

### 2.10 Read the supporting docs once

Skim:
- `coordination/inbox/fed-status-2026-05-19T1200Z-cohort-3-scope-survey.md` (PRIMARY scope input — cartridge mapping, page mapping, halt conditions)
- `coordination/inbox/admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` (parent directive context)
- `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-2-stage06-handoff.md` (structural template — §1-§11; particularly §3 per-PR shape for pattern-009 cluster-endpoint rebind)
- `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-1-stage06-handoff.md` (auth policy precedent; PR 4 close-out pattern)
- `shipyard/packages/blocks-reports/Cartridges/{TrialBalance,ArAgingSummary,ProfitAndLossByProperty,RentRoll}/` (parameter + result type definitions)
- `sunfish/apps/web/src/api/cockpit.ts` + `src/api/leases.ts` + `src/api/financial.ts` (frontend client templates from cohort-1 / cohort-2)
- `sunfish/apps/web/src/pages/RentRoll.tsx` + `PLReport.tsx` (existing pages being rewritten)
- `shipyard/_shared/engineering/standing-approved-patterns.md` (pattern-009 + read-POST carve-out section if present; sec-eng SPOT-CHECK on PR 1 confirms or amends)
- `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (Bridge endpoint tenant-isolation contract; reports endpoints honor same shape)

---

## 3. Engineer prereq PR 0 — Bridge cartridge-runner endpoint family

**This PR is NOT in cohort-3's FED scope.** It is documented here as the contract FED depends on. Engineer's directive routing is separate; this hand-off section is the spec FED uses to type-check the api layer at PR 1.

**Estimated effort (Engineer):** ~2-3h
**Branch (Engineer's call):** suggested `feat/cohort-3-reports-bridge-endpoints` or similar
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — same shape as cohort-2 Engineer PRs (cross-tenant surface; tenant-keyed `IReportRunner.RunAsync` call shape; read-via-POST first instance in fleet).

### 3.0.1 Bridge endpoint family — `/api/v1/reports`

REPLACE the existing ERPNext-backed `signal-bridge/Sunfish.Bridge/Reports/ReportsEndpoints.cs` with a new cartridge-runner-backed family. Pattern lifted from cohort-1 `LeasesEndpoints.cs` / cohort-2 `PaymentsEndpoints.cs`:

1. **Route prefix:** `/api/v1/reports` (existing; replace the 3 ERPNext-backed routes inside).
2. **Authorization:** `AuthenticatedTenantPolicy` (REUSE from cohort-1).
3. **Endpoints (4 net-new replacing 3 ERPNext-backed):**

| Route | Cartridge | ReportKind enum |
|---|---|---|
| `POST /api/v1/reports/trial-balance` | TrialBalance | `ReportKind.TrialBalance` |
| `POST /api/v1/reports/ar-aging-summary` | ArAgingSummary | `ReportKind.ArAgingSummary` |
| `POST /api/v1/reports/profit-and-loss-by-property` | ProfitAndLossByProperty | `ReportKind.ProfitAndLossByProperty` |
| `POST /api/v1/reports/rent-roll` | RentRoll | `ReportKind.RentRoll` |

**Note on POST-for-read:** Cartridge parameter envelopes carry typed lists (`CustomerIds[]`, `PropertyIds[]`, `PropertyAuthorityKeys[]`), opaque IDs (`ChartId`), and `DateOnly` fields — not URL-safe. POST body is the right call. **This is read-only POST** (no mutation; `IReportRunner.RunAsync` produces a result; nothing persists). `pattern-009 §B1` should name this carve-out; if it does not, Engineer's PR 0 + ONR file a follow-on amendment proposing the catalog addition.

4. **Handler signature (uniform shape across all 4):**

```csharp
internal static async Task<Results<Ok<ReportRunResult<TResult>>, BadRequest<ProblemDetails>, UnauthorizedHttpResult>>
  HandleRun<TParams, TResult>Async(
      TParams parameters,
      ITenantContext tenantContext,
      IReportRunner runner,
      CancellationToken ct)
  where TParams : IReportParameters
  where TResult : IReportResult
```

(Engineer may choose to write 4 separate non-generic handlers for handler-discovery + Swagger reasons; the spec is logical, not literal.)

Flow:
1. Validate parameters per cartridge schema (cartridge has its own validation surface inside; handler does minimal pre-flight — required-field presence, basic shape).
2. Resolve `TenantId` from `ITenantContext.TenantId` + `PrincipalId` from `ITenantContext.UserId` (or whatever the principal-resolution accessor is post-ADR-0091).
3. Call `runner.RunAsync(reportKind, tenantContext.TenantId, tenantContext.UserId, parameters, ct)`.
4. Return `Ok(result)` with the `ReportRunResult<TResult>` envelope. Envelope carries `IsProvisional`, `Warnings`, `RunAtUtc`, `SnapshotMarker`, `RunDuration`.

5. **Registration:** add `MapReportsEndpoints` extension; wire in `Program.cs` next to `MapAccountingEndpoints()` from cohort-2.

6. **Deprecated routes removed in the same PR:** the existing GET `/api/v1/reports/rent-roll`, GET `/api/v1/reports/profit-loss`, GET `/api/v1/reports/profit-loss/export` are ERPNext-backed (W#60 P5 thin-slice); Engineer's PR 0 REMOVES them. Frontend PR 5 ladders the `@deprecated` mark on the `erpnext.ts` exports that USED to call those routes (the routes themselves are gone post-PR-0; the JSDoc deprecation is a courtesy to any non-cohort-3 caller).

### 3.0.2 Engineer-side tests (≥10 new)

- 4× `RunReport_<Kind>_AuthenticatedTenant_ReturnsResult`
- 4× `RunReport_<Kind>_Unauthenticated_Returns401`
- 1× `RunReport_TrialBalance_CrossTenantChartId_ReturnsBadRequest` (cartridge validation surface — chart belongs to another tenant)
- 1× `RunReport_RentRoll_PropertyIdsCrossTenant_ReturnsBadRequest` (cartridge validation — property list contains other-tenant items)
- 1× `RunReport_ArAgingSummary_IsProvisional_FlaggedWhenCrossesOpenPeriod`

Engineer's PR 0 may add more per cartridge shape — these are minimum.

### 3.0.3 Pattern conformance (Engineer)

```
@standing-pattern: pattern-009 (cluster-endpoint rebind — formal post-cohort-1)
@candidate-pattern: pattern-011-cartridge-read-via-post (FIRST INSTANCE — qualifying-candidate; ratify after a second cartridge cluster ships through the same shape, likely cohort-4 ApAgingSummary)
```

---

## 4. Per-PR deliverables (FED)

### PR 1 — `apps/web/src/api/reports.ts` shared api layer

**Estimated effort:** ~1h
**Scope:** new `sunfish/apps/web/src/api/reports.ts` with TypeScript analogues of the 4 cartridge parameter + result types + `ReportRunResult<T>` envelope + 4 typed fetch functions; reuse `AuthenticatedTenantPolicy` (frontend-side, this means `credentials: 'include'` on every request — same cohort-1 pattern)
**Commit subject:** `feat(anchor-react): cohort-3 PR 1 reports api layer (TypeScript)`
**Branch:** `engineer/cohort-3-pr-1-reports-api` (Engineer or FED owns; FED preferred — frontend-only)
**Depends on:** Engineer prereq PR 0 contract-frozen (Engineer files `engineer-status-*-cohort-3-pr-0-contract-frozen.md` when types stabilize; FED can start authoring against the spec earlier but cannot MERGE PR 1 until Engineer PR 0 has merged)
**Pre-merge council:** **security-engineering SPOT-CHECK MANDATORY** — first cohort-3 instance establishes read-via-POST + cartridge-parameter-validation surface posture.

#### 4.1 TypeScript types (analogues of C# cartridge records)

New file `sunfish/apps/web/src/api/reports.ts`. Structure:

```typescript
// Envelope
export interface ReportRunResult<TResult> {
  kind: string;                    // ReportKind enum as kebab-case string
  result: TResult;
  runAtUtc: string;                // ISO timestamp
  snapshotMarker: string;          // opaque cartridge-specific marker
  runDuration: string;             // ISO 8601 duration (e.g., "PT0.123S")
  isProvisional: boolean;          // true if crossed Open/SoftClosed period
  warnings: string[];
}

// TrialBalance
export interface TrialBalanceParameters {
  chartId: string;                 // opaque ULID-like; FED treats as opaque string
  fiscalPeriodId?: string;
  asOfDate?: string;               // ISO date
  includeZeroBalanceAccounts: boolean;
  includeInactiveAccounts: boolean;
}

export interface TrialBalanceResult {
  rows: Array<{
    accountCode: string;
    accountName: string;
    accountType: string;           // AccountType enum string
    debit: number;
    credit: number;
  }>;
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
  isProvisional: boolean;
  warnings: string[];
  asOf: string;                    // ISO date
  chartId: string;
}

// ArAgingSummary
export interface ArAgingSummaryParameters {
  chartId: string;
  asOfDate?: string;
  customerIds?: string[];
  propertyIds?: string[];
  topDelinquentN?: number;         // default 10 server-side
}

export interface ArAgingBucket {
  current: number;
  days0to30: number;
  days31to60: number;
  days61to90: number;
  days90plus: number;
  total: number;
}

export interface ArAgingSummaryResult {
  byCustomer: Array<{ customerId: string; customerName: string; buckets: ArAgingBucket }>;
  byProperty: Array<{ propertyId: string; propertyName: string; buckets: ArAgingBucket }>;
  totals: ArAgingBucket;
  topDelinquent: Array<{ customerId: string; customerName: string; total: number; days90plus: number }>;
  asOf: string;
  chartId: string;
}

// ProfitAndLossByProperty
export interface ProfitAndLossByPropertyParameters {
  chartId: string;
  periodStart?: string;
  periodEnd?: string;
  propertyIds?: string[];
  includeZeroBalanceAccounts: boolean;
}

export interface PnlLine {
  accountCode: string;
  accountName: string;
  amount: number;
}

export interface ProfitAndLossByPropertyResult {
  byProperty: Array<{
    propertyKey: string;
    propertyName: string;
    totalRevenue: number;
    totalExpenses: number;
    netIncome: number;
    revenueLines: PnlLine[];
    expenseLines: PnlLine[];
  }>;
  totals: {
    totalRevenue: number;
    totalExpenses: number;
    netIncome: number;
  };
}

// RentRoll v2
export interface RentRollParameters {
  chartId: string;
  asOfDate?: string;
  propertyAuthorityKeys?: string[];
  expiringWindowDays: number;      // default 90 server-side
  includeVacant: boolean;          // default true server-side
}

export interface RentRollUnitRow {
  unitKey: string;
  unitLabel: string;
  occupancyStatus: 'Occupied' | 'Vacant' | 'NoticeGiven' | 'OffMarket';
  vacancyReason: string | null;
  tenantName: string | null;
  leaseStart: string | null;
  leaseEnd: string | null;
  expiringSoon: boolean;
  monthlyRent: number;
  openBalance: number;
  delinquencyBucket: 'Current' | '0-30' | '31-60' | '61-90' | '90+' | null;
  lastPaymentDate: string | null;    // always null in v1 — TODO cross-cluster note
  prepaidBalance: number;            // always 0 in v1 — TODO cross-cluster note
}

export interface RentRollResult {
  properties: Array<{
    propertyKey: string;
    propertyName: string;
    units: RentRollUnitRow[];
    summary: { occupancyRate: number; monthlyRentTotal: number; openBalanceTotal: number };
  }>;
  portfolio: { occupancyRate: number; monthlyRentTotal: number; openBalanceTotal: number };
}

// Fetch functions
export async function runTrialBalance(params: TrialBalanceParameters): Promise<ReportRunResult<TrialBalanceResult>> { /* ... */ }
export async function runArAgingSummary(params: ArAgingSummaryParameters): Promise<ReportRunResult<ArAgingSummaryResult>> { /* ... */ }
export async function runProfitAndLossByProperty(params: ProfitAndLossByPropertyParameters): Promise<ReportRunResult<ProfitAndLossByPropertyResult>> { /* ... */ }
export async function runRentRoll(params: RentRollParameters): Promise<ReportRunResult<RentRollResult>> { /* ... */ }
```

Each fetch function uses `credentials: 'include'`, POSTs JSON body, handles 4xx via `ProblemDetails` parsing, throws on non-2xx with diagnostic-safe error text.

#### 4.2 Tests (≥6 frontend)

- `reports.ts` unit tests using MSW: each of the 4 fetchers happy-path returns typed result; each handles a 400 ProblemDetails response with a typed error; one cross-fetcher consistency test (envelope `isProvisional` flag round-trips).

#### 4.3 Pattern conformance

```
@standing-pattern: pattern-009 (cluster-endpoint rebind pair — api-layer half)
@candidate-pattern: pattern-011-cartridge-read-via-post (frontend half of Engineer's PR 0 candidate; same pattern instance)
```

#### 4.4 Do NOT in this PR

- Do NOT touch any page component (`RentRoll.tsx`, `PLReport.tsx`, etc.) — PR 2+.
- Do NOT introduce hook abstractions (`useTrialBalance`, etc.) — those land in the page PRs alongside the consumer.
- Do NOT export anything from `erpnext.ts` related to reports — those are deprecated/removed in PR 5.

---

### PR 2 — RentRoll v2 rewrite (RB-10)

**Estimated effort:** ~1.5h
**Scope:** rewrite `sunfish/apps/web/src/pages/RentRoll.tsx` to consume `runRentRoll` from `@/api/reports`; new field surface (ExpiringSoon badge, DelinquencyBucket coloring, OccupancyStatus richer enum, VacancyReason, OpenBalance, portfolio summary row); add ExpiringWindowDays filter + IncludeVacant toggle; provisionality banner; CSV export hook
**Commit subject:** `feat(anchor-react): cohort-3 PR 2 RentRoll v2 rewrite (cartridge-backed)`
**Branch:** `fed/cohort-3-pr-2-rent-roll-v2`
**Depends on:** PR 1 merged (api layer); Engineer prereq PR 0 merged (cartridge endpoint reachable); PAO design 4e on cohort-2-track-C
**Pre-merge council:** NOT required — pattern-009 formal mirror of cohort-2's mechanical page rebinds.

#### 4.5 Page rewrite

Current `RentRoll.tsx` calls `getRentRoll()` from `@/api/erpnext` and renders a flat table (Property/Unit/Tenant/Lease Start-End/MonthlyRent/LastPayment/BalanceDue/Status).

Cohort-3 v2 rewrites to:
- `usePropertyAuthorityFilter()` (existing hook or new — verify) drives `propertyAuthorityKeys?` parameter
- ExpiringWindowDays slider control (default 90)
- IncludeVacant toggle (default true)
- Run button → calls `runRentRoll(params)`
- Provisionality banner (yellow inline below header) when `result.isProvisional`
- Property accordions with units inside; each unit row carries: OccupancyStatus badge, DelinquencyBucket colored pill (reuse pattern from cohort-2 AccountingPage `DaysDuePill` if extracted — see PR 5), ExpiringSoon badge, VacancyReason text when Vacant
- Portfolio summary row at bottom: OccupancyRate%, MonthlyRentTotal, OpenBalanceTotal
- Export CSV button (filename: `rent-roll-{asOf}.csv`)

#### 4.6 Tests

Update `RentRoll.test.tsx`; MSW mocks for the new cartridge endpoint; assert: provisionality banner appears when `isProvisional=true`; ExpiringSoon badges render correctly; DelinquencyBucket coloring matches the enum spec; ExpiringWindowDays + IncludeVacant filter changes refetch.

#### 4.7 Pattern conformance

```
@standing-pattern: pattern-009
```

---

### PR 3 — `PLReport.tsx` → `ProfitAndLossByPropertyPage.tsx` rewrite (RB-11)

**Estimated effort:** ~1.5h
**Scope:** delete `sunfish/apps/web/src/pages/PLReport.tsx`; create `sunfish/apps/web/src/pages/ProfitAndLossByPropertyPage.tsx` with per-property accordion + portfolio totals tile + Export CSV; route update; remove `getProfitLoss` + `exportProfitLoss` consumers (kept in `erpnext.ts` until PR 5 deprecation)
**Commit subject:** `feat(anchor-react): cohort-3 PR 3 ProfitAndLossByPropertyPage rewrite (cartridge-backed)`
**Branch:** `fed/cohort-3-pr-3-pnl-by-property`
**Depends on:** PR 1 merged; PAO design 4d
**Pre-merge council:** NOT required.

Same shape as PR 2:
- File rename / new page
- Period start + end date pickers
- Property multi-select filter
- Run button → calls `runProfitAndLossByProperty(params)`
- Provisionality banner
- Portfolio Totals tile at top (Revenue / Expenses / Net Income)
- Per-property accordion: each property shows TotalRevenue + TotalExpenses + NetIncome, with expandable RevenueLines + ExpenseLines tables
- Export CSV button

Tests parallel to PR 2.

```
@standing-pattern: pattern-009
```

---

### PR 4 — `TrialBalancePage.tsx` new (RB-12)

**Estimated effort:** ~1h
**Scope:** new `sunfish/apps/web/src/pages/TrialBalancePage.tsx` + route `/reports/trial-balance`; period selector OR as-of date picker (exactly one — FED enforces via radio + state); chart selector; IncludeZeroBalance toggle; IncludeInactive toggle; IsBalanced indicator badge; provisionality banner; Export CSV
**Commit subject:** `feat(anchor-react): cohort-3 PR 4 TrialBalancePage (new; cartridge-backed)`
**Branch:** `fed/cohort-3-pr-4-trial-balance`
**Depends on:** PR 1 merged; PAO design 4b
**Pre-merge council:** NOT required.

Page deliverable:
- New route `/reports/trial-balance` wired in `app.tsx`
- Nav link added (location per PAO design 4b)
- Chart selector (reuses cohort-2 chart-selector component if available; else simple `<select>` driven by tenant's charts)
- Mode toggle: "By Period" (`fiscalPeriodId` parameter) vs "As Of Date" (`asOfDate` parameter); exactly one active at a time
- IncludeZeroBalance + IncludeInactive toggles
- Run button → calls `runTrialBalance(params)`
- Provisionality banner when `isProvisional`
- Result table: AccountCode / AccountName / AccountType / Debit (right-aligned) / Credit (right-aligned); virtual-scroll if rows >100
- IsBalanced indicator: green pill "Balanced" if `isBalanced=true`; red pill "Out of balance by ${|totalDebit - totalCredit|}" otherwise
- Export CSV button

Tests parallel to PR 2 / PR 3.

```
@standing-pattern: pattern-009
```

---

### PR 5 — `ArAgingPage.tsx` new + cohort-3 close-out (RB-13)

**Estimated effort:** ~1.5h
**Scope:** new `sunfish/apps/web/src/pages/ArAgingPage.tsx` + route `/reports/ar-aging`; ByCustomer + ByProperty tabs; TopDelinquent section; OPTIONAL `DaysDuePill` extraction into shared component if cohort-2 hasn't already extracted it; **PLUS the cohort-3 close-out work** — mark `@deprecated` on `erpnext.ts` report exports; extend Playwright CDP E2E smoke (4 new page scenarios); append cohort-3 section to `apps/docs/anchor/anchor-react-rebind.md` running log; flip W#77 ledger row from `building` → `built`
**Commit subject:** `feat(anchor-react,docs): cohort-3 PR 5 ArAgingPage + close-out (cartridge-backed; W77 built)`
**Branch:** `fed/cohort-3-pr-5-ar-aging-and-closeout`
**Depends on:** PR 4 merged
**Pre-merge council:** NOT required (page is pattern-009 mirror; close-out is docs/cleanup). **CIC sees this PR regardless of pre-authorization** — ledger-flip PR per pre-auth ruling §Step 4.

#### 4.8 Page deliverable

- New route `/reports/ar-aging` wired in `app.tsx`
- Chart selector + AsOfDate picker
- Tab switcher: "By Customer" vs "By Property" (two tabs, one active at a time)
- ByCustomer tab: table with 5 aging buckets (Current/0-30/31-60/61-90/90+) + Total column; DaysDuePill colored column headers (reuse from cohort-2 AccountingPage; if cohort-2 hasn't extracted it as a shared component, extract `sunfish/apps/web/src/components/DaysDuePill.tsx` in this PR — see §"Optional DaysDuePill extraction" below)
- ByProperty tab: same shape but PropertyId/PropertyName instead of CustomerId/CustomerName
- TopDelinquent section: list (1..N) showing CustomerName + 90+ amount + Total, ranked
- Provisionality banner
- Export CSV button

#### 4.9 Optional DaysDuePill extraction

If cohort-2 PR 2 (`AccountingPage`) has merged with `DaysDuePill` defined inline inside `AccountingPage.tsx`, extract it to a shared component in PR 5:

```typescript
// sunfish/apps/web/src/components/DaysDuePill.tsx
export type DaysDueBucket = 'Current' | '0-30' | '31-60' | '61-90' | '90+';
export function DaysDuePill({ bucket }: { bucket: DaysDueBucket }) { /* ... color logic ... */ }
```

Then update `AccountingPage.tsx` (in this same PR; minor) to import from `@/components/DaysDuePill` instead of declaring inline. If cohort-2 hasn't merged at the time of PR 5 kickoff, FED has two options: (a) inline-duplicate the component in this PR (acceptable for v1; ~10 LOC); (b) hold PR 5 until cohort-2 merges + extract in a follow-on cleanup PR. **FED's call at PR 5 kickoff.**

#### 4.10 Close-out work (combined into this PR per cohort-3 5-PR shape)

##### 4.10.1 ERPNext deprecation marks

Edit `sunfish/apps/web/src/api/erpnext.ts`. For each report export consumed by Cohort 3:

- `getRentRoll()` (consumed by old RentRoll.tsx — now retired)
- `getProfitLoss()` (consumed by old PLReport.tsx — now retired)
- `exportProfitLoss()` (consumed by old PLReport CSV export — now retired)

Add JSDoc `@deprecated` + dev-mode console warning (mirror cohort-1 PR 4 §3.26 + cohort-2 PR 4 §3.29 shape):

```typescript
/**
 * @deprecated Cohort 3 (W#77) rebound the reports surface to cartridge endpoints.
 * The Bridge route this used has been removed; calls will 404. This export will be
 * removed in Cohort 4 (RB-12).
 */
```

(`@deprecated` mark stays even though the Bridge route is gone — the export is still defined in `erpnext.ts` until cohort-4 deletion; the JSDoc + console warning informs any non-cohort-3 caller.)

##### 4.10.2 E2E smoke extension

Extend cohort-1's Playwright CDP smoke test (location: `sunfish/apps/web/tests/e2e/` per cohort-1 PR 4 §3.27 + cohort-2 PR 4 §3.30):

- `TrialBalancePage` renders against a Bridge with ERPNext stopped (cartridge fixture seeded; provisionality flag exercised)
- `ArAgingPage` renders with both ByCustomer + ByProperty tabs functional
- `ProfitAndLossByPropertyPage` renders with per-property accordion functional
- `RentRoll v2` renders with new field surface (OccupancyStatus / DelinquencyBucket badges visible)

Network-panel assertion: each page makes zero requests to `/api/v1/erpnext/*` for report endpoints.

##### 4.10.3 Docs running log

Append cohort-3 section to `sunfish/apps/docs/anchor/anchor-react-rebind.md`:

- Pages rebound (4): RentRoll v2 (rewrite), ProfitAndLossByPropertyPage (rewrite of PLReport), TrialBalancePage (new), ArAgingPage (new)
- Bridge endpoints introduced (4 POST replacing 3 ERPNext-backed GETs): trial-balance, ar-aging-summary, profit-and-loss-by-property, rent-roll
- New shared component: `DaysDuePill` (if extracted in PR 5)
- Standing-pattern claims: `pattern-009` (formal, all PRs); `pattern-011-cartridge-read-via-post` (qualifying-candidate, PR 1 + Engineer prereq PR 0 are the first instance; ratify after second cartridge cluster ships, likely cohort-4 ApAgingSummary)
- ERPNext deprecation timeline: marks land here; deletion at Cohort 4 (RB-12)
- AP Aging deferred to cohort-4 (cartridge not yet shipped)

##### 4.10.4 Ledger flip

Update `shipyard/icm/_state/workstreams/W77-anchor-react-rebind-cohort-3.md` source row from `building` → `built`; run render-ledger; commit the rendered `active-workstreams.md` change.

##### 4.10.5 Status beacon

File `coordination/inbox/engineer-status-2026-05-XXTHH-MMZ-w77-cohort-3-built.md` (or `fed-status-*` if FED files it — verify routing convention) naming each PR's merge SHA, summarizing the pattern-011 candidate, and proposing the pattern for catalog ratification.

#### 4.11 Pattern conformance

```
@standing-pattern: pattern-009 (page rebind pair — formal post-cohort-1)
```

---

## 5. Cross-cluster integration

| Frontend page | Frontend hook | Frontend client | Bridge endpoint | Bridge handler | Cartridge / runner |
|---|---|---|---|---|---|
| `RentRollPage.tsx` (rewrite) | TanStack `useRentRoll(params)` | `sunfish/apps/web/src/api/reports.ts:runRentRoll` | `POST /api/v1/reports/rent-roll` | `Sunfish.Bridge.Reports.ReportsEndpoints.HandleRunRentRollAsync` | `Sunfish.Blocks.Reports.Cartridges.RentRoll.RentRollCartridge` via `IReportRunner.RunAsync(ReportKind.RentRoll, ...)` |
| `ProfitAndLossByPropertyPage.tsx` (new, replaces PLReport) | TanStack `useProfitAndLossByProperty(params)` | `runProfitAndLossByProperty` | `POST /api/v1/reports/profit-and-loss-by-property` | `HandleRunProfitAndLossByPropertyAsync` | `ProfitAndLossByPropertyCartridge` |
| `TrialBalancePage.tsx` (new) | TanStack `useTrialBalance(params)` | `runTrialBalance` | `POST /api/v1/reports/trial-balance` | `HandleRunTrialBalanceAsync` | `TrialBalanceCartridge` |
| `ArAgingPage.tsx` (new) | TanStack `useArAgingSummary(params)` | `runArAgingSummary` | `POST /api/v1/reports/ar-aging-summary` | `HandleRunArAgingSummaryAsync` | `ArAgingSummaryCartridge` |

All Bridge handlers resolve `TenantId` + `PrincipalId` via `ITenantContext` (cohort-1 + cohort-2 + ADR 0091 Rev 2 inherited); no tenant filter is accepted as a parameter.

---

## 6. Idempotency-key catalog

**Not applicable.** Cohort-3's surface is read-only POST (cartridge runs produce results; nothing persists). Naturally idempotent at the runner level (`IReportRunner.RunAsync` re-runs cleanly given same parameters).

**Follow-on consideration (out of cohort-3 scope):** caching layer for expensive cartridges (Trial Balance / ArAgingSummary on large tenants). If user behavior shows repeated identical calls, a follow-on PR adds a result-cache keyed on `(tenantId, reportKind, parameters-hash)` with TTL. Not in cohort-3 scope; logged as forward-watched (§7).

---

## 7. Forward-watched concerns (logged; not blocking cohort-3)

Drawn from FED scope survey + cohort-3 surface analysis. Cohort-3 does NOT close these; each becomes its own workstream or block-engineering follow-up.

1. **ApAgingSummary cartridge ship** — `Cartridges/ApAgingSummary/` does not exist; `ReportKind.ApAgingSummary` is reserved enum stub. Cohort-4 picks up after Engineer ships the cartridge.
2. **PDF export** — reserved in `ReportKind` enum comments; cohort-3 ships CSV export only. Future cohort or substrate workstream.
3. **`DaysDuePill` shared component** — extract in cohort-3 PR 5 if cohort-2 hasn't already; otherwise inline-duplicate v1.
4. **`ChartOfAccountsId` wire format** — Engineer's PR 0 contract-frozen beacon resolves whether `ChartId` serializes as plain string ULID or opaque object. FED PR 1 stubs as `string` and updates if Engineer's contract differs.
5. **`LastPaymentDate` + `PrepaidBalance` always null/0** — cross-cluster gap (RentRoll cartridge doesn't yet wire these from `blocks-financial-payments`). FED documents in the UI ("Payment history will appear after the next migration step") OR hides the columns. Follow-on workstream wires them.
6. **`pattern-009 §B1` read-POST carve-out** — if the standing-pattern catalog does not yet name a read-POST carve-out, ONR files a follow-on amendment proposing the catalog addition after sec-eng SPOT-CHECK on PR 1 confirms the disposition.
7. **`pattern-011-cartridge-read-via-post` ratification** — qualifying-candidate from PR 1 + Engineer prereq PR 0. Ratify after a second cartridge cluster ships through the same shape (cohort-4 ApAgingSummary expected).
8. **Result-caching layer on expensive cartridges** — TTL-based cache keyed on `(tenantId, reportKind, parameters-hash)`. Out of cohort-3 scope; follow-on if operational data shows repeated identical calls.
9. **Multi-export formats (PDF, XLSX)** — out of cohort-3 (CSV only). Future cohort.

---

## 8. Halt conditions (`engineer-question-*` or `fed-question-*` beacons; ONR-question for ONR-routable items)

### H1. PAO cohort-3 design direction not yet authored

**Symptom:** Pre-build §2.8 reveals `shipyard/_shared/design/cohort-3/` does not exist.

**Mitigation:** PR 1 (api layer) can ship without PAO direction (no design surface). PR 2-5 require design specs (provisionality banner placement, IsBalanced badge styling, Top-N delinquent ranking layout, DaysDuePill colored column headers, Export CSV button placement).

**Halt:** if PAO direction is not landing in cohort-2's design-cohort-3 window, **STOP** at PR 2 + file `fed-question-*-cohort-3-pao-design-blocked.md`. Admiral routes PAO to author. PR 2-5 can author DRAFT-ahead (cohort-2 DRAFT-ahead pattern) once PAO publishes.

### H2. Engineer prereq PR 0 not yet opened OR contract-frozen

**Symptom:** Pre-build §2.2 reveals no Engineer PR for the cartridge-runner endpoint family.

**Mitigation:** Authoring of FED PR 1 (api layer) is unblocked NOW — TypeScript types are authored against the spec in §"Engineer prereq PR 0" (this document is the contract). PR 1 cannot MERGE until Engineer PR 0 has merged.

**Halt:** if Engineer is dormant on PR 0 when FED reaches PR 1 merge time, **STOP** + file `engineer-question-*-cohort-3-pr-0-blocked.md` (FED files; Admiral routes). Engineer is the technical corps generalist + the prereq directive routes through Admiral.

### H3. Cross-cluster scope expansion — cartridge contract differs from FED survey

**Symptom:** Engineer's PR 0 contract-frozen beacon names cartridge parameter or result types that don't match FED's PR 1 TypeScript analogues (e.g., `ChartId` serializes as object, not string; or a cartridge gains a new field FED didn't anticipate).

**Mitigation:** Update PR 1 TypeScript types to match. Single commit amendment.

**Halt:** if the contract drift is substantive (e.g., a new required parameter that requires UX surfacing in the page), **STOP** + file `onr-question-*-cohort-3-contract-drift.md` (ONR files; Admiral re-routes ONR for a small amendment to this hand-off).

### H4. `ApAgingSummary` cartridge ships mid-cohort-3

**Symptom:** Engineer ships the AP Aging cartridge while cohort-3 is in flight; CIC considers adding ApAgingPage to cohort-3 scope.

**Mitigation:** Cohort-3 hand-off explicitly defers AP Aging to cohort-4. If CIC wants to add it, cohort-3 becomes 6-PR; this hand-off needs a Revision 2 amendment.

**Halt:** **STOP** + file `onr-question-*-cohort-3-ap-aging-inclusion.md` requesting Admiral to route a scope-expansion decision to CIC. Pre-auth scope frontmatter may need amendment.

### H5. `DaysDuePill` extraction blocked by cohort-2 merge window

**Symptom:** PR 5 needs `DaysDuePill` color logic for AR Aging coloring; cohort-2 PR 2 (`AccountingPage`) hasn't merged yet (DaysDuePill is inline inside AccountingPage.tsx in an unmerged branch).

**Mitigation:** Two options:
1. Inline-duplicate the ~10-LOC component in PR 5 (acceptable v1; cleanup PR later)
2. Hold PR 5 until cohort-2 merges + extract in a follow-on PR

**Halt:** no halt; FED decides at PR 5 kickoff time. Document the choice in PR 5 description.

### H6. ERPNext deprecation breaks a non-cohort-3 page

**Symptom:** Same shape as cohort-1 H8 / cohort-2 H8 — adding `@deprecated` to the three erpnext.ts report exports triggers a compile error or audit-check failure because a non-cohort-3 page (or test helper, story, feature-flag config) imports them.

**Mitigation:** TypeScript treats `@deprecated` as a warning, not an error; audit non-cohort-3 consumers; update or annotate as part of PR 5.

**Halt:** if a non-cohort-3 consumer is found with a legitimate use, **STOP** + file `engineer-question-*` (or `fed-question-*` depending on consumer ownership). Admiral rules on whether to defer the deprecation or rebind the consumer.

### H7. Playwright CDP infrastructure regression

**Symptom:** Same shape as cohort-1 H7 / cohort-2 H10 — the CDP harness isn't running at PR 5 close-out.

**Mitigation:** Manual-test checklist substitute; file follow-on workstream to wire CDP smoke testing.

**Halt:** not a hard halt; ship PR 5 with a manual-test checklist substitute if needed.

### H8. `pattern-009 §B1` read-POST carve-out not yet in standing-patterns catalog

**Symptom:** Sec-eng SPOT-CHECK on PR 1 finds that the catalog does not name a read-POST carve-out; flags PR 1 as ambiguous-pattern.

**Mitigation:** ONR files a follow-on `onr-status-*-pattern-009-read-post-carve-out-amendment.md` proposing the catalog addition. Sec-eng may self-attest GREEN on PR 1 with reference to the proposed amendment.

**Halt:** if sec-eng escalates beyond the amendment proposal (e.g., insists pattern-011-cartridge-read-via-post needs ratification BEFORE PR 1 merges), **STOP** + file `onr-question-*-cohort-3-pattern-011-pre-ratification.md`. Admiral rules — likely accepts ONR's amendment proposal in parallel with PR 1 merge.

---

## 9. Test plan summary

| PR | New Bridge tests | New frontend tests | Sec-eng SPOT-CHECK | Total |
|---|---|---|---|---|
| Engineer PR 0 (prereq; not cohort-3 scope) | ~10 | — | YES | ~10 |
| PR 1 (api layer) | — | ~6 | YES (first cohort-3 instance) | ~6 |
| PR 2 (RentRoll v2) | — | ~5 | — | ~5 |
| PR 3 (P&L by Property) | — | ~5 | — | ~5 |
| PR 4 (TrialBalance new) | — | ~5 | — | ~5 |
| PR 5 (ArAging new + close-out) | — | ~6 (+4 E2E smoke scenarios) | — | ~10 |
| **Total (FED only)** | **—** | **~27 unit + 4 E2E** | **1 sec-eng SPOT-CHECK** | **~31** |

### Cohort-level acceptance (PASS gate at end of PR 5)

**A1.** `dotnet build` succeeds for `Sunfish.Bridge` (after Engineer prereq PR 0 + sec-eng SPOT-CHECK GREEN).
**A2.** `pnpm --filter @sunfish/web test` passes all PR 1 + PR 2 + PR 3 + PR 4 + PR 5 frontend tests.
**A3.** `pnpm --filter @sunfish/web build` succeeds.
**A4.** E2E smoke test (cohort-1 + cohort-2 baseline + cohort-3 four new scenarios) passes against a seeded Bridge with ERPNext **NOT running**.
**A5.** Network-panel verification: each of the four report pages renders with NO `/api/v1/erpnext/*` calls for report endpoints.
**A6.** Workstream W#77 ledger row reads `built`; PR refs #N..#N+4 captured (Engineer PR 0 + 5 FED PRs).
**A7.** `coordination/inbox/engineer-status-*-w77-cohort-3-built.md` (or `fed-status-*`) beacon dropped.
**A8.** No outstanding `@deviation-from-spec:` flags without CIC acknowledgement.
**A9.** Sec-eng SPOT-CHECK GREEN on Engineer prereq PR 0 + FED PR 1.
**A10.** `pattern-011-cartridge-read-via-post` proposal: if PR 1 + Engineer prereq PR 0 ship clean, the W#77 status beacon includes a section proposing pattern catalog ratification at CIC's next standing-patterns review (or after cohort-4 ApAgingSummary qualifies the second-instance trigger).
**A11.** AP Aging deferral documented in the running log; cohort-4 hand-off (when authored) inherits AP Aging as its scope.

---

## 10. Dependencies + sequencing

**Critical path:** Engineer PR 0 → FED PR 1 → FED PR 2 → FED PR 3 → FED PR 4 → FED PR 5. Sequential for review-load smoothing + pattern-009 + pattern-011 candidacy tracking.

**Parallel work possible (FED's call at hand-off review):**
- FED PR 1 authoring can start as soon as Engineer's PR 0 contract is documented (TypeScript types lifted from this hand-off's §4.1)
- FED PR 1 can MERGE only after Engineer PR 0 has merged (CI integration tests need real endpoints)
- FED PR 2/3/4/5 can be opened as DRAFTs simultaneously once PAO design ships (cohort-2 DRAFT-ahead pattern)
- PR 2/3/4/5 are independent at the codebase level; sequential merge for review discipline

**External gates (non-blocking but noted):**
- W#72 blocks-reports Phase 1 MVP cartridges (4) on main — verified at pre-build §2.1
- PAO Track C cohort-3 design — gates PR 2-5 (Halt H1)
- Cohort-1 + cohort-2 precedents (AuthenticatedTenantPolicy + pattern-009 formal) on main — verified at pre-build §2.3
- ADR 0091 Rev 2 (Accepted) — Bridge tenant-isolation contract; Engineer prereq PR 0 honors

---

## 11. PASS gate (end-state for declaring W#77 `built`)

The hand-off ships when ALL of the following are true:

1. **Engineer PR 0 + FED PRs 1 + 2 + 3 + 4 + 5 merged to `main`** in sequence (or per FED-elected parallelization).
2. **All Bridge endpoint + block tests pass** (A1).
3. **Frontend tests pass** (A2 + A3).
4. **E2E smoke test passes** for the four new cohort-3 scenarios OR (per H7) a manual-test checklist is published with PR 5 and CIC accepts the manual-test substitute.
5. **Network-panel verification:** each of the four report pages renders against a Bridge with ERPNext stopped, with NO `/api/v1/erpnext/*` report calls.
6. **ERPNext route deletion deferred to Cohort 4** — Cohort-3 only deprecates (PR 5).
7. **`sunfish/apps/docs/anchor/anchor-react-rebind.md` cohort-3 section published** + linked in the docs TOC.
8. **Workstream W#77 ledger row reads `built`** with PR refs (6 entries: Engineer PR 0 + FED PR 1-5).
9. **`coordination/inbox/engineer-status-*-w77-cohort-3-built.md` (or `fed-status-*`)** beacon dropped.
10. **No outstanding `@deviation-from-spec:` flags** without CIC acknowledgement.
11. **`pattern-011-cartridge-read-via-post` catalog candidacy** — if PR 1 + Engineer prereq PR 0 ship clean, the W#77 status beacon includes a section proposing pattern ratification (qualifying-candidate; ratifies after a second cartridge cluster ships through the same shape).
12. **AP Aging deferral** explicitly documented in the running log + cohort-4 hand-off intake.

When the PASS gate is met, the next cohort hand-off can proceed:

- `anchor-react-rebind-cohort-4-stage06-handoff.md` — AP Aging page (gated on ApAgingSummary cartridge shipping) + ERPNext route deletion (RB-12 final close-out for the entire rebind initiative).

---

## 12. Companion artifacts

**PAO Track C cohort-3 design direction** (Admiral routes to PAO during cohort-2 review window) ships under `shipyard/_shared/design/cohort-3/`. FED's execution of PR 2-5 consumes:
- This hand-off (engineering contract — surface mapping + halt conditions)
- PAO Track C output (UX direction + ASCII mockups + design-token specs — design contract)
- Engineer prereq PR 0 (Bridge endpoint contract)

All three must land before FED begins PR 2-5 execution. PR 1 (api layer) can start earlier.

---

**End of hand-off.**

— ONR, 2026-05-20T11:45Z
