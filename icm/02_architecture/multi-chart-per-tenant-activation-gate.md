# Multi-Chart-Per-Tenant Activation Gate

**Authored by:** ONR (V3 batch item #5)
**Requester:** Admiral (per `admiral-directive-2026-05-21T12-45Z` item #5)
**Authored at:** 2026-05-21T12-55Z
**Status:** canonical reference (formalizes demand-trigger condition for V2 #5 multi-chart work)

---

## Purpose

V2 #5 research (shipyard#73) recommended "ship multi-chart-per-tenant demand-driven (first customer signal); not blocking MVP." This document FORMALIZES the activation trigger so the work doesn't sit indefinitely and doesn't accidentally activate before substrate-readiness.

When ANY of the **Activation Triggers** below fires, ONR files `cic-question-*-multi-chart-activation.md` to CIC for confirmation. CIC ratifies → work begins per V2 #5 §4 4-phase migration plan.

---

## Activation Triggers (any of these fires the gate)

### Trigger A — Customer dual-reporting signal

**Condition:** A current or prospective Sunfish customer asks for support BOTH US GAAP AND IFRS reporting on the same tenant.

**Detection:** customer email / sales-call note / support ticket mentioning "dual-reporting" / "GAAP and IFRS" / "parallel charts" / "secondary chart of accounts" / "Schedule E + EU-equivalent".

**Action when fires:**
1. CIC reviews customer context (is this a serious prospect; what's the close-cost?)
2. If yes → file `cic-status-multi-chart-activated.md` + dispatch Engineer per V2 #5 §4 Phase 1

### Trigger B — Operating-model separation signal

**Condition:** A customer operates two distinct business lines under one Sunfish tenant (e.g., property management LLC + separate consulting business; rental properties + landscaping side business) and asks for separated ledgers.

**Detection:** customer narrative / support request mentioning "separate books" / "two businesses" / "side business chart" / "LLC #1 + LLC #2 in one Sunfish".

**Action when fires:**
1. CIC reviews — is the separation accounting-line OR full multi-tenant (separate sign-in)?
2. If accounting-line separation → multi-chart applies; dispatch per V2 #5
3. If full multi-tenant separation → out of multi-chart scope; route to multi-tenant admin surface (V2 #6 candidate C2)

### Trigger C — Cohort-4 audit-trail viewer surfaces the gap

**Condition:** When cohort-4 ships (V3 #1 deliverable + CIC ratification), the audit-trail viewer exposes audit events that span multiple charts (if any tenant has migrated to multi-chart manually outside Sunfish + we surface their audit records). UI shows "no chart filter" + becomes load-bearing UX request.

**Detection:** post-cohort-4-launch feedback noting "I can't filter audit events by chart" OR "I have two charts in my mind but Sunfish only shows one".

**Action when fires:**
1. CIC reviews — likely defer until Trigger A or B also fires (cohort-4 launch alone may not justify the full multi-chart investment)

### Trigger D — Mid-cycle chart redesign signal

**Condition:** A customer's accountant migrates from one chart of accounts to a refined version + needs dual-posting during the transition.

**Detection:** customer support / accountant-facing inquiry about "chart redesign" / "new chart of accounts" / "dual-posting" / "old chart + new chart".

**Action when fires:**
1. CIC reviews — is the transition immediate (next month) OR multi-quarter?
2. If immediate → multi-chart applies as a transitional tool; dispatch per V2 #5
3. If long-horizon → defer; recommend customer ship the redesign as an ADR-style internal migration

### Trigger E — Multi-currency operations signal

**Condition:** Customer operates in multiple currencies + asks for separate ledger maintenance per currency.

**Detection:** support / sales-call note mentioning "EUR operations" / "USD primary" / "currency conversion" / "secondary currency chart".

**Action when fires:**
1. CIC reviews — multi-currency is a lower priority than dual-reporting (Trigger A) per V2 #5 §1.4
2. Likely defer until Trigger A also fires; multi-currency builds on the multi-chart substrate

### Trigger F — Investor / sales-team explicit request

**Condition:** Investor pre-meeting OR sales-team prep names multi-chart as a deal-breaker or critical differentiator.

**Detection:** investor-meeting-prep doc / sales-call note flagging multi-chart as needed-to-close.

**Action when fires:**
1. CIC reviews — assess deal-size vs Engineer effort tradeoff
2. If deal-bound → activate; if speculative → defer per V2 #5 recommendation

---

## What does NOT fire the gate

- **Internal engineering aesthetics** ("we'd architect it this way going forward"). Engineering preference alone doesn't justify the Engineer+FED time.
- **Cohort-3 reports cluster shipping.** Reports cluster surfaces single-chart data; multi-chart adds complexity not directly demanded by reports cluster.
- **W#60 P4 PR 2 JournalEntry POST.** The endpoint accepts `chart_code` in request body (per V3 #2 design doc) — if absent, defaults to the tenant's default chart. v1 single-chart customers see no change.

The gate prevents speculative work; the triggers above are concrete customer signals or business decisions that justify the ~10-14h Engineer + FED effort.

---

## When the gate fires — the activation sequence

1. **ONR files** `cic-question-*-multi-chart-activation.md` with:
   - The trigger (A/B/C/D/E/F) that fired
   - Customer context (anonymized if needed)
   - Effort estimate per V2 #5 §4 (~10-14h Engineer; ~5-8h FED)
   - Cohort assignment recommendation (cohort-N+1 OR slot into current cohort?)

2. **CIC ratifies or declines** within ~24h.

3. **If ratified:** Admiral dispatches:
   - `admiral-directive-*-engineer-multi-chart-phase-1.md` (API + middleware; ~3-4h)
   - `admiral-directive-*-fed-multi-chart-phase-2.md` (chart selector UI; ~2-3h)
   - `admiral-directive-*-fed-multi-chart-phase-3.md` (chart admin page; ~3-4h; optional based on demand)

4. **If declined:** ONR logs the deferral reason in this document (append to §"Deferral Log" below) so future Triggers know the prior signals.

---

## Pre-activation prerequisites (must be on main before activation)

These are SHIPPED today (verified 2026-05-21T12:55Z):

- ✅ `IChartCatalogService.GetChartIdAsync(TenantId)` (PR#67; main)
- ✅ Cohort-2 PR 0d `IJournalStore` tenant-keyed (PR#64; main)
- ✅ Cohort-2 PR 0a `IInvoiceRepository` tenant-keyed with `chart_id` field on Invoice (PR#52; main)
- ✅ ADR 0092 §"Step 2 EF Core query-filter convention" — substrate semantics

These are PENDING (V3 #1+ work; Engineer ships independent of multi-chart activation):

- ⏳ ADR 0091 Step 2.0 DbContext rewrite (V3 #3 sequencing plan; Phase 2)
- ⏳ ADR 0092 Step 2 per-cluster EF Core query-filter (V3 #3 Phase 3)
- ⏳ Cohort-4 audit-trail viewer (V3 #1)

**Activation can fire without the pending items shipped** — multi-chart Phase 1 (API + middleware) is independent of ADR 0091 Step 2.0. However, multi-chart Phase 2/3 (frontend chart selector + chart admin) benefits from Step 2.0's narrowed ITenantContext (cleaner DI registration).

---

## Engineer + FED scope IF activated

Per V2 #5 §4 4-phase migration:

### Phase 1 — Backend API + middleware (~3-4h Engineer)

- `IChartCatalogService.ListChartsAsync(TenantId)` + `ResolveChartAsync(TenantId, string code)` + `GetDefaultChartAsync(TenantId)` extensions
- `ChartContextMiddleware` (reads `X-Sunfish-Chart-Code` header; rejects cross-tenant codes; sets `ChartContext.Current`)
- `ChartSummary` record + `ChartStatus` enum
- ≥6 integration tests (header absent → default; header present + valid; header present + invalid; header present + cross-tenant → 403; ListCharts returns tenant-scoped only; ResolveChart returns null on mismatch)

### Phase 2 — Frontend chart selector (~2-3h FED)

- `apps/web/src/hooks/useChartContext.tsx` (TanStack hook + localStorage)
- Chart selector dropdown in app header
- `X-Sunfish-Chart-Code` header included in all API requests
- Disabled when `ListCharts` returns 1 chart

### Phase 3 — Chart admin (~3-4h FED; optional)

- `apps/web/src/pages/ChartAdminPage.tsx` (create new chart + archive + set default)
- `POST /api/v1/financial/charts` + `PATCH /api/v1/financial/charts/{id}/archive` + `PATCH /api/v1/financial/charts/{id}/set-default`
- Owner / Spouse / Accountant role-gated

### Phase 4 — Cross-chart reporting (future; demand-driven within demand-driven)

- Reports cluster (cohort-3) filtered by chart selector — already supported via `X-Sunfish-Chart-Code` header per Phase 1; no new substrate work
- Consolidated reporting across multiple charts → separate ADR

---

## Activation effort estimate (per CIC question)

When ONR files `cic-question-*-multi-chart-activation.md`, include this effort breakdown:

```
Total: ~10-14h (Phase 1 + 2 mandatory)
+ ~3-4h FED (Phase 3 if customer needs chart admin UI)
+ deferred (Phase 4 cross-chart reporting; demand-driven within demand-driven)

Sequenced over ~1 week:
- Day 1-2: Phase 1 backend (Engineer)
- Day 3-4: Phase 2 frontend (FED)
- Day 5 (optional): Phase 3 chart admin (FED)
- Day 5-7: integration tests + customer feedback + ship
```

---

## Deferral Log

Tracks deferred activation events. Future Triggers can reference prior deferrals to avoid re-deciding the same question.

| Date | Trigger | Customer signal | CIC decision | Rationale |
|---|---|---|---|---|
| — | — | — | — | (No deferral events yet; gate authored 2026-05-21) |

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T12-45Z-onr-v3-batch-cohort-4-and-pattern-renumber.md` item #5
2. V2 #5 multi-chart-per-tenant research (shipyard#73) — 5 use cases + 4-phase migration plan
3. `shipyard/packages/blocks-financial-ledger/Services/IChartCatalogService.cs` (PR#67) — v1 substrate
4. ADR 0091 R2 + ADR 0092 — substrate consumption patterns

---

## What ONR does next

V3 #5 deliverable complete (this document). Files `onr-status-*-research-queue-v3-item-5-multi-chart-gate-complete.md`. Proceeds to V3 #7 (cohort-3 hand-off readiness review; ~2-3h).

— ONR, 2026-05-21T12:55Z
