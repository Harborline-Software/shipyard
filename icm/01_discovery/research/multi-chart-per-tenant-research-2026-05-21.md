# ONR research — Multi-chart-per-tenant readiness (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #4 — multi-chart research)
**Authored by:** ONR
**Authored at:** 2026-05-21T12-26Z
**Status:** draft

---

## Scope of investigation

- **In scope:** when Sunfish customers need multi-chart-per-tenant; API expansion design for `IChartCatalogService`; frontend chart-selector UI sketch; header validation server-side; migration story v1 (single-chart default) → v2 (multi-chart).
- **Out of scope:** implementation (Engineer's territory); chart-of-accounts schema design (existing per `blocks-financial-ledger`); cross-chart consolidation reporting (future ADR if customers need it).
- **Authoritative sources consulted:** Admiral 07:40Z Option C ruling (referenced by directive); `IChartCatalogService` shipped at shipyard#67 (per c821347 main HEAD); cohort-2 PR 0d (Journal tenant-keyed); cohort-2 PR 0a (Invoice tenant-keyed with chart_id field).
- **Success looks like:** when customer scenarios emerge that need multi-chart, Engineer can extend `IChartCatalogService` + add chart-selector UI + header validation using this research as scaffold.

---

## TL;DR

1. **Multi-chart use cases (real, not hypothetical):**
   - **GAAP + IFRS dual-reporting** — companies that file under both US GAAP and IFRS need parallel charts with different account hierarchies + period rules
   - **Property-mgmt + main-business separation** — landlord operates both a property management LLC + a separate small business; wants one Sunfish tenant for both with separate charts
   - **Side-business carve-out** — landlord adds a side service (e.g., maintenance subcontracting; cleaning services); separate chart isolates that line of business for tax purposes
   - **Multi-currency operations** — single tenant with USD-primary + EUR-secondary chart for European holdings
   - **Mid-cycle re-chart-of-accounts redesign** — accountant migrates from one chart to a refined one; both charts exist during transition; entries are dual-posted briefly

2. **Per Admiral 07:40Z Option C ruling:** frontend exposes chart-selector UI; selected chart-code travels in HTTP header (e.g., `X-Sunfish-Chart-Code`); NOT a tenant-bypass — server still validates the chart belongs to the tenant.

3. **API expansion (ONR design):**
   - `IChartCatalogService.ListChartsAsync(TenantId)` → returns `IReadOnlyList<ChartSummary>` (active charts for this tenant)
   - `IChartCatalogService.ResolveChartAsync(TenantId, string chartCode)` → returns `ChartOfAccountsId` (or null if mismatch)
   - `IChartCatalogService.GetDefaultChartAsync(TenantId)` → returns the tenant's primary chart (the "default if header absent" path)

4. **Header validation pattern:**
   - Middleware reads `X-Sunfish-Chart-Code` header; if absent, sets `ChartContext` to default chart (`GetDefaultChartAsync(tenant.TenantId)`)
   - If present, calls `ResolveChartAsync(tenant.TenantId, code)`; on null → return 403 (chart not on this tenant)
   - Resolved `ChartOfAccountsId` flows into a `ChartContext` scoped service consumed by Bridge handlers + repository queries

5. **Frontend chart-selector UI sketch:**
   - Dropdown in app header (next to tenant indicator); shows chart code + chart name
   - "Switch chart" → updates active chart in app state; subsequent API calls include header
   - Persisted per-user preference (last-active chart per tenant)
   - Disabled if `ListChartsAsync` returns single chart (no toggle needed)

6. **v1 → v2 migration:** v1 ships single-chart default; v2 adds API + UI + header validation; v2 is backward-compatible (single-chart tenants see no change; header absent → default chart selected).

7. **ONR recommendation:** ship multi-chart in **cohort-4 or W#60 P4 PR 2 era**. Trigger: first customer signal (GAAP+IFRS dual-reporting OR property-mgmt+main-biz separation). Not blocking MVP; demand-driven.

---

## 1. Use case inventory

### 1.1 GAAP + IFRS dual-reporting (highest forensic value)

**Scenario:** US-based landlord with European holdings + EU-based accountant filing IFRS for EU operations. Needs parallel charts.

**Pattern:** dual-posting — each journal entry posts to both charts simultaneously OR primary chart + IFRS-overlay mapping at report time.

**Sunfish impact:** trial balance + P&L by Property reports gain a "by chart" filter; chart selector at app header.

### 1.2 Property-mgmt + main-business separation

**Scenario:** landlord operates LLC #1 for properties + LLC #2 for separate consulting business. Wants one Sunfish login that sees both with separate ledgers.

**Pattern:** separate charts of accounts, separate tax-year boundaries, separate reports.

**Sunfish impact:** workflow-dependent (Sunfish today doesn't model multi-LLC under one tenant; multi-chart is the lightweight stepping stone before multi-LLC support).

### 1.3 Side-business carve-out

**Scenario:** landlord adds a side service business (e.g., subcontracted maintenance for other landlords' properties). Wants separate revenue + expense tracking.

**Pattern:** secondary chart for the side business; entries flow into one or the other.

**Sunfish impact:** chart selector visible; reports filter by chart.

### 1.4 Multi-currency operations

**Scenario:** tenant with USD primary holdings + EUR secondary (e.g., one EU property). Reports in primary currency with conversion at report date.

**Pattern:** secondary chart in EUR + currency-conversion overlay; not strictly multi-chart but uses the same infrastructure.

**Sunfish impact:** lower priority than 1.1-1.3; revisit when first multi-currency tenant onboards.

### 1.5 Mid-cycle chart redesign

**Scenario:** accountant decides current chart needs refinement (e.g., split "Repairs" into "Routine Maintenance" vs "Capital Improvements"). New chart introduced; entries dual-posted for transition period; old chart archived after transition.

**Pattern:** old chart + new chart both active for 3-6 months; chart selector picks which for new entries.

**Sunfish impact:** archive lifecycle on `ChartSummary.Status` enum (`Active | Archived | Draft`).

---

## 2. API expansion design

### 2.1 Proposed `IChartCatalogService` extensions

Current `IChartCatalogService` (shipped 2026-05-21 at PR #67):

```csharp
public interface IChartCatalogService
{
    // Existing — single-chart lookup
    Task<ChartOfAccountsId?> GetChartIdAsync(TenantId tenantId, CancellationToken ct);
}
```

ONR's proposed v2 extensions:

```csharp
public interface IChartCatalogService
{
    // Existing — returns the DEFAULT chart for the tenant
    Task<ChartOfAccountsId?> GetChartIdAsync(TenantId tenantId, CancellationToken ct);

    // NEW — list all charts visible to tenant
    Task<IReadOnlyList<ChartSummary>> ListChartsAsync(
        TenantId tenantId,
        CancellationToken ct);

    // NEW — resolve a caller-supplied chart code to a chart id (returns null if not on tenant)
    Task<ChartOfAccountsId?> ResolveChartAsync(
        TenantId tenantId,
        string chartCode,
        CancellationToken ct);

    // NEW — explicit default-chart accessor (clarifies semantics vs GetChartIdAsync)
    Task<ChartOfAccountsId?> GetDefaultChartAsync(
        TenantId tenantId,
        CancellationToken ct);
}

public sealed record ChartSummary(
    ChartOfAccountsId Id,
    string Code,         // human-friendly e.g. "PRIMARY", "IFRS", "SIDE"
    string Name,         // e.g. "Property Management Operations"
    ChartStatus Status,  // Active | Archived | Draft
    bool IsDefault);

public enum ChartStatus { Active, Archived, Draft }
```

**Naming note:** if `GetChartIdAsync` was the v1 accessor for "the default chart", consider renaming to `GetDefaultChartIdAsync` for clarity in v2. Engineer decides.

### 2.2 Header validation middleware

New file: `signal-bridge/Sunfish.Bridge/Middleware/ChartContextMiddleware.cs`

```csharp
public sealed class ChartContextMiddleware
{
    private const string ChartHeaderName = "X-Sunfish-Chart-Code";

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IChartCatalogService charts,
        ChartContext chartContext,    // Scoped DI service
        RequestDelegate next)
    {
        var tenantId = new TenantId(tenantContext.TenantId);

        var chartCode = context.Request.Headers[ChartHeaderName].FirstOrDefault();

        ChartOfAccountsId? chartId;
        if (string.IsNullOrEmpty(chartCode))
        {
            // Header absent → use default chart
            chartId = await charts.GetDefaultChartAsync(tenantId, context.RequestAborted);
        }
        else
        {
            // Header present → resolve + tenant-bind
            chartId = await charts.ResolveChartAsync(tenantId, chartCode, context.RequestAborted);
            if (chartId is null)
            {
                // Chart code doesn't resolve to a chart on this tenant
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "chart_not_found_for_tenant",
                    chart_code = chartCode,
                });
                return;
            }
        }

        chartContext.SetChart(chartId);
        await next(context);
    }
}

public sealed class ChartContext
{
    public ChartOfAccountsId? Current { get; private set; }
    public void SetChart(ChartOfAccountsId? id) => Current = id;
}
```

Wire in `Program.cs` between `AuthenticatedTenantPolicy` middleware and Bridge handlers.

### 2.3 Audit emission on chart-not-found

Per V2 #3 retrofit pattern, emit `AuditEventType.ChartNotFoundForTenant` audit record on resolve-failure (forensics value for chart-code probe attacks).

---

## 3. Frontend chart-selector UI sketch

```
┌─────────────────────────────────────────────────────┐
│ Sunfish | [Tenant: Acme Properties ▾] [Chart: PRIMARY ▾] │  <- app header
├─────────────────────────────────────────────────────┤
│                                                       │
│ [main content area]                                  │
│                                                       │
└─────────────────────────────────────────────────────┘
```

Chart dropdown opens to:

```
┌──────────────────────────────┐
│ ▼ Chart: PRIMARY              │
├──────────────────────────────┤
│ ● PRIMARY                     │
│   Property Management Operations │
│                              │
│ ○ IFRS                        │
│   IFRS Reporting (Q2 2026+) │
│                              │
│ ○ SIDE                        │
│   Subcontracted Maintenance  │
├──────────────────────────────┤
│ Manage charts...              │  <- link to chart admin
└──────────────────────────────┘
```

State management:
- `useChartContext()` hook (TanStack); fetches `GET /api/v1/financial/charts` (returns ListChartsAsync result)
- Active chart stored in localStorage per (tenant, user); restored on app load
- Chart selector emits `chart-changed` event; subscribers refetch their data (TanStack `invalidateQueries`)
- Disabled (greyed-out) if only one chart exists for the tenant

---

## 4. Migration story v1 → v2

### Phase 1 — v2 API + middleware (Engineer; ~3-4h)

- Extend `IChartCatalogService` with `ListChartsAsync` + `ResolveChartAsync` + `GetDefaultChartAsync`
- Ship `ChartContextMiddleware` + `ChartContext` scoped service
- All existing Bridge handlers consume `ChartContext.Current` instead of calling `IChartCatalogService.GetChartIdAsync` directly
- v1 callers (no header) → continue to receive default chart (backward-compatible)

### Phase 2 — Frontend chart selector (FED; ~2-3h)

- Chart selector UI in app header (per §3 sketch)
- `useChartContext()` hook + localStorage persistence
- API calls include `X-Sunfish-Chart-Code` header for the active chart
- Disabled when single-chart tenant

### Phase 3 — Multi-chart authoring (FED; ~3-4h)

- Chart admin page: create new chart, archive existing chart, set default
- `POST /api/v1/financial/charts` + `PATCH /api/v1/financial/charts/{id}/archive` + `PATCH /api/v1/financial/charts/{id}/set-default`
- Permissions: Owner / Spouse / Accountant roles only

### Phase 4 — Cross-chart reporting (future; demand-driven)

- Trial balance + P&L by Property filtered by chart selector (already supported via header in Phases 1-2)
- Consolidated reporting across multiple charts — separate ADR if customers ask

### Backward compatibility

v1 single-chart tenants:
- API: no header → default chart resolves correctly
- Frontend: chart selector disabled if `ListChartsAsync` returns 1
- Migration: zero customer action required

v2 multi-chart tenants:
- API: include `X-Sunfish-Chart-Code` header on each request
- Frontend: chart selector active; per-user preference persisted

---

## 5. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **Naming — rename `GetChartIdAsync` to `GetDefaultChartIdAsync` for clarity?** ONR recommends YES.
2. **`ChartSummary.Code` uniqueness scope — per-tenant (ONR recommended) vs global?** Per-tenant allows tenant A's "PRIMARY" + tenant B's "PRIMARY" to coexist.
3. **`ChartContextMiddleware` placement — between auth + handlers (ONR recommended) vs as a filter on specific endpoint groups?** Global middleware is simpler; filter is more selective but loses default-chart semantics on non-financial endpoints.

### For security-engineering council

1. **`X-Sunfish-Chart-Code` header rejection on chart-not-found — 403 (ONR recommended) vs 404 (chart-as-resource)?** 403 implies "you can't access this chart"; 404 implies "no such chart". ONR's read: 403 is correct (chart exists but not on this tenant); 404 leaks info ("yes, that chart exists in some other tenant").
2. **Audit emission on chart-not-found — `AuditEventType.ChartNotFoundForTenant` (ONR recommended) vs no audit (low forensics value)?** Forensics value is real if attackers probe for chart-code enumeration.
3. **Cross-chart query isolation — does `HasQueryFilter` on JournalEntry filter by BOTH `(TenantId == captured) AND (ChartId == chartContext.Current)`?** ONR recommends YES; chart isolation should be type-system-enforceable like tenant isolation.

### For CIC

1. **Multi-chart shipping trigger — demand-driven (ONR recommended) vs proactive in cohort-4?** Customer signals (GAAP+IFRS dual-reporting, property-mgmt+side-biz) inform timing.

---

## 6. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Chart selector UX confusion — users post entries to wrong chart | Medium | Medium (data correction needed) | Confirmation modal on chart switch; "Last entry to PRIMARY at 14:23" indicator |
| Cross-chart query leak (forgetting ChartContext.Current filter) | Medium | High (cross-chart data exposure) | HasQueryFilter on ChartId mirrors tenant filter; Step 4a analyzer extension |
| Backward compat broken if existing tenants suddenly required to send header | Low | High (production breakage) | Default-chart resolution path tested in Phase 1; no migration required |
| Chart admin permissions misconfiguration — non-Owner creates chart | Medium | Medium (governance issue) | Role check at endpoint; sec-eng SPOT-CHECK on chart admin endpoints |
| `ChartCode` collision across tenants | Low | Low (per-tenant scoping) | Per-tenant uniqueness; `(TenantId, Code)` composite index |
| Per-user chart preference lost on browser clear | Low | Low (defaults to tenant default) | localStorage; acceptable degraded behavior |

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #4 (multi-chart research)
2. `shipyard/packages/blocks-financial-ledger/Services/IChartCatalogService.cs` (PR #67 — current v1 single-chart accessor)
3. Admiral 07:40Z Option C ruling (referenced by directive; X-Sunfish-Chart-Code header pattern)
4. Cohort-2 PR 0d journal tenant-keyed contract (substrate ready)
5. Cohort-2 PR 0a invoice tenant-keyed contract (chart_id field already on Invoice)

---

## 8. What ONR does next

Returns to V2 research queue. Per proceed-continuously discipline:

- Item #5 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-item-5-multi-chart-complete.md`.
- Proceed to V2 #6: Cohort-4 scope survey (~4-6h).

— ONR, 2026-05-21T12:26Z
